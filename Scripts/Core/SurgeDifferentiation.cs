namespace SlotTheory.Core;

/// <summary>
/// Pure-logic helpers for the surge differentiation system.
///
/// GLOBAL SURGE (3 feels): Pressure / Chain(Neutral) / Detonation
///   Derived from dominant contributing modifier across all towers.
///
/// TOWER SURGE (4 categories): Spread / Burst / Control / Echo
///   Derived per-surge from the tower's equipped modifiers + tower base bias.
///   Used to bias each individual Tower Surge toward one perceptually clear identity.
///   Spread  = chain / split / branching / group propagation
///   Burst   = heavy impact / explosion / overkill / execution spike
///   Control = slow / pin / trap / compression / zone manipulation
///   Echo    = afterimage / repeat / delayed follow-through / lingering strikes
///
/// No Godot dependencies -- fully unit-testable.
/// </summary>
public static class SurgeDifferentiation
{
    // ── Tower Surge Category system ─────────────────────────────────────────

    /// <summary>Dominant identity category for a single tower surge event.</summary>
    public enum TowerSurgeCategory { Spread, Burst, Control, Echo }

    public readonly record struct ModCategoryEntry(string ModId, TowerSurgeCategory Category, float Weight);
    public readonly record struct TowerBiasEntry(string TowerId, TowerSurgeCategory Category, float Weight);

    /// <summary>
    /// Modifier to category weight mapping.
    /// Weight reflects how strongly a mod pulls the surge toward its category.
    /// A mod absent from this table does not contribute a category score.
    /// </summary>
    public static readonly ModCategoryEntry[] ModCategoryTable =
    {
        // Spread: arcs, splits, area propagation to groups
        new(SpectacleDefinitions.ChainReaction,   TowerSurgeCategory.Spread,  2.5f),
        new(SpectacleDefinitions.SplitShot,       TowerSurgeCategory.Spread,  2.0f),
        new(SpectacleDefinitions.Overreach,       TowerSurgeCategory.Spread,  1.8f),
        new(SpectacleDefinitions.BlastCore,       TowerSurgeCategory.Spread,  1.5f),

        // Burst: heavy single-target hit, execution, overkill conversion
        new(SpectacleDefinitions.Overkill,        TowerSurgeCategory.Burst,   2.5f),
        new(SpectacleDefinitions.FocusLens,       TowerSurgeCategory.Burst,   2.2f),
        new(SpectacleDefinitions.ExploitWeakness, TowerSurgeCategory.Burst,   1.8f),
        new(SpectacleDefinitions.ReaperProtocol,  TowerSurgeCategory.Burst,   1.5f),

        // Control: slow, trap, zone, compression, positional manipulation
        new(SpectacleDefinitions.ChillShot,       TowerSurgeCategory.Control, 2.5f),
        new(SpectacleDefinitions.Deadzone,        TowerSurgeCategory.Control, 2.2f),
        new(SpectacleDefinitions.Momentum,        TowerSurgeCategory.Control, 2.0f),

        // Echo: afterimage, repeat strikes, lingering follow-through
        new(SpectacleDefinitions.Afterimage,      TowerSurgeCategory.Echo,    2.5f),
        new(SpectacleDefinitions.Wildfire,        TowerSurgeCategory.Echo,    2.0f),
        new(SpectacleDefinitions.HairTrigger,     TowerSurgeCategory.Echo,    2.0f),
        new(SpectacleDefinitions.FeedbackLoop,    TowerSurgeCategory.Echo,    1.8f),
    };

    /// <summary>
    /// Tower base category bias.
    /// Anchors the surge identity when mods are weak, absent, or evenly mixed.
    /// Does not override a strong modifier signal -- it only adds weight.
    /// </summary>
    public static readonly TowerBiasEntry[] TowerBiasTable =
    {
        new("rapid_shooter",    TowerSurgeCategory.Echo,    1.5f),  // rapid cascading fire rhythm
        new("heavy_cannon",     TowerSurgeCategory.Burst,   2.0f),  // big single-shot punch
        new("marker_tower",     TowerSurgeCategory.Control, 1.8f),  // mark/support utility
        new("chain_tower",      TowerSurgeCategory.Spread,  2.0f),  // arc branching identity
        new("rift_prism",       TowerSurgeCategory.Control, 1.8f),  // mine/trap framework
        new("accordion_engine", TowerSurgeCategory.Control, 2.0f),  // pack compression
        new("phase_splitter",   TowerSurgeCategory.Spread,  1.5f),  // multi-hit phase volleys
        new("latch_nest",       TowerSurgeCategory.Echo,    1.5f),  // lingering swarm pressure
        new("rocket_launcher",  TowerSurgeCategory.Burst,   1.8f),  // explosive punchy hits
        new("undertow_engine",  TowerSurgeCategory.Control, 2.0f),  // slow rhythmic manipulation
    };

