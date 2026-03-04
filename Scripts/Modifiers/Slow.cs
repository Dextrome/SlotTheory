using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 75% movement speed for 5 seconds. Multiple Chill Shots on the same tower stack multiplicatively.</summary>
public class Slow : Modifier
{
    public Slow(ModifierDef def) { ModifierId = def.Id; }

    public override void OnHit(DamageContext ctx)
    {
        // Count how many "slow" modifiers are on this tower
        int slowCount = 0;
        foreach (var mod in ctx.Attacker.Modifiers)
            if (mod.ModifierId == "slow")
                slowCount++;

        // Calculate stacked speed factor: 0.75^slowCount
        // 1 slow: 0.75 (−25%)
        // 2 slows: 0.5625 (−43.75%)
        // 3 slows: 0.4219 (−57.81%)
        float stackedFactor = Core.Balance.SlowSpeedFactor;
        for (int i = 1; i < slowCount; i++)
            stackedFactor *= Core.Balance.SlowSpeedFactor;

        Statuses.ApplySlow(ctx.Target, Core.Balance.SlowDuration, stackedFactor);
    }
}
