using Godot;
using System.Collections.Generic;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Intermediate screen shown when the player presses Play on the main menu.
/// Presents all play entry points in one place.
/// </summary>
public partial class ModeSelectPanel : Node
{
    private readonly List<Control> _animatedSurfaces = new();
    private float _animTime;
    private AcceptDialog _customPlaceholderDialog = null!;

    public override void _Ready()
    {
        CampaignManager.ClearActiveStage();
        CampaignProgress.Load();
        bool isMobile = MobileOptimization.IsMobile();
        var viewportSize = GetViewport().GetVisibleRect().Size;
        bool compactDesktopLayout = !isMobile && viewportSize.Y <= 940f;
        bool isDemo = Balance.IsDemo;

        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = UITheme.BgMenu;
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.AnchorBottom = isMobile ? 0.97f : (compactDesktopLayout ? 0.87f : 0.93f); // bias content upward so bottom controls stay visible
        if (compactDesktopLayout)
        {
            // Lift the stack just enough to keep Back visible while preserving top breathing room.
            center.OffsetTop = -16f;
            center.OffsetBottom = -16f;
        }
        center.Theme = UITheme.Build();
        canvas.AddChild(center);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", compactDesktopLayout ? 10 : 22);
        root.CustomMinimumSize = new Vector2(isMobile ? 420f : 920f, 0f);
        center.AddChild(root);

        var headerStack = new VBoxContainer();
        headerStack.AddThemeConstantOverride("separation", compactDesktopLayout ? 3 : 8);
        root.AddChild(headerStack);

        var kicker = new Label
        {
            Text = "PLAY SCREEN",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(kicker, semiBold: true, size: 16);
        kicker.Modulate = new Color(0.42f, 0.64f, 0.78f, 0.92f);
        headerStack.AddChild(kicker);

        var title = new Label
        {
            Text = "SELECT YOUR OPERATION",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(title, semiBold: true, size: isMobile ? 30 : 36);
        title.Modulate = new Color(0.88f, 0.95f, 1.00f, 0.97f);
        headerStack.AddChild(title);

        var subtitle = new Label
        {
            Text = "Campaign progression, quick skirmish, guided tutorial, or a future custom setup.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(isMobile ? 390f : 860f, 0f),
        };
        UITheme.ApplyFont(subtitle, size: 14);
        subtitle.Modulate = new Color(0.67f, 0.75f, 0.86f, 0.92f);
        headerStack.AddChild(subtitle);

        var cardGrid = new GridContainer
        {
            Columns = isMobile ? 1 : 2,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(isMobile ? 390f : 860f, 0f),
        };
        cardGrid.AddThemeConstantOverride("h_separation", 18);
        cardGrid.AddThemeConstantOverride("v_separation", compactDesktopLayout ? 8 : 18);
        root.AddChild(cardGrid);

        var tutorialDone = SettingsManager.Instance?.TutorialCompleted ?? false;
        var tutorialCard = BuildModeCard(
            title: "TUTORIAL",
            subtitle: tutorialDone ? "REFRESHER RUN" : "GUIDED START",
            description: tutorialDone
                ? "Re-run the guided flow and re-learn core systems.\nFast warm-up before serious runs."
                : "Step-by-step onboarding with curated waves.\nLearn drafting, tower timing, and surge usage.",
            actionLabel: tutorialDone ? "PLAY AGAIN" : "START TUTORIAL",
            accentColor: new Color(0.26f, 0.92f, 0.86f),
            onPressed: OnTutorial,
            compactLayout: compactDesktopLayout);
        cardGrid.AddChild(tutorialCard);
        AnimateCardIn(tutorialCard, 0.00f);

        var campaignCard = BuildModeCard(
            title: "CAMPAIGN",
            subtitle: "THE FRACTURE CIRCUIT",
            description: "Four linked combat zones.\nEach sector imposes a new mandate.\nPush deeper. Break the circuit.",
            actionLabel: "ENTER CIRCUIT",
            accentColor: UITheme.Lime,
            onPressed: OnCampaign,
            isPrimaryAction: true,
            isEnabled: !isDemo,
            compactLayout: compactDesktopLayout);
        cardGrid.AddChild(campaignCard);
        AnimateCardIn(campaignCard, 0.05f);

        var skirmishCard = BuildModeCard(
            title: "SKIRMISH",
            subtitle: "SINGLE RUN",
            description: "Pick any map, any difficulty.\nNo restrictions. Standard rules.\nOne run, one result.",
            actionLabel: "START SKIRMISH",
            accentColor: new Color(0.72f, 0.45f, 0.96f),
            onPressed: OnSkirmish,
            compactLayout: compactDesktopLayout);
        cardGrid.AddChild(skirmishCard);
        AnimateCardIn(skirmishCard, 0.10f);

        var customCard = BuildModeCard(
            title: "CUSTOM GAME",
            subtitle: "PLACEHOLDER",
            description: "Sandbox knobs and custom run rules are planned.\nThis tile is active as a placeholder for now.",
            actionLabel: "COMING SOON",
            accentColor: new Color(0.92f, 0.55f, 0.26f),
            onPressed: OnCustomGamePlaceholder,
            isPlaceholder: true,
            isEnabled: !isDemo,
            compactLayout: compactDesktopLayout);
        cardGrid.AddChild(customCard);
        AnimateCardIn(customCard, 0.15f);

        var backCenter = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        backCenter.Theme = UITheme.Build();
        root.AddChild(backCenter);

        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(isMobile ? 170f : 190f, isMobile ? 42f : 46f),
        };
        backBtn.AddThemeFontSizeOverride("font_size", isMobile ? 18 : 20);
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.09f, 0.11f);
        backBtn.Pressed += OnBack;
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        RegisterAnimatedSurface(backBtn);
        backCenter.AddChild(backBtn);

        _customPlaceholderDialog = new AcceptDialog
        {
            Title = "Custom Game",
            DialogText = "Custom Game is a placeholder for now.\nFull sandbox setup is coming soon.",
            Exclusive = true,
        };
        _customPlaceholderDialog.OkButtonText = "Back";
        canvas.AddChild(_customPlaceholderDialog);

        SetProcess(true);
        MobileOptimization.ApplyUIScale(center);
        AddChild(new PinchZoomHandler(center));
    }

