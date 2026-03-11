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
    private double  _lastBackTime     = -1.0;  // debounce Android back (fires on key-down AND key-up)
    private Button? _pauseFsBtn;
    private Button? _pauseCbBtn;
    private Button? _pauseRmBtn;

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
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.04f, 0.04f, 0.12f),
            border: new Color(0.18f, 0.22f, 0.18f),
            corners: 12, borderWidth: 1, padH: 24, padV: 20));
        parent.AddChild(card);
        _mainPanel = card;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        card.AddChild(vbox);

        AddLabel(vbox, "PAUSED", 48, UITheme.Lime);
        AddSpacer(vbox, 6);
        AddSeparatorLine(vbox);
        AddSpacer(vbox, 6);

        var resumeBtn = MakePauseBtn("Resume", 260, 50, 22);
        UITheme.ApplyPrimaryStyle(resumeBtn);
        resumeBtn.Pressed += () => { SoundManager.Instance?.Play("ui_select"); OnResume(); };
        resumeBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        vbox.AddChild(resumeBtn);

        AddSpacer(vbox, 4);
        AddBtn(vbox, "Restart Run",  OnRestart);
        AddBtn(vbox, "Settings",     OnOpenSettings);
        AddBtn(vbox, "How to Play",  OnHowToPlay);
        AddBtn(vbox, "Achievements", OnAchievements);
        AddBtn(vbox, "Slot Codex",   OnSlotCodex);
        AddSpacer(vbox, 4);
        AddSeparatorLine(vbox);
        AddSpacer(vbox, 4);

        var mmBtn = MakePauseBtn("Main Menu", 260, 44, 20);
        UITheme.ApplyCyanStyle(mmBtn);
        mmBtn.Pressed += () => { SoundManager.Instance?.Play("ui_select"); OnMainMenu(); };
        mmBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        vbox.AddChild(mmBtn);

        var quitBtn = MakePauseBtn("Quit", 260, 44, 20);
        UITheme.ApplyMutedStyle(quitBtn);
        quitBtn.Pressed += () => { SoundManager.Instance?.Play("ui_select"); OnQuit(); };
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
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        parent.AddChild(vbox);
        _settingsPanel = vbox;

        AddLabel(vbox, "SETTINGS", 42, new Color("#a6d608"));
        AddSpacer(vbox, 8);

        var sm = SettingsManager.Instance;
        AddVolumeRow(vbox, "Master", sm?.MasterVolume ?? 80f,
            v => SettingsManager.Instance?.SetVolume(v));
        AddVolumeRow(vbox, "Music",  sm?.MusicVolume  ?? 80f,
            v => SettingsManager.Instance?.SetMusicVolume(v));
        AddVolumeRow(vbox, "FX",     sm?.FxVolume     ?? 80f,
            v => SettingsManager.Instance?.SetFxVolume(v));

        // Fullscreen toggle
        bool isFs = SettingsManager.Instance?.Fullscreen ?? false;
        _pauseFsBtn = new Button
        {
            Text              = "Display:  " + (isFs ? "Fullscreen" : "Windowed"),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _pauseFsBtn.AddThemeFontSizeOverride("font_size", 20);
        _pauseFsBtn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            SettingsManager.Instance?.ToggleFullscreen();
            if (_pauseFsBtn != null)
                _pauseFsBtn.Text = "Display:  " + ((SettingsManager.Instance?.Fullscreen ?? false) ? "Fullscreen" : "Windowed");
        };
        vbox.AddChild(_pauseFsBtn);

        // Colorblind toggle
        bool isCb = SettingsManager.Instance?.ColorblindMode ?? false;
        _pauseCbBtn = new Button
        {
            Text              = "Colorblind:  " + (isCb ? "On" : "Off"),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _pauseCbBtn.AddThemeFontSizeOverride("font_size", 20);
        _pauseCbBtn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            bool next = !(SettingsManager.Instance?.ColorblindMode ?? false);
            SettingsManager.Instance?.SetColorblindMode(next);
            if (_pauseCbBtn != null) _pauseCbBtn.Text = "Colorblind:  " + (next ? "On" : "Off");
        };
        vbox.AddChild(_pauseCbBtn);

        // Reduced motion toggle
        bool isRm = SettingsManager.Instance?.ReducedMotion ?? false;
        _pauseRmBtn = new Button
        {
            Text              = "Reduced Motion:  " + (isRm ? "On" : "Off"),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _pauseRmBtn.AddThemeFontSizeOverride("font_size", 20);
        _pauseRmBtn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            bool next = !(SettingsManager.Instance?.ReducedMotion ?? false);
            SettingsManager.Instance?.SetReducedMotion(next);
            if (_pauseRmBtn != null) _pauseRmBtn.Text = "Reduced Motion:  " + (next ? "On" : "Off");
        };
        vbox.AddChild(_pauseRmBtn);

        AddSpacer(vbox, 8);
        AddBtn(vbox, "\u2190 Back", OnCloseSettings);
    }

    private void BuildQuitConfirmPanel(VBoxContainer parent)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 28);
        parent.AddChild(vbox);
        _quitConfirmPanel = vbox;

        var msg = new Label
        {
            Text = "Are you really just gonna\nquit like a little bitch?",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        SlotTheory.Core.UITheme.ApplyFont(msg, semiBold: true, size: 32);
        msg.Modulate = Colors.White;
        vbox.AddChild(msg);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 20);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnRow);

        var yesBtn = new Button { Text = "Yes", CustomMinimumSize = new Vector2(140, 56) };
        yesBtn.AddThemeFontSizeOverride("font_size", 24);
        UITheme.ApplyMutedStyle(yesBtn);
        yesBtn.Pressed += () =>
        {
            _quitConfirmPanel.Visible = false;
            OnMainMenu();
        };
        btnRow.AddChild(yesBtn);

        var noBtn = new Button { Text = "No", CustomMinimumSize = new Vector2(140, 56) };
        noBtn.AddThemeFontSizeOverride("font_size", 24);
        UITheme.ApplyPrimaryStyle(noBtn);
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
        _mainPanel.Visible        = true;
        _settingsPanel.Visible    = false;
        _quitConfirmPanel.Visible = false;
        Visible = true;
        GetTree().Paused = true;
    }
    public void Unpause() { Visible = false; GetTree().Paused = false; }

    private void OnResume()  => Unpause();
    private void OnRestart() { Unpause(); GameController.Instance.RestartRun(); }
	private void OnMainMenu()
	{
		GetTree().Paused = false;
		GameController.Instance.AbandonRun();
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}
    private void OnQuit()    => GetTree().Quit();

    private void OnHowToPlay()
    {
        _mainPanel.Visible = false;
        var howTo = new HowToPlay();
        howTo.OnBack = () => _mainPanel.Visible = true;
        AddChild(howTo);
    }

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
    }

    private void OnCloseSettings()
    {
        _settingsPanel.Visible = false;
        _mainPanel.Visible     = true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void AddVolumeRow(VBoxContainer vbox, string label, float current,
        System.Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(row);

        var lbl = new Label { Text = label };
        lbl.AddThemeFontSizeOverride("font_size", 17);
        lbl.Modulate = new Color(0.85f, 0.85f, 0.85f);
        lbl.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue            = 0,
            MaxValue            = 100,
            Value               = current,
            Step                = 1,
            CustomMinimumSize   = new Vector2(150, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(slider);

        var valLbl = new Label { Text = $"{(int)current}" };
        valLbl.AddThemeFontSizeOverride("font_size", 17);
        valLbl.Modulate = new Color(0.60f, 0.60f, 0.60f);
        valLbl.CustomMinimumSize = new Vector2(36, 0);
        row.AddChild(valLbl);

        slider.ValueChanged += v =>
        {
            valLbl.Text = $"{(int)v}";
            onChange((float)v);
        };
    }

    private static void AddLabel(Control parent, string text, int fontSize, Color color)
    {
        var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        if (fontSize > 28)
            SlotTheory.Core.UITheme.ApplyFont(lbl, semiBold: true);
        lbl.Modulate = color;
        parent.AddChild(lbl);
    }

    private static void AddSpacer(Control parent, int px) =>
        parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });

    private static void AddBtn(Control parent, string text, System.Action callback)
    {
        var btn = new Button { Text = text, CustomMinimumSize = new Vector2(260, 44) };
        btn.AddThemeFontSizeOverride("font_size", 20);
        btn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            callback();
        };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        parent.AddChild(btn);
    }
}
