using Godot;
using System.Linq;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Entry-point scene shown before a run starts and returned to after each run ends.
/// All UI built procedurally — no .tscn children required.
/// </summary>
public partial class MainMenu : Node
{
	public override void _Ready()
	{
		if (OS.GetCmdlineUserArgs().Contains("--bot"))
		{
			CallDeferred(nameof(AutoStartBotRun));
			return;
		}

		if (MobileOptimization.IsMobile() && MobileRunSession.HasSnapshot())
		{
			CallDeferred(nameof(AutoResumeRun));
			return;
		}

		var canvas = new CanvasLayer();
		AddChild(canvas);

		// Background
		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color("#07071a");
		canvas.AddChild(bg);

		// Animated neon grid — rendered above solid bg, below all UI
		var grid = new NeonGridBg();
		grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		grid.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(grid);

		// Centre everything
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.Theme = UITheme.Build();
		canvas.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		vbox.CustomMinimumSize = new Vector2(300, 0);
		center.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "SLOT THEORY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.LabelSettings = MakeTitleSettings();
		vbox.AddChild(title);

		// Sub-title
		var sub = new Label
		{
			Text = "TOWER DEFENSE  ·  DRAFT  ·  SURVIVE 20 WAVES",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		sub.AddThemeFontSizeOverride("font_size", 15);
		sub.Modulate = UITheme.Lime;
		vbox.AddChild(sub);

		AddSpacer(vbox, 10);

		// Menu card
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
			bg: new Color(0.04f, 0.04f, 0.12f),
			border: new Color(0.18f, 0.22f, 0.18f),
			corners: 12, borderWidth: 1, padH: 24, padV: 12));
		card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		card.CustomMinimumSize   = new Vector2(320, 0);
		vbox.AddChild(card);

		var cardVbox = new VBoxContainer();
		cardVbox.AddThemeConstantOverride("separation", 7);
		card.AddChild(cardVbox);

		// Play — primary
		var playBtn = MakeMenuButton("PLAY", 260, 50, 24);
		UITheme.ApplyPrimaryStyle(playBtn);
		playBtn.Pressed += OnPlay;
		cardVbox.AddChild(playBtn);

		AddSpacer(cardVbox, 4);
		AddSeparator(cardVbox);
		AddSpacer(cardVbox, 4);

		AddNavButton(cardVbox, "Leaderboards", OnLeaderboards);
		AddNavButton(cardVbox, "Achievements",  OnAchievements);
		AddNavButton(cardVbox, "Slot Codex",    OnSlotCodex);
		AddNavButton(cardVbox, "How to Play",   OnHowToPlay);
		AddNavButton(cardVbox, "Settings",      OnSettings);

		if (SteamAchievements.IsSteamInitialized && Balance.FullGameSteamAppId != 0u)
		{
			var wishBtn = MakeMenuButton("\u2665  Wishlist on Steam", 260, 40, 17);
			UITheme.ApplyMutedStyle(wishBtn);
			wishBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
			wishBtn.Pressed += () =>
			{
				SoundManager.Instance?.Play("ui_select");
				SteamAchievements.OpenFullGameStorePage();
			};
			cardVbox.AddChild(wishBtn);
		}

		AddSpacer(cardVbox, 4);
		AddSeparator(cardVbox);
		AddSpacer(cardVbox, 4);

		// Quit — muted
		var quitBtn = MakeMenuButton("Quit", 260, 36, 18);
		UITheme.ApplyMutedStyle(quitBtn);
		quitBtn.Pressed += OnQuit;
		cardVbox.AddChild(quitBtn);

		// Demo-complete banner — shown once after all 3 campaign maps are cleared
		bool allUnlocked = AchievementManager.Instance?.IsUnlocked(Unlocks.RiftPrismAchievementId) == true;
		bool alreadyNotified = SettingsManager.Instance?.DemoCompleteNotified == true;
		if (allUnlocked && !alreadyNotified)
			vbox.AddChild(BuildDemoCompleteBanner());

		// Version label — inside vbox so it scales with pinch zoom
		var versionLabel = new Label
		{
			Text = $"v{GetGameVersion()}",
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		versionLabel.AddThemeFontSizeOverride("font_size", 13);
		versionLabel.Modulate = new Color(0.30f, 0.30f, 0.38f);
		vbox.AddChild(versionLabel);
		if (MobileOptimization.IsMobile())
			CallDeferred(nameof(ApplyFitScale), center);
		AddChild(new PinchZoomHandler(center));
	}

