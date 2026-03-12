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
        string? outputFile = GetArgValue(userArgs, "--lab_out");

        if (!string.IsNullOrWhiteSpace(scenarioFile))
            return RunScenarioFile(scenarioFile!, outputFile);
        if (!string.IsNullOrWhiteSpace(sweepFile))
            return RunSweepFile(sweepFile!, outputFile);

        GD.PrintErr("[LAB] Missing --lab_scenario <path> or --lab_sweep <path>.");
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

    private static string? GetArgValue(string[] args, string key)
    {
        int idx = Array.IndexOf(args, key);
        if (idx >= 0 && idx + 1 < args.Length)
            return args[idx + 1];
        return null;
    }
}
