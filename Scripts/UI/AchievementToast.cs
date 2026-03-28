using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Autoload singleton. Subscribes to AchievementManager.AchievementUnlocked and
/// shows a brief toast notification in the bottom-right corner of the screen.
/// Multiple unlocks queue and show sequentially.
/// </summary>
public partial class AchievementToast : CanvasLayer
{
    public static AchievementToast? Instance { get; private set; }

    private const float ShowDuration  = 4.8f;
    private const float SlideDuration = 0.24f;
    private const int   PanelW        = 390;
    private const int   PanelH        = 110;
    private const int   MarginRight   = 16;
    private const int   MarginBottom  = 16;
    private const float EnterShiftPx  = 34f;

    private Panel?   _panel;
    private Label?   _title;
    private Label?   _desc;
    private Control? _iconHolder;
    private ColorRect? _flash;
    private float   _timer;
    private double  _lastTickSec;
    private bool    _visible;
    private readonly System.Collections.Generic.Queue<string> _queue = new();

    public override void _Ready()
    {
        Instance = this;
        Layer = 20;

        _panel = new Panel();
        _panel.CustomMinimumSize = new Vector2(PanelW, PanelH);
        // Anchor bottom-right, offset so it starts off-screen (slides in)
        _panel.AnchorLeft   = 1f;
        _panel.AnchorRight  = 1f;
        _panel.AnchorTop    = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft   = -(PanelW + MarginRight);
        _panel.OffsetRight  = -MarginRight;
        _panel.OffsetTop    = -(PanelH + MarginBottom);
        _panel.OffsetBottom = -MarginBottom;
        _panel.Modulate     = new Color(1, 1, 1, 0); // start invisible
        _panel.MouseFilter  = Control.MouseFilterEnum.Ignore;
        _panel.PivotOffset  = new Vector2(PanelW, PanelH);
        AddChild(_panel);

        var style = new StyleBoxFlat
        {
            BgColor      = UITheme.ToastPanelBg,
            BorderColor  = UITheme.Lime,
            CornerRadiusTopLeft     = 6,
            CornerRadiusTopRight    = 6,
            CornerRadiusBottomLeft  = 6,
            CornerRadiusBottomRight = 6,
        };
        style.SetBorderWidthAll(2);
        _panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left",   12);
        margin.AddThemeConstantOverride("margin_right",  12);
        margin.AddThemeConstantOverride("margin_top",    10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(margin);

        var outerHbox = new HBoxContainer();
        outerHbox.AddThemeConstantOverride("separation", 20);
        outerHbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        margin.AddChild(outerHbox);

        // Icon holder -- filled with achievement PNG if available, star badge otherwise
        _iconHolder = new Control();
        _iconHolder.CustomMinimumSize = new Vector2(44f, 44f);
        _iconHolder.MouseFilter = Control.MouseFilterEnum.Ignore;
        outerHbox.AddChild(_iconHolder);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerHbox.AddChild(vbox);

        var unlockLabel = new Label { Text = "Achievement Unlocked" };
        unlockLabel.AddThemeFontSizeOverride("font_size", 12);
        unlockLabel.Modulate    = new Color(0.55f, 0.55f, 0.55f);
        unlockLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(unlockLabel);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 15);
        UITheme.ApplyFont(_title, semiBold: true);
        _title.Modulate    = Colors.White;
        _title.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(_title);

        _desc = new Label();
        _desc.AddThemeFontSizeOverride("font_size", 12);
        _desc.Modulate    = new Color(0.65f, 0.65f, 0.65f);
        _desc.MouseFilter = Control.MouseFilterEnum.Ignore;
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _desc.MaxLinesVisible = 3;
        vbox.AddChild(_desc);

        _flash = new ColorRect();
        _flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _flash.Color = new Color(0.85f, 1.0f, 0.80f, 0f);
        _flash.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(_flash);

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.AchievementUnlocked += OnAchievementUnlocked;

        _lastTickSec = Time.GetTicksUsec() / 1_000_000.0;
        SetProcess(false);
    }

    // ── Signal handler ────────────────────────────────────────────────────────

