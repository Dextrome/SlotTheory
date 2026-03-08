using Godot;
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

		AddSpacer(vbox, 28);

		// Menu card
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
			bg: new Color(0.04f, 0.04f, 0.12f),
			border: new Color(0.18f, 0.22f, 0.18f),
			corners: 12, borderWidth: 1, padH: 24, padV: 24));
		card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		card.CustomMinimumSize   = new Vector2(320, 0);
		vbox.AddChild(card);

		var cardVbox = new VBoxContainer();
		cardVbox.AddThemeConstantOverride("separation", 8);
		card.AddChild(cardVbox);

		// Play — primary
		var playBtn = MakeMenuButton("PLAY", 260, 56, 24);
		UITheme.ApplyPrimaryStyle(playBtn);
		playBtn.Pressed += OnPlay;
		cardVbox.AddChild(playBtn);

		AddSpacer(cardVbox, 4);
		AddSeparator(cardVbox);
		AddSpacer(cardVbox, 4);

		AddNavButton(cardVbox, "Leaderboards", OnLeaderboards);
		AddNavButton(cardVbox, "Achievements",  OnAchievements);
		AddNavButton(cardVbox, "How to Play",   OnHowToPlay);
		AddNavButton(cardVbox, "Settings",      OnSettings);

		AddSpacer(cardVbox, 4);
		AddSeparator(cardVbox);
		AddSpacer(cardVbox, 4);

		// Quit — muted
		var quitBtn = MakeMenuButton("Quit", 260, 42, 18);
		UITheme.ApplyMutedStyle(quitBtn);
		quitBtn.Pressed += OnQuit;
		cardVbox.AddChild(quitBtn);

		// Version label (bottom-right, outside card)
		var versionLabel = new Label
		{
			Text = "v0.1.5",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment   = VerticalAlignment.Bottom,
			AnchorLeft   = 1f, AnchorRight  = 1f,
			AnchorTop    = 1f, AnchorBottom = 1f,
			OffsetLeft   = -80f, OffsetRight  = -10f,
			OffsetTop    = -28f, OffsetBottom = -8f,
			MouseFilter  = Control.MouseFilterEnum.Ignore,
		};
		versionLabel.AddThemeFontSizeOverride("font_size", 13);
		versionLabel.Modulate = new Color(0.30f, 0.30f, 0.38f);
		canvas.AddChild(versionLabel);
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
	private void OnHowToPlay()    => Transition.Instance?.FadeToScene("res://Scenes/HowToPlay.tscn");
	private void OnSettings()     => Transition.Instance?.FadeToScene("res://Scenes/Settings.tscn");
	private void OnQuit()         => GetTree().Quit();

	private void AutoResumeRun()
	{
		Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private void AddNavButton(VBoxContainer parent, string text, System.Action callback)
	{
		var btn = MakeMenuButton(text, 260, 44, 20);
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
}
