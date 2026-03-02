using System.Linq;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Tools;
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
	private Node2D[] _slotNodes      = new Node2D[Balance.SlotCount];
	private Line2D[] _slotHighlights      = new Line2D[Balance.SlotCount];
	private Tween?[] _slotHighlightTweens = new Tween?[Balance.SlotCount];
	private int      _highlightedSlot      = -1;
	private bool     _highlightedSlotValid = false;
	private MapLayout _currentMap = null!;
	private Node2D _mapVisuals = null!;
	private Panel _tooltipPanel = null!;
	private Label _tooltipLabel = null!;
	private Label _waveAnnounce = null!;
	private BotRunner? _botRunner;
	private int _extraPicksRemaining;

	public override void _Ready()
	{
		Instance = this;
		DataLoader.LoadAll();
		// Bot playtest mode: godot --headless --path ... -- --bot --runs N
		var userArgs = OS.GetCmdlineUserArgs();
		if (userArgs.Contains("--bot"))
		{
			int runs = 50;
			int ri = System.Array.IndexOf(userArgs, "--runs");
			if (ri >= 0 && ri + 1 < userArgs.Length)
				int.TryParse(userArgs[ri + 1], out runs);
			_botRunner = new BotRunner(runs);
			Engine.MaxFps = 0;
			GD.Print($"[BOT] Headless playtest: {runs} runs");
		}

		_runState = new RunState();
		_draftSystem = new DraftSystem();
		_waveSystem = new WaveSystem();
		_combatSim = new CombatSim(_runState)
		{
			EnemyScene = EnemyScene,
			LanePath   = LanePath,
			Sounds     = SoundManager.Instance,
		};
		_combatSim.BotMode = _botRunner != null;
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
		SetupAnnouncer();

		GD.Print("Slot Theory booted.");
		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		if (_botRunner == null) UpdateTooltip();

		if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
			UpdateSlotHighlights();
		else if (_highlightedSlot != -1)
			ClearSlotHighlights();

		if (CurrentPhase != GamePhase.Wave) return;

		if (_botRunner != null) { BotTick(); return; }

		int livesBefore = _runState.Lives;
		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		_hudPanel.RefreshEnemies(_runState.EnemiesAlive.Count, _waveSystem.GetTotalCount());
		if (_runState.Lives < livesBefore) _hudPanel.FlashLives();

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			int livesLost = Balance.StartingLives - _runState.Lives;
			GD.Print("Run lost.");
			SoundManager.Instance?.Play("game_over");
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost, BuildBuildSummary());
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
				_endScreen.ShowWin(BuildBuildSummary());
			}
			else
			{
				SoundManager.Instance?.Play("wave_clear");
				_extraPicksRemaining = Balance.ExtraPicksForWave(_runState.WaveIndex);
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

		if (_botRunner != null)
		{
			var pick = _botRunner.CurrentBot.Pick(options, _runState);
			if (pick != null) OnDraftPick(pick.Option, pick.SlotIndex);
			else              StartWavePhase();
			return;
		}
		int totalPicks = Balance.ExtraPicksForWave(_runState.WaveIndex) + 1;
		int pickNum    = totalPicks - _extraPicksRemaining;
		_draftPanel.Show(options, _runState.WaveIndex + 1, pickNum, totalPicks);
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
		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
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
		if (_extraPicksRemaining > 0)
		{
			_extraPicksRemaining--;
			StartDraftPhase();
			return;
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
				"heavy_cannon"  => new Color(1.00f, 0.55f, 0.00f),
				"marker_tower"  => new Color(1.00f, 0.15f, 0.60f),
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

		// Draft assignment: click a slot in the world to place tower / assign modifier
		if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
		{
			for (int i = 0; i < Balance.SlotCount; i++)
			{
				if (!_draftPanel.IsSlotValidTarget(i)) continue;
				var hitRect = new Rect2(_slotNodes[i].GlobalPosition - new Vector2(22f, 22f), new Vector2(44f, 44f));
				if (hitRect.HasPoint(mousePos))
				{
					_draftPanel.SelectSlot(i);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			return;
		}

		// Wave phase: click tower to cycle targeting mode
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

			// Empty slot visual ΓÇö dark purple fill + neon violet border
		_slotNodes[i].AddChild(new ColorRect
		{
			Color        = new Color(0.08f, 0.00f, 0.16f, 0.85f),
			OffsetLeft   = -20f,
			OffsetTop    = -20f,
			OffsetRight  =  20f,
			OffsetBottom =  20f,
			MouseFilter  = Control.MouseFilterEnum.Ignore,
		});
		var borderSq = new[] { new Vector2(-20f,-20f), new Vector2(20f,-20f), new Vector2(20f,20f), new Vector2(-20f,20f), new Vector2(-20f,-20f) };
		_slotNodes[i].AddChild(new Line2D { Points = borderSq, Width = 7f,   DefaultColor = new Color(0.80f, 0.00f, 1.00f, 0.18f) });
		_slotNodes[i].AddChild(new Line2D { Points = borderSq, Width = 1.5f, DefaultColor = new Color(0.80f, 0.00f, 1.00f, 0.80f) });

		// Placement highlight — gold border, invisible until hovered in draft assignment mode
		var hlSq = new[] { new Vector2(-23f,-23f), new Vector2(23f,-23f), new Vector2(23f,23f), new Vector2(-23f,23f), new Vector2(-23f,-23f) };
		var hl = new Line2D { Points = hlSq, Width = 2.5f, DefaultColor = new Color(1f, 0.85f, 0.15f) };
		hl.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(hl);
		_slotHighlights[i] = hl;

			// Slot number label — below the slot square, always visible
			_slotNodes[i].AddChild(new ColorRect
			{
				Color       = new Color(0f, 0f, 0f, 0.65f),
				Position    = new Vector2(-13f, 23f),
				Size        = new Vector2(26f, 16f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				ZIndex      = 1,
			});
			var slotLabel = new Label
			{
				Text                = (i + 1).ToString(),
				Position            = new Vector2(-13f, 22f),
				Size                = new Vector2(26f, 18f),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment   = VerticalAlignment.Center,
				MouseFilter         = Control.MouseFilterEnum.Ignore,
				ZIndex              = 2,
			};
			slotLabel.AddThemeColorOverride("font_color", Colors.White);
			slotLabel.AddThemeFontSizeOverride("font_size", 16);
			_slotNodes[i].AddChild(slotLabel);
		}
	}

	private void UpdateSlotHighlights()
	{
		var mousePos = GetViewport().GetMousePosition();
		int  newHover = -1;
		bool newValid = false;

		for (int i = 0; i < Balance.SlotCount; i++)
		{
			// Decide if this slot is hoverable at all in current mode
			bool hoverable = _draftPanel.IsAwaitingSlot
				? true                                    // tower placement: all 6 slots
				: _runState.Slots[i].Tower != null;       // modifier assign: only occupied slots
			if (!hoverable) continue;

			var hitRect = new Rect2(_slotNodes[i].GlobalPosition - new Vector2(22f, 22f), new Vector2(44f, 44f));
			if (!hitRect.HasPoint(mousePos)) continue;

			newHover = i;
			newValid = _draftPanel.IsSlotValidTarget(i);
			break;
		}

		if (newHover == _highlightedSlot && newValid == _highlightedSlotValid) return;

		// Fade out previous highlight
		if (_highlightedSlot >= 0 && GodotObject.IsInstanceValid(_slotHighlights[_highlightedSlot]))
		{
			_slotHighlightTweens[_highlightedSlot]?.Kill();
			var tw = _slotHighlights[_highlightedSlot].CreateTween();
			_slotHighlightTweens[_highlightedSlot] = tw;
			tw.TweenProperty(_slotHighlights[_highlightedSlot], "modulate", Colors.Transparent, 0.10f);
		}

		// Fade in new highlight with appropriate color
		if (newHover >= 0 && GodotObject.IsInstanceValid(_slotHighlights[newHover]))
		{
			// Tower placement valid=gold, Modifier placement valid=white; invalid=red either way
			_slotHighlights[newHover].DefaultColor = newValid
				? (_draftPanel.IsAwaitingSlot ? new Color(1f, 0.85f, 0.15f) : Colors.White)
				: new Color(1f, 0.15f, 0.15f);
			_slotHighlightTweens[newHover]?.Kill();
			var tw = _slotHighlights[newHover].CreateTween();
			_slotHighlightTweens[newHover] = tw;
			tw.TweenProperty(_slotHighlights[newHover], "modulate", Colors.White, 0.10f);
		}

		_highlightedSlot      = newHover;
		_highlightedSlotValid = newValid;
	}

	private void ClearSlotHighlights()
	{
		for (int i = 0; i < Balance.SlotCount; i++)
		{
			_slotHighlightTweens[i]?.Kill();
			_slotHighlightTweens[i] = null;
			if (GodotObject.IsInstanceValid(_slotHighlights[i]))
				_slotHighlights[i].Modulate = Colors.Transparent;
		}
		_highlightedSlot      = -1;
		_highlightedSlotValid = false;
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
		// Background + neon grid
		_mapVisuals.AddChild(new GridBackground());
		// Neon path ΓÇö layered glow: outer haze ΓåÆ dark road fill ΓåÆ edge glow ΓåÆ bright edge
		Vector2[] pts = _currentMap.PathWaypoints;
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 120f, DefaultColor = new Color(1.0f, 0.05f, 0.55f, 0.05f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 80f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.10f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 50f,  DefaultColor = new Color(0.10f, 0.00f, 0.18f, 0.95f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 16f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.18f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 3f,   DefaultColor = new Color(1.0f, 0.25f, 0.65f, 0.85f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		// Path flow arrows
		var pathFlow = new PathFlow();
		_mapVisuals.AddChild(pathFlow);
		pathFlow.Initialize(_currentMap.PathWaypoints);
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
		// Show during wave, and during modifier assignment so player can see what's on each tower
		bool tooltipAllowed = CurrentPhase == GamePhase.Wave
		                   || (CurrentPhase == GamePhase.Draft && _draftPanel.IsAwaitingTower);
		if (!tooltipAllowed)
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
				{
					var mdef = DataLoader.GetModifierDef(mod.ModifierId);
					text += "• " + mdef.Name + "  \u2014  " + mdef.Description + "\n";
				}
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


	// -- Bot multi-step simulation -------------------------------------------------

	private const float BOT_DT    = 0.05f;
	private const int   BOT_STEPS = 100;

	private void BotTick()
	{
		for (int i = 0; i < BOT_STEPS && CurrentPhase == GamePhase.Wave; i++)
		{
			// Manually advance enemies (PathFollow2D._Process is disabled in bot mode)
			foreach (var enemy in _runState.EnemiesAlive)
			{
				if (!GodotObject.IsInstanceValid(enemy)) continue;
				float spd = enemy.IsSlowed ? enemy.Speed * Balance.SlowSpeedFactor : enemy.Speed;
				enemy.Progress     += spd * BOT_DT;
				if (enemy.MarkedRemaining > 0f) enemy.MarkedRemaining -= BOT_DT;
				if (enemy.SlowRemaining   > 0f) enemy.SlowRemaining   -= BOT_DT;
			}

			var result = _combatSim.Step(BOT_DT, _runState, _waveSystem);

			if (result == WaveResult.Loss)
			{
				CurrentPhase = GamePhase.Loss;
				_botRunner!.RecordResult(false, _runState.WaveIndex + 1, _runState);
				if (_botRunner.HasMoreRuns) { RestartRun(); return; }
				_botRunner.PrintSummary();
				GetTree().Quit();
				return;
			}

			if (result == WaveResult.WaveComplete)
			{
				_botRunner!.RecordWaveEnd(_runState.Lives);
				_runState.WaveIndex++;
				if (_runState.WaveIndex >= Balance.TotalWaves)
				{
					CurrentPhase = GamePhase.Win;
					_botRunner.RecordResult(true, _runState.WaveIndex, _runState);
					if (_botRunner.HasMoreRuns) { RestartRun(); return; }
					_botRunner.PrintSummary();
					GetTree().Quit();
					return;
				}
				_extraPicksRemaining = Balance.ExtraPicksForWave(_runState.WaveIndex);
				StartDraftPhase();
				return;
			}
		}
	}

	// -- Wave announcement + build summary ----------------------------------------

	private void SetupAnnouncer()
	{
		var layer = new CanvasLayer { Layer = 4 };
		AddChild(layer);

		var anchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(anchor);

		_waveAnnounce = new Label
		{
			Text               = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode       = TextServer.AutowrapMode.Off,
			AnchorLeft         = 0.5f,
			AnchorRight        = 0.5f,
			AnchorTop          = 0.38f,
			AnchorBottom       = 0.38f,
			GrowHorizontal     = Control.GrowDirection.Both,
			GrowVertical       = Control.GrowDirection.Both,
			Modulate           = new Color(1f, 1f, 1f, 0f),
			MouseFilter        = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(_waveAnnounce, semiBold: true, size: 54);
		_waveAnnounce.AddThemeColorOverride("font_color", new Color(0.85f, 0.20f, 1.00f));
		anchor.AddChild(_waveAnnounce);
	}

	private void ShowWaveAnnouncement(int wave)
	{
		_waveAnnounce.Text     = $"WAVE {wave}";
		_waveAnnounce.Scale    = new Vector2(1.35f, 1.35f);
		_waveAnnounce.Modulate = new Color(1f, 1f, 1f, 1f);
		var tween = _waveAnnounce.CreateTween();
		tween.TweenProperty(_waveAnnounce, "scale", Vector2.One, 0.3f)
			 .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(0.35f);
		tween.TweenProperty(_waveAnnounce, "modulate:a", 0f, 0.45f);
	}

	private string BuildBuildSummary()
	{
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null) continue;
			var def = DataLoader.GetTowerDef(tower.TowerId);
			var modNames = string.Join(", ", tower.Modifiers.Select(m => DataLoader.GetModifierDef(m.ModifierId).Name));
			sb.AppendLine(modNames.Length > 0
				? $"Slot {i + 1}  ·  {def.Name}  +  {modNames}"
				: $"Slot {i + 1}  ·  {def.Name}");
		}
		return sb.ToString().TrimEnd();
	}
	private void StartWavePhase()
	{
		CurrentPhase = GamePhase.Wave;
		if (_botRunner == null) ShowWaveAnnouncement(_runState.WaveIndex + 1);
		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave(_waveSystem);
		SoundManager.Instance?.Play("wave_start");
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		GD.Print($"Wave {_runState.WaveIndex + 1} started.");
	}
}
