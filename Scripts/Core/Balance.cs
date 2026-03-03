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
    public const float HpGrowthPerWave = 1.10f;  // HP × 1.10^(wave-1)
    public const float BaseEnemySpeed = 120f;     // pixels per second along path

    // Enemies — Armored Walker
    public const float TankyHpMultiplier = 4f;    // 4× basic walker HP
    public const float TankyEnemySpeed = 60f;     // pixels per second (half speed)

    // Marked status
    public const float MarkedDamageBonus = 0.30f; // +30% incoming damage to all towers
    public const float MarkedDuration = 2f;       // seconds

    // Slow status
    public const float SlowSpeedFactor = 0.80f;   // enemy moves at 80% speed (-20%) per Chill Shot; stacks multiplicatively
    public const float SlowDuration = 5f;          // seconds

    // Momentum modifier
    public const int   MomentumMaxStacks    = 5;     // 5 stacks × 8% = ×1.4 max multiplier
    public const float MomentumBonusPerStack = 0.08f;

    // Split Shot modifier
    public const float SplitShotDamageRatio = 0.55f; // 55% of base damage per split projectile
    public const float SplitShotRange       = 200f;  // search radius from impact point for split targets

    // Feedback Loop modifier
    public const float FeedbackLoopCooldownReduction = 0.30f; // 30% of remaining cooldown removed on kill

    // Hair Trigger modifier
    public const float HairTriggerAttackSpeed = 1.35f; // +35% attack speed
    public const float HairTriggerRangeFactor = 0.70f; // -30% range

    // Overkill modifier
    public const float OverkillSpillEfficiency = 0.60f; // 60% excess damage spill

    // Focus Lens modifier
    public const float FocusLensDamageBonus = 2.2f; // +120% damage
    public const float FocusLensAttackInterval = 2f; // x2 attack interval

    // Enemies — Swift Walker
    public const float SwiftHpMultiplier = 1.5f;  // 1.5× basic walker HP
    public const float SwiftEnemySpeed   = 240f;  // pixels per second (2× basic)

    // Waves
    public const float DefaultSpawnInterval = 1.275f;
    public const int DefaultEnemyCount = 12;
}
