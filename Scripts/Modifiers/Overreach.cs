using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+45% range, -10% damage. Wider coverage with a light damage tradeoff.</summary>
public class Overreach : Modifier
{
    public Overreach(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(ITowerView tower)
    {
        tower.Range      *= Core.Balance.OverreachRangeFactor;
        tower.BaseDamage *= Core.Balance.OverreachDamageFactor;
        tower.RefreshRangeCircle();
    }
}

