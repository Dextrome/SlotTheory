using Godot;
using System.Linq;
using System.Collections.Generic;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton - source of truth for achievement state.
///
/// Responsibilities:
///   - Persist unlock state to user://achievements.cfg
///   - Emit AchievementUnlocked signal when newly unlocked (drives toast + Steam forward)
///   - Evaluate run-end conditions (logic previously in SteamAchievements)
///
/// Steam forwarding: SteamAchievements.cs subscribes to AchievementUnlocked and
/// calls Steamworks when available - this class has zero Steam dependency.
/// </summary>
public partial class AchievementManager : Node
{
    public static AchievementManager? Instance { get; private set; }

    private const string SavePath = "user://achievements.cfg";
    private const string Section  = "unlocked";

    // ── Evaluation constants ──────────────────────────────────────────────────
    private const float SpeedRunMaxSeconds  = 15f * 60f;  // 15 minutes
    private const int   AnnihilatorDamage   = 100_000;
    private const int   DevastatorDamage    = 200_000;
    private const int   HalfwayWaveIndex    = 9;           // 0-based → wave 10
    private const int   EndlessWave25Index  = 24;          // 0-based → wave 25
    private const int   EndlessWave30Index  = 29;
    private const int   EndlessWave40Index  = 39;
    private const int   OverEquippedMinMods = 18;          // 6 towers × 3 modifiers
    private const int   ChainGangMinArcs    = 3;

    // ── Achievement definitions ───────────────────────────────────────────────

