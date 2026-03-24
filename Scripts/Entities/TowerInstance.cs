using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Modifiers;
using SlotTheory.UI;

namespace SlotTheory.Entities;

public enum TargetingMode { First, Strongest, LowestHp, Last }

/// <summary>
/// Tower node. Positioned as a child of its Slot node so GlobalPosition is correct for range checks.
/// </summary>
public partial class TowerInstance : Node2D, ITowerView
{
    public string TowerId { get; set; } = string.Empty;
    public float BaseDamage { get; set; }
    public float AttackInterval { get; set; }
    public float Range { get; set; }
    public bool AppliesMark { get; set; }

    public TargetingMode TargetingMode { get; set; } = TargetingMode.First;
    public float Cooldown { get; set; } = 0f;
    public Color ProjectileColor { get; set; } = Colors.Yellow;
    public Color BodyColor { get; set; } = Colors.White;

    public List<Modifier> Modifiers { get; } = new();
    public string? LastTargetId { get; set; }
    public Vector2? LastTargetPosition { get; set; }

    public int   ChainCount       { get; set; } = 0;
    public float ChainRange       { get; set; } = 400f;
    public float ChainDamageDecay { get; set; } = Balance.ChainDamageDecay;
    public bool  IsChainTower     => ChainCount > 0;
    public int   SplitCount       { get; set; } = 0;

    public bool CanAddModifier => Modifiers.Count < Balance.MaxModifiersPerTower;

    public TargetModeIcon? ModeIconControl { get; set; }
    public ColorRect? ModeBadgeControl { get; set; }
    public Line2D? ModeBadgeBorder { get; set; }
    public Polygon2D? RangeCircle { get; set; }
    public Line2D?   RangeBorder  { get; set; }
    public float SpectacleMeterNormalized { get; set; } = 0f;
    public float SpectaclePulse { get; set; } = 0f;
    public string SpectacleAccent { get; set; } = string.Empty;
    private Tween? _spectacleFlashTween;
    private Tween? _recoilTween;
    private float _idleTime = 0f;
    private float _lockLineRemaining = 0f;
    private Vector2 _lockLineTargetGlobal = Vector2.Zero;
    private float _shotElapsed = 99f;
    private float _spectacleChargePulse = 0f;
    private float _lastSpectacleMeter = 0f;
    private float _teachingHighlightRemaining = 0f;
    private float _teachingHighlightDuration = 0f;
    private const float ShotAttackSeconds = 0.030f;
    private const float ShotDecaySeconds = 0.18f;

    /// <summary>Rebuilds the range circle fill and border to match the tower's current Range value.</summary>
    public void RefreshRangeCircle()
    {
        var pts = new Vector2[65]; // 65th point closes the loop for Line2D
        for (int p = 0; p < 64; p++)
        {
            float a = p * Mathf.Tau / 64;
            pts[p] = new Vector2(Mathf.Cos(a) * Range, Mathf.Sin(a) * Range);
        }
        pts[64] = pts[0];

        if (RangeCircle != null)
            RangeCircle.Polygon = pts[..64];
        if (RangeBorder != null)
            RangeBorder.Points = pts;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _idleTime += dt;
        if (_shotElapsed < ShotAttackSeconds + ShotDecaySeconds)
            _shotElapsed += dt;
        if (_lockLineRemaining > 0f)
            _lockLineRemaining = Mathf.Max(0f, _lockLineRemaining - dt);
        if (_spectacleChargePulse > 0f)
            _spectacleChargePulse = Mathf.Max(0f, _spectacleChargePulse - dt * 1.8f);
        if (_teachingHighlightRemaining > 0f)
            _teachingHighlightRemaining = Mathf.Max(0f, _teachingHighlightRemaining - dt);

        float spectacleMeter = Mathf.Clamp(SpectacleMeterNormalized, 0f, 1f);
        if (spectacleMeter > _lastSpectacleMeter + 0.03f)
            _spectacleChargePulse = Mathf.Clamp(_spectacleChargePulse + 0.42f, 0f, 1f);
        if (spectacleMeter < 0.05f && _lastSpectacleMeter > 0.85f)
            _spectacleChargePulse = Mathf.Max(_spectacleChargePulse, 0.24f);
        _lastSpectacleMeter = spectacleMeter;

        // Smoothly rotate barrel toward last known target
        if (LastTargetPosition.HasValue)
        {
            var dir = LastTargetPosition.Value - GlobalPosition;
            if (dir.LengthSquared() > 0.0001f)
            {
            float targetAngle = dir.Angle() + Mathf.Pi * 0.5f; // barrels point local -Y
                float turnLerp = Mathf.Clamp(15f * dt, 0f, 1f);
                Rotation = Mathf.LerpAngle(Rotation, targetAngle, turnLerp);
            }
        }
        if (AttackInterval > 0f || spectacleMeter > 0f || _spectacleChargePulse > 0f || _teachingHighlightRemaining > 0f)
            QueueRedraw();
    }

