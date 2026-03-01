namespace SlotTheory.Core;

/// <summary>All tunables in one place. Change values here, nowhere else.</summary>
public static class Balance
{
    // Run structure
    public const int TotalWaves = 20;
    public const int SlotCount = 6;
    public const int StartingLives = 10;
    public const int MaxModifiersPerTower = 3;
    public const int DraftOptionsCount = 5;
    public const int DraftTowerOptions = 2;             // when free slots exist
    public const int DraftModifierOptions = 3;          // when free slots exist
    public const int DraftModifierOptionsFull = 4;      // when all slots occupied (< pool size keeps scarcity)

    // Enemies
    public const float BaseEnemyHp = 65f;
    public const float HpGrowthPerWave = 1.12f;  // HP × 1.12^(wave-1)
    public const float BaseEnemySpeed = 120f;     // pixels per second along path

    // Marked status
    public const float MarkedDamageBonus = 0.20f; // +20% incoming damage to all towers
    public const float MarkedDuration = 2f;       // seconds

    // Slow status
    public const float SlowSpeedFactor = 0.70f;   // enemy moves at 70% speed (-30%)
    public const float SlowDuration = 5f;          // seconds

    // Momentum modifier
    public const int MomentumMaxStacks = 5;        // caps at ×1.5 damage multiplier

    // Waves
    public const float DefaultSpawnInterval = 1.5f;
    public const int DefaultEnemyCount = 10;
}
