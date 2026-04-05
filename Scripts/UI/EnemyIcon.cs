using Godot;
using SlotTheory.Core;
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
            "armored_walker"             => 18.8f,
            "reverse_walker"             => 15.8f,
            "shield_drone"               => 14.8f,
            EnemyCatalog.AnchorWalkerId  => 17.5f, // cross arm half-span
            EnemyCatalog.NullDroneId     => 14.5f, // ring outer edge + margin
            EnemyCatalog.LancerWalkerId  => 22.5f, // lance tip to tail fin
            EnemyCatalog.VeilWalkerId    => 12.0f, // pentagon radius + margin
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
            case EnemyCatalog.AnchorWalkerId: DrawAnchor(style); break;
            case EnemyCatalog.NullDroneId: DrawNullDrone(style); break;
            case EnemyCatalog.LancerWalkerId: DrawLancer(style); break;
            case EnemyCatalog.VeilWalkerId: DrawVeil(style); break;
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

    // ── Anchor Walker icon: cross / plus silhouette ──
    private void DrawAnchor(in EnemyRenderStyle style)
    {
        // Soft glow behind the cross
        DrawCircle(Vector2.Zero, 13.5f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.14f));

        // Cross / plus polygon (12 points) -- unique in the entire enemy roster
        var outer = CrossPoly(17.5f, 5.8f);
        var inner = CrossPoly(15.2f, 4.2f);
        DrawPolygon(outer, new[] { new Color(0.05f, 0.01f, 0.03f) });
        DrawPolygon(inner, new[] { style.BodyPrimary });

        // Center junction fill
        DrawRect(new Rect2(-4.2f, -4.2f, 8.4f, 8.4f), style.BodySecondary, true);

        // Arm-tip end-caps
        const float th = 4.2f;
        DrawRect(new Rect2(-th, -17.5f, th * 2f, 2.8f), style.BodySecondary, true);
        DrawRect(new Rect2(-th,  14.7f, th * 2f, 2.8f), style.BodySecondary, true);
        DrawRect(new Rect2(-17.5f, -th, 2.8f, th * 2f), style.BodySecondary, true);
        DrawRect(new Rect2( 14.7f, -th, 2.8f, th * 2f), style.BodySecondary, true);

        // Cross outline glow
        for (int i = 0; i < outer.Length; i++)
            DrawLine(outer[i], outer[(i + 1) % outer.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.76f), 1.2f);

        // Hot nodes at each of the 4 arm tips
        Color hot = new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.95f);
        DrawCircle(new Vector2(   0f, -17.5f), 2.2f, hot);
        DrawCircle(new Vector2(   0f,  17.5f), 2.2f, hot);
        DrawCircle(new Vector2(-17.5f,    0f), 2.2f, hot);
        DrawCircle(new Vector2( 17.5f,    0f), 2.2f, hot);
        DrawCircle(Vector2.Zero, 2.5f, hot);
    }

    // ── Null Drone icon: torus ring with struts and center core ──
    private void DrawNullDrone(in EnemyRenderStyle style)
    {
        // Expanding interference ring (static snapshot)
        DrawArc(Vector2.Zero, 12.5f, 0f, Mathf.Tau, 28,
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.22f), 1.0f);

        // Outer torus ring
        DrawArc(Vector2.Zero, 9.5f, 0f, Mathf.Tau, 40, style.BodyPrimary, 5.5f);

        // Four cardinal struts
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Pi * 0.5f;
            DrawLine(Vector2.Zero,
                new Vector2(Mathf.Cos(angle) * 6.7f, Mathf.Sin(angle) * 6.7f),
                new Color(style.BodySecondary.R, style.BodySecondary.G, style.BodySecondary.B, 0.82f), 1.5f);
        }

        // Ring glow outline
        DrawArc(Vector2.Zero, 9.5f, 0f, Mathf.Tau, 40,
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.84f), 1.1f);

        // Jammer sweep line (fixed angle in icon)
        DrawLine(Vector2.Zero, new Vector2(8.2f, -3.2f),
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.72f), 1.3f);

        // Center core
        DrawCircle(Vector2.Zero, 3.4f, style.BodySecondary);
        DrawCircle(Vector2.Zero, 1.9f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.96f));
    }

    // ── Lancer Walker icon: elongated javelin with forward lance spike ──
    private void DrawLancer(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 13f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.14f));

        // Elongated lance body
        var lanceBody = new Vector2[]
        {
            new(13.5f,  0f),
            new( 5.0f, -5.5f),
            new(-8.5f, -4.6f),
            new(-12.5f, 0f),
            new(-8.5f,  4.6f),
            new( 5.0f,  5.5f),
        };
        DrawPolygon(lanceBody, new[] { style.BodyPrimary });
        DrawPolygon(new Vector2[]
        {
            new(9.2f,  0f),
            new(3.2f, -3.6f),
            new(-7.8f, 0f),
            new(3.2f,  3.6f),
        }, new[] { style.BodySecondary });

        // Front lance spike -- the defining feature
        DrawPolygon(new Vector2[]
        {
            new(13.5f, -2.6f),
            new(22.5f,  0f),
            new(13.5f,  2.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.94f) });

        // Back stabilizer fins
        DrawPolygon(new Vector2[]
            { new(-8.5f, -2.8f), new(-19.5f, -9.0f), new(-12.5f, -1.4f) },
            new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.76f) });
        DrawPolygon(new Vector2[]
            { new(-8.5f,  2.8f), new(-19.5f,  9.0f), new(-12.5f,  1.4f) },
            new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.76f) });

        // Charge spine along the lance shaft
        DrawLine(new Vector2(-10.5f, 0f), new Vector2(20.0f, 0f),
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.84f), 1.3f);

        // Lance tip hot dot
        DrawCircle(new Vector2(21.5f, 0f), 2.0f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.96f));

        // Body edge highlight
        for (int i = 0; i < lanceBody.Length; i++)
            DrawLine(lanceBody[i], lanceBody[(i + 1) % lanceBody.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.68f), 1.0f);
    }

    // ── Veil Walker icon: pentagon with inner star spokes ──
    private void DrawVeil(in EnemyRenderStyle style)
    {
        DrawCircle(Vector2.Zero, 14.5f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.16f));

        // Pentagon body (5-sided -- unique in roster)
        DrawPolygon(RegularPoly(5, 10.2f, -Mathf.Pi * 0.5f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(5,  7.6f, -Mathf.Pi * 0.5f), new[] { style.BodySecondary });

        // Five spokes: outer corner → inner star point
        var outerPts = RegularPoly(5, 10.2f, -Mathf.Pi * 0.5f);
        var innerPts = RegularPoly(5,  4.6f, -Mathf.Pi * 0.5f + Mathf.Pi / 5f);
        for (int i = 0; i < 5; i++)
            DrawLine(outerPts[i], innerPts[i],
                new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.34f), 1.0f);

        // Pentagon outline glow
        for (int i = 0; i < outerPts.Length; i++)
            DrawLine(outerPts[i], outerPts[(i + 1) % outerPts.Length],
                new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.78f), 1.2f);

        // Shell ring halo (indicates the veil mechanic)
        DrawArc(Vector2.Zero, 13.5f, 0f, Mathf.Tau, 32,
            new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.52f), 1.4f);

        // Center core
        DrawCircle(Vector2.Zero, 2.8f,
            new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.55f));
        DrawCircle(Vector2.Zero, 1.5f,
            new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.92f));
    }

    // Helper: 12-point cross polygon (h = arm half-span, a = arm half-thickness)
    private static Vector2[] CrossPoly(float h, float a) => new Vector2[]
    {
        new(-a, -h), new( a, -h),
        new( a, -a), new( h, -a),
        new( h,  a), new( a,  a),
        new( a,  h), new(-a,  h),
        new(-a,  a), new(-h,  a),
        new(-h, -a), new(-a, -a),
    };

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
