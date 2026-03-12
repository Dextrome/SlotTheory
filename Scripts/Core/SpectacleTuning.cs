using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlotTheory.Core;

/// <summary>
/// Runtime tuning overrides used by automation, sweeps, and combat lab scenarios.
/// Defaults are neutral multipliers so gameplay is unchanged unless explicitly overridden.
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
    public float OverkillBloomDamageScaleMultiplier { get; set; } = 1f;
    [JsonPropertyName("overkill_bloom_radius_multiplier")]
    public float OverkillBloomRadiusMultiplier { get; set; } = 1f;
    [JsonPropertyName("overkill_bloom_threshold_multiplier")]
    public float OverkillBloomThresholdMultiplier { get; set; } = 1f;
    [JsonPropertyName("overkill_bloom_max_targets_multiplier")]
    public float OverkillBloomMaxTargetsMultiplier { get; set; } = 1f;

    [JsonPropertyName("detonation_max_targets_multiplier")]
    public float DetonationMaxTargetsMultiplier { get; set; } = 1f;
    [JsonPropertyName("detonation_stagger_multiplier")]
    public float DetonationStaggerMultiplier { get; set; } = 1f;
    [JsonPropertyName("status_detonation_damage_multiplier")]
    public float StatusDetonationDamageMultiplier { get; set; } = 1f;

    [JsonPropertyName("residue_duration_multiplier")]
    public float ResidueDurationMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_potency_multiplier")]
    public float ResiduePotencyMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_damage_multiplier")]
    public float ResidueDamageMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_tick_interval_multiplier")]
    public float ResidueTickIntervalMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_max_active_multiplier")]
    public float ResidueMaxActiveMultiplier { get; set; } = 1f;

    [JsonPropertyName("explosion_followup_damage_multiplier")]
    public float ExplosionFollowUpDamageMultiplier { get; set; } = 1f;

    [JsonPropertyName("meter_gain_multiplier")]
    public float MeterGainMultiplier { get; set; } = 1f;
    [JsonPropertyName("surge_threshold_multiplier")]
    public float SurgeThresholdMultiplier { get; set; } = 1f;
    [JsonPropertyName("surge_cooldown_multiplier")]
    public float SurgeCooldownMultiplier { get; set; } = 1f;
    [JsonPropertyName("surge_meter_after_trigger_multiplier")]
    public float SurgeMeterAfterTriggerMultiplier { get; set; } = 1f;
    [JsonPropertyName("global_meter_per_surge_multiplier")]
    public float GlobalMeterPerSurgeMultiplier { get; set; } = 1f;
    [JsonPropertyName("global_threshold_multiplier")]
    public float GlobalThresholdMultiplier { get; set; } = 1f;
    [JsonPropertyName("global_meter_after_trigger_multiplier")]
    public float GlobalMeterAfterTriggerMultiplier { get; set; } = 1f;
    [JsonPropertyName("global_contribution_window_multiplier")]
    public float GlobalContributionWindowMultiplier { get; set; } = 1f;
    [JsonPropertyName("inactivity_grace_multiplier")]
    public float InactivityGraceMultiplier { get; set; } = 1f;
    [JsonPropertyName("inactivity_decay_multiplier")]
    public float InactivityDecayMultiplier { get; set; } = 1f;
    [JsonPropertyName("contribution_window_multiplier")]
    public float ContributionWindowMultiplier { get; set; } = 1f;
    [JsonPropertyName("role_lock_meter_threshold_multiplier")]
    public float RoleLockMeterThresholdMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_damage_reference_multiplier")]
    public float MeterDamageReferenceMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_damage_weight_multiplier")]
    public float MeterDamageWeightMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_damage_min_clamp_multiplier")]
    public float MeterDamageMinClampMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_damage_max_clamp_multiplier")]
    public float MeterDamageMaxClampMultiplier { get; set; } = 1f;
    [JsonPropertyName("token_cap_multiplier")]
    public float TokenCapMultiplier { get; set; } = 1f;
    [JsonPropertyName("token_regen_multiplier")]
    public float TokenRegenMultiplier { get; set; } = 1f;
    [JsonPropertyName("copy_multiplier_scale")]
    public float CopyMultiplierScale { get; set; } = 1f;
    [JsonPropertyName("diversity_multiplier_scale")]
    public float DiversityMultiplierScale { get; set; } = 1f;
    [JsonPropertyName("event_scalar_multiplier")]
    public float EventScalarMultiplier { get; set; } = 1f;
    [JsonPropertyName("second_stage_power_threshold")]
    public float SecondStagePowerThreshold { get; set; } = 0.95f;
    [JsonPropertyName("gain_multipliers")]
    public Dictionary<string, float> GainMultipliers { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("event_scalar_multipliers")]
    public Dictionary<string, float> EventScalarMultipliers { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("token_cap_multipliers")]
    public Dictionary<string, float> TokenCapMultipliers { get; set; } = new(StringComparer.Ordinal);
    [JsonPropertyName("token_regen_multipliers")]
    public Dictionary<string, float> TokenRegenMultipliers { get; set; } = new(StringComparer.Ordinal);

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
