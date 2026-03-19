using System;
using SlotTheory.Data;

namespace SlotTheory.Core;

public class WaveSystem
{
    private WaveConfig? _current;

    public void LoadWave(int waveIndex, RunState state)
    {
        var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;
        _current = waveIndex >= Balance.TotalWaves
            ? BuildEndlessWaveConfig(state.EndlessWaveDepth, difficulty)
            : DataLoader.GetWaveConfig(waveIndex, difficulty, state.SelectedMapId);
        state.EnemiesSpawnedThisWave = 0;
        state.WaveTime = 0f;
    }

    private static WaveConfig BuildEndlessWaveConfig(int depth, DifficultyMode difficulty)
    {
        // Wave-20 baseline (mirrors waves.json index 19 before difficulty scaling)
        const int   baseCount    = 36;
        const int   baseTanky   = 4;
        const int   baseReverse = 1;
        const float baseInterval = 1.25f;

        float countMult   = MathF.Pow(1f + Balance.EndlessEnemyCountScalePerWave, depth);
        int   enemyCount  = (int)MathF.Ceiling(baseCount    * Balance.GetEnemyCountMultiplier(difficulty) * countMult);
        int   tankyCount  = (int)MathF.Ceiling(baseTanky   * Balance.GetEnemyCountMultiplier(difficulty) * countMult);
        int   swiftBonus  = depth / Balance.EndlessSwiftBonusInterval;  // +1 per 5 waves
        int   reverseCount = Balance.IsDemo
            ? 0
            : (int)MathF.Ceiling(baseReverse
                * Balance.GetEnemyCountMultiplier(difficulty)
                * Balance.GetReverseCountMultiplier(difficulty)
                * (1f + (depth / (float)Balance.EndlessReverseBonusInterval) * 0.45f));
        float spawnInterval = MathF.Max(
            baseInterval * Balance.GetSpawnIntervalMultiplier(difficulty)
                / MathF.Pow(1f + Balance.EndlessEnemyCountScalePerWave * 0.5f, depth),
            Balance.EndlessSpawnIntervalFloor);

        return new WaveConfig(
            EnemyCount: enemyCount,
            SpawnInterval: spawnInterval,
            TankyCount: tankyCount,
            ClumpArmored: false,
            SwiftCount: swiftBonus,
            SplitterCount: 0,
            ReverseCount: reverseCount);
    }

    public int GetWalkerCount()    => _current?.EnemyCount    ?? Balance.DefaultEnemyCount;
    public int GetTankyCount()     => _current?.TankyCount    ?? 0;
    public int GetSwiftCount()     => _current?.SwiftCount    ?? 0;
    public int GetSplitterCount()  => _current?.SplitterCount ?? 0;
    public int GetReverseCount()   => _current?.ReverseCount  ?? 0;
    public int GetTotalCount()     => GetWalkerCount() + GetTankyCount() + GetSwiftCount() + GetSplitterCount() + GetReverseCount();
    public float GetSpawnInterval()  => _current?.SpawnInterval ?? Balance.DefaultSpawnInterval;
    public bool GetClumpArmored()    => _current?.ClumpArmored  ?? false;

    public static float GetScaledHp(string typeId, int waveIndex)
    {
        var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;
        return GetScaledHp(typeId, waveIndex, difficulty);
    }

    /// <summary>Deterministic overload for unit tests - pass difficulty explicitly.</summary>
    public static float GetScaledHp(string typeId, int waveIndex, DifficultyMode difficulty,
        int endlessDepth = 0, float mandateHpMultiplier = 1.0f)
    {
        float baseHp = typeId switch
        {
            "armored_walker"  => Balance.BaseEnemyHp * Balance.TankyHpMultiplier,
            "swift_walker"    => Balance.BaseEnemyHp * Balance.SwiftHpMultiplier,
            "splitter_walker" => Balance.BaseEnemyHp * Balance.SplitterHpMultiplier,
            "splitter_shard"  => Balance.BaseEnemyHp * Balance.SplitterShardHpMultiplier,
            "reverse_walker"  => Balance.BaseEnemyHp * Balance.ReverseWalkerHpMultiplier,
            _                 => Balance.BaseEnemyHp,
        };
        float scaledHp = baseHp * MathF.Pow(Balance.HpGrowthPerWave, waveIndex);
        float hp = scaledHp * Balance.GetEnemyHpMultiplier(difficulty);
        if (endlessDepth > 0)
            hp *= MathF.Pow(1f + Balance.EndlessEnemyHpScalePerWave, endlessDepth);
        if (mandateHpMultiplier != 1.0f)
            hp *= mandateHpMultiplier;
        return hp;
    }

    // Backward-compatible overload - assumes basic_walker
    public static float GetScaledHp(int waveIndex) => GetScaledHp("basic_walker", waveIndex);
}
