using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
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
	public static float CombatVisualChaosLoad { get; private set; } = 0f;

	public GamePhase CurrentPhase { get; private set; } = GamePhase.Boot;
	public TutorialManager? ActiveTutorial => _tutorialManager;

	[Export] public PackedScene? EnemyScene { get; set; }
	[Export] public Path2D? LanePath { get; set; }

	private RunState _runState = null!;
	private DraftSystem _draftSystem = null!;
	private WaveSystem _waveSystem = null!;
	private CombatSim _combatSim = null!;
	private DraftPanel _draftPanel = null!;
	private HudPanel _hudPanel = null!;
	private EndScreen _endScreen = null!;
	private RunScorePayload? _pendingWinScorePayload;
	private string _pendingWinLeaderboardLine = "";
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
	private float[,] _slotModIconPulseRemaining = new float[Balance.SlotCount, Balance.MaxPremiumModSlots];
	private int      _highlightedSlot      = -1;
	private bool     _highlightedSlotValid = false;
	private Map _currentMap = null!;
	private Node2D _mapVisuals = null!;
	private PathFlow? _pathFlow;
	private GridBackground? _gridBackground;
	private Panel _tooltipPanel = null!;
	private VBoxContainer _tooltipBody = null!;
	private Label _tooltipTitleLabel = null!;
	private VBoxContainer _tooltipStatsBox = null!;
	private Label _tooltipLabel = null!;
	private int _mobileTooltipFontSize = 13;
	private float _mobileTooltipUiScale = 1f;
	private TowerInstance? _selectedTooltipTower;
	private EnemyInstance? _selectedTooltipEnemy;
	private Node2D? _targetModePanelRoot;
	private TowerInstance? _targetModePanelTower;
	private readonly List<TargetModePanelOption> _targetModePanelOptions = new();
	private ColorRect? _targetModeHoverBg;
	private Label? _targetModeHoverLabel;
	private string _targetModeHoverText = string.Empty;
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
	private double _lastSpectacleScreenFlashAt = -99d;
	private double _lastSpectacleAfterimageAt = -99d;
	private int   _surgeChainCount = 0;
	private float _surgeChainResetTimer = 0f;
	private Tween? _globalSurgeBannerTween;
	private ulong _globalSurgeBannerToken = 0;
	private Node2D _worldNode = null!;
	private CanvasLayer _pipLayer = null!;
	private int _activePipCount;
	private Line2D[]? _pathLines;
	private Vector2[]? _fullPathPoints;
	private BotRunner? _botRunner;
	private ScreenshotPipeline? _screenshotPipeline;
	private string _screenshotOutputDir = "";
	// Auto-draft mode: bot AI picks cards but _botRunner stays null so all VFX fire normally.
	private bool       _autoDraftMode        = false;
	private BotPlayer? _autoDraftBot;
	private int        _autoDraftRunsTotal    = 0;
	private int        _autoDraftRunsDone     = 0;
	private int   _botWaveInRangeSum;
	private int   _botWaveInRangeSamples;
	private int   _botWaveSteps;
	private int   _botStepExplosionBursts;
	private int   _botStepHitStopRequests;
	private bool  _botFastMetrics = false;
	private bool  _botTraceCaptureEnabled = false;
	private bool  _botGlobalSurgePending = false;
	private float _botGlobalSurgeReadyAtPlayTime = -1f;
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
	private Line2D? _previewRangeOverlay;
	private int _previewTowerGhostSlot = -1;
	private float _previewTowerGhostPhase = 0f;
	private string _previewTowerGhostId = "";
	private TowerInstance? _previewTowerGhost;
	private bool _runAbandoned      = false;  // set on intentional exit to suppress _ExitTree re-save
	private bool _exitSnapshotAlreadySaved = false; // explicit pause-exit save already done
	private bool _restartInProgress = false;  // guard against re-entrant RestartRun() calls
	private TutorialManager? _tutorialManager;
	private SlotTheory.UI.TutorialCallout? _tutorialCallout;
	private CanvasLayer? _tutorialTargetingOverlay;
	private bool _awaitingTargetingCycleDismiss = false;
	private int _mapTotalWaves = Balance.TotalWaves; // overridden for maps with custom wave counts (e.g. tutorial)
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
	private readonly System.Collections.Generic.Dictionary<string, int> _surgeArchetypeCounts = new();
	private readonly HashSet<ITowerView> _comboTowersSeenThisRun = new();
	private readonly SurgeHintRuntimeState _surgeHintRuntime = new();
	private readonly Dictionary<ITowerView, float> _surgeHintLastMeterByTower = new();
	private string _runBuildIdentity = "UNFORMED";
	private bool _runBuildIdentityFormed = false;
	private bool _firstTowerSurgeAssistShown = false;
	private bool _tipSeenProcFeed = false;
	private bool _tipSeenTowerSurge = false;
	private bool _tipSeenGlobalReady = false;
	private int _runIdentityPressureScore = 0;
	private int _runIdentityChainScore = 0;
	private int _runIdentityDetonationScore = 0;
	private int _activeProcFeedPips = 0;
	private bool _globalReadyWindowOpen = false;
	private float _globalReadySincePlayTime = 0f;
	private ulong _globalActivateHintToken = 0;
	private ulong _nonCriticalHintSuppressUntilUsec = 0;
	private bool _initialDraftMusicPrimed = false;
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
	private static readonly TargetingMode[] StandardTargetingModes =
	{
		TargetingMode.First,
		TargetingMode.Strongest,
		TargetingMode.LowestHp,
		TargetingMode.Last,
	};
	private static readonly TargetingMode[] RiftSapperTargetingModes =
	{
		TargetingMode.First,
		TargetingMode.Strongest,
		TargetingMode.LowestHp,
	};

	private enum SpectacleConsequenceKind
	{
		None,
		FrostSlow,
		Vulnerability,
		BurnPatch,
	}

	private readonly struct TargetModePanelOption
	{
		public TargetModePanelOption(TargetingMode mode, Rect2 worldRect, string displayName)
		{
			Mode = mode;
			WorldRect = worldRect;
			DisplayName = displayName;
		}

		public TargetingMode Mode { get; }
		public Rect2 WorldRect { get; }
		public string DisplayName { get; }
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
		_exitSnapshotAlreadySaved = false;
		CombatVisualChaosLoad = 0f;
		_lastSpectacleScreenFlashAt = -99d;
		_lastSpectacleAfterimageAt = -99d;
		SoundManager.Instance?.SetMenuAmbientEnabled(false);
		_isMobilePlatform = MobileOptimization.IsMobile();
		EnemyInstance.SetSpectacleSpeedMultiplier(1f);

		// Apply --demo override before DataLoader.LoadAll() so demo-gating is correct.
		if (OS.GetCmdlineUserArgs().Contains("--demo"))
			Balance.SetDemoOverride(true);

		DataLoader.LoadAll();
		SpectacleTuning.Reset();

		// Auto-load the per-build tuning profile from Data/ if present.
		// --tuning_file (parsed below) overrides this when explicitly provided.
		{
			string autoTuningPath = Balance.IsDemo
				? "res://Data/best_tuning_demo.json"
				: "res://Data/best_tuning_full.json";
			if (SpectacleTuningLoader.TryLoadFromGodotResource(autoTuningPath, out var autoProfile, out _))
				SpectacleTuning.Apply(autoProfile, Balance.IsDemo ? "auto_demo" : "auto_full");
		}

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

		if (BotMetricsDeltaReporter.TryRunFromArgs(userArgs))
		{
			GetTree().Quit();
			return;
		}
		if (userArgs.Contains("--lab_scenario") || userArgs.Contains("--lab_sweep") || userArgs.Contains("--lab_tower_benchmark") || userArgs.Contains("--lab_modifier_benchmark"))
		{
			bool success = CombatLabCli.Run(userArgs);
			if (!success)
				GD.PrintErr("[LAB] Failed to run combat lab automation.");
			GetTree().Quit();
			return;
		}

		// Bot playtest mode: godot --headless --path ... -- --bot --runs N --difficulty easy|normal|hard
		if (userArgs.Contains("--bot"))
		{
			string NormalizeBotArg(string token)
			{
				if (string.IsNullOrWhiteSpace(token))
					return string.Empty;
				return token.Trim().TrimStart('-').Replace('-', '_').ToLowerInvariant();
			}

			int FindBotArgIndex(params string[] aliases)
			{
				if (aliases == null || aliases.Length == 0)
					return -1;

				for (int i = 0; i < userArgs.Length; i++)
				{
					string current = NormalizeBotArg(userArgs[i]);
					foreach (string alias in aliases)
					{
						if (current == NormalizeBotArg(alias))
							return i;
					}
				}
				return -1;
			}

			bool HasBotArg(params string[] aliases) => FindBotArgIndex(aliases) >= 0;

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
			if (rio < 0)
				rio = System.Array.IndexOf(userArgs, "--run_offset"); // legacy alias
			if (rio >= 0 && rio + 1 < userArgs.Length)
				int.TryParse(userArgs[rio + 1], out runIndexOffset);

			bool botFastMetrics = HasBotArg("--bot_fast_metrics", "--bot-fast-metrics");
			bool botQuiet = HasBotArg("--bot_quiet", "--bot-quiet");
			bool towerSurgeBenchmark = HasBotArg("--tower_surge_benchmark", "--tower-surge-benchmark");
			BotRunner.TowerSurgeBenchmarkConfig? towerSurgeBenchmarkConfig = null;

			if (towerSurgeBenchmark)
			{
				if (targetMap == null)
					targetMap = "crossroads";
				if (!targetDifficulty.HasValue)
					targetDifficulty = DifficultyMode.Easy;

				int benchmarkSlot = 0;
				int bsi = FindBotArgIndex("--benchmark_slot", "--benchmark-slot");
				if (bsi >= 0 && bsi + 1 < userArgs.Length)
					int.TryParse(userArgs[bsi + 1], out benchmarkSlot);

				int benchmarkMaxWaves = 10;
				int bwi = FindBotArgIndex("--benchmark_max_waves", "--benchmark-max-waves");
				if (bwi >= 0 && bwi + 1 < userArgs.Length)
					int.TryParse(userArgs[bwi + 1], out benchmarkMaxWaves);

				int benchmarkTrialsPerTower = 1;
				int btpi = FindBotArgIndex("--benchmark_trials_per_tower", "--benchmark-trials-per-tower");
				if (btpi >= 0 && btpi + 1 < userArgs.Length)
					int.TryParse(userArgs[btpi + 1], out benchmarkTrialsPerTower);

				string[] benchmarkMods = new[] { "focus_lens", "overkill", "feedback_loop" };
				int bmi = FindBotArgIndex("--benchmark_mods", "--benchmark-mods");
				if (bmi >= 0 && bmi + 1 < userArgs.Length)
				{
					string[] parsedMods = userArgs[bmi + 1]
						.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
					if (parsedMods.Length > 0)
						benchmarkMods = parsedMods;
				}

				string[] benchmarkTowers = System.Array.Empty<string>();
				int bti = FindBotArgIndex("--benchmark_towers", "--benchmark-towers");
				if (bti >= 0 && bti + 1 < userArgs.Length)
				{
					benchmarkTowers = userArgs[bti + 1]
						.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
				}
				if (benchmarkTowers.Length == 0)
				{
					benchmarkTowers = DataLoader.GetAllTowerIds()
						.Where(id => !string.IsNullOrWhiteSpace(id))
						.OrderBy(id => id, System.StringComparer.Ordinal)
						.ToArray();
				}

				benchmarkSlot = Mathf.Clamp(benchmarkSlot, 0, Balance.SlotCount - 1);
				benchmarkMaxWaves = Mathf.Max(1, benchmarkMaxWaves);
				benchmarkTrialsPerTower = Mathf.Max(1, benchmarkTrialsPerTower);
				runs = Mathf.Max(1, benchmarkTowers.Length * benchmarkTrialsPerTower);
				forcedTower = null;
				forcedMod = null;
				targetStrategy = BotStrategy.TowerFirst;
				strategySet = null;

				towerSurgeBenchmarkConfig = new BotRunner.TowerSurgeBenchmarkConfig
				{
					MapId = targetMap,
					TowerIds = benchmarkTowers,
					ModifierOrder = benchmarkMods,
					SlotIndex = benchmarkSlot,
					MaxWaves = benchmarkMaxWaves,
					TrialsPerTower = benchmarkTrialsPerTower,
				};
			}

			// Orchestrator mode: when --bot is used without --map or --difficulty,
			// spawn one Godot process per map*difficulty pair (up to 24 concurrent).
			// Each child gets --no_orchestrate so it runs normally without re-forking.
			bool noOrchestrate = HasBotArg("--no_orchestrate");
			if (!noOrchestrate && !towerSurgeBenchmark && targetMap == null && !targetDifficulty.HasValue)
			{
				RunBotOrchestrator(
					godotExe: OS.GetExecutablePath(),
					projectPath: Path.GetFullPath(ProjectSettings.GlobalizePath("res://")),
					totalRuns: runs,
					strategySet: strategySet,
					tuningFile: tuningFile,
					runIndexOffset: runIndexOffset,
					fastMetrics: botFastMetrics,
					quiet: botQuiet,
					isDemo: Balance.IsDemo,
					metricsOutputPath: metricsOutputPath,
					forcedTower: forcedTower,
					forcedMod: forcedMod);
				GetTree().Quit();
				return;
			}

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
				runIndexOffset,
				botFastMetrics,
				botQuiet,
				towerSurgeBenchmarkConfig);
			_botFastMetrics = _botRunner.FastMetrics;
			_botTraceCaptureEnabled = _botRunner.TraceCaptureEnabled;
			Engine.MaxFps = 0;
			string benchmarkSuffix = towerSurgeBenchmarkConfig == null
				? string.Empty
				: $" tower_surge_benchmark=on slot={towerSurgeBenchmarkConfig.SlotIndex} mods={string.Join("->", towerSurgeBenchmarkConfig.ModifierOrder)} max_waves={towerSurgeBenchmarkConfig.MaxWaves} towers={towerSurgeBenchmarkConfig.TowerIds.Length}";
			GD.Print($"[BOT] Headless playtest: {runs} runs{(targetDifficulty.HasValue ? $" ({targetDifficulty.Value})" : "")}{(targetMap != null ? $" on {targetMap}" : "")}{(targetStrategy.HasValue ? $" strategy={targetStrategy.Value}" : "")}{(strategySet != null ? $" strategy_set={strategySet}" : "")}{(forcedTower != null ? $" tower={forcedTower}" : "")}{(forcedMod != null ? $" mod={forcedMod}" : "")}{(runIndexOffset > 0 ? $" run_offset={runIndexOffset}" : "")}{(botFastMetrics ? " fast_metrics=on" : "")}{(botQuiet ? " quiet=on" : "")}{(string.IsNullOrWhiteSpace(metricsOutputPath) ? "" : $" metrics={metricsOutputPath}")}{(string.IsNullOrWhiteSpace(traceOutputPath) ? "" : $" trace={traceOutputPath}")}{benchmarkSuffix}");
		}

		// Screenshot pipeline: enabled by --screenshot-capture (requires non-headless)
		if (userArgs.Contains("--screenshot-capture") && DisplayServer.GetName() != "headless")
		{
			string defaultOut = Path.Combine(
				Path.GetFullPath(ProjectSettings.GlobalizePath("res://")),
				"screenshots");
			int soIdx = System.Array.IndexOf(userArgs, "--screenshot_out");
			string outDir = (soIdx >= 0 && soIdx + 1 < userArgs.Length)
				? userArgs[soIdx + 1]
				: defaultOut;
			_screenshotPipeline = new ScreenshotPipeline();
			AddChild(_screenshotPipeline);
			GD.Print($"[SCREENSHOT] Pipeline created. Output: {outDir}");
			// Store outDir for InitScreenshotPipeline(); actual Initialize() called after _runState is set.
			// We stash the resolved path in a local -- reused in InitScreenshotPipeline below.
			// (Captured via lambda scope in RestartRun via field set below.)
			_screenshotOutputDir = outDir;
		}

		// Auto-draft mode: visually-rendered games with AI draft picks for screenshot capture.
		// Enabled via --auto-draft. Does NOT set _botRunner, so all VFX/visuals fire normally.
		if (userArgs.Contains("--auto-draft") && _botRunner == null)
		{
			int runs = 5;
			int ri = System.Array.IndexOf(userArgs, "--auto-runs");
			if (ri >= 0 && ri + 1 < userArgs.Length) int.TryParse(userArgs[ri + 1], out runs);

			BotStrategy strategy = BotStrategy.PlayerStyle2;
			int si = System.Array.IndexOf(userArgs, "--strategy");
			if (si >= 0 && si + 1 < userArgs.Length)
				System.Enum.TryParse<BotStrategy>(userArgs[si + 1], ignoreCase: true, out strategy);

			int mapIdx = System.Array.IndexOf(userArgs, "--map");
			if (mapIdx >= 0 && mapIdx + 1 < userArgs.Length)
				SlotTheory.UI.MapSelectPanel.SetPendingMapSelection(userArgs[mapIdx + 1]);

			_autoDraftMode      = true;
			_autoDraftRunsTotal = runs;
			_autoDraftRunsDone  = 0;
			_autoDraftBot       = new BotPlayer(strategy, (int)(System.Environment.TickCount64 & 0x7FFFFFFF));
			GD.Print($"[AUTO-DRAFT] {runs} run(s), strategy={strategy}, map={SlotTheory.UI.MapSelectPanel.PendingMapSelection}. All visuals active.");
		}

		_runState = new RunState();
		_botGlobalSurgePending = false;
		_botGlobalSurgeReadyAtPlayTime = -1f;
		_initialDraftMusicPrimed = false;
		if (_botRunner == null)
			SettingsManager.Instance?.IncrementRunsStarted();
		if (_botRunner == null)
			_botTraceCaptureEnabled = false;
		if (_botRunner != null)
			ResetBotTraceBuffer();
		
		// Tutorial run: override map selection and force Easy difficulty
		bool isTutorialRun = SettingsManager.Instance?.PendingTutorialRun ?? false;
		_runState.IsTutorialRun = isTutorialRun;
		if (isTutorialRun && SettingsManager.Instance != null)
		{
			SettingsManager.Instance.PendingTutorialRun = false;
			SettingsManager.Instance.SetDifficulty(DifficultyMode.Easy);
			SlotTheory.UI.MapSelectPanel.SetPendingMapSelection("tutorial");
		}

		// Apply pending map selection from MapSelectPanel if available
		_runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
		if (_runState.SelectedMapId == "random_map" && SlotTheory.UI.MapSelectPanel.PendingProceduralSeed > 0)
			_runState.RngSeed = (int)SlotTheory.UI.MapSelectPanel.PendingProceduralSeed;
		if (_botRunner != null && _runState.SelectedMapId == "random_map")
			_runState.RngSeed = ResolveBotProceduralSeed();
		bool resumeRequested = SettingsManager.Instance?.ConsumePendingResumeRun() ?? false;
		if (resumeRequested)
			LoadPendingMobileSnapshot();
		else
			MobileRunSession.Clear();

		// Determine wave count - tutorial map has its own shorter wave table
		_mapTotalWaves = ResolveMapTotalWaves(_runState.SelectedMapId);

		// Set up tutorial manager if this is a tutorial run
		if (isTutorialRun && _botRunner == null)
		{
			_tutorialCallout = new SlotTheory.UI.TutorialCallout();
			GetTree().Root.AddChild(_tutorialCallout);
			_tutorialManager = new TutorialManager(_tutorialCallout);
		}
		
		// In bot mode seed the draft pool deterministically from the run index so
		// all tuning candidates face identical card offerings on the same run number.
		_draftSystem = _botRunner != null
			? new DraftSystem(_botRunner.CompletedRuns * 6271 + 1)
			: new DraftSystem();
		_waveSystem = new WaveSystem();
		_combatSim = new CombatSim(_runState)
		{
			EnemyScene = EnemyScene,
			LanePath   = LanePath,
			Sounds     = SoundManager.Instance,
		};
		_combatSim.BotMode = _botRunner != null;
		_spectacleSystem.Reset();
		_spectacleSystem.OnSurgeTriggered   -= OnSpectacleSurgeTriggered;
		_spectacleSystem.OnProcMeterContributed -= OnSpectacleProcMeterContributed;
		_spectacleSystem.OnTowerGlobalContribution -= OnTowerGlobalContribution;
		_spectacleSystem.OnGlobalTriggered  -= OnGlobalSurgeTriggered;
		_spectacleSystem.OnGlobalSurgeReady -= OnGlobalSurgeReadyHandler;
		_spectacleSystem.OnSurgeTriggered   += OnSpectacleSurgeTriggered;
		_spectacleSystem.OnProcMeterContributed += OnSpectacleProcMeterContributed;
		_spectacleSystem.OnTowerGlobalContribution += OnTowerGlobalContribution;
		_spectacleSystem.OnGlobalTriggered  += OnGlobalSurgeTriggered;
		_spectacleSystem.OnGlobalSurgeReady += OnGlobalSurgeReadyHandler;
		_screenshotPipeline?.Initialize(_runState, _screenshotOutputDir, 0);
		_draftPanel = GetNode<DraftPanel>("../DraftPanel");
		_hudPanel   = GetNode<HudPanel>("../HudPanel");
		_hudPanel.GlobalSurgeActivateRequested -= OnHudGlobalSurgeActivate;
		_hudPanel.GlobalSurgeActivateRequested += OnHudGlobalSurgeActivate;
		_endScreen  = GetNode<EndScreen>("../EndScreen");
		_endScreen.ContinueEndlessPressed += OnContinueEndlessPressed;
		_endScreen.WinExited += OnWinExited;
		_unlockRevealScreen = GetNode<UnlockRevealScreen>("../UnlockRevealScreen");
		_unlockRevealScreen.RevealClosed += OnUnlockRevealClosed;

		_worldNode = GetNode<Node2D>("../World");
		_activePipCount = 0;
		if (!GodotObject.IsInstanceValid(_pipLayer))
		{
			_pipLayer = new CanvasLayer { Layer = 2, Name = "SurgePipLayer" };
			AddChild(_pipLayer);
		}
		_mapVisuals = new Node2D { Name = "_mapVisuals" };
		_worldNode.AddChild(_mapVisuals);
		_worldNode.MoveChild(_mapVisuals, 0);

		_hudPanel.SetTotalWaves(_mapTotalWaves);
		_hudPanel.SetDifficulty(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);
		if (CampaignManager.IsCampaignRun && CampaignManager.ActiveStage?.Mandate.IsActive == true)
			_hudPanel.SetMandateStrip(CampaignManager.ActiveStage.Mandate.DisplayText);

			GenerateMap();
			SetupMobileCamera();
			CenterWorldNode();
			CallDeferred(MethodName.CenterWorldNode); // re-run after layout settles
			ApplyCampaignMandate();
			SetupSlots();
			SetupTooltip();
			SetupAnnouncer();

		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		_lastWaveReport = null;
		_runState.MaxLives = Balance.GetStartingLives(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);
		_runState.Lives = _runState.MaxLives;
		_hudPanel.ResetSpeed();   // Engine.TimeScale persists across scene loads; always reset on fresh run
		GetViewport().SizeChanged += () => CallDeferred(MethodName.CenterWorldNode);
		if (!TryRestoreMobileSnapshot())
			AnimateTileDropIn(() => AnimateMapReveal(() => AnimateSlotDropIn(() =>
			{
				SoundManager.Instance?.EnsureBackgroundMusicStarted();
				ShowStageIntroThenContinue(StartDraftPhase);
			})));
	}

	public override void _Process(double delta)
	{
		// When ProcessMode=Always is set (e.g. targeting tutorial panel) _Process still ticks
		// while the tree is paused. Keep hover UI responsive, skip gameplay logic.
		if (GetTree().Paused)
		{
			if (_targetModePanelTower != null && (CurrentPhase != GamePhase.Wave || !GodotObject.IsInstanceValid(_targetModePanelTower)))
				HideTargetModePanel();
			UpdateTargetModePanelHover();
			return;
		}
		if (_targetModePanelTower != null && (CurrentPhase != GamePhase.Wave || !GodotObject.IsInstanceValid(_targetModePanelTower)))
			HideTargetModePanel();
		UpdateTargetModePanelHover();

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
		RefreshNonCriticalHintSuppression();
		if (_runState == null || _combatSim == null || _waveSystem == null)
		{
			CombatVisualChaosLoad = 0f;
			_cancelBtnShown = false;
			return;
		}

		UpdateCombatVisualChaosLoad((float)delta);

		// Fast bot path: skip HUD/tooltips/visual updates and only advance simulation.
		if (_botRunner != null)
		{
			CombatVisualChaosLoad = 0f;
			if (CurrentPhase != GamePhase.Wave)
			{
				_lowLivesHeartbeatTimer = 0f;
				ClearExplosionResidues();
				return;
			}

			BotTick();
			return;
		}

		if (_botRunner == null) UpdateTooltip();

		if (!_undoPlacementActive && CurrentPhase == GamePhase.Draft && draftPanelReady && (draftPanel!.IsAwaitingSlot || draftPanel.IsAwaitingTower || draftPanel.IsAwaitingPremiumTarget))
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

		if (CurrentPhase == GamePhase.Draft && draftPanelReady && !draftPanel!.IsAwaitingSlot && !draftPanel.IsAwaitingTower && !draftPanel.IsAwaitingPremiumTarget)
			UpdateDraftSynergyHighlights((float)delta);
		else
			ClearDraftSynergyHighlights();

		if (CurrentPhase == GamePhase.Wave && _botRunner == null)
		{
			_spectacleSystem.Update((float)delta);
			UpdateExplosionResidues((float)delta);
			if (_globalReadyWindowOpen && _spectacleSystem.IsGlobalSurgeReady && !GetTree().Paused)
				_runState.SurgeHintTelemetry.GlobalReadyUnusedSeconds += (float)delta;
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
			string surgePreview = "";
			float previewAlpha = 0f;
			string[] peekMods = System.Array.Empty<string>();
			// Ghost Global Surge label materialises from 70% fill, fully opaque at 100%.
			if (globalFill >= 0.70f && _botRunner == null)
			{
				peekMods = _spectacleSystem.PeekDominantMods();
				surgePreview = SurgeDifferentiation.ResolveLabel(peekMods);
				previewAlpha = Mathf.Clamp((globalFill - 0.70f) / 0.30f, 0f, 1f) * 0.80f;
			}
			// Screen-edge shimmer intensifies late in the global meter buildup.
			if (_botRunner == null && !_autoDraftMode && GodotObject.IsInstanceValid(_vignetteRect))
			{
				float vigIntensity = Mathf.Clamp((globalFill - 0.78f) / 0.22f, 0f, 1f);
				if (vigIntensity > 0f)
				{
					Color vigColor = peekMods.Length > 0
						? ResolveSpectacleColor(peekMods[0])
						: new Color(1f, 0.90f, 0.56f);
					_vignetteRect.Visible = true;
					var vmat = (ShaderMaterial)_vignetteRect.Material;
					float eased = Mathf.Pow(vigIntensity, 1.25f);
					float chaosGuard = Mathf.Lerp(1f, 0.68f, CombatVisualChaosLoad);
					if (IsNonCriticalHintSuppressed())
						chaosGuard *= 0.86f;
					vmat.SetShaderParameter("intensity", eased * 0.52f * chaosGuard);
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
				surgePreview,
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
			bool inPlacement = IsDraftPlacementActive();
			if (hudPanelReady)
				hudPanel!.SetGlobalSurgeInteractionEnabled(!inPlacement);
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

		int livesBefore = _runState.Lives;
		var result = _combatSim.Step((float)delta, _runState, _waveSystem);
		int alive = _runState.EnemiesAlive.Count;
		int unspawned = Mathf.Max(0, _waveSystem.GetTotalCount() - _runState.EnemiesSpawnedThisWave);
		int remaining = alive + unspawned;
		if (hudPanelReady)
		{
			hudPanel!.Refresh(_runState.WaveIndex + 1, _runState.Lives);
			hudPanel.RefreshTime(_runState.TotalPlayTime);
			hudPanel.RefreshEnemies(alive, remaining);
		}
		if (_tutorialManager != null
			&& _runState.WaveIndex == 0
			&& !_tutorialManager.SpeedPanelShown
			&& result == WaveResult.Ongoing
			&& remaining > 0
			&& remaining <= 5)
		{
			_tutorialManager.MarkSpeedPanelShown();
			ShowTutorialSpeedButtonPanel();
		}
		if (_runState.Lives < livesBefore)
		{
			if (hudPanelReady)
				hudPanel!.FlashLives();
			ShakeWorld();
			MobileOptimization.HapticStrong();
			ScreenFilter.Instance?.FlashVhs(0.45f);
			if (_runState.Lives <= 2)
				ShowClutchToast(_runState.Lives <= 1 ? "TOO CLOSE" : "CLUTCH");
			MusicDirector.Instance?.OnLivesChanged(_runState.Lives);
		}
		UpdateLowLivesTension((float)delta);

		if (result == WaveResult.Loss)
		{
			CurrentPhase = GamePhase.Loss;
			MobileRunSession.Clear();
			MusicDirector.Instance?.OnRunEnd(won: false);
			MobileOptimization.HapticStrong();
			var newlyUnlocked = AchievementManager.Instance?.CheckRunEndAndCollectUnlocks(
				_runState,
				SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
				won: false,
				isTutorialRun: _tutorialManager != null) ?? System.Array.Empty<string>();
			int livesLost = _runState.MaxLives - _runState.Lives;
			SoundManager.Instance?.Play("game_over");
				string runName = BuildRunName(registerInHistory: true, wonOverride: false, waveReachedOverride: _runState.WaveIndex + 1);
			var runColors = BuildRunNameColors();
			string mvpLine = BuildMvpLine();
			string modLine = BuildMostValuableModLine();
			// Endless runs get won=true - the player did beat wave 20 to get here.
			// This ensures endless scores always beat a base wave-20 win, scaled by depth.
			var scorePayload = BuildRunScorePayload(won: _runState.IsEndlessMode, waveReached: _runState.WaveIndex + 1, buildName: runName);
			var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
			string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
			_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
			ApplySurgeProfileToEndScreen();
			if (_tutorialManager != null) _endScreen.SetTutorialMode(true);
			_endScreen.ShowLoss(_runState.WaveIndex + 1, livesLost, _runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), _runState, runName, mvpLine, modLine, runColors.start, runColors.end, _mapTotalWaves);
			_screenshotPipeline?.NotifyLossScreen();
			if (_autoDraftMode) AutoDraftScheduleNextRun();
			_endScreen.SetLeaderboardStatus(leaderboardLine);
			string? surgeLossHint = FinalizeSurgeHintingRun(won: false);
			var lossGoalHint = AchievementManager.Instance?.GetGoalHint(_runState, scorePayload.Difficulty, won: false);
			string? mergedLossHint = MergeEndScreenHints(surgeLossHint, lossGoalHint);
			if (!string.IsNullOrEmpty(mergedLossHint)) _endScreen.SetGoalHint(mergedLossHint);
			if (CampaignManager.IsCampaignRun) _endScreen.SetCampaignMode(null);
			QueueGlobalSubmit(scorePayload, leaderboardLine);
			EnqueueUnlockReveals(newlyUnlocked);
			return;
		}

		if (result == WaveResult.WaveComplete)
		{
			// Complete wave tracking for next draft panel display
			var waveReport = _runState.CompleteWave();
			
			_runState.WaveIndex++;
			if (_runState.WaveIndex >= _mapTotalWaves && !_runState.IsEndlessMode)
			{
				CurrentPhase = GamePhase.Win;
				// Tutorial completion
				if (_tutorialManager != null)
					SettingsManager.Instance?.MarkTutorialCompleted();
				if (_tutorialManager != null)
					AchievementManager.Instance?.CheckTutorialComplete();
				MobileRunSession.Clear();
				MusicDirector.Instance?.OnRunEnd(won: true);
				MobileOptimization.HapticStrong();
				var newlyUnlocked = AchievementManager.Instance?.CheckRunEndAndCollectUnlocks(
					_runState,
					SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
					won: true,
					isTutorialRun: _tutorialManager != null) ?? System.Array.Empty<string>();
				SoundManager.Instance?.Play("victory");
					string runName = BuildRunName(registerInHistory: true, wonOverride: true, waveReachedOverride: _mapTotalWaves);
				var runColors = BuildRunNameColors();
				string mvpLine = BuildMvpLine();
				string modLine = BuildMostValuableModLine();
				var scorePayload = BuildRunScorePayload(won: true, waveReached: _mapTotalWaves, buildName: runName);
				var localSubmit = HighScoreManager.Instance?.SubmitLocal(scorePayload);
				string leaderboardLine = BuildInitialLeaderboardLine(scorePayload, localSubmit);
				_endScreen.SetLeaderboardContext(scorePayload.MapId, scorePayload.Difficulty);
				ApplySurgeProfileToEndScreen();
				if (_tutorialManager != null) _endScreen.SetTutorialMode(true);
			_endScreen.ShowWin(_runState.TotalKills, _runState.TotalDamageDealt, _runState.TotalPlayTime, BuildBuildSummary(), runName, mvpLine, modLine, runColors.start, runColors.end, _runState.Lives, _mapTotalWaves, _runState.MaxLives);
			_screenshotPipeline?.NotifyWinScreen();
				if (_tutorialManager == null) _endScreen.SetLeaderboardStatus(leaderboardLine);
				FinalizeSurgeHintingRun(won: true);
				if (_autoDraftMode) AutoDraftScheduleNextRun();
				var winGoalHint = AchievementManager.Instance?.GetGoalHint(_runState, scorePayload.Difficulty, won: true);
				if (!string.IsNullOrEmpty(winGoalHint)) _endScreen.SetGoalHint(winGoalHint);
				HandleCampaignStageWin(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);
				// Defer global submit: stored and submitted when player exits win screen.
				// If they choose Continue-Endless instead, the win entry is discarded.
				_pendingWinScorePayload    = scorePayload;
				_pendingWinLeaderboardLine = leaderboardLine;
				EnqueueUnlockReveals(newlyUnlocked);
			}
			else
			{
				if (_runState.IsEndlessMode) _runState.EndlessWaveDepth++;
				if (_botRunner == null) ShowWaveClearFlash();
				MobileOptimization.HapticMedium();
				SoundManager.Instance?.Play("wave_clear");
				MusicDirector.Instance?.OnWaveClear();
				AchievementManager.Instance?.CheckAnnihilator(_runState);
				if (_runState.IsEndlessMode)
					AchievementManager.Instance?.CheckEndlessMilestones(_runState);
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
		if (!_runAbandoned && !_exitSnapshotAlreadySaved)
			SaveMobileRunSnapshot("exit_tree");
		if (_tutorialCallout != null && GodotObject.IsInstanceValid(_tutorialCallout))
			_tutorialCallout.QueueFree();
		// Generate gallery if screenshot pipeline was active (handles manual window close)
		if (_botRunner == null)
			_screenshotPipeline?.FinalizeSession();
		if (ReferenceEquals(Instance, this))
			Instance = null!;
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
				case Unlocks.AccordionEngineAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.AccordionEngineTowerId));
					break;
				case Unlocks.BlastCoreAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.BlastCoreModifierId));
					break;
				case Unlocks.WildfireAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.WildfireModifierId));
					break;
				case Unlocks.AfterimageAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.AfterimageModifierId));
					break;
				case Unlocks.PhaseSplitterAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.PhaseSplitterTowerId));
					break;
				case Unlocks.ReaperProtocolAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.ReaperProtocolModifierId));
					break;
				case Unlocks.RocketLauncherAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.RocketLauncherTowerId));
					break;
				case Unlocks.UndertowEngineAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.UndertowEngineTowerId));
					break;
				case Unlocks.LatchNestAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: true, Unlocks.LatchNestTowerId));
					break;
				case Unlocks.DeadzoneAchievementId:
					_pendingUnlockReveals.Enqueue(new UnlockRevealRequest(IsTower: false, Unlocks.DeadzoneModifierId));
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
		if (!MobileRunSession.TryLoad(out var snapshot))
			return;

		_mobileResumeSnapshot = snapshot;
		_runState.SelectedMapId = string.IsNullOrWhiteSpace(snapshot.MapId)
			? LeaderboardKey.RandomMapId
			: snapshot.MapId;
		_runState.RngSeed = snapshot.RngSeed;
		_runState.WaveIndex = Mathf.Clamp(snapshot.WaveIndex, 0, Balance.TotalWaves - 1);
		_runState.MaxLives = Balance.GetStartingLives(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);
		_runState.LivesCeiling = Mathf.Max(Balance.ReaperMaxLives, snapshot.LivesCeiling);
		_runState.Lives = Mathf.Clamp(snapshot.Lives, 0, _runState.LivesCeiling);
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

		PrepareVisualsForImmediateRunStart();

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
					if (tower.Modifiers.Count >= tower.MaxModifiers)
						break;
					if (string.IsNullOrWhiteSpace(modifierId))
						continue;
					_draftSystem.ApplyModifier(modifierId, tower);
				}

				if (tower is TowerInstance visualTower)
					visualTower.RebuildEvolutionVisuals(allowTransition: false);

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

		// Restore the exact draft options that were showing - prevents reload-to-reroll exploit
		if (snapshot.Phase == "draft" && snapshot.DraftOptions.Count > 0)
		{
			_currentDraftOptions = snapshot.DraftOptions
				.Select(o => new DraftOption(
					o.Type == "tower" ? DraftOptionType.Tower : o.Type == "premium" ? DraftOptionType.Premium : DraftOptionType.Modifier,
					o.Id,
					o.IsVolatile,
					o.VolatileRuleId))
				.ToList();
		}

		_lastWaveReport = null;
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);

		if (snapshot.Phase == "wave")
		{
			if (!TryResumeWaveFromSnapshot(snapshot))
			{
				GD.Print("[MobileSession] Wave runtime snapshot missing/invalid - restarting current wave.");
				StartWavePhase();
			}
		}
		else
			StartDraftPhase();

		SaveMobileRunSnapshot("restored");
		GD.Print("[MobileSession] Restored active run snapshot.");
		return true;
	}

	private void PrepareVisualsForImmediateRunStart()
	{
		if (_gridBackground != null)
		{
			_gridBackground.Visible = true;
			_gridBackground.QueueRedraw();
		}

		if (_pathLines != null && _fullPathPoints != null)
		{
			SetPathRevealProgress(1f);
			_pathFlow?.Initialize(_fullPathPoints);
			foreach (var line in _pathLines)
			{
				if (!GodotObject.IsInstanceValid(line))
					continue;
				line.Visible = true;
				line.QueueRedraw();
			}
		}

		for (int i = 0; i < Balance.SlotCount; i++)
		{
			if (!GodotObject.IsInstanceValid(_slotNodes[i]))
				continue;
			_slotNodes[i].Position = _currentMap.Slots[i];
			_slotNodes[i].Scale = Vector2.One;
		}

		CenterWorldNode();
		CallDeferred(MethodName.CenterWorldNode);
	}

	private bool TryResumeWaveFromSnapshot(MobileRunSnapshot snapshot)
	{
		if (snapshot.WaveRuntime == null)
			return false;

		ClearUndoPlacementState();
		_currentDraftOptions = null;
		CurrentPhase = GamePhase.Wave;
		_runState.StartNewWave(_runState.WaveIndex + 1);

		_waveSystem.LoadWave(_runState.WaveIndex, _runState);
		_combatSim.ResetForWave(_waveSystem);
		if (!_combatSim.RestoreWaveRuntimeSnapshot(_runState, snapshot.WaveRuntime))
			return false;

		SoundManager.Instance?.EnsureBackgroundMusicStarted();
		MusicDirector.Instance?.OnWaveStart(_runState.WaveIndex + 1, _runState.Lives);

		int alive = _runState.EnemiesAlive.Count;
		int unspawned = Mathf.Max(0, _waveSystem.GetTotalCount() - _runState.EnemiesSpawnedThisWave);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		_hudPanel.RefreshEnemies(alive, alive + unspawned);
		string runName = BuildRunName();
		var runColors = BuildRunNameColors();
		_hudPanel.SetBuildName(runName, visible: true, startColor: runColors.start, endColor: runColors.end);
		_screenshotPipeline?.NotifyWaveStart(_runState.WaveIndex);
		return true;
	}

	private void SaveMobileRunSnapshot(string reason)
	{
		if (!MobileRunSession.IsActiveRunPhase(CurrentPhase))
			return;

		MobileWaveRuntimeSnapshot? waveRuntime = CurrentPhase == GamePhase.Wave
			? _combatSim.CaptureWaveRuntimeSnapshot(_runState)
			: null;
		MobileRunSession.Save(CurrentPhase, _runState, _extraPicksRemaining,
			CurrentPhase == GamePhase.Draft ? _currentDraftOptions : null,
			waveRuntime);
		if (_botRunner == null)
			GD.Print($"[MobileSession] Snapshot saved ({reason}).");
	}

	public void PersistRunSnapshotForPauseExit(string reason = "pause_exit")
	{
		SaveMobileRunSnapshot(reason);
		_exitSnapshotAlreadySaved = true;
	}

	public void StartDraftPhase()
	{
		if (_restartInProgress) return;  // stale timer/deferred call during restart animation
		SoundManager.Instance?.EnsureBackgroundMusicStarted();
		if (!_initialDraftMusicPrimed && _runState.WaveIndex == 0)
		{
			MusicDirector.Instance?.OnInitialDraftStart(_runState.Lives);
			_initialDraftMusicPrimed = true;
		}
		_tutorialManager?.DismissCallouts();
		ClearUndoPlacementState();
		_hudPanel?.RefreshEnemies(0, 0);  // hide enemy counter during draft
		CurrentPhase = GamePhase.Draft;
		if (_botRunner == null) _screenshotPipeline?.NotifyDraftScreen();
		// Music continues uninterrupted through the draft phase.
		_hudPanel?.SetBuildName("", visible: false);
		// Tutorial: inject scripted picks for this wave (overrides random generation)
		if (_currentDraftOptions == null && _tutorialManager != null)
			_currentDraftOptions = _tutorialManager.GetScriptedOptions(_runState.WaveIndex);

		// Use restored options if available (prevents reload-to-reroll), otherwise generate fresh
		var options = _currentDraftOptions ?? _draftSystem.GenerateOptions(_runState);
		_currentDraftOptions = options;

		if (_botRunner?.IsTowerSurgeBenchmark == true)
		{
			if (!TryApplyTowerSurgeBenchmarkLoadout())
				GD.PrintErr("[BOT] Tower surge benchmark: failed to apply fixed loadout; continuing with current state.");
			_currentDraftOptions = null;
			StartWavePhase();
			return;
		}

		// Fire tutorial callout for this draft wave
		_tutorialManager?.OnDraftOpened(_runState.WaveIndex);

		// All slots full AND all towers at modifier cap ? nothing to offer, skip draft.
		if (options.Count == 0)
		{
			_currentDraftOptions = null;
			GD.Print("No draft options available - skipping draft.");
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

		// Auto-draft: show panel briefly for screenshot capture, then pick automatically.
		if (_autoDraftMode && _autoDraftBot != null)
		{
			var optionsSnap = options;
			GetTree().CreateTimer(0.7f).Timeout += () =>
			{
				if (CurrentPhase != GamePhase.Draft) return;
				_draftPanel.Hide();  // panel hides itself in the normal click flow; we do it manually here
				var pick = _autoDraftBot!.Pick(optionsSnap, _runState);
				if (pick != null) OnDraftPick(pick.Option, pick.SlotIndex);
				else              StartWavePhase();
			};
		}
	}

	public RunState GetRunState() => _runState;
	public string GetCurrentRunName() => BuildRunName();

	private bool TryApplyTowerSurgeBenchmarkLoadout()
	{
		if (_botRunner == null || !_botRunner.IsTowerSurgeBenchmark || _runState == null)
			return false;

		// Loadout is already in place from wave 1 onward; benchmark skips all further draft picks.
		if (_runState.Slots.Any(s => s.Tower != null))
			return true;

		int slotIndex = Mathf.Clamp(_botRunner.BenchmarkSlotIndex, 0, _runState.Slots.Length - 1);
		if (_runState.Slots[slotIndex].IsLocked)
		{
			int fallback = System.Array.FindIndex(_runState.Slots, s => !s.IsLocked && s.Tower == null);
			if (fallback < 0)
				return false;
			slotIndex = fallback;
		}

		string towerId = _botRunner.CurrentBenchmarkTowerId;
		if (string.IsNullOrWhiteSpace(towerId))
			return false;

		PlaceTower(towerId, slotIndex);
		ITowerView? tower = _runState.Slots[slotIndex].Tower;
		if (tower == null)
			return false;
		if (tower is TowerInstance placedTower && placedTower.TowerId == "rift_prism")
		{
			// Rift Prism retarget labels: Strongest == Closest, LowestHp == Furthest.
			placedTower.TargetingMode = TargetingMode.Strongest;
			if (GodotObject.IsInstanceValid(placedTower.ModeIconControl))
				placedTower.ModeIconControl.Mode = TargetingMode.Strongest;
		}

		foreach (string modId in _botRunner.BenchmarkModifierOrder)
		{
			if (string.IsNullOrWhiteSpace(modId))
				continue;
			_draftSystem.ApplyModifier(modId, tower);
		}

		if (tower is TowerInstance visualTower)
			visualTower.RebuildEvolutionVisuals(allowTransition: false);
		RefreshModPips(slotIndex);
		return true;
	}

	/// <summary>Wipe all in-flight state and restart from wave 1 draft.</summary>
	private void OnContinueEndlessPressed()
	{
		AchievementManager.Instance?.CheckKeepGoing();
		// Discard the deferred win score - the endless result will replace it.
		_pendingWinScorePayload    = null;
		_pendingWinLeaderboardLine = "";
		_runState.IsEndlessMode    = true;
		_runState.EndlessWaveDepth = 1;
		_extraPicksRemaining       = 0;
		_hudPanel.SetEndlessMode(true);
		SoundManager.Instance?.FadePad(2.5f);  // re-silence background music that OnRunEnd faded in
		MusicDirector.Instance?.OnEndlessContinue(_runState.WaveIndex + 1, _runState.Lives);
		StartDraftPhase();
	}

	private void AutoDraftScheduleNextRun()
	{
		_autoDraftRunsDone++;
		GD.Print($"[AUTO-DRAFT] Run {_autoDraftRunsDone}/{_autoDraftRunsTotal} complete.");
		if (_autoDraftRunsDone >= _autoDraftRunsTotal)
		{
			GetTree().CreateTimer(3.5f).Timeout += () =>
			{
				_screenshotPipeline?.FinalizeSession();
				GD.Print("[AUTO-DRAFT] All runs complete. Quitting.");
				GetTree().Quit();
			};
		}
		else
		{
			// Brief pause so win/loss screen is visible for screenshot, then restart.
			GetTree().CreateTimer(3.5f).Timeout += () => RestartRun();
		}
	}

	private void OnWinExited()
	{
		if (_pendingWinScorePayload == null) return;
		QueueGlobalSubmit(_pendingWinScorePayload, _pendingWinLeaderboardLine);
		_pendingWinScorePayload    = null;
		_pendingWinLeaderboardLine = "";
	}

	public void RestartRun()
	{
		if (_restartInProgress) return;
		_restartInProgress = true;
		CurrentPhase = GamePhase.Boot;  // block OnDraftPick clicks during restart animations
		_draftPanel.Hide();             // hide immediately; StartDraftPhase re-shows after animations
		ClearTowerPlacementPreviewGhost();
		ClearModifierPreviewGhost();

		_pendingWinScorePayload    = null;
		_pendingWinLeaderboardLine = "";
		MobileRunSession.Clear();

		// Free all enemies currently in the scene
		foreach (var e in _runState.EnemiesAlive)
		{
			if (GodotObject.IsInstanceValid(e)) e.QueueFree();
		}

		// Remove tower nodes from slot scene nodes - use Free() so ClearSlotVisuals
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
		_currentDraftOptions = null;
		_runState.MaxLives = Balance.GetStartingLives(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);
		_runState.Reset();
		RunState? runState = _runState;
		if (runState == null)
		{
			GD.PrintErr("[RUN] RestartRun aborted: run state is null.");
			_restartInProgress = false;
			return;
		}
		_combatSim.ResetForWave();
		_endScreen.Visible = false;
		_pendingUnlockReveals.Clear();
		if (GodotObject.IsInstanceValid(_unlockRevealScreen))
			_unlockRevealScreen.Visible = false;
		Engine.TimeScale = 1.0;   // always reset speed on new run
		SoundManager.Instance?.SetMusicTension(0f);
		SoundManager.Instance?.FadePad(2.5f);  // re-silence pad if OnRunEnd had faded it back in
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
		_surgeArchetypeCounts.Clear();
		_comboTowersSeenThisRun.Clear();
		_surgeHintRuntime.Reset();
		_surgeHintLastMeterByTower.Clear();
		_runBuildIdentity = "UNFORMED";
		_runBuildIdentityFormed = false;
		_firstTowerSurgeAssistShown = false;
		_tipSeenProcFeed = false;
		_tipSeenTowerSurge = false;
		_tipSeenGlobalReady = false;
		_runIdentityPressureScore = 0;
		_runIdentityChainScore = 0;
		_runIdentityDetonationScore = 0;
		_activeProcFeedPips = 0;
		_globalReadyWindowOpen = false;
		_globalReadySincePlayTime = 0f;
		_globalActivateHintToken++;
		_nonCriticalHintSuppressUntilUsec = 0;
		_initialDraftMusicPrimed = false;
		_spectacleSystem.Reset();
		if (_screenshotPipeline != null)
			_screenshotPipeline.Initialize(runState, _screenshotOutputDir, (_botRunner?.CompletedRuns ?? 0));
		if (_botRunner != null)
			ResetBotTraceBuffer();
		ClearUndoPlacementState();
		int hudMaxLives = runState.MaxLives;
		_hudPanel.Refresh(1, hudMaxLives);
		_hudPanel.SetBuildName("", visible: false);
		_hudPanel.ResetSpeed();
		_hudPanel.SetGlobalSurgeReady(false);
		_hudPanel.SetPersistentSurgeHint(null);
		_hudPanel.SetNonCriticalHintSuppressed(false);
		_hudPanel.SetRunBuildIdentity("UNFORMED", formed: false, accent: new Color(0.62f, 0.86f, 1.00f));

			// In bot/auto-draft mode re-apply the pending map since _Ready() only runs once.
			if (_botRunner != null || _autoDraftMode)
			{
				runState.SelectedMapId = SlotTheory.UI.MapSelectPanel.PendingMapSelection;
				if (runState.SelectedMapId == "random_map" && _botRunner != null)
					runState.RngSeed = ResolveBotProceduralSeed();
			}
			else if (runState.SelectedMapId == "random_map")
			{
				// Fresh procedural seed on player restarts for random_map.
				runState.RngSeed = (int)(System.Environment.TickCount64 & 0x7FFFFFFF);
			}

			_mapTotalWaves = ResolveMapTotalWaves(runState.SelectedMapId);
			_hudPanel.SetTotalWaves(_mapTotalWaves);
			_hudPanel.SetDifficulty(SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal);

			ClearMapVisuals();
			GenerateMap();
			SetupMobileCamera();
			CenterWorldNode();
			CallDeferred(MethodName.CenterWorldNode); // re-run after layout settles
			ClearSlotVisuals();
			SetupSlots();

		_extraPicksRemaining = Balance.ExtraPicksForWave(0);
		
		// Force garbage collection in bot mode every 25 runs to prevent memory accumulation
		if (_botRunner != null)
		{
			int completedRuns = _botRunner.CompletedRuns;
			int gcInterval = _botFastMetrics ? 100 : 25;
			if (completedRuns % gcInterval == 0 && completedRuns > 0)
			{
				GD.Print($"[MEMORY] Forcing GC after {completedRuns} completed runs");
				System.GC.Collect();
				System.GC.WaitForPendingFinalizers();
				System.GC.Collect(); // Second collection to clean up objects released after finalization
			}
		}

		AnimateTileDropIn(() => AnimateMapReveal(() => AnimateSlotDropIn(() =>
		{
			_restartInProgress = false;
			StartDraftPhase();
		})));
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
		if (_botRunner == null || !_botTraceCaptureEnabled)
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
		if (CurrentPhase != GamePhase.Draft) return;
		bool pickApplied = false;

		if (option.Type == DraftOptionType.Tower)
		{
			if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length
				&& _runState.Slots[targetSlotIndex].Tower == null
				&& !_runState.Slots[targetSlotIndex].IsLocked)
			{
				ClearTowerPlacementPreviewGhost();
				PlaceTower(option.Id, targetSlotIndex);
				// Tower placement haptic fired inside PlaceTower
				if (_botRunner != null && _runState.Slots[targetSlotIndex].Tower is TowerInstance botPlacedTower)
				{
					var preferredMode = _botRunner.CurrentBot.GetPreferredTargetingMode(option.Id);
					if (preferredMode.HasValue)
					{
						botPlacedTower.TargetingMode = preferredMode.Value;
						if (GodotObject.IsInstanceValid(botPlacedTower.ModeIconControl))
							botPlacedTower.ModeIconControl.Mode = preferredMode.Value;
					}
				}
				pickApplied = true;
			}
		}
		else if (option.Type == DraftOptionType.Modifier)
		{
			if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length)
			{
				var tower = _runState.Slots[targetSlotIndex].Tower;
				if (tower != null)
				{
					_draftSystem.ApplyModifier(option.Id, tower);
					if (tower is TowerInstance visualTower)
						visualTower.RebuildEvolutionVisuals(allowTransition: _botRunner == null);
					if (_botRunner == null)
					{
						RefreshModPips(targetSlotIndex);
						if (tower.Modifiers.Count >= 2 && _comboTowersSeenThisRun.Add(tower))
						{
							_runState.SurgeHintTelemetry.ComboTowersBuiltThisRun++;
							if (tower is TowerInstance comboTower)
							{
								TryShowSurgeMicroHint(
									SurgeHintId.ComboUnlock,
									"2nd mod adds a surge mutation",
									worldPos: comboTower.GlobalPosition,
									towerForHighlight: comboTower);
							}
						}
					}
					MobileOptimization.HapticLight(); // modifier equipped
					if (_tutorialManager != null && !_tutorialManager.BuildNamePanelShown)
					{
						_tutorialManager.MarkBuildNamePanelShown();
						ShowTutorialBuildNamePanel();
					}
					pickApplied = true;
				}
			}
		}
		else if (option.Type == DraftOptionType.Premium)
		{
			// Expanded Chassis requires a target tower slot; all other premium cards apply globally.
			if (PremiumCardRegistry.RequiresTarget(option.Id))
			{
				if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length)
				{
					var tower = _runState.Slots[targetSlotIndex].Tower;
					if (tower != null && tower.MaxModifiers < Balance.MaxPremiumModSlots)
					{
						ApplyPremiumCard(option.Id, targetSlotIndex);
						MobileOptimization.HapticMedium();
						pickApplied = true;
					}
				}
			}
			else
			{
				ApplyPremiumCard(option.Id, -1);
				MobileOptimization.HapticMedium();
				pickApplied = true;
			}
		}

		if (!pickApplied)
		{
			GD.PrintErr($"[Draft] Ignored invalid pick '{option.Id}' at slot {targetSlotIndex}. Re-opening draft.");
			CallDeferred(nameof(StartDraftPhase));
			return;
		}

		ApplyVolatileCommitmentIfNeeded(option, targetSlotIndex);

		_currentDraftOptions = null; // generate fresh options for the next pick once a pick is applied
		AchievementManager.Instance?.CheckDraftMilestones(_runState);
		AdvanceAfterDraftPickFlow();
	}

	// ── Premium Card application ──────────────────────────────────────────────

	private void ApplyVolatileCommitmentIfNeeded(DraftOption option, int targetSlotIndex)
	{
		if (_runState == null || !option.IsVolatile)
			return;
		if (!VolatileDraftRegistry.TryGetForOption(option, out var def))
			return;

		_runState.PickedVolatileCards.Add(def.Id);
		if (def.Scope == VolatileEffectScope.TargetTower)
		{
			if (targetSlotIndex < 0 || targetSlotIndex >= _runState.Slots.Length)
				return;
			var targetTower = _runState.Slots[targetSlotIndex].Tower;
			if (targetTower == null)
				return;

			_runState.Slots[targetSlotIndex].VolatileRuleId = def.Id;

			if (def.FlatDamageDelta != 0)
				targetTower.BaseDamage += def.FlatDamageDelta;
			if (Mathf.Abs(def.AttackIntervalMultiplier - 1f) > 0.0001f)
				targetTower.AttackInterval *= def.AttackIntervalMultiplier;
			if (Mathf.Abs(def.RangeBonus) > 0.001f)
				targetTower.Range = Mathf.Max(20f, targetTower.Range + def.RangeBonus);
			if (Mathf.Abs(def.ChainRangeBonus) > 0.001f)
				targetTower.ChainRange = Mathf.Max(40f, targetTower.ChainRange + def.ChainRangeBonus);
			if (Mathf.Abs(def.SlowDurationMultiplier - 1f) > 0.0001f)
				_runState.MultiplyTowerSlowDurationMultiplier(targetSlotIndex, def.SlowDurationMultiplier);

			if (targetTower is TowerInstance targetTowerNode)
				targetTowerNode.RefreshRangeCircle();
			if (_botRunner == null)
				_hudPanel.ShowSurgeMicroHint($"{def.Name}: {def.UpsideText}", holdSeconds: 2.0f);
			_hudPanel?.RefreshPremiumCards(_runState.PickedPremiumCards, _runState.PickedVolatileCards);
			return;
		}

		if (def.FlatDamageDelta != 0)
		{
			_runState.BonusDamage += def.FlatDamageDelta;
			foreach (var slot in _runState.Slots)
			{
				if (slot.Tower != null)
					slot.Tower.BaseDamage += def.FlatDamageDelta;
			}
		}

		if (Mathf.Abs(def.AttackIntervalMultiplier - 1f) > 0.0001f)
		{
			_runState.AttackIntervalMultiplier *= def.AttackIntervalMultiplier;
			foreach (var slot in _runState.Slots)
			{
				if (slot.Tower != null)
					slot.Tower.AttackInterval *= def.AttackIntervalMultiplier;
			}
		}

		if (Mathf.Abs(def.RangeBonus) > 0.001f)
		{
			_runState.TowerRangeBonus += def.RangeBonus;
			foreach (var slot in _runState.Slots)
			{
				if (slot.Tower == null)
					continue;
				slot.Tower.Range = Mathf.Max(20f, slot.Tower.Range + def.RangeBonus);
			}
		}

		if (Mathf.Abs(def.ChainRangeBonus) > 0.001f)
		{
			_runState.ChainRangeBonus += def.ChainRangeBonus;
			foreach (var slot in _runState.Slots)
			{
				if (slot.Tower == null)
					continue;
				slot.Tower.ChainRange = Mathf.Max(40f, slot.Tower.ChainRange + def.ChainRangeBonus);
			}
		}

		if (Mathf.Abs(def.SlowDurationMultiplier - 1f) > 0.0001f)
			_runState.SlowDurationMultiplier *= def.SlowDurationMultiplier;

		if (_botRunner == null)
			_hudPanel.ShowSurgeMicroHint($"{def.Name}: {def.UpsideText}", holdSeconds: 2.0f);
		_hudPanel?.RefreshPremiumCards(_runState.PickedPremiumCards, _runState.PickedVolatileCards);
	}

	private void ApplyPremiumCard(string cardId, int targetSlotIndex)
	{
		_runState.PickedPremiumCards.Add(cardId);

		switch (cardId)
		{
			case PremiumCardRegistry.BetterOddsId:
				_runState.BonusDraftCards += Balance.BetterOddsBonusDraftCards;
				break;

			case PremiumCardRegistry.KineticCalibrationId:
				_runState.BonusDamage += Balance.KineticCalibrationBonusDamage;
				foreach (var slot in _runState.Slots)
					if (slot.Tower != null) slot.Tower.BaseDamage += Balance.KineticCalibrationBonusDamage;
				break;

			case PremiumCardRegistry.HotLoadersId:
				float rateBoost = Balance.HotLoadersIntervalMultiplier;
				_runState.AttackIntervalMultiplier *= rateBoost;
				foreach (var slot in _runState.Slots)
					if (slot.Tower != null) slot.Tower.AttackInterval *= rateBoost;
				break;

			case PremiumCardRegistry.ExtendedRailsId:
				_runState.TowerRangeBonus += Balance.ExtendedRailsRangeBonus;
				foreach (var slot in _runState.Slots)
				{
					if (slot.Tower != null)
					{
						slot.Tower.Range += Balance.ExtendedRailsRangeBonus;
						if (slot.Tower is TowerInstance ti) ti.RefreshRangeCircle();
					}
				}
				break;

			case PremiumCardRegistry.MultitargetRelayId:
				_runState.ChainRangeBonus += Balance.MultitargetRelayChainRangeBonus;
				foreach (var slot in _runState.Slots)
					if (slot.Tower != null) slot.Tower.ChainRange += Balance.MultitargetRelayChainRangeBonus;
				break;

			case PremiumCardRegistry.ExpandedChassisId:
				if (targetSlotIndex >= 0 && targetSlotIndex < _runState.Slots.Length)
				{
					var tower = _runState.Slots[targetSlotIndex].Tower;
					if (tower != null)
					{
						tower.MaxModifiers = System.Math.Min(tower.MaxModifiers + 1, Balance.MaxPremiumModSlots);
						if (tower is TowerInstance ti) RefreshModPips(targetSlotIndex);
					}
				}
				break;

			case PremiumCardRegistry.EmergencyReservesId:
				_runState.Lives   += Balance.EmergencyReservesLivesGain;
				_runState.MaxLives = System.Math.Max(_runState.MaxLives, _runState.Lives);
				break;

			case PremiumCardRegistry.HardenedReservesId:
				_runState.LivesCeiling += Balance.HardenedReservesMaxLivesBonus;
				_runState.Lives = System.Math.Min(_runState.Lives + 1, _runState.LivesCeiling);
				break;

			case PremiumCardRegistry.LongFuseId:
				_runState.ExplosionRadiusBonus += Balance.LongFuseRadiusBonus;
				break;

			case PremiumCardRegistry.SignalBoostId:
				_runState.MarkDurationBonus += Balance.SignalBoostMarkDurationBonus;
				break;

			case PremiumCardRegistry.ColdCircuitId:
				_runState.SlowDurationMultiplier *= Balance.ColdCircuitSlowDurationMultiplier;
				break;
		}

		GD.Print($"[Premium] Applied {cardId} (slot {targetSlotIndex})");
		_hudPanel?.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		_hudPanel?.RefreshPremiumCards(_runState.PickedPremiumCards, _runState.PickedVolatileCards);
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
		if (_targetModePanelTower == tower)
			HideTargetModePanel();
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
		if (_runState.Slots[slotIndex].IsLocked) return;

		MobileOptimization.HapticMedium();
		var def = DataLoader.GetTowerDef(towerId);
		var tower = new TowerInstance
		{
			TowerId           = towerId,
			BaseDamage        = def.BaseDamage  + _runState.BonusDamage,
			AttackInterval    = def.AttackInterval * _runState.AttackIntervalMultiplier,
			Range             = def.Range       + _runState.TowerRangeBonus,
			AppliesMark       = def.AppliesMark,
			SplitCount        = def.SplitCount,
			ChainCount        = def.ChainCount,
			ChainRange        = def.ChainRange  + _runState.ChainRangeBonus,
			ChainDamageDecay  = def.ChainDamageDecay,
			ProjectileColor = GetTowerProjectileColor(towerId),
			BodyColor = GetTowerBodyColor(towerId),
		};
		tower.ZIndex = 20;

		bool mobile = MobileOptimization.IsMobile();
		float rangeFillAlpha = mobile ? 0.050f : 0.035f;
		float rangeBorderAlpha = mobile ? 0.16f : 0.11f;
		float rangeBorderWidth = mobile ? 1.25f : 1.05f;

		// Range indicator - semi-transparent fill in tower's body colour
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

		// Range border - white ring outline
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

		// Targeting mode badge - always visible on the right side of each tower.
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

		// Phase Splitter always hits front + back -- targeting mode is not applicable.
		if (tower.TowerId == "phase_splitter")
		{
			modeBadge.Visible = false;
			modeBadgeBorder.Visible = false;
		}

		_slotNodes[slotIndex].AddChild(tower);
		_runState.Slots[slotIndex].Tower = tower;
		tower.SlotIndex = slotIndex;
		tower.RebuildEvolutionVisuals(allowTransition: _botRunner == null);

		// Placement bounce - scale from 0 ? 1.15 ? 1.0
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

	private static Color GetTowerProjectileColor(string towerId) =>
		UIStyle.TowerAccent(towerId, UIStyle.TowerAccentVariant.Projectile);

	private static Color GetTowerBodyColor(string towerId) =>
		UIStyle.TowerAccent(towerId, UIStyle.TowerAccentVariant.Body);

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
		if (CurrentPhase != GamePhase.Wave && (_targetModePanelRoot != null || _targetModePanelTower != null))
			HideTargetModePanel();
		// Draft assignment: click a slot in the world to place tower / assign modifier / target premium
		if (CurrentPhase == GamePhase.Draft && (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower || _draftPanel.IsAwaitingPremiumTarget))
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
			
			if (MobileOptimization.IsMobile() && (_draftPanel.IsAwaitingTower || _draftPanel.IsAwaitingPremiumTarget))
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
			if (HandleTargetModePanelPress(pressPos))
			{
				GetViewport().SetInputAsHandled();
				return;
			}

			for (int i = 0; i < _runState.Slots.Length; i++)
			{
				var tower = _runState.Slots[i].TowerNode;
				if (tower == null) continue;
				var hitRect = new Rect2(tower.GlobalPosition - new Vector2(25f, 25f), new Vector2(50f, 50f));
				if (!hitRect.HasPoint(pressPos)) continue;

				// Phase Splitter always targets front + back, so there is no targeting picker.
				if (tower.TowerId == "phase_splitter")
				{
					if (MobileOptimization.IsMobile())
					{
						_selectedTooltipTower = (_selectedTooltipTower == tower) ? null : tower;
						_selectedTooltipEnemy = null;
						GetViewport().SetInputAsHandled();
					}
					return;
				}

				_selectedTooltipTower = tower;
				_selectedTooltipEnemy = null;
				ShowTargetModePanel(tower, i);
				GetViewport().SetInputAsHandled();
				return;
			}
			if (MobileOptimization.IsMobile())
			{
				// Check if an enemy was tapped to show/dismiss its tooltip
				if (_runState?.EnemiesAlive != null)
				{
					foreach (var enemy in _runState.EnemiesAlive)
					{
						if (!GodotObject.IsInstanceValid(enemy)) continue;
						var eHit = new Rect2(enemy.GlobalPosition - new Vector2(28f, 28f), new Vector2(56f, 56f));
						if (!eHit.HasPoint(pressPos)) continue;
						_selectedTooltipEnemy = (_selectedTooltipEnemy == enemy) ? null : enemy;
						_selectedTooltipTower = null;
						GetViewport().SetInputAsHandled();
						return;
					}
				}
				_selectedTooltipTower = null;
				_selectedTooltipEnemy = null;
			}
		}
	}

	private bool HandleTargetModePanelPress(Vector2 pressPos)
	{
		if (!IsTargetModePanelOpen())
		{
			if (_targetModePanelRoot != null || _targetModePanelTower != null || _targetModePanelOptions.Count > 0)
				HideTargetModePanel();
			return false;
		}

		var tower = _targetModePanelTower!;
		foreach (var option in _targetModePanelOptions)
		{
			if (!option.WorldRect.HasPoint(pressPos))
				continue;

			tower.TargetingMode = option.Mode;
			if (GodotObject.IsInstanceValid(tower.ModeIconControl))
				tower.ModeIconControl.Mode = option.Mode;
			DismissTutorialTargetingPanel();
			HideTargetModePanel();
			return true;
		}

		// Click outside panel closes it without changing mode.
		HideTargetModePanel();
		return true;
	}

	private bool IsTargetModePanelOpen()
	{
		return _targetModePanelRoot != null
			&& _targetModePanelTower != null
			&& GodotObject.IsInstanceValid(_targetModePanelRoot)
			&& GodotObject.IsInstanceValid(_targetModePanelTower);
	}

	private void UpdateTargetModePanelHover()
	{
		if (!IsTargetModePanelOpen() || MobileOptimization.IsMobile())
		{
			HideTargetModeHover();
			return;
		}

		var viewport = GetViewport();
		if (viewport == null || !GodotObject.IsInstanceValid(viewport))
		{
			HideTargetModeHover();
			return;
		}

		Vector2 mouseWorld = ScreenToWorld(viewport.GetMousePosition());
		foreach (var option in _targetModePanelOptions)
		{
			if (!option.WorldRect.HasPoint(mouseWorld))
				continue;

			ShowTargetModeHover(option);
			return;
		}

		HideTargetModeHover();
	}

	private void ShowTargetModeHover(TargetModePanelOption option)
	{
		if (_targetModePanelRoot == null
			|| !GodotObject.IsInstanceValid(_targetModePanelRoot)
			|| _targetModeHoverBg == null
			|| !GodotObject.IsInstanceValid(_targetModeHoverBg)
			|| _targetModeHoverLabel == null
			|| !GodotObject.IsInstanceValid(_targetModeHoverLabel))
			return;

		if (_targetModeHoverText != option.DisplayName)
		{
			_targetModeHoverText = option.DisplayName;
			_targetModeHoverLabel.Text = option.DisplayName;
		}

		int fontSize = _targetModeHoverLabel.GetThemeFontSize("font_size");
		Font? font = _targetModeHoverLabel.GetThemeFont("font");
		Vector2 textSize = font?.GetStringSize(option.DisplayName, HorizontalAlignment.Left, -1, fontSize)
			?? new Vector2(Mathf.Max(18f, option.DisplayName.Length * 7f), 13f);
		float padX = 6f;
		float padY = 4f;
		Vector2 bgSize = textSize + new Vector2(padX * 2f, padY * 2f);
		Vector2 localAnchor = option.WorldRect.Position - _targetModePanelRoot.GlobalPosition + option.WorldRect.Size * 0.5f;
		Vector2 bgPos = localAnchor + new Vector2(-bgSize.X * 0.5f, -bgSize.Y - 6f);

		_targetModeHoverBg.Position = bgPos;
		_targetModeHoverBg.Size = bgSize;
		_targetModeHoverLabel.Position = bgPos + new Vector2(padX, padY - 1f);
		_targetModeHoverBg.Visible = true;
		_targetModeHoverLabel.Visible = true;
	}

	private void HideTargetModeHover()
	{
		_targetModeHoverText = string.Empty;
		if (_targetModeHoverBg != null && GodotObject.IsInstanceValid(_targetModeHoverBg))
			_targetModeHoverBg.Visible = false;
		if (_targetModeHoverLabel != null && GodotObject.IsInstanceValid(_targetModeHoverLabel))
			_targetModeHoverLabel.Visible = false;
	}

	private static string GetTargetModeDisplayName(TowerInstance tower, TargetingMode mode)
	{
		if (tower.TowerId == "rift_prism")
		{
			return mode switch
			{
				TargetingMode.First => "Random",
				TargetingMode.Strongest => "Closest",
				TargetingMode.LowestHp => "Furthest",
				_ => "Random",
			};
		}

		return mode switch
		{
			TargetingMode.First => "First",
			TargetingMode.Strongest => "Strongest",
			TargetingMode.LowestHp => "Lowest HP",
			TargetingMode.Last => "Last",
			_ => "First",
		};
	}

	private void ShowTargetModePanel(TowerInstance tower, int slotIndex)
	{
		HideTargetModePanel();
		if (slotIndex < 0 || slotIndex >= _slotNodes.Length)
			return;
		if (tower.TowerId == "phase_splitter")
			return;

		var slotNode = _slotNodes[slotIndex];
		if (!GodotObject.IsInstanceValid(slotNode))
			return;

		TargetingMode[] modes = tower.TowerId == "rift_prism"
			? RiftSapperTargetingModes
			: StandardTargetingModes;
		float buttonSize = MobileOptimization.IsMobile() ? 26f : 21f;
		float gap = MobileOptimization.IsMobile() ? 5f : 4f;
		float padding = MobileOptimization.IsMobile() ? 5f : 4f;
		float panelWidth = buttonSize + padding * 2f;
		float panelHeight = modes.Length * buttonSize + (modes.Length - 1) * gap + padding * 2f;
		float rightOffset = MobileOptimization.IsMobile() ? 42f : 36f;
		float sideOffset = rightOffset;
		float yOffset = -panelHeight * 0.5f;

		var viewport = GetViewport();
		if (viewport != null && GodotObject.IsInstanceValid(viewport))
		{
			Vector2 viewportSize = viewport.GetVisibleRect().Size;
			Vector2 panelRightWorld = slotNode.GlobalPosition + new Vector2(rightOffset + panelWidth, 0f);
			Vector2 panelRightScreen = WorldToScreen(panelRightWorld);
			if (panelRightScreen.X > viewportSize.X - 8f)
				sideOffset = -(rightOffset + panelWidth);
		}

		Vector2 panelTopLeft = new Vector2(sideOffset, yOffset);
		var root = new Node2D
		{
			Name = "_TargetModePanel",
			ZIndex = 240,
		};
		slotNode.AddChild(root);

		var panelBg = new ColorRect
		{
			Position = panelTopLeft,
			Size = new Vector2(panelWidth, panelHeight),
			Color = new Color(0.02f, 0.04f, 0.11f, 0.92f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		root.AddChild(panelBg);

		var panelBorder = new Line2D
		{
			Points = new[]
			{
				panelTopLeft,
				panelTopLeft + new Vector2(panelWidth, 0f),
				panelTopLeft + new Vector2(panelWidth, panelHeight),
				panelTopLeft + new Vector2(0f, panelHeight),
				panelTopLeft,
			},
			Width = 1.8f,
			DefaultColor = new Color(0.66f, 0.92f, 1.00f, 0.90f),
			Antialiased = true,
		};
		root.AddChild(panelBorder);

		_targetModeHoverBg = new ColorRect
		{
			Visible = false,
			Color = new Color(0.01f, 0.03f, 0.08f, 0.96f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		root.AddChild(_targetModeHoverBg);
		_targetModeHoverLabel = new Label
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.Off,
		};
		UITheme.ApplyFont(_targetModeHoverLabel, semiBold: true, size: 12);
		_targetModeHoverLabel.Modulate = new Color(0.95f, 0.99f, 1.00f, 0.98f);
		root.AddChild(_targetModeHoverLabel);
		_targetModeHoverText = string.Empty;

		TargetModeIconSet iconSet = tower.TowerId == "rift_prism"
			? TargetModeIconSet.RiftSapper
			: TargetModeIconSet.Default;

		for (int i = 0; i < modes.Length; i++)
		{
			TargetingMode mode = modes[i];
			bool active = mode == tower.TargetingMode;
			Vector2 buttonPos = panelTopLeft + new Vector2(padding, padding + i * (buttonSize + gap));
			Vector2 buttonSizeVec = new Vector2(buttonSize, buttonSize);

			var buttonBg = new ColorRect
			{
				Position = buttonPos,
				Size = buttonSizeVec,
				Color = active
					? new Color(0.14f, 0.34f, 0.50f, 0.98f)
					: new Color(0.06f, 0.10f, 0.18f, 0.90f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			root.AddChild(buttonBg);

			var buttonBorder = new Line2D
			{
				Points = new[]
				{
					buttonPos,
					buttonPos + new Vector2(buttonSize, 0f),
					buttonPos + new Vector2(buttonSize, buttonSize),
					buttonPos + new Vector2(0f, buttonSize),
					buttonPos,
				},
				Width = active ? 1.7f : 1.3f,
				DefaultColor = active
					? new Color(0.78f, 0.98f, 1.00f, 0.95f)
					: new Color(0.42f, 0.68f, 0.82f, 0.78f),
				Antialiased = true,
			};
			root.AddChild(buttonBorder);

			float iconInset = Mathf.Max(2f, buttonSize * 0.14f);
			float iconSize = buttonSize - iconInset * 2f;
			var icon = new TargetModeIcon
			{
				Mode = mode,
				IconSet = iconSet,
				IconColor = active
					? new Color(0.95f, 1.00f, 1.00f)
					: new Color(0.79f, 0.89f, 0.98f),
				Position = new Vector2(iconInset, iconInset),
				Size = new Vector2(iconSize, iconSize),
				CustomMinimumSize = new Vector2(iconSize, iconSize),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			buttonBg.AddChild(icon);

			Vector2 worldTopLeft = slotNode.GlobalPosition + buttonPos;
			string displayName = GetTargetModeDisplayName(tower, mode);
			_targetModePanelOptions.Add(new TargetModePanelOption(mode, new Rect2(worldTopLeft, buttonSizeVec), displayName));
		}

		// Keep hover tooltip in the foreground above icon buttons.
		if (_targetModeHoverBg != null && GodotObject.IsInstanceValid(_targetModeHoverBg))
		{
			_targetModeHoverBg.ZIndex = 50;
			root.MoveChild(_targetModeHoverBg, root.GetChildCount() - 1);
		}
		if (_targetModeHoverLabel != null && GodotObject.IsInstanceValid(_targetModeHoverLabel))
		{
			_targetModeHoverLabel.ZIndex = 51;
			root.MoveChild(_targetModeHoverLabel, root.GetChildCount() - 1);
		}

		_targetModePanelRoot = root;
		_targetModePanelTower = tower;
	}

	private void HideTargetModePanel()
	{
		HideTargetModeHover();
		if (_targetModePanelRoot != null && GodotObject.IsInstanceValid(_targetModePanelRoot))
			_targetModePanelRoot.QueueFree();
		_targetModePanelRoot = null;
		_targetModePanelTower = null;
		_targetModePanelOptions.Clear();
		_targetModeHoverBg = null;
		_targetModeHoverLabel = null;
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

	private void CenterWorldNode()
	{
		if (MobileOptimization.IsMobile()) return;
		var vpSize = GetViewport().GetVisibleRect().Size;
		float xOffset = Mathf.Max(0f, (vpSize.X - 1280f) / 2f);
		float yOffset = Mathf.Max(0f, (vpSize.Y - 720f) / 2f);
		_worldNode.Position = new Vector2(xOffset, yOffset);
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
				if (GodotObject.IsInstanceValid(_tooltipTitleLabel))
					UITheme.ApplyFont(_tooltipTitleLabel, semiBold: true, size: _mobileTooltipFontSize + 1);
			}
			if (GodotObject.IsInstanceValid(_tooltipBody))
				_tooltipBody.Position = new Vector2(8f * _mobileTooltipUiScale, 6f * _mobileTooltipUiScale);
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

		// Placement highlight - gold border, invisible until hovered in draft assignment mode
		var hlSq = new[] { new Vector2(-23f,-23f), new Vector2(23f,-23f), new Vector2(23f,23f), new Vector2(-23f,23f), new Vector2(-23f,-23f) };
		var hl = new Line2D { Points = hlSq, Width = 2.5f, DefaultColor = new Color(1f, 0.85f, 0.15f) };
		hl.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(hl);
		_slotHighlights[i] = hl;

		// Preview glow - shown when a modifier is previewed on this slot.
		var previewGlow = new Line2D { Points = hlSq, Width = 4.2f, DefaultColor = new Color(0.45f, 0.95f, 1.00f, 0.92f) };
		previewGlow.Modulate = Colors.Transparent;
		_slotNodes[i].AddChild(previewGlow);
		_slotPreviewGlows[i] = previewGlow;

		// Synergy glow - shown while hovering a modifier card with known tower synergies.
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

			// Mod-count pip - just below slot square, shown only when tower has = 1 modifier
			// Modifier pips - 3 small squares below slot, one per modifier slot
			var pips = new ColorRect[Balance.MaxPremiumModSlots];
			var icons = new ModifierIcon[Balance.MaxPremiumModSlots];
			for (int p = 0; p < Balance.MaxPremiumModSlots; p++)
			{
				float px = (p - (Balance.MaxPremiumModSlots - 1) / 2f) * 9f;
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
				float ix = (p - (Balance.MaxPremiumModSlots - 1) / 2f) * 12f;
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

			// Locked slot overlay (campaign LockedSlots mandate)
			if (_runState.Slots[i].IsLocked)
			{
				var lockFill = new ColorRect
				{
					Color        = new Color(0.0f, 0.0f, 0.0f, 0.72f),
					OffsetLeft   = -20f,
					OffsetTop    = -20f,
					OffsetRight  =  20f,
					OffsetBottom =  20f,
					MouseFilter  = Control.MouseFilterEnum.Ignore,
				};
				_slotNodes[i].AddChild(lockFill);
				var lockLabel = new Label
				{
					Text = "🔒",
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment   = VerticalAlignment.Center,
					Position = new Vector2(-12f, -12f),
					Size     = new Vector2(24f, 24f),
					MouseFilter = Control.MouseFilterEnum.Ignore,
				};
				lockLabel.AddThemeFontSizeOverride("font_size", 14);
				_slotNodes[i].AddChild(lockLabel);
			}

			// Start offscreen above the grid - AnimateSlotDropIn brings them in after path reveal.
			// In bot mode, leave at final position so no animation delay occurs.
			if (_botRunner == null)
				_slotNodes[i].Position = new Vector2(_currentMap.Slots[i].X, _currentMap.Slots[i].Y - 900f);
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
		bool suppressBotLogs = _botRunner?.QuietMode == true;

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
				if (!suppressBotLogs)
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
			if (!suppressBotLogs)
				GD.Print("[GameController] Generated procedural map");
		}

		RenderMap();
		if (LanePath != null)
			LanePath.Curve = BuildCurve(_currentMap.Path);
	}

	private void RenderMap()
	{
		// Permanent dark backdrop - same colour as GridBackground's fill but always visible,
		// so hiding GridBackground doesn't expose the engine clear colour.
		const float ext = 4096f;
		_mapVisuals.AddChild(new ColorRect
		{
			Position = new Vector2(-ext, -ext),
			Size     = new Vector2(ext * 2f, ext * 2f),
			Color    = new Color(0.04f, 0.00f, 0.10f),
		});
		// Grid lines start hidden; AnimateTileDropIn reveals them when tiles dissolve.
		_gridBackground = new GridBackground { Visible = false };
		_mapVisuals.AddChild(_gridBackground);

		// Procedural scenery props (trees/rocks) are generated but previously not rendered.
		if (_currentMap is ProceduralMap procedural && procedural.Decorations.Length > 0)
		{
			var decoLayer = new MapDecorationLayer();
			decoLayer.Initialize(procedural.Decorations, _runState.RngSeed);
			_mapVisuals.AddChild(decoLayer);
		}

		// Neon path - tuned to keep lane separation dark and reduce red bleed between segments.
		// Points start empty; AnimateMapReveal() fills them in over 0.8s before opening the draft panel.
		_fullPathPoints = _currentMap.Path;
		var seed = new Vector2[] { _fullPathPoints[0], _fullPathPoints[0] }; // Line2D needs ≥2 pts to be valid
		var l0 = new Line2D { Points = seed, Width = 112f, DefaultColor = new Color(0.72f, 0.01f, 0.50f, 0.015f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		var l1 = new Line2D { Points = seed, Width = 70f,  DefaultColor = new Color(0.64f, 0.03f, 0.46f, 0.030f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		var l2 = new Line2D { Points = seed, Width = 46f,  DefaultColor = new Color(0.06f, 0.00f, 0.12f, 0.97f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		var l3 = new Line2D { Points = seed, Width = 14f,  DefaultColor = new Color(1.00f, 0.12f, 0.58f, 0.11f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		var l4 = new Line2D { Points = seed, Width = 2.6f, DefaultColor = new Color(1.00f, 0.27f, 0.70f, 0.78f), JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		_pathLines = new[] { l0, l1, l2, l3, l4 };
		foreach (var line in _pathLines) _mapVisuals.AddChild(line);
		// Path flow arrows - initialized after reveal animation finishes
		_pathFlow = new PathFlow();
		_mapVisuals.AddChild(_pathFlow);
	}

	private void ClearMapVisuals()
	{
		while (_mapVisuals.GetChildCount() > 0)
			_mapVisuals.GetChild(0).Free();
		_pathFlow = null;
		_pathLines = null;
		_fullPathPoints = null;
	}

	private Vector2[] ComputePartialPath(float t)
	{
		if (_fullPathPoints == null || _fullPathPoints.Length < 2) return System.Array.Empty<Vector2>();
		if (t >= 1f) return _fullPathPoints;

		float totalLen = 0f;
		for (int i = 0; i < _fullPathPoints.Length - 1; i++)
			totalLen += _fullPathPoints[i].DistanceTo(_fullPathPoints[i + 1]);

		float target = t * totalLen;
		var pts = new System.Collections.Generic.List<Vector2> { _fullPathPoints[0] };
		float walked = 0f;
		for (int i = 0; i < _fullPathPoints.Length - 1; i++)
		{
			float segLen = _fullPathPoints[i].DistanceTo(_fullPathPoints[i + 1]);
			if (walked + segLen >= target)
			{
				float frac = segLen > 0f ? (target - walked) / segLen : 0f;
				pts.Add(_fullPathPoints[i].Lerp(_fullPathPoints[i + 1], frac));
				break;
			}
			walked += segLen;
			pts.Add(_fullPathPoints[i + 1]);
		}
		return pts.ToArray();
	}

	private void SetPathRevealProgress(float t)
	{
		if (_pathLines == null) return;
		var pts = ComputePartialPath(t);
		foreach (var line in _pathLines)
			if (GodotObject.IsInstanceValid(line))
				line.Points = pts;
	}

	private void AnimateTileDropIn(System.Action onComplete)
	{
		if (_gridBackground != null) _gridBackground.Visible = true;
		onComplete();
	}

	private void AnimateSlotDropIn(System.Action onComplete)
	{
		if (_botRunner != null) { onComplete(); return; }

		const float stagger      = 0.13f;
		const float dropDuration = 0.26f;
		const float bounceUp    = 0.07f;
		const float bounceDown  = 0.05f;
		const float scaleUp     = 0.08f;
		const float scaleDown   = 0.22f;

		float lastLandTime = (Balance.SlotCount - 1) * stagger + dropDuration;

		for (int i = 0; i < Balance.SlotCount; i++)
		{
			int    idx        = i;
			float  finalY     = _currentMap.Slots[i].Y;
			float  delay      = i * stagger;
			float  pitchScale = 0.86f + i * 0.05f;

			// Position: gravity drop + bounce
			var posTween = _slotNodes[i].CreateTween();
			posTween.TweenInterval(delay);
			posTween.TweenProperty(_slotNodes[i], "position:y", finalY + 8f, dropDuration)
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Quad);
			posTween.TweenProperty(_slotNodes[i], "position:y", finalY - 4f, bounceUp)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Sine);
			posTween.TweenProperty(_slotNodes[i], "position:y", finalY, bounceDown)
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Sine);

			// Scale punch on landing + elastic spring back
			var scaleTween = _slotNodes[i].CreateTween();
			scaleTween.TweenInterval(delay + dropDuration);
			scaleTween.TweenCallback(Callable.From(() =>
				SoundManager.Instance?.Play("tower_place", pitchScale: pitchScale)));
			scaleTween.TweenProperty(_slotNodes[i], "scale", new Vector2(1.22f, 1.22f), scaleUp)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Quad);
			scaleTween.TweenProperty(_slotNodes[i], "scale", Vector2.One, scaleDown)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Elastic);
		}

		GetTree().CreateTimer(lastLandTime + bounceUp + bounceDown + scaleDown + 0.15f).Timeout += () =>
			onComplete();
	}

	private void AnimateMapReveal(System.Action onComplete)
	{
		// Skip animation in bot mode or if path wasn't rendered
		if (_botRunner != null || _pathLines == null || _fullPathPoints == null)
		{
			if (_pathLines != null && _fullPathPoints != null)
				SetPathRevealProgress(1f);
			_pathFlow?.Initialize(_fullPathPoints ?? System.Array.Empty<Vector2>());
			onComplete();
			return;
		}

		var tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(SetPathRevealProgress), 0f, 1f, 1.1f)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Cubic);
		tween.TweenCallback(Callable.From(() =>
		{
			SetPathRevealProgress(1f); // ensure final frame is exact
			_pathFlow?.Initialize(_fullPathPoints);
			onComplete();
		}));
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

	// ── Campaign ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Applies the active campaign mandate to RunState before the run begins.
	/// Locks the last N slots for a LockedSlots mandate.
	/// No-op when not a campaign run.
	/// </summary>
	private void ApplyCampaignMandate()
	{
		if (!CampaignManager.IsCampaignRun || CampaignManager.ActiveStage == null)
			return;

		var mandate = CampaignManager.ActiveStage.Mandate;
		_runState.ActiveMandate = mandate;

		if (mandate.Type == MandateType.LockedSlots && mandate.LockedSlotCount > 0)
		{
			int lockFrom = Balance.SlotCount - mandate.LockedSlotCount;
			for (int i = lockFrom; i < Balance.SlotCount; i++)
				_runState.Slots[i].IsLocked = true;
		}
	}

	/// <summary>
	/// Shows the stage intro overlay for a campaign run then calls onDismiss.
	/// For non-campaign runs, calls onDismiss immediately.
	/// </summary>
	private void ShowStageIntroThenContinue(System.Action onDismiss)
	{
		if (!CampaignManager.IsCampaignRun || CampaignManager.ActiveStage == null)
		{
			onDismiss();
			return;
		}

		var stage = CampaignManager.ActiveStage;
		var overlayLayer = new CanvasLayer { Layer = 20 };
		AddChild(overlayLayer);

		// CanvasLayer has no Modulate - use an inner Control as the fade target
		var overlay = new Control();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlayLayer.AddChild(overlay);

		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0f, 0f, 0f, 0.92f);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		overlay.AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.Theme = UITheme.Build();
		overlay.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		vbox.CustomMinimumSize = new Vector2(640f, 0f);
		center.AddChild(vbox);

		var stageNum = new Label
		{
			Text = $"STAGE {stage.StageIndex + 1}",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(stageNum, semiBold: true, size: 16);
		stageNum.Modulate = new Color(0.35f, 0.55f, 0.65f, 0.88f);
		vbox.AddChild(stageNum);

		var nameLabel = new Label
		{
			Text = stage.StageName.ToUpper(),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(nameLabel, semiBold: true, size: 52);
		nameLabel.Modulate = new Color(0.93f, 0.97f, 1.00f);
		vbox.AddChild(nameLabel);

		var subtitleLabel = new Label
		{
			Text = stage.StageSubtitle.ToUpper(),
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(subtitleLabel, semiBold: false, size: 16);
		subtitleLabel.Modulate = new Color(0.40f, 0.78f, 0.92f, 0.80f);
		vbox.AddChild(subtitleLabel);

		var rule = new ColorRect
		{
			Color = new Color(0.24f, 0.52f, 0.64f, 0.44f),
			CustomMinimumSize = new Vector2(0f, 1f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		vbox.AddChild(rule);

		if (!string.IsNullOrEmpty(stage.IntroLine))
		{
			var introLabel = new Label
			{
				Text = stage.IntroLine,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Off,
			};
			UITheme.ApplyFont(introLabel, semiBold: false, size: 18);
			introLabel.Modulate = new Color(0.70f, 0.88f, 0.72f, 0.90f);
			vbox.AddChild(introLabel);
		}

		if (stage.Mandate.IsActive && !string.IsNullOrEmpty(stage.Mandate.DisplayText))
		{
			var mandateLabel = new Label
			{
				Text = stage.Mandate.DisplayText,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			UITheme.ApplyFont(mandateLabel, semiBold: true, size: 15);
			mandateLabel.Modulate = new Color(1.0f, 0.62f, 0.15f, 0.96f);
			vbox.AddChild(mandateLabel);
		}

		var hint = new Label
		{
			Text = "[click or wait]",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", 12);
		hint.Modulate = new Color(0.35f, 0.45f, 0.55f, 0.70f);
		vbox.AddChild(hint);

		overlay.Modulate = new Color(1f, 1f, 1f, 0f);
		var tw = overlay.CreateTween();
		tw.TweenProperty(overlay, "modulate:a", 1f, 0.40f).SetTrans(Tween.TransitionType.Sine);

		bool dismissed = false;
		void Dismiss()
		{
			if (dismissed || !GodotObject.IsInstanceValid(overlayLayer)) return;
			dismissed = true;
			var fadeTw = overlay.CreateTween();
			fadeTw.TweenProperty(overlay, "modulate:a", 0f, 0.35f).SetTrans(Tween.TransitionType.Sine);
			fadeTw.TweenCallback(Callable.From(() => { overlayLayer.QueueFree(); onDismiss(); }));
		}

		bg.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed)
				Dismiss();
		};
		GetTree().CreateTimer(3.8).Timeout += Dismiss;
	}

	/// <summary>
	/// Records campaign stage completion and shows the sector stamp on the end screen.
	/// Also checks for full-campaign achievement unlocks.
	/// </summary>
	private void HandleCampaignStageWin(DifficultyMode difficulty)
	{
		if (!CampaignManager.IsCampaignRun || CampaignManager.ActiveStage == null)
			return;

		var stage = CampaignManager.ActiveStage;
		CampaignProgress.MarkCleared(stage.StageIndex, difficulty);

		int stageCount = DataLoader.GetCampaignStages().Count;
		bool isFinal = stage.StageIndex == stageCount - 1;
		string finalText = isFinal ? DataLoader.GetFinalCompletionText() : "";
		_endScreen.SetCampaignStageStamp(stage.ClearStamp, isFinal, finalText);

		bool allCleared     = true;
		bool allHardCleared = true;
		for (int i = 0; i < stageCount; i++)
		{
			if (!CampaignProgress.IsClearedOnAny(i))        allCleared     = false;
			if (!CampaignProgress.IsCleared(i, DifficultyMode.Hard)) allHardCleared = false;
		}
		if (allCleared)     AchievementManager.Instance?.Unlock("CAMPAIGN_CLEAR");
		if (allHardCleared) AchievementManager.Instance?.Unlock("CAMPAIGN_HARD_CLEAR");

		// Configure end screen: hide standard buttons, show next stage button if not final
		CampaignStageDefinition? nextStage = null;
		if (!isFinal)
		{
			var allStages = DataLoader.GetCampaignStages();
			int nextIndex = stage.StageIndex + 1;
			if (nextIndex < allStages.Count)
				nextStage = new CampaignStageDefinition(allStages[nextIndex]);
		}
		_endScreen.SetCampaignMode(nextStage);
	}

	// ── End Campaign ──────────────────────────────────────────────────────────

	private void SetupTooltip()
	{
		var tooltipLayer = new CanvasLayer { Layer = 5 };
		AddChild(tooltipLayer);

		_tooltipPanel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
		_tooltipPanel.Visible = false;
		tooltipLayer.AddChild(_tooltipPanel);

		_tooltipBody = new VBoxContainer
		{
			Position = new Vector2(8f, 6f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_tooltipBody.AddThemeConstantOverride("separation", 4);
		_tooltipPanel.AddChild(_tooltipBody);

		_tooltipTitleLabel = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.Off,
			Visible = false,
		};
		UITheme.ApplyFont(_tooltipTitleLabel, semiBold: true, size: 14);
		_tooltipTitleLabel.Modulate = new Color(0.95f, 0.99f, 1.00f, 0.98f);
		_tooltipBody.AddChild(_tooltipTitleLabel);

		_tooltipStatsBox = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false,
		};
		_tooltipStatsBox.AddThemeConstantOverride("separation", 4);
		_tooltipBody.AddChild(_tooltipStatsBox);

		_tooltipLabel = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.Off,
		};
		UITheme.ApplyFont(_tooltipLabel, size: 13);
		_tooltipLabel.Modulate = new Color(0.86f, 0.93f, 1.00f, 0.96f);
		_mobileTooltipFontSize = 13;
		_mobileTooltipUiScale = 1f;
		_tooltipBody.AddChild(_tooltipLabel);
		ApplyMobileZoomReadability();
	}

	private bool IsTooltipReady()
	{
		return _tooltipPanel != null
			&& _tooltipBody != null
			&& _tooltipTitleLabel != null
			&& _tooltipStatsBox != null
			&& _tooltipLabel != null
			&& GodotObject.IsInstanceValid(_tooltipPanel)
			&& GodotObject.IsInstanceValid(_tooltipBody)
			&& GodotObject.IsInstanceValid(_tooltipTitleLabel)
			&& GodotObject.IsInstanceValid(_tooltipStatsBox)
			&& GodotObject.IsInstanceValid(_tooltipLabel);
	}

	private void HideTooltip()
	{
		if (_tooltipPanel != null && GodotObject.IsInstanceValid(_tooltipPanel))
			_tooltipPanel.Visible = false;
		_selectedTooltipTower = null;
		_selectedTooltipEnemy = null;
	}

	private void ClearTooltipStatRows()
	{
		if (_tooltipStatsBox == null || !GodotObject.IsInstanceValid(_tooltipStatsBox))
			return;

		foreach (Node child in _tooltipStatsBox.GetChildren())
		{
			_tooltipStatsBox.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void AddTooltipStatRow(StatIconNode.IconType iconType, Color iconColor, string text)
	{
		if (_tooltipStatsBox == null || !GodotObject.IsInstanceValid(_tooltipStatsBox))
			return;

		float iconSize = Mathf.Round(14f * _mobileTooltipUiScale);
		var row = new HBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", Mathf.RoundToInt(7f * _mobileTooltipUiScale));

		var icon = new StatIconNode
		{
			Type = iconType,
			IconColor = iconColor,
			CustomMinimumSize = new Vector2(iconSize, iconSize),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		row.AddChild(icon);

		var label = new Label
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(label, size: _mobileTooltipFontSize);
		label.Modulate = new Color(0.74f, 0.84f, 1.00f);
		row.AddChild(label);

		_tooltipStatsBox.AddChild(row);
	}

	private void BuildTowerTooltipStatRows(ITowerView tower, float effectiveDamage, float effectiveInterval)
	{
		ClearTooltipStatRows();

		float attackSpeed = effectiveInterval > 0.0001f ? 1f / effectiveInterval : 0f;
		AddTooltipStatRow(StatIconNode.IconType.Burst, UITheme.GetStatIconColor(StatIconNode.IconType.Burst), $"{effectiveDamage:0.#} damage");
		AddTooltipStatRow(StatIconNode.IconType.Cadence, UITheme.GetStatIconColor(StatIconNode.IconType.Cadence), $"{attackSpeed:0.##} atk/s");
		AddTooltipStatRow(StatIconNode.IconType.Range, UITheme.GetStatIconColor(StatIconNode.IconType.Range), $"{tower.Range:0} px range");

		if (tower.IsChainTower && tower.ChainCount > 0)
			AddTooltipStatRow(
				StatIconNode.IconType.Chain,
				UITheme.GetStatIconColor(StatIconNode.IconType.Chain),
				$"x{tower.ChainCount} chain bounces ({tower.ChainDamageDecay * 100f:0}% dmg/bounce)");

		if (tower.SplitCount > 0)
			AddTooltipStatRow(
				StatIconNode.IconType.Split,
				UITheme.GetStatIconColor(StatIconNode.IconType.Split),
				$"x{tower.SplitCount + 1} total shots ({tower.SplitCount} split, {Balance.SplitShotDamageRatio * 100f:0}% split dmg)");

		_tooltipStatsBox.Visible = _tooltipStatsBox.GetChildCount() > 0;
	}

	private void ResizeTooltipPanelToContent()
	{
		if (_tooltipPanel == null
			|| _tooltipBody == null
			|| !GodotObject.IsInstanceValid(_tooltipPanel)
			|| !GodotObject.IsInstanceValid(_tooltipBody))
			return;

		_tooltipBody.Position = new Vector2(8f * _mobileTooltipUiScale, 6f * _mobileTooltipUiScale);
		Vector2 contentSize = _tooltipBody.GetCombinedMinimumSize();
		var padding = new Vector2(16f * _mobileTooltipUiScale, 12f * _mobileTooltipUiScale);
		_tooltipPanel.Size = contentSize + padding;
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
			bool towerOk = _selectedTooltipTower != null && GodotObject.IsInstanceValid(_selectedTooltipTower);
			bool enemyOk = _selectedTooltipEnemy != null && GodotObject.IsInstanceValid(_selectedTooltipEnemy);
			if (!towerOk && !enemyOk)
			{
				HideTooltip();
				return;
			}
			mousePos = towerOk ? _selectedTooltipTower!.GlobalPosition : Vector2.Zero;
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
					TargetingMode.Last      => "Last",
					_                       => "First",
				};
			// Effective attack interval: baked (HairTrigger) + runtime hooks (FocusLens)
			float effInterval = tower.AttackInterval;
			foreach (var mod in tower.Modifiers)
				mod.ModifyAttackInterval(ref effInterval, tower);
			// Effective damage: unconditional modifiers (FocusLens) + baked changes
			float effDamage = tower.GetEffectiveDamageForPreview();
			_tooltipTitleLabel.Visible = true;
			_tooltipTitleLabel.Text = tower.TowerId == "phase_splitter"
				? $"Slot {i + 1}  -  {def.Name}"
				: $"Slot {i + 1}  -  {def.Name}  [{targetingName}]";
			BuildTowerTooltipStatRows(tower, effDamage, effInterval);

			var text = string.Empty;
			if (tower.TowerId == "rift_prism")
			{
				text += $"plants up to {Balance.RiftMineMaxActivePerTower} mines  ({Balance.RiftMineChargesPerMine} charges each)\n";
				text += $"trigger {Balance.RiftMineTriggerRadius:0}px  burst seed {Balance.RiftMineBurstWindow:0.#}s (+{Balance.RiftMineBurstFastPlantsPerTower} fast plants)\n";
			}
			if (tower.TowerId == "accordion_engine")
			{
				text += $"compresses enemy spacing on pulse  ({(int)((1f - Balance.AccordionCompressionFactor) * 100)}% spread reduction)\n";
				text += $"min {Balance.AccordionMinSpacingPx:0}px spacing  -  hits all enemies in range\n";
			}
			if (tower.TowerId == "phase_splitter")
			{
				text += $"hits front + back in range at {(int)(Balance.PhaseSplitterDamageRatio * 100)}% damage per target\n";
				text += "strong lane-edge pressure, weaker into dense mid packs\n";
			}
			if (tower.TowerId == "rocket_launcher")
			{
				text += $"explodes on hit: full primary + {(int)(Balance.RocketLauncherSplashDamageRatio * 100)}% splash in {Balance.RocketLauncherSplashRadius:0}px\n";
				text += "Rocket Launcher fires explosive rockets that damage the target and nearby enemies.\n";
				if (tower.Modifiers.Any(m => m.ModifierId == "blast_core"))
					text += "Blast Core further expands the blast radius.\n";
			}
			if (tower.TowerId == "undertow_engine")
			{
				text += $"drags the lead enemy backward for {Balance.UndertowDuration:0.##}s while heavily slowing it\n";
				text += "Undertow Engine drags enemies backward so they spend longer inside your defenses.\n";
			}
			if (tower.TowerId == "latch_nest")
			{
				text += $"primary pod hits attach parasites up to {Balance.LatchNestMaxParasitesPerHost} per host ({Balance.LatchNestMaxActiveParasitesPerTower} total)\n";
				text += $"parasite ticks every {Balance.LatchNestParasiteTickInterval:0.##}s for {(int)(Balance.LatchNestParasiteTickDamageMultiplier * 100)}% secondary-hit damage\n";
				text += "attach impact is primary; parasite ticks are secondary (chain-style) hits\n";
			}
			if (tower.Modifiers.Count == 0)
				text += "(no modifiers)";
			else
				foreach (var mod in tower.Modifiers)
				{
					var mdef = DataLoader.GetModifierDef(mod.ModifierId);
					text += "* " + mdef.Name + " " + mdef.Description + "\n";
				}
			var slotVolatileId = _runState.Slots[i].VolatileRuleId;
			if (slotVolatileId != null && VolatileDraftRegistry.TryGet(slotVolatileId, out var volatileDef))
			{
				if (!text.EndsWith('\n'))
					text += "\n";
				text += $"\n[MUTATION: {volatileDef.Name}]\n";
				text += $"  + {volatileDef.UpsideText}\n";
				text += $"  - {volatileDef.TradeoffText}\n";
			}
			if (!text.EndsWith('\n'))
				text += "\n";
			text += BuildSpectacleTooltipSection(tower);
			_tooltipLabel.Text = text.TrimEnd();
			_tooltipLabel.Visible = _tooltipLabel.Text.Length > 0;
			ResizeTooltipPanelToContent();
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

		// Enemy hover/tap tooltip (wave phase only)
		if (CurrentPhase == GamePhase.Wave)
		{
			EnemyInstance? hoveredEnemy = null;
			if (MobileOptimization.IsMobile())
			{
				if (_selectedTooltipEnemy != null && GodotObject.IsInstanceValid(_selectedTooltipEnemy))
					hoveredEnemy = _selectedTooltipEnemy;
			}
			else
			{
				foreach (var e in _runState.EnemiesAlive)
				{
					if (!GodotObject.IsInstanceValid(e)) continue;
					if (new Rect2(e.GlobalPosition - new Vector2(22f, 22f), new Vector2(44f, 44f)).HasPoint(mousePos))
					{
						hoveredEnemy = e;
						break;
					}
				}
			}
			if (hoveredEnemy != null)
			{
				_tooltipTitleLabel.Visible = false;
				ClearTooltipStatRows();
				_tooltipStatsBox.Visible = false;
				_tooltipLabel.Text = BuildEnemyTooltipText(hoveredEnemy);
				_tooltipLabel.Visible = true;
				ResizeTooltipPanelToContent();
				Vector2 pos;
				if (MobileOptimization.IsMobile())
				{
					var screenAnchor = WorldToScreen(hoveredEnemy.GlobalPosition);
					pos = new Vector2(screenAnchor.X - _tooltipPanel.Size.X * 0.5f, screenAnchor.Y - _tooltipPanel.Size.Y - 14f);
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
		}

		HideTooltip();
	}

	private static string BuildEnemyTooltipText(EnemyInstance enemy)
	{
		string typeName = EnemyCatalog.GetDisplayName(enemy.EnemyTypeId);
		int leakCost = EnemyCatalog.GetLeakCost(enemy.EnemyTypeId);
		float displaySpeed = enemy.IsSlowed ? enemy.Speed * enemy.SlowSpeedFactor : enemy.Speed;
		string speedSuffix = enemy.IsSlowed ? "  (slowed)" : "";
		string statusSuffix = enemy.IsMarked ? "\nMARKED" : "";
		string splitSuffix = enemy.EnemyTypeId == "splitter_walker" ? $"\nSplits into {Balance.SplitterShardCount} shards on death" : "";
		string reverseSuffix = enemy.EnemyTypeId == "reverse_walker"
			? $"\nSingle hit >= {Balance.ReverseWalkerTriggerDamageRatio * 100f:0}% max HP can rewind ({Balance.ReverseWalkerMaxTriggersPerLife}x max)"
			: "";
		string shieldSuffix = enemy.EnemyTypeId == "shield_drone"
			? $"\nShields nearby allies  ({Balance.ShieldDroneAuraRadius:0}px, {Balance.ShieldDroneProtectionReduction * 100f:0}% dmg reduction)"
			: "";
		string anchorSuffix = enemy.EnemyTypeId == EnemyCatalog.AnchorWalkerId
			? "\nStrongly resists pull/compression control effects"
			: "";
		string nullSuffix = enemy.EnemyTypeId == EnemyCatalog.NullDroneId
			? $"\nCleanses nearby allies every {Balance.NullDronePulseInterval:0.0}s ({Balance.NullDronePulseRadius:0}px)"
			: "";
		string lancerSuffix = enemy.EnemyTypeId == EnemyCatalog.LancerWalkerId
			? $"\nDashes forward roughly every {Balance.LancerWalkerDashInterval:0.0}s"
			: "";
		string veilSuffix = enemy.EnemyTypeId == EnemyCatalog.VeilWalkerId
			? $"\nVeil shell {(enemy.VeilShellActive ? "ready" : "recharging")} ({Balance.VeilWalkerShellDamageReduction * 100f:0}% next-hit reduction)"
			: "";
		return $"{typeName}\nHP  {enemy.Hp:0}/{enemy.MaxHp:0}  |  Speed  {displaySpeed:0} px/s{speedSuffix}\nLeak cost  {leakCost} life{(leakCost > 1 ? "s" : "")}{splitSuffix}{reverseSuffix}{shieldSuffix}{anchorSuffix}{nullSuffix}{lancerSuffix}{veilSuffix}{statusSuffix}";
	}

	private string BuildSpectacleTooltipSection(ITowerView tower)
	{
		var supportedMods = tower.Modifiers
			.Select(m => SpectacleDefinitions.NormalizeModId(m.ModifierId))
			.Where(SpectacleDefinitions.IsSupported)
			.Distinct()
			.ToList();

		if (supportedMods.Count == 0)
			return "Surge: (no compatible mods)";

		var sig = _spectacleSystem.PreviewSignature(tower);
		if (string.IsNullOrEmpty(sig.PrimaryModId))
			return "Surge: (no compatible mods)";

		SurgeDifferentiation.TowerSurgeCategory category =
			SurgeDifferentiation.ResolveTowerSurgeCategory(tower.TowerId, sig);
		string categoryLabel = SurgeDifferentiation.GetCategoryCallout(category);

		return $"Surge Category: {categoryLabel}";
	}
	// -- Bot multi-step simulation -------------------------------------------------

	private const float BOT_DT    = 0.05f;
	private const int   BOT_STEPS = 100;
	private const int   BOT_STEPS_FAST = 200;
	private const int   BOT_IN_RANGE_SAMPLE_INTERVAL = 2;

	private void BotTick()
	{
		int stepsPerTick = _botFastMetrics ? BOT_STEPS_FAST : BOT_STEPS;
		for (int i = 0; i < stepsPerTick && CurrentPhase == GamePhase.Wave; i++)
		{
			_botStepExplosionBursts = 0;
			_botStepHitStopRequests = 0;

			// Manually advance enemies (PathFollow2D._Process is disabled in bot mode).
			// No IsInstanceValid check needed: enemies are never freed mid-step in simulation.
			foreach (var enemy in _runState.EnemiesAlive)
			{
				float spd = enemy.IsSlowed ? enemy.Speed * enemy.SlowSpeedFactor : enemy.Speed;
				enemy.Progress     += spd * BOT_DT;
				enemy.AdvanceCombatTimers(BOT_DT);
			}

			_spectacleSystem.Update(BOT_DT);
			UpdateExplosionResidues(BOT_DT);
			var result = _combatSim.Step(BOT_DT, _runState, _waveSystem);
			TryBotActivateGlobalSurge();
			_botWaveSteps++;

			if (!_botFastMetrics && (i % BOT_IN_RANGE_SAMPLE_INTERVAL) == 0)
			{
				// Lightweight diagnostics: sample every N bot-steps instead of every step.
				int inRange = 0;
				foreach (var e in _runState.EnemiesAlive)
					foreach (var sl in _runState.Slots)
						if (sl.Tower != null && sl.Tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= sl.Tower.Range)
						{ inRange++; break; }
				_botWaveInRangeSum     += inRange;
				_botWaveInRangeSamples += 1;
			}
			_runState.TrackFrameStressProxies(_botStepExplosionBursts, _explosionResidues.Count, _botStepHitStopRequests);
			if (result == WaveResult.Loss)
			{
				CurrentPhase = GamePhase.Loss;
				_runState.CompleteWave();
				FinalizeBotRunAndContinue(
					won: false,
					waveReached: _runState.WaveIndex + 1,
					stopReason: _botRunner?.IsTowerSurgeBenchmark == true ? "loss" : null);
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
				if (_botRunner?.IsTowerSurgeBenchmark == true
					&& _runState.WaveIndex >= _botRunner.BenchmarkMaxWaves)
				{
					CurrentPhase = GamePhase.Win;
					FinalizeBotRunAndContinue(
						won: true,
						waveReached: _runState.WaveIndex,
						stopReason: "max_wave_cap_reached");
					return;
				}
				if (_runState.WaveIndex >= Balance.TotalWaves && !_runState.IsEndlessMode)
				{
					CurrentPhase = GamePhase.Win;
					FinalizeBotRunAndContinue(
						won: true,
						waveReached: _runState.WaveIndex,
						stopReason: _botRunner?.IsTowerSurgeBenchmark == true ? "wave_table_completed" : null);
					return;
				}
				_extraPicksRemaining = Balance.ExtraPicksForWave(_runState.WaveIndex);
				StartDraftPhase();
				return;
			}
		}
	}

	private void FinalizeBotRunAndContinue(bool won, int waveReached, string? stopReason = null)
	{
		if (_botRunner == null)
			return;

		if (_botTraceCaptureEnabled)
			_botRunner.RecordRunTrace(_botRunTraceEvents);
		_botRunner.RecordResult(won, waveReached, _runState, stopReason);
		if (_botRunner.HasMoreRuns)
		{
			RestartRun();
			return;
		}

		_screenshotPipeline?.FinalizeSession();
		_botRunner.PrintSummary();
		GetTree().Quit();
	}

	private void TryBotActivateGlobalSurge()
	{
		if (_botRunner == null || _runState == null || CurrentPhase != GamePhase.Wave)
			return;
		if (!_botGlobalSurgePending || !_spectacleSystem.IsGlobalSurgeReady)
			return;

		float readyAgeSeconds = 0f;
		if (_botGlobalSurgeReadyAtPlayTime >= 0f)
			readyAgeSeconds = Mathf.Max(0f, _runState.TotalPlayTime - _botGlobalSurgeReadyAtPlayTime);

		int minCrowdOverride = _botRunner.CurrentBot?.Strategy == BotStrategy.PlayerStyle2 ? 20 : -1;
		var snapshot = new BotGlobalSurgeSnapshot(
			IsGlobalSurgeReady: _spectacleSystem.IsGlobalSurgeReady,
			HasPendingGlobalSurge: _botGlobalSurgePending,
			Lives: _runState.Lives,
			EnemiesAlive: _runState.EnemiesAlive.Count,
			EnemiesSpawnedThisWave: _runState.EnemiesSpawnedThisWave,
			TotalEnemiesThisWave: Mathf.Max(1, _waveSystem.GetTotalCount()),
			ReadyAgeSeconds: readyAgeSeconds,
			MinCrowdOverride: minCrowdOverride);

		if (!BotGlobalSurgeAdvisor.ShouldActivate(snapshot))
			return;

		_botGlobalSurgePending = false;
		_botGlobalSurgeReadyAtPlayTime = -1f;
		_spectacleSystem.ActivateGlobalSurge();
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
		UITheme.ApplyFont(_threatWarn, semiBold: true, size: 22);
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

		// Sustained archetype tint - lingers after global surge, separate from fast flash
		_lingerTint = new ColorRect();
		_lingerTint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lingerTint.Color = new Color(1f, 0.90f, 0.56f, 0f);
		_lingerTint.Visible = false;
		_lingerTint.MouseFilter = Control.MouseFilterEnum.Ignore;
		anchor.AddChild(_lingerTint);

		// Screen-edge shimmer - subtle, glossy buildup toward Global Surge readiness.
		_vignetteRect = new ColorRect();
		_vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_vignetteRect.Visible = false;
		_vignetteRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		var vignetteMat = new ShaderMaterial();
		var vignetteShader = new Shader();
		vignetteShader.Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform vec3 tint : source_color = vec3(1.0, 0.2, 0.1);
void fragment() {
    vec2 centered = UV - vec2(0.5);
    float squareDist = max(abs(centered.x), abs(centered.y)) * 2.0;
    float edge = smoothstep(0.70, 1.05, squareDist);
    float rim = smoothstep(0.62, 1.00, length(centered) * 1.45);
    float shimmer = 0.65 + 0.35 * sin(TIME * 2.6 + (UV.x + UV.y) * 10.0);
    float shine = edge * (0.42 + 0.48 * shimmer) + rim * 0.25;
    vec3 glow = mix(tint * 0.55, vec3(1.0), 0.26 * shine);
    COLOR = vec4(glow, intensity * (0.020 + 0.035 * shine));
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

		// Subtitle line - smaller text below the main banner, shows the gameplay effect.
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
		BeginCriticalFeedbackWindow(0.45f);
		bool isFinalWave = wave >= _mapTotalWaves && !_runState.IsEndlessMode;
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
		BeginCriticalFeedbackWindow(0.95f);
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
		BeginCriticalFeedbackWindow(0.36f);
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

		int aliveThreats = 0;
		float maxProgress = 0f;
		foreach (var enemy in _runState.EnemiesAlive)
		{
			if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f || enemy.ProgressRatio >= 1f)
				continue;
			aliveThreats++;
			if (enemy.ProgressRatio > maxProgress)
				maxProgress = enemy.ProgressRatio;
		}

		// No active threats on lane: keep the heartbeat silent.
		if (aliveThreats == 0)
		{
			_lowLivesHeartbeatTimer = 0f;
			return;
		}

		// Threat ramps as enemies approach leak end of lane.
		float threat = Mathf.InverseLerp(0.45f, 0.95f, maxProgress);
		float cadence = _runState.Lives <= 1
			? Mathf.Lerp(1.28f, 0.76f, threat)
			: Mathf.Lerp(1.70f, 1.04f, threat);
		float pitch = _runState.Lives <= 1
			? Mathf.Lerp(0.88f, 1.03f, threat)
			: Mathf.Lerp(0.84f, 0.98f, threat);

		_lowLivesHeartbeatTimer -= delta;
		if (_lowLivesHeartbeatTimer > 0f) return;

		SoundManager.Instance?.Play("low_heartbeat", pitchScale: pitch);
		_lowLivesHeartbeatTimer = cadence;
	}

	private void ShowClutchToast(string text)
	{
		if (!GodotObject.IsInstanceValid(_clutchToast) || _botRunner != null) return;
		BeginCriticalFeedbackWindow(0.42f);
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
		BeginCriticalFeedbackWindow(1.25f);
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

	private CombatCallout? SpawnCombatCallout(
		string text,
		Vector2 worldPos,
		Color color,
		float durationScale = 1f,
		float yOffset = -16f,
		bool drift = true,
		float holdPortion = 0.42f,
		int sizeOverride = 0)
	{
		if (_botRunner != null
			|| OS.HasFeature("headless")
			|| _worldNode == null
			|| !GodotObject.IsInstanceValid(_worldNode))
			return null;
		var callout = new CombatCallout();
		_worldNode.AddChild(callout);
		callout.ZAsRelative = false;
		callout.ZIndex = 60; // Ensure surge callouts render above lane props like Rift Sapper mines.
		callout.GlobalPosition = worldPos + new Vector2(0f, yOffset);
		callout.Initialize(
			text,
			color,
			duration: 1.35f * Mathf.Max(0.1f, durationScale),
			sizeOverride: sizeOverride,
			driftEnabled: drift,
			holdPortion: holdPortion);
		return callout;
	}

	// ── Surge differentiation helpers ──────────────────────────────────────────
	// Label/feel logic delegated to SurgeDifferentiation (pure, unit-testable).

	/// <summary>
	/// Spawn mode-based signature rings from a position - 1 ring for Single, 2 for Combo, 3 for Triad.
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
	/// Tower-type archetype FX - pattern=tower visual identity.
	/// drama: 1.0 = full (global surge), 0.28 = mini rider on tower surge.
	/// </summary>
	private void SpawnTowerArchetypeFx(TowerInstance tower, Color accent, float drama)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(tower) || !GodotObject.IsInstanceValid(_worldNode))
			return;
		switch (tower.TowerId)
		{
			case "chain_tower":      SpawnArchetypeChainArcs(tower, accent, drama); break;
			case "heavy_cannon":     SpawnArchetypeCannonRing(tower.GlobalPosition, accent, drama); break;
			case "rocket_launcher":  SpawnArchetypeRocketRing(tower.GlobalPosition, accent, drama); break;
			case "rapid_shooter":    SpawnArchetypeSparks(tower.GlobalPosition, accent, drama); break;
			case "marker_tower":     SpawnArchetypeMarkedFlash(accent, drama); break;
			case "rift_prism":       SpawnArchetypeRiftRing(tower.GlobalPosition, accent, drama); break;
			case "accordion_engine": SpawnArchetypeAccordionRing(tower.GlobalPosition, accent, drama); break;
			case "phase_splitter":   SpawnArchetypePhaseRing(tower.GlobalPosition, accent, drama); break;
			case "undertow_engine":  SpawnArchetypeUndertowRing(tower.GlobalPosition, accent, drama); break;
			case "latch_nest":       SpawnArchetypeLatchNestPulse(tower.GlobalPosition, accent, drama); break;
		}
	}

	private void SpawnArchetypeLatchNestPulse(Vector2 worldPos, Color accent, float drama)
	{
		float outer = Mathf.Lerp(72f, 144f, Mathf.Clamp(drama, 0f, 1f));
		float inner = outer * 0.58f;
		float width = Mathf.Lerp(1.8f, 3.6f, Mathf.Clamp(drama, 0f, 1f));
		float duration = Mathf.Lerp(0.22f, 0.34f, Mathf.Clamp(drama, 0f, 1f));
		Color shell = new Color(accent.R * 0.74f + 0.16f, accent.G * 0.94f, accent.B * 0.62f, 0.42f);
		EmitSignatureRing(worldPos, shell, outer, duration, width);
		EmitSignatureRing(worldPos, new Color(shell.R, shell.G, shell.B, 0.28f), inner, duration * 0.84f, width * 0.72f);
	}

	private void SpawnArchetypePhaseRing(Vector2 worldPos, Color accent, float drama)
	{
		float radiusA = Mathf.Lerp(80f, 170f, Mathf.Clamp(drama, 0f, 1f));
		float radiusB = radiusA * 0.62f;
		float duration = Mathf.Lerp(0.22f, 0.34f, Mathf.Clamp(drama, 0f, 1f));
		EmitSignatureRing(worldPos, new Color(accent.R, accent.G, accent.B, 0.84f), radiusA, duration, 3.1f);
		EmitSignatureRing(worldPos, new Color(accent.R, accent.G, accent.B, 0.56f), radiusB, duration * 0.8f, 2.0f);
	}

	private void SpawnArchetypeUndertowRing(Vector2 worldPos, Color accent, float drama)
	{
		float radius = Mathf.Lerp(76f, 168f, Mathf.Clamp(drama, 0f, 1f));
		float duration = Mathf.Lerp(0.24f, 0.36f, Mathf.Clamp(drama, 0f, 1f));
		EmitSignatureRing(worldPos, new Color(accent.R, accent.G, accent.B, 0.86f), radius, duration, 2.8f);
		EmitSignatureRing(worldPos, new Color(accent.R, accent.G, accent.B, 0.54f), radius * 0.62f, duration * 0.86f, 1.8f);
	}

	private void SpawnArchetypeRocketRing(Vector2 worldPos, Color accent, float drama)
	{
		float radius = Mathf.Lerp(84f, 192f, Mathf.Clamp(drama, 0f, 1f));
		float duration = Mathf.Lerp(0.22f, 0.33f, Mathf.Clamp(drama, 0f, 1f));
		EmitSignatureRing(worldPos, new Color(accent.R, accent.G, accent.B, 0.86f), radius, duration, 3.2f);
		EmitSignatureRing(worldPos, new Color(1f, 0.92f, 0.72f, 0.46f), radius * 0.66f, duration * 0.82f, 2.0f);
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

	private void SpawnArchetypeAccordionRing(Vector2 origin, Color accent, float drama)
	{
		// Accordion Engine identity: converging double-ring that contracts inward
		for (int i = 0; i < 2; i++)
		{
			int idx = i;
			float delay = i * 0.08f;
			float radius = 80f + drama * 140f - idx * 22f;  // second ring smaller (inward compression feel)
			float dur = 0.36f + drama * 0.28f;
			float rw = 3.0f + drama * 2.4f - idx * 0.6f;
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

	private void OnSpectacleProcMeterContributed(SpectacleProcContributionInfo info)
	{
		if (CurrentPhase != GamePhase.Wave || _botRunner != null)
			return;
		if (info.Tower is not TowerInstance tower || !GodotObject.IsInstanceValid(tower))
			return;
		if (!info.TriggeredSurge && info.MeterGain < Balance.SurgeFeedMinimumGain)
			return;

		Color accent = ResolveSpectacleColor(info.ModifierId);
		bool reducedMotion = SettingsManager.Instance?.ReducedMotion == true;
		Vector2 towerMeterAnchor = tower.GlobalPosition + new Vector2(0f, -32f);
		if (reducedMotion)
		{
			tower.FlashSpectacle(accent, major: false);
			EmitSignatureRing(tower.GlobalPosition, accent, endRadius: 22f, duration: 0.18f, ringWidth: 2.2f);
			EmitSignatureRing(towerMeterAnchor, accent, endRadius: 15f, duration: 0.16f, ringWidth: 2f);
		}
		else if (_activeProcFeedPips < Balance.ProcFeedPipMaxActive && GodotObject.IsInstanceValid(_pipLayer))
		{
			_activeProcFeedPips++;
			SpawnSurgePip(
				worldPos: tower.GlobalPosition + new Vector2(0f, -8f),
				accent: accent,
				explicitScreenTarget: WorldToScreen(towerMeterAnchor),
				glyph: SurgePip.SurgePipGlyph.Diamond,
				coreScale: 0.76f,
				glowScale: 0.84f,
				lingerSec: 0.06f,
				travelSec: 0.20f,
				arcHeight: 20f,
				onArrival: () =>
				{
					_activeProcFeedPips = Mathf.Max(0, _activeProcFeedPips - 1);
					if (GodotObject.IsInstanceValid(tower))
						tower.TriggerTeachingHighlight(0.18f);
				},
				allowWhenGlobalReady: true);
		}

		if (!_tipSeenProcFeed && _tutorialManager == null)
		{
			_tipSeenProcFeed = true;
			_hudPanel.ShowSurgeMicroHint("Modifiers can power Surges.", holdSeconds: 2.2f);
		}
	}

	private void OnTowerGlobalContribution(TowerGlobalContributionInfo info)
	{
		if (CurrentPhase != GamePhase.Wave || _botRunner != null)
			return;

		float threshold = SpectacleDefinitions.ResolveGlobalThreshold();
		_hudPanel.FlashGlobalContributionChunk(info.MeterBefore, info.MeterAfter, threshold);
		if (info.Tower is not TowerInstance tower || !GodotObject.IsInstanceValid(tower))
			return;

		Color accent = ResolveSpectacleColor(_spectacleSystem.PreviewSignature(tower).PrimaryModId);
		if (SettingsManager.Instance?.ReducedMotion == true)
		{
			tower.FlashSpectacle(accent, major: false);
			_hudPanel.PulseGlobalSurgeMeter(1.1f);
			return;
		}

		SpawnSurgePip(
			worldPos: tower.GlobalPosition,
			accent: accent,
			glyph: SurgePip.SurgePipGlyph.Square,
			coreScale: 1.10f,
			glowScale: 1.08f);
	}

	private void AccumulateRunIdentityFromSurge(SpectacleSignature signature)
	{
		foreach (string modId in new[] { signature.PrimaryModId, signature.SecondaryModId, signature.TertiaryModId })
		{
			if (string.IsNullOrWhiteSpace(modId))
				continue;

			switch (SurgeDifferentiation.ResolveFeelFromMod(modId))
			{
				case SurgeDifferentiation.GlobalSurgeFeel.Pressure:
					_runIdentityPressureScore++;
					break;
				case SurgeDifferentiation.GlobalSurgeFeel.Detonation:
					_runIdentityDetonationScore++;
					break;
				default:
					_runIdentityChainScore++;
					break;
			}
		}
		UpdateRunBuildIdentityHud();
	}

	private void UpdateRunBuildIdentityHud()
	{
		if (!GodotObject.IsInstanceValid(_hudPanel))
			return;

		int total = _runIdentityPressureScore + _runIdentityChainScore + _runIdentityDetonationScore;
		if (total < Balance.BuildIdentityMinCycleScore)
		{
			_runBuildIdentityFormed = false;
			_runBuildIdentity = "UNFORMED";
			_hudPanel.SetRunBuildIdentity("UNFORMED", formed: false, accent: new Color(0.62f, 0.86f, 1.00f));
			return;
		}

		(string label, int score, Color accent)[] ranking =
		{
			("PRESSURE", _runIdentityPressureScore, new Color(0.30f, 0.86f, 1.00f)),
			("CHAIN", _runIdentityChainScore, new Color(0.78f, 0.58f, 1.00f)),
			("DETONATION", _runIdentityDetonationScore, new Color(1.00f, 0.60f, 0.24f)),
		};
		var ordered = ranking.OrderByDescending(x => x.score).ToArray();
		var best = ordered[0];
		var second = ordered[1];

		if (!_runBuildIdentityFormed)
		{
			float lead = (best.score - second.score) / Mathf.Max(1f, total);
			if (lead >= Balance.BuildIdentityHysteresis * 0.5f || best.score >= second.score + 2)
			{
				_runBuildIdentityFormed = true;
				_runBuildIdentity = best.label;
			}
			else
			{
				_runBuildIdentity = "UNFORMED";
			}
		}
		else if (!string.Equals(_runBuildIdentity, best.label, System.StringComparison.Ordinal))
		{
			float lead = (best.score - second.score) / Mathf.Max(1f, total);
			if (lead >= Balance.BuildIdentityHysteresis)
				_runBuildIdentity = best.label;
		}

		if (!_runBuildIdentityFormed || string.Equals(_runBuildIdentity, "UNFORMED", System.StringComparison.Ordinal))
		{
			_hudPanel.SetRunBuildIdentity("UNFORMED", formed: false, accent: new Color(0.62f, 0.86f, 1.00f));
			return;
		}

		var active = ranking.FirstOrDefault(x => x.label == _runBuildIdentity);
		Color accent = string.IsNullOrWhiteSpace(active.label)
			? new Color(0.62f, 0.86f, 1.00f)
			: active.accent;
		_hudPanel.SetRunBuildIdentity(_runBuildIdentity, formed: true, accent: accent);
	}

	private void OnSpectacleSurgeTriggered(SpectacleTriggerInfo info)
	{
		if (CurrentPhase != GamePhase.Wave)
			return;
		_screenshotPipeline?.NotifyTowerSurge(info);
		if (_tutorialManager != null && !_tutorialManager.SurgePanelShown)
		{
			_tutorialManager.MarkSurgePanelShown();
			ShowTutorialSurgePanel();
		}
		if (_botRunner == null && _runState != null)
		{
			_runState.SurgeHintTelemetry.TowersSurged++;
			int uniqueSupportedMods = info.Tower?.Modifiers
				.Select(m => SpectacleDefinitions.NormalizeModId(m.ModifierId))
				.Where(SpectacleDefinitions.IsSupported)
				.Distinct()
				.Count() ?? 0;
			if (uniqueSupportedMods >= 2)
				_runState.SurgeHintTelemetry.ComboTowerSurgesThisRun++;
		}
		if (_runState != null)
			_runState.TrackSpectacleSurge(info.Signature.EffectId, info.Tower?.TowerId, _runState.TotalPlayTime);
		if (_botRunner == null)
			AccumulateRunIdentityFromSurge(info.Signature);
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
		// Surge chain counter - accumulates while global meter is building
		_surgeChainCount++;
		_surgeChainResetTimer = SpectacleDefinitions.SurgeCooldownSeconds;
		if (_surgeChainCount >= 2)
			SpawnSurgeChainCallout(_surgeChainCount, ResolveSpectacleColor(info.Signature.PrimaryModId));

		ITowerView? sourceTower = info.Tower;
		if (sourceTower == null)
			return;
		if (!_firstTowerSurgeAssistShown && _botRunner == null)
		{
			_firstTowerSurgeAssistShown = true;
			TriggerHitStop(realDuration: 0.045f, slowScale: 0.62f);
			if (!_tipSeenTowerSurge && _tutorialManager == null)
			{
				_tipSeenTowerSurge = true;
				_hudPanel.ShowSurgeMicroHint("Surges build from modifier procs.", holdSeconds: 2.0f);
			}
		}
		if (_botRunner == null)
		{
			TryShowSurgeMicroHint(
				SurgeHintId.TowerReady,
				"Full ring auto-triggers Tower Surge",
				worldPos: sourceTower.GlobalPosition,
				holdSeconds: 2.4f,
				towerForHighlight: sourceTower);
			if (!_spectacleSystem.IsGlobalSurgeReady)
				_hudPanel.PulseGlobalSurgeMeter(0.85f);
			TryShowSurgeMicroHint(
				SurgeHintId.GlobalContribution,
				"Tower surges build this Global Surge bar",
				anchorToGlobalBar: true,
				holdSeconds: 2.2f,
				barPulseStrength: 1.0f);
		}
		bool mobileLite = IsMobileSpectacleLite();

		// Resolve dominant category to bias presentation toward one clear perceptual read.
		SurgeDifferentiation.TowerSurgeCategory surgeCategory =
			SurgeDifferentiation.ResolveTowerSurgeCategory(sourceTower.TowerId, info.Signature);
		int   catMaxLinks       = SurgeDifferentiation.GetCategoryMaxLinks(surgeCategory);
		float catLinkRangeMult  = SurgeDifferentiation.GetCategoryLinkRangeMult(surgeCategory);
		float catFlashAlpha     = SurgeDifferentiation.GetCategoryFlashAlpha(surgeCategory);
		float catSignatureDrama = SurgeDifferentiation.GetCategorySignatureDrama(surgeCategory);
		float catArchetypeDrama = SurgeDifferentiation.GetCategoryArchetypeDrama(surgeCategory);
		float catSlowMoDuration = SurgeDifferentiation.GetCategorySlowMoDuration(surgeCategory);
		float catSlowMoFactor   = SurgeDifferentiation.GetCategorySlowMoFactor(surgeCategory);
		float catBurstPower     = info.Signature.SurgePower * SurgeDifferentiation.GetCategoryBurstPowerMult(surgeCategory);

		Color accent = ResolveSpectacleColor(info.Signature.PrimaryModId);
		GD.Print($"[Surge] tower={sourceTower.TowerId}  category={surgeCategory}  mode={info.Signature.Mode}  effect={info.Signature.EffectName}  augment={info.Signature.AugmentName}  power={info.Signature.SurgePower:F2}");
		if (sourceTower is TowerInstance triggerTower && GodotObject.IsInstanceValid(triggerTower))
			triggerTower.FlashSpectacle(accent, major: true);
		// Tower Surge links are kept local: short reach, few targets.
		// Long-range board-wide arcs are reserved for Global Surge.
		// catLinkRangeMult extends reach for Spread to emphasize branching.
		float linkDistance = Mathf.Max(Balance.TowerSurgeMinLinkDistance, sourceTower.Range * Balance.TowerSurgeLinkRangeFactor * catLinkRangeMult);
		SpectacleConsequenceKind surgeRider = ResolveConsequenceKindFromSkin(comboSkin);
		float surgeConsequenceStrength = Mathf.Clamp(info.Signature.SurgePower, 0.6f, 2.2f) * 0.58f;
		// ── Universal substrate: UI, identity FX, and mod payload (all categories) ──────────
		// These fire regardless of category. They represent the surge's presence and
		// mod-driven identity, not its dominant gameplay mechanic.
		SpawnComboExplosionSkinFx(sourceTower, info.Signature, accent, comboSkin);
		ApplySpectacleGameplayPayload(info, isMajor: true);
		string surgeSoundId = surgeCategory switch
		{
			SurgeDifferentiation.TowerSurgeCategory.Spread  => "surge_spread",
			SurgeDifferentiation.TowerSurgeCategory.Burst   => "surge_burst",
			SurgeDifferentiation.TowerSurgeCategory.Control => "surge_control",
			SurgeDifferentiation.TowerSurgeCategory.Echo    => "surge_echo",
			_                                               => "surge",
		};
		SoundManager.Instance?.Play(surgeSoundId);

		// Signature rings: mod identity rings around the tower (drama varies per category).
		// Control gets prominent zone rings; Burst/Echo get stronger archetype presence.
		if (!mobileLite)
			SpawnSurgeSignatureRings(sourceTower.GlobalPosition, info.Signature, drama: catSignatureDrama);
		if (sourceTower is TowerInstance towerForArchetype && !mobileLite)
			SpawnTowerArchetypeFx(towerForArchetype, accent, drama: catArchetypeDrama);

		// Category label callout.
		string surgeCalloutUpper = SurgeDifferentiation.GetCategoryCallout(surgeCategory);
		float surgeCalloutDurationScale = SurgeUxTiming.ResolveSurgeCalloutDurationScale(2.8f);
		float surgeCalloutHoldPortion = SurgeUxTiming.ResolveSurgeCalloutHoldPortion(0.68f);
		Vector2 surgeCalloutOrigin = sourceTower.GlobalPosition + new Vector2(6f, 0f);
		SpawnCombatCallout(
			surgeCalloutUpper,
			surgeCalloutOrigin,
			accent,
			durationScale: surgeCalloutDurationScale,
			yOffset: -32f,
			drift: false,
			holdPortion: surgeCalloutHoldPortion,
			sizeOverride: 19);
		PulseTowerLoadoutIndicators(sourceTower, duration: 0.32f);
		// Keep in-world surge text concise: category callout only.
		// Combo/triad identity stays available in tooltip/readouts instead of adding a second combat label.

		ExplosionHitStopProfile hitStop = SpectacleExplosionCore.ResolveExplosionHitStopProfile(
			majorExplosion: true,
			globalSurge: false,
			info.Signature.SurgePower);
		if (hitStop.ShouldApply)
			TriggerHitStop(realDuration: hitStop.DurationSeconds, slowScale: hitStop.SlowScale);
		// Slowmo duration/factor already biased per category via catSlowMo* vars.
		TriggerSpectacleSlowMo(realDuration: catSlowMoDuration, speedFactor: catSlowMoFactor);
		float afterimageStrength = SpectacleExplosionCore.ResolveLargeSurgeAfterimageStrength(
			majorExplosion: true,
			globalSurge: false,
			info.Signature.SurgePower);
		FlashSpectacleAfterimage(accent, afterimageStrength);

		// ── Category-specific payload: one dominant mechanic per category ─────────────────
		// Each helper owns the flash, burst, links, and headline for its identity.
		// Behaviors are NOT shared across categories to avoid cross-category bleed.
		// Spread: propagation (chain arcs, volley spray, status chain)
		// Burst:  impact     (heavy explosion + second pulse only -- no spread)
		// Control: manipulation (slow field + subdued presence -- no chaining)
		// Echo:   repetition (triple-beat cadence -- no spray, no chain detonation)
		switch (surgeCategory)
		{
			case SurgeDifferentiation.TowerSurgeCategory.Spread:
				ApplySpreadSurge(sourceTower.GlobalPosition, accent, catFlashAlpha, catBurstPower,
					catMaxLinks, linkDistance, surgeRider, surgeConsequenceStrength,
					sourceTower, comboSkin, mobileLite);
				break;
			case SurgeDifferentiation.TowerSurgeCategory.Burst:
				ApplyBurstSurge(sourceTower.GlobalPosition, accent, catFlashAlpha, catBurstPower, sourceTower);
				break;
			case SurgeDifferentiation.TowerSurgeCategory.Control:
				ApplyControlSurge(sourceTower.GlobalPosition, accent, catFlashAlpha, catBurstPower,
					catMaxLinks, linkDistance, sourceTower, mobileLite);
				break;
			case SurgeDifferentiation.TowerSurgeCategory.Echo:
				ApplyEchoSurge(sourceTower.GlobalPosition, accent, catFlashAlpha, info.Signature.SurgePower,
					linkDistance, sourceTower, surgeRider, surgeConsequenceStrength, mobileLite);
				break;
		}
	}

	private void OnGlobalSurgeReadyHandler(string surgeLabel)
	{
		if (CurrentPhase != GamePhase.Wave) return;
		_screenshotPipeline?.NotifyGlobalSurgeReady(surgeLabel);
		if (_botRunner == null && _runState != null)
		{
			_runState.SurgeHintTelemetry.GlobalsBecameReady++;
			_globalReadyWindowOpen = true;
			_globalReadySincePlayTime = _runState.TotalPlayTime;
		}

		if (_autoDraftMode)
		{
			// Auto-draft: let the ready banner screenshot fire, then activate.
			GetTree().CreateTimer(1.2f).Timeout += () =>
			{
				if (CurrentPhase == GamePhase.Wave && _spectacleSystem.IsGlobalSurgeReady)
					_spectacleSystem.ActivateGlobalSurge();
			};
		}

		if (_botRunner != null)
		{
			// Bot mode: queue activation and trigger when wave state is favorable.
			_botGlobalSurgePending = true;
			_botGlobalSurgeReadyAtPlayTime = _runState?.TotalPlayTime ?? 0f;
			return;
		}

		_hudPanel.SetGlobalSurgeReady(true, surgeLabel);
		if (_tutorialManager == null)
			_hudPanel.SetPersistentSurgeHint("Global Surge ready: click this bar");
		if (!_tipSeenGlobalReady && _tutorialManager == null)
		{
			_tipSeenGlobalReady = true;
			_hudPanel.ShowSurgeMicroHint("Global Surge is ready - click the meter.", holdSeconds: 2.6f);
		}
		TryShowSurgeMicroHint(
			SurgeHintId.GlobalActivate,
			"Global Surge ready: click this bar",
			anchorToGlobalBar: true,
			holdSeconds: 2.4f,
			barPulseStrength: 1.05f);
		QueueGlobalActivateHintPrompt();

		// In tutorial, always pause and guide the player to activate the surge bar.
		if (_tutorialManager != null)
			ShowTutorialGlobalSurgeActivatePanel();
	}

	private void BeginCriticalFeedbackWindow(float seconds)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_hudPanel))
			return;

		ulong holdUsec = (ulong)(Mathf.Max(0.08f, seconds) * 1_000_000f);
		ulong untilUsec = Time.GetTicksUsec() + holdUsec;
		if (untilUsec > _nonCriticalHintSuppressUntilUsec)
			_nonCriticalHintSuppressUntilUsec = untilUsec;

		_hudPanel.SetNonCriticalHintSuppressed(true);
	}

	private void RefreshNonCriticalHintSuppression()
	{
		if (_nonCriticalHintSuppressUntilUsec == 0 || !GodotObject.IsInstanceValid(_hudPanel))
			return;
		if (Time.GetTicksUsec() < _nonCriticalHintSuppressUntilUsec)
			return;

		_nonCriticalHintSuppressUntilUsec = 0;
		_hudPanel.SetNonCriticalHintSuppressed(false);
	}

	private bool IsNonCriticalHintSuppressed()
	{
		if (_nonCriticalHintSuppressUntilUsec == 0)
			return false;

		if (Time.GetTicksUsec() >= _nonCriticalHintSuppressUntilUsec)
		{
			_nonCriticalHintSuppressUntilUsec = 0;
			if (GodotObject.IsInstanceValid(_hudPanel))
				_hudPanel.SetNonCriticalHintSuppressed(false);
			return false;
		}

		return true;
	}

	private void UpdateCombatVisualChaosLoad(float delta)
	{
		if (delta <= 0f)
			return;

		float target = 0f;
		if (_botRunner == null && CurrentPhase == GamePhase.Wave && _runState != null)
			target = ComputeCombatVisualChaosTarget();

		float rate = target > CombatVisualChaosLoad ? 6.2f : 3.4f;
		float t = Mathf.Clamp(delta * rate, 0.05f, 0.32f);
		CombatVisualChaosLoad = Mathf.Lerp(CombatVisualChaosLoad, target, t);
		if (CombatVisualChaosLoad < 0.001f)
			CombatVisualChaosLoad = 0f;
	}

	private float ComputeCombatVisualChaosTarget()
	{
		float enemyDensity = Mathf.InverseLerp(7f, 30f, _runState.EnemiesAlive.Count);
		float residueDensity = Mathf.InverseLerp(2f, 10f, _explosionResidues.Count);
		float overlaySum = 0f;
		overlaySum += ReadOverlayAlpha(_spectaclePulse) * 1.10f;
		overlaySum += ReadOverlayAlpha(_spectacleAfterimage) * 0.90f;
		overlaySum += ReadOverlayAlpha(_threatPulse) * 1.20f;
		overlaySum += ReadOverlayAlpha(_waveClearFlash) * 0.95f;
		overlaySum += ReadOverlayAlpha(_lingerTint) * 0.70f;
		if (GodotObject.IsInstanceValid(_vignetteRect) && _vignetteRect.Visible)
			overlaySum += 0.18f;

		float overlayDensity = Mathf.Clamp(overlaySum / 1.45f, 0f, 1f);
		float criticalBoost = IsNonCriticalHintSuppressed() ? 0.12f : 0f;
		return Mathf.Clamp(
			enemyDensity * 0.62f +
			overlayDensity * 0.28f +
			residueDensity * 0.18f +
			criticalBoost,
			0f, 1f);
	}

	private static float ReadOverlayAlpha(ColorRect? overlay)
	{
		if (!GodotObject.IsInstanceValid(overlay) || !overlay.Visible)
			return 0f;
		return Mathf.Clamp(overlay.Color.A, 0f, 1f);
	}

	private double ResolveVisualFxClock()
	{
		if (_runState != null)
			return _runState.TotalPlayTime;
		return Time.GetTicksUsec() / 1_000_000.0;
	}

	private bool TryShowSurgeMicroHint(
		SurgeHintId id,
		string text,
		Vector2? worldPos = null,
		bool anchorToGlobalBar = false,
		float holdSeconds = 2.2f,
		float barPulseStrength = 0.9f,
		ITowerView? towerForHighlight = null)
	{
		if (_botRunner != null || _runState == null || _tutorialManager != null || SettingsManager.Instance == null)
			return false;
		if (IsNonCriticalHintSuppressed())
			return false;
		if (anchorToGlobalBar && !GodotObject.IsInstanceValid(_hudPanel))
			return false;
		if (!anchorToGlobalBar && worldPos.HasValue && !GodotObject.IsInstanceValid(_worldNode))
			return false;

		var profile = SettingsManager.Instance.SurgeHintProfile;
		if (!SurgeHintAdvisor.ShouldShowMicroHint(id, profile, _surgeHintRuntime, _runState.TotalPlayTime))
			return false;

		if (anchorToGlobalBar)
		{
			_hudPanel.ShowSurgeMicroHint(text, holdSeconds);
			_hudPanel.PulseGlobalSurgeMeter(barPulseStrength);
		}
		else if (worldPos.HasValue)
		{
			float visibleSeconds = SurgeUxTiming.ResolveWorldTeachingHintHold(holdSeconds);
			_hudPanel.ShowTeachingHintAtScreen(
				$"Tip: {text}",
				WorldToScreen(worldPos.Value),
				holdSeconds: visibleSeconds);
			if (towerForHighlight is TowerInstance highlightedTower && GodotObject.IsInstanceValid(highlightedTower))
				highlightedTower.TriggerTeachingHighlight(visibleSeconds + 0.25f);
		}
		else
		{
			return false;
		}

		SurgeHintAdvisor.RecordMicroHintShown(id, profile, _surgeHintRuntime, _runState.TotalPlayTime);
		SettingsManager.Instance.SaveSurgeHintingProgress();
		return true;
	}

	private void QueueGlobalActivateHintPrompt()
	{
		if (_botRunner != null || _tutorialManager != null || _runState == null || SettingsManager.Instance == null)
			return;

		ulong token = ++_globalActivateHintToken;
		GetTree().CreateTimer(2.4f, true, false, true).Timeout += () =>
		{
			if (token != _globalActivateHintToken || !GodotObject.IsInstanceValid(this))
				return;
			if (CurrentPhase != GamePhase.Wave || _runState == null || !_spectacleSystem.IsGlobalSurgeReady)
				return;
			if (_runState.SurgeHintTelemetry.GlobalsActivated > 0)
				return;

			TryShowSurgeMicroHint(
				SurgeHintId.GlobalActivate,
				"Click to trigger Global Surge",
				anchorToGlobalBar: true,
				holdSeconds: 2.5f,
				barPulseStrength: 1.15f);
		};
	}

	private void OnHudGlobalSurgeActivate()
	{
		if (IsDraftPlacementActive())
			return;
		_globalActivateHintToken++;
		_hudPanel.SetGlobalSurgeReady(false);
		_hudPanel.SetPersistentSurgeHint(null);
		_spectacleSystem.ActivateGlobalSurge();
	}

	private bool IsDraftPlacementActive()
	{
		return CurrentPhase == GamePhase.Draft
			&& _draftPanel != null
			&& GodotObject.IsInstanceValid(_draftPanel)
			&& (_draftPanel.IsAwaitingSlot || _draftPanel.IsAwaitingTower || _draftPanel.IsAwaitingPremiumTarget);
	}

	private void OnGlobalSurgeTriggered(GlobalSurgeTriggerInfo info)
	{
		if (CurrentPhase != GamePhase.Wave)
			return;
		if (_runState == null)
			return;
		_screenshotPipeline?.NotifyGlobalSurge(info);
		_botGlobalSurgePending = false;
		_botGlobalSurgeReadyAtPlayTime = -1f;
		if (_botRunner == null)
		{
			_runState.SurgeHintTelemetry.GlobalsActivated++;
			if (_globalReadyWindowOpen)
			{
				float activationDelay = Mathf.Max(0f, _runState.TotalPlayTime - _globalReadySincePlayTime);
				if (activationDelay <= 10f)
					_runState.SurgeHintTelemetry.QuickGlobalActivationsWithin10s++;
			}
		}
		_globalReadyWindowOpen = false;
		_globalActivateHintToken++;
		_hudPanel.SetPersistentSurgeHint(null);
		_runState.TrackSpectacleGlobal(info.EffectId);
		MusicDirector.Instance?.OnGlobalSurge();
		string traceId = NextSurgeTraceId(global: true);
		_botLastTraceTriggerId = traceId;
		AppendBotTraceEvent(
			eventType: "global_surge_triggered",
			surgeTriggerId: traceId,
			stageId: "trigger",
			comboSkin: "global");
		// Resolve feel early so both bot and visual paths use differentiated payloads.
		string[] dominantMods = info.DominantModIds ?? System.Array.Empty<string>();
		SurgeDifferentiation.GlobalSurgeFeel feel = SurgeDifferentiation.ResolveFeel(dominantMods);
		bool overcharged = info.Overcharged;
		float storedOverfill = Mathf.Max(0f, info.StoredOverfill);

		if (_botRunner != null)
		{
			ApplyGlobalSurgeGameplayPayload(info, feel);
			if (overcharged)
				ApplyOverfillCatastropheBonus(sourceTower: null, accent: new Color(1.00f, 0.82f, 0.42f), storedOverfill);
			return;
		}
		Vector2 center = ScreenToWorld(GetViewport().GetVisibleRect().Size * 0.5f);
		var globalColor = new Color(1.00f, 0.90f, 0.56f);
		ITowerView? globalDamageSource = ResolveSpectacleSourceTower(null);
		float globalDamageBase = ResolveGlobalSpectacleBaseDamage();
		float globalDurationScale = GlobalSurgeLingerMultiplier * GlobalSurgeDurationScale;

		// ── Resolve identity from dominant contributing mods ───────────────────────
		string surgeLabel = SurgeDifferentiation.ResolveLabel(feel);
		_surgeArchetypeCounts.TryGetValue(surgeLabel, out int _existingCount);
		_surgeArchetypeCounts[surgeLabel] = _existingCount + 1;
		float flashAlpha = SurgeDifferentiation.ResolveFlashAlpha(feel);

		// ── Clear buildup effects - vignette and chain counter ────────────────────
		_surgeChainCount = 0;
		_surgeChainResetTimer = 0f;
		if (GodotObject.IsInstanceValid(_vignetteRect))
			_vignetteRect.Visible = false;

		// ── Banner subtitle: feel-specific mechanical summary ─────────────────────
		string surgeSubtitle = SurgeDifferentiation.ResolveTypeSubtitle(feel);
		if (overcharged)
			surgeSubtitle += "  ·  OVERCHARGED";

		GD.Print($"[GlobalSurge] label={surgeLabel}  feel={feel}  dominantMods=[{string.Join(", ", dominantMods)}]  contributors={info.UniqueContributors}");

		// Multi-color ripples - one distinct mod color per contributing role (Phase 1)
		Color[] rippleColors = dominantMods.Length > 0
			? dominantMods.Select(SlotTheory.UI.ModifierVisuals.GetAccent).ToArray()
			: new[] { globalColor };

		// ── Group 1: immediate - gameplay payload + center visuals + hitstop/slowmo ──
		ApplyGlobalSurgeGameplayPayload(info, feel);
		SpawnSpectacleBurstFx(center, globalColor, major: true, power: 2.15f);
		SpawnGlobalSurgeRipples(center, rippleColors, Mathf.Max(2, info.UniqueContributors), lingerMultiplier: globalDurationScale, feel: feel);
		FlashSpectacleScreen(globalColor, peakAlpha: flashAlpha, rampSec: 0.09f, fadeSec: 0.62f * globalDurationScale);
		// Sustained archetype tint – color and persistence keyed to feel type.
		Color lingerColor = SurgeDifferentiation.ResolveLingerColor(feel);
		// Extended linger tint – Global Surge should leave a sustained atmospheric residue.
		FlashSpectacleScreenLinger(lingerColor, alpha: 0.13f, holdSec: Balance.GlobalSurgeLingerHoldSec, fadeSec: Balance.GlobalSurgeLingerFadeSec);
		// Detonation: second snap-flash after brief delay (feel-specific extra punch)
		if (feel == SurgeDifferentiation.GlobalSurgeFeel.Detonation)
		{
			GetTree().CreateTimer(0.42f, true, false, true).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(this))
					FlashSpectacleScreen(globalColor, peakAlpha: 0.14f, rampSec: 0.04f, fadeSec: 0.24f);
			};
		}
		// Feel-differentiated sound: Pressure=low rumble, Chain=lightning, Detonation=high spike
		string surgeSoundId  = feel == SurgeDifferentiation.GlobalSurgeFeel.Neutral ? "surge_lightning" : "surge_global";
		float  surgeSoundPitch = feel switch
		{
			SurgeDifferentiation.GlobalSurgeFeel.Pressure   => 0.88f,
			SurgeDifferentiation.GlobalSurgeFeel.Detonation => 1.14f,
			_                                               => 1.00f,
		};
		SoundManager.Instance?.Play(surgeSoundId, surgeSoundPitch);
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
		if (overcharged)
			ApplyOverfillCatastropheBonus(globalDamageSource, globalColor, storedOverfill);

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
				// Each tower fires far-reaching links – the whole board becomes the storm.
				SpawnSpectacleLinks(
					t.GlobalPosition,
					accent,
					maxLinks: Balance.GlobalSurgeLinksPerTower,
					maxDistance: Mathf.Max(Balance.GlobalSurgeMinLinkDistance, t.Range * Balance.GlobalSurgeLinkRangeFactor),
					majorStyle: true,
					sourceTower: t,
					consequenceDamageScale: 0.08f,
					rider: SpectacleConsequenceKind.Vulnerability,
					riderStrength: 0.92f);
				SpawnSpectacleTowerVolleyFx(t, accent, major: true, power: 1.10f);
				// Full-drama signature rings + archetype FX – this is the main event.
				var tSig = _spectacleSystem.PreviewSignature(t);
				SpawnSurgeSignatureRings(t.GlobalPosition, tSig, drama: Balance.GlobalSurgeSignatureDrama);
				if (!IsMobileSpectacleLite())
					SpawnTowerArchetypeFx(t, accent, drama: Balance.GlobalSurgeArchetypeDrama);
				// Extended afterglow – towers stay illuminated through the entire event.
				t.StartAfterGlow(accent, duration: Balance.GlobalSurgeTowerAfterglow);
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

			// Tower web: connect every active tower to every other – the board becomes a grid of arcs.
			// Purely visual, no damage; fires simultaneously with the banner.
			SpawnGlobalSurgeTowerWeb(activeTowers, Balance.GlobalSurgeTowerWebLifetime);

			// Chain feel: arcs jump enemy-to-enemy through the pack (spreading chain reaction visual).
			if (feel == SurgeDifferentiation.GlobalSurgeFeel.Neutral)
				SpawnGlobalSurgeEnemyChainArcs(globalColor, Balance.ChainSurgeArcLifetime);

		// Phase 5: Triad Factorio moment - second flash pulse when 3 distinct mod identities converge
		if (dominantMods.Length >= 3)
		{
			GetTree().CreateTimer(0.55f, true, false, true).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(this))
					FlashSpectacleScreen(globalColor, peakAlpha: 0.16f, rampSec: 0.05f, fadeSec: 0.30f);
			};
		}
		};

		// ── Group 4: lingering storm aftermath (purely visual, decoupled from damage) ──
		// Pattern varies by feel:
		//   Pressure   – board saturation: every tower arcs to every enemy
		//   Chain      – enemy-web: arcs chain enemy→neighbor along the path
		//   Detonation – radial blast: arcs from screen center to all enemies
		float stormDelay = finaleDelay + 0.38f;
		Vector2 capturedCenter = center;
		Color capturedGlobalColor = globalColor;
		GetTree().CreateTimer(stormDelay, true, false, true).Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(this) || CurrentPhase != GamePhase.Wave)
				return;
			SpawnGlobalSurgeLingeringStorm(activeTowers, Balance.GlobalSurgeLongArcLifetime, capturedCenter, capturedGlobalColor, feel);
		};
	}

	/// <summary>
	/// Spawns a longer-lived arc between two world positions. No gameplay payload.
	/// Used for Global Surge's tower web and lingering storm phases.
	/// </summary>
	private void SpawnSpectacleArcLingering(Vector2 from, Vector2 to, Color color, float intensity, float lifetimeSec)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		var arc = new ChainArc();
		_worldNode.AddChild(arc);
		arc.GlobalPosition = Vector2.Zero;
		arc.Initialize(from, to, color, intensity, mineChainStyle: false, lifetimeSec: lifetimeSec);
	}

	/// <summary>
	/// Connects every active tower to every other tower with a lingering arc.
	/// Creates the "whole board is one storm" visual at Global Surge ignition.
	/// Purely cosmetic – no damage applied.
	/// </summary>
	private void SpawnGlobalSurgeTowerWeb(
		System.Collections.Generic.List<(TowerInstance tower, Color accent)> towers,
		float arcLifetime)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		if (IsMobileSpectacleLite())
			return; // skip on mobile to preserve performance
		for (int i = 0; i < towers.Count; i++)
		{
			for (int j = i + 1; j < towers.Count; j++)
			{
				var (tA, colorA) = towers[i];
				var (tB, colorB) = towers[j];
				if (!GodotObject.IsInstanceValid(tA) || !GodotObject.IsInstanceValid(tB))
					continue;
				Color webColor = colorA.Lerp(colorB, 0.5f);
				SpawnSpectacleArcLingering(tA.GlobalPosition, tB.GlobalPosition, webColor, 0.82f, arcLifetime);
			}
		}
	}

	/// <summary>
	/// Sends long-lasting arcs from every active tower toward the furthest enemies on the board.
	/// This is the "storm dissipating" visual aftermath of Global Surge –
	/// visuals persist well after the damage has resolved, keeping the board alive.
	/// Purely cosmetic – no damage applied.
	/// </summary>
	/// <summary>
	/// Lingering storm aftermath arcs -- pattern varies by feel type:
	///   Pressure   = board saturation: every tower → every enemy (maximum control coverage)
	///   Chain      = enemy web: arcs chain along the path, enemy → nearest neighbor
	///   Detonation = radial blast: screen center → all enemies (explosion pattern)
	/// Purely visual, no gameplay payload.
	/// </summary>
	private void SpawnGlobalSurgeLingeringStorm(
		System.Collections.Generic.List<(TowerInstance tower, Color accent)> towers,
		float arcLifetime,
		Vector2 center,
		Color globalColor,
		SurgeDifferentiation.GlobalSurgeFeel feel)
	{
		if (_botRunner != null || _runState == null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		bool mobileLite = IsMobileSpectacleLite();

		var allEnemies = _runState.EnemiesAlive
			.Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f)
			.ToList();
		if (allEnemies.Count == 0)
			return;

		switch (feel)
		{
			case SurgeDifferentiation.GlobalSurgeFeel.Pressure:
			{
				// Board saturation: each tower arcs to every live enemy -- full control coverage.
				int maxPerTower = mobileLite ? 4 : allEnemies.Count;
				foreach (var (t, accent) in towers)
				{
					if (!GodotObject.IsInstanceValid(t)) continue;
					int take = Mathf.Min(maxPerTower, allEnemies.Count);
					for (int j = 0; j < take; j++)
						SpawnSpectacleArcLingering(t.GlobalPosition, allEnemies[j].GlobalPosition, accent, 0.82f + j * 0.02f, arcLifetime);
				}
				break;
			}
			case SurgeDifferentiation.GlobalSurgeFeel.Detonation:
			{
				// Radial explosion: arcs fan from screen center outward to all enemies.
				int maxFromCenter = mobileLite ? 8 : allEnemies.Count;
				for (int i = 0; i < Mathf.Min(maxFromCenter, allEnemies.Count); i++)
					SpawnSpectacleArcLingering(center, allEnemies[i].GlobalPosition, globalColor, 0.88f + i * 0.02f, arcLifetime);
				break;
			}
			default: // Chain/Neutral: arcs chain enemy → neighbor along the path.
			{
				var ordered = allEnemies.OrderByDescending(e => e.ProgressRatio).ToList();
				// Each tower connects to the lead enemy, then arcs propagate through the pack.
				foreach (var (t, accent) in towers)
				{
					if (!GodotObject.IsInstanceValid(t) || ordered.Count == 0) continue;
					SpawnSpectacleArcLingering(t.GlobalPosition, ordered[0].GlobalPosition, accent, 0.90f, arcLifetime);
				}
				int chainJumps = mobileLite ? 4 : Mathf.Min(Balance.ChainSurgeEnemyArcs, ordered.Count - 1);
				for (int i = 0; i < chainJumps && i + 1 < ordered.Count; i++)
				{
					Color chainColor = towers.Count > 0 ? towers[i % towers.Count].accent : globalColor;
					SpawnSpectacleArcLingering(ordered[i].GlobalPosition, ordered[i + 1].GlobalPosition, chainColor, 0.85f + i * 0.03f, arcLifetime);
				}
				break;
			}
		}
	}

	/// <summary>
	/// Spread category Tower Surge: arcs jump enemy-to-enemy through the wave pack.
	/// Draws lingering arcs between adjacent enemies (sorted by progress), creating a
	/// visible chain that reads as "the surge propagated through the group" rather than
	/// "the tower shot multiple targets" (which is what the standard links already show).
	/// Purely visual -- no gameplay payload.
	/// </summary>
	private void SpawnTowerSurgeSpreadArcs(Vector2 origin, Color accent, bool mobileLite)
	{
		if (_botRunner != null || _runState == null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		// Collect enemies in range, sorted front-to-back (highest progress first).
		int arcCap = mobileLite ? 3 : Balance.TowerSurgeSpreadWebCount;
		var targets = _runState.EnemiesAlive
			.Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f
				&& origin.DistanceTo(e.GlobalPosition) <= Balance.TowerSurgeSpreadWebReach)
			.OrderByDescending(e => e.ProgressRatio)
			.Take(arcCap + 1) // +1 so we can draw N arcs between N+1 enemies
			.ToList();
		// Draw arcs between adjacent enemies: [0]→[1], [1]→[2], etc.
		// This reads as "damage spreading through the pack" not "tower reaching out".
		int jumps = Mathf.Min(arcCap, targets.Count - 1);
		for (int i = 0; i < jumps; i++)
			SpawnSpectacleArcLingering(
				targets[i].GlobalPosition,
				targets[i + 1].GlobalPosition,
				accent,
				intensity: 0.90f - i * 0.04f,
				lifetimeSec: Balance.TowerSurgeSpreadWebLifetime);
	}

	/// <summary>
	/// Echo category Tower Surge: third beat of the repeat cadence (fires at 0.48s).
	/// Delivers a ghost burst + links WITH real damage -- the late echo has material consequence,
	/// not just a visual callback. Player feels "it hit again, not just lit up again".
	/// </summary>
	private void QueueTowerSurgeLateEcho(Vector2 origin, Color accent, float power, float maxDist, bool mobileLite, ITowerView? sourceTower)
	{
		GetTree().CreateTimer(Balance.TowerSurgeEchoDelay2, true, false, true).Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(this) || CurrentPhase != GamePhase.Wave) return;
			float echoPower = power * Balance.TowerSurgeEchoPulse2Power;
			if (!mobileLite)
				SpawnSpectacleBurstFx(origin, accent, major: true, power: echoPower);
			// Late echo links with real damage -- the ghost hit counts
			SpawnSpectacleLinks(origin, accent,
				maxLinks: mobileLite ? 1 : 2,
				maxDistance: Mathf.Max(160f, maxDist * 0.80f),
				majorStyle: false,
				sourceTower: sourceTower,
				consequenceDamageScale: Balance.TowerSurgeEchoLateEchoDamageScale,
				rider: SpectacleConsequenceKind.None,
				riderStrength: 1f,
				spawnResidue: false);
			FlashSpectacleScreen(accent, peakAlpha: 0.07f, rampSec: 0.04f, fadeSec: 0.18f);
		};
	}

	/// <summary>
	/// Control category Tower Surge: apply a slow + vulnerability field to all enemies in range.
	/// Slow makes movement manipulation visible; vulnerability window makes it a setup event --
	/// the next few hits on those enemies deal more damage, giving the surge a material payoff
	/// beyond "just a slow". This distinguishes Control surge from a normal Chill Shot slow.
	/// Balance: short duration and mild multiplier to keep the amp supplemental, not dominant.
	/// </summary>
	private void ApplyControlSurgeSlowField(Vector2 origin)
	{
		if (_runState == null) return;
		foreach (var enemy in _runState.EnemiesAlive)
		{
			if (!GodotObject.IsInstanceValid(enemy) || enemy.Hp <= 0f) continue;
			if (origin.DistanceTo(enemy.GlobalPosition) > Balance.TowerSurgeControlSlowRadius) continue;
			// Slow: the visible manipulation (enemy moves at 62% speed for 2.2s)
			Statuses.ApplySlow(enemy, Balance.TowerSurgeControlSlowDuration, Balance.TowerSurgeControlSlowFactor);
			// Vulnerability: slowed enemies also take +18% damage for 1.8s
			// This makes Control a setup/setup event, not just a slow application.
			Statuses.ApplyDamageAmp(enemy, Balance.TowerSurgeControlVulnDuration, Balance.TowerSurgeControlVulnMultiplier);
		}
	}

	// ── Category-specific payload helpers ──────────────────────────────────────────────────
	// Each method owns ALL of the gameplay + VFX for its category.
	// They do NOT share behaviors -- overlap removal is enforced here, not via flags.

	/// <summary>
	/// SPREAD: propagation identity.
	/// Tower→many links + volley spray + status chain detonation + enemy→enemy arcs.
	/// Player reads: "the surge spread through the whole group".
	/// </summary>
	private void ApplySpreadSurge(
		Vector2 origin, Color accent, float flashAlpha, float burstPower,
		int maxLinks, float linkDistance,
		SpectacleConsequenceKind rider, float consequenceStrength,
		ITowerView? sourceTower, ComboExplosionSkin comboSkin, bool mobileLite)
	{
		// Wide radiating flash -- fast spark that lingers as a wave (distinct from Burst's sharp spike)
		FlashSpectacleScreen(accent, peakAlpha: flashAlpha, rampSec: 0.03f, fadeSec: 0.42f);
		// Ignition burst at tower (the origin of the spread)
		SpawnSpectacleBurstFx(origin, accent, major: true, power: burstPower);
		// Tower→many links: 5 targets, extended range -- the tower's reach made visible
		SpawnSpectacleLinks(origin, accent,
			maxLinks: maxLinks,
			maxDistance: linkDistance,
			majorStyle: true,
			sourceTower: sourceTower,
			consequenceDamageScale: 0.12f + 0.02f * consequenceStrength,
			rider: rider,
			riderStrength: consequenceStrength,
			spawnResidue: rider != SpectacleConsequenceKind.None);
		// Volley spray: arc fan to nearby enemies (multi-target feel, Spread-only)
		if (sourceTower != null)
			SpawnSpectacleTowerVolleyFx(sourceTower, accent, major: true, power: burstPower);
		// Status chain: primed enemies (marked/slowed) detonate in sequence (chain identity, Spread-only)
		TriggerStatusDetonationChain(sourceTower, origin, accent, comboSkin, globalSurge: false, burstPower);
		// Headline: enemy→enemy propagation arcs (the damage jumping through the pack)
		SpawnTowerSurgeSpreadArcs(origin, accent, mobileLite);
	}

	/// <summary>
	/// BURST: impact identity.
	/// Heavy concentrated explosion + second pulse at 0.38s with real damage.
	/// Player reads: "this surge slammed a chunk of HP off, then hit again".
	/// The second pulse is not just more FX -- it delivers actual gameplay consequence.
	/// </summary>
	private void ApplyBurstSurge(Vector2 origin, Color accent, float flashAlpha, float burstPower, ITowerView? sourceTower)
	{
		// Hard concentrated flash -- sharp spike in, quick out (impact feel)
		FlashSpectacleScreen(accent, peakAlpha: flashAlpha, rampSec: 0.02f, fadeSec: 0.11f);
		// Heavy initial explosion at tower (1.55x power -- the dominant hit)
		SpawnSpectacleBurstFx(origin, accent, major: true, power: burstPower);
		// Second detonation after a deliberate gap -- the one-two punch
		float p2 = burstPower * Balance.TowerSurgeBurstPulse2Power;
		GetTree().CreateTimer(Balance.TowerSurgeBurstPulse2Delay, true, false, true).Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(this) || CurrentPhase != GamePhase.Wave) return;
			SpawnSpectacleBurstFx(origin, accent, major: true, power: p2);
			FlashSpectacleScreen(accent, peakAlpha: flashAlpha * 0.80f, rampSec: 0.02f, fadeSec: 0.11f);
			// Second pulse delivers real damage -- the "slam" has material consequence, not just FX.
			// Targets the 2 highest-HP enemies in range: the biggest threats take the biggest hit.
			// Bypasses ApplyTargetedSpectacleConsequence's 0.30 cap by calling ApplySpectacleDamage directly.
			ITowerView? resolvedBurst = ResolveSpectacleSourceTower(sourceTower);
			if (resolvedBurst != null && _runState != null)
			{
				float rawDamage = resolvedBurst.BaseDamage * Balance.TowerSurgeBurstPulse2DamageScale;
				var pulseTargets = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => origin.DistanceTo(e.GlobalPosition) <= Balance.TowerSurgeBurstPulse2Range)
					.OrderByDescending(e => e.Hp)
					.Take(2)
					.ToList();
				foreach (var tgt in pulseTargets)
					ApplySpectacleDamage(resolvedBurst, tgt, rawDamage, accent, heavyHit: true,
						triggerHitStopOnKill: false, allowOverkillBloom: true, allowMarkedPop: false);
			}
		};
		// No links, no volley, no status chain, no echo -- Burst is concentrated impact only.
	}

	/// <summary>
	/// CONTROL: manipulation identity.
	/// Slow field applied to nearby enemies + subdued visual presence. No chaining, no spray.
	/// Player reads: "this surge changed how enemies move".
	/// </summary>
	private void ApplyControlSurge(
		Vector2 origin, Color accent, float flashAlpha, float burstPower,
		int maxLinks, float linkDistance, ITowerView? sourceTower, bool mobileLite)
	{
		// Very soft flash -- zone presence, not explosive impact
		FlashSpectacleScreen(accent, peakAlpha: flashAlpha, rampSec: 0.06f, fadeSec: 0.28f);
		// Subdued burst marker -- shows the zone origin without explosion feel (0.55x power)
		SpawnSpectacleBurstFx(origin, accent, major: true, power: burstPower * 0.55f);
		// Minimal focused links (2, low damage) -- targeting clarity, not multi-hit spray
		SpawnSpectacleLinks(origin, accent,
			maxLinks: maxLinks,
			maxDistance: linkDistance,
			majorStyle: false,
			sourceTower: sourceTower,
			consequenceDamageScale: 0.06f,
			rider: SpectacleConsequenceKind.None,
			riderStrength: 1f,
			spawnResidue: false);
		// Headline: actual slow applied to nearby enemies (battlefield manipulation, Control-only)
		ApplyControlSurgeSlowField(origin);
		// No volley, no status chain, no echo -- Control is zone manipulation, not chained explosions.
	}

	/// <summary>
	/// ECHO: repetition identity.
	/// Triple-beat cadence: initial hit → 0.24s echo → 0.48s late echo.
	/// Player reads: "this surge keeps happening after the first hit".
	/// </summary>
	private void ApplyEchoSurge(
		Vector2 origin, Color accent, float flashAlpha, float surgePower,
		float linkDistance, ITowerView? sourceTower,
		SpectacleConsequenceKind rider, float consequenceStrength, bool mobileLite)
	{
		// Moderate initial flash -- first beat anchor
		FlashSpectacleScreen(accent, peakAlpha: flashAlpha, rampSec: 0.05f, fadeSec: 0.22f);
		// Initial burst (first hit -- establishes the pattern)
		SpawnSpectacleBurstFx(origin, accent, major: true, power: surgePower);
		// Initial links (2, focused) -- first strike targeting clarity
		SpawnSpectacleLinks(origin, accent,
			maxLinks: 2,
			maxDistance: Mathf.Max(Balance.TowerSurgeMinLinkDistance, linkDistance * 0.85f),
			majorStyle: true,
			sourceTower: sourceTower,
			consequenceDamageScale: 0.10f + 0.02f * consequenceStrength,
			rider: rider,
			riderStrength: consequenceStrength * 0.80f,
			spawnResidue: rider != SpectacleConsequenceKind.None);
		// Second beat: delayed burst + links at 0.24s (the echo begins)
		QueueSpectacleEcho(origin, accent,
			major: true,
			power: surgePower,
			maxDistance: linkDistance,
			sourceTower: sourceTower,
			rider: rider,
			spawnResidue: rider != SpectacleConsequenceKind.None);
		// Third beat: late echo at 0.48s (the ghost fires again, with real damage)
		QueueTowerSurgeLateEcho(origin, accent, surgePower, linkDistance, mobileLite, sourceTower);
		// No volley, no status chain -- Echo is temporal repetition, not spreading damage.
	}

	/// <summary>
	/// Chain feel only: initial enemy→enemy arc chain at finale time.
	/// Fires before the lingering storm to establish the "spreading chain reaction" identity.
	/// Purely visual, no gameplay payload.
	/// </summary>
	private void SpawnGlobalSurgeEnemyChainArcs(Color globalColor, float arcLifetime)
	{
		if (_botRunner != null || _runState == null || !GodotObject.IsInstanceValid(_worldNode))
			return;
		bool mobileLite = IsMobileSpectacleLite();
		var ordered = _runState.EnemiesAlive
			.Where(e => GodotObject.IsInstanceValid(e) && e.Hp > 0f)
			.OrderByDescending(e => e.ProgressRatio)
			.ToList();
		int jumps = mobileLite ? 3 : Mathf.Min(Balance.ChainSurgeEnemyArcs, ordered.Count - 1);
		for (int i = 0; i < jumps && i + 1 < ordered.Count; i++)
			SpawnSpectacleArcLingering(ordered[i].GlobalPosition, ordered[i + 1].GlobalPosition, globalColor, 0.90f + i * 0.03f, arcLifetime);
	}

	private void ApplyOverfillCatastropheBonus(ITowerView? sourceTower, Color accent, float storedOverfill)
	{
		if (_runState == null || storedOverfill <= 0f)
			return;

		// Modest deterministic bonus: small all-tower cooldown reclaim + delayed echo pulse.
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].Tower;
			if (tower == null)
				continue;
			ReduceTowerCooldown(tower, Balance.OverfillCatastropheCooldownRefund);
		}

		float threshold = SpectacleDefinitions.ResolveGlobalThreshold();
		float overfillScale = Mathf.Clamp(storedOverfill / Mathf.Max(1f, threshold), 0.12f, 0.50f);
		void EmitDelayedEcho()
		{
			if (_runState == null)
				return;

			ITowerView? resolvedSource = ResolveSpectacleSourceTower(sourceTower);
			float baseDamage = ResolveGlobalSpectacleBaseDamage() * Balance.OverfillCatastropheEchoDamageScale * (0.75f + overfillScale);
			Vector2 center = ScreenToWorld(GetViewport().GetVisibleRect().Size * 0.5f);
			var targets = _runState.EnemiesAlive
				.Where(IsEnemyUsable)
				.OrderByDescending(e => e.ProgressRatio)
				.Take(6)
				.ToList();

			for (int i = 0; i < targets.Count; i++)
			{
				float falloff = Mathf.Max(0.58f, 1f - i * 0.11f);
				if (_botRunner == null)
				{
					SpawnSpectacleArc(center, targets[i].GlobalPosition, accent, intensity: 0.94f + i * 0.06f, mineChainStyle: true);
					SpawnSpectacleBurstFx(targets[i].GlobalPosition, accent, major: false, power: 0.72f + overfillScale * 0.6f);
				}
				if (resolvedSource != null)
					ApplySpectacleDamage(resolvedSource, targets[i], baseDamage * falloff, accent, heavyHit: false);
			}
		}

		if (_botRunner != null || SettingsManager.Instance?.ReducedMotion == true)
		{
			EmitDelayedEcho();
			return;
		}

		GetTree().CreateTimer(0.42f, true, false, true).Timeout += EmitDelayedEcho;
	}

	private void ApplyGlobalSurgeGameplayPayload(
		GlobalSurgeTriggerInfo info,
		SurgeDifferentiation.GlobalSurgeFeel feel = SurgeDifferentiation.GlobalSurgeFeel.Neutral)
	{
		if (_runState == null || _runState.Slots == null)
			return;

		int contributors = Mathf.Max(2, info.UniqueContributors);

		// Feel-specific cooldown refund: Detonation gets an extra burst to re-fire everything.
		float cooldownRefund = Mathf.Clamp(0.28f + 0.045f * contributors, 0.28f, 0.52f);
		if (feel == SurgeDifferentiation.GlobalSurgeFeel.Detonation)
			cooldownRefund = Mathf.Min(0.65f, cooldownRefund + Balance.DetonationSurgeCooldownBonus);

		// Feel-specific damage multiplier applied to per-tower spectacle payloads.
		float damageMult = feel switch
		{
			SurgeDifferentiation.GlobalSurgeFeel.Pressure   => Balance.PressureSurgeDamageMult,
			SurgeDifferentiation.GlobalSurgeFeel.Detonation => Balance.DetonationSurgeDamageMult,
			_                                               => 1.00f,
		};
		float perTowerScale = Mathf.Clamp(0.78f + 0.09f * contributors, 0.78f, 1.16f) * damageMult;

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

		// Base mark/slow durations.
		float markDuration = 2.4f + 0.32f * contributors;
		float slowDuration = 2.0f + 0.28f * contributors;
		float slowFactor   = Mathf.Clamp(0.86f - 0.065f * contributors, 0.54f, 0.88f);

		// Feel-specific status adjustments.
		switch (feel)
		{
			case SurgeDifferentiation.GlobalSurgeFeel.Pressure:
				// Sustained control: significantly longer and deeper mark + slow.
				markDuration *= Balance.PressureSurgeMarkMult;
				slowDuration *= Balance.PressureSurgeSlowMult;
				slowFactor    = Mathf.Max(0.36f, slowFactor - Balance.PressureSurgeSlowBonus);
				break;
			case SurgeDifferentiation.GlobalSurgeFeel.Detonation:
				// Burst focus: shorter status windows (enemies should be dead before they matter).
				markDuration *= Balance.DetonationSurgeMarkMult;
				slowDuration *= Balance.DetonationSurgeSlowMult;
				break;
			// Chain: normal status -- the value is in the spreading arcs, not the status depth.
		}

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
			const float secondaryWeight = 0.90f;
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
			float augmentScale = isMajor ? 2.25f : 1.65f;
			ApplyTriadAugmentSpectacleEffect(
				info,
				seededTargets,
				isMajor,
				effectScale * augmentScale,
				ResolveSpectacleColor(info.Signature.TertiaryModId));
		}
	}

	// ── Combo finisher helpers ────────────────────────────────────────────────

	/// <summary>
	/// CDR amount for a Reload-family modifier used as a finisher rider.
	/// FeedbackLoop > HairTrigger > Momentum, preserving identity within the family.
	/// </summary>
	private static float ResolveReloadCdr(string a, string b, float finisherPower)
	{
		bool hasFeedback = a == SpectacleDefinitions.FeedbackLoop || b == SpectacleDefinitions.FeedbackLoop;
		bool hasHair     = a == SpectacleDefinitions.HairTrigger  || b == SpectacleDefinitions.HairTrigger;
		float baseAmt    = hasFeedback ? 0.22f : hasHair ? 0.18f : 0.10f;
		float scaledAmt  = hasFeedback ? 0.15f : hasHair ? 0.13f : 0.12f;
		return baseAmt + scaledAmt * finisherPower;
	}

	/// <summary>
	/// Applies the combo finisher effect for a Combo or Triad surge.
	/// Dispatches on the primitive-family pair of r1 × r2 (18 cases) rather
	/// than on explicit modifier-pair string keys (was 45 cases).
	/// </summary>
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
		var tower = info.Tower;
		EnemyInstance? primary = seededTargets.FirstOrDefault(IsEnemyUsable);
		if (primary == null)
			return;

		float finisherPower = Mathf.Clamp(power * (isMajor ? 0.80f : 0.54f), 0.22f, 2.4f);
		Color secondaryColor = ResolveSpectacleColor(info.Signature.SecondaryModId);

		// ── Local helpers ───────────────────────────────────────────────────
		List<EnemyInstance> PickTargets(Vector2 center, float radius, int count, bool preferFront = false)
			=> GetSpectacleTargets(center, radius, count, preferFront).Where(IsEnemyUsable).ToList();

		void ApplyMarkMany(IEnumerable<EnemyInstance> targets, float duration)
		{ foreach (var e in targets) Statuses.ApplyMarked(e, duration); }

		void ApplySlowMany(IEnumerable<EnemyInstance> targets, float duration, float factor)
		{ foreach (var e in targets) Statuses.ApplySlow(e, duration, factor); }

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
					SpawnSpectacleArc(arcOrigin.Value, targets[i].GlobalPosition, color, intensity: 0.92f + i * 0.06f, mineChainStyle: heavy);
				ApplySpectacleDamage(tower, targets[i], baseDamage * falloff, color, heavyHit: heavy);
			}
		}

		// ── Primitive-pair dispatch ─────────────────────────────────────────
		SurgePrimitive pa = SpectacleDefinitions.PrimitiveOf(a);
		SurgePrimitive pb = SpectacleDefinitions.PrimitiveOf(b);
		// Normalize to (lo ≤ hi) so each pair has one canonical switch arm.
		SurgePrimitive lo = pa <= pb ? pa : pb;
		SurgePrimitive hi = pa <= pb ? pb : pa;

		// Per-pair modifiers that need mod-specific sub-selection.
		bool isMark   = a == SpectacleDefinitions.ExploitWeakness || b == SpectacleDefinitions.ExploitWeakness;
		bool isFarBurst = a == SpectacleDefinitions.Overreach || b == SpectacleDefinitions.Overreach;

		switch (lo, hi)
		{
			// ── Burst + Burst: wide reach wave (overkill|overreach) ─────────
			case (SurgePrimitive.Burst, SurgePrimitive.Burst):
			{
				var targets = _runState.EnemiesAlive
					.Where(IsEnemyUsable)
					.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= Mathf.Max(320f, tower.Range * 1.44f))
					.OrderByDescending(e => e.ProgressRatio)
					.Take(isMajor ? 4 : 3).ToList();
				DamageWave(targets, tower.BaseDamage * (isMajor ? 0.76f : 0.42f) * finisherPower, 0.14f, secondaryColor, isMajor, tower.GlobalPosition);
				break;
			}
			// ── Burst + Chain: spill then chain ─────────────────────────────
			case (SurgePrimitive.Burst, SurgePrimitive.Chain):
			{
				var spill = PickTargets(primary.GlobalPosition, 145f, isMajor ? 3 : 2).Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(spill, tower.BaseDamage * (isMajor ? 0.44f : 0.26f) * finisherPower, 0.18f, secondaryColor, false);
				ApplySpectacleChain(tower, primary,
					maxBounces: isMajor ? 4 : 2,
					startDamage: tower.BaseDamage * (isMajor ? 0.52f : 0.30f) * finisherPower,
					decay: 0.72f, linkRange: Mathf.Max(220f, tower.ChainRange * 1.10f),
					secondaryColor, heavy: isMajor);
				break;
			}
			// ── Burst + Beam: heavy beam strike then area spill ─────────────
			case (SurgePrimitive.Burst, SurgePrimitive.Beam):
			{
				float beam = tower.BaseDamage * (isMajor ? 1.24f : 0.80f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.26f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				var spill = PickTargets(primary.GlobalPosition, 150f, isMajor ? 3 : 2);
				DamageWave(spill, tower.BaseDamage * (isMajor ? 0.44f : 0.26f) * finisherPower, 0.20f, secondaryColor, false);
				break;
			}
			// ── Burst + Status: mark/slow pack then damage wave ─────────────
			case (SurgePrimitive.Burst, SurgePrimitive.Status):
			{
				float radius = isFarBurst ? Mathf.Max(300f, tower.Range * 1.38f) : 170f + 20f * finisherPower;
				int count = isMajor ? 4 : 3;
				var pack = isFarBurst
					? _runState.EnemiesAlive.Where(IsEnemyUsable)
						.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
						.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
						.ThenByDescending(e => e.ProgressRatio).Take(count).ToList()
					: PickTargets(primary.GlobalPosition, radius, count, preferFront: true);
				if (isMark)
					ApplyMarkMany(pack, (isMajor ? 3.6f : 2.5f) + 1.0f * finisherPower);
				else
					ApplySlowMany(pack, (isMajor ? 3.2f : 2.2f) + 0.8f * finisherPower,
						Mathf.Clamp(0.74f - 0.10f * finisherPower, 0.30f, 0.86f));
				DamageWave(pack, tower.BaseDamage * (isMajor ? 0.68f : 0.40f) * finisherPower, 0.15f,
					secondaryColor, isMajor, primary.GlobalPosition);
				break;
			}
			// ── Burst + Reload: CDR then area wave ──────────────────────────
			case (SurgePrimitive.Burst, SurgePrimitive.Reload):
			{
				ReduceTowerCooldown(tower, ResolveReloadCdr(a, b, finisherPower));
				float radius = isFarBurst ? Mathf.Max(300f, tower.Range * 1.44f) : 150f + 50f * finisherPower;
				var targets = isFarBurst
					? _runState.EnemiesAlive.Where(IsEnemyUsable)
						.Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= radius)
						.OrderByDescending(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
						.Take(isMajor ? 4 : 3).ToList()
					: PickTargets(primary.GlobalPosition, radius, isMajor ? 4 : 3);
				DamageWave(targets, tower.BaseDamage * (isMajor ? 0.76f : 0.44f) * finisherPower,
					0.14f, secondaryColor, isMajor, primary.GlobalPosition);
				break;
			}
			// ── Burst + Scatter: shards from primary ────────────────────────
			case (SurgePrimitive.Burst, SurgePrimitive.Scatter):
			{
				var shards = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.30f,
					isMajor ? 5 : 3, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(shards, tower.BaseDamage * (isMajor ? 0.74f : 0.44f) * finisherPower,
					0.12f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			// ── Beam + Status: mark/slow then heavy strike ──────────────────
			case (SurgePrimitive.Beam, SurgePrimitive.Status):
			{
				if (isMark)
					Statuses.ApplyMarked(primary, (isMajor ? 4.1f : 2.9f) + 1.2f * finisherPower);
				float beam = tower.BaseDamage * (isMajor ? 1.10f : 0.68f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.22f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor && isMark);
				if (!isMark)
				{
					var aura = PickTargets(primary.GlobalPosition, 165f, isMajor ? 4 : 3);
					ApplySlowMany(aura, (isMajor ? 3.0f : 2.2f) + 0.8f * finisherPower,
						Mathf.Clamp(0.72f - 0.10f * finisherPower, 0.26f, 0.86f));
				}
				break;
			}
			// ── Beam + Reload: CDR then heavy strike with follow-up ─────────
			case (SurgePrimitive.Beam, SurgePrimitive.Reload):
			{
				ReduceTowerCooldown(tower, ResolveReloadCdr(a, b, finisherPower));
				float beam = tower.BaseDamage * (isMajor ? 1.06f : 0.66f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.20f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, beam, secondaryColor, heavyHit: true, triggerHitStopOnKill: isMajor);
				if (tower.Cooldown <= Mathf.Max(0.06f, tower.AttackInterval * 0.08f))
					ApplySpectacleDamage(tower, primary, beam * 0.50f, secondaryColor, heavyHit: false);
				break;
			}
			// ── Beam + Chain: lance strike then chain from target ───────────
			case (SurgePrimitive.Beam, SurgePrimitive.Chain):
			{
				float lance = tower.BaseDamage * (isMajor ? 0.96f : 0.60f) * finisherPower;
				SpawnSpectacleArc(tower.GlobalPosition, primary.GlobalPosition, secondaryColor, intensity: 1.18f, mineChainStyle: true);
				ApplySpectacleDamage(tower, primary, lance, secondaryColor, heavyHit: true);
				ApplySpectacleChain(tower, primary,
					maxBounces: isMajor ? 4 : 2, startDamage: lance * 0.62f,
					decay: 0.76f, linkRange: Mathf.Max(220f, tower.ChainRange * 1.12f),
					secondaryColor, heavy: isMajor);
				break;
			}
			// ── Beam + Scatter: prism scatter from primary ──────────────────
			case (SurgePrimitive.Beam, SurgePrimitive.Scatter):
			{
				var prism = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.34f,
					isMajor ? 6 : 4, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(prism, tower.BaseDamage * (isMajor ? 0.66f : 0.40f) * finisherPower,
					0.11f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			// ── Status + Status: dual mark+slow pack (exploit|slow) ─────────
			case (SurgePrimitive.Status, SurgePrimitive.Status):
			{
				var hunted = PickTargets(primary.GlobalPosition, 185f, isMajor ? 4 : 3, preferFront: true);
				ApplyMarkMany(hunted, (isMajor ? 3.4f : 2.2f) + 1.0f * finisherPower);
				ApplySlowMany(hunted, (isMajor ? 2.8f : 2.0f) + 0.8f * finisherPower,
					Mathf.Clamp(0.76f - 0.10f * finisherPower, 0.28f, 0.86f));
				DamageWave(hunted, tower.BaseDamage * (isMajor ? 0.52f : 0.30f) * finisherPower,
					0.15f, secondaryColor, false);
				break;
			}
			// ── Status + Reload: CDR + mark execute or slow-bonus wave ──────
			case (SurgePrimitive.Status, SurgePrimitive.Reload):
			{
				float cdr = ResolveReloadCdr(a, b, finisherPower);
				if (isMark)
				{
					bool wasMarked = primary.IsMarked;
					Statuses.ApplyMarked(primary, (isMajor ? 3.8f : 2.6f) + 1.2f * finisherPower);
					float execute = tower.BaseDamage * (wasMarked
						? (isMajor ? 1.00f : 0.60f)
						: (isMajor ? 0.64f : 0.38f)) * finisherPower;
					ApplySpectacleDamage(tower, primary, execute, secondaryColor,
						heavyHit: isMajor, triggerHitStopOnKill: wasMarked && isMajor);
				}
				else
				{
					var frosted = PickTargets(primary.GlobalPosition, 180f, isMajor ? 4 : 3, preferFront: true);
					float sloFact = Mathf.Clamp(0.70f - 0.10f * finisherPower, 0.26f, 0.84f);
					float sloDur  = (isMajor ? 3.4f : 2.4f) + 0.9f * finisherPower;
					foreach (var e in frosted)
					{
						bool wasSlowed = e.SlowRemaining > 0f;
						Statuses.ApplySlow(e, sloDur, sloFact);
						float dmg = tower.BaseDamage * (wasSlowed
							? (isMajor ? 0.64f : 0.40f)
							: (isMajor ? 0.44f : 0.28f)) * finisherPower;
						ApplySpectacleDamage(tower, e, dmg, secondaryColor, heavyHit: false);
					}
				}
				ReduceTowerCooldown(tower, cdr);
				break;
			}
			// ── Status + Chain: status pack then chain arc ──────────────────
			case (SurgePrimitive.Status, SurgePrimitive.Chain):
			{
				float dur = (isMajor ? 3.6f : 2.4f) + 0.9f * finisherPower;
				if (isMark)
					ApplyMarkMany(PickTargets(primary.GlobalPosition, 220f, isMajor ? 4 : 3, preferFront: true), dur);
				else
					Statuses.ApplySlow(primary, dur, Mathf.Clamp(0.72f - 0.10f * finisherPower, 0.28f, 0.86f));
				ApplySpectacleChain(tower, primary,
					maxBounces: isMajor ? 4 : 2,
					startDamage: tower.BaseDamage * (isMajor ? 0.52f : 0.32f) * finisherPower,
					decay: 0.75f, linkRange: Mathf.Max(220f, tower.ChainRange * 1.10f),
					secondaryColor, heavy: isMajor);
				break;
			}
			// ── Status + Scatter: status scatter wave ───────────────────────
			case (SurgePrimitive.Status, SurgePrimitive.Scatter):
			{
				float range = Balance.SplitShotRange * 1.32f;
				float dur   = (isMajor ? 3.1f : 2.2f) + 0.8f * finisherPower;
				var targets = PickTargets(primary.GlobalPosition, range, isMajor ? 5 : 3, preferFront: true);
				if (isMark)
					ApplyMarkMany(targets, dur);
				else
					ApplySlowMany(targets, dur, Mathf.Clamp(0.72f - 0.08f * finisherPower, 0.30f, 0.88f));
				DamageWave(targets, tower.BaseDamage * (isMajor ? 0.56f : 0.32f) * finisherPower,
					0.12f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			// ── Reload + Reload: double CDR + heavy double-hit ──────────────
			case (SurgePrimitive.Reload, SurgePrimitive.Reload):
			{
				ReduceTowerCooldown(tower, 0.30f + 0.16f * finisherPower);
				float burst = tower.BaseDamage * (isMajor ? 0.74f : 0.44f) * finisherPower;
				ApplySpectacleDamage(tower, primary, burst, secondaryColor, heavyHit: isMajor);
				ApplySpectacleDamage(tower, primary, burst * 0.64f, secondaryColor, heavyHit: false);
				break;
			}
			// ── Reload + Chain: CDR then chain arc ──────────────────────────
			case (SurgePrimitive.Reload, SurgePrimitive.Chain):
			{
				ReduceTowerCooldown(tower, ResolveReloadCdr(a, b, finisherPower));
				ApplySpectacleChain(tower, primary,
					maxBounces: (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower),
					startDamage: tower.BaseDamage * (isMajor ? 0.60f : 0.35f) * finisherPower,
					decay: 0.74f, linkRange: Mathf.Max(220f, tower.ChainRange * 1.12f),
					secondaryColor, heavy: isMajor);
				break;
			}
			// ── Reload + Scatter: CDR then scatter wave ──────────────────────
			case (SurgePrimitive.Reload, SurgePrimitive.Scatter):
			{
				ReduceTowerCooldown(tower, ResolveReloadCdr(a, b, finisherPower));
				var bloom = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.38f,
					isMajor ? 6 : 4, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(bloom, tower.BaseDamage * (isMajor ? 0.64f : 0.37f) * finisherPower,
					0.11f, secondaryColor, false, primary.GlobalPosition);
				break;
			}
			// ── Chain + Scatter: chain then split scatter ────────────────────
			case (SurgePrimitive.Chain, SurgePrimitive.Scatter):
			{
				int bounces   = (isMajor ? 4 : 2) + Mathf.FloorToInt(finisherPower);
				float chainDmg = tower.BaseDamage * (isMajor ? 0.52f : 0.31f) * finisherPower;
				ApplySpectacleChain(tower, primary, maxBounces: bounces, startDamage: chainDmg,
					decay: 0.76f, linkRange: Mathf.Max(220f, tower.ChainRange * 1.15f),
					secondaryColor, heavy: isMajor);
				var extras = PickTargets(primary.GlobalPosition, Balance.SplitShotRange * 1.30f,
					isMajor ? 4 : 2, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary)).ToList();
				DamageWave(extras, chainDmg * 0.52f, 0.14f, secondaryColor, false, primary.GlobalPosition);
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
			case SpectacleDefinitions.BlastCore:
			{
				if (primary == null)
					break;
				float blast = tower.BaseDamage * (isMajor ? 0.62f : 0.34f) * p;
				float blastRadius = (isMajor ? 130f : 100f) + 40f * p;
				var blastTargets = GetSpectacleTargets(primary.GlobalPosition, blastRadius, isMajor ? 4 : 3, preferFront: false)
					.Where(e => !ReferenceEquals(e, primary))
					.ToList();
				for (int i = 0; i < blastTargets.Count; i++)
				{
					float falloff = Mathf.Max(0.50f, 1f - i * 0.20f);
					SpawnSpectacleArc(primary.GlobalPosition, blastTargets[i].GlobalPosition, accent, intensity: 0.92f + 0.08f * i);
					ApplySpectacleDamage(tower, blastTargets[i], blast * falloff, accent, heavyHit: isMajor);
				}
				break;
			}
			case SpectacleDefinitions.Wildfire:
			{
				// Conflagration: ignite enemies in extended range with chip damage and scorched slow.
				float burnRadius = tower.Range * (isMajor ? 1.35f : 1.15f);
				float chip       = tower.BaseDamage * (isMajor ? 0.28f : 0.15f) * p;
				float slowFactor = isMajor ? 0.68f : 0.80f;
				var burnTargets  = _runState.EnemiesAlive
					.Where(e => IsEnemyUsable(e) && tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= burnRadius)
					.Take(isMajor ? 5 : 3)
					.ToList();
				foreach (var enemy in burnTargets)
				{
					Statuses.ApplySlow(enemy, isMajor ? 3.5f : 2.0f, slowFactor);
					SpawnSpectacleArc(tower.GlobalPosition, enemy.GlobalPosition, accent, intensity: 0.85f);
					ApplySpectacleDamage(tower, enemy, chip, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleDefinitions.ReaperProtocol:
			{
				// Execution strike: heavy hit on the lowest-HP enemy in extended range.
				// Grants +1 life (capped at ReaperMaxLives) if the strike kills.
				float reach = tower.Range * (isMajor ? 1.45f : 1.20f);
				var execTarget = _runState.EnemiesAlive
					.Where(e => IsEnemyUsable(e) && tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= reach)
					.OrderBy(e => e.Hp)
					.FirstOrDefault();
				if (execTarget == null) break;
				float strike = tower.BaseDamage * (isMajor ? 2.4f : 1.5f) * p;
				SpawnSpectacleArc(tower.GlobalPosition, execTarget.GlobalPosition, accent, intensity: 1.35f, mineChainStyle: true);
				ApplySpectacleDamage(tower, execTarget, strike, accent, heavyHit: true, triggerHitStopOnKill: isMajor);
				// Life gain if the strike killed -- bypasses the per-wave kill cap (surge is its own payoff)
				if (execTarget.Hp <= 0f)
					NotifyReaperProtocolKill(tower);
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
		float strength = augment.Coefficient
			* (isMajor ? 1f : 0.68f)
			* Mathf.Max(0.15f, effectScale);
		float aug = Mathf.Clamp(strength, 0f, 0.95f);
		if (aug <= 0.001f)
			return;

		var tower = info.Tower;
		EnemyInstance? primary = seededTargets.FirstOrDefault(IsEnemyUsable);

		switch (augment.Kind)
		{
			case SpectacleAugmentKind.Area:
			{
				Vector2 center = primary?.GlobalPosition ?? tower.GlobalPosition;
				float radius = Mathf.Max(tower.Range * 1.25f, 220f);
				float pulse = tower.BaseDamage * (0.20f + 0.60f * aug);
				var targets = GetSpectacleTargets(center, radius, isMajor ? 4 : 2, preferFront: false);
				for (int i = 0; i < targets.Count; i++)
				{
					float falloff = Mathf.Max(0.55f, 1f - i * 0.18f);
					SpawnSpectacleArc(center, targets[i].GlobalPosition, accent, intensity: 0.85f);
					ApplySpectacleDamage(tower, targets[i], pulse * falloff, accent, heavyHit: false);
				}
				break;
			}
			case SpectacleAugmentKind.Strike:
			{
				var target = primary
					?? GetSpectacleTargets(tower.GlobalPosition, tower.Range * 1.45f, 1, preferFront: true).FirstOrDefault();
				if (target != null)
				{
					float beam = tower.BaseDamage * (0.40f + 1.30f * aug);
					SpawnSpectacleArc(tower.GlobalPosition, target.GlobalPosition, accent, intensity: 1.25f, mineChainStyle: true);
					ApplySpectacleDamage(tower, target, beam, accent, heavyHit: true, triggerHitStopOnKill: isMajor);
				}
				break;
			}
			case SpectacleAugmentKind.Reload:
			{
				ReduceTowerCooldown(tower, 0.10f + 0.36f * aug);
				if (primary != null && tower.Cooldown <= Mathf.Max(0.06f, tower.AttackInterval * 0.10f))
				{
					float follow = tower.BaseDamage * (0.18f + 0.60f * aug);
					ApplySpectacleDamage(tower, primary, follow, accent, heavyHit: false);
				}
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
			SpawnSpectacleBurstFx(origin, accent, major: true);
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

	/// <summary>
	/// Spawns a BlastCoreRing at the given world position. Ring is a short-lived expanding
	/// circle outline -- compact, distinct from chain arcs and spectacle burst effects.
	/// mechanicalRadius matches the actual damage area so the ring correctly represents reach.
	/// power [0..1] scales brightness and line weight only.
	/// </summary>
	private void SpawnBlastCoreRing(Vector2 origin, Color accent, float mechanicalRadius, float power = 0.5f)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		var ring = new BlastCoreRing();
		ring.GlobalPosition = Vector2.Zero;
		_worldNode.AddChild(ring);
		ring.Initialize(origin, accent, mechanicalRadius, power);
	}

	/// <summary>
	/// Spawns a damage number at reduced scale for secondary/area hits (Blast Core splash, fire trail).
	/// Visually distinct from primary tower-shot numbers: smaller font, same amber/fire color.
	/// </summary>
	private void SpawnSecondaryDamageNumber(Vector2 worldPos, float damage, bool isKill, Color color)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode)) return;
		if (damage < 1f) return;
		var num = new DamageNumber();
		_worldNode.AddChild(num);
		num.GlobalPosition = worldPos + new Vector2(0f, -14f);
		num.Initialize(damage, color, isKill, scale: 0.72f);
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

		_runState.TrackSpectacleDamage(tower.SlotIndex, dealtInt, isKill, source, killDepth);
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

	private void SpawnSpectacleBurstFx(Vector2 worldPos, Color accent, bool major, float power = 1f)
	{
		bool mobileLite = IsMobileSpectacleLite();
		bool emitSecondStage = SpectacleExplosionCore.ShouldEmitSecondStage(major)
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

	/// <summary>
	/// Spawns a flying energy pip from the surging tower to the Global Surge HUD bar.
	/// Capped at <see cref="Balance.SurgePipMaxActive"/> simultaneous pips so heavy combat
	/// stays readable. The pip lives in a screen-space CanvasLayer (layer 2) so it can
	/// cross from world coordinates to the HUD bar without coordinate-system hacks.
	/// </summary>
	private void SpawnSurgePip(
		Vector2 worldPos,
		Color accent,
		Vector2? explicitScreenTarget = null,
		SurgePip.SurgePipGlyph glyph = SurgePip.SurgePipGlyph.Orb,
		float coreScale = 1f,
		float glowScale = 1f,
		float lingerSec = -1f,
		float travelSec = -1f,
		float arcHeight = float.NaN,
		System.Action? onArrival = null,
		bool allowWhenGlobalReady = false)
	{
		if (_botRunner != null || !GodotObject.IsInstanceValid(_pipLayer))
			return;
		if (!allowWhenGlobalReady && _spectacleSystem.IsGlobalSurgeReady)
			return; // bar is full/ready -- no point flying more pips to it
		if (_activePipCount >= Balance.SurgePipMaxActive)
			return;

		Vector2 screenStart  = WorldToScreen(worldPos);
		Vector2 screenTarget = explicitScreenTarget ?? (_hudPanel.GetSurgeMeterViewportRect().Position + _hudPanel.GetSurgeMeterViewportRect().Size * 0.5f);

		_activePipCount++;
		var pip = new SurgePip();
		_pipLayer.AddChild(pip);
		pip.Initialize(
			screenStart  : screenStart,
			screenTarget : screenTarget,
			color        : accent,
			lingerSec    : lingerSec >= 0f ? lingerSec : Balance.SurgePipLingerSec,
			travelSec    : travelSec >= 0f ? travelSec : Balance.SurgePipTravelSec,
			arcHeight    : float.IsNaN(arcHeight) ? Balance.SurgePipArcHeight : arcHeight,
			onArrival    : () =>
			{
				_activePipCount = Mathf.Max(0, _activePipCount - 1);
				if (onArrival != null)
					onArrival();
				else if (GodotObject.IsInstanceValid(_hudPanel) && !_spectacleSystem.IsGlobalSurgeReady)
					_hudPanel.FlashPipArrival();
			},
			glyph: glyph,
			coreScale: coreScale,
			glowScale: glowScale);
	}

	private void SpawnGlobalSurgeRipples(
		Vector2 origin,
		Color[] colors,
		int contributors,
		float lingerMultiplier = 1f,
		SurgeDifferentiation.GlobalSurgeFeel feel = SurgeDifferentiation.GlobalSurgeFeel.Neutral)
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

		// Feel-specific ripple tuning:
		//   Pressure   – slow, wide rings (sustained atmospheric presence)
		//   Chain      – normal (balanced)
		//   Detonation – fast, far-reaching, sharp rings (explosive snap)
		float durationMult  = feel switch
		{
			SurgeDifferentiation.GlobalSurgeFeel.Pressure   => 1.60f,
			SurgeDifferentiation.GlobalSurgeFeel.Detonation => 0.55f,
			_                                               => 1.00f,
		};
		float endRadiusMult = feel == SurgeDifferentiation.GlobalSurgeFeel.Detonation ? 1.25f : 1.00f;
		float ringWidthMult = feel switch
		{
			SurgeDifferentiation.GlobalSurgeFeel.Pressure   => 1.50f,
			SurgeDifferentiation.GlobalSurgeFeel.Detonation => 0.75f,
			_                                               => 1.00f,
		};

		float contributorT = Mathf.Clamp((contributors - 1f) / 5f, 0f, 1f);
		int maxRipples = reducedMotion ? 1 : Mathf.Min(3, colors.Length > 1 ? colors.Length : 3);
		// Pressure gets an extra ripple for a broader, more saturating wash.
		if (!reducedMotion && feel == SurgeDifferentiation.GlobalSurgeFeel.Pressure)
			maxRipples = Mathf.Min(maxRipples + 1, 4);
		if (IsMobileSpectacleLite())
			maxRipples = Mathf.Min(maxRipples, 2);
		float baseDuration = Mathf.Lerp(0.62f, 0.86f, contributorT) * linger * durationMult;
		float adjustedEndRadius = endRadius * endRadiusMult;

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
					adjustedEndRadius * (0.90f + rippleIndex * 0.09f),
					durationSec: baseDuration + rippleIndex * 0.08f,
					ringWidth: (4.8f + rippleIndex * 1.0f) * ringWidthMult);
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
		if (_botRunner != null || _autoDraftMode || !GodotObject.IsInstanceValid(_spectaclePulse))
			return;

		float chaos = CombatVisualChaosLoad;
		double now = ResolveVisualFxClock();
		double minInterval = Mathf.Lerp(0.028f, 0.100f, chaos);
		if (now - _lastSpectacleScreenFlashAt < minInterval)
			return;
		_lastSpectacleScreenFlashAt = now;

		float chaosScale = Mathf.Lerp(1f, 0.76f, chaos);
		if (IsNonCriticalHintSuppressed())
			chaosScale *= 0.86f;
		peakAlpha = Mathf.Max(0.012f, peakAlpha * chaosScale);
		fadeSec = Mathf.Max(0.05f, fadeSec * Mathf.Lerp(1f, 0.88f, chaos));

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
		if (_botRunner != null || _autoDraftMode || !GodotObject.IsInstanceValid(_lingerTint))
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
		callout.ZAsRelative = false;
		callout.ZIndex = 60;
		callout.GlobalPosition = worldPos;
		callout.Initialize(
			$"SURGE ×{chainCount}",
			new Color(1.00f, 0.90f, 0.30f),
			duration: 1.8f,
			sizeOverride: 36);
	}

	private void FlashSpectacleAfterimage(Color accent, float strength)
	{
		if (_botRunner != null || _autoDraftMode || !GodotObject.IsInstanceValid(_spectacleAfterimage))
			return;

		float chaos = CombatVisualChaosLoad;
		double now = ResolveVisualFxClock();
		double minInterval = Mathf.Lerp(0.11f, 0.30f, chaos);
		if (now - _lastSpectacleAfterimageAt < minInterval)
			return;
		_lastSpectacleAfterimageAt = now;

		float s = Mathf.Clamp(strength, 0f, 1f);
		s *= Mathf.Lerp(1f, 0.72f, chaos);
		if (IsNonCriticalHintSuppressed())
			s *= 0.84f;
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
		int maxResidues = baseMaxResidues;
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
			TickInterval = Mathf.Max(0.06f, profile.TickIntervalSeconds),
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
			: UIStyle.TowerAccent(option.Id);

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
					"rocket_launcher" => "RL",
					"marker_tower" => "MK",
					"chain_tower" => "AR",
					"rift_prism" => "SA",
					"accordion_engine" => "AC",
					"phase_splitter" => "PS",
					"undertow_engine" => "UT",
					"latch_nest" => "LN",
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

	public void NotifyOverkillSpill(ITowerView sourceTower, Vector2 worldPos, float spillDamage, float dealtDamage)
	{
		if (CurrentPhase != GamePhase.Wave || spillDamage <= 0f)
			return;
		_screenshotPipeline?.NotifyOverkillSpill(spillDamage);

		// Track actual damage dealt (capped by target HP), not the attempted spill amount.
		TrackSpectacleDamage(sourceTower, dealtDamage, isKill: false, SpectacleDamageSource.ExplosionFollowUp);

		// Bloom threshold uses spillDamage (= excess * SpillEfficiency), which represents how large the overkill was.
		OverkillBloomProfile bloomProfile = SpectacleExplosionCore.BuildOverkillBloomProfile(spillDamage);
		if (bloomProfile.ShouldTrigger && sourceTower != null)
		{
			Color bloomColor = new Color(1.00f, 0.56f, 0.25f);
			SpawnOverkillBloom(sourceTower, worldPos, bloomProfile, bloomColor, heavySourceHit: spillDamage >= 42f);
		}

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
				power: 0.86f + spillT * 0.72f);
		}

		if (spillDamage < 34f) return;
		if (!TryCombatCallout("overkill_spill", 6.5f)) return;
		SpawnCombatCallout("OVERKILL SPILL", worldPos, spillColor);
	}

	public void NotifyMarkedEnemyPop(ITowerView sourceTower, IEnemyView deadEnemy, IEnumerable<IEnemyView> enemiesAlive)
	{
		if (CurrentPhase != GamePhase.Wave || sourceTower == null || deadEnemy == null || enemiesAlive == null)
			return;
		_screenshotPipeline?.NotifyMarkedEnemyPop();

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
			power: 0.92f + Mathf.Clamp(nearby.Count * 0.06f, 0f, 0.42f));

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

	/// <summary>
	/// Called by ReaperProtocol.OnKill after a valid primary kill within the per-wave cap.
	/// Increments lives (capped at MaxLives), refreshes HUD, plays sound, and spawns subtle VFX.
	/// Runs in both live and bot/headless modes -- only the VFX branch is skipped when headless.
	/// </summary>
	public void NotifyReaperProtocolKill(SlotTheory.Entities.ITowerView tower)
	{
		if (CurrentPhase != GamePhase.Wave) return;

		// Reaper Protocol can accumulate lives beyond the starting cap, up to LivesCeiling
		if (_runState.Lives >= _runState.LivesCeiling) return;

		_runState.Lives++;

		// Refresh HUD so the life counter updates immediately
		if (GodotObject.IsInstanceValid(_hudPanel))
			_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);

		// Notify music director (life gain = positive tension event)
		MusicDirector.Instance?.OnLivesChanged(_runState.Lives);

		// Sound: soft soul-collect chime, pitch-varied to avoid monotony at up to 5 procs/wave.
		// SoundManager._headless guard silences this automatically in headless/bot mode.
		float pitch = 0.90f + (float)GD.Randf() * 0.20f;
		SoundManager.Instance?.Play("life_gain", pitchScale: pitch);

		// VFX: subtle floating "+1" at the tower. SpawnCombatCallout already guards against
		// bot runner and invalid scene tree, so no explicit bot check needed here.
		Color reaperColor = new Color(0.22f, 0.90f, 0.62f);  // jade-teal matching ModifierVisuals accent
		SpawnCombatCallout("+1", tower.GlobalPosition, reaperColor,
			durationScale: 0.65f, yOffset: -14f, drift: true, holdPortion: 0.22f, sizeOverride: 14);
	}

	public void NotifyFeedbackLoopProc(SlotTheory.Entities.ITowerView tower)
	{
		if (_botRunner != null || CurrentPhase != GamePhase.Wave) return;
		_screenshotPipeline?.NotifyFeedbackLoop();

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
		_screenshotPipeline?.NotifyChainMaxBounce(bounceCount);
		if (!TryCombatCallout("chain_reaction", 7.0f)) return;
		SpawnCombatCallout("CHAIN REACTION", worldPos, new Color(0.56f, 0.95f, 1.00f));
	}

	/// <summary>
	/// Called by DamageModel when an Afterimage-enabled tower lands a valid primary hit.
	/// Delegates delayed imprint tracking and echo replay to CombatSim.
	/// </summary>
	public void NotifyAfterimageHit(ITowerView sourceTower, Vector2 impactPos, float sourceDamage)
	{
		if (CurrentPhase != GamePhase.Wave || sourceTower == null || _combatSim == null)
			return;

		_combatSim.QueueAfterimageImprint(sourceTower, impactPos, sourceDamage);
	}

	/// <summary>
	/// Called by DamageModel when a Deadzone-enabled tower lands a valid primary hit.
	/// Delegates zone placement and crossing detection to CombatSim.
	/// </summary>
	public void NotifyDeadzoneHit(ITowerView sourceTower, Vector2 impactPos, float sourceDamage)
	{
		if (CurrentPhase != GamePhase.Wave || sourceTower == null || _combatSim == null)
			return;

		_combatSim.QueueDeadzone(sourceTower, impactPos, sourceDamage);
	}

	/// <summary>
	/// Called by BlastCore.OnHit after splash damage has been applied to splashTargets.
	/// Handles visual feedback (expanding ring + impact sparks) and spectacle damage tracking.
	/// No-op in bot/headless mode -- damage is already applied, only visuals are skipped.
	/// </summary>
	public void NotifyBlastCoreSplash(
		ITowerView sourceTower,
		Vector2 origin,
		float splashDamage,
		System.Collections.Generic.IReadOnlyList<IEnemyView> splashTargets,
		float mechanicalRadius = SlotTheory.Core.Balance.BlastCoreRadius)
	{
		if (CurrentPhase != GamePhase.Wave) return;
		_screenshotPipeline?.NotifyBlastCoreSplash(splashTargets?.Count ?? 0);

		// Warm amber -- distinct from chain cyan (0.56,0.95,1) and overkill orange (1,0.56,0.25).
		Color blastColor = new Color(1.00f, 0.72f, 0.22f);
		float power = Mathf.Clamp(splashDamage / 60f, 0f, 1f);

		if (_botRunner == null && GodotObject.IsInstanceValid(_worldNode))
		{
			// Detonation ring always fires -- gives the player feedback that Blast Core is active
			// even when no enemies are in splash range (common early-wave when enemies are spread out).
			SpawnBlastCoreRing(origin, blastColor, mechanicalRadius, power);
		}

		// Everything below requires actual splash targets.
		if (splashTargets == null || splashTargets.Count == 0) return;

		// Attribute splash damage to the source tower for spectacle/tuning stat tracking.
		float totalSplash = splashDamage * splashTargets.Count;
		TrackSpectacleDamage(sourceTower, totalSplash, isKill: false, SpectacleDamageSource.ExplosionFollowUp);

		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		// Central impact flash at the detonation origin.
		SpawnSpectacleImpactSparks(origin, blastColor, heavy: splashDamage >= 40f);

		// Smaller hit sparks + secondary damage numbers at each affected enemy position.
		// Note: use a plain IsInstanceValid check rather than IsEnemyUsable here --
		// IsEnemyUsable requires Hp > 0 but splash damage is applied before this notify,
		// so a splash-killed enemy already has Hp = 0 and must still show its damage number.
		foreach (var target in splashTargets)
		{
			if (target is EnemyInstance enemy && GodotObject.IsInstanceValid(enemy))
			{
				SpawnSpectacleImpactSparks(enemy.GlobalPosition, blastColor, heavy: false);
				SpawnSecondaryDamageNumber(enemy.GlobalPosition, splashDamage, enemy.Hp <= 0f, blastColor);
			}
		}

		// Combat callout when blast catches 2+ enemies.
		if (splashTargets.Count >= 2 && TryCombatCallout("blast_core", 5.5f))
			SpawnCombatCallout("BLAST CORE", origin, blastColor);
	}

	/// <summary>
	/// Called by Rocket Launcher native splash after radial damage is applied.
	/// Handles ring/readability feedback and secondary hit numbers.
	/// </summary>
	public void NotifyRocketSplash(
		ITowerView sourceTower,
		Vector2 origin,
		float splashDamage,
		System.Collections.Generic.IReadOnlyList<IEnemyView> splashTargets,
		float mechanicalRadius,
		bool burstCoreEnhanced)
	{
		if (CurrentPhase != GamePhase.Wave)
			return;

		Color blastColor = sourceTower is TowerInstance towerNode
			? towerNode.ProjectileColor
			: new Color(1.00f, 0.56f, 0.16f);
		float power = Mathf.Clamp(splashDamage / 45f, 0f, 1f);

		if (_botRunner == null && GodotObject.IsInstanceValid(_worldNode))
		{
			SpawnBlastCoreRing(origin, blastColor, mechanicalRadius, power);
		}

		if (splashTargets == null || splashTargets.Count == 0)
			return;

		float totalSplash = splashDamage * splashTargets.Count;
		TrackSpectacleDamage(sourceTower, totalSplash, isKill: false, SpectacleDamageSource.ExplosionFollowUp);

		if (_botRunner != null || !GodotObject.IsInstanceValid(_worldNode))
			return;

		SpawnSpectacleImpactSparks(origin, blastColor, heavy: true);
		foreach (IEnemyView target in splashTargets)
		{
			if (target is EnemyInstance enemy && GodotObject.IsInstanceValid(enemy))
			{
				SpawnSpectacleImpactSparks(enemy.GlobalPosition, blastColor, heavy: false);
				SpawnSecondaryDamageNumber(enemy.GlobalPosition, splashDamage, enemy.Hp <= 0f, blastColor);
			}
		}

		if (burstCoreEnhanced && splashTargets.Count >= 2 && TryCombatCallout("rocket_launcher_blast", 6.0f))
			SpawnCombatCallout("ROCKET BLAST", origin, blastColor);
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

		if (localSubmit.PreviousBest != null)
		{
			int delta = localSubmit.Score - localSubmit.PreviousBest.Score;
			string sign = delta >= 0 ? "+" : "\u2212";
			return $"Score: {localSubmit.Score:N0}  |  {sign}{System.Math.Abs(delta):N0} vs. your best";
		}

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
		GD.Print($"[Leaderboards] Submit result: {result.State} - {result.Message}");

		string? gapSuffix = null;
		if (result.State == GlobalSubmitState.Submitted && result.Rank.HasValue && result.Rank.Value > 1)
		{
			var entryAbove = await manager.GetEntryAtRankAsync(payload.MapId, payload.Difficulty, result.Rank.Value - 1);
			if (entryAbove != null)
			{
				int gap = entryAbove.Score - ScoreCalculator.ComputeScore(payload);
				if (gap > 0)
					gapSuffix = $"- {gap:N0} from #{result.Rank.Value - 1}";
			}
		}

		string line = BuildGlobalLeaderboardLine(localLine, result, gapSuffix);
		bool isError = result.State == GlobalSubmitState.Failed;
		CallDeferred(nameof(ApplyLeaderboardStatus), line, isError);
	}

	private void ApplyLeaderboardStatus(string line, bool isError)
	{
		if (!GodotObject.IsInstanceValid(_endScreen) || !_endScreen.Visible) return;
		_endScreen.SetLeaderboardStatus(line, isError);
	}

	private static string BuildGlobalLeaderboardLine(string localLine, GlobalSubmitResult result, string? gapSuffix = null)
	{
		string globalText = result.State switch
		{
			GlobalSubmitState.Submitted when result.Rank.HasValue
				=> $"Global ({result.Provider}): rank #{result.Rank.Value}{(gapSuffix != null ? $"  {gapSuffix}" : "")}",
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

	private void ApplySurgeProfileToEndScreen()
	{
		if (_surgeArchetypeCounts.Count == 0) return;
		var best = _surgeArchetypeCounts.OrderByDescending(kvp => kvp.Value).First();
		_endScreen.SetSurgeProfile(best.Key, best.Value);
	}

	private string? FinalizeSurgeHintingRun(bool won)
	{
		if (_botRunner != null || _runState == null || SettingsManager.Instance == null)
			return null;

		if (!won)
		{
			_runState.SurgeHintTelemetry.LostWithGlobalReadyUnused =
				_runState.SurgeHintTelemetry.GlobalsBecameReady > _runState.SurgeHintTelemetry.GlobalsActivated
				|| (_globalReadyWindowOpen && _spectacleSystem.IsGlobalSurgeReady);
		}

		_globalReadyWindowOpen = false;
		_globalReadySincePlayTime = 0f;
		_globalActivateHintToken++;
		if (GodotObject.IsInstanceValid(_hudPanel))
			_hudPanel.SetPersistentSurgeHint(null);

		var profile = SettingsManager.Instance.SurgeHintProfile;
		string? surgeTipText = null;
		if (!won)
		{
			var tip = SurgeHintAdvisor.SelectPostLossTip(_runState.SurgeHintTelemetry, profile);
			if (tip.HasValue)
			{
				surgeTipText = tip.Value.Text;
				SurgeHintAdvisor.RecordPostLossTipDisplayed(profile, tip.Value.Id);
			}
		}

		SurgeHintAdvisor.ApplyRunOutcome(profile, _runState.SurgeHintTelemetry, won);
		SettingsManager.Instance.SaveSurgeHintingProgress();
		return surgeTipText;
	}

	private static string? MergeEndScreenHints(string? surgeHint, string? goalHint)
	{
		bool hasSurge = !string.IsNullOrWhiteSpace(surgeHint);
		bool hasGoal = !string.IsNullOrWhiteSpace(goalHint);
		if (hasSurge && hasGoal)
		{
			if (string.Equals(surgeHint, goalHint, System.StringComparison.Ordinal))
				return surgeHint;
			return $"{surgeHint}\n{goalHint}";
		}

		if (hasSurge) return surgeHint;
		if (hasGoal) return goalHint;
		return null;
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

	private static int ResolveMapTotalWaves(string? mapId)
	{
		if (mapId == "tutorial")
		{
			try
			{
				var def = Data.DataLoader.GetMapDef("tutorial");
				if (def.TutorialWaves != null) return def.TutorialWaves.Length;
			}
			catch { }
		}
		return Balance.TotalWaves;
	}

	private int GuessCurrentWaveReached()
	{
		int waveReached = CurrentPhase switch
		{
			GamePhase.Wave => _runState.WaveIndex + 1,
			GamePhase.Loss => _runState.WaveIndex + 1,
			GamePhase.Win => _mapTotalWaves,
			_ => _runState.WaveIndex,
		};
		return System.Math.Clamp(waveReached, 0, _mapTotalWaves);
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
		_screenshotPipeline?.NotifyWaveStart(_runState.WaveIndex);
		int waveNumber = _runState.WaveIndex + 1;
		_tutorialManager?.OnWaveStarted(_runState.WaveIndex);
		if (_tutorialManager != null && _runState.WaveIndex == 2)
			ShowTutorialTargetingPanel();
		// Tutorial: pre-fill global meter to 99% at wave 7 so it fills naturally during combat.
		if (_tutorialManager != null && _runState.WaveIndex == 6)
			_spectacleSystem.SetGlobalMeterFraction(0.99f);
		if (_botRunner == null) ShowWaveAnnouncement(waveNumber);
		WaveConfig? nextCfg = _runState.WaveIndex < _mapTotalWaves
			? DataLoader.GetWaveConfig(
				_runState.WaveIndex,
				SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy,
				_runState.SelectedMapId)
			: null;
		bool clumpedArmored = (nextCfg?.ClumpArmored ?? false) && (nextCfg?.TankyCount ?? 0) >= 2;
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
			AchievementManager.Instance?.CheckHalfwayThere();
		}
		if (waveNumber >= _mapTotalWaves && !_runState.IsEndlessMode && _tutorialManager == null)
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
		MusicDirector.Instance?.OnWaveStart(waveNumber, _runState.Lives);
		_hudPanel.Refresh(_runState.WaveIndex + 1, _runState.Lives);
		string runName = BuildRunName();
		var runColors = BuildRunNameColors();
		_hudPanel.SetBuildName(runName, visible: true, startColor: runColors.start, endColor: runColors.end);
		SaveMobileRunSnapshot("start_wave");
	}

	private void ShowBuildNameTutorial()
	{
		SettingsManager.Instance?.MarkBuildNameTutorialSeen();
		GetTree().Paused = true;

		var overlay = new CanvasLayer { Layer = 6, ProcessMode = ProcessModeEnum.Always };
		GetTree().Root.AddChild(overlay);

		var blocker = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.80f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(blocker);

		// Raise HUD so the build name label renders above the blocker
		_hudPanel.SetBuildLabelForcedVisible(true);

		// Highlight rect around the build name label
		Rect2 labelRect = _hudPanel.GetBuildLabelViewportRect();
		const float pad = 6f;
		var highlight = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.92f),
			Width = 2.5f,
			Antialiased = true,
		};
		highlight.Points = new Vector2[]
		{
			new Vector2(labelRect.Position.X - pad, labelRect.Position.Y - pad),
			new Vector2(labelRect.End.X + pad,      labelRect.Position.Y - pad),-
			new Vector2(labelRect.End.X + pad,      labelRect.End.Y + pad),
			new Vector2(labelRect.Position.X - pad, labelRect.End.Y + pad),
			new Vector2(labelRect.Position.X - pad, labelRect.Position.Y - pad),
		};
		overlay.AddChild(highlight);

		// Connector line from banner top down to label bottom
		const float bannerTopY = 56f;
		float labelCx = labelRect.GetCenter().X;
		var connector = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.80f),
			Width = 2f,
			Antialiased = true,
		};
		connector.Points = new Vector2[]
		{
			new Vector2(labelCx, bannerTopY),
			new Vector2(labelCx, labelRect.End.Y + 6f),
		};
		overlay.AddChild(connector);

		// Banner panel
		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(0.20f, 0.95f, 1.00f, 0.90f),
			ShadowColor = new Color(0.20f, 0.95f, 1.00f, 0.35f),
			ShadowSize  = 10,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft   = 16;
		bannerStyle.ContentMarginRight  = 16;
		bannerStyle.ContentMarginTop    = 12;
		bannerStyle.ContentMarginBottom = 12;

		var banner = new PanelContainer
		{
			AnchorLeft   = 0f, AnchorRight  = 0f,
			AnchorTop    = 0f, AnchorBottom = 0f,
			GrowHorizontal = Control.GrowDirection.End,
			GrowVertical   = Control.GrowDirection.End,
			OffsetLeft   = 10f, OffsetRight  = 10f,
			OffsetTop    = bannerTopY, OffsetBottom = bannerTopY,
		};
		banner.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(banner);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		banner.AddChild(vbox);

		var header = new Label { Text = "BUILD NAME" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.AddThemeColorOverride("font_color", UITheme.Lime);
		vbox.AddChild(header);

		var body = new Label
		{
			Text = "This name updates every wave to describe your current strategy - based on which tower is dealing the most damage, the modifier types you're stacking (damage, cryo, chain, etc.), and how fast you're clearing waves.\nWatch it evolve as your build takes shape.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(520f, 0f),
		};
		UITheme.ApplyFont(body, size: 14);
		body.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(body);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 20);
		var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		btnRow.AddChild(spacer);

		var gotItBtn = new Button { Text = "Got it", ProcessMode = ProcessModeEnum.Always };
		gotItBtn.CustomMinimumSize = new Vector2(90f, 0f);
		gotItBtn.AddThemeFontSizeOverride("font_size", 13);
		UITheme.ApplyPrimaryStyle(gotItBtn);
		gotItBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		gotItBtn.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			_hudPanel.SetBuildLabelForcedVisible(false);
			overlay.QueueFree();
			GetTree().Paused = false;
		};
		btnRow.AddChild(gotItBtn);
		vbox.AddChild(btnRow);
	}

	/// <summary>
	/// Tutorial-mode build name panel. Like ShowBuildNameTutorial but called during draft phase
	/// (no need to pause - the wave isn't running). Highlights the build label above the draft panel.
	/// </summary>
	private void ShowTutorialBuildNamePanel()
	{
		GetTree().Paused = true;

		var overlay = new CanvasLayer { Layer = 8, ProcessMode = ProcessModeEnum.Always };
		GetTree().Root.AddChild(overlay);

		var blocker = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.80f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(blocker);

		// Raise HUD so the build name label renders above the blocker
		_hudPanel.SetBuildLabelForcedVisible(true);

		// Open-right bracket highlight: top, left edge, bottom - no right side because name length is variable.
		Rect2 labelRect = _hudPanel.GetBuildLabelViewportRect();
		const float pad = 6f;
		float x  = labelRect.Position.X - pad;
		float y  = labelRect.Position.Y - pad;
		float b  = labelRect.End.Y + pad;
		float rx = labelRect.End.X + pad; // right edge - only used for short top/bottom caps
		const float capLen = 18f;
		var highlight = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.92f),
			Width = 2.5f,
			Antialiased = true,
		};
		// Top cap → left edge → bottom cap (open on the right)
		highlight.Points = new Vector2[]
		{
			new Vector2(rx,      y),   // top-right cap start (short)
			new Vector2(rx - capLen, y),
			new Vector2(x, y),         // top-left corner
			new Vector2(x, b),         // bottom-left corner
			new Vector2(rx - capLen, b),
			new Vector2(rx,      b),   // bottom-right cap end (short)
		};
		overlay.AddChild(highlight);

		// Connector line from banner top down to label bottom
		const float bannerTopY = 56f;
		float labelCx = labelRect.GetCenter().X;
		var connector = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.80f),
			Width = 2f,
			Antialiased = true,
		};
		connector.Points = new Vector2[]
		{
			new Vector2(labelCx, bannerTopY),
			new Vector2(labelCx, labelRect.End.Y + 6f),
		};
		overlay.AddChild(connector);

		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(0.20f, 0.95f, 1.00f, 0.90f),
			ShadowColor = new Color(0.20f, 0.95f, 1.00f, 0.35f),
			ShadowSize  = 10,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft = 12; bannerStyle.ContentMarginRight  = 12;
		bannerStyle.ContentMarginTop  = 10; bannerStyle.ContentMarginBottom = 10;

		var banner = new PanelContainer
		{
			AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 0f,
			GrowHorizontal = Control.GrowDirection.End, GrowVertical = Control.GrowDirection.End,
			OffsetLeft = 10f, OffsetRight = 10f, OffsetTop = bannerTopY, OffsetBottom = bannerTopY,
		};
		banner.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(banner);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 5);
		banner.AddChild(vbox);

		var header = new Label { Text = "BUILD NAME" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.AddThemeColorOverride("font_color", UITheme.Lime);
		vbox.AddChild(header);

		var body = new Label
		{
			Text = "Your build now has a name - it describes your current strategy based on which towers are dealing the most damage and which modifier types you're stacking.\nWatch it evolve as your build takes shape.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(480f, 0f),
		};
		UITheme.ApplyFont(body, size: 14);
		body.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(body);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 20);
		btnRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		var gotItBtn = new Button { Text = "Got it", ProcessMode = ProcessModeEnum.Always };
		gotItBtn.CustomMinimumSize = new Vector2(90f, 0f);
		gotItBtn.AddThemeFontSizeOverride("font_size", 13);
		UITheme.ApplyPrimaryStyle(gotItBtn);
		gotItBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		gotItBtn.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			_hudPanel.SetBuildLabelForcedVisible(false);
			overlay.QueueFree();
			GetTree().Paused = false;
		};
		btnRow.AddChild(gotItBtn);
		vbox.AddChild(btnRow);
	}

	/// <summary>
	/// Tutorial-mode targeting panel. Pauses the wave and explains targeting modes.
	/// Dismissed when the player picks a targeting icon from a tower's mode panel.
	/// </summary>
	private void ShowTutorialTargetingPanel()
	{
		if (_awaitingTargetingCycleDismiss) return;
		GetTree().Paused = true;
		ProcessMode = ProcessModeEnum.Always; // keep _Input alive so tower clicks register

		var overlay = new CanvasLayer { Layer = 21, ProcessMode = ProcessModeEnum.Always };
		_tutorialTargetingOverlay = overlay;
		GetTree().Root.AddChild(overlay);

		var vpSize = GetViewport().GetVisibleRect().Size;

		// Darken the scene, but keep a clear window around the tutorial tower and
		// nearby targeting icon panel so both remain fully readable.
		Vector2? focusTowerPos = null;
		if (_runState != null)
		{
			for (int i = 0; i < _runState.Slots.Length; i++)
			{
				var tower = _runState.Slots[i].TowerNode;
				if (tower == null || !GodotObject.IsInstanceValid(tower))
					continue;
				focusTowerPos = tower.GlobalPosition;
				break;
			}
		}

		if (focusTowerPos.HasValue)
		{
			// Tight focus around the tutorial tower and targeting icon column.
			var holeRect = new Rect2(focusTowerPos.Value - new Vector2(92f, 64f), new Vector2(184f, 128f));
			float left = Mathf.Clamp(holeRect.Position.X, 0f, vpSize.X);
			float top = Mathf.Clamp(holeRect.Position.Y, 0f, vpSize.Y);
			float right = Mathf.Clamp(holeRect.End.X, 0f, vpSize.X);
			float bottom = Mathf.Clamp(holeRect.End.Y, 0f, vpSize.Y);
			var dimColor = new Color(0f, 0f, 0f, 0.72f);

			void AddDimRect(float x, float y, float w, float h)
			{
				if (w <= 0f || h <= 0f)
					return;
				var r = new ColorRect
				{
					Color = dimColor,
					Position = new Vector2(x, y),
					Size = new Vector2(w, h),
					MouseFilter = Control.MouseFilterEnum.Ignore,
				};
				overlay.AddChild(r);
			}

			// Top / bottom bands.
			AddDimRect(0f, 0f, vpSize.X, top);
			AddDimRect(0f, bottom, vpSize.X, vpSize.Y - bottom);
			// Left / right bands around the tower window.
			AddDimRect(0f, top, left, bottom - top);
			AddDimRect(right, top, vpSize.X - right, bottom - top);
		}
		else
		{
			var backdrop = new ColorRect
			{
				Color = new Color(0f, 0f, 0f, 0.72f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			overlay.AddChild(backdrop);
		}

		float panelW = Mathf.Min(520f, vpSize.X - 32f);

		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(0.20f, 0.95f, 1.00f, 0.80f),
			ShadowColor = new Color(0.10f, 0.70f, 1.00f, 0.25f),
			ShadowSize  = 8,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft = 18; bannerStyle.ContentMarginRight  = 18;
		bannerStyle.ContentMarginTop  = 14; bannerStyle.ContentMarginBottom = 14;

		var panel = new PanelContainer
		{
			Position = new Vector2((vpSize.X - panelW) / 2f, 24f),
			CustomMinimumSize = new Vector2(panelW, 0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		panel.AddChild(vbox);

		var header = new Label { Text = "TARGETING MODE" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.AddThemeColorOverride("font_color", new Color(0.20f, 0.95f, 1.00f));
		vbox.AddChild(header);

		var body = new Label
		{
			Text = "Each tower has a target icon showing how it picks enemies.\n- First: attacks the enemy furthest along the path.\n- Strongest: attacks the highest HP enemy in range.\n- Lowest HP: focuses the weakest enemy to finish it fast.\n- Last: attacks the enemy least along the path.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(body, size: 14);
		body.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(body);

		var actionLine = new Label
		{
			Text = "Click your tower, then click one icon in its panel to set targeting mode.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		UITheme.ApplyFont(actionLine, semiBold: true, size: 14);
		actionLine.AddThemeColorOverride("font_color", new Color(0.94f, 0.98f, 1.00f));
		vbox.AddChild(actionLine);

		_awaitingTargetingCycleDismiss = true;
	}

	private void DismissTutorialTargetingPanel()
	{
		if (!_awaitingTargetingCycleDismiss) return;
		_awaitingTargetingCycleDismiss = false;
		if (_tutorialTargetingOverlay != null && GodotObject.IsInstanceValid(_tutorialTargetingOverlay))
			_tutorialTargetingOverlay.QueueFree();
		_tutorialTargetingOverlay = null;
		ProcessMode = ProcessModeEnum.Inherit;
		GetTree().Paused = false;
	}

	/// <summary>
	/// Tutorial-mode surge panel. Pauses the wave, forces the global surge meter visible,
	/// and shows a blocking overlay with a highlight rect around the surge bar.
	/// </summary>
	private void ShowTutorialSurgePanel()
	{
		GetTree().Paused = true;
		_hudPanel.SetSurgeMeterForcedVisible(true);

		var overlay = new CanvasLayer { Layer = 21, ProcessMode = ProcessModeEnum.Always };
		GetTree().Root.AddChild(overlay);

		var blocker = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.75f),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(blocker);

		// Highlight rect around the global surge meter
		const float meterPad = 12f;
		Rect2 meter = _hudPanel.GetSurgeMeterViewportRect();
		var surgeHighlight = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.92f),
			Width = 2.5f,
			Antialiased = true,
		};
		surgeHighlight.Points = new Vector2[]
		{
			new Vector2(meter.Position.X - meterPad, meter.Position.Y - meterPad),
			new Vector2(meter.End.X + meterPad,       meter.Position.Y - meterPad),
			new Vector2(meter.End.X + meterPad,       meter.End.Y + meterPad),
			new Vector2(meter.Position.X - meterPad,  meter.End.Y + meterPad),
			new Vector2(meter.Position.X - meterPad,  meter.Position.Y - meterPad),
		};
		overlay.AddChild(surgeHighlight);

		// Banner anchored to bottom, above the surge meter
		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(0.20f, 0.95f, 1.00f, 0.90f),
			ShadowColor = new Color(0.20f, 0.95f, 1.00f, 0.35f),
			ShadowSize  = 10,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft = 12; bannerStyle.ContentMarginRight  = 12;
		bannerStyle.ContentMarginTop  = 12; bannerStyle.ContentMarginBottom = 12;

		var vpSize = GetViewport().GetVisibleRect().Size;
		float cx = meter.GetCenter().X;
		const float bannerW = 372f;
		float bannerLeft = Mathf.Clamp(cx - bannerW / 2f, 8f, vpSize.X - bannerW - 8f);

		var banner = new PanelContainer
		{
			AnchorTop    = 1f, AnchorBottom = 1f,
			AnchorLeft   = 0f, AnchorRight  = 0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical   = Control.GrowDirection.Begin,
			OffsetTop    = -82f,
			OffsetBottom = -82f,
			OffsetLeft   = bannerLeft,
			OffsetRight  = bannerLeft + bannerW,
		};
		banner.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(banner);

		// Connector from banner top to surge meter
		var connector = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.80f),
			Width = 2f,
			Antialiased = true,
		};
		connector.Points = new Vector2[]
		{
			new Vector2(cx, vpSize.Y - 82f),
			new Vector2(cx, meter.Position.Y - meterPad),
		};
		overlay.AddChild(connector);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		banner.AddChild(vbox);

		var header = new Label { Text = "SURGE" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.AddThemeColorOverride("font_color", UITheme.Lime);
		vbox.AddChild(header);

		var intro = new Label
		{
			Text = "Combat fills each tower's Surge meter.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0f, 0f),
		};
		UITheme.ApplyFont(intro, size: 14);
		intro.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(intro);

		var surgeModes = new Label
		{
			Text =
				"The surge category is set by your mods and the tower's identity:\n" +
				"- Spread: arcs through groups, triggers chain effects\n" +
				"- Burst: heavy single-target strike and execution\n" +
				"- Control: slows and suppresses nearby enemies\n" +
				"- Echo: repeat strikes with lingering damage",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		UITheme.ApplyFont(surgeModes, size: 13);
		surgeModes.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(surgeModes);

		var footerRow = new HBoxContainer();
		footerRow.AddThemeConstantOverride("separation", 12);
		footerRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var footerText = new Label
		{
			Text = "Tower surges charge the Global Surge bar below.\nWhen it's full, you can activate it.",
			AutowrapMode = TextServer.AutowrapMode.Off,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0f, 0f),
			VerticalAlignment = VerticalAlignment.Bottom,
		};
		UITheme.ApplyFont(footerText, size: 13);
		footerText.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		footerRow.AddChild(footerText);

		var gotItBtn = new Button { Text = "Got it", ProcessMode = ProcessModeEnum.Always };
		gotItBtn.CustomMinimumSize = new Vector2(118f, 38f);
		gotItBtn.AddThemeFontSizeOverride("font_size", 14);
		UITheme.ApplyPrimaryStyle(gotItBtn);
		gotItBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		gotItBtn.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			_hudPanel.SetSurgeMeterForcedVisible(false);
			overlay.QueueFree();
			GetTree().Paused = false;
		};
		footerRow.AddChild(gotItBtn);
		footerRow.AddChild(new Control { CustomMinimumSize = new Vector2(52f, 0f) });
		vbox.AddChild(footerRow);
	}

	/// <summary>
	/// Tutorial-only first-time panel when the global surge meter fills.
	/// Pauses the wave, highlights the surge bar, and has an "Activate →" button
	/// that fires the global surge and unpauses.
	/// </summary>
	private void ShowTutorialGlobalSurgeActivatePanel()
	{
		GetTree().Paused = true;
		// Raise HudPanel above the overlay (Layer=22) so the surge bar receives clicks directly.
		_hudPanel.SetSurgeMeterForcedVisible(true);
		_hudPanel.Layer = 22;

		var overlay = new CanvasLayer { Layer = 21, ProcessMode = ProcessModeEnum.Always };
		GetTree().Root.AddChild(overlay);

		// Dim backdrop - Ignore so clicks pass through to HudPanel above it.
		var blocker = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.75f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(blocker);

		// Highlight rect around the global surge meter
		const float pad = 12f;
		Rect2 meter = _hudPanel.GetSurgeMeterViewportRect();
		var highlight = new Line2D
		{
			DefaultColor = new Color(1.00f, 0.88f, 0.20f, 0.95f),
			Width = 3f,
			Antialiased = true,
		};
		highlight.Points = new Vector2[]
		{
			new Vector2(meter.Position.X - pad, meter.Position.Y - pad),
			new Vector2(meter.End.X + pad,       meter.Position.Y - pad),
			new Vector2(meter.End.X + pad,       meter.End.Y + pad),
			new Vector2(meter.Position.X - pad,  meter.End.Y + pad),
			new Vector2(meter.Position.X - pad,  meter.Position.Y - pad),
		};
		overlay.AddChild(highlight);

		// Banner centered on the surge meter's horizontal midpoint
		var vpSize = GetViewport().GetVisibleRect().Size;
		float cx = meter.GetCenter().X;
		const float bannerW = 552f;
		float bannerLeft = Mathf.Clamp(cx - bannerW / 2f, 8f, vpSize.X - bannerW - 8f);

		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(1.00f, 0.88f, 0.20f, 0.95f),
			ShadowColor = new Color(1.00f, 0.80f, 0.10f, 0.35f),
			ShadowSize  = 10,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft = 16; bannerStyle.ContentMarginRight  = 16;
		bannerStyle.ContentMarginTop  = 12; bannerStyle.ContentMarginBottom = 12;

		var banner = new PanelContainer
		{
			AnchorTop    = 1f, AnchorBottom = 1f,
			AnchorLeft   = 0f, AnchorRight  = 0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical   = Control.GrowDirection.Begin,
			OffsetTop    = -82f,
			OffsetBottom = -82f,
			OffsetLeft   = bannerLeft,
			OffsetRight  = bannerLeft + bannerW,
		};
		banner.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(banner);
		var connector = new Line2D
		{
			DefaultColor = new Color(1.00f, 0.88f, 0.20f, 0.80f),
			Width = 2f,
			Antialiased = true,
		};
		connector.Points = new Vector2[]
		{
			new Vector2(cx, vpSize.Y - 82f),
			new Vector2(cx, meter.Position.Y - pad),
		};
		overlay.AddChild(connector);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		banner.AddChild(vbox);

		var header = new Label { Text = "GLOBAL SURGE READY" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.AddThemeColorOverride("font_color", new Color(1.00f, 0.88f, 0.20f));
		vbox.AddChild(header);

		var body = new Label
		{
			Text = "Global Surge is ready.\nClick the glowing Global Surge bar below to trigger it.\nIt refunds all tower cooldowns and hits every enemy on the lane.\nClick it now to continue tutorial.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(520f, 0f),
		};
		UITheme.ApplyFont(body, size: 14);
		body.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(body);

		// Allow HudPanel to receive input while the tree is paused so the bar is clickable.
		_hudPanel.ProcessMode = ProcessModeEnum.Always;

		void OnSurgeBarClicked()
		{
			_hudPanel.GlobalSurgeActivateRequested -= OnSurgeBarClicked;
			_hudPanel.ProcessMode = ProcessModeEnum.Inherit;
			_hudPanel.Layer = 1; // restore normal layer
			_hudPanel.SetSurgeMeterForcedVisible(false);
			overlay.QueueFree();
			GetTree().Paused = false;
			// OnHudGlobalSurgeActivate (subscribed at init) has already fired SetGlobalSurgeReady(false)
			// and ActivateGlobalSurge() - nothing extra needed here.
		}
		_hudPanel.GlobalSurgeActivateRequested += OnSurgeBarClicked;
	}

	/// <summary>
	/// Tutorial-only speed prompt shown during wave 2 when few enemies remain.
	/// Pauses the wave, highlights the speed button, and waits for one speed click to continue.
	/// </summary>
	private void ShowTutorialSpeedButtonPanel()
	{
		GetTree().Paused = true;
		int previousHudLayer = _hudPanel.Layer;
		ProcessModeEnum previousHudProcessMode = _hudPanel.ProcessMode;
		_hudPanel.Layer = 22;
		_hudPanel.ProcessMode = ProcessModeEnum.Always;

		var overlay = new CanvasLayer { Layer = 21, ProcessMode = ProcessModeEnum.Always };
		GetTree().Root.AddChild(overlay);

		var blocker = new ColorRect
		{
			Color = new Color(0f, 0f, 0f, 0.75f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(blocker);

		const float pad = 10f;
		Rect2 speedRect = _hudPanel.GetSpeedButtonViewportRect();
		if (speedRect.Size.X < 1f || speedRect.Size.Y < 1f)
		{
			Vector2 vpFallback = GetViewport().GetVisibleRect().Size;
			speedRect = new Rect2(vpFallback.X - 156f, 8f, 68f, 34f);
		}

		var highlight = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.95f),
			Width = 3f,
			Antialiased = true,
		};
		highlight.Points = new Vector2[]
		{
			new Vector2(speedRect.Position.X - pad, speedRect.Position.Y - pad),
			new Vector2(speedRect.End.X + pad,      speedRect.Position.Y - pad),
			new Vector2(speedRect.End.X + pad,      speedRect.End.Y + pad),
			new Vector2(speedRect.Position.X - pad, speedRect.End.Y + pad),
			new Vector2(speedRect.Position.X - pad, speedRect.Position.Y - pad),
		};
		overlay.AddChild(highlight);

		Vector2 vpSize = GetViewport().GetVisibleRect().Size;
		float cx = speedRect.GetCenter().X;
		float bannerW = Mathf.Min(560f, vpSize.X - 16f);
		float bannerLeft = Mathf.Clamp(cx - bannerW * 0.5f, 8f, vpSize.X - bannerW - 8f);
		float bannerTop = Mathf.Clamp(speedRect.End.Y + 24f, 56f, vpSize.Y - 220f);

		var bannerStyle = new StyleBoxFlat
		{
			BgColor     = new Color(0.04f, 0.09f, 0.16f, 0.97f),
			BorderColor = new Color(0.20f, 0.95f, 1.00f, 0.90f),
			ShadowColor = new Color(0.20f, 0.95f, 1.00f, 0.35f),
			ShadowSize  = 10,
			ShadowOffset = Vector2.Zero,
		};
		bannerStyle.SetBorderWidthAll(2);
		bannerStyle.SetCornerRadiusAll(10);
		bannerStyle.ContentMarginLeft = 16;
		bannerStyle.ContentMarginRight = 16;
		bannerStyle.ContentMarginTop = 12;
		bannerStyle.ContentMarginBottom = 12;

		var banner = new PanelContainer
		{
			AnchorLeft = 0f,
			AnchorRight = 0f,
			AnchorTop = 0f,
			AnchorBottom = 0f,
			OffsetLeft = bannerLeft,
			OffsetRight = bannerLeft + bannerW,
			OffsetTop = bannerTop,
			OffsetBottom = bannerTop,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		banner.AddThemeStyleboxOverride("panel", bannerStyle);
		overlay.AddChild(banner);

		var connector = new Line2D
		{
			DefaultColor = new Color(0.20f, 0.95f, 1.00f, 0.80f),
			Width = 2f,
			Antialiased = true,
		};
		connector.Points = new Vector2[]
		{
			new Vector2(cx, speedRect.End.Y + pad),
			new Vector2(cx, bannerTop),
		};
		overlay.AddChild(connector);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		banner.AddChild(vbox);

		var header = new Label { Text = "GAME SPEED" };
		UITheme.ApplyFont(header, semiBold: true, size: 13);
		header.AddThemeColorOverride("font_color", UITheme.Lime);
		vbox.AddChild(header);

		var body = new Label
		{
			Text =
				"You can choose 1x, 2x, or 3x game speed.\n" +
				"Click the speed button now to continue tutorial.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(500f, 0f),
		};
		UITheme.ApplyFont(body, size: 14);
		body.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
		vbox.AddChild(body);

		void OnSpeedClicked()
		{
			_hudPanel.SpeedToggleRequested -= OnSpeedClicked;
			_hudPanel.ProcessMode = previousHudProcessMode;
			_hudPanel.Layer = previousHudLayer;
			overlay.QueueFree();
			GetTree().Paused = false;
		}

		_hudPanel.SpeedToggleRequested += OnSpeedClicked;
	}

	private void ShakeWorld()
	{
		var origin = _worldNode.Position;
		var tween = CreateTween();
		tween.TweenProperty(_worldNode, "position", origin + new Vector2( 4f,  2f), 0.04f);
		tween.TweenProperty(_worldNode, "position", origin + new Vector2(-4f, -3f), 0.04f);
		tween.TweenProperty(_worldNode, "position", origin + new Vector2( 3f, -2f), 0.04f);
		tween.TweenProperty(_worldNode, "position", origin + new Vector2(-2f,  3f), 0.04f);
		tween.TweenProperty(_worldNode, "position", origin,                         0.04f);
	}

	private void ShakeWorldMicro()
	{
		if (!Balance.EnableScreenShake || !GodotObject.IsInstanceValid(_worldNode))
			return;

		var origin = _worldNode.Position;
		var tween = CreateTween();
		tween.TweenProperty(_worldNode, "position", origin + new Vector2( 1.6f,  0.9f),  0.018f);
		tween.TweenProperty(_worldNode, "position", origin + new Vector2(-1.3f, -0.8f),  0.018f);
		tween.TweenProperty(_worldNode, "position", origin,                               0.028f);
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
		if (CurrentPhase != GamePhase.Draft || (!_draftPanel.IsAwaitingSlot && !_draftPanel.IsAwaitingTower && !_draftPanel.IsAwaitingPremiumTarget))
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
			"chain_reaction"   => tower.IsChainTower,
			"overkill" or "focus_lens" => tower.TowerId == "heavy_cannon"
				|| tower.TowerId == "rocket_launcher"
				|| tower.Modifiers.Any(m => m.ModifierId == "focus_lens")
				|| tower.BaseDamage >= 40f,
			// Overreach expands control radius; Blast Core capitalizes on re-clumped enemies after control effects.
			"overreach" => tower.TowerId == "accordion_engine" || tower.TowerId == "undertow_engine" || tower.TowerId == "rocket_launcher",
			"blast_core" => tower.TowerId == "accordion_engine" || tower.TowerId == "undertow_engine" || tower.TowerId == "rocket_launcher",
			"slow" => tower.TowerId == "undertow_engine" || tower.TowerId == "chain_tower" || tower.TowerId == "rapid_shooter" || tower.TowerId == "latch_nest",
			"wildfire" => tower.TowerId == "phase_splitter" || tower.TowerId == "rapid_shooter" || tower.TowerId == "rift_prism",
			"afterimage" => tower.TowerId == "undertow_engine" || tower.TowerId == "rocket_launcher" || tower.TowerId == "chain_tower" || tower.TowerId == "phase_splitter" || tower.TowerId == "latch_nest",
			_ => false,
		};
	}

	public void NotifyModifierProc(SlotTheory.Entities.ITowerView tower, string modifierId)
	{
		if (tower == null || string.IsNullOrEmpty(modifierId))
			return;

		if (_modifierProcCounts.TryGetValue(modifierId, out int n))
			_modifierProcCounts[modifierId] = n + 1;
		else
			_modifierProcCounts[modifierId] = 1;

		// CombatLab/headless runs can invoke modifier logic without full view-state setup.
		// In those contexts we still track proc counts, but skip visual pulse side effects.
		if (_runState == null || _runState.Slots == null)
			return;

		int slot = -1;
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			if (ReferenceEquals(_runState.Slots[i].Tower, tower)) { slot = i; break; }
		}
		if (slot < 0) return;
		if (slot >= _slotProcHaloRemaining.Length || slot >= _slotProcHaloColor.Length)
			return;
		if (slot >= _slotModIconPulseRemaining.GetLength(0))
			return;

		_slotProcHaloRemaining[slot] = Mathf.Max(_slotProcHaloRemaining[slot], 0.20f);
		_slotProcHaloColor[slot] = ModifierVisuals.GetAccent(modifierId);

		for (int p = 0; p < tower.Modifiers.Count; p++)
		{
			if (tower.Modifiers[p].ModifierId != modifierId) continue;
			_slotModIconPulseRemaining[slot, p] = Mathf.Max(_slotModIconPulseRemaining[slot, p], 0.24f);
		}
	}

	private void PulseTowerLoadoutIndicators(SlotTheory.Entities.ITowerView tower, float duration = 0.34f)
	{
		if (_runState == null || tower == null)
			return;

		int slot = -1;
		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			if (ReferenceEquals(_runState.Slots[i].Tower, tower))
			{
				slot = i;
				break;
			}
		}
		if (slot < 0 || slot >= _slotModIconPulseRemaining.GetLength(0))
			return;

		int count = Mathf.Min(tower.Modifiers.Count, _slotModIconPulseRemaining.GetLength(1));
		for (int p = 0; p < count; p++)
			_slotModIconPulseRemaining[slot, p] = Mathf.Max(_slotModIconPulseRemaining[slot, p], duration);
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

		HashSet<ITowerView>? activeHintTowers = _botRunner == null ? new HashSet<ITowerView>() : null;

		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			var tower = _runState.Slots[i].TowerNode;
			if (tower == null || !GodotObject.IsInstanceValid(tower))
				continue;

			SpectacleVisualState visual = _spectacleSystem.GetVisualState(tower);
			tower.SpectacleMeterNormalized = visual.MeterNormalized;
			tower.SpectaclePulse = visual.Pulse;
			tower.SpectacleAccent = visual.PrimaryModId;

			if (activeHintTowers != null)
			{
				activeHintTowers.Add(tower);
				float previousMeter = _surgeHintLastMeterByTower.TryGetValue(tower, out float previous) ? previous : 0f;
				_surgeHintLastMeterByTower[tower] = visual.MeterNormalized;

				if (_tutorialManager == null
					&& tower.Modifiers.Count >= 1
					&& visual.MeterNormalized >= 0.12f
					&& visual.MeterNormalized > previousMeter + 0.04f
					&& _runState.SurgeHintTelemetry.GlobalsActivated <= 0
					&& (SettingsManager.Instance?.SurgeHintProfile.GlobalActivationsTotal ?? 0) <= 0)
				{
					TryShowSurgeMicroHint(
						SurgeHintId.CombatFills,
						"Combat fills this ring",
						worldPos: tower.GlobalPosition,
						holdSeconds: 2.4f,
						towerForHighlight: tower);
				}
			}
		}

		if (activeHintTowers != null && _surgeHintLastMeterByTower.Count > 0)
		{
			foreach (var trackedTower in _surgeHintLastMeterByTower.Keys.ToArray())
			{
				if (!activeHintTowers.Contains(trackedTower))
					_surgeHintLastMeterByTower.Remove(trackedTower);
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
		int visibleCount = tower?.MaxModifiers ?? Balance.MaxModifiersPerTower;
		bool atMax = filled >= tower?.MaxModifiers;
		// Single row, centered on the current visible count.
		for (int p = 0; p < pips.Length; p++)
		{
			float px = (p - (visibleCount - 1) / 2f) * 9f;
			float ix = (p - (visibleCount - 1) / 2f) * 12f;

			pips[p].Position = new Vector2(px - 3f, 27f);
			pips[p].Visible = tower != null && p < visibleCount;
			pips[p].Color = p < filled
				? (atMax ? new Color(1.00f, 0.60f, 0.05f) : new Color(0.30f, 0.95f, 0.40f))
				: new Color(0.22f, 0.22f, 0.22f, 0.45f);

			if (icons != null)
			{
				icons[p].Position = new Vector2(ix - 5f, 38f);
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

	/// <summary>
	/// Applies settings-driven visual refreshes to the currently running scene
	/// so pause-menu setting changes are reflected immediately without restart.
	/// </summary>
	public void RefreshInGameSettingVisuals()
	{
		if (_runState?.Slots == null)
			return;

		for (int i = 0; i < _runState.Slots.Length; i++)
		{
			RefreshModPips(i);

			var towerNode = _runState.Slots[i].TowerNode;
			if (GodotObject.IsInstanceValid(towerNode))
				towerNode.QueueRedraw();
		}

		foreach (var enemy in _runState.EnemiesAlive)
		{
			if (GodotObject.IsInstanceValid(enemy))
				enemy.QueueRedraw();
		}

		if (GodotObject.IsInstanceValid(_previewModifierIcon) && _draftPanel != null)
		{
			string pendingModifierId = _draftPanel.PendingModifierId;
			if (!string.IsNullOrWhiteSpace(pendingModifierId))
			{
				_previewModifierIcon.IconColor = ModifierVisuals.GetAccent(pendingModifierId);
				_previewModifierIcon.QueueRedraw();
			}
		}

		if (_draftSynergyHintModifierId.Length > 0)
			UpdateDraftSynergyHighlights(0f);
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

			// Range preview - fill + border, higher alphas since ghost Modulate will reduce them
			var ghostPts = new Vector2[64];
			for (int p = 0; p < 64; p++)
			{
				float a = p * Mathf.Tau / 64;
				ghostPts[p] = new Vector2(Mathf.Cos(a) * _previewTowerGhost.Range, Mathf.Sin(a) * _previewTowerGhost.Range);
			}
			var ghostRangeFill = new Polygon2D
			{
				Color = new Color(_previewTowerGhost.BodyColor.R, _previewTowerGhost.BodyColor.G, _previewTowerGhost.BodyColor.B, 0.18f),
				ZIndex = -1,
				ShowBehindParent = true,
				Polygon = ghostPts,
			};
			_previewTowerGhost.AddChild(ghostRangeFill);
			var ghostBorderPts = new Vector2[65];
			for (int p = 0; p < 64; p++) ghostBorderPts[p] = ghostPts[p];
			ghostBorderPts[64] = ghostPts[0];
			var ghostRangeBorder = new Line2D
			{
				Points = ghostBorderPts,
				Width = 1.4f,
				DefaultColor = new Color(_previewTowerGhost.BodyColor.R, _previewTowerGhost.BodyColor.G, _previewTowerGhost.BodyColor.B, 0.70f),
				ZIndex = -1,
				ShowBehindParent = true,
			};
			_previewTowerGhost.AddChild(ghostRangeBorder);
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
		if (filled >= tower.MaxModifiers || filled < 0) return;

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

		// Range preview overlay for Overreach (+45%) and Hair Trigger (-18%)
		string modId = _draftPanel.PendingModifierId;
		float rangeFactor = modId switch
		{
			"overreach"    => Balance.OverreachRangeFactor,
			"hair_trigger" => Balance.HairTriggerRangeFactor,
			_              => -1f,
		};
		var towerNode = _runState.Slots[slot].TowerNode;
		if (rangeFactor > 0f && towerNode != null && GodotObject.IsInstanceValid(towerNode))
		{
			float previewRange = tower.Range * rangeFactor;
			bool needsRebuild = !GodotObject.IsInstanceValid(_previewRangeOverlay)
				|| _previewRangeOverlay!.GetParent() != towerNode;
			if (needsRebuild)
			{
				if (GodotObject.IsInstanceValid(_previewRangeOverlay))
					_previewRangeOverlay!.QueueFree();
				var pts = new Vector2[65];
				for (int p = 0; p < 64; p++)
				{
					float a = p * Mathf.Tau / 64;
					pts[p] = new Vector2(Mathf.Cos(a) * previewRange, Mathf.Sin(a) * previewRange);
				}
				pts[64] = pts[0];
				_previewRangeOverlay = new Line2D
				{
					Points = pts,
					Width = 1.8f,
					DefaultColor = new Color(accent.R, accent.G, accent.B, 0.80f),
					ZIndex = 2,
				};
				towerNode.AddChild(_previewRangeOverlay);
			}
			else
			{
				// Update radius in case the slot changed
				for (int p = 0; p < 64; p++)
				{
					float a = p * Mathf.Tau / 64;
					_previewRangeOverlay!.SetPointPosition(p, new Vector2(Mathf.Cos(a) * previewRange, Mathf.Sin(a) * previewRange));
				}
				_previewRangeOverlay!.SetPointPosition(64, _previewRangeOverlay.GetPointPosition(0));
			}
			_previewRangeOverlay!.Modulate = new Color(1f, 1f, 1f, 0.45f + pulse * 0.40f);
			_previewRangeOverlay.Visible = true;
		}
		else if (GodotObject.IsInstanceValid(_previewRangeOverlay))
		{
			_previewRangeOverlay!.Visible = false;
		}
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

		if (GodotObject.IsInstanceValid(_previewRangeOverlay))
		{
			_previewRangeOverlay!.QueueFree();
			_previewRangeOverlay = null;
		}

		_previewGhostSlot = -1;
		_previewGhostPhase = 0f;
	}

	private void ShowWaveClearFlash()
	{
		BeginCriticalFeedbackWindow(0.42f);
		_waveClearFlash.Color   = new Color(0.10f, 1f, 0.45f, 0f);
		_waveClearFlash.Visible = true;
		var tween = CreateTween();
		tween.SetIgnoreTimeScale(true);
		tween.TweenProperty(_waveClearFlash, "color", new Color(0.10f, 1f, 0.45f, 0.18f), 0.08f);
		tween.TweenProperty(_waveClearFlash, "color", new Color(0.10f, 1f, 0.45f, 0f),    0.40f)
			 .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() => _waveClearFlash.Visible = false));
		_hudPanel.PulseWaveLabel();
	}

	// ------------------------------------------------------------------
	// Bot orchestrator: spawns one Godot process per map*difficulty pair.
	// Called when --bot is used without --map or --difficulty.
	// ------------------------------------------------------------------
	private static void RunBotOrchestrator(
		string godotExe,
		string projectPath,
		int totalRuns,
		string? strategySet,
		string? tuningFile,
		int runIndexOffset,
		bool fastMetrics,
		bool quiet,
		bool isDemo,
		string? metricsOutputPath,
		string? forcedTower,
		string? forcedMod)
	{
		const int MaxConcurrent = 24;

		// Build the map*difficulty combo list.
		string[] mapIds;
		try
		{
			mapIds = DataLoader.GetAllMapDefs()
				.Where(m => !m.IsRandom && (!Balance.IsDemo || !m.IsFullGame))
				.OrderBy(m => m.DisplayOrder)
				.Select(m => m.Id)
				.ToArray();
		}
		catch
		{
			mapIds = System.Array.Empty<string>();
		}
		if (mapIds.Length == 0)
			mapIds = Balance.IsDemo
				? new[] { "crossroads", "pinch_bleed", "orbit" }
				: new[] { "orbit", "crossroads", "pinch_bleed", "ridgeback", "double_back",
				          "crossfire", "threshold", "switchback", "ziggurat", "hourglass",
				          "perimeter_lock", "trident" };

		var difficulties = new[] { "easy", "normal", "hard" };
		int numCombos = mapIds.Length * difficulties.Length;
		int runsPerChild = System.Math.Max(1, (int)System.Math.Ceiling((double)totalRuns / numCombos));

		GD.Print($"[BOT-ORCH] Orchestrating {numCombos} processes ({mapIds.Length} maps x {difficulties.Length} diffs), {runsPerChild} runs each = {numCombos * runsPerChild} total");
		if (!string.IsNullOrWhiteSpace(metricsOutputPath))
			GD.Print("[BOT-ORCH] Note: --bot_metrics_out is not aggregated in orchestrator mode; use --map + --difficulty for per-combo metrics.");

		// Shared args forwarded to every child.
		var sharedArgs = new System.Collections.Generic.List<string>();
		if (!string.IsNullOrWhiteSpace(strategySet)) { sharedArgs.Add("--strategy_set"); sharedArgs.Add(strategySet!); }
		if (!string.IsNullOrWhiteSpace(tuningFile)) { sharedArgs.Add("--tuning_file"); sharedArgs.Add(tuningFile!); }
		if (!string.IsNullOrWhiteSpace(forcedTower)) { sharedArgs.Add("--force_tower"); sharedArgs.Add(forcedTower!); }
		if (!string.IsNullOrWhiteSpace(forcedMod)) { sharedArgs.Add("--force_mod"); sharedArgs.Add(forcedMod!); }
		if (fastMetrics) sharedArgs.Add("--bot_fast_metrics");
		if (quiet) sharedArgs.Add("--bot_quiet");
		if (isDemo) sharedArgs.Add("--demo");

		// Launch all processes; throttle to MaxConcurrent at a time.
		var allProcs = new System.Collections.Generic.List<(string map, string diff, System.Diagnostics.Process proc, System.Text.StringBuilder stdout)>();

		int comboIdx = 0;
		foreach (string mapId in mapIds)
		{
			foreach (string diff in difficulties)
			{
				int childOffset = runIndexOffset + comboIdx * runsPerChild;
				var args = new System.Collections.Generic.List<string>();
				args.Add("--headless");
				args.Add("--path");
				args.Add(projectPath);
				args.Add("--scene");
				args.Add("res://Scenes/Main.tscn");
				args.Add("--");
				args.Add("--bot");
				args.Add("--runs");
				args.Add($"{runsPerChild}");
				args.Add("--map");
				args.Add(mapId);
				args.Add("--difficulty");
				args.Add(diff);
				args.Add("--no_orchestrate");
				if (childOffset > 0) { args.Add("--run_index_offset"); args.Add($"{childOffset}"); }
				args.AddRange(sharedArgs);

				var psi = new System.Diagnostics.ProcessStartInfo
				{
					FileName = godotExe,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
				};
				foreach (string a in args) psi.ArgumentList.Add(a);

				var sb = new System.Text.StringBuilder();
				var proc = new System.Diagnostics.Process { StartInfo = psi };
				proc.OutputDataReceived += (_, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
				proc.Start();
				proc.BeginOutputReadLine();

				allProcs.Add((mapId, diff, proc, sb));
				comboIdx++;
				GD.Print($"[BOT-ORCH] Launched [{mapId}|{diff}] (PID {proc.Id})");

				// Throttle: wait until a slot is free before launching the next.
				while (allProcs.Count(p => !p.proc.HasExited) >= MaxConcurrent)
					System.Threading.Thread.Sleep(500);
			}
		}

		// Wait for all remaining processes to finish.
		while (allProcs.Any(p => !p.proc.HasExited))
		{
			int remaining = allProcs.Count(p => !p.proc.HasExited);
			GD.Print($"[BOT-ORCH] Waiting... {remaining}/{allProcs.Count} still running");
			System.Threading.Thread.Sleep(2000);
		}

		// Print each child's buffered output.
		foreach (var (map, diff, proc, sb) in allProcs)
		{
			GD.Print($"\n=== [{map}|{diff.ToUpper()}] (exit {proc.ExitCode}) ===");
			GD.Print(sb.ToString());
		}

		GD.Print("\n[BOT-ORCH] All processes complete.");
	}

}
