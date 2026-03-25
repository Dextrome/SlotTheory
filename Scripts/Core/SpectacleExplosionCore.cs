using System;

namespace SlotTheory.Core;

public readonly record struct OverkillBloomProfile(
    bool ShouldTrigger,
    float OverflowVisualT,
    float VisualRadius,
    float BloomDamage,
    int MaxTargets);

public readonly record struct GlobalSurgeWaveTiming(
    float WaveSpeed,
    float ImpactDelay,
    float PreFlashDelay);

public enum ExplosionResidueKind
{
    None,
    FrostSlow,
    VulnerabilityZone,
    BurnPatch,
}

public readonly record struct ExplosionResidueProfile(
    bool ShouldSpawn,
    ExplosionResidueKind Kind,
    float DurationSeconds,
    float Radius,
    float TickIntervalSeconds,
    float Potency);

public readonly record struct ExplosionHitStopProfile(
    bool ShouldApply,
    float DurationSeconds,
    float SlowScale);

public readonly record struct ResidueTickAdvance(
    int TickCount,
    float TickRemainingAfter);

public enum ComboExplosionSkin
{
    Default,
    ChillShatter,
    ChainArc,
    SplitShrapnel,
    FocusImplosion,
}

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
    public const float SurgeStatusDetonationStaggerSeconds = 0.20f;
    public const float MarkedPopRadius = 170f;
    public const float MarkedPopDamageAmpDuration = 1.45f;
    public const float MarkedPopDamageAmpMultiplier = 0.22f;
    public const float ResidueFrostSlowDurationSeconds = 0.80f;
    public const float ResidueVulnerabilityDurationSeconds = 1.20f;
    public const float ResidueBurnDurationSeconds = 0.90f;
    public const float ResidueTickIntervalSeconds = 0.20f;
    public const float ResidueFrostRadius = 88f;
    public const float ResidueVulnerabilityRadius = 108f;
    public const float ResidueBurnRadius = 96f;
    public const float HitStopMinDurationSeconds = 0.04f;
    public const float HitStopMaxDurationSeconds = 0.08f;
    public const float LargeSurgeAfterimagePowerThreshold = 1.55f;

    public static bool ShouldEmitSecondStage(bool major) => major;

    public static ExplosionResidueProfile ResolveResidueProfile(
        ComboExplosionSkin skin,
        bool globalSurge,
        float surgePower,
        int chainIndex)
    {
        if (!SpectacleTuning.Current.EnableResidue || (!globalSurge && chainIndex > 0))
        {
            return new ExplosionResidueProfile(
                ShouldSpawn: false,
                Kind: ExplosionResidueKind.None,
                DurationSeconds: 0f,
                Radius: 0f,
                TickIntervalSeconds: 0f,
                Potency: 0f);
        }

        return new ExplosionResidueProfile(
            ShouldSpawn: true,
            Kind: ExplosionResidueKind.VulnerabilityZone,
            DurationSeconds: ResidueVulnerabilityDurationSeconds,
            Radius: ResidueVulnerabilityRadius,
            TickIntervalSeconds: ResidueTickIntervalSeconds,
            Potency: globalSurge ? 1.10f : 1.00f);
    }

    public static ExplosionHitStopProfile ResolveExplosionHitStopProfile(bool majorExplosion, bool globalSurge, float surgePower)
    {
        if (!majorExplosion)
            return new ExplosionHitStopProfile(ShouldApply: false, DurationSeconds: 0f, SlowScale: 1f);

        float duration = globalSurge ? HitStopMaxDurationSeconds : HitStopMinDurationSeconds + 0.02f;
        float slowScale = globalSurge ? 0.24f : 0.34f;
        return new ExplosionHitStopProfile(ShouldApply: true, DurationSeconds: duration, SlowScale: slowScale);
    }

    public static ResidueTickAdvance ResolveResidueTickAdvance(
        float tickRemaining,
        float tickIntervalSeconds,
        float deltaSeconds,
        int maxTicksPerFrame = 12)
    {
        float interval = MathF.Max(0.001f, tickIntervalSeconds);
        int tickCap = Math.Max(1, maxTicksPerFrame);
        float remaining = tickRemaining - MathF.Max(0f, deltaSeconds);
        int dueTicks = 0;

        while (remaining <= 0f && dueTicks < tickCap)
        {
            dueTicks++;
            remaining += interval;
        }

        return new ResidueTickAdvance(dueTicks, remaining);
    }

    public static float ResolveLargeSurgeAfterimageStrength(bool majorExplosion, bool globalSurge, float surgePower)
    {
        if (!majorExplosion) return 0f;
        return globalSurge ? 1.0f : 0.5f;
    }

    public static int ResolveStatusDetonationMaxTargets(bool globalSurge, bool reducedMotion)
    {
        if (!SpectacleTuning.Current.EnableStatusDetonation)
            return 0;

        int baseCap = globalSurge
            ? (reducedMotion ? 10 : 24)
            : 8;

        float multiplier = MathF.Max(0.1f, SpectacleTuning.Current.DetonationMaxTargetsMultiplier);
        int adjusted = (int)MathF.Round(baseCap * multiplier);
        int hardCap = globalSurge ? 48 : 8;
        return Clamp(adjusted, 1, hardCap);
    }

    public static float ResolveStatusDetonationStaggerSeconds(bool reducedMotion)
        => SurgeStatusDetonationStaggerSeconds * (reducedMotion ? 0.55f : 1f);

    public static ComboExplosionSkin ResolveComboExplosionSkin(string modA, string modB)
    {
        string a = SpectacleDefinitions.NormalizeModId(modA ?? string.Empty);
        string b = SpectacleDefinitions.NormalizeModId(modB ?? string.Empty);
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return ComboExplosionSkin.Default;

        if (a == SpectacleDefinitions.ChillShot || b == SpectacleDefinitions.ChillShot)
            return ComboExplosionSkin.ChillShatter;
        if (a == SpectacleDefinitions.ChainReaction || b == SpectacleDefinitions.ChainReaction)
            return ComboExplosionSkin.ChainArc;
        if (a == SpectacleDefinitions.SplitShot || b == SpectacleDefinitions.SplitShot)
            return ComboExplosionSkin.SplitShrapnel;
        if (a == SpectacleDefinitions.FocusLens || b == SpectacleDefinitions.FocusLens)
            return ComboExplosionSkin.FocusImplosion;
        return ComboExplosionSkin.Default;
    }

    public static OverkillBloomProfile BuildOverkillBloomProfile(float overflowDamage)
    {
        if (!SpectacleTuning.Current.EnableOverkillBloom || overflowDamage < OverkillBloomOverflowThreshold)
        {
            return new OverkillBloomProfile(
                ShouldTrigger: false,
                OverflowVisualT: 0f,
                VisualRadius: 0f,
                BloomDamage: 0f,
                MaxTargets: 0);
        }

        float overflowVisual = Clamp(overflowDamage, 0f, OverkillBloomVisualOverflowCap);
        float overflowVisualT = Clamp(overflowVisual / OverkillBloomVisualOverflowCap, 0f, 1f);
        float radius = Lerp(OverkillBloomRadiusMin, OverkillBloomRadiusMax, overflowVisualT)
            * MathF.Max(0.1f, SpectacleTuning.Current.OverkillBloomRadiusMultiplier);
        float damageScale = MathF.Max(0f, SpectacleTuning.Current.OverkillBloomDamageScaleMultiplier);
        float bloomDamageCap = OverkillBloomDamageCap * MathF.Max(0.1f, damageScale);
        float bloomDamage = damageScale <= 0f
            ? 0f
            : Clamp(overflowDamage * OverkillBloomDamageScale * damageScale, 4f, bloomDamageCap);
        int maxTargets = Clamp(2 + (int)MathF.Floor(overflowVisualT * 5f), 2, 7);

        return new OverkillBloomProfile(
            ShouldTrigger: true,
            OverflowVisualT: overflowVisualT,
            VisualRadius: radius,
            BloomDamage: bloomDamage,
            MaxTargets: maxTargets);
    }

    public static GlobalSurgeWaveTiming ResolveGlobalSurgeWaveTiming(float distance, int contributors, bool reducedMotion)
    {
        const float waveSpeed = (GlobalSurgeWaveSpeedMin + GlobalSurgeWaveSpeedMax) * 0.5f;
        if (reducedMotion)
            return new GlobalSurgeWaveTiming(waveSpeed, ImpactDelay: 0f, PreFlashDelay: 0f);
        float impactDelay = MathF.Max(0f, distance) / waveSpeed;
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
