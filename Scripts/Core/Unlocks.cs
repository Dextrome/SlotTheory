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
    public const string RiftPrismTowerId = "rift_prism";
    public const string RiftPrismAchievementId = "RIFT_UNSEALED";
    private const string ArcEmitterFallbackMapId = "sprawl";
    private const string RiftPrismFallbackMapId = "arena_classic";

    public static bool ShouldUnlockArcEmitter(RunState state, DifficultyMode difficulty)
    {
        string unlockMapId = GetArcEmitterUnlockMapId();
        if (!string.Equals(state.SelectedMapId, unlockMapId, StringComparison.OrdinalIgnoreCase))
            return false;

        return difficulty == DifficultyMode.Normal || difficulty == DifficultyMode.Hard;
    }

    public static bool ShouldUnlockRiftPrism(RunState state, DifficultyMode difficulty)
    {
        string unlockMapId = GetRiftPrismUnlockMapId();
        if (!string.Equals(state.SelectedMapId, unlockMapId, StringComparison.OrdinalIgnoreCase))
            return false;

        return difficulty == DifficultyMode.Normal || difficulty == DifficultyMode.Hard;
    }

    /// <summary>
    /// Arc Emitter unlock follows the first non-random campaign map by display order.
    /// This keeps progression behavior correct even if map ordering changes.
    /// </summary>
    public static string GetArcEmitterUnlockMapId()
        => GetCampaignMapByOrder(order: 0, fallbackId: ArcEmitterFallbackMapId);

    /// <summary>
    /// Rift Sapper unlock follows the second non-random campaign map by display order.
    /// </summary>
    public static string GetRiftPrismUnlockMapId()
        => GetCampaignMapByOrder(order: 1, fallbackId: RiftPrismFallbackMapId);

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
        // Bot simulations evaluate full content balance regardless of player progression.
        if (OS.GetCmdlineUserArgs().Contains("--bot"))
            return true;

        if (string.Equals(towerId, ArcEmitterTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(ArcEmitterAchievementId) == true;

        if (string.Equals(towerId, RiftPrismTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(RiftPrismAchievementId) == true;

        return true;
    }

    public static bool IsModifierUnlocked(string modifierId)
    {
        // Reserved for future progression-gated modifiers.
        // Keep bots fully unlocked for deterministic balance tests.
        if (OS.GetCmdlineUserArgs().Contains("--bot"))
            return true;

        _ = modifierId;
        return true;
    }
}
