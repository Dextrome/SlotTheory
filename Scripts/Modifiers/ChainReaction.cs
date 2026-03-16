using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Adds 1 chain bounce to the tower. IsChainTower is computed as ChainCount > 0,
/// so this activates the full existing chain infrastructure (ProjectileVisual.ApplyChainHits,
/// SpawnChainArc visual, CombatSim.ApplyChainBotMode) with no extra wiring.
/// Default ChainRange=260, ChainDamageDecay=0.60 apply automatically on non-chain towers.
/// Stacks: each copy adds 1 more bounce. Damage decays 60% per hop - diminishing by design.
/// </summary>
public class ChainReaction : Modifier
{
    public ChainReaction(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(ITowerView tower)
    {
        tower.ChainCount += 1;
        tower.ChainDamageDecay = Core.Balance.ChainDamageDecay;
        if (tower.ChainRange < 400f)
            tower.ChainRange = 400f;
    }
}
