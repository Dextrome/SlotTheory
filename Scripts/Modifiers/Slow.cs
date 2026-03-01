using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 70% movement speed for 5 seconds.</summary>
public class Slow : Modifier
{
    public Slow(ModifierDef def) { ModifierId = def.Id; }

    public override void OnHit(DamageContext ctx) =>
        Statuses.ApplySlow(ctx.Target, Core.Balance.SlowDuration);
}
