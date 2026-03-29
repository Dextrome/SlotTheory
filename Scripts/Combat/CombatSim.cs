using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public enum WaveResult { Ongoing, WaveComplete, Loss }

/// <summary>
/// Orchestrates wave execution each frame. Called by GameController._Process().
/// Enemy movement is self-handled by EnemyInstance._Process() via PathFollow2D.
/// </summary>
public class CombatSim
{
    private readonly struct MineAnchor
    {
        public Vector2 Position { get; }
        public float Score { get; }

        public MineAnchor(Vector2 position, float score)
        {
            Position = position;
            Score = score;
        }
    }

    private sealed class ActiveMine
    {
        public ulong Id { get; init; }
        public TowerInstance Owner { get; init; } = null!;
        public Vector2 Position { get; init; }
        public float DamageScale { get; init; } = 1f;
        public bool IsMiniMine { get; init; }
        public int MaxCharges { get; init; } = Balance.RiftMineChargesPerMine;
        public float ArmRemaining { get; set; }
        public float RearmRemaining { get; set; }
        public int ChargesRemaining { get; set; } = Balance.RiftMineChargesPerMine;
        public RiftMineVisual? Visual { get; init; }
    }

    private sealed class ActiveUndertow
    {
        public ITowerView SourceTower { get; init; } = null!;
        public TowerInstance? SourceNode { get; init; }
        public EnemyInstance Target { get; init; } = null!;
        public float TotalDuration { get; init; }
        public float Remaining { get; set; }
        public float TotalPullDistance { get; init; }
        public float PulledDistance { get; set; }
        public float SlowFactor { get; init; }
        public bool IsSecondary { get; init; }
        public bool EnableEndpointPulse { get; init; }
        public bool IsFollowup { get; init; }
        public float DelayRemaining { get; set; }
        public UndertowTetherVfx? TetherVfx { get; set; }
    }

    private sealed class ActiveAfterimage
    {
        public ITowerView SourceTower { get; init; } = null!;
        public TowerInstance? SourceNode { get; init; }
        public Vector2 Position { get; set; }
        public float SeedDamage { get; set; }
        public float DelayTotal { get; set; }
        public float DelayRemaining { get; set; }
        public AfterimageImprintVfx? Visual { get; set; }
    }

    private readonly RunState _state;
    private float _spawnTimer = 0f;
    private readonly Queue<string> _spawnQueue = new();
    private bool _walkerNextIsFirst = true; // alternates short/long gap for basic_walker pairs
    private float _initialSpawnDelay = 0f;
    private float _killComboTimer = 0f;
    private int _killComboCount = 0;
    private readonly List<ActiveMine> _activeMines = new();
    private readonly List<MineAnchor> _mineAnchors = new();
    private readonly Dictionary<ulong, int> _riftBurstFastPlantsUsed = new();
    private ulong _nextMineId = 1;
    private readonly Random _mineRng;
    private readonly List<ActiveUndertow> _activeUndertows = new();
    private readonly Dictionary<ulong, float> _undertowRecentRemaining = new();
    private readonly Dictionary<ulong, float> _undertowRetargetLockoutRemaining = new();
    private readonly List<ActiveAfterimage> _activeAfterimages = new();
    private readonly LatchNestParasiteController<EnemyInstance> _latchParasites = new();
    private readonly Dictionary<ulong, LatchParasiteVfx> _latchParasiteVisuals = new();

    // Wildfire fire trail segments -- managed like active mines but simpler (no charges, no targeting)
    private sealed class FireTrailSegment
    {
        public Vector2 Position { get; init; }
        public Vector2 Direction { get; init; }
        public float TotalLifetime { get; init; }
        public float LifetimeRemaining { get; set; }
        public float DamagePerSecond { get; init; }
        public int OwnerSlotIndex { get; init; }
        public WildfireTrailVfx? Visual { get; init; }
    }
    private readonly List<FireTrailSegment> _activeFireTrails = new();
    // Wildfire trail number readability: accumulate fractional trail damage per enemy globally
    // across all overlapping segments, then emit numbers once full HP chunks build up.
    private readonly Dictionary<ulong, float> _wildfireTrailDmgAccumulator = new();
    private static readonly Color WildfireFireColor = new(1.0f, 0.55f, 0.12f);
    private static readonly Color AfterimageColor = new(0.72f, 0.88f, 1.00f);
    private bool _afterimageFollowupsConsumed;

    // Set externally by GameController when an enemy scene is needed
    public PackedScene? EnemyScene { get; set; }

    // Set externally - the Path2D node enemies are added to as PathFollow2D children
    public Path2D? LanePath { get; set; }

    public CombatSim(RunState state)
    {
        _state = state;
        int seed = state.RngSeed != 0 ? state.RngSeed : System.Environment.TickCount;
        _mineRng = new Random(unchecked(seed * 1103515245 + 12345));
    }

    // Injected by GameController after scene is ready
    public SoundManager? Sounds { get; set; }

    /// <summary>When true: damage is instant, no visuals spawned, enemies don't self-move.</summary>
    public bool BotMode { get; set; }
    public float InitialSpawnDelay { get => _initialSpawnDelay; set => _initialSpawnDelay = Mathf.Max(0f, value); }

    /// <summary>Lightweight reset used on run restart (before wave is loaded).</summary>
    public void ResetForWave()
    {
        _spawnTimer = _initialSpawnDelay;
        _spawnQueue.Clear();
        _walkerNextIsFirst = true;
        _killComboTimer = 0f;
        _killComboCount = 0;
        _riftBurstFastPlantsUsed.Clear();
        ClearMines();
        ClearFireTrails();
        ClearUndertowEffects();
        ClearAfterimages();
        ClearLatchParasites();
        RebuildMineAnchors();
    }

    /// <summary>Full reset + build spawn queue. Call after WaveSystem.LoadWave().</summary>
    public void ResetForWave(WaveSystem ws)
    {
        _spawnTimer = _initialSpawnDelay;
        _spawnQueue.Clear();
        _walkerNextIsFirst = true;
        _killComboTimer = 0f;
        _killComboCount = 0;
        _riftBurstFastPlantsUsed.Clear();
        ClearMines();
        ClearFireTrails();
        ClearUndertowEffects();
        ClearAfterimages();
        ClearLatchParasites();
        RebuildMineAnchors();

        int walkers   = ws.GetWalkerCount();
        int tankies   = ws.GetTankyCount();
        int swifties  = ws.GetSwiftCount();
        int splitters = ws.GetSplitterCount();
        int reversers = ws.GetReverseCount();
        int drones    = ws.GetShieldDroneCount();
        int total     = walkers + tankies + swifties + splitters + reversers + drones;

        // Build a per-slot type array; fill with basics then overlay other types
        var slots = new string[total];
        for (int i = 0; i < total; i++) slots[i] = "basic_walker";

        if (ws.GetClumpArmored() && tankies > 0)
        {
            // Group all armored enemies into one block, starting at the 1/3 mark.
            // Creates a panic spike: warm-up basics → armored wall → cleanup basics.
            int blockStart = total / 3;
            for (int i = blockStart; i < blockStart + tankies; i++)
                slots[i] = "armored_walker";
        }
        else if (tankies > 0)
        {
            // Spread tankies evenly
            for (int t = 0; t < tankies; t++)
            {
                int ideal = (int)Math.Round((t + 0.5) * total / tankies);
                for (int d = 0; d < total; d++)
                {
                    int s = (ideal + d) % total;
                    if (slots[s] == "basic_walker") { slots[s] = "armored_walker"; break; }
                }
            }
        }

        if (swifties > 0)
        {
            // Spread swift walkers evenly, skipping already-assigned slots
            for (int sw = 0; sw < swifties; sw++)
            {
                int ideal = (int)Math.Round((sw + 0.5) * total / swifties);
                for (int d = 0; d < total; d++)
                {
                    int s = (ideal + d) % total;
                    if (slots[s] == "basic_walker") { slots[s] = "swift_walker"; break; }
                }
            }
        }

        if (splitters > 0)
        {
            // Spread splitters evenly, skipping already-assigned slots
            for (int sp = 0; sp < splitters; sp++)
            {
                int ideal = (int)Math.Round((sp + 0.5) * total / splitters);
                for (int d = 0; d < total; d++)
                {
                    int s = (ideal + d) % total;
                    if (slots[s] == "basic_walker") { slots[s] = "splitter_walker"; break; }
                }
            }
        }

        if (reversers > 0)
        {
            // Spread reverse walkers evenly, skipping already-assigned slots.
            for (int rv = 0; rv < reversers; rv++)
            {
                int ideal = (int)Math.Round((rv + 0.5) * total / reversers);
                for (int d = 0; d < total; d++)
                {
                    int s = (ideal + d) % total;
                    if (slots[s] == "basic_walker") { slots[s] = "reverse_walker"; break; }
                }
            }
        }

        if (drones > 0)
        {
            // Spread shield drones evenly, skipping already-assigned slots.
            // Placed toward the middle of the pack so they arrive with a group to protect.
            for (int dr = 0; dr < drones; dr++)
            {
                int ideal = (int)Math.Round((dr + 0.5) * total / drones);
                for (int d = 0; d < total; d++)
                {
                    int s = (ideal + d) % total;
                    if (slots[s] == "basic_walker") { slots[s] = "shield_drone"; break; }
                }
            }
        }

        foreach (string t in slots) _spawnQueue.Enqueue(t);
    }

    public MobileWaveRuntimeSnapshot CaptureWaveRuntimeSnapshot(RunState state)
    {
        var snapshot = new MobileWaveRuntimeSnapshot
        {
            SpawnTimer = Mathf.Max(0f, _spawnTimer),
            WalkerNextIsFirst = _walkerNextIsFirst,
            EnemiesSpawnedThisWave = Mathf.Max(0, state.EnemiesSpawnedThisWave),
            WaveTime = Mathf.Max(0f, state.WaveTime),
            RemainingSpawnQueue = _spawnQueue.ToList(),
            ActiveEnemies = new List<MobileWaveEnemySnapshot>(),
        };

        foreach (var enemy in state.EnemiesAlive)
        {
            if (!GodotObject.IsInstanceValid(enemy))
                continue;
            if (enemy.Hp <= 0f)
                continue;

            snapshot.ActiveEnemies.Add(new MobileWaveEnemySnapshot
            {
                TypeId = enemy.EnemyTypeId,
                Hp = enemy.Hp,
                Progress = Mathf.Max(0f, enemy.Progress),
                Speed = Mathf.Max(1f, enemy.Speed),
                MarkedRemaining = Mathf.Max(0f, enemy.MarkedRemaining),
                SlowRemaining = Mathf.Max(0f, enemy.SlowRemaining),
                SlowSpeedFactor = enemy.SlowSpeedFactor,
                DamageAmpRemaining = Mathf.Max(0f, enemy.DamageAmpRemaining),
                DamageAmpMultiplier = Mathf.Max(0f, enemy.DamageAmpMultiplier),
                BurnRemaining = Mathf.Max(0f, enemy.BurnRemaining),
                BurnDamagePerSecond = Mathf.Max(0f, enemy.BurnDamagePerSecond),
                BurnOwnerSlotIndex = enemy.BurnOwnerSlotIndex,
                BurnTrailDropTimer = Mathf.Max(0f, enemy.BurnTrailDropTimer),
            });
        }

        return snapshot;
    }

    public bool RestoreWaveRuntimeSnapshot(RunState state, MobileWaveRuntimeSnapshot snapshot)
    {
        if (EnemyScene == null || LanePath == null)
            return false;

        foreach (var enemy in state.EnemiesAlive)
        {
            if (GodotObject.IsInstanceValid(enemy))
                enemy.QueueFree();
        }
        state.EnemiesAlive.Clear();
        ClearLatchParasites();

        _spawnQueue.Clear();
        foreach (string typeId in snapshot.RemainingSpawnQueue ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(typeId))
                _spawnQueue.Enqueue(typeId);
        }

        _spawnTimer = Mathf.Max(0f, snapshot.SpawnTimer);
        _walkerNextIsFirst = snapshot.WalkerNextIsFirst;
        state.EnemiesSpawnedThisWave = Mathf.Max(0, snapshot.EnemiesSpawnedThisWave);
        state.WaveTime = Mathf.Max(0f, snapshot.WaveTime);

        float mandateMult = state.ActiveMandate?.Type == MandateType.EnemyHpBonus
            ? state.ActiveMandate.EnemyHpMultiplier : 1.0f;
        var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;

        foreach (var enemySnapshot in snapshot.ActiveEnemies ?? new List<MobileWaveEnemySnapshot>())
        {
            if (string.IsNullOrWhiteSpace(enemySnapshot.TypeId))
                continue;

            float maxHp = WaveSystem.GetScaledHp(enemySnapshot.TypeId, state.WaveIndex,
                difficulty, state.EndlessWaveDepth, mandateMult);
            float speed = enemySnapshot.Speed > 0f
                ? enemySnapshot.Speed
                : ResolveEnemySpeed(enemySnapshot.TypeId);

            var enemy = EnemyScene.Instantiate<EnemyInstance>();
            enemy.Initialize(enemySnapshot.TypeId, maxHp, speed);
            LanePath.AddChild(enemy);
            if (BotMode)
                enemy.SetProcess(false);

            enemy.Progress = Mathf.Max(0f, enemySnapshot.Progress);
            enemy.Hp = Mathf.Clamp(enemySnapshot.Hp, 0f, maxHp);
            enemy.MarkedRemaining = Mathf.Max(0f, enemySnapshot.MarkedRemaining);
            enemy.SlowRemaining = Mathf.Max(0f, enemySnapshot.SlowRemaining);
            enemy.SlowSpeedFactor = enemySnapshot.SlowSpeedFactor > 0f
                ? enemySnapshot.SlowSpeedFactor
                : Balance.SlowSpeedFactor;
            enemy.DamageAmpRemaining = Mathf.Max(0f, enemySnapshot.DamageAmpRemaining);
            enemy.DamageAmpMultiplier = Mathf.Max(0f, enemySnapshot.DamageAmpMultiplier);
            enemy.BurnRemaining = Mathf.Max(0f, enemySnapshot.BurnRemaining);
            enemy.BurnDamagePerSecond = Mathf.Max(0f, enemySnapshot.BurnDamagePerSecond);
            enemy.BurnOwnerSlotIndex = enemySnapshot.BurnOwnerSlotIndex;
            enemy.BurnTrailDropTimer = Mathf.Max(0f, enemySnapshot.BurnTrailDropTimer);
            enemy.IsShieldProtected = false;
            state.EnemiesAlive.Add(enemy);
        }

