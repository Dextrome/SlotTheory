using System.Collections.Generic;
using System.Text;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using SlotTheory.Core.Naming;
using SlotTheory.Data;

namespace SlotTheory.UI;

public partial class LeaderboardsMenu : Node
{
    private const int PageSize        = 10;
    private const int LocalFetchLimit  = 1000;
    private const int GlobalFetchLimit = 100;

    private static string? _pendingMapId;
    private static DifficultyMode? _pendingDifficulty;
    private static bool _pendingPreferGlobal;

    public static void SetPendingContext(string mapId, DifficultyMode difficulty, bool preferGlobal)
    {
        _pendingMapId = mapId;
        _pendingDifficulty = difficulty;
        _pendingPreferGlobal = preferGlobal;
    }

    private enum BoardMode { Local, Global }

    private BoardMode _mode = BoardMode.Local;
    private readonly List<MapDef> _maps = new();
    private string _selectedMapId = "arena_classic";
    private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;
    private int _refreshToken;

    // Pagination state
    private int _currentPage;
    private readonly List<LeaderboardEntryView> _allEntries = new();

    // UI refs
    private Button _localButton  = null!;
    private Button _globalButton = null!;
    private OptionButton _mapOption        = null!;
    private OptionButton _difficultyOption = null!;
    private VBoxContainer _entryList = null!;
    private Label _status    = null!;
    private Label _pageLabel = null!;
    private Button _firstBtn = null!;
    private Button _prevBtn  = null!;
    private Button _nextBtn  = null!;
    private Button _lastBtn  = null!;

    public override void _Ready()
    {
        DataLoader.LoadAll();
        _maps.Clear();
        _maps.AddRange(DataLoader.GetAllMapDefs());
        if (_maps.Count > 0)
            _selectedMapId = _maps[0].Id;

        if (!string.IsNullOrEmpty(_pendingMapId) && _maps.Any(m => m.Id == _pendingMapId))
            _selectedMapId = _pendingMapId!;
        if (_pendingDifficulty.HasValue)
            _selectedDifficulty = _pendingDifficulty.Value;
        _mode = _pendingPreferGlobal ? BoardMode.Global : BoardMode.Local;
        _pendingMapId = null;
        _pendingDifficulty = null;
        _pendingPreferGlobal = false;

        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#141420");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = UITheme.Build();
        canvas.AddChild(center);

        var frame = new VBoxContainer();
        frame.CustomMinimumSize = new Vector2(940f, 560f);
        frame.AddThemeConstantOverride("separation", 10);
        frame.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        frame.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        center.AddChild(frame);

        var title = new Label
        {
            Text = "LEADERBOARDS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(title, semiBold: true, size: 56);
        title.Modulate = new Color("#a6d608");
        frame.AddChild(title);

        var bodyPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(940f, 490f),
        };
        UITheme.ApplyGlassChassisPanel(
            bodyPanel,
            bg: new Color(0.040f, 0.052f, 0.105f, 0.94f),
            accent: new Color(0.40f, 0.78f, 0.94f, 0.92f),
            corners: 12,
            borderWidth: 2,
            padH: 14,
            padV: 12,
            sideEmitters: true,
            emitterIntensity: 0.84f);
        frame.AddChild(bodyPanel);

        var body = new VBoxContainer();
        body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 10);
        bodyPanel.AddChild(body);

        // ── Mode row ──────────────────────────────────────────────────────────
        var modeRow = new HBoxContainer();
        modeRow.Alignment = BoxContainer.AlignmentMode.Center;
        modeRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(modeRow);

        _localButton  = MakeButton("Local",  140, 40, 20, OnLocalMode);
        _globalButton = MakeButton("Global", 140, 40, 20, OnGlobalMode);
        modeRow.AddChild(_localButton);
        modeRow.AddChild(_globalButton);

        // ── Filter row ────────────────────────────────────────────────────────
        var filterRow = new HBoxContainer();
        filterRow.Alignment = BoxContainer.AlignmentMode.Center;
        filterRow.AddThemeConstantOverride("separation", 10);
        body.AddChild(filterRow);

