using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyRenderStateTests
{
    [Theory]
    [InlineData(1.00f, EnemyDamageBand.Healthy)]
    [InlineData(0.98f, EnemyDamageBand.Healthy)]
    [InlineData(0.97f, EnemyDamageBand.Worn)]
    [InlineData(0.80f, EnemyDamageBand.Worn)]
    [InlineData(0.79f, EnemyDamageBand.Damaged)]
    [InlineData(0.52f, EnemyDamageBand.Damaged)]
    [InlineData(0.51f, EnemyDamageBand.Critical)]
    [InlineData(0.00f, EnemyDamageBand.Critical)]
    public void ResolveDamageBand_UsesExpectedThresholds(float hpRatio, EnemyDamageBand expected)
    {
        Assert.Equal(expected, EnemyRenderState.ResolveDamageBand(hpRatio));
    }

    [Theory]
    [InlineData(1.00f, 0.00f)]
    [InlineData(0.75f, 0.423050f)]
    [InlineData(0.40f, 0.728923f)]
    [InlineData(0.00f, 1.00f)]
    public void ResolveDamageIntensity_MapsFromHpRatio(float hpRatio, float expected)
    {
        Assert.Equal(expected, EnemyRenderState.ResolveDamageIntensity(hpRatio), precision: 3);
    }

    [Fact]
    public void Constructor_CombinesPulseInputs_IntoEmissivePulse()
    {
        var baseline = new EnemyRenderState(
            hpRatio: 1f,
            thrustPulse: 0.2f,
            nearDeathPulse: 0f,
            nearDeathFlicker: 0f,
            hitFlash: 0f,
            isMarked: false,
            isSlowed: false);

        var boosted = new EnemyRenderState(
            hpRatio: 0.15f,
            thrustPulse: 0.8f,
            nearDeathPulse: 0.7f,
            nearDeathFlicker: 0.4f,
            hitFlash: 0.9f,
            isMarked: true,
            isSlowed: true);

        Assert.True(boosted.EmissivePulse > baseline.EmissivePulse);
        Assert.Equal(EnemyDamageBand.Critical, boosted.DamageBand);
    }
}
