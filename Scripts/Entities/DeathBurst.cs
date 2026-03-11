using Godot;

namespace SlotTheory.Entities;

public enum DeathBurstStyle
{
    Basic,
    Swift,
    Armored,
}

/// <summary>
/// Short-lived expanding burst with style variants per enemy class.
/// Caller sets GlobalPosition after AddChild, then calls Initialize().
/// </summary>
public partial class DeathBurst : Node2D
{
    private const float Duration = 0.38f;
    private const int MaxSparkCount = 24;
    private const int TrailGhostCount = 3;

    private float _life;
    private Color _color;
    private float _scale;
    private readonly float[] _sparkAngles = new float[MaxSparkCount];
    private readonly float[] _sparkLengths = new float[MaxSparkCount];
    private readonly float[] _sparkWidths = new float[MaxSparkCount];
    private readonly Vector2[] _trailDirs = new Vector2[TrailGhostCount];
    private int _sparkCount;
    private DeathBurstStyle _style = DeathBurstStyle.Basic;
    private float _styleDelay;
    private float _speedFactor = 1f;

    public void Initialize(Color color, float scale = 1f, DeathBurstStyle style = DeathBurstStyle.Basic)
    {
        _color = color;
        _scale = scale;
        _style = style;
        _styleDelay = style == DeathBurstStyle.Armored ? 0.11f : 0f;
        _speedFactor = style switch
        {
            DeathBurstStyle.Swift => 1.22f,
            DeathBurstStyle.Armored => 0.92f,
            _ => 1f,
        };

        _sparkCount = style switch
        {
            DeathBurstStyle.Swift => 18,
            DeathBurstStyle.Armored => 14,
            _ => 16,
        };

        var rng = new System.Random();
        for (int i = 0; i < _sparkCount; i++)
        {
            _sparkAngles[i] = (float)(rng.NextDouble() * Mathf.Tau);
            _sparkLengths[i] = style switch
            {
                DeathBurstStyle.Swift => 0.95f + (float)(rng.NextDouble() * 1.25f),
                DeathBurstStyle.Armored => 0.70f + (float)(rng.NextDouble() * 0.85f),
                _ => 0.55f + (float)(rng.NextDouble() * 0.90f),
            };
            _sparkWidths[i] = style switch
            {
                DeathBurstStyle.Armored => 2.0f + (float)(rng.NextDouble() * 1.4f),
                DeathBurstStyle.Swift => 1.0f + (float)(rng.NextDouble() * 0.6f),
                _ => 1.2f + (float)(rng.NextDouble() * 0.8f),
            };
        }

        for (int i = 0; i < TrailGhostCount; i++)
        {
            float a = (float)(rng.NextDouble() * Mathf.Tau);
            _trailDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)).Normalized();
        }
    }

    public override void _Process(double delta)
    {
        _life += (float)delta * _speedFactor;
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
        float easeT = 1f - (1f - t) * (1f - t);

        if (t < 0.20f)
        {
            float fa = (0.20f - t) / 0.20f;
            DrawCircle(
                Vector2.Zero,
                _scale * 10f * (1f - t / 0.18f),
                new Color(1f, 1f, 1f, fa * 0.55f));
        }

        DrawFadeTrail(t);

        float ringR = _scale * (_style == DeathBurstStyle.Armored ? 27f : 24f) * easeT;
        DrawArc(
            Vector2.Zero,
            ringR,
            0f,
            Mathf.Tau,
            32,
            new Color(_color.R, _color.G, _color.B, (1f - t) * 0.85f),
            2.5f);

        if (t < 0.60f)
        {
            float t2 = t / 0.60f;
            float ease2 = 1f - (1f - t2) * (1f - t2);
            float innerR = _scale * 13f * ease2;
            float innerA = (1f - t2) * 0.65f;
            DrawArc(
                Vector2.Zero,
                innerR,
                0f,
                Mathf.Tau,
                24,
                new Color(
                    Mathf.Min(_color.R * 1.4f, 1f),
                    Mathf.Min(_color.G * 1.4f, 1f),
                    Mathf.Min(_color.B * 1.4f, 1f),
                    innerA),
                1.5f);
        }

        float sa = (1f - t) * (1f - t);
        for (int i = 0; i < _sparkCount; i++)
        {
            float inner = _scale * 5f * easeT;
            float outer = _scale * _sparkLengths[i] * 21f * easeT;
            var dir = new Vector2(Mathf.Cos(_sparkAngles[i]), Mathf.Sin(_sparkAngles[i]));
            DrawLine(
                dir * inner,
                dir * outer,
                new Color(_color.R, _color.G, _color.B, sa),
                _sparkWidths[i]);
        }

        if (_style == DeathBurstStyle.Swift)
        {
            DrawSwiftSnap(t, easeT);
        }
        else if (_style == DeathBurstStyle.Armored)
        {
            DrawArmoredSecondaryPop(t);
        }
    }

    private void DrawFadeTrail(float t)
    {
        float baseAlpha = (1f - t) * (_style == DeathBurstStyle.Swift ? 0.26f : 0.20f);
        float baseRadius = _style == DeathBurstStyle.Armored ? 8.6f : 6.8f;
        for (int i = 0; i < TrailGhostCount; i++)
        {
            float k = i / (float)TrailGhostCount;
            Vector2 dir = _trailDirs[i];
            Vector2 pos = -dir * _scale * (3f + i * 2.4f) * (0.45f + t * 1.8f);
            float radius = _scale * baseRadius * (1f - k * 0.18f) * (1f - 0.30f * t);
            float alpha = baseAlpha * (0.70f - k * 0.20f);
            DrawCircle(pos, radius, new Color(_color.R, _color.G, _color.B, alpha));
        }
    }

    private void DrawSwiftSnap(float t, float easeT)
    {
        float a = (1f - t) * 0.65f;
        float stretch = _scale * (18f + easeT * 18f);
        DrawLine(new Vector2(-stretch, -1.8f), new Vector2(stretch, 1.8f), new Color(0.95f, 1.00f, 0.75f, a), 1.5f);
        DrawLine(new Vector2(-stretch * 0.75f, 2.6f), new Vector2(stretch * 0.75f, -2.6f), new Color(0.75f, 1.00f, 0.45f, a * 0.85f), 1.2f);
    }

    private void DrawArmoredSecondaryPop(float t)
    {
        if (t <= _styleDelay)
            return;

        float delayedT = Mathf.Clamp((t - _styleDelay) / (1f - _styleDelay), 0f, 1f);
        float delayedEase = 1f - (1f - delayedT) * (1f - delayedT);
        float alpha = (1f - delayedT) * 0.58f;
        float radius = _scale * 30f * delayedEase;
        DrawArc(
            Vector2.Zero,
            radius,
            0f,
            Mathf.Tau,
            36,
            new Color(_color.R, _color.G * 0.8f, _color.B * 0.8f, alpha),
            3.1f);

        for (int i = 0; i < 6; i++)
        {
            float a = i * (Mathf.Tau / 6f) + delayedT * 0.35f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(
                dir * (_scale * 8f * delayedEase),
                dir * (_scale * 16f * delayedEase),
                new Color(_color.R, _color.G, _color.B, alpha * 0.85f),
                2.3f);
        }
    }
}