    public static readonly AchievementDefinition[] All =
    [
        new("TUTORIAL_COMPLETE", "First Steps",   "Complete the tutorial run."),
        new("FIRST_WIN",     "First Victory",     "Complete all 20 waves for the first time."),
        new("HARD_WIN",      "Hard Carry",         "Complete all 20 waves on Hard difficulty."),
        new(Unlocks.ArcEmitterAchievementId, "Arc Unsealed", "Beat the first campaign map to unlock Arc Emitter."),
        new(Unlocks.SplitShotAchievementId, "Split Unsealed", "Beat the second campaign map to unlock Split Shot."),
        new(Unlocks.RiftPrismAchievementId, "Rift Unsealed", "Beat the third campaign map to unlock Rift Sapper."),
        new(Unlocks.AccordionEngineAchievementId, "Accordion Unsealed", "Beat the fifth campaign map to unlock Accordion Engine."),
        new(Unlocks.BlastCoreAchievementId, "Blast Unsealed", "Beat the fourth campaign map to unlock Blast Core."),
        new(Unlocks.WildfireAchievementId, "Wildfire Unsealed", "Beat the sixth campaign map to unlock Wildfire."),
        new(Unlocks.AfterimageAchievementId, "Afterimage Unsealed", "Beat Perimeter Lock on any difficulty to unlock Afterimage."),
        new(Unlocks.PhaseSplitterAchievementId, "Phase Unsealed", "Beat the seventh campaign map to unlock Phase Splitter."),
        new(Unlocks.ReaperProtocolAchievementId, "Reaper Unsealed", "Beat the eighth campaign map to unlock Reaper Protocol."),
        new(Unlocks.RocketLauncherAchievementId, "Rocket Unsealed", "Beat Hourglass on any difficulty to unlock Rocket Launcher."),
        new(Unlocks.UndertowEngineAchievementId, "Undertow Unsealed", "Beat Trident on any difficulty to unlock Undertow Engine."),
        new(Unlocks.LatchNestAchievementId, "Latch Unsealed", "Beat Ziggurat on any difficulty to unlock Latch Nest."),
        new("FLAWLESS",      "Flawless",           "Win a run without losing a single life."),
        new("LAST_STAND",    "Last Stand",         "Win a run with exactly 1 life remaining."),
        new("HALFWAY_THERE", "Halfway There",      "Survive to wave 10 in any run."),
        new("FULL_HOUSE",    "Full House",         "Fill all 6 tower slots in a single run."),
        new("STACKED",       "Stacked",            "Give any tower 3 modifiers in a single run."),
        new("SPEED_RUN",     "Speed Run",          "Win a run in under 15 minutes."),
        new("ANNIHILATOR",   "Annihilator",        "Deal 100,000 total damage in a single run."),
        new("CHAIN_MASTER",  "Chain Master",       "Win with all 6 slots filled by Arc Emitters."),
        new("DEVASTATOR",    "Devastator",         "Deal 200,000 total damage in a single run."),
        new("KEEP_GOING",    "Keep Going",         "Start an endless run."),
        new("ENDLESS_25",    "Into the Void",      "Clear wave 25 in endless mode."),
        new("ENDLESS_30",    "No End in Sight",    "Clear wave 30 in endless mode."),
        new("ENDLESS_40",    "The Abyss",          "Clear wave 40 in endless mode."),
        new("FULL_ARSENAL",  "Full Arsenal",       "Use 5 different towers in one run."),
        new("OVER_EQUIPPED", "Over Equipped",      "Fill all 18 modifier slots in a single run."),
        new("CHAIN_GANG",    "Chain Gang",         "Place 3 or more Arc Emitters in a single run."),
        new("GLASS_CANNON",  "Glass Cannon",       "Equip Focus Lens and Hair Trigger on the same tower."),
        new("CAMPAIGN_CLEAR",      "The Circuit",    "Clear all four stages of The Fracture Circuit."),
        new("CAMPAIGN_HARD_CLEAR", "Iron Mandate",   "Clear all four stages of The Fracture Circuit on Hard difficulty."),
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
    /// Clears progression unlock flags for this profile (tower/modifier gates only).
    /// </summary>
    public void ResetUnlockFlags()
    {
        bool changed = false;
        foreach (string id in GetProgressionUnlockIds())
        {
            if (!_cfg.HasSectionKey(Section, id))
                continue;
            _cfg.EraseSectionKey(Section, id);
            changed = true;
        }

        if (!changed)
            return;

        Save();
        GD.Print("[Achievements] Progression unlock flags reset for current profile.");
    }

    /// <summary>
    /// Wipes every achievement unlock. Dev/testing tool only.
    /// </summary>
    public void ResetAllAchievements()
    {
        _cfg.EraseSection(Section);
        Save();
        GD.Print("[Achievements] All achievements reset.");
    }

    /// <summary>
    /// Evaluates all achievements at run end. Call from GameController with the
    /// final RunState (before it is cleared).
    /// </summary>
    public IReadOnlyList<string> CheckRunEndAndCollectUnlocks(RunState state, DifficultyMode difficulty, bool won,
        bool isTutorialRun = false)
    {
        var newlyUnlocked = new List<string>();

        void TryUnlock(string id)
        {
            if (Unlock(id))
                newlyUnlocked.Add(id);
        }

        // Keep progression/achievement saves deterministic for real play only.
        // Bot simulations should not consume unlocks or suppress toasts in normal runs.
        if (OS.GetCmdlineUserArgs().Contains("--bot"))
            return newlyUnlocked;

        // Tutorial runs only award TUTORIAL_COMPLETE (handled by CheckTutorialComplete).
        // All other achievements require a real run.
        if (isTutorialRun)
            return newlyUnlocked;

        // Wave milestone (win or loss)
        if (state.WaveIndex >= HalfwayWaveIndex)
            TryUnlock("HALFWAY_THERE");

        // Slot / modifier milestones (win or loss)
        if (state.FreeSlotCount() == 0)
            TryUnlock("FULL_HOUSE");

        foreach (var slot in state.Slots)
        {
            if (slot.Tower?.Modifiers.Count >= Balance.MaxModifiersPerTower)
            {
                TryUnlock("STACKED");
                break;
            }
        }

        // Damage milestones (win or loss)
        if (state.TotalDamageDealt >= AnnihilatorDamage)
            TryUnlock("ANNIHILATOR");
        if (state.TotalDamageDealt >= DevastatorDamage)
            TryUnlock("DEVASTATOR");

        // Win-only achievements
        if (won)
        {
            TryUnlock("FIRST_WIN");

            if (difficulty == DifficultyMode.Hard)
                TryUnlock("HARD_WIN");

            if (Unlocks.ShouldUnlockArcEmitter(state, difficulty))
                TryUnlock(Unlocks.ArcEmitterAchievementId);

            if (Unlocks.ShouldUnlockSplitShot(state, difficulty))
                TryUnlock(Unlocks.SplitShotAchievementId);

            if (Unlocks.ShouldUnlockRiftPrism(state, difficulty))
                TryUnlock(Unlocks.RiftPrismAchievementId);

            if (Unlocks.ShouldUnlockAccordionEngine(state, difficulty))
                TryUnlock(Unlocks.AccordionEngineAchievementId);

            if (Unlocks.ShouldUnlockBlastCore(state, difficulty))
                TryUnlock(Unlocks.BlastCoreAchievementId);

            if (Unlocks.ShouldUnlockWildfire(state, difficulty))
                TryUnlock(Unlocks.WildfireAchievementId);

            if (Unlocks.ShouldUnlockAfterimage(state, difficulty))
                TryUnlock(Unlocks.AfterimageAchievementId);

            if (Unlocks.ShouldUnlockPhaseSplitter(state, difficulty))
                TryUnlock(Unlocks.PhaseSplitterAchievementId);

            if (Unlocks.ShouldUnlockReaperProtocol(state, difficulty))
                TryUnlock(Unlocks.ReaperProtocolAchievementId);

            if (Unlocks.ShouldUnlockRocketLauncher(state, difficulty))
                TryUnlock(Unlocks.RocketLauncherAchievementId);

            if (Unlocks.ShouldUnlockUndertowEngine(state, difficulty))
                TryUnlock(Unlocks.UndertowEngineAchievementId);

            if (Unlocks.ShouldUnlockLatchNest(state, difficulty))
                TryUnlock(Unlocks.LatchNestAchievementId);

            if (state.Lives == state.MaxLives)
                TryUnlock("FLAWLESS");

            if (state.Lives == 1)
                TryUnlock("LAST_STAND");

            if (state.TotalPlayTime <= SpeedRunMaxSeconds)
                TryUnlock("SPEED_RUN");

            if (AllFilledSlotsAreArcEmitter(state))
                TryUnlock("CHAIN_MASTER");
        }

        return newlyUnlocked;
    }

    public void CheckRunEnd(RunState state, DifficultyMode difficulty, bool won)
        => _ = CheckRunEndAndCollectUnlocks(state, difficulty, won);

    /// <summary>
    /// Called at wave 10 start. Unlocks HALFWAY_THERE mid-run.
    /// No-op in bot mode or if already unlocked.
    /// </summary>
    public void CheckHalfwayThere()
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return;
        Unlock("HALFWAY_THERE");
    }

