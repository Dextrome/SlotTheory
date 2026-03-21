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
        float naturalRadius = _enemyId switch
        {
            "armored_walker" => 18.8f,
            "reverse_walker" => 15.8f,
            "shield_drone"   => 14.8f,
            _ => 14.5f,
        };
        float available = Mathf.Min(Size.X, Size.Y) * 0.46f;
        float s = available / naturalRadius;

        DrawSetTransform(center, 0f, new Vector2(s, s));

        switch (_enemyId)
        {
            case "armored_walker":  DrawArmored(style);      break;
            case "swift_walker":    DrawSwift(style);        break;
            case "reverse_walker":  DrawReverse(style);      break;
            case "splitter_walker": DrawSplitter(style);     break;
            case "splitter_shard":  DrawShard(style);        break;
            case "shield_drone":    DrawShieldDrone(style);  break;
            default:                DrawBasic(style);        break;
        }
    }

    private void DrawBasic(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 13.5f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));
        DrawPolygon(RegularPoly(8, 11.6f, Mathf.Pi * 0.12f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(8, 9.2f, Mathf.Pi * 0.12f), new[] { style.BodySecondary });
        DrawCircle(new Vector2(-0.2f, -1.8f), 4.5f,
            new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.30f));

        DrawArc(new Vector2(0f, -1.5f), 6.2f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 12,
            new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);
        DrawArc(new Vector2(0f, 1.5f), 6.2f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 12,
            new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);

        DrawLine(new Vector2(-1.8f, -7.8f), new Vector2(-4.2f, -11f),
            new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawLine(new Vector2(1.8f, -7.8f), new Vector2(4.2f, -11f),
            new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawCircle(new Vector2(-4.2f, -11f), 1.0f, Colors.White);
        DrawCircle(new Vector2(4.2f, -11f), 1.0f, Colors.White);
    }

    private void DrawArmored(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 19.4f, new Color(1.00f, 0.46f, 0.22f, 0.09f));
        DrawCircle(Vector2.Zero, 14.8f, new Color(1.00f, 0.46f, 0.22f, 0.14f));

        DrawPolygon(RegularPoly(6, 17.6f, 0f), new[] { new Color(0.08f, 0.02f, 0.03f) });
        DrawPolygon(RegularPoly(6, 14.9f, 0f), new[] { new Color(0.30f, 0.06f, 0.07f) });
        DrawPolygon(RegularPoly(6, 12.1f, 0f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(6, 8.8f, 0f), new[] { style.BodySecondary });

        var outerRim = RegularPoly(6, 16.1f, 0f);
        for (int i = 0; i < outerRim.Length; i++)
            DrawLine(outerRim[i], outerRim[(i + 1) % outerRim.Length],
                new Color(0.96f, 0.30f, 0.46f, 0.70f), 1.55f);

        var innerRim = RegularPoly(6, 13.4f, 0f);
        for (int i = 0; i < innerRim.Length; i++)
            DrawLine(innerRim[i], innerRim[(i + 1) % innerRim.Length],
                new Color(1.00f, 0.46f, 0.18f, 0.52f), 1.2f);

        DrawRect(new Rect2(-5.6f, -2.2f, 11.2f, 4.4f), new Color(0.09f, 0.02f, 0.03f, 0.95f), filled: true);
        DrawRect(new Rect2(-4.2f, -1.0f, 8.4f, 2.0f),
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.78f), filled: true);
    }

    private void DrawSwift(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 14f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));

        var outerBody = new[]
        {
            new Vector2(12.5f, 0f),
            new Vector2(0f, -9.2f),
            new Vector2(-13f, -4.0f),
            new Vector2(-9.0f, 0f),
            new Vector2(-13f, 4.0f),
            new Vector2(0f, 9.2f),
        };
        DrawPolygon(outerBody, new[] { style.BodyPrimary });

        DrawPolygon(new[]
        {
            new Vector2(8.6f, 0f),
            new Vector2(0f, -5.8f),
            new Vector2(-8.8f, 0f),
            new Vector2(0f, 5.8f),
        }, new[] { style.BodySecondary });

        DrawPolygon(new[]
        {
            new Vector2(-3f, -4f),
            new Vector2(-12.5f, -10f),
            new Vector2(-7f, -1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });
        DrawPolygon(new[]
        {
            new Vector2(-3f, 4f),
            new Vector2(-12.5f, 10f),
            new Vector2(-7f, 1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });

        for (int i = 0; i < outerBody.Length; i++)
            DrawLine(outerBody[i], outerBody[(i + 1) % outerBody.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.70f), 1.2f);

        DrawCircle(new Vector2(1.8f, 0f), 2.2f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.90f));
    }

    private void DrawReverse(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 14.2f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.18f));

        var shell = new[]
        {
            new Vector2(11.8f, 0f),
            new Vector2(2.2f, -9.1f),
            new Vector2(-12.8f, -5.8f),
            new Vector2(-7.4f, 0f),
            new Vector2(-12.8f, 5.8f),
            new Vector2(2.2f, 9.1f),
        };
        DrawPolygon(shell, new[] { style.BodyPrimary });
        DrawPolygon(new[]
        {
            new Vector2(7.6f, 0f),
            new Vector2(1.0f, -5.2f),
            new Vector2(-8.2f, 0f),
            new Vector2(1.0f, 5.2f),
        }, new[] { style.BodySecondary });

        DrawPolygon(new[]
        {
            new Vector2(-7.4f, -1.8f),
            new Vector2(-15.6f, -6.7f),
            new Vector2(-11.2f, 0.1f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.76f) });
        DrawPolygon(new[]
        {
            new Vector2(-7.4f, 1.8f),
            new Vector2(-15.6f, 6.7f),
            new Vector2(-11.2f, -0.1f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.76f) });

        for (int i = 0; i < shell.Length; i++)
            DrawLine(shell[i], shell[(i + 1) % shell.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.74f), 1.2f);

        DrawArc(new Vector2(-0.4f, 0f), 4.0f, Mathf.Pi * 0.2f, Mathf.Pi * 1.8f, 16,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.88f), 1.3f);
        DrawLine(new Vector2(-3.8f, 0f), new Vector2(3.8f, 0f),
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.82f), 1.3f);
    }

    private void DrawSplitter(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 13.5f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.18f));
        DrawPolygon(RegularPoly(6, 11.8f, Mathf.Pi * 0.08f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(6, 9.0f, Mathf.Pi * 0.08f), new[] { style.BodySecondary });

        DrawCircle(new Vector2(0f, -1.2f), 4.0f,
            new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.32f));

        DrawLine(new Vector2(-5f, -8f), new Vector2(5f, 8f),
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.85f), 1.5f);
        DrawLine(new Vector2(5f, -8f), new Vector2(-5f, 8f),
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.55f), 1.0f);

        var rim = RegularPoly(6, 11.8f, Mathf.Pi * 0.08f);
        for (int i = 0; i < rim.Length; i++)
            DrawLine(rim[i], rim[(i + 1) % rim.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.70f), 1.3f);
    }

    private void DrawShard(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 11f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));

        var body = new[]
        {
            new Vector2(9.5f, 0f),
            new Vector2(0f, -6.5f),
            new Vector2(-9.0f, -2.8f),
            new Vector2(-6.5f, 0f),
            new Vector2(-9.0f, 2.8f),
            new Vector2(0f, 6.5f),
        };
        DrawPolygon(body, new[] { style.BodyPrimary });
        DrawPolygon(new[]
        {
            new Vector2(6.0f, 0f),
            new Vector2(0f, -4.0f),
            new Vector2(-6.0f, 0f),
            new Vector2(0f, 4.0f),
        }, new[] { style.BodySecondary });

        for (int i = 0; i < body.Length; i++)
            DrawLine(body[i], body[(i + 1) % body.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.65f), 1.1f);

        DrawCircle(new Vector2(1.5f, 0f), 1.8f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.90f));
    }

    private void DrawShieldDrone(in EnemyRenderStyle style)
    {
        // Outer glow
        DrawCircle(Vector2.Zero, 15f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.18f));

        // Diamond body
        const float w = 8.0f;
        const float h = 10.5f;
        DrawPolygon(new Vector2[]
        {
            new Vector2(0f, -h),
            new Vector2( w,  0f),
            new Vector2(0f,  h),
            new Vector2(-w,  0f),
        }, new[] { style.BodyPrimary });

        // Inner core
        const float iw = 5.0f;
        const float ih = 6.8f;
        DrawPolygon(new Vector2[]
        {
            new Vector2(0f, -ih),
            new Vector2( iw,  0f),
            new Vector2(0f,  ih),
            new Vector2(-iw,  0f),
        }, new[] { style.BodySecondary });

        // Orbiting arc pair (static in icon, at a fixed angle to show the signature look)
        Color arcColor = new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.80f);
        DrawArc(Vector2.Zero, 13.5f, Mathf.Pi * 0.10f, Mathf.Pi * 0.75f, 14, arcColor, 1.5f);
        DrawArc(Vector2.Zero, 13.5f, Mathf.Pi * 1.10f, Mathf.Pi * 1.75f, 14, arcColor, 1.5f);

        // Central projector core
        DrawCircle(Vector2.Zero, 3.0f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.94f));
    }

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
