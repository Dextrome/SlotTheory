using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Top bar: Wave X/20, Lives, speed toggle (1x/2x/3x/5x), ESC hint.
/// </summary>
public partial class HudPanel : CanvasLayer
{
    private const string PlayIcon = "\u25B6";
    private const string PauseIcon = "||";
    private bool _isEndlessMode = false;
    private Label _waveLabel = null!;
    private RichTextLabel _buildLabel = null!;
    private Label _livesLabel = null!;
    private Label _enemyLabel = null!;
    private Label _timeLabel = null!;
    private Label _devStatsLabel = null!;
    private Label _difficultyLabel = null!;
    private Panel _mandateBanner   = null!;
    private Label _mandateLabel    = null!;
    private Label _speedToast = null!;
    private ColorRect _speedToastStreak = null!;
    private Panel _globalSpectaclePanel = null!;
    private ColorRect[] _surgePips = System.Array.Empty<ColorRect>();
    private Label _surgeNameLabel = null!;  // shows "GLOBAL SURGE" normally, archetype name when building
    private Label _surgeMeterHint = null!;
    private Tween? _surgeMeterPulseTween;
    private PanelContainer _teachingHintPanel = null!;
    private Label _teachingHintLabel = null!;
    private Line2D _teachingHintConnector = null!;
    private ulong _surgeMeterHintStartUsec = 0;
    private float _surgeMeterHintHoldSeconds = 0f;
    private bool _surgeMeterHintTransientActive = false;
    private ulong _teachingHintStartUsec = 0;
    private float _teachingHintHoldSeconds = 0f;
    private bool _teachingHintActive = false;
    private bool _persistentSurgeHintActive = false;
    private bool _surgeMeterIntroShown = false;
    private bool _surgeMeterForcedVisible = false;
    private bool _buildLabelForcedVisible = false;
    private bool _isGlobalSurgeReady = false;
    private bool _globalSurgeInteractionEnabled = true;
    private Tween? _surgeReadyTween;
    private string _lockedSurgeName = "";

    /// <summary>Fired when the player clicks the surge bar while IsGlobalSurgeReady.</summary>
    public event Action? GlobalSurgeActivateRequested;
    private const int SurgePipCount = 20;
    private static readonly Color PipFilled = new(1.00f, 0.90f, 0.44f, 0.95f);
    private static readonly Color PipEmpty  = new(0.07f, 0.16f, 0.25f, 0.95f);
    private string _lastBuildName = "";
    private Button _speedBtn = null!;
    private Button _pausePlayBtn = null!;
    private Panel _topBarPanel = null!;
    private PanelContainer _rightControlCluster = null!;
    private Panel _waveReadoutChip = null!;
    private Panel _timeReadoutChip = null!;
    private Panel _enemyReadoutChip = null!;
    private Panel _livesReadoutChip = null!;
    private bool _lastPausedState;
    private float _hudAnimTime = 0f;
    private readonly List<Control> _animatedSurfaces = new();
    private int _speedIdx = 0;
    private static readonly double[] SpeedStepsNormal = { 1.0, 2.0, 3.0 };
    private static readonly double[] SpeedStepsDev    = { 1.0, 2.0, 3.0, 5.0, 10.0 };
    private double[] SpeedSteps => (SlotTheory.Core.SettingsManager.Instance?.DevMode == true)
        ? SpeedStepsDev : SpeedStepsNormal;
    private const float ZoomMin = 1.0f;
    private const float ZoomMax = 2.6f;
    private const float HudPunchExpandSecs = 0.07f;
    private const float HudPunchSettleSecs = 0.22f;
    private const float TopBarHeight = 50f;
    public float CurrentSpeed => (float)SpeedSteps[_speedIdx];

    public override void _Ready()
    {
        Layer = 1;
        ProcessMode = ProcessModeEnum.Always;

        var bar = new Panel();
        bar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        bar.CustomMinimumSize = new Vector2(0, TopBarHeight);
        bar.Theme = SlotTheory.Core.UITheme.Build();
        AddChild(bar);
        _topBarPanel = bar;
        ApplyTopHudBarStyle(bar);

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
            FitContent = false,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipContents = true,
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 280f : 420f, 40f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
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

        var rightAnchor = new HBoxContainer();
        rightAnchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        rightAnchor.Alignment = BoxContainer.AlignmentMode.End;
        right.AddChild(rightAnchor);

        var rightCluster = new PanelContainer();
        rightCluster.CustomMinimumSize = Vector2.Zero;
        rightCluster.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        rightCluster.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightAnchor.AddChild(rightCluster);
        _rightControlCluster = rightCluster;
        ApplyTopHudControlClusterStyle(rightCluster);

        var rightHbox = new HBoxContainer();
        rightHbox.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        rightHbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        rightHbox.Alignment = BoxContainer.AlignmentMode.End;
        rightHbox.AddThemeConstantOverride("separation", 6);
        rightCluster.AddChild(rightHbox);

        // Keep equal visual breathing room on both sides of the control cluster.
        var leftInnerPad = new Control();
        leftInnerPad.CustomMinimumSize = new Vector2(8f, 0f);
        rightHbox.AddChild(leftInnerPad);

        _pausePlayBtn = new Button();
        _pausePlayBtn.Text = PauseIcon;
        _pausePlayBtn.CustomMinimumSize = new Vector2(44, 30);
        _pausePlayBtn.AddThemeFontSizeOverride("font_size", 18);
        ApplyTopHudButtonStyle(_pausePlayBtn, UITheme.Cyan);
        _pausePlayBtn.Pressed += ToggleGameplayPause;
        rightHbox.AddChild(_pausePlayBtn);

        _speedBtn = new Button();
        _speedBtn.Text = $"1\u00D7";
        _speedBtn.CustomMinimumSize = new Vector2(56, 30);
        _speedBtn.AddThemeFontSizeOverride("font_size", 18);
        ApplyTopHudButtonStyle(_speedBtn, UITheme.Lime);
        _speedBtn.Pressed += OnSpeedToggle;
        rightHbox.AddChild(_speedBtn);

        _difficultyLabel = new Label
        {
            Text = "",
            VerticalAlignment = VerticalAlignment.Center,
        };
        _difficultyLabel.AddThemeFontSizeOverride("font_size", 15);
        UITheme.ApplyFont(_difficultyLabel, semiBold: true, size: 15);
        _difficultyLabel.Modulate = new Color(0.55f, 0.65f, 0.75f);
        rightHbox.AddChild(_difficultyLabel);

        // Mandate banner — sits below the main HUD bar, full width
        _mandateBanner = new Panel();
        _mandateBanner.LayoutMode = 1;
        _mandateBanner.AnchorLeft   = 0f;
        _mandateBanner.AnchorTop    = 0f;
        _mandateBanner.AnchorRight  = 1f;
        _mandateBanner.AnchorBottom = 0f;
        _mandateBanner.OffsetTop    = TopBarHeight;
        _mandateBanner.OffsetBottom = TopBarHeight + 22f;
        _mandateBanner.Visible = false;
        ApplyMandateBannerStyle(_mandateBanner);
        AddChild(_mandateBanner);

        _mandateLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        _mandateLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _mandateLabel.AddThemeFontSizeOverride("font_size", 13);
        UITheme.ApplyFont(_mandateLabel, semiBold: true, size: 13);
        _mandateLabel.Modulate = new Color(1.0f, 0.70f, 0.20f, 0.96f);
        _mandateBanner.AddChild(_mandateLabel);

        // Platform-specific pause controls
        if (OS.GetName() == "Android")
        {
            // Mobile menu button (Android only)
        }
        else
        {
            var menuBtn = new Button();
            menuBtn.Text = "Menu";
            menuBtn.AddThemeFontSizeOverride("font_size", 16);
            menuBtn.CustomMinimumSize = new Vector2(84, 30);
            ApplyTopHudButtonStyle(menuBtn, UITheme.Cyan);
            menuBtn.Pressed += OnMenuButtonPressed;
            rightHbox.AddChild(menuBtn);
        }

        // Mobile menu button (Android only)
        if (OS.GetName() == "Android")
        {
            var mobileMenuBtn = new Button();
            mobileMenuBtn.Text = "\u2630";
            mobileMenuBtn.CustomMinimumSize = new Vector2(50, 30);
            mobileMenuBtn.AddThemeFontSizeOverride("font_size", 20);
            ApplyTopHudButtonStyle(mobileMenuBtn, UITheme.Cyan);
            mobileMenuBtn.Pressed += OnMobileMenuPressed;
            rightHbox.AddChild(mobileMenuBtn);
        }

        // small right pad
        var pad = new Control();
        pad.CustomMinimumSize = new Vector2(8, 0);
        rightHbox.AddChild(pad);

        // Wave label - pinned exactly to screen center via full-width anchor + center alignment.
        const float waveCenterShiftX = -40f;
        _waveLabel = new Label
        {
            Text = "[ Wave 1 / 20 ]",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft   = 0f,
            AnchorRight  = 1f,
            AnchorTop    = 0f,
            AnchorBottom = 0f,
            OffsetLeft   = waveCenterShiftX,
            OffsetRight  = waveCenterShiftX,
            OffsetTop    = 0f,
            OffsetBottom = TopBarHeight,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
        };
        _waveLabel.AddThemeFontSizeOverride("font_size", 22);
        UITheme.ApplyFont(_waveLabel, semiBold: true, size: 22);
        _waveLabel.Modulate = new Color(0.78f, 0.96f, 1.00f);
        bar.AddChild(_waveLabel);

        float uiScale = MobileOptimization.GetUIScale();
        float enemyOffsetX = MobileOptimization.IsMobile() ? 120f * uiScale : 130f;
        float livesOffsetX = MobileOptimization.IsMobile() ? 240f * uiScale : 270f;
        _waveReadoutChip = CreateTopHudReadoutChip(bar, 0.5f, 0.5f, waveCenterShiftX - 132f, waveCenterShiftX + 132f, 5f, TopBarHeight - 4f, UITheme.Cyan);
        _timeReadoutChip = CreateTopHudReadoutChip(bar, 0.33f, 0.33f, -66f, 66f, 7f, TopBarHeight - 6f, UITheme.Cyan, alphaScale: 0.76f);
        _enemyReadoutChip = CreateTopHudReadoutChip(bar, 0.5f, 0.5f, enemyOffsetX - 96f, enemyOffsetX + 96f, 7f, TopBarHeight - 6f, UITheme.Cyan, alphaScale: 0.70f);
        _livesReadoutChip = CreateTopHudReadoutChip(bar, 0.5f, 0.5f, livesOffsetX - 84f, livesOffsetX + 84f, 7f, TopBarHeight - 6f, UITheme.Lime, alphaScale: 0.74f);

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
            OffsetBottom = TopBarHeight,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
            Modulate     = new Color(0.86f, 0.94f, 1f, 0.78f),
        };
        _enemyLabel.AddThemeFontSizeOverride("font_size", 18);
        UITheme.ApplyFont(_enemyLabel, semiBold: true, size: 18);
        bar.AddChild(_enemyLabel);
        _enemyLabel.Visible = false;

