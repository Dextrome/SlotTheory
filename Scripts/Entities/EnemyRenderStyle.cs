using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Color/style palette used by layered enemy rendering.
/// </summary>
public readonly struct EnemyRenderStyle
{
    public Color BodyPrimary { get; }
    public Color BodySecondary { get; }
    public Color Emissive { get; }
    public Color EmissiveHot { get; }
    public Color DamageTint { get; }
    public Color BloomTint { get; }

    public EnemyRenderStyle(
        Color bodyPrimary,
        Color bodySecondary,
        Color emissive,
        Color emissiveHot,
        Color damageTint,
        Color bloomTint)
    {
        BodyPrimary = bodyPrimary;
        BodySecondary = bodySecondary;
        Emissive = emissive;
        EmissiveHot = emissiveHot;
        DamageTint = damageTint;
        BloomTint = bloomTint;
    }

    public static EnemyRenderStyle ForType(string enemyTypeId) => enemyTypeId switch
    {
        "swift_walker" => new EnemyRenderStyle(
            bodyPrimary: new Color(0.70f, 1.00f, 0.24f),
            bodySecondary: new Color(0.05f, 0.12f, 0.03f),
            emissive: new Color(1.00f, 0.94f, 0.30f),
            emissiveHot: new Color(1.00f, 0.98f, 0.74f),
            damageTint: new Color(1.00f, 0.62f, 0.12f),
            bloomTint: new Color(0.86f, 1.00f, 0.45f)),
        "reverse_walker" => new EnemyRenderStyle(
            bodyPrimary: new Color(0.24f, 0.92f, 1.00f),
            bodySecondary: new Color(0.05f, 0.08f, 0.19f),
            emissive: new Color(0.66f, 1.00f, 0.98f),
            emissiveHot: new Color(0.96f, 0.66f, 1.00f),
            damageTint: new Color(1.00f, 0.44f, 0.68f),
            bloomTint: new Color(0.46f, 0.92f, 1.00f)),
        "armored_walker" => new EnemyRenderStyle(
            bodyPrimary: new Color(0.86f, 0.28f, 0.11f),
            bodySecondary: new Color(0.10f, 0.02f, 0.03f),
            emissive: new Color(0.98f, 0.42f, 0.24f),
            emissiveHot: new Color(1.00f, 0.60f, 0.32f),
            damageTint: new Color(0.98f, 0.34f, 0.18f),
            bloomTint: new Color(0.96f, 0.36f, 0.20f)),
        "splitter_walker" => new EnemyRenderStyle(
            bodyPrimary: new Color(0.96f, 0.62f, 0.08f),
            bodySecondary: new Color(0.14f, 0.08f, 0.01f),
            emissive: new Color(1.00f, 0.80f, 0.28f),
            emissiveHot: new Color(1.00f, 0.92f, 0.60f),
            damageTint: new Color(1.00f, 0.30f, 0.10f),
            bloomTint: new Color(1.00f, 0.72f, 0.20f)),
        "splitter_shard" => new EnemyRenderStyle(
            bodyPrimary: new Color(1.00f, 0.82f, 0.38f),
            bodySecondary: new Color(0.18f, 0.12f, 0.02f),
            emissive: new Color(1.00f, 0.90f, 0.50f),
            emissiveHot: new Color(1.00f, 0.96f, 0.78f),
            damageTint: new Color(1.00f, 0.50f, 0.10f),
            bloomTint: new Color(1.00f, 0.85f, 0.40f)),
        _ => new EnemyRenderStyle(
            bodyPrimary: new Color(0.20f, 0.98f, 0.86f),
            bodySecondary: new Color(0.02f, 0.17f, 0.18f),
            emissive: new Color(0.72f, 1.00f, 0.96f),
            emissiveHot: new Color(0.92f, 1.00f, 0.98f),
            damageTint: new Color(1.00f, 0.30f, 0.40f),
            bloomTint: new Color(0.60f, 1.00f, 0.92f)),
    };
}
