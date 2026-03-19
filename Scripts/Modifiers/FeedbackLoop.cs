using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Killing an enemy fully resets the tower's current cooldown to 0 (Balance.FeedbackLoopCooldownReduction = 1.0).
/// Only Cooldown (the live timer) is touched - AttackInterval (base period) is never changed,
/// so there is no permanent acceleration. Multiple copies all fire on kill but have no additional
/// effect since the first copy already zeroes the cooldown.
/// </summary>
public class FeedbackLoop : Modifier
{
    public FeedbackLoop(ModifierDef def) { ModifierId = def.Id; }

    public override bool OnKill(DamageContext ctx)
    {
        float preCooldown = ctx.Attacker.Cooldown;
        ctx.Attacker.Cooldown = System.MathF.Max(
            0f,
            ctx.Attacker.Cooldown * (1f - Balance.FeedbackLoopCooldownReduction)
        );
        if (preCooldown > 0.001f)
        {
            float refunded = preCooldown - ctx.Attacker.Cooldown;
            float refundFrac = refunded / preCooldown;
            float scalar = SpectacleDefinitions.FeedbackLoopEventScalar(refundFrac);
            GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId, scalar, ctx.DamageDealt);
        }
        GameController.Instance?.NotifyFeedbackLoopProc(ctx.Attacker);
        return true;
    }
}
