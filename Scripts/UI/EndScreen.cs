using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen score overlay shown at run end (win or loss).
/// Navigation is explicit via buttons (Open Leaderboards / Main Menu).
/// </summary>
public partial class EndScreen : CanvasLayer
{
	private Control _root = null!;
	private Label _titleLabel    = null!;
	private Label _subtitleLabel = null!;
	private Label _difficultyLabel = null!;
	private Label _statsLabel    = null!;
	private Label _leaderboardLabel = null!;
	private RichTextLabel _runNameLabel  = null!;
	private Label _mvpLabel      = null!;
	private Label _modLabel      = null!;
	private Label _surgeProfileLabel = null!;
	private Label _buildLabel    = null!;
	private Label _lossAnalysisLabel = null!;
	private Label _goalLabel = null!;
	private Button _viewLeaderboardButton = null!;
	private Button _wishlistButton = null!;
	private Button _mainMenuButton = null!;
	private Button _playAgainButton = null!;
	private Button _continueEndlessButton = null!;
	private Button _nextCampaignButton = null!;
	private MarginContainer _campaignFooter = null!;
	private MarginContainer _buttonBlock = null!;
	private bool _isCampaignRun;
	private bool _isPlaytestRun;
	private CampaignStageDefinition? _nextCampaignStage;
	public event System.Action? ContinueEndlessPressed;
	public event System.Action? WinExited;  // fires when leaving win screen via Play Again or Main Menu
	private string _leaderboardMapId = LeaderboardKey.RandomMapId;
	private DifficultyMode _leaderboardDifficulty = DifficultyMode.Easy;
	private RunScorePayload? _pendingPayload;
	private string _pendingLocalLine = "";
	private bool _namePromptActive;
	private bool _canDismiss;
	private bool _continuingEndless;
	private bool _isTutorialRun;
	private DifficultyMode _playAgainTargetDifficulty = DifficultyMode.Easy;
	private Button _howToPlayButton = null!;
	private HBoxContainer _secondaryRow = null!;
	private HBoxContainer _primaryRow = null!;
	private VBoxContainer _vbox = null!;
	private const float BackgroundOverlayAlpha = 0.88f;
	private const float SingleRowButtonWidthFactor = 0.40f; // ~60% narrower than full-width row
	private const float SingleRowButtonMinWidth = 260f;
	private const float SingleRowButtonMaxWidth = 560f;

	public override void _Ready()
	{
		Layer = 10;
		Visible = false;

		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.Theme = SlotTheory.Core.UITheme.Build();
		AddChild(_root);
		var root = _root;

		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0f, 0f, 0f, BackgroundOverlayAlpha);
		root.AddChild(bg);

		var mainLayout = new VBoxContainer();
		mainLayout.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(mainLayout);

		// HBox with flanking spacers to center the scroll horizontally at content width.
		var scrollRow = new HBoxContainer();
		scrollRow.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
		scrollRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		mainLayout.AddChild(scrollRow);

		var spacerLeft = new Control();
		spacerLeft.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollRow.AddChild(spacerLeft);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical  = Control.SizeFlags.ExpandFill;
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		TouchScrollHelper.EnableDragScroll(scroll);
		scrollRow.AddChild(scroll);

		var spacerRight = new Control();
		spacerRight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scrollRow.AddChild(spacerRight);

		// Style the scrollbar so players notice they can scroll.
		var vbar = scroll.GetVScrollBar();
		vbar.CustomMinimumSize = new Vector2(10, 0);
		var trackStyle = new StyleBoxFlat();
		trackStyle.BgColor = new Color(1f, 1f, 1f, 0.08f);
		trackStyle.SetCornerRadiusAll(5);
		vbar.AddThemeStyleboxOverride("scroll", trackStyle);
		var thumbStyle = new StyleBoxFlat();
		thumbStyle.BgColor = new Color(0.55f, 0.85f, 1.0f, 0.55f);
		thumbStyle.SetCornerRadiusAll(5);
		vbar.AddThemeStyleboxOverride("grabber", thumbStyle);
		var thumbHoverStyle = new StyleBoxFlat();
		thumbHoverStyle.BgColor = new Color(0.55f, 0.85f, 1.0f, 0.80f);
		thumbHoverStyle.SetCornerRadiusAll(5);
		vbar.AddThemeStyleboxOverride("grabber_highlight", thumbHoverStyle);
		vbar.AddThemeStyleboxOverride("grabber_pressed", thumbHoverStyle);

		var footer = new MarginContainer();
		footer.AddThemeConstantOverride("margin_left", 16);
		footer.AddThemeConstantOverride("margin_right", 16);
		footer.AddThemeConstantOverride("margin_top", 8);
		footer.AddThemeConstantOverride("margin_bottom", 12);
		mainLayout.AddChild(footer);

		var contentMargin = new MarginContainer();
		contentMargin.AddThemeConstantOverride("margin_right", 18);
		scroll.AddChild(contentMargin);

		_vbox = new VBoxContainer();
		_vbox.AddThemeConstantOverride("separation", 24);
		_vbox.CustomMinimumSize = new Vector2(520f, 0f);
		contentMargin.AddChild(_vbox);
		var vbox = _vbox;

