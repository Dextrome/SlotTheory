using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Dedicated detonation burst for Rift Sapper mines.
/// Chain detonations get a larger shock ring and brighter core flash.
/// </summary>
public partial class RiftMineBurst : Node2D
{
    private const float BaseDuration = 0.30f;
    private const float ChainDuration = 0.36f;
    private const int BaseShardCount = 14;
    private const int ChainShardCount = 20;

    private float _life;
    private float _duration = BaseDuration;
    private float _radiusScale = 1f;
    private Color _accent = new Color(0.70f, 1.0f, 0.66f);
    private bool _chainPop;

    private Vector2[] _positions = System.Array.Empty<Vector2>();
    private Vector2[] _velocities = System.Array.Empty<Vector2>();
    private float[] _weights = System.Array.Empty<float>();

    public void Initialize(Color accent, bool chainPop, float intensity = 1f)
    {
        _accent = accent;
        _chainPop = chainPop;
        _radiusScale = Mathf.Clamp(intensity, 0.75f, 1.55f);
        _duration = chainPop ? ChainDuration : BaseDuration;

        int count = chainPop ? ChainShardCount : BaseShardCount;
        _positions = new Vector2[count];
        _velocities = new Vector2[count];
        _weights = new float[count];

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int i = 0; i < count; i++)
        {
            float angle = rng.RandfRange(0f, Mathf.Tau);
            float speedMin = chainPop ? 130f : 110f;
            float speedMax = chainPop ? 310f : 270f;
            float speed = rng.RandfRange(speedMin, speedMax) * _radiusScale;
            _velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            _positions[i] = Vector2.Zero;
            _weights[i] = rng.RandfRange(0.65f, 1.2f);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _life += dt;
        if (_life >= _duration)
        {
            QueueFree();
            return;
        }

        float drag = _chainPop ? 0.88f : 0.84f;
        for (int i = 0; i < _positions.Length; i++)
        {
            _positions[i] += _velocities[i] * dt;
            _velocities[i] *= drag;
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_positions.Length == 0)
            return;

        float t = Mathf.Clamp(_life / _duration, 0f, 1f);
        float fade = 1f - t;
        float ease = 1f - Mathf.Pow(1f - t, 2f);

        float ringR = Mathf.Lerp(5f, (_chainPop ? 56f : 48f) * _radiusScale, ease);
        float ringW = (_chainPop ? 3.9f : 2.5f) * (0.45f + 0.55f * fade);
        var ringCol = new Color(_accent.R, _accent.G, _accent.B, (_chainPop ? 0.58f : 0.52f) * fade);
        DrawArc(Vector2.Zero, ringR, 0f, Mathf.Tau, 42, ringCol, ringW);

        if (_chainPop)
        {
            float ring2R = Mathf.Lerp(2f, 78f * _radiusScale, ease);
            float ring2A = Mathf.SmoothStep(0.95f, 0f, t) * 0.30f;
            DrawArc(Vector2.Zero, ring2R, 0f, Mathf.Tau, 54,
                new Color(0.86f, 1f, 0.84f, ring2A), 2.1f);
        }

        float flashRadius = Mathf.Lerp(_chainPop ? 18f : 14f, _chainPop ? 3f : 2f, t) * _radiusScale;
        DrawCircle(Vector2.Zero, flashRadius, new Color(1f, 1f, 1f, (_chainPop ? 0.36f : 0.30f) * fade));
        DrawCircle(Vector2.Zero, flashRadius * 0.55f,
            new Color(_accent.R, _accent.G, _accent.B, (_chainPop ? 0.40f : 0.34f) * fade));

        for (int i = 0; i < _positions.Length; i++)
        {
            Vector2 pos = _positions[i];
            Vector2 vel = _velocities[i];
            Vector2 dir = vel.LengthSquared() > 0.0001f ? vel.Normalized() : Vector2.Right;

            float shardLen = (_chainPop ? 12f : 10f) * _weights[i] * (0.52f + 0.48f * fade);
            float width = (_chainPop ? 2.2f : 1.6f) * _weights[i] * (0.30f + 0.70f * fade);
            DrawLine(pos - dir * shardLen, pos,
                new Color(_accent.R, _accent.G, _accent.B, 0.84f * fade), width);

            if ((i % 4) == 0)
            {
                DrawCircle(pos, (1.0f + 1.3f * fade) * _weights[i],
                    new Color(1f, 1f, 1f, 0.42f * fade));
            }
        }
    }
}
