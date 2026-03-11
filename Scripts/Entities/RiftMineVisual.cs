using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Lightweight mine marker for Rift Sapper traps.
/// </summary>
public partial class RiftMineVisual : Node2D
{
    private const float ChainFlashDuration = 0.20f;
    private const float MiniVisualScale = 0.80f;

    private Color _accent = new Color(0.60f, 1.00f, 0.56f);
    private float _damageScale = 1f;
    private bool _isMiniMine;
    private bool _armed;
    private int _chargesRemaining = 1;
    private int _chargesMax = 1;
    private float _pulseT;
    private float _chainFlash;
    private float _chainFlashStrength = 1f;

    public void Initialize(Color accent, float damageScale, bool isMiniMine = false)
    {
        _accent = accent;
        _damageScale = Mathf.Clamp(damageScale, 0.25f, 1.5f);
        _isMiniMine = isMiniMine;
        SetProcess(true);
        QueueRedraw();
    }

    public void SetArmed(bool armed)
    {
        if (_armed == armed) return;
        _armed = armed;
        QueueRedraw();
    }

    public void SetCharges(int remaining, int max)
    {
        int clampedMax = Mathf.Max(1, max);
        int clampedRemaining = remaining;
        if (clampedRemaining < 0) clampedRemaining = 0;
        if (clampedRemaining > clampedMax) clampedRemaining = clampedMax;
        if (_chargesRemaining == clampedRemaining && _chargesMax == clampedMax)
            return;
        _chargesRemaining = clampedRemaining;
        _chargesMax = clampedMax;
        QueueRedraw();
    }

    public void TriggerChainFlash(float strength = 1f)
    {
        _chainFlash = ChainFlashDuration;
        _chainFlashStrength = Mathf.Clamp(strength, 0.75f, 1.45f);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _pulseT += dt;
        if (_chainFlash > 0f)
            _chainFlash = Mathf.Max(0f, _chainFlash - dt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 0.72f + 0.28f * Mathf.Sin(_pulseT * 6.0f);
        float armAlpha = _armed ? 1f : 0.44f;
        float mini = Mathf.Lerp(0.72f, 1f, _damageScale);
        if (_isMiniMine)
            mini *= MiniVisualScale;

        var glow = new Color(_accent.R, _accent.G, _accent.B, (0.12f + 0.08f * pulse) * armAlpha);
        DrawCircle(Vector2.Zero, 13f * mini, glow);
        DrawCircle(Vector2.Zero, 8.5f * mini, new Color(_accent.R, _accent.G, _accent.B, (0.20f + 0.10f * pulse) * armAlpha));

        var shell = new Color(_accent.R, _accent.G, _accent.B, (0.84f + 0.12f * pulse) * armAlpha);
        var core = new Color(0.08f, 0.20f, 0.10f, 0.95f);
        DrawPolygon(RegularPoly(6, 6.8f * mini, Mathf.Pi / 6f), new[] { shell });
        DrawPolygon(RegularPoly(6, 5.1f * mini, Mathf.Pi / 6f), new[] { core });

        // Arming indicator.
        float ringAlpha = _armed ? 0.92f : 0.40f;
        DrawArc(Vector2.Zero, 9.5f * mini, 0f, Mathf.Tau, 28, new Color(0.84f, 1.0f, 0.80f, ringAlpha), 1.3f);

        DrawChargeSegments(mini, armAlpha, pulse);

        // Crosshair center.
        var cross = new Color(0.90f, 1.0f, 0.84f, 0.80f * armAlpha);
        DrawLine(new Vector2(-2.8f, 0f), new Vector2(2.8f, 0f), cross, 1.2f);
        DrawLine(new Vector2(0f, -2.8f), new Vector2(0f, 2.8f), cross, 1.2f);

        if (_chainFlash > 0f)
        {
            float t = _chainFlash / ChainFlashDuration;
            float boost = _chainFlashStrength;
            DrawCircle(Vector2.Zero, 14.5f * (1f + (1f - t) * 0.48f) * boost,
                new Color(0.92f, 1.00f, 0.84f, 0.24f * t));
            DrawArc(Vector2.Zero, 9.2f * (1f + (1f - t) * 0.40f) * boost, 0f, Mathf.Tau, 24,
                new Color(0.86f, 1.00f, 0.80f, 0.30f * t), 1.4f);
            DrawArc(Vector2.Zero, 12.7f * (1f + (1f - t) * 0.28f) * boost, 0f, Mathf.Tau, 28,
                new Color(0.96f, 1.00f, 0.88f, 0.20f * t), 1.0f);
        }
    }

    private static Vector2[] RegularPoly(int sides, float radius, float angleOffset)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = angleOffset + i * Mathf.Tau / sides;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }

    private void DrawChargeSegments(float mini, float armAlpha, float pulse)
    {
        if (_chargesMax <= 0) return;

        float radius = 12.2f * mini;
        float width = 1.6f;
        float gap = 0.14f;
        float seg = (Mathf.Tau / _chargesMax) - gap;
        float startOffset = -Mathf.Pi * 0.5f;

        for (int i = 0; i < _chargesMax; i++)
        {
            float a0 = startOffset + i * (seg + gap);
            float a1 = a0 + seg;
            bool active = i < _chargesRemaining;
            float alpha = active ? (0.78f + 0.14f * pulse) * armAlpha : 0.20f * armAlpha;
            var col = new Color(0.86f, 1.00f, 0.86f, alpha);
            DrawArc(Vector2.Zero, radius, a0, a1, 12, col, width);
        }

        // Make the final charge state pop visually.
        if (_chargesRemaining == 1)
        {
            float glowAlpha = (0.30f + 0.16f * pulse) * armAlpha;
            DrawArc(Vector2.Zero, radius + 1.2f, 0f, Mathf.Tau, 28,
                new Color(1.00f, 0.96f, 0.72f, glowAlpha), 1.2f);
        }
    }
}
