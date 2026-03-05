using System;
using System.Collections.Generic;
using System.Linq;
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
    private float _initialSpawnDelay = 0f;
    private float _killComboTimer = 0f;
    private int _killComboCount = 0;

    // Set externally by GameController when an enemy scene is needed
    public PackedScene? EnemyScene { get; set; }

    // Set externally — the Path2D node enemies are added to as PathFollow2D children
    public Path2D? LanePath { get; set; }

    public CombatSim(RunState state) => _state = state;

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
    }

    /// <summary>Full reset + build spawn queue. Call after WaveSystem.LoadWave().</summary>
    public void ResetForWave(WaveSystem ws)
    {
        _spawnTimer = _initialSpawnDelay;
        _spawnQueue.Clear();
        _killComboTimer = 0f;
        _killComboCount = 0;

        int walkers  = ws.GetWalkerCount();
        int tankies  = ws.GetTankyCount();
        int swifties = ws.GetSwiftCount();
        int total    = walkers + tankies + swifties;

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

        foreach (string t in slots) _spawnQueue.Enqueue(t);
    }

    public WaveResult Step(float delta, RunState state, WaveSystem waveSystem)
    {
        state.WaveTime += delta;
        state.TotalPlayTime += delta;  // Track total playtime across all waves
        _killComboTimer = Mathf.Max(0f, _killComboTimer - delta);

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
            int livesLost = e.EnemyTypeId == "armored_walker" ? 2 : 1;
            state.Lives -= livesLost;
            
            // Track leaks for post-wave micro reports and loss analysis
            state.TrackLeak(e.EnemyTypeId);
            
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

            var target = Targeting.SelectTarget(tower, state.EnemiesAlive, ignoreRange: false);
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
            if (!BotMode)
            {
                tower.OnShotFired(target);
                tower.FlashAttack();
                tower.KickRecoil();
            }

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
            string dieSound = dead.EnemyTypeId switch
            {
                "armored_walker" => "die_armored",
                "swift_walker"   => "die_swift",
                _                => "die_basic",
            };
            if (_killComboTimer <= 0f) _killComboCount = 0;
            float pitch = Mathf.Clamp(1.0f + _killComboCount * 0.05f, 1.0f, 1.15f);
            Sounds?.Play(dieSound, pitchScale: pitch);
            _killComboCount = Mathf.Min(_killComboCount + 1, 3);
            _killComboTimer = 0.24f;
            SpawnDeathBurst(dead.GlobalPosition, dead.EnemyTypeId);
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
        // Use proper range-based chaining even in bot mode for fairness
        var alreadyHit = new HashSet<EnemyInstance> { primary };
        float damage = tower.BaseDamage * tower.ChainDamageDecay;
        int bounces = 0;
        var currentTarget = primary;

        while (bounces < tower.ChainCount)
        {
            // Find nearest enemy within chain range that hasn't been hit
            EnemyInstance? nextTarget = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var e in enemies)
            {
                if (alreadyHit.Contains(e) || e.Hp <= 0) continue;
                
                // Use progress-based distance approximation since GlobalPosition may be unreliable
                // This approximates spatial distance using position along the path
                float distance = Math.Abs(e.ProgressRatio - currentTarget.ProgressRatio);
                if (distance < nearestDistance && distance <= 0.3f) // ~30% of path as max chain range
                {
                    nearestDistance = distance;
                    nextTarget = e;
                }
            }
            
            if (nextTarget == null) break;
            
            DamageModel.Apply(new DamageContext(tower, nextTarget, waveIndex, enemies, _state,
                                                isChain: true, damageOverride: damage));
            alreadyHit.Add(nextTarget);
            currentTarget = nextTarget;
            damage *= tower.ChainDamageDecay;
            bounces++;
        }
    }

    private void ApplySplitBotMode(TowerInstance tower, EnemyInstance primary,
                                    int waveIndex, List<EnemyInstance> enemies)
    {
        // Use proper range-based splitting for fairness
        float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
        var targetsInRange = new List<EnemyInstance>();
        
        // Find all enemies within reasonable range of the primary target
        foreach (var e in enemies)
        {
            if (e == primary || e.Hp <= 0) continue;
            
            // Use progress-based distance approximation
            float distance = Math.Abs(e.ProgressRatio - primary.ProgressRatio);
            if (distance <= 0.25f) // ~25% of path as split range
            {
                targetsInRange.Add(e);
            }
        }
        
        // Apply split damage to up to SplitCount targets, prioritizing by targeting mode
        var sortedTargets = tower.TargetingMode switch
        {
            TargetingMode.First => targetsInRange.OrderByDescending(e => e.ProgressRatio).ToList(),
            TargetingMode.Strongest => targetsInRange.OrderByDescending(e => e.Hp).ToList(),
            TargetingMode.LowestHp => targetsInRange.OrderBy(e => e.Hp).ToList(),
            _ => targetsInRange
        };
        
        int hitCount = 0;
        foreach (var target in sortedTargets)
        {
            if (hitCount >= tower.SplitCount) break;
            DamageModel.Apply(new DamageContext(tower, target, waveIndex, enemies, _state,
                                                isChain: true, damageOverride: splitDamage));
            hitCount++;
        }
    }

    private void SpawnDeathBurst(Vector2 worldPos, string typeId)
    {
        if (BotMode || LanePath == null) return;
        var burst = new DeathBurst();
        LanePath.GetParent().AddChild(burst);
        burst.GlobalPosition = worldPos;
        var (color, scale) = typeId switch
        {
            "armored_walker" => (new Color(0.62f, 0.07f, 0.07f), 1.5f),
            "swift_walker"   => (new Color(0.60f, 1.00f, 0.10f), 0.75f),
            _                => (new Color(0.95f, 0.22f, 0.12f), 1.0f),
        };
        burst.Initialize(color, scale);
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
        float speed = typeId switch
        {
            "armored_walker" => Balance.TankyEnemySpeed,
            "swift_walker"   => Balance.SwiftEnemySpeed,
            _                => Balance.BaseEnemySpeed,
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
