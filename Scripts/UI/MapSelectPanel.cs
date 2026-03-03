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
	public static string PendingMapSelection => _pendingMapSelection;

	private string _selectedMapId = "random_map";  // Default to random
	private VBoxContainer? _mapListContainer;

	public override void _Ready()
	{
		// Reset to default state when MapSelectPanel loads
		_selectedMapId = "random_map";
		
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

		AddSpacer(vbox, 24);

		// Map list
		_mapListContainer = new VBoxContainer();
		_mapListContainer.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(_mapListContainer);

		PopulateMapList();

		AddSpacer(vbox, 24);

		// Start button
		AddButton(vbox, "Start Run", 260, 58, 28, OnStartRun);
		AddButton(vbox, "Back", 260, 48, 22, OnBack);
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
			foreach (var mapDef in maps)
			{
				var mapButton = CreateMapButton(mapDef.Id, mapDef.Name, mapDef.Description);
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

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("margin_left", 16);
		vbox.AddThemeConstantOverride("margin_top", 12);
		vbox.AddThemeConstantOverride("margin_right", 16);
		vbox.AddThemeConstantOverride("margin_bottom", 12);
		vbox.AddThemeConstantOverride("separation", 4);
		container.AddChild(vbox);

		// Map name
		var nameLabel = new Label
		{
			Text = mapName,
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		SlotTheory.Core.UITheme.ApplyFont(nameLabel, semiBold: true, size: 24);
		vbox.AddChild(nameLabel);

		// Description
		var descLabel = new Label
		{
			Text = description,
			HorizontalAlignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(440, 0),
			AutowrapMode = TextServer.AutowrapMode.Word,
		};
		descLabel.AddThemeFontSizeOverride("font_size", 16);
		descLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
		vbox.AddChild(descLabel);

		// Button area
		var btn = new Button
		{
			Text = "SELECT",
			CustomMinimumSize = new Vector2(0, 40),
		};
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.Pressed += () => SelectMap(mapId);
		btn.MouseEntered += () => SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
		vbox.AddChild(btn);

		// Highlight if selected
		if (mapId == _selectedMapId)
		{
			container.Modulate = new Color(1.2f, 1.2f, 1.0f);  // Slight highlight
		}

		return container;
	}

	private void SelectMap(string mapId)
	{
		_selectedMapId = mapId;
		SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
		
		// Rebuild map list to show selection
		if (_mapListContainer != null)
		{
			while (_mapListContainer.GetChildCount() > 0)
				_mapListContainer.GetChild(0).Free();
			PopulateMapList();
		}
	}

	private void OnStartRun()
	{
		// Store selected map globally
		_pendingMapSelection = _selectedMapId;
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
}
