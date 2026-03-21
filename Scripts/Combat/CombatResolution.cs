using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public static class CombatResolution
{
    public static int ApplyChainHits<TEnemy>(
        ITowerView tower,
        TEnemy primary,
        int waveIndex,
        List<TEnemy> enemies,
        RunState? state,
        System.Action<DamageContext>? onHit = null)
        where TEnemy : class, IEnemyView
    {
        if (!tower.IsChainTower || tower.ChainCount <= 0)
            return 0;

        var alreadyHit = new HashSet<TEnemy> { primary };
        float damage = tower.BaseDamage * tower.ChainDamageDecay;
        int bounces = 0;
        TEnemy currentTarget = primary;

        while (bounces < tower.ChainCount)
        {
            TEnemy? nextTarget = null;
            float bestDist = tower.ChainRange;

            foreach (TEnemy enemy in enemies)
            {
                if (alreadyHit.Contains(enemy) || enemy.Hp <= 0f)
                    continue;

                float distance = currentTarget.GlobalPosition.DistanceTo(enemy.GlobalPosition);
                if (distance < bestDist)
                {
                    bestDist = distance;
                    nextTarget = enemy;
                }
            }

            if (nextTarget == null)
                break;

            var context = new DamageContext(
                tower,
                nextTarget,
                waveIndex,
                enemies,
                state,
                isChain: true,
                damageOverride: damage);
            DamageModel.Apply(context);
            onHit?.Invoke(context);

            alreadyHit.Add(nextTarget);
            currentTarget = nextTarget;
            damage *= tower.ChainDamageDecay;
            bounces++;
        }

        return bounces;
    }

    public static int ApplySplitHits<TEnemy>(
        ITowerView tower,
        TEnemy primary,
        int waveIndex,
        List<TEnemy> enemies,
        RunState? state,
        System.Action<DamageContext>? onHit = null)
        where TEnemy : class, IEnemyView
    {
        if (tower.SplitCount <= 0)
            return 0;

        float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
        var candidates = enemies
            .Where(enemy => !ReferenceEquals(enemy, primary) && enemy.Hp > 0f)
            .OrderBy(enemy => enemy.GlobalPosition.DistanceTo(primary.GlobalPosition));

        int spawned = 0;
        foreach (TEnemy candidate in candidates)
        {
            if (spawned >= tower.SplitCount + 1)
                break;
            if (candidate.GlobalPosition.DistanceTo(primary.GlobalPosition) > Balance.SplitShotRange)
                break;

            var context = new DamageContext(
                tower,
                candidate,
                waveIndex,
                enemies,
                state,
                isChain: true,
                damageOverride: splitDamage);
            DamageModel.Apply(context);
            onHit?.Invoke(context);
            spawned++;
        }

        return spawned;
    }
}
