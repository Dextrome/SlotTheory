using System;
using System.Linq;
using Godot;
using SlotTheory.Data;

namespace SlotTheory.Core;

/// <summary>
/// Progression gates for unlockable content.
/// </summary>
public static class Unlocks
{
    public const string ArcEmitterTowerId = "chain_tower";
    public const string ArcEmitterAchievementId = "ARC_UNSEALED";
    public const string SplitShotModifierId = "split_shot";
    public const string SplitShotAchievementId = "SPLIT_UNSEALED";
    public const string RiftPrismTowerId = "rift_prism";
    public const string RiftPrismAchievementId = "RIFT_UNSEALED";
    public const string AccordionEngineTowerId = "accordion_engine";
    public const string AccordionEngineAchievementId = "ACCORDION_UNSEALED";
    public const string BlastCoreModifierId = "blast_core";
    public const string BlastCoreAchievementId = "BLAST_UNSEALED";
    public const string WildfireModifierId = "wildfire";
    public const string WildfireAchievementId = "WILDFIRE_UNSEALED";
    private const string ArcEmitterFallbackMapId = "sprawl";
    private const string WildfireFallbackMapId = "six";
    private const string SplitShotFallbackMapId = "arena_classic";
    private const string RiftPrismFallbackMapId = "gauntlet";
    private const string AccordionEngineFallbackMapId = "double_back";
    private const string BlastCoreFallbackMapId = "ridgeback";

    public static bool ShouldUnlockArcEmitter(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetArcEmitterUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockSplitShot(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetSplitShotUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockRiftPrism(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetRiftPrismUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockAccordionEngine(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetAccordionEngineUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockBlastCore(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetBlastCoreUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockWildfire(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetWildfireUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    /// <summary>
    /// Arc Emitter unlock follows the first non-random campaign map by display order.
    /// This keeps progression behavior correct even if map ordering changes.
    /// </summary>
    public static string GetArcEmitterUnlockMapId()
        => GetCampaignMapByOrder(order: 0, fallbackId: ArcEmitterFallbackMapId);

    /// <summary>
    /// Split Shot unlock follows the second non-random campaign map by display order.
    /// </summary>
    public static string GetSplitShotUnlockMapId()
        => GetCampaignMapByOrder(order: 1, fallbackId: SplitShotFallbackMapId);

    /// <summary>
    /// Rift Sapper unlock follows the third non-random campaign map by display order.
    /// </summary>
    public static string GetRiftPrismUnlockMapId()
        => GetCampaignMapByOrder(order: 2, fallbackId: RiftPrismFallbackMapId);

    /// <summary>
    /// Accordion Engine unlock follows the fifth non-random campaign map by display order.
    /// </summary>
    public static string GetAccordionEngineUnlockMapId()
        => GetCampaignMapByOrder(order: 4, fallbackId: AccordionEngineFallbackMapId);

    /// <summary>
    /// Blast Core unlock follows the fourth non-random campaign map by display order.
    /// </summary>
    public static string GetBlastCoreUnlockMapId()
        => GetCampaignMapByOrder(order: 3, fallbackId: BlastCoreFallbackMapId);

    /// <summary>
    /// Wildfire unlock follows the sixth non-random campaign map by display order.
    /// </summary>
    public static string GetWildfireUnlockMapId()
        => GetCampaignMapByOrder(order: 5, fallbackId: WildfireFallbackMapId);

    private static string GetCampaignMapByOrder(int order, string fallbackId)
    {
        try
        {
            var map = DataLoader
                .GetAllMapDefs()
                .Where(m => !m.IsRandom)
                .OrderBy(m => m.DisplayOrder)
                .Skip(Mathf.Max(0, order))
                .FirstOrDefault();

            if (map != null && !string.IsNullOrWhiteSpace(map.Id))
                return map.Id;
        }
        catch
        {
            // Data may not be loaded yet in tooling edge cases; use stable fallback.
        }

        return fallbackId;
    }

    public static bool IsTowerUnlocked(string towerId)
    {
        // Accordion Engine unlocks on ridgeback, which is not in the demo map pool --
        // demo players can never reach it, so exclude from demo regardless of bot mode.
        if (Balance.IsDemo && string.Equals(towerId, AccordionEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Bot simulations evaluate full content balance regardless of player progression.
        if (OS.GetCmdlineUserArgs().Contains("--bot"))
            return true;

        if (string.Equals(towerId, ArcEmitterTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(ArcEmitterAchievementId) == true;

        if (string.Equals(towerId, RiftPrismTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(RiftPrismAchievementId) == true;

        if (string.Equals(towerId, AccordionEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(AccordionEngineAchievementId) == true;

        return true;
    }

    public static bool IsModifierUnlocked(string modifierId)
    {
        // Blast Core and Wildfire are full-game only -- excluded from demo regardless of bot mode.
        if (Balance.IsDemo && string.Equals(modifierId, BlastCoreModifierId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(modifierId, WildfireModifierId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Keep bots fully unlocked for deterministic balance tests.
        if (OS.GetCmdlineUserArgs().Contains("--bot"))
            return true;

        if (string.Equals(modifierId, SplitShotModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(SplitShotAchievementId) == true;

        if (string.Equals(modifierId, BlastCoreModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(BlastCoreAchievementId) == true;

        if (string.Equals(modifierId, WildfireModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(WildfireAchievementId) == true;

        return true;
    }

    private static bool IsRunOnUnlockMap(RunState state, string unlockMapId)
        => string.Equals(state.SelectedMapId, unlockMapId, StringComparison.OrdinalIgnoreCase);
}
