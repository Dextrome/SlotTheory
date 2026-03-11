using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Top bar: Wave X/20, Lives, speed toggle (1x/2x/3x/5x), ESC hint.
/// </summary>
public partial class HudPanel : CanvasLayer
{
    private Label _waveLabel = null!;
    private RichTextLabel _buildLabel = null!;
    private Label _livesLabel = null!;
    private Label _enemyLabel = null!;
    private Label _timeLabel = null!;
    private Label _devStatsLabel = null!;
    private Label _speedToast = null!;
    private ColorRect _speedToastStreak = null!;
    private Panel _globalSpectaclePanel = null!;
    private ProgressBar _globalSpectacleBar = null!;
    private Label _globalSpectacleLabel = null!;
    private Button _speedBtn = null!;
    private int _speedIdx = 0;
    private static readonly double[] SpeedStepsNormal = { 1.0, 2.0, 3.0 };
    private static readonly double[] SpeedStepsDev    = { 1.0, 2.0, 3.0, 5.0, 10.0 };
    private double[] SpeedSteps => (SlotTheory.Core.SettingsManager.Instance?.DevMode == true)
        ? SpeedStepsDev : SpeedStepsNormal;
    private const float ZoomMin = 1.0f;
    private const float ZoomMax = 2.6f;
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

        var leftPad = new Control();
        leftPad.CustomMinimumSize = new Vector2(18f, 0f);
        hbox.AddChild(leftPad);

        _buildLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = false,
            FitContent = true,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(340f, 0f),
            Modulate = new Color(0.74f, 0.88f, 1.00f, 0.96f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _buildLabel.AddThemeFontOverride("normal_font", UITheme.SemiBold);
        _buildLabel.AddThemeFontSizeOverride("normal_font_size", 30);
        _buildLabel.AddThemeConstantOverride("line_separation", 0);
        hbox.AddChild(_buildLabel);

        // Spacer to keep right HUD controls on the far side.
        var left = new Control();
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(left);

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

        // Wave label — pinned exactly to screen center via full-width anchor + center alignment.
        _waveLabel = new Label
        {
            Text = "Wave 1 / 20",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft   = 0f,
            AnchorRight  = 1f,
            AnchorTop    = 0f,
            AnchorBottom = 0f,
            OffsetLeft   = 0f,
            OffsetRight  = 0f,
            OffsetTop    = 0f,
            OffsetBottom = 44f,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
        };
        _waveLabel.AddThemeFontSizeOverride("font_size", 22);
        bar.AddChild(_waveLabel);

        float uiScale = MobileOptimization.GetUIScale();
        float enemyOffsetX = MobileOptimization.IsMobile() ? 120f * uiScale : 175f;
        float livesOffsetX = MobileOptimization.IsMobile() ? 260f * uiScale : 390f;

        _enemyLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft   = 0.5f,
            AnchorRight  = 0.5f,
            AnchorTop    = 0f,
            AnchorBottom = 0f,
            OffsetLeft   = enemyOffsetX - 90f,
            OffsetRight  = enemyOffsetX + 90f,
            OffsetTop    = 0f,
            OffsetBottom = 44f,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
            Modulate     = new Color(1f, 1f, 1f, 0.62f),
        };
        _enemyLabel.AddThemeFontSizeOverride("font_size", 16);
        bar.AddChild(_enemyLabel);

        _livesLabel = new Label
        {
            Text = $"Lives: {Balance.StartingLives}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = livesOffsetX - 92f,
            OffsetRight = livesOffsetX + 92f,
            OffsetTop = 0f,
            OffsetBottom = 44f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _livesLabel.AddThemeFontSizeOverride("font_size", 22);
        bar.AddChild(_livesLabel);

        // Timer — centered between build-name right edge (~370px) and screen center (50%).
        // Anchored at 35% of screen width so it sits visually between the two.
        _timeLabel = new Label
        {
            Text = "0:00",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft   = 0.35f,
            AnchorRight  = 0.35f,
            AnchorTop    = 0f,
            AnchorBottom = 0f,
            OffsetLeft   = -60f,
            OffsetRight  =  60f,
            OffsetTop    = 0f,
            OffsetBottom = 44f,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
            Modulate     = new Color(1f, 1f, 1f, 0.55f),
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        bar.AddChild(_timeLabel);

        _devStatsLabel = new Label
        {
            Text = "",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0f,
            AnchorRight = 0f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 10f,
            OffsetRight = 760f,
            OffsetTop = 46f,
            OffsetBottom = 66f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.70f, 0.95f, 1.00f, 0.78f),
        };
        _devStatsLabel.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_devStatsLabel);

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

