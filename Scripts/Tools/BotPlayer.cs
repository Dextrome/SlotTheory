using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Tools;

public enum BotStrategy
{
    Random, TowerFirst, GreedyDps, MarkerSynergy,
    ChainFocus, SplitFocus, HeavyStack, RiftPrismFocus,
    SpectacleSingleStack, AccordionEngine,
    PlayerStyleKenny,
    HeavyOverkill
}

/// <summary>
/// Stateless draft-picker. Given the current options and run state, returns
/// the best pick according to the active strategy.
/// </summary>
public class BotPlayer
{
    public BotStrategy Strategy { get; }
    private readonly Random _rng;
    private readonly string? _forcedTowerId;
    private readonly string? _forcedModifierId;
    private readonly bool _forcedLabMode;
    private string? _spectacleSingleTargetMod;

    public BotPlayer(
        BotStrategy strategy,
        int seed,
        string? forcedTowerId = null,
        string? forcedModifierId = null)
    {
        Strategy = strategy;
        _rng     = new Random(seed);
        _forcedTowerId = string.IsNullOrWhiteSpace(forcedTowerId) ? null : forcedTowerId;
        _forcedModifierId = string.IsNullOrWhiteSpace(forcedModifierId) ? null : forcedModifierId;
        _forcedLabMode = _forcedTowerId != null || _forcedModifierId != null;
    }

    public record DraftPick(DraftOption Option, int SlotIndex);

    public DraftPick? Pick(List<DraftOption> options, RunState state)
    {
        if (_forcedLabMode)
            return PickForcedLab(options, state);

        var earlySecondTowerPick = TryForceEarlySecondTower(options, state);
        if (earlySecondTowerPick != null)
            return earlySecondTowerPick;

        return Strategy switch
    {
        BotStrategy.Random        => PickRandom(options, state),
        BotStrategy.TowerFirst    => PickTowerFirst(options, state),
        BotStrategy.GreedyDps     => PickGreedyDps(options, state),
        BotStrategy.MarkerSynergy => PickMarkerSynergy(options, state),
        BotStrategy.ChainFocus    => PickChainFocus(options, state),
        BotStrategy.SplitFocus    => PickSplitFocus(options, state),
        BotStrategy.HeavyStack    => PickHeavyStack(options, state),
        BotStrategy.RiftPrismFocus => PickRiftPrismFocus(options, state),
        BotStrategy.SpectacleSingleStack => PickSpectacleSingleStack(options, state),
        BotStrategy.AccordionEngine      => PickAccordionEngine(options, state),
        BotStrategy.PlayerStyleKenny     => PickPlayerStyleKenny(options, state),
        BotStrategy.HeavyOverkill        => PickHeavyOverkill(options, state),
        _                         => PickRandom(options, state),
    };
    }

    //  Helpers 

    /// <summary>
    /// Ridgeback safety guard:
    /// for modifier-heavy profiles, force a second tower before the first mod
    /// so early armored waves don't auto-leak due to single-slot coverage.
    /// </summary>
    private DraftPick? TryForceEarlySecondTower(List<DraftOption> opts, RunState s)
    {
        if (!string.Equals(s.SelectedMapId, "ridgeback", StringComparison.Ordinal))
            return null;

        bool guardStrategy = Strategy is
            BotStrategy.TowerFirst or
            BotStrategy.ChainFocus or
            BotStrategy.SpectacleSingleStack;
        if (!guardStrategy)
            return null;

        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        if (towerCount != 1)
            return null;

        int totalMods = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        if (totalMods > 0)
            return null;

        var empty = EmptySlots(s);
        if (empty.Count == 0)
            return null;

        var forcedTower = FindTowerOption(opts, "marker_tower", "rapid_shooter", "chain_tower", "latch_nest", "rocket_launcher", "undertow_engine", "heavy_cannon", "rift_prism")
                       ?? Towers(opts).FirstOrDefault();
        if (forcedTower == null)
            return null;

        return new DraftPick(forcedTower, empty[0]);
    }

    private List<int> EmptySlots(RunState s) =>
        Enumerable.Range(0, s.Slots.Length).Where(i => s.Slots[i].Tower == null && !s.Slots[i].IsLocked).ToList();

    private List<int> ModSlots(RunState s) =>
        Enumerable.Range(0, s.Slots.Length)
            .Where(i => s.Slots[i].Tower?.CanAddModifier == true).ToList();


    private static List<DraftOption> Towers(List<DraftOption> opts) =>
        opts.Where(o => o.Type == DraftOptionType.Tower).ToList();

    private static List<DraftOption> Mods(List<DraftOption> opts) =>
        opts.Where(o => o.Type == DraftOptionType.Modifier).ToList();

    // Note: accordion_engine is NOT a fast tower (3.2s interval); it's a zone-control tower.
    private static bool IsFastTower(string? towerId) =>
        towerId is "rapid_shooter" or "chain_tower" or "rift_prism" or "phase_splitter" or "latch_nest";

    private static bool IsOpenerTower(string? towerId) =>
        towerId is "rapid_shooter" or "chain_tower" or "rocket_launcher" or "heavy_cannon" or "phase_splitter" or "latch_nest";

    private static bool IsWildfireAnchorTower(string? towerId) =>
        towerId is "rapid_shooter" or "rift_prism" or "accordion_engine" or "phase_splitter";

    private static bool IsAfterimageAnchorTower(string? towerId) =>
        towerId is "undertow_engine" or "chain_tower" or "phase_splitter" or "rocket_launcher" or "marker_tower";

    private static bool IsWildfireTimingOnline(RunState s, DifficultyMode difficulty, int picksSoFar)
    {
        return difficulty switch
        {
            DifficultyMode.Hard => s.WaveIndex >= 10 || picksSoFar >= 10,
            DifficultyMode.Normal => s.WaveIndex >= 7 || picksSoFar >= 7,
            _ => s.WaveIndex >= 5 || picksSoFar >= 5,
        };
    }

