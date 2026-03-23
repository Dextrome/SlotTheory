using Godot;
using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for Wildfire fire trail hazard damage behavior.
///
/// CombatSim.UpdateBurnAndTrails (section B) can't be instantiated in unit tests
/// because CombatSim is a Godot Node. These tests instead replicate the trail tick
/// logic directly -- the same four lines that live in the sim loop -- to:
///   a) verify the DPS formula and radius check in isolation, and
///   b) act as a regression harness if Balance constants or the tick math change.
///
/// The trail drop timer / first-ignition behavior is covered in ModifierTests.cs.
/// </summary>
public class WildfireTrailTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Replicates the per-frame trail tick from CombatSim.UpdateBurnAndTrails section B.
    /// Returns the damage dealt to the enemy this tick.
    /// </summary>
    private static float SimulateTrailTick(
        Vector2 trailPos, float trailDps,
        FakeEnemy enemy, float delta)
    {
        float trailDamage = trailDps * delta;
        if (trailDamage <= 0.001f) return 0f;
        if (trailPos.DistanceTo(enemy.GlobalPosition) > Balance.WildfireTrailRadius) return 0f;

        float hpBefore = enemy.Hp;
        enemy.Hp = MathF.Max(0f, enemy.Hp - trailDamage);
        return hpBefore - enemy.Hp;
    }

    /// <summary>Trail DPS = burnDPS × WildfireTrailDamageRatio.</summary>
    private static float TrailDps(float burnDps) => burnDps * Balance.WildfireTrailDamageRatio;

    // ── DPS formula ───────────────────────────────────────────────────────

    [Fact]
    public void TrailDps_IsTrailRatioOfBurnDps()
    {
        float burnDps = 10f * Balance.WildfireBurnDpsRatio; // Rapid Shooter example
        float expected = burnDps * Balance.WildfireTrailDamageRatio;

        Assert.Equal(expected, TrailDps(burnDps), precision: 4);
    }

    // ── Enemy in radius takes damage ──────────────────────────────────────

    [Fact]
    public void TrailTick_EnemyAtCenter_TakesDpsTimesDelta()
    {
        float burnDps = 4f;
        float dps     = TrailDps(burnDps);
        var enemy     = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        float delta   = 0.016f; // ~60 fps

        float dealt = SimulateTrailTick(Vector2.Zero, dps, enemy, delta);

        Assert.Equal(dps * delta, dealt,   precision: 4);
        Assert.Equal(100f - dps * delta, enemy.Hp, precision: 4);
    }

    [Fact]
    public void TrailTick_EnemyInsideRadius_TakesDamage()
    {
        float dps   = TrailDps(4f);
        var enemy   = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(Balance.WildfireTrailRadius - 1f, 0f) };

        float dealt = SimulateTrailTick(Vector2.Zero, dps, enemy, 1f);

        Assert.True(dealt > 0f);
    }

    // ── Enemy outside radius takes no damage ─────────────────────────────

    [Fact]
    public void TrailTick_EnemyOutsideRadius_TakesNoDamage()
    {
        float dps   = TrailDps(4f);
        var enemy   = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(Balance.WildfireTrailRadius + 1f, 0f) };

        float dealt = SimulateTrailTick(Vector2.Zero, dps, enemy, 1f);

        Assert.Equal(0f, dealt);
        Assert.Equal(100f, enemy.Hp);
    }

    [Fact]
    public void TrailTick_EnemyExactlyAtRadius_TakesDamage()
    {
        // Check is > radius (strict), so distance == radius is still within range.
        float dps   = TrailDps(4f);
        var enemy   = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(Balance.WildfireTrailRadius, 0f) };

        float dealt = SimulateTrailTick(Vector2.Zero, dps, enemy, 1f);

        Assert.True(dealt > 0f);
    }

    // ── HP never goes below zero ───────────────────────────────────────────

    [Fact]
    public void TrailTick_DamageExceedsHp_ClampsToZero()
    {
        float dps   = TrailDps(1000f);
        var enemy   = new FakeEnemy { Hp = 5f, GlobalPosition = Vector2.Zero };

        SimulateTrailTick(Vector2.Zero, dps, enemy, 1f);

        Assert.Equal(0f, enemy.Hp);
    }

    [Fact]
    public void TrailTick_DamageExceedsHp_DealtEqualsRemainingHp()
    {
        float dps      = TrailDps(1000f);
        var enemy      = new FakeEnemy { Hp = 3f, GlobalPosition = Vector2.Zero };

        float dealt = SimulateTrailTick(Vector2.Zero, dps, enemy, 1f);

        Assert.Equal(3f, dealt, precision: 4);
    }

    // ── Multiple ticks accumulate correctly ───────────────────────────────

    [Fact]
    public void TrailTick_TenTicksAtSixtyFps_TotalDamageMatchesOneSecondOfDps()
    {
        float burnDps  = 6f;
        float dps      = TrailDps(burnDps);
        var enemy      = new FakeEnemy { Hp = 1000f, GlobalPosition = Vector2.Zero };
        float delta    = 1f / 60f;
        float total    = 0f;

        for (int i = 0; i < 60; i++)
            total += SimulateTrailTick(Vector2.Zero, dps, enemy, delta);

        Assert.Equal(dps, total, precision: 2); // 1 second of damage ≈ dps
    }

    // ── Trail radius constant sanity ─────────────────────────────────────

    [Fact]
    public void Balance_WildfireTrailRadius_IsPositive()
    {
        Assert.True(Balance.WildfireTrailRadius > 0f);
    }
}
