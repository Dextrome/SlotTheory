using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton that routes global leaderboard submissions to the active platform provider.
/// </summary>
public partial class LeaderboardManager : Node
{
    public static LeaderboardManager? Instance { get; private set; }

    private const string RetryQueuePath = "user://leaderboard_retry_queue.json";
    private const double RetryFlushIntervalSeconds = 20.0;
    private const double InitRetryIntervalSeconds = 15.0;

    private ILeaderboardService _service = new NullLeaderboardService();
    private readonly List<PendingSubmission> _retryQueue = new();
    private System.Threading.Tasks.Task? _initTask;   // all callers await the same task
    private double _nextInitRetryAtUnixSeconds;
    private bool _isFlushingRetryQueue;
    private double _retryFlushCountdown = RetryFlushIntervalSeconds;

    public string ProviderName => _service.ProviderName;
    public bool IsAvailable => _service.IsAvailable;

    public override void _Ready()
    {
        Instance = this;
        _service = CreateService();
        LoadRetryQueue();
        _ = EnsureInitializedAsync();
    }

    public override void _Process(double delta)
    {
        _service.Tick();

        if (_retryQueue.Count == 0) return;
        _retryFlushCountdown -= delta;
        if (_retryFlushCountdown > 0.0) return;

        _retryFlushCountdown = RetryFlushIntervalSeconds;
        _ = FlushRetryQueueAsync();
    }

    public async System.Threading.Tasks.Task<GlobalSubmitResult> SubmitAsync(RunScorePayload payload)
    {
        var bucket = new LeaderboardBucket(payload.MapId, payload.Difficulty);
        if (!bucket.IsGlobalEligible)
            return GlobalSubmitResult.Skipped(_service.ProviderName, "Map excluded from global leaderboards.");

        if (!ScoreCalculator.IsPayloadSane(payload))
            return GlobalSubmitResult.Failed(_service.ProviderName, "Score payload failed sanity checks.");

        int score = ScoreCalculator.ComputeScore(payload);
        await EnsureInitializedAsync();
        if (!_service.IsAvailable)
        {
            EnqueueRetry(payload, score, "Service unavailable.");
            return GlobalSubmitResult.Queued(_service.ProviderName, "Service unavailable. Submission queued for retry.");
        }

        var result = await _service.SubmitScoreAsync(bucket, payload, score);
        if (result.State == GlobalSubmitState.Failed)
        {
            EnqueueRetry(payload, score, result.Message);
            return GlobalSubmitResult.Queued(result.Provider, "Submission failed. Queued for retry.");
        }
        return result;
    }

    public async System.Threading.Tasks.Task<bool> ShowNativeUiAsync(string mapId, DifficultyMode difficulty)
    {
        var bucket = new LeaderboardBucket(mapId, difficulty);
        if (!bucket.IsGlobalEligible) return false;

        await EnsureInitializedAsync();
        if (!_service.IsAvailable) return false;
        return await _service.ShowNativeUiAsync(bucket);
    }

    public async System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<LeaderboardEntryView>> GetTopEntriesAsync(
        string mapId,
        DifficultyMode difficulty,
        int maxEntries = 20)
    {
        var bucket = new LeaderboardBucket(mapId, difficulty);
        if (!bucket.IsGlobalEligible)
            return System.Array.Empty<LeaderboardEntryView>();

        await EnsureInitializedAsync();
        if (!_service.IsAvailable)
            return System.Array.Empty<LeaderboardEntryView>();

        return await _service.GetTopEntriesAsync(bucket, maxEntries);
    }

    public async System.Threading.Tasks.Task<bool> ShowGlobalHubAsync()
    {
        await EnsureInitializedAsync();
        if (!_service.IsAvailable) return false;

        // Steam overlay route opens the leaderboards hub; bucket is only used by non-hub providers.
        var defaultBucket = new LeaderboardBucket("arena_classic", DifficultyMode.Easy);
        return await _service.ShowNativeUiAsync(defaultBucket);
    }

    private System.Threading.Tasks.Task EnsureInitializedAsync()
    {
        double now = Time.GetUnixTimeFromSystem();
        bool hasInFlightInitTask = _initTask != null && !_initTask.IsCompleted;
        bool hasCompletedInitTask = _initTask != null && _initTask.IsCompleted;
        if (!LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
                _service.IsAvailable,
                hasInFlightInitTask,
                hasCompletedInitTask,
                now,
                _nextInitRetryAtUnixSeconds))
        {
            return _initTask ?? System.Threading.Tasks.Task.CompletedTask;
        }

