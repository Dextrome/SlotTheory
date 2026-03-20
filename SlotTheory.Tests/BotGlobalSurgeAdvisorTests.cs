using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class BotGlobalSurgeAdvisorTests
{
    [Fact]
    public void ShouldActivate_ReturnsFalse_WhenNotReady()
    {
        var snapshot = new BotGlobalSurgeSnapshot(
            IsGlobalSurgeReady: false,
            HasPendingGlobalSurge: true,
            Lives: 10,
            EnemiesAlive: 9,
            EnemiesSpawnedThisWave: 20,
            TotalEnemiesThisWave: 30,
            ReadyAgeSeconds: 4f);

        Assert.False(BotGlobalSurgeAdvisor.ShouldActivate(snapshot));
    }

    [Fact]
    public void ShouldActivate_ReturnsTrue_ForMidWaveCrowd()
    {
        var snapshot = new BotGlobalSurgeSnapshot(
            IsGlobalSurgeReady: true,
            HasPendingGlobalSurge: true,
            Lives: 9,
            EnemiesAlive: 8,
            EnemiesSpawnedThisWave: 16,
            TotalEnemiesThisWave: 30,
            ReadyAgeSeconds: 1.2f);

        Assert.True(BotGlobalSurgeAdvisor.ShouldActivate(snapshot));
    }

    [Fact]
    public void ShouldActivate_ReturnsTrue_ForEmergencyEvenBeforeHold()
    {
        var snapshot = new BotGlobalSurgeSnapshot(
            IsGlobalSurgeReady: true,
            HasPendingGlobalSurge: true,
            Lives: 2,
            EnemiesAlive: 4,
            EnemiesSpawnedThisWave: 5,
            TotalEnemiesThisWave: 30,
            ReadyAgeSeconds: 0.1f);

        Assert.True(BotGlobalSurgeAdvisor.ShouldActivate(snapshot));
    }

    [Fact]
    public void ShouldActivate_ReturnsFalse_WhenReadyTooBrieflyAndNoEmergency()
    {
        var snapshot = new BotGlobalSurgeSnapshot(
            IsGlobalSurgeReady: true,
            HasPendingGlobalSurge: true,
            Lives: 10,
            EnemiesAlive: 9,
            EnemiesSpawnedThisWave: 16,
            TotalEnemiesThisWave: 30,
            ReadyAgeSeconds: 0.2f);

        Assert.False(BotGlobalSurgeAdvisor.ShouldActivate(snapshot));
    }

    [Fact]
    public void ShouldActivate_ReturnsTrue_ForStaleReadyFallback()
    {
        var snapshot = new BotGlobalSurgeSnapshot(
            IsGlobalSurgeReady: true,
            HasPendingGlobalSurge: true,
            Lives: 8,
            EnemiesAlive: 2,
            EnemiesSpawnedThisWave: 29,
            TotalEnemiesThisWave: 30,
            ReadyAgeSeconds: 14f);

        Assert.True(BotGlobalSurgeAdvisor.ShouldActivate(snapshot));
    }
}
