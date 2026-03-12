using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Read/write view of an enemy's combat-relevant state. Implemented by EnemyInstance (production)
/// and FakeEnemy (tests).
/// </summary>
public interface IEnemyView
{
    float Hp { get; set; }
    float ProgressRatio { get; }
    Vector2 GlobalPosition { get; }
    bool IsMarked { get; }
    float MarkedRemaining { get; set; }
    float SlowSpeedFactor { get; set; }
    float SlowRemaining { get; set; }
    float DamageAmpRemaining { get; set; }
    float DamageAmpMultiplier { get; set; }
}
