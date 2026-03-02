namespace SlotTheory.Core;

/// <summary>All tunables in one place. Change values here, nowhere else.</summary>
public static class Balance
{
    // Run structure
    public const int TotalWaves = 20;
    public const int SlotCount = 6;
    public const int Wave1ExtraPicks  = 1;          // extra draft picks before wave 1 starts
    public const int Wave15ExtraPicks = 1;          // extra draft picks before wave 15 starts

    public static int ExtraPicksForWave(int waveIndex) => waveIndex switch
    {
        0  => Wave1ExtraPicks,
        14 => Wave15ExtraPicks,
        _  => 0
    };
    public const int StartingLives = 10;
    public const int MaxModifiersPerTower = 3;
    public const int DraftOptionsCount = 5;
    public const int DraftTowerOptions = 2;             // when free slots exist
    public const int DraftModifierOptions = 3;          // when free slots exist
    public const int DraftModifierOptionsFull = 4;      // when all slots occupied (< pool size keeps scarcity)

    // Enemies — Basic Walker
    public const float BaseEnemyHp = 65f;
    public const float HpGrowthPerWave = 1.08f;  // HP × 1.08^(wave-1)
    public const float BaseEnemySpeed = 120f;     // pixels per second along path

    // Enemies — Armored Walker
    public const float TankyHpMultiplier = 4f;    // 4× basic walker HP
    public const float TankyEnemySpeed = 60f;     // pixels per second (half speed)

    // Marked status
    public const float MarkedDamageBonus = 0.30f; // +30% incoming damage to all towers
    public const float MarkedDuration = 2f;       // seconds

    // Slow status
    public const float SlowSpeedFactor = 0.70f;   // enemy moves at 70% speed (-30%)
    public const float SlowDuration = 5f;          // seconds

    // Momentum modifier
    public const int   MomentumMaxStacks    = 5;     // 5 stacks × 8% = ×1.4 max multiplier
    public const float MomentumBonusPerStack = 0.08f;

    // Split Shot modifier
    public const float SplitShotDamageRatio = 0.80f; // 80% of base damage per split projectile
    public const float SplitShotRange       = 200f;  // search radius from impact point for split targets

    // Feedback Loop modifier
    public const float FeedbackLoopCooldownReduction = 0.30f; // 30% of remaining cooldown removed on kill

    // Chain Reaction modifier — range/decay inherit tower defaults (260f / 0.6f); no new constants needed

    // Waves
    public const float DefaultSpawnInterval = 1.5f;
    public const int DefaultEnemyCount = 10;
}
