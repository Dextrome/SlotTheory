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

    // Set externally by GameController when an enemy scene is needed
    public PackedScene? EnemyScene { get; set; }

    // Set externally — the Path2D node enemies are added to as PathFollow2D children
    public Path2D? LanePath { get; set; }

    public CombatSim(RunState state) => _state = state;

    public WaveResult Step(float delta, RunState state, WaveSystem waveSystem)
    {
        state.WaveTime += delta;

        // 1. Spawn
        _spawnTimer -= delta;
        int quota = waveSystem.GetEnemyCount();
        if (_spawnTimer <= 0f && state.EnemiesSpawnedThisWave < quota)
        {
            SpawnEnemy(state);
            _spawnTimer = waveSystem.GetSpawnInterval();
        }

        // 2. Loss check — enemy reached end of path (ProgressRatio >= 1)
        if (state.EnemiesAlive.Exists(e => e.ProgressRatio >= 1.0f))
            return WaveResult.Loss;

        // 3. Tower attacks (hitscan — no projectiles)
        foreach (var slot in state.Slots)
        {
            if (slot.Tower == null) continue;
            var tower = slot.Tower;

            tower.Cooldown -= delta;
            if (tower.Cooldown > 0f) continue;

            var target = Targeting.SelectTarget(tower, state.EnemiesAlive);
            if (target == null) continue;

            var ctx = new DamageContext(tower, target, state.WaveIndex, state.EnemiesAlive);
            DamageModel.Apply(ctx);
            tower.Cooldown = tower.AttackInterval;
        }

        // 4. Remove dead enemies
        foreach (var dead in state.EnemiesAlive.FindAll(e => e.Hp <= 0))
        {
            dead.QueueFree();
        }
        state.EnemiesAlive.RemoveAll(e => e.Hp <= 0 || !GodotObject.IsInstanceValid(e));

        // 5. Wave complete
        bool quotaDone = state.EnemiesSpawnedThisWave >= quota;
        if (quotaDone && state.EnemiesAlive.Count == 0)
            return WaveResult.WaveComplete;

        return WaveResult.Ongoing;
    }

    private void SpawnEnemy(RunState state)
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

        var enemy = EnemyScene.Instantiate<EnemyInstance>();
        enemy.Initialize("basic_walker", WaveSystem.GetScaledHp(state.WaveIndex), Balance.BaseEnemySpeed);
        LanePath.AddChild(enemy);

        state.EnemiesAlive.Add(enemy);
        state.EnemiesSpawnedThisWave++;
    }
}
