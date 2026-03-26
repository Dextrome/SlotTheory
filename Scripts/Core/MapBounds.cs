using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Shared map/world bounds used by gameplay and editor UX.
/// </summary>
public static class MapBounds
{
    public const float WorldMinX = 0f;
    public const float WorldMaxX = 1280f;
    public const float WorldMinY = 0f;
    public const float WorldMaxY = 720f;

    // Keep editor placement aligned with authored/procedural map content limits.
    public const float EditableMinX = MapGenerator.CELL_W / 2f; // 80
    public const float EditableMaxX = MapGenerator.COLS * MapGenerator.CELL_W - (MapGenerator.CELL_W / 2f); // 1200
    // Keep custom-map placement below the in-game top HUD/menu bar.
    public const float EditableMinY = MapGenerator.GRID_Y; // 80
    public const float EditableMaxY = WorldMaxY; // 720

    public static Vector2 ClampToEditable(Vector2 world)
        => new(
            Mathf.Clamp(world.X, EditableMinX, EditableMaxX),
            Mathf.Clamp(world.Y, EditableMinY, EditableMaxY));

    public static bool IsInsideEditable(Vector2 world)
        => world.X >= EditableMinX && world.X <= EditableMaxX
        && world.Y >= EditableMinY && world.Y <= EditableMaxY;
}
