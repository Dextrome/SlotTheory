using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace SlotTheory.Core.Leaderboards;

/// <summary>
/// Global leaderboard provider backed by Supabase REST API.
/// Used for itch.io / standalone builds (non-Steam platforms).
/// Fill in ProjectUrl and AnonKey from your Supabase project settings.
/// </summary>
public sealed class SupabaseLeaderboardService : ILeaderboardService
{
    // ── Configuration — fill these in from Supabase dashboard ─────────────
    private const string ProjectUrl = "https://YOUR_PROJECT_ID.supabase.co";
    private const string AnonKey    = "YOUR_ANON_KEY_HERE";
    // ──────────────────────────────────────────────────────────────────────

    private static readonly System.Net.Http.HttpClient Http = new() { Timeout = System.TimeSpan.FromSeconds(10) };

    public string ProviderName => "Supabase";
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            using var req = MakeRequest(System.Net.Http.HttpMethod.Get, $"{ProjectUrl}/rest/v1/scores?limit=0&select=id");
            using var resp = await Http.SendAsync(req);
            IsAvailable = resp.IsSuccessStatusCode;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Supabase] Init failed: {ex.Message}");
            IsAvailable = false;
        }
    }

    public void Tick() { }

    public async Task<GlobalSubmitResult> SubmitScoreAsync(LeaderboardBucket bucket, RunScorePayload payload, int score)
    {
        var sm = SettingsManager.Instance;
        if (sm == null || string.IsNullOrEmpty(sm.PlayerId))
            return GlobalSubmitResult.Failed(ProviderName, "Player identity not initialized.");

        string buildCode = string.Join(",", BuildSnapshotCodec.Pack(payload.Build));

        var body = new
        {
            p_player_id        = sm.PlayerId,
            p_player_name      = string.IsNullOrEmpty(sm.PlayerName) ? "Anonymous" : sm.PlayerName,
            p_map_id           = payload.MapId,
            p_difficulty       = payload.Difficulty.ToString().ToLowerInvariant(),
            p_score            = score,
            p_won              = payload.Won,
            p_wave_reached     = payload.WaveReached,
            p_lives_remaining  = payload.LivesRemaining,
            p_play_time_seconds = payload.PlayTimeSeconds,
            p_game_version     = payload.GameVersion,
            p_build_code       = buildCode,
        };

        try
        {
            string json = JsonSerializer.Serialize(body);
            using var req = MakeRequest(System.Net.Http.HttpMethod.Post, $"{ProjectUrl}/rest/v1/rpc/submit_score");
            req.Content = new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync();
                GD.PrintErr($"[Supabase] Submit failed {(int)resp.StatusCode}: {err}");
                return GlobalSubmitResult.Failed(ProviderName, $"HTTP {(int)resp.StatusCode}");
            }

            string resultJson = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resultJson);
            int? rank = doc.RootElement.TryGetProperty("rank", out var rankEl) ? rankEl.GetInt32() : null;
            return GlobalSubmitResult.Submitted(ProviderName, "Score submitted.", rank);
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Supabase] Submit exception: {ex.Message}");
            return GlobalSubmitResult.Failed(ProviderName, ex.Message);
        }
    }

    public async Task<IReadOnlyList<LeaderboardEntryView>> GetTopEntriesAsync(LeaderboardBucket bucket, int maxEntries = 20)
    {
        string difficulty = bucket.Difficulty.ToString().ToLowerInvariant();
        string url = $"{ProjectUrl}/rest/v1/scores"
                   + $"?map_id=eq.{System.Uri.EscapeDataString(bucket.MapId)}"
                   + $"&difficulty=eq.{System.Uri.EscapeDataString(difficulty)}"
                   + $"&order=score.desc"
                   + $"&limit={maxEntries}"
                   + $"&select=player_name,score,wave_reached,lives_remaining,play_time_seconds,build_code";

        try
        {
            using var req = MakeRequest(System.Net.Http.HttpMethod.Get, url);
            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return System.Array.Empty<LeaderboardEntryView>();

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var entries = new List<LeaderboardEntryView>();
            int rank = 1;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                string name   = row.TryGetProperty("player_name", out var n) ? n.GetString() ?? "" : "";
                int scoreVal  = row.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
                int waves     = row.TryGetProperty("wave_reached", out var w) ? w.GetInt32() : 0;
                int lives     = row.TryGetProperty("lives_remaining", out var l) ? l.GetInt32() : 0;
                float time    = row.TryGetProperty("play_time_seconds", out var t) ? (float)t.GetDouble() : 0f;
                string code   = row.TryGetProperty("build_code", out var b) ? b.GetString() ?? "" : "";

                var build = TryDecodeBuild(code);
                entries.Add(new LeaderboardEntryView(rank++, name, scoreVal, waves, lives, 0, 0, (int)time, build));
            }

            return entries;
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Supabase] GetTopEntries exception: {ex.Message}");
            return System.Array.Empty<LeaderboardEntryView>();
        }
    }

    public Task<bool> ShowNativeUiAsync(LeaderboardBucket bucket) => Task.FromResult(false);

    // ── Helpers ───────────────────────────────────────────────────────────

    private static System.Net.Http.HttpRequestMessage MakeRequest(System.Net.Http.HttpMethod method, string url)
    {
        var req = new System.Net.Http.HttpRequestMessage(method, url);
        req.Headers.Add("apikey", AnonKey);
        req.Headers.Add("Authorization", $"Bearer {AnonKey}");
        return req;
    }

    private static RunBuildSnapshot TryDecodeBuild(string code)
    {
        try
        {
            if (string.IsNullOrEmpty(code)) return BuildSnapshotCodec.Empty();
            int[] packed = code.Split(',').Select(int.Parse).ToArray();
            return BuildSnapshotCodec.Unpack(packed);
        }
        catch
        {
            return BuildSnapshotCodec.Empty();
        }
    }
}
