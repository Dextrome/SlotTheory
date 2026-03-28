using System;
using System.Collections.Generic;
using Godot;
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
    public bool SuppressAfterimageSeed { get; }
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
                         bool isChain = false, float damageOverride = -1f, bool suppressAfterimageSeed = false)
    {
        Attacker = attacker;
        Target = target;
        WaveIndex = waveIndex;
        EnemiesAlive = enemies;
        IsChain = isChain;
        SuppressAfterimageSeed = suppressAfterimageSeed;
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

        // Afterimage: primary hit seeds a delayed ghost imprint at the impact point.
        // Guardrails:
        // - primary only (no chain-bounce seeds)
        // - requires a real hit (damage dealt > 0)
        // - delayed echo execution is owned by CombatSim
        if (!ctx.IsChain && !ctx.SuppressAfterimageSeed && damageDealtRaw > 0f && CountModifier(ctx.Attacker, "afterimage") > 0)
            GameController.Instance?.NotifyAfterimageHit(ctx.Attacker, ctx.Target.GlobalPosition, ctx.FinalDamage);

        // 4. On-hit effects (skipped for chain bounces if modifier opts out)
        foreach (var mod in ctx.Attacker.Modifiers)
        {
            if (!ctx.IsChain || mod.ApplyToChainTargets)
            {
                if (mod.OnHit(ctx))
                    GameController.Instance?.NotifyModifierProc(ctx.Attacker, mod.ModifierId);
            }
        }

        // Rocket Launcher has a native built-in splash on primary impacts.
        if (ctx.Attacker.TowerId == "rocket_launcher" && !ctx.IsChain)
            ApplyRocketLauncherSplash(ctx);

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

    private static void ApplyRocketLauncherSplash(DamageContext ctx)
    {
        float splashDamage = ctx.FinalDamage * Balance.RocketLauncherSplashDamageRatio;
        if (splashDamage <= 0f)
            return;

        int blastCoreCopies = CountModifier(ctx.Attacker, SpectacleDefinitions.BlastCore);
        float radius = Balance.RocketLauncherSplashRadius
            + blastCoreCopies * Balance.RocketLauncherBlastCoreRadiusPerCopy;
        Vector2 origin = ctx.Target.GlobalPosition;

        var splashTargets = new List<IEnemyView>();
        foreach (IEnemyView enemy in ctx.EnemiesAlive)
        {
            if (ReferenceEquals(enemy, ctx.Target))
                continue;
            if (enemy.Hp <= 0f)
                continue;
            if (origin.DistanceTo(enemy.GlobalPosition) <= radius)
                splashTargets.Add(enemy);
        }

        int attackerSlotIndex = -1;
        if (ctx.State != null)
            attackerSlotIndex = FindTowerSlotIndex(ctx.State, ctx.Attacker);
        bool applyChill = Statuses.TryGetChillSlowFactor(ctx.Attacker, out float chillSlowFactor);
        float totalDealt = 0f;
        foreach (IEnemyView enemy in splashTargets)
        {
            float damage = splashDamage;
            if (enemy.IsShieldProtected)
                damage *= (1f - Balance.ShieldDroneProtectionReduction);

            float hpBefore = enemy.Hp;
            enemy.Hp = MathF.Max(0f, enemy.Hp - damage);
            float dealt = hpBefore - enemy.Hp;
            totalDealt += dealt;
            if (applyChill)
                Statuses.ApplySlow(enemy, Balance.SlowDuration, chillSlowFactor);

            if (dealt > 0f && ctx.State != null)
                ctx.State.TrackBaseAttackDamage(attackerSlotIndex, (int)dealt, isKill: enemy.Hp <= 0f, enemy.ProgressRatio);
        }

        ctx.SplashDamageDealt += totalDealt;

        GameController.Instance?.NotifyRocketSplash(
            ctx.Attacker,
            origin,
            splashDamage,
            splashTargets,
            radius,
            burstCoreEnhanced: blastCoreCopies > 0);
    }

    private static void RegisterSpectacleDamageProcs(DamageContext ctx, float damageDealt, bool isKill)
    {
        var gc = GameController.Instance;
        if (gc == null)
            return;

        if (CountModifier(ctx.Attacker, SpectacleDefinitions.ExploitWeakness) > 0 && ctx.Target.IsMarked)
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.ExploitWeakness,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ExploitWeakness), damageDealt);

        if (CountModifier(ctx.Attacker, SpectacleDefinitions.FocusLens) > 0)
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.FocusLens,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.FocusLens), damageDealt);

        if (CountModifier(ctx.Attacker, SpectacleDefinitions.Overreach) > 0)
            gc.RegisterSpectacleProc(ctx.Attacker, SpectacleDefinitions.Overreach,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.Overreach), damageDealt);
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
