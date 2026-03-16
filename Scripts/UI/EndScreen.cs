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
	private Label _statsLabel    = null!;
	private Label _leaderboardLabel = null!;
	private RichTextLabel _runNameLabel  = null!;
	private Label _mvpLabel      = null!;
	private Label _modLabel      = null!;
	private Label _buildLabel    = null!;
	private Label _lossAnalysisLabel = null!;
	private Button _viewLeaderboardButton = null!;
	private Button _wishlistButton = null!;
	private Button _mainMenuButton = null!;
	private string _leaderboardMapId = LeaderboardKey.RandomMapId;
	private DifficultyMode _leaderboardDifficulty = DifficultyMode.Easy;
	private RunScorePayload? _pendingPayload;
	private string _pendingLocalLine = "";
	private bool _namePromptActive;

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
		bg.Color = new Color(0f, 0f, 0f, 0.88f);
		root.AddChild(bg);

		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		TouchScrollHelper.EnableDragScroll(scroll);
		root.AddChild(scroll);

		var center = new CenterContainer();
		center.CustomMinimumSize = GetViewport().GetVisibleRect().Size;
		scroll.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		center.AddChild(vbox);

		_titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 72);
		SlotTheory.Core.UITheme.ApplyFont(_titleLabel, semiBold: true, size: 72);
		vbox.AddChild(_titleLabel);

		_subtitleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_subtitleLabel.AddThemeFontSizeOverride("font_size", 28);
		vbox.AddChild(_subtitleLabel);

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
		UITheme.ApplyFont(_mvpLabel, semiBold: true, size: 17);
		_mvpLabel.Modulate = new Color(0.72f, 0.92f, 1.00f);
		_mvpLabel.Visible = false;
		vbox.AddChild(_mvpLabel);

		_modLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		UITheme.ApplyFont(_modLabel, size: 15);
		_modLabel.Modulate = new Color(0.80f, 0.88f, 1.00f, 0.90f);
		_modLabel.Visible = false;
		vbox.AddChild(_modLabel);

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

		// Secondary actions row: Leaderboards + Wishlist side by side
		var secondaryRow = new HBoxContainer();
		secondaryRow.AddThemeConstantOverride("separation", 8);
		secondaryRow.CustomMinimumSize = new Vector2(360f, 0f);
		vbox.AddChild(secondaryRow);

		_viewLeaderboardButton = new Button
		{
			Text = "Open Leaderboards",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = false,
		};
		_viewLeaderboardButton.AddThemeFontSizeOverride("font_size", 16);
		UITheme.ApplyCyanStyle(_viewLeaderboardButton);
		_viewLeaderboardButton.Pressed += OnViewLeaderboardPressed;
		secondaryRow.AddChild(_viewLeaderboardButton);

		_wishlistButton = new Button
		{
			Text = "\u2665  Wishlist Full Game",
			CustomMinimumSize = new Vector2(0f, 42f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			Visible = SteamAchievements.IsSteamInitialized && Balance.FullGameSteamAppId != 0u,
		};
		_wishlistButton.AddThemeFontSizeOverride("font_size", 15);
		UITheme.ApplyMutedStyle(_wishlistButton);
		_wishlistButton.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
		_wishlistButton.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			SteamAchievements.OpenFullGameStorePage();
		};
		_wishlistButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		secondaryRow.AddChild(_wishlistButton);

		_mainMenuButton = new Button
		{
			Text = "Main Menu",
			CustomMinimumSize = new Vector2(360f, 46f),
			Visible = true,
		};
		_mainMenuButton.AddThemeFontSizeOverride("font_size", 20);
		UITheme.ApplyPrimaryStyle(_mainMenuButton);
		_mainMenuButton.Pressed += OnMainMenuPressed;
		_mainMenuButton.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		vbox.AddChild(_mainMenuButton);
		MobileOptimization.ApplyUIScale(center);
	AddChild(new PinchZoomHandler(center));
	}

	public void ShowWin(int kills, int damageDealt, float totalPlayTime, string buildSummary, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor)
	{
		_titleLabel.Text = "VICTORY";
		_titleLabel.Modulate = new Color(0.3f, 1.0f, 0.5f);
		_subtitleLabel.Text = $"All {Balance.TotalWaves} waves survived!";
		_statsLabel.Text = $"Enemies killed: {kills}  -  Total damage: {damageDealt:N0}  -  Time: {FormatTime(totalPlayTime)}";
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
		_leaderboardLabel.Visible = false;
		PlayEntranceAnimation();
	}

	public void ShowLoss(int waveReached, int livesLost, int kills, int damageDealt, float totalPlayTime, string buildSummary, RunState runState, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor)
	{
		_titleLabel.Text = "GAME OVER";
		_titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_subtitleLabel.Text = $"Reached wave {waveReached} / {Balance.TotalWaves}  -  Lives lost: {livesLost}";
		_statsLabel.Text = $"Enemies killed: {kills}  -  Total damage: {damageDealt:N0}  -  Time: {FormatTime(totalPlayTime)}";
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

		PlayEntranceAnimation();
	}

	private void PlayEntranceAnimation()
	{
		_root.Modulate = new Color(1f, 1f, 1f, 0f);
		Visible = true;
		_titleLabel.PivotOffset = _titleLabel.Size / 2f;
		_titleLabel.Scale = new Vector2(0.78f, 0.78f);

		var tw = CreateTween();
		tw.SetParallel(true);
		tw.TweenProperty(_root, "modulate:a", 1f, 0.30f)
		  .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tw.TweenProperty(_titleLabel, "scale", Vector2.One, 0.26f)
		  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	public void SetLeaderboardStatus(string text, bool isError = false)
	{
		_leaderboardLabel.Text = text;
		_leaderboardLabel.Modulate = isError
			? new Color(1.00f, 0.72f, 0.72f)
			: new Color(0.72f, 0.93f, 0.78f);
		_leaderboardLabel.Visible = text.Length > 0;
	}

	public void SetLeaderboardContext(string mapId, DifficultyMode difficulty)
	{
		_leaderboardMapId = string.IsNullOrEmpty(mapId) ? LeaderboardKey.RandomMapId : mapId;
		_leaderboardDifficulty = difficulty;
		bool eligible = LeaderboardKey.IsGlobalEligibleMap(_leaderboardMapId);
		if (GodotObject.IsInstanceValid(_viewLeaderboardButton))
		{
			_viewLeaderboardButton.Visible = eligible;
			_viewLeaderboardButton.Disabled = !eligible;
		}
		if (GodotObject.IsInstanceValid(_mainMenuButton))
		{
			_mainMenuButton.Visible = true;
			_mainMenuButton.Disabled = false;
		}
	}

	private void OnViewLeaderboardPressed()
	{
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
		overlayBg.Color = new Color(0f, 0f, 0f, 0.88f);
		overlay.AddChild(overlayBg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
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
		skipBtn.Modulate = new Color(0.55f, 0.55f, 0.55f);
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

		bool isError = result.State == GlobalSubmitState.Failed;
		if (GodotObject.IsInstanceValid(this))
			SetLeaderboardStatus($"{localLine}  |  {globalText}", isError);
	}

	public override void _Notification(int what)
	{
		if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */ && Visible && !_namePromptActive)
		{
			SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
			Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") && Visible && !_namePromptActive)
		{
			Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnMainMenuPressed()
	{
		Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
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
				"armored_walker" => "Armored",
				"swift_walker" => "Swift",
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
				"armored_walker" => "Armored",
				"swift_walker" => "Swift",
				_ => "Basic"
			};
			insights.Add($"Final leak: {lastType}");
		}

		return insights.Count > 0 ? string.Join("  |  ", insights) : "";
	}
}
