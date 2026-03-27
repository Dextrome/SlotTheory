using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Modifiers;
using SlotTheory.UI;

namespace SlotTheory.Entities;

public enum TargetingMode { First, Strongest, LowestHp, Last }
public enum TowerVisualTier { Tier0, Tier1, Tier2, Tier3 }
public enum FocalAccentShape { Crest, Lens, Bracket, Spike, Chain }
public enum AccentChannel { None, Top, Left, Base, Inner }

public readonly struct ModVisualRecipe
{
    public int VisualPriority { get; }
    public FocalAccentShape FocalShape { get; }

    public ModVisualRecipe(int visualPriority, FocalAccentShape focalShape)
    {
        VisualPriority = visualPriority;
        FocalShape = focalShape;
    }
}

public readonly struct TowerVisualEvolutionState
{
    public int EquippedModifierCount { get; }
    public TowerVisualTier Tier { get; }
    public bool HasFocalAccent { get; }
    public string FocalModId { get; }
    public FocalAccentShape FocalShape { get; }
    public bool HasSupportAccent { get; }
    public string SupportModId { get; }
    public FocalAccentShape SupportShape { get; }
    public AccentChannel SupportChannel { get; }
    public bool SupportReinforced { get; }
    public bool HasTertiaryHint { get; }
    public string TertiaryModId { get; }
    public FocalAccentShape TertiaryShape { get; }
    public AccentChannel TertiaryChannel { get; }
    public bool TertiaryReinforced { get; }

    public TowerVisualEvolutionState(
        TowerVisualTier tier,
        int equippedModifierCount,
        bool hasFocalAccent,
        string focalModId,
        FocalAccentShape focalShape,
        bool hasSupportAccent,
        string supportModId,
        FocalAccentShape supportShape,
        AccentChannel supportChannel,
        bool supportReinforced,
        bool hasTertiaryHint,
        string tertiaryModId,
        FocalAccentShape tertiaryShape,
        AccentChannel tertiaryChannel,
        bool tertiaryReinforced)
    {
        EquippedModifierCount = equippedModifierCount;
        Tier = tier;
        HasFocalAccent = hasFocalAccent;
        FocalModId = focalModId;
        FocalShape = focalShape;
        HasSupportAccent = hasSupportAccent;
        SupportModId = supportModId;
        SupportShape = supportShape;
        SupportChannel = supportChannel;
        SupportReinforced = supportReinforced;
        HasTertiaryHint = hasTertiaryHint;
        TertiaryModId = tertiaryModId;
        TertiaryShape = tertiaryShape;
        TertiaryChannel = tertiaryChannel;
        TertiaryReinforced = tertiaryReinforced;
    }
}

/// <summary>
/// Tower node. Positioned as a child of its Slot node so GlobalPosition is correct for range checks.
/// </summary>
public partial class TowerInstance : Node2D, ITowerView
{
    // Recipe metadata defines accent shape language. Focal ownership itself is driven by
    // equip order (first-equipped modifier), not spectacle ordering.
    private static readonly Dictionary<string, ModVisualRecipe> ModVisualRecipes = new(StringComparer.Ordinal)
    {
        ["focus_lens"]       = new(100, FocalAccentShape.Lens),
        ["blast_core"]       = new(96, FocalAccentShape.Spike),
        ["chain_reaction"]   = new(92, FocalAccentShape.Chain),
        ["split_shot"]       = new(90, FocalAccentShape.Chain),
        ["wildfire"]         = new(88, FocalAccentShape.Spike),
        ["overkill"]         = new(86, FocalAccentShape.Spike),
        ["feedback_loop"]    = new(84, FocalAccentShape.Crest),
        ["reaper_protocol"]  = new(82, FocalAccentShape.Lens),
        ["exploit_weakness"] = new(80, FocalAccentShape.Bracket),
        ["momentum"]         = new(78, FocalAccentShape.Crest),
        ["overreach"]        = new(76, FocalAccentShape.Lens),
        ["slow"]             = new(74, FocalAccentShape.Bracket),
        ["hair_trigger"]     = new(72, FocalAccentShape.Spike),
    };
    private static readonly ModVisualRecipe DefaultModRecipe = new(0, FocalAccentShape.Crest);
    private static readonly HashSet<string> FlagshipTowerIds = new(StringComparer.Ordinal)
    {
        "heavy_cannon",
        "rocket_launcher",
        "chain_tower",
        "phase_splitter",
    };
    private static readonly HashSet<string> FlagshipModIds = new(StringComparer.Ordinal)
    {
        "focus_lens",
        "chain_reaction",
        "blast_core",
        "wildfire",
    };

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
    private float _visualChaosLoad = 0f;
    private TowerVisualEvolutionState _visualEvolution = new(
        TowerVisualTier.Tier0,
        equippedModifierCount: 0,
        hasFocalAccent: false,
        focalModId: string.Empty,
        focalShape: FocalAccentShape.Crest,
        hasSupportAccent: false,
        supportModId: string.Empty,
        supportShape: FocalAccentShape.Crest,
        supportChannel: AccentChannel.None,
        supportReinforced: false,
        hasTertiaryHint: false,
        tertiaryModId: string.Empty,
        tertiaryShape: FocalAccentShape.Crest,
        tertiaryChannel: AccentChannel.None,
        tertiaryReinforced: false);
    private int _visualEvolutionHash = int.MinValue;
    private float _evolutionTransitionRemaining = 0f;
    private float _evolutionTransitionDuration = 0f;
    private float _evolutionTransitionIntensity = 0f;
    private float _shellAssemblyBoost = 0f;
    private float _accentLockBoost = 0f;
    private float _channelSurgeBoost = 0f;
    private AccentChannel _transitionChannel = AccentChannel.None;
    private Color _transitionAccent = Colors.White;
    private const float ShotAttackSeconds = 0.030f;
    private const float ShotDecaySeconds = 0.18f;

    public override void _Ready()
    {
        RebuildEvolutionVisuals(allowTransition: false);
    }

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
        _visualChaosLoad = Mathf.Clamp(GameController.CombatVisualChaosLoad, 0f, 1f);
        if (_shotElapsed < ShotAttackSeconds + ShotDecaySeconds)
            _shotElapsed += dt;
        if (_lockLineRemaining > 0f)
            _lockLineRemaining = Mathf.Max(0f, _lockLineRemaining - dt);
        if (_spectacleChargePulse > 0f)
            _spectacleChargePulse = Mathf.Max(0f, _spectacleChargePulse - dt * 1.8f);
        if (_teachingHighlightRemaining > 0f)
            _teachingHighlightRemaining = Mathf.Max(0f, _teachingHighlightRemaining - dt);
        if (_evolutionTransitionRemaining > 0f)
        {
            _evolutionTransitionRemaining = Mathf.Max(0f, _evolutionTransitionRemaining - dt);
            UpdateEvolutionTransitionEnvelope();
        }
        else if (_shellAssemblyBoost > 0f || _accentLockBoost > 0f || _channelSurgeBoost > 0f)
        {
            _shellAssemblyBoost = 0f;
            _accentLockBoost = 0f;
            _channelSurgeBoost = 0f;
        }

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
        if (AttackInterval > 0f || spectacleMeter > 0f || _spectacleChargePulse > 0f || _teachingHighlightRemaining > 0f || _evolutionTransitionRemaining > 0f)
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

