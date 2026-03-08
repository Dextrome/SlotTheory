using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen map selection panel shown before a run starts.
/// Player selects a map, which is stored globally before proceeding to Main.tscn.
/// </summary>
public partial class MapSelectPanel : Node
{
	private static string _pendingMapSelection = "random_map";
	private static ulong _pendingProceduralSeed = 0;
	public static string PendingMapSelection => _pendingMapSelection;
	public static ulong PendingProceduralSeed => _pendingProceduralSeed;
	public static void SetPendingMapSelection(string mapId) => _pendingMapSelection = mapId;

	private string _selectedMapId = "random_map";
	private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;
	private VBoxContainer? _mapListContainer;
	private Button? _normalButton;
	private Button? _hardButton;
	private Label? _personalBestLabel;
	private ulong _proceduralPreviewSeed;

	public override void _Ready()
	{
		_selectedMapId = "random_map";
		_selectedDifficulty = DifficultyMode.Normal;
		_proceduralPreviewSeed = _pendingProceduralSeed != 0
			? _pendingProceduralSeed
			: (ulong)(System.Environment.TickCount64 & 0x7FFFFFFF);

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

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		center.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "SELECT A MAP",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(title, semiBold: true, size: 42);
		title.Modulate = new Color("#a6d608");
		vbox.AddChild(title);

		// Content row: map list | difficulty
		var contentRow = new HBoxContainer();
		contentRow.AddThemeConstantOverride("separation", 24);
		vbox.AddChild(contentRow);

		// Left column: map list
		var leftColumn = new VBoxContainer();
		leftColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		contentRow.AddChild(leftColumn);

		var scrollContainer = new ScrollContainer();
		scrollContainer.CustomMinimumSize = new Vector2(420, 200);
		scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scrollContainer.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
		scrollContainer.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
		TouchScrollHelper.EnableDragScroll(scrollContainer);
		leftColumn.AddChild(scrollContainer);

		_mapListContainer = new VBoxContainer();
		_mapListContainer.AddThemeConstantOverride("separation", 12);
		_mapListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollContainer.AddChild(_mapListContainer);
		PopulateMapList();

		// Right column: difficulty + personal best
		var rightColumn = new VBoxContainer();
		rightColumn.AddThemeConstantOverride("separation", 14);
		rightColumn.CustomMinimumSize = new Vector2(240, 0);
		contentRow.AddChild(rightColumn);

		var difficultyLabel = new Label
		{
			Text = "DIFFICULTY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(difficultyLabel, semiBold: true, size: 20);
		difficultyLabel.Modulate = new Color("#a6d608");
		rightColumn.AddChild(difficultyLabel);

		var difficultyContainer = new HBoxContainer();
		difficultyContainer.AddThemeConstantOverride("separation", 12);
		rightColumn.AddChild(difficultyContainer);

		_normalButton = CreateDifficultyButton("Normal", DifficultyMode.Normal);
		difficultyContainer.AddChild(_normalButton);
		_hardButton = CreateDifficultyButton("Hard", DifficultyMode.Hard);
		difficultyContainer.AddChild(_hardButton);
		UpdateDifficultyVisuals();

		_personalBestLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(220, 0),
			Text = "",
		};
		_personalBestLabel.AddThemeFontSizeOverride("font_size", 15);
		_personalBestLabel.Modulate = new Color(0.74f, 0.88f, 1.00f, 0.95f);
		rightColumn.AddChild(_personalBestLabel);
		UpdatePersonalBestLabel();

		// Start Run + Back in the right column — always visible, no layout tricks needed
		var startBtn = new Button
		{
			Text = "Start Run",
			CustomMinimumSize = new Vector2(0, 54),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		startBtn.AddThemeFontSizeOverride("font_size", 24);
		startBtn.Pressed      += OnStartRun;
		startBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		rightColumn.AddChild(startBtn);

		var backBtn = new Button
		{
			Text = "Back",
			CustomMinimumSize = new Vector2(0, 44),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		backBtn.AddThemeFontSizeOverride("font_size", 20);
		backBtn.Pressed      += OnBack;
		backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		rightColumn.AddChild(backBtn);

		AddChild(new PinchZoomHandler(center));
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

	private void PopulateMapList()
	{
		if (_mapListContainer == null) return;

		try
		{
			var maps = DataLoader.GetAllMapDefs();
			if (!maps.Any())
			{
				GD.PrintErr("MapSelectPanel: No maps loaded in DataLoader");
				return;
			}

			if (_selectedMapId == "random_map" && maps.Any())
				_selectedMapId = maps.First().Id;

			foreach (var mapDef in maps)
			{
				string mapName = mapDef.Name;
				string mapDesc = mapDef.Description;
				if (mapDef.IsRandom || mapDef.Id == "random_map")
				{
					string seedName = MapGenerator.DescribeSeed((int)_proceduralPreviewSeed);
					mapName = $"MAP: {seedName}";
					mapDesc = $"Procedural seed {_proceduralPreviewSeed}  |  Different flow each run.";
				}
				_mapListContainer.AddChild(CreateMapButton(mapDef.Id, mapName, mapDesc));
			}
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"MapSelectPanel.PopulateMapList() error: {ex.Message}");
		}
	}

	private Control CreateMapButton(string mapId, string mapName, string description)
	{
		var container = new PanelContainer();
		container.ThemeTypeVariation = "NoVisualHBox";
		container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation",    10);
		hbox.AddThemeConstantOverride("margin_left",   10);
		hbox.AddThemeConstantOverride("margin_top",    10);
		hbox.AddThemeConstantOverride("margin_right",  10);
		hbox.AddThemeConstantOverride("margin_bottom", 10);
		container.AddChild(hbox);

		var btn = new Button
		{
			Text = "SELECT",
			CustomMinimumSize = new Vector2(80, 52),
		};
		btn.AddThemeFontSizeOverride("font_size", 15);
		btn.Pressed      += () => SelectMap(mapId);
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		hbox.AddChild(btn);

		var textVbox = new VBoxContainer();
		textVbox.AddThemeConstantOverride("separation", 2);
		textVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(textVbox);

		var nameLabel = new Label
		{
			Text = mapName,
			HorizontalAlignment = HorizontalAlignment.Left,
			ClipText = false,
		};
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 20);
		textVbox.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.Word,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 13);
		descLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
		textVbox.AddChild(descLabel);

		if (mapId == _selectedMapId)
			container.Modulate = new Color(1.5f, 1.4f, 0.8f);

		return container;
	}

