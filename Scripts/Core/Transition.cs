using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton.
/// Provides stylized scene transitions and hosts global post-processing.
/// </summary>
public partial class Transition : CanvasLayer
{
    public static Transition Instance { get; private set; } = null!;

    private ColorRect _overlay = null!;
    private ColorRect _gradeOverlay = null!;
    private ColorRect _tintOverlay = null!;
    private ColorRect _scanline = null!;
    private ColorRect _sweep = null!;
    private WorldEnvironment _worldEnvironment = null!;

    private bool _busy;
    private bool _postFxReducedMotion;
    private bool _postFxEnabled;
    private Vector2 _cachedViewportSize = Vector2.Zero;

    private const float FadeOutSeconds = 0.20f;
    private const float FadeInSeconds  = 0.26f;

    private static readonly Color TintColor     = new Color(0.15f, 0.05f, 0.26f, 0f);
    private static readonly Color GradeColor    = new Color(0.20f, 0.08f, 0.30f, 0f);
    private static readonly Color ScanlineColor = new Color(0.65f, 0.95f, 1.00f, 0f);
    private static readonly Color SweepColor    = new Color(0.35f, 1.00f, 0.92f, 0f);

    /// <summary>Reduced-motion timing multipliers. Scanline and sweep are omitted entirely.</summary>
    private static class ReducedMotion
    {
        public const float FadeScale = 0.72f;  // primary overlay fade runs at 72% duration
        public const float TintScale = 0.88f;  // tint overlay duration fraction (non-reduced path)
    }

    public override void _Ready()
    {
        Instance = this;
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        _postFxReducedMotion = IsReducedMotionEnabled();
        _postFxEnabled = IsPostFxEnabled();
        SetupPostFx();
        SetupTransitionLayers();
        ApplyPostFxState();
        RefreshFxLayout(force: true);
        PlayFadeIn();
    }

    public override void _Process(double delta)
    {
        RefreshFxLayout();

        bool reducedMotion = IsReducedMotionEnabled();
        bool postFxEnabled = IsPostFxEnabled();
        if (reducedMotion == _postFxReducedMotion &&
            postFxEnabled == _postFxEnabled)
            return;

        _postFxReducedMotion = reducedMotion;
        _postFxEnabled = postFxEnabled;
        ApplyPostFxState();
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null!;
    }

    public void FadeToScene(string scenePath)
    {
        if (_busy)
            return;

        _busy = true;

        bool reducedMotion = IsReducedMotionEnabled();
        bool postFxEnabled = IsPostFxEnabled();
        _overlay.Color = Colors.Transparent;
        _tintOverlay.Color = TintColor;

        RefreshFxLayout(force: true);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_overlay, "color", Colors.Black, reducedMotion ? FadeOutSeconds * ReducedMotion.FadeScale : FadeOutSeconds)
             .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        if (postFxEnabled)
        {
            tween.TweenProperty(_tintOverlay, "color:a", reducedMotion ? 0.08f : 0.28f, reducedMotion ? FadeOutSeconds * ReducedMotion.FadeScale : FadeOutSeconds * ReducedMotion.TintScale)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }

        if (postFxEnabled && !reducedMotion)
            PlayTransitionAccents(FadeOutSeconds);

