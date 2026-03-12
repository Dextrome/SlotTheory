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
    public float TotalPlayTime    { get; set; } = 0f;  // Total seconds spent in waves

    // Automation / balancing metrics
    public int BaseAttackDamage { get; private set; } = 0;
    public int SurgeCoreDamage { get; private set; } = 0;
    public int ExplosionFollowUpDamage { get; private set; } = 0;
    public int ResidueDamage { get; private set; } = 0;
    public int SpectacleKills { get; private set; } = 0;
    public int SpectacleExplosionBurstCount { get; private set; } = 0;
    public int OverkillBloomCount { get; private set; } = 0;
    public int StatusDetonationCount { get; private set; } = 0;
    public int SpectacleMaxChainDepth { get; private set; } = 0;
    public float ResidueUptimeSeconds { get; private set; } = 0f;
    public int PeakSimultaneousExplosions { get; private set; } = 0;
    public int PeakSimultaneousActiveHazards { get; private set; } = 0;
    public int PeakSimultaneousHitStopsRequested { get; private set; } = 0;

    // Per-wave tracking for micro reports and loss analysis
    public WaveReport CurrentWave { get; private set; } = new();
    public List<WaveReport> CompletedWaves { get; } = new();

    // Loss analysis tracking
    public WaveReport? WorstWave => CompletedWaves.OrderByDescending(w => w.Leaks).FirstOrDefault();
    public Dictionary<string, int>   TotalLeaksByType  { get; } = new();
    public Dictionary<string, float> TotalLeakHpByType { get; } = new();  // sum of HP remaining on leaked enemies per type
    public string? LastLeakedType { get; set; }

    // Fire rate utilization tracking (bot mode only)
    public int[] SlotEligibleSteps { get; } = new int[Balance.SlotCount];  // steps where tower was ready to fire
    public int[] SlotFiredSteps    { get; } = new int[Balance.SlotCount];   // steps where tower actually fired

    // Spectacle trigger tracking (run-wide)
    public int SpectacleSurgeTriggers { get; private set; } = 0;
    public int SpectacleGlobalTriggers { get; private set; } = 0;
    public Dictionary<string, int> SpectacleSurgeByEffect { get; } = new();
    public Dictionary<string, int> SpectacleGlobalByEffect { get; } = new();
    public Dictionary<string, int> SpectacleSurgeByTower { get; } = new();

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
            ITowerView? tower = Slots[i].Tower;
            if (tower != null)
            {
                CurrentWave.TowerStats.Add(new TowerWaveStats
                {
                    TowerId = tower.TowerId,
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

    /// <summary>Tracks HP remaining on a leaked enemy (bot mode diagnostics).</summary>
    public void TrackLeakHp(string enemyType, float remainingHp)
    {
        TotalLeakHpByType.TryGetValue(enemyType, out float current);
        TotalLeakHpByType[enemyType] = current + remainingHp;
    }

    public void TrackSpectacleSurge(string effectId, string? towerId = null)
    {
        SpectacleSurgeTriggers++;
        IncrementSpectacleCounter(SpectacleSurgeByEffect, effectId);
        if (!string.IsNullOrWhiteSpace(towerId))
            IncrementSpectacleCounter(SpectacleSurgeByTower, towerId!);
    }

    public void TrackSpectacleGlobal(string effectId)
    {
        SpectacleGlobalTriggers++;
        IncrementSpectacleCounter(SpectacleGlobalByEffect, effectId);
    }

    public void TrackBaseAttackDamage(int slotIndex, int damageDealt, bool isKill)
    {
        if (damageDealt <= 0)
            return;

        BaseAttackDamage += damageDealt;
        TotalDamageDealt += damageDealt;
        if (slotIndex >= 0)
            TrackTowerDamage(slotIndex, damageDealt);

        if (!isKill)
            return;

        TotalKills += 1;
        if (slotIndex >= 0)
            TrackTowerKill(slotIndex);
    }

    public void TrackSpectacleDamage(int slotIndex, int damageDealt, bool isKill, SpectacleDamageSource source)
    {
        if (damageDealt <= 0)
            return;

        switch (source)
        {
            case SpectacleDamageSource.Residue:
                ResidueDamage += damageDealt;
                break;
            case SpectacleDamageSource.ExplosionFollowUp:
                ExplosionFollowUpDamage += damageDealt;
                break;
            default:
                SurgeCoreDamage += damageDealt;
                break;
        }

        TotalDamageDealt += damageDealt;
        if (slotIndex >= 0)
            TrackTowerDamage(slotIndex, damageDealt);

        if (!isKill)
            return;

        SpectacleKills += 1;
        TotalKills += 1;
        if (slotIndex >= 0)
            TrackTowerKill(slotIndex);
    }

    public void TrackSpectacleExplosionBurst()
    {
        SpectacleExplosionBurstCount += 1;
    }

    public void TrackOverkillBloom()
    {
        OverkillBloomCount += 1;
    }

    public void TrackStatusDetonation(int detonations)
    {
        if (detonations <= 0)
            return;
        StatusDetonationCount += detonations;
    }

    public void TrackSpectacleChainDepth(int depth)
    {
        if (depth > SpectacleMaxChainDepth)
            SpectacleMaxChainDepth = depth;
    }

    public void TrackResidueUptime(float deltaSeconds, int activeHazards)
    {
        if (deltaSeconds <= 0f || activeHazards <= 0)
            return;
        ResidueUptimeSeconds += deltaSeconds * activeHazards;
    }

    public void TrackFrameStressProxies(int simultaneousExplosions, int simultaneousActiveHazards, int simultaneousHitStopsRequested)
    {
        if (simultaneousExplosions > PeakSimultaneousExplosions)
            PeakSimultaneousExplosions = simultaneousExplosions;
        if (simultaneousActiveHazards > PeakSimultaneousActiveHazards)
            PeakSimultaneousActiveHazards = simultaneousActiveHazards;
        if (simultaneousHitStopsRequested > PeakSimultaneousHitStopsRequested)
            PeakSimultaneousHitStopsRequested = simultaneousHitStopsRequested;
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
        TotalPlayTime = 0f;
        BaseAttackDamage = 0;
        SurgeCoreDamage = 0;
        ExplosionFollowUpDamage = 0;
        ResidueDamage = 0;
        SpectacleKills = 0;
        SpectacleExplosionBurstCount = 0;
        OverkillBloomCount = 0;
        StatusDetonationCount = 0;
        SpectacleMaxChainDepth = 0;
        ResidueUptimeSeconds = 0f;
        PeakSimultaneousExplosions = 0;
        PeakSimultaneousActiveHazards = 0;
        PeakSimultaneousHitStopsRequested = 0;
        for (int i = 0; i < Slots.Length; i++)
            Slots[i] = new SlotInstance(i);
            
        // Reset wave tracking
        CurrentWave = new();
        CompletedWaves.Clear();
        TotalLeaksByType.Clear();
        TotalLeakHpByType.Clear();
        LastLeakedType = null;
        System.Array.Clear(SlotEligibleSteps, 0, SlotEligibleSteps.Length);
        System.Array.Clear(SlotFiredSteps,    0, SlotFiredSteps.Length);
        SpectacleSurgeTriggers = 0;
        SpectacleGlobalTriggers = 0;
        SpectacleSurgeByEffect.Clear();
        SpectacleGlobalByEffect.Clear();
        SpectacleSurgeByTower.Clear();
    }

    /// <summary>Gets total damage dealt by a specific tower across all completed waves.</summary>
    public int GetTowerTotalDamage(int slotIndex)
    {
        return CompletedWaves.SelectMany(w => w.TowerStats)
                           .Where(t => t.SlotIndex == slotIndex)
                           .Sum(t => t.Damage);
    }

    /// <summary>Gets total kills by a specific tower across all completed waves.</summary>
    public int GetTowerTotalKills(int slotIndex)
    {
        return CompletedWaves.SelectMany(w => w.TowerStats)
                           .Where(t => t.SlotIndex == slotIndex)
                           .Sum(t => t.Kills);
    }

    /// <summary>Calculates DPS for a specific tower (damage per second).</summary>
    public float GetTowerDPS(int slotIndex)
    {
        if (TotalPlayTime <= 0f) return 0f;
        return GetTowerTotalDamage(slotIndex) / TotalPlayTime;
    }

    private static void IncrementSpectacleCounter(Dictionary<string, int> counters, string effectId)
    {
        string key = string.IsNullOrWhiteSpace(effectId) ? "UNKNOWN_EFFECT" : effectId;
        counters.TryGetValue(key, out int n);
        counters[key] = n + 1;
    }
}
