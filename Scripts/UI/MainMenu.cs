using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Entry-point scene shown before a run starts and returned to after each run ends.
/// All UI built procedurally — no .tscn children required.
/// </summary>
public partial class MainMenu : Node
{
	public override void _Ready()
	{
		if (MobileOptimization.IsMobile() && SlotTheory.Core.MobileRunSession.HasSnapshot())
		{
			CallDeferred(nameof(AutoResumeRun));
			return;
		}

		var canvas = new CanvasLayer();
		AddChild(canvas);

		// Background
		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color("#141420");
		canvas.AddChild(bg);

		// Animated neon grid — rendered above solid bg, below all UI
		var grid = new NeonGridBg();
		grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		grid.MouseFilter = Control.MouseFilterEnum.Ignore;
		canvas.AddChild(grid);

		// Centre everything
		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		center.Theme = SlotTheory.Core.UITheme.Build();
		canvas.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		center.AddChild(vbox);

		// Title
		var title = new Label
		{
			Text = "SLOT THEORY",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		SlotTheory.Core.UITheme.ApplyFont(title, semiBold: true, size: 80);
		title.Modulate = new Color("#a6d608");
		vbox.AddChild(title);

		// Sub-title
		var sub = new Label
		{
			Text = "Tower Defense  ·  Draft  ·  Survive 20 Waves",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		sub.AddThemeFontSizeOverride("font_size", 20);
		sub.Modulate = new Color(0.65f, 0.65f, 0.65f);
		vbox.AddChild(sub);

		AddSpacer(vbox, 36);

		AddButton(vbox, "Play",            260, 58, 28, OnPlay);
		AddSpacer(vbox, 8);
		AddButton(vbox, "Leaderboards",    260, 48, 22, OnLeaderboards);
		AddButton(vbox, "How to Play",    260, 48, 22, OnHowToPlay);
		AddButton(vbox, "Settings",       260, 48, 22, OnSettings);
		AddSpacer(vbox, 4);
		AddButton(vbox, "Quit", 260, 48, 22, OnQuit);

		var versionLabel = new Label
		{
			Text = "v0.1.5",
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Bottom,
			AnchorLeft = 1f, AnchorRight = 1f,
			AnchorTop = 1f,  AnchorBottom = 1f,
			OffsetLeft = -80f, OffsetRight = -10f,
			OffsetTop = -28f,  OffsetBottom = -8f,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		versionLabel.AddThemeFontSizeOverride("font_size", 13);
		versionLabel.Modulate = new Color(0.38f, 0.38f, 0.38f);
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
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
	}
	private void OnLeaderboards()
	{
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Leaderboards.tscn");
	}
	private void OnHowToPlay() => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/HowToPlay.tscn");
	private void OnSettings()  => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Settings.tscn");
	private void OnQuit()      => GetTree().Quit();

	private void AutoResumeRun()
	{
		SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	}

	private static void AddSpacer(VBoxContainer vbox, int px)
	{
		var s = new Control { CustomMinimumSize = new Vector2(0, px) };
		vbox.AddChild(s);
	}

	private static void AddButton(VBoxContainer vbox, string text,
		int minW, int minH, int fontSize, System.Action callback)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(minW, minH),
		};
		btn.AddThemeFontSizeOverride("font_size", fontSize);
		btn.Pressed += callback;
		btn.MouseEntered += () => SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
		vbox.AddChild(btn);
	}
}
