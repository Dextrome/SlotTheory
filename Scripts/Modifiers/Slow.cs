using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 70% movement speed for 6 seconds. Multiple Chill Shots on the same tower stack multiplicatively.</summary>
public class Slow : Modifier
{
    public Slow(ModifierDef def) { ModifierId = def.Id; }

    public override bool OnHit(DamageContext ctx)
    {
        // Count how many "slow" modifiers are on this tower
        int slowCount = 0;
        foreach (var mod in ctx.Attacker.Modifiers)
            if (mod.ModifierId == "slow")
                slowCount++;

        // Calculate stacked speed factor: 0.70^slowCount
        // 1 slow: 0.70 (-30%)
        // 2 slows: 0.49 (-51%)
        // 3 slows: 0.343 (-65.7%)
        float stackedFactor = Core.Balance.SlowSpeedFactor;
        for (int i = 1; i < slowCount; i++)
            stackedFactor *= Core.Balance.SlowSpeedFactor;

        Statuses.ApplySlow(ctx.Target, Core.Balance.SlowDuration, stackedFactor);
        Core.GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
            Core.SpectacleDefinitions.GetProcScalar(ModifierId), ctx.DamageDealt);
        return true;
    }
}