    private static DraftOption? FindTowerOption(List<DraftOption> opts, params string[] towerIds)
    {
        foreach (string towerId in towerIds)
        {
            var tower = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == towerId);
            if (tower != null) return tower;
        }
        return null;
    }

    private static DraftOption? FindModOption(List<DraftOption> opts, params string[] modIds)
    {
        foreach (string modId in modIds)
        {
            var mod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == modId);
            if (mod != null) return mod;
        }
        return null;
    }

    private static readonly string[] SpectacleModPriority =
    {
        "overkill",         // bloom from kills; spill now tracked as explosion damage
        "blast_core",       // splash AoE drives multi-kill chains; strong spectacle meter generator
        "wildfire",         // burn DOT + trail hazards drive sustained damage and kill chains
        "afterimage",       // delayed replay value in chokepoints; stronger with slows/pulls/clumps
        "chain_reaction",   // chain bounces → more kills → more overkill spills
        "split_shot",       // multi-ignition: each split projectile applies Burning independently
        "hair_trigger",     // attack speed → more kills + denser trail painting
        "momentum",
        "feedback_loop",
        "focus_lens",
        "slow",             // status-primes enemies; slowed enemies linger in trails longer
        "exploit_weakness",
        "overreach",
    };

    private static readonly string[] SpectacleTowerPriority =
    {
        "rapid_shooter",
        "latch_nest",
        "phase_splitter",
        "rocket_launcher",
        "undertow_engine",
        "marker_tower",   // provides Marked status - status-primes enemies for detonation
        "chain_tower",
        "heavy_cannon",
        "rift_prism",
    };

    private static readonly string[] SurvivalStabilityModPriority =
    {
        "hair_trigger",
        "momentum",
        "feedback_loop",
        "slow",
        "afterimage",
        "focus_lens",
        "overreach",
    };

    private static readonly string[] HardPanicModPriority =
    {
        "slow",
        "afterimage",
        "focus_lens",
        "hair_trigger",
        "feedback_loop",
        "overreach",
        "momentum",
    };

    private static readonly int[] CenterSlotPreference = { 2, 3, 1, 4, 0, 5 };

    private static string NormalizeSpectacleMod(string modId) =>
        SpectacleDefinitions.NormalizeModId(modId);

    private static bool IsSpectacleMod(string modId) =>
        SpectacleDefinitions.IsSupported(NormalizeSpectacleMod(modId));

    private static IEnumerable<DraftOption> SpectacleModsOffered(List<DraftOption> opts) =>
        Mods(opts).Where(o => IsSpectacleMod(o.Id));

    private static int CountSpectacleModCopies(SlotInstance slot, string modId)
    {
        if (slot.Tower == null) return 0;
        string normalized = NormalizeSpectacleMod(modId);
        return slot.Tower.Modifiers.Count(m => NormalizeSpectacleMod(m.ModifierId) == normalized);
    }

    private static int CountOffTargetSpectacleMods(SlotInstance slot, HashSet<string> allowed)
    {
        if (slot.Tower == null) return 0;
        return slot.Tower.Modifiers
            .Select(m => NormalizeSpectacleMod(m.ModifierId))
            .Count(id => SpectacleDefinitions.IsSupported(id) && !allowed.Contains(id));
    }

    private static DifficultyMode ResolveDifficultyMode() =>
        SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;

    private static bool IsPressureMap(string? mapId) =>
        string.Equals(mapId, "pinch_bleed", StringComparison.Ordinal) ||
        string.Equals(mapId, "crossroads", StringComparison.Ordinal);

    private static WaveConfig? TryGetWaveConfig(RunState s, DifficultyMode difficulty)
    {
        try
        {
            int clampedWave = Math.Clamp(s.WaveIndex, 0, Balance.TotalWaves - 1);
            return DataLoader.GetWaveConfig(clampedWave, difficulty, s.SelectedMapId);
        }
        catch
        {
            return null;
        }
    }

    private static bool SlotHasModifier(SlotInstance slot, string modId) =>
        slot.Tower?.Modifiers.Any(m => m.ModifierId == modId) == true;

    private static int CountTowerModifiers(RunState s, params string[] modIds)
    {
        if (modIds.Length == 0) return 0;
        var wanted = new HashSet<string>(modIds, StringComparer.Ordinal);
        return s.Slots.Sum(sl =>
            sl.Tower?.Modifiers.Count(m => wanted.Contains(m.ModifierId)) ?? 0);
    }

    private static int ScoreFastTowerForCadence(string? towerId) => towerId switch
    {
        "rapid_shooter" => 4,
        "latch_nest" => 4,
        "phase_splitter"=> 3,
        "rocket_launcher" => 2,
        "chain_tower"   => 2,
        "undertow_engine" => 1,
        "rift_prism"    => 1,
        "heavy_cannon"  => 1,
        _               => 0
    };

    private static int ScoreBurstTowerForSnap(string? towerId) => towerId switch
    {
        "heavy_cannon"  => 4,
        "rocket_launcher" => 3,
        "latch_nest" => 3,
        "phase_splitter"=> 3,
        "rapid_shooter" => 2,
        "chain_tower"   => 2,
        "undertow_engine" => 1,
        "rift_prism"    => 1,
        _               => 0
    };

    private static int ScoreBacklineTower(string? towerId) => towerId switch
    {
        "phase_splitter" => 4,
        "undertow_engine" => 4,
        "latch_nest" => 3,
        "rocket_launcher" => 2,
        "chain_tower"    => 3,
        "rapid_shooter"  => 2,
        "rift_prism"     => 2,
        "heavy_cannon"  => 1,
        _               => 0
    };

    private static int ScoreControlTowerForReposition(string? towerId) => towerId switch
    {
        "undertow_engine" => 5,
        "latch_nest" => 4,
        "rocket_launcher" => 3,
        "chain_tower" => 3,
        "phase_splitter" => 2,
        "rift_prism" => 2,
        "rapid_shooter" => 2,
        "heavy_cannon" => 1,
        _ => 0,
    };

    private static int ScoreFeedbackTower(string? towerId) => towerId switch
    {
        "undertow_engine" => 4,
        "latch_nest" => 4,
        "rocket_launcher" => 3,
        "rapid_shooter" => 3,
        "phase_splitter" => 3,
        "chain_tower" => 2,
        "rift_prism" => 1,
        "heavy_cannon" => 1,
        _ => 0,
    };

    private int CountStabilityMods(RunState s) =>
        CountTowerModifiers(s,
            "hair_trigger",
            "momentum",
            "feedback_loop",
            "slow",
            "focus_lens",
            "overreach");

    private bool IsHardPanicState(RunState s, DifficultyMode difficulty, int picksSoFar)
    {
        if (difficulty != DifficultyMode.Hard)
            return false;

        if (s.Lives <= 4)
            return true;

        var wave = TryGetWaveConfig(s, difficulty);
        if (wave == null)
            return s.Lives <= 6 && (s.WaveIndex < 9 || picksSoFar < 9);

        int speedThreat = wave.SwiftCount + wave.ReverseCount * 2;
        bool hasSpeedPressure = wave.ReverseCount > 0 || wave.SwiftCount >= 2 || speedThreat >= 3;
        if (!hasSpeedPressure)
            return false;

        bool stillScaling = s.WaveIndex < 10 || picksSoFar < 10;
        return stillScaling || s.Lives <= 7;
    }

    private bool ShouldApplySpectacleSurvivalGate(RunState s, DifficultyMode difficulty, int picksSoFar)
    {
        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        int modCount = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        bool early = s.WaveIndex < 6 || picksSoFar < 6;

        if (s.Lives <= 6)
            return true;

        if (towerCount == 0)
            return true;

        if (towerCount == 1 && modCount == 0)
            return true;

        if (difficulty == DifficultyMode.Hard && s.WaveIndex < 9 && (towerCount < 2 || modCount < 2))
            return true;

        return early && towerCount < 2;
    }

    private DraftPick? TryPickPriorityMod(List<DraftOption> opts, RunState s, List<int> eligible, string[] priority)
    {
        if (eligible.Count == 0) return null;

        foreach (string modId in priority)
        {
            var mod = FindModOption(opts, modId);
            if (mod == null) continue;

            int slot = modId switch
            {
                "hair_trigger" => eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) && !SlotHasModifier(s.Slots[i], "hair_trigger"))
                    .OrderByDescending(i => ScoreFastTowerForCadence(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "momentum" => eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) && !SlotHasModifier(s.Slots[i], "momentum"))
                    .OrderByDescending(i => ScoreFastTowerForCadence(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "feedback_loop" => eligible
                    .Where(i => (IsFastTower(s.Slots[i].Tower?.TowerId) || s.Slots[i].Tower?.TowerId == "undertow_engine")
                                && !SlotHasModifier(s.Slots[i], "feedback_loop"))
                    .OrderByDescending(i => ScoreFeedbackTower(s.Slots[i].Tower?.TowerId))
                    .ThenByDescending(i => ScoreFastTowerForCadence(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "focus_lens" => eligible
                    .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" && !SlotHasModifier(s.Slots[i], "focus_lens"))
                    .OrderByDescending(i => ScoreControlTowerForReposition(s.Slots[i].Tower?.TowerId))
                    .ThenByDescending(i => ScoreBurstTowerForSnap(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "slow" => eligible
                    .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" && !SlotHasModifier(s.Slots[i], "slow"))
                    .OrderByDescending(i => ScoreControlTowerForReposition(s.Slots[i].Tower?.TowerId))
                    .ThenByDescending(i => ScoreFastTowerForCadence(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "afterimage" => eligible
                    .Where(i => IsAfterimageAnchorTower(s.Slots[i].Tower?.TowerId) && !SlotHasModifier(s.Slots[i], "afterimage"))
                    .OrderByDescending(i => ScoreControlTowerForReposition(s.Slots[i].Tower?.TowerId))
                    .ThenByDescending(i => ScoreBacklineTower(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                "overreach" => eligible
                    .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" && !SlotHasModifier(s.Slots[i], "overreach"))
                    .OrderByDescending(i => ScoreControlTowerForReposition(s.Slots[i].Tower?.TowerId))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1),

                _ => -1
            };

            if (slot >= 0)
                return new DraftPick(mod, slot);
        }

        return null;
    }

    private DraftPick? TryPickPriorityTower(List<DraftOption> opts, List<int> empty, params string[] towerIds)
    {
        if (empty.Count == 0) return null;
        var tower = FindTowerOption(opts, towerIds);
        return tower == null ? null : new DraftPick(tower, empty[0]);
    }

    private DraftPick? TryPickHardPanicOverride(
        List<DraftOption> opts,
        RunState s,
        List<int> empty,
        List<int> eligible,
        DifficultyMode difficulty,
        int picksSoFar,
        bool allowRiftTower)
    {
        if (!IsHardPanicState(s, difficulty, picksSoFar))
            return null;

        var panicMod = TryPickPriorityMod(opts, s, eligible, HardPanicModPriority);
        if (panicMod != null)
            return panicMod;

        return allowRiftTower
            ? TryPickPriorityTower(opts, empty, "rapid_shooter", "chain_tower", "rocket_launcher", "undertow_engine", "heavy_cannon", "marker_tower", "rift_prism")
            : TryPickPriorityTower(opts, empty, "rapid_shooter", "chain_tower", "rocket_launcher", "undertow_engine", "heavy_cannon", "marker_tower");
    }

    private DraftPick? TryPickSpectacleSurvivalGate(
        List<DraftOption> opts,
        RunState s,
        List<int> empty,
        List<int> eligible,
        DifficultyMode difficulty,
        int picksSoFar)
    {
        if (!ShouldApplySpectacleSurvivalGate(s, difficulty, picksSoFar))
            return null;

        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        if (towerCount < 2)
        {
            var tower = TryPickPriorityTower(opts, empty, "rapid_shooter", "chain_tower", "rocket_launcher", "undertow_engine", "marker_tower", "heavy_cannon", "rift_prism");
            if (tower != null) return tower;
        }

        var modPick = TryPickPriorityMod(
            opts,
            s,
            eligible,
            IsHardPanicState(s, difficulty, picksSoFar) ? HardPanicModPriority : SurvivalStabilityModPriority);
        if (modPick != null) return modPick;

        return TryPickPriorityTower(opts, empty, "rapid_shooter", "chain_tower", "rocket_launcher", "undertow_engine", "marker_tower", "heavy_cannon", "rift_prism");
    }

    private int PickMostCenteredEmptySlot(RunState s)
    {
        var empty = EmptySlots(s);
        if (empty.Count == 0) return -1;
        foreach (int slot in CenterSlotPreference)
        {
            if (slot >= 0 && slot < s.Slots.Length && s.Slots[slot].Tower == null && !s.Slots[slot].IsLocked)
                return slot;
        }
        return empty[0];
    }

    private DraftPick? AnyTower(List<DraftOption> opts, RunState s)
    {
        var towers = Towers(opts);
        var empty  = EmptySlots(s);
        return towers.Count > 0 && empty.Count > 0
            ? new DraftPick(towers[_rng.Next(towers.Count)], empty[_rng.Next(empty.Count)])
            : null;
    }

    private DraftPick? AnyMod(List<DraftOption> opts, RunState s)
    {
        var mods     = Mods(opts);
        var eligible = ModSlots(s);
        return mods.Count > 0 && eligible.Count > 0
            ? new DraftPick(mods[_rng.Next(mods.Count)], eligible[_rng.Next(eligible.Count)])
            : null;
    }

    private DraftPick? PickPreferredSpectacleTower(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        if (empty.Count == 0) return null;

        foreach (string towerId in SpectacleTowerPriority)
        {
            var tower = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == towerId);
            if (tower != null)
                return new DraftPick(tower, empty[0]);
        }

        return AnyTower(opts, s);
    }

    /// <summary>
    /// Deterministic test harness for focused balance runs:
    /// - prioritizes placing the forced tower when offered
    /// - only equips the forced modifier on that tower (if set)
    /// - refuses unrelated picks to keep signal clean
    /// </summary>
    private DraftPick? PickForcedLab(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);

        if (_forcedTowerId != null && empty.Count > 0)
        {
            var forcedTower = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == _forcedTowerId);
            if (forcedTower != null)
                return new DraftPick(forcedTower, empty[0]);
        }

        if (_forcedModifierId != null && eligible.Count > 0)
        {
            var forcedMod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == _forcedModifierId);
            if (forcedMod != null)
            {
                var targetSlots = eligible
                    .Where(i => _forcedTowerId == null || s.Slots[i].Tower?.TowerId == _forcedTowerId)
                    .ToList();
                if (targetSlots.Count > 0)
                {
                    int slot = targetSlots
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == _forcedModifierId))
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .First();
                    return new DraftPick(forcedMod, slot);
                }
            }
        }

        return null;
    }

    //  Strategies 

    // Human-inspired profile modeled from a practical ladder playstyle.
    // Arc Emitter is represented by chain_tower in data ids.
    private DraftPick? PickPlayerStyleKenny(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);
        DifficultyMode difficulty = ResolveDifficultyMode();

        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool hasRift = s.Slots.Any(sl => sl.Tower?.TowerId == "rift_prism");
        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        int totalModCount = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        int picksSoFar = towerCount + totalModCount;
        int wildfireCopies = CountTowerModifiers(s, "wildfire");

        if (eligible.Count > 0)
        {
            // Prioritize blast_core on any tower that doesn't already have it.
            var blastCore = FindModOption(opts, "blast_core");
            if (blastCore != null)
            {
                int slot = eligible
                    .Where(i => !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "blast_core"))
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rocket_launcher" ? 3 :
                                            s.Slots[i].Tower?.TowerId == "accordion_engine" ? 2 :
                                            s.Slots[i].Tower?.TowerId == "chain_tower" ? 1 : 0)
                    .ThenByDescending(i => s.Slots[i].Tower!.Modifiers.Count) // stack on busiest tower
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(blastCore, slot);
            }

            // After opener, force one strong damage modifier early.
            var earlyDamage = FindModOption(opts, "hair_trigger", "momentum", "split_shot", "focus_lens", "chain_reaction", "exploit_weakness");
            if (earlyDamage != null)
            {
                int earlySlot = eligible
                    .Where(i => IsOpenerTower(s.Slots[i].Tower?.TowerId) && s.Slots[i].Tower!.Modifiers.Count == 0)
                    .OrderBy(i => s.Slots[i].Tower!.TowerId == "rapid_shooter" ? 0 :
                                  s.Slots[i].Tower!.TowerId == "chain_tower" ? 1 : 2)
                    .FirstOrDefault(-1);
                if (earlySlot >= 0) return new DraftPick(earlyDamage, earlySlot);
            }

            // Wildfire is strong but high-variance; only add one copy and only once the run is stabilized.
            // Anchor it on towers that can reliably apply ignites and convert trails.
            var wildfire = FindModOption(opts, "wildfire");
            if (wildfire != null && wildfireCopies < 1 && IsWildfireTimingOnline(s, difficulty, picksSoFar))
            {
                int slot = eligible
                    .Where(i =>
                        IsWildfireAnchorTower(s.Slots[i].Tower?.TowerId) &&
                        !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "wildfire"))
                    .OrderBy(i => s.Slots[i].Tower!.TowerId == "rapid_shooter" ? 0 :
                                  s.Slots[i].Tower!.TowerId == "rift_prism" ? 1 :
                                  s.Slots[i].Tower!.TowerId == "accordion_engine" ? 2 : 3)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(wildfire, slot);
            }

            // Marker upgrades: chain reaction then overreach.
            if (hasMarker)
            {
                var markerSlots = eligible.Where(i => s.Slots[i].Tower?.TowerId == "marker_tower").ToList();
                if (markerSlots.Count > 0)
                {
                    var markerCR = FindModOption(opts, "chain_reaction");
                    if (markerCR != null)
                    {
                        int slot = markerSlots
                            .Where(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "chain_reaction") < 2)
                            .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "chain_reaction"))
                            .FirstOrDefault(-1);
                        if (slot >= 0) return new DraftPick(markerCR, slot);
                    }

                    var markerOR = FindModOption(opts, "overreach");
                    if (markerOR != null)
                    {
                        int slot = markerSlots
                            .Where(i => !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "overreach"))
                            .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                            .FirstOrDefault(-1);
                        if (slot >= 0) return new DraftPick(markerOR, slot);
                    }
                }
            }

            // Heavy cannon stack: split -> exploit -> hair -> focus.
            foreach (string modId in new[] { "split_shot", "exploit_weakness", "hair_trigger", "focus_lens" })
            {
                var mod = FindModOption(opts, modId);
                if (mod == null) continue;
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == modId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(mod, slot);
            }

            // Rapid shooter stack: chain -> momentum -> exploit.
            foreach (string modId in new[] { "chain_reaction", "momentum", "exploit_weakness" })
            {
                var mod = FindModOption(opts, modId);
                if (mod == null) continue;
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == modId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(mod, slot);
            }

            // Arc emitter role: up to 3x chill shot ("slow") on chain_tower.
            var slow = FindModOption(opts, "slow");
            if (slow != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "chain_tower" &&
                                s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "slow") < 3)
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "slow"))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(slow, slot);
            }

            // Rift sapper setup: split + chain + overkill/exploit.
            foreach (string modId in new[] { "split_shot", "chain_reaction", "overkill", "exploit_weakness" })
            {
                var mod = FindModOption(opts, modId);
                if (mod == null) continue;
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == modId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(mod, slot);
            }
        }

        if (empty.Count > 0)
        {
            // Open with an early damage tower.
            if (towerCount == 0)
            {
            var opener = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "rapid_shooter", "chain_tower", "heavy_cannon");
                if (opener != null) return new DraftPick(opener, empty[0]);
            }

            // Grab accordion engine and rift sapper as early as possible.
            bool hasAccordion = s.Slots.Any(sl => sl.Tower?.TowerId == "accordion_engine");
            if (!hasAccordion && towerCount >= 1)
            {
                var accordion = FindTowerOption(opts, "accordion_engine");
                if (accordion != null) return new DraftPick(accordion, empty[0]);
            }
            if (!hasRift && towerCount >= 1)
            {
                var rift = FindTowerOption(opts, "rift_prism");
                if (rift != null) return new DraftPick(rift, empty[0]);
            }

            // Grab marker as soon as possible after opener; place near center.
            if (!hasMarker && towerCount >= 1)
            {
                var marker = FindTowerOption(opts, "marker_tower");
                if (marker != null)
                {
                    int centerSlot = PickMostCenteredEmptySlot(s);
                    if (centerSlot >= 0) return new DraftPick(marker, centerSlot);
                }
            }

            var preferredTower = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "accordion_engine", "rift_prism", "rapid_shooter", "heavy_cannon", "chain_tower");
            if (preferredTower != null) return new DraftPick(preferredTower, empty[0]);
        }

        bool allowWildfireFallback = wildfireCopies < 1 && IsWildfireTimingOnline(s, difficulty, picksSoFar);
        if (!allowWildfireFallback)
        {
            var filtered = opts
                .Where(o => !(o.Type == DraftOptionType.Modifier && o.Id == "wildfire"))
                .ToList();
            if (filtered.Count > 0)
                return PickGreedyDps(filtered, s);
        }

        return PickGreedyDps(opts, s);
    }

    /// <summary>
    /// Accordion Engine strategy: places accordion early for compression coverage,
    /// stacks Overreach + Blast Core to exploit the packed formations it creates,
    /// then fills remaining slots with standard damage towers.
    /// </summary>
    private DraftPick? PickAccordionEngine(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        DifficultyMode difficulty = ResolveDifficultyMode();
        int towerCount     = s.Slots.Count(sl => sl.Tower != null);
        int totalModCount  = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        int picksSoFar     = towerCount + totalModCount;
        bool hasAccordion  = s.Slots.Any(sl => sl.Tower?.TowerId == "accordion_engine");

        var survivalGatePick = TryPickSpectacleSurvivalGate(opts, s, empty, eligible, difficulty, picksSoFar);
        if (survivalGatePick != null) return survivalGatePick;

        var hardPanicPick = TryPickHardPanicOverride(opts, s, empty, eligible, difficulty, picksSoFar, allowRiftTower: true);
        if (hardPanicPick != null) return hardPanicPick;

        // Priority 1: place accordion early (ideally slot 1 or 2).
        if (!hasAccordion && empty.Count > 0)
        {
            var accordion = FindTowerOption(opts, "accordion_engine");
            if (accordion != null)
            {
                // Need at least one damage tower first on wave 1 or we'll get crushed.
                if (towerCount == 0)
                {
                    var opener = FindTowerOption(opts, "rapid_shooter", "heavy_cannon");
                    if (opener != null) return new DraftPick(opener, empty[0]);
                }
                return new DraftPick(accordion, empty[0]);
            }
        }

        if (eligible.Count > 0)
        {
            // Priority 2: stack Overreach on accordion to maximize compression zone.
            if (hasAccordion)
            {
                var overreach = FindModOption(opts, "overreach");
                if (overreach != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "accordion_engine" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "overreach"))
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(overreach, slot);
                }

                // Priority 3: stack Blast Core on a damage tower -- rewards packed formations.
                var blastCore = FindModOption(opts, "blast_core");
                if (blastCore != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "accordion_engine" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "blast_core"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rocket_launcher" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "phase_splitter" ? 1 : 0)
                        .ThenByDescending(i => s.Slots[i].Tower!.BaseDamage)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(blastCore, slot);
                }

                // Priority 4: Chill Shot on accordion for combined slow + compression pressure.
                var slow = FindModOption(opts, "slow");
                if (slow != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "accordion_engine" &&
                                    s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "slow") < 2)
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(slow, slot);
                }
            }

            // Generic stability mods on damage towers.
            var fallbackMod = TryPickPriorityMod(opts, s, eligible, SurvivalStabilityModPriority);
            if (fallbackMod != null) return fallbackMod;
        }

        if (empty.Count > 0)
        {
            var dmgTower = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "heavy_cannon", "rapid_shooter", "chain_tower");
            if (dmgTower != null) return new DraftPick(dmgTower, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }

    /// <summary>
    /// Heavy Overkill strategy: fills all slots with Heavy Cannon first,
    /// falling back to Arc Emitter then Rapid Shooter if unavailable.
    /// Each Heavy Cannon gets at most 1 Overkill and 1 Feedback Loop, plus one
    /// finisher mod (Hair Trigger > Focus Lens > Chain Reaction > Split Shot).
    /// Non-heavy slots get whatever damage mods are available.
    /// </summary>
    private DraftPick? PickHeavyOverkill(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        DifficultyMode difficulty = ResolveDifficultyMode();
        int picksSoFar = s.Slots.Count(sl => sl.Tower != null) + s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);

        var survivalGatePick = TryPickSpectacleSurvivalGate(opts, s, empty, eligible, difficulty, picksSoFar);
        if (survivalGatePick != null) return survivalGatePick;

        var hardPanicPick = TryPickHardPanicOverride(opts, s, empty, eligible, difficulty, picksSoFar, allowRiftTower: false);
        if (hardPanicPick != null) return hardPanicPick;

        // Priority 1: fill empty slots - heavy cannon first, then arc emitter, then rapid shooter.
        if (empty.Count > 0)
        {
            var tower = FindTowerOption(opts, "heavy_cannon", "rocket_launcher", "chain_tower", "rapid_shooter");
            if (tower != null) return new DraftPick(tower, empty[0]);
        }

        // Priority 2: place targeted mods on heavy cannon slots.
        if (eligible.Count > 0)
        {
            var heavySlots = eligible
                .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon")
                .ToList();

            if (heavySlots.Count > 0)
            {
                // Up to 1 Overkill per heavy cannon.
                var overkill = FindModOption(opts, "overkill");
                if (overkill != null)
                {
                    int slot = heavySlots
                        .Where(i => !SlotHasModifier(s.Slots[i], "overkill"))
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(overkill, slot);
                }

                // Up to 1 Feedback Loop per heavy cannon.
                var feedbackLoop = FindModOption(opts, "feedback_loop");
                if (feedbackLoop != null)
                {
                    int slot = heavySlots
                        .Where(i => !SlotHasModifier(s.Slots[i], "feedback_loop"))
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(feedbackLoop, slot);
                }

                // Third mod slot: Hair Trigger > Focus Lens > Chain Reaction > Split Shot.
                foreach (string finisher in new[] { "hair_trigger", "focus_lens", "chain_reaction", "split_shot" })
                {
                    var mod = FindModOption(opts, finisher);
                    if (mod == null) continue;
                    int slot = heavySlots
                        .Where(i => !SlotHasModifier(s.Slots[i], finisher))
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(mod, slot);
                }
            }

            // Non-heavy slots: fallback damage mods.
            var fallback = TryPickPriorityMod(opts, s, eligible, new[]
                { "overkill", "feedback_loop", "hair_trigger", "focus_lens", "chain_reaction" });
            if (fallback != null) return fallback;
        }

        return PickGreedyDps(opts, s);
    }

    private DraftPick? PickRandom(List<DraftOption> opts, RunState s)
    {
        foreach (var opt in opts.OrderBy(_ => _rng.Next()))
        {
            if (opt.Type == DraftOptionType.Tower)
            {
                var empty = EmptySlots(s);
                if (empty.Count > 0) return new DraftPick(opt, empty[_rng.Next(empty.Count)]);
            }
            else
            {
                var eligible = ModSlots(s);
                if (eligible.Count > 0) return new DraftPick(opt, eligible[_rng.Next(eligible.Count)]);
            }
        }
        return null;
    }

    // Tower hoarder: always grab a tower when available (in sensible priority), then pick a strong mod.
    private DraftPick? PickTowerFirst(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);

        if (empty.Count > 0)
        {
            var tower = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "rapid_shooter", "heavy_cannon", "chain_tower", "rift_prism", "marker_tower");
            if (tower != null) return new DraftPick(tower, empty[0]);
        }

        if (eligible.Count > 0)
        {
            var mod = FindModOption(opts, "hair_trigger", "momentum", "split_shot", "feedback_loop",
                                         "exploit_weakness", "chain_reaction", "focus_lens", "overkill", "overreach", "slow", "afterimage");
            if (mod != null)
                return new DraftPick(mod, eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First());
        }

        return null;
    }

    // Score each option by estimated DPS impact and pick the best
    private DraftPick? PickGreedyDps(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        DraftPick? best      = null;
        float      bestScore = -1f;
        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");

        foreach (var opt in opts)
        {
            float score;
            int   slot;

            if (opt.Type == DraftOptionType.Tower && empty.Count > 0)
            {
                var def = DataLoader.GetTowerDef(opt.Id);
                float chainMult = def.ChainCount > 0
                    ? 1f + Enumerable.Range(1, def.ChainCount)
                           .Sum(i => MathF.Pow(def.ChainDamageDecay, i))
                    : 1f;
                // Split damage is context-dependent (nearby targets), so we value it conservatively.
                float splitMult = def.SplitCount > 0
                    ? 1f + (def.SplitCount + 1) * Balance.SplitShotDamageRatio * 0.65f
                    : 1f;
                bool ewOffered = opts.Any(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
                float identityBonus = opt.Id == "rift_prism" ? 1.06f
                    : (opt.Id == "marker_tower" && ewOffered && !hasMarker) ? 2.5f
                    : 1f;
                score = def.BaseDamage / def.AttackInterval * chainMult * splitMult * identityBonus;
                slot  = PickMostCenteredEmptySlot(s);
            }
            else if (opt.Type == DraftOptionType.Modifier && eligible.Count > 0)
            {
                score = opt.Id switch
                {
                    "hair_trigger"     => 10f,
                    "momentum"         =>  9f,
                    "split_shot"       =>  8f,
                    "blast_core"       =>  8f,
                    "afterimage"       =>  7f,
                    "exploit_weakness" => hasMarker ? 20f : 2f,
                    "feedback_loop"    =>  8f,
                    "overkill"         =>  7f,
                    "chain_reaction"   =>  6f,
                    "focus_lens"       =>  7f,
                    "overreach"        =>  6f,
                    "slow"             =>  6f,
                    _                  =>  2f,
                };
                // Context-aware slot: some mods prefer specific tower types
                slot = opt.Id switch
                {
                    // Fast-attack mods: prefer rapid_shooter or chain_tower
                    "hair_trigger" or "feedback_loop" or "momentum" =>
                        eligible.OrderByDescending(i =>
                            IsFastTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First(),
                    // Heavy-synergy mods: prefer heavy_cannon (big base damage)
                    "focus_lens" or "overkill" or "blast_core" =>
                        eligible.OrderByDescending(i =>
                            s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 2 :
                            s.Slots[i].Tower?.TowerId == "rocket_launcher" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First(),
                    "split_shot" =>
                        eligible.OrderByDescending(i => ScoreBacklineTower(s.Slots[i].Tower?.TowerId))
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First(),
                    "afterimage" =>
                        eligible.OrderByDescending(i => ScoreControlTowerForReposition(s.Slots[i].Tower?.TowerId))
                        .ThenByDescending(i => IsAfterimageAnchorTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First(),
                    // Default: tower with fewest mods
                    _ => eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First(),
                };
            }
            else continue;

            if (score > bestScore) { bestScore = score; best = new DraftPick(opt, slot); }
        }
        return best ?? PickRandom(opts, s);
    }

    // Build around Marker Tower + Exploit Weakness synergy.
    // Keystone: marker marks enemies â†’ EW gives +45% dmg to marked â†’ momentum stacks on repeat hits.
    // Human pattern: marker + 3-4 rapid_shooters all stacked with EW + momentum.
    private DraftPick? PickMarkerSynergy(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        bool hasMarker  = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        int  dpsCount   = s.Slots.Count(sl => IsFastTower(sl.Tower?.TowerId));

        // 1. Need at least one DPS tower before chasing marker synergy - open with rapid/chain, not Rift
        if (dpsCount == 0 && empty.Count > 0)
        {
            var dps = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
            if (dps != null) return new DraftPick(dps, empty[0]);
        }

        // 2. Place Marker Tower (exactly one needed)
        if (!hasMarker && empty.Count > 0)
        {
            var marker = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
            if (marker != null) return new DraftPick(marker, empty[0]);
        }

        if (eligible.Count > 0)
        {
            // 3. EW on every DPS tower that doesn't have it yet â€" ONLY if marker is placed
            if (hasMarker)
            {
                var ew = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible.FirstOrDefault(
                        i => s.Slots[i].Tower!.TowerId != "marker_tower" &&
                             !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"), -1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }
            }

            // 4. Hair trigger on marker_tower (marks enemies faster = more EW uptime)
            var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
            if (ht != null && hasMarker)
            {
                int markerSlot = eligible.FirstOrDefault(
                    i => s.Slots[i].Tower?.TowerId == "marker_tower" &&
                         !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"), -1);
                if (markerSlot >= 0) return new DraftPick(ht, markerSlot);
            }

            // 5. Momentum on DPS towers (stacks to Ã-1.80 when hitting the same marked enemy)
            var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
            if (mom != null)
            {
                int slot = eligible.FirstOrDefault(
                    i => s.Slots[i].Tower!.TowerId != "marker_tower" &&
                         !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "momentum"), -1);
                if (slot >= 0) return new DraftPick(mom, slot);
            }

            // 6. Hair trigger on DPS towers for more shots into marked targets
            if (ht != null)
            {
                int slot = eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(ht, slot);
            }

            // 7. Feedback loop on rapid_shooter for kill-reset uptime
            var fl = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "feedback_loop");
            if (fl != null)
            {
                int slot = eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(fl, slot);
            }
        }

        // 8. Fill remaining slots with rapid_shooter (primary DPS + synergizes with EW)
        if (empty.Count > 0)
        {
            var dps = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
            if (dps != null) return new DraftPick(dps, empty[0]);
        }

        // 9. Scored fallback for any remaining modifier â€" prefer DPS-boosting mods on DPS towers
        if (eligible.Count > 0)
        {
            bool needsEw = hasMarker && eligible.Any(i =>
                s.Slots[i].Tower!.TowerId != "marker_tower" &&
                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"));
            DraftOption? bestMod = null;
            float bestScore = -1f;
            foreach (var opt in Mods(opts))
            {
                float score = opt.Id switch
                {
                    "exploit_weakness" => needsEw ? 20f : 3f,
                    "momentum"         => 9f,
                    "hair_trigger"     => 8f,
                    "feedback_loop"    => 6f,
                    "overkill"         => 5f,
                    "split_shot"       => 5f,
                    "blast_core"       => 5f,
                    "afterimage"       => 6f,
                    "chain_reaction"   => 4f,
                    "focus_lens"       => 5f,
                    "overreach"        => 5f,
                    "slow"             => 5f,
                    _                  => 2f,
                };
                if (score > bestScore) { bestScore = score; bestMod = opt; }
            }
            if (bestMod != null)
            {
                int slot = eligible
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId != "marker_tower" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(bestMod, slot);
            }
        }

        return PickTowerFirst(opts, s);
    }

    // Chain strategy: build up to 2 chain_reactions (on chain_tower), then exactly 1 damage mod,
    // then pivot fully to direct DPS. Chain bounces decay Ã-0.60/hop â€" diminishing vs armored late game.
    private DraftPick? PickChainFocus(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);

        bool hasDmgMod = s.Slots.Any(sl =>
            sl.Tower?.Modifiers.Any(m => m.ModifierId is
                "hair_trigger" or "momentum" or "split_shot" or "overkill" or "feedback_loop") == true);

        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool hasEW     = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == "exploit_weakness") == true);

        if (eligible.Count > 0)
        {
            // 0. EW top priority until first copy - only pays off once marker is placed
            if (hasMarker && !hasEW)
            {
                var ew = FindModOption(opts, "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower")
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }
            }

            // 1. chain_reaction up to 2 per tower - prefer chain_tower, then any fast tower with headroom
            {
                var cr = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "chain_reaction");
                if (cr != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "chain_reaction") < 2)
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "chain_tower" ? 2 :
                                                IsFastTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(cr, slot);
                }
            }

            // 2. Exactly 1 damage modifier â€" pick the best available, with tower affinity
            if (!hasDmgMod)
            {
                // hair_trigger â†’ fast towers; momentum â†’ fast towers; split_shot â†’ heavy_cannon
                var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
                if (ht != null)
                {
                    int slot = eligible
                        .OrderByDescending(i => IsFastTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(ht, slot);
                }
                var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
                if (mom != null)
                {
                    int slot = eligible
                        .OrderByDescending(i => IsFastTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(mom, slot);
                }
                foreach (var modId in new[] { "feedback_loop", "split_shot", "overkill" })
                {
                    var mod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == modId);
                    if (mod != null)
                    {
                        int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                        return new DraftPick(mod, slot);
                    }
                }
            }

            // 3. Scored fallback for remaining picks â€" treat like GreedyDps from here
            DraftOption? bestMod = null;
            float bestScore = -1f;
            foreach (var opt in Mods(opts))
            {
                float score = opt.Id switch
                {
                    "hair_trigger"     => 10f,
                    "momentum"         =>  9f,
                    "feedback_loop"    =>  8f,
                    "split_shot"       =>  7f,
                    "blast_core"       =>  7f,
                    "afterimage"       =>  6f,
                    "overkill"         =>  6f,
                    "focus_lens"       =>  6f,
                    "chain_reaction"   =>  3f,  // low fallback priority - step 1 handles active placement
                    "overreach"        =>  5f,
                    "slow"             =>  6f,
                    "exploit_weakness" => hasMarker ? 15f : 2f,
                    _                  =>  2f,
                };
                if (score > bestScore) { bestScore = score; bestMod = opt; }
            }
            if (bestMod != null)
            {
                int slot = eligible
                    .OrderByDescending(i => IsFastTower(s.Slots[i].Tower?.TowerId) ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(bestMod, slot);
            }
        }

        // Tower placement: open with rapid/chain for pressure, then fill with more
        if (empty.Count > 0)
        {
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
            // Grab marker for EW synergy once initial pressure towers are placed
            if (!hasMarker)
            {
                var marker = FindTowerOption(opts, "marker_tower");
                if (marker != null) return new DraftPick(marker, empty[0]);
            }
            var rift = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
            if (rift != null) return new DraftPick(rift, empty[0]);
            var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavy != null) return new DraftPick(heavy, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }

    // Stack split_shot on heavy_cannon + overkill for max splash damage
    private DraftPick? PickSplitFocus(List<DraftOption> opts, RunState s)
    {
        var empty      = EmptySlots(s);
        var eligible   = ModSlots(s);
        bool hasMarker  = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        int  towerCount = s.Slots.Count(sl => sl.Tower != null);

        if (eligible.Count > 0)
        {
            // 1. Stack split_shot - prefer heavy_cannon (big base = impactful splits)
            var ss = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "split_shot");
            if (ss != null)
            {
                int slot = eligible
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(ss, slot);
            }

            // 2. overkill synergizes: split kills spill excess to lead enemy
            var ok = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "overkill");
            if (ok != null)
            {
                int slot = eligible
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(ok, slot);
            }

            // 3. exploit_weakness if marker placed - every split target benefits
            if (hasMarker)
            {
                var ew = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }
            }

            // 4. hair_trigger on rapid shooters (more shots = more splits per second)
            var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
            if (ht != null)
            {
                int slot = eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(ht, slot);
            }
        }

        if (empty.Count > 0)
        {
            // Wave 1: rapid_shooter opener - heavy alone is too slow for early waves
            if (towerCount == 0)
            {
                var opener = FindTowerOption(opts, "rapid_shooter", "chain_tower");
                if (opener != null) return new DraftPick(opener, empty[0]);
            }

            // Core: heavy_cannon for high-damage splits
            var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavy != null) return new DraftPick(heavy, empty[0]);

            // Grab marker mid-game for exploit_weakness + split synergy
            if (!hasMarker && towerCount >= 2)
            {
                var marker = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
                if (marker != null) return new DraftPick(marker, empty[0]);
            }
        }

        return PickGreedyDps(opts, s);
    }

    // Rift Sapper strategy: prioritize cadence + chain/split so trap fields reseed quickly and cascade.
    private DraftPick? PickRiftPrismFocus(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        DifficultyMode difficulty = ResolveDifficultyMode();
        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool isHard = difficulty == DifficultyMode.Hard;
        int riftCount = s.Slots.Count(sl => sl.Tower?.TowerId == "rift_prism");
        int rapidCount = s.Slots.Count(sl => sl.Tower?.TowerId == "rapid_shooter");
        int chainCount = s.Slots.Count(sl => sl.Tower?.TowerId == "chain_tower");
        int heavyCount = s.Slots.Count(sl => sl.Tower?.TowerId == "heavy_cannon");
        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        int totalModCount = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        int picksSoFar = towerCount + totalModCount;
        bool earlyGame = s.WaveIndex < 6 || picksSoFar < 6;
        bool needsStability = isHard || earlyGame || s.Lives <= 6;
        bool pressureMap = IsPressureMap(s.SelectedMapId);
        int stabilityModCount = CountStabilityMods(s);
        bool mapFallbackActive = pressureMap &&
                                 ((difficulty == DifficultyMode.Hard && (s.WaveIndex < 7 || picksSoFar < 7)) ||
                                  s.Lives <= 5);
        bool readyForFirstRift = !mapFallbackActive ||
                                 towerCount >= 2 ||
                                 stabilityModCount >= 1 ||
                                 ((rapidCount + chainCount) >= 1 && picksSoFar >= 4);
        int chainReactionOnRift = s.Slots.Sum(sl =>
            sl.Tower?.TowerId == "rift_prism"
                ? sl.Tower.Modifiers.Count(m => m.ModifierId == "chain_reaction")
                : 0);

        if (eligible.Count > 0)
        {
            var hardPanicPick = TryPickHardPanicOverride(
                opts, s, empty, eligible, difficulty, picksSoFar, allowRiftTower: readyForFirstRift || riftCount > 0);
            if (hardPanicPick != null)
                return hardPanicPick;

            if (mapFallbackActive && riftCount == 0)
            {
                var stabilityPick = TryPickPriorityMod(opts, s, eligible, SurvivalStabilityModPriority);
                if (stabilityPick != null)
                    return stabilityPick;
            }

            if (hasMarker)
            {
                var ew = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 3 :
                                                s.Slots[i].Tower?.TowerId == "rift_prism" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "chain_tower" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }

                var markerHt = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
                if (markerHt != null)
                {
                    int markerSlot = eligible.FirstOrDefault(i =>
                        s.Slots[i].Tower?.TowerId == "marker_tower" &&
                        !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"), -1);
                    if (markerSlot >= 0) return new DraftPick(markerHt, markerSlot);
                }
            }

            if (needsStability)
            {
                var hairTriggerEarly = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
                if (hairTriggerEarly != null)
                {
                    int slot = eligible
                        .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "chain_tower" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(hairTriggerEarly, slot);
                }

                var momentumEarly = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
                if (momentumEarly != null)
                {
                    int slot = eligible
                        .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "momentum"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "chain_tower" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(momentumEarly, slot);
                }

                var feedbackEarly = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "feedback_loop");
                if (feedbackEarly != null)
                {
                    int slot = eligible
                        .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "rift_prism" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(feedbackEarly, slot);
                }
            }

            var split = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "split_shot");
            if (split != null && (readyForFirstRift || riftCount > 0))
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "split_shot"))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot < 0)
                {
                    slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "split_shot"))
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                }
                if (slot < 0)
                {
                    slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism")
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                }
                if (slot >= 0) return new DraftPick(split, slot);
            }

            var chainReaction = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "chain_reaction");
            if (chainReaction != null && (readyForFirstRift || riftCount > 0) && chainReactionOnRift < Math.Max(1, riftCount))
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism" &&
                                s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "split_shot"))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "chain_reaction"))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot < 0)
                {
                    slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism")
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count(m => m.ModifierId == "chain_reaction"))
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                }
                if (slot >= 0) return new DraftPick(chainReaction, slot);
            }

            var hairTrigger = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
            if (hairTrigger != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"))
                    .OrderByDescending(i => s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "split_shot") ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot < 0)
                {
                    slot = eligible
                        .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "hair_trigger"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rift_prism" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                }
                if (slot >= 0) return new DraftPick(hairTrigger, slot);
            }

            var feedback = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "feedback_loop");
            if (feedback != null && (readyForFirstRift || riftCount > 0))
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rift_prism")
                    .OrderByDescending(i => s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "split_shot") ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot < 0)
                {
                    slot = eligible
                        .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rift_prism" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                }
                if (slot >= 0) return new DraftPick(feedback, slot);
            }

            if (!needsStability)
            {
                var overkill = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "overkill");
                if (overkill != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon")
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot < 0)
                        slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(overkill, slot);
                }
            }

            if (hasMarker)
            {
                var ew = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower" &&
                                    !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"))
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rift_prism" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }
            }

            var momentum = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
            if (momentum != null)
            {
                int slot = eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId) &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "momentum"))
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ||
                                            s.Slots[i].Tower?.TowerId == "chain_tower" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(momentum, slot);
            }

            // Scored fallback so we do not stall on low-value picks.
            DraftOption? best = null;
            float bestScore = -1f;
            foreach (var mod in Mods(opts))
            {
                float score = mod.Id switch
                {
                    "exploit_weakness" => hasMarker ? 12f : 2f,
                    "hair_trigger"     => needsStability ? 11f : 8f,
                    "momentum"         => needsStability ? 10f : 7f,
                    "feedback_loop"    => needsStability ? 9f : 7f,
                    "split_shot"       => needsStability ? 8f : 11f,
                    "blast_core"       => needsStability ? 5f : 9f,
                    "afterimage"       => needsStability ? 7f : 10f,
                    "chain_reaction"   => needsStability ? 6f : 9f,
                    "overkill"         => needsStability ? 4f : 8f,
                    "focus_lens"       => needsStability ? 3f : 5f,
                    "overreach"        => 3f,
                    "slow"             => 3f,
                    _                  => 1f
                };
                if (score > bestScore) { bestScore = score; best = mod; }
            }
            if (best != null)
            {
                int slot = eligible
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 2 :
                                            s.Slots[i].Tower?.TowerId == "rift_prism" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(best, slot);
            }
        }

        if (empty.Count > 0)
        {
            var hardPanicTower = TryPickHardPanicOverride(
                opts, s, empty, eligible, difficulty, picksSoFar, allowRiftTower: readyForFirstRift || riftCount > 0);
            if (hardPanicTower != null)
                return hardPanicTower;

            // Open with a DPS tower first - Rift Sapper is a setup tower, not an opener.
            if (towerCount == 0)
            {
                var opener = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "phase_splitter")
                          ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                          ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower")
                          ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (opener != null) return new DraftPick(opener, empty[0]);
            }

            if (mapFallbackActive && !readyForFirstRift && riftCount == 0)
            {
            var safetyOpener = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "rapid_shooter", "chain_tower", "heavy_cannon", "marker_tower");
                if (safetyOpener != null) return new DraftPick(safetyOpener, empty[0]);
            }

            // Anchor with Rift once DPS is established, then scale to extra Rifts.
            if (riftCount == 0 && towerCount >= 1 && readyForFirstRift)
            {
                var rift = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
                if (rift != null) return new DraftPick(rift, empty[0]);
            }

            if (rapidCount == 0)
            {
                var rapidFirst = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                              ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
                if (rapidFirst != null) return new DraftPick(rapidFirst, empty[0]);
            }

            bool shouldForceMarker = !hasMarker && (isHard || towerCount >= 2 || picksSoFar >= 4);
            if (shouldForceMarker)
            {
                var marker = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
                if (marker != null) return new DraftPick(marker, empty[0]);
            }

            if (needsStability && chainCount == 0)
            {
                var chainPreferred = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
                if (chainPreferred != null) return new DraftPick(chainPreferred, empty[0]);
            }

            if (riftCount < 2 && (hasMarker || towerCount >= 3) && (readyForFirstRift || riftCount > 0))
            {
                var rift = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
                if (rift != null) return new DraftPick(rift, empty[0]);
            }

            if (heavyCount == 0 && !needsStability && towerCount >= 4)
            {
                var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (heavy != null) return new DraftPick(heavy, empty[0]);
            }

            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "phase_splitter")
                     ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
            var markerFallback = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
            if (markerFallback != null) return new DraftPick(markerFallback, empty[0]);
            var heavyFallback = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavyFallback != null) return new DraftPick(heavyFallback, empty[0]);
            var riftExtra = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
            if (riftExtra != null && (readyForFirstRift || riftCount > 0 || towerCount >= 4))
                return new DraftPick(riftExtra, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }

    // Single spectacle profile: stack one supported modifier aggressively to isolate single-trigger tuning.
    private DraftPick? PickSpectacleSingleStack(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);
        DifficultyMode difficulty = ResolveDifficultyMode();
        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        int totalModCount = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        int picksSoFar = towerCount + totalModCount;
        var offeredSpectacleMods = SpectacleModsOffered(opts)
            .Select(o => NormalizeSpectacleMod(o.Id))
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

        var survivalGatePick = TryPickSpectacleSurvivalGate(opts, s, empty, eligible, difficulty, picksSoFar);
        if (survivalGatePick != null)
            return survivalGatePick;

        var hardPanicPick = TryPickHardPanicOverride(opts, s, empty, eligible, difficulty, picksSoFar, allowRiftTower: true);
        if (hardPanicPick != null)
            return hardPanicPick;

        // EW top priority until first copy if marker is placed
        if (eligible.Count > 0)
        {
            bool hasMarkerSS = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
            bool hasEWSS     = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == "exploit_weakness") == true);
            if (hasMarkerSS && !hasEWSS)
            {
                var ewPick = FindModOption(opts, "exploit_weakness");
                if (ewPick != null)
                {
                    int ewSlot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower")
                        .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (ewSlot >= 0) return new DraftPick(ewPick, ewSlot);
                }
            }
        }

        if (_spectacleSingleTargetMod == null)
        {
            _spectacleSingleTargetMod = SpectacleModPriority.FirstOrDefault(offeredSpectacleMods.Contains);
        }

        if (eligible.Count > 0 && _spectacleSingleTargetMod != null)
        {
            var targetOption = SpectacleModsOffered(opts)
                .FirstOrDefault(o => NormalizeSpectacleMod(o.Id) == _spectacleSingleTargetMod);
            if (targetOption != null)
            {
                var allowed = new HashSet<string>(StringComparer.Ordinal) { _spectacleSingleTargetMod };
                int slot = eligible
                    .OrderByDescending(i => CountSpectacleModCopies(s.Slots[i], _spectacleSingleTargetMod))
                    .ThenBy(i => CountOffTargetSpectacleMods(s.Slots[i], allowed))
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(targetOption, slot);
            }
        }

        if (empty.Count > 0)
        {
            var towerPick = PickPreferredSpectacleTower(opts, s);
            if (towerPick != null) return towerPick;
        }

        if (eligible.Count > 0 && _spectacleSingleTargetMod == null)
        {
            var fallbackSpectacle = SpectacleModsOffered(opts)
                .OrderBy(o => Array.IndexOf(SpectacleModPriority, NormalizeSpectacleMod(o.Id)))
                .FirstOrDefault();
            if (fallbackSpectacle != null)
            {
                _spectacleSingleTargetMod = NormalizeSpectacleMod(fallbackSpectacle.Id);
                int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                return new DraftPick(fallbackSpectacle, slot);
            }
        }

        return PickGreedyDps(opts, s);
    }



    // Stack focus_lens + momentum on heavy_cannon for maximum single-shot burst damage.
    // Tests whether ultra-high-damage slow towers can handle hard waves.
    private DraftPick? PickHeavyStack(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        int heavyCount = s.Slots.Count(sl => sl.Tower?.TowerId == "heavy_cannon");

        bool hasMarkerHS = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool hasEWHS     = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == "exploit_weakness") == true);

        if (eligible.Count > 0)
        {
            // 0. EW top priority until first copy - only pays off once marker is placed
            if (hasMarkerHS && !hasEWHS)
            {
                var ew = FindModOption(opts, "exploit_weakness");
                if (ew != null)
                {
                    int slot = eligible
                        .Where(i => s.Slots[i].Tower?.TowerId != "marker_tower")
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .FirstOrDefault(-1);
                    if (slot >= 0) return new DraftPick(ew, slot);
                }
            }

                        // 1. Focus Lens on heavy_cannon: x2.40 damage, x1.85 interval for burst-heavy scaling
            var fl = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "focus_lens");
            if (fl != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "focus_lens"))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(fl, slot);
            }

            // 2. Momentum on heavy_cannon: builds Ã-1.80 on armored/tanky clumps
            var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
            if (mom != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon" &&
                                !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "momentum"))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(mom, slot);
            }

            // 3. Overkill on heavy_cannon: massive overkill damage spills to next enemy
            var ok = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "overkill");
            if (ok != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "heavy_cannon")
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(ok, slot);
            }

            // 4. Hair trigger on rapid_shooter fillers to compensate for heavy_cannon slow fire rate
            var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
            if (ht != null)
            {
                int slot = eligible
                    .Where(i => IsFastTower(s.Slots[i].Tower?.TowerId))
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(ht, slot);
            }

            // 5. Any remaining strong mod on best available tower
            var anyMod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier &&
                                                   o.Id is "split_shot" or "feedback_loop" or "chain_reaction");
            if (anyMod != null)
            {
                int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                return new DraftPick(anyMod, slot);
            }
        }

        // Tower placement: rapid opener, then 2 heavy_cannons, rest rapid_shooters for chip
        if (empty.Count > 0)
        {
            int towerCount = s.Slots.Count(sl => sl.Tower != null);

            // Wave 1: rapid_shooter opener - heavy alone can't cover early waves
            if (towerCount == 0)
            {
            var opener = FindTowerOption(opts, "phase_splitter", "rocket_launcher", "rapid_shooter", "chain_tower");
                if (opener != null) return new DraftPick(opener, empty[0]);
            }

            if (heavyCount < 2)
            {
                var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (heavy != null) return new DraftPick(heavy, empty[0]);
            }
            // Grab marker for EW synergy once heavies are established
            if (!hasMarkerHS)
            {
                var marker = FindTowerOption(opts, "marker_tower");
                if (marker != null) return new DraftPick(marker, empty[0]);
            }
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "phase_splitter")
                     ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }
}
