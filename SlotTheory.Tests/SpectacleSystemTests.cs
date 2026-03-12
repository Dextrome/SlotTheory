using SlotTheory.Core;
using SlotTheory.Modifiers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace SlotTheory.Tests;

public class SpectacleSystemTests
{
    private sealed class StubModifier : Modifier
    {
        public StubModifier(string id) => ModifierId = id;
    }

    private static readonly string[] OrderedSupportedMods =
    {
        SpectacleDefinitions.Momentum,
        SpectacleDefinitions.Overkill,
        SpectacleDefinitions.ExploitWeakness,
        SpectacleDefinitions.FocusLens,
        SpectacleDefinitions.ChillShot,
        SpectacleDefinitions.Overreach,
        SpectacleDefinitions.HairTrigger,
        SpectacleDefinitions.SplitShot,
        SpectacleDefinitions.FeedbackLoop,
        SpectacleDefinitions.ChainReaction,
    };

    private static FakeTower TowerWithMods(params string[] modifierIds)
    {
        var tower = new FakeTower { AttackInterval = 1f };
        foreach (string id in modifierIds)
            tower.Modifiers.Add(new StubModifier(id));
        return tower;
    }

    public static IEnumerable<object[]> ComboPairs()
    {
        for (int i = 0; i < OrderedSupportedMods.Length; i++)
        {
            for (int j = i + 1; j < OrderedSupportedMods.Length; j++)
                yield return new object[] { OrderedSupportedMods[i], OrderedSupportedMods[j] };
        }
    }

    public static IEnumerable<object[]> AugmentExpectations()
    {
        yield return new object[] { SpectacleDefinitions.Momentum, SpectacleAugmentKind.RampCap };
        yield return new object[] { SpectacleDefinitions.Overkill, SpectacleAugmentKind.SpillTransfer };
        yield return new object[] { SpectacleDefinitions.ExploitWeakness, SpectacleAugmentKind.MarkedVulnerability };
        yield return new object[] { SpectacleDefinitions.FocusLens, SpectacleAugmentKind.BeamBurst };
        yield return new object[] { SpectacleDefinitions.ChillShot, SpectacleAugmentKind.SlowIntensity };
        yield return new object[] { SpectacleDefinitions.Overreach, SpectacleAugmentKind.RangePulse };
        yield return new object[] { SpectacleDefinitions.HairTrigger, SpectacleAugmentKind.AttackSpeed };
        yield return new object[] { SpectacleDefinitions.SplitShot, SpectacleAugmentKind.SplitVolley };
        yield return new object[] { SpectacleDefinitions.FeedbackLoop, SpectacleAugmentKind.CooldownRefund };
        yield return new object[] { SpectacleDefinitions.ChainReaction, SpectacleAugmentKind.ChainBounces };
    }

    public static IEnumerable<object[]> AugmentModIds()
    {
        foreach (string modId in OrderedSupportedMods)
            yield return new object[] { modId };
    }

    private static SpectacleTriggerInfo TriggerOneSurge(SpectacleSystem system, FakeTower tower, string procModId, float eventScalar)
    {
        SpectacleTriggerInfo triggered = default;
        int surgeCount = 0;
        system.OnSurgeTriggered += info =>
        {
            surgeCount++;
            triggered = info;
        };

        system.RegisterProc(tower, procModId, eventScalar);

        Assert.Equal(1, surgeCount);
        return triggered;
    }

    private static SpectacleTriggerInfo TriggerTriadSurgeWithOrderedRoles(
        SpectacleSystem system,
        FakeTower tower,
        string primary,
        string secondary,
        string tertiary)
    {
        SpectacleTriggerInfo triggered = default;
        int surgeCount = 0;
        system.OnSurgeTriggered += info =>
        {
            surgeCount++;
            triggered = info;
        };

        // Build a deterministic contribution order without crossing lock threshold first.
        system.RegisterProc(tower, primary, 2.0f);
        system.RegisterProc(tower, secondary, 1.5f);
        system.RegisterProc(tower, tertiary, 1.0f);
        system.RegisterProc(tower, primary, 125f);

        Assert.Equal(1, surgeCount);
        return triggered;
    }

