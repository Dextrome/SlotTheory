using System.Collections.Generic;
using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Enemy node. Extends PathFollow2D so it self-moves along LanePath.
/// RunState.EnemiesAlive holds references to these nodes.
/// </summary>
public partial class EnemyInstance : PathFollow2D, IEnemyView
{
    private readonly struct TrailSample
    {
        public Vector2 WorldPos { get; }
        public float Age { get; }
        public Vector2 Heading { get; }
        public float TurnCurve { get; }
        public float SpeedNorm { get; }

        public TrailSample(Vector2 worldPos, float age, Vector2 heading, float turnCurve, float speedNorm)
        {
            WorldPos = worldPos;
            Age = age;
            Heading = heading;
            TurnCurve = turnCurve;
            SpeedNorm = speedNorm;
        }

        public TrailSample WithAge(float age) => new(WorldPos, age, Heading, TurnCurve, SpeedNorm);
    }

    public string EnemyTypeId { get; private set; } = "basic_walker";
    public float Hp { get; set; }
    public float MaxHp { get; private set; }
    public float Speed { get; set; }

    public float MarkedRemaining { get; set; } = 0f;
    public bool IsMarked => MarkedRemaining > 0f;

    public float SlowRemaining { get; set; } = 0f;
    public float SlowSpeedFactor { get; set; } = Balance.SlowSpeedFactor;
    public bool IsSlowed => SlowRemaining > 0f;

    private ColorRect? _hpFill;
    private float _hpBarWidth;
    private bool _wasSlow;
    private float _markAngle;
    private float _visualTime;
    private Vector2 _drawOffset = Vector2.Zero;
    private Vector2 _drawScale = Vector2.One;
    private float _drawRotation;
    private float _thrustPulse;
    private float _nearDeathPulse;
    private float _nearDeathFlicker;
    private float _hitFlash;
    private Vector2 _baseScale = Vector2.One;
    private Tween? _hitTween;

    private EnemyVisualArchetype _archetype;
    private float _hpRatio = 1f;
    private Vector2 _lastWorldPos;
    private Vector2 _lastHeading = Vector2.Right;
    private bool _hasLastHeading;
    private float _facingAngle;
    private bool _hasFacingAngle;
    private float _turnTilt;
    private float _trailEmitTimer;
    private readonly List<TrailSample> _trail = new();

    public override void _Ready()
    {
        Loop = false;
        Rotates = false; // We drive facing manually so corner turns can ease instead of snapping 90°.
        _lastWorldPos = GlobalPosition;
    }