    public void TriggerTeachingHighlight(float duration = 4.6f)
    {
        float d = Mathf.Max(0.5f, duration);
        _teachingHighlightDuration = Mathf.Max(_teachingHighlightDuration, d);
        _teachingHighlightRemaining = Mathf.Max(_teachingHighlightRemaining, d);
    }

    public void FlashAttack()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", new Color(1.4f, 1.4f, 1.4f), 0.03f);
        tween.TweenProperty(this, "modulate", Colors.White, 0.25f)
             .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    public void FlashSpectacle(Color accent, bool major)
    {
        if (_spectacleFlashTween != null && GodotObject.IsInstanceValid(_spectacleFlashTween))
            _spectacleFlashTween.Kill();

        float tintBoost = major ? 0.62f : 0.40f;
        Color flash = new Color(
            1f + accent.R * tintBoost,
            1f + accent.G * tintBoost,
            1f + accent.B * tintBoost,
            1f);
        Vector2 peakScale = Vector2.One * (major ? 1.11f : 1.07f);
        float inTime = major ? 0.055f : 0.045f;
        float outTime = major ? 0.22f : 0.16f;

        _spectacleFlashTween = CreateTween();
        _spectacleFlashTween.SetParallel(true);
        _spectacleFlashTween.TweenProperty(this, "modulate", flash, inTime);
        _spectacleFlashTween.TweenProperty(this, "scale", peakScale, inTime)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _spectacleFlashTween.Chain().SetParallel(true);
        _spectacleFlashTween.TweenProperty(this, "modulate", Colors.White, outTime)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        _spectacleFlashTween.TweenProperty(this, "scale", Vector2.One, outTime)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void StartAfterGlow(Color accent, float duration = 2.4f)
    {
        if (_spectacleFlashTween != null && GodotObject.IsInstanceValid(_spectacleFlashTween))
            _spectacleFlashTween.Kill();
        // Blend white toward accent so values stay in [0,1] - visible as a colored tint
        Color glow = new Color(
            0.45f + accent.R * 0.55f,
            0.45f + accent.G * 0.55f,
            0.45f + accent.B * 0.55f,
            1f);
        _spectacleFlashTween = CreateTween();
        _spectacleFlashTween.TweenProperty(this, "modulate", glow, 0.08f);
        _spectacleFlashTween.TweenProperty(this, "modulate", Colors.White, Mathf.Max(0.5f, duration))
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    public void KickRecoil(float distance = 3.5f)
    {
        if (!LastTargetPosition.HasValue) return;

        var dir = LastTargetPosition.Value - GlobalPosition;
        if (dir.LengthSquared() <= 0.0001f)
            return;

        if (_recoilTween != null && GodotObject.IsInstanceValid(_recoilTween))
            _recoilTween.Kill();

        var kickOffset = -dir.Normalized() * distance;
        _recoilTween = CreateTween();
        _recoilTween.TweenProperty(this, "position", kickOffset, 0.032f)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _recoilTween.TweenProperty(this, "position", Vector2.Zero, 0.075f)
             .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _recoilTween.TweenCallback(Callable.From(() => _recoilTween = null));
    }

    public void OnShotFired(EnemyInstance target)
    {
        ulong id = target.GetInstanceId();
        string idStr = id.ToString();
        LastTargetPosition = target.GlobalPosition;
        _shotElapsed = 0f;
        if (LastTargetId == idStr)
        {
            _lockLineTargetGlobal = target.GlobalPosition;
            _lockLineRemaining = 0.15f;
        }
        LastTargetId = idStr;
    }

    /// <summary>
    /// Computes the effective damage for tooltip display by applying unconditional damage modifiers.
    /// Conditional modifiers (e.g., ExploitWeakness vs Marked enemies) are conservatively omitted.
    /// </summary>
    public float GetEffectiveDamageForPreview()
    {
        float damage = BaseDamage;

        foreach (var mod in Modifiers)
        {
            // Skip modifiers that require target state to compute their effect.
            // These would need a specific enemy to evaluate correctly.
            if (mod is ExploitWeakness || mod is Momentum)
                continue;

            try
            {
                // Create a minimal context for unconditional damage modifiers.
                // Modifiers like FocusLens only care about the tower, not the target.
                var ctx = new Combat.DamageContext(this, null!, 0, new List<Entities.IEnemyView>());
                mod.ModifyDamage(ref damage, ctx);
            }
            catch
            {
                // Gracefully skip modifiers that fail to compute their preview effect.
            }
        }

        return damage;
    }

    public void CycleTargetingMode()
    {
        // Phase Splitter always hits front + back simultaneously; targeting mode has no effect.
        if (TowerId == "phase_splitter") return;

        // Rift Sapper has 3 custom targeting semantics (Random/Closest/Furthest); Last is not applicable.
        bool isRiftSapper = TowerId == "rift_prism";
        TargetingMode = TargetingMode switch
        {
            TargetingMode.First     => TargetingMode.Strongest,
            TargetingMode.Strongest => TargetingMode.LowestHp,
            TargetingMode.LowestHp  => isRiftSapper ? TargetingMode.First : TargetingMode.Last,
            _                       => TargetingMode.First,
        };
        if (ModeIconControl != null)
            ModeIconControl.Mode = TargetingMode;
    }

    public override void _Draw()
    {
        // Draw cooldown ring first so tower/barrel geometry sits on top.
        DrawChargeArc();
        DrawSpectacleArc();
        DrawTeachingHighlight();

        switch (TowerId)
        {
            case "rapid_shooter":    DrawRapidShooter();    break;
            case "heavy_cannon":     DrawHeavyCannon();     break;
            case "marker_tower":     DrawMarkerTower();     break;
            case "chain_tower":      DrawChainTower();      break;
            case "rift_prism":       DrawRiftSapper();      break;
            case "accordion_engine": DrawAccordionEngine(); break;
            case "phase_splitter":   DrawPhaseSplitter();   break;
            default: DrawCircle(Vector2.Zero, 10f, new Color(0.2f, 0.5f, 1.0f)); break;
        }
        DrawReadabilityAccent();
        DrawTargetLockLine();
    }

    private void DrawTargetLockLine()
    {
        if (_lockLineRemaining <= 0f) return;
        var localTo = ToLocal(_lockLineTargetGlobal);
        float t = _lockLineRemaining / 0.15f;
        var c = new Color(BodyColor.R, BodyColor.G, BodyColor.B, 0.10f + 0.20f * t);
        DrawLine(Vector2.Zero, localTo, c, 1.6f);
    }

    private void DrawChargeArc()
    {
        if (AttackInterval <= 0f) return;
        float fill   = Mathf.Clamp(1f - Cooldown / AttackInterval, 0f, 1f);
        const float r = 21f;

        // When full (mine cap reached or pre-fire), draw the background ring brighter
        // and skip the charge arc: drawing from -π/2 to -π/2+2π causes a seam artifact
        // at the top where the arc start and end points coincide.
        bool atFull = fill >= 0.999f;
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 48,
            new Color(BodyColor.R, BodyColor.G, BodyColor.B, atFull ? 0.50f : 0.16f), 2f);

        // Filled charge arc - clockwise from top (only when not fully charged)
        if (fill > 0.01f && !atFull)
        {
            float start = -Mathf.Pi / 2f;
            float end   = start + fill * Mathf.Tau;
            DrawArc(Vector2.Zero, r, start, end, 48,
                new Color(BodyColor.R, BodyColor.G, BodyColor.B, 0.88f), 2.5f);
        }
    }

    private void DrawSpectacleArc()
    {
        float meter = Mathf.Clamp(SpectacleMeterNormalized, 0f, 1f);
        float pulse = Mathf.Clamp(SpectaclePulse, 0f, 1f);
        float chargePulse = Mathf.Clamp(_spectacleChargePulse, 0f, 1f);
        if (meter <= 0.001f && pulse <= 0.001f && chargePulse <= 0.001f)
            return;

        Color accent = string.IsNullOrEmpty(SpectacleAccent)
            ? BodyColor
            : ModifierVisuals.GetAccent(SpectacleAccent);
        float nearFull = Mathf.InverseLerp(0.72f, 1f, meter);
        float readyPulse = meter >= 0.98f ? (0.5f + 0.5f * Mathf.Sin(_idleTime * 8.5f)) : 0f;
        float glow = 0.08f + meter * 0.26f + pulse * 0.42f + chargePulse * 0.22f + nearFull * 0.10f + readyPulse * 0.20f;
        float glowRadius = 28f + pulse * 3.0f + nearFull * 1.6f + readyPulse * 2.0f;
        DrawCircle(Vector2.Zero, glowRadius, new Color(accent.R, accent.G, accent.B, glow * 0.62f));

        float ringAlpha = 0.24f + meter * 0.42f + pulse * 0.40f + chargePulse * 0.12f;
        float baseRingWidth = 2.1f + nearFull * 0.8f;
        DrawArc(Vector2.Zero, 24f, 0f, Mathf.Tau, 48, new Color(accent.R, accent.G, accent.B, ringAlpha * (0.40f + nearFull * 0.24f)), baseRingWidth);

        if (meter > 0.01f)
        {
            float start = -Mathf.Pi / 2f;
            float end = start + meter * Mathf.Tau;
            DrawArc(Vector2.Zero, 24f, start, end, 48, new Color(accent.R, accent.G, accent.B, ringAlpha), 3.0f + nearFull * 1.2f + chargePulse * 0.6f);
        }

        if (nearFull > 0.001f)
        {
            float shimmer = 0.40f + 0.60f * (0.5f + 0.5f * Mathf.Sin(_idleTime * 10f));
            float alpha = nearFull * (0.12f + 0.16f * shimmer);
            DrawArc(Vector2.Zero, 27.5f, 0f, Mathf.Tau, 48, new Color(accent.R, accent.G, accent.B, alpha), 1.6f);
        }
    }

    private void DrawTeachingHighlight()
    {
        if (_teachingHighlightRemaining <= 0f)
            return;

        float life = _teachingHighlightDuration > 0.001f
            ? Mathf.Clamp(_teachingHighlightRemaining / _teachingHighlightDuration, 0f, 1f)
            : 1f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 10f);
        float alpha = (0.22f + 0.38f * pulse) * Mathf.Clamp(life * 1.25f, 0.30f, 1f);
        var color = new Color(1.00f, 0.94f, 0.58f, alpha);
        DrawCircle(Vector2.Zero, 34.5f + pulse * 1.5f, new Color(color.R, color.G, color.B, alpha * 0.18f));
        DrawArc(Vector2.Zero, 31.5f + pulse * 0.8f, 0f, Mathf.Tau, 56, color, 3.1f);
    }

