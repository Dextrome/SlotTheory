using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Tools;

/// <summary>
/// Orchestrates multiple bot runs, records results, and prints a balancing report.
/// Call RecordWaveEnd() after each wave and RecordResult() on win/loss.
/// HasMoreRuns tells GameController whether to restart or quit.
/// </summary>
public class BotRunner
{
    public const string StrategySetAll = "all";
    public const string StrategySetOptimization = "optimization";
    public const string StrategySetEdge = "edge";
    public const string StrategySetSpectacle = "spectacle";

    private static readonly BotStrategy[] OptimizationStrategies =
    {
        BotStrategy.GreedyDps,
        BotStrategy.MarkerSynergy,
        BotStrategy.SpectacleComboPairing,
        BotStrategy.PlayerStyleKenny,
    };

    private static readonly BotStrategy[] EdgeStrategies =
    {
        BotStrategy.TowerFirst,
        BotStrategy.ChainFocus,
        BotStrategy.HeavyStack,
    };

    // Spectacle strategy set: only drafts that consistently build Overkill + Chain combos,
    // ensuring explosion/residue/detonation systems actually fire during evaluation.
    private static readonly BotStrategy[] SpectacleStrategies =
    {
        BotStrategy.ChainFocus,
        BotStrategy.SpectacleSingleStack,
        BotStrategy.SpectacleComboPairing,
        BotStrategy.SpectacleTriadDiversity,
    };

    private record RunTrace(
        int RunIndex,
        BotStrategy Strategy,
        string Map,
        DifficultyMode Difficulty,
        List<CombatLabTraceEvent> Events);

    private record RunResult(
        BotStrategy Strategy,
        string      Map,
        bool        Won,
        int         WaveReached,   // 1-based; last wave fought (20 = won)
        int         LivesEnd,
        int[]       WaveLives,     // lives after each completed wave (index 0 = wave 1)
        float[]     WaveInRange,   // avg enemies in range per step, per wave
        int[]       WaveSteps,     // bot steps to complete each wave
        string[]    Towers,
        string[]    Mods,
        DifficultyMode Difficulty,
        int[]       SlotDamage,    // total damage per slot index (6 entries)
        float[]     SlotFireRate,  // fire rate utilisation per slot (0-1); -1 = no tower in slot
        Dictionary<string, int>   LeaksByType,   // total leaks per enemy type
        Dictionary<string, float> LeakHpByType,  // summed HP remaining on leaked enemies
        int SpectacleSurgeTriggers,
        int SpectacleGlobalTriggers,
        Dictionary<string, int> SpectacleSurgeByEffect,
        Dictionary<string, int> SpectacleGlobalByEffect,
        Dictionary<string, int> SpectacleSurgeByTower,
        Dictionary<string, float> SpectacleFirstSurgeTimeByTower,
        Dictionary<string, float> SpectacleRechargeSecondsTotalByTower,
        Dictionary<string, int> SpectacleRechargeCountByTower,
        float RunDurationSeconds,
        int BaseAttackDamage,
        int SurgeCoreDamage,
        int ExplosionFollowUpDamage,
        int ResidueDamage,
        int SpectacleKills,
        int SpectacleExplosionBurstCount,
        int OverkillBloomCount,
        int StatusDetonationCount,
        int SpectacleMaxChainDepth,
        float ResidueUptimeSeconds,
        float[] KillDepthSamples,
        int[] SpectacleChainSizeSamples,
        int PeakSimultaneousExplosions,
        int PeakSimultaneousActiveHazards,
        int PeakSimultaneousHitStopsRequested
    );

    private readonly record struct PercentileStats(
        int SampleCount,
        float Average,
        float P25,
        float P50,
        float P75,
        float P90,
        float P99);

    private const float DamageConcentrationWarningThreshold = 0.60f;
    private const float DamageConcentrationProblemThreshold = 0.75f;

    private readonly int              _totalRuns;
    private readonly BotStrategy[]    _strategies;
    private readonly string[]         _maps;
	private readonly DifficultyMode? _targetDifficulty;
    private readonly string?          _forcedTowerId;
    private readonly string?          _forcedModifierId;
    private readonly string?          _metricsOutputPath;
    private readonly string?          _traceOutputPath;
    private readonly bool             _traceCaptureEnabled;
    private readonly string           _tuningLabel;
    private readonly string           _strategySetLabel = StrategySetAll;
    private readonly int              _runIndexOffset;
    private readonly bool             _fastMetrics;
    private readonly bool             _quiet;
	private DifficultyMode[] _difficulties = { DifficultyMode.Easy, DifficultyMode.Normal, DifficultyMode.Hard };
    private readonly List<RunResult>  _results = new();
    private readonly List<RunTrace>   _runTraces = new();
    private BotStrategy       _curStrategy;
    private DifficultyMode    _curDifficulty = DifficultyMode.Easy;
    private string            _curMap = "random_map";
    private readonly List<int>   _waveLives   = new();
    private readonly List<float> _waveInRange = new();
    private readonly List<int>   _waveSteps   = new();
    private readonly Dictionary<string, int>   _pickOffered = new();
    private readonly Dictionary<string, int>   _pickChosen  = new();

    public BotPlayer CurrentBot  { get; private set; } = null!;
    public bool      HasMoreRuns => _results.Count < _totalRuns;
    public int       CompletedRuns => _results.Count;
    public bool      FastMetrics => _fastMetrics;
    public bool      TraceCaptureEnabled => _traceCaptureEnabled;
    public bool      QuietMode => _quiet;

public BotRunner(
    int totalRuns,
    DifficultyMode? targetDifficulty = null,
    string? targetMap = null,
    BotStrategy? targetStrategy = null,
    string? forcedTowerId = null,
    string? forcedModifierId = null,
    string? metricsOutputPath = null,
    string? traceOutputPath = null,
    string? tuningLabel = null,
    string? strategySet = null,
    int runIndexOffset = 0,
    bool fastMetrics = false,
    bool quiet = false)
	{
		_totalRuns  = totalRuns;
		_strategies = targetStrategy.HasValue
            ? new[] { targetStrategy.Value }
            : ResolveStrategyPool(strategySet, out _strategySetLabel);
        if (targetStrategy.HasValue)
            _strategySetLabel = "single";
        _targetDifficulty = targetDifficulty;
        _forcedTowerId = string.IsNullOrWhiteSpace(forcedTowerId) ? null : forcedTowerId;
        _forcedModifierId = string.IsNullOrWhiteSpace(forcedModifierId) ? null : forcedModifierId;
        _metricsOutputPath = string.IsNullOrWhiteSpace(metricsOutputPath) ? null : metricsOutputPath;
        _traceOutputPath = string.IsNullOrWhiteSpace(traceOutputPath) ? null : traceOutputPath;
        _traceCaptureEnabled = !string.IsNullOrWhiteSpace(_traceOutputPath);
        _tuningLabel = string.IsNullOrWhiteSpace(tuningLabel) ? "baseline" : tuningLabel.Trim();
        _runIndexOffset = Math.Max(0, runIndexOffset);
        _fastMetrics = fastMetrics;
        _quiet = quiet;
		_maps = targetMap != null
			? new[] { targetMap }
			: Balance.IsDemo
				? new[] { "arena_classic", "gauntlet", "sprawl" }
				: new[] { "arena_classic", "gauntlet", "sprawl", "ridgeback" };
		// Filter difficulties if specific one requested
		if (targetDifficulty.HasValue)
			_difficulties = new[] { targetDifficulty.Value };
        StartNextRun();
    }

