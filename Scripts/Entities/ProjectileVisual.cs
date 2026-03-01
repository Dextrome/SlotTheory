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

    /// <summary>Call immediately after AddChild so GlobalPosition is resolvable.</summary>
    public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                           TowerInstance tower, int waveIndex, List<EnemyInstance> enemies)
    {
        GlobalPosition = fromGlobal;
        _target  = target;
        _tower   = tower;
        _waveIndex = waveIndex;
        _enemies = enemies;
        _speed   = speed;

        AddChild(new ColorRect
        {
            Color        = color,
            OffsetLeft   = -4f,
            OffsetTop    = -4f,
            OffsetRight  =  4f,
            OffsetBottom =  4f,
        });
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
