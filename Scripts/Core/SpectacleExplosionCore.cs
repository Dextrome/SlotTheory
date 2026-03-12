using System;

namespace SlotTheory.Core;

public readonly record struct OverkillBloomProfile(
    bool ShouldTrigger,
    float OverflowVisualT,
    float VisualRadius,
    float BloomDamage,
    int MaxTargets,
    float BloomPower,
    bool StageTwoKick);

public readonly record struct GlobalSurgeWaveTiming(
    float WaveSpeed,
    float ImpactDelay,
    float PreFlashDelay);

/// <summary>
/// Pure math helpers for spectacle explosion tuning.
/// Keeps balancing and unit tests decoupled from Godot node/runtime APIs.
/// </summary>
public static class SpectacleExplosionCore
{
    public const float TwoStageBlastDelaySeconds = 0.12f;

    public const float OverkillBloomOverflowThreshold = 6f;
    // Intentionally larger than the damage-cap threshold so visual radius can
    // continue scaling after mechanical damage is capped.
    public const float OverkillBloomVisualOverflowCap = 320f;
    public const float OverkillBloomDamageScale = 0.42f;
    public const float OverkillBloomDamageCap = 86f;
    public const float OverkillBloomRadiusMin = 120f;
    public const float OverkillBloomRadiusMax = 250f;

    public const float GlobalSurgeWavePreFlashLeadSeconds = 0.05f;
    public const float GlobalSurgeWaveSpeedMin = 880f;
    public const float GlobalSurgeWaveSpeedMax = 1280f;

    public static bool ShouldEmitSecondStage(bool major, float power)
        => major || power >= 0.95f;

    public static OverkillBloomProfile BuildOverkillBloomProfile(float overflowDamage)
    {
        if (overflowDamage < OverkillBloomOverflowThreshold)
        {
            return new OverkillBloomProfile(
                ShouldTrigger: false,
                OverflowVisualT: 0f,
                VisualRadius: 0f,
                BloomDamage: 0f,
                MaxTargets: 0,
                BloomPower: 0f,
                StageTwoKick: false);
        }

        float overflowVisual = Clamp(overflowDamage, 0f, OverkillBloomVisualOverflowCap);
        float overflowVisualT = Clamp(overflowVisual / OverkillBloomVisualOverflowCap, 0f, 1f);
        float radius = Lerp(OverkillBloomRadiusMin, OverkillBloomRadiusMax, overflowVisualT);
        float bloomDamage = Clamp(overflowDamage * OverkillBloomDamageScale, 4f, OverkillBloomDamageCap);
        int maxTargets = Clamp(2 + (int)MathF.Floor(overflowVisualT * 5f), 2, 7);
        float bloomPower = Clamp(0.92f + overflowVisualT * 0.90f, 0.92f, 1.95f);

        return new OverkillBloomProfile(
            ShouldTrigger: true,
            OverflowVisualT: overflowVisualT,
            VisualRadius: radius,
            BloomDamage: bloomDamage,
            MaxTargets: maxTargets,
            BloomPower: bloomPower,
            StageTwoKick: overflowVisualT >= 0.40f);
    }

    public static GlobalSurgeWaveTiming ResolveGlobalSurgeWaveTiming(float distance, int contributors, bool reducedMotion)
    {
        float contributorT = Clamp((contributors - 1f) / 5f, 0f, 1f);
        float waveSpeed = Lerp(GlobalSurgeWaveSpeedMin, GlobalSurgeWaveSpeedMax, contributorT);

        if (reducedMotion)
            return new GlobalSurgeWaveTiming(waveSpeed, ImpactDelay: 0f, PreFlashDelay: 0f);

        float impactDelay = MathF.Max(0f, distance) / MathF.Max(220f, waveSpeed);
        float preFlashDelay = MathF.Max(0f, impactDelay - GlobalSurgeWavePreFlashLeadSeconds);
        return new GlobalSurgeWaveTiming(waveSpeed, impactDelay, preFlashDelay);
    }

    private static float Clamp(float value, float min, float max)
        => MathF.Min(max, MathF.Max(min, value));

    private static int Clamp(int value, int min, int max)
        => Math.Min(max, Math.Max(min, value));

    private static float Lerp(float a, float b, float t)
        => a + (b - a) * Clamp(t, 0f, 1f);
}
