using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Pause overlay. Esc toggles pause from any phase except Win/Loss.
/// ProcessMode = Always so it receives input even while the scene tree is paused.
/// </summary>
public partial class PauseScreen : CanvasLayer
{
    private Control _mainPanel        = null!;
    private Control _settingsPanel    = null!;
    private Control _quitConfirmPanel = null!;
    private Control _rootContainer    = null!;
    private ScrollContainer? _settingsScroll;
    private Control? _settingsContent;
    private double  _lastBackTime     = -1.0;  // debounce Android back (fires on key-down AND key-up)
    private Button? _pauseFsBtn;
    private Button? _pauseCbBtn;
    private Button? _pauseRmBtn;
    private Button? _pausePostFxBtn;

    public override void _Ready()
    {
        Layer       = 8;
        ProcessMode = ProcessModeEnum.Always;
        Visible     = false;

        // Add to group so mobile menu button can find it
        AddToGroup("pause_screen");

        // Dark overlay
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.80f);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = SlotTheory.Core.UITheme.Build();
        AddChild(center);
        _rootContainer = center;

        // Both panels live inside the same CenterContainer; only one is visible at a time
        var stack = new VBoxContainer();   // wrapper so CenterContainer centres both
        center.AddChild(stack);

        BuildMainPanel(stack);
        BuildSettingsPanel(stack);
        BuildQuitConfirmPanel(stack);

