using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Godot;

namespace SlotTheory.Tools;

public static class BotMetricsDeltaReporter
{
    private readonly record struct Snapshot(
        float WinRate,
        float AvgWaveReached,
        float AvgRunDurationSeconds,
        float AvgSurgesPerRun,
        float AvgStatusDetonations,
        float AvgResidueUptimeSeconds,
        float BaseDps,
        float SurgeDps,
        float ExplosionDps,
        float ResidueDps,
        float ExplosionSharePct,
        float PeakSimultaneousExplosions,
        float PeakSimultaneousHazards,
        float PeakHitStops);

    public static bool TryRunFromArgs(string[] args)
    {
        int idx = Array.IndexOf(args, "--metrics_delta");
        if (idx < 0)
            return false;

        if (idx + 2 >= args.Length)
        {
            Console.Error.WriteLine("[DELTA] Usage: --metrics_delta <baseline.json> <tuned.json> [--delta_out <path>]");
            return true;
        }

        string baselinePath = args[idx + 1];
        string tunedPath = args[idx + 2];
        string? outputPath = GetArgValue(args, "--delta_out");
        Run(baselinePath, tunedPath, outputPath);
        return true;
    }

    public static bool Run(string baselinePath, string tunedPath, string? outputPath = null)
    {
        if (!TryReadSnapshotFromFile(baselinePath, out Snapshot baseline, out string baseError))
        {
            Console.Error.WriteLine($"[DELTA] Failed to read baseline metrics: {baseError}");
            return false;
        }
        if (!TryReadSnapshotFromFile(tunedPath, out Snapshot tuned, out string tunedError))
        {
            Console.Error.WriteLine($"[DELTA] Failed to read tuned metrics: {tunedError}");
            return false;
        }

        string report = BuildDeltaText(baseline, tuned);
        Console.WriteLine("[DELTA] Baseline metrics file: " + ResolvePath(baselinePath));
        Console.WriteLine("[DELTA] Tuned metrics file: " + ResolvePath(tunedPath));
        Console.WriteLine(report);

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            try
            {
                string resolved = ResolvePath(outputPath);
                string? dir = Path.GetDirectoryName(resolved);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(resolved, report);
                Console.WriteLine("[DELTA] Report written: " + resolved);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DELTA] Failed to write report: {ex.Message}");
            }
        }

        return true;
    }

    private static string BuildDeltaText(Snapshot baseline, Snapshot tuned)
    {
        static string F(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.AppendLine($"baseline build: avg wave {F(baseline.AvgWaveReached, "0.0")}");
        sb.AppendLine($"after change: avg wave {F(tuned.AvgWaveReached, "0.0")}");
        sb.AppendLine($"explosion damage share: {F(baseline.ExplosionSharePct, "0.0")}% -> {F(tuned.ExplosionSharePct, "0.0")}%");
        sb.AppendLine($"surges per run: {F(baseline.AvgSurgesPerRun, "0.00")} -> {F(tuned.AvgSurgesPerRun, "0.00")}");
        sb.AppendLine($"win rate: {F(baseline.WinRate * 100f, "0.0")}% -> {F(tuned.WinRate * 100f, "0.0")}%");
        sb.AppendLine($"status detonations/run: {F(baseline.AvgStatusDetonations, "0.00")} -> {F(tuned.AvgStatusDetonations, "0.00")}");
        sb.AppendLine($"residue uptime/run: {F(baseline.AvgResidueUptimeSeconds, "0.00")}s -> {F(tuned.AvgResidueUptimeSeconds, "0.00")}s");
        sb.AppendLine($"run duration: {F(baseline.AvgRunDurationSeconds, "0.00")}s -> {F(tuned.AvgRunDurationSeconds, "0.00")}s");
        sb.AppendLine($"DPS split (base/surge/explosion/residue): " +
            $"{F(baseline.BaseDps, "0.0")}/{F(baseline.SurgeDps, "0.0")}/{F(baseline.ExplosionDps, "0.0")}/{F(baseline.ResidueDps, "0.0")} -> " +
            $"{F(tuned.BaseDps, "0.0")}/{F(tuned.SurgeDps, "0.0")}/{F(tuned.ExplosionDps, "0.0")}/{F(tuned.ResidueDps, "0.0")}");
        sb.Append($"frame-stress peaks (explosions/hazards/hitstops): " +
            $"{F(baseline.PeakSimultaneousExplosions, "0")}/{F(baseline.PeakSimultaneousHazards, "0")}/{F(baseline.PeakHitStops, "0")} -> " +
            $"{F(tuned.PeakSimultaneousExplosions, "0")}/{F(tuned.PeakSimultaneousHazards, "0")}/{F(tuned.PeakHitStops, "0")}");
        return sb.ToString();
    }

    private static bool TryReadSnapshotFromFile(string path, out Snapshot snapshot, out string error)
    {
        snapshot = default;
        error = string.Empty;
        string resolved = ResolvePath(path);
        if (!File.Exists(resolved))
        {
            error = $"File not found: {resolved}";
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(resolved));
            JsonElement summary = doc.RootElement.GetProperty("summary");

            float winRate = GetFloat(summary, "win_rate");
            float avgWave = GetFloat(summary, "avg_wave_reached");
            float avgDuration = GetFloat(summary, "avg_run_duration_seconds");
            float avgSurges = GetFloat(summary, "avg_surges_per_run");
            if (avgSurges <= 0f)
                avgSurges = GetFloat(summary, "avg_major_surges_per_run"); // Legacy metrics payloads.
            float avgStatusDet = GetFloat(summary, "avg_status_detonation_count");
            float avgResidue = GetFloat(summary, "avg_residue_uptime_seconds");

            JsonElement dpsSplit = summary.GetProperty("dps_split");
            float baseDps = GetFloat(dpsSplit, "base_attacks");
            float surgeDps = GetFloat(dpsSplit, "surge_core");
            float explosionDps = GetFloat(dpsSplit, "explosion_follow_ups");
            float residueDps = GetFloat(dpsSplit, "residue");
            float totalDps = baseDps + surgeDps + explosionDps + residueDps;
            float explosionShare = totalDps > 0.0001f ? (explosionDps + residueDps) * 100f / totalDps : 0f;

            JsonElement stress = summary.GetProperty("frame_stress_peaks");
            float peakExplosions = GetFloat(stress, "simultaneous_explosions");
            float peakHazards = GetFloat(stress, "simultaneous_active_hazards");
            float peakHitStops = GetFloat(stress, "simultaneous_hitstops_requested");

            snapshot = new Snapshot(
                winRate,
                avgWave,
                avgDuration,
                avgSurges,
                avgStatusDet,
                avgResidue,
                baseDps,
                surgeDps,
                explosionDps,
                residueDps,
                explosionShare,
                peakExplosions,
                peakHazards,
                peakHitStops);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static float GetFloat(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value))
            return 0f;

        if (value.ValueKind == JsonValueKind.Number)
            return value.GetSingle();
        if (value.ValueKind == JsonValueKind.String && float.TryParse(value.GetString(), out float parsed))
            return parsed;
        return 0f;
    }

    private static string ResolvePath(string path)
    {
        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
            return ProjectSettings.GlobalizePath(path);
        if (Path.IsPathRooted(path))
            return path;
        return Path.GetFullPath(path);
    }

    private static string? GetArgValue(string[] args, string key)
    {
        int idx = Array.IndexOf(args, key);
        if (idx >= 0 && idx + 1 < args.Length)
            return args[idx + 1];
        return null;
    }
}
