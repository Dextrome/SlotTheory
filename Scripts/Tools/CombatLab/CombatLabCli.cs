using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using SlotTheory.Core;

namespace SlotTheory.Tools;

public static class CombatLabCli
{
    public static bool Run(string[] userArgs)
    {
        string? scenarioFile = GetArgValue(userArgs, "--lab_scenario");
        string? sweepFile = GetArgValue(userArgs, "--lab_sweep");
        string? towerBenchmarkFile = GetArgValue(userArgs, "--lab_tower_benchmark");
        string? modifierBenchmarkFile = GetArgValue(userArgs, "--lab_modifier_benchmark");
        string? outputFile = GetArgValue(userArgs, "--lab_out");

        if (!string.IsNullOrWhiteSpace(modifierBenchmarkFile))
            return RunModifierBenchmarkFile(modifierBenchmarkFile!, outputFile);
        if (!string.IsNullOrWhiteSpace(towerBenchmarkFile))
            return RunTowerBenchmarkFile(towerBenchmarkFile!, outputFile);
        if (!string.IsNullOrWhiteSpace(scenarioFile))
            return RunScenarioFile(scenarioFile!, outputFile);
        if (!string.IsNullOrWhiteSpace(sweepFile))
            return RunSweepFile(sweepFile!, outputFile);

        GD.PrintErr("[LAB] Missing --lab_scenario <path>, --lab_sweep <path>, --lab_tower_benchmark <path>, or --lab_modifier_benchmark <path>.");
        return false;
    }

    private static bool RunScenarioFile(string scenarioFile, string? outputFile)
    {
        if (!TryLoadScenarioSuite(scenarioFile, out var suite, out string error))
        {
            GD.PrintErr($"[LAB] Failed to load scenario file: {error}");
            return false;
        }

        var runner = new CombatLabScenarioRunner();
        List<CombatLabScenarioResult> results = runner.RunSuite(suite);
        int passCount = results.Count(r => r.Passed);

        GD.Print($"[LAB] Scenario suite '{suite.Name}' -> {passCount}/{results.Count} passed");
        foreach (var result in results)
        {
            string status = result.Passed ? "PASS" : "FAIL";
            GD.Print($"[LAB] [{status}] {result.Name} | hits={result.Metrics.ExplosionHits}, residue={result.Metrics.ResidueCount}, status_det={result.Metrics.StatusDetonations}");
            foreach (string failure in result.Failures)
                GD.PrintErr($"  - {failure}");
        }

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            WriteJson(outputFile!, new
            {
                suite = suite.Name,
                passed = passCount,
                total = results.Count,
                results,
            });
        }

