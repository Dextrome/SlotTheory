using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen score overlay shown at run end (win or loss).
/// Any left-click returns the player to the main menu.
/// </summary>
public partial class EndScreen : CanvasLayer
{
	private Label _titleLabel = null!;
	private Label _subtitleLabel = null!;

	public override void _Ready()
	{
		Layer = 10;
		Visible = false;

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color(0f, 0f, 0f, 0.88f);
		root.AddChild(bg);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(center);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		center.AddChild(vbox);

		_titleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 72);
		vbox.AddChild(_titleLabel);

		_subtitleLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_subtitleLabel.AddThemeFontSizeOverride("font_size", 28);
		vbox.AddChild(_subtitleLabel);

		var spacer = new Control { CustomMinimumSize = new Vector2(0, 32) };
		vbox.AddChild(spacer);

		var hint = new Label
		{
			Text = "Click anywhere to return to menu",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", 18);
		hint.Modulate = new Color(0.65f, 0.65f, 0.65f);
		vbox.AddChild(hint);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
		GetViewport().SetInputAsHandled();
		GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
	}

	public void ShowWin()
	{
		_titleLabel.Text = "VICTORY";
		_titleLabel.Modulate = new Color(0.3f, 1.0f, 0.5f);
		_subtitleLabel.Text = $"All {Balance.TotalWaves} waves survived!";
		Visible = true;
	}

	public void ShowLoss(int waveReached, int livesLost)
	{
		_titleLabel.Text = "GAME OVER";
		_titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_subtitleLabel.Text = $"Reached wave {waveReached} / {Balance.TotalWaves}  ·  Lives lost: {livesLost}";
		Visible = true;
	}
}
