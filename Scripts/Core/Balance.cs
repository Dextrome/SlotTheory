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
    // Build type - set via export preset custom_features="demo".
    // Use this to gate all full-game-only content. Never hardcode true/false here.
    // _isDemoOverride allows headless bot runs to pass --demo and simulate demo conditions.
    private static bool? _isDemoOverride;
    public static bool IsDemo => _isDemoOverride ?? Godot.OS.HasFeature("demo");
    /// <summary>Called by GameController when --demo arg is detected. Survives scene reloads.</summary>
    public static void SetDemoOverride(bool isDemo) => _isDemoOverride = isDemo;

    // Steam - demo wishlist CTA
    // Set to the full game's Steam App ID (different from the demo's App ID).
    // When non-zero and Steam is running, a "Wishlist" button appears on EndScreen and MainMenu.
    public const uint FullGameSteamAppId = 4523160;

    // Run structure
    public const int TotalWaves = 20;

    // Endless mode scaling (waves beyond TotalWaves)
    public const float EndlessEnemyCountScalePerWave = 0.05f;  // +5% enemy count per endless wave (multiplicative)
    public const float EndlessEnemyHpScalePerWave    = 0.02f;  // +2% enemy HP per endless wave (multiplicative)
    public const int   EndlessSwiftBonusInterval     = 5;      // every N endless waves: +1 Swift Walker
    public const int   EndlessReverseBonusInterval   = 6;      // every N endless waves: +1 Reverse Walker (full game only)
    public const float EndlessSpawnIntervalFloor     = 0.70f;  // minimum spawn interval in endless mode
    public const int SlotCount = 6;
    public const int Wave1ExtraPicks  = 0;          // temporarily disabled: always 1 pick before wave 1
    public const int Wave15ExtraPicks = 0;          // temporarily disabled: always 1 pick before wave 15

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

    // Enemies - Basic Walker
    public const float BaseEnemyHp = 65f;
    public const float HpGrowthPerWave = 1.10f;  // HP × 1.10^(wave-1)
    public const float BaseEnemySpeed = 120f;     // pixels per second along path

    // Enemies - Armored Walker
    public const float TankyHpMultiplier = 3.5f;  // 3.5× basic walker HP
    public const float TankyEnemySpeed = 60f;     // pixels per second (half speed)

    // Enemies - Splitter Walker
    public const float SplitterHpMultiplier = 1.8f;   // 1.8× basic walker HP
    public const float SplitterSpeed = 90f;            // pixels per second (slower than basic)
    public const int   SplitterShardCount = 2;         // number of shards spawned on death

    // Enemies - Splitter Shard (spawned when a Splitter dies)
    public const float SplitterShardHpMultiplier = 0.55f;  // 55% of basic walker HP
    public const float SplitterShardSpeed = 165f;           // pixels per second (faster than basic)

    // Enemies - Shield Drone
    public const float ShieldDroneHpMultiplier        = 1.8f;   // 1.8× basic walker HP
    public const float ShieldDroneSpeed               = 85f;    // pixels per second (slow-moderate)
    public const float ShieldDroneAuraRadius          = 140f;   // protection field radius in pixels
    public const float ShieldDroneProtectionReduction = 0.35f;  // 35% damage reduction for shielded allies

    // Endless mode - Shield Drone scaling
    public const int   EndlessShieldDroneBonusInterval = 8;     // every N endless waves: +1 Shield Drone

    // Enemies - Reverse Walker (full game)
    public const float ReverseWalkerHpMultiplier = 1.35f;
    public const float ReverseWalkerSpeed = 108f;
    public const float ReverseWalkerTriggerDamageRatio = 0.10f;     // trigger when a single non-lethal hit deals >= 10% max HP
    public const float ReverseWalkerTriggerDamageRamp = 0.24f;      // controls scaling into max jump distance
    public const float ReverseWalkerJumpDistanceMin = 62f;          // path distance in pixels
    public const float ReverseWalkerJumpDistanceMax = 102f;         // path distance in pixels
    public const float ReverseWalkerMinEffectiveJump = 16f;         // ignore triggers if near lane start
    public const float ReverseWalkerJumpCooldown = 2.6f;
    public const int   ReverseWalkerMaxTriggersPerLife = 2;
    public const float ReverseWalkerFxDuration = 0.32f;

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
    public const float SplitShotDamageRatio = 0.28f; // 28% of base damage per split projectile
    public const float SplitShotRange       = 220f;  // search radius from impact point for split targets

    // Feedback Loop modifier
    public const float FeedbackLoopCooldownReduction = 1.00f; // 100% of remaining cooldown removed on kill (full reset)

    // Hair Trigger modifier
    public const float HairTriggerAttackSpeed = 1.30f; // +30% attack speed
    public const float HairTriggerRangeFactor = 0.82f; // -18% range

    // Overreach modifier
    public const float OverreachRangeFactor = 1.45f;  // +45% range
    public const float OverreachDamageFactor = 0.90f; // -10% damage

    // Exploit Weakness modifier
    public const float ExploitWeaknessDamageBonus = 1.45f; // ×1.45 damage to Marked enemies (+45%)

    // Overkill modifier
    public const float OverkillSpillEfficiency = 0.60f; // 60% excess damage spill

    // Chain decay
    public const float ChainDamageDecay = 0.50f;           // damage multiplier per bounce (Arc Emitter base + ChainReaction modifier)
    public const float ChainReactionDamageDecay = 0.50f;   // kept in sync with ChainDamageDecay for consistent player-facing rule
    public const float ChainReactionRange = 320f;           // ChainReaction modifier: chain search radius

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

    // Enemies - Swift Walker
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
        public const float NormalEnemyHpMultiplier = 1.2f;      // +20% HP
        public const float NormalEnemyCountMultiplier = 1.05f; // +5% more enemies
        public const float NormalSpawnIntervalMultiplier = 0.95f; // ~5% faster spawns
        
        // Hard mode - tuned toward ~50% bot win target with surge/global surge pacing
        public const float HardEnemyHpMultiplier = 1.3f;      // +30% HP
        public const float HardEnemyCountMultiplier = 1.1f;   // +10% more enemies
        public const float HardSpawnIntervalMultiplier = 0.90f;  // ~10% faster spawns
    }

    public static float GetEnemyHpMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasyEnemyHpMultiplier,
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalEnemyHpMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardEnemyHpMultiplier, 0.1f, 5f),
        _ => 1.0f
    };

    public static float GetEnemyCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasyEnemyCountMultiplier,
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalEnemyCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardEnemyCountMultiplier, 0.1f, 5f),
        _ => 1.0f
    };

    public static float GetSpawnIntervalMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => DifficultyMultipliers.EasySpawnIntervalMultiplier,
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalSpawnIntervalMultiplier, 0.2f, 3f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardSpawnIntervalMultiplier, 0.2f, 3f),
        _ => 1.0f
    };

    public static float GetTankyCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyTankyCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalTankyCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardTankyCountMultiplier, 0.1f, 5f),
        _ => 1f
    };

    public static float GetSwiftCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasySwiftCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalSwiftCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardSwiftCountMultiplier, 0.1f, 5f),
        _ => 1f
    };

    public static float GetSplitterCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasySplitterCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalSplitterCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardSplitterCountMultiplier, 0.1f, 5f),
        _ => 1f
    };

    public static float GetReverseCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyReverseCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalReverseCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardReverseCountMultiplier, 0.1f, 5f),
        _ => 1f
    };

    public static float GetShieldDroneCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyShieldDroneCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalShieldDroneCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardShieldDroneCountMultiplier, 0.1f, 5f),
        _ => 1f
    };

    private static float ClampDifficultyMultiplier(float value, float min, float max)
        => value < min ? min : (value > max ? max : value);
}
