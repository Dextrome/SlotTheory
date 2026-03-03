using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>−33% attack interval (fires 1.5× faster), −30% range. Close-quarters rapid-fire.</summary>
public class HairTrigger : Modifier
{
    public HairTrigger(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        tower.AttackInterval *= 1f / SlotTheory.Core.Balance.HairTriggerAttackSpeed;
        tower.Range          *= SlotTheory.Core.Balance.HairTriggerRangeFactor;
        tower.RefreshRangeCircle();
    }
}
