using Godot;
using SlotTheory.Entities;

namespace SlotTheory.UI;

public enum TargetModeIconSet
{
    Default,
    RiftSapper
}

/// <summary>
/// Procedural icon for tower targeting mode badges.
/// Drawn shapes keep all three modes visually balanced in size/weight.
/// </summary>
public partial class TargetModeIcon : Control
{
    private TargetingMode _mode = TargetingMode.First;
    private Color _iconColor = new Color(0.95f, 0.98f, 1.00f);
    private TargetModeIconSet _iconSet = TargetModeIconSet.Default;

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

    [Export]
    public TargetModeIconSet IconSet
    {
        get => _iconSet;
        set
        {
            if (_iconSet == value) return;
            _iconSet = value;
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
                if (_iconSet == TargetModeIconSet.RiftSapper) DrawRandomIcon(c, r);
                else DrawFirstIcon(c, r);
                break;
            case TargetingMode.Strongest:
                if (_iconSet == TargetModeIconSet.RiftSapper) DrawDownArrowIcon(c, r);
                else DrawStrongestIcon(c, r);
                break;
            case TargetingMode.LowestHp:
                if (_iconSet == TargetModeIconSet.RiftSapper) DrawUpArrowIcon(c, r);
                else DrawLowestHpIcon(c, r);
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

    private void DrawDownArrowIcon(Vector2 c, float r)
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

    private void DrawUpArrowIcon(Vector2 c, float r)
    {
        float stroke = Mathf.Max(1.6f, r * 0.38f);
        DrawLine(c + new Vector2(0f, r * 0.95f), c + new Vector2(0f, -r * 0.12f), _iconColor, stroke, true);

        var head = new[]
        {
            c + new Vector2(-r * 0.85f, -r * 0.12f),
            c + new Vector2(0f, -r * 1.02f),
            c + new Vector2(r * 0.85f, -r * 0.12f),
        };
        DrawColoredPolygon(head, _iconColor);
    }

    private void DrawRandomIcon(Vector2 c, float r)
    {
        float side = r * 1.55f;
        float half = side * 0.5f;
        var rect = new Rect2(c - new Vector2(half, half), new Vector2(side, side));

        DrawRect(rect, new Color(_iconColor.R, _iconColor.G, _iconColor.B, 0.14f), true);
        DrawRect(rect, _iconColor, false, Mathf.Max(1.4f, r * 0.24f), true);

        float pipR = Mathf.Max(1.0f, r * 0.17f);
        float pipOffset = side * 0.27f;
        DrawCircle(c, pipR, _iconColor);
        DrawCircle(c + new Vector2(-pipOffset, -pipOffset), pipR, _iconColor);
        DrawCircle(c + new Vector2(pipOffset, -pipOffset), pipR, _iconColor);
        DrawCircle(c + new Vector2(-pipOffset, pipOffset), pipR, _iconColor);
        DrawCircle(c + new Vector2(pipOffset, pipOffset), pipR, _iconColor);
    }
}