        _livesLabel = new Label
        {
            Text = $"Lives: {Balance.StartingLives}",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = livesOffsetX - 76f,
            OffsetRight = livesOffsetX + 76f,
            OffsetTop = 0f,
            OffsetBottom = TopBarHeight,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _livesLabel.AddThemeFontSizeOverride("font_size", 22);
        UITheme.ApplyFont(_livesLabel, semiBold: true, size: 22);
        bar.AddChild(_livesLabel);

        // Timer - centered between build-name right edge (~370px) and screen center (50%).
        // Anchored at 35% of screen width so it sits visually between the two.
        _timeLabel = new Label
        {
            Text = "0:00",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AnchorLeft   = 0.33f,
            AnchorRight  = 0.33f,
            AnchorTop    = 0f,
            AnchorBottom = 0f,
            OffsetLeft   = -60f,
            OffsetRight  =  60f,
            OffsetTop    = 0f,
            OffsetBottom = TopBarHeight,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
            Modulate     = new Color(0.94f, 0.98f, 1f, 0.58f),
        };
        _timeLabel.AddThemeFontSizeOverride("font_size", 18);
        UITheme.ApplyFont(_timeLabel, semiBold: true, size: 18);
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

        // #8: 2px cyan accent line at the bottom edge of the HUD bar
        var bottomAccent = new ColorRect
        {
            Color = new Color(UITheme.Cyan.R, UITheme.Cyan.G, UITheme.Cyan.B, 0.55f),
            AnchorLeft   = 0f, AnchorRight  = 1f,
            AnchorTop    = 1f, AnchorBottom = 1f,
            OffsetTop    = -2f, OffsetBottom = 0f,
            MouseFilter  = Control.MouseFilterEnum.Ignore,
            ZIndex       = 1,
        };
        bar.AddChild(bottomAccent);

        BuildGlobalSurgeMeter();
        ResetSpeed();
        _lastPausedState = GetTree().Paused;
        UpdatePausePlayButtonLabel();
    }

    public override void _Process(double delta)
    {
        _hudAnimTime += (float)delta;
        LayoutTopReadouts();
        for (int i = _animatedSurfaces.Count - 1; i >= 0; i--)
        {
            var surface = _animatedSurfaces[i];
            if (!GodotObject.IsInstanceValid(surface) || !surface.IsInsideTree())
            {
                _animatedSurfaces.RemoveAt(i);
                continue;
            }
            surface.QueueRedraw();
        }

        UpdateSurgeMeterHintPresentation();
        UpdateTeachingHintPresentation();

        bool isPaused = GetTree().Paused;
        if (isPaused == _lastPausedState)
            return;

        _lastPausedState = isPaused;
        UpdatePausePlayButtonLabel();
    }

    private void OnSpeedToggle()
    {
        var steps = SpeedSteps;
        // Clamp index in case dev mode was just disabled while at a high speed
        _speedIdx = Mathf.Min(_speedIdx, steps.Length - 1);
        _speedIdx = (_speedIdx + 1) % steps.Length;
        Engine.TimeScale = steps[_speedIdx];
        RefreshSpeedLabelFromActual((float)Engine.TimeScale);
        ShowSpeedToast();
        MusicDirector.Instance?.SetGameSpeedScale((float)steps[_speedIdx]);
        SoundManager.Instance?.SetSpeedFeel((float)steps[_speedIdx]);
        SoundManager.Instance?.Play("ui_speed_shift");
    }

    public void ResetSpeed()
    {
        _isEndlessMode = false;
        _speedIdx = 0;
        Engine.TimeScale = 1.0;
        RefreshSpeedLabelFromActual((float)Engine.TimeScale);
        MusicDirector.Instance?.SetGameSpeedScale(1.0f);
        SoundManager.Instance?.SetSpeedFeel(1.0f);
    }

    public void RefreshSpeedLabelFromActual(float timeScale)
    {
        if (!GodotObject.IsInstanceValid(_speedBtn))
            return;
        _speedBtn.Text = $"{FormatSpeedMultiplier(timeScale)}\u00D7";
    }

