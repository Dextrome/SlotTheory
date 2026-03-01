using Godot;

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
    private string _text  = "";
    private Color  _color;

    public void Initialize(float damage, Color color)
    {
        _text  = $"{(int)damage}";
        _color = color;
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
        var   font  = ThemeDB.FallbackFont;

        // Drop shadow
        DrawString(font, new Vector2(1f, 1f), _text, HorizontalAlignment.Center,
            -1, 18, new Color(0f, 0f, 0f, alpha * 0.6f));
        // Main text
        DrawString(font, Vector2.Zero, _text, HorizontalAlignment.Center,
            -1, 18, new Color(_color.R, _color.G, _color.B, alpha));
    }
}
