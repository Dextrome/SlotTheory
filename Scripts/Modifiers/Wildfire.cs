using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Wildfire: valid hits ignite enemies. Burning enemies leave short-lived fire trail segments
/// behind them as they move; enemies walking through those trail segments take damage.
///
/// Triggering rules:
///   IGNITES on:  all hits -- primary, chain bounces, split-projectile hits, mine triggers,
///                Accordion primary and secondary hits. Proc spaghetti is intentional.
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
///   - Arc Emitter:    primary hit and all chain bounces ignite. Entire chain spreads fire.
///   - Accordion Engine: primary and secondary compression hits both ignite.
///   - Rift Sapper:    mine primary and secondary blast targets all ignite.
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

    public override bool OnHit(DamageContext ctx)
    {
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
        int slotIndex = ctx.Attacker.SlotIndex;

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

}
