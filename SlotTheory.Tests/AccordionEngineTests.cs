using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for Accordion Engine mechanics.
///
/// Coverage:
///   1. Formation compression math (via AccordionFormation.Compress -- pure C#, no Godot deps)
///   2. isChain differentiation: primary hit (isChain=false) vs. secondary hits (isChain=true)
///      as expressed through the BlastCore modifier's OnHit gate.
///
/// CombatSim itself is not tested here because it requires Godot scene nodes (EnemyInstance,
/// PathFollow2D). The compression logic is extracted into AccordionFormation so that the
/// pure math can be tested independently.
/// </summary>
public class AccordionEngineTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private const float Factor = Balance.AccordionCompressionFactor; // 0.25f
    private const float Spacing = Balance.AccordionMinSpacingPx;     // 8f

    private static ModifierDef Def(string id) => new(id, id, "", new Dictionary<string, float>());

    private static DamageContext Ctx(ITowerView attacker, IEnemyView target,
                                     IEnumerable<IEnemyView>? enemies = null,
                                     bool isChain = false)
        => new(attacker, target, waveIndex: 0,
               enemies ?? new List<IEnemyView>(),
               state: null, isChain: isChain);

    // ── Formation compression -- basic math ────────────────────────────────────

    [Fact]
    public void Compress_ThreeEnemies_EvenSpread_MovesTowardMedian()
    {
        // Median = arr[3/2] = arr[1] = 500
        // [0] = 500 + (100-500)*0.25 = 400  |  [1] = 500  |  [2] = 500 + (900-500)*0.25 = 600
        float[] progress = [100f, 500f, 900f];
        AccordionFormation.Compress(progress, Factor, Spacing);

        Assert.Equal(400f, progress[0], precision: 2);
        Assert.Equal(500f, progress[1], precision: 2);
        Assert.Equal(600f, progress[2], precision: 2);
    }

    [Fact]
    public void Compress_TwoEnemies_TrailingMovesToPullDistance()
    {
        // Even count: median = arr[2/2] = arr[1] = 800 (the leading enemy).
        // [0] = 800 + (200-800)*0.25 = 800 - 150 = 650  |  [1] = 800 (unchanged)
        float[] progress = [200f, 800f];
        AccordionFormation.Compress(progress, Factor, Spacing);

        Assert.Equal(650f, progress[0], precision: 2);
        Assert.Equal(800f, progress[1], precision: 2);
    }

    [Fact]
    public void Compress_FactorZero_AllCollapsesToMedian()
    {
        // Factor=0 → every value becomes median (= arr[1] = 500 for 3 enemies).
        // After collapse all are at 500. Spacing pass nudges trailing enemies back:
        //   i=1: minAllowed=500-8=492 → arr[1]=492
        //   i=0: minAllowed=492-8=484 → arr[0]=484
        float[] progress = [100f, 500f, 900f];
        AccordionFormation.Compress(progress, compressionFactor: 0f, Spacing);

        Assert.Equal(484f, progress[0], precision: 2);
        Assert.Equal(492f, progress[1], precision: 2);
        Assert.Equal(500f, progress[2], precision: 2);
    }

    [Fact]
    public void Compress_FactorOne_PositionsUnchanged()
    {
        // Factor=1 → (x-median)*1 = original offset preserved → no movement at all.
        float[] progress = [100f, 500f, 900f];
        AccordionFormation.Compress(progress, compressionFactor: 1f, Spacing);

        Assert.Equal(100f, progress[0], precision: 2);
        Assert.Equal(500f, progress[1], precision: 2);
        Assert.Equal(900f, progress[2], precision: 2);
    }

    [Fact]
    public void Compress_SingleEnemy_NoChangeAndNoException()
    {
        float[] progress = [500f];
        AccordionFormation.Compress(progress, Factor, Spacing);
        Assert.Equal(500f, progress[0], precision: 2);
    }

    // ── Formation compression -- minimum spacing enforcement ───────────────────

    [Fact]
    public void Compress_CloselyPackedEnemies_EnforcesMinimumSpacing()
    {
        // Three enemies almost on top of each other. After compression they'd violate spacing.
        // [490, 500, 510]: median=arr[1]=500
        //   After compress: [497.5, 500, 502.5]
        //   Spacing pass (i=1): minAllowed=502.5-8=494.5; arr[1]=500>494.5 → arr[1]=494.5
        //   Spacing pass (i=0): minAllowed=494.5-8=486.5; arr[0]=497.5>486.5 → arr[0]=486.5
        float[] progress = [490f, 500f, 510f];
        AccordionFormation.Compress(progress, Factor, Spacing);

        Assert.Equal(486.5f, progress[0], precision: 2);
        Assert.Equal(494.5f, progress[1], precision: 2);
        Assert.Equal(502.5f, progress[2], precision: 2);

        // All spacings must be >= minSpacingPx
        Assert.True(progress[1] - progress[0] >= Spacing - 0.01f);
        Assert.True(progress[2] - progress[1] >= Spacing - 0.01f);
    }

    [Fact]
    public void Compress_SpacingAlwaysMaintainedForResultingPositions()
    {
        // Generic invariant: after compression, every adjacent pair must satisfy spacing.
        float[] progress = [50f, 52f, 54f, 900f];
        AccordionFormation.Compress(progress, Factor, Spacing);

        for (int i = 0; i < progress.Length - 1; i++)
            Assert.True(progress[i + 1] - progress[i] >= Spacing - 0.01f,
                $"Spacing violated between index {i} and {i + 1}: {progress[i + 1] - progress[i]:F2} < {Spacing}");
    }

    // ── Formation compression -- negative progress clamping ────────────────────

    [Fact]
    public void Compress_NegativeResultClamped_NeverBelowZero()
    {
        // Two enemies at [0, 4]: median=arr[1]=4
        //   [0]=4+(0-4)*0.25=3, [1]=4
        //   Spacing pass (i=0): minAllowed=4-8=-4; arr[0]=3>-4 → arr[0]=-4
        //   Clamp: -4→0
        // Result: [0, 4]
        float[] progress = [0f, 4f];
        AccordionFormation.Compress(progress, Factor, Spacing);

        Assert.True(progress[0] >= 0f, $"Progress[0] should not be negative, got {progress[0]}");
        Assert.True(progress[1] >= 0f, $"Progress[1] should not be negative, got {progress[1]}");
    }

    [Fact]
    public void Compress_AllResultsNonNegative()
    {
        // Invariant: no enemy progress should go negative after compression.
        float[] progress = [0f, 1f, 2f, 3f, 4f];
        AccordionFormation.Compress(progress, Factor, Spacing);
        foreach (float p in progress)
            Assert.True(p >= 0f, $"Negative progress after compression: {p}");
    }

    // ── isChain differentiation -- Blast Core fires on all hits (proc spaghetti) ─
    //
    // Blast Core fires on primary AND secondary (chain) hits by design.
    // "Proc spaghetti is desired" -- chain + split combos cascade explosions.

    [Fact]
    public void BlastCore_ApplyToChainTargets_IsTrue()
    {
        var mod = new BlastCore(Def("blast_core"));
        Assert.True(mod.ApplyToChainTargets);
    }

    [Fact]
    public void BlastCore_OnHit_ReturnsFalse_WhenIsChainTrue()
    {
        var tower  = new FakeTower { BaseDamage = 20f };
        var target = new FakeEnemy { Hp = 100f };
        var mod    = new BlastCore(Def("blast_core"));
        tower.Modifiers.Add(mod);

        var ctx = Ctx(tower, target, isChain: true);
        ctx.FinalDamage = tower.BaseDamage;

        bool result = mod.OnHit(ctx);
        Assert.False(result);
    }

    [Fact]
    public void BlastCore_OnHit_ReturnsTrue_WhenIsChainFalse_AndEnemyInRange()
    {
        // One enemy in splash radius: BlastCore should return true (splash fired).
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby  = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(50f, 0f) };
        var enemies = new List<IEnemyView> { target, nearby };
        var mod     = new BlastCore(Def("blast_core"));
        tower.Modifiers.Add(mod);

        var ctx = Ctx(tower, target, enemies, isChain: false);
        ctx.FinalDamage = tower.BaseDamage;

        bool result = mod.OnHit(ctx);
        Assert.True(result);
    }

    [Fact]
    public void BlastCore_SplashDamagesNearbyEnemy_OnPrimaryHit()
    {
        // isChain=false (primary): nearby enemy within BlastCoreRadius should take splash damage.
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby  = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(50f, 0f) }; // within 140px
        var enemies = new List<IEnemyView> { target, nearby };
        var mod     = new BlastCore(Def("blast_core"));
        tower.Modifiers.Add(mod);

        var ctx = Ctx(tower, target, enemies, isChain: false);
        ctx.FinalDamage = tower.BaseDamage; // 20f

        DamageModel.Apply(ctx);

        // Nearby enemy should have taken splash: 20 * 0.45 = 9 damage
        float expectedSplash = 20f * Balance.BlastCoreDamageRatio;
        Assert.Equal(100f - expectedSplash, nearby.Hp, precision: 2);
    }

    [Fact]
    public void BlastCore_SplashWithChill_AppliesSlowToNearbyEnemy()
    {
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby  = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(50f, 0f) };
        var enemies = new List<IEnemyView> { target, nearby };
        tower.Modifiers.Add(new BlastCore(Def("blast_core")));
        tower.Modifiers.Add(new Slow(Def("slow")));
        Assert.True(Statuses.TryGetChillSlowFactor(tower, out float chillFactor));
        Assert.Equal(Balance.SlowSpeedFactor, chillFactor, precision: 3);

        var ctx = Ctx(tower, target, enemies, isChain: false);
        ctx.FinalDamage = tower.BaseDamage;

        DamageModel.Apply(ctx);

        Assert.True(nearby.Hp < 100f);
        Assert.True(nearby.SlowRemaining > 0f);
        Assert.Equal(Balance.SlowSpeedFactor, nearby.SlowSpeedFactor, precision: 3);
    }

    [Fact]
    public void BlastCore_SplashesNearbyEnemy_OnSecondaryHit()
    {
        // isChain=true (secondary/accordion hit): Blast Core DOES fire (proc spaghetti).
        // Nearby enemy within radius takes splash damage.
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby  = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(50f, 0f) };
        var enemies = new List<IEnemyView> { target, nearby };
        var mod     = new BlastCore(Def("blast_core"));
        tower.Modifiers.Add(mod);

        var ctx = Ctx(tower, target, enemies, isChain: true);
        ctx.FinalDamage = tower.BaseDamage;

        DamageModel.Apply(ctx);

        float expectedSplash = 20f * Balance.BlastCoreDamageRatio;
        Assert.Equal(100f - expectedSplash, nearby.Hp, precision: 2);
        Assert.Equal(expectedSplash, ctx.SplashDamageDealt, precision: 2);
    }

    [Fact]
    public void BlastCore_SplashDamageDealt_IsNonZero_OnSecondaryHit()
    {
        // SplashDamageDealt is populated for isChain=true hits (proc spaghetti).
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var nearby  = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(50f, 0f) };
        var enemies = new List<IEnemyView> { target, nearby };
        tower.Modifiers.Add(new BlastCore(Def("blast_core")));

        var ctx = Ctx(tower, target, enemies, isChain: true);
        ctx.FinalDamage = tower.BaseDamage;

        DamageModel.Apply(ctx);

        float expectedSplash = 20f * Balance.BlastCoreDamageRatio;
        Assert.Equal(expectedSplash, ctx.SplashDamageDealt, precision: 2);
    }

    [Fact]
    public void BlastCore_EnemyOutsideSplashRadius_NotDamaged_OnPrimaryHit()
    {
        // Enemy beyond BlastCoreRadius (140px): should not take splash even on isChain=false.
        var tower   = new FakeTower { BaseDamage = 20f };
        var target  = new FakeEnemy { Hp = 100f, GlobalPosition = Vector2.Zero };
        var farAway = new FakeEnemy { Hp = 100f, GlobalPosition = new Vector2(200f, 0f) }; // > 140px
        var enemies = new List<IEnemyView> { target, farAway };
        tower.Modifiers.Add(new BlastCore(Def("blast_core")));

        var ctx = Ctx(tower, target, enemies, isChain: false);
        ctx.FinalDamage = tower.BaseDamage;

        DamageModel.Apply(ctx);

        Assert.Equal(100f, farAway.Hp, precision: 2);
    }
}
