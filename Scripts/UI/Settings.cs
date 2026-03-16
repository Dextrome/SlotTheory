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
    private Button? _screenFilterBtn;
    private Button? _vhsGlitchBtn;
    private Button? _phosphorGridBtn;
    private Button? _resetProfileBtn;
    private Label? _resetProfileStatus;

    public override void _Ready()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#07071a");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        canvas.AddChild(root);

        // ── Pinned title header ───────────────────────────────────────────
        var titleMargin = new MarginContainer();
        titleMargin.AddThemeConstantOverride("margin_left",   24);
        titleMargin.AddThemeConstantOverride("margin_right",  24);
        titleMargin.AddThemeConstantOverride("margin_top",    22);
        titleMargin.AddThemeConstantOverride("margin_bottom", 14);
        root.AddChild(titleMargin);

        var title = new Label
        {
            Text = "SETTINGS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 52);
        title.Modulate = new Color("#a6d608");
        titleMargin.AddChild(title);

        // ── Bordered scroll panel — horizontally constrained to content width ──
        float vpWidth   = GetViewport().GetVisibleRect().Size.X;
        float sidePad   = Mathf.Max(20f, (vpWidth - 560f) / 2f);

        var scrollPanelMargin = new MarginContainer();
        scrollPanelMargin.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        scrollPanelMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollPanelMargin.AddThemeConstantOverride("margin_left",   (int)sidePad);
        scrollPanelMargin.AddThemeConstantOverride("margin_right",  (int)sidePad);
        scrollPanelMargin.AddThemeConstantOverride("margin_top",    0);
        scrollPanelMargin.AddThemeConstantOverride("margin_bottom", 0);
        root.AddChild(scrollPanelMargin);

        var scrollPanel = new PanelContainer();
        scrollPanel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        scrollPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scrollPanel.AddThemeStyleboxOverride("panel", SlotTheory.Core.UITheme.MakePanel(
            bg: new Color(0.05f, 0.04f, 0.13f),
            border: new Color(0.30f, 0.18f, 0.55f),
            corners: 10, borderWidth: 2, padH: 8, padV: 8));
        scrollPanelMargin.AddChild(scrollPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        scrollPanel.AddChild(scroll);

        var center = new CenterContainer();
        center.Theme = SlotTheory.Core.UITheme.Build();
        center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        center.CustomMinimumSize = new Vector2(0, GetViewport().GetVisibleRect().Size.Y);
        scroll.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        vbox.CustomMinimumSize = new Vector2(420, 0);
        center.AddChild(vbox);

        AddSpacer(vbox, 12);

        // Audio
        AddSectionHeader(vbox, "AUDIO");

        var sm = SettingsManager.Instance;
        AddVolumeRow(vbox, "Master",
            sm?.MasterVolume ?? 80f,
            v => SettingsManager.Instance?.SetVolume(v));
        AddVolumeRow(vbox, "Music",
            sm?.MusicVolume  ?? 80f,
            v => SettingsManager.Instance?.SetMusicVolume(v));
        AddVolumeRow(vbox, "Game FX",
            sm?.FxVolume     ?? 80f,
            v => SettingsManager.Instance?.SetFxVolume(v));
        AddVolumeRow(vbox, "UI FX",
            sm?.UiFxVolume   ?? 80f,
            v => SettingsManager.Instance?.SetUiFxVolume(v));

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

        bool isSf = sm?.ScreenFilterEnabled ?? true;
        _screenFilterBtn = new Button
        {
            Text = ScreenFilterLabel(isSf),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _screenFilterBtn.AddThemeFontSizeOverride("font_size", 20);
        _screenFilterBtn.Pressed += OnToggleScreenFilter;
        vbox.AddChild(_screenFilterBtn);

        AddSpacer(vbox, 8);
        AddSectionHeader(vbox, "SCREEN EFFECTS");

        bool isVhs = sm?.VhsGlitchEnabled ?? false;
        _vhsGlitchBtn = new Button { Text = VhsGlitchLabel(isVhs), CustomMinimumSize = new Vector2(260, 44) };
        _vhsGlitchBtn.AddThemeFontSizeOverride("font_size", 20);
        _vhsGlitchBtn.Pressed += OnToggleVhsGlitch;
        vbox.AddChild(_vhsGlitchBtn);

        bool isPhos = sm?.PhosphorGridEnabled ?? false;
        _phosphorGridBtn = new Button { Text = PhosphorGridLabel(isPhos), CustomMinimumSize = new Vector2(260, 44) };
        _phosphorGridBtn.AddThemeFontSizeOverride("font_size", 20);
        _phosphorGridBtn.Pressed += OnTogglePhosphorGrid;
        vbox.AddChild(_phosphorGridBtn);

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

        if (sm?.DevMode == true)
        {
            AddSpacer(vbox, 8);
            AddSectionHeader(vbox, "DEVELOPER");

            _resetProfileBtn = new Button
            {
                Text = "Reset Profile Unlocks",
                CustomMinimumSize = new Vector2(260, 44),
            };
            _resetProfileBtn.AddThemeFontSizeOverride("font_size", 20);
            UITheme.ApplyMutedStyle(_resetProfileBtn);
            _resetProfileBtn.Pressed += OnResetProfileUnlocks;
            vbox.AddChild(_resetProfileBtn);

            _resetProfileStatus = new Label
            {
                Text = "",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _resetProfileStatus.AddThemeFontSizeOverride("font_size", 14);
            _resetProfileStatus.Modulate = new Color(0.85f, 0.72f, 0.72f);
            vbox.AddChild(_resetProfileStatus);
        }

        MobileOptimization.ApplyUIScale(center);

        // Back button pinned outside scroll so it's always visible
        var bottomMargin = new MarginContainer();
        bottomMargin.AddThemeConstantOverride("margin_left",   24);
        bottomMargin.AddThemeConstantOverride("margin_right",  24);
        bottomMargin.AddThemeConstantOverride("margin_top",    12);
        bottomMargin.AddThemeConstantOverride("margin_bottom", 16);
        bottomMargin.Theme = SlotTheory.Core.UITheme.Build();
        root.AddChild(bottomMargin);

        var backCenter = new CenterContainer();
        bottomMargin.AddChild(backCenter);

        var back = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        back.AddThemeFontSizeOverride("font_size", 20);
        back.Pressed += () => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        backCenter.AddChild(back);
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

    private void OnToggleScreenFilter()
    {
        bool next = !(SettingsManager.Instance?.ScreenFilterEnabled ?? true);
        SettingsManager.Instance?.SetScreenFilterEnabled(next);
        if (_screenFilterBtn != null)
            _screenFilterBtn.Text = ScreenFilterLabel(next);
    }

    private void OnToggleVhsGlitch()
    {
        bool next = !(SettingsManager.Instance?.VhsGlitchEnabled ?? false);
        SettingsManager.Instance?.SetVhsGlitchEnabled(next);
        if (_vhsGlitchBtn != null)
            _vhsGlitchBtn.Text = VhsGlitchLabel(next);
    }

    private void OnTogglePhosphorGrid()
    {
        bool next = !(SettingsManager.Instance?.PhosphorGridEnabled ?? false);
        SettingsManager.Instance?.SetPhosphorGridEnabled(next);
        if (_phosphorGridBtn != null)
            _phosphorGridBtn.Text = PhosphorGridLabel(next);
    }

    private static string VhsGlitchLabel(bool on) =>
        on ? "VHS Glitch:  On" : "VHS Glitch:  Off";

    private static string PhosphorGridLabel(bool on) =>
        on ? "Phosphor Grid:  On" : "Phosphor Grid:  Off";

    private static string FullscreenLabel(bool full) =>
        full ? "Display:  Fullscreen" : "Display:  Windowed";

    private static string ColorblindLabel(bool on) =>
        on ? "Colorblind:  On" : "Colorblind:  Off";

    private static string ReducedMotionLabel(bool on) =>
        on ? "Reduced Motion:  On" : "Reduced Motion:  Off";

    private static string PostFxLabel(bool on) =>
        on ? "Post FX:  On" : "Post FX:  Off";

    private static string ScreenFilterLabel(bool on) =>
        on ? "Screen Filter (CA/Bloom/Scanlines):  On" : "Screen Filter (CA/Bloom/Scanlines):  Off";

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

    private void OnResetProfileUnlocks()
    {
        AchievementManager.Instance?.ResetAllAchievements();
        if (_resetProfileStatus != null)
            _resetProfileStatus.Text = "All achievements and unlock flags cleared.";
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
