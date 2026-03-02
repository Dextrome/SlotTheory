using System.Collections.Generic;
using Godot;
using SlotTheory.Combat;

namespace SlotTheory.Entities;

/// <summary>
/// Visual projectile that tracks its target, leaves a glowing trail, applies damage on arrival,
/// and spawns a floating damage number.
/// If the target dies before arrival the projectile fades without dealing damage.
/// </summary>
public partial class ProjectileVisual : Node2D
{
    private EnemyInstance? _target;
    private TowerInstance? _tower;
    private int   _waveIndex;
    private List<EnemyInstance>? _enemies;
    private SlotTheory.Core.RunState? _runState;
    private float _speed;
    private Color _color;

    private const int TrailMax = 10;
    private readonly List<Vector2> _trail = new();

    public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                           TowerInstance tower, int waveIndex, List<EnemyInstance> enemies,
                           SlotTheory.Core.RunState? runState = null)
    {
        GlobalPosition = fromGlobal;
        _target    = target;
        _tower     = tower;
        _waveIndex = waveIndex;
        _enemies   = enemies;
        _runState  = runState;
        _speed     = speed;
        _color     = color;
    }

    public override void _Draw()
    {
        // Trail — taper from thin/transparent at tail to full at head
        for (int i = 1; i < _trail.Count; i++)
        {
            float t = i / (float)_trail.Count;
            DrawLine(ToLocal(_trail[i - 1]), ToLocal(_trail[i]),
                new Color(_color.R, _color.G, _color.B, t * 0.55f),
                t * 3f);
        }

        // Glow bloom behind diamond head
        DrawCircle(Vector2.Zero, 8f, new Color(_color.R, _color.G, _color.B, 0.25f));
        DrawCircle(Vector2.Zero, 4f, new Color(1f, 1f, 1f, 0.20f));

        // Diamond head
        DrawPolygon(
            new[] { new Vector2(0f, -4f), new Vector2(4f, 0f), new Vector2(0f, 4f), new Vector2(-4f, 0f) },
            new[] { _color });
        DrawCircle(Vector2.Zero, 1.5f, new Color(1f, 1f, 1f, 0.85f));
    }

    public override void _Process(double delta)
    {
        // Target already dead or freed — dissolve
        if (_target == null || !GodotObject.IsInstanceValid(_target) || _target.Hp <= 0)
        {
            QueueFree();
            return;
        }

        // Store position before moving (trail grows from oldest to newest)
        _trail.Add(GlobalPosition);
        if (_trail.Count > TrailMax)
            _trail.RemoveAt(0);
        QueueRedraw();

        var   toTarget = _target.GlobalPosition - GlobalPosition;
        float dist     = toTarget.Length();

        if (dist <= _speed * (float)delta)
        {
            // Arrived — apply damage
            if (_tower != null && _enemies != null && _target.Hp > 0)
            {
                float hpBefore = _target.Hp;
                var ctx = new DamageContext(_tower, _target, _waveIndex, _enemies, _runState);
                DamageModel.Apply(ctx);
                SlotTheory.Core.SoundManager.Instance?.Play("hit");

                float dealt = hpBefore - _target.Hp;
                if (dealt > 0f)
                {
                    bool isKill = _target.Hp <= 0;
                    SpawnDamageNumber(_target.GlobalPosition, dealt, isKill);
                    if (!isKill && GodotObject.IsInstanceValid(_target))
                        _target.FlashHit();
                }
            }
            QueueFree();
            return;
        }

        GlobalPosition += toTarget.Normalized() * _speed * (float)delta;
    }

    private void SpawnDamageNumber(Vector2 worldPos, float damage, bool isKill = false)
    {
        var num = new DamageNumber();
        GetParent().AddChild(num);
        num.GlobalPosition = worldPos + new Vector2(0f, -14f);
        num.Initialize(damage, _color, isKill);
    }
}