    private static (string Primary, string Secondary) PickCorePairExcluding(string excludedModId)
    {
        var cores = OrderedSupportedMods
            .Where(modId => !string.Equals(modId, excludedModId, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return (cores[0], cores[1]);
    }

    private static float ScalarToGuaranteeSingleProcSurge(string modId)
    {
        float threshold = SpectacleDefinitions.ResolveSurgeThreshold();
        float baseGain = SpectacleDefinitions.GetBaseGain(modId);
        float copy = SpectacleDefinitions.GetCopyMultiplier(1);
        float diversity = SpectacleDefinitions.GetDiversityMultiplier(1);
        float meterScale = SpectacleDefinitions.ResolveMeterGainScale();
        float damageScale = SpectacleDefinitions.ResolveDamageMeterMultiplier(eventDamage: -1f);

        float perScalarGain = baseGain * copy * diversity * meterScale * damageScale;
        Assert.True(perScalarGain > 0f);
        return (threshold / perScalarGain) + 1f;
    }

    private static IEnumerable<string[]> GenerateLoadoutsUpToThreeMods()
    {
        var current = new List<string>(capacity: 3);

        IEnumerable<string[]> GenerateOfSize(int size, int start)
        {
            if (current.Count == size)
            {
                yield return current.ToArray();
                yield break;
            }

            for (int i = start; i < OrderedSupportedMods.Length; i++)
            {
                current.Add(OrderedSupportedMods[i]);
                foreach (var loadout in GenerateOfSize(size, i))
                    yield return loadout;
                current.RemoveAt(current.Count - 1);
            }
        }

        foreach (var loadout in GenerateOfSize(1, 0))
            yield return loadout;
        foreach (var loadout in GenerateOfSize(2, 0))
            yield return loadout;
        foreach (var loadout in GenerateOfSize(3, 0))
            yield return loadout;
    }

    private static List<string> RunDeterminismTrace()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.SplitShot, SpectacleDefinitions.ChainReaction, SpectacleDefinitions.FeedbackLoop);
        var trace = new List<string>();

        system.OnSurgeTriggered += info =>
        {
            trace.Add(string.Join("|",
                info.Signature.Mode,
                info.Signature.PrimaryModId,
                info.Signature.SecondaryModId,
                info.Signature.TertiaryModId,
                info.Signature.EffectId,
                info.Signature.ComboEffectId,
                info.Signature.AugmentEffectId));
        };

        string[] cycle = { SpectacleDefinitions.SplitShot, SpectacleDefinitions.ChainReaction, SpectacleDefinitions.FeedbackLoop };
        for (int i = 0; i < 240; i++)
        {
            string mod = cycle[(i * 7 + 3) % cycle.Length];
            float scalar = 45f + (i % 11) * 8f;
            system.RegisterProc(tower, mod, scalar);
            system.Update(0.50f);
        }

        return trace;
    }

    private static IDictionary ReadPrivateStaticDictionary(string fieldName)
    {
        FieldInfo? field = typeof(SpectacleDefinitions).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);

        object? value = field!.GetValue(null);
        Assert.NotNull(value);

