using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton — source of truth for achievement state.
///
/// Responsibilities:
///   - Persist unlock state to user://achievements.cfg
///   - Emit AchievementUnlocked signal when newly unlocked (drives toast + Steam forward)
///   - Evaluate run-end conditions (logic previously in SteamAchievements)
///
/// Steam forwarding: SteamAchievements.cs subscribes to AchievementUnlocked and
/// calls Steamworks when available — this class has zero Steam dependency.
/// </summary>
public partial class AchievementManager : Node
{
    public static AchievementManager? Instance { get; private set; }

    private const string SavePath = "user://achievements.cfg";
    private const string Section  = "unlocked";

    // ── Evaluation constants ──────────────────────────────────────────────────
    private const float SpeedRunMaxSeconds = 8f * 60f;  // 8 minutes
    private const int   AnnihilatorDamage  = 100_000;
    private const int   HalfwayWaveIndex   = 9;          // 0-based → wave 10

    // ── Achievement definitions ───────────────────────────────────────────────

    public static readonly AchievementDefinition[] All =
    [
        new("FIRST_WIN",     "First Victory",     "Complete all 20 waves for the first time."),
        new("HARD_WIN",      "Hard Carry",         "Complete all 20 waves on Hard difficulty."),
        new(Unlocks.RiftPrismAchievementId, "Rift Unsealed", "Beat Orbit on Normal or Hard to unlock Rift Prism."),
        new("FLAWLESS",      "Flawless",           "Win a run without losing a single life."),
        new("LAST_STAND",    "Last Stand",         "Win a run with exactly 1 life remaining."),
        new("HALFWAY_THERE", "Halfway There",      "Survive to wave 10 in any run."),
        new("FULL_HOUSE",    "Full House",         "Fill all 6 tower slots in a single run."),
        new("STACKED",       "Stacked",            "Give any tower 3 modifiers in a single run."),
        new("SPEED_RUN",     "Speed Run",          "Win a run in under 8 minutes."),
        new("ANNIHILATOR",   "Annihilator",        "Deal 100,000 total damage in a single run."),
        new("CHAIN_MASTER",  "Chain Master",       "Win with all 6 slots filled by Arc Emitters."),
    ];

    // ── Runtime state ─────────────────────────────────────────────────────────

    [Signal]
    public delegate void AchievementUnlockedEventHandler(string id);

    private ConfigFile _cfg = new();

    public override void _Ready()
    {
        Instance = this;
        Load();
        // Wire Steam forwarding
        AchievementUnlocked += SteamAchievements.ForwardUnlock;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns true if the achievement was newly unlocked (false if already had it).</summary>
    public bool Unlock(string id)
    {
        if (IsUnlocked(id)) return false;
        _cfg.SetValue(Section, id, true);
        Save();
        GD.Print($"[Achievements] Unlocked: {id}");
        EmitSignal(SignalName.AchievementUnlocked, id);
        return true;
    }

    public bool IsUnlocked(string id)
        => _cfg.HasSectionKey(Section, id) && (bool)_cfg.GetValue(Section, id, false);

    /// <summary>
    /// Evaluates all achievements at run end. Call from GameController with the
    /// final RunState (before it is cleared).
    /// </summary>
    public void CheckRunEnd(RunState state, DifficultyMode difficulty, bool won)
    {
        // Wave milestone (win or loss)
        if (state.WaveIndex >= HalfwayWaveIndex)
            Unlock("HALFWAY_THERE");

        // Slot / modifier milestones (win or loss)
        if (state.FreeSlotCount() == 0)
            Unlock("FULL_HOUSE");

        foreach (var slot in state.Slots)
        {
            if (slot.Tower?.Modifiers.Count >= Balance.MaxModifiersPerTower)
            {
                Unlock("STACKED");
                break;
            }
        }

        // Damage milestone (win or loss)
        if (state.TotalDamageDealt >= AnnihilatorDamage)
            Unlock("ANNIHILATOR");

        // Win-only achievements
        if (won)
        {
            Unlock("FIRST_WIN");

            if (difficulty == DifficultyMode.Hard)
                Unlock("HARD_WIN");

            if (Unlocks.ShouldUnlockRiftPrism(state, difficulty))
                Unlock(Unlocks.RiftPrismAchievementId);

            if (state.Lives == Balance.StartingLives)
                Unlock("FLAWLESS");

            if (state.Lives == 1)
                Unlock("LAST_STAND");

            if (state.TotalPlayTime <= SpeedRunMaxSeconds)
                Unlock("SPEED_RUN");

            if (AllFilledSlotsAreArcEmitter(state))
                Unlock("CHAIN_MASTER");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private void Load()
    {
        _cfg = new ConfigFile();
        var err = _cfg.Load(SavePath);
        if (err != Error.Ok && err != Error.FileNotFound)
            GD.PrintErr($"[Achievements] Failed to load {SavePath}: {err}");
    }

    private void Save()
    {
        var err = _cfg.Save(SavePath);
        if (err != Error.Ok)
            GD.PrintErr($"[Achievements] Failed to save {SavePath}: {err}");
    }
}
