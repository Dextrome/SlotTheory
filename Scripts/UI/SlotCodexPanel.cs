using System;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen encyclopedia for towers and modifiers.
/// Locked entries render as unrevealed card backs.
/// </summary>
public partial class SlotCodexPanel : Node
{
    public Action? BackOverride { get; set; }

    private enum CodexTab { Towers, Mods }

    private ScrollContainer _towerScroll = null!;
    private ScrollContainer _modScroll = null!;
    private GridContainer _towerGrid = null!;
    private GridContainer _modGrid = null!;
    private Button _towerTabBtn = null!;
    private Button _modTabBtn = null!;
    private Label _progressLabel = null!;
    private float _lastWidth = -1f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DataLoader.LoadAll();

        var canvas = new CanvasLayer { Layer = BackOverride != null ? 10 : 9 };
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0.05f, 0.06f, 0.12f);
        canvas.AddChild(bg);

        var neonBg = new NeonGridBg
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        neonBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        neonBg.Modulate = new Color(1f, 1f, 1f, 0.42f);
        canvas.AddChild(neonBg);

        var topShade = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.08f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        topShade.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topShade.OffsetBottom = MobileOptimization.IsMobile() ? 120f : 132f;
        canvas.AddChild(topShade);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        int sideMargin = MobileOptimization.IsMobile() ? 12 : 30;
        margin.AddThemeConstantOverride("margin_left", sideMargin);
        margin.AddThemeConstantOverride("margin_right", sideMargin);
        margin.AddThemeConstantOverride("margin_top", MobileOptimization.IsMobile() ? 8 : 12);
        margin.AddThemeConstantOverride("margin_bottom", MobileOptimization.IsMobile() ? 14 : 12);
        margin.Theme = UITheme.Build();
        canvas.AddChild(margin);

        var root = new VBoxContainer();
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var headerPanel = new PanelContainer();
        headerPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.07f, 0.08f, 0.17f, 0.94f),
            border: new Color(0.26f, 0.34f, 0.58f, 0.82f),
            corners: 12,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 12 : 16,
            padV: MobileOptimization.IsMobile() ? 9 : 10));
        root.AddChild(headerPanel);

        var headerBody = new VBoxContainer();
        headerBody.AddThemeConstantOverride("separation", 4);
        headerPanel.AddChild(headerBody);

        var title = new Label
        {
            Text = "SLOT CODEX",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(title, semiBold: true, size: MobileOptimization.IsMobile() ? 34 : 44);
        title.Modulate = UITheme.Lime;
        headerBody.AddChild(title);

        var subtitle = new Label
        {
            Text = "Tower and modifier encyclopedia",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 14 : 16);
        subtitle.Modulate = new Color(0.65f, 0.70f, 0.82f);
        headerBody.AddChild(subtitle);

        _progressLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(_progressLabel, semiBold: true, size: MobileOptimization.IsMobile() ? 13 : 14);
        _progressLabel.Modulate = new Color(0.73f, 0.88f, 1.00f, 0.92f);
        headerBody.AddChild(_progressLabel);

        var tabs = new HBoxContainer();
        tabs.Alignment = BoxContainer.AlignmentMode.Center;
        tabs.AddThemeConstantOverride("separation", 12);
        root.AddChild(tabs);

        _towerTabBtn = BuildTabButton("Towers", () => SetActiveTab(CodexTab.Towers));
        _modTabBtn = BuildTabButton("Mods", () => SetActiveTab(CodexTab.Mods));
        tabs.AddChild(_towerTabBtn);
        tabs.AddChild(_modTabBtn);

        var contentFrame = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 360f : 440f)
        };
        contentFrame.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.05f, 0.06f, 0.13f, 0.90f),
            border: new Color(0.18f, 0.25f, 0.42f, 0.86f),
            corners: 12,
            borderWidth: 2,
            padH: 8,
            padV: 8));
        root.AddChild(contentFrame);

        var contentHolder = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 360f : 440f)
        };
        contentFrame.AddChild(contentHolder);

        _towerScroll = BuildCardScroll(contentHolder, out _towerGrid);
        _modScroll = BuildCardScroll(contentHolder, out _modGrid);

        PopulateTowerCards();
        PopulateModifierCards();
        SetActiveTab(CodexTab.Towers);
        RefreshGridColumns(force: true);

        var backCenter = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        root.AddChild(backCenter);

        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(180f, MobileOptimization.IsMobile() ? 44f : 48f),
        };
        backBtn.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 18 : 20);
        UITheme.ApplyCyanStyle(backBtn);
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        backBtn.Pressed += HandleBack;
        backCenter.AddChild(backBtn);
    }

    public override void _Process(double delta)
    {
        _ = delta;
        RefreshGridColumns(force: false);
    }

    public override void _Notification(int what)
    {
        if (what == 1007)
            HandleBack();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            HandleBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleBack()
    {
        SoundManager.Instance?.Play("ui_select");
        if (BackOverride != null)
        {
            BackOverride();
            QueueFree();
            return;
        }

        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }

    private Button BuildTabButton(string text, Action onPressed)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 130f : 178f, MobileOptimization.IsMobile() ? 44f : 46f),
        };
        btn.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 17 : 19);
        UITheme.ApplyCyanStyle(btn);
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        btn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            onPressed();
        };
        return btn;
    }

    private ScrollContainer BuildCardScroll(Control parent, out GridContainer grid)
    {
        var scroll = new ScrollContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetRight = 0f,
            OffsetBottom = 0f,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        TouchScrollHelper.EnableDragScroll(scroll);
        parent.AddChild(scroll);

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", 2);
        inner.AddThemeConstantOverride("margin_right", 2);
        inner.AddThemeConstantOverride("margin_top", 4);
        inner.AddThemeConstantOverride("margin_bottom", 8);
        inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        inner.MouseFilter = Control.MouseFilterEnum.Pass;
        scroll.AddChild(inner);

        grid = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        inner.AddChild(grid);

        return scroll;
    }

    private void PopulateTowerCards()
    {
        foreach (var id in DataLoader.GetAllTowerIds(includeLocked: true))
        {
            var def = DataLoader.GetTowerDef(id);
            bool unlocked = Unlocks.IsTowerUnlocked(id);
            _towerGrid.AddChild(BuildTowerCard(id, def, unlocked));
        }
        _towerGrid.AddChild(BuildFullGameCard("More in the full release."));
    }

    private void PopulateModifierCards()
    {
        foreach (var id in DataLoader.GetAllModifierIds(includeLocked: true))
        {
            var def = DataLoader.GetModifierDef(id);
            bool unlocked = Unlocks.IsModifierUnlocked(id);
            _modGrid.AddChild(BuildModifierCard(id, def, unlocked));
        }
        _modGrid.AddChild(BuildFullGameCard("More in the full release."));
        _modGrid.AddChild(BuildFullGameCard("More in the full release."));
    }

    private Control BuildTowerCard(string towerId, TowerDef def, bool unlocked)
    {
        var panel = BuildCardShell(unlocked, GetTowerAccent(towerId));
        var body = BuildCardBody(panel);

        if (!unlocked)
        {
            BuildCardBack(body, "UNREVEALED TOWER", GetUnlockGateNote(towerId));
            return panel;
        }

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        body.AddChild(top);

        var icon = new TowerIcon
        {
            TowerId = towerId,
            CustomMinimumSize = new Vector2(52f, 52f),
            Size = new Vector2(52f, 52f)
        };
        top.AddChild(icon);

        var titleCol = new VBoxContainer();
        titleCol.AddThemeConstantOverride("separation", 2);
        titleCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(titleCol);

        var name = new Label { Text = def.Name };
        UITheme.ApplyFont(name, semiBold: true, size: 18);
        name.Modulate = Colors.White;
        titleCol.AddChild(name);

        var type = new Label { Text = "TOWER" };
        type.AddThemeFontSizeOverride("font_size", 12);
        type.Modulate = GetTowerAccent(towerId);
        titleCol.AddChild(type);

        var stats = new Label
        {
            Text = $"{def.BaseDamage:0.#} dmg  |  {def.AttackInterval:0.##} s  |  {(int)def.Range} px",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        stats.AddThemeFontSizeOverride("font_size", 14);
        stats.Modulate = new Color(0.64f, 0.80f, 1.00f);
        body.AddChild(stats);

        var desc = new Label
        {
            Text = GetTowerCodexDescription(towerId),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        desc.AddThemeFontSizeOverride("font_size", 13);
        desc.Modulate = new Color(0.76f, 0.80f, 0.88f);
        body.AddChild(desc);

        return panel;
    }

    private Control BuildModifierCard(string modifierId, ModifierDef def, bool unlocked)
    {
        var panel = BuildCardShell(unlocked, ModifierVisuals.GetAccent(modifierId));
        var body = BuildCardBody(panel);

        if (!unlocked)
        {
            BuildCardBack(body, "UNREVEALED MOD", GetUnlockGateNote(modifierId));
            return panel;
        }

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        body.AddChild(top);

        var icon = new ModifierIcon
        {
            ModifierId = modifierId,
            IconColor = ModifierVisuals.GetAccent(modifierId),
            CustomMinimumSize = new Vector2(50f, 50f),
            Size = new Vector2(50f, 50f)
        };
        top.AddChild(icon);

        var titleCol = new VBoxContainer();
        titleCol.AddThemeConstantOverride("separation", 2);
        titleCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(titleCol);

        var name = new Label { Text = def.Name };
        UITheme.ApplyFont(name, semiBold: true, size: 18);
        name.Modulate = Colors.White;
        titleCol.AddChild(name);

        var tag = new Label { Text = ModifierVisuals.GetTag(modifierId) };
        tag.AddThemeFontSizeOverride("font_size", 12);
        tag.Modulate = ModifierVisuals.GetAccent(modifierId);
        titleCol.AddChild(tag);

        var desc = new Label
        {
            Text = def.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        desc.AddThemeFontSizeOverride("font_size", 14);
        desc.Modulate = new Color(0.80f, 0.85f, 0.94f);
        body.AddChild(desc);

        return panel;
    }

    private PanelContainer BuildCardShell(bool unlocked, Color accent)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(290f, 166f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        // On mobile, codex cards are read-only; let drag gestures pass through to ScrollContainer.
        if (MobileOptimization.IsMobile())
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        var baseBorder = unlocked
            ? new Color(
                Mathf.Lerp(0.22f, accent.R, 0.35f),
                Mathf.Lerp(0.32f, accent.G, 0.35f),
                Mathf.Lerp(0.46f, accent.B, 0.35f),
                0.86f)
            : new Color(0.18f, 0.19f, 0.30f);

        var style = UITheme.MakePanel(
            bg: unlocked ? new Color(0.08f, 0.09f, 0.18f) : new Color(0.06f, 0.07f, 0.13f),
            border: baseBorder,
            corners: 10,
            borderWidth: unlocked ? 2 : 1,
            padH: 12,
            padV: 10);
        panel.AddThemeStyleboxOverride("panel", style);

        if (unlocked)
        {
            var hoverStyle = (StyleBoxFlat)style.Duplicate();
            hoverStyle.BorderColor = new Color(accent.R, accent.G, accent.B, 0.95f);
            hoverStyle.ShadowColor = new Color(accent.R, accent.G, accent.B, 0.22f);
            hoverStyle.ShadowSize = 8;
            hoverStyle.ShadowOffset = Vector2.Zero;

            panel.MouseEntered += () =>
            {
                panel.ZIndex = 2;
                panel.PivotOffset = panel.Size * 0.5f;
                panel.AddThemeStyleboxOverride("panel", hoverStyle);
                var tw = panel.CreateTween();
                tw.TweenProperty(panel, "scale", new Vector2(1.015f, 1.015f), 0.09f)
                  .SetTrans(Tween.TransitionType.Sine)
                  .SetEase(Tween.EaseType.Out);
            };
            panel.MouseExited += () =>
            {
                panel.AddThemeStyleboxOverride("panel", style);
                var tw = panel.CreateTween();
                tw.TweenProperty(panel, "scale", Vector2.One, 0.09f)
                  .SetTrans(Tween.TransitionType.Sine)
                  .SetEase(Tween.EaseType.Out);
                tw.TweenCallback(Callable.From(() => panel.ZIndex = 0));
            };
        }

        return panel;
    }

    private static VBoxContainer BuildCardBody(PanelContainer panel)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);
        return body;
    }

    private static string GetUnlockGateNote(string id)
    {
        try
        {
            string mapId = id switch
            {
                Unlocks.ArcEmitterTowerId  => Unlocks.GetArcEmitterUnlockMapId(),
                Unlocks.RiftPrismTowerId   => Unlocks.GetRiftPrismUnlockMapId(),
                Unlocks.SplitShotModifierId => Unlocks.GetSplitShotUnlockMapId(),
                _ => ""
            };
            if (string.IsNullOrEmpty(mapId)) return "Reveal by completing unlock achievements.";
            var map = DataLoader.GetAllMapDefs().FirstOrDefault(m => m.Id == mapId);
            string mapName = map?.Name ?? "a campaign map";
            return $"Unlock by clearing {mapName} on any difficulty.";
        }
        catch
        {
            return "Reveal by completing unlock achievements.";
        }
    }

    private static void BuildCardBack(VBoxContainer body, string label, string? gateNote = null)
    {
        var top = new Label
        {
            Text = "CLASSIFIED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(top, semiBold: true, size: 17);
        top.Modulate = new Color(0.76f, 0.82f, 0.98f);
        body.AddChild(top);

        var sep = new ColorRect
        {
            CustomMinimumSize = new Vector2(0f, 1f),
            Color = new Color(0.45f, 0.50f, 0.64f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.AddChild(sep);

        var lockLabel = new Label
        {
            Text = label,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(lockLabel, semiBold: true, size: 14);
        lockLabel.Modulate = new Color(0.80f, 0.84f, 0.95f);
        body.AddChild(lockLabel);

        var note = new Label
        {
            Text = gateNote ?? "Reveal by completing unlock achievements.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 13);
        note.Modulate = new Color(0.56f, 0.62f, 0.76f);
        body.AddChild(note);
    }

    private void SetActiveTab(CodexTab tab)
    {
        bool towers = tab == CodexTab.Towers;
        _towerScroll.Visible = towers;
        _modScroll.Visible = !towers;
        ApplyTabButtonState(_towerTabBtn, towers);
        ApplyTabButtonState(_modTabBtn, !towers);
        RefreshProgressLabel(tab);
    }

    private static void ApplyTabButtonState(Button btn, bool active)
    {
        if (active)
            UITheme.ApplyPrimaryStyle(btn);
        else
            UITheme.ApplyCyanStyle(btn);
    }

    private void RefreshGridColumns(bool force)
    {
        float width = GetViewport().GetVisibleRect().Size.X;
        if (!force && Mathf.Abs(width - _lastWidth) < 1f)
            return;

        _lastWidth = width;
        int maxCols = MobileOptimization.IsMobile() ? 2 : 4;
        float cardMin = MobileOptimization.IsMobile() ? 252f : 296f;
        int cols = Mathf.Clamp(Mathf.FloorToInt((width - 80f) / cardMin), 1, maxCols);
        _towerGrid.Columns = cols;
        _modGrid.Columns = cols;
    }

    private void RefreshProgressLabel(CodexTab tab)
    {
        int towerTotal = DataLoader.GetAllTowerIds(includeLocked: true).Count();
        int towerUnlocked = DataLoader.GetAllTowerIds(includeLocked: true).Count(Unlocks.IsTowerUnlocked);
        int modTotal = DataLoader.GetAllModifierIds(includeLocked: true).Count();
        int modUnlocked = DataLoader.GetAllModifierIds(includeLocked: true).Count(Unlocks.IsModifierUnlocked);

        _progressLabel.Text = tab == CodexTab.Towers
            ? $"Demo: {towerUnlocked}/{towerTotal} towers   •   Mods: {modUnlocked}/{modTotal}   •   More in full game"
            : $"Demo: {modUnlocked}/{modTotal} mods   •   Towers: {towerUnlocked}/{towerTotal}   •   More in full game";
    }

    private static Control BuildFullGameCard(string note)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(290f, 166f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        if (MobileOptimization.IsMobile())
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        panel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.05f, 0.06f, 0.10f),
            border: new Color(0.22f, 0.24f, 0.36f, 0.60f),
            corners: 10,
            borderWidth: 1,
            padH: 12,
            padV: 10));

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);

        var lockLabel = new Label
        {
            Text = "FULL GAME",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(lockLabel, semiBold: true, size: 17);
        lockLabel.Modulate = new Color(0.55f, 0.60f, 0.80f, 0.70f);
        body.AddChild(lockLabel);

        var sep = new ColorRect
        {
            CustomMinimumSize = new Vector2(0f, 1f),
            Color = new Color(0.30f, 0.34f, 0.50f, 0.35f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.AddChild(sep);

        var noteLabel = new Label
        {
            Text = note,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        noteLabel.AddThemeFontSizeOverride("font_size", 13);
        noteLabel.Modulate = new Color(0.45f, 0.50f, 0.65f, 0.70f);
        body.AddChild(noteLabel);

        return panel;
    }

    private static Color GetTowerAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.30f, 0.90f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.55f, 0.00f),
        "marker_tower" => new Color(1.00f, 0.15f, 0.60f),
        "chain_tower" => new Color(0.50f, 0.85f, 1.00f),
        "rift_prism" => new Color(0.60f, 1.00f, 0.58f),
        _ => new Color(0.82f, 0.88f, 1.00f),
    };

    private static string GetTowerCodexDescription(string towerId) => towerId switch
    {
        "rapid_shooter" => "High cadence chip tower. Great for stacking Momentum, Feedback Loop, and Chill pressure.",
        "heavy_cannon" => "Burst cannon with huge per-shot impact. Best when paired with Overkill or Focus Lens.",
        "marker_tower" => "Applies Mark to amplify team damage. Core enabler for Exploit Weakness strategies.",
        "chain_tower" => "Built-in chain bounces for dense waves and lane clear. Excellent in clustered path sections.",
        "rift_prism" => "Plants charged lane mines. Final charge detonates harder, with rapid wave-start seeding for early trap setup.",
        _ => "Tower entry."
    };
}
