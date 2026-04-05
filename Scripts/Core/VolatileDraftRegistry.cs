using System.Collections.Generic;
using System.Linq;

namespace SlotTheory.Core;

public enum VolatileEffectScope
{
    Global,
    TargetTower,
}

public readonly record struct VolatileDraftDef(
    string Id,
    DraftOptionType OptionType,
    string OptionId,
    string Name,
    string UpsideText,
    string TradeoffText,
    string IdentityTag,
    int FlatDamageDelta = 0,
    float AttackIntervalMultiplier = 1f,
    float RangeBonus = 0f,
    float ChainRangeBonus = 0f,
    float SlowDurationMultiplier = 1f,
    VolatileEffectScope Scope = VolatileEffectScope.Global);

public static class VolatileDraftRegistry
{
    private static readonly VolatileDraftDef[] Definitions =
    {
        new(
            Id: "volatile_deadeye_oath",
            OptionType: DraftOptionType.Modifier,
            OptionId: SpectacleDefinitions.FocusLens,
            Name: "DEADEYE OATH",
            UpsideText: "+2 base damage to this tower",
            TradeoffText: "-12% range on this tower",
            IdentityTag: "DETONATION",
            FlatDamageDelta: 2,
            RangeBonus: -28f,
            Scope: VolatileEffectScope.TargetTower),
        new(
            Id: "volatile_storm_lattice",
            OptionType: DraftOptionType.Modifier,
            OptionId: SpectacleDefinitions.ChainReaction,
            Name: "STORM LATTICE",
            UpsideText: "+22% chain range on this tower",
            TradeoffText: "-1 base damage on this tower",
            IdentityTag: "CHAIN",
            FlatDamageDelta: -1,
            ChainRangeBonus: 22f,
            Scope: VolatileEffectScope.TargetTower),
        new(
            Id: "volatile_cryo_lock",
            OptionType: DraftOptionType.Modifier,
            OptionId: SpectacleDefinitions.ChillShot,
            Name: "CRYO LOCK",
            UpsideText: "This tower's slows last +35%",
            TradeoffText: "This tower attack interval +10% (slower)",
            IdentityTag: "PRESSURE",
            AttackIntervalMultiplier: 1.10f,
            SlowDurationMultiplier: 1.35f,
            Scope: VolatileEffectScope.TargetTower),
        new(
            Id: "volatile_siege_protocol",
            OptionType: DraftOptionType.Tower,
            OptionId: "",
            Name: "SIEGE PROTOCOL",
            UpsideText: "+3 base damage to this tower",
            TradeoffText: "This tower attack interval +12% (slower)",
            IdentityTag: "DETONATION",
            FlatDamageDelta: 3,
            AttackIntervalMultiplier: 1.12f,
            Scope: VolatileEffectScope.TargetTower),
        new(
            Id: "volatile_phase_net",
            OptionType: DraftOptionType.Tower,
            OptionId: "",
            Name: "PHASE NET",
            UpsideText: "+18% chain range on this tower",
            TradeoffText: "-8% range on this tower",
            IdentityTag: "CHAIN",
            ChainRangeBonus: 18f,
            RangeBonus: -20f,
            Scope: VolatileEffectScope.TargetTower),
    };

    public static IReadOnlyList<VolatileDraftDef> GetDefinitions() => Definitions;

    public static bool TryGet(string id, out VolatileDraftDef def)
    {
        foreach (var item in Definitions)
        {
            if (item.Id == id)
            {
                def = item;
                return true;
            }
        }

        def = default;
        return false;
    }

    public static bool TryGetForOption(DraftOption option, out VolatileDraftDef def)
    {
        if (!option.IsVolatile || string.IsNullOrWhiteSpace(option.VolatileRuleId))
        {
            def = default;
            return false;
        }

        return TryGet(option.VolatileRuleId, out def);
    }

    public static string BuildCardTag(DraftOption option)
    {
        if (!TryGetForOption(option, out var def))
            return string.Empty;
        return $"VOLATILE {def.IdentityTag}";
    }

    public static string BuildTooltip(DraftOption option)
    {
        if (!TryGetForOption(option, out var def))
            return string.Empty;
        return $"{def.Name}: {def.UpsideText}. Tradeoff: {def.TradeoffText}.";
    }
}