    public void Initialize(string typeId, float hp, float speed)
    {
        EnemyTypeId = typeId;
        Hp = MaxHp = hp;
        Speed = speed;
        _archetype = EnemyVisualArchetype.ForType(typeId);

        bool isArmored = typeId == "armored_walker";
        bool isSwift = typeId == "swift_walker";
        _baseScale = isArmored ? new Vector2(1.5f, 1.5f)
            : isSwift ? new Vector2(0.8f, 0.8f)
            : Vector2.One;
        Scale = _baseScale;

        _hpBarWidth = isArmored ? 34f : isSwift ? 20f : 24f;
        float barY = isArmored ? -26f : isSwift ? -17f : -20f;
        float barX = -_hpBarWidth / 2f;

        AddChild(new ColorRect
        {
            Position = new Vector2(barX, barY),
            Size = new Vector2(_hpBarWidth, 3f),
            Color = new Color(0.15f, 0.15f, 0.15f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _hpFill = new ColorRect
        {
            Position = new Vector2(barX, barY),
            Size = new Vector2(_hpBarWidth, 3f),
            Color = new Color(0.15f, 0.90f, 0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_hpFill);

        _lastWorldPos = GlobalPosition;
        _trail.Clear();
        _trailEmitTimer = 0f;
        _hasLastHeading = false;
        _hasFacingAngle = false;
        _turnTilt = 0f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        float effectiveSpeed = IsSlowed ? Speed * SlowSpeedFactor : Speed;

        Vector2 worldBefore = GlobalPosition;
        Progress += effectiveSpeed * dt;
        Vector2 worldAfter = GlobalPosition;
        _visualTime += dt;

        if (MarkedRemaining > 0f) MarkedRemaining -= dt;
        if (SlowRemaining > 0f) SlowRemaining -= dt;

        _hpRatio = MaxHp > 0f ? Mathf.Clamp(Hp / MaxHp, 0f, 1f) : 1f;
        _hitFlash = Mathf.Max(0f, _hitFlash - dt * 5.2f);

        float turnRate = UpdateHeadingAndTilt(worldBefore, worldAfter, dt);
        UpdateFacingRotation(dt, effectiveSpeed);

        var motion = EnemyVisualProfile.Evaluate(EnemyTypeId, _visualTime, effectiveSpeed, _hpRatio);
        float bankOffset = _turnTilt * 18.0f;
        float bankAbs = Mathf.Abs(_turnTilt);
        _drawOffset = new Vector2(
            motion.JitterX,
            motion.BobOffsetPx + motion.JitterY + bankOffset);
        _drawRotation = motion.BodyTiltRad + _turnTilt;
        _drawScale = new Vector2(
            1f + motion.ThrustPulse * 0.025f - bankAbs * 0.10f,
            1f - motion.ThrustPulse * 0.016f + bankAbs * 0.08f);
        _thrustPulse = motion.ThrustPulse;
        _nearDeathPulse = motion.NearDeathPulse;
        _nearDeathFlicker = motion.NearDeathFlicker;

        UpdateTrail(worldAfter, effectiveSpeed, turnRate, dt);

        if (IsMarked) _markAngle += dt * (2.2f + motion.ThrustPulse * 0.9f);

        if (_hpFill != null && MaxHp > 0f)
        {
            _hpFill.Size = new Vector2(_hpBarWidth * _hpRatio, _hpFill.Size.Y);
            _hpFill.Color = ResolveHpBandColor(_hpRatio);
        }

        bool nowSlowed = IsSlowed;
        if (nowSlowed != _wasSlow)
            SelfModulate = nowSlowed ? new Color(0.66f, 0.80f, 0.95f) : Colors.White;

        _wasSlow = nowSlowed;
        _lastWorldPos = worldAfter;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        DrawMotionTrail();

        DrawSetTransform(_drawOffset, _drawRotation, _drawScale);
        var layerSettings = EnemyRenderLayerSettings.Resolve(SettingsManager.Instance);
        if (layerSettings.LayeredEnabled)
        {
            var rs = BuildRenderState();
            DrawLayeredEnemy(rs, layerSettings);
            DrawLayeredStatusOverlays();
        }
        else
        {
            switch (EnemyTypeId)
            {
                case "armored_walker": DrawArmoredWalker(); break;
                case "swift_walker": DrawSwiftWalker(); break;
                default: DrawBasicWalker(); break;
            }
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private EnemyRenderState BuildRenderState()
        => new EnemyRenderState(
            hpRatio: _hpRatio,
            thrustPulse: _thrustPulse,
            nearDeathPulse: _nearDeathPulse,
            nearDeathFlicker: _nearDeathFlicker,
            hitFlash: _hitFlash,
            isMarked: IsMarked,
            isSlowed: IsSlowed);

    private void DrawLayeredEnemy(in EnemyRenderState rs, in EnemyRenderLayerSettings layerSettings)
    {
        var style = EnemyRenderStyle.ForType(EnemyTypeId);
        float emissiveWidthScale = ResolveEmissiveWidthScale(layerSettings);
        switch (EnemyTypeId)
        {
            case "armored_walker":
                DrawBodyPassArmored(style, rs);
                if (layerSettings.RenderDamage) DrawDamagePassArmored(style, rs);
                if (layerSettings.RenderEmissive) DrawEmissivePassArmored(style, rs, emissiveWidthScale);
                DrawBloomOrFallbackArmored(style, rs, layerSettings);
                break;
            case "swift_walker":
                DrawBodyPassSwift(style, rs);
                if (layerSettings.RenderDamage) DrawDamagePassSwift(style, rs);
                if (layerSettings.RenderEmissive) DrawEmissivePassSwift(style, rs, emissiveWidthScale);
                DrawBloomOrFallbackSwift(style, rs, layerSettings);
                break;
            default:
                DrawBodyPassBasic(style, rs);
                if (layerSettings.RenderDamage) DrawDamagePassBasic(style, rs);
                if (layerSettings.RenderEmissive) DrawEmissivePassBasic(style, rs, emissiveWidthScale);
                DrawBloomOrFallbackBasic(style, rs, layerSettings);
                break;
        }
    }

    private float ResolveEmissiveWidthScale(in EnemyRenderLayerSettings layerSettings)
    {
        float resolutionScale = 1f;
        var viewport = GetViewport();
        if (viewport != null)
        {
            float height = viewport.GetVisibleRect().Size.Y;
            resolutionScale = Mathf.Clamp(height / 720f, 0.78f, 1.45f);
        }

        float result = resolutionScale * layerSettings.EmissivePerfScale;
        return Mathf.Clamp(result, 0.72f, 1.45f);
    }

    private void DrawBloomOrFallbackBasic(in EnemyRenderStyle style, in EnemyRenderState rs, in EnemyRenderLayerSettings layerSettings)
    {
        const int bloomPrimitives = 2;
        if (layerSettings.RenderBloom &&
            EnemyRenderDebugCounters.TryReserveBloom(bloomPrimitives, layerSettings.BloomPrimitiveBudget))
        {
            DrawBloomPassBasic(style, rs, layerSettings.BloomAlphaScale);
            return;
        }

        if (ShouldRenderBloomFallback(layerSettings))
            DrawBloomFallbackBasic(style, rs, layerSettings.BloomFallbackAlphaScale);
    }

    private void DrawBloomOrFallbackSwift(in EnemyRenderStyle style, in EnemyRenderState rs, in EnemyRenderLayerSettings layerSettings)
    {
        const int bloomPrimitives = 3;
        if (layerSettings.RenderBloom &&
            EnemyRenderDebugCounters.TryReserveBloom(bloomPrimitives, layerSettings.BloomPrimitiveBudget))
        {
            DrawBloomPassSwift(style, rs, layerSettings.BloomAlphaScale);
            return;
        }

        if (ShouldRenderBloomFallback(layerSettings))
            DrawBloomFallbackSwift(style, rs, layerSettings.BloomFallbackAlphaScale);
    }

    private void DrawBloomOrFallbackArmored(in EnemyRenderStyle style, in EnemyRenderState rs, in EnemyRenderLayerSettings layerSettings)
    {
        const int bloomPrimitives = 3;
        if (layerSettings.RenderBloom &&
            EnemyRenderDebugCounters.TryReserveBloom(bloomPrimitives, layerSettings.BloomPrimitiveBudget))
        {
            DrawBloomPassArmored(style, rs, layerSettings.BloomAlphaScale);
            return;
        }

        if (ShouldRenderBloomFallback(layerSettings))
            DrawBloomFallbackArmored(style, rs, layerSettings.BloomFallbackAlphaScale);
    }

    private static bool ShouldRenderBloomFallback(in EnemyRenderLayerSettings layerSettings)
        => layerSettings.RenderBloomFallback || (layerSettings.RenderBloom && layerSettings.RenderEmissive);

    private void DrawLayeredStatusOverlays()
    {
        switch (EnemyTypeId)
        {
            case "armored_walker":
                DrawMarkedOverlay(19f);
                DrawSlowOverlay(21.5f, 17f);
                DrawNearDeathOverlay(16f);
                break;
            case "swift_walker":
                DrawMarkedOverlay(13f);
                DrawSlowOverlay(14.5f, 11f);
                DrawNearDeathOverlay(10f);
                break;
            default:
                DrawMarkedOverlay(13f);
                DrawSlowOverlay(15.5f, 11f);
                DrawNearDeathOverlay(11f);
                break;
        }
    }

    private float UpdateHeadingAndTilt(Vector2 worldBefore, Vector2 worldAfter, float dt)
    {
        Vector2 velocity = worldAfter - worldBefore;
        float turnRate = 0f;

        if (velocity.LengthSquared() > 0.001f)
        {
            Vector2 heading = velocity.Normalized();
            if (_hasLastHeading)
            {
                float turn = Mathf.Wrap(heading.Angle() - _lastHeading.Angle(), -Mathf.Pi, Mathf.Pi);
                turnRate = turn / Mathf.Max(0.0001f, dt);
            }

            _lastHeading = heading;
            _hasLastHeading = true;
        }

        float desiredTilt = Mathf.Clamp(
            turnRate * 0.090f * _archetype.TurnTiltScale,
            -_archetype.TurnTiltMaxRad,
            _archetype.TurnTiltMaxRad);
        // Apply turn impulse immediately on corners, then decay smoothly for a readable bank.
        if (Mathf.Abs(desiredTilt) > Mathf.Abs(_turnTilt))
        {
            _turnTilt = desiredTilt;
        }
        else
        {
            float decayLerp = Mathf.Clamp(4.8f * dt, 0.03f, 0.24f);
            _turnTilt = Mathf.Lerp(_turnTilt, desiredTilt, decayLerp);
        }
        return turnRate;
    }

    private void UpdateFacingRotation(float dt, float effectiveSpeed)
    {
        if (!_hasLastHeading)
            return;

        float targetAngle = _lastHeading.Angle();
        if (!_hasFacingAngle)
        {
            _facingAngle = targetAngle;
            _hasFacingAngle = true;
        }

        float speedNorm = Mathf.Clamp(effectiveSpeed / Mathf.Max(1f, Balance.BaseEnemySpeed), 0.35f, 2.6f);
        float response = EnemyTypeId switch
        {
            "swift_walker" => 6.6f,
            "armored_walker" => 3.8f,
            _ => 4.9f,
        };
        float lerp = Mathf.Clamp(response * speedNorm * dt, 0.02f, 0.42f);
        _facingAngle = Mathf.LerpAngle(_facingAngle, targetAngle, lerp);
        Rotation = _facingAngle;
    }

    private void UpdateTrail(Vector2 worldPos, float effectiveSpeed, float turnRate, float dt)
    {
        float speedNorm = Mathf.Clamp(effectiveSpeed / Mathf.Max(1f, Balance.BaseEnemySpeed), 0.35f, 2.6f);

        for (int i = _trail.Count - 1; i >= 0; i--)
        {
            var aged = _trail[i].WithAge(_trail[i].Age + dt);
            if (aged.Age >= _archetype.TrailLifetime)
                _trail.RemoveAt(i);
            else
                _trail[i] = aged;
        }

        _trailEmitTimer += dt;
        float emitSpacing = _archetype.TrailSpacing / Mathf.Clamp(speedNorm, 0.65f, 1.9f);
        if (_trailEmitTimer < emitSpacing || !_hasLastHeading)
            return;

        _trailEmitTimer = 0f;
        float curve = Mathf.Clamp(turnRate * 0.06f, -1f, 1f);
        _trail.Add(new TrailSample(worldPos, 0f, _lastHeading, curve, speedNorm));

        // Extra sample on hard turns so corners leave a stronger curve hint.
        if (Mathf.Abs(turnRate) > 3.4f)
        {
            _trail.Add(new TrailSample(worldPos, 0f, _lastHeading, curve * 1.35f, speedNorm));
        }

        if (_trail.Count > 28)
            _trail.RemoveRange(0, _trail.Count - 28);
    }

    private void DrawMotionTrail()
    {
        if (_trail.Count < 2)
            return;

        for (int i = 1; i < _trail.Count; i++)
        {
            var prev = _trail[i - 1];
            var cur = _trail[i];
            float ageT = Mathf.Clamp(cur.Age / Mathf.Max(0.001f, _archetype.TrailLifetime), 0f, 1f);
            float alpha = (1f - ageT) * (1f - ageT) * _archetype.TrailColor.A;
            if (alpha <= 0.01f)
                continue;

            Vector2 p0 = ToLocal(prev.WorldPos);
            Vector2 p1 = ToLocal(cur.WorldPos);
            Vector2 delta = p1 - p0;
            if (delta.LengthSquared() <= 0.001f)
                continue;

            Vector2 dir = delta.Normalized();
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float curveOffset = cur.TurnCurve * 4.0f * (1f - ageT);
            Vector2 mid = (p0 + p1) * 0.5f + perp * curveOffset;
            float width = _archetype.TrailWidth * (1f - ageT * 0.62f) * (0.84f + cur.SpeedNorm * 0.22f);
            Color c = new Color(_archetype.TrailColor.R, _archetype.TrailColor.G, _archetype.TrailColor.B, alpha);

            switch (_archetype.TrailShape)
            {
                case EnemyTrailShape.RazorArc:
                    DrawLine(p0, mid, c, width * 0.9f);
                    DrawLine(mid, p1, c, width * 0.9f);
                    DrawLine(mid + perp * 0.8f, p1, new Color(0.95f, 1.00f, 0.84f, alpha * 0.52f), width * 0.42f);
                    break;
                case EnemyTrailShape.DenseEmber:
                    DrawLine(p0, mid, c, width * 1.08f);
                    DrawLine(mid, p1, c, width * 1.08f);
                    DrawCircle(mid, 1.1f + width * 0.20f, new Color(c.R, c.G * 0.9f, c.B * 0.9f, alpha * 0.58f));
                    break;
                default:
                    DrawLine(p0, mid, c, width);
                    DrawLine(mid, p1, c, width);
                    DrawLine(mid, p1, new Color(0.90f, 1.00f, 1.00f, alpha * 0.26f), width * 0.48f);
                    break;
            }
        }
    }

    private void DrawMarkedOverlay(float radius)
    {
        if (!IsMarked) return;

        for (int s = 0; s < 3; s++)
        {
            float a = _markAngle + s * (Mathf.Tau / 3f);
            DrawArc(Vector2.Zero, radius, a, a + Mathf.Pi * 0.5f, 12, new Color(0.85f, 0.30f, 1.00f, 0.90f), 2.5f);
        }

        float scanY = Mathf.Sin(_markAngle * 1.35f) * radius * 0.58f;
        DrawLine(new Vector2(-radius * 0.82f, scanY), new Vector2(radius * 0.82f, scanY), new Color(0.90f, 0.42f, 1.00f, 0.35f), 1.6f);
        DrawLine(new Vector2(-radius * 0.70f, scanY - 3.2f), new Vector2(radius * 0.70f, scanY - 3.2f), new Color(0.90f, 0.42f, 1.00f, 0.20f), 1.0f);
    }

    private void DrawSlowOverlay(float ringRadius, float bodyRadius)
    {
        if (!IsSlowed) return;

        DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 32, new Color(0.20f, 0.85f, 1.00f, 0.90f), 2.5f);
        bool heavyProfile = bodyRadius >= 15f;
        float fillAlpha = heavyProfile ? 0.05f : 0.08f;
        DrawCircle(Vector2.Zero, bodyRadius + 3.6f, new Color(0.62f, 0.86f, 1.00f, fillAlpha));

        if (heavyProfile)
        {
            // Armored walkers looked "bubbly" with large chill circles; use frosty streaks instead.
            for (int i = 1; i <= 3; i++)
            {
                float k = i / 3f;
                float x = -4.0f - i * (4.2f + _thrustPulse * 1.2f);
                float y = (i % 2 == 0 ? -1.1f : 1.1f) * k;
                float halfLen = 1.8f + (1f - k) * 2.2f;
                float width = 0.9f + (1f - k) * 0.6f;
                Color streak = new Color(0.70f, 0.92f, 1.00f, 0.16f - k * 0.04f);
                DrawLine(new Vector2(x - halfLen, y), new Vector2(x + halfLen, y), streak, width);
            }
        }
        else
        {
            for (int i = 1; i <= 3; i++)
            {
                float k = i / 3f;
                float x = -3f - i * (3.8f + _thrustPulse * 1.1f);
                float y = (i % 2 == 0 ? -1.2f : 1.0f) * k;
                float r = (bodyRadius * (0.36f - k * 0.08f)) + 1.4f;
                DrawCircle(new Vector2(x, y), r, new Color(0.70f, 0.92f, 1.00f, 0.20f - k * 0.08f));
            }
        }
    }

    private void DrawNearDeathOverlay(float radius)
    {
        if (_nearDeathPulse <= 0.001f) return;

        float coreRadius = radius * (0.26f + _nearDeathPulse * 0.20f);
        float ringRadius = radius * (0.72f + _nearDeathPulse * 0.42f);
        DrawCircle(Vector2.Zero, coreRadius, new Color(1.00f, 0.24f, 0.42f, 0.14f + _nearDeathPulse * 0.30f));
        DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 24, new Color(1.00f, 0.24f, 0.42f, 0.32f + _nearDeathPulse * 0.30f), 1.3f);

        if (_nearDeathFlicker > 0.78f)
        {
            float flashAlpha = ((_nearDeathFlicker - 0.78f) / 0.22f) * 0.35f;
            DrawCircle(new Vector2(0f, -1f), radius * 0.18f, new Color(1f, 1f, 1f, flashAlpha));
        }
    }

    private static Color ResolveHpBandColor(float hpRatio)
    {
        if (hpRatio > 0.75f) return new Color(0.15f, 0.90f, 0.25f); // green
        if (hpRatio > 0.50f) return new Color(0.95f, 0.88f, 0.18f); // yellow
        if (hpRatio > 0.25f) return new Color(0.98f, 0.56f, 0.14f); // orange
        return new Color(0.90f, 0.15f, 0.10f); // red
    }

    // Keep current crack visibility as the "max look" below 25% HP,
    // then smoothly attenuate visibility as HP rises above that.
    private static float ResolveCrackVisibilityScale(float hpRatio)
    {
        float clamped = Mathf.Clamp(hpRatio, 0f, 1f);
        if (clamped <= 0.25f)
            return 1f;

        float t = (clamped - 0.25f) / 0.75f;
        return Mathf.Lerp(1f, 0.14f, t);
    }

    private void DrawBodyPassBasic(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        EnemyRenderDebugCounters.RegisterBodyPass();

        DrawPolygon(RegularPoly(8, 11.6f, Mathf.Pi * 0.12f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(8, 9.2f, Mathf.Pi * 0.12f), new[] { style.BodySecondary });
        DrawCircle(new Vector2(-0.2f, -1.8f), 4.5f, new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.30f));
    }

    private void DrawDamagePassBasic(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        if (rs.DamageBand == EnemyDamageBand.Healthy)
            return;

        EnemyRenderDebugCounters.RegisterDamagePass();
        float visibility = ResolveCrackVisibilityScale(rs.HpRatio);
        float wear = (0.54f + rs.DamageIntensity * 0.42f + rs.HitFlash * 0.26f) * visibility;
        float widthScale = Mathf.Lerp(0.42f, 1f, visibility);
        var fissureDark = new Color(0.05f, 0.01f, 0.02f, Mathf.Clamp(wear * 0.88f, 0f, 0.95f));
        Color hpBandTint = ResolveHpBandColor(rs.HpRatio);
        Color crackRgb = style.DamageTint.Lerp(hpBandTint, 0.58f).Lerp(Colors.White, 0.24f * visibility);
        var crack = new Color(crackRgb.R, crackRgb.G, crackRgb.B, Mathf.Clamp(wear * 0.96f, 0f, 0.98f));

        DrawLine(new Vector2(-5.8f, -3.4f), new Vector2(5.1f, 1.3f), fissureDark, 2.8f * widthScale);
        DrawLine(new Vector2(-2.1f, 4.2f), new Vector2(5.8f, -2.9f), fissureDark, 2.5f * widthScale);
        DrawLine(new Vector2(-6.2f, 0.8f), new Vector2(2.0f, -5.6f), new Color(fissureDark.R, fissureDark.G, fissureDark.B, fissureDark.A * 0.86f), 2.2f * widthScale);

        DrawLine(new Vector2(-5.8f, -3.4f), new Vector2(5.1f, 1.3f), crack, 1.5f * widthScale);
        DrawLine(new Vector2(-2.1f, 4.2f), new Vector2(5.8f, -2.9f), crack, 1.3f * widthScale);
        DrawLine(new Vector2(-6.2f, 0.8f), new Vector2(2.0f, -5.6f), new Color(crack.R, crack.G, crack.B, crack.A * 0.82f), 1.1f * widthScale);
        if (rs.DamageBand == EnemyDamageBand.Critical)
        {
            DrawCircle(new Vector2(2.2f, -5.3f), 1.2f, new Color(style.DamageTint.R, style.DamageTint.G, style.DamageTint.B, wear));
            DrawArc(Vector2.Zero, 8.1f, Mathf.Pi * 0.12f, Mathf.Pi * 0.80f, 12, crack, 2.2f * widthScale);
        }
    }

    private void DrawEmissivePassBasic(in EnemyRenderStyle style, in EnemyRenderState rs, float widthScale)
    {
        EnemyRenderDebugCounters.RegisterEmissivePass();
        float e = Mathf.Clamp(0.44f + rs.EmissivePulse * 0.30f, 0f, 1f);
        float lineWidth = 1.3f * widthScale;
        float accentWidth = 1.2f * widthScale;

        DrawArc(new Vector2(0f, -1.5f), 6.2f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 12, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.70f * e), lineWidth);
        DrawArc(new Vector2(0f, 1.5f), 6.2f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 12, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.70f * e), lineWidth);

        DrawLine(new Vector2(-1.8f, -7.8f), new Vector2(-4.2f, -11f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.85f * e), accentWidth);
        DrawLine(new Vector2(1.8f, -7.8f), new Vector2(4.2f, -11f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.85f * e), accentWidth);
        DrawCircle(Vector2.Zero, 2.1f + rs.HitFlash * 0.4f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.82f * e));
    }

    private void DrawBloomPassBasic(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float bloomAlpha = (0.10f + rs.EmissivePulse * 0.06f + rs.HitFlash * 0.16f) * alphaScale;
        DrawCircle(Vector2.Zero, 18.5f, new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha * 0.45f));
        DrawCircle(new Vector2(-11.5f - _thrustPulse * 1.6f, 0.5f), 2.8f + _thrustPulse * 1.3f, new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha));
        EnemyRenderDebugCounters.RegisterBloomPass(2);
    }

    private void DrawBloomFallbackBasic(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float fallbackAlpha = (0.08f + rs.EmissivePulse * 0.06f + rs.HitFlash * 0.05f) * alphaScale;
        DrawCircle(Vector2.Zero, 10.8f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, fallbackAlpha * 0.52f));
        DrawCircle(new Vector2(-10.2f - _thrustPulse * 1.2f, 0.5f), 2.2f + _thrustPulse * 0.8f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, fallbackAlpha * 0.92f));
        EnemyRenderDebugCounters.RegisterBloomFallback(2);
    }

    private void DrawBodyPassSwift(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        EnemyRenderDebugCounters.RegisterBodyPass();

        float finStretch = 1.0f + _thrustPulse * 0.55f;
        var outerBody = new[]
        {
            new Vector2(12.5f, 0f),
            new Vector2(0f, -9.2f),
            new Vector2(-13f, -4.0f),
            new Vector2(-9.0f, 0f),
            new Vector2(-13f, 4.0f),
            new Vector2(0f, 9.2f),
        };
        DrawPolygon(outerBody, new[] { style.BodyPrimary });

        var coreBody = new[]
        {
            new Vector2(8.6f, 0f),
            new Vector2(0f, -5.8f),
            new Vector2(-8.8f, 0f),
            new Vector2(0f, 5.8f),
        };
        DrawPolygon(coreBody, new[] { style.BodySecondary });

        DrawPolygon(new[]
        {
            new Vector2(-3f, -4f),
            new Vector2(-12.5f * finStretch, -10f),
            new Vector2(-7f, -1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });
        DrawPolygon(new[]
        {
            new Vector2(-3f, 4f),
            new Vector2(-12.5f * finStretch, 10f),
            new Vector2(-7f, 1.6f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.72f) });
    }

    private void DrawDamagePassSwift(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        if (rs.DamageBand == EnemyDamageBand.Healthy)
            return;

        EnemyRenderDebugCounters.RegisterDamagePass();
        float visibility = ResolveCrackVisibilityScale(rs.HpRatio);
        float wear = (0.52f + rs.DamageIntensity * 0.44f + rs.HitFlash * 0.24f) * visibility;
        float widthScale = Mathf.Lerp(0.42f, 1f, visibility);
        var fissureDark = new Color(0.05f, 0.01f, 0.02f, Mathf.Clamp(wear * 0.88f, 0f, 0.95f));
        Color hpBandTint = ResolveHpBandColor(rs.HpRatio);
        Color crackRgb = style.DamageTint.Lerp(hpBandTint, 0.58f).Lerp(Colors.White, 0.22f * visibility);
        var crack = new Color(crackRgb.R, crackRgb.G, crackRgb.B, Mathf.Clamp(wear * 0.96f, 0f, 0.98f));

        DrawLine(new Vector2(-5.4f, -2.1f), new Vector2(5.1f, 0.4f), fissureDark, 2.6f * widthScale);
        DrawLine(new Vector2(-6.4f, 3.0f), new Vector2(2.2f, -2.6f), fissureDark, 2.3f * widthScale);
        DrawLine(new Vector2(-2.8f, 4.6f), new Vector2(7.2f, -1.8f), new Color(fissureDark.R, fissureDark.G, fissureDark.B, fissureDark.A * 0.82f), 2.0f * widthScale);

        DrawLine(new Vector2(-5.4f, -2.1f), new Vector2(5.1f, 0.4f), crack, 1.4f * widthScale);
        DrawLine(new Vector2(-6.4f, 3.0f), new Vector2(2.2f, -2.6f), crack, 1.2f * widthScale);
        DrawLine(new Vector2(-2.8f, 4.6f), new Vector2(7.2f, -1.8f), new Color(crack.R, crack.G, crack.B, crack.A * 0.76f), 1.1f * widthScale);
        if (rs.DamageBand == EnemyDamageBand.Critical)
        {
            DrawLine(new Vector2(-10.4f, -0.8f), new Vector2(-13.8f, -6.2f), crack, 2.0f * widthScale);
            DrawLine(new Vector2(-10.4f, 0.8f), new Vector2(-13.8f, 6.2f), crack, 2.0f * widthScale);
        }
    }

    private void DrawEmissivePassSwift(in EnemyRenderStyle style, in EnemyRenderState rs, float widthScale)
    {
        EnemyRenderDebugCounters.RegisterEmissivePass();
        float e = Mathf.Clamp(0.44f + rs.EmissivePulse * 0.34f, 0f, 1f);
        float lineWidth = 1.35f * widthScale;

        var outerBody = new[]
        {
            new Vector2(12.5f, 0f),
            new Vector2(0f, -9.2f),
            new Vector2(-13f, -4.0f),
            new Vector2(-9.0f, 0f),
            new Vector2(-13f, 4.0f),
            new Vector2(0f, 9.2f),
        };
        for (int i = 0; i < outerBody.Length; i++)
        {
            Vector2 a = outerBody[i];
            Vector2 b = outerBody[(i + 1) % outerBody.Length];
            DrawLine(a, b, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.88f * e), lineWidth);
        }

        DrawCircle(new Vector2(1.8f, 0f), 2.2f + rs.HitFlash * 0.3f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.90f * e));
    }

    private void DrawBloomPassSwift(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float bloomAlpha = (0.11f + rs.EmissivePulse * 0.07f + rs.HitFlash * 0.16f) * alphaScale;
        DrawCircle(Vector2.Zero, 15f, new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha * 0.58f));
        DrawLine(new Vector2(-9f, -2.8f), new Vector2(-16f * (1.0f + _thrustPulse * 0.45f), -5.2f), new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha), 1.3f);
        DrawLine(new Vector2(-9f, 2.8f), new Vector2(-16f * (1.0f + _thrustPulse * 0.45f), 5.2f), new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha), 1.3f);
        EnemyRenderDebugCounters.RegisterBloomPass(3);
    }

    private void DrawBloomFallbackSwift(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float fallbackAlpha = (0.09f + rs.EmissivePulse * 0.06f + rs.HitFlash * 0.05f) * alphaScale;
        DrawCircle(Vector2.Zero, 10.8f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, fallbackAlpha * 0.58f));
        DrawLine(new Vector2(-9f, -2.8f), new Vector2(-14f * (1.0f + _thrustPulse * 0.30f), -4.6f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, fallbackAlpha), 1.0f);
        DrawLine(new Vector2(-9f, 2.8f), new Vector2(-14f * (1.0f + _thrustPulse * 0.30f), 4.6f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, fallbackAlpha), 1.0f);
        EnemyRenderDebugCounters.RegisterBloomFallback(3);
    }

    private void DrawBodyPassArmored(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        EnemyRenderDebugCounters.RegisterBodyPass();

        DrawPolygon(RegularPoly(6, 17.6f, 0f), new[] { new Color(0.08f, 0.02f, 0.03f) });
        DrawPolygon(RegularPoly(6, 14.9f, 0f), new[] { new Color(0.30f, 0.06f, 0.07f) });
        DrawPolygon(RegularPoly(6, 12.1f, 0f), new[] { style.BodyPrimary });
        DrawPolygon(RegularPoly(6, 8.8f, 0f), new[] { style.BodySecondary });

        DrawPolygon(new[]
        {
            new Vector2(-9.8f, -4.2f),
            new Vector2(-15.4f, -0.6f),
            new Vector2(-9.8f, 1.2f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.84f) });
        DrawPolygon(new[]
        {
            new Vector2(9.8f, -4.2f),
            new Vector2(15.4f, -0.6f),
            new Vector2(9.8f, 1.2f),
        }, new[] { new Color(style.BodyPrimary.R, style.BodyPrimary.G, style.BodyPrimary.B, 0.84f) });
    }

    private void DrawDamagePassArmored(in EnemyRenderStyle style, in EnemyRenderState rs)
    {
        if (rs.DamageBand == EnemyDamageBand.Healthy)
            return;

        EnemyRenderDebugCounters.RegisterDamagePass();
        float visibility = ResolveCrackVisibilityScale(rs.HpRatio);
        float wear = (0.56f + rs.DamageIntensity * 0.40f + rs.HitFlash * 0.26f) * visibility;
        float widthScale = Mathf.Lerp(0.42f, 1f, visibility);
        var fissureDark = new Color(0.05f, 0.01f, 0.02f, Mathf.Clamp(wear * 0.88f, 0f, 0.95f));
        Color hpBandTint = ResolveHpBandColor(rs.HpRatio);
        Color crackRgb = style.DamageTint.Lerp(hpBandTint, 0.58f).Lerp(Colors.White, 0.18f * visibility);
        var crack = new Color(crackRgb.R, crackRgb.G, crackRgb.B, Mathf.Clamp(wear * 0.96f, 0f, 0.98f));

        var plateCenters = new[]
        {
            new Vector2(-5.6f, -6.1f),
            new Vector2(0f,   -7.4f),
            new Vector2(5.6f, -6.1f),
            new Vector2(-5.6f, 6.1f),
            new Vector2(5.6f,  6.1f),
        };
        int totalPlates = plateCenters.Length;
        int intact = Mathf.Clamp(Mathf.CeilToInt(rs.HpRatio * totalPlates), 0, totalPlates);
        for (int i = 0; i < totalPlates; i++)
        {
            Vector2 c = plateCenters[i];
            if (i < intact)
            {
                float plateWear = 0.42f + rs.DamageIntensity * 0.28f;
                DrawPolygon(new[]
                {
                    c + new Vector2(-2.4f, -1.7f),
                    c + new Vector2( 2.4f, -1.7f),
                    c + new Vector2( 2.4f,  1.7f),
                    c + new Vector2(-2.4f,  1.7f),
                }, new[] { new Color(0.96f, 0.34f, 0.22f, plateWear) });
                DrawLine(c + new Vector2(-2.2f, -0.7f), c + new Vector2(2.0f, 0.9f), new Color(fissureDark.R, fissureDark.G, fissureDark.B, fissureDark.A * 0.72f), 2.0f * widthScale);
                DrawLine(c + new Vector2(-2.2f, -0.7f), c + new Vector2(2.0f, 0.9f), new Color(crack.R, crack.G, crack.B, crack.A * 0.62f), 1.3f * widthScale);
            }
            else
            {
                DrawLine(c + new Vector2(-2.5f, -1.7f), c + new Vector2(2.4f, 1.7f), fissureDark, 2.7f * widthScale);
                DrawLine(c + new Vector2(-2.5f, -1.7f), c + new Vector2(2.4f, 1.7f), crack, 1.8f * widthScale);
                DrawCircle(c + new Vector2(2.7f, -1.8f), 1.1f, new Color(crack.R, crack.G, crack.B, crack.A * 0.82f));
            }
        }

        if (rs.DamageBand == EnemyDamageBand.Critical)
            DrawArc(Vector2.Zero, 10.4f, Mathf.Pi * 0.08f, Mathf.Pi * 1.86f, 20, crack, 2.3f * widthScale);
    }

    private void DrawEmissivePassArmored(in EnemyRenderStyle style, in EnemyRenderState rs, float widthScale)
    {
        EnemyRenderDebugCounters.RegisterEmissivePass();
        float e = Mathf.Clamp(0.42f + rs.EmissivePulse * 0.34f, 0f, 1f);
        float rimWidth = 1.5f * widthScale;
        float visorWidth = 1.0f * widthScale;

        var outerRimPts = RegularPoly(6, 16.1f, 0f);
        for (int i = 0; i < outerRimPts.Length; i++)
        {
            Vector2 a = outerRimPts[i];
            Vector2 b = outerRimPts[(i + 1) % outerRimPts.Length];
            DrawLine(a, b, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.54f * e), rimWidth);
        }

        DrawRect(new Rect2(-5.6f, -2.2f, 11.2f, 4.4f), new Color(0.09f, 0.02f, 0.03f, 0.95f), true);
        DrawRect(new Rect2(-4.2f, -1.0f, 8.4f, 2.0f), new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, 0.78f * e), true);
        DrawLine(new Vector2(-1.2f, -1.0f), new Vector2(-1.2f, 1.0f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.56f * e), visorWidth);
        DrawLine(new Vector2(1.2f, -1.0f), new Vector2(1.2f, 1.0f), new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, 0.56f * e), visorWidth);
    }

    private void DrawBloomPassArmored(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float bloomAlpha = (0.10f + rs.EmissivePulse * 0.06f + rs.HitFlash * 0.16f) * alphaScale;
        DrawCircle(new Vector2(0f, 0.8f), 13.6f, new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha * 0.30f));
        DrawCircle(new Vector2(0f, 0.8f), 9.8f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, bloomAlpha * 0.50f));
        DrawCircle(new Vector2(-13.5f - _thrustPulse * 1.2f, 1.0f), 2.8f + _thrustPulse * 0.9f, new Color(style.BloomTint.R, style.BloomTint.G, style.BloomTint.B, bloomAlpha));
        EnemyRenderDebugCounters.RegisterBloomPass(3);
    }

    private void DrawBloomFallbackArmored(in EnemyRenderStyle style, in EnemyRenderState rs, float alphaScale)
    {
        float fallbackAlpha = (0.08f + rs.EmissivePulse * 0.05f + rs.HitFlash * 0.05f) * alphaScale;
        DrawCircle(new Vector2(0f, 0.8f), 9.8f, new Color(style.Emissive.R, style.Emissive.G, style.Emissive.B, fallbackAlpha * 0.40f));
        DrawCircle(new Vector2(0f, 0.8f), 6.8f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, fallbackAlpha * 0.72f));
        DrawCircle(new Vector2(-12.8f - _thrustPulse * 0.9f, 1.0f), 2.1f + _thrustPulse * 0.65f, new Color(style.EmissiveHot.R, style.EmissiveHot.G, style.EmissiveHot.B, fallbackAlpha));
        EnemyRenderDebugCounters.RegisterBloomFallback(3);
    }

    // Basic = neon beetle drone
    private void DrawBasicWalker()
    {
        var shell = new Color(0.20f, 0.98f, 0.86f);
        var shellDark = new Color(0.02f, 0.17f, 0.18f);
        var core = new Color(0.72f, 1.00f, 0.96f);

        DrawCircle(new Vector2(-11.5f - _thrustPulse * 1.9f, 0.5f), 2.4f + _thrustPulse * 1.8f, new Color(0.20f, 1.00f, 0.92f, 0.24f + _thrustPulse * 0.16f));

        // Glow shell
        DrawCircle(Vector2.Zero, 19f, new Color(shell.R, shell.G, shell.B, 0.10f));
        DrawCircle(Vector2.Zero, 13f, new Color(shell.R, shell.G, shell.B, 0.16f));

        // Modular layers: shell + core + accessories.
        DrawPolygon(RegularPoly(8, 11.5f, Mathf.Pi * 0.12f), new[] { shell });
        DrawPolygon(RegularPoly(8, 9.2f, Mathf.Pi * 0.12f), new[] { shellDark });
        DrawCircle(new Vector2(-0.2f, -1.8f), 4.6f, new Color(core.R, core.G, core.B, 0.72f));

        // Segmented beetle shell lines.
        DrawArc(new Vector2(0f, -1.5f), 6.2f, Mathf.Pi * 0.15f, Mathf.Pi * 0.85f, 12, new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);
        DrawArc(new Vector2(0f, 1.5f), 6.2f, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 12, new Color(0.08f, 0.72f, 0.65f, 0.80f), 1.3f);

        // Antennae accessories.
        DrawLine(new Vector2(-1.8f, -7.8f), new Vector2(-4.2f, -11f), new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawLine(new Vector2(1.8f, -7.8f), new Vector2(4.2f, -11f), new Color(0.65f, 1.00f, 0.92f, 0.90f), 1.2f);
        DrawCircle(new Vector2(-4.2f, -11f), 1.0f, Colors.White);
        DrawCircle(new Vector2(4.2f, -11f), 1.0f, Colors.White);

        DrawMarkedOverlay(13f);
        DrawSlowOverlay(15.5f, 11f);
        DrawNearDeathOverlay(11f);
    }

    // Swift = razor ray / dart eel
    private void DrawSwiftWalker()
    {
        var edge = new Color(0.70f, 1.00f, 0.24f);
        var bodyDark = new Color(0.05f, 0.12f, 0.03f);
        var glow = new Color(0.86f, 1.00f, 0.65f);

        float finStretch = 1.0f + _thrustPulse * 0.55f;

        DrawLine(new Vector2(-9f, -2.8f), new Vector2(-16f * finStretch, -5.2f), new Color(0.74f, 1.00f, 0.35f, 0.52f), 1.2f);
        DrawLine(new Vector2(-9f, 2.8f), new Vector2(-16f * finStretch, 5.2f), new Color(0.74f, 1.00f, 0.35f, 0.52f), 1.2f);

        DrawCircle(Vector2.Zero, 15f, new Color(edge.R, edge.G, edge.B, 0.10f));

        // Core ray body.
        var outerBody = new[]
        {
            new Vector2(12.5f, 0f),
            new Vector2(0f, -9.2f),
            new Vector2(-13f, -4.0f),
            new Vector2(-9.0f, 0f),
            new Vector2(-13f, 4.0f),
            new Vector2(0f, 9.2f),
        };
        DrawPolygon(outerBody, new[] { edge });

        var coreBody = new[]
        {
            new Vector2(8.6f, 0f),
            new Vector2(0f, -5.8f),
            new Vector2(-8.8f, 0f),
            new Vector2(0f, 5.8f),
        };
        DrawPolygon(coreBody, new[] { bodyDark });

        // Bright yellow border accent to improve swift target readability.
        for (int i = 0; i < outerBody.Length; i++)
        {
            Vector2 a = outerBody[i];
            Vector2 b = outerBody[(i + 1) % outerBody.Length];
            DrawLine(a, b, new Color(1.00f, 0.94f, 0.30f, 0.90f), 1.35f);
        }
        for (int i = 0; i < coreBody.Length; i++)
        {
            Vector2 a = coreBody[i];
            Vector2 b = coreBody[(i + 1) % coreBody.Length];
            DrawLine(a, b, new Color(1.00f, 0.82f, 0.22f, 0.62f), 1.0f);
        }

        // Fins and tail thrusters.
        DrawPolygon(new[]
        {
            new Vector2(-3f, -4f),
            new Vector2(-12.5f * finStretch, -10f),
            new Vector2(-7f, -1.6f),
        }, new[] { new Color(0.62f, 1.00f, 0.32f, 0.82f) });
        DrawPolygon(new[]
        {
            new Vector2(-3f, 4f),
            new Vector2(-12.5f * finStretch, 10f),
            new Vector2(-7f, 1.6f),
        }, new[] { new Color(0.62f, 1.00f, 0.32f, 0.82f) });

        DrawCircle(new Vector2(1.8f, 0f), 2.2f, glow);

        DrawMarkedOverlay(13f);
        DrawSlowOverlay(14.5f, 11f);
        DrawNearDeathOverlay(10f);
    }

    // Armored = plated rhino core
    private void DrawArmoredWalker()
    {
        var rim = new Color(0.08f, 0.02f, 0.03f);
        var shellA = new Color(0.30f, 0.06f, 0.07f);
        var shellB = new Color(0.82f, 0.26f, 0.10f);
        var coreDark = new Color(0.12f, 0.02f, 0.03f);

        float exhaust = 0.18f + _thrustPulse * 0.24f;
        DrawCircle(
            new Vector2(-13.5f - _thrustPulse * 1.3f, 1.0f),
            2.9f + _thrustPulse * 1.0f,
            new Color(1.00f, 0.45f, 0.24f, exhaust));

        // Stronger glow to separate from the magenta path colors.
        DrawCircle(Vector2.Zero, 19.4f, new Color(1.00f, 0.46f, 0.22f, 0.09f));
        DrawCircle(Vector2.Zero, 14.8f, new Color(1.00f, 0.46f, 0.22f, 0.14f));

        // Main heavy shell layers.
        DrawPolygon(RegularPoly(6, 17.6f, 0f), new[] { rim });
        DrawPolygon(RegularPoly(6, 14.9f, 0f), new[] { shellA });
        DrawPolygon(RegularPoly(6, 12.1f, 0f), new[] { shellB });
        DrawPolygon(RegularPoly(6, 8.8f, 0f), new[] { coreDark });

        // Neon accent border so armored units read distinctly from regular enemies.
        var outerRimPts = RegularPoly(6, 16.1f, 0f);
        for (int i = 0; i < outerRimPts.Length; i++)
        {
            Vector2 a = outerRimPts[i];
            Vector2 b = outerRimPts[(i + 1) % outerRimPts.Length];
            DrawLine(a, b, new Color(0.96f, 0.30f, 0.46f, 0.70f), 1.55f);
        }

        var innerRimPts = RegularPoly(6, 13.4f, 0f);
        for (int i = 0; i < innerRimPts.Length; i++)
        {
            Vector2 a = innerRimPts[i];
            Vector2 b = innerRimPts[(i + 1) % innerRimPts.Length];
            DrawLine(a, b, new Color(1.00f, 0.46f, 0.18f, 0.52f), 1.2f);
        }

        // Side armor ears for a more unique silhouette.
        DrawPolygon(new[]
        {
            new Vector2(-9.8f, -4.2f),
            new Vector2(-15.4f, -0.6f),
            new Vector2(-9.8f, 1.2f),
        }, new[] { new Color(0.88f, 0.28f, 0.18f) });
        DrawPolygon(new[]
        {
            new Vector2(9.8f, -4.2f),
            new Vector2(15.4f, -0.6f),
            new Vector2(9.8f, 1.2f),
        }, new[] { new Color(0.88f, 0.28f, 0.18f) });

        // Short reinforced front prow (less cartoony than the previous long horn).
        DrawPolygon(new[]
        {
            new Vector2(9.8f, -3.2f),
            new Vector2(16.8f, -1.6f),
            new Vector2(16.8f, 1.6f),
            new Vector2(9.8f, 3.2f),
        }, new[] { new Color(0.92f, 0.30f, 0.22f) });
        DrawLine(
            new Vector2(10.2f, 0f),
            new Vector2(16.8f, 0f),
            new Color(1.00f, 0.55f, 0.35f, 0.78f),
            1.6f);

        // Segmented plates: break away as HP drops.
        var plateCenters = new[]
        {
            new Vector2(-5.6f, -6.1f),
            new Vector2(0f,   -7.4f),
            new Vector2(5.6f, -6.1f),
            new Vector2(-5.6f, 6.1f),
            new Vector2(5.6f,  6.1f),
        };
        int totalPlates = plateCenters.Length;
        int intact = Mathf.Clamp(Mathf.CeilToInt(_hpRatio * totalPlates), 0, totalPlates);
        for (int i = 0; i < totalPlates; i++)
        {
            Vector2 c = plateCenters[i];
            if (i < intact)
            {
                DrawPolygon(new[]
                {
                    c + new Vector2(-2.4f, -1.7f),
                    c + new Vector2( 2.4f, -1.7f),
                    c + new Vector2( 2.4f,  1.7f),
                    c + new Vector2(-2.4f,  1.7f),
                }, new[] { new Color(0.88f, 0.24f, 0.20f) });
            }
            else
            {
                DrawLine(
                    c + new Vector2(-2.5f, -1.7f),
                    c + new Vector2( 2.4f,  1.7f),
                    new Color(1.00f, 0.45f, 0.28f, 0.82f),
                    1.5f);
                DrawCircle(c + new Vector2(2.7f, -1.8f), 0.95f, new Color(1.00f, 0.45f, 0.28f, 0.45f));
            }
        }

        // Simple visor strip (replaces cartoon eyes).
        DrawRect(new Rect2(-5.6f, -2.2f, 11.2f, 4.4f), new Color(0.09f, 0.02f, 0.03f, 0.96f), true);
        DrawRect(new Rect2(-4.2f, -1.0f, 8.4f, 2.0f), new Color(1.00f, 0.62f, 0.36f, 0.90f), true);
        DrawLine(new Vector2(-1.2f, -1.0f), new Vector2(-1.2f, 1.0f), new Color(1f, 0.68f, 0.44f, 0.70f), 1.0f);
        DrawLine(new Vector2( 1.2f, -1.0f), new Vector2( 1.2f, 1.0f), new Color(1f, 0.68f, 0.44f, 0.70f), 1.0f);

        DrawMarkedOverlay(19f);
        DrawSlowOverlay(21.5f, 17f);
        DrawNearDeathOverlay(16f);
    }

    public void FlashHit()
    {
        _hitTween?.Kill();
        Scale = _baseScale;
        _hitFlash = 1f;
        _hitTween = CreateTween();
        _hitTween.SetParallel(true);
        _hitTween.TweenProperty(this, "modulate", new Color(2f, 2f, 2f), 0.03f);
        _hitTween.TweenProperty(this, "scale", new Vector2(_baseScale.X * 1.08f, _baseScale.Y * 0.90f), 0.03f);
        _hitTween.SetParallel(false);
        _hitTween.TweenProperty(this, "modulate", Colors.White, 0.15f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        _hitTween.TweenProperty(this, "scale", _baseScale, 0.11f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    private static Vector2[] RegularPoly(int sides, float radius, float angleOffset)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = angleOffset + i * Mathf.Tau / sides;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }
}
