namespace SlotTheory.Entities;

/// <summary>Plain C# wrapper for a fixed slot. Tower is null when the slot is empty.</summary>
public class SlotInstance
{
    public int Index { get; }
    public ITowerView? Tower { get; set; }

    /// <summary>
    /// Returns the Tower cast to TowerInstance (the Godot node type).
    /// Use this in production code that needs Godot-specific methods (Free, QueueFree, visuals).
    /// Always non-null in production when Tower is non-null; null in unit tests using FakeTower.
    /// </summary>
    public TowerInstance? TowerNode => Tower as TowerInstance;

    /// <summary>Set by DraftSystem when a tower card is selected but not yet instantiated in scene.</summary>
    public string? PendingTowerId { get; set; }

    public SlotInstance(int index) => Index = index;
}
