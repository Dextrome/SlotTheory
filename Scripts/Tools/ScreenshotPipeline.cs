using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Tools;

// ── Event types ──────────────────────────────────────────────────────────────

public enum ScreenshotEvent
{
    TowerSurge,
    GlobalSurge,
    GlobalSurgeReady,
    OverkillSpill,
    BlastCoreSplash,
    MarkedEnemyPop,
    ChainMaxBounce,
    FeedbackLoop,
    HighEnemyDensity,
    WaveStart,
    DraftScreen,
    WinScreen,
    LossScreen,
}

// ── Metadata sidecar (saved as JSON next to each PNG) ────────────────────────

public sealed class ScreenshotMetadata
{
    [JsonPropertyName("file")]          public string       File         { get; set; } = "";
    [JsonPropertyName("event")]         public string       Event        { get; set; } = "";
    [JsonPropertyName("label")]         public string       Label        { get; set; } = "";
    [JsonPropertyName("score")]         public float        Score        { get; set; }
    [JsonPropertyName("wave")]          public int          Wave         { get; set; }
    [JsonPropertyName("enemy_count")]   public int          EnemyCount   { get; set; }
    [JsonPropertyName("lives")]         public int          Lives        { get; set; }
    [JsonPropertyName("towers")]        public List<string> Towers       { get; set; } = new();
    [JsonPropertyName("modifiers")]     public List<string> Modifiers    { get; set; } = new();
    [JsonPropertyName("surge_effect")]  public string?      SurgeEffect  { get; set; }
    [JsonPropertyName("run_index")]     public int          RunIndex     { get; set; }
    [JsonPropertyName("captured_at")]   public string       CapturedAt   { get; set; } = "";
}

// ── Pipeline node ─────────────────────────────────────────────────────────────

/// <summary>
/// Automated screenshot extraction pipeline.
/// Activated via --screenshot-capture CLI flag (requires non-headless rendering).
///
/// Usage:
///   "E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
///     --path "E:/SlotTheory" --scene "res://Scenes/Main.tscn" \
///     -- --bot --runs 20 --screenshot-capture [--screenshot_out "E:/my/out"]
///
/// Output: screenshots/YYYYMMDD_HHmmss_fff_EventType_w{wave}_s{score}.png + .json
/// Gallery: screenshots/gallery.html  (regenerated each session)
/// Manifest: screenshots/manifest.json
/// </summary>
public partial class ScreenshotPipeline : Node
{
    // ── Tuning ────────────────────────────────────────────────────────────────

    /// Maximum candidates kept per session (across all runs).
    private const int MaxCandidates = 300;

    /// Frames to wait after queueing before capturing (lets VFX render).
    private const int DefaultFrameDelay = 2;

    /// Frames to wait for win/loss screen animations to settle.
    private const int ScreenFrameDelay = 90;

    /// Frames to wait for draft card-flip animation to complete (~500 ms at 60 fps).
    private const int DraftFrameDelay = 30;

    /// Minimum enemies on screen to earn an enemy-density bonus.
    private const int DensityBonusThreshold = 4;

    /// Minimum enemies for a periodic density capture to be queued.
    private const int PeriodicDensityMinEnemies = 6;

    /// Frame interval for periodic density checks (at 60fps ≈ every 5s).
    private const ulong PeriodicCheckFrameInterval = 300;

    // Per-event-type minimum gap in seconds between captures of the same type.
    private static readonly float[] EventCooldowns;

