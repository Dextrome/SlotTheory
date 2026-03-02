using Godot;
using SlotTheory.Core;

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
	private Label _buildLabel    = null!;

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

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(center);

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

		_buildLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_buildLabel.AddThemeFontSizeOverride("font_size", 16);
		_buildLabel.Modulate = new Color(0.75f, 0.75f, 0.85f);
		_buildLabel.Visible = false;
		vbox.AddChild(_buildLabel);

		var spacer = new Control { CustomMinimumSize = new Vector2(0, 24) };
		vbox.AddChild(spacer);

		var hint = new Label
		{
			Text = "Click or press Enter to return to menu",
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", 18);
		hint.Modulate = new Color(0.65f, 0.65f, 0.65f);
		vbox.AddChild(hint);
	}

	public override void _Input(InputEvent @event)
	{
		if (!Visible) return;
		bool triggered = @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
		              || @event is InputEventKey { Pressed: true, KeyLabel: Key.Enter or Key.KpEnter or Key.Space };
		if (!triggered) return;
		GetViewport().SetInputAsHandled();
		Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
	}

	public void ShowWin(int kills, int damageDealt, string buildSummary)
	{
		_titleLabel.Text = "VICTORY";
		_titleLabel.Modulate = new Color(0.3f, 1.0f, 0.5f);
		_subtitleLabel.Text = $"All {Balance.TotalWaves} waves survived!";
		_statsLabel.Text = $"Enemies killed: {kills}  ·  Total damage: {damageDealt:N0}";
		_statsLabel.Visible = true;
		_buildLabel.Text = buildSummary;
		_buildLabel.Visible = buildSummary.Length > 0;
		Visible = true;
	}

	public void ShowLoss(int waveReached, int livesLost, int kills, int damageDealt, string buildSummary)
	{
		_titleLabel.Text = "GAME OVER";
		_titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
		_subtitleLabel.Text = $"Reached wave {waveReached} / {Balance.TotalWaves}  ·  Lives lost: {livesLost}";
		_statsLabel.Text = $"Enemies killed: {kills}  ·  Total damage: {damageDealt:N0}";
		_statsLabel.Visible = kills > 0 || damageDealt > 0;
		_buildLabel.Text = buildSummary;
		_buildLabel.Visible = buildSummary.Length > 0;
		Visible = true;
	}
}
