using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Provides fade-to-black transitions between scenes.
/// Layer = 100 so it renders above everything including tooltips.
/// </summary>
public partial class Transition : CanvasLayer
{
    public static Transition Instance { get; private set; } = null!;

    private ColorRect _overlay = null!;
    private bool _busy = false;

    public override void _Ready()
    {
        Instance = this;
        Layer = 100;
        ProcessMode = ProcessModeEnum.Always; // survive paused state

        _overlay = new ColorRect();
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _overlay.Color = Colors.Black;
        _overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_overlay);

        // Fade in: new scene just loaded — fade overlay out
        var tween = CreateTween();
        tween.TweenProperty(_overlay, "color", Colors.Transparent, 0.30f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    public void FadeToScene(string scenePath)
    {
        if (_busy) return;
        _busy = true;
        _overlay.Color = Colors.Transparent;
        var tween = CreateTween();
        tween.TweenProperty(_overlay, "color", Colors.Black, 0.25f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            _busy = false;
            GetTree().ChangeSceneToFile(scenePath);
            // Overlay stays black; _Ready() on new scene's Transition will fade it out.
            // But Transition is an autoload — _Ready() only runs once.
            // So we must explicitly schedule the fade-out after the scene change.
        }));
        // After scene change (deferred to next frame), fade out
        tween.TweenInterval(0.05f);
        tween.TweenCallback(Callable.From(FadeOut));
    }

    private void FadeOut()
    {
        _overlay.Color = Colors.Black;
        var tween = CreateTween();
        tween.TweenProperty(_overlay, "color", Colors.Transparent, 0.30f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }
}
