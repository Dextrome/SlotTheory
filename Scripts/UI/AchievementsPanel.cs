using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen achievements list. All UI built procedurally.
/// Navigate here via MainMenu → Achievements, or as an inline overlay from PauseScreen.
/// </summary>
public partial class AchievementsPanel : Node
{
    /// <summary>
    /// When set, the Back button calls this and frees the node instead of navigating to MainMenu.
    /// Used by PauseScreen to dismiss the overlay without leaving the game scene.
    /// </summary>
    public System.Action? BackOverride { get; set; }
    public override void _Ready()
    {
        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#141420");
        canvas.AddChild(bg);

        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.Theme = UITheme.Build();
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        canvas.AddChild(scroll);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   30);
        margin.AddThemeConstantOverride("margin_right",  30);
        margin.AddThemeConstantOverride("margin_top",    24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.AddChild(vbox);

        // ── Title ──────────────────────────────────────────────────────────────
        var title = new Label { Text = "ACHIEVEMENTS" };
        UITheme.ApplyFont(title, semiBold: true, size: 36);
        title.Modulate = new Color("#a6d608");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        int unlocked = 0;
        foreach (var def in AchievementManager.All)
            if (AchievementManager.Instance?.IsUnlocked(def.Id) == true) unlocked++;

        var progress = new Label
        {
            Text = $"{unlocked} / {AchievementManager.All.Length}  unlocked",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        progress.AddThemeFontSizeOverride("font_size", 16);
        progress.Modulate = new Color(0.55f, 0.55f, 0.55f);
        vbox.AddChild(progress);

        AddSpacer(vbox, 8);

        // ── Achievement rows ───────────────────────────────────────────────────
        foreach (var def in AchievementManager.All)
        {
            bool isUnlocked = AchievementManager.Instance?.IsUnlocked(def.Id) == true;
            vbox.AddChild(BuildRow(def, isUnlocked));
        }

        AddSpacer(vbox, 8);

        // ── Back button ────────────────────────────────────────────────────────
        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(160, 44),
        };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        backBtn.Pressed += OnBack;
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");

        var backCenter = new CenterContainer();
        backCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        backCenter.AddChild(backBtn);
        vbox.AddChild(backCenter);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            OnBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBack()
    {
        if (BackOverride != null) { BackOverride(); QueueFree(); return; }
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }

    // ── Row builder ───────────────────────────────────────────────────────────

    private static Control BuildRow(AchievementDefinition def, bool isUnlocked)
    {
        var style = new StyleBoxFlat
        {
            BgColor = isUnlocked ? new Color("#1e2e1e") : new Color("#1a1a28"),
            BorderColor = isUnlocked ? new Color("#a6d608") : new Color("#333348"),
            CornerRadiusTopLeft     = 6,
            CornerRadiusTopRight    = 6,
            CornerRadiusBottomLeft  = 6,
            CornerRadiusBottomRight = 6,
        };
        style.SetBorderWidthAll(isUnlocked ? 2 : 1);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left",   14);
        inner.AddThemeConstantOverride("margin_right",  14);
        inner.AddThemeConstantOverride("margin_top",    10);
        inner.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(inner);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 14);
        inner.AddChild(hbox);

        // Icon badge
        var badge = new Label { Text = isUnlocked ? "★" : "☆" };
        badge.AddThemeFontSizeOverride("font_size", 28);
        badge.Modulate = isUnlocked ? new Color("#a6d608") : new Color(0.3f, 0.3f, 0.3f);
        badge.VerticalAlignment   = VerticalAlignment.Center;
        hbox.AddChild(badge);

        // Text
        var textCol = new VBoxContainer();
        textCol.AddThemeConstantOverride("separation", 2);
        textCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(textCol);

        var nameLabel = new Label { Text = isUnlocked ? def.Name : "???" };
        UITheme.ApplyFont(nameLabel, semiBold: true, size: 17);
        nameLabel.Modulate = isUnlocked ? new Color("#ffffff") : new Color(0.4f, 0.4f, 0.4f);
        textCol.AddChild(nameLabel);

        var descLabel = new Label { Text = isUnlocked ? def.Desc : "Keep playing to unlock." };
        descLabel.AddThemeFontSizeOverride("font_size", 13);
        descLabel.Modulate     = isUnlocked ? new Color(0.65f, 0.65f, 0.65f) : new Color(0.3f, 0.3f, 0.3f);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textCol.AddChild(descLabel);

        return panel;
    }

    private static void AddSpacer(VBoxContainer vbox, int px)
    {
        vbox.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });
    }
}
