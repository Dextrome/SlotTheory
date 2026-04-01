using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived jagged arc line drawn between two world positions when a chain hit fires.
/// Caller sets GlobalPosition = Vector2.Zero, then calls Initialize().
/// </summary>
public partial class ChainArc : Node2D
{
    private const float BaseDuration = 0.20f;
    private const float MineDuration = 0.22f;

    private float _life;
    private float _duration = BaseDuration;
    private float _intensity = 1f;
    private bool _mineChainStyle;
    private Color _color;
    private Vector2 _from;
    private Vector2 _to;
    private Vector2[] _jitter = System.Array.Empty<Vector2>();

    public void Initialize(
        Vector2 worldFrom,
        Vector2 worldTo,
        Color color,
        float intensity = 1f,
        bool mineChainStyle = false,
        float lifetimeSec = 0f)   // 0 = use default per-style duration
    {
        _from = worldFrom;
        _to = worldTo;
        _color = color;
        _intensity = Mathf.Clamp(intensity, 0.85f, 2.20f);
        _mineChainStyle = mineChainStyle;
        float t = Mathf.Clamp((_intensity - 1f) / 1.2f, 0f, 1f);
        float defaultDuration = _mineChainStyle ? Mathf.Lerp(MineDuration, 0.30f, t) : BaseDuration;
        _duration = lifetimeSec > 0f ? lifetimeSec : defaultDuration;

        // Midpoints with random perpendicular jitter for electric zigzag look.
        var rng = new System.Random();
        var span = worldTo - worldFrom;
        var perp = new Vector2(-span.Y, span.X).Normalized();
        int midpointCount = _mineChainStyle ? 5 : 5;
        float jitterScale = (_mineChainStyle ? 0.31f : 0.30f) * (0.95f + (_intensity - 1f) * 0.30f);
        _jitter = new Vector2[midpointCount];

        for (int i = 0; i < midpointCount; i++)
        {
            float ptT = (i + 1) / (float)(midpointCount + 1);
            float offset = (float)(rng.NextDouble() - 0.5) * span.Length() * jitterScale;
            _jitter[i] = worldFrom + span * ptT + perp * offset;
        }
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
        float alpha = 1f - _life / _duration;
        float width = (alpha * 2f + 0.55f) * (0.85f + _intensity * 0.55f);
        float glowWidth = width * (_mineChainStyle ? 2.05f : 1.95f);
        var coreCol = new Color(_color.R, _color.G, _color.B,
            alpha * (_mineChainStyle ? 0.93f : 0.94f));
        var glowCol = new Color(_color.R, _color.G, _color.B,
            alpha * (_mineChainStyle ? 0.38f : 0.36f));

        // Jagged chain segments: from -> jitter points -> to
        var pts = new Vector2[_jitter.Length + 2];
        pts[0] = ToLocal(_from);
        for (int i = 0; i < _jitter.Length; i++)
            pts[i + 1] = ToLocal(_jitter[i]);
        pts[pts.Length - 1] = ToLocal(_to);

        for (int i = 0; i < pts.Length - 1; i++)
        {
            DrawLine(pts[i], pts[i + 1], glowCol, glowWidth);
            DrawLine(pts[i], pts[i + 1], coreCol, width);
        }

        // Endpoint glows.
        float endR = _mineChainStyle ? 6.2f : 5.8f;
        float endGlowA = _mineChainStyle ? 0.40f : 0.37f;
        DrawCircle(pts[0], endR, new Color(_color.R, _color.G, _color.B, alpha * endGlowA));
        DrawCircle(pts[pts.Length - 1], endR, new Color(_color.R, _color.G, _color.B, alpha * endGlowA));
        DrawCircle(pts[pts.Length - 1], _mineChainStyle ? 3.0f : 2.8f,
            new Color(1f, 1f, 1f, alpha * (_mineChainStyle ? 0.78f : 0.76f)));
    }
}
