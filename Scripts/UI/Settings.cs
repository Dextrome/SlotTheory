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
    private Button? _postFxBtn;
    private Button? _enemyLayeredBtn;
    private Button? _enemyEmissiveBtn;
    private Button? _enemyDamageBtn;
    private Button? _enemyBloomBtn;

    public override void _Ready()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#141420");
        canvas.AddChild(bg);

        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        canvas.AddChild(scroll);

        var center = new CenterContainer();
        center.Theme = SlotTheory.Core.UITheme.Build();
        center.CustomMinimumSize = GetViewport().GetVisibleRect().Size;
        scroll.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        vbox.CustomMinimumSize = new Vector2(420, 0);
        center.AddChild(vbox);

        // Title
        var title = new Label
        {
            Text = "SETTINGS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 52);
        title.Modulate = new Color("#a6d608");
        vbox.AddChild(title);

        AddSpacer(vbox, 20);

        // Audio
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

        // Display
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

        bool isPostFx = sm?.PostFxEnabled ?? true;
        _postFxBtn = new Button
        {
            Text = PostFxLabel(isPostFx),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _postFxBtn.AddThemeFontSizeOverride("font_size", 20);
        _postFxBtn.Pressed += OnTogglePostFx;
        vbox.AddChild(_postFxBtn);

        AddSpacer(vbox, 8);
        AddSectionHeader(vbox, "ENEMY FX");

        bool layered = sm?.LayeredEnemyRendering ?? true;
        _enemyLayeredBtn = new Button
        {
            Text = EnemyLayeredLabel(layered),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _enemyLayeredBtn.AddThemeFontSizeOverride("font_size", 20);
        _enemyLayeredBtn.Pressed += OnToggleEnemyLayered;
        vbox.AddChild(_enemyLayeredBtn);

        bool emissive = sm?.EnemyEmissiveLines ?? true;
        _enemyEmissiveBtn = new Button
        {
            Text = EnemyEmissiveLabel(emissive),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _enemyEmissiveBtn.AddThemeFontSizeOverride("font_size", 20);
        _enemyEmissiveBtn.Pressed += OnToggleEnemyEmissive;
        vbox.AddChild(_enemyEmissiveBtn);

        bool damage = sm?.EnemyDamageMaterial ?? true;
        _enemyDamageBtn = new Button
        {
            Text = EnemyDamageLabel(damage),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _enemyDamageBtn.AddThemeFontSizeOverride("font_size", 20);
        _enemyDamageBtn.Pressed += OnToggleEnemyDamage;
        vbox.AddChild(_enemyDamageBtn);

        bool bloom = sm?.EnemyBloomHighlights ?? !MobileOptimization.IsMobile();
        _enemyBloomBtn = new Button
        {
            Text = EnemyBloomLabel(bloom),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _enemyBloomBtn.AddThemeFontSizeOverride("font_size", 20);
        _enemyBloomBtn.Pressed += OnToggleEnemyBloom;
        vbox.AddChild(_enemyBloomBtn);

        AddSpacer(vbox, 24);

        // Back
        var back = new Button
        {
            Text = "<- Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        back.AddThemeFontSizeOverride("font_size", 20);
        back.Pressed += () => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        vbox.AddChild(back);
        MobileOptimization.ApplyUIScale(center);
        AddSpacer(vbox, 24);
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

    private void OnTogglePostFx()
    {
        bool next = !(SettingsManager.Instance?.PostFxEnabled ?? true);
        SettingsManager.Instance?.SetPostFxEnabled(next);
        if (_postFxBtn != null)
            _postFxBtn.Text = PostFxLabel(next);
    }

    private static string FullscreenLabel(bool full) =>
        full ? "Display:  Fullscreen" : "Display:  Windowed";

    private static string ColorblindLabel(bool on) =>
        on ? "Colorblind:  On" : "Colorblind:  Off";

    private static string ReducedMotionLabel(bool on) =>
        on ? "Reduced Motion:  On" : "Reduced Motion:  Off";

    private static string PostFxLabel(bool on) =>
        on ? "Post FX:  On" : "Post FX:  Off";

    private void OnToggleEnemyLayered()
    {
        bool next = !(SettingsManager.Instance?.LayeredEnemyRendering ?? true);
        SettingsManager.Instance?.SetLayeredEnemyRendering(next);
        if (_enemyLayeredBtn != null)
            _enemyLayeredBtn.Text = EnemyLayeredLabel(next);
    }

    private void OnToggleEnemyEmissive()
    {
        bool next = !(SettingsManager.Instance?.EnemyEmissiveLines ?? true);
        SettingsManager.Instance?.SetEnemyEmissiveLines(next);
        if (_enemyEmissiveBtn != null)
            _enemyEmissiveBtn.Text = EnemyEmissiveLabel(next);
    }

    private void OnToggleEnemyDamage()
    {
        bool next = !(SettingsManager.Instance?.EnemyDamageMaterial ?? true);
        SettingsManager.Instance?.SetEnemyDamageMaterial(next);
        if (_enemyDamageBtn != null)
            _enemyDamageBtn.Text = EnemyDamageLabel(next);
    }

    private void OnToggleEnemyBloom()
    {
        bool next = !(SettingsManager.Instance?.EnemyBloomHighlights ?? !MobileOptimization.IsMobile());
        SettingsManager.Instance?.SetEnemyBloomHighlights(next);
        if (_enemyBloomBtn != null)
            _enemyBloomBtn.Text = EnemyBloomLabel(next);
    }

    private static string EnemyLayeredLabel(bool on) =>
        on ? "Layered Enemies:  On" : "Layered Enemies:  Off";

    private static string EnemyEmissiveLabel(bool on) =>
        on ? "Enemy Emissive:  On" : "Enemy Emissive:  Off";

    private static string EnemyDamageLabel(bool on) =>
        on ? "Enemy Damage FX:  On" : "Enemy Damage FX:  Off";

    private static string EnemyBloomLabel(bool on) =>
        on ? "Enemy Bloom:  On" : "Enemy Bloom:  Off";

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
