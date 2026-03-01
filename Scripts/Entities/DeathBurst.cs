using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived expanding ring + radial sparks spawned when an enemy dies.
/// Caller sets GlobalPosition after AddChild, then calls Initialize().
/// </summary>
public partial class DeathBurst : Node2D
{
    private const float Duration = 0.35f;

    private float _life;
    private Color _color;
    private float _scale;

    public void Initialize(Color color, float scale = 1f)
    {
        _color = color;
        _scale = scale;
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= Duration) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t     = _life / Duration;
        float easeT = 1f - (1f - t) * (1f - t);   // ease-out

        // Brief white flash at centre
        if (t < 0.18f)
        {
            float fa = (0.18f - t) / 0.18f;
            DrawCircle(Vector2.Zero, _scale * 10f * (1f - t / 0.18f),
                new Color(1f, 1f, 1f, fa * 0.55f));
        }

        // Expanding ring
        float ringR = _scale * 24f * easeT;
        DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 32,
            new Color(_color.R, _color.G, _color.B, (1f - t) * 0.85f), 2.5f);

        // 8 radial spark lines
        for (int i = 0; i < 8; i++)
        {
            float a     = i * Mathf.Tau / 8f;
            float inner = _scale * 6f  * easeT;
            float outer = _scale * 20f * easeT;
            float sa    = (1f - t) * (1f - t);
            var   dir   = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(dir * inner, dir * outer,
                new Color(_color.R, _color.G, _color.B, sa), 1.5f);
        }
    }
}
