using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Core;

/// <summary>Tracks damage and kills for a single tower in the current wave.</summary>
public class TowerWaveStats
{
    public int Damage { get; set; } = 0;
    public int Kills { get; set; } = 0;
    public string TowerId { get; set; } = "";
    public int SlotIndex { get; set; } = -1;
}

/// <summary>Tracks what happened during a wave for micro reports.</summary>
public class WaveReport
{
    public int WaveNumber { get; set; }
    public int Leaks { get; set; } = 0;
    public Dictionary<string, int> LeaksByType { get; set; } = new();
    public List<TowerWaveStats> TowerStats { get; set; } = new();
    public TowerWaveStats? TopDamageDealer => TowerStats.OrderByDescending(t => t.Damage).FirstOrDefault();
}

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

    // Per-wave tracking for micro reports and loss analysis
    public WaveReport CurrentWave { get; private set; } = new();
    public List<WaveReport> CompletedWaves { get; } = new();

    // Loss analysis tracking
    public WaveReport? WorstWave => CompletedWaves.OrderByDescending(w => w.Leaks).FirstOrDefault();
    public Dictionary<string, int> TotalLeaksByType { get; } = new();
    public string? LastLeakedType { get; set; }

    // Map selection
    public string? SelectedMapId { get; set; } = null;  // null = random
    public int RngSeed { get; set; } = 0;

    public RunState()
    {
        for (int i = 0; i < Balance.SlotCount; i++)
            Slots[i] = new SlotInstance(i);
    }

    public bool HasFreeSlots() => System.Array.Exists(Slots, s => s.Tower == null);
    public int FreeSlotCount() => System.Array.FindAll(Slots, s => s.Tower == null).Length;

    /// <summary>Called at the start of each new wave to reset tracking.</summary>
    public void StartNewWave(int waveNumber)
    {
        CurrentWave = new WaveReport { WaveNumber = waveNumber };
        // Initialize tower stats for all placed towers
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].Tower != null)
            {
                CurrentWave.TowerStats.Add(new TowerWaveStats
                {
                    TowerId = Slots[i].Tower.TowerId,
                    SlotIndex = i
                });
            }
        }
    }

    /// <summary>Called when a wave completes to archive the report.</summary>
    public WaveReport CompleteWave()
    {
        CompletedWaves.Add(CurrentWave);
        return CurrentWave;
    }

    /// <summary>Tracks damage dealt by a specific tower for micro reports.</summary>
    public void TrackTowerDamage(int slotIndex, int damage)
    {
        var towerStat = CurrentWave.TowerStats.Find(t => t.SlotIndex == slotIndex);
        if (towerStat != null)
        {
            towerStat.Damage += damage;
        }
    }

    /// <summary>Tracks a kill by a specific tower for micro reports.</summary>
    public void TrackTowerKill(int slotIndex)
    {
        var towerStat = CurrentWave.TowerStats.Find(t => t.SlotIndex == slotIndex);
        if (towerStat != null)
        {
            towerStat.Kills++;
        }
    }

    /// <summary>Tracks an enemy leak for loss analysis.</summary>
    public void TrackLeak(string enemyType)
    {
        CurrentWave.Leaks++;
        CurrentWave.LeaksByType.TryGetValue(enemyType, out int currentCount);
        CurrentWave.LeaksByType[enemyType] = currentCount + 1;
        
        TotalLeaksByType.TryGetValue(enemyType, out int totalCount);
        TotalLeaksByType[enemyType] = totalCount + 1;
        
        LastLeakedType = enemyType;
    }

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
            
        // Reset wave tracking
        CurrentWave = new();
        CompletedWaves.Clear();
        TotalLeaksByType.Clear();
        LastLeakedType = null;
    }
}
