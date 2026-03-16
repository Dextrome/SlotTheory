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
	public static void SetPendingProceduralSeed(ulong seed) => _pendingProceduralSeed = seed;

	private string _selectedMapId = "random_map";
	private DifficultyMode _selectedDifficulty = DifficultyMode.Normal;
	private VBoxContainer? _mapListContainer;
	private Button? _easyButton;
	private Button? _normalButton;
	private Button? _hardButton;
	private Label? _personalBestLabel;
	private ulong _proceduralPreviewSeed;
	private bool _isMobile;

	public override void _Ready()
	{
		_selectedMapId = DataLoader.GetAllMapDefs().FirstOrDefault()?.Id ?? "random_map";
		_selectedDifficulty = DifficultyMode.Normal;
		_proceduralPreviewSeed = (ulong)(System.Environment.TickCount64 & 0x7FFFFFFF);

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
		vbox.AddThemeConstantOverride("separation", 10);
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
		contentRow.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(contentRow);

		// Left column: map list
		var leftColumn = new VBoxContainer();
		leftColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		contentRow.AddChild(leftColumn);

		_isMobile = MobileOptimization.IsMobile();
		var scrollContainer = new ScrollContainer();
		scrollContainer.CustomMinimumSize = _isMobile
			? new Vector2(420, 200)
			: new Vector2(760, 260);
		scrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scrollContainer.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
		scrollContainer.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
		TouchScrollHelper.EnableDragScroll(scrollContainer);
		leftColumn.AddChild(scrollContainer);

		var listMargin = new MarginContainer();
		listMargin.AddThemeConstantOverride("margin_left",   6);
		listMargin.AddThemeConstantOverride("margin_right",  6);
		listMargin.AddThemeConstantOverride("margin_top",    4);
		listMargin.AddThemeConstantOverride("margin_bottom", 4);
		listMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollContainer.AddChild(listMargin);

		_mapListContainer = new VBoxContainer();
		_mapListContainer.AddThemeConstantOverride("separation", 12);
		_mapListContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (_isMobile)
			_mapListContainer.MouseFilter = Control.MouseFilterEnum.Pass;
		listMargin.AddChild(_mapListContainer);
		PopulateMapList();

		// Right column: difficulty + personal best
		var rightColumnWrap = new MarginContainer();
		if (!_isMobile)
			rightColumnWrap.AddThemeConstantOverride("margin_top", -32);
		contentRow.AddChild(rightColumnWrap);

		var rightColumn = new VBoxContainer();
		rightColumn.AddThemeConstantOverride("separation", 10);
		rightColumn.CustomMinimumSize = new Vector2(240, 0);
		rightColumnWrap.AddChild(rightColumn);

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

		_easyButton = CreateDifficultyButton("Easy", DifficultyMode.Easy);
		difficultyContainer.AddChild(_easyButton);
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

		// Start Run + Back in the right column - always visible, no layout tricks needed
		var startBtn = new Button
		{
			Text = "Start Run",
			CustomMinimumSize = new Vector2(0, 48),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		startBtn.AddThemeFontSizeOverride("font_size", 24);
		startBtn.Pressed      += OnStartRun;
		startBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		rightColumn.AddChild(startBtn);

		var backBtn = new Button
		{
			Text = "Back",
			CustomMinimumSize = new Vector2(0, 38),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		backBtn.AddThemeFontSizeOverride("font_size", 20);
		backBtn.Pressed      += OnBack;
		backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		rightColumn.AddChild(backBtn);

		MobileOptimization.ApplyUIScale(center);
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

			// Campaign maps and fixed maps first, random map kept last
			var campaignMaps = maps.Where(m => m.Id != "random_map");
			var randomMap    = maps.FirstOrDefault(m => m.Id == "random_map");

			foreach (var mapDef in campaignMaps)
				_mapListContainer.AddChild(CreateMapButton(mapDef.Id, mapDef.Name, mapDef.Description));

			// Full-game placeholder slots - visible but unplayable
			_mapListContainer.AddChild(CreateFullGameMapRow("???", "A fractured zone - something stranger awaits."));
			_mapListContainer.AddChild(CreateFullGameMapRow("???", "Classified. Requires full clearance."));

			if (randomMap != null)
				_mapListContainer.AddChild(CreateMapButton(randomMap.Id, randomMap.Name, randomMap.Description));
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"MapSelectPanel.PopulateMapList() error: {ex.Message}");
		}
	}

	private Control CreateFullGameMapRow(string mapName, string description)
	{
		var container = new PanelContainer();
		container.ThemeTypeVariation = "NoVisualHBox";
		container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		container.Modulate = new Color(1f, 1f, 1f, 0.45f);
		if (_isMobile)
			container.MouseFilter = Control.MouseFilterEnum.Ignore;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation",    10);
		hbox.AddThemeConstantOverride("margin_left",   10);
		hbox.AddThemeConstantOverride("margin_top",    10);
		hbox.AddThemeConstantOverride("margin_right",  10);
		hbox.AddThemeConstantOverride("margin_bottom", 10);
		if (_isMobile)
			hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		container.AddChild(hbox);

		// Lock badge in place of SELECT button
		var lockBadge = new Label
		{
			Text = "FULL\nGAME",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(80, 52),
		};
		lockBadge.AddThemeFontSizeOverride("font_size", 12);
		UITheme.ApplyFont(lockBadge, semiBold: true, size: 12);
		lockBadge.Modulate = new Color(0.55f, 0.60f, 0.80f);
		hbox.AddChild(lockBadge);

		var textVbox = new VBoxContainer();
		textVbox.AddThemeConstantOverride("separation", 2);
		textVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (_isMobile)
			textVbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(textVbox);

		var nameLabel = new Label
		{
			Text = mapName,
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		if (_isMobile)
			nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 20);
		nameLabel.Modulate = new Color(0.55f, 0.60f, 0.80f);
		textVbox.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = _isMobile
				? TextServer.AutowrapMode.Word
				: TextServer.AutowrapMode.Off,
		};
		if (_isMobile)
			descLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		descLabel.AddThemeFontSizeOverride("font_size", 13);
		descLabel.Modulate = new Color(0.45f, 0.48f, 0.62f);
		textVbox.AddChild(descLabel);

		return container;
	}

	private Control CreateMapButton(string mapId, string mapName, string description)
	{
		var container = new PanelContainer();
		container.ThemeTypeVariation = "NoVisualHBox";
		container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (_isMobile)
			container.MouseFilter = Control.MouseFilterEnum.Pass;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation",    10);
		hbox.AddThemeConstantOverride("margin_left",   10);
		hbox.AddThemeConstantOverride("margin_top",    10);
		hbox.AddThemeConstantOverride("margin_right",  10);
		hbox.AddThemeConstantOverride("margin_bottom", 10);
		if (_isMobile)
			hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		container.AddChild(hbox);

		var btn = new Button
		{
			Text = "SELECT",
			CustomMinimumSize = new Vector2(80, 52),
		};
		if (_isMobile)
			btn.MouseFilter = Control.MouseFilterEnum.Pass;
		btn.AddThemeFontSizeOverride("font_size", 15);
		btn.Pressed      += () => SelectMap(mapId);
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		hbox.AddChild(btn);

		var textVbox = new VBoxContainer();
		textVbox.AddThemeConstantOverride("separation", 2);
		textVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		if (_isMobile)
			textVbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(textVbox);

		var nameLabel = new Label
		{
			Text = mapName,
			HorizontalAlignment = HorizontalAlignment.Left,
			ClipText = false,
		};
		if (_isMobile)
			nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 20);
		textVbox.AddChild(nameLabel);

		var descLabel = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = _isMobile
				? TextServer.AutowrapMode.Word
				: TextServer.AutowrapMode.Off,
		};
		if (_isMobile)
			descLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
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
		UpdateDifficultyVisuals();

		if (_mapListContainer == null) return;

		foreach (var old in _mapListContainer.GetChildren())
			old.QueueFree();
		PopulateMapList();

		// Alpha-in + scale-punch the newly selected row so selection feels deliberate
		foreach (var child in _mapListContainer.GetChildren())
		{
			if (child is Control ctrl && ctrl.Modulate.R > 1.2f) // gold modulate = selected row
			{
				ctrl.Modulate = new Color(ctrl.Modulate.R, ctrl.Modulate.G, ctrl.Modulate.B, 0f);
				ctrl.PivotOffset = ctrl.Size / 2f;
				ctrl.Scale = new Vector2(0.97f, 0.97f);
				var tw = ctrl.CreateTween();
				tw.SetParallel(true);
				tw.TweenProperty(ctrl, "modulate:a", 1f, 0.10f)
				  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
				tw.TweenProperty(ctrl, "scale", Vector2.One, 0.12f)
				  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				break;
			}
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
		btn.MouseEntered += () =>
		{
			SoundManager.Instance?.Play("ui_hover");
			btn.PivotOffset = btn.Size / 2f;
			btn.CreateTween().TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.08f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () =>
		{
			btn.PivotOffset = btn.Size / 2f;
			btn.CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.08f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		};
		parent.AddChild(btn);
	}

	private Button CreateDifficultyButton(string text, DifficultyMode difficulty)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(86, 40),
			ToggleMode = false,
		};
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.Pressed      += () => SelectDifficulty(difficulty);
		btn.MouseEntered += () =>
		{
			SoundManager.Instance?.Play("ui_hover");
			btn.PivotOffset = btn.Size / 2f;
			btn.CreateTween().TweenProperty(btn, "scale", new Vector2(1.04f, 1.04f), 0.08f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
		btn.MouseExited += () =>
		{
			btn.PivotOffset = btn.Size / 2f;
			btn.CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.08f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		};

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
		UpdateDifficultyButton(_easyButton,   DifficultyMode.Easy,   "Easy");
		UpdateDifficultyButton(_normalButton, DifficultyMode.Normal, "Normal");
		UpdateDifficultyButton(_hardButton,   DifficultyMode.Hard,   "Hard");
	}

	private void UpdateDifficultyButton(Button? btn, DifficultyMode mode, string baseLabel)
	{
		if (btn == null) return;
		var best = HighScoreManager.Instance?.GetPersonalBest(_selectedMapId, mode);
		bool hasWon    = best?.Won == true;
		bool hasPlayed = best != null;
		bool isSelected = _selectedDifficulty == mode;

		btn.Text = hasWon ? $"{baseLabel} \u2713" : baseLabel;

		Color color;
		if (isSelected)
			color = new Color(1.0f, 0.85f, 0.25f);   // gold - selected
		else if (hasWon)
			color = new Color(0.40f, 0.92f, 0.50f);  // green - cleared ✓
		else
			color = new Color(0.45f, 0.45f, 0.45f);  // gray - unplayed or attempted

		ApplyDifficultyButtonColor(btn, color, isSelected);
	}

	private void UpdatePersonalBestLabel()
	{
		if (_personalBestLabel == null) return;
		var best = HighScoreManager.Instance?.GetPersonalBest(_selectedMapId, _selectedDifficulty);
		if (best == null)
		{
			_personalBestLabel.Text = "No record yet - claim it.";
			return;
		}

		string diff = _selectedDifficulty switch
		{
			DifficultyMode.Easy => "Easy",
			DifficultyMode.Normal => "Normal",
			DifficultyMode.Hard => "Hard",
			_ => "Easy"
		};
		_personalBestLabel.Text =
			$"Personal Best ({diff}): {best.Score:N0}\n" +
			$"Wave {best.WaveReached}/{Balance.TotalWaves}  |  Lives {best.LivesRemaining}";
	}

	private static void ApplyDifficultyButtonColor(Button button, Color color, bool selected = false)
	{
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color);
		button.AddThemeColorOverride("font_pressed_color", color);
		button.AddThemeColorOverride("font_focus_color", color);

		if (selected)
		{
			var border = new StyleBoxFlat();
			border.BgColor     = new Color(0.10f, 0.12f, 0.22f, 0.0f);  // transparent fill
			border.BorderColor = new Color(0.25f, 0.95f, 0.45f);         // bright green
			border.SetBorderWidthAll(2);
			border.SetCornerRadiusAll(4);
			button.AddThemeStyleboxOverride("normal",   border);
			button.AddThemeStyleboxOverride("hover",    border);
			button.AddThemeStyleboxOverride("pressed",  border);
			button.AddThemeStyleboxOverride("focus",    border);
		}
		else
		{
			button.RemoveThemeStyleboxOverride("normal");
			button.RemoveThemeStyleboxOverride("hover");
			button.RemoveThemeStyleboxOverride("pressed");
			button.RemoveThemeStyleboxOverride("focus");
		}
	}
}
