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
	private MapPreviewControl? _previewControl;
	private ulong _proceduralPreviewSeed;
	private bool _isMobile;

	public override void _Ready()
	{
		DataLoader.LoadAll();
		_selectedMapId = DataLoader.GetAllMapDefs().FirstOrDefault()?.Id ?? "random_map";
		_selectedDifficulty = DifficultyMode.Normal;
		_proceduralPreviewSeed = (ulong)(System.Environment.TickCount64 & 0x7FFFFFFF);

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

		_isMobile = MobileOptimization.IsMobile();

		var bodyPanel = new PanelContainer();
		bodyPanel.CustomMinimumSize = _isMobile ? new Vector2(680, 420) : new Vector2(1120, 560);
		bodyPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bodyPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		UITheme.ApplyGlassChassisPanel(
			bodyPanel,
			bg: new Color(0.040f, 0.052f, 0.102f, 0.94f),
			accent: new Color(0.40f, 0.78f, 0.94f, 0.92f),
			corners: 12,
			borderWidth: 2,
			padH: 14,
			padV: 12,
			sideEmitters: true,
			emitterIntensity: 0.86f);
		vbox.AddChild(bodyPanel);

		// Content row: map list | difficulty
		var contentRow = new HBoxContainer();
		contentRow.AddThemeConstantOverride("separation", 16);
		contentRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		contentRow.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		bodyPanel.AddChild(contentRow);

		var leftFrame = new PanelContainer();
		leftFrame.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		leftFrame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		UITheme.ApplyGlassChassisPanel(
			leftFrame,
			bg: new Color(0.022f, 0.038f, 0.080f, 0.95f),
			accent: new Color(0.36f, 0.74f, 0.90f, 0.88f),
			corners: 10,
			borderWidth: 1,
			padH: 10,
			padV: 10,
			sideEmitters: false);
		contentRow.AddChild(leftFrame);

		// Left column: map list
		var leftColumn = new VBoxContainer();
		leftColumn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		leftColumn.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		leftColumn.AddThemeConstantOverride("separation", 8);
		leftFrame.AddChild(leftColumn);

		var scrollContainer = new ScrollContainer();
		scrollContainer.CustomMinimumSize = _isMobile
			? new Vector2(420, 200)
			: new Vector2(760, 260);
		scrollContainer.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scrollContainer.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
		scrollContainer.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
		TouchScrollHelper.EnableDragScroll(scrollContainer);
		ApplySlimScrollBarStyle(scrollContainer);
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

		// Preview row: map thumbnail on the left, legend panel on the right
		var previewRow = new HBoxContainer();
		previewRow.AddThemeConstantOverride("separation", 10);
		leftColumn.AddChild(previewRow);

		_previewControl = new MapPreviewControl();
		_previewControl.CustomMinimumSize = _isMobile
			? new Vector2(0, 120)
			: new Vector2(0, 180);
		_previewControl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_previewControl.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		_previewControl.MouseFilter = Control.MouseFilterEnum.Ignore;
		previewRow.AddChild(_previewControl);
		previewRow.AddChild(BuildPreviewLegend());
		UpdateMapPreview();

		// Right column: difficulty + personal best
		var rightFrame = new PanelContainer();
		rightFrame.CustomMinimumSize = new Vector2(248, 0);
		rightFrame.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		UITheme.ApplyGlassChassisPanel(
			rightFrame,
			bg: new Color(0.022f, 0.038f, 0.080f, 0.95f),
			accent: new Color(0.36f, 0.74f, 0.90f, 0.88f),
			corners: 10,
			borderWidth: 1,
			padH: 12,
			padV: 10,
			sideEmitters: false);
		contentRow.AddChild(rightFrame);

		var rightColumn = new VBoxContainer();
		rightColumn.AddThemeConstantOverride("separation", 12);
		rightColumn.CustomMinimumSize = new Vector2(240, 0);
		rightFrame.AddChild(rightColumn);

		var difficultyCard = new PanelContainer();
		difficultyCard.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		UITheme.ApplyGlassChassisPanel(
			difficultyCard,
			bg: new Color(0.020f, 0.034f, 0.074f, 0.96f),
			accent: new Color(0.34f, 0.70f, 0.88f, 0.78f),
			corners: 8,
			borderWidth: 1,
			padH: 10,
			padV: 8,
			sideEmitters: false);
		rightColumn.AddChild(difficultyCard);

		var difficultyVBox = new VBoxContainer();
		difficultyVBox.AddThemeConstantOverride("separation", 8);
		difficultyCard.AddChild(difficultyVBox);

		var difficultyLabel = new Label
		{
			Text = "DIFFICULTY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(difficultyLabel, semiBold: true, size: 20);
		difficultyLabel.Modulate = new Color("#a6d608");
		difficultyVBox.AddChild(difficultyLabel);

		var difficultyContainer = new HBoxContainer();
		difficultyContainer.AddThemeConstantOverride("separation", 10);
		difficultyContainer.Alignment = BoxContainer.AlignmentMode.Center;
		difficultyVBox.AddChild(difficultyContainer);

		_easyButton = CreateDifficultyButton("Easy", DifficultyMode.Easy);
		difficultyContainer.AddChild(_easyButton);
		_normalButton = CreateDifficultyButton("Normal", DifficultyMode.Normal);
		difficultyContainer.AddChild(_normalButton);
		_hardButton = CreateDifficultyButton("Hard", DifficultyMode.Hard);
		difficultyContainer.AddChild(_hardButton);
		UpdateDifficultyVisuals();

		var diffDivider = new ColorRect
		{
			Color = new Color(0.32f, 0.70f, 0.90f, 0.24f),
			CustomMinimumSize = new Vector2(0, 1),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		difficultyVBox.AddChild(diffDivider);

		_personalBestLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(220, 44),
			Text = "",
		};
		_personalBestLabel.AddThemeFontSizeOverride("font_size", 14);
		_personalBestLabel.Modulate = new Color(0.78f, 0.90f, 1.00f, 0.94f);
		difficultyVBox.AddChild(_personalBestLabel);
		UpdatePersonalBestLabel();

		rightColumn.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

		// Start Run + Back in the right column - always visible, no layout tricks needed
		var startBtn = new Button
		{
			Text = "Start Run",
			CustomMinimumSize = new Vector2(0, 50),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		startBtn.AddThemeFontSizeOverride("font_size", 24);
		UITheme.ApplyPrimaryStyle(startBtn);
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
		UITheme.ApplyCyanStyle(backBtn);
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
		if (_isMobile)
			container.MouseFilter = Control.MouseFilterEnum.Ignore;
		ApplyMapRowStyle(container, selected: false, locked: true);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation",    10);
		hbox.AddThemeConstantOverride("margin_left",   10);
		hbox.AddThemeConstantOverride("margin_top",    9);
		hbox.AddThemeConstantOverride("margin_right",  10);
		hbox.AddThemeConstantOverride("margin_bottom", 9);
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
		textVbox.AddThemeConstantOverride("separation", 1);
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
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 19);
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
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.Modulate = new Color(0.45f, 0.48f, 0.62f);
		textVbox.AddChild(descLabel);

		return container;
	}

	private Control CreateMapButton(string mapId, string mapName, string description)
	{
		bool isSelected = mapId == _selectedMapId;
		var container = new PanelContainer();
		container.ThemeTypeVariation = "NoVisualHBox";
		container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		container.SetMeta("map_id", mapId);
		if (_isMobile)
			container.MouseFilter = Control.MouseFilterEnum.Pass;
		ApplyMapRowStyle(container, selected: isSelected, locked: false);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation",    10);
		hbox.AddThemeConstantOverride("margin_left",   10);
		hbox.AddThemeConstantOverride("margin_top",    9);
		hbox.AddThemeConstantOverride("margin_right",  10);
		hbox.AddThemeConstantOverride("margin_bottom", 9);
		if (_isMobile)
			hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		container.AddChild(hbox);

		var btn = new Button
		{
			Text = "SELECT",
			CustomMinimumSize = new Vector2(80, 52),
		};
		if (isSelected)
			UITheme.ApplyPrimaryStyle(btn);
		else
			UITheme.ApplyCyanStyle(btn);
		if (_isMobile)
			btn.MouseFilter = Control.MouseFilterEnum.Pass;
		btn.AddThemeFontSizeOverride("font_size", 15);
		btn.Pressed      += () => SelectMap(mapId);
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		hbox.AddChild(btn);

		var textVbox = new VBoxContainer();
		textVbox.AddThemeConstantOverride("separation", 1);
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
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 19);
		nameLabel.Modulate = isSelected
			? new Color(0.95f, 0.99f, 0.86f)
			: new Color(0.90f, 0.95f, 1.00f);
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
		descLabel.AddThemeFontSizeOverride("font_size", 12);
		descLabel.Modulate = isSelected
			? new Color(0.78f, 0.84f, 0.70f)
			: new Color(0.66f, 0.70f, 0.78f);
		textVbox.AddChild(descLabel);

		return container;
	}

	private static void ApplyMapRowStyle(PanelContainer container, bool selected, bool locked)
	{
		Color bg = locked
			? new Color(0.030f, 0.036f, 0.072f, 0.70f)
			: new Color(0.018f, 0.030f, 0.078f, 0.94f);
		Color border = locked
			? new Color(0.30f, 0.34f, 0.46f, 0.44f)
			: selected
				? new Color(0.64f, 0.82f, 0.24f, 0.96f)
				: new Color(0.24f, 0.62f, 0.76f, 0.72f);

		container.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
			bg: bg,
			border: border,
			corners: 9,
			borderWidth: selected ? 2 : 1,
			padH: 0,
			padV: 0));

		if (locked)
			UITheme.AddTopAccent(container, new Color(0.56f, 0.64f, 0.82f, 0.18f));
		else if (selected)
			UITheme.AddTopAccent(container, new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.42f));
		else
			UITheme.AddTopAccent(container, new Color(0.70f, 0.92f, 1.00f, 0.18f));
	}

	private void SelectMap(string mapId)
	{
		_selectedMapId = mapId;
		SoundManager.Instance?.Play("ui_select");
		UpdatePersonalBestLabel();
		UpdateDifficultyVisuals();
		UpdateMapPreview();

		if (_mapListContainer == null) return;

		foreach (var old in _mapListContainer.GetChildren())
			old.QueueFree();
		PopulateMapList();

		// Alpha-in + scale-punch the newly selected row so selection feels deliberate.
		foreach (var child in _mapListContainer.GetChildren())
		{
			if (child is not PanelContainer row || !row.HasMeta("map_id"))
				continue;

			string rowId = row.GetMeta("map_id").AsString();
			if (rowId != _selectedMapId)
				continue;

			row.Modulate = new Color(row.Modulate.R, row.Modulate.G, row.Modulate.B, 0f);
			row.PivotOffset = row.Size / 2f;
			row.Scale = new Vector2(0.97f, 0.97f);
			var tw = row.CreateTween();
			tw.SetParallel(true);
			tw.TweenProperty(row, "modulate:a", 1f, 0.10f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(row, "scale", Vector2.One, 0.12f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			break;
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

	private void UpdateMapPreview()
	{
		if (_previewControl == null) return;

		// Unstable Anomaly has no fixed path - show the mystery "?" panel
		if (_selectedMapId == "random_map")
		{
			_previewControl.SetMystery();
			return;
		}

		var mapDef = DataLoader.GetAllMapDefs().FirstOrDefault(m => m.Id == _selectedMapId);
		if (mapDef == null || mapDef.Path == null || mapDef.Path.Length < 2)
		{
			_previewControl.SetMystery();
			return;
		}

		var path  = System.Array.ConvertAll(mapDef.Path,  p => new Vector2(p.X, p.Y));
		var slots = System.Array.ConvertAll(mapDef.Slots, s => new Vector2(s.X, s.Y));
		_previewControl.SetMap(path, slots);
	}

	private Control BuildPreviewLegend()
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(_isMobile ? 88 : 108, 0);
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		panel.MouseFilter       = Control.MouseFilterEnum.Ignore;
		UITheme.ApplyGlassChassisPanel(
			panel,
			bg: new Color(0.030f, 0.046f, 0.088f, 0.94f),
			accent: new Color(0.36f, 0.72f, 0.90f, 0.82f),
			corners: 8,
			borderWidth: 1,
			padH: 8,
			padV: 8,
			sideEmitters: false);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(vbox);

		var title = new Label
		{
			Text = "LEGEND",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(title, semiBold: true, size: 11);
		title.Modulate = new Color(0.55f, 0.55f, 0.75f, 0.80f);
		vbox.AddChild(title);

		vbox.AddChild(BuildLegendEntry(MapPreviewControl.StartMarker, "Start"));
		vbox.AddChild(BuildLegendEntry(MapPreviewControl.SlotFill,    "Tower Slot"));
		vbox.AddChild(BuildLegendEntry(MapPreviewControl.ExitMarker,  "Exit"));

		return panel;
	}

	private static Control BuildLegendEntry(Color dotColor, string text)
	{
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 5);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;

		var dot = new Label
		{
			Text        = "\u25CF",   // ●
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		dot.AddThemeFontSizeOverride("font_size", 13);
		dot.Modulate = dotColor;
		hbox.AddChild(dot);

		var lbl = new Label
		{
			Text               = text,
			VerticalAlignment  = VerticalAlignment.Center,
			MouseFilter        = Control.MouseFilterEnum.Ignore,
		};
		lbl.AddThemeFontSizeOverride("font_size", 11);
		lbl.Modulate = new Color(0.72f, 0.72f, 0.82f, 0.85f);
		hbox.AddChild(lbl);

		return hbox;
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

	private static void ApplySlimScrollBarStyle(ScrollContainer container)
	{
		var vbar = container.GetVScrollBar();
		if (vbar == null)
			return;

		vbar.CustomMinimumSize = new Vector2(8f, 0f);
		vbar.AddThemeStyleboxOverride("scroll", UITheme.MakePanel(
			bg: new Color(0.014f, 0.024f, 0.060f, 0.88f),
			border: new Color(0.14f, 0.28f, 0.40f, 0.66f),
			corners: 4,
			borderWidth: 1,
			padH: 1,
			padV: 1));

		var grabberNormal = UITheme.MakePanel(
			bg: new Color(0.30f, 0.70f, 0.86f, 0.58f),
			border: new Color(0.70f, 0.93f, 1.00f, 0.74f),
			corners: 4,
			borderWidth: 1,
			padH: 1,
			padV: 1);
		var grabberHover = UITheme.MakePanel(
			bg: new Color(0.40f, 0.86f, 0.98f, 0.72f),
			border: new Color(0.86f, 0.98f, 1.00f, 0.86f),
			corners: 4,
			borderWidth: 1,
			padH: 1,
			padV: 1);

		vbar.AddThemeStyleboxOverride("grabber", grabberNormal);
		vbar.AddThemeStyleboxOverride("grabber_highlight", grabberHover);
		vbar.AddThemeStyleboxOverride("grabber_pressed", grabberHover);
	}

	private static void ApplyDifficultyButtonColor(Button button, Color color, bool selected = false)
	{
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", color);
		button.AddThemeColorOverride("font_pressed_color", color);
		button.AddThemeColorOverride("font_focus_color", color);

		if (selected)
		{
			var sel = UITheme.MakeBtn(
				new Color(0.06f, 0.14f, 0.08f, 0.92f),
				new Color(0.64f, 0.92f, 0.30f),
				border: 2, corners: 6,
				glowAlpha: 0.12f, glowSize: 4,
				glowColor: new Color(0.64f, 0.92f, 0.30f));
			button.AddThemeStyleboxOverride("normal",   sel);
			button.AddThemeStyleboxOverride("hover",    sel);
			button.AddThemeStyleboxOverride("pressed",  sel);
			button.AddThemeStyleboxOverride("focus",    sel);
		}
		else
		{
			var border = new Color(color.R * 0.72f, color.G * 0.72f, color.B * 0.72f, 0.72f);
			var normal = UITheme.MakeBtn(
				new Color(0.018f, 0.028f, 0.076f, 0.94f),
				border,
				border: 1,
				corners: 6);
			var hover = UITheme.MakeBtn(
				new Color(0.024f, 0.040f, 0.090f, 0.96f),
				new Color(color.R, color.G, color.B, 0.92f),
				border: 2,
				corners: 6,
				glowAlpha: 0.08f,
				glowSize: 3,
				glowColor: color);
			button.AddThemeStyleboxOverride("normal", normal);
			button.AddThemeStyleboxOverride("hover", hover);
			button.AddThemeStyleboxOverride("pressed", normal);
			button.AddThemeStyleboxOverride("focus", hover);
		}
	}
}
