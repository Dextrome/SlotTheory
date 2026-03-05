using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Top bar: Wave X/20, Lives, speed toggle (1x/2x/3x), ESC hint.
/// </summary>
public partial class HudPanel : CanvasLayer
{
    private Label _waveLabel = null!;
    private Label _livesLabel = null!;
    private Label _enemyLabel = null!;
    private Label _speedToast = null!;
    private ColorRect _speedToastStreak = null!;
    private Button _speedBtn = null!;
    private int _speedIdx = 0;
    private static readonly double[] SpeedSteps = { 1.0, 2.0, 3.0 };
    public float CurrentSpeed => (float)SpeedSteps[_speedIdx];

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
        _speedBtn.Text = $"1\u00D7";
        _speedBtn.CustomMinimumSize = new Vector2(50, 0);
        _speedBtn.Pressed += OnSpeedToggle;
        rightHbox.AddChild(_speedBtn);

        // Platform-specific pause controls
        if (OS.GetName() == "Android")
        {
            // Mobile menu button (Android only)
        }
        else
        {
            // Desktop ESC pause button (clickable)
            var escBtn = new Button();
            escBtn.Text = "ESC pause";
            escBtn.AddThemeFontSizeOverride("font_size", 14);
            escBtn.Modulate = new Color(1f, 1f, 1f, 0.7f);
            escBtn.Flat = true;
            escBtn.Pressed += OnEscButtonPressed;
            rightHbox.AddChild(escBtn);
        }

        // Mobile menu button (Android only)
        if (OS.GetName() == "Android")
        {
            var mobileMenuBtn = new Button();
            mobileMenuBtn.Text = "\u2630";
            mobileMenuBtn.CustomMinimumSize = new Vector2(50, 0);
            mobileMenuBtn.AddThemeFontSizeOverride("font_size", 20);
            mobileMenuBtn.Pressed += OnMobileMenuPressed;
            rightHbox.AddChild(mobileMenuBtn);
        }

        // small right pad
        var pad = new Control();
        pad.CustomMinimumSize = new Vector2(8, 0);
        rightHbox.AddChild(pad);

        _speedToast = new Label
        {
            Text = "",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            GrowHorizontal = Control.GrowDirection.Both,
            Position = new Vector2(0f, 54f),
            Modulate = new Color(0.82f, 0.94f, 1.00f, 0f),
        };
        UITheme.ApplyFont(_speedToast, semiBold: true, size: 23);
        AddChild(_speedToast);

        _speedToastStreak = new ColorRect
        {
            Color = new Color(0.35f, 0.85f, 1.00f, 0f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            Position = new Vector2(-90f, 68f),
            Size = new Vector2(180f, 4f),
        };
        AddChild(_speedToastStreak);
    }

    private void OnSpeedToggle()
    {
        _speedIdx = (_speedIdx + 1) % SpeedSteps.Length;
        Engine.TimeScale = SpeedSteps[_speedIdx];
        _speedBtn.Text = $"{SpeedSteps[_speedIdx]:0}\u00D7";
        ShowSpeedToast();
        SoundManager.Instance?.SetSpeedFeel((float)SpeedSteps[_speedIdx]);
        SoundManager.Instance?.Play("ui_speed_shift");
    }

    public void ResetSpeed()
    {
        _speedIdx = 0;
        Engine.TimeScale = 1.0;
        _speedBtn.Text = $"1\u00D7";
        SoundManager.Instance?.SetSpeedFeel(1.0f);
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

    public void PulseWaveLabel()
    {
        _waveLabel.PivotOffset = _waveLabel.Size / 2f;
        var tw = _waveLabel.CreateTween();
        tw.TweenProperty(_waveLabel, "scale", new Vector2(1.18f, 1.18f), 0.08f)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_waveLabel, "scale", Vector2.One, 0.20f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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

    private void OnEscButtonPressed()
    {
        // Find and pause game on desktop (same as mobile menu)
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

    private void ShowSpeedToast()
    {
        _speedToast.Text = $"SPEED {SpeedSteps[_speedIdx]:0}\u00D7";
        _speedToast.Visible = true;
        _speedToast.Scale = new Vector2(1.16f, 1.16f);
        _speedToast.Modulate = new Color(0.82f, 0.94f, 1.00f, 0f);
        _speedToastStreak.Visible = true;
        _speedToastStreak.Position = new Vector2(-130f, 68f);
        _speedToastStreak.Size = new Vector2(180f, 4f);
        _speedToastStreak.Color = new Color(0.35f, 0.85f, 1.00f, 0f);

        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(_speedToast, "modulate:a", 1f, 0.06f);
        tw.TweenProperty(_speedToast, "scale", Vector2.One, 0.12f)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_speedToastStreak, "color:a", 0.34f, 0.06f);
        tw.TweenProperty(_speedToastStreak, "position:x", 70f, 0.20f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.Chain();
        tw.SetParallel(true);
        tw.TweenProperty(_speedToast, "modulate:a", 0f, 0.16f);
        tw.TweenProperty(_speedToastStreak, "color:a", 0f, 0.14f);
        tw.Chain().TweenCallback(Callable.From(() =>
        {
            _speedToast.Visible = false;
            _speedToastStreak.Visible = false;
        }));
    }
}

