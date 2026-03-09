using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+40% range, −20% damage. Wider coverage at damage cost.</summary>
public class Overreach : Modifier
{
    public Overreach(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(ITowerView tower)
    {
        tower.Range      *= 1.40f;
        tower.BaseDamage *= 0.80f;
        tower.RefreshRangeCircle();
    }
}