    /// <summary>
    /// Called after each draft pick. Unlocks build-related achievements mid-run.
    /// No-op in bot mode or if already unlocked.
    /// </summary>
    public void CheckDraftMilestones(RunState state)
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return;

        if (state.FreeSlotCount() == 0)
            Unlock("FULL_HOUSE");

        int totalMods = 0;
        int arcCount  = 0;
        var towerIds  = new System.Collections.Generic.HashSet<string>();
        bool hasGlassCannon = false;

        foreach (var slot in state.Slots)
        {
            if (slot.Tower == null) continue;
            towerIds.Add(slot.Tower.TowerId);
            int modCount = slot.Tower.Modifiers.Count;
            totalMods += modCount;
            if (modCount >= Balance.MaxModifiersPerTower)
                Unlock("STACKED");
            if (slot.Tower.TowerId == "chain_tower")
                arcCount++;
            if (slot.Tower.Modifiers.Exists(m => m.ModifierId == "focus_lens") &&
                slot.Tower.Modifiers.Exists(m => m.ModifierId == "hair_trigger"))
                hasGlassCannon = true;
        }

        if (totalMods >= OverEquippedMinMods)
            Unlock("OVER_EQUIPPED");

        if (towerIds.Count >= 5)
            Unlock("FULL_ARSENAL");

        if (arcCount >= ChainGangMinArcs)
            Unlock("CHAIN_GANG");

