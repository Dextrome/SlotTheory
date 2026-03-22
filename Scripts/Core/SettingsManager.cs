using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Loads, persists, and applies audio/display settings.
/// Creates Music and FX audio buses (children of Master) on first run.
///
/// Two save files:
///   user://settings.cfg  - account/preference settings; cloud-synced via SteamCloudSync
///   user://display.cfg   - device-specific display/render settings; NOT cloud-synced
/// </summary>
public partial class SettingsManager : Node
{
    public static SettingsManager? Instance { get; private set; }

    public static event System.Action<bool>? ScreenFilterChanged;
    public static event System.Action<bool>? VhsGlitchChanged;
    public static event System.Action<bool>? PhosphorGridChanged;

    private const string SavePath        = "user://settings.cfg";
    private const string DisplaySavePath = "user://display.cfg";
    private const string SecAudio    = "audio";
    private const string SecDisp     = "display";
    private const string SecIdentity = "identity";
    private const string SecSurgeHinting = "surge_hinting";
    private const string SecProfileFlags = "profile_flags";
    private const string LegacyDevModeKey = "dev_mode";

    // ── Account / preference settings (cloud-synced) ─────────────────────
    public float MasterVolume  { get; private set; } = 80f;  // 0–100
    public float MusicVolume   { get; private set; } = 80f;
    public float FxVolume      { get; private set; } = 80f;
    public float UiFxVolume    { get; private set; } = 80f;
    public bool  AltMenuMusic  { get; private set; } = false;
    public bool  ColorblindMode { get; private set; } = false;
    public bool  ReducedMotion  { get; private set; } = false;
    // Hidden per-profile capability flag (not exposed in user-facing settings UI).
    public bool  DevMode        { get; private set; } = false;
    public DifficultyMode Difficulty { get; private set; } = DifficultyMode.Easy;
    public string PlayerName    { get; private set; } = "";
    public string PlayerId      { get; private set; } = "";
    public int    RunsStarted   { get; private set; } = 0;
    public bool   IsFirstRun    => RunsStarted <= 1;
    public bool   SurgeTutorialSeen     { get; private set; } = false;
    public bool   BuildNameTutorialSeen { get; private set; } = false;
    public bool   DemoCompleteNotified { get; private set; } = false;
    public bool   TutorialCompleted    { get; private set; } = false;
    public SurgeHintProfileState SurgeHintProfile { get; } = new();
    /// <summary>Transient flag - set by MainMenu before loading Main.tscn to request a tutorial run. Not persisted.</summary>
    public bool   PendingTutorialRun   { get; set; } = false;

    // ── Device-specific display settings (NOT cloud-synced) ──────────────
    public bool  Fullscreen    { get; private set; } = false;
    public bool  PostFxEnabled  { get; private set; } = true;
    public bool  LayeredEnemyRendering { get; private set; } = true;
    public bool  EnemyEmissiveLines { get; private set; } = true;
    public bool  EnemyDamageMaterial { get; private set; } = true;
    public bool  EnemyBloomHighlights { get; private set; } = !MobileOptimization.IsMobile();
    public bool  ScreenFilterEnabled  { get; private set; } = true;
    public bool  VhsGlitchEnabled     { get; private set; } = false;
    public bool  PhosphorGridEnabled  { get; private set; } = false;

    public override void _Ready()
    {
        Instance = this;
        EnsureBuses();
        Load();
        ApplyMaster(MasterVolume);
        ApplyMusic(MusicVolume);
        ApplyFx(FxVolume);
        ApplyUiFx(UiFxVolume);
        ApplyFullscreen(Fullscreen);

        if (string.IsNullOrEmpty(PlayerId))
        {
            PlayerId = System.Guid.NewGuid().ToString();
            SaveAccount();
        }
    }

    // ── Public API - account setters ─────────────────────────────────────