        return passCount == results.Count;
    }

    private static bool RunSweepFile(string sweepFile, string? outputFile)
    {
        string fullPath = ResolvePath(sweepFile);
        if (!File.Exists(fullPath))
        {
            GD.PrintErr($"[LAB] Sweep file not found: {fullPath}");
            return false;
        }

        CombatLabSweepConfig? sweep;
        try
        {
            string json = File.ReadAllText(fullPath);
            var opts = JsonOptions();
            sweep = JsonSerializer.Deserialize<CombatLabSweepConfig>(json, opts);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LAB] Failed reading sweep file: {ex.Message}");
            return false;
        }

        if (sweep == null)
        {
            GD.PrintErr("[LAB] Sweep file deserialized to null.");
            return false;
        }

        if (!TryLoadScenarioSuite(sweep.ScenarioFile, out var suite, out string error))
        {
            GD.PrintErr($"[LAB] Failed to load sweep scenario suite: {error}");
            return false;
        }

        int runsPerVariant = Math.Max(1, sweep.RunsPerVariant);
        var runner = new CombatLabScenarioRunner();
        var variantReports = new List<object>();

        foreach (CombatLabSweepVariant variant in sweep.Variants)
        {
            SpectacleTuning.Apply(variant.Tuning, variant.Id);
            int passCount = 0;
            int total = 0;
            float totalExplosionDamage = 0f;
            float totalExplosionHits = 0f;
            float totalStatusDetonations = 0f;
            float totalResidue = 0f;
            float totalSimulDetPeak = 0f;
            int totalFailures = 0;

            for (int run = 0; run < runsPerVariant; run++)
            {
                foreach (CombatLabScenario sourceScenario in suite.Scenarios)
                {
                    var scenario = CloneScenarioWithSeedOffset(sourceScenario, run);
                    CombatLabScenarioResult result = runner.RunScenario(scenario);
                    total += 1;
                    if (result.Passed) passCount++;
                    totalFailures += result.Failures.Count;
                    totalExplosionDamage += result.Metrics.ExplosionDamage;
                    totalExplosionHits += result.Metrics.ExplosionHits;
                    totalStatusDetonations += result.Metrics.StatusDetonations;
                    totalResidue += result.Metrics.ResidueUptimeSeconds;
                    totalSimulDetPeak += result.Metrics.SimultaneousDetonationPeak;
                }
            }

            float passRate = total > 0 ? (float)passCount / total : 0f;
            float avgExplosionHits = total > 0 ? totalExplosionHits / total : 0f;
            float avgStatusDet = total > 0 ? totalStatusDetonations / total : 0f;
            float avgResidue = total > 0 ? totalResidue / total : 0f;
            float avgSimulPeak = total > 0 ? totalSimulDetPeak / total : 0f;
            float avgFailures = total > 0 ? (float)totalFailures / total : 0f;

            // Simple optimization signal; weights can be revised per project goals.
            float score = passRate * 100f
                + avgExplosionHits * 2f
                + avgStatusDet
                + avgResidue * 0.5f
                - avgSimulPeak * 3f
                - avgFailures * 8f;

            variantReports.Add(new
            {
                id = variant.Id,
                pass_rate = passRate,
                avg_explosion_hits = avgExplosionHits,
                avg_status_detonations = avgStatusDet,
                avg_residue_uptime_seconds = avgResidue,
                avg_simultaneous_detonation_peak = avgSimulPeak,
                avg_failures_per_scenario = avgFailures,
                score,
            });

            GD.Print($"[LAB] Sweep variant {variant.Id}: pass={passCount}/{total} score={score:0.00}");
        }

        SpectacleTuning.Reset();

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            WriteJson(outputFile!, new
            {
                sweep = sweep.Name,
                runs_per_variant = runsPerVariant,
                variants = variantReports,
            });
        }

        return true;
    }

    private static bool RunTowerBenchmarkFile(string benchmarkFile, string? outputFile)
    {
        if (!TryLoadTowerBenchmarkSuite(benchmarkFile, out CombatLabTowerBenchmarkSuite suite, out string error))
        {
            GD.PrintErr($"[LAB] Failed to load tower benchmark file: {error}");
            return false;
        }

        CombatLabTowerBenchmarkReport report;
        try
        {
            var runner = new CombatLabTowerBenchmarkRunner();
            report = runner.RunSuite(suite);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LAB] Tower benchmark run failed: {ex.Message}");
            return false;
        }

        GD.Print($"[LAB] Tower benchmark '{suite.Name}' mode={suite.Mode} scenarios={suite.Scenarios.Count} towers={report.TowerProfiles.Count}");
        foreach (CombatLabTowerBenchmarkProfile profile in report.TowerProfiles.OrderByDescending(p => p.AvgNormalizedGlobal))
        {
            string flags = profile.Flags.Count > 0 ? string.Join("; ", profile.Flags) : "ok";
            string label = string.Equals(profile.CaseId, profile.TowerId, StringComparison.Ordinal)
                ? profile.TowerId
                : $"{profile.CaseId} ({profile.TowerId})";
            GD.Print(
                $"[LAB] Tower {label}: global={profile.AvgNormalizedGlobal:0.00} role={profile.AvgNormalizedRole:0.00} cost_band={profile.CostBand} sensitivity={profile.MapPathSensitivity:0.00} flags={flags}");
        }

        if (report.TuningSuggestions.Count > 0)
        {
            GD.Print("[LAB] Suggested tuning deltas:");
            foreach (CombatLabTowerBenchmarkSuggestion suggestion in report.TuningSuggestions)
            {
                string deltaText = string.Join(", ", suggestion.SuggestedDeltas.Select(kv => $"{kv.Key}:{kv.Value:+0.###;-0.###;0}"));
                GD.Print($"[LAB]   {suggestion.TowerId}: {deltaText} ({suggestion.Reason})");
            }
        }

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            string resolved = ResolvePath(outputFile!);
            WriteJson(resolved, report);

            string baseNoExt = Path.Combine(
                Path.GetDirectoryName(resolved) ?? string.Empty,
                Path.GetFileNameWithoutExtension(resolved));
            string scenarioCsvPath = baseNoExt + ".scenarios.csv";
            string towerCsvPath = baseNoExt + ".towers.csv";
            WriteText(scenarioCsvPath, CombatLabTowerBenchmarkRunner.BuildScenarioCsv(report));
            WriteText(towerCsvPath, CombatLabTowerBenchmarkRunner.BuildTowerProfileCsv(report));
            GD.Print($"[LAB] Wrote benchmark CSV: {scenarioCsvPath}");
            GD.Print($"[LAB] Wrote benchmark CSV: {towerCsvPath}");
        }

        return true;
    }

    private static bool RunModifierBenchmarkFile(string benchmarkFile, string? outputFile)
    {
        if (!TryLoadModifierBenchmarkSuite(benchmarkFile, out CombatLabModifierBenchmarkSuite suite, out string error))
        {
            GD.PrintErr($"[LAB] Failed to load modifier benchmark file: {error}");
            return false;
        }

        CombatLabModifierBenchmarkReport report;
        try
        {
            var runner = new CombatLabModifierBenchmarkRunner();
            report = runner.RunSuite(suite);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LAB] Modifier benchmark run failed: {ex}");
            return false;
        }

        GD.Print(
            $"[LAB] Modifier benchmark '{suite.Name}' mode={suite.Mode} scenarios={suite.Scenarios.Count} modifiers={report.ModifierProfiles.Count} rows={report.DeltaRows.Count}");
        foreach (CombatLabModifierBenchmarkProfile profile in report.ModifierProfiles.OrderByDescending(p => p.AvgGainPct))
        {
            string flags = profile.Flags.Count > 0 ? string.Join("; ", profile.Flags) : "ok";
            GD.Print(
                $"[LAB] Modifier {profile.ModifierId}: class={profile.Classification} avg={profile.AvgGainPct:+0.00%;-0.00%;0.00%} best={profile.BestCaseGainPct:+0.00%;-0.00%;0.00%} spread={profile.CompatibilitySpread:0.00} useless={profile.UselessnessRate:0.00} abuse={profile.AbusePotential:0.00} flags={flags}");
        }

        if (report.PairRows.Count > 0)
        {
            int abusePairs = report.PairRows.Count(r => string.Equals(r.Classification, "suspected abuse combo", StringComparison.Ordinal));
            int deadPairs = report.PairRows.Count(r => string.Equals(r.Classification, "suspected dead combo", StringComparison.Ordinal));
            GD.Print($"[LAB] Pair probes: total={report.PairRows.Count} abuse={abusePairs} dead={deadPairs}");
        }

        if (report.Warnings.Count > 0)
        {
            GD.Print("[LAB] Warnings:");
            foreach (string warning in report.Warnings)
                GD.PrintErr($"[LAB]   {warning}");
        }

        if (report.TuningSuggestions.Count > 0)
        {
            GD.Print("[LAB] Suggested modifier tuning deltas:");
            foreach (CombatLabModifierBenchmarkSuggestion suggestion in report.TuningSuggestions)
            {
                string deltaText = string.Join(", ", suggestion.SuggestedDeltas.Select(kv => $"{kv.Key}:{kv.Value:+0.###;-0.###;0}"));
                GD.Print($"[LAB]   {suggestion.ModifierId}: {deltaText} ({suggestion.Reason})");
            }
        }

        if (!string.IsNullOrWhiteSpace(outputFile))
        {
            string resolved = ResolvePath(outputFile!);
            WriteJson(resolved, report);

            string baseNoExt = Path.Combine(
                Path.GetDirectoryName(resolved) ?? string.Empty,
                Path.GetFileNameWithoutExtension(resolved));
            string deltaCsvPath = baseNoExt + ".deltas.csv";
            string profileCsvPath = baseNoExt + ".profiles.csv";
            string pairCsvPath = baseNoExt + ".pairs.csv";
            WriteText(deltaCsvPath, CombatLabModifierBenchmarkRunner.BuildDeltaCsv(report));
            WriteText(profileCsvPath, CombatLabModifierBenchmarkRunner.BuildProfileCsv(report));
            WriteText(pairCsvPath, CombatLabModifierBenchmarkRunner.BuildPairCsv(report));
            GD.Print($"[LAB] Wrote benchmark CSV: {deltaCsvPath}");
            GD.Print($"[LAB] Wrote benchmark CSV: {profileCsvPath}");
            GD.Print($"[LAB] Wrote benchmark CSV: {pairCsvPath}");
        }

        return true;
    }

    private static CombatLabScenario CloneScenarioWithSeedOffset(CombatLabScenario source, int seedOffset)
    {
        return new CombatLabScenario
        {
            Name = source.Name,
            ScenarioType = source.ScenarioType,
            Seed = source.Seed + seedOffset,
            TowerSetup = source.TowerSetup.Select(t => new CombatLabTowerSetup
            {
                Tower = t.Tower,
                Position = t.Position.ToArray(),
                Mods = t.Mods.ToArray(),
                BaseDamage = t.BaseDamage,
                AttackInterval = t.AttackInterval,
                Range = t.Range,
            }).ToList(),
            Enemies = source.Enemies.Select(e => new CombatLabEnemySetup
            {
                Id = e.Id,
                Type = e.Type,
                PathT = e.PathT,
                Position = e.Position?.ToArray(),
                Status = e.Status.ToArray(),
                Hp = e.Hp,
            }).ToList(),
            SimulateSeconds = source.SimulateSeconds,
            TriggerDamage = source.TriggerDamage,
            SurgePower = source.SurgePower,
            GlobalSurge = source.GlobalSurge,
            ReducedMotion = source.ReducedMotion,
            Contributors = source.Contributors,
            Expect = source.Expect ?? new CombatLabExpectations(),
        };
    }

    private static bool TryLoadScenarioSuite(string scenarioFile, out CombatLabScenarioSuite suite, out string error)
    {
        suite = new CombatLabScenarioSuite();
        error = string.Empty;

        string fullPath = ResolvePath(scenarioFile);
        if (!File.Exists(fullPath))
        {
            error = $"File not found: {fullPath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var opts = JsonOptions();
            CombatLabScenarioSuite? parsed = JsonSerializer.Deserialize<CombatLabScenarioSuite>(json, opts);
            if (parsed == null)
            {
                error = "Scenario suite JSON deserialized to null.";
                return false;
            }

            suite = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryLoadTowerBenchmarkSuite(string benchmarkFile, out CombatLabTowerBenchmarkSuite suite, out string error)
    {
        suite = new CombatLabTowerBenchmarkSuite();
        error = string.Empty;

        string fullPath = ResolvePath(benchmarkFile);
        if (!File.Exists(fullPath))
        {
            error = $"File not found: {fullPath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var opts = JsonOptions();
            CombatLabTowerBenchmarkSuite? parsed = JsonSerializer.Deserialize<CombatLabTowerBenchmarkSuite>(json, opts);
            if (parsed == null)
            {
                error = "Tower benchmark JSON deserialized to null.";
                return false;
            }

            suite = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryLoadModifierBenchmarkSuite(string benchmarkFile, out CombatLabModifierBenchmarkSuite suite, out string error)
    {
        suite = new CombatLabModifierBenchmarkSuite();
        error = string.Empty;

        string fullPath = ResolvePath(benchmarkFile);
        if (!File.Exists(fullPath))
        {
            error = $"File not found: {fullPath}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var opts = JsonOptions();
            CombatLabModifierBenchmarkSuite? parsed = JsonSerializer.Deserialize<CombatLabModifierBenchmarkSuite>(json, opts);
            if (parsed == null)
            {
                error = "Modifier benchmark JSON deserialized to null.";
                return false;
            }

            suite = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static string ResolvePath(string path)
    {
        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return ProjectSettings.GlobalizePath(path);
        if (Path.IsPathRooted(path))
            return path;
        return Path.GetFullPath(path);
    }

    private static void WriteJson(string path, object payload)
    {
        string resolved = ResolvePath(path);
        string? dir = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(payload, JsonOptions());
        File.WriteAllText(resolved, json);
        GD.Print($"[LAB] Wrote report: {resolved}");
    }

    private static void WriteText(string path, string payload)
    {
        string resolved = ResolvePath(path);
        string? dir = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(resolved, payload);
    }

    private static string? GetArgValue(string[] args, string key)
    {
        int idx = Array.IndexOf(args, key);
        if (idx >= 0 && idx + 1 < args.Length)
            return args[idx + 1];
        return null;
    }
}
