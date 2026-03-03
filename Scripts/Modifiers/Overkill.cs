using System.Linq;
using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>80% of excess damage from a kill spills to the next enemy in lane (one spill only).</summary>
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
            next.Hp -= excess * SpillEfficiency;
    }
}
