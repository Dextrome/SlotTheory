using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Kill: instantly resets cooldown to 0 AND grants a +20% attack speed stim for
/// Balance.FeedbackLoopStimDuration seconds (stim refreshes on each kill).
/// Multiple copies all fire OnKill but the stim timer is per-instance, so stacking
/// extends nothing beyond refreshing the same 4s window.
/// </summary>
public class FeedbackLoop : Modifier
{
    private float _stimRemaining = 0f;

    public FeedbackLoop(ModifierDef def) { ModifierId = def.Id; }

    public override void Update(float dt, ITowerView tower)
    {
        if (_stimRemaining > 0f)
            _stimRemaining = System.MathF.Max(0f, _stimRemaining - dt);
    }

    public override void ModifyAttackInterval(ref float interval, ITowerView tower)
    {
        if (_stimRemaining > 0f)
            interval *= Balance.FeedbackLoopStimFactor;
    }

    public override bool OnKill(DamageContext ctx)
    {
        float preCooldown = ctx.Attacker.Cooldown;
        ctx.Attacker.Cooldown = System.MathF.Max(
            0f,
            ctx.Attacker.Cooldown * (1f - Balance.FeedbackLoopCooldownReduction)
        );

        _stimRemaining = Balance.FeedbackLoopStimDuration;

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
