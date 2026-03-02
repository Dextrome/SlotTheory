using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived expanding ring + radial sparks spawned when an enemy dies.
/// Caller sets GlobalPosition after AddChild, then calls Initialize().
/// </summary>
public partial class DeathBurst : Node2D
{
    private const float Duration = 0.35f;

    private const int SparkCount = 16;

    private float   _life;
    private Color   _color;
    private float   _scale;
    private float[] _sparkAngles  = new float[SparkCount];
    private float[] _sparkLengths = new float[SparkCount];

    public void Initialize(Color color, float scale = 1f)
    {
        _color = color;
        _scale = scale;
        var rng = new System.Random();
        for (int i = 0; i < SparkCount; i++)
        {
            _sparkAngles[i]  = (float)(rng.NextDouble() * Mathf.Tau);
            _sparkLengths[i] = 0.55f + (float)(rng.NextDouble() * 0.90f); // 0.55–1.45× base
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
        float t     = _life / Duration;
        float easeT = 1f - (1f - t) * (1f - t);   // ease-out

        // Brief white flash at centre
        if (t < 0.18f)
        {
            float fa = (0.18f - t) / 0.18f;
            DrawCircle(Vector2.Zero, _scale * 10f * (1f - t / 0.18f),
                new Color(1f, 1f, 1f, fa * 0.55f));
        }

        // Outer expanding ring
        float ringR = _scale * 24f * easeT;
        DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 32,
            new Color(_color.R, _color.G, _color.B, (1f - t) * 0.85f), 2.5f);

        // Inner ring — faster expansion, fades out in first 60% of animation
        if (t < 0.60f)
        {
            float t2     = t / 0.60f;
            float ease2  = 1f - (1f - t2) * (1f - t2);
            float innerR = _scale * 13f * ease2;
            float innerA = (1f - t2) * 0.65f;
            DrawArc(Vector2.Zero, innerR, 0f, Mathf.Tau, 24,
                new Color(Mathf.Min(_color.R * 1.4f, 1f), Mathf.Min(_color.G * 1.4f, 1f), Mathf.Min(_color.B * 1.4f, 1f), innerA), 1.5f);
        }

        // 16 semi-random radial sparks
        float sa = (1f - t) * (1f - t);
        for (int i = 0; i < SparkCount; i++)
        {
            float inner = _scale * 5f  * easeT;
            float outer = _scale * _sparkLengths[i] * 21f * easeT;
            var   dir   = new Vector2(Mathf.Cos(_sparkAngles[i]), Mathf.Sin(_sparkAngles[i]));
            DrawLine(dir * inner, dir * outer,
                new Color(_color.R, _color.G, _color.B, sa), 1.5f);
        }
    }
}
