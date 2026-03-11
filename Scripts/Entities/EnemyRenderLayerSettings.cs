using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Runtime pass toggles for layered enemy rendering.
/// </summary>
public readonly struct EnemyRenderLayerSettings
{
    private static ulong _perfCacheFrame;
    private static PerformanceLevel _perfCacheValue = PerformanceLevel.High;

    public bool LayeredEnabled { get; }
    public bool EmissiveEnabled { get; }
    public bool DamageEnabled { get; }
    public bool BloomEnabled { get; }
    public bool PostFxEnabled { get; }
    public int BloomPrimitiveBudget { get; }
    public float EmissivePerfScale { get; }

    public bool RenderEmissive => LayeredEnabled && EmissiveEnabled;
    public bool RenderDamage => LayeredEnabled && DamageEnabled;
    public bool RenderBloom => LayeredEnabled && BloomEnabled;
    public bool RenderBloomFallback => LayeredEnabled && EmissiveEnabled && !BloomEnabled;
    public float BloomAlphaScale => PostFxEnabled ? 1f : 0.45f;
    public float BloomFallbackAlphaScale => PostFxEnabled ? 0.42f : 0.28f;

    private EnemyRenderLayerSettings(
        bool layeredEnabled,
        bool emissiveEnabled,
        bool damageEnabled,
        bool bloomEnabled,
        bool postFxEnabled,
        int bloomPrimitiveBudget,
        float emissivePerfScale)
    {
        LayeredEnabled = layeredEnabled;
        EmissiveEnabled = emissiveEnabled;
        DamageEnabled = damageEnabled;
        BloomEnabled = bloomEnabled;
        PostFxEnabled = postFxEnabled;
        BloomPrimitiveBudget = bloomPrimitiveBudget;
        EmissivePerfScale = emissivePerfScale;
    }

    public static EnemyRenderLayerSettings FromFlags(
        bool layeredEnabled,
        bool emissiveEnabled,
        bool damageEnabled,
        bool bloomEnabled,
        bool postFxEnabled = true,
        int bloomPrimitiveBudget = 320,
        float emissivePerfScale = 1f)
        => new(
            layeredEnabled,
            emissiveEnabled,
            damageEnabled,
            bloomEnabled,
            postFxEnabled,
            bloomPrimitiveBudget,
            emissivePerfScale);

    public static EnemyRenderLayerSettings Resolve(SettingsManager? settings)
    {
        var performance = ResolvePerformanceLevel();
        float emissivePerfScale = performance switch
        {
            PerformanceLevel.Low => 0.82f,
            PerformanceLevel.Medium => 0.92f,
            _ => 1.0f,
        };

        if (settings == null)
        {
            bool bloomEnabled = !MobileOptimization.IsMobile();
            return new EnemyRenderLayerSettings(
                layeredEnabled: true,
                emissiveEnabled: true,
                damageEnabled: true,
                bloomEnabled: bloomEnabled,
                postFxEnabled: true,
                bloomPrimitiveBudget: ResolveBloomPrimitiveBudget(performance, bloomEnabled, postFxEnabled: true),
                emissivePerfScale: emissivePerfScale);
        }

        return new EnemyRenderLayerSettings(
            layeredEnabled: settings.LayeredEnemyRendering,
            emissiveEnabled: settings.EnemyEmissiveLines,
            damageEnabled: settings.EnemyDamageMaterial,
            bloomEnabled: settings.EnemyBloomHighlights,
            postFxEnabled: settings.PostFxEnabled,
            bloomPrimitiveBudget: ResolveBloomPrimitiveBudget(performance, settings.EnemyBloomHighlights, settings.PostFxEnabled),
            emissivePerfScale: emissivePerfScale);
    }

    private static int ResolveBloomPrimitiveBudget(PerformanceLevel level, bool bloomEnabled, bool postFxEnabled)
    {
        if (!bloomEnabled)
            return 0;

        int budget = level switch
        {
            PerformanceLevel.Low => 120,
            PerformanceLevel.Medium => 220,
            _ => 360,
        };

        if (!postFxEnabled)
            budget = (int)(budget * 0.70f);

        return budget;
    }

    private static PerformanceLevel ResolvePerformanceLevel()
    {
        ulong frame = Engine.GetProcessFrames();
        if (_perfCacheFrame == frame)
            return _perfCacheValue;

        _perfCacheFrame = frame;
        _perfCacheValue = MobileOptimization.GetPerformanceLevel();
        return _perfCacheValue;
    }
}
