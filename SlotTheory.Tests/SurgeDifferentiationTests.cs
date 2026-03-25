using SlotTheory.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for the surge differentiation system (Phases 1–5).
/// Covers: dynamic label resolution, feel classification, flash alpha,
/// and DominantModIds propagation from SpectacleSystem.
/// </summary>
public class SurgeDifferentiationTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

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
    /// Fire enough surges on <paramref name="tower"/> to fill the global meter once.
    /// Returns the GlobalSurgeTriggerInfo that was fired, or null if it didn't fire.
    /// </summary>
    private static GlobalSurgeTriggerInfo? DriveToGlobalSurge(
        SpectacleSystem system,
        FakeTower tower,
        string procModId)
    {
        GlobalSurgeTriggerInfo? result = null;
        system.OnGlobalTriggered += info => result = info;

        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float globalThreshold = SpectacleDefinitions.ResolveGlobalThreshold();
        float globalPerSurge = SpectacleDefinitions.ResolveGlobalMeterPerSurge();
        int requiredSurges = (int)Math.Ceiling(globalThreshold / Math.Max(0.0001f, globalPerSurge));

        float baseGain = SpectacleDefinitions.GetBaseGain(procModId);
        float copy = SpectacleDefinitions.GetCopyMultiplier(1);
        float scale = SpectacleDefinitions.ResolveMeterGainScale();
        float perScalarGain = baseGain * copy * scale;
        float scalarForOneSurge = (surgeThreshold / perScalarGain) + 1f;

        for (int i = 0; i < requiredSurges; i++)
        {
            system.RegisterProc(tower, procModId, scalarForOneSurge);
            if (i < requiredSurges - 1)
                system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
        }

        system.ActivateGlobalSurge();
        return result;
    }

    // ── SurgeDifferentiation.ResolveLabel ──────────────────────────────────────

    [Theory]
    [InlineData(SpectacleDefinitions.Momentum,        "MOMENTUM SURGE")]
    [InlineData(SpectacleDefinitions.Overkill,        "OVERKILL SURGE")]
    [InlineData(SpectacleDefinitions.ExploitWeakness, "EXPLOIT SURGE")]
    [InlineData(SpectacleDefinitions.FocusLens,       "FOCUS SURGE")]
    [InlineData(SpectacleDefinitions.ChillShot,       "CHILL SURGE")]
    [InlineData(SpectacleDefinitions.Overreach,       "OVERREACH SURGE")]
    [InlineData(SpectacleDefinitions.HairTrigger,     "HAIR TRIGGER SURGE")]
    [InlineData(SpectacleDefinitions.SplitShot,       "SPLIT SHOT SURGE")]
    [InlineData(SpectacleDefinitions.FeedbackLoop,    "FEEDBACK SURGE")]
    [InlineData(SpectacleDefinitions.ChainReaction,   "CHAIN SURGE")]
    public void ResolveLabel_KnownModId_ReturnsExpectedLabel(string modId, string expected)
    {
        string label = SurgeDifferentiation.ResolveLabel(new[] { modId });
        Assert.Equal(expected, label);
    }

    [Fact]
    public void ResolveLabel_EmptyArray_ReturnsDefaultLabel()
    {
        Assert.Equal("GLOBAL SURGE", SurgeDifferentiation.ResolveLabel(Array.Empty<string>()));
    }

    [Fact]
    public void ResolveLabel_UnknownModId_ReturnsDefaultLabel()
    {
        Assert.Equal("GLOBAL SURGE", SurgeDifferentiation.ResolveLabel(new[] { "unknown_mod_xyz" }));
    }

    [Fact]
    public void ResolveLabel_UsesFirstEntryOnly()
    {
        // Even if the array has multiple entries, label is driven by [0].
        string[] mods = { SpectacleDefinitions.FocusLens, SpectacleDefinitions.Momentum };
        Assert.Equal("FOCUS SURGE", SurgeDifferentiation.ResolveLabel(mods));
    }

    [Fact]
    public void ResolveLabel_AllSupportedMods_NoneReturnDefaultLabel()
    {
        var supported = new[]
        {
            SpectacleDefinitions.Momentum, SpectacleDefinitions.Overkill,
            SpectacleDefinitions.ExploitWeakness, SpectacleDefinitions.FocusLens,
            SpectacleDefinitions.ChillShot, SpectacleDefinitions.Overreach,
            SpectacleDefinitions.HairTrigger, SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.FeedbackLoop, SpectacleDefinitions.ChainReaction,
        };
        foreach (string mod in supported)
        {
            string label = SurgeDifferentiation.ResolveLabel(new[] { mod });
            Assert.NotEqual("GLOBAL SURGE", label);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    // ── SurgeDifferentiation.ResolveFeel ───────────────────────────────────────

    [Theory]
    [InlineData(SpectacleDefinitions.FocusLens)]
    [InlineData(SpectacleDefinitions.Overkill)]
    [InlineData(SpectacleDefinitions.FeedbackLoop)]
    [InlineData(SpectacleDefinitions.HairTrigger)]
    public void ResolveFeel_DetonationMods_ReturnDetonation(string modId)
    {
        var feel = SurgeDifferentiation.ResolveFeel(new[] { modId });
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Detonation, feel);
    }

    [Theory]
    [InlineData(SpectacleDefinitions.ChillShot)]
    [InlineData(SpectacleDefinitions.Overreach)]
    [InlineData(SpectacleDefinitions.Momentum)]
    public void ResolveFeel_PressureMods_ReturnPressure(string modId)
    {
        var feel = SurgeDifferentiation.ResolveFeel(new[] { modId });
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Pressure, feel);
    }

    [Theory]
    [InlineData(SpectacleDefinitions.ExploitWeakness)]
    [InlineData(SpectacleDefinitions.SplitShot)]
    [InlineData(SpectacleDefinitions.ChainReaction)]
    public void ResolveFeel_NeutralMods_ReturnNeutral(string modId)
    {
        var feel = SurgeDifferentiation.ResolveFeel(new[] { modId });
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Neutral, feel);
    }

    [Fact]
    public void ResolveFeel_EmptyArray_ReturnsNeutral()
    {
        var feel = SurgeDifferentiation.ResolveFeel(Array.Empty<string>());
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Neutral, feel);
    }

    [Fact]
    public void ResolveFeel_UnknownModId_ReturnsNeutral()
    {
        var feel = SurgeDifferentiation.ResolveFeel(new[] { "mystery_mod" });
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Neutral, feel);
    }

    // ── SurgeDifferentiation.ResolveFlashAlpha ─────────────────────────────────

    [Fact]
    public void ResolveFlashAlpha_DetonationIsHigherThanNeutralIsHigherThanPressure()
    {
        float detonation = SurgeDifferentiation.ResolveFlashAlpha(SurgeDifferentiation.GlobalSurgeFeel.Detonation);
        float neutral    = SurgeDifferentiation.ResolveFlashAlpha(SurgeDifferentiation.GlobalSurgeFeel.Neutral);
        float pressure   = SurgeDifferentiation.ResolveFlashAlpha(SurgeDifferentiation.GlobalSurgeFeel.Pressure);

        Assert.True(detonation > neutral, $"Detonation ({detonation}) should be > Neutral ({neutral})");
        Assert.True(neutral    > pressure, $"Neutral ({neutral}) should be > Pressure ({pressure})");
        Assert.True(pressure   > 0f);
        Assert.True(detonation < 1f);
    }

    // ── GlobalSurgeTriggerInfo.DominantModIds (SpectacleSystem integration) ───

    [Fact]
    public void GlobalTrigger_DominantModIds_SingleTowerSingleMod_ReturnsOneMod()
    {
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.Momentum);

        GlobalSurgeTriggerInfo? triggered = DriveToGlobalSurge(system, tower, SpectacleDefinitions.Momentum);

        Assert.NotNull(triggered);
        Assert.NotNull(triggered!.Value.DominantModIds);
        string singleMod = Assert.Single(triggered.Value.DominantModIds);
        Assert.Equal(SpectacleDefinitions.Momentum, singleMod);
    }

    [Fact]
    public void GlobalTrigger_DominantModIds_MostFrequentModComesFirst()
    {
        var system = new SpectacleSystem();
        // towerA contributes momentum surges (2×)
        var towerA = TowerWith(SpectacleDefinitions.Momentum);
        // towerB contributes chain_reaction surge (1×)
        var towerB = TowerWith(SpectacleDefinitions.ChainReaction);

        GlobalSurgeTriggerInfo? result = null;
        system.OnGlobalTriggered += info => result = info;

        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float baseGain = SpectacleDefinitions.GetBaseGain(SpectacleDefinitions.Momentum);
        float copy = SpectacleDefinitions.GetCopyMultiplier(1);
        float scale = SpectacleDefinitions.ResolveMeterGainScale();
        float scalarForSurge = (surgeThreshold / (baseGain * copy * scale)) + 1f;

        // Two momentum surges from towerA
        system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalarForSurge);
        system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
        system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalarForSurge);
        system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);

        // One chain_reaction surge from towerB (may trigger global here)
        system.RegisterProc(towerB, SpectacleDefinitions.ChainReaction, scalarForSurge);

        // If global didn't fire yet, keep going until it does
        if (!system.IsGlobalSurgeReady)
        {
            for (int i = 0; i < 20 && !system.IsGlobalSurgeReady; i++)
            {
                system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
                system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalarForSurge);
            }
        }

        system.ActivateGlobalSurge();
        Assert.NotNull(result);
        Assert.NotNull(result!.Value.DominantModIds);
        Assert.True(result.Value.DominantModIds.Length >= 1);
        // Momentum contributed more surges, should be first
        Assert.Equal(SpectacleDefinitions.Momentum, result.Value.DominantModIds[0]);
    }

    [Fact]
    public void GlobalTrigger_DominantModIds_UpToThreeDistinctMods()
    {
        var system = new SpectacleSystem();
        var towerA = TowerWith(SpectacleDefinitions.Momentum);
        var towerB = TowerWith(SpectacleDefinitions.ChainReaction);
        var towerC = TowerWith(SpectacleDefinitions.FocusLens);

        GlobalSurgeTriggerInfo? result = null;
        system.OnGlobalTriggered += info => result = info;

        float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float baseGain = SpectacleDefinitions.GetBaseGain(SpectacleDefinitions.Momentum);
        float scalarForSurge = (surgeThreshold / (baseGain * SpectacleDefinitions.GetCopyMultiplier(1)
            * SpectacleDefinitions.ResolveMeterGainScale())) + 1f;

        for (int i = 0; i < 30 && !system.IsGlobalSurgeReady; i++)
        {
            system.RegisterProc(towerA, SpectacleDefinitions.Momentum, scalarForSurge);
            system.RegisterProc(towerB, SpectacleDefinitions.ChainReaction, scalarForSurge);
            system.RegisterProc(towerC, SpectacleDefinitions.FocusLens, scalarForSurge);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
        }

        system.ActivateGlobalSurge();
        Assert.NotNull(result);
        var mods = result!.Value.DominantModIds;
        Assert.NotNull(mods);
        Assert.InRange(mods.Length, 1, 3);
        // All entries should be valid mod IDs
        foreach (string modId in mods)
            Assert.True(SpectacleDefinitions.IsSupported(modId), $"Unknown modId in DominantModIds: {modId}");
        // No duplicates
        Assert.Equal(mods.Length, mods.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GlobalTrigger_DominantModIds_NeverNull()
    {
        var system = new SpectacleSystem();
        var tower = TowerWith(SpectacleDefinitions.SplitShot);

        GlobalSurgeTriggerInfo? result = DriveToGlobalSurge(system, tower, SpectacleDefinitions.SplitShot);

        Assert.NotNull(result);
        Assert.NotNull(result!.Value.DominantModIds);
    }

    [Fact]
    public void GlobalTrigger_DominantModIds_ReflectsFullCyclePattern()
    {
        SpectacleTuning.Reset();
        try
        {
            // Lower threshold so the global fires after two surges.
            SpectacleTuning.Apply(new SpectacleTuningProfile
            {
                GlobalThresholdMultiplier = 0.10f,
            }, "test");

            var system = new SpectacleSystem();
            var tower = TowerWith(SpectacleDefinitions.Momentum);

            GlobalSurgeTriggerInfo? result = null;
            system.OnGlobalTriggered += info => result = info;

            float surgeThreshold = SpectacleDefinitions.ResolveSurgeThreshold();
            float baseGain = SpectacleDefinitions.GetBaseGain(SpectacleDefinitions.Momentum);
            float scalarForSurge = (surgeThreshold / (baseGain * SpectacleDefinitions.GetCopyMultiplier(1)
                * SpectacleDefinitions.ResolveMeterGainScale())) + 1f;

            // Fire surge, advance time (contributions no longer expire), fire second surge to arm global.
            system.RegisterProc(tower, SpectacleDefinitions.Momentum, scalarForSurge);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.1f);
            system.RegisterProc(tower, SpectacleDefinitions.Momentum, scalarForSurge);
            system.ActivateGlobalSurge();

            // Both surges contributed Momentum during the cycle - it must appear as dominant.
            Assert.NotNull(result);
            Assert.NotNull(result!.Value.DominantModIds);
            Assert.NotEmpty(result.Value.DominantModIds);
            Assert.Equal(SpectacleDefinitions.Momentum, result.Value.DominantModIds[0]);
        }
        finally
        {
            SpectacleTuning.Reset();
        }
    }

    // ── Feel + Label integration ───────────────────────────────────────────────

    [Fact]
    public void LabelAndFeel_AllSupportedMods_HaveConsistentCategories()
    {
        // Detonation mods should produce non-default labels
        string[] detonationMods = { SpectacleDefinitions.FocusLens, SpectacleDefinitions.Overkill,
                                    SpectacleDefinitions.FeedbackLoop, SpectacleDefinitions.HairTrigger };
        foreach (string mod in detonationMods)
        {
            Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Detonation,
                SurgeDifferentiation.ResolveFeel(new[] { mod }));
            Assert.NotEqual("GLOBAL SURGE", SurgeDifferentiation.ResolveLabel(new[] { mod }));
        }

        // Pressure mods should also have named labels and correct feel
        string[] pressureMods = { SpectacleDefinitions.ChillShot, SpectacleDefinitions.Overreach,
                                   SpectacleDefinitions.Momentum };
        foreach (string mod in pressureMods)
        {
            Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Pressure,
                SurgeDifferentiation.ResolveFeel(new[] { mod }));
            Assert.NotEqual("GLOBAL SURGE", SurgeDifferentiation.ResolveLabel(new[] { mod }));
        }
    }

    [Fact]
    public void ResolveLabel_NullArray_ReturnsDefaultLabel()
    {
        // Defensive: null should not throw, should return default
        Assert.Equal("GLOBAL SURGE", SurgeDifferentiation.ResolveLabel(null!));
    }

    [Fact]
    public void ResolveFeel_NullArray_ReturnsNeutral()
    {
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Neutral, SurgeDifferentiation.ResolveFeel(null!));
    }
}
