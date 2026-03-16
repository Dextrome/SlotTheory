using System.Threading.Tasks;

namespace SlotTheory.Core.Leaderboards;

public interface ILeaderboardService
{
    string ProviderName { get; }
    bool IsAvailable { get; }

    Task InitializeAsync();
    void Tick();

    Task<GlobalSubmitResult> SubmitScoreAsync(LeaderboardBucket bucket, RunScorePayload payload, int score);
    Task<System.Collections.Generic.IReadOnlyList<LeaderboardEntryView>> GetTopEntriesAsync(LeaderboardBucket bucket, int maxEntries = 20);
    Task<LeaderboardEntryView?> GetEntryAtRankAsync(LeaderboardBucket bucket, int rank);
    Task<bool> ShowNativeUiAsync(LeaderboardBucket bucket);
}