    private void OnAchievementUnlocked(string id)
    {
        _queue.Enqueue(id);
        if (!_visible)
            ShowNext();
    }

    // ── Process ───────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        double nowSec = Time.GetTicksUsec() / 1_000_000.0;
        float realDelta = _lastTickSec > 0 ? (float)(nowSec - _lastTickSec) : (float)delta;
        _lastTickSec = nowSec;

        _timer -= realDelta;
        if (_timer <= 0)
            DismissToast();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void ShowNext()
    {
        if (_queue.Count == 0) { _visible = false; SetProcess(false); return; }

        string id = _queue.Dequeue();
        var def = System.Array.Find(AchievementManager.All, d => d.Id == id);
        if (def == null) { ShowNext(); return; }

        if (_title != null) _title.Text = def.Name;
        if (_desc  != null) _desc.Text  = def.Desc;

        // Populate icon: PNG from Assets/Achievements if it exists, ★ badge otherwise
        if (_iconHolder != null)
        {
            foreach (var child in _iconHolder.GetChildren())
                ((Node)child).QueueFree();

            string iconPath = $"res://Assets/Achievements/{id}.png";
            Texture2D? tex = null;
            if (ResourceLoader.Exists(iconPath))
            {
                try { tex = ResourceLoader.Load<Texture2D>(iconPath); }
                catch { tex = null; }
            }
            if (tex != null)
            {
                var iconRect = new TextureRect
                {
                    Texture           = tex,
                    ExpandMode        = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode       = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter       = Control.MouseFilterEnum.Ignore,
                };
                iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _iconHolder.AddChild(iconRect);
            }
            else
            {
                var badge = new Label { Text = "★" };
                badge.AddThemeFontSizeOverride("font_size", 22);
                badge.Modulate            = UITheme.Lime;
                badge.MouseFilter         = Control.MouseFilterEnum.Ignore;
                badge.HorizontalAlignment = HorizontalAlignment.Center;
                badge.VerticalAlignment   = VerticalAlignment.Center;
                badge.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _iconHolder.AddChild(badge);
            }
        }

        _visible = true;
        _timer   = ShowDuration;
        _lastTickSec = Time.GetTicksUsec() / 1_000_000.0;
        SetProcess(true);

        SoundManager.Instance?.Play("achievement_unlock");

        // Flashy pop-in: quick flash, slight slide from right, and scale punch.
        if (_panel != null)
        {
            _panel.OffsetLeft  = -(PanelW + MarginRight) + EnterShiftPx;
            _panel.OffsetRight = -MarginRight + EnterShiftPx;
            _panel.Modulate = new Color(1f, 1f, 1f, 0f);
            _panel.Scale = new Vector2(0.94f, 0.94f);

            if (_flash != null)
                _flash.Color = new Color(_flash.Color.R, _flash.Color.G, _flash.Color.B, 0f);

            var tween = CreateTween();
            tween.SetIgnoreTimeScale(true); // runs regardless of time scale - achievements can unlock during pause or slow-motion surge states
            tween.SetTrans(Tween.TransitionType.Back);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(_panel, "modulate:a", 1.0f, 0.12f);
            tween.Parallel().TweenProperty(_panel, "offset_left", -(PanelW + MarginRight), SlideDuration);
            tween.Parallel().TweenProperty(_panel, "offset_right", -MarginRight, SlideDuration);
            tween.Parallel().TweenProperty(_panel, "scale", new Vector2(1.05f, 1.05f), 0.18f);
            tween.TweenProperty(_panel, "scale", Vector2.One, 0.12f);

            if (_flash != null)
            {
                tween.Parallel().TweenProperty(_flash, "color:a", 0.50f, 0.06f);
                tween.TweenProperty(_flash, "color:a", 0.0f, 0.26f);
            }
        }
    }

    private void DismissToast()
    {
        _visible = false;
        SetProcess(false);

        if (_panel == null) return;
        var tween = CreateTween();
        tween.SetIgnoreTimeScale(true);
        tween.TweenProperty(_panel, "modulate:a", 0.0f, SlideDuration);
        tween.TweenCallback(Callable.From(ShowNext));
    }
}


