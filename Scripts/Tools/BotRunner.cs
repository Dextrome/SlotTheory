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
        int PeakSimultaneousExplosions,
        int PeakSimultaneousActiveHazards,
        int PeakSimultaneousHitStopsRequested
    );

    private readonly int              _totalRuns;
    private readonly BotStrategy[]    _strategies;
    private readonly string[]         _maps;
	private readonly DifficultyMode? _targetDifficulty;
    private readonly string?          _forcedTowerId;
    private readonly string?          _forcedModifierId;
    private readonly string?          _metricsOutputPath;
    private readonly string?          _traceOutputPath;
    private readonly string           _tuningLabel;
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

public BotRunner(
    int totalRuns,
    DifficultyMode? targetDifficulty = null,
    string? targetMap = null,
    BotStrategy? targetStrategy = null,
    string? forcedTowerId = null,
    string? forcedModifierId = null,
    string? metricsOutputPath = null,
    string? traceOutputPath = null,
    string? tuningLabel = null)
	{
		_totalRuns  = totalRuns;
		_strategies = targetStrategy.HasValue
            ? new[] { targetStrategy.Value }
            : (BotStrategy[])Enum.GetValues(typeof(BotStrategy));
        _targetDifficulty = targetDifficulty;
        _forcedTowerId = string.IsNullOrWhiteSpace(forcedTowerId) ? null : forcedTowerId;
        _forcedModifierId = string.IsNullOrWhiteSpace(forcedModifierId) ? null : forcedModifierId;
        _metricsOutputPath = string.IsNullOrWhiteSpace(metricsOutputPath) ? null : metricsOutputPath;
        _traceOutputPath = string.IsNullOrWhiteSpace(traceOutputPath) ? null : traceOutputPath;
        _tuningLabel = string.IsNullOrWhiteSpace(tuningLabel) ? "baseline" : tuningLabel.Trim();
		_maps = targetMap != null
			? new[] { targetMap }
			: new[] { "arena_classic", "gauntlet", "sprawl" };
		// Filter difficulties if specific one requested
		if (targetDifficulty.HasValue)
			_difficulties = new[] { targetDifficulty.Value };
        StartNextRun();
    }

    private void StartNextRun()
    {
        int idx      = _results.Count;
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
        GD.Print($"[BOT] Run {idx + 1}/{_totalRuns} - {_curStrategy} on {_curMap} ({_curDifficulty}){forcedLabel}");
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
        if (events == null || events.Count == 0)
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
            state.PeakSimultaneousExplosions,
            state.PeakSimultaneousActiveHazards,
            state.PeakSimultaneousHitStopsRequested));

        if (HasMoreRuns) StartNextRun();
    }

    public void PrintSummary()
    {
        int total = _results.Count;
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
        GD.Print("║  For balancing SpawnInterval, TankyCount, SwiftCount adjustments  ║");
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
        float avgMajorSurges = avgSurges; // Minor tier was removed; all surge triggers are major.
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

        float avgBaseDps = (float)_results.Average(r => SafeDps(r.BaseAttackDamage, r.RunDurationSeconds));
        float avgSurgeDps = (float)_results.Average(r => SafeDps(r.SurgeCoreDamage, r.RunDurationSeconds));
        float avgExplosionDps = (float)_results.Average(r => SafeDps(r.ExplosionFollowUpDamage, r.RunDurationSeconds));
        float avgResidueDps = (float)_results.Average(r => SafeDps(r.ResidueDamage, r.RunDurationSeconds));

        int peakExplosions = _results.Max(r => r.PeakSimultaneousExplosions);
        int peakHazards = _results.Max(r => r.PeakSimultaneousActiveHazards);
        int peakHitStops = _results.Max(r => r.PeakSimultaneousHitStopsRequested);

        GD.Print("\nAUTOMATION METRICS (all runs):");
        GD.Print($"  Tuning profile: {_tuningLabel}");
        GD.Print($"  Avg run duration: {avgDuration:0.00}s");
        GD.Print($"  Surges/run: {avgSurges:0.00} | Major surges/run: {avgMajorSurges:0.00}");
        GD.Print($"  Kills per surge: {avgKillsPerSurge:0.00}");
        GD.Print($"  Explosion damage/run: {avgExplosionDamagePerRun:0.0}");
        GD.Print($"  Explosion damage/trigger: {avgExplosionDamagePerTrigger:0.0}");
        GD.Print($"  Status detonations/run: {avgStatusDetonations:0.00}");
        GD.Print($"  Residue uptime/run: {avgResidueUptime:0.00}s");
        GD.Print($"  Max chain depth/run: {avgChainDepth:0.00}");
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

            var payload = new
            {
                generated_utc = DateTime.UtcNow,
                tuning_profile = _tuningLabel,
                run_count = _results.Count,
                summary = new
                {
                    win_rate = _results.Count > 0 ? (float)_results.Count(r => r.Won) / _results.Count : 0f,
                    avg_wave_reached = _results.Count > 0 ? (float)_results.Average(r => r.WaveReached) : 0f,
                    avg_run_duration_seconds = _results.Count > 0 ? (float)_results.Average(r => r.RunDurationSeconds) : 0f,
                    avg_surges_per_run = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleSurgeTriggers) : 0f,
                    avg_major_surges_per_run = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleSurgeTriggers) : 0f,
                    avg_kills_per_surge = _results.Count > 0
                        ? (float)_results.Average(r => r.SpectacleSurgeTriggers > 0 ? (float)r.SpectacleKills / r.SpectacleSurgeTriggers : 0f)
                        : 0f,
                    avg_explosion_damage_per_run = _results.Count > 0 ? (float)totalExplosionDamage / _results.Count : 0f,
                    avg_explosion_damage_per_trigger = totalExplosionTriggers > 0 ? (float)totalExplosionDamage / totalExplosionTriggers : 0f,
                    avg_status_detonation_count = _results.Count > 0 ? (float)_results.Average(r => r.StatusDetonationCount) : 0f,
                    avg_residue_uptime_seconds = _results.Count > 0 ? (float)_results.Average(r => r.ResidueUptimeSeconds) : 0f,
                    avg_max_chain_depth = _results.Count > 0 ? (float)_results.Average(r => r.SpectacleMaxChainDepth) : 0f,
                    dps_split = new
                    {
                        base_attacks = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.BaseAttackDamage, r.RunDurationSeconds)) : 0f,
                        surge_core = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.SurgeCoreDamage, r.RunDurationSeconds)) : 0f,
                        explosion_follow_ups = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.ExplosionFollowUpDamage, r.RunDurationSeconds)) : 0f,
                        residue = _results.Count > 0 ? (float)_results.Average(r => SafeDps(r.ResidueDamage, r.RunDurationSeconds)) : 0f,
                    },
                    frame_stress_peaks = new
                    {
                        simultaneous_explosions = _results.Max(r => r.PeakSimultaneousExplosions),
                        simultaneous_active_hazards = _results.Max(r => r.PeakSimultaneousActiveHazards),
                        simultaneous_hitstops_requested = _results.Max(r => r.PeakSimultaneousHitStopsRequested),
                    },
                },
                runs = _results.Select(r => new
                {
                    strategy = r.Strategy.ToString(),
                    map = r.Map,
                    difficulty = r.Difficulty.ToString(),
                    won = r.Won,
                    wave_reached = r.WaveReached,
                    run_duration_seconds = r.RunDurationSeconds,
                    surges = r.SpectacleSurgeTriggers,
                    globals = r.SpectacleGlobalTriggers,
                    major_surges = r.SpectacleSurgeTriggers,
                    status_detonations = r.StatusDetonationCount,
                    residue_uptime_seconds = r.ResidueUptimeSeconds,
                    max_chain_depth = r.SpectacleMaxChainDepth,
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
                }).ToList(),
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, options));
            GD.Print($"[BOT] Metrics JSON written: {outputPath}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[BOT] Failed to write metrics JSON: {ex.Message}");
        }
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
    /// Focuses on the three main tuning levers: SpawnInterval, TankyCount, SwiftCount.
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
            float avgLives = livesAfterWave.Count > 0 ? (float)livesAfterWave.Average() : Balance.StartingLives;
            float livesLost = Balance.StartingLives - avgLives;
            
            GD.Print($"Wave {waveNum,2}: {lossRate,5:0.0%} loss rate, {livesLost,4:0.1} avg lives lost | " +
                    $"Config: {waveConfig.EnemyCount} basic, {waveConfig.TankyCount} tanky, {waveConfig.SwiftCount} swift, " +
                    $"{waveConfig.SpawnInterval:0.0}s interval{(waveConfig.ClumpArmored ? ", clumped" : "")}");
            
            // Identify problem patterns
            if (lossRate > 0.20f) // More than 20% loss rate
            {
                if (waveConfig.TankyCount >= 3 && waveConfig.ClumpArmored)
                {
                    problemWaves.Add((waveNum, "High tank clump spike", $"Reduce TankyCount to {waveConfig.TankyCount - 1} or disable ClumpArmored"));
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
