using System.Collections.Generic;
using SlotTheory.Entities;

namespace SlotTheory.Core;

/// <summary>Single source of truth for all runtime run data.</summary>
public class RunState
{
    public int WaveIndex { get; set; } = 0;
    public int Lives { get; set; } = Balance.StartingLives;
    public SlotInstance[] Slots { get; } = new SlotInstance[Balance.SlotCount];
    public List<EnemyInstance> EnemiesAlive { get; } = new();
    public int EnemiesSpawnedThisWave { get; set; } = 0;
    public float WaveTime { get; set; } = 0f;

    // Run-wide stats shown on the end screen
    public int   TotalKills       { get; set; } = 0;
    public int   TotalDamageDealt { get; set; } = 0;

    public RunState()
    {
        for (int i = 0; i < Balance.SlotCount; i++)
            Slots[i] = new SlotInstance(i);
    }

    public bool HasFreeSlots() => System.Array.Exists(Slots, s => s.Tower == null);
    public int FreeSlotCount() => System.Array.FindAll(Slots, s => s.Tower == null).Length;

    public void Reset()
    {
        WaveIndex = 0;
        Lives = Balance.StartingLives;
        EnemiesAlive.Clear();
        EnemiesSpawnedThisWave = 0;
        WaveTime = 0f;
        TotalKills = 0;
        TotalDamageDealt = 0;
        for (int i = 0; i < Slots.Length; i++)
            Slots[i] = new SlotInstance(i);
    }
}
