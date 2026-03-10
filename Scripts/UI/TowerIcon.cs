using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Lightweight procedural icon for towers in leaderboard loadout strips.
/// </summary>
public partial class TowerIcon : Control
{
    private string _towerId = "";

    [Export]
    public string TowerId
    {
        get => _towerId;
        set
        {
            if (_towerId == value) return;
            _towerId = value ?? "";
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        if (CustomMinimumSize == Vector2.Zero)
            CustomMinimumSize = new Vector2(32f, 32f);
    }

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Mathf.Min(Size.X, Size.Y) * 0.34f;

        var colors = TowerId switch
        {
            "rapid_shooter" => (new Color(0.20f, 0.78f, 1.00f), new Color(0.02f, 0.12f, 0.24f)),
            "heavy_cannon" => (new Color(1.00f, 0.60f, 0.20f), new Color(0.24f, 0.12f, 0.03f)),
            "marker_tower" => (new Color(1.00f, 0.34f, 0.74f), new Color(0.24f, 0.08f, 0.20f)),
            "chain_tower" => (new Color(0.62f, 0.95f, 1.00f), new Color(0.05f, 0.15f, 0.25f)),
            "rift_prism" => (new Color(0.62f, 1.00f, 0.62f), new Color(0.08f, 0.20f, 0.10f)),
            _ => (new Color(0.42f, 0.48f, 0.60f), new Color(0.18f, 0.20f, 0.26f)),
        };

        DrawCircle(c, r + 6f, new Color(colors.Item1.R, colors.Item1.G, colors.Item1.B, 0.18f));
        DrawCircle(c, r + 1f, colors.Item1);
        DrawCircle(c, r - 2f, colors.Item2);

        if (string.IsNullOrEmpty(TowerId))
        {
            DrawLine(c + new Vector2(-r * 0.5f, 0), c + new Vector2(r * 0.5f, 0), new Color(0.8f, 0.85f, 0.95f, 0.65f), 1.6f);
            return;
        }

        string glyph = TowerId switch
        {
            "rapid_shooter" => "R",
            "heavy_cannon" => "H",
            "marker_tower" => "M",
            "chain_tower" => "C",
            "rift_prism" => "P",
            _ => "?",
        };

        const int fontSize = 16;
        var font = UITheme.Bold;
        float ascent = font.GetAscent(fontSize);
        float descent = font.GetDescent(fontSize);
        Vector2 glyphSize = font.GetStringSize(glyph, HorizontalAlignment.Left, -1, fontSize);
        // DrawString uses a baseline position. Compute it so the glyph is centered in the ring.
        Vector2 baseline = new Vector2(
            c.X - glyphSize.X * 0.5f,
            c.Y + (ascent - descent) * 0.5f);

        DrawString(font, baseline + new Vector2(1f, 1f), glyph, HorizontalAlignment.Left, -1, fontSize, new Color(0f, 0f, 0f, 0.65f));
        DrawString(font, baseline, glyph, HorizontalAlignment.Left, -1, fontSize, new Color(0.95f, 0.98f, 1.0f, 0.95f));
    }
}