    private float ShotKick()
    {
        float total = ShotAttackSeconds + ShotDecaySeconds;
        if (_shotElapsed >= total) return 0f;
        if (_shotElapsed <= ShotAttackSeconds)
            return Mathf.Clamp(_shotElapsed / ShotAttackSeconds, 0f, 1f);

        float decayT = (_shotElapsed - ShotAttackSeconds) / ShotDecaySeconds;
        float k = 1f - decayT;
        return Mathf.Pow(Mathf.Clamp(k, 0f, 1f), 0.65f);
    }

    private void DrawRapidShooter()
    {
        var cyan  = new Color(0.10f, 0.75f, 1.00f);
        var dark  = LiftInnerTone(new Color(0.02f, 0.02f, 0.12f));
        var flash = new Color(0.70f, 0.95f, 1.00f);
        float shot = ShotKick();
        float sway = Mathf.Sin(_idleTime * 2.7f) * 1.2f;
        float barrelBack = shot * 2.2f;
        float muzzleBoost = 1f + shot * 0.72f;
        // Soft glow behind everything
        DrawCircle(Vector2.Zero, 22f, new Color(cyan.R, cyan.G, cyan.B, 0.07f));
        DrawCircle(Vector2.Zero, 15f, new Color(cyan.R, cyan.G, cyan.B, 0.14f));
        // Barrel ΓÇö bright outer, dark inner
        DrawRect(new Rect2(-3.5f + sway, -23f + barrelBack, 7f, 18f), cyan);
        DrawRect(new Rect2(-2.0f + sway, -22f + barrelBack, 4f, 16f), dark);
        // Muzzle glow
        DrawCircle(new Vector2(sway, -23f + barrelBack * 0.72f), 7f * muzzleBoost, new Color(flash.R, flash.G, flash.B, 0.20f + shot * 0.18f));
        DrawCircle(new Vector2(sway, -23f + barrelBack * 0.72f), 4f * muzzleBoost, flash);
        if (shot > 0.02f)
            DrawCircle(new Vector2(sway, -23f + barrelBack * 0.72f), 9f * shot, new Color(0.95f, 0.98f, 1.0f, 0.18f * shot));
        // Hexagonal base ΓÇö bright outer hex + dark inner hex
        DrawPolygon(RegularPoly(6, 12f, -Mathf.Pi / 6f), new[] { cyan });
        DrawPolygon(RegularPoly(6, 10f, -Mathf.Pi / 6f), new[] { dark });
        DrawCircle(Vector2.Zero, 3.5f, cyan);
    }

