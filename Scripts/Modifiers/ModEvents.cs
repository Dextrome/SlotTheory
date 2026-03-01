using System;
using SlotTheory.Combat;

namespace SlotTheory.Modifiers;

/// <summary>Optional global event bus for cross-modifier communication. Reset between runs.</summary>
public static class ModEvents
{
    public static event Action<DamageContext>? OnAnyHit;
    public static event Action<DamageContext>? OnAnyKill;

    public static void RaiseHit(DamageContext ctx) => OnAnyHit?.Invoke(ctx);
    public static void RaiseKill(DamageContext ctx) => OnAnyKill?.Invoke(ctx);

    public static void Reset()
    {
        OnAnyHit = null;
        OnAnyKill = null;
    }
}
