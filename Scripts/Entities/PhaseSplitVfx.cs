using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// One-shot VFX for Phase Splitter: single emission from the tower that forks
/// toward front/back targets with linked impact pulses.
/// </summary>
public partial class PhaseSplitVfx : Node2D
{
    private Vector2 _source;
    private Vector2? _front;
    private Vector2? _back;
    private Color _color = Colors.White;
    private float _life = 0.16f;
    private float _maxLife = 0.16f;

    public void Initialize(Vector2 source, Vector2? frontTarget, Vector2? backTarget, Color color)
    {
        _source = source;
        _front = frontTarget;
        _back = backTarget;
        _color = color;
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        if (_life <= 0f)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(_life / Mathf.Max(0.0001f, _maxLife), 0f, 1f);
        float fade = t * t;
        Vector2 sourceLocal = ToLocal(_source);

        DrawCircle(sourceLocal, 14f * (1f - t) + 4f, new Color(_color.R, _color.G, _color.B, 0.26f * fade));
        DrawCircle(sourceLocal, 3.2f + (1f - t) * 1.8f, new Color(1f, 1f, 1f, 0.78f * fade));

        if (_front.HasValue)
            DrawSplitArm(sourceLocal, ToLocal(_front.Value), fade, emphasize: true);
        if (_back.HasValue)
            DrawSplitArm(sourceLocal, ToLocal(_back.Value), fade, emphasize: false);

        if (_front.HasValue && _back.HasValue)
        {
            Vector2 a = ToLocal(_front.Value);
            Vector2 b = ToLocal(_back.Value);
            DrawLine(a, b, new Color(_color.R, _color.G, _color.B, 0.08f * fade), 1.2f);
        }
    }

    private void DrawSplitArm(Vector2 from, Vector2 to, float fade, bool emphasize)
    {
        float width = emphasize ? 3.0f : 2.6f;
        DrawLine(from, to, new Color(_color.R, _color.G, _color.B, 0.72f * fade), width);
        DrawLine(from, to, new Color(1f, 1f, 1f, 0.40f * fade), 1.2f);

        float ringR = emphasize ? 9.5f : 8.5f;
        DrawCircle(to, ringR, new Color(_color.R, _color.G, _color.B, 0.14f * fade));
        DrawArc(to, ringR * 0.82f, 0f, Mathf.Tau, 22, new Color(1f, 1f, 1f, 0.42f * fade), 1.3f);
    }
}
