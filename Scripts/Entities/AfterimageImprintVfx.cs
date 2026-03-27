using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived spectral imprint marker for the Afterimage modifier.
/// Shows anticipation during delay, then plays a single burst when triggered.
/// </summary>
public partial class AfterimageImprintVfx : Node2D
{
    private const float TriggerDuration = 0.24f;

    private float _delayTotal = 0.8f;
    private float _delayRemaining = 0.8f;
    private float _radius = 80f;
    private Color _color = new(0.72f, 0.88f, 1.00f);
    private string _towerId = string.Empty;
    private int _copies = 1;
    private bool _triggered;
    private float _triggerElapsed;
    private float _phase;

    public void Initialize(float delaySeconds, float radius, Color color, string towerId, int copies)
    {
        _delayTotal = Mathf.Max(0.05f, delaySeconds);
        _delayRemaining = _delayTotal;
        _radius = Mathf.Max(20f, radius);
        _color = color;
        _towerId = towerId ?? string.Empty;
        _copies = Mathf.Clamp(copies, 1, 3);
        _triggered = false;
        _triggerElapsed = 0f;
        _phase = 0f;
        ZIndex = 8;
        QueueRedraw();
    }

    public void Reset(Vector2 worldPos, float delaySeconds, float radius, Color color, string towerId, int copies)
    {
        GlobalPosition = worldPos;
        _delayTotal = Mathf.Max(0.05f, delaySeconds);
        _delayRemaining = _delayTotal;
        _radius = Mathf.Max(20f, radius);
        _color = color;
        _towerId = towerId ?? _towerId;
        _copies = Mathf.Clamp(copies, 1, 3);
        _triggered = false;
        _triggerElapsed = 0f;
        QueueRedraw();
    }

    public void SetDelayRemaining(float delayRemaining, float delayTotal)
    {
        _delayTotal = Mathf.Max(0.05f, delayTotal);
        _delayRemaining = Mathf.Clamp(delayRemaining, 0f, _delayTotal);
        QueueRedraw();
    }

    public void TriggerAndFree()
    {
        _triggered = true;
        _triggerElapsed = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _phase += dt;

        if (_triggered)
        {
            _triggerElapsed += dt;
            if (_triggerElapsed >= TriggerDuration)
            {
                QueueFree();
                return;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_triggered)
        {
            float t = Mathf.Clamp(_triggerElapsed / TriggerDuration, 0f, 1f);
            float inv = 1f - t;
            float ringRadius = Mathf.Lerp(_radius * 0.18f, _radius * 0.62f, t);
            DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 40,
                new Color(_color.R, _color.G, _color.B, inv * 0.65f), 2.2f);
            DrawCircle(Vector2.Zero, ringRadius * 0.42f,
                new Color(1f, 1f, 1f, inv * 0.34f));
            DrawTowerGlyph(ringRadius * 0.46f, inv * 0.70f, triggerMode: true);
            return;
        }

        float prep = _delayTotal > 0.001f ? 1f - (_delayRemaining / _delayTotal) : 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_phase * 7.4f);
        float alpha = 0.26f + pulse * 0.12f + prep * 0.16f;

        float baseRadius = _radius * (0.16f + prep * 0.14f);
        DrawCircle(Vector2.Zero, baseRadius,
            new Color(_color.R, _color.G, _color.B, alpha * 0.16f));
        float prepRingRadius = baseRadius * 1.45f;
        DrawArc(Vector2.Zero, prepRingRadius, 0f, Mathf.Tau, 36,
            new Color(_color.R, _color.G, _color.B, alpha * 0.64f), 1.6f);

