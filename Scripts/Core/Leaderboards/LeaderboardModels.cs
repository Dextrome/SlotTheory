using SlotTheory.Core;

namespace SlotTheory.Core.Leaderboards;

public sealed record LeaderboardBucket(string MapId, DifficultyMode Difficulty)
{
    public string Id => LeaderboardKey.ToBucketId(MapId, Difficulty);
    public bool IsGlobalEligible => LeaderboardKey.IsGlobalEligibleMap(MapId);
}

public sealed record RunScorePayload(
    string MapId,
    DifficultyMode Difficulty,
    bool Won,
    int WaveReached,
    int LivesRemaining,
    int TotalDamageDealt,
    int TotalKills,
    float PlayTimeSeconds,
    long RunTimestampUnixSeconds,
    string GameVersion,
    RunBuildSnapshot Build
);

public sealed record RunSlotBuild(
    string TowerId,
    string[] ModifierIds
);

public sealed record RunBuildSnapshot(
    RunSlotBuild[] Slots
);

public sealed record PersonalBestEntry(
    int Score,
    bool Won,
    int WaveReached,
    int LivesRemaining,
    int TotalDamageDealt,
    int TotalKills,
    float PlayTimeSeconds,
    long RunTimestampUnixSeconds,
    string GameVersion,
    RunBuildSnapshot Build
);

public sealed record LocalSubmitResult(
    LeaderboardBucket Bucket,
    int Score,
    bool IsNewPersonalBest,
    PersonalBestEntry? PreviousBest,
    PersonalBestEntry CurrentBest
);

public enum GlobalSubmitState
{
    Submitted,
    Queued,
    Failed,
    Skipped,
}

public sealed record GlobalSubmitResult(
    GlobalSubmitState State,
    string Provider,
    string Message,
    int? Rank
)
{
    public static GlobalSubmitResult Submitted(string provider, string message, int? rank = null)
        => new(GlobalSubmitState.Submitted, provider, message, rank);

    public static GlobalSubmitResult Queued(string provider, string message)
        => new(GlobalSubmitState.Queued, provider, message, null);

    public static GlobalSubmitResult Failed(string provider, string message)
        => new(GlobalSubmitState.Failed, provider, message, null);

    public static GlobalSubmitResult Skipped(string provider, string message)
        => new(GlobalSubmitState.Skipped, provider, message, null);
}

public sealed record LeaderboardEntryView(
    int Rank,
    string Name,
    int Score,
    int WaveReached,
    int LivesRemaining,
    int TotalKills,
    int TotalDamageDealt,
    int TimeSeconds,
    RunBuildSnapshot Build,
    bool IsLocal = false
);
