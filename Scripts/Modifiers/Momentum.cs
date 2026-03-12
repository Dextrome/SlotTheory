using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+16% damage per consecutive hit on the same target (max ×1.80). Resets on target switch. Stacks carry through chain/split sequences.</summary>
public class Momentum : Modifier
{
    private int _stacks = 0;
    private Entities.IEnemyView? _lastTarget = null;

    public Momentum(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        // Apply bonus if hitting same target, or continuing a chain/split sequence
        bool sameTarget = ReferenceEquals(_lastTarget, ctx.Target);
        bool inChainSequence = ctx.IsChain && _stacks > 0;

        if (sameTarget || inChainSequence)
            damage *= (1f + _stacks * Core.Balance.MomentumBonusPerStack);
    }

    public override bool OnHit(DamageContext ctx)
    {
        bool sameTarget = ReferenceEquals(_lastTarget, ctx.Target);

        if (sameTarget || ctx.IsChain)
        {
            _stacks = System.Math.Min(_stacks + 1, Core.Balance.MomentumMaxStacks);
        }
        else
        {
            _stacks = 1;
        }

        if (!ctx.IsChain)
            _lastTarget = ctx.Target;

        float stackNorm = _stacks / (float)System.Math.Max(1, Core.Balance.MomentumMaxStacks);
        float scalar = Core.SpectacleDefinitions.MomentumEventScalar(stackNorm);
        Core.GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId, scalar, ctx.DamageDealt);

        // Only show proc visual when stacks are actively building (not on a reset to 1)
        return sameTarget || ctx.IsChain;
    }
}
