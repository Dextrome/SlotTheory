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

    public static bool ShouldEmitSecondStage(bool major, float power)
    {
        if (major)
            return true;

        float threshold = SpectacleTuning.Current.SecondStagePowerThreshold;
        return power >= threshold;
    }

    public static ExplosionResidueProfile ResolveResidueProfile(
        ComboExplosionSkin skin,
        bool globalSurge,
        float surgePower,
        int chainIndex)
    {
        if (!SpectacleTuning.Current.EnableResidue)
        {
            return new ExplosionResidueProfile(
                ShouldSpawn: false,
                Kind: ExplosionResidueKind.None,
                DurationSeconds: 0f,
                Radius: 0f,
                TickIntervalSeconds: 0f,
                Potency: 0f);
        }

        ExplosionResidueKind kind = skin switch
        {
            ComboExplosionSkin.ChillShatter => ExplosionResidueKind.FrostSlow,
            ComboExplosionSkin.SplitShrapnel => ExplosionResidueKind.BurnPatch,
            ComboExplosionSkin.ChainArc => ExplosionResidueKind.VulnerabilityZone,
            ComboExplosionSkin.FocusImplosion => ExplosionResidueKind.VulnerabilityZone,
            _ => globalSurge ? ExplosionResidueKind.VulnerabilityZone : ExplosionResidueKind.None,
        };

        if (kind == ExplosionResidueKind.None)
        {
            return new ExplosionResidueProfile(
                ShouldSpawn: false,
                Kind: ExplosionResidueKind.None,
                DurationSeconds: 0f,
                Radius: 0f,
                TickIntervalSeconds: 0f,
                Potency: 0f);
        }

        int stride = kind switch
        {
            ExplosionResidueKind.FrostSlow => 2,
            ExplosionResidueKind.BurnPatch => 2,
            ExplosionResidueKind.VulnerabilityZone => globalSurge ? 2 : 3,
            _ => 99,
        };

        if (chainIndex < 0)
            chainIndex = 0;
        bool shouldSpawn = chainIndex % stride == 0;
        if (!shouldSpawn)
        {
            return new ExplosionResidueProfile(
                ShouldSpawn: false,
                Kind: kind,
                DurationSeconds: 0f,
                Radius: 0f,
                TickIntervalSeconds: 0f,
                Potency: 0f);
        }

        float duration = kind switch
        {
            ExplosionResidueKind.FrostSlow => ResidueFrostSlowDurationSeconds,
            ExplosionResidueKind.BurnPatch => ResidueBurnDurationSeconds,
            ExplosionResidueKind.VulnerabilityZone => ResidueVulnerabilityDurationSeconds,
            _ => 0f,
        };
        duration *= MathF.Max(0.1f, SpectacleTuning.Current.ResidueDurationMultiplier);
        float radius = kind switch
        {
            ExplosionResidueKind.FrostSlow => ResidueFrostRadius,
            ExplosionResidueKind.BurnPatch => ResidueBurnRadius,
            ExplosionResidueKind.VulnerabilityZone => ResidueVulnerabilityRadius,
            _ => 0f,
        };

        float potency = Clamp(
            (0.72f + 0.20f * Clamp(surgePower, 0.6f, 2.2f)) * (globalSurge ? 1.10f : 1f),
            0.65f,
            1.35f);
        potency *= MathF.Max(0.1f, SpectacleTuning.Current.ResiduePotencyMultiplier);
        potency = Clamp(potency, 0.20f, 2.40f);

        return new ExplosionResidueProfile(
            ShouldSpawn: true,
            Kind: kind,
            DurationSeconds: duration,
            Radius: radius,
            TickIntervalSeconds: ResidueTickIntervalSeconds,
            Potency: potency);
    }

    public static ExplosionHitStopProfile ResolveExplosionHitStopProfile(bool majorExplosion, bool globalSurge, float surgePower)
    {
        if (!majorExplosion)
        {
            return new ExplosionHitStopProfile(
                ShouldApply: false,
                DurationSeconds: 0f,
                SlowScale: 1f);
        }

        float t = Clamp((surgePower - 0.85f) / 1.35f, 0f, 1f);
        float duration = Lerp(
            globalSurge ? 0.056f : HitStopMinDurationSeconds,
            HitStopMaxDurationSeconds,
            t);
        float slowScale = Lerp(
            globalSurge ? 0.30f : 0.38f,
            globalSurge ? 0.22f : 0.30f,
            t);

        return new ExplosionHitStopProfile(
            ShouldApply: true,
            DurationSeconds: Clamp(duration, HitStopMinDurationSeconds, HitStopMaxDurationSeconds),
            SlowScale: Clamp(slowScale, 0.20f, 0.55f));
    }

    public static float ResolveLargeSurgeAfterimageStrength(bool majorExplosion, bool globalSurge, float surgePower)
    {
        if (!majorExplosion)
            return 0f;

        float t = Clamp((surgePower - LargeSurgeAfterimagePowerThreshold) / 0.75f, 0f, 1f);
        if (globalSurge)
            t = MathF.Max(0.62f, t);
        if (t <= 0f)
            return 0f;

        return Clamp(0.40f + 0.60f * t, 0f, 1f);
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
    {
        float baseStagger = SurgeStatusDetonationStaggerSeconds * (reducedMotion ? 0.55f : 1f);
        float multiplier = MathF.Max(0.1f, SpectacleTuning.Current.DetonationStaggerMultiplier);
        return baseStagger * multiplier;
    }

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
        if (!SpectacleTuning.Current.EnableOverkillBloom)
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

        float overflowThreshold = OverkillBloomOverflowThreshold * MathF.Max(0.1f, SpectacleTuning.Current.OverkillBloomThresholdMultiplier);
        if (overflowDamage < overflowThreshold)
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
        float radius = Lerp(OverkillBloomRadiusMin, OverkillBloomRadiusMax, overflowVisualT)
            * MathF.Max(0.1f, SpectacleTuning.Current.OverkillBloomRadiusMultiplier);
        float damageScale = MathF.Max(0f, SpectacleTuning.Current.OverkillBloomDamageScaleMultiplier);
        float bloomDamageCap = OverkillBloomDamageCap * MathF.Max(0.1f, damageScale);
        float bloomDamage = Clamp(overflowDamage * OverkillBloomDamageScale * damageScale, 4f, bloomDamageCap);
        int baseMaxTargets = Clamp(2 + (int)MathF.Floor(overflowVisualT * 5f), 2, 7);
        float maxTargetMult = MathF.Max(0.1f, SpectacleTuning.Current.OverkillBloomMaxTargetsMultiplier);
        int maxTargets = Clamp((int)MathF.Round(baseMaxTargets * maxTargetMult), 1, 14);
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
