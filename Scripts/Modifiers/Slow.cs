using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 80% movement speed for 5 seconds. Multiple Chill Shots on the same tower stack multiplicatively.</summary>
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

        // Calculate stacked speed factor: 0.80^slowCount
        // 1 slow: 0.80 (−20%)
        // 2 slows: 0.64 (−36%)
        // 3 slows: 0.512 (−48.8%)
        float stackedFactor = Core.Balance.SlowSpeedFactor;
        for (int i = 1; i < slowCount; i++)
            stackedFactor *= Core.Balance.SlowSpeedFactor;

        Statuses.ApplySlow(ctx.Target, Core.Balance.SlowDuration, stackedFactor);
    }
}
