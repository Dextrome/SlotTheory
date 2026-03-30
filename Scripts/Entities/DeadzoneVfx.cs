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
    private const float TriggerDuration = 0.55f;
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

    // ── Trigger state: vortex implosion + lock-snap ─────────────────────────

    private void DrawTriggerBurst()
    {
        float t    = Mathf.Clamp(_stateElapsed / TriggerDuration, 0f, 1f);
        float inv  = 1f - t;
        float ease = t * t;                         // ease-in for inward rush
        float outerR = _triggerRadius * 0.92f;
        float ringRot = _phase + t * Mathf.Pi * 0.9f; // rings rotate as they implode

        // ── Layer 1: 3 concentric rotating broken rings contracting inward ──
        float r1 = outerR * (1f - ease);
        float r2 = outerR * 0.65f * Mathf.Max(0f, 1f - ease * 1.35f);
        float r3 = outerR * 0.36f * Mathf.Max(0f, 1f - ease * 1.80f);

        if (r1 > 1.5f)
        {
            int segs = 5;
            float gapA = 0.28f;
            float segA = (Mathf.Tau - segs * gapA) / segs;
            for (int s = 0; s < segs; s++)
            {
                float start = ringRot + s * (segA + gapA);
                float whitenFactor = 1f - inv * inv;
                DrawArc(Vector2.Zero, r1, start, start + segA, 14,
                    new Color(
                        Mathf.Lerp(_color.R, 1f, whitenFactor),
                        Mathf.Lerp(_color.G, 1f, whitenFactor),
                        Mathf.Lerp(_color.B, 1f, whitenFactor),
                        inv * 0.88f),
                    2.2f);
            }
        }
        if (r2 > 1.5f)
            DrawArc(Vector2.Zero, r2, ringRot * 0.6f, ringRot * 0.6f + Mathf.Tau, 28,
                new Color(_color.R * 0.5f + 0.5f, _color.G * 0.6f + 0.4f, 1.0f, inv * 0.55f), 1.6f);
        if (r3 > 1.5f)
            DrawArc(Vector2.Zero, r3, 0f, Mathf.Tau, 20,
                new Color(1f, 1f, 1f, Mathf.Max(0f, inv - 0.15f) * 0.72f), 1.4f);

        // ── Layer 2: 10 vortex spikes spiraling inward ───────────────────────
        int spikes = 10;
        float spikeLen = outerR * 0.30f;
        for (int s = 0; s < spikes; s++)
        {
            float baseAngle = s * Mathf.Tau / spikes;
            float vortexAngle = baseAngle + ease * Mathf.Pi * 0.60f;
            float spikeR = outerR * Mathf.Max(0f, 1f - ease * 1.08f);
            if (spikeR < 2f) continue;

            float tailAngle = vortexAngle - 0.20f;
            Vector2 head = new(Mathf.Cos(vortexAngle) * spikeR, Mathf.Sin(vortexAngle) * spikeR);
            Vector2 tail = new(Mathf.Cos(tailAngle) * (spikeR + spikeLen), Mathf.Sin(tailAngle) * (spikeR + spikeLen));
            float spikeAlpha = inv * 0.92f;
            float whitenSpike = 1f - inv * inv;
            DrawLine(tail, head,
                new Color(
                    Mathf.Lerp(_color.R * 0.6f + 0.4f, 1f, whitenSpike),
                    Mathf.Lerp(_color.G * 0.7f + 0.3f, 1f, whitenSpike),
                    Mathf.Lerp(_color.B * 0.6f + 0.1f, 1f, whitenSpike),
                    spikeAlpha),
                Mathf.Lerp(2.4f, 0.8f, t));
        }

        // ── Layer 3: Core implosion flash (peaks at t = 0.42) ────────────────
        float flashIn   = Mathf.Clamp(Mathf.InverseLerp(0f, 0.42f, t), 0f, 1f);
        float flashOut  = Mathf.Clamp(1f - Mathf.InverseLerp(0.42f, 1.0f, t), 0f, 1f);
        float flashA    = flashIn * flashOut;
        if (flashA > 0.01f)
        {
            float coreR = outerR * 0.30f * flashIn;
            DrawCircle(Vector2.Zero, coreR,       new Color(1f, 1f, 1f, flashA * 0.82f));
            DrawCircle(Vector2.Zero, coreR * 0.5f, new Color(1f, 1f, 1f, flashA * 0.96f));
        }

        // ── Layer 4: Lock-snap ticks (brief flash as zone locks shut) ─────────
        float lockIn  = Mathf.Clamp(Mathf.InverseLerp(0.46f, 0.60f, t), 0f, 1f);
        float lockOut = Mathf.Clamp(1f - Mathf.InverseLerp(0.60f, 1.0f, t), 0f, 1f);
        float lockA   = lockIn * lockOut;
        if (lockA > 0.01f)
        {
            float lockR = outerR * 0.40f;
            int ticks = 8;
            for (int k = 0; k < ticks; k++)
            {
                float angle = k * Mathf.Tau / ticks;
                Vector2 inner = new(Mathf.Cos(angle) * lockR * 0.40f, Mathf.Sin(angle) * lockR * 0.40f);
                Vector2 outer2 = new(Mathf.Cos(angle) * lockR, Mathf.Sin(angle) * lockR);
                DrawLine(inner, outer2,
                    new Color(_color.R * 0.4f + 0.6f, _color.G * 0.7f + 0.3f, 1.0f, lockA * 0.92f), 1.6f);
            }
            DrawCircle(Vector2.Zero, 3.8f, new Color(1f, 1f, 1f, lockA * 0.85f));
        }

        // ── Layer 5: Outward recoil sparks (physical counter-reaction) ────────
        float sparkFade = Mathf.Clamp(1f - Mathf.InverseLerp(0.40f, 1.0f, t), 0f, 1f) * 0.78f;
        if (t > 0.38f && sparkFade > 0.01f)
        {
            float st = Mathf.Clamp(Mathf.InverseLerp(0.38f, 1.0f, t), 0f, 1f);
            int nsparks = 7;
            for (int s = 0; s < nsparks; s++)
            {
                float angle = s * Mathf.Tau / nsparks + Mathf.Pi * 0.14f;
                float sparkDist = outerR * 0.52f * st;
                float sLen = outerR * 0.17f;
                Vector2 sparkTail = new(Mathf.Cos(angle) * Mathf.Max(0f, sparkDist - sLen), Mathf.Sin(angle) * Mathf.Max(0f, sparkDist - sLen));
                Vector2 sparkTip  = new(Mathf.Cos(angle) * sparkDist, Mathf.Sin(angle) * sparkDist);
                DrawLine(sparkTail, sparkTip,
                    new Color(_color.R * 0.5f + 0.5f, _color.G * 0.6f + 0.4f, 1.0f, sparkFade), 1.3f);
            }
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
