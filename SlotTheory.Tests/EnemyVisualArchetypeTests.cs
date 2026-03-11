using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

public sealed class EnemyVisualArchetypeTests
{
    [Fact]
    public void ForType_BasicWalker_UsesBeetleProfile()
    {
        var archetype = EnemyVisualArchetype.ForType("basic_walker");
        Assert.Equal("Neon Beetle Drone", archetype.Name);
        Assert.Equal(EnemyTrailShape.SoftRibbon, archetype.TrailShape);
    }

    [Fact]
    public void ForType_SwiftWalker_UsesRazorProfile()
    {
        var archetype = EnemyVisualArchetype.ForType("swift_walker");
        Assert.Equal("Razor Ray / Dart Eel", archetype.Name);
        Assert.Equal(EnemyTrailShape.RazorArc, archetype.TrailShape);
        Assert.True(archetype.TurnTiltScale > EnemyVisualArchetype.ForType("armored_walker").TurnTiltScale);
    }

    [Fact]
    public void ForType_ArmoredWalker_UsesRhinoProfile()
    {
        var archetype = EnemyVisualArchetype.ForType("armored_walker");
        Assert.Equal("Plated Rhino Core", archetype.Name);
        Assert.Equal(EnemyTrailShape.DenseEmber, archetype.TrailShape);
        Assert.True(archetype.TrailWidth > EnemyVisualArchetype.ForType("swift_walker").TrailWidth);
    }

    [Fact]
    public void ForType_Unknown_FallsBackToBasic()
    {
        var archetype = EnemyVisualArchetype.ForType("unknown_enemy_type");
        Assert.Equal("Neon Beetle Drone", archetype.Name);
        Assert.Equal(EnemyTrailShape.SoftRibbon, archetype.TrailShape);
    }
}