    private void DrawHeavyCannon()
    {
        var orange = new Color(1.00f, 0.55f, 0.00f);
        var dark   = LiftInnerTone(new Color(0.10f, 0.04f, 0.00f));
        var rim    = new Color(1.00f, 0.82f, 0.30f);
        float shot = ShotKick();
        float piston = Mathf.Sin(_idleTime * 1.8f) * 1.1f;
        float slamBack = shot * 8.8f;
        float barrelY = piston + slamBack;
        // Soft glow
        DrawCircle(Vector2.Zero, 24f, new Color(orange.R, orange.G, orange.B, 0.07f));
        DrawCircle(Vector2.Zero, 17f, new Color(orange.R, orange.G, orange.B, 0.14f));
        // Octagonal base ΓÇö bright outer, dark inner
        DrawPolygon(RegularPoly(8, 14f, 0f), new[] { orange });
        DrawPolygon(RegularPoly(8, 12f, 0f), new[] { dark });
        DrawCircle(Vector2.Zero, 4f, rim);
        // Barrel is drawn AFTER the base so recoil is clearly visible.
        DrawRect(new Rect2(-6.6f, -27f + barrelY, 13.2f, 20f), orange);
        DrawRect(new Rect2(-4.6f, -26f + barrelY, 9.2f, 18f), dark);
        DrawRect(new Rect2(-6.8f, -31f + barrelY, 13.6f, 4.5f), rim);
        DrawRect(new Rect2(-4.2f, -32f + barrelY, 8.4f, 4.5f), new Color(rim.R, rim.G, rim.B, 0.36f));
        if (shot > 0.02f)
        {
            var muzzlePos = new Vector2(0f, -31f + barrelY);
            DrawCircle(muzzlePos, 10.0f + shot * 7.0f, new Color(rim.R, rim.G, rim.B, 0.24f + 0.34f * shot));
            DrawCircle(muzzlePos, 3.2f + shot * 3.2f, new Color(1f, 0.95f, 0.75f, 0.82f));
        }
    }

