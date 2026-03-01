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

    public int GetEnemyCount() => _current?.EnemyCount ?? Balance.DefaultEnemyCount;
    public float GetSpawnInterval() => _current?.SpawnInterval ?? Balance.DefaultSpawnInterval;

    public static float GetScaledHp(int waveIndex) =>
        Balance.BaseEnemyHp * MathF.Pow(Balance.HpGrowthPerWave, waveIndex);
}
