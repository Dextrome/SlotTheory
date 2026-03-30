using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 70% movement speed for 6 seconds. Multiple Chill Shots on the same tower stack multiplicatively.</summary>
public class Slow : Modifier
{
    public Slow(ModifierDef def) { ModifierId = def.Id; }

    public override bool OnHit(DamageContext ctx)
    {
        if (!Statuses.TryApplyChillFromAttacker(ctx.Attacker, ctx.Target, ctx.State))
            return false;

        Core.GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
            Core.SpectacleDefinitions.GetProcScalar(ModifierId), ctx.DamageDealt);
        return true;
    }
}
