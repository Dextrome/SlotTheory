using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for WaveSystem.GetScaledHp - pure math, zero Godot dependencies.
/// Uses the explicit-difficulty overload so SettingsManager.Instance is never accessed.
/// </summary>
public class WaveSystemTests
{
    [Fact]
    public void GetScaledHp_BasicWalker_Wave0_EqualsBaseHpTimesEasyMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        float actual = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Easy);
        Assert.Equal(expected, actual, precision: 2);
    }

    [Fact]
    public void GetScaledHp_BasicWalker_Wave9_AppliesGrowthExponent()
    {
        float expected = Balance.BaseEnemyHp
                       * System.MathF.Pow(Balance.HpGrowthPerWave, 9)
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        float actual = WaveSystem.GetScaledHp("basic_walker", 9, DifficultyMode.Easy);
        Assert.Equal(expected, actual, precision: 2);
    }

    [Fact]
    public void GetScaledHp_ArmoredWalker_Wave0_AppliesTankyMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.TankyHpMultiplier
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        float actual = WaveSystem.GetScaledHp("armored_walker", 0, DifficultyMode.Easy);
        Assert.Equal(expected, actual, precision: 2);
    }

    [Fact]
    public void GetScaledHp_NormalDifficulty_AppliesNormalMultiplier()
    {
        float easy = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Easy);
        float normal = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Normal);

        float ratio = normal / easy;
        float expected = Balance.GetEnemyHpMultiplier(DifficultyMode.Normal)
                       / Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        Assert.Equal(expected, ratio, precision: 3);
    }

    [Fact]
    public void GetScaledHp_HardDifficulty_AppliesHardMultiplier()
    {
        float normal = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Normal);
        float hard = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Hard);

        float ratio = hard / normal;
        float expected = Balance.GetEnemyHpMultiplier(DifficultyMode.Hard)
                       / Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        Assert.Equal(expected, ratio, precision: 3);
    }

    [Fact]
    public void GetScaledHp_SwiftWalker_Wave0_AppliesSwiftMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.SwiftHpMultiplier
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Easy);
        float actual = WaveSystem.GetScaledHp("swift_walker", 0, DifficultyMode.Easy);
        Assert.Equal(expected, actual, precision: 2);
    }
}
