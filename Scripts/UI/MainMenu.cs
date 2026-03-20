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
		SoundManager.Instance?.SetMenuAmbientEnabled(true);

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
		bg.Color = new Color("#030a14");
		canvas.AddChild(bg);

		var grid = new NeonGridBg();
		grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		grid.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(grid);

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
		sub.Modulate = new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.82f);
		vbox.AddChild(sub);

		vbox.AddChild(new ReactorFeedBar());
		AddSpacer(vbox, 2);

		var menuRow = new HBoxContainer();
		menuRow.AddThemeConstantOverride("separation", 12);
		menuRow.Alignment = BoxContainer.AlignmentMode.Center;
		vbox.AddChild(menuRow);

		var card = new PanelContainer();
		var cardStyle = UITheme.MakePanel(
			bg:          new Color(0.016f, 0.021f, 0.064f),
			border:      new Color(0.20f, 0.37f, 0.43f),
			corners:     4,
			borderWidth: 2,
			padH:        24,
			padV:        12);
		cardStyle.ShadowColor  = new Color(0.12f, 0.36f, 0.44f, 0.14f);
		cardStyle.ShadowSize   = 8;
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
			float frameInset = 5f;
			float shellInset = 10f;
			float contentInset = 14f;

			// Outer controlled bloom: crisp cyan underlay + lime edge read.
			for (int i = 0; i < 5; i++)
			{
				float spread = 0.9f + i * 1.5f;
				float cyanA = 0.058f - i * 0.010f + breath * 0.006f;
				card.DrawRect(new Rect2(-spread, -spread, cw + spread * 2f, ch + spread * 2f),
					new Color(0.22f, 0.60f, 0.72f, cyanA), false, 1f);
			}
			card.DrawRect(new Rect2(-1f, -1f, cw + 2f, ch + 2f),
				new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.22f + breath * 0.04f), false, 1f);

			// Chassis shell + inner ring for clear frame/content separation.
			card.DrawRect(new Rect2(frameInset, frameInset, cw - frameInset * 2f, ch - frameInset * 2f),
				new Color(0.032f, 0.056f, 0.092f, 0.84f));
			card.DrawRect(new Rect2(frameInset, frameInset, cw - frameInset * 2f, ch - frameInset * 2f),
				new Color(0.24f, 0.64f, 0.78f, 0.28f), false, 1f);
			card.DrawRect(new Rect2(frameInset + 1f, frameInset + 1f, cw - (frameInset + 1f) * 2f, ch - (frameInset + 1f) * 2f),
				new Color(0.94f, 0.99f, 1.00f, 0.11f), false, 1f);
			card.DrawRect(new Rect2(shellInset, shellInset, cw - shellInset * 2f, ch - shellInset * 2f),
				new Color(0.010f, 0.020f, 0.044f, 0.96f));
			card.DrawRect(new Rect2(shellInset, shellInset, cw - shellInset * 2f, ch - shellInset * 2f),
				new Color(0.17f, 0.42f, 0.54f, 0.21f), false, 1f);

			// Richer, darker content bay.
			float contentW = cw - contentInset * 2f;
			float contentH = ch - contentInset * 2f;
			card.DrawRect(new Rect2(contentInset - 2f, contentInset - 2f, contentW + 4f, contentH + 4f),
				new Color(0.13f, 0.30f, 0.40f, 0.22f), false, 1f);
			card.DrawRect(new Rect2(contentInset, contentInset, contentW, contentH),
				new Color(0.006f, 0.012f, 0.034f, 0.995f));
			for (int i = 0; i < 6; i++)
			{
				float t = i / 5f;
				float y = contentInset + 1f + t * contentH * 0.56f;
				float hBand = contentH * 0.11f;
				float a = Mathf.Lerp(0.11f, 0.028f, t);
				card.DrawRect(new Rect2(contentInset + 1f, y, contentW - 2f, hBand), new Color(0.14f, 0.36f, 0.50f, a));
			}
			card.DrawRect(new Rect2(contentInset + 1f, contentInset + contentH * 0.60f, contentW - 2f, contentH * 0.40f - 1f),
				new Color(0f, 0f, 0f, 0.20f));
			card.DrawRect(new Rect2(contentInset, contentInset, contentW, contentH),
				new Color(0.22f, 0.52f, 0.64f, 0.16f), false, 1f);
			card.DrawLine(new Vector2(contentInset + 2f, contentInset + 2f), new Vector2(cw - contentInset - 2f, contentInset + 2f),
				new Color(0.94f, 0.99f, 1.00f, 0.22f), 1f);
			card.DrawLine(new Vector2(contentInset + 2f, contentInset + 2f), new Vector2(contentInset + 2f, ch - contentInset - 2f),
				new Color(0.64f, 0.86f, 0.96f, 0.08f), 1f);

			// Shared edge logic with demo panel: top lip + centered bottom energy cap.
			card.DrawLine(new Vector2(8f, 3f), new Vector2(cw - 8f, 3f), new Color(0.90f, 0.98f, 0.96f, 0.36f), 1f);
			card.DrawLine(new Vector2(9f, 4f), new Vector2(cw - 9f, 4f),
				new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.20f), 1f);
			float capW = Mathf.Clamp(cw * 0.17f, 44f, 72f);
			float capX = (cw - capW) * 0.5f;
			card.DrawRect(new Rect2(capX, 1.2f, capW, 3f), new Color(0.92f, 0.99f, 0.82f, 0.26f + breath * 0.05f));
			card.DrawRect(new Rect2(capX + 3f, ch - 5.8f, capW - 6f, 3f),
				new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.38f + breath * 0.10f));
			card.DrawRect(new Rect2(capX + 6f, ch - 8.8f, capW - 12f, 3f), new Color(0.30f, 0.56f, 0.24f, 0.18f));

			// Tiny, restrained top rail glint (horizontal, never crossing bounds).
			float glintW = Mathf.Clamp(cw * 0.18f, 44f, 78f);
			float glintSpan = Mathf.Max(1f, cw - 16f - glintW);
			float glintX = 8f + ((_menuAnimTime * 0.06f) % 1f) * glintSpan;
			card.DrawRect(new Rect2(glintX, 6f, glintW, 2f), new Color(0.78f, 0.96f, 1.0f, 0.11f));

			// Glassy side emitters like a premium chassis lock.
			float emitH = 44f;
			float emitY = ch * 0.53f - emitH * 0.5f;
			for (int i = 0; i < 4; i++)
			{
				float spread = i;
				float a = 0.18f - i * 0.036f + breath * 0.035f;
				card.DrawRect(new Rect2(1f - spread, emitY - spread, 5f + spread * 2f, emitH + spread * 2f),
					new Color(0.42f, 0.90f, 1.0f, a), false, 1f);
				card.DrawRect(new Rect2(cw - 6f - spread, emitY - spread, 5f + spread * 2f, emitH + spread * 2f),
					new Color(0.64f, 0.52f, 1.0f, a), false, 1f);
			}
			card.DrawRect(new Rect2(1f, emitY, 5f, emitH), new Color(0.52f, 0.94f, 1.0f, 0.35f + breath * 0.09f));
			card.DrawRect(new Rect2(2f, emitY + 5f, 3f, emitH - 10f), new Color(0.86f, 1.0f, 1.0f, 0.58f + breath * 0.10f));
			card.DrawRect(new Rect2(cw - 6f, emitY, 5f, emitH), new Color(0.60f, 0.56f, 1.0f, 0.34f + breath * 0.09f));
			card.DrawRect(new Rect2(cw - 5f, emitY + 5f, 3f, emitH - 10f), new Color(0.90f, 0.84f, 1.0f, 0.56f + breath * 0.10f));
		};
		RegisterAnimatedSurface(card);

		var cardVbox = new VBoxContainer();
		cardVbox.AddThemeConstantOverride("separation", 8);
		card.AddChild(cardVbox);
		AddSpacer(cardVbox, 6); // keep Tutorial from riding the top frame edge

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
			new Color(0.052f, 0.112f, 0.034f),
			new Color(0.56f, 0.76f, 0.20f),
			border: 2, corners: 10, glowAlpha: 0.24f, glowSize: 7, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("hover", UITheme.MakeBtn(
			new Color(0.088f, 0.205f, 0.064f),
			UITheme.Lime,
			border: 2, corners: 10, glowAlpha: 0.34f, glowSize: 9, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("focus", UITheme.MakeBtn(
			new Color(0.082f, 0.186f, 0.054f),
			UITheme.Lime,
			border: 2, corners: 10, glowAlpha: 0.28f, glowSize: 8, glowColor: UITheme.Lime));
		playBtn.AddThemeStyleboxOverride("pressed", UITheme.MakeBtn(
			new Color(0.030f, 0.074f, 0.020f),
			new Color(0.38f, 0.54f, 0.13f),
			border: 2, corners: 10, glowAlpha: 0.14f, glowSize: 4, glowColor: UITheme.LimeDim));
		playBtn.AddThemeFontOverride("font", UITheme.Bold);
		playBtn.AddThemeColorOverride("font_color", new Color(0.93f, 0.98f, 0.86f));
		playBtn.AddThemeColorOverride("font_hover_color", new Color(0.94f, 1.0f, 0.74f));
		playBtn.Draw += () =>
		{
			float pw = playBtn.Size.X;
			float ph = playBtn.Size.Y;
			float breath = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.85f);

			for (int i = 0; i < 3; i++)
			{
				float spread = 0.9f + i * 1.2f;
				float alpha = 0.064f - i * 0.013f + breath * 0.008f;
				playBtn.DrawRect(new Rect2(-spread, -spread, pw + spread * 2f, ph + spread * 2f),
					new Color(0.44f, 0.72f, 0.22f, alpha), false, 1f);
			}

			// Subtle inner material gradient for a richer, less flat PLAY surface.
			float innerLeft = 6f;
			float innerTop = 6f;
			float innerW = pw - 12f;
			float innerH = ph - 12f;
			playBtn.DrawRect(new Rect2(innerLeft, innerTop, innerW, innerH * 0.48f), new Color(0.80f, 1.00f, 0.70f, 0.10f));
			playBtn.DrawRect(new Rect2(innerLeft, innerTop + innerH * 0.30f, innerW, innerH * 0.24f), new Color(0.54f, 0.78f, 0.36f, 0.05f));
			playBtn.DrawRect(new Rect2(innerLeft, innerTop + innerH * 0.54f, innerW, innerH * 0.40f),
				new Color(0f, 0f, 0f, 0.10f));

			// Restrained reflective sweep near the top edge only.
			float sweepW = Mathf.Clamp(pw * 0.17f, 26f, 44f);
			float sweepX = 10f + (((_menuAnimTime * 0.14f) % 1f) * Mathf.Max(1f, pw - 20f - sweepW));
			playBtn.DrawRect(new Rect2(sweepX, 7f, sweepW, 2f), new Color(0.92f, 1.0f, 0.88f, 0.12f));
			playBtn.DrawRect(new Rect2(5f, 5f, pw - 10f, ph - 10f), new Color(0.84f, 0.99f, 0.74f, 0.14f), false, 1f);
			playBtn.DrawLine(new Vector2(8f, 6f), new Vector2(pw - 8f, 6f), new Color(1.0f, 1.0f, 0.90f, 0.34f), 1f);
			playBtn.DrawLine(new Vector2(8f, 7f), new Vector2(pw - 8f, 7f), new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 0.28f), 1f);

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

		AddNavButton(cardVbox, "Leaderboards", OnLeaderboards);
		AddNavButton(cardVbox, "Achievements", OnAchievements);
		AddNavButton(cardVbox, "Slot Codex", OnSlotCodex);
		AddNavButton(cardVbox, "How to Play", OnHowToPlay);
		AddNavButton(cardVbox, "Settings", OnSettings);

		if (Balance.IsDemo && Balance.FullGameSteamAppId != 0u)
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

		var quitBtn = MakeMenuButton("Quit", 260, 36, 18);
		UITheme.ApplyMutedStyle(quitBtn);
		AddButtonSurface(quitBtn, UITheme.Magenta, 0.10f, 0.16f);
		quitBtn.Pressed += OnQuit;
		cardVbox.AddChild(quitBtn);
		AddSpacer(cardVbox, 6); // keep Quit inside the lower frame edge

		bool allUnlocked = AchievementManager.Instance?.IsUnlocked(Unlocks.RiftPrismAchievementId) == true;
		bool alreadyNotified = SettingsManager.Instance?.DemoCompleteNotified == true;
		if (Balance.IsDemo && allUnlocked && !alreadyNotified)
		{
			var balancer = new Control { CustomMinimumSize = new Vector2(272, 0) };
			menuRow.AddChild(balancer);
			menuRow.MoveChild(balancer, 0);

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
		// Escape does nothing on the main menu — quit via the Quit button only.
	}

	private void OnPlay()
	{
		SlotTheory.Data.DataLoader.LoadAll();
		// Demo build skips mode select and goes straight to map select (no campaign available)
		string destination = Core.Balance.IsDemo
			? "res://Scenes/MapSelect.tscn"
			: "res://Scenes/ModeSelect.tscn";
		Transition.Instance?.FadeToScene(destination);
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
			new Color(0.018f, 0.028f, 0.080f),
			new Color(0.16f, 0.28f, 0.36f),
			border: 1, corners: 8, glowAlpha: 0.08f, glowSize: 3, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("hover", UITheme.MakeBtn(
			new Color(0.028f, 0.050f, 0.10f),
			new Color(0.30f, 0.78f, 0.84f),
			border: 2, corners: 8, glowAlpha: 0.18f, glowSize: 6, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("focus", UITheme.MakeBtn(
			new Color(0.026f, 0.044f, 0.095f),
			new Color(0.30f, 0.78f, 0.84f),
			border: 2, corners: 8, glowAlpha: 0.16f, glowSize: 5, glowColor: UITheme.Cyan));
		btn.AddThemeStyleboxOverride("pressed", UITheme.MakeBtn(
			new Color(0.018f, 0.024f, 0.078f),
			new Color(0.20f, 0.50f, 0.54f),
			border: 2, corners: 8, glowAlpha: 0.08f, glowSize: 3, glowColor: UITheme.Cyan));
		btn.Draw += () =>
		{
			float bw = btn.Size.X;
			btn.DrawLine(new Vector2(11f, 2f), new Vector2(bw - 11f, 2f), new Color(UITheme.Cyan, 0.22f), 1f);
			btn.DrawLine(new Vector2(11f, btn.Size.Y - 2f), new Vector2(bw - 11f, btn.Size.Y - 2f), new Color(0f, 0f, 0f, 0.34f), 1f);
			btn.DrawRect(new Rect2(5f, 5f, bw - 10f, btn.Size.Y - 10f), new Color(0.80f, 0.95f, 1.0f, 0.10f), false, 1f);
			btn.DrawLine(new Vector2(8f, 6f), new Vector2(bw - 8f, 6f), new Color(0.90f, 0.99f, 1.0f, 0.19f), 1f);
		};
		AddButtonSurface(btn, UITheme.Cyan, 0.10f, 0.12f);
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
		ls.FontSize = 70;
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
			bg:          new Color(0.024f, 0.020f, 0.090f),
			border:      new Color(0.56f, 0.44f, 0.86f, 0.88f),
			corners:     6,
			borderWidth: 2,
			padH:        16,
			padV:        10);
		bannerStyle.ShadowColor = new Color(0.55f, 0.28f, 0.90f, 0.20f);
		bannerStyle.ShadowSize = 7;
		bannerStyle.ShadowOffset = new Vector2(0f, 2f);
		panel.AddThemeStyleboxOverride("panel", bannerStyle);

		panel.Draw += () =>
		{
			float w = panel.Size.X;
			float h = panel.Size.Y;
			float pulse = 0.5f + 0.5f * Mathf.Sin(_menuAnimTime * 0.72f + 1.1f);
			panel.DrawRect(new Rect2(3f, 3f, w - 6f, h - 6f), new Color(0.66f, 0.48f, 0.98f, 0.10f + pulse * 0.04f), false, 1f);
			panel.DrawRect(new Rect2(4f, 4f, w - 8f, h - 8f), new Color(0.05f, 0.08f, 0.14f, 0.24f));
			panel.DrawRect(new Rect2(8f, 8f, w - 16f, h - 16f), new Color(0.012f, 0.020f, 0.060f, 0.88f));
			panel.DrawRect(new Rect2(8f, 8f, w - 16f, h - 16f), new Color(0.22f, 0.46f, 0.60f, 0.09f), false, 1f);
			panel.DrawRect(new Rect2(11f, 11f, w - 22f, h - 22f), new Color(0.008f, 0.012f, 0.040f, 0.78f));
			panel.DrawRect(new Rect2(5f, 5f, w - 10f, h - 10f), new Color(0.92f, 0.86f, 1.00f, 0.10f), false, 1f);
			panel.DrawLine(new Vector2(8f, 3f), new Vector2(w - 8f, 3f), new Color(0.88f, 0.72f, 1.00f, 0.34f), 1f);
			panel.DrawLine(new Vector2(9f, 4f), new Vector2(w - 9f, 4f), new Color(0.66f, 0.84f, 0.98f, 0.16f), 1f);
			panel.DrawRect(new Rect2((w - 42f) * 0.5f, 2f, 42f, 3f), new Color(0.84f, 0.68f, 1.0f, 0.18f + pulse * 0.04f));
			panel.DrawRect(new Rect2(10f, h - 6f, w - 20f, 2f), new Color(0.70f, 0.42f, 0.95f, 0.20f + pulse * 0.05f));
			panel.DrawRect(new Rect2((w - 34f) * 0.5f, h - 6f, 34f, 2f), new Color(0.68f, 0.84f, 0.95f, 0.18f + pulse * 0.05f));
			panel.DrawRect(new Rect2(14f, h - 4f, 24f, 2f), new Color(0.52f, 0.64f, 1.0f, 0.28f + pulse * 0.05f));
			panel.DrawRect(new Rect2(w - 38f, h - 4f, 24f, 2f), new Color(0.52f, 0.64f, 1.0f, 0.28f + pulse * 0.05f));
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

		if (Balance.IsDemo && Balance.FullGameSteamAppId != 0u)
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
			float breath = 0.5f + 0.5f * MathF.Sin(_time * 0.30f);
			var core = new Vector2(w * 0.50f, h * 0.50f);

			// Distributed cloud field so the atmosphere reads wispy instead of circular overlays.
			for (int i = 0; i < 140; i++)
			{
				float t = i + 1f;
				float hx = Hash01(t * 3.17f + 0.8f);
				float hy = Hash01(t * 5.03f + 1.9f);
				float hr = Hash01(t * 7.91f + 2.6f);
				var p = new Vector2(
					core.X + (hx - 0.5f) * w * 0.44f,
					core.Y + (hy - 0.5f) * h * 0.30f);
				float r = w * (0.006f + hr * 0.020f);
				float a = (0.002f + hr * 0.004f) * (0.82f + 0.18f * breath);
				Color c = hr > 0.68f
					? new Color(0.10f, 0.34f, 0.48f, a)
					: new Color(0.05f, 0.17f, 0.26f, a);
				DrawCircle(p, r, c);
			}

			// Left cyan-green nebula pocket.
			for (int i = 0; i < 150; i++)
			{
				float t = i + 1f;
				float hx = Hash01(t * 9.31f + 0.6f);
				float hy = Hash01(t * 7.87f + 2.2f);
				float hr = Hash01(t * 5.61f + 1.4f);
				var p = new Vector2(
					w * 0.34f + (hx - 0.5f) * w * 0.24f,
					h * 0.62f + (hy - 0.5f) * h * 0.24f);
				float r = w * (0.003f + hr * 0.010f);
				float a = (0.0018f + hr * 0.0038f) * (0.80f + 0.20f * breath);
				DrawCircle(p, r, new Color(0.07f, 0.30f, 0.26f, a));
			}

			// Right blue-violet pocket behind demo card.
			for (int i = 0; i < 110; i++)
			{
				float t = i + 1f;
				float hx = Hash01(t * 6.11f + 0.4f);
				float hy = Hash01(t * 4.87f + 1.6f);
				float hr = Hash01(t * 8.61f + 2.4f);
				var p = new Vector2(
					w * 0.63f + (hx - 0.5f) * w * 0.18f,
					h * 0.50f + (hy - 0.5f) * h * 0.18f);
				float r = w * (0.003f + hr * 0.009f);
				float a = (0.0015f + hr * 0.0034f) * (0.80f + 0.20f * breath);
				DrawCircle(p, r, new Color(0.06f, 0.22f, 0.34f, a));
			}

			DrawCircle(new Vector2(w * 0.34f, h * 0.62f), w * 0.11f, new Color(0.08f, 0.36f, 0.30f, 0.022f + breath * 0.006f));
			DrawCircle(new Vector2(w * 0.57f, h * 0.50f), w * 0.12f, new Color(0.07f, 0.26f, 0.42f, 0.020f + breath * 0.006f));
			DrawCircle(new Vector2(w * 0.50f, h * 0.56f), w * 0.14f, new Color(0.05f, 0.18f, 0.30f, 0.016f + breath * 0.004f));

			// Fine haze grain to avoid visible large circles.
			for (int i = 0; i < 180; i++)
			{
				float t = i + 1f;
				float hx = Hash01(t * 12.17f + 0.2f);
				float hy = Hash01(t * 9.37f + 1.1f);
				float hr = Hash01(t * 6.77f + 2.5f);
				var p = new Vector2(
					w * 0.50f + (hx - 0.5f) * w * 0.52f,
					h * 0.54f + (hy - 0.5f) * h * 0.40f);
				float r = w * (0.0012f + hr * 0.0056f);
				float a = 0.0016f + hr * 0.0035f;
				Color c = hr > 0.64f
					? new Color(0.10f, 0.38f, 0.48f, a)
					: new Color(0.05f, 0.20f, 0.30f, a);
				DrawCircle(p, r, c);
			}

			// Local glow pockets around the panel side emitters.
			DrawCircle(new Vector2(w * 0.40f, h * 0.58f), w * 0.028f, new Color(0.30f, 0.96f, 1.0f, 0.14f));
			DrawCircle(new Vector2(w * 0.60f, h * 0.58f), w * 0.028f, new Color(0.66f, 0.58f, 1.0f, 0.12f));
			DrawCircle(new Vector2(w * 0.40f, h * 0.58f), w * 0.016f, new Color(0.80f, 1.0f, 1.0f, 0.22f));
			DrawCircle(new Vector2(w * 0.60f, h * 0.58f), w * 0.016f, new Color(0.94f, 0.86f, 1.0f, 0.20f));

			DrawRect(new Rect2(0f, 0f, w * 0.09f, h), new Color(0f, 0f, 0f, 0.035f));
			DrawRect(new Rect2(w * 0.91f, 0f, w * 0.09f, h), new Color(0f, 0f, 0f, 0.035f));
		}

		private static float Hash01(float x)
		{
			float s = MathF.Sin(x) * 43758.5453f;
			return s - MathF.Floor(s);
		}
	}
}
