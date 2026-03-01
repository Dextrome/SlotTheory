using System.Linq;
using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>Excess damage from a kill spills to the next enemy in lane (one spill only).</summary>
public class Overkill : Modifier
{
    public Overkill(ModifierDef def) { ModifierId = def.Id; }

    public override void OnKill(DamageContext ctx)
    {
        // Target.Hp is negative after kill — excess = how much we overkilled by
        float excess = -ctx.Target.Hp;
        if (excess <= 0f) return;

        var next = ctx.EnemiesAlive
            .Where(e => e.Hp > 0f)
            .MaxBy(e => e.ProgressRatio); // spill to furthest surviving enemy

        if (next != null)
            next.Hp -= excess;
    }
}
