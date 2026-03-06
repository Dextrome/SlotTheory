using Godot;
using SlotTheory.Core.Leaderboards;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton for personal highscores stored locally in user://.
/// </summary>
public partial class HighScoreManager : Node
{
    public static HighScoreManager? Instance { get; private set; }

    private const string SavePath = "user://high_scores.cfg";
    private ConfigFile _cfg = new();
    private bool _loaded;

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public LocalSubmitResult SubmitLocal(RunScorePayload payload)
    {
        EnsureLoaded();

        var bucket = new LeaderboardBucket(payload.MapId, payload.Difficulty);
        var previousBest = GetPersonalBest(payload.MapId, payload.Difficulty);
        int score = ScoreCalculator.ComputeScore(payload);
        bool isNewBest = ScoreCalculator.IsBetterThanExisting(payload, previousBest);

        var entry = new PersonalBestEntry(
            score,
            payload.Won,
            payload.WaveReached,
            payload.LivesRemaining,
            payload.TotalDamageDealt,
            payload.TotalKills,
            payload.PlayTimeSeconds,
            payload.RunTimestampUnixSeconds,
            payload.GameVersion
        );

        if (isNewBest)
        {
            WriteBest(bucket.MapId, bucket.Difficulty, entry);
            Save();
        }

        return new LocalSubmitResult(
            bucket,
            score,
            isNewBest,
            previousBest,
            isNewBest ? entry : previousBest ?? entry
        );
    }

    public PersonalBestEntry? GetPersonalBest(string mapId, DifficultyMode difficulty)
    {
        EnsureLoaded();
        string section = LeaderboardKey.ToSectionKey(mapId, difficulty);
        if (!_cfg.HasSectionKey(section, "best_score"))
            return null;

        return new PersonalBestEntry(
            (int)_cfg.GetValue(section, "best_score", 0),
            (bool)_cfg.GetValue(section, "best_won", false),
            (int)_cfg.GetValue(section, "best_wave", 0),
            (int)_cfg.GetValue(section, "best_lives", 0),
            (int)_cfg.GetValue(section, "best_damage", 0),
            (int)_cfg.GetValue(section, "best_kills", 0),
            (float)_cfg.GetValue(section, "best_time_seconds", 0f),
            (long)_cfg.GetValue(section, "best_run_unix", 0L),
            (string)_cfg.GetValue(section, "best_game_version", "")
        );
    }

    public System.Collections.Generic.IReadOnlyList<LeaderboardEntryView> GetLocalEntries(
        string mapId,
        DifficultyMode difficulty,
        int maxEntries = 20)
    {
        var best = GetPersonalBest(mapId, difficulty);
        if (best == null || maxEntries <= 0)
            return System.Array.Empty<LeaderboardEntryView>();

        return
        [
            new LeaderboardEntryView(
                Rank: 1,
                Name: "You",
                Score: best.Score,
                WaveReached: best.WaveReached,
                LivesRemaining: best.LivesRemaining,
                TotalKills: best.TotalKills,
                TotalDamageDealt: best.TotalDamageDealt,
                TimeSeconds: (int)System.Math.Floor(best.PlayTimeSeconds),
                IsLocal: true
            )
        ];
    }

    private void WriteBest(string mapId, DifficultyMode difficulty, PersonalBestEntry entry)
    {
        string section = LeaderboardKey.ToSectionKey(mapId, difficulty);
        _cfg.SetValue(section, "best_score", entry.Score);
        _cfg.SetValue(section, "best_won", entry.Won);
        _cfg.SetValue(section, "best_wave", entry.WaveReached);
        _cfg.SetValue(section, "best_lives", entry.LivesRemaining);
        _cfg.SetValue(section, "best_damage", entry.TotalDamageDealt);
        _cfg.SetValue(section, "best_kills", entry.TotalKills);
        _cfg.SetValue(section, "best_time_seconds", entry.PlayTimeSeconds);
        _cfg.SetValue(section, "best_run_unix", entry.RunTimestampUnixSeconds);
        _cfg.SetValue(section, "best_game_version", entry.GameVersion);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
    }

    private void Load()
    {
        _cfg = new ConfigFile();
        var err = _cfg.Load(SavePath);
        if (err != Error.Ok && err != Error.FileNotFound)
        {
            GD.PrintErr($"[HighScore] Failed to load {SavePath}: {err}");
        }
        _loaded = true;
    }

    private void Save()
    {
        var err = _cfg.Save(SavePath);
        if (err != Error.Ok)
            GD.PrintErr($"[HighScore] Failed to save {SavePath}: {err}");
    }
}
