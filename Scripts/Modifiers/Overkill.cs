using System.Linq;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>60% of excess damage from a kill spills to the next enemy in lane (one spill only).</summary>
public class Overkill : Modifier
{
    private const float SpillEfficiency = SlotTheory.Core.Balance.OverkillSpillEfficiency;

    public Overkill(ModifierDef def) { ModifierId = def.Id; }

    public override void OnKill(DamageContext ctx)
    {
        float excess = -ctx.Target.Hp;
        if (excess <= 0f) return;

        var next = ctx.EnemiesAlive
            .Where(e => e.Hp > 0f)
            .MaxBy(e => e.ProgressRatio);

        if (next != null)
        {
            float spillDamage = excess * SpillEfficiency;
            next.Hp -= spillDamage;
            float spillRatio = spillDamage / System.MathF.Max(1f, ctx.FinalDamage);
            float scalar = SpectacleDefinitions.OverkillEventScalar(spillRatio);
            GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId, scalar);
            GameController.Instance?.NotifyOverkillSpill(next.GlobalPosition, spillDamage);
        }
    }
}
