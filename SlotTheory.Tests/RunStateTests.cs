using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>Unit tests for RunState — pure C#, no Godot dependencies.</summary>
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

    // ── Reset ─────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsAllState()
    {
        var state = new RunState();
        state.WaveIndex = 5;
        state.Lives = 3;
        state.TotalKills = 100;
        state.Reset();

        Assert.Equal(0, state.WaveIndex);
        Assert.Equal(Balance.StartingLives, state.Lives);
        Assert.Equal(0, state.TotalKills);
    }
}
