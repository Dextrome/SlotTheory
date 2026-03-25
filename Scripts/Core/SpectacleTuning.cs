using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlotTheory.Core;

/// <summary>
/// Runtime tuning overrides used by automation, sweeps, and combat lab scenarios.
/// Defaults are used when no best_tuning JSON file is present. Production values are
/// loaded from Data/best_tuning_full.json or best_tuning_demo.json at startup.
/// All multipliers are applied on top of Balance.cs constants - Reset() returns to hardcoded defaults.
/// </summary>
public sealed class SpectacleTuningProfile
{
    [JsonPropertyName("enable_overkill_bloom")]
    public bool EnableOverkillBloom { get; set; } = true;
    [JsonPropertyName("enable_status_detonation")]
    public bool EnableStatusDetonation { get; set; } = true;
    [JsonPropertyName("enable_residue")]
    public bool EnableResidue { get; set; } = true;

    [JsonPropertyName("overkill_bloom_damage_scale_multiplier")]
    public float OverkillBloomDamageScaleMultiplier { get; set; } = 0.7532f;
    [JsonPropertyName("overkill_bloom_radius_multiplier")]
    public float OverkillBloomRadiusMultiplier { get; set; } = 0.9387f;

    [JsonPropertyName("detonation_max_targets_multiplier")]
    public float DetonationMaxTargetsMultiplier { get; set; } = 1.0799f;
    [JsonPropertyName("status_detonation_damage_multiplier")]
    public float StatusDetonationDamageMultiplier { get; set; } = 1.2506f;

    [JsonPropertyName("residue_damage_multiplier")]
    public float ResidueDamageMultiplier { get; set; } = 0.9283f;

