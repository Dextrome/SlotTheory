using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived world-space callout for major combat moments.
/// </summary>
public partial class CombatCallout : Node2D
{
    private const float Duration = 0.48f;
    private const float RiseSpeed = 26f;
    private static float _mobileReadabilityScale = 1f;

    private float _life = 0f;
    private string _text = "";
    private Color _color = Colors.White;

    public void Initialize(string text, Color color)
    {
        _text = text;
        _color = color;
    }

    public static void SetMobileReadabilityScale(float scale)
    {
        _mobileReadabilityScale = Mathf.Clamp(scale, 1f, 2f);
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        Position += new Vector2(0f, -RiseSpeed * (float)delta);
        if (_life >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _life / Duration;
        float alpha = 1f - t * t;
        int size = Mathf.Clamp(Mathf.RoundToInt(16f * _mobileReadabilityScale), 12, 44);
        var col = new Color(_color.R, _color.G, _color.B, alpha);
        DrawString(UITheme.Bold, new Vector2(1f, 1f), _text, HorizontalAlignment.Center, -1, size, new Color(0f, 0f, 0f, alpha * 0.65f));
        DrawString(UITheme.Bold, Vector2.Zero, _text, HorizontalAlignment.Center, -1, size, col);
    }
}
