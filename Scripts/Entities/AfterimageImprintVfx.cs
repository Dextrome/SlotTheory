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
    private bool _triggered;
    private float _triggerElapsed;
    private float _phase;

    public void Initialize(float delaySeconds, float radius, Color color, string towerId)
    {
        _delayTotal = Mathf.Max(0.05f, delaySeconds);
        _delayRemaining = _delayTotal;
        _radius = Mathf.Max(20f, radius);
        _color = color;
        _towerId = towerId ?? string.Empty;
        _triggered = false;
        _triggerElapsed = 0f;
        _phase = 0f;
        ZIndex = 8;
        QueueRedraw();
    }

    public void Reset(Vector2 worldPos, float delaySeconds, float radius, Color color)
    {
        GlobalPosition = worldPos;
        _delayTotal = Mathf.Max(0.05f, delaySeconds);
        _delayRemaining = _delayTotal;
        _radius = Mathf.Max(20f, radius);
        _color = color;
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
            return;
        }

        float prep = _delayTotal > 0.001f ? 1f - (_delayRemaining / _delayTotal) : 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_phase * 7.4f);
        float alpha = 0.26f + pulse * 0.12f + prep * 0.16f;

        float baseRadius = _radius * (0.16f + prep * 0.14f);
        DrawCircle(Vector2.Zero, baseRadius,
            new Color(_color.R, _color.G, _color.B, alpha * 0.16f));
        DrawArc(Vector2.Zero, baseRadius * 1.45f, 0f, Mathf.Tau, 36,
            new Color(_color.R, _color.G, _color.B, alpha * 0.64f), 1.6f);

        float glyphScale = baseRadius * 0.55f;
        switch (_towerId)
        {
            case "chain_tower":
                DrawArc(new Vector2(-glyphScale * 0.48f, 0f), glyphScale * 0.42f, 0f, Mathf.Tau, 20,
                    new Color(_color.R, _color.G, _color.B, alpha * 0.70f), 1.4f);
                DrawArc(new Vector2(glyphScale * 0.48f, 0f), glyphScale * 0.42f, 0f, Mathf.Tau, 20,
                    new Color(_color.R, _color.G, _color.B, alpha * 0.70f), 1.4f);
                break;
            case "heavy_cannon":
            case "rocket_launcher":
                DrawLine(new Vector2(-glyphScale, 0f), new Vector2(glyphScale, 0f),
                    new Color(_color.R, _color.G, _color.B, alpha * 0.74f), 1.6f);
                DrawLine(new Vector2(0f, -glyphScale), new Vector2(0f, glyphScale),
                    new Color(_color.R, _color.G, _color.B, alpha * 0.74f), 1.6f);
                break;
            default:
                DrawArc(Vector2.Zero, glyphScale * 0.56f, 0f, Mathf.Tau, 22,
                    new Color(_color.R, _color.G, _color.B, alpha * 0.74f), 1.5f);
                break;
        }
    }
}