    public void RebuildEvolutionVisuals(bool allowTransition = true)
    {
        int hash = ComputeEvolutionHash();
        if (hash == _visualEvolutionHash)
            return;

        int previousCount = _visualEvolution.EquippedModifierCount;
        _visualEvolutionHash = hash;

        var tier = ResolveVisualTier(Modifiers.Count);
        ResolveOrderedModIds(out string focalModId, out string supportModId, out string tertiaryModId);
        bool hasFocal = focalModId.Length > 0;
        var focalRecipe = hasFocal ? ResolveModRecipe(focalModId) : DefaultModRecipe;

        bool hasSupport = tier >= TowerVisualTier.Tier2 && supportModId.Length > 0;
        var supportRecipe = hasSupport ? ResolveModRecipe(supportModId) : DefaultModRecipe;
        AccentChannel supportChannel = hasSupport
            ? ResolveSupportChannel(focalRecipe.FocalShape, supportRecipe.FocalShape)
            : AccentChannel.None;
        bool supportReinforced = hasSupport && StringComparer.OrdinalIgnoreCase.Equals(supportModId, focalModId);

        bool hasTertiary = tier >= TowerVisualTier.Tier3 && tertiaryModId.Length > 0;
        var tertiaryRecipe = hasTertiary ? ResolveModRecipe(tertiaryModId) : DefaultModRecipe;
        AccentChannel tertiaryChannel = hasTertiary
            ? ResolveTertiaryChannel(focalRecipe.FocalShape, supportChannel, tertiaryRecipe.FocalShape)
            : AccentChannel.None;
        bool tertiaryReinforced = hasTertiary && (
            StringComparer.OrdinalIgnoreCase.Equals(tertiaryModId, focalModId) ||
            (hasSupport && StringComparer.OrdinalIgnoreCase.Equals(tertiaryModId, supportModId)));

        _visualEvolution = new TowerVisualEvolutionState(
            tier,
            equippedModifierCount: Modifiers.Count,
            hasFocalAccent: hasFocal,
            focalModId,
            focalRecipe.FocalShape,
            hasSupportAccent: hasSupport,
            supportModId,
            supportRecipe.FocalShape,
            supportChannel,
            supportReinforced,
            hasTertiaryHint: hasTertiary,
            tertiaryModId,
            tertiaryRecipe.FocalShape,
            tertiaryChannel,
            tertiaryReinforced);

        if (allowTransition && Modifiers.Count > previousCount)
            StartEvolutionTransition(previousCount, _visualEvolution);

        QueueRedraw();
    }

    public override void _Draw()
    {
        _visualChaosLoad = Mathf.Clamp(GameController.CombatVisualChaosLoad, 0f, 1f);
        // Draw cooldown ring base first so tower/barrel geometry sits on top.
        DrawChargeArc();
        DrawSpectacleArc();
        DrawTeachingHighlight();

        switch (TowerId)
        {
            case "rapid_shooter":    DrawRapidShooter();    break;
            case "heavy_cannon":     DrawHeavyCannon();     break;
            case "rocket_launcher":  DrawRocketLauncher();  break;
            case "marker_tower":     DrawMarkerTower();     break;
            case "chain_tower":      DrawChainTower();      break;
            case "rift_prism":       DrawRiftSapper();      break;
            case "accordion_engine": DrawAccordionEngine(); break;
            case "phase_splitter":   DrawPhaseSplitter();   break;
            case "undertow_engine":  DrawUndertowEngine();  break;
            default: DrawCircle(Vector2.Zero, 10f, new Color(0.2f, 0.5f, 1.0f)); break;
        }

        DrawTierShell();
        DrawFlagshipTowerSignature();
        DrawEvolutionTransitionFx();
        DrawTertiaryModHint();
        DrawSupportModAccent();
        DrawFocalModAccent();
        DrawReadabilityAccent();
        DrawChargeArc(overlayPass: true);
        DrawTargetLockLine();
    }

    private static TowerVisualTier ResolveVisualTier(int modCount) => modCount switch
    {
        <= 0 => TowerVisualTier.Tier0,
        1 => TowerVisualTier.Tier1,
        2 => TowerVisualTier.Tier2,
        _ => TowerVisualTier.Tier3,
    };