        BuildGlobalSpectacleMeter();
    }

    private void OnSpeedToggle()
    {
        var steps = SpeedSteps;
        // Clamp index in case dev mode was just disabled while at a high speed
        _speedIdx = Mathf.Min(_speedIdx, steps.Length - 1);
        _speedIdx = (_speedIdx + 1) % steps.Length;
        Engine.TimeScale = steps[_speedIdx];
        _speedBtn.Text = $"{steps[_speedIdx]:0}\u00D7";
        ShowSpeedToast();
        SoundManager.Instance?.SetSpeedFeel((float)steps[_speedIdx]);
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

    public void RefreshTime(float totalSeconds)
    {
        _timeLabel.Text = FormatTime(totalSeconds);
    }

    private static string FormatTime(float seconds)
    {
        int s = (int)seconds;
        return $"{s / 60}:{s % 60:D2}";
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

    public void SetBuildName(string buildName, bool visible = true, Color? startColor = null, Color? endColor = null)
    {
        if (!GodotObject.IsInstanceValid(_buildLabel)) return;
        _buildLabel.Clear();
        if (buildName.Length > 0)
        {
            var c0 = startColor ?? new Color(0.74f, 0.88f, 1.00f);
            var c1 = endColor ?? c0;
            _buildLabel.AppendText(BuildGradientBbCode(buildName, c0, c1));
        }
        _buildLabel.Visible = visible && buildName.Length > 0;
    }

    private static string BuildGradientBbCode(string text, Color start, Color end)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        if (text.Length == 1)
            return $"[color=#{start.ToHtml(false)}]{text}[/color]";

        var sb = new System.Text.StringBuilder(text.Length * 24);
        for (int i = 0; i < text.Length; i++)
        {
            float t = i / (float)(text.Length - 1);
            var c = start.Lerp(end, t);
            sb.Append("[color=#").Append(c.ToHtml(false)).Append(']');
            sb.Append(text[i]);
            sb.Append("[/color]");
        }
        return sb.ToString();
    }

    public void RefreshEnemies(int alive, int total)
    {
        _enemyLabel.Text = alive > 0 ? $"Enemies: {alive} / {total}" : "";
    }

    public void RefreshGlobalSpectacleMeter(float meter, float threshold, bool visible)
    {
        if (!GodotObject.IsInstanceValid(_globalSpectaclePanel)
            || !GodotObject.IsInstanceValid(_globalSpectacleBar)
            || !GodotObject.IsInstanceValid(_globalSpectacleLabel))
            return;

        bool canShow = visible && threshold > 0.001f;
        _globalSpectaclePanel.Visible = canShow;
        if (!canShow)
            return;

        float clampedThreshold = Mathf.Max(1f, threshold);
        float clampedMeter = Mathf.Clamp(meter, 0f, clampedThreshold);
        _globalSpectacleBar.MaxValue = clampedThreshold;
        _globalSpectacleBar.Value = clampedMeter;
        _globalSpectacleLabel.Text = $"GLOBAL SPECTACLE {Mathf.RoundToInt(clampedMeter)}/{Mathf.RoundToInt(clampedThreshold)}";
    }

    public void RefreshDevRenderStats(bool enabled, int enemiesAlive, string perfSummary)
    {
        if (!enabled)
        {
            _devStatsLabel.Visible = false;
            return;
        }

        var sm = SettingsManager.Instance;
        string layeredFlags = $"L:{(sm?.LayeredEnemyRendering == true ? 1 : 0)} E:{(sm?.EnemyEmissiveLines == true ? 1 : 0)} D:{(sm?.EnemyDamageMaterial == true ? 1 : 0)} B:{(sm?.EnemyBloomHighlights == true ? 1 : 0)}";

        _devStatsLabel.Visible = true;
        _devStatsLabel.Text =
            $"[DEV] {perfSummary} | {layeredFlags} | alive:{enemiesAlive} " +
            $"body:{EnemyRenderDebugCounters.BodyPassCalls} dmg:{EnemyRenderDebugCounters.DamagePassCalls} em:{EnemyRenderDebugCounters.EmissivePassCalls} " +
            $"bloom:{EnemyRenderDebugCounters.BloomPassCalls}/{EnemyRenderDebugCounters.BloomPrimitives} " +
            $"fallback:{EnemyRenderDebugCounters.BloomFallbackCalls}/{EnemyRenderDebugCounters.BloomFallbackPrimitives} " +
            $"budget:{EnemyRenderDebugCounters.BloomBudgetUsed}/{EnemyRenderDebugCounters.BloomBudgetCap} drop:{EnemyRenderDebugCounters.BloomBudgetRejected}";
    }

    public void SetMobileZoomReadability(float zoomLevel)
    {
        if (!MobileOptimization.IsMobile())
            return;

        float t = Mathf.Clamp((zoomLevel - ZoomMin) / (ZoomMax - ZoomMin), 0f, 1f);
        int buildSize = Mathf.RoundToInt(Mathf.Lerp(30f, 40f, t));
        int waveSize = Mathf.RoundToInt(Mathf.Lerp(22f, 30f, t));
        int livesSize = Mathf.RoundToInt(Mathf.Lerp(22f, 30f, t));
        int enemySize = Mathf.RoundToInt(Mathf.Lerp(16f, 22f, t));
        int speedBtnSize = Mathf.RoundToInt(Mathf.Lerp(14f, 20f, t));

        _buildLabel.AddThemeFontSizeOverride("normal_font_size", buildSize);
        _waveLabel.AddThemeFontSizeOverride("font_size", waveSize);
        _livesLabel.AddThemeFontSizeOverride("font_size", livesSize);
        _enemyLabel.AddThemeFontSizeOverride("font_size", enemySize);
        _speedBtn.AddThemeFontSizeOverride("font_size", speedBtnSize);
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

    private void BuildGlobalSpectacleMeter()
    {
        _globalSpectaclePanel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -240f,
            OffsetRight = 240f,
            OffsetTop = -54f,
            OffsetBottom = -14f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.97f),
        };
        _globalSpectaclePanel.AddThemeStyleboxOverride(
            "panel",
            UITheme.MakePanel(
                bg: new Color(0.04f, 0.09f, 0.15f, 0.88f),
                border: new Color(0.62f, 0.92f, 1.00f, 0.82f),
                corners: 10,
                borderWidth: 2,
                padH: 10,
                padV: 7));
        AddChild(_globalSpectaclePanel);

        _globalSpectacleLabel = new Label
        {
            Text = "GLOBAL SPECTACLE 0/100",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = 0f,
            OffsetRight = 0f,
            OffsetTop = 0f,
            OffsetBottom = 18f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1.00f, 0.95f, 0.76f, 1f),
        };
        UITheme.ApplyFont(_globalSpectacleLabel, semiBold: true, size: 16);
        _globalSpectacleLabel.AddThemeConstantOverride("outline_size", 2);
        _globalSpectacleLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.78f));
        _globalSpectaclePanel.AddChild(_globalSpectacleLabel);

        _globalSpectacleBar = new ProgressBar
        {
            MinValue = 0f,
            MaxValue = 100f,
            Value = 0f,
            Step = 0.01f,
            ShowPercentage = false,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetRight = -10f,
            OffsetTop = -14f,
            OffsetBottom = -4f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _globalSpectacleBar.AddThemeStyleboxOverride(
            "background",
            UITheme.MakePanel(
                bg: new Color(0.07f, 0.16f, 0.25f, 0.95f),
                border: new Color(0.32f, 0.70f, 0.90f, 0.85f),
                corners: 6,
                borderWidth: 1,
                padH: 0,
                padV: 0));
        _globalSpectacleBar.AddThemeStyleboxOverride(
            "fill",
            UITheme.MakePanel(
                bg: new Color(1.00f, 0.90f, 0.44f, 0.95f),
                border: new Color(1.00f, 0.98f, 0.80f, 0.95f),
                corners: 6,
                borderWidth: 1,
                padH: 0,
                padV: 0));
        _globalSpectaclePanel.AddChild(_globalSpectacleBar);
    }
}
