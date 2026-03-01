using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public static class Targeting
{
    public static EnemyInstance? SelectTarget(TowerInstance tower, List<EnemyInstance> enemies)
    {
        var inRange = enemies.Where(e => e.Hp > 0 && IsInRange(tower, e)).ToList();
        if (inRange.Count == 0) return null;

        return tower.TargetingMode switch
        {
            TargetingMode.First     => inRange.MaxBy(e => e.ProgressRatio),
            TargetingMode.Strongest => inRange.MaxBy(e => e.Hp),
            TargetingMode.LowestHp  => inRange.MinBy(e => e.Hp),
            _                       => inRange.MaxBy(e => e.ProgressRatio),
        };
    }

    /// <summary>Circular range check using world positions of tower and enemy nodes.</summary>
    private static bool IsInRange(TowerInstance tower, EnemyInstance enemy) =>
        tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range;
}
