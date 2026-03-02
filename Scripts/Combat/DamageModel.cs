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

        // 1. Modifier damage pass (interval is handled in CombatSim, not here)
        foreach (var mod in ctx.Attacker.Modifiers)
            mod.ModifyDamage(ref damage, ctx);

        // 2. Global Marked bonus — all towers deal +20% to Marked enemies
        if (ctx.Target.IsMarked)
            damage *= (1f + Balance.MarkedDamageBonus);

        ctx.FinalDamage = damage;

        // 3. Apply damage
        ctx.Target.Hp -= damage;

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