    private void DrawMarkerTower()
    {
        var pink = new Color(1.00f, 0.15f, 0.60f);
        var dark = LiftInnerTone(new Color(0.12f, 0.00f, 0.08f));
        float shot = ShotKick();
        float antennaRecoil = shot * 1.7f;
        var beam = new Color(0.90f, 0.70f, 1.00f);
        // Soft glow
        DrawCircle(Vector2.Zero, 20f, new Color(pink.R, pink.G, pink.B, 0.07f));
        DrawCircle(Vector2.Zero, 14f, new Color(pink.R, pink.G, pink.B, 0.14f));
        // Diamond body ΓÇö bright outer, dark inner
        DrawPolygon(new[] { new Vector2(0,-16), new Vector2(16,0), new Vector2(0,16), new Vector2(-16,0) }, new[] { pink });
        DrawPolygon(new[] { new Vector2(0,-13), new Vector2(13,0), new Vector2(0,13), new Vector2(-13,0) }, new[] { dark });
        // Antenna with glow
        var baseP = new Vector2(0f, -13f + antennaRecoil);
        var tipP = new Vector2(0f, -22f + antennaRecoil);
        DrawLine(baseP, tipP, beam, 2f + shot * 0.6f);
        DrawCircle(tipP, 8f + shot * 2.5f, new Color(beam.R, beam.G, beam.B, 0.20f + shot * 0.20f));
        DrawCircle(tipP, 4f + shot * 1.1f, beam);
        DrawCircle(tipP, 1.5f + shot * 0.8f, new Color(1f, 0.95f, 1f));
    }

