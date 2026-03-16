using Godot;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Lightweight procedural icon that renders the actual in-game enemy body shape.
/// Mirrors the draw passes in EnemyInstance without requiring an active scene.
/// </summary>
public partial class EnemyIcon : Control
{
    private string _enemyId = "basic_walker";

    [Export]
    public string EnemyId
    {
        get => _enemyId;
        set
        {
            if (_enemyId == value) return;
            _enemyId = value ?? "basic_walker";
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (CustomMinimumSize == Vector2.Zero)
            CustomMinimumSize = new Vector2(32f, 32f);
    }

    public override void _Draw()
    {
        var style = EnemyRenderStyle.ForType(_enemyId);
        var center = Size * 0.5f;

        // Scale so the largest shape fits inside the icon bounds with a small margin.
        float naturalRadius = _enemyId == "armored_walker" ? 18.8f : 14.5f;
        float available = Mathf.Min(Size.X, Size.Y) * 0.46f;
        float s = available / naturalRadius;

        DrawSetTransform(center, 0f, new Vector2(s, s));

        switch (_enemyId)
        {
            case "armored_walker": DrawArmored(style); break;
            case "swift_walker":   DrawSwift(style);   break;
            default:               DrawBasic(style);   break;
        }
    }

    // ── Basic Walker (Neon Beetle Drone) ─────────────────────────────────────

    private void DrawBasic(in EnemyRenderStyle style)
    {
        // Soft glow halo
        DrawCircle(Vector2.Zero, 13.5f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));

        // Body layers
        DrawPolygon(RegularPoly(8, 11.6f, Mathf.Pi * 0.12f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(8, 9.2f,  Mathf.Pi * 0.12f), new[] { style.BodySecondary });

        // Core glow spot
        DrawCircle(new Vector2(-0.2f, -1.8f), 4.5f,
            new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.30f));

        // Beetle shell arc lines (emissive)
        DrawArc(new Vector2(0f, -1.5f), 6.2f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 12,
            new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);
        DrawArc(new Vector2(0f,  1.5f), 6.2f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 12,
            new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);

        // Antennae
        DrawLine(new Vector2(-1.8f, -7.8f), new Vector2(-4.2f, -11f),
            new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawLine(new Vector2( 1.8f, -7.8f), new Vector2( 4.2f, -11f),
            new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawCircle(new Vector2(-4.2f, -11f), 1.0f, Colors.White);
        DrawCircle(new Vector2( 4.2f, -11f), 1.0f, Colors.White);
    }

    // ── Armored Walker (Plated Rhino Core) ───────────────────────────────────

    private void DrawArmored(in EnemyRenderStyle style)
    {
        // Glow halos
        DrawCircle(Vector2.Zero, 19.4f, new Color(1.00f, 0.46f, 0.22f, 0.09f));
        DrawCircle(Vector2.Zero, 14.8f, new Color(1.00f, 0.46f, 0.22f, 0.14f));

        // Shell layers
        DrawPolygon(RegularPoly(6, 17.6f, 0f), new[] { new Color(0.08f, 0.02f, 0.03f) });
        DrawPolygon(RegularPoly(6, 14.9f, 0f), new[] { new Color(0.30f, 0.06f, 0.07f) });
        DrawPolygon(RegularPoly(6, 12.1f, 0f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(6,  8.8f, 0f), new[] { style.BodySecondary });

        // Neon outer rim
        var outerRim = RegularPoly(6, 16.1f, 0f);
        for (int i = 0; i < outerRim.Length; i++)
            DrawLine(outerRim[i], outerRim[(i + 1) % outerRim.Length],
                new Color(0.96f, 0.30f, 0.46f, 0.70f), 1.55f);

        // Inner accent rim
        var innerRim = RegularPoly(6, 13.4f, 0f);
        for (int i = 0; i < innerRim.Length; i++)
            DrawLine(innerRim[i], innerRim[(i + 1) % innerRim.Length],
                new Color(1.00f, 0.46f, 0.18f, 0.52f), 1.2f);

        // Visor slot
        DrawRect(new Rect2(-5.6f, -2.2f, 11.2f, 4.4f), new Color(0.09f, 0.02f, 0.03f, 0.95f), filled: true);
        DrawRect(new Rect2(-4.2f, -1.0f,  8.4f, 2.0f),
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.78f), filled: true);
    }

    // ── Swift Walker (Razor Ray / Dart Eel) ──────────────────────────────────

    private void DrawSwift(in EnemyRenderStyle style)
    {
        // Soft glow
        DrawCircle(Vector2.Zero, 14f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));

        // Outer dart body
        var outerBody = new[]
        {
            new Vector2( 12.5f,  0f),
            new Vector2(  0f,   -9.2f),
            new Vector2(-13f,   -4.0f),
            new Vector2( -9.0f,  0f),
            new Vector2(-13f,    4.0f),
            new Vector2(  0f,    9.2f),
        };
        DrawPolygon(outerBody, new[] { style.BodyPrimary });

        // Core diamond
        DrawPolygon(new[]
        {
            new Vector2( 8.6f,  0f),
            new Vector2(  0f,  -5.8f),
            new Vector2(-8.8f,  0f),
            new Vector2(  0f,   5.8f),
        }, new[] { style.BodySecondary });

        // Back fins
        DrawPolygon(new[]
        {
            new Vector2(-3f,   -4f),
            new Vector2(-12.5f, -10f),
            new Vector2(-7f,   -1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });
        DrawPolygon(new[]
        {
            new Vector2(-3f,    4f),
            new Vector2(-12.5f, 10f),
            new Vector2(-7f,    1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });

        // Emissive outline
        for (int i = 0; i < outerBody.Length; i++)
            DrawLine(outerBody[i], outerBody[(i + 1) % outerBody.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.70f), 1.2f);

        // Hot-core eye
        DrawCircle(new Vector2(1.8f, 0f), 2.2f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.90f));
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Vector2[] RegularPoly(int sides, float radius, float angleOffset)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = angleOffset + i * Mathf.Tau / sides;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }
}
