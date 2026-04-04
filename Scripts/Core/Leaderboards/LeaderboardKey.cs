using SlotTheory.Core;

namespace SlotTheory.Core.Leaderboards;

public static class LeaderboardKey
{
    public const string RandomMapId  = "random_map";
    public const string TutorialMapId = "tutorial";

    public static string ToBucketId(string mapId, DifficultyMode difficulty)
        => $"{mapId}_{DifficultyToken(difficulty)}";

    public static string ToSectionKey(string mapId, DifficultyMode difficulty)
        => $"bucket.{mapId}.{DifficultyToken(difficulty)}";

    public static string ToSteamLeaderboardName(string mapId, DifficultyMode difficulty)
        => $"global_{mapId}_{DifficultyToken(difficulty)}";

    public static bool IsGlobalEligibleMap(string mapId)
        => mapId != TutorialMapId;

    public static string DifficultyToken(DifficultyMode difficulty)
        => difficulty switch
        {
            DifficultyMode.Easy => "easy",
            DifficultyMode.Normal => "normal",
            DifficultyMode.Hard => "hard",
            _ => "easy",
        };
}
