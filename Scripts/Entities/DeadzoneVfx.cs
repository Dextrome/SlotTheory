using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived spatial trap marker for the Deadzone modifier.
///
/// Visual identity: cracked pressure ring / impact scar -- a latent fault in the lane.
/// Distinct from Afterimage (soft ghost-blue replay) and Wildfire (persistent fire hazard).
///
/// Lifecycle states:
///   Active:  cracked hazard ring pulses, radiating fault lines. Reads as "primed, dangerous."
///   Trigger: implosive inward collapse burst -- sharp, compact, communicates "enemy crossed it."
///   Expire:  fast fade-out snap when lifetime runs out without a trigger.
/// </summary>
public partial class DeadzoneVfx : Node2D
{
    private const float TriggerDuration = 0.22f;
    private const float ExpireDuration  = 0.18f;

    private float _lifetimeTotal = SlotTheory.Core.Balance.DeadzoneLifetime;
    private float _lifetimeRemaining = SlotTheory.Core.Balance.DeadzoneLifetime;
    private float _triggerRadius = SlotTheory.Core.Balance.DeadzoneTriggerRadius;
    private Color _color = new(1.00f, 0.55f, 0.14f);
    private string _towerId = string.Empty;

    private bool _triggered;
    private bool _expiring;
    private float _stateElapsed;
    private float _phase;

    public void Initialize(float lifetimeSeconds, float triggerRadius, Color color, string towerId)
    {
        _lifetimeTotal     = Mathf.Max(0.1f, lifetimeSeconds);
        _lifetimeRemaining = _lifetimeTotal;
        _triggerRadius     = Mathf.Max(10f, triggerRadius);
        _color             = color;
        _towerId           = towerId ?? string.Empty;
        _triggered         = false;
        _expiring          = false;
        _stateElapsed      = 0f;
        _phase             = 0f;
        ZIndex             = 7;
        QueueRedraw();
    }

    public void Reset(Vector2 worldPos, float lifetimeSeconds, float triggerRadius, Color color, string towerId)
    {
        GlobalPosition     = worldPos;
        _lifetimeTotal     = Mathf.Max(0.1f, lifetimeSeconds);
        _lifetimeRemaining = _lifetimeTotal;
        _triggerRadius     = Mathf.Max(10f, triggerRadius);
        _color             = color;
        _towerId           = towerId ?? _towerId;
        _triggered         = false;
        _expiring          = false;
        _stateElapsed      = 0f;
        QueueRedraw();
    }

    public void SetLifetimeRemaining(float remaining, float total)
    {
        _lifetimeTotal     = Mathf.Max(0.01f, total);
        _lifetimeRemaining = Mathf.Clamp(remaining, 0f, _lifetimeTotal);
        QueueRedraw();
    }

    /// <summary>Enemy crossed the zone -- play implosive collapse burst, then free.</summary>
    public void TriggerAndFree()
    {
        _triggered    = true;
        _expiring     = false;
        _stateElapsed = 0f;
        QueueRedraw();
    }

    /// <summary>Lifetime expired without a trigger -- fade out, then free.</summary>
    public void ExpireAndFree()
    {
        _expiring     = true;
        _triggered    = false;
        _stateElapsed = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _phase += dt;

        if (_triggered)
        {
            _stateElapsed += dt;
            if (_stateElapsed >= TriggerDuration)
            {
                QueueFree();
                return;
            }
        }
        else if (_expiring)
        {
            _stateElapsed += dt;
            if (_stateElapsed >= ExpireDuration)
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
            DrawTriggerBurst();
            return;
        }
        if (_expiring)
        {
            DrawExpireFade();
            return;
        }
        DrawActiveScar();
    }

    // ── Active state: cracked hazard ring + fault lines ────────────────────

