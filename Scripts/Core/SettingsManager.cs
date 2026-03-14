using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Loads, persists, and applies audio/display settings.
/// Creates Music and FX audio buses (children of Master) on first run.
///
/// Two save files:
///   user://settings.cfg  — account/preference settings; cloud-synced via SteamCloudSync
///   user://display.cfg   — device-specific display/render settings; NOT cloud-synced
/// </summary>
public partial class SettingsManager : Node
{
    public static SettingsManager? Instance { get; private set; }

    private const string SavePath        = "user://settings.cfg";
    private const string DisplaySavePath = "user://display.cfg";
    private const string SecAudio    = "audio";
    private const string SecDisp     = "display";
    private const string SecIdentity = "identity";
    private const string SecProfileFlags = "profile_flags";
    private const string LegacyDevModeKey = "dev_mode";

    // ── Account / preference settings (cloud-synced) ─────────────────────
    public float MasterVolume  { get; private set; } = 80f;  // 0–100
    public float MusicVolume   { get; private set; } = 80f;
    public float FxVolume      { get; private set; } = 80f;
    public float UiFxVolume    { get; private set; } = 80f;
    public bool  ColorblindMode { get; private set; } = false;
    public bool  ReducedMotion  { get; private set; } = false;
    // Hidden per-profile capability flag (not exposed in user-facing settings UI).
    public bool  DevMode        { get; private set; } = false;
    public DifficultyMode Difficulty { get; private set; } = DifficultyMode.Easy;
    public string PlayerName    { get; private set; } = "";
    public string PlayerId      { get; private set; } = "";

    // ── Device-specific display settings (NOT cloud-synced) ──────────────
    public bool  Fullscreen    { get; private set; } = false;
    public bool  PostFxEnabled  { get; private set; } = true;
    public bool  LayeredEnemyRendering { get; private set; } = true;
    public bool  EnemyEmissiveLines { get; private set; } = true;
    public bool  EnemyDamageMaterial { get; private set; } = true;
    public bool  EnemyBloomHighlights { get; private set; } = !MobileOptimization.IsMobile();

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

    // ── Public API — account setters ─────────────────────────────────────

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

    // ── Public API — display setters ─────────────────────────────────────

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
            PlayerName = (string)cfg.GetValue(SecIdentity, "player_name", "");
            PlayerId   = (string)cfg.GetValue(SecIdentity, "player_id",   "");
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
        cfg.SetValue(SecDisp,  "colorblind",     ColorblindMode);
        cfg.SetValue(SecDisp,  "reduced_motion", ReducedMotion);
        if (cfg.HasSectionKey(SecDisp, LegacyDevModeKey))
            cfg.EraseSectionKey(SecDisp, LegacyDevModeKey);
        cfg.SetValue(SecProfileFlags, BuildHiddenDevModeProfileKey(PlayerId), DevMode);
        cfg.SetValue("gameplay",   "difficulty",  (int)Difficulty);
        cfg.SetValue(SecIdentity, "player_name", PlayerName);
        cfg.SetValue(SecIdentity, "player_id",   PlayerId);
        if (cfg.Save(SavePath) == Error.Ok)
            SteamCloudSync.Push(ProjectSettings.GlobalizePath(SavePath), "settings.cfg");
    }

    private void SaveDisplay()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SecDisp, "fullscreen", Fullscreen);
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
}
