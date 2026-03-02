using System;
using SlotTheory.Data;

namespace SlotTheory.Core;

public class WaveSystem
{
    private WaveConfig? _current;

    public void LoadWave(int waveIndex, RunState state)
    {
        _current = DataLoader.GetWaveConfig(waveIndex);
        state.EnemiesSpawnedThisWave = 0;
        state.WaveTime = 0f;
    }

    public int GetWalkerCount()    => _current?.EnemyCount    ?? Balance.DefaultEnemyCount;
    public int GetTankyCount()     => _current?.TankyCount    ?? 0;
    public int GetSwiftCount()     => _current?.SwiftCount    ?? 0;
    public int GetTotalCount()     => GetWalkerCount() + GetTankyCount() + GetSwiftCount();
    public float GetSpawnInterval()  => _current?.SpawnInterval ?? Balance.DefaultSpawnInterval;
    public bool GetClumpArmored()    => _current?.ClumpArmored  ?? false;

    public static float GetScaledHp(string typeId, int waveIndex)
    {
        float baseHp = typeId switch
        {
            "armored_walker" => Balance.BaseEnemyHp * Balance.TankyHpMultiplier,
            "swift_walker"   => Balance.BaseEnemyHp * Balance.SwiftHpMultiplier,
            _                => Balance.BaseEnemyHp,
        };
        return baseHp * MathF.Pow(Balance.HpGrowthPerWave, waveIndex);
    }

    // Backward-compatible overload — assumes basic_walker
    public static float GetScaledHp(int waveIndex) => GetScaledHp("basic_walker", waveIndex);
}
