namespace SlotTheory.Entities;

/// <summary>Plain C# wrapper for a fixed slot. Tower is null when the slot is empty.</summary>
public class SlotInstance
{
    public int Index { get; }
    public TowerInstance? Tower { get; set; }

    /// <summary>Set by DraftSystem when a tower card is selected but not yet instantiated in scene.</summary>
    public string? PendingTowerId { get; set; }

    public SlotInstance(int index) => Index = index;
}
