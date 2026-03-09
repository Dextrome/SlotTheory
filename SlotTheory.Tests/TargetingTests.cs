using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public class TargetingTests
{
    private static FakeTower Tower(TargetingMode mode = TargetingMode.First, float range = 500f)
        => new() { TargetingMode = mode, Range = range };

    private static FakeEnemy E(float progress, float hp, Vector2? pos = null)
        => new() { ProgressRatio = progress, Hp = hp, GlobalPosition = pos ?? Vector2.Zero };

    // ── Empty / out of range ──────────────────────────────────────────────

    [Fact]
    public void SelectTarget_EmptyList_ReturnsNull()
    {
        var result = Targeting.SelectTarget(Tower(), new List<FakeEnemy>());
        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_AllDead_ReturnsNull()
    {
        var enemies = new List<FakeEnemy> { E(0.5f, 0f), E(0.3f, 0f) };
        var result = Targeting.SelectTarget(Tower(), enemies);
        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_AllOutOfRange_ReturnsNull()
    {
        var tower = Tower(range: 50f);
        var enemies = new List<FakeEnemy>
        {
            E(0.5f, 100f, new Vector2(200f, 0f)), // 200 px away, range = 50
        };
        var result = Targeting.SelectTarget(tower, enemies);
        Assert.Null(result);
    }

    // ── Mode: First ───────────────────────────────────────────────────────

    [Fact]
    public void SelectTarget_First_ReturnsHighestProgress()
    {
        var enemies = new List<FakeEnemy>
        {
            E(0.3f, 100f),
            E(0.9f, 50f), // highest progress
            E(0.5f, 80f),
        };
        var result = Targeting.SelectTarget(Tower(TargetingMode.First), enemies);
        Assert.NotNull(result);
        Assert.Equal(0.9f, result.ProgressRatio);
    }

    // ── Mode: Strongest ───────────────────────────────────────────────────

    [Fact]
    public void SelectTarget_Strongest_ReturnsHighestHp()
    {
        var enemies = new List<FakeEnemy>
        {
            E(0.5f, 30f),
            E(0.2f, 200f), // highest HP
            E(0.8f, 80f),
        };
        var result = Targeting.SelectTarget(Tower(TargetingMode.Strongest), enemies);
        Assert.NotNull(result);
        Assert.Equal(200f, result.Hp);
    }

    // ── Mode: LowestHp ────────────────────────────────────────────────────

    [Fact]
    public void SelectTarget_LowestHp_ReturnsLowestHp()
    {
        var enemies = new List<FakeEnemy>
        {
            E(0.5f, 100f),
            E(0.2f, 15f),  // lowest HP
            E(0.8f, 80f),
        };
        var result = Targeting.SelectTarget(Tower(TargetingMode.LowestHp), enemies);
        Assert.NotNull(result);
        Assert.Equal(15f, result.Hp);
    }

    // ── Range check ───────────────────────────────────────────────────────

    [Fact]
    public void SelectTarget_IgnoreRange_IncludesOutOfRangeEnemies()
    {
        var tower = Tower(range: 10f);
        var enemies = new List<FakeEnemy>
        {
            E(0.8f, 100f, new Vector2(500f, 0f)), // far away but alive
        };
        var result = Targeting.SelectTarget(tower, enemies, ignoreRange: true);
        Assert.NotNull(result);
    }

    [Fact]
    public void SelectTarget_OnlyInRangeEnemiesConsidered()
    {
        // Two enemies: one in range (close), one out of range (far).
        // Tower picks First (highest progress). Out-of-range enemy has higher progress
        // but should be excluded — in-range enemy (lower progress) should win.
        var tower = new FakeTower
        {
            TargetingMode = TargetingMode.First,
            Range = 100f,
            GlobalPosition = Vector2.Zero,
        };
        var enemies = new List<FakeEnemy>
        {
            E(0.9f, 100f, new Vector2(500f, 0f)),  // out of range, high progress
            E(0.4f, 100f, new Vector2(50f,  0f)),  // in range
        };
        var result = Targeting.SelectTarget(tower, enemies);
        Assert.NotNull(result);
        Assert.Equal(0.4f, result.ProgressRatio);
    }
}
