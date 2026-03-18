using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Entry-point scene shown before a run starts and returned to after each run ends.
/// All UI built procedurally - no .tscn children required.
/// </summary>
public partial class MainMenu : Node
{
	private float _menuAnimTime;
	private readonly List<Control> _animatedSurfaces = new();

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

		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color("#07071a");
		canvas.AddChild(bg);

		var grid = new NeonGridBg();
		grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		grid.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(grid);

		// Keep exact composition, but add subtle light falloff for premium depth.
		var atmosphere = new MenuAtmosphereOverlay();
		atmosphere.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		atmosphere.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(atmosphere);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.AnchorBottom = 0.78f;
		center.Theme = UITheme.Build();
		canvas.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 0);
		vbox.CustomMinimumSize = new Vector2(300, 0);
		center.AddChild(vbox);

		vbox.AddChild(new TitleArmDecor());

		var title = new Label
		{
			Text = "SLOT THEORY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		title.LabelSettings = MakeTitleSettings();
		title.AddThemeConstantOverride("line_spacing", -36);
		vbox.AddChild(title);

		var sub = new Label
		{
			Text = "TOWER DEFENSE  -  DRAFT  -  SURVIVE 20 WAVES",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		sub.AddThemeFontSizeOverride("font_size", 13);
		sub.Modulate = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.72f);
		vbox.AddChild(sub);

		vbox.AddChild(new ReactorFeedBar());
		AddSpacer(vbox, 2);

		var menuRow = new HBoxContainer();
		menuRow.AddThemeConstantOverride("separation", 12);
		menuRow.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(menuRow);

		var card = new PanelContainer();
		var cardStyle = UITheme.MakePanel(
			bg:          new Color(0.022f, 0.028f, 0.082f),
			border:      new Color(0.23f, 0.43f, 0.30f),
			corners:     4,
			borderWidth: 2,
			padH:        24,
			padV:        12);
		cardStyle.ShadowColor  = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.14f);
		cardStyle.ShadowSize   = 7;
		cardStyle.ShadowOffset = new Vector2(0f, 2f);
		card.AddThemeStyleboxOverride("panel", cardStyle);
		card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		card.CustomMinimumSize   = new Vector2(320, 0);
		menuRow.AddChild(card);

		card.Draw += () =>
		{
			float cw = card.Size.X;
			float ch = card.Size.Y;
			if (cw < 24f || ch < 24f)
				return;

			float breath = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.42f);

			// Controlled frame glow with tight falloff and subtle breathing.
			for (int i = 0; i < 4; i++)
			{
				float spread = 1f + i * 1.8f;
				float alpha = 0.048f - i * 0.010f + breath * 0.006f;
				card.DrawRect(new Rect2(-spread, -spread, cw + spread * 2f, ch + spread * 2f),
					new Color(0.30f, 0.58f, 0.34f, alpha), false, 1f);
			}

			// Frame shell.
			card.DrawRect(new Rect2(2f, 2f, cw - 4f, ch - 4f), new Color(0.06f, 0.10f, 0.14f, 0.22f));
			card.DrawRect(new Rect2(2f, 2f, cw - 4f, ch - 4f), new Color(0.28f, 0.52f, 0.26f, 0.26f), false, 1f);

			// Content well: darker, richer, clearly separated from outer frame.
			const float contentInset = 12f;
			card.DrawRect(new Rect2(contentInset, contentInset, cw - contentInset * 2f, ch - contentInset * 2f),
				new Color(0.015f, 0.020f, 0.058f, 0.96f));
			card.DrawRect(new Rect2(contentInset + 1f, contentInset + 1f, cw - (contentInset + 1f) * 2f, (ch - contentInset * 2f) * 0.28f),
				new Color(0.08f, 0.16f, 0.20f, 0.11f));
			card.DrawRect(new Rect2(contentInset, contentInset, cw - contentInset * 2f, ch - contentInset * 2f),
				new Color(0.20f, 0.48f, 0.40f, 0.17f), false, 1f);

			// Shared framing language: crisp top lip + restrained bottom rail.
			card.DrawLine(new Vector2(6f, 2f), new Vector2(cw - 6f, 2f), new Color(0.94f, 0.99f, 0.84f, 0.42f), 1f);
			card.DrawLine(new Vector2(8f, 3f), new Vector2(cw - 8f, 3f), new Color(0.66f, 0.84f, 0.34f, 0.18f), 1f);
			card.DrawRect(new Rect2(10f, ch - 6f, cw - 20f, 2f), new Color(0.80f, 0.90f, 0.56f, 0.26f + breath * 0.04f));
			card.DrawRect(new Rect2(10f, ch - 10f, cw - 20f, 4f), new Color(0.38f, 0.58f, 0.24f, 0.08f));

			// Restrained sweep over the core, always fully inside bounds.
			float sweepW = 18f;
			float coreLeft = contentInset + 2f;
			float coreRight = cw - contentInset - 2f;
			float coreSpan = Mathf.Max(1f, coreRight - coreLeft - sweepW);
			float sweepLeft = coreLeft + ((_menuAnimTime * 0.08f) % 1f) * coreSpan;
			card.DrawRect(new Rect2(sweepLeft, contentInset + 6f, sweepW, ch - contentInset * 2f - 12f),
				new Color(0.70f, 0.84f, 0.92f, 0.014f + breath * 0.004f));
		};
		RegisterAnimatedSurface(card);

		var cardVbox = new VBoxContainer();
		cardVbox.AddThemeConstantOverride("separation", 8);
		card.AddChild(cardVbox);

		bool tutorialDone = SettingsManager.Instance?.TutorialCompleted ?? false;
		var tutorialBtn = MakeMenuButton(tutorialDone ? "Tutorial" : "> Tutorial", 260, 44, tutorialDone ? 18 : 20);
		if (tutorialDone)
			UITheme.ApplyMutedStyle(tutorialBtn);
		else
			UITheme.ApplyCyanStyle(tutorialBtn);
		AddButtonSurface(tutorialBtn, UITheme.Cyan, 0.10f, 0.14f);
		tutorialBtn.Pressed += OnTutorial;
		cardVbox.AddChild(tutorialBtn);

		AddSpacer(cardVbox, 4);

		var playBtn = MakeMenuButton("PLAY", 260, 52, 22);
		UITheme.ApplyPrimaryStyle(playBtn);
		playBtn.AddThemeStyleboxOverride("normal", UITheme.MakeBtn(
			new Color(0.060f, 0.125f, 0.038f),
			new Color(0.36f, 0.52f, 0.13f),
			border: 2, corners: 10, glowAlpha: 0.18f, glowSize: 6, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("hover", UITheme.MakeBtn(
			new Color(0.105f, 0.235f, 0.070f),
			UITheme.Lime,
			border: 2, corners: 10, glowAlpha: 0.30f, glowSize: 9, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("focus", UITheme.MakeBtn(
			new Color(0.093f, 0.205f, 0.058f),
			UITheme.Lime,
			border: 2, corners: 10, glowAlpha: 0.24f, glowSize: 8, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("pressed", UITheme.MakeBtn(
			new Color(0.036f, 0.084f, 0.022f),
			new Color(0.34f, 0.48f, 0.11f),
			border: 2, corners: 10, glowAlpha: 0.12f, glowSize: 4, glowColor: UITheme.LimeDim));
		playBtn.AddThemeFontOverride("font", UITheme.Bold);
		playBtn.AddThemeColorOverride("font_color", new Color(0.93f, 0.98f, 0.86f));
		playBtn.AddThemeColorOverride("font_hover_color", new Color(0.94f, 1.0f, 0.74f));
		playBtn.Draw += () =>
		{
			float pw = playBtn.Size.X;
			float ph = playBtn.Size.Y;
			float breath = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.85f);
			float sweepT = (_menuAnimTime * 0.22f) % 1f;
			for (int i = 0; i < 3; i++)
			{
				float spread = 0.8f + i * 1.4f;
				float alpha = 0.045f - i * 0.010f + breath * 0.006f;
				playBtn.DrawRect(new Rect2(-spread, -spread, pw + spread * 2f, ph + spread * 2f),
					new Color(0.44f, 0.72f, 0.22f, alpha), false, 1f);
			}
			playBtn.DrawRect(new Rect2(6f, 6f, pw - 12f, ph * 0.22f), new Color(0.90f, 1.0f, 0.86f, 0.04f));
			float sweepW = 10f;
			float btnLeft = 7f;
			float btnRight = pw - 7f;
			float playSpan = Mathf.Max(1f, btnRight - btnLeft - sweepW);
			float playSweepLeft = btnLeft + sweepT * playSpan;
			playBtn.DrawRect(new Rect2(playSweepLeft, 6f, sweepW, ph - 12f), new Color(0.84f, 0.95f, 0.82f, 0.034f + breath * 0.010f));
			playBtn.DrawLine(new Vector2(13f, 2f), new Vector2(pw - 13f, 2f),
				new Color(0.94f, 1.0f, 0.84f, 0.46f), 1f);
			playBtn.DrawLine(new Vector2(15f, 3f), new Vector2(pw - 15f, 3f),
				new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.18f), 1f);
			playBtn.DrawLine(new Vector2(14f, playBtn.Size.Y - 2f), new Vector2(pw - 14f, playBtn.Size.Y - 2f),
				new Color(0f, 0f, 0f, 0.42f), 1f);
		};
		AddButtonSurface(playBtn, UITheme.Lime, 0.10f, 0.14f);
		playBtn.Pressed += OnPlay;
		cardVbox.AddChild(playBtn);

		AddSpacer(cardVbox, 4);
		AddSeparator(cardVbox);
		AddSpacer(cardVbox, 4);

		AddNavButton(cardVbox, "Leaderboards", OnLeaderboards);
		AddNavButton(cardVbox, "Achievements", OnAchievements);
		AddNavButton(cardVbox, "Slot Codex", OnSlotCodex);
		AddNavButton(cardVbox, "How to Play", OnHowToPlay);
		AddNavButton(cardVbox, "Settings", OnSettings);

		if (SteamAchievements.IsSteamInitialized && Balance.FullGameSteamAppId != 0u)
		{
			var wishBtn = MakeMenuButton("Wishlist on Steam", 260, 40, 17);
			UITheme.ApplyMutedStyle(wishBtn);
			wishBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
			AddButtonSurface(wishBtn, UITheme.Magenta, 0.10f, 0.16f);
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

		var quitBtn = MakeMenuButton("Quit", 260, 36, 18);
		UITheme.ApplyMutedStyle(quitBtn);
		AddButtonSurface(quitBtn, UITheme.Magenta, 0.10f, 0.16f);
		quitBtn.Pressed += OnQuit;
		cardVbox.AddChild(quitBtn);

		bool allUnlocked = AchievementManager.Instance?.IsUnlocked(Unlocks.RiftPrismAchievementId) == true;
		bool alreadyNotified = SettingsManager.Instance?.DemoCompleteNotified == true;
		if (allUnlocked && !alreadyNotified)
		{
			var balancer = new Control { CustomMinimumSize = new Vector2(272, 0) };
			menuRow.AddChild(balancer);
			menuRow.MoveChild(balancer, 0);

			var purpleNode = new Color(0.55f, 0.30f, 0.90f);
			balancer.Draw += () =>
			{
				float midY = balancer.Size.Y * 0.5f;
				float pulse = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.95f);
				float packetX = 10f + (((_menuAnimTime * 0.14f) % 1f) * (Mathf.Max(20f, balancer.Size.X - 20f)));
				balancer.DrawLine(new Vector2(6f, midY),
					new Vector2(balancer.Size.X - 6f, midY),
					new Color(purpleNode, 0.22f + pulse * 0.10f), 1f);
				balancer.DrawLine(new Vector2(6f, midY + 1f),
					new Vector2(balancer.Size.X - 6f, midY + 1f),
					new Color(0.20f, 0.90f, 0.95f, 0.05f + pulse * 0.04f), 1f);
				balancer.DrawRect(new Rect2(3f, midY - 2.5f, 5f, 5f),
					new Color(purpleNode, 0.38f));
				balancer.DrawRect(new Rect2(balancer.Size.X - 8f, midY - 2.5f, 5f, 5f),
					new Color(purpleNode, 0.38f));
				balancer.DrawRect(new Rect2(packetX - 2f, midY - 2f, 4f, 4f), new Color(0.80f, 0.60f, 1.0f, 0.30f));
			};
			RegisterAnimatedSurface(balancer);

			var banner = BuildDemoCompleteBanner(balancer);
			banner.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			menuRow.AddChild(banner);
			RegisterAnimatedSurface(banner);
		}

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

	public override void _Process(double delta)
	{
		_menuAnimTime += (float)delta;
		for (int i = _animatedSurfaces.Count - 1; i >= 0; i--)
		{
			var c = _animatedSurfaces[i];
			if (!GodotObject.IsInstanceValid(c) || !c.IsInsideTree())
			{
				_animatedSurfaces.RemoveAt(i);
				continue;
			}
			c.QueueRedraw();
		}
	}

	public override void _Notification(int what)
	{
		if (what == 1007)
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

	private void OnTutorial()
	{
		SoundManager.Instance?.Play("ui_select");
		SlotTheory.Data.DataLoader.LoadAll();
		if (SettingsManager.Instance != null)
			SettingsManager.Instance.PendingTutorialRun = true;
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private void OnLeaderboards() => Transition.Instance?.FadeToScene("res://Scenes/Leaderboards.tscn");
	private void OnAchievements() => Transition.Instance?.FadeToScene("res://Scenes/Achievements.tscn");
	private void OnSlotCodex() => Transition.Instance?.FadeToScene("res://Scenes/SlotCodex.tscn");
	private void OnHowToPlay() => Transition.Instance?.FadeToScene("res://Scenes/HowToPlay.tscn");
	private void OnSettings() => Transition.Instance?.FadeToScene("res://Scenes/Settings.tscn");
	private void OnQuit() => GetTree().Quit();

	private void ApplyFitScale(CenterContainer center)
	{
		var vp = GetViewport().GetVisibleRect().Size;
		center.Size = vp;
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
		GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
	}

	private void RegisterAnimatedSurface(Control control)
	{
		if (!_animatedSurfaces.Contains(control))
			_animatedSurfaces.Add(control);
	}

	private void AddNavButton(VBoxContainer parent, string text, Action callback)
	{
		var btn = MakeMenuButton(text, 260, 38, 20);
		btn.AddThemeStyleboxOverride("normal", UITheme.MakeBtn(
			new Color(0.020f, 0.028f, 0.085f),
			new Color(0.12f, 0.18f, 0.20f),
			border: 1, corners: 8, glowAlpha: 0.06f, glowSize: 2, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("hover", UITheme.MakeBtn(
			new Color(0.030f, 0.050f, 0.10f),
			new Color(0.27f, 0.72f, 0.78f),
			border: 2, corners: 8, glowAlpha: 0.16f, glowSize: 6, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("focus", UITheme.MakeBtn(
			new Color(0.028f, 0.044f, 0.095f),
			new Color(0.27f, 0.72f, 0.78f),
			border: 2, corners: 8, glowAlpha: 0.14f, glowSize: 5, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("pressed", UITheme.MakeBtn(
			new Color(0.018f, 0.024f, 0.078f),
			new Color(0.20f, 0.50f, 0.54f),
			border: 2, corners: 8, glowAlpha: 0.08f, glowSize: 3, glowColor: UITheme.Cyan));
		btn.Draw += () =>
		{
			float bw = btn.Size.X;
			btn.DrawLine(new Vector2(11f, 2f), new Vector2(bw - 11f, 2f), new Color(UITheme.Cyan, 0.18f), 1f);
			btn.DrawLine(new Vector2(11f, btn.Size.Y - 2f), new Vector2(bw - 11f, btn.Size.Y - 2f), new Color(0f, 0f, 0f, 0.34f), 1f);
		};
		AddButtonSurface(btn, UITheme.Cyan, 0.08f, 0.12f);
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

	private void AddButtonSurface(Button btn, Color accent, float topAlpha, float bottomAlpha)
	{
		btn.Draw += () =>
		{
			float bw = btn.Size.X;
			float bh = btn.Size.Y;
			if (bw < 8f || bh < 8f)
				return;

			float inset = 4f;
			float innerW = bw - inset * 2f;
			float innerH = bh - inset * 2f;
			float topBandH = Mathf.Max(2f, bh * 0.14f);
			float bottomBandH = Mathf.Max(3f, bh * 0.16f);

			btn.DrawRect(new Rect2(inset, inset, innerW, topBandH), new Color(accent.R, accent.G, accent.B, topAlpha));
			btn.DrawRect(new Rect2(inset, bh - inset - bottomBandH, innerW, bottomBandH), new Color(0f, 0f, 0f, bottomAlpha));
			btn.DrawLine(new Vector2(inset + 1f, inset + 1f), new Vector2(bw - inset - 1f, inset + 1f),
				new Color(1f, 1f, 1f, topAlpha * 0.45f), 1f);
			btn.DrawLine(new Vector2(inset + 1f, bh - inset - 1f), new Vector2(bw - inset - 1f, bh - inset - 1f),
				new Color(0f, 0f, 0f, bottomAlpha * 1.2f), 1f);
			btn.DrawRect(new Rect2(inset, inset, innerW, innerH), new Color(accent.R, accent.G, accent.B, topAlpha * 0.22f), false, 1f);
		};
		RegisterAnimatedSurface(btn);
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
			Color = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.12f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		parent.AddChild(line);
	}

	private static LabelSettings MakeTitleSettings()
	{
		var ls = new LabelSettings();
		ls.Font = GD.Load<FontFile>("res://Assets/Fonts/Anagram.ttf");
		ls.FontSize = 88;
		ls.FontColor = new Color("#d4f020");
		ls.OutlineColor = new Color("#1a4400");
		ls.OutlineSize = 4;
		ls.ShadowColor = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.65f);
		ls.ShadowSize = 11;
		ls.ShadowOffset = Vector2.Zero;
		return ls;
	}

	private Control BuildDemoCompleteBanner(Control? balancer = null)
	{
		var panel = new PanelContainer();
		panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		panel.CustomMinimumSize = new Vector2(260, 0);

		var bannerStyle = UITheme.MakePanel(
			bg:          new Color(0.030f, 0.022f, 0.102f),
			border:      new Color(0.52f, 0.38f, 0.78f, 0.86f),
			corners:     6,
			borderWidth: 2,
			padH:        16,
			padV:        10);
		bannerStyle.ShadowColor = new Color(0.55f, 0.28f, 0.90f, 0.22f);
		bannerStyle.ShadowSize = 8;
		bannerStyle.ShadowOffset = new Vector2(0f, 2f);
		panel.AddThemeStyleboxOverride("panel", bannerStyle);

		panel.Draw += () =>
		{
			float w = panel.Size.X;
			float h = panel.Size.Y;
			float pulse = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.72f + 1.1f);
			panel.DrawRect(new Rect2(3f, 3f, w - 6f, h - 6f), new Color(0.64f, 0.44f, 0.95f, 0.10f + pulse * 0.05f), false, 1f);
			panel.DrawRect(new Rect2(4f, 4f, w - 8f, h - 8f), new Color(0.06f, 0.10f, 0.18f, 0.22f));
			panel.DrawRect(new Rect2(12f, 12f, w - 24f, h - 24f), new Color(0.02f, 0.03f, 0.09f, 0.72f));
			panel.DrawRect(new Rect2(12f, 12f, w - 24f, h - 24f), new Color(0.58f, 0.43f, 0.84f, 0.12f), false, 1f);
			panel.DrawLine(new Vector2(8f, 3f), new Vector2(w - 8f, 3f), new Color(0.88f, 0.72f, 1.00f, 0.38f), 1f);
			panel.DrawRect(new Rect2(10f, h - 6f, w - 20f, 2f), new Color(0.70f, 0.42f, 0.95f, 0.24f + pulse * 0.06f));
		};

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 6);
		panel.AddChild(inner);

		var heading = new Label
		{
			Text = "Demo complete!",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		UITheme.ApplyFont(heading, semiBold: true, size: 17);
		heading.Modulate = new Color(0.82f, 0.64f, 1.00f);
		inner.AddChild(heading);

		var body = new Label
		{
			Text = "You've unlocked everything in the demo.\nThe full game features more maps, towers, and challenges.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		body.AddThemeFontSizeOverride("font_size", 13);
		body.Modulate = new Color(0.75f, 0.74f, 0.86f);
		inner.AddChild(body);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 8);
		inner.AddChild(btnRow);

		if (SteamAchievements.IsSteamInitialized && Balance.FullGameSteamAppId != 0u)
		{
			var wishBtn = new Button
			{
				Text = "Wishlist",
				CustomMinimumSize = new Vector2(110, 32),
			};
			wishBtn.AddThemeFontSizeOverride("font_size", 14);
			UITheme.ApplyMutedStyle(wishBtn);
			wishBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.65f, 1.0f));
			AddButtonSurface(wishBtn, UITheme.Magenta, 0.09f, 0.14f);
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
		AddButtonSurface(dismissBtn, UITheme.Magenta, 0.09f, 0.14f);
		dismissBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
		dismissBtn.Pressed += () =>
		{
			SoundManager.Instance?.Play("ui_select");
			SettingsManager.Instance?.SetDemoCompleteNotified();
			balancer?.QueueFree();
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

	private sealed partial class MenuAtmosphereOverlay : Control
	{
		private float _time;

		public override void _Process(double delta)
		{
			_time += (float)delta;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var size = GetViewportRect().Size;
			float w = size.X;
			float h = size.Y;
			var core = new Vector2(w * 0.5f, h * 0.48f);
			var side = new Vector2(w * 0.66f, h * 0.53f);
			float breath = 0.5f + 0.5f * MathF.Sin(_time * 0.30f);

			DrawCircle(core, w * 0.33f, new Color(0.07f, 0.15f, 0.10f, 0.07f + 0.020f * breath));
			DrawCircle(core, w * 0.23f, new Color(0.11f, 0.24f, 0.14f, 0.05f + 0.015f * breath));
			DrawCircle(side, w * 0.16f, new Color(0.17f, 0.14f, 0.24f, 0.018f + 0.012f * breath));
			DrawCircle(new Vector2(core.X, h * 0.64f), w * 0.21f, new Color(0f, 0f, 0f, 0.18f));
			DrawRect(new Rect2(0f, 0f, w * 0.13f, h), new Color(0f, 0f, 0f, 0.12f));
			DrawRect(new Rect2(w * 0.87f, 0f, w * 0.13f, h), new Color(0f, 0f, 0f, 0.12f));
		}
	}
}
