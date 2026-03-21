using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SlotTheory.Data;

namespace SlotTheory.Core;

/// <summary>
/// Manages saving, loading, and deleting custom maps stored in the user data directory.
/// Each custom map is an individual JSON file under user://custom_maps/.
/// </summary>
public static class CustomMapManager
{
    private const string Dir = "user://custom_maps";

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Loads all custom maps from user://custom_maps/.</summary>
    public static List<MapDef> LoadAll()
    {
        var result = new List<MapDef>();

        var dir = DirAccess.Open(Dir);
        if (dir == null) return result;  // directory doesn't exist yet -- no maps

        dir.ListDirBegin();
        string fileName = dir.GetNext();
        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string path = $"{Dir}/{fileName}";
                try
                {
                    using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
                    if (file != null)
                    {
                        var def = JsonSerializer.Deserialize<MapDef>(file.GetAsText(), Opts);
                        if (def != null && !string.IsNullOrWhiteSpace(def.Id))
                        {
                            if (!def.IsCustom)
                                def = def with { IsCustom = true };
                            result.Add(def);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[CustomMapManager] Failed to load {path}: {ex.Message}");
                }
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>Saves a custom map to user://custom_maps/{id}.json.</summary>
    public static bool Save(MapDef def)
    {
        EnsureDir();
        var tagged = def.IsCustom ? def : def with { IsCustom = true };
        string path = $"{Dir}/{tagged.Id}.json";
        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PrintErr($"[CustomMapManager] Cannot open {path} for write: {FileAccess.GetOpenError()}");
                return false;
            }
            file.StoreString(JsonSerializer.Serialize(tagged, Opts));
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CustomMapManager] Failed to save {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Deletes user://custom_maps/{id}.json.</summary>
    public static bool Delete(string id)
    {
        string path = $"{Dir}/{id}.json";
        if (!FileAccess.FileExists(path)) return false;

        var err = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (err != Error.Ok)
        {
            GD.PrintErr($"[CustomMapManager] Delete failed for {path}: {err}");
            return false;
        }
        return true;
    }

    /// <summary>Generates a unique ID for a new custom map.</summary>
    public static string GenerateNewId()
        => $"custom_{System.Environment.TickCount64 & 0xFFFFFF:x}";

    /// <summary>
    /// Writes a MapDef as JSON to an arbitrary native filesystem path (for player sharing).
    /// Uses the same serialization format as Save() so the file can be re-imported.
    /// </summary>
    public static bool ExportToFile(MapDef def, string nativePath)
    {
        try
        {
            var tagged = def.IsCustom ? def : def with { IsCustom = true };
            System.IO.File.WriteAllText(nativePath, JsonSerializer.Serialize(tagged, Opts));
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CustomMapManager] Export failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reads and deserializes a MapDef from a native filesystem path.
    /// Returns null if the file is missing, unreadable, or not a valid map.
    /// </summary>
    public static MapDef? ImportFromFile(string nativePath)
    {
        try
        {
            var json = System.IO.File.ReadAllText(nativePath);
            var def  = JsonSerializer.Deserialize<MapDef>(json, Opts);
            if (def == null || string.IsNullOrWhiteSpace(def.Name)) return null;
            return def with { IsCustom = true };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[CustomMapManager] Import failed: {ex.Message}");
            return null;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void EnsureDir() => DirAccess.MakeDirRecursiveAbsolute(Dir);
}
