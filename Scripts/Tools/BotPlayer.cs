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
    SpectacleSingleStack, SpectacleComboPairing, SpectacleTriadDiversity,
    PlayerStyleKenny
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
    private string? _spectacleComboModA;
    private string? _spectacleComboModB;
    private readonly HashSet<string> _spectacleTriadTargets = new(StringComparer.Ordinal);

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
        BotStrategy.SpectacleComboPairing => PickSpectacleComboPairing(options, state),
        BotStrategy.SpectacleTriadDiversity => PickSpectacleTriadDiversity(options, state),
        BotStrategy.PlayerStyleKenny => PickPlayerStyleKenny(options, state),
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
            BotStrategy.SpectacleSingleStack or
            BotStrategy.SpectacleComboPairing or
            BotStrategy.SpectacleTriadDiversity;
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

        var forcedTower = FindTowerOption(opts, "marker_tower", "rapid_shooter", "chain_tower", "heavy_cannon", "rift_prism")
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

    private static bool IsFastTower(string? towerId) =>
        towerId is "rapid_shooter" or "chain_tower" or "rift_prism";

    private static bool IsOpenerTower(string? towerId) =>
        towerId is "rapid_shooter" or "chain_tower" or "heavy_cannon";

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
        "chain_reaction",   // chain bounces → more kills → more overkill spills
        "split_shot",       // BurnPatch residue (only damage-dealing residue type)
        "hair_trigger",     // attack speed → more kills → more overkill spills
        "momentum",
        "feedback_loop",
        "focus_lens",
        "slow",             // status-primes enemies for detonation
        "exploit_weakness",
        "overreach",
    };

    private static readonly string[] SpectacleTowerPriority =
    {
        "rapid_shooter",
        "marker_tower",   // provides Marked status - status-primes enemies for detonation
        "chain_tower",
        "heavy_cannon",
        "rift_prism",
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

    private int PickMostCenteredEmptySlot(RunState s)
    {
        var empty = EmptySlots(s);
        if (empty.Count == 0) return -1;
        foreach (int slot in CenterSlotPreference)
        {
            if (slot >= 0 && slot < s.Slots.Length && s.Slots[slot].Tower == null)
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

        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool hasRift = s.Slots.Any(sl => sl.Tower?.TowerId == "rift_prism");
        int towerCount = s.Slots.Count(sl => sl.Tower != null);

        if (eligible.Count > 0)
        {
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
                var opener = FindTowerOption(opts, "rapid_shooter", "chain_tower", "heavy_cannon");
                if (opener != null) return new DraftPick(opener, empty[0]);
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

            // Add rift sapper once core is established.
            if (!hasRift && towerCount >= 2)
            {
                var rift = FindTowerOption(opts, "rift_prism");
                if (rift != null) return new DraftPick(rift, empty[0]);
            }

            var preferredTower = FindTowerOption(opts, "rapid_shooter", "heavy_cannon", "chain_tower", "rift_prism");
            if (preferredTower != null) return new DraftPick(preferredTower, empty[0]);
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
            var tower = FindTowerOption(opts, "rapid_shooter", "heavy_cannon", "chain_tower", "rift_prism", "marker_tower");
            if (tower != null) return new DraftPick(tower, empty[0]);
        }

        if (eligible.Count > 0)
        {
            var mod = FindModOption(opts, "hair_trigger", "momentum", "split_shot", "feedback_loop",
                                         "exploit_weakness", "chain_reaction", "focus_lens", "overkill", "overreach", "slow");
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
                    "focus_lens" or "split_shot" or "overkill" =>
                        eligible.OrderByDescending(i =>
                            s.Slots[i].Tower?.TowerId == "heavy_cannon" ? 1 : 0)
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
            // 0. EW top priority until first copy — only pays off once marker is placed
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

            // 1. chain_reaction up to 2 per tower — prefer chain_tower, then any fast tower with headroom
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
                    "overkill"         =>  6f,
                    "focus_lens"       =>  6f,
                    "chain_reaction"   =>  3f,  // low fallback priority — step 1 handles active placement
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
        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        bool isHard = SettingsManager.Instance?.Difficulty == DifficultyMode.Hard;
        int riftCount = s.Slots.Count(sl => sl.Tower?.TowerId == "rift_prism");
        int rapidCount = s.Slots.Count(sl => sl.Tower?.TowerId == "rapid_shooter");
        int chainCount = s.Slots.Count(sl => sl.Tower?.TowerId == "chain_tower");
        int heavyCount = s.Slots.Count(sl => sl.Tower?.TowerId == "heavy_cannon");
        int towerCount = s.Slots.Count(sl => sl.Tower != null);
        int totalModCount = s.Slots.Sum(sl => sl.Tower?.Modifiers.Count ?? 0);
        int picksSoFar = towerCount + totalModCount;
        bool earlyGame = s.WaveIndex < 6 || picksSoFar < 6;
        bool needsStability = isHard || earlyGame || s.Lives <= 6;
        int chainReactionOnRift = s.Slots.Sum(sl =>
            sl.Tower?.TowerId == "rift_prism"
                ? sl.Tower.Modifiers.Count(m => m.ModifierId == "chain_reaction")
                : 0);

        if (eligible.Count > 0)
        {
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
            if (split != null)
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
            if (chainReaction != null && chainReactionOnRift < Math.Max(1, riftCount))
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
            if (feedback != null)
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
            // Open with a DPS tower first - Rift Sapper is a setup tower, not an opener.
            if (towerCount == 0)
            {
                var opener = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                          ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower")
                          ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (opener != null) return new DraftPick(opener, empty[0]);
            }

            // Anchor with Rift once DPS is established, then scale to extra Rifts.
            if (riftCount == 0 && towerCount >= 1)
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

            if (riftCount < 2 && (hasMarker || towerCount >= 3))
            {
                var rift = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
                if (rift != null) return new DraftPick(rift, empty[0]);
            }

            if (heavyCount == 0 && !needsStability && towerCount >= 4)
            {
                var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (heavy != null) return new DraftPick(heavy, empty[0]);
            }

            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
            var markerFallback = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
            if (markerFallback != null) return new DraftPick(markerFallback, empty[0]);
            var heavyFallback = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavyFallback != null) return new DraftPick(heavyFallback, empty[0]);
            var riftExtra = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rift_prism");
            if (riftExtra != null) return new DraftPick(riftExtra, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }

    // Single spectacle profile: stack one supported modifier aggressively to isolate single-trigger tuning.
    private DraftPick? PickSpectacleSingleStack(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);
        var offeredSpectacleMods = SpectacleModsOffered(opts)
            .Select(o => NormalizeSpectacleMod(o.Id))
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

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

    // Combo spectacle profile: lock to two modifiers and bias placements that preserve pair purity.
    private DraftPick? PickSpectacleComboPairing(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);
        var offeredNormalized = SpectacleModsOffered(opts)
            .Select(o => NormalizeSpectacleMod(o.Id))
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

        // EW top priority until first copy if marker is placed
        if (eligible.Count > 0)
        {
            bool hasMarkerCP = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
            bool hasEWCP     = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == "exploit_weakness") == true);
            if (hasMarkerCP && !hasEWCP)
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

        if (_spectacleComboModA == null)
            _spectacleComboModA = SpectacleModPriority.FirstOrDefault(offeredNormalized.Contains);
        if (_spectacleComboModB == null && _spectacleComboModA != null)
            _spectacleComboModB = SpectacleModPriority.FirstOrDefault(m => m != _spectacleComboModA && offeredNormalized.Contains(m));

        if (eligible.Count > 0 && _spectacleComboModA != null && _spectacleComboModB != null)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal) { _spectacleComboModA, _spectacleComboModB };
            foreach (string targetMod in new[] { _spectacleComboModA, _spectacleComboModB })
            {
                var targetOption = SpectacleModsOffered(opts).FirstOrDefault(o => NormalizeSpectacleMod(o.Id) == targetMod);
                if (targetOption == null) continue;

                string partner = targetMod == _spectacleComboModA ? _spectacleComboModB : _spectacleComboModA;
                int slot = eligible
                    .OrderByDescending(i =>
                    {
                        int partnerCopies = CountSpectacleModCopies(s.Slots[i], partner);
                        int targetCopies = CountSpectacleModCopies(s.Slots[i], targetMod);
                        int offTarget = CountOffTargetSpectacleMods(s.Slots[i], allowed);
                        return (partnerCopies > 0 ? 6 : 0)
                            + (targetCopies > 0 ? 3 : 0)
                            - offTarget * 8
                            - s.Slots[i].Tower!.Modifiers.Count;
                    })
                    .First();
                return new DraftPick(targetOption, slot);
            }
        }

        if (eligible.Count > 0)
        {
            if (_spectacleComboModA == null)
            {
                var firstPairMod = SpectacleModsOffered(opts)
                    .OrderBy(o => Array.IndexOf(SpectacleModPriority, NormalizeSpectacleMod(o.Id)))
                    .FirstOrDefault();
                if (firstPairMod != null)
                {
                    _spectacleComboModA = NormalizeSpectacleMod(firstPairMod.Id);
                    int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(firstPairMod, slot);
                }
            }
            else if (_spectacleComboModB == null)
            {
                var secondPairMod = SpectacleModsOffered(opts)
                    .Where(o => NormalizeSpectacleMod(o.Id) != _spectacleComboModA)
                    .OrderBy(o => Array.IndexOf(SpectacleModPriority, NormalizeSpectacleMod(o.Id)))
                    .FirstOrDefault();
                if (secondPairMod != null)
                {
                    _spectacleComboModB = NormalizeSpectacleMod(secondPairMod.Id);
                    int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(secondPairMod, slot);
                }
            }
        }

        if (empty.Count > 0)
        {
            var towerPick = PickPreferredSpectacleTower(opts, s);
            if (towerPick != null) return towerPick;
        }

        return PickGreedyDps(opts, s);
    }

    // Triad spectacle profile: build and maintain three unique supported mods for triad + augment coverage.
    private DraftPick? PickSpectacleTriadDiversity(List<DraftOption> opts, RunState s)
    {
        var empty = EmptySlots(s);
        var eligible = ModSlots(s);
        var offeredSpectacle = SpectacleModsOffered(opts).ToList();

        // EW top priority until first copy if marker is placed
        if (eligible.Count > 0)
        {
            bool hasMarkerTD = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
            bool hasEWTD     = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == "exploit_weakness") == true);
            if (hasMarkerTD && !hasEWTD)
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

        foreach (string mod in SpectacleModPriority)
        {
            if (_spectacleTriadTargets.Count >= 3) break;
            if (offeredSpectacle.Any(o => NormalizeSpectacleMod(o.Id) == mod))
                _spectacleTriadTargets.Add(mod);
        }

        if (eligible.Count > 0 && _spectacleTriadTargets.Count > 0)
        {
            DraftOption? bestOption = null;
            int bestSlot = -1;
            int bestScore = int.MinValue;

            foreach (var opt in offeredSpectacle)
            {
                string normalized = NormalizeSpectacleMod(opt.Id);
                if (_spectacleTriadTargets.Count >= 3 && !_spectacleTriadTargets.Contains(normalized))
                    continue;

                var allowed = new HashSet<string>(_spectacleTriadTargets, StringComparer.Ordinal) { normalized };
                foreach (int slotIndex in eligible)
                {
                    var slot = s.Slots[slotIndex];
                    var slotTargetSet = slot.Tower!.Modifiers
                        .Select(m => NormalizeSpectacleMod(m.ModifierId))
                        .Where(allowed.Contains)
                        .Distinct()
                        .ToHashSet(StringComparer.Ordinal);
                    bool hasThisMod = slotTargetSet.Contains(normalized);
                    int offTarget = CountOffTargetSpectacleMods(slot, allowed);
                    int score = 0;

                    if (!hasThisMod && slotTargetSet.Count == 2) score += 10;
                    else if (!hasThisMod && slotTargetSet.Count == 1) score += 6;
                    else if (!hasThisMod && slotTargetSet.Count == 0) score += 3;
                    else score += 2;

                    score -= offTarget * 8;
                    score -= slot.Tower.Modifiers.Count;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestOption = opt;
                        bestSlot = slotIndex;
                    }
                }
            }

            if (bestOption != null && bestSlot >= 0)
            {
                string normalized = NormalizeSpectacleMod(bestOption.Id);
                if (_spectacleTriadTargets.Count < 3)
                    _spectacleTriadTargets.Add(normalized);
                return new DraftPick(bestOption, bestSlot);
            }
        }

        if (empty.Count > 0)
        {
            var towerPick = PickPreferredSpectacleTower(opts, s);
            if (towerPick != null) return towerPick;
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
            // 0. EW top priority until first copy — only pays off once marker is placed
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
                var opener = FindTowerOption(opts, "rapid_shooter", "chain_tower");
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
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
        }

        return PickGreedyDps(opts, s);
    }
}


