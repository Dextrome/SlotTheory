using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Modifiers;
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

    /// <summary>
    /// Creates modifiers without DataLoader so benchmark tests can run without the Godot JSON pipeline.
    /// Uses a stub ModifierDef (id only; no params needed for stat-default modifiers).
    /// </summary>
    private static Func<string, Modifier> StubModifierFactory() => id =>
    {
        var def = new ModifierDef(id, id, string.Empty);
        return id switch
        {
            "momentum"         => new Momentum(def),
            "overkill"         => new Overkill(def),
            "exploit_weakness" => new ExploitWeakness(def),
            "focus_lens"       => new FocusLens(def),
            "slow"             => new Slow(def),
            "overreach"        => new Overreach(def),
            "hair_trigger"     => new HairTrigger(def),
            "split_shot"       => new SplitShot(def),
            "feedback_loop"    => new FeedbackLoop(def),
            "chain_reaction"   => new ChainReaction(def),
            "blast_core"       => new BlastCore(def),
            "wildfire"         => new Wildfire(def),
            "afterimage"       => new Afterimage(def),
            "reaper_protocol"  => new ReaperProtocol(def),
            _ => throw new InvalidOperationException($"Unknown modifier id in test stub: '{id}'"),
        };
    };

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

    [Fact]
    public void PhaseSplitter_SingleTarget_DoesNotDoubleHit()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["phase_splitter"] = new TowerDef("Phase Splitter", BaseDamage: 20f, AttackInterval: 0.95f, Range: 275f),
        };

        var suite = new CombatLabTowerBenchmarkSuite
        {
            Name = "phase_single_target",
            Seed = 1337,
            TrialsPerScenario = 1,
            IncludeAllTowers = false,
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { Tower = "phase_splitter", Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "single_enemy",
                    Name = "Single Enemy",
                    DurationSeconds = 4f,
                    PathLength = 1200f,
                    TowerPosition = new[] { 100f, 0f },
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "solo", Count = 1, Hp = 500f, Speed = 40f, SpawnInterval = 0.1f },
                    },
                },
            },
        };

        var report = new CombatLabTowerBenchmarkRunner(defs).RunSuite(suite);
        var row = report.ScenarioResults.Single().Results.Single();
        Assert.InRange(row.AverageTargetsHit, 0.95f, 1.05f);
    }

    [Fact]
    public void PhaseSplitter_TwoEndpoints_HitsBothTargets()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["phase_splitter"] = new TowerDef("Phase Splitter", BaseDamage: 20f, AttackInterval: 0.95f, Range: 275f),
        };

        var suite = new CombatLabTowerBenchmarkSuite
        {
            Name = "phase_two_targets",
            Seed = 7331,
            TrialsPerScenario = 1,
            IncludeAllTowers = false,
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { Tower = "phase_splitter", Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "two_enemies",
                    Name = "Two Enemies",
                    DurationSeconds = 4f,
                    PathLength = 1200f,
                    TowerPosition = new[] { 100f, 0f },
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "pair", Count = 2, Hp = 500f, Speed = 40f, SpawnInterval = 0.1f },
                    },
                },
            },
        };

        var report = new CombatLabTowerBenchmarkRunner(defs).RunSuite(suite);
        var row = report.ScenarioResults.Single().Results.Single();
        Assert.True(row.AverageTargetsHit >= 1.75f);
    }

    [Fact]
    public void PhaseSplitter_WithSplitShot_ExpandsToFourHitPattern()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["phase_splitter"] = new TowerDef("Phase Splitter", BaseDamage: 20f, AttackInterval: 0.95f, Range: 275f),
        };

        var suite = new CombatLabTowerBenchmarkSuite
        {
            Name = "phase_split_pattern",
            Seed = 9898,
            TrialsPerScenario = 1,
            IncludeAllTowers = false,
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { Tower = "phase_splitter", Mods = new[] { "split_shot" }, Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "four_enemies",
                    Name = "Four Enemies",
                    DurationSeconds = 4f,
                    PathLength = 1200f,
                    TowerPosition = new[] { 100f, 0f },
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "pack", Count = 4, Hp = 700f, Speed = 35f, SpawnInterval = 0.08f },
                    },
                },
            },
        };

        var report = new CombatLabTowerBenchmarkRunner(defs, StubModifierFactory()).RunSuite(suite);
        var row = report.ScenarioResults.Single().Results.Single();
        // ~3.4 expected: first shot fires with 1 enemy active (warm-up), subsequent shots hit
        // all 4 (front primary + back primary + 1 split each). 3.0 threshold clears baseline ~1.8.
        Assert.True(row.AverageTargetsHit >= 3.0f);
    }

    [Fact]
    public void PhaseSplitter_BlastCore_UsesBothPrimaryAnchors()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["phase_splitter"] = new TowerDef("Phase Splitter", BaseDamage: 20f, AttackInterval: 0.95f, Range: 275f),
        };

        var suite = new CombatLabTowerBenchmarkSuite
        {
            Name = "phase_blast_core",
            Seed = 7788,
            TrialsPerScenario = 1,
            IncludeAllTowers = false,
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { CaseId = "base", Tower = "phase_splitter", Targeting = "first" },
                new() { CaseId = "blast", Tower = "phase_splitter", Mods = new[] { "blast_core" }, Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "paired_targets",
                    Name = "Paired Targets",
                    DurationSeconds = 4f,
                    PathLength = 1200f,
                    TowerPosition = new[] { 100f, 0f },
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "pair", Count = 2, Hp = 1200f, Speed = 30f, SpawnInterval = 0.10f },
                    },
                },
            },
        };

        var report = new CombatLabTowerBenchmarkRunner(defs, StubModifierFactory()).RunSuite(suite);
        var rows = report.ScenarioResults.Single().Results.ToDictionary(r => r.CaseId);
        Assert.True(rows["blast"].TotalDamage > rows["base"].TotalDamage * 1.10f);
    }

    [Fact]
    public void Undertow_ChainReactionCopies_IncreaseSecondaryTargets()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["undertow_engine"] = new TowerDef("Undertow Engine", BaseDamage: 8f, AttackInterval: 0.75f, Range: 300f),
        };

        var suite = new CombatLabTowerBenchmarkSuite
        {
            Name = "undertow_chain_scaling",
            Seed = 9412,
            TrialsPerScenario = 1,
            IncludeAllTowers = false,
            Towers = new List<CombatLabTowerBenchmarkTowerSetup>
            {
                new() { CaseId = "chain1", Tower = "undertow_engine", Mods = new[] { "chain_reaction" }, Targeting = "first" },
                new() { CaseId = "chain2", Tower = "undertow_engine", Mods = new[] { "chain_reaction", "chain_reaction" }, Targeting = "first" },
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "dense_pack",
                    Name = "Dense Pack",
                    DurationSeconds = 8f,
                    PathLength = 4200f,
                    LaneWidth = 120f,
                    TowerPosition = new[] { 320f, 0f },
                    StopWhenResolved = false,
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "pack", Count = 12, Hp = 1600f, Speed = 34f, SpawnInterval = 0.06f },
                    },
                },
            },
        };

        var report = new CombatLabTowerBenchmarkRunner(defs, StubModifierFactory()).RunSuite(suite);
        var rows = report.ScenarioResults.Single().Results.ToDictionary(r => r.CaseId);
        float chain1 = rows["chain1"].AverageTargetsHit;
        float chain2 = rows["chain2"].AverageTargetsHit;

        Assert.True(chain2 > chain1,
            $"Expected chain2 targets/shot to exceed chain1, but chain1={chain1:F2}, chain2={chain2:F2}.");
    }
}