    public void FlashLives()
    {
        SoundManager.Instance?.Play("life_lost");
        _livesLabel.PivotOffset = _livesLabel.Size / 2f;
        var tween = CreateTween();
        tween.TweenProperty(_livesLabel, "scale", new Vector2(1.22f, 1.22f), HudPunchExpandSecs);
        tween.TweenProperty(_livesLabel, "scale", Vector2.One, HudPunchSettleSecs)
             .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private int _totalWaves = Balance.TotalWaves;
    public void SetTotalWaves(int n) => _totalWaves = n;
    public void SetEndlessMode(bool endless) => _isEndlessMode = endless;

    public void SetDifficulty(Core.DifficultyMode difficulty)
    {
        if (!GodotObject.IsInstanceValid(_difficultyLabel)) return;
        (_difficultyLabel.Text, _difficultyLabel.Modulate) = difficulty switch
        {
            Core.DifficultyMode.Easy   => ("EASY",   new Color(0.40f, 0.90f, 0.45f)),
            Core.DifficultyMode.Normal => ("NORMAL", new Color(1.00f, 0.82f, 0.30f)),
            Core.DifficultyMode.Hard   => ("HARD",   new Color(1.00f, 0.35f, 0.30f)),
            _                          => ("",       Colors.White),
        };
    }

    public void SetMandateStrip(string mandateText)
    {
        if (!GodotObject.IsInstanceValid(_mandateLabel)) return;
        _mandateLabel.Text = mandateText;
        bool show = !string.IsNullOrEmpty(mandateText);
        _mandateLabel.Visible = show;
        if (GodotObject.IsInstanceValid(_mandateBanner)) _mandateBanner.Visible = show;
    }

    public void Refresh(int wave, int lives)
    {
        _waveLabel.Text = _isEndlessMode
            ? $"[ Wave {wave}  \u221e ]"
            : $"[ Wave {wave} / {_totalWaves} ]";
        _livesLabel.Text = $"Lives: {lives}";
        _livesLabel.Modulate = lives <= 3 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
        _livesLabel.PivotOffset = _livesLabel.Size / 2f;
        LayoutTopReadouts();
    }

    public void RefreshTime(float totalSeconds)
    {
        _timeLabel.Text = FormatTime(totalSeconds);
        LayoutTopReadouts();
    }

    private static string FormatTime(float seconds)
    {
        int s = (int)seconds;
        return $"{s / 60}:{s % 60:D2}";
    }

    private static string FormatSpeedMultiplier(float speed)
    {
        float clamped = Mathf.Max(0f, speed);
        float roundedInt = Mathf.Round(clamped);
        if (Mathf.Abs(clamped - roundedInt) < 0.05f)
            return $"{(int)roundedInt}";
        if (clamped >= 1f)
            return $"{clamped:0.##}";
        return $"{clamped:0.###}";
    }

    public void PulseWaveLabel()
    {
        _waveLabel.PivotOffset = _waveLabel.Size / 2f;
        var tw = _waveLabel.CreateTween();
        tw.TweenProperty(_waveLabel, "scale", new Vector2(1.18f, 1.18f), HudPunchExpandSecs)
          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_waveLabel, "scale", Vector2.One, HudPunchSettleSecs)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    public void SetBuildName(string buildName, bool visible = true, Color? startColor = null, Color? endColor = null)
    {
        if (!GodotObject.IsInstanceValid(_buildLabel)) return;
        bool nameChanged = buildName.Length > 0 && buildName != _lastBuildName;
        _lastBuildName = buildName;
        _buildLabel.Clear();
        if (buildName.Length > 0)
        {
            var c0 = startColor ?? new Color(0.74f, 0.88f, 1.00f);
            var c1 = endColor ?? c0;
            _buildLabel.AppendText(BuildGradientBbCode(buildName, c0, c1));
        }
        _buildLabel.Visible = visible && buildName.Length > 0;
        if (nameChanged && visible)
        {
            _buildLabel.PivotOffset = _buildLabel.Size / 2f;
            var tw = _buildLabel.CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(_buildLabel, "scale", new Vector2(1.18f, 1.18f), 0.10f)
              .From(new Vector2(0.82f, 0.82f))
              .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenProperty(_buildLabel, "modulate:a", 0.96f, 0.12f)
              .From(0f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            tw.Chain();
            tw.TweenProperty(_buildLabel, "scale", Vector2.One, 0.16f)
              .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
        LayoutTopReadouts();
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
        _enemyLabel.Visible = alive > 0;
        if (GodotObject.IsInstanceValid(_enemyReadoutChip))
            _enemyReadoutChip.Visible = alive > 0;
        LayoutTopReadouts();
    }

    /// <summary>
    /// Computes the surge meter's viewport-space rect from its anchor/offset constants.
    /// Safe to call even when the panel is invisible (no layout pass required).
    /// </summary>
    public Rect2 GetSurgeMeterViewportRect()
    {
        // Mirrors _globalSpectaclePanel anchor settings:
        //   AnchorLeft = AnchorRight = 0.5  →  centered horizontally
        //   OffsetLeft = -211, OffsetRight = 211  →  422 px wide
        //   AnchorTop = AnchorBottom = 1.0  →  bottom of viewport
        //   OffsetTop = -36, OffsetBottom = -14  →  22 px tall
        var vp = GetViewport().GetVisibleRect().Size;
        return new Rect2(vp.X * 0.5f - 211f, vp.Y - 36f, 422f, 22f);
    }

    public void ShowSurgeMicroHint(string text, float holdSeconds = 1.7f)
    {
        if (!GodotObject.IsInstanceValid(_surgeMeterHint))
            return;
        if (_persistentSurgeHintActive)
            return;

        _surgeMeterHintTransientActive = true;
        _surgeMeterHintStartUsec = Time.GetTicksUsec();
        _surgeMeterHintHoldSeconds = SurgeUxTiming.ResolveSurgeMeterHintHold(holdSeconds);
        _surgeMeterHint.Text = text;
        _surgeMeterHint.Visible = true;
        _surgeMeterHint.Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void ShowTeachingHint(string text, float holdSeconds = 3.4f)
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        ShowTeachingHintAtScreen(text, new Vector2(vp.X * 0.5f, 170f), holdSeconds);
    }

    public void ShowTeachingHintAtScreen(string text, Vector2 screenPos, float holdSeconds = 3.4f)
    {
        if (!GodotObject.IsInstanceValid(_teachingHintPanel) || !GodotObject.IsInstanceValid(_teachingHintLabel))
            return;

        var panelSize = ResolveTeachingHintPanelSize(text);
        float panelW = panelSize.X;
        float panelH = panelSize.Y;
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float preferredBottomY = screenPos.Y + 34f;
        bool placeLeft = preferredBottomY > vp.Y - panelH - 46f;
        float x;
        float y;
        if (placeLeft)
        {
            x = Mathf.Clamp(screenPos.X - panelW - 28f, 12f, vp.X - panelW - 12f);
            y = Mathf.Clamp(screenPos.Y - panelH * 0.5f, 56f, vp.Y - panelH - 20f);
        }
        else
        {
            x = Mathf.Clamp(screenPos.X - panelW * 0.5f, 12f, vp.X - panelW - 12f);
            y = Mathf.Clamp(preferredBottomY, 56f, vp.Y - panelH - 46f);
        }

        _teachingHintPanel.AnchorLeft = 0f;
        _teachingHintPanel.AnchorRight = 0f;
        _teachingHintPanel.AnchorTop = 0f;
        _teachingHintPanel.AnchorBottom = 0f;
        _teachingHintPanel.OffsetLeft = x;
        _teachingHintPanel.OffsetRight = x + panelW;
        _teachingHintPanel.OffsetTop = y;
        _teachingHintPanel.OffsetBottom = y + panelH;

        if (GodotObject.IsInstanceValid(_teachingHintConnector))
        {
            Vector2 from = placeLeft
                ? new Vector2(x + panelW, y + panelH * 0.5f)
                : new Vector2(x + panelW * 0.5f, y);
            _teachingHintConnector.Points = new[] { from, screenPos };
            _teachingHintConnector.Visible = true;
            _teachingHintConnector.Modulate = new Color(1f, 1f, 1f, 0f);
        }

        _teachingHintActive = true;
        _teachingHintStartUsec = Time.GetTicksUsec();
        _teachingHintHoldSeconds = SurgeUxTiming.ResolveWorldTeachingHintHold(holdSeconds);
        _teachingHintLabel.CustomMinimumSize = new Vector2(Mathf.Max(120f, panelW - 24f), 0f);
        _teachingHintLabel.Text = text;
        _teachingHintPanel.Visible = true;
        _teachingHintPanel.Modulate = new Color(1f, 1f, 1f, 0f);
    }

    public void SetPersistentSurgeHint(string? text)
    {
        if (!GodotObject.IsInstanceValid(_surgeMeterHint))
            return;

        _surgeMeterHintTransientActive = false;
        _surgeMeterHintStartUsec = 0;
        _surgeMeterHintHoldSeconds = 0f;

        if (string.IsNullOrWhiteSpace(text))
        {
            _persistentSurgeHintActive = false;
            _surgeMeterHint.Visible = false;
            return;
        }

        _persistentSurgeHintActive = true;
        _surgeMeterHint.Text = text;
        _surgeMeterHint.Visible = true;
        _surgeMeterHint.Modulate = new Color(1f, 1f, 1f, 0.95f);
    }

    private void UpdateSurgeMeterHintPresentation()
    {
        if (!GodotObject.IsInstanceValid(_surgeMeterHint))
            return;
        if (_persistentSurgeHintActive)
            return;
        if (!_surgeMeterHintTransientActive || _surgeMeterHintStartUsec == 0)
            return;

        const float fadeInSeconds = 0.14f;
        const float peakAlpha = 0.92f;
        float fadeOutSeconds = SurgeUxTiming.SurgeMeterHintFadeOutSeconds;
        float holdSeconds = Mathf.Max(0.2f, _surgeMeterHintHoldSeconds);
        float elapsed = (Time.GetTicksUsec() - _surgeMeterHintStartUsec) / 1_000_000f;
        float total = fadeInSeconds + holdSeconds + fadeOutSeconds;
        if (elapsed >= total)
        {
            _surgeMeterHintTransientActive = false;
            _surgeMeterHint.Visible = false;
            return;
        }

        float alpha;
        if (elapsed < fadeInSeconds)
        {
            float t = Mathf.Clamp(elapsed / fadeInSeconds, 0f, 1f);
            alpha = peakAlpha * t;
        }
        else if (elapsed < fadeInSeconds + holdSeconds)
        {
            alpha = peakAlpha;
        }
        else
        {
            float t = Mathf.Clamp((elapsed - fadeInSeconds - holdSeconds) / fadeOutSeconds, 0f, 1f);
            alpha = peakAlpha * (1f - t * t * t);
        }

        _surgeMeterHint.Visible = true;
        _surgeMeterHint.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private void UpdateTeachingHintPresentation()
    {
        if (!_teachingHintActive || _teachingHintStartUsec == 0)
            return;
        if (!GodotObject.IsInstanceValid(_teachingHintPanel))
            return;

        const float fadeInSeconds = 0.14f;
        const float panelPeakAlpha = 0.98f;
        const float connectorPeakAlpha = 0.85f;
        float fadeOutSeconds = SurgeUxTiming.WorldTeachingHintFadeOutSeconds;
        float holdSeconds = Mathf.Max(0.2f, _teachingHintHoldSeconds);
        float elapsed = (Time.GetTicksUsec() - _teachingHintStartUsec) / 1_000_000f;
        float total = fadeInSeconds + holdSeconds + fadeOutSeconds;
        if (elapsed >= total)
        {
            _teachingHintActive = false;
            _teachingHintPanel.Visible = false;
            if (GodotObject.IsInstanceValid(_teachingHintConnector))
                _teachingHintConnector.Visible = false;
            return;
        }

        float alpha;
        if (elapsed < fadeInSeconds)
        {
            float t = Mathf.Clamp(elapsed / fadeInSeconds, 0f, 1f);
            alpha = t;
        }
        else if (elapsed < fadeInSeconds + holdSeconds)
        {
            alpha = 1f;
        }
        else
        {
            float t = Mathf.Clamp((elapsed - fadeInSeconds - holdSeconds) / fadeOutSeconds, 0f, 1f);
            alpha = 1f - t * t * t;
        }

        _teachingHintPanel.Visible = true;
        _teachingHintPanel.Modulate = new Color(1f, 1f, 1f, panelPeakAlpha * alpha);
        if (GodotObject.IsInstanceValid(_teachingHintConnector))
        {
            _teachingHintConnector.Visible = true;
            _teachingHintConnector.Modulate = new Color(1f, 1f, 1f, connectorPeakAlpha * alpha);
        }
    }

    private Vector2 ResolveTeachingHintPanelSize(string text)
    {
        const float minPanelW = 250f;
        const float maxPanelW = 680f;
        const float minPanelH = 38f;
        const float maxPanelH = 150f;
        const float contentPadX = 24f;
        const float contentPadY = 14f;

        Font font = _teachingHintLabel.GetThemeFont("font") ?? UITheme.SemiBold;
        int fontSize = _teachingHintLabel.GetThemeFontSize("font_size");
        if (fontSize <= 0)
            fontSize = 17;

        string safeText = string.IsNullOrWhiteSpace(text) ? " " : text.Trim();
        float rawTextW = font.GetStringSize(safeText, HorizontalAlignment.Left, -1, fontSize).X;
        float panelW = Mathf.Clamp(rawTextW + contentPadX, minPanelW, maxPanelW);
        float maxTextW = Mathf.Max(90f, panelW - contentPadX);
        int lineCount = EstimateWrappedLineCount(font, fontSize, safeText, maxTextW);
        float lineHeight = font.GetAscent(fontSize) + font.GetDescent(fontSize) + 2f;
        float panelH = Mathf.Clamp(contentPadY + lineHeight * lineCount, minPanelH, maxPanelH);

        return new Vector2(panelW, panelH);
    }

    private static int EstimateWrappedLineCount(Font font, int fontSize, string text, float maxLineWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 1;

        int lines = 0;
        string[] hardLines = text.Split('\n');
        for (int i = 0; i < hardLines.Length; i++)
        {
            string segment = hardLines[i].Trim();
            if (segment.Length == 0)
            {
                lines += 1;
                continue;
            }

            lines += EstimateWrappedLineCountForSegment(font, fontSize, segment, maxLineWidth);
        }
        return Mathf.Max(1, lines);
    }

    private static int EstimateWrappedLineCountForSegment(Font font, int fontSize, string segment, float maxLineWidth)
    {
        if (font.GetStringSize(segment, HorizontalAlignment.Left, -1, fontSize).X <= maxLineWidth)
            return 1;

        int lines = 1;
        string[] words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string current = "";
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            string candidate = current.Length == 0 ? word : $"{current} {word}";
            float candidateWidth = font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X;
            if (candidateWidth <= maxLineWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
                lines++;

            float wordWidth = font.GetStringSize(word, HorizontalAlignment.Left, -1, fontSize).X;
            if (wordWidth > maxLineWidth)
            {
                int wrappedWordLines = Mathf.Max(1, Mathf.CeilToInt(wordWidth / Mathf.Max(1f, maxLineWidth)));
                lines += wrappedWordLines - 1;
                current = "";
            }
            else
            {
                current = word;
            }
        }

        return Mathf.Max(1, lines);
    }

    public void PulseGlobalSurgeMeter(float strength = 1f)
    {
        if (!GodotObject.IsInstanceValid(_globalSpectaclePanel) || _isGlobalSurgeReady)
            return;

        if (_surgeMeterPulseTween != null && GodotObject.IsInstanceValid(_surgeMeterPulseTween))
            _surgeMeterPulseTween.Kill();

        float pulseStrength = Mathf.Clamp(strength, 0.25f, 1.4f);
        float peak = 1f + 0.24f * pulseStrength;
        _surgeMeterPulseTween = CreateTween();
        _surgeMeterPulseTween.SetIgnoreTimeScale(true);
        _surgeMeterPulseTween.TweenProperty(_globalSpectaclePanel, "modulate:v", peak, 0.12f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _surgeMeterPulseTween.TweenProperty(_globalSpectaclePanel, "modulate:v", 1f, 0.25f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _surgeMeterPulseTween.TweenCallback(Callable.From(() => _surgeMeterPulseTween = null));
    }

    public void RefreshGlobalSurgeMeter(float meter, float threshold, bool visible,
        string archetypePreview = "", float previewAlpha = 0f)
    {
        if (!GodotObject.IsInstanceValid(_globalSpectaclePanel)
            || !GodotObject.IsInstanceValid(_surgeNameLabel))
            return;

        bool wasHidden = !_globalSpectaclePanel.Visible;
        bool canShow = (visible && threshold > 0.001f) || _surgeMeterForcedVisible;
        _globalSpectaclePanel.Visible = canShow;
        if (!canShow)
            return;

        if (wasHidden && !_surgeMeterIntroShown)
        {
            _surgeMeterIntroShown = true;
            ShowSurgeMicroHint("Tower surges fill this bar", holdSeconds: 2.6f);
        }

        float fill = Mathf.Clamp(meter / Mathf.Max(1f, threshold), 0f, 1f);
        float pipsToFill = fill * SurgePipCount;
        float nearReady = Mathf.InverseLerp(0.72f, 1f, fill);
        Color pipReadyColor = PipFilled.Lerp(new Color(1.00f, 0.97f, 0.76f, 0.98f), nearReady);
        bool pulseTweenActive = _surgeMeterPulseTween != null && GodotObject.IsInstanceValid(_surgeMeterPulseTween);
        if (!_isGlobalSurgeReady && !pulseTweenActive)
            _globalSpectaclePanel.Modulate = new Color(1.00f + nearReady * 0.06f, 1.00f + nearReady * 0.05f, 1.00f, 0.97f);

        for (int i = 0; i < _surgePips.Length; i++)
        {
            if (!GodotObject.IsInstanceValid(_surgePips[i])) continue;
            float pipFill = Mathf.Clamp(pipsToFill - i, 0f, 1f);
            Color color = PipEmpty.Lerp(pipReadyColor, pipFill);
            if (nearReady > 0.001f)
            {
                float shimmer = (0.5f + 0.5f * Mathf.Sin((float)Time.GetTicksMsec() * 0.010f + i * 0.32f)) * nearReady * 0.12f;
                color = new Color(
                    Mathf.Clamp(color.R + shimmer, 0f, 1f),
                    Mathf.Clamp(color.G + shimmer, 0f, 1f),
                    Mathf.Clamp(color.B + shimmer * 0.7f, 0f, 1f),
                    color.A);
            }
            _surgePips[i].Color = color;
        }

        if (!string.IsNullOrEmpty(_lockedSurgeName))
        {
            // Surge is ready and waiting for activation - hold the archetype name.
            _surgeNameLabel.Text = _lockedSurgeName;
            _surgeNameLabel.Modulate = new Color(1.00f, 0.92f, 0.60f, 1f);
        }
        else
        {
            bool hasPreview = !string.IsNullOrEmpty(archetypePreview) && previewAlpha > 0.01f;
            if (hasPreview)
            {
                _surgeNameLabel.Text = archetypePreview;
                _surgeNameLabel.Modulate = new Color(1.00f, 0.92f, 0.60f, 0.60f + previewAlpha * 0.40f);
            }
            else
            {
                _surgeNameLabel.Text = "GLOBAL SURGE";
                _surgeNameLabel.Modulate = new Color(1.00f, 0.95f, 0.76f, 1f);
            }
        }
    }

    /// <summary>
    /// Called by the surge tutorial panel to show the meter above the draft overlay.
    /// Temporarily raises HudPanel to Layer=7 (above DraftPanel Layer=6) so the surge
    /// meter renders on top; PauseScreen is also Layer=7 but appears later in the scene
    /// tree so it still renders above HudPanel when the game is paused.
    /// </summary>
    public void SetSurgeMeterForcedVisible(bool forced)
    {
        _surgeMeterForcedVisible = forced;
        Layer = (_surgeMeterForcedVisible || _buildLabelForcedVisible) ? 7 : 1;
        if (!forced && GodotObject.IsInstanceValid(_globalSpectaclePanel))
            _globalSpectaclePanel.Visible = false; // let the next RefreshGlobalSurgeMeter decide
    }

    /// <summary>
    /// Switches the global surge bar into "ready to activate" mode: makes it clickable,
    /// shows a pulsing glow, and changes the label to prompt the player.
    /// Pass false to return to normal display after the surge fires.
    /// </summary>
    public void SetGlobalSurgeReady(bool ready, string surgeName = "")
    {
        _isGlobalSurgeReady = ready;
        _lockedSurgeName = ready ? surgeName : "";

        if (!GodotObject.IsInstanceValid(_globalSpectaclePanel)) return;

        // Kill any running pulse tween
        if (_surgeReadyTween != null && GodotObject.IsInstanceValid(_surgeReadyTween))
            _surgeReadyTween.Kill();
        _surgeReadyTween = null;
        if (_surgeMeterPulseTween != null && GodotObject.IsInstanceValid(_surgeMeterPulseTween))
            _surgeMeterPulseTween.Kill();
        _surgeMeterPulseTween = null;

        if (ready)
        {
            // Label is now driven by _lockedSurgeName in RefreshGlobalSurgeMeter; nothing to set here.
            // Override border to bright gold to make it obviously interactive
            _globalSpectaclePanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
                bg:     new Color(0.10f, 0.12f, 0.06f, 0.92f),
                border: new Color(1.00f, 0.88f, 0.20f, 1.00f),
                corners: 8, borderWidth: 3, padH: 8, padV: 4));
            // Pulse brightness 1→1.4→1
            _surgeReadyTween = CreateTween();
            _surgeReadyTween.SetLoops();
            _surgeReadyTween.TweenProperty(_globalSpectaclePanel, "modulate:v", 1.38f, 0.45f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            _surgeReadyTween.TweenProperty(_globalSpectaclePanel, "modulate:v", 1.00f, 0.45f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        }
        else
        {
            // Restore default style
            _globalSpectaclePanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
                bg:     new Color(0.04f, 0.09f, 0.15f, 0.88f),
                border: new Color(0.62f, 0.92f, 1.00f, 0.82f),
                corners: 8, borderWidth: 2, padH: 8, padV: 4));
            _globalSpectaclePanel.Modulate = new Color(1f, 1f, 1f, 0.97f);
            // Label text/colour will be reset to "GLOBAL SURGE" on the next RefreshGlobalSurgeMeter tick.
        }

        RefreshGlobalSurgeInteractionState();
    }

    public void SetGlobalSurgeInteractionEnabled(bool enabled)
    {
        _globalSurgeInteractionEnabled = enabled;
        RefreshGlobalSurgeInteractionState();
    }

    private void RefreshGlobalSurgeInteractionState()
    {
        if (!GodotObject.IsInstanceValid(_globalSpectaclePanel))
            return;
        bool clickable = _isGlobalSurgeReady && _globalSurgeInteractionEnabled;
        _globalSpectaclePanel.MouseFilter = clickable
            ? Control.MouseFilterEnum.Stop
            : Control.MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// Returns the viewport-space rect of the build name label.
    /// Uses the actual laid-out rect when available, falls back to layout constants.
    /// </summary>
    public Rect2 GetBuildLabelViewportRect()
    {
        if (GodotObject.IsInstanceValid(_buildLabel) && _buildLabel.Visible)
        {
            var rect = _buildLabel.GetGlobalRect();
            if (rect.Size.X > 20f)
                return rect;
        }
        // Fallback: leftPad=18px, bar height=44px, estimated content width
        return new Rect2(18f, 2f, 220f, 40f);
    }

    /// <summary>
    /// Raises HudPanel above DraftPanel (Layer=7) so the build name label renders
    /// above the tutorial blocker overlay, without forcing the surge meter visible.
    /// </summary>
    public void SetBuildLabelForcedVisible(bool forced)
    {
        _buildLabelForcedVisible = forced;
        Layer = (_surgeMeterForcedVisible || _buildLabelForcedVisible) ? 7 : 1;
    }

    public void RefreshDevRenderStats(bool enabled, int enemiesAlive, string perfSummary)
    {
        _devStatsLabel.Visible = false;
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
        int pauseBtnSize = Mathf.RoundToInt(Mathf.Lerp(18f, 26f, t));

        _buildLabel.AddThemeFontSizeOverride("normal_font_size", buildSize);
        _waveLabel.AddThemeFontSizeOverride("font_size", waveSize);
        _livesLabel.AddThemeFontSizeOverride("font_size", livesSize);
        _enemyLabel.AddThemeFontSizeOverride("font_size", enemySize);
        _speedBtn.AddThemeFontSizeOverride("font_size", speedBtnSize);
        _pausePlayBtn.AddThemeFontSizeOverride("font_size", pauseBtnSize);
        LayoutTopReadouts();
    }

    private void LayoutTopReadouts()
    {
        if (!GodotObject.IsInstanceValid(_topBarPanel)
            || !GodotObject.IsInstanceValid(_waveLabel)
            || !GodotObject.IsInstanceValid(_livesLabel)
            || !GodotObject.IsInstanceValid(_timeLabel)
            || !GodotObject.IsInstanceValid(_enemyLabel))
            return;

        float barW = _topBarPanel.Size.X;
        if (barW < 220f)
            return;

        float barLeft = _topBarPanel.GetGlobalRect().Position.X;
        float clusterLeft = barW - 10f;
        if (GodotObject.IsInstanceValid(_rightControlCluster))
        {
            float candidate = _rightControlCluster.GetGlobalRect().Position.X - barLeft;
            if (candidate > 40f)
                clusterLeft = candidate;
            else
                clusterLeft = barW - Mathf.Max(120f, _rightControlCluster.Size.X) - 8f;
        }

        float buildRight = 16f;
        if (GodotObject.IsInstanceValid(_buildLabel) && _buildLabel.Visible)
        {
            var buildRect = _buildLabel.GetGlobalRect();
            if (buildRect.Size.X > 1f)
                buildRight = Mathf.Max(buildRight, buildRect.End.X - barLeft + 12f);
        }
        buildRight = Mathf.Min(buildRight, barW * 0.40f);

        // Keep a safety gutter between readouts and right controls.
        // Anchor against the first interactive control (pause) so we can safely use the
        // empty decorative area inside the cluster without overlapping buttons.
        float controlGutter = MobileOptimization.IsMobile() ? 8f : 12f;
        float rightAnchor = clusterLeft - controlGutter;
        if (GodotObject.IsInstanceValid(_pausePlayBtn))
        {
            float pauseLeft = _pausePlayBtn.GetGlobalRect().Position.X - barLeft;
            if (pauseLeft > 40f)
            {
                float pauseGutter = MobileOptimization.IsMobile() ? 8f : 10f;
                rightAnchor = pauseLeft - pauseGutter;
            }
        }
        float rightEdge = Mathf.Min(rightAnchor, barW - 10f);
        float leftEdge = Mathf.Max(buildRight, 10f);
        rightEdge = Mathf.Clamp(rightEdge, 20f, barW - 10f);
        leftEdge = Mathf.Clamp(leftEdge, 10f, barW - 20f);
        if (rightEdge - leftEdge < 180f)
            return;

        float waveW = Mathf.Clamp(_waveLabel.GetCombinedMinimumSize().X + 36f, 200f, 350f);
        float fixedTimeW = MobileOptimization.IsMobile() ? 80f : 84f;
        float timeW = fixedTimeW;
        bool enemyHasText = !string.IsNullOrWhiteSpace(_enemyLabel.Text);
        float fixedEnemyW = MobileOptimization.IsMobile() ? 152f : 160f;
        float enemyW = enemyHasText ? fixedEnemyW : 0f;
        float livesW = Mathf.Clamp(_livesLabel.GetCombinedMinimumSize().X + 30f, 128f, 220f);

        const float gap = 6f;
        const float livesMinW = 96f;
        const float enemyMinW = 156f;
        const float timeMinW = 62f;
        const float waveMinW = 156f;

        // Wave is always hard-centered in the viewport.
        float waveCenter = barW * 0.5f;

        // Shrink wave (if needed) so both sides can still fit key chips.
        float minRightNeed = enemyHasText
            ? (gap * 2f + enemyMinW + timeMinW)
            : (gap + timeMinW);
        float maxWaveFromLeft = (waveCenter - leftEdge - gap - livesMinW) * 2f;
        float maxWaveFromRight = (rightEdge - waveCenter - minRightNeed) * 2f;
        float maxWaveW = Mathf.Min(maxWaveFromLeft, maxWaveFromRight);
        if (maxWaveW < waveMinW)
            waveW = Mathf.Clamp(maxWaveW, 96f, waveW);
        else
            waveW = Mathf.Min(waveW, maxWaveW);

        float waveLeft = waveCenter - waveW * 0.5f;
        float waveRight = waveCenter + waveW * 0.5f;

        // Left lane: Lives
        float leftSpace = waveLeft - gap - leftEdge;
        leftSpace = Mathf.Max(0f, leftSpace);
        livesW = Mathf.Min(livesW, leftSpace);
        livesW = Mathf.Max(livesMinW, livesW);
        if (livesW > leftSpace)
            livesW = leftSpace;
        float livesRight = waveLeft - gap;
        float livesCenter = livesRight - livesW * 0.5f;

        // Right lanes: Enemies then Time
        float rightSpace = rightEdge - waveRight;
        float enemyCenter = 0f;
        float timeCenter;

        if (enemyHasText)
        {
            float availBoxes = Mathf.Max(40f, rightSpace - gap * 2f);
            float boxTotal = enemyW + timeW;
            float minTotal = enemyMinW + timeMinW;
            if (boxTotal > availBoxes)
            {
                float overflow = boxTotal - availBoxes;
                // Keep enemy width stable first; let time absorb squeeze.
                float timeTake = Mathf.Min(overflow, Mathf.Max(0f, timeW - timeMinW));
                timeW -= timeTake;
                overflow -= timeTake;
                if (overflow > 0f)
                {
                    float enemyTake = Mathf.Min(overflow, Mathf.Max(0f, enemyW - enemyMinW));
                    enemyW -= enemyTake;
                }
            }

            if (enemyW + timeW > availBoxes)
            {
                float scale = availBoxes / Mathf.Max(1f, enemyW + timeW);
                enemyW = Mathf.Max(60f, enemyW * scale);
                timeW = Mathf.Max(52f, timeW * scale);
            }
            else if (enemyW + timeW < minTotal && availBoxes >= minTotal)
            {
                enemyW = Mathf.Max(enemyW, enemyMinW);
                timeW = Mathf.Max(timeW, timeMinW);
            }

            float enemyLeft = waveRight + gap;
            float enemyRight = enemyLeft + enemyW;
            enemyCenter = enemyLeft + enemyW * 0.5f;
            float timeLeft = enemyRight + gap;
            timeCenter = timeLeft + timeW * 0.5f;
        }
        else
        {
            float availTime = Mathf.Max(52f, rightSpace - gap);
            timeW = Mathf.Min(timeW, availTime);
            timeW = Mathf.Max(52f, timeW);
            float timeLeft = waveRight + gap;
            timeCenter = timeLeft + timeW * 0.5f;
        }

        ApplyTopReadoutRect(_waveLabel, waveCenter, waveW);
        ApplyTopReadoutRect(_livesLabel, livesCenter, livesW);

        _timeLabel.Visible = true;
        ApplyTopReadoutRect(_timeLabel, timeCenter, timeW);

        _enemyLabel.Visible = enemyHasText;
        if (enemyHasText)
            ApplyTopReadoutRect(_enemyLabel, enemyCenter, enemyW);

        if (GodotObject.IsInstanceValid(_waveReadoutChip))
            ApplyTopReadoutRect(_waveReadoutChip, waveCenter, waveW);
        if (GodotObject.IsInstanceValid(_livesReadoutChip))
            ApplyTopReadoutRect(_livesReadoutChip, livesCenter, livesW);

        if (GodotObject.IsInstanceValid(_timeReadoutChip))
        {
            _timeReadoutChip.Visible = true;
            ApplyTopReadoutRect(_timeReadoutChip, timeCenter, timeW);
        }

        if (GodotObject.IsInstanceValid(_enemyReadoutChip))
        {
            _enemyReadoutChip.Visible = enemyHasText;
            if (enemyHasText)
                ApplyTopReadoutRect(_enemyReadoutChip, enemyCenter, enemyW);
        }
    }

    private static void ApplyTopReadoutRect(Control control, float centerX, float width)
    {
        float clampedW = Mathf.Max(40f, width);
        control.AnchorLeft = 0f;
        control.AnchorRight = 0f;
        control.AnchorTop = 0f;
        control.AnchorBottom = 0f;
        control.OffsetLeft = centerX - clampedW * 0.5f;
        control.OffsetRight = centerX + clampedW * 0.5f;
        control.OffsetTop = 6f;
        control.OffsetBottom = TopBarHeight - 4f;
    }

    private void ApplyTopHudBarStyle(Panel bar)
    {
        var barStyle = UITheme.MakePanel(
            bg: new Color(0.012f, 0.020f, 0.060f, 0.96f),
            border: new Color(0.24f, 0.56f, 0.68f, 0.78f),
            corners: 0,
            borderWidth: 1,
            padH: 0,
            padV: 0);
        barStyle.ShadowColor = new Color(0.16f, 0.46f, 0.62f, 0.14f);
        barStyle.ShadowSize = 6;
        barStyle.ShadowOffset = new Vector2(0f, 1f);
        bar.AddThemeStyleboxOverride("panel", barStyle);

        bar.Draw += () =>
        {
            float w = bar.Size.X;
            float h = bar.Size.Y;
            if (w < 20f || h < 8f)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_hudAnimTime * 0.62f);
            bar.DrawRect(new Rect2(0f, 0f, w, h * 0.54f), new Color(0.12f, 0.30f, 0.40f, 0.14f));
            bar.DrawRect(new Rect2(0f, h * 0.54f, w, h * 0.46f), new Color(0f, 0f, 0f, 0.18f));
            bar.DrawRect(new Rect2(3f, 3f, w - 6f, h - 6f), new Color(0.82f, 0.96f, 1.00f, 0.07f), false, 1f);

            bar.DrawLine(new Vector2(0f, 1f), new Vector2(w, 1f), new Color(0.90f, 0.98f, 1.00f, 0.25f), 1f);
            bar.DrawLine(new Vector2(0f, 2f), new Vector2(w, 2f), new Color(UITheme.Cyan.R, UITheme.Cyan.G, UITheme.Cyan.B, 0.17f), 1f);
            bar.DrawLine(new Vector2(0f, h - 2f), new Vector2(w, h - 2f),
                new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.21f + pulse * 0.06f), 1f);
            bar.DrawLine(new Vector2(0f, h - 1f), new Vector2(w, h - 1f), new Color(0f, 0f, 0f, 0.44f), 1f);

            float capW = Mathf.Clamp(w * 0.08f, 48f, 108f);
            float capX = (w - capW) * 0.5f;
            bar.DrawRect(new Rect2(capX, 1f, capW, 2f), new Color(0.86f, 0.97f, 1.00f, 0.18f + pulse * 0.04f));
            bar.DrawRect(new Rect2(capX + 6f, h - 3f, capW - 12f, 2f),
                new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.24f + pulse * 0.05f));
        };

        RegisterAnimatedSurface(bar);
    }

    private void ApplyTopHudControlClusterStyle(PanelContainer rightCluster)
    {
        var clusterStyle = UITheme.MakePanel(
            bg: new Color(0.012f, 0.028f, 0.074f, 0.86f),
            border: new Color(0.24f, 0.62f, 0.76f, 0.68f),
            corners: 7,
            borderWidth: 1,
            padH: 5,
            padV: 2);
        clusterStyle.ShadowColor = new Color(0.22f, 0.66f, 0.82f, 0.12f);
        clusterStyle.ShadowSize = 6;
        clusterStyle.ShadowOffset = new Vector2(0f, 1f);
        rightCluster.AddThemeStyleboxOverride("panel", clusterStyle);

        rightCluster.Draw += () =>
        {
            float w = rightCluster.Size.X;
            float h = rightCluster.Size.Y;
            if (w < 40f || h < 10f)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_hudAnimTime * 0.75f + 0.5f);

            rightCluster.DrawRect(new Rect2(1f, 1f, w - 2f, h - 2f), new Color(0.22f, 0.46f, 0.58f, 0.16f), false, 1f);
            rightCluster.DrawRect(new Rect2(2f, 2f, w - 4f, h * 0.46f), new Color(0.22f, 0.44f, 0.54f, 0.10f));
            rightCluster.DrawRect(new Rect2(5f, 3f, w - 10f, h - 6f), new Color(0.84f, 0.97f, 1.00f, 0.08f), false, 1f);
            rightCluster.DrawLine(new Vector2(7f, 3f), new Vector2(w - 7f, 3f), new Color(0.92f, 0.99f, 1.00f, 0.24f), 1f);
            rightCluster.DrawLine(new Vector2(7f, h - 3f), new Vector2(w - 7f, h - 3f), new Color(0f, 0f, 0f, 0.40f), 1f);

            float capW = Mathf.Clamp(w * 0.20f, 24f, 62f);
            float capX = (w - capW) * 0.5f;
            rightCluster.DrawRect(new Rect2(capX, 2f, capW, 2f), new Color(0.90f, 0.98f, 1.00f, 0.18f + pulse * 0.05f));
            rightCluster.DrawRect(new Rect2(capX + 2f, h - 4f, capW - 4f, 2f),
                new Color(UITheme.Cyan.R, UITheme.Cyan.G, UITheme.Cyan.B, 0.22f + pulse * 0.07f));

            float emitH = Mathf.Clamp(h * 0.50f, 12f, 20f);
            float emitY = h * 0.5f - emitH * 0.5f;
            rightCluster.DrawRect(new Rect2(2f, emitY, 2f, emitH), new Color(0.52f, 0.94f, 1.00f, 0.30f + pulse * 0.08f));
            rightCluster.DrawRect(new Rect2(w - 4f, emitY, 2f, emitH), new Color(0.60f, 0.58f, 1.00f, 0.28f + pulse * 0.08f));
        };

        RegisterAnimatedSurface(rightCluster);
    }

    private Panel CreateTopHudReadoutChip(
        Panel parent,
        float anchorLeft,
        float anchorRight,
        float offsetLeft,
        float offsetRight,
        float offsetTop,
        float offsetBottom,
        Color accent,
        float alphaScale = 1f)
    {
        var chip = new Panel
        {
            AnchorLeft = anchorLeft,
            AnchorRight = anchorRight,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = offsetLeft,
            OffsetRight = offsetRight,
            OffsetTop = offsetTop,
            OffsetBottom = offsetBottom,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var chipStyle = UITheme.MakePanel(
            bg: new Color(0.010f, 0.028f, 0.070f, 0.72f * alphaScale),
            border: new Color(accent.R, accent.G, accent.B, 0.54f * alphaScale),
            corners: 6,
            borderWidth: 1,
            padH: 0,
            padV: 0);
        chipStyle.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.08f * alphaScale);
        chipStyle.ShadowSize = 4;
        chipStyle.ShadowOffset = new Vector2(0f, 1f);
        chip.AddThemeStyleboxOverride("panel", chipStyle);

        chip.Draw += () =>
        {
            float w = chip.Size.X;
            float h = chip.Size.Y;
            if (w < 20f || h < 8f)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_hudAnimTime * 0.70f + w * 0.01f);
            chip.DrawRect(new Rect2(2f, 2f, w - 4f, h * 0.48f), new Color(accent.R, accent.G, accent.B, 0.10f * alphaScale));
            chip.DrawRect(new Rect2(2f, h * 0.50f, w - 4f, h * 0.46f), new Color(0f, 0f, 0f, 0.14f * alphaScale));
            chip.DrawRect(new Rect2(3f, 3f, w - 6f, h - 6f), new Color(0.88f, 0.98f, 1.00f, 0.05f * alphaScale), false, 1f);
            chip.DrawLine(new Vector2(5f, 3f), new Vector2(w - 5f, 3f), new Color(0.92f, 0.99f, 1.00f, 0.18f * alphaScale), 1f);
            chip.DrawLine(new Vector2(5f, h - 3f), new Vector2(w - 5f, h - 3f), new Color(0f, 0f, 0f, 0.26f * alphaScale), 1f);
            chip.DrawRect(new Rect2((w - 26f) * 0.5f, 2f, 26f, 2f), new Color(0.90f, 0.98f, 1.00f, (0.14f + pulse * 0.05f) * alphaScale));
            chip.DrawRect(new Rect2((w - 22f) * 0.5f, h - 4f, 22f, 2f), new Color(accent.R, accent.G, accent.B, (0.18f + pulse * 0.06f) * alphaScale));
        };

        parent.AddChild(chip);
        parent.MoveChild(chip, 0);
        RegisterAnimatedSurface(chip);
        return chip;
    }

