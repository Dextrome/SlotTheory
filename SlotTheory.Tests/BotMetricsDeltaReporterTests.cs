using System;
using System.IO;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class BotMetricsDeltaReporterTests
{
    [Fact]
    public void Run_GeneratesDeltaReportFromTwoMetricsFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "slot_theory_delta_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            string baselinePath = Path.Combine(tempDir, "baseline.json");
            string tunedPath = Path.Combine(tempDir, "tuned.json");
            string outPath = Path.Combine(tempDir, "delta.txt");

            File.WriteAllText(baselinePath, """
            {
              "summary": {
                "win_rate": 0.51,
                "avg_wave_reached": 18.4,
                "avg_run_duration_seconds": 225.0,
                "avg_major_surges_per_run": 0.70,
                "avg_status_detonation_count": 3.2,
                "avg_residue_uptime_seconds": 10.1,
                "dps_split": {
                  "base_attacks": 110.0,
                  "surge_core": 24.0,
                  "explosion_follow_ups": 16.0,
                  "residue": 6.0
                },
                "frame_stress_peaks": {
                  "simultaneous_explosions": 4,
                  "simultaneous_active_hazards": 5,
                  "simultaneous_hitstops_requested": 2
                }
              }
            }
            """);

            File.WriteAllText(tunedPath, """
            {
              "summary": {
                "win_rate": 0.63,
                "avg_wave_reached": 21.9,
                "avg_run_duration_seconds": 233.0,
                "avg_major_surges_per_run": 2.80,
                "avg_status_detonation_count": 6.9,
                "avg_residue_uptime_seconds": 15.0,
                "dps_split": {
                  "base_attacks": 95.0,
                  "surge_core": 32.0,
                  "explosion_follow_ups": 41.0,
                  "residue": 15.0
                },
                "frame_stress_peaks": {
                  "simultaneous_explosions": 7,
                  "simultaneous_active_hazards": 8,
                  "simultaneous_hitstops_requested": 3
                }
              }
            }
            """);

            bool ok = BotMetricsDeltaReporter.Run(baselinePath, tunedPath, outPath);
            Assert.True(ok);
            Assert.True(File.Exists(outPath));

            string report = File.ReadAllText(outPath);
            string normalized = report.Replace(',', '.');
            Assert.Contains("baseline build: avg wave 18.4", normalized);
            Assert.Contains("after change: avg wave 21.9", normalized);
            Assert.Contains("major surges per run: 0.70 -> 2.80", normalized);
            Assert.Contains("explosion damage share:", normalized);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
