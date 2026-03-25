using System.Collections.Generic;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class CombatLabScenarioRunnerTests
{
    [Fact]
    public void OverkillScenario_PassesStageTwoTimingExpectation()
    {
        var scenario = new CombatLabScenario
        {
            Name = "overkill_test",
            ScenarioType = "overkill_bloom",
            TriggerDamage = 260f,
            TowerSetup = new List<CombatLabTowerSetup>
            {
                new() { Tower = "heavy_cannon", BaseDamage = 180f, Position = new[] { 0f, 0f }, Mods = new[] { "overkill", "focus_lens" } },
            },
            Enemies = new List<CombatLabEnemySetup>
            {
                new() { Id = 1, Hp = 120f, Position = new[] { 120f, 0f } },
                new() { Id = 2, Hp = 52f, Position = new[] { 180f, 0f } },
                new() { Id = 3, Hp = 52f, Position = new[] { 215f, 0f } },
            },
            Expect = new CombatLabExpectations
            {
                MajorSurges = 1,
                ExplosionHitsMin = 1,
                ExplosionHitsMax = 3,
                StageTwoDelayMinSeconds = 0.10f,
                StageTwoDelayMaxSeconds = 0.14f,
            },
        };

        CombatLabScenarioResult result = new CombatLabScenarioRunner().RunScenario(scenario);
        Assert.True(result.Passed, string.Join("\n", result.Failures));
    }

    [Fact]
    public void StatusDetonationScenario_RespectsTargetCapsAndResidueExpectation()
    {
        var scenario = new CombatLabScenario
        {
            Name = "status_detonation_test",
            ScenarioType = "status_detonation",
            SurgePower = 1.2f,
            ReducedMotion = true,
            TowerSetup = new List<CombatLabTowerSetup>
            {
                new() { Tower = "arc_emitter", Position = new[] { 0f, 0f }, Mods = new[] { "chain_reaction", "slow", "feedback_loop" }, BaseDamage = 44f },
            },
            Enemies = new List<CombatLabEnemySetup>
            {
                new() { Id = 1, PathT = 0.1f, Hp = 65f, Status = new[] { "slow" } },
                new() { Id = 2, PathT = 0.2f, Hp = 65f, Status = new[] { "mark" } },
                new() { Id = 3, PathT = 0.3f, Hp = 65f, Status = new[] { "slow" } },
                new() { Id = 4, PathT = 0.4f, Hp = 65f, Status = new[] { "amp" } },
                new() { Id = 5, PathT = 0.5f, Hp = 65f, Status = new[] { "mark" } },
            },
            Expect = new CombatLabExpectations
            {
                MajorSurges = 1,
                StatusDetonationsMin = 4,
                StatusDetonationsMax = 5,
                ResidueCount = 1,
                MaxSimultaneousDetonations = 1,
            },
        };

        CombatLabScenarioResult result = new CombatLabScenarioRunner().RunScenario(scenario);
        Assert.True(result.Passed, string.Join("\n", result.Failures));
    }

    [Fact]
    public void GlobalSurgeReducedMotion_CollapsesDelays()
    {
        var scenario = new CombatLabScenario
        {
            Name = "global_reduced_motion_test",
            ScenarioType = "global_surge",
            ReducedMotion = true,
            Contributors = 3,
            TowerSetup = new List<CombatLabTowerSetup>
            {
                new() { Tower = "marker_tower", Position = new[] { 0f, 0f }, Mods = new[] { "exploit_weakness", "split_shot" }, BaseDamage = 20f },
            },
            Enemies = new List<CombatLabEnemySetup>
            {
                new() { Id = 1, Position = new[] { 120f, 0f }, Hp = 65f },
                new() { Id = 2, Position = new[] { 240f, 0f }, Hp = 65f },
                new() { Id = 3, Position = new[] { 360f, 0f }, Hp = 65f },
            },
            Expect = new CombatLabExpectations
            {
                MajorSurges = 1,
                RippleMonotonic = true,
                ReducedMotionCollapsesDelays = true,
            },
        };

        CombatLabScenarioResult result = new CombatLabScenarioRunner().RunScenario(scenario);
        Assert.True(result.Passed, string.Join("\n", result.Failures));
    }
}
