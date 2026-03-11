using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Lightweight rolling profiler focused on enemy rendering workloads.
/// Designed for live dev HUD diagnostics and reproducible perf report dumps.
/// </summary>
public sealed class EnemyRenderPerfProfiler
{
    private readonly float[] _frameMsWindow = new float[360];
    private int _frameMsCursor;
    private int _frameMsCount;

    private long _frameCount;
    private float _totalFrameMs;
    private float _maxFrameMs;
    private int _maxEnemiesAlive;
    private int _maxBloomPrimitives;
    private int _maxBloomBudgetRejected;
    private int _maxBloomFallbackCalls;

    public void RecordFrame(float deltaSeconds, int enemiesAlive)
    {
        float frameMs = Mathf.Max(0f, deltaSeconds * 1000f);
        _frameMsWindow[_frameMsCursor] = frameMs;
        _frameMsCursor = (_frameMsCursor + 1) % _frameMsWindow.Length;
        _frameMsCount = Mathf.Min(_frameMsCount + 1, _frameMsWindow.Length);

        _frameCount++;
        _totalFrameMs += frameMs;
        _maxFrameMs = Mathf.Max(_maxFrameMs, frameMs);
        _maxEnemiesAlive = Mathf.Max(_maxEnemiesAlive, enemiesAlive);
        _maxBloomPrimitives = Mathf.Max(_maxBloomPrimitives, SlotTheory.Entities.EnemyRenderDebugCounters.BloomPrimitives);
        _maxBloomBudgetRejected = Mathf.Max(_maxBloomBudgetRejected, SlotTheory.Entities.EnemyRenderDebugCounters.BloomBudgetRejected);
        _maxBloomFallbackCalls = Mathf.Max(_maxBloomFallbackCalls, SlotTheory.Entities.EnemyRenderDebugCounters.BloomFallbackCalls);
    }

    public string BuildOverlaySummary()
    {
        float avgMs = _frameCount > 0 ? _totalFrameMs / _frameCount : 0f;
        float p95Ms = CalculatePercentileMs(0.95f);
        return $"fps:{Engine.GetFramesPerSecond(),3:0} ms(avg/p95):{avgMs:0.00}/{p95Ms:0.00}";
    }

    public string WriteReport(
        string mapId,
        int wave,
        SettingsManager? settings,
        bool isMobile)
    {
        string dir = "user://perf_reports";
        DirAccess.MakeDirRecursiveAbsolute(dir);

        string stamp = Time.GetDatetimeStringFromSystem()
            .Replace(":", "-")
            .Replace(" ", "_");
        string filePath = $"{dir}/enemy_render_profile_{stamp}.md";

        float avgMs = _frameCount > 0 ? _totalFrameMs / _frameCount : 0f;
        float p95Ms = CalculatePercentileMs(0.95f);
        float p99Ms = CalculatePercentileMs(0.99f);

        using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
        if (file == null)
            return ProjectSettings.GlobalizePath(filePath);

        file.StoreLine("# Enemy Render Performance Report");
        file.StoreLine("");
        file.StoreLine($"- Timestamp: {Time.GetDatetimeStringFromSystem()}");
        file.StoreLine($"- Platform: {(isMobile ? "Mobile" : "Desktop")} ({OS.GetName()})");
        file.StoreLine($"- Map: {mapId}");
        file.StoreLine($"- Wave: {wave}");
        file.StoreLine($"- Frames sampled: {_frameCount}");
        file.StoreLine("");
        file.StoreLine("## Frame Timing");
        file.StoreLine($"- Average frame ms: {avgMs:0.00}");
        file.StoreLine($"- P95 frame ms: {p95Ms:0.00}");
        file.StoreLine($"- P99 frame ms: {p99Ms:0.00}");
        file.StoreLine($"- Worst frame ms: {_maxFrameMs:0.00}");
        file.StoreLine("");
        file.StoreLine("## Enemy Rendering Load");
        file.StoreLine($"- Max enemies alive: {_maxEnemiesAlive}");
        file.StoreLine($"- Max bloom primitives/frame: {_maxBloomPrimitives}");
        file.StoreLine($"- Max bloom budget rejects/frame: {_maxBloomBudgetRejected}");
        file.StoreLine($"- Max bloom fallback calls/frame: {_maxBloomFallbackCalls}");
        file.StoreLine("");
        file.StoreLine("## Toggle State");
        file.StoreLine($"- Layered enemy rendering: {settings?.LayeredEnemyRendering ?? true}");
        file.StoreLine($"- Emissive lines: {settings?.EnemyEmissiveLines ?? true}");
        file.StoreLine($"- Damage material: {settings?.EnemyDamageMaterial ?? true}");
        file.StoreLine($"- Enemy bloom highlights: {settings?.EnemyBloomHighlights ?? !isMobile}");
        file.StoreLine($"- Post FX: {settings?.PostFxEnabled ?? true}");
        file.StoreLine("");
        file.StoreLine("## Notes");
        file.StoreLine("- Capture this report on both Desktop and Android for side-by-side comparison.");
        file.StoreLine("- Prefer same map + wave + speed setting for apples-to-apples checks.");

        return ProjectSettings.GlobalizePath(filePath);
    }

    private float CalculatePercentileMs(float percentile)
    {
        if (_frameMsCount <= 0)
            return 0f;

        var scratch = new float[_frameMsCount];
        int sourceIndex = (_frameMsCursor - _frameMsCount + _frameMsWindow.Length) % _frameMsWindow.Length;
        for (int i = 0; i < _frameMsCount; i++)
            scratch[i] = _frameMsWindow[(sourceIndex + i) % _frameMsWindow.Length];

        System.Array.Sort(scratch);
        int idx = Mathf.Clamp(Mathf.CeilToInt((_frameMsCount - 1) * percentile), 0, _frameMsCount - 1);
        return scratch[idx];
    }
}
