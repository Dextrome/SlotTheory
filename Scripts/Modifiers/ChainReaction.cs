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
        bool hadChain = tower.ChainCount > 0;
        tower.ChainCount += 1;
        // Only set decay for towers that didn't already chain. Arc Emitter retains its native
        // decay so equipping chain_reaction doesn't regress its existing bounces.
        if (!hadChain)
            tower.ChainDamageDecay = Core.Balance.ChainReactionDamageDecay;
        if (tower.ChainRange < Core.Balance.ChainReactionRange)
            tower.ChainRange = Core.Balance.ChainReactionRange;
    }
}
