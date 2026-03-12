using SlotTheory.Core;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class SpectacleTuningTests
{
    [Fact]
    public void OverkillBloomTuningMultiplier_AffectsDamage()
    {
        SpectacleTuning.Reset();
        OverkillBloomProfile baseline = SpectacleExplosionCore.BuildOverkillBloomProfile(140f);

        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            OverkillBloomDamageScaleMultiplier = 1.25f,
        }, "test");
        OverkillBloomProfile tuned = SpectacleExplosionCore.BuildOverkillBloomProfile(140f);

        Assert.True(tuned.BloomDamage > baseline.BloomDamage);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void DetonationTargetCapTuning_AffectsResolvedCap()
    {
        SpectacleTuning.Reset();
        int baseline = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge: false, reducedMotion: false);

        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            DetonationMaxTargetsMultiplier = 0.75f,
        }, "test");
        int tuned = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge: false, reducedMotion: false);

        Assert.True(tuned < baseline);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void DisableOverkillBloom_PreventsBloomTrigger()
    {
        SpectacleTuning.Reset();
        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            EnableOverkillBloom = false,
        }, "test");

        OverkillBloomProfile profile = SpectacleExplosionCore.BuildOverkillBloomProfile(220f);
        Assert.False(profile.ShouldTrigger);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void DisableStatusDetonation_ResolvesZeroMaxTargets()
    {
        SpectacleTuning.Reset();
        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            EnableStatusDetonation = false,
        }, "test");

        int maxTargets = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge: false, reducedMotion: false);
        Assert.Equal(0, maxTargets);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void DisableResidue_StopsResidueProfilesFromSpawning()
    {
        SpectacleTuning.Reset();
        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            EnableResidue = false,
        }, "test");

        ExplosionResidueProfile profile = SpectacleExplosionCore.ResolveResidueProfile(
            ComboExplosionSkin.ChillShatter,
            globalSurge: false,
            surgePower: 1.2f,
            chainIndex: 0);
        Assert.False(profile.ShouldSpawn);
        Assert.Equal(ExplosionResidueKind.None, profile.Kind);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void Reset_RestoresBaselineBehavior()
    {
        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            MeterGainMultiplier = 1.40f,
        }, "test");
        float boosted = SpectacleDefinitions.ResolveMeterGainScale();
        SpectacleTuning.Reset();
        float baseline = SpectacleDefinitions.ResolveMeterGainScale();

        Assert.True(boosted > baseline);
        Assert.Equal("baseline", SpectacleTuning.ActiveLabel);
    }

    [Fact]
    public void SurgeThresholdMultiplier_AffectsResolvedThreshold()
    {
        SpectacleTuning.Reset();
        float baseline = SpectacleDefinitions.ResolveSurgeThreshold();

        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            SurgeThresholdMultiplier = 1.25f,
        }, "test");
        float tuned = SpectacleDefinitions.ResolveSurgeThreshold();

        Assert.True(tuned > baseline);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void TokenMultipliers_AffectResolvedTokenConfig()
    {
        SpectacleTuning.Reset();
        SpectacleTokenConfig baseline = SpectacleDefinitions.GetTokenConfig(SpectacleDefinitions.Overkill);

        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            TokenCapMultiplier = 1.20f,
            TokenRegenMultiplier = 1.30f,
            TokenCapMultipliers = { [SpectacleDefinitions.Overkill] = 1.10f },
            TokenRegenMultipliers = { [SpectacleDefinitions.Overkill] = 1.10f },
        }, "test");
        SpectacleTokenConfig tuned = SpectacleDefinitions.GetTokenConfig(SpectacleDefinitions.Overkill);

        Assert.True(tuned.Cap > baseline.Cap);
        Assert.True(tuned.RegenPerSecond > baseline.RegenPerSecond);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void DifficultyMultipliers_AffectBalanceForNormalAndHard()
    {
        SpectacleTuning.Reset();
        float baselineNormalHp = Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        float baselineHardCount = Balance.GetEnemyCountMultiplier(DifficultyMode.Hard);
        float baselineHardSpawn = Balance.GetSpawnIntervalMultiplier(DifficultyMode.Hard);

        SpectacleTuning.Apply(new SpectacleTuningProfile
        {
            NormalEnemyHpMultiplier = 1.35f,
            HardEnemyCountMultiplier = 1.22f,
            HardSpawnIntervalMultiplier = 0.84f,
        }, "test");

        Assert.True(Balance.GetEnemyHpMultiplier(DifficultyMode.Normal) > baselineNormalHp);
        Assert.True(Balance.GetEnemyCountMultiplier(DifficultyMode.Hard) > baselineHardCount);
        Assert.True(Balance.GetSpawnIntervalMultiplier(DifficultyMode.Hard) < baselineHardSpawn);
        SpectacleTuning.Reset();
    }

    [Fact]
    public void Loader_ParsesSnakeCaseTuningJson()
    {
        const string json = """
        {
          "overkill_bloom_damage_scale_multiplier": 1.2,
          "detonation_max_targets_multiplier": 0.8,
          "normal_enemy_hp_multiplier": 1.28,
          "gain_multipliers": {
            "overkill": 1.3
          }
        }
        """;

        bool ok = SpectacleTuningLoader.TryLoadFromJson(json, out var profile, out string error);
        Assert.True(ok, error);
        Assert.Equal(1.2f, profile.OverkillBloomDamageScaleMultiplier, 3);
        Assert.Equal(0.8f, profile.DetonationMaxTargetsMultiplier, 3);
        Assert.Equal(1.28f, profile.NormalEnemyHpMultiplier, 3);
        Assert.Equal(1.3f, profile.ResolveGainMultiplier("overkill"), 3);
    }
}
