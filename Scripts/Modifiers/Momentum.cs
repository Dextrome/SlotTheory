using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+16% damage per consecutive hit on the same target (max ×1.80). Resets on target switch. Stacks carry through chain/split sequences.</summary>
public class Momentum : Modifier
{
    private int _stacks = 0;
    private ulong _lastTargetInstanceId = 0;

    public Momentum(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        // Apply bonus if hitting same target, or continuing a chain/split sequence
        ulong targetId = ctx.Target.GetInstanceId();
        bool sameTarget = _lastTargetInstanceId == targetId;
        bool inChainSequence = ctx.IsChain && _stacks > 0;

        if (sameTarget || inChainSequence)
            damage *= (1f + _stacks * Core.Balance.MomentumBonusPerStack);
    }

    public override void OnHit(DamageContext ctx)
    {
        ulong id = ctx.Target.GetInstanceId();
        bool sameTarget = _lastTargetInstanceId == id;

        if (sameTarget || ctx.IsChain)
        {
            // Increment stacks if same target or in a chain/split sequence
            _stacks = System.Math.Min(_stacks + 1, Core.Balance.MomentumMaxStacks);
        }
        else
        {
            // Reset to 1 stack for a new primary target
            _stacks = 1;
        }

        // Track the last target ID (important for non-chain hits)
        if (!ctx.IsChain)
            _lastTargetInstanceId = id;
    }
}
