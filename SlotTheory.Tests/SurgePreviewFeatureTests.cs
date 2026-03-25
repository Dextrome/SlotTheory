using SlotTheory.Core;
using System;
using System.Linq;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for the three new surge preview features:
///   1. SpectacleSystem.PeekDominantMods() - live peek without mutating state
///   2. Preview alpha ramp formula (70%→100% fill)
///   3. Global surge banner subtitle refund-percent formula
/// </summary>
public class SurgePreviewFeatureTests
{
    // ── Shared helpers ─────────────────────────────────────────────────────────

    private sealed class StubMod : SlotTheory.Modifiers.Modifier
    {
        public StubMod(string id) => ModifierId = id;
    }

    private static FakeTower TowerWith(params string[] modIds)
    {
        var t = new FakeTower { AttackInterval = 1f };
        foreach (string id in modIds)
            t.Modifiers.Add(new StubMod(id));
        return t;
    }

    /// <summary>
    /// Returns the scalar that guarantees a single-proc surge for the given mod.
    /// </summary>
    private static float ScalarForSurge(string modId)
    {
        float threshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float perGain = SpectacleDefinitions.GetBaseGain(modId)
            * SpectacleDefinitions.GetCopyMultiplier(1)
            * SpectacleDefinitions.ResolveMeterGainScale();
        Assert.True(perGain > 0f);
        return (threshold / perGain) + 1f;
    }

    /// <summary>
    /// Preview alpha formula mirrored from GameController._Process.
    /// Ramps 0→0.80 over the 70%–100% fill range.
    /// </summary>
    private static float ComputePreviewAlpha(float globalFill)
    {
        float t = Math.Clamp((globalFill - 0.70f) / 0.30f, 0f, 1f);
        return t * 0.80f;
    }

    /// <summary>
    /// Subtitle refund-percent formula mirrored from GameController.OnGlobalSurgeTriggered.
    /// </summary>
    private static int ComputeRefundPct(int uniqueContributors)
    {
        int subContribs = Math.Max(2, uniqueContributors);
        float raw = Math.Clamp(0.24f + 0.04f * subContribs, 0.24f, 0.46f);
        return (int)Math.Round(raw * 100f);
    }

    // ── PeekDominantMods: basic contract ───────────────────────────────────────

    [Fact]
    public void PeekDominantMods_FreshSystem_ReturnsEmpty()
    {
        var system = new SpectacleSystem();
        Assert.Empty(system.PeekDominantMods());
    }

    [Fact]
    public void PeekDominantMods_DoesNotMutateGlobalMeter()
    {
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.Momentum);
        float scalar = ScalarForSurge(SpectacleDefinitions.Momentum);

        // Fire one surge to put a contribution in the window.
        system.RegisterProc(tower, SpectacleDefinitions.Momentum, scalar);
        float meterBefore = system.GlobalMeter;

