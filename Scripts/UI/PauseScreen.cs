using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Pause overlay. Esc toggles pause from any phase except Win/Loss.
/// ProcessMode = Always so it receives input even while the scene tree is paused.
/// </summary>
public partial class PauseScreen : CanvasLayer
{
    private Control _mainPanel     = null!;
    private Control _settingsPanel = null!;

    public override void _Ready()
    {
        Layer       = 8;
        ProcessMode = ProcessModeEnum.Always;
        Visible     = false;

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

        _settingsPanel.Visible = false;
    }

    // ── Panel builders ───────────────────────────────────────────────────

    private void BuildMainPanel(VBoxContainer parent)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        parent.AddChild(vbox);
        _mainPanel = vbox;

        AddLabel(vbox, "PAUSED", 52, Colors.White);
        AddSpacer(vbox, 10);
        AddBtn(vbox, "Resume",      OnResume);
        AddBtn(vbox, "Restart Run", OnRestart);
        AddBtn(vbox, "Settings",    OnOpenSettings);
        AddSpacer(vbox, 10);
        AddBtn(vbox, "Main Menu",       OnMainMenu);
        AddBtn(vbox, "Quit to Desktop", OnQuit);
    }

    private void BuildSettingsPanel(VBoxContainer parent)
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        parent.AddChild(vbox);
        _settingsPanel = vbox;

        AddLabel(vbox, "SETTINGS", 42, new Color("#a6d608"));
        AddSpacer(vbox, 8);

        // Volume row
        var volRow = new HBoxContainer();
        volRow.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(volRow);

        var volLbl = new Label { Text = "Master Volume" };
        volLbl.AddThemeFontSizeOverride("font_size", 17);
        volLbl.Modulate = new Color(0.85f, 0.85f, 0.85f);
        volLbl.CustomMinimumSize = new Vector2(160, 0);
        volRow.AddChild(volLbl);

        float vol    = SettingsManager.Instance?.MasterVolume ?? 80f;
        var slider   = new HSlider
        {
            MinValue            = 0,
            MaxValue            = 100,
            Value               = vol,
            Step                = 1,
            CustomMinimumSize   = new Vector2(150, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        volRow.AddChild(slider);

        var volVal = new Label { Text = $"{(int)vol}" };
        volVal.AddThemeFontSizeOverride("font_size", 17);
        volVal.Modulate = new Color(0.60f, 0.60f, 0.60f);
        volVal.CustomMinimumSize = new Vector2(36, 0);
        volRow.AddChild(volVal);

        slider.ValueChanged += v =>
        {
            volVal.Text = $"{(int)v}";
            SettingsManager.Instance?.SetVolume((float)v);
        };

        // Fullscreen toggle
        bool isFs   = SettingsManager.Instance?.Fullscreen ?? false;
        var fsBtn   = new Button
        {
            Text              = "Display:  " + (isFs ? "Fullscreen" : "Windowed"),
            CustomMinimumSize = new Vector2(260, 44),
        };
        fsBtn.AddThemeFontSizeOverride("font_size", 20);
        fsBtn.Pressed += () =>
        {
            SettingsManager.Instance?.ToggleFullscreen();
            fsBtn.Text = "Display:  " + ((SettingsManager.Instance?.Fullscreen ?? false) ? "Fullscreen" : "Windowed");
        };
        vbox.AddChild(fsBtn);

        AddSpacer(vbox, 8);
        AddBtn(vbox, "← Back", OnCloseSettings);
    }

    // ── Input ────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            var phase = GameController.Instance.CurrentPhase;
            if (phase == GamePhase.Win || phase == GamePhase.Loss) return;

            if (Visible)
            {
                // Esc while settings open → back to main pause panel
                if (_settingsPanel.Visible)
                    OnCloseSettings();
                else
                    Unpause();
            }
            else
            {
                Pause();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    // ── Actions ──────────────────────────────────────────────────────────

    private void Pause()   { Visible = true;  GetTree().Paused = true; }
    private void Unpause() { Visible = false; GetTree().Paused = false; }

    private void OnResume()  => Unpause();
    private void OnRestart() { Unpause(); GameController.Instance.RestartRun(); }
    private void OnMainMenu() { GetTree().Paused = false; SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn"); }
    private void OnQuit()    => GetTree().Quit();

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
        btn.Pressed += callback;
        parent.AddChild(btn);
    }
}
