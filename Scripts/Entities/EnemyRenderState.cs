using Godot;

namespace SlotTheory.Entities;

public enum EnemyDamageBand
{
    Healthy,
    Worn,
    Damaged,
    Critical,
}

/// <summary>
/// Shared per-frame render state used by layered enemy passes.
/// </summary>
public readonly struct EnemyRenderState
{
    public float HpRatio { get; }
    public float ThrustPulse { get; }
    public float NearDeathPulse { get; }
    public float NearDeathFlicker { get; }
    public float HitFlash { get; }
    public float EmissivePulse { get; }
    public bool IsMarked { get; }
    public bool IsSlowed { get; }
    public EnemyDamageBand DamageBand { get; }
    public float DamageIntensity { get; }

    public EnemyRenderState(
        float hpRatio,
        float thrustPulse,
        float nearDeathPulse,
        float nearDeathFlicker,
        float hitFlash,
        bool isMarked,
        bool isSlowed)
    {
        HpRatio = Mathf.Clamp(hpRatio, 0f, 1f);
        ThrustPulse = thrustPulse;
        NearDeathPulse = nearDeathPulse;
        NearDeathFlicker = nearDeathFlicker;
        HitFlash = Mathf.Clamp(hitFlash, 0f, 1f);
        IsMarked = isMarked;
        IsSlowed = isSlowed;

        DamageBand = ResolveDamageBand(HpRatio);
        DamageIntensity = ResolveDamageIntensity(HpRatio);
        EmissivePulse = Mathf.Clamp(0.52f + ThrustPulse * 0.34f + NearDeathPulse * 0.44f + HitFlash * 0.60f, 0f, 2.4f);
    }

    public static EnemyDamageBand ResolveDamageBand(float hpRatio)
    {
        float clamped = Mathf.Clamp(hpRatio, 0f, 1f);
        if (clamped >= 0.98f) return EnemyDamageBand.Healthy;
        if (clamped >= 0.80f) return EnemyDamageBand.Worn;
        if (clamped >= 0.52f) return EnemyDamageBand.Damaged;
        return EnemyDamageBand.Critical;
    }

    public static float ResolveDamageIntensity(float hpRatio)
    {
        float damage = Mathf.Clamp(1f - hpRatio, 0f, 1f);
        // Slightly front-load visibility so material wear is readable before critical HP.
        return Mathf.Pow(damage, 0.62f);
    }
}
