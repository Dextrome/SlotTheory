using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Modifiers;

namespace SlotTheory.Tools;

public sealed class CombatLabModifierBenchmarkRunner
{
    private sealed class ContextPlan
    {
        public string ContextId { get; init; } = string.Empty;
        public string TowerId { get; init; } = string.Empty;
        public string[] BaselineMods { get; init; } = Array.Empty<string>();
        public string BaselineCaseId { get; init; } = string.Empty;
        public Dictionary<string, string> ModifierCaseById { get; } = new(StringComparer.Ordinal);
    }

    private sealed class PairPlan
    {
        public string ProbeId { get; init; } = string.Empty;
        public string ContextId { get; init; } = string.Empty;
        public string TowerId { get; init; } = string.Empty;
        public string ModifierA { get; init; } = string.Empty;
        public string ModifierB { get; init; } = string.Empty;
        public string BaselineCaseId { get; init; } = string.Empty;
        public string CaseAId { get; init; } = string.Empty;
        public string CaseBId { get; init; } = string.Empty;
        public string PairCaseId { get; init; } = string.Empty;
        public HashSet<string> ScenarioFilter { get; } = new(StringComparer.Ordinal);
    }

    private readonly CombatLabTowerBenchmarkRunner _towerRunner;
    private readonly IReadOnlyDictionary<string, TowerDef>? _towerDefsOverride;

    public CombatLabModifierBenchmarkRunner(
        IReadOnlyDictionary<string, TowerDef>? towerDefsOverride = null,
        Func<string, Modifier>? modifierFactoryOverride = null)
    {
        _towerDefsOverride = towerDefsOverride;
        _towerRunner = new CombatLabTowerBenchmarkRunner(towerDefsOverride, modifierFactoryOverride);
    }