        if (hasGlassCannon)
            Unlock("GLASS_CANNON");
    }

    /// <summary>
    /// Called after each wave clear. Unlocks ANNIHILATOR and DEVASTATOR mid-run.
    /// No-op in bot mode or if already unlocked.
    /// </summary>
    public void CheckAnnihilator(RunState state)
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return;
        if (state.TotalDamageDealt >= AnnihilatorDamage)
            Unlock("ANNIHILATOR");
        if (state.TotalDamageDealt >= DevastatorDamage)
            Unlock("DEVASTATOR");
    }

    /// <summary>
    /// Called after each endless wave clear. Unlocks wave-depth milestones.
    /// No-op in bot mode.
    /// </summary>
    public void CheckEndlessMilestones(RunState state)
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return;
        if (state.WaveIndex >= EndlessWave25Index) Unlock("ENDLESS_25");
        if (state.WaveIndex >= EndlessWave30Index) Unlock("ENDLESS_30");
        if (state.WaveIndex >= EndlessWave40Index) Unlock("ENDLESS_40");
    }

    /// <summary>
    /// Called when the player presses Continue - Endless. Unlocks KEEP_GOING.
    /// No-op in bot mode.
    /// </summary>
    public void CheckKeepGoing()
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return;
        Unlock("KEEP_GOING");
    }

    /// <summary>
    /// Called when the tutorial run is completed. Unlocks TUTORIAL_COMPLETE.
    /// Safe to call multiple times (Unlock is idempotent).
    /// </summary>
    public void CheckTutorialComplete()
    {
        Unlock("TUTORIAL_COMPLETE");
    }

    /// <summary>
    /// Returns 1–2 lines of near-miss or forward-goal hints for the end screen.
    /// Near-misses from this run take priority; falls back to the single most
    /// accessible unearned achievement as a forward goal. Returns null if nothing
    /// relevant to show.
    /// </summary>
    public string? GetGoalHint(RunState state, DifficultyMode difficulty, bool won)
    {
        if (OS.GetCmdlineUserArgs().Contains("--bot")) return null;

        var hints = new System.Collections.Generic.List<string>();

        // ── Near-misses: things that almost happened this run ─────────────
        if (won)
        {
            int livesLost = state.MaxLives - state.Lives;
            if (!IsUnlocked("FLAWLESS") && livesLost > 0 && livesLost <= 3)
                hints.Add($"Almost FLAWLESS - lost {livesLost} {(livesLost == 1 ? "life" : "lives")}");

            if (!IsUnlocked("LAST_STAND") && state.Lives == 2)
                hints.Add("Almost LAST STAND - won with 2 lives (target: 1)");

            if (!IsUnlocked("SPEED_RUN") && state.TotalPlayTime > SpeedRunMaxSeconds
                && state.TotalPlayTime <= SpeedRunMaxSeconds + 90f)
            {
                int overBy = (int)(state.TotalPlayTime - SpeedRunMaxSeconds);
                hints.Add($"Almost SPEED RUN - {overBy}s over the 15:00 limit");
            }
        }

        // Chain Master near-miss: won with 4+ Arc Emitters in filled slots
        if (won && !IsUnlocked("CHAIN_MASTER") && IsUnlocked(Unlocks.ArcEmitterAchievementId))
        {
            int arcCount    = state.Slots.Count(s => s.Tower?.TowerId == "chain_tower");
            int filledCount = state.Slots.Count(s => s.Tower != null);
            if (arcCount >= 4 && arcCount < filledCount)
                hints.Add($"Almost CHAIN MASTER - {arcCount}/{filledCount} slots were Arc Emitters");
            else if (arcCount == filledCount && filledCount >= 4 && filledCount < Balance.SlotCount)
                hints.Add($"Almost CHAIN MASTER - all {arcCount} filled slots are Arc Emitters, fill the rest");
        }

        // Annihilator near-miss applies on win or loss
        if (!IsUnlocked("ANNIHILATOR")
            && state.TotalDamageDealt >= AnnihilatorDamage * 0.70f
            && state.TotalDamageDealt < AnnihilatorDamage)
        {
            hints.Add($"Almost ANNIHILATOR - {state.TotalDamageDealt:N0} / {AnnihilatorDamage:N0} damage");
        }

        if (hints.Count > 0)
            return string.Join("\n", hints.Take(2));

        // ── Forward goals: most accessible unearned achievement ───────────
        if (!won && !IsUnlocked("FIRST_WIN"))
            return "Next goal: FIRST VICTORY - complete all 20 waves";

        if (won && difficulty != DifficultyMode.Hard && !IsUnlocked("HARD_WIN"))
            return "Next goal: HARD CARRY - win on Hard difficulty";

        if (won && !IsUnlocked("FLAWLESS"))
            return "Next goal: FLAWLESS - win without losing a single life";

        if (won && !IsUnlocked("SPEED_RUN"))
        {
            int s = (int)state.TotalPlayTime;
            return $"Next goal: SPEED RUN - win in under 15:00  (this run: {s / 60}:{s % 60:D2})";
        }

        if (!IsUnlocked("ANNIHILATOR"))
            return $"Next goal: ANNIHILATOR - deal 100,000 damage in one run";

        if (!IsUnlocked("CHAIN_MASTER") && IsUnlocked(Unlocks.ArcEmitterAchievementId))
            return "Next goal: CHAIN MASTER - win with Arc Emitters in all 6 slots";

        return null;
    }

    /// <summary>
    /// Pushes all locally-unlocked achievements to Steam.
    /// Call once after Steam successfully initializes to sync achievements that were
    /// unlocked locally before the Steam DLL was available.
    /// </summary>
    public void SyncAllToSteam()
    {
        foreach (var def in All)
            if (IsUnlocked(def.Id))
                SteamAchievements.ForwardUnlock(def.Id);
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

    private static IReadOnlyList<string> GetProgressionUnlockIds()
        => new[]
        {
            Unlocks.ArcEmitterAchievementId,
            Unlocks.SplitShotAchievementId,
            Unlocks.RiftPrismAchievementId,
            Unlocks.AccordionEngineAchievementId,
            Unlocks.BlastCoreAchievementId,
            Unlocks.WildfireAchievementId,
            Unlocks.AfterimageAchievementId,
            Unlocks.PhaseSplitterAchievementId,
            Unlocks.ReaperProtocolAchievementId,
            Unlocks.RocketLauncherAchievementId,
            Unlocks.UndertowEngineAchievementId,
            Unlocks.LatchNestAchievementId,
        };

    private void Load()
    {
        SteamCloudSync.PullIfNewer(ProjectSettings.GlobalizePath(SavePath), "achievements.cfg");
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
        else
            SteamCloudSync.Push(ProjectSettings.GlobalizePath(SavePath), "achievements.cfg");
    }
}
