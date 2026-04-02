using System;

namespace SlotTheory.Tools;

public readonly record struct BotGlobalSurgeSnapshot(
    bool IsGlobalSurgeReady,
    bool HasPendingGlobalSurge,
    int Lives,
    int EnemiesAlive,
    int EnemiesSpawnedThisWave,
    int TotalEnemiesThisWave,
    float ReadyAgeSeconds,
    int MinCrowdOverride = -1);  // -1 = use default ratio calc; >0 = hard minimum enemies required

/// <summary>
/// Bot policy for manual global-surge activation.
/// Goal: prefer activation in mid-wave when enemy density is high.
/// </summary>
public static class BotGlobalSurgeAdvisor
{
    public const float MidWaveMinSpawnProgress = 0.38f;
    public const float MidWaveMaxSpawnProgress = 0.92f;
    public const float MinReadyAgeSeconds = 0.60f;
    public const float FallbackReadyAgeSeconds = 12.0f;
    public const float CrowdRatioThreshold = 0.18f;
    public const int CrowdFloor = 4;

    public static bool ShouldActivate(in BotGlobalSurgeSnapshot snapshot)
    {
        if (!snapshot.IsGlobalSurgeReady || !snapshot.HasPendingGlobalSurge)
            return false;

        int totalEnemies = Math.Max(1, snapshot.TotalEnemiesThisWave);
        float spawnProgress = Math.Clamp((float)snapshot.EnemiesSpawnedThisWave / totalEnemies, 0f, 1f);
        float readyAge = Math.Max(0f, snapshot.ReadyAgeSeconds);

        int crowdThreshold = snapshot.MinCrowdOverride > 0
            ? snapshot.MinCrowdOverride
            : Math.Max(CrowdFloor, (int)MathF.Ceiling(totalEnemies * CrowdRatioThreshold));
        bool crowded = snapshot.EnemiesAlive >= crowdThreshold;
        bool midWave = spawnProgress >= MidWaveMinSpawnProgress && spawnProgress <= MidWaveMaxSpawnProgress;
        bool emergency = snapshot.Lives <= 3 && snapshot.EnemiesAlive >= Math.Max(2, crowdThreshold / 2);
        bool staleReadyFallback = readyAge >= FallbackReadyAgeSeconds && snapshot.EnemiesAlive >= 2;

        if (readyAge < MinReadyAgeSeconds && !emergency)
            return false;

        return (midWave && crowded) || emergency || staleReadyFallback;
    }
}
