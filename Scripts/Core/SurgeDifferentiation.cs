namespace SlotTheory.Core;

/// <summary>
/// Pure-logic helpers for the surge differentiation system.
/// Three distinct Global Surge types derived from the dominant contributing modifier:
///   Pressure   – control/slow focus: extended mark + deep slow, cold blue presence
///   Chain      – spreading chain: arcs jump enemy-to-enemy, electric purple presence
///   Detonation – burst damage: heavy spike, max cooldown refund, hot orange presence
/// "Neutral" internal feel maps to the player-facing "CHAIN SURGE" label.
/// No Godot dependencies – fully unit-testable.
/// </summary>
public static class SurgeDifferentiation
{
    public enum GlobalSurgeFeel { Pressure, Neutral, Detonation }

    // "Neutral" is surfaced to the player as "CHAIN SURGE".
    // The internal name is preserved to avoid a large refactor of dependent code.

    public readonly record struct GlobalSurgeEntry(string ModId, GlobalSurgeFeel Feel);

    /// <summary>Mod → feel mapping. Single source of truth for feel classification.</summary>
    public static readonly GlobalSurgeEntry[] GlobalSurgeTable =
    {
        new(SpectacleDefinitions.Momentum,        GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overkill,        GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ExploitWeakness, GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FocusLens,       GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChillShot,       GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.Overreach,       GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.HairTrigger,     GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.SplitShot,       GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.FeedbackLoop,    GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.ChainReaction,   GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.BlastCore,       GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.Wildfire,        GlobalSurgeFeel.Detonation),
        new(SpectacleDefinitions.Afterimage,      GlobalSurgeFeel.Pressure),
        new(SpectacleDefinitions.ReaperProtocol,  GlobalSurgeFeel.Neutral),
        new(SpectacleDefinitions.Deadzone,        GlobalSurgeFeel.Neutral),
    };

    /// <summary>
    /// Returns the player-facing label based on the dominant mod's feel.
    /// Three distinct labels: PRESSURE SURGE / CHAIN SURGE / DETONATION SURGE.
    /// </summary>
    public static string ResolveLabel(string[]? dominantModIds) =>
        ResolveLabel(ResolveFeel(dominantModIds));

    /// <summary>Returns the player-facing label for a given feel.</summary>
    public static string ResolveLabel(GlobalSurgeFeel feel) => feel switch
    {
        GlobalSurgeFeel.Pressure   => "PRESSURE SURGE",
        GlobalSurgeFeel.Detonation => "DETONATION SURGE",
        _                          => "CHAIN SURGE",
    };

    /// <summary>
    /// Classifies the dominant mod's feel.
    /// Detonation = spike/burst; Pressure = control/slow; Neutral/Chain = spreading chain.
    /// </summary>
    public static GlobalSurgeFeel ResolveFeel(string[]? dominantModIds)
    {
        string primary = dominantModIds is { Length: > 0 } ? dominantModIds[0] : string.Empty;
        foreach (var entry in GlobalSurgeTable)
            if (entry.ModId == primary) return entry.Feel;
        return GlobalSurgeFeel.Neutral;
    }

    /// <summary>
    /// Screen flash alpha for the main global surge flash, keyed by feel.
    /// Detonation = sharpest spike; Pressure = softest wash; Chain = balanced.
    /// </summary>
    public static float ResolveFlashAlpha(GlobalSurgeFeel feel) => feel switch
    {
        GlobalSurgeFeel.Detonation => 0.34f,
        GlobalSurgeFeel.Pressure   => 0.21f,
        _                          => 0.28f,
    };

    /// <summary>
    /// Sustained screen tint color keyed to feel.
    /// Pressure = cold blue-cyan; Chain = electric purple; Detonation = hot orange.
    /// </summary>
    public static Godot.Color ResolveLingerColor(GlobalSurgeFeel feel) => feel switch
    {
        GlobalSurgeFeel.Pressure   => new Godot.Color(0.08f, 0.55f, 1.00f),  // cold blue -- control
        GlobalSurgeFeel.Detonation => new Godot.Color(1.00f, 0.50f, 0.08f),  // hot orange -- explosion
        _                          => new Godot.Color(0.60f, 0.20f, 1.00f),  // electric purple -- chain
    };

    /// <summary>Short mechanical subtitle shown below the global surge banner.</summary>
    public static string ResolveTypeSubtitle(GlobalSurgeFeel feel) => feel switch
    {
        GlobalSurgeFeel.Pressure   => "Extended control · Enemies deeply slowed",
        GlobalSurgeFeel.Detonation => "Heavy detonation · Max cooldown refund",
        _                          => "Chain spread · Arcs jump enemy to enemy",
    };
}
