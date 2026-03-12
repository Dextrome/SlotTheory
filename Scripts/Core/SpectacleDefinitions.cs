using System;
using System.Collections.Generic;

namespace SlotTheory.Core;

public enum SpectacleMode
{
    Single,
    Combo,
    Triad,
}

public enum SpectacleAugmentKind
{
    RampCap,
    SpillTransfer,
    MarkedVulnerability,
    BeamBurst,
    SlowIntensity,
    RangePulse,
    AttackSpeed,
    SplitVolley,
    CooldownRefund,
    ChainBounces,
}

public readonly record struct SpectacleSingleDef(string EffectId, string Name);
public readonly record struct SpectacleComboDef(string EffectId, string Name);
public readonly record struct SpectacleTriadAugmentDef(
    string EffectId,
    string Name,
    float Coefficient,
    float DurationSec,
    SpectacleAugmentKind Kind);
public readonly record struct SpectacleTokenConfig(float Cap, float RegenPerSecond);

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

    public const float SurgeThreshold = 145f;
    public const float SurgeCooldownSeconds = 6.0f;
    public const float SurgeMeterAfterTrigger = 18f;
    public const float GlobalMeterPerSurge = 10f;
    public const float GlobalThreshold = 100f;
    public const float GlobalMeterAfterTrigger = 20f;
    public const float GlobalContributionWindowSeconds = 6f;
    public const float InactivityGraceSeconds = 2f;
    public const float InactivityDecayPerSecond = 6f;
    public const float ContributionWindowSeconds = 20f;
    // Fill is intentionally slower so spectacle pacing is easier to read:
    // ~30% longer than baseline (rate = baseline / 1.3).
    public const float MeterGainScale = 0.75f / 1.30f;
    // Damage-aware meter gain normalization so surge pacing is less dominated by hit frequency alone.
    public const float MeterDamageReference = 20f;
    public const float MeterDamageWeight = 0.72f;
    public const float MeterDamageMinMultiplier = 0.65f;
    public const float MeterDamageMaxMultiplier = 1.75f;

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
    };

    private static readonly Dictionary<string, SpectacleTokenConfig> TokenConfig = new(StringComparer.Ordinal)
    {
        [Momentum] = new SpectacleTokenConfig(5f, 6.0f),
        [Overkill] = new SpectacleTokenConfig(4f, 4.0f),
        [ExploitWeakness] = new SpectacleTokenConfig(3f, 3.0f),
        [FocusLens] = new SpectacleTokenConfig(4f, 4.0f),
        [ChillShot] = new SpectacleTokenConfig(4f, 5.0f),
        [Overreach] = new SpectacleTokenConfig(4f, 4.0f),
        [HairTrigger] = new SpectacleTokenConfig(4f, 5.0f),
        [SplitShot] = new SpectacleTokenConfig(3f, 4.0f),
        [FeedbackLoop] = new SpectacleTokenConfig(2f, 2.0f),
        [ChainReaction] = new SpectacleTokenConfig(3f, 4.0f),
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
        [ChainReaction] = "Chain Reaction",
    };

    private static readonly Dictionary<string, SpectacleSingleDef> SingleDefs = new(StringComparer.Ordinal)
    {
        [Momentum] = new SpectacleSingleDef("S_MOMENTUM_REDLINE_BREAK", "Redline Break"),
        [Overkill] = new SpectacleSingleDef("S_OVERKILL_SPILL_CASCADE", "Spill Cascade"),
        [ExploitWeakness] = new SpectacleSingleDef("S_EXPLOIT_MARK_COLLAPSE", "Mark Collapse"),
        [FocusLens] = new SpectacleSingleDef("S_FOCUS_PRISM_LANCE", "Prism Lance"),
        [ChillShot] = new SpectacleSingleDef("S_CHILL_ABSOLUTE_ZERO", "Absolute Zero"),
        [Overreach] = new SpectacleSingleDef("S_OVERREACH_HORIZON_SWEEP", "Horizon Sweep"),
        [HairTrigger] = new SpectacleSingleDef("S_HAIR_HYPERBURST", "Hyperburst"),
        [SplitShot] = new SpectacleSingleDef("S_SPLIT_FRACTAL_BLOOM", "Fractal Bloom"),
        [FeedbackLoop] = new SpectacleSingleDef("S_FEEDBACK_REBOOT_STORM", "Reboot Storm"),
        [ChainReaction] = new SpectacleSingleDef("S_CHAIN_GRID_OVERLOAD", "Grid Overload"),
    };

    private static readonly Dictionary<string, SpectacleComboDef> ComboDefs = BuildComboDefs();

    private static readonly Dictionary<string, SpectacleTriadAugmentDef> TriadAugments = new(StringComparer.Ordinal)
    {
        [Momentum] = new SpectacleTriadAugmentDef("T_AUG_MOMENTUM", "Momentum Surge", 0.18f, 2.0f, SpectacleAugmentKind.RampCap),
        [Overkill] = new SpectacleTriadAugmentDef("T_AUG_OVERKILL", "Spill Multiplier", 0.22f, 1.8f, SpectacleAugmentKind.SpillTransfer),
        [ExploitWeakness] = new SpectacleTriadAugmentDef("T_AUG_EXPLOIT", "Marked Execute", 0.20f, 2.2f, SpectacleAugmentKind.MarkedVulnerability),
        [FocusLens] = new SpectacleTriadAugmentDef("T_AUG_FOCUS", "Beam Spike", 0.25f, 0.0f, SpectacleAugmentKind.BeamBurst),
        [ChillShot] = new SpectacleTriadAugmentDef("T_AUG_CHILL", "Cryo Saturation", 0.20f, 2.0f, SpectacleAugmentKind.SlowIntensity),
        [Overreach] = new SpectacleTriadAugmentDef("T_AUG_OVERREACH", "Range Pulse", 0.22f, 2.0f, SpectacleAugmentKind.RangePulse),
        [HairTrigger] = new SpectacleTriadAugmentDef("T_AUG_HAIR", "Overclock Injection", 0.24f, 1.6f, SpectacleAugmentKind.AttackSpeed),
        [SplitShot] = new SpectacleTriadAugmentDef("T_AUG_SPLIT", "Split Volley", 0.30f, 0.0f, SpectacleAugmentKind.SplitVolley),
        [FeedbackLoop] = new SpectacleTriadAugmentDef("T_AUG_FEEDBACK", "Cooldown Reclaim", 0.35f, 0.0f, SpectacleAugmentKind.CooldownRefund),
        [ChainReaction] = new SpectacleTriadAugmentDef("T_AUG_CHAIN", "Chain Charge", 0.28f, 1.8f, SpectacleAugmentKind.ChainBounces),
    };

    public static IReadOnlyCollection<string> SupportedModIds => Supported;

    public static string NormalizeModId(string modifierId)
    {
        if (string.Equals(modifierId, "chill_shot", StringComparison.Ordinal))
            return ChillShot;
        return modifierId;
    }

    public static bool IsSupported(string modifierId) => Supported.Contains(NormalizeModId(modifierId));

    public static float GetBaseGain(string modifierId)
    {
        string normalized = NormalizeModId(modifierId);
        float baseGain = BaseGain.GetValueOrDefault(normalized, 0f);
        if (baseGain <= 0f)
            return 0f;

        float perModMultiplier = SpectacleTuning.Current.ResolveGainMultiplier(normalized);
        return baseGain * MathF.Max(0f, perModMultiplier);
    }

    public static float ResolveMeterGainScale()
        => MeterGainScale * MathF.Max(0f, SpectacleTuning.Current.MeterGainMultiplier);

    public static float ResolveDamageMeterMultiplier(float eventDamage)
    {
        if (eventDamage <= 0f)
            return 1f;

        float normalized = Clamp(eventDamage / MathF.Max(0.001f, MeterDamageReference), 0.10f, 4.0f);
        float blended = (1f - MeterDamageWeight) + MeterDamageWeight * normalized;
        return Clamp(blended, MeterDamageMinMultiplier, MeterDamageMaxMultiplier);
    }

    public static SpectacleTokenConfig GetTokenConfig(string modifierId)
        => TokenConfig.GetValueOrDefault(NormalizeModId(modifierId), new SpectacleTokenConfig(0f, 0f));

    public static string GetDisplayName(string modifierId)
        => DisplayNames.GetValueOrDefault(NormalizeModId(modifierId), modifierId);

    public static float GetCopyMultiplier(int copies) => copies switch
    {
        <= 0 => 0f,
        1 => 1.00f,
        2 => 1.92f,
        _ => 2.70f,
    };

    public static float GetDiversityMultiplier(int uniqueCount) => uniqueCount switch
    {
        <= 1 => 1.00f,
        2 => 1.08f,
        _ => 1.16f,
    };

    public static float GetModeBase(SpectacleMode mode) => mode switch
    {
        SpectacleMode.Single => 1.00f,
        SpectacleMode.Combo => 1.08f,
        _ => 1.16f,
    };

    public static SpectacleSingleDef GetSingle(string modifierId)
        => SingleDefs.GetValueOrDefault(NormalizeModId(modifierId),
            new SpectacleSingleDef($"S_{NormalizeModId(modifierId).ToUpperInvariant()}", GetDisplayName(modifierId)));

    public static SpectacleComboDef GetCombo(string modA, string modB)
    {
        string key = PairKey(modA, modB);
        if (ComboDefs.TryGetValue(key, out var def))
            return def;
        string a = GetDisplayName(modA);
        string b = GetDisplayName(modB);
        return new SpectacleComboDef($"C_{NormalizeModId(modA).ToUpperInvariant()}_{NormalizeModId(modB).ToUpperInvariant()}", $"{a} x {b}");
    }

    public static SpectacleTriadAugmentDef GetTriadAugment(string modifierId)
        => TriadAugments.GetValueOrDefault(NormalizeModId(modifierId),
            new SpectacleTriadAugmentDef($"T_AUG_{NormalizeModId(modifierId).ToUpperInvariant()}", GetDisplayName(modifierId), 0.15f, 1.5f, SpectacleAugmentKind.RangePulse));

    public static float MomentumEventScalar(float stackNorm)
        => 0.75f + 0.50f * Clamp01(stackNorm);

    public static float OverkillEventScalar(float spillRatio)
        => Clamp(0.65f + 0.55f * spillRatio, 0.65f, 1.80f);

    public static float ExploitWeaknessEventScalar(bool markedHit, bool markedKill)
        => 0.70f + 0.65f * (markedHit ? 1f : 0f) + 0.45f * (markedKill ? 1f : 0f);

    public static float FocusLensEventScalar(float damageNorm)
        => Clamp(0.85f + 0.45f * Clamp(damageNorm, 0f, 2f), 0.85f, 1.75f);

    public static float ChillEventScalar(int affectedEnemies)
        => Clamp(0.80f + 0.20f * affectedEnemies, 0.80f, 1.80f);

    public static float OverreachEventScalar(float rangeNorm)
        => 0.85f + 0.40f * Clamp(rangeNorm, 0f, 1.5f);

    public static float HairTriggerEventScalar(float streakNorm)
        => 0.70f + 0.30f * Clamp(streakNorm, 0f, 2f);

    public static float SplitShotEventScalar(int extraHits)
        => Clamp(0.70f + 0.15f * extraHits, 0.70f, 1.75f);

    public static float FeedbackLoopEventScalar(float refundFrac)
        => Clamp(0.90f + 1.20f * refundFrac, 0.90f, 2.00f);

    public static float ChainReactionEventScalar(int bounces)
        => Clamp(0.75f + 0.18f * bounces, 0.75f, 1.85f);

    private static float Clamp01(float v) => Clamp(v, 0f, 1f);
    private static float Clamp(float v, float min, float max) => MathF.Min(max, MathF.Max(min, v));

    private static string PairKey(string a, string b)
    {
        string na = NormalizeModId(a);
        string nb = NormalizeModId(b);
        return string.CompareOrdinal(na, nb) <= 0 ? $"{na}|{nb}" : $"{nb}|{na}";
    }

    private static Dictionary<string, SpectacleComboDef> BuildComboDefs()
    {
        var map = new Dictionary<string, SpectacleComboDef>(StringComparer.Ordinal);

        static void Add(Dictionary<string, SpectacleComboDef> m, string a, string b, string id, string name)
        {
            string key = string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
            m[key] = new SpectacleComboDef(id, name);
        }

        Add(map, Momentum, Overkill, "C_MOMENTUM_OVERKILL", "Redline Spillstorm");
        Add(map, Momentum, ExploitWeakness, "C_MOMENTUM_EXPLOIT", "Predatory Ramp");
        Add(map, Momentum, FocusLens, "C_MOMENTUM_FOCUS", "Siege Tempo");
        Add(map, Momentum, ChillShot, "C_MOMENTUM_CHILL", "Glacial Grind");
        Add(map, Momentum, Overreach, "C_MOMENTUM_OVERREACH", "Longline Pressure");
        Add(map, Momentum, HairTrigger, "C_MOMENTUM_HAIR", "Ramped Overclock");
        Add(map, Momentum, SplitShot, "C_MOMENTUM_SPLIT", "Escalating Barrage");
        Add(map, Momentum, FeedbackLoop, "C_MOMENTUM_FEEDBACK", "Adrenal Reboot");
        Add(map, Momentum, ChainReaction, "C_MOMENTUM_CHAIN", "Acceleration Lattice");
        Add(map, Overkill, ExploitWeakness, "C_OVERKILL_EXPLOIT", "Execution Spill");
        Add(map, Overkill, FocusLens, "C_OVERKILL_FOCUS", "Prism Overflow");
        Add(map, Overkill, ChillShot, "C_OVERKILL_CHILL", "Shatter Spill");
        Add(map, Overkill, Overreach, "C_OVERKILL_OVERREACH", "Linebreaker Wake");
        Add(map, Overkill, HairTrigger, "C_OVERKILL_HAIR", "Shrapnel Cycler");
        Add(map, Overkill, SplitShot, "C_OVERKILL_SPLIT", "Splinter Flood");
        Add(map, Overkill, FeedbackLoop, "C_OVERKILL_FEEDBACK", "Overflow Reboot");
        Add(map, Overkill, ChainReaction, "C_OVERKILL_CHAIN", "Spill Conduit");
        Add(map, ExploitWeakness, FocusLens, "C_EXPLOIT_FOCUS", "Marked Lance");
        Add(map, ExploitWeakness, ChillShot, "C_EXPLOIT_CHILL", "Cryo Execute");
        Add(map, ExploitWeakness, Overreach, "C_EXPLOIT_OVERREACH", "Huntline");
        Add(map, ExploitWeakness, HairTrigger, "C_EXPLOIT_HAIR", "Predator Haste");
        Add(map, ExploitWeakness, SplitShot, "C_EXPLOIT_SPLIT", "Marked Shrapnel");
        Add(map, ExploitWeakness, FeedbackLoop, "C_EXPLOIT_FEEDBACK", "Execution Loop");
        Add(map, ExploitWeakness, ChainReaction, "C_EXPLOIT_CHAIN", "Reticle Web");
        Add(map, FocusLens, ChillShot, "C_FOCUS_CHILL", "Polar Beam");
        Add(map, FocusLens, Overreach, "C_FOCUS_OVERREACH", "Rail Horizon");
        Add(map, FocusLens, HairTrigger, "C_FOCUS_HAIR", "Pulse Gatling");
        Add(map, FocusLens, SplitShot, "C_FOCUS_SPLIT", "Prism Bloom");
        Add(map, FocusLens, FeedbackLoop, "C_FOCUS_FEEDBACK", "Lens Reboot");
        Add(map, FocusLens, ChainReaction, "C_FOCUS_CHAIN", "Arc Lance");
        Add(map, ChillShot, Overreach, "C_CHILL_OVERREACH", "Frostline Sweep");
        Add(map, ChillShot, HairTrigger, "C_CHILL_HAIR", "Rime Turbine");
        Add(map, ChillShot, SplitShot, "C_CHILL_SPLIT", "Shatter Bloom");
        Add(map, ChillShot, FeedbackLoop, "C_CHILL_FEEDBACK", "Cryo Recursion");
        Add(map, ChillShot, ChainReaction, "C_CHILL_CHAIN", "Cryo Lattice");
        Add(map, Overreach, HairTrigger, "C_OVERREACH_HAIR", "Suppressive Reach");
        Add(map, Overreach, SplitShot, "C_OVERREACH_SPLIT", "Wide Bloom");
        Add(map, Overreach, FeedbackLoop, "C_OVERREACH_FEEDBACK", "Echo Range Loop");
        Add(map, Overreach, ChainReaction, "C_OVERREACH_CHAIN", "Grid Sweep");
        Add(map, HairTrigger, SplitShot, "C_HAIR_SPLIT", "Needle Storm");
        Add(map, HairTrigger, FeedbackLoop, "C_HAIR_FEEDBACK", "Overclock Loop");
        Add(map, HairTrigger, ChainReaction, "C_HAIR_CHAIN", "Livewire Barrage");
        Add(map, SplitShot, FeedbackLoop, "C_SPLIT_FEEDBACK", "Recursive Bloom");
        Add(map, SplitShot, ChainReaction, "C_SPLIT_CHAIN", "Fractal Overload");
        Add(map, FeedbackLoop, ChainReaction, "C_FEEDBACK_CHAIN", "Reactor Grid");

        return map;
    }
}
