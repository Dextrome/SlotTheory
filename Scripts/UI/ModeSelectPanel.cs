using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Intermediate screen shown when the player presses Play on the main menu.
/// Presents two mode choices: Campaign (The Fracture Circuit) or Skirmish (map select).
/// </summary>
public partial class ModeSelectPanel : Node
{
    public override void _Ready()
    {
        CampaignManager.ClearActiveStage();
        CampaignProgress.Load();

        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#030a14");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = UITheme.Build();
        canvas.AddChild(center);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 20);
        root.CustomMinimumSize = new Vector2(860f, 0f);
        center.AddChild(root);

        var header = new Label
        {
            Text = "SELECT MODE",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(header, semiBold: true, size: 20);
        header.Modulate = new Color(0.45f, 0.60f, 0.72f, 0.90f);
        root.AddChild(header);

        var cardRow = new HBoxContainer();
        cardRow.AddThemeConstantOverride("separation", 24);
        cardRow.Alignment = BoxContainer.AlignmentMode.Center;
        root.AddChild(cardRow);

        cardRow.AddChild(BuildModeCard(
            title: "CAMPAIGN",
            subtitle: "THE FRACTURE CIRCUIT",
            description: "Four linked combat zones.\nEach sector imposes a new mandate.\nPush deeper. Break the circuit.",
            accentColor: UITheme.Lime,
            onPressed: OnCampaign));

        cardRow.AddChild(BuildModeCard(
            title: "SKIRMISH",
            subtitle: "SINGLE RUN",
            description: "Pick any map, any difficulty.\nNo restrictions. Standard rules.\nOne run, one result.",
            accentColor: UITheme.Cyan,
            onPressed: OnSkirmish));

        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(160f, 40f),
        };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.09f, 0.11f);
        backBtn.Pressed      += OnBack;
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");

        var backCenter = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        backCenter.AddChild(backBtn);
        root.AddChild(backCenter);

        MobileOptimization.ApplyUIScale(center);
        AddChild(new PinchZoomHandler(center));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
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
        Transition.Instance?.FadeToScene("res://Scenes/CampaignSelect.tscn");
    }

    private void OnSkirmish()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
    }

    private void OnBack()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }

    private static Control BuildModeCard(string title, string subtitle, string description,
        Color accentColor, System.Action onPressed)
    {
        var card = new PanelContainer();
        card.CustomMinimumSize = new Vector2(380f, 220f);
        UITheme.ApplyGlassChassisPanel(
            card,
            bg: new Color(0.018f, 0.030f, 0.078f, 0.94f),
            accent: new Color(accentColor.R, accentColor.G, accentColor.B, 0.88f),
            corners: 12,
            borderWidth: 2,
            padH: 24,
            padV: 20,
            sideEmitters: true,
            emitterIntensity: 0.80f);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        card.AddChild(vbox);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(titleLabel, semiBold: true, size: 38);
        titleLabel.Modulate = accentColor;
        vbox.AddChild(titleLabel);

        var subtitleLabel = new Label
        {
            Text = subtitle,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(subtitleLabel, semiBold: false, size: 13);
        subtitleLabel.Modulate = new Color(accentColor.R, accentColor.G, accentColor.B, 0.65f);
        vbox.AddChild(subtitleLabel);

        var rule = new ColorRect
        {
            Color = new Color(accentColor.R, accentColor.G, accentColor.B, 0.28f),
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
        descLabel.Modulate = new Color(0.70f, 0.76f, 0.86f, 0.90f);
        descLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddChild(descLabel);

        vbox.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var btn = new Button
        {
            Text = "SELECT",
            CustomMinimumSize = new Vector2(0f, 44f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        btn.AddThemeFontSizeOverride("font_size", 20);
        if (accentColor == UITheme.Lime)
        {
            UITheme.ApplyPrimaryStyle(btn);
            UITheme.ApplyMenuButtonFinish(btn, UITheme.Lime, 0.11f, 0.14f);
        }
        else
        {
            UITheme.ApplyCyanStyle(btn);
            UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.09f, 0.11f);
        }
        btn.Pressed      += onPressed;
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
        vbox.AddChild(btn);

        return card;
    }
}
