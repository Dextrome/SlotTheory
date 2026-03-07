namespace SlotTheory.Core;

/// <summary>Difficulty modes for enemy scaling.</summary>
public enum DifficultyMode
{
    Normal,
    Hard
}

/// <summary>All tunables in one place. Change values here, nowhere else.</summary>
public static class Balance
{
    // Run structure
    public const int TotalWaves = 20;
    public const int SlotCount = 6;
    public const int Wave1ExtraPicks  = 1;          // extra draft picks before wave 1 starts
    public const int Wave15ExtraPicks = 1;          // extra draft picks before wave 15 starts

    // Visual effects (platform-adaptive)
    public static int MaxParticles => MobileOptimization.IsMobile() ? MobileOptimization.MaxParticles : 100;
    public static int ProjectileHistoryLength => MobileOptimization.IsMobile() ? MobileOptimization.ProjectileHistoryLength : 10;
    public static bool EnableScreenShake => MobileOptimization.IsMobile() ? MobileOptimization.EnableScreenShake : true;
    public static float GlowRadius => MobileOptimization.IsMobile() ? MobileOptimization.GlowRadius : 1.0f;

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
    public const float MarkedDamageBonus = 0.40f; // +40% incoming damage to all towers
    public const float MarkedDuration = 4.0f;     // seconds

    // Slow status
    public const float SlowSpeedFactor = 0.75f;   // enemy moves at 75% speed (-25%) per Chill Shot; stacks multiplicatively
    public const float SlowDuration = 5f;          // seconds

    // Momentum modifier
    public const int   MomentumMaxStacks    = 5;     // 5 stacks × 16% = ×1.80 max multiplier
    public const float MomentumBonusPerStack = 0.16f;

    // Split Shot modifier
    public const float SplitShotDamageRatio = 0.42f; // 42% of base damage per split projectile
    public const float SplitShotRange       = 300f;  // search radius from impact point for split targets

    // Feedback Loop modifier
    public const float FeedbackLoopCooldownReduction = 0.25f; // 25% of remaining cooldown removed on kill

    // Hair Trigger modifier
    public const float HairTriggerAttackSpeed = 1.40f; // +40% attack speed
    public const float HairTriggerRangeFactor = 0.82f; // -18% range

    // Exploit Weakness modifier
    public const float ExploitWeaknessDamageBonus = 1.60f; // ×1.60 damage to Marked enemies (+60%)

    // Overkill modifier
    public const float OverkillSpillEfficiency = 0.60f; // 60% excess damage spill

    // Chain decay (shared by Arc Emitter base and ChainReaction modifier)
    public const float ChainDamageDecay = 0.60f; // damage multiplier per bounce

    // Focus Lens modifier
    public const float FocusLensDamageBonus = 2.25f; // +125% damage
    public const float FocusLensAttackInterval = 2f; // x2 attack interval

    // Enemies — Swift Walker
    public const float SwiftHpMultiplier = 1.5f;  // 1.5× basic walker HP
    public const float SwiftEnemySpeed   = 240f;  // pixels per second (2× basic)

    // Waves
    public const float DefaultSpawnInterval = 1.275f;
    public const int DefaultEnemyCount = 12;

    // Difficulty multipliers
    public static class DifficultyMultipliers
    {
        // Normal mode - light difficulty (+5% challenge) (targeting ~80% win rate)
        public const float NormalEnemyHpMultiplier = 1.05f;
        public const float NormalEnemyCountMultiplier = 1.05f;
        public const float NormalSpawnIntervalMultiplier = 0.95f;
        
        // Hard mode - moderately more challenging (targeting ~40% win rate)
        public const float HardEnemyHpMultiplier = 1.1f;        // +10% HP
        public const float HardEnemyCountMultiplier = 1.2f;     // +20% more enemies 
        public const float HardSpawnIntervalMultiplier = 0.9f;  // 10% faster spawns
    }

    public static float GetEnemyHpMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Normal => DifficultyMultipliers.NormalEnemyHpMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardEnemyHpMultiplier,
        _ => 1.0f
    };

    public static float GetEnemyCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Normal => DifficultyMultipliers.NormalEnemyCountMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardEnemyCountMultiplier,
        _ => 1.0f
    };

    public static float GetSpawnIntervalMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Normal => DifficultyMultipliers.NormalSpawnIntervalMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardSpawnIntervalMultiplier,
        _ => 1.0f
    };
}