    private void DrawActiveScar()
    {
        float lifeRatio  = _lifetimeTotal > 0.001f ? _lifetimeRemaining / _lifetimeTotal : 1f;
        float urgency    = 1f - lifeRatio; // increases toward expiry
        float pulse      = 0.5f + 0.5f * Mathf.Sin(_phase * 9.2f);
        float alpha      = Mathf.Lerp(0.55f, 0.35f, lifeRatio) + pulse * 0.08f;
        float ringR      = _triggerRadius * 0.88f;

        // Faint hazard fill at center.
        DrawCircle(Vector2.Zero, ringR * 0.44f,
            new Color(_color.R, _color.G, _color.B, alpha * 0.10f));

        // Main cracked ring -- drawn as broken arc segments for angular identity.
        int segments = 6;
        float gapAngle = 0.26f + urgency * 0.08f;
        float segAngle = (Mathf.Tau - segments * gapAngle) / segments;
        float rotOffset = _phase * 0.35f; // slow drift
        for (int s = 0; s < segments; s++)
        {
            float startA = rotOffset + s * (segAngle + gapAngle);
            DrawArc(Vector2.Zero, ringR, startA, startA + segAngle, 18,
                new Color(_color.R, _color.G, _color.B, alpha), 2.0f);
        }

        // Fault line cross-hatch: 4 radial cracks emanating from center.
        float crackAlpha = alpha * 0.55f;
        Color crackColor = new(_color.R, _color.G, _color.B, crackAlpha);
        float crackLen = ringR * 0.70f;
        float crackRot = rotOffset * 0.5f;
        for (int k = 0; k < 4; k++)
        {
            float angle = crackRot + k * (Mathf.Pi * 0.5f);
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            Vector2 inner = new(cos * ringR * 0.18f, sin * ringR * 0.18f);
            Vector2 outer = new(cos * crackLen, sin * crackLen);
            DrawLine(inner, outer, crackColor, 1.3f);
        }

        // Tower identity glyph at center.
        DrawDeadzoneGlyph(ringR * 0.38f, alpha * 0.80f);

        // Hazard pip markers at cardinal points on the ring.
        float pipR = Mathf.Max(1.4f, _triggerRadius * 0.025f);
        for (int p = 0; p < 4; p++)
        {
            float a = crackRot + p * Mathf.Pi * 0.5f + Mathf.Pi * 0.25f;
            Vector2 pos = new(Mathf.Cos(a) * ringR * 1.04f, Mathf.Sin(a) * ringR * 1.04f);
            DrawCircle(pos, pipR, new Color(_color.R, _color.G, _color.B, alpha * 0.90f));
            DrawCircle(pos, pipR * 0.50f, new Color(1f, 1f, 1f, alpha * 0.45f));
        }
    }

    // ── Trigger state: implosive inward collapse ────────────────────────────

    private void DrawTriggerBurst()
    {
        float t      = Mathf.Clamp(_stateElapsed / TriggerDuration, 0f, 1f);
        float inv    = 1f - t;
        // Ring implodes from outer radius inward to nothing.
        float ringR  = _triggerRadius * 0.88f * Mathf.Lerp(1f, 0.05f, t * t);
        float bright = Mathf.Lerp(1.0f, 0.6f, t);

        // Collapsing ring.
        DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 48,
            new Color(_color.R * bright, _color.G * bright * 0.85f, _color.B * bright * 0.55f, inv * 0.90f),
            2.4f);

        // Flash disc at center.
        DrawCircle(Vector2.Zero, ringR * 0.55f,
            new Color(1f, Mathf.Lerp(0.90f, 0.55f, t), Mathf.Lerp(0.70f, 0.20f, t), inv * 0.60f));

