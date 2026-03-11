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

    private const float ShowDuration  = 3.5f;
    private const float SlideDuration = 0.25f;
    private const int   PanelW        = 320;
    private const int   PanelH        = 72;
    private const int   MarginRight   = 16;
    private const int   MarginBottom  = 16;

    private Panel?  _panel;
    private Label?  _title;
    private Label?  _desc;
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
        AddChild(_panel);

        var style = new StyleBoxFlat
        {
            BgColor      = new Color("#1e1e2e"),
            BorderColor  = new Color("#a6d608"),
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
        margin.AddThemeConstantOverride("margin_top",    8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        _panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        margin.AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 6);
        header.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(header);

        var badge = new Label { Text = "★" };
        badge.AddThemeFontSizeOverride("font_size", 14);
        badge.Modulate    = new Color("#a6d608");
        badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(badge);

        var unlockLabel = new Label { Text = "Achievement Unlocked" };
        unlockLabel.AddThemeFontSizeOverride("font_size", 12);
        unlockLabel.Modulate    = new Color(0.55f, 0.55f, 0.55f);
        unlockLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        header.AddChild(unlockLabel);

        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 15);
        UITheme.ApplyFont(_title, semiBold: true);
        _title.Modulate    = new Color("#ffffff");
        _title.MouseFilter = Control.MouseFilterEnum.Ignore;
        vbox.AddChild(_title);

        _desc = new Label();
        _desc.AddThemeFontSizeOverride("font_size", 12);
        _desc.Modulate    = new Color(0.65f, 0.65f, 0.65f);
        _desc.MouseFilter = Control.MouseFilterEnum.Ignore;
        _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(_desc);

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

        _visible = true;
        _timer   = ShowDuration;
        _lastTickSec = Time.GetTicksUsec() / 1_000_000.0;
        SetProcess(true);

        // Fade in
        if (_panel != null)
        {
            var tween = CreateTween();
            tween.SetIgnoreTimeScale(true);
            tween.TweenProperty(_panel, "modulate:a", 1.0f, SlideDuration);
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
