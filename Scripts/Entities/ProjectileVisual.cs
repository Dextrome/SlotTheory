using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;

namespace SlotTheory.Entities;

/// <summary>
/// Visual projectile that tracks its target and applies damage on arrival.
/// If the target dies before arrival the projectile fades without dealing damage.
/// </summary>
public partial class ProjectileVisual : Node2D
{
    private EnemyInstance? _target;
    private TowerInstance? _tower;
    private int _waveIndex;
    private List<EnemyInstance>? _enemies;
    private float _speed;
    private Color _color;

    /// <summary>Call immediately after AddChild so GlobalPosition is resolvable.</summary>
    public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                           TowerInstance tower, int waveIndex, List<EnemyInstance> enemies)
    {
        GlobalPosition = fromGlobal;
        _target    = target;
        _tower     = tower;
        _waveIndex = waveIndex;
        _enemies   = enemies;
        _speed     = speed;
        _color     = color;
    }

    public override void _Draw()
    {
        // Small diamond shape
        DrawPolygon(
            new[] { new Vector2(0f,-4f), new Vector2(4f,0f), new Vector2(0f,4f), new Vector2(-4f,0f) },
            new[] { _color });
        // Bright core
        DrawCircle(Vector2.Zero, 1.5f, new Color(1f, 1f, 1f, 0.85f));
    }

    public override void _Process(double delta)
    {
        // Target already dead or freed — dissolve without hitting
        if (_target == null || !GodotObject.IsInstanceValid(_target) || _target.Hp <= 0)
        {
            QueueFree();
            return;
        }

        var toTarget = _target.GlobalPosition - GlobalPosition;
        float dist = toTarget.Length();

        if (dist <= _speed * (float)delta)
        {
            // Arrived — apply damage now
            if (_tower != null && _enemies != null && _target.Hp > 0)
            {
                var ctx = new DamageContext(_tower, _target, _waveIndex, _enemies);
                DamageModel.Apply(ctx);
            }
            QueueFree();
            return;
        }

        GlobalPosition += toTarget.Normalized() * _speed * (float)delta;
    }
}
