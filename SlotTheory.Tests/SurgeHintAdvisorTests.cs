using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

public class SurgeHintAdvisorTests
{
    [Fact]
    public void ShouldShowMicroHint_ReturnsFalseWhenRetired()
    {
        var profile = new SurgeHintProfileState
        {
            GlobalActivateRetired = true
        };
        var runtime = new SurgeHintRuntimeState();

        Assert.False(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.GlobalActivate, profile, runtime, playTime: 0f));
    }

    [Fact]
    public void ShouldShowMicroHint_UnretiresWhenBelowNewLifetimeCap()
    {
        int cap = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.TowerReady);
        var profile = new SurgeHintProfileState
        {
            TowerReadyRetired = true,
            TowerReadyLifetimeShows = cap - 1
        };
        var runtime = new SurgeHintRuntimeState();

        bool shown = SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.TowerReady, profile, runtime, playTime: 0f);

        Assert.True(shown);
        Assert.False(profile.TowerReadyRetired);
    }

    [Fact]
    public void RuntimeState_ResetClearsRunCapsAndCooldowns()
    {
        var profile = new SurgeHintProfileState();
        var runtime = new SurgeHintRuntimeState();

        SurgeHintAdvisor.RecordMicroHintShown(SurgeHintId.CombatFills, profile, runtime, playTime: 10f);
        Assert.False(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.CombatFills, profile, runtime, playTime: 100f));

        runtime.Reset();

        Assert.True(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.CombatFills, profile, runtime, playTime: 100f));
    }

    [Fact]
    public void ShouldShowMicroHint_RespectsRunCapsAndCooldown()
    {
        var profile = new SurgeHintProfileState();
        var runtime = new SurgeHintRuntimeState();

        Assert.True(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.GlobalActivate, profile, runtime, playTime: 5f));

        SurgeHintAdvisor.RecordMicroHintShown(SurgeHintId.GlobalActivate, profile, runtime, playTime: 5f);
        Assert.False(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.GlobalActivate, profile, runtime, playTime: 8f));

        float afterCooldown = 5f + SurgeHintAdvisor.GetRunCooldownSeconds(SurgeHintId.GlobalActivate) + 0.1f;
        Assert.True(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.GlobalActivate, profile, runtime, playTime: afterCooldown));

        SurgeHintAdvisor.RecordMicroHintShown(SurgeHintId.GlobalActivate, profile, runtime, playTime: afterCooldown);
        Assert.False(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.GlobalActivate, profile, runtime, playTime: afterCooldown + 100f));
    }

    [Fact]
    public void RecordMicroHintShown_RetiresAtLifetimeCap()
    {
        var profile = new SurgeHintProfileState();
        var runtime = new SurgeHintRuntimeState();
        int cap = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.CombatFills);

        for (int i = 0; i < cap; i++)
            SurgeHintAdvisor.RecordMicroHintShown(SurgeHintId.CombatFills, profile, runtime, playTime: i * 20f);

        Assert.Equal(cap, profile.CombatFillsLifetimeShows);
        Assert.True(profile.CombatFillsRetired);
        Assert.False(SurgeHintAdvisor.ShouldShowMicroHint(SurgeHintId.CombatFills, profile, runtime, playTime: 999f));
    }

    [Fact]
    public void RecordPostLossTipDisplayed_TracksRepeatsAndSwitchesTip()
    {
        var profile = new SurgeHintProfileState();

        SurgeHintAdvisor.RecordPostLossTipDisplayed(profile, "global_unused");
        Assert.Equal("global_unused", profile.LastPostLossTipId);
        Assert.Equal(1, profile.LastPostLossTipRepeatCount);

        SurgeHintAdvisor.RecordPostLossTipDisplayed(profile, "global_unused");
        Assert.Equal(2, profile.LastPostLossTipRepeatCount);

        SurgeHintAdvisor.RecordPostLossTipDisplayed(profile, "combat_fills");
        Assert.Equal("combat_fills", profile.LastPostLossTipId);
        Assert.Equal(1, profile.LastPostLossTipRepeatCount);
    }

    [Fact]
    public void SelectPostLossTip_ReturnsNullWhenNoRelevantMissedOpportunity()
    {
        var run = new SurgeHintRunTelemetry
        {
            TowersSurged = 1,
            GlobalsBecameReady = 0,
            GlobalsActivated = 0,
            ComboTowersBuiltThisRun = 1
        };
        var profile = new SurgeHintProfileState
        {
            ComboTowersBuiltTotal = 2
        };

        SurgePostLossTip? tip = SurgeHintAdvisor.SelectPostLossTip(run, profile);

        Assert.Null(tip);
    }

    [Fact]
    public void SelectPostLossTip_PrioritizesUnusedGlobalSurge()
    {
        var run = new SurgeHintRunTelemetry
        {
            GlobalsBecameReady = 1,
            GlobalsActivated = 0,
            GlobalReadyUnusedSeconds = 4f,
            LostWithGlobalReadyUnused = true,
            TowersSurged = 3
        };
        var profile = new SurgeHintProfileState();

        SurgePostLossTip? tip = SurgeHintAdvisor.SelectPostLossTip(run, profile);

        Assert.NotNull(tip);
        Assert.Equal("global_unused", tip!.Value.Id);
    }

    [Fact]
    public void SelectPostLossTip_SkipsSameTipAfterRepeatCap()
    {
        var run = new SurgeHintRunTelemetry
        {
            GlobalsBecameReady = 1,
            GlobalsActivated = 0,
            GlobalReadyUnusedSeconds = 6f,
            TowersSurged = 4
        };
        var profile = new SurgeHintProfileState
        {
            LastPostLossTipId = "global_unused",
            LastPostLossTipRepeatCount = 2
        };

        SurgePostLossTip? tip = SurgeHintAdvisor.SelectPostLossTip(run, profile);

        Assert.NotNull(tip);
        Assert.Equal("full_towers_power", tip!.Value.Id);
    }

    [Fact]
    public void ApplyRunOutcome_GlobalActivateRetiresAfterTwoActivatedRuns()
    {
        var profile = new SurgeHintProfileState();

        SurgeHintAdvisor.ApplyRunOutcome(profile, new SurgeHintRunTelemetry { GlobalsActivated = 1 }, won: false);
        Assert.False(profile.GlobalActivateRetired);
        Assert.Equal(1, profile.GlobalActivationRuns);
        Assert.Equal(1, profile.GlobalActivationsTotal);

        SurgeHintAdvisor.ApplyRunOutcome(profile, new SurgeHintRunTelemetry { GlobalsActivated = 1 }, won: false);
        Assert.True(profile.GlobalActivateRetired);
        Assert.Equal(2, profile.GlobalActivationRuns);
        Assert.Equal(2, profile.GlobalActivationsTotal);
    }

    [Fact]
    public void ApplyRunOutcome_DoesNotRetireCoreExplanationHintsAfterSingleActivation()
    {
        var profile = new SurgeHintProfileState();

        SurgeHintAdvisor.ApplyRunOutcome(profile, new SurgeHintRunTelemetry { GlobalsActivated = 1 }, won: false);

        Assert.False(profile.CombatFillsRetired);
        Assert.False(profile.GlobalContributionRetired);
    }

    [Fact]
    public void ApplyRunOutcome_RetiresHintsWhenPlayerShowsUnderstanding()
    {
        var run = new SurgeHintRunTelemetry
        {
            GlobalsActivated = 2,
            QuickGlobalActivationsWithin10s = 2,
            ComboTowersBuiltThisRun = 3,
            ComboTowerSurgesThisRun = 2
        };
        var profile = new SurgeHintProfileState();

        SurgeHintAdvisor.ApplyRunOutcome(profile, run, won: true);

        Assert.Equal(2, profile.GlobalActivationsTotal);
        Assert.Equal(1, profile.GlobalActivationRuns);
        Assert.Equal(1, profile.WinsWithGlobalActivation);
        Assert.Equal(3, profile.ComboTowersBuiltTotal);
        Assert.Equal(2, profile.QuickGlobalActivationsTotal);
        Assert.True(profile.CombatFillsRetired);
        Assert.True(profile.TowerReadyRetired);
        Assert.True(profile.GlobalContributionRetired);
        Assert.True(profile.ComboUnlockRetired);
    }

    [Fact]
    public void ApplyRunOutcome_RetiresOnLifetimeCaps()
    {
        var run = new SurgeHintRunTelemetry();
        var profile = new SurgeHintProfileState
        {
            CombatFillsLifetimeShows = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.CombatFills),
            TowerReadyLifetimeShows = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.TowerReady),
            GlobalContributionLifetimeShows = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.GlobalContribution),
            GlobalActivateLifetimeShows = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.GlobalActivate),
            ComboUnlockLifetimeShows = SurgeHintAdvisor.GetLifetimeCap(SurgeHintId.ComboUnlock)
        };

        SurgeHintAdvisor.ApplyRunOutcome(profile, run, won: false);

        Assert.True(profile.CombatFillsRetired);
        Assert.True(profile.TowerReadyRetired);
        Assert.True(profile.GlobalContributionRetired);
        Assert.True(profile.GlobalActivateRetired);
        Assert.True(profile.ComboUnlockRetired);
    }
}
