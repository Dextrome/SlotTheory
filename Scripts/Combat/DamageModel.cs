using System.Collections.Generic;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public class DamageContext
{
    public ITowerView Attacker { get; }
    public IEnemyView Target { get; }
    public int WaveIndex { get; }
    public IEnumerable<IEnemyView> EnemiesAlive { get; }

    public float BaseDamage { get; }
    public float FinalDamage { get; set; }
    public RunState? State { get; }

    public bool IsChain { get; }
    public float DamageDealt { get; set; }
    // Populated by modifiers that deal secondary area damage outside the normal damage pipeline (e.g. Blast Core splash).
    // Tracked separately so benchmark runners can include it in total damage metrics.
    public float SplashDamageDealt { get; set; }
    // Populated by Overkill when excess damage is spilled to the next enemy.
    // Tracked separately so the benchmark counts spill kills -- otherwise overkill looks negative
    // (chain-kills reduce shot count → less tracked direct damage, masking the spill benefit).
    public float OverkillSpillDealt { get; set; }

    public DamageContext(ITowerView attacker, IEnemyView target, int waveIndex,
                         IEnumerable<IEnemyView> enemies, RunState? state = null,
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
        bool wasMarkedBeforeHit = ctx.Target.IsMarked;

        // 1. Modifier damage pass - applies to all hits (primary, chain, split)
        foreach (var mod in ctx.Attacker.Modifiers)
        {
            float before = damage;
            mod.ModifyDamage(ref damage, ctx);
            if (System.MathF.Abs(damage - before) > 0.001f)
                GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
        }

        // 2. Global Marked bonus - all towers deal +40% to Marked enemies
        if (ctx.Target.IsMarked)
            damage *= (1f + Balance.MarkedDamageBonus);
        if (ctx.Target.DamageAmpRemaining > 0f && ctx.Target.DamageAmpMultiplier > 0f)
            damage *= (1f + ctx.Target.DamageAmpMultiplier);

        // Shield Drone protection - 35% damage reduction for allies within aura radius
        if (ctx.Target.IsShieldProtected)
            damage *= (1f - Balance.ShieldDroneProtectionReduction);

        ctx.FinalDamage = damage;

        // 3. Apply damage; track run-wide and per-tower stats when RunState is available
        float hpBefore = ctx.Target.Hp;
        ctx.Target.Hp -= damage;
        float damageDealtRaw = hpBefore - System.MathF.Max(0f, ctx.Target.Hp);
        ctx.DamageDealt = damageDealtRaw;
        bool isKill = ctx.Target.Hp <= 0f;

        if (damageDealtRaw > 0f && ctx.Target is EnemyInstance runtimeEnemy)
        {
            if (runtimeEnemy.TryTriggerReverseJump(damageDealtRaw))
            {
                float hpRatio = damageDealtRaw / System.MathF.Max(1f, runtimeEnemy.MaxHp);
                float pitch = 1f + System.MathF.Min(0.22f, hpRatio * 0.30f);
                SoundManager.Instance?.Play("enemy_rewind", pitchScale: pitch);
            }
        }

        if (ctx.State != null)
        {
            int damageDealt = (int)(hpBefore - System.MathF.Max(0f, ctx.Target.Hp));
            var attackerSlotIndex = FindTowerSlotIndex(ctx.State, ctx.Attacker);
            ctx.State.TrackBaseAttackDamage(attackerSlotIndex, damageDealt, isKill, ctx.Target.ProgressRatio);
        }

        RegisterSpectacleDamageProcs(ctx, damageDealtRaw, isKill);

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
            if (wasMarkedBeforeHit)
                GameController.Instance?.NotifyMarkedEnemyPop(ctx.Attacker, ctx.Target, ctx.EnemiesAlive);
            foreach (var mod in ctx.Attacker.Modifiers)
            {
                if (mod.OnKill(ctx))
                    GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
            }
        }
    }

    private static void RegisterSpectacleDamageProcs(DamageContext ctx, float damageDealt, bool isKill)
    {
        var gc = GameController.Instance;
        if (gc == null)
            return;

        if (CountModifier(ctx.Attacker, SpectacleDefinitions.ExploitWeakness) > 0 && ctx.Target.IsMarked)
        {
            float scalar = SpectacleDefinitions.ExploitWeaknessEventScalar(markedHit: true, markedKill: isKill);
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.ExploitWeakness, scalar, damageDealt);
        }

        if (CountModifier(ctx.Attacker, SpectacleDefinitions.FocusLens) > 0)
        {
            float baseShotDamage = System.MathF.Max(1f, ctx.Attacker.BaseDamage);
            float damageNorm = System.MathF.Max(0f, damageDealt) / baseShotDamage;
            float scalar = SpectacleDefinitions.FocusLensEventScalar(damageNorm);
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.FocusLens, scalar, damageDealt);
        }

        int overreachCopies = CountModifier(ctx.Attacker, SpectacleDefinitions.Overreach);
        if (overreachCopies > 0)
        {
            float overreachFactor = System.MathF.Pow(Balance.OverreachRangeFactor, overreachCopies);
            float baseRange = ctx.Attacker.Range / System.MathF.Max(0.001f, overreachFactor);
            float rangeNorm = (ctx.Attacker.Range / System.MathF.Max(1f, baseRange)) - 1f;
            float scalar = SpectacleDefinitions.OverreachEventScalar(rangeNorm);
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.Overreach, scalar, damageDealt);
        }
    }

    private static int CountModifier(ITowerView tower, string modifierId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modifierId);
        int count = 0;
        foreach (var mod in tower.Modifiers)
        {
            if (SpectacleDefinitions.NormalizeModId(mod.ModifierId) == normalized)
                count++;
        }
        return count;
    }

    /// <summary>Finds which slot index a tower belongs to for damage tracking.</summary>
    private static int FindTowerSlotIndex(RunState state, ITowerView tower)
    {
        for (int i = 0; i < state.Slots.Length; i++)
        {
            if (ReferenceEquals(state.Slots[i].Tower, tower))
                return i;
        }
        return -1;
    }
}
