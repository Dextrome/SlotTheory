using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Tools;

public enum BotStrategy
{
    Random, TowerFirst, GreedyDps, MarkerSynergy,
    ChainFocus, SplitFocus, WeirdnessMix
}

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
        BotStrategy.ChainFocus    => PickChainFocus(options, state),
        BotStrategy.SplitFocus    => PickSplitFocus(options, state),
        BotStrategy.WeirdnessMix  => PickWeirdnessMix(options, state),
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
                float chainMult = def.ChainCount > 0
                    ? 1f + Enumerable.Range(1, def.ChainCount)
                           .Sum(i => MathF.Pow(def.ChainDamageDecay, i))
                    : 1f;
                score = def.BaseDamage / def.AttackInterval * chainMult;
                slot  = empty[0];
            }
            else if (opt.Type == DraftOptionType.Modifier && eligible.Count > 0)
            {
                score = opt.Id switch
                {
                    "hair_trigger"     => 10f,
                    "momentum"         =>  9f,
                    "split_shot"       =>  8f,
                    "exploit_weakness" => hasMarker ? 15f : 2f,
                    "feedback_loop"    =>  7f,
                    "overkill"         =>  7f,
                    "chain_reaction"   =>  6f,
                    "focus_lens"       =>  5f,
                    "overreach"        =>  4f,
                    "slow"             =>  3f,
                    _                  =>  2f,
                };
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

        // 4. Fill remaining slots with a DPS tower (rapid_shooter preferred, chain_tower accepted)
        if (empty.Count > 0)
        {
            var dps = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (dps != null) return new DraftPick(dps, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }

    // Stack chain_reaction on a chain_tower, fill with feedback_loop + rapid shooters
    private DraftPick? PickChainFocus(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        bool hasChain = s.Slots.Any(sl => sl.Tower?.TowerId == "chain_tower");

        // 1. Place chain_tower first
        if (!hasChain && empty.Count > 0)
        {
            var ct = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (ct != null) return new DraftPick(ct, empty[0]);
        }

        if (eligible.Count > 0)
        {
            // 2. Stack chain_reaction — prefer the chain_tower slot, then any slot
            var cr = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "chain_reaction");
            if (cr != null)
            {
                int chainSlot = eligible
                    .FirstOrDefault(i => s.Slots[i].Tower?.TowerId == "chain_tower", -1);
                int slot = chainSlot >= 0 ? chainSlot
                         : eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                return new DraftPick(cr, slot);
            }

            // 3. feedback_loop on any tower (chain kills many enemies quickly)
            var fl = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "feedback_loop");
            if (fl != null)
            {
                int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                return new DraftPick(fl, slot);
            }

            // 4. momentum on non-chain towers
            var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
            if (mom != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId != "chain_tower")
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(mom, slot);
            }
        }

        // 5. Fill remaining slots with rapid_shooter
        if (empty.Count > 0)
        {
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }

    // Stack split_shot on heavy_cannon + overkill for max splash damage
    private DraftPick? PickSplitFocus(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);

        if (eligible.Count > 0)
        {
            // 1. Stack split_shot — prefer heavy_cannon (big base = impactful splits)
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
                int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                return new DraftPick(ok, slot);
            }

            // 3. hair_trigger on rapid shooters (more shots = more splits per second)
            var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
            if (ht != null)
            {
                int slot = eligible
                    .Where(i => s.Slots[i].Tower?.TowerId == "rapid_shooter")
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(ht, slot);
            }
        }

        // 4. Place heavy_cannon first (big damage splits)
        if (empty.Count > 0)
        {
            var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavy != null) return new DraftPick(heavy, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }

    // Get one of each new modifier on different towers, then stack freely
    private DraftPick? PickWeirdnessMix(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        string[] newMods = ["chain_reaction", "split_shot", "feedback_loop"];

        if (eligible.Count > 0)
        {
            // 1. Spread one copy of each new modifier across different towers
            foreach (var modId in newMods)
            {
                bool alreadyHave = s.Slots.Any(sl => sl.Tower?.Modifiers.Any(m => m.ModifierId == modId) == true);
                if (alreadyHave) continue;
                var mod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == modId);
                if (mod != null)
                {
                    // Put each new mod on a different tower where possible
                    int existing = s.Slots.Count(sl =>
                        sl.Tower?.Modifiers.Any(m => newMods.Contains(m.ModifierId)) == true);
                    int slot = eligible
                        .OrderByDescending(i => {
                            int newModCount = s.Slots[i].Tower!.Modifiers.Count(m => newMods.Contains(m.ModifierId));
                            return -newModCount; // prefer towers with fewer new mods
                        })
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .First();
                    return new DraftPick(mod, slot);
                }
            }

            // 2. Once spread, stack round-robin by pick order
            foreach (var modId in newMods)
            {
                var mod = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == modId);
                if (mod != null)
                {
                    int slot = eligible.OrderBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(mod, slot);
                }
            }
        }

        // 3. Mixed tower placement — rotate through types
        if (empty.Count > 0)
        {
            int placed = s.Slots.Count(sl => sl.Tower != null);
            string[] towerOrder = ["rapid_shooter", "chain_tower", "heavy_cannon", "marker_tower"];
            string preferredId = towerOrder[placed % towerOrder.Length];
            var tower = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == preferredId)
                     ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower);
            if (tower != null) return new DraftPick(tower, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }
}
