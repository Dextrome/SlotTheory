using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Expanding ring used by Global Surge to sell a center-out cataclysm wave.
/// </summary>
public partial class GlobalSurgeRipple : Node2D
{
    private float _life;
    private float _duration = 0.55f;
    private float _startRadius = 18f;
    private float _endRadius = 620f;
    private float _baseWidth = 6f;
    private Color _color = new Color(1f, 0.92f, 0.60f, 1f);

    public void Initialize(Color color, float endRadius, float durationSec = 0.55f, float ringWidth = 6f)
    {
        _color = color;
        _endRadius = Mathf.Max(80f, endRadius);
        _duration = Mathf.Clamp(durationSec, 0.18f, 1.20f);
        _baseWidth = Mathf.Clamp(ringWidth, 1.6f, 10f);
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= _duration)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(_life / _duration, 0f, 1f);
        float ease = 1f - Mathf.Pow(1f - t, 2f);
        float radius = Mathf.Lerp(_startRadius, _endRadius, ease);
        float fade = 1f - t;

        float width = _baseWidth * (0.58f + 0.42f * fade);
        var ring = new Color(_color.R, _color.G, _color.B, 0.40f * fade);
        var inner = new Color(1f, 1f, 1f, 0.22f * fade);

        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 96, ring, width);
        DrawArc(Vector2.Zero, radius * 0.94f, 0f, Mathf.Tau, 96, inner, Mathf.Max(1.2f, width * 0.45f));
    }
}