        // Copy count readability: add brighter pips around the ring.
        DrawCopyPips(prepRingRadius, alpha);
        DrawTowerGlyph(baseRadius * 0.55f, alpha * 0.88f, triggerMode: false);
    }

    private void DrawCopyPips(float ringRadius, float alpha)
    {
        int pips = Mathf.Clamp(_copies, 1, 3);
        float pipRadius = Mathf.Max(1.3f, _radius * 0.022f);
        float orbit = ringRadius * 1.02f;
        float start = -Mathf.Pi * 0.5f - (pips - 1) * 0.26f;
        for (int i = 0; i < pips; i++)
        {
            float a = start + i * 0.52f;
            Vector2 pos = new(Mathf.Cos(a) * orbit, Mathf.Sin(a) * orbit);
            DrawCircle(pos, pipRadius, new Color(_color.R, _color.G, _color.B, alpha * 0.90f));
            DrawCircle(pos, pipRadius * 0.56f, new Color(1f, 1f, 1f, alpha * 0.55f));
        }
    }

    private void DrawTowerGlyph(float glyphScale, float alpha, bool triggerMode)
    {
        Color line = new(_color.R, _color.G, _color.B, alpha);
        float width = triggerMode ? 2.0f : 1.6f;
        switch (_towerId)
        {
            case "rapid_shooter":
                DrawLine(new Vector2(-glyphScale * 0.62f, -glyphScale * 0.30f), new Vector2(glyphScale * 0.46f, 0f), line, width);
                DrawLine(new Vector2(-glyphScale * 0.62f, glyphScale * 0.30f), new Vector2(glyphScale * 0.46f, 0f), line, width);
                break;
            case "marker_tower":
                DrawArc(Vector2.Zero, glyphScale * 0.62f, 0f, Mathf.Tau, 28, line, width);
                DrawLine(new Vector2(-glyphScale, 0f), new Vector2(glyphScale, 0f), line, width);
                DrawLine(new Vector2(0f, -glyphScale), new Vector2(0f, glyphScale), line, width);
                break;
            case "chain_tower":
                DrawArc(new Vector2(-glyphScale * 0.48f, 0f), glyphScale * 0.42f, 0f, Mathf.Tau, 20,
                    line, width);
                DrawArc(new Vector2(glyphScale * 0.48f, 0f), glyphScale * 0.42f, 0f, Mathf.Tau, 20,
                    line, width);
                DrawLine(new Vector2(-glyphScale * 0.08f, 0f), new Vector2(glyphScale * 0.08f, 0f), line, width);
                break;
            case "heavy_cannon":
                DrawLine(new Vector2(-glyphScale, 0f), new Vector2(glyphScale, 0f), line, width);
                DrawLine(new Vector2(0f, -glyphScale), new Vector2(0f, glyphScale), line, width);
                DrawCircle(Vector2.Zero, glyphScale * 0.18f, new Color(1f, 1f, 1f, alpha * 0.55f));
                break;
            case "rocket_launcher":
                DrawLine(new Vector2(-glyphScale * 0.70f, glyphScale * 0.52f), new Vector2(glyphScale * 0.62f, -glyphScale * 0.52f), line, width);
                DrawLine(new Vector2(glyphScale * 0.22f, -glyphScale * 0.16f), new Vector2(glyphScale * 0.62f, -glyphScale * 0.52f), line, width);
                break;
            case "rift_prism":
                Vector2 a = new(0f, -glyphScale);
                Vector2 b = new(glyphScale * 0.86f, glyphScale * 0.48f);
                Vector2 c = new(-glyphScale * 0.86f, glyphScale * 0.48f);
                DrawLine(a, b, line, width);
                DrawLine(b, c, line, width);
                DrawLine(c, a, line, width);
                break;
            case "phase_splitter":
                DrawCircle(new Vector2(-glyphScale * 0.58f, 0f), glyphScale * 0.22f, new Color(_color.R, _color.G, _color.B, alpha * 0.70f));
                DrawCircle(new Vector2(glyphScale * 0.58f, 0f), glyphScale * 0.22f, new Color(_color.R, _color.G, _color.B, alpha * 0.70f));
                DrawLine(new Vector2(-glyphScale * 0.36f, 0f), new Vector2(glyphScale * 0.36f, 0f), line, width);
                break;
            case "undertow_engine":
                DrawArc(Vector2.Zero, glyphScale * 0.72f, -Mathf.Pi * 0.10f, Mathf.Pi * 1.10f, 24, line, width);
                DrawArc(Vector2.Zero, glyphScale * 0.40f, Mathf.Pi * 0.20f, Mathf.Pi * 1.36f, 20, line, width);
                break;
            case "accordion_engine":
                DrawLine(new Vector2(-glyphScale * 0.56f, -glyphScale * 0.72f), new Vector2(-glyphScale * 0.56f, glyphScale * 0.72f), line, width);
                DrawLine(new Vector2(0f, -glyphScale * 0.90f), new Vector2(0f, glyphScale * 0.90f), line, width);
                DrawLine(new Vector2(glyphScale * 0.56f, -glyphScale * 0.72f), new Vector2(glyphScale * 0.56f, glyphScale * 0.72f), line, width);
                break;
            default:
                DrawArc(Vector2.Zero, glyphScale * 0.56f, 0f, Mathf.Tau, 22,
                    line, width);
                break;
        }
    }
}
