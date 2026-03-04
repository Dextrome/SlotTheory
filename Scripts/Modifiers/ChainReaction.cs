using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Adds 1 chain bounce to the tower. IsChainTower is computed as ChainCount > 0,
/// so this activates the full existing chain infrastructure (ProjectileVisual.ApplyChainHits,
/// SpawnChainArc visual, CombatSim.ApplyChainBotMode) with no extra wiring.
/// Default ChainRange=260, ChainDamageDecay=0.55 apply automatically on non-chain towers.
/// Stacks: each copy adds 1 more bounce. Damage decays 55% per hop — diminishing by design.
/// </summary>
public class ChainReaction : Modifier
{
    public ChainReaction(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        tower.ChainCount += 1;
        tower.ChainDamageDecay = 0.55f; // Nerf from 60% to 55% per bounce
    }
}