    /// <summary>
    /// Resolves the dominant surge category for a tower surge event.
    /// Combines equipped modifier weights (primary > secondary > tertiary) with the
    /// tower's base bias. Tie-breaks by stable enum order (Spread wins over Burst etc.).
    /// </summary>
    public static TowerSurgeCategory ResolveTowerSurgeCategory(string towerId, SpectacleSignature sig)
    {
        // 4 slots: Spread=0, Burst=1, Control=2, Echo=3
        float[] scores = new float[4];

        // All three mods contribute equally -- two mods in the same category
        // should always outweigh one mod in a different category.
        ApplyModScore(scores, sig.PrimaryModId,   1.00f);
        ApplyModScore(scores, sig.SecondaryModId, 1.00f);
        ApplyModScore(scores, sig.TertiaryModId,  1.00f);

        // Tower base bias -- added on top of modifier signal
        string normTower = (towerId ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var entry in TowerBiasTable)
            if (entry.TowerId == normTower) { scores[(int)entry.Category] += entry.Weight; break; }

        // Winner: highest score; ties broken by stable enum order (lower value wins)
        int best = 0;
        for (int i = 1; i < scores.Length; i++)
            if (scores[i] > scores[best]) best = i;
        return (TowerSurgeCategory)best;
    }

    private static void ApplyModScore(float[] scores, string modId, float positionScale)
    {
        if (string.IsNullOrEmpty(modId)) return;
        foreach (var entry in ModCategoryTable)
            if (entry.ModId == modId) { scores[(int)entry.Category] += entry.Weight * positionScale; return; }
    }

    /// <summary>
    /// Short player-facing callout label for a tower surge, used as the main callout headline.
    /// This replaces "SURGE: {mod_name}" -- the category is the identity, not the mod.
    /// </summary>
    public static string GetCategoryCallout(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Spread  => "SPREAD SURGE",
        TowerSurgeCategory.Burst   => "BURST SURGE",
        TowerSurgeCategory.Control => "CONTROL SURGE",
        TowerSurgeCategory.Echo    => "ECHO SURGE",
        _                          => "SURGE",
    };

    // ── Category presentation helpers (read Balance.cs constants) ───────────

    /// <summary>Max enemy links for SpawnSpectacleLinks during a tower surge.</summary>
    public static int GetCategoryMaxLinks(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Spread  => Balance.TowerSurgeSpreadMaxLinks,
        TowerSurgeCategory.Control => Balance.TowerSurgeControlMaxLinks,
        _                          => Balance.TowerSurgeMaxLinks,
    };

    /// <summary>Multiplier applied to the link range factor for the surge.</summary>
    public static float GetCategoryLinkRangeMult(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Spread  => Balance.TowerSurgeSpreadLinkMult,
        _                          => 1.00f,
    };

    /// <summary>Peak alpha for the screen flash on tower surge.</summary>
    public static float GetCategoryFlashAlpha(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Burst   => Balance.TowerSurgeBurstFlashAlpha,
        TowerSurgeCategory.Spread  => Balance.TowerSurgeSpreadFlashAlpha,
        TowerSurgeCategory.Control => Balance.TowerSurgeControlFlashAlpha,
        _                          => Balance.TowerSurgeScreenFlashAlpha,  // Echo = base
    };

    /// <summary>Signature ring drama scale for the surge.</summary>
    public static float GetCategorySignatureDrama(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Control => Balance.TowerSurgeControlSignatureDrama,
        _                          => Balance.TowerSurgeSignatureDrama,
    };

    /// <summary>Archetype FX drama scale for the surge.</summary>
    public static float GetCategoryArchetypeDrama(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Burst   => Balance.TowerSurgeBurstArchetypeDrama,
        TowerSurgeCategory.Echo    => Balance.TowerSurgeEchoArchetypeDrama,
        _                          => Balance.TowerSurgeArchetypeDrama,
    };

    /// <summary>Slowmo real-time duration for the surge.</summary>
    public static float GetCategorySlowMoDuration(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Burst   => Balance.TowerSurgeBurstSlowMoDuration,
        TowerSurgeCategory.Control => Balance.TowerSurgeControlSlowMoDuration,
        _                          => Balance.TowerSurgeSlowMoDuration,
    };

    /// <summary>Slowmo speed factor (lower = more dilated) for the surge.</summary>
    public static float GetCategorySlowMoFactor(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Burst   => Balance.TowerSurgeBurstSlowMoFactor,
        _                          => Balance.TowerSurgeSlowMoFactor,
    };

    /// <summary>Burst FX power multiplier applied to catBurstPower.</summary>
    public static float GetCategoryBurstPowerMult(TowerSurgeCategory c) => c switch
    {
        TowerSurgeCategory.Burst   => Balance.TowerSurgeBurstPowerMult,
        _                          => 1.00f,
    };

    // ─── Global Surge feels ─────────────────────────────────────────────────
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
        return ResolveFeelFromMod(primary);
    }

    public static GlobalSurgeFeel ResolveFeelFromMod(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return GlobalSurgeFeel.Neutral;

        string normalized = SpectacleDefinitions.NormalizeModId(modId);
        foreach (var entry in GlobalSurgeTable)
            if (entry.ModId == normalized) return entry.Feel;
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
