using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen campaign stage selection panel.
/// Displays The Fracture Circuit stage ladder with lock/clear state per difficulty.
/// </summary>
public partial class CampaignSelectPanel : Node
{
    private List<CampaignStageDefinition> _stages = new();
    private int           _selectedIndex     = 0;
    private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;

    private VBoxContainer? _stageListContainer;
    private Label?         _introLineLabel;
    private Button?        _easyBtn;
    private Button?        _normalBtn;
    private Button?        _hardBtn;
    private bool           _isMobile;

    public override void _Ready()
    {
        DataLoader.LoadAll();
        CampaignProgress.Load();

        _stages = DataLoader.GetCampaignStages()
                            .Select(d => new CampaignStageDefinition(d))
                            .ToList();

        // Default to first available-but-uncleared stage
        _selectedIndex = _stages.FindIndex(s =>
            CampaignProgress.IsAvailable(s.StageIndex) && !CampaignProgress.IsClearedOnAny(s.StageIndex));
        if (_selectedIndex < 0) _selectedIndex = 0;

        _isMobile = MobileOptimization.IsMobile();

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
        root.AddThemeConstantOverride("separation", 10);
        center.AddChild(root);

        // Header
        var headerVbox = new VBoxContainer();
        headerVbox.AddThemeConstantOverride("separation", 2);
        root.AddChild(headerVbox);

        var title = new Label
        {
            Text = "THE FRACTURE CIRCUIT",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(title, semiBold: true, size: 38);
        title.Modulate = new Color("#a6d608");
        headerVbox.AddChild(title);

        var headerSub = new Label
        {
            Text = "Four sectors. One path forward.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(headerSub, semiBold: false, size: 15);
        headerSub.Modulate = new Color(0.45f, 0.60f, 0.72f, 0.82f);
        headerVbox.AddChild(headerSub);

        // Body: stage list | right panel
        var bodyPanel = new PanelContainer();
        bodyPanel.CustomMinimumSize = _isMobile ? new Vector2(680, 360) : new Vector2(1100, 480);
        bodyPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bodyPanel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(bodyPanel,
            bg: new Color(0.040f, 0.052f, 0.102f, 0.94f),
            accent: new Color(0.40f, 0.78f, 0.94f, 0.92f),
            corners: 12, borderWidth: 2, padH: 14, padV: 12,
            sideEmitters: true, emitterIntensity: 0.86f);
        root.AddChild(bodyPanel);

        var contentRow = new HBoxContainer();
        contentRow.AddThemeConstantOverride("separation", 16);
        contentRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contentRow.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        bodyPanel.AddChild(contentRow);

        // Left: stage list
        var leftFrame = new PanelContainer();
        leftFrame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        leftFrame.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(leftFrame,
            bg: new Color(0.022f, 0.038f, 0.080f, 0.95f),
            accent: new Color(0.36f, 0.74f, 0.90f, 0.88f),
            corners: 10, borderWidth: 1, padH: 10, padV: 10,
            sideEmitters: false);
        contentRow.AddChild(leftFrame);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode  = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        leftFrame.AddChild(scroll);

        _stageListContainer = new VBoxContainer();
        _stageListContainer.AddThemeConstantOverride("separation", 10);
        _stageListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_stageListContainer);
        PopulateStageList();

        // Right panel: difficulty + intro line + begin/back
        var rightFrame = new PanelContainer();
        rightFrame.CustomMinimumSize = new Vector2(260, 0);
        rightFrame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(rightFrame,
            bg: new Color(0.022f, 0.038f, 0.080f, 0.95f),
            accent: new Color(0.36f, 0.74f, 0.90f, 0.88f),
            corners: 10, borderWidth: 1, padH: 12, padV: 10,
            sideEmitters: false);
        contentRow.AddChild(rightFrame);

        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 12);
        rightFrame.AddChild(rightCol);

        var diffLabel = new Label
        {
            Text = "DIFFICULTY",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(diffLabel, semiBold: true, size: 18);
        diffLabel.Modulate = new Color("#a6d608");
        rightCol.AddChild(diffLabel);

        var diffRow = new HBoxContainer();
        diffRow.Alignment = BoxContainer.AlignmentMode.Center;
        diffRow.AddThemeConstantOverride("separation", 8);
        rightCol.AddChild(diffRow);

        _easyBtn   = CreateDifficultyButton("Easy",   DifficultyMode.Easy);
        _normalBtn = CreateDifficultyButton("Normal", DifficultyMode.Normal);
        _hardBtn   = CreateDifficultyButton("Hard",   DifficultyMode.Hard);
        diffRow.AddChild(_easyBtn);
        diffRow.AddChild(_normalBtn);
        diffRow.AddChild(_hardBtn);
        UpdateDifficultyVisuals();

        var divider = new ColorRect
        {
            Color = new Color(0.32f, 0.70f, 0.90f, 0.24f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        rightCol.AddChild(divider);

        _introLineLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(230, 52),
        };
        _introLineLabel.AddThemeFontSizeOverride("font_size", 13);
        _introLineLabel.Modulate = new Color(0.70f, 0.88f, 0.72f, 0.88f);
        rightCol.AddChild(_introLineLabel);

        rightCol.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var beginBtn = new Button
        {
            Text = "Start Run",
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        beginBtn.AddThemeFontSizeOverride("font_size", 22);
        UITheme.ApplyPrimaryStyle(beginBtn);
        UITheme.ApplyMenuButtonFinish(beginBtn, UITheme.Lime, 0.11f, 0.14f);
        beginBtn.Pressed      += OnBegin;
        beginBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        rightCol.AddChild(beginBtn);

        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(0, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        backBtn.AddThemeFontSizeOverride("font_size", 18);
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.09f, 0.11f);
        backBtn.Pressed      += OnBack;
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        rightCol.AddChild(backBtn);

        UpdateIntroLine();
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

    private void PopulateStageList()
    {
        if (_stageListContainer == null) return;
        foreach (var child in _stageListContainer.GetChildren())
            child.QueueFree();

        for (int i = 0; i < _stages.Count; i++)
        {
            var stage = _stages[i];
            bool available = CampaignProgress.IsAvailable(stage.StageIndex);
            bool selected  = i == _selectedIndex;
            _stageListContainer.AddChild(CreateStageCard(stage, available, selected, i));
        }
    }

    private Control CreateStageCard(CampaignStageDefinition stage, bool available, bool selected, int listIndex)
    {
        bool easy   = CampaignProgress.IsCleared(stage.StageIndex, DifficultyMode.Easy);
        bool normal = CampaignProgress.IsCleared(stage.StageIndex, DifficultyMode.Normal);
        bool hard   = CampaignProgress.IsCleared(stage.StageIndex, DifficultyMode.Hard);

        var container = new PanelContainer();
        container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        container.SetMeta("stage_index", listIndex);
        ApplyStageCardStyle(container, selected, available);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 10);
        hbox.AddThemeConstantOverride("margin_left", 10);
        hbox.AddThemeConstantOverride("margin_top", 9);
        hbox.AddThemeConstantOverride("margin_right", 10);
        hbox.AddThemeConstantOverride("margin_bottom", 9);
        container.AddChild(hbox);

        // Number badge or lock
        var badge = new Label
        {
            Text = available
                ? $"0{stage.StageIndex + 1}"
                : "-",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            CustomMinimumSize   = new Vector2(44f, 52f),
        };
        UITheme.ApplyFont(badge, semiBold: true, size: available ? 22 : 18);
        badge.Modulate = available
            ? (selected ? UITheme.Lime : new Color(0.55f, 0.70f, 0.82f))
            : new Color(0.35f, 0.38f, 0.50f, 0.70f);
        hbox.AddChild(badge);

        // Text column
        var textVbox = new VBoxContainer();
        textVbox.AddThemeConstantOverride("separation", 2);
        textVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(textVbox);

        var nameLabel = new Label { Text = available ? stage.StageName : "LOCKED" };
        UITheme.ApplyFont(nameLabel, semiBold: true, size: 19);
        nameLabel.Modulate = available
            ? (selected ? new Color(0.95f, 0.99f, 0.86f) : new Color(0.90f, 0.95f, 1.00f))
            : new Color(0.40f, 0.43f, 0.56f);
        textVbox.AddChild(nameLabel);

        var subtitleLabel = new Label { Text = available ? stage.StageSubtitle : "Clear previous stage to unlock" };
        subtitleLabel.AddThemeFontSizeOverride("font_size", 12);
        subtitleLabel.Modulate = available
            ? (selected ? new Color(0.78f, 0.84f, 0.70f) : new Color(0.55f, 0.60f, 0.70f))
            : new Color(0.30f, 0.33f, 0.44f);
        textVbox.AddChild(subtitleLabel);

        // Mandate badge (only for available stages with an active mandate)
        if (available && stage.Mandate.IsActive && !string.IsNullOrEmpty(stage.Mandate.DisplayText))
        {
            Color mandateColor = stage.Mandate.Type switch
            {
                MandateType.BannedModifiers or MandateType.BannedTowers => new Color(1.0f, 0.62f, 0.15f, 0.90f),
                MandateType.LockedSlots  => new Color(1.0f, 0.38f, 0.30f, 0.90f),
                MandateType.EnemyHpBonus => new Color(0.80f, 0.40f, 1.00f, 0.90f),
                _ => new Color(0.70f, 0.70f, 0.70f),
            };
            var mandateLabel = new Label { Text = stage.Mandate.DisplayText };
            mandateLabel.AddThemeFontSizeOverride("font_size", 11);
            mandateLabel.Modulate = mandateColor;
            textVbox.AddChild(mandateLabel);
        }

        // Difficulty checkmarks
        if (available && (easy || normal || hard))
        {
            string checks = "";
            if (easy)   checks += "E✓ ";
            if (normal) checks += "N✓ ";
            if (hard)   checks += "H✓";
            var clearLabel = new Label { Text = checks.Trim() };
            clearLabel.AddThemeFontSizeOverride("font_size", 12);
            clearLabel.Modulate = new Color(0.40f, 0.92f, 0.50f, 0.90f);
            textVbox.AddChild(clearLabel);
        }

        if (available)
        {
            container.GuiInput += (InputEvent ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                    SelectStage(listIndex);
            };
        }

        return container;
    }

    private static void ApplyStageCardStyle(PanelContainer container, bool selected, bool available)
    {
        Color bgColor = !available
            ? new Color(0.030f, 0.036f, 0.072f, 0.70f)
            : new Color(0.018f, 0.030f, 0.078f, 0.94f);
        Color borderColor = !available
            ? new Color(0.30f, 0.34f, 0.46f, 0.44f)
            : selected
                ? new Color(0.64f, 0.82f, 0.24f, 0.96f)
                : new Color(0.24f, 0.62f, 0.76f, 0.72f);

        container.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: bgColor, border: borderColor, corners: 9,
            borderWidth: selected ? 2 : 1, padH: 0, padV: 0));
    }

    private void SelectStage(int index)
    {
        if (index < 0 || index >= _stages.Count) return;
        if (!CampaignProgress.IsAvailable(_stages[index].StageIndex)) return;
        _selectedIndex = index;
        SoundManager.Instance?.Play("ui_select");
        PopulateStageList();
        UpdateDifficultyVisuals();
        UpdateIntroLine();
    }

    private void UpdateIntroLine()
    {
        if (_introLineLabel == null || _selectedIndex < 0 || _selectedIndex >= _stages.Count) return;
        var stage = _stages[_selectedIndex];
        _introLineLabel.Text = CampaignProgress.IsAvailable(stage.StageIndex)
            ? $"\"{stage.IntroLine}\""
            : "";
    }

    private void UpdateDifficultyVisuals()
    {
        UpdateDifficultyButton(_easyBtn,   DifficultyMode.Easy,   "Easy");
        UpdateDifficultyButton(_normalBtn, DifficultyMode.Normal, "Normal");
        UpdateDifficultyButton(_hardBtn,   DifficultyMode.Hard,   "Hard");
    }

    private void UpdateDifficultyButton(Button? btn, DifficultyMode mode, string baseLabel)
    {
        if (btn == null) return;
        int stageIndex = _selectedIndex >= 0 && _selectedIndex < _stages.Count
            ? _stages[_selectedIndex].StageIndex : 0;
        bool cleared    = CampaignProgress.IsCleared(stageIndex, mode);
        bool isSelected = _selectedDifficulty == mode;

        btn.Text = cleared ? $"{baseLabel} ✓" : baseLabel;
        Color color = isSelected
            ? new Color(1.0f, 0.85f, 0.25f)
            : cleared
                ? new Color(0.40f, 0.92f, 0.50f)
                : new Color(0.45f, 0.45f, 0.45f);

        btn.AddThemeColorOverride("font_color",         color);
        btn.AddThemeColorOverride("font_hover_color",   color);
        btn.AddThemeColorOverride("font_pressed_color", color);
        btn.AddThemeColorOverride("font_focus_color",   color);
    }

    private Button CreateDifficultyButton(string label, DifficultyMode mode)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(80, 38),
        };
        btn.AddThemeFontSizeOverride("font_size", 16);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.08f, 0.10f);
        btn.Pressed      += () => { _selectedDifficulty = mode; SoundManager.Instance?.Play("ui_select"); UpdateDifficultyVisuals(); };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        return btn;
    }

    private void OnBegin()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _stages.Count) return;
        var stage = _stages[_selectedIndex];
        if (!CampaignProgress.IsAvailable(stage.StageIndex)) return;

        CampaignManager.SetActiveStage(stage);
        MapSelectPanel.SetPendingMapSelection(stage.MapId);
        SettingsManager.Instance?.SetDifficulty(_selectedDifficulty);
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
    }

    private void OnBack()
    {
        CampaignManager.ClearActiveStage();
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/ModeSelect.tscn");
    }
}
