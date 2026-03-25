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

    public override bool OnKill(DamageContext ctx)
    {
        // isChain marks spill-sourced hits - skip to prevent recursive spill chains.
        if (ctx.IsChain) return false;

        float excess = -ctx.Target.Hp;
        if (excess <= 0f) return false;

        var next = ctx.EnemiesAlive
            .Where(e => e.Hp > 0f)
            .MaxBy(e => e.ProgressRatio);

        if (next == null) return false;

        // Make spill damage scale with number of Overkill copies
        int overkillCount = ctx.Attacker.Modifiers.Count(m => m.ModifierId == "overkill");
        float spillDamage = excess * SpillEfficiency * overkillCount;

        // Apply spill as raw damage (no ModifyDamage, no Marked/DamageAmp bonuses, no OnHit side effects)
        // so the spill amount stays exactly excess * SpillEfficiency and doesn't inherit modifier multipliers.
        bool wasMarked = next.IsMarked;
        float hpBefore = next.Hp;
        next.Hp = System.MathF.Max(0f, next.Hp - spillDamage);
        float dealt = hpBefore - next.Hp;
        bool isKill = next.Hp <= 0f;

        if (isKill)
        {
            if (wasMarked)
                GameController.Instance?.NotifyMarkedEnemyPop(ctx.Attacker, next, ctx.EnemiesAlive);
            // Fire OnKill for other modifiers (e.g. FeedbackLoop) so kill effects aren't silently skipped.
            // isChain: true prevents this Overkill instance from recursing.
            var killCtx = new DamageContext(ctx.Attacker, next, ctx.WaveIndex, ctx.EnemiesAlive, ctx.State,
                                             isChain: true, damageOverride: spillDamage);
            killCtx.DamageDealt = dealt;
            foreach (var mod in ctx.Attacker.Modifiers)
                if (mod.OnKill(killCtx))
                    GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
        }

        ctx.OverkillSpillDealt += dealt;

        GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
            SpectacleDefinitions.GetProcScalar(ModifierId), dealt);
        GameController.Instance?.NotifyOverkillSpill(ctx.Attacker, next.GlobalPosition, spillDamage, dealt);
        return true;
    }
}
