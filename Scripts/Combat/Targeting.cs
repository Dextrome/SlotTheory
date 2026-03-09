using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Combat;

public static class Targeting
{
    /// <summary>
    /// Selects a target from the enemy list based on the tower's targeting mode and range.
    /// Generic so production code can pass List&lt;EnemyInstance&gt; and get EnemyInstance? back
    /// while tests can pass List&lt;FakeEnemy&gt; without any casting.
    /// </summary>
    public static T? SelectTarget<T>(ITowerView tower, IEnumerable<T> enemies,
                                      bool ignoreRange = false)
        where T : class, IEnemyView
    {
        var inRange = ignoreRange
            ? enemies.Where(e => e.Hp > 0).ToList()
            : enemies.Where(e => e.Hp > 0 && IsInRange(tower, e)).ToList();
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
    private static bool IsInRange(ITowerView tower, IEnemyView enemy) =>
        tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range;
}
