using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived battlefield marker for explosion aftermath zones.
/// Keeps phase-3 residue readable without long-lived clutter.
/// </summary>
public partial class ExplosionResidueZoneFx : Node2D
{
    private ExplosionResidueKind _kind = ExplosionResidueKind.None;
    private float _life;
    private float _duration = 1f;
    private float _radius = 96f;
    private float _potency = 1f;
    private Color _accent = new Color(0.86f, 0.95f, 1.00f, 1f);

    public void Initialize(ExplosionResidueKind kind, Color accent, float radius, float durationSec, float potency)
    {
        _kind = kind;
        _accent = accent;
        _radius = Mathf.Clamp(radius, 36f, 180f);
        _duration = Mathf.Clamp(durationSec, 0.20f, 1.40f);
        _potency = Mathf.Clamp(potency, 0.50f, 1.50f);
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
        float t = Mathf.Clamp(_life / _duration, 0f, 1f);
        float fade = 1f - t;
        float pulse = 0.88f + 0.12f * Mathf.Sin(_life * 12f);
        float radius = _radius * (0.94f + 0.05f * pulse);

        switch (_kind)
        {
            case ExplosionResidueKind.FrostSlow:
            {
                Color rim = new Color(0.56f, 0.92f, 1.00f, 0.34f * fade);
                DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 64, rim, 2.2f);
                DrawArc(Vector2.Zero, radius * 0.78f, 0f, Mathf.Tau, 48, new Color(0.90f, 1f, 1f, 0.18f * fade), 1.2f);
                for (int i = 0; i < 3; i++)
                {
                    float a = _life * 1.6f + i * Mathf.Tau / 3f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    DrawLine(
                        dir * (radius * 0.22f),
                        dir * (radius * 0.66f),
                        new Color(0.80f, 0.97f, 1f, 0.22f * fade),
                        1.2f);
                }
                break;
            }
            case ExplosionResidueKind.VulnerabilityZone:
            {
                Color zone = new Color(1f, 0.66f, 0.92f, 0.12f * fade);
                DrawCircle(Vector2.Zero, radius * 0.88f, zone);
                DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 72, new Color(1f, 0.78f, 0.96f, 0.36f * fade), 2.4f);
                DrawLine(
                    new Vector2(-radius * 0.64f, 0f),
                    new Vector2(radius * 0.64f, 0f),
                    new Color(1f, 0.90f, 1f, 0.28f * fade),
                    1.3f);
                DrawLine(
                    new Vector2(0f, -radius * 0.64f),
                    new Vector2(0f, radius * 0.64f),
                    new Color(1f, 0.90f, 1f, 0.20f * fade),
                    1.1f);
                break;
            }
            case ExplosionResidueKind.BurnPatch:
            {
                Color core = new Color(1f, 0.48f, 0.22f, 0.13f * fade);
                DrawCircle(Vector2.Zero, radius * 0.80f, core);
                DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 72, new Color(1f, 0.62f, 0.24f, 0.30f * fade), 2.1f);
                DrawArc(Vector2.Zero, radius * 0.62f, 0f, Mathf.Tau, 48, new Color(1f, 0.86f, 0.54f, 0.18f * fade), 1.1f);
                float emberRadius = 1.4f + 1.1f * _potency;
                DrawCircle(new Vector2(-radius * 0.20f, -radius * 0.06f), emberRadius, new Color(1f, 0.90f, 0.62f, 0.26f * fade));
                DrawCircle(new Vector2(radius * 0.24f, radius * 0.10f), emberRadius * 0.9f, new Color(1f, 0.84f, 0.56f, 0.22f * fade));
                break;
            }
            default:
                DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 48, new Color(_accent.R, _accent.G, _accent.B, 0.20f * fade), 1.8f);
                break;
        }
    }
}
