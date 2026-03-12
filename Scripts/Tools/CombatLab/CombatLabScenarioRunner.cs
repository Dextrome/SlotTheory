using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;
using SlotTheory.Modifiers;

namespace SlotTheory.Tools;

public sealed class CombatLabScenarioRunner
{
    private sealed class LabTower : ITowerView
    {
        public string TowerId { get; set; } = "arc_emitter";
        public bool AppliesMark { get; set; }
        public float BaseDamage { get; set; } = 36f;
        public float AttackInterval { get; set; } = 1f;
        public float Range { get; set; } = 280f;
        public int SplitCount { get; set; }
        public int ChainCount { get; set; }
        public float ChainRange { get; set; } = 320f;
        public float ChainDamageDecay { get; set; } = 0.6f;
        public bool IsChainTower => ChainCount > 0;
        public TargetingMode TargetingMode { get; set; } = TargetingMode.First;
        public List<Modifier> Modifiers { get; } = new();
        public bool CanAddModifier => true;
        public float Cooldown { get; set; }
        public Vector2 GlobalPosition { get; set; } = Vector2.Zero;
        public void RefreshRangeCircle() { }
        public float GetEffectiveDamageForPreview() => BaseDamage;
    }

    private sealed class LabEnemy : IEnemyView
    {
        public int Id { get; set; }
        public float Hp { get; set; }
        public float ProgressRatio { get; set; }
        public Vector2 GlobalPosition { get; set; }
        public bool IsMarked => MarkedRemaining > 0f;
        public float MarkedRemaining { get; set; }
        public float SlowSpeedFactor { get; set; } = 1f;
        public float SlowRemaining { get; set; }
        public float DamageAmpRemaining { get; set; }
        public float DamageAmpMultiplier { get; set; }

        public string StatusTags()
        {
            var tags = new List<string>(capacity: 3);
            if (IsMarked) tags.Add("mark");
            if (SlowRemaining > 0f) tags.Add("slow");
            if (DamageAmpRemaining > 0f) tags.Add("amp");
            return string.Join(",", tags);
        }
    }

    public List<CombatLabScenarioResult> RunSuite(CombatLabScenarioSuite suite)
    {
        var results = new List<CombatLabScenarioResult>(suite.Scenarios.Count);
        foreach (CombatLabScenario scenario in suite.Scenarios)
            results.Add(RunScenario(scenario));
        return results;
    }

    public CombatLabScenarioResult RunScenario(CombatLabScenario scenario)
    {
        var result = new CombatLabScenarioResult
        {
            Name = scenario.Name,
            Passed = true,
        };

        string type = (scenario.ScenarioType ?? string.Empty).Trim().ToLowerInvariant();
        switch (type)
        {
            case "overkill_bloom":
                RunOverkillBloomScenario(scenario, result);
                break;
            case "global_surge":
                RunGlobalSurgeScenario(scenario, result);
                break;
            case "status_detonation":
            default:
                RunStatusDetonationScenario(scenario, result);
                break;
        }

        ValidateExpectations(scenario, result);
        result.Passed = result.Failures.Count == 0;
        return result;
    }

