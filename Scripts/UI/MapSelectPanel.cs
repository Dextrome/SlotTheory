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

	private string _selectedMapId = "random_map";  // Default to random
	private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;
	private VBoxContainer? _mapListContainer;
	private Button? _normalButton;
	private Button? _hardButton;
	private Label? _personalBestLabel;
	private ulong _proceduralPreviewSeed;

	public override void _Ready()
	{
		// Reset to default state when MapSelectPanel loads
		// Will be auto-updated to first map in PopulateMapList if maps exist
		_selectedMapId = "random_map";  
		_selectedDifficulty = DifficultyMode.Normal;  // Always default to Normal
		_proceduralPreviewSeed = _pendingProceduralSeed != 0
			? _pendingProceduralSeed
			: (ulong)(System.Environment.TickCount64 & 0x7FFFFFFF);

		var canvas = new CanvasLayer();
		AddChild(canvas);

		// Dark background
		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color("#141420");
		canvas.AddChild(bg);

		// Animated neon grid
		var grid = new NeonGridBg();
		grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		grid.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(grid);

		// Center container
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.Theme = SlotTheory.Core.UITheme.Build();
		canvas.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		center.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "SELECT A MAP",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 60);
		title.Modulate = new Color("#a6d608");
		vbox.AddChild(title);

		AddSpacer(vbox, 12);

		// Main content split: map list on the left, actions on the right.
		var contentRow = new HBoxContainer();
		contentRow.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(contentRow);

		// Left column: map list
		var leftColumn = new VBoxContainer();
		leftColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		leftColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		contentRow.AddChild(leftColumn);

		var scrollContainer = new ScrollContainer();
		scrollContainer.CustomMinimumSize = new Vector2(560, 360);
		scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		leftColumn.AddChild(scrollContainer);

		_mapListContainer = new VBoxContainer();
		_mapListContainer.AddThemeConstantOverride("separation", 12);
		scrollContainer.AddChild(_mapListContainer);
		PopulateMapList();

		// Right column: difficulty + actions
		var rightColumn = new VBoxContainer();
		rightColumn.AddThemeConstantOverride("separation", 14);
		rightColumn.CustomMinimumSize = new Vector2(280, 0);
		rightColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		contentRow.AddChild(rightColumn);

		var difficultyLabel = new Label
		{
			Text = "DIFFICULTY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		SlotTheory.Core.UITheme.ApplyFont(difficultyLabel, semiBold: true, size: 20);
		difficultyLabel.Modulate = new Color("#a6d608");
		rightColumn.AddChild(difficultyLabel);

		var difficultyContainer = new HBoxContainer();
		difficultyContainer.AddThemeConstantOverride("separation", 12);
		rightColumn.AddChild(difficultyContainer);

		_normalButton = CreateDifficultyButton("Normal", DifficultyMode.Normal);
		difficultyContainer.AddChild(_normalButton);

		_hardButton = CreateDifficultyButton("Hard", DifficultyMode.Hard);
		difficultyContainer.AddChild(_hardButton);

		_personalBestLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(260, 64),
			Text = "",
		};
		_personalBestLabel.AddThemeFontSizeOverride("font_size", 15);
		_personalBestLabel.Modulate = new Color(0.74f, 0.88f, 1.00f, 0.95f);
		rightColumn.AddChild(_personalBestLabel);
		UpdatePersonalBestLabel();

		var actionSpacer = new Control();
		actionSpacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		rightColumn.AddChild(actionSpacer);

		AddButton(rightColumn, "Start Run", 260, 58, 28, OnStartRun);
		AddButton(rightColumn, "Back", 260, 48, 22, OnBack);
		MobileOptimization.ApplyUIScale(center);
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
			
			// Auto-select the first map if still on default "random_map"
			if (_selectedMapId == "random_map" && maps.Any())
			{
				_selectedMapId = maps.First().Id;
			}
			
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
				var mapButton = CreateMapButton(mapDef.Id, mapName, mapDesc);
				_mapListContainer.AddChild(mapButton);
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
		container.CustomMinimumSize = new Vector2(480, 0);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 12);
		hbox.AddThemeConstantOverride("margin_left", 12);
		hbox.AddThemeConstantOverride("margin_top", 12);
		hbox.AddThemeConstantOverride("margin_right", 12);
		hbox.AddThemeConstantOverride("margin_bottom", 12);
		container.AddChild(hbox);

		// Select button on the left
		var btn = new Button
		{
			Text = "SELECT",
			CustomMinimumSize = new Vector2(80, 60),
		};
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.Pressed += () => SelectMap(mapId);
		btn.MouseEntered += () => SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
		hbox.AddChild(btn);

		// Text content on the right
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 2);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(vbox);

		// Map name
		var nameLabel = new Label
		{
			Text = mapName,
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		SlotTheory.Core.UITheme.ApplyFont(nameLabel, semiBold: true, size: 22);
		vbox.AddChild(nameLabel);

		// Description
		var descLabel = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(340, 0),
			AutowrapMode = TextServer.AutowrapMode.Word,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 14);
		descLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
		vbox.AddChild(descLabel);

		// Highlight if selected - much brighter
		if (mapId == _selectedMapId)
		{
			container.Modulate = new Color(1.5f, 1.4f, 0.8f);  // Bright gold highlight
		}

		return container;
	}

	private void SelectMap(string mapId)
	{
		_selectedMapId = mapId;
		SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
		UpdatePersonalBestLabel();

		// Rebuild map list to show selection highlight
		if (_mapListContainer != null)
		{
			// Clear existing buttons by removing them from the container
			var childCount = _mapListContainer.GetChildCount();
			for (int i = childCount - 1; i >= 0; i--)
			{
				_mapListContainer.RemoveChild(_mapListContainer.GetChild(i));
			}

			// Repopulate with updated selections
			PopulateMapList();
		}
	}

	private void OnStartRun()
	{
		// Store selected map and difficulty globally
		_pendingMapSelection = _selectedMapId;
		_pendingProceduralSeed = _selectedMapId == "random_map" ? _proceduralPreviewSeed : 0;
		SlotTheory.Core.SettingsManager.Instance?.SetDifficulty(_selectedDifficulty);
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private void OnBack()
	{
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}

	private static void AddSpacer(VBoxContainer vbox, int px)
	{
		var s = new Control { CustomMinimumSize = new Vector2(0, px) };
		vbox.AddChild(s);
	}

	private static void AddButton(VBoxContainer vbox, string text,
		int minW, int minH, int fontSize, System.Action callback)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(minW, minH),
		};
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		btn.Pressed += callback;
		btn.MouseEntered += () => SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
		vbox.AddChild(btn);
	}

	private Button CreateDifficultyButton(string text, DifficultyMode difficulty)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(120, 40),
			ToggleMode = true,
			ButtonPressed = difficulty == _selectedDifficulty
		};
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.Pressed += () => {
			// Always select this difficulty when pressed (prevents toggling off)
			SelectDifficulty(difficulty);
		};
		btn.MouseEntered += () => SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
		return btn;
	}

	private void SelectDifficulty(DifficultyMode difficulty)
	{
		_selectedDifficulty = difficulty;
		SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
		UpdatePersonalBestLabel();

		// Update button states to ensure only one is pressed
		if (_normalButton != null)
			_normalButton.ButtonPressed = (difficulty == DifficultyMode.Normal);
		if (_hardButton != null)
			_hardButton.ButtonPressed = (difficulty == DifficultyMode.Hard);
	}

	private void UpdatePersonalBestLabel()
	{
		if (_personalBestLabel == null) return;
		var best = SlotTheory.Core.HighScoreManager.Instance?.GetPersonalBest(_selectedMapId, _selectedDifficulty);
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
