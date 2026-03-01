using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Pause overlay. Esc toggles pause from any phase except Win/Loss.
/// ProcessMode = Always so it receives input even while the scene tree is paused.
/// </summary>
public partial class PauseScreen : CanvasLayer
{
    private Control _panel = null!;

    public override void _Ready()
    {
        Layer = 8;                                    // below EndScreen (10), above HUD (1)
        ProcessMode = ProcessModeEnum.Always;         // must process while paused
        Visible = false;

        // Dark overlay
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.80f);
        AddChild(bg);

        // Center panel
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        _panel = new VBoxContainer();
        ((VBoxContainer)_panel).AddThemeConstantOverride("separation", 16);
        center.AddChild(_panel);

        AddLabel("PAUSED", 52, Colors.White);
        AddSpacer(12);
        AddButton("Resume",      OnResume);
        AddButton("Restart Run", OnRestart);
        AddSpacer(8);
        AddVolumeRow();
        AddButton("Fullscreen: " + FullscreenLabel(), OnToggleFullscreen);
        AddSpacer(12);
        AddButton("Main Menu",       OnMainMenu);
        AddButton("Quit to Desktop", OnQuit);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))   // Esc
        {
            // Only pause/unpause during wave or draft — not on end screen
            var phase = GameController.Instance.CurrentPhase;
            if (phase == GamePhase.Win || phase == GamePhase.Loss) return;

            if (Visible) Unpause();
            else         Pause();

            GetViewport().SetInputAsHandled();
        }
    }

    private void Pause()
    {
        Visible = true;
        GetTree().Paused = true;
    }

    private void Unpause()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    private void OnResume()
    {
        Unpause();
    }

    private void OnRestart()
    {
        Unpause();
        GameController.Instance.RestartRun();
    }

    private void OnToggleFullscreen()
    {
        SlotTheory.Core.SettingsManager.Instance?.ToggleFullscreen();
    }

    private static string FullscreenLabel() =>
        (SlotTheory.Core.SettingsManager.Instance?.Fullscreen ?? false) ? "On" : "Off";

    private void OnMainMenu()
    {
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    private void OnQuit() => GetTree().Quit();

    // ── helpers ──────────────────────────────────────────────────────────────

    private void AddLabel(string text, int fontSize, Color color)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.AddThemeFontSizeOverride("font_size", fontSize);
        lbl.Modulate = color;
        _panel.AddChild(lbl);
    }

    private void AddSpacer(int px)
    {
        var s = new Control();
        s.CustomMinimumSize = new Vector2(0, px);
        _panel.AddChild(s);
    }

    private void AddButton(string text, System.Action callback)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(260, 44);
        btn.Pressed += callback;
        _panel.AddChild(btn);
    }

    private void AddVolumeRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(row);

        var lbl = new Label { Text = "Volume" };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        lbl.Modulate = new Color(0.80f, 0.80f, 0.80f);
        lbl.CustomMinimumSize = new Vector2(72, 0);
        row.AddChild(lbl);

        float vol = SlotTheory.Core.SettingsManager.Instance?.MasterVolume ?? 80f;
        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value    = vol,
            Step     = 1,
            CustomMinimumSize   = new Vector2(160, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(slider);

        slider.ValueChanged += v =>
            SlotTheory.Core.SettingsManager.Instance?.SetVolume((float)v);
    }
}