    private static void RunOverkillBloomScenario(CombatLabScenario scenario, CombatLabScenarioResult result)
    {
        var tower = ResolveTower(scenario);
        var enemies = ResolveEnemies(scenario);
        if (enemies.Count == 0)
            return;

        var primary = enemies[0];
        float hpBefore = primary.Hp;
        float dealt = SpectacleDamageCore.ApplyRawDamage(primary, scenario.TriggerDamage);
        bool killed = primary.Hp <= 0f;
        float overflow = killed ? MathF.Max(0f, scenario.TriggerDamage - hpBefore) : 0f;
        OverkillBloomProfile profile = SpectacleExplosionCore.BuildOverkillBloomProfile(overflow);

        result.Metrics.MajorSurges = 1;
        result.Trace.Add(new CombatLabTraceEvent
        {
            Timestamp = 0f,
            EventType = "primary_hit",
            EnemyId = primary.Id,
            X = primary.GlobalPosition.X,
            Y = primary.GlobalPosition.Y,
            HpBefore = hpBefore,
            HpAfter = primary.Hp,
            ExplosionStageId = "stage_one",
            StatusTags = primary.StatusTags(),
            SurgeTriggerId = "surge_1",
            ComboSkin = "default",
        });

        if (!profile.ShouldTrigger)
            return;

        result.Trace.Add(new CombatLabTraceEvent
        {
            Timestamp = 0f,
            EventType = "overkill_bloom_triggered",
            EnemyId = primary.Id,
            X = primary.GlobalPosition.X,
            Y = primary.GlobalPosition.Y,
            HpBefore = hpBefore,
            HpAfter = primary.Hp,
            ExplosionStageId = "stage_one",
            SurgeTriggerId = "surge_1",
            ComboSkin = "default",
        });

        if (SpectacleExplosionCore.ShouldEmitSecondStage(major: true, profile.BloomPower))
        {
            result.Trace.Add(new CombatLabTraceEvent
            {
                Timestamp = SpectacleExplosionCore.TwoStageBlastDelaySeconds,
                EventType = "overkill_stage_two",
                EnemyId = primary.Id,
                X = primary.GlobalPosition.X,
                Y = primary.GlobalPosition.Y,
                ExplosionStageId = "stage_two",
                SurgeTriggerId = "surge_1",
                ComboSkin = "default",
            });
        }

        var targets = enemies
            .Skip(1)
            .Where(e => e.Hp > 0f)
            .Where(e => primary.GlobalPosition.DistanceTo(e.GlobalPosition) <= profile.VisualRadius)
            .OrderBy(e => primary.GlobalPosition.DistanceTo(e.GlobalPosition))
            .Take(profile.MaxTargets)
            .ToList();

        for (int i = 0; i < targets.Count; i++)
        {
            LabEnemy enemy = targets[i];
            float distance = MathF.Max(1f, primary.GlobalPosition.DistanceTo(enemy.GlobalPosition));
            float distT = Math.Clamp(distance / profile.VisualRadius, 0f, 1f);
            float falloff = Mathf.Lerp(1f, 0.35f, distT);
            float damage = profile.BloomDamage * falloff;

            float before = enemy.Hp;
            float bloomDealt = SpectacleDamageCore.ApplyRawDamage(enemy, damage);
            result.Metrics.ExplosionHits += 1;
            result.Metrics.ExplosionDamage += bloomDealt;

            result.Trace.Add(new CombatLabTraceEvent
            {
                Timestamp = 0.01f + i * 0.005f,
                EventType = "overkill_bloom_hit",
                EnemyId = enemy.Id,
                X = enemy.GlobalPosition.X,
                Y = enemy.GlobalPosition.Y,
                HpBefore = before,
                HpAfter = enemy.Hp,
                ExplosionStageId = "bloom",
                SurgeTriggerId = "surge_1",
                ComboSkin = "default",
                StatusTags = enemy.StatusTags(),
            });
        }
    }

