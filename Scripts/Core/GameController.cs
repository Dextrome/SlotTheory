using Godot;
using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.UI;

namespace SlotTheory.Core;

public enum GamePhase { Boot, Draft, Wave, Win, Loss }

public partial class GameController : Node
{
	public static GameController Instance { get; private set; } = null!;

	public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;

	[Export] public PackedScene? EnemyScene { get; set; }
	[Export] public Path2D? LanePath { get; set; }

	private RunState _runState = null!;
	private DraftSystem _draftSystem = null!;
	private WaveSystem _waveSystem = null!;
	private CombatSim _combatSim = null!;
	private DraftPanel _draftPanel = null!;
	private HudPanel _hudPanel = null!;
	private EndScreen _endScreen = null!;
	private Node2D[] _slotNodes = new Node2D[Balance.SlotCount];
	private MapLayout _currentMap = null!;
	private Node2D _mapVisuals = null!;

	public override void _Ready()
	{
		Instance = this;
		DataLoader.LoadAll();

		_runState = new RunState();
		_draftSystem = new DraftSystem();
		_waveSystem = new WaveSystem();
		_combatSim = new CombatSim(_runState)
		{
			EnemyScene = EnemyScene,
			LanePath = LanePath,
		};
		_draftPanel = GetNode<DraftPanel>("../DraftPanel");
		_hudPanel   = GetNode<HudPanel>("../HudPanel");
		_endScreen  = GetNode<EndScreen>("../EndScreen");

		var world = GetNode<Node2D>("../World");
		_mapVisuals = new Node2D { Name = "_mapVisuals" };
		world.AddChild(_mapVisuals);
		world.MoveChild(_mapVisuals, 0);

		GenerateMap();
		SetupSlots();

		GD.Print("Slot Theory booted.");
		StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		if (CurrentPhase != GamePhase.Wave) return;

		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			int livesLost = Balance.StartingLives - _runState.Lives;
			GD.Print("Run lost.");
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost);
			return;
		}

		if (result == WaveResult.WaveComplete)
		{
			_runState.WaveIndex++;
			if (_runState.WaveIndex >= Balance.TotalWaves)
			{
				CurrentPhase = GamePhase.Win;
				GD.Print("Run won!");
				_endScreen.ShowWin();
			}
			else
			{
				StartDraftPhase();
			}
		}
	}

	public void StartDraftPhase()
	{
		CurrentPhase = GamePhase.Draft;
		var options = _draftSystem.GenerateOptions(_runState);
		GD.Print($"Wave {_runState.WaveIndex + 1} draft. Options: {options.Count}");

		// All slots full AND all towers at modifier cap → nothing to offer, skip draft.
		if (options.Count == 0)
		{
			GD.Print("No draft options available — skipping draft.");
			StartWavePhase();
			return;
		}

		_draftPanel.Show(options, _runState.WaveIndex + 1);
	}

	public RunState GetRunState() => _runState;

	/// <summary>Wipe all in-flight state and restart from wave 1 draft.</summary>
	public void RestartRun()
	{
		// Free all enemies currently in the scene
		foreach (var e in _runState.EnemiesAlive)
		{
			if (GodotObject.IsInstanceValid(e)) e.QueueFree();
		}

		// Remove tower nodes from slot scene nodes — use Free() so ClearSlotVisuals
		// doesn't encounter QueueFree-pending nodes when it iterates slot children.
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower != null && GodotObject.IsInstanceValid(tower))
				tower.Free();
		}

		_runState.Reset();
		_combatSim.ResetForWave();
		_endScreen.Visible = false;
		Engine.TimeScale = 1.0;   // always reset speed on new run
		_hudPanel.Refresh(1, Balance.StartingLives);
		_hudPanel.ResetSpeed();

		ClearMapVisuals();
		GenerateMap();
		ClearSlotVisuals();
		SetupSlots();

		GD.Print("Run restarted.");
		StartDraftPhase();
	}

	/// <summary>Called by DraftPanel after the player picks an option.</summary>
	public void OnDraftPick(DraftOption option, int targetSlotIndex)
	{
		if (option.Type == DraftOptionType.Tower)
		{
			if (targetSlotIndex >= 0 && _runState.Slots[targetSlotIndex].Tower == null)
				PlaceTower(option.Id, targetSlotIndex);
		}
		else
		{
			var tower = _runState.Slots[targetSlotIndex].Tower;
			if (tower != null)
				_draftSystem.ApplyModifier(option.Id, tower);
		}
		StartWavePhase();
	}

	/// <summary>Place a tower by ID into a slot. Called by draft UI later.</summary>
	public void PlaceTower(string towerId, int slotIndex)
	{
		if (_runState.Slots[slotIndex].Tower != null) return;

		var def = DataLoader.GetTowerDef(towerId);
		var tower = new TowerInstance
		{
			TowerId        = towerId,
			BaseDamage     = def.BaseDamage,
			AttackInterval = def.AttackInterval,
			Range          = def.Range,
			AppliesMark    = def.AppliesMark,
			ProjectileColor = towerId switch
			{
				"rapid_shooter" => new Color(0.3f, 0.9f, 1.0f),  // cyan
				"heavy_cannon"  => new Color(1.0f, 0.55f, 0.0f), // orange
				"marker_tower"  => new Color(0.75f, 0.3f, 1.0f), // purple
				_               => Colors.Yellow,
			},
		};

		// Range indicator — semi-transparent circle
		var rangeCircle = new Polygon2D { Color = new Color(0.2f, 0.6f, 1.0f, 0.10f) };
		var pts = new Vector2[64];
		for (int p = 0; p < 64; p++)
		{
			float a = p * Mathf.Tau / 64;
			pts[p] = new Vector2(Mathf.Cos(a) * tower.Range, Mathf.Sin(a) * tower.Range);
		}
		rangeCircle.Polygon = pts;
		tower.AddChild(rangeCircle);

		// Tower visual — blue square
		tower.AddChild(new ColorRect
		{
			Color        = new Color(0.2f, 0.5f, 1.0f),
			OffsetLeft   = -15f,
			OffsetTop    = -15f,
			OffsetRight  =  15f,
			OffsetBottom =  15f,
		});

		// Targeting mode icon — centred on the tower square
		var modeLabel = new Label
		{
			Text                  = TowerInstance.ModeIcon(TargetingMode.First),
			Position              = new Vector2(-15f, -10f),
			Size                  = new Vector2(30f, 20f),
			HorizontalAlignment   = HorizontalAlignment.Center,
			VerticalAlignment     = VerticalAlignment.Center,
		};
		modeLabel.AddThemeColorOverride("font_color", Colors.White);
		modeLabel.AddThemeFontSizeOverride("font_size", 14);
		tower.ModeLabel = modeLabel;
		tower.AddChild(modeLabel);

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;
		GD.Print($"Placed {def.Name} in slot {slotIndex}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb) return;

		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null) continue;
			if (tower.GlobalPosition.DistanceTo(mb.Position) <= 22f)
			{
				tower.CycleTargetingMode();
				GetViewport().SetInputAsHandled();
				return;
			}
		}
	}

	private void SetupSlots()
	{
		var slotsNode = GetNode<Node2D>("../World/Slots");
		for (int i = 0; i < Balance.SlotCount; i++)
		{
			_slotNodes[i] = slotsNode.GetNode<Node2D>($"Slot{i}");
			_slotNodes[i].Position = _currentMap.SlotPositions[i];

			// Empty slot visual — dark gray square
			_slotNodes[i].AddChild(new ColorRect
			{
				Color        = new Color(0.25f, 0.25f, 0.25f, 0.5f),
				OffsetLeft   = -20f,
				OffsetTop    = -20f,
				OffsetRight  =  20f,
				OffsetBottom =  20f,
			});

			// Slot number label
			var slotLabel = new Label
			{
				Text                = (i + 1).ToString(),
				Position            = new Vector2(-20f, -10f),
				Size                = new Vector2(40f, 20f),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment   = VerticalAlignment.Center,
			};
			slotLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.8f));
			slotLabel.AddThemeFontSizeOverride("font_size", 14);
			_slotNodes[i].AddChild(slotLabel);
		}
	}

	private void GenerateMap()
	{
		_currentMap = MapGenerator.Generate(System.Environment.TickCount);
		RenderMap();
		if (LanePath != null)
			LanePath.Curve = BuildCurve(_currentMap.PathWaypoints);
	}

	private void RenderMap()
	{
		// Full-screen grass background
		_mapVisuals.AddChild(new ColorRect
		{
			Position = Vector2.Zero,
			Size     = new Vector2(1280, 720),
			Color    = new Color("#a6d608"),
		});

		// One rect per path cell
		for (int c = 0; c < MapGenerator.COLS; c++)
		{
			for (int r = 0; r < MapGenerator.ROWS; r++)
			{
				if (!_currentMap.PathGrid[c, r]) continue;
				_mapVisuals.AddChild(new ColorRect
				{
					Position = new Vector2(c * MapGenerator.CELL_W,
					                       MapGenerator.GRID_Y + r * MapGenerator.CELL_H),
					Size  = new Vector2(MapGenerator.CELL_W, MapGenerator.CELL_H),
					Color = new Color("#8B5E3C"),
				});
			}
		}
	}

	private void ClearMapVisuals()
	{
		while (_mapVisuals.GetChildCount() > 0)
			_mapVisuals.GetChild(0).Free();
	}

	private void ClearSlotVisuals()
	{
		foreach (var slotNode in _slotNodes)
		{
			if (slotNode == null) continue;
			while (slotNode.GetChildCount() > 0)
				slotNode.GetChild(0).Free();
		}
	}

	private static Curve2D BuildCurve(Vector2[] pts)
	{
		var curve = new Curve2D();
		foreach (var pt in pts)
			curve.AddPoint(pt);
		return curve;
	}

	private void StartWavePhase()
	{
		CurrentPhase = GamePhase.Wave;
		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave();
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		GD.Print($"Wave {_runState.WaveIndex + 1} started.");
	}
}
