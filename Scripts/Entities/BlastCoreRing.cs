using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived expanding ring drawn at the primary impact point when Blast Core fires.
/// Communicates: "this hit caused a small localized detonation."
///
/// Distinct from ChainArc (line between two points), DeathBurst (enemy death sparks),
/// and spectacle burst FX (which are part of the surge/combo system).
///
/// Caller sets GlobalPosition = Vector2.Zero, then calls Initialize().
/// The ring stores world-space origin and converts to local in _Draw.
/// </summary>
public partial class BlastCoreRing : Node2D
{
    private const float Duration  = 0.24f;
    private const float MinRadius = 14f;

    private float   _life;
    private Color   _color;
    private Vector2 _origin;
    private float   _power;      // 0..1, scales brightness and line weight
    private float   _maxRadius;  // mechanical blast radius -- ring expands to match actual damage area

    public void Initialize(Vector2 worldOrigin, Color color, float mechanicalRadius, float power = 0.5f)
    {
        _origin    = worldOrigin;
        _color     = color;
        _maxRadius = mechanicalRadius;
        _power     = Mathf.Clamp(power, 0f, 1f);
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
        float t = _life / Duration;

        // Quad ease-out expansion: fast initial pop, slows at edge.
        float expandT  = 1f - (1f - t) * (1f - t);
        float radius   = Mathf.Lerp(MinRadius, _maxRadius, expandT);

        // Alpha: sharp rise in first 12%, then linear fade.
        float alpha = t < 0.12f
            ? t / 0.12f
            : Mathf.Max(0f, 1f - (t - 0.12f) / 0.88f);

        Vector2 center = ToLocal(_origin);

        // Soft outer glow ring (wide, low alpha).
        var glowCol = new Color(_color.R, _color.G, _color.B, alpha * 0.22f);
        DrawArc(center, radius * 1.18f, 0f, Mathf.Tau, 32, glowCol, 9f, false);

        // Core ring: solid, narrow.
        float coreAlpha = alpha * (0.72f + _power * 0.22f);
        var coreCol = new Color(_color.R, _color.G, _color.B, coreAlpha);
        DrawArc(center, radius, 0f, Mathf.Tau, 32, coreCol, 2.0f, false);

        // Center detonation flash: tiny bright disc that fades in the first 20%.
        if (t < 0.20f)
        {
            float flashA = (1f - t / 0.20f) * (0.55f + _power * 0.20f);
            float flashR = 4f + _power * 5f;
            DrawCircle(center, flashR, new Color(1f, 0.95f, 0.75f, flashA));
        }
    }
}
