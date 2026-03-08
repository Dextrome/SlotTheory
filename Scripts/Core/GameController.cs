using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core.Leaderboards;
using SlotTheory.Core.Naming;
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
	private Line2D[]      _slotSynergyGlows    = new Line2D[Balance.SlotCount];
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
	private PathFlow? _pathFlow;
	private Panel _tooltipPanel = null!;
	private Label _tooltipLabel = null!;
	private int _mobileTooltipFontSize = 13;
	private float _mobileTooltipUiScale = 1f;
	private TowerInstance? _selectedTooltipTower;
	private CanvasLayer _announceLayer = null!;
	private Label _waveAnnounce = null!;
	private Label _halfwayBeat = null!;
	private Label _threatWarn = null!;
	private Label _placementLabel = null!;
	private Button _undoPlacementButton = null!;
	private Label _clutchToast = null!;
	private ColorRect _waveClearFlash = null!;
	private ColorRect _threatPulse = null!;
	private Node2D _worldNode = null!;
	private BotRunner? _botRunner;
	private int _extraPicksRemaining;
	private List<DraftOption>? _currentDraftOptions;
	private WaveReport? _lastWaveReport;
	private int _previewGhostSlot = -1;
	private float _previewGhostPhase = 0f;
	private ModifierIcon? _previewModifierIcon;
	private bool _runAbandoned = false;  // set on intentional exit to suppress _ExitTree re-save
	private bool _hitStopActive = false;
	private double _preHitStopTimeScale = 1.0;
	private float _hitStopCooldown = 0f;
	private string _draftSynergyHintModifierId = "";
	private float _draftSynergyPulseT = 0f;
	private float _lowLivesHeartbeatTimer = 0f;
	private readonly System.Collections.Generic.Dictionary<string, ulong> _combatCalloutNextMs = new();
	private readonly System.Collections.Generic.Dictionary<ulong, (ulong firstMs, int count)> _feedbackLoopBurst = new();
	private readonly System.Collections.Generic.Dictionary<string, int> _modifierProcCounts = new();
	private bool _undoPlacementActive = false;
	private ulong _undoPlacementToken = 0;
	private int _undoPlacementSlot = -1;
	private List<DraftOption> _undoDraftOptions = new();
	private int _undoDraftWave = 1;
	private int _undoDraftPick = 1;
	private int _undoDraftTotal = 1;
	private System.Action? _undoPlacementCommit;
	private Camera2D? _mobileCamera;
	private Rect2 _mobileCameraBounds = new Rect2(0f, 0f, 1280f, 720f);
	private float _mobileZoomLevel = 1f;
	private Vector2 _mobileLastViewportSize = Vector2.Zero;
	private readonly Dictionary<int, Vector2> _mobileTouchPositions = new();
	private readonly Dictionary<int, Vector2> _mobileTouchStartPositions = new();
	private bool _mobileTapSuppressed = false;
	private bool _mobilePinchActive = false;
	private float _mobilePinchStartDistance = 1f;
	private float _mobilePinchStartZoom = 1f;
	private Vector2 _mobilePinchLastMidpoint = Vector2.Zero;
	private bool _mobileCameraDirectZoom = true;
	private MobileRunSnapshot? _mobileResumeSnapshot;
	private bool _isRestoringSnapshot = false;
	private const float MobileMinZoom = 1.0f;
	private const float MobileMaxZoom = 2.6f;
	private const float MobileTapMoveThreshold = 18f;
	private const float MobilePanStartThreshold = 6f;

	public override void _Ready()
	{
		Instance = this;
		DataLoader.LoadAll();
		// Bot playtest mode: godot --headless --path ... -- --bot --runs N --difficulty normal|hard
		var userArgs = OS.GetCmdlineUserArgs();
		if (userArgs.Contains("--bot"))
		{
			int runs = 50;
			int ri = System.Array.IndexOf(userArgs, "--runs");
			if (ri >= 0 && ri + 1 < userArgs.Length)
				int.TryParse(userArgs[ri + 1], out runs);
			
			DifficultyMode? targetDifficulty = null;
			int di = System.Array.IndexOf(userArgs, "--difficulty");
			if (di >= 0 && di + 1 < userArgs.Length)
			{
				var diffStr = userArgs[di + 1].ToLower();
				if (diffStr == "normal") targetDifficulty = DifficultyMode.Normal;
				else if (diffStr == "hard") targetDifficulty = DifficultyMode.Hard;
			}
			
			_botRunner = new BotRunner(runs, targetDifficulty);
			Engine.MaxFps = 0;
			GD.Print($"[BOT] Headless playtest: {runs} runs{(targetDifficulty.HasValue ? $" ({targetDifficulty.Value})" : "")}");
		}

		_runState = new RunState();
		
		// Apply pending map selection from MapSelectPanel if available
		_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
		if (_runState.SelectedMapId == "random_map" && SlotTheory.UI.MapSelectPanel.PendingProceduralSeed > 0)
			_runState.RngSeed = (int)SlotTheory.UI.MapSelectPanel.PendingProceduralSeed;
		LoadPendingMobileSnapshot();
		
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
			SetupMobileCamera();
			SetupSlots();
			SetupTooltip();
			SetupAnnouncer();

		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		_lastWaveReport = null;
		if (!TryRestoreMobileSnapshot())
			StartDraftPhase();
	}

	public override void _Process(double delta)
	{
		if (MobileOptimization.IsMobile() && GodotObject.IsInstanceValid(_mobileCamera))
		{
			var vpSize = GetViewport().GetVisibleRect().Size;
			if (!_mobileLastViewportSize.IsEqualApprox(vpSize))
			{
				_mobileLastViewportSize = vpSize;
				ClampMobileCameraToBounds();
			}
		}

		if (_botRunner == null) UpdateTooltip();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
			UpdateSlotHighlights();
		else if (_highlightedSlot != -1)
			ClearSlotHighlights();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && _draftPanel.IsAwaitingTower)
			UpdateModifierPreviewGhost((float)delta);
		else
			ClearModifierPreviewGhost();

		if (CurrentPhase == GamePhase.Draft && !_draftPanel.IsAwaitingSlot && !_draftPanel.IsAwaitingTower)
			UpdateDraftSynergyHighlights((float)delta);
		else
			ClearDraftSynergyHighlights();

		if (_botRunner == null) UpdatePlacementLabel();
		if (_botRunner == null) UpdateProcVisuals((float)delta);

		if (CurrentPhase != GamePhase.Wave)
		{
			_lowLivesHeartbeatTimer = 0f;
			return;
		}

		if (_botRunner != null) { BotTick(); return; }

		int livesBefore = _runState.Lives;
		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		_hudPanel.RefreshTime(_runState.TotalPlayTime);
		_hudPanel.RefreshEnemies(_runState.EnemiesAlive.Count, _waveSystem.GetTotalCount());
		if (_runState.Lives < livesBefore)
		{
			_hudPanel.FlashLives();
			ShakeWorld();
			MobileOptimization.HapticStrong();
			if (_runState.Lives <= 2)
				ShowClutchToast(_runState.Lives <= 1 ? "TOO CLOSE" : "CLUTCH");
		}
		UpdateLowLivesTension((float)delta);

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			MobileRunSession.Clear();
			MobileOptimization.HapticStrong();
			AchievementManager.Instance?.CheckRunEnd(_runState, SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal, won: false);
			int livesLost = Balance.StartingLives - _runState.Lives;
			SoundManager.Instance?.Play("game_over");
				string runName = BuildRunName(registerInHistory: true, wonOverride: false, waveReachedOverride: _runState.WaveIndex + 1);
			var runColors = BuildRunNameColors();
			string mvpLine = BuildMvpLine();
			string modLine = BuildMostValuableModLine();
			var scorePayload = BuildRunScorePayload(won: false, waveReached: _runState.WaveIndex + 1);
			var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
			string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
			_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost, _runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), _runState, runName, mvpLine, modLine, runColors.start, runColors.end);
			_endScreen.SetLeaderboardStatus(leaderboardLine);
			QueueGlobalSubmit(scorePayload, leaderboardLine);
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
				MobileRunSession.Clear();
				MobileOptimization.HapticStrong();
				AchievementManager.Instance?.CheckRunEnd(_runState, SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal, won: true);
				SoundManager.Instance?.Play("victory");
					string runName = BuildRunName(registerInHistory: true, wonOverride: true, waveReachedOverride: Balance.TotalWaves);
				var runColors = BuildRunNameColors();
				string mvpLine = BuildMvpLine();
				string modLine = BuildMostValuableModLine();
				var scorePayload = BuildRunScorePayload(won: true, waveReached: Balance.TotalWaves);
				var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
				string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
				_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
				_endScreen.ShowWin(_runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), runName, mvpLine, modLine, runColors.start, runColors.end);
				_endScreen.SetLeaderboardStatus(leaderboardLine);
				QueueGlobalSubmit(scorePayload, leaderboardLine);
			}
			else
			{
				if (_botRunner == null) ShowWaveClearFlash();
				MobileOptimization.HapticMedium();
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
		if (!MobileOptimization.IsMobile())
			return;

		switch (what)
		{
			case (int)NotificationApplicationPaused:
				// Auto-pause when app goes to background (minimized/phone call/etc).
				if (CurrentPhase == GamePhase.Wave && GetTree().Paused == false)
				{
					GD.Print("App paused - auto-pausing game");
					var pauseScreen = GetNode<PauseScreen>("../PauseScreen");
					pauseScreen?.Pause();
				}
				SaveMobileRunSnapshot("app_paused");
				break;

			case (int)NotificationApplicationResumed:
				// App returned from background - game stays paused for user control.
				GD.Print("App resumed from background");
				break;
		}
	}

	public override void _ExitTree()
	{
		if (!_runAbandoned)
			SaveMobileRunSnapshot("exit_tree");
	}

	/// <summary>
	/// Call before intentionally navigating away (Main Menu button, quit confirm Yes).
	/// Clears the mobile session and prevents _ExitTree from re-saving it.
	/// </summary>
	public void AbandonRun()
	{
		_runAbandoned = true;
		MobileRunSession.Clear();
	}

	private void LoadPendingMobileSnapshot()
	{
		if (!MobileOptimization.IsMobile())
			return;
		if (!MobileRunSession.TryLoad(out var snapshot))
			return;

		_mobileResumeSnapshot = snapshot;
		_runState.SelectedMapId = string.IsNullOrWhiteSpace(snapshot.MapId)
			? LeaderboardKey.RandomMapId
			: snapshot.MapId;
		_runState.RngSeed = snapshot.RngSeed;
		_runState.WaveIndex = Mathf.Clamp(snapshot.WaveIndex, 0, Balance.TotalWaves - 1);
		_runState.Lives = Mathf.Clamp(snapshot.Lives, 0, Balance.StartingLives);
		_runState.TotalKills = Mathf.Max(0, snapshot.TotalKills);
		_runState.TotalDamageDealt = Mathf.Max(0, snapshot.TotalDamageDealt);
		_runState.TotalPlayTime = Mathf.Max(0f, snapshot.TotalPlayTime);
	}

	private bool TryRestoreMobileSnapshot()
	{
		var snapshot = _mobileResumeSnapshot;
		_mobileResumeSnapshot = null;
		if (snapshot == null)
			return false;

		try
		{
			_isRestoringSnapshot = true;
			foreach (var slot in snapshot.Slots.OrderBy(s => s.SlotIndex))
			{
				if (slot.SlotIndex < 0 || slot.SlotIndex >= _runState.Slots.Length)
					continue;
				if (string.IsNullOrWhiteSpace(slot.TowerId))
					continue;

				PlaceTower(slot.TowerId, slot.SlotIndex);
				var tower = _runState.Slots[slot.SlotIndex].Tower;
				if (tower == null)
					continue;

				foreach (string modifierId in slot.ModifierIds)
				{
					if (tower.Modifiers.Count >= Balance.MaxModifiersPerTower)
						break;
					if (string.IsNullOrWhiteSpace(modifierId))
						continue;
					_draftSystem.ApplyModifier(modifierId, tower);
				}

				RefreshModPips(slot.SlotIndex);
			}
		}
		catch (System.Exception ex)
		{
			GD.PrintErr($"[MobileSession] Failed to restore snapshot: {ex.Message}");
			MobileRunSession.Clear();
			_isRestoringSnapshot = false;
			return false;
		}
		finally
		{
			_isRestoringSnapshot = false;
		}

		// Restore exact pick position within the draft (not just recalculate from wave index)
		_extraPicksRemaining = snapshot.Phase == "draft"
			? snapshot.ExtraPicksRemaining
			: Balance.ExtraPicksForWave(_runState.WaveIndex);

		// Restore the exact draft options that were showing — prevents reload-to-reroll exploit
		if (snapshot.Phase == "draft" && snapshot.DraftOptions.Count > 0)
		{
			_currentDraftOptions = snapshot.DraftOptions
				.Select(o => new DraftOption(
					o.Type == "tower" ? DraftOptionType.Tower : DraftOptionType.Modifier,
					o.Id))
				.ToList();
		}

		_lastWaveReport = null;
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);

		if (snapshot.Phase == "wave")
			StartWavePhase();
		else
			StartDraftPhase();

		SaveMobileRunSnapshot("restored");
		GD.Print("[MobileSession] Restored active run snapshot.");
		return true;
	}

	private void SaveMobileRunSnapshot(string reason)
	{
		if (!MobileOptimization.IsMobile())
			return;
		if (!MobileRunSession.IsActiveRunPhase(CurrentPhase))
			return;

		MobileRunSession.Save(CurrentPhase, _runState, _extraPicksRemaining,
			CurrentPhase == GamePhase.Draft ? _currentDraftOptions : null);
		GD.Print($"[MobileSession] Snapshot saved ({reason}).");
	}

	public void StartDraftPhase()
	{
		ClearUndoPlacementState();
		CurrentPhase = GamePhase.Draft;
		_hudPanel.SetBuildName("", visible: false);
		// Use restored options if available (prevents reload-to-reroll), otherwise generate fresh
		var options = _currentDraftOptions ?? _draftSystem.GenerateOptions(_runState);
		_currentDraftOptions = options;

		// All slots full AND all towers at modifier cap ? nothing to offer, skip draft.
		if (options.Count == 0)
		{
			_currentDraftOptions = null;
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
		SaveMobileRunSnapshot("start_draft");
	}

	public RunState GetRunState() => _runState;
	public string GetCurrentRunName() => BuildRunName();

	/// <summary>Wipe all in-flight state and restart from wave 1 draft.</summary>
	public void RestartRun()
	{
		MobileRunSession.Clear();

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
		
		_runAbandoned = false;
		_runState.Reset();
		_combatSim.ResetForWave();
		_endScreen.Visible = false;
		Engine.TimeScale = 1.0;   // always reset speed on new run
		SoundManager.Instance?.SetMusicTension(0f);
		_hitStopActive = false;
		_hitStopCooldown = 0f;
		_preHitStopTimeScale = 1.0;
		_draftSynergyHintModifierId = "";
		_draftSynergyPulseT = 0f;
		_lowLivesHeartbeatTimer = 0f;
		_combatCalloutNextMs.Clear();
		_feedbackLoopBurst.Clear();
		_modifierProcCounts.Clear();
		ClearUndoPlacementState();
		_hudPanel.Refresh(1, Balance.StartingLives);
		_hudPanel.SetBuildName("", visible: false);
		_hudPanel.ResetSpeed();

			// In bot mode BotRunner.StartNextRun() already called SetPendingMapSelection
			// before RestartRun() — pick it up here since _Ready() only runs once.
			if (_botRunner != null)
				_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;

			ClearMapVisuals();
			GenerateMap();
			SetupMobileCamera();
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
		_currentDraftOptions = null; // always generate fresh options for the next pick

		if (option.Type == DraftOptionType.Tower)
		{
			if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length && _runState.Slots[targetSlotIndex].Tower == null)
			{
				PlaceTower(option.Id, targetSlotIndex);
				// Tower placement haptic fired inside PlaceTower
			}
		}
		else
		{
			var tower = _runState.Slots[targetSlotIndex].Tower;
			if (tower != null)
			{
				_draftSystem.ApplyModifier(option.Id, tower);
				if (_botRunner == null) RefreshModPips(targetSlotIndex);
				MobileOptimization.HapticLight(); // modifier equipped
			}
		}
		AdvanceAfterDraftPickFlow();
	}

	private void AdvanceAfterDraftPickFlow()
	{
		if (_extraPicksRemaining > 0)
		{
			_extraPicksRemaining--;
			StartDraftPhase();
			return;
		}
		StartWavePhase();
	}

	private void BeginTowerUndoWindow(
		int slotIndex,
		List<DraftOption> draftOptions,
		int waveNumber,
		int pickNumber,
		int totalPicks,
		System.Action onCommit)
	{
		_undoPlacementToken++;
		_undoPlacementActive = true;
		_undoPlacementSlot = slotIndex;
		_undoDraftOptions = draftOptions;
		_undoDraftWave = waveNumber;
		_undoDraftPick = pickNumber;
		_undoDraftTotal = totalPicks;
		_undoPlacementCommit = onCommit;

		if (GodotObject.IsInstanceValid(_undoPlacementButton))
		{
			_undoPlacementButton.Visible = true;
			_undoPlacementButton.Scale = new Vector2(1.08f, 1.08f);
			_undoPlacementButton.Modulate = new Color(0.92f, 0.98f, 1.00f, 0f);
			var tw = _undoPlacementButton.CreateTween();
			tw.SetParallel(true);
			tw.TweenProperty(_undoPlacementButton, "modulate:a", 1f, 0.08f);
			tw.TweenProperty(_undoPlacementButton, "scale", Vector2.One, 0.10f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		ulong token = _undoPlacementToken;
		GetTree().CreateTimer(2.0f).Timeout += () =>
		{
			if (!_undoPlacementActive || token != _undoPlacementToken) return;
			CommitPendingPlacement();
		};
	}

	private void CommitPendingPlacement()
	{
		if (!_undoPlacementActive) return;
		_undoPlacementActive = false;
		if (GodotObject.IsInstanceValid(_undoPlacementButton))
			_undoPlacementButton.Visible = false;
		var commit = _undoPlacementCommit;
		_undoPlacementCommit = null;
		commit?.Invoke();
	}

	private void ClearUndoPlacementState()
	{
		_undoPlacementActive = false;
		_undoPlacementToken++;
		_undoPlacementSlot = -1;
		_undoDraftOptions = new();
		_undoDraftWave = 1;
		_undoDraftPick = 1;
		_undoDraftTotal = 1;
		_undoPlacementCommit = null;
		if (GodotObject.IsInstanceValid(_undoPlacementButton))
			_undoPlacementButton.Visible = false;
	}

	private void OnUndoPlacementPressed()
	{
		if (!_undoPlacementActive || _undoPlacementSlot < 0 || _undoPlacementSlot >= _runState.Slots.Length)
			return;

		SoundManager.Instance?.Play("ui_select");
		int slot = _undoPlacementSlot;
		var opts = _undoDraftOptions;
		int wave = _undoDraftWave;
		int pick = _undoDraftPick;
		int total = _undoDraftTotal;
		ClearUndoPlacementState();

		RemoveTowerAtSlot(slot);
		CurrentPhase = GamePhase.Draft;
		_draftPanel.Show(opts, wave, pick, total, null);
	}

	private void RemoveTowerAtSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _runState.Slots.Length) return;
		var tower = _runState.Slots[slotIndex].Tower;
		if (tower == null) return;
		if (_selectedTooltipTower == tower)
			_selectedTooltipTower = null;

		if (GodotObject.IsInstanceValid(tower.ModeIconControl))
			tower.ModeIconControl.QueueFree();
		if (GodotObject.IsInstanceValid(tower.ModeBadgeControl))
			tower.ModeBadgeControl.QueueFree();
		if (GodotObject.IsInstanceValid(tower.ModeBadgeBorder))
			tower.ModeBadgeBorder.QueueFree();
		if (GodotObject.IsInstanceValid(tower))
			tower.QueueFree();

		_runState.Slots[slotIndex].Tower = null;
		RefreshModPips(slotIndex);
	}

	/// <summary>Place a tower by ID into a slot. Called by draft UI later.</summary>
	public void PlaceTower(string towerId, int slotIndex)
	{
		if (_runState.Slots[slotIndex].Tower != null) return;

		MobileOptimization.HapticMedium();
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
		var rangeCircle = new Polygon2D { Color = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, 0.08f) };
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
			DefaultColor = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, 0.22f),
		};
		tower.RangeBorder = rangeBorder;
		tower.AddChild(rangeBorder);

		// Targeting mode badge — always visible on the right side of each tower.
		var modeBadge = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.09f, 0.86f),
			Position = new Vector2(30f, -11f),
			Size = new Vector2(18f, 18f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_slotNodes[slotIndex].AddChild(modeBadge);
		tower.ModeBadgeControl = modeBadge;
		var modeBadgeBorder = new Line2D
		{
			Points = new[]
			{
				new Vector2(30f, -11f), new Vector2(48f, -11f), new Vector2(48f, 7f),
				new Vector2(30f, 7f), new Vector2(30f, -11f)
			},
			Width = 1.6f,
			DefaultColor = new Color(0.68f, 0.94f, 1.00f, 0.90f),
			Antialiased = true,
		};
		_slotNodes[slotIndex].AddChild(modeBadgeBorder);
		tower.ModeBadgeBorder = modeBadgeBorder;
		var modeIcon = new TargetModeIcon
		{
			Mode = TargetingMode.First,
			IconColor = new Color(0.95f, 0.98f, 1.00f),
			Position = new Vector2(2f, 2f),
			Size = new Vector2(14f, 14f),
			CustomMinimumSize = new Vector2(14f, 14f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		modeBadge.AddChild(modeIcon);
		tower.ModeIconControl = modeIcon;

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;

		// Placement bounce — scale from 0 ? 1.15 ? 1.0
		if (_botRunner == null && !_isRestoringSnapshot)
		{
			tower.Scale = Vector2.Zero;
			var placeTween = tower.CreateTween();
			placeTween.TweenProperty(tower, "scale", new Vector2(1.15f, 1.15f), 0.15f)
			          .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			placeTween.TweenProperty(tower, "scale", Vector2.One, 0.10f)
			          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		}

		RefreshModPips(slotIndex);
		if (!_isRestoringSnapshot)
			SoundManager.Instance?.Play("tower_place");
	}

	public override void _Input(InputEvent @event)
	{
		Vector2 pressPos;
		if (MobileOptimization.IsMobile())
		{
			if (!TryGetMobileGameplayTap(@event, out pressPos))
				return;
		}
		else if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			pressPos = ScreenToWorld(GetViewport().GetMousePosition());
		}
		else if (@event is InputEventScreenTouch { Pressed: true } touch)
		{
			pressPos = ScreenToWorld(touch.Position);
		}
		else
		{
			return;
		}

		HandleGameplayPress(pressPos);
	}

	private void HandleGameplayPress(Vector2 pressPos)
	{
		if (_undoPlacementActive)
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

	private bool TryGetMobileGameplayTap(InputEvent @event, out Vector2 worldTapPos)
	{
		worldTapPos = Vector2.Zero;

		switch (@event)
		{
			case InputEventScreenTouch touch:
			{
				if (touch.Pressed)
				{
					_mobileTouchPositions[touch.Index] = touch.Position;
					_mobileTouchStartPositions[touch.Index] = touch.Position;
					if (_mobileTouchPositions.Count >= 2)
					{
						_mobileTapSuppressed = true;
						StartMobilePinch();
					}
					return false;
				}

				bool wasSingleTouch = _mobileTouchPositions.Count == 1 && _mobileTouchPositions.ContainsKey(touch.Index);
				Vector2 startPos = _mobileTouchStartPositions.TryGetValue(touch.Index, out var storedStart) ? storedStart : touch.Position;
				float moved = startPos.DistanceTo(touch.Position);

				_mobileTouchPositions.Remove(touch.Index);
				_mobileTouchStartPositions.Remove(touch.Index);

				if (_mobileTouchPositions.Count == 0)
				{
					bool isTap = wasSingleTouch && !_mobileTapSuppressed && moved <= MobileTapMoveThreshold;
					ResetMobileGestureState();
					if (isTap)
					{
						worldTapPos = ScreenToWorld(touch.Position);
						return true;
					}
					return false;
				}

				if (_mobileTouchPositions.Count == 1)
				{
					_mobilePinchActive = false;
					int remainingId = _mobileTouchPositions.Keys.First();
					_mobileTouchStartPositions[remainingId] = _mobileTouchPositions[remainingId];
				}
				else
				{
					StartMobilePinch();
				}
				return false;
			}

			case InputEventScreenDrag drag:
			{
				if (!_mobileTouchPositions.ContainsKey(drag.Index))
					return false;

				Vector2 prevPos = _mobileTouchPositions[drag.Index];
				_mobileTouchPositions[drag.Index] = drag.Position;

				if (_mobileTouchPositions.Count >= 2)
				{
					if (!_mobilePinchActive)
						StartMobilePinch();
					UpdateMobilePinch();
					_mobileTapSuppressed = true;
					GetViewport().SetInputAsHandled();
					return false;
				}

				if (GodotObject.IsInstanceValid(_mobileCamera) && _mobileZoomLevel > MobileMinZoom + 0.001f)
				{
					Vector2 delta = drag.Position - prevPos;
					if (delta.Length() >= MobilePanStartThreshold)
						_mobileTapSuppressed = true;

					if (_mobileTapSuppressed)
					{
						PanMobileCameraByScreenDelta(delta);
						GetViewport().SetInputAsHandled();
					}
				}
				return false;
			}
		}

		return false;
	}

	private void StartMobilePinch()
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera) || _mobileTouchPositions.Count < 2)
		{
			_mobilePinchActive = false;
			return;
		}

		var points = _mobileTouchPositions.Values.Take(2).ToArray();
		_mobilePinchStartDistance = Mathf.Max(1f, points[0].DistanceTo(points[1]));
		_mobilePinchStartZoom = _mobileZoomLevel;
		_mobilePinchLastMidpoint = (points[0] + points[1]) * 0.5f;
		_mobilePinchActive = true;
	}

	private void UpdateMobilePinch()
	{
		if (!_mobilePinchActive || !GodotObject.IsInstanceValid(_mobileCamera) || _mobileTouchPositions.Count < 2)
			return;

		var points = _mobileTouchPositions.Values.Take(2).ToArray();
		Vector2 midpoint = (points[0] + points[1]) * 0.5f;
		float distance = Mathf.Max(1f, points[0].DistanceTo(points[1]));

		Vector2 midDelta = midpoint - _mobilePinchLastMidpoint;
		if (midDelta.LengthSquared() > 0.0001f)
			PanMobileCameraByScreenDelta(midDelta);
		_mobilePinchLastMidpoint = midpoint;

		float scale = distance / _mobilePinchStartDistance;
		float nextZoom = Mathf.Clamp(_mobilePinchStartZoom * scale, MobileMinZoom, MobileMaxZoom);
		Vector2 worldBefore = ScreenToWorld(midpoint);
		ApplyMobileZoom(nextZoom);
		Vector2 worldAfter = ScreenToWorld(midpoint);

		if (GodotObject.IsInstanceValid(_mobileCamera))
		{
			_mobileCamera.Position += worldBefore - worldAfter;
			ClampMobileCameraToBounds();
		}
	}

	private void PanMobileCameraByScreenDelta(Vector2 deltaScreen)
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return;

		float worldPerScreenUnit = 1f / Mathf.Max(0.001f, _mobileZoomLevel);
		_mobileCamera.Position -= deltaScreen * worldPerScreenUnit;
		ClampMobileCameraToBounds();
	}

	private void SetupMobileCamera()
	{
		if (!MobileOptimization.IsMobile())
			return;

		if (!GodotObject.IsInstanceValid(_mobileCamera))
		{
			_mobileCamera = _worldNode.GetNodeOrNull<Camera2D>("MobileCamera");
			if (!GodotObject.IsInstanceValid(_mobileCamera))
			{
				_mobileCamera = new Camera2D
				{
					Name = "MobileCamera",
					Enabled = true,
					ProcessCallback = Camera2D.Camera2DProcessCallback.Idle,
					PositionSmoothingEnabled = false,
				};
				_worldNode.AddChild(_mobileCamera);
			}
		}

		_mobileCamera.AnchorMode = Camera2D.AnchorModeEnum.DragCenter;
		_mobileCamera.IgnoreRotation = true;
		_mobileCamera.Offset = Vector2.Zero;
		_mobileCameraBounds = ComputeMobileCameraBounds();
		CalibrateMobileCameraZoomMode();
		ApplyMobileZoom(1f);
		Vector2 boundsCenter = _mobileCameraBounds.Position + (_mobileCameraBounds.Size * 0.5f);
		_mobileCamera.Position = ClampMobileCameraPosition(boundsCenter);
		_mobileCamera.MakeCurrent();
		_mobileLastViewportSize = GetViewport().GetVisibleRect().Size;
		ResetMobileGestureState();
	}

	private void ApplyMobileZoom(float zoomLevel)
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return;

		_mobileZoomLevel = Mathf.Clamp(zoomLevel, MobileMinZoom, MobileMaxZoom);
		float cameraZoom = _mobileCameraDirectZoom
			? _mobileZoomLevel
			: 1f / Mathf.Max(0.001f, _mobileZoomLevel);
		_mobileCamera.Zoom = new Vector2(cameraZoom, cameraZoom);
		ApplyMobileZoomReadability();
		ClampMobileCameraToBounds();
	}

	private void ApplyMobileZoomReadability()
	{
		if (!MobileOptimization.IsMobile())
			return;

		float t = Mathf.Clamp((_mobileZoomLevel - MobileMinZoom) / (MobileMaxZoom - MobileMinZoom), 0f, 1f);
		float combatScale = Mathf.Lerp(1.0f, 1.65f, t);
		DamageNumber.SetMobileReadabilityScale(combatScale);
		CombatCallout.SetMobileReadabilityScale(combatScale);

		if (GodotObject.IsInstanceValid(_hudPanel))
			_hudPanel.SetMobileZoomReadability(_mobileZoomLevel);

		if (GodotObject.IsInstanceValid(_tooltipLabel))
		{
			_mobileTooltipUiScale = Mathf.Lerp(1.0f, 2.2f, t);
			int targetSize = Mathf.RoundToInt(13f * _mobileTooltipUiScale);
			if (targetSize != _mobileTooltipFontSize)
			{
				_mobileTooltipFontSize = targetSize;
				UITheme.ApplyFont(_tooltipLabel, size: _mobileTooltipFontSize);
			}
			_tooltipLabel.Position = new Vector2(8f * _mobileTooltipUiScale, 6f * _mobileTooltipUiScale);
		}
	}

	private void CalibrateMobileCameraZoomMode()
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return;

		Vector2 prevZoom = _mobileCamera.Zoom;
		Vector2 prevPos = _mobileCamera.Position;

		Vector2 center = _mobileCameraBounds.Position + (_mobileCameraBounds.Size * 0.5f);
		_mobileCamera.Position = center;
		_mobileCamera.Zoom = Vector2.One;

		Vector2 vp = GetViewport().GetVisibleRect().Size;
		Vector2 a = vp * 0.5f + new Vector2(-120f, 0f);
		Vector2 b = vp * 0.5f + new Vector2(120f, 0f);
		float worldSpanAt1 = ScreenToWorld(a).DistanceTo(ScreenToWorld(b));

		_mobileCamera.Zoom = new Vector2(1.2f, 1.2f);
		float worldSpanAt12 = ScreenToWorld(a).DistanceTo(ScreenToWorld(b));

		_mobileCameraDirectZoom = worldSpanAt12 < worldSpanAt1;
		GD.Print($"[MobileCamera] Zoom mapping: {(_mobileCameraDirectZoom ? "direct" : "inverse")}");

		_mobileCamera.Zoom = prevZoom;
		_mobileCamera.Position = prevPos;
	}

	private Rect2 ComputeMobileCameraBounds()
	{
		if (_currentMap.Path.Length == 0 && _currentMap.Slots.Length == 0)
			return new Rect2(0f, 0f, 1280f, 720f);

		float minX = float.MaxValue;
		float minY = float.MaxValue;
		float maxX = float.MinValue;
		float maxY = float.MinValue;

		foreach (var p in _currentMap.Path)
		{
			minX = Mathf.Min(minX, p.X);
			minY = Mathf.Min(minY, p.Y);
			maxX = Mathf.Max(maxX, p.X);
			maxY = Mathf.Max(maxY, p.Y);
		}

		foreach (var s in _currentMap.Slots)
		{
			minX = Mathf.Min(minX, s.X);
			minY = Mathf.Min(minY, s.Y);
			maxX = Mathf.Max(maxX, s.X);
			maxY = Mathf.Max(maxY, s.Y);
		}

		float margin = 200f;
		minX -= margin;
		minY -= margin;
		maxX += margin;
		maxY += margin;

		if (maxX - minX < 1f) maxX = minX + 1f;
		if (maxY - minY < 1f) maxY = minY + 1f;

		return new Rect2(minX, minY, maxX - minX, maxY - minY);
	}

	private Vector2 ClampMobileCameraPosition(Vector2 position)
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return position;

		var viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 halfView = viewportSize * (0.5f / Mathf.Max(0.001f, _mobileZoomLevel));
		Vector2 min = _mobileCameraBounds.Position + halfView;
		Vector2 max = _mobileCameraBounds.End - halfView;

		if (min.X > max.X)
			position.X = (_mobileCameraBounds.Position.X + _mobileCameraBounds.End.X) * 0.5f;
		else
			position.X = Mathf.Clamp(position.X, min.X, max.X);

		if (min.Y > max.Y)
			position.Y = (_mobileCameraBounds.Position.Y + _mobileCameraBounds.End.Y) * 0.5f;
		else
			position.Y = Mathf.Clamp(position.Y, min.Y, max.Y);

		return position;
	}

	private void ClampMobileCameraToBounds()
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return;
		_mobileCamera.Position = ClampMobileCameraPosition(_mobileCamera.Position);
	}

	private void ResetMobileGestureState()
	{
		_mobileTouchPositions.Clear();
		_mobileTouchStartPositions.Clear();
		_mobileTapSuppressed = false;
		_mobilePinchActive = false;
		_mobilePinchStartDistance = 1f;
		_mobilePinchStartZoom = _mobileZoomLevel;
		_mobilePinchLastMidpoint = Vector2.Zero;
	}

	private Vector2 ScreenToWorld(Vector2 screenPos)
	{
		return GetViewport().GetCanvasTransform().AffineInverse() * screenPos;
	}

	private Vector2 WorldToScreen(Vector2 worldPos)
	{
		return GetViewport().GetCanvasTransform() * worldPos;
	}
	private void SetupSlots()
	{
		var slotsNode = _worldNode.GetNode<Node2D>("Slots");
		for (int i = 0; i < Balance.SlotCount; i++)
		{
			_slotNodes[i] = slotsNode.GetNode<Node2D>($"Slot{i}");
			_slotNodes[i].Position = _currentMap.Slots[i];

			// Empty slot visual GÇö dark purple fill + neon violet border
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

		// Synergy glow — shown while hovering a modifier card with known tower synergies.
		var synergyGlow = new Line2D { Points = hlSq, Width = 3.6f, DefaultColor = new Color(0.88f, 0.95f, 1.00f, 0.82f) };
		synergyGlow.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(synergyGlow);
		_slotSynergyGlows[i] = synergyGlow;

		var procHalo = new Line2D { Points = hlSq, Width = 5.2f, DefaultColor = new Color(0.70f, 0.95f, 1.00f, 0.92f) };
		procHalo.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(procHalo);
		_slotProcHalos[i] = procHalo;
		_slotProcHaloRemaining[i] = 0f;
		_slotProcHaloColor[i] = Colors.White;

			// Mod-count pip — just below slot square, shown only when tower has = 1 modifier
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
		// Neon path — layered glow: outer haze ? dark road fill ? edge glow ? bright edge
		Vector2[] pts = _currentMap.Path;
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 120f, DefaultColor = new Color(1.0f, 0.05f, 0.55f, 0.05f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 80f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.10f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 50f,  DefaultColor = new Color(0.10f, 0.00f, 0.18f, 0.95f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 16f,  DefaultColor = new Color(1.0f, 0.10f, 0.55f, 0.18f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 3f,   DefaultColor = new Color(1.0f, 0.25f, 0.65f, 0.85f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		// Path flow arrows
		_pathFlow = new PathFlow();
		_mapVisuals.AddChild(_pathFlow);
		_pathFlow.Initialize(_currentMap.Path);
	}

	private void ClearMapVisuals()
	{
		while (_mapVisuals.GetChildCount() > 0)
			_mapVisuals.GetChild(0).Free();
		_pathFlow = null;
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
		_mobileTooltipFontSize = 13;
		_mobileTooltipUiScale = 1f;
		_tooltipPanel.AddChild(_tooltipLabel);
		ApplyMobileZoomReadability();
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
			var padding = new Vector2(16f * _mobileTooltipUiScale, 12f * _mobileTooltipUiScale);
			_tooltipPanel.Size = labelSize + padding;
				// Positioning: mobile shows above selected tower; desktop follows cursor.
				Vector2 pos;
				if (MobileOptimization.IsMobile())
				{
					Vector2 screenAnchor = WorldToScreen(mousePos);
					pos = new Vector2(
						screenAnchor.X - _tooltipPanel.Size.X * 0.5f,
						screenAnchor.Y - _tooltipPanel.Size.Y - 14f
					);
				}
				else
				{
					pos = mousePos + new Vector2(20, 10);
				}
				var vpSize = GetViewport().GetVisibleRect().Size;
				pos.X = Mathf.Clamp(pos.X, 4f, vpSize.X - _tooltipPanel.Size.X - 4f);
				pos.Y = Mathf.Clamp(pos.Y, 4f, vpSize.Y - _tooltipPanel.Size.Y - 4f);
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
		_announceLayer = new CanvasLayer { Layer = 4 };
		AddChild(_announceLayer);

		var anchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		anchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_announceLayer.AddChild(anchor);

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

		_halfwayBeat = new Label
		{
			Text = "HALFWAY",
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetTop = 126f,
			OffsetBottom = 156f,
			Visible = false,
			Modulate = new Color(0.74f, 0.90f, 1.00f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(_halfwayBeat, semiBold: true, size: 26);
		anchor.AddChild(_halfwayBeat);

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

		_undoPlacementButton = new Button
		{
			Text = "UNDO",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetLeft = -58f,
			OffsetRight = 58f,
			OffsetTop = -56f,
			OffsetBottom = -20f,
			Visible = false,
		};
		UITheme.ApplyFont(_undoPlacementButton, semiBold: true, size: 18);
		_undoPlacementButton.Pressed += OnUndoPlacementPressed;
		anchor.AddChild(_undoPlacementButton);

		_clutchToast = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0.48f,
			AnchorBottom = 0.48f,
			OffsetTop = -18f,
			OffsetBottom = 18f,
			Visible = false,
			Modulate = new Color(1.00f, 0.84f, 0.32f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(_clutchToast, semiBold: true, size: 30);
		anchor.AddChild(_clutchToast);

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
		bool isFinalWave = wave >= Balance.TotalWaves;
		_waveAnnounce.Text     = isFinalWave ? $"WAVE {wave}  FINAL" : $"WAVE {wave}";
		_waveAnnounce.Scale    = isFinalWave ? new Vector2(1.55f, 1.55f) : new Vector2(1.35f, 1.35f);
		_waveAnnounce.Modulate = new Color(1f, 1f, 1f, 1f);
		_waveAnnounce.AddThemeColorOverride("font_color",
			isFinalWave ? new Color(1.00f, 0.62f, 0.30f) : new Color(0.85f, 0.20f, 1.00f));
		var tween = _waveAnnounce.CreateTween();
		tween.TweenProperty(_waveAnnounce, "scale", isFinalWave ? new Vector2(1.03f, 1.03f) : Vector2.One, isFinalWave ? 0.38f : 0.3f)
			 .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenInterval(isFinalWave ? 0.42f : 0.35f);
		tween.TweenProperty(_waveAnnounce, "modulate:a", 0f, isFinalWave ? 0.52f : 0.45f);

		if (isFinalWave)
		{
			PlaySignatureScanline(_waveAnnounce, new Color(1.00f, 0.72f, 0.30f, 0.90f));
			_threatPulse.Visible = true;
			_threatPulse.Color = new Color(1.00f, 0.42f, 0.10f, 0f);
			var pulse = CreateTween();
			pulse.TweenProperty(_threatPulse, "color:a", 0.22f, 0.12f);
			pulse.TweenProperty(_threatPulse, "color:a", 0f, 0.42f);
			pulse.TweenCallback(Callable.From(() => _threatPulse.Visible = false));
		}
	}

	private void ShowArmoredWaveWarning()
	{
		_threatWarn.Text = "? ARMORED WAVE INCOMING";
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

	private void ShowHalfwayBeat()
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_halfwayBeat)) return;
		_halfwayBeat.Visible = true;
		_halfwayBeat.Scale = new Vector2(1.10f, 1.10f);
		_halfwayBeat.Modulate = new Color(0.74f, 0.90f, 1.00f, 0f);
		var tw = _halfwayBeat.CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_halfwayBeat, "modulate:a", 1f, 0.07f);
		tw.TweenProperty(_halfwayBeat, "scale", Vector2.One, 0.09f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.Chain();
		tw.TweenProperty(_halfwayBeat, "modulate:a", 0f, 0.20f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tw.TweenCallback(Callable.From(() => _halfwayBeat.Visible = false));
		PlaySignatureScanline(_halfwayBeat, new Color(0.68f, 0.92f, 1.00f, 0.85f));
	}

	private static void PlaySignatureScanline(Control target, Color color)
	{
		var stripe = new ColorRect
		{
			Color = new Color(color.R, color.G, color.B, 0f),
			Position = new Vector2(-82f, -8f),
			Size = new Vector2(52f, 22f),
			RotationDegrees = 12f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		target.AddChild(stripe);
		var tw = stripe.CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(stripe, "color:a", 0.30f, 0.05f);
		tw.TweenProperty(stripe, "position:x", target.Size.X + 82f, 0.30f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tw.Chain().TweenProperty(stripe, "color:a", 0f, 0.08f);
		tw.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(stripe))
				stripe.QueueFree();
		}));
	}

	private void UpdateLowLivesTension(float delta)
	{
		if (CurrentPhase != GamePhase.Wave || _runState.Lives > 2 || _botRunner != null)
		{
			_lowLivesHeartbeatTimer = 0f;
			return;
		}

		_lowLivesHeartbeatTimer -= delta;
		if (_lowLivesHeartbeatTimer > 0f) return;

		SoundManager.Instance?.Play("low_heartbeat");
		_lowLivesHeartbeatTimer = _runState.Lives <= 1 ? 0.62f : 0.86f;
	}

	private void ShowClutchToast(string text)
	{
		if (!GodotObject.IsInstanceValid(_clutchToast) || _botRunner != null) return;
		_clutchToast.Text = text;
		_clutchToast.Visible = true;
		_clutchToast.Scale = new Vector2(1.08f, 1.08f);
		_clutchToast.Modulate = new Color(1.00f, 0.84f, 0.32f, 0f);
		var tw = _clutchToast.CreateTween();
		tw.TweenProperty(_clutchToast, "modulate:a", 1f, 0.08f);
		tw.TweenProperty(_clutchToast, "scale", Vector2.One, 0.08f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.Chain().TweenInterval(0.19f);
		tw.TweenProperty(_clutchToast, "modulate:a", 0f, 0.12f);
		tw.TweenCallback(Callable.From(() => _clutchToast.Visible = false));
	}

	private bool TryCombatCallout(string id, float cooldownSec)
	{
		ulong now = Time.GetTicksMsec();
		if (_combatCalloutNextMs.TryGetValue(id, out ulong nextMs) && now < nextMs)
			return false;

		_combatCalloutNextMs[id] = now + (ulong)(Mathf.Max(0.1f, cooldownSec) * 1000f);
		return true;
	}

	private void SpawnCombatCallout(string text, Vector2 worldPos, Color color)
	{
		if (_botRunner != null) return;
		var callout = new CombatCallout();
		_worldNode.AddChild(callout);
		callout.GlobalPosition = worldPos + new Vector2(0f, -16f);
		callout.Initialize(text, color);
	}

	public void SpawnTargetAcquirePing(Vector2 worldPos, Color color)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave) return;
		var ping = new TargetAcquirePing();
		_worldNode.AddChild(ping);
		ping.GlobalPosition = worldPos;
		ping.Initialize(color);
	}

	public void PlayDraftCardSpirit(Vector2 screenStart, DraftOption option)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_announceLayer)) return;

		var spirit = new Control
		{
			TopLevel = true,
			Position = screenStart - new Vector2(10f, 10f),
			Size = new Vector2(20f, 20f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_announceLayer.AddChild(spirit);

		var accent = option.Type == DraftOptionType.Modifier
			? ModifierVisuals.GetAccent(option.Id)
			: option.Id switch
			{
				"rapid_shooter" => new Color(0.25f, 0.92f, 1.00f),
				"heavy_cannon" => new Color(1.00f, 0.60f, 0.18f),
				"marker_tower" => new Color(1.00f, 0.30f, 0.72f),
				"chain_tower" => new Color(0.62f, 0.90f, 1.00f),
				_ => new Color(0.75f, 0.85f, 1.00f),
			};

		if (option.Type == DraftOptionType.Modifier)
		{
			var icon = new ModifierIcon
			{
				ModifierId = option.Id,
				IconColor = accent,
				Size = new Vector2(20f, 20f),
				CustomMinimumSize = new Vector2(20f, 20f),
			};
			icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			spirit.AddChild(icon);
		}
		else
		{
			var glyph = new Label
			{
				Text = option.Id switch
				{
					"rapid_shooter" => "RS",
					"heavy_cannon" => "HC",
					"marker_tower" => "MK",
					"chain_tower" => "AR",
					_ => "TW",
				},
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Modulate = new Color(accent.R, accent.G, accent.B, 1f),
			};
			glyph.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			UITheme.ApplyFont(glyph, semiBold: true, size: 13);
			spirit.AddChild(glyph);
		}

		var glow = new ColorRect
		{
			Color = new Color(accent.R, accent.G, accent.B, 0.18f),
			Position = new Vector2(-4f, -4f),
			Size = new Vector2(28f, 28f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		spirit.AddChild(glow);
		spirit.MoveChild(glow, 0);

		Vector2 end = new Vector2(GetViewport().GetVisibleRect().Size.X * 0.5f - 10f, 58f);
		var tw = spirit.CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(spirit, "position", end, 0.28f)
		  .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(spirit, "scale", new Vector2(0.84f, 0.84f), 0.28f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tw.TweenProperty(spirit, "modulate:a", 0.10f, 0.28f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		tw.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(spirit))
				spirit.QueueFree();
		}));
	}

	public void NotifyOverkillSpill(Vector2 worldPos, float spillDamage)
	{
		if (spillDamage < 34f) return;
		if (!TryCombatCallout("overkill_spill", 6.5f)) return;
		SpawnCombatCallout("OVERKILL SPILL", worldPos, new Color(1.00f, 0.56f, 0.25f));
	}

	public void NotifyFeedbackLoopProc(TowerInstance tower)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave) return;

		ulong id = tower.GetInstanceId();
		ulong now = Time.GetTicksMsec();
		if (_feedbackLoopBurst.TryGetValue(id, out var state) && now - state.firstMs <= 1300)
			_feedbackLoopBurst[id] = (state.firstMs, state.count + 1);
		else
			_feedbackLoopBurst[id] = (now, 1);

		var burst = _feedbackLoopBurst[id];
		if (burst.count < 3) return;

		_feedbackLoopBurst.Remove(id);
		if (!TryCombatCallout("feedback_loop", 8.0f)) return;
		SpawnCombatCallout("FEEDBACK LOOP", tower.GlobalPosition + new Vector2(0f, -10f), new Color(0.62f, 1.00f, 0.88f));
	}

	public void NotifyChainMaxBounce(Vector2 worldPos, int bounceCount)
	{
		if (bounceCount < 2) return;
		if (!TryCombatCallout("chain_reaction", 7.0f)) return;
		SpawnCombatCallout("CHAIN REACTION", worldPos, new Color(0.56f, 0.95f, 1.00f));
	}

	public void PlayModifierLockInFx(int slotIndex, string modifierId, System.Action? onComplete = null)
	{
		if (_botRunner != null || slotIndex < 0 || slotIndex >= _slotNodes.Length)
		{
			onComplete?.Invoke();
			return;
		}

		SoundManager.Instance?.Play("ui_lock_in_hard");

		// One-frame white edge flash on target border.
		if (GodotObject.IsInstanceValid(_slotPreviewGlows[slotIndex]))
		{
			var glow = _slotPreviewGlows[slotIndex];
			glow.DefaultColor = Colors.White;
			glow.Modulate = new Color(1f, 1f, 1f, 1f);
			var flash = glow.CreateTween();
			flash.TweenProperty(glow, "modulate", Colors.Transparent, 0.06f);
		}

		ModifierIcon icon;
		bool tempIcon = false;
		if (GodotObject.IsInstanceValid(_previewModifierIcon) && _previewModifierIcon.GetParent() == _slotNodes[slotIndex])
		{
			icon = _previewModifierIcon;
		}
		else
		{
			tempIcon = true;
			icon = new ModifierIcon
			{
				Size = new Vector2(18f, 18f),
				CustomMinimumSize = new Vector2(18f, 18f),
				Position = new Vector2(-9f, -9f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			_slotNodes[slotIndex].AddChild(icon);
		}

		var accent = ModifierVisuals.GetAccent(modifierId);
		icon.ModifierId = modifierId;
		icon.IconColor = accent;
		icon.Modulate = new Color(1f, 1f, 1f, 1f);
		icon.Scale = new Vector2(0.96f, 0.96f);
		icon.Visible = true;
		var snap = icon.CreateTween();
		snap.TweenProperty(icon, "scale", new Vector2(1.08f, 1.08f), 0.06f)
		    .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		snap.TweenProperty(icon, "scale", Vector2.One, 0.06f)
		    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		GetTree().CreateTimer(0.12f).Timeout += () =>
		{
			if (tempIcon && GodotObject.IsInstanceValid(icon))
				icon.QueueFree();
			else if (GodotObject.IsInstanceValid(_previewModifierIcon))
				_previewModifierIcon.Visible = false;

			onComplete?.Invoke();
		};
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

	private System.Collections.Generic.IEnumerable<TowerWaveStats> GetAllTowerStats()
	{
		foreach (var wave in _runState.CompletedWaves)
		{
			foreach (var stat in wave.TowerStats)
				yield return stat;
		}

		// Current wave is only additive when the run ends mid-wave (loss).
		if (_runState.CurrentWave.WaveNumber > _runState.CompletedWaves.Count)
		{
			foreach (var stat in _runState.CurrentWave.TowerStats)
				yield return stat;
		}
	}

	private string BuildMvpLine()
	{
		if (_runState.TotalDamageDealt <= 0) return "";

		var bySlot = GetAllTowerStats()
			.GroupBy(s => s.SlotIndex)
			.Select(g => new { Slot = g.Key, Damage = g.Sum(x => x.Damage) })
			.Where(x => x.Slot >= 0)
			.OrderByDescending(x => x.Damage)
			.FirstOrDefault();
		if (bySlot == null || bySlot.Damage <= 0) return "";

		var tower = _runState.Slots[bySlot.Slot].Tower;
		if (tower == null) return "";

		var def = DataLoader.GetTowerDef(tower.TowerId);
		float share = 100f * bySlot.Damage / Mathf.Max(1, _runState.TotalDamageDealt);
		return $"MVP Tower: Slot {bySlot.Slot + 1} {def.Name} - {share:0.#}% of total damage";
	}

	private string BuildMostValuableModLine()
	{
		if (_modifierProcCounts.Count == 0) return "";
		var best = _modifierProcCounts.OrderByDescending(kvp => kvp.Value).First();
		if (best.Value <= 0) return "";

		var def = DataLoader.GetModifierDef(best.Key);
		return $"Most Valuable Mod: {def.Name} - triggered {best.Value}x";
	}

	private RunScorePayload BuildRunScorePayload(bool won, int waveReached)
	{
		string mapId = string.IsNullOrEmpty(_runState.SelectedMapId)
			? LeaderboardKey.RandomMapId
			: _runState.SelectedMapId!;
		var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal;
		long nowUnix = (long)System.Math.Floor(Time.GetUnixTimeFromSystem());
		string gameVersion = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
		return new RunScorePayload(
			mapId,
			difficulty,
			won,
			waveReached,
			_runState.Lives,
			_runState.TotalDamageDealt,
			_runState.TotalKills,
			_runState.TotalPlayTime,
			nowUnix,
			gameVersion,
			BuildSnapshotCodec.CaptureFromRunState(_runState)
		);
	}

	private static string BuildInitialLeaderboardLine(RunScorePayload payload, LocalSubmitResult? localSubmit)
	{
		if (localSubmit == null)
			return $"Score: {ScoreCalculator.ComputeScore(payload):N0}";

		if (localSubmit.IsNewPersonalBest)
			return $"Score: {localSubmit.Score:N0}  |  NEW PERSONAL BEST";

		return $"Score: {localSubmit.Score:N0}  |  Personal Best: {localSubmit.CurrentBest.Score:N0}";
	}

	private void QueueGlobalSubmit(RunScorePayload payload, string localLine)
	{
		var sm = SettingsManager.Instance;
		var bucket = new SlotTheory.Core.Leaderboards.LeaderboardBucket(payload.MapId, payload.Difficulty);
		if (bucket.IsGlobalEligible && (sm == null || string.IsNullOrEmpty(sm.PlayerName)))
		{
			_endScreen.ShowNamePrompt(payload, localLine);
			return;
		}

		_endScreen.SetLeaderboardStatus($"{localLine}  |  Global: submitting...");
		_ = SubmitGlobalScoreAsync(payload, localLine);
	}

	private async System.Threading.Tasks.Task SubmitGlobalScoreAsync(RunScorePayload payload, string localLine)
	{
		var manager = LeaderboardManager.Instance;
		if (manager == null)
		{
			CallDeferred(nameof(ApplyLeaderboardStatus), $"{localLine}  |  Global: unavailable", true);
			return;
		}

		var result = await manager.SubmitAsync(payload);
		string line = BuildGlobalLeaderboardLine(localLine, result);
		bool isError = result.State == GlobalSubmitState.Failed;
		CallDeferred(nameof(ApplyLeaderboardStatus), line, isError);
	}

	private void ApplyLeaderboardStatus(string line, bool isError)
	{
		if (!GodotObject.IsInstanceValid(_endScreen) || !_endScreen.Visible) return;
		_endScreen.SetLeaderboardStatus(line, isError);
	}

	private static string BuildGlobalLeaderboardLine(string localLine, GlobalSubmitResult result)
	{
		string globalText = result.State switch
		{
			GlobalSubmitState.Submitted when result.Rank.HasValue
				=> $"Global ({result.Provider}): rank #{result.Rank.Value}",
			GlobalSubmitState.Submitted
				=> $"Global ({result.Provider}): submitted",
			GlobalSubmitState.Queued
				=> $"Global ({result.Provider}): queued",
			GlobalSubmitState.Failed
				=> $"Global ({result.Provider}): failed",
			_ => result.Message,
		};

		return $"{localLine}  |  {globalText}";
	}

	private string BuildRunName(bool registerInHistory = false, bool? wonOverride = null, int? waveReachedOverride = null)
	{
		var profile = BuildRunNameProfile(wonOverride, waveReachedOverride);
		return RunNameGenerator.GenerateName(profile, _runState.RngSeed, registerInHistory);
	}

	private RunNameProfile BuildRunNameProfile(bool? wonOverride = null, int? waveReachedOverride = null)
	{
		string mapId = string.IsNullOrEmpty(_runState.SelectedMapId)
			? LeaderboardKey.RandomMapId
			: _runState.SelectedMapId!;
		var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal;
		bool won = wonOverride ?? CurrentPhase == GamePhase.Win;
		int waveReached = waveReachedOverride ?? GuessCurrentWaveReached();

		return RunNameGenerator.AnalyzeProfile(_runState, difficulty, mapId, won, waveReached);
	}

	private int GuessCurrentWaveReached()
	{
		int waveReached = CurrentPhase switch
		{
			GamePhase.Wave => _runState.WaveIndex + 1,
			GamePhase.Loss => _runState.WaveIndex + 1,
			GamePhase.Win => Balance.TotalWaves,
			_ => _runState.WaveIndex,
		};
		return System.Math.Clamp(waveReached, 0, Balance.TotalWaves);
	}

	private (Color start, Color end) BuildRunNameColors()
	{
		var profile = BuildRunNameProfile();
		return RunNameGenerator.ResolveNameColors(profile);
	}

	private void StartWavePhase()
	{
		_currentDraftOptions = null;
		ClearUndoPlacementState();
		CurrentPhase = GamePhase.Wave;
		int waveNumber = _runState.WaveIndex + 1;
		if (_botRunner == null) ShowWaveAnnouncement(waveNumber);
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
		if (waveNumber == 10)
		{
			ShowHalfwayBeat();
			SoundManager.Instance?.Play("wave_halfway_lift");
		}
		if (waveNumber >= Balance.TotalWaves)
		{
			SoundManager.Instance?.Play("wave20_start");
			SoundManager.Instance?.Play("wave20_swell");
			_hudPanel.PulseWaveLabel();
			_pathFlow?.TriggerSurge(1.0f);
		}
		// Tension ramp: waves 15-20 gradually increase music intensity
		if (waveNumber >= 15)
			SoundManager.Instance?.SetMusicTension((waveNumber - 14f) / 6f);
		else
			SoundManager.Instance?.SetMusicTension(0f);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		string runName = BuildRunName();
		var runColors = BuildRunNameColors();
		_hudPanel.SetBuildName(runName, visible: true, startColor: runColors.start, endColor: runColors.end);
		SaveMobileRunSnapshot("start_wave");
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
		if (_undoPlacementActive)
		{
			_placementLabel.Text = "Tower placed  •  tap UNDO to revert";
			_placementLabel.Visible = true;
			return;
		}
		if (CurrentPhase != GamePhase.Draft || (!_draftPanel.IsAwaitingSlot && !_draftPanel.IsAwaitingTower))
		{
			_placementLabel.Visible = false;
			return;
		}
		_placementLabel.Text = _draftPanel.PlacementHint;
		_placementLabel.Visible = true;
	}

	public void SetDraftSynergyHint(string modifierId)
	{
		_draftSynergyHintModifierId = modifierId ?? "";
		if (_draftSynergyHintModifierId.Length == 0)
			ClearDraftSynergyHighlights();
	}

	private void UpdateDraftSynergyHighlights(float delta)
	{
		if (_draftSynergyHintModifierId.Length == 0)
		{
			ClearDraftSynergyHighlights();
			return;
		}

		_draftSynergyPulseT += delta;
		float pulse = 0.58f + 0.42f * Mathf.Sin(_draftSynergyPulseT * 5.2f);
		var accent = ModifierVisuals.GetAccent(_draftSynergyHintModifierId);

		for (int i = 0; i < _slotSynergyGlows.Length; i++)
		{
			var glow = _slotSynergyGlows[i];
			if (!GodotObject.IsInstanceValid(glow)) continue;

			var tower = _runState.Slots[i].Tower;
			if (tower == null || !IsSynergyTower(tower, _draftSynergyHintModifierId))
			{
				glow.Modulate = Colors.Transparent;
				continue;
			}

			glow.DefaultColor = new Color(accent.R, accent.G, accent.B, 0.90f);
			glow.Modulate = new Color(1f, 1f, 1f, 0.18f + pulse * 0.48f);
		}
	}

	private void ClearDraftSynergyHighlights()
	{
		for (int i = 0; i < _slotSynergyGlows.Length; i++)
		{
			var glow = _slotSynergyGlows[i];
			if (GodotObject.IsInstanceValid(glow))
				glow.Modulate = Colors.Transparent;
		}
		_draftSynergyPulseT = 0f;
	}

	private static bool IsSynergyTower(TowerInstance tower, string modifierId)
	{
		return modifierId switch
		{
			"exploit_weakness" => tower.AppliesMark || tower.TowerId == "marker_tower",
			"chain_reaction" => tower.TowerId == "chain_tower",
			"overkill" or "focus_lens" => tower.TowerId == "heavy_cannon"
				|| tower.Modifiers.Any(m => m.ModifierId == "focus_lens")
				|| tower.BaseDamage >= 40f,
			_ => false,
		};
	}

	public void NotifyModifierProc(TowerInstance tower, string modifierId)
	{
		if (_modifierProcCounts.TryGetValue(modifierId, out int n))
			_modifierProcCounts[modifierId] = n + 1;
		else
			_modifierProcCounts[modifierId] = 1;

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