        state.EnemiesSpawnedThisWave = Math.Max(state.EnemiesSpawnedThisWave, state.EnemiesAlive.Count);
        return true;
    }

    /// <summary>
    /// Seeds (or replaces) the active Afterimage imprint for a tower.
    /// Rule: one active imprint per tower; new hits overwrite old imprint state.
    /// </summary>
    public void QueueAfterimageImprint(ITowerView tower, Vector2 worldPos, float sourceDamage)
    {
        if (tower == null || sourceDamage <= 0f)
            return;

        float delay = Balance.AfterimageDelaySeconds;
        int copies = CountModifierCopies(tower, "afterimage");
        if (copies <= 0)
            return;

        float seedDamage = Mathf.Max(Balance.AfterimageMinDamage, sourceDamage);

        ActiveAfterimage? existing = _activeAfterimages.FirstOrDefault(a => ReferenceEquals(a.SourceTower, tower));
        if (existing != null)
        {
            existing.Position = worldPos;
            existing.SeedDamage = seedDamage;
            existing.DelayRemaining = delay;
            existing.DelayTotal = delay;
            if (!BotMode && existing.Visual != null && GodotObject.IsInstanceValid(existing.Visual))
                existing.Visual.Reset(worldPos, delay, ResolveAfterimageRadius(tower, copies), ResolveAfterimageTint(tower), tower.TowerId, copies);
            Sounds?.Play("afterimage_seed", pitchScale: ResolveAfterimageSeedPitch(tower));
            return;
        }

        var imprint = new ActiveAfterimage
        {
            SourceTower = tower,
            SourceNode = tower as TowerInstance,
            Position = worldPos,
            SeedDamage = seedDamage,
            DelayTotal = delay,
            DelayRemaining = delay,
        };

        if (!BotMode && LanePath != null)
        {
            var visual = new AfterimageImprintVfx();
            LanePath.GetParent().AddChild(visual);
            visual.GlobalPosition = worldPos;
            visual.Initialize(
                delay,
                ResolveAfterimageRadius(tower, copies),
                ResolveAfterimageTint(tower),
                tower.TowerId,
                copies);
            imprint.Visual = visual;
        }

        _activeAfterimages.Add(imprint);
        Sounds?.Play("afterimage_seed", pitchScale: ResolveAfterimageSeedPitch(tower));
    }

    public WaveResult Step(float delta, RunState state, WaveSystem waveSystem)
    {
        state.WaveTime += delta;
        state.TotalPlayTime += delta;  // Game-time seconds (scaled delta = 1x-equivalent, fair for leaderboard comparison)
        _killComboTimer = Mathf.Max(0f, _killComboTimer - delta);

        // 1. Spawn
        _spawnTimer -= delta;
        int quota = waveSystem.GetTotalCount();
        if (_spawnTimer <= 0f && state.EnemiesSpawnedThisWave < quota && _spawnQueue.Count > 0)
        {
            string typeId = _spawnQueue.Dequeue();
            SpawnEnemy(state, typeId);
            _spawnTimer = NextSpawnInterval(typeId, waveSystem.GetSpawnInterval());
        }

        // 1.5. Undertow active effects tick every frame so progress rewinds are smooth and
        // happen through path progress (safe on curved/snake/zigzag maps).
        UpdateActiveUndertows(delta, state.EnemiesAlive);

        // 2. Leaked enemies - each one costs a life; return Loss when lives run out
        var leaked = state.EnemiesAlive.FindAll(e => e.ProgressRatio >= 1.0f);
        foreach (var e in leaked)
        {
            int livesLost = e.EnemyTypeId switch
            {
                "armored_walker"  => 2,
                "splitter_walker" => 3,
                _                 => 1,
            };
            state.Lives = Math.Max(0, state.Lives - livesLost);

            // Track leaks for post-wave micro reports and loss analysis
            state.TrackLeak(e.EnemyTypeId);
            if (BotMode) state.TrackLeakHp(e.EnemyTypeId, e.Hp);
            
            Sounds?.Play("leak");
            e.QueueFree();
        }
        state.EnemiesAlive.RemoveAll(e => !GodotObject.IsInstanceValid(e) || e.ProgressRatio >= 1.0f);
        if (state.Lives <= 0)
        {
            ClearMines();
            ClearFireTrails();
            ClearUndertowEffects();
            ClearAfterimages();
            ClearLatchParasites();
            return WaveResult.Loss;
        }

        ResolveMineTriggers(delta, state.WaveIndex, state.EnemiesAlive);

        // 3a. Update Shield Drone protection auras (must run before tower attacks so damage reduction is current).
        // Known ordering constraint: if a Shield Drone is killed mid-frame by an earlier tower slot,
        // enemies it was shielding retain IsShieldProtected=true for the rest of that frame.
        // The flag is cleared correctly at the start of the next Step(). One frame of ghost shielding
        // (~16ms at 60fps) is imperceptible and not worth re-running the O(n²) aura pass after each kill.
        UpdateShieldDroneAuras(state.EnemiesAlive);
        PruneLatchParasitesForMissingTowers(state);

        // 3. Tower attacks (hitscan - no projectiles)
        for (int si = 0; si < state.Slots.Length; si++)
        {
            var slot = state.Slots[si];
            if (slot.Tower == null) continue;
            var tower = slot.Tower;
            // towerNode is null only in tests (FakeTower); in production it is always TowerInstance
            var towerNode = slot.TowerNode;

            tower.Cooldown -= delta;
            foreach (var mod in tower.Modifiers)
                mod.Update(delta, tower);
            if (tower.Cooldown > 0f) continue;

            if (BotMode) state.SlotEligibleSteps[si]++;

            // Rift Sapper plants mines directly on the lane, so it does not require an enemy target.
            if (tower.TowerId == "rift_prism")
            {
                float riftInterval = tower.AttackInterval;
                foreach (var mod in tower.Modifiers)
                    mod.ModifyAttackInterval(ref riftInterval, tower);

                bool burstActive = state.WaveTime <= Balance.RiftMineBurstWindow
                    && GetRiftBurstFastPlantsUsed(tower) < Balance.RiftMineBurstFastPlantsPerTower;
                if (burstActive)
                    riftInterval *= Balance.RiftMineBurstIntervalMultiplier;

                if (TryPlantMine(tower, state.EnemiesAlive))
                {
                    if (BotMode) state.SlotFiredSteps[si]++;
                    tower.Cooldown = riftInterval;
                    GameController.Instance?.RegisterSpectacleShotFired(tower);
                    if (burstActive)
                        IncrementRiftBurstFastPlantsUsed(tower);
                    if (!BotMode)
                    {
                        towerNode?.FlashAttack();
                        Sounds?.Play("shoot_marker", pitchScale: 0.92f);
                    }
                }
                else
                {
                    tower.Cooldown = 0.12f;
                }
                continue;
            }

            // Accordion Engine: compression pulse hits all in-range enemies simultaneously.
            if (tower.TowerId == "accordion_engine")
            {
                float accordionInterval = tower.AttackInterval;
                foreach (var mod in tower.Modifiers)
                    mod.ModifyAttackInterval(ref accordionInterval, tower);

                // Collect in-range enemies sorted by Progress ascending (trailing → leading).
                var inRange = new List<EnemyInstance>();
                foreach (var e in state.EnemiesAlive)
                {
                    if (!GodotObject.IsInstanceValid(e) || e.Hp <= 0f) continue;
                    if (tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range)
                        inRange.Add(e);
                }
                inRange.Sort((a, b) => a.Progress.CompareTo(b.Progress));

                if (inRange.Count == 0)
                {
                    tower.Cooldown = 0.12f;
                    continue;
                }

                if (BotMode) state.SlotFiredSteps[si]++;
                tower.Cooldown = accordionInterval;
                GameController.Instance?.RegisterSpectacleShotFired(tower);

                // Apply formation compression: squeeze enemy Progress values toward their median.
                if (inRange.Count >= Balance.AccordionMinEnemiesForCompression)
                    CompressEnemyFormation(inRange);

                // Primary = leading enemy (highest Progress, last in ascending sort).
                var primaryTarget = inRange[inRange.Count - 1];

                // Primary hit: isChain=false so Blast Core and Overkill OnHit fire.
                float primaryHpBefore = primaryTarget.Hp;
                var primaryCtx = new DamageContext(tower, primaryTarget, state.WaveIndex, state.EnemiesAlive, _state, isChain: false);
                DamageModel.Apply(primaryCtx);
                if (!BotMode)
                {
                    float dealt = primaryHpBefore - primaryTarget.Hp;
                    if (dealt > 0.01f)
                    {
                        bool isKill = primaryTarget.Hp <= 0f;
                        SpawnDamageNumber(primaryTarget.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, towerNode?.ProjectileColor ?? Godot.Colors.Yellow);
                        if (!isKill && GodotObject.IsInstanceValid(primaryTarget))
                            primaryTarget.FlashHit();
                    }
                }

                // Secondary hits: isChain=true so Blast Core and Overkill OnHit are skipped.
                for (int pi = 0; pi < inRange.Count - 1; pi++)
                {
                    var sec = inRange[pi];
                    if (!GodotObject.IsInstanceValid(sec) || sec.Hp <= 0f) continue;
                    float secHpBefore = sec.Hp;
                    DamageModel.Apply(new DamageContext(tower, sec, state.WaveIndex, state.EnemiesAlive, _state, isChain: true));
                    if (!BotMode)
                    {
                        float dealt = secHpBefore - sec.Hp;
                        if (dealt > 0.01f)
                        {
                            bool isKill = sec.Hp <= 0f;
                            SpawnDamageNumber(sec.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, towerNode?.ProjectileColor ?? Godot.Colors.Yellow);
                            if (!isKill && GodotObject.IsInstanceValid(sec))
                                sec.FlashHit();
                        }
                    }
                }

                // Chain bounces from primary (ChainReaction modifier).
                if (tower.IsChainTower && GodotObject.IsInstanceValid(primaryTarget) && primaryTarget.Hp > 0f)
                {
                    if (BotMode)
                        ApplyChainBotMode(tower, primaryTarget, state.WaveIndex, state.EnemiesAlive);
                    else if (towerNode != null)
                        ApplyMineEnemyChain(towerNode, primaryTarget, state.WaveIndex, state.EnemiesAlive, primaryCtx.FinalDamage * tower.ChainDamageDecay);
                }

                if (!BotMode && towerNode != null)
                {
                    towerNode.FlashAttack();
                    towerNode.KickRecoil(2.8f);
                    SpawnAccordionPulseVfx(tower.GlobalPosition, towerNode.ProjectileColor, tower.Range, inRange.Count);
                }
                Sounds?.Play("shoot_accordion", pitchScale: ComputeShootPitch(tower));
                continue;
            }

            // Phase Splitter: single fire event that applies two independent primary hits
            // to the first and last in-range enemies.
            if (tower.TowerId == "phase_splitter")
            {
                float interval = tower.AttackInterval;
                foreach (var mod in tower.Modifiers)
                    mod.ModifyAttackInterval(ref interval, tower);

                var (frontTarget, backTarget) = Targeting.SelectFirstAndLastTargets(tower, state.EnemiesAlive, ignoreRange: false);
                if (frontTarget == null && backTarget == null)
                {
                    tower.Cooldown = 0.12f;
                    continue;
                }

                if (BotMode) state.SlotFiredSteps[si]++;
                tower.Cooldown = interval;
                GameController.Instance?.RegisterSpectacleShotFired(tower);

                var primaryTargets = new List<EnemyInstance>(2);
                if (frontTarget != null) primaryTargets.Add(frontTarget);
                if (backTarget != null && !ReferenceEquals(frontTarget, backTarget))
                    primaryTargets.Add(backTarget);

                if (!BotMode && towerNode != null)
                {
                    towerNode.LastTargetPosition = frontTarget?.GlobalPosition ?? backTarget!.GlobalPosition;
                    towerNode.OnShotFired(primaryTargets[0]);
                }

                bool anyImpact = false;
                float primaryDamage = tower.BaseDamage * Balance.PhaseSplitterDamageRatio;

                foreach (EnemyInstance primary in primaryTargets)
                {
                    if (!GodotObject.IsInstanceValid(primary) || primary.Hp <= 0f)
                        continue;

                    float hpBefore = primary.Hp;
                    var primaryCtx = new DamageContext(
                        tower,
                        primary,
                        state.WaveIndex,
                        state.EnemiesAlive,
                        _state,
                        isChain: false,
                        damageOverride: primaryDamage);
                    DamageModel.Apply(primaryCtx);
                    if (tower.IsChainTower)
                    {
                        if (BotMode)
                        {
                            int bounces = CombatResolution.ApplyChainHits(tower, primary, state.WaveIndex, state.EnemiesAlive, _state);
                            _state.TrackSpectacleChainDepth(bounces);
                            if (bounces > 0)
                            {
                                float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
                                GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
                            }
                        }
                        else
                        {
                            ApplyChainImmediate(tower, primary, state.WaveIndex, state.EnemiesAlive, towerNode?.ProjectileColor ?? Colors.Yellow, tower.BaseDamage * tower.ChainDamageDecay);
                        }
                    }

                    if (tower.SplitCount > 0)
                    {
                        ApplyPhaseSplitterSplitHits(
                            tower,
                            primary,
                            state.WaveIndex,
                            state.EnemiesAlive,
                            inBotMode: BotMode,
                            sourceColor: towerNode?.ProjectileColor ?? Colors.Yellow);
                    }

                    if (!BotMode)
                    {
                        float dealt = hpBefore - primary.Hp;
                        if (dealt > 0.01f)
                        {
                            anyImpact = true;
                            bool isKill = primary.Hp <= 0f;
                            SpawnDamageNumber(primary.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, towerNode?.ProjectileColor ?? Colors.Yellow);
                            if (!isKill && GodotObject.IsInstanceValid(primary))
                                primary.FlashHit();
                        }
                    }
                }

                if (!BotMode && towerNode != null)
                {
                    towerNode.FlashAttack();
                    towerNode.KickRecoil(3.9f);
                    SpawnPhaseSplitVfx(
                        tower.GlobalPosition,
                        frontTarget?.GlobalPosition,
                        backTarget?.GlobalPosition,
                        towerNode.ProjectileColor);
                }

                string phaseShootId = "shoot_phase_splitter";
                Sounds?.Play(phaseShootId, pitchScale: ComputeShootPitch(tower));
                if (anyImpact)
                    Sounds?.Play("hit_phase_splitter", pitchScale: 1.0f + (float)(_mineRng.NextDouble() - 0.5) * 0.08f);
                continue;
            }

            if (tower.TowerId == "undertow_engine")
            {
                float interval = tower.AttackInterval;
                foreach (var mod in tower.Modifiers)
                    mod.ModifyAttackInterval(ref interval, tower);

                EnemyInstance? primaryTarget = SelectUndertowPrimaryTarget(tower, state.EnemiesAlive);
                if (primaryTarget == null)
                {
                    tower.Cooldown = 0.12f;
                    continue;
                }

                if (BotMode) state.SlotFiredSteps[si]++;
                tower.Cooldown = MathF.Max(0.01f, interval);
                GameController.Instance?.RegisterSpectacleShotFired(tower);

                if (!BotMode && towerNode != null)
                {
                    towerNode.LastTargetPosition = primaryTarget.GlobalPosition;
                    towerNode.OnShotFired(primaryTarget);
                    towerNode.FlashAttack();
                    towerNode.KickRecoil(4.2f);
                }

                float hpBefore = primaryTarget.Hp;
                var primaryCtx = new DamageContext(tower, primaryTarget, state.WaveIndex, state.EnemiesAlive, _state, isChain: false);
                DamageModel.Apply(primaryCtx);
                if (!BotMode)
                {
                    float dealt = hpBefore - primaryTarget.Hp;
                    if (dealt > 0.01f)
                    {
                        bool isKill = primaryTarget.Hp <= 0f;
                        SpawnDamageNumber(primaryTarget.GlobalPosition, MathF.Max(1f, dealt), isKill, tower.TowerId, towerNode?.ProjectileColor ?? Colors.Yellow);
                        if (!isKill && GodotObject.IsInstanceValid(primaryTarget))
                            primaryTarget.FlashHit();
                    }
                }

                bool startedPrimary = TryStartUndertowEffect(
                    tower,
                    towerNode,
                    primaryTarget,
                    strengthMultiplier: 1f,
                    isSecondary: false,
                    isFollowup: false,
                    enableEndpointPulse: true);

                if (startedPrimary)
                {
                    ApplyUndertowSecondaryTugs(
                        tower,
                        towerNode,
                        primaryTarget,
                        state.EnemiesAlive);
                    ScheduleUndertowFeedbackFollowup(
                        tower,
                        towerNode,
                        primaryTarget);
                }

                Sounds?.Play("shoot_undertow", pitchScale: ComputeShootPitch(tower) * 0.94f);
                continue;
            }

            EnemyInstance? target = tower.TowerId switch
            {
                "rocket_launcher" => SelectRocketSplashTarget(tower, state.EnemiesAlive),
                "latch_nest" => SelectLatchNestTarget(tower, state.EnemiesAlive),
                _ => Targeting.SelectTarget(tower, state.EnemiesAlive, ignoreRange: false),
            };
            if (target == null) continue;

            if (BotMode) state.SlotFiredSteps[si]++;

            if (!BotMode && towerNode != null) towerNode.LastTargetPosition = target.GlobalPosition;
            string nextTargetId = target.GetInstanceId().ToString();
            if (!BotMode && towerNode != null && towerNode.LastTargetId != null && towerNode.LastTargetId != nextTargetId)
                GameController.Instance?.SpawnTargetAcquirePing(target.GlobalPosition, towerNode.ProjectileColor);

            // Effective interval: base × modifier multipliers (e.g. FocusLens ×2)
            float effectiveInterval = tower.AttackInterval;
            foreach (var mod in tower.Modifiers)
                mod.ModifyAttackInterval(ref effectiveInterval, tower);

            tower.Cooldown = effectiveInterval;
            GameController.Instance?.RegisterSpectacleShotFired(tower);

            // Damage applied on projectile arrival, not here
            Action<DamageContext, float, bool>? onPrimaryImpact = null;
            if (tower.TowerId == "latch_nest")
            {
                onPrimaryImpact = (ctx, dealt, isKill) =>
                {
                    if (isKill || ctx.Target is not EnemyInstance host || host.Hp <= 0f)
                        return;
                    AttachLatchParasiteIfPossible(tower, host);
                };
            }

            SpawnProjectile(
                tower.GlobalPosition,
                target,
                towerNode?.ProjectileColor ?? Godot.Colors.Yellow,
                tower,
                state.WaveIndex,
                state.EnemiesAlive,
                onPrimaryImpact);
            if (!BotMode && towerNode != null)
            {
                towerNode.OnShotFired(target);
                towerNode.FlashAttack();
                float recoilPx = tower.TowerId switch
                {
                    "heavy_cannon" => 6.4f,
                    "rocket_launcher" => 5.2f,
                    _ => 3.5f,
                };
                towerNode.KickRecoil(recoilPx);
            }

            string shootId = tower.TowerId switch
            {
                "heavy_cannon"  => "shoot_heavy",
                "rocket_launcher" => "shoot_rocket",
                "marker_tower"  => "shoot_marker",
                "chain_tower"   => "shoot_rapid",
                "latch_nest"    => "shoot_latch",
                "phase_splitter"=> "shoot_phase_splitter",
                "undertow_engine" => "shoot_undertow",
                _               => "shoot_rapid",
            };
            // Chill Shot gives rapid-fire towers a softer, icier sound
            if (shootId == "shoot_rapid" && tower.Modifiers.Any(m => m.ModifierId == "slow"))
                shootId = "shoot_rapid_cold";
            Sounds?.Play(shootId, pitchScale: ComputeShootPitch(tower));
            if (tower.TowerId is "heavy_cannon" or "rocket_launcher")
                Sounds?.DuckMusic(1.9f, 0.11f);
        }

        // 3.5. Wildfire burn DOT and fire trail hazards
        UpdateBurnAndTrails(delta, state.WaveIndex, state.EnemiesAlive);
        UpdateAfterimages(delta, state.EnemiesAlive);
        _latchParasites.Tick(
            delta,
            state.WaveIndex,
            state.EnemiesAlive,
            _state,
            Balance.LatchNestParasiteTickDamageMultiplier,
            OnLatchParasiteTick,
            OnLatchParasiteDetached);

        // 4. Remove dead enemies
        foreach (var dead in state.EnemiesAlive.FindAll(e => e.Hp <= 0))
        {
            string dieSound = dead.EnemyTypeId switch
            {
                "armored_walker"  => "die_armored",
                "swift_walker"    => "die_swift",
                "reverse_walker"  => "die_reverse",
                "splitter_walker" => "die_basic",
                "shield_drone"    => "die_swift",  // light drone collapse sound
                _                 => "die_basic",
            };
            if (_killComboTimer <= 0f) _killComboCount = 0;
            float pitch = Mathf.Clamp(1.0f + _killComboCount * 0.05f, 1.0f, 1.15f);
            Sounds?.Play(dieSound, pitchScale: pitch);
            _killComboCount = Mathf.Min(_killComboCount + 1, 3);
            _killComboTimer = 0.24f;
            SpawnDeathBurst(dead.GlobalPosition, dead.EnemyTypeId);
            if (dead.EnemyTypeId == "splitter_walker")
                SpawnShards(state, dead);
            dead.QueueFree();
        }
        state.EnemiesAlive.RemoveAll(e => e.Hp <= 0 || !GodotObject.IsInstanceValid(e));

        // 5. Wave complete
        bool quotaDone = state.EnemiesSpawnedThisWave >= quota;
        if (quotaDone && state.EnemiesAlive.Count == 0)
        {
            ClearMines();
            ClearFireTrails();
            ClearUndertowEffects();
            ClearAfterimages();
            ClearLatchParasites();
            return WaveResult.WaveComplete;
        }

        return WaveResult.Ongoing;
    }

    private void ClearMines()
    {
        foreach (var mine in _activeMines)
        {
            if (mine.Visual != null && GodotObject.IsInstanceValid(mine.Visual))
                mine.Visual.QueueFree();
        }
        _activeMines.Clear();
    }

    private void ClearFireTrails()
    {
        foreach (var trail in _activeFireTrails)
        {
            if (trail.Visual != null && GodotObject.IsInstanceValid(trail.Visual))
                trail.Visual.QueueFree();
        }
        _activeFireTrails.Clear();
        _wildfireTrailDmgAccumulator.Clear();
    }

    private void ClearUndertowEffects()
    {
        foreach (var effect in _activeUndertows)
        {
            if (effect.TetherVfx != null && GodotObject.IsInstanceValid(effect.TetherVfx))
                effect.TetherVfx.QueueFree();
        }
        _activeUndertows.Clear();
        _undertowRecentRemaining.Clear();
        _undertowRetargetLockoutRemaining.Clear();
    }

    private void ClearAfterimages()
    {
        foreach (var imprint in _activeAfterimages)
        {
            if (imprint.Visual != null && GodotObject.IsInstanceValid(imprint.Visual))
                imprint.Visual.QueueFree();
        }
        _activeAfterimages.Clear();
    }

    private void ClearLatchParasites()
    {
        _latchParasites.Clear(OnLatchParasiteDetached);
        foreach ((_, LatchParasiteVfx visual) in _latchParasiteVisuals)
        {
            if (GodotObject.IsInstanceValid(visual))
                visual.QueueFree();
        }
        _latchParasiteVisuals.Clear();
    }

    private static void TickTimerDict(Dictionary<ulong, float> timers, float delta)
    {
        if (timers.Count == 0 || delta <= 0f)
            return;

        var keys = timers.Keys.ToArray();
        foreach (ulong key in keys)
        {
            float remaining = timers[key] - delta;
            if (remaining <= 0f)
                timers.Remove(key);
            else
                timers[key] = remaining;
        }
    }

    private void UpdateActiveUndertows(float delta, List<EnemyInstance> enemies)
    {
        TickTimerDict(_undertowRecentRemaining, delta);
        TickTimerDict(_undertowRetargetLockoutRemaining, delta);

        if (_activeUndertows.Count == 0 || delta <= 0f)
            return;

        var activeCountByEnemy = new Dictionary<ulong, int>();
        foreach (var effect in _activeUndertows)
        {
            if (effect.DelayRemaining > 0f)
                continue;
            if (!GodotObject.IsInstanceValid(effect.Target) || effect.Target.Hp <= 0f)
                continue;

            ulong id = effect.Target.GetInstanceId();
            activeCountByEnemy.TryGetValue(id, out int count);
            activeCountByEnemy[id] = count + 1;
        }

        for (int i = _activeUndertows.Count - 1; i >= 0; i--)
        {
            ActiveUndertow effect = _activeUndertows[i];
            if (!GodotObject.IsInstanceValid(effect.Target) || effect.Target.Hp <= 0f)
            {
                if (effect.TetherVfx != null && GodotObject.IsInstanceValid(effect.TetherVfx))
                    effect.TetherVfx.QueueFree();
                _activeUndertows.RemoveAt(i);
                continue;
            }

            if (effect.DelayRemaining > 0f)
            {
                effect.DelayRemaining = MathF.Max(0f, effect.DelayRemaining - delta);
                if (effect.DelayRemaining > 0f)
                    continue;

                RegisterUndertowAffect(effect.Target);
                Statuses.ApplySlow(effect.Target, MathF.Max(0.12f, effect.TotalDuration), effect.SlowFactor);
                if (!BotMode && effect.SourceNode != null && GodotObject.IsInstanceValid(effect.SourceNode) && LanePath != null)
                {
                    var tether = new UndertowTetherVfx();
                    LanePath.GetParent().AddChild(tether);
                    tether.GlobalPosition = Vector2.Zero;
                    tether.Initialize(effect.SourceNode, effect.Target, effect.SourceNode.ProjectileColor, effect.TotalDuration, effect.IsSecondary || effect.IsFollowup);
                    effect.TetherVfx = tether;
                }
            }

            float remainingDistance = MathF.Max(0f, effect.TotalPullDistance - effect.PulledDistance);
            if (remainingDistance <= 0.01f || effect.Remaining <= 0f)
            {
                CompleteUndertowEffect(effect, enemies);
                if (effect.TetherVfx != null && GodotObject.IsInstanceValid(effect.TetherVfx))
                    effect.TetherVfx.QueueFree();
                _activeUndertows.RemoveAt(i);
                continue;
            }

            Statuses.ApplySlow(effect.Target, MathF.Max(0.10f, delta + 0.05f), effect.SlowFactor);

            float stepPull = effect.TotalDuration <= 0.001f
                ? remainingDistance
                : effect.TotalPullDistance * (delta / effect.TotalDuration);
            stepPull = MathF.Min(stepPull, remainingDistance);

            ulong targetId = effect.Target.GetInstanceId();
            if (activeCountByEnemy.TryGetValue(targetId, out int concurrent) && concurrent > 1)
            {
                float concurrentScale = 1f;
                for (int c = 1; c < concurrent; c++)
                    concurrentScale *= Balance.UndertowConcurrentExtraDecay;
                stepPull *= concurrentScale;
            }

            float startProgress = effect.Target.Progress;
            float endProgress = MathF.Max(0f, startProgress - stepPull);
            float applied = startProgress - endProgress;
            if (applied > 0f)
            {
                effect.Target.Progress = endProgress;
                effect.PulledDistance += applied;
            }

            effect.Remaining -= delta;
            if (effect.TetherVfx != null && GodotObject.IsInstanceValid(effect.TetherVfx))
            {
                float t = 1f - (MathF.Max(0f, effect.Remaining) / MathF.Max(0.0001f, effect.TotalDuration));
                effect.TetherVfx.SetProgress(Mathf.Clamp(t, 0f, 1f));
            }

            if (effect.Remaining <= 0f || effect.PulledDistance >= effect.TotalPullDistance - 0.01f)
            {
                CompleteUndertowEffect(effect, enemies);
                if (effect.TetherVfx != null && GodotObject.IsInstanceValid(effect.TetherVfx))
                    effect.TetherVfx.QueueFree();
                _activeUndertows.RemoveAt(i);
            }
        }
    }

    private static bool IsUsableUndertowTarget(EnemyInstance enemy)
        => GodotObject.IsInstanceValid(enemy) && enemy.Hp > 0f;

    private static bool IsInRange(ITowerView tower, EnemyInstance enemy)
        => tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range;

    private EnemyInstance? SelectUndertowPrimaryTarget(ITowerView tower, List<EnemyInstance> enemies)
    {
        var candidates = enemies
            .Where(IsUsableUndertowTarget)
            .Where(e => IsInRange(tower, e))
            .ToList();
        if (candidates.Count == 0)
            return null;

        bool PreferCandidate(EnemyInstance a, EnemyInstance b)
        {
            // Prefer targets not currently under active undertow to avoid degenerate lock loops.
            bool aActive = _activeUndertows.Any(u => u.DelayRemaining <= 0f && ReferenceEquals(u.Target, a));
            bool bActive = _activeUndertows.Any(u => u.DelayRemaining <= 0f && ReferenceEquals(u.Target, b));
            if (aActive != bActive)
                return !aActive;

            bool aLocked = _undertowRetargetLockoutRemaining.TryGetValue(a.GetInstanceId(), out float aLock) && aLock > 0f;
            bool bLocked = _undertowRetargetLockoutRemaining.TryGetValue(b.GetInstanceId(), out float bLock) && bLock > 0f;
            if (aLocked != bLocked)
                return !aLocked;

            switch (tower.TargetingMode)
            {
                case TargetingMode.Strongest:
                    if (MathF.Abs(a.Hp - b.Hp) > 0.001f) return a.Hp > b.Hp;
                    if (MathF.Abs(a.Progress - b.Progress) > 0.001f) return a.Progress > b.Progress;
                    break;
                case TargetingMode.LowestHp:
                    if (MathF.Abs(a.Hp - b.Hp) > 0.001f) return a.Hp < b.Hp;
                    if (MathF.Abs(a.Progress - b.Progress) > 0.001f) return a.Progress > b.Progress;
                    break;
                case TargetingMode.Last:
                    if (MathF.Abs(a.Progress - b.Progress) > 0.001f) return a.Progress < b.Progress;
                    if (MathF.Abs(a.Hp - b.Hp) > 0.001f) return a.Hp > b.Hp;
                    break;
                default:
                    // Default identity: aggressively prefer the furthest-progressed target.
                    if (MathF.Abs(a.Progress - b.Progress) > 0.001f) return a.Progress > b.Progress;
                    if (MathF.Abs(a.Hp - b.Hp) > 0.001f) return a.Hp > b.Hp;
                    break;
            }

            return a.GetInstanceId() < b.GetInstanceId();
        }

        EnemyInstance best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            EnemyInstance c = candidates[i];
            if (PreferCandidate(c, best))
                best = c;
        }
        return best;
    }

    private static int CountModifierCopies(ITowerView tower, string modifierId)
    {
        int count = 0;
        foreach (var mod in tower.Modifiers)
        {
            if (mod.ModifierId == modifierId)
                count++;
        }
        return count;
    }

    private float ResolveUndertowRecentMultiplier(EnemyInstance target)
    {
        if (!_undertowRecentRemaining.TryGetValue(target.GetInstanceId(), out float remaining))
            return 1f;

        float t = Mathf.Clamp(remaining / MathF.Max(0.0001f, Balance.UndertowRecentWindow), 0f, 1f);
        return Mathf.Lerp(1f, Balance.UndertowRecentMinMultiplier, t);
    }

    private static float ResolveUndertowResistance(EnemyInstance target)
    {
        return target.EnemyTypeId switch
        {
            "armored_walker" => Balance.UndertowArmoredResistanceMultiplier,
            "reverse_walker" or "shield_drone" or "splitter_walker" => Balance.UndertowHeavyResistanceMultiplier,
            _ => 1f
        };
    }

    private float ResolveUndertowSlowFactor(ITowerView tower, bool isSecondary, bool isFollowup)
    {
        float factor = Balance.UndertowSlowFactor;
        int chillCopies = CountModifierCopies(tower, "slow");
        int focusCopies = CountModifierCopies(tower, "focus_lens");
        factor -= chillCopies * Balance.UndertowSlowPerChillCopy;
        factor -= focusCopies * Balance.UndertowFocusLensSlowPerCopy;
        if (isSecondary)
            factor = Mathf.Lerp(factor, 1f, 0.35f);
        if (isFollowup)
            factor = Mathf.Lerp(factor, 1f, 0.24f);
        return Mathf.Clamp(factor, 0.12f, 0.95f);
    }

    private void RegisterUndertowAffect(EnemyInstance target)
    {
        ulong id = target.GetInstanceId();
        _undertowRecentRemaining.TryGetValue(id, out float currentRecent);
        _undertowRecentRemaining[id] = MathF.Max(currentRecent, Balance.UndertowRecentWindow);

        _undertowRetargetLockoutRemaining.TryGetValue(id, out float currentLockout);
        _undertowRetargetLockoutRemaining[id] = MathF.Max(currentLockout, Balance.UndertowRetargetLockout);
    }

    private bool TryStartUndertowEffect(
        ITowerView tower,
        TowerInstance? towerNode,
        EnemyInstance target,
        float strengthMultiplier,
        bool isSecondary,
        bool isFollowup,
        bool enableEndpointPulse,
        float delaySeconds = 0f)
    {
        if (!IsUsableUndertowTarget(target))
            return false;

        float pull = Balance.UndertowPullDistance;
        pull *= (1f + CountModifierCopies(tower, "focus_lens") * Balance.UndertowFocusLensPullPerCopy);
        if (target.IsMarked || target.DamageAmpRemaining > 0f || target.SlowRemaining > 0f)
            pull *= (1f + Balance.UndertowMarkedSusceptibilityBonus);

        pull *= ResolveUndertowResistance(target);
        pull *= ResolveUndertowRecentMultiplier(target);
        pull *= MathF.Max(0f, strengthMultiplier);
        if (isFollowup)
            pull *= Balance.UndertowFeedbackFollowupMultiplier;
        pull = MathF.Min(Balance.UndertowPullDistanceCap, pull);
        pull = MathF.Min(pull, target.Progress);
        if (pull < Balance.UndertowMinEffectivePull)
            return false;

        float duration = Balance.UndertowDuration;
        if (isSecondary)
            duration *= Balance.UndertowSecondaryDurationMultiplier;
        if (isFollowup)
            duration *= 0.82f;
        duration = MathF.Max(0.12f, duration);

        var effect = new ActiveUndertow
        {
            SourceTower = tower,
            SourceNode = towerNode,
            Target = target,
            TotalDuration = duration,
            Remaining = duration,
            TotalPullDistance = pull,
            PulledDistance = 0f,
            SlowFactor = ResolveUndertowSlowFactor(tower, isSecondary, isFollowup),
            IsSecondary = isSecondary,
            EnableEndpointPulse = enableEndpointPulse,
            IsFollowup = isFollowup,
            DelayRemaining = MathF.Max(0f, delaySeconds),
        };

        _activeUndertows.Add(effect);
        if (effect.DelayRemaining <= 0f)
        {
            RegisterUndertowAffect(target);
            Statuses.ApplySlow(target, MathF.Max(0.12f, duration), effect.SlowFactor);
            if (!BotMode && towerNode != null && GodotObject.IsInstanceValid(towerNode) && LanePath != null)
            {
                var tether = new UndertowTetherVfx();
                LanePath.GetParent().AddChild(tether);
                tether.GlobalPosition = Vector2.Zero;
                tether.Initialize(towerNode, target, towerNode.ProjectileColor, duration, isSecondary || isFollowup);
                effect.TetherVfx = tether;
            }
        }

        return true;
    }

    private EnemyInstance? SelectUndertowSecondaryTarget(
        ITowerView tower,
        EnemyInstance primary,
        List<EnemyInstance> enemies,
        HashSet<EnemyInstance> excluded,
        float searchRadius)
    {
        EnemyInstance? best = null;
        float bestDist = searchRadius;
        foreach (EnemyInstance enemy in enemies)
        {
            if (!IsUsableUndertowTarget(enemy) || ReferenceEquals(enemy, primary) || excluded.Contains(enemy))
                continue;
            if (!IsInRange(tower, enemy))
                continue;

            float d = primary.GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (d > searchRadius)
                continue;

            if (best == null
                || d < bestDist - 0.001f
                || (MathF.Abs(d - bestDist) <= 0.001f && enemy.GetInstanceId() < best.GetInstanceId()))
            {
                best = enemy;
                bestDist = d;
            }
        }
        return best;
    }

    private int ApplyUndertowSecondaryTugs(
        ITowerView tower,
        TowerInstance? towerNode,
        EnemyInstance primaryTarget,
        List<EnemyInstance> enemies)
    {
        int applied = 0;
        var excluded = new HashSet<EnemyInstance> { primaryTarget };

        if (tower.SplitCount > 0)
        {
            EnemyInstance? splitTarget = SelectUndertowSecondaryTarget(
                tower,
                primaryTarget,
                enemies,
                excluded,
                Balance.UndertowSecondarySearchRadius);
            if (splitTarget != null && TryStartUndertowEffect(
                tower,
                towerNode,
                splitTarget,
                Balance.UndertowSplitSecondaryMultiplier,
                isSecondary: true,
                isFollowup: false,
                enableEndpointPulse: false))
            {
                applied++;
                excluded.Add(splitTarget);
                GameController.Instance?.RegisterSpectacleProc(
                    tower,
                    SpectacleDefinitions.SplitShot,
                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot),
                    tower.BaseDamage * Balance.UndertowSplitSecondaryMultiplier);
            }
        }

        if (tower.ChainCount > 0)
        {
            float searchRadius = MathF.Max(Balance.UndertowSecondarySearchRadius, tower.ChainRange);
            EnemyInstance chainAnchor = primaryTarget;
            for (int link = 0; link < tower.ChainCount; link++)
            {
                EnemyInstance? chainTarget = SelectUndertowSecondaryTarget(
                    tower,
                    chainAnchor,
                    enemies,
                    excluded,
                    searchRadius);
                if (chainTarget == null)
                    break;

                // Mark this target as consumed regardless of whether it can start an effect,
                // so later links can still try other nearby enemies.
                excluded.Add(chainTarget);

                bool started = TryStartUndertowEffect(
                    tower,
                    towerNode,
                    chainTarget,
                    Balance.UndertowChainSecondaryMultiplier,
                    isSecondary: true,
                    isFollowup: false,
                    enableEndpointPulse: false);
                if (!started)
                    continue;

                applied++;
                if (!BotMode && towerNode != null)
                    SpawnChainArc(chainAnchor.GlobalPosition, chainTarget.GlobalPosition, towerNode.ProjectileColor, intensity: 0.94f);

                GameController.Instance?.RegisterSpectacleProc(
                    tower,
                    SpectacleDefinitions.ChainReaction,
                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction),
                    tower.BaseDamage * Balance.UndertowChainSecondaryMultiplier);

                chainAnchor = chainTarget;
            }
        }

        return applied;
    }

    private void ScheduleUndertowFeedbackFollowup(
        ITowerView tower,
        TowerInstance? towerNode,
        EnemyInstance primaryTarget)
    {
        int feedbackCopies = CountModifierCopies(tower, "feedback_loop");
        if (feedbackCopies <= 0)
            return;

        float chance = Mathf.Clamp(feedbackCopies * Balance.UndertowFeedbackFollowupChance, 0f, 0.72f);
        if (_mineRng.NextDouble() > chance)
            return;

        TryStartUndertowEffect(
            tower,
            towerNode,
            primaryTarget,
            strengthMultiplier: 1f,
            isSecondary: true,
            isFollowup: true,
            enableEndpointPulse: false,
            delaySeconds: Balance.UndertowFeedbackFollowupDelay);
    }

    private void CompleteUndertowEffect(ActiveUndertow effect, List<EnemyInstance> enemies)
    {
        if (!IsUsableUndertowTarget(effect.Target))
            return;
        if (effect.PulledDistance <= 0.01f)
            return;

        if (effect.EnableEndpointPulse)
            ApplyUndertowEndpointCompression(effect, enemies);

        if (!BotMode && effect.PulledDistance >= Balance.UndertowMinEffectivePull)
        {
            Sounds?.Play("undertow_release", pitchScale: 0.95f + (float)(_mineRng.NextDouble() - 0.5) * 0.08f);
        }
    }

    private void ApplyUndertowEndpointCompression(ActiveUndertow effect, List<EnemyInstance> enemies)
    {
        int blastCopies = CountModifierCopies(effect.SourceTower, "blast_core");
        float radius = Balance.UndertowEndpointBaseRadius + blastCopies * Balance.UndertowEndpointRadiusPerBlastCore;
        float basePull = Balance.UndertowEndpointBasePull + blastCopies * Balance.UndertowEndpointPullPerBlastCore;
        if (radius <= 0f || basePull <= 0f)
            return;

        foreach (EnemyInstance enemy in enemies)
        {
            if (!IsUsableUndertowTarget(enemy))
                continue;
            if (enemy.GlobalPosition.DistanceTo(effect.Target.GlobalPosition) > radius)
                continue;

            float pull = basePull;
            if (!ReferenceEquals(enemy, effect.Target))
                pull *= 0.72f;
            pull *= ResolveUndertowResistance(enemy);
            pull *= ResolveUndertowRecentMultiplier(enemy);

            float start = enemy.Progress;
            float end = MathF.Max(0f, start - pull);
            float moved = start - end;
            if (moved <= 0.001f)
                continue;

            enemy.Progress = end;
            if (!ReferenceEquals(enemy, effect.Target))
                Statuses.ApplySlow(enemy, Balance.UndertowEndpointSlowDuration, Balance.UndertowEndpointSlowFactor);
        }

        if (!BotMode && LanePath != null)
        {
            var pulse = new UndertowReleaseVfx();
            LanePath.GetParent().AddChild(pulse);
            pulse.GlobalPosition = effect.Target.GlobalPosition;
            Color pulseColor = effect.SourceNode?.ProjectileColor ?? new Color(0.08f, 0.64f, 0.86f);
            pulse.Initialize(pulseColor, radius, blastCopies > 0);
        }
    }

    /// <summary>
    /// Advances Wildfire burn DOT on all burning enemies and manages fire trail segments.
    ///
    /// Called from Step() between tower attacks (step 3) and dead-enemy removal (step 4)
    /// so burn kills are caught by the same cleanup pass as tower kills.
    ///
    /// Anti-recursion guarantees (structural, no flag needed):
    ///   - Burn DOT and trail damage are raw HP reductions -- no modifier OnHit pipeline is called.
    ///   - Only Wildfire.OnHit() writes BurnRemaining; that method is only invoked by DamageModel
    ///     from direct tower attack dispatch. Burn ticks never call DamageModel.Apply.
    ///   - Trail damage does NOT write BurnRemaining onto enemies it hits. Structural guarantee.
    /// </summary>
    private void UpdateBurnAndTrails(float delta, int waveIndex, List<EnemyInstance> enemies)
    {
        // A. Advance burn timers, apply burn DOT, and deposit trail segments.
        foreach (var enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f) continue;
            if (enemy.BurnRemaining <= 0f) continue;

            // Tick burn timer.
            enemy.BurnRemaining = MathF.Max(0f, enemy.BurnRemaining - delta);

            // Apply burn damage proportional to elapsed delta.
            float burnDamage = enemy.BurnDamagePerSecond * delta;
            if (burnDamage > 0.001f)
            {
                float hpBefore = enemy.Hp;
                enemy.Hp = MathF.Max(0f, enemy.Hp - burnDamage);
                float dealt = hpBefore - enemy.Hp;

                if (dealt > 0f && _state != null)
                {
                    bool isKill = enemy.Hp <= 0f;
                    _state.TrackBaseAttackDamage(enemy.BurnOwnerSlotIndex, (int)dealt,
                        isKill, enemy.ProgressRatio);
                }

                // Spectacle: register kill events so Wildfire-heavy builds can surge on burn kills.
                // Per-tick spectacle is only sent on kills to prevent meter spam.
                if (enemy.Hp <= 0f && enemy.BurnOwnerSlotIndex >= 0
                    && _state != null && enemy.BurnOwnerSlotIndex < _state.Slots.Length)
                {
                    var ownerTower = _state.Slots[enemy.BurnOwnerSlotIndex].Tower;
                    if (ownerTower != null)
                        GameController.Instance?.RegisterSpectacleProc(ownerTower, "wildfire",
                            SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.Wildfire), dealt);
                }

                // Damage numbers for burn ticks -- small scale to distinguish from primary hits.
                if (!BotMode && dealt >= 1f)
                    SpawnDamageNumber(enemy.GlobalPosition + new Godot.Vector2(6f, 0f),
                        MathF.Max(1f, dealt), enemy.Hp <= 0f, "wildfire", WildfireFireColor, scale: 0.72f);
            }

            // Trail drop: count down and deposit a segment at the enemy's current position.
            // Only deposit while burn is still active (BurnRemaining > 0 after tick).
            if (enemy.BurnRemaining > 0f && enemy.BurnDamagePerSecond > 0f)
            {
                enemy.BurnTrailDropTimer -= delta;
                if (enemy.BurnTrailDropTimer <= 0f)
                {
                    enemy.BurnTrailDropTimer = Balance.WildfireTrailDropInterval;
                    DropFireTrail(enemy);
                }
            }
        }

        // B. Advance fire trail lifetimes, update visuals, apply trail hazard damage.
        for (int i = _activeFireTrails.Count - 1; i >= 0; i--)
        {
            var trail = _activeFireTrails[i];
            trail.LifetimeRemaining -= delta;

            if (trail.LifetimeRemaining <= 0f)
            {
                // Trail expired -- free the visual and remove the segment.
                if (!BotMode && trail.Visual != null && GodotObject.IsInstanceValid(trail.Visual))
                    trail.Visual.ExpireAndFree();
                _activeFireTrails.RemoveAt(i);
                continue;
            }

            // Update visual fade.
            if (!BotMode && trail.Visual != null && GodotObject.IsInstanceValid(trail.Visual))
                trail.Visual.SetLifetimeFraction(trail.LifetimeRemaining / trail.TotalLifetime);

            // Apply trail hazard damage to enemies in radius.
            // Raw HP reduction -- no modifier pipeline, no Burning re-application.
            float trailDamage = trail.DamagePerSecond * delta;
            if (trailDamage <= 0.001f) continue;

            foreach (var enemy in enemies)
            {
                if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f) continue;
                if (trail.Position.DistanceTo(enemy.GlobalPosition) > Balance.WildfireTrailRadius) continue;

                float hpBefore = enemy.Hp;
                enemy.Hp = MathF.Max(0f, enemy.Hp - trailDamage);
                float dealt = hpBefore - enemy.Hp;

                if (dealt > 0f && _state != null)
                {
                    bool isKill = enemy.Hp <= 0f;
                    _state.TrackBaseAttackDamage(trail.OwnerSlotIndex, (int)dealt,
                        isKill, enemy.ProgressRatio);

                    // Spectacle: trail kills are minor events but still register.
                    if (isKill && trail.OwnerSlotIndex >= 0
                        && trail.OwnerSlotIndex < _state.Slots.Length)
                    {
                        var ownerTower = _state.Slots[trail.OwnerSlotIndex].Tower;
                        if (ownerTower != null)
                            GameController.Instance?.RegisterSpectacleProc(ownerTower, "wildfire",
                            SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.Wildfire), dealt);
                    }
                }

                // Accumulate fractional trail damage per enemy; pop a number once ≥ 1 HP builds up.
                if (!BotMode && dealt > 0f)
                {
                    ulong eid = enemy.GetInstanceId();
                    _wildfireTrailDmgAccumulator.TryGetValue(eid, out float acc);
                    acc += dealt;
                    if (acc >= 1f)
                    {
                        float shown = MathF.Floor(acc);
                        SpawnDamageNumber(enemy.GlobalPosition + new Godot.Vector2(-6f, 0f),
                            shown, enemy.Hp <= 0f, "wildfire", WildfireFireColor, scale: 0.72f);
                        acc -= shown;
                    }

                    // Flush tiny remainder on kill so the final trail tick has visible feedback.
                    if (enemy.Hp <= 0f && acc > 0.01f)
                    {
                        SpawnDamageNumber(enemy.GlobalPosition + new Godot.Vector2(-6f, 0f),
                            1f, isKill: true, "wildfire", WildfireFireColor, scale: 0.72f);
                        acc = 0f;
                    }

                    if (acc <= 0.0001f)
                        _wildfireTrailDmgAccumulator.Remove(eid);
                    else
                        _wildfireTrailDmgAccumulator[eid] = acc;
                }
            }
        }

        if (!BotMode && _wildfireTrailDmgAccumulator.Count > 0)
        {
            // Prune dead/invalid enemy ids so this cache doesn't grow unbounded.
            var liveIds = new HashSet<ulong>();
            foreach (var enemy in enemies)
            {
                if (GodotObject.IsInstanceValid(enemy) && enemy.Hp > 0f)
                    liveIds.Add(enemy.GetInstanceId());
            }

            var stale = new List<ulong>();
            foreach (ulong eid in _wildfireTrailDmgAccumulator.Keys)
            {
                if (!liveIds.Contains(eid))
                    stale.Add(eid);
            }

            foreach (ulong eid in stale)
                _wildfireTrailDmgAccumulator.Remove(eid);
        }
    }

    /// <summary>
    /// Returns the next spawn timer interval after spawning <paramref name="typeId"/>.
    /// Basic walkers alternate between a short gap (within pair) and long gap (between pairs)
    /// to create a "2s arriving in a cluster" feel without changing total wave density.
    /// All other enemy types use the unmodified base interval.
    /// </summary>
    private float NextSpawnInterval(string typeId, float baseInterval)
    {
        if (typeId != "basic_walker")
            return baseInterval;

        bool isFirst = _walkerNextIsFirst;
        _walkerNextIsFirst = !_walkerNextIsFirst;
        return isFirst
            ? baseInterval * Balance.BasicWalkerGroupShortInterval  // tight gap inside pair
            : baseInterval * Balance.BasicWalkerGroupLongInterval;  // breathing room between pairs
    }

    private void DropFireTrail(EnemyInstance enemy)
    {
        float trailDps = enemy.BurnDamagePerSecond * Balance.WildfireTrailDamageRatio;
        if (trailDps <= 0f) return;

        // Global segment cap: when full, remove the oldest (lowest index) to make room.
        // This keeps behavior predictable under rapid-fire + Hair Trigger combinations.
        if (_activeFireTrails.Count >= Balance.WildfireMaxTrailSegments)
        {
            var oldest = _activeFireTrails[0];
            if (!BotMode && oldest.Visual != null && GodotObject.IsInstanceValid(oldest.Visual))
                oldest.Visual.ExpireAndFree();
            _activeFireTrails.RemoveAt(0);
        }

        WildfireTrailVfx? visual = null;
        if (!BotMode && LanePath != null)
        {
            visual = new WildfireTrailVfx();
            LanePath.GetParent().AddChild(visual);
            visual.GlobalPosition = enemy.GlobalPosition;
            visual.Initialize(Balance.WildfireTrailLifetime, enemy.Heading);
        }

        _activeFireTrails.Add(new FireTrailSegment
        {
            Position = enemy.GlobalPosition,
            Direction = enemy.Heading,
            TotalLifetime = Balance.WildfireTrailLifetime,
            LifetimeRemaining = Balance.WildfireTrailLifetime,
            DamagePerSecond = trailDps,
            OwnerSlotIndex = enemy.BurnOwnerSlotIndex,
            Visual = visual,
        });

        // Audio: subtle crackle cue for trail deposit. Throttled naturally by TrailDropInterval.
        Sounds?.Play("wildfire_trail", pitchScale: 0.85f + GD.Randf() * 0.30f);
    }

    private void UpdateAfterimages(float delta, List<EnemyInstance> enemies)
    {
        if (_activeAfterimages.Count == 0 || delta <= 0f)
            return;

        for (int i = _activeAfterimages.Count - 1; i >= 0; i--)
        {
            ActiveAfterimage imprint = _activeAfterimages[i];
            if (imprint.SourceNode != null && !GodotObject.IsInstanceValid(imprint.SourceNode))
            {
                if (imprint.Visual != null && GodotObject.IsInstanceValid(imprint.Visual))
                    imprint.Visual.QueueFree();
                _activeAfterimages.RemoveAt(i);
                continue;
            }

            imprint.DelayRemaining = Mathf.Max(0f, imprint.DelayRemaining - delta);
            if (!BotMode && imprint.Visual != null && GodotObject.IsInstanceValid(imprint.Visual))
                imprint.Visual.SetDelayRemaining(imprint.DelayRemaining, imprint.DelayTotal);

            if (imprint.DelayRemaining > 0f)
                continue;

            TriggerAfterimageEcho(imprint, enemies);

            if (imprint.Visual != null && GodotObject.IsInstanceValid(imprint.Visual))
                imprint.Visual.TriggerAndFree();

            _activeAfterimages.RemoveAt(i);
        }
    }

    private void TriggerAfterimageEcho(ActiveAfterimage imprint, List<EnemyInstance> enemies)
    {
        ITowerView tower = imprint.SourceTower;
        _afterimageFollowupsConsumed = false;
        int copies = Math.Max(1, CountModifierCopies(tower, "afterimage"));
        float radius = ResolveAfterimageRadius(tower, copies);
        float baseDamage = Mathf.Max(Balance.AfterimageMinDamage, imprint.SeedDamage * ResolveAfterimageDamageScale(tower, copies));
        Color tint = ResolveAfterimageTint(tower);
        var inRange = enemies
            .Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f && imprint.Position.DistanceTo(e.GlobalPosition) <= radius)
            .ToList();

        Sounds?.Play("afterimage_echo", pitchScale: ResolveAfterimageEchoPitch(tower));

        if (inRange.Count == 0)
            return;

        switch (tower.TowerId)
        {
            case "heavy_cannon":
                ApplyAfterimageBurst(tower, imprint.Position, inRange, baseDamage * 1.06f,
                    Balance.AfterimageHeavyBurstRadius + (copies - 1) * 6f,
                    maxTargets: 3, color: tint, applyFalloff: true);
                break;
            case "rocket_launcher":
                ApplyAfterimageBurst(tower, imprint.Position, inRange, baseDamage,
                    Balance.AfterimageRocketBurstRadius + (copies - 1) * 8f,
                    maxTargets: Balance.AfterimageMaxTargetsPerEcho, color: tint, applyFalloff: true);
                break;
            case "rift_prism":
                ApplyAfterimageBurst(tower, imprint.Position, inRange, baseDamage * 0.92f,
                    Balance.AfterimageRiftBurstRadius + (copies - 1) * 7f,
                    maxTargets: 3, color: tint, applyFalloff: true);
                break;
            case "chain_tower":
                ApplyAfterimageChain(tower, imprint.Position, inRange, baseDamage, tint, copies);
                break;
            case "marker_tower":
                ApplyAfterimageMarkerPulse(tower, imprint.Position, inRange, baseDamage * 0.82f, tint);
                break;
            case "phase_splitter":
                ApplyAfterimagePhaseSplitEcho(tower, imprint.Position, inRange, baseDamage * 0.88f, tint);
                break;
            case "undertow_engine":
                ApplyAfterimageUndertowPulse(tower, imprint.Position, inRange, tint);
                break;
            case "accordion_engine":
                ApplyAfterimageBurst(tower, imprint.Position, inRange, baseDamage * 0.76f,
                    Balance.AfterimageAccordionPulseRadius + (copies - 1) * 8f,
                    maxTargets: Balance.AfterimageMaxTargetsPerEcho, color: tint, applyFalloff: false);
                break;
            case "rapid_shooter":
            default:
                EnemyInstance? primary = SelectAfterimagePrimaryTarget(tower, imprint.Position, inRange);
                if (primary != null)
                    ApplyAfterimageDirectHit(tower, primary, baseDamage, tint);

                if (tower.TowerId == "rapid_shooter")
                {
                    EnemyInstance? bonus = inRange
                        .Where(e => !ReferenceEquals(e, primary))
                        .OrderBy(e => imprint.Position.DistanceTo(e.GlobalPosition))
                        .FirstOrDefault();
                    if (bonus != null && imprint.Position.DistanceTo(bonus.GlobalPosition) <= radius * 0.65f)
                        ApplyAfterimageDirectHit(tower, bonus, baseDamage * 0.46f, tint, numberScale: 0.86f);
                }
                break;
        }
    }

    private EnemyInstance? SelectAfterimagePrimaryTarget(ITowerView tower, Vector2 origin, List<EnemyInstance> candidates)
    {
        if (candidates.Count == 0)
            return null;

        EnemyInstance best = candidates[0];
        foreach (EnemyInstance candidate in candidates)
        {
            if (ReferenceEquals(candidate, best))
                continue;

            bool candidatePreferred = PreferTargetByMode(candidate, best, tower.TargetingMode);
            if (candidatePreferred)
            {
                best = candidate;
                continue;
            }

            bool bestPreferred = PreferTargetByMode(best, candidate, tower.TargetingMode);
            if (!bestPreferred)
            {
                float cDist = origin.DistanceTo(candidate.GlobalPosition);
                float bDist = origin.DistanceTo(best.GlobalPosition);
                if (cDist < bDist)
                    best = candidate;
            }
        }
        return best;
    }

    private static Color ResolveAfterimageTint(ITowerView tower)
    {
        if (tower is TowerInstance node)
        {
            Color c = node.ProjectileColor;
            return new Color(
                Mathf.Clamp(c.R * 0.84f + 0.16f, 0f, 1f),
                Mathf.Clamp(c.G * 0.88f + 0.12f, 0f, 1f),
                Mathf.Clamp(c.B * 0.94f + 0.08f, 0f, 1f),
                1f);
        }
        return AfterimageColor;
    }

    private static float ResolveAfterimageDamageScale(ITowerView tower, int copies)
    {
        float scale = Balance.AfterimageBaseDamageRatio * (1f + (copies - 1) * 0.16f);
        return tower.TowerId switch
        {
            "heavy_cannon" => scale * 1.10f,
            "rocket_launcher" => scale * 0.96f,
            "marker_tower" => scale * 0.78f,
            "chain_tower" => scale * 0.90f,
            "undertow_engine" => scale * 0.74f,
            _ => scale
        };
    }

    private static float ResolveAfterimageRadius(ITowerView tower, int copies)
    {
        float radius = Balance.AfterimageBaseRadius + (copies - 1) * 8f;
        return tower.TowerId switch
        {
            "heavy_cannon" => radius * 0.92f,
            "rocket_launcher" => radius * 1.05f,
            "marker_tower" => radius * 1.06f,
            "undertow_engine" => radius * 1.08f,
            "phase_splitter" => radius * 1.02f,
            _ => radius
        };
    }

    private static float ResolveAfterimageSeedPitch(ITowerView tower) => tower.TowerId switch
    {
        "heavy_cannon" => 0.84f,
        "rocket_launcher" => 0.88f,
        "undertow_engine" => 0.82f,
        _ => 0.96f,
    };

    private static float ResolveAfterimageEchoPitch(ITowerView tower) => tower.TowerId switch
    {
        "rapid_shooter" => 1.12f,
        "phase_splitter" => 1.08f,
        "heavy_cannon" => 0.86f,
        "rocket_launcher" => 0.90f,
        "undertow_engine" => 0.84f,
        _ => 0.98f,
    };

    private float ApplyAfterimageDirectHit(
        ITowerView tower,
        EnemyInstance target,
        float damage,
        Color color,
        float numberScale = 0.92f)
    {
        if (!GodotObject.IsInstanceValid(target) || target.Hp <= 0f || damage <= 0f)
            return 0f;

        var ctx = new DamageContext(
            tower,
            target,
            _state.WaveIndex,
            _state.EnemiesAlive,
            _state,
            isChain: false,
            damageOverride: damage,
            suppressAfterimageSeed: true);
        DamageModel.Apply(ctx);

        float dealt = ctx.DamageDealt;
        if (dealt <= 0.001f)
            return 0f;

        if (!_afterimageFollowupsConsumed)
        {
            _afterimageFollowupsConsumed = true;
            ApplyAfterimageTargetingFollowups(tower, target, color);
        }

        if (!BotMode)
        {
            Color numberColor = new(
                Mathf.Clamp(color.R * 0.78f + 0.22f, 0f, 1f),
                Mathf.Clamp(color.G * 0.78f + 0.22f, 0f, 1f),
                Mathf.Clamp(color.B * 0.78f + 0.22f, 0f, 1f),
                1f);
            Vector2 offset = ResolveAfterimageNumberOffset(target, tower);
            SpawnDamageNumber(target.GlobalPosition + offset, MathF.Max(1f, dealt), target.Hp <= 0f, tower.TowerId, numberColor, scale: numberScale);
            if (target.Hp > 0f && GodotObject.IsInstanceValid(target))
                target.FlashHit();
        }

        return dealt;
    }

    private void ApplyAfterimageTargetingFollowups(ITowerView tower, EnemyInstance primary, Color color)
    {
        int splitHits = CombatResolution.ApplySplitHits(
            tower,
            primary,
            _state.WaveIndex,
            _state.EnemiesAlive,
            _state,
            onHit: ctx =>
            {
                if (BotMode || ctx.Target is not EnemyInstance splitTarget)
                    return;
                if (ctx.DamageDealt <= 0.01f)
                    return;

                bool isKill = splitTarget.Hp <= 0f;
                SpawnDamageNumber(splitTarget.GlobalPosition, MathF.Max(1f, ctx.DamageDealt), isKill, tower.TowerId, color, scale: 0.88f);
                if (!isKill && GodotObject.IsInstanceValid(splitTarget))
                    splitTarget.FlashHit();
            },
            suppressAfterimageSeed: true);
        if (splitHits > 0)
        {
            float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.SplitShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
        }

        EnemyInstance previous = primary;
        int chainHits = CombatResolution.ApplyChainHits(
            tower,
            primary,
            _state.WaveIndex,
            _state.EnemiesAlive,
            _state,
            onHit: ctx =>
            {
                if (ctx.Target is not EnemyInstance chainTarget)
                    return;

                if (!BotMode)
                {
                    if (GodotObject.IsInstanceValid(previous) && GodotObject.IsInstanceValid(chainTarget))
                        SpawnChainArc(previous.GlobalPosition, chainTarget.GlobalPosition, color, intensity: 0.84f);

                    if (ctx.DamageDealt > 0.01f)
                    {
                        bool isKill = chainTarget.Hp <= 0f;
                        SpawnDamageNumber(chainTarget.GlobalPosition, MathF.Max(1f, ctx.DamageDealt), isKill, tower.TowerId, color, scale: 0.88f);
                        if (!isKill && GodotObject.IsInstanceValid(chainTarget))
                            chainTarget.FlashHit();
                    }
                }

                previous = chainTarget;
            },
            suppressAfterimageSeed: true);
        _state.TrackSpectacleChainDepth(chainHits);
        if (chainHits > 0)
        {
            float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
        }
    }

    private static Vector2 ResolveAfterimageNumberOffset(EnemyInstance target, ITowerView tower)
    {
        ulong id = target.GetInstanceId();
        int towerHash = (tower.TowerId ?? string.Empty).GetHashCode() & 0x7fffffff;
        float normalized = (float)((id % 53UL) + (ulong)(towerHash % 29)) / 82f;
        float angle = normalized * Mathf.Tau;
        float radius = 9f + (float)(id % 3UL);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius + new Vector2(0f, -5f);
    }

    private void ApplyAfterimageBurst(
        ITowerView tower,
        Vector2 origin,
        List<EnemyInstance> candidates,
        float baseDamage,
        float radius,
        int maxTargets,
        Color color,
        bool applyFalloff)
    {
        int hits = 0;
        foreach (EnemyInstance enemy in candidates
                     .OrderBy(e => origin.DistanceTo(e.GlobalPosition))
                     .ThenByDescending(e => e.Progress))
        {
            if (hits >= maxTargets)
                break;

            float dist = origin.DistanceTo(enemy.GlobalPosition);
            if (dist > radius)
                continue;

            float falloff = applyFalloff ? Mathf.Lerp(1f, 0.72f, dist / MathF.Max(1f, radius)) : 1f;
            float dealt = ApplyAfterimageDirectHit(tower, enemy, baseDamage * falloff, color, numberScale: 0.88f);
            if (dealt > 0f)
                hits++;
        }
    }

    private void ApplyAfterimageChain(
        ITowerView tower,
        Vector2 origin,
        List<EnemyInstance> candidates,
        float baseDamage,
        Color color,
        int copies)
    {
        EnemyInstance? first = SelectAfterimagePrimaryTarget(tower, origin, candidates);
        if (first == null)
            return;

        var alreadyHit = new HashSet<EnemyInstance>();
        EnemyInstance current = first;
        float damage = baseDamage;
        int maxBounces = Mathf.Clamp(1 + (copies - 1), 1, 2);
        float range = MathF.Max(36f, tower.ChainRange * Balance.AfterimageChainRangeMultiplier);

        for (int hop = 0; hop <= maxBounces; hop++)
        {
            if (!GodotObject.IsInstanceValid(current) || current.Hp <= 0f)
                break;

            float dealt = ApplyAfterimageDirectHit(tower, current, damage, color, numberScale: 0.90f);
            if (dealt <= 0f)
                break;

            alreadyHit.Add(current);
            if (hop == maxBounces)
                break;

            EnemyInstance? next = candidates
                .Where(e => !alreadyHit.Contains(e) && GodotObject.IsInstanceValid(e) && e.Hp > 0f)
                .OrderBy(e => current.GlobalPosition.DistanceTo(e.GlobalPosition))
                .FirstOrDefault(e => current.GlobalPosition.DistanceTo(e.GlobalPosition) <= range);
            if (next == null)
                break;

            if (!BotMode)
                SpawnChainArc(current.GlobalPosition, next.GlobalPosition, color, intensity: 0.82f);

            current = next;
            damage *= Balance.AfterimageChainBounceDamageDecay;
        }
    }

    private void ApplyAfterimageMarkerPulse(
        ITowerView tower,
        Vector2 origin,
        List<EnemyInstance> candidates,
        float baseDamage,
        Color color)
    {
        EnemyInstance? primary = SelectAfterimagePrimaryTarget(tower, origin, candidates);
        if (primary != null)
        ApplyAfterimageDirectHit(tower, primary, baseDamage, color, numberScale: 0.90f);

        foreach (EnemyInstance enemy in candidates)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f)
                continue;
            if (origin.DistanceTo(enemy.GlobalPosition) > Balance.AfterimageMarkerPulseRadius)
                continue;
            Statuses.ApplyMarked(enemy, Balance.AfterimageMarkerPulseMarkDuration);
        }
    }

    private void ApplyAfterimagePhaseSplitEcho(
        ITowerView tower,
        Vector2 origin,
        List<EnemyInstance> candidates,
        float baseDamage,
        Color color)
    {
        EnemyInstance? front = candidates.OrderByDescending(e => e.Progress).FirstOrDefault();
        EnemyInstance? back = candidates.OrderBy(e => e.Progress).FirstOrDefault();

        if (front != null)
            ApplyAfterimageDirectHit(tower, front, baseDamage, color, numberScale: 0.88f);

        if (back != null && !ReferenceEquals(back, front))
            ApplyAfterimageDirectHit(tower, back, baseDamage, color, numberScale: 0.88f);
    }

    private void ApplyAfterimageUndertowPulse(
        ITowerView tower,
        Vector2 origin,
        List<EnemyInstance> candidates,
        Color color)
    {
        int maxTargets = Math.Min(3, Balance.AfterimageMaxTargetsPerEcho);
        int affected = 0;
        foreach (EnemyInstance enemy in candidates
                     .OrderByDescending(e => e.Progress)
                     .ThenBy(e => origin.DistanceTo(e.GlobalPosition)))
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f)
                continue;
            if (origin.DistanceTo(enemy.GlobalPosition) > Balance.AfterimageUndertowPulseRadius)
                continue;

            float start = enemy.Progress;
            float end = MathF.Max(0f, start - Balance.AfterimageUndertowPullDistance);
            float moved = start - end;
            if (moved <= 0.01f)
                continue;

            enemy.Progress = end;
            Statuses.ApplySlow(enemy, Balance.AfterimageUndertowSlowDuration, Balance.AfterimageUndertowSlowFactor);
            affected++;

            if (!BotMode)
                enemy.FlashHit();

            if (affected >= maxTargets)
                break;
        }

        if (!BotMode && affected > 0 && LanePath != null)
        {
            var pulse = new UndertowReleaseVfx();
            LanePath.GetParent().AddChild(pulse);
            pulse.GlobalPosition = origin;
            pulse.Initialize(color, Balance.AfterimageUndertowPulseRadius * 0.74f, major: false);
        }
    }

    private static int FindTowerSlotIndex(RunState state, ITowerView tower)
    {
        for (int i = 0; i < state.Slots.Length; i++)
        {
            if (ReferenceEquals(state.Slots[i].Tower, tower))
                return i;
        }
        return -1;
    }

    private int GetRiftBurstFastPlantsUsed(ITowerView tower)
    {
        if (tower is not TowerInstance inst)
            return Balance.RiftMineBurstFastPlantsPerTower;
        ulong id = inst.GetInstanceId();
        return _riftBurstFastPlantsUsed.TryGetValue(id, out int count) ? count : 0;
    }

    private void IncrementRiftBurstFastPlantsUsed(ITowerView tower)
    {
        if (tower is not TowerInstance inst)
            return;
        ulong id = inst.GetInstanceId();
        _riftBurstFastPlantsUsed.TryGetValue(id, out int count);
        _riftBurstFastPlantsUsed[id] = count + 1;
    }

    private void RebuildMineAnchors()
    {
        _mineAnchors.Clear();
        if (LanePath?.Curve == null) return;

        var curve = LanePath.Curve;
        float length = curve.GetBakedLength();
        if (length <= 0.01f) return;

        float step = Mathf.Max(8f, Balance.RiftMineAnchorStep);
        int count = Mathf.Max(8, Mathf.CeilToInt(length / step));
        if (count < 3) return;

        for (int i = 1; i < count - 1; i++)
        {
            float d0 = Mathf.Max(0f, (i - 1) * step);
            float d1 = i * step;
            float d2 = Mathf.Min(length, (i + 1) * step);

            Vector2 p0 = LanePath.ToGlobal(curve.SampleBaked(d0));
            Vector2 p1 = LanePath.ToGlobal(curve.SampleBaked(d1));
            Vector2 p2 = LanePath.ToGlobal(curve.SampleBaked(d2));

            var a = (p1 - p0).Normalized();
            var b = (p2 - p1).Normalized();
            float turn = 1f - Mathf.Clamp(a.Dot(b), -1f, 1f); // 0 = straight, 1 = hard bend

            // Slight anti-symmetry jitter avoids all mines selecting identical mirrored anchors.
            float jitter = 0.11f * Mathf.Sin(i * 0.73f);
            _mineAnchors.Add(new MineAnchor(p1, turn * 2.2f + jitter));
        }
    }

    private void ResolveMineTriggers(float delta, int waveIndex, List<EnemyInstance> enemies)
    {
        if (_activeMines.Count == 0) return;

        foreach (var mine in _activeMines)
        {
            mine.ArmRemaining = Mathf.Max(0f, mine.ArmRemaining - delta);
            mine.RearmRemaining = Mathf.Max(0f, mine.RearmRemaining - delta);
            if (mine.Visual != null && GodotObject.IsInstanceValid(mine.Visual))
            {
                mine.Visual.SetArmed(mine.ArmRemaining <= 0f && mine.RearmRemaining <= 0f);
                mine.Visual.SetCharges(mine.ChargesRemaining, mine.MaxCharges);
            }
        }

        var triggers = new List<(ActiveMine Mine, EnemyInstance Target)>();
        foreach (var mine in _activeMines)
        {
            if (mine.ArmRemaining > 0f || mine.RearmRemaining > 0f) continue;
            var target = FindTriggerTarget(mine.Position, enemies);
            if (target != null)
                triggers.Add((mine, target));
        }

        foreach (var t in triggers)
        {
            if (!_activeMines.Contains(t.Mine)) continue;
            bool finalPop = t.Mine.ChargesRemaining <= 1;
            float stageMult = finalPop
                ? Balance.RiftMineFinalDamageMultiplier
                : Balance.RiftMineTickDamageMultiplier;
            float damage = t.Mine.Owner.BaseDamage
                * Balance.RiftMineDamageMultiplier
                * t.Mine.DamageScale
                * stageMult;
            DetonateMine(t.Mine, t.Target, waveIndex, enemies, damage, t.Mine.Owner.ChainCount,
                chainSource: false, chainHop: 0, forceFinalPop: false);
        }
    }

    private bool TryPlantMine(ITowerView towerView, List<EnemyInstance> enemies)
    {
        if (towerView is not TowerInstance tower) return false;
        if (CountActiveBaseMinesFor(tower) >= Balance.RiftMineMaxActivePerTower)
            return false;

        Vector2? chosen = PickMineAnchor(tower, enemies);
        if (chosen == null) return false;

        if (!AddMine(tower, chosen.Value, damageScale: 1f))
            return false;

        return true;
    }

    private bool TryPlantMineNear(TowerInstance owner, Vector2 center, float damageScale)
    {
        float splitRadius = Balance.RiftMineSplitPlantRadius;
        float ownerRangeLimit = owner.Range * 0.98f;
        float miniSpacing = Balance.RiftMinePlantSpacing * Balance.RiftMineMiniPlantSpacingMultiplier;

        // First try re-seeding at the exact detonation spot. This keeps Split Shot
        // behavior consistent even when nearby anchors are sparse/crowded.
        if (owner.GlobalPosition.DistanceTo(center) <= ownerRangeLimit
            && IsMineSpotFree(center, miniSpacing))
        {
            return AddMine(
                owner,
                center,
                damageScale,
                armTime: Balance.RiftMineArmTime * 0.75f,
                isMiniMine: true,
                minSpacing: miniSpacing);
        }

        // Prefer valid lane anchors near the pop (stable/readable placement).
        Vector2? best = null;
        float bestScore = float.MinValue;
        foreach (var anchor in _mineAnchors)
        {
            float toCenter = anchor.Position.DistanceTo(center);
            if (toCenter > splitRadius) continue;
            if (owner.GlobalPosition.DistanceTo(anchor.Position) > ownerRangeLimit) continue;
            if (!IsMineSpotFree(anchor.Position, miniSpacing)) continue;

            float score = anchor.Score - toCenter * 0.014f;
            if (score > bestScore)
            {
                bestScore = score;
                best = anchor.Position;
            }
        }

        if (best != null)
            return AddMine(
                owner,
                best.Value,
                damageScale,
                armTime: Balance.RiftMineArmTime * 0.75f,
                isMiniMine: true,
                minSpacing: miniSpacing);

        // Fallback: sample the lane curve at finer resolution near the blast point.
        // This preserves "on-path only" placement while avoiding sparse-anchor misses.
        if (LanePath == null) return false;
        var curve = LanePath.Curve;
        if (curve == null || curve.PointCount < 2) return false;

        float length = curve.GetBakedLength();
        if (length <= 0f) return false;

        float sampleStep = Mathf.Max(6f, Balance.RiftMineAnchorStep * 0.35f);
        Vector2? fallbackBest = null;
        float fallbackBestScore = float.MinValue;
        for (float d = 0f; d <= length; d += sampleStep)
        {
            Vector2 p = LanePath.ToGlobal(curve.SampleBaked(d));
            float toCenter = p.DistanceTo(center);
            if (toCenter > splitRadius) continue;
            if (owner.GlobalPosition.DistanceTo(p) > ownerRangeLimit) continue;
            if (!IsMineSpotFree(p, miniSpacing)) continue;

            float score = -toCenter;
            if (score > fallbackBestScore)
            {
                fallbackBestScore = score;
                fallbackBest = p;
            }
        }

        if (fallbackBest == null) return false;
        return AddMine(
            owner,
            fallbackBest.Value,
            damageScale,
            armTime: Balance.RiftMineArmTime * 0.75f,
            isMiniMine: true,
            minSpacing: miniSpacing);
    }

    private Vector2? PickMineAnchor(TowerInstance tower, List<EnemyInstance> enemies)
    {
        Vector2? fromAnchors = tower.TargetingMode switch
        {
            TargetingMode.First => PickRandomAnchorInRange(tower),
            TargetingMode.Strongest => PickEdgeAnchorInRange(tower, farthest: false),
            TargetingMode.LowestHp => PickEdgeAnchorInRange(tower, farthest: true),
            _ => PickRandomAnchorInRange(tower),
        };
        if (fromAnchors != null)
            return fromAnchors;

        // If all sampled path anchors are occupied, fall back to current path positions of enemies in range.
        return tower.TargetingMode switch
        {
            TargetingMode.First => PickRandomEnemyPointInRange(tower, enemies),
            TargetingMode.Strongest => PickEdgeEnemyPointInRange(tower, enemies, farthest: false),
            TargetingMode.LowestHp => PickEdgeEnemyPointInRange(tower, enemies, farthest: true),
            _ => PickRandomEnemyPointInRange(tower, enemies),
        };
    }

    private Vector2? PickRandomAnchorInRange(TowerInstance tower)
    {
        Vector2? picked = null;
        int seen = 0;
        float rangeLimit = tower.Range * 0.98f;

        foreach (var anchor in _mineAnchors)
        {
            if (tower.GlobalPosition.DistanceTo(anchor.Position) > rangeLimit) continue;
            if (!IsMineSpotFree(anchor.Position)) continue;

            seen++;
            if (_mineRng.Next(seen) == 0)
                picked = anchor.Position;
        }
        return picked;
    }

    private Vector2? PickEdgeAnchorInRange(TowerInstance tower, bool farthest)
    {
        Vector2? picked = null;
        float pickedDist = farthest ? float.MinValue : float.MaxValue;
        float rangeLimit = tower.Range * 0.98f;

        foreach (var anchor in _mineAnchors)
        {
            float dist = tower.GlobalPosition.DistanceTo(anchor.Position);
            if (dist > rangeLimit) continue;
            if (!IsMineSpotFree(anchor.Position)) continue;

            bool better = farthest ? dist > pickedDist : dist < pickedDist;
            if (better)
            {
                picked = anchor.Position;
                pickedDist = dist;
            }
        }
        return picked;
    }

    private Vector2? PickRandomEnemyPointInRange(TowerInstance tower, List<EnemyInstance> enemies)
    {
        Vector2? picked = null;
        int seen = 0;
        foreach (var enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f) continue;
            Vector2 p = enemy.GlobalPosition;
            if (tower.GlobalPosition.DistanceTo(p) > tower.Range) continue;
            if (!IsMineSpotFree(p)) continue;

            seen++;
            if (_mineRng.Next(seen) == 0)
                picked = p;
        }
        return picked;
    }

    private Vector2? PickEdgeEnemyPointInRange(TowerInstance tower, List<EnemyInstance> enemies, bool farthest)
    {
        Vector2? picked = null;
        float pickedDist = farthest ? float.MinValue : float.MaxValue;
        foreach (var enemy in enemies)
        {
            if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f) continue;
            Vector2 p = enemy.GlobalPosition;
            float dist = tower.GlobalPosition.DistanceTo(p);
            if (dist > tower.Range) continue;
            if (!IsMineSpotFree(p)) continue;

            bool better = farthest ? dist > pickedDist : dist < pickedDist;
            if (better)
            {
                picked = p;
                pickedDist = dist;
            }
        }
        return picked;
    }

    private bool AddMine(
        TowerInstance owner,
        Vector2 worldPos,
        float damageScale,
        float armTime = Balance.RiftMineArmTime,
        bool isMiniMine = false,
        float minSpacing = Balance.RiftMinePlantSpacing)
    {
        if (!isMiniMine && CountActiveBaseMinesFor(owner) >= Balance.RiftMineMaxActivePerTower)
            return false;
        if (!IsMineSpotFree(worldPos, minSpacing))
            return false;

        int initialCharges = isMiniMine ? 1 : Balance.RiftMineChargesPerMine;

        RiftMineVisual? visual = null;
        if (!BotMode && LanePath != null)
        {
            ResolveOrderedModifierIds(owner, out string focalModId, out string supportModId, out string tertiaryModId);
            visual = new RiftMineVisual
            {
                ZIndex = 9,
            };
            LanePath.GetParent().AddChild(visual);
            visual.GlobalPosition = worldPos;
            visual.Initialize(
                owner.ProjectileColor,
                damageScale,
                isMiniMine,
                modCount: owner.Modifiers.Count,
                focalModId: focalModId,
                supportModId: supportModId,
                tertiaryModId: tertiaryModId);
            visual.SetCharges(initialCharges, initialCharges);
            visual.SetArmed(armTime <= 0f);
        }

        _activeMines.Add(new ActiveMine
        {
            Id = _nextMineId++,
            Owner = owner,
            Position = worldPos,
            DamageScale = damageScale,
            IsMiniMine = isMiniMine,
            MaxCharges = initialCharges,
            ArmRemaining = Mathf.Max(0f, armTime),
            RearmRemaining = 0f,
            ChargesRemaining = initialCharges,
            Visual = visual
        });
        return true;
    }

    private static void ResolveOrderedModifierIds(
        TowerInstance owner,
        out string focalModId,
        out string supportModId,
        out string tertiaryModId)
    {
        focalModId = string.Empty;
        supportModId = string.Empty;
        tertiaryModId = string.Empty;

        for (int i = 0; i < owner.Modifiers.Count; i++)
        {
            string id = owner.Modifiers[i]?.ModifierId ?? string.Empty;
            if (id.Length == 0)
                continue;

            if (focalModId.Length == 0)
            {
                focalModId = id;
                continue;
            }

            if (supportModId.Length == 0)
            {
                supportModId = id;
                continue;
            }

            tertiaryModId = id;
            break;
        }
    }

    private int CountActiveBaseMinesFor(TowerInstance owner)
        => _activeMines.Count(m => ReferenceEquals(m.Owner, owner) && !m.IsMiniMine);

    private bool IsMineSpotFree(Vector2 worldPos, float minSpacing = Balance.RiftMinePlantSpacing)
        => _activeMines.All(m => m.Position.DistanceTo(worldPos) >= minSpacing);

    private EnemyInstance? FindTriggerTarget(Vector2 minePos, List<EnemyInstance> enemies)
    {
        EnemyInstance? best = null;
        float bestProgress = -1f;
        foreach (var e in enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.Hp <= 0f) continue;
            if (e.GlobalPosition.DistanceTo(minePos) > Balance.RiftMineTriggerRadius) continue;
            if (e.ProgressRatio > bestProgress)
            {
                bestProgress = e.ProgressRatio;
                best = e;
            }
        }
        return best;
    }

    private EnemyInstance? FindBlastTarget(Vector2 minePos, List<EnemyInstance> enemies, EnemyInstance? preferred)
    {
        if (preferred != null
            && GodotObject.IsInstanceValid(preferred)
            && preferred.Hp > 0f
            && preferred.GlobalPosition.DistanceTo(minePos) <= Balance.RiftMineBlastRadius)
        {
            return preferred;
        }

        EnemyInstance? best = null;
        float bestProgress = -1f;
        foreach (var e in enemies)
        {
            if (!GodotObject.IsInstanceValid(e) || e.Hp <= 0f) continue;
            if (e.GlobalPosition.DistanceTo(minePos) > Balance.RiftMineBlastRadius) continue;
            if (e.ProgressRatio > bestProgress)
            {
                bestProgress = e.ProgressRatio;
                best = e;
            }
        }
        return best;
    }

    private void DetonateMine(
        ActiveMine mine,
        EnemyInstance? preferredTarget,
        int waveIndex,
        List<EnemyInstance> enemies,
        float damage,
        int mineChainDepth,
        bool chainSource,
        int chainHop,
        bool forceFinalPop)
    {
        bool finalPop = forceFinalPop || mine.ChargesRemaining <= 1;
        if (finalPop)
        {
            if (!_activeMines.Remove(mine))
                return;
        }
        else if (!_activeMines.Contains(mine))
        {
            return;
        }

        var owner = mine.Owner;
        bool heavyPop = finalPop || damage >= owner.BaseDamage * 1.8f;
        bool chainPop = finalPop && (chainSource || mineChainDepth > 0 || chainHop > 0);
        SpawnMineBurst(mine.Position, owner.ProjectileColor, heavy: heavyPop, chainPop: chainPop, chainHop: chainHop);

        string mineSound = chainPop ? "mine_chain_pop" : "mine_pop";
        float pitch = chainPop
            ? Mathf.Clamp(1.02f + chainHop * 0.055f, 1.02f, 1.30f)
            : Mathf.Clamp(0.94f + mine.DamageScale * 0.15f, 0.92f, 1.10f);
        Sounds?.Play(mineSound, pitchScale: pitch);
        if (chainPop)
            Sounds?.DuckMusic(1.8f, 0.09f);

        var target = FindBlastTarget(mine.Position, enemies, preferredTarget);
        if (target != null)
        {
            float hpBefore = target.Hp;
            // isChain: true on non-final pops so Blast Core only fires on final-charge detonations,
            // not on charge-tick damage. Chain-source pops are always treated as chain regardless.
            var ctx = new DamageContext(owner, target, waveIndex, enemies, _state, isChain: chainSource || !finalPop, damageOverride: damage);
            DamageModel.Apply(ctx);
            float dealt = hpBefore - target.Hp;
            if (dealt > 0.01f)
            {
                bool isKill = target.Hp <= 0f;
                // Mine tick damage can be fractional after armor/resists; keep feedback visible.
                SpawnDamageNumber(target.GlobalPosition, Mathf.Max(1f, dealt), isKill, owner.TowerId, owner.ProjectileColor);
                if (!isKill && GodotObject.IsInstanceValid(target))
                    target.FlashHit();
                if (isKill)
                    GameController.Instance?.TriggerHitStop(realDuration: 0.040f, slowScale: 0.22f);
            }

            if (finalPop && owner.ChainCount > 0)
                ApplyMineEnemyChain(owner, target, waveIndex, enemies, damage * owner.ChainDamageDecay);
        }

        // Only base mines should seed split-shot mini mines (avoid recursive mini-mine loops).
        if (finalPop && owner.SplitCount > 0 && !mine.IsMiniMine)
        {
            // Mirror projectile Split Shot semantics: one copy = two extra spawns.
            int splitSeeds = owner.SplitCount + 1;
            int plantedMiniMines = 0;
            for (int i = 0; i < splitSeeds; i++)
            {
                if (TryPlantMineNear(owner, mine.Position, Balance.RiftMineMiniDamageFactor * mine.DamageScale))
                    plantedMiniMines++;
            }
            if (plantedMiniMines > 0)
                GameController.Instance?.RegisterSpectacleProc(owner, SpectacleDefinitions.SplitShot,
                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), damage);
        }

        if (finalPop && mineChainDepth > 0)
        {
            var chainMine = _activeMines
                .Where(m => ReferenceEquals(m.Owner, owner))
                .OrderBy(m => m.Position.DistanceTo(mine.Position))
                .FirstOrDefault(m => m.Position.DistanceTo(mine.Position) <= owner.ChainRange);

            if (chainMine != null)
            {
                if (chainMine.Visual != null && GodotObject.IsInstanceValid(chainMine.Visual))
                    chainMine.Visual.TriggerChainFlash(0.96f + chainHop * 0.06f);
                SpawnChainArc(
                    mine.Position,
                    chainMine.Position,
                    owner.ProjectileColor,
                    intensity: 1.10f + chainHop * 0.08f,
                    mineChainStyle: true);
                DetonateMine(
                    chainMine,
                    preferredTarget: null,
                    waveIndex,
                    enemies,
                    damage * owner.ChainDamageDecay,
                    mineChainDepth - 1,
                    chainSource: true,
                    chainHop: chainHop + 1,
                    forceFinalPop: true);
                GameController.Instance?.RegisterSpectacleProc(owner, SpectacleDefinitions.ChainReaction,
                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), damage);
            }
        }

        if (finalPop)
        {
            if (mine.Visual != null && GodotObject.IsInstanceValid(mine.Visual))
                mine.Visual.QueueFree();
        }
        else
        {
            mine.ChargesRemaining = Mathf.Max(0, mine.ChargesRemaining - 1);
            mine.RearmRemaining = Balance.RiftMineRetriggerDelay;
            if (mine.Visual != null && GodotObject.IsInstanceValid(mine.Visual))
            {
                mine.Visual.SetCharges(mine.ChargesRemaining, mine.MaxCharges);
                mine.Visual.SetArmed(false);
            }
        }
    }

    private void ApplyMineEnemyChain(
        TowerInstance tower,
        EnemyInstance primary,
        int waveIndex,
        List<EnemyInstance> enemies,
        float initialDamage)
    {
        if (tower.ChainCount <= 0) return;

        var alreadyHit = new HashSet<EnemyInstance> { primary };
        var current = primary;
        float damage = initialDamage;
        int bounces = 0;

        while (bounces < tower.ChainCount)
        {
            EnemyInstance? next = null;
            float bestDist = tower.ChainRange;

            foreach (var e in enemies)
            {
                if (!GodotObject.IsInstanceValid(e) || e.Hp <= 0f || alreadyHit.Contains(e))
                    continue;
                float d = current.GlobalPosition.DistanceTo(e.GlobalPosition);
                if (d < bestDist) { bestDist = d; next = e; }
            }

            if (next == null) break;

            float hpBefore = next.Hp;
            DamageModel.Apply(new DamageContext(tower, next, waveIndex, enemies, _state, isChain: true, damageOverride: damage));
            SpawnChainArc(
                current.GlobalPosition,
                next.GlobalPosition,
                tower.ProjectileColor,
                intensity: 1.05f + bounces * 0.06f,
                mineChainStyle: true);

            float dealt = hpBefore - next.Hp;
            if (dealt > 0.01f)
            {
                bool isKill = next.Hp <= 0f;
                SpawnDamageNumber(next.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, tower.ProjectileColor);
                if (!isKill && GodotObject.IsInstanceValid(next))
                    next.FlashHit();
            }

            alreadyHit.Add(next);
            current = next;
            damage *= tower.ChainDamageDecay;
            bounces++;
        }

        _state.TrackSpectacleChainDepth(bounces);

        if (bounces > 0)
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), initialDamage);
    }

    /// <summary>
    /// Computes a pitch multiplier for the shoot sound based on equipped modifiers.
    /// Each modifier contributes a factor that shifts tonal character to match its gameplay role.
    /// Stacking is multiplicative and clamped by Play() to 0.75–1.40.
    /// </summary>
    /// <summary>
    /// Compresses the Progress values of sorted (ascending) in-range enemies toward their median.
    /// Shrinks the spread by <see cref="Balance.AccordionCompressionFactor"/> while enforcing
    /// a minimum inter-enemy spacing of <see cref="Balance.AccordionMinSpacingPx"/> pixels.
    /// Safe to call in both bot mode and live mode: PathFollow2D.GlobalPosition updates automatically
    /// when Progress is assigned, exactly as the Reverse Walker already does.
    /// </summary>
    private static void CompressEnemyFormation(List<EnemyInstance> sortedAscending)
    {
        int count = sortedAscending.Count;
        if (count < 2) return;

        // Extract, compress via pure helper (testable without Godot), write back.
        // PathFollow2D.GlobalPosition updates automatically when Progress is assigned.
        float[] progress = new float[count];
        for (int i = 0; i < count; i++)
            progress[i] = sortedAscending[i].Progress;

        AccordionFormation.Compress(progress, Balance.AccordionCompressionFactor, Balance.AccordionMinSpacingPx);

        for (int i = 0; i < count; i++)
            sortedAscending[i].Progress = progress[i];
    }

    private void SpawnAccordionPulseVfx(Vector2 worldPos, Color color, float range, int enemyCount)
    {
        if (BotMode || LanePath == null) return;

        var parent = LanePath.GetParent();
        var vfx = new Entities.AccordionPulseVfx();
        parent.AddChild(vfx);
        vfx.GlobalPosition = worldPos;
        vfx.Initialize(color, range, enemyCount);
    }

    private static float ComputeShootPitch(ITowerView tower)
    {
        float pitch = 1f;
        foreach (var mod in tower.Modifiers)
        {
            pitch *= mod.ModifierId switch
            {
                "focus_lens"     => 0.78f,  // slow/heavy hits - deeper, weightier
                "hair_trigger"   => 1.22f,  // overclock - crisper, brighter
                "chain_reaction" => 0.88f,  // loaded payload - heavier onset
                "overreach"      => 0.92f,  // wide range - more body
                "overkill"       => 0.94f,  // spill burst - slightly heavier
                _                => 1.00f,
            };
        }
        return pitch;
    }

    private void SpawnMineBurst(Vector2 worldPos, Color color, bool heavy = false, bool chainPop = false, int chainHop = 0)
    {
        if (BotMode || LanePath == null) return;

        var parent = LanePath.GetParent();
        var burst = new RiftMineBurst();
        parent.AddChild(burst);
        burst.GlobalPosition = worldPos;
        float intensity = (heavy ? 1.02f : 0.92f) + Mathf.Clamp(chainHop, 0, 4) * 0.05f;
        burst.Initialize(color, chainPop, intensity);
    }

    private void SpawnChainArc(
        Vector2 worldFrom,
        Vector2 worldTo,
        Color color,
        float intensity = 1f,
        bool mineChainStyle = false)
    {
        if (BotMode || LanePath == null) return;
        var arc = new ChainArc();
        LanePath.GetParent().AddChild(arc);
        arc.GlobalPosition = Vector2.Zero;
        arc.Initialize(worldFrom, worldTo, color, intensity, mineChainStyle);
    }

    private void SpawnDamageNumber(Vector2 worldPos, float damage, bool isKill, string sourceTowerId, Color color,
                                    float scale = 1f)
    {
        if (BotMode || LanePath == null) return;
        var num = new DamageNumber();
        LanePath.GetParent().AddChild(num);
        num.GlobalPosition = worldPos + new Vector2(0f, -14f);
        num.Initialize(damage, color, isKill, sourceTowerId, scale);
    }

    private int ApplyPhaseSplitterSplitHits(
        ITowerView tower,
        EnemyInstance primary,
        int waveIndex,
        List<EnemyInstance> enemies,
        bool inBotMode,
        Color sourceColor)
    {
        if (tower.SplitCount <= 0)
            return 0;

        float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
        int splitBudget = tower.SplitCount; // phase splitter: one additional split per copy
        int spawned = 0;
        var candidates = enemies
            .Where(e => !ReferenceEquals(e, primary) && e.Hp > 0f && GodotObject.IsInstanceValid(e))
            .OrderBy(e => e.GlobalPosition.DistanceTo(primary.GlobalPosition));

        foreach (EnemyInstance candidate in candidates)
        {
            if (spawned >= splitBudget)
                break;
            if (candidate.GlobalPosition.DistanceTo(primary.GlobalPosition) > Balance.SplitShotRange)
                break;

            float hpBefore = candidate.Hp;
            DamageModel.Apply(new DamageContext(
                tower,
                candidate,
                waveIndex,
                enemies,
                _state,
                isChain: true,
                damageOverride: splitDamage));
            spawned++;

            if (!inBotMode)
            {
                float dealt = hpBefore - candidate.Hp;
                if (dealt > 0.01f)
                {
                    bool isKill = candidate.Hp <= 0f;
                    SpawnDamageNumber(candidate.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, sourceColor, 0.90f);
                    if (!isKill && GodotObject.IsInstanceValid(candidate))
                        candidate.FlashHit();
                }
            }
        }

        if (spawned > 0)
        {
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.SplitShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
        }

        return spawned;
    }

    private int ApplyChainImmediate(
        ITowerView tower,
        EnemyInstance primary,
        int waveIndex,
        List<EnemyInstance> enemies,
        Color sourceColor,
        float initialDamage)
    {
        if (tower.ChainCount <= 0)
            return 0;

        var alreadyHit = new HashSet<EnemyInstance> { primary };
        EnemyInstance current = primary;
        float damage = initialDamage;
        int bounces = 0;

        while (bounces < tower.ChainCount)
        {
            EnemyInstance? next = null;
            float bestDist = tower.ChainRange;

            foreach (EnemyInstance e in enemies)
            {
                if (!GodotObject.IsInstanceValid(e) || e.Hp <= 0f || alreadyHit.Contains(e))
                    continue;
                float d = current.GlobalPosition.DistanceTo(e.GlobalPosition);
                if (d < bestDist)
                {
                    bestDist = d;
                    next = e;
                }
            }

            if (next == null)
                break;

            float hpBefore = next.Hp;
            DamageModel.Apply(new DamageContext(
                tower,
                next,
                waveIndex,
                enemies,
                _state,
                isChain: true,
                damageOverride: damage));
            SpawnChainArc(current.GlobalPosition, next.GlobalPosition, sourceColor);

            float dealt = hpBefore - next.Hp;
            if (dealt > 0.01f)
            {
                bool isKill = next.Hp <= 0f;
                SpawnDamageNumber(next.GlobalPosition, Mathf.Max(1f, dealt), isKill, tower.TowerId, sourceColor);
                if (!isKill && GodotObject.IsInstanceValid(next))
                    next.FlashHit();
            }

            alreadyHit.Add(next);
            current = next;
            damage *= tower.ChainDamageDecay;
            bounces++;
        }

        _state.TrackSpectacleChainDepth(bounces);
        if (bounces > 0)
        {
            float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
        }

        return bounces;
    }

    private EnemyInstance? SelectRocketSplashTarget(ITowerView tower, List<EnemyInstance> enemies)
    {
        var inRange = enemies
            .Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f)
            .Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range)
            .ToList();
        if (inRange.Count == 0)
            return null;

        int blastCoreCopies = CountModifierCopies(tower, SpectacleDefinitions.BlastCore);
        float splashRadius = Balance.RocketLauncherSplashRadius
            + blastCoreCopies * Balance.RocketLauncherBlastCoreRadiusPerCopy;

        EnemyInstance best = inRange[0];
        int bestCluster = -1;

        foreach (EnemyInstance candidate in inRange)
        {
            int cluster = 0;
            foreach (EnemyInstance enemy in enemies)
            {
                if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f)
                    continue;
                if (candidate.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= splashRadius)
                    cluster++;
            }

            if (cluster > bestCluster)
            {
                best = candidate;
                bestCluster = cluster;
                continue;
            }

            if (cluster == bestCluster && PreferTargetByMode(candidate, best, tower.TargetingMode))
                best = candidate;
        }

        return best;
    }

    private EnemyInstance? SelectLatchNestTarget(ITowerView tower, List<EnemyInstance> enemies)
    {
        var inRange = enemies
            .Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f)
            .Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range)
            .ToList();
        if (inRange.Count == 0)
            return null;

        EnemyInstance? bestUnsaturated = null;
        EnemyInstance? bestFallback = null;
        foreach (EnemyInstance candidate in inRange)
        {
            if (bestFallback == null || PreferTargetByMode(candidate, bestFallback, tower.TargetingMode))
                bestFallback = candidate;

            int attached = _latchParasites.ActiveCountOnHost(tower, candidate);
            if (attached >= Balance.LatchNestMaxParasitesPerHost)
                continue;

            if (bestUnsaturated == null || PreferTargetByMode(candidate, bestUnsaturated, tower.TargetingMode))
                bestUnsaturated = candidate;
        }

        return bestUnsaturated ?? bestFallback;
    }

    private static bool PreferTargetByMode(EnemyInstance candidate, EnemyInstance current, TargetingMode mode)
    {
        switch (mode)
        {
            case TargetingMode.Strongest:
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                if (MathF.Abs(candidate.Progress - current.Progress) > 0.001f)
                    return candidate.Progress > current.Progress;
                break;
            case TargetingMode.LowestHp:
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp < current.Hp;
                if (MathF.Abs(candidate.Progress - current.Progress) > 0.001f)
                    return candidate.Progress > current.Progress;
                break;
            case TargetingMode.Last:
                if (MathF.Abs(candidate.Progress - current.Progress) > 0.001f)
                    return candidate.Progress < current.Progress;
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                break;
            default:
                if (MathF.Abs(candidate.Progress - current.Progress) > 0.001f)
                    return candidate.Progress > current.Progress;
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                break;
        }

        return candidate.GetInstanceId() < current.GetInstanceId();
    }

    private void SpawnPhaseSplitVfx(Vector2 source, Vector2? frontTargetPos, Vector2? backTargetPos, Color color)
    {
        if (BotMode || LanePath == null)
            return;

        if (!frontTargetPos.HasValue && !backTargetPos.HasValue)
            return;

        var vfx = new PhaseSplitVfx();
        LanePath.GetParent().AddChild(vfx);
        vfx.GlobalPosition = Vector2.Zero;
        vfx.Initialize(source, frontTargetPos, backTargetPos, color);
    }

    private void SpawnProjectile(Vector2 fromGlobal, EnemyInstance target, Color color,
                                 ITowerView tower, int waveIndex, List<EnemyInstance> enemies,
                                 Action<DamageContext, float, bool>? onPrimaryImpact = null)
    {
        if (BotMode)
        {
            float hpBefore = target.Hp;
            var ctx = new DamageContext(tower, target, waveIndex, enemies, _state);
            DamageModel.Apply(ctx);
            float dealt = Mathf.Max(0f, hpBefore - target.Hp);
            onPrimaryImpact?.Invoke(ctx, dealt, target.Hp <= 0f);
            if (tower.IsChainTower)
                ApplyChainBotMode(tower, target, waveIndex, enemies);
            if (tower.SplitCount > 0)
                ApplySplitBotMode(tower, target, waveIndex, enemies);
            return;
        }
        if (LanePath == null) return;
        var proj = new ProjectileVisual();
        LanePath.GetParent().AddChild(proj);
        float speed = tower.TowerId == "rocket_launcher"
            ? Balance.RocketLauncherProjectileSpeed
            : 500f;
        proj.Initialize(
            fromGlobal,
            target,
            color,
            speed,
            (TowerInstance)tower,
            waveIndex,
            enemies,
            _state,
            onPrimaryImpact: onPrimaryImpact);
    }

    private void AttachLatchParasiteIfPossible(ITowerView tower, EnemyInstance host)
    {
        if (tower.TowerId != "latch_nest" || host.Hp <= 0f)
            return;

        if (_latchParasites.TryAttach(
            tower,
            host,
            Balance.LatchNestParasiteDuration,
            Balance.LatchNestParasiteTickInterval,
            Balance.LatchNestMaxActiveParasitesPerTower,
            Balance.LatchNestMaxParasitesPerHost,
            out ulong parasiteId,
            out int hostSlot))
        {
            if (!BotMode && LanePath != null)
            {
                var vfx = new LatchParasiteVfx();
                LanePath.GetParent().AddChild(vfx);
                vfx.Initialize(parasiteId, host, hostSlot, UIStyle.TowerAccent("latch_nest", UIStyle.TowerAccentVariant.Projectile));
                _latchParasiteVisuals[parasiteId] = vfx;
            }
            Sounds?.Play("latch_attach", pitchScale: 0.97f + (float)(_mineRng.NextDouble() - 0.5) * 0.06f);
        }
    }

    private void OnLatchParasiteTick(LatchParasiteTickEvent<EnemyInstance> evt)
    {
        if (!BotMode && _latchParasiteVisuals.TryGetValue(evt.ParasiteId, out LatchParasiteVfx? vfx) && GodotObject.IsInstanceValid(vfx))
            vfx.NotifyTick();
        Sounds?.Play("latch_tick", pitchScale: 0.94f + (float)(_mineRng.NextDouble() - 0.5) * 0.08f);
    }

    private void OnLatchParasiteDetached(LatchParasiteDetachEvent<EnemyInstance> evt)
    {
        if (_latchParasiteVisuals.TryGetValue(evt.ParasiteId, out LatchParasiteVfx? vfx))
        {
            if (GodotObject.IsInstanceValid(vfx))
                vfx.NotifyDetached(evt.Reason == LatchParasiteDetachReason.HostDead);
            _latchParasiteVisuals.Remove(evt.ParasiteId);
        }

        if (evt.Reason == LatchParasiteDetachReason.HostDead)
            Sounds?.Play("latch_pop", pitchScale: 1.0f + (float)(_mineRng.NextDouble() - 0.5) * 0.10f);
    }

    private void PruneLatchParasitesForMissingTowers(RunState state)
    {
        var live = new List<ITowerView>();
        foreach (SlotInstance slot in state.Slots)
        {
            if (slot.Tower != null)
                live.Add(slot.Tower);
        }

        var activeTowers = new List<ITowerView>();
        _latchParasites.ForEachActive((_, tower, _, _, _) =>
        {
            foreach (ITowerView existing in activeTowers)
            {
                if (ReferenceEquals(existing, tower))
                    return;
            }
            activeTowers.Add(tower);
        });

        foreach (ITowerView tracked in activeTowers)
        {
            bool stillLive = false;
            foreach (ITowerView tower in live)
            {
                if (ReferenceEquals(tower, tracked))
                {
                    stillLive = true;
                    break;
                }
            }

            if (!stillLive)
                _latchParasites.RemoveByTower(tracked, OnLatchParasiteDetached);
        }
    }

    private void ApplyChainBotMode(ITowerView tower, EnemyInstance primary,
                                   int waveIndex, List<EnemyInstance> enemies)
    {
        int bounces = CombatResolution.ApplyChainHits(tower, primary, waveIndex, enemies, _state);

        _state.TrackSpectacleChainDepth(bounces);

        if (bounces > 0)
        {
            float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
        }
    }

    private void ApplySplitBotMode(ITowerView tower, EnemyInstance primary,
                                    int waveIndex, List<EnemyInstance> enemies)
    {
        int spawned = CombatResolution.ApplySplitHits(tower, primary, waveIndex, enemies, _state);

        if (spawned > 0)
        {
            float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.SplitShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
        }
    }

    private void SpawnDeathBurst(Vector2 worldPos, string typeId)
    {
        if (BotMode || LanePath == null) return;
        var burst = new DeathBurst();
        LanePath.GetParent().AddChild(burst);
        burst.GlobalPosition = worldPos;
        var (color, scale, style) = typeId switch
        {
            "armored_walker"  => (new Color(0.62f, 0.07f, 0.07f), 1.5f, DeathBurstStyle.Armored),
            "swift_walker"    => (new Color(0.60f, 1.00f, 0.10f), 0.75f, DeathBurstStyle.Swift),
            "reverse_walker"  => (new Color(0.36f, 0.92f, 1.00f), 0.96f, DeathBurstStyle.Swift),
            "splitter_walker" => (new Color(0.96f, 0.65f, 0.10f), 1.2f, DeathBurstStyle.Basic),
            "splitter_shard"  => (new Color(0.96f, 0.65f, 0.10f), 0.55f, DeathBurstStyle.Swift),
            "shield_drone"    => (new Color(0.30f, 0.76f, 1.00f), 0.90f, DeathBurstStyle.Swift),
            _                 => (new Color(0.95f, 0.22f, 0.12f), 1.0f, DeathBurstStyle.Basic),
        };
        burst.Initialize(color, scale, style);
    }

    /// <summary>
    /// Resets IsShieldProtected on all enemies, then marks any enemy within a live
    /// Shield Drone's aura radius as protected. Called once per Step(), before tower
    /// attacks, so damage reduction is correctly applied this frame.
    /// </summary>
    private static void UpdateShieldDroneAuras(List<EnemyInstance> enemies)
    {
        // Reset all protection flags first
        foreach (var e in enemies)
            e.IsShieldProtected = false;

        float radiusSq = Balance.ShieldDroneAuraRadius * Balance.ShieldDroneAuraRadius;

        foreach (var drone in enemies)
        {
            if (!GodotObject.IsInstanceValid(drone)) continue;
            if (drone.EnemyTypeId != "shield_drone" || drone.Hp <= 0f) continue;

            Vector2 dronePos = drone.GlobalPosition;
            foreach (var ally in enemies)
            {
                if (!GodotObject.IsInstanceValid(ally) || ally.Hp <= 0f) continue;
                if (ReferenceEquals(ally, drone)) continue;
                if (ally.EnemyTypeId == "shield_drone") continue; // drones don't shield each other

                if (dronePos.DistanceSquaredTo(ally.GlobalPosition) <= radiusSq)
                    ally.IsShieldProtected = true;
            }
        }
    }

    /// <summary>
    /// Spawns Balance.SplitterShardCount shards at the dead splitter's current path position.
    /// Shards are added directly to EnemiesAlive without touching EnemiesSpawnedThisWave,
    /// so the wave quota logic stays clean and wave-end waits for shards too.
    /// </summary>
    private void SpawnShards(RunState state, EnemyInstance parent)
    {
        if (EnemyScene == null || LanePath == null) return;

        float shardMandateMult = state.ActiveMandate?.Type == MandateType.EnemyHpBonus
            ? state.ActiveMandate.EnemyHpMultiplier : 1.0f;
        float shardHp = WaveSystem.GetScaledHp("splitter_shard", state.WaveIndex,
            SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
            state.EndlessWaveDepth, shardMandateMult);

        float[] shardOffsets = { -4f, -22f };
        for (int i = 0; i < Balance.SplitterShardCount; i++)
        {
            var shard = EnemyScene.Instantiate<EnemyInstance>();
            shard.Initialize("splitter_shard", shardHp, Balance.SplitterShardSpeed);
            LanePath.AddChild(shard);
            float offset = i < shardOffsets.Length ? shardOffsets[i] : -4f;
            shard.Progress = Mathf.Max(0f, parent.Progress + offset);
            if (BotMode)
                shard.SetProcess(false);
            state.EnemiesAlive.Add(shard);
            // EnemiesSpawnedThisWave intentionally not incremented - shards aren't queued enemies
        }
    }

    private void SpawnEnemy(RunState state, string typeId)
    {
        if (EnemyScene == null)
        {
            GD.PrintErr("CombatSim: EnemyScene is null - assign it on GameController in the Inspector.");
            return;
        }
        if (LanePath == null)
        {
            GD.PrintErr("CombatSim: LanePath is null - assign it on GameController in the Inspector.");
            return;
        }

        float mandateMult = state.ActiveMandate?.Type == MandateType.EnemyHpBonus
            ? state.ActiveMandate.EnemyHpMultiplier : 1.0f;
        float hp    = WaveSystem.GetScaledHp(typeId, state.WaveIndex,
                          SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
                          state.EndlessWaveDepth, mandateMult);
        float speed = ResolveEnemySpeed(typeId);

        var enemy = EnemyScene.Instantiate<EnemyInstance>();
        enemy.Initialize(typeId, hp, speed);
        LanePath.AddChild(enemy);
        if (BotMode)
        {
            enemy.SetProcess(false);
        }
        else
        {
            var finalScale = enemy.Scale;
            enemy.Scale = Vector2.Zero;
            enemy.CreateTween()
                 .TweenProperty(enemy, "scale", finalScale, 0.15f)
                 .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }

        state.EnemiesAlive.Add(enemy);
        state.EnemiesSpawnedThisWave++;
    }

    private static float ResolveEnemySpeed(string typeId) => typeId switch
    {
        "armored_walker"  => Balance.TankyEnemySpeed,
        "swift_walker"    => Balance.SwiftEnemySpeed,
        "splitter_walker" => Balance.SplitterSpeed,
        "splitter_shard"  => Balance.SplitterShardSpeed,
        "reverse_walker"  => Balance.ReverseWalkerSpeed,
        "shield_drone"    => Balance.ShieldDroneSpeed,
        _                 => Balance.BaseEnemySpeed,
    };
}