    private static BotStrategy[] ResolveStrategyPool(string? strategySet, out string resolvedLabel)
    {
        string normalized = (strategySet ?? StrategySetAll).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case StrategySetOptimization:
            case "opt":
                resolvedLabel = StrategySetOptimization;
                return OptimizationStrategies;
            case StrategySetEdge:
                resolvedLabel = StrategySetEdge;
                return EdgeStrategies;
            case StrategySetSpectacle:
                resolvedLabel = StrategySetSpectacle;
                return SpectacleStrategies;
            case StrategySetAll:
            case "":
                resolvedLabel = StrategySetAll;
                return (BotStrategy[])Enum.GetValues(typeof(BotStrategy));
            default:
                GD.PrintErr($"[BOT] Unknown strategy set '{strategySet}'. Falling back to '{StrategySetAll}'.");
                resolvedLabel = StrategySetAll;
                return (BotStrategy[])Enum.GetValues(typeof(BotStrategy));
        }
    }

    private void StartNextRun()
    {
        int idx      = _results.Count + _runIndexOffset;
        // Cycle through (map, difficulty, strategy) combinations
        int totalCombos = _strategies.Length * _difficulties.Length;
        int mapIdx   = (idx / totalCombos) % _maps.Length;
        int comboIdx = idx % totalCombos;
        int diffIdx  = comboIdx / _strategies.Length;
        int stratIdx = comboIdx % _strategies.Length;
        
        _curMap      = _maps[mapIdx];
        _curDifficulty = _difficulties[diffIdx];
        _curStrategy = _strategies[stratIdx];
        
        // Set difficulty for this run
        SettingsManager.Instance?.SetDifficulty(_curDifficulty);
        
        CurrentBot = new BotPlayer(_curStrategy, idx * 7919, _forcedTowerId, _forcedModifierId);
        _waveLives.Clear();
        _waveInRange.Clear();
        _waveSteps.Clear();
        
        // Set the map for this bot run
        SlotTheory.UI.MapSelectPanel.SetPendingMapSelection(_curMap);
        string forcedLabel = (_forcedTowerId != null || _forcedModifierId != null)
            ? $" [forced tower={_forcedTowerId ?? "any"}, mod={_forcedModifierId ?? "any"}]"
            : "";
        int localRunNumber = _results.Count + 1;
        int globalRunNumber = idx + 1;
        bool shouldLog =
            !_quiet
            || localRunNumber == 1
            || localRunNumber == _totalRuns
            || (localRunNumber % 100) == 0;
        if (shouldLog)
            GD.Print($"[BOT] Run {localRunNumber}/{_totalRuns} (global #{globalRunNumber}) - {_curStrategy} on {_curMap} ({_curDifficulty}){forcedLabel}");
    }

    /// <summary>Call after each wave completes, before WaveIndex increments.</summary>
    public void RecordWaveEnd(int lives, float avgInRange = 0f, int waveSteps = 0)
    {
        _waveLives.Add(lives);
        _waveInRange.Add(avgInRange);
        _waveSteps.Add(waveSteps);
    }

    /// <summary>Records which cards were offered and which was chosen (bot mode only).</summary>
    public void RecordPick(List<DraftOption> options, string? chosenId)
    {
        foreach (var opt in options)
        {
            _pickOffered.TryGetValue(opt.Id, out int o);
            _pickOffered[opt.Id] = o + 1;
        }
        if (chosenId != null)
        {
            _pickChosen.TryGetValue(chosenId, out int c);
            _pickChosen[chosenId] = c + 1;
        }
    }

    public void RecordRunTrace(IReadOnlyList<CombatLabTraceEvent> events)
    {
        if (!_traceCaptureEnabled || events == null || events.Count == 0)
            return;

        var copied = events.Select(CloneTraceEvent).ToList();
        _runTraces.Add(new RunTrace(
            RunIndex: _results.Count + 1,
            Strategy: _curStrategy,
            Map: _curMap,
            Difficulty: _curDifficulty,
            Events: copied));
    }

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

        var slotDamage   = new int[Balance.SlotCount];
        var slotFireRate = new float[Balance.SlotCount];
        for (int i = 0; i < Balance.SlotCount; i++)
        {
            slotDamage[i]   = state.GetTowerTotalDamage(i);
            slotFireRate[i] = state.SlotEligibleSteps[i] > 0
                ? (float)state.SlotFiredSteps[i] / state.SlotEligibleSteps[i]
                : (state.Slots[i].Tower != null ? 0f : -1f);  // -1 = no tower placed in slot
        }

        _results.Add(new RunResult(
            _curStrategy, _curMap, won, waveReached, state.Lives,
            [.. _waveLives], [.. _waveInRange], [.. _waveSteps], towers, mods, _curDifficulty,
            slotDamage, slotFireRate,
            new Dictionary<string, int>(state.TotalLeaksByType),
            new Dictionary<string, float>(state.TotalLeakHpByType),
            state.SpectacleSurgeTriggers,
            state.SpectacleGlobalTriggers,
            new Dictionary<string, int>(state.SpectacleSurgeByEffect),
            new Dictionary<string, int>(state.SpectacleGlobalByEffect),
            new Dictionary<string, int>(state.SpectacleSurgeByTower),
            new Dictionary<string, float>(state.SpectacleFirstSurgeTimeByTower),
            new Dictionary<string, float>(state.SpectacleRechargeSecondsTotalByTower),
            new Dictionary<string, int>(state.SpectacleRechargeCountByTower),
            state.TotalPlayTime,
            state.BaseAttackDamage,
            state.SurgeCoreDamage,
            state.ExplosionFollowUpDamage,
            state.ResidueDamage,
            state.SpectacleKills,
            state.SpectacleExplosionBurstCount,
            state.OverkillBloomCount,
            state.StatusDetonationCount,
            state.SpectacleMaxChainDepth,
            state.ResidueUptimeSeconds,
            [.. state.KillDepthSamples],
            [.. state.SpectacleChainSizeSamples],
            state.PeakSimultaneousExplosions,
            state.PeakSimultaneousActiveHazards,
            state.PeakSimultaneousHitStopsRequested));

        if (HasMoreRuns) StartNextRun();
    }

    public void PrintSummary()
    {
        int total = _results.Count;
        if (_quiet)
        {
            int wins = _results.Count(r => r.Won);
            float winRate = total > 0 ? wins * 100f / total : 0f;
            GD.Print($"[BOT] Completed {total} runs. Wins={wins} ({winRate:0.0}%).");
            WriteMetricsSummaryJson();
            WriteTraceJson();
            return;
        }

        var allMaps = _results.Select(r => r.Map).Distinct().OrderBy(m => m).ToList();

        GD.Print("");
        GD.Print("+==================================================================+");
        GD.Print($"|  SLOT THEORY PLAYTEST - {total} runs across {_strategies.Length} strategies × {_difficulties.Length} difficulties   |");
        GD.Print("+==================================================================+");
        GD.Print("");

        // ── Overall per-strategy table ─────────────────────────────────────────────
        GD.Print($"{"STRATEGY",-16} {"RUNS",5} {"WINS",5} {"WIN%",6} {"AVG WAVE",10} {"AVG LIVES",10}");
        GD.Print(new string('-', 54));
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
        
        // ── Per-difficulty breakdown ─────────────────────────────────────────────
        GD.Print("");
        GD.Print("DIFFICULTY BREAKDOWN:");
        foreach (var difficulty in _difficulties)
        {
            var diffResults = _results.Where(r => r.Difficulty == difficulty).ToList();
            if (diffResults.Count == 0) continue;
            int diffWins = diffResults.Count(r => r.Won);
            float diffWinPct = diffWins * 100f / diffResults.Count;
            GD.Print($"{difficulty}: {diffWins}/{diffResults.Count} wins ({diffWinPct:0}%)");
        }
        GD.Print("");
        // Competitive overall excludes Random and TowerFirst (skill-independent baselines)
        var competitive = _results.Where(r => r.Strategy != BotStrategy.Random && r.Strategy != BotStrategy.TowerFirst).ToList();
        int compWins = competitive.Count(r => r.Won);
        int compPct = competitive.Count > 0 ? (int)(compWins * 100f / competitive.Count) : 0;
        int allWins = _results.Count(r => r.Won);
        int allPct  = total > 0 ? (int)(allWins * 100f / total) : 0;
        GD.Print($"Overall (competitive): {compWins}/{competitive.Count} wins ({compPct}%)");
        GD.Print($"Overall (all):         {allWins}/{total} wins ({allPct}%)");
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
                "ridgeback" => "Ridgeback",
                _ => mapId
            };

            GD.Print($"");
            GD.Print($"+- {mapName.ToUpper()} ({mapId}) - {mapTotal} runs ({mapWins}/{mapTotal} wins, {mapWins * 100 / mapTotal}%) -+");
            GD.Print($"| STRATEGY          RUNS  WINS   WIN%   AVG WAVE  AVG LIVES                    |");
            GD.Print($"+{new string('-', 70)}+");

            foreach (var strat in _strategies)
            {
                var stratRuns = mapResults.Where(r => r.Strategy == strat).ToList();
                if (stratRuns.Count == 0) continue;
                int wins       = stratRuns.Count(r => r.Won);
                float winPct   = wins * 100f / stratRuns.Count;
                float avgWave  = (float)stratRuns.Average(r => r.WaveReached);
                float avgLives = (float)stratRuns.Average(r => r.LivesEnd);
                GD.Print($"| {strat,-16} {stratRuns.Count,4}  {wins,4}  {winPct,4:0}%  {avgWave,8:0.0}  {avgLives,9:0.0}                      |");
            }
            
            // Per-difficulty breakdown for this map
            GD.Print($"| DIFFICULTY BREAKDOWN:                                  |");
            foreach (var difficulty in _difficulties)
            {
                var diffMapResults = mapResults.Where(r => r.Difficulty == difficulty).ToList();
                if (diffMapResults.Count == 0) continue;
                int diffWins = diffMapResults.Count(r => r.Won);
                float diffWinPct = diffWins * 100f / diffMapResults.Count;
                GD.Print($"|   {difficulty}: {diffWins}/{diffMapResults.Count} wins ({diffWinPct:0}%)                                       |");
            }

            // Per-map wave difficulty
            GD.Print($"| WAVE DIFFICULTY (lives / avg enemies in range / steps):          |");
            for (int w = 0; w < Balance.TotalWaves; w++)
            {
                var liveSamples = mapResults
                    .Where(r => r.WaveLives.Length > w)
                    .Select(r => r.WaveLives[w])
                    .ToList();
                if (liveSamples.Count == 0) break;
                float avgLives = (float)liveSamples.Average();

                var densitySamples = mapResults
                    .Where(r => r.WaveInRange.Length > w)
                    .Select(r => r.WaveInRange[w])
                    .ToList();
                float avgInRange = densitySamples.Count > 0 ? (float)densitySamples.Average() : 0f;

                var stepSamples = mapResults
                    .Where(r => r.WaveSteps.Length > w)
                    .Select(r => r.WaveSteps[w])
                    .ToList();
                float avgSteps = stepSamples.Count > 0 ? (float)stepSamples.Average() : 0f;

                GD.Print($"|   Wave {w + 1,2}: {avgLives,4:0.0} lives  in-range={avgInRange,4:0.0}  steps={avgSteps,5:0}  |");
            }

            // Per-map loss distribution
            var mapLosses = mapResults.Where(r => !r.Won).ToList();
            if (mapLosses.Count > 0)
            {
                GD.Print($"| {mapLosses.Count} LOSSES:                                                      |");
                var byWave = mapLosses.GroupBy(r => r.WaveReached).OrderBy(g => g.Key);
                foreach (var g in byWave)
                {
                    string bar = new string('*', g.Count());
                    GD.Print($"|   Wave {g.Key,2}: {g.Count()} loss(es) {bar}                                |");
                }
            }

            GD.Print($"+{new string('-', 70)}+");
        }

        // ── Per-modifier win rates ─────────────────────────────────────────────
        var allMods = _results.SelectMany(r => r.Mods).Distinct().OrderBy(m => m).ToList();
        GD.Print("\nMODIFIER WIN RATES:");
        GD.Print($"{"MODIFIER",-18} {"RUNS",5} {"WINS",5} {"WIN%",6} {"AVG WAVE",10} {"AVG LIVES",10}");
        GD.Print(new string('-', 54));
        foreach (var mod in allMods)
        {
            var runs = _results.Where(r => r.Mods.Contains(mod)).ToList();
            if (runs.Count == 0) continue;
            int wins = runs.Count(r => r.Won);
            float winPct = wins * 100f / runs.Count;
            float avgWave = (float)runs.Average(r => r.WaveReached);
            float avgLives = (float)runs.Average(r => r.LivesEnd);
            GD.Print($"{mod,-18} {runs.Count,5} {wins,5} {winPct,5:0}% {avgWave,10:0.0} {avgLives,10:0.0}");
        }

        // ── Per-tower win rates ─────────────────────────────────────────────
        var allTowers = _results.SelectMany(r => r.Towers).Distinct().OrderBy(t => t).ToList();
        GD.Print("\nTOWER WIN RATES:");
        GD.Print($"{"TOWER",-18} {"RUNS",5} {"WINS",5} {"WIN%",6} {"AVG WAVE",10} {"AVG LIVES",10}");
        GD.Print(new string('-', 54));
        foreach (var tower in allTowers)
        {
            var runs = _results.Where(r => r.Towers.Contains(tower)).ToList();
            if (runs.Count == 0) continue;
            int wins = runs.Count(r => r.Won);
            float winPct = wins * 100f / runs.Count;
            float avgWave = (float)runs.Average(r => r.WaveReached);
            float avgLives = (float)runs.Average(r => r.LivesEnd);
            GD.Print($"{tower,-18} {runs.Count,5} {wins,5} {winPct,5:0}% {avgWave,10:0.0} {avgLives,10:0.0}");
        }

        GD.Print("");

        // ── Leak analysis ─────────────────────────────────────────────────────────
        GD.Print("\nLEAK ANALYSIS (all runs):");
        var allLeakTypes = _results.SelectMany(r => r.LeaksByType.Keys).Distinct().OrderBy(k => k).ToList();
        if (allLeakTypes.Count == 0)
        {
            GD.Print("  No leaks recorded.");
        }
        else
        {
            GD.Print($"  {"TYPE",-18} {"TOTAL LEAKS",12} {"AVG HP REMAINING",18}");
            GD.Print("  " + new string('-', 50));
            foreach (var t in allLeakTypes)
            {
                int totalLeaks = _results.Sum(r => r.LeaksByType.GetValueOrDefault(t, 0));
                float totalHp  = _results.Sum(r => r.LeakHpByType.GetValueOrDefault(t, 0f));
                float avgHp    = totalLeaks > 0 ? totalHp / totalLeaks : 0f;
                GD.Print($"  {t,-18} {totalLeaks,12} {avgHp,18:0.0}");
            }
        }

        // ── Per-slot damage & fire rate ───────────────────────────────────────────
        GD.Print("\nSPECTACLE TRIGGER ANALYSIS (all runs):");
        GD.Print("  Only surge and global surge triggers are active.");
        int totalSurgeTriggers = _results.Sum(r => r.SpectacleSurgeTriggers);
        int totalGlobalTriggers = _results.Sum(r => r.SpectacleGlobalTriggers);
        float runCount = _results.Count;
        GD.Print($"  {"TIER",-12} {"TOTAL",8} {"AVG/RUN",9}");
        GD.Print("  " + new string('-', 34));
        GD.Print($"  {"Surge",-12} {totalSurgeTriggers,8} {(runCount > 0 ? totalSurgeTriggers / runCount : 0f),9:0.00}");
        GD.Print($"  {"Global Surge",-12} {totalGlobalTriggers,8} {(runCount > 0 ? totalGlobalTriggers / runCount : 0f),9:0.00}");

        var surgeEffectTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        var globalEffectTotals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var run in _results)
        {
            foreach (var kv in run.SpectacleSurgeByEffect)
            {
                surgeEffectTotals.TryGetValue(kv.Key, out int cur);
                surgeEffectTotals[kv.Key] = cur + kv.Value;
            }
            foreach (var kv in run.SpectacleGlobalByEffect)
            {
                globalEffectTotals.TryGetValue(kv.Key, out int cur);
                globalEffectTotals[kv.Key] = cur + kv.Value;
            }
        }

        void PrintTopEffects(string label, Dictionary<string, int> totals, int tierTotal)
        {
            GD.Print($"  {label}:");
            if (totals.Count == 0)
            {
                GD.Print("    (none)");
                return;
            }

            foreach (var kv in totals.OrderByDescending(k => k.Value).ThenBy(k => k.Key).Take(8))
            {
                float pct = tierTotal > 0 ? kv.Value * 100f / tierTotal : 0f;
                GD.Print($"    {kv.Key,-28} {kv.Value,6} ({pct,5:0.0}%)");
            }
        }

        PrintTopEffects("Top Surge Effects", surgeEffectTotals, totalSurgeTriggers);
        PrintTopEffects("Top Global Surge Effects", globalEffectTotals, totalGlobalTriggers);

        GD.Print("\nSLOT DAMAGE & FIRE RATE UTILISATION (avg across runs with tower in slot):");
        GD.Print($"  {"SLOT",4} {"AVG DAMAGE",12} {"AVG FIRE RATE",14}");
        GD.Print("  " + new string('-', 32));
        for (int si = 0; si < Balance.SlotCount; si++)
        {
            var runsWithTower = _results.Where(r => r.SlotFireRate[si] >= 0f).ToList();
            if (runsWithTower.Count == 0) continue;
            float avgDmg  = (float)runsWithTower.Average(r => r.SlotDamage[si]);
            float avgRate = (float)runsWithTower.Average(r => r.SlotFireRate[si]);
            GD.Print($"  {si,4} {avgDmg,12:0} {avgRate,13:0.0%}");
        }

        // ── Card pick frequency ───────────────────────────────────────────────────
        GD.Print("\nCARD PICK FREQUENCY (offered → chosen):");
        GD.Print($"  {"CARD",-20} {"OFFERED",8} {"CHOSEN",8} {"PICK%",7}");
        GD.Print("  " + new string('-', 46));
        foreach (var kv in _pickOffered.OrderByDescending(kv => kv.Value))
        {
            _pickChosen.TryGetValue(kv.Key, out int chosen);
            float pickPct = chosen * 100f / kv.Value;
            GD.Print($"  {kv.Key,-20} {kv.Value,8} {chosen,8} {pickPct,6:0}%");
        }

        PrintAutomationMetrics();

        // ── Wave Difficulty Analysis for Balancing ─────────────────────────────────
        GD.Print("\n╔═══════════════════════════════════════════════════════════════════╗");
        GD.Print("║                    WAVE DIFFICULTY ANALYSIS                       ║");
        GD.Print("║  For balancing SpawnInterval, Tanky/Swift/Splitter/Reverse counts ║");
        GD.Print("╚═══════════════════════════════════════════════════════════════════╝");

        AnalyzeWaveDifficulty();
        WriteMetricsSummaryJson();
        WriteTraceJson();
    }

    private void PrintAutomationMetrics()
    {
        if (_results.Count == 0)
            return;

        float runCount = _results.Count;
        float avgDuration = (float)_results.Average(r => r.RunDurationSeconds);
        float avgSurges = (float)_results.Average(r => r.SpectacleSurgeTriggers);
        float avgKillsPerSurge = (float)_results.Average(r =>
            r.SpectacleSurgeTriggers > 0 ? (float)r.SpectacleKills / r.SpectacleSurgeTriggers : 0f);

        int totalExplosionDamage = _results.Sum(r => r.ExplosionFollowUpDamage + r.ResidueDamage);
        int totalExplosionTriggers = _results.Sum(r => r.SpectacleExplosionBurstCount);
        float avgExplosionDamagePerRun = totalExplosionDamage / runCount;
        float avgExplosionDamagePerTrigger = totalExplosionTriggers > 0
            ? (float)totalExplosionDamage / totalExplosionTriggers
            : 0f;

        float avgStatusDetonations = (float)_results.Average(r => r.StatusDetonationCount);
        float avgResidueUptime = (float)_results.Average(r => r.ResidueUptimeSeconds);
        float avgChainDepth = (float)_results.Average(r => r.SpectacleMaxChainDepth);
        var topDamageShares = _results.Select(ResolveTopTowerDamageShare).ToList();
        PercentileStats damageConcentration = BuildPercentileStats(topDamageShares);
        var killDepthSamples = _results.SelectMany(r => r.KillDepthSamples).ToList();
        PercentileStats killDepth = BuildPercentileStats(killDepthSamples);
        var chainSizeSamples = _results.SelectMany(r => r.SpectacleChainSizeSamples).Select(v => (float)v).ToList();
        PercentileStats chainSize = BuildPercentileStats(chainSizeSamples);

        float avgBaseDps = (float)_results.Average(r => SafeDps(r.BaseAttackDamage, r.RunDurationSeconds));
        float avgSurgeDps = (float)_results.Average(r => SafeDps(r.SurgeCoreDamage, r.RunDurationSeconds));
        float avgExplosionDps = (float)_results.Average(r => SafeDps(r.ExplosionFollowUpDamage, r.RunDurationSeconds));
        float avgResidueDps = (float)_results.Average(r => SafeDps(r.ResidueDamage, r.RunDurationSeconds));

        int peakExplosions = _results.Max(r => r.PeakSimultaneousExplosions);
        int peakHazards = _results.Max(r => r.PeakSimultaneousActiveHazards);
        int peakHitStops = _results.Max(r => r.PeakSimultaneousHitStopsRequested);

        GD.Print("\nAUTOMATION METRICS (all runs):");
        GD.Print($"  Tuning profile: {_tuningLabel}");
        GD.Print($"  Strategy set: {_strategySetLabel} ({_strategies.Length} strategies)");
        GD.Print($"  Avg run duration: {avgDuration:0.00}s");
        GD.Print($"  Surges/run: {avgSurges:0.00}");
        GD.Print($"  Kills per surge: {avgKillsPerSurge:0.00}");
        GD.Print($"  Explosion damage/run: {avgExplosionDamagePerRun:0.0}");
        GD.Print($"  Explosion damage/trigger: {avgExplosionDamagePerTrigger:0.0}");
        GD.Print($"  Status detonations/run: {avgStatusDetonations:0.00}");
        GD.Print($"  Residue uptime/run: {avgResidueUptime:0.00}s");
        GD.Print($"  Max chain depth/run: {avgChainDepth:0.00}");
        GD.Print($"  Top tower damage share: p50={damageConcentration.P50 * 100f:0.0}% p90={damageConcentration.P90 * 100f:0.0}%");
        GD.Print($"  Kill depth: avg={killDepth.Average:0.000}, p25={killDepth.P25:0.000}, p50={killDepth.P50:0.000}, p75={killDepth.P75:0.000}");
        GD.Print($"  Chain size: median={chainSize.P50:0.0}, p90={chainSize.P90:0.0}, p99={chainSize.P99:0.0}");
        GD.Print($"  DPS split: base={avgBaseDps:0.00}, surge={avgSurgeDps:0.00}, explosion={avgExplosionDps:0.00}, residue={avgResidueDps:0.00}");
        GD.Print($"  Frame-stress peaks: explosions={peakExplosions}, hazards={peakHazards}, hitstops={peakHitStops}");
    }

    private void WriteMetricsSummaryJson()
    {
        if (_results.Count == 0 || string.IsNullOrWhiteSpace(_metricsOutputPath))
            return;

        try
        {
            string outputPath = _metricsOutputPath!;
            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            int totalExplosionDamage = _results.Sum(r => r.ExplosionFollowUpDamage + r.ResidueDamage);
            int totalExplosionTriggers = _results.Sum(r => r.SpectacleExplosionBurstCount);
            var surgesPerRun = _results.Select(r => r.SpectacleSurgeTriggers).ToList();
            var chainDepthPerRun = _results.Select(r => r.SpectacleMaxChainDepth).ToList();
            var waveReachedPerRun = _results.Select(r => r.WaveReached).ToList();
            var explosionSharePerRun = _results
                .Select(r => ResolveExplosionSharePerRun(r))
                .ToList();
            var topTowerDamageSharePerRun = _results
                .Select(ResolveTopTowerDamageShare)
                .ToList();
            PercentileStats damageConcentration = BuildPercentileStats(topTowerDamageSharePerRun);
            int healthyDamageConcentrationRuns = topTowerDamageSharePerRun.Count(v => v < DamageConcentrationWarningThreshold);
            int warningDamageConcentrationRuns = topTowerDamageSharePerRun.Count(v => v >= DamageConcentrationWarningThreshold && v < DamageConcentrationProblemThreshold);
            int problemDamageConcentrationRuns = topTowerDamageSharePerRun.Count(v => v >= DamageConcentrationProblemThreshold);
            float avgSurgeIntervalSeconds = ResolveAverageSurgeIntervalSeconds(_results);
            var placementsByTower = new Dictionary<string, int>(StringComparer.Ordinal);
            var surgesByTower = new Dictionary<string, int>(StringComparer.Ordinal);
            var firstSurgeSecondsTotalByTower = new Dictionary<string, float>(StringComparer.Ordinal);
            var firstSurgeSamplesByTower = new Dictionary<string, int>(StringComparer.Ordinal);
            var rechargeSecondsTotalByTower = new Dictionary<string, float>(StringComparer.Ordinal);
            var rechargeSamplesByTower = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (RunResult run in _results)
            {
                foreach (string towerId in run.Towers)
                {
                    placementsByTower.TryGetValue(towerId, out int current);
                    placementsByTower[towerId] = current + 1;
                }
                foreach (var kv in run.SpectacleSurgeByTower)
                {
                    surgesByTower.TryGetValue(kv.Key, out int current);
                    surgesByTower[kv.Key] = current + kv.Value;
                }
                foreach (var kv in run.SpectacleFirstSurgeTimeByTower)
                {
                    firstSurgeSecondsTotalByTower.TryGetValue(kv.Key, out float totalSeconds);
                    firstSurgeSecondsTotalByTower[kv.Key] = totalSeconds + kv.Value;
                    firstSurgeSamplesByTower.TryGetValue(kv.Key, out int sampleCount);
                    firstSurgeSamplesByTower[kv.Key] = sampleCount + 1;
                }
                foreach (var kv in run.SpectacleRechargeSecondsTotalByTower)
                {
                    rechargeSecondsTotalByTower.TryGetValue(kv.Key, out float totalSeconds);
                    rechargeSecondsTotalByTower[kv.Key] = totalSeconds + kv.Value;
                }
                foreach (var kv in run.SpectacleRechargeCountByTower)
                {
                    rechargeSamplesByTower.TryGetValue(kv.Key, out int sampleCount);
                    rechargeSamplesByTower[kv.Key] = sampleCount + kv.Value;
                }
            }
            var towerSurgeRows = placementsByTower.Keys
                .Union(surgesByTower.Keys, StringComparer.Ordinal)
                .Union(firstSurgeSamplesByTower.Keys, StringComparer.Ordinal)
                .Union(rechargeSamplesByTower.Keys, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id =>
                {
                    placementsByTower.TryGetValue(id, out int placements);
                    surgesByTower.TryGetValue(id, out int surges);
                    firstSurgeSecondsTotalByTower.TryGetValue(id, out float firstSurgeTotalSeconds);
                    firstSurgeSamplesByTower.TryGetValue(id, out int firstSurgeSamples);
                    rechargeSecondsTotalByTower.TryGetValue(id, out float rechargeTotalSeconds);
                    rechargeSamplesByTower.TryGetValue(id, out int rechargeSamples);
                    float surgesPerPlacedTower = placements > 0 ? (float)surges / placements : 0f;
                    return new
                    {
                        tower_id = id,
                        placements,
                        surges,
                        surges_per_placed_tower = surgesPerPlacedTower,
                        avg_time_to_first_surge_seconds = firstSurgeSamples > 0 ? firstSurgeTotalSeconds / firstSurgeSamples : 0f,
                        first_surge_samples = firstSurgeSamples,
                        avg_recharge_seconds = rechargeSamples > 0 ? rechargeTotalSeconds / rechargeSamples : 0f,
                        recharge_samples = rechargeSamples,
                    };
                })
                .ToList();

            var modifierSurgeStats = new Dictionary<string, (int Runs, int Surges)>(StringComparer.Ordinal);
            foreach (RunResult run in _results)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string modifierId in run.Mods)
                {
                    if (string.IsNullOrWhiteSpace(modifierId) || !seen.Add(modifierId))
                        continue;
                    modifierSurgeStats.TryGetValue(modifierId, out var row);
                    row.Runs += 1;
                    row.Surges += run.SpectacleSurgeTriggers;
                    modifierSurgeStats[modifierId] = row;
                }
            }
            var modifierSurgeRows = modifierSurgeStats
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv =>
                {
                    int runCount = kv.Value.Runs;
                    int surgeCount = kv.Value.Surges;
                    return new
                    {
                        modifier_id = kv.Key,
                        runs = runCount,
                        surges = surgeCount,
                        surges_per_run = runCount > 0 ? (float)surgeCount / runCount : 0f,
                    };
                })
                .ToList();
            var difficultyWinRates = Enum.GetValues<DifficultyMode>()
                .Select(difficulty =>
                {
                    var difficultyRuns = _results.Where(r => r.Difficulty == difficulty).ToList();
                    int runCount = difficultyRuns.Count;
                    int wins = difficultyRuns.Count(r => r.Won);
                    return new
                    {
                        difficulty = difficulty.ToString(),
                        runs = runCount,
                        wins,
                        win_rate = runCount > 0 ? (float)wins / runCount : 0f,
                    };
                })
                .Where(row => row.runs > 0)
                .ToList();

            var towerWinRates = BuildRunItemWinRateRows(_results, r => r.Towers);
            var modifierWinRates = BuildRunItemWinRateRows(_results, r => r.Mods);

            var summary = new Dictionary<string, object?>
            {
                ["win_rate"] = _results.Count > 0 ? (float)_results.Count(r => r.Won) / _results.Count : 0f,
                ["avg_wave_reached"] = _results.Count > 0 ? (float)_results.Average(r => r.WaveReached) : 0f,
                ["avg_run_duration_seconds"] = _results.Count > 0 ? (float)_results.Average(r => r.RunDurationSeconds) : 0f,
                ["avg_surges_per_run"] = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleSurgeTriggers) : 0f,
                ["avg_global_surges_per_run"] = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleGlobalTriggers) : 0f,
                ["avg_surge_interval_seconds"] = avgSurgeIntervalSeconds,
                ["avg_kills_per_surge"] = _results.Count > 0
                    ? (float)_results.Average(r => r.SpectacleSurgeTriggers > 0 ? (float)r.SpectacleKills / r.SpectacleSurgeTriggers : 0f)
                    : 0f,
                ["avg_explosion_damage_per_run"] = _results.Count > 0 ? (float)totalExplosionDamage / _results.Count : 0f,
                ["avg_explosion_damage_per_trigger"] = totalExplosionTriggers > 0 ? (float)totalExplosionDamage / totalExplosionTriggers : 0f,
                ["avg_status_detonation_count"] = _results.Count > 0 ? (float)_results.Average(r => r.StatusDetonationCount) : 0f,
                ["avg_residue_uptime_seconds"] = _results.Count > 0 ? (float)_results.Average(r => r.ResidueUptimeSeconds) : 0f,
                ["avg_max_chain_depth"] = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleMaxChainDepth) : 0f,
                ["dps_split"] = new
                {
                    base_attacks = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.BaseAttackDamage, r.RunDurationSeconds)) : 0f,
                    surge_core = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.SurgeCoreDamage, r.RunDurationSeconds)) : 0f,
                    explosion_follow_ups = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.ExplosionFollowUpDamage, r.RunDurationSeconds)) : 0f,
                    residue = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.ResidueDamage, r.RunDurationSeconds)) : 0f,
                },
                ["frame_stress_peaks"] = new
                {
                    simultaneous_explosions = _results.Max(r => r.PeakSimultaneousExplosions),
                    simultaneous_active_hazards = _results.Max(r => r.PeakSimultaneousActiveHazards),
                    simultaneous_hitstops_requested = _results.Max(r => r.PeakSimultaneousHitStopsRequested),
                },
                ["surges_by_tower"] = towerSurgeRows,
                ["surges_by_modifier"] = modifierSurgeRows,
                ["difficulty_win_rates"] = difficultyWinRates,
                ["tower_win_rates"] = towerWinRates,
                ["modifier_win_rates"] = modifierWinRates,
            };

            if (!_fastMetrics)
            {
                var allKillDepthSamples = _results
                    .SelectMany(r => r.KillDepthSamples)
                    .ToList();
                PercentileStats killDepthDistribution = BuildPercentileStats(allKillDepthSamples);
                var allChainReactionSizes = _results
                    .SelectMany(r => r.SpectacleChainSizeSamples)
                    .Select(v => (float)v)
                    .ToList();
                PercentileStats chainReactionSizeDistribution = BuildPercentileStats(allChainReactionSizes);
                const float explosionShareBinSize = 0.001f; // 0.10% bins

                summary["damage_concentration"] = new
                {
                    thresholds = new
                    {
                        warning_min = DamageConcentrationWarningThreshold,
                        problem_min = DamageConcentrationProblemThreshold,
                    },
                    avg_top_tower_share = damageConcentration.Average,
                    p25_top_tower_share = damageConcentration.P25,
                    p50_top_tower_share = damageConcentration.P50,
                    p75_top_tower_share = damageConcentration.P75,
                    p90_top_tower_share = damageConcentration.P90,
                    p99_top_tower_share = damageConcentration.P99,
                    healthy_runs = healthyDamageConcentrationRuns,
                    warning_runs = warningDamageConcentrationRuns,
                    problem_runs = problemDamageConcentrationRuns,
                };
                summary["kill_depth_distribution"] = new
                {
                    sample_count = killDepthDistribution.SampleCount,
                    avg = killDepthDistribution.Average,
                    p25 = killDepthDistribution.P25,
                    p50 = killDepthDistribution.P50,
                    p75 = killDepthDistribution.P75,
                    p90 = killDepthDistribution.P90,
                    p99 = killDepthDistribution.P99,
                };
                summary["chain_reaction_size_distribution"] = new
                {
                    sample_count = chainReactionSizeDistribution.SampleCount,
                    median = chainReactionSizeDistribution.P50,
                    p90 = chainReactionSizeDistribution.P90,
                    p99 = chainReactionSizeDistribution.P99,
                };
                summary["distributions"] = new
                {
                    surges_per_run = BuildDiscreteDistribution(surgesPerRun),
                    chain_depth_per_run = BuildDiscreteDistribution(chainDepthPerRun),
                    wave_reached_per_run = BuildDiscreteDistribution(waveReachedPerRun),
                    explosion_damage_share_per_run = new
                    {
                        bin_size_fraction = explosionShareBinSize,
                        bin_size_percent = explosionShareBinSize * 100f,
                        bins = BuildSparseBinnedDistribution(explosionSharePerRun, explosionShareBinSize),
                    },
                    top_tower_damage_share_per_run = new
                    {
                        bin_size_fraction = explosionShareBinSize,
                        bin_size_percent = explosionShareBinSize * 100f,
                        bins = BuildSparseBinnedDistribution(topTowerDamageSharePerRun, explosionShareBinSize),
                    },
                    kill_depth = new
                    {
                        bin_size_fraction = 0.01f,
                        bins = BuildSparseBinnedDistribution(allKillDepthSamples, 0.01f),
                    },
                    chain_reaction_size = BuildDiscreteDistribution(allChainReactionSizes.Select(v => (int)MathF.Round(v))),
                };
            }

            object runsPayload = _fastMetrics
                ? _results.Select(r => new
                {
                    difficulty = r.Difficulty.ToString(),
                    won = r.Won,
                    towers = r.Towers,
                    mods = r.Mods,
                    wave_reached = r.WaveReached,
                    run_duration_seconds = r.RunDurationSeconds,
                    surges = r.SpectacleSurgeTriggers,
                    global_surges = r.SpectacleGlobalTriggers,
                    surge_interval_seconds = ResolveSurgeIntervalSeconds(r),
                    status_detonations = r.StatusDetonationCount,
                    residue_uptime_seconds = r.ResidueUptimeSeconds,
                    max_chain_depth = r.SpectacleMaxChainDepth,
                    top_tower_damage_share = ResolveTopTowerDamageShare(r),
                    damage_split = new
                    {
                        base_attacks = r.BaseAttackDamage,
                        surge_core = r.SurgeCoreDamage,
                        explosion_follow_ups = r.ExplosionFollowUpDamage,
                        residue = r.ResidueDamage,
                    },
                    frame_stress = new
                    {
                        simultaneous_explosions = r.PeakSimultaneousExplosions,
                        simultaneous_active_hazards = r.PeakSimultaneousActiveHazards,
                        simultaneous_hitstops_requested = r.PeakSimultaneousHitStopsRequested,
                    },
                }).ToList()
                : _results.Select(r => new
                {
                    strategy = r.Strategy.ToString(),
                    map = r.Map,
                    difficulty = r.Difficulty.ToString(),
                    won = r.Won,
                    wave_reached = r.WaveReached,
                    run_duration_seconds = r.RunDurationSeconds,
                    surges = r.SpectacleSurgeTriggers,
                    global_surges = r.SpectacleGlobalTriggers,
                    towers = r.Towers,
                    mods = r.Mods,
                    surge_interval_seconds = ResolveSurgeIntervalSeconds(r),
                    surges_by_tower = r.SpectacleSurgeByTower,
                    first_surge_seconds_by_tower = r.SpectacleFirstSurgeTimeByTower,
                    recharge_seconds_total_by_tower = r.SpectacleRechargeSecondsTotalByTower,
                    recharge_count_by_tower = r.SpectacleRechargeCountByTower,
                    surges_by_effect = r.SpectacleSurgeByEffect,
                    global_surges_by_effect = r.SpectacleGlobalByEffect,
                    status_detonations = r.StatusDetonationCount,
                    residue_uptime_seconds = r.ResidueUptimeSeconds,
                    max_chain_depth = r.SpectacleMaxChainDepth,
                    top_tower_damage_share = ResolveTopTowerDamageShare(r),
                    kill_depth_samples = r.KillDepthSamples,
                    chain_reaction_sizes = r.SpectacleChainSizeSamples,
                    damage_split = new
                    {
                        base_attacks = r.BaseAttackDamage,
                        surge_core = r.SurgeCoreDamage,
                        explosion_follow_ups = r.ExplosionFollowUpDamage,
                        residue = r.ResidueDamage,
                    },
                    frame_stress = new
                    {
                        simultaneous_explosions = r.PeakSimultaneousExplosions,
                        simultaneous_active_hazards = r.PeakSimultaneousActiveHazards,
                        simultaneous_hitstops_requested = r.PeakSimultaneousHitStopsRequested,
                    },
                }).ToList();

            var payload = new Dictionary<string, object?>
            {
                ["generated_utc"] = DateTime.UtcNow,
                ["tuning_profile"] = _tuningLabel,
                ["run_count"] = _results.Count,
                ["summary"] = summary,
                ["runs"] = runsPayload,
            };

            var options = new JsonSerializerOptions { WriteIndented = !_fastMetrics };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, options));
            GD.Print($"[BOT] Metrics JSON written: {outputPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BOT] Failed to write metrics JSON: {ex.Message}");
        }
    }

    private static List<object> BuildRunItemWinRateRows(IEnumerable<RunResult> runs, Func<RunResult, IEnumerable<string>> selector)
    {
        var stats = new Dictionary<string, (int Runs, int Wins)>(StringComparer.Ordinal);

        foreach (RunResult run in runs)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in selector(run))
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    continue;
                stats.TryGetValue(id, out var row);
                row.Runs += 1;
                if (run.Won)
                    row.Wins += 1;
                stats[id] = row;
            }
        }

        return stats
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv =>
            {
                int runsForItem = kv.Value.Runs;
                int winsForItem = kv.Value.Wins;
                return (object)new
                {
                    item_id = kv.Key,
                    runs = runsForItem,
                    wins = winsForItem,
                    win_rate = runsForItem > 0 ? (float)winsForItem / runsForItem : 0f,
                };
            })
            .ToList();
    }

    private static float ResolveExplosionSharePerRun(RunResult run)
    {
        float total = run.BaseAttackDamage + run.SurgeCoreDamage + run.ExplosionFollowUpDamage + run.ResidueDamage;
        if (total <= 0.0001f)
            return 0f;
        return (run.ExplosionFollowUpDamage + run.ResidueDamage) / total;
    }

    private static float ResolveTopTowerDamageShare(RunResult run)
    {
        float total = run.SlotDamage.Where(v => v > 0).Sum();
        if (total <= 0.0001f)
            return 0f;
        float top = run.SlotDamage.Where(v => v > 0).DefaultIfEmpty(0).Max();
        return top / total;
    }

    private static float ResolveSurgeIntervalSeconds(RunResult run)
    {
        if (run.SpectacleSurgeTriggers <= 0)
            return run.RunDurationSeconds;
        return run.RunDurationSeconds / run.SpectacleSurgeTriggers;
    }

    private static float ResolveAverageSurgeIntervalSeconds(IEnumerable<RunResult> runs)
    {
        var intervals = runs
            .Where(r => r.SpectacleSurgeTriggers > 0)
            .Select(ResolveSurgeIntervalSeconds)
            .ToList();
        if (intervals.Count == 0)
            return 0f;
        return (float)intervals.Average();
    }

    private static List<object> BuildDiscreteDistribution(IEnumerable<int> values)
    {
        return values
            .GroupBy(v => v)
            .OrderBy(g => g.Key)
            .Select(g => (object)new
            {
                value = g.Key,
                count = g.Count(),
            })
            .ToList();
    }

    private static List<object> BuildSparseBinnedDistribution(IEnumerable<float> values, float binSize)
    {
        if (binSize <= 0f)
            return new List<object>();

        var buckets = new Dictionary<int, int>();
        foreach (float raw in values)
        {
            float value = MathF.Max(0f, raw);
            int binIndex = (int)MathF.Floor(value / binSize);
            if (buckets.TryGetValue(binIndex, out int count))
                buckets[binIndex] = count + 1;
            else
                buckets[binIndex] = 1;
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => (object)new
            {
                bin_start = kv.Key * binSize,
                bin_end = (kv.Key + 1) * binSize,
                bin_start_percent = kv.Key * binSize * 100f,
                bin_end_percent = (kv.Key + 1) * binSize * 100f,
                count = kv.Value,
            })
            .ToList();
    }

    private static PercentileStats BuildPercentileStats(IReadOnlyList<float> values)
    {
        if (values.Count == 0)
            return new PercentileStats(0, 0f, 0f, 0f, 0f, 0f, 0f);

        var sorted = values.OrderBy(v => v).ToArray();
        float avg = sorted.Average();

        return new PercentileStats(
            SampleCount: sorted.Length,
            Average: avg,
            P25: ResolveQuantile(sorted, 0.25f),
            P50: ResolveQuantile(sorted, 0.50f),
            P75: ResolveQuantile(sorted, 0.75f),
            P90: ResolveQuantile(sorted, 0.90f),
            P99: ResolveQuantile(sorted, 0.99f));
    }

    private static float ResolveQuantile(float[] sorted, float quantile)
    {
        if (sorted.Length == 0)
            return 0f;
        if (sorted.Length == 1)
            return sorted[0];

        float q = Math.Clamp(quantile, 0f, 1f);
        float index = q * (sorted.Length - 1);
        int lo = (int)MathF.Floor(index);
        int hi = (int)MathF.Ceiling(index);
        if (lo == hi)
            return sorted[lo];
        float t = index - lo;
        return Mathf.Lerp(sorted[lo], sorted[hi], t);
    }

    private static float SafeDps(int damage, float durationSeconds)
    {
        if (durationSeconds <= 0.0001f)
            return 0f;
        return damage / durationSeconds;
    }

    private void WriteTraceJson()
    {
        if (_runTraces.Count == 0 || string.IsNullOrWhiteSpace(_traceOutputPath))
            return;

        try
        {
            string outputPath = _traceOutputPath!;
            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outDir))
                Directory.CreateDirectory(outDir);

            var payload = new
            {
                generated_utc = DateTime.UtcNow,
                tuning_profile = _tuningLabel,
                run_count = _runTraces.Count,
                runs = _runTraces.Select(r => new
                {
                    run_index = r.RunIndex,
                    strategy = r.Strategy.ToString(),
                    map = r.Map,
                    difficulty = r.Difficulty.ToString(),
                    event_count = r.Events.Count,
                    events = r.Events,
                }).ToList(),
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, options));
            GD.Print($"[BOT] Trace JSON written: {outputPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BOT] Failed to write trace JSON: {ex.Message}");
        }
    }

    private static CombatLabTraceEvent CloneTraceEvent(CombatLabTraceEvent source)
    {
        return new CombatLabTraceEvent
        {
            Timestamp = source.Timestamp,
            EventType = source.EventType,
            EnemyId = source.EnemyId,
            X = source.X,
            Y = source.Y,
            HpBefore = source.HpBefore,
            HpAfter = source.HpAfter,
            StatusTags = source.StatusTags,
            SurgeTriggerId = source.SurgeTriggerId,
            ExplosionStageId = source.ExplosionStageId,
            HitStopRequested = source.HitStopRequested,
            ResidueSpawned = source.ResidueSpawned,
            ComboSkin = source.ComboSkin,
        };
    }

    /// <summary>
    /// Analyzes per-wave difficulty metrics to identify balance issues.
    /// Focuses on wave-shape tuning levers: SpawnInterval and enemy composition counts.
    /// </summary>
    private void AnalyzeWaveDifficulty()
    {
        var problemWaves = new List<(int wave, string issue, string suggestion)>();
        string? mapForConfig = _results.Select(r => r.Map).Distinct().Count() == 1
            ? _results[0].Map
            : null;
        DifficultyMode difficultyForConfig = _results.Select(r => r.Difficulty).Distinct().Count() == 1
            ? _results[0].Difficulty
            : (SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy);
        
        for (int w = 0; w < Balance.TotalWaves; w++)
        {
            int waveNum = w + 1;
            var waveConfig = DataLoader.GetWaveConfig(w, difficultyForConfig, mapForConfig);
            
            // Gather loss data for this wave
            var lossesAtWave = _results.Where(r => !r.Won && r.WaveReached == waveNum).Count();
            var totalAttempts = _results.Where(r => r.WaveReached >= waveNum).Count();
            float lossRate = totalAttempts > 0 ? (float)lossesAtWave / totalAttempts : 0f;
            
            // Gather lives lost data for completed waves
            var livesAfterWave = _results
                .Where(r => r.WaveLives.Length > w)
                .Select(r => r.WaveLives[w])
                .ToList();
            float avgLives = livesAfterWave.Count > 0 ? (float)livesAfterWave.Average() : Balance.GetStartingLives(difficultyForConfig);
            float livesLost = Balance.GetStartingLives(difficultyForConfig) - avgLives;
            
            GD.Print($"Wave {waveNum,2}: {lossRate,5:0.0%} loss rate, {livesLost,4:0.1} avg lives lost | " +
                    $"Config: {waveConfig.EnemyCount} basic, {waveConfig.TankyCount} tanky, {waveConfig.SwiftCount} swift, {waveConfig.SplitterCount} splitter, {waveConfig.ReverseCount} reverse, " +
                    $"{waveConfig.SpawnInterval:0.0}s interval{(waveConfig.ClumpArmored ? ", clumped" : "")}");
            
            // Identify problem patterns
            if (lossRate > 0.20f) // More than 20% loss rate
            {
                if (waveConfig.TankyCount >= 3 && waveConfig.ClumpArmored)
                {
                    problemWaves.Add((waveNum, "High tank clump spike", $"Reduce TankyCount to {waveConfig.TankyCount - 1} or disable ClumpArmored"));
                }
                else if (waveConfig.SplitterCount >= 2)
                {
                    problemWaves.Add((waveNum, "Splitter burst overwhelming", $"Reduce SplitterCount to {waveConfig.SplitterCount - 1} or increase SpawnInterval to {waveConfig.SpawnInterval + 0.1f:0.1f}"));
                }
                else if (waveConfig.ReverseCount >= 3)
                {
                    problemWaves.Add((waveNum, "Reverse walker pressure spike", $"Reduce ReverseCount to {waveConfig.ReverseCount - 1} or raise SpawnInterval by 0.1s"));
                }
                else if (waveConfig.SwiftCount >= 3)
                {
                    problemWaves.Add((waveNum, "Swift rush overwhelming", $"Reduce SwiftCount to {waveConfig.SwiftCount - 1} or increase SpawnInterval to {waveConfig.SpawnInterval + 0.1f:0.1f}"));
                }
                else if (waveConfig.SpawnInterval < 1.3f)
                {
                    problemWaves.Add((waveNum, "Spawn rate too intense", $"Increase SpawnInterval to {waveConfig.SpawnInterval + 0.1f:0.1f}"));
                }
                else
                {
                    problemWaves.Add((waveNum, "General difficulty spike", $"Reduce EnemyCount to {waveConfig.EnemyCount - 2} or increase SpawnInterval"));
                }
            }
            else if (livesLost > 2.5f) // Significant life loss even without total failures
            {
                problemWaves.Add((waveNum, "Gradual attrition spike", $"Minor adjustment: increase SpawnInterval by 0.05s or reduce TankyCount by 1"));
            }
        }
        
        if (problemWaves.Count > 0)
        {
            GD.Print("\n🎯 RECOMMENDED BALANCE ADJUSTMENTS:");
            foreach (var (wave, issue, suggestion) in problemWaves)
            {
                GD.Print($"  Wave {wave,2} ({issue}): {suggestion}");
            }
        }
        else
        {
            GD.Print("\n✅ Wave difficulty curve looks balanced - no major spikes detected.");
        }
    }
}
