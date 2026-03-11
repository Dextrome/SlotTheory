using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyRenderLayerSettingsTests
{
    [Fact]
    public void FromFlags_LayeredOff_DisablesAdvancedPasses()
    {
        var settings = EnemyRenderLayerSettings.FromFlags(
            layeredEnabled: false,
            emissiveEnabled: true,
            damageEnabled: true,
            bloomEnabled: true,
            postFxEnabled: true);

        Assert.False(settings.LayeredEnabled);
        Assert.False(settings.RenderEmissive);
        Assert.False(settings.RenderDamage);
        Assert.False(settings.RenderBloom);
    }

    [Fact]
    public void FromFlags_LayeredOn_RespectsIndividualPassToggles()
    {
        var settings = EnemyRenderLayerSettings.FromFlags(
            layeredEnabled: true,
            emissiveEnabled: false,
            damageEnabled: true,
            bloomEnabled: false,
            postFxEnabled: true);

        Assert.True(settings.LayeredEnabled);
        Assert.False(settings.RenderEmissive);
        Assert.True(settings.RenderDamage);
        Assert.False(settings.RenderBloom);
    }

    [Fact]
    public void BloomAlphaScale_DropsWhenPostFxDisabled()
    {
        var withPostFx = EnemyRenderLayerSettings.FromFlags(true, true, true, true, postFxEnabled: true);
        var withoutPostFx = EnemyRenderLayerSettings.FromFlags(true, true, true, true, postFxEnabled: false);

        Assert.True(withPostFx.BloomAlphaScale > withoutPostFx.BloomAlphaScale);
        Assert.Equal(0.45f, withoutPostFx.BloomAlphaScale, 4);
    }

    [Fact]
    public void FromFlags_BloomDisabled_EnablesFallbackWhenEmissiveIsOn()
    {
        var settings = EnemyRenderLayerSettings.FromFlags(
            layeredEnabled: true,
            emissiveEnabled: true,
            damageEnabled: true,
            bloomEnabled: false,
            postFxEnabled: true);

        Assert.False(settings.RenderBloom);
        Assert.True(settings.RenderBloomFallback);
    }

    [Fact]
    public void FromFlags_StoresBudgetAndPerfScale()
    {
        var settings = EnemyRenderLayerSettings.FromFlags(
            layeredEnabled: true,
            emissiveEnabled: true,
            damageEnabled: true,
            bloomEnabled: true,
            postFxEnabled: true,
            bloomPrimitiveBudget: 123,
            emissivePerfScale: 0.88f);

        Assert.Equal(123, settings.BloomPrimitiveBudget);
        Assert.Equal(0.88f, settings.EmissivePerfScale, 3);
    }
}
