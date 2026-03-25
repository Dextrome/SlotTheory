using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Wildfire: valid hits ignite enemies. Burning enemies leave short-lived fire trail segments
/// behind them as they move; enemies walking through those trail segments take damage.
///
/// Triggering rules (mirrors BlastCore -- consistent player mental model):
///   IGNITES on:  primary hits, split-projectile hits, mine primary triggers, Accordion primary
///                (all contexts where ctx.IsChain == false)
///   SKIPS on:    chain bounces (IsChain=true), Accordion Engine secondary hits (IsChain=true)
///
/// Stacking copies of Wildfire:
///   Each additional copy on the same tower increases BurnDPS additively.
///   Duration always refreshes to full on any valid hit.
///
/// Burn DPS formula:
///   burnDps = tower.BaseDamage × Balance.WildfireBurnDpsRatio × (copy count)
///   Intentionally does NOT scale with FinalDamage so Focus Lens / Momentum don't
///   create runaway burn values. The exciting scaling comes from choosing which tower
///   gets Wildfire, not from stacking damage amplifiers.
///
/// Anti-recursion:
///   - Burn DOT and trail damage are raw HP reduction in CombatSim (no modifier pipeline).
///   - Only Wildfire.OnHit() writes BurnRemaining; OnHit is called only by DamageModel
///     from direct tower attack hits.
///   - Trail damage does NOT re-apply Burning. Structural guarantee: trail damage bypasses
///     OnHit modifier dispatch entirely (consistent with BlastCore splash rules).
///
/// Key tower interaction rules:
///   - Rapid Shooter:  high ignition cadence, rapid trail painting. Hair Trigger stacks
///     amplify lane coverage. Both are intentional and fun; trail cap prevents blowup.
///   - Heavy Cannon:   powerful burn per-hit due to high BaseDamage. Trail segments from
///     heavy hits persist as dangerous hazard zones.
///   - Arc Emitter:    primary hit ignites, chain bounces do not. Chains clean up burning
///     enemies, which synergizes naturally (burning pack gets chained through).
///   - Accordion Engine: primary hit (isChain=false) ignites. Secondary compression hits
///     (isChain=true) do not -- compression is an area effect, not a direct strike.
///   - Rift Sapper:    mine primary trigger (isChain=false) ignites. Blast zone secondary
///     targets (isChain=true) do not.
///   - Chill Shot:     slowed enemies linger inside trail segments longer -- naturally strong
///     synergy without any special case code.
///   - Blast Core:     splash damage is raw HP reduction, bypasses OnHit → no Burning from
///     splash. Only the primary hit that triggered the splash applies Burning.
///   - Split Shot:     each split projectile uses isChain=false, so each can apply/refresh
///     Burning independently. Multi-target ignition is the intended behavior.
///
/// CombatSim (not this class) handles all timer advancement and damage application.
/// Wildfire.OnHit() only writes burn state onto the target and returns true to show the proc halo.
/// </summary>
public class Wildfire : Modifier
{
    public Wildfire(ModifierDef def) { ModifierId = def.Id; }

    // Chain bounces don't ignite. Consistent with BlastCore.
    public override bool ApplyToChainTargets => false;

    public override bool OnHit(DamageContext ctx)
    {
        // ApplyToChainTargets=false already blocks chain calls in DamageModel;
        // this guard covers direct invocations from test harnesses.
        if (ctx.IsChain) return false;
        if (ctx.Target.Hp <= 0f) return false; // don't ignite already-dead enemies

        // Count copies of Wildfire on this tower so stacking increases burn DPS.
        int copies = 0;
        foreach (var m in ctx.Attacker.Modifiers)
            if (m.ModifierId == ModifierId) copies++;
        copies = System.Math.Max(1, copies);

        // Burn DPS scales with tower identity, not modifier-amplified FinalDamage.
        float burnDps = ctx.Attacker.BaseDamage * Balance.WildfireBurnDpsRatio * copies;
        if (burnDps <= 0f) return false;

        // Find slot index for RunState damage attribution.
        int slotIndex = FindTowerSlotIndex(ctx.State, ctx.Attacker);

        // Apply burn state: refresh duration and overwrite DPS (last write wins).
        // Only initialize the drop timer on first ignition -- re-ignition while already
        // burning lets the existing timer continue, so a fast-firing tower (Rapid Shooter
        // at 0.45 s) can't perpetually reset a 0.65 s timer and prevent trails from ever dropping.
        bool wasAlreadyBurning = ctx.Target.BurnRemaining > 0f;
        ctx.Target.BurnRemaining = Balance.WildfireBurnDuration;
        ctx.Target.BurnDamagePerSecond = burnDps;
        ctx.Target.BurnOwnerSlotIndex = slotIndex;
        if (!wasAlreadyBurning)
            ctx.Target.BurnTrailDropTimer = Balance.WildfireTrailDropInterval;

        GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
            SpectacleDefinitions.GetProcScalar(ModifierId), burnDps * 0.5f);

        return true; // show the tower proc halo
    }

    /// <summary>
    /// Mirrors DamageModel.FindTowerSlotIndex for burn DOT attribution.
    /// Returns -1 if the tower is not found; TrackBaseAttackDamage accepts -1 safely.
    /// </summary>
    private static int FindTowerSlotIndex(SlotTheory.Core.RunState? state, ITowerView tower)
    {
        if (state == null) return -1;
        for (int i = 0; i < state.Slots.Length; i++)
            if (ReferenceEquals(state.Slots[i].Tower, tower)) return i;
        return -1;
    }
}
