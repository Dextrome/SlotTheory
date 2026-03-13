using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Steamworks;

namespace SlotTheory.Core.Leaderboards;

public sealed class SteamLeaderboardService : ILeaderboardService
{
    private static readonly SteamLeaderboard_t InvalidLeaderboard = new(0);
    private const double InitRetryIntervalSeconds = 15.0;

    private readonly Dictionary<string, SteamLeaderboard_t> _cache = new();
    private CallResult<LeaderboardFindResult_t>? _findResult;
    private CallResult<LeaderboardScoreUploaded_t>? _uploadResult;
    private CallResult<LeaderboardScoresDownloaded_t>? _downloadResult;
    private TaskCompletionSource<SteamLeaderboard_t>? _pendingFind;
    private TaskCompletionSource<GlobalSubmitResult>? _pendingUpload;
    private TaskCompletionSource<IReadOnlyList<LeaderboardEntryView>>? _pendingDownload;
    private string _pendingFindName = "";
    private string _pendingUploadBoard = "";

    private bool _initAttempted;
    private bool _initialized;
    private double _nextInitRetryAtUnixSeconds;

    public string ProviderName => "Steam";
    public bool IsAvailable => _initialized;

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;

        if (OS.GetName() != "Windows")
        {
            return Task.CompletedTask;
        }

        double now = Time.GetUnixTimeFromSystem();
        if (_initAttempted && now < _nextInitRetryAtUnixSeconds)
            return Task.CompletedTask;

        _initAttempted = true;
        _nextInitRetryAtUnixSeconds = now + InitRetryIntervalSeconds;

        try
        {
            _initialized = SteamAPI.Init();
            if (!_initialized)
            {
                GD.Print("[Leaderboards] SteamAPI.Init returned false. Ensure steam_api64.dll is next to the game executable and steam_appid.txt is present for local runs.");
                return Task.CompletedTask;
            }

            _findResult = CallResult<LeaderboardFindResult_t>.Create(OnFindLeaderboardResult);
            _uploadResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnUploadScoreResult);
            _downloadResult = CallResult<LeaderboardScoresDownloaded_t>.Create(OnScoresDownloadedResult);

            // Required before GetAchievement / SetAchievement calls will return correct data.
            // The response fires asynchronously via UserStatsReceived_t; stats are ready
            // well before any run ends so no need to await the callback.
            SteamUserStats.RequestCurrentStats();

