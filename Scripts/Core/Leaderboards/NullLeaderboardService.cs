using System.Threading.Tasks;

namespace SlotTheory.Core.Leaderboards;

public sealed class NullLeaderboardService : ILeaderboardService
{
    public string ProviderName => "None";
    public bool IsAvailable => false;

    public Task InitializeAsync() => Task.CompletedTask;

    public void Tick() { }

    public Task<GlobalSubmitResult> SubmitScoreAsync(LeaderboardBucket bucket, RunScorePayload payload, int score)
    {
        return Task.FromResult(GlobalSubmitResult.Skipped(ProviderName, "Global leaderboard service unavailable."));
    }

    public Task<System.Collections.Generic.IReadOnlyList<LeaderboardEntryView>> GetTopEntriesAsync(LeaderboardBucket bucket, int maxEntries = 20)
    {
        return Task.FromResult<System.Collections.Generic.IReadOnlyList<LeaderboardEntryView>>(
            System.Array.Empty<LeaderboardEntryView>());
    }

    public Task<LeaderboardEntryView?> GetEntryAtRankAsync(LeaderboardBucket bucket, int rank)
        => Task.FromResult<LeaderboardEntryView?>(null);

    public Task<bool> ShowNativeUiAsync(LeaderboardBucket bucket) => Task.FromResult(false);
}
