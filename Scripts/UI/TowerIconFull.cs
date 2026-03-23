using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Procedural icon that replicates the actual in-game tower _Draw() shapes.
/// Used in contexts (like the Slot Codex) where a detailed static rendering is appropriate.
/// Uses the same geometry as TowerInstance but with idle/neutral state (no shot kick or idle sway).
/// </summary>
public partial class TowerIconFull : Control
{
    private string _towerId = "";

    [Export]
    public string TowerId
    {
        get => _towerId;
        set
        {
            if (_towerId == value) return;
            _towerId = value ?? "";
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
        // Each tower has a visual bounding box in content space.
        // visualCenterY: y coordinate of the content's visual center (shifts barrel towers upward so they're centered in the icon).
        // naturalHalf: largest extent from that center, used to compute scale.
        var (naturalHalf, visualCenterY) = _towerId switch
        {
            "rapid_shooter"    => (24f, -5.5f),   // barrel top ~-23, base bottom ~+12
            "heavy_cannon"     => (24f, -9.0f),   // barrel top ~-32 (scaled), base bottom ~+14
            "marker_tower"     => (22f, -3.0f),   // antenna tip ~-22, diamond bottom ~+16
            "chain_tower"      => (20f,  0.0f),   // prong tips at ~r=19, symmetric
            "rift_prism"       => (23f,  0.0f),   // fins at ±22, symmetric
            "accordion_engine" => (23f,  0.0f),   // claw arm tips at ±18, symmetric
            "phase_splitter"   => (24f,  0.0f),   // mirrored emitters at ±16
            _                  => (14f,  0.0f),
        };

        float available = Mathf.Min(Size.X, Size.Y) * 0.44f;
        float s = available / naturalHalf;

        // Shift the draw origin so the visual center lands at the icon center.
        var offset = new Vector2(Size.X * 0.5f, Size.Y * 0.5f - visualCenterY * s);
        DrawSetTransform(offset, 0f, new Vector2(s, s));

        switch (_towerId)
        {
            case "rapid_shooter":    DrawRapidShooter();    break;
            case "heavy_cannon":     DrawHeavyCannon();     break;
            case "marker_tower":     DrawMarkerTower();     break;
            case "chain_tower":      DrawChainTower();      break;
            case "rift_prism":       DrawRiftSapper();      break;
            case "accordion_engine": DrawAccordionEngine(); break;
            case "phase_splitter":   DrawPhaseSplitter();   break;
            default:
                DrawCircle(Vector2.Zero, 10f, new Color(0.2f, 0.5f, 1.0f));
                break;
        }
    }

    // ── Rapid Shooter ────────────────────────────────────────────────────────

    private void DrawRapidShooter()
    {
        var cyan  = new Color(0.10f, 0.75f, 1.00f);
        var dark  = new Color(0.04f, 0.06f, 0.20f);
        var flash = new Color(0.70f, 0.95f, 1.00f);

        // Soft glow
        DrawCircle(Vector2.Zero, 22f, new Color(cyan.R, cyan.G, cyan.B, 0.07f));
        DrawCircle(Vector2.Zero, 15f, new Color(cyan.R, cyan.G, cyan.B, 0.14f));

        // Barrel
        DrawRect(new Rect2(-3.5f, -23f, 7f, 18f), cyan);
        DrawRect(new Rect2(-2.0f, -22f, 4f, 16f), dark);
        // Muzzle cap
        DrawCircle(new Vector2(0f, -23f), 4f, flash);

        // Hexagonal base
        DrawPolygon(RegularPoly(6, 12f, -Mathf.Pi / 6f), new[] { cyan });
        DrawPolygon(RegularPoly(6, 10f, -Mathf.Pi / 6f), new[] { dark });
        DrawCircle(Vector2.Zero, 3.5f, cyan);
    }

    // ── Heavy Cannon ─────────────────────────────────────────────────────────

    private void DrawHeavyCannon()
    {
        var orange = new Color(1.00f, 0.55f, 0.00f);
        var dark   = new Color(0.16f, 0.08f, 0.02f);
        var rim    = new Color(1.00f, 0.82f, 0.30f);

        // Soft glow
        DrawCircle(Vector2.Zero, 24f, new Color(orange.R, orange.G, orange.B, 0.07f));
        DrawCircle(Vector2.Zero, 17f, new Color(orange.R, orange.G, orange.B, 0.14f));

        // Octagonal base drawn first (barrel overlaps it)
        DrawPolygon(RegularPoly(8, 14f, 0f), new[] { orange });
        DrawPolygon(RegularPoly(8, 12f, 0f), new[] { dark });
        DrawCircle(Vector2.Zero, 4f, rim);

        // Barrel
        DrawRect(new Rect2(-6.6f, -27f, 13.2f, 20f), orange);
        DrawRect(new Rect2(-4.6f, -26f, 9.2f, 18f),  dark);
        // Muzzle band
        DrawRect(new Rect2(-6.8f, -31f, 13.6f, 4.5f), rim);
        DrawRect(new Rect2(-4.2f, -32f,  8.4f, 4.5f), new Color(rim.R, rim.G, rim.B, 0.36f));
    }

    // ── Marker Tower ─────────────────────────────────────────────────────────

    private void DrawMarkerTower()
    {
        var pink = new Color(1.00f, 0.15f, 0.60f);
        var dark = new Color(0.18f, 0.04f, 0.12f);
        var beam = new Color(0.90f, 0.70f, 1.00f);

        // Soft glow
        DrawCircle(Vector2.Zero, 20f, new Color(pink.R, pink.G, pink.B, 0.07f));
        DrawCircle(Vector2.Zero, 14f, new Color(pink.R, pink.G, pink.B, 0.14f));

        // Diamond body
        DrawPolygon(new[] { new Vector2(0,-16), new Vector2(16,0), new Vector2(0,16), new Vector2(-16,0) }, new[] { pink });
        DrawPolygon(new[] { new Vector2(0,-13), new Vector2(13,0), new Vector2(0,13), new Vector2(-13,0) }, new[] { dark });

        // Antenna
        var baseP = new Vector2(0f, -13f);
        var tipP  = new Vector2(0f, -22f);
        DrawLine(baseP, tipP, beam, 2f);
        DrawCircle(tipP, 6f, new Color(beam.R, beam.G, beam.B, 0.22f));
        DrawCircle(tipP, 4f, beam);
        DrawCircle(tipP, 1.5f, new Color(1f, 0.95f, 1f));
    }

    // ── Arc Emitter (chain_tower) ─────────────────────────────────────────────

    private void DrawChainTower()
    {
        var blue  = new Color(0.50f, 0.85f, 1.00f);
        var dark  = new Color(0.05f, 0.10f, 0.22f);
        var white = new Color(0.90f, 0.97f, 1.00f);

        // Soft glow
        DrawCircle(Vector2.Zero, 22f, new Color(blue.R, blue.G, blue.B, 0.09f));
        DrawCircle(Vector2.Zero, 15f, new Color(blue.R, blue.G, blue.B, 0.16f));

        // Circular base
        DrawCircle(Vector2.Zero, 11f, blue);
        DrawCircle(Vector2.Zero,  9f, dark);

        // Three discharge prongs (120° apart)
        for (int i = 0; i < 3; i++)
        {
            float angle    = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
            var   dir      = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var   prongBase = dir * 9f;
            var   prongTip  = dir * 19f;
            DrawLine(prongBase, prongTip, blue, 3f);
            DrawCircle(prongTip, 4f, new Color(blue.R, blue.G, blue.B, 0.50f));
            DrawCircle(prongTip, 2.5f, white);
        }

        // Inner energy core
        DrawCircle(Vector2.Zero, 4f, new Color(blue.R, blue.G, blue.B, 0.55f));
        DrawCircle(Vector2.Zero, 2.5f, white);
    }

    // ── Rift Sapper (rift_prism) ──────────────────────────────────────────────

    private void DrawRiftSapper()
    {
        var lime  = new Color(0.62f, 1.00f, 0.58f);
        var dark  = new Color(0.06f, 0.18f, 0.10f);
        var fin   = new Color(0.88f, 1.00f, 0.82f, 0.85f);
        var glyph = new Color(0.96f, 1.00f, 0.90f, 0.90f);

        // Halos
        DrawCircle(Vector2.Zero, 25f, new Color(lime.R, lime.G, lime.B, 0.09f));
        DrawCircle(Vector2.Zero, 17f, new Color(lime.R, lime.G, lime.B, 0.16f));

        // Outer body shell
        DrawPolygon(RegularPoly(8, 14.2f, Mathf.Pi / 8f), new[] { lime });
        DrawPolygon(RegularPoly(8, 11.6f, Mathf.Pi / 8f), new[] { dark });

        // Mine-cell fins
        DrawRect(new Rect2(-10f, -22f, 20f, 4.6f), fin);
        DrawRect(new Rect2(-10f,  17.4f, 20f, 4.6f), fin);
        DrawRect(new Rect2(-22f, -10f, 4.6f, 20f), fin);
        DrawRect(new Rect2( 17.4f, -10f, 4.6f, 20f), fin);

        // Central mine glyph
        DrawCircle(Vector2.Zero, 5.3f, new Color(lime.R, lime.G, lime.B, 0.60f));
        DrawLine(new Vector2(-4f, 0f), new Vector2(4f, 0f), glyph, 1.7f);
        DrawLine(new Vector2(0f, -4f), new Vector2(0f, 4f), glyph, 1.7f);
        DrawCircle(Vector2.Zero, 2.2f, new Color(1f, 1f, 0.95f, 0.94f));
    }

    // ── Accordion Engine ─────────────────────────────────────────────────────

    private void DrawAccordionEngine()
    {
        var violet = new Color(0.72f, 0.20f, 1.00f);
        var dark   = new Color(0.10f, 0.02f, 0.20f);
        var bright = new Color(0.88f, 0.55f, 1.00f);

        // Soft glow
        DrawCircle(Vector2.Zero, 26f, new Color(violet.R, violet.G, violet.B, 0.07f));
        DrawCircle(Vector2.Zero, 17f, new Color(violet.R, violet.G, violet.B, 0.16f));

        // Hexagonal core body
        DrawPolygon(RegularPoly(6, 13f, 0f), new[] { violet });
        DrawPolygon(RegularPoly(6, 10.5f, 0f), new[] { dark });

        // Resonator ring
        DrawArc(Vector2.Zero, 17f, 0f, Mathf.Tau, 48, new Color(bright.R, bright.G, bright.B, 0.28f), 1.6f);

        // Compression claw arms: top and bottom (neutral, no compression offset)
        const float armY = 18f;
        var clawColor = new Color(bright.R, bright.G, bright.B, 0.82f);
        DrawLine(new Vector2(-8f, -armY),           new Vector2( 8f, -armY),           clawColor, 2.8f);
        DrawLine(new Vector2(-8f, -armY),           new Vector2(-8f, -armY + 4.5f),    clawColor, 2.2f);
        DrawLine(new Vector2( 8f, -armY),           new Vector2( 8f, -armY + 4.5f),    clawColor, 2.2f);
        DrawLine(new Vector2(-8f,  armY),           new Vector2( 8f,  armY),           clawColor, 2.8f);
        DrawLine(new Vector2(-8f,  armY),           new Vector2(-8f,  armY - 4.5f),    clawColor, 2.2f);
        DrawLine(new Vector2( 8f,  armY),           new Vector2( 8f,  armY - 4.5f),    clawColor, 2.2f);

        // Core energy node
        DrawCircle(Vector2.Zero, 4.5f, new Color(violet.R, violet.G, violet.B, 0.68f));
        DrawCircle(Vector2.Zero, 2.5f, new Color(1f, 0.85f, 1f, 0.90f));
    }

    // -- Phase Splitter --

    private void DrawPhaseSplitter()
    {
        var aqua = new Color(0.45f, 1.00f, 0.95f);
        var dark = new Color(0.07f, 0.16f, 0.22f);
        var white = new Color(0.92f, 1.00f, 1.00f);

        DrawCircle(Vector2.Zero, 24f, new Color(aqua.R, aqua.G, aqua.B, 0.08f));
        DrawCircle(Vector2.Zero, 16f, new Color(aqua.R, aqua.G, aqua.B, 0.15f));

        DrawPolygon(RegularPoly(10, 10.5f, Mathf.Pi / 10f), new[] { aqua });
        DrawPolygon(RegularPoly(10, 8.2f, Mathf.Pi / 10f), new[] { dark });

        DrawPolygon(new[]
        {
            new Vector2(-3.0f, -16f),
            new Vector2( 3.0f, -16f),
            new Vector2( 2.0f, -9f),
            new Vector2(-2.0f, -9f),
        }, new[] { aqua });
        DrawPolygon(new[]
        {
            new Vector2(-3.0f, 16f),
            new Vector2( 3.0f, 16f),
            new Vector2( 2.0f, 9f),
            new Vector2(-2.0f, 9f),
        }, new[] { aqua });

        DrawCircle(new Vector2(0f, -16f), 3.4f, new Color(white.R, white.G, white.B, 0.86f));
        DrawCircle(new Vector2(0f, 16f), 3.4f, new Color(white.R, white.G, white.B, 0.86f));
        DrawArc(Vector2.Zero, 13.5f, -Mathf.Pi * 0.43f, Mathf.Pi * 0.43f, 26, new Color(aqua.R, aqua.G, aqua.B, 0.36f), 1.8f);
        DrawArc(Vector2.Zero, 13.5f, Mathf.Pi * 0.57f, Mathf.Pi * 1.43f, 26, new Color(aqua.R, aqua.G, aqua.B, 0.36f), 1.8f);
        DrawCircle(Vector2.Zero, 2.6f, new Color(1f, 1f, 1f, 0.90f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
