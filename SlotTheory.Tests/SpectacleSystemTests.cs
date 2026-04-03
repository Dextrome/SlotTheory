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
    // Apply a neutral-1.0 profile before each test so that tuned production
    // defaults in SpectacleTuningProfile don't affect isolated logic assertions.
    public SpectacleSystemTests() => SpectacleTuning.Apply(SpectacleTuningProfile.Neutral(), "test-neutral");

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
        yield return new object[] { SpectacleDefinitions.Momentum,       SpectacleAugmentKind.Reload };
        yield return new object[] { SpectacleDefinitions.Overkill,       SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.ExploitWeakness,SpectacleAugmentKind.Strike };
        yield return new object[] { SpectacleDefinitions.FocusLens,      SpectacleAugmentKind.Strike };
        yield return new object[] { SpectacleDefinitions.ChillShot,      SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.Overreach,      SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.HairTrigger,    SpectacleAugmentKind.Reload };
        yield return new object[] { SpectacleDefinitions.SplitShot,      SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.FeedbackLoop,   SpectacleAugmentKind.Reload };
        yield return new object[] { SpectacleDefinitions.ChainReaction,  SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.BlastCore,      SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.Wildfire,       SpectacleAugmentKind.Area };
        yield return new object[] { SpectacleDefinitions.ReaperProtocol, SpectacleAugmentKind.Strike };
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

    // Roles are now determined purely by loadout (copy count + canonical rank),
    // so the primary/secondary/tertiary parameters are ignored for role assignment.
    // This wrapper exists only to avoid breaking callsite signatures during Phase 2 migration.
    private static SpectacleTriggerInfo TriggerTriadSurgeWithOrderedRoles(
        SpectacleSystem system,
        FakeTower tower,
        string primary,
        string secondary,
        string tertiary)
        => TriggerOneSurge(system, tower, primary, 125f);

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
        float meterScale = SpectacleDefinitions.ResolveMeterGainScale();

        float perScalarGain = baseGain * copy * meterScale;
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
    public void CopyMultiplier_MatchesPlannedTable()
    {
        Assert.Equal(0f, SpectacleDefinitions.GetCopyMultiplier(0), 3);
        Assert.Equal(1.00f, SpectacleDefinitions.GetCopyMultiplier(1), 3);
        Assert.Equal(1.92f, SpectacleDefinitions.GetCopyMultiplier(2), 3);
        Assert.Equal(2.70f, SpectacleDefinitions.GetCopyMultiplier(3), 3);
    }

    [Fact]
    public void ComboDefinition_UsesCanonicalPairMapping()
    {
        // Dynamic naming: GetCombo(r1, r2) -- r1 is the primary (passed first), r2 is the secondary.
        // ID format: C_{R1_UPPER}_{R2_UPPER}. Name format: "[PrimaryShortName] [SecondaryTag]".
        SpectacleComboDef combo = SpectacleDefinitions.GetCombo("momentum", "split_shot");
        Assert.Equal("C_MOMENTUM_SPLIT_SHOT", combo.EffectId);
        Assert.Equal("Momentum Split", combo.Name);
    }

    [Fact]
    public void PreviewSignature_DuplicateModCollapsesToUniqueSet()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot", "split_shot", "slow");

        SpectacleSignature sig = system.PreviewSignature(tower);

        // Two unique mods: chill_shot (canonical rank 4) and split_shot (rank 7).
        // All mods contribute equally -- canonical order determines slot assignment.
        Assert.Equal(SpectacleMode.Combo, sig.Mode);
        Assert.Equal(SpectacleDefinitions.ChillShot, sig.PrimaryModId);
        Assert.Equal(SpectacleDefinitions.SplitShot, sig.SecondaryModId);
    }

    [Fact]
    public void DispatchLogic_SplitAndChain_UsesSplitChainComboCore()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.SplitShot, SpectacleDefinitions.ChainReaction);

        SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, SpectacleDefinitions.SplitShot, 125f);

        // Dynamic IDs: C_{R1_UPPER}_{R2_UPPER} where r1/r2 are primary/secondary by canonical rank.
        SpectacleComboDef expected = SpectacleDefinitions.GetCombo(surge.Signature.PrimaryModId, surge.Signature.SecondaryModId);
        Assert.Equal(SpectacleMode.Combo, surge.Signature.Mode);
        Assert.Equal(expected.EffectId, surge.Signature.ComboEffectId);
        Assert.Equal(expected.EffectId, surge.Signature.EffectId);
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

        // Loadout canonical sort: SplitShot(7) < FeedbackLoop(8) < ChainReaction(9)
        // r1=SplitShot, r2=FeedbackLoop, r3=ChainReaction
        // Dynamic combo ID: C_{R1_UPPER}_{R2_UPPER}
        SpectacleComboDef expectedCombo = SpectacleDefinitions.GetCombo(surge.Signature.PrimaryModId, surge.Signature.SecondaryModId);
        Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
        Assert.Equal(expectedCombo.EffectId, surge.Signature.ComboEffectId);
        Assert.Equal("T_AUG_CHAIN", surge.Signature.AugmentEffectId);
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

        // Pair permutations: tower has [comboA(SplitShot,7), comboB(ChainReaction,9)]
        // Canonical sort: r1=comboA, r2=comboB → combo = C_SPLIT_CHAIN
        SpectacleComboDef expectedPairCombo = SpectacleDefinitions.GetCombo(comboA, comboB);

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
            Assert.Equal(expectedPairCombo.EffectId, surge.Signature.ComboEffectId);
            Assert.True(string.IsNullOrEmpty(surge.Signature.AugmentEffectId));
        }

        // Triad permutations: tower has [comboA(SplitShot,7), comboB(ChainReaction,9), augment(FeedbackLoop,8)]
        // Loadout-based canonical sort: r1=comboA(7), r2=augment(8), r3=comboB(9)
        // combo = GetCombo(comboA, augment), triad augment = GetTriadAugment(comboB)
        SpectacleComboDef expectedTriadCombo = SpectacleDefinitions.GetCombo(comboA, augment);
        SpectacleTriadAugmentDef expectedAugment = SpectacleDefinitions.GetTriadAugment(comboB);

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
            SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, comboA, 125f);

            Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
            Assert.Equal(expectedTriadCombo.EffectId, surge.Signature.ComboEffectId);
            Assert.Equal(expectedAugment.EffectId, surge.Signature.AugmentEffectId);
            Assert.Equal($"{expectedTriadCombo.EffectId}+{expectedAugment.EffectId}", surge.Signature.EffectId);
        }
    }

    [Fact]
    public void DefinitionCoverage_ComboAndAugmentTablesHaveExpectedCounts()
    {
        // Combo names are now dynamically generated -- no static ComboDefs table.
        var supported = SpectacleDefinitions.SupportedModIds.ToArray();
        int supportedCount = supported.Length;
        var comboIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string r1 in supported)
            foreach (string r2 in supported)
            {
                if (r1 == r2) continue;
                SpectacleComboDef combo = SpectacleDefinitions.GetCombo(r1, r2);
                Assert.False(string.IsNullOrWhiteSpace(combo.EffectId));
                Assert.False(string.IsNullOrWhiteSpace(combo.Name));
                comboIds.Add(combo.EffectId);
            }
        Assert.Equal(supportedCount * (supportedCount - 1), comboIds.Count); // ordered pairs

        // Augment table: one entry per supported mod.
        IDictionary augments = ReadPrivateStaticDictionary("TriadAugments");
        Assert.Equal(supportedCount, augments.Count);

        var augmentIds = augments.Values.Cast<SpectacleTriadAugmentDef>().Select(def => def.EffectId).ToArray();
        Assert.Equal(supportedCount, augmentIds.Distinct(StringComparer.Ordinal).Count());

        var augmentNames = augments.Values.Cast<SpectacleTriadAugmentDef>().Select(def => def.Name).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Equal(3, augmentNames.Length);
        Assert.Contains("Pulse", augmentNames);
        Assert.Contains("Strike", augmentNames);
        Assert.Contains("Recharge", augmentNames);
    }

    [Theory]
    [MemberData(nameof(AugmentExpectations))]
    public void AugmentDefinition_MapsToExpectedAugmentKind(string modId, SpectacleAugmentKind expectedKind)
    {
        SpectacleTriadAugmentDef augment = SpectacleDefinitions.GetTriadAugment(modId);
        string expectedName = expectedKind switch
        {
            SpectacleAugmentKind.Area => "Pulse",
            SpectacleAugmentKind.Strike => "Strike",
            SpectacleAugmentKind.Reload => "Recharge",
            _ => string.Empty,
        };

        Assert.Equal(expectedKind, augment.Kind);
        Assert.False(string.IsNullOrWhiteSpace(augment.EffectId));
        Assert.Equal(expectedName, augment.Name);
        Assert.True(augment.Coefficient > 0f);
    }

    [Theory]
    [MemberData(nameof(AugmentModIds))]
    public void TriadIntegration_EachAugmentModDispatchesExpectedCoreAndAugment(string augmentModId)
    {
        (string coreA, string coreB) = PickCorePairExcluding(augmentModId);
        var system = new SpectacleSystem();
        var tower = TowerWithMods(coreA, coreB, augmentModId);

        SpectacleTriggerInfo surge = TriggerOneSurge(system, tower, coreA, 125f);

        // Roles are now determined by canonical rank (copy counts equal here).
        string[] roles = new[] { coreA, coreB, augmentModId }
            .OrderBy(m => Array.IndexOf(OrderedSupportedMods, m))
            .ToArray();
        string r1 = roles[0], r2 = roles[1], r3 = roles[2];

        SpectacleComboDef expectedCombo = SpectacleDefinitions.GetCombo(r1, r2);
        SpectacleTriadAugmentDef expectedAugment = SpectacleDefinitions.GetTriadAugment(r3);

        Assert.Equal(SpectacleMode.Triad, surge.Signature.Mode);
        Assert.Equal(r1, surge.Signature.PrimaryModId);
        Assert.Equal(r2, surge.Signature.SecondaryModId);
        Assert.Equal(r3, surge.Signature.TertiaryModId);
        Assert.Equal(expectedCombo.EffectId, surge.Signature.ComboEffectId);
        Assert.Equal(expectedAugment.EffectId, surge.Signature.AugmentEffectId);
        Assert.Equal($"{expectedCombo.EffectId}+{expectedAugment.EffectId}", surge.Signature.EffectId);
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

        // Tick past the per-mod cooldown so the second proc is admitted.
        system.Update(SpectacleDefinitions.ModProcCooldownSeconds + 0.01f);

        // Carried meter + second proc crosses surge threshold.
        system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge * 0.80f);
        Assert.Equal(1, surgeCount);
    }

    [Fact]
    public void RegisterProc_NonFiniteScalar_IsIgnored()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");

        system.RegisterProc(tower, "split_shot", float.NaN, eventDamage: 20f);
        SpectacleVisualState afterNaN = system.GetVisualState(tower);
        Assert.Equal(0f, afterNaN.MeterNormalized, 3);

        system.RegisterProc(tower, "split_shot", float.PositiveInfinity, eventDamage: 20f);
        SpectacleVisualState afterInf = system.GetVisualState(tower);
        Assert.Equal(0f, afterInf.MeterNormalized, 3);
    }

    [Fact]
    public void RegisterProc_NonFiniteDamage_DoesNotPoisonMeter()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");
        int surgeCount = 0;

        system.OnSurgeTriggered += _ => surgeCount++;
        system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge * 0.30f, eventDamage: float.NaN);
        SpectacleVisualState visual = system.GetVisualState(tower);
        Assert.True(float.IsFinite(visual.MeterNormalized));
        Assert.True(visual.MeterNormalized > 0f);

        system.Update(SpectacleDefinitions.ModProcCooldownSeconds + 0.01f);
        system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge * 0.80f, eventDamage: float.PositiveInfinity);
        Assert.Equal(1, surgeCount);
        Assert.True(float.IsFinite(system.GlobalMeter));
    }

    [Fact]
    public void GlobalSurge_BecomesReadyBeforeActivation()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        int readyCount = 0;
        int globalCount = 0;
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");
        int requiredSurges = (int)Math.Ceiling(
            SpectacleDefinitions.ResolveGlobalThreshold() /
            Math.Max(0.0001f, SpectacleDefinitions.ResolveGlobalMeterPerSurge()));

        system.OnGlobalSurgeReady += _ => readyCount++;
        system.OnGlobalTriggered += _ => globalCount++;

        for (int i = 0; i < requiredSurges; i++)
        {
            system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge);
            if (i < requiredSurges - 1)
                system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.25f);
        }

        Assert.Equal(1, readyCount);
        Assert.Equal(0, globalCount);
        Assert.True(system.IsGlobalSurgeReady);

        system.ActivateGlobalSurge();

        Assert.Equal(1, globalCount);
        Assert.False(system.IsGlobalSurgeReady);
    }

    [Fact]
    public void GlobalSurgeReady_DoesNotRefireWhilePendingActivation()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        int readyCount = 0;
        int globalCount = 0;
        float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");
        int requiredSurges = (int)Math.Ceiling(
            SpectacleDefinitions.ResolveGlobalThreshold() /
            Math.Max(0.0001f, SpectacleDefinitions.ResolveGlobalMeterPerSurge()));

        system.OnGlobalSurgeReady += _ => readyCount++;
        system.OnGlobalTriggered += _ => globalCount++;

        for (int i = 0; i < requiredSurges + 3; i++)
        {
            system.RegisterProc(tower, "split_shot", scalarForSingleProcSurge);
            system.Update(SpectacleDefinitions.SurgeCooldownSeconds + 0.25f);
        }

        Assert.Equal(1, readyCount);
        Assert.Equal(0, globalCount);
        Assert.True(system.IsGlobalSurgeReady);

        system.ActivateGlobalSurge();

        Assert.Equal(1, globalCount);
        Assert.False(system.IsGlobalSurgeReady);
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

        system.ActivateGlobalSurge();
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

        system.ActivateGlobalSurge();
        Assert.Equal(requiredSurges, surgeCount);
        Assert.Equal(1, globalCount);
        Assert.Equal(1, lastGlobal.UniqueContributors);
        Assert.Equal(SpectacleDefinitions.GlobalMeterAfterTrigger, lastGlobal.MeterAfter, 3);
    }

    [Fact]
    public void GlobalTrigger_RemovedTower_DoesNotCountAsContributor()
    {
        SpectacleTuning.Reset();
        try
        {
            SpectacleTuning.Apply(new SpectacleTuningProfile
            {
                GlobalThresholdMultiplier = 0.10f,
            }, "test");

            var system = new SpectacleSystem();
            var towerA = TowerWithMods("split_shot");
            var towerB = TowerWithMods("split_shot");
            int globalCount = 0;
            GlobalSurgeTriggerInfo lastGlobal = default;
            float scalarForSingleProcSurge = ScalarToGuaranteeSingleProcSurge("split_shot");

            system.OnGlobalTriggered += info =>
            {
                globalCount++;
                lastGlobal = info;
            };

            system.RegisterProc(towerA, "split_shot", scalarForSingleProcSurge);
            system.RemoveTower(towerA);
            system.RegisterProc(towerB, "split_shot", scalarForSingleProcSurge);
            system.ActivateGlobalSurge();

            Assert.Equal(1, globalCount);
            Assert.Equal(1, lastGlobal.UniqueContributors);
        }
        finally
        {
            SpectacleTuning.Reset();
        }
    }

    [Fact]
    public void TowerSpecificMeterGainMultiplier_ChangesMeterFillRate()
    {
        SpectacleTuning.Reset();
        try
        {
            SpectacleTuning.Apply(new SpectacleTuningProfile
            {
                TowerMeterGainMultipliers =
                {
                    ["rapid_shooter"] = 1.6f,
                    ["heavy_cannon"] = 0.6f,
                },
                // Keep threshold bias neutral so this test isolates meter-gain behavior only.
                TowerSurgeThresholdMultipliers =
                {
                    ["rapid_shooter"] = 1f,
                    ["heavy_cannon"] = 1f,
                },
            }, "test");

            var system = new SpectacleSystem();
            var fastTower = TowerWithMods(SpectacleDefinitions.SplitShot);
            fastTower.TowerId = "rapid_shooter";
            var slowTower = TowerWithMods(SpectacleDefinitions.SplitShot);
            slowTower.TowerId = "heavy_cannon";

            system.RegisterProc(fastTower, SpectacleDefinitions.SplitShot, 20f);
            system.RegisterProc(slowTower, SpectacleDefinitions.SplitShot, 20f);

            SpectacleVisualState fastVisual = system.GetVisualState(fastTower);
            SpectacleVisualState slowVisual = system.GetVisualState(slowTower);
            Assert.True(fastVisual.MeterNormalized > slowVisual.MeterNormalized);
        }
        finally
        {
            SpectacleTuning.Reset();
        }
    }

    [Fact]
    public void TowerSpecificSurgeThresholdMultiplier_ChangesMeterNormalization()
    {
        SpectacleTuning.Reset();
        try
        {
            SpectacleTuning.Apply(new SpectacleTuningProfile
            {
                // Keep gain bias neutral so this test isolates threshold normalization behavior only.
                TowerMeterGainMultipliers =
                {
                    ["rapid_shooter"] = 1f,
                    ["heavy_cannon"] = 1f,
                },
                TowerSurgeThresholdMultipliers =
                {
                    ["rapid_shooter"] = 0.8f,
                    ["heavy_cannon"] = 1.3f,
                },
            }, "test");

            var system = new SpectacleSystem();
            var lowThresholdTower = TowerWithMods(SpectacleDefinitions.SplitShot);
            lowThresholdTower.TowerId = "rapid_shooter";
            var highThresholdTower = TowerWithMods(SpectacleDefinitions.SplitShot);
            highThresholdTower.TowerId = "heavy_cannon";

            system.RegisterProc(lowThresholdTower, SpectacleDefinitions.SplitShot, 20f);
            system.RegisterProc(highThresholdTower, SpectacleDefinitions.SplitShot, 20f);

            SpectacleVisualState lowThresholdVisual = system.GetVisualState(lowThresholdTower);
            SpectacleVisualState highThresholdVisual = system.GetVisualState(highThresholdTower);
            Assert.True(lowThresholdVisual.MeterNormalized > highThresholdVisual.MeterNormalized);
        }
        finally
        {
            SpectacleTuning.Reset();
        }
    }

    [Fact]
    public void GlobalSurge_StoresOverfillAndMarksOvercharged_WhenOverflowIsMeaningful()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.Momentum);
        system.SetGlobalMeterFraction(0.98f);

        system.RegisterProc(tower, SpectacleDefinitions.Momentum, eventScalar: 140f);
        Assert.True(system.IsGlobalSurgeReady);

        GlobalSurgeTriggerInfo triggered = default;
        bool fired = false;
        system.OnGlobalTriggered += info =>
        {
            fired = true;
            triggered = info;
        };

        system.ActivateGlobalSurge();

        Assert.True(fired);
        Assert.True(triggered.Overcharged);
        Assert.True(triggered.StoredOverfill > 0f);
    }

    [Fact]
    public void GlobalSurge_DoesNotMarkOvercharged_WhenOverflowBelowThreshold()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods(SpectacleDefinitions.Momentum);
        system.SetGlobalMeterFraction(0.95f);

        system.RegisterProc(tower, SpectacleDefinitions.Momentum, eventScalar: 140f);
        Assert.True(system.IsGlobalSurgeReady);

        GlobalSurgeTriggerInfo triggered = default;
        system.OnGlobalTriggered += info => triggered = info;
        system.ActivateGlobalSurge();

        Assert.False(triggered.Overcharged);
    }
}
