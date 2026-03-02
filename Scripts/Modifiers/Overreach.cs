using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+50% range, −15% damage. Wider coverage at a small damage cost.</summary>
public class Overreach : Modifier
{
    public Overreach(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        tower.Range      *= 1.50f;
        tower.BaseDamage *= 0.85f;
        tower.RefreshRangeCircle();
    }
}