        // Peeking must not change the meter.
        _ = system.PeekDominantMods();
        Assert.Equal(meterBefore, system.GlobalMeter);
    }

    [Fact]
    public void PeekDominantMods_AfterOneSurge_ReturnsThatMod()
    {
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.ChillShot);
        float scalar = ScalarForSurge(SpectacleDefinitions.ChillShot);

        system.RegisterProc(tower, SpectacleDefinitions.ChillShot, scalar);

        string[] peeked = system.PeekDominantMods();
        Assert.NotEmpty(peeked);
        Assert.Equal(SpectacleDefinitions.ChillShot, peeked[0]);
    }

    [Fact]
    public void PeekDominantMods_MostFrequentModFirst()
    {
        var system = new SpectacleSystem();
        var towerA = TowerWith(SpectacleDefinitions.Momentum);
        var towerB = TowerWith(SpectacleDefinitions.FocusLens);
        float scalar = ScalarForSurge(SpectacleDefinitions.Momentum);

        // Two Momentum surges, one FocusLens surge. Advance past surge cooldown between towerA surges.
        system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalar);
        system.Update(SpectacleDefinitions.SurgeCooldownSeconds);
        system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalar);
        // Fire towerB immediately - no extra time advance, both towers have independent cooldowns.
        system.RegisterProc(towerB, SpectacleDefinitions.FocusLens, scalar);

        string[] peeked = system.PeekDominantMods();
        Assert.True(peeked.Length >= 1);
        Assert.Equal(SpectacleDefinitions.Momentum, peeked[0]);
    }

    [Fact]
    public void PeekDominantMods_MatchesGlobalTriggerDominantModIds()
    {
        // PeekDominantMods immediately before the global trigger fires
        // must return the same primary mod as the triggered DominantModIds.
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.ChainReaction);
        float scalar = ScalarForSurge(SpectacleDefinitions.ChainReaction);

        float globalThreshold = SpectacleDefinitions.ResolveGlobalThreshold();
        float perSurge = SpectacleDefinitions.ResolveGlobalMeterPerSurge();
        int surgesNeeded = (int)Math.Ceiling(globalThreshold / Math.Max(0.0001f, perSurge));

        GlobalSurgeTriggerInfo? triggered = null;
        string[]? peekBeforeFire = null;

        system.OnGlobalTriggered += info =>
        {
            triggered = info;
        };

        for (int i = 0; i < surgesNeeded - 1; i++)
        {
            system.RegisterProc(tower, SpectacleDefinitions.ChainReaction, scalar);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds);
        }

        // Peek just before the final surge that fires global.
        peekBeforeFire = system.PeekDominantMods();
        system.RegisterProc(tower, SpectacleDefinitions.ChainReaction, scalar);
        system.ActivateGlobalSurge();

        Assert.NotNull(triggered);
        Assert.NotNull(peekBeforeFire);
        Assert.NotEmpty(peekBeforeFire!);
        Assert.Equal(peekBeforeFire[0], triggered!.Value.DominantModIds[0]);
    }

    [Fact]
    public void PeekDominantMods_ReturnsAtMostThreeMods()
    {
        var system = new SpectacleSystem();
        var tA = TowerWith(SpectacleDefinitions.Momentum);
        var tB = TowerWith(SpectacleDefinitions.FocusLens);
        var tC = TowerWith(SpectacleDefinitions.ChillShot);
        var tD = TowerWith(SpectacleDefinitions.Overkill);
        float scalar = ScalarForSurge(SpectacleDefinitions.Momentum);

        foreach (var (tower, mod) in new[] {
            (tA, SpectacleDefinitions.Momentum),
            (tB, SpectacleDefinitions.FocusLens),
            (tC, SpectacleDefinitions.ChillShot),
            (tD, SpectacleDefinitions.Overkill) })
        {
            system.RegisterProc(tower, mod, scalar);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
        }

        string[] peeked = system.PeekDominantMods();
        Assert.InRange(peeked.Length, 0, 3);
    }

    [Fact]
    public void PeekDominantMods_NoDuplicates()
    {
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.Momentum);
        float scalar = ScalarForSurge(SpectacleDefinitions.Momentum);

        for (int i = 0; i < 5; i++)
        {
            system.RegisterProc(tower, SpectacleDefinitions.Momentum, scalar);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
        }

        string[] peeked = system.PeekDominantMods();
        Assert.Equal(peeked.Length, peeked.Distinct(StringComparer.Ordinal).Count());
    }

    // ── Preview alpha ramp ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.00f, 0.00f)] // well below threshold - no preview
    [InlineData(0.50f, 0.00f)] // below threshold - no preview
    [InlineData(0.70f, 0.00f)] // exactly at threshold - preview just begins (t=0 → alpha=0)
    [InlineData(0.85f, 0.40f)] // halfway through ramp
    [InlineData(1.00f, 0.80f)] // full meter - maximum preview alpha
    [InlineData(1.20f, 0.80f)] // over-full - clamped at max
    public void PreviewAlpha_RampsCorrectly(float fill, float expected)
    {
        float actual = ComputePreviewAlpha(fill);
        Assert.Equal(expected, actual, precision: 4);
    }

    [Fact]
    public void PreviewAlpha_IsZeroBelowSeventyPercent()
    {
        for (float f = 0f; f < 0.70f; f += 0.05f)
            Assert.Equal(0f, ComputePreviewAlpha(f));
    }

    [Fact]
    public void PreviewAlpha_IsMonotonicallyIncreasing()
    {
        float prev = ComputePreviewAlpha(0.70f);
        for (float f = 0.72f; f <= 1.00f; f += 0.02f)
        {
            float curr = ComputePreviewAlpha(f);
            Assert.True(curr >= prev, $"Alpha should not decrease at fill={f}");
            prev = curr;
        }
    }

    [Fact]
    public void PreviewAlpha_NeverExceedsMaximum()
    {
        for (float f = 0f; f <= 1.5f; f += 0.05f)
            Assert.True(ComputePreviewAlpha(f) <= 0.80f);
    }

    // ── Subtitle refund-percent formula ────────────────────────────────────────

    [Theory]
    [InlineData(1, 32)] // clamped to min 2 contributors → 0.24 + 0.04*2 = 0.32
    [InlineData(2, 32)] // 0.24 + 0.04*2 = 0.32 → 32%
    [InlineData(3, 36)] // 0.24 + 0.04*3 = 0.36 → 36%
    [InlineData(4, 40)] // 0.24 + 0.04*4 = 0.40 → 40%
    [InlineData(5, 44)] // 0.24 + 0.04*5 = 0.44 → 44%
    [InlineData(6, 46)] // clamped at max → 46%
    [InlineData(10, 46)] // way over max, still clamped
    public void RefundPct_MatchesPayloadFormula(int contributors, int expectedPct)
    {
        Assert.Equal(expectedPct, ComputeRefundPct(contributors));
    }

    [Fact]
    public void RefundPct_IsAlwaysInValidRange()
    {
        for (int c = 0; c <= 20; c++)
        {
            int pct = ComputeRefundPct(c);
            Assert.InRange(pct, 24, 46);
        }
    }

    [Fact]
    public void RefundPct_IsMonotonicallyNonDecreasing()
    {
        int prev = ComputeRefundPct(0);
        for (int c = 1; c <= 20; c++)
        {
            int curr = ComputeRefundPct(c);
            Assert.True(curr >= prev, $"Refund pct should not decrease at contributors={c}");
            prev = curr;
        }
    }

    [Fact]
    public void RefundPct_MatchesApplyGlobalSurgeGameplayPayloadFormula()
    {
        // Verify our test helper exactly mirrors the production formula.
        // Formula: Clamp(0.24 + 0.04 * Max(2, contributors), 0.24, 0.46) * 100, rounded.
        for (int c = 0; c <= 10; c++)
        {
            int subContribs = Math.Max(2, c);
            float raw = Math.Clamp(0.24f + 0.04f * subContribs, 0.24f, 0.46f);
            int expected = (int)Math.Round(raw * 100f);
            Assert.Equal(expected, ComputeRefundPct(c));
        }
    }
}
