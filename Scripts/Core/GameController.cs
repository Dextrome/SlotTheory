using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
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
	private UnlockRevealScreen _unlockRevealScreen = null!;
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
	private CanvasLayer _globalSurgeLayer = null!;
	private Label _waveAnnounce = null!;
	private Label _halfwayBeat = null!;
	private Label _threatWarn = null!;
	private Button _undoPlacementButton = null!;
	private Label _clutchToast = null!;
	private Label _globalSpectacleBanner = null!;
	private Label _globalSurgeSubtitleLabel = null!;
	private ColorRect _waveClearFlash = null!;
	private ColorRect _threatPulse = null!;
	private ColorRect _spectacleAfterimage = null!;
	private ColorRect _spectaclePulse = null!;
	private ColorRect _lingerTint = null!;
	private ColorRect _vignetteRect = null!;
	private int   _surgeChainCount = 0;
	private float _surgeChainResetTimer = 0f;
	private Tween? _globalSurgeBannerTween;
	private ulong _globalSurgeBannerToken = 0;
	private Node2D _worldNode = null!;
	private BotRunner? _botRunner;
	private int   _botWaveInRangeSum;
	private int   _botWaveInRangeSamples;
	private int   _botWaveSteps;
	private int   _botStepExplosionBursts;
	private int   _botStepHitStopRequests;
	private readonly List<CombatLabTraceEvent> _botRunTraceEvents = new();
	private int _botSurgeTraceCounter = 0;
	private int _botGlobalTraceCounter = 0;
	private string _botLastTraceTriggerId = string.Empty;
	private int _extraPicksRemaining;
	private List<DraftOption>? _currentDraftOptions;
	private WaveReport? _lastWaveReport;
	private int _previewGhostSlot = -1;
	private float _previewGhostPhase = 0f;
	private ModifierIcon? _previewModifierIcon;
	private int _previewTowerGhostSlot = -1;
	private float _previewTowerGhostPhase = 0f;
	private string _previewTowerGhostId = "";
	private TowerInstance? _previewTowerGhost;
	private bool _runAbandoned = false;  // set on intentional exit to suppress _ExitTree re-save
	private bool _hitStopActive = false;
	private float _hitStopCooldown = 0f;
	private float _hitStopFactor = 1f;
	private ulong _hitStopToken = 0;
	private bool _spectacleSlowMoActive = false;
	private ulong _spectacleSlowMoToken = 0;
	private float _spectacleSlowMoFactor = 1f;
	private Tween? _explosionZoomTween;
	private string _draftSynergyHintModifierId = "";
	private float _draftSynergyPulseT = 0f;
	private float _lowLivesHeartbeatTimer = 0f;
	private readonly System.Collections.Generic.Dictionary<string, ulong> _combatCalloutNextMs = new();
	private readonly System.Collections.Generic.Dictionary<SlotTheory.Entities.ITowerView, (ulong firstMs, int count)> _feedbackLoopBurst = new();
	private readonly System.Collections.Generic.Dictionary<string, int> _modifierProcCounts = new();
	private readonly Queue<UnlockRevealRequest> _pendingUnlockReveals = new();
	private bool _undoPlacementActive = false;
	private bool _cancelBtnShown = false;
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
	private bool _isMobilePlatform = false;
	private const float MobileMinZoom = 1.0f;
	private const float MobileMaxZoom = 2.6f;
	private const float MobileZoomSnapEpsilon = 0.015f;
	private const float MobileTapMoveThreshold = 18f;
	private const float MobilePanStartThreshold = 6f;
	private const float DraftOpenDelaySeconds = 0.50f;
	private readonly EnemyRenderPerfProfiler _enemyRenderPerfProfiler = new();
	private bool _perfReportHotkeyLatch;
	private readonly SpectacleSystem _spectacleSystem = new();
	private readonly List<ExplosionResidueState> _explosionResidues = new();
	private const float GlobalSurgeLingerMultiplier = 4f;
	private const float GlobalSurgeDurationScale = 4f;
	private const float GlobalSurgeBannerHoldSeconds = 1f;
	private const float GlobalSurgeBannerFadeSeconds = 1.8f;

	private enum SpectacleConsequenceKind
	{
		None,
		FrostSlow,
		Vulnerability,
		BurnPatch,
	}

	private sealed class ExplosionResidueState
	{
		public ExplosionResidueKind Kind;
		public Vector2 Origin;
		public float Radius;
		public float Remaining;
		public float TickInterval;
		public float TickRemaining;
		public float Potency;
		public Color Accent;
		public ITowerView? SourceTower;
		public bool PulseGate;
		public ExplosionResidueZoneFx? FxNode;
	}

	private readonly record struct UnlockRevealRequest(bool IsTower, string Id);

	public override void _Ready()
	{
		Instance = this;
		_isMobilePlatform = MobileOptimization.IsMobile();
		EnemyInstance.SetSpectacleSpeedMultiplier(1f);
		DataLoader.LoadAll();
		SpectacleTuning.Reset();

		var userArgs = OS.GetCmdlineUserArgs();
		int dumpSeedIndex = System.Array.IndexOf(userArgs, "--dump_seed_tuning_json");
		if (dumpSeedIndex >= 0)
		{
			if (dumpSeedIndex + 1 >= userArgs.Length)
			{
				GD.PrintErr("[TUNING] Missing output path after --dump_seed_tuning_json.");
				GetTree().Quit();
				return;
			}

			string outputPath = userArgs[dumpSeedIndex + 1];
			try
			{
				string fullPath = Path.GetFullPath(outputPath);
				string? dir = Path.GetDirectoryName(fullPath);
				if (!string.IsNullOrWhiteSpace(dir))
					Directory.CreateDirectory(dir);

				var options = new JsonSerializerOptions { WriteIndented = true };
				string json = JsonSerializer.Serialize(SpectacleTuning.Current, options);
				File.WriteAllText(fullPath, json);
				GD.Print($"[TUNING] Wrote current seed tuning to {fullPath}");
			}
			catch (System.Exception ex)
			{
				GD.PrintErr($"[TUNING] Failed to write seed tuning JSON: {ex.Message}");
			}

			GetTree().Quit();
			return;
		}

		if (BotMetricsDeltaReporter.TryRunFromArgs(userArgs))
		{
			GetTree().Quit();
			return;
		}
		if (userArgs.Contains("--lab_scenario") || userArgs.Contains("--lab_sweep"))
		{
			bool success = CombatLabCli.Run(userArgs);
			if (!success)
				GD.PrintErr("[LAB] Failed to run combat lab automation.");
			GetTree().Quit();
			return;
		}

		string? tuningFile = null;
		int tf = System.Array.IndexOf(userArgs, "--tuning_file");
		if (tf >= 0 && tf + 1 < userArgs.Length)
			tuningFile = userArgs[tf + 1];
		if (!string.IsNullOrWhiteSpace(tuningFile))
		{
			if (SpectacleTuningLoader.TryLoadFromFile(tuningFile!, out var profile, out string error))
			{
				string tuningLabel = System.IO.Path.GetFileNameWithoutExtension(tuningFile!);
				SpectacleTuning.Apply(profile, tuningLabel);
				GD.Print($"[TUNING] Loaded profile '{SpectacleTuning.ActiveLabel}' from {tuningFile}");
			}
			else
			{
				GD.PrintErr($"[TUNING] Failed to load {tuningFile}: {error}");
			}
		}

		// Bot playtest mode: godot --headless --path ... -- --bot --runs N --difficulty easy|normal|hard
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
				if (diffStr == "easy") targetDifficulty = DifficultyMode.Easy;
				else if (diffStr == "normal") targetDifficulty = DifficultyMode.Normal;
				else if (diffStr == "hard") targetDifficulty = DifficultyMode.Hard;
			}
			
			string? targetMap = null;
			int mi = System.Array.IndexOf(userArgs, "--map");
			if (mi >= 0 && mi + 1 < userArgs.Length)
				targetMap = userArgs[mi + 1];

			BotStrategy? targetStrategy = null;
			int si = System.Array.IndexOf(userArgs, "--strategy");
			if (si >= 0 && si + 1 < userArgs.Length &&
				System.Enum.TryParse<BotStrategy>(userArgs[si + 1], ignoreCase: true, out var parsedStrategy))
			{
				targetStrategy = parsedStrategy;
			}

			string? strategySet = null;
			int ssi = System.Array.IndexOf(userArgs, "--strategy_set");
			if (ssi >= 0 && ssi + 1 < userArgs.Length)
				strategySet = userArgs[ssi + 1];

			string? forcedTower = null;
			int fi = System.Array.IndexOf(userArgs, "--force_tower");
			if (fi >= 0 && fi + 1 < userArgs.Length)
				forcedTower = userArgs[fi + 1];

			string? forcedMod = null;
			int fmi = System.Array.IndexOf(userArgs, "--force_mod");
			if (fmi >= 0 && fmi + 1 < userArgs.Length)
				forcedMod = userArgs[fmi + 1];

			string? metricsOutputPath = null;
			int oi = System.Array.IndexOf(userArgs, "--bot_metrics_out");
			if (oi >= 0 && oi + 1 < userArgs.Length)
				metricsOutputPath = userArgs[oi + 1];

			string? traceOutputPath = null;
			int toi = System.Array.IndexOf(userArgs, "--bot_trace_out");
			if (toi >= 0 && toi + 1 < userArgs.Length)
				traceOutputPath = userArgs[toi + 1];

			int runIndexOffset = 0;
			int rio = System.Array.IndexOf(userArgs, "--run_index_offset");
			if (rio >= 0 && rio + 1 < userArgs.Length)
				int.TryParse(userArgs[rio + 1], out runIndexOffset);

			_botRunner = new BotRunner(
				runs,
				targetDifficulty,
				targetMap,
				targetStrategy,
				forcedTower,
				forcedMod,
				metricsOutputPath,
				traceOutputPath,
				SpectacleTuning.ActiveLabel,
				strategySet,
				runIndexOffset);
			Engine.MaxFps = 0;
			GD.Print($"[BOT] Headless playtest: {runs} runs{(targetDifficulty.HasValue ? $" ({targetDifficulty.Value})" : "")}{(targetMap != null ? $" on {targetMap}" : "")}{(targetStrategy.HasValue ? $" strategy={targetStrategy.Value}" : "")}{(strategySet != null ? $" strategy_set={strategySet}" : "")}{(forcedTower != null ? $" tower={forcedTower}" : "")}{(forcedMod != null ? $" mod={forcedMod}" : "")}{(runIndexOffset > 0 ? $" run_offset={runIndexOffset}" : "")}{(string.IsNullOrWhiteSpace(metricsOutputPath) ? "" : $" metrics={metricsOutputPath}")}{(string.IsNullOrWhiteSpace(traceOutputPath) ? "" : $" trace={traceOutputPath}")}");
		}

		_runState = new RunState();
		if (_botRunner == null)
			SettingsManager.Instance?.IncrementRunsStarted();
		if (_botRunner != null)
			ResetBotTraceBuffer();
		
		// Apply pending map selection from MapSelectPanel if available
		_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
		if (_runState.SelectedMapId == "random_map" && SlotTheory.UI.MapSelectPanel.PendingProceduralSeed > 0)
			_runState.RngSeed = (int)SlotTheory.UI.MapSelectPanel.PendingProceduralSeed;
		if (_botRunner != null && _runState.SelectedMapId == "random_map")
			_runState.RngSeed = ResolveBotProceduralSeed();
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
		_spectacleSystem.Reset();
		_spectacleSystem.OnSurgeTriggered -= OnSpectacleSurgeTriggered;
		_spectacleSystem.OnGlobalTriggered -= OnGlobalSurgeTriggered;
		_spectacleSystem.OnSurgeTriggered += OnSpectacleSurgeTriggered;
		_spectacleSystem.OnGlobalTriggered += OnGlobalSurgeTriggered;
		_draftPanel = GetNode<DraftPanel>("../DraftPanel");
		_hudPanel   = GetNode<HudPanel>("../HudPanel");
		_endScreen  = GetNode<EndScreen>("../EndScreen");
		_unlockRevealScreen = GetNode<UnlockRevealScreen>("../UnlockRevealScreen");
		_unlockRevealScreen.RevealClosed += OnUnlockRevealClosed;

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
		var draftPanel = _draftPanel;
		var hudPanel = _hudPanel;
		bool draftPanelReady = draftPanel != null && GodotObject.IsInstanceValid(draftPanel);
		bool hudPanelReady = hudPanel != null && GodotObject.IsInstanceValid(hudPanel);
		if (_runState == null || _combatSim == null || _waveSystem == null)
		{
			_cancelBtnShown = false;
			return;
		}

		if (_botRunner == null) UpdateTooltip();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && draftPanelReady && (draftPanel!.IsAwaitingSlot || draftPanel.IsAwaitingTower))
			UpdateSlotHighlights();
		else if (_highlightedSlot != -1)
			ClearSlotHighlights();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && draftPanelReady && draftPanel!.IsAwaitingTower)
			UpdateModifierPreviewGhost((float)delta);
		else
			ClearModifierPreviewGhost();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && draftPanelReady && draftPanel!.IsAwaitingSlot)
			UpdateTowerPlacementPreviewGhost((float)delta);
		else
			ClearTowerPlacementPreviewGhost();

		if (CurrentPhase == GamePhase.Draft && draftPanelReady && !draftPanel!.IsAwaitingSlot && !draftPanel.IsAwaitingTower)
			UpdateDraftSynergyHighlights((float)delta);
		else
			ClearDraftSynergyHighlights();

		if (CurrentPhase == GamePhase.Wave && _botRunner == null)
		{
			_spectacleSystem.Update((float)delta);
			UpdateExplosionResidues((float)delta);
		}

		// Surge chain reset timer
		if (_surgeChainCount > 0)
		{
			_surgeChainResetTimer -= (float)delta;
			if (_surgeChainResetTimer <= 0f)
				_surgeChainCount = 0;
		}

		bool showGlobalSurgeMeter = CurrentPhase == GamePhase.Draft || CurrentPhase == GamePhase.Wave;
		if (hudPanelReady)
		{
			float globalThreshold = SpectacleDefinitions.ResolveGlobalThreshold();
			float globalFill = globalThreshold > 0f ? _spectacleSystem.GlobalMeter / globalThreshold : 0f;
			string archetypePreview = "";
			float previewAlpha = 0f;
			string[] peekMods = System.Array.Empty<string>();
			// Ghost archetype label materialises from 70% fill, fully opaque at 100%.
			if (globalFill >= 0.70f && _botRunner == null)
			{
				peekMods = _spectacleSystem.PeekDominantMods();
				archetypePreview = SurgeDifferentiation.ResolveLabel(peekMods);
				previewAlpha = Mathf.Clamp((globalFill - 0.70f) / 0.30f, 0f, 1f) * 0.80f;
			}
			// Screen-edge vignette intensifies as meter approaches full
			if (_botRunner == null && GodotObject.IsInstanceValid(_vignetteRect))
			{
				float vigIntensity = Mathf.Clamp((globalFill - 0.70f) / 0.30f, 0f, 1f);
				if (vigIntensity > 0f)
				{
					Color vigColor = peekMods.Length > 0
						? ResolveSpectacleColor(peekMods[0])
						: new Color(1f, 0.90f, 0.56f);
					_vignetteRect.Visible = true;
					var vmat = (ShaderMaterial)_vignetteRect.Material;
					vmat.SetShaderParameter("intensity", vigIntensity * 0.85f);
					// Boost saturation so low-alpha tints read as their true hue rather than washed-out yellow
				float vigR = Mathf.Clamp(vigColor.R * 1.4f - vigColor.G * 0.2f, 0f, 1f);
				float vigG = Mathf.Clamp(vigColor.G * 0.7f, 0f, 1f);
				float vigB = Mathf.Clamp(vigColor.B * 1.1f, 0f, 1f);
				vmat.SetShaderParameter("tint", new Vector3(vigR, vigG, vigB));
				}
				else
				{
					_vignetteRect.Visible = false;
				}
			}
			hudPanel!.RefreshGlobalSurgeMeter(
				_spectacleSystem.GlobalMeter,
				globalThreshold,
				showGlobalSurgeMeter,
				archetypePreview,
				previewAlpha);
			hudPanel.RefreshSpeedLabelFromActual((float)Engine.TimeScale);
		}

		if (_botRunner == null) UpdatePlacementLabel();
		if (_botRunner == null) UpdateProcVisuals((float)delta);
		if (_botRunner == null) UpdateSpectacleVisuals();
		if (_botRunner == null)
		{
			_enemyRenderPerfProfiler.RecordFrame((float)delta, _runState.EnemiesAlive.Count);
			bool devMode = SettingsManager.Instance?.DevMode ?? false;
			if (hudPanelReady)
				hudPanel!.RefreshDevRenderStats(devMode, _runState.EnemiesAlive.Count, _enemyRenderPerfProfiler.BuildOverlaySummary());
			HandlePerfProfilerHotkey(devMode);
		}

		// Show/hide cancel button when player is in tower/modifier placement step
		if (_botRunner == null)
		{
			bool inPlacement = draftPanelReady
				&& CurrentPhase == GamePhase.Draft
				&& (draftPanel!.IsAwaitingSlot || draftPanel.IsAwaitingTower);
			if (draftPanelReady && inPlacement && !_cancelBtnShown)
			{
				_cancelBtnShown = true;
				draftPanel!.ShowPlacementUI(CancelPlacement);
			}
			else if (draftPanelReady && !inPlacement && _cancelBtnShown)
			{
				_cancelBtnShown = false;
				draftPanel!.HidePlacementUI();
			}
			else if (!draftPanelReady)
			{
				_cancelBtnShown = false;
			}
		}

		if (CurrentPhase != GamePhase.Wave)
		{
			_lowLivesHeartbeatTimer = 0f;
			ClearExplosionResidues();
			return;
		}

		if (_botRunner != null) { BotTick(); return; }

		int livesBefore = _runState.Lives;
		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		if (hudPanelReady)
		{
			hudPanel!.Refresh(_runState.WaveIndex + 1, _runState.Lives);
			hudPanel.RefreshTime(_runState.TotalPlayTime);
			hudPanel.RefreshEnemies(_runState.EnemiesAlive.Count, _waveSystem.GetTotalCount());
		}
		if (_runState.Lives < livesBefore)
		{
			if (hudPanelReady)
				hudPanel!.FlashLives();
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
			var newlyUnlocked = AchievementManager.Instance?.CheckRunEndAndCollectUnlocks(
				_runState,
				SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
				won: false) ?? System.Array.Empty<string>();
			int livesLost = Balance.StartingLives - _runState.Lives;
			SoundManager.Instance?.Play("game_over");
				string runName = BuildRunName(registerInHistory: true, wonOverride: false, waveReachedOverride: _runState.WaveIndex + 1);
			var runColors = BuildRunNameColors();
			string mvpLine = BuildMvpLine();
			string modLine = BuildMostValuableModLine();
			var scorePayload = BuildRunScorePayload(won: false, waveReached: _runState.WaveIndex + 1, buildName: runName);
			var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
			string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
			_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost, _runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), _runState, runName, mvpLine, modLine, runColors.start, runColors.end);
			_endScreen.SetLeaderboardStatus(leaderboardLine);
			QueueGlobalSubmit(scorePayload, leaderboardLine);
			EnqueueUnlockReveals(newlyUnlocked);
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
				var newlyUnlocked = AchievementManager.Instance?.CheckRunEndAndCollectUnlocks(
					_runState,
					SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
					won: true) ?? System.Array.Empty<string>();
				SoundManager.Instance?.Play("victory");
					string runName = BuildRunName(registerInHistory: true, wonOverride: true, waveReachedOverride: Balance.TotalWaves);
				var runColors = BuildRunNameColors();
				string mvpLine = BuildMvpLine();
				string modLine = BuildMostValuableModLine();
				var scorePayload = BuildRunScorePayload(won: true, waveReached: Balance.TotalWaves, buildName: runName);
				var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
				string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
				_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
				_endScreen.ShowWin(_runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), runName, mvpLine, modLine, runColors.start, runColors.end);
				_endScreen.SetLeaderboardStatus(leaderboardLine);
				QueueGlobalSubmit(scorePayload, leaderboardLine);
				EnqueueUnlockReveals(newlyUnlocked);
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
					GetTree().CreateTimer(DraftOpenDelaySeconds).Timeout += StartDraftPhase;
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
			case 1007: // NOTIFICATION_WM_GO_BACK_REQUEST
				if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower))
					CancelPlacement();
				break;

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
		_spectacleSlowMoFactor = 1f;
		_hitStopFactor = 1f;
		RefreshSpectacleSpeedMultiplier();
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

	private void EnqueueUnlockReveals(IReadOnlyList<string> achievementIds)
	{
		if (achievementIds.Count == 0)
			return;

		foreach (string achievementId in achievementIds)
		{
			switch (achievementId)
			{
				case Unlocks.ArcEmitterAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.ArcEmitterTowerId));
					break;
				case Unlocks.SplitShotAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.SplitShotModifierId));
					break;
				case Unlocks.RiftPrismAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.RiftPrismTowerId));
					break;
			}
		}

		TryShowNextUnlockReveal();
	}

	private void OnUnlockRevealClosed()
	{
		TryShowNextUnlockReveal();
	}

	private void TryShowNextUnlockReveal()
	{
		if (!GodotObject.IsInstanceValid(_unlockRevealScreen)
			|| _unlockRevealScreen.IsShowing
			|| _pendingUnlockReveals.Count == 0)
		{
			return;
		}

		UnlockRevealRequest request = _pendingUnlockReveals.Dequeue();
		if (request.IsTower)
			_unlockRevealScreen.ShowTowerUnlock(request.Id);
		else
			_unlockRevealScreen.ShowModifierUnlock(request.Id);
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
			_botRunner.RecordPick(options, pick?.Option.Id);
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
			var tower = _runState.Slots[i].TowerNode;
			if (tower != null && GodotObject.IsInstanceValid(tower))
				tower.Free();
		}

		// Memory leak fixes for bot mode
		SlotTheory.Modifiers.ModEvents.Reset();  // Clear static event handlers
		
		_runAbandoned = false;
		_runState.Reset();
		_combatSim.ResetForWave();
		_endScreen.Visible = false;
		_pendingUnlockReveals.Clear();
		if (GodotObject.IsInstanceValid(_unlockRevealScreen))
			_unlockRevealScreen.Visible = false;
		Engine.TimeScale = 1.0;   // always reset speed on new run
		SoundManager.Instance?.SetMusicTension(0f);
		_hitStopActive = false;
		_hitStopCooldown = 0f;
		_hitStopFactor = 1f;
		_hitStopToken = 0;
		_spectacleSlowMoActive = false;
		_spectacleSlowMoToken = 0;
		_spectacleSlowMoFactor = 1f;
		RefreshSpectacleSpeedMultiplier();
		ClearExplosionResidues();
		_draftSynergyHintModifierId = "";
		_draftSynergyPulseT = 0f;
		_lowLivesHeartbeatTimer = 0f;
		_combatCalloutNextMs.Clear();
		_feedbackLoopBurst.Clear();
		_modifierProcCounts.Clear();
		_spectacleSystem.Reset();
		if (_botRunner != null)
			ResetBotTraceBuffer();
		ClearUndoPlacementState();
		_hudPanel.Refresh(1, Balance.StartingLives);
		_hudPanel.SetBuildName("", visible: false);
		_hudPanel.ResetSpeed();

			// In bot mode BotRunner.StartNextRun() already called SetPendingMapSelection
			// before RestartRun() — pick it up here since _Ready() only runs once.
			if (_botRunner != null)
			{
				_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
				if (_runState.SelectedMapId == "random_map")
					_runState.RngSeed = ResolveBotProceduralSeed();
			}
			else if (_runState.SelectedMapId == "random_map")
			{
				// Fresh procedural seed on player restarts for random_map.
				_runState.RngSeed = (int)(System.Environment.TickCount64 & 0x7FFFFFFF);
			}

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

	private int ResolveBotProceduralSeed()
	{
		// CompletedRuns is 0 for run 1, 1 for run 2, etc.
		// Shift by +1 to avoid seed 0.
		return (_botRunner?.CompletedRuns ?? 0) + 1;
	}

	private void ResetBotTraceBuffer()
	{
		_botRunTraceEvents.Clear();
		_botSurgeTraceCounter = 0;
		_botGlobalTraceCounter = 0;
		_botLastTraceTriggerId = string.Empty;
	}

	private string NextSurgeTraceId(bool global)
	{
		if (global)
			return $"global_{++_botGlobalTraceCounter}";
		return $"surge_{++_botSurgeTraceCounter}";
	}

	private void AppendBotTraceEvent(
		string eventType,
		EnemyInstance? enemy = null,
		Vector2? worldPos = null,
		float hpBefore = 0f,
		float hpAfter = 0f,
		string surgeTriggerId = "",
		string stageId = "",
		bool hitStopRequested = false,
		bool residueSpawned = false,
		string comboSkin = "",
		float? timestampOverride = null)
	{
		if (_botRunner == null)
			return;

		float timestamp = timestampOverride ?? (_runState?.TotalPlayTime ?? 0f);
		int enemyId = -1;
		float x = worldPos?.X ?? 0f;
		float y = worldPos?.Y ?? 0f;
		string statusTags = string.Empty;
		if (enemy != null && GodotObject.IsInstanceValid(enemy))
		{
			enemyId = unchecked((int)(enemy.GetInstanceId() & 0x7FFFFFFF));
			x = enemy.GlobalPosition.X;
			y = enemy.GlobalPosition.Y;
			statusTags = BuildEnemyStatusTags(enemy);
		}

		string resolvedTriggerId = string.IsNullOrWhiteSpace(surgeTriggerId)
			? _botLastTraceTriggerId
			: surgeTriggerId;
		_botRunTraceEvents.Add(new CombatLabTraceEvent
		{
			Timestamp = timestamp,
			EventType = eventType,
			EnemyId = enemyId,
			X = x,
			Y = y,
			HpBefore = hpBefore,
			HpAfter = hpAfter,
			StatusTags = statusTags,
			SurgeTriggerId = resolvedTriggerId,
			ExplosionStageId = stageId,
			HitStopRequested = hitStopRequested,
			ResidueSpawned = residueSpawned,
			ComboSkin = comboSkin,
		});
	}

	private static string BuildEnemyStatusTags(EnemyInstance enemy)
	{
		var tags = new List<string>(capacity: 3);
		if (enemy.IsMarked) tags.Add("mark");
		if (enemy.IsSlowed) tags.Add("slow");
		if (enemy.DamageAmpRemaining > 0f) tags.Add("amp");
		return string.Join(",", tags);
	}

	/// <summary>Called by DraftPanel after the player picks an option.</summary>
	public void OnDraftPick(DraftOption option, int targetSlotIndex)
	{
		_currentDraftOptions = null; // always generate fresh options for the next pick

		if (option.Type == DraftOptionType.Tower)
		{
			if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length && _runState.Slots[targetSlotIndex].Tower == null)
			{
				ClearTowerPlacementPreviewGhost();
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

	private void CancelPlacement()
	{
		ClearTowerPlacementPreviewGhost();
		ClearModifierPreviewGhost();
		_draftPanel.CancelAssignment();
		SoundManager.Instance?.Play("ui_select");
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
		var tower = _runState.Slots[slotIndex].TowerNode;
		if (tower == null) return;
		_spectacleSystem.RemoveTower(tower);
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
			SplitCount        = def.SplitCount,
			ChainCount        = def.ChainCount,
			ChainRange        = def.ChainRange,
			ChainDamageDecay  = def.ChainDamageDecay,
			ProjectileColor = GetTowerProjectileColor(towerId),
			BodyColor = GetTowerBodyColor(towerId),
		};
		tower.ZIndex = 20;

		bool mobile = MobileOptimization.IsMobile();
		float rangeFillAlpha = mobile ? 0.050f : 0.035f;
		float rangeBorderAlpha = mobile ? 0.16f : 0.11f;
		float rangeBorderWidth = mobile ? 1.25f : 1.05f;

		// Range indicator — semi-transparent fill in tower's body colour
		var rangeCircle = new Polygon2D
		{
			Color = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, rangeFillAlpha),
			ZIndex = -1,
			ShowBehindParent = true,
		};
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
			Width        = rangeBorderWidth,
			DefaultColor = new Color(tower.BodyColor.R, tower.BodyColor.G, tower.BodyColor.B, rangeBorderAlpha),
			ZIndex       = -1,
			ShowBehindParent = true,
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
			IconSet = tower.TowerId == "rift_prism" ? TargetModeIconSet.RiftSapper : TargetModeIconSet.Default,
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

	private static Color GetTowerProjectileColor(string towerId) => towerId switch
	{
		"rapid_shooter" => new Color(0.30f, 0.90f, 1.00f),  // cyan
		"heavy_cannon"  => new Color(1.00f, 0.55f, 0.00f),  // orange
		"marker_tower"  => new Color(0.75f, 0.30f, 1.00f),  // purple
		"chain_tower"   => new Color(0.55f, 0.90f, 1.00f),  // electric blue
		"rift_prism"    => new Color(0.70f, 1.00f, 0.56f),  // lime
		_               => Colors.Yellow,
	};

	private static Color GetTowerBodyColor(string towerId) => towerId switch
	{
		"rapid_shooter" => new Color(0.15f, 0.65f, 1.00f),
		"heavy_cannon"  => new Color(1.00f, 0.55f, 0.00f),
		"marker_tower"  => new Color(1.00f, 0.15f, 0.60f),
		"chain_tower"   => new Color(0.50f, 0.85f, 1.00f),
		"rift_prism"    => new Color(0.58f, 0.98f, 0.50f),
		_               => new Color(0.20f, 0.50f, 1.00f),
	};

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
					var tower = _runState.Slots[i].TowerNode;
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
			// Outside tap while previewing a tower cancels tower preview.
			else if (_draftPanel.IsAwaitingSlot && _draftPanel.HasTowerPreview)
			{
				_draftPanel.CancelTowerPreview();
				ClearTowerPlacementPreviewGhost();
				GetViewport().SetInputAsHandled();
			}
			return;
		}
		if (CurrentPhase == GamePhase.Wave)
		{
			for (int i = 0; i < _runState.Slots.Length; i++)
			{
				var tower = _runState.Slots[i].TowerNode;
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
			if (_mobileZoomLevel <= MobileMinZoom + MobileZoomSnapEpsilon)
				CenterMobileCamera();
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
		if (_mobileZoomLevel <= MobileMinZoom + MobileZoomSnapEpsilon)
			CenterMobileCamera();
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

	private void CenterMobileCamera()
	{
		if (!GodotObject.IsInstanceValid(_mobileCamera))
			return;
		Vector2 boundsCenter = _mobileCameraBounds.Position + (_mobileCameraBounds.Size * 0.5f);
		_mobileCamera.Position = ClampMobileCameraPosition(boundsCenter);
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
		// Neon grid background
		_mapVisuals.AddChild(new GridBackground());

		// Procedural scenery props (trees/rocks) are generated but previously not rendered.
		if (_currentMap is ProceduralMap procedural && procedural.Decorations.Length > 0)
		{
			var decoLayer = new MapDecorationLayer();
			decoLayer.Initialize(procedural.Decorations, _runState.RngSeed);
			_mapVisuals.AddChild(decoLayer);
		}

		// Neon path — tuned to keep lane separation dark and reduce red bleed between segments.
		Vector2[] pts = _currentMap.Path;
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 112f, DefaultColor = new Color(0.72f, 0.01f, 0.50f, 0.015f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 70f,  DefaultColor = new Color(0.64f, 0.03f, 0.46f, 0.030f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 46f,  DefaultColor = new Color(0.06f, 0.00f, 0.12f, 0.97f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 14f,  DefaultColor = new Color(1.00f, 0.12f, 0.58f, 0.11f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		_mapVisuals.AddChild(new Line2D { Points = pts, Width = 2.6f, DefaultColor = new Color(1.00f, 0.27f, 0.70f, 0.78f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
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

	private bool IsTooltipReady()
	{
		return _tooltipPanel != null
			&& _tooltipLabel != null
			&& GodotObject.IsInstanceValid(_tooltipPanel)
			&& GodotObject.IsInstanceValid(_tooltipLabel);
	}

	private void HideTooltip()
	{
		if (_tooltipPanel != null && GodotObject.IsInstanceValid(_tooltipPanel))
			_tooltipPanel.Visible = false;
		_selectedTooltipTower = null;
	}

	private void UpdateTooltip()
	{
		// Tooltip UI is irrelevant in headless automation runs.
		if (OS.HasFeature("headless") || DisplayServer.GetName() == "headless")
			return;

		if (!IsTooltipReady())
		{
			_selectedTooltipTower = null;
			return;
		}

		bool draftAwaitingTower = CurrentPhase == GamePhase.Draft
			&& _draftPanel != null
			&& GodotObject.IsInstanceValid(_draftPanel)
			&& _draftPanel.IsAwaitingTower;

		// Show during wave, and during modifier assignment so player can see what's on each tower
		bool tooltipAllowed = CurrentPhase == GamePhase.Wave
		                   || draftAwaitingTower;
		if (!tooltipAllowed || _runState?.Slots == null)
		{
			HideTooltip();
			return;
		}

		var viewport = GetViewport();
		if (viewport == null || !GodotObject.IsInstanceValid(viewport))
		{
			HideTooltip();
			return;
		}

		Vector2 mousePos;
		if (MobileOptimization.IsMobile())
		{
			if (_selectedTooltipTower == null || !GodotObject.IsInstanceValid(_selectedTooltipTower))
			{
				HideTooltip();
				return;
			}
			mousePos = _selectedTooltipTower.GlobalPosition;
		}
		else
		{
			mousePos = viewport.GetMousePosition();
		}
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null) continue;
			var hitRect = new Rect2(tower.GlobalPosition - new Vector2(25f, 25f), new Vector2(50f, 50f));
			if (!hitRect.HasPoint(mousePos)) continue;
			// Build tooltip text
			var def = DataLoader.GetTowerDef(tower.TowerId);
			var targetingName = tower.TowerId == "rift_prism"
				? tower.TargetingMode switch
				{
					TargetingMode.First     => "Random",
					TargetingMode.Strongest => "Closest",
					TargetingMode.LowestHp  => "Furthest",
					_                       => "Random",
				}
				: tower.TargetingMode switch
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
			if (tower.TowerId == "rift_prism")
			{
				text += $"plants up to {Balance.RiftMineMaxActivePerTower} mines  ({Balance.RiftMineChargesPerMine} charges each)\n";
				text += $"trigger {Balance.RiftMineTriggerRadius:0}px  burst seed {Balance.RiftMineBurstWindow:0.#}s (+{Balance.RiftMineBurstFastPlantsPerTower} fast plants)\n";
			}
			if (tower.IsChainTower)
				text += $"chains x{tower.ChainCount}  ({(int)(tower.ChainDamageDecay * 100)}% per bounce)  range {(int)tower.ChainRange} px\n";
			if (tower.SplitCount > 0)
				text += $"split shots x{tower.SplitCount + 1}  ({(int)(Balance.SplitShotDamageRatio * 100)}% each)\n";
			if (tower.Modifiers.Count == 0)
				text += "(no modifiers)";
			else
				foreach (var mod in tower.Modifiers)
				{
					var mdef = DataLoader.GetModifierDef(mod.ModifierId);
				text += "* " + mdef.Name + " " + mdef.Description + "\n";
				}
			text += "\n\n" + BuildSpectacleTooltipSection(tower);
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
				var vpSize = viewport.GetVisibleRect().Size;
				pos.X = Mathf.Clamp(pos.X, 4f, vpSize.X - _tooltipPanel.Size.X - 4f);
				pos.Y = Mathf.Clamp(pos.Y, 4f, vpSize.Y - _tooltipPanel.Size.Y - 4f);
				_tooltipPanel.Position = pos;
				_tooltipPanel.Visible = true;
				return;
		}
		HideTooltip();
	}

	private string BuildSpectacleTooltipSection(ITowerView tower)
	{
		var supportedMods = tower.Modifiers
			.Select(m => SpectacleDefinitions.NormalizeModId(m.ModifierId))
			.Where(SpectacleDefinitions.IsSupported)
			.Distinct()
			.ToList();

		if (supportedMods.Count == 0)
			return "Surge Triggers: (none)";

		if (supportedMods.Count == 1)
		{
			SpectacleSingleDef single = SpectacleDefinitions.GetSingle(supportedMods[0]);
			return $"Surge Triggers [Single]:\n* {single.Name}";
		}

		if (supportedMods.Count == 2)
		{
			SpectacleComboDef combo = SpectacleDefinitions.GetCombo(supportedMods[0], supportedMods[1]);
			return $"Surge Triggers [Combo]:\n* {combo.Name}";
		}

		var triadVariants = new HashSet<string>();
		for (int a = 0; a < supportedMods.Count; a++)
		{
			string augmentMod = supportedMods[a];
			SpectacleTriadAugmentDef aug = SpectacleDefinitions.GetTriadAugment(augmentMod);
			for (int i = 0; i < supportedMods.Count; i++)
			{
				if (i == a) continue;
				for (int j = i + 1; j < supportedMods.Count; j++)
				{
					if (j == a) continue;
					SpectacleComboDef combo = SpectacleDefinitions.GetCombo(supportedMods[i], supportedMods[j]);
					triadVariants.Add($"{combo.Name} + {aug.Name}");
				}
			}
		}

		if (triadVariants.Count == 0)
			return "Surge Triggers [Triad]:\n* (none)";

		var ordered = triadVariants.OrderBy(name => name, System.StringComparer.Ordinal).ToList();
		return $"Surge Triggers [Triad]:\n* {string.Join("\n* ", ordered)}";
	}
	// -- Bot multi-step simulation -------------------------------------------------

	private const float BOT_DT    = 0.05f;
	private const int   BOT_STEPS = 100;

	private void BotTick()
	{
		for (int i = 0; i < BOT_STEPS && CurrentPhase == GamePhase.Wave; i++)
		{
			_botStepExplosionBursts = 0;
			_botStepHitStopRequests = 0;

			// Manually advance enemies (PathFollow2D._Process is disabled in bot mode)
			foreach (var enemy in _runState.EnemiesAlive)
			{
				if (!GodotObject.IsInstanceValid(enemy)) continue;
				float spd = enemy.IsSlowed ? enemy.Speed * Balance.SlowSpeedFactor : enemy.Speed;
				enemy.Progress     += spd * BOT_DT;
				if (enemy.MarkedRemaining > 0f) enemy.MarkedRemaining -= BOT_DT;
				if (enemy.SlowRemaining   > 0f) enemy.SlowRemaining   -= BOT_DT;
				if (enemy.DamageAmpRemaining > 0f)
				{
					enemy.DamageAmpRemaining -= BOT_DT;
					if (enemy.DamageAmpRemaining <= 0f)
					{
						enemy.DamageAmpRemaining = 0f;
						enemy.DamageAmpMultiplier = 0f;
					}
				}
			}

			_spectacleSystem.Update(BOT_DT);
			UpdateExplosionResidues(BOT_DT);
			var result = _combatSim.Step(BOT_DT, _runState, _waveSystem);
			_botWaveSteps++;

			// Sample how many enemies are currently in range of at least one tower
			int inRange = 0;
			foreach (var e in _runState.EnemiesAlive)
				foreach (var sl in _runState.Slots)
					if (sl.Tower != null && sl.Tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= sl.Tower.Range)
					{ inRange++; break; }
			_botWaveInRangeSum     += inRange;
			_botWaveInRangeSamples += 1;
			_runState.TrackFrameStressProxies(_botStepExplosionBursts, _explosionResidues.Count, _botStepHitStopRequests);

			if (result == WaveResult.Loss)
			{
				CurrentPhase = GamePhase.Loss;
				_runState.CompleteWave();
				_botRunner!.RecordRunTrace(_botRunTraceEvents);
				_botRunner!.RecordResult(false, _runState.WaveIndex + 1, _runState);
				if (_botRunner.HasMoreRuns) { RestartRun(); return; }
				_botRunner.PrintSummary();
				GetTree().Quit();
				return;
			}

			if (result == WaveResult.WaveComplete)
			{
				_runState.CompleteWave();
				float avgInRange = _botWaveInRangeSamples > 0 ? (float)_botWaveInRangeSum / _botWaveInRangeSamples : 0f;
				_botWaveInRangeSum = _botWaveInRangeSamples = 0;
				_botRunner!.RecordWaveEnd(_runState.Lives, avgInRange, _botWaveSteps);
				_botWaveSteps = 0;
				_runState.WaveIndex++;
				if (_runState.WaveIndex >= Balance.TotalWaves)
				{
					CurrentPhase = GamePhase.Win;
					_botRunner.RecordRunTrace(_botRunTraceEvents);
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

		_spectacleAfterimage = new ColorRect();
		_spectacleAfterimage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_spectacleAfterimage.Color = new Color(0.94f, 0.99f, 1.00f, 0f);
		_spectacleAfterimage.Visible = false;
		_spectacleAfterimage.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_spectacleAfterimage);

		_spectaclePulse = new ColorRect();
		_spectaclePulse.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_spectaclePulse.Color = new Color(0.72f, 0.98f, 1.00f, 0f);
		_spectaclePulse.Visible = false;
		_spectaclePulse.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_spectaclePulse);

		// Sustained archetype tint — lingers after global surge, separate from fast flash
		_lingerTint = new ColorRect();
		_lingerTint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lingerTint.Color = new Color(1f, 0.90f, 0.56f, 0f);
		_lingerTint.Visible = false;
		_lingerTint.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_lingerTint);

		// Screen-edge vignette — intensifies during final 30% of global meter buildup
		_vignetteRect = new ColorRect();
		_vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_vignetteRect.Visible = false;
		_vignetteRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		var vignetteMat = new ShaderMaterial();
		var vignetteShader = new Shader();
		vignetteShader.Code = @"
shader_type canvas_item;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(1.0, 0.2, 0.1);
void fragment() {
    float dist = max(abs(UV.x - 0.5), abs(UV.y - 0.5)) * 2.0;
    float edge = smoothstep(0.65, 1.0, dist);
    COLOR = vec4(tint, edge * intensity * 0.09);
}";
		vignetteMat.Shader = vignetteShader;
		_vignetteRect.Material = vignetteMat;
		anchor.AddChild(_vignetteRect);

		_globalSurgeLayer = new CanvasLayer { Layer = 64 };
		AddChild(_globalSurgeLayer);
		var globalAnchor = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		globalAnchor.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_globalSurgeLayer.AddChild(globalAnchor);

		_globalSpectacleBanner = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			Visible = false,
			Modulate = new Color(1f, 1f, 1f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_globalSpectacleBanner.ZIndex = 10;
		UITheme.ApplyFont(_globalSpectacleBanner, bold: true, size: 118);
		_globalSpectacleBanner.AddThemeColorOverride("font_color", new Color(1.00f, 0.95f, 0.76f));
		_globalSpectacleBanner.AddThemeConstantOverride("outline_size", 10);
		_globalSpectacleBanner.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.10f, 0.16f, 0.98f));
		globalAnchor.AddChild(_globalSpectacleBanner);

		// Subtitle line — smaller text below the main banner, shows the gameplay effect.
		_globalSurgeSubtitleLabel = new Label
		{
			Text = "",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetTop = 52f,
			OffsetBottom = 92f,
			Visible = false,
			Modulate = new Color(1f, 1f, 1f, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_globalSurgeSubtitleLabel.ZIndex = 10;
		UITheme.ApplyFont(_globalSurgeSubtitleLabel, semiBold: true, size: 30);
		_globalSurgeSubtitleLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.98f, 1.00f));
		_globalSurgeSubtitleLabel.AddThemeConstantOverride("outline_size", 6);
		_globalSurgeSubtitleLabel.AddThemeColorOverride("font_outline_color", new Color(0.04f, 0.10f, 0.16f, 0.92f));
		globalAnchor.AddChild(_globalSurgeSubtitleLabel);
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

	private void ShowGlobalSurgeBanner(string effectName, Color accent, string subtitle, float lingerMultiplier = 1f)
	{
		if (!GodotObject.IsInstanceValid(_globalSpectacleBanner) || _botRunner != null)
			return;
		float holdSeconds = GlobalSurgeBannerHoldSeconds;
		float fadeSeconds = GlobalSurgeBannerFadeSeconds;
		ulong token = ++_globalSurgeBannerToken;
		if (_globalSurgeBannerTween != null && GodotObject.IsInstanceValid(_globalSurgeBannerTween))
			_globalSurgeBannerTween.Kill();

		_globalSpectacleBanner.Text = string.IsNullOrWhiteSpace(effectName) ? "GLOBAL SURGE" : effectName.ToUpperInvariant();
		_globalSpectacleBanner.Visible = true;
		_globalSpectacleBanner.PivotOffset = _globalSpectacleBanner.Size / 2f;
		_globalSpectacleBanner.Modulate = new Color(accent.R, accent.G, accent.B, 0f);
		_globalSpectacleBanner.AddThemeColorOverride(
			"font_color",
			new Color(
				Mathf.Clamp(accent.R * 0.82f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.82f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.82f + 0.18f, 0f, 1f),
				1f));

		bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle)
			&& GodotObject.IsInstanceValid(_globalSurgeSubtitleLabel);
		if (hasSubtitle)
		{
			_globalSurgeSubtitleLabel!.Text = subtitle.ToUpperInvariant();
			_globalSurgeSubtitleLabel.Visible = true;
			_globalSurgeSubtitleLabel.Modulate = new Color(1f, 1f, 1f, 0f);
		}

		// Pop in: scale 0.5 → 1.45 fast, alpha 0 → 1 fast
		// Use .From() to force the starting value regardless of current property state.
		_globalSurgeBannerTween = _globalSpectacleBanner.CreateTween();
		var tw = _globalSurgeBannerTween;
		tw.SetIgnoreTimeScale(true);
		tw.TweenProperty(_globalSpectacleBanner, "modulate:a", 1f, 0.08f).From(0f);
		tw.Parallel().TweenProperty(_globalSpectacleBanner, "scale", new Vector2(1.45f, 1.45f), 0.22f)
			.From(new Vector2(0.5f, 0.5f))
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		// Subtitle fades in slightly after the main label, with a gentle slide up.
		if (hasSubtitle)
		{
			tw.Parallel().TweenProperty(_globalSurgeSubtitleLabel, "modulate:a", 0.88f, 0.14f)
				.SetDelay(0.10f).From(0f);
		}

		GetTree().CreateTimer(holdSeconds, true, false, true).Timeout += () =>
		{
			if (token != _globalSurgeBannerToken || !GodotObject.IsInstanceValid(_globalSpectacleBanner))
				return;
			if (_globalSurgeBannerTween != null && GodotObject.IsInstanceValid(_globalSurgeBannerTween))
				_globalSurgeBannerTween.Kill();

			// Fade out: alpha 1 → 0 slow, scale 1.45 → 0.85 slow (drifts smaller as it fades)
			_globalSurgeBannerTween = _globalSpectacleBanner.CreateTween();
			var fadeTween = _globalSurgeBannerTween;
			fadeTween.SetIgnoreTimeScale(true);
			fadeTween.TweenProperty(_globalSpectacleBanner, "modulate:a", 0f, fadeSeconds)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			fadeTween.Parallel().TweenProperty(_globalSpectacleBanner, "scale", new Vector2(0.85f, 0.85f), fadeSeconds)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			if (hasSubtitle && GodotObject.IsInstanceValid(_globalSurgeSubtitleLabel))
				fadeTween.Parallel().TweenProperty(_globalSurgeSubtitleLabel, "modulate:a", 0f, fadeSeconds * 0.7f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			fadeTween.TweenCallback(Callable.From(() =>
			{
				if (token != _globalSurgeBannerToken)
					return;
				_globalSpectacleBanner.Visible = false;
				if (GodotObject.IsInstanceValid(_globalSurgeSubtitleLabel))
					_globalSurgeSubtitleLabel.Visible = false;
				_globalSurgeBannerTween = null;
			}));
		};
	}

	private bool TryCombatCallout(string id, float cooldownSec)
	{
		ulong now = Time.GetTicksMsec();
		if (_combatCalloutNextMs.TryGetValue(id, out ulong nextMs) && now < nextMs)
			return false;

		_combatCalloutNextMs[id] = now + (ulong)(Mathf.Max(0.1f, cooldownSec) * 1000f);
		return true;
	}

	private void SpawnCombatCallout(string text, Vector2 worldPos, Color color, float durationScale = 1f)
	{
		if (_botRunner != null) return;
		var callout = new CombatCallout();
		_worldNode.AddChild(callout);
		callout.GlobalPosition = worldPos + new Vector2(0f, -16f);
		callout.Initialize(text, color, duration: 0.96f * Mathf.Max(0.1f, durationScale));
	}

	// ── Surge differentiation helpers ──────────────────────────────────────────
	// Label/feel logic delegated to SurgeDifferentiation (pure, unit-testable).

	/// <summary>
	/// Spawn mode-based signature rings from a position — 1 ring for Single, 2 for Combo, 3 for Triad.
	/// Uses each mod slot's accent color. drama=1.0 for full scale, 0.25 for mini (tower surge rider).
	/// </summary>
	private void SpawnSurgeSignatureRings(Vector2 origin, SpectacleSignature sig, float drama)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode)) return;
		string[] mods = sig.Mode switch
		{
			SpectacleMode.Single => new[] { sig.PrimaryModId },
			SpectacleMode.Combo  => new[] { sig.PrimaryModId, sig.SecondaryModId },
			_                    => new[] { sig.PrimaryModId, sig.SecondaryModId, sig.TertiaryModId },
		};
		float baseRadius = 75f + drama * 130f;
		float baseDuration = 0.22f + drama * 0.40f;
		float ringWidth = 2.8f + drama * 3.2f;
		bool useRealTime = drama < 0.5f; // mini rings are real-time so they survive speed-up
		for (int i = 0; i < mods.Length; i++)
		{
			if (string.IsNullOrEmpty(mods[i])) continue;
			Color c = SlotTheory.UI.ModifierVisuals.GetAccent(mods[i]);
			float delay = i * (0.05f + drama * 0.04f);
			float radius = baseRadius * (1f + i * 0.11f);
			float dur = baseDuration + i * 0.04f;
			float rw = ringWidth;
			if (delay <= 0f)
				EmitSignatureRing(origin, c, radius, dur, rw);
			else
			{
				var cc = c; var cr = radius; var cd = dur; var cw = rw;
				GetTree().CreateTimer(delay, true, false, useRealTime).Timeout += () =>
					EmitSignatureRing(origin, cc, cr, cd, cw);
			}
		}
	}

	private void EmitSignatureRing(Vector2 origin, Color color, float endRadius, float duration, float ringWidth)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_worldNode)) return;
		var ripple = new GlobalSurgeRipple();
		_worldNode.AddChild(ripple);
		ripple.GlobalPosition = origin;
		ripple.Initialize(color, endRadius, duration, ringWidth);
	}

	/// <summary>
	/// Tower-type archetype FX — pattern=tower visual identity.
	/// drama: 1.0 = full (global surge), 0.28 = mini rider on tower surge.
	/// </summary>
	private void SpawnTowerArchetypeFx(TowerInstance tower, Color accent, float drama)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(tower) || !GodotObject.IsInstanceValid(_worldNode))
			return;
		switch (tower.TowerId)
		{
			case "chain_tower":   SpawnArchetypeChainArcs(tower, accent, drama); break;
			case "heavy_cannon":  SpawnArchetypeCannonRing(tower.GlobalPosition, accent, drama); break;
			case "rapid_shooter": SpawnArchetypeSparks(tower.GlobalPosition, accent, drama); break;
			case "marker_tower":  SpawnArchetypeMarkedFlash(accent, drama); break;
			case "rift_prism":    SpawnArchetypeRiftRing(tower.GlobalPosition, accent, drama); break;
		}
	}

	private void SpawnArchetypeChainArcs(TowerInstance tower, Color accent, float drama)
	{
		if (_runState == null) return;
		int count = drama >= 0.6f ? 2 : 1;
		var targets = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.OrderBy(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
			.Take(count)
			.ToList();
		foreach (var enemy in targets)
		{
			var arc = new ChainArc();
			_worldNode.AddChild(arc);
			arc.Initialize(tower.GlobalPosition, enemy.GlobalPosition, accent, intensity: 0.9f + drama * 1.1f);
		}
	}

	private void SpawnArchetypeCannonRing(Vector2 origin, Color accent, float drama)
	{
		var ripple = new GlobalSurgeRipple();
		_worldNode.AddChild(ripple);
		ripple.GlobalPosition = origin;
		ripple.Initialize(accent, 120f + drama * 220f, durationSec: 0.50f + drama * 0.50f, ringWidth: 5f + drama * 8f);
	}

	private void SpawnArchetypeSparks(Vector2 origin, Color accent, float drama)
	{
		int count = drama >= 0.6f ? 3 : 2;
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < count; i++)
		{
			float angle = rng.RandfRange(0f, Mathf.Tau);
			float dist = rng.RandfRange(12f, 55f * drama);
			var spark = new ImpactSparkBurst();
			_worldNode.AddChild(spark);
			spark.GlobalPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			spark.Initialize(accent, heavy: drama >= 0.6f);
		}
	}

	private void SpawnArchetypeMarkedFlash(Color accent, float drama)
	{
		if (_runState == null) return;
		FlashSpectacleScreen(accent, peakAlpha: 0.06f + drama * 0.14f, rampSec: 0.04f, fadeSec: 0.16f + drama * 0.18f);
		int maxFlashes = drama >= 0.6f ? 4 : 2;
		int count = 0;
		foreach (var enemy in _runState.EnemiesAlive)
		{
			if (!IsEnemyUsable(enemy) || enemy.MarkedRemaining <= 0f) continue;
			if (count++ >= maxFlashes) break;
			var spark = new ImpactSparkBurst();
			_worldNode.AddChild(spark);
			spark.GlobalPosition = enemy.GlobalPosition;
			spark.Initialize(accent, heavy: drama >= 0.6f);
		}
	}

	private void SpawnArchetypeRiftRing(Vector2 origin, Color accent, float drama)
	{
		// Rift Sapper identity: tight double-ring pulse
		for (int i = 0; i < 2; i++)
		{
			int idx = i;
			float delay = i * 0.10f;
			float radius = 55f + drama * 95f + idx * 28f;
			float dur = 0.28f + drama * 0.32f + idx * 0.05f;
			float rw = 2.8f + drama * 2.2f;
			if (delay <= 0f)
				EmitSignatureRing(origin, accent, radius, dur, rw);
			else
			{
				var cc = accent; var cr = radius; var cd = dur; var cw = rw;
				GetTree().CreateTimer(delay, true, false, true).Timeout += () =>
					EmitSignatureRing(origin, cc, cr, cd, cw);
			}
		}
	}

	// ── end surge differentiation helpers ──────────────────────────────────────

	private void OnSpectacleSurgeTriggered(SpectacleTriggerInfo info)
	{
		if (CurrentPhase != GamePhase.Wave)
			return;
		_runState?.TrackSpectacleSurge(info.Signature.EffectId, info.Tower?.TowerId);
		ComboExplosionSkin comboSkin = ResolveComboExplosionSkin(info.Signature);
		string traceId = NextSurgeTraceId(global: false);
		_botLastTraceTriggerId = traceId;
		AppendBotTraceEvent(
			eventType: "surge_triggered",
			surgeTriggerId: traceId,
			stageId: "trigger",
			comboSkin: comboSkin.ToString());
		AppendBotTraceEvent(
			eventType: "combo_skin_selected",
			surgeTriggerId: traceId,
			stageId: "skin",
			comboSkin: comboSkin.ToString());
		if (_botRunner != null)
		{
			ApplySpectacleGameplayPayload(info, isMajor: true);
			return;
		}
		// Surge chain counter — accumulates while global meter is building
		_surgeChainCount++;
		_surgeChainResetTimer = SpectacleDefinitions.ResolveGlobalContributionWindowSeconds();
		if (_surgeChainCount >= 2)
			SpawnSurgeChainCallout(_surgeChainCount, ResolveSpectacleColor(info.Signature.PrimaryModId));

		ITowerView? sourceTower = info.Tower;
		if (sourceTower == null)
			return;
		bool mobileLite = IsMobileSpectacleLite();

		Color accent = ResolveSpectacleColor(info.Signature.PrimaryModId);
		GD.Print($"[Surge] tower={sourceTower.TowerId}  mode={info.Signature.Mode}  effect={info.Signature.EffectName}  augment={info.Signature.AugmentName}  power={info.Signature.SurgePower:F2}");
		if (sourceTower is TowerInstance triggerTower && GodotObject.IsInstanceValid(triggerTower))
			triggerTower.FlashSpectacle(accent, major: true);
		float linkDistance = Mathf.Max(340f, sourceTower.Range * 1.30f);
		SpectacleConsequenceKind surgeRider = ResolveConsequenceKindFromSkin(comboSkin);
		float surgeConsequenceStrength = Mathf.Clamp(info.Signature.SurgePower, 0.6f, 2.2f) * 0.58f;
		SpawnSpectacleBurstFx(sourceTower.GlobalPosition, accent, major: true, power: info.Signature.SurgePower);
		SpawnSpectacleLinks(
			sourceTower.GlobalPosition,
			accent,
			maxLinks: 6,
			maxDistance: linkDistance,
			majorStyle: true,
			sourceTower: sourceTower,
			consequenceDamageScale: 0.12f + 0.03f * surgeConsequenceStrength,
			rider: surgeRider,
			riderStrength: surgeConsequenceStrength,
			spawnResidue: surgeRider != SpectacleConsequenceKind.None);
		SpawnSpectacleTowerVolleyFx(sourceTower, accent, major: true, power: info.Signature.SurgePower);
		SpawnComboExplosionSkinFx(sourceTower, info.Signature, accent, comboSkin);
		FlashSpectacleScreen(accent, peakAlpha: 0.17f, rampSec: 0.06f, fadeSec: 0.34f);
		QueueSpectacleEcho(
			sourceTower.GlobalPosition,
			accent,
			major: true,
			power: info.Signature.SurgePower,
			maxDistance: linkDistance,
			sourceTower: sourceTower,
			rider: surgeRider,
			spawnResidue: surgeRider != SpectacleConsequenceKind.None);
		bool isLightningSurge = info.Signature.PrimaryModId == SpectacleDefinitions.ChainReaction
		    || info.Signature.SecondaryModId == SpectacleDefinitions.ChainReaction;
		SoundManager.Instance?.Play(isLightningSurge ? "surge_lightning" : "surge");
		ApplySpectacleGameplayPayload(info, isMajor: true);
		TriggerStatusDetonationChain(
			sourceTower,
			sourceTower.GlobalPosition,
			accent,
			comboSkin,
			globalSurge: false,
			info.Signature.SurgePower);

		// Phase 1 (user amendment): mini mode-based signature rings — ripples=mode at tower level
		if (!mobileLite)
			SpawnSurgeSignatureRings(sourceTower.GlobalPosition, info.Signature, drama: 0.28f);

		// Phase 2: tower-type archetype identity FX (pattern=tower, mini at ~30% drama)
		if (sourceTower is TowerInstance towerForArchetype && !mobileLite)
			SpawnTowerArchetypeFx(towerForArchetype, accent, drama: 0.28f);

		// Phase 3: effect name callout (build archetype label)
		// For Triad surges use only the combo portion — the augment gets its own callout below.
		string surgeCalloutText = info.Signature.Mode == SpectacleMode.Triad
			&& !string.IsNullOrEmpty(info.Signature.ComboEffectName)
			? info.Signature.ComboEffectName
			: info.Signature.EffectName;
		SpawnCombatCallout(
			surgeCalloutText.ToUpperInvariant(),
			sourceTower.GlobalPosition,
			accent,
			durationScale: 4f);

		// Phase 5: Triad Factorio moment — augment name callout + second flash pulse
		if (info.Signature.Mode == SpectacleMode.Triad && !string.IsNullOrEmpty(info.Signature.AugmentName))
		{
			var capturedOrigin = sourceTower.GlobalPosition;
			Color augAccent = ResolveSpectacleColor(info.Signature.AugmentEffectId);
			string augName = info.Signature.AugmentName;
			GetTree().CreateTimer(0.28f, true, false, true).Timeout += () =>
			{
				if (!GodotObject.IsInstanceValid(this)) return;
				SpawnCombatCallout($"+ {augName.ToUpperInvariant()}", capturedOrigin + new Vector2(0f, +14f), augAccent, durationScale: 3f);
				FlashSpectacleScreen(augAccent, peakAlpha: 0.10f, rampSec: 0.04f, fadeSec: 0.20f);
			};
		}

		ExplosionHitStopProfile hitStop = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
			majorExplosion: true,
			globalSurge: false,
			info.Signature.SurgePower);
		if (hitStop.ShouldApply)
			TriggerHitStop(realDuration: hitStop.DurationSeconds, slowScale: hitStop.SlowScale);
		TriggerSpectacleSlowMo(realDuration: 0.5f, speedFactor: 0.50f);
		float afterimageStrength = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
			majorExplosion: true,
			globalSurge: false,
			info.Signature.SurgePower);
		FlashSpectacleAfterimage(accent, afterimageStrength);
	}

	private void OnGlobalSurgeTriggered(GlobalSurgeTriggerInfo info)
	{
		if (CurrentPhase != GamePhase.Wave)
			return;
		if (_runState == null)
			return;
		_runState.TrackSpectacleGlobal(info.EffectId);
		string traceId = NextSurgeTraceId(global: true);
		_botLastTraceTriggerId = traceId;
		AppendBotTraceEvent(
			eventType: "global_surge_triggered",
			surgeTriggerId: traceId,
			stageId: "trigger",
			comboSkin: "global");
		if (_botRunner != null)
		{
			ApplyGlobalSurgeGameplayPayload(info);
			return;
		}
		Vector2 center = ScreenToWorld(GetViewport().GetVisibleRect().Size * 0.5f);
		var globalColor = new Color(1.00f, 0.90f, 0.56f);
		ITowerView? globalDamageSource = ResolveSpectacleSourceTower(null);
		float globalDamageBase = ResolveGlobalSpectacleBaseDamage();
		float globalDurationScale = GlobalSurgeLingerMultiplier * GlobalSurgeDurationScale;

		// ── Resolve identity from dominant contributing mods ───────────────────────
		string[] dominantMods = info.DominantModIds ?? System.Array.Empty<string>();
		string surgeLabel = SurgeDifferentiation.ResolveLabel(dominantMods);
		SurgeDifferentiation.GlobalSurgeFeel feel = SurgeDifferentiation.ResolveFeel(dominantMods);
		float flashAlpha = SurgeDifferentiation.ResolveFlashAlpha(feel);

		// ── Clear buildup effects — vignette and chain counter ────────────────────
		_surgeChainCount = 0;
		_surgeChainResetTimer = 0f;
		if (GodotObject.IsInstanceValid(_vignetteRect))
			_vignetteRect.Visible = false;

		// ── Banner subtitle: mechanical summary of the payload ─────────────────────
		int subContribs = Mathf.Max(2, info.UniqueContributors);
		int refundPct = Mathf.RoundToInt(Mathf.Clamp(0.24f + 0.04f * subContribs, 0.24f, 0.46f) * 100f);
		string surgeSubtitle = $"Towers −{refundPct}% reload · Enemies marked & slowed";

		GD.Print($"[GlobalSurge] label={surgeLabel}  feel={feel}  dominantMods=[{string.Join(", ", dominantMods)}]  contributors={info.UniqueContributors}");

		// Multi-color ripples — one distinct mod color per contributing role (Phase 1)
		Color[] rippleColors = dominantMods.Length > 0
			? dominantMods.Select(SlotTheory.UI.ModifierVisuals.GetAccent).ToArray()
			: new[] { globalColor };

		// ── Group 1: immediate — gameplay payload + center visuals + hitstop/slowmo ──
		ApplyGlobalSurgeGameplayPayload(info);
		SpawnSpectacleBurstFx(center, globalColor, major: true, power: 2.15f, stageTwoKick: true);
		SpawnGlobalSurgeRipples(center, rippleColors, Mathf.Max(2, info.UniqueContributors), lingerMultiplier: globalDurationScale);
		FlashSpectacleScreen(globalColor, peakAlpha: flashAlpha, rampSec: 0.09f, fadeSec: 0.62f * globalDurationScale);
		// Sustained archetype tint — low-alpha linger keyed to feel
		Color lingerColor = feel switch
		{
			SurgeDifferentiation.GlobalSurgeFeel.Pressure    => new Color(0.85f, 0.08f, 0.08f),
			SurgeDifferentiation.GlobalSurgeFeel.Detonation  => new Color(1.00f, 0.50f, 0.08f),
			_                                                 => new Color(0.60f, 0.20f, 1.00f),
		};
		FlashSpectacleScreenLinger(lingerColor, alpha: 0.13f, holdSec: 1.0f, fadeSec: 1.4f);
		// Detonation: second snap-flash after brief delay (Phase 4)
		if (feel == SurgeDifferentiation.GlobalSurgeFeel.Detonation)
		{
			GetTree().CreateTimer(0.42f, true, false, true).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(this))
					FlashSpectacleScreen(globalColor, peakAlpha: 0.14f, rampSec: 0.04f, fadeSec: 0.24f);
			};
		}
		SoundManager.Instance?.Play("surge_global");
		ExplosionHitStopProfile hitStop = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
			majorExplosion: true,
			globalSurge: true,
			surgePower: 2.15f);
		if (hitStop.ShouldApply)
			TriggerHitStop(realDuration: hitStop.DurationSeconds, slowScale: hitStop.SlowScale);
		TriggerSpectacleSlowMo(realDuration: 14.0f, speedFactor: 0.50f);
		float afterimageStrength = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
			majorExplosion: true,
			globalSurge: true,
			surgePower: 2.15f);
		FlashSpectacleAfterimage(globalColor, afterimageStrength);

		// ── Group 2: per-tower effects staggered 0.07 s apart (real-time) ────────
		const float TowerStepSeconds = 0.07f;
		var activeTowers = new System.Collections.Generic.List<(TowerInstance tower, Color accent)>();
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].TowerNode;
			if (tower == null || !GodotObject.IsInstanceValid(tower))
				continue;
			Color accent = ResolveSpectacleColor(_spectacleSystem.PreviewSignature(tower).PrimaryModId);
			activeTowers.Add((tower, accent));
		}
		for (int i = 0; i < activeTowers.Count; i++)
		{
			float delay = i * TowerStepSeconds;
			var (t, accent) = activeTowers[i];
			GetTree().CreateTimer(delay, true, false, true).Timeout += () =>
			{
				if (!GodotObject.IsInstanceValid(t)) return;
				t.FlashSpectacle(accent, major: true);
				SpawnSpectacleBurstFx(t.GlobalPosition, accent, major: true, power: 1.12f);
				SpawnSpectacleLinks(
					t.GlobalPosition,
					accent,
					maxLinks: 2,
					maxDistance: Mathf.Max(280f, t.Range * 1.15f),
					majorStyle: true,
					sourceTower: t,
					consequenceDamageScale: 0.08f,
					rider: SpectacleConsequenceKind.Vulnerability,
					riderStrength: 0.92f);
				SpawnSpectacleTowerVolleyFx(t, accent, major: true, power: 1.10f);
				// Phase 1+2: signature rings (mode) + tower archetype pattern
				var tSig = _spectacleSystem.PreviewSignature(t);
				SpawnSurgeSignatureRings(t.GlobalPosition, tSig, drama: 0.7f);
				if (!IsMobileSpectacleLite())
					SpawnTowerArchetypeFx(t, accent, drama: 0.75f);
				// Persistent afterglow — tower stays lit for 2.4s after the surge sequence
				t.StartAfterGlow(accent, duration: 2.4f);
			};
		}

		// ── Group 3: finale after all towers have fired ───────────────────────────
		float finaleDelay = activeTowers.Count * TowerStepSeconds + 0.06f;
		GetTree().CreateTimer(finaleDelay, true, false, true).Timeout += () =>
		{
			SpawnGlobalSurgeAffectFx(
				center,
				globalColor,
				Mathf.Max(2, info.UniqueContributors),
				sourceTower: globalDamageSource,
				damageBaseOverride: globalDamageBase);
			TriggerStatusDetonationChain(
				sourceTower: globalDamageSource,
				origin: center,
				accent: globalColor,
				skin: ComboExplosionSkin.ChainArc,
				globalSurge: true,
				surgePower: 1.45f,
				damageBaseOverride: globalDamageBase);
			QueueSpectacleEcho(
				center,
				globalColor,
				major: true,
				power: 1.65f,
				maxDistance: 420f,
				sourceTower: globalDamageSource,
				rider: SpectacleConsequenceKind.Vulnerability,
				spawnResidue: true,
				damageBaseOverride: globalDamageBase);
			ShowGlobalSurgeBanner(surgeLabel, globalColor, surgeSubtitle, lingerMultiplier: globalDurationScale);
		// Phase 5: Triad Factorio moment — second flash pulse when 3 distinct mod identities converge
		if (dominantMods.Length >= 3)
		{
			GetTree().CreateTimer(0.55f, true, false, true).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(this))
					FlashSpectacleScreen(globalColor, peakAlpha: 0.16f, rampSec: 0.05f, fadeSec: 0.30f);
			};
		}
		};
	}

	private void ApplyGlobalSurgeGameplayPayload(GlobalSurgeTriggerInfo info)
	{
		if (_runState == null || _runState.Slots == null)
			return;

		int contributors = Mathf.Max(2, info.UniqueContributors);
		float perTowerScale = Mathf.Clamp(0.72f + 0.08f * contributors, 0.72f, 1.08f);
		float cooldownRefund = Mathf.Clamp(0.24f + 0.04f * contributors, 0.24f, 0.46f);

		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null)
				continue;
			if (tower is GodotObject towerObj && !GodotObject.IsInstanceValid(towerObj))
				continue;

			ReduceTowerCooldown(tower, cooldownRefund);
			SpectacleSignature signature = _spectacleSystem.PreviewSignature(tower);
			ApplySpectacleGameplayPayload(
				new SpectacleTriggerInfo(tower, IsSurge: true, signature, MeterAfter: 0f),
				isMajor: true,
				effectScale: perTowerScale);
		}

		float markDuration = 2.2f + 0.28f * contributors;
		float slowDuration = 1.8f + 0.24f * contributors;
		float slowFactor = Mathf.Clamp(0.88f - 0.06f * contributors, 0.58f, 0.90f);
		foreach (var enemy in _runState.EnemiesAlive)
		{
			if (!IsEnemyUsable(enemy))
				continue;
			Statuses.ApplyMarked(enemy, markDuration);
			Statuses.ApplySlow(enemy, slowDuration, slowFactor);
		}
	}

	private void ApplySpectacleGameplayPayload(SpectacleTriggerInfo info, bool isMajor, float effectScale = 1f)
	{
		GodotObject? towerObj = info.Tower as GodotObject;
		if (_runState == null
			|| info.Tower == null
			|| towerObj == null
			|| !GodotObject.IsInstanceValid(towerObj)
			|| effectScale <= 0f)
		{
			return;
		}

		float modePower = info.Signature.Mode switch
		{
			SpectacleMode.Single => isMajor ? 1.24f : 1.12f,
			SpectacleMode.Combo => isMajor ? 1.14f : 1.05f,
			_ => isMajor ? 1.20f : 1.10f,
		};
		float modeRange = info.Signature.Mode switch
		{
			SpectacleMode.Single => 0.96f,
			SpectacleMode.Combo => 1.06f,
			_ => 1.14f,
		};
		int modeTargetBonus = info.Signature.Mode switch
		{
			SpectacleMode.Single => 0,
			SpectacleMode.Combo => 1,
			_ => 2,
		};

		float basePower = info.Signature.SurgePower;
		float power = Mathf.Max(0.30f, basePower * modePower * Mathf.Max(0.20f, effectScale));
		float maxDistance = Mathf.Max(220f, info.Tower.Range * (isMajor ? 1.20f : 0.96f) * modeRange);
		int maxTargets = (isMajor ? 3 : 2) + modeTargetBonus;
		var seededTargets = GetSpectacleTargets(info.Tower.GlobalPosition, maxDistance, maxTargets, preferFront: true);
		Color accent = ResolveSpectacleColor(info.Signature.PrimaryModId);

		float coreDamage = info.Tower.BaseDamage * (isMajor ? (1.05f + 0.56f * power) : (0.42f + 0.44f * power));
		for (int i = 0; i < seededTargets.Count; i++)
		{
			float falloff = Mathf.Max(0.58f, 1f - i * 0.15f);
			ApplySpectacleDamage(
				info.Tower,
				seededTargets[i],
				coreDamage * falloff,
				accent,
				heavyHit: isMajor);
		}

		ApplyModSpectacleEffect(
			info.Tower,
			info.Signature.PrimaryModId,
			seededTargets,
			isMajor,
			power,
			accent,
			weight: info.Signature.Mode == SpectacleMode.Single ? 1.18f : 1.04f);

		if (info.Signature.Mode != SpectacleMode.Single && !string.IsNullOrEmpty(info.Signature.SecondaryModId))
		{
			float secondaryWeight = info.Signature.Mode == SpectacleMode.Combo
				? Mathf.Lerp(0.84f, 1.02f, info.Signature.SecondaryShare)
				: Mathf.Lerp(0.70f, 0.92f, info.Signature.SecondaryShare);
			ApplyModSpectacleEffect(
				info.Tower,
				info.Signature.SecondaryModId,
				seededTargets,
				isMajor,
				power * secondaryWeight,
				ResolveSpectacleColor(info.Signature.SecondaryModId),
				weight: secondaryWeight);
			ApplyComboSpectacleFinisher(info, seededTargets, isMajor, power * secondaryWeight);
		}

		if (info.Signature.Mode == SpectacleMode.Triad && !string.IsNullOrEmpty(info.Signature.TertiaryModId))
		{
			float augmentScale = isMajor
				? Mathf.Lerp(2.10f, 2.55f, info.Signature.TertiaryShare)
				: Mathf.Lerp(1.50f, 1.95f, info.Signature.TertiaryShare);
			ApplyTriadAugmentSpectacleEffect(
				info,
				seededTargets,
				isMajor,
				effectScale * augmentScale,
				ResolveSpectacleColor(info.Signature.TertiaryModId));
		}
	}

	private void ApplyComboSpectacleFinisher(
		SpectacleTriggerInfo info,
		List<EnemyInstance> seededTargets,
		bool isMajor,
		float power)
	{
		if (string.IsNullOrEmpty(info.Signature.PrimaryModId) || string.IsNullOrEmpty(info.Signature.SecondaryModId))
			return;

		string a = SpectacleDefinitions.NormalizeModId(info.Signature.PrimaryModId);
		string b = SpectacleDefinitions.NormalizeModId(info.Signature.SecondaryModId);
		string key = BuildModPairKey(a, b);
		var tower = info.Tower;
		EnemyInstance? primary = seededTargets.FirstOrDefault(IsEnemyUsable);
		if (primary == null)
			return;

		float finisherPower = Mathf.Clamp(power * (isMajor ? 0.80f : 0.54f), 0.22f, 2.4f);
		Color secondaryColor = ResolveSpectacleColor(info.Signature.SecondaryModId);

		List<EnemyInstance> PickTargets(Vector2 center, float radius, int count, bool preferFront = false)
			=> GetSpectacleTargets(center, radius, count, preferFront).Where(IsEnemyUsable).ToList();

		EnemyInstance? FindFarthestInRange(float radius)
			=> _runState.EnemiesAlive
				.Where(IsEnemyUsable)
				.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
				.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
				.ThenByDescending(e => e.ProgressRatio)
				.FirstOrDefault();

		void ApplyMarkMany(IEnumerable<EnemyInstance> targets, float duration)
		{
			foreach (var e in targets)
				Statuses.ApplyMarked(e, duration);
		}

		void ApplySlowMany(IEnumerable<EnemyInstance> targets, float duration, float factor)
		{
			foreach (var e in targets)
				Statuses.ApplySlow(e, duration, factor);
		}

		void DamageWave(
			IReadOnlyList<EnemyInstance> targets,
			float baseDamage,
			float falloffPerStep,
			Color color,
			bool heavy,
			Vector2? arcOrigin = null)
		{
			for (int i = 0; i < targets.Count; i++)
			{
				float falloff = Mathf.Max(0.56f, 1f - i * falloffPerStep);
				if (arcOrigin.HasValue)
				{
					SpawnSpectacleArc(
						arcOrigin.Value,
						targets[i].GlobalPosition,
						color,
						intensity: 0.92f + i * 0.06f,
						mineChainStyle: heavy);
				}
				ApplySpectacleDamage(tower, targets[i], baseDamage * falloff, color, heavyHit: heavy);
			}
		}

		switch (key)
		{
			case "momentum|overkill":
			{
				ReduceTowerCooldown(tower, 0.10f + 0.12f * finisherPower);
				var blast = PickTargets(primary.GlobalPosition, 150f + 50f * finisherPower, isMajor ? 4 : 3);
				DamageWave(blast, tower.BaseDamage * (isMajor ? 0.78f : 0.44f) * finisherPower, 0.14f, secondaryColor, isMajor, primary.GlobalPosition);
				break;
			}
			case "exploit_weakness|momentum":
			{
				bool wasMarked = primary.IsMarked;
				Statuses.ApplyMarked(primary, (isMajor ? 3.8f : 2.6f) + 1.2f * finisherPower);
				float execute = tower.BaseDamage * (wasMarked ? (isMajor ? 1.02f : 0.60f) : (isMajor ? 0.66f : 0.40f)) * finisherPower;
				ApplySpectacleDamage(tower, primary, execute, secondaryColor, heavyHit: isMajor, triggerHitStopOnKill: wasMarked && isMajor);
				ReduceTowerCooldown(tower, 0.06f + 0.08f * finisherPower);
				break;
			}
			case "focus_lens|momentum":
			{
				float beam = tower.BaseDamage * (isMajor ? 1.10f : 0.70f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.20f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				ReduceTowerCooldown(tower, 0.10f + 0.08f * finisherPower);
				break;
			}
			case "momentum|slow":
			{
				var pack = PickTargets(primary.GlobalPosition, 180f + 30f * finisherPower, isMajor ? 4 : 3);
				float slowFactor = isMajor
					? Mathf.Clamp(0.66f - 0.10f * finisherPower, 0.26f, 0.78f)
					: Mathf.Clamp(0.78f - 0.08f * finisherPower, 0.36f, 0.88f);
				ApplySlowMany(pack, (isMajor ? 3.4f : 2.4f) + 0.9f * finisherPower, slowFactor);
				DamageWave(pack, tower.BaseDamage * (isMajor ? 0.64f : 0.38f) * finisherPower, 0.13f, secondaryColor, false);
				break;
			}
			case "momentum|overreach":
			{
				var far = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range * 1.45f)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.Take(isMajor ? 4 : 3)
					.ToList();
				DamageWave(far, tower.BaseDamage * (isMajor ? 0.74f : 0.42f) * finisherPower, 0.15f, secondaryColor, isMajor);
				break;
			}
			case "hair_trigger|momentum":
			{
				ReduceTowerCooldown(tower, 0.16f + 0.14f * finisherPower);
				float hit = tower.BaseDamage * (isMajor ? 0.54f : 0.32f) * finisherPower;
				int bursts = isMajor ? 3 : 2;
				for (int i = 0; i < bursts; i++)
				{
					float falloff = Mathf.Pow(0.72f, i);
					ApplySpectacleDamage(tower, primary, hit * falloff, secondaryColor, heavyHit: false);
				}
				break;
			}
			case "momentum|split_shot":
			{
				var targets = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.25f, isMajor ? 5 : 3)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				DamageWave(targets, tower.BaseDamage * (isMajor ? 0.70f : 0.44f) * finisherPower, 0.12f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "feedback_loop|momentum":
			{
				ReduceTowerCooldown(tower, 0.24f + 0.16f * finisherPower);
				float burst = tower.BaseDamage * (isMajor ? 0.82f : 0.52f) * finisherPower;
				ApplySpectacleDamage(tower, primary, burst, secondaryColor, heavyHit: isMajor);
				if (tower.Cooldown <= Mathf.Max(0.06f, tower.AttackInterval * 0.08f))
					ApplySpectacleDamage(tower, primary, burst * 0.58f, secondaryColor, heavyHit: false);
				break;
			}
			case "chain_reaction|momentum":
			{
				ReduceTowerCooldown(tower, 0.08f + 0.08f * finisherPower);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower),
					startDamage: tower.BaseDamage * (isMajor ? 0.62f : 0.36f) * finisherPower,
					decay: 0.74f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.12f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "exploit_weakness|overkill":
			{
				var markedPack = PickTargets(primary.GlobalPosition, 170f, isMajor ? 4 : 3, preferFront: true);
				ApplyMarkMany(markedPack, (isMajor ? 3.6f : 2.5f) + 1.0f * finisherPower);
				DamageWave(markedPack, tower.BaseDamage * (isMajor ? 0.70f : 0.40f) * finisherPower, 0.16f, secondaryColor, isMajor, primary.GlobalPosition);
				break;
			}
			case "focus_lens|overkill":
			{
				float beam = tower.BaseDamage * (isMajor ? 1.24f : 0.80f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.26f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				var spill = PickTargets(primary.GlobalPosition, 150f, isMajor ? 3 : 2);
				DamageWave(spill, tower.BaseDamage * (isMajor ? 0.44f : 0.26f) * finisherPower, 0.20f, secondaryColor, false);
				break;
			}
			case "overkill|slow":
			{
				var pack = PickTargets(primary.GlobalPosition, 190f, isMajor ? 4 : 3);
				ApplySlowMany(pack, (isMajor ? 3.2f : 2.2f) + 0.8f * finisherPower, Mathf.Clamp(0.74f - 0.10f * finisherPower, 0.30f, 0.86f));
				DamageWave(pack, tower.BaseDamage * (isMajor ? 0.58f : 0.34f) * finisherPower, 0.15f, secondaryColor, false);
				break;
			}
			case "overkill|overreach":
			{
				var line = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range * 1.44f)
					.OrderByDescending(e => e.ProgressRatio)
					.Take(isMajor ? 4 : 3)
					.ToList();
				DamageWave(line, tower.BaseDamage * (isMajor ? 0.76f : 0.42f) * finisherPower, 0.14f, secondaryColor, isMajor, tower.GlobalPosition);
				break;
			}
			case "hair_trigger|overkill":
			{
				ReduceTowerCooldown(tower, 0.14f + 0.14f * finisherPower);
				float burst = tower.BaseDamage * (isMajor ? 0.66f : 0.38f) * finisherPower;
				ApplySpectacleDamage(tower, primary, burst, secondaryColor, heavyHit: isMajor);
				var extra = PickTargets(primary.GlobalPosition, 150f, isMajor ? 2 : 1).Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(extra, burst * 0.82f, 0.18f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "overkill|split_shot":
			{
				var shards = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.22f, isMajor ? 5 : 3, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				DamageWave(shards, tower.BaseDamage * (isMajor ? 0.74f : 0.46f) * finisherPower, 0.13f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "feedback_loop|overkill":
			{
				ReduceTowerCooldown(tower, 0.20f + 0.14f * finisherPower);
				float before = primary.Hp;
				float slam = tower.BaseDamage * (isMajor ? 0.94f : 0.56f) * finisherPower;
				ApplySpectacleDamage(tower, primary, slam, secondaryColor, heavyHit: isMajor);
				if (before <= slam * 1.08f)
				{
					var over = PickTargets(primary.GlobalPosition, 165f, isMajor ? 3 : 2).Where(e => !ReferenceEquals(e, primary)).ToList();
					DamageWave(over, slam * 0.52f, 0.18f, secondaryColor, false, primary.GlobalPosition);
				}
				break;
			}
			case "chain_reaction|overkill":
			{
				var spill = PickTargets(primary.GlobalPosition, 145f, isMajor ? 3 : 2).Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(spill, tower.BaseDamage * (isMajor ? 0.44f : 0.26f) * finisherPower, 0.18f, secondaryColor, false);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: isMajor ? 4 : 2,
					startDamage: tower.BaseDamage * (isMajor ? 0.52f : 0.30f) * finisherPower,
					decay: 0.72f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.10f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "exploit_weakness|focus_lens":
			{
				Statuses.ApplyMarked(primary, (isMajor ? 4.1f : 2.9f) + 1.2f * finisherPower);
				float beam = tower.BaseDamage * (isMajor ? 1.12f : 0.70f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.24f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				break;
			}
			case "exploit_weakness|slow":
			{
				var hunted = PickTargets(primary.GlobalPosition, 185f, isMajor ? 4 : 3, preferFront: true);
				ApplyMarkMany(hunted, (isMajor ? 3.4f : 2.2f) + 1.0f * finisherPower);
				ApplySlowMany(hunted, (isMajor ? 2.8f : 2.0f) + 0.8f * finisherPower, Mathf.Clamp(0.76f - 0.10f * finisherPower, 0.28f, 0.86f));
				DamageWave(hunted, tower.BaseDamage * (isMajor ? 0.52f : 0.30f) * finisherPower, 0.15f, secondaryColor, false);
				break;
			}
			case "exploit_weakness|overreach":
			{
				var far = FindFarthestInRange(tower.Range * 1.45f);
				if (far == null) break;
				Statuses.ApplyMarked(far, (isMajor ? 4.2f : 3.0f) + 1.2f * finisherPower);
				SpawnSpectacleArc(tower.GlobalPosition, far.GlobalPosition, secondaryColor, intensity: 1.16f, mineChainStyle: isMajor);
				ApplySpectacleDamage(tower, far, tower.BaseDamage * (isMajor ? 1.04f : 0.64f) * finisherPower, secondaryColor, heavyHit: isMajor);
				break;
			}
			case "exploit_weakness|hair_trigger":
			{
				Statuses.ApplyMarked(primary, (isMajor ? 3.6f : 2.4f) + 0.9f * finisherPower);
				ReduceTowerCooldown(tower, 0.16f + 0.12f * finisherPower);
				float tick = tower.BaseDamage * (isMajor ? 0.58f : 0.36f) * finisherPower;
				ApplySpectacleDamage(tower, primary, tick, secondaryColor, heavyHit: false);
				ApplySpectacleDamage(tower, primary, tick * 0.82f, secondaryColor, heavyHit: false);
				if (isMajor)
					ApplySpectacleDamage(tower, primary, tick * 0.56f, secondaryColor, heavyHit: false);
				break;
			}
			case "exploit_weakness|split_shot":
			{
				var marked = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.30f, isMajor ? 5 : 3, preferFront: true);
				ApplyMarkMany(marked, (isMajor ? 3.1f : 2.2f) + 0.8f * finisherPower);
				DamageWave(marked, tower.BaseDamage * (isMajor ? 0.54f : 0.32f) * finisherPower, 0.12f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "exploit_weakness|feedback_loop":
			{
				Statuses.ApplyMarked(primary, (isMajor ? 4.0f : 2.8f) + 1.1f * finisherPower);
				ReduceTowerCooldown(tower, 0.24f + 0.16f * finisherPower);
				float execute = tower.BaseDamage * (isMajor ? 0.94f : 0.60f) * finisherPower;
				ApplySpectacleDamage(tower, primary, execute, secondaryColor, heavyHit: isMajor);
				break;
			}
			case "chain_reaction|exploit_weakness":
			{
				var markSet = PickTargets(primary.GlobalPosition, 220f, isMajor ? 4 : 3, preferFront: true);
				ApplyMarkMany(markSet, (isMajor ? 3.6f : 2.4f) + 0.8f * finisherPower);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: isMajor ? 4 : 2,
					startDamage: tower.BaseDamage * (isMajor ? 0.50f : 0.30f) * finisherPower,
					decay: 0.75f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.08f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "focus_lens|slow":
			{
				float beam = tower.BaseDamage * (isMajor ? 1.08f : 0.66f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.18f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true);
				var aura = PickTargets(primary.GlobalPosition, 165f, isMajor ? 4 : 3);
				ApplySlowMany(aura, (isMajor ? 3.0f : 2.2f) + 0.8f * finisherPower, Mathf.Clamp(0.72f - 0.10f * finisherPower, 0.26f, 0.86f));
				break;
			}
			case "focus_lens|overreach":
			{
				var far = FindFarthestInRange(tower.Range * 1.55f);
				if (far == null) break;
				float rail = tower.BaseDamage * (isMajor ? 1.06f : 0.70f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, far.GlobalPosition, secondaryColor, intensity: 1.22f, mineChainStyle: true);
				ApplySpectacleDamage(tower, far, rail, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				var pierce = PickTargets(far.GlobalPosition, 150f, isMajor ? 2 : 1, preferFront: true).Where(e => !ReferenceEquals(e, far)).ToList();
				DamageWave(pierce, rail * 0.46f, 0.20f, secondaryColor, false, far.GlobalPosition);
				break;
			}
			case "focus_lens|hair_trigger":
			{
				ReduceTowerCooldown(tower, 0.18f + 0.14f * finisherPower);
				float beam = tower.BaseDamage * (isMajor ? 0.84f : 0.54f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.12f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: isMajor);
				ApplySpectacleDamage(tower, primary, beam * 0.72f, secondaryColor, heavyHit: false);
				break;
			}
			case "focus_lens|split_shot":
			{
				var prism = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.34f, isMajor ? 6 : 4, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				DamageWave(prism, tower.BaseDamage * (isMajor ? 0.66f : 0.40f) * finisherPower, 0.11f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "feedback_loop|focus_lens":
			{
				ReduceTowerCooldown(tower, 0.20f + 0.14f * finisherPower);
				float beam = tower.BaseDamage * (isMajor ? 1.00f : 0.64f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.22f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true);
				if (tower.Cooldown <= Mathf.Max(0.06f, tower.AttackInterval * 0.08f))
					ApplySpectacleDamage(tower, primary, beam * 0.46f, secondaryColor, heavyHit: false);
				break;
			}
			case "chain_reaction|focus_lens":
			{
				float lance = tower.BaseDamage * (isMajor ? 0.96f : 0.60f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.18f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, lance, secondaryColor, heavyHit: true);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: isMajor ? 4 : 2,
					startDamage: lance * 0.62f,
					decay: 0.76f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.12f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "overreach|slow":
			{
				float radius = Mathf.Max(320f, tower.Range * 1.36f);
				var targets = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.ThenByDescending(e => e.ProgressRatio)
					.Take(isMajor ? 4 : 3)
					.ToList();
				float dmg = tower.BaseDamage * (isMajor ? 0.62f : 0.36f) * finisherPower;
				float slowFactor = isMajor
					? Mathf.Clamp(0.60f - 0.12f * finisherPower, 0.24f, 0.80f)
					: Mathf.Clamp(0.74f - 0.10f * finisherPower, 0.34f, 0.88f);
				float slowDuration = (isMajor ? 3.8f : 2.6f) + 1.1f * finisherPower;
				for (int i = 0; i < targets.Count; i++)
				{
					float falloff = Mathf.Max(0.62f, 1f - i * 0.14f);
					Statuses.ApplySlow(targets[i], slowDuration, slowFactor);
					ApplySpectacleDamage(tower, targets[i], dmg * falloff, secondaryColor, heavyHit: isMajor);
				}
				break;
			}
			case "hair_trigger|slow":
			{
				ReduceTowerCooldown(tower, 0.20f + 0.14f * finisherPower);
				var chill = PickTargets(primary.GlobalPosition, 170f, isMajor ? 4 : 3, preferFront: false);
				ApplySlowMany(chill, (isMajor ? 2.9f : 2.1f) + 0.8f * finisherPower, Mathf.Clamp(0.70f - 0.09f * finisherPower, 0.28f, 0.86f));
				DamageWave(chill, tower.BaseDamage * (isMajor ? 0.66f : 0.40f) * finisherPower, 0.14f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "slow|split_shot":
			{
				var bloom = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.36f, isMajor ? 6 : 4, preferFront: false);
				ApplySlowMany(bloom, (isMajor ? 2.8f : 2.0f) + 0.7f * finisherPower, Mathf.Clamp(0.72f - 0.08f * finisherPower, 0.30f, 0.88f));
				DamageWave(bloom, tower.BaseDamage * (isMajor ? 0.58f : 0.34f) * finisherPower, 0.11f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "feedback_loop|slow":
			{
				ReduceTowerCooldown(tower, 0.22f + 0.15f * finisherPower);
				var frosted = PickTargets(primary.GlobalPosition, 180f, isMajor ? 4 : 3, preferFront: true);
				float slowDuration = (isMajor ? 3.4f : 2.4f) + 0.9f * finisherPower;
				foreach (var e in frosted)
				{
					bool wasSlowed = e.SlowRemaining > 0f;
					Statuses.ApplySlow(e, slowDuration, Mathf.Clamp(0.70f - 0.10f * finisherPower, 0.26f, 0.84f));
					float dmg = tower.BaseDamage * (wasSlowed ? (isMajor ? 0.66f : 0.40f) : (isMajor ? 0.46f : 0.28f)) * finisherPower;
					ApplySpectacleDamage(tower, e, dmg, secondaryColor, heavyHit: false);
				}
				break;
			}
			case "chain_reaction|slow":
			{
				Statuses.ApplySlow(primary, (isMajor ? 3.4f : 2.3f) + 0.9f * finisherPower, Mathf.Clamp(0.72f - 0.10f * finisherPower, 0.28f, 0.86f));
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower * 0.8f),
					startDamage: tower.BaseDamage * (isMajor ? 0.56f : 0.34f) * finisherPower,
					decay: 0.76f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.10f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "hair_trigger|overreach":
			{
				ReduceTowerCooldown(tower, 0.20f + 0.12f * finisherPower);
				var far = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range * 1.50f)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.Take(isMajor ? 5 : 3)
					.ToList();
				DamageWave(far, tower.BaseDamage * (isMajor ? 0.70f : 0.40f) * finisherPower, 0.13f, secondaryColor, false);
				break;
			}
			case "overreach|split_shot":
			{
				var frontline = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.OrderByDescending(e => e.ProgressRatio)
					.ThenBy(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.FirstOrDefault();
				var center = frontline ?? primary;
				var wide = PickTargets(center.GlobalPosition, Balance.SplitShotRange * 1.42f, isMajor ? 6 : 4, preferFront: true);
				float baseDamage = tower.BaseDamage * (isMajor ? 0.78f : 0.46f) * finisherPower;
				float floorDamage = tower.BaseDamage
					* (isMajor ? 0.18f : 0.11f)
					* Mathf.Clamp(finisherPower, 0.70f, 1.60f);
				for (int i = 0; i < wide.Count; i++)
				{
					float falloff = Mathf.Max(0.62f, 1f - i * 0.11f);
					float damage = Mathf.Max(baseDamage * falloff, floorDamage * falloff);
					SpawnSpectacleArc(center.GlobalPosition, wide[i].GlobalPosition, secondaryColor, intensity: 1.00f + i * 0.06f, mineChainStyle: false);
					ApplySpectacleDamage(tower, wide[i], damage, secondaryColor, heavyHit: false);
				}
				break;
			}
			case "feedback_loop|overreach":
			{
				ReduceTowerCooldown(tower, (isMajor ? 0.20f : 0.12f) + 0.16f * finisherPower);
				float radius = Mathf.Max(300f, tower.Range * 1.34f);
				var far = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.FirstOrDefault();
				if (far != null)
				{
					float dmg = tower.BaseDamage * (isMajor ? 0.88f : 0.52f) * finisherPower;
					SpawnSpectacleArc(tower.GlobalPosition, far.GlobalPosition, secondaryColor, intensity: 1.16f, mineChainStyle: true);
					ApplySpectacleDamage(tower, far, dmg, secondaryColor, heavyHit: true);
				}
				break;
			}
			case "chain_reaction|overreach":
			{
				var seed = FindFarthestInRange(tower.Range * 1.44f) ?? primary;
				ApplySpectacleChain(
					tower,
					seed,
					maxBounces: isMajor ? 5 : 3,
					startDamage: tower.BaseDamage * (isMajor ? 0.62f : 0.36f) * finisherPower,
					decay: 0.78f,
					linkRange: Mathf.Max(230f, tower.ChainRange * 1.14f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "hair_trigger|split_shot":
			{
				Vector2 center = primary.GlobalPosition;
				int extraCount = isMajor ? 5 : 3;
				float volley = tower.BaseDamage * (isMajor ? 0.58f : 0.34f) * finisherPower;
				var extras = GetSpectacleTargets(center, Balance.SplitShotRange * 1.34f, extraCount, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < extras.Count; i++)
				{
					float falloff = Mathf.Max(0.68f, 1f - i * 0.10f);
					SpawnSpectacleArc(center, extras[i].GlobalPosition, secondaryColor, intensity: 0.96f + 0.05f * i);
					ApplySpectacleDamage(tower, extras[i], volley * falloff, secondaryColor, heavyHit: false);
				}
				ReduceTowerCooldown(tower, (isMajor ? 0.18f : 0.11f) + 0.10f * finisherPower);
				break;
			}
			case "feedback_loop|hair_trigger":
			{
				ReduceTowerCooldown(tower, 0.30f + 0.16f * finisherPower);
				float burst = tower.BaseDamage * (isMajor ? 0.74f : 0.44f) * finisherPower;
				ApplySpectacleDamage(tower, primary, burst, secondaryColor, heavyHit: isMajor);
				ApplySpectacleDamage(tower, primary, burst * 0.66f, secondaryColor, heavyHit: false);
				break;
			}
			case "chain_reaction|hair_trigger":
			{
				ReduceTowerCooldown(tower, 0.14f + 0.10f * finisherPower);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower),
					startDamage: tower.BaseDamage * (isMajor ? 0.58f : 0.34f) * finisherPower,
					decay: 0.74f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.08f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			case "feedback_loop|split_shot":
			{
				ReduceTowerCooldown(tower, 0.24f + 0.14f * finisherPower);
				var bloom = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.38f, isMajor ? 6 : 4, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				DamageWave(bloom, tower.BaseDamage * (isMajor ? 0.62f : 0.36f) * finisherPower, 0.11f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "chain_reaction|split_shot":
			{
				int bounces = (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower);
				float startDamage = tower.BaseDamage * (isMajor ? 0.52f : 0.31f) * finisherPower;
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: bounces,
					startDamage,
					decay: 0.76f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.15f),
					secondaryColor,
					heavy: isMajor);
				var extras = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.30f, isMajor ? 4 : 2, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				DamageWave(extras, startDamage * 0.52f, 0.14f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			case "chain_reaction|feedback_loop":
			{
				ReduceTowerCooldown(tower, 0.22f + 0.12f * finisherPower);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: isMajor ? 5 : 3,
					startDamage: tower.BaseDamage * (isMajor ? 0.56f : 0.34f) * finisherPower,
					decay: 0.74f,
					linkRange: Mathf.Max(230f, tower.ChainRange * 1.14f),
					secondaryColor,
					heavy: isMajor);
				break;
			}
			default:
			{
				float burst = tower.BaseDamage * (isMajor ? 0.50f : 0.30f) * finisherPower;
				ApplySpectacleDamage(tower, primary, burst, secondaryColor, heavyHit: false);
				break;
			}
		}
	}

	private void ApplyModSpectacleEffect(
		ITowerView tower,
		string modId,
		List<EnemyInstance> seededTargets,
		bool isMajor,
		float power,
		Color accent,
		float weight)
	{
		if (tower == null || string.IsNullOrEmpty(modId))
			return;

		string normalized = SpectacleDefinitions.NormalizeModId(modId);
		float tempoBoost = isMajor ? 1.22f : 1.10f;
		float p = Mathf.Max(0.24f, power * Mathf.Clamp(weight, 0.25f, 1.45f) * tempoBoost);
		EnemyInstance? primary = seededTargets.FirstOrDefault(IsEnemyUsable);

		switch (normalized)
		{
			case SpectacleDefinitions.Momentum:
			{
				ReduceTowerCooldown(tower, (isMajor ? 0.14f : 0.08f) * Mathf.Clamp(p, 0.5f, 2.0f));
				if (primary != null)
				{
					float bonus = tower.BaseDamage * (isMajor ? 0.38f : 0.20f) * p;
					ApplySpectacleDamage(tower, primary, bonus, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.Overkill:
			{
				if (primary == null)
					break;
				float splashDamage = tower.BaseDamage * (isMajor ? 0.58f : 0.30f) * p;
				var spillTargets = GetSpectacleTargets(
					primary.GlobalPosition,
					isMajor ? 180f : 130f,
					isMajor ? 3 : 2,
					preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < spillTargets.Count; i++)
				{
					float falloff = Mathf.Max(0.48f, 1f - i * 0.22f);
					SpawnSpectacleArc(primary.GlobalPosition, spillTargets[i].GlobalPosition, accent, intensity: 0.94f + 0.08f * i);
					ApplySpectacleDamage(tower, spillTargets[i], splashDamage * falloff, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.ExploitWeakness:
			{
				float markDuration = (isMajor ? 4.2f : 2.6f) + 1.1f * p;
				int count = isMajor ? 4 : 2;
				foreach (var enemy in seededTargets.Where(IsEnemyUsable).Take(count))
				{
					bool wasMarked = enemy.IsMarked;
					Statuses.ApplyMarked(enemy, markDuration);
					if (wasMarked)
					{
						float execute = tower.BaseDamage * (isMajor ? 0.60f : 0.34f) * p;
						ApplySpectacleDamage(tower, enemy, execute, accent, heavyHit: isMajor);
					}
				}
				break;
			}
			case SpectacleDefinitions.FocusLens:
			{
				var focusTarget = primary
					?? GetSpectacleTargets(tower.GlobalPosition, tower.Range * 1.35f, 1, preferFront: true).FirstOrDefault();
				if (focusTarget != null)
				{
					float beam = tower.BaseDamage * (isMajor ? 1.05f : 0.60f) * p;
					SpawnSpectacleArc(tower.GlobalPosition, focusTarget.GlobalPosition, accent, intensity: 1.20f, mineChainStyle: isMajor);
					ApplySpectacleDamage(tower, focusTarget, beam, accent, heavyHit: true, triggerHitStopOnKill: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.ChillShot:
			{
				float slowDuration = (isMajor ? 4.6f : 3.1f) + 0.9f * p;
				float slowFactor = isMajor
					? Mathf.Clamp(0.76f - 0.09f * p, 0.30f, 0.86f)
					: Mathf.Clamp(0.84f - 0.07f * p, 0.45f, 0.92f);
				float chip = tower.BaseDamage * (isMajor ? 0.24f : 0.12f) * p;
				foreach (var enemy in seededTargets.Where(IsEnemyUsable).Take(isMajor ? 4 : 3))
				{
					Statuses.ApplySlow(enemy, slowDuration, slowFactor);
					ApplySpectacleDamage(tower, enemy, chip, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleDefinitions.Overreach:
			{
				float reach = Mathf.Max(isMajor ? 340f : 280f, tower.Range * (isMajor ? 1.45f : 1.25f));
				float pulse = tower.BaseDamage * (isMajor ? 0.68f : 0.34f) * p;
				var farTargets = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= reach)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.ThenByDescending(e => e.ProgressRatio)
					.Take(isMajor ? 3 : 2)
					.ToList();
				for (int i = 0; i < farTargets.Count; i++)
				{
					float falloff = Mathf.Max(0.58f, 1f - i * 0.18f);
					ApplySpectacleDamage(tower, farTargets[i], pulse * falloff, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.HairTrigger:
			{
				ReduceTowerCooldown(tower, (isMajor ? 0.30f : 0.18f) * Mathf.Clamp(p, 0.6f, 2.0f));
				if (primary != null && tower.Cooldown <= Mathf.Max(0.08f, tower.AttackInterval * 0.12f))
				{
					float burst = tower.BaseDamage * (isMajor ? 0.44f : 0.24f) * p;
					ApplySpectacleDamage(tower, primary, burst, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleDefinitions.SplitShot:
			{
				Vector2 center = primary?.GlobalPosition ?? tower.GlobalPosition;
				float splitRange = Balance.SplitShotRange * (isMajor ? 1.20f : 1.00f);
				float splitDamage = tower.BaseDamage * (isMajor ? 0.45f : 0.24f) * p;
				var splitTargets = GetSpectacleTargets(
					center,
					splitRange,
					isMajor ? 4 : 2,
					preferFront: false)
					.Where(e => primary == null || !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < splitTargets.Count; i++)
				{
					float falloff = Mathf.Max(0.62f, 1f - i * 0.14f);
					SpawnSpectacleArc(center, splitTargets[i].GlobalPosition, accent, intensity: 0.88f + 0.06f * i);
					ApplySpectacleDamage(tower, splitTargets[i], splitDamage * falloff, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleDefinitions.FeedbackLoop:
			{
				ReduceTowerCooldown(tower, (isMajor ? 0.42f : 0.24f) * Mathf.Clamp(p, 0.55f, 2.2f));
				if (primary != null && tower.Cooldown <= Mathf.Max(0.05f, tower.AttackInterval * 0.08f))
				{
					float reboot = tower.BaseDamage * (isMajor ? 0.52f : 0.28f) * p;
					ApplySpectacleDamage(tower, primary, reboot, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.ChainReaction:
			{
				if (primary == null)
					break;
				int bounces = (isMajor ? 4 : 2) + Mathf.FloorToInt(Mathf.Clamp(p - 1f, 0f, 1.8f));
				float chainDamage = tower.BaseDamage * (isMajor ? 0.56f : 0.30f) * p;
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: bounces,
					startDamage: chainDamage,
					decay: 0.72f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.05f),
					accent,
					heavy: isMajor);
				break;
			}
		}
	}

	private static string BuildModPairKey(string modA, string modB)
		=> string.CompareOrdinal(modA, modB) <= 0 ? $"{modA}|{modB}" : $"{modB}|{modA}";

	private void ApplyTriadAugmentSpectacleEffect(
		SpectacleTriggerInfo info,
		List<EnemyInstance> seededTargets,
		bool isMajor,
		float effectScale,
		Color accent)
	{
		if (string.IsNullOrEmpty(info.Signature.TertiaryModId))
			return;

		var augment = SpectacleDefinitions.GetTriadAugment(info.Signature.TertiaryModId);
		float strength = info.Signature.AugmentStrength
			* augment.Coefficient
			* (isMajor ? 1f : 0.68f)
			* Mathf.Max(0.15f, effectScale);
		float aug = Mathf.Clamp(strength, 0f, 0.95f);
		if (aug <= 0.001f)
			return;

		var tower = info.Tower;
		EnemyInstance? primary = seededTargets.FirstOrDefault(IsEnemyUsable);
		float durationBase = augment.DurationSec > 0f ? augment.DurationSec : (isMajor ? 1.8f : 1.2f);

		switch (augment.Kind)
		{
			case SpectacleAugmentKind.RampCap:
			{
				ReduceTowerCooldown(tower, 0.08f + 0.34f * aug);
				if (primary != null)
				{
					float dmg = tower.BaseDamage * (0.20f + 0.80f * aug);
					ApplySpectacleDamage(tower, primary, dmg, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleAugmentKind.SpillTransfer:
			{
				if (primary == null) break;
				float splash = tower.BaseDamage * (0.18f + 0.65f * aug);
				float radius = 120f + 70f * aug;
				var neighbors = GetSpectacleTargets(primary.GlobalPosition, radius, isMajor ? 3 : 2, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < neighbors.Count; i++)
				{
					float falloff = Mathf.Max(0.55f, 1f - i * 0.18f);
					ApplySpectacleDamage(tower, neighbors[i], splash * falloff, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleAugmentKind.MarkedVulnerability:
			{
				float markDuration = durationBase + 1.2f + 1.8f * aug;
				foreach (var enemy in GetSpectacleTargets(tower.GlobalPosition, tower.Range * 1.15f, isMajor ? 4 : 3, preferFront: true))
				{
					bool marked = enemy.IsMarked;
					Statuses.ApplyMarked(enemy, markDuration);
					if (marked)
					{
						float vuln = tower.BaseDamage * (0.20f + 0.70f * aug);
						ApplySpectacleDamage(tower, enemy, vuln, accent, heavyHit: false);
					}
				}
				break;
			}
			case SpectacleAugmentKind.BeamBurst:
			{
				var beamTarget = primary
					?? GetSpectacleTargets(tower.GlobalPosition, tower.Range * 1.45f, 1, preferFront: true).FirstOrDefault();
				if (beamTarget != null)
				{
					float beam = tower.BaseDamage * (0.42f + 1.35f * aug);
					SpawnSpectacleArc(tower.GlobalPosition, beamTarget.GlobalPosition, accent, intensity: 1.26f, mineChainStyle: true);
					ApplySpectacleDamage(tower, beamTarget, beam, accent, heavyHit: true, triggerHitStopOnKill: isMajor);
				}
				break;
			}
			case SpectacleAugmentKind.SlowIntensity:
			{
				float slowDuration = durationBase + 1.0f + 1.8f * aug;
				float slowFactor = Mathf.Clamp(0.82f - 0.62f * aug, 0.22f, 0.80f);
				foreach (var enemy in GetSpectacleTargets(tower.GlobalPosition, tower.Range * 1.10f, isMajor ? 4 : 3, preferFront: true))
					Statuses.ApplySlow(enemy, slowDuration, slowFactor);
				break;
			}
			case SpectacleAugmentKind.RangePulse:
			{
				float pulse = tower.BaseDamage * (0.24f + 0.78f * aug);
				float reach = Mathf.Max(320f, tower.Range * 1.48f);
				var farTargets = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= reach)
					.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
					.ThenByDescending(e => e.ProgressRatio)
					.Take(isMajor ? 3 : 2)
					.ToList();
				for (int i = 0; i < farTargets.Count; i++)
				{
					float falloff = Mathf.Max(0.60f, 1f - i * 0.16f);
					ApplySpectacleDamage(tower, farTargets[i], pulse * falloff, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleAugmentKind.AttackSpeed:
			{
				ReduceTowerCooldown(tower, 0.12f + 0.42f * aug);
				break;
			}
			case SpectacleAugmentKind.SplitVolley:
			{
				Vector2 center = primary?.GlobalPosition ?? tower.GlobalPosition;
				int extraCount = 2 + Mathf.FloorToInt(aug * 4f);
				float volley = tower.BaseDamage * (0.18f + 0.52f * aug);
				var extras = GetSpectacleTargets(center, Balance.SplitShotRange * 1.28f, extraCount, preferFront: false)
					.Where(e => primary == null || !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < extras.Count; i++)
				{
					float falloff = Mathf.Max(0.64f, 1f - i * 0.12f);
					SpawnSpectacleArc(center, extras[i].GlobalPosition, accent, intensity: 0.90f + i * 0.05f);
					ApplySpectacleDamage(tower, extras[i], volley * falloff, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleAugmentKind.CooldownRefund:
			{
				// Matches the spec behavior: refund X% of current cooldown where X = augmentStrength.
				ReduceTowerCooldown(tower, aug);
				break;
			}
			case SpectacleAugmentKind.ChainBounces:
			{
				if (primary == null) break;
				int bounces = 1 + Mathf.FloorToInt(aug * 4f);
				float chain = tower.BaseDamage * (0.20f + 0.70f * aug);
				ApplySpectacleChain(
					tower,
					primary,
					maxBounces: bounces,
					startDamage: chain,
					decay: 0.74f,
					linkRange: Mathf.Max(220f, tower.ChainRange * 1.08f),
					accent,
					heavy: isMajor);
				break;
			}
		}
	}

	private void ApplySpectacleChain(
		ITowerView tower,
		EnemyInstance start,
		int maxBounces,
		float startDamage,
		float decay,
		float linkRange,
		Color color,
		bool heavy)
	{
		if (!IsEnemyUsable(start) || maxBounces <= 0 || startDamage <= 0.01f)
			return;

		var hit = new HashSet<EnemyInstance> { start };
		var current = start;
		float damage = startDamage;
		int resolvedDepth = 0;

		for (int bounce = 0; bounce < maxBounces; bounce++)
		{
			EnemyInstance? next = _runState.EnemiesAlive
				.Where(IsEnemyUsable)
				.Where(e => !hit.Contains(e))
				.Where(e => current.GlobalPosition.DistanceTo(e.GlobalPosition) <= linkRange)
				.OrderBy(e => current.GlobalPosition.DistanceTo(e.GlobalPosition))
				.ThenByDescending(e => e.ProgressRatio)
				.FirstOrDefault();
			if (next == null)
				break;

			SpawnSpectacleArc(
				current.GlobalPosition,
				next.GlobalPosition,
				color,
				intensity: 1.0f + bounce * 0.08f,
				mineChainStyle: heavy);
			ApplySpectacleDamage(tower, next, damage, color, heavyHit: heavy);
			hit.Add(next);
			current = next;
			resolvedDepth = bounce + 1;
			damage *= Mathf.Clamp(decay, 0.45f, 0.95f);
			if (damage <= 0.5f)
				break;
		}

		_runState?.TrackSpectacleChainDepth(resolvedDepth);
	}

	private static bool IsEnemyUsable(EnemyInstance? enemy)
		=> enemy != null && GodotObject.IsInstanceValid(enemy) && enemy.Hp > 0f;

	private List<EnemyInstance> GetSpectacleTargets(Vector2 origin, float maxDistance, int maxTargets, bool preferFront)
	{
		if (_runState == null || maxTargets <= 0 || maxDistance <= 0f)
			return new List<EnemyInstance>();

		var query = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.Where(e => origin.DistanceTo(e.GlobalPosition) <= maxDistance);

		query = preferFront
			? query.OrderByDescending(e => e.ProgressRatio).ThenBy(e => origin.DistanceTo(e.GlobalPosition))
			: query.OrderBy(e => origin.DistanceTo(e.GlobalPosition)).ThenByDescending(e => e.ProgressRatio);

		return query.Take(maxTargets).ToList();
	}

	private float ApplySpectacleDamage(
		ITowerView tower,
		EnemyInstance enemy,
		float damage,
		Color color,
		bool heavyHit,
		bool triggerHitStopOnKill = false,
		bool allowOverkillBloom = true,
		bool allowMarkedPop = true,
		SpectacleDamageSource source = SpectacleDamageSource.SurgeCore)
	{
		float tunedDamage = damage;
		if (source == SpectacleDamageSource.ExplosionFollowUp || source == SpectacleDamageSource.Residue)
			tunedDamage *= Mathf.Max(0f, SpectacleTuning.Current.ExplosionFollowUpDamageMultiplier);
		if (source == SpectacleDamageSource.Residue)
			tunedDamage *= Mathf.Max(0f, SpectacleTuning.Current.ResidueDamageMultiplier);

		if (!IsEnemyUsable(enemy) || tunedDamage <= 0.05f)
			return 0f;

		bool wasMarked = enemy.IsMarked;
		float hpBefore = enemy.Hp;
		float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, tunedDamage);
		if (dealt <= 0.01f)
			return 0f;

		bool isKill = enemy.Hp <= 0f;
		float overflow = isKill ? Mathf.Max(0f, tunedDamage - hpBefore) : 0f;
		OverkillBloomProfile bloomProfile = SpectacleExplosionCore.BuildOverkillBloomProfile(overflow);
		string eventType = source switch
		{
			SpectacleDamageSource.Residue => "spectacle_damage_residue",
			SpectacleDamageSource.ExplosionFollowUp => "spectacle_damage_followup",
			_ => "spectacle_damage_core",
		};
		AppendBotTraceEvent(
			eventType: eventType,
			enemy: enemy,
			hpBefore: hpBefore,
			hpAfter: enemy.Hp,
			stageId: source.ToString().ToLowerInvariant(),
			residueSpawned: source == SpectacleDamageSource.Residue);
		TrackSpectacleDamage(tower, dealt, isKill, source, enemy.ProgressRatio);
		SpawnSpectacleDamageNumber(enemy.GlobalPosition, Mathf.Max(1f, dealt), isKill, color, tower.TowerId);
		SpawnSpectacleImpactSparks(enemy.GlobalPosition, color, heavy: heavyHit);
		if (_botRunner == null && !isKill && GodotObject.IsInstanceValid(enemy))
			enemy.FlashHit();
		if (isKill && triggerHitStopOnKill)
		{
			ExplosionHitStopProfile killHitStop = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
				majorExplosion: heavyHit,
				globalSurge: false,
				surgePower: heavyHit ? 1.18f : 0.70f);
			if (killHitStop.ShouldApply)
				TriggerHitStop(realDuration: killHitStop.DurationSeconds, slowScale: killHitStop.SlowScale);
		}
		if (isKill && allowMarkedPop && wasMarked && _runState != null)
			NotifyMarkedEnemyPop(tower, enemy, _runState.EnemiesAlive);
		if (isKill && allowOverkillBloom && bloomProfile.ShouldTrigger)
			SpawnOverkillBloom(tower, enemy.GlobalPosition, bloomProfile, color, heavyHit);
		return dealt;
	}

	private void SpawnOverkillBloom(
		ITowerView tower,
		Vector2 origin,
		OverkillBloomProfile profile,
		Color accent,
		bool heavySourceHit)
	{
		if (_runState == null || tower == null || !profile.ShouldTrigger)
			return;
		_runState.TrackOverkillBloom();
		AppendBotTraceEvent(
			eventType: "overkill_bloom_triggered",
			stageId: "stage_one",
			comboSkin: "default");

		if (_botRunner == null)
		{
			PrimeExplosionCompression(
				origin,
				profile.VisualRadius * 0.82f,
				accent,
				maxTargets: Mathf.Clamp(3 + Mathf.FloorToInt(profile.OverflowVisualT * 4f), 3, 7));
			SpawnSpectacleBurstFx(
				origin,
				accent,
				major: true,
				power: profile.BloomPower,
				stageTwoKick: profile.StageTwoKick);
			FlashSpectacleScreen(accent, peakAlpha: 0.09f + profile.OverflowVisualT * 0.06f, rampSec: 0.03f, fadeSec: 0.16f);
			if (profile.BloomDamage >= 30f && TryCombatCallout("overkill_bloom", 5.2f))
				SpawnCombatCallout("OVERKILL BLOOM", origin, accent, durationScale: 1.35f);
		}

		var targets = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.Where(e => origin.DistanceTo(e.GlobalPosition) <= profile.VisualRadius)
			.OrderBy(e => origin.DistanceTo(e.GlobalPosition))
			.ThenByDescending(e => e.ProgressRatio)
			.Take(profile.MaxTargets)
			.ToList();

		for (int i = 0; i < targets.Count; i++)
		{
			var target = targets[i];
			float distance = Mathf.Max(1f, origin.DistanceTo(target.GlobalPosition));
			float distT = Mathf.Clamp(distance / profile.VisualRadius, 0f, 1f);
			float falloff = Mathf.Lerp(1f, 0.35f, distT);
			float damage = profile.BloomDamage * falloff;
			if (damage <= 0.05f)
				continue;

			if (_botRunner == null)
			{
				SpawnSpectacleArc(
					origin,
					target.GlobalPosition,
					accent,
					intensity: 0.88f + 0.08f * i,
					mineChainStyle: heavySourceHit);
			}

			ApplySpectacleDamage(
				tower,
				target,
				damage,
				accent,
				heavyHit: false,
				triggerHitStopOnKill: false,
				allowOverkillBloom: false,
				source: SpectacleDamageSource.ExplosionFollowUp);
		}
	}

	private void PrimeExplosionCompression(Vector2 origin, float radius, Color accent, int maxTargets)
	{
		if (_botRunner != null || _runState == null || radius <= 0f || maxTargets <= 0)
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		var compressed = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.Where(e => origin.DistanceTo(e.GlobalPosition) <= radius)
			.OrderBy(e => origin.DistanceTo(e.GlobalPosition))
			.Take(maxTargets)
			.ToList();

		for (int i = 0; i < compressed.Count; i++)
		{
			var enemy = compressed[i];
			if (!GodotObject.IsInstanceValid(enemy))
				continue;
			enemy.FlashHit();
			if (reducedMotion)
				continue;

			SpawnSpectacleArc(
				enemy.GlobalPosition,
				origin,
				accent,
				intensity: 0.74f + 0.05f * i,
				mineChainStyle: false);
		}
	}

	private void TrackSpectacleDamage(ITowerView tower, float damage, bool isKill, SpectacleDamageSource source, float killDepth = -1f)
	{
		if (_runState == null)
			return;

		int dealtInt = Mathf.Max(0, (int)damage);
		if (dealtInt <= 0)
			return;

		int slotIndex = FindTowerSlotIndex(tower);
		_runState.TrackSpectacleDamage(slotIndex, dealtInt, isKill, source, killDepth);
	}

	private int FindTowerSlotIndex(ITowerView tower)
	{
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			if (ReferenceEquals(_runState.Slots[i].Tower, tower))
				return i;
		}
		return -1;
	}

	private void SpawnSpectacleDamageNumber(Vector2 worldPos, float damage, bool isKill, Color color, string sourceTowerId)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		var num = new DamageNumber();
		_worldNode.AddChild(num);
		num.GlobalPosition = worldPos + new Vector2(0f, -14f);
		num.Initialize(damage, color, isKill, sourceTowerId);
	}

	private void SpawnSpectacleImpactSparks(Vector2 worldPos, Color color, bool heavy)
	{
		if (_botRunner != null || SettingsManager.Instance?.ReducedMotion == true || !GodotObject.IsInstanceValid(_worldNode))
			return;
		var sparks = new ImpactSparkBurst();
		_worldNode.AddChild(sparks);
		sparks.GlobalPosition = worldPos;
		sparks.Initialize(color, heavy);
	}

	private void SpawnSpectacleArc(Vector2 from, Vector2 to, Color color, float intensity = 1f, bool mineChainStyle = false)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		var arc = new ChainArc();
		_worldNode.AddChild(arc);
		arc.GlobalPosition = Vector2.Zero;
		arc.Initialize(from, to, color, intensity, mineChainStyle);
	}

	private static void ReduceTowerCooldown(ITowerView tower, float refundFrac)
	{
		if (tower == null || refundFrac <= 0f || tower.Cooldown <= 0f)
			return;
		float clamped = Mathf.Clamp(refundFrac, 0f, 0.92f);
		tower.Cooldown = Mathf.Max(0f, tower.Cooldown * (1f - clamped));
	}

	private bool IsMobileSpectacleLite()
		=> _isMobilePlatform;

	private static Color ResolveSpectacleColor(string modId)
	{
		if (string.IsNullOrEmpty(modId))
			return new Color(0.80f, 0.92f, 1.00f);
		return ModifierVisuals.GetAccent(modId);
	}

	private static ComboExplosionSkin ResolveComboExplosionSkin(SpectacleSignature signature)
	{
		if (signature.Mode == SpectacleMode.Single)
			return ComboExplosionSkin.Default;
		return SpectacleExplosionCore.ResolveComboExplosionSkin(signature.PrimaryModId, signature.SecondaryModId);
	}

	private static Color ResolveComboSkinAccent(Color accent, ComboExplosionSkin skin)
	{
		return skin switch
		{
			ComboExplosionSkin.ChillShatter => new Color(
				Mathf.Clamp(accent.R * 0.42f + 0.42f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.62f + 0.32f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.88f + 0.16f, 0f, 1f),
				1f),
			ComboExplosionSkin.ChainArc => new Color(
				Mathf.Clamp(accent.R * 0.78f + 0.20f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.86f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.54f + 0.32f, 0f, 1f),
				1f),
			ComboExplosionSkin.SplitShrapnel => new Color(
				Mathf.Clamp(accent.R * 0.82f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.54f + 0.24f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.46f + 0.18f, 0f, 1f),
				1f),
			ComboExplosionSkin.FocusImplosion => new Color(
				Mathf.Clamp(accent.R * 0.86f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.86f + 0.18f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.96f + 0.16f, 0f, 1f),
				1f),
			_ => accent,
		};
	}

	private static SpectacleConsequenceKind ResolveConsequenceKindFromSkin(ComboExplosionSkin skin)
	{
		return skin switch
		{
			ComboExplosionSkin.ChillShatter => SpectacleConsequenceKind.FrostSlow,
			ComboExplosionSkin.SplitShrapnel => SpectacleConsequenceKind.BurnPatch,
			ComboExplosionSkin.ChainArc => SpectacleConsequenceKind.Vulnerability,
			ComboExplosionSkin.FocusImplosion => SpectacleConsequenceKind.Vulnerability,
			_ => SpectacleConsequenceKind.None,
		};
	}

	private ITowerView? ResolveSpectacleSourceTower(ITowerView? preferred)
	{
		if (preferred is GodotObject preferredObj && !GodotObject.IsInstanceValid(preferredObj))
			preferred = null;
		if (preferred != null)
			return preferred;
		if (_runState == null)
			return null;

		return _runState.Slots
			.Select(s => s.Tower)
			.FirstOrDefault(t => t != null && (t is not GodotObject go || GodotObject.IsInstanceValid(go)));
	}

	private float ResolveGlobalSpectacleBaseDamage()
	{
		if (_runState == null)
			return 0f;

		float total = 0f;
		int count = 0;
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null)
				continue;
			if (tower is GodotObject go && !GodotObject.IsInstanceValid(go))
				continue;

			total += Mathf.Max(0f, tower.BaseDamage);
			count++;
		}

		if (count <= 0)
			return 0f;
		return total / count;
	}

	private void ApplyTargetedSpectacleConsequence(
		ITowerView? sourceTower,
		EnemyInstance target,
		Color color,
		float damageScale,
		bool heavyHit,
		SpectacleConsequenceKind rider = SpectacleConsequenceKind.None,
		float riderStrength = 1f,
		bool spawnResidue = false,
		float damageBaseOverride = -1f)
	{
		if (_runState == null || !IsEnemyUsable(target))
			return;

		ITowerView? resolvedSource = ResolveSpectacleSourceTower(sourceTower);
		float resolvedBaseDamage = resolvedSource?.BaseDamage ?? 0f;
		if (float.IsFinite(damageBaseOverride) && damageBaseOverride >= 0f)
			resolvedBaseDamage = damageBaseOverride;
		float strength = Mathf.Clamp(riderStrength, 0.40f, 1.80f);

		if (resolvedSource != null && damageScale > 0f && resolvedBaseDamage > 0f)
		{
			float scaledDamage = Mathf.Clamp(damageScale, 0.01f, 0.30f);
			float damage = resolvedBaseDamage * scaledDamage;
			ApplySpectacleDamage(
				resolvedSource,
				target,
				damage,
				color,
				heavyHit,
				triggerHitStopOnKill: false,
				allowOverkillBloom: false,
				allowMarkedPop: false,
				source: SpectacleDamageSource.ExplosionFollowUp);
		}

		switch (rider)
		{
			case SpectacleConsequenceKind.FrostSlow:
			{
				float slowDuration = Mathf.Clamp(0.24f + 0.28f * strength, 0.20f, 0.72f);
				float slowFactor = Mathf.Clamp(0.88f - 0.16f * strength, 0.60f, 0.90f);
				Statuses.ApplySlow(target, slowDuration, slowFactor);
				break;
			}
			case SpectacleConsequenceKind.Vulnerability:
			{
				float ampDuration = Mathf.Clamp(0.24f + 0.30f * strength, 0.20f, 0.72f);
				float ampMultiplier = Mathf.Clamp(0.03f + 0.06f * strength, 0.03f, 0.12f);
				Statuses.ApplyDamageAmp(target, ampDuration, ampMultiplier);
				break;
			}
			case SpectacleConsequenceKind.BurnPatch:
			{
				if (resolvedSource != null && resolvedBaseDamage > 0f)
				{
					float burnTick = resolvedBaseDamage * Mathf.Clamp(0.03f + 0.03f * strength, 0.03f, 0.09f);
					ApplySpectacleDamage(
						resolvedSource,
						target,
						burnTick,
						color,
						heavyHit: false,
						triggerHitStopOnKill: false,
						allowOverkillBloom: false,
						allowMarkedPop: false,
						source: SpectacleDamageSource.ExplosionFollowUp);
				}
				break;
			}
		}

		if (!spawnResidue)
			return;

		ExplosionResidueKind residueKind = rider switch
		{
			SpectacleConsequenceKind.FrostSlow => ExplosionResidueKind.FrostSlow,
			SpectacleConsequenceKind.Vulnerability => ExplosionResidueKind.VulnerabilityZone,
			SpectacleConsequenceKind.BurnPatch => ExplosionResidueKind.BurnPatch,
			_ => ExplosionResidueKind.None,
		};
		if (residueKind == ExplosionResidueKind.None)
			return;

		float duration = residueKind switch
		{
			ExplosionResidueKind.FrostSlow => SpectacleExplosionCore.ResidueFrostSlowDurationSeconds,
			ExplosionResidueKind.VulnerabilityZone => SpectacleExplosionCore.ResidueVulnerabilityDurationSeconds,
			ExplosionResidueKind.BurnPatch => SpectacleExplosionCore.ResidueBurnDurationSeconds,
			_ => 0f,
		};
		float radius = residueKind switch
		{
			ExplosionResidueKind.FrostSlow => SpectacleExplosionCore.ResidueFrostRadius,
			ExplosionResidueKind.VulnerabilityZone => SpectacleExplosionCore.ResidueVulnerabilityRadius,
			ExplosionResidueKind.BurnPatch => SpectacleExplosionCore.ResidueBurnRadius,
			_ => 0f,
		};

		ExplosionResidueProfile profile = new(
			ShouldSpawn: true,
			Kind: residueKind,
			DurationSeconds: duration,
			Radius: radius * Mathf.Lerp(0.82f, 1.04f, Mathf.Clamp(strength / 1.6f, 0f, 1f)),
			TickIntervalSeconds: SpectacleExplosionCore.ResidueTickIntervalSeconds,
			Potency: Mathf.Clamp(strength, 0.60f, 1.15f));
		TrySpawnExplosionResidue(profile, target.GlobalPosition, color, resolvedSource);
	}

	private void SpawnComboSkinGlyphFx(Vector2 worldPos, ComboExplosionSkin skin, Color accent, float power, bool major)
	{
		if (_botRunner != null || skin == ComboExplosionSkin.Default || !GodotObject.IsInstanceValid(_worldNode))
			return;
		if (IsMobileSpectacleLite() && !major)
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		var glyph = new SpectacleSkinGlyph();
		_worldNode.AddChild(glyph);
		glyph.GlobalPosition = worldPos;

		float powerT = Mathf.Clamp(power / 2.2f, 0f, 1f);
		float scale = Mathf.Clamp(
			(major ? 1.12f : 0.92f) * Mathf.Lerp(0.90f, 1.50f, powerT),
			0.58f,
			1.95f);
		float duration = reducedMotion ? 0.22f : (major ? 0.58f : 0.40f);
		glyph.Initialize(skin, accent, durationSec: duration, scale: scale);
	}

	private void SpawnComboExplosionSkinFx(ITowerView tower, SpectacleSignature signature, Color accent, ComboExplosionSkin skin)
	{
		if (_botRunner != null || _runState == null || tower == null || skin == ComboExplosionSkin.Default || !GodotObject.IsInstanceValid(_worldNode))
			return;

		Color skinAccent = ResolveComboSkinAccent(accent, skin);
		Vector2 origin = tower.GlobalPosition;
		float power = Mathf.Clamp(signature.SurgePower, 0.6f, 2.2f);
		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		ITowerView? sourceTower = ResolveSpectacleSourceTower(tower);
		float skinRiderStrength = power * 0.56f;
		SpawnComboSkinGlyphFx(origin, skin, skinAccent, power, major: true);

		switch (skin)
		{
			case ComboExplosionSkin.ChillShatter:
			{
				var ring = new GlobalSurgeRipple();
				_worldNode.AddChild(ring);
				ring.GlobalPosition = origin;
				ring.Initialize(
					skinAccent,
					endRadius: 210f + 26f * power,
					durationSec: reducedMotion ? 0.22f : 0.34f,
					ringWidth: 3.6f);
				var shards = GetSpectacleTargets(origin, 240f, reducedMotion ? 2 : 4, preferFront: false);
				for (int i = 0; i < shards.Count; i++)
				{
					SpawnSpectacleArc(origin, shards[i].GlobalPosition, skinAccent, intensity: 0.90f + i * 0.08f, mineChainStyle: false);
					if (!reducedMotion)
						SpawnComboSkinGlyphFx(shards[i].GlobalPosition, skin, skinAccent, power * 0.62f, major: false);
					ApplyTargetedSpectacleConsequence(
						sourceTower,
						shards[i],
						skinAccent,
						damageScale: (0.08f + 0.02f * power) * Mathf.Max(0.70f, 1f - i * 0.12f),
						heavyHit: false,
						rider: SpectacleConsequenceKind.FrostSlow,
						riderStrength: skinRiderStrength,
						spawnResidue: i % 2 == 0);
				}
				break;
			}
			case ComboExplosionSkin.ChainArc:
			{
				SpawnSpectacleLinks(
					origin,
					skinAccent,
					maxLinks: reducedMotion ? 3 : 6,
					maxDistance: 340f,
					majorStyle: true,
					sourceTower: sourceTower,
					consequenceDamageScale: 0.08f + 0.02f * power,
					rider: SpectacleConsequenceKind.Vulnerability,
					riderStrength: skinRiderStrength,
					spawnResidue: !reducedMotion);
				var chainTargets = GetSpectacleTargets(origin, 340f, reducedMotion ? 2 : 3, preferFront: true);
				for (int i = 0; i < chainTargets.Count; i++)
				{
					SpawnComboSkinGlyphFx(chainTargets[i].GlobalPosition, skin, skinAccent, power * (0.72f + i * 0.05f), major: false);
					ApplyTargetedSpectacleConsequence(
						sourceTower,
						chainTargets[i],
						skinAccent,
						damageScale: (0.06f + 0.015f * power) * Mathf.Max(0.70f, 1f - i * 0.12f),
						heavyHit: false,
						rider: SpectacleConsequenceKind.Vulnerability,
						riderStrength: skinRiderStrength * 0.95f,
						spawnResidue: false);
				}
				break;
			}
			case ComboExplosionSkin.SplitShrapnel:
			{
				var fan = GetSpectacleTargets(origin, 260f, reducedMotion ? 3 : 6, preferFront: false);
				for (int i = 0; i < fan.Count; i++)
				{
					SpawnSpectacleArc(origin, fan[i].GlobalPosition, skinAccent, intensity: 0.86f + i * 0.07f, mineChainStyle: false);
					SpawnComboSkinGlyphFx(fan[i].GlobalPosition, skin, skinAccent, power * 0.64f, major: false);
					if (!reducedMotion)
						SpawnSpectacleImpactSparks(fan[i].GlobalPosition, skinAccent, heavy: false);
					ApplyTargetedSpectacleConsequence(
						sourceTower,
						fan[i],
						skinAccent,
						damageScale: (0.10f + 0.03f * power) * Mathf.Max(0.66f, 1f - i * 0.10f),
						heavyHit: false,
						rider: SpectacleConsequenceKind.BurnPatch,
						riderStrength: skinRiderStrength,
						spawnResidue: i % 2 == 0);
				}
				break;
			}
			case ComboExplosionSkin.FocusImplosion:
			{
				PrimeExplosionCompression(origin, 118f + 22f * power, skinAccent, maxTargets: reducedMotion ? 2 : 4);
				void BeamPop()
				{
					if (CurrentPhase != GamePhase.Wave
						|| _botRunner != null
						|| !GodotObject.IsInstanceValid(this))
					{
						return;
					}

					SpawnSpectacleBurstFx(origin, skinAccent, major: false, power: 0.82f + 0.12f * power);
					var beamTarget = GetSpectacleTargets(origin, 280f, 1, preferFront: true).FirstOrDefault();
					if (beamTarget != null)
					{
						SpawnComboSkinGlyphFx(beamTarget.GlobalPosition, skin, skinAccent, power * 0.84f, major: false);
						SpawnSpectacleArc(origin, beamTarget.GlobalPosition, skinAccent, intensity: 1.22f, mineChainStyle: true);
						SpawnSpectacleImpactSparks(beamTarget.GlobalPosition, skinAccent, heavy: true);
						ApplyTargetedSpectacleConsequence(
							sourceTower,
							beamTarget,
							skinAccent,
							damageScale: 0.24f + 0.06f * power,
							heavyHit: true,
							rider: SpectacleConsequenceKind.Vulnerability,
							riderStrength: skinRiderStrength * 1.08f,
							spawnResidue: true);
					}
				}

				if (reducedMotion)
					BeamPop();
				else
					GetTree().CreateTimer(0.10f).Timeout += BeamPop;
				break;
			}
		}
	}

	private static bool IsEnemyStatusPrimed(EnemyInstance enemy)
		=> enemy.IsMarked || enemy.IsSlowed || enemy.DamageAmpRemaining > 0f;

	private void TriggerStatusDetonationChain(
		ITowerView? sourceTower,
		Vector2 origin,
		Color accent,
		ComboExplosionSkin skin,
		bool globalSurge,
		float surgePower,
		float damageBaseOverride = -1f)
	{
		if (_runState == null || CurrentPhase != GamePhase.Wave)
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		int maxTargets = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge, reducedMotion);
		if (maxTargets <= 0)
			return;

		var statusTargets = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.Where(IsEnemyStatusPrimed)
			.OrderBy(e => origin.DistanceTo(e.GlobalPosition))
			.ThenByDescending(e => e.ProgressRatio)
			.Take(maxTargets)
			.ToList();
		if (statusTargets.Count == 0)
			return;
		_runState.TrackStatusDetonation(statusTargets.Count);

		Color detonationColor = ResolveComboSkinAccent(accent, skin);
		float stagger = SpectacleExplosionCore.ResolveStatusDetonationStaggerSeconds(reducedMotion);

		ITowerView? damageSource = sourceTower;
		if (damageSource is GodotObject towerObj && !GodotObject.IsInstanceValid(towerObj))
			damageSource = null;
		if (damageSource == null)
		{
			damageSource = _runState.Slots
				.Select(s => s.Tower)
				.FirstOrDefault(t => t != null && (t is not GodotObject go || GodotObject.IsInstanceValid(go)));
		}
		float sourceBaseDamage = damageSource?.BaseDamage ?? 0f;
		if (float.IsFinite(damageBaseOverride) && damageBaseOverride >= 0f)
			sourceBaseDamage = damageBaseOverride;

		for (int i = 0; i < statusTargets.Count; i++)
		{
			var enemyRef = statusTargets[i];
			int index = i;
			bool renderDetonationFx = !IsMobileSpectacleLite() || index < 2;
			Vector2? previousAnchor = index > 0 ? statusTargets[index - 1].GlobalPosition : (Vector2?)null;
			float delay = reducedMotion ? index * (stagger * 0.7f) : index * stagger;

			void EmitImpact()
			{
				if (CurrentPhase != GamePhase.Wave
					|| !GodotObject.IsInstanceValid(this)
					|| !IsEnemyUsable(enemyRef))
				{
					return;
				}

				if (_botRunner == null && previousAnchor.HasValue && !reducedMotion)
				{
					SpawnSpectacleArc(previousAnchor.Value, enemyRef.GlobalPosition, detonationColor, intensity: 1.00f + index * 0.03f, mineChainStyle: true);
				}
				AppendBotTraceEvent(
					eventType: "status_detonation_hit",
					enemy: enemyRef,
					stageId: $"detonation_{index}",
					comboSkin: skin.ToString());

				if (_botRunner == null && renderDetonationFx)
				{
					SpawnSpectacleBurstFx(
						enemyRef.GlobalPosition,
						detonationColor,
						major: false,
						power: Mathf.Clamp(0.78f + 0.04f * index, 0.78f, 1.28f));
					SpawnComboSkinGlyphFx(
						enemyRef.GlobalPosition,
						skin,
						detonationColor,
						power: Mathf.Clamp(0.72f + 0.03f * index, 0.72f, 1.20f),
						major: false);
					SpawnSpectacleImpactSparks(enemyRef.GlobalPosition, detonationColor, heavy: false);
				}

				ExplosionResidueProfile residueProfile = SpectacleExplosionCore.ResolveResidueProfile(
					skin,
					globalSurge,
					surgePower,
					index);
				TrySpawnExplosionResidue(
					residueProfile,
					enemyRef.GlobalPosition,
					detonationColor,
					damageSource);

				if (damageSource != null && sourceBaseDamage > 0f)
				{
					float damage = sourceBaseDamage
						* (globalSurge ? 0.22f : 0.16f)
						* Mathf.Clamp(surgePower, 0.6f, 2.2f)
						* Mathf.Max(0.52f, 1f - index * 0.04f)
						* Mathf.Max(0f, SpectacleTuning.Current.StatusDetonationDamageMultiplier);
					ApplySpectacleDamage(
						damageSource,
						enemyRef,
						damage,
						detonationColor,
						heavyHit: false,
						triggerHitStopOnKill: false,
						allowOverkillBloom: false,
						allowMarkedPop: false,
						source: SpectacleDamageSource.ExplosionFollowUp);
				}
			}

			if (delay <= 0f)
				EmitImpact();
			else
				GetTree().CreateTimer(delay).Timeout += EmitImpact;
		}
	}

	private void SpawnSpectacleBurstFx(Vector2 worldPos, Color accent, bool major, float power = 1f, bool stageTwoKick = false)
	{
		bool mobileLite = IsMobileSpectacleLite();
		bool emitSecondStage = SpectacleExplosionCore.ShouldEmitSecondStage(major, power)
			&& (!mobileLite || major);
		_runState?.TrackSpectacleExplosionBurst();
		AppendBotTraceEvent(
			eventType: "spectacle_burst",
			stageId: "stage_one");
		if (emitSecondStage)
		{
			AppendBotTraceEvent(
				eventType: "spectacle_burst",
				stageId: "stage_two",
				timestampOverride: (_runState?.TotalPlayTime ?? 0f) + SpectacleExplosionCore.TwoStageBlastDelaySeconds);
		}
		if (_botRunner != null)
		{
			_botStepExplosionBursts += 1;
			return;
		}
		if (!GodotObject.IsInstanceValid(_worldNode))
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		float intensity = Mathf.Clamp((major ? 1.26f : 1.02f) * Mathf.Max(0.60f, power), 0.85f, 2.60f);

		// Stage 1: tiny and sharp impact pop.
		var stageOne = new RiftMineBurst();
		_worldNode.AddChild(stageOne);
		stageOne.GlobalPosition = worldPos;
		float stageOneIntensity = intensity * (major ? 0.62f : 0.74f);
		stageOne.Initialize(accent, chainPop: false, intensity: stageOneIntensity);

		if (!emitSecondStage)
			return;

		// Stage 2: delayed wide detonation ring.
		void EmitStageTwo()
		{
			if (_botRunner != null || !GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_worldNode))
				return;

			var stageTwo = new RiftMineBurst();
			_worldNode.AddChild(stageTwo);
			stageTwo.GlobalPosition = worldPos;
			stageTwo.Modulate = new Color(1f, 1f, 1f, major ? 0.76f : 0.64f);
			Color stageTwoAccent = new Color(
				Mathf.Clamp(accent.R * 0.78f + 0.22f, 0f, 1f),
				Mathf.Clamp(accent.G * 0.78f + 0.22f, 0f, 1f),
				Mathf.Clamp(accent.B * 0.78f + 0.22f, 0f, 1f),
				1f);
			float stageTwoIntensity = intensity * (major ? 1.15f : 1.03f);
			stageTwo.Initialize(stageTwoAccent, chainPop: true, intensity: stageTwoIntensity);

			if (stageTwoKick && !reducedMotion && !mobileLite)
			{
				ShakeWorldMicro();
				SoundManager.Instance?.Play("mine_chain_pop", pitchScale: major ? 0.92f : 0.98f);
			}
		}

		if (reducedMotion)
		{
			EmitStageTwo();
		}
		else
		{
			GetTree().CreateTimer(SpectacleExplosionCore.TwoStageBlastDelaySeconds).Timeout += EmitStageTwo;
		}
	}

	private void SpawnSpectacleLinks(
		Vector2 origin,
		Color accent,
		int maxLinks,
		float maxDistance,
		bool majorStyle,
		ITowerView? sourceTower = null,
		float consequenceDamageScale = 0f,
		SpectacleConsequenceKind rider = SpectacleConsequenceKind.None,
		float riderStrength = 1f,
		bool spawnResidue = false,
		float damageBaseOverride = -1f)
	{
		if (_runState == null || maxLinks <= 0)
			return;

		bool canVisualize = _botRunner == null && GodotObject.IsInstanceValid(_worldNode);
		int mobileVisualCap = IsMobileSpectacleLite() ? 2 : int.MaxValue;
		var links = _runState.EnemiesAlive
			.Where(e =>
				GodotObject.IsInstanceValid(e)
				&& e.Hp > 0f
				&& origin.DistanceTo(e.GlobalPosition) <= maxDistance)
			.OrderByDescending(e => e.ProgressRatio)
			.ThenBy(e => origin.DistanceTo(e.GlobalPosition))
			.Take(maxLinks)
			.ToList();

		for (int i = 0; i < links.Count; i++)
		{
			var enemy = links[i];
			float intensity = (majorStyle ? 1.34f : 1.06f) + i * 0.08f;
			if (canVisualize && i < mobileVisualCap)
			{
				var arc = new ChainArc();
				_worldNode.AddChild(arc);
				arc.GlobalPosition = Vector2.Zero;
				arc.Initialize(origin, enemy.GlobalPosition, accent, intensity, mineChainStyle: majorStyle);

				if (majorStyle)
				{
					var sparks = new ImpactSparkBurst();
					_worldNode.AddChild(sparks);
					sparks.GlobalPosition = enemy.GlobalPosition;
					sparks.Initialize(accent, heavy: true);
				}
			}

			if (consequenceDamageScale > 0f || rider != SpectacleConsequenceKind.None || spawnResidue)
			{
				float falloff = Mathf.Max(0.58f, 1f - i * 0.12f);
				ApplyTargetedSpectacleConsequence(
					sourceTower,
					enemy,
					accent,
					damageScale: consequenceDamageScale * falloff,
					heavyHit: majorStyle,
					rider,
					riderStrength: riderStrength * (0.96f - i * 0.08f),
					spawnResidue: spawnResidue && (i % 2 == 0),
					damageBaseOverride: damageBaseOverride);
			}
		}
	}

	private void SpawnGlobalSurgeRipples(Vector2 origin, Color[] colors, int contributors, float lingerMultiplier = 1f)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		if (colors == null || colors.Length == 0)
			colors = new[] { new Color(1.00f, 0.90f, 0.56f) };

		float linger = Mathf.Clamp(lingerMultiplier, 1f, 12f);
		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		Vector2 topLeft = ScreenToWorld(Vector2.Zero);
		Vector2 bottomRight = ScreenToWorld(viewportSize);
		float diagonal = topLeft.DistanceTo(bottomRight);
		float endRadius = Mathf.Max(340f, diagonal * 0.62f);

		float contributorT = Mathf.Clamp((contributors - 1f) / 5f, 0f, 1f);
		// Clamp ripple count to number of distinct colors (max 3) — one ripple per mod slot
		int maxRipples = reducedMotion ? 1 : Mathf.Min(3, colors.Length > 1 ? colors.Length : 3);
		if (IsMobileSpectacleLite())
			maxRipples = Mathf.Min(maxRipples, 2);
		float baseDuration = Mathf.Lerp(0.62f, 0.86f, contributorT) * linger;

		for (int i = 0; i < maxRipples; i++)
		{
			int rippleIndex = i;
			Color rippleColor = colors[rippleIndex % colors.Length];
			float delay = reducedMotion ? 0f : rippleIndex * 0.14f;

			void Emit()
			{
				if (_botRunner != null || !GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_worldNode))
					return;

				var ripple = new GlobalSurgeRipple();
				_worldNode.AddChild(ripple);
				ripple.GlobalPosition = origin;
				ripple.Initialize(
					rippleColor,
					endRadius * (0.90f + rippleIndex * 0.09f),
					durationSec: baseDuration + rippleIndex * 0.08f,
					ringWidth: 4.8f + rippleIndex * 1.0f);
			}

			if (delay <= 0f)
				Emit();
			else
				GetTree().CreateTimer(delay).Timeout += Emit;
		}
	}

	private void SpawnGlobalSurgeAffectFx(
		Vector2 origin,
		Color accent,
		int contributors,
		ITowerView? sourceTower = null,
		float damageBaseOverride = -1f)
	{
		if (_botRunner != null || _runState == null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		int maxLinks = reducedMotion
			? Mathf.Clamp(8 + contributors * 2, 8, 20)
			: Mathf.Clamp(10 + contributors * 4, 12, 36);
		var targets = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.OrderBy(e => origin.DistanceTo(e.GlobalPosition))
			.ThenByDescending(e => e.ProgressRatio)
			.Take(maxLinks)
			.ToList();
		if (targets.Count == 0)
			return;

		float baseIntensity = 1.12f + 0.08f * Mathf.Clamp(contributors, 1, 6);
		ITowerView? resolvedSourceTower = ResolveSpectacleSourceTower(sourceTower);
		float sourceBaseDamage = resolvedSourceTower?.BaseDamage ?? 0f;
		if (float.IsFinite(damageBaseOverride) && damageBaseOverride >= 0f)
			sourceBaseDamage = damageBaseOverride;
		float impactDamageScale = Mathf.Lerp(0.08f, 0.14f, Mathf.Clamp((contributors - 1f) / 6f, 0f, 1f));
		float impactRiderStrength = 0.90f + 0.08f * Mathf.Clamp(contributors, 1, 6);
		int consequenceCount = reducedMotion
			? Mathf.Clamp(6 + contributors, 6, 12)
			: Mathf.Clamp(8 + contributors * 2, 10, 18);
		for (int i = 0; i < targets.Count; i++)
		{
			var enemyRef = targets[i];
			int index = i;
			bool renderImpactFx = !IsMobileSpectacleLite() || index < 6;
			float distance = origin.DistanceTo(enemyRef.GlobalPosition);
			float intensity = baseIntensity + index * 0.02f;
			GlobalSurgeWaveTiming timing = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(distance, contributors, reducedMotion);
			float impactDelay = timing.ImpactDelay;
			float preFlashDelay = timing.PreFlashDelay;

			void PreFlash()
			{
				if (CurrentPhase != GamePhase.Wave
					|| _botRunner != null
					|| !GodotObject.IsInstanceValid(this)
					|| !GodotObject.IsInstanceValid(enemyRef))
				{
					return;
				}

				enemyRef.FlashHit();
			}

			void EmitImpact()
			{
				if (CurrentPhase != GamePhase.Wave
					|| _botRunner != null
					|| !GodotObject.IsInstanceValid(this)
					|| !GodotObject.IsInstanceValid(enemyRef))
				{
					return;
				}

				if (renderImpactFx)
				{
					SpawnSpectacleArc(origin, enemyRef.GlobalPosition, accent, intensity, mineChainStyle: true);
					SpawnSpectacleImpactSparks(enemyRef.GlobalPosition, accent, heavy: true);
					if (!reducedMotion)
					{
						float popPower = 0.90f + 0.05f * Mathf.Clamp(contributors, 1, 7);
						SpawnSpectacleBurstFx(enemyRef.GlobalPosition, accent, major: false, power: popPower);
					}
				}

				bool primaryConsequence = index < consequenceCount;
				float damageFalloff = primaryConsequence
					? Mathf.Max(0.55f, 1f - index * 0.03f)
					: Mathf.Max(0.16f, 0.42f - (index - consequenceCount) * 0.02f);
				float riderFalloff = primaryConsequence
					? Mathf.Max(0.60f, 0.95f - index * 0.015f)
					: 0.38f;
				ApplyTargetedSpectacleConsequence(
					resolvedSourceTower,
					enemyRef,
					accent,
					damageScale: impactDamageScale * damageFalloff,
					heavyHit: false,
					rider: SpectacleConsequenceKind.Vulnerability,
					riderStrength: impactRiderStrength * riderFalloff,
					spawnResidue: primaryConsequence && !reducedMotion && (index % 4 == 0),
					damageBaseOverride: sourceBaseDamage);
			}

			if (preFlashDelay <= 0f)
				PreFlash();
			else
				GetTree().CreateTimer(preFlashDelay).Timeout += PreFlash;

			if (impactDelay <= 0f)
			{
				EmitImpact();
			}
			else
			{
				GetTree().CreateTimer(impactDelay).Timeout += EmitImpact;
			}
		}
	}

	private void SpawnSpectacleTowerVolleyFx(ITowerView tower, Color accent, bool major, float power)
	{
		if (_botRunner != null || _runState == null || tower == null)
			return;
		bool mobileLite = IsMobileSpectacleLite();

		if (!mobileLite)
			SpawnSpectacleImpactSparks(tower.GlobalPosition, accent, heavy: major);
		float volleyRange = Mathf.Max(major ? 320f : 250f, tower.Range * (major ? 1.30f : 1.05f));
		int volleyCount = major ? 4 : 2;
		var targets = GetSpectacleTargets(tower.GlobalPosition, volleyRange, volleyCount, preferFront: true);
		if (targets.Count == 0)
			return;

		float intensityBase = (major ? 1.52f : 1.22f) + 0.24f * Mathf.Clamp(power, 0.5f, 2.5f);
		float volleyDamageScale = (major ? 0.14f : 0.08f) * Mathf.Clamp(power, 0.6f, 2.2f);
		for (int i = 0; i < targets.Count; i++)
		{
			bool renderVolleyFx = !mobileLite || i == 0;
			float intensity = intensityBase + i * 0.12f;
			if (renderVolleyFx)
				SpawnSpectacleArc(tower.GlobalPosition, targets[i].GlobalPosition, accent, intensity, mineChainStyle: major);
			if (major && renderVolleyFx)
				SpawnSpectacleImpactSparks(targets[i].GlobalPosition, accent, heavy: true);

			float falloff = Mathf.Max(0.60f, 1f - i * 0.14f);
			ApplyTargetedSpectacleConsequence(
				tower,
				targets[i],
				accent,
				damageScale: volleyDamageScale * falloff,
				heavyHit: major,
				rider: major ? SpectacleConsequenceKind.Vulnerability : SpectacleConsequenceKind.None,
				riderStrength: major ? 0.86f : 1f,
				spawnResidue: major && i == 0);
		}
	}

	private void QueueSpectacleEcho(
		Vector2 origin,
		Color accent,
		bool major,
		float power,
		float maxDistance,
		ITowerView? sourceTower = null,
		SpectacleConsequenceKind rider = SpectacleConsequenceKind.None,
		bool spawnResidue = false,
		float damageBaseOverride = -1f)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave || !GodotObject.IsInstanceValid(_worldNode))
			return;

		float delay = major ? 0.24f : 0.16f;
		GetTree().CreateTimer(delay).Timeout += () =>
		{
			if (_botRunner != null || CurrentPhase != GamePhase.Wave || !GodotObject.IsInstanceValid(this))
				return;

			if (!IsMobileSpectacleLite())
				SpawnSpectacleBurstFx(origin, accent, major, power * (major ? 0.72f : 0.62f));
			SpawnSpectacleLinks(
				origin,
				accent,
				maxLinks: major ? 2 : 1,
				maxDistance: Mathf.Max(200f, maxDistance * 0.90f),
				majorStyle: major,
				sourceTower: sourceTower,
				consequenceDamageScale: (major ? 0.08f : 0.05f) * Mathf.Clamp(power, 0.6f, 2.0f),
				rider: rider,
				riderStrength: Mathf.Clamp(power, 0.6f, 2.2f) * 0.46f,
				spawnResidue: spawnResidue && major,
				damageBaseOverride: damageBaseOverride);
			var echoTargets = GetSpectacleTargets(
				origin,
				Mathf.Max(160f, maxDistance * 0.62f),
				major ? 2 : 1,
				preferFront: false);
			for (int i = 0; i < echoTargets.Count; i++)
			{
				float falloff = Mathf.Max(0.62f, 1f - i * 0.18f);
				ApplyTargetedSpectacleConsequence(
					sourceTower,
					echoTargets[i],
					accent,
					damageScale: (major ? 0.06f : 0.04f) * Mathf.Clamp(power, 0.6f, 2.0f) * falloff,
					heavyHit: false,
					rider: rider,
					riderStrength: Mathf.Clamp(power, 0.6f, 2.2f) * 0.42f * falloff,
					spawnResidue: spawnResidue && i == 0 && !major,
					damageBaseOverride: damageBaseOverride);
			}
			FlashSpectacleScreen(
				accent,
				peakAlpha: major ? 0.055f : 0.030f,
				rampSec: 0.03f,
				fadeSec: major ? 0.16f : 0.12f);
		};
	}

	private void FlashSpectacleScreen(Color accent, float peakAlpha, float rampSec, float fadeSec)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_spectaclePulse))
			return;

		_spectaclePulse.Visible = true;
		_spectaclePulse.Color = new Color(accent.R, accent.G, accent.B, 0f);
		var tw = _spectaclePulse.CreateTween();
		tw.TweenProperty(_spectaclePulse, "color:a", Mathf.Clamp(peakAlpha, 0f, 0.32f), Mathf.Max(0.02f, rampSec));
		tw.TweenProperty(_spectaclePulse, "color:a", 0f, Mathf.Max(0.05f, fadeSec))
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tw.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(_spectaclePulse))
				_spectaclePulse.Visible = false;
		}));
	}

	private void FlashSpectacleScreenLinger(Color color, float alpha, float holdSec, float fadeSec)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_lingerTint))
			return;
		_lingerTint.Visible = true;
		_lingerTint.Color = new Color(color.R, color.G, color.B, 0f);
		var tw = _lingerTint.CreateTween();
		tw.TweenProperty(_lingerTint, "color:a", Mathf.Clamp(alpha, 0f, 0.22f), 0.12f);
		tw.TweenInterval(Mathf.Max(0f, holdSec));
		tw.TweenProperty(_lingerTint, "color:a", 0f, Mathf.Max(0.1f, fadeSec))
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tw.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(_lingerTint))
				_lingerTint.Visible = false;
		}));
	}

	private void SpawnSurgeChainCallout(int chainCount, Color accent)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		Vector2 screenCenter = GetViewport().GetVisibleRect().Size * 0.5f;
		Vector2 worldPos = ScreenToWorld(screenCenter) + new Vector2(0f, 60f);
		var callout = new CombatCallout();
		_worldNode.AddChild(callout);
		callout.GlobalPosition = worldPos;
		callout.Initialize(
			$"SURGE ×{chainCount}",
			new Color(1.00f, 0.90f, 0.30f),
			duration: 1.8f,
			sizeOverride: 36);
	}

	private void FlashSpectacleAfterimage(Color accent, float strength)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_spectacleAfterimage))
			return;

		float s = Mathf.Clamp(strength, 0f, 1f);
		if (s <= 0f)
			return;

		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		_spectacleAfterimage.Visible = true;
		_spectacleAfterimage.PivotOffset = viewportSize * 0.5f;
		_spectacleAfterimage.Position = Vector2.Zero;
		_spectacleAfterimage.Scale = new Vector2(0.985f, 0.985f);
		_spectacleAfterimage.Color = new Color(
			Mathf.Clamp(accent.R * 0.82f + 0.18f, 0f, 1f),
			Mathf.Clamp(accent.G * 0.82f + 0.18f, 0f, 1f),
			Mathf.Clamp(accent.B * 0.82f + 0.18f, 0f, 1f),
			0f);

		float peakAlpha = Mathf.Lerp(0.05f, 0.12f, s);
		float expandScale = Mathf.Lerp(1.02f, 1.06f, s);
		float expandSec = Mathf.Lerp(0.10f, 0.20f, s);
		float fadeSec = Mathf.Lerp(0.20f, 0.36f, s);

		var tw = _spectacleAfterimage.CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_spectacleAfterimage, "color:a", peakAlpha, 0.04f);
		tw.TweenProperty(_spectacleAfterimage, "scale", new Vector2(expandScale, expandScale), expandSec)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		tw.SetParallel(false);
		tw.TweenProperty(_spectacleAfterimage, "color:a", 0f, fadeSec)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		tw.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(_spectacleAfterimage))
				return;

			_spectacleAfterimage.Visible = false;
			_spectacleAfterimage.Scale = Vector2.One;
		}));
	}

	private void TrySpawnExplosionResidue(
		ExplosionResidueProfile profile,
		Vector2 origin,
		Color accent,
		ITowerView? sourceTower)
	{
		if (_runState == null
			|| !SpectacleTuning.Current.EnableResidue
			|| !profile.ShouldSpawn
			|| profile.Kind == ExplosionResidueKind.None)
			return;

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		int baseMaxResidues = reducedMotion ? 7 : 12;
		int maxResidues = Mathf.Max(1, Mathf.RoundToInt(baseMaxResidues * Mathf.Max(0.1f, SpectacleTuning.Current.ResidueMaxActiveMultiplier)));
		while (_explosionResidues.Count >= maxResidues)
		{
			ExplosionResidueState evicted = _explosionResidues[0];
			_explosionResidues.RemoveAt(0);
			FreeResidueFx(evicted);
		}

		var residueState = new ExplosionResidueState
		{
			Kind = profile.Kind,
			Origin = origin,
			Radius = profile.Radius,
			Remaining = profile.DurationSeconds,
			TickInterval = Mathf.Max(
				0.06f,
				profile.TickIntervalSeconds * Mathf.Max(0.2f, SpectacleTuning.Current.ResidueTickIntervalMultiplier)),
			TickRemaining = 0f,
			Potency = profile.Potency,
			Accent = accent,
			SourceTower = sourceTower,
			PulseGate = false,
		};
		_explosionResidues.Add(residueState);
		AppendBotTraceEvent(
			eventType: "residue_spawned",
			worldPos: origin,
			stageId: profile.Kind.ToString().ToLowerInvariant(),
			residueSpawned: true,
			comboSkin: profile.Kind.ToString());

		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		var fx = new ExplosionResidueZoneFx();
		_worldNode.AddChild(fx);
		fx.GlobalPosition = origin;
		fx.Initialize(profile.Kind, accent, profile.Radius, profile.DurationSeconds, profile.Potency);
		residueState.FxNode = fx;
	}

	private void UpdateExplosionResidues(float delta)
	{
		if (_runState == null || CurrentPhase != GamePhase.Wave || delta <= 0f || _explosionResidues.Count == 0)
			return;

		_runState.TrackResidueUptime(delta, _explosionResidues.Count);

		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		for (int i = _explosionResidues.Count - 1; i >= 0; i--)
		{
			ExplosionResidueState residue = _explosionResidues[i];
			residue.Remaining -= delta;
			if (residue.Remaining <= 0f)
			{
				FreeResidueFx(residue);
				_explosionResidues.RemoveAt(i);
				continue;
			}

			ResidueTickAdvance tickAdvance = SpectacleExplosionCore.ResolveResidueTickAdvance(
				residue.TickRemaining,
				residue.TickInterval,
				delta,
				maxTicksPerFrame: 12);
			residue.TickRemaining = tickAdvance.TickRemainingAfter;
			if (tickAdvance.TickCount <= 0)
				continue;

			for (int tick = 0; tick < tickAdvance.TickCount; tick++)
				ApplyExplosionResidueTick(residue, reducedMotion);
		}
	}

	private static void FreeResidueFx(ExplosionResidueState residue)
	{
		if (residue.FxNode != null && GodotObject.IsInstanceValid(residue.FxNode))
			residue.FxNode.QueueFree();
		residue.FxNode = null;
	}

	private void ClearExplosionResidues()
	{
		for (int i = 0; i < _explosionResidues.Count; i++)
			FreeResidueFx(_explosionResidues[i]);
		_explosionResidues.Clear();
	}

	private void ApplyExplosionResidueTick(ExplosionResidueState residue, bool reducedMotion)
	{
		if (_runState == null)
			return;

		ITowerView? sourceTower = residue.SourceTower;
		if (sourceTower is GodotObject towerObj && !GodotObject.IsInstanceValid(towerObj))
			sourceTower = null;
		if (sourceTower == null)
			sourceTower = _runState.Slots
				.Select(s => s.Tower)
				.FirstOrDefault(t => t != null && (t is not GodotObject go || GodotObject.IsInstanceValid(go)));

		int maxTargets = residue.Kind == ExplosionResidueKind.BurnPatch
			? (reducedMotion ? 4 : 6)
			: (reducedMotion ? 5 : 8);
		var targets = _runState.EnemiesAlive
			.Where(IsEnemyUsable)
			.Where(e => residue.Origin.DistanceTo(e.GlobalPosition) <= residue.Radius)
			.OrderBy(e => residue.Origin.DistanceTo(e.GlobalPosition))
			.Take(maxTargets)
			.ToList();
		if (targets.Count == 0)
			return;

		switch (residue.Kind)
		{
			case ExplosionResidueKind.FrostSlow:
			{
				float slowDuration = Mathf.Clamp(0.20f + 0.10f * residue.Potency, 0.16f, 0.36f);
				float slowFactor = Mathf.Clamp(0.88f - 0.22f * residue.Potency, 0.56f, 0.90f);
				for (int i = 0; i < targets.Count; i++)
					Statuses.ApplySlow(targets[i], slowDuration, slowFactor);
				break;
			}
			case ExplosionResidueKind.VulnerabilityZone:
			{
				float ampDuration = Mathf.Clamp(0.24f + 0.12f * residue.Potency, 0.20f, 0.40f);
				float ampMultiplier = Mathf.Clamp(0.05f + 0.08f * residue.Potency, 0.05f, 0.16f);
				for (int i = 0; i < targets.Count; i++)
					Statuses.ApplyDamageAmp(targets[i], ampDuration, ampMultiplier);
				break;
			}
			case ExplosionResidueKind.BurnPatch:
			{
				if (sourceTower == null)
					break;
				float baseTickDamage = sourceTower.BaseDamage * Mathf.Clamp(0.06f + 0.08f * residue.Potency, 0.05f, 0.18f);
				for (int i = 0; i < targets.Count; i++)
				{
					float falloff = Mathf.Max(0.52f, 1f - i * 0.12f);
					ApplySpectacleDamage(
						sourceTower,
						targets[i],
						baseTickDamage * falloff,
						residue.Accent,
						heavyHit: false,
						triggerHitStopOnKill: false,
						allowOverkillBloom: false,
						allowMarkedPop: false,
						source: SpectacleDamageSource.Residue);
				}
				break;
			}
		}

		if (_botRunner == null && !reducedMotion && residue.PulseGate && targets.Count > 0)
			SpawnSpectacleImpactSparks(residue.Origin, residue.Accent, heavy: false);
		residue.PulseGate = !residue.PulseGate;
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
				"rift_prism" => new Color(0.60f, 1.00f, 0.58f),
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
					"rift_prism" => "SA",
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
		if (spillDamage <= 0f)
			return;

		Color spillColor = new Color(1.00f, 0.56f, 0.25f);
		float spillT = Mathf.Clamp(spillDamage / 140f, 0f, 1f);
		float visualRadius = Mathf.Lerp(84f, 190f, spillT);

		if (_botRunner == null)
		{
			PrimeExplosionCompression(worldPos, visualRadius * 0.75f, spillColor, maxTargets: 4 + Mathf.FloorToInt(spillT * 2f));
			SpawnSpectacleBurstFx(
				worldPos,
				spillColor,
				major: spillDamage >= 42f,
				power: 0.86f + spillT * 0.72f,
				stageTwoKick: spillDamage >= 34f);
		}

		if (spillDamage < 34f) return;
		if (!TryCombatCallout("overkill_spill", 6.5f)) return;
		SpawnCombatCallout("OVERKILL SPILL", worldPos, spillColor);
	}

	public void NotifyMarkedEnemyPop(ITowerView sourceTower, IEnemyView deadEnemy, IEnumerable<IEnemyView> enemiesAlive)
	{
		if (CurrentPhase != GamePhase.Wave || sourceTower == null || deadEnemy == null || enemiesAlive == null)
			return;

		Vector2 origin = deadEnemy.GlobalPosition;
		float radius = SpectacleExplosionCore.MarkedPopRadius;
		var nearby = enemiesAlive
			.Where(e => e != null && !ReferenceEquals(e, deadEnemy) && e.Hp > 0f)
			.Where(e => origin.DistanceTo(e.GlobalPosition) <= radius)
			.OrderBy(e => origin.DistanceTo(e.GlobalPosition))
			.Take(6)
			.ToList();

		foreach (var target in nearby)
		{
			Statuses.ApplyDamageAmp(
				target,
				SpectacleExplosionCore.MarkedPopDamageAmpDuration,
				SpectacleExplosionCore.MarkedPopDamageAmpMultiplier);
		}

		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		Color markColor = new Color(0.94f, 0.42f, 1.00f, 1f);
		PrimeExplosionCompression(
			origin,
			radius * 0.72f,
			markColor,
			maxTargets: Mathf.Clamp(3 + nearby.Count / 2, 3, 6));
		SpawnSpectacleBurstFx(
			origin,
			markColor,
			major: false,
			power: 0.92f + Mathf.Clamp(nearby.Count * 0.06f, 0f, 0.42f),
			stageTwoKick: nearby.Count >= 3);

		float baseDamage = sourceTower.BaseDamage * 0.24f;
		for (int i = 0; i < nearby.Count; i++)
		{
			if (nearby[i] is not EnemyInstance enemy || !IsEnemyUsable(enemy))
				continue;

			SpawnSpectacleArc(
				origin,
				enemy.GlobalPosition,
				markColor,
				intensity: 0.92f + i * 0.06f,
				mineChainStyle: true);
			SpawnSpectacleImpactSparks(enemy.GlobalPosition, markColor, heavy: false);
			ApplySpectacleDamage(
				sourceTower,
				enemy,
				baseDamage * Mathf.Max(0.56f, 1f - i * 0.12f),
				markColor,
				heavyHit: false,
				triggerHitStopOnKill: false,
				allowOverkillBloom: false,
				allowMarkedPop: false,
				source: SpectacleDamageSource.ExplosionFollowUp);
		}

		if (nearby.Count >= 2 && TryCombatCallout("marked_pop", 4.8f))
			SpawnCombatCallout("MARKED POP", origin, markColor, durationScale: 1.20f);
	}

	public void NotifyFeedbackLoopProc(SlotTheory.Entities.ITowerView tower)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave) return;

		ulong now = Time.GetTicksMsec();
		if (_feedbackLoopBurst.TryGetValue(tower, out var state) && now - state.firstMs <= 1300)
			_feedbackLoopBurst[tower] = (state.firstMs, state.count + 1);
		else
			_feedbackLoopBurst[tower] = (now, 1);

		var burst = _feedbackLoopBurst[tower];
		if (burst.count < 3) return;

		_feedbackLoopBurst.Remove(tower);
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

	private RunScorePayload BuildRunScorePayload(bool won, int waveReached, string buildName = "")
	{
		string mapId = string.IsNullOrEmpty(_runState.SelectedMapId)
			? LeaderboardKey.RandomMapId
			: _runState.SelectedMapId!;
		var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;
		long nowUnix = (long)System.Math.Floor(Time.GetUnixTimeFromSystem());
		string gameVersion = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
		return new RunScorePayload(
			mapId,
			difficulty,
			won,
			waveReached,
			System.Math.Max(0, _runState.Lives),
			_runState.TotalDamageDealt,
			_runState.TotalKills,
			_runState.TotalPlayTime,
			nowUnix,
			gameVersion,
			BuildSnapshotCodec.CaptureFromRunState(_runState),
			buildName
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
		// Steam supplies the player name itself; only non-Steam providers need an explicit display name.
		bool providerNeedsName = LeaderboardManager.Instance?.ProviderName != "Steam";
		if (bucket.IsGlobalEligible && providerNeedsName && (sm == null || string.IsNullOrEmpty(sm.PlayerName)))
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
		GD.Print($"[Leaderboards] Submit result: {result.State} — {result.Message}");
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
		var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;
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
		var nextCfg = DataLoader.GetWaveConfig(
			_runState.WaveIndex,
			SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
			_runState.SelectedMapId);
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

	private void ShakeWorldMicro()
	{
		if (!Balance.EnableScreenShake || !GodotObject.IsInstanceValid(_worldNode))
			return;

		var tween = CreateTween();
		tween.TweenProperty(_worldNode, "position", new Vector2(1.6f, 0.9f), 0.018f);
		tween.TweenProperty(_worldNode, "position", new Vector2(-1.3f, -0.8f), 0.018f);
		tween.TweenProperty(_worldNode, "position", Vector2.Zero, 0.028f);
	}

	private void HandlePerfProfilerHotkey(bool devMode)
	{
		if (!devMode)
		{
			_perfReportHotkeyLatch = false;
			return;
		}

		bool hotkeyPressed = Input.IsPhysicalKeyPressed(Key.F9);
		if (hotkeyPressed && !_perfReportHotkeyLatch)
		{
			string mapId = string.IsNullOrWhiteSpace(_runState.SelectedMapId) ? "unknown_map" : _runState.SelectedMapId;
			int wave = Mathf.Clamp(_runState.WaveIndex + 1, 1, Balance.TotalWaves);
			string reportPath = _enemyRenderPerfProfiler.WriteReport(
				mapId,
				wave,
				SettingsManager.Instance,
				MobileOptimization.IsMobile());
			GD.Print($"[PERF] Enemy render report saved: {reportPath}");
		}

		_perfReportHotkeyLatch = hotkeyPressed;
	}

	private void UpdatePlacementLabel()
	{
		if (_undoPlacementActive)
		{
			_draftPanel.SetPlacementHintText("Tower placed  •  tap UNDO to revert");
			return;
		}
		if (CurrentPhase != GamePhase.Draft || (!_draftPanel.IsAwaitingSlot && !_draftPanel.IsAwaitingTower))
			return;
		_draftPanel.SetPlacementHintText(_draftPanel.PlacementHint);
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

	private static bool IsSynergyTower(SlotTheory.Entities.ITowerView tower, string modifierId)
	{
		return modifierId switch
		{
			"exploit_weakness" => tower.AppliesMark || tower.TowerId == "marker_tower",
			"chain_reaction" => tower.IsChainTower,
			"overkill" or "focus_lens" => tower.TowerId == "heavy_cannon"
				|| tower.Modifiers.Any(m => m.ModifierId == "focus_lens")
				|| tower.BaseDamage >= 40f,
			_ => false,
		};
	}

	public void NotifyModifierProc(SlotTheory.Entities.ITowerView tower, string modifierId)
	{
		if (_modifierProcCounts.TryGetValue(modifierId, out int n))
			_modifierProcCounts[modifierId] = n + 1;
		else
			_modifierProcCounts[modifierId] = 1;

		int slot = -1;
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			if (ReferenceEquals(_runState.Slots[i].Tower, tower)) { slot = i; break; }
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

	public void RegisterSpectacleShotFired(SlotTheory.Entities.ITowerView tower)
	{
		if (CurrentPhase != GamePhase.Wave || tower == null)
			return;
		_spectacleSystem.RegisterShotFired(tower);
	}

	public void RegisterSpectacleProc(
		SlotTheory.Entities.ITowerView tower,
		string modifierId,
		float eventScalar = 1f,
		float eventDamage = -1f)
	{
		if (CurrentPhase != GamePhase.Wave
			|| tower == null
			|| string.IsNullOrEmpty(modifierId)
			|| !float.IsFinite(eventScalar)
			|| eventScalar <= 0f)
			return;
		if (!float.IsFinite(eventDamage))
			eventDamage = -1f;
		_spectacleSystem.RegisterProc(tower, modifierId, eventScalar, eventDamage);
	}

	private void TriggerSpectacleSlowMo(float realDuration, float speedFactor)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave)
			return;
		if (_spectacleSlowMoActive)
			return;

		float duration = Mathf.Max(0.10f, realDuration);
		float factor = Mathf.Clamp(speedFactor, 0.05f, 1.0f);
		_spectacleSlowMoActive = true;
		_spectacleSlowMoFactor = factor;
		RefreshSpectacleSpeedMultiplier();

		ulong token = ++_spectacleSlowMoToken;
		GetTree().CreateTimer(duration, true, false, true).Timeout += () =>
		{
			if (!_spectacleSlowMoActive || token != _spectacleSlowMoToken)
				return;

			_spectacleSlowMoActive = false;
			_spectacleSlowMoFactor = 1f;
			RefreshSpectacleSpeedMultiplier();
		};
	}

	private void RefreshSpectacleSpeedMultiplier()
	{
		float mergedFactor = Mathf.Min(_spectacleSlowMoFactor, _hitStopFactor);
		EnemyInstance.SetSpectacleSpeedMultiplier(Mathf.Clamp(mergedFactor, 0.05f, 1f));
	}

	public void TriggerHitStop(float realDuration = 0.042f, float slowScale = 0.20f)
	{
		if (_botRunner != null)
		{
			_botStepHitStopRequests += 1;
			AppendBotTraceEvent(
				eventType: "hitstop_requested",
				hitStopRequested: true,
				stageId: "hitstop");
			return;
		}
		if (CurrentPhase != GamePhase.Wave) return;
		if (_hitStopActive || _hitStopCooldown > 0f) return;

		_hitStopActive = true;
		_hitStopCooldown = 0.08f;
		_hitStopFactor = Mathf.Clamp(slowScale, 0.05f, 1f);
		RefreshSpectacleSpeedMultiplier();

		float duration = Mathf.Max(0.005f, realDuration);
		ulong token = ++_hitStopToken;
		GetTree().CreateTimer(duration, true, false, true).Timeout += () =>
		{
			if (token != _hitStopToken)
				return;
			_hitStopFactor = 1f;
			_hitStopActive = false;
			RefreshSpectacleSpeedMultiplier();
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

	private void UpdateSpectacleVisuals()
	{
		if (_runState?.Slots == null || _spectacleSystem == null)
			return;

		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].TowerNode;
			if (tower == null || !GodotObject.IsInstanceValid(tower))
				continue;

			SpectacleVisualState visual = _spectacleSystem.GetVisualState(tower);
			tower.SpectacleMeterNormalized = visual.MeterNormalized;
			tower.SpectaclePulse = visual.Pulse;
			tower.SpectacleAccent = visual.PrimaryModId;
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

	private void UpdateTowerPlacementPreviewGhost(float delta)
	{
		if (!_draftPanel.HasTowerPreview || !_draftPanel.IsAwaitingSlot)
		{
			ClearTowerPlacementPreviewGhost();
			return;
		}

		int slot = _draftPanel.TowerPreviewSlot;
		string towerId = _draftPanel.PendingTowerId;
		if (slot < 0 || slot >= _slotNodes.Length || string.IsNullOrEmpty(towerId))
		{
			ClearTowerPlacementPreviewGhost();
			return;
		}
		if (!_draftPanel.IsSlotValidTarget(slot))
		{
			_draftPanel.CancelTowerPreview();
			ClearTowerPlacementPreviewGhost();
			return;
		}

		bool needsRebuild = _previewTowerGhostSlot != slot
			|| _previewTowerGhostId != towerId
			|| !GodotObject.IsInstanceValid(_previewTowerGhost);
		if (needsRebuild)
		{
			ClearTowerPlacementPreviewGhost();
			_previewTowerGhostSlot = slot;
			_previewTowerGhostId = towerId;
			_previewTowerGhostPhase = 0f;

			var def = DataLoader.GetTowerDef(towerId);
			_previewTowerGhost = new TowerInstance
			{
				TowerId = towerId,
				BaseDamage = def.BaseDamage,
				AttackInterval = def.AttackInterval,
				Range = def.Range,
				AppliesMark = def.AppliesMark,
				SplitCount = def.SplitCount,
				ChainCount = def.ChainCount,
				ChainRange = def.ChainRange,
				ChainDamageDecay = def.ChainDamageDecay,
				ProjectileColor = GetTowerProjectileColor(towerId),
				BodyColor = GetTowerBodyColor(towerId),
				ZIndex = 19,
			};
			_slotNodes[slot].AddChild(_previewTowerGhost);
		}
		else
		{
			_previewTowerGhostPhase += delta;
		}

		float pulse = 0.5f + 0.5f * Mathf.Sin(_previewTowerGhostPhase * 9f);
		if (GodotObject.IsInstanceValid(_previewTowerGhost))
		{
			_previewTowerGhost.Modulate = new Color(1f, 1f, 1f, 0.26f + pulse * 0.28f);
			float s = 0.94f + pulse * 0.04f;
			_previewTowerGhost.Scale = new Vector2(s, s);
		}

		if (GodotObject.IsInstanceValid(_slotPreviewGlows[slot]))
		{
			var accent = GetTowerBodyColor(towerId);
			var glow = _slotPreviewGlows[slot];
			glow.DefaultColor = new Color(accent.R, accent.G, accent.B, 0.92f);
			glow.Modulate = new Color(1f, 1f, 1f, 0.24f + pulse * 0.38f);
		}
	}

	private void ClearTowerPlacementPreviewGhost()
	{
		if (_previewTowerGhostSlot >= 0 && _previewTowerGhostSlot < _slotPreviewGlows.Length)
		{
			if (GodotObject.IsInstanceValid(_slotPreviewGlows[_previewTowerGhostSlot]))
				_slotPreviewGlows[_previewTowerGhostSlot].Modulate = Colors.Transparent;
		}

		if (GodotObject.IsInstanceValid(_previewTowerGhost))
			_previewTowerGhost.QueueFree();

		_previewTowerGhost = null;
		_previewTowerGhostSlot = -1;
		_previewTowerGhostPhase = 0f;
		_previewTowerGhostId = "";
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




