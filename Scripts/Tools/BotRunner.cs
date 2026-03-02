using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;

namespace SlotTheory.Tools;

/// <summary>
/// Orchestrates multiple bot runs, records results, and prints a balancing report.
/// Call RecordWaveEnd() after each wave and RecordResult() on win/loss.
/// HasMoreRuns tells GameController whether to restart or quit.
/// </summary>
public class BotRunner
{
    private record RunResult(
        BotStrategy Strategy,
        bool        Won,
        int         WaveReached,   // 1-based; last wave fought (20 = won)
        int         LivesEnd,
        int[]       WaveLives,     // lives after each completed wave (index 0 = wave 1)
        string[]    Towers,
        string[]    Mods
    );

    private readonly int              _totalRuns;
    private readonly BotStrategy[]    _strategies;
    private readonly List<RunResult>  _results = new();

    // state for the run currently in progress
    private BotStrategy       _curStrategy;
    private readonly List<int> _waveLives = new();

    public BotPlayer CurrentBot  { get; private set; } = null!;
    public bool      HasMoreRuns => _results.Count < _totalRuns;

    public BotRunner(int totalRuns)
    {
        _totalRuns  = totalRuns;
        _strategies = (BotStrategy[])Enum.GetValues(typeof(BotStrategy));
        StartNextRun();
    }

    private void StartNextRun()
    {
        int idx      = _results.Count;
        _curStrategy = _strategies[idx % _strategies.Length];
        CurrentBot   = new BotPlayer(_curStrategy, idx * 7919);
        _waveLives.Clear();
        GD.Print($"[BOT] Run {idx + 1}/{_totalRuns} — {_curStrategy}");
    }

    /// <summary>Call after each wave completes, before WaveIndex increments.</summary>
    public void RecordWaveEnd(int lives) => _waveLives.Add(lives);

    /// <summary>Call on win or loss. Automatically prepares the next run if needed.</summary>
    public void RecordResult(bool won, int waveReached, RunState state)
    {
        var towers = state.Slots
            .Where(s => s.Tower != null)
            .Select(s => s.Tower!.TowerId)
            .ToArray();
        var mods = state.Slots
            .Where(s => s.Tower != null)
            .SelectMany(s => s.Tower!.Modifiers.Select(m => m.ModifierId))
            .ToArray();

        _results.Add(new RunResult(
            _curStrategy, won, waveReached, state.Lives,
            [.. _waveLives], towers, mods));

        if (HasMoreRuns) StartNextRun();
    }

    public void PrintSummary()
    {
        int total = _results.Count;
        GD.Print("");
        GD.Print("╔══════════════════════════════════════════════════════════════════╗");
        GD.Print($"║  SLOT THEORY PLAYTEST — {total} runs across {_strategies.Length} strategies{Pad(total, _strategies.Length)}║");
        GD.Print("╚══════════════════════════════════════════════════════════════════╝");
        GD.Print("");

        // ── Per-strategy table ─────────────────────────────────────────────
        GD.Print($"{"STRATEGY",-16} {"RUNS",5} {"WINS",5} {"WIN%",6} {"AVG WAVE",10} {"AVG LIVES",10}");
        GD.Print(new string('─', 54));
        foreach (var strat in _strategies)
        {
            var runs = _results.Where(r => r.Strategy == strat).ToList();
            if (runs.Count == 0) continue;
            int wins       = runs.Count(r => r.Won);
            float winPct   = wins * 100f / runs.Count;
            float avgWave  = (float)runs.Average(r => r.WaveReached);
            float avgLives = (float)runs.Average(r => r.LivesEnd);
            GD.Print($"{strat,-16} {runs.Count,5} {wins,5} {winPct,5:0}% {avgWave,10:0.0} {avgLives,10:0.0}");
        }
        GD.Print("");
        GD.Print($"Overall: {_results.Count(r => r.Won)}/{total} wins ({_results.Count(r => r.Won) * 100 / total}%)");
        GD.Print("");

        // ── Wave difficulty ────────────────────────────────────────────────
        GD.Print("WAVE DIFFICULTY — avg lives remaining after each wave:");
        GD.Print(new string('─', 62));
        for (int w = 0; w < Balance.TotalWaves; w++)
        {
            var samples = _results
                .Where(r => r.WaveLives.Length > w)
                .Select(r => r.WaveLives[w])
                .ToList();
            if (samples.Count == 0) break;
            float avg     = (float)samples.Average();
            int   reached = samples.Count;
            int   lost    = w == 0
                ? samples.Count(l => l < Balance.StartingLives)
                : _results.Count(r => r.WaveLives.Length > w && r.WaveLives[w] < r.WaveLives[w - 1]);
            string bar = new string('█', (int)(avg * 20 / Balance.StartingLives));
            GD.Print($"  Wave {w + 1,2}: {avg,4:0.0} lives  [{bar,-20}]  {reached,3} runs reached  {lost} took damage");
        }
        GD.Print("");

        // ── Tower usage ────────────────────────────────────────────────────
        GD.Print("TOWER USAGE (% of all runs where it was placed):");
        foreach (var id in new[] { "rapid_shooter", "heavy_cannon", "marker_tower" })
        {
            int count = _results.Count(r => r.Towers.Contains(id));
            GD.Print($"  {id,-18} {count * 100 / total,3}%  ({count}/{total})");
        }
        GD.Print("");

        // ── Modifier usage ─────────────────────────────────────────────────
        GD.Print("MODIFIER USAGE (% of all runs where it was applied):");
        var allModIds = _results.SelectMany(r => r.Mods).Distinct().OrderBy(x => x).ToList();
        foreach (var id in allModIds)
        {
            int count = _results.Count(r => r.Mods.Contains(id));
            GD.Print($"  {id,-20} {count * 100 / total,3}%  ({count}/{total})");
        }
        GD.Print("");

        // ── Worst waves (where most lives were lost on average) ────────────
        GD.Print("MOST DANGEROUS WAVES (avg lives lost that wave, all runs that reached it):");
        var waveDanger = new List<(int Wave, float AvgLost)>();
        for (int w = 0; w < Balance.TotalWaves; w++)
        {
            var losses = _results
                .Where(r => r.WaveLives.Length > w)
                .Select(r => w == 0
                    ? Balance.StartingLives - r.WaveLives[0]
                    : r.WaveLives[w - 1] - r.WaveLives[w])
                .ToList();
            if (losses.Count > 0)
                waveDanger.Add((w + 1, (float)losses.Average()));
        }
        foreach (var (wave, avgLost) in waveDanger.OrderByDescending(x => x.AvgLost).Take(5))
            GD.Print($"  Wave {wave,2}: avg {avgLost:0.00} lives lost per run");
    }

    private static string Pad(int runs, int strategies)
    {
        int taken = runs.ToString().Length + strategies.ToString().Length + 24;
        return new string(' ', Math.Max(0, 34 - taken));
    }
}
