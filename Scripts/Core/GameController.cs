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
	private Panel _tooltipPanel = null!;
	private Label _tooltipLabel = null!;

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
			LanePath   = LanePath,
			Sounds     = SoundManager.Instance,
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
		SetupTooltip();

		GD.Print("Slot Theory booted.");
		StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		UpdateTooltip();
		if (CurrentPhase != GamePhase.Wave) return;

		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			int livesLost = Balance.StartingLives - _runState.Lives;
			GD.Print("Run lost.");
			SoundManager.Instance?.Play("game_over");
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
				SoundManager.Instance?.Play("victory");
				_endScreen.ShowWin();
			}
			else
			{
				SoundManager.Instance?.Play("wave_clear");
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
			BodyColor = towerId switch
			{
				"rapid_shooter" => new Color(0.15f, 0.65f, 1.00f),
				"heavy_cannon"  => new Color(0.10f, 0.20f, 0.80f),
				"marker_tower"  => new Color(0.55f, 0.20f, 0.92f),
				_               => new Color(0.20f, 0.50f, 1.00f),
			},
		};

		// Range indicator — semi-transparent fill in tower's body colour
		var rangeCircle = new Polygon2D { Color = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, 0.10f) };
		var pts = new Vector2[64];
		for (int p = 0; p < 64; p++)
		{
			float a = p * Mathf.Tau / 64;
			pts[p] = new Vector2(Mathf.Cos(a) * tower.Range, Mathf.Sin(a) * tower.Range);
		}
		rangeCircle.Polygon = pts;
		tower.RangeCircle = rangeCircle;
		tower.AddChild(rangeCircle);

		// Range border — white ring outline
		var borderPts = new Vector2[65];
		for (int p = 0; p < 64; p++) borderPts[p] = pts[p];
		borderPts[64] = pts[0];
		var rangeBorder = new Line2D
		{
			Points       = borderPts,
			Width        = 1.5f,
			DefaultColor = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, 0.28f),
		};
		tower.RangeBorder = rangeBorder;
		tower.AddChild(rangeBorder);

		// Cooldown bar — background track + fill, positioned below the tower square
		tower.AddChild(new ColorRect
		{
			Color       = new Color(0.15f, 0.15f, 0.15f),
			Position    = new Vector2(-15f, 17f),
			Size        = new Vector2(30f, 4f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		var cooldownBar = new ColorRect
		{
			Color       = new Color(0.25f, 0.9f, 0.35f),
			Position    = new Vector2(-15f, 17f),
			Size        = new Vector2(0f, 4f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		tower.AddChild(cooldownBar);
		tower.CooldownBar = cooldownBar;

		// Targeting mode icon — centred on the tower square
		var modeLabel = new Label
		{
			Text                  = TowerInstance.ModeIcon(TargetingMode.First),
			Position              = new Vector2(-15f, -10f),
			Size                  = new Vector2(30f, 20f),
			HorizontalAlignment   = HorizontalAlignment.Center,
			VerticalAlignment     = VerticalAlignment.Center,
			MouseFilter           = Control.MouseFilterEnum.Ignore,
		};
		modeLabel.AddThemeColorOverride("font_color", Colors.White);
		modeLabel.AddThemeFontSizeOverride("font_size", 14);
		tower.ModeLabel = modeLabel;
		tower.AddChild(modeLabel);

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;
		SoundManager.Instance?.Play("tower_place");
		GD.Print($"Placed {def.Name} in slot {slotIndex}");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;

		var mousePos = GetViewport().GetMousePosition();
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null) continue;
			var hitRect = new Rect2(tower.GlobalPosition - new Vector2(25f, 25f), new Vector2(50f, 50f));
			if (hitRect.HasPoint(mousePos))
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
				MouseFilter  = Control.MouseFilterEnum.Ignore,
			});

			// Slot number label
			var slotLabel = new Label
			{
				Text                = (i + 1).ToString(),
				Position            = new Vector2(-20f, -10f),
				Size                = new Vector2(40f, 20f),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment   = VerticalAlignment.Center,
				MouseFilter         = Control.MouseFilterEnum.Ignore,
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
		// ── Grass background ────────────────────────────────────────────
		_mapVisuals.AddChild(new ColorRect
		{
			Position = Vector2.Zero,
			Size     = new Vector2(1280, 720),
			Color    = new Color(0.65f, 0.84f, 0.03f),
		});

		// Subtle darker splotches for grass texture
		var splotchRng = new System.Random(_currentMap.PathWaypoints.Length * 31337);
		for (int i = 0; i < 14; i++)
		{
			float rx = (float)(splotchRng.NextDouble() * 1280);
			float ry = MapGenerator.GRID_Y + (float)(splotchRng.NextDouble() * (MapGenerator.ROWS * MapGenerator.CELL_H));
			float rw = 60f + (float)(splotchRng.NextDouble() * 80f);
			float rh = rw * (0.5f + (float)(splotchRng.NextDouble() * 0.5f));
			int pts = 12;
			var splotch = new Polygon2D { Color = new Color(0.55f, 0.72f, 0.01f, 0.35f) };
			var verts = new Vector2[pts];
			for (int p = 0; p < pts; p++)
			{
				float a = p * Mathf.Tau / pts;
				float jitter = 0.75f + (float)(splotchRng.NextDouble() * 0.5f);
				verts[p] = new Vector2(rx + Mathf.Cos(a) * rw * jitter,
				                       ry + Mathf.Sin(a) * rh * jitter);
			}
			splotch.Polygon = verts;
			_mapVisuals.AddChild(splotch);
		}

		// ── Road — outline then surface via Line2D (smooth rounded corners) ──
		_mapVisuals.AddChild(new Line2D
		{
			Points       = _currentMap.PathWaypoints,
			Width        = 116f,
			DefaultColor = new Color(0.33f, 0.19f, 0.09f),
			JointMode    = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode   = Line2D.LineCapMode.Round,
		});
		_mapVisuals.AddChild(new Line2D
		{
			Points       = _currentMap.PathWaypoints,
			Width        = 100f,
			DefaultColor = new Color(0.55f, 0.37f, 0.24f),
			JointMode    = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode   = Line2D.LineCapMode.Round,
		});
		// Faint centre highlight — gives the dirt road some depth
		_mapVisuals.AddChild(new Line2D
		{
			Points       = _currentMap.PathWaypoints,
			Width        = 50f,
			DefaultColor = new Color(0.62f, 0.44f, 0.28f, 0.45f),
			JointMode    = Line2D.LineJointMode.Round,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode   = Line2D.LineCapMode.Round,
		});

		// ── Decorations (trees and rocks in grass cells) ────────────────
		foreach (var dec in _currentMap.Decorations)
			_mapVisuals.AddChild(dec.Type == DecorationType.Tree
				? MakeTree(dec.Pos)
				: MakeRock(dec.Pos));
	}

	private static Node2D MakeTree(Vector2 pos)
	{
		var node = new Node2D { Position = pos };
		// Trunk
		node.AddChild(new Polygon2D
		{
			Color   = new Color(0.36f, 0.21f, 0.09f),
			Polygon = new[] { new Vector2(-3f,2f), new Vector2(3f,2f), new Vector2(3f,15f), new Vector2(-3f,15f) },
		});
		// Canopy shadow (slightly offset darker circle)
		node.AddChild(MakeCirclePoly(new Vector2(2f, -4f), 15f, 14, new Color(0.10f, 0.32f, 0.02f)));
		// Canopy main
		node.AddChild(MakeCirclePoly(new Vector2(0f, -7f), 15f, 14, new Color(0.14f, 0.46f, 0.04f)));
		// Canopy highlight
		node.AddChild(MakeCirclePoly(new Vector2(-3f, -11f), 9f, 10, new Color(0.22f, 0.60f, 0.08f)));
		return node;
	}

	private static Node2D MakeRock(Vector2 pos)
	{
		var node = new Node2D { Position = pos };
		// Rock body
		node.AddChild(new Polygon2D
		{
			Color   = new Color(0.48f, 0.46f, 0.42f),
			Polygon = new[] { new Vector2(-10f,5f), new Vector2(-7f,-5f), new Vector2(0f,-10f),
			                  new Vector2(8f,-8f),  new Vector2(11f,2f),  new Vector2(6f,8f), new Vector2(-5f,8f) },
		});
		// Rock highlight
		node.AddChild(new Polygon2D
		{
			Color   = new Color(0.64f, 0.62f, 0.57f),
			Polygon = new[] { new Vector2(-7f,0f), new Vector2(-4f,-6f), new Vector2(2f,-9f),
			                  new Vector2(6f,-5f), new Vector2(4f,0f),   new Vector2(-2f,2f) },
		});
		return node;
	}

	private static Polygon2D MakeCirclePoly(Vector2 center, float radius, int pts, Color color)
	{
		var verts = new Vector2[pts];
		for (int i = 0; i < pts; i++)
		{
			float a = i * Mathf.Tau / pts;
			verts[i] = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
		}
		return new Polygon2D { Color = color, Polygon = verts };
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

	private void SetupTooltip()
	{
		var tooltipLayer = new CanvasLayer { Layer = 5 };
		AddChild(tooltipLayer);

		_tooltipPanel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
		_tooltipPanel.Visible = false;
		tooltipLayer.AddChild(_tooltipPanel);

		_tooltipLabel = new Label
		{
			Position    = new Vector2(8, 6),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.Off,
		};
		_tooltipLabel.AddThemeFontSizeOverride("font_size", 13);
		_tooltipPanel.AddChild(_tooltipLabel);
	}

	private void UpdateTooltip()
	{
		// Only show during wave — hide behind draft/end overlays otherwise
		if (CurrentPhase != GamePhase.Wave)
		{
			_tooltipPanel.Visible = false;
			return;
		}

		var mousePos = GetViewport().GetMousePosition();
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null) continue;
			var hitRect = new Rect2(tower.GlobalPosition - new Vector2(25f, 25f), new Vector2(50f, 50f));
			if (!hitRect.HasPoint(mousePos)) continue;

			// Build tooltip text
			var def = DataLoader.GetTowerDef(tower.TowerId);
			var targetingName = tower.TargetingMode switch
			{
				TargetingMode.First     => "First",
				TargetingMode.Strongest => "Strongest",
				TargetingMode.LowestHp  => "Lowest HP",
				_                       => "First",
			};
			var text = $"Slot {i + 1}  ·  {def.Name}  [{targetingName}]\n";
			if (tower.Modifiers.Count == 0)
				text += "(no modifiers)";
			else
				foreach (var mod in tower.Modifiers)
					text += "• " + DataLoader.GetModifierDef(mod.ModifierId).Name + "\n";
			_tooltipLabel.Text = text.TrimEnd();

			// Size panel to fit label
			var labelSize = _tooltipLabel.GetMinimumSize();
			_tooltipPanel.Size = labelSize + new Vector2(16, 12);

			// Position near cursor, clamped to viewport
			var pos = mousePos + new Vector2(20, 10);
			pos.X = Mathf.Min(pos.X, 1280 - _tooltipPanel.Size.X - 4);
			pos.Y = Mathf.Min(pos.Y, 720  - _tooltipPanel.Size.Y - 4);
			_tooltipPanel.Position = pos;
			_tooltipPanel.Visible = true;
			return;
		}

		_tooltipPanel.Visible = false;
	}

	private void StartWavePhase()
	{
		CurrentPhase = GamePhase.Wave;
		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave(_waveSystem);
		SoundManager.Instance?.Play("wave_start");
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		GD.Print($"Wave {_runState.WaveIndex + 1} started.");
	}
}
