using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived hit spark burst used on projectile impact.
/// </summary>
public partial class ImpactSparkBurst : Node2D
{
    private const float Duration = 0.22f;
    private const int BaseParticles = 12;
    private const int HeavyParticles = 20;

    private float _life;
    private Color _color = Colors.White;
    private Vector2[] _positions = System.Array.Empty<Vector2>();
    private Vector2[] _velocities = System.Array.Empty<Vector2>();
    private bool _heavy;

    public void Initialize(Color color, bool heavy = false)
    {
        _color = color;
        _heavy = heavy;

        int count = heavy ? HeavyParticles : BaseParticles;
        _positions = new Vector2[count];
        _velocities = new Vector2[count];

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < count; i++)
        {
            float angle = rng.RandfRange(0f, Mathf.Tau);
            float speed = rng.RandfRange(heavy ? 110f : 90f, heavy ? 290f : 210f);
            _velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            _positions[i] = Vector2.Zero;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life += dt;
        if (_life >= Duration)
        {
            QueueFree();
            return;
        }

        float drag = _heavy ? 0.86f : 0.83f;
        for (int i = 0; i < _positions.Length; i++)
        {
            _positions[i] += _velocities[i] * dt;
            _velocities[i] *= drag;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_positions.Length == 0) return;

        float t = Mathf.Clamp(_life / Duration, 0f, 1f);
        float fade = 1f - t;

        float flashRadius = Mathf.Lerp(_heavy ? 8f : 6f, 1f, t);
        DrawCircle(Vector2.Zero, flashRadius, new Color(1f, 1f, 1f, 0.34f * fade));

        for (int i = 0; i < _positions.Length; i++)
        {
            Vector2 pos = _positions[i];
            Vector2 vel = _velocities[i];
            Vector2 dir = vel.LengthSquared() > 0.0001f ? vel.Normalized() : Vector2.Right;

            float trailLen = (_heavy ? 10f : 7f) * (0.55f + 0.45f * fade);
            Vector2 start = pos - dir * trailLen;
            float width = (_heavy ? 2.0f : 1.6f) * (0.35f + 0.65f * fade);
            var spark = new Color(_color.R, _color.G, _color.B, 0.78f * fade);
            DrawLine(start, pos, spark, width);

            if ((i % 3) == 0)
                DrawCircle(pos, 1.1f + 0.9f * fade, new Color(1f, 1f, 1f, 0.46f * fade));
        }
    }
}