    public override void _Process(double delta)
    {
        _animTime += (float)delta;
        for (int i = _animatedSurfaces.Count - 1; i >= 0; i--)
        {
            var c = _animatedSurfaces[i];
            if (!GodotObject.IsInstanceValid(c) || !c.IsInsideTree())
            {
                _animatedSurfaces.RemoveAt(i);
                continue;
            }
            c.QueueRedraw();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (GodotObject.IsInstanceValid(_customPlaceholderDialog) && _customPlaceholderDialog.Visible)
            {
                _customPlaceholderDialog.Hide();
                GetViewport().SetInputAsHandled();
                return;
            }
            OnBack();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
            OnBack();
    }

    private void OnCampaign()
    {
        SoundManager.Instance?.Play("ui_select");
        SlotTheory.Data.DataLoader.LoadAll();
        Transition.Instance?.FadeToScene("res://Scenes/CampaignSelect.tscn");
    }

    private void OnSkirmish()
    {
        SoundManager.Instance?.Play("ui_select");
        SlotTheory.Data.DataLoader.LoadAll();
        Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
    }

    private void OnTutorial()
    {
        SoundManager.Instance?.Play("ui_select");
        SlotTheory.Data.DataLoader.LoadAll();
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.PendingTutorialRun = true;
        Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
    }

    private void OnCustomGamePlaceholder()
    {
        SoundManager.Instance?.Play("ui_select");
        _customPlaceholderDialog.PopupCentered();
    }

    private void OnBack()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }

