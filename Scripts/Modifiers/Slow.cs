using SlotTheory.Combat;
using SlotTheory.Data;

namespace SlotTheory.Modifiers;

/// <summary>On hit, slows the target to 50% movement speed for 10 seconds.</summary>
public class Slow : Modifier
{
    private const float Duration = 10f;

    public Slow(ModifierDef def) { ModifierId = def.Id; }

    public override void OnHit(DamageContext ctx) =>
        Statuses.ApplySlow(ctx.Target, Duration);
}
