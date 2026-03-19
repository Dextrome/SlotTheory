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
    private Button? _resetTutorialBtn;
    private HBoxContainer? _resetTutorialConfirmRow;
    private Label?  _resetTutorialStatus;
    private Button? _resetProfileBtn;
    private Label?  _resetProfileStatus;

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

        // ── Title ─────────────────────────────────────────────────────────────
        var titleMargin = new MarginContainer();
        titleMargin.AddThemeConstantOverride("margin_left",   24);
        titleMargin.AddThemeConstantOverride("margin_right",  24);
        titleMargin.AddThemeConstantOverride("margin_top",    14);
        titleMargin.AddThemeConstantOverride("margin_bottom", 8);
        root.AddChild(titleMargin);

        var title = new Label
        {
            Text = "SETTINGS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(title, semiBold: true, size: 52);
        title.Modulate = new Color("#a6d608");
        titleMargin.AddChild(title);

        // ── Scroll panel ──────────────────────────────────────────────────────
        float vpWidth = GetViewport().GetVisibleRect().Size.X;
        float sidePad = Mathf.Max(20f, (vpWidth - 600f) / 2f);

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
        UITheme.ApplyGlassChassisPanel(
            scrollPanel,
            bg: new Color(0.05f, 0.04f, 0.13f),
            accent: new Color(0.42f, 0.78f, 0.94f, 0.92f),
            corners: 10, borderWidth: 2, padH: 8, padV: 8,
            sideEmitters: true, emitterIntensity: 0.72f);
        scrollPanelMargin.AddChild(scrollPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        scrollPanel.AddChild(scroll);

        var center = new CenterContainer();
        center.Theme = UITheme.Build();
        center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        center.CustomMinimumSize = new Vector2(0, GetViewport().GetVisibleRect().Size.Y);
        scroll.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        vbox.CustomMinimumSize = new Vector2(500, 0);
        center.AddChild(vbox);

        AddSpacer(vbox, 4);

        var sm = SettingsManager.Instance;

        // ── AUDIO ─────────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "AUDIO");
        AddVolumeRow(vbox, "Master",  sm?.MasterVolume ?? 80f, v => SettingsManager.Instance?.SetVolume(v));
        AddVolumeRow(vbox, "Music",   sm?.MusicVolume  ?? 80f, v => SettingsManager.Instance?.SetMusicVolume(v));
        AddVolumeRow(vbox, "Game FX", sm?.FxVolume     ?? 80f, v => SettingsManager.Instance?.SetFxVolume(v));
        AddVolumeRow(vbox, "UI FX",   sm?.UiFxVolume   ?? 80f, v => SettingsManager.Instance?.SetUiFxVolume(v));

        AddSpacer(vbox, 4);

        // ── DISPLAY ───────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "DISPLAY");

        bool isFs = sm?.Fullscreen ?? false;
        _fullscreenBtn = AddSettingRow(vbox, "Display Mode",
            isFs ? "Fullscreen" : "Windowed", isOn: isFs, OnToggleFullscreen);

        bool isCb = sm?.ColorblindMode ?? false;
        _colorblindBtn = AddSettingRow(vbox, "Colorblind Mode",
            OnOffText(isCb), isOn: isCb, OnToggleColorblind);

        bool isRm = sm?.ReducedMotion ?? false;
        _reducedMotionBtn = AddSettingRow(vbox, "Reduced Motion",
            OnOffText(isRm), isOn: isRm, OnToggleReducedMotion);

        bool isPostFx = sm?.PostFxEnabled ?? true;
        _postFxBtn = AddSettingRow(vbox, "Post FX (Bloom / Glow)",
            OnOffText(isPostFx), isOn: isPostFx, OnTogglePostFx);

        AddSpacer(vbox, 4);

        // ── SCREEN EFFECTS ────────────────────────────────────────────────────
        AddSectionHeader(vbox, "SCREEN EFFECTS");

        bool isSf = sm?.ScreenFilterEnabled ?? true;
        _screenFilterBtn = AddSettingRow(vbox, "Screen Filter",
            OnOffText(isSf), isOn: isSf, OnToggleScreenFilter);

        bool isVhs = sm?.VhsGlitchEnabled ?? false;
        _vhsGlitchBtn = AddSettingRow(vbox, "VHS Glitch",
            OnOffText(isVhs), isOn: isVhs, OnToggleVhsGlitch);

        AddSpacer(vbox, 4);

        // ── ENEMY FX ──────────────────────────────────────────────────────────
        AddSectionHeader(vbox, "ENEMY FX");

        bool layered = sm?.LayeredEnemyRendering ?? true;
        _enemyLayeredBtn = AddSettingRow(vbox, "Layered Rendering",
            OnOffText(layered), isOn: layered, OnToggleEnemyLayered);

        bool emissive = sm?.EnemyEmissiveLines ?? true;
        _enemyEmissiveBtn = AddSettingRow(vbox, "Emissive Lines",
            OnOffText(emissive), isOn: emissive, OnToggleEnemyEmissive);

        bool damage = sm?.EnemyDamageMaterial ?? true;
        _enemyDamageBtn = AddSettingRow(vbox, "Damage Material",
            OnOffText(damage), isOn: damage, OnToggleEnemyDamage);

        bool bloom = sm?.EnemyBloomHighlights ?? !MobileOptimization.IsMobile();
        _enemyBloomBtn = AddSettingRow(vbox, "Bloom Highlights",
            OnOffText(bloom), isOn: bloom, OnToggleEnemyBloom);

        // ── PROFILE ───────────────────────────────────────────────────────────
        AddSpacer(vbox, 4);
        AddSectionHeader(vbox, "PROFILE");

        _resetTutorialBtn = AddSettingRow(vbox, "Reset Tutorial",
            "Reset", isOn: false, OnResetTutorialPressed);
        UITheme.ApplyMutedStyle(_resetTutorialBtn);
        UITheme.ApplyMenuButtonFinish(_resetTutorialBtn, UITheme.Magenta, 0.09f, 0.14f);

        // Confirmation row (hidden until button is pressed)
        _resetTutorialConfirmRow = new HBoxContainer();
        _resetTutorialConfirmRow.AddThemeConstantOverride("separation", 8);
        _resetTutorialConfirmRow.Visible = false;
        var confirmMargin = new MarginContainer();
        confirmMargin.AddThemeConstantOverride("margin_left", 14);
        confirmMargin.AddThemeConstantOverride("margin_right", 10);
        confirmMargin.AddThemeConstantOverride("margin_top", 2);
        confirmMargin.AddThemeConstantOverride("margin_bottom", 2);
        confirmMargin.AddChild(_resetTutorialConfirmRow);
        vbox.AddChild(confirmMargin);

        var confirmLabel = new Label
        {
            Text = "Show tutorial on next run?",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        confirmLabel.AddThemeFontSizeOverride("font_size", 15);
        confirmLabel.Modulate = new Color(0.88f, 0.88f, 0.92f);
        _resetTutorialConfirmRow.AddChild(confirmLabel);

        var yesBtn = new Button
        {
            Text = "Yes",
            CustomMinimumSize = new Vector2(60, 28),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        yesBtn.AddThemeFontSizeOverride("font_size", 15);
        ApplyValueButtonStyle(yesBtn, isOn: true);
        UITheme.ApplyMenuButtonFinish(yesBtn, UITheme.Lime, 0.08f, 0.11f);
        yesBtn.Pressed      += OnResetTutorialConfirmed;
        yesBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        _resetTutorialConfirmRow.AddChild(yesBtn);

        var noBtn = new Button
        {
            Text = "No",
            CustomMinimumSize = new Vector2(60, 28),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        noBtn.AddThemeFontSizeOverride("font_size", 15);
        ApplyValueButtonStyle(noBtn, isOn: false);
        UITheme.ApplyMenuButtonFinish(noBtn, UITheme.Cyan, 0.08f, 0.11f);
        noBtn.Pressed      += OnResetTutorialCancelled;
        noBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        _resetTutorialConfirmRow.AddChild(noBtn);

        _resetTutorialStatus = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _resetTutorialStatus.AddThemeFontSizeOverride("font_size", 13);
        _resetTutorialStatus.Modulate = new Color(0.72f, 0.85f, 0.72f);
        var tutStatusMargin = new MarginContainer();
        tutStatusMargin.AddThemeConstantOverride("margin_left", 16);
        tutStatusMargin.AddChild(_resetTutorialStatus);
        vbox.AddChild(tutStatusMargin);

        if (sm?.DevMode == true)
        {
            AddSpacer(vbox, 4);
            AddSectionHeader(vbox, "DEVELOPER");

            _resetProfileBtn = AddSettingRow(vbox, "Reset All Achievements",
                "Reset", isOn: false, OnResetProfileUnlocks);
            UITheme.ApplyMutedStyle(_resetProfileBtn);
            UITheme.ApplyMenuButtonFinish(_resetProfileBtn, UITheme.Magenta, 0.09f, 0.14f);

            _resetProfileStatus = new Label
            {
                Text = "",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _resetProfileStatus.AddThemeFontSizeOverride("font_size", 13);
            _resetProfileStatus.Modulate = new Color(0.85f, 0.72f, 0.72f);
            var statusMargin = new MarginContainer();
            statusMargin.AddThemeConstantOverride("margin_left", 16);
            statusMargin.AddChild(_resetProfileStatus);
            vbox.AddChild(statusMargin);
        }

        AddSpacer(vbox, 6);
        MobileOptimization.ApplyUIScale(center);

        // ── Back button ───────────────────────────────────────────────────────
        var bottomMargin = new MarginContainer();
        bottomMargin.AddThemeConstantOverride("margin_left",   24);
        bottomMargin.AddThemeConstantOverride("margin_right",  24);
        bottomMargin.AddThemeConstantOverride("margin_top",    12);
        bottomMargin.AddThemeConstantOverride("margin_bottom", 16);
        bottomMargin.Theme = UITheme.Build();
        root.AddChild(bottomMargin);

        var backCenter = new CenterContainer();
        bottomMargin.AddChild(backCenter);

        var back = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        back.AddThemeFontSizeOverride("font_size", 20);
        UITheme.ApplyCyanStyle(back);
        UITheme.ApplyMenuButtonFinish(back, UITheme.Cyan, 0.10f, 0.12f);
        back.Pressed      += () => Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        back.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        backCenter.AddChild(back);
    }

    // ── Toggle handlers ───────────────────────────────────────────────────────

    private void OnToggleFullscreen()
    {
        SettingsManager.Instance?.ToggleFullscreen();
        bool isFs = SettingsManager.Instance?.Fullscreen ?? false;
        UpdateValueButton(_fullscreenBtn, isFs ? "Fullscreen" : "Windowed", isOn: isFs);
    }

    private void OnToggleColorblind()
    {
        bool next = !(SettingsManager.Instance?.ColorblindMode ?? false);
        SettingsManager.Instance?.SetColorblindMode(next);
        UpdateValueButton(_colorblindBtn, OnOffText(next), next);
    }

    private void OnToggleReducedMotion()
    {
        bool next = !(SettingsManager.Instance?.ReducedMotion ?? false);
        SettingsManager.Instance?.SetReducedMotion(next);
        UpdateValueButton(_reducedMotionBtn, OnOffText(next), next);
    }

    private void OnTogglePostFx()
    {
        bool next = !(SettingsManager.Instance?.PostFxEnabled ?? true);
        SettingsManager.Instance?.SetPostFxEnabled(next);
        UpdateValueButton(_postFxBtn, OnOffText(next), next);
    }

    private void OnToggleScreenFilter()
    {
        bool next = !(SettingsManager.Instance?.ScreenFilterEnabled ?? false);
        SettingsManager.Instance?.SetScreenFilterEnabled(next);
        SettingsManager.Instance?.SetPhosphorGridEnabled(next);
        UpdateValueButton(_screenFilterBtn, OnOffText(next), next);
    }

    private void OnToggleVhsGlitch()
    {
        bool next = !(SettingsManager.Instance?.VhsGlitchEnabled ?? false);
        SettingsManager.Instance?.SetVhsGlitchEnabled(next);
        UpdateValueButton(_vhsGlitchBtn, OnOffText(next), next);
    }

    private void OnToggleEnemyLayered()
    {
        bool next = !(SettingsManager.Instance?.LayeredEnemyRendering ?? true);
        SettingsManager.Instance?.SetLayeredEnemyRendering(next);
        UpdateValueButton(_enemyLayeredBtn, OnOffText(next), next);
    }

    private void OnToggleEnemyEmissive()
    {
        bool next = !(SettingsManager.Instance?.EnemyEmissiveLines ?? true);
        SettingsManager.Instance?.SetEnemyEmissiveLines(next);
        UpdateValueButton(_enemyEmissiveBtn, OnOffText(next), next);
    }

    private void OnToggleEnemyDamage()
    {
        bool next = !(SettingsManager.Instance?.EnemyDamageMaterial ?? true);
        SettingsManager.Instance?.SetEnemyDamageMaterial(next);
        UpdateValueButton(_enemyDamageBtn, OnOffText(next), next);
    }

    private void OnToggleEnemyBloom()
    {
        bool next = !(SettingsManager.Instance?.EnemyBloomHighlights ?? !MobileOptimization.IsMobile());
        SettingsManager.Instance?.SetEnemyBloomHighlights(next);
        UpdateValueButton(_enemyBloomBtn, OnOffText(next), next);
    }

    private void OnResetTutorialPressed()
    {
        if (_resetTutorialConfirmRow == null) return;
        _resetTutorialConfirmRow.Visible = true;
        if (_resetTutorialStatus != null)
            _resetTutorialStatus.Text = "";
    }

    private void OnResetTutorialConfirmed()
    {
        SettingsManager.Instance?.ResetTutorial();
        if (_resetTutorialConfirmRow != null)
            _resetTutorialConfirmRow.Visible = false;
        if (_resetTutorialStatus != null)
            _resetTutorialStatus.Text = "Tutorial will show on your next run.";
    }

    private void OnResetTutorialCancelled()
    {
        if (_resetTutorialConfirmRow != null)
            _resetTutorialConfirmRow.Visible = false;
    }

    private void OnResetProfileUnlocks()
    {
        AchievementManager.Instance?.ResetAllAchievements();
        if (_resetProfileStatus != null)
            _resetProfileStatus.Text = "All achievements and unlock flags cleared.";
    }

    // ── Row builders ──────────────────────────────────────────────────────────

    /// <summary>Adds a label + value-button row. Returns the value button for later updates.</summary>
    private static Button AddSettingRow(VBoxContainer vbox, string labelText, string valueText,
        bool isOn, System.Action callback)
    {
        var row = new PanelContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeStyleboxOverride("panel", MakeRowStyle());
        vbox.AddChild(row);

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 12);
        row.AddChild(inner);

        var lbl = new Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
        };
        lbl.AddThemeFontSizeOverride("font_size", 18);
        lbl.Modulate = new Color(0.88f, 0.88f, 0.92f);
        inner.AddChild(lbl);

        var btn = new Button
        {
            Text = valueText,
            CustomMinimumSize = new Vector2(100, 28),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        btn.AddThemeFontSizeOverride("font_size", 15);
        ApplyValueButtonStyle(btn, isOn);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.07f, 0.11f);
        btn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); callback(); };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        inner.AddChild(btn);

        return btn;
    }

    private static void AddVolumeRow(VBoxContainer vbox, string labelText, float current,
        System.Action<float> onChange)
    {
        var row = new PanelContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeStyleboxOverride("panel", MakeRowStyle());
        vbox.AddChild(row);

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 12);
        row.AddChild(inner);

        var lbl = new Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(100, 0),
        };
        lbl.AddThemeFontSizeOverride("font_size", 18);
        lbl.Modulate = new Color(0.88f, 0.88f, 0.92f);
        inner.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value    = current,
            Step     = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(160, 20),
        };
        inner.AddChild(slider);

        var valueLabel = new Label
        {
            Text = $"{(int)current}",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize = new Vector2(36, 0),
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 16);
        valueLabel.Modulate = new Color(0.55f, 0.85f, 0.55f);
        inner.AddChild(valueLabel);

        slider.ValueChanged += v =>
        {
            valueLabel.Text = $"{(int)v}";
            valueLabel.Modulate = (int)v > 0
                ? new Color(0.55f, 0.85f, 0.55f)
                : new Color(0.45f, 0.45f, 0.45f);
            onChange((float)v);
        };
    }

    private static void AddSectionHeader(VBoxContainer vbox, string text)
    {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(row);

        // Accent bar
        var bar = new ColorRect
        {
            Color = UITheme.Lime,
            CustomMinimumSize = new Vector2(3f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(bar);

        var lbl = new Label { Text = text };
        UITheme.ApplyFont(lbl, semiBold: true, size: 13);
        lbl.Modulate = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.85f);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(lbl);

        // Dim line extending to the right
        var line = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 1),
            Color = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.12f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(line);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StyleBoxFlat MakeRowStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.05f, 0.14f, 0.80f),
            CornerRadiusTopLeft     = 5,
            CornerRadiusTopRight    = 5,
            CornerRadiusBottomLeft  = 5,
            CornerRadiusBottomRight = 5,
        };
        s.ContentMarginLeft   = 14;
        s.ContentMarginRight  = 10;
        s.ContentMarginTop    = 6;
        s.ContentMarginBottom = 6;
        return s;
    }

    private static void ApplyValueButtonStyle(Button btn, bool isOn)
    {
        if (isOn)
        {
            btn.AddThemeColorOverride("font_color",          UITheme.Lime);
            btn.AddThemeColorOverride("font_hover_color",    UITheme.Lime);
            btn.AddThemeColorOverride("font_pressed_color",  UITheme.LimeDim);
            btn.AddThemeColorOverride("font_focus_color",    UITheme.Lime);
            btn.AddThemeStyleboxOverride("normal",  MakeValBtn(new Color(0.06f, 0.14f, 0.04f), UITheme.LimeDark));
            btn.AddThemeStyleboxOverride("hover",   MakeValBtn(new Color(0.09f, 0.20f, 0.05f), UITheme.Lime,    glowAlpha: 0.12f, glowSize: 4, glowColor: UITheme.Lime));
            btn.AddThemeStyleboxOverride("pressed", MakeValBtn(new Color(0.04f, 0.08f, 0.02f), UITheme.LimeDim));
            btn.AddThemeStyleboxOverride("focus",   MakeValBtn(new Color(0.09f, 0.20f, 0.05f), UITheme.Lime,    glowAlpha: 0.08f, glowSize: 3, glowColor: UITheme.Lime));
        }
        else
        {
            var dimText   = new Color(0.50f, 0.50f, 0.54f);
            var dimBorder = new Color(0.20f, 0.20f, 0.26f);
            var dimBg     = new Color(0.05f, 0.05f, 0.10f);
            btn.AddThemeColorOverride("font_color",         dimText);
            btn.AddThemeColorOverride("font_hover_color",   new Color(0.70f, 0.70f, 0.75f));
            btn.AddThemeColorOverride("font_pressed_color", dimText);
            btn.AddThemeColorOverride("font_focus_color",   dimText);
            btn.AddThemeStyleboxOverride("normal",  MakeValBtn(dimBg,                           dimBorder));
            btn.AddThemeStyleboxOverride("hover",   MakeValBtn(new Color(0.08f, 0.08f, 0.14f),  dimBorder));
            btn.AddThemeStyleboxOverride("pressed", MakeValBtn(dimBg,                           dimBorder));
            btn.AddThemeStyleboxOverride("focus",   MakeValBtn(new Color(0.08f, 0.08f, 0.14f),  dimBorder));
        }
    }

    private static StyleBoxFlat MakeValBtn(Color bg, Color border,
        float glowAlpha = 0f, int glowSize = 0, Color? glowColor = null)
    {
        var s = UITheme.MakeBtn(bg, border, border: 1, corners: 6,
            glowAlpha: glowAlpha, glowSize: glowSize, glowColor: glowColor);
        // Override content margins to fit snugly inside the 28px button height
        s.ContentMarginTop    = 4;
        s.ContentMarginBottom = 4;
        s.ContentMarginLeft   = 10;
        s.ContentMarginRight  = 10;
        return s;
    }

    private static void UpdateValueButton(Button? btn, string text, bool isOn)
    {
        if (btn == null) return;
        btn.Text = text;
        ApplyValueButtonStyle(btn, isOn);
    }

    private static string OnOffText(bool on) => on ? "ON" : "OFF";

    private static void AddSpacer(VBoxContainer vbox, int px) =>
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
        {
            SoundManager.Instance?.Play("ui_select");
            Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }
}
