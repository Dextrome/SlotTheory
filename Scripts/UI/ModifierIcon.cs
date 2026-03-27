using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Lightweight procedural icon for modifier cards.
/// </summary>
public partial class ModifierIcon : Control
{
    private string _modifierId = "";
    private Color _iconColor = Colors.White;

    [Export]
    public string ModifierId
    {
        get => _modifierId;
        set
        {
            if (_modifierId == value) return;
            _modifierId = value;
            QueueRedraw();
        }
    }

    [Export]
    public Color IconColor
    {
        get => _iconColor;
        set
        {
            if (_iconColor == value) return;
            _iconColor = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (CustomMinimumSize == Vector2.Zero)
            CustomMinimumSize = new Vector2(24f, 24f);
    }

    public override void _Draw()
    {
        var c = new Vector2(Size.X * 0.5f, Size.Y * 0.5f);
        float r = Mathf.Min(Size.X, Size.Y) * 0.35f;
        float glowPad = Mathf.Clamp(r * 0.90f, 1.1f, 4.0f);
        float ringPad = Mathf.Clamp(r * 0.45f, 0.8f, 2.0f);
        float ringWidth = Mathf.Clamp(r * 0.35f, 0.9f, 1.2f);
        var glow = new Color(_iconColor.R, _iconColor.G, _iconColor.B, 0.20f);

        DrawCircle(c, r + glowPad, glow);
        DrawArc(c, r + ringPad, 0f, Mathf.Tau, 28, new Color(_iconColor.R, _iconColor.G, _iconColor.B, 0.55f), ringWidth);

        switch (_modifierId)
        {
            case "momentum": DrawMomentum(c, r); break;
            case "overkill": DrawOverkill(c, r); break;
            case "exploit_weakness": DrawExploitWeakness(c, r); break;
            case "focus_lens": DrawFocusLens(c, r); break;
            case "slow": DrawSlow(c, r); break;
            case "overreach": DrawOverreach(c, r); break;
            case "hair_trigger": DrawHairTrigger(c, r); break;
            case "split_shot": DrawSplitShot(c, r); break;
            case "feedback_loop": DrawFeedbackLoop(c, r); break;
            case "chain_reaction": DrawChainReaction(c, r); break;
            case "blast_core": DrawBlastCore(c, r); break;
            case "wildfire": DrawWildfire(c, r); break;
            case "afterimage": DrawAfterimage(c, r); break;
            case "reaper_protocol": DrawReaperProtocol(c, r); break;
            default:
                DrawCircle(c, r * 0.45f, _iconColor);
                break;
        }
    }

    private void DrawMomentum(Vector2 c, float r)
    {
        DrawLine(c + new Vector2(-r * 0.8f, 0), c + new Vector2(r * 0.3f, 0), _iconColor, 2f);
        DrawLine(c + new Vector2(r * 0.1f, -r * 0.45f), c + new Vector2(r * 0.6f, 0), _iconColor, 2f);
        DrawLine(c + new Vector2(r * 0.1f, r * 0.45f), c + new Vector2(r * 0.6f, 0), _iconColor, 2f);
    }

    private void DrawOverkill(Vector2 c, float r)
    {
        DrawCircle(c, r * 0.35f, Colors.Transparent);
        DrawArc(c, r * 0.35f, 0f, Mathf.Tau, 24, _iconColor, 2f);
        DrawLine(c + new Vector2(-r * 0.85f, 0), c + new Vector2(-r * 0.2f, 0), _iconColor, 2f);
        DrawLine(c + new Vector2(r * 0.2f, 0), c + new Vector2(r * 0.85f, 0), _iconColor, 2f);
        DrawLine(c + new Vector2(0, -r * 0.85f), c + new Vector2(0, -r * 0.2f), _iconColor, 2f);
        DrawLine(c + new Vector2(0, r * 0.2f), c + new Vector2(0, r * 0.85f), _iconColor, 2f);
    }

    private void DrawExploitWeakness(Vector2 c, float r)
    {
        var pts = new[]
        {
            c + new Vector2(0, -r * 0.9f),
            c + new Vector2(r * 0.75f, -r * 0.2f),
            c + new Vector2(r * 0.45f, r * 0.9f),
            c + new Vector2(-r * 0.45f, r * 0.9f),
            c + new Vector2(-r * 0.75f, -r * 0.2f),
            c + new Vector2(0, -r * 0.9f),
        };
        DrawPolyline(pts, _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.35f, -r * 0.05f), c + new Vector2(r * 0.5f, r * 0.65f), _iconColor, 2f);
    }

