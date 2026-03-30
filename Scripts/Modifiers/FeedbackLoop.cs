using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Kill: resets 50% of remaining cooldown per copy equipped (1 copy = 50%, 2 copies = 100%).
/// Also grants a +20% attack speed stim for Balance.FeedbackLoopStimDuration seconds (stim refreshes on each kill).
/// Cooldown reset is applied once using the combined reduction; stim timer is per-instance.
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
        int copies = 0;
        foreach (var m in ctx.Attacker.Modifiers)
            if (m.ModifierId == ModifierId) copies++;
        float reduction = System.MathF.Min(1f, copies * Balance.FeedbackLoopCooldownReductionPerCopy);

        float preCooldown = ctx.Attacker.Cooldown;
        ctx.Attacker.Cooldown = System.MathF.Max(
            0f,
            ctx.Attacker.Cooldown * (1f - reduction)
        );

        _stimRemaining = Balance.FeedbackLoopStimDuration;

        if (preCooldown > 0.001f)
            GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
                SpectacleDefinitions.GetProcScalar(ModifierId), ctx.DamageDealt);
        GameController.Instance?.NotifyFeedbackLoopProc(ctx.Attacker);
        return true;
    }
}
