using System.Collections.Generic;
using System.Linq;
using System;
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
    private int _waveIndex;
    private List<EnemyInstance>? _enemies;
    private SlotTheory.Core.RunState? _runState;
    private float _speed;
    private Color _color;
    private bool _isSplitProjectile;
    private float _damageOverride = -1f;
    private Action<DamageContext, float, bool>? _onPrimaryImpact;

    private const int TrailMax = 14;
    private readonly List<Vector2> _trail = new();

    public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                           TowerInstance tower, int waveIndex, List<EnemyInstance> enemies,
                           SlotTheory.Core.RunState? runState = null,
                           bool isSplitProjectile = false, float damageOverride = -1f,
                           Action<DamageContext, float, bool>? onPrimaryImpact = null)
    {
        GlobalPosition = fromGlobal;
        _target = target;
        _tower = tower;
        _waveIndex = waveIndex;
        _enemies = enemies;
        _runState = runState;
        _speed = speed;
        _color = color;
        _isSplitProjectile = isSplitProjectile;
        _damageOverride = damageOverride;
        _onPrimaryImpact = onPrimaryImpact;
    }

    public override void _Draw()
    {
        bool rocket = _tower?.TowerId == "rocket_launcher";
        bool latch = _tower?.TowerId == "latch_nest";

        // Trail taper from thin/transparent at tail to full at head.
        for (int i = 1; i < _trail.Count; i++)
        {
            float t = i / (float)_trail.Count;
            float width = rocket ? (2.0f + t * 5.8f) : (1.2f + t * 4.2f);
            if (latch)
                width = 1.6f + t * 3.2f;
            DrawLine(ToLocal(_trail[i - 1]), ToLocal(_trail[i]),
                new Color(_color.R, _color.G, _color.B, t * (rocket ? 0.88f : (latch ? 0.56f : 0.82f))),
                width);
        }

        if (rocket)
        {
            DrawCircle(new Vector2(0f, 7.5f), 6.0f, new Color(1f, 0.55f, 0.12f, 0.36f));
            DrawCircle(new Vector2(0f, 9.8f), 3.6f, new Color(1f, 0.86f, 0.40f, 0.42f));
            DrawRect(new Rect2(-2.8f, -8.2f, 5.6f, 12.4f), _color);
            DrawPolygon(new[]
            {
                new Vector2(0f, -11.0f),
                new Vector2(3.3f, -5.5f),
                new Vector2(-3.3f, -5.5f),
            }, new[] { new Color(1f, 0.90f, 0.68f, 0.95f) });
            DrawPolygon(new[]
            {
                new Vector2(-2.8f, 3.2f),
                new Vector2(-5.9f, 7.0f),
                new Vector2(-1.2f, 6.4f),
            }, new[] { new Color(_color.R, _color.G, _color.B, 0.85f) });
            DrawPolygon(new[]
            {
                new Vector2(2.8f, 3.2f),
                new Vector2(5.9f, 7.0f),
                new Vector2(1.2f, 6.4f),
            }, new[] { new Color(_color.R, _color.G, _color.B, 0.85f) });
            DrawCircle(new Vector2(0f, -2.5f), 1.5f, new Color(1f, 1f, 1f, 0.90f));
            return;
        }

        if (latch)
        {
            DrawCircle(Vector2.Zero, 8.8f, new Color(_color.R * 0.5f, _color.G * 0.5f, _color.B * 0.36f, 0.30f));
            DrawPolygon(new[]
            {
                new Vector2(0f, -7.2f),
                new Vector2(6.4f, -1.5f),
                new Vector2(3.4f, 7.0f),
                new Vector2(-3.4f, 7.0f),
                new Vector2(-6.4f, -1.5f),
            }, new[] { new Color(_color.R * 0.56f + 0.20f, _color.G * 0.46f + 0.18f, _color.B * 0.30f + 0.12f, 0.96f) });
            DrawCircle(new Vector2(0f, -0.8f), 2.0f, new Color(_color.R, _color.G, _color.B, 0.92f));
            DrawLine(new Vector2(-2.0f, 4.4f), new Vector2(-5.0f, 8.0f), new Color(0.90f, 0.98f, 0.84f, 0.84f), 1.1f);
            DrawLine(new Vector2(2.0f, 4.4f), new Vector2(5.0f, 8.0f), new Color(0.90f, 0.98f, 0.84f, 0.84f), 1.1f);
            return;
        }

        // Default projectile look.
        DrawCircle(Vector2.Zero, 10f, new Color(_color.R, _color.G, _color.B, 0.30f));
        DrawCircle(Vector2.Zero, 5f, new Color(1f, 1f, 1f, 0.25f));
        DrawPolygon(
            new[] { new Vector2(0f, -5f), new Vector2(5f, 0f), new Vector2(0f, 5f), new Vector2(-5f, 0f) },
            new[] { _color });
        DrawCircle(Vector2.Zero, 2.0f, new Color(1f, 1f, 1f, 0.90f));
    }

    public override void _Process(double delta)
    {
        // Target already dead or freed - dissolve.
        if (_target == null || !GodotObject.IsInstanceValid(_target) || _target.Hp <= 0)
        {
            QueueFree();
            return;
        }

        // Store position before moving (trail grows from oldest to newest).
        _trail.Add(GlobalPosition);
        if (_trail.Count > TrailMax)
            _trail.RemoveAt(0);
        QueueRedraw();

        var toTarget = _target.GlobalPosition - GlobalPosition;
        float dist = toTarget.Length();

        if (_tower?.TowerId == "rocket_launcher" && dist > 0.0001f)
            Rotation = toTarget.Angle() + Mathf.Pi * 0.5f;

        if (dist <= _speed * (float)delta)
        {
            TowerInstance? tower = _tower;
            // Arrived - apply primary damage.
            if (tower != null && _enemies != null && _target.Hp > 0)
            {
                float hpBefore = _target.Hp;
                var ctx = _damageOverride >= 0f
                    ? new DamageContext(tower, _target, _waveIndex, _enemies, _runState,
                                        isChain: false, damageOverride: _damageOverride)
                    : new DamageContext(tower, _target, _waveIndex, _enemies, _runState);
                DamageModel.Apply(ctx);
                _onPrimaryImpact?.Invoke(ctx, Mathf.Max(0f, hpBefore - _target.Hp), _target.Hp <= 0f);

                string hitSound = tower.TowerId == "rocket_launcher" ? "hit_rocket" : "hit";
                SlotTheory.Core.SoundManager.Instance?.Play(hitSound);

                float dealt = hpBefore - _target.Hp;
                if (dealt > 0f)
                {
                    bool isKill = _target.Hp <= 0;
                    SpawnDamageNumber(_target.GlobalPosition, dealt, isKill, tower.TowerId);
                    bool heavyImpact = tower.TowerId is "heavy_cannon" or "rocket_launcher";
                    SpawnImpactSparks(_target.GlobalPosition, heavy: heavyImpact);
                    if (isKill)
                    {
                        bool heavy = tower.TowerId is "heavy_cannon" or "rocket_launcher";
                        GameController.Instance?.TriggerHitStop(
                            realDuration: heavy ? 0.053f : 0.040f,
                            slowScale: heavy ? 0.18f : 0.22f);
                    }
                    if (!isKill && GodotObject.IsInstanceValid(_target))
                        _target.FlashHit();
                }

                // Chain bounces first (hitscan) - split shot then picks from surviving enemies.
                if (tower.IsChainTower && !_isSplitProjectile)
                    ApplyChainHits(_target.GlobalPosition);

                // Split Shot - fires at enemies that weren't already killed by chain.
                if (tower.SplitCount > 0 && !_isSplitProjectile)
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

        var alreadyHit = new HashSet<EnemyInstance>();
        if (GodotObject.IsInstanceValid(_target) && _target != null)
            alreadyHit.Add(_target);

        Vector2 chainFrom = startWorldPos;
        float damage = tower.BaseDamage * tower.ChainDamageDecay;
        int bounceHits = 0;

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
            SlotTheory.Core.SoundManager.Instance?.Play("hit");

            SpawnChainArc(chainFrom, chainTarget.GlobalPosition);

            float dealt = hpBefore - chainTarget.Hp;
            if (dealt >= 1f)
            {
                bool isKill = chainTarget.Hp <= 0;
                SpawnDamageNumber(chainTarget.GlobalPosition, dealt, isKill, tower.TowerId);
                SpawnImpactSparks(chainTarget.GlobalPosition);
                if (isKill)
                    GameController.Instance?.TriggerHitStop(realDuration: 0.038f, slowScale: 0.24f);
                if (!isKill && GodotObject.IsInstanceValid(chainTarget))
                    chainTarget.FlashHit();
            }

            alreadyHit.Add(chainTarget);
            chainFrom = chainTarget.GlobalPosition;
            damage *= tower.ChainDamageDecay;
            bounceHits++;
        }

        if (tower.ChainCount > 0 && bounceHits >= tower.ChainCount)
            GameController.Instance?.NotifyChainMaxBounce(chainFrom, bounceHits);
        if (bounceHits > 0)
        {
            float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
            GameController.Instance?.RegisterSpectacleProc(tower, SpectacleDefinitions.ChainReaction,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
        }
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

        if (spawned > 0)
        {
            SlotTheory.Core.SoundManager.Instance?.Play("shoot_rapid");
            GameController.Instance?.RegisterSpectacleProc(_tower, SpectacleDefinitions.SplitShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
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

    private void SpawnImpactSparks(Vector2 worldPos, bool heavy = false)
    {
        if (SettingsManager.Instance?.ReducedMotion == true)
            return;

        var parent = GetParent();
        if (parent == null)
            return;

        var burst = new ImpactSparkBurst();
        parent.AddChild(burst);
        burst.GlobalPosition = worldPos;
        burst.Initialize(_color, heavy);
    }
}
