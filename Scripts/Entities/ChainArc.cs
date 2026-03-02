using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived jagged arc line drawn between two world positions when a chain hit fires.
/// Caller sets GlobalPosition = Vector2.Zero, then calls Initialize().
/// </summary>
public partial class ChainArc : Node2D
{
    private const float Duration = 0.18f;

    private float    _life;
    private Color    _color;
    private Vector2  _from;
    private Vector2  _to;
    private Vector2[] _jitter = System.Array.Empty<Vector2>();

    public void Initialize(Vector2 worldFrom, Vector2 worldTo, Color color)
    {
        _from  = worldFrom;
        _to    = worldTo;
        _color = color;

        // 4 midpoints with random perpendicular jitter for electric zigzag look
        var rng  = new System.Random();
        var span = worldTo - worldFrom;
        var perp = new Vector2(-span.Y, span.X).Normalized();
        _jitter = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float t      = (i + 1) / 5f;
            float offset = (float)(rng.NextDouble() - 0.5) * span.Length() * 0.28f;
            _jitter[i]   = worldFrom + span * t + perp * offset;
        }
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= Duration) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float alpha = 1f - _life / Duration;
        float width = alpha * 2f + 0.5f;
        var   col   = new Color(_color.R, _color.G, _color.B, alpha * 0.90f);

        // Jagged chain segments: from → 4 jitter points → to
        var pts = new Vector2[6];
        pts[0] = ToLocal(_from);
        pts[1] = ToLocal(_jitter[0]);
        pts[2] = ToLocal(_jitter[1]);
        pts[3] = ToLocal(_jitter[2]);
        pts[4] = ToLocal(_jitter[3]);
        pts[5] = ToLocal(_to);

        for (int i = 0; i < pts.Length - 1; i++)
            DrawLine(pts[i], pts[i + 1], col, width);

        // Glow bloom at each endpoint
        DrawCircle(pts[0], 5f, new Color(_color.R, _color.G, _color.B, alpha * 0.35f));
        DrawCircle(pts[5], 5f, new Color(_color.R, _color.G, _color.B, alpha * 0.35f));
        DrawCircle(pts[5], 2.5f, new Color(1f, 1f, 1f, alpha * 0.70f));
    }
}
