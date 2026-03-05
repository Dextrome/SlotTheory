using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Draws the neon-synthwave background: dark space + purple grid + violet horizon line.
/// Added as first child of _mapVisuals so it renders behind everything else.
/// </summary>
public partial class GridBackground : Node2D
{
    public override void _Draw()
    {
        // Get viewport size to extend background on wider screens
        var viewportSize = GetViewport().GetVisibleRect().Size;
        float bgWidth = Mathf.Max(1280f, viewportSize.X);
        
        // Dark space background - extend to fill full viewport width
        DrawRect(new Rect2(0, 0, bgWidth, 720), new Color(0.04f, 0.00f, 0.10f));

        var glow = new Color(0.50f, 0.00f, 0.80f, 0.07f);
        var line = new Color(0.40f, 0.00f, 0.65f, 0.28f);
        float top = MapGenerator.GRID_Y;
        float bot = top + MapGenerator.ROWS * MapGenerator.CELL_H;

        // Vertical grid lines
        for (int c = 0; c <= MapGenerator.COLS; c++)
        {
            float x = c * MapGenerator.CELL_W;
            DrawLine(new Vector2(x, top), new Vector2(x, bot), glow, 6f);
            DrawLine(new Vector2(x, top), new Vector2(x, bot), line, 1.5f);
        }

        // Horizontal grid lines - extend to fill full viewport width
        float lineWidth = Mathf.Max(1280f, viewportSize.X);
        for (int r = 0; r <= MapGenerator.ROWS; r++)
        {
            float y = top + r * MapGenerator.CELL_H;
            DrawLine(new Vector2(0f, y), new Vector2(lineWidth, y), glow, 6f);
            DrawLine(new Vector2(0f, y), new Vector2(lineWidth, y), line, 1.5f);
        }

        // Horizon line — bright violet bar at top of play area
        DrawLine(new Vector2(0f, top), new Vector2(lineWidth, top),
            new Color(0.70f, 0.00f, 1.00f, 0.12f), 14f);
        DrawLine(new Vector2(0f, top), new Vector2(lineWidth, top),
            new Color(0.70f, 0.00f, 1.00f, 0.65f), 2f);
    }
}
