using System.Collections.Generic;
using SlotTheory.Entities;

namespace SlotTheory.Core;

/// <summary>Single source of truth for all runtime run data.</summary>
public class RunState
{
    public int WaveIndex { get; set; } = 0;
    public SlotInstance[] Slots { get; } = new SlotInstance[Balance.SlotCount];
    public List<EnemyInstance> EnemiesAlive { get; } = new();
    public int EnemiesSpawnedThisWave { get; set; } = 0;
    public float WaveTime { get; set; } = 0f;

    public RunState()
    {
        for (int i = 0; i < Balance.SlotCount; i++)
            Slots[i] = new SlotInstance(i);
    }

    public bool HasFreeSlots() => System.Array.Exists(Slots, s => s.Tower == null);
    public int FreeSlotCount() => System.Array.FindAll(Slots, s => s.Tower == null).Length;
}
