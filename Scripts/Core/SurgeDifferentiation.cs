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
    /// Player-facing global surge labels in canonical mod order.
    /// Single source of truth for label + feel mapping.
    /// </summary>
    public static readonly GlobalSurgeEntry[] GlobalSurgeTable =
    {
        new(SpectacleDefinitions.Momentum,        "Momentum",     GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overkill,        "Overkill",     GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ExploitWeakness, "Exploit",      GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FocusLens,       "Focus",        GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChillShot,       "Chill",        GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overreach,       "Overreach",    GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.HairTrigger,     "Hair Trigger", GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.SplitShot,       "Split Shot",   GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FeedbackLoop,    "Feedback",     GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChainReaction,   "Chain",        GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.BlastCore,       "Blast",        GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.Wildfire,        "Wildfire",     GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.Afterimage,      "Afterimage",   GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.ReaperProtocol,  "Reaper",       GlobalSurgeFeel.Neutral),
    };

    /// <summary>
    /// Maps the dominant contributing mod to the displayed global-surge label.
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
