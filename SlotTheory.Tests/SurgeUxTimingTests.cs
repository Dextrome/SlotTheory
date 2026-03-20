using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public class SurgeUxTimingTests
{
    [Fact]
    public void ResolveWorldTeachingHintHold_UsesFloor()
    {
        Assert.Equal(SurgeUxTiming.WorldTeachingHintMinHoldSeconds, SurgeUxTiming.ResolveWorldTeachingHintHold(1.0f), 3);
        Assert.Equal(13.25f, SurgeUxTiming.ResolveWorldTeachingHintHold(13.25f), 3);
    }

    [Fact]
    public void ResolveSurgeMeterHintHold_UsesFloor()
    {
        Assert.Equal(SurgeUxTiming.SurgeMeterHintMinHoldSeconds, SurgeUxTiming.ResolveSurgeMeterHintHold(0.8f), 3);
        Assert.Equal(6.1f, SurgeUxTiming.ResolveSurgeMeterHintHold(6.1f), 3);
    }

    [Fact]
    public void ResolveSurgeCalloutDurationScale_UsesFloor()
    {
        Assert.Equal(SurgeUxTiming.SurgeCalloutMinDurationScale, SurgeUxTiming.ResolveSurgeCalloutDurationScale(2.8f), 3);
        Assert.Equal(6.4f, SurgeUxTiming.ResolveSurgeCalloutDurationScale(6.4f), 3);
    }

    [Fact]
    public void ResolveSurgeCalloutHoldPortion_ClampsAndUsesFloor()
    {
        Assert.Equal(SurgeUxTiming.SurgeCalloutMinHoldPortion, SurgeUxTiming.ResolveSurgeCalloutHoldPortion(0.6f), 3);
        Assert.Equal(0.90f, SurgeUxTiming.ResolveSurgeCalloutHoldPortion(0.90f), 3);
        Assert.Equal(0.95f, SurgeUxTiming.ResolveSurgeCalloutHoldPortion(1.4f), 3);
    }

    [Fact]
    public void ResolveTriadAugmentRemaining_UsesFloor()
    {
        Assert.Equal(SurgeUxTiming.TriadAugmentMinRemainingSeconds, SurgeUxTiming.ResolveTriadAugmentRemaining(2.0f), 3);
        Assert.Equal(7.4f, SurgeUxTiming.ResolveTriadAugmentRemaining(7.4f), 3);
    }
}
