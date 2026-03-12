using Godot;
using SlotTheory.Entities;

namespace SlotTheory.Core;

/// <summary>
/// Pure damage mutation helper used by spectacle/surge gameplay payloads.
/// Kept engine-agnostic so it can be covered by unit tests.
/// </summary>
public static class SpectacleDamageCore
{
    public static float ApplyRawDamage(IEnemyView enemy, float damage)
    {
        if (enemy == null || enemy.Hp <= 0f || damage <= 0.05f)
            return 0f;

        float hpBefore = enemy.Hp;
        enemy.Hp = Mathf.Max(0f, enemy.Hp - damage);
        return hpBefore - enemy.Hp;
    }
}
