using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Draws the neon-synthwave background: dark space + purple grid + violet horizon line.
/// Added as first child of _mapVisuals so it renders behind everything else.
/// </summary>
public partial class GridBackground : Node2D
{
    private const float BgHalfExtent = 4096f;

    public override void _Draw()
    {
        // Draw a large world-space backdrop so camera panning never reveals clear color.
        DrawRect(
            new Rect2(-BgHalfExtent, -BgHalfExtent, BgHalfExtent * 2f, BgHalfExtent * 2f),
            new Color(0.04f, 0.00f, 0.10f)
        );

        var glow = new Color(0.50f, 0.00f, 0.80f, 0.07f);
        var line = new Color(0.40f, 0.00f, 0.65f, 0.28f);
        float top = MapGenerator.GRID_Y;
        float bot = top + MapGenerator.ROWS * MapGenerator.CELL_H;
        float minX = -BgHalfExtent;
        float maxX = BgHalfExtent;

        // Vertical grid lines continued left/right of the map body.
        int startCol = Mathf.FloorToInt(minX / MapGenerator.CELL_W) - 1;
        int endCol = Mathf.CeilToInt(maxX / MapGenerator.CELL_W) + 1;
        for (int c = startCol; c <= endCol; c++)
        {
            float x = c * MapGenerator.CELL_W;
            DrawLine(new Vector2(x, top), new Vector2(x, bot), glow, 6f);
            DrawLine(new Vector2(x, top), new Vector2(x, bot), line, 1.5f);
        }

        // Horizontal grid lines across the same wide world span.
        for (int r = 0; r <= MapGenerator.ROWS; r++)
        {
            float y = top + r * MapGenerator.CELL_H;
            DrawLine(new Vector2(minX, y), new Vector2(maxX, y), glow, 6f);
            DrawLine(new Vector2(minX, y), new Vector2(maxX, y), line, 1.5f);
        }

        // Horizon line at the top of the play area.
        DrawLine(new Vector2(minX, top), new Vector2(maxX, top),
            new Color(0.70f, 0.00f, 1.00f, 0.12f), 14f);
        DrawLine(new Vector2(minX, top), new Vector2(maxX, top),
            new Color(0.70f, 0.00f, 1.00f, 0.65f), 2f);
    }
}
