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
    [JsonPropertyName("overkill_bloom_damage_scale_multiplier")]
    public float OverkillBloomDamageScaleMultiplier { get; set; } = 1f;
    [JsonPropertyName("overkill_bloom_radius_multiplier")]
    public float OverkillBloomRadiusMultiplier { get; set; } = 1f;
    [JsonPropertyName("detonation_max_targets_multiplier")]
    public float DetonationMaxTargetsMultiplier { get; set; } = 1f;
    [JsonPropertyName("detonation_stagger_multiplier")]
    public float DetonationStaggerMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_duration_multiplier")]
    public float ResidueDurationMultiplier { get; set; } = 1f;
    [JsonPropertyName("residue_potency_multiplier")]
    public float ResiduePotencyMultiplier { get; set; } = 1f;
    [JsonPropertyName("meter_gain_multiplier")]
    public float MeterGainMultiplier { get; set; } = 1f;
    [JsonPropertyName("second_stage_power_threshold")]
    public float SecondStagePowerThreshold { get; set; } = 0.95f;
    [JsonPropertyName("gain_multipliers")]
    public Dictionary<string, float> GainMultipliers { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public bool HasModGainOverrides => GainMultipliers.Count > 0;

    public float ResolveGainMultiplier(string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId ?? string.Empty);
        if (GainMultipliers.TryGetValue(normalized, out float specific))
            return MathF.Max(0f, specific);
        return 1f;
    }

    public SpectacleTuningProfile CloneNormalized()
    {
        var clone = new SpectacleTuningProfile
        {
            OverkillBloomDamageScaleMultiplier = MathF.Max(0f, OverkillBloomDamageScaleMultiplier),
            OverkillBloomRadiusMultiplier = MathF.Max(0.1f, OverkillBloomRadiusMultiplier),
            DetonationMaxTargetsMultiplier = MathF.Max(0.1f, DetonationMaxTargetsMultiplier),
            DetonationStaggerMultiplier = MathF.Max(0.1f, DetonationStaggerMultiplier),
            ResidueDurationMultiplier = MathF.Max(0.1f, ResidueDurationMultiplier),
            ResiduePotencyMultiplier = MathF.Max(0.1f, ResiduePotencyMultiplier),
            MeterGainMultiplier = MathF.Max(0f, MeterGainMultiplier),
            SecondStagePowerThreshold = Math.Clamp(SecondStagePowerThreshold, 0.05f, 3f),
            GainMultipliers = new Dictionary<string, float>(StringComparer.Ordinal),
        };

        foreach (var kv in GainMultipliers)
        {
            string normalized = SpectacleDefinitions.NormalizeModId(kv.Key);
            clone.GainMultipliers[normalized] = MathF.Max(0f, kv.Value);
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
