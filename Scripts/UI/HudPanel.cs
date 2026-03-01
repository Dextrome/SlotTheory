using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Top bar showing Wave X/20 and Lives remaining. Updated every frame during combat.
/// </summary>
public partial class HudPanel : CanvasLayer
{
    private Label _waveLabel = null!;
    private Label _livesLabel = null!;

    public override void _Ready()
    {
        Layer = 1;

        var bar = new Panel();
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, 44);
        AddChild(bar);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 0);
        bar.AddChild(hbox);

        var left = new Control();
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(left);

        _waveLabel = new Label();
        _waveLabel.Text = "Wave 1 / 20";
        _waveLabel.AddThemeFontSizeOverride("font_size", 22);
        _waveLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(_waveLabel);

        var mid = new Control();
        mid.CustomMinimumSize = new Vector2(60, 0);
        hbox.AddChild(mid);

        _livesLabel = new Label();
        _livesLabel.Text = $"Lives: {Balance.StartingLives}";
        _livesLabel.AddThemeFontSizeOverride("font_size", 22);
        _livesLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(_livesLabel);

        var right = new Control();
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(right);
    }

    public void Refresh(int wave, int lives)
    {
        _waveLabel.Text = $"Wave {wave} / {Balance.TotalWaves}";
        _livesLabel.Text = $"Lives: {lives}";
        _livesLabel.Modulate = lives <= 3 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
    }
}
