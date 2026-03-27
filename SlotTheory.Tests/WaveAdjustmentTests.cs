using System.Collections.Generic;
using SlotTheory.Core;
using SlotTheory.Data;
using Xunit;

namespace SlotTheory.Tests;

public class WaveAdjustmentTests
{
    [Fact]
    public void ApplyWaveAdjustmentsForTesting_AppliesMatchingEntry()
    {
        var baseWave = new WaveConfig(
            EnemyCount: 20,
            SpawnInterval: 1.30f,
            TankyCount: 2,
            ClumpArmored: false,
            SwiftCount: 1,
            SplitterCount: 0,
            ReverseCount: 0,
            ShieldDroneCount: 0);

        var entries = new[]
        {
            new WaveAdjustmentEntry(
                MapId: "trident",
                Difficulty: "Hard",
                Wave: 17,
                TankyDelta: -1,
                SwiftDelta: 1)
        };

        var adjusted = DataLoader.ApplyWaveAdjustmentsForTesting(
            waveIndex: 16,
            wave: baseWave,
            mapId: "trident",
            difficulty: DifficultyMode.Hard,
            entries: entries,
            maxWaveCount: 20);

        Assert.Equal(1, adjusted.TankyCount);
        Assert.Equal(2, adjusted.SwiftCount);
    }

    [Fact]
    public void ApplyWaveAdjustmentsForTesting_IgnoresNonMatchingEntry()
    {
        var baseWave = new WaveConfig(EnemyCount: 20, SpawnInterval: 1.30f, TankyCount: 2, SwiftCount: 1);
        var entries = new[]
        {
            new WaveAdjustmentEntry(
                MapId: "trident",
                Difficulty: "Hard",
                Wave: 17,
                TankyDelta: -1)
        };

        var adjusted = DataLoader.ApplyWaveAdjustmentsForTesting(
            waveIndex: 16,
            wave: baseWave,
            mapId: "crossroads",
            difficulty: DifficultyMode.Hard,
            entries: entries,
            maxWaveCount: 20);

        Assert.Equal(baseWave, adjusted);
    }

    [Fact]
    public void ApplyWaveAdjustmentsForTesting_SupportsWildcardsAndAllWaves()
    {
        var baseWave = new WaveConfig(EnemyCount: 12, SpawnInterval: 1.90f, TankyCount: 0, SwiftCount: 0);
        var entries = new[]
        {
            new WaveAdjustmentEntry(
                MapId: "*",
                Difficulty: "*",
                Wave: null,
                TankyDelta: 1)
        };

        var adjusted = DataLoader.ApplyWaveAdjustmentsForTesting(
            waveIndex: 3,
            wave: baseWave,
            mapId: "crossroads",
            difficulty: DifficultyMode.Easy,
            entries: entries,
            maxWaveCount: 20);

        Assert.Equal(1, adjusted.TankyCount);
    }

    [Fact]
    public void ApplyWaveAdjustmentsForTesting_ClampsNegativeCountsAndSpawnInterval()
    {
        var baseWave = new WaveConfig(EnemyCount: 1, SpawnInterval: 1.0f, TankyCount: 0, SwiftCount: 0);
        var entries = new[]
        {
            new WaveAdjustmentEntry(
                MapId: "trident",
                Difficulty: "*",
                Wave: 17,
                EnemyCountDelta: -5,
                TankyDelta: -2,
                SpawnIntervalDelta: -2f)
        };

        var adjusted = DataLoader.ApplyWaveAdjustmentsForTesting(
            waveIndex: 16,
            wave: baseWave,
            mapId: "trident",
            difficulty: DifficultyMode.Normal,
            entries: entries,
            maxWaveCount: 20);

        Assert.Equal(0, adjusted.EnemyCount);
        Assert.Equal(0, adjusted.TankyCount);
        Assert.Equal(0.85f, adjusted.SpawnInterval, precision: 3);
    }

    [Fact]
    public void ApplyWaveAdjustmentsForTesting_IgnoresInvalidEntries()
    {
        var baseWave = new WaveConfig(EnemyCount: 20, SpawnInterval: 1.30f, TankyCount: 2, SwiftCount: 1);
        var entries = new List<WaveAdjustmentEntry>
        {
            new(
                MapId: "trident",
                Difficulty: "Nightmare",
                Wave: 17,
                TankyDelta: -1),
            new(
                MapId: "trident",
                Difficulty: "Hard",
                Wave: 17,
                TankyDelta: -1)
        };

        var adjusted = DataLoader.ApplyWaveAdjustmentsForTesting(
            waveIndex: 16,
            wave: baseWave,
            mapId: "trident",
            difficulty: DifficultyMode.Hard,
            entries: entries,
            maxWaveCount: 20);

        Assert.Equal(1, adjusted.TankyCount);
    }
}
