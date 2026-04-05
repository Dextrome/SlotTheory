using SlotTheory.Core;
using SlotTheory.Data;
using Xunit;

namespace SlotTheory.Tests;

public sealed class MapEnemyProfileTests
{
    [Fact]
    public void ApplyMapEnemyProfileForTesting_ConvertsBasicIntoSpecials_WhenShareRequiresIt()
    {
        var baseWave = new WaveConfig(
            EnemyCount: 20,
            SpawnInterval: 1.5f);

        var profiles = new[]
        {
            new MapEnemyProfileEntry(
                MapId: "profile_map",
                Enemies: new[]
                {
                    new MapEnemyProfileEnemy(EnemyId: EnemyCatalog.SwiftWalkerId, Weight: 1f)
                },
                PackageBlend: 1f,
                OffProfileRetention: 0f,
                DefaultSpecialShare: 0.25f)
        };

        var profiled = DataLoader.ApplyMapEnemyProfileForTesting(
            waveIndex: 0,
            wave: baseWave,
            mapId: "profile_map",
            difficulty: DifficultyMode.Easy,
            profiles: profiles);

        Assert.Equal(15, profiled.EnemyCount);
        Assert.Equal(5, profiled.SwiftCount);
    }

    [Fact]
    public void ApplyMapEnemyProfileForTesting_SuppressesOffProfileTypes_WhenBlendIsHigh()
    {
        var baseWave = new WaveConfig(
            EnemyCount: 18,
            SpawnInterval: 1.4f,
            TankyCount: 2,
            SwiftCount: 1,
            ReverseCount: 1,
            ShieldDroneCount: 1);

        var profiles = new[]
        {
            new MapEnemyProfileEntry(
                MapId: "ridge_map",
                Enemies: new[]
                {
                    new MapEnemyProfileEnemy(EnemyId: EnemyCatalog.ArmoredWalkerId, Weight: 1f),
                    new MapEnemyProfileEnemy(EnemyId: EnemyCatalog.AnchorWalkerId, Weight: 1f),
                },
                PackageBlend: 1f,
                OffProfileRetention: 0f,
                DefaultSpecialShare: -1f)
        };

        var profiled = DataLoader.ApplyMapEnemyProfileForTesting(
            waveIndex: 10,
            wave: baseWave,
            mapId: "ridge_map",
            difficulty: DifficultyMode.Easy,
            profiles: profiles);

        Assert.Equal(0, profiled.SwiftCount);
        Assert.Equal(0, profiled.ReverseCount);
        Assert.Equal(0, profiled.ShieldDroneCount);
        Assert.Equal(5, profiled.TankyCount + profiled.AnchorCount);
    }

    [Fact]
    public void ApplyDemoEnemySuppressionForTesting_ZeroesFullGameEnemyCounts()
    {
        var wave = new WaveConfig(
            EnemyCount: 12,
            SpawnInterval: 1.5f,
            TankyCount: 1,
            ReverseCount: 2,
            ShieldDroneCount: 1,
            AnchorCount: 2,
            NullDroneCount: 2,
            LancerCount: 2,
            VeilCount: 1);

        var suppressed = DataLoader.ApplyDemoEnemySuppressionForTesting(wave, isDemo: true);

        Assert.Equal(1, suppressed.TankyCount);
        Assert.Equal(0, suppressed.ReverseCount);
        Assert.Equal(0, suppressed.ShieldDroneCount);
        Assert.Equal(0, suppressed.AnchorCount);
        Assert.Equal(0, suppressed.NullDroneCount);
        Assert.Equal(0, suppressed.LancerCount);
        Assert.Equal(0, suppressed.VeilCount);
    }
}