        // 6 short burst sparks radiating outward (counter to the implosion -- inertia).
        int sparks = 6;
        for (int s = 0; s < sparks; s++)
        {
            float angle = s * Mathf.Tau / sparks + _phase;
            float sparkLen = _triggerRadius * 0.55f * inv;
            float startR = ringR * 1.1f;
            Vector2 origin = new(Mathf.Cos(angle) * startR, Mathf.Sin(angle) * startR);
            Vector2 tip    = new(Mathf.Cos(angle) * (startR + sparkLen), Mathf.Sin(angle) * (startR + sparkLen));
            DrawLine(origin, tip,
                new Color(_color.R, _color.G * 0.75f, 0f, inv * 0.80f), 1.5f);
        }
    }

    // ── Expire state: fast fade snap ────────────────────────────────────────

    private void DrawExpireFade()
    {
        float t   = Mathf.Clamp(_stateElapsed / ExpireDuration, 0f, 1f);
        float inv = 1f - t;
        float ringR = _triggerRadius * 0.88f;

        DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 36,
            new Color(_color.R, _color.G, _color.B, inv * 0.40f), 1.6f);
        DrawCircle(Vector2.Zero, ringR * 0.40f,
            new Color(_color.R, _color.G, _color.B, inv * 0.06f));
    }

    // ── Tower identity glyph (matching AfterimageImprintVfx glyph style) ───

    private void DrawDeadzoneGlyph(float scale, float alpha)
    {
        Color line = new(_color.R, _color.G, _color.B, alpha);
        const float w = 1.5f;
        switch (_towerId)
        {
            case "rapid_shooter":
                DrawLine(new Vector2(-scale * 0.62f, -scale * 0.30f), new Vector2(scale * 0.46f, 0f), line, w);
                DrawLine(new Vector2(-scale * 0.62f,  scale * 0.30f), new Vector2(scale * 0.46f, 0f), line, w);
                break;
            case "marker_tower":
                DrawArc(Vector2.Zero, scale * 0.62f, 0f, Mathf.Tau, 22, line, w);
                DrawLine(new Vector2(-scale, 0f), new Vector2(scale, 0f), line, w);
                DrawLine(new Vector2(0f, -scale), new Vector2(0f, scale), line, w);
                break;
            case "chain_tower":
                DrawArc(new Vector2(-scale * 0.48f, 0f), scale * 0.38f, 0f, Mathf.Tau, 16, line, w);
                DrawArc(new Vector2( scale * 0.48f, 0f), scale * 0.38f, 0f, Mathf.Tau, 16, line, w);
                DrawLine(new Vector2(-scale * 0.08f, 0f), new Vector2(scale * 0.08f, 0f), line, w);
                break;
            case "heavy_cannon":
                DrawLine(new Vector2(-scale, 0f), new Vector2(scale, 0f), line, w);
                DrawLine(new Vector2(0f, -scale), new Vector2(0f, scale), line, w);
                DrawCircle(Vector2.Zero, scale * 0.18f, new Color(1f, 1f, 1f, alpha * 0.50f));
                break;
            case "rocket_launcher":
                DrawLine(new Vector2(-scale * 0.70f, scale * 0.50f), new Vector2(scale * 0.60f, -scale * 0.50f), line, w);
                DrawLine(new Vector2(scale * 0.20f, -scale * 0.18f), new Vector2(scale * 0.60f, -scale * 0.50f), line, w);
                break;
            case "rift_prism":
            {
                Vector2 a = new(0f, -scale);
                Vector2 b = new( scale * 0.86f,  scale * 0.48f);
                Vector2 c = new(-scale * 0.86f,  scale * 0.48f);
                DrawLine(a, b, line, w);
                DrawLine(b, c, line, w);
                DrawLine(c, a, line, w);
                break;
            }
            case "undertow_engine":
                DrawArc(Vector2.Zero, scale * 0.70f, -Mathf.Pi * 0.10f, Mathf.Pi * 1.10f, 20, line, w);
                DrawArc(Vector2.Zero, scale * 0.38f,  Mathf.Pi * 0.20f, Mathf.Pi * 1.36f, 16, line, w);
                break;
            case "accordion_engine":
                DrawLine(new Vector2(-scale * 0.56f, -scale * 0.72f), new Vector2(-scale * 0.56f, scale * 0.72f), line, w);
                DrawLine(new Vector2(0f, -scale * 0.90f), new Vector2(0f, scale * 0.90f), line, w);
                DrawLine(new Vector2( scale * 0.56f, -scale * 0.72f), new Vector2( scale * 0.56f, scale * 0.72f), line, w);
                break;
            case "phase_splitter":
                DrawCircle(new Vector2(-scale * 0.55f, 0f), scale * 0.20f, new Color(_color.R, _color.G, _color.B, alpha * 0.68f));
                DrawCircle(new Vector2( scale * 0.55f, 0f), scale * 0.20f, new Color(_color.R, _color.G, _color.B, alpha * 0.68f));
                DrawLine(new Vector2(-scale * 0.34f, 0f), new Vector2(scale * 0.34f, 0f), line, w);
                break;
            default:
                // X mark -- "danger zone" universal glyph.
                DrawLine(new Vector2(-scale * 0.62f, -scale * 0.62f), new Vector2(scale * 0.62f, scale * 0.62f), line, w);
                DrawLine(new Vector2( scale * 0.62f, -scale * 0.62f), new Vector2(-scale * 0.62f, scale * 0.62f), line, w);
                break;
        }
    }
}