        tween.Chain();
        tween.TweenCallback(Callable.From(() =>
        {
            // Force-clear any pause left over from gameplay (e.g. Space pressed during the fade).
            GetTree().Paused = false;
            Engine.TimeScale  = 1.0;
            GetTree().ChangeSceneToFile(scenePath);
        }));
        tween.TweenInterval(0.05f);
        tween.TweenCallback(Callable.From(() =>
        {
            PlayFadeIn();
            _busy = false;
        }));
    }

    private void SetupPostFx()
    {
        _worldEnvironment = new WorldEnvironment
        {
            Environment = BuildEnvironment(_postFxReducedMotion, _postFxEnabled)
        };
        AddChild(_worldEnvironment);
        MoveChild(_worldEnvironment, 0);
    }

    private void SetupTransitionLayers()
    {
        _gradeOverlay = new ColorRect
        {
            Color = GradeColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _gradeOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_gradeOverlay);

        _overlay = new ColorRect
        {
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        _tintOverlay = new ColorRect
        {
            Color = TintColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _tintOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_tintOverlay);

        _scanline = new ColorRect
        {
            Color = ScanlineColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_scanline);

        _sweep = new ColorRect
        {
            Color = SweepColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            RotationDegrees = 15f,
        };
        AddChild(_sweep);
    }

    private static Environment? BuildEnvironment(bool reducedMotion, bool postFxEnabled)
    {
        if (!postFxEnabled)
            return null;

        var env = new Environment();

        // Bloom / glow
        float glowIntensity = reducedMotion ? 0.58f : 0.78f;
        float glowStrength = reducedMotion ? 0.68f : 1.02f;
        float glowBloom = reducedMotion ? 0.04f : 0.15f;
        float glowThreshold = 0.62f;
        float glowLvl1 = 0.90f;
        float glowLvl2 = 0.65f;
        float glowLvl3 = 0.40f;
        float gradeBrightness = 1.01f;
        float gradeContrast = 1.06f;
        float gradeSaturation = 1.05f;

        env.Set("glow_enabled", true);
        env.Set("glow_normalized", true);
        env.Set("glow_intensity", glowIntensity);
        env.Set("glow_strength", glowStrength);
        env.Set("glow_bloom", glowBloom);
        env.Set("glow_hdr_threshold", glowThreshold);
        env.Set("glow_hdr_scale", 1.0f);
        env.Set("glow_blend_mode", 0); // additive
        env.Set("glow_levels/1", glowLvl1);
        env.Set("glow_levels/2", glowLvl2);
        env.Set("glow_levels/3", glowLvl3);

        // Mild global grade to unify neon palette
        env.Set("adjustment_enabled", true);
        env.Set("adjustment_brightness", gradeBrightness);
        env.Set("adjustment_contrast", gradeContrast);
        env.Set("adjustment_saturation", gradeSaturation);

        return env;
    }

    private void PlayFadeIn()
    {
        bool reducedMotion = IsReducedMotionEnabled();
        bool postFxEnabled = IsPostFxEnabled();

        _overlay.Color = Colors.Black;
        _tintOverlay.Color = TintColor with { A = postFxEnabled ? (reducedMotion ? 0.06f : 0.22f) : 0f };

        RefreshFxLayout(force: true);

        // Start near bottom to sweep upward on fade in.
        _scanline.Position = new Vector2(0f, _cachedViewportSize.Y + 8f);
        _scanline.Color = ScanlineColor;

        _sweep.Position = new Vector2(_cachedViewportSize.X + _sweep.Size.X, _cachedViewportSize.Y * 0.5f);
        _sweep.Color = SweepColor;

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_overlay, "color:a", 0f, reducedMotion ? FadeInSeconds * ReducedMotion.FadeScale : FadeInSeconds)
             .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        if (postFxEnabled)
        {
            tween.TweenProperty(_tintOverlay, "color:a", 0f, reducedMotion ? FadeInSeconds * ReducedMotion.FadeScale : FadeInSeconds * 0.92f)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }

        if (postFxEnabled && !reducedMotion)
        {
            var fx = CreateTween();
            fx.SetParallel(true);
            fx.TweenProperty(_scanline, "color:a", 0.30f, 0.12f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            fx.TweenProperty(_scanline, "position:y", -10f, FadeInSeconds * 0.85f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            fx.TweenProperty(_sweep, "color:a", 0.16f, 0.14f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            fx.TweenProperty(_sweep, "position:x", -_sweep.Size.X, FadeInSeconds * 0.85f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            fx.Chain();
            fx.TweenProperty(_scanline, "color:a", 0f, 0.14f);
            fx.TweenProperty(_sweep, "color:a", 0f, 0.14f);
        }
    }

    private void PlayTransitionAccents(float duration)
    {
        _scanline.Position = new Vector2(0f, -10f);
        _scanline.Color = ScanlineColor;

        _sweep.Position = new Vector2(-_sweep.Size.X, _cachedViewportSize.Y * 0.5f);
        _sweep.Color = SweepColor;

        var fx = CreateTween();
        fx.SetParallel(true);
        fx.TweenProperty(_scanline, "color:a", 0.30f, 0.10f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        fx.TweenProperty(_scanline, "position:y", _cachedViewportSize.Y + 10f, duration * 1.08f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        fx.TweenProperty(_sweep, "color:a", 0.22f, 0.12f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        fx.TweenProperty(_sweep, "position:x", _cachedViewportSize.X + _sweep.Size.X, duration * 1.12f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        fx.Chain();
        fx.TweenProperty(_scanline, "color:a", 0f, 0.16f);
        fx.TweenProperty(_sweep, "color:a", 0f, 0.16f);
    }

    private void RefreshFxLayout(bool force = false)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (!force && _cachedViewportSize.IsEqualApprox(size))
            return;

        _cachedViewportSize = size;

        _scanline.Size = new Vector2(size.X, 8f);
        _scanline.Position = new Vector2(0f, -10f);

        _sweep.Size = new Vector2(Mathf.Max(220f, size.X * 0.20f), size.Y * 1.8f);
        _sweep.PivotOffset = _sweep.Size * 0.5f;
        _sweep.Position = new Vector2(-_sweep.Size.X, size.Y * 0.5f);
        _sweep.RotationDegrees = 15f;
    }

    private void ApplyPostFxState()
    {
        _worldEnvironment.Environment = BuildEnvironment(_postFxReducedMotion, _postFxEnabled);
        float gradeAlpha = _postFxEnabled
            ? (_postFxReducedMotion ? 0.028f : 0.04f)
            : 0f;
        _gradeOverlay.Color = GradeColor with { A = gradeAlpha };
        _gradeOverlay.Visible = _postFxEnabled;
        _tintOverlay.Visible = _postFxEnabled;
        _scanline.Visible = _postFxEnabled;
        _sweep.Visible = _postFxEnabled;
    }

    public void RefreshPostFxFromSettings()
    {
        _postFxReducedMotion = IsReducedMotionEnabled();
        _postFxEnabled = IsPostFxEnabled();
        ApplyPostFxState();
    }

    private static bool IsReducedMotionEnabled()
    {
        return SettingsManager.Instance?.ReducedMotion ?? false;
    }

    private static bool IsPostFxEnabled()
    {
        return SettingsManager.Instance?.PostFxEnabled ?? true;
    }
}
