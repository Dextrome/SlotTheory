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

    public DamageContext(TowerInstance attacker, EnemyInstance target, int waveIndex, List<EnemyInstance> enemies)
    {
        Attacker = attacker;
        Target = target;
        WaveIndex = waveIndex;
        EnemiesAlive = enemies;
        BaseDamage = FinalDamage = attacker.BaseDamage;
    }
}

public static class DamageModel
{
    public static void Apply(DamageContext ctx)
    {
        float damage = ctx.BaseDamage;

        // 1. Stat modifier pass
        float interval = ctx.Attacker.AttackInterval;
        foreach (var mod in ctx.Attacker.Modifiers)
        {
            mod.ModifyAttackInterval(ref interval, ctx.Attacker);
            mod.ModifyDamage(ref damage, ctx);
        }
        ctx.Attacker.AttackInterval = interval;

        // 2. Global Marked bonus — all towers deal +20% to Marked enemies
        if (ctx.Target.IsMarked)
            damage *= (1f + Balance.MarkedDamageBonus);

        ctx.FinalDamage = damage;

        // 3. Apply damage
        ctx.Target.Hp -= damage;

        // 4. On-hit effects (e.g., apply Marked, Momentum tracking)
        foreach (var mod in ctx.Attacker.Modifiers)
            mod.OnHit(ctx);

        // Apply Marked from Marker Tower
        if (ctx.Attacker.AppliesMark)
            Statuses.ApplyMarked(ctx.Target, Balance.MarkedDuration);

        // 5. On-kill effects (e.g., Overkill spill)
        if (ctx.Target.Hp <= 0)
            foreach (var mod in ctx.Attacker.Modifiers)
                mod.OnKill(ctx);

        ctx.Attacker.LastTargetId = ctx.Target.EnemyTypeId;
    }
}
