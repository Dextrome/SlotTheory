using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Persists which campaign stages have been cleared and on which difficulty.
///
/// Save format: user://campaign_progress.cfg
///   [cleared]
///   stage_0_easy   = true
///   stage_1_normal = true
///   ...
/// </summary>
public static class CampaignProgress
{
    private const string SavePath = "user://campaign_progress.cfg";
    private const string Section  = "cleared";

    private static ConfigFile _cfg = new();

    public static void Load()
    {
        _cfg = new ConfigFile();
        var err = _cfg.Load(SavePath);
        if (err != Error.Ok && err != Error.FileNotFound)
            GD.PrintErr($"[Campaign] Failed to load {SavePath}: {err}");
    }

    public static void MarkCleared(int stageIndex, DifficultyMode difficulty)
    {
        _cfg.SetValue(Section, Key(stageIndex, difficulty), true);
        Save();
        GD.Print($"[Campaign] Stage {stageIndex} cleared on {difficulty}.");
    }

    public static bool IsCleared(int stageIndex, DifficultyMode difficulty)
        => _cfg.HasSectionKey(Section, Key(stageIndex, difficulty))
           && (bool)_cfg.GetValue(Section, Key(stageIndex, difficulty), false);

    public static bool IsClearedOnAny(int stageIndex)
        => IsCleared(stageIndex, DifficultyMode.Easy)
           || IsCleared(stageIndex, DifficultyMode.Normal)
           || IsCleared(stageIndex, DifficultyMode.Hard);

    /// <summary>
    /// Stage N is available if it is stage 0, or if stage N-1 has been cleared on any difficulty.
    /// </summary>
    public static bool IsAvailable(int stageIndex)
        => stageIndex == 0 || IsClearedOnAny(stageIndex - 1);

    private static string Key(int stageIndex, DifficultyMode difficulty)
        => $"stage_{stageIndex}_{difficulty.ToString().ToLower()}";

    private static void Save()
    {
        var err = _cfg.Save(SavePath);
        if (err != Error.Ok)
            GD.PrintErr($"[Campaign] Failed to save {SavePath}: {err}");
    }
}
