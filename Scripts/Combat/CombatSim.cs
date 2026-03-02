using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public enum WaveResult { Ongoing, WaveComplete, Loss }

/// <summary>
/// Orchestrates wave execution each frame. Called by GameController._Process().
/// Enemy movement is self-handled by EnemyInstance._Process() via PathFollow2D.
/// </summary>
public class CombatSim
{
    private readonly RunState _state;
    private float _spawnTimer = 0f;
    private readonly Queue<string> _spawnQueue = new();

    // Set externally by GameController when an enemy scene is needed
    public PackedScene? EnemyScene { get; set; }

    // Set externally — the Path2D node enemies are added to as PathFollow2D children
    public Path2D? LanePath { get; set; }

    public CombatSim(RunState state) => _state = state;

    // Injected by GameController after scene is ready
    public SoundManager? Sounds { get; set; }

    /// <summary>When true: damage is instant, no visuals spawned, enemies don't self-move.</summary>
    public bool BotMode { get; set; }

    /// <summary>Lightweight reset used on run restart (before wave is loaded).</summary>
    public void ResetForWave()
    {
        _spawnTimer = 0f;
        _spawnQueue.Clear();
    }

    /// <summary>Full reset + build spawn queue. Call after WaveSystem.LoadWave().</summary>
    public void ResetForWave(WaveSystem ws)
    {
        _spawnTimer = 0f;
        _spawnQueue.Clear();

        int walkers = ws.GetWalkerCount();
        int tankies = ws.GetTankyCount();
        int total   = walkers + tankies;

        if (ws.GetClumpArmored() && tankies > 0)
        {
            // Group all armored enemies into one block, starting at the 1/3 mark.
            // Creates a panic spike: warm-up basics → armored wall → cleanup basics.
            int blockStart = total / 3;
            for (int i = 0; i < total; i++)
                _spawnQueue.Enqueue(i >= blockStart && i < blockStart + tankies ? "armored_walker" : "basic_walker");
        }
        else
        {
            // Spread tankies evenly across the wave (default)
            var tankySlots = new HashSet<int>();
            if (tankies > 0)
                for (int t = 0; t < tankies; t++)
                    tankySlots.Add((int)Math.Round((t + 0.5) * total / tankies));

            for (int i = 0; i < total; i++)
                _spawnQueue.Enqueue(tankySlots.Contains(i) ? "armored_walker" : "basic_walker");
        }
    }

    public WaveResult Step(float delta, RunState state, WaveSystem waveSystem)
    {
        state.WaveTime += delta;

        // 1. Spawn
        _spawnTimer -= delta;
        int quota = waveSystem.GetTotalCount();
        if (_spawnTimer <= 0f && state.EnemiesSpawnedThisWave < quota && _spawnQueue.Count > 0)
        {
            SpawnEnemy(state, _spawnQueue.Dequeue());
            _spawnTimer = waveSystem.GetSpawnInterval();
        }

        // 2. Leaked enemies — each one costs a life; return Loss when lives run out
        var leaked = state.EnemiesAlive.FindAll(e => e.ProgressRatio >= 1.0f);
        foreach (var e in leaked)
        {
            state.Lives -= e.EnemyTypeId == "armored_walker" ? 2 : 1;
            Sounds?.Play("leak");
            e.QueueFree();
        }
        state.EnemiesAlive.RemoveAll(e => !GodotObject.IsInstanceValid(e) || e.ProgressRatio >= 1.0f);
        if (state.Lives <= 0)
            return WaveResult.Loss;

        // 3. Tower attacks (hitscan — no projectiles)
        foreach (var slot in state.Slots)
        {
            if (slot.Tower == null) continue;
            var tower = slot.Tower;

            tower.Cooldown -= delta;
            if (tower.Cooldown > 0f) continue;

            var target = Targeting.SelectTarget(tower, state.EnemiesAlive, ignoreRange: BotMode);
            if (target == null) continue;

            if (!BotMode) tower.LastTargetPosition = target.GlobalPosition;

            // Effective interval: base × modifier multipliers (e.g. FocusLens ×2)
            float effectiveInterval = tower.AttackInterval;
            foreach (var mod in tower.Modifiers)
                mod.ModifyAttackInterval(ref effectiveInterval, tower);
            tower.Cooldown = effectiveInterval;

            // Damage applied on projectile arrival, not here
            SpawnProjectile(tower.GlobalPosition, target, tower.ProjectileColor,
                            tower, state.WaveIndex, state.EnemiesAlive);
            if (!BotMode) tower.FlashAttack();

            string shootId = tower.TowerId switch
            {
                "heavy_cannon"  => "shoot_heavy",
                "marker_tower"  => "shoot_marker",
                "chain_tower"   => "shoot_rapid",
                _               => "shoot_rapid",
            };
            Sounds?.Play(shootId);
        }

        // 4. Remove dead enemies
        foreach (var dead in state.EnemiesAlive.FindAll(e => e.Hp <= 0))
        {
            bool isArmored = dead.EnemyTypeId == "armored_walker";
            Sounds?.Play(isArmored ? "die_armored" : "die_basic");
            SpawnDeathBurst(dead.GlobalPosition, isArmored);
            dead.QueueFree();
        }
        state.EnemiesAlive.RemoveAll(e => e.Hp <= 0 || !GodotObject.IsInstanceValid(e));

        // 5. Wave complete
        bool quotaDone = state.EnemiesSpawnedThisWave >= quota;
        if (quotaDone && state.EnemiesAlive.Count == 0)
            return WaveResult.WaveComplete;

        return WaveResult.Ongoing;
    }

