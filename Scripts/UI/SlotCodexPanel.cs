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
    private float _lastWidth = -1f;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DataLoader.LoadAll();

        var canvas = new CanvasLayer { Layer = BackOverride != null ? 10 : 9 };
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#121426");
        canvas.AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        int sideMargin = MobileOptimization.IsMobile() ? 12 : 30;
        margin.AddThemeConstantOverride("margin_left", sideMargin);
        margin.AddThemeConstantOverride("margin_right", sideMargin);
        margin.AddThemeConstantOverride("margin_top", MobileOptimization.IsMobile() ? 10 : 12);
        margin.AddThemeConstantOverride("margin_bottom", MobileOptimization.IsMobile() ? 10 : 12);
        margin.Theme = UITheme.Build();
        canvas.AddChild(margin);

        var root = new VBoxContainer();
        root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var title = new Label
        {
            Text = "SLOT CODEX",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(title, semiBold: true, size: MobileOptimization.IsMobile() ? 34 : 44);
        title.Modulate = UITheme.Lime;
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = "Tower and modifier encyclopedia",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 14 : 16);
        subtitle.Modulate = new Color(0.65f, 0.70f, 0.82f);
        root.AddChild(subtitle);

        var tabs = new HBoxContainer();
        tabs.Alignment = BoxContainer.AlignmentMode.Center;
        tabs.AddThemeConstantOverride("separation", 10);
        root.AddChild(tabs);

        _towerTabBtn = BuildTabButton("Towers", () => SetActiveTab(CodexTab.Towers));
        _modTabBtn = BuildTabButton("Mods", () => SetActiveTab(CodexTab.Mods));
        tabs.AddChild(_towerTabBtn);
        tabs.AddChild(_modTabBtn);

        var contentHolder = new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 360f : 440f)
        };
        root.AddChild(contentHolder);

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
            Text = "<- Back",
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
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 130f : 170f, 42f),
        };
        btn.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 17 : 19);
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
        var panel = BuildCardShell(unlocked);
        var body = BuildCardBody(panel);

        if (!unlocked)
        {
            BuildCardBack(body, "UNREVEALED TOWER");
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
        var panel = BuildCardShell(unlocked);
        var body = BuildCardBody(panel);

        if (!unlocked)
        {
            BuildCardBack(body, "UNREVEALED MOD");
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

    private static PanelContainer BuildCardShell(bool unlocked)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(290f, 166f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        var style = UITheme.MakePanel(
            bg: unlocked ? new Color(0.08f, 0.09f, 0.18f) : new Color(0.06f, 0.07f, 0.13f),
            border: unlocked ? new Color(0.22f, 0.32f, 0.46f) : new Color(0.18f, 0.19f, 0.30f),
            corners: 10,
            borderWidth: unlocked ? 2 : 1,
            padH: 12,
            padV: 10);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static VBoxContainer BuildCardBody(PanelContainer panel)
    {
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);
        return body;
    }

    private static void BuildCardBack(VBoxContainer body, string label)
    {
        var top = new Label
        {
            Text = "SLOT CODEX",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(top, semiBold: true, size: 17);
        top.Modulate = new Color(0.72f, 0.76f, 0.90f);
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
            Text = "Win runs and unlock achievements to reveal this card.",
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
        float cardMin = MobileOptimization.IsMobile() ? 250f : 290f;
        int cols = Mathf.Clamp(Mathf.FloorToInt((width - 80f) / cardMin), 1, maxCols);
        _towerGrid.Columns = cols;
        _modGrid.Columns = cols;
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
