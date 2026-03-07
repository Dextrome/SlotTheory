using SlotTheory.Combat;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>Base class for all tower modifiers. Override only what you need.</summary>
public abstract class Modifier
{
    public string ModifierId { get; protected set; } = string.Empty;
    /// <summary>If false, OnHit is skipped for chain/split targets. Defaults to true.</summary>
    public virtual bool ApplyToChainTargets => true;

    /// <summary>Called once when the modifier is equipped to a tower. Use for permanent stat changes.</summary>
    public virtual void OnEquip(TowerInstance tower) { }
    public virtual void ModifyAttackInterval(ref float interval, TowerInstance tower) { }
    public virtual void ModifyDamage(ref float damage, DamageContext ctx) { }
    /// <summary>Called on every hit. Return true if the modifier did something (triggers proc visual).</summary>
    public virtual bool OnHit(DamageContext ctx) => false;
    public virtual void OnKill(DamageContext ctx) { }
}