    private void DrawChainTower()
    {
        var blue  = new Color(0.50f, 0.85f, 1.00f);
        var dark  = LiftInnerTone(new Color(0.02f, 0.05f, 0.15f));
        float shot = ShotKick();
        float surge = shot * 0.6f;
        float flicker = 0.78f + 0.22f * Mathf.Sin(_idleTime * 16f);
        var white = new Color(0.90f, 0.97f, 1.00f, Mathf.Clamp(flicker + shot * 0.48f, 0f, 1f));
        // Soft glow
        DrawCircle(Vector2.Zero, 22f, new Color(blue.R, blue.G, blue.B, 0.07f + 0.03f * flicker + 0.05f * shot));
        DrawCircle(Vector2.Zero, 15f, new Color(blue.R, blue.G, blue.B, 0.14f + 0.03f * flicker + 0.05f * shot));
        // Circular base
        DrawCircle(Vector2.Zero, 11f, blue);
        DrawCircle(Vector2.Zero,  9f, dark);
        // Three discharge prongs (120° apart, pointing out from base)
        for (int i = 0; i < 3; i++)
        {
            float angle    = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
            var   dir      = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var   prongBase = dir * (9f + surge * 0.8f);
            var   prongTip  = dir * (19f + surge * 3.2f);
            DrawLine(prongBase, prongTip, blue, 3f + surge);
            DrawCircle(prongTip, 4f + surge * 1.2f, new Color(blue.R, blue.G, blue.B, 0.50f + shot * 0.20f));
            DrawCircle(prongTip, 2.5f + surge * 0.8f, white);
        }
        // Inner energy core
        DrawCircle(Vector2.Zero, 4f + surge * 0.6f, new Color(blue.R, blue.G, blue.B, 0.55f + shot * 0.20f));
        DrawCircle(Vector2.Zero, 2.5f + surge * 0.4f, white);
    }

