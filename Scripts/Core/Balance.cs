namespace SlotTheory.Core;

/// <summary>Difficulty modes for enemy scaling.</summary>
public enum DifficultyMode
{
    Easy = 0,  // legacy "Normal" mode
    Hard = 1,  // keep legacy value for existing saved settings
    Normal = 2
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
    public const float TankyHpMultiplier = 3.5f;  // 3.5× basic walker HP
    public const float TankyEnemySpeed = 60f;     // pixels per second (half speed)

    // Marked status
    public const float MarkedDamageBonus = 0.40f; // +40% incoming damage to all towers
    public const float MarkedDuration = 4.0f;     // seconds

    // Slow status
    public const float SlowSpeedFactor = 0.70f;   // enemy moves at 70% speed (-30%) per Chill Shot; stacks multiplicatively
    public const float SlowDuration = 6f;          // seconds

    // Momentum modifier
    public const int   MomentumMaxStacks    = 5;     // 5 stacks × 16% = ×1.80 max multiplier
    public const float MomentumBonusPerStack = 0.16f;

    // Split Shot modifier
    public const float SplitShotDamageRatio = 0.35f; // 35% of base damage per split projectile
    public const float SplitShotRange       = 280f;  // search radius from impact point for split targets

    // Feedback Loop modifier
    public const float FeedbackLoopCooldownReduction = 0.50f; // 50% of remaining cooldown removed on kill

    // Hair Trigger modifier
    public const float HairTriggerAttackSpeed = 1.30f; // +30% attack speed
    public const float HairTriggerRangeFactor = 0.82f; // -18% range

    // Overreach modifier
    public const float OverreachRangeFactor = 1.55f;  // +55% range
    public const float OverreachDamageFactor = 0.90f; // -10% damage

    // Exploit Weakness modifier
    public const float ExploitWeaknessDamageBonus = 1.60f; // ×1.60 damage to Marked enemies (+60%)

    // Overkill modifier
    public const float OverkillSpillEfficiency = 0.60f; // 60% excess damage spill

    // Chain decay (shared by Arc Emitter base and ChainReaction modifier)
    public const float ChainDamageDecay = 0.60f; // damage multiplier per bounce

    // Rift Sapper mines
    public const int   RiftMineMaxActivePerTower = 7;
    public const float RiftMineDamageMultiplier  = 1.00f; // base multiplier before per-charge stage multipliers
    public const int   RiftMineChargesPerMine    = 3;     // number of triggers before mine is consumed
    public const float RiftMineTickDamageMultiplier  = 0.65f; // damage for non-final charge triggers
    public const float RiftMineFinalDamageMultiplier = 1.15f; // damage for final charge trigger
public const float RiftMineMiniDamageFactor  = 0.35f; // split-planted mine damage scale (matches Split Shot ratio)
    public const float RiftMineMiniPlantSpacingMultiplier = 0.62f; // mini mines can pack tighter than base mines
    public const float RiftMineArmTime           = 0.16f; // seconds before planted mine can trigger
    public const float RiftMineRetriggerDelay    = 0.18f; // per-mine lockout between charge triggers
    public const float RiftMineTriggerRadius     = 32f;   // enemy must enter this radius to trigger
    public const float RiftMineBlastRadius       = 82f;   // detonation target search radius
    public const float RiftMinePlantSpacing      = 46f;   // min spacing between active mines
    public const float RiftMineAnchorStep        = 26f;   // path sampling resolution
    public const float RiftMineSplitPlantRadius  = 104f;  // where split-shot mini mines can be planted
    public const float RiftMineBurstWindow       = 2.4f;  // wave-start rapid seeding window
    public const float RiftMineBurstIntervalMultiplier = 0.55f; // attack interval multiplier during burst
    public const int   RiftMineBurstFastPlantsPerTower  = 3;    // cap of burst-boosted plants per tower per wave

    // Focus Lens modifier
    public const float FocusLensDamageBonus = 2.40f; // +140% damage
    public const float FocusLensAttackInterval = 1.85f; // x1.85 attack interval

    // Enemies — Swift Walker
    public const float SwiftHpMultiplier = 1.5f;  // 1.5× basic walker HP
    public const float SwiftEnemySpeed   = 240f;  // pixels per second (2× basic)

    // Waves
    public const float DefaultSpawnInterval = 1.275f;
    public const int DefaultEnemyCount = 12;

    // Difficulty multipliers
    public static class DifficultyMultipliers
    {
        // Easy mode (legacy "Normal")
        public const float EasyEnemyHpMultiplier = 1.0f;
        public const float EasyEnemyCountMultiplier = 1.0f;
        public const float EasySpawnIntervalMultiplier = 1.0f;

        // Normal mode - tuned toward ~75% bot win target with surge/global surge pacing
        public const float NormalEnemyHpMultiplier = 1.24f;      // +24% HP
        public const float NormalEnemyCountMultiplier = 1.1f;
        public const float NormalSpawnIntervalMultiplier = 0.95f;
        
        // Hard mode - tuned toward ~50% bot win target with surge/global surge pacing
        public const float HardEnemyHpMultiplier = 1.40f;      // +40% HP
        public const float HardEnemyCountMultiplier = 1.15f;   // +15% more enemies
        public const float HardSpawnIntervalMultiplier = 0.90f;  // ~10% faster spawns
    }

    public static float GetEnemyHpMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasyEnemyHpMultiplier,
        DifficultyMode.Normal => DifficultyMultipliers.NormalEnemyHpMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardEnemyHpMultiplier,
        _ => 1.0f
    };

    public static float GetEnemyCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasyEnemyCountMultiplier,
        DifficultyMode.Normal => DifficultyMultipliers.NormalEnemyCountMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardEnemyCountMultiplier,
        _ => 1.0f
    };

    public static float GetSpawnIntervalMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasySpawnIntervalMultiplier,
        DifficultyMode.Normal => DifficultyMultipliers.NormalSpawnIntervalMultiplier,
        DifficultyMode.Hard => DifficultyMultipliers.HardSpawnIntervalMultiplier,
        _ => 1.0f
    };
}