    public CombatLabModifierBenchmarkReport RunSuite(CombatLabModifierBenchmarkSuite suite)
    {
        if (suite.Scenarios == null || suite.Scenarios.Count == 0)
            throw new InvalidOperationException("Modifier benchmark suite has no scenarios.");

        Dictionary<string, TowerDef> towerDefs = ResolveTowerDefinitions();
        List<CombatLabModifierBenchmarkContext> contexts = ResolveContexts(suite, towerDefs);
        if (contexts.Count == 0)
            throw new InvalidOperationException("Modifier benchmark suite resolved zero tower contexts.");

        List<string> modifierIds = ResolveModifierIds(suite);
        if (modifierIds.Count == 0)
            throw new InvalidOperationException("Modifier benchmark suite resolved zero modifiers.");

        var warnings = new List<string>();
        var caseSetups = new Dictionary<string, CombatLabTowerBenchmarkTowerSetup>(StringComparer.Ordinal);
        var contextPlans = new Dictionary<string, ContextPlan>(StringComparer.Ordinal);

        foreach (CombatLabModifierBenchmarkContext context in contexts)
        {
            string contextId = ResolveContextId(context);
            string[] baselineMods = NormalizeMods(context.BaselineMods);
            string baselineCaseId = BuildBaselineCaseId(contextId);

            AddOrUpdateCase(caseSetups, BuildTowerSetup(context, baselineCaseId, baselineMods));
            var contextPlan = new ContextPlan
            {
                ContextId = contextId,
                TowerId = context.Tower,
                BaselineMods = baselineMods,
                BaselineCaseId = baselineCaseId,
            };

            foreach (string modifierId in modifierIds)
            {
                if (!IsCompatible(suite, modifierId, context.Tower))
                    continue;
                if (baselineMods.Length >= Balance.MaxModifiersPerTower)
                {
                    warnings.Add($"Context '{contextId}' skipped modifier '{modifierId}' because baseline already has {Balance.MaxModifiersPerTower} modifiers.");
                    continue;
                }

                string[] testedMods = baselineMods.Concat(new[] { modifierId }).Take(Balance.MaxModifiersPerTower).ToArray();
                if (testedMods.Length <= baselineMods.Length)
                {
                    warnings.Add($"Context '{contextId}' could not add modifier '{modifierId}' due to modifier-slot cap.");
                    continue;
                }

                string caseId = BuildModifierCaseId(contextId, modifierId);
                AddOrUpdateCase(caseSetups, BuildTowerSetup(context, caseId, testedMods));
                contextPlan.ModifierCaseById[modifierId] = caseId;
            }

            contextPlans[contextId] = contextPlan;
        }

        List<PairPlan> pairPlans = BuildPairPlans(suite, contexts, contextPlans, caseSetups, warnings);

        var expandedSuite = new CombatLabTowerBenchmarkSuite
        {
            Name = suite.Name + "_modifier_delta",
            Mode = suite.Mode,
            Seed = suite.Seed,
            TrialsPerScenario = suite.TrialsPerScenario,
            TimestepSeconds = suite.TimestepSeconds,
            IncludeAllTowers = false,
            Towers = caseSetups.Values.OrderBy(v => v.CaseId, StringComparer.Ordinal).ToList(),
            Scenarios = suite.Scenarios,
            CostByTower = suite.CostByTower ?? new Dictionary<string, float>(),
            CostBands = suite.CostBands ?? new List<CombatLabCostBand>(),
        };

        CombatLabTowerBenchmarkReport baseReport = _towerRunner.RunSuite(expandedSuite);
        Dictionary<string, Dictionary<string, CombatLabTowerBenchmarkTowerResult>> rowsByScenario = baseReport.ScenarioResults
            .ToDictionary(
                s => s.ScenarioId,
                s => s.Results.ToDictionary(r => r.CaseId, StringComparer.Ordinal),
                StringComparer.Ordinal);

        List<CombatLabModifierBenchmarkDeltaRow> deltaRows = BuildDeltaRows(
            baseReport,
            rowsByScenario,
            contextPlans,
            warnings);
        List<CombatLabModifierPairResult> pairRows = BuildPairRows(
            baseReport,
            rowsByScenario,
            pairPlans,
            warnings);
        List<CombatLabModifierBenchmarkProfile> profiles = BuildProfiles(suite, modifierIds, deltaRows, pairRows);
        List<CombatLabModifierBenchmarkSuggestion> suggestions = BuildSuggestions(suite, profiles);

        return new CombatLabModifierBenchmarkReport
        {
            Suite = suite.Name,
            Mode = suite.Mode,
            GeneratedUtc = DateTime.UtcNow,
            DeltaRows = deltaRows,
            PairRows = pairRows,
            ModifierProfiles = profiles,
            TuningSuggestions = suggestions,
            Warnings = warnings.Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal).ToList(),
        };
    }

    public static string BuildDeltaCsv(CombatLabModifierBenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "scenario_id,scenario_name,path_type,tags,context_id,tower_id,modifier_id,baseline_mods,tested_mods,baseline_damage,modified_damage,delta_damage,delta_damage_pct,baseline_dps,modified_dps,delta_dps,delta_dps_pct,baseline_kills,modified_kills,delta_kills,baseline_leaks,modified_leaks,delta_leaks,baseline_leak_prevention,modified_leak_prevention,delta_leak_prevention,baseline_wave_clear_seconds,modified_wave_clear_seconds,delta_wave_clear_seconds,baseline_overkill_waste,modified_overkill_waste,delta_overkill_waste,baseline_uptime,modified_uptime,delta_uptime,baseline_targets_hit,modified_targets_hit,delta_targets_hit,baseline_reliability,modified_reliability,delta_reliability,baseline_surges,modified_surges,delta_surges,baseline_global_surges,modified_global_surges,delta_global_surges,range_value_delta");
        foreach (CombatLabModifierBenchmarkDeltaRow row in report.DeltaRows.OrderBy(r => r.ModifierId, StringComparer.Ordinal).ThenBy(r => r.ContextId, StringComparer.Ordinal).ThenBy(r => r.ScenarioId, StringComparer.Ordinal))
        {
            sb.AppendLine(string.Join(",",
                Csv(row.ScenarioId),
                Csv(row.ScenarioName),
                Csv(row.PathType),
                Csv(string.Join("|", row.Tags ?? Array.Empty<string>())),
                Csv(row.ContextId),
                Csv(row.TowerId),
                Csv(row.ModifierId),
                Csv(string.Join("|", row.BaselineMods ?? Array.Empty<string>())),
                Csv(string.Join("|", row.TestedMods ?? Array.Empty<string>())),
                Csv(row.BaselineDamage),
                Csv(row.ModifiedDamage),
                Csv(row.DeltaDamage),
                Csv(row.DeltaDamagePct),
                Csv(row.BaselineDps),
                Csv(row.ModifiedDps),
                Csv(row.DeltaDps),
                Csv(row.DeltaDpsPct),
                Csv(row.BaselineKills),
                Csv(row.ModifiedKills),
                Csv(row.DeltaKills),
                Csv(row.BaselineLeaks),
                Csv(row.ModifiedLeaks),
                Csv(row.DeltaLeaks),
                Csv(row.BaselineLeakPrevention),
                Csv(row.ModifiedLeakPrevention),
                Csv(row.DeltaLeakPrevention),
                Csv(row.BaselineWaveClearSeconds),
                Csv(row.ModifiedWaveClearSeconds),
                Csv(row.DeltaWaveClearSeconds),
                Csv(row.BaselineOverkillWaste),
                Csv(row.ModifiedOverkillWaste),
                Csv(row.DeltaOverkillWaste),
                Csv(row.BaselineUptime),
                Csv(row.ModifiedUptime),
                Csv(row.DeltaUptime),
                Csv(row.BaselineTargetsHit),
                Csv(row.ModifiedTargetsHit),
                Csv(row.DeltaTargetsHit),
                Csv(row.BaselineReliability),
                Csv(row.ModifiedReliability),
                Csv(row.DeltaReliability),
                Csv(row.BaselineSurges),
                Csv(row.ModifiedSurges),
                Csv(row.DeltaSurges),
                Csv(row.BaselineGlobalSurges),
                Csv(row.ModifiedGlobalSurges),
                Csv(row.DeltaGlobalSurges),
                Csv(row.RangeValueDelta)));
        }

        return sb.ToString();
    }

    public static string BuildProfileCsv(CombatLabModifierBenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "modifier_id,classification,scenario_count,context_count,best_case_gain_pct,avg_gain_pct,worst_case_gain_pct,compatibility_spread,uselessness_rate,abuse_potential,avg_delta_kills,avg_delta_leaks,avg_delta_wave_clear_seconds,avg_delta_overkill,avg_delta_uptime,avg_delta_targets_hit,avg_delta_reliability,flags");
        foreach (CombatLabModifierBenchmarkProfile profile in report.ModifierProfiles.OrderBy(p => p.ModifierId, StringComparer.Ordinal))
        {
            sb.AppendLine(string.Join(",",
                Csv(profile.ModifierId),
                Csv(profile.Classification),
                Csv(profile.ScenarioCount),
                Csv(profile.ContextCount),
                Csv(profile.BestCaseGainPct),
                Csv(profile.AvgGainPct),
                Csv(profile.WorstCaseGainPct),
                Csv(profile.CompatibilitySpread),
                Csv(profile.UselessnessRate),
                Csv(profile.AbusePotential),
                Csv(profile.AvgDeltaKills),
                Csv(profile.AvgDeltaLeaks),
                Csv(profile.AvgDeltaWaveClearSeconds),
                Csv(profile.AvgDeltaOverkill),
                Csv(profile.AvgDeltaUptime),
                Csv(profile.AvgDeltaTargetsHit),
                Csv(profile.AvgDeltaReliability),
                Csv(string.Join("|", profile.Flags))));
        }

        return sb.ToString();
    }

    public static string BuildPairCsv(CombatLabModifierBenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "probe_id,scenario_id,context_id,tower_id,modifier_a,modifier_b,pair_delta_damage_pct,expected_additive_damage_pct,synergy_excess_pct,pair_delta_leak_prevention,classification");
        foreach (CombatLabModifierPairResult row in report.PairRows.OrderBy(r => r.ProbeId, StringComparer.Ordinal).ThenBy(r => r.ScenarioId, StringComparer.Ordinal))
        {
            sb.AppendLine(string.Join(",",
                Csv(row.ProbeId),
                Csv(row.ScenarioId),
                Csv(row.ContextId),
                Csv(row.TowerId),
                Csv(row.ModifierA),
                Csv(row.ModifierB),
                Csv(row.PairDeltaDamagePct),
                Csv(row.ExpectedAdditiveDamagePct),
                Csv(row.SynergyExcessPct),
                Csv(row.PairDeltaLeakPrevention),
                Csv(row.Classification)));
        }

        return sb.ToString();
    }

    private static List<CombatLabModifierBenchmarkDeltaRow> BuildDeltaRows(
        CombatLabTowerBenchmarkReport baseReport,
        Dictionary<string, Dictionary<string, CombatLabTowerBenchmarkTowerResult>> rowsByScenario,
        Dictionary<string, ContextPlan> contextPlans,
        List<string> warnings)
    {
        var rows = new List<CombatLabModifierBenchmarkDeltaRow>();
        foreach (CombatLabTowerBenchmarkScenarioResult scenario in baseReport.ScenarioResults)
        {
            if (!rowsByScenario.TryGetValue(scenario.ScenarioId, out Dictionary<string, CombatLabTowerBenchmarkTowerResult>? caseRows))
                continue;

            foreach ((string contextId, ContextPlan contextPlan) in contextPlans)
            {
                if (!caseRows.TryGetValue(contextPlan.BaselineCaseId, out CombatLabTowerBenchmarkTowerResult? baselineRow))
                {
                    warnings.Add($"Missing baseline row for context '{contextId}' in scenario '{scenario.ScenarioId}'.");
                    continue;
                }

                foreach ((string modifierId, string modCaseId) in contextPlan.ModifierCaseById)
                {
                    if (!caseRows.TryGetValue(modCaseId, out CombatLabTowerBenchmarkTowerResult? modifiedRow))
                    {
                        warnings.Add($"Missing modifier row for context '{contextId}', modifier '{modifierId}', scenario '{scenario.ScenarioId}'.");
                        continue;
                    }

                    float deltaDamage = modifiedRow.TotalDamage - baselineRow.TotalDamage;
                    float deltaDps = modifiedRow.EffectiveDps - baselineRow.EffectiveDps;
                    float deltaKills = modifiedRow.KillCount - baselineRow.KillCount;
                    float deltaLeaks = modifiedRow.LeakCount - baselineRow.LeakCount;
                    float deltaLeakPrevention = modifiedRow.LeakPreventionValue - baselineRow.LeakPreventionValue;
                    float deltaWaveClearSeconds = modifiedRow.WaveClearSeconds - baselineRow.WaveClearSeconds;
                    float deltaOverkill = modifiedRow.OverkillWaste - baselineRow.OverkillWaste;
                    float deltaUptime = modifiedRow.Uptime - baselineRow.Uptime;
                    float deltaTargets = modifiedRow.AverageTargetsHit - baselineRow.AverageTargetsHit;
                    float deltaReliability = modifiedRow.ReliabilityVariance - baselineRow.ReliabilityVariance;
                    float deltaSurges = modifiedRow.SurgesTriggered - baselineRow.SurgesTriggered;
                    float deltaGlobalSurges = modifiedRow.GlobalSurgesTriggered - baselineRow.GlobalSurgesTriggered;

                    rows.Add(new CombatLabModifierBenchmarkDeltaRow
                    {
                        ScenarioId = scenario.ScenarioId,
                        ScenarioName = scenario.ScenarioName,
                        PathType = scenario.PathType,
                        Tags = scenario.Tags ?? Array.Empty<string>(),
                        ContextId = contextId,
                        TowerId = contextPlan.TowerId,
                        ModifierId = modifierId,
                        BaselineMods = contextPlan.BaselineMods,
                        TestedMods = contextPlan.BaselineMods.Concat(new[] { modifierId }).Take(Balance.MaxModifiersPerTower).ToArray(),
                        BaselineDamage = baselineRow.TotalDamage,
                        ModifiedDamage = modifiedRow.TotalDamage,
                        DeltaDamage = deltaDamage,
                        DeltaDamagePct = PercentDelta(baselineRow.TotalDamage, deltaDamage),
                        BaselineDps = baselineRow.EffectiveDps,
                        ModifiedDps = modifiedRow.EffectiveDps,
                        DeltaDps = deltaDps,
                        DeltaDpsPct = PercentDelta(baselineRow.EffectiveDps, deltaDps),
                        BaselineKills = baselineRow.KillCount,
                        ModifiedKills = modifiedRow.KillCount,
                        DeltaKills = deltaKills,
                        BaselineLeaks = baselineRow.LeakCount,
                        ModifiedLeaks = modifiedRow.LeakCount,
                        DeltaLeaks = deltaLeaks,
                        BaselineLeakPrevention = baselineRow.LeakPreventionValue,
                        ModifiedLeakPrevention = modifiedRow.LeakPreventionValue,
                        DeltaLeakPrevention = deltaLeakPrevention,
                        BaselineWaveClearSeconds = baselineRow.WaveClearSeconds,
                        ModifiedWaveClearSeconds = modifiedRow.WaveClearSeconds,
                        DeltaWaveClearSeconds = deltaWaveClearSeconds,
                        BaselineOverkillWaste = baselineRow.OverkillWaste,
                        ModifiedOverkillWaste = modifiedRow.OverkillWaste,
                        DeltaOverkillWaste = deltaOverkill,
                        BaselineUptime = baselineRow.Uptime,
                        ModifiedUptime = modifiedRow.Uptime,
                        DeltaUptime = deltaUptime,
                        BaselineTargetsHit = baselineRow.AverageTargetsHit,
                        ModifiedTargetsHit = modifiedRow.AverageTargetsHit,
                        DeltaTargetsHit = deltaTargets,
                        BaselineReliability = baselineRow.ReliabilityVariance,
                        ModifiedReliability = modifiedRow.ReliabilityVariance,
                        DeltaReliability = deltaReliability,
                        BaselineSurges = baselineRow.SurgesTriggered,
                        ModifiedSurges = modifiedRow.SurgesTriggered,
                        DeltaSurges = deltaSurges,
                        BaselineGlobalSurges = baselineRow.GlobalSurgesTriggered,
                        ModifiedGlobalSurges = modifiedRow.GlobalSurgesTriggered,
                        DeltaGlobalSurges = deltaGlobalSurges,
                        RangeValueDelta = baselineRow.IdleTimeSeconds - modifiedRow.IdleTimeSeconds,
                    });
                }
            }
        }

        return rows.OrderBy(r => r.ModifierId, StringComparer.Ordinal)
            .ThenBy(r => r.ContextId, StringComparer.Ordinal)
            .ThenBy(r => r.ScenarioId, StringComparer.Ordinal)
            .ToList();
    }

    private static List<CombatLabModifierPairResult> BuildPairRows(
        CombatLabTowerBenchmarkReport baseReport,
        Dictionary<string, Dictionary<string, CombatLabTowerBenchmarkTowerResult>> rowsByScenario,
        List<PairPlan> pairPlans,
        List<string> warnings)
    {
        var rows = new List<CombatLabModifierPairResult>();
        foreach (PairPlan pairPlan in pairPlans)
        {
            foreach (CombatLabTowerBenchmarkScenarioResult scenario in baseReport.ScenarioResults)
            {
                if (pairPlan.ScenarioFilter.Count > 0 && !pairPlan.ScenarioFilter.Contains(scenario.ScenarioId))
                    continue;
                if (!rowsByScenario.TryGetValue(scenario.ScenarioId, out Dictionary<string, CombatLabTowerBenchmarkTowerResult>? caseRows))
                    continue;
                if (!caseRows.TryGetValue(pairPlan.BaselineCaseId, out CombatLabTowerBenchmarkTowerResult? baseline))
                {
                    warnings.Add($"Pair probe '{pairPlan.ProbeId}' missing baseline case in scenario '{scenario.ScenarioId}'.");
                    continue;
                }
                if (!caseRows.TryGetValue(pairPlan.CaseAId, out CombatLabTowerBenchmarkTowerResult? caseA))
                {
                    warnings.Add($"Pair probe '{pairPlan.ProbeId}' missing single-mod A case in scenario '{scenario.ScenarioId}'.");
                    continue;
                }
                if (!caseRows.TryGetValue(pairPlan.CaseBId, out CombatLabTowerBenchmarkTowerResult? caseB))
                {
                    warnings.Add($"Pair probe '{pairPlan.ProbeId}' missing single-mod B case in scenario '{scenario.ScenarioId}'.");
                    continue;
                }
                if (!caseRows.TryGetValue(pairPlan.PairCaseId, out CombatLabTowerBenchmarkTowerResult? pair))
                {
                    warnings.Add($"Pair probe '{pairPlan.ProbeId}' missing pair case in scenario '{scenario.ScenarioId}'.");
                    continue;
                }

                float deltaA = PercentDelta(baseline.TotalDamage, caseA.TotalDamage - baseline.TotalDamage);
                float deltaB = PercentDelta(baseline.TotalDamage, caseB.TotalDamage - baseline.TotalDamage);
                float pairDelta = PercentDelta(baseline.TotalDamage, pair.TotalDamage - baseline.TotalDamage);
                float expected = deltaA + deltaB;
                float synergyExcess = pairDelta - expected;
                float pairLeakDelta = pair.LeakPreventionValue - baseline.LeakPreventionValue;

                string classification = "healthy pair";
                if (pairDelta >= 0.35f && synergyExcess >= 0.18f)
                    classification = "suspected abuse combo";
                else if (pairDelta <= MathF.Max(deltaA, deltaB) * 0.35f)
                    classification = "suspected dead combo";

                rows.Add(new CombatLabModifierPairResult
                {
                    ProbeId = pairPlan.ProbeId,
                    ScenarioId = scenario.ScenarioId,
                    ContextId = pairPlan.ContextId,
                    TowerId = pairPlan.TowerId,
                    ModifierA = pairPlan.ModifierA,
                    ModifierB = pairPlan.ModifierB,
                    PairDeltaDamagePct = pairDelta,
                    ExpectedAdditiveDamagePct = expected,
                    SynergyExcessPct = synergyExcess,
                    PairDeltaLeakPrevention = pairLeakDelta,
                    Classification = classification,
                });
            }
        }

        return rows.OrderBy(r => r.ProbeId, StringComparer.Ordinal)
            .ThenBy(r => r.ScenarioId, StringComparer.Ordinal)
            .ToList();
    }

    private static List<CombatLabModifierBenchmarkProfile> BuildProfiles(
        CombatLabModifierBenchmarkSuite suite,
        List<string> allModifierIds,
        List<CombatLabModifierBenchmarkDeltaRow> deltaRows,
        List<CombatLabModifierPairResult> pairRows)
    {
        var profiles = new List<CombatLabModifierBenchmarkProfile>();
        CombatLabModifierThresholds thresholds = suite.Thresholds ?? new CombatLabModifierThresholds();

        foreach (string modifierId in allModifierIds.OrderBy(v => v, StringComparer.Ordinal))
        {
            List<CombatLabModifierBenchmarkDeltaRow> rows = deltaRows
                .Where(r => string.Equals(r.ModifierId, modifierId, StringComparison.Ordinal))
                .ToList();
            if (rows.Count == 0)
            {
                profiles.Add(new CombatLabModifierBenchmarkProfile
                {
                    ModifierId = modifierId,
                    Classification = "too narrow",
                    ScenarioCount = 0,
                    ContextCount = 0,
                    Flags = new List<string> { "no compatible contexts in current suite" },
                });
                continue;
            }

            float avgGain = (float)rows.Average(r => r.DeltaDamagePct);
            float best = rows.Max(r => r.DeltaDamagePct);
            float worst = rows.Min(r => r.DeltaDamagePct);
            float avgKills = (float)rows.Average(r => r.DeltaKills);
            float avgLeaks = (float)rows.Average(r => r.DeltaLeaks);
            float avgWaveClear = (float)rows.Average(r => r.DeltaWaveClearSeconds);
            float avgOverkill = (float)rows.Average(r => r.DeltaOverkillWaste);
            float avgUptime = (float)rows.Average(r => r.DeltaUptime);
            float avgTargets = (float)rows.Average(r => r.DeltaTargetsHit);
            float avgReliability = (float)rows.Average(r => r.DeltaReliability);
            float uselessnessRate = (float)rows.Count(r =>
                MathF.Abs(r.DeltaDamagePct) <= thresholds.UselessDeltaPct
                && r.DeltaLeakPrevention <= 0.005f
                && r.DeltaKills <= 0.10f) / rows.Count;
            float abusePotential = (float)rows.Count(r =>
                r.DeltaDamagePct >= thresholds.AbuseDamageDeltaPct
                || r.DeltaLeakPrevention >= thresholds.AbuseLeakReductionDelta) / rows.Count;

            List<float> byContext = rows
                .GroupBy(r => r.ContextId)
                .Select(g => (float)g.Average(v => v.DeltaDamagePct))
                .ToList();
            float spread = byContext.Count <= 1 ? 0f : byContext.Max() - byContext.Min();
            int contextCount = rows.Select(r => r.ContextId).Distinct(StringComparer.Ordinal).Count();

            string[] intentTags = ResolveIntentTags(suite, modifierId);
            bool intendedNiche = intentTags.Any(t => string.Equals(t, "niche", StringComparison.OrdinalIgnoreCase));
            string classification = ResolveClassification(thresholds, avgGain, best, worst, spread, uselessnessRate, abusePotential, intendedNiche);

            var flags = new List<string>();
            if (best >= thresholds.EdgeCasePeakDeltaPct && worst <= thresholds.EdgeCaseWorstDeltaPct)
                flags.Add("warning: balanced by one abusive edge case");
            if (uselessnessRate >= 0.80f)
                flags.Add("suspected dead combos");
            if (abusePotential >= 0.30f)
                flags.Add("suspected abuse combos");
            if (pairRows.Any(p =>
                    (string.Equals(p.ModifierA, modifierId, StringComparison.Ordinal) || string.Equals(p.ModifierB, modifierId, StringComparison.Ordinal))
                    && string.Equals(p.Classification, "suspected abuse combo", StringComparison.Ordinal)))
                flags.Add("abusive pair interactions detected");

            profiles.Add(new CombatLabModifierBenchmarkProfile
            {
                ModifierId = modifierId,
                Classification = classification,
                ScenarioCount = rows.Count,
                ContextCount = contextCount,
                BestCaseGainPct = best,
                AvgGainPct = avgGain,
                WorstCaseGainPct = worst,
                CompatibilitySpread = spread,
                UselessnessRate = uselessnessRate,
                AbusePotential = abusePotential,
                AvgDeltaKills = avgKills,
                AvgDeltaLeaks = avgLeaks,
                AvgDeltaWaveClearSeconds = avgWaveClear,
                AvgDeltaOverkill = avgOverkill,
                AvgDeltaUptime = avgUptime,
                AvgDeltaTargetsHit = avgTargets,
                AvgDeltaReliability = avgReliability,
                Flags = flags,
            });
        }

        return profiles.OrderBy(p => p.ModifierId, StringComparer.Ordinal).ToList();
    }

    private static List<CombatLabModifierBenchmarkSuggestion> BuildSuggestions(
        CombatLabModifierBenchmarkSuite suite,
        List<CombatLabModifierBenchmarkProfile> profiles)
    {
        var autotune = suite.Autotune;
        if (autotune == null || !autotune.Enabled)
            return new List<CombatLabModifierBenchmarkSuggestion>();

        float maxDelta = Mathf.Clamp(autotune.MaxPercentDelta, 0.01f, 0.30f);
        float targetMin = MathF.Max(0f, autotune.TargetAvgDamageDeltaPctMin);
        float targetMax = MathF.Max(targetMin, autotune.TargetAvgDamageDeltaPctMax);
        float targetMid = (targetMin + targetMax) * 0.5f;
        var suggestions = new List<CombatLabModifierBenchmarkSuggestion>();

        foreach (CombatLabModifierBenchmarkProfile profile in profiles)
        {
            bool buff = profile.Classification is "too weak" or "trap pick" or "too narrow" or "only valuable in edge cases";
            bool nerf = profile.Classification is "too generically strong" or "broken on specific towers";
            if (!buff && !nerf)
                continue;

            float error = MathF.Abs(profile.AvgGainPct - targetMid) / MathF.Max(0.01f, targetMid + 0.01f);
            float magnitude = Mathf.Clamp(error * 0.6f, 0.01f, maxDelta);

            CombatLabModifierAutotuneBounds? bounds = ResolveAutotuneBoundsForModifier(autotune, profile.ModifierId);
            Dictionary<string, float> deltas = BuildSuggestedModifierDeltas(profile.ModifierId, buff, magnitude, maxDelta, bounds);
            bool structural = profile.Classification == "broken on specific towers" && profile.ModifierId == "feedback_loop";

            suggestions.Add(new CombatLabModifierBenchmarkSuggestion
            {
                ModifierId = profile.ModifierId,
                Reason = buff ? "average modifier delta below healthy band" : "average modifier delta above healthy band",
                SuggestedDeltas = deltas,
                Notes = structural
                    ? "Numeric tuning helps, but structural behavior likely needs redesign for stable balance."
                    : "Numeric suggestion preserves modifier identity and targets healthy delta bands.",
                RequiresStructuralChange = structural,
            });
        }

        return suggestions;
    }

    private static List<PairPlan> BuildPairPlans(
        CombatLabModifierBenchmarkSuite suite,
        List<CombatLabModifierBenchmarkContext> contexts,
        Dictionary<string, ContextPlan> contextPlans,
        Dictionary<string, CombatLabTowerBenchmarkTowerSetup> caseSetups,
        List<string> warnings)
    {
        var result = new List<PairPlan>();
        if (suite.PairProbes == null || suite.PairProbes.Count == 0)
            return result;

        Dictionary<string, CombatLabModifierBenchmarkContext> contextById = contexts.ToDictionary(c => ResolveContextId(c), c => c, StringComparer.Ordinal);
        foreach (CombatLabModifierPairProbe probe in suite.PairProbes)
        {
            string contextId = (probe.ContextId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(contextId))
            {
                warnings.Add("Pair probe skipped because context_id is empty.");
                continue;
            }
            if (!contextById.TryGetValue(contextId, out CombatLabModifierBenchmarkContext? context))
            {
                warnings.Add($"Pair probe '{probe.Id}' skipped because context '{contextId}' was not found.");
                continue;
            }
            if (!contextPlans.TryGetValue(contextId, out ContextPlan? contextPlan))
            {
                warnings.Add($"Pair probe '{probe.Id}' skipped because context plan '{contextId}' was not resolved.");
                continue;
            }

            string modifierA = SpectacleDefinitions.NormalizeModId(probe.ModifierA);
            string modifierB = SpectacleDefinitions.NormalizeModId(probe.ModifierB);
            if (string.IsNullOrWhiteSpace(modifierA) || string.IsNullOrWhiteSpace(modifierB))
            {
                warnings.Add($"Pair probe '{probe.Id}' skipped due to empty modifier ids.");
                continue;
            }

            if (contextPlan.BaselineMods.Length + 2 > Balance.MaxModifiersPerTower)
            {
                warnings.Add($"Pair probe '{probe.Id}' skipped because context '{contextId}' has no room for two additional modifiers.");
                continue;
            }

            string caseAId = BuildModifierCaseId(contextId, modifierA);
            string caseBId = BuildModifierCaseId(contextId, modifierB);
            string pairCaseId = BuildPairCaseId(contextId, modifierA, modifierB, probe.Id);

            if (!contextPlan.ModifierCaseById.ContainsKey(modifierA))
            {
                string[] modsA = contextPlan.BaselineMods.Concat(new[] { modifierA }).Take(Balance.MaxModifiersPerTower).ToArray();
                AddOrUpdateCase(caseSetups, BuildTowerSetup(context, caseAId, modsA));
                contextPlan.ModifierCaseById[modifierA] = caseAId;
            }
            if (!contextPlan.ModifierCaseById.ContainsKey(modifierB))
            {
                string[] modsB = contextPlan.BaselineMods.Concat(new[] { modifierB }).Take(Balance.MaxModifiersPerTower).ToArray();
                AddOrUpdateCase(caseSetups, BuildTowerSetup(context, caseBId, modsB));
                contextPlan.ModifierCaseById[modifierB] = caseBId;
            }

            string[] pairMods = contextPlan.BaselineMods
                .Concat(new[] { modifierA, modifierB })
                .Take(Balance.MaxModifiersPerTower)
                .ToArray();
            AddOrUpdateCase(caseSetups, BuildTowerSetup(context, pairCaseId, pairMods));

            var plan = new PairPlan
            {
                ProbeId = string.IsNullOrWhiteSpace(probe.Id) ? $"{contextId}:{modifierA}+{modifierB}" : probe.Id.Trim(),
                ContextId = contextId,
                TowerId = context.Tower,
                ModifierA = modifierA,
                ModifierB = modifierB,
                BaselineCaseId = contextPlan.BaselineCaseId,
                CaseAId = contextPlan.ModifierCaseById[modifierA],
                CaseBId = contextPlan.ModifierCaseById[modifierB],
                PairCaseId = pairCaseId,
            };
            foreach (string scenarioId in probe.ScenarioIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(scenarioId))
                    plan.ScenarioFilter.Add(scenarioId.Trim());
            }

            result.Add(plan);
        }

        return result;
    }

    private static string ResolveClassification(
        CombatLabModifierThresholds thresholds,
        float avgGain,
        float best,
        float worst,
        float spread,
        float uselessnessRate,
        float abusePotential,
        bool intendedNiche)
    {
        if (avgGain <= thresholds.WeakAvgDamageDeltaPct && uselessnessRate >= thresholds.TrapUselessnessRate)
            return "trap pick";
        if (avgGain <= thresholds.WeakAvgDamageDeltaPct)
            return "too weak";
        if (abusePotential >= 0.25f && spread >= 0.18f)
            return "broken on specific towers";
        if (avgGain >= thresholds.StrongAvgDamageDeltaPct && spread <= 0.12f && uselessnessRate <= 0.20f)
            return "too generically strong";

        bool edgeCaseOnly = best >= thresholds.EdgeCasePeakDeltaPct
            && avgGain <= thresholds.WeakAvgDamageDeltaPct * 1.2f
            && worst <= thresholds.EdgeCaseWorstDeltaPct;
        if (edgeCaseOnly)
            return intendedNiche ? "high variance but acceptable" : "only valuable in edge cases";

        if (spread >= 0.28f)
            return intendedNiche ? "high variance but acceptable" : "too narrow";

        return "broadly healthy";
    }

    private static Dictionary<string, float> BuildSuggestedModifierDeltas(
        string modifierId,
        bool buff,
        float magnitude,
        float maxDelta,
        CombatLabModifierAutotuneBounds? bounds)
    {
        var keys = ResolveSuggestedKeys(modifierId);
        var deltas = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            float sign = key is "penalty" or "cooldown"
                ? (buff ? -1f : 1f)
                : (buff ? 1f : -1f);
            float raw = sign * magnitude;
            CombatLabNumericBounds? keyBounds = ResolveModifierBoundsForKey(bounds, key);
            deltas[key] = ClampDelta(raw, maxDelta, keyBounds);
        }

        return deltas;
    }

    private static string[] ResolveSuggestedKeys(string modifierId)
    {
        return modifierId switch
        {
            "momentum" => new[] { "bonus", "stack_cap" },
            "overkill" => new[] { "proc_strength" },
            "exploit_weakness" => new[] { "bonus" },
            "focus_lens" => new[] { "bonus", "penalty" },
            "slow" => new[] { "proc_strength", "duration" },
            "overreach" => new[] { "radius", "penalty" },
            "hair_trigger" => new[] { "bonus", "penalty" },
            "split_shot" => new[] { "proc_strength", "stack_cap" },
            "feedback_loop" => new[] { "cooldown" },
            "chain_reaction" => new[] { "proc_strength", "radius" },
            _ => new[] { "bonus" },
        };
    }

    private static CombatLabNumericBounds? ResolveModifierBoundsForKey(CombatLabModifierAutotuneBounds? bounds, string key)
    {
        if (bounds == null)
            return null;
        return key switch
        {
            "bonus" => bounds.Bonus,
            "flat_bonus" => bounds.FlatBonus,
            "proc_chance" => bounds.ProcChance,
            "proc_strength" => bounds.ProcStrength,
            "duration" => bounds.Duration,
            "radius" => bounds.Radius,
            "stack_cap" => bounds.StackCap,
            "cooldown" => bounds.Cooldown,
            "penalty" => bounds.Penalty,
            _ => null,
        };
    }

    private static CombatLabModifierAutotuneBounds? ResolveAutotuneBoundsForModifier(
        CombatLabModifierBenchmarkAutotuneConfig autotune,
        string modifierId)
    {
        if (autotune.PerModifierBounds != null && autotune.PerModifierBounds.TryGetValue(modifierId, out CombatLabModifierAutotuneBounds? perModifier))
            return perModifier ?? autotune.GlobalBounds;
        return autotune.GlobalBounds;
    }

    private static float ClampDelta(float value, float maxAbs, CombatLabNumericBounds? bounds)
    {
        float min = -Math.Abs(maxAbs);
        float max = Math.Abs(maxAbs);
        if (bounds != null)
        {
            min = Math.Max(min, Math.Min(bounds.Min, bounds.Max));
            max = Math.Min(max, Math.Max(bounds.Min, bounds.Max));
        }
        if (min > max)
        {
            float mid = (min + max) * 0.5f;
            min = mid;
            max = mid;
        }
        return Mathf.Clamp(value, min, max);
    }

    private Dictionary<string, TowerDef> ResolveTowerDefinitions()
    {
        if (_towerDefsOverride != null)
            return new Dictionary<string, TowerDef>(_towerDefsOverride, StringComparer.Ordinal);

        return DataLoader.GetAllTowerIds(includeLocked: true)
            .ToDictionary(id => id, DataLoader.GetTowerDef, StringComparer.Ordinal);
    }

    private static List<CombatLabModifierBenchmarkContext> ResolveContexts(
        CombatLabModifierBenchmarkSuite suite,
        Dictionary<string, TowerDef> towerDefs)
    {
        var map = new Dictionary<string, CombatLabModifierBenchmarkContext>(StringComparer.Ordinal);

        if (suite.IncludeAllTowers)
        {
            foreach ((string towerId, _) in towerDefs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                map[towerId] = new CombatLabModifierBenchmarkContext
                {
                    Id = towerId,
                    Tower = towerId,
                    BaselineMods = Array.Empty<string>(),
                    Targeting = DefaultTargetingForTower(towerId),
                    Cost = ResolveDefaultCostByTower(towerId),
                };
            }
        }

        foreach (CombatLabModifierBenchmarkContext context in suite.Contexts ?? new List<CombatLabModifierBenchmarkContext>())
        {
            if (string.IsNullOrWhiteSpace(context.Tower))
                continue;
            if (!towerDefs.ContainsKey(context.Tower))
                continue;

            string id = ResolveContextId(context);
            map[id] = new CombatLabModifierBenchmarkContext
            {
                Id = id,
                Tower = context.Tower,
                BaselineMods = NormalizeMods(context.BaselineMods),
                Targeting = string.IsNullOrWhiteSpace(context.Targeting) ? DefaultTargetingForTower(context.Tower) : context.Targeting,
                BaseDamageOverride = context.BaseDamageOverride,
                AttackIntervalOverride = context.AttackIntervalOverride,
                RangeOverride = context.RangeOverride,
                SplitCountOverride = context.SplitCountOverride,
                ChainCountOverride = context.ChainCountOverride,
                Cost = context.Cost,
            };
        }

        return map.Values.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    private static List<string> ResolveModifierIds(CombatLabModifierBenchmarkSuite suite)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (suite.IncludeAllModifiers)
        {
            foreach (string id in DataLoader.GetAllModifierIds(includeLocked: true))
                set.Add(SpectacleDefinitions.NormalizeModId(id));
        }

        foreach (string id in suite.Modifiers ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(id))
                set.Add(SpectacleDefinitions.NormalizeModId(id));
        }

        foreach (CombatLabModifierPairProbe probe in suite.PairProbes ?? new List<CombatLabModifierPairProbe>())
        {
            if (!string.IsNullOrWhiteSpace(probe.ModifierA))
                set.Add(SpectacleDefinitions.NormalizeModId(probe.ModifierA));
            if (!string.IsNullOrWhiteSpace(probe.ModifierB))
                set.Add(SpectacleDefinitions.NormalizeModId(probe.ModifierB));
        }

        set.RemoveWhere(string.IsNullOrWhiteSpace);
        return set.OrderBy(v => v, StringComparer.Ordinal).ToList();
    }

    private static bool IsCompatible(CombatLabModifierBenchmarkSuite suite, string modifierId, string towerId)
    {
        if (suite.CompatibilityByModifier == null || !suite.CompatibilityByModifier.TryGetValue(modifierId, out string[]? compatible) || compatible == null || compatible.Length == 0)
            return true;
        return compatible.Any(v => string.Equals(v, towerId, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] ResolveIntentTags(CombatLabModifierBenchmarkSuite suite, string modifierId)
    {
        if (suite.IntentTagsByModifier != null && suite.IntentTagsByModifier.TryGetValue(modifierId, out string[]? tags) && tags != null)
            return tags;
        return Array.Empty<string>();
    }

    private static string ResolveContextId(CombatLabModifierBenchmarkContext context)
    {
        string id = (context.Id ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(id) ? context.Tower : id;
    }

    private static string[] NormalizeMods(string[]? mods)
    {
        if (mods == null || mods.Length == 0)
            return Array.Empty<string>();
        return mods
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(SpectacleDefinitions.NormalizeModId)
            .Take(Balance.MaxModifiersPerTower)
            .ToArray();
    }

    private static string BuildBaselineCaseId(string contextId) => $"ctx:{contextId}:base";
    private static string BuildModifierCaseId(string contextId, string modifierId) => $"ctx:{contextId}:mod:{modifierId}";
    private static string BuildPairCaseId(string contextId, string modifierA, string modifierB, string probeId)
    {
        string suffix = string.IsNullOrWhiteSpace(probeId) ? $"{modifierA}+{modifierB}" : probeId.Trim();
        return $"ctx:{contextId}:pair:{suffix}";
    }

    private static CombatLabTowerBenchmarkTowerSetup BuildTowerSetup(
        CombatLabModifierBenchmarkContext context,
        string caseId,
        string[] mods)
    {
        return new CombatLabTowerBenchmarkTowerSetup
        {
            CaseId = caseId,
            Tower = context.Tower,
            Mods = mods,
            Targeting = context.Targeting,
            BaseDamageOverride = context.BaseDamageOverride,
            AttackIntervalOverride = context.AttackIntervalOverride,
            RangeOverride = context.RangeOverride,
            SplitCountOverride = context.SplitCountOverride,
            ChainCountOverride = context.ChainCountOverride,
            Cost = context.Cost,
        };
    }

    private static void AddOrUpdateCase(
        Dictionary<string, CombatLabTowerBenchmarkTowerSetup> caseSetups,
        CombatLabTowerBenchmarkTowerSetup setup)
    {
        caseSetups[setup.CaseId] = setup;
    }

    private static float PercentDelta(float baseline, float delta)
    {
        if (MathF.Abs(baseline) <= 0.0001f)
        {
            if (MathF.Abs(delta) <= 0.0001f)
                return 0f;
            return delta > 0f ? 1f : -1f;
        }

        return delta / baseline;
    }

    private static string DefaultTargetingForTower(string towerId)
    {
        return towerId switch
        {
            "heavy_cannon" => "strongest",
            _ => "first",
        };
    }

    private static float ResolveDefaultCostByTower(string towerId)
    {
        return towerId switch
        {
            "rapid_shooter" => 95f,
            "heavy_cannon" => 130f,
            "marker_tower" => 90f,
            "chain_tower" => 115f,
            "rift_prism" => 120f,
            "phase_splitter" => 122f,
            _ => 100f,
        };
    }

    private static string Csv(string value)
    {
        string text = value ?? string.Empty;
        bool quote = text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
        if (text.Contains('"'))
            text = text.Replace("\"", "\"\"");
        return quote ? $"\"{text}\"" : text;
    }

    private static string Csv(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Csv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
