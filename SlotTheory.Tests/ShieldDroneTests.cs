using System.Collections.Generic;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for Shield Drone combat behavior:
/// - Damage reduction via IsShieldProtected flag in DamageModel
/// - HP scaling via WaveSystem.GetScaledHp
/// - Default stub state
/// </summary>
public class ShieldDroneTests
{
    // ── DamageModel ────────────────────────────────────────────────────────────

    [Fact]
    public void IsShieldProtected_ReducesDamageByExpectedFactor()
    {
        var tower = new FakeTower { BaseDamage = 100f };
        var enemy = new FakeEnemy { Hp = 500f, IsShieldProtected = true };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, enemies: new List<IEnemyView> { enemy });

        DamageModel.Apply(ctx);

        float expectedDamage = 100f * (1f - Balance.ShieldDroneProtectionReduction);
        Assert.Equal(500f - expectedDamage, enemy.Hp, precision: 3);
    }

    [Fact]
    public void IsShieldProtected_False_DoesNotReduceDamage()
    {
        var tower = new FakeTower { BaseDamage = 100f };
        var enemy = new FakeEnemy { Hp = 500f, IsShieldProtected = false };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, enemies: new List<IEnemyView> { enemy });

        DamageModel.Apply(ctx);

        Assert.Equal(400f, enemy.Hp, precision: 3);
    }

    [Fact]
    public void IsShieldProtected_And_Marked_BothApplyIndependently()
    {
        var tower = new FakeTower { BaseDamage = 100f };
        var enemy = new FakeEnemy { Hp = 500f, IsShieldProtected = true, MarkedRemaining = 1f };
        var ctx = new DamageContext(tower, enemy, waveIndex: 0, enemies: new List<IEnemyView> { enemy });

        DamageModel.Apply(ctx);

        // Marked adds bonus, then shield reduces - both are multiplicative
        float damage = 100f * (1f + Balance.MarkedDamageBonus) * (1f - Balance.ShieldDroneProtectionReduction);
        Assert.Equal(500f - damage, enemy.Hp, precision: 3);
    }

    [Fact]
    public void ShieldProtectionReduction_MatchesBalanceConstant()
    {
        // Guard against accidental Balance.cs constant changes breaking the test expectations
        Assert.Equal(0.35f, Balance.ShieldDroneProtectionReduction, precision: 4);
    }

    // ── WaveSystem.GetScaledHp ─────────────────────────────────────────────────

    [Fact]
    public void GetScaledHp_ShieldDrone_Wave0_AppliesShieldDroneMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.ShieldDroneHpMultiplier
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        float actual = WaveSystem.GetScaledHp("shield_drone", 0, DifficultyMode.Easy);
        Assert.Equal(expected, actual, precision: 2);
    }

    [Fact]
    public void GetScaledHp_ShieldDrone_IsMoreThanBasicWalker()
    {
        float drone = WaveSystem.GetScaledHp("shield_drone", 0, DifficultyMode.Easy);
        float basic = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Easy);
        Assert.True(drone > basic,
            $"Shield Drone HP ({drone}) should exceed Basic Walker HP ({basic})");
    }

    [Fact]
    public void GetScaledHp_ShieldDrone_ScalesWithWaveIndex()
    {
        float wave0 = WaveSystem.GetScaledHp("shield_drone", 0, DifficultyMode.Easy);
        float wave9 = WaveSystem.GetScaledHp("shield_drone", 9, DifficultyMode.Easy);
        float expectedRatio = System.MathF.Pow(Balance.HpGrowthPerWave, 9);
        Assert.Equal(expectedRatio, wave9 / wave0, precision: 3);
    }

    // ── FakeEnemy default state ────────────────────────────────────────────────

    [Fact]
    public void FakeEnemy_IsShieldProtected_DefaultsFalse()
    {
        var enemy = new FakeEnemy();
        Assert.False(enemy.IsShieldProtected);
    }

    [Fact]
    public void FakeEnemy_IsShieldProtected_CanBeSetTrue()
    {
        var enemy = new FakeEnemy { IsShieldProtected = true };
        Assert.True(enemy.IsShieldProtected);
    }
}
