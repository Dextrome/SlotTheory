using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+10% damage per consecutive hit on the same target. Resets on target switch.</summary>
public class Momentum : Modifier
{
    private const float BonusPerStack = 0.10f;
    private int _stacks = 0;
    private string? _lastTargetId;

    public Momentum(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        if (_lastTargetId == ctx.Target.EnemyTypeId && _stacks > 0)
            damage *= (1f + _stacks * BonusPerStack);
    }

    public override void OnHit(DamageContext ctx)
    {
        if (_lastTargetId == ctx.Target.EnemyTypeId)
            _stacks++;
        else
        {
            _stacks = 1;
            _lastTargetId = ctx.Target.EnemyTypeId;
        }
    }
}
