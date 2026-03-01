using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Top bar: Wave X/20, Lives, speed toggle (1×/2×), ESC hint.
/// </summary>
public partial class HudPanel : CanvasLayer
{
    private Label _waveLabel = null!;
    private Label _livesLabel = null!;
    private Button _speedBtn = null!;
    private bool _fast = false;

    public override void _Ready()
    {
        Layer = 1;

        var bar = new Panel();
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, 44);
        AddChild(bar);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 8);
        bar.AddChild(hbox);

        // Left spacer
        var left = new Control();
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(left);

        _waveLabel = new Label();
        _waveLabel.Text = "Wave 1 / 20";
        _waveLabel.AddThemeFontSizeOverride("font_size", 22);
        _waveLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(_waveLabel);

        var mid = new Control();
        mid.CustomMinimumSize = new Vector2(40, 0);
        hbox.AddChild(mid);

        _livesLabel = new Label();
        _livesLabel.Text = $"Lives: {Balance.StartingLives}";
        _livesLabel.AddThemeFontSizeOverride("font_size", 22);
        _livesLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hbox.AddChild(_livesLabel);

        // Right side: speed toggle + ESC hint
        var right = new Control();
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(right);

        var rightHbox = new HBoxContainer();
        rightHbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rightHbox.Alignment = BoxContainer.AlignmentMode.End;
        rightHbox.AddThemeConstantOverride("separation", 8);
        right.AddChild(rightHbox);

        _speedBtn = new Button();
        _speedBtn.Text = "1×";
        _speedBtn.CustomMinimumSize = new Vector2(50, 0);
        _speedBtn.Pressed += OnSpeedToggle;
        rightHbox.AddChild(_speedBtn);

        var escHint = new Label();
        escHint.Text = "ESC pause";
        escHint.AddThemeFontSizeOverride("font_size", 14);
        escHint.Modulate = new Color(1f, 1f, 1f, 0.5f);
        escHint.VerticalAlignment = VerticalAlignment.Center;
        rightHbox.AddChild(escHint);

        // small right pad
        var pad = new Control();
        pad.CustomMinimumSize = new Vector2(8, 0);
        rightHbox.AddChild(pad);
    }

    private void OnSpeedToggle()
    {
        _fast = !_fast;
        Engine.TimeScale = _fast ? 2.0 : 1.0;
        _speedBtn.Text = _fast ? "2×" : "1×";
    }

    public void ResetSpeed()
    {
        _fast = false;
        _speedBtn.Text = "1×";
    }

    public void Refresh(int wave, int lives)
    {
        _waveLabel.Text = $"Wave {wave} / {Balance.TotalWaves}";
        _livesLabel.Text = $"Lives: {lives}";
        _livesLabel.Modulate = lives <= 3 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
    }
}