    private void DrawRiftSapper()
    {
        var lime = new Color(0.62f, 1.00f, 0.58f);
        var dark = LiftInnerTone(new Color(0.03f, 0.11f, 0.07f));
        float shot = ShotKick();
        float pulse = 0.70f + 0.30f * Mathf.Sin(_idleTime * 6.9f);
        float surge = shot * 0.75f;

        DrawCircle(Vector2.Zero, 25f, new Color(lime.R, lime.G, lime.B, 0.09f + 0.03f * pulse));
        DrawCircle(Vector2.Zero, 17f, new Color(lime.R, lime.G, lime.B, 0.14f + 0.05f * pulse));

        // Outer body shell.
        DrawPolygon(RegularPoly(8, 14.2f + surge * 0.6f, Mathf.Pi / 8f), new[] { lime });
        DrawPolygon(RegularPoly(8, 11.6f + surge * 0.4f, Mathf.Pi / 8f), new[] { dark });

        // Mine-cell fins.
        var fin = new Color(0.88f, 1.00f, 0.82f, 0.80f + 0.14f * pulse);
        DrawRect(new Rect2(-10f, -22f - surge * 1.4f, 20f, 4.6f), fin);
        DrawRect(new Rect2(-10f,  17.4f + surge * 1.4f, 20f, 4.6f), fin);
        DrawRect(new Rect2(-22f - surge * 1.4f, -10f, 4.6f, 20f), fin);
        DrawRect(new Rect2(17.4f + surge * 1.4f, -10f, 4.6f, 20f), fin);

        // Central mine glyph.
        var glyph = new Color(0.96f, 1.00f, 0.90f, 0.86f + 0.08f * pulse);
        DrawCircle(Vector2.Zero, 5.3f + surge * 0.45f, new Color(lime.R, lime.G, lime.B, 0.58f + 0.18f * shot));
        DrawLine(new Vector2(-4f, 0f), new Vector2(4f, 0f), glyph, 1.7f);
        DrawLine(new Vector2(0f, -4f), new Vector2(0f, 4f), glyph, 1.7f);
        DrawCircle(Vector2.Zero, 2.2f + surge * 0.3f, new Color(1f, 1f, 0.95f, 0.94f));
    }

    private void DrawAccordionEngine()
    {
        var violet = new Color(0.72f, 0.20f, 1.00f);
        var dark   = LiftInnerTone(new Color(0.08f, 0.01f, 0.18f));
        var bright = new Color(0.88f, 0.55f, 1.00f);
        float shot = ShotKick();
        float pulse = 0.65f + 0.35f * Mathf.Sin(_idleTime * 5.2f);
        float compress = shot * 0.70f;   // claw arm drives inward on fire

        // Soft glow
        DrawCircle(Vector2.Zero, 26f, new Color(violet.R, violet.G, violet.B, 0.07f + 0.04f * pulse));
        DrawCircle(Vector2.Zero, 17f, new Color(violet.R, violet.G, violet.B, 0.14f + 0.05f * pulse));

        // Hexagonal core body
        DrawPolygon(RegularPoly(6, 13f, 0f), new[] { violet });
        DrawPolygon(RegularPoly(6, 10.5f, 0f), new[] { dark });

        // Resonator ring
        float ringPulse = 0.20f + 0.12f * Mathf.Sin(_idleTime * 8.0f) + shot * 0.28f;
        DrawArc(Vector2.Zero, 17f, 0f, Mathf.Tau, 48, new Color(bright.R, bright.G, bright.B, ringPulse), 1.6f);

        // Compression claw arms: top and bottom, drive inward on shot
        float armY = 18f - compress * 5.5f;
        var clawColor = new Color(bright.R, bright.G, bright.B, 0.80f + 0.14f * shot);
        // Top claw
        DrawLine(new Vector2(-8f, -armY),   new Vector2( 8f, -armY),   clawColor, 2.8f);
        DrawLine(new Vector2(-8f, -armY),   new Vector2(-8f, -armY + 4.5f), clawColor, 2.2f);
        DrawLine(new Vector2( 8f, -armY),   new Vector2( 8f, -armY + 4.5f), clawColor, 2.2f);
        // Bottom claw
        DrawLine(new Vector2(-8f,  armY),   new Vector2( 8f,  armY),   clawColor, 2.8f);
        DrawLine(new Vector2(-8f,  armY),   new Vector2(-8f,  armY - 4.5f), clawColor, 2.2f);
        DrawLine(new Vector2( 8f,  armY),   new Vector2( 8f,  armY - 4.5f), clawColor, 2.2f);

        // Core energy node
        DrawCircle(Vector2.Zero, 4.5f + compress * 0.8f, new Color(violet.R, violet.G, violet.B, 0.65f + 0.20f * shot));
        DrawCircle(Vector2.Zero, 2.5f + compress * 0.5f, new Color(1f, 0.85f, 1f, 0.90f));
    }

