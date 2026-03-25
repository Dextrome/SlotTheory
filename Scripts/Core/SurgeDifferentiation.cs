namespace SlotTheory.Core;

/// <summary>
/// Pure-logic helpers for the surge differentiation system (Phase 3–5).
/// No Godot dependencies - fully unit-testable.
/// </summary>
public static class SurgeDifferentiation
{
    public enum GlobalSurgeFeel { Pressure, Neutral, Detonation }

    public readonly record struct GlobalSurgeEntry(string ModId, string Label, GlobalSurgeFeel Feel);

    /// <summary>
    /// All named global surge archetypes in canonical mod order.
    /// Single source of truth for labels, feel, and HowToPlay display.
    /// </summary>
    public static readonly GlobalSurgeEntry[] GlobalSurgeTable =
    {
        new(SpectacleDefinitions.Momentum,        "REDLINE WAVE",    GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overkill,        "OVERKILL STORM",  GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ExploitWeakness, "EXECUTION STORM", GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FocusLens,       "PRISM BARRAGE",   GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChillShot,       "CRYO STORM",      GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overreach,       "HORIZON BREAK",   GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.HairTrigger,     "HYPERBURST WAVE", GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.SplitShot,       "FRACTAL STORM",   GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FeedbackLoop,    "REBOOT CASCADE",  GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChainReaction,   "CHAIN STORM",     GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.BlastCore,       "BLAST WAVE",      GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.Wildfire,        "INFERNO SURGE",   GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ReaperProtocol,  "GRIM TIDE",       GlobalSurgeFeel.Neutral),
    };

    /// <summary>
    /// Maps the dominant contributing mod to a named global-surge label (build archetype).
    /// dominantModIds must be ordered by contribution (most → least); only [0] is used.
    /// </summary>
    public static string ResolveLabel(string[]? dominantModIds)
    {
        string primary = dominantModIds is { Length: > 0 } ? dominantModIds[0] : string.Empty;
        foreach (var entry in GlobalSurgeTable)
            if (entry.ModId == primary) return entry.Label;
        return "GLOBAL SURGE";
    }

    /// <summary>
    /// Classifies the global surge feel for visual tuning (flash alpha, secondary pulses).
    /// Detonation = spike builds; Pressure = sustained/range builds; Neutral = everything else.
    /// </summary>
    public static GlobalSurgeFeel ResolveFeel(string[]? dominantModIds)
    {
        string primary = dominantModIds is { Length: > 0 } ? dominantModIds[0] : string.Empty;
        foreach (var entry in GlobalSurgeTable)
            if (entry.ModId == primary) return entry.Feel;
        return GlobalSurgeFeel.Neutral;
    }

    /// <summary>
    /// Flash alpha for the main global surge screen flash, keyed by feel.
    /// </summary>
    public static float ResolveFlashAlpha(GlobalSurgeFeel feel) => feel switch
    {
        GlobalSurgeFeel.Detonation => 0.34f,
        GlobalSurgeFeel.Pressure   => 0.21f,
        _                          => 0.28f,
    };
}
