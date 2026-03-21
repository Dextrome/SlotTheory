namespace SlotTheory.UI;

/// <summary>
/// Static state shared between the map editor and a playtest run.
/// Lives outside the scene tree so it survives scene transitions.
/// </summary>
public static class MapEditorState
{
    /// <summary>True while the current game run was launched as a map editor playtest.</summary>
    public static bool IsPlaytesting { get; set; } = false;

    /// <summary>ID of the custom map currently being playtested.</summary>
    public static string? PlaytestMapId { get; set; } = null;

    /// <summary>ID of the map that was open in the editor (restored on return from playtest).</summary>
    public static string? LastEditedMapId { get; set; } = null;

    public static void ClearPlaytest()
    {
        IsPlaytesting = false;
        PlaytestMapId = null;
    }
}
