using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Increments SplitCount by 1. On primary projectile impact, ProjectileVisual spawns
/// SplitCount split projectiles (60% damage each) toward the nearest valid enemies
/// within SplitShotRange. Split projectiles are marked isSplitProjectile=true so they
/// cannot trigger further splits or chain bounces.
/// Stacks: each copy fires one additional split. Natural limiter: needs nearby targets.
/// </summary>
public class SplitShot : Modifier
{
    public SplitShot(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        tower.SplitCount += 1;
    }
}