		var titleGroup = new VBoxContainer();
		titleGroup.AddThemeConstantOverride("separation", 2);
		vbox.AddChild(titleGroup);

		_titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 72);
		SlotTheory.Core.UITheme.ApplyFont(_titleLabel, semiBold: true, size: 72);
		titleGroup.AddChild(_titleLabel);

		_difficultyLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, Visible = false };
		UITheme.ApplyFont(_difficultyLabel, semiBold: true, size: 18);
		titleGroup.AddChild(_difficultyLabel);

		_subtitleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_subtitleLabel.AddThemeFontSizeOverride("font_size", 28);
		titleGroup.AddChild(_subtitleLabel);

		_statsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_statsLabel.AddThemeFontSizeOverride("font_size", 20);
		_statsLabel.Modulate = new Color(0.65f, 0.85f, 1.0f);
		_statsLabel.Visible = false;
		vbox.AddChild(_statsLabel);

		_leaderboardLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_leaderboardLabel.AddThemeFontSizeOverride("font_size", 18);
		_leaderboardLabel.Modulate = new Color(0.72f, 0.93f, 0.78f);
		_leaderboardLabel.Visible = false;
		vbox.AddChild(_leaderboardLabel);

		_runNameLabel = new RichTextLabel
		{
			BbcodeEnabled = true,
			ScrollActive = false,
			FitContent = true,
			AutowrapMode = TextServer.AutowrapMode.Off,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			CustomMinimumSize = new Vector2(0f, 34f),
		};
		_runNameLabel.AddThemeFontOverride("normal_font", UITheme.SemiBold);
		_runNameLabel.AddThemeFontSizeOverride("normal_font_size", 24);
		_runNameLabel.Visible = false;
		vbox.AddChild(_runNameLabel);

		_mvpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		UITheme.ApplyFont(_mvpLabel, semiBold: true, size: 18);
		_mvpLabel.Modulate = new Color(0.72f, 0.92f, 1.00f);
		_mvpLabel.Visible = false;
		vbox.AddChild(_mvpLabel);

		_modLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		UITheme.ApplyFont(_modLabel, size: 15);
		_modLabel.Modulate = new Color(0.80f, 0.88f, 1.00f, 0.90f);
		_modLabel.Visible = false;
		vbox.AddChild(_modLabel);

		_surgeProfileLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		UITheme.ApplyFont(_surgeProfileLabel, semiBold: true, size: 15);
		_surgeProfileLabel.Modulate = new Color(1.00f, 0.90f, 0.44f, 0.92f);
		_surgeProfileLabel.Visible = false;
		vbox.AddChild(_surgeProfileLabel);

		_buildLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_buildLabel.AddThemeFontSizeOverride("font_size", 16);
		_buildLabel.AddThemeConstantOverride("line_spacing", -8);
		_buildLabel.Modulate = new Color(0.75f, 0.75f, 0.85f);
		_buildLabel.Visible = false;
		vbox.AddChild(_buildLabel);

		// Add a loss analysis label for defeat insights
		_lossAnalysisLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_lossAnalysisLabel.AddThemeFontSizeOverride("font_size", 16);
		_lossAnalysisLabel.Modulate = new Color(1.0f, 0.7f, 0.7f);
		_lossAnalysisLabel.Visible = false;
		vbox.AddChild(_lossAnalysisLabel);

		// Near-miss / next-goal hint
		_goalLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_goalLabel.AddThemeFontSizeOverride("font_size", 15);
		_goalLabel.Modulate = new Color(1.0f, 0.88f, 0.45f, 0.92f);
		_goalLabel.Visible = false;
		vbox.AddChild(_goalLabel);

		// Shared button stack - lives in the sticky footer, always on-screen.
		_buttonBlock = new MarginContainer();
		footer.AddChild(_buttonBlock);

		var buttonStack = new VBoxContainer();
		buttonStack.AddThemeConstantOverride("separation", 12);
		_buttonBlock.AddChild(buttonStack);

		// Secondary actions row: Leaderboards + Wishlist side by side
		_secondaryRow = new HBoxContainer();
		var secondaryRow = _secondaryRow;
		secondaryRow.AddThemeConstantOverride("separation", 8);
		secondaryRow.CustomMinimumSize = new Vector2(360f, 0f);
		buttonStack.AddChild(secondaryRow);

		_viewLeaderboardButton = new Button
		{
			Text = "Open Leaderboards",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false,
		};
		_viewLeaderboardButton.AddThemeFontSizeOverride("font_size", 16);
		UITheme.ApplyCyanStyle(_viewLeaderboardButton);
		UITheme.ApplyMenuButtonFinish(_viewLeaderboardButton, UITheme.Cyan, 0.09f, 0.11f);
		_viewLeaderboardButton.Pressed += OnViewLeaderboardPressed;
		secondaryRow.AddChild(_viewLeaderboardButton);

		_wishlistButton = new Button
		{
			Text = ProductCopy.WishlistCta,
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = Balance.IsDemo && Balance.FullGameSteamAppId != 0u,
		};
		_wishlistButton.AddThemeFontSizeOverride("font_size", 15);
		UITheme.ApplyMutedStyle(_wishlistButton);
		UITheme.ApplyMenuButtonFinish(_wishlistButton, UITheme.Magenta, 0.09f, 0.14f);
		_wishlistButton.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
		_wishlistButton.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			SteamAchievements.OpenFullGameStorePage();
		};
		_wishlistButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		secondaryRow.AddChild(_wishlistButton);

		// Play Again + Continue Endless side by side
		_primaryRow = new HBoxContainer();
		var primaryRow = _primaryRow;
		primaryRow.AddThemeConstantOverride("separation", 8);
		primaryRow.CustomMinimumSize = new Vector2(360f, 0f);
		buttonStack.AddChild(primaryRow);

		_playAgainButton = new Button
		{
			Text = "Play Again",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false,
		};
		_playAgainButton.AddThemeFontSizeOverride("font_size", 18);
		UITheme.ApplyPrimaryStyle(_playAgainButton);
		UITheme.ApplyMenuButtonFinish(_playAgainButton, UITheme.Lime, 0.11f, 0.14f);
		_playAgainButton.Pressed += OnPlayAgainPressed;
		_playAgainButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		primaryRow.AddChild(_playAgainButton);

		_continueEndlessButton = new Button
		{
			Text = "Continue | Endless",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false,
		};
		_continueEndlessButton.AddThemeFontSizeOverride("font_size", 18);
		UITheme.ApplyPrimaryStyle(_continueEndlessButton);
		UITheme.ApplyMenuButtonFinish(_continueEndlessButton, UITheme.Lime, 0.11f, 0.14f);
		_continueEndlessButton.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.30f));
		_continueEndlessButton.Pressed += OnContinueEndlessPressed;
		_continueEndlessButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		primaryRow.AddChild(_continueEndlessButton);

		_nextCampaignButton = new Button
		{
			Text = "Next Stage",
			CustomMinimumSize = new Vector2(360f, 48f),
			Visible = false,
		};
		_nextCampaignButton.AddThemeFontSizeOverride("font_size", 20);
		UITheme.ApplyPrimaryStyle(_nextCampaignButton);
		UITheme.ApplyMenuButtonFinish(_nextCampaignButton, UITheme.Lime, 0.11f, 0.14f);
		_nextCampaignButton.Pressed += OnNextCampaignPressed;
		_nextCampaignButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");

		_campaignFooter = new MarginContainer();
		_campaignFooter.AddThemeConstantOverride("margin_top", 0);
		buttonStack.AddChild(_campaignFooter);

		var campaignFooterVbox = new VBoxContainer();
		campaignFooterVbox.AddThemeConstantOverride("separation", 12);
		_campaignFooter.AddChild(campaignFooterVbox);
		campaignFooterVbox.AddChild(_nextCampaignButton);

		_mainMenuButton = new Button
		{
			Text = "Main Menu",
			CustomMinimumSize = new Vector2(360f, 42f),
			Visible = true,
		};
		_mainMenuButton.AddThemeFontSizeOverride("font_size", 18);
		UITheme.ApplyCyanStyle(_mainMenuButton);
		UITheme.ApplyMenuButtonFinish(_mainMenuButton, UITheme.Cyan, 0.09f, 0.11f);
		_mainMenuButton.Pressed += OnMainMenuPressed;
		_mainMenuButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		campaignFooterVbox.AddChild(_mainMenuButton);

		_howToPlayButton = new Button
		{
			Text = "How to Play",
			CustomMinimumSize = new Vector2(360f, 38f),
			Visible = false,
		};
		_howToPlayButton.AddThemeFontSizeOverride("font_size", 16);
		UITheme.ApplyMutedStyle(_howToPlayButton);
		UITheme.ApplyMenuButtonFinish(_howToPlayButton, UITheme.Magenta, 0.09f, 0.14f);
		_howToPlayButton.Pressed += OnHowToPlayPressed;
		_howToPlayButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		buttonStack.AddChild(_howToPlayButton);

		MobileOptimization.ApplyUIScale(scroll);
		AddChild(new PinchZoomHandler(scroll));
		GetViewport().SizeChanged += RefreshFooterButtonLayout;
		CallDeferred(MethodName.RefreshFooterButtonLayout);
	}

	/// <summary>
	/// Injects a campaign sector stamp (e.g. "SECTOR BREACHED") into the win screen.
	/// Called by GameController immediately after ShowWin on a campaign run win.
	/// </summary>
	public void SetCampaignStageStamp(string stampText, bool isFinalStage, string finalCompletionText)
	{
		if (!GodotObject.IsInstanceValid(_vbox) || string.IsNullOrEmpty(stampText)) return;

		var stamp = new Label
		{
			Text = stampText,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(stamp, semiBold: true, size: 26);
		stamp.Modulate = isFinalStage
			? new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 1.0f)
			: new Color(1.0f, 0.85f, 0.25f, 0.96f);
		_vbox.AddChild(stamp);
		// Place right after the subtitle label
		_vbox.MoveChild(stamp, _subtitleLabel.GetIndex() + 1);

		if (isFinalStage && !string.IsNullOrEmpty(finalCompletionText))
		{
			var finalLabel = new Label
			{
				Text = finalCompletionText,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
			};
			UITheme.ApplyFont(finalLabel, semiBold: false, size: 18);
			finalLabel.Modulate = new Color(0.55f, 0.72f, 0.88f, 0.88f);
			_vbox.AddChild(finalLabel);
			_vbox.MoveChild(finalLabel, stamp.GetIndex() + 1);
		}
	}

	/// <summary>
	/// Switches the end screen into campaign mode: hides leaderboard/play-again/endless buttons,
	/// shows a "Next Stage" button if nextStage is provided, and relabels Main Menu → Campaign Select.
	/// </summary>
	public void SetCampaignMode(CampaignStageDefinition? nextStage)
	{
		_isCampaignRun = true;
		_isPlaytestRun = false;
		_nextCampaignStage = nextStage;

		if (GodotObject.IsInstanceValid(_viewLeaderboardButton)) _viewLeaderboardButton.Visible = false;
		if (GodotObject.IsInstanceValid(_primaryRow))            _primaryRow.Visible = false;

		if (GodotObject.IsInstanceValid(_nextCampaignButton))
		{
			if (nextStage != null)
			{
				_nextCampaignButton.Text = $"Next Stage: {nextStage.StageName}";
				_nextCampaignButton.Visible = true;
			}
			else
			{
				_nextCampaignButton.Visible = false;
			}
		}

		if (GodotObject.IsInstanceValid(_mainMenuButton))
			_mainMenuButton.Text = "Campaign Select";

		// Campaign-only: add a little extra lift to the footer CTA pair.
		if (GodotObject.IsInstanceValid(_campaignFooter))
			_campaignFooter.AddThemeConstantOverride("margin_top", -10);
		RefreshFooterButtonLayout();
	}

	private void OnNextCampaignPressed()
	{
		if (_nextCampaignStage == null) return;
		SoundManager.Instance?.Play("ui_select");
		CampaignManager.SetActiveStage(_nextCampaignStage);
		MapSelectPanel.SetPendingMapSelection(_nextCampaignStage.MapId);
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	public void ShowWin(int kills, int damageDealt, float totalPlayTime, string buildSummary, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor, int livesRemaining = Balance.StartingLives, int totalWaves = Balance.TotalWaves, int maxLives = Balance.StartingLives)
	{
		_continuingEndless = false;
		_titleLabel.Text = _isPlaytestRun ? "VICTORY  [PLAYTEST]" : "VICTORY";
		bool isHardWin = _leaderboardDifficulty == DifficultyMode.Hard;
		_titleLabel.Modulate = isHardWin ? new Color(1.0f, 0.85f, 0.2f) : new Color(0.3f, 1.0f, 0.5f);
		_subtitleLabel.Text = $"All {totalWaves} waves survived | {livesRemaining} {(livesRemaining == 1 ? "life" : "lives")} remaining";
		ShowDifficultyLabel();
		_statsLabel.Text = $"Enemies killed: {kills} | Damage: {damageDealt:N0} | Lives: {livesRemaining}/{maxLives} | Time: {FormatTime(totalPlayTime)}";
		_statsLabel.Visible = true;
		SetRunNameGradient(runName, runStartColor, runEndColor);
		_runNameLabel.Visible = runName.Length > 0;
		_mvpLabel.Text = mvpLine;
		_mvpLabel.Visible = mvpLine.Length > 0;
		_modLabel.Text = modLine;
		_modLabel.Visible = modLine.Length > 0;
		_buildLabel.Text = buildSummary;
		_buildLabel.Visible = buildSummary.Length > 0;
		_lossAnalysisLabel.Visible = false;
		_surgeProfileLabel.Visible = false;
		_goalLabel.Visible = false;
		_leaderboardLabel.Visible = false;
		// Escalate Play Again label toward next difficulty tier
		if (GodotObject.IsInstanceValid(_playAgainButton))
		{
			(_playAgainButton.Text, _playAgainTargetDifficulty) = _leaderboardDifficulty switch
			{
				DifficultyMode.Easy   => ("Play Again | Try Normal", DifficultyMode.Normal),
				DifficultyMode.Normal => ("Play Again | Try Hard", DifficultyMode.Hard),
				_                     => ("Play Again", DifficultyMode.Hard),
			};
		}
		if (GodotObject.IsInstanceValid(_continueEndlessButton))
			_continueEndlessButton.Visible = true;
		RefreshFooterButtonLayout();
		PlayEntranceAnimation();
	}

	public void ShowLoss(int waveReached, int livesLost, int kills, int damageDealt, float totalPlayTime, string buildSummary, RunState runState, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor, int totalWaves = Balance.TotalWaves)
	{
		_continuingEndless = false;
		_playAgainTargetDifficulty = _leaderboardDifficulty;
		_titleLabel.Text = _isPlaytestRun ? "GAME OVER  [PLAYTEST]" : "GAME OVER";
		_titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		int wavesLeft = totalWaves - waveReached;
		string wavesFromVictory = wavesLeft > 0 ? $" | {wavesLeft} wave{(wavesLeft == 1 ? "" : "s")} from victory" : "";
		_subtitleLabel.Text = $"{waveReached} / {totalWaves}{wavesFromVictory} | Lives lost: {livesLost}";
		ShowDifficultyLabel();
		_statsLabel.Text = $"Enemies killed: {kills} | Total damage: {damageDealt:N0} | Time: {FormatTime(totalPlayTime)}";
		_statsLabel.Visible = kills > 0 || damageDealt > 0;
		SetRunNameGradient(runName, runStartColor, runEndColor);
		_runNameLabel.Visible = runName.Length > 0;
		_mvpLabel.Text = mvpLine;
		_mvpLabel.Visible = mvpLine.Length > 0;
		_modLabel.Text = modLine;
		_modLabel.Visible = modLine.Length > 0;
		_buildLabel.Text = buildSummary;
		_buildLabel.Visible = buildSummary.Length > 0;
		_leaderboardLabel.Visible = false;
		_surgeProfileLabel.Visible = false;
		_goalLabel.Visible = false;

		// Show loss analysis for actionable insights
		string lossAnalysis = GenerateLossAnalysis(runState);
		if (!string.IsNullOrEmpty(lossAnalysis))
		{
			_lossAnalysisLabel.Text = lossAnalysis;
			_lossAnalysisLabel.Visible = true;
		}
		else
		{
			_lossAnalysisLabel.Visible = false;
		}

		if (GodotObject.IsInstanceValid(_continueEndlessButton))
			_continueEndlessButton.Visible = false;
		RefreshFooterButtonLayout();
		PlayEntranceAnimation();
	}

	private void PlayEntranceAnimation()
	{
		_canDismiss = false;
		_root.Modulate = new Color(1f, 1f, 1f, 0f);
		Visible = true;
		GetTree().CreateTimer(0.40f).Timeout += () => _canDismiss = true;
		_titleLabel.PivotOffset = _titleLabel.Size / 2f;
		_titleLabel.Scale = new Vector2(0.78f, 0.78f);

		// Capture original alphas, then zero them so labels stay hidden during the root fade
		var stagger = new (CanvasItem item, float delay, float alpha)[]
		{
			(_subtitleLabel,     0.28f, _subtitleLabel.Modulate.A),
			(_statsLabel,        0.38f, _statsLabel.Modulate.A),
			(_runNameLabel,      0.46f, _runNameLabel.Modulate.A),
			(_mvpLabel,          0.54f, _mvpLabel.Modulate.A),
			(_modLabel,          0.60f, _modLabel.Modulate.A),
			(_surgeProfileLabel, 0.63f, _surgeProfileLabel.Modulate.A),
			(_buildLabel,        0.66f, _buildLabel.Modulate.A),
			(_lossAnalysisLabel, 0.66f, _lossAnalysisLabel.Modulate.A),
			(_goalLabel,         0.74f, _goalLabel.Modulate.A),
		};
		foreach (var entry in stagger)
		{
			var c = entry.item.Modulate;
			entry.item.Modulate = new Color(c.R, c.G, c.B, 0f);
		}

		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_root, "modulate:a", 1f, 0.30f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(_titleLabel, "scale", Vector2.One, 0.26f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		foreach (var entry in stagger)
		{
			if (!entry.item.Visible) continue;
			float target = entry.alpha > 0f ? entry.alpha : 1f;
			var lt = CreateTween();
			lt.TweenInterval(entry.delay);
			lt.TweenProperty(entry.item, "modulate:a", target, 0.18f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	public void SetTutorialMode(bool isTutorial)
	{
		_isTutorialRun = isTutorial;
		if (GodotObject.IsInstanceValid(_howToPlayButton))
			_howToPlayButton.Visible = isTutorial;
		if (isTutorial)
		{
			// Hide play-again row and endless button - tutorial win screen only shows How to Play + Main Menu.
			if (GodotObject.IsInstanceValid(_primaryRow))           _primaryRow.Visible = false;
			if (GodotObject.IsInstanceValid(_viewLeaderboardButton)) _viewLeaderboardButton.Visible = false;
			if (GodotObject.IsInstanceValid(_continueEndlessButton)) _continueEndlessButton.Visible = false;
			// Move How to Play above Main Menu.
			if (GodotObject.IsInstanceValid(_howToPlayButton) && GodotObject.IsInstanceValid(_mainMenuButton))
				_howToPlayButton.GetParent().MoveChild(_howToPlayButton, _mainMenuButton.GetIndex());
		}
		RefreshFooterButtonLayout();
	}

	public void SetLeaderboardStatus(string text, bool isError = false)
	{
		_leaderboardLabel.Text = text;
		_leaderboardLabel.Modulate = isError
			? new Color(1.00f, 0.72f, 0.72f)
			: new Color(0.72f, 0.93f, 0.78f);
		_leaderboardLabel.Visible = text.Length > 0;
		if (text.Length > 0 && GodotObject.IsInstanceValid(_leaderboardLabel))
		{
			SoundManager.Instance?.Play(isError ? "ui_cancel" : "ui_select");
			_leaderboardLabel.PivotOffset = _leaderboardLabel.Size / 2f;
			var tw = _leaderboardLabel.CreateTween();
			tw.TweenProperty(_leaderboardLabel, "scale", new Vector2(1.12f, 1.12f), 0.07f)
			  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			tw.TweenProperty(_leaderboardLabel, "scale", Vector2.One, 0.18f)
			  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	public void SetSurgeProfile(string surgeLabel, int count)
	{
		if (!GodotObject.IsInstanceValid(_surgeProfileLabel)) return;
		if (string.IsNullOrEmpty(surgeLabel) || count <= 0)
		{
			_surgeProfileLabel.Visible = false;
			return;
		}
		_surgeProfileLabel.Text = count == 1
			? $"Dominant Global Surge: {surgeLabel}"
			: $"Dominant Global Surge: {surgeLabel}  x{count}";
		_surgeProfileLabel.Visible = true;
	}
	public void SetGoalHint(string hint)
	{
		if (!GodotObject.IsInstanceValid(_goalLabel)) return;
		_goalLabel.Text = hint;
		_goalLabel.Visible = hint.Length > 0;
	}

	public void SetLeaderboardContext(string mapId, DifficultyMode difficulty)
	{
		_leaderboardMapId = string.IsNullOrEmpty(mapId) ? LeaderboardKey.RandomMapId : mapId;
		_leaderboardDifficulty = difficulty;
		_playAgainTargetDifficulty = difficulty;

		_isPlaytestRun = MapEditorState.IsPlaytesting;

		bool eligible = !_isPlaytestRun && LeaderboardKey.IsGlobalEligibleMap(_leaderboardMapId);
		if (GodotObject.IsInstanceValid(_viewLeaderboardButton))
		{
			_viewLeaderboardButton.Visible = eligible;
			_viewLeaderboardButton.Disabled = !eligible;
		}
		if (GodotObject.IsInstanceValid(_mainMenuButton))
		{
			_mainMenuButton.Text    = _isPlaytestRun ? "Back to Editor" : "Main Menu";
			_mainMenuButton.Visible = true;
			_mainMenuButton.Disabled = false;
		}
		if (GodotObject.IsInstanceValid(_playAgainButton))
		{
			_playAgainButton.Text    = _isPlaytestRun ? "Retest Map" : "Play Again";
			_playAgainButton.Visible = true;
			_playAgainButton.Disabled = false;
		}
		RefreshFooterButtonLayout();
	}

	private void OnViewLeaderboardPressed()
	{
		OnScreenExit();
		LeaderboardsMenu.SetPendingContext(_leaderboardMapId, _leaderboardDifficulty, preferGlobal: true);
		Transition.Instance?.FadeToScene("res://Scenes/Leaderboards.tscn");
	}

	/// <summary>
	/// Shows a one-time display name prompt before the first global submission.
	/// On confirm, saves the name and submits the score.
	/// </summary>
	public void ShowNamePrompt(RunScorePayload payload, string localLine)
	{
		_pendingPayload   = payload;
		_pendingLocalLine = localLine;
		_namePromptActive = true;

		var overlay = new Control();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Theme = SlotTheory.Core.UITheme.Build();
		AddChild(overlay);

		var overlayBg = new ColorRect();
		overlayBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlayBg.Color = new Color(0f, 0f, 0f, BackgroundOverlayAlpha);
		overlay.AddChild(overlayBg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 26);
		vbox.CustomMinimumSize = new Vector2(380, 0);
		center.AddChild(vbox);

		var title = new Label
		{
			Text = "Enter your display name",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 28);
		title.Modulate = new Color(0.72f, 0.93f, 0.78f);
		vbox.AddChild(title);

		string idSuffix = GetPlayerIdSuffix();
		var sub = new Label
		{
			Text = $"Shown as  YourName#{idSuffix}  on global leaderboards.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		sub.AddThemeFontSizeOverride("font_size", 15);
		sub.Modulate = new Color(0.65f, 0.65f, 0.65f);
		vbox.AddChild(sub);

		var nameEdit = new LineEdit
		{
			PlaceholderText = "Your name (max 10 chars)",
			MaxLength = 10,
			CustomMinimumSize = new Vector2(300, 40),
		};
		nameEdit.AddThemeFontSizeOverride("font_size", 20);
		vbox.AddChild(nameEdit);

		var confirmBtn = new Button
		{
			Text = "Submit Score",
			CustomMinimumSize = new Vector2(300, 44),
		};
		confirmBtn.AddThemeFontSizeOverride("font_size", 20);
		UITheme.ApplyPrimaryStyle(confirmBtn);
		UITheme.ApplyMenuButtonFinish(confirmBtn, UITheme.Lime, 0.11f, 0.14f);
		confirmBtn.Pressed += () =>
		{
			string name = nameEdit.Text.Trim();
			if (name.Length == 0) return;
			SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
			// Store as "Name#suffix" so display names are unique across players
			string fullName = $"{name}#{GetPlayerIdSuffix()}";
			SlotTheory.Core.SettingsManager.Instance?.SetPlayerName(fullName);
			_namePromptActive = false;
			overlay.QueueFree();
			_ = SubmitAfterNameAsync();
		};
		vbox.AddChild(confirmBtn);

		var skipBtn = new Button
		{
			Text = "Skip (no global submission)",
			CustomMinimumSize = new Vector2(300, 36),
			Flat = true,
		};
		skipBtn.AddThemeFontSizeOverride("font_size", 15);
		UITheme.ApplyMutedStyle(skipBtn);
		UITheme.ApplyMenuButtonFinish(skipBtn, UITheme.Magenta, 0.09f, 0.14f);
		skipBtn.Pressed += () =>
		{
			SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
			_namePromptActive = false;
			overlay.QueueFree();
			_pendingPayload = null;
			SetLeaderboardStatus(localLine);
		};
		vbox.AddChild(skipBtn);

		nameEdit.GrabFocus();
	}

	private async System.Threading.Tasks.Task SubmitAfterNameAsync()
	{
		if (_pendingPayload == null) return;
		var payload   = _pendingPayload;
		var localLine = _pendingLocalLine;
		_pendingPayload = null;

		SetLeaderboardStatus($"{localLine}  |  Global: submitting...");

		var manager = LeaderboardManager.Instance;
		if (manager == null)
		{
			SetLeaderboardStatus($"{localLine}  |  Global: unavailable", true);
			return;
		}

		var result = await manager.SubmitAsync(payload);

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

		bool isError = result.State == GlobalSubmitState.Failed;
		if (GodotObject.IsInstanceValid(this))
			SetLeaderboardStatus($"{localLine}  |  {globalText}", isError);
	}

	public override void _Notification(int what)
	{
		if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */ && Visible && !_namePromptActive && _canDismiss)
		{
			OnScreenExit();
			SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
			Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && Visible && !_namePromptActive && _canDismiss)
		{
			OnScreenExit();
			SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
			Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void ShowDifficultyLabel()
	{
		if (!GodotObject.IsInstanceValid(_difficultyLabel)) return;
		(_difficultyLabel.Text, _difficultyLabel.Modulate) = _leaderboardDifficulty switch
		{
			DifficultyMode.Easy   => ("EASY",   new Color(0.40f, 0.90f, 0.45f)),
			DifficultyMode.Normal => ("NORMAL", new Color(1.00f, 0.82f, 0.30f)),
			DifficultyMode.Hard   => ("HARD",   new Color(1.00f, 0.35f, 0.30f)),
			_                     => ("",       Colors.White),
		};
		_difficultyLabel.Visible = _difficultyLabel.Text.Length > 0;
	}

	// Fires WinExited on every exit path except "Continue - Endless".
	private void OnScreenExit()
	{
		if (!_continuingEndless) WinExited?.Invoke();
	}

	private void OnContinueEndlessPressed()
	{
		SoundManager.Instance?.Play("ui_select");
		_continuingEndless = true;
		if (GodotObject.IsInstanceValid(_continueEndlessButton))
			_continueEndlessButton.Visible = false;
		RefreshFooterButtonLayout();
		Visible = false;
		ContinueEndlessPressed?.Invoke();
	}

	private void RefreshFooterButtonLayout()
	{
		float viewportWidth = GetViewport().GetVisibleRect().Size.X;
		float availableWidth = Mathf.Max(320f, viewportWidth - 32f);
		float singleWidth = Mathf.Clamp(availableWidth * SingleRowButtonWidthFactor, SingleRowButtonMinWidth, SingleRowButtonMaxWidth);

		ApplyStandaloneButtonLayout(_howToPlayButton, singleWidth);
		ApplyStandaloneButtonLayout(_mainMenuButton, singleWidth);
		ApplyStandaloneButtonLayout(_nextCampaignButton, singleWidth);

		ApplyRowButtonLayout(_secondaryRow, singleWidth);
		ApplyRowButtonLayout(_primaryRow, singleWidth);
	}

	private static void ApplyStandaloneButtonLayout(Button? button, float width)
	{
		if (button == null || !GodotObject.IsInstanceValid(button))
			return;

		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		button.CustomMinimumSize = new Vector2(width, button.CustomMinimumSize.Y);
	}

	private static void ApplyRowButtonLayout(HBoxContainer? row, float singleWidth)
	{
		if (row == null || !GodotObject.IsInstanceValid(row))
			return;

		var visibleButtons = row.GetChildren().OfType<Button>().Where(b => b.Visible && GodotObject.IsInstanceValid(b)).ToList();
		int count = visibleButtons.Count;
		row.Visible = count > 0;
		if (count == 0)
			return;

		// Keep row width consistent with single-button rows; center all row variants.
		row.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		row.CustomMinimumSize = new Vector2(singleWidth, row.CustomMinimumSize.Y);
		bool single = count == 1;
		row.Alignment = BoxContainer.AlignmentMode.Center;

		foreach (var btn in visibleButtons)
		{
			btn.SizeFlagsHorizontal = single ? Control.SizeFlags.ShrinkCenter : Control.SizeFlags.ExpandFill;
			if (single)
				btn.CustomMinimumSize = new Vector2(singleWidth, btn.CustomMinimumSize.Y);
			else
				btn.CustomMinimumSize = new Vector2(0f, btn.CustomMinimumSize.Y);
		}
	}

	private void OnPlayAgainPressed()
	{
		OnScreenExit();
		SoundManager.Instance?.Play("ui_select");
		if (_isPlaytestRun)
		{
			// Re-run the same custom map playtest
			MapSelectPanel.SetPendingMapSelection(MapEditorState.PlaytestMapId ?? _leaderboardMapId);
			SettingsManager.Instance?.SetDifficulty(DifficultyMode.Easy);
			Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
			return;
		}
		if (_isTutorialRun)
		{
			// Tutorial "Play Again" goes to map select so the player picks a real run
			Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
			return;
		}
		MapSelectPanel.SetPendingMapSelection(_leaderboardMapId);
		if (_leaderboardMapId == LeaderboardKey.RandomMapId)
			MapSelectPanel.SetPendingProceduralSeed((ulong)(System.Environment.TickCount64 & 0x7FFFFFFF));
		SettingsManager.Instance?.SetDifficulty(_playAgainTargetDifficulty);
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private void OnMainMenuPressed()
	{
		OnScreenExit();
		if (_isPlaytestRun)
		{
			MapEditorState.ClearPlaytest();
			Transition.Instance?.FadeToScene("res://Scenes/MapEditor.tscn");
			return;
		}
		string dest = _isCampaignRun ? "res://Scenes/CampaignSelect.tscn" : "res://Scenes/MainMenu.tscn";
		Transition.Instance?.FadeToScene(dest);
	}

	private void OnHowToPlayPressed()
	{
		SoundManager.Instance?.Play("ui_select");
		SlotCodexPanel.PendingSceneStartTab = SlotCodexPanel.CodexStartTab.HowToPlay;
		Transition.Instance?.FadeToScene("res://Scenes/SlotCodex.tscn");
	}

	private static string FormatTime(float seconds)
	{
		int s = (int)seconds;
		return $"{s / 60}:{s % 60:D2}";
	}

	private void SetRunNameGradient(string runName, Color startColor, Color endColor)
	{
		_runNameLabel.Clear();
		if (string.IsNullOrEmpty(runName)) return;
		_runNameLabel.AppendText(BuildGradientBbCode($"Build Name: {runName}", startColor, endColor));
	}

	private static string BuildGradientBbCode(string text, Color start, Color end)
	{
		if (string.IsNullOrEmpty(text))
			return "";
		if (text.Length == 1)
			return $"[color=#{start.ToHtml(false)}]{text}[/color]";

		var sb = new System.Text.StringBuilder(text.Length * 24);
		for (int i = 0; i < text.Length; i++)
		{
			float t = i / (float)(text.Length - 1);
			var c = start.Lerp(end, t);
			sb.Append("[color=#").Append(c.ToHtml(false)).Append(']');
			sb.Append(text[i]);
			sb.Append("[/color]");
		}
		return sb.ToString();
	}

	private static string GetPlayerIdSuffix()
	{
		string id = SlotTheory.Core.SettingsManager.Instance?.PlayerId ?? "";
		return id.Length >= 5 ? id.Substring(0, 5) : (id.Length > 0 ? id : "?????");
	}

	/// <summary>Generates causal insights about why the player lost.</summary>
	private string GenerateLossAnalysis(RunState runState)
	{
		var insights = new System.Collections.Generic.List<string>();

		// Most leaked enemy type
		if (runState.TotalLeaksByType.Count > 0)
		{
			var mostLeaked = runState.TotalLeaksByType.OrderByDescending(kvp => kvp.Value).First();
			string enemyName = mostLeaked.Key switch
			{
				"armored_walker"  => "Armored",
				"swift_walker"    => "Swift",
				"reverse_walker"  => "Reverse",
				"splitter_walker" => "Splitter",
				"splitter_shard"  => "Shard",
				_ => "Basic"
			};
			insights.Add($"Most leaks: {enemyName} ({mostLeaked.Value})");
		}

		// Wave where most lives were lost
		var worstWave = runState.WorstWave;
		if (worstWave != null && worstWave.Leaks > 1)
		{
			insights.Add($"Hardest wave: {worstWave.WaveNumber} ({worstWave.Leaks} leaks)");
		}

		// Last leaked type before defeat
		if (!string.IsNullOrEmpty(runState.LastLeakedType))
		{
			string lastType = runState.LastLeakedType switch
			{
				"armored_walker"  => "Armored",
				"swift_walker"    => "Swift",
				"reverse_walker"  => "Reverse",
				"splitter_walker" => "Splitter",
				"splitter_shard"  => "Shard",
				_ => "Basic"
			};
			insights.Add($"Final leak: {lastType}");
		}

		return insights.Count > 0 ? string.Join("  |  ", insights) : "";
	}
}





