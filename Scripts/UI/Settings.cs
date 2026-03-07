using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen settings page. All UI built procedurally.
/// </summary>
public partial class Settings : Node
{
    private Button? _fullscreenBtn;
    private Button? _colorblindBtn;
    private Button? _reducedMotionBtn;

    public override void _Ready()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#141420");
        canvas.AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = SlotTheory.Core.UITheme.Build();
        canvas.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        vbox.CustomMinimumSize = new Vector2(420, 0);
        center.AddChild(vbox);

        // ── Title ─────────────────────────────────────────────────────
        var title = new Label
        {
            Text = "SETTINGS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 52);
        title.Modulate = new Color("#a6d608");
        vbox.AddChild(title);

        AddSpacer(vbox, 20);

        // ── Audio ─────────────────────────────────────────────────────
        AddSectionHeader(vbox, "AUDIO");

        var sm = SettingsManager.Instance;
        AddVolumeRow(vbox, "Master",
            sm?.MasterVolume ?? 80f,
            v => SettingsManager.Instance?.SetVolume(v));
        AddVolumeRow(vbox, "Music",
            sm?.MusicVolume  ?? 80f,
            v => SettingsManager.Instance?.SetMusicVolume(v));
        AddVolumeRow(vbox, "FX",
            sm?.FxVolume     ?? 80f,
            v => SettingsManager.Instance?.SetFxVolume(v));

        AddSpacer(vbox, 8);

        // ── Display ───────────────────────────────────────────────────
        AddSectionHeader(vbox, "DISPLAY");

        bool isFs = sm?.Fullscreen ?? false;
        _fullscreenBtn = new Button
        {
            Text = FullscreenLabel(isFs),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _fullscreenBtn.AddThemeFontSizeOverride("font_size", 20);
        _fullscreenBtn.Pressed += OnToggleFullscreen;
        vbox.AddChild(_fullscreenBtn);

        bool isCb = sm?.ColorblindMode ?? false;
        _colorblindBtn = new Button
        {
            Text = ColorblindLabel(isCb),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _colorblindBtn.AddThemeFontSizeOverride("font_size", 20);
        _colorblindBtn.Pressed += OnToggleColorblind;
        vbox.AddChild(_colorblindBtn);

        bool isRm = sm?.ReducedMotion ?? false;
        _reducedMotionBtn = new Button
        {
            Text = ReducedMotionLabel(isRm),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _reducedMotionBtn.AddThemeFontSizeOverride("font_size", 20);
        _reducedMotionBtn.Pressed += OnToggleReducedMotion;
        vbox.AddChild(_reducedMotionBtn);

        AddSpacer(vbox, 24);

        // ── Back ──────────────────────────────────────────────────────
        var back = new Button
        {
            Text = "← Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        back.AddThemeFontSizeOverride("font_size", 20);
        back.Pressed += () => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        vbox.AddChild(back);
        MobileOptimization.ApplyUIScale(center);
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
        {
            SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
            SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnToggleFullscreen()
    {
        SettingsManager.Instance?.ToggleFullscreen();
        if (_fullscreenBtn != null)
            _fullscreenBtn.Text = FullscreenLabel(SettingsManager.Instance?.Fullscreen ?? false);
    }

    private void OnToggleColorblind()
    {
        bool next = !(SettingsManager.Instance?.ColorblindMode ?? false);
        SettingsManager.Instance?.SetColorblindMode(next);
        if (_colorblindBtn != null)
            _colorblindBtn.Text = ColorblindLabel(next);
    }

    private void OnToggleReducedMotion()
    {
        bool next = !(SettingsManager.Instance?.ReducedMotion ?? false);
        SettingsManager.Instance?.SetReducedMotion(next);
        if (_reducedMotionBtn != null)
            _reducedMotionBtn.Text = ReducedMotionLabel(next);
    }

    private static string FullscreenLabel(bool full) =>
        full ? "Display:  Fullscreen" : "Display:  Windowed";

    private static string ColorblindLabel(bool on) =>
        on ? "Colorblind:  On" : "Colorblind:  Off";

    private static string ReducedMotionLabel(bool on) =>
        on ? "Reduced Motion:  On" : "Reduced Motion:  Off";

    private static void AddVolumeRow(VBoxContainer vbox, string label, float current,
        System.Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(row);

        var lbl = new Label { Text = label };
        lbl.AddThemeFontSizeOverride("font_size", 18);
        lbl.Modulate = new Color(0.85f, 0.85f, 0.85f);
        lbl.CustomMinimumSize = new Vector2(120, 0);
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value    = current,
            Step     = 1,
            CustomMinimumSize   = new Vector2(160, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(slider);

        var valueLabel = new Label { Text = $"{(int)current}" };
        valueLabel.AddThemeFontSizeOverride("font_size", 18);
        valueLabel.Modulate = new Color(0.65f, 0.65f, 0.65f);
        valueLabel.CustomMinimumSize = new Vector2(38, 0);
        row.AddChild(valueLabel);

        slider.ValueChanged += v =>
        {
            valueLabel.Text = $"{(int)v}";
            onChange((float)v);
        };
    }

    private static void AddSectionHeader(VBoxContainer vbox, string text)
    {
        var sep = new HSeparator();
        sep.Modulate = new Color(0.30f, 0.30f, 0.30f);
        vbox.AddChild(sep);

        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.Modulate = new Color("#a6d608");
        vbox.AddChild(lbl);
    }

    private static void AddSpacer(VBoxContainer vbox, int px) =>
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });
}