            GD.Print("[Leaderboards] Steam service initialized.");
        }
        catch (Exception ex)
        {
            _initialized = false;
            GD.PrintErr($"[Leaderboards] Steam init failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public void Tick()
    {
        if (_initialized)
            SteamAPI.RunCallbacks();
    }

    public async Task<GlobalSubmitResult> SubmitScoreAsync(LeaderboardBucket bucket, RunScorePayload payload, int score)
    {
        if (!_initialized)
            return GlobalSubmitResult.Skipped(ProviderName, "Steam service unavailable.");
        if (_pendingUpload != null)
            return GlobalSubmitResult.Failed(ProviderName, "Steam upload busy; retrying shortly.");

        string boardName = LeaderboardKey.ToSteamLeaderboardName(bucket.MapId, bucket.Difficulty);
        var board = await FindLeaderboardAsync(boardName);
        if (!IsValid(board))
            return GlobalSubmitResult.Failed(ProviderName, $"Leaderboard not found: {boardName}");

        var details = BuildDetails(payload);
        var uploadCall = SteamUserStats.UploadLeaderboardScore(
            board,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score,
            details,
            details.Length
        );

        _pendingUploadBoard = boardName;
        _pendingUpload = new TaskCompletionSource<GlobalSubmitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uploadResult?.Set(uploadCall);
        return await _pendingUpload.Task;
    }

    public async Task<IReadOnlyList<LeaderboardEntryView>> GetTopEntriesAsync(LeaderboardBucket bucket, int maxEntries = 20)
    {
        if (!_initialized)
            return Array.Empty<LeaderboardEntryView>();

        string boardName = LeaderboardKey.ToSteamLeaderboardName(bucket.MapId, bucket.Difficulty);
        var board = await FindLeaderboardAsync(boardName);
        if (!IsValid(board))
            return Array.Empty<LeaderboardEntryView>();

        if (_pendingDownload != null)
            return Array.Empty<LeaderboardEntryView>();

        int rangeEnd = Math.Max(1, maxEntries);
        _pendingDownload = new TaskCompletionSource<IReadOnlyList<LeaderboardEntryView>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dlCall = SteamUserStats.DownloadLeaderboardEntries(
            board,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
            1,
            rangeEnd
        );
        _downloadResult?.Set(dlCall);
        return await _pendingDownload.Task;
    }

    public Task<bool> ShowNativeUiAsync(LeaderboardBucket bucket)
    {
        if (!_initialized) return Task.FromResult(false);
        SteamFriends.ActivateGameOverlay("leaderboards");
        return Task.FromResult(true);
    }

    private async Task<SteamLeaderboard_t> FindLeaderboardAsync(string boardName)
    {
        if (_cache.TryGetValue(boardName, out var cached))
            return cached;

        if (_pendingFind != null)
        {
            // Same board already resolving — queue behind the in-flight find.
            if (_pendingFindName == boardName)
                return await _pendingFind.Task;
            // Different board in-flight — Steam doesn't allow overlapping FindLeaderboard
            // calls; bail and let the caller retry on the next refresh.
            return InvalidLeaderboard;
        }

        _pendingFindName = boardName;
        _pendingFind = new TaskCompletionSource<SteamLeaderboard_t>(TaskCreationOptions.RunContinuationsAsynchronously);
        var findCall = SteamUserStats.FindLeaderboard(boardName);
        _findResult?.Set(findCall);
        var handle = await _pendingFind.Task;

        if (IsValid(handle))
            _cache[boardName] = handle;

        return handle;
    }

    private void OnFindLeaderboardResult(LeaderboardFindResult_t result, bool ioFailure)
    {
        var pending = _pendingFind;
        _pendingFind = null;

        if (pending == null) return;

        if (ioFailure || result.m_bLeaderboardFound == 0)
        {
            GD.PrintErr($"[Leaderboards] Steam find failed for '{_pendingFindName}'.");
            pending.TrySetResult(InvalidLeaderboard);
            return;
        }

        pending.TrySetResult(result.m_hSteamLeaderboard);
    }

    private void OnUploadScoreResult(LeaderboardScoreUploaded_t result, bool ioFailure)
    {
        var pending = _pendingUpload;
        _pendingUpload = null;

        if (pending == null) return;

        if (ioFailure || result.m_bSuccess == 0)
        {
            pending.TrySetResult(GlobalSubmitResult.Failed(ProviderName, $"Upload failed: {_pendingUploadBoard}"));
            return;
        }

        int? rank = result.m_nGlobalRankNew > 0 ? result.m_nGlobalRankNew : null;
        string message = result.m_bScoreChanged != 0
            ? $"Score submitted to {_pendingUploadBoard}"
            : $"Score kept (not higher) on {_pendingUploadBoard}";
        pending.TrySetResult(GlobalSubmitResult.Submitted(ProviderName, message, rank));
    }

    private void OnScoresDownloadedResult(LeaderboardScoresDownloaded_t result, bool ioFailure)
    {
        var pending = _pendingDownload;
        _pendingDownload = null;

        if (pending == null) return;

        if (ioFailure || result.m_cEntryCount <= 0)
        {
            pending.TrySetResult(Array.Empty<LeaderboardEntryView>());
            return;
        }

        var entries = new List<LeaderboardEntryView>(result.m_cEntryCount);
        for (int i = 0; i < result.m_cEntryCount; i++)
        {
            var details = new int[16];
            if (!SteamUserStats.GetDownloadedLeaderboardEntry(
                    result.m_hSteamLeaderboardEntries,
                    i,
                    out LeaderboardEntry_t entry,
                    details,
                    details.Length))
            {
                continue;
            }

            string name = SteamFriends.GetFriendPersonaName(entry.m_steamIDUser);
            int wave = details.Length > 1 ? details[1] : 0;
            int lives = details.Length > 2 ? details[2] : 0;
            int kills = details.Length > 3 ? details[3] : 0;
            int damage = details.Length > 4 ? details[4] : 0;
            int timeSeconds = details.Length > 5 ? details[5] : 0;
            var build = BuildSnapshotCodec.Unpack(details.Skip(6).Take(Balance.SlotCount).ToArray());

            entries.Add(new LeaderboardEntryView(
                entry.m_nGlobalRank,
                string.IsNullOrEmpty(name) ? $"Player {entry.m_steamIDUser.m_SteamID}" : name,
                entry.m_nScore,
                wave,
                lives,
                kills,
                damage,
                timeSeconds,
                build
            ));
        }

        pending.TrySetResult(entries);
    }

    private static bool IsValid(SteamLeaderboard_t handle) => handle.m_SteamLeaderboard != 0;

    private static int[] BuildDetails(RunScorePayload payload)
    {
        var packedBuild = BuildSnapshotCodec.Pack(payload.Build);
        return
        [
            payload.Won ? 1 : 0,
            payload.WaveReached,
            payload.LivesRemaining,
            payload.TotalKills,
            payload.TotalDamageDealt,
            (int)MathF.Floor(payload.PlayTimeSeconds),
            packedBuild[0],
            packedBuild[1],
            packedBuild[2],
            packedBuild[3],
            packedBuild[4],
            packedBuild[5],
        ];
    }
}
