using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+8% damage per consecutive hit on the same target (max ×1.4). Resets on target switch.</summary>
public class Momentum : Modifier
{
    private int _stacks = 0;
    private ulong _lastTargetInstanceId = 0;

    public Momentum(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        if (_stacks > 0 && _lastTargetInstanceId == ctx.Target.GetInstanceId())
            damage *= (1f + _stacks * Core.Balance.MomentumBonusPerStack);
    }

    public override void OnHit(DamageContext ctx)
    {
        ulong id = ctx.Target.GetInstanceId();
        if (_lastTargetInstanceId == id)
            _stacks = System.Math.Min(_stacks + 1, Core.Balance.MomentumMaxStacks);
        else
        {
            _stacks = 1;
            _lastTargetInstanceId = id;
        }
    }
}
