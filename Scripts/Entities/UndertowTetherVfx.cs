using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived reverse-current tether that visually links tower and enemy during an active undertow drag.
/// </summary>
public partial class UndertowTetherVfx : Node2D
{
    private Node2D? _source;
    private EnemyInstance? _target;
    private Color _color = new(0.08f, 0.64f, 0.86f);
    private float _duration = 0.75f;
    private float _elapsed;
    private bool _secondary;
    private float _progress;

    public void Initialize(Node2D source, EnemyInstance target, Color color, float duration, bool secondary)
    {
        _source = source;
        _target = target;
        _color = color;
        _duration = Mathf.Max(0.12f, duration);
        _secondary = secondary;
        _elapsed = 0f;
        _progress = 0f;
    }

    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp(progress, 0f, 1f);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        if (_elapsed >= _duration)
        {
            QueueFree();
            return;
        }

        if (_source == null || !GodotObject.IsInstanceValid(_source) ||
            _target == null || !GodotObject.IsInstanceValid(_target) || _target.Hp <= 0f)
        {
            QueueFree();
            return;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_source == null || _target == null || !GodotObject.IsInstanceValid(_source) || !GodotObject.IsInstanceValid(_target))
            return;

        Vector2 source = ToLocal(_source.GlobalPosition);
        Vector2 target = ToLocal(_target.GlobalPosition);
        Vector2 span = target - source;
        float len = span.Length();
        if (len < 2f)
            return;

        float life = Mathf.Clamp(1f - (_elapsed / _duration), 0f, 1f);
        float secondaryScale = _secondary ? 0.76f : 1f;
        float beamAlpha = (0.24f + _progress * 0.46f) * life * secondaryScale;
        float beamWidth = (2.4f + _progress * 1.3f) * secondaryScale;

        DrawLine(source, target, new Color(_color.R, _color.G, _color.B, beamAlpha * 0.45f), beamWidth * 2.2f);
        DrawLine(source, target, new Color(0.92f, 0.98f, 1f, beamAlpha * 0.68f), beamWidth * 0.66f);

        Vector2 dir = span / len;
        Vector2 perp = new(-dir.Y, dir.X);
        float phase = _elapsed * 11.5f;
        int knots = _secondary ? 3 : 4;
        for (int i = 1; i <= knots; i++)
        {
            float t = i / (float)(knots + 1);
            float wobble = Mathf.Sin(phase + i * 0.92f) * (5.2f - i * 0.6f) * secondaryScale;
            Vector2 p = source + span * t + perp * wobble;
            float r = Mathf.Lerp(4.2f, 2.0f, t) * secondaryScale;
            float a = beamAlpha * Mathf.Lerp(0.62f, 0.28f, t);
            DrawCircle(p, r, new Color(_color.R, _color.G, _color.B, a));
            DrawArc(p, r * 0.72f, 0f, Mathf.Tau, 18, new Color(1f, 1f, 1f, a * 0.48f), 1.1f * secondaryScale);
        }

        // Reverse-flow chevrons (pointing toward the tower) make "drag backward" immediately readable.
        int chevrons = _secondary ? 2 : 3;
        for (int i = 0; i < chevrons; i++)
        {
            float t = 0.25f + i * 0.22f;
            Vector2 c = source + span * t;
            float s = (4.4f - i * 0.35f) * secondaryScale;
            Vector2 a = c + (-dir + perp * 0.60f) * s;
            Vector2 b = c;
            Vector2 d = c + (-dir - perp * 0.60f) * s;
            DrawLine(a, b, new Color(_color.R, _color.G, _color.B, beamAlpha * 0.76f), 1.8f * secondaryScale);
            DrawLine(d, b, new Color(_color.R, _color.G, _color.B, beamAlpha * 0.76f), 1.8f * secondaryScale);
        }
    }
}