	public override void _Notification(int what)
	{
		if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
			GetTree().Quit();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			GetTree().Quit();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnPlay()
	{
		SlotTheory.Data.DataLoader.LoadAll();
		Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
	}
	private void OnLeaderboards() => Transition.Instance?.FadeToScene("res://Scenes/Leaderboards.tscn");
	private void OnAchievements() => Transition.Instance?.FadeToScene("res://Scenes/Achievements.tscn");
	private void OnSlotCodex()   => Transition.Instance?.FadeToScene("res://Scenes/SlotCodex.tscn");
	private void OnHowToPlay()    => Transition.Instance?.FadeToScene("res://Scenes/HowToPlay.tscn");
	private void OnSettings()     => Transition.Instance?.FadeToScene("res://Scenes/Settings.tscn");
	private void OnQuit()         => GetTree().Quit();

	private void ApplyFitScale(CenterContainer center)
	{
		var vp = GetViewport().GetVisibleRect().Size;
		center.Size = vp; // anchor-independent size for reliable pivot
		var content = center.GetChild<Control>(0);
		float contentH = content.Size.Y;
		float maxScale = MobileOptimization.GetUIScale();
		float fitScale = contentH > 0 ? Mathf.Min(maxScale, vp.Y * 0.92f / contentH) : maxScale;
		center.PivotOffset = vp * 0.5f;
		center.Scale = new Vector2(fitScale, fitScale);
	}

	private void AutoResumeRun()
	{
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private void AutoStartBotRun()
	{
		// Headless bot mode bypasses menu/map-select and enters gameplay directly.
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private void AddNavButton(VBoxContainer parent, string text, System.Action callback)
	{
		var btn = MakeMenuButton(text, 260, 38, 20);
		btn.Pressed += callback;
		parent.AddChild(btn);
	}

	private Button MakeMenuButton(string text, int minW, int minH, int fontSize)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(minW, minH),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		return btn;
	}

	private static void AddSpacer(Control parent, int px)
	{
		var s = new Control { CustomMinimumSize = new Vector2(0, px) };
		(parent as Container)?.AddChild(s);
		if (parent is not Container)
			parent.AddChild(s);
	}

	private static void AddSpacer(VBoxContainer parent, int px)
	{
		parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, px) });
	}

	private static void AddSeparator(VBoxContainer parent)
	{
		var line = new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color             = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.12f),
			MouseFilter       = Control.MouseFilterEnum.Ignore,
		};
		parent.AddChild(line);
	}

	private static LabelSettings MakeTitleSettings()
	{
		var ls = new LabelSettings();
		ls.Font          = UITheme.Bold;
		ls.FontSize      = 120;
		ls.FontColor     = new Color("#d4f020");     // bright core lime (slightly lighter)
		ls.OutlineColor  = new Color("#1a4400");    // very dark green → creates bright-center-dark-edge look
		ls.OutlineSize   = 4;
		ls.ShadowColor   = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.55f);
		ls.ShadowSize    = 14;
		ls.ShadowOffset  = Vector2.Zero;
		return ls;
	}

	private Control BuildDemoCompleteBanner()
	{
		var panel = new PanelContainer();
		panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		panel.CustomMinimumSize = new Vector2(320, 0);
		panel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
			bg: new Color(0.06f, 0.04f, 0.14f),
			border: new Color(0.55f, 0.30f, 0.90f, 0.80f),
			corners: 10, borderWidth: 2, padH: 16, padV: 10));

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		panel.AddChild(inner);

		var heading = new Label
		{
			Text = "Demo complete!",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(heading, semiBold: true, size: 17);
		heading.Modulate = new Color(0.80f, 0.60f, 1.00f);
		inner.AddChild(heading);

		var body = new Label
		{
			Text = "You've unlocked everything in the demo.\nThe full game features more maps, towers, and challenges.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		body.AddThemeFontSizeOverride("font_size", 13);
		body.Modulate = new Color(0.72f, 0.72f, 0.84f);
		inner.AddChild(body);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 8);
		inner.AddChild(btnRow);

		if (SteamAchievements.IsSteamInitialized && Balance.FullGameSteamAppId != 0u)
		{
			var wishBtn = new Button
			{
				Text = "\u2665  Wishlist",
				CustomMinimumSize = new Vector2(110, 32),
			};
			wishBtn.AddThemeFontSizeOverride("font_size", 14);
			UITheme.ApplyMutedStyle(wishBtn);
			wishBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
			wishBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
			wishBtn.Pressed += () =>
			{
				SoundManager.Instance?.Play("ui_select");
				SteamAchievements.OpenFullGameStorePage();
			};
			btnRow.AddChild(wishBtn);
		}

		var dismissBtn = new Button
		{
			Text = "Got it",
			CustomMinimumSize = new Vector2(80, 32),
		};
		dismissBtn.AddThemeFontSizeOverride("font_size", 14);
		UITheme.ApplyMutedStyle(dismissBtn);
		dismissBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		dismissBtn.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			SettingsManager.Instance?.SetDemoCompleteNotified();
			panel.QueueFree();
		};
		btnRow.AddChild(dismissBtn);

		return panel;
	}

	private static string GetGameVersion()
	{
		string gameVersion = ProjectSettings.GetSetting("application/config/version", "dev").AsString();
		return string.IsNullOrWhiteSpace(gameVersion) ? "dev" : gameVersion;
	}
}
