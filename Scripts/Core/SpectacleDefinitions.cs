using System;
using System.Collections.Generic;

namespace SlotTheory.Core;

public enum SpectacleMode
{
    Single,
    Combo,
    Triad,
}

public enum ProcIntensity { Low, Medium, High }

public enum SpectacleAugmentKind
{
    Area,
    Strike,
    Reload,
}

/// <summary>
/// Gameplay payload family for a modifier. Determines how the modifier
/// contributes to the combo finisher. Two modifiers in the same family
/// share a dispatch path; only 18 primitive pairs replace the previous
/// 45 hand-written combo cases.
/// </summary>
public enum SurgePrimitive
{
    Burst,    // Area/falloff damage (Overkill, Overreach, BlastCore)
    Chain,    // Chain arc bounces (ChainReaction)
    Beam,     // Heavy single-target strike (FocusLens)
    Status,   // Mark or slow application (ExploitWeakness, ChillShot)
    Reload,   // Cooldown reduction + follow-up (Momentum, HairTrigger, FeedbackLoop)
    Scatter,  // Multi-target split fire (SplitShot)
}

public readonly record struct SpectacleSingleDef(string EffectId, string Name);
public readonly record struct SpectacleComboDef(string EffectId, string Name);
public readonly record struct SpectacleTriadAugmentDef(
    string EffectId,
    string Name,
    float Coefficient,
    float DurationSec,
    SpectacleAugmentKind Kind);
public static class SpectacleDefinitions
{
    // Canonical mod IDs used by runtime systems.
    // Note: "slow" is Chill Shot in data/UI naming.
    public const string Momentum = "momentum";
    public const string Overkill = "overkill";
    public const string ExploitWeakness = "exploit_weakness";
    public const string FocusLens = "focus_lens";
    public const string ChillShot = "slow";
    public const string Overreach = "overreach";
    public const string HairTrigger = "hair_trigger";
    public const string SplitShot = "split_shot";
    public const string FeedbackLoop = "feedback_loop";
    public const string ChainReaction = "chain_reaction";
    public const string BlastCore      = "blast_core";
    public const string Wildfire       = "wildfire";
    public const string Afterimage     = "afterimage";
    public const string ReaperProtocol = "reaper_protocol";
    public const string Deadzone       = "deadzone";

