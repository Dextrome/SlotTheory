using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>Unit tests for RunState - pure C#, no Godot dependencies.</summary>
public class RunStateTests
{
    // ── HasFreeSlots ──────────────────────────────────────────────────────

    [Fact]
    public void HasFreeSlots_AllEmpty_ReturnsTrue()
    {
        var state = new RunState();
        Assert.True(state.HasFreeSlots());
    }

    [Fact]
    public void HasFreeSlots_AllFull_ReturnsFalse()
    {
        var state = new RunState();
        var tower = new FakeTower();
        for (int i = 0; i < Balance.SlotCount; i++)
            state.Slots[i].Tower = tower;

        Assert.False(state.HasFreeSlots());
    }

    [Fact]
    public void HasFreeSlots_SomeFull_ReturnsTrue()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower();
        state.Slots[2].Tower = new FakeTower();

        Assert.True(state.HasFreeSlots());
    }

    // ── Wave tracking ─────────────────────────────────────────────────────

    [Fact]
    public void StartNewWave_InitializesTowerStats()
    {
        var state = new RunState();
        var tower = new FakeTower { TowerId = "rapid_shooter" };
        state.Slots[0].Tower = tower;

        state.StartNewWave(1);

        Assert.Single(state.CurrentWave.TowerStats);
        Assert.Equal("rapid_shooter", state.CurrentWave.TowerStats[0].TowerId);
    }

    [Fact]
    public void CompleteWave_ArchivesWaveReport()
    {
        var state = new RunState();
        state.StartNewWave(1);
        state.CompleteWave();

        Assert.Single(state.CompletedWaves);
        Assert.Equal(1, state.CompletedWaves[0].WaveNumber);
    }

    // ── Damage / kill tracking ────────────────────────────────────────────

    [Fact]
    public void TrackTowerDamage_AccumulatesCorrectly()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower();
        state.StartNewWave(1);

        state.TrackTowerDamage(0, 50);
        state.TrackTowerDamage(0, 30);

        Assert.Equal(80, state.CurrentWave.TowerStats[0].Damage);
    }

    [Fact]
    public void WaveReport_TopDamageDealer_ReturnsTowerWithMostDamage()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower { TowerId = "rapid_shooter" };
        state.Slots[1].Tower = new FakeTower { TowerId = "heavy_cannon" };
        state.StartNewWave(1);

        state.TrackTowerDamage(0, 100);
        state.TrackTowerDamage(1, 500);

        Assert.Equal("heavy_cannon", state.CurrentWave.TopDamageDealer?.TowerId);
    }

    [Fact]
    public void TrackSpectacleTriggers_AccumulatesTierAndEffectCounts()
    {
        var state = new RunState();

        state.TrackSpectacleSurge("C_MOMENTUM_SPLIT");
        state.TrackSpectacleGlobal("G_SPECTACLE_CATHARSIS");

        Assert.Equal(1, state.SpectacleSurgeTriggers);
        Assert.Equal(1, state.SpectacleGlobalTriggers);
        Assert.Equal(1, state.SpectacleSurgeByEffect["C_MOMENTUM_SPLIT"]);
        Assert.Equal(1, state.SpectacleGlobalByEffect["G_SPECTACLE_CATHARSIS"]);
    }

    [Fact]
    public void TrackSpectacleSurge_TracksFirstAndRechargeTiming_PerTower()
    {
        var state = new RunState();

        state.TrackSpectacleSurge("C_MOMENTUM_SPLIT", "rapid_shooter", 5f);
        state.TrackSpectacleSurge("C_MOMENTUM_SPLIT", "rapid_shooter", 9f);
        state.TrackSpectacleSurge("C_MOMENTUM_SPLIT", "rapid_shooter", 12.5f);

        Assert.Equal(5f, state.SpectacleFirstSurgeTimeByTower["rapid_shooter"], 3);
        Assert.Equal(7.5f, state.SpectacleRechargeSecondsTotalByTower["rapid_shooter"], 3);
        Assert.Equal(2, state.SpectacleRechargeCountByTower["rapid_shooter"]);
    }

    [Fact]
    public void SurgeHintTelemetry_TracksAndResets()
    {
        var state = new RunState();

        state.SurgeHintTelemetry.TowersSurged = 3;
        state.SurgeHintTelemetry.GlobalsBecameReady = 2;
        state.SurgeHintTelemetry.GlobalsActivated = 1;
        state.SurgeHintTelemetry.GlobalReadyUnusedSeconds = 7.5f;
        state.SurgeHintTelemetry.LostWithGlobalReadyUnused = true;
        state.SurgeHintTelemetry.ComboTowersBuiltThisRun = 2;
        state.SurgeHintTelemetry.ComboTowerSurgesThisRun = 1;
        state.SurgeHintTelemetry.QuickGlobalActivationsWithin10s = 1;

        state.Reset();

        Assert.Equal(0, state.SurgeHintTelemetry.TowersSurged);
        Assert.Equal(0, state.SurgeHintTelemetry.GlobalsBecameReady);
        Assert.Equal(0, state.SurgeHintTelemetry.GlobalsActivated);
        Assert.Equal(0f, state.SurgeHintTelemetry.GlobalReadyUnusedSeconds);
        Assert.False(state.SurgeHintTelemetry.LostWithGlobalReadyUnused);
        Assert.Equal(0, state.SurgeHintTelemetry.ComboTowersBuiltThisRun);
        Assert.Equal(0, state.SurgeHintTelemetry.ComboTowerSurgesThisRun);
        Assert.Equal(0, state.SurgeHintTelemetry.QuickGlobalActivationsWithin10s);
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsAllState()
    {
        var state = new RunState();
        state.WaveIndex = 5;
        state.Lives = 3;
        state.TotalKills = 100;
        state.TrackSpectacleSurge("C_MOMENTUM_SPLIT");
        state.TrackSpectacleGlobal("G_SPECTACLE_CATHARSIS");
        state.Reset();

        Assert.Equal(0, state.WaveIndex);
        Assert.Equal(Balance.StartingLives, state.Lives);
        Assert.Equal(0, state.TotalKills);
        Assert.Equal(0, state.SpectacleSurgeTriggers);
        Assert.Equal(0, state.SpectacleGlobalTriggers);
        Assert.Empty(state.SpectacleSurgeByEffect);
        Assert.Empty(state.SpectacleGlobalByEffect);
    }
}
