using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Loads, persists, and applies audio/display settings.
/// Creates Music and FX audio buses (children of Master) on first run.
/// </summary>
public partial class SettingsManager : Node
{
    public static SettingsManager? Instance { get; private set; }

    private const string SavePath = "user://settings.cfg";
    private const string SecAudio = "audio";
    private const string SecDisp  = "display";

    public float MasterVolume { get; private set; } = 80f;  // 0–100
    public float MusicVolume  { get; private set; } = 80f;
    public float FxVolume     { get; private set; } = 80f;
    public bool  Fullscreen   { get; private set; } = false;
    public DifficultyMode Difficulty { get; private set; } = DifficultyMode.Normal;

    public override void _Ready()
    {
        Instance = this;
        EnsureBuses();
        Load();
        ApplyMaster(MasterVolume);
        ApplyMusic(MusicVolume);
        ApplyFx(FxVolume);
        ApplyFullscreen(Fullscreen);
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void SetVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyMaster(MasterVolume);
        Save();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyMusic(MusicVolume);
        Save();
    }

    public void SetFxVolume(float value)
    {
        FxVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyFx(FxVolume);
        Save();
    }

    public void SetFullscreen(bool full)
    {
        Fullscreen = full;
        ApplyFullscreen(Fullscreen);
        Save();
    }

    public void ToggleFullscreen() => SetFullscreen(!Fullscreen);

    public void SetDifficulty(DifficultyMode difficulty)
    {
        Difficulty = difficulty;
        Save();
    }

    // ── Apply ────────────────────────────────────────────────────────────

    private static void EnsureBuses()
    {
        EnsureBus("Music");
        EnsureBus("FX");
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

    private static void ApplyFullscreen(bool full)
    {
        DisplayServer.WindowSetMode(full
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    // ── Persist ──────────────────────────────────────────────────────────

    private void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SavePath) != Error.Ok) return;

        MasterVolume = (float)cfg.GetValue(SecAudio, "master_volume", 80f);
        MusicVolume  = (float)cfg.GetValue(SecAudio, "music_volume",  80f);
        FxVolume     = (float)cfg.GetValue(SecAudio, "fx_volume",     80f);
        Fullscreen   = (bool) cfg.GetValue(SecDisp,  "fullscreen",    false);
        Difficulty   = (DifficultyMode)(int)cfg.GetValue("gameplay", "difficulty", (int)DifficultyMode.Normal);
    }

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SecAudio, "master_volume", MasterVolume);
        cfg.SetValue(SecAudio, "music_volume",  MusicVolume);
        cfg.SetValue(SecAudio, "fx_volume",     FxVolume);
        cfg.SetValue(SecDisp,  "fullscreen",    Fullscreen);
        cfg.SetValue("gameplay", "difficulty",   (int)Difficulty);
        cfg.Save(SavePath);
    }
}