    private void DrawFocusLens(Vector2 c, float r)
    {
        DrawArc(c, r * 0.75f, 0f, Mathf.Tau, 28, _iconColor, 2f);
        DrawArc(c, r * 0.30f, 0f, Mathf.Tau, 24, _iconColor, 1.6f);
        DrawLine(c + new Vector2(r * 0.45f, r * 0.45f), c + new Vector2(r * 0.9f, r * 0.9f), _iconColor, 2f);
    }

    private void DrawSlow(Vector2 c, float r)
    {
        DrawLine(c + new Vector2(0, -r * 0.9f), c + new Vector2(0, r * 0.9f), _iconColor, 1.6f);
        DrawLine(c + new Vector2(-r * 0.9f, 0), c + new Vector2(r * 0.9f, 0), _iconColor, 1.6f);
        DrawLine(c + new Vector2(-r * 0.65f, -r * 0.65f), c + new Vector2(r * 0.65f, r * 0.65f), _iconColor, 1.3f);
        DrawLine(c + new Vector2(r * 0.65f, -r * 0.65f), c + new Vector2(-r * 0.65f, r * 0.65f), _iconColor, 1.3f);
    }

    private void DrawOverreach(Vector2 c, float r)
    {
        DrawArc(c, r * 0.25f, 0f, Mathf.Tau, 22, _iconColor, 1.8f);
        DrawLine(c + new Vector2(0, -r * 0.2f), c + new Vector2(0, -r * 0.95f), _iconColor, 1.8f);
        DrawLine(c + new Vector2(0, r * 0.2f), c + new Vector2(0, r * 0.95f), _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.2f, 0), c + new Vector2(-r * 0.95f, 0), _iconColor, 1.8f);
        DrawLine(c + new Vector2(r * 0.2f, 0), c + new Vector2(r * 0.95f, 0), _iconColor, 1.8f);
    }

    private void DrawHairTrigger(Vector2 c, float r)
    {
        var bolt = new[]
        {
            c + new Vector2(-r * 0.15f, -r * 0.9f),
            c + new Vector2(r * 0.25f, -r * 0.2f),
            c + new Vector2(-r * 0.05f, -r * 0.2f),
            c + new Vector2(r * 0.15f, r * 0.9f),
            c + new Vector2(-r * 0.35f, r * 0.2f),
            c + new Vector2(-r * 0.05f, r * 0.2f),
        };
        DrawColoredPolygon(bolt, _iconColor);
    }

    private void DrawSplitShot(Vector2 c, float r)
    {
        DrawLine(c + new Vector2(-r * 0.85f, 0), c + new Vector2(-r * 0.25f, 0), _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.15f, 0), c + new Vector2(r * 0.7f, -r * 0.55f), _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.15f, 0), c + new Vector2(r * 0.75f, 0), _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.15f, 0), c + new Vector2(r * 0.7f, r * 0.55f), _iconColor, 1.8f);
    }

    private void DrawFeedbackLoop(Vector2 c, float r)
    {
        DrawArc(c, r * 0.75f, Mathf.Pi * 0.2f, Mathf.Pi * 1.35f, 24, _iconColor, 2f);
        DrawArc(c, r * 0.75f, Mathf.Pi * 1.2f, Mathf.Pi * 2.35f, 24, _iconColor, 2f);
        DrawLine(c + new Vector2(-r * 0.6f, -r * 0.25f), c + new Vector2(-r * 0.85f, -r * 0.45f), _iconColor, 2f);
        DrawLine(c + new Vector2(r * 0.6f, r * 0.25f), c + new Vector2(r * 0.85f, r * 0.45f), _iconColor, 2f);
    }

    private void DrawChainReaction(Vector2 c, float r)
    {
        DrawArc(c + new Vector2(-r * 0.35f, 0), r * 0.32f, 0f, Mathf.Tau, 24, _iconColor, 1.8f);
        DrawArc(c + new Vector2(r * 0.35f, 0), r * 0.32f, 0f, Mathf.Tau, 24, _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.05f, -r * 0.22f), c + new Vector2(r * 0.05f, -r * 0.22f), _iconColor, 1.8f);
        DrawLine(c + new Vector2(-r * 0.05f, r * 0.22f), c + new Vector2(r * 0.05f, r * 0.22f), _iconColor, 1.8f);
    }

    private void DrawBlastCore(Vector2 c, float r)
    {
        // Explosion burst: small center circle + 8 radiating spikes
        DrawCircle(c, r * 0.22f, _iconColor);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            DrawLine(c + dir * r * 0.32f, c + dir * r * 0.88f, _iconColor, 1.8f);
        }
    }

    private void DrawReaperProtocol(Vector2 c, float r)
    {
        // Green cross -- classic healing symbol.
        float armW = r * 0.32f;
        float armH = r * 0.88f;
        // Vertical bar
        DrawLine(c + new Vector2(0, -armH), c + new Vector2(0, armH), _iconColor, armW * 2f);
        // Horizontal bar
        DrawLine(c + new Vector2(-armH, 0), c + new Vector2(armH, 0), _iconColor, armW * 2f);
    }

    private void DrawWildfire(Vector2 c, float r)
    {
        // Flame silhouette: teardrop pointing up, with two side-curving tendrils.
        // Reads as "fire" at small icon sizes -- distinct from explosion (spikes) and chain (rings).

        // Central flame body -- tall teardrop
        var flame = new[]
        {
            c + new Vector2(0,        -r * 0.92f),  // tip
            c + new Vector2( r * 0.42f,  r * 0.10f),
            c + new Vector2( r * 0.30f,  r * 0.72f),
            c + new Vector2(0,           r * 0.50f), // base
            c + new Vector2(-r * 0.30f,  r * 0.72f),
            c + new Vector2(-r * 0.42f,  r * 0.10f),
            c + new Vector2(0,        -r * 0.92f),
        };
        DrawPolyline(flame, _iconColor, 2.0f);

        // Left ember tendril -- short curved line suggesting a trail fragment
        DrawLine(c + new Vector2(-r * 0.55f, r * 0.30f),
                 c + new Vector2(-r * 0.88f, r * 0.65f), _iconColor, 1.5f);

        // Right ember tendril
        DrawLine(c + new Vector2( r * 0.55f, r * 0.30f),
                 c + new Vector2( r * 0.88f, r * 0.65f), _iconColor, 1.5f);

        // Hot inner core dot
        DrawCircle(c + new Vector2(0, r * 0.10f), r * 0.18f, _iconColor);
    }

    private void DrawAfterimage(Vector2 c, float r)
    {
        // Seed imprint ring.
        DrawArc(c + new Vector2(-r * 0.10f, r * 0.08f), r * 0.44f, 0f, Mathf.Tau, 24, _iconColor, 1.7f);
        // Delayed replay ring.
        DrawArc(c + new Vector2(r * 0.30f, -r * 0.24f), r * 0.34f, 0f, Mathf.Tau, 24, _iconColor, 1.5f);
        // Echo link.
        DrawLine(c + new Vector2(-r * 0.04f, -r * 0.02f), c + new Vector2(r * 0.18f, -r * 0.18f), _iconColor, 1.6f);
        // Trigger spark.
        DrawCircle(c + new Vector2(r * 0.30f, -r * 0.24f), r * 0.10f, _iconColor);
    }
}