    private static void RunStatusDetonationScenario(CombatLabScenario scenario, CombatLabScenarioResult result)
    {
        var tower = ResolveTower(scenario);
        var enemies = ResolveEnemies(scenario);
        if (enemies.Count == 0)
            return;

        bool reducedMotion = scenario.ReducedMotion;
        int maxTargets = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(scenario.GlobalSurge, reducedMotion);
        float stagger = SpectacleExplosionCore.ResolveStatusDetonationStaggerSeconds(reducedMotion);
        ComboExplosionSkin skin = ResolveSkinFromTowerMods(scenario);
        Vector2 origin = tower.GlobalPosition;

        var statusTargets = enemies
            .Where(e => e.Hp > 0f)
            .Where(e => e.IsMarked || e.SlowRemaining > 0f || e.DamageAmpRemaining > 0f)
            .OrderBy(e => origin.DistanceTo(e.GlobalPosition))
            .ThenByDescending(e => e.ProgressRatio)
            .Take(maxTargets)
            .ToList();

        result.Metrics.MajorSurges = 1;
        result.Metrics.StatusDetonations = statusTargets.Count;

        var perTimestamp = new Dictionary<int, int>();
        for (int i = 0; i < statusTargets.Count; i++)
        {
            LabEnemy enemy = statusTargets[i];
            float timestamp = i * stagger;
            int stampMs = (int)MathF.Round(timestamp * 1000f);
            perTimestamp.TryGetValue(stampMs, out int currentAtStamp);
            perTimestamp[stampMs] = currentAtStamp + 1;

            float damage = tower.BaseDamage
                * (scenario.GlobalSurge ? 0.22f : 0.16f)
                * Math.Clamp(scenario.SurgePower, 0.6f, 2.2f)
                * MathF.Max(0.52f, 1f - i * 0.04f);

            float hpBefore = enemy.Hp;
            float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, damage);
            result.Metrics.ExplosionHits += 1;
            result.Metrics.ExplosionDamage += dealt;

            result.Trace.Add(new CombatLabTraceEvent
            {
                Timestamp = timestamp,
                EventType = "status_detonation_hit",
                EnemyId = enemy.Id,
                X = enemy.GlobalPosition.X,
                Y = enemy.GlobalPosition.Y,
                HpBefore = hpBefore,
                HpAfter = enemy.Hp,
                ExplosionStageId = "detonation",
                SurgeTriggerId = "surge_1",
                ComboSkin = skin.ToString(),
                StatusTags = enemy.StatusTags(),
            });

            ExplosionResidueProfile residue = SpectacleExplosionCore.ResolveResidueProfile(
                skin,
                scenario.GlobalSurge,
                scenario.SurgePower,
                i);
            if (residue.ShouldSpawn)
            {
                result.Metrics.ResidueCount += 1;
                result.Metrics.ResidueUptimeSeconds += residue.DurationSeconds;

                result.Trace.Add(new CombatLabTraceEvent
                {
                    Timestamp = timestamp,
                    EventType = "residue_spawned",
                    EnemyId = enemy.Id,
                    X = enemy.GlobalPosition.X,
                    Y = enemy.GlobalPosition.Y,
                    HpBefore = enemy.Hp,
                    HpAfter = enemy.Hp,
                    ExplosionStageId = "residue",
                    SurgeTriggerId = "surge_1",
                    ComboSkin = skin.ToString(),
                    ResidueSpawned = true,
                    StatusTags = enemy.StatusTags(),
                });
            }
        }

