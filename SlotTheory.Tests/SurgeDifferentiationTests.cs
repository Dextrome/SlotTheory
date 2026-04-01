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

    // ResolveLabel now returns feel-type labels: PRESSURE SURGE / CHAIN SURGE / DETONATION SURGE.
    // Per-mod labels were removed when the feel-differentiation system was introduced.
    [Theory]
    [InlineData(SpectacleDefinitions.Momentum,        "PRESSURE SURGE")]
    [InlineData(SpectacleDefinitions.Overkill,        "DETONATION SURGE")]
    [InlineData(SpectacleDefinitions.ExploitWeakness, "CHAIN SURGE")]
    [InlineData(SpectacleDefinitions.FocusLens,       "DETONATION SURGE")]
    [InlineData(SpectacleDefinitions.ChillShot,       "PRESSURE SURGE")]
    [InlineData(SpectacleDefinitions.Overreach,       "PRESSURE SURGE")]
    [InlineData(SpectacleDefinitions.HairTrigger,     "DETONATION SURGE")]
    [InlineData(SpectacleDefinitions.SplitShot,       "CHAIN SURGE")]
    [InlineData(SpectacleDefinitions.FeedbackLoop,    "DETONATION SURGE")]
    [InlineData(SpectacleDefinitions.ChainReaction,   "CHAIN SURGE")]
    public void ResolveLabel_KnownModId_ReturnsExpectedLabel(string modId, string expected)
    {
        string label = SurgeDifferentiation.ResolveLabel(new[] { modId });
        Assert.Equal(expected, label);
    }

    [Fact]
    public void ResolveLabel_EmptyArray_ReturnsDefaultLabel()
    {
        // Default feel is Neutral, which maps to "CHAIN SURGE".
        Assert.Equal("CHAIN SURGE", SurgeDifferentiation.ResolveLabel(Array.Empty<string>()));
    }

    [Fact]
    public void ResolveLabel_UnknownModId_ReturnsDefaultLabel()
    {
        Assert.Equal("CHAIN SURGE", SurgeDifferentiation.ResolveLabel(new[] { "unknown_mod_xyz" }));
    }

    [Fact]
    public void ResolveLabel_UsesFirstEntryOnly()
    {
        // Label is driven by [0]; FocusLens is Detonation, Momentum is Pressure.
        string[] mods = { SpectacleDefinitions.FocusLens, SpectacleDefinitions.Momentum };
        Assert.Equal("DETONATION SURGE", SurgeDifferentiation.ResolveLabel(mods));
    }

    [Fact]
    public void ResolveLabel_AllSupportedMods_ReturnOneOfThreeTypes()
    {
        var supported = new[]
        {
            SpectacleDefinitions.Momentum, SpectacleDefinitions.Overkill,
            SpectacleDefinitions.ExploitWeakness, SpectacleDefinitions.FocusLens,
            SpectacleDefinitions.ChillShot, SpectacleDefinitions.Overreach,
            SpectacleDefinitions.HairTrigger, SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.FeedbackLoop, SpectacleDefinitions.ChainReaction,
        };
        var validLabels = new[] { "PRESSURE SURGE", "CHAIN SURGE", "DETONATION SURGE" };
        foreach (string mod in supported)
        {
            string label = SurgeDifferentiation.ResolveLabel(new[] { mod });
            Assert.Contains(label, validLabels);
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
        // towerA contributes Momentum surges (2×)
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

        // Two Momentum surges from towerA
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
        // Detonation mods produce "DETONATION SURGE"
        string[] detonationMods = { SpectacleDefinitions.FocusLens, SpectacleDefinitions.Overkill,
                                    SpectacleDefinitions.FeedbackLoop, SpectacleDefinitions.HairTrigger };
        foreach (string mod in detonationMods)
        {
            Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Detonation,
                SurgeDifferentiation.ResolveFeel(new[] { mod }));
            Assert.Equal("DETONATION SURGE", SurgeDifferentiation.ResolveLabel(new[] { mod }));
        }

        // Pressure mods produce "PRESSURE SURGE"
        string[] pressureMods = { SpectacleDefinitions.ChillShot, SpectacleDefinitions.Overreach,
                                   SpectacleDefinitions.Momentum };
        foreach (string mod in pressureMods)
        {
            Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Pressure,
                SurgeDifferentiation.ResolveFeel(new[] { mod }));
            Assert.Equal("PRESSURE SURGE", SurgeDifferentiation.ResolveLabel(new[] { mod }));
        }
    }

    [Fact]
    public void ResolveLabel_NullArray_ReturnsDefaultLabel()
    {
        // Defensive: null should not throw, should return the Neutral default ("CHAIN SURGE").
        Assert.Equal("CHAIN SURGE", SurgeDifferentiation.ResolveLabel(null!));
    }

    [Fact]
    public void ResolveFeel_NullArray_ReturnsNeutral()
    {
        Assert.Equal(SurgeDifferentiation.GlobalSurgeFeel.Neutral, SurgeDifferentiation.ResolveFeel(null!));
    }

    // ── TowerSurgeCategory resolution ─────────────────────────────────────────

    [Fact]
    public void ResolveTowerSurgeCategory_ChainReactionOnChainTower_ReturnsSpread()
    {
        var sig = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: SpectacleDefinitions.ChainReaction,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 1f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);
        var cat = SurgeDifferentiation.ResolveTowerSurgeCategory("chain_tower", sig);
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Spread, cat);
    }

    [Fact]
    public void ResolveTowerSurgeCategory_OverkillOnHeavyCannon_ReturnsBurst()
    {
        var sig = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: SpectacleDefinitions.Overkill,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 1f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);
        var cat = SurgeDifferentiation.ResolveTowerSurgeCategory("heavy_cannon", sig);
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Burst, cat);
    }

    [Fact]
    public void ResolveTowerSurgeCategory_SlowOnRiftPrism_ReturnsControl()
    {
        var sig = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: SpectacleDefinitions.ChillShot,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 1f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);
        var cat = SurgeDifferentiation.ResolveTowerSurgeCategory("rift_prism", sig);
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Control, cat);
    }

    [Fact]
    public void ResolveTowerSurgeCategory_AfterimageOnRapidShooter_ReturnsEcho()
    {
        var sig = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: SpectacleDefinitions.Afterimage,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 1f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);
        var cat = SurgeDifferentiation.ResolveTowerSurgeCategory("rapid_shooter", sig);
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Echo, cat);
    }

    [Fact]
    public void ResolveTowerSurgeCategory_NoMods_UsesTowerBiasOnly()
    {
        var emptyMods = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: string.Empty,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 0f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);

        // Tower bias alone should win with no mods
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Burst,
            SurgeDifferentiation.ResolveTowerSurgeCategory("heavy_cannon", emptyMods));
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Spread,
            SurgeDifferentiation.ResolveTowerSurgeCategory("chain_tower", emptyMods));
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Control,
            SurgeDifferentiation.ResolveTowerSurgeCategory("accordion_engine", emptyMods));
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Echo,
            SurgeDifferentiation.ResolveTowerSurgeCategory("latch_nest", emptyMods));
    }

    [Fact]
    public void ResolveTowerSurgeCategory_UnknownTowerId_StillResolvesFromMods()
    {
        var sig = new SpectacleSignature(
            Mode: SpectacleMode.Single,
            PrimaryModId: SpectacleDefinitions.Overkill,
            SecondaryModId: string.Empty,
            TertiaryModId: string.Empty,
            PrimaryShare: 1f, SecondaryShare: 0f, TertiaryShare: 0f,
            SurgePower: 1f, AugmentStrength: 0f,
            EffectId: string.Empty, EffectName: string.Empty,
            ComboEffectId: string.Empty, ComboEffectName: string.Empty,
            AugmentEffectId: string.Empty, AugmentName: string.Empty);
        // Unknown tower has no bias, so overkill (Burst 2.5) should win
        var cat = SurgeDifferentiation.ResolveTowerSurgeCategory("unknown_tower_xyz", sig);
        Assert.Equal(SurgeDifferentiation.TowerSurgeCategory.Burst, cat);
    }

    [Fact]
    public void GetCategoryMaxLinks_SpreadIsHigherThanControlIsHigherThanBase()
    {
        int spread  = SurgeDifferentiation.GetCategoryMaxLinks(SurgeDifferentiation.TowerSurgeCategory.Spread);
        int control = SurgeDifferentiation.GetCategoryMaxLinks(SurgeDifferentiation.TowerSurgeCategory.Control);
        int burst   = SurgeDifferentiation.GetCategoryMaxLinks(SurgeDifferentiation.TowerSurgeCategory.Burst);

        Assert.True(spread > burst, $"Spread ({spread}) should have more links than Burst ({burst})");
        Assert.True(control < burst, $"Control ({control}) should have fewer links than Burst ({burst})");
        Assert.True(spread > 0 && control > 0);
    }

    [Fact]
    public void GetCategoryFlashAlpha_BurstIsHighest_ControlIsLowest()
    {
        float burst   = SurgeDifferentiation.GetCategoryFlashAlpha(SurgeDifferentiation.TowerSurgeCategory.Burst);
        float spread  = SurgeDifferentiation.GetCategoryFlashAlpha(SurgeDifferentiation.TowerSurgeCategory.Spread);
        float echo    = SurgeDifferentiation.GetCategoryFlashAlpha(SurgeDifferentiation.TowerSurgeCategory.Echo);
        float control = SurgeDifferentiation.GetCategoryFlashAlpha(SurgeDifferentiation.TowerSurgeCategory.Control);

        Assert.True(burst > spread,  $"Burst ({burst:F3}) should be > Spread ({spread:F3})");
        Assert.True(control < echo,  $"Control ({control:F3}) should be < Echo ({echo:F3})");
        Assert.True(control > 0f && burst < 1f);
    }
}
