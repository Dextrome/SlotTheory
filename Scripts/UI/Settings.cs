using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen settings page. All UI built procedurally.
/// </summary>
public partial class Settings : Node
{
    private Button? _fullscreenBtn;

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
        title.AddThemeFontSizeOverride("font_size", 52);
        title.Modulate = new Color("#a6d608");
        vbox.AddChild(title);

        AddSpacer(vbox, 20);

        // ── Audio ─────────────────────────────────────────────────────
        AddSectionHeader(vbox, "AUDIO");

        var settings = SettingsManager.Instance;
        float currentVol = settings?.MasterVolume ?? 80f;

        var volRow = new HBoxContainer();
        volRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(volRow);

        var volLabel = new Label { Text = "Master Volume" };
        volLabel.AddThemeFontSizeOverride("font_size", 18);
        volLabel.Modulate = new Color(0.85f, 0.85f, 0.85f);
        volLabel.CustomMinimumSize = new Vector2(180, 0);
        volRow.AddChild(volLabel);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Value    = currentVol,
            Step     = 1,
            CustomMinimumSize = new Vector2(160, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        volRow.AddChild(slider);

        var volValue = new Label { Text = $"{(int)currentVol}" };
        volValue.AddThemeFontSizeOverride("font_size", 18);
        volValue.Modulate = new Color(0.65f, 0.65f, 0.65f);
        volValue.CustomMinimumSize = new Vector2(38, 0);
        volRow.AddChild(volValue);

        slider.ValueChanged += v =>
        {
            volValue.Text = $"{(int)v}";
            SettingsManager.Instance?.SetVolume((float)v);
        };

        AddSpacer(vbox, 8);

        // ── Display ───────────────────────────────────────────────────
        AddSectionHeader(vbox, "DISPLAY");

        bool isFs = settings?.Fullscreen ?? false;
        _fullscreenBtn = new Button
        {
            Text = FullscreenLabel(isFs),
            CustomMinimumSize = new Vector2(260, 44),
        };
        _fullscreenBtn.AddThemeFontSizeOverride("font_size", 20);
        _fullscreenBtn.Pressed += OnToggleFullscreen;
        vbox.AddChild(_fullscreenBtn);

        AddSpacer(vbox, 24);

        // ── Back ──────────────────────────────────────────────────────
        var back = new Button
        {
            Text = "← Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        back.AddThemeFontSizeOverride("font_size", 20);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        vbox.AddChild(back);
    }

    private void OnToggleFullscreen()
    {
        SettingsManager.Instance?.ToggleFullscreen();
        if (_fullscreenBtn != null)
            _fullscreenBtn.Text = FullscreenLabel(SettingsManager.Instance?.Fullscreen ?? false);
    }

    private static string FullscreenLabel(bool full) =>
        full ? "Display:  Fullscreen" : "Display:  Windowed";

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
