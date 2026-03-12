using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public class SpectacleExplosionCoreTests
{
    [Fact]
    public void OverkillBloomProfile_BelowThreshold_DoesNotTrigger()
    {
        OverkillBloomProfile profile = SpectacleExplosionCore.BuildOverkillBloomProfile(
            SpectacleExplosionCore.OverkillBloomOverflowThreshold - 0.01f);

        Assert.False(profile.ShouldTrigger);
        Assert.Equal(0f, profile.VisualRadius, 3);
        Assert.Equal(0f, profile.BloomDamage, 3);
    }

    [Fact]
    public void OverkillBloomProfile_HighOverflow_ClampsDamageAndTargetCount()
    {
        OverkillBloomProfile profile = SpectacleExplosionCore.BuildOverkillBloomProfile(999f);

        Assert.True(profile.ShouldTrigger);
        Assert.Equal(SpectacleExplosionCore.OverkillBloomDamageCap, profile.BloomDamage, 3);
        Assert.Equal(SpectacleExplosionCore.OverkillBloomRadiusMax, profile.VisualRadius, 3);
        Assert.Equal(7, profile.MaxTargets);
    }

    [Fact]
    public void OverkillBloomProfile_VisualRadiusContinuesScaling_AfterDamageCaps()
    {
        // Both values are above damage-cap threshold.
        OverkillBloomProfile lower = SpectacleExplosionCore.BuildOverkillBloomProfile(220f);
        OverkillBloomProfile higher = SpectacleExplosionCore.BuildOverkillBloomProfile(300f);

        Assert.Equal(SpectacleExplosionCore.OverkillBloomDamageCap, lower.BloomDamage, 3);
        Assert.Equal(SpectacleExplosionCore.OverkillBloomDamageCap, higher.BloomDamage, 3);
        Assert.True(higher.VisualRadius > lower.VisualRadius);
    }

    [Theory]
    [InlineData(false, 0.94f, false)]
    [InlineData(false, 0.95f, true)]
    [InlineData(false, 1.25f, true)]
    [InlineData(true, 0.10f, true)]
    public void ShouldEmitSecondStage_FollowsMajorOrPowerRule(bool major, float power, bool expected)
    {
        bool result = SpectacleExplosionCore.ShouldEmitSecondStage(major, power);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GlobalSurgeWaveTiming_ReducedMotion_UsesZeroDelays()
    {
        GlobalSurgeWaveTiming timing = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(
            distance: 420f,
            contributors: 4,
            reducedMotion: true);

        Assert.Equal(0f, timing.ImpactDelay, 4);
        Assert.Equal(0f, timing.PreFlashDelay, 4);
    }

    [Fact]
    public void GlobalSurgeWaveTiming_PreFlashLeadsImpact_ByConfiguredLead()
    {
        GlobalSurgeWaveTiming timing = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(
            distance: 440f,
            contributors: 2,
            reducedMotion: false);

        Assert.True(timing.ImpactDelay > SpectacleExplosionCore.GlobalSurgeWavePreFlashLeadSeconds);
        Assert.Equal(
            SpectacleExplosionCore.GlobalSurgeWavePreFlashLeadSeconds,
            timing.ImpactDelay - timing.PreFlashDelay,
            3);
    }

    [Fact]
    public void GlobalSurgeWaveTiming_WaveSpeedScalesWithContributors()
    {
        GlobalSurgeWaveTiming low = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(
            distance: 200f,
            contributors: 1,
            reducedMotion: false);
        GlobalSurgeWaveTiming high = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(
            distance: 200f,
            contributors: 10,
            reducedMotion: false);

        Assert.True(high.WaveSpeed > low.WaveSpeed);
        Assert.Equal(SpectacleExplosionCore.GlobalSurgeWaveSpeedMin, low.WaveSpeed, 3);
        Assert.Equal(SpectacleExplosionCore.GlobalSurgeWaveSpeedMax, high.WaveSpeed, 3);
    }
}
