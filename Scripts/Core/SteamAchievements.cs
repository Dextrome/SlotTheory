using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Wraps Steam achievement unlocks. All methods are safe to call even when
/// Steam is unavailable — failures are silently swallowed.
/// </summary>
public static class SteamAchievements
{
    // ── IDs must match exactly what you define in the Steamworks partner portal ──
    public const string FirstWin      = "FIRST_WIN";
    public const string HardWin       = "HARD_WIN";
    public const string Flawless      = "FLAWLESS";
    public const string LastStand     = "LAST_STAND";
    public const string HalfwayThere  = "HALFWAY_THERE";
    public const string FullHouse     = "FULL_HOUSE";
    public const string Stacked       = "STACKED";
    public const string SpeedRun      = "SPEED_RUN";
    public const string Annihilator   = "ANNIHILATOR";
    public const string ChainMaster   = "CHAIN_MASTER";

    private const float SpeedRunMaxSeconds  = 8f * 60f;   // 8 minutes
    private const int   AnnihilatorDamage   = 100_000;
    private const int   HalfwayWaveIndex    = 9;           // 0-based → wave 10

    /// <summary>
    /// Call once at the end of every run (win or loss) while RunState is still populated.
    /// Evaluates all achievements and flushes stats to Steam in a single StoreStats call.
    /// </summary>
    public static void CheckRunEnd(RunState state, DifficultyMode difficulty, bool won)
    {
        if (!IsAvailable()) return;

        bool dirty = false;

        // ── Wave milestone (win or loss) ───────────────────────────────────────
        if (state.WaveIndex >= HalfwayWaveIndex)
            dirty |= Unlock(HalfwayThere);

        // ── Slot/modifier milestones (win or loss) ─────────────────────────────
        if (state.FreeSlotCount() == 0)
            dirty |= Unlock(FullHouse);

        foreach (var slot in state.Slots)
        {
            if (slot.Tower?.Modifiers.Count >= Balance.MaxModifiersPerTower)
            {
                dirty |= Unlock(Stacked);
                break;
            }
        }

        // ── Damage milestone (win or loss) ─────────────────────────────────────
        if (state.TotalDamageDealt >= AnnihilatorDamage)
            dirty |= Unlock(Annihilator);

        // ── Win-only achievements ───────────────────────────────────────────────
        if (won)
        {
            dirty |= Unlock(FirstWin);

            if (difficulty == DifficultyMode.Hard)
                dirty |= Unlock(HardWin);

            if (state.Lives == Balance.StartingLives)
                dirty |= Unlock(Flawless);

            if (state.Lives == 1)
                dirty |= Unlock(LastStand);

            if (state.TotalPlayTime <= SpeedRunMaxSeconds)
                dirty |= Unlock(SpeedRun);

            if (AllFilledSlotsAreArcEmitter(state))
                dirty |= Unlock(ChainMaster);
        }

        if (dirty) StoreStats();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static bool IsAvailable()
    {
        if (OS.GetName() != "Windows") return false;
        try { return Steamworks.SteamAPI.IsSteamRunning(); }
        catch { return false; }
    }

    /// <summary>Sets the achievement. Returns true if the state changed (was not already set).</summary>
    private static bool Unlock(string id)
    {
        try
        {
            Steamworks.SteamUserStats.GetAchievement(id, out bool alreadyUnlocked);
            if (alreadyUnlocked) return false;
            bool ok = Steamworks.SteamUserStats.SetAchievement(id);
            if (ok) GD.Print($"[Achievements] Unlocked: {id}");
            return ok;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Achievements] Failed to unlock '{id}': {ex.Message}");
            return false;
        }
    }

    private static void StoreStats()
    {
        try { Steamworks.SteamUserStats.StoreStats(); }
        catch (System.Exception ex) { GD.PrintErr($"[Achievements] StoreStats failed: {ex.Message}"); }
    }

    private static bool AllFilledSlotsAreArcEmitter(RunState state)
    {
        bool anyTower = false;
        foreach (var slot in state.Slots)
        {
            if (slot.Tower == null) continue;
            anyTower = true;
            if (slot.Tower.TowerId != "chain_tower") return false;
        }
        return anyTower;
    }
}
