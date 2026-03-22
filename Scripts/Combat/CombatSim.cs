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

    private readonly RunState _state;
    private float _spawnTimer = 0f;
    private readonly Queue<string> _spawnQueue = new();
    private float _initialSpawnDelay = 0f;
    private float _killComboTimer = 0f;
    private int _killComboCount = 0;
    private readonly List<ActiveMine> _activeMines = new();
    private readonly List<MineAnchor> _mineAnchors = new();
    private readonly Dictionary<ulong, int> _riftBurstFastPlantsUsed = new();
    private ulong _nextMineId = 1;
    private readonly Random _mineRng;

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
        _killComboTimer = 0f;
        _killComboCount = 0;
        _riftBurstFastPlantsUsed.Clear();
        ClearMines();
        RebuildMineAnchors();
    }

    /// <summary>Full reset + build spawn queue. Call after WaveSystem.LoadWave().</summary>
    public void ResetForWave(WaveSystem ws)
    {
        _spawnTimer = _initialSpawnDelay;
        _spawnQueue.Clear();
        _killComboTimer = 0f;
        _killComboCount = 0;
        _riftBurstFastPlantsUsed.Clear();
        ClearMines();
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
            SpawnEnemy(state, _spawnQueue.Dequeue());
            _spawnTimer = waveSystem.GetSpawnInterval();
        }

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
            return WaveResult.Loss;
        }

        ResolveMineTriggers(delta, state.WaveIndex, state.EnemiesAlive);

        // 3a. Update Shield Drone protection auras (must run before tower attacks so damage reduction is current).
        // Known ordering constraint: if a Shield Drone is killed mid-frame by an earlier tower slot,
        // enemies it was shielding retain IsShieldProtected=true for the rest of that frame.
        // The flag is cleared correctly at the start of the next Step(). One frame of ghost shielding
        // (~16ms at 60fps) is imperceptible and not worth re-running the O(n²) aura pass after each kill.
        UpdateShieldDroneAuras(state.EnemiesAlive);

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

            var target = Targeting.SelectTarget(tower, state.EnemiesAlive, ignoreRange: false);
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
            SpawnProjectile(tower.GlobalPosition, target, towerNode?.ProjectileColor ?? Godot.Colors.Yellow,
                            tower, state.WaveIndex, state.EnemiesAlive);
            if (!BotMode && towerNode != null)
            {
                towerNode.OnShotFired(target);
                towerNode.FlashAttack();
                float recoilPx = tower.TowerId == "heavy_cannon" ? 6.4f : 3.5f;
                towerNode.KickRecoil(recoilPx);
            }

            string shootId = tower.TowerId switch
            {
                "heavy_cannon"  => "shoot_heavy",
                "marker_tower"  => "shoot_marker",
                "chain_tower"   => "shoot_rapid",
                _               => "shoot_rapid",
            };
            // Chill Shot gives rapid-fire towers a softer, icier sound
            if (shootId == "shoot_rapid" && tower.Modifiers.Any(m => m.ModifierId == "slow"))
                shootId = "shoot_rapid_cold";
            Sounds?.Play(shootId, pitchScale: ComputeShootPitch(tower));
            if (tower.TowerId == "heavy_cannon")
                Sounds?.DuckMusic(1.9f, 0.11f);
        }

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
            visual = new RiftMineVisual
            {
                ZIndex = 9,
            };
            LanePath.GetParent().AddChild(visual);
            visual.GlobalPosition = worldPos;
            visual.Initialize(owner.ProjectileColor, damageScale, isMiniMine);
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
            {
                float splitScalar = SpectacleDefinitions.SplitShotEventScalar(plantedMiniMines);
                GameController.Instance?.RegisterSpectacleProc(owner, SpectacleDefinitions.SplitShot, splitScalar, damage);
            }
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
                GameController.Instance?.RegisterSpectacleProc(
                    owner,
                    SpectacleDefinitions.ChainReaction,
                    SpectacleDefinitions.ChainReactionEventScalar(1),
                    damage);
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
        {
            float chainScalar = SpectacleDefinitions.ChainReactionEventScalar(bounces);
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction, chainScalar, initialDamage);
        }
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

    private void SpawnDamageNumber(Vector2 worldPos, float damage, bool isKill, string sourceTowerId, Color color)
    {
        if (BotMode || LanePath == null) return;
        var num = new DamageNumber();
        LanePath.GetParent().AddChild(num);
        num.GlobalPosition = worldPos + new Vector2(0f, -14f);
        num.Initialize(damage, color, isKill, sourceTowerId);
    }

    private void SpawnProjectile(Vector2 fromGlobal, EnemyInstance target, Color color,
                                 ITowerView tower, int waveIndex, List<EnemyInstance> enemies)
    {
        if (BotMode)
        {
            DamageModel.Apply(new DamageContext(tower, target, waveIndex, enemies, _state));
            if (tower.IsChainTower)
                ApplyChainBotMode(tower, target, waveIndex, enemies);
            if (tower.SplitCount > 0)
                ApplySplitBotMode(tower, target, waveIndex, enemies);
            return;
        }
        if (LanePath == null) return;
        var proj = new ProjectileVisual();
        LanePath.GetParent().AddChild(proj);
        proj.Initialize(fromGlobal, target, color, speed: 500f, (TowerInstance)tower, waveIndex, enemies, _state);
    }

    private void ApplyChainBotMode(ITowerView tower, EnemyInstance primary,
                                   int waveIndex, List<EnemyInstance> enemies)
    {
        int bounces = CombatResolution.ApplyChainHits(tower, primary, waveIndex, enemies, _state);

        _state.TrackSpectacleChainDepth(bounces);

        if (bounces > 0)
        {
            float chainScalar = SpectacleDefinitions.ChainReactionEventScalar(bounces);
            float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction, chainScalar, chainEventDamage);
        }
    }

    private void ApplySplitBotMode(ITowerView tower, EnemyInstance primary,
                                    int waveIndex, List<EnemyInstance> enemies)
    {
        int spawned = CombatResolution.ApplySplitHits(tower, primary, waveIndex, enemies, _state);

        if (spawned > 0)
        {
            float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
            float splitScalar = SpectacleDefinitions.SplitShotEventScalar(spawned);
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.SplitShot, splitScalar, splitDamage);
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
        float speed = typeId switch
        {
            "armored_walker"  => Balance.TankyEnemySpeed,
            "swift_walker"    => Balance.SwiftEnemySpeed,
            "splitter_walker" => Balance.SplitterSpeed,
            "splitter_shard"  => Balance.SplitterShardSpeed,
            "reverse_walker"  => Balance.ReverseWalkerSpeed,
            "shield_drone"    => Balance.ShieldDroneSpeed,
            _                 => Balance.BaseEnemySpeed,
        };

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
}
