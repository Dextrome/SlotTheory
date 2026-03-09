using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for WaveSystem.GetScaledHp — pure math, zero Godot dependencies.
/// Uses the explicit-difficulty overload so SettingsManager.Instance is never accessed.
/// </summary>
public class WaveSystemTests
{
    // ── Basic walker ──────────────────────────────────────────────────────

    [Fact]
    public void GetScaledHp_BasicWalker_Wave0_EqualsBaseHpTimesNormalMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        float actual   = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Normal);
        Assert.Equal(expected, actual, precision: 2);
    }

    [Fact]
    public void GetScaledHp_BasicWalker_Wave9_AppliesGrowthExponent()
    {
        // Wave index 9 → HP × 1.10^9
        float expected = Balance.BaseEnemyHp
                       * System.MathF.Pow(Balance.HpGrowthPerWave, 9)
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        float actual = WaveSystem.GetScaledHp("basic_walker", 9, DifficultyMode.Normal);
        Assert.Equal(expected, actual, precision: 2);
    }

    // ── Armored walker ────────────────────────────────────────────────────

    [Fact]
    public void GetScaledHp_ArmoredWalker_Wave0_AppliesTankyMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.TankyHpMultiplier
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        float actual = WaveSystem.GetScaledHp("armored_walker", 0, DifficultyMode.Normal);
        Assert.Equal(expected, actual, precision: 2);
    }

    // ── Hard difficulty ───────────────────────────────────────────────────

    [Fact]
    public void GetScaledHp_HardDifficulty_AppliesHardMultiplier()
    {
        float normal = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Normal);
        float hard   = WaveSystem.GetScaledHp("basic_walker", 0, DifficultyMode.Hard);

        float ratio = hard / normal;
        float expected = Balance.GetEnemyHpMultiplier(DifficultyMode.Hard)
                       / Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        Assert.Equal(expected, ratio, precision: 3);
    }

    // ── Swift walker ──────────────────────────────────────────────────────

    [Fact]
    public void GetScaledHp_SwiftWalker_Wave0_AppliesSwiftMultiplier()
    {
        float expected = Balance.BaseEnemyHp * Balance.SwiftHpMultiplier
                       * Balance.GetEnemyHpMultiplier(DifficultyMode.Normal);
        float actual = WaveSystem.GetScaledHp("swift_walker", 0, DifficultyMode.Normal);
        Assert.Equal(expected, actual, precision: 2);
    }
}
