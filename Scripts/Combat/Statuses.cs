using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

/// <summary>Helpers for applying status effects. Marked state is stored directly on EnemyInstance.</summary>
public static class Statuses
{
    private const string ChillModifierId = "slow";

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

    /// <summary>
    /// Applies Chill Shot's slow to <paramref name="target"/> if the attacker has at least one
    /// Chill Shot copy equipped. Multiple copies stack multiplicatively.
    /// Optionally pass <paramref name="state"/> to apply Cold Circuit's duration bonus.
    /// </summary>
    public static bool TryApplyChillFromAttacker(ITowerView attacker, IEnemyView target, RunState? state = null)
    {
        if (!TryGetChillSlowFactor(attacker, out float slowFactor))
            return false;

        float duration = Balance.SlowDuration * (state?.SlowDurationMultiplier ?? 1f);
        ApplySlow(target, duration, slowFactor);
        return true;
    }

    /// <summary>
    /// Resolves the multiplicative Chill Shot slow factor from the attacker's equipped copies.
    /// Returns false when no Chill Shot is equipped.
    /// </summary>
    public static bool TryGetChillSlowFactor(ITowerView attacker, out float slowFactor)
    {
        int chillCopies = 0;
        foreach (var mod in attacker.Modifiers)
        {
            if (mod.ModifierId == ChillModifierId)
                chillCopies++;
        }

        if (chillCopies <= 0)
        {
            slowFactor = 1f;
            return false;
        }

        slowFactor = 1f;
        for (int i = 0; i < chillCopies; i++)
            slowFactor *= Balance.SlowSpeedFactor;
        return true;
    }
}
