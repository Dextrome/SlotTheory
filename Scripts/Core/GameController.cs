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
	private Node2D[]      _slotNodes           = new Node2D[Balance.SlotCount];
	private Line2D[]      _slotHighlights      = new Line2D[Balance.SlotCount];
	private Line2D[]      _slotPreviewGlows    = new Line2D[Balance.SlotCount];
	private Line2D[]      _slotProcHalos       = new Line2D[Balance.SlotCount];
	private Tween?[]      _slotHighlightTweens = new Tween?[Balance.SlotCount];
	private ColorRect[][] _slotModPips         = new ColorRect[Balance.SlotCount][];
	private ModifierIcon[][] _slotModIcons     = new ModifierIcon[Balance.SlotCount][];
	private float[] _slotProcHaloRemaining     = new float[Balance.SlotCount];
	private Color[] _slotProcHaloColor         = new Color[Balance.SlotCount];
	private float[,] _slotModIconPulseRemaining = new float[Balance.SlotCount, Balance.MaxModifiersPerTower];
	private int      _highlightedSlot      = -1;
	private bool     _highlightedSlotValid = false;
	private Map _currentMap = null!;
	private Node2D _mapVisuals = null!;
	private Panel _tooltipPanel = null!;
	private Label _tooltipLabel = null!;
	private TowerInstance? _selectedTooltipTower;
	private Label _waveAnnounce = null!;
	private Label _threatWarn = null!;
	private Label _placementLabel = null!;
	private ColorRect _waveClearFlash = null!;
	private ColorRect _threatPulse = null!;
	private Node2D _worldNode = null!;
	private BotRunner? _botRunner;
	private int _extraPicksRemaining;
	private WaveReport? _lastWaveReport;
	private int _previewGhostSlot = -1;
	private float _previewGhostPhase = 0f;
	private ModifierIcon? _previewModifierIcon;
	private bool _hitStopActive = false;
	private double _preHitStopTimeScale = 1.0;
	private float _hitStopCooldown = 0f;

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
		
		// Apply pending map selection from MapSelectPanel if available
		_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
		
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

		_worldNode = GetNode<Node2D>("../World");
		_mapVisuals = new Node2D { Name = "_mapVisuals" };
		_worldNode.AddChild(_mapVisuals);
		_worldNode.MoveChild(_mapVisuals, 0);

		GenerateMap();
		SetupSlots();
		SetupTooltip();
		SetupAnnouncer();

		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		_lastWaveReport = null;
		StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		if (_botRunner == null) UpdateTooltip();

		if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
			UpdateSlotHighlights();
		else if (_highlightedSlot != -1)
			ClearSlotHighlights();

		if (CurrentPhase == GamePhase.Draft && _draftPanel.IsAwaitingTower)
			UpdateModifierPreviewGhost((float)delta);
		else
			ClearModifierPreviewGhost();

		if (_botRunner == null) UpdatePlacementLabel();
		if (_botRunner == null) UpdateProcVisuals((float)delta);

		if (CurrentPhase != GamePhase.Wave) return;

		if (_botRunner != null) { BotTick(); return; }

		int livesBefore = _runState.Lives;
		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		_hudPanel.RefreshEnemies(_runState.EnemiesAlive.Count, _waveSystem.GetTotalCount());
		if (_runState.Lives < livesBefore) { _hudPanel.FlashLives(); ShakeWorld(); }

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			int livesLost = Balance.StartingLives - _runState.Lives;
			SoundManager.Instance?.Play("game_over");
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost, _runState.TotalKills, _runState.TotalDamageDealt, BuildBuildSummary(), _runState);
			return;
		}

		if (result == WaveResult.WaveComplete)
		{
			// Complete wave tracking for next draft panel display
			var waveReport = _runState.CompleteWave();
			
			_runState.WaveIndex++;
			if (_runState.WaveIndex >= Balance.TotalWaves)
			{
				CurrentPhase = GamePhase.Win;
				SoundManager.Instance?.Play("victory");
				_endScreen.ShowWin(_runState.TotalKills, _runState.TotalDamageDealt, BuildBuildSummary());
			}
			else
			{
				if (_botRunner == null) ShowWaveClearFlash();
				SoundManager.Instance?.Play("wave_clear");
				_extraPicksRemaining = Balance.ExtraPicksForWave(_runState.WaveIndex);
				if (_botRunner != null)
				{
					StartDraftPhase();
				}
				else
				{
					// Let the wave-clear flash breathe before showing the draft panel
					CurrentPhase = GamePhase.Draft;
					// Store the wave report for the next draft panel
					_lastWaveReport = waveReport;
					GetTree().CreateTimer(0.48f).Timeout += StartDraftPhase;
				}
			}
		}
	}

	public override void _Notification(int what)
	{
		// Handle Android app lifecycle for mobile devices
		if (OS.GetName() == "Android")
		{
			switch (what)
			{
				case (int)NotificationApplicationPaused:
					// Auto-pause when app goes to background (minimized/phone call/etc)
					if (CurrentPhase == GamePhase.Wave && GetTree().Paused == false)
					{
						GD.Print("App paused - auto-pausing game");
						var pauseScreen = GetNode<PauseScreen>("../PauseScreen");
						pauseScreen?.Pause();
					}
					break;

				case (int)NotificationApplicationResumed:
					// App returned from background - game stays paused for user control
					GD.Print("App resumed from background");
					break;
			}
		}
	}

	public void StartDraftPhase()
	{
		CurrentPhase = GamePhase.Draft;
		var options = _draftSystem.GenerateOptions(_runState);

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
		_draftPanel.Show(options, _runState.WaveIndex + 1, pickNum, totalPicks, _lastWaveReport);
		_lastWaveReport = null; // Clear after use
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

		// Memory leak fixes for bot mode
		SlotTheory.Modifiers.ModEvents.Reset();  // Clear static event handlers
		
		_runState.Reset();
		_combatSim.ResetForWave();
		_endScreen.Visible = false;
		Engine.TimeScale = 1.0;   // always reset speed on new run
		_hitStopActive = false;
		_hitStopCooldown = 0f;
		_preHitStopTimeScale = 1.0;
		_hudPanel.Refresh(1, Balance.StartingLives);
		_hudPanel.ResetSpeed();

		ClearMapVisuals();
		GenerateMap();
		ClearSlotVisuals();
		SetupSlots();

		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		
		// Force garbage collection in bot mode every 25 runs to prevent memory accumulation
		if (_botRunner != null)
		{
			int completedRuns = _botRunner.CompletedRuns;
			if (completedRuns % 25 == 0 && completedRuns > 0)
			{
				GD.Print($"[MEMORY] Forcing GC after {completedRuns} completed runs");
				System.GC.Collect();
				System.GC.WaitForPendingFinalizers();
				System.GC.Collect(); // Second collection to clean up objects released after finalization
			}
		}
		
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
			{
				_draftSystem.ApplyModifier(option.Id, tower);
				if (_botRunner == null) RefreshModPips(targetSlotIndex);
			}
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
			TowerId           = towerId,
			BaseDamage        = def.BaseDamage,
			AttackInterval    = def.AttackInterval,
			Range             = def.Range,
			AppliesMark       = def.AppliesMark,
			ChainCount        = def.ChainCount,
			ChainRange        = def.ChainRange,
			ChainDamageDecay  = def.ChainDamageDecay,
			ProjectileColor = towerId switch
			{
				"rapid_shooter" => new Color(0.30f, 0.90f, 1.00f),  // cyan
				"heavy_cannon"  => new Color(1.00f, 0.55f, 0.00f),  // orange
				"marker_tower"  => new Color(0.75f, 0.30f, 1.00f),  // purple
				"chain_tower"   => new Color(0.55f, 0.90f, 1.00f),  // electric blue
				_               => Colors.Yellow,
			},
			BodyColor = towerId switch
			{
				"rapid_shooter" => new Color(0.15f, 0.65f, 1.00f),
				"heavy_cannon"  => new Color(1.00f, 0.55f, 0.00f),
				"marker_tower"  => new Color(1.00f, 0.15f, 0.60f),
				"chain_tower"   => new Color(0.50f, 0.85f, 1.00f),
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

		// Targeting mode badge — always visible on the right side of each tower.
		var modeBadge = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.09f, 0.86f),
			Position = new Vector2(24f, -11f),
			Size = new Vector2(18f, 18f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_slotNodes[slotIndex].AddChild(modeBadge);
		var modeBadgeBorder = new Line2D
		{
			Points = new[]
			{
				new Vector2(24f, -11f), new Vector2(42f, -11f), new Vector2(42f, 7f),
				new Vector2(24f, 7f), new Vector2(24f, -11f)
			},
			Width = 1.6f,
			DefaultColor = new Color(0.68f, 0.94f, 1.00f, 0.90f),
			Antialiased = true,
		};
		_slotNodes[slotIndex].AddChild(modeBadgeBorder);
		var modeLabel = new Label
		{
			Text = TowerInstance.ModeIcon(TargetingMode.First),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		modeLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		UITheme.ApplyFont(modeLabel, semiBold: true, size: 13);
		modeLabel.AddThemeColorOverride("font_color", Colors.White);
		modeBadge.AddChild(modeLabel);
		tower.ModeLabel = modeLabel;

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;

		// Placement bounce — scale from 0 → 1.15 → 1.0
		if (_botRunner == null)
		{
			tower.Scale = Vector2.Zero;
			var placeTween = tower.CreateTween();
			placeTween.TweenProperty(tower, "scale", new Vector2(1.15f, 1.15f), 0.15f)
			          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			placeTween.TweenProperty(tower, "scale", Vector2.One, 0.10f)
			          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}

		RefreshModPips(slotIndex);
		SoundManager.Instance?.Play("tower_place");
	}

	public override void _Input(InputEvent @event)
	{
		Vector2 pressPos;
		if (MobileOptimization.IsMobile())
		{
			if (@event is not InputEventScreenTouch { Pressed: true } touch)
				return;
			pressPos = touch.Position;
		}
		else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			pressPos = GetViewport().GetMousePosition();
		}
		else if (@event is InputEventScreenTouch { Pressed: true } touch)
		{
			pressPos = touch.Position;
		}
		else
		{
			return;
		}
		// Draft assignment: click a slot in the world to place tower / assign modifier
		if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
		{
			float slotHalf = MobileOptimization.IsMobile() ? 30f : 22f;
			for (int i = 0; i < Balance.SlotCount; i++)
			{
				if (!_draftPanel.IsSlotValidTarget(i)) continue;
				var hitRect = new Rect2(_slotNodes[i].GlobalPosition - new Vector2(slotHalf, slotHalf), new Vector2(slotHalf * 2f, slotHalf * 2f));
				if (hitRect.HasPoint(pressPos))
				{
					_draftPanel.SelectSlot(i);
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			
			if (MobileOptimization.IsMobile() && _draftPanel.IsAwaitingTower)
			{
				for (int i = 0; i < _runState.Slots.Length; i++)
				{
					var tower = _runState.Slots[i].Tower;
					if (tower == null) continue;
					var hitRect = new Rect2(tower.GlobalPosition - new Vector2(34f, 34f), new Vector2(68f, 68f));
					if (!hitRect.HasPoint(pressPos)) continue;

					// Touch fallback: allow tapping the tower body itself for preview/confirm.
					if (_draftPanel.IsSlotValidTarget(i))
					{
						_draftPanel.SelectSlot(i);
						GetViewport().SetInputAsHandled();
						return;
					}

					_selectedTooltipTower = tower;
					GetViewport().SetInputAsHandled();
					return;
				}
				_selectedTooltipTower = null;
			}

			// Outside tap while previewing a modifier cancels preview selection.
			if (_draftPanel.IsAwaitingTower && _draftPanel.HasModifierPreview)
			{
				_draftPanel.CancelModifierPreview();
				ClearModifierPreviewGhost();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		if (CurrentPhase == GamePhase.Wave)
		{
			for (int i = 0; i < _runState.Slots.Length; i++)
			{
				var tower = _runState.Slots[i].Tower;
				if (tower == null) continue;
				var hitRect = new Rect2(tower.GlobalPosition - new Vector2(25f, 25f), new Vector2(50f, 50f));
				if (!hitRect.HasPoint(pressPos)) continue;
				if (MobileOptimization.IsMobile())
				{
					if (_selectedTooltipTower == tower)
						tower.CycleTargetingMode();
					else
						_selectedTooltipTower = tower;
				}
				else
				{
					tower.CycleTargetingMode();
				}
				GetViewport().SetInputAsHandled();
				return;
			}
			if (MobileOptimization.IsMobile())
				_selectedTooltipTower = null;
		}
	}
	private void SetupSlots()
	{
		var slotsNode = _worldNode.GetNode<Node2D>("Slots");
		for (int i = 0; i < Balance.SlotCount; i++)
		{
			_slotNodes[i] = slotsNode.GetNode<Node2D>($"Slot{i}");
			_slotNodes[i].Position = _currentMap.Slots[i];

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

		// Preview glow — shown when a modifier is previewed on this slot.
		var previewGlow = new Line2D { Points = hlSq, Width = 4.2f, DefaultColor = new Color(0.45f, 0.95f, 1.00f, 0.92f) };
		previewGlow.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(previewGlow);
		_slotPreviewGlows[i] = previewGlow;

		var procHalo = new Line2D { Points = hlSq, Width = 5.2f, DefaultColor = new Color(0.70f, 0.95f, 1.00f, 0.92f) };
		procHalo.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(procHalo);
		_slotProcHalos[i] = procHalo;
		_slotProcHaloRemaining[i] = 0f;
		_slotProcHaloColor[i] = Colors.White;

			// Mod-count pip — just below slot square, shown only when tower has ≥ 1 modifier
			// Modifier pips — 3 small squares below slot, one per modifier slot
			var pips = new ColorRect[Balance.MaxModifiersPerTower];
			var icons = new ModifierIcon[Balance.MaxModifiersPerTower];
			for (int p = 0; p < Balance.MaxModifiersPerTower; p++)
			{
				float px = (p - 1) * 9f;  // centers at -9, 0, +9
				var pip = new ColorRect
				{
					Position    = new Vector2(px - 3f, 27f),
					Size        = new Vector2(6f, 6f),
					Color       = new Color(0.22f, 0.22f, 0.22f, 0.45f),
					MouseFilter = Control.MouseFilterEnum.Ignore,
					Visible     = false,
				};
				_slotNodes[i].AddChild(pip);
				pips[p] = pip;

				// Modifier icons shown below pips for quick visual build reads.
				float ix = (p - 1) * 12f;
				var icon = new ModifierIcon
				{
					Position = new Vector2(ix - 5f, 38f),
					Size = new Vector2(10f, 10f),
					CustomMinimumSize = new Vector2(10f, 10f),
					Visible = false,
				};
				_slotNodes[i].AddChild(icon);
				icons[p] = icon;
				_slotModIconPulseRemaining[i, p] = 0f;
			}
			_slotModPips[i] = pips;
			_slotModIcons[i] = icons;
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
		// Generate seed if not set
		if (_runState.RngSeed == 0)
			_runState.RngSeed = System.Environment.TickCount;

		// Load or generate map
		if (!string.IsNullOrEmpty(_runState.SelectedMapId) && _runState.SelectedMapId != "random_map")
		{
			// Load hand-crafted map
			try
			{
				var mapDef = DataLoader.GetMapDef(_runState.SelectedMapId);
				_currentMap = HandCraftedMap.LoadFromDef(mapDef);
				GD.Print($"[GameController] Loaded hand-crafted map: {_runState.SelectedMapId}");
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[GameController] Failed to load map '{_runState.SelectedMapId}': {ex.Message}. Falling back to procedural.");
				_currentMap = ProceduralMap.Generate((ulong)_runState.RngSeed);
			}
		}
		else
		{
			// Generate procedural map
			_currentMap = ProceduralMap.Generate((ulong)_runState.RngSeed);
			GD.Print("[GameController] Generated procedural map");
		}

		RenderMap();
		if (LanePath != null)
			LanePath.Curve = BuildCurve(_currentMap.Path);
	}

	private void RenderMap()
	{
		// Background + neon grid
		_mapVisuals.AddChild(new GridBackground());
		// Neon path — layered glow: outer haze → dark road fill → edge glow → bright edge
		Vector2[] pts = _currentMap.Path;
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 120f, DefaultColor = new Color(1.0f, 0.05f, 0.55f, 0.05f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 80f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.10f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 50f,  DefaultColor = new Color(0.10f, 0.00f, 0.18f, 0.95f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 16f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.18f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 3f,   DefaultColor = new Color(1.0f, 0.25f, 0.65f, 0.85f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		// Path flow arrows
		var pathFlow = new PathFlow();
		_mapVisuals.AddChild(pathFlow);
		pathFlow.Initialize(_currentMap.Path);
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
		UITheme.ApplyFont(_tooltipLabel, size: 13);
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
			_selectedTooltipTower = null;
			return;
		}
		Vector2 mousePos;
		if (MobileOptimization.IsMobile())
		{
			if (_selectedTooltipTower == null || !GodotObject.IsInstanceValid(_selectedTooltipTower))
			{
				_tooltipPanel.Visible = false;
				return;
			}
			mousePos = _selectedTooltipTower.GlobalPosition;
		}
		else
		{
			mousePos = GetViewport().GetMousePosition();
		}
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
			// Effective attack interval: baked (HairTrigger) + runtime hooks (FocusLens)
			float effInterval = tower.AttackInterval;
			foreach (var mod in tower.Modifiers)
				mod.ModifyAttackInterval(ref effInterval, tower);
			// Effective damage: unconditional modifiers (FocusLens) + baked changes
			float effDamage = tower.GetEffectiveDamageForPreview();
			var text = $"Slot {i + 1}  -  {def.Name}  [{targetingName}]\n";
			text += $"{effDamage:0.#} dmg  -  {effInterval:0.##} s  -  {(int)tower.Range} px\n";
			if (tower.IsChainTower)
				text += $"chains x{tower.ChainCount}  ({(int)(tower.ChainDamageDecay * 100)}% per bounce)  range {(int)tower.ChainRange} px\n";
			if (tower.Modifiers.Count == 0)
				text += "(no modifiers)";
			else
				foreach (var mod in tower.Modifiers)
				{
					var mdef = DataLoader.GetModifierDef(mod.ModifierId);
					text += "* " + mdef.Name + " - " + mdef.Description + "\n";
				}
			_tooltipLabel.Text = text.TrimEnd();
			// Size panel to fit label
			var labelSize = _tooltipLabel.GetMinimumSize();
			_tooltipPanel.Size = labelSize + new Vector2(16, 12);
			// Positioning: mobile shows above selected tower; desktop follows cursor.
			Vector2 pos;
			if (MobileOptimization.IsMobile())
			{
				pos = new Vector2(
					mousePos.X - _tooltipPanel.Size.X * 0.5f,
					mousePos.Y - _tooltipPanel.Size.Y - 14f
				);
			}
			else
			{
				pos = mousePos + new Vector2(20, 10);
			}
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

		_threatWarn = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetTop = 94f,
			OffsetBottom = 124f,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(_threatWarn, semiBold: true, size: 24);
		_threatWarn.AddThemeColorOverride("font_color", new Color(1.00f, 0.56f, 0.25f));
		anchor.AddChild(_threatWarn);

		_placementLabel = new Label();
		_placementLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_placementLabel.AnchorLeft   = 0f;
		_placementLabel.AnchorRight  = 1f;
		_placementLabel.AnchorTop    = 0f;
		_placementLabel.AnchorBottom = 0f;
		_placementLabel.OffsetTop    = 50f;
		_placementLabel.OffsetBottom = 80f;
		_placementLabel.GrowHorizontal = Control.GrowDirection.Both;
		_placementLabel.Visible = false;
		_placementLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		UITheme.ApplyFont(_placementLabel, semiBold: true, size: 20);
		_placementLabel.Modulate = new Color(1f, 0.85f, 0.15f);
		anchor.AddChild(_placementLabel);

		_waveClearFlash = new ColorRect();
		_waveClearFlash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_waveClearFlash.Color   = new Color(0.10f, 1f, 0.45f, 0f);
		_waveClearFlash.Visible = false;
		_waveClearFlash.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_waveClearFlash);

		_threatPulse = new ColorRect();
		_threatPulse.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_threatPulse.Color = new Color(1.00f, 0.48f, 0.16f, 0f);
		_threatPulse.Visible = false;
		_threatPulse.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_threatPulse);
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

	private void ShowArmoredWaveWarning()
	{
		_threatWarn.Text = "⚠ ARMORED WAVE INCOMING";
		_threatWarn.Visible = true;
		_threatWarn.Modulate = new Color(1f, 1f, 1f, 0f);
		_threatPulse.Visible = true;
		_threatPulse.Color = new Color(1.00f, 0.48f, 0.16f, 0f);

		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_threatWarn, "modulate:a", 1f, 0.16f);
		tw.TweenProperty(_threatPulse, "color:a", 0.18f, 0.12f);
		tw.Chain().TweenInterval(0.42f);
		tw.SetParallel(true);
		tw.TweenProperty(_threatWarn, "modulate:a", 0f, 0.22f);
		tw.TweenProperty(_threatPulse, "color:a", 0f, 0.24f);
		tw.Chain().TweenCallback(Callable.From(() =>
		{
			_threatWarn.Visible = false;
			_threatPulse.Visible = false;
		}));
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
			
			// Calculate DPS for this tower
			var totalDamage = _runState.GetTowerTotalDamage(i);
			var dps = _runState.GetTowerDPS(i);
			var dpsDisplay = dps >= 100f ? $"{dps:F0} DPS" : $"{dps:F1} DPS";
			
			sb.AppendLine(modNames.Length > 0
				? $"Slot {i + 1}  ·  {def.Name}  +  {modNames}  •  {dpsDisplay}"
				: $"Slot {i + 1}  ·  {def.Name}  •  {dpsDisplay}");
		}
		return sb.ToString().TrimEnd();
	}
	private void StartWavePhase()
	{
		CurrentPhase = GamePhase.Wave;
		if (_botRunner == null) ShowWaveAnnouncement(_runState.WaveIndex + 1);
		var nextCfg = DataLoader.GetWaveConfig(_runState.WaveIndex);
		bool clumpedArmored = nextCfg.ClumpArmored && nextCfg.TankyCount >= 2;
		_combatSim.InitialSpawnDelay = (_botRunner == null && clumpedArmored) ? 0.8f : 0f;
		if (_botRunner == null && clumpedArmored)
			ShowArmoredWaveWarning();
		
		// Initialize tracking for the new wave
		_runState.StartNewWave(_runState.WaveIndex + 1);
		
		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave(_waveSystem);
		SoundManager.Instance?.Play("wave_start");
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
	}

	private void ShakeWorld()
	{
		var tween = CreateTween();
		tween.TweenProperty(_worldNode, "position", new Vector2( 4f,  2f), 0.04f);
		tween.TweenProperty(_worldNode, "position", new Vector2(-4f, -3f), 0.04f);
		tween.TweenProperty(_worldNode, "position", new Vector2( 3f, -2f), 0.04f);
		tween.TweenProperty(_worldNode, "position", new Vector2(-2f,  3f), 0.04f);
		tween.TweenProperty(_worldNode, "position", Vector2.Zero,          0.04f);
	}

	private void UpdatePlacementLabel()
	{
		if (CurrentPhase != GamePhase.Draft || (!_draftPanel.IsAwaitingSlot && !_draftPanel.IsAwaitingTower))
		{
			_placementLabel.Visible = false;
			return;
		}
		_placementLabel.Text = _draftPanel.PlacementHint;
		_placementLabel.Visible = true;
	}

	public void NotifyModifierProc(TowerInstance tower, string modifierId)
	{
		int slot = -1;
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			if (_runState.Slots[i].Tower == tower) { slot = i; break; }
		}
		if (slot < 0) return;

		_slotProcHaloRemaining[slot] = Mathf.Max(_slotProcHaloRemaining[slot], 0.20f);
		_slotProcHaloColor[slot] = ModifierVisuals.GetAccent(modifierId);

		for (int p = 0; p < Balance.MaxModifiersPerTower; p++)
		{
			if (p >= tower.Modifiers.Count) break;
			if (tower.Modifiers[p].ModifierId != modifierId) continue;
			_slotModIconPulseRemaining[slot, p] = Mathf.Max(_slotModIconPulseRemaining[slot, p], 0.24f);
		}
	}

	public void TriggerHitStop(float realDuration = 0.042f, float slowScale = 0.20f)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave) return;
		if (_hitStopActive || _hitStopCooldown > 0f) return;

		_hitStopActive = true;
		_hitStopCooldown = 0.08f;
		_preHitStopTimeScale = Engine.TimeScale;
		float targetScale = Mathf.Min((float)_preHitStopTimeScale, slowScale);
		Engine.TimeScale = targetScale;

		float scaledWait = Mathf.Max(0.005f, realDuration * targetScale);
		GetTree().CreateTimer(scaledWait).Timeout += () =>
		{
			Engine.TimeScale = _preHitStopTimeScale;
			_hitStopActive = false;
		};
	}

	private void UpdateProcVisuals(float delta)
	{
		if (_hitStopCooldown > 0f)
			_hitStopCooldown = Mathf.Max(0f, _hitStopCooldown - delta);

		for (int i = 0; i < Balance.SlotCount; i++)
		{
			var halo = _slotProcHalos[i];
			if (GodotObject.IsInstanceValid(halo))
			{
				float rem = _slotProcHaloRemaining[i];
				if (rem > 0f)
				{
					rem = Mathf.Max(0f, rem - delta);
					_slotProcHaloRemaining[i] = rem;
					float t = rem / 0.20f;
					float pulse = 0.65f + 0.35f * Mathf.Sin((1f - t) * 18f);
					halo.DefaultColor = _slotProcHaloColor[i];
					halo.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.55f * t * pulse);
				}
				else
				{
					halo.Modulate = Colors.Transparent;
				}
			}

			var icons = _slotModIcons[i];
			if (icons == null) continue;
			for (int p = 0; p < icons.Length; p++)
			{
				var icon = icons[p];
				if (!GodotObject.IsInstanceValid(icon) || !icon.Visible) continue;

				float rem = _slotModIconPulseRemaining[i, p];
				if (rem > 0f)
				{
					rem = Mathf.Max(0f, rem - delta);
					_slotModIconPulseRemaining[i, p] = rem;
					float t = rem / 0.24f;
					float amp = 1f + 0.35f * t * (0.5f + 0.5f * Mathf.Sin((1f - t) * 20f));
					icon.Scale = new Vector2(amp, amp);
					icon.Modulate = new Color(1.15f, 1.15f, 1.15f, 1f);
				}
				else
				{
					icon.Scale = Vector2.One;
					icon.Modulate = new Color(1f, 1f, 1f, 0.95f);
				}
			}
		}
	}

	private void RefreshModPips(int slotIndex)
	{
		var pips = _slotModPips[slotIndex];
		if (pips == null) return;
		var icons = _slotModIcons[slotIndex];
		var tower = _runState.Slots[slotIndex].Tower;
		int filled = tower?.Modifiers.Count ?? 0;
		bool atMax = filled >= Balance.MaxModifiersPerTower;
		for (int p = 0; p < pips.Length; p++)
		{
			pips[p].Visible = tower != null;
			pips[p].Color = p < filled
				? (atMax ? new Color(1.00f, 0.60f, 0.05f) : new Color(0.30f, 0.95f, 0.40f))
				: new Color(0.22f, 0.22f, 0.22f, 0.45f);

			if (icons != null)
			{
				if (tower != null && p < filled && p < tower.Modifiers.Count)
				{
					var modId = tower.Modifiers[p].ModifierId;
					icons[p].ModifierId = modId;
					icons[p].IconColor = ModifierVisuals.GetAccent(modId);
					icons[p].Modulate = new Color(1f, 1f, 1f, 0.95f);
					icons[p].Visible = true;
				}
				else
				{
					icons[p].Visible = false;
				}
			}
		}
	}

	private void UpdateModifierPreviewGhost(float delta)
	{
		int slot = _draftPanel.ModifierPreviewSlot;
		if (slot < 0 || slot >= _slotModPips.Length)
		{
			ClearModifierPreviewGhost();
			return;
		}

		if (_previewGhostSlot != slot)
		{
			if (_previewGhostSlot >= 0)
			{
				RefreshModPips(_previewGhostSlot);
				if (GodotObject.IsInstanceValid(_slotPreviewGlows[_previewGhostSlot]))
					_slotPreviewGlows[_previewGhostSlot].Modulate = Colors.Transparent;
			}
			_previewGhostSlot = slot;
			_previewGhostPhase = 0f;
		}
		else
		{
			_previewGhostPhase += delta;
		}

		var tower = _runState.Slots[slot].Tower;
		if (tower == null) return;
		int filled = tower.Modifiers.Count;
		if (filled >= Balance.MaxModifiersPerTower || filled < 0) return;

		RefreshModPips(slot);
		var pip = _slotModPips[slot][filled];
		var accent = ModifierVisuals.GetAccent(_draftPanel.PendingModifierId);
		float pulse = 0.5f + 0.5f * Mathf.Sin(_previewGhostPhase * 9f);
		pip.Visible = true;
		pip.Color = new Color(accent.R, accent.G, accent.B, 0.40f + pulse * 0.30f);

		// Persistent slot glow for the currently previewed tower.
		if (GodotObject.IsInstanceValid(_slotPreviewGlows[slot]))
		{
			var glow = _slotPreviewGlows[slot];
			glow.DefaultColor = new Color(accent.R, accent.G, accent.B, 0.95f);
			glow.Modulate = new Color(1f, 1f, 1f, 0.30f + pulse * 0.45f);
		}

		// Semi-transparent preview icon that sits on the tower.
		if (!GodotObject.IsInstanceValid(_previewModifierIcon))
		{
			_previewModifierIcon = new ModifierIcon
			{
				Size = new Vector2(18f, 18f),
				CustomMinimumSize = new Vector2(18f, 18f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
		}
		if (_previewModifierIcon.GetParent() != _slotNodes[slot])
		{
			_previewModifierIcon.GetParent()?.RemoveChild(_previewModifierIcon);
			_slotNodes[slot].AddChild(_previewModifierIcon);
		}
		_previewModifierIcon.Position = new Vector2(-9f, -9f);
		_previewModifierIcon.ModifierId = _draftPanel.PendingModifierId;
		_previewModifierIcon.IconColor = accent;
		_previewModifierIcon.Modulate = new Color(1f, 1f, 1f, 0.35f + pulse * 0.35f);
		_previewModifierIcon.Visible = true;
	}

	private void ClearModifierPreviewGhost()
	{
		if (_previewGhostSlot >= 0)
			RefreshModPips(_previewGhostSlot);

		for (int i = 0; i < _slotPreviewGlows.Length; i++)
		{
			if (GodotObject.IsInstanceValid(_slotPreviewGlows[i]))
				_slotPreviewGlows[i].Modulate = Colors.Transparent;
		}

		if (GodotObject.IsInstanceValid(_previewModifierIcon))
			_previewModifierIcon.Visible = false;

		_previewGhostSlot = -1;
		_previewGhostPhase = 0f;
	}

	private void ShowWaveClearFlash()
	{
		_waveClearFlash.Color   = new Color(0.10f, 1f, 0.45f, 0f);
		_waveClearFlash.Visible = true;
		var tween = CreateTween();
		tween.TweenProperty(_waveClearFlash, "color", new Color(0.10f, 1f, 0.45f, 0.18f), 0.08f);
		tween.TweenProperty(_waveClearFlash, "color", new Color(0.10f, 1f, 0.45f, 0f),    0.40f)
			 .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => _waveClearFlash.Visible = false));
	}

}




