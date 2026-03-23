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

    private enum CodexTab { Towers, Mods, Enemies }

    private ScrollContainer _towerScroll = null!;
    private ScrollContainer _modScroll = null!;
    private ScrollContainer _enemyScroll = null!;
    private GridContainer _towerGrid = null!;
    private GridContainer _modGrid = null!;
    private GridContainer _enemyGrid = null!;
    private Button _towerTabBtn = null!;
    private Button _modTabBtn = null!;
    private Button _enemyTabBtn = null!;
    private ColorRect _tabIndicator = null!;
    private CodexTab _activeTab = CodexTab.Towers;
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
        UITheme.ApplyGlassChassisPanel(
            headerPanel,
            bg: new Color(0.07f, 0.08f, 0.17f, 0.94f),
            accent: new Color(0.38f, 0.74f, 0.92f, 0.90f),
            corners: 12,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 12 : 16,
            padV: MobileOptimization.IsMobile() ? 9 : 10,
            sideEmitters: false);
        root.AddChild(headerPanel);
        UITheme.AddTopAccent(headerPanel);

        var headerBody = new VBoxContainer();
        headerBody.AddThemeConstantOverride("separation", 4);
        headerPanel.AddChild(headerBody);

        var title = new Label { Text = "SLOT CODEX" };
        UITheme.ApplyAnagramTitle(title, MobileOptimization.IsMobile() ? 38 : 50);
        headerBody.AddChild(title);

        var subtitle = new Label
        {
            Text = "Tower, modifier, and enemy encyclopedia",
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
        headerBody.AddChild(tabs);

        tabs.AddChild(BuildTabEntry("Towers",  () => SetActiveTab(CodexTab.Towers),  out _towerTabBtn));
        tabs.AddChild(BuildTabEntry("Mods",    () => SetActiveTab(CodexTab.Mods),    out _modTabBtn));
        tabs.AddChild(BuildTabEntry("Enemies", () => SetActiveTab(CodexTab.Enemies), out _enemyTabBtn));

        // Single sliding indicator -- repositioned under the active tab on each switch.
        var indicatorRow = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, 3f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        headerBody.AddChild(indicatorRow);
        _tabIndicator = new ColorRect
        {
            Color = UITheme.Lime,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Size = new Vector2(MobileOptimization.IsMobile() ? 106f : 158f, 3f),
        };
        indicatorRow.AddChild(_tabIndicator);

        var contentFrame = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 360f : 440f)
        };
        UITheme.ApplyGlassChassisPanel(
            contentFrame,
            bg: new Color(0.05f, 0.06f, 0.13f, 0.90f),
            accent: new Color(0.38f, 0.74f, 0.92f, 0.90f),
            corners: 12,
            borderWidth: 2,
            padH: 8,
            padV: 8,
            sideEmitters: true,
            emitterIntensity: 0.92f);
        root.AddChild(contentFrame);

        var contentHolder = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 360f : 440f)
        };
        contentFrame.AddChild(contentHolder);

        _towerScroll  = BuildCardScroll(contentHolder, out _towerGrid);
        _modScroll    = BuildCardScroll(contentHolder, out _modGrid);
        _enemyScroll  = BuildCardScroll(contentHolder, out _enemyGrid);

        PopulateTowerCards();
        PopulateModifierCards();
        PopulateEnemyCards();
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
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.10f, 0.12f);
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

    private Control BuildTabEntry(string label, Action onPressed, out Button btn)
    {
        float btnW = MobileOptimization.IsMobile() ? 106f : 158f;
        float btnH = MobileOptimization.IsMobile() ? 40f : 42f;

        btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(btnW, btnH),
        };
        btn.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 16 : 18);

        // Flat uniform style -- no border swap, no BG color change
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.17f, 0.0f),
            BorderWidthTop = 0, BorderWidthBottom = 0, BorderWidthLeft = 0, BorderWidthRight = 0,
            ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 4, ContentMarginBottom = 4
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover",  style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("focus",   new StyleBoxEmpty());
        btn.AddThemeColorOverride("font_color", new Color(0.48f, 0.58f, 0.76f));
        btn.AddThemeColorOverride("font_hover_color", new Color(0.65f, 0.75f, 0.92f));
        btn.AddThemeColorOverride("font_pressed_color", new Color(0.48f, 0.58f, 0.76f));

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
        inner.AddThemeConstantOverride("margin_left", 10);  // ≥ ShadowSize (8) so hover glow isn't clipped
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
    }

    private void PopulateModifierCards()
    {
        foreach (var id in DataLoader.GetAllModifierIds(includeLocked: true))
        {
            var def = DataLoader.GetModifierDef(id);
            bool unlocked = Unlocks.IsModifierUnlocked(id);
            _modGrid.AddChild(BuildModifierCard(id, def, unlocked));
        }
    }

    private Control BuildTowerCard(string towerId, TowerDef def, bool unlocked)
    {
        var panel = BuildCardShell(unlocked, GetTowerAccent(towerId));
        var body = BuildCardBody(panel);

        if (!unlocked)
        {
            bool isFullGameOnly = Balance.IsDemo &&
                string.Equals(towerId, Unlocks.AccordionEngineTowerId, StringComparison.OrdinalIgnoreCase);
            BuildCardBack(body,
                isFullGameOnly ? "FULL GAME"               : "UNREVEALED TOWER",
                isFullGameOnly ? "Available in full release." : GetUnlockGateNote(towerId));
            return panel;
        }

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        body.AddChild(top);

        var icon = new TowerIconFull
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
            bool isFullGameOnly = Balance.IsDemo &&
                string.Equals(modifierId, Unlocks.BlastCoreModifierId, StringComparison.OrdinalIgnoreCase);
            BuildCardBack(body,
                isFullGameOnly ? "FULL GAME"        : "UNREVEALED MOD",
                isFullGameOnly ? "Available in full release." : GetUnlockGateNote(modifierId));
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

            // 2px accent stripe at top of card in the card's accent color
            UITheme.AddTopAccent(panel, new Color(accent.R, accent.G, accent.B, 0.75f));
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
                Unlocks.ArcEmitterTowerId      => Unlocks.GetArcEmitterUnlockMapId(),
                Unlocks.RiftPrismTowerId       => Unlocks.GetRiftPrismUnlockMapId(),
                Unlocks.AccordionEngineTowerId => Unlocks.GetAccordionEngineUnlockMapId(),
                Unlocks.SplitShotModifierId    => Unlocks.GetSplitShotUnlockMapId(),
                Unlocks.BlastCoreModifierId    => Unlocks.GetBlastCoreUnlockMapId(),
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
        _activeTab = tab;
        _towerScroll.Visible  = tab == CodexTab.Towers;
        _modScroll.Visible    = tab == CodexTab.Mods;
        _enemyScroll.Visible  = tab == CodexTab.Enemies;
        ApplyTabButtonState(_towerTabBtn, tab == CodexTab.Towers);
        ApplyTabButtonState(_modTabBtn,   tab == CodexTab.Mods);
        ApplyTabButtonState(_enemyTabBtn, tab == CodexTab.Enemies);
        RefreshProgressLabel(tab);
        CallDeferred(nameof(RepositionIndicator));
    }

    private static void ApplyTabButtonState(Button btn, bool active)
    {
        btn.AddThemeColorOverride("font_color",
            active ? UITheme.Lime : new Color(0.48f, 0.58f, 0.76f));
        btn.AddThemeColorOverride("font_hover_color",
            active ? UITheme.Lime : new Color(0.65f, 0.75f, 0.92f));
        btn.AddThemeColorOverride("font_pressed_color",
            active ? UITheme.Lime : new Color(0.48f, 0.58f, 0.76f));
    }

    private void RepositionIndicator()
    {
        if (!GodotObject.IsInstanceValid(_tabIndicator)) return;
        Button activeBtn = _activeTab switch
        {
            CodexTab.Towers  => _towerTabBtn,
            CodexTab.Mods    => _modTabBtn,
            _                => _enemyTabBtn,
        };
        if (!GodotObject.IsInstanceValid(activeBtn)) return;
        var row = _tabIndicator.GetParent<Control>();
        if (!GodotObject.IsInstanceValid(row)) return;

        float x = activeBtn.GlobalPosition.X - row.GlobalPosition.X;
        float w = activeBtn.Size.X;
        _tabIndicator.Position = new Vector2(x, 0f);
        _tabIndicator.Size     = new Vector2(w, 3f);
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
        _towerGrid.Columns  = cols;
        _modGrid.Columns    = cols;
        _enemyGrid.Columns  = cols;
    }

    private void RefreshProgressLabel(CodexTab tab)
    {
        int towerTotal = DataLoader.GetAllTowerIds(includeLocked: true).Count();
        int towerUnlocked = DataLoader.GetAllTowerIds(includeLocked: true).Count(Unlocks.IsTowerUnlocked);
        int modTotal = DataLoader.GetAllModifierIds(includeLocked: true).Count();
        int modUnlocked = DataLoader.GetAllModifierIds(includeLocked: true).Count(Unlocks.IsModifierUnlocked);
        int enemyTotal = Balance.IsDemo ? 5 : 7;

        _towerTabBtn.Text = $"{towerUnlocked}/{towerTotal} Towers";
        _modTabBtn.Text   = $"{modUnlocked}/{modTotal} Mods";
        _enemyTabBtn.Text = $"{enemyTotal} Enemies";

        _progressLabel.Text = (Balance.IsDemo && tab != CodexTab.Enemies)
            ? "Full game has more content"
            : (Balance.IsDemo && tab == CodexTab.Enemies)
            ? "2 more enemy types in the full game"
            : "";
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
        "chain_tower"      => new Color(0.50f, 0.85f, 1.00f),
        "rift_prism"       => new Color(0.60f, 1.00f, 0.58f),
        "accordion_engine" => new Color(0.72f, 0.20f, 1.00f),
        _ => new Color(0.82f, 0.88f, 1.00f),
    };

    private static string GetTowerCodexDescription(string towerId) => towerId switch
    {
        "rapid_shooter" => "High cadence chip tower. Great for stacking Momentum, Feedback Loop, and Chill pressure.",
        "heavy_cannon" => "Burst cannon with huge per-shot impact. Best when paired with Overkill or Focus Lens.",
        "marker_tower" => "Applies Mark to amplify team damage. Core enabler for Exploit Weakness strategies.",
        "chain_tower"      => "Built-in chain bounces for dense waves and lane clear. Excellent in clustered path sections.",
        "rift_prism"       => "Plants charged lane mines. Final charge detonates harder, with rapid wave-start seeding for early trap setup.",
        "accordion_engine" => "Emits compression pulses that physically squeeze enemy spacing along the lane. Not a slow or stun -- it edits wave formation topology. Packed enemies become better targets for Blast Core, Arc Emitter chains, and Rift Sapper mines.",
        _ => "Tower entry."
    };

    // ── Enemies tab ──────────────────────────────────────────────────────────

    private void PopulateEnemyCards()
    {
        _enemyGrid.AddChild(BuildEnemyCard("basic_walker"));
        _enemyGrid.AddChild(BuildEnemyCard("armored_walker"));
        _enemyGrid.AddChild(BuildEnemyCard("splitter_walker"));
        _enemyGrid.AddChild(BuildEnemyCard("splitter_shard"));
        _enemyGrid.AddChild(BuildEnemyCard("swift_walker"));
        if (!Balance.IsDemo)
        {
            _enemyGrid.AddChild(BuildEnemyCard("shield_drone"));
            _enemyGrid.AddChild(BuildEnemyCard("reverse_walker"));
        }
        if (Balance.IsDemo)
        {
            _enemyGrid.AddChild(BuildFullGameLockedEnemyCard("shield_drone"));
            _enemyGrid.AddChild(BuildFullGameLockedEnemyCard("reverse_walker"));
        }
    }

    private Control BuildEnemyCard(string enemyId)
    {
        Color accent = GetEnemyAccent(enemyId);
        var panel = BuildCardShell(unlocked: true, accent);
        panel.CustomMinimumSize = new Vector2(290f, 0f);
        var body  = BuildCardBody(panel);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        body.AddChild(top);

        var icon = new EnemyIcon
        {
            EnemyId = enemyId,
            CustomMinimumSize = new Vector2(52f, 52f),
            Size = new Vector2(52f, 52f)
        };
        top.AddChild(icon);

        var titleCol = new VBoxContainer();
        titleCol.AddThemeConstantOverride("separation", 2);
        titleCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(titleCol);

        var nameLabel = new Label { Text = GetEnemyDisplayName(enemyId) };
        UITheme.ApplyFont(nameLabel, semiBold: true, size: 18);
        nameLabel.Modulate = Colors.White;
        titleCol.AddChild(nameLabel);

        var typeLabel = new Label { Text = "ENEMY" };
        typeLabel.AddThemeFontSizeOverride("font_size", 12);
        typeLabel.Modulate = accent;
        titleCol.AddChild(typeLabel);

        var statsBox = new VBoxContainer();
        statsBox.AddThemeConstantOverride("separation", 5);
        body.AddChild(statsBox);

        foreach (var (iconType, text) in GetEnemyStats(enemyId))
            statsBox.AddChild(BuildStatRow(iconType, GetStatIconColor(iconType), text));

        var desc = new Label
        {
            Text = GetEnemyDescription(enemyId),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        desc.AddThemeFontSizeOverride("font_size", 13);
        desc.Modulate = new Color(0.76f, 0.80f, 0.88f);
        body.AddChild(desc);

        return panel;
    }

    private Control BuildFullGameLockedEnemyCard(string enemyId)
    {
        Color accent = GetEnemyAccent(enemyId);
        Color dimAccent = new Color(accent.R * 0.40f, accent.G * 0.40f, accent.B * 0.40f, 0.55f);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(290f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        if (MobileOptimization.IsMobile())
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        panel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.05f, 0.06f, 0.10f),
            border: new Color(0.20f, 0.22f, 0.35f, 0.50f),
            corners: 10,
            borderWidth: 1,
            padH: 12,
            padV: 10));

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        body.AddChild(top);

        var icon = new EnemyIcon
        {
            EnemyId = enemyId,
            CustomMinimumSize = new Vector2(52f, 52f),
            Size = new Vector2(52f, 52f),
            Modulate = new Color(0.32f, 0.32f, 0.38f, 0.50f)
        };
        top.AddChild(icon);

        var titleCol = new VBoxContainer();
        titleCol.AddThemeConstantOverride("separation", 2);
        titleCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(titleCol);

        var nameLabel = new Label { Text = GetEnemyDisplayName(enemyId) };
        UITheme.ApplyFont(nameLabel, semiBold: true, size: 18);
        nameLabel.Modulate = new Color(0.40f, 0.42f, 0.54f);
        titleCol.AddChild(nameLabel);

        var typeLabel = new Label { Text = "ENEMY" };
        typeLabel.AddThemeFontSizeOverride("font_size", 12);
        typeLabel.Modulate = dimAccent;
        titleCol.AddChild(typeLabel);

        var sep = new ColorRect
        {
            CustomMinimumSize = new Vector2(0f, 1f),
            Color = new Color(0.25f, 0.28f, 0.42f, 0.35f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        body.AddChild(sep);

        var fullGameLabel = new Label
        {
            Text = "FULL GAME",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(fullGameLabel, semiBold: true, size: 17);
        fullGameLabel.Modulate = new Color(0.60f, 0.68f, 0.90f, 0.88f);
        body.AddChild(fullGameLabel);

        var noteLabel = new Label
        {
            Text = "Available in the full release.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        noteLabel.AddThemeFontSizeOverride("font_size", 13);
        noteLabel.Modulate = new Color(0.40f, 0.46f, 0.60f, 0.70f);
        body.AddChild(noteLabel);

        return panel;
    }

    private static Color GetEnemyAccent(string enemyId) => enemyId switch
    {
        "armored_walker"  => new Color(1.00f, 0.58f, 0.10f),
        "swift_walker"    => new Color(0.20f, 0.92f, 1.00f),
        "reverse_walker"  => new Color(0.48f, 0.96f, 1.00f),
        "splitter_walker" => new Color(1.00f, 0.72f, 0.15f),
        "splitter_shard"  => new Color(1.00f, 0.88f, 0.45f),
        "shield_drone"    => new Color(0.30f, 0.76f, 1.00f),
        _                 => new Color(0.55f, 0.95f, 0.25f),
    };

    private static string GetEnemyDisplayName(string enemyId) => enemyId switch
    {
        "armored_walker"  => "Armored Walker",
        "swift_walker"    => "Swift Walker",
        "reverse_walker"  => "Reverse Walker",
        "splitter_walker" => "Splitter Walker",
        "splitter_shard"  => "Splitter Shard",
        "shield_drone"    => "Shield Drone",
        _                 => "Basic Walker",
    };

    private static Control BuildStatRow(StatIconNode.IconType iconType, Color iconColor, string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 7);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;

        var icon = new StatIconNode
        {
            Type = iconType,
            IconColor = iconColor,
            CustomMinimumSize = new Vector2(14f, 14f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(icon);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.Modulate = new Color(0.74f, 0.84f, 1.00f);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(label);

        return row;
    }

    private static Color GetStatIconColor(StatIconNode.IconType type) => type switch
    {
        StatIconNode.IconType.Heart => new Color(1.00f, 0.38f, 0.52f),
        StatIconNode.IconType.Arrow => new Color(0.28f, 0.90f, 1.00f),
        StatIconNode.IconType.Skull => new Color(1.00f, 0.52f, 0.12f),
        StatIconNode.IconType.Wave  => new Color(0.90f, 0.88f, 0.28f),
        StatIconNode.IconType.Split => new Color(1.00f, 0.78f, 0.28f),
        _ => Colors.White,
    };

    private static (StatIconNode.IconType, string)[] GetEnemyStats(string enemyId) => enemyId switch
    {
        "armored_walker" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.TankyHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.TankyEnemySpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 2 lives"),
            (StatIconNode.IconType.Wave,  "From wave 6"),
        },
        "swift_walker" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.SwiftHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.SwiftEnemySpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 1 life"),
            (StatIconNode.IconType.Wave,  "Waves 10-19"),
        },
        "reverse_walker" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.ReverseWalkerHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.ReverseWalkerSpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 1 life"),
            (StatIconNode.IconType.Wave,  "From wave 11"),
        },
        "splitter_walker" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.SplitterHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.SplitterSpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 3 lives"),
            (StatIconNode.IconType.Wave,  "Waves 9-15"),
        },
        "splitter_shard" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.SplitterShardHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.SplitterShardSpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 1 life"),
            (StatIconNode.IconType.Split, "On Splitter death"),
        },
        "shield_drone" => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp * Balance.ShieldDroneHpMultiplier:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.ShieldDroneSpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 1 life"),
            (StatIconNode.IconType.Wave,  "From wave 9"),
        },
        _ => new (StatIconNode.IconType, string)[]
        {
            (StatIconNode.IconType.Heart, $"{Balance.BaseEnemyHp:0} HP"),
            (StatIconNode.IconType.Arrow, $"{Balance.BaseEnemySpeed:0} px/s"),
            (StatIconNode.IconType.Skull, "Leak: 1 life"),
            (StatIconNode.IconType.Wave,  "From wave 1"),
        },
    };

    private static string GetEnemyDescription(string enemyId) => enemyId switch
    {
        "armored_walker"  => "High-HP tanker. Takes sustained damage to bring down. Pairs poorly against single-hit burst builds without Overkill.",
        "swift_walker"    => "Fast sprinter that rushes the lane in mid-game pressure waves. Outpaces slow-paced builds - Chill Shot and Hair Trigger both help.",
        "reverse_walker"  => $"Trickster unit. If one hit chunks at least {Balance.ReverseWalkerTriggerDamageRatio * 100f:0}% of its max HP, it rewinds a short distance. Cooldown-gated and capped at {Balance.ReverseWalkerMaxTriggersPerLife} rewinds per life.",
        "splitter_walker" => $"Splits into {Balance.SplitterShardCount} fast shards on death. Burst damage that kills it cleanly does not reach the shards - sustained DPS or AoE builds must deal with both. A leaked Splitter risks 3 lives total.",
        "splitter_shard"  => $"A fragment of a destroyed Splitter. Faster than its parent and harder to catch mid-lane. {Balance.SplitterShardCount} spawn per kill - if your DPS cannot clean them up quickly they will slip through.",
        "shield_drone"    => $"Support unit. Projects a {Balance.ShieldDroneProtectionReduction * 100f:0}% damage reduction field to nearby allies within {Balance.ShieldDroneAuraRadius:0}px. Eliminate it first - shielded groups absorb far more punishment before breaking.",
        _                 => "Standard threat. Low bulk, steady pace. Pressure escalates each wave via HP scaling.",
    };
}

