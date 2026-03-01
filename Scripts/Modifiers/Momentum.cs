using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+10% damage per consecutive hit on the same target. Resets on target switch.</summary>
public class Momentum : Modifier
{
    private const float BonusPerStack = 0.10f;
    private int _stacks = 0;
    private ulong _lastTargetInstanceId = 0;

    public Momentum(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        if (_stacks > 0 && _lastTargetInstanceId == ctx.Target.GetInstanceId())
        {
            damage *= (1f + _stacks * BonusPerStack);
            Godot.GD.Print($"  [Momentum] ×{1f + _stacks * BonusPerStack:F2} ({_stacks} stacks)");
        }
    }

    public override void OnHit(DamageContext ctx)
    {
        ulong id = ctx.Target.GetInstanceId();
        if (_lastTargetInstanceId == id)
            _stacks++;
        else
        {
            _stacks = 1;
            _lastTargetInstanceId = id;
        }
    }
}