        _settingsPanel.Visible    = false;
        _quitConfirmPanel.Visible = false;
        MobileOptimization.ApplyUIScale(center);
        AddChild(new PinchZoomHandler(center));
    }

    // ── Panel builders ───────────────────────────────────────────────────

    private void BuildMainPanel(VBoxContainer parent)
    {
        bool isPlaytestRun = MapEditorState.IsPlaytesting;

        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.04f, 0.04f, 0.12f),
            border: new Color(0.18f, 0.22f, 0.18f),
            corners: 12, borderWidth: 1, padH: 20, padV: 14));
        parent.AddChild(card);
        _mainPanel = card;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        card.AddChild(vbox);

        AddLabel(vbox, "PAUSED", 40, UITheme.Lime);
        AddSpacer(vbox, 4);
        AddSeparatorLine(vbox);
        AddSpacer(vbox, 4);

        var resumeBtn = MakePauseBtn("Resume", 260, 44, 22);
        UITheme.ApplyPrimaryStyle(resumeBtn);
        UITheme.ApplyMenuButtonFinish(resumeBtn, UITheme.Lime, 0.11f, 0.14f);
        resumeBtn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); OnResume(); };
        resumeBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        vbox.AddChild(resumeBtn);

        AddSpacer(vbox, 2);
        AddBtn(vbox, "Restart Run",  OnRestart);
        if (isPlaytestRun)
        {
            AddBtn(vbox, "Back to Editor", OnBackToEditor);
            AddSpacer(vbox, 2);
            AddSeparatorLine(vbox);
            AddSpacer(vbox, 2);
            AddBtn(vbox, "Settings", OnOpenSettings);
        }
        else
        {
            AddBtn(vbox, "Settings",     OnOpenSettings);
            AddBtn(vbox, "Achievements", OnAchievements);
            AddBtn(vbox, "Slot Codex",   OnSlotCodex);
            AddSpacer(vbox, 2);
            AddSeparatorLine(vbox);
            AddSpacer(vbox, 2);
        }

        var mmBtn = MakePauseBtn("Main Menu", 260, 38, 20);
        UITheme.ApplyCyanStyle(mmBtn);
        UITheme.ApplyMenuButtonFinish(mmBtn, UITheme.Cyan, 0.09f, 0.11f);
        mmBtn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); OnMainMenu(); };
        mmBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        vbox.AddChild(mmBtn);

        var quitBtn = MakePauseBtn("Quit", 260, 38, 20);
        UITheme.ApplyMutedStyle(quitBtn);
        UITheme.ApplyMenuButtonFinish(quitBtn, UITheme.Magenta, 0.09f, 0.14f);
        quitBtn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); OnQuit(); };
        quitBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        vbox.AddChild(quitBtn);
    }

    private static Button MakePauseBtn(string text, int minW, int minH, int fontSize)
    {
        var btn = new Button
        {
            Text              = text,
            CustomMinimumSize = new Vector2(minW, minH),
        };
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        return btn;
    }

    private static void AddSeparatorLine(VBoxContainer parent)
    {
        parent.AddChild(new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 1),
            Color             = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.12f),
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        });
    }

    private void BuildSettingsPanel(VBoxContainer parent)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.04f, 0.04f, 0.12f),
            border: new Color(0.18f, 0.22f, 0.18f),
            corners: 12, borderWidth: 1, padH: 0, padV: 0));
        parent.AddChild(card);
        _settingsPanel = card;

        float heightFactor = MobileOptimization.IsMobile() ? 0.70f : 0.78f;
        float maxHeight = Mathf.Clamp(
            GetViewport().GetVisibleRect().Size.Y * heightFactor,
            320f,
            MobileOptimization.IsMobile() ? 540f : 620f);

        var cardInner = new VBoxContainer();
        cardInner.AddThemeConstantOverride("separation", 0);
        cardInner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        card.AddChild(cardInner);

        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize    = new Vector2(380f, 0f);
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical    = Control.SizeFlags.ShrinkCenter;
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        cardInner.AddChild(scroll);
        _settingsScroll = scroll;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   16);
        margin.AddThemeConstantOverride("margin_right",  16);
        margin.AddThemeConstantOverride("margin_top",    14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.MouseFilter = Control.MouseFilterEnum.Pass;
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.AddChild(vbox);
        _settingsContent = vbox;

        AddLabel(vbox, "SETTINGS", 36, new Color("#a6d608"));
        AddSpacer(vbox, 6);

        var sm = SettingsManager.Instance;
        AddPauseVolumeRow(vbox, "Master",  sm?.MasterVolume ?? 80f, v => { SettingsManager.Instance?.SetVolume(v);      ApplyRuntimeSettingsNow(); });
        AddPauseVolumeRow(vbox, "Music",   sm?.MusicVolume  ?? 80f, v => { SettingsManager.Instance?.SetMusicVolume(v); ApplyRuntimeSettingsNow(); });
        AddPauseVolumeRow(vbox, "Game FX", sm?.FxVolume     ?? 80f, v => { SettingsManager.Instance?.SetFxVolume(v);    ApplyRuntimeSettingsNow(); });
        AddPauseVolumeRow(vbox, "UI FX",   sm?.UiFxVolume   ?? 80f, v => { SettingsManager.Instance?.SetUiFxVolume(v);  ApplyRuntimeSettingsNow(); });

        AddSpacer(vbox, 4);
        AddPauseSectionHeader(vbox, "DISPLAY");

        bool isFs = sm?.Fullscreen ?? false;
        _pauseFsBtn = AddPauseSettingRow(vbox, "Display Mode",
            isFs ? "Fullscreen" : "Windowed", isOn: isFs, () =>
            {
                SettingsManager.Instance?.ToggleFullscreen();
                bool v = SettingsManager.Instance?.Fullscreen ?? false;
                UpdatePauseValueBtn(_pauseFsBtn, v ? "Fullscreen" : "Windowed", v);
                ApplyRuntimeSettingsNow();
            });

        var cbProfile = sm?.ColorblindProfileType ?? ColorblindProfile.Off;
        _pauseCbBtn = AddPauseSettingRow(vbox, "Colorblind Profile",
            SettingsManager.GetColorblindProfileLabel(cbProfile),
            isOn: cbProfile != ColorblindProfile.Off, () =>
            {
                int current = (int)(SettingsManager.Instance?.ColorblindProfileType ?? ColorblindProfile.Off);
                var next = (ColorblindProfile)((current + 1) % 4);
                SettingsManager.Instance?.SetColorblindProfile(next);
                UpdatePauseValueBtn(
                    _pauseCbBtn,
                    SettingsManager.GetColorblindProfileLabel(next),
                    next != ColorblindProfile.Off);
                ApplyRuntimeSettingsNow();
            });

        bool isRm = sm?.ReducedMotion ?? false;
        _pauseRmBtn = AddPauseSettingRow(vbox, "Reduced Motion",
            OnOffText(isRm), isOn: isRm, () =>
            {
                bool next = !(SettingsManager.Instance?.ReducedMotion ?? false);
                SettingsManager.Instance?.SetReducedMotion(next);
                UpdatePauseValueBtn(_pauseRmBtn, OnOffText(next), next);
                ApplyRuntimeSettingsNow();
            });

        bool isPostFx = sm?.PostFxEnabled ?? true;
        _pausePostFxBtn = AddPauseSettingRow(vbox, "Visual Glow",
            OnOffText(isPostFx), isOn: isPostFx, () =>
            {
                bool next = !(SettingsManager.Instance?.PostFxEnabled ?? true);
                SettingsManager.Instance?.SetPostFxEnabled(next);
                UpdatePauseValueBtn(_pausePostFxBtn, OnOffText(next), next);
                ApplyRuntimeSettingsNow();
            });

        // Back button pinned below scroll
        var sep = new HSeparator();
        sep.Modulate = new Color(0.22f, 0.22f, 0.22f);
        cardInner.AddChild(sep);

        var backMargin = new MarginContainer();
        backMargin.AddThemeConstantOverride("margin_left",   16);
        backMargin.AddThemeConstantOverride("margin_right",  16);
        backMargin.AddThemeConstantOverride("margin_top",    8);
        backMargin.AddThemeConstantOverride("margin_bottom", 8);
        cardInner.AddChild(backMargin);

        var backBtn = new Button
        {
            Text = "\u2190 Back",
            CustomMinimumSize = new Vector2(140, 38),
        };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.09f, 0.11f);
        backBtn.Pressed += () => { SoundManager.Instance?.Play("ui_select"); OnCloseSettings(); };
        backMargin.AddChild(backBtn);

        CallDeferred(nameof(RefreshSettingsPanelHeight), maxHeight);
    }

    private void BuildQuitConfirmPanel(VBoxContainer parent)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        parent.AddChild(vbox);
        _quitConfirmPanel = vbox;

        var msg = new Label
        {
            Text = "Are you sure you want to quit?",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(msg, semiBold: true, size: 26);
        msg.Modulate = Colors.White;
        vbox.AddChild(msg);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnRow);

        var yesBtn = new Button { Text = "Yes", CustomMinimumSize = new Vector2(130, 46) };
        yesBtn.AddThemeFontSizeOverride("font_size", 22);
        UITheme.ApplyMutedStyle(yesBtn);
        UITheme.ApplyMenuButtonFinish(yesBtn, UITheme.Magenta, 0.09f, 0.14f);
        yesBtn.Pressed += () =>
        {
            _quitConfirmPanel.Visible = false;
            OnMainMenu();
        };
        btnRow.AddChild(yesBtn);

        var noBtn = new Button { Text = "No", CustomMinimumSize = new Vector2(130, 46) };
        noBtn.AddThemeFontSizeOverride("font_size", 22);
        UITheme.ApplyPrimaryStyle(noBtn);
        UITheme.ApplyMenuButtonFinish(noBtn, UITheme.Lime, 0.11f, 0.14f);
        noBtn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            _quitConfirmPanel.Visible = false;
            Unpause();
        };
        btnRow.AddChild(noBtn);
    }

    // ── Input ────────────────────────────────────────────────────────────

    public override void _Notification(int what)
    {
        // Android back button arrives as a system notification, not a ui_cancel input event
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
            HandleBack(androidBack: true);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            ToggleGameplayPause();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            HandleBack(androidBack: false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleBack(bool androidBack)
    {
        if (androidBack)
        {
            double now = Time.GetUnixTimeFromSystem();
            if (now - _lastBackTime < 0.5) return;
            _lastBackTime = now;
        }

        var phase = GameController.Instance.CurrentPhase;
        if (phase == GamePhase.Win || phase == GamePhase.Loss) return;

        if (Visible)
        {
            if (_quitConfirmPanel.Visible)
            {
                SoundManager.Instance?.Play("ui_select");
                _quitConfirmPanel.Visible = false;
                Unpause();
            }
            else if (_settingsPanel.Visible)
            {
                SoundManager.Instance?.Play("ui_select");
                OnCloseSettings();
            }
            else
            {
                SoundManager.Instance?.Play("ui_select");
                Unpause();
            }
        }
        else if (androidBack && phase == GamePhase.Wave)
        {
            // Android back during a wave → quit confirmation
            SoundManager.Instance?.Play("ui_select");
            Pause();
            _mainPanel.Visible        = false;
            _quitConfirmPanel.Visible = true;
        }
        else
        {
            // Android back during draft, or desktop ESC → normal pause menu
            SoundManager.Instance?.Play("ui_select");
            Pause();
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────

    public void Pause()
    {
        OpenPauseMenu();
    }

    public void OpenPauseMenu()
    {
        var phase = GameController.Instance.CurrentPhase;
        if (phase == GamePhase.Win || phase == GamePhase.Loss)
            return;

        if (Visible)
        {
            Unpause();
            return;
        }

        _mainPanel.Visible        = true;
        _settingsPanel.Visible    = false;
        _quitConfirmPanel.Visible = false;
        _rootContainer.Modulate = new Color(1f, 1f, 1f, 0f);
        Visible = true;
        GameController.Instance.ActiveTutorial?.SetOverlayPaused(true);
        GetTree().Paused = true;
        var tween = CreateTween();
        tween.SetIgnoreTimeScale(true);
        tween.TweenProperty(_rootContainer, "modulate:a", 1f, 0.16f)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void Unpause()
    {
        if (Visible)
        {
            var tween = CreateTween();
            tween.SetIgnoreTimeScale(true);
            tween.TweenProperty(_rootContainer, "modulate:a", 0f, 0.12f)
                 .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tween.TweenCallback(Callable.From(() =>
            {
                Visible = false;
                _rootContainer.Modulate = Colors.White;
                GameController.Instance.ActiveTutorial?.SetOverlayPaused(false);
                GetTree().Paused = false;
            }));
        }
        else
        {
            GetTree().Paused = false;
        }
    }

    public void ToggleGameplayPause()
    {
        var phase = GameController.Instance.CurrentPhase;
        if (phase == GamePhase.Win || phase == GamePhase.Loss)
            return;

        if (GetTree().Paused)
        {
            Unpause();
            return;
        }

        Visible = false;
        GetTree().Paused = true;
    }

    private void OnResume()  => Unpause();
    private void OnRestart() { Unpause(); GameController.Instance.RestartRun(); }
    private void OnBackToEditor()
    {
        Engine.TimeScale = 1.0;
        GetTree().Paused = false;
        GameController.Instance.AbandonRun();
        MapEditorState.ClearPlaytest();
        SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MapEditor.tscn");
    }
	private void OnMainMenu()
	{
		Engine.TimeScale = 1.0;
		GetTree().Paused = false;
		GameController.Instance.AbandonRun();
        if (MapEditorState.IsPlaytesting)
            MapEditorState.ClearPlaytest();
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}
    private void OnQuit()    => GetTree().Quit();

    private void OnAchievements()
    {
        _mainPanel.Visible = false;
        var panel = new AchievementsPanel();
        panel.BackOverride = () => _mainPanel.Visible = true;
        AddChild(panel);
    }

    private void OnSlotCodex()
    {
        _mainPanel.Visible = false;
        var panel = new SlotCodexPanel();
        panel.BackOverride = () => _mainPanel.Visible = true;
        AddChild(panel);
    }

    private void OnOpenSettings()
    {
        _mainPanel.Visible     = false;
        _settingsPanel.Visible = true;
        float heightFactor = MobileOptimization.IsMobile() ? 0.70f : 0.78f;
        float maxHeight = Mathf.Clamp(
            GetViewport().GetVisibleRect().Size.Y * heightFactor,
            320f,
            MobileOptimization.IsMobile() ? 540f : 620f);
        CallDeferred(nameof(RefreshSettingsPanelHeight), maxHeight);
    }

    private void OnCloseSettings()
    {
        _settingsPanel.Visible = false;
        _mainPanel.Visible     = true;
    }

    private static void ApplyRuntimeSettingsNow()
    {
        Transition.Instance?.RefreshPostFxFromSettings();

        if (!GodotObject.IsInstanceValid(GameController.Instance))
            return;

        GameController.Instance.RefreshInGameSettingVisuals();
    }

    // ── Pause settings helpers (mirror Settings.cs 2-column style) ────────────

    private static Button AddPauseSettingRow(VBoxContainer vbox, string labelText,
        string valueText, bool isOn, System.Action callback)
    {
        var row = new PanelContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeStyleboxOverride("panel", MakePauseRowStyle());
        vbox.AddChild(row);

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 10);
        row.AddChild(inner);

        var lbl = new Label
        {
            Text = labelText,
            VerticalAlignment   = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
        };
        lbl.AddThemeFontSizeOverride("font_size", 17);
        lbl.Modulate = new Color(0.88f, 0.88f, 0.92f);
        inner.AddChild(lbl);

        var btn = new Button
        {
            Text = valueText,
            CustomMinimumSize = new Vector2(90, 26),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        btn.AddThemeFontSizeOverride("font_size", 14);
        ApplyPauseValueButtonStyle(btn, isOn);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.07f, 0.11f);
        btn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); callback(); };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        inner.AddChild(btn);

        return btn;
    }

    private static void AddPauseVolumeRow(VBoxContainer vbox, string labelText, float current,
        System.Action<float> onChange)
    {
        var row = new PanelContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeStyleboxOverride("panel", MakePauseRowStyle());
        vbox.AddChild(row);

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 10);
        row.AddChild(inner);

        var lbl = new Label
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(80, 0),
        };
        lbl.AddThemeFontSizeOverride("font_size", 17);
        lbl.Modulate = new Color(0.88f, 0.88f, 0.92f);
        inner.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = 0, MaxValue = 100, Value = current, Step = 1,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize   = new Vector2(120, 20),
        };
        inner.AddChild(slider);

        var valLbl = new Label
        {
            Text = $"{(int)current}",
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            CustomMinimumSize   = new Vector2(32, 0),
        };
        valLbl.AddThemeFontSizeOverride("font_size", 15);
        valLbl.Modulate = new Color(0.55f, 0.85f, 0.55f);
        inner.AddChild(valLbl);

        slider.ValueChanged += v =>
        {
            valLbl.Text = $"{(int)v}";
            valLbl.Modulate = (int)v > 0
                ? new Color(0.55f, 0.85f, 0.55f)
                : new Color(0.45f, 0.45f, 0.45f);
            onChange((float)v);
        };
    }

    private static void AddPauseSectionHeader(VBoxContainer vbox, string text)
    {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(row);

        var bar = new ColorRect
        {
            Color = UITheme.Lime,
            CustomMinimumSize = new Vector2(3f, 0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter       = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(bar);

        var lbl = new Label { Text = text };
        UITheme.ApplyFont(lbl, semiBold: true, size: 12);
        lbl.Modulate = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.85f);
        lbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(lbl);

        var line = new ColorRect
        {
            CustomMinimumSize   = new Vector2(0, 1),
            Color               = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.12f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(line);
    }

    private static StyleBoxFlat MakePauseRowStyle()
    {
        var s = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.05f, 0.14f, 0.80f),
            CornerRadiusTopLeft     = 5,
            CornerRadiusTopRight    = 5,
            CornerRadiusBottomLeft  = 5,
            CornerRadiusBottomRight = 5,
        };
        s.ContentMarginLeft   = 12;
        s.ContentMarginRight  = 8;
        s.ContentMarginTop    = 5;
        s.ContentMarginBottom = 5;
        return s;
    }

    private static void ApplyPauseValueButtonStyle(Button btn, bool isOn)
    {
        if (isOn)
        {
            btn.AddThemeColorOverride("font_color",         UITheme.Lime);
            btn.AddThemeColorOverride("font_hover_color",   UITheme.Lime);
            btn.AddThemeColorOverride("font_pressed_color", UITheme.LimeDim);
            btn.AddThemeColorOverride("font_focus_color",   UITheme.Lime);
            btn.AddThemeStyleboxOverride("normal",  MakePauseValBtn(new Color(0.06f, 0.14f, 0.04f), UITheme.LimeDark));
            btn.AddThemeStyleboxOverride("hover",   MakePauseValBtn(new Color(0.09f, 0.20f, 0.05f), UITheme.Lime, glowAlpha: 0.10f, glowSize: 3, glowColor: UITheme.Lime));
            btn.AddThemeStyleboxOverride("pressed", MakePauseValBtn(new Color(0.04f, 0.08f, 0.02f), UITheme.LimeDim));
            btn.AddThemeStyleboxOverride("focus",   MakePauseValBtn(new Color(0.09f, 0.20f, 0.05f), UITheme.Lime, glowAlpha: 0.07f, glowSize: 2, glowColor: UITheme.Lime));
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
            btn.AddThemeStyleboxOverride("normal",  MakePauseValBtn(dimBg,                           dimBorder));
            btn.AddThemeStyleboxOverride("hover",   MakePauseValBtn(new Color(0.08f, 0.08f, 0.14f),  dimBorder));
            btn.AddThemeStyleboxOverride("pressed", MakePauseValBtn(dimBg,                           dimBorder));
            btn.AddThemeStyleboxOverride("focus",   MakePauseValBtn(new Color(0.08f, 0.08f, 0.14f),  dimBorder));
        }
    }

    private static StyleBoxFlat MakePauseValBtn(Color bg, Color border,
        float glowAlpha = 0f, int glowSize = 0, Color? glowColor = null)
    {
        var s = UITheme.MakeBtn(bg, border, border: 1, corners: 5,
            glowAlpha: glowAlpha, glowSize: glowSize, glowColor: glowColor);
        s.ContentMarginTop    = 6;
        s.ContentMarginBottom = 2;
        s.ContentMarginLeft   = 8;
        s.ContentMarginRight  = 8;
        return s;
    }

    private static void UpdatePauseValueBtn(Button? btn, string text, bool isOn)
    {
        if (btn == null) return;
        btn.Text = text;
        ApplyPauseValueButtonStyle(btn, isOn);
    }

    private static string OnOffText(bool on) => on ? "ON" : "OFF";

    private void RefreshSettingsPanelHeight(float maxHeight)
    {
        if (!GodotObject.IsInstanceValid(_settingsScroll) || !GodotObject.IsInstanceValid(_settingsContent))
            return;

        float contentHeight = _settingsContent.GetCombinedMinimumSize().Y + 28f; // top + bottom margin
        float targetHeight = Mathf.Clamp(contentHeight, 220f, maxHeight);
        _settingsScroll.CustomMinimumSize = new Vector2(380f, targetHeight);
    }

    private static void AddLabel(Control parent, string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        if (fontSize > 28)
            UITheme.ApplyFont(lbl, semiBold: true);
        lbl.Modulate = color;
        parent.AddChild(lbl);
    }

    private static void AddSpacer(Control parent, int px) =>
        parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });

    private static void AddBtn(Control parent, string text, System.Action callback)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(260, 36) };
        btn.AddThemeFontSizeOverride("font_size", 20);
        UITheme.ApplyCyanStyle(btn);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.09f, 0.11f);
        btn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); callback(); };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        parent.AddChild(btn);
    }
}