/// <summary>Small procedural icon drawn in 14×14 local space for the enemy stat rows.</summary>
public sealed partial class StatIconNode : Control
{
    public enum IconType { Heart, Arrow, Skull, Wave, Split }

    public IconType Type      { get; set; }
    public Color    IconColor { get; set; } = Colors.White;

    public override void _Draw()
    {
        switch (Type)
        {
            case IconType.Heart: DrawHeart(); break;
            case IconType.Arrow: DrawArrow(); break;
            case IconType.Skull: DrawSkull(); break;
            case IconType.Wave:  DrawWave();  break;
            case IconType.Split: DrawSplit(); break;
        }
    }

    // ♥  HP
    private void DrawHeart()
    {
        var c = IconColor;
        DrawCircle(new Vector2(4f, 5f),  3.5f, c);
        DrawCircle(new Vector2(10f, 5f), 3.5f, c);
        DrawPolygon(new[] { new Vector2(0.5f, 5f), new Vector2(13.5f, 5f), new Vector2(7f, 14f) }, new[] { c });
    }

    // ▶  Speed
    private void DrawArrow()
    {
        DrawPolygon(new[] { new Vector2(1f, 2f), new Vector2(13f, 7f), new Vector2(1f, 12f) }, new[] { IconColor });
    }