        _nextInitRetryAtUnixSeconds = now + InitRetryIntervalSeconds;
        _initTask = InitializeServiceSafeAsync();
        return _initTask;
    }

    private async System.Threading.Tasks.Task InitializeServiceSafeAsync()
    {
        try
        {
            await _service.InitializeAsync();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Leaderboards] Service init failed: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task FlushRetryQueueAsync()
    {
        if (_isFlushingRetryQueue || _retryQueue.Count == 0) return;

        await EnsureInitializedAsync();
        if (!_service.IsAvailable) return;

        _isFlushingRetryQueue = true;
        try
        {
            // Process each currently queued entry at most once per flush cycle so
            // a single permanently failing run cannot block everything behind it.
            int pendingToScan = LeaderboardRetryQueuePolicy.BeginFlushBudget(_retryQueue.Count);
            while (LeaderboardRetryQueuePolicy.HasFlushWork(_retryQueue.Count, pendingToScan))
            {
                pendingToScan--;
                var next = _retryQueue[0];
                var bucket = new LeaderboardBucket(next.Payload.MapId, next.Payload.Difficulty);
                if (!bucket.IsGlobalEligible)
                {
                    _retryQueue.RemoveAt(0);
                    SaveRetryQueue();
                    continue;
                }

                var result = await _service.SubmitScoreAsync(bucket, next.Payload, next.Score);
                LeaderboardRetryQueuePolicy.ApplySubmissionResult(
                    _retryQueue,
                    result,
                    (entry, message) => entry.Reason = message);
                SaveRetryQueue();
            }
        }
        finally
        {
            _isFlushingRetryQueue = false;
        }
    }

    private void EnqueueRetry(RunScorePayload payload, int score, string reason)
    {
        string runId = BuildRunId(payload);
        if (_retryQueue.Exists(x => x.RunId == runId))
            return;

        _retryQueue.Add(new PendingSubmission
        {
            RunId = runId,
            Score = score,
            Payload = payload,
            Reason = reason,
        });
        SaveRetryQueue();
    }

    private void LoadRetryQueue()
    {
        try
        {
            string fullPath = ProjectSettings.GlobalizePath(RetryQueuePath);
            if (!File.Exists(fullPath))
                return;

            string json = File.ReadAllText(fullPath);
            var loaded = JsonSerializer.Deserialize<List<PendingSubmission>>(json);
            _retryQueue.Clear();
            if (loaded != null)
                _retryQueue.AddRange(loaded);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Leaderboards] Failed to load retry queue: {ex.Message}");
            _retryQueue.Clear();
        }
    }

    private void SaveRetryQueue()
    {
        try
        {
            string fullPath = ProjectSettings.GlobalizePath(RetryQueuePath);
            string? dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(_retryQueue, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            File.WriteAllText(fullPath, json);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Leaderboards] Failed to save retry queue: {ex.Message}");
        }
    }

    private static string BuildRunId(RunScorePayload payload)
    {
        int seconds = (int)System.Math.Floor(payload.PlayTimeSeconds);
        return string.Join("|",
            payload.MapId,
            payload.Difficulty,
            payload.Won ? "1" : "0",
            payload.WaveReached,
            payload.LivesRemaining,
            payload.TotalDamageDealt,
            payload.TotalKills,
            seconds,
            payload.RunTimestampUnixSeconds);
    }

    private sealed class PendingSubmission
    {
        public string RunId { get; set; } = "";
        public int Score { get; set; }
        public RunScorePayload Payload { get; set; } = new(
            LeaderboardKey.RandomMapId,
            DifficultyMode.Easy,
            false,
            0,
            0,
            0,
            0,
            0f,
            0L,
            "dev",
            BuildSnapshotCodec.Empty());
        public string Reason { get; set; } = "";
    }

    private static ILeaderboardService CreateService()
    {
        if (OS.GetName() == "Android" || OS.GetName() == "iOS")
            return new SupabaseLeaderboardService();

        // The Steam export preset sets custom_features="steam"; standalone/itch builds don't.
        // In the editor / dev builds, also try Steam (steam_appid.txt + running Steam client).
        if (!OS.HasFeature("steam") && !OS.HasFeature("editor"))
            return new SupabaseLeaderboardService();

        return TryCreateSteamService();
    }

    // NoInlining ensures the JIT only compiles this method if it's actually called.
    // SteamLeaderboardService references Steamworks.NET types that don't exist on Android —
    // keeping this in a separate non-inlined method prevents TypeLoadException at startup.
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ILeaderboardService TryCreateSteamService()
    {
        try { return new SteamLeaderboardService(); }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Leaderboards] Steam service failed to load: {ex.Message}");
            return new SupabaseLeaderboardService();
        }
    }

}
