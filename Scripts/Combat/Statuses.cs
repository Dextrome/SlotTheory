using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

/// <summary>Helpers for applying status effects. Marked state is stored directly on EnemyInstance.</summary>
public static class Statuses
{
    /// <summary>Apply or refresh Marked. Duration resets on reapplication.</summary>
    public static void ApplyMarked(EnemyInstance target, float duration) =>
        target.MarkedRemaining = duration;
}
