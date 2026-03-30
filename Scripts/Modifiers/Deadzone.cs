using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Deadzone behavior is orchestrated by DamageModel + CombatSim:
/// primary hits seed a short-lived spatial trap at the impact point;
/// the first enemy to cross into the zone triggers a reduced follow-up hit,
/// then the zone collapses.
///
/// Structural guardrails (all enforced structurally, not flag-based):
///   - one active zone per tower (new placement overwrites old)
///   - primary hits only (IsChain check in DamageModel + ApplyToChainTargets=false)
///   - triggered follow-up is applied with suppressDeadzoneSeed:true (no recursion)
///   - zone has an arm time before it can fire (prevents same-frame self-trigger)
///   - zone expires after DeadzoneLifetime seconds if uncrossed (not a permanent hazard)
///
/// Tower-specific follow-up expressions:
///   - heavy_cannon / rocket_launcher / rift_prism: small area burst around trigger point
///   - undertow_engine: brief pull + slow from the zone
///   - accordion_engine / phase_splitter: compact area follow-up
///   - all others: single-target reduced damage hit on the crossing enemy
/// </summary>
public class Deadzone : Modifier
{
    public Deadzone(ModifierDef def) { ModifierId = def.Id; }

    // Chain bounces / split secondaries do not plant zones.
    public override bool ApplyToChainTargets => false;
}