        if (perTimestamp.Count > 0)
            result.Metrics.SimultaneousDetonationPeak = perTimestamp.Values.Max();
    }

    private static void RunGlobalSurgeScenario(CombatLabScenario scenario, CombatLabScenarioResult result)
    {
        var tower = ResolveTower(scenario);
        var enemies = ResolveEnemies(scenario);
        if (enemies.Count == 0)
            return;

        Vector2 center = tower.GlobalPosition;
        int contributors = Math.Max(1, scenario.Contributors);

        result.Metrics.MajorSurges = 1;
        foreach (LabEnemy enemy in enemies.OrderBy(e => center.DistanceTo(e.GlobalPosition)))
        {
            float distance = center.DistanceTo(enemy.GlobalPosition);
            GlobalSurgeWaveTiming timing = SpectacleExplosionCore.ResolveGlobalSurgeWaveTiming(
                distance,
                contributors,
                scenario.ReducedMotion);

            result.Trace.Add(new CombatLabTraceEvent
            {
                Timestamp = timing.PreFlashDelay,
                EventType = "global_preflash",
                EnemyId = enemy.Id,
                X = enemy.GlobalPosition.X,
                Y = enemy.GlobalPosition.Y,
                HpBefore = enemy.Hp,
                HpAfter = enemy.Hp,
                SurgeTriggerId = "global_1",
                ExplosionStageId = "preflash",
                ComboSkin = "global",
            });
            result.Trace.Add(new CombatLabTraceEvent
            {
                Timestamp = timing.ImpactDelay,
                EventType = "global_impact",
                EnemyId = enemy.Id,
                X = enemy.GlobalPosition.X,
                Y = enemy.GlobalPosition.Y,
                HpBefore = enemy.Hp,
                HpAfter = enemy.Hp,
                SurgeTriggerId = "global_1",
                ExplosionStageId = "impact",
                ComboSkin = "global",
            });
        }
    }

    private static void ValidateExpectations(CombatLabScenario scenario, CombatLabScenarioResult result)
    {
        CombatLabExpectations expect = scenario.Expect ?? new CombatLabExpectations();
        if (expect.MajorSurges.HasValue && result.Metrics.MajorSurges != expect.MajorSurges.Value)
            result.Failures.Add($"Expected major_surges={expect.MajorSurges.Value}, got {result.Metrics.MajorSurges}.");

        if (expect.ExplosionHitsMin.HasValue && result.Metrics.ExplosionHits < expect.ExplosionHitsMin.Value)
            result.Failures.Add($"Expected explosion hits >= {expect.ExplosionHitsMin.Value}, got {result.Metrics.ExplosionHits}.");
        if (expect.ExplosionHitsMax.HasValue && result.Metrics.ExplosionHits > expect.ExplosionHitsMax.Value)
            result.Failures.Add($"Expected explosion hits <= {expect.ExplosionHitsMax.Value}, got {result.Metrics.ExplosionHits}.");

        if (expect.ResidueCount.HasValue && result.Metrics.ResidueCount != expect.ResidueCount.Value)
            result.Failures.Add($"Expected residue_count={expect.ResidueCount.Value}, got {result.Metrics.ResidueCount}.");

        if (expect.StatusDetonationsMin.HasValue && result.Metrics.StatusDetonations < expect.StatusDetonationsMin.Value)
            result.Failures.Add($"Expected status detonations >= {expect.StatusDetonationsMin.Value}, got {result.Metrics.StatusDetonations}.");
        if (expect.StatusDetonationsMax.HasValue && result.Metrics.StatusDetonations > expect.StatusDetonationsMax.Value)
            result.Failures.Add($"Expected status detonations <= {expect.StatusDetonationsMax.Value}, got {result.Metrics.StatusDetonations}.");

        if (expect.MaxSimultaneousDetonations.HasValue && result.Metrics.SimultaneousDetonationPeak > expect.MaxSimultaneousDetonations.Value)
        {
            result.Failures.Add(
                $"Expected simultaneous detonations <= {expect.MaxSimultaneousDetonations.Value}, got {result.Metrics.SimultaneousDetonationPeak}.");
        }

        if (expect.StageTwoDelayMinSeconds.HasValue || expect.StageTwoDelayMaxSeconds.HasValue)
        {
            CombatLabTraceEvent? stageOne = result.Trace.FirstOrDefault(e => e.EventType == "overkill_bloom_triggered");
            CombatLabTraceEvent? stageTwo = result.Trace.FirstOrDefault(e => e.EventType == "overkill_stage_two");
            if (stageOne == null || stageTwo == null)
            {
                result.Failures.Add("Expected overkill stage-two timing, but stage-one/stage-two events were missing.");
            }
            else
            {
                float delta = stageTwo.Timestamp - stageOne.Timestamp;
                if (expect.StageTwoDelayMinSeconds.HasValue && delta < expect.StageTwoDelayMinSeconds.Value)
                    result.Failures.Add($"Expected stage-two delay >= {expect.StageTwoDelayMinSeconds.Value:0.###}s, got {delta:0.###}s.");
                if (expect.StageTwoDelayMaxSeconds.HasValue && delta > expect.StageTwoDelayMaxSeconds.Value)
                    result.Failures.Add($"Expected stage-two delay <= {expect.StageTwoDelayMaxSeconds.Value:0.###}s, got {delta:0.###}s.");
            }
        }

        if (expect.ReducedMotionCollapsesDelays == true)
        {
            bool hasDelay = result.Trace
                .Where(e => e.EventType == "global_preflash" || e.EventType == "global_impact")
                .Any(e => e.Timestamp > 0.0001f);
            if (hasDelay)
                result.Failures.Add("Expected reduced-motion ripple timings to collapse to zero, but non-zero delays were recorded.");
        }

        if (expect.RippleMonotonic.HasValue)
        {
            Vector2 origin = Vector2.Zero;
            CombatLabTowerSetup? setup = scenario.TowerSetup.FirstOrDefault();
            if (setup != null && setup.Position.Length >= 2)
                origin = new Vector2(setup.Position[0], setup.Position[1]);

            var impacts = result.Trace
                .Where(e => e.EventType == "global_impact")
                .OrderBy(e => origin.DistanceTo(new Vector2(e.X, e.Y)))
                .ToList();

            bool monotonic = true;
            for (int i = 1; i < impacts.Count; i++)
            {
                if (impacts[i].Timestamp + 0.0001f < impacts[i - 1].Timestamp)
                {
                    monotonic = false;
                    break;
                }
            }

            if (expect.RippleMonotonic.Value != monotonic)
                result.Failures.Add($"Expected ripple monotonic={expect.RippleMonotonic.Value}, got {monotonic}.");
        }
    }

    private static LabTower ResolveTower(CombatLabScenario scenario)
    {
        CombatLabTowerSetup setup = scenario.TowerSetup.FirstOrDefault() ?? new CombatLabTowerSetup();
        var tower = new LabTower
        {
            TowerId = setup.Tower,
            BaseDamage = setup.BaseDamage,
            AttackInterval = setup.AttackInterval,
            Range = setup.Range,
            AppliesMark = setup.Mods.Any(m => string.Equals(m, SpectacleDefinitions.ExploitWeakness, StringComparison.Ordinal)),
        };

        if (setup.Position.Length >= 2)
            tower.GlobalPosition = new Vector2(setup.Position[0], setup.Position[1]);
        return tower;
    }

    private static List<LabEnemy> ResolveEnemies(CombatLabScenario scenario)
    {
        var enemies = new List<LabEnemy>();
        int nextId = 1;
        foreach (CombatLabEnemySetup setup in scenario.Enemies)
        {
            Vector2 pos = setup.Position != null && setup.Position.Length >= 2
                ? new Vector2(setup.Position[0], setup.Position[1])
                : new Vector2(setup.PathT * 1000f, 0f);
            var enemy = new LabEnemy
            {
                Id = setup.Id ?? nextId++,
                Hp = setup.Hp,
                ProgressRatio = setup.PathT,
                GlobalPosition = pos,
                SlowSpeedFactor = 1f,
            };

            foreach (string rawStatus in setup.Status)
            {
                string status = (rawStatus ?? string.Empty).Trim().ToLowerInvariant();
                if (status is "mark" or "marked")
                {
                    enemy.MarkedRemaining = 3f;
                    continue;
                }
                if (status is "slow" or "chill")
                {
                    enemy.SlowRemaining = 3f;
                    enemy.SlowSpeedFactor = 0.7f;
                    continue;
                }
                if (status is "amp" or "vulnerability")
                {
                    enemy.DamageAmpRemaining = 2f;
                    enemy.DamageAmpMultiplier = 0.1f;
                }
            }

            enemies.Add(enemy);
        }

        return enemies;
    }

    private static ComboExplosionSkin ResolveSkinFromTowerMods(CombatLabScenario scenario)
    {
        CombatLabTowerSetup setup = scenario.TowerSetup.FirstOrDefault() ?? new CombatLabTowerSetup();
        if (setup.Mods.Length >= 2)
            return SpectacleExplosionCore.ResolveComboExplosionSkin(setup.Mods[0], setup.Mods[1]);

        return ComboExplosionSkin.ChainArc;
    }
}