    private int ComputeEvolutionHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + Modifiers.Count;
            for (int i = 0; i < Modifiers.Count; i++)
            {
                string id = Modifiers[i]?.ModifierId ?? string.Empty;
                h = h * 31 + StringComparer.Ordinal.GetHashCode(id);
            }
            return h;
        }
    }

    private static ModVisualRecipe ResolveModRecipe(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return DefaultModRecipe;

        if (ModVisualRecipes.TryGetValue(modId, out var recipe))
            return recipe;

        string normalized = modId.Trim().ToLowerInvariant();
        if (ModVisualRecipes.TryGetValue(normalized, out recipe))
            return recipe;

        return DefaultModRecipe;
    }

    private void ResolveOrderedModIds(out string focalModId, out string supportModId, out string tertiaryModId)
    {
        focalModId = string.Empty;
        supportModId = string.Empty;
        tertiaryModId = string.Empty;

        for (int i = 0; i < Modifiers.Count; i++)
        {
            string id = Modifiers[i]?.ModifierId ?? string.Empty;
            if (id.Length == 0)
                continue;

            if (focalModId.Length == 0)
            {
                // Preserve continuity: first equipped modifier owns focal identity.
                focalModId = id;
                continue;
            }

            if (supportModId.Length == 0)
            {
                supportModId = id;
                continue;
            }

            tertiaryModId = id;
            break;
        }
    }

    private static AccentChannel ResolveSupportChannel(FocalAccentShape focalShape, FocalAccentShape supportShape)
    {
        AccentChannel preferred = supportShape switch
        {
            FocalAccentShape.Lens => AccentChannel.Base,
            FocalAccentShape.Bracket => AccentChannel.Base,
            _ => AccentChannel.Left,
        };

        if (!IsBlockedByFocal(preferred, focalShape))
            return preferred;

        AccentChannel fallback = preferred == AccentChannel.Left ? AccentChannel.Base : AccentChannel.Left;
        if (!IsBlockedByFocal(fallback, focalShape))
            return fallback;

        return AccentChannel.Base;
    }

    private static AccentChannel ResolveTertiaryChannel(FocalAccentShape focalShape, AccentChannel supportChannel, FocalAccentShape tertiaryShape)
    {
        AccentChannel[] order = tertiaryShape switch
        {
            FocalAccentShape.Spike => new[] { AccentChannel.Base, AccentChannel.Inner, AccentChannel.Left },
            FocalAccentShape.Chain => new[] { AccentChannel.Base, AccentChannel.Left, AccentChannel.Inner },
            FocalAccentShape.Bracket => new[] { AccentChannel.Inner, AccentChannel.Base, AccentChannel.Left },
            FocalAccentShape.Lens => new[] { AccentChannel.Inner, AccentChannel.Base, AccentChannel.Left },
            _ => new[] { AccentChannel.Inner, AccentChannel.Left, AccentChannel.Base },
        };

        for (int i = 0; i < order.Length; i++)
        {
            AccentChannel channel = order[i];
            if (channel == supportChannel)
                continue;
            if (IsBlockedByFocal(channel, focalShape))
                continue;
            return channel;
        }

        return AccentChannel.Inner;
    }

    private static bool IsBlockedByFocal(AccentChannel channel, FocalAccentShape focalShape)
    {
        // Focal owns the prime top channel. Keep support/micro away from it.
        if (channel == AccentChannel.Top)
            return true;

        // Bracket focal accents occupy both flanks, so avoid left channel overlap.
        if (channel == AccentChannel.Left && focalShape == FocalAccentShape.Bracket)
            return true;

        return false;
    }

    private void StartEvolutionTransition(int previousCount, in TowerVisualEvolutionState next)
    {
        int nextCount = next.EquippedModifierCount;
        if (nextCount <= previousCount || nextCount <= 0)
            return;

        bool reducedMotion = IsReducedMotionEnabled();
        _evolutionTransitionDuration = reducedMotion ? 0.24f : 0.42f;
        _evolutionTransitionRemaining = _evolutionTransitionDuration;
        _evolutionTransitionIntensity = reducedMotion ? 0.62f : 1f;
        _transitionChannel = ResolveGainedChannel(nextCount, next);
        _transitionAccent = ResolveGainedAccent(nextCount, next);
        UpdateEvolutionTransitionEnvelope();
    }

    private static AccentChannel ResolveGainedChannel(int nextCount, in TowerVisualEvolutionState state)
    {
        return nextCount switch
        {
            1 => AccentChannel.Top,
            2 => state.SupportChannel != AccentChannel.None ? state.SupportChannel : AccentChannel.Base,
            _ => state.TertiaryChannel != AccentChannel.None ? state.TertiaryChannel : AccentChannel.Inner,
        };
    }

    private static Color ResolveGainedAccent(int nextCount, in TowerVisualEvolutionState state)
    {
        string modId = nextCount switch
        {
            1 => state.FocalModId,
            2 => state.SupportModId,
            _ => state.TertiaryModId,
        };
        return modId.Length > 0 ? ModifierVisuals.GetAccent(modId) : Colors.White;
    }

    private void UpdateEvolutionTransitionEnvelope()
    {
        if (_evolutionTransitionDuration <= 0f || _evolutionTransitionRemaining <= 0f)
        {
            _shellAssemblyBoost = 0f;
            _accentLockBoost = 0f;
            _channelSurgeBoost = 0f;
            return;
        }

        float t = 1f - (_evolutionTransitionRemaining / _evolutionTransitionDuration);
        float intensity = _evolutionTransitionIntensity;

        // Phase 3 transition grammar:
        // 1) shell assembly pulse, 2) accent lock-in, 3) channel surge, 4) settle.
        _shellAssemblyBoost = intensity * WindowPulse(t, 0.00f, 0.16f, 0.48f);
        _accentLockBoost = intensity * WindowPulse(t, 0.14f, 0.34f, 0.70f);
        _channelSurgeBoost = intensity * WindowPulse(t, 0.24f, 0.44f, 0.84f);
    }

    private static float WindowPulse(float t, float start, float peak, float end)
    {
        if (t <= start || t >= end)
            return 0f;
        if (t < peak)
            return Mathf.Clamp((t - start) / Mathf.Max(0.0001f, peak - start), 0f, 1f);
        return Mathf.Clamp(1f - ((t - peak) / Mathf.Max(0.0001f, end - peak)), 0f, 1f);
    }

    private bool IsReducedMotionEnabled() => SettingsManager.Instance?.ReducedMotion == true;

    private void DrawTierShell()
    {
        if (_visualEvolution.Tier == TowerVisualTier.Tier0)
            return;

        float shellPulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 2.2f);
        float transitionBoost = _shellAssemblyBoost;
        Color shell = new Color(BodyColor.R, BodyColor.G, BodyColor.B, 1f);

        switch (_visualEvolution.Tier)
        {
            case TowerVisualTier.Tier1:
            {
                // Light infusion cue: small, partial top shell.
                float alpha = 0.22f + shellPulse * 0.10f + transitionBoost * 0.24f;
                DrawArc(Vector2.Zero, 18.4f, -2.28f, -0.86f, 20, new Color(shell.R, shell.G, shell.B, alpha), 1.6f + transitionBoost * 0.55f);
                DrawCircle(new Vector2(0f, -18.2f), 1.6f + transitionBoost * 0.30f, new Color(shell.R, shell.G, shell.B, 0.30f + shellPulse * 0.16f + transitionBoost * 0.18f));
                break;
            }
            case TowerVisualTier.Tier2:
            {
                // Clear structural escalation: segmented frame + dual-node crest.
                float alpha = 0.36f + shellPulse * 0.12f + transitionBoost * 0.22f;
                var c = new Color(shell.R, shell.G, shell.B, alpha);
                DrawArc(Vector2.Zero, 21.2f, -2.80f, -0.34f, 24, c, 2.4f + transitionBoost * 0.60f);
                DrawArc(Vector2.Zero, 21.2f, 0.34f, 2.80f, 24, c, 2.4f + transitionBoost * 0.60f);
                DrawLine(new Vector2(-16.6f, -7.0f), new Vector2(-16.6f, 7.2f), new Color(shell.R, shell.G, shell.B, alpha * 0.92f), 2.3f + transitionBoost * 0.45f);
                DrawLine(new Vector2(16.6f, -7.0f), new Vector2(16.6f, 7.2f), new Color(shell.R, shell.G, shell.B, alpha * 0.92f), 2.3f + transitionBoost * 0.45f);
                DrawLine(new Vector2(-5.0f, -20.6f), new Vector2(5.0f, -20.6f), new Color(shell.R, shell.G, shell.B, alpha * 0.94f), 1.9f + transitionBoost * 0.28f);
                DrawCircle(new Vector2(-5.0f, -20.6f), 2.0f + transitionBoost * 0.24f, new Color(shell.R, shell.G, shell.B, alpha * (0.90f + shellPulse * 0.08f)));
                DrawCircle(new Vector2(5.0f, -20.6f), 2.0f + transitionBoost * 0.24f, new Color(shell.R, shell.G, shell.B, alpha * (0.90f + shellPulse * 0.08f)));
                DrawLine(new Vector2(0f, -17.2f), new Vector2(0f, -20.2f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.7f + transitionBoost * 0.22f);
                break;
            }
            case TowerVisualTier.Tier3:
            {
                // Unmistakable maxed state: complete frame + triad anchors + inner lock ring.
                float alpha = 0.46f + shellPulse * 0.14f + transitionBoost * 0.20f;
                var c = new Color(shell.R, shell.G, shell.B, alpha);
                DrawArc(Vector2.Zero, 22.8f, 0f, Mathf.Tau, 48, c, 3.0f + transitionBoost * 0.60f);
                DrawArc(Vector2.Zero, 17.2f, 0f, Mathf.Tau, 40, new Color(shell.R, shell.G, shell.B, alpha * 0.78f), 2.0f + transitionBoost * 0.42f);
                DrawArc(Vector2.Zero, 12.6f, 0f, Mathf.Tau, 32, new Color(shell.R, shell.G, shell.B, alpha * 0.58f), 1.6f + transitionBoost * 0.28f);
                for (int i = 0; i < 3; i++)
                {
                    float a = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
                    var outer = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 22.8f;
                    var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 16.0f;
                    DrawCircle(outer, 2.9f + transitionBoost * 0.24f, new Color(shell.R, shell.G, shell.B, alpha * (0.86f + shellPulse * 0.10f)));
                    DrawLine(inner, outer, new Color(shell.R, shell.G, shell.B, alpha * 0.80f), 1.9f + transitionBoost * 0.30f);
                }
                DrawLine(new Vector2(-8.6f, -24.0f), new Vector2(8.6f, -24.0f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.9f + transitionBoost * 0.24f);
                DrawCircle(new Vector2(-8.6f, -24.0f), 1.9f + transitionBoost * 0.16f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));
                DrawCircle(new Vector2(0f, -25.4f), 2.1f + transitionBoost * 0.20f, new Color(shell.R, shell.G, shell.B, alpha * 0.96f));
                DrawCircle(new Vector2(8.6f, -24.0f), 1.9f + transitionBoost * 0.16f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));
                DrawCircle(Vector2.Zero, 2.0f + transitionBoost * 0.18f, new Color(shell.R, shell.G, shell.B, alpha * 0.84f));
                break;
            }
        }
    }

    private void DrawFlagshipTowerSignature()
    {
        if (_visualEvolution.Tier < TowerVisualTier.Tier2 || !FlagshipTowerIds.Contains(TowerId))
            return;

        bool t3 = _visualEvolution.Tier == TowerVisualTier.Tier3;
        float shot = ShotKick();
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 2.9f);
        float alpha = (t3 ? 0.34f : 0.24f) + pulse * (t3 ? 0.10f : 0.07f) + _shellAssemblyBoost * 0.16f;
        Color shell = new Color(BodyColor.R, BodyColor.G, BodyColor.B, alpha);

        switch (TowerId)
        {
            case "chain_tower":
            {
                float r = t3 ? 24.2f : 20.8f;
                float innerR = t3 ? 18.8f : 15.8f;
                for (int i = 0; i < 3; i++)
                {
                    float a = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
                    float b = -Mathf.Pi / 2f + ((i + 1) % 3) * Mathf.Tau / 3f;
                    var p0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var p1 = new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r;
                    var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * innerR;
                    DrawLine(p0, p1, shell, t3 ? 2.0f : 1.5f);
                    DrawLine(inner, p0, new Color(shell.R, shell.G, shell.B, alpha * 0.78f), t3 ? 1.5f : 1.2f);
                    DrawCircle(p0, t3 ? 2.3f : 1.8f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));
                }

                // Pair signature: chain tower + chain reaction resolves into a stabilized orbit.
                if (t3 && StringComparer.OrdinalIgnoreCase.Equals(_visualEvolution.FocalModId, "chain_reaction"))
                {
                    DrawArc(Vector2.Zero, 27.0f, -2.72f, -1.46f, 20, new Color(shell.R, shell.G, shell.B, alpha * 0.80f), 1.6f);
                    DrawArc(Vector2.Zero, 27.0f, -0.63f, 0.63f, 20, new Color(shell.R, shell.G, shell.B, alpha * 0.80f), 1.6f);
                    DrawArc(Vector2.Zero, 27.0f, 1.46f, 2.72f, 20, new Color(shell.R, shell.G, shell.B, alpha * 0.80f), 1.6f);
                }
                break;
            }
            case "heavy_cannon":
            {
                float railY0 = -6.6f - shot * 1.2f;
                float railY1 = 7.6f + shot * 0.8f;
                float railX = t3 ? 19.6f : 17.8f;
                DrawLine(new Vector2(-railX, railY0), new Vector2(-railX, railY1), shell, t3 ? 2.3f : 1.8f);
                DrawLine(new Vector2(railX, railY0), new Vector2(railX, railY1), shell, t3 ? 2.3f : 1.8f);
                DrawLine(new Vector2(-9.6f, -22.2f), new Vector2(9.6f, -22.2f), new Color(shell.R, shell.G, shell.B, alpha * 0.92f), t3 ? 2.1f : 1.6f);
                DrawCircle(new Vector2(0f, -22.2f), t3 ? 2.3f : 1.7f, new Color(shell.R, shell.G, shell.B, alpha * 0.88f));

                // Pair signature: heavy cannon + blast core gets an overbore muzzle collar.
                if (t3 && StringComparer.OrdinalIgnoreCase.Equals(_visualEvolution.FocalModId, "blast_core"))
                {
                    DrawArc(Vector2.Zero, 26.8f, -2.15f, -0.98f, 18, new Color(shell.R, shell.G, shell.B, alpha * 0.84f), 1.9f);
                    DrawLine(new Vector2(-6.6f, -24.4f), new Vector2(6.6f, -24.4f), new Color(shell.R, shell.G, shell.B, alpha * 0.88f), 1.7f);
                }
                break;
            }
            case "rocket_launcher":
            {
                float ring = t3 ? 24.8f : 21.6f;
                float tail = t3 ? 18.2f : 16.0f;
                DrawArc(Vector2.Zero, ring, -2.32f, -0.90f, 24, shell, t3 ? 2.0f : 1.6f);
                DrawArc(Vector2.Zero, ring, 0.90f, 2.32f, 24, shell, t3 ? 2.0f : 1.6f);
                DrawLine(new Vector2(-7.2f, -24.4f), new Vector2(7.2f, -24.4f), new Color(shell.R, shell.G, shell.B, alpha * 0.90f), t3 ? 1.9f : 1.5f);
                DrawLine(new Vector2(0f, -6.8f), new Vector2(0f, tail), new Color(shell.R, shell.G, shell.B, alpha * 0.82f), t3 ? 1.6f : 1.3f);
                DrawCircle(new Vector2(0f, -24.4f), t3 ? 2.2f : 1.7f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));

                if (t3 && StringComparer.OrdinalIgnoreCase.Equals(_visualEvolution.FocalModId, "blast_core"))
                    DrawArc(Vector2.Zero, 28.0f, -2.74f, -0.40f, 26, new Color(shell.R, shell.G, shell.B, alpha * 0.82f), 1.6f);
                break;
            }
            case "phase_splitter":
            {
                float arm = t3 ? 22.0f : 19.5f;
                float cross = t3 ? 9.6f : 8.2f;
                DrawLine(new Vector2(-cross, -arm), new Vector2(cross, -arm), shell, t3 ? 2.0f : 1.6f);
                DrawLine(new Vector2(-cross, arm), new Vector2(cross, arm), shell, t3 ? 2.0f : 1.6f);
                DrawLine(new Vector2(-cross, -arm), new Vector2(-cross, -11.0f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.5f);
                DrawLine(new Vector2(cross, -arm), new Vector2(cross, -11.0f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.5f);
                DrawLine(new Vector2(-cross, arm), new Vector2(-cross, 11.0f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.5f);
                DrawLine(new Vector2(cross, arm), new Vector2(cross, 11.0f), new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.5f);
                DrawCircle(new Vector2(0f, -arm), t3 ? 2.2f : 1.7f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));
                DrawCircle(new Vector2(0f, arm), t3 ? 2.2f : 1.7f, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));

                // Pair signature: phase splitter + focus lens gets dual-lens gate caps.
                if (t3 && StringComparer.OrdinalIgnoreCase.Equals(_visualEvolution.FocalModId, "focus_lens"))
                {
                    DrawArc(new Vector2(0f, -arm), 3.2f, 0f, Mathf.Tau, 14, new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.4f);
                    DrawArc(new Vector2(0f, arm), 3.2f, 0f, Mathf.Tau, 14, new Color(shell.R, shell.G, shell.B, alpha * 0.86f), 1.4f);
                }
                break;
            }
        }
    }

    private void DrawFocalModAccent()
    {
        if (!_visualEvolution.HasFocalAccent || _visualEvolution.FocalModId.Length == 0)
            return;

        bool reducedMotion = IsReducedMotionEnabled();
        float motionScale = reducedMotion ? 0.45f : 1f;
        Color accent = ModifierVisuals.GetAccent(_visualEvolution.FocalModId);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 5.1f * motionScale);
        float tierStrength = _visualEvolution.Tier switch
        {
            TowerVisualTier.Tier1 => 0.92f,
            TowerVisualTier.Tier2 => 1.00f,
            TowerVisualTier.Tier3 => 1.08f,
            _ => 0.88f,
        };
        float pulseStrength = reducedMotion ? 0.18f : 0.30f;
        float alpha = Mathf.Clamp((0.50f + pulse * pulseStrength + _accentLockBoost * 0.22f) * tierStrength, 0f, 1f);

        switch (_visualEvolution.FocalShape)
        {
            case FocalAccentShape.Lens:
                DrawArc(Vector2.Zero, 8.0f, 0f, Mathf.Tau, 24, new Color(accent.R, accent.G, accent.B, alpha), 1.8f);
                DrawArc(Vector2.Zero, 11.2f, -2.05f, -1.10f, 10, new Color(accent.R, accent.G, accent.B, alpha * 0.78f), 1.6f);
                DrawLine(new Vector2(0f, -7.2f), new Vector2(0f, -13.8f), new Color(accent.R, accent.G, accent.B, alpha * 0.72f), 1.5f);
                break;

            case FocalAccentShape.Bracket:
                DrawLine(new Vector2(-11.5f, -3.4f), new Vector2(-15.2f, -3.4f), new Color(accent.R, accent.G, accent.B, alpha), 1.8f);
                DrawLine(new Vector2(-15.2f, -3.4f), new Vector2(-15.2f, 2.6f), new Color(accent.R, accent.G, accent.B, alpha), 1.8f);
                DrawLine(new Vector2(11.5f, -3.4f), new Vector2(15.2f, -3.4f), new Color(accent.R, accent.G, accent.B, alpha), 1.8f);
                DrawLine(new Vector2(15.2f, -3.4f), new Vector2(15.2f, 2.6f), new Color(accent.R, accent.G, accent.B, alpha), 1.8f);
                DrawCircle(new Vector2(0f, -9.2f), 1.8f, new Color(accent.R, accent.G, accent.B, alpha * 0.85f));
                break;

            case FocalAccentShape.Spike:
            {
                var tip = new Vector2(0f, -19.8f);
                var left = new Vector2(-3.5f, -12.6f);
                var right = new Vector2(3.5f, -12.6f);
                DrawPolygon(new[] { tip, right, left }, new[] { new Color(accent.R, accent.G, accent.B, alpha * 0.95f) });
                DrawLine(new Vector2(-7.4f, -10.8f), new Vector2(-11.8f, -7.6f), new Color(accent.R, accent.G, accent.B, alpha * 0.72f), 1.5f);
                DrawLine(new Vector2(7.4f, -10.8f), new Vector2(11.8f, -7.6f), new Color(accent.R, accent.G, accent.B, alpha * 0.72f), 1.5f);
                break;
            }

            case FocalAccentShape.Chain:
            {
                var p0 = new Vector2(-6.2f, -12.0f);
                var p1 = new Vector2(0f, -15.0f);
                var p2 = new Vector2(6.2f, -12.0f);
                var c = new Color(accent.R, accent.G, accent.B, alpha * 0.88f);
                DrawLine(p0, p1, c, 1.6f);
                DrawLine(p1, p2, c, 1.6f);
                DrawCircle(p0, 1.8f, c);
                DrawCircle(p1, 2.1f, c);
                DrawCircle(p2, 1.8f, c);
                break;
            }

            default:
                DrawArc(Vector2.Zero, 12.2f, -2.40f, -0.74f, 18, new Color(accent.R, accent.G, accent.B, alpha), 1.9f);
                DrawCircle(new Vector2(0f, -13.2f), 2.0f, new Color(accent.R, accent.G, accent.B, alpha * 0.88f));
                break;
        }

        DrawFlagshipFocalSignature();
    }

    private void DrawFlagshipFocalSignature()
    {
        string modId = _visualEvolution.FocalModId;
        if (!FlagshipModIds.Contains(modId))
            return;

        bool t3 = _visualEvolution.Tier == TowerVisualTier.Tier3;
        bool reducedMotion = IsReducedMotionEnabled();
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * (reducedMotion ? 2.0f : 4.2f));
        float alpha = (t3 ? 0.46f : 0.34f) + pulse * (t3 ? 0.16f : 0.10f);
        Color accent = ModifierVisuals.GetAccent(modId);
        Color c = BlendAccentWithBody(accent, bodyMix: 0.16f, alpha: Mathf.Clamp(alpha, 0f, 1f));

        switch (modId)
        {
            case "focus_lens":
                DrawArc(Vector2.Zero, 5.8f, 0f, Mathf.Tau, 18, c, 1.3f);
                DrawLine(new Vector2(-4.4f, 0f), new Vector2(4.4f, 0f), new Color(c.R, c.G, c.B, c.A * 0.82f), 1.1f);
                DrawLine(new Vector2(0f, -4.4f), new Vector2(0f, 4.4f), new Color(c.R, c.G, c.B, c.A * 0.82f), 1.1f);
                if (t3)
                    DrawArc(Vector2.Zero, 9.0f, -2.00f, -1.14f, 10, new Color(c.R, c.G, c.B, c.A * 0.72f), 1.1f);
                break;

            case "chain_reaction":
            {
                Vector2 p0 = new Vector2(-9.8f, -2.6f);
                Vector2 p1 = new Vector2(-6.2f, 1.2f);
                Vector2 p2 = new Vector2(-2.8f, 4.2f);
                DrawLine(p0, p1, c, 1.2f);
                DrawLine(p1, p2, c, 1.2f);
                DrawCircle(p0, 1.3f, c);
                DrawCircle(p1, 1.5f, c);
                DrawCircle(p2, 1.2f, c);
                break;
            }

            case "blast_core":
                DrawArc(Vector2.Zero, 18.8f, -2.18f, -0.96f, 16, c, 1.6f);
                DrawLine(new Vector2(-4.8f, -20.0f), new Vector2(4.8f, -20.0f), new Color(c.R, c.G, c.B, c.A * 0.82f), 1.3f);
                break;

            case "wildfire":
                DrawCircle(new Vector2(-3.6f, 12.6f), 1.2f, new Color(c.R, c.G, c.B, c.A * 0.92f));
                DrawCircle(new Vector2(0f, 14.2f), 1.5f, new Color(c.R, c.G, c.B, c.A * 0.88f));
                DrawCircle(new Vector2(3.6f, 12.6f), 1.2f, new Color(c.R, c.G, c.B, c.A * 0.92f));
                break;
        }
    }

    private void DrawSupportModAccent()
    {
        if (!_visualEvolution.HasSupportAccent || _visualEvolution.SupportModId.Length == 0)
            return;

        float detailBudget = Mathf.Lerp(1f, 0.54f, _visualChaosLoad);
        if (detailBudget <= 0.22f)
            return;

        bool reducedMotion = IsReducedMotionEnabled();
        float motionScale = reducedMotion ? 0.42f : 1f;
        Color baseAccent = ModifierVisuals.GetAccent(_visualEvolution.SupportModId);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 4.0f * motionScale + 0.6f);
        float pulseStrength = reducedMotion ? 0.06f : 0.10f;
        float alpha = 0.24f + pulse * pulseStrength + _accentLockBoost * 0.13f;
        if (_visualEvolution.SupportReinforced)
            alpha += 0.06f;
        if (_visualEvolution.Tier == TowerVisualTier.Tier3)
            alpha += 0.03f;
        alpha *= detailBudget;

        Color accent = BlendAccentWithBody(baseAccent, bodyMix: 0.32f, alpha: Mathf.Clamp(alpha, 0f, 1f));
        Vector2 anchor = ResolveSupportAnchor(_visualEvolution.SupportChannel);
        float scale = _visualEvolution.SupportReinforced ? 1.08f : 1.0f;
        float width = (1.2f + pulse * 0.16f) * Mathf.Lerp(1f, 0.88f, _visualChaosLoad);

        switch (_visualEvolution.SupportShape)
        {
            case FocalAccentShape.Lens:
                DrawArc(anchor, 3.6f * scale, 0f, Mathf.Tau, 14, accent, width);
                DrawLine(anchor + new Vector2(-2.3f, 0f) * scale, anchor + new Vector2(2.3f, 0f) * scale, new Color(accent.R, accent.G, accent.B, accent.A * 0.78f), width * 0.86f);
                break;

            case FocalAccentShape.Bracket:
                DrawLine(anchor + new Vector2(-2.5f, -1.8f) * scale, anchor + new Vector2(-4.4f, -1.8f) * scale, accent, width);
                DrawLine(anchor + new Vector2(-4.4f, -1.8f) * scale, anchor + new Vector2(-4.4f, 1.6f) * scale, accent, width);
                DrawLine(anchor + new Vector2(2.5f, -1.8f) * scale, anchor + new Vector2(4.4f, -1.8f) * scale, accent, width);
                DrawLine(anchor + new Vector2(4.4f, -1.8f) * scale, anchor + new Vector2(4.4f, 1.6f) * scale, accent, width);
                break;

            case FocalAccentShape.Spike:
            {
                var tip = anchor + new Vector2(0f, -4.6f) * scale;
                var left = anchor + new Vector2(-2.1f, -0.4f) * scale;
                var right = anchor + new Vector2(2.1f, -0.4f) * scale;
                DrawPolygon(new[] { tip, right, left }, new[] { new Color(accent.R, accent.G, accent.B, accent.A * 0.90f) });
                DrawLine(anchor + new Vector2(0f, -0.4f) * scale, anchor + new Vector2(0f, 2.2f) * scale, new Color(accent.R, accent.G, accent.B, accent.A * 0.72f), width * 0.88f);
                break;
            }

            case FocalAccentShape.Chain:
            {
                var p0 = anchor + new Vector2(-3.0f, -1.0f) * scale;
                var p1 = anchor + new Vector2(0f, -2.4f) * scale;
                var p2 = anchor + new Vector2(3.0f, -1.0f) * scale;
                var c = new Color(accent.R, accent.G, accent.B, accent.A * 0.92f);
                DrawLine(p0, p1, c, width * 0.90f);
                DrawLine(p1, p2, c, width * 0.90f);
                DrawCircle(p0, 1.1f * scale, c);
                DrawCircle(p2, 1.1f * scale, c);
                break;
            }

            default:
                DrawArc(anchor, 4.3f * scale, -2.42f, -0.74f, 12, accent, width);
                DrawCircle(anchor + new Vector2(0f, -4.0f) * scale, 1.1f * scale, new Color(accent.R, accent.G, accent.B, accent.A * 0.82f));
                break;
        }

        if (_visualChaosLoad < 0.72f)
            DrawFlagshipSupportSignature(anchor, scale, accent);
    }

    private void DrawTertiaryModHint()
    {
        if (!_visualEvolution.HasTertiaryHint || _visualEvolution.TertiaryModId.Length == 0)
            return;

        float detailBudget = Mathf.Lerp(1f, 0.18f, _visualChaosLoad);
        if (detailBudget <= 0.26f)
            return;

        bool reducedMotion = IsReducedMotionEnabled();
        float motionScale = reducedMotion ? 0.40f : 1f;
        Color baseAccent = ModifierVisuals.GetAccent(_visualEvolution.TertiaryModId);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_idleTime * 3.4f * motionScale + 1.4f);
        float pulseStrength = reducedMotion ? 0.04f : 0.06f;
        float alpha = 0.17f + pulse * pulseStrength + _accentLockBoost * 0.07f;
        if (_visualEvolution.TertiaryReinforced)
            alpha += 0.04f;
        alpha *= detailBudget;

        Color accent = BlendAccentWithBody(baseAccent, bodyMix: 0.56f, alpha: Mathf.Clamp(alpha, 0f, 1f));
        Vector2 anchor = ResolveTertiaryAnchor(_visualEvolution.TertiaryChannel);
        float scale = _visualEvolution.TertiaryReinforced ? 1.08f : 1.0f;

        switch (_visualEvolution.TertiaryShape)
        {
            case FocalAccentShape.Lens:
                DrawArc(anchor, 1.8f * scale, 0f, Mathf.Tau, 10, accent, 0.95f);
                DrawCircle(anchor, 0.85f * scale, new Color(accent.R, accent.G, accent.B, accent.A * 0.82f));
                break;

            case FocalAccentShape.Bracket:
                DrawLine(anchor + new Vector2(-1.3f, -1.1f) * scale, anchor + new Vector2(-1.3f, 1.1f) * scale, accent, 1.0f);
                DrawLine(anchor + new Vector2(-1.3f, 1.1f) * scale, anchor + new Vector2(1.1f, 1.1f) * scale, accent, 1.0f);
                break;

            case FocalAccentShape.Spike:
            {
                var tip = anchor + new Vector2(0f, -2.4f) * scale;
                var left = anchor + new Vector2(-1.2f, -0.2f) * scale;
                var right = anchor + new Vector2(1.2f, -0.2f) * scale;
                DrawPolygon(new[] { tip, right, left }, new[] { new Color(accent.R, accent.G, accent.B, accent.A * 0.90f) });
                break;
            }

            case FocalAccentShape.Chain:
            {
                var p0 = anchor + new Vector2(-1.5f, 0f) * scale;
                var p1 = anchor + new Vector2(1.5f, 0f) * scale;
                DrawLine(p0, p1, accent, 0.95f);
                DrawCircle(p0, 0.75f * scale, accent);
                DrawCircle(p1, 0.75f * scale, accent);
                break;
            }

            default:
                DrawArc(anchor, 2.0f * scale, -2.45f, -0.72f, 10, accent, 0.95f);
                break;
        }

        if (_visualChaosLoad < 0.60f)
            DrawFlagshipTertiarySignature(anchor, scale, accent);
    }

    private void DrawFlagshipSupportSignature(Vector2 anchor, float scale, Color accent)
    {
        string modId = _visualEvolution.SupportModId;
        if (!FlagshipModIds.Contains(modId))
            return;

        float a = Mathf.Clamp(accent.A * 0.84f, 0f, 1f);
        Color c = new Color(accent.R, accent.G, accent.B, a);
        switch (modId)
        {
            case "focus_lens":
                DrawArc(anchor, 2.2f * scale, 0f, Mathf.Tau, 10, c, 0.95f);
                DrawLine(anchor + new Vector2(-1.2f, 0f) * scale, anchor + new Vector2(1.2f, 0f) * scale, c, 0.9f);
                break;
            case "chain_reaction":
                DrawLine(anchor + new Vector2(-2.4f, 0.9f) * scale, anchor + new Vector2(2.4f, 0.9f) * scale, c, 0.95f);
                DrawCircle(anchor + new Vector2(-2.4f, 0.9f) * scale, 0.8f * scale, c);
                DrawCircle(anchor + new Vector2(2.4f, 0.9f) * scale, 0.8f * scale, c);
                break;
            case "blast_core":
                DrawPolygon(new[]
                {
                    anchor + new Vector2(0f, -2.2f) * scale,
                    anchor + new Vector2(1.7f, 0.8f) * scale,
                    anchor + new Vector2(-1.7f, 0.8f) * scale,
                }, new[] { new Color(c.R, c.G, c.B, c.A * 0.86f) });
                break;
            case "wildfire":
                DrawCircle(anchor + new Vector2(-1.2f, 1.4f) * scale, 0.8f * scale, c);
                DrawCircle(anchor + new Vector2(1.2f, 1.2f) * scale, 0.7f * scale, new Color(c.R, c.G, c.B, c.A * 0.90f));
                break;
        }
    }

    private void DrawFlagshipTertiarySignature(Vector2 anchor, float scale, Color accent)
    {
        string modId = _visualEvolution.TertiaryModId;
        if (!FlagshipModIds.Contains(modId))
            return;

        float a = Mathf.Clamp(accent.A * 0.72f, 0f, 1f);
        Color c = new Color(accent.R, accent.G, accent.B, a);
        switch (modId)
        {
            case "focus_lens":
                DrawCircle(anchor, 0.62f * scale, c);
                DrawArc(anchor, 1.25f * scale, 0f, Mathf.Tau, 8, c, 0.8f);
                break;
            case "chain_reaction":
                DrawLine(anchor + new Vector2(-1.2f, 0f) * scale, anchor + new Vector2(1.2f, 0f) * scale, c, 0.85f);
                DrawCircle(anchor + new Vector2(-1.2f, 0f) * scale, 0.52f * scale, c);
                DrawCircle(anchor + new Vector2(1.2f, 0f) * scale, 0.52f * scale, c);
                break;
            case "blast_core":
                DrawPolygon(new[]
                {
                    anchor + new Vector2(0f, -1.4f) * scale,
                    anchor + new Vector2(1.0f, 0.7f) * scale,
                    anchor + new Vector2(-1.0f, 0.7f) * scale,
                }, new[] { new Color(c.R, c.G, c.B, c.A * 0.90f) });
                break;
            case "wildfire":
                DrawCircle(anchor + new Vector2(-0.7f, 0.5f) * scale, 0.52f * scale, c);
                DrawCircle(anchor + new Vector2(0.7f, 0.5f) * scale, 0.52f * scale, c);
                break;
        }
    }

    private Vector2 ResolveSupportAnchor(AccentChannel channel) => channel switch
    {
        AccentChannel.Base => new Vector2(0f, 15.8f),
        AccentChannel.Inner => new Vector2(-5.8f, 8.8f),
        AccentChannel.Top => new Vector2(0f, -18.4f),
        _ => new Vector2(-12.8f, -9.4f),
    };

    private Vector2 ResolveTertiaryAnchor(AccentChannel channel) => channel switch
    {
        AccentChannel.Base => new Vector2(-1.0f, 18.4f),
        AccentChannel.Left => new Vector2(-9.2f, 11.2f),
        AccentChannel.Top => new Vector2(-4.0f, -17.2f),
        _ => new Vector2(-3.8f, 9.8f),
    };

    private void DrawEvolutionTransitionFx()
    {
        if (_channelSurgeBoost <= 0.001f || _transitionChannel == AccentChannel.None)
            return;

        Vector2 anchor = ResolveTransitionAnchor(_transitionChannel);
        float strength = _channelSurgeBoost;
        Color surge = new Color(_transitionAccent.R, _transitionAccent.G, _transitionAccent.B, 0.06f + strength * 0.30f);
        float width = 1.0f + strength * 1.5f;

        DrawLine(Vector2.Zero, anchor, surge, width);
        DrawCircle(anchor, 1.1f + strength * 1.8f, new Color(surge.R, surge.G, surge.B, 0.18f + strength * 0.42f));
        DrawCircle(Vector2.Zero, 1.6f + strength * 1.0f, new Color(surge.R, surge.G, surge.B, 0.10f + strength * 0.22f));
    }

    private Vector2 ResolveTransitionAnchor(AccentChannel channel) => channel switch
    {
        AccentChannel.Top => new Vector2(0f, -18.8f),
        AccentChannel.Left => new Vector2(-12.2f, -8.8f),
        AccentChannel.Base => new Vector2(0f, 17.0f),
        AccentChannel.Inner => new Vector2(-3.8f, 9.2f),
        _ => Vector2.Zero,
    };

    private Color BlendAccentWithBody(Color accent, float bodyMix, float alpha)
    {
        return new Color(
            Mathf.Lerp(accent.R, BodyColor.R, bodyMix),
            Mathf.Lerp(accent.G, BodyColor.G, bodyMix),
            Mathf.Lerp(accent.B, BodyColor.B, bodyMix),
            alpha);
    }

    private void DrawTargetLockLine()
    {
        if (_lockLineRemaining <= 0f) return;
        if (_visualChaosLoad >= 0.86f) return;
        var localTo = ToLocal(_lockLineTargetGlobal);
        float t = _lockLineRemaining / 0.15f;
        float alpha = (0.10f + 0.20f * t) * Mathf.Lerp(1f, 0.56f, _visualChaosLoad);
        var c = new Color(BodyColor.R, BodyColor.G, BodyColor.B, alpha);
        DrawLine(Vector2.Zero, localTo, c, 1.6f);
    }
    private void DrawChargeArc(bool overlayPass = false)
    {
        if (AttackInterval <= 0f) return;

        int tierLevel = _visualEvolution.Tier switch
        {
            TowerVisualTier.Tier1 => 1,
            TowerVisualTier.Tier2 => 2,
            TowerVisualTier.Tier3 => 3,
            _ => 0,
        };
        if (overlayPass && tierLevel <= 0)
            return;

        float fill = Mathf.Clamp(1f - Cooldown / AttackInterval, 0f, 1f);
        float radius = overlayPass ? (22.6f + tierLevel * 0.9f) : 21f;

        // When full (mine cap reached or pre-fire), draw the background ring brighter
        // and skip the charge arc: drawing from -PI/2 to -PI/2+2PI causes a seam artifact
        // at the top where the arc start and end points coincide.
        bool atFull = fill >= 0.999f;

        float ringAlpha;
        float ringWidth;
        Color ringColor;
        if (overlayPass)
        {
            float lift = 0.28f;
            ringAlpha = 0.16f + tierLevel * 0.07f + (atFull ? 0.10f : 0f);
            ringWidth = 1.55f + tierLevel * 0.22f;
            ringColor = new Color(
                Mathf.Lerp(BodyColor.R, 1f, lift),
                Mathf.Lerp(BodyColor.G, 1f, lift),
                Mathf.Lerp(BodyColor.B, 1f, lift),
                ringAlpha);
        }
        else
        {
            ringAlpha = atFull ? 0.50f : 0.16f;
            ringWidth = 2f;
            ringColor = new Color(BodyColor.R, BodyColor.G, BodyColor.B, ringAlpha);
        }
        if (atFull)
            ringColor = new Color(ringColor.R, ringColor.G, ringColor.B, ringColor.A + 0.10f * _visualChaosLoad);

        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 48, ringColor, ringWidth);

        // Filled charge arc - clockwise from top (only when not fully charged)
        if (fill > 0.01f && !atFull)
        {
            float start = -Mathf.Pi / 2f;
            float end = start + fill * Mathf.Tau;
            float arcAlpha = overlayPass ? (0.64f + tierLevel * 0.06f) : 0.88f;
            arcAlpha *= overlayPass ? 1f : Mathf.Lerp(1f, 0.92f, _visualChaosLoad);
            float arcWidth = overlayPass ? (1.95f + tierLevel * 0.24f) : 2.5f;
            DrawArc(Vector2.Zero, radius, start, end, 48,
                new Color(ringColor.R, ringColor.G, ringColor.B, arcAlpha), arcWidth);
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
        accent = EnsureReadableSpectacleAccent(accent);
        float nearFull = Mathf.InverseLerp(0.72f, 1f, meter);
        float readyPulse = meter >= 0.98f ? (0.5f + 0.5f * Mathf.Sin(_idleTime * 8.5f)) : 0f;
        float auraBudget = Mathf.Lerp(1f, 0.60f, _visualChaosLoad);
        float ringBudget = Mathf.Lerp(1f, 0.90f, _visualChaosLoad);
        float meterRadius = 30f;
        float meterPresence = Mathf.InverseLerp(0.05f, 0.20f, meter);
        float persistentLane = Mathf.Max(meterPresence, pulse * 0.65f);
        float laneVisibility = Mathf.Clamp(persistentLane, 0f, 1f);
        float lowMeterFade = Mathf.InverseLerp(0.03f, 0.16f, meter);
        float glow = 0.08f + meter * 0.26f + pulse * 0.42f + chargePulse * 0.22f + nearFull * 0.10f + readyPulse * 0.20f;
        float glowRadius = meterRadius + 6.4f + pulse * 2.4f + nearFull * 1.5f + readyPulse * 1.8f;
        float haloAlpha = Mathf.Clamp(glow * 0.11f * auraBudget * (0.20f + lowMeterFade * 0.80f) * Mathf.Lerp(0.25f, 1f, laneVisibility), 0f, 0.15f);
        if (haloAlpha > 0.001f)
        {
            DrawArc(
                Vector2.Zero,
                glowRadius,
                0f,
                Mathf.Tau,
                52,
                new Color(accent.R, accent.G, accent.B, haloAlpha),
                1.6f + nearFull * 0.4f + chargePulse * 0.5f);
            DrawArc(
                Vector2.Zero,
                glowRadius - 2.6f,
                0f,
                Mathf.Tau,
                48,
                new Color(accent.R, accent.G, accent.B, haloAlpha * 0.58f),
                1.0f);
        }

        float ringAlpha = 0.34f + meter * 0.44f + pulse * 0.34f + chargePulse * 0.16f;
        float baseRingWidth = 2.1f + nearFull * 0.8f;
        float baseReadAlpha = Mathf.Clamp(ringAlpha * ringBudget, 0.30f, 0.95f);

        if (laneVisibility > 0.001f)
        {
            // Backplate only appears once the meter has established a visible lane.
            // Keep this light-tinted to avoid a dark/black halo on neon maps.
            Color plateTint = accent.Lerp(Colors.White, 0.62f);
            float plateAlpha = (0.06f + meter * 0.05f + _visualChaosLoad * 0.03f) * (0.35f + lowMeterFade * 0.65f) * laneVisibility;
            DrawArc(
                Vector2.Zero,
                meterRadius,
                0f,
                Mathf.Tau,
                48,
                new Color(plateTint.R, plateTint.G, plateTint.B, plateAlpha),
                baseRingWidth + 0.9f);
            DrawArc(
                Vector2.Zero,
                meterRadius,
                0f,
                Mathf.Tau,
                48,
                new Color(accent.R, accent.G, accent.B, baseReadAlpha * (0.46f + nearFull * 0.22f) * laneVisibility),
                baseRingWidth);
        }

        if (meter > 0.01f)
        {
            float start = -Mathf.Pi / 2f;
            float end = start + meter * Mathf.Tau;
            Color arcColor = accent.Lerp(Colors.White, 0.22f + nearFull * 0.20f);
            float arcAlpha = Mathf.Clamp(baseReadAlpha + 0.12f + nearFull * 0.10f, 0.44f, 1f);
            float arcWidth = 3.2f + nearFull * 1.4f + chargePulse * 0.8f;
            DrawArc(Vector2.Zero, meterRadius, start, end, 48, new Color(arcColor.R, arcColor.G, arcColor.B, arcAlpha), arcWidth);

            // Arc head marker helps meter progress read instantly in chaos.
            Vector2 tip = new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * meterRadius;
            float tipAlpha = Mathf.Clamp(0.48f + nearFull * 0.24f + chargePulse * 0.18f, 0f, 1f) * ringBudget;
            DrawCircle(tip, 2.0f + nearFull * 1.0f, new Color(arcColor.R, arcColor.G, arcColor.B, tipAlpha));
        }

        if (nearFull > 0.001f)
        {
            float shimmer = 0.40f + 0.60f * (0.5f + 0.5f * Mathf.Sin(_idleTime * 10f));
            float alpha = nearFull * (0.12f + 0.16f * shimmer) * Mathf.Lerp(1f, 0.84f, _visualChaosLoad);
            DrawArc(Vector2.Zero, meterRadius + 3.6f, 0f, Mathf.Tau, 48, new Color(accent.R, accent.G, accent.B, alpha * auraBudget), 1.6f);
        }
    }

    private static Color EnsureReadableSpectacleAccent(Color accent)
    {
        bool invalid = float.IsNaN(accent.R) || float.IsNaN(accent.G) || float.IsNaN(accent.B);
        if (invalid)
            return new Color(0.60f, 0.88f, 1.00f);

        float luma = accent.R * 0.2126f + accent.G * 0.7152f + accent.B * 0.0722f;
        if (luma < 0.12f)
            return accent.Lerp(new Color(0.60f, 0.88f, 1.00f), 0.72f);
        return accent;
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

    private void DrawRocketLauncher()
    {
        var ember = new Color(0.96f, 0.36f, 0.10f);
        var dark = LiftInnerTone(new Color(0.20f, 0.08f, 0.04f));
        var glow = new Color(1.00f, 0.80f, 0.42f);
        float shot = ShotKick();
        float sway = Mathf.Sin(_idleTime * 2.4f) * 0.8f;
        float recoil = shot * 4.6f;
        float tubeY = -24f + recoil;

        DrawCircle(Vector2.Zero, 24f, new Color(ember.R, ember.G, ember.B, 0.07f));
        DrawCircle(Vector2.Zero, 17f, new Color(ember.R, ember.G, ember.B, 0.14f));

        DrawPolygon(RegularPoly(8, 13.8f, 0f), new[] { ember });
        DrawPolygon(RegularPoly(8, 11.2f, 0f), new[] { dark });
        DrawCircle(Vector2.Zero, 3.5f, new Color(glow.R, glow.G, glow.B, 0.84f));

        DrawRect(new Rect2(-4.9f + sway, tubeY, 9.8f, 18f), ember);
        DrawRect(new Rect2(-3.1f + sway, tubeY + 1.3f, 6.2f, 15.4f), dark);
        DrawPolygon(new[]
        {
            new Vector2(0f + sway, tubeY - 8.3f),
            new Vector2(6.3f + sway, tubeY + 0.7f),
            new Vector2(-6.3f + sway, tubeY + 0.7f),
        }, new[] { glow });
        DrawCircle(new Vector2(0f + sway, tubeY - 0.2f), 1.8f, new Color(1f, 0.94f, 0.78f, 0.95f));

        DrawPolygon(new[]
        {
            new Vector2(-4.9f + sway, tubeY + 13.2f),
            new Vector2(-8.7f + sway, tubeY + 18.8f),
            new Vector2(-2.2f + sway, tubeY + 17.3f),
        }, new[] { new Color(ember.R, ember.G, ember.B, 0.86f) });
        DrawPolygon(new[]
        {
            new Vector2(4.9f + sway, tubeY + 13.2f),
            new Vector2(8.7f + sway, tubeY + 18.8f),
            new Vector2(2.2f + sway, tubeY + 17.3f),
        }, new[] { new Color(ember.R, ember.G, ember.B, 0.86f) });

        if (shot > 0.02f)
        {
            var nozzle = new Vector2(0f + sway, tubeY + 18.3f);
            DrawCircle(nozzle, 6.2f + shot * 4.6f, new Color(1f, 0.55f, 0.18f, 0.20f + shot * 0.20f));
            DrawCircle(nozzle, 2.8f + shot * 2.2f, new Color(1f, 0.90f, 0.55f, 0.76f));
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

    private void DrawUndertowEngine()
    {
        var sea = new Color(0.08f, 0.64f, 0.86f);
        var deep = LiftInnerTone(new Color(0.02f, 0.10f, 0.18f));
        var foam = new Color(0.78f, 0.96f, 1.00f);
        float shot = ShotKick();
        float pulse = 0.58f + 0.42f * Mathf.Sin(_idleTime * 4.6f);
        float drawIn = shot * 3.0f;
        float swirl = _idleTime * 2.2f;

        DrawCircle(Vector2.Zero, 25f, new Color(sea.R, sea.G, sea.B, 0.08f + 0.04f * pulse));
        DrawCircle(Vector2.Zero, 17f, new Color(sea.R, sea.G, sea.B, 0.15f + 0.05f * pulse));

        // Outer flow frame.
        DrawPolygon(RegularPoly(10, 12.4f, Mathf.Pi / 10f), new[] { sea });
        DrawPolygon(RegularPoly(10, 9.6f, Mathf.Pi / 10f), new[] { deep });

        // Reverse-current vanes.
        for (int i = 0; i < 4; i++)
        {
            float a = swirl + i * Mathf.Tau / 4f;
            Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 perp = new(-dir.Y, dir.X);
            Vector2 tip = dir * (17f - drawIn);
            DrawPolygon(new[]
            {
                tip + perp * 2.2f,
                tip - perp * 2.2f,
                tip - dir * 7.5f,
            }, new[] { new Color(foam.R, foam.G, foam.B, 0.72f + shot * 0.16f) });
        }

        // Implosive rings.
        float ringRadius = 16.5f - shot * 2.8f;
        DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 46, new Color(sea.R, sea.G, sea.B, 0.34f + shot * 0.20f), 1.8f);
        DrawArc(Vector2.Zero, ringRadius * 0.64f, 0f, Mathf.Tau, 32, new Color(foam.R, foam.G, foam.B, 0.30f + shot * 0.16f), 1.3f);
        DrawCircle(Vector2.Zero, 3.0f + shot * 0.75f, new Color(1f, 1f, 1f, 0.90f));
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
        ringAlpha += 0.10f * _visualChaosLoad;
        ringWidth += 0.18f * _visualChaosLoad;
        float coreAlpha = (mobile ? 0.78f : 0.62f) + 0.10f * _visualChaosLoad;
        var ring = new Color(BodyColor.R, BodyColor.G, BodyColor.B, ringAlpha);
        DrawArc(Vector2.Zero, 13.5f, 0f, Mathf.Tau, 36, ring, ringWidth);
        DrawCircle(Vector2.Zero, 1.6f, new Color(1f, 1f, 1f, coreAlpha));
    }
}

