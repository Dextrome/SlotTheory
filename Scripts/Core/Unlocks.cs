using System;

namespace SlotTheory.Core;

/// <summary>
/// Progression gates for unlockable content.
/// </summary>
public static class Unlocks
{
    public const string RiftPrismTowerId = "rift_prism";
    public const string RiftPrismAchievementId = "RIFT_UNSEALED";
    private const string RiftPrismMapId = "sprawl";

    public static bool ShouldUnlockRiftPrism(RunState state, DifficultyMode difficulty)
    {
        if (!string.Equals(state.SelectedMapId, RiftPrismMapId, StringComparison.OrdinalIgnoreCase))
            return false;

        return difficulty == DifficultyMode.Normal || difficulty == DifficultyMode.Hard;
    }

    public static bool IsTowerUnlocked(string towerId)
    {
        if (!string.Equals(towerId, RiftPrismTowerId, StringComparison.OrdinalIgnoreCase))
            return true;

        return AchievementManager.Instance?.IsUnlocked(RiftPrismAchievementId) == true;
    }
}
