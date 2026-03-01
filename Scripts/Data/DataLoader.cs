using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace SlotTheory.Data;

public static class DataLoader
{
    private static Dictionary<string, TowerDef> _towers = new();
    private static Dictionary<string, ModifierDef> _modifiers = new();
    private static WaveConfig[] _waves = System.Array.Empty<WaveConfig>();

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void LoadAll()
    {
        _towers    = Load<Dictionary<string, TowerDef>>("res://Data/towers.json");
        _modifiers = Load<Dictionary<string, ModifierDef>>("res://Data/modifiers.json");
        _waves     = Load<WaveConfig[]>("res://Data/waves.json");
    }

    public static TowerDef GetTowerDef(string id) => _towers[id];
    public static ModifierDef GetModifierDef(string id) => _modifiers[id];
    public static WaveConfig GetWaveConfig(int index) => _waves[index];
    public static IEnumerable<string> GetAllTowerIds() => _towers.Keys;
    public static IEnumerable<string> GetAllModifierIds() => _modifiers.Keys;

    private static T Load<T>(string resPath)
    {
        string path = ProjectSettings.GlobalizePath(resPath);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _opts)!;
    }
}
