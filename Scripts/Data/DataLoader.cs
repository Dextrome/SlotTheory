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
    private static List<CampaignStageDef> _campaignStages = new();
    // Custom maps are stored separately so they survive repeated LoadAll() calls.
    private static readonly Dictionary<string, MapDef> _customMaps = new();
    private static string _campaignFinalCompletionText = "";

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
        LoadCampaignStages();
        // Re-merge custom maps so GetMapDef works for playtests after a scene reload.
        foreach (var kv in _customMaps)
            _maps[kv.Key] = kv.Value;

        // Validate modifier descriptions match implementation (debug check)
        Tools.ModifierDataValidator.ValidateModifierData(_modifiers);
    }

    /// <summary>
    /// Registers custom maps so they can be retrieved by GetMapDef.
    /// Called by CustomMapManager after load/save. Survives repeated LoadAll() calls
    /// because _customMaps is a separate persistent dictionary.
    /// </summary>
    public static void RegisterCustomMaps(System.Collections.Generic.IEnumerable<MapDef> defs)
    {
        foreach (var def in defs)
        {
            var tagged = def.IsCustom ? def : def with { IsCustom = true };
            _customMaps[tagged.Id] = tagged;
            _maps[tagged.Id] = tagged;
        }
    }

    /// <summary>Registers a single custom map (e.g. after a save).</summary>
    public static void RegisterCustomMap(MapDef def)
    {
        var tagged = def.IsCustom ? def : def with { IsCustom = true };
        _customMaps[tagged.Id] = tagged;
        _maps[tagged.Id] = tagged;
    }

    /// <summary>Removes a custom map from the runtime registry (e.g. after delete).</summary>
    public static void UnregisterCustomMap(string id)
    {
        _customMaps.Remove(id);
        _maps.Remove(id);
    }

    /// <summary>Returns all custom maps in alphabetical order by name.</summary>
    public static System.Collections.Generic.IEnumerable<MapDef> GetCustomMapDefs()
        => _customMaps.Values.OrderBy(m => m.Name);

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
        // Tutorial map has its own inline wave table - bypass global waves and difficulty scaling.
        if (mapId == "tutorial" && _maps.TryGetValue("tutorial", out var tutDef) && tutDef.TutorialWaves != null)
        {
            int clampedIndex = System.Math.Min(index, tutDef.TutorialWaves.Length - 1);
            return tutDef.TutorialWaves[clampedIndex];
        }

        var baseWave = _waves[index];
        if (Balance.IsDemo && baseWave.ReverseCount > 0)
        {
            // Reverse Walker is a full-game enemy. Demo builds parse the same data file,
            // but force this lane type to zero so composition stays demo-safe.
            baseWave = baseWave with { ReverseCount = 0 };
        }
        if (Balance.IsDemo && baseWave.ShieldDroneCount > 0)
        {
            // Shield Drone is a full-game enemy. Zero it out in demo builds.
            baseWave = baseWave with { ShieldDroneCount = 0 };
        }
        if (difficulty == DifficultyMode.Easy)
            return ApplyMapDifficultyTuning(index, baseWave, mapId, difficulty);

        // Apply difficulty multipliers for scaled modes (Normal/Hard).
        // TankyCount/SwiftCount/SplitterCount/ReverseCount get independent multipliers
        // so the tuning pipeline can adjust enemy composition (armored/swift/splitter density)
        // separately from basic walker volume.
        var scaled = new WaveConfig(
            EnemyCount: Mathf.CeilToInt(baseWave.EnemyCount * Balance.GetEnemyCountMultiplier(difficulty)),
            SpawnInterval: baseWave.SpawnInterval * Balance.GetSpawnIntervalMultiplier(difficulty),
            TankyCount: Mathf.FloorToInt(baseWave.TankyCount * Balance.GetEnemyCountMultiplier(difficulty) * Balance.GetTankyCountMultiplier(difficulty)),
            ClumpArmored: baseWave.ClumpArmored,
            SwiftCount: Mathf.FloorToInt(baseWave.SwiftCount * Balance.GetEnemyCountMultiplier(difficulty) * Balance.GetSwiftCountMultiplier(difficulty)),
            SplitterCount: Mathf.FloorToInt(baseWave.SplitterCount * Balance.GetEnemyCountMultiplier(difficulty) * Balance.GetSplitterCountMultiplier(difficulty)),
            ReverseCount: Mathf.FloorToInt(baseWave.ReverseCount * Balance.GetEnemyCountMultiplier(difficulty) * Balance.GetReverseCountMultiplier(difficulty)),
            ShieldDroneCount: Mathf.FloorToInt(baseWave.ShieldDroneCount * Balance.GetEnemyCountMultiplier(difficulty) * Balance.GetShieldDroneCountMultiplier(difficulty))
        );

        return ApplyMapDifficultyTuning(index, scaled, mapId, difficulty);
    }

    public static MapDef GetMapDef(string id) => _maps[id];
    public static IReadOnlyList<CampaignStageDef> GetCampaignStages() => _campaignStages;
    public static string GetFinalCompletionText() => _campaignFinalCompletionText;
    public static IEnumerable<string> GetAllTowerIds(bool includeLocked = false)
        => includeLocked ? _towers.Keys : _towers.Keys.Where(Core.Unlocks.IsTowerUnlocked);
    public static IEnumerable<string> GetAllModifierIds(bool includeLocked = false)
        => includeLocked ? _modifiers.Keys : _modifiers.Keys.Where(Core.Unlocks.IsModifierUnlocked);
    public static IEnumerable<MapDef> GetAllMapDefs(bool includeTutorial = false)
        => _maps.Values.Where(m => (includeTutorial || !m.IsTutorial) && !m.IsCustom).OrderBy(m => m.DisplayOrder);

    private static void LoadMaps()
    {
        // Load maps.json and extract the "maps" array, "tutorial" object, and "random" object
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

        // Load tutorial map
        if (root.TryGetProperty("tutorial", out var tutorialElem))
        {
            var tutorialDef = JsonSerializer.Deserialize<MapDef>(tutorialElem.GetRawText(), _opts);
            if (tutorialDef != null)
                _maps[tutorialDef.Id] = tutorialDef;
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

    private static void LoadCampaignStages()
    {
        try
        {
            var root = Load<CampaignDataRoot>("res://Data/campaign_stages.json");
            _campaignStages = new List<CampaignStageDef>(root.Stages ?? System.Array.Empty<CampaignStageDef>());
            _campaignFinalCompletionText = root.FinalCompletionText ?? "";
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Campaign] Failed to load campaign_stages.json: {ex.Message}");
            _campaignStages = new List<CampaignStageDef>();
            _campaignFinalCompletionText = "";
        }
    }

    private static WaveConfig ApplyMapDifficultyTuning(int waveIndex, WaveConfig wave, string? mapId, DifficultyMode difficulty)
    {
        if (string.IsNullOrWhiteSpace(mapId) || mapId == "random_map")
            return wave;

        int enemyCount = wave.EnemyCount;
        int tankyCount = wave.TankyCount;
        int swiftCount = wave.SwiftCount;
        int reverseCount = wave.ReverseCount;
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
            SwiftCount: swiftCount,
            SplitterCount: wave.SplitterCount,
            ReverseCount: reverseCount,
            ShieldDroneCount: wave.ShieldDroneCount
        );
    }
}
