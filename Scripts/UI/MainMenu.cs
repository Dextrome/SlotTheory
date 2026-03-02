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
		AddButton(vbox, "How to Play",    260, 48, 22, OnHowToPlay);
		AddButton(vbox, "Settings",       260, 48, 22, OnSettings);
		AddSpacer(vbox, 8);
		AddButton(vbox, "Quit to Desktop", 260, 48, 22, OnQuit);
	}

	private void OnPlay()      => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
	private void OnHowToPlay() => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/HowToPlay.tscn");
	private void OnSettings()  => SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/Settings.tscn");
	private void OnQuit()      => GetTree().Quit();

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