	private void SelectMap(string mapId)
	{
		_selectedMapId = mapId;
		SoundManager.Instance?.Play("ui_select");
		UpdatePersonalBestLabel();

		if (_mapListContainer != null)
		{
			for (int i = _mapListContainer.GetChildCount() - 1; i >= 0; i--)
				_mapListContainer.RemoveChild(_mapListContainer.GetChild(i));
			PopulateMapList();
		}
	}

	private void OnStartRun()
	{
		_pendingMapSelection = _selectedMapId;
		_pendingProceduralSeed = _selectedMapId == "random_map" ? _proceduralPreviewSeed : 0;
		SettingsManager.Instance?.SetDifficulty(_selectedDifficulty);
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private void OnBack()
	{
		Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}

	private static void AddButton(Node parent, string text,
		int minW, int minH, int fontSize, System.Action callback)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(minW, minH),
		};
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		btn.Pressed      += callback;
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		parent.AddChild(btn);
	}

	private Button CreateDifficultyButton(string text, DifficultyMode difficulty)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(114, 40),
			ToggleMode = false,
		};
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.Pressed      += () => SelectDifficulty(difficulty);
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		return btn;
	}

	private void SelectDifficulty(DifficultyMode difficulty)
	{
		_selectedDifficulty = difficulty;
		SoundManager.Instance?.Play("ui_select");
		UpdatePersonalBestLabel();
		UpdateDifficultyVisuals();
	}

	private void UpdateDifficultyVisuals()
	{
		var selected   = new Color(1.0f, 0.85f, 0.25f); // gold
		var unselected = new Color(0.55f, 0.55f, 0.55f); // grey

		if (_normalButton != null)
		{
			var c = _selectedDifficulty == DifficultyMode.Normal ? selected : unselected;
			_normalButton.AddThemeColorOverride("font_color",         c);
			_normalButton.AddThemeColorOverride("font_hover_color",   c);
			_normalButton.AddThemeColorOverride("font_pressed_color", c);
			_normalButton.AddThemeColorOverride("font_focus_color",   c);
		}
		if (_hardButton != null)
		{
			var c = _selectedDifficulty == DifficultyMode.Hard ? selected : unselected;
			_hardButton.AddThemeColorOverride("font_color",         c);
			_hardButton.AddThemeColorOverride("font_hover_color",   c);
			_hardButton.AddThemeColorOverride("font_pressed_color", c);
			_hardButton.AddThemeColorOverride("font_focus_color",   c);
		}
	}

	private void UpdatePersonalBestLabel()
	{
		if (_personalBestLabel == null) return;
		var best = HighScoreManager.Instance?.GetPersonalBest(_selectedMapId, _selectedDifficulty);
		if (best == null)
		{
			_personalBestLabel.Text = "Personal Best: --";
			return;
		}

		string diff = _selectedDifficulty == DifficultyMode.Hard ? "Hard" : "Normal";
		_personalBestLabel.Text =
			$"Personal Best ({diff}): {best.Score:N0}\n" +
			$"Wave {best.WaveReached}/{Balance.TotalWaves}  |  Lives {best.LivesRemaining}";
	}
}
