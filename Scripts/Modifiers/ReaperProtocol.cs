using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>
/// Kill (primary only): the first <see cref="Balance.ReaperProtocolKillCap"/> kills per wave
/// by this tower each restore 1 life (capped at MaxLives). Available from wave 10 onward.
/// Full game only -- excluded from demo builds and demo bot simulations.
///
/// Counter resets automatically at wave boundary (tracked via WaveIndex in DamageContext).
/// IsChain kills (chain bounces, Blast Core splash, Wildfire burn/trail, mine blasts) are ignored.
/// </summary>
public class ReaperProtocol : Modifier
{
    private int _killsThisWave = 0;
    private int _lastWaveIndex = -1;  // -1 sentinel triggers first-wave reset

    public ReaperProtocol(ModifierDef def) { ModifierId = def.Id; }

    public override bool OnKill(DamageContext ctx)
    {
        // Reset per-wave counter on wave transition
        if (ctx.WaveIndex != _lastWaveIndex)
        {
            _killsThisWave = 0;
            _lastWaveIndex = ctx.WaveIndex;
        }

        // Only credit primary kills.
        // Chain bounces, Blast Core splash, Wildfire burn/trail DOT, mine explosions,
        // and Accordion Engine secondary hits all arrive with IsChain=true.
        if (ctx.IsChain) return false;

        // Per-instance, per-wave cap (each modifier copy tracks independently;
        // the expected use case is a single copy per tower).
        if (_killsThisWave >= Balance.ReaperProtocolKillCap) return false;

        _killsThisWave++;

        // Delegate life gain + UI/VFX/sound to GameController.
        // Null-safe: no-op in unit tests where GameController.Instance is null.
        GameController.Instance?.NotifyReaperProtocolKill(ctx.Attacker);

        return true;  // triggers proc halo on tower slot (DamageModel checks return value)
    }
}
