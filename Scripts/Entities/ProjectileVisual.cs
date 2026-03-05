using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core;

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
    private bool  _isSplitProjectile;
    private float _damageOverride = -1f;

    private const int TrailMax = 14;
    private readonly List<Vector2> _trail = new();

    public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                           TowerInstance tower, int waveIndex, List<EnemyInstance> enemies,
                           SlotTheory.Core.RunState? runState = null,
                           bool isSplitProjectile = false, float damageOverride = -1f)
    {
        GlobalPosition      = fromGlobal;
        _target             = target;
        _tower              = tower;
        _waveIndex          = waveIndex;
        _enemies            = enemies;
        _runState           = runState;
        _speed              = speed;
        _color              = color;
        _isSplitProjectile  = isSplitProjectile;
        _damageOverride     = damageOverride;
    }

    public override void _Draw()
    {
        // Trail — taper from thin/transparent at tail to full at head
        for (int i = 1; i < _trail.Count; i++)
        {
            float t = i / (float)_trail.Count;
            DrawLine(ToLocal(_trail[i - 1]), ToLocal(_trail[i]),
                new Color(_color.R, _color.G, _color.B, t * 0.82f),
                1.2f + t * 4.2f);
        }

        // Glow bloom behind diamond head
        DrawCircle(Vector2.Zero, 10f, new Color(_color.R, _color.G, _color.B, 0.30f));
        DrawCircle(Vector2.Zero, 5f, new Color(1f, 1f, 1f, 0.25f));

        // Diamond head
        DrawPolygon(
            new[] { new Vector2(0f, -5f), new Vector2(5f, 0f), new Vector2(0f, 5f), new Vector2(-5f, 0f) },
            new[] { _color });
        DrawCircle(Vector2.Zero, 2.0f, new Color(1f, 1f, 1f, 0.90f));
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
            // Arrived — apply primary damage
            if (_tower != null && _enemies != null && _target.Hp > 0)
            {
                float hpBefore = _target.Hp;
                var ctx = _damageOverride >= 0f
                    ? new DamageContext(_tower, _target, _waveIndex, _enemies, _runState,
                                        isChain: true, damageOverride: _damageOverride)
                    : new DamageContext(_tower, _target, _waveIndex, _enemies, _runState);
                DamageModel.Apply(ctx);
                SlotTheory.Core.SoundManager.Instance?.Play("hit");

                float dealt = hpBefore - _target.Hp;
                if (dealt > 0f)
                {
                    bool isKill = _target.Hp <= 0;
                    SpawnDamageNumber(_target.GlobalPosition, dealt, isKill, _tower?.TowerId ?? "");
                    if (isKill)
                    {
                        bool heavy = _tower?.TowerId == "heavy_cannon";
                        GameController.Instance?.TriggerHitStop(
                            realDuration: heavy ? 0.055f : 0.040f,
                            slowScale: heavy ? 0.16f : 0.22f);
                    }
                    if (!isKill && GodotObject.IsInstanceValid(_target))
                        _target.FlashHit();
                }

                // Chain bounces first (hitscan) — split shot then picks from surviving enemies
                if (_tower!.IsChainTower && !_isSplitProjectile)
                    ApplyChainHits(_target.GlobalPosition);

                // Split Shot — fires at enemies that weren't already killed by chain
                if (_tower!.SplitCount > 0 && !_isSplitProjectile)
                    SpawnSplitProjectiles(_target.GlobalPosition);
            }
            QueueFree();
            return;
        }

        GlobalPosition += toTarget.Normalized() * _speed * (float)delta;
    }

    private void ApplyChainHits(Vector2 startWorldPos)
    {
        var tower = _tower;
        if (tower == null || _enemies == null) return;

        var alreadyHit = new System.Collections.Generic.HashSet<EnemyInstance>();
        if (GodotObject.IsInstanceValid(_target) && _target != null)
            alreadyHit.Add(_target);

        Vector2 chainFrom = startWorldPos;
        float   damage    = tower.BaseDamage * tower.ChainDamageDecay;
        int     bounceHits = 0;

        for (int bounce = 0; bounce < tower.ChainCount; bounce++)
        {
            EnemyInstance? chainTarget = null;
            float bestDist = tower.ChainRange;

            foreach (var e in _enemies)
            {
                if (alreadyHit.Contains(e)) continue;
                if (e.Hp <= 0 || !GodotObject.IsInstanceValid(e)) continue;
                float d = chainFrom.DistanceTo(e.GlobalPosition);
                if (d < bestDist) { bestDist = d; chainTarget = e; }
            }

            if (chainTarget == null) break;

            float hpBefore = chainTarget.Hp;
            var ctx = new DamageContext(tower, chainTarget, _waveIndex, _enemies, _runState,
                                        isChain: true, damageOverride: damage);
            DamageModel.Apply(ctx);

            SpawnChainArc(chainFrom, chainTarget.GlobalPosition);

            float dealt = hpBefore - chainTarget.Hp;
            if (dealt >= 1f)
            {
                bool isKill = chainTarget.Hp <= 0;
                SpawnDamageNumber(chainTarget.GlobalPosition, dealt, isKill, tower.TowerId);
                if (isKill)
                    GameController.Instance?.TriggerHitStop(realDuration: 0.038f, slowScale: 0.24f);
                if (!isKill && GodotObject.IsInstanceValid(chainTarget))
                    chainTarget.FlashHit();
            }

            alreadyHit.Add(chainTarget);
            chainFrom = chainTarget.GlobalPosition;
            damage   *= tower.ChainDamageDecay;
            bounceHits++;
        }

        if (tower.ChainCount > 0 && bounceHits >= tower.ChainCount)
            GameController.Instance?.NotifyChainMaxBounce(chainFrom, bounceHits);
    }

    private void SpawnSplitProjectiles(Vector2 impactPos)
    {
        if (_tower == null || _enemies == null) return;

        float splitDamage = _tower.BaseDamage * SlotTheory.Core.Balance.SplitShotDamageRatio;
        int spawned = 0;

        var candidates = _enemies
            .Where(e => e != _target && e.Hp > 0 && GodotObject.IsInstanceValid(e))
            .OrderBy(e => e.GlobalPosition.DistanceTo(impactPos));

        foreach (var candidate in candidates)
        {
            if (spawned >= _tower.SplitCount + 1) break;
            if (candidate.GlobalPosition.DistanceTo(impactPos) > SlotTheory.Core.Balance.SplitShotRange) break;

            var split = new ProjectileVisual();
            GetParent().AddChild(split);
            var splitColor = new Color(_color.R, _color.G, _color.B, 0.65f);
            split.Initialize(impactPos, candidate, splitColor, speed: 500f,
                             _tower, _waveIndex, _enemies, _runState,
                             isSplitProjectile: true, damageOverride: splitDamage);
            spawned++;
        }
    }

    private void SpawnChainArc(Vector2 worldFrom, Vector2 worldTo)
    {
        var arc = new ChainArc();
        GetParent().AddChild(arc);
        arc.GlobalPosition = Vector2.Zero;
        arc.Initialize(worldFrom, worldTo, _color);
    }

    private void SpawnDamageNumber(Vector2 worldPos, float damage, bool isKill = false, string sourceTowerId = "")
    {
        var num = new DamageNumber();
        GetParent().AddChild(num);
        num.GlobalPosition = worldPos + new Vector2(0f, -14f);
        num.Initialize(damage, _color, isKill, sourceTowerId);
    }
}
