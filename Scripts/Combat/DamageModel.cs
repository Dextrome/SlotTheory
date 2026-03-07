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

        // 1. Modifier damage pass — applies to all hits (primary, chain, split)
        foreach (var mod in ctx.Attacker.Modifiers)
        {
            float before = damage;
            mod.ModifyDamage(ref damage, ctx);
            if (System.MathF.Abs(damage - before) > 0.001f)
                GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
        }

        // 2. Global Marked bonus — all towers deal +20% to Marked enemies
        if (ctx.Target.IsMarked)
            damage *= (1f + Balance.MarkedDamageBonus);

        ctx.FinalDamage = damage;

        // 3. Apply damage; track run-wide and per-tower stats when RunState is available
        float hpBefore = ctx.Target.Hp;
        ctx.Target.Hp -= damage;
        if (ctx.State != null)
        {
            int damageDealt = (int)(hpBefore - System.MathF.Max(0f, ctx.Target.Hp));
            ctx.State.TotalDamageDealt += damageDealt;
            
            // Track damage for the specific tower for micro reports
            var attackerSlotIndex = FindTowerSlotIndex(ctx.State, ctx.Attacker);
            if (attackerSlotIndex >= 0)
            {
                ctx.State.TrackTowerDamage(attackerSlotIndex, damageDealt);
            }

            if (ctx.Target.Hp <= 0) 
            {
                ctx.State.TotalKills++;
                // Track kill for the specific tower
                if (attackerSlotIndex >= 0)
                {
                    ctx.State.TrackTowerKill(attackerSlotIndex);
                }
            }
        }

        // 4. On-hit effects (skipped for chain bounces if modifier opts out)
        foreach (var mod in ctx.Attacker.Modifiers)
        {
            if (!ctx.IsChain || mod.ApplyToChainTargets)
            {
                if (mod.OnHit(ctx))
                    GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
            }
        }

        if (ctx.Attacker.AppliesMark)
            Statuses.ApplyMarked(ctx.Target, Balance.MarkedDuration);

        // 5. On-kill effects (run for all kills regardless of bounce type)
        if (ctx.Target.Hp <= 0)
        {
            foreach (var mod in ctx.Attacker.Modifiers)
            {
                mod.OnKill(ctx);
                GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
            }
        }
    }

    /// <summary>Finds which slot index a tower belongs to for damage tracking.</summary>
    private static int FindTowerSlotIndex(RunState state, TowerInstance tower)
    {
        for (int i = 0; i < state.Slots.Length; i++)
        {
            if (state.Slots[i].Tower == tower)
                return i;
        }
        return -1; // Not found (should not happen in normal play)
    }
}