        _mapOption = new OptionButton { CustomMinimumSize = new Vector2(360, 40) };
        _mapOption.AddThemeFontSizeOverride("font_size", 18);
        filterRow.AddChild(_mapOption);

        foreach (var map in _maps)
            _mapOption.AddItem(map.Name);
        _mapOption.ItemSelected += OnMapSelected;

        _difficultyOption = new OptionButton { CustomMinimumSize = new Vector2(220, 40) };
        _difficultyOption.AddThemeFontSizeOverride("font_size", 18);
        _difficultyOption.AddItem("Easy");
        _difficultyOption.AddItem("Normal");
        _difficultyOption.AddItem("Hard");
        _difficultyOption.ItemSelected += OnDifficultySelected;
        filterRow.AddChild(_difficultyOption);

        // ── Status ────────────────────────────────────────────────────────────
        _status = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "",
            Modulate = new Color(0.90f, 0.90f, 0.92f, 0.86f),
        };
        _status.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(_status);

        // ── Entry list ────────────────────────────────────────────────────────
        var entriesScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(900f, 380f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        entriesScroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        entriesScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(entriesScroll);
        body.AddChild(entriesScroll);

        _entryList = new VBoxContainer();
        _entryList.AddThemeConstantOverride("separation", 10);
        _entryList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        entriesScroll.AddChild(_entryList);

        // ── Pagination row ────────────────────────────────────────────────────
        var pageRow = new HBoxContainer();
        pageRow.Alignment = BoxContainer.AlignmentMode.Center;
        pageRow.AddThemeConstantOverride("separation", 8);
        body.AddChild(pageRow);

        _firstBtn = MakePageButton("|<", () => GoToPage(0));
        _prevBtn  = MakePageButton("<",  () => GoToPage(_currentPage - 1));
        _nextBtn  = MakePageButton(">",  () => GoToPage(_currentPage + 1));
        _lastBtn  = MakePageButton(">|", () => GoToPage(TotalPages - 1));

        _pageLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(120f, 0f),
        };
        _pageLabel.AddThemeFontSizeOverride("font_size", 16);
        _pageLabel.Modulate = new Color(0.80f, 0.80f, 0.85f);

        pageRow.AddChild(_firstBtn);
        pageRow.AddChild(_prevBtn);
        pageRow.AddChild(_pageLabel);
        pageRow.AddChild(_nextBtn);
        pageRow.AddChild(_lastBtn);

        // ── Footer ────────────────────────────────────────────────────────────
        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        footer.AddThemeConstantOverride("separation", 16);
        body.AddChild(footer);

        var refresh = MakeButton("Refresh", 180, 42, 20, () => _ = RefreshBoardAsync());
        var back    = MakeButton("Back",    180, 42, 20, OnBack);
        footer.AddChild(refresh);
        footer.AddChild(back);

        // ── Init selections ───────────────────────────────────────────────────
        if (_maps.Count > 0)
        {
            int selectedIndex = _maps.FindIndex(m => m.Id == _selectedMapId);
            _mapOption.Selected = selectedIndex >= 0 ? selectedIndex : 0;
        }
        _difficultyOption.Selected = DifficultyToOptionIndex(_selectedDifficulty);
        UpdateModeButtons();

        if (_mode == BoardMode.Global && _selectedMapId == LeaderboardKey.RandomMapId)
        {
            var replacement = _maps.FirstOrDefault(m => m.Id != LeaderboardKey.RandomMapId);
            if (replacement != null)
            {
                int idx = _maps.FindIndex(m => m.Id == replacement.Id);
                if (idx >= 0) _mapOption.Selected = idx;
                _selectedMapId = replacement.Id;
            }
        }
        _ = RefreshBoardAsync();
        AddChild(new PinchZoomHandler(center));
    }

    // ── Pagination helpers ────────────────────────────────────────────────────

    private int TotalPages => _allEntries.Count == 0 ? 1
        : (int)System.Math.Ceiling(_allEntries.Count / (double)PageSize);

    private void GoToPage(int page)
    {
        _currentPage = System.Math.Clamp(page, 0, TotalPages - 1);
        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        ClearRows();
        var slice = _allEntries
            .Skip(_currentPage * PageSize)
            .Take(PageSize)
            .ToList();

        if (slice.Count == 0)
            AddMessageRow("--");
        else
            foreach (var row in slice)
                _entryList.AddChild(BuildEntryCard(row));

        UpdatePaginationControls();
    }

    private void UpdatePaginationControls()
    {
        bool hasMultiplePages = TotalPages > 1;
        _pageLabel.Text = hasMultiplePages ? $"Page {_currentPage + 1} / {TotalPages}" : "";
        _firstBtn.Disabled = !hasMultiplePages || _currentPage == 0;
        _prevBtn.Disabled  = !hasMultiplePages || _currentPage == 0;
        _nextBtn.Disabled  = !hasMultiplePages || _currentPage >= TotalPages - 1;
        _lastBtn.Disabled  = !hasMultiplePages || _currentPage >= TotalPages - 1;
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    private void OnMapSelected(long index)
    {
        int i = (int)index;
        if (i < 0 || i >= _maps.Count) return;
        _selectedMapId = _maps[i].Id;
        _ = RefreshBoardAsync();
    }

    private void OnDifficultySelected(long index)
    {
        _selectedDifficulty = OptionIndexToDifficulty(index);
        _ = RefreshBoardAsync();
    }

    private void OnLocalMode()
    {
        _mode = BoardMode.Local;
        UpdateModeButtons();
        _ = RefreshBoardAsync();
    }

    private void OnGlobalMode()
    {
        _mode = BoardMode.Global;
        if (_selectedMapId == LeaderboardKey.RandomMapId)
        {
            var replacement = _maps.FirstOrDefault(m => m.Id != LeaderboardKey.RandomMapId);
            if (replacement != null)
            {
                int idx = _maps.FindIndex(m => m.Id == replacement.Id);
                if (idx >= 0) _mapOption.Selected = idx;
                _selectedMapId = replacement.Id;
            }
        }
        UpdateModeButtons();
        _ = RefreshBoardAsync();
    }

    private void UpdateModeButtons()
    {
        bool local = _mode == BoardMode.Local;
        _localButton.Disabled  = local;
        _globalButton.Disabled = !local;
    }

    private async System.Threading.Tasks.Task RefreshBoardAsync()
    {
        int token = ++_refreshToken;
        _allEntries.Clear();
        _currentPage = 0;
        ClearRows();
        UpdatePaginationControls();

        string diff    = DifficultyToLabel(_selectedDifficulty);
        string mapName = _maps.FirstOrDefault(m => m.Id == _selectedMapId)?.Name ?? _selectedMapId;
        _status.Text = $"{(_mode == BoardMode.Local ? "Local" : "Global")} - {mapName} - {diff}";

        IReadOnlyList<LeaderboardEntryView> rows;
        if (_mode == BoardMode.Local)
        {
            rows = HighScoreManager.Instance?.GetLocalEntries(_selectedMapId, _selectedDifficulty, LocalFetchLimit)
                ?? System.Array.Empty<LeaderboardEntryView>();
            if (token != _refreshToken) return;
            _allEntries.AddRange(rows);
            if (_allEntries.Count == 0)
                _status.Text += "  |  No local scores yet";
            RenderCurrentPage();
            return;
        }

        if (!LeaderboardKey.IsGlobalEligibleMap(_selectedMapId))
        {
            AddMessageRow("Global leaderboard not available for Random Map.");
            UpdatePaginationControls();
            return;
        }

        AddMessageRow("Loading global leaderboard...");
        rows = await (LeaderboardManager.Instance?.GetTopEntriesAsync(_selectedMapId, _selectedDifficulty, GlobalFetchLimit)
            ?? System.Threading.Tasks.Task.FromResult<IReadOnlyList<LeaderboardEntryView>>(System.Array.Empty<LeaderboardEntryView>()));
        if (token != _refreshToken) return;
        _allEntries.AddRange(rows);
        if (_allEntries.Count == 0)
            _status.Text += "  |  No global entries yet (or service unavailable)";
        RenderCurrentPage();
    }

    // ── Row rendering ─────────────────────────────────────────────────────────

    private PanelContainer BuildEntryCard(LeaderboardEntryView row)
    {
        var panel = new PanelContainer();
        panel.ThemeTypeVariation = "NoVisualHBox";
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(880f, 84f);
        panel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.018f, 0.030f, 0.078f, 0.94f),
            border: new Color(0.24f, 0.62f, 0.76f, 0.74f),
            corners: 9,
            borderWidth: 1,
            padH: 12,
            padV: 10));
        UITheme.AddTopAccent(panel, new Color(0.70f, 0.92f, 1.00f, 0.24f));

        var contentRow = new HBoxContainer();
        contentRow.AddThemeConstantOverride("separation", 12);
        panel.AddChild(contentRow);

        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 6);
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        contentRow.AddChild(leftCol);

        var header = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = $"#{row.Rank}  {Truncate(row.Name, 18)}  |  {row.Score:N0}  |  W {row.WaveReached}/{Balance.TotalWaves}  |  L {row.LivesRemaining}  |  {FormatTime(row.TimeSeconds)}",
            ClipText = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UITheme.ApplyFont(header, semiBold: true, size: 16);
        header.Modulate = new Color(0.92f, 0.96f, 1.00f);
        leftCol.AddChild(header);

        var buildStyle = ResolveBuildNameStyle(row);
        var rightCenter = new CenterContainer
        {
            CustomMinimumSize = new Vector2(360f, 0f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        contentRow.AddChild(rightCenter);

        var buildName = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = false,
            FitContent = true,
            AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(360f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        buildName.AddThemeFontOverride("normal_font", UITheme.SemiBold);
        buildName.AddThemeFontSizeOverride("normal_font_size", 24);
        buildName.AddThemeConstantOverride("line_separation", 0);
        buildName.AppendText(BuildGradientBbCode(
            Truncate(buildStyle.Name, 30),
            buildStyle.StartColor,
            buildStyle.EndColor));
        rightCenter.AddChild(buildName);

        leftCol.AddChild(BuildLoadoutStrip(row.Build));

        return panel;
    }

    private (string Name, Color StartColor, Color EndColor) ResolveBuildNameStyle(LeaderboardEntryView row)
    {
        if (!string.IsNullOrEmpty(row.BuildName))
        {
            var colors = RunNameGenerator.GenerateStyledFromSnapshot(
                _selectedMapId, _selectedDifficulty,
                row.Score, row.WaveReached, row.LivesRemaining,
                row.TotalKills, row.TotalDamageDealt, row.TimeSeconds, row.Build);
            return (row.BuildName, colors.StartColor, colors.EndColor);
        }

        return RunNameGenerator.GenerateStyledFromSnapshot(
            _selectedMapId,
            _selectedDifficulty,
            row.Score,
            row.WaveReached,
            row.LivesRemaining,
            row.TotalKills,
            row.TotalDamageDealt,
            row.TimeSeconds,
            row.Build);
    }

    private static string BuildGradientBbCode(string text, Color start, Color end)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length == 1)
            return $"[color=#{start.ToHtml(false)}]{text}[/color]";

        var sb = new StringBuilder(text.Length * 24);
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

    private Control BuildLoadoutStrip(RunBuildSnapshot build)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.Alignment = BoxContainer.AlignmentMode.Begin;

        for (int i = 0; i < Balance.SlotCount; i++)
        {
            var slot = i < build.Slots.Length
                ? build.Slots[i]
                : new RunSlotBuild("", []);

            var slotBox = new VBoxContainer();
            slotBox.AddThemeConstantOverride("separation", 3);
            slotBox.CustomMinimumSize = new Vector2(54f, 0f);
            row.AddChild(slotBox);

            var towerIcon = new TowerIcon
            {
                TowerId = slot.TowerId,
                CustomMinimumSize = new Vector2(34f, 34f),
                Size = new Vector2(34f, 34f),
            };
            slotBox.AddChild(towerIcon);

            var modsRow = new HBoxContainer();
            modsRow.AddThemeConstantOverride("separation", 2);
            slotBox.AddChild(modsRow);

            modsRow.AddChild(new Control { CustomMinimumSize = new Vector2(5f, 0f) });

            if (slot.ModifierIds.Length == 0)
            {
                var emptyMod = new ColorRect
                {
                    Color = new Color(0.35f, 0.40f, 0.52f, 0.35f),
                    CustomMinimumSize = new Vector2(12f, 12f),
                };
                modsRow.AddChild(emptyMod);
            }
            else
            {
                foreach (var mod in slot.ModifierIds.Take(Balance.MaxModifiersPerTower))
                {
                    var modIcon = new ModifierIcon
                    {
                        ModifierId = mod,
                        IconColor  = ModifierVisuals.GetAccent(mod),
                        CustomMinimumSize = new Vector2(12f, 12f),
                        Size = new Vector2(12f, 12f),
                    };
                    modsRow.AddChild(modIcon);
                }
            }
        }

        return row;
    }

    private void AddMessageRow(string message)
    {
        var label = new Label
        {
            Text = message,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(0.86f, 0.90f, 0.96f, 0.92f),
        };
        UITheme.ApplyFont(label, semiBold: true, size: 20);
        _entryList.AddChild(label);
    }

    private void ClearRows()
    {
        for (int i = _entryList.GetChildCount() - 1; i >= 0; i--)
            _entryList.GetChild(i).QueueFree();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatTime(int seconds)
    {
        if (seconds < 0) seconds = 0;
        int m = seconds / 60;
        int s = seconds % 60;
        return $"{m:00}:{s:00}";
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "Player";
        if (text.Length <= maxLen) return text;
        return maxLen <= 3 ? text[..maxLen] : text[..(maxLen - 3)] + "...";
    }

    private static int DifficultyToOptionIndex(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy   => 0,
        DifficultyMode.Normal => 1,
        DifficultyMode.Hard   => 2,
        _ => 0,
    };

    private static DifficultyMode OptionIndexToDifficulty(long index) => index switch
    {
        0 => DifficultyMode.Easy,
        1 => DifficultyMode.Normal,
        2 => DifficultyMode.Hard,
        _ => DifficultyMode.Easy,
    };

    private static string DifficultyToLabel(DifficultyMode difficulty) => difficulty switch
    {
        DifficultyMode.Easy   => "Easy",
        DifficultyMode.Normal => "Normal",
        DifficultyMode.Hard   => "Hard",
        _ => "Easy",
    };

    private static Button MakeButton(string text, int width, int height, int fontSize, System.Action callback)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, height),
        };
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        bool isPrimary = text == "Refresh";
        if (isPrimary)
        {
            UITheme.ApplyPrimaryStyle(btn);
            UITheme.ApplyMenuButtonFinish(btn, UITheme.Lime, 0.10f, 0.13f);
        }
        else
        {
            UITheme.ApplyCyanStyle(btn);
            UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.09f, 0.11f);
        }
        btn.Pressed      += callback;
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        return btn;
    }

    private static Button MakePageButton(string text, System.Action callback)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(48, 36),
        };
        btn.AddThemeFontSizeOverride("font_size", 17);
        UITheme.ApplyCyanStyle(btn);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.08f, 0.11f);
        btn.Pressed      += () => { SoundManager.Instance?.Play("ui_select"); callback(); };
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        return btn;
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
        {
            SoundManager.Instance?.Play("ui_select");
            Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBack()
    {
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }
}
