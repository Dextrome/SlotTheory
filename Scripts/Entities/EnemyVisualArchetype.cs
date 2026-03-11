using Godot;

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
            turnTiltScale: 1.65f,
            turnTiltMaxRad: 0.20f,
            trailColor: new Color(0.72f, 1.00f, 0.45f, 0.62f)),
        "armored_walker" => new EnemyVisualArchetype(
            name: "Plated Rhino Core",
            trailShape: EnemyTrailShape.DenseEmber,
            trailLifetime: 0.28f,
            trailSpacing: 0.030f,
            trailWidth: 2.6f,
            turnTiltScale: 0.75f,
            turnTiltMaxRad: 0.09f,
            trailColor: new Color(0.98f, 0.33f, 0.23f, 0.44f)),
        _ => new EnemyVisualArchetype(
            name: "Neon Beetle Drone",
            trailShape: EnemyTrailShape.SoftRibbon,
            trailLifetime: 0.24f,
            trailSpacing: 0.022f,
            trailWidth: 2.1f,
            turnTiltScale: 1.0f,
            turnTiltMaxRad: 0.12f,
            trailColor: new Color(0.26f, 0.96f, 0.92f, 0.50f)),
    };
}
