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
    private static CompiledWaveAdjustment[] _waveAdjustments = System.Array.Empty<CompiledWaveAdjustment>();
    private static Dictionary<string, CompiledMapEnemyProfile> _mapEnemyProfiles = new(System.StringComparer.OrdinalIgnoreCase);

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
        LoadMapEnemyProfiles();
        LoadWaveAdjustments();
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

        WaveConfig composed;
        if (difficulty == DifficultyMode.Easy)
        {
            composed = baseWave;
        }
        else
        {
            // Apply difficulty multipliers for scaled modes (Normal/Hard).
            // Core enemy subtypes have dedicated multipliers; new package enemies currently
            // scale with the global enemy-count multiplier.
            float countMult = Balance.GetEnemyCountMultiplier(difficulty);
            composed = new WaveConfig(
                EnemyCount: Mathf.CeilToInt(baseWave.EnemyCount * countMult),
                SpawnInterval: baseWave.SpawnInterval * Balance.GetSpawnIntervalMultiplier(difficulty),
                TankyCount: Mathf.FloorToInt(baseWave.TankyCount * countMult * Balance.GetTankyCountMultiplier(difficulty)),
                ClumpArmored: baseWave.ClumpArmored,
                SwiftCount: Mathf.FloorToInt(baseWave.SwiftCount * countMult * Balance.GetSwiftCountMultiplier(difficulty)),
                SplitterCount: Mathf.FloorToInt(baseWave.SplitterCount * countMult * Balance.GetSplitterCountMultiplier(difficulty)),
                ReverseCount: Mathf.FloorToInt(baseWave.ReverseCount * countMult * Balance.GetReverseCountMultiplier(difficulty)),
                ShieldDroneCount: Mathf.FloorToInt(baseWave.ShieldDroneCount * countMult * Balance.GetShieldDroneCountMultiplier(difficulty)),
                AnchorCount: Mathf.FloorToInt(baseWave.AnchorCount * countMult),
                NullDroneCount: Mathf.FloorToInt(baseWave.NullDroneCount * countMult),
                LancerCount: Mathf.FloorToInt(baseWave.LancerCount * countMult),
                VeilCount: Mathf.FloorToInt(baseWave.VeilCount * countMult)
            );
        }

        composed = ApplyMapEnemyProfile(index, composed, mapId, difficulty);
        composed = ApplyMapDifficultyTuning(index, composed, mapId, difficulty);
        composed = ApplyDemoEnemySuppression(composed);
        return composed;
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

    /// <summary>
    /// Returns the ordered enemy IDs for a map's package: cores first, then spice.
    /// Basic Walker is omitted -- it is assumed present on every map and adds no signal.
    /// Returns an empty array for maps with no profile (e.g. random_map).
    /// </summary>
    public static string[] GetMapEnemyIds(string mapId)
    {
        if (!_mapEnemyProfiles.TryGetValue(mapId, out var profile))
            return System.Array.Empty<string>();

        var cores  = new System.Collections.Generic.List<string>();
        var spices = new System.Collections.Generic.List<string>();
        foreach (var e in profile.Enemies)
        {
            if (e.EnemyId == Core.EnemyCatalog.BasicWalkerId) continue;
            if (e.IsSpice) spices.Add(e.EnemyId);
            else           cores.Add(e.EnemyId);
        }
        cores.AddRange(spices);
        return cores.ToArray();
    }

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

    private static void LoadMapEnemyProfiles()
    {
        const string path = "res://Data/map_enemy_profiles.json";
        _mapEnemyProfiles.Clear();

        if (!FileAccess.FileExists(path))
            return;

        try
        {
            var root = Load<MapEnemyProfileFile>(path);
            if (root.Profiles == null || root.Profiles.Length == 0)
                return;

            var compiled = CompileMapEnemyProfiles(root.Profiles, logErrors: true);
            _mapEnemyProfiles = compiled.ToDictionary(p => p.MapId, p => p, System.StringComparer.OrdinalIgnoreCase);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapEnemyProfiles] Failed to load map_enemy_profiles.json: {ex.Message}");
            _mapEnemyProfiles.Clear();
        }
    }

    private static void LoadWaveAdjustments()
    {
        const string path = "res://Data/wave_adjustments.json";
        _waveAdjustments = System.Array.Empty<CompiledWaveAdjustment>();

        if (!FileAccess.FileExists(path))
            return;

        try
        {
            var root = Load<WaveAdjustmentFile>(path);
            if (root.Entries == null || root.Entries.Length == 0)
                return;

            var parsed = new List<CompiledWaveAdjustment>();
            for (int i = 0; i < root.Entries.Length; i++)
            {
                if (!TryCompileWaveAdjustment(root.Entries[i], i + 1, _waves.Length, logErrors: true, out var compiled))
                    continue;

                parsed.Add(compiled);
            }

            _waveAdjustments = parsed.ToArray();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[WaveAdjustments] Failed to load wave_adjustments.json: {ex.Message}");
            _waveAdjustments = System.Array.Empty<CompiledWaveAdjustment>();
        }
    }

    internal static WaveConfig ApplyWaveAdjustmentsForTesting(
        int waveIndex,
        WaveConfig wave,
        string? mapId,
        DifficultyMode difficulty,
        System.Collections.Generic.IEnumerable<WaveAdjustmentEntry> entries,
        int maxWaveCount = Balance.TotalWaves)
    {
        var compiled = new List<CompiledWaveAdjustment>();
        int row = 1;
        foreach (var entry in entries)
        {
            if (TryCompileWaveAdjustment(entry, row, maxWaveCount, logErrors: false, out var parsed))
                compiled.Add(parsed);
            row++;
        }

        return ApplyCompiledWaveAdjustments(waveIndex, wave, mapId, difficulty, compiled);
    }

    internal static WaveConfig ApplyMapEnemyProfileForTesting(
        int waveIndex,
        WaveConfig wave,
        string? mapId,
        DifficultyMode difficulty,
        System.Collections.Generic.IEnumerable<MapEnemyProfileEntry> profiles,
        int maxWaveCount = Balance.TotalWaves)
    {
        _ = difficulty;
        if (string.IsNullOrWhiteSpace(mapId) || mapId == "random_map")
            return wave;

        var compiled = CompileMapEnemyProfiles(
            profiles,
            logErrors: false,
            maxWaveCount: maxWaveCount,
            validateMapIds: false);
        for (int i = 0; i < compiled.Count; i++)
        {
            if (compiled[i].MapId.Equals(mapId, System.StringComparison.OrdinalIgnoreCase))
                return ApplyCompiledMapEnemyProfile(waveIndex, wave, compiled[i]);
        }

        return wave;
    }

    private static IReadOnlyList<CompiledMapEnemyProfile> CompileMapEnemyProfiles(
        System.Collections.Generic.IEnumerable<MapEnemyProfileEntry> profiles,
        bool logErrors,
        int maxWaveCount = Balance.TotalWaves,
        bool validateMapIds = true)
    {
        var supported = new HashSet<string>(EnemyCatalog.GetWaveSpecialEnemyIds(), System.StringComparer.Ordinal);
        var parsed = new Dictionary<string, CompiledMapEnemyProfile>(System.StringComparer.OrdinalIgnoreCase);
        int row = 0;
        foreach (var profile in profiles)
        {
            row++;
            if (string.IsNullOrWhiteSpace(profile.MapId))
            {
                if (logErrors)
                    GD.PrintErr($"[MapEnemyProfiles] Entry #{row} ignored: MapId is required.");
                continue;
            }

            string mapId = profile.MapId.Trim();
            if (mapId.Equals("random_map", System.StringComparison.OrdinalIgnoreCase))
            {
                if (logErrors)
                    GD.PrintErr($"[MapEnemyProfiles] Entry #{row} ignored: random_map does not use fixed enemy packages.");
                continue;
            }
            bool mapExists = _maps.ContainsKey(mapId)
                || _maps.Keys.Any(k => k.Equals(mapId, System.StringComparison.OrdinalIgnoreCase));
            if (validateMapIds && !mapExists)
            {
                if (logErrors)
                    GD.PrintErr($"[MapEnemyProfiles] Entry #{row} ignored: unknown map id '{mapId}'.");
                continue;
            }

            if (profile.Enemies == null || profile.Enemies.Length == 0)
            {
                if (logErrors)
                    GD.PrintErr($"[MapEnemyProfiles] Entry #{row} ignored: at least one enemy weight is required.");
                continue;
            }

            var compiledEnemies = new List<CompiledMapEnemyWeight>();
            foreach (var enemy in profile.Enemies)
            {
                if (string.IsNullOrWhiteSpace(enemy.EnemyId))
                    continue;

                string enemyId = enemy.EnemyId.Trim();
                if (!supported.Contains(enemyId))
                {
                    if (logErrors)
                        GD.PrintErr($"[MapEnemyProfiles] Entry #{row} ignored enemy '{enemyId}': unsupported enemy id.");
                    continue;
                }

                float weight = Mathf.Max(0f, enemy.Weight);
                if (weight <= 0f)
                    continue;

                int minWave = Mathf.Clamp(enemy.MinWave, 1, maxWaveCount) - 1;
                int maxWave = Mathf.Clamp(enemy.MaxWave, 1, maxWaveCount) - 1;
                if (maxWave < minWave)
                    (minWave, maxWave) = (maxWave, minWave);

                bool isSpice = enemy.Tier?.Trim().Equals("spice", System.StringComparison.OrdinalIgnoreCase) == true;
                compiledEnemies.Add(new CompiledMapEnemyWeight(enemyId, weight, isSpice, minWave, maxWave));
            }

            if (compiledEnemies.Count == 0)
                continue;

            var compiledBands = new List<CompiledMapEnemyBand>();
            foreach (var band in profile.Bands ?? System.Array.Empty<MapEnemyProfileBand>())
            {
                int from = Mathf.Clamp(band.FromWave, 1, maxWaveCount) - 1;
                int to = Mathf.Clamp(band.ToWave, 1, maxWaveCount) - 1;
                if (to < from)
                    (from, to) = (to, from);

                compiledBands.Add(new CompiledMapEnemyBand(
                    FromWaveIndex: from,
                    ToWaveIndex: to,
                    SpecialShare: band.SpecialShare,
                    PackageBlendMultiplier: Mathf.Max(0f, band.PackageBlendMultiplier),
                    CoreWeightMultiplier: Mathf.Max(0f, band.CoreWeightMultiplier),
                    SpiceWeightMultiplier: Mathf.Max(0f, band.SpiceWeightMultiplier)));
            }

            parsed[mapId] = new CompiledMapEnemyProfile(
                MapId: mapId,
                PackageBlend: Mathf.Clamp(profile.PackageBlend, 0f, 1f),
                OffProfileRetention: Mathf.Clamp(profile.OffProfileRetention, 0f, 1f),
                DefaultSpecialShare: profile.DefaultSpecialShare,
                Enemies: compiledEnemies.ToArray(),
                Bands: compiledBands.ToArray());
        }

        return parsed.Values.ToList();
    }

    private static bool TryCompileWaveAdjustment(
        WaveAdjustmentEntry entry,
        int row,
        int maxWaveCount,
        bool logErrors,
        out CompiledWaveAdjustment compiled)
    {
        compiled = default;

        if (string.IsNullOrWhiteSpace(entry.MapId))
        {
            if (logErrors)
                GD.PrintErr($"[WaveAdjustments] Entry #{row} ignored: MapId is required.");
            return false;
        }

        string mapFilter = entry.MapId.Trim();
        if (mapFilter != "*" && mapFilter.Equals("random_map", System.StringComparison.OrdinalIgnoreCase))
        {
            if (logErrors)
                GD.PrintErr($"[WaveAdjustments] Entry #{row} ignored: random_map is procedural and does not support fixed adjustments.");
            return false;
        }

        DifficultyMode? difficultyFilter = null;
        if (!string.IsNullOrWhiteSpace(entry.Difficulty))
        {
            string difficultyText = entry.Difficulty.Trim();
            if (difficultyText != "*")
            {
                if (!System.Enum.TryParse(difficultyText, ignoreCase: true, out DifficultyMode parsedDifficulty))
                {
                    if (logErrors)
                        GD.PrintErr($"[WaveAdjustments] Entry #{row} ignored: unknown difficulty '{entry.Difficulty}'.");
                    return false;
                }

                difficultyFilter = parsedDifficulty;
            }
        }

        int? waveIndexFilter = null;
        if (entry.Wave.HasValue)
        {
            int waveNumber = entry.Wave.Value;
            if (waveNumber < 1 || waveNumber > maxWaveCount)
            {
                if (logErrors)
                    GD.PrintErr($"[WaveAdjustments] Entry #{row} ignored: wave must be between 1 and {maxWaveCount}.");
                return false;
            }

            waveIndexFilter = waveNumber - 1;
        }

        bool hasAnyDelta =
            entry.EnemyCountDelta != 0 ||
            entry.TankyDelta != 0 ||
            entry.SwiftDelta != 0 ||
            entry.SplitterDelta != 0 ||
            entry.ReverseDelta != 0 ||
            entry.ShieldDroneDelta != 0 ||
            entry.AnchorDelta != 0 ||
            entry.NullDroneDelta != 0 ||
            entry.LancerDelta != 0 ||
            entry.VeilDelta != 0 ||
            !Mathf.IsZeroApprox(entry.SpawnIntervalDelta);

        if (!hasAnyDelta)
            return false;

        compiled = new CompiledWaveAdjustment(
            MapIdFilter: mapFilter == "*" ? null : mapFilter,
            DifficultyFilter: difficultyFilter,
            WaveIndexFilter: waveIndexFilter,
            EnemyCountDelta: entry.EnemyCountDelta,
            SpawnIntervalDelta: entry.SpawnIntervalDelta,
            TankyDelta: entry.TankyDelta,
            SwiftDelta: entry.SwiftDelta,
            SplitterDelta: entry.SplitterDelta,
            ReverseDelta: entry.ReverseDelta,
            ShieldDroneDelta: entry.ShieldDroneDelta,
            AnchorDelta: entry.AnchorDelta,
            NullDroneDelta: entry.NullDroneDelta,
            LancerDelta: entry.LancerDelta,
            VeilDelta: entry.VeilDelta
        );

        return true;
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

    private static WaveConfig ApplyMapEnemyProfile(int waveIndex, WaveConfig wave, string? mapId, DifficultyMode difficulty)
    {
        _ = difficulty;
        if (string.IsNullOrWhiteSpace(mapId) || mapId == "random_map" || mapId == "tutorial")
            return wave;
        if (!_mapEnemyProfiles.TryGetValue(mapId, out var profile))
            return wave;

        return ApplyCompiledMapEnemyProfile(waveIndex, wave, profile);
    }

    private static WaveConfig ApplyCompiledMapEnemyProfile(int waveIndex, WaveConfig wave, in CompiledMapEnemyProfile profile)
    {
        var specialIds = EnemyCatalog.GetWaveSpecialEnemyIds();
        var baseline = new Dictionary<string, int>(System.StringComparer.Ordinal)
        {
            [EnemyCatalog.ArmoredWalkerId] = wave.TankyCount,
            [EnemyCatalog.SwiftWalkerId] = wave.SwiftCount,
            [EnemyCatalog.SplitterWalkerId] = wave.SplitterCount,
            [EnemyCatalog.ReverseWalkerId] = wave.ReverseCount,
            [EnemyCatalog.ShieldDroneId] = wave.ShieldDroneCount,
            [EnemyCatalog.AnchorWalkerId] = wave.AnchorCount,
            [EnemyCatalog.NullDroneId] = wave.NullDroneCount,
            [EnemyCatalog.LancerWalkerId] = wave.LancerCount,
            [EnemyCatalog.VeilWalkerId] = wave.VeilCount,
        };

        int baselineSpecial = baseline.Values.Sum();
        int totalEnemies = wave.EnemyCount + baselineSpecial;
        if (totalEnemies <= 0)
            return wave;

        CompiledMapEnemyBand? activeBand = profile.ResolveBand(waveIndex);
        float requestedShare = activeBand?.SpecialShare ?? profile.DefaultSpecialShare;
        int targetSpecial = baselineSpecial;
        if (requestedShare >= 0f)
        {
            float clampedShare = Mathf.Clamp(requestedShare, 0f, 0.95f);
            targetSpecial = System.Math.Max(baselineSpecial, Mathf.RoundToInt(totalEnemies * clampedShare));
        }
        targetSpecial = Mathf.Clamp(targetSpecial, 0, totalEnemies);

        var activeWeights = new Dictionary<string, float>(System.StringComparer.Ordinal);
        foreach (var enemy in profile.Enemies)
        {
            if (!enemy.IsActiveOnWave(waveIndex))
                continue;

            float tierMult = enemy.IsSpice
                ? (activeBand?.SpiceWeightMultiplier ?? 1f)
                : (activeBand?.CoreWeightMultiplier ?? 1f);
            float weight = enemy.Weight * tierMult;
            if (weight <= 0f)
                continue;

            activeWeights[enemy.EnemyId] = activeWeights.TryGetValue(enemy.EnemyId, out float current)
                ? current + weight
                : weight;
        }

        if (activeWeights.Count == 0)
            return wave;

        var desired = AllocateCountsByWeight(activeWeights, targetSpecial);
        float packageBlend = Mathf.Clamp(profile.PackageBlend * (activeBand?.PackageBlendMultiplier ?? 1f), 0f, 1f);
        float offProfileRetention = profile.OffProfileRetention;

        var final = new Dictionary<string, int>(System.StringComparer.Ordinal);
        for (int i = 0; i < specialIds.Count; i++)
        {
            string enemyId = specialIds[i];
            bool inProfile = activeWeights.ContainsKey(enemyId);
            float retained = baseline[enemyId] * (inProfile ? 1f : offProfileRetention);
            desired.TryGetValue(enemyId, out int desiredCount);
            float blended = Mathf.Lerp(retained, desiredCount, packageBlend);
            final[enemyId] = Mathf.Max(0, Mathf.RoundToInt(blended));
        }

        NormalizeCountsToTarget(final, activeWeights, targetSpecial);

        int additionalSpecials = System.Math.Max(0, targetSpecial - baselineSpecial);
        int basicCount = Mathf.Max(0, wave.EnemyCount - additionalSpecials);
        int armored = final[EnemyCatalog.ArmoredWalkerId];
        bool clumpArmored = wave.ClumpArmored && armored >= 2;

        return new WaveConfig(
            EnemyCount: basicCount,
            SpawnInterval: wave.SpawnInterval,
            TankyCount: armored,
            ClumpArmored: clumpArmored,
            SwiftCount: final[EnemyCatalog.SwiftWalkerId],
            SplitterCount: final[EnemyCatalog.SplitterWalkerId],
            ReverseCount: final[EnemyCatalog.ReverseWalkerId],
            ShieldDroneCount: final[EnemyCatalog.ShieldDroneId],
            AnchorCount: final[EnemyCatalog.AnchorWalkerId],
            NullDroneCount: final[EnemyCatalog.NullDroneId],
            LancerCount: final[EnemyCatalog.LancerWalkerId],
            VeilCount: final[EnemyCatalog.VeilWalkerId]
        );
    }

    private static Dictionary<string, int> AllocateCountsByWeight(
        System.Collections.Generic.IReadOnlyDictionary<string, float> weights,
        int totalCount)
    {
        var result = new Dictionary<string, int>(System.StringComparer.Ordinal);
        if (totalCount <= 0 || weights.Count == 0)
            return result;

        float weightSum = 0f;
        foreach (var kv in weights)
            weightSum += Mathf.Max(0f, kv.Value);
        if (weightSum <= 0f)
            return result;

        var remainders = new List<(string EnemyId, float Remainder)>(weights.Count);
        int allocated = 0;
        foreach (var kv in weights.OrderBy(kv => kv.Key))
        {
            float normalized = Mathf.Max(0f, kv.Value) / weightSum;
            float raw = normalized * totalCount;
            int count = (int)System.MathF.Floor(raw);
            allocated += count;
            result[kv.Key] = count;
            remainders.Add((kv.Key, raw - count));
        }

        int remaining = totalCount - allocated;
        foreach (var entry in remainders
            .OrderByDescending(r => r.Remainder)
            .ThenBy(r => r.EnemyId)
            .Take(remaining))
        {
            result[entry.EnemyId] += 1;
        }

        return result;
    }

    private static void NormalizeCountsToTarget(
        Dictionary<string, int> counts,
        System.Collections.Generic.IReadOnlyDictionary<string, float> activeWeights,
        int targetTotal)
    {
        int current = counts.Values.Sum();
        if (current == targetTotal)
            return;

        string[] addOrder = counts.Keys
            .OrderByDescending(id => activeWeights.TryGetValue(id, out float weight) ? weight : 0f)
            .ThenBy(id => id, System.StringComparer.Ordinal)
            .ToArray();
        string[] removeOrder = counts.Keys
            .OrderBy(id => activeWeights.TryGetValue(id, out float weight) ? weight : 0f)
            .ThenBy(id => id, System.StringComparer.Ordinal)
            .ToArray();

        if (current < targetTotal)
        {
            if (addOrder.Length == 0)
                return;
            int cursor = 0;
            while (current < targetTotal)
            {
                string enemyId = addOrder[cursor % addOrder.Length];
                counts[enemyId] += 1;
                cursor++;
                current++;
            }
            return;
        }

        int guard = 0;
        while (current > targetTotal && guard < 4096)
        {
            bool changed = false;
            for (int i = 0; i < removeOrder.Length && current > targetTotal; i++)
            {
                string enemyId = removeOrder[i];
                if (counts[enemyId] <= 0)
                    continue;
                counts[enemyId] -= 1;
                current--;
                changed = true;
            }
            if (!changed)
                break;
            guard++;
        }
    }

    internal static WaveConfig ApplyDemoEnemySuppressionForTesting(WaveConfig wave, bool isDemo)
        => ApplyDemoEnemySuppression(wave, isDemo);

    private static WaveConfig ApplyDemoEnemySuppression(WaveConfig wave)
        => ApplyDemoEnemySuppression(wave, Balance.IsDemo);

    private static WaveConfig ApplyDemoEnemySuppression(WaveConfig wave, bool isDemo)
    {
        if (!isDemo)
            return wave;

        if (wave.ReverseCount == 0
            && wave.ShieldDroneCount == 0
            && wave.AnchorCount == 0
            && wave.NullDroneCount == 0
            && wave.LancerCount == 0
            && wave.VeilCount == 0)
            return wave;

        return wave with
        {
            ReverseCount = 0,
            ShieldDroneCount = 0,
            AnchorCount = 0,
            NullDroneCount = 0,
            LancerCount = 0,
            VeilCount = 0
        };
    }

    private static WaveConfig ApplyMapDifficultyTuning(int waveIndex, WaveConfig wave, string? mapId, DifficultyMode difficulty)
    {
        return ApplyCompiledWaveAdjustments(waveIndex, wave, mapId, difficulty, _waveAdjustments);
    }

    private static WaveConfig ApplyCompiledWaveAdjustments(
        int waveIndex,
        WaveConfig wave,
        string? mapId,
        DifficultyMode difficulty,
        System.Collections.Generic.IReadOnlyList<CompiledWaveAdjustment> adjustments)
    {
        if (string.IsNullOrWhiteSpace(mapId) || mapId == "random_map")
            return wave;

        int enemyCount = wave.EnemyCount;
        int tankyCount = wave.TankyCount;
        int swiftCount = wave.SwiftCount;
        int splitterCount = wave.SplitterCount;
        int reverseCount = wave.ReverseCount;
        int shieldDroneCount = wave.ShieldDroneCount;
        int anchorCount = wave.AnchorCount;
        int nullDroneCount = wave.NullDroneCount;
        int lancerCount = wave.LancerCount;
        int veilCount = wave.VeilCount;
        float spawnInterval = wave.SpawnInterval;

        for (int i = 0; i < adjustments.Count; i++)
        {
            var adjustment = adjustments[i];
            if (!adjustment.Matches(mapId!, difficulty, waveIndex))
                continue;

            enemyCount += adjustment.EnemyCountDelta;
            tankyCount += adjustment.TankyDelta;
            swiftCount += adjustment.SwiftDelta;
            splitterCount += adjustment.SplitterDelta;
            reverseCount += adjustment.ReverseDelta;
            shieldDroneCount += adjustment.ShieldDroneDelta;
            anchorCount += adjustment.AnchorDelta;
            nullDroneCount += adjustment.NullDroneDelta;
            lancerCount += adjustment.LancerDelta;
            veilCount += adjustment.VeilDelta;
            spawnInterval += adjustment.SpawnIntervalDelta;
        }

        enemyCount = Mathf.Max(0, enemyCount);
        tankyCount = Mathf.Max(0, tankyCount);
        swiftCount = Mathf.Max(0, swiftCount);
        splitterCount = Mathf.Max(0, splitterCount);
        reverseCount = Mathf.Max(0, reverseCount);
        shieldDroneCount = Mathf.Max(0, shieldDroneCount);
        anchorCount = Mathf.Max(0, anchorCount);
        nullDroneCount = Mathf.Max(0, nullDroneCount);
        lancerCount = Mathf.Max(0, lancerCount);
        veilCount = Mathf.Max(0, veilCount);
        spawnInterval = Mathf.Clamp(spawnInterval, 0.85f, 3.0f);

        return new WaveConfig(
            EnemyCount: enemyCount,
            SpawnInterval: spawnInterval,
            TankyCount: tankyCount,
            ClumpArmored: wave.ClumpArmored,
            SwiftCount: swiftCount,
            SplitterCount: splitterCount,
            ReverseCount: reverseCount,
            ShieldDroneCount: shieldDroneCount,
            AnchorCount: anchorCount,
            NullDroneCount: nullDroneCount,
            LancerCount: lancerCount,
            VeilCount: veilCount
        );
    }

    private readonly record struct CompiledMapEnemyWeight(
        string EnemyId,
        float Weight,
        bool IsSpice,
        int MinWaveIndex,
        int MaxWaveIndex)
    {
        public bool IsActiveOnWave(int waveIndex)
            => waveIndex >= MinWaveIndex && waveIndex <= MaxWaveIndex;
    }

    private readonly record struct CompiledMapEnemyBand(
        int FromWaveIndex,
        int ToWaveIndex,
        float SpecialShare,
        float PackageBlendMultiplier,
        float CoreWeightMultiplier,
        float SpiceWeightMultiplier)
    {
        public bool ContainsWave(int waveIndex)
            => waveIndex >= FromWaveIndex && waveIndex <= ToWaveIndex;
    }

    private readonly record struct CompiledMapEnemyProfile(
        string MapId,
        float PackageBlend,
        float OffProfileRetention,
        float DefaultSpecialShare,
        CompiledMapEnemyWeight[] Enemies,
        CompiledMapEnemyBand[] Bands)
    {
        public CompiledMapEnemyBand? ResolveBand(int waveIndex)
        {
            for (int i = 0; i < Bands.Length; i++)
            {
                if (Bands[i].ContainsWave(waveIndex))
                    return Bands[i];
            }
            return null;
        }
    }

    private readonly record struct CompiledWaveAdjustment(
        string? MapIdFilter,
        DifficultyMode? DifficultyFilter,
        int? WaveIndexFilter,
        int EnemyCountDelta,
        float SpawnIntervalDelta,
        int TankyDelta,
        int SwiftDelta,
        int SplitterDelta,
        int ReverseDelta,
        int ShieldDroneDelta,
        int AnchorDelta,
        int NullDroneDelta,
        int LancerDelta,
        int VeilDelta)
    {
        public bool Matches(string mapId, DifficultyMode difficulty, int waveIndex)
        {
            if (MapIdFilter != null && !MapIdFilter.Equals(mapId, System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (DifficultyFilter.HasValue && DifficultyFilter.Value != difficulty)
                return false;
            if (WaveIndexFilter.HasValue && WaveIndexFilter.Value != waveIndex)
                return false;
            return true;
        }
    }
}
