using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SlotTheory.Core;

namespace SlotTheory.Data;

public static class DataLoader
{
    private static Dictionary<string, TowerDef> _towers = new();
    private static Dictionary<string, ModifierDef> _modifiers = new();
    private static WaveConfig[] _waves = System.Array.Empty<WaveConfig>();
    private static Dictionary<string, MapDef> _maps = new();

    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void LoadAll()
    {
        _towers = Load<Dictionary<string, TowerDef>>("res://Data/towers.json");
        _modifiers = Load<Dictionary<string, ModifierDef>>("res://Data/modifiers.json");
        _waves = Load<WaveConfig[]>("res://Data/waves.json");
        LoadMaps();

        // Validate modifier descriptions match implementation (debug check)
        Tools.ModifierDataValidator.ValidateModifierData(_modifiers);
    }

    public static TowerDef GetTowerDef(string id) => _towers[id];
    public static ModifierDef GetModifierDef(string id) => _modifiers[id];

    public static WaveConfig GetWaveConfig(int index)
        => GetWaveConfig(
            index,
            Core.SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
            null);

    public static WaveConfig GetWaveConfig(int index, DifficultyMode difficulty)
        => GetWaveConfig(index, difficulty, null);

    public static WaveConfig GetWaveConfig(int index, DifficultyMode difficulty, string? mapId)
    {
        var baseWave = _waves[index];
        if (difficulty == DifficultyMode.Easy)
            return ApplyMapDifficultyTuning(index, baseWave, mapId, difficulty);

        // Apply difficulty multipliers for scaled modes (Normal/Hard)
        var scaled = new WaveConfig(
            EnemyCount: Mathf.CeilToInt(baseWave.EnemyCount * Balance.GetEnemyCountMultiplier(difficulty)),
            SpawnInterval: baseWave.SpawnInterval * Balance.GetSpawnIntervalMultiplier(difficulty),
            TankyCount: Mathf.CeilToInt(baseWave.TankyCount * Balance.GetEnemyCountMultiplier(difficulty)),
            ClumpArmored: baseWave.ClumpArmored,
            SwiftCount: Mathf.CeilToInt(baseWave.SwiftCount * Balance.GetEnemyCountMultiplier(difficulty))
        );

        return ApplyMapDifficultyTuning(index, scaled, mapId, difficulty);
    }

    public static MapDef GetMapDef(string id) => _maps[id];
    public static IEnumerable<string> GetAllTowerIds()
        => _towers.Keys.Where(Core.Unlocks.IsTowerUnlocked);
    public static IEnumerable<string> GetAllModifierIds() => _modifiers.Keys;
    public static IEnumerable<MapDef> GetAllMapDefs() => _maps.Values.OrderBy(m => m.DisplayOrder);

    private static void LoadMaps()
    {
        // Load maps.json and extract the "maps" array and "random" object
        using var file = FileAccess.Open("res://Data/maps.json", FileAccess.ModeFlags.Read);
        if (file == null)
            throw new System.Exception(
                $"DataLoader: cannot open 'res://Data/maps.json' - {FileAccess.GetOpenError()}");

        string json = file.GetAsText();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        _maps.Clear();

        // Load hand-crafted maps
        if (root.TryGetProperty("maps", out var mapsArray))
        {
            foreach (var mapElem in mapsArray.EnumerateArray())
            {
                var mapDef = JsonSerializer.Deserialize<MapDef>(mapElem.GetRawText(), _opts);
                if (mapDef != null)
                    _maps[mapDef.Id] = mapDef;
            }
        }

        // Load random/procedural map
        if (root.TryGetProperty("random", out var randomElem))
        {
            var randomDef = JsonSerializer.Deserialize<MapDef>(randomElem.GetRawText(), _opts);
            if (randomDef != null)
                _maps[randomDef.Id] = randomDef;
        }
    }

    private static T Load<T>(string resPath)
    {
        using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new System.Exception(
                $"DataLoader: cannot open '{resPath}' - {FileAccess.GetOpenError()}");

        string json = file.GetAsText();
        return JsonSerializer.Deserialize<T>(json, _opts)!;
    }

    private static WaveConfig ApplyMapDifficultyTuning(int waveIndex, WaveConfig wave, string? mapId, DifficultyMode difficulty)
    {
        if (string.IsNullOrWhiteSpace(mapId) || mapId == "random_map")
            return wave;

        int enemyCount = wave.EnemyCount;
        int tankyCount = wave.TankyCount;
        int swiftCount = wave.SwiftCount;
        float spawnInterval = wave.SpawnInterval;

        // 0-based wave indices: 17=wave18, 18=wave19, 19=wave20.
        // Map-specific tuning can be added here once calibrated.
        _ = waveIndex;
        _ = mapId;
        _ = difficulty;

        spawnInterval = Mathf.Clamp(spawnInterval, 0.85f, 3.0f);
        return new WaveConfig(
            EnemyCount: enemyCount,
            SpawnInterval: spawnInterval,
            TankyCount: tankyCount,
            ClumpArmored: wave.ClumpArmored,
            SwiftCount: swiftCount
        );
    }
}
