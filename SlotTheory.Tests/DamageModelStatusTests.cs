using System.Collections.Generic;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public class DamageModelStatusTests
{
    [Fact]
    public void DamageAmpStatus_IncreasesDamageTaken()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy = new FakeEnemy
        {
            Hp = 100f,
            DamageAmpRemaining = 1.2f,
            DamageAmpMultiplier = 0.25f,
        };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, enemies: new List<IEnemyView> { enemy });

        DamageModel.Apply(ctx);

        Assert.Equal(87.5f, enemy.Hp, 3);
    }

    [Fact]
    public void MarkedAndDamageAmp_BothApply()
    {
        var tower = new FakeTower { BaseDamage = 10f };
        var enemy = new FakeEnemy
        {
            Hp = 100f,
            MarkedRemaining = 1f,
            DamageAmpRemaining = 1f,
            DamageAmpMultiplier = 0.25f,
        };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, enemies: new List<IEnemyView> { enemy });

        DamageModel.Apply(ctx);

        float expectedDamage = 10f * (1f + Balance.MarkedDamageBonus) * (1f + 0.25f);
        Assert.Equal(100f - expectedDamage, enemy.Hp, 3);
    }

    [Fact]
    public void ApplyDamageAmp_UsesMaxDurationAndMultiplier()
    {
        var enemy = new FakeEnemy
        {
            DamageAmpRemaining = 1.2f,
            DamageAmpMultiplier = 0.18f,
        };

        Statuses.ApplyDamageAmp(enemy, duration: 0.8f, multiplier: 0.12f);
        Assert.Equal(1.2f, enemy.DamageAmpRemaining, 3);
        Assert.Equal(0.18f, enemy.DamageAmpMultiplier, 3);

        Statuses.ApplyDamageAmp(enemy, duration: 1.8f, multiplier: 0.30f);
        Assert.Equal(1.8f, enemy.DamageAmpRemaining, 3);
        Assert.Equal(0.30f, enemy.DamageAmpMultiplier, 3);
    }

    [Fact]
    public void ApplyDamageAmp_IgnoresNonPositiveInputs()
    {
        var enemy = new FakeEnemy
        {
            DamageAmpRemaining = 1.0f,
            DamageAmpMultiplier = 0.2f,
        };

        Statuses.ApplyDamageAmp(enemy, duration: 0f, multiplier: 0.4f);
        Statuses.ApplyDamageAmp(enemy, duration: 1.0f, multiplier: 0f);
        Statuses.ApplyDamageAmp(enemy, duration: -1f, multiplier: 0.4f);
        Statuses.ApplyDamageAmp(enemy, duration: 1.0f, multiplier: -0.1f);

        Assert.Equal(1.0f, enemy.DamageAmpRemaining, 3);
        Assert.Equal(0.2f, enemy.DamageAmpMultiplier, 3);
    }

    [Fact]
    public void ClearMarked_RemovesMarkedState()
    {
        var enemy = new FakeEnemy { MarkedRemaining = 2.0f };
        bool changed = Statuses.ClearMarked(enemy);
        Assert.True(changed);
        Assert.Equal(0f, enemy.MarkedRemaining, 3);
        Assert.False(enemy.IsMarked);
    }

    [Fact]
    public void CleanseSlow_TrimsDurationAndLiftsSeverity()
    {
        var enemy = new FakeEnemy
        {
            SlowRemaining = 4.0f,
            SlowSpeedFactor = 0.40f,
        };

        bool changed = Statuses.CleanseSlow(enemy,
            durationRetention: Balance.NullDroneSlowDurationRetention,
            severityLift: Balance.NullDroneSlowSeverityLift);

        Assert.True(changed);
        Assert.True(enemy.SlowRemaining < 4.0f);
        Assert.True(enemy.SlowSpeedFactor > 0.40f);
    }
}
