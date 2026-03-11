using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyVisualProfileTests
{
    [Fact]
    public void Evaluate_NearDeathPulse_IsZeroAboveThreshold()
    {
        var sample = EnemyVisualProfile.Evaluate("basic_walker", elapsed: 1.2f, effectiveSpeed: 120f, hpRatio: 0.95f);
        Assert.Equal(0f, sample.NearDeathPulse, precision: 5);
        Assert.Equal(0f, sample.NearDeathFlicker, precision: 5);
    }

    [Fact]
    public void Evaluate_NearDeathPulse_ActivatesBelowThreshold()
    {
        var sample = EnemyVisualProfile.Evaluate("basic_walker", elapsed: 1.2f, effectiveSpeed: 120f, hpRatio: 0.08f);
        Assert.True(sample.NearDeathPulse > 0.05f);
        Assert.True(sample.NearDeathFlicker > 0.05f);
    }

    [Fact]
    public void Evaluate_ThrustPulse_IncreasesWithSpeed()
    {
        var slow = EnemyVisualProfile.Evaluate("basic_walker", elapsed: 2.4f, effectiveSpeed: 65f, hpRatio: 1f);
        var fast = EnemyVisualProfile.Evaluate("basic_walker", elapsed: 2.4f, effectiveSpeed: 230f, hpRatio: 1f);
        Assert.True(fast.ThrustPulse > slow.ThrustPulse);
    }

    [Fact]
    public void Evaluate_SwiftHasMoreJitterThanBasic()
    {
        var basic = EnemyVisualProfile.Evaluate("basic_walker", elapsed: 3.17f, effectiveSpeed: 120f, hpRatio: 1f);
        var swift = EnemyVisualProfile.Evaluate("swift_walker", elapsed: 3.17f, effectiveSpeed: 240f, hpRatio: 1f);
        Assert.True(swift.JitterMagnitude > basic.JitterMagnitude * 1.8f);
    }

    [Fact]
    public void Evaluate_ArmoredHasLowerTiltMagnitudeThanSwift()
    {
        var swift = EnemyVisualProfile.Evaluate("swift_walker", elapsed: 4.1f, effectiveSpeed: 240f, hpRatio: 1f);
        var armored = EnemyVisualProfile.Evaluate("armored_walker", elapsed: 4.1f, effectiveSpeed: 60f, hpRatio: 1f);
        Assert.True(System.MathF.Abs(armored.BodyTiltRad) < System.MathF.Abs(swift.BodyTiltRad));
    }

    [Fact]
    public void Evaluate_OutputValuesRemainInExpectedRange()
    {
        var sample = EnemyVisualProfile.Evaluate("swift_walker", elapsed: 6.4f, effectiveSpeed: 240f, hpRatio: 0.1f);
        Assert.InRange(sample.ThrustPulse, 0f, 3.2f);
        Assert.InRange(sample.NearDeathPulse, 0f, 1f);
        Assert.InRange(sample.NearDeathFlicker, 0f, 1f);
    }
}