    public void SetVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyMaster(MasterVolume);
        SaveAccount();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyMusic(MusicVolume);
        SaveAccount();
    }

    public void SetFxVolume(float value)
    {
        FxVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyFx(FxVolume);
        SaveAccount();
    }

    public void SetUiFxVolume(float value)
    {
        UiFxVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyUiFx(UiFxVolume);
        SaveAccount();
    }

    public void SetAltMenuMusic(bool alt)
    {
        AltMenuMusic = alt;
        SoundManager.Instance?.SetMenuMusicStyle(alt);
        SaveAccount();
    }

    public void SetColorblindMode(bool enabled)
    {
        ColorblindMode = enabled;
        SaveAccount();
    }

    public void SetReducedMotion(bool enabled)
    {
        ReducedMotion = enabled;
        SaveAccount();
    }

    public void SetDevMode(bool enabled)
    {
        DevMode = enabled;
        SaveAccount();
    }

    public void SetPlayerName(string name)
    {
        PlayerName = name.Trim();
        SaveAccount();
    }

    public void SetDifficulty(DifficultyMode difficulty)
    {
        Difficulty = difficulty;
        SaveAccount();
    }

    public void IncrementRunsStarted()
    {
        RunsStarted++;
        SaveAccount();
    }

    public void MarkSurgeTutorialSeen()
    {
        if (SurgeTutorialSeen) return;
        SurgeTutorialSeen = true;
        SaveAccount();
    }

    public void MarkBuildNameTutorialSeen()
    {
        if (BuildNameTutorialSeen) return;
        BuildNameTutorialSeen = true;
        SaveAccount();
    }

    public void MarkTutorialCompleted()
    {
        if (TutorialCompleted) return;
        TutorialCompleted = true;
        SaveAccount();
    }

    public void SetDemoCompleteNotified() => DemoCompleteNotified = true;

    public void ResetTutorial()
    {
        RunsStarted = 0;
        SurgeTutorialSeen = false;
        BuildNameTutorialSeen = false;
        ResetSurgeHintingProgress();
    }

    public void ResetSurgeHintingProgress()
    {
        SurgeHintProfile.Reset();
        SaveAccount();
    }

    public void SaveSurgeHintingProgress() => SaveAccount();

    // ── Public API - display setters ─────────────────────────────────────

    public void SetFullscreen(bool full)
    {
        Fullscreen = full;
        ApplyFullscreen(Fullscreen);
        SaveDisplay();
    }

    public void ToggleFullscreen() => SetFullscreen(!Fullscreen);

    public void SetPostFxEnabled(bool enabled)
    {
        PostFxEnabled = enabled;
        Transition.Instance?.RefreshPostFxFromSettings();
        SaveDisplay();
    }

    public void SetLayeredEnemyRendering(bool enabled)
    {
        LayeredEnemyRendering = enabled;
        SaveDisplay();
    }

    public void SetEnemyEmissiveLines(bool enabled)
    {
        EnemyEmissiveLines = enabled;
        SaveDisplay();
    }

    public void SetEnemyDamageMaterial(bool enabled)
    {
        EnemyDamageMaterial = enabled;
        SaveDisplay();
    }

    public void SetEnemyBloomHighlights(bool enabled)
    {
        EnemyBloomHighlights = enabled;
        SaveDisplay();
    }

    public void SetScreenFilterEnabled(bool enabled)
    {
        ScreenFilterEnabled = enabled;
        ScreenFilterChanged?.Invoke(enabled);
        SaveDisplay();
    }

    public void SetVhsGlitchEnabled(bool enabled)
    {
        VhsGlitchEnabled = enabled;
        VhsGlitchChanged?.Invoke(enabled);
        SaveDisplay();
    }

    public void SetPhosphorGridEnabled(bool enabled)
    {
        PhosphorGridEnabled = enabled;
        PhosphorGridChanged?.Invoke(enabled);
        SaveDisplay();
    }

    // ── Apply ────────────────────────────────────────────────────────────

    private static void EnsureBuses()
    {
        EnsureBus("Music");
        EnsureBus("FX");
        EnsureBus("UI");
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) != -1) return;
        AudioServer.AddBus();
        int idx = AudioServer.BusCount - 1;
        AudioServer.SetBusName(idx, name);
        AudioServer.SetBusSend(idx, "Master");
    }

    private static void ApplyMaster(float value)
    {
        float db = value < 1f ? -80f : Mathf.LinearToDb(value / 100f);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
    }

    private static void ApplyMusic(float value)
    {
        int idx = AudioServer.GetBusIndex("Music");
        if (idx == -1) return;
        float db = value < 1f ? -80f : Mathf.LinearToDb(value / 100f);
        AudioServer.SetBusVolumeDb(idx, db);
    }

    private static void ApplyFx(float value)
    {
        int idx = AudioServer.GetBusIndex("FX");
        if (idx == -1) return;
        float db = value < 1f ? -80f : Mathf.LinearToDb(value / 100f);
        AudioServer.SetBusVolumeDb(idx, db);
    }

    private static void ApplyUiFx(float value)
    {
        int idx = AudioServer.GetBusIndex("UI");
        if (idx == -1) return;
        float db = value < 1f ? -80f : Mathf.LinearToDb(value / 100f);
        AudioServer.SetBusVolumeDb(idx, db);
    }

    private static void ApplyFullscreen(bool full)
    {
        DisplayServer.WindowSetMode(full
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    // ── Persist ──────────────────────────────────────────────────────────

    private void Load()
    {
        // --- Account settings (cloud-synced) ---
        SteamCloudSync.PullIfNewer(ProjectSettings.GlobalizePath(SavePath), "settings.cfg");
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) == Error.Ok)
        {
            MasterVolume   = (float)cfg.GetValue(SecAudio, "master_volume",    80f);
            MusicVolume    = (float)cfg.GetValue(SecAudio, "music_volume",    80f);
            FxVolume       = (float)cfg.GetValue(SecAudio, "fx_volume",       80f);
            UiFxVolume     = (float)cfg.GetValue(SecAudio, "ui_fx_volume",    80f);
            AltMenuMusic   = (bool) cfg.GetValue(SecAudio, "alt_menu_music", false);
            ColorblindMode = (bool) cfg.GetValue(SecDisp,  "colorblind",    false);
            ReducedMotion  = (bool) cfg.GetValue(SecDisp,  "reduced_motion", false);
            int rawDifficulty = (int)cfg.GetValue("gameplay", "difficulty", (int)DifficultyMode.Easy);
            Difficulty = rawDifficulty switch
            {
                (int)DifficultyMode.Easy   => DifficultyMode.Easy,
                (int)DifficultyMode.Normal => DifficultyMode.Normal,
                (int)DifficultyMode.Hard   => DifficultyMode.Hard,
                _ => DifficultyMode.Easy,
            };
            PlayerName   = (string)cfg.GetValue(SecIdentity, "player_name",    "");
            PlayerId     = (string)cfg.GetValue(SecIdentity, "player_id",      "");
            RunsStarted  = (int)   cfg.GetValue(SecIdentity, "runs_started",   0);
            SurgeTutorialSeen     = (bool)cfg.GetValue(SecIdentity, "surge_tutorial_seen",      false);
            BuildNameTutorialSeen = (bool)cfg.GetValue(SecIdentity, "build_name_tutorial_seen", false);
            TutorialCompleted     = (bool)cfg.GetValue(SecIdentity, "tutorial_completed",        false);
            LoadSurgeHintProfile(cfg);
            DevMode = ReadHiddenDevModeForProfile(cfg, PlayerId, out bool migratedFromLegacy);
            if (migratedFromLegacy)
                SaveAccount();
        }

        // --- Display settings (device-specific, not cloud-synced) ---
        // Migration: if display.cfg doesn't exist yet, fall back to reading display
        // keys from settings.cfg (one-time migration from the old combined file).
        var displayCfg = new ConfigFile();
        bool displayLoaded = displayCfg.Load(DisplaySavePath) == Error.Ok;
        if (!displayLoaded)
        {
            // Reuse already-attempted settings.cfg load as migration source.
            displayCfg = cfg;
        }

        Fullscreen = (bool)displayCfg.GetValue(SecDisp, "fullscreen", false);
        ScreenFilterEnabled = (bool)displayCfg.GetValue(SecDisp, "screen_filter",  true);
        PhosphorGridEnabled = (bool)displayCfg.GetValue(SecDisp, "phosphor_grid",  ScreenFilterEnabled);
        VhsGlitchEnabled    = (bool)displayCfg.GetValue(SecDisp, "vhs_glitch",     false);
        var renderSettings = EnemyRenderSettingsSnapshot.ReadFrom(displayCfg, defaultBloomEnabled: !MobileOptimization.IsMobile());
        PostFxEnabled         = renderSettings.PostFxEnabled;
        LayeredEnemyRendering = renderSettings.LayeredEnabled;
        EnemyEmissiveLines    = renderSettings.EmissiveEnabled;
        EnemyDamageMaterial   = renderSettings.DamageMaterialEnabled;
        EnemyBloomHighlights  = renderSettings.BloomEnabled;

        if (!displayLoaded)
            SaveDisplay(); // persist display.cfg immediately after migration
    }

    private void SaveAccount()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SecAudio, "master_volume",  MasterVolume);
        cfg.SetValue(SecAudio, "music_volume",   MusicVolume);
        cfg.SetValue(SecAudio, "fx_volume",      FxVolume);
        cfg.SetValue(SecAudio, "ui_fx_volume",   UiFxVolume);
        cfg.SetValue(SecAudio, "alt_menu_music", AltMenuMusic);
        cfg.SetValue(SecDisp,  "colorblind",     ColorblindMode);
        cfg.SetValue(SecDisp,  "reduced_motion", ReducedMotion);
        if (cfg.HasSectionKey(SecDisp, LegacyDevModeKey))
            cfg.EraseSectionKey(SecDisp, LegacyDevModeKey);
        cfg.SetValue(SecProfileFlags, BuildHiddenDevModeProfileKey(PlayerId), DevMode);
        cfg.SetValue("gameplay",   "difficulty",  (int)Difficulty);
        cfg.SetValue(SecIdentity, "player_name",  PlayerName);
        cfg.SetValue(SecIdentity, "player_id",    PlayerId);
        cfg.SetValue(SecIdentity, "runs_started", RunsStarted);
        cfg.SetValue(SecIdentity, "surge_tutorial_seen",      SurgeTutorialSeen);
        cfg.SetValue(SecIdentity, "build_name_tutorial_seen", BuildNameTutorialSeen);
        cfg.SetValue(SecIdentity, "tutorial_completed",        TutorialCompleted);
        SaveSurgeHintProfile(cfg);
        if (cfg.Save(SavePath) == Error.Ok)
            SteamCloudSync.Push(ProjectSettings.GlobalizePath(SavePath), "settings.cfg");
    }

    private void SaveDisplay()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SecDisp, "fullscreen", Fullscreen);
        cfg.SetValue(SecDisp, "screen_filter",   ScreenFilterEnabled);
        cfg.SetValue(SecDisp, "vhs_glitch",      VhsGlitchEnabled);
        cfg.SetValue(SecDisp, "phosphor_grid",   PhosphorGridEnabled);
        var renderSettings = new EnemyRenderSettingsSnapshot(
            postFxEnabled: PostFxEnabled,
            layeredEnabled: LayeredEnemyRendering,
            emissiveEnabled: EnemyEmissiveLines,
            damageMaterialEnabled: EnemyDamageMaterial,
            bloomEnabled: EnemyBloomHighlights);
        renderSettings.WriteTo(cfg);
        cfg.Save(DisplaySavePath);
    }

    private static bool ReadHiddenDevModeForProfile(ConfigFile cfg, string playerId, out bool migratedFromLegacy)
    {
        migratedFromLegacy = false;
        string profileKey = BuildHiddenDevModeProfileKey(playerId);
        if (cfg.HasSectionKey(SecProfileFlags, profileKey))
            return (bool)cfg.GetValue(SecProfileFlags, profileKey, false);

        // Legacy fallback for migration from old display setting.
        if (cfg.HasSectionKey(SecDisp, LegacyDevModeKey))
        {
            migratedFromLegacy = true;
            return (bool)cfg.GetValue(SecDisp, LegacyDevModeKey, false);
        }

        return false;
    }

    private static string BuildHiddenDevModeProfileKey(string playerId)
        => string.IsNullOrWhiteSpace(playerId) ? "dev_mode_default" : $"dev_mode_{playerId}";

    private void LoadSurgeHintProfile(ConfigFile cfg)
    {
        SurgeHintProfile.CombatFillsLifetimeShows = (int)cfg.GetValue(SecSurgeHinting, "combat_fills_shown", 0);
        SurgeHintProfile.TowerReadyLifetimeShows = (int)cfg.GetValue(SecSurgeHinting, "tower_ready_shown", 0);
        SurgeHintProfile.GlobalContributionLifetimeShows = (int)cfg.GetValue(SecSurgeHinting, "global_contrib_shown", 0);
        SurgeHintProfile.GlobalActivateLifetimeShows = (int)cfg.GetValue(SecSurgeHinting, "global_activate_shown", 0);
        SurgeHintProfile.ComboUnlockLifetimeShows = (int)cfg.GetValue(SecSurgeHinting, "combo_unlock_shown", 0);

        SurgeHintProfile.CombatFillsRetired = (bool)cfg.GetValue(SecSurgeHinting, "combat_fills_retired", false);
        SurgeHintProfile.TowerReadyRetired = (bool)cfg.GetValue(SecSurgeHinting, "tower_ready_retired", false);
        SurgeHintProfile.GlobalContributionRetired = (bool)cfg.GetValue(SecSurgeHinting, "global_contrib_retired", false);
        SurgeHintProfile.GlobalActivateRetired = (bool)cfg.GetValue(SecSurgeHinting, "global_activate_retired", false);
        SurgeHintProfile.ComboUnlockRetired = (bool)cfg.GetValue(SecSurgeHinting, "combo_unlock_retired", false);

        SurgeHintProfile.GlobalActivationsTotal = (int)cfg.GetValue(SecSurgeHinting, "global_activations_total", 0);
        SurgeHintProfile.GlobalActivationRuns = (int)cfg.GetValue(SecSurgeHinting, "global_activation_runs", 0);
        SurgeHintProfile.WinsWithGlobalActivation = (int)cfg.GetValue(SecSurgeHinting, "wins_with_global_activation", 0);
        SurgeHintProfile.ComboTowersBuiltTotal = (int)cfg.GetValue(SecSurgeHinting, "combo_towers_built_total", 0);
        SurgeHintProfile.QuickGlobalActivationsTotal = (int)cfg.GetValue(SecSurgeHinting, "quick_global_activations_total", 0);

        SurgeHintProfile.LastPostLossTipId = (string)cfg.GetValue(SecSurgeHinting, "last_post_loss_tip_id", "");
        SurgeHintProfile.LastPostLossTipRepeatCount = (int)cfg.GetValue(SecSurgeHinting, "last_post_loss_tip_repeat_count", 0);
    }

    private void SaveSurgeHintProfile(ConfigFile cfg)
    {
        cfg.SetValue(SecSurgeHinting, "combat_fills_shown", SurgeHintProfile.CombatFillsLifetimeShows);
        cfg.SetValue(SecSurgeHinting, "tower_ready_shown", SurgeHintProfile.TowerReadyLifetimeShows);
        cfg.SetValue(SecSurgeHinting, "global_contrib_shown", SurgeHintProfile.GlobalContributionLifetimeShows);
        cfg.SetValue(SecSurgeHinting, "global_activate_shown", SurgeHintProfile.GlobalActivateLifetimeShows);
        cfg.SetValue(SecSurgeHinting, "combo_unlock_shown", SurgeHintProfile.ComboUnlockLifetimeShows);

        cfg.SetValue(SecSurgeHinting, "combat_fills_retired", SurgeHintProfile.CombatFillsRetired);
        cfg.SetValue(SecSurgeHinting, "tower_ready_retired", SurgeHintProfile.TowerReadyRetired);
        cfg.SetValue(SecSurgeHinting, "global_contrib_retired", SurgeHintProfile.GlobalContributionRetired);
        cfg.SetValue(SecSurgeHinting, "global_activate_retired", SurgeHintProfile.GlobalActivateRetired);
        cfg.SetValue(SecSurgeHinting, "combo_unlock_retired", SurgeHintProfile.ComboUnlockRetired);

        cfg.SetValue(SecSurgeHinting, "global_activations_total", SurgeHintProfile.GlobalActivationsTotal);
        cfg.SetValue(SecSurgeHinting, "global_activation_runs", SurgeHintProfile.GlobalActivationRuns);
        cfg.SetValue(SecSurgeHinting, "wins_with_global_activation", SurgeHintProfile.WinsWithGlobalActivation);
        cfg.SetValue(SecSurgeHinting, "combo_towers_built_total", SurgeHintProfile.ComboTowersBuiltTotal);
        cfg.SetValue(SecSurgeHinting, "quick_global_activations_total", SurgeHintProfile.QuickGlobalActivationsTotal);

        cfg.SetValue(SecSurgeHinting, "last_post_loss_tip_id", SurgeHintProfile.LastPostLossTipId ?? "");
        cfg.SetValue(SecSurgeHinting, "last_post_loss_tip_repeat_count", SurgeHintProfile.LastPostLossTipRepeatCount);
    }
}
