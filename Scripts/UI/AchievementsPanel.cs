using System.Linq;
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
        ProcessMode = ProcessModeEnum.Always;
        var canvas = new CanvasLayer { Layer = 9 };
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#07071a");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        canvas.AddChild(root);

        float vpW = GetViewport().GetVisibleRect().Size.X;
        int sidePad = MobileOptimization.IsMobile() ? 8 : (int)(vpW * 0.15f);

        var panelMargin = new MarginContainer();
        panelMargin.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        panelMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panelMargin.AddThemeConstantOverride("margin_left",   sidePad);
        panelMargin.AddThemeConstantOverride("margin_right",  sidePad);
        panelMargin.AddThemeConstantOverride("margin_top",    14);
        panelMargin.AddThemeConstantOverride("margin_bottom", 14);
        root.AddChild(panelMargin);

        var scrollPanel = new PanelContainer();
        scrollPanel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        scrollPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(
            scrollPanel,
            bg: new Color(0.05f, 0.04f, 0.13f, 0.96f),
            accent: new Color(0.42f, 0.78f, 0.94f, 0.92f),
            corners: 10, borderWidth: 2, padH: 0, padV: 0,
            sideEmitters: true, emitterIntensity: 0.86f);
        panelMargin.AddChild(scrollPanel);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.Theme = UITheme.Build();
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        scrollPanel.AddChild(scroll);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left",   18);
        margin.AddThemeConstantOverride("margin_right",  18);
        margin.AddThemeConstantOverride("margin_top",    16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        margin.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        margin.MouseFilter = Control.MouseFilterEnum.Pass;
        scroll.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
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

        string[] prestigeOrder = { "CHAIN_MASTER", "FLAWLESS", "SPEED_RUN", "HARD_WIN", "LAST_STAND", "ANNIHILATOR" };
        string? nextHard = prestigeOrder.FirstOrDefault(id => AchievementManager.Instance?.IsUnlocked(id) != true);
        string progressText = nextHard != null
            ? $"{unlocked} / {AchievementManager.All.Length}  -  next hard target: {nextHard.Replace('_', ' ')}"
            : $"{unlocked} / {AchievementManager.All.Length}  unlocked";

        var progress = new Label
        {
            Text = progressText,
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
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.10f, 0.12f);
        backBtn.Pressed += OnBack;
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");

        var backCenter = new CenterContainer();
        backCenter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        backCenter.AddChild(backBtn);
        vbox.AddChild(backCenter);
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
            OnBack();
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
        SoundManager.Instance?.Play("ui_select");
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
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left",   14);
        inner.AddThemeConstantOverride("margin_right",  14);
        inner.AddThemeConstantOverride("margin_top",    7);
        inner.AddThemeConstantOverride("margin_bottom", 7);
        inner.MouseFilter = Control.MouseFilterEnum.Ignore;
        panel.AddChild(inner);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 14);
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
        inner.AddChild(hbox);

        // Icon badge - use achievement icon (locked variant when not yet unlocked)
        var iconPath       = $"res://Assets/Achievements/{def.Id}.png";
        var iconLockedPath = $"res://Assets/Achievements/{def.Id}_locked.png";
        string? loadPath = isUnlocked && ResourceLoader.Exists(iconPath)        ? iconPath
                         : !isUnlocked && ResourceLoader.Exists(iconLockedPath) ? iconLockedPath
                         : null;
        if (loadPath != null)
        {
            var tex = ResourceLoader.Load<Texture2D>(loadPath);
            var iconRect = new TextureRect
            {
                Texture             = tex,
                ExpandMode          = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode         = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize   = new Vector2(48, 48),
                SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter,
            };
            hbox.AddChild(iconRect);
        }
        else
        {
            var badge = new Label { Text = isUnlocked ? "★" : "☆" };
            badge.AddThemeFontSizeOverride("font_size", 28);
            badge.Modulate          = isUnlocked ? new Color("#a6d608") : new Color(0.3f, 0.3f, 0.3f);
            badge.VerticalAlignment = VerticalAlignment.Center;
            hbox.AddChild(badge);
        }

        // Text
        var textCol = new VBoxContainer();
        textCol.AddThemeConstantOverride("separation", 2);
        textCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        textCol.MouseFilter = Control.MouseFilterEnum.Ignore;
        hbox.AddChild(textCol);

        var nameLabel = new Label { Text = def.Name };
        UITheme.ApplyFont(nameLabel, semiBold: true, size: 17);
        nameLabel.Modulate = isUnlocked ? new Color("#ffffff") : new Color(0.4f, 0.4f, 0.4f);
        textCol.AddChild(nameLabel);

        var descLabel = new Label { Text = def.Desc };
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
