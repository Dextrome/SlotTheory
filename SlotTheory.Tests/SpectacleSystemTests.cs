using SlotTheory.Core;
using SlotTheory.Modifiers;
using Xunit;

namespace SlotTheory.Tests;

public class SpectacleSystemTests
{
    private sealed class StubModifier : Modifier
    {
        public StubModifier(string id) => ModifierId = id;
    }

    private static FakeTower TowerWithMods(params string[] modifierIds)
    {
        var tower = new FakeTower { AttackInterval = 1f };
        foreach (string id in modifierIds)
            tower.Modifiers.Add(new StubModifier(id));
        return tower;
    }

    [Fact]
    public void CopyAndDiversityMultipliers_MatchPlannedTables()
    {
        Assert.Equal(0f, SpectacleDefinitions.GetCopyMultiplier(0), 3);
        Assert.Equal(1.00f, SpectacleDefinitions.GetCopyMultiplier(1), 3);
        Assert.Equal(1.92f, SpectacleDefinitions.GetCopyMultiplier(2), 3);
        Assert.Equal(2.70f, SpectacleDefinitions.GetCopyMultiplier(3), 3);

        Assert.Equal(1.00f, SpectacleDefinitions.GetDiversityMultiplier(1), 3);
        Assert.Equal(1.08f, SpectacleDefinitions.GetDiversityMultiplier(2), 3);
        Assert.Equal(1.16f, SpectacleDefinitions.GetDiversityMultiplier(3), 3);
    }

    [Fact]
    public void ComboDefinition_UsesCanonicalPairMapping()
    {
        SpectacleComboDef combo = SpectacleDefinitions.GetCombo("split_shot", "momentum");
        Assert.Equal("C_MOMENTUM_SPLIT", combo.EffectId);
        Assert.Equal("Escalating Barrage", combo.Name);
    }

    [Fact]
    public void PreviewSignature_DuplicateModGetsDoubleShareInCombo()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot", "split_shot", "slow");

        SpectacleSignature sig = system.PreviewSignature(tower);

        Assert.Equal(SpectacleMode.Combo, sig.Mode);
        Assert.Equal(SpectacleDefinitions.SplitShot, sig.PrimaryModId);
        Assert.Equal(SpectacleDefinitions.ChillShot, sig.SecondaryModId);
        Assert.Equal(2f / 3f, sig.PrimaryShare, 3);
        Assert.Equal(1f / 3f, sig.SecondaryShare, 3);
    }

    [Fact]
    public void RegisterProc_WithMinorDisabled_OnlyMajorTriggersAtThreshold()
    {
        var system = new SpectacleSystem();
        var tower = TowerWithMods("split_shot");
        int minorCount = 0;
        int majorCount = 0;

        system.OnMinorTriggered += _ => minorCount++;
        system.OnMajorTriggered += _ => majorCount++;

        // 1.7 * 36 * (0.75 / 1.3) ~= 35.3 gain -> no trigger while minors are disabled.
        system.RegisterProc(tower, "split_shot", 36f);
        Assert.Equal(0, minorCount);
        Assert.Equal(0, majorCount);

        // 35.3 carried meter + (1.7 * 80 * (0.75 / 1.3) ~= 78.5) reaches major threshold.
        system.RegisterProc(tower, "split_shot", 80f);
        Assert.Equal(0, minorCount);
        Assert.Equal(1, majorCount);
    }

    [Fact]
    public void GlobalTrigger_FiresAfterFourMajorsFromTwoTowers()
    {
        var system = new SpectacleSystem();
        var towerA = TowerWithMods("split_shot");
        var towerB = TowerWithMods("split_shot");
        int globalCount = 0;
        GlobalSpectacleTriggerInfo lastGlobal = default;

        system.OnGlobalTriggered += info =>
        {
            globalCount++;
            lastGlobal = info;
        };

        // Major #1 and #2.
        system.RegisterProc(towerA, "split_shot", 110f);
        system.RegisterProc(towerB, "split_shot", 110f);

        // Wait for tower major cooldown to expire.
        system.Update(SpectacleDefinitions.MajorCooldownSeconds + 0.05f);

        // Major #3 and #4 -> global should trigger (two unique contributors in-window).
        system.RegisterProc(towerA, "split_shot", 110f);
        system.RegisterProc(towerB, "split_shot", 110f);

        Assert.Equal(1, globalCount);
        Assert.True(lastGlobal.UniqueContributors >= 2);
        Assert.Equal("G_SPECTACLE_CATHARSIS", lastGlobal.EffectId);
    }
}
