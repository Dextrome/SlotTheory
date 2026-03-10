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
    private ColorRect _tintOverlay = null!;
    private ColorRect _scanline = null!;
    private ColorRect _sweep = null!;
    private WorldEnvironment _worldEnvironment = null!;

    private bool _busy;
    private bool _postFxReducedMotion;
    private Vector2 _cachedViewportSize = Vector2.Zero;

    private const float FadeOutSeconds = 0.26f;
    private const float FadeInSeconds = 0.30f;

    public override void _Ready()
    {
        Instance = this;
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always;

        _postFxReducedMotion = IsReducedMotionEnabled();
        SetupPostFx();
        SetupTransitionLayers();
        RefreshFxLayout(force: true);
        PlayFadeIn();
    }

    public override void _Process(double delta)
    {
        RefreshFxLayout();

        bool reducedMotion = IsReducedMotionEnabled();
        if (reducedMotion == _postFxReducedMotion)
            return;

        _postFxReducedMotion = reducedMotion;
        _worldEnvironment.Environment = BuildEnvironment(_postFxReducedMotion);
    }

    public void FadeToScene(string scenePath)
    {
        if (_busy)
            return;

        _busy = true;

        bool reducedMotion = IsReducedMotionEnabled();
        _overlay.Color = Colors.Transparent;
        _tintOverlay.Color = new Color(0.15f, 0.05f, 0.26f, 0f);

        RefreshFxLayout(force: true);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_overlay, "color", Colors.Black, reducedMotion ? FadeOutSeconds * 0.72f : FadeOutSeconds)
             .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(_tintOverlay, "color:a", reducedMotion ? 0.08f : 0.28f, reducedMotion ? FadeOutSeconds * 0.72f : FadeOutSeconds * 0.88f)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        if (!reducedMotion)
            PlayTransitionAccents(FadeOutSeconds);

        tween.Chain();
        tween.TweenCallback(Callable.From(() =>
        {
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
            Environment = BuildEnvironment(_postFxReducedMotion)
        };
        AddChild(_worldEnvironment);
        MoveChild(_worldEnvironment, 0);
    }

    private void SetupTransitionLayers()
    {
        _overlay = new ColorRect
        {
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        _tintOverlay = new ColorRect
        {
            Color = new Color(0.15f, 0.05f, 0.26f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _tintOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_tintOverlay);

        _scanline = new ColorRect
        {
            Color = new Color(0.65f, 0.95f, 1.00f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_scanline);

        _sweep = new ColorRect
        {
            Color = new Color(0.35f, 1.00f, 0.92f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            RotationDegrees = 15f,
        };
        AddChild(_sweep);
    }

    private Environment BuildEnvironment(bool reducedMotion)
    {
        var env = new Environment();

        // Bloom / glow
        env.Set("glow_enabled", true);
        env.Set("glow_normalized", true);
        env.Set("glow_intensity", reducedMotion ? 0.58f : 0.78f);
        env.Set("glow_strength", reducedMotion ? 0.68f : 1.02f);
        env.Set("glow_bloom", reducedMotion ? 0.04f : 0.15f);
        env.Set("glow_hdr_threshold", 0.62f);
        env.Set("glow_hdr_scale", 1.0f);
        env.Set("glow_blend_mode", 0); // additive
        env.Set("glow_levels/1", 0.90f);
        env.Set("glow_levels/2", 0.65f);
        env.Set("glow_levels/3", 0.40f);

        // Mild global grade to unify neon palette
        env.Set("adjustment_enabled", true);
        env.Set("adjustment_brightness", 1.01f);
        env.Set("adjustment_contrast", 1.06f);
        env.Set("adjustment_saturation", 1.05f);

        return env;
    }

    private void PlayFadeIn()
    {
        bool reducedMotion = IsReducedMotionEnabled();

        _overlay.Color = Colors.Black;
        _tintOverlay.Color = new Color(0.15f, 0.05f, 0.26f, reducedMotion ? 0.06f : 0.22f);

        RefreshFxLayout(force: true);

        // Start near bottom to sweep upward on fade in.
        _scanline.Position = new Vector2(0f, _cachedViewportSize.Y + 8f);
        _scanline.Color = new Color(0.65f, 0.95f, 1.00f, 0f);

        _sweep.Position = new Vector2(_cachedViewportSize.X + _sweep.Size.X, _cachedViewportSize.Y * 0.5f);
        _sweep.Color = new Color(0.35f, 1.00f, 0.92f, 0f);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_overlay, "color:a", 0f, reducedMotion ? FadeInSeconds * 0.72f : FadeInSeconds)
             .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_tintOverlay, "color:a", 0f, reducedMotion ? FadeInSeconds * 0.72f : FadeInSeconds * 0.92f)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        if (!reducedMotion)
        {
            var fx = CreateTween();
            fx.SetParallel(true);
            fx.TweenProperty(_scanline, "color:a", 0.32f, 0.06f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            fx.TweenProperty(_scanline, "position:y", -10f, FadeInSeconds * 0.88f)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            fx.TweenProperty(_sweep, "color:a", 0.18f, 0.09f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            fx.TweenProperty(_sweep, "position:x", -_sweep.Size.X, FadeInSeconds)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            fx.Chain();
            fx.TweenProperty(_scanline, "color:a", 0f, 0.12f);
            fx.TweenProperty(_sweep, "color:a", 0f, 0.12f);
        }
    }

    private void PlayTransitionAccents(float duration)
    {
        _scanline.Position = new Vector2(0f, -10f);
        _scanline.Color = new Color(0.65f, 0.95f, 1.00f, 0f);

        _sweep.Position = new Vector2(-_sweep.Size.X, _cachedViewportSize.Y * 0.5f);
        _sweep.Color = new Color(0.35f, 1.00f, 0.92f, 0f);

        var fx = CreateTween();
        fx.SetParallel(true);
        fx.TweenProperty(_scanline, "color:a", 0.34f, 0.05f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        fx.TweenProperty(_scanline, "position:y", _cachedViewportSize.Y + 10f, duration * 0.86f)
          .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        fx.TweenProperty(_sweep, "color:a", 0.26f, 0.08f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        fx.TweenProperty(_sweep, "position:x", _cachedViewportSize.X + _sweep.Size.X, duration)
          .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        fx.Chain();
        fx.TweenProperty(_scanline, "color:a", 0f, 0.10f);
        fx.TweenProperty(_sweep, "color:a", 0f, 0.10f);
    }

    private void RefreshFxLayout(bool force = false)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        if (!force && _cachedViewportSize.IsEqualApprox(size))
            return;

        _cachedViewportSize = size;

        _scanline.Size = new Vector2(size.X, 6f);
        _scanline.Position = new Vector2(0f, -10f);

        _sweep.Size = new Vector2(Mathf.Max(180f, size.X * 0.16f), size.Y * 1.8f);
        _sweep.PivotOffset = _sweep.Size * 0.5f;
        _sweep.Position = new Vector2(-_sweep.Size.X, size.Y * 0.5f);
        _sweep.RotationDegrees = 15f;
    }

    private static bool IsReducedMotionEnabled()
    {
        return SettingsManager.Instance?.ReducedMotion ?? false;
    }
}