    [JsonPropertyName("meter_gain_multiplier")]
    public float MeterGainMultiplier { get; set; } = 1f;
    [JsonPropertyName("surge_threshold_multiplier")]
    public float SurgeThresholdMultiplier { get; set; } = 1.0561f;
    [JsonPropertyName("surge_cooldown_multiplier")]
    public float SurgeCooldownMultiplier { get; set; } = 0.9831f;
    [JsonPropertyName("surge_meter_after_trigger_multiplier")]
    public float SurgeMeterAfterTriggerMultiplier { get; set; } = 1.0825f;
    [JsonPropertyName("global_meter_per_surge_multiplier")]
    public float GlobalMeterPerSurgeMultiplier { get; set; } = 1.0778f;
    [JsonPropertyName("global_threshold_multiplier")]
    public float GlobalThresholdMultiplier { get; set; } = 1.1049f;
    [JsonPropertyName("global_meter_after_trigger_multiplier")]
    public float GlobalMeterAfterTriggerMultiplier { get; set; } = 0.9987f;
    [JsonPropertyName("inactivity_grace_multiplier")]
    public float InactivityGraceMultiplier { get; set; } = 0.9151f;
    [JsonPropertyName("inactivity_decay_multiplier")]
    public float InactivityDecayMultiplier { get; set; } = 1.2156f;
    [JsonPropertyName("copy_multiplier_scale")]
    public float CopyMultiplierScale { get; set; } = 0.8532f;
    // Easy mode base difficulty (was hardcoded 1.0x; now tunable so optimizer can hit ~90% win target).
    [JsonPropertyName("easy_enemy_hp_multiplier")]
    public float EasyEnemyHpMultiplier { get; set; } = 1f;
    [JsonPropertyName("easy_enemy_count_multiplier")]
    public float EasyEnemyCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("easy_spawn_interval_multiplier")]
    public float EasySpawnIntervalMultiplier { get; set; } = 1f;

    // Per-difficulty multiplier on the HpGrowthPerWave exponent base.
    // Scales the 1.10^waveIndex curve - the dominant factor in late-wave (14-20) HP spikes.
    // Values below 1.0 flatten the curve (less HP growth per wave = easier late game).
    [JsonPropertyName("easy_hp_growth_multiplier")]
    public float EasyHpGrowthMultiplier { get; set; } = 1f;
    [JsonPropertyName("normal_hp_growth_multiplier")]
    public float NormalHpGrowthMultiplier { get; set; } = 1f;
    [JsonPropertyName("hard_hp_growth_multiplier")]
    public float HardHpGrowthMultiplier { get; set; } = 1f;

    [JsonPropertyName("normal_enemy_hp_multiplier")]
    public float NormalEnemyHpMultiplier { get; set; } = 1f;
    [JsonPropertyName("normal_enemy_count_multiplier")]
    public float NormalEnemyCountMultiplier { get; set; } = 1.136f;
    [JsonPropertyName("normal_spawn_interval_multiplier")]
    public float NormalSpawnIntervalMultiplier { get; set; } = 0.898f;
    [JsonPropertyName("hard_enemy_hp_multiplier")]
    public float HardEnemyHpMultiplier { get; set; } = 1.1887f;
    [JsonPropertyName("hard_enemy_count_multiplier")]
    public float HardEnemyCountMultiplier { get; set; } = 1.1835f;
    [JsonPropertyName("hard_spawn_interval_multiplier")]
    public float HardSpawnIntervalMultiplier { get; set; } = 0.98f;

    // Separate multipliers for armored (tanky) and swift enemy counts.
    // The base enemy_count_multiplier scales all types uniformly - these let the
    // optimizer tune enemy composition independently (e.g. more armored without
    // more basics). Applied on top of enemy_count_multiplier in DataLoader.GetWaveConfig.
    [JsonPropertyName("easy_tanky_count_multiplier")]
    public float EasyTankyCountMultiplier { get; set; } = 1.0529f;
    [JsonPropertyName("easy_swift_count_multiplier")]
    public float EasySwiftCountMultiplier { get; set; } = 1.022f;
    [JsonPropertyName("easy_splitter_count_multiplier")]
    public float EasySplitterCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("easy_reverse_count_multiplier")]
    public float EasyReverseCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("normal_tanky_count_multiplier")]
    public float NormalTankyCountMultiplier { get; set; } = 1.0293f;
    [JsonPropertyName("normal_swift_count_multiplier")]
    public float NormalSwiftCountMultiplier { get; set; } = 1.1648f;
    [JsonPropertyName("normal_splitter_count_multiplier")]
    public float NormalSplitterCountMultiplier { get; set; } = 1.055f;
    [JsonPropertyName("normal_reverse_count_multiplier")]
    public float NormalReverseCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("hard_tanky_count_multiplier")]
    public float HardTankyCountMultiplier { get; set; } = 0.876f;
    [JsonPropertyName("hard_swift_count_multiplier")]
    public float HardSwiftCountMultiplier { get; set; } = 1.1182f;
    [JsonPropertyName("hard_splitter_count_multiplier")]
    public float HardSplitterCountMultiplier { get; set; } = 0.9333f;
    [JsonPropertyName("hard_reverse_count_multiplier")]
    public float HardReverseCountMultiplier { get; set; } = 1f;

    [JsonPropertyName("easy_shield_drone_count_multiplier")]
    public float EasyShieldDroneCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("normal_shield_drone_count_multiplier")]
    public float NormalShieldDroneCountMultiplier { get; set; } = 1f;
    [JsonPropertyName("hard_shield_drone_count_multiplier")]
    public float HardShieldDroneCountMultiplier { get; set; } = 1f;

    [JsonPropertyName("gain_multipliers")]
    public Dictionary<string, float> GainMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["overkill"]   = 0.879f,
        ["split_shot"] = 0.8719f,
        // Chain-specific meter pacing trim to avoid reducing overall surge power globally.
        ["chain_reaction"] = 0.94f,
        // Blast core: each splash hit generates meter - trim to prevent over-filling on dense waves.
        ["blast_core"] = 0.88f,
    };

    [JsonPropertyName("tower_meter_gain_multipliers")]
    public Dictionary<string, float> TowerMeterGainMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["rapid_shooter"]    = 0.40f,   // fires 4+/s: heavy per-hit dampening to reduce over-surging
        ["heavy_cannon"]     = 3.50f,   // fires slowly: needs large per-hit boost to compete
        ["marker_tower"]     = 0.70f,   // slightly above average fire rate, trim down
        ["chain_tower"]      = 1.20f,   // moderate rate, small boost
        ["rift_prism"]       = 1.50f,   // mine pops are infrequent, boost per-proc
        ["phase_splitter"]   = 0.75f,   // hits 2 targets per shot (double proc rate), trim
        ["accordion_engine"] = 3.00f,   // 3.2s pulse interval, needs large per-hit boost
    };

    [JsonPropertyName("tower_surge_threshold_multipliers")]
    public Dictionary<string, float> TowerSurgeThresholdMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["rapid_shooter"]    = 1.50f,   // raise bar to further slow surge cadence
        ["heavy_cannon"]     = 0.50f,   // lower bar so slow fire rate can still reach threshold
        ["marker_tower"]     = 1.00f,
        ["chain_tower"]      = 0.90f,   // slight ease to offset reduced gain from chain bounce rate
        ["rift_prism"]       = 0.90f,   // slight ease to match mine-pop infrequency
        ["phase_splitter"]   = 1.00f,
        ["accordion_engine"] = 0.55f,   // low bar to compensate for the very slow pulse interval
    };

    /// <summary>
    /// Returns a profile with every multiplier at 1.0 and no per-mod overrides.
    /// Use in unit tests that need deterministic, unscaled spectacle behaviour.
    /// </summary>
    public static SpectacleTuningProfile Neutral() => new SpectacleTuningProfile
    {
        EnableOverkillBloom = true,
        EnableStatusDetonation = true,
        EnableResidue = true,
        OverkillBloomDamageScaleMultiplier = 1f,
        OverkillBloomRadiusMultiplier = 1f,
        DetonationMaxTargetsMultiplier = 1f,
        StatusDetonationDamageMultiplier = 1f,
        ResidueDamageMultiplier = 1f,
        MeterGainMultiplier = 1f,
        SurgeThresholdMultiplier = 1f,
        SurgeCooldownMultiplier = 1f,
        SurgeMeterAfterTriggerMultiplier = 1f,
        GlobalMeterPerSurgeMultiplier = 1f,
        GlobalThresholdMultiplier = 1f,
        GlobalMeterAfterTriggerMultiplier = 1f,
        InactivityGraceMultiplier = 1f,
        InactivityDecayMultiplier = 1f,
        CopyMultiplierScale = 1f,
        EasyTankyCountMultiplier = 1f,
        EasySwiftCountMultiplier = 1f,
        EasySplitterCountMultiplier = 1f,
        EasyReverseCountMultiplier = 1f,
        NormalTankyCountMultiplier = 1f,
        NormalSwiftCountMultiplier = 1f,
        NormalSplitterCountMultiplier = 1f,
        NormalReverseCountMultiplier = 1f,
        HardTankyCountMultiplier = 1f,
        HardSwiftCountMultiplier = 1f,
        HardSplitterCountMultiplier = 1f,
        HardReverseCountMultiplier = 1f,
        EasyShieldDroneCountMultiplier = 1f,
        NormalShieldDroneCountMultiplier = 1f,
        HardShieldDroneCountMultiplier = 1f,
        GainMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
        TowerMeterGainMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
        TowerSurgeThresholdMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
    };

    [JsonIgnore]
    public bool HasModGainOverrides => GainMultipliers.Count > 0;

    public float ResolveGainMultiplier(string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId ?? string.Empty);
        if (GainMultipliers.TryGetValue(normalized, out float specific))
            return MathF.Max(0f, specific);
        return 1f;
    }

    public float ResolveTowerMeterGainMultiplier(string towerId)
    {
        string normalized = NormalizeTowerId(towerId);
        if (TowerMeterGainMultipliers.TryGetValue(normalized, out float specific))
            return MathF.Max(0f, specific);
        return 1f;
    }

    public float ResolveTowerSurgeThresholdMultiplier(string towerId)
    {
        string normalized = NormalizeTowerId(towerId);
        if (TowerSurgeThresholdMultipliers.TryGetValue(normalized, out float specific))
            return MathF.Max(0.05f, specific);
        return 1f;
    }

    public SpectacleTuningProfile CloneNormalized()
    {
        var clone = new SpectacleTuningProfile
        {
            EnableOverkillBloom = EnableOverkillBloom,
            EnableStatusDetonation = EnableStatusDetonation,
            EnableResidue = EnableResidue,
            OverkillBloomDamageScaleMultiplier = MathF.Max(0f, OverkillBloomDamageScaleMultiplier),
            OverkillBloomRadiusMultiplier = MathF.Max(0.1f, OverkillBloomRadiusMultiplier),
            DetonationMaxTargetsMultiplier = MathF.Max(0.1f, DetonationMaxTargetsMultiplier),
            StatusDetonationDamageMultiplier = MathF.Max(0f, StatusDetonationDamageMultiplier),
            ResidueDamageMultiplier = MathF.Max(0f, ResidueDamageMultiplier),
            MeterGainMultiplier = MathF.Max(0f, MeterGainMultiplier),
            SurgeThresholdMultiplier = MathF.Max(0.05f, SurgeThresholdMultiplier),
            SurgeCooldownMultiplier = MathF.Max(0f, SurgeCooldownMultiplier),
            SurgeMeterAfterTriggerMultiplier = MathF.Max(0f, SurgeMeterAfterTriggerMultiplier),
            GlobalMeterPerSurgeMultiplier = MathF.Max(0f, GlobalMeterPerSurgeMultiplier),
            GlobalThresholdMultiplier = MathF.Max(0.05f, GlobalThresholdMultiplier),
            GlobalMeterAfterTriggerMultiplier = MathF.Max(0f, GlobalMeterAfterTriggerMultiplier),
            InactivityGraceMultiplier = MathF.Max(0f, InactivityGraceMultiplier),
            InactivityDecayMultiplier = MathF.Max(0f, InactivityDecayMultiplier),
            CopyMultiplierScale = MathF.Max(0f, CopyMultiplierScale),
            EasyEnemyHpMultiplier = Math.Clamp(EasyEnemyHpMultiplier, 0.1f, 5f),
            EasyEnemyCountMultiplier = Math.Clamp(EasyEnemyCountMultiplier, 0.1f, 5f),
            EasySpawnIntervalMultiplier = Math.Clamp(EasySpawnIntervalMultiplier, 0.2f, 3f),
            EasyHpGrowthMultiplier = Math.Clamp(EasyHpGrowthMultiplier, 0.5f, 2f),
            NormalHpGrowthMultiplier = Math.Clamp(NormalHpGrowthMultiplier, 0.5f, 2f),
            HardHpGrowthMultiplier = Math.Clamp(HardHpGrowthMultiplier, 0.5f, 2f),
            NormalEnemyHpMultiplier = Math.Clamp(NormalEnemyHpMultiplier, 0.1f, 5f),
            NormalEnemyCountMultiplier = Math.Clamp(NormalEnemyCountMultiplier, 0.1f, 5f),
            NormalSpawnIntervalMultiplier = Math.Clamp(NormalSpawnIntervalMultiplier, 0.2f, 3f),
            HardEnemyHpMultiplier = Math.Clamp(HardEnemyHpMultiplier, 0.1f, 5f),
            HardEnemyCountMultiplier = Math.Clamp(HardEnemyCountMultiplier, 0.1f, 5f),
            HardSpawnIntervalMultiplier = Math.Clamp(HardSpawnIntervalMultiplier, 0.2f, 3f),
            EasyTankyCountMultiplier = Math.Clamp(EasyTankyCountMultiplier, 0.1f, 5f),
            EasySwiftCountMultiplier = Math.Clamp(EasySwiftCountMultiplier, 0.1f, 5f),
            EasySplitterCountMultiplier = Math.Clamp(EasySplitterCountMultiplier, 0.1f, 5f),
            EasyReverseCountMultiplier = Math.Clamp(EasyReverseCountMultiplier, 0.1f, 5f),
            NormalTankyCountMultiplier = Math.Clamp(NormalTankyCountMultiplier, 0.1f, 5f),
            NormalSwiftCountMultiplier = Math.Clamp(NormalSwiftCountMultiplier, 0.1f, 5f),
            NormalSplitterCountMultiplier = Math.Clamp(NormalSplitterCountMultiplier, 0.1f, 5f),
            NormalReverseCountMultiplier = Math.Clamp(NormalReverseCountMultiplier, 0.1f, 5f),
            HardTankyCountMultiplier = Math.Clamp(HardTankyCountMultiplier, 0.1f, 5f),
            HardSwiftCountMultiplier = Math.Clamp(HardSwiftCountMultiplier, 0.1f, 5f),
            HardSplitterCountMultiplier = Math.Clamp(HardSplitterCountMultiplier, 0.1f, 5f),
            HardReverseCountMultiplier = Math.Clamp(HardReverseCountMultiplier, 0.1f, 5f),
            EasyShieldDroneCountMultiplier = Math.Clamp(EasyShieldDroneCountMultiplier, 0.1f, 5f),
            NormalShieldDroneCountMultiplier = Math.Clamp(NormalShieldDroneCountMultiplier, 0.1f, 5f),
            HardShieldDroneCountMultiplier = Math.Clamp(HardShieldDroneCountMultiplier, 0.1f, 5f),
            GainMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
            TowerMeterGainMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
            TowerSurgeThresholdMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
        };

        foreach (var kv in GainMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.GainMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }
        foreach (var kv in TowerMeterGainMultipliers)
        {
            string normalized = NormalizeTowerId(kv.Key);
            clone.TowerMeterGainMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }
        foreach (var kv in TowerSurgeThresholdMultipliers)
        {
            string normalized = NormalizeTowerId(kv.Key);
            clone.TowerSurgeThresholdMultipliers[normalized] = MathF.Max(0.05f, kv.Value);
        }

        return clone;
    }

    private static string NormalizeTowerId(string towerId)
        => (towerId ?? string.Empty).Trim().ToLowerInvariant();
}

public static class SpectacleTuning
{
    private static SpectacleTuningProfile _current = new SpectacleTuningProfile();

    public static SpectacleTuningProfile Current => _current;
    public static string ActiveLabel { get; private set; } = "baseline";

    public static void Reset()
    {
        _current = new SpectacleTuningProfile();
        ActiveLabel = "baseline";
    }

    public static void Apply(SpectacleTuningProfile? profile, string? label = null)
    {
        _current = (profile ?? new SpectacleTuningProfile()).CloneNormalized();
        ActiveLabel = string.IsNullOrWhiteSpace(label) ? "custom" : label.Trim();
    }
}
