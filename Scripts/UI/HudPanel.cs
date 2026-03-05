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
    private Label _enemyLabel = null!;
    private Button _speedBtn = null!;
    private int _speedIdx = 0;
    private static readonly double[] SpeedSteps = { 1.0, 2.0, 3.0 };

    public override void _Ready()
    {
        Layer = 1;

        var bar = new Panel();
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, 44);
        bar.Theme = SlotTheory.Core.UITheme.Build();
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

        _enemyLabel = new Label();
        _enemyLabel.Text = "";
        _enemyLabel.AddThemeFontSizeOverride("font_size", 16);
        _enemyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _enemyLabel.Modulate = new Color(1f, 1f, 1f, 0.6f);
        hbox.AddChild(_enemyLabel);

        var mid2 = new Control();
        mid2.CustomMinimumSize = new Vector2(40, 0);
        hbox.AddChild(mid2);

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

        // Mobile menu button (Android only)
        if (OS.GetName() == "Android")
        {
            var mobileMenuBtn = new Button();
            mobileMenuBtn.Text = "☰";
            mobileMenuBtn.CustomMinimumSize = new Vector2(50, 0);
            mobileMenuBtn.AddThemeFontSizeOverride("font_size", 20);
            mobileMenuBtn.Pressed += OnMobileMenuPressed;
            rightHbox.AddChild(mobileMenuBtn);
        }

        // small right pad
        var pad = new Control();
        pad.CustomMinimumSize = new Vector2(8, 0);
        rightHbox.AddChild(pad);
    }

    private void OnSpeedToggle()
    {
        _speedIdx = (_speedIdx + 1) % SpeedSteps.Length;
        Engine.TimeScale = SpeedSteps[_speedIdx];
        _speedBtn.Text = $"{SpeedSteps[_speedIdx]:0}×";
    }

    public void ResetSpeed()
    {
        _speedIdx = 0;
        Engine.TimeScale = 1.0;
        _speedBtn.Text = "1×";
    }

    public void FlashLives()
    {
        _livesLabel.PivotOffset = _livesLabel.Size / 2f;
        var tween = CreateTween();
        tween.TweenProperty(_livesLabel, "scale", new Vector2(1.4f, 1.4f), 0.06f);
        tween.TweenProperty(_livesLabel, "scale", Vector2.One, 0.3f)
             .SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
    }

    public void Refresh(int wave, int lives)
    {
        _waveLabel.Text = $"Wave {wave} / {Balance.TotalWaves}";
        _livesLabel.Text = $"Lives: {lives}";
        _livesLabel.Modulate = lives <= 3 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
        _livesLabel.PivotOffset = _livesLabel.Size / 2f;
    }

    public void RefreshEnemies(int alive, int total)
    {
        _enemyLabel.Text = alive > 0 ? $"{alive} / {total}" : "";
    }

    private void OnMobileMenuPressed()
    {
        // Find and pause game on mobile
        var pauseScreens = GetTree().GetNodesInGroup("pause_screen");
        
        foreach (Node node in pauseScreens)
        {
            if (node is PauseScreen pauseScreen)
            {
                pauseScreen.Pause();
                break;
            }
        }
    }
}
