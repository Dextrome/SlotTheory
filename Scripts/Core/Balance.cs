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
    public static bool IsDemo => _isDemoOverride ?? (Godot.OS.HasFeature("demo") || System.Array.IndexOf(Godot.OS.GetCmdlineUserArgs(), "--demo") >= 0);
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

    // Surge pip -- flying contribution indicator (tower surge → global surge bar)
    public const float SurgePipLingerSec  = 0.70f;  // dwell at source tower before flying
    public const float SurgePipTravelSec  = 0.65f;  // flight duration
    public const float SurgePipArcHeight  = 52f;    // upward arc offset in screen pixels
    public const float SurgePipCoreRadius = 5.0f;   // core dot radius
    public const float SurgePipGlowRadius = 10.0f;  // outer glow radius
    public const int   SurgePipMaxActive  = 6;      // cap on simultaneous active pips
    public const float SurgePipBarPulse   = 1.28f;  // HUD bar brightness peak on pip arrival

    // ── Surge spectacle hierarchy ──────────────────────────────────────────────
    // Tower Surge: contained local burst – fuel generation, NOT the main event.
    public const int   TowerSurgeMaxLinks         = 3;     // max enemies linked (was 6)
    public const float TowerSurgeMinLinkDistance  = 160f;  // floor link reach in px
    public const float TowerSurgeLinkRangeFactor  = 0.70f; // multiplier on tower.Range
    public const float TowerSurgeScreenFlashAlpha = 0.09f; // screen flash peak alpha (was 0.17)
    public const float TowerSurgeSlowMoDuration   = 0.12f; // realtime slowmo length in sec (was 0.5)
    public const float TowerSurgeSlowMoFactor     = 0.70f; // speed scale during slowmo (was 0.50)
    public const float TowerSurgeSignatureDrama   = 0.14f; // signature ring intensity (was 0.28)
    public const float TowerSurgeArchetypeDrama   = 0.12f; // archetype FX intensity (was 0.28)

    // Global Surge: board-wide premium storm – the true earned payoff.
    public const int   GlobalSurgeLinksPerTower    = 5;     // enemies linked per tower (was 2)
    public const float GlobalSurgeMinLinkDistance  = 440f;  // floor link reach in px (was 280)
    public const float GlobalSurgeLinkRangeFactor  = 1.65f; // multiplier on tower.Range (was 1.15)
    public const float GlobalSurgeTowerAfterglow   = 5.0f;  // afterglow duration in sec (was 2.4)
    public const float GlobalSurgeLingerHoldSec    = 2.4f;  // screen tint hold (was 1.0)
    public const float GlobalSurgeLingerFadeSec    = 2.8f;  // screen tint fade (was 1.4)
    public const float GlobalSurgeSignatureDrama   = 0.95f; // signature ring intensity (was 0.7)
    public const float GlobalSurgeArchetypeDrama   = 0.92f; // archetype FX intensity (was 0.75)
    public const float GlobalSurgeLongArcLifetime  = 0.72f; // lingering storm arc duration in sec
    public const float GlobalSurgeTowerWebLifetime = 0.58f; // tower-to-tower web arc duration in sec

    // ── Feel-differentiated Global Surge payloads ──────────────────────────────
    // PRESSURE: control focus -- longer/deeper status effects, slightly reduced burst
    public const float PressureSurgeMarkMult      = 1.60f;  // mark duration multiplier
    public const float PressureSurgeSlowMult      = 1.50f;  // slow duration multiplier
    public const float PressureSurgeSlowBonus     = 0.14f;  // subtracted from speed factor (deeper slow)
    public const float PressureSurgeDamageMult    = 0.88f;  // burst damage multiplier (control, not explosion)
    // CHAIN (Neutral): spreading reactions -- normal payload + enemy→enemy arc aftermath
    public const int   ChainSurgeEnemyArcs        = 8;      // enemy→enemy arc jumps in aftermath
    public const float ChainSurgeArcLifetime      = 0.50f;  // lifetime of chain aftermath arcs in sec
    // DETONATION: burst focus -- heavy damage, max cooldown, shorter status
    public const float DetonationSurgeDamageMult  = 1.35f;  // burst damage multiplier
    public const float DetonationSurgeCooldownBonus = 0.12f;// extra cooldown refund (added on top of base)
    public const float DetonationSurgeMarkMult    = 0.75f;  // mark duration multiplier (shorter -- they die fast)
    public const float DetonationSurgeSlowMult    = 0.80f;  // slow duration multiplier

    // ── Tower Surge category-biased presentation ────────────────────────────
    // Spread category: tower-to-enemy web arcs, extended reach, electric branching read
    public const int   TowerSurgeSpreadMaxLinks    = 5;     // more enemy connections (base is 3)
    public const float TowerSurgeSpreadLinkMult    = 1.40f; // extended link reach multiplier
    public const float TowerSurgeSpreadFlashAlpha  = 0.13f; // brighter radiating flash
    public const int   TowerSurgeSpreadWebCount    = 5;     // tower-to-enemy web arcs (Spread headline)
    public const float TowerSurgeSpreadWebLifetime = 0.38f; // arc lifetime in sec -- long enough to read as web
    public const float TowerSurgeSpreadWebReach    = 520f;  // max distance for web arcs in px

    // Burst category: double-punch explosion, hard flash, deeper time dilation
    public const float TowerSurgeBurstFlashAlpha      = 0.22f; // hard punchy flash (vs 0.09 base)
    public const float TowerSurgeBurstArchetypeDrama  = 0.22f; // more visible burst archetype FX
    public const float TowerSurgeBurstSlowMoDuration  = 0.20f; // longer impact window
    public const float TowerSurgeBurstSlowMoFactor    = 0.55f; // deeper time dilation
    public const float TowerSurgeBurstPowerMult       = 1.55f; // stronger burst + volley FX
    public const float TowerSurgeBurstPulse2Delay     = 0.30f; // second explosion pulse delay in sec
    public const float TowerSurgeBurstPulse2Power     = 0.70f; // second pulse relative power

    // Control category: softer flash, prominent zone rings, longer presence
    public const int   TowerSurgeControlMaxLinks        = 2;     // fewer, more focused links
    public const float TowerSurgeControlFlashAlpha      = 0.05f; // subdued flash (zone, not pop)
    public const float TowerSurgeControlSignatureDrama  = 0.46f; // much more visible zone rings (vs 0.14 base)
    public const float TowerSurgeControlSlowMoDuration  = 0.22f; // longer zone presence

    // Echo category: strong afterimage ghost, second delayed repeat burst
    public const float TowerSurgeEchoArchetypeDrama = 0.24f; // more visible echo ghost
    public const float TowerSurgeEchoDelay2         = 0.48f; // second repeat strike delay in sec
    public const float TowerSurgeEchoPulse2Power    = 0.72f; // second repeat burst power

    public static int ExtraPicksForWave(int waveIndex) => waveIndex switch
    {
        0  => Wave1ExtraPicks,
        14 => Wave15ExtraPicks,
        _  => 0
    };
    public const int StartingLives = 10; // fallback / default param sentinel -- prefer GetStartingLives(difficulty)
    public static int GetStartingLives(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy   => 25,
        DifficultyMode.Normal => 20,
        DifficultyMode.Hard   => 15,
        _                     => StartingLives,
    };
    public const int ReaperMaxLives = 100;
    public const int MaxModifiersPerTower = 3;
    public const int DraftOptionsCount = 5;
    public const int DraftTowerOptions = 2;             // when free slots exist
    public const int DraftModifierOptions = 3;          // when free slots exist
    public const int DraftModifierOptionsFull = 4;      // when all slots occupied (< pool size keeps scarcity)

    // Premium card system -- tune here only.
    public const float PremiumCardChance         = 0.10f;  // 10% chance of any premium card per draft
    public const float SuperRarePremiumFraction  = 0.30f;  // of all premium appearances, 30% are Super Rare

    public const int MaxPremiumCardsPerRun       = 4;   // total premium cards that can appear in one run
    public const int MaxPremiumCardCopiesDefault = 3;   // default max copies of any single premium card per run
    public const int MaxPremiumModSlots          = 5;   // Expanded Chassis hard cap on mod slots per tower

    // Effect magnitudes -- tune here only.
    public const int   KineticCalibrationBonusDamage    = 1;     // +1 base damage to all towers
    public const float HotLoadersIntervalMultiplier      = 0.90f; // x0.90 attack interval (-10% = faster)
    public const float ExtendedRailsRangeBonus           = 30f;   // +30px range to all towers
    public const float MultitargetRelayChainRangeBonus   = 10f;   // +10px chain reach on all towers
    public const float LongFuseRadiusBonus               = 20f;   // +20px explosion/splash radius globally
    public const float SignalBoostMarkDurationBonus       = 1.5f;  // +1.5s mark duration globally
    public const float ColdCircuitSlowDurationMultiplier = 1.35f; // x1.35 slow duration (35% longer)
    public const int   EmergencyReservesLivesGain        = 5;     // immediate +5 lives
    public const int   HardenedReservesMaxLivesBonus     = 1;     // +1 max lives (also heals +1 immediately)
    public const int   BetterOddsBonusDraftCards         = 1;     // +1 card per draft

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
    public const float FeedbackLoopCooldownReductionPerCopy = 0.50f; // 50% cooldown reset per copy; 2 copies = 100% (full reset)
    public const float FeedbackLoopStimDuration             = 4.00f; // seconds the attack speed stim lasts after a kill
    public const float FeedbackLoopStimFactor               = 5f / 6f; // ×(5/6) attack interval on kill = +20% attack speed

    // Hair Trigger modifier
    public const float HairTriggerAttackSpeed = 1.30f; // +30% attack speed
    public const float HairTriggerRangeFactor = 0.82f; // -18% range

    // Overreach modifier
    public const float OverreachRangeFactor = 1.45f;  // +45% range
    public const float OverreachDamageFactor = 0.90f; // -10% damage

    // Exploit Weakness modifier
    public const float ExploitWeaknessDamageBonus = 1.45f; // ×1.45 damage to Marked enemies (+45%)

    // Overkill modifier
    public const float OverkillSpillEfficiency = 1.00f; // 100% excess damage spill

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

    // Blast Core modifier
    // Adjacent parallel path legs are 128px apart (CELL_H). Base radius must exceed 128px
    // for Blast Core to hit cross-leg targets -- the primary cluster scenario on snake paths.
    // At 110px it would almost never trigger against basic walkers (153px same-leg spacing).
    public const float BlastCoreRadius        = 140f;  // base splash radius in pixels (1 copy); exceeds 128px leg gap
    public const float BlastCoreRadiusPerCopy = 25f;   // radius per additional copy: 140 / 165 / 190px at 1/2/3 copies
    public const float BlastCoreDamageRatio   = 0.45f; // splash damage as fraction of primary hit's FinalDamage

    // Focus Lens modifier
    public const float FocusLensDamageBonus = 2.40f; // +140% damage
    public const float FocusLensAttackInterval = 1.85f; // x1.85 attack interval

    // Enemies - Swift Walker
    public const float SwiftHpMultiplier = 1.5f;  // 1.5× basic walker HP
    public const float SwiftEnemySpeed   = 240f;  // pixels per second (2× basic)

    // Waves
    public const float DefaultSpawnInterval = 1.275f;
    public const int DefaultEnemyCount = 12;
    // Basic walker pair grouping: walkers arrive in pairs separated by a short gap,
    // with a longer gap between pairs. Average = 1.0× so total wave density is unchanged.
    public const float BasicWalkerGroupShortInterval = 0.5f; // gap within a pair
    public const float BasicWalkerGroupLongInterval  = 1.5f; // gap between pairs

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
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyEnemyHpMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalEnemyHpMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardEnemyHpMultiplier, 0.1f, 5f),
        _ => 1.0f
    };

    public static float GetEnemyCountMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyEnemyCountMultiplier, 0.1f, 5f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalEnemyCountMultiplier, 0.1f, 5f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardEnemyCountMultiplier, 0.1f, 5f),
        _ => 1.0f
    };

    public static float GetSpawnIntervalMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasySpawnIntervalMultiplier, 0.2f, 3f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalSpawnIntervalMultiplier, 0.2f, 3f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardSpawnIntervalMultiplier, 0.2f, 3f),
        _ => 1.0f
    };

    public static float GetHpGrowthMultiplier(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy => ClampDifficultyMultiplier(SpectacleTuning.Current.EasyHpGrowthMultiplier, 0.5f, 2f),
        DifficultyMode.Normal => ClampDifficultyMultiplier(SpectacleTuning.Current.NormalHpGrowthMultiplier, 0.5f, 2f),
        DifficultyMode.Hard => ClampDifficultyMultiplier(SpectacleTuning.Current.HardHpGrowthMultiplier, 0.5f, 2f),
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

    // Accordion Engine tower
    public const float AccordionCompressionFactor        = 0.25f;  // 75% spread reduction per pulse
    public const float AccordionMinSpacingPx             = 8f;     // minimum path distance between enemies after compression
    public const int   AccordionMinEnemiesForCompression = 2;      // need at least 2 in range to compress
    public const float PhaseSplitterDamageRatio          = 0.65f;  // each dual-end primary hit deals 65% base damage

    // Rocket Launcher tower (direct explosive hits with built-in radial splash)
    public const float RocketLauncherSplashRadius            = 88f;  // base built-in splash radius
    public const float RocketLauncherSplashDamageRatio       = 0.45f; // splash damage as fraction of primary FinalDamage
    public const float RocketLauncherBlastCoreRadiusPerCopy = 24f;  // extra splash radius per Blast Core copy
    public const float RocketLauncherProjectileSpeed         = 620f; // px/s, readable but punchy rocket travel

    // Undertow Engine tower (battlefield control via path-progress pull)
    public const float UndertowDuration                    = 0.78f;  // active drag time
    public const float UndertowPullDistance                = 84f;    // baseline pull distance along path progress
    public const float UndertowPullDistanceCap             = 112f;   // hard cap before resist/DR
    public const float UndertowMinEffectivePull            = 12f;    // skip tiny pulls that read as jitter
    public const float UndertowSlowFactor                  = 0.28f;  // dragged target speed while active
    public const float UndertowSlowPerChillCopy            = 0.08f;  // extra slow strength per Chill Shot copy
    public const float UndertowFocusLensPullPerCopy        = 0.22f;  // stronger pull per Focus Lens copy
    public const float UndertowFocusLensSlowPerCopy        = 0.06f;  // stronger slow per Focus Lens copy
    public const float UndertowMarkedSusceptibilityBonus   = 0.12f;  // marked/debuffed targets are slightly easier to drag
    public const float UndertowRecentWindow                = 3.0f;   // DR timer after an undertow application
    public const float UndertowRecentMinMultiplier         = 0.42f;  // minimum pull multiplier under fresh DR
    public const float UndertowRetargetLockout             = 0.85f;  // short anti-stack lockout for same target
    public const float UndertowConcurrentExtraDecay        = 0.36f;  // additional multiplier per extra active undertow on same target
    public const float UndertowArmoredResistanceMultiplier = 0.62f;  // armored walkers resist pull
    public const float UndertowHeavyResistanceMultiplier   = 0.76f;  // reverse/shield/splitter heavies resist pull
    public const float UndertowSecondarySearchRadius       = 156f;   // nearby enemy search for split/chain secondary tug
    public const float UndertowSplitSecondaryMultiplier    = 0.46f;  // split-shot secondary tug strength
    public const float UndertowChainSecondaryMultiplier    = 0.56f;  // chain-reaction secondary tug strength
    public const float UndertowSecondaryDurationMultiplier = 0.72f;  // secondary tug is shorter than primary
    public const float UndertowEndpointBaseRadius          = 68f;    // tiny base endpoint compression pulse
    public const float UndertowEndpointRadiusPerBlastCore  = 26f;    // additional radius per Blast Core copy
    public const float UndertowEndpointBasePull            = 14f;    // tiny endpoint tug without Blast Core
    public const float UndertowEndpointPullPerBlastCore    = 12f;    // endpoint tug increase per Blast Core copy
    public const float UndertowEndpointSlowDuration        = 0.72f;  // short lingering slow from endpoint compression
    public const float UndertowEndpointSlowFactor          = 0.80f;  // mild endpoint slow
    public const float UndertowFeedbackFollowupChance      = 0.22f;  // chance per Feedback Loop copy for delayed tug
    public const float UndertowFeedbackFollowupDelay       = 0.38f;  // delayed follow-up tug timing
    public const float UndertowFeedbackFollowupMultiplier  = 0.40f;  // follow-up tug strength

    // Latch Nest tower (persistent parasite attrition)
    public const int   LatchNestMaxActiveParasitesPerTower = 6;
    public const int   LatchNestMaxParasitesPerHost        = 2;
    public const float LatchNestParasiteDuration           = 7.0f;
    public const float LatchNestParasiteTickInterval       = 0.45f;
    public const float LatchNestParasiteTickDamageMultiplier = 0.22f;

    // Wildfire modifier
    // Burn DPS = tower.BaseDamage × WildfireBurnDpsRatio (intentionally does NOT scale with FinalDamage
    // so Focus Lens / Momentum amplification doesn't create runaway burn values).
    public const float WildfireBurnDuration      = 4.0f;   // seconds the burn status lasts
    public const float WildfireBurnDpsRatio      = 0.25f;  // burn DPS as fraction of tower BaseDamage
    public const float WildfireTrailDropInterval = 0.65f;  // seconds between trail segment deposits
    public const float WildfireTrailLifetime     = 2.2f;   // seconds a trail segment persists
    public const float WildfireTrailDamageRatio  = 0.40f;  // trail DPS = burnDPS × this
    public const float WildfireTrailRadius       = 30f;    // overlap radius for trail damage (px)
    public const int   WildfireMaxTrailSegments  = 20;     // global cap per wave (prevents perf blowup with Hair Trigger)

    // Deadzone modifier
    // Identity: primary hits leave a short-lived spatial trap at the impact point.
    // The first enemy to cross into the armed zone is pinned (speed = 0) for a duration, then resumes.
    // Structural guardrails:
    //  - one active zone per tower (new hit replaces old zone)
    //  - primary hits only (no chain/split seeding; ApplyToChainTargets=false)
    //  - arm time + enemy snapshot prevents same-frame trigger from the hit enemy itself
    //  - zone expires after DeadzoneLifetime if uncrossed (not a permanent hazard)
    public const float DeadzoneLifetime = 2.5f;             // seconds zone persists before expiring
    public const float DeadzoneArmTime = 0.12f;             // seconds before zone can be triggered
    public const float DeadzoneTriggerRadius = 38f;         // enemy must be within this px radius
    public const float DeadzonePinDuration = 1.2f;          // seconds enemy is pinned on trigger
    public const float DeadzonePinDurationPerCopy = 0.35f;  // extra pin seconds per additional copy

    // Afterimage modifier
    // Identity: hit now, leave short-lived ghost imprint, trigger one delayed reduced echo from that spot.
    // Structural guardrails:
    //  - one active imprint per tower (new replaces old)
    //  - one trigger only (no ticking field / no persistent hazard)
    //  - echo damage is reduced and bounded per tower expression
    public const float AfterimageDelaySeconds = 0.82f;
    public const float AfterimageBaseDamageRatio = 0.42f;
    public const float AfterimageBaseRadius = 86f;
    public const float AfterimageMinDamage = 1f;
    public const int   AfterimageMaxTargetsPerEcho = 4;
    public const float AfterimageHeavyBurstRadius = 72f;
    public const float AfterimageRocketBurstRadius = 88f;
    public const float AfterimageRiftBurstRadius = 78f;
    public const float AfterimageChainRangeMultiplier = 0.62f;
    public const float AfterimageChainBounceDamageDecay = 0.60f;
    public const float AfterimageMarkerPulseRadius = 96f;
    public const float AfterimageMarkerPulseMarkDuration = 2.4f;
    public const float AfterimageUndertowPulseRadius = 90f;
    public const float AfterimageUndertowPullDistance = 18f;
    public const float AfterimageUndertowSlowDuration = 0.95f;
    public const float AfterimageUndertowSlowFactor = 0.84f;
    public const float AfterimageAccordionPulseRadius = 104f;

    // Reaper Protocol modifier
    // Full game only; excluded from demo builds and demo bot simulations.
    // Per-instance per-wave cap: each copy of the modifier tracks kills independently.
    // Expected usage: one copy per tower (stacking is legal but each copy caps at 5).
    public const int ReaperProtocolKillCap      = 5;  // max life-restoring kills per modifier copy per wave
    public const int ReaperProtocolMinWaveIndex = 4;  // 0-based; first eligible in draft at wave 5 (display)

    /// <summary>
    /// Returns the minimum 0-based wave index at which a modifier may appear in the draft pool.
    /// Returns 0 (always eligible) for all modifiers that have no wave gate.
    /// </summary>
    public static int GetModifierMinWaveIndex(string modifierId)
        => modifierId == "reaper_protocol" ? ReaperProtocolMinWaveIndex : 0;

    // Tower wave gates -- control towers hidden until the player has built enough context to use them.
    public const int UndertowEngineMinWaveIndex  = 4;  // 0-based; first eligible in draft at wave 5 (display)
    public const int AccordionEngineMinWaveIndex = 4;  // 0-based; first eligible in draft at wave 5 (display)

    /// <summary>
    /// Returns the minimum 0-based wave index at which a tower may appear in the draft pool.
    /// Returns 0 (always eligible) for towers that have no wave gate.
    /// </summary>
    public static int GetTowerMinWaveIndex(string towerId) => towerId switch
    {
        "undertow_engine"  => UndertowEngineMinWaveIndex,
        "accordion_engine" => AccordionEngineMinWaveIndex,
        _                  => 0,
    };

    private static float ClampDifficultyMultiplier(float value, float min, float max)
        => value < min ? min : (value > max ? max : value);
}
