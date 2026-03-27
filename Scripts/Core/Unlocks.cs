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
    public const string RocketLauncherTowerId = "rocket_launcher";
    public const string RocketLauncherAchievementId = "ROCKET_UNSEALED";
    public const string UndertowEngineTowerId = "undertow_engine";
    public const string UndertowEngineAchievementId = "UNDERTOW_UNSEALED";
    public const string BlastCoreModifierId = "blast_core";
    public const string BlastCoreAchievementId = "BLAST_UNSEALED";
    public const string WildfireModifierId = "wildfire";
    public const string WildfireAchievementId = "WILDFIRE_UNSEALED";
    public const string ReaperProtocolModifierId = "reaper_protocol";
    public const string ReaperProtocolAchievementId = "REAPER_UNSEALED";
    public const string PhaseSplitterTowerId = "phase_splitter";
    public const string PhaseSplitterAchievementId = "PHASE_UNSEALED";
    private const string ArcEmitterFallbackMapId = "orbit";
    private const string WildfireFallbackMapId = "crossfire";
    private const string SplitShotFallbackMapId = "crossroads";
    private const string RiftPrismFallbackMapId = "pinch_bleed";
    private const string AccordionEngineFallbackMapId = "double_back";
    private const string BlastCoreFallbackMapId = "ridgeback";
    private const string PhaseSplitterFallbackMapId = "threshold";
    private const string ReaperProtocolFallbackMapId = "switchback";
    private const string RocketLauncherUnlockMapId = "hourglass";
    private const string UndertowEngineUnlockMapId = "trident";

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

    public static bool ShouldUnlockPhaseSplitter(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetPhaseSplitterUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockReaperProtocol(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetReaperProtocolUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockRocketLauncher(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetRocketLauncherUnlockMapId();
        return IsRunOnUnlockMap(state, unlockMapId);
    }

    public static bool ShouldUnlockUndertowEngine(RunState state, DifficultyMode difficulty)
    {
        _ = difficulty;
        string unlockMapId = GetUndertowEngineUnlockMapId();
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

    /// <summary>
    /// Phase Splitter unlock follows the seventh non-random campaign map by display order.
    /// </summary>
    public static string GetPhaseSplitterUnlockMapId()
        => GetCampaignMapByOrder(order: 6, fallbackId: PhaseSplitterFallbackMapId);

    /// <summary>
    /// Reaper Protocol unlock follows the eighth non-random campaign map by display order.
    /// </summary>
    public static string GetReaperProtocolUnlockMapId()
        => GetCampaignMapByOrder(order: 7, fallbackId: ReaperProtocolFallbackMapId);

    /// <summary>
    /// Rocket Launcher unlock is tied to Hourglass.
    /// </summary>
    public static string GetRocketLauncherUnlockMapId()
        => RocketLauncherUnlockMapId;

    /// <summary>
    /// Undertow Engine unlock is tied to Trident.
    /// </summary>
    public static string GetUndertowEngineUnlockMapId()
        => UndertowEngineUnlockMapId;

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
        // Full-game-only towers are excluded from demo regardless of bot mode.
        if (Balance.IsDemo && string.Equals(towerId, AccordionEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(towerId, PhaseSplitterTowerId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(towerId, RocketLauncherTowerId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(towerId, UndertowEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Bot simulations evaluate full content balance regardless of player progression.
        // Restrict bypass to headless automation (or explicit opt-in) so an accidental
        // "--bot" launch arg in desktop runs doesn't silently unlock everything.
        if (ShouldBypassProgressionForBot())
            return true;

        if (string.Equals(towerId, ArcEmitterTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(ArcEmitterAchievementId) == true;

        if (string.Equals(towerId, RiftPrismTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(RiftPrismAchievementId) == true;

        if (string.Equals(towerId, AccordionEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(AccordionEngineAchievementId) == true;

        if (string.Equals(towerId, PhaseSplitterTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(PhaseSplitterAchievementId) == true;

        if (string.Equals(towerId, RocketLauncherTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(RocketLauncherAchievementId) == true;

        if (string.Equals(towerId, UndertowEngineTowerId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(UndertowEngineAchievementId) == true;

        return true;
    }

    public static bool IsModifierUnlocked(string modifierId)
    {
        // Blast Core, Wildfire, and Reaper Protocol are full-game only --
        // excluded from demo builds and demo bot simulations regardless of bot mode.
        if (Balance.IsDemo && string.Equals(modifierId, BlastCoreModifierId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(modifierId, WildfireModifierId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (Balance.IsDemo && string.Equals(modifierId, ReaperProtocolModifierId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Keep bots fully unlocked for deterministic balance tests.
        if (ShouldBypassProgressionForBot())
            return true;

        if (string.Equals(modifierId, SplitShotModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(SplitShotAchievementId) == true;

        if (string.Equals(modifierId, BlastCoreModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(BlastCoreAchievementId) == true;

        if (string.Equals(modifierId, WildfireModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(WildfireAchievementId) == true;

        if (string.Equals(modifierId, ReaperProtocolModifierId, StringComparison.OrdinalIgnoreCase))
            return AchievementManager.Instance?.IsUnlocked(ReaperProtocolAchievementId) == true;

        return true;
    }

    private static bool IsRunOnUnlockMap(RunState state, string unlockMapId)
        => string.Equals(state.SelectedMapId, unlockMapId, StringComparison.OrdinalIgnoreCase);

    private static bool ShouldBypassProgressionForBot()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (!args.Contains("--bot"))
            return false;

        // Headless bot runs are the normal automation path.
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
            return true;

        // Allow intentional non-headless bypass for tooling.
        return args.Contains("--bot_unlocks");
    }
}
