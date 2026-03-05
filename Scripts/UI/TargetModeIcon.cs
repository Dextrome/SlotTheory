using Godot;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Procedural icon for tower targeting mode badges.
/// Drawn shapes keep all three modes visually balanced in size/weight.
/// </summary>
public partial class TargetModeIcon : Control
{
    private TargetingMode _mode = TargetingMode.First;
    private Color _iconColor = new Color(0.95f, 0.98f, 1.00f);

    [Export]
    public TargetingMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
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
            CustomMinimumSize = new Vector2(14f, 14f);
    }

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.34f;
        DrawCircle(c, r + 3.0f, new Color(_iconColor.R, _iconColor.G, _iconColor.B, 0.22f));

        switch (_mode)
        {
            case TargetingMode.First:
                DrawFirstIcon(c, r);
                break;
            case TargetingMode.Strongest:
                DrawStrongestIcon(c, r);
                break;
            case TargetingMode.LowestHp:
                DrawLowestHpIcon(c, r);
                break;
        }
    }

    private void DrawFirstIcon(Vector2 c, float r)
    {
        float stroke = Mathf.Max(1.6f, r * 0.38f);
        DrawLine(c + new Vector2(-r * 0.95f, 0f), c + new Vector2(r * 0.08f, 0f), _iconColor, stroke, true);

        var head = new[]
        {
            c + new Vector2(r * 0.08f, -r * 0.85f),
            c + new Vector2(r * 1.02f, 0f),
            c + new Vector2(r * 0.08f, r * 0.85f),
        };
        DrawColoredPolygon(head, _iconColor);
    }

    private void DrawStrongestIcon(Vector2 c, float r)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < pts.Length; i++)
        {
            float a = -Mathf.Pi / 2f + i * Mathf.Tau / pts.Length;
            float pr = (i % 2 == 0) ? r * 1.02f : r * 0.44f;
            pts[i] = c + new Vector2(Mathf.Cos(a) * pr, Mathf.Sin(a) * pr);
        }
        DrawColoredPolygon(pts, _iconColor);
    }

    private void DrawLowestHpIcon(Vector2 c, float r)
    {
        float stroke = Mathf.Max(1.6f, r * 0.38f);
        DrawLine(c + new Vector2(0f, -r * 0.95f), c + new Vector2(0f, r * 0.12f), _iconColor, stroke, true);

        var head = new[]
        {
            c + new Vector2(-r * 0.85f, r * 0.12f),
            c + new Vector2(0f, r * 1.02f),
            c + new Vector2(r * 0.85f, r * 0.12f),
        };
        DrawColoredPolygon(head, _iconColor);
    }
}