    private void ApplyMandateBannerStyle(Panel banner)
    {
        var style = UITheme.MakePanel(
            bg: new Color(0.10f, 0.06f, 0.01f, 0.90f),
            border: new Color(1.00f, 0.74f, 0.30f, 0.74f),
            corners: 0,
            borderWidth: 1,
            padH: 0,
            padV: 0);
        banner.AddThemeStyleboxOverride("panel", style);

        banner.Draw += () =>
        {
            float w = banner.Size.X;
            float h = banner.Size.Y;
            if (w < 20f || h < 6f)
                return;

            banner.DrawRect(new Rect2(0f, 0f, w, h * 0.48f), new Color(1.00f, 0.72f, 0.24f, 0.11f));
            banner.DrawRect(new Rect2(0f, h * 0.52f, w, h * 0.48f), new Color(0f, 0f, 0f, 0.18f));
            banner.DrawLine(new Vector2(0f, 1f), new Vector2(w, 1f), new Color(1.00f, 0.88f, 0.60f, 0.36f), 1f);
            banner.DrawLine(new Vector2(0f, h - 1f), new Vector2(w, h - 1f), new Color(0f, 0f, 0f, 0.36f), 1f);
        };
    }

    private void ApplyTopHudButtonStyle(Button btn, Color accent)
    {
        bool limeAccent = accent == UITheme.Lime;
        Color normalBg = limeAccent ? new Color(0.036f, 0.092f, 0.034f) : new Color(0.020f, 0.040f, 0.090f);
        Color hoverBg  = limeAccent ? new Color(0.060f, 0.145f, 0.048f) : new Color(0.038f, 0.068f, 0.125f);
        Color pressBg  = limeAccent ? new Color(0.024f, 0.064f, 0.024f) : new Color(0.018f, 0.030f, 0.078f);

        btn.AddThemeStyleboxOverride("normal", MakeTopHudButtonStyle(
            normalBg,
            new Color(accent.R, accent.G, accent.B, 0.74f),
            borderWidth: 1));
        btn.AddThemeStyleboxOverride("hover", MakeTopHudButtonStyle(hoverBg, accent, borderWidth: 2, glowAlpha: 0.19f, glowSize: 6, glowColor: accent));
        btn.AddThemeStyleboxOverride("focus", MakeTopHudButtonStyle(hoverBg, accent, borderWidth: 2, glowAlpha: 0.15f, glowSize: 5, glowColor: accent));
        btn.AddThemeStyleboxOverride("pressed", MakeTopHudButtonStyle(
            pressBg,
            new Color(accent.R, accent.G, accent.B, 0.75f),
            borderWidth: 2,
            glowAlpha: 0.10f,
            glowSize: 3,
            glowColor: accent));
        btn.AddThemeFontOverride("font", UITheme.SemiBold);
        btn.AddThemeColorOverride("font_color", limeAccent ? new Color(0.92f, 0.98f, 0.86f) : new Color(0.84f, 0.96f, 1.00f));
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", limeAccent ? new Color(0.84f, 0.94f, 0.72f) : new Color(0.72f, 0.90f, 0.96f));
        btn.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.58f));
        btn.AddThemeConstantOverride("outline_size", 1);
        UITheme.ApplyMenuButtonFinish(btn, accent, 0.10f, 0.13f);
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");

        btn.Draw += () =>
        {
            float w = btn.Size.X;
            float h = btn.Size.Y;
            if (w < 10f || h < 8f)
                return;

            float sweepW = Mathf.Clamp(w * 0.28f, 12f, 28f);
            float sweepSpan = Mathf.Max(1f, w - 12f - sweepW);
            float sweepX = 6f + ((_hudAnimTime * (limeAccent ? 0.24f : 0.18f)) % 1f) * sweepSpan;
            btn.DrawRect(new Rect2(sweepX, 4f, sweepW, 2f), new Color(0.88f, 0.98f, 1.00f, 0.10f));
            btn.DrawRect(new Rect2(4f, 4f, w - 8f, h - 8f), new Color(0.88f, 0.96f, 1.00f, 0.08f), false, 1f);
            btn.DrawLine(new Vector2(6f, 3f), new Vector2(w - 6f, 3f), new Color(accent.R, accent.G, accent.B, 0.23f), 1f);
            btn.DrawLine(new Vector2(6f, h - 3f), new Vector2(w - 6f, h - 3f), new Color(0f, 0f, 0f, 0.34f), 1f);
        };

        RegisterAnimatedSurface(btn);
    }

    private static StyleBoxFlat MakeTopHudButtonStyle(
        Color bg,
        Color border,
        int borderWidth,
        float glowAlpha = 0f,
        int glowSize = 0,
        Color? glowColor = null)
    {
        var style = UITheme.MakeBtn(
            bg,
            border,
            border: borderWidth,
            corners: 7,
            glowAlpha: glowAlpha,
            glowSize: glowSize,
            glowColor: glowColor);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        return style;
    }

    private void RegisterAnimatedSurface(Control control)
    {
        if (!_animatedSurfaces.Contains(control))
            _animatedSurfaces.Add(control);
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

    private void OnMenuButtonPressed()
    {
        var pauseScreen = FindPauseScreen();
        if (pauseScreen == null)
            return;

        pauseScreen.OpenPauseMenu();
        UpdatePausePlayButtonLabel();
    }

    private PauseScreen? FindPauseScreen()
    {
        var pauseScreens = GetTree().GetNodesInGroup("pause_screen");
        foreach (Node node in pauseScreens)
        {
            if (node is PauseScreen pauseScreen)
                return pauseScreen;
        }
        return null;
    }

    private void ToggleGameplayPause()
    {
        var pauseScreen = FindPauseScreen();
        if (pauseScreen == null)
            return;

        pauseScreen.ToggleGameplayPause();
        UpdatePausePlayButtonLabel();
    }

    private void UpdatePausePlayButtonLabel()
    {
        if (!GodotObject.IsInstanceValid(_pausePlayBtn))
            return;
        _pausePlayBtn.Text = GetTree().Paused ? PlayIcon : PauseIcon;
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

    private void BuildGlobalSurgeMeter()
    {
        // Single-row panel: label left, pips fill the rest.
        _globalSpectaclePanel = new Panel
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -211f,
            OffsetRight = 211f,
            OffsetTop = -36f,
            OffsetBottom = -14f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0.97f),
        };
        _globalSpectaclePanel.GuiInput += (@event) =>
        {
            if (_isGlobalSurgeReady
                && _globalSurgeInteractionEnabled
                && @event is InputEventMouseButton mb
                && mb.Pressed
                && mb.ButtonIndex == MouseButton.Left)
                GlobalSurgeActivateRequested?.Invoke();
        };
        _globalSpectaclePanel.AddThemeStyleboxOverride(
            "panel",
            UITheme.MakePanel(
                bg: new Color(0.04f, 0.09f, 0.15f, 0.88f),
                border: new Color(0.62f, 0.92f, 1.00f, 0.82f),
                corners: 8,
                borderWidth: 2,
                padH: 8,
                padV: 4));
        AddChild(_globalSpectaclePanel);

        // Horizontal layout: [label] [pips] - with explicit margins so content clears the border.
        var row = new HBoxContainer
        {
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f,
            OffsetLeft = 10f,
            OffsetRight = -10f,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 4);
        _globalSpectaclePanel.AddChild(row);

        // Label - fixed width, vertically centred.
        _surgeNameLabel = new Label
        {
            Text = "GLOBAL SURGE",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1.00f, 0.95f, 0.76f, 1f),
        };
        UITheme.ApplyFont(_surgeNameLabel, semiBold: true, size: 13);
        _surgeNameLabel.AddThemeConstantOverride("outline_size", 2);
        _surgeNameLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.78f));
        row.AddChild(_surgeNameLabel);

        // Pip row fills remaining width, pips centred vertically at fixed height.
        var pipsRow = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        pipsRow.AddThemeConstantOverride("separation", 1);
        row.AddChild(pipsRow);

        _surgePips = new ColorRect[SurgePipCount];
        for (int i = 0; i < SurgePipCount; i++)
        {
            var pip = new ColorRect
            {
                Color = PipEmpty,
                CustomMinimumSize = new Vector2(0f, 6f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _surgePips[i] = pip;
            pipsRow.AddChild(pip);
        }

        _surgeMeterHint = new Label
        {
            Text = "Full towers power this",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = -230f,
            OffsetRight = 230f,
            OffsetTop = -58f,
            OffsetBottom = -40f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        UITheme.ApplyFont(_surgeMeterHint, size: 16);
        _surgeMeterHint.AddThemeColorOverride("font_color", new Color(1.00f, 0.95f, 0.76f));
        _surgeMeterHint.AddThemeConstantOverride("outline_size", 2);
        _surgeMeterHint.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.88f));
        AddChild(_surgeMeterHint);

        _teachingHintPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetLeft = -255f,
            OffsetRight = 255f,
            OffsetTop = 58f,
            OffsetBottom = 96f,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        _teachingHintPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.05f, 0.12f, 0.20f, 0.94f),
            border: new Color(1.00f, 0.90f, 0.42f, 0.95f),
            corners: 10,
            borderWidth: 2,
            padH: 12,
            padV: 7));
        AddChild(_teachingHintPanel);

        _teachingHintConnector = new Line2D
        {
            Width = 2.2f,
            Antialiased = true,
            DefaultColor = new Color(1.00f, 0.92f, 0.54f, 0.92f),
            Visible = false,
        };
        AddChild(_teachingHintConnector);

        _teachingHintLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        UITheme.ApplyFont(_teachingHintLabel, semiBold: true, size: 17);
        _teachingHintLabel.AddThemeColorOverride("font_color", new Color(1.00f, 0.98f, 0.84f));
        _teachingHintLabel.AddThemeConstantOverride("outline_size", 2);
        _teachingHintLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.82f));
        _teachingHintPanel.AddChild(_teachingHintLabel);
    }
}
