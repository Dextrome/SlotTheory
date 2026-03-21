using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class CombatLabTowerBenchmarkRunnerTests
{
    [Fact]
    public void RunSuite_WithSameSeed_IsDeterministic()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["tower_a"] = new TowerDef("Tower A", BaseDamage: 22f, AttackInterval: 0.65f, Range: 300f),
            ["tower_b"] = new TowerDef("Tower B", BaseDamage: 11f, AttackInterval: 0.90f, Range: 280f),
        };
        var suite = BuildSimpleSuite(seed: 4242);

        var runner = new CombatLabTowerBenchmarkRunner(defs);
        CombatLabTowerBenchmarkReport first = runner.RunSuite(suite);
        CombatLabTowerBenchmarkReport second = runner.RunSuite(suite);

        Assert.Equal(first.ScenarioResults.Count, second.ScenarioResults.Count);
        Assert.Equal(first.TowerProfiles.Count, second.TowerProfiles.Count);

        var firstRows = first.ScenarioResults.SelectMany(s => s.Results).OrderBy(r => r.TowerId).ToList();
        var secondRows = second.ScenarioResults.SelectMany(s => s.Results).OrderBy(r => r.TowerId).ToList();
        Assert.Equal(firstRows.Count, secondRows.Count);
        for (int i = 0; i < firstRows.Count; i++)
        {
            Assert.Equal(firstRows[i].TowerId, secondRows[i].TowerId);
            Assert.Equal(firstRows[i].TotalDamage, secondRows[i].TotalDamage, 3);
            Assert.Equal(firstRows[i].EffectiveDps, secondRows[i].EffectiveDps, 3);
            Assert.Equal(firstRows[i].NormalizedGlobal, secondRows[i].NormalizedGlobal, 3);
        }
    }

    [Fact]
    public void RunSuite_FlagsRelativePowerOutliers()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["tower_a"] = new TowerDef("Tower A", BaseDamage: 34f, AttackInterval: 0.45f, Range: 320f),
            ["tower_b"] = new TowerDef("Tower B", BaseDamage: 6f, AttackInterval: 1.10f, Range: 250f),
        };
        var suite = BuildSimpleSuite(seed: 777);

        var report = new CombatLabTowerBenchmarkRunner(defs).RunSuite(suite);
        CombatLabTowerBenchmarkProfile strong = report.TowerProfiles.First(p => p.TowerId == "tower_a");
        CombatLabTowerBenchmarkProfile weak = report.TowerProfiles.First(p => p.TowerId == "tower_b");

        Assert.True(strong.AvgNormalizedGlobal > weak.AvgNormalizedGlobal);
        Assert.Contains(strong.Flags, f => f.Contains("overpowered") || f.Contains("dominant"));
        Assert.Contains(weak.Flags, f => f.Contains("underpowered"));
    }

    private static CombatLabTowerBenchmarkSuite BuildSimpleSuite(int seed)
    {
        return new CombatLabTowerBenchmarkSuite
        {
            Name = "test_suite",
            Mode = "base_combat_only",
            Seed = seed,
            TrialsPerScenario = 3,
            TimestepSeconds = 0.05f,
            IncludeAllTowers = false,
            RoleAssignments = new Dictionary<string, string[]>
            {
                ["tower_a"] = new[] { "generalist" },
                ["tower_b"] = new[] { "generalist" },
            },
            CostByTower = new Dictionary<string, float>
            {
                ["tower_a"] = 100f,
                ["tower_b"] = 100f,
            },
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { Tower = "tower_a", Mods = new string[0], Targeting = "first" },
                new() { Tower = "tower_b", Mods = new string[0], Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "mixed_wave",
                    Name = "Mixed Wave",
                    Tags = new[] { "generalist", "open_lane" },
                    DurationSeconds = 28f,
                    PathType = "open_lane",
                    PathLength = 1600f,
                    LaneWidth = 120f,
                    TowerPosition = new[] { 620f, 0f },
                    StopWhenResolved = true,
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "basic_swarm", Count = 12, Hp = 90f, Speed = 120f, SpawnInterval = 0.25f },
                        new() { Id = "tank_pack", Count = 3, Hp = 500f, Speed = 80f, SpawnInterval = 0.75f, StartTime = 1.2f },
                    },
                },
                new()
                {
                    Id = "tank_focus",
                    Name = "Tank Focus",
                    Tags = new[] { "generalist", "long_path", "anti_tank" },
                    DurationSeconds = 34f,
                    PathType = "long_path",
                    PathLength = 2400f,
                    LaneWidth = 100f,
                    TowerPosition = new[] { 880f, 0f },
                    StopWhenResolved = true,
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "tank_column", Count = 5, Hp = 720f, Speed = 70f, SpawnInterval = 0.8f },
                    },
                },
            },
        };
    }
}
