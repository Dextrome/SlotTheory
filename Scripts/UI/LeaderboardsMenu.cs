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
    private const int DisplayEntryLimit = 10;

    private static string? _pendingMapId;
    private static DifficultyMode? _pendingDifficulty;
    private static bool _pendingPreferGlobal;

    public static void SetPendingContext(string mapId, DifficultyMode difficulty, bool preferGlobal)
    {
        _pendingMapId = mapId;
        _pendingDifficulty = difficulty;
        _pendingPreferGlobal = preferGlobal;
    }

    private enum BoardMode
    {
        Local,
        Global
    }

    private BoardMode _mode = BoardMode.Local;
    private readonly List<MapDef> _maps = new();
    private string _selectedMapId = "arena_classic";
    private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;
    private int _refreshToken;

    private Button _localButton = null!;
    private Button _globalButton = null!;
    private OptionButton _mapOption = null!;
    private OptionButton _difficultyOption = null!;
    private ScrollContainer _entriesScroll = null!;
    private VBoxContainer _entryList = null!;
    private Label _status = null!;

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
        frame.AddThemeConstantOverride("separation", 14);
        center.AddChild(frame);

        var title = new Label
        {
            Text = "LEADERBOARDS",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(title, semiBold: true, size: 56);
        title.Modulate = new Color("#a6d608");
        frame.AddChild(title);

        var modeRow = new HBoxContainer();
        modeRow.Alignment = BoxContainer.AlignmentMode.Center;
        modeRow.AddThemeConstantOverride("separation", 10);
        frame.AddChild(modeRow);

        _localButton = MakeButton("Local", 140, 40, 20, OnLocalMode);
        _globalButton = MakeButton("Global", 140, 40, 20, OnGlobalMode);
        modeRow.AddChild(_localButton);
        modeRow.AddChild(_globalButton);

        var filterRow = new HBoxContainer();
        filterRow.Alignment = BoxContainer.AlignmentMode.Center;
        filterRow.AddThemeConstantOverride("separation", 10);
        frame.AddChild(filterRow);

        _mapOption = new OptionButton { CustomMinimumSize = new Vector2(360, 40) };
        _mapOption.AddThemeFontSizeOverride("font_size", 18);
        filterRow.AddChild(_mapOption);

        foreach (var map in _maps)
            _mapOption.AddItem(map.Name);
        _mapOption.ItemSelected += OnMapSelected;

        _difficultyOption = new OptionButton { CustomMinimumSize = new Vector2(180, 40) };
        _difficultyOption.AddThemeFontSizeOverride("font_size", 18);
        _difficultyOption.AddItem("Normal");
        _difficultyOption.AddItem("Hard");
        _difficultyOption.ItemSelected += OnDifficultySelected;
        filterRow.AddChild(_difficultyOption);

        _status = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Text = "",
            Modulate = new Color(0.90f, 0.90f, 0.92f, 0.86f),
        };
        _status.AddThemeFontSizeOverride("font_size", 16);
        frame.AddChild(_status);

        _entriesScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(900f, 380f),
        };
        _entriesScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        _entriesScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(_entriesScroll);
        frame.AddChild(_entriesScroll);

        _entryList = new VBoxContainer();
        _entryList.AddThemeConstantOverride("separation", 10);
        _entryList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _entriesScroll.AddChild(_entryList);

        var footer = new HBoxContainer();
        footer.Alignment = BoxContainer.AlignmentMode.Center;
        footer.AddThemeConstantOverride("separation", 16);
        frame.AddChild(footer);

        var refresh = MakeButton("Refresh", 180, 42, 20, () => _ = RefreshBoardAsync());
        var back = MakeButton("Back", 180, 42, 20, OnBack);
        footer.AddChild(refresh);
        footer.AddChild(back);

        if (_maps.Count > 0)
        {
            int selectedIndex = _maps.FindIndex(m => m.Id == _selectedMapId);
            _mapOption.Selected = selectedIndex >= 0 ? selectedIndex : 0;
        }
        _difficultyOption.Selected = _selectedDifficulty == DifficultyMode.Hard ? 1 : 0;
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
    }

    private void OnMapSelected(long index)
    {
        int i = (int)index;
        if (i < 0 || i >= _maps.Count) return;
        _selectedMapId = _maps[i].Id;
        _ = RefreshBoardAsync();
    }

    private void OnDifficultySelected(long index)
    {
        _selectedDifficulty = index == 1 ? DifficultyMode.Hard : DifficultyMode.Normal;
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
        _localButton.Disabled = local;
        _globalButton.Disabled = !local;
    }

    private async System.Threading.Tasks.Task RefreshBoardAsync()
    {
        int token = ++_refreshToken;
        ClearRows();

        string diff = _selectedDifficulty == DifficultyMode.Hard ? "Hard" : "Normal";
        string mapName = _maps.FirstOrDefault(m => m.Id == _selectedMapId)?.Name ?? _selectedMapId;
        _status.Text = $"{(_mode == BoardMode.Local ? "Local" : "Global")} - {mapName} - {diff}";

        IReadOnlyList<LeaderboardEntryView> rows;
        if (_mode == BoardMode.Local)
        {
            rows = HighScoreManager.Instance?.GetLocalEntries(_selectedMapId, _selectedDifficulty, DisplayEntryLimit)
                ?? System.Array.Empty<LeaderboardEntryView>();
            if (token != _refreshToken) return;
            RenderRows(rows);
            if (rows.Count == 0)
                _status.Text += "  |  No local scores yet";
            return;
        }

        if (!LeaderboardKey.IsGlobalEligibleMap(_selectedMapId))
        {
            AddMessageRow("Global leaderboard not available for Random Map.");
            return;
        }

        AddMessageRow("Loading global leaderboard...");
        rows = await (LeaderboardManager.Instance?.GetTopEntriesAsync(_selectedMapId, _selectedDifficulty, DisplayEntryLimit)
            ?? System.Threading.Tasks.Task.FromResult<IReadOnlyList<LeaderboardEntryView>>(System.Array.Empty<LeaderboardEntryView>()));
        if (token != _refreshToken) return;
        RenderRows(rows);
        if (rows.Count == 0)
            _status.Text += "  |  No global entries yet (or service unavailable)";
    }

    private void RenderRows(IReadOnlyList<LeaderboardEntryView> rows)
    {
        if (rows.Count == 0)
        {
            AddMessageRow("--");
            return;
        }

        foreach (var row in rows)
        {
            _entryList.AddChild(BuildEntryCard(row));
        }
    }

    private PanelContainer BuildEntryCard(LeaderboardEntryView row)
    {
        var panel = new PanelContainer();
        panel.ThemeTypeVariation = "NoVisualHBox";
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(880f, 84f);

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
            Text = $"#{row.Rank}  {Truncate(row.Name, 18)}  |  {row.Score:N0}  |  W {row.WaveReached}/{Balance.TotalWaves}  |  L {row.LivesRemaining}  |  K {row.TotalKills}  |  {FormatTime(row.TimeSeconds)}",
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
        if (string.IsNullOrEmpty(text))
            return "";
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

            // Slight right offset so mod clusters sit more centered under tower icons.
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
                        IconColor = ModifierVisuals.GetAccent(mod),
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

    private static Button MakeButton(string text, int width, int height, int fontSize, System.Action callback)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, height),
        };
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        btn.Pressed += callback;
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
