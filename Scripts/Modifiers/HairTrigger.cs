using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>Fires 1.40× faster, −18% range. Close-quarters rapid-fire.</summary>
public class HairTrigger : Modifier
{
    public HairTrigger(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(ITowerView tower)
    {
        tower.AttackInterval *= 1f / SlotTheory.Core.Balance.HairTriggerAttackSpeed;
        tower.Range          *= SlotTheory.Core.Balance.HairTriggerRangeFactor;
        tower.RefreshRangeCircle();
    }
}
