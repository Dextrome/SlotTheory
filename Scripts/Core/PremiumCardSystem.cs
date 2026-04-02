using System.Collections.Generic;
using System.Linq;

namespace SlotTheory.Core;

/// <summary>Rarity tier for premium cards.</summary>
public enum PremiumRarity { Rare, SuperRare }

/// <summary>Definition for a premium draft card. All cards are code-defined (no JSON).</summary>
public record PremiumCardDef(
    string Id,
    string Name,
    string Description,
    PremiumRarity Rarity,
    bool RequiresTarget = false
);

/// <summary>
/// Central registry for all premium cards. Single source of truth for IDs,
/// definitions, and per-card copy caps.
/// </summary>
public static class PremiumCardRegistry
{
    // ── Super Rare card IDs ──────────────────────────────────────────────────
    public const string BetterOddsId           = "better_odds";
    public const string KineticCalibrationId   = "kinetic_calibration";
    public const string HotLoadersId           = "hot_loaders";

    // ── Rare card IDs ────────────────────────────────────────────────────────
    public const string ExpandedChassisId      = "expanded_chassis";
    public const string EmergencyReservesId    = "emergency_reserves";
    public const string HardenedReservesId     = "hardened_reserves";
    public const string LongFuseId             = "long_fuse";
    public const string SignalBoostId          = "signal_boost";
    public const string MultitargetRelayId     = "multitarget_relay";
    public const string ExtendedRailsId        = "extended_rails";
    public const string ColdCircuitId          = "cold_circuit";

    private static readonly List<PremiumCardDef> _cards = new()
    {
        // ── Super Rare ───────────────────────────────────────────────────────
        new(BetterOddsId,
            "Better Odds",
            "Future drafts show 1 extra card.",
            PremiumRarity.SuperRare),

        new(KineticCalibrationId,
            "Kinetic Calibration",
            "All towers gain +1 base damage.",
            PremiumRarity.SuperRare),

        new(HotLoadersId,
            "Hot Loaders",
            "All towers reload slightly faster.",
            PremiumRarity.SuperRare),

        // ── Rare ─────────────────────────────────────────────────────────────
        new(ExpandedChassisId,
            "Expanded Chassis",
            "Give one tower +1 mod slot (up to 5).",
            PremiumRarity.Rare,
            RequiresTarget: true),

        new(EmergencyReservesId,
            "Emergency Reserves",
            "Gain +5 lives.",
            PremiumRarity.Rare),

        new(HardenedReservesId,
            "Hardened Reserves",
            "Increase max lives by +1.",
            PremiumRarity.Rare),

        new(LongFuseId,
            "Long Fuse",
            "Explosion radius is slightly increased globally.",
            PremiumRarity.Rare),

        new(SignalBoostId,
            "Signal Boost",
            "Marked enemies stay marked longer.",
            PremiumRarity.Rare),

        new(MultitargetRelayId,
            "Multitarget Relay",
            "Chain effects gain 10px more reach.",
            PremiumRarity.Rare),

        new(ExtendedRailsId,
            "Extended Rails",
            "All towers gain a small range increase.",
            PremiumRarity.Rare),

        new(ColdCircuitId,
            "Cold Circuit",
            "Chill Shot slows last 35% longer.",
            PremiumRarity.Rare),
    };

    private static readonly Dictionary<string, PremiumCardDef> _byId =
        _cards.ToDictionary(c => c.Id);

    public static IReadOnlyList<PremiumCardDef> GetAll()                         => _cards;
    public static IEnumerable<PremiumCardDef>   GetByRarity(PremiumRarity r)     => _cards.Where(c => c.Rarity == r);
    public static PremiumCardDef?               TryGet(string id)                => _byId.GetValueOrDefault(id);
    public static bool                          RequiresTarget(string id)        => _byId.TryGetValue(id, out var c) && c.RequiresTarget;

    /// <summary>Maximum copies of a given premium card that can appear in one run.</summary>
    public static int GetMaxCopies(string id) => id switch
    {
        BetterOddsId       => 1,  // one-time upgrade only
        ExpandedChassisId  => 6,  // one per tower slot
        _                  => Balance.MaxPremiumCardCopiesDefault,
    };
}
