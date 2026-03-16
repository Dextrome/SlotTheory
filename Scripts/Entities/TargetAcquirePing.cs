using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Ultra-short ring ping used when a tower acquires a new target.
/// </summary>
public partial class TargetAcquirePing : Node2D
{
    private const float Duration = 0.10f;

    private float _life = 0f;
    private Color _color = new Color(0.75f, 0.95f, 1f);

    public void Initialize(Color color)
    {
        _color = color;
    }

    public override void _Process(double delta)
    {
        _life += (float)delta;
        if (_life >= Duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(_life / Duration, 0f, 1f);
        float radius = Mathf.Lerp(8f, 14f, t);
        float alpha = 1f - t;
        var ring = new Color(_color.R, _color.G, _color.B, 0.50f * alpha);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, ring, 1.8f);
        DrawCircle(Vector2.Zero, 1.8f, new Color(1f, 1f, 1f, 0.46f * alpha));
    }
}
