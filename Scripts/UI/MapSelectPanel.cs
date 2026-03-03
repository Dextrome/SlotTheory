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
	public static void SetPendingMapSelection(string mapId) => _pendingMapSelection = mapId;

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

		// Map list (scrollable to keep buttons visible)
		var scrollContainer = new ScrollContainer();
		scrollContainer.CustomMinimumSize = new Vector2(500, 300);
		vbox.AddChild(scrollContainer);

		_mapListContainer = new VBoxContainer();
		_mapListContainer.AddThemeConstantOverride("separation", 12);
		scrollContainer.AddChild(_mapListContainer);

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
