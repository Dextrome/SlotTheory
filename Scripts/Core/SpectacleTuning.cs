using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlotTheory.Core;

/// <summary>
/// Runtime tuning overrides used by automation, sweeps, and combat lab scenarios.
/// Defaults reflect the tuned best_tuning profile (iter01_cand02, 2026-03-16 pipeline run).
/// All multipliers are applied on top of Balance.cs constants — Reset() returns to these same defaults.
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
    public float OverkillBloomDamageScaleMultiplier { get; set; } = 0.889f;
    [JsonPropertyName("overkill_bloom_radius_multiplier")]
    public float OverkillBloomRadiusMultiplier { get; set; } = 1f;
    [JsonPropertyName("overkill_bloom_threshold_multiplier")]
    public float OverkillBloomThresholdMultiplier { get; set; } = 1.1841f;
    [JsonPropertyName("overkill_bloom_max_targets_multiplier")]
    public float OverkillBloomMaxTargetsMultiplier { get; set; } = 1f;

    [JsonPropertyName("detonation_max_targets_multiplier")]
    public float DetonationMaxTargetsMultiplier { get; set; } = 0.9457f;
    [JsonPropertyName("detonation_stagger_multiplier")]
    public float DetonationStaggerMultiplier { get; set; } = 0.9043f;
    [JsonPropertyName("status_detonation_damage_multiplier")]
    public float StatusDetonationDamageMultiplier { get; set; } = 1.1732f;

    [JsonPropertyName("residue_duration_multiplier")]
    public float ResidueDurationMultiplier { get; set; } = 1.1929f;
    [JsonPropertyName("residue_potency_multiplier")]
    public float ResiduePotencyMultiplier { get; set; } = 1.2142f;
    [JsonPropertyName("residue_damage_multiplier")]
    public float ResidueDamageMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_tick_interval_multiplier")]
    public float ResidueTickIntervalMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_max_active_multiplier")]
    public float ResidueMaxActiveMultiplier { get; set; } = 0.9831f;

    [JsonPropertyName("explosion_followup_damage_multiplier")]
    public float ExplosionFollowUpDamageMultiplier { get; set; } = 1f;

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
    [JsonPropertyName("global_contribution_window_multiplier")]
    public float GlobalContributionWindowMultiplier { get; set; } = 1.008f;
    [JsonPropertyName("inactivity_grace_multiplier")]
    public float InactivityGraceMultiplier { get; set; } = 0.9151f;
    [JsonPropertyName("inactivity_decay_multiplier")]
    public float InactivityDecayMultiplier { get; set; } = 1.2156f;
    [JsonPropertyName("contribution_window_multiplier")]
    public float ContributionWindowMultiplier { get; set; } = 0.8148f;
    [JsonPropertyName("role_lock_meter_threshold_multiplier")]
    public float RoleLockMeterThresholdMultiplier { get; set; } = 1.1286f;
    [JsonPropertyName("meter_damage_reference_multiplier")]
    public float MeterDamageReferenceMultiplier { get; set; } = 0.8279f;
    [JsonPropertyName("meter_damage_weight_multiplier")]
    public float MeterDamageWeightMultiplier { get; set; } = 1.1475f;
    [JsonPropertyName("meter_damage_min_clamp_multiplier")]
    public float MeterDamageMinClampMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_damage_max_clamp_multiplier")]
    public float MeterDamageMaxClampMultiplier { get; set; } = 0.9065f;
    [JsonPropertyName("token_cap_multiplier")]
    public float TokenCapMultiplier { get; set; } = 1.1632f;
    [JsonPropertyName("token_regen_multiplier")]
    public float TokenRegenMultiplier { get; set; } = 1f;
    [JsonPropertyName("copy_multiplier_scale")]
    public float CopyMultiplierScale { get; set; } = 0.8532f;
    [JsonPropertyName("diversity_multiplier_scale")]
    public float DiversityMultiplierScale { get; set; } = 0.867f;
    [JsonPropertyName("event_scalar_multiplier")]
    public float EventScalarMultiplier { get; set; } = 1.0115f;
    [JsonPropertyName("second_stage_power_threshold")]
    public float SecondStagePowerThreshold { get; set; } = 0.95f;
    [JsonPropertyName("normal_enemy_hp_multiplier")]
    public float NormalEnemyHpMultiplier { get; set; } = 1.208f;
    [JsonPropertyName("normal_enemy_count_multiplier")]
    public float NormalEnemyCountMultiplier { get; set; } = 1.0776f;
    [JsonPropertyName("normal_spawn_interval_multiplier")]
    public float NormalSpawnIntervalMultiplier { get; set; } = 0.9196f;
    [JsonPropertyName("hard_enemy_hp_multiplier")]
    public float HardEnemyHpMultiplier { get; set; } = 1.2277f;
    [JsonPropertyName("hard_enemy_count_multiplier")]
    public float HardEnemyCountMultiplier { get; set; } = 1.074f;
    [JsonPropertyName("hard_spawn_interval_multiplier")]
    public float HardSpawnIntervalMultiplier { get; set; } = 0.9312f;

    [JsonPropertyName("gain_multipliers")]
    public Dictionary<string, float> GainMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["overkill"]   = 0.879f,
        ["split_shot"] = 0.8719f,
    };

    [JsonPropertyName("event_scalar_multipliers")]
    public Dictionary<string, float> EventScalarMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["chain_reaction"] = 0.7224f,
        ["split_shot"]     = 1.2471f,
        ["feedback_loop"]  = 0.9835f,
        ["hair_trigger"]   = 0.7396f,
    };

    [JsonPropertyName("token_cap_multipliers")]
    public Dictionary<string, float> TokenCapMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["chain_reaction"] = 1.0174f,
        ["split_shot"]     = 0.9327f,
    };

    [JsonPropertyName("token_regen_multipliers")]
    public Dictionary<string, float> TokenRegenMultipliers { get; set; } = new(StringComparer.Ordinal)
    {
        ["overkill"]   = 1.1516f,
        ["split_shot"] = 0.8316f,
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
        OverkillBloomThresholdMultiplier = 1f,
        OverkillBloomMaxTargetsMultiplier = 1f,
        DetonationMaxTargetsMultiplier = 1f,
        DetonationStaggerMultiplier = 1f,
        StatusDetonationDamageMultiplier = 1f,
        ResidueDurationMultiplier = 1f,
        ResiduePotencyMultiplier = 1f,
        ResidueDamageMultiplier = 1f,
        ResidueTickIntervalMultiplier = 1f,
        ResidueMaxActiveMultiplier = 1f,
        ExplosionFollowUpDamageMultiplier = 1f,
        MeterGainMultiplier = 1f,
        SurgeThresholdMultiplier = 1f,
        SurgeCooldownMultiplier = 1f,
        SurgeMeterAfterTriggerMultiplier = 1f,
        GlobalMeterPerSurgeMultiplier = 1f,
        GlobalThresholdMultiplier = 1f,
        GlobalMeterAfterTriggerMultiplier = 1f,
        GlobalContributionWindowMultiplier = 1f,
        InactivityGraceMultiplier = 1f,
        InactivityDecayMultiplier = 1f,
        ContributionWindowMultiplier = 1f,
        RoleLockMeterThresholdMultiplier = 1f,
        MeterDamageReferenceMultiplier = 1f,
        MeterDamageWeightMultiplier = 1f,
        MeterDamageMinClampMultiplier = 1f,
        MeterDamageMaxClampMultiplier = 1f,
        TokenCapMultiplier = 1f,
        TokenRegenMultiplier = 1f,
        CopyMultiplierScale = 1f,
        DiversityMultiplierScale = 1f,
        EventScalarMultiplier = 1f,
        SecondStagePowerThreshold = 0.95f,
        GainMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
        EventScalarMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
        TokenCapMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
        TokenRegenMultipliers = new System.Collections.Generic.Dictionary<string, float>(StringComparer.Ordinal),
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

    public float ResolveEventScalarMultiplier(string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId ?? string.Empty);
        float global = MathF.Max(0f, EventScalarMultiplier);
        if (EventScalarMultipliers.TryGetValue(normalized, out float specific))
            return global * MathF.Max(0f, specific);
        return global;
    }

    public float ResolveTokenCapMultiplier(string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId ?? string.Empty);
        float global = MathF.Max(0f, TokenCapMultiplier);
        if (TokenCapMultipliers.TryGetValue(normalized, out float specific))
            return global * MathF.Max(0f, specific);
        return global;
    }

    public float ResolveTokenRegenMultiplier(string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId ?? string.Empty);
        float global = MathF.Max(0f, TokenRegenMultiplier);
        if (TokenRegenMultipliers.TryGetValue(normalized, out float specific))
            return global * MathF.Max(0f, specific);
        return global;
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
            OverkillBloomThresholdMultiplier = MathF.Max(0.1f, OverkillBloomThresholdMultiplier),
            OverkillBloomMaxTargetsMultiplier = MathF.Max(0.1f, OverkillBloomMaxTargetsMultiplier),
            DetonationMaxTargetsMultiplier = MathF.Max(0.1f, DetonationMaxTargetsMultiplier),
            DetonationStaggerMultiplier = MathF.Max(0.1f, DetonationStaggerMultiplier),
            StatusDetonationDamageMultiplier = MathF.Max(0f, StatusDetonationDamageMultiplier),
            ResidueDurationMultiplier = MathF.Max(0.1f, ResidueDurationMultiplier),
            ResiduePotencyMultiplier = MathF.Max(0.1f, ResiduePotencyMultiplier),
            ResidueDamageMultiplier = MathF.Max(0f, ResidueDamageMultiplier),
            ResidueTickIntervalMultiplier = MathF.Max(0.2f, ResidueTickIntervalMultiplier),
            ResidueMaxActiveMultiplier = MathF.Max(0.1f, ResidueMaxActiveMultiplier),
            ExplosionFollowUpDamageMultiplier = MathF.Max(0f, ExplosionFollowUpDamageMultiplier),
            MeterGainMultiplier = MathF.Max(0f, MeterGainMultiplier),
            SurgeThresholdMultiplier = MathF.Max(0.05f, SurgeThresholdMultiplier),
            SurgeCooldownMultiplier = MathF.Max(0f, SurgeCooldownMultiplier),
            SurgeMeterAfterTriggerMultiplier = MathF.Max(0f, SurgeMeterAfterTriggerMultiplier),
            GlobalMeterPerSurgeMultiplier = MathF.Max(0f, GlobalMeterPerSurgeMultiplier),
            GlobalThresholdMultiplier = MathF.Max(0.05f, GlobalThresholdMultiplier),
            GlobalMeterAfterTriggerMultiplier = MathF.Max(0f, GlobalMeterAfterTriggerMultiplier),
            GlobalContributionWindowMultiplier = MathF.Max(0.05f, GlobalContributionWindowMultiplier),
            InactivityGraceMultiplier = MathF.Max(0f, InactivityGraceMultiplier),
            InactivityDecayMultiplier = MathF.Max(0f, InactivityDecayMultiplier),
            ContributionWindowMultiplier = MathF.Max(0.05f, ContributionWindowMultiplier),
            RoleLockMeterThresholdMultiplier = MathF.Max(0f, RoleLockMeterThresholdMultiplier),
            MeterDamageReferenceMultiplier = MathF.Max(0.05f, MeterDamageReferenceMultiplier),
            MeterDamageWeightMultiplier = MathF.Max(0f, MeterDamageWeightMultiplier),
            MeterDamageMinClampMultiplier = MathF.Max(0f, MeterDamageMinClampMultiplier),
            MeterDamageMaxClampMultiplier = MathF.Max(0f, MeterDamageMaxClampMultiplier),
            TokenCapMultiplier = MathF.Max(0f, TokenCapMultiplier),
            TokenRegenMultiplier = MathF.Max(0f, TokenRegenMultiplier),
            CopyMultiplierScale = MathF.Max(0f, CopyMultiplierScale),
            DiversityMultiplierScale = MathF.Max(0f, DiversityMultiplierScale),
            EventScalarMultiplier = MathF.Max(0f, EventScalarMultiplier),
            SecondStagePowerThreshold = Math.Clamp(SecondStagePowerThreshold, 0.05f, 3f),
            NormalEnemyHpMultiplier = Math.Clamp(NormalEnemyHpMultiplier, 0.1f, 5f),
            NormalEnemyCountMultiplier = Math.Clamp(NormalEnemyCountMultiplier, 0.1f, 5f),
            NormalSpawnIntervalMultiplier = Math.Clamp(NormalSpawnIntervalMultiplier, 0.2f, 3f),
            HardEnemyHpMultiplier = Math.Clamp(HardEnemyHpMultiplier, 0.1f, 5f),
            HardEnemyCountMultiplier = Math.Clamp(HardEnemyCountMultiplier, 0.1f, 5f),
            HardSpawnIntervalMultiplier = Math.Clamp(HardSpawnIntervalMultiplier, 0.2f, 3f),
            GainMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
            EventScalarMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
            TokenCapMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
            TokenRegenMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
        };

        foreach (var kv in GainMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.GainMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }
        foreach (var kv in EventScalarMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.EventScalarMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }
        foreach (var kv in TokenCapMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.TokenCapMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }
        foreach (var kv in TokenRegenMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.TokenRegenMultipliers[normalized] = MathF.Max(0f, kv.Value);
        }

        return clone;
    }
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
