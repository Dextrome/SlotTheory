using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Loads, persists, and applies audio/display settings.
/// </summary>
public partial class SettingsManager : Node
{
    public static SettingsManager? Instance { get; private set; }

    private const string SavePath = "user://settings.cfg";
    private const string SecAudio = "audio";
    private const string SecDisp  = "display";

    // Exposed so UI can read current values without re-loading from disk
    public float MasterVolume { get; private set; } = 80f;  // 0–100
    public bool  Fullscreen   { get; private set; } = false;

    public override void _Ready()
    {
        Instance = this;
        Load();
        ApplyVolume(MasterVolume);
        ApplyFullscreen(Fullscreen);
    }

    // ── Public API ───────────────────────────────────────────────────────

    public void SetVolume(float value)
    {
        MasterVolume = Mathf.Clamp(value, 0f, 100f);
        ApplyVolume(MasterVolume);
        Save();
    }

    public void SetFullscreen(bool full)
    {
        Fullscreen = full;
        ApplyFullscreen(Fullscreen);
        Save();
    }

    public void ToggleFullscreen()
    {
        SetFullscreen(!Fullscreen);
    }

    // ── Apply ────────────────────────────────────────────────────────────

    private static void ApplyVolume(float value)
    {
        float db = value < 1f ? -80f : Mathf.LinearToDb(value / 100f);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
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
        Fullscreen   = (bool) cfg.GetValue(SecDisp,  "fullscreen",    false);
    }

    private void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(SecAudio, "master_volume", MasterVolume);
        cfg.SetValue(SecDisp,  "fullscreen",    Fullscreen);
        cfg.Save(SavePath);
    }
}
