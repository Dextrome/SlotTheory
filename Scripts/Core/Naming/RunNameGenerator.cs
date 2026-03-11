using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.Core.Naming;

public static class RunNameGenerator
{
    private const string HistoryPath = "user://run_name_history.cfg";
    private const string HistorySection = "recent";
    private const int HistoryLimit = 20;

    private static bool _historyLoaded;
    private static readonly List<string> _recentNames = new();
    private static readonly HashSet<string> _recentNamesSet = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] DefaultFamilyWords =
    [
        "Neon", "Pulse", "Vector", "Grid", "Flux", "Signal", "Prime", "Drift", "Core", "Static",
        "Lumen", "Glyph"
    ];

    private static readonly Dictionary<string, string[]> FamilyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DamageScaling"] =
        [
            "Overclocked", "Overdrive", "Rupture", "Crush", "Breaker", "Shredder", "Anvil", "Rampage",
            "Spike", "Hammerfall", "Wildfire", "Pressure", "Savage", "Overkill", "Brutal", "Critical"
        ],
        ["Utility"] =
        [
            "Cryo", "Stasis", "Frostline", "Control", "Snare", "Chill", "Anchor", "Latch",
            "Lockstep", "Brake", "Icelock", "Trapper", "Holdfast", "Tether", "Glacier", "Coldfront"
        ],
        ["Range"] =
        [
            "Longshot", "Farreach", "Horizon", "Skylance", "Overreach", "Sightline", "Outrider", "Linelock",
            "Perimeter", "Truesight", "Outpost", "Deepfield", "Railline", "Distance", "Watchline", "Skylink"
        ],
        ["StatusSynergy"] =
        [
            "Marked", "Hex", "Expose", "Punisher", "Sting", "Catalyst", "Tracer", "Signal",
            "Exploit", "Weakpoint", "Brand", "Tagline", "Needle", "Trigger", "Tracerlock", "Vector"
        ],
        ["MultiTarget"] =
        [
            "Cascade", "Chain", "Arcstorm", "Forked", "Splitline", "Scatter", "Storm", "Volley",
            "Ripchain", "Webline", "Scattergrid", "Branch", "Shatter", "Ripcurrent", "Splice", "Lattice"
        ],
        ["Other"] =
        [
            "Neon", "Pulse", "Vector", "Flux", "Signal", "Core", "Variant", "Prime", "Grid", "Current"
        ],
    };

    private static readonly string[] DefaultTowerNouns =
    [
        "Rig", "Engine", "Array", "Matrix", "Protocol", "Pattern", "Frame", "Loop", "Directive", "System"
    ];

    private static readonly Dictionary<string, string[]> TowerNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rapid_shooter"] =
        [
            "Needler", "Ripper", "Burstline", "Stitcher", "Injector", "Stinger", "Talon", "Pulsegun",
            "Rainshot", "Harrier", "Skewer", "Gateline"
        ],
        ["heavy_cannon"] =
        [
            "Cannon", "Driver", "Siegebreaker", "Mortar", "Hammer", "Slugger", "Railmaw", "Anvil",
            "Breaker", "Crusher", "Bastion", "Bombard"
        ],
        ["marker_tower"] =
        [
            "Beacon", "Painter", "Tracer", "Signaler", "Tagger", "Locator", "Brander", "Targeter",
            "Guidelight", "Pointer", "Lockline", "Spotter"
        ],
        ["chain_tower"] =
        [
            "Coil", "Emitter", "Arcgrid", "Stormline", "Forknode", "Conduit", "Relay", "Surge",
            "Tesla", "Lattice", "Sparkweb", "Arcforge"
        ],
        ["rift_prism"] =
        [
            "Sapper", "Minegrid", "Trapline", "Riftcharge", "Detonator", "Demolisher", "Saboteur", "Fuseweb",
            "Chokemine", "Burrowline", "Blastnet", "Voidmine"
        ],
    };

    private static readonly Dictionary<string, string[]> FamilySuffixWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DamageScaling"] = ["Ruin", "Impact", "Pressure", "Breach", "Overload", "Rend", "Crack", "Break"],
        ["Utility"] = ["Hold", "Grip", "Freeze", "Stall", "Latch", "Control", "Lock", "Tether"],
        ["Range"] = ["Sight", "Reach", "Horizon", "Outfield", "Line", "Scan", "Watch", "Sweep"],
        ["StatusSynergy"] = ["Mark", "Hex", "Trigger", "Tag", "Expose", "Weakpoint", "Signal", "Brand"],
        ["MultiTarget"] = ["Fork", "Arc", "Scatter", "Chain", "Cascade", "Branch", "Volley", "Split"],
        ["Other"] = ["Flux", "Signal", "Pulse", "Vector", "Grid", "Frame", "Pattern", "Prime"],
    };

    private static readonly Dictionary<string, string[]> MapWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arena_classic"] = ["Crossroad", "Conflux", "Junction", "Interlock", "Midlane", "Switchback", "X-Line", "Merge"],
        ["gauntlet"] = ["Gauntlet", "Pinch", "Bleedline", "Choke", "S-Curve", "Clamp", "Bottleneck", "Vice"],
        ["sprawl"] = ["Orbital", "Spiral", "Ringline", "Perihelion", "Slingshot", "Helix", "Orbit", "Outring"],
        ["random_map"] = ["Anomaly", "Chaosline", "Wildgrid", "Fracture", "Glitch", "Unstable", "Unknown", "Rift"],
    };

    private static readonly string[] DefaultMapWords = ["Frontline", "Lattice", "Route", "Circuit", "Grid", "Track", "Path"];
    private static readonly string[] HardWords = ["Iron", "Savage", "Ruthless", "Apex", "Night", "Elite", "Dire", "Relentless"];
    private static readonly string[] NormalWords = ["Tempered", "Focused", "Adaptive", "Sharpened", "Steadfast", "Tactical", "Forward", "Charged"];
    private static readonly string[] EasyWords = ["Prime", "Clean", "Steady", "Balanced", "Classic", "True", "Core", "Mainline"];
    private static readonly string[] BlazingWords = ["Blitz", "Flash", "Rapid", "Breakneck", "Snap"];
    private static readonly string[] FastWords = ["Quick", "Swift", "Surge", "Dash", "Rush"];
    private static readonly string[] SteadyWords = ["Steady", "Measured", "Rhythm", "Flow", "Balanced"];
    private static readonly string[] DeliberateWords = ["Methodical", "Heavy", "Deliberate", "Slowburn", "Grind"];
    private static readonly string[] MarathonWords = ["Endurance", "Marathon", "Siege", "Longhaul", "Attrition"];
    private static readonly string[] MonoWords = ["Solo", "Pure", "Single", "Monoline", "Lone"];
    private static readonly string[] DuoWords = ["Twin", "Dual", "Pair", "Splitcore", "Hybrid"];
    private static readonly string[] MixedWords = ["Mixed", "Composite", "Spectrum", "Patchwork", "Mosaic"];
    private static readonly string[] TailWords = ["Protocol", "Pattern", "Engine", "Directive", "Loop", "Frame", "System", "Doctrine", "Scheme"];
    private static readonly string[] CollisionSuffixes = ["Prime", "Mk II", "Redux", "Variant", "Sigma", "Omega", "Plus", "Delta"];

    public static RunNameProfile AnalyzeProfile(
        RunState runState,
        DifficultyMode difficulty,
        string mapId,
        bool won,
        int waveReached)
    {
        string resolvedMap = string.IsNullOrEmpty(mapId) ? LeaderboardKey.RandomMapId : mapId;
        int cappedWaveReached = Math.Clamp(waveReached, 0, Balance.TotalWaves);

        var towerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var familyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int towersPlaced = 0;

        for (int i = 0; i < runState.Slots.Length; i++)
        {
            var tower = runState.Slots[i].Tower;
            if (tower == null) continue;

            towersPlaced++;
            towerCounts.TryGetValue(tower.TowerId, out int towerCount);
            towerCounts[tower.TowerId] = towerCount + 1;

            foreach (var modifier in tower.Modifiers)
            {
                string family = ModifierFamily(modifier.ModifierId);
                familyCounts.TryGetValue(family, out int count);
                familyCounts[family] = count + 1;
            }
        }

        if (familyCounts.Count == 0)
        {
            string inferredFamily = InferFamilyFromTowers(towerCounts);
            familyCounts[inferredFamily] = 1;
        }

        var sortedFamilies = familyCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToArray();

        string primaryFamily = sortedFamilies[0].Key;
        string secondaryFamily = sortedFamilies.Length > 1 ? sortedFamilies[1].Key : primaryFamily;
        int primaryFamilyCount = sortedFamilies[0].Value;
        int secondaryFamilyCount = sortedFamilies.Length > 1 ? sortedFamilies[1].Value : 0;

        (string mvpTowerId, string supportTowerId) = ResolveTowerIdentity(runState, towerCounts);
        int uniqueTowerTypes = towerCounts.Count;

        float secondsPerWave = cappedWaveReached > 0
            ? runState.TotalPlayTime / cappedWaveReached
            : runState.TotalPlayTime;

        return new RunNameProfile(
            resolvedMap,
            difficulty,
            won,
            cappedWaveReached,
            runState.Lives,
            primaryFamily,
            secondaryFamily,
            primaryFamilyCount,
            secondaryFamilyCount,
            mvpTowerId,
            supportTowerId,
            uniqueTowerTypes,
            towersPlaced,
            runState.TotalKills,
            runState.TotalDamageDealt,
            ResolvePace(secondsPerWave)
        );
    }

    public static string GenerateName(RunNameProfile profile, int runSeed, bool registerInHistory = false)
    {
        int baseSeed = BuildBaseSeed(profile, runSeed);
        EnsureHistoryLoaded();

        string? firstCandidate = null;
        for (int attempt = 0; attempt < 14; attempt++)
        {
            string candidate = BuildCandidate(profile, baseSeed, attempt);
            firstCandidate ??= candidate;
            if (!_recentNamesSet.Contains(candidate))
            {
                if (registerInHistory)
                    AddRecentName(candidate);
                return candidate;
            }
        }

        string fallback = BuildCollisionFallback(firstCandidate ?? BuildCandidate(profile, baseSeed, 0), baseSeed);
        if (registerInHistory)
            AddRecentName(fallback);
        return fallback;
    }

    public static string GenerateFromSnapshot(
        string mapId,
        DifficultyMode difficulty,
        int score,
        int waveReached,
        int livesRemaining,
        int totalKills,
        int totalDamage,
        int timeSeconds,
        RunBuildSnapshot build)
    {
        var styled = GenerateStyledFromSnapshot(
            mapId,
            difficulty,
            score,
            waveReached,
            livesRemaining,
            totalKills,
            totalDamage,
            timeSeconds,
            build);
        return styled.Name;
    }

    public static (string Name, Color StartColor, Color EndColor) GenerateStyledFromSnapshot(
        string mapId,
        DifficultyMode difficulty,
        int score,
        int waveReached,
        int livesRemaining,
        int totalKills,
        int totalDamage,
        int timeSeconds,
        RunBuildSnapshot build)
    {
        bool won = waveReached >= Balance.TotalWaves;
        var profile = AnalyzeProfileFromSnapshot(
            mapId,
            difficulty,
            won,
            waveReached,
            livesRemaining,
            totalKills,
            totalDamage,
            timeSeconds,
            build);

        int syntheticSeed = StableHash(string.Join("|",
            mapId,
            (int)difficulty,
            score,
            waveReached,
            livesRemaining,
            totalKills,
            totalDamage,
            timeSeconds));

        string name = GenerateName(profile, syntheticSeed, registerInHistory: false);
        var colors = ResolveNameColors(profile);
        return (name, colors.start, colors.end);
    }

    public static string ModifierFamily(string modifierId) => modifierId switch
    {
        "momentum" or "overkill" or "focus_lens" or "hair_trigger" or "feedback_loop" => "DamageScaling",
        "slow" => "Utility",
        "overreach" => "Range",
        "exploit_weakness" => "StatusSynergy",
        "split_shot" or "chain_reaction" => "MultiTarget",
        _ => "Other",
    };

    public static (Color start, Color end) ResolveNameColors(RunNameProfile profile)
    {
        var familyColor = profile.PrimaryFamily switch
        {
            "DamageScaling" => new Color(1.00f, 0.60f, 0.20f),
            "Utility" => new Color(0.45f, 0.92f, 1.00f),
            "Range" => new Color(0.72f, 0.58f, 1.00f),
            "StatusSynergy" => new Color(1.00f, 0.36f, 0.80f),
            "MultiTarget" => new Color(0.48f, 1.00f, 0.76f),
            _ => new Color(0.78f, 0.88f, 1.00f),
        };

        var towerColor = profile.MvpTowerId switch
        {
            "rapid_shooter" => new Color(0.25f, 0.92f, 1.00f),
            "heavy_cannon" => new Color(1.00f, 0.60f, 0.18f),
            "marker_tower" => new Color(1.00f, 0.30f, 0.72f),
            "chain_tower" => new Color(0.62f, 0.90f, 1.00f),
            "rift_prism" => new Color(0.62f, 1.00f, 0.58f),
            _ => new Color(0.84f, 0.92f, 1.00f),
        };

        return (EnsureBrightColor(familyColor), EnsureBrightColor(towerColor));
    }

    private static (string mvpTowerId, string supportTowerId) ResolveTowerIdentity(
        RunState runState,
        IReadOnlyDictionary<string, int> towerCounts)
    {
        var slotDamage = new Dictionary<int, int>();
        foreach (var stats in EnumerateTowerStats(runState))
        {
            if (stats.SlotIndex < 0) continue;
            slotDamage.TryGetValue(stats.SlotIndex, out int current);
            slotDamage[stats.SlotIndex] = current + stats.Damage;
        }

        var orderedSlots = slotDamage
            .Where(kv => kv.Key >= 0 && kv.Key < runState.Slots.Length)
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => kv.Key)
            .ToList();

        int mvpSlot = orderedSlots.FirstOrDefault(-1);
        int supportSlot = orderedSlots.Skip(1).FirstOrDefault(-1);

        if (mvpSlot < 0 || runState.Slots[mvpSlot].Tower == null)
        {
            mvpSlot = FirstPlacedTowerSlot(runState);
        }
        if (supportSlot < 0 || runState.Slots[supportSlot].Tower == null || supportSlot == mvpSlot)
        {
            supportSlot = NextPlacedTowerSlot(runState, mvpSlot);
        }

        string mvpTower = mvpSlot >= 0 ? runState.Slots[mvpSlot].Tower?.TowerId ?? "" : "";
        string supportTower = supportSlot >= 0 ? runState.Slots[supportSlot].Tower?.TowerId ?? "" : "";

        if (string.IsNullOrEmpty(mvpTower) && towerCounts.Count > 0)
            mvpTower = towerCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First().Key;

        if (string.IsNullOrEmpty(supportTower))
            supportTower = mvpTower;

        return (mvpTower, supportTower);
    }

    private static IEnumerable<TowerWaveStats> EnumerateTowerStats(RunState runState)
    {
        foreach (var wave in runState.CompletedWaves)
        {
            foreach (var stat in wave.TowerStats)
                yield return stat;
        }

        if (runState.CurrentWave.WaveNumber > runState.CompletedWaves.Count)
        {
            foreach (var stat in runState.CurrentWave.TowerStats)
                yield return stat;
        }
    }

    private static int FirstPlacedTowerSlot(RunState runState)
    {
        for (int i = 0; i < runState.Slots.Length; i++)
        {
            if (runState.Slots[i].Tower != null)
                return i;
        }
        return -1;
    }

    private static int NextPlacedTowerSlot(RunState runState, int skipSlot)
    {
        for (int i = 0; i < runState.Slots.Length; i++)
        {
            if (i == skipSlot) continue;
            if (runState.Slots[i].Tower != null)
                return i;
        }
        return -1;
    }

    private static string InferFamilyFromTowers(IReadOnlyDictionary<string, int> towerCounts)
    {
        if (towerCounts.Count == 0)
            return "DamageScaling";

        string dominantTower = towerCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .First().Key;

        return dominantTower switch
        {
            "chain_tower" => "MultiTarget",
            "rift_prism" => "Utility",
            "marker_tower" => "StatusSynergy",
            "rapid_shooter" => "DamageScaling",
            "heavy_cannon" => "DamageScaling",
            _ => "DamageScaling",
        };
    }

    private static RunNameProfile AnalyzeProfileFromSnapshot(
        string mapId,
        DifficultyMode difficulty,
        bool won,
        int waveReached,
        int livesRemaining,
        int totalKills,
        int totalDamage,
        int timeSeconds,
        RunBuildSnapshot build)
    {
        string resolvedMap = string.IsNullOrEmpty(mapId) ? LeaderboardKey.RandomMapId : mapId;
        int cappedWave = Math.Clamp(waveReached, 0, Balance.TotalWaves);

        var towerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var familyCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int placedTowers = 0;
        var slots = build?.Slots ?? Array.Empty<RunSlotBuild>();

        foreach (var slot in slots)
        {
            if (slot == null || string.IsNullOrEmpty(slot.TowerId))
                continue;

            placedTowers++;
            towerCounts.TryGetValue(slot.TowerId, out int tCount);
            towerCounts[slot.TowerId] = tCount + 1;

            var mods = slot.ModifierIds ?? Array.Empty<string>();
            foreach (string mod in mods)
            {
                if (string.IsNullOrEmpty(mod))
                    continue;
                string family = ModifierFamily(mod);
                familyCounts.TryGetValue(family, out int fCount);
                familyCounts[family] = fCount + 1;
            }
        }

        if (familyCounts.Count == 0)
        {
            string inferred = InferFamilyFromTowers(towerCounts);
            familyCounts[inferred] = 1;
        }

        var sortedFamilies = familyCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToArray();
        string primaryFamily = sortedFamilies[0].Key;
        string secondaryFamily = sortedFamilies.Length > 1 ? sortedFamilies[1].Key : primaryFamily;
        int primaryCount = sortedFamilies[0].Value;
        int secondaryCount = sortedFamilies.Length > 1 ? sortedFamilies[1].Value : 0;

        var orderedTowers = towerCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .ToArray();
        string mvpTower = orderedTowers.Length > 0 ? orderedTowers[0] : "";
        string supportTower = orderedTowers.Length > 1 ? orderedTowers[1] : mvpTower;

        float secondsPerWave = cappedWave > 0
            ? timeSeconds / (float)cappedWave
            : timeSeconds;

        return new RunNameProfile(
            resolvedMap,
            difficulty,
            won,
            cappedWave,
            Math.Max(0, livesRemaining),
            primaryFamily,
            secondaryFamily,
            primaryCount,
            secondaryCount,
            mvpTower,
            supportTower,
            towerCounts.Count,
            placedTowers,
            Math.Max(0, totalKills),
            Math.Max(0, totalDamage),
            ResolvePace(secondsPerWave)
        );
    }

    private static RunPaceBucket ResolvePace(float secondsPerWave)
    {
        if (secondsPerWave <= 50f) return RunPaceBucket.Blazing;
        if (secondsPerWave <= 70f) return RunPaceBucket.Fast;
        if (secondsPerWave <= 95f) return RunPaceBucket.Steady;
        if (secondsPerWave <= 125f) return RunPaceBucket.Deliberate;
        return RunPaceBucket.Marathon;
    }

    private static string BuildCandidate(RunNameProfile profile, int baseSeed, int attempt)
    {
        int seed = Mix(baseSeed, 997 * (attempt + 1));

        string familyWord = Pick(GetFamilyWords(profile.PrimaryFamily), seed, 11);
        string towerWord = Pick(GetTowerWords(profile.MvpTowerId), seed, 17);
        string mapWord = Pick(GetMapWords(profile.MapId), seed, 23);
        string paceWord = Pick(GetPaceWords(profile.Pace), seed, 31);
        string diffWord = Pick(GetDifficultyWords(profile.Difficulty), seed, 37);
        string secondaryWord = Pick(GetFamilySuffixWords(profile.SecondaryFamily), seed, 41);
        string tailWord = Pick(TailWords, seed, 47);
        string compositionWord = Pick(GetCompositionWords(profile.UniqueTowerTypes), seed, 53);

        int template = PositiveMod(Mix(seed, 61), 12);
        string name = template switch
        {
            0 => $"{familyWord} {towerWord}",
            1 => $"{mapWord} {familyWord} {towerWord}",
            2 => $"{familyWord} {towerWord} {tailWord}",
            3 => $"{familyWord} {towerWord} of {secondaryWord}",
            4 => $"{diffWord} {familyWord} {towerWord}",
            5 => $"{paceWord} {towerWord} {tailWord}",
            6 => $"{compositionWord} {familyWord} {towerWord}",
            7 => $"{mapWord} {towerWord} {tailWord}",
            8 => $"{diffWord} {mapWord} {towerWord}",
            9 => $"{familyWord} {secondaryWord} {towerWord}",
            10 => profile.Won
                ? $"{familyWord} {towerWord} Triumph"
                : $"{familyWord} {towerWord} Last Stand",
            11 => profile.LivesRemaining <= 2
                ? $"{diffWord} {towerWord} Clutch"
                : $"{paceWord} {familyWord} {towerWord}",
            _ => $"{familyWord} {towerWord}",
        };

        return NormalizeName(name);
    }

    private static string BuildCollisionFallback(string baseName, int baseSeed)
    {
        for (int i = 0; i < CollisionSuffixes.Length; i++)
        {
            string suffix = CollisionSuffixes[PositiveMod(Mix(baseSeed, 131 + i * 19), CollisionSuffixes.Length)];
            string candidate = NormalizeName($"{baseName} {suffix}");
            if (!_recentNamesSet.Contains(candidate))
                return candidate;
        }

        int code = PositiveMod(baseSeed, 89) + 11;
        return NormalizeName($"{baseName} {code}");
    }

    private static Color EnsureBrightColor(Color c)
    {
        float max = Mathf.Max(c.R, Mathf.Max(c.G, c.B));
        if (max < 0.78f)
        {
            float scale = 0.78f / Mathf.Max(0.001f, max);
            c = new Color(c.R * scale, c.G * scale, c.B * scale, 1f);
        }
        return new Color(Mathf.Clamp(c.R, 0f, 1f), Mathf.Clamp(c.G, 0f, 1f), Mathf.Clamp(c.B, 0f, 1f), 1f);
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Neon Protocol";

        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts);
    }

    private static string[] GetFamilyWords(string family)
        => FamilyWords.TryGetValue(family, out var words) ? words : DefaultFamilyWords;

    private static string[] GetTowerWords(string towerId)
        => TowerNouns.TryGetValue(towerId ?? "", out var words) ? words : DefaultTowerNouns;

    private static string[] GetFamilySuffixWords(string family)
        => FamilySuffixWords.TryGetValue(family, out var words) ? words : FamilySuffixWords["Other"];

    private static string[] GetMapWords(string mapId)
        => MapWords.TryGetValue(mapId ?? "", out var words) ? words : DefaultMapWords;

    private static string[] GetDifficultyWords(DifficultyMode difficulty)
        => difficulty switch
        {
            DifficultyMode.Easy => EasyWords,
            DifficultyMode.Normal => NormalWords,
            DifficultyMode.Hard => HardWords,
            _ => EasyWords,
        };

    private static string[] GetPaceWords(RunPaceBucket pace) => pace switch
    {
        RunPaceBucket.Blazing => BlazingWords,
        RunPaceBucket.Fast => FastWords,
        RunPaceBucket.Steady => SteadyWords,
        RunPaceBucket.Deliberate => DeliberateWords,
        _ => MarathonWords,
    };

    private static string[] GetCompositionWords(int uniqueTowerTypes)
    {
        if (uniqueTowerTypes <= 1) return MonoWords;
        if (uniqueTowerTypes == 2) return DuoWords;
        return MixedWords;
    }

    private static string Pick(string[] pool, int seed, int salt)
    {
        if (pool.Length == 0) return "Neon";
        int idx = PositiveMod(Mix(seed, salt), pool.Length);
        return pool[idx];
    }

    private static int BuildBaseSeed(RunNameProfile profile, int runSeed)
    {
        string fingerprint =
            $"{profile.MapId}|{(int)profile.Difficulty}|{profile.Won}|{profile.WaveReached}|{profile.LivesRemaining}|{profile.PrimaryFamily}|{profile.SecondaryFamily}|{profile.MvpTowerId}|{profile.SupportTowerId}|{profile.UniqueTowerTypes}|{profile.TotalTowersPlaced}|{profile.TotalKills}|{profile.TotalDamage}|{(int)profile.Pace}|{runSeed}";
        return StableHash(fingerprint);
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7fffffff);
        }
    }

    private static int Mix(int seed, int salt)
    {
        unchecked
        {
            uint x = (uint)(seed + 0x9E3779B9u + (uint)(salt * 0x85EBCA6Bu));
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return (int)(x & 0x7fffffff);
        }
    }

    private static int PositiveMod(int value, int modulus)
    {
        if (modulus <= 0) return 0;
        int r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    private static void EnsureHistoryLoaded()
    {
        if (_historyLoaded) return;
        _historyLoaded = true;

        _recentNames.Clear();
        _recentNamesSet.Clear();

        var cfg = new ConfigFile();
        var err = cfg.Load(HistoryPath);
        if (err != Error.Ok && err != Error.FileNotFound)
        {
            GD.PrintErr($"[RunName] Failed to load history {HistoryPath}: {err}");
            return;
        }

        int count = (int)cfg.GetValue(HistorySection, "count", 0);
        for (int i = 0; i < count; i++)
        {
            string name = ((string)cfg.GetValue(HistorySection, $"name_{i}", "")).Trim();
            if (name.Length == 0) continue;
            if (_recentNamesSet.Add(name))
                _recentNames.Add(name);
        }
    }

    private static void AddRecentName(string name)
    {
        EnsureHistoryLoaded();
        if (name.Length == 0) return;

        int existingIndex = _recentNames.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
            _recentNames.RemoveAt(existingIndex);

        _recentNames.Insert(0, name);
        _recentNamesSet.Clear();
        for (int i = 0; i < _recentNames.Count; i++)
        {
            if (_recentNamesSet.Contains(_recentNames[i]))
            {
                _recentNames.RemoveAt(i);
                i--;
            }
            else
            {
                _recentNamesSet.Add(_recentNames[i]);
            }
        }

        if (_recentNames.Count > HistoryLimit)
            _recentNames.RemoveRange(HistoryLimit, _recentNames.Count - HistoryLimit);
        _recentNamesSet.Clear();
        foreach (string recent in _recentNames)
            _recentNamesSet.Add(recent);

        SaveHistory();
    }

    private static void SaveHistory()
    {
        var cfg = new ConfigFile();
        cfg.SetValue(HistorySection, "count", _recentNames.Count);
        for (int i = 0; i < _recentNames.Count; i++)
            cfg.SetValue(HistorySection, $"name_{i}", _recentNames[i]);

        var err = cfg.Save(HistoryPath);
        if (err != Error.Ok)
            GD.PrintErr($"[RunName] Failed to save history {HistoryPath}: {err}");
    }
}
