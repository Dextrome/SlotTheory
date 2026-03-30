using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

/// <summary>
/// Blast Core: each primary hit creates a small splash explosion around the target,
/// dealing damage to all enemies in a fixed radius.
///
/// Triggering rules:
///   FIRES on:  primary projectile hits, mine final-charge pops, split-projectile hits
///              (all contexts where ctx.IsChain == false)
///   SKIPS on:  chain bounces (IsChain=true), overkill spill (IsChain=true)
///
/// Splash rules:
///   - Splash damage = FinalDamage × BlastCoreDamageRatio. Because FinalDamage already
///     includes modifier and Marked bonuses, Blast Core naturally scales with Focus Lens,
///     Momentum stacks, etc. -- this is intentional.
///   - Primary target is excluded: no double-hit by design.
///   - Splash respects Shield Drone protection (35% reduction applied per enemy).
///   - Splash applies Chill Shot's slow only when the attacker has Chill Shot equipped.
///     Other status effects are not applied by splash.
///   - Splash does NOT pass through the modifier OnHit pipeline. Raw HP reduction only.
///     Reason: prevents recursive blast, modifier double-procs, and proc spaghetti.
///   - Splash kills do NOT trigger FeedbackLoop or other OnKill modifier effects.
///     (They do, however, kill the enemy -- CombatSim cleanup handles it normally.)
///   - Stacking: each additional copy widens the blast radius by BlastCoreRadiusPerCopy (25px).
///     The explosion hits everything in radius -- no artificial target cap.
///
/// Anti-recursion:
///   ApplyToChainTargets = false prevents DamageModel from calling OnHit on chain targets.
///   All splash damage is raw HP subtraction, not DamageModel.Apply calls, so Blast Core
///   can never proc more Blast Core. No guard flag needed in the call stack.
/// </summary>
public class BlastCore : Modifier
{
    public BlastCore(ModifierDef def) { ModifierId = def.Id; }

    // Only fire splash on primary-style hits. Chain bounce targets are excluded.
    public override bool ApplyToChainTargets => false;

    public override bool OnHit(DamageContext ctx)
    {
        // Belt-and-suspenders: ApplyToChainTargets=false already blocks chain-target calls
        // in DamageModel, but guard here for test harness and direct OnHit invocations.
        if (ctx.IsChain) return false;

        float splashDamage = ctx.FinalDamage * Balance.BlastCoreDamageRatio;
        if (splashDamage <= 0f) return false;

        // Count copies to scale blast radius. Each additional copy widens the explosion.
        // Radius grows naturally -- more enemies fall inside without artificial target selection.
        int copies = 0;
        foreach (var m in ctx.Attacker.Modifiers)
            if (m.ModifierId == ModifierId) copies++;

        Vector2 origin = ctx.Target.GlobalPosition;
        float radius = Balance.BlastCoreRadius + (copies - 1) * Balance.BlastCoreRadiusPerCopy
            + (ctx.State?.ExplosionRadiusBonus ?? 0f);

        // Collect in-range enemies, excluding the primary hit target.
        var candidates = new List<IEnemyView>();
        foreach (var enemy in ctx.EnemiesAlive)
        {
            if (ReferenceEquals(enemy, ctx.Target)) continue;
            if (enemy.Hp <= 0f) continue;
            if (origin.DistanceTo(enemy.GlobalPosition) <= radius)
                candidates.Add(enemy);
        }

        // Always notify even with no targets -- ring shows on every primary hit so the player
        // can see Blast Core is active. Splash sparks and spectacle tracking require targets.
        if (candidates.Count == 0)
        {
            GameController.Instance?.NotifyBlastCoreSplash(ctx.Attacker, origin, splashDamage, candidates, radius);
            return false; // no damage dealt, no proc halo
        }

        // No artificial target cap -- the blast hits everything in radius.
        // Order by distance for consistent visual and damage-number ordering only.
        candidates.Sort((a, b) =>
            origin.DistanceTo(a.GlobalPosition).CompareTo(origin.DistanceTo(b.GlobalPosition)));

        // Apply raw splash damage directly to HP.
        // Shield Drone protection is respected. Marked/DamageAmp bonuses are NOT re-applied
        // because splash is a secondary area effect, not a direct tower shot.
        // No modifier hooks are called on splash targets to prevent recursive effects.
        //
        // RunState kill/damage tracking IS called per-enemy so that TotalKills,
        // TotalDamageDealt, and per-tower bot analytics stay accurate.
        // OnKill modifier effects (FeedbackLoop, Overkill) are intentionally NOT fired --
        // splash is secondary damage and should not propagate the full kill pipeline.
        int slotIndex = FindTowerSlotIndex(ctx.State, ctx.Attacker);
        bool applyChill = Statuses.TryGetChillSlowFactor(ctx.Attacker, out float chillSlowFactor);
        float totalDealt = 0f;
        foreach (var enemy in candidates)
        {
            float damage = splashDamage;
            if (enemy.IsShieldProtected)
                damage *= (1f - Balance.ShieldDroneProtectionReduction);
            float hpBefore = enemy.Hp;
            enemy.Hp = MathF.Max(0f, enemy.Hp - damage);
            float dealt = hpBefore - enemy.Hp;
            totalDealt += dealt;
            if (applyChill)
                Statuses.ApplySlow(enemy, Balance.SlowDuration * (ctx.State?.SlowDurationMultiplier ?? 1f), chillSlowFactor);

            if (dealt > 0f && ctx.State != null)
                ctx.State.TrackBaseAttackDamage(slotIndex, (int)dealt, isKill: enemy.Hp <= 0f, enemy.ProgressRatio);
        }

        ctx.SplashDamageDealt += totalDealt;

        GameController.Instance?.RegisterSpectacleProc(ctx.Attacker, ModifierId,
            SpectacleDefinitions.GetProcScalar(ModifierId), totalDealt);

        // Notify GameController for visuals, stats, and callout. No-op in bot/headless mode.
        GameController.Instance?.NotifyBlastCoreSplash(ctx.Attacker, origin, splashDamage, candidates, radius);

        return true; // trigger tower proc halo
    }

    /// <summary>
    /// Mirrors DamageModel.FindTowerSlotIndex (private there). Needed to attribute splash
    /// damage and kills to the correct slot in RunState for end-screen stats and bot analytics.
    /// Returns -1 if the tower is not found (safe: TrackBaseAttackDamage accepts -1).
    /// </summary>
    private static int FindTowerSlotIndex(SlotTheory.Core.RunState? state, SlotTheory.Entities.ITowerView tower)
    {
        if (state == null) return -1;
        for (int i = 0; i < state.Slots.Length; i++)
            if (ReferenceEquals(state.Slots[i].Tower, tower)) return i;
        return -1;
    }
}
