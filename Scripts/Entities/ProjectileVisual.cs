using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Purely visual projectile. Travels to the target's world position at time of firing,
/// then frees itself. No gameplay effect — damage is already applied hitscan-style.
/// </summary>
public partial class ProjectileVisual : Node2D
{
    private Vector2 _targetPos;
    private float _speed;

    /// <summary>Call immediately after AddChild so GlobalPosition is resolvable.</summary>
    public void Initialize(Vector2 fromGlobal, Vector2 targetGlobal, Color color, float speed)
    {
        GlobalPosition = fromGlobal;
        _targetPos = targetGlobal;
        _speed = speed;

        AddChild(new ColorRect
        {
            Color = color,
            OffsetLeft  = -4f,
            OffsetTop   = -4f,
            OffsetRight =  4f,
            OffsetBottom = 4f,
        });
    }

    public override void _Process(double delta)
    {
        var toTarget = _targetPos - GlobalPosition;
        float dist = toTarget.Length();

        if (dist <= _speed * (float)delta)
        {
            QueueFree();
            return;
        }

        GlobalPosition += toTarget.Normalized() * _speed * (float)delta;
    }
}
