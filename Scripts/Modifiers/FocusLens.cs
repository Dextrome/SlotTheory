using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+150% damage, ×2 attack interval. Big hits, slow fire — ideal for Overkill combos.</summary>
public class FocusLens : Modifier
{
    public FocusLens(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        damage *= SlotTheory.Core.Balance.FocusLensDamageBonus;
    }

    public override void ModifyAttackInterval(ref float interval, TowerInstance tower) =>
        interval *= SlotTheory.Core.Balance.FocusLensAttackInterval;
}
