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
    public void Loader_ParsesSnakeCaseTuningJson()
    {
        const string json = """
        {
          "overkill_bloom_damage_scale_multiplier": 1.2,
          "detonation_max_targets_multiplier": 0.8,
          "gain_multipliers": {
            "overkill": 1.3
          }
        }
        """;

        bool ok = SpectacleTuningLoader.TryLoadFromJson(json, out var profile, out string error);
        Assert.True(ok, error);
        Assert.Equal(1.2f, profile.OverkillBloomDamageScaleMultiplier, 3);
        Assert.Equal(0.8f, profile.DetonationMaxTargetsMultiplier, 3);
        Assert.Equal(1.3f, profile.ResolveGainMultiplier("overkill"), 3);
    }
}
