using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Thin Steam forwarding layer. Subscribed to AchievementManager.AchievementUnlocked
/// in AchievementManager._Ready(). All game logic and persistence lives in AchievementManager.
/// </summary>
public static class SteamAchievements
{
    /// <summary>
    /// Set to true by SteamLeaderboardService after SteamAPI.Init() succeeds.
    /// Guards against calling Steamworks with a null ISteamUserStats pointer.
    /// </summary>
    internal static bool IsSteamInitialized { get; set; }

    /// <summary>
    /// Called by AchievementManager signal whenever an achievement is newly unlocked.
    /// Forwards to Steamworks if available; silently no-ops otherwise.
    /// </summary>
    public static void ForwardUnlock(string id)
    {
        if (!IsSteamInitialized) return;
        try
        {
            Steamworks.SteamUserStats.GetAchievement(id, out bool alreadyUnlocked);
            if (alreadyUnlocked) return;
            if (Steamworks.SteamUserStats.SetAchievement(id))
                Steamworks.SteamUserStats.StoreStats();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Achievements] Steam forward failed for '{id}': {ex.Message}");
        }
    }
}
