using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Killing an enemy reduces the tower's current cooldown by 30%.
/// Only Cooldown (the live timer) is touched — AttackInterval (base period) is never changed,
/// so there is no permanent acceleration. Each copy fires independently on kill,
/// giving multiplicative diminishing returns (×0.7 per copy, never below 0).
/// </summary>
public class FeedbackLoop : Modifier
{
    public FeedbackLoop(ModifierDef def) { ModifierId = def.Id; }

    public override void OnKill(DamageContext ctx)
    {
        ctx.Attacker.Cooldown = System.MathF.Max(
            0f,
            ctx.Attacker.Cooldown * (1f - Balance.FeedbackLoopCooldownReduction)
        );
    }
}
