using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class CombatLabModifierBenchmarkRunnerTests
{
    [Fact]
    public void RunSuite_WithSameSeed_IsDeterministic()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["tower_a"] = new TowerDef("Tower A", BaseDamage: 20f, AttackInterval: 0.60f, Range: 290f),
        };
        var suite = BuildSuite(seed: 2468);

        var runner = new CombatLabModifierBenchmarkRunner(
            defs,
            modifierFactoryOverride: CreateModifier);
        CombatLabModifierBenchmarkReport first = runner.RunSuite(suite);
        CombatLabModifierBenchmarkReport second = runner.RunSuite(suite);

        Assert.Equal(first.DeltaRows.Count, second.DeltaRows.Count);
        Assert.Equal(first.ModifierProfiles.Count, second.ModifierProfiles.Count);

        var firstRows = first.DeltaRows
            .OrderBy(r => r.ModifierId, StringComparer.Ordinal)
            .ThenBy(r => r.ContextId, StringComparer.Ordinal)
            .ThenBy(r => r.ScenarioId, StringComparer.Ordinal)
            .ToList();
        var secondRows = second.DeltaRows
            .OrderBy(r => r.ModifierId, StringComparer.Ordinal)
            .ThenBy(r => r.ContextId, StringComparer.Ordinal)
            .ThenBy(r => r.ScenarioId, StringComparer.Ordinal)
            .ToList();

        for (int i = 0; i < firstRows.Count; i++)
        {
            Assert.Equal(firstRows[i].ModifierId, secondRows[i].ModifierId);
            Assert.Equal(firstRows[i].DeltaDamagePct, secondRows[i].DeltaDamagePct, 3);
            Assert.Equal(firstRows[i].DeltaDpsPct, secondRows[i].DeltaDpsPct, 3);
        }
    }

    [Fact]
    public void RunSuite_ClassifiesStrongAndWeakModifiers()
    {
        var defs = new Dictionary<string, TowerDef>
        {
            ["tower_a"] = new TowerDef("Tower A", BaseDamage: 20f, AttackInterval: 0.60f, Range: 290f),
        };
        var suite = BuildSuite(seed: 777);

        CombatLabModifierBenchmarkReport report = new CombatLabModifierBenchmarkRunner(
            defs,
            modifierFactoryOverride: CreateModifier).RunSuite(suite);

        CombatLabModifierBenchmarkProfile strong = report.ModifierProfiles.First(p => p.ModifierId == "boost_mod");
        CombatLabModifierBenchmarkProfile weak = report.ModifierProfiles.First(p => p.ModifierId == "weak_mod");

        Assert.True(strong.AvgGainPct > weak.AvgGainPct);
        Assert.Contains("strong", strong.Classification.ToLowerInvariant());
        Assert.Contains("weak", weak.Classification.ToLowerInvariant());
    }

    private static CombatLabModifierBenchmarkSuite BuildSuite(int seed)
    {
        return new CombatLabModifierBenchmarkSuite
        {
            Name = "modifier_test_suite",
            Mode = "base_combat_only",
            Seed = seed,
            TrialsPerScenario = 3,
            TimestepSeconds = 0.05f,
            IncludeAllModifiers = false,
            IncludeAllTowers = false,
            Modifiers = new[] { "boost_mod", "weak_mod" },
            Contexts = new List<CombatLabModifierBenchmarkContext>
            {
                new()
                {
                    Id = "ctx_a",
                    Tower = "tower_a",
                    BaselineMods = Array.Empty<string>(),
                    Targeting = "first",
                    Cost = 100f,
                }
            },
            Scenarios = new List<CombatLabTowerBenchmarkScenario>
            {
                new()
                {
                    Id = "mixed_wave",
                    Name = "Mixed Wave",
                    Tags = new[] { "open_lane", "anti_swarm" },
                    DurationSeconds = 26f,
                    PathType = "open_lane",
                    PathLength = 1600f,
                    LaneWidth = 120f,
                    TowerPosition = new[] { 620f, 0f },
                    StopWhenResolved = true,
                    EnemyGroups = new List<CombatLabTowerBenchmarkEnemyGroup>
                    {
                        new() { Id = "basic", Count = 14, Hp = 90f, Speed = 120f, SpawnInterval = 0.24f },
                        new() { Id = "tank", Count = 3, Hp = 480f, Speed = 80f, SpawnInterval = 0.72f, StartTime = 0.9f },
                    },
                },
            },
        };
    }

    private static Modifier CreateModifier(string modifierId)
    {
        return modifierId switch
        {
            "boost_mod" => new DamageScaleModifier("boost_mod", 1.35f),
            "weak_mod" => new DamageScaleModifier("weak_mod", 0.80f),
            _ => throw new InvalidOperationException($"Unknown test modifier: {modifierId}"),
        };
    }

    private sealed class DamageScaleModifier : Modifier
    {
        private readonly float _factor;

        public DamageScaleModifier(string id, float factor)
        {
            ModifierId = id;
            _factor = factor;
        }

        public override void OnEquip(ITowerView tower)
        {
            tower.BaseDamage *= _factor;
        }
    }
}