    private void SpawnProjectile(Vector2 fromGlobal, EnemyInstance target, Color color,
                                 TowerInstance tower, int waveIndex, List<EnemyInstance> enemies)
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
        proj.Initialize(fromGlobal, target, color, speed: 500f, tower, waveIndex, enemies, _state);
    }

    private void ApplyChainBotMode(TowerInstance tower, EnemyInstance primary,
                                   int waveIndex, List<EnemyInstance> enemies)
    {
        // In bot mode GlobalPositions are unreliable; chain to the next N alive enemies
        // in list order as an approximation of "nearby targets".
        var alreadyHit = new HashSet<EnemyInstance> { primary };
        float damage   = tower.BaseDamage * tower.ChainDamageDecay;
        int   bounces  = 0;

        foreach (var e in enemies)
        {
            if (bounces >= tower.ChainCount) break;
            if (alreadyHit.Contains(e) || e.Hp <= 0) continue;
            DamageModel.Apply(new DamageContext(tower, e, waveIndex, enemies, _state,
                                                isChain: true, damageOverride: damage));
            alreadyHit.Add(e);
            damage  *= tower.ChainDamageDecay;
            bounces++;
        }
    }

    private void ApplySplitBotMode(TowerInstance tower, EnemyInstance primary,
                                    int waveIndex, List<EnemyInstance> enemies)
    {
        // GlobalPositions are unreliable in bot mode; approximate by hitting
        // the next SplitCount alive enemies in list order.
        float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
        int spawned = 0;

        foreach (var e in enemies)
        {
            if (spawned >= tower.SplitCount + 1) break;
            if (e == primary || e.Hp <= 0) continue;
            DamageModel.Apply(new DamageContext(tower, e, waveIndex, enemies, _state,
                                                isChain: true, damageOverride: splitDamage));
            spawned++;
        }
    }

    private void SpawnDeathBurst(Vector2 worldPos, bool isArmored)
    {
        if (BotMode || LanePath == null) return;
        var burst = new DeathBurst();
        LanePath.GetParent().AddChild(burst);
        burst.GlobalPosition = worldPos;
        burst.Initialize(
            isArmored ? new Color(0.62f, 0.07f, 0.07f) : new Color(0.95f, 0.22f, 0.12f),
            isArmored ? 1.5f : 1.0f);
    }

    private void SpawnEnemy(RunState state, string typeId)
    {
        if (EnemyScene == null)
        {
            GD.PrintErr("CombatSim: EnemyScene is null — assign it on GameController in the Inspector.");
            return;
        }
        if (LanePath == null)
        {
            GD.PrintErr("CombatSim: LanePath is null — assign it on GameController in the Inspector.");
            return;
        }

        float hp    = WaveSystem.GetScaledHp(typeId, state.WaveIndex);
        float speed = typeId == "armored_walker" ? Balance.TankyEnemySpeed : Balance.BaseEnemySpeed;

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
