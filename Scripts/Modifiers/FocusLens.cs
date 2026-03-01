using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>+100% damage, +80% attack interval (slower attack speed, net DPS gain ~11%).</summary>
public class FocusLens : Modifier
{
    public FocusLens(ModifierDef def) { ModifierId = def.Id; }

    public override void ModifyDamage(ref float damage, DamageContext ctx)
    {
        damage *= 2f;
        Godot.GD.Print($"  [FocusLens] ×2 damage → {damage:F1}");
    }

    public override void ModifyAttackInterval(ref float interval, TowerInstance tower) =>
        interval *= 1.8f;
}
