using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

public enum EnemyTrailShape
{
    SoftRibbon,
    RazorArc,
    DenseEmber,
}

/// <summary>
/// Centralized archetype mapping for phase-2/3 enemy visuals.
/// Keeps per-class visual tuning in one place and easy to test.
/// </summary>
public readonly struct EnemyVisualArchetype
{
    public string Name { get; }
    public EnemyTrailShape TrailShape { get; }
    public float TrailLifetime { get; }
    public float TrailSpacing { get; }
    public float TrailWidth { get; }
    public float TurnTiltScale { get; }
    public float TurnTiltMaxRad { get; }
    public Color TrailColor { get; }

    public EnemyVisualArchetype(
        string name,
        EnemyTrailShape trailShape,
        float trailLifetime,
        float trailSpacing,
        float trailWidth,
        float turnTiltScale,
        float turnTiltMaxRad,
        Color trailColor)
    {
        Name = name;
        TrailShape = trailShape;
        TrailLifetime = trailLifetime;
        TrailSpacing = trailSpacing;
        TrailWidth = trailWidth;
        TurnTiltScale = turnTiltScale;
        TurnTiltMaxRad = turnTiltMaxRad;
        TrailColor = trailColor;
    }

    public static EnemyVisualArchetype ForType(string enemyTypeId) => enemyTypeId switch
    {
        "swift_walker" => new EnemyVisualArchetype(
            name: "Razor Ray / Dart Eel",
            trailShape: EnemyTrailShape.RazorArc,
            trailLifetime: 0.20f,
            trailSpacing: 0.016f,
            trailWidth: 1.9f,
            turnTiltScale: 2.35f,
            turnTiltMaxRad: 0.34f,
            trailColor: new Color(0.72f, 1.00f, 0.45f, 0.62f)),
        "reverse_walker" => new EnemyVisualArchetype(
            name: "Reverse Walker",
            trailShape: EnemyTrailShape.RazorArc,
            trailLifetime: 0.26f,
            trailSpacing: 0.020f,
            trailWidth: 2.2f,
            turnTiltScale: 1.90f,
            turnTiltMaxRad: 0.30f,
            trailColor: new Color(0.44f, 0.96f, 1.00f, 0.64f)),
        "armored_walker" => new EnemyVisualArchetype(
            name: "Plated Rhino Core",
            trailShape: EnemyTrailShape.DenseEmber,
            trailLifetime: 0.28f,
            trailSpacing: 0.030f,
            trailWidth: 2.6f,
            turnTiltScale: 1.30f,
            turnTiltMaxRad: 0.20f,
            trailColor: new Color(0.98f, 0.33f, 0.23f, 0.44f)),
        "splitter_walker" => new EnemyVisualArchetype(
            name: "Amber Splitter",
            trailShape: EnemyTrailShape.DenseEmber,
            trailLifetime: 0.22f,
            trailSpacing: 0.025f,
            trailWidth: 2.0f,
            turnTiltScale: 1.50f,
            turnTiltMaxRad: 0.22f,
            trailColor: new Color(0.96f, 0.65f, 0.10f, 0.48f)),
        "splitter_shard" => new EnemyVisualArchetype(
            name: "Amber Shard",
            trailShape: EnemyTrailShape.RazorArc,
            trailLifetime: 0.14f,
            trailSpacing: 0.014f,
            trailWidth: 1.5f,
            turnTiltScale: 2.00f,
            turnTiltMaxRad: 0.30f,
            trailColor: new Color(1.00f, 0.82f, 0.40f, 0.52f)),
        "shield_drone" => new EnemyVisualArchetype(
            name: "Shield Drone",
            trailShape: EnemyTrailShape.SoftRibbon,
            trailLifetime: 0.22f,
            trailSpacing: 0.028f,
            trailWidth: 1.9f,
            turnTiltScale: 1.20f,
            turnTiltMaxRad: 0.18f,
            trailColor: new Color(0.30f, 0.76f, 1.00f, 0.44f)),
        EnemyCatalog.AnchorWalkerId => new EnemyVisualArchetype(
            // Wide ember wake -- slow, planted, magenta-crimson to match body
            name: "Anchor Walker",
            trailShape: EnemyTrailShape.DenseEmber,
            trailLifetime: 0.36f,
            trailSpacing: 0.038f,
            trailWidth: 3.4f,
            turnTiltScale: 0.75f,   // barely tilts -- anchored feel
            turnTiltMaxRad: 0.10f,
            trailColor: new Color(0.88f, 0.16f, 0.48f, 0.55f)),
        EnemyCatalog.NullDroneId => new EnemyVisualArchetype(
            // Soft violet ribbon -- hovering jammer, distinct purple trail
            name: "Null Drone",
            trailShape: EnemyTrailShape.SoftRibbon,
            trailLifetime: 0.30f,
            trailSpacing: 0.026f,
            trailWidth: 2.4f,
            turnTiltScale: 1.10f,
            turnTiltMaxRad: 0.16f,
            trailColor: new Color(0.64f, 0.26f, 1.00f, 0.56f)),
        EnemyCatalog.LancerWalkerId => new EnemyVisualArchetype(
            // Short sharp gold razor -- dash feel, aggressive tilt on turns
            name: "Lancer Walker",
            trailShape: EnemyTrailShape.RazorArc,
            trailLifetime: 0.16f,
            trailSpacing: 0.014f,
            trailWidth: 1.7f,
            turnTiltScale: 2.55f,   // extreme tilt during turns/dashes
            turnTiltMaxRad: 0.40f,
            trailColor: new Color(1.00f, 0.88f, 0.18f, 0.64f)),
        EnemyCatalog.VeilWalkerId => new EnemyVisualArchetype(
            // Silver-white soft ribbon -- ghostly, shell-protected
            name: "Veil Walker",
            trailShape: EnemyTrailShape.SoftRibbon,
            trailLifetime: 0.28f,
            trailSpacing: 0.019f,
            trailWidth: 2.0f,
            turnTiltScale: 1.65f,
            turnTiltMaxRad: 0.24f,
            trailColor: new Color(0.90f, 0.96f, 1.00f, 0.40f)),
        _ => new EnemyVisualArchetype(
            name: "Neon Beetle Drone",
            trailShape: EnemyTrailShape.SoftRibbon,
            trailLifetime: 0.24f,
            trailSpacing: 0.022f,
            trailWidth: 2.1f,
            turnTiltScale: 1.55f,
            turnTiltMaxRad: 0.24f,
            trailColor: new Color(0.26f, 0.96f, 0.92f, 0.50f)),
    };
}
