using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Floating damage number that drifts upward and fades over ~0.7 seconds.
/// Caller sets GlobalPosition after AddChild, then calls Initialize().
/// </summary>
public partial class DamageNumber : Node2D
{
    private const float Duration  = 0.70f;
    private const float RiseSpeed = 38f;

    private float  _life;
    private string _text   = "";
    private Color  _color;
    private bool   _isKill;
    private string _sourceTowerId = "";

    public void Initialize(float damage, Color color, bool isKill = false, string sourceTowerId = "")
    {
        _text   = $"{(int)damage}";
        _color  = color;
        _isKill = isKill;
        _sourceTowerId = sourceTowerId ?? "";
    }

    public override void _Process(double delta)
    {
        _life     += (float)delta;
        Position  += new Vector2(0f, -RiseSpeed * (float)delta);
        if (_life >= Duration) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t     = _life / Duration;
        float alpha = 1f - t * t;   // stays bright longer, then drops off
        var   font  = UITheme.SemiBold;
        int   size  = _isKill ? 24 : 18;
        var   col   = _isKill ? new Color(1f, 0.85f, 0.15f, alpha)
                               : new Color(_color.R, _color.G, _color.B, alpha);

        // Drop shadow
        DrawString(font, new Vector2(1f, 1f), _text, HorizontalAlignment.Center,
            -1, size, new Color(0f, 0f, 0f, alpha * 0.6f));
        // Main text
        DrawString(font, Vector2.Zero, _text, HorizontalAlignment.Center,
            -1, size, col);

        DrawSourceHint(alpha, size);
    }

    private void DrawSourceHint(float alpha, int numberSize)
    {
        if (_sourceTowerId.Length == 0) return;

        var tagColor = _sourceTowerId switch
        {
            "rapid_shooter" => new Color(0.25f, 0.88f, 1.00f, 0.86f * alpha),
            "heavy_cannon"  => new Color(1.00f, 0.60f, 0.20f, 0.90f * alpha),
            "marker_tower"  => new Color(1.00f, 0.34f, 0.74f, 0.88f * alpha),
            "chain_tower"   => new Color(0.62f, 0.95f, 1.00f, 0.88f * alpha),
            _               => new Color(0.85f, 0.90f, 1.00f, 0.84f * alpha),
        };

        float leftX = -Mathf.Max(14f, _text.Length * numberSize * 0.18f) - 9f;
        var notchRect = new Rect2(leftX, -numberSize * 0.66f, 4.5f, 9f);
        DrawRect(notchRect, tagColor);
        DrawRect(new Rect2(notchRect.Position + new Vector2(4.5f, 2.5f), new Vector2(2.5f, 4f)),
            new Color(tagColor.R, tagColor.G, tagColor.B, tagColor.A * 0.65f));
    }
}
