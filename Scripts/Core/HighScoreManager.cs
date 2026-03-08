using System.Collections.Generic;
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
    private const int MaxLocalHistoryEntries = 20;
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
        var history = ReadLocalHistory(payload.MapId, payload.Difficulty, MaxLocalHistoryEntries);
        var previousBest = history.Count > 0 ? history[0] : ReadBestEntry(LeaderboardKey.ToSectionKey(payload.MapId, payload.Difficulty));
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
            payload.GameVersion,
            payload.Build
        );

        history.Add(entry);
        history.Sort(CompareEntries);
        if (history.Count > MaxLocalHistoryEntries)
            history.RemoveRange(MaxLocalHistoryEntries, history.Count - MaxLocalHistoryEntries);

        var currentBest = history.Count > 0 ? history[0] : entry;
        WriteLocalHistory(bucket.MapId, bucket.Difficulty, history);
        WriteBest(bucket.MapId, bucket.Difficulty, currentBest);
        Save();

        return new LocalSubmitResult(
            bucket,
            score,
            isNewBest,
            previousBest,
            currentBest
        );
    }

    public PersonalBestEntry? GetPersonalBest(string mapId, DifficultyMode difficulty)
    {
        EnsureLoaded();
        string section = LeaderboardKey.ToSectionKey(mapId, difficulty);
        return ReadBestEntry(section);
    }

    public System.Collections.Generic.IReadOnlyList<LeaderboardEntryView> GetLocalEntries(
        string mapId,
        DifficultyMode difficulty,
        int maxEntries = 20)
    {
        if (maxEntries <= 0)
            return System.Array.Empty<LeaderboardEntryView>();

        var history = ReadLocalHistory(mapId, difficulty, maxEntries);
        if (history.Count == 0)
            return System.Array.Empty<LeaderboardEntryView>();

        var rows = new List<LeaderboardEntryView>(history.Count);
        for (int i = 0; i < history.Count; i++)
        {
            var entry = history[i];
            rows.Add(new LeaderboardEntryView(
                Rank: i + 1,
                Name: "You",
                Score: entry.Score,
                WaveReached: entry.WaveReached,
                LivesRemaining: entry.LivesRemaining,
                TotalKills: entry.TotalKills,
                TotalDamageDealt: entry.TotalDamageDealt,
                TimeSeconds: (int)System.Math.Floor(entry.PlayTimeSeconds),
                Build: entry.Build,
                IsLocal: true
            ));
        }
        return rows;
    }

    private List<PersonalBestEntry> ReadLocalHistory(string mapId, DifficultyMode difficulty, int maxEntries)
    {
        string section = LeaderboardKey.ToSectionKey(mapId, difficulty);
        var entries = new List<PersonalBestEntry>();
        int runCount = (int)_cfg.GetValue(section, "run_count", 0);

        if (runCount > 0)
        {
            for (int i = 0; i < runCount; i++)
            {
                if (TryReadHistoryEntry(section, i, out var entry))
                    entries.Add(entry!);
            }

            entries.Sort(CompareEntries);
            if (entries.Count > maxEntries)
                entries.RemoveRange(maxEntries, entries.Count - maxEntries);
            if (entries.Count > 0)
                return entries;
        }

        var legacyBest = ReadBestEntry(section);
        if (legacyBest != null)
            entries.Add(legacyBest);
        return entries;
    }

    private bool TryReadHistoryEntry(string section, int index, out PersonalBestEntry? entry)
    {
        string prefix = $"run_{index}";
        if (!_cfg.HasSectionKey(section, $"{prefix}_score"))
        {
            entry = null;
            return false;
        }

        entry = new PersonalBestEntry(
            (int)_cfg.GetValue(section, $"{prefix}_score", 0),
            (bool)_cfg.GetValue(section, $"{prefix}_won", false),
            (int)_cfg.GetValue(section, $"{prefix}_wave", 0),
            (int)_cfg.GetValue(section, $"{prefix}_lives", 0),
            (int)_cfg.GetValue(section, $"{prefix}_damage", 0),
            (int)_cfg.GetValue(section, $"{prefix}_kills", 0),
            (float)_cfg.GetValue(section, $"{prefix}_time_seconds", 0f),
            (long)_cfg.GetValue(section, $"{prefix}_run_unix", 0L),
            (string)_cfg.GetValue(section, $"{prefix}_game_version", ""),
            ReadBuildSnapshot(section, $"{prefix}_slot_pack_")
        );
        return true;
    }

    private void WriteLocalHistory(string mapId, DifficultyMode difficulty, IReadOnlyList<PersonalBestEntry> entries)
    {
        string section = LeaderboardKey.ToSectionKey(mapId, difficulty);
        if (_cfg.HasSection(section))
        {
            foreach (string key in _cfg.GetSectionKeys(section))
            {
                if (key.StartsWith("run_"))
                    _cfg.EraseSectionKey(section, key);
            }
        }

        _cfg.SetValue(section, "run_count", entries.Count);
        for (int i = 0; i < entries.Count; i++)
            WriteHistoryEntry(section, i, entries[i]);
    }

    private void WriteHistoryEntry(string section, int index, PersonalBestEntry entry)
    {
        string prefix = $"run_{index}";
        _cfg.SetValue(section, $"{prefix}_score", entry.Score);
        _cfg.SetValue(section, $"{prefix}_won", entry.Won);
        _cfg.SetValue(section, $"{prefix}_wave", entry.WaveReached);
        _cfg.SetValue(section, $"{prefix}_lives", entry.LivesRemaining);
        _cfg.SetValue(section, $"{prefix}_damage", entry.TotalDamageDealt);
        _cfg.SetValue(section, $"{prefix}_kills", entry.TotalKills);
        _cfg.SetValue(section, $"{prefix}_time_seconds", entry.PlayTimeSeconds);
        _cfg.SetValue(section, $"{prefix}_run_unix", entry.RunTimestampUnixSeconds);
        _cfg.SetValue(section, $"{prefix}_game_version", entry.GameVersion);
        WriteBuildSnapshot(section, $"{prefix}_slot_pack_", entry.Build);
    }

    private PersonalBestEntry? ReadBestEntry(string section)
    {
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
            (string)_cfg.GetValue(section, "best_game_version", ""),
            ReadBuildSnapshot(section, "best_slot_pack_")
        );
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
        WriteBuildSnapshot(section, "best_slot_pack_", entry.Build);
    }

    private void WriteBuildSnapshot(string section, string keyPrefix, RunBuildSnapshot build)
    {
        var packed = BuildSnapshotCodec.Pack(build);
        for (int i = 0; i < packed.Length; i++)
            _cfg.SetValue(section, $"{keyPrefix}{i}", packed[i]);
    }

    private RunBuildSnapshot ReadBuildSnapshot(string section, string keyPrefix)
    {
        var packed = new int[Balance.SlotCount];
        for (int i = 0; i < packed.Length; i++)
            packed[i] = (int)_cfg.GetValue(section, $"{keyPrefix}{i}", 0);
        return BuildSnapshotCodec.Unpack(packed);
    }

    private static int CompareEntries(PersonalBestEntry left, PersonalBestEntry right)
    {
        int cmp = right.Score.CompareTo(left.Score);
        if (cmp != 0) return cmp;

        if (left.Won != right.Won) return left.Won ? -1 : 1;

        cmp = right.WaveReached.CompareTo(left.WaveReached);
        if (cmp != 0) return cmp;

        cmp = right.LivesRemaining.CompareTo(left.LivesRemaining);
        if (cmp != 0) return cmp;

        cmp = right.TotalDamageDealt.CompareTo(left.TotalDamageDealt);
        if (cmp != 0) return cmp;

        cmp = right.TotalKills.CompareTo(left.TotalKills);
        if (cmp != 0) return cmp;

        cmp = left.PlayTimeSeconds.CompareTo(right.PlayTimeSeconds);
        if (cmp != 0) return cmp;

        return right.RunTimestampUnixSeconds.CompareTo(left.RunTimestampUnixSeconds);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
    }

    private void Load()
    {
        SteamCloudSync.PullIfNewer(ProjectSettings.GlobalizePath(SavePath), "high_scores.cfg");
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
        else
            SteamCloudSync.Push(ProjectSettings.GlobalizePath(SavePath), "high_scores.cfg");
    }
}
