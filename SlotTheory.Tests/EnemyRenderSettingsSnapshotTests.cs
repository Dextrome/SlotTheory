using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyRenderSettingsSnapshotTests
{
    [Fact]
    public void WriteThenRead_RoundTripsAllEnemyRenderSettings()
    {
        var original = new EnemyRenderSettingsSnapshot(
            postFxEnabled: false,
            layeredEnabled: true,
            emissiveEnabled: false,
            damageMaterialEnabled: true,
            bloomEnabled: false);

        var serialized = original.ToDictionary();
        var restored = EnemyRenderSettingsSnapshot.ReadFrom(serialized, defaultBloomEnabled: true);

        Assert.Equal(original.PostFxEnabled, restored.PostFxEnabled);
        Assert.Equal(original.LayeredEnabled, restored.LayeredEnabled);
        Assert.Equal(original.EmissiveEnabled, restored.EmissiveEnabled);
        Assert.Equal(original.DamageMaterialEnabled, restored.DamageMaterialEnabled);
        Assert.Equal(original.BloomEnabled, restored.BloomEnabled);
    }

    [Fact]
    public void ReadFrom_UsesProvidedBloomDefaultWhenUnset()
    {
        var values = new System.Collections.Generic.Dictionary<string, bool>();

        var mobileDefault = EnemyRenderSettingsSnapshot.ReadFrom(values, defaultBloomEnabled: false);
        var desktopDefault = EnemyRenderSettingsSnapshot.ReadFrom(values, defaultBloomEnabled: true);

        Assert.False(mobileDefault.BloomEnabled);
        Assert.True(desktopDefault.BloomEnabled);
    }
}
