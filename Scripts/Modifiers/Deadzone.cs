using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Deadzone behavior is orchestrated by DamageModel + CombatSim:
/// primary hits seed a short-lived spatial trap at the impact point;
/// the first enemy to cross into the armed zone is pinned (speed = 0) for
/// Balance.DeadzonePinDuration seconds, then resumes. Zone collapses on trigger.
///
/// Structural guardrails (all enforced structurally, not flag-based):
///   - one active zone per tower (new placement overwrites old)
///   - primary hits only (IsChain check in DamageModel + ApplyToChainTargets=false)
///   - arm time + enemy snapshot prevents same-frame trigger from the hit enemy itself
///   - zone expires after DeadzoneLifetime seconds if uncrossed (not a permanent hazard)
/// </summary>
public class Deadzone : Modifier
{
    public Deadzone(ModifierDef def) { ModifierId = def.Id; }

    // Chain bounces / split secondaries do not plant zones.
    public override bool ApplyToChainTargets => false;
}
