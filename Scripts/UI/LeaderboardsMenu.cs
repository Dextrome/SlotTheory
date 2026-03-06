using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using SlotTheory.Data;

namespace SlotTheory.UI;

public partial class LeaderboardsMenu : Node
{
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
    private RichTextLabel _entries = null!;
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

        _entries = new RichTextLabel
        {
            BbcodeEnabled = false,
            ScrollActive = true,
            FitContent = false,
            CustomMinimumSize = new Vector2(900f, 380f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _entries.AddThemeFontOverride("normal_font", UITheme.SemiBold);
        _entries.AddThemeFontSizeOverride("normal_font_size", 20);
        frame.AddChild(_entries);

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
        _entries.Clear();

        string diff = _selectedDifficulty == DifficultyMode.Hard ? "Hard" : "Normal";
        string mapName = _maps.FirstOrDefault(m => m.Id == _selectedMapId)?.Name ?? _selectedMapId;
        _status.Text = $"{(_mode == BoardMode.Local ? "Local" : "Global")} - {mapName} - {diff}";

        IReadOnlyList<LeaderboardEntryView> rows;
        if (_mode == BoardMode.Local)
        {
            rows = HighScoreManager.Instance?.GetLocalEntries(_selectedMapId, _selectedDifficulty, 20)
                ?? System.Array.Empty<LeaderboardEntryView>();
            if (token != _refreshToken) return;
            RenderRows(rows);
            if (rows.Count == 0)
                _status.Text += "  |  No local scores yet";
            return;
        }

        if (!LeaderboardKey.IsGlobalEligibleMap(_selectedMapId))
        {
            _entries.Text = "Global leaderboard not available for Random Map.";
            return;
        }

        _entries.Text = "Loading global leaderboard...";
        rows = await (LeaderboardManager.Instance?.GetTopEntriesAsync(_selectedMapId, _selectedDifficulty, 20)
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
            _entries.Text = "--";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("RANK  NAME                 SCORE        WAVE  LIVES  KILLS   TIME");
        sb.AppendLine("------------------------------------------------------------------");
        foreach (var row in rows)
        {
            string name = Truncate(row.Name, 18).PadRight(18);
            string line = $"{row.Rank,4}  {name}  {row.Score,10:N0}  {row.WaveReached,4}/{Balance.TotalWaves,-2}  {row.LivesRemaining,5}  {row.TotalKills,5}  {FormatTime(row.TimeSeconds),6}";
            sb.AppendLine(line);
        }
        _entries.Text = sb.ToString();
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
        return text[..(maxLen - 1)] + "…";
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

    private void OnBack()
    {
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }
}