    private Control BuildModeCard(
        string title,
        string subtitle,
        string description,
        string actionLabel,
        Color accentColor,
        System.Action onPressed,
        bool isPrimaryAction = false,
        bool isPlaceholder = false,
        bool isEnabled = true,
        bool compactLayout = false)
    {
        var card = new PanelContainer();
        bool isMobile = MobileOptimization.IsMobile();
        card.CustomMinimumSize = new Vector2(isMobile ? 380f : 420f, isMobile ? 222f : (compactLayout ? 198f : 232f));
        var displayAccent = isEnabled
            ? accentColor
            : new Color(accentColor.R * 0.55f, accentColor.G * 0.55f, accentColor.B * 0.55f, 0.95f);
        UITheme.ApplyGlassChassisPanel(
            card,
            bg: isEnabled
                ? new Color(0.018f, 0.030f, 0.078f, 0.94f)
                : new Color(0.010f, 0.014f, 0.030f, 0.97f),
            accent: new Color(displayAccent.R, displayAccent.G, displayAccent.B, isEnabled ? 0.88f : 0.58f),
            corners: 12,
            borderWidth: 2,
            padH: 24,
            padV: 18,
            sideEmitters: true,
            emitterIntensity: 0.80f);
        RegisterAnimatedSurface(card);

        card.Draw += () =>
        {
            float w = card.Size.X;
            float h = card.Size.Y;
            if (w < 24f || h < 24f)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(_animTime * 0.95f + card.GetIndex() * 0.77f);
            card.DrawRect(new Rect2(8f, 8f, w - 16f, h - 16f),
                new Color(0f, 0f, 0f, (isEnabled ? 0.14f : 0.28f) + pulse * (isEnabled ? 0.04f : 0.02f)));
            card.DrawRect(new Rect2(10f, 10f, w - 20f, h - 20f),
                new Color(displayAccent.R, displayAccent.G, displayAccent.B, (isEnabled ? 0.06f : 0.03f) + pulse * 0.03f), false, 1f);
            card.DrawRect(new Rect2(12f, 12f, w - 24f, 20f), new Color(0.92f, 0.98f, 1.00f, isEnabled ? 0.06f : 0.03f));

            float scanW = Mathf.Clamp(w * 0.20f, 56f, 90f);
            float scanTravel = Mathf.Max(1f, w - 32f - scanW);
            float scanX = 16f + ((_animTime * 0.11f + card.GetIndex() * 0.23f) % 1f) * scanTravel;
            card.DrawRect(new Rect2(scanX, 14f, scanW, 2f), new Color(0.96f, 1.00f, 1.00f, (isEnabled ? 0.15f : 0.06f) + pulse * 0.05f));

            card.DrawLine(new Vector2(18f, h - 18f), new Vector2(w - 18f, h - 18f),
                new Color(displayAccent.R, displayAccent.G, displayAccent.B, (isEnabled ? 0.22f : 0.14f) + pulse * 0.08f), 1f);

            if (!isEnabled)
            {
                card.DrawRect(new Rect2(10f, 10f, w - 20f, h - 20f), new Color(0f, 0f, 0f, 0.24f));
            }
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        card.AddChild(vbox);

        var chip = new Label
        {
            Text = isPlaceholder ? "FUTURE SLOT" : "LIVE MODE",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(chip, semiBold: true, size: 11);
        chip.Modulate = new Color(displayAccent.R, displayAccent.G, displayAccent.B, isEnabled ? 0.75f : 0.60f);
        if (!isEnabled)
            chip.Text = "DEMO LOCKED";
        vbox.AddChild(chip);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(titleLabel, semiBold: true, size: 34);
        titleLabel.Modulate = displayAccent;
        vbox.AddChild(titleLabel);

        var subtitleLabel = new Label
        {
            Text = subtitle,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(subtitleLabel, semiBold: false, size: 13);
        subtitleLabel.Modulate = new Color(displayAccent.R, displayAccent.G, displayAccent.B, isEnabled ? 0.65f : 0.50f);
        vbox.AddChild(subtitleLabel);

        var rule = new ColorRect
        {
            Color = new Color(displayAccent.R, displayAccent.G, displayAccent.B, isEnabled ? 0.28f : 0.18f),
            CustomMinimumSize = new Vector2(0f, 1f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vbox.AddChild(rule);

        var descLabel = new Label
        {
            Text = description,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Word,
        };
        descLabel.AddThemeFontSizeOverride("font_size", 14);
        descLabel.Modulate = isEnabled
            ? new Color(0.70f, 0.76f, 0.86f, 0.90f)
            : new Color(0.58f, 0.62f, 0.72f, 0.84f);
        descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddChild(descLabel);

        vbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var btn = new Button
        {
            Text = actionLabel,
            CustomMinimumSize = new Vector2(0f, 44f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        btn.AddThemeFontSizeOverride("font_size", 18);
        if (isPrimaryAction)
        {
            UITheme.ApplyPrimaryStyle(btn);
            UITheme.ApplyMenuButtonFinish(btn, UITheme.Lime, 0.11f, 0.15f);
        }
        else
        {
            btn.AddThemeStyleboxOverride("normal", UITheme.MakeBtn(
                new Color(0.014f, 0.034f, 0.066f),
                new Color(accentColor.R * 0.65f, accentColor.G * 0.65f, accentColor.B * 0.65f, 0.90f),
                border: 1, corners: 9, glowAlpha: 0.05f, glowSize: 2, glowColor: accentColor));
            btn.AddThemeStyleboxOverride("hover", UITheme.MakeBtn(
                new Color(0.032f, 0.072f, 0.118f),
                accentColor,
                border: 2, corners: 9, glowAlpha: 0.22f, glowSize: 8, glowColor: accentColor));
            btn.AddThemeStyleboxOverride("focus", UITheme.MakeBtn(
                new Color(0.028f, 0.064f, 0.106f),
                accentColor,
                border: 2, corners: 9, glowAlpha: 0.18f, glowSize: 6, glowColor: accentColor));
            btn.AddThemeStyleboxOverride("pressed", UITheme.MakeBtn(
                new Color(0.008f, 0.020f, 0.044f),
                new Color(accentColor.R * 0.75f, accentColor.G * 0.75f, accentColor.B * 0.75f, 0.95f),
                border: 2, corners: 9, glowAlpha: 0.08f, glowSize: 4, glowColor: accentColor));
            btn.AddThemeColorOverride("font_color", new Color(0.88f, 0.95f, 1.00f, 0.96f));
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            UITheme.ApplyMenuButtonFinish(btn, accentColor, 0.10f, 0.12f);
        }

        if (isPlaceholder)
            btn.AddThemeColorOverride("font_color", new Color(1.0f, 0.90f, 0.78f, 0.96f));

        btn.Disabled = !isEnabled;
        if (!isEnabled)
        {
            btn.Text = "LOCKED IN DEMO";
            btn.AddThemeStyleboxOverride("normal", UITheme.MakeBtn(
                new Color(0.012f, 0.016f, 0.028f),
                new Color(0.22f, 0.24f, 0.30f, 0.95f),
                border: 1, corners: 9, glowAlpha: 0f, glowSize: 0));
            btn.AddThemeStyleboxOverride("disabled", UITheme.MakeBtn(
                new Color(0.012f, 0.016f, 0.028f),
                new Color(0.22f, 0.24f, 0.30f, 0.95f),
                border: 1, corners: 9, glowAlpha: 0f, glowSize: 0));
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.53f, 0.56f, 0.62f, 0.95f));
            UITheme.ApplyMenuButtonFinish(btn, new Color(0.25f, 0.28f, 0.34f), 0.05f, 0.10f);
        }
        else
        {
            btn.Pressed += onPressed;
            btn.MouseEntered += () =>
            {
                SoundManager.Instance?.Play("ui_hover");
                btn.PivotOffset = btn.Size / 2f;
                btn.CreateTween()
                   .TweenProperty(btn, "scale", new Vector2(1.03f, 1.03f), 0.08f)
                   .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            };
            btn.MouseExited += () =>
            {
                btn.PivotOffset = btn.Size / 2f;
                btn.CreateTween()
                   .TweenProperty(btn, "scale", Vector2.One, 0.08f)
                   .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            };
        }
        RegisterAnimatedSurface(btn);
        vbox.AddChild(btn);

        return card;
    }

    private void AnimateCardIn(Control card, float delaySeconds)
    {
        card.Modulate = new Color(1f, 1f, 1f, 0f);
        card.Scale = new Vector2(0.97f, 0.97f);
        var tween = CreateTween();
        tween.TweenInterval(delaySeconds);
        tween.SetParallel(true);
        tween.TweenProperty(card, "modulate:a", 1f, 0.30f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(card, "scale", Vector2.One, 0.30f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void RegisterAnimatedSurface(Control control)
    {
        if (!_animatedSurfaces.Contains(control))
            _animatedSurfaces.Add(control);
    }
}