        return Assert.IsAssignableFrom<IDictionary>(value);
    }

    [Fact]
    public void CopyAndDiversityMultipliers_MatchPlannedTables()
    {
        Assert.Equal(0f, SpectacleDefinitions.GetCopyMultiplier(0), 3);
        Assert.Equal(1.00f, SpectacleDefinitions.GetCopyMultiplier(1), 3);
        Assert.Equal(1.92f, SpectacleDefinitions.GetCopyMultiplier(2), 3);
        Assert.Equal(2.70f, SpectacleDefinitions.GetCopyMultiplier(3), 3);

        Assert.Equal(1.00f, SpectacleDefinitions.GetDiversityMultiplier(1), 3);
        Assert.Equal(1.08f, SpectacleDefinitions.GetDiversityMultiplier(2), 3);
        Assert.Equal(1.16f, SpectacleDefinitions.GetDiversityMultiplier(3), 3);
    }

    [Fact]
    public void ComboDefinition_UsesCanonicalPairMapping()
    {
        SpectacleComboDef combo = SpectacleDefinitions.GetCombo("split_shot", "momentum");
        Assert.Equal("C_MOMENTUM_SPLIT", combo.EffectId);
        Assert.Equal("Escalating Barrage", combo.Name);
    }

    [Fact]
    public void PreviewSignature_DuplicateModGetsDoubleShareInCombo()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot", "split_shot", "slow");

        SpectacleSignature sig = system.PreviewSignature(tower);

        Assert.Equal(SpectacleMode.Combo, sig.Mode);
        Assert.Equal(SpectacleDefinitions.SplitShot, sig.PrimaryModId);
        Assert.Equal(SpectacleDefinitions.ChillShot, sig.SecondaryModId);
        Assert.Equal(2f / 3f, sig.PrimaryShare, 3);
        Assert.Equal(1f / 3f, sig.SecondaryShare, 3);
    }

    [Fact]
    public void DispatchLogic_SplitAndChain_UsesSplitChainComboCore()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.SplitShot, SpectacleDefinitions.ChainReaction);

        SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, SpectacleDefinitions.SplitShot, 125f);

        Assert.Equal(SpectacleMode.Combo, surge.Signature.Mode);
        Assert.Equal("C_SPLIT_CHAIN", surge.Signature.ComboEffectId);
        Assert.Equal("C_SPLIT_CHAIN", surge.Signature.EffectId);
    }

    [Fact]
    public void DispatchLogic_SplitChainFeedback_UsesSplitChainCoreAndFeedbackAugment()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(
            SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.ChainReaction,
            SpectacleDefinitions.FeedbackLoop);

        SpectacleTriggerInfo surge = TriggerTriadSurgeWithOrderedRoles(
            system,
            tower,
            primary: SpectacleDefinitions.SplitShot,
            secondary: SpectacleDefinitions.ChainReaction,
            tertiary: SpectacleDefinitions.FeedbackLoop);

        Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
        Assert.Equal("C_SPLIT_CHAIN", surge.Signature.ComboEffectId);
        Assert.Equal("T_AUG_FEEDBACK", surge.Signature.AugmentEffectId);
    }

    [Fact]
    public void DispatchLogic_TripleSplit_ResolvesToSplitSingleSurge()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(
            SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.SplitShot);

        SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, SpectacleDefinitions.SplitShot, 90f);
        SpectacleSingleDef expected = SpectacleDefinitions.GetSingle(SpectacleDefinitions.SplitShot);

        Assert.Equal(SpectacleMode.Single, surge.Signature.Mode);
        Assert.Equal(expected.EffectId, surge.Signature.EffectId);
        Assert.Equal(SpectacleDefinitions.SplitShot, surge.Signature.PrimaryModId);
    }

    [Theory]
    [MemberData(nameof(ComboPairs))]
    public void ComboDispatch_AllPairsTriggerExpectedComboCore(string modA, string modB)
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(modA, modB);

        SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, modA, 125f);
        SpectacleComboDef expected = SpectacleDefinitions.GetCombo(modA, modB);

        Assert.Equal(SpectacleMode.Combo, surge.Signature.Mode);
        Assert.Equal(expected.EffectId, surge.Signature.ComboEffectId);
        Assert.Equal(expected.Name, surge.Signature.ComboEffectName);
        Assert.Equal(expected.EffectId, surge.Signature.EffectId);
        Assert.True(string.IsNullOrEmpty(surge.Signature.AugmentEffectId));
    }

    [Fact]
    public void PermutationOrder_DoesNotChangeComboOrTriadDispatchResult()
    {
        const string comboA = SpectacleDefinitions.SplitShot;
        const string comboB = SpectacleDefinitions.ChainReaction;
        const string augment = SpectacleDefinitions.FeedbackLoop;

        SpectacleComboDef expectedCombo = SpectacleDefinitions.GetCombo(comboA, comboB);
        SpectacleTriadAugmentDef expectedAugment = SpectacleDefinitions.GetTriadAugment(augment);

        // Pair permutations.
        string[][] pairPermutations =
        {
            new[] { comboA, comboB },
            new[] { comboB, comboA },
        };

        foreach (string[] pair in pairPermutations)
        {
            var system = new SpectacleSystem();
            var tower = TowerWithMods(pair);
            SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, comboA, 125f);

            Assert.Equal(SpectacleMode.Combo, surge.Signature.Mode);
            Assert.Equal(expectedCombo.EffectId, surge.Signature.ComboEffectId);
            Assert.True(string.IsNullOrEmpty(surge.Signature.AugmentEffectId));
        }

        // Triad permutations.
        string[][] triadPermutations =
        {
            new[] { comboA, comboB, augment },
            new[] { comboB, comboA, augment },
            new[] { comboA, augment, comboB },
            new[] { augment, comboA, comboB },
        };

        foreach (string[] triad in triadPermutations)
        {
            var system = new SpectacleSystem();
            var tower = TowerWithMods(triad);
            SpectacleTriggerInfo surge = TriggerTriadSurgeWithOrderedRoles(system, tower, comboA, comboB, augment);

            Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
            Assert.Equal(expectedCombo.EffectId, surge.Signature.ComboEffectId);
            Assert.Equal(expectedAugment.EffectId, surge.Signature.AugmentEffectId);
            Assert.Equal($"{expectedCombo.EffectId}+{expectedAugment.EffectId}", surge.Signature.EffectId);
        }
    }

    [Fact]
    public void DefinitionCoverage_ComboAndAugmentTablesHaveExpectedCounts()
    {
        IDictionary combos = ReadPrivateStaticDictionary("ComboDefs");
        IDictionary augments = ReadPrivateStaticDictionary("TriadAugments");

        Assert.Equal(45, combos.Count);
        Assert.Equal(10, augments.Count);

        var comboIds = combos.Values.Cast<SpectacleComboDef>().Select(def => def.EffectId).ToArray();
        var augmentIds = augments.Values.Cast<SpectacleTriadAugmentDef>().Select(def => def.EffectId).ToArray();

        Assert.Equal(45, comboIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(10, augmentIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(AugmentExpectations))]
    public void AugmentDefinition_MapsToExpectedAugmentKind(string modId, SpectacleAugmentKind expectedKind)
    {
        SpectacleTriadAugmentDef augment = SpectacleDefinitions.GetTriadAugment(modId);

        Assert.Equal(expectedKind, augment.Kind);
        Assert.False(string.IsNullOrWhiteSpace(augment.EffectId));
        Assert.False(string.IsNullOrWhiteSpace(augment.Name));
        Assert.True(augment.Coefficient > 0f);
    }

    [Theory]
    [MemberData(nameof(AugmentModIds))]
    public void TriadIntegration_EachAugmentModDispatchesExpectedCoreAndAugment(string augmentModId)
    {
        (string coreA, string coreB) = PickCorePairExcluding(augmentModId);
        var system = new SpectacleSystem();
        var tower = TowerWithMods(coreA, coreB, augmentModId);

        SpectacleTriggerInfo surge = TriggerTriadSurgeWithOrderedRoles(
            system,
            tower,
            primary: coreA,
            secondary: coreB,
            tertiary: augmentModId);

        SpectacleComboDef expectedCombo = SpectacleDefinitions.GetCombo(coreA, coreB);
        SpectacleTriadAugmentDef expectedAugment = SpectacleDefinitions.GetTriadAugment(augmentModId);

        Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
        Assert.Equal(coreA, surge.Signature.PrimaryModId);
        Assert.Equal(coreB, surge.Signature.SecondaryModId);
        Assert.Equal(augmentModId, surge.Signature.TertiaryModId);
        Assert.Equal(expectedCombo.EffectId, surge.Signature.ComboEffectId);
        Assert.Equal(expectedAugment.EffectId, surge.Signature.AugmentEffectId);
        Assert.Equal($"{expectedCombo.EffectId}+{expectedAugment.EffectId}", surge.Signature.EffectId);
        Assert.True(surge.Signature.AugmentStrength > 0f);
    }

    [Fact]
    public void SignatureSanitySweep_AllOneTwoThreeModLoadoutsResolveToValidEffects()
    {
        foreach (string[] loadout in GenerateLoadoutsUpToThreeMods())
        {
            var system = new SpectacleSystem();
            var tower = TowerWithMods(loadout);
            SpectacleSignature sig = system.PreviewSignature(tower);

            int unique = loadout.Distinct(StringComparer.Ordinal).Count();
            SpectacleMode expectedMode = unique switch
            {
                <= 1 => SpectacleMode.Single,
                2 => SpectacleMode.Combo,
                _ => SpectacleMode.Triad,
            };

            Assert.Equal(expectedMode, sig.Mode);
            Assert.False(string.IsNullOrWhiteSpace(sig.EffectId));
            Assert.False(string.IsNullOrWhiteSpace(sig.EffectName));
            Assert.True(sig.SurgePower >= 1f);

            if (sig.Mode == SpectacleMode.Single)
            {
                Assert.True(string.IsNullOrEmpty(sig.ComboEffectId));
                Assert.True(string.IsNullOrEmpty(sig.AugmentEffectId));
            }
            else if (sig.Mode == SpectacleMode.Combo)
            {
                Assert.False(string.IsNullOrWhiteSpace(sig.ComboEffectId));
                Assert.True(string.IsNullOrEmpty(sig.AugmentEffectId));
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(sig.ComboEffectId));
                Assert.False(string.IsNullOrWhiteSpace(sig.AugmentEffectId));
                Assert.True(sig.AugmentStrength > 0f);
            }
        }
    }

    [Fact]
    public void Determinism_SameSequenceProducesSameSurgeTrace()
    {
        List<string> traceA = RunDeterminismTrace();
        List<string> traceB = RunDeterminismTrace();

        Assert.NotEmpty(traceA);
        Assert.Equal(traceA.Count, traceB.Count);
        Assert.Equal(traceA, traceB);
    }

    [Fact]
    public void Stress_OneThousandProcs_StaysFiniteAndWithinBounds()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.Momentum, SpectacleDefinitions.SplitShot, SpectacleDefinitions.FeedbackLoop);
        int surgeCount = 0;

        system.OnSurgeTriggered += _ => surgeCount++;

        var rng = new Random(12345);
        string[] modCycle =
        {
            SpectacleDefinitions.Momentum,
            SpectacleDefinitions.SplitShot,
            SpectacleDefinitions.FeedbackLoop,
        };

        for (int i = 0; i < 1000; i++)
        {
            string modId = modCycle[rng.Next(modCycle.Length)];
            float scalar = 25f + (float)rng.NextDouble() * 120f;
            float delta = 0.40f + (float)rng.NextDouble() * 0.20f;

            system.RegisterProc(tower, modId, scalar);
            system.Update(delta);

            SpectacleVisualState visual = system.GetVisualState(tower);
            Assert.True(float.IsFinite(visual.MeterNormalized));
            Assert.True(float.IsFinite(system.GlobalMeter));
            Assert.InRange(visual.MeterNormalized, 0f, 1f);
            Assert.InRange(system.GlobalMeter, 0f, SpectacleDefinitions.GlobalThreshold);
        }

        Assert.True(surgeCount >= 5);
    }

    [Fact]
    public void RegisterProc_OnlySurgeTriggersAtThreshold()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        int surgeCount = 0;
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");

        system.OnSurgeTriggered += _ => surgeCount++;

        // Below surge threshold on first proc.
        system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge * 0.25f);
        Assert.Equal(0, surgeCount);

        // Carried meter + second proc crosses surge threshold.
        system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge * 0.80f);
        Assert.Equal(1, surgeCount);
    }

    [Fact]
    public void GlobalTrigger_FiresAfterRequiredSurgesFromTwoTowers()
    {
        var system = new SpectacleSystem();
        var towerA = TowerWithMods("split_shot");
        var towerB = TowerWithMods("split_shot");
        int globalCount = 0;
        GlobalSurgeTriggerInfo lastGlobal = default;
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");
        int requiredSurges = (int)Math.Ceiling(
            SpectacleDefinitions.ResolveGlobalThreshold() /
            Math.Max(0.0001f, SpectacleDefinitions.ResolveGlobalMeterPerSurge()));
        int surgesIssued = 0;

        system.OnGlobalTriggered += info =>
        {
            globalCount++;
            lastGlobal = info;
        };

        while (surgesIssued < requiredSurges)
        {
            system.RegisterProc(towerA, "split_shot", scalarForSingleProcSurge);
            surgesIssued++;
            if (surgesIssued < requiredSurges)
            {
                system.RegisterProc(towerB, "split_shot", scalarForSingleProcSurge);
                surgesIssued++;
            }
            if (surgesIssued < requiredSurges)
                system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.05f);
        }

        Assert.Equal(1, globalCount);
        Assert.True(lastGlobal.UniqueContributors >= 2);
        Assert.Equal("G_SPECTACLE_CATHARSIS", lastGlobal.EffectId);
    }

    [Fact]
    public void GlobalTrigger_FiresWhenMeterIsFull_EvenWithSingleContributor()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        int surgeCount = 0;
        int globalCount = 0;
        GlobalSurgeTriggerInfo lastGlobal = default;
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");
        int requiredSurges = (int)Math.Ceiling(
            SpectacleDefinitions.ResolveGlobalThreshold() /
            Math.Max(0.0001f, SpectacleDefinitions.ResolveGlobalMeterPerSurge()));

        system.OnSurgeTriggered += _ => surgeCount++;
        system.OnGlobalTriggered += info =>
        {
            globalCount++;
            lastGlobal = info;
        };

        for (int i = 0; i < requiredSurges; i++)
        {
            system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge);
            if (i < requiredSurges - 1)
                system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.25f);
        }

        Assert.Equal(requiredSurges, surgeCount);
        Assert.Equal(1, globalCount);
        Assert.Equal(1, lastGlobal.UniqueContributors);
        Assert.Equal(SpectacleDefinitions.GlobalMeterAfterTrigger, lastGlobal.MeterAfter, 3);
    }
}
