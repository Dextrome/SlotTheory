using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Endpoint compression pulse shown when an undertow drag resolves.
/// </summary>
public partial class UndertowReleaseVfx : Node2D
{
    private const float Duration = 0.34f;

    private Color _color = new(0.08f, 0.64f, 0.86f);
    private float _maxRadius = 80f;
    private bool _major;
    private float _elapsed;

    public void Initialize(Color color, float maxRadius, bool major)
    {
        _color = color;
        _maxRadius = Mathf.Max(24f, maxRadius);
        _major = major;
        _elapsed = 0f;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(_elapsed / Duration, 0f, 1f);
        float inv = 1f - t;
        float majorScale = _major ? 1.14f : 1f;

        float shellRadius = _maxRadius * (0.28f + t * 0.78f) * majorScale;
        float shellAlpha = inv * (_major ? 0.56f : 0.42f);
        DrawArc(Vector2.Zero, shellRadius, 0f, Mathf.Tau, 48,
            new Color(_color.R, _color.G, _color.B, shellAlpha), 2.2f);

        float coreRadius = _maxRadius * (0.12f + t * 0.32f) * majorScale;
        DrawCircle(Vector2.Zero, coreRadius,
            new Color(_color.R, _color.G, _color.B, inv * (_major ? 0.30f : 0.22f)));
        DrawCircle(Vector2.Zero, coreRadius * 0.42f,
            new Color(1f, 1f, 1f, inv * (_major ? 0.48f : 0.34f)));

        int spokes = _major ? 10 : 8;
        float spokeStart = shellRadius * 0.92f;
        float spokeEnd = shellRadius * 0.44f;
        float spokeAlpha = inv * (_major ? 0.40f : 0.28f);
        for (int i = 0; i < spokes; i++)
        {
            float a = i * Mathf.Tau / spokes + t * 0.45f;
            Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(dir * spokeStart, dir * spokeEnd,
                new Color(_color.R, _color.G, _color.B, spokeAlpha), 1.6f);
        }
    }
}
