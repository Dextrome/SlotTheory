using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen overlay shown on run win or loss. Sits above everything else.
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

        _titleLabel = new Label();
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 72);
        vbox.AddChild(_titleLabel);

        _subtitleLabel = new Label();
        _subtitleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _subtitleLabel.AddThemeFontSizeOverride("font_size", 28);
        vbox.AddChild(_subtitleLabel);

        // Spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 20);
        vbox.AddChild(spacer);

        var restartBtn = new Button();
        restartBtn.Text = "Restart Run";
        restartBtn.CustomMinimumSize = new Vector2(220, 50);
        restartBtn.AddThemeFontSizeOverride("font_size", 22);
        restartBtn.Pressed += () => GameController.Instance.RestartRun();
        vbox.AddChild(restartBtn);

        var quitBtn = new Button();
        quitBtn.Text = "Quit to Desktop";
        quitBtn.CustomMinimumSize = new Vector2(220, 50);
        quitBtn.AddThemeFontSizeOverride("font_size", 22);
        quitBtn.Pressed += () => GetTree().Quit();
        vbox.AddChild(quitBtn);
    }

    public void ShowWin()
    {
        _titleLabel.Text = "YOU WON!";
        _titleLabel.Modulate = new Color(0.3f, 1.0f, 0.5f);
        _subtitleLabel.Text = $"All {Balance.TotalWaves} waves survived.";
        Visible = true;
    }

    public void ShowLoss(int waveReached, int livesLost)
    {
        _titleLabel.Text = "RUN OVER";
        _titleLabel.Modulate = new Color(1.0f, 0.35f, 0.35f);
        _subtitleLabel.Text = $"Reached wave {waveReached}  ·  Lost {livesLost} lives";
        Visible = true;
    }
}
