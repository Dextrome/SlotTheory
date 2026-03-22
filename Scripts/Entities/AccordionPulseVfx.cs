using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Contracting ring visual that plays at the Accordion Engine's position when it fires.
/// Outer ring contracts inward to reinforce the compression-pull metaphor.
/// Lifetime: ~0.40s; auto-frees when complete.
/// </summary>
public partial class AccordionPulseVfx : Node2D
{
    private const float Duration = 0.40f;

    private Color _color;
    private float _maxRadius;
    private int   _enemyCount;
    private float _life;

    public void Initialize(Color color, float maxRadius, int enemyCount)
    {
        _color      = color;
        _maxRadius  = Mathf.Max(32f, maxRadius);
        _enemyCount = enemyCount;
        _life       = 0f;
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _life / Duration;                   // 0 → 1 over lifetime

        // Outer contracting ring: starts at maxRadius, collapses to 0
        float outerRadius = _maxRadius * (1f - t);
        float outerAlpha  = (1f - t) * 0.70f;
        if (outerRadius > 1f)
            DrawArc(Vector2.Zero, outerRadius, 0f, Mathf.Tau, 48,
                new Color(_color.R, _color.G, _color.B, outerAlpha), 2.8f);

        // Eight inward rib lines emanating from max radius toward center
        float ribLen  = outerRadius * 0.45f;
        float ribAlpha = (1f - t) * 0.38f;
        for (int i = 0; i < 8; i++)
        {
            float a   = i * Mathf.Tau / 8f;
            var   dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(dir * outerRadius, dir * (outerRadius - ribLen),
                new Color(_color.R, _color.G, _color.B, ribAlpha), 1.4f);
        }

        // Central bloom: brief bright flash in first 30% of lifetime
        float bloomT = Mathf.Clamp(1f - t / 0.30f, 0f, 1f);
        if (bloomT > 0.01f)
        {
            DrawCircle(Vector2.Zero, 16f * bloomT,
                new Color(_color.R, _color.G, _color.B, bloomT * 0.45f));
            DrawCircle(Vector2.Zero, 6f * bloomT,
                new Color(1f, 0.9f, 1f, bloomT * 0.70f));
        }

        // Small expanding inner ripple that fades in after 0.25s
        float rippleStart = 0.25f / Duration;
        if (t > rippleStart)
        {
            float rt = (t - rippleStart) / (1f - rippleStart);
            float rippleRadius = rt * _maxRadius * 0.40f;
            float rippleAlpha  = (1f - rt) * 0.32f;
            DrawArc(Vector2.Zero, rippleRadius, 0f, Mathf.Tau, 32,
                new Color(_color.R, _color.G, _color.B, rippleAlpha), 1.6f);
        }
    }
}