    private void DrawPhaseSplitter()
    {
        var aqua = new Color(0.45f, 1.00f, 0.95f);
        var dark = LiftInnerTone(new Color(0.04f, 0.11f, 0.16f));
        var white = new Color(0.92f, 1.00f, 1.00f);
        float shot = ShotKick();
        float pulse = 0.62f + 0.38f * Mathf.Sin(_idleTime * 6.2f);
        float phase = Mathf.Sin(_idleTime * 5.2f) * 1.4f;
        float armPush = shot * 2.8f;

        DrawCircle(Vector2.Zero, 24f, new Color(aqua.R, aqua.G, aqua.B, 0.08f + 0.04f * pulse));
        DrawCircle(Vector2.Zero, 16f, new Color(aqua.R, aqua.G, aqua.B, 0.13f + 0.06f * pulse));

        // Split core.
        DrawPolygon(RegularPoly(10, 10.5f, Mathf.Pi / 10f), new[] { aqua });
        DrawPolygon(RegularPoly(10, 8.2f, Mathf.Pi / 10f), new[] { dark });

        // Opposed emitter arms (front/back symmetry).
        float armLen = 15.5f + armPush;
        float armHalfWidth = 3.1f + shot * 0.5f;
        DrawPolygon(new[]
        {
            new Vector2(-armHalfWidth, -armLen - phase),
            new Vector2( armHalfWidth, -armLen - phase),
            new Vector2( armHalfWidth * 0.7f, -9f),
            new Vector2(-armHalfWidth * 0.7f, -9f),
        }, new[] { aqua });
        DrawPolygon(new[]
        {
            new Vector2(-armHalfWidth,  armLen - phase),
            new Vector2( armHalfWidth,  armLen - phase),
            new Vector2( armHalfWidth * 0.7f, 9f),
            new Vector2(-armHalfWidth * 0.7f, 9f),
        }, new[] { aqua });

        DrawCircle(new Vector2(0f, -armLen - phase), 3.4f + shot, new Color(white.R, white.G, white.B, 0.80f + shot * 0.15f));
        DrawCircle(new Vector2(0f, armLen - phase), 3.4f + shot, new Color(white.R, white.G, white.B, 0.80f + shot * 0.15f));

        // Midline phase seam.
        DrawArc(Vector2.Zero, 13.5f + shot * 0.8f, -Mathf.Pi * 0.43f, Mathf.Pi * 0.43f, 26, new Color(aqua.R, aqua.G, aqua.B, 0.34f + shot * 0.18f), 1.8f);
        DrawArc(Vector2.Zero, 13.5f + shot * 0.8f, Mathf.Pi * 0.57f, Mathf.Pi * 1.43f, 26, new Color(aqua.R, aqua.G, aqua.B, 0.34f + shot * 0.18f), 1.8f);
        DrawCircle(Vector2.Zero, 2.6f + shot * 0.5f, new Color(1f, 1f, 1f, 0.88f));
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

    private static Color LiftInnerTone(Color color)
    {
        // Preserve hue while lifting very dark fills so they stay readable on neon maps.
        bool mobile = MobileOptimization.IsMobile();
        float minLuma = mobile ? 0.17f : 0.13f;
        float maxChannel = mobile ? 0.44f : 0.38f;
        float luma = color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
        if (luma >= minLuma)
            return color;

        float gain = minLuma / Mathf.Max(0.001f, luma);
        return new Color(
            Mathf.Min(color.R * gain, maxChannel),
            Mathf.Min(color.G * gain, maxChannel),
            Mathf.Min(color.B * gain, maxChannel),
            color.A
        );
    }

    private void DrawReadabilityAccent()
    {
        bool mobile = MobileOptimization.IsMobile();
        float ringAlpha = mobile ? 0.58f : 0.42f;
        float ringWidth = mobile ? 1.7f : 1.45f;
        float coreAlpha = mobile ? 0.78f : 0.62f;
        var ring = new Color(BodyColor.R, BodyColor.G, BodyColor.B, ringAlpha);
        DrawArc(Vector2.Zero, 13.5f, 0f, Mathf.Tau, 36, ring, ringWidth);
        DrawCircle(Vector2.Zero, 1.6f, new Color(1f, 1f, 1f, coreAlpha));
    }
}
