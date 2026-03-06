using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen score overlay shown at run end (win or loss).
/// Any left-click returns the player to the main menu.
/// </summary>
public partial class EndScreen : CanvasLayer
{
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
	private Button _mainMenuButton = null!;
	private Label _hintLabel = null!;
	private string _leaderboardMapId = LeaderboardKey.RandomMapId;
	private DifficultyMode _leaderboardDifficulty = DifficultyMode.Normal;

	public override void _Ready()
	{
		Layer = 10;
		Visible = false;

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.Theme = SlotTheory.Core.UITheme.Build();
		AddChild(root);

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
		vbox.AddThemeConstantOverride("separation", 20);
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

		_viewLeaderboardButton = new Button
		{
			Text = "Open Leaderboards",
			CustomMinimumSize = new Vector2(360f, 42f),
			Visible = false,
		};
		_viewLeaderboardButton.AddThemeFontSizeOverride("font_size", 18);
		_viewLeaderboardButton.Pressed += OnViewLeaderboardPressed;
		vbox.AddChild(_viewLeaderboardButton);

		_mainMenuButton = new Button
		{
			Text = "Main Menu",
			CustomMinimumSize = new Vector2(360f, 42f),
			Visible = MobileOptimization.IsMobile(),
		};
		_mainMenuButton.AddThemeFontSizeOverride("font_size", 18);
		_mainMenuButton.Pressed += OnMainMenuPressed;
		vbox.AddChild(_mainMenuButton);

		var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
		vbox.AddChild(spacer);

		_hintLabel = new Label
		{
			Text = MobileOptimization.IsMobile()
				? "Use buttons below to continue"
				: "Click or press Enter to return to menu",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_hintLabel.AddThemeFontSizeOverride("font_size", 18);
		_hintLabel.Modulate = new Color(0.65f, 0.65f, 0.65f);
		vbox.AddChild(_hintLabel);
		MobileOptimization.ApplyUIScale(center);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (MobileOptimization.IsMobile())
			return;

		if (@event is InputEventMouseButton click
			&& click.Pressed
			&& click.ButtonIndex == MouseButton.Left
			&& GodotObject.IsInstanceValid(_viewLeaderboardButton)
			&& _viewLeaderboardButton.Visible
			&& _viewLeaderboardButton.GetGlobalRect().HasPoint(click.Position))
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, KeyLabel: Key.Enter or Key.KpEnter or Key.Space }
			&& GodotObject.IsInstanceValid(_viewLeaderboardButton)
			&& _viewLeaderboardButton.Visible
			&& _viewLeaderboardButton.HasFocus())
		{
			return;
		}

		bool triggered = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
		              || @event is InputEventKey { Pressed: true, KeyLabel: Key.Enter or Key.KpEnter or Key.Space };
		if (!triggered) return;
		GetViewport().SetInputAsHandled();
		Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}

	public void ShowWin(int kills, int damageDealt, string buildSummary, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor)
	{
		_titleLabel.Text = "VICTORY";
		_titleLabel.Modulate = new Color(0.3f, 1.0f, 0.5f);
		_subtitleLabel.Text = $"All {Balance.TotalWaves} waves survived!";
		_statsLabel.Text = $"Enemies killed: {kills}  -  Total damage: {damageDealt:N0}";
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
		Visible = true;
	}

	public void ShowLoss(int waveReached, int livesLost, int kills, int damageDealt, string buildSummary, RunState runState, string runName, string mvpLine, string modLine, Color runStartColor, Color runEndColor)
	{
		_titleLabel.Text = "GAME OVER";
		_titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_subtitleLabel.Text = $"Reached wave {waveReached} / {Balance.TotalWaves}  -  Lives lost: {livesLost}";
		_statsLabel.Text = $"Enemies killed: {kills}  -  Total damage: {damageDealt:N0}";
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

		Visible = true;
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
			_mainMenuButton.Visible = MobileOptimization.IsMobile();
			_mainMenuButton.Disabled = false;
		}
	}

	private void OnViewLeaderboardPressed()
	{
		LeaderboardsMenu.SetPendingContext(_leaderboardMapId, _leaderboardDifficulty, preferGlobal: true);
		Transition.Instance?.FadeToScene("res://Scenes/Leaderboards.tscn");
	}

	private void OnMainMenuPressed()
	{
		Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
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
