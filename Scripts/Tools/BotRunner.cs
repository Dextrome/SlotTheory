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
        string      Map,
        bool        Won,
        int         WaveReached,   // 1-based; last wave fought (20 = won)
        int         LivesEnd,
        int[]       WaveLives,     // lives after each completed wave (index 0 = wave 1)
        string[]    Towers,
        string[]    Mods
    );

    private readonly int              _totalRuns;
    private readonly BotStrategy[]    _strategies;
    private readonly string[]         _maps = { "arena_classic", "gauntlet", "sprawl" };
    private readonly List<RunResult>  _results = new();

    // state for the run currently in progress
    private BotStrategy       _curStrategy;
    private string            _curMap = "random_map";
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
        // Cycle through (map, strategy) pairs
        int mapIdx   = (idx / _strategies.Length) % _maps.Length;
        _curMap      = _maps[mapIdx];
        _curStrategy = _strategies[idx % _strategies.Length];
        CurrentBot   = new BotPlayer(_curStrategy, idx * 7919);
        _waveLives.Clear();
        // Set the map for this bot run
        SlotTheory.UI.MapSelectPanel.SetPendingMapSelection(_curMap);
        GD.Print($"[BOT] Run {idx + 1}/{_totalRuns} — {_curStrategy} on {_curMap}");
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
            _curStrategy, _curMap, won, waveReached, state.Lives,
            [.. _waveLives], towers, mods));

        if (HasMoreRuns) StartNextRun();
    }

    public void PrintSummary()
    {
        int total = _results.Count;
        var allMaps = _results.Select(r => r.Map).Distinct().OrderBy(m => m).ToList();

        GD.Print("");
        GD.Print("╔══════════════════════════════════════════════════════════════════╗");
        GD.Print($"║  SLOT THEORY PLAYTEST — {total} runs across {_strategies.Length} strategies   ║");
        GD.Print("╚══════════════════════════════════════════════════════════════════╝");
        GD.Print("");

        // ── Overall per-strategy table ─────────────────────────────────────────────
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

        // ── Per-map detailed reports ───────────────────────────────────────────────
        foreach (var mapId in allMaps)
        {
            var mapResults = _results.Where(r => r.Map == mapId).ToList();
            int mapTotal = mapResults.Count;
            int mapWins = mapResults.Count(r => r.Won);
            var mapName = mapId switch
            {
                "arena_classic" => "Crossroads",
                "gauntlet" => "Pinch & Bleed",
                "sprawl" => "Orbit",
                _ => mapId
            };

            GD.Print($"");
            GD.Print($"┌─ {mapName.ToUpper()} ({mapId}) — {mapTotal} runs ({mapWins}/{mapTotal} wins, {mapWins * 100 / mapTotal}%) ─┐");
            GD.Print($"│ STRATEGY          RUNS  WINS   WIN%   AVG WAVE  AVG LIVES                    │");
            GD.Print($"├{'─' * 70}┤");

            foreach (var strat in _strategies)
            {
                var stratRuns = mapResults.Where(r => r.Strategy == strat).ToList();
                if (stratRuns.Count == 0) continue;
                int wins       = stratRuns.Count(r => r.Won);
                float winPct   = wins * 100f / stratRuns.Count;
                float avgWave  = (float)stratRuns.Average(r => r.WaveReached);
                float avgLives = (float)stratRuns.Average(r => r.LivesEnd);
                GD.Print($"│ {strat,-16} {stratRuns.Count,4}  {wins,4}  {winPct,4:0}%  {avgWave,8:0.0}  {avgLives,9:0.0}                      │");
            }

            // Per-map wave difficulty
            GD.Print($"│ WAVE DIFFICULTY:                                               │");
            for (int w = 0; w < Balance.TotalWaves; w++)
            {
                var samples = mapResults
                    .Where(r => r.WaveLives.Length > w)
                    .Select(r => r.WaveLives[w])
                    .ToList();
                if (samples.Count == 0) break;
                float avg = (float)samples.Average();
                string bar = new string('█', (int)(avg * 10 / Balance.StartingLives));
                GD.Print($"│   Wave {w + 1,2}: {avg,4:0.0} lives  [{bar,-10}]                                 │");
            }

            // Per-map loss distribution
            var mapLosses = mapResults.Where(r => !r.Won).ToList();
            if (mapLosses.Count > 0)
            {
                GD.Print($"│ {mapLosses.Count} LOSSES:                                                      │");
                var byWave = mapLosses.GroupBy(r => r.WaveReached).OrderBy(g => g.Key);
                foreach (var g in byWave)
                {
                    string bar = new string('█', g.Count());
                    GD.Print($"│   Wave {g.Key,2}: {g.Count()} loss(es) {bar}                                │");
                }
            }

            GD.Print($"└{'─' * 70}┘");
        }

        GD.Print("");
    }
}
