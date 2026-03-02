using System.Collections.Generic;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public class DamageContext
{
    public TowerInstance Attacker { get; }
    public EnemyInstance Target { get; }
    public int WaveIndex { get; }
    public List<EnemyInstance> EnemiesAlive { get; }

    public float BaseDamage { get; }
    public float FinalDamage { get; set; }
    public RunState? State { get; }

    public bool IsChain { get; }

    public DamageContext(TowerInstance attacker, EnemyInstance target, int waveIndex,
                         List<EnemyInstance> enemies, RunState? state = null,
                         bool isChain = false, float damageOverride = -1f)
    {
        Attacker = attacker;
        Target = target;
        WaveIndex = waveIndex;
        EnemiesAlive = enemies;
        IsChain = isChain;
        BaseDamage = FinalDamage = damageOverride >= 0f ? damageOverride : attacker.BaseDamage;
        State = state;
    }
}

public static class DamageModel
{
    public static void Apply(DamageContext ctx)
    {
        float damage = ctx.BaseDamage;

        // 1. Modifier damage pass — skipped for chain bounces (damage already decayed at spawn)
        if (!ctx.IsChain)
            foreach (var mod in ctx.Attacker.Modifiers)
                mod.ModifyDamage(ref damage, ctx);

        // 2. Global Marked bonus — all towers deal +20% to Marked enemies
        if (ctx.Target.IsMarked)
            damage *= (1f + Balance.MarkedDamageBonus);

        ctx.FinalDamage = damage;

        // 3. Apply damage; track run-wide stats when RunState is available
        float hpBefore = ctx.Target.Hp;
        ctx.Target.Hp -= damage;
        if (ctx.State != null)
        {
            ctx.State.TotalDamageDealt += (int)(hpBefore - System.MathF.Max(0f, ctx.Target.Hp));
            if (ctx.Target.Hp <= 0) ctx.State.TotalKills++;
        }

        // 4. On-hit effects
        foreach (var mod in ctx.Attacker.Modifiers)
            mod.OnHit(ctx);

        if (ctx.Attacker.AppliesMark)
            Statuses.ApplyMarked(ctx.Target, Balance.MarkedDuration);

        // 5. On-kill effects
        if (ctx.Target.Hp <= 0)
            foreach (var mod in ctx.Attacker.Modifiers)
                mod.OnKill(ctx);
    }
}
