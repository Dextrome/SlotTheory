using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+100% damage, ×2 attack interval. Big hits, slow fire — ideal for Overkill combos.</summary>
public class FocusLens : Modifier
{
    public FocusLens(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        damage *= 2.0f;
    }

    public override void ModifyAttackInterval(ref float interval, TowerInstance tower) =>
        interval *= 2f;
}