    static ScreenshotPipeline()
    {
        int n = Enum.GetValues<ScreenshotEvent>().Length;
        EventCooldowns = new float[n];
        EventCooldowns[(int)ScreenshotEvent.GlobalSurge]       = 15f;
        EventCooldowns[(int)ScreenshotEvent.TowerSurge]        = 10f;
        EventCooldowns[(int)ScreenshotEvent.GlobalSurgeReady]  = 12f;
        EventCooldowns[(int)ScreenshotEvent.OverkillSpill]     =  8f;
        EventCooldowns[(int)ScreenshotEvent.BlastCoreSplash]   =  8f;
        EventCooldowns[(int)ScreenshotEvent.MarkedEnemyPop]    =  8f;
        EventCooldowns[(int)ScreenshotEvent.ChainMaxBounce]    =  8f;
        EventCooldowns[(int)ScreenshotEvent.FeedbackLoop]      = 10f;
        EventCooldowns[(int)ScreenshotEvent.HighEnemyDensity]  = 12f;
        EventCooldowns[(int)ScreenshotEvent.WaveStart]         =  0f; // once per wave
        EventCooldowns[(int)ScreenshotEvent.DraftScreen]       =  0f; // once per draft
        EventCooldowns[(int)ScreenshotEvent.WinScreen]         =  0f;
        EventCooldowns[(int)ScreenshotEvent.LossScreen]        =  0f;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private struct PendingCapture
    {
        public ScreenshotEvent    Event;
        public ScreenshotMetadata Metadata;
        public int                FramesRemaining;
        /// <summary>When true, re-snapshot dynamic fields (enemy count, lives, wave) at capture time.</summary>
        public bool               RefreshMetadata;
    }

    private RunState?           _runState;
    private string              _outputDir  = "";
    private int                 _runIndex;
    private bool                _initialized;

    private readonly List<PendingCapture>             _pending        = new();
    private readonly List<(string path, float score)> _saved          = new();
    private readonly float[]                          _lastCaptureTime;
    private readonly List<(ScreenshotEvent evt, float t)> _recentCaptures = new();

    public ScreenshotPipeline()
    {
        _lastCaptureTime = new float[Enum.GetValues<ScreenshotEvent>().Length];
        Array.Fill(_lastCaptureTime, -9999f);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call at the start of each run (both on first _Ready and on RestartRun).
    /// </summary>
    public void Initialize(RunState runState, string outputDir, int runIndex)
    {
        _runState   = runState;
        _outputDir  = outputDir;
        _runIndex   = runIndex;
        _initialized = true;
        _pending.Clear();
        _recentCaptures.Clear();
        Array.Fill(_lastCaptureTime, -9999f);

        try   { Directory.CreateDirectory(_outputDir); }
        catch { /* non-fatal */ }

        GD.Print($"[SCREENSHOT] Pipeline initialized → {_outputDir}");
    }

    public override void _Process(double _delta)
    {
        if (!_initialized) return;

        // Tick pending captures
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var p = _pending[i];
            p.FramesRemaining--;
            if (p.FramesRemaining <= 0)
            {
                _pending.RemoveAt(i);
                ExecuteCapture(p);
            }
            else
            {
                _pending[i] = p;
            }
        }

        // Periodic enemy-density check
        if (Engine.GetProcessFrames() % PeriodicCheckFrameInterval == 0)
            TryQueuePeriodicDensity();
    }

    // ── Notification entry points (called by GameController) ─────────────────

    public void NotifyTowerSurge(SpectacleTriggerInfo info)
    {
        string label = $"{info.Tower?.TowerId ?? "?"} ({info.Signature.EffectId})";
        QueueCapture(ScreenshotEvent.TowerSurge, extraLabel: label, surgeEffect: info.Signature.EffectId);
    }

    public void NotifyGlobalSurge(GlobalSurgeTriggerInfo info)
    {
        QueueBurst(ScreenshotEvent.GlobalSurge, count: 10, durationSeconds: 2f,
            extraLabel: info.EffectId, surgeEffect: info.EffectId);
    }

    public void NotifyGlobalSurgeReady(string archetypeLabel)
    {
        QueueCapture(ScreenshotEvent.GlobalSurgeReady, extraLabel: archetypeLabel);
    }

    public void NotifyOverkillSpill(float spillDamage)
    {
        // Only visually interesting spills
        if (spillDamage < 34f) return;
        QueueCapture(ScreenshotEvent.OverkillSpill, extraLabel: $"{spillDamage:F0}dmg");
    }

    public void NotifyBlastCoreSplash(int targetsHit)
    {
        if (targetsHit < 2) return;
        QueueCapture(ScreenshotEvent.BlastCoreSplash, extraLabel: $"{targetsHit} targets");
    }

    public void NotifyMarkedEnemyPop()
    {
        QueueCapture(ScreenshotEvent.MarkedEnemyPop);
    }

    public void NotifyChainMaxBounce(int bounceCount)
    {
        if (bounceCount < 2) return;
        QueueCapture(ScreenshotEvent.ChainMaxBounce, extraLabel: $"{bounceCount} bounces");
    }

    public void NotifyFeedbackLoop()
    {
        QueueCapture(ScreenshotEvent.FeedbackLoop);
    }

    public void NotifyWaveStart(int waveIndex)
    {
        QueueCapture(ScreenshotEvent.WaveStart, extraLabel: $"wave {waveIndex + 1}");
    }

    public void NotifyDraftScreen()
    {
        QueueCapture(ScreenshotEvent.DraftScreen, frameDelay: DraftFrameDelay);
    }

    public void NotifyWinScreen()
    {
        QueueCapture(ScreenshotEvent.WinScreen, frameDelay: ScreenFrameDelay);
    }

    public void NotifyLossScreen()
    {
        QueueCapture(ScreenshotEvent.LossScreen, frameDelay: ScreenFrameDelay);
    }

    // ── Internal queueing and scoring ─────────────────────────────────────────

    private void TryQueuePeriodicDensity()
    {
        if (_runState == null) return;
        int count = _runState.EnemiesAlive.Count;
        if (count >= PeriodicDensityMinEnemies)
            QueueCapture(ScreenshotEvent.HighEnemyDensity, extraLabel: $"{count} enemies");
    }

    private void QueueCapture(
        ScreenshotEvent evt,
        string?         extraLabel  = null,
        string?         surgeEffect = null,
        int?            frameDelay  = null)
    {
        if (!_initialized || _runState == null) return;
        if (_saved.Count >= MaxCandidates) return;

        float now      = _runState.TotalPlayTime;
        float cooldown = EventCooldowns[(int)evt];
        if (cooldown > 0f && now - _lastCaptureTime[(int)evt] < cooldown)
            return;

        float score = ComputeScore(evt, now, extraLabel);
        if (score < 5f) return;

        _pending.Add(new PendingCapture
        {
            Event           = evt,
            Metadata        = SnapshotMetadata(evt, score, extraLabel, surgeEffect),
            FramesRemaining = frameDelay ?? DefaultFrameDelay,
        });
    }

    /// <summary>
    /// Queues <paramref name="count"/> captures spread evenly over <paramref name="durationSeconds"/>.
    /// Bypasses per-event cooldown -- intended for high-value burst moments (e.g. global surge).
    /// Metadata is re-snapshotted at each capture frame so enemy counts stay accurate.
    /// </summary>
    private void QueueBurst(
        ScreenshotEvent evt,
        int             count,
        float           durationSeconds,
        string?         extraLabel  = null,
        string?         surgeEffect = null)
    {
        if (!_initialized || _runState == null) return;
        if (_saved.Count >= MaxCandidates) return;

        float now   = _runState.TotalPlayTime;
        float score = ComputeScore(evt, now, extraLabel);
        if (score < 5f) return;

        int totalFrames = Math.Max(count - 1, 1);
        int frameSpan   = (int)(durationSeconds * 60f);

        for (int i = 0; i < count; i++)
        {
            if (_saved.Count + _pending.Count >= MaxCandidates) break;
            int delay      = DefaultFrameDelay + (i == 0 ? 0 : frameSpan * i / totalFrames);
            string label   = extraLabel != null ? $"{extraLabel} [{i + 1}/{count}]" : $"[{i + 1}/{count}]";
            _pending.Add(new PendingCapture
            {
                Event           = evt,
                Metadata        = SnapshotMetadata(evt, score, label, surgeEffect),
                FramesRemaining = delay,
                RefreshMetadata = true,
            });
        }

        // Mark cooldown so normal QueueCapture won't fire a duplicate right after.
        _lastCaptureTime[(int)evt] = now;
    }

    private float ComputeScore(ScreenshotEvent evt, float now, string? extraLabel)
    {
        float score = evt switch
        {
            ScreenshotEvent.WinScreen         => 90f,
            ScreenshotEvent.GlobalSurge       => 88f,
            ScreenshotEvent.TowerSurge        => 65f,
            ScreenshotEvent.LossScreen        => 62f,
            ScreenshotEvent.GlobalSurgeReady  => 58f,
            ScreenshotEvent.ChainMaxBounce    => 52f,
            ScreenshotEvent.BlastCoreSplash   => 48f,
            ScreenshotEvent.MarkedEnemyPop    => 43f,
            ScreenshotEvent.OverkillSpill     => 38f,
            ScreenshotEvent.HighEnemyDensity  => 35f,
            ScreenshotEvent.WaveStart         => 35f,
            ScreenshotEvent.DraftScreen       => 30f,
            ScreenshotEvent.FeedbackLoop      => 28f,
            _                                 => 20f,
        };

        int   enemyCount = _runState?.EnemiesAlive.Count ?? 0;
        int   waveIdx    = _runState?.WaveIndex          ?? 0;
        float waveTime   = _runState?.WaveTime           ?? 0f;

        // Enemy density bonus (capped)
        if (enemyCount >= DensityBonusThreshold)
            score += Math.Min((enemyCount - DensityBonusThreshold + 1) * 2.5f, 20f);

        // Late-game bonus
        if      (waveIdx >= 14) score += 18f;
        else if (waveIdx >= 10) score += 10f;
        else if (waveIdx >=  7) score += 5f;

        // Mid-wave action bonus (not right at spawn, not at cleanup)
        if (waveTime > 3f && waveTime < 35f) score += 8f;

        // Penalty: too few enemies for action events
        bool isActionEvent = evt is not (ScreenshotEvent.WinScreen or ScreenshotEvent.LossScreen
                                       or ScreenshotEvent.DraftScreen or ScreenshotEvent.WaveStart);
        if (isActionEvent && enemyCount < 2) score -= 22f;

        // Diversity bonus: event type not seen recently
        bool recentSameType = _recentCaptures.Any(r => r.evt == evt && now - r.t < 25f);
        if (!recentSameType) score += 12f;

        // BlastCore: more targets = higher score
        if (evt == ScreenshotEvent.BlastCoreSplash && extraLabel != null)
        {
            if (int.TryParse(extraLabel.Split(' ')[0], out int targets))
                score += Math.Min(targets * 4f, 20f);
        }

        return score;
    }

    private ScreenshotMetadata SnapshotMetadata(
        ScreenshotEvent evt,
        float           score,
        string?         extraLabel,
        string?         surgeEffect)
    {
        var towers = new List<string>();
        var mods   = new List<string>();

        if (_runState?.Slots != null)
        {
            foreach (var slot in _runState.Slots)
            {
                if (slot?.Tower == null) continue;
                towers.Add(slot.Tower.TowerId);
                foreach (var m in slot.Tower.Modifiers)
                {
                    if (!mods.Contains(m.ModifierId))
                        mods.Add(m.ModifierId);
                }
            }
        }

        string evtName = evt.ToString();
        string label   = extraLabel != null ? $"{evtName}: {extraLabel}" : evtName;

        return new ScreenshotMetadata
        {
            Event       = evtName,
            Label       = label,
            Score       = MathF.Round(score, 1),
            Wave        = (_runState?.WaveIndex ?? 0) + 1,
            EnemyCount  = _runState?.EnemiesAlive.Count ?? 0,
            Lives       = _runState?.Lives ?? 0,
            Towers      = towers,
            Modifiers   = mods,
            SurgeEffect = surgeEffect,
            RunIndex    = _runIndex,
            CapturedAt  = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
    }

    // ── Capture execution ─────────────────────────────────────────────────────

    private void ExecuteCapture(PendingCapture capture)
    {
        if (_saved.Count >= MaxCandidates) return;

        // Re-snapshot dynamic state for burst shots so each frame has accurate counts.
        if (capture.RefreshMetadata && _runState != null)
        {
            capture.Metadata.EnemyCount = _runState.EnemiesAlive.Count;
            capture.Metadata.Lives      = _runState.Lives;
            capture.Metadata.Wave       = _runState.WaveIndex + 1;
        }

        try
        {
            // DisplayServer.ScreenGetImage captures the final composited window output,
            // including tone-mapping, bloom, and all post-processing effects.
            // GetViewport().GetTexture().GetImage() misses all of that and looks dark/flat.
            var image = DisplayServer.ScreenGetImage();

            string ts       = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string evtName  = capture.Event.ToString();
            string filename = $"{ts}_{evtName}_w{capture.Metadata.Wave}_s{(int)capture.Metadata.Score:D3}";
            string pngPath  = Path.Combine(_outputDir, filename + ".png");
            string jsonPath = Path.Combine(_outputDir, filename + ".json");

            image.SavePng(pngPath);

            capture.Metadata.File = Path.GetFileName(pngPath);
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(capture.Metadata, opts));

            _saved.Add((pngPath, capture.Metadata.Score));
            _lastCaptureTime[(int)capture.Event] = _runState?.TotalPlayTime ?? 0f;
            _recentCaptures.Add((capture.Event, _runState?.TotalPlayTime ?? 0f));
            if (_recentCaptures.Count > 60)
                _recentCaptures.RemoveAt(0);

            GD.Print($"[SCREENSHOT] {capture.Event,-20} score={capture.Metadata.Score,5:F0}  wave={capture.Metadata.Wave,2}  enemies={capture.Metadata.EnemyCount,2}  → {Path.GetFileName(pngPath)}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SCREENSHOT] Capture failed: {ex.Message}");
        }
    }

    // ── Session finalization ──────────────────────────────────────────────────

    /// <summary>
    /// Call when the session ends (e.g. BotRunner finishes all runs) to write
    /// gallery.html and manifest.json into the output directory.
    /// </summary>
    public void FinalizeSession()
    {
        if (_saved.Count == 0)
        {
            GD.Print("[SCREENSHOT] No candidates captured this session.");
            return;
        }

        GD.Print($"[SCREENSHOT] Session complete: {_saved.Count} candidates in {_outputDir}");

        try
        {
            // Load all metadata
            var allMeta = new List<ScreenshotMetadata>();
            foreach (var (path, _) in _saved)
            {
                string jsonPath = Path.ChangeExtension(path, ".json");
                if (!File.Exists(jsonPath)) continue;
                try
                {
                    var m = JsonSerializer.Deserialize<ScreenshotMetadata>(File.ReadAllText(jsonPath));
                    if (m != null) allMeta.Add(m);
                }
                catch { /* skip corrupt sidecar */ }
            }

            allMeta.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Write manifest
            var opts         = new JsonSerializerOptions { WriteIndented = true };
            string manifest  = Path.Combine(_outputDir, "manifest.json");
            File.WriteAllText(manifest, JsonSerializer.Serialize(allMeta, opts));

            // Write gallery
            string gallery   = Path.Combine(_outputDir, "gallery.html");
            File.WriteAllText(gallery, BuildGalleryHtml(allMeta));

            GD.Print($"[SCREENSHOT] Gallery  → {gallery}");
            GD.Print($"[SCREENSHOT] Manifest → {manifest}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SCREENSHOT] FinalizeSession failed: {ex.Message}");
        }
    }

    // ── HTML gallery builder ──────────────────────────────────────────────────

    private static string BuildGalleryHtml(List<ScreenshotMetadata> items)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Slot Theory Screenshot Candidates</title>
<style>
* { box-sizing: border-box; }
body { background:#0c0c12; color:#ccc; font-family:'Courier New',monospace;
       margin:0; padding:16px; }
h1   { color:#a8e063; font-size:1.15em; margin:0 0 4px 0; }
.sub { color:#555; font-size:.8em; margin:0 0 14px 0; }
.grid{ display:flex; flex-wrap:wrap; gap:10px; }
.card{ background:#17171f; border:1px solid #2c2c3a; padding:8px;
       width:300px; border-radius:4px; transition:border-color .15s; }
.card:hover{ border-color:#a8e063; }
.thumb{ width:100%; display:block; border-radius:2px; }
.score{ color:#a8e063; font-size:1.05em; font-weight:bold; }
.evnt { color:#7ec8e3; }
.meta { font-size:.75em; color:#666; margin-top:3px; }
.tag  { background:#222230; border-radius:3px; padding:1px 5px;
        margin:1px; display:inline-block; font-size:.72em; }
.surge{ color:#ffcc44; }
.mod  { color:#c5a3ff; }
</style>
</head>
<body>
""");

        sb.AppendLine($"<h1>Slot Theory -- Screenshot Candidates</h1>");
        sb.AppendLine($"<p class='sub'>{items.Count} candidates &nbsp;·&nbsp; generated {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
        sb.AppendLine("<div class='grid'>");

        foreach (var m in items)
        {
            sb.AppendLine($"""
<div class="card">
  <a href="{m.File}" target="_blank"><img class="thumb" src="{m.File}" loading="lazy" alt="{m.Label}"></a>
  <div><span class="score">★ {m.Score:F0}</span>&nbsp;&nbsp;<span class="evnt">{m.Event}</span></div>
  <div class="meta">Wave {m.Wave} · {m.EnemyCount} enemies · {m.Lives} lives · Run #{m.RunIndex}</div>
  <div class="meta">
""");
            foreach (var t in m.Towers)
                sb.AppendLine($"    <span class='tag'>{t}</span>");
            foreach (var mod in m.Modifiers)
                sb.AppendLine($"    <span class='tag mod'>{mod}</span>");
            if (!string.IsNullOrEmpty(m.SurgeEffect))
                sb.AppendLine($"    <span class='tag surge'>⚡ {m.SurgeEffect}</span>");
            sb.AppendLine($"  </div>");
            sb.AppendLine($"  <div class='meta'>{m.Label}</div>");
            sb.AppendLine($"</div>");
        }

        sb.AppendLine("</div>\n</body>\n</html>");
        return sb.ToString();
    }
}
