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

    [Theory]
    [InlineData(false, false, 8)]
    [InlineData(true, false, 24)]
    [InlineData(false, true, 8)]
    [InlineData(true, true, 10)]
    public void ResolveStatusDetonationMaxTargets_UsesExpectedCaps(bool globalSurge, bool reducedMotion, int expected)
    {
        int result = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge, reducedMotion);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(SpectacleDefinitions.ChillShot, SpectacleDefinitions.Overkill, ComboExplosionSkin.ChillShatter)]
    [InlineData(SpectacleDefinitions.ChainReaction, SpectacleDefinitions.FocusLens, ComboExplosionSkin.ChainArc)]
    [InlineData(SpectacleDefinitions.SplitShot, SpectacleDefinitions.Overreach, ComboExplosionSkin.SplitShrapnel)]
    [InlineData(SpectacleDefinitions.FocusLens, SpectacleDefinitions.Momentum, ComboExplosionSkin.FocusImplosion)]
    [InlineData(SpectacleDefinitions.Momentum, SpectacleDefinitions.Overkill, ComboExplosionSkin.Default)]
    public void ResolveComboExplosionSkin_MapsByComboFamily(string modA, string modB, ComboExplosionSkin expected)
    {
        ComboExplosionSkin skin = SpectacleExplosionCore.ResolveComboExplosionSkin(modA, modB);
        Assert.Equal(expected, skin);
    }

    [Fact]
    public void ResolveComboExplosionSkin_NormalizesLegacyChillId()
    {
        ComboExplosionSkin skin = SpectacleExplosionCore.ResolveComboExplosionSkin("chill_shot", SpectacleDefinitions.SplitShot);
        Assert.Equal(ComboExplosionSkin.ChillShatter, skin);
    }

    [Fact]
    public void ResolveComboExplosionSkin_UsesPriorityOrder_ChillOverChainOverSplitOverFocus()
    {
        Assert.Equal(
            ComboExplosionSkin.ChillShatter,
            SpectacleExplosionCore.ResolveComboExplosionSkin(SpectacleDefinitions.ChillShot, SpectacleDefinitions.ChainReaction));
        Assert.Equal(
            ComboExplosionSkin.ChainArc,
            SpectacleExplosionCore.ResolveComboExplosionSkin(SpectacleDefinitions.ChainReaction, SpectacleDefinitions.SplitShot));
        Assert.Equal(
            ComboExplosionSkin.SplitShrapnel,
            SpectacleExplosionCore.ResolveComboExplosionSkin(SpectacleDefinitions.SplitShot, SpectacleDefinitions.FocusLens));
    }

    [Theory]
    [InlineData(ComboExplosionSkin.ChillShatter, false, 0, true, ExplosionResidueKind.FrostSlow, SpectacleExplosionCore.ResidueFrostSlowDurationSeconds)]
    [InlineData(ComboExplosionSkin.SplitShrapnel, false, 0, true, ExplosionResidueKind.BurnPatch, SpectacleExplosionCore.ResidueBurnDurationSeconds)]
    [InlineData(ComboExplosionSkin.ChainArc, false, 0, true, ExplosionResidueKind.VulnerabilityZone, SpectacleExplosionCore.ResidueVulnerabilityDurationSeconds)]
    [InlineData(ComboExplosionSkin.Default, false, 0, false, ExplosionResidueKind.None, 0f)]
    public void ResolveResidueProfile_MapsExplosionFamilyToExpectedResidue(
        ComboExplosionSkin skin,
        bool globalSurge,
        int chainIndex,
        bool shouldSpawn,
        ExplosionResidueKind expectedKind,
        float expectedDuration)
    {
        ExplosionResidueProfile profile = SpectacleExplosionCore.ResolveResidueProfile(
            skin,
            globalSurge,
            surgePower: 1.2f,
            chainIndex);

        Assert.Equal(shouldSpawn, profile.ShouldSpawn);
        Assert.Equal(expectedKind, profile.Kind);
        Assert.Equal(expectedDuration, profile.DurationSeconds, 3);
    }

    [Fact]
    public void ResolveResidueProfile_UsesChainStrideToAvoidVisualClutter()
    {
        ExplosionResidueProfile nonGlobalSkip = SpectacleExplosionCore.ResolveResidueProfile(
            ComboExplosionSkin.ChainArc,
            globalSurge: false,
            surgePower: 1.0f,
            chainIndex: 1);
        ExplosionResidueProfile nonGlobalSpawn = SpectacleExplosionCore.ResolveResidueProfile(
            ComboExplosionSkin.ChainArc,
            globalSurge: false,
            surgePower: 1.0f,
            chainIndex: 3);
        ExplosionResidueProfile globalSpawn = SpectacleExplosionCore.ResolveResidueProfile(
            ComboExplosionSkin.ChainArc,
            globalSurge: true,
            surgePower: 1.0f,
            chainIndex: 2);

        Assert.False(nonGlobalSkip.ShouldSpawn);
        Assert.True(nonGlobalSpawn.ShouldSpawn);
        Assert.True(globalSpawn.ShouldSpawn);
    }

    [Fact]
    public void ResolveExplosionHitStopProfile_MinorExplosion_DoesNotApplyHitStop()
    {
        ExplosionHitStopProfile profile = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
            majorExplosion: false,
            globalSurge: false,
            surgePower: 1.0f);

        Assert.False(profile.ShouldApply);
    }

    [Fact]
    public void ResolveExplosionHitStopProfile_MajorExplosion_StaysWithinConfiguredBounds()
    {
        ExplosionHitStopProfile profile = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
            majorExplosion: true,
            globalSurge: true,
            surgePower: 2.2f);

        Assert.True(profile.ShouldApply);
        Assert.InRange(
            profile.DurationSeconds,
            SpectacleExplosionCore.HitStopMinDurationSeconds,
            SpectacleExplosionCore.HitStopMaxDurationSeconds);
        Assert.InRange(profile.SlowScale, 0.20f, 0.55f);
    }

    [Fact]
    public void ResolveLargeSurgeAfterimageStrength_OnlyTriggersForLargeOrGlobalMajorExplosions()
    {
        float none = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
            majorExplosion: false,
            globalSurge: false,
            surgePower: 2.0f);
        float belowThreshold = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
            majorExplosion: true,
            globalSurge: false,
            surgePower: 1.2f);
        float global = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
            majorExplosion: true,
            globalSurge: true,
            surgePower: 1.0f);

        Assert.Equal(0f, none, 3);
        Assert.Equal(0f, belowThreshold, 3);
        Assert.True(global > 0f);
    }
}
