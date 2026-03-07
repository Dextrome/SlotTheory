using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Tools;

public enum BotStrategy
{
    Random, TowerFirst, GreedyDps, MarkerSynergy,
    ChainFocus, SplitFocus, HeavyStack
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
        BotStrategy.HeavyStack    => PickHeavyStack(options, state),
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
                    "exploit_weakness" => hasMarker ? 20f : 2f,
                    "feedback_loop"    =>  8f,
                    "overkill"         =>  7f,
                    "chain_reaction"   =>  6f,
                    "focus_lens"       =>  6f,
                    "overreach"        =>  4f,
                    "slow"             =>  5f,
                    _                  =>  2f,
                };
                // Context-aware slot: some mods prefer specific tower types
                slot = opt.Id switch
                {
                    // Fast-attack mods: prefer rapid_shooter or chain_tower
                    "hair_trigger" or "feedback_loop" or "momentum" =>
                        eligible.OrderByDescending(i =>
                            s.Slots[i].Tower?.TowerId is "rapid_shooter" or "chain_tower" ? 1 : 0)
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
    // Keystone: marker marks enemies → EW gives +60% dmg to marked → momentum stacks on repeat hits.
    // Human pattern: marker + 3-4 rapid_shooters all stacked with EW + momentum.
    private DraftPick? PickMarkerSynergy(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        bool hasMarker  = s.Slots.Any(sl => sl.Tower?.TowerId == "marker_tower");
        int  dpsCount   = s.Slots.Count(sl => sl.Tower?.TowerId is "rapid_shooter" or "chain_tower");

        // 1. Need at least one DPS tower before chasing marker synergy
        if (dpsCount == 0 && empty.Count > 0)
        {
            var dps = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
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
            // 3. EW on every DPS tower that doesn't have it yet — ONLY if marker is placed
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

            // 5. Momentum on DPS towers (stacks to ×1.80 when hitting the same marked enemy)
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
                    .Where(i => s.Slots[i].Tower?.TowerId is "rapid_shooter" or "chain_tower" &&
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
                    .Where(i => s.Slots[i].Tower?.TowerId == "rapid_shooter")
                    .OrderBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .FirstOrDefault(-1);
                if (slot >= 0) return new DraftPick(fl, slot);
            }
        }

        // 8. Fill remaining slots with rapid_shooter (primary DPS + synergizes with EW)
        if (empty.Count > 0)
        {
            var dps = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter")
                   ?? opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (dps != null) return new DraftPick(dps, empty[0]);
        }

        // 9. Scored fallback for any remaining modifier — prefer DPS-boosting mods on DPS towers
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
                    "slow"             => 3f,
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
    // then pivot fully to direct DPS. Chain bounces decay ×0.60/hop — diminishing vs armored late game.
    private DraftPick? PickChainFocus(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);

        int chainReactionTotal = s.Slots.Sum(sl =>
            sl.Tower?.Modifiers.Count(m => m.ModifierId == "chain_reaction") ?? 0);
        bool hasDmgMod = s.Slots.Any(sl =>
            sl.Tower?.Modifiers.Any(m => m.ModifierId is
                "hair_trigger" or "momentum" or "split_shot" or "overkill" or "feedback_loop") == true);

        if (eligible.Count > 0)
        {
            // 1. Up to 2 chain_reactions total — always on chain_tower if possible
            if (chainReactionTotal < 2)
            {
                var cr = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "chain_reaction");
                if (cr != null)
                {
                    int slot = eligible
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId == "chain_tower" ? 2 :
                                                s.Slots[i].Tower?.TowerId == "rapid_shooter" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                        .First();
                    return new DraftPick(cr, slot);
                }
            }

            // 2. Exactly 1 damage modifier — pick the best available, with tower affinity
            if (!hasDmgMod)
            {
                // hair_trigger → fast towers; momentum → fast towers; split_shot → heavy_cannon
                var ht = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "hair_trigger");
                if (ht != null)
                {
                    int slot = eligible
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId is "rapid_shooter" or "chain_tower" ? 1 : 0)
                        .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count).First();
                    return new DraftPick(ht, slot);
                }
                var mom = opts.FirstOrDefault(o => o.Type == DraftOptionType.Modifier && o.Id == "momentum");
                if (mom != null)
                {
                    int slot = eligible
                        .OrderByDescending(i => s.Slots[i].Tower?.TowerId is "rapid_shooter" or "chain_tower" ? 1 : 0)
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

            // 3. Scored fallback for remaining picks — treat like GreedyDps from here
            DraftOption? bestMod = null;
            float bestScore = -1f;
            foreach (var opt in Mods(opts))
            {
                float score = opt.Id switch
                {
                    "hair_trigger"   => 10f,
                    "momentum"       =>  9f,
                    "feedback_loop"  =>  8f,
                    "split_shot"     =>  7f,
                    "overkill"       =>  6f,
                    "focus_lens"     =>  5f,
                    "chain_reaction" =>  3f,  // already have 2, deprioritize
                    "slow"           =>  4f,
                    _                =>  2f,
                };
                if (score > bestScore) { bestScore = score; bestMod = opt; }
            }
            if (bestMod != null)
            {
                int slot = eligible
                    .OrderByDescending(i => s.Slots[i].Tower?.TowerId is "rapid_shooter" or "chain_tower" ? 1 : 0)
                    .ThenBy(i => s.Slots[i].Tower!.Modifiers.Count)
                    .First();
                return new DraftPick(bestMod, slot);
            }
        }

        // Tower placement: chain_tower → rapid_shooter → heavy_cannon
        if (empty.Count > 0)
        {
            var chain = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "chain_tower");
            if (chain != null) return new DraftPick(chain, empty[0]);
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
            var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
            if (heavy != null) return new DraftPick(heavy, empty[0]);
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

    // Stack focus_lens + momentum on heavy_cannon for maximum single-shot burst damage.
    // Tests whether ultra-high-damage slow towers can handle hard waves.
    private DraftPick? PickHeavyStack(List<DraftOption> opts, RunState s)
    {
        var empty    = EmptySlots(s);
        var eligible = ModSlots(s);
        int heavyCount = s.Slots.Count(sl => sl.Tower?.TowerId == "heavy_cannon");

        if (eligible.Count > 0)
        {
            // 1. Focus Lens on heavy_cannon: ×2.25 damage, ×2 interval — still a big DPS gain
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

            // 2. Momentum on heavy_cannon: builds ×1.80 on armored/tanky clumps
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
                    .Where(i => s.Slots[i].Tower?.TowerId == "rapid_shooter")
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

        // Tower placement: 2 heavy_cannons for stacking, rest rapid_shooters for chip
        if (empty.Count > 0)
        {
            if (heavyCount < 2)
            {
                var heavy = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "heavy_cannon");
                if (heavy != null) return new DraftPick(heavy, empty[0]);
            }
            var rapid = opts.FirstOrDefault(o => o.Type == DraftOptionType.Tower && o.Id == "rapid_shooter");
            if (rapid != null) return new DraftPick(rapid, empty[0]);
        }

        return PickTowerFirst(opts, s);
    }
}
