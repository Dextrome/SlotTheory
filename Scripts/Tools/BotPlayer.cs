using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Tools;

public enum BotStrategy { Random, TowerFirst, GreedyDps, MarkerSynergy }

/// <summary>
/// Stateless draft-picker. Given the current options and run state, returns
/// the best pick according to the active strategy.
/// </summary>
public class BotPlayer
{
    public BotStrategy Strategy { get; }
    private readonly Random _rng;

    public BotPlayer(BotStrategy strategy, int seed)
    {
        Strategy = strategy;
        _rng     = new Random(seed);
    }

    public record DraftPick(DraftOption Option, int SlotIndex);

    public DraftPick? Pick(List<DraftOption> options, RunState state) => Strategy switch
    {
        BotStrategy.Random        => PickRandom(options, state),
        BotStrategy.TowerFirst    => PickTowerFirst(options, state),
        BotStrategy.GreedyDps     => PickGreedyDps(options, state),
        BotStrategy.MarkerSynergy => PickMarkerSynergy(options, state),
        _                         => PickRandom(options, state),
    };

    // ── Helpers ────────────────────────────────────────────────────────────

    private List<int> EmptySlots(RunState s) =>
        Enumerable.Range(0, s.Slots.Length).Where(i => s.Slots[i].Tower == null).ToList();

    private List<int> ModSlots(RunState s) =>
        Enumerable.Range(0, s.Slots.Length)
            .Where(i => s.Slots[i].Tower?.CanAddModifier == true).ToList();

    private static List<DraftOption> Towers(List<DraftOption> opts) =>
        opts.Where(o => o.Type == DraftOptionType.Tower).ToList();

    private static List<DraftOption> Mods(List<DraftOption> opts) =>
        opts.Where(o => o.Type == DraftOptionType.Modifier).ToList();

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

    // ── Strategies ─────────────────────────────────────────────────────────

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

    // Always fill empty slots first, then random modifier
    private DraftPick? PickTowerFirst(List<DraftOption> opts, RunState s) =>
        AnyTower(opts, s) ?? AnyMod(opts, s);

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
                score = def.BaseDamage / def.AttackInterval;
                slot  = empty[0];
            }
            else if (opt.Type == DraftOptionType.Modifier && eligible.Count > 0)
            {
                score = opt.Id switch
                {
                    "hair_trigger"     => 10f,
                    "momentum"         =>  9f,
                    "exploit_weakness" => hasMarker ? 15f : 2f,
                    "overkill"         =>  7f,
                    "focus_lens"       =>  5f,
                    "overreach"        =>  4f,
                    "chill_shot"       =>  3f,
                    _                  =>  2f,
                };
                // spread mods across towers with fewest already applied
                slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
            }
            else continue;

            if (score > bestScore) { bestScore = score; best = new DraftPick(opt, slot); }
        }
        return best ?? PickRandom(opts, s);
    }

    // Build around Marker Tower + Exploit Weakness synergy
    private DraftPick? PickMarkerSynergy(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        bool hasMarker = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");

        // 1. Place Marker Tower first if not yet present
        if (!hasMarker && empty.Count > 0)
        {
            var marker = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "marker_tower");
            if (marker != null) return new DraftPick(marker, empty[0]);
        }

        if (eligible.Count > 0)
        {
            // 2. Apply Exploit Weakness to any tower that doesn't have it yet
            var ew = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "exploit_weakness");
            if (ew != null)
            {
                int slot = eligible.FirstOrDefault(
                    i => !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "exploit_weakness"), -1);
                if (slot >= 0) return new DraftPick(ew, slot);
            }

            // 3. Momentum on DPS towers (not the Marker Tower itself)
            var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
            if (mom != null)
            {
                int slot = eligible.FirstOrDefault(
                    i => s.Slots[i].Tower!.TowerId != "marker_tower" &&
                         !s.Slots[i].Tower!.Modifiers.Any(m => m.ModifierId == "momentum"), -1);
                if (slot >= 0) return new DraftPick(mom, slot);
            }
        }

        // 4. Fill remaining slots with Rapid Shooter for DPS
        if (empty.Count > 0)
        {
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }
}