    public const float SurgeThreshold = 150f;
    public const float SurgeCooldownSeconds = 6.0f;
    public const float SurgeMeterAfterTrigger = 10f;
    public const float GlobalMeterPerSurge = 10f;
    public const float GlobalThreshold = 200f;
    public const float GlobalMeterAfterTrigger = 0f;
    public const float InactivityGraceSeconds = 2f;
    public const float InactivityDecayPerSecond = 3f;
    // Fill is intentionally slower so spectacle pacing is easier to read:
    // ~30% longer than baseline (rate = baseline / 1.3).
    public const float MeterGainScale = 0.75f / 1.30f;
    // Per-mod-per-tower cooldown: minimum seconds between contributions from the same mod on the same tower.
    public const float ModProcCooldownSeconds = 0.25f;

    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        Momentum,
        Overkill,
        ExploitWeakness,
        FocusLens,
        ChillShot,
        Overreach,
        HairTrigger,
        SplitShot,
        FeedbackLoop,
        ChainReaction,
        BlastCore,
        Wildfire,
        Afterimage,
        ReaperProtocol,
        Deadzone,
    };

    private static readonly HashSet<string> SupportedTowers = new(StringComparer.Ordinal)
    {
        "rapid_shooter",
        "heavy_cannon",
        "rocket_launcher",
        "marker_tower",
        "chain_tower",
        "rift_prism",
        "accordion_engine",
        "phase_splitter",
        "undertow_engine",
        "latch_nest",
    };

    // Compressed gain spread to keep triad charge pacing more consistent across modifier groups.
    private static readonly Dictionary<string, float> BaseGain = new(StringComparer.Ordinal)
    {
        [Momentum] = 2.4f,
        [Overkill] = 2.9f,
        [ExploitWeakness] = 3.0f,
        [FocusLens] = 2.7f,
        [ChillShot] = 2.4f,
        [Overreach] = 2.5f,
        [HairTrigger] = 2.2f,
        [SplitShot] = 2.1f,
        [FeedbackLoop] = 3.1f,
        [ChainReaction] = 2.2f,
        [BlastCore]      = 2.3f,
        // Wildfire: gains per ignition hit -- moderate frequency, trail procs add up
        [Wildfire]       = 2.2f,
        [Afterimage]     = 2.4f,
        // Reaper: gains per kill event -- infrequent but high-value
        [ReaperProtocol] = 3.2f,
        // Deadzone: gains per trigger crossing -- moderate frequency, synergy-dependent
        [Deadzone]       = 2.3f,
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.Ordinal)
    {
        [Momentum] = "Momentum",
        [Overkill] = "Overkill",
        [ExploitWeakness] = "Exploit Weakness",
        [FocusLens] = "Focus Lens",
        [ChillShot] = "Chill Shot",
        [Overreach] = "Overreach",
        [HairTrigger] = "Hair Trigger",
        [SplitShot] = "Split Shot",
        [FeedbackLoop] = "Feedback Loop",
        [ChainReaction]  = "Chain Reaction",
        [BlastCore]      = "Blast Core",
        [Wildfire]       = "Wildfire",
        [Afterimage]     = "Afterimage",
        [ReaperProtocol] = "Reaper Protocol",
        [Deadzone]       = "Deadzone",
    };

    // Single surge names: primary mod name is always the first word so the player can
    // instantly connect the combat callout back to the mod on their tower.
    private static readonly Dictionary<string, SpectacleSingleDef> SingleDefs = new(StringComparer.Ordinal)
    {
        [Momentum]       = new SpectacleSingleDef("S_MOMENTUM_REDLINE_BREAK",   "Momentum Break"),
        [Overkill]       = new SpectacleSingleDef("S_OVERKILL_SPILL_CASCADE",   "Overkill Cascade"),
        [ExploitWeakness]= new SpectacleSingleDef("S_EXPLOIT_MARK_COLLAPSE",    "Exploit Collapse"),
        [FocusLens]      = new SpectacleSingleDef("S_FOCUS_PRISM_LANCE",        "Focus Lance"),
        [ChillShot]      = new SpectacleSingleDef("S_CHILL_ABSOLUTE_ZERO",      "Absolute Zero"),
        [Overreach]      = new SpectacleSingleDef("S_OVERREACH_HORIZON_SWEEP",  "Overreach Sweep"),
        [HairTrigger]    = new SpectacleSingleDef("S_HAIR_HYPERBURST",          "Hyperburst"),
        [SplitShot]      = new SpectacleSingleDef("S_SPLIT_FRACTAL_BLOOM",      "Split Bloom"),
        [FeedbackLoop]   = new SpectacleSingleDef("S_FEEDBACK_REBOOT_STORM",    "Feedback Storm"),
        [ChainReaction]  = new SpectacleSingleDef("S_CHAIN_ARC_OVERLOAD",       "Arc Overload"),
        [BlastCore]      = new SpectacleSingleDef("S_BLAST_DETONATION_ZONE",    "Detonation Zone"),
        [Wildfire]       = new SpectacleSingleDef("S_WILDFIRE_FIRESTORM",       "Firestorm"),
        [Afterimage]     = new SpectacleSingleDef("S_AFTERIMAGE_ECHO_SCAR",     "Echo Scar"),
        [ReaperProtocol] = new SpectacleSingleDef("S_REAPER_DEATH_DECREE",      "Death Decree"),
        [Deadzone]       = new SpectacleSingleDef("S_DEADZONE_FAULT_COLLAPSE",  "Fault Collapse"),
    };

    // Combo naming: [PrimaryShortName] [SecondaryTag] -- always shows primary mod identity + how the secondary mutates it.
    private static readonly Dictionary<string, string> PrimaryShortNames = new(StringComparer.Ordinal)
    {
        [Momentum]        = "Momentum",
        [Overkill]        = "Overkill",
        [ExploitWeakness] = "Exploit",
        [FocusLens]       = "Focus",
        [ChillShot]       = "Chill",
        [Overreach]       = "Overreach",
        [HairTrigger]     = "Hair Trigger",
        [SplitShot]       = "Split Shot",
        [FeedbackLoop]    = "Feedback",
        [ChainReaction]   = "Chain",
        [BlastCore]       = "Blast",
        [Wildfire]        = "Wildfire",
        [Afterimage]      = "Afterimage",
        [ReaperProtocol]  = "Reaper",
        [Deadzone]        = "Deadzone",
    };

    private static readonly Dictionary<string, string> SecondaryTags = new(StringComparer.Ordinal)
    {
        [Momentum]        = "Surge",
        [Overkill]        = "Spill",
        [ExploitWeakness] = "Mark",
        [FocusLens]       = "Beam",
        [ChillShot]       = "Cryo",
        [Overreach]       = "Range",
        [HairTrigger]     = "Burst",
        [SplitShot]       = "Split",
        [FeedbackLoop]    = "Reload",
        [ChainReaction]   = "Arc",
        [BlastCore]       = "Blast",
        [Wildfire]        = "Fire",
        [Afterimage]      = "Echo",
        [ReaperProtocol]  = "Death",
        [Deadzone]        = "Zone",
    };

    private static readonly Dictionary<string, SpectacleTriadAugmentDef> TriadAugments = new(StringComparer.Ordinal)
    {
        // Player-facing triad accents are intentionally collapsed to 3 names:
        // Pulse / Strike / Recharge.
        [Momentum]       = new SpectacleTriadAugmentDef("T_AUG_MOMENTUM",  "Recharge", 0.18f, 2.0f, SpectacleAugmentKind.Reload),
        [HairTrigger]    = new SpectacleTriadAugmentDef("T_AUG_HAIR",      "Recharge", 0.24f, 1.6f, SpectacleAugmentKind.Reload),
        [FeedbackLoop]   = new SpectacleTriadAugmentDef("T_AUG_FEEDBACK",  "Recharge", 0.35f, 0.0f, SpectacleAugmentKind.Reload),
        [ReaperProtocol] = new SpectacleTriadAugmentDef("T_AUG_REAPER",    "Strike",   0.28f, 0.0f, SpectacleAugmentKind.Strike),
        [ExploitWeakness]= new SpectacleTriadAugmentDef("T_AUG_EXPLOIT",   "Strike",   0.20f, 2.2f, SpectacleAugmentKind.Strike),
        [FocusLens]      = new SpectacleTriadAugmentDef("T_AUG_FOCUS",     "Strike",   0.25f, 0.0f, SpectacleAugmentKind.Strike),
        [Overkill]       = new SpectacleTriadAugmentDef("T_AUG_OVERKILL",  "Pulse",    0.22f, 1.8f, SpectacleAugmentKind.Area),
        [ChillShot]      = new SpectacleTriadAugmentDef("T_AUG_CHILL",     "Pulse",    0.20f, 2.0f, SpectacleAugmentKind.Area),
        [Overreach]      = new SpectacleTriadAugmentDef("T_AUG_OVERREACH", "Pulse",    0.22f, 2.0f, SpectacleAugmentKind.Area),
        [SplitShot]      = new SpectacleTriadAugmentDef("T_AUG_SPLIT",     "Pulse",    0.30f, 0.0f, SpectacleAugmentKind.Area),
        [ChainReaction]  = new SpectacleTriadAugmentDef("T_AUG_CHAIN",     "Pulse",    0.28f, 1.8f, SpectacleAugmentKind.Area),
        [BlastCore]      = new SpectacleTriadAugmentDef("T_AUG_BLAST",     "Pulse",    0.22f, 1.8f, SpectacleAugmentKind.Area),
        [Wildfire]       = new SpectacleTriadAugmentDef("T_AUG_WILDFIRE",  "Pulse",    0.24f, 2.5f, SpectacleAugmentKind.Area),
        [Afterimage]     = new SpectacleTriadAugmentDef("T_AUG_AFTERIMAGE","Pulse",    0.24f, 2.0f, SpectacleAugmentKind.Area),
        [Deadzone]       = new SpectacleTriadAugmentDef("T_AUG_DEADZONE",  "Pulse",    0.22f, 1.8f, SpectacleAugmentKind.Area),
    };

    public static IReadOnlyCollection<string> SupportedModIds => Supported;
    public static IReadOnlyCollection<string> SupportedTowerIds => SupportedTowers;

    public static string NormalizeModId(string modifierId)
    {
        if (string.Equals(modifierId, "chill_shot", StringComparison.Ordinal))
            return ChillShot;
        return modifierId;
    }

    public static string NormalizeTowerId(string towerId)
        => (towerId ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsSupported(string modifierId) => Supported.Contains(NormalizeModId(modifierId));
    public static bool IsSupportedTowerId(string towerId) => SupportedTowers.Contains(NormalizeTowerId(towerId));

    public static float GetBaseGain(string modifierId)
    {
        string normalized = NormalizeModId(modifierId);
        float baseGain = BaseGain.GetValueOrDefault(normalized, 0f);
        if (baseGain <= 0f)
            return 0f;

        float perModMultiplier = SpectacleTuning.Current.ResolveGainMultiplier(normalized);
        return baseGain * MathF.Max(0f, perModMultiplier);
    }

    public static float ResolveSurgeThreshold(string? towerId = null)
    {
        float global = MathF.Max(0.05f, SurgeThreshold * MathF.Max(0.05f, SpectacleTuning.Current.SurgeThresholdMultiplier));
        if (string.IsNullOrWhiteSpace(towerId))
            return global;
        return MathF.Max(0.05f, global * SpectacleTuning.Current.ResolveTowerSurgeThresholdMultiplier(towerId));
    }

    public static float ResolveSurgeCooldownSeconds()
        => MathF.Max(0f, SurgeCooldownSeconds * MathF.Max(0f, SpectacleTuning.Current.SurgeCooldownMultiplier));

    public static float ResolveSurgeMeterAfterTrigger()
        => MathF.Max(0f, SurgeMeterAfterTrigger * MathF.Max(0f, SpectacleTuning.Current.SurgeMeterAfterTriggerMultiplier));

    public static float ResolveGlobalMeterPerSurge()
        => MathF.Max(0f, GlobalMeterPerSurge * MathF.Max(0f, SpectacleTuning.Current.GlobalMeterPerSurgeMultiplier));

    public static float ResolveGlobalThreshold()
        => MathF.Max(0.05f, GlobalThreshold * MathF.Max(0.05f, SpectacleTuning.Current.GlobalThresholdMultiplier));

    public static float ResolveGlobalMeterAfterTrigger()
        => MathF.Max(0f, GlobalMeterAfterTrigger * MathF.Max(0f, SpectacleTuning.Current.GlobalMeterAfterTriggerMultiplier));

    public static float ResolveInactivityGraceSeconds()
        => MathF.Max(0f, InactivityGraceSeconds * MathF.Max(0f, SpectacleTuning.Current.InactivityGraceMultiplier));

    public static float ResolveInactivityDecayPerSecond()
        => MathF.Max(0f, InactivityDecayPerSecond * MathF.Max(0f, SpectacleTuning.Current.InactivityDecayMultiplier));

    public static float ResolveMeterGainScale()
        => MeterGainScale * MathF.Max(0f, SpectacleTuning.Current.MeterGainMultiplier);

    public static float ResolveTowerMeterGainMultiplier(string? towerId)
    {
        if (string.IsNullOrWhiteSpace(towerId))
            return 1f;
        return MathF.Max(0f, SpectacleTuning.Current.ResolveTowerMeterGainMultiplier(towerId));
    }

    /// <summary>
    /// Returns the gameplay primitive family for the given modifier.
    /// Used by the combo finisher to dispatch by primitive pair (18 cases)
    /// rather than by explicit modifier pair (was 45 cases).
    /// </summary>
    public static SurgePrimitive PrimitiveOf(string modId) => NormalizeModId(modId) switch
    {
        Overkill         => SurgePrimitive.Burst,
        Overreach        => SurgePrimitive.Burst,
        BlastCore        => SurgePrimitive.Burst,
        ChainReaction    => SurgePrimitive.Chain,
        FocusLens        => SurgePrimitive.Beam,
        ExploitWeakness  => SurgePrimitive.Status,
        ChillShot        => SurgePrimitive.Status,
        Momentum         => SurgePrimitive.Reload,
        HairTrigger      => SurgePrimitive.Reload,
        FeedbackLoop     => SurgePrimitive.Reload,
        SplitShot        => SurgePrimitive.Scatter,
        Wildfire         => SurgePrimitive.Status,
        Afterimage       => SurgePrimitive.Burst,
        ReaperProtocol   => SurgePrimitive.Reload,
        Deadzone         => SurgePrimitive.Burst,
        _                => SurgePrimitive.Burst,
    };

    public static string GetDisplayName(string modifierId)
        => DisplayNames.GetValueOrDefault(NormalizeModId(modifierId), modifierId);

    public static float GetCopyMultiplier(int copies) => copies switch
    {
        <= 0 => 0f,
        1 => 1.00f,
        2 => 1.92f,
        _ => 2.70f,
    } * MathF.Max(0f, SpectacleTuning.Current.CopyMultiplierScale);

    public static float GetModeBase(SpectacleMode mode) => mode switch
    {
        SpectacleMode.Single => 1.00f,
        SpectacleMode.Combo => 1.10f,
        _ => 1.20f,
    };

    private const float ProcScalarLow    = 0.75f;
    private const float ProcScalarMedium = 1.00f;
    private const float ProcScalarHigh   = 1.35f;

    /// <summary>
    /// Returns the fixed proc intensity scalar for a modifier.
    /// Replaces per-mod bespoke event scalar formulas with 3 flat buckets.
    /// Low (0.75): high-frequency procs. Medium (1.00): conditional hits.
    /// High (1.35): infrequent/high-value events.
    /// </summary>
    public static float GetProcScalar(string modId) => NormalizeModId(modId) switch
    {
        HairTrigger     => ProcScalarLow,
        SplitShot       => ProcScalarLow,
        ChainReaction   => ProcScalarLow,
        BlastCore       => ProcScalarLow,
        Wildfire        => ProcScalarLow,
        Afterimage      => ProcScalarLow,
        Deadzone        => ProcScalarLow,
        Overkill        => ProcScalarHigh,
        FocusLens       => ProcScalarHigh,
        ReaperProtocol  => ProcScalarHigh,
        _               => ProcScalarMedium,
    };

    public static SpectacleSingleDef GetSingle(string modifierId)
        => SingleDefs.GetValueOrDefault(NormalizeModId(modifierId),
            new SpectacleSingleDef($"S_{NormalizeModId(modifierId).ToUpperInvariant()}", GetDisplayName(modifierId)));

    /// <summary>
    /// Returns a combo surge definition for the given role pair.
    /// r1 must be the primary mod (highest copy count, then canonical rank).
    /// The name is generated as "[PrimaryShortName] [SecondaryTag]" so the player
    /// can always identify the primary mod and how the secondary mutates it.
    /// </summary>
    public static SpectacleComboDef GetCombo(string r1, string r2)
    {
        string n1 = NormalizeModId(r1);
        string n2 = NormalizeModId(r2);
        string primary   = PrimaryShortNames.GetValueOrDefault(n1, GetDisplayName(n1));
        string secondary = SecondaryTags.GetValueOrDefault(n2, GetDisplayName(n2));
        return new SpectacleComboDef(
            $"C_{n1.ToUpperInvariant()}_{n2.ToUpperInvariant()}",
            $"{primary} {secondary}");
    }

    public static SpectacleTriadAugmentDef GetTriadAugment(string modifierId)
        => TriadAugments.GetValueOrDefault(NormalizeModId(modifierId),
            new SpectacleTriadAugmentDef($"T_AUG_{NormalizeModId(modifierId).ToUpperInvariant()}", GetDisplayName(modifierId), 0.15f, 1.5f, SpectacleAugmentKind.Area));

}
