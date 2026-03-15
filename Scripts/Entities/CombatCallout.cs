using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived world-space callout for major combat moments.
/// </summary>
public partial class CombatCallout : Node2D
{
    private const float DefaultDuration = 0.96f;
    private const float RiseSpeed = 13f;
    private static float _mobileReadabilityScale = 1f;
    private static readonly Vector2[] OutlineDirs =
    {
        new Vector2(-1f, 0f),
        new Vector2(1f, 0f),
        new Vector2(0f, -1f),
        new Vector2(0f, 1f),
        new Vector2(-0.7071f, -0.7071f),
        new Vector2(0.7071f, -0.7071f),
        new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, 0.7071f),
    };

    private float _life = 0f;
    private float _duration = DefaultDuration;
    private string _text = "";
    private Color _color = Colors.White;

    private int _sizeOverride = 0;

    public void Initialize(string text, Color color, float duration = DefaultDuration, int sizeOverride = 0)
    {
        _text = text;
        _color = color;
        _duration = Mathf.Max(0.1f, duration);
        _sizeOverride = sizeOverride;
    }

    public static void SetMobileReadabilityScale(float scale)
    {
        _mobileReadabilityScale = Mathf.Clamp(scale, 1f, 2f);
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        Position += new Vector2(0f, -RiseSpeed * (float)delta);
        if (_life >= _duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _life / _duration;
        float alpha = 1f - t * t * t;
        int size = _sizeOverride > 0
            ? Mathf.Clamp(Mathf.RoundToInt(_sizeOverride * _mobileReadabilityScale), 12, 72)
            : Mathf.Clamp(Mathf.RoundToInt(16f * _mobileReadabilityScale), 12, 44);
        var col = new Color(_color.R, _color.G, _color.B, alpha);

        // High-contrast layered outline: dark outer ring + light inner ring.
        DrawOutline(UITheme.Bold, size, 2.2f, new Color(0f, 0f, 0f, alpha * 0.92f));
        DrawOutline(UITheme.Bold, size, 1.1f, new Color(1f, 1f, 1f, alpha * 0.94f));
        DrawString(UITheme.Bold, Vector2.Zero, _text, HorizontalAlignment.Center, -1, size, col);
    }

    private void DrawOutline(Font font, int size, float radius, Color color)
    {
        for (int i = 0; i < OutlineDirs.Length; i++)
        {
            Vector2 offset = OutlineDirs[i] * radius;
            DrawString(font, offset, _text, HorizontalAlignment.Center, -1, size, color);
        }
    }
}