    // ☠  Leak
    private void DrawSkull()
    {
        var c    = IconColor;
        var dark = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        DrawCircle(new Vector2(7f, 6f),   5.5f, c);
        DrawCircle(new Vector2(4.2f, 6f), 1.6f, dark);
        DrawCircle(new Vector2(9.8f, 6f), 1.6f, dark);
        DrawRect(new Rect2(3.5f, 9.5f, 7f, 4f), c);
        DrawRect(new Rect2(5.4f, 9f, 1.1f, 4.5f), dark);
        DrawRect(new Rect2(7.5f, 9f, 1.1f, 4.5f), dark);
    }

    // 🚩  Spawns from wave X
    private void DrawWave()
    {
        var c = IconColor;
        DrawRect(new Rect2(2f, 0f, 2f, 14f), c);
        DrawPolygon(new[] { new Vector2(4f, 1f), new Vector2(13f, 4.5f), new Vector2(4f, 8f) }, new[] { c });
    }

    // ⑃  Spawns on death (split fork)
    private void DrawSplit()
    {
        var c = IconColor;
        DrawCircle(new Vector2(2.5f, 7f),  2f, c);
        DrawLine(new Vector2(4.5f, 7f), new Vector2(9f, 4f),   c, 1.5f);
        DrawLine(new Vector2(4.5f, 7f), new Vector2(9f, 10f),  c, 1.5f);
        DrawCircle(new Vector2(11f, 3.5f),  2f, c);
        DrawCircle(new Vector2(11f, 10.5f), 2f, c);
    }
}
