using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

/// <summary>Helpers for applying status effects. Marked state is stored directly on EnemyInstance.</summary>
public static class Statuses
{
    /// <summary>Apply or refresh Marked. Duration resets on reapplication.</summary>
    public static void ApplyMarked(IEnemyView target, float duration) =>
        target.MarkedRemaining = duration;

    /// <summary>Apply or refresh Slow. Duration resets on reapplication. Optional speedFactor defaults to Balance.SlowSpeedFactor.</summary>
    public static void ApplySlow(IEnemyView target, float duration, float? speedFactor = null)
    {
        target.SlowRemaining = duration;
        target.SlowSpeedFactor = speedFactor ?? Balance.SlowSpeedFactor;
    }

    /// <summary>Apply or refresh a temporary damage-taken amplification window.</summary>
    public static void ApplyDamageAmp(IEnemyView target, float duration, float multiplier)
    {
        if (duration <= 0f || multiplier <= 0f)
            return;
        target.DamageAmpRemaining = System.MathF.Max(target.DamageAmpRemaining, duration);
        target.DamageAmpMultiplier = System.MathF.Max(target.DamageAmpMultiplier, multiplier);
    }
}
