using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Data;
using SlotTheory.Entities;
using SlotTheory.Modifiers;

namespace SlotTheory.Tools;

public sealed class CombatLabTowerBenchmarkRunner
{
    private sealed class BenchmarkTower : ITowerView
    {
        public string TowerId { get; set; } = string.Empty;
        public bool AppliesMark { get; set; }
        public float BaseDamage { get; set; }
        public float AttackInterval { get; set; }
        public float Range { get; set; }
        public int SplitCount { get; set; }
        public int ChainCount { get; set; }
        public float ChainRange { get; set; } = 400f;
        public float ChainDamageDecay { get; set; } = Balance.ChainDamageDecay;
        public bool IsChainTower => ChainCount > 0;
        public TargetingMode TargetingMode { get; set; } = TargetingMode.First;
        public List<Modifier> Modifiers { get; } = new();
        public bool CanAddModifier => true;
        public float Cooldown { get; set; }
        public Vector2 GlobalPosition { get; set; }

        public void RefreshRangeCircle() { }
        public float GetEffectiveDamageForPreview() => BaseDamage;
    }

    private sealed class BenchmarkEnemy : IEnemyView
    {
        public int Id { get; init; }
        public string GroupId { get; init; } = string.Empty;
        public bool IsTank { get; init; }
        public bool IsSwarm { get; init; }
        public float MaxHp { get; init; }
        public float BaseSpeed { get; init; }
        public float PathLength { get; init; }
        public float LaneOffsetY { get; init; }
        public float SpawnTime { get; init; }
        public bool Spawned { get; set; }
        public bool Resolved { get; set; }
        public bool Killed { get; set; }
        public float PathDistance { get; set; }
        public float Hp { get; set; }
        public float ProgressRatio => PathLength <= 0.0001f ? 0f : Mathf.Clamp(PathDistance / PathLength, 0f, 1f);
        public Vector2 GlobalPosition => new(PathDistance, LaneOffsetY);
        public bool IsMarked => MarkedRemaining > 0f;
        public float MarkedRemaining { get; set; }
        public float SlowSpeedFactor { get; set; } = 1f;
        public float SlowRemaining { get; set; }
        public float DamageAmpRemaining { get; set; }
        public float DamageAmpMultiplier { get; set; }
        public bool IsShieldProtected { get; set; }
        public float BurnRemaining { get; set; }
        public float BurnDamagePerSecond { get; set; }
        public int BurnOwnerSlotIndex { get; set; } = -1;
        public float BurnTrailDropTimer { get; set; }
    }

    private sealed class ResidueZone
    {
        public ExplosionResidueKind Kind { get; init; }
        public Vector2 Origin { get; init; }
        public float Radius { get; init; }
        public float RemainingSeconds { get; set; }
        public float TickIntervalSeconds { get; init; }
        public float TickRemainingSeconds { get; set; }
        public float Potency { get; init; }
    }

    private sealed class WildfireTrail
    {
        public Vector2 Position { get; init; }
        public float RemainingSeconds { get; set; }
        public float DamagePerSecond { get; init; }
    }

    private sealed class BenchmarkUndertowEffect
    {
        public BenchmarkTower Tower { get; init; } = null!;
        public BenchmarkEnemy Target { get; init; } = null!;
        public float TotalDuration { get; init; }
        public float Remaining { get; set; }
        public float TotalPullDistance { get; init; }
        public float PulledDistance { get; set; }
        public float SlowFactor { get; init; }
        public bool IsSecondary { get; init; }
        public bool IsFollowup { get; init; }
        public bool EnableEndpointPulse { get; init; }
        public float DelayRemaining { get; set; }
    }

    private sealed class TrialMetrics
    {
        public float TotalDamage;
        public float RawDamage;
        public float OverkillWaste;
        public float TankDamage;
        public float SwarmDamage;
        public float TankHpTotal;
        public float SwarmHpTotal;
        public float LeakedHp;
        public float TotalHp;
        public int Hits;
        public int Shots;
        public int TargetsHitTotal;
        public int EligibleSteps;
        public int FiredSteps;
        public int IdleSteps;
        public int Leaks;
        public int Kills;
        public float WaveClearSeconds;
        public bool Cleared;
        public float SimulatedSeconds;
        public int SurgesTriggered;
        public int GlobalSurgesTriggered;
        public bool EdgeSpike;
    }

    private sealed class TowerCase
    {
        public string CaseId { get; init; } = string.Empty;
        public string TowerId { get; init; } = string.Empty;
        public string[] Mods { get; init; } = Array.Empty<string>();
        public TargetingMode TargetingMode { get; init; } = TargetingMode.First;
        public float? BaseDamageOverride { get; init; }
        public float? AttackIntervalOverride { get; init; }
        public float? RangeOverride { get; init; }
        public int? SplitCountOverride { get; init; }
        public int? ChainCountOverride { get; init; }
        public float Cost { get; init; } = 1f;
    }

    private readonly IReadOnlyDictionary<string, TowerDef>? _towerDefsOverride;
    private readonly Func<string, Modifier>? _modifierFactoryOverride;

    public CombatLabTowerBenchmarkRunner(
        IReadOnlyDictionary<string, TowerDef>? towerDefsOverride = null,
        Func<string, Modifier>? modifierFactoryOverride = null)
    {
        _towerDefsOverride = towerDefsOverride;
        _modifierFactoryOverride = modifierFactoryOverride;
    }

    public CombatLabTowerBenchmarkReport RunSuite(CombatLabTowerBenchmarkSuite suite)
    {
        if (suite.Scenarios.Count == 0)
            throw new InvalidOperationException("Tower benchmark suite has no scenarios.");

        Dictionary<string, TowerDef> towerDefs = ResolveTowerDefinitions();
        List<TowerCase> towerCases = ResolveTowerCases(suite, towerDefs);
        if (towerCases.Count == 0)
            throw new InvalidOperationException("Tower benchmark suite resolved zero towers to evaluate.");

        List<CombatLabTowerBenchmarkScenarioResult> scenarioResults = new();
        foreach (CombatLabTowerBenchmarkScenario scenario in suite.Scenarios)
        {
            var scenarioResult = new CombatLabTowerBenchmarkScenarioResult
            {
                ScenarioId = scenario.Id,
                ScenarioName = scenario.Name,
                PathType = scenario.PathType,
                Tags = scenario.Tags ?? Array.Empty<string>(),
            };

            foreach (TowerCase towerCase in towerCases)
            {
                CombatLabTowerBenchmarkTowerResult towerResult = RunTowerScenario(
                    suite,
                    scenario,
                    towerCase,
                    towerDefs[towerCase.TowerId]);
                scenarioResult.Results.Add(towerResult);
            }

            ApplyScenarioNormalization(scenarioResult, suite, towerCases);
            scenarioResults.Add(scenarioResult);
        }

        List<CombatLabTowerBenchmarkProfile> towerProfiles = BuildTowerProfiles(suite, towerCases, scenarioResults);
        List<CombatLabTowerBenchmarkSuggestion> suggestions = BuildSuggestions(suite, towerProfiles);

        return new CombatLabTowerBenchmarkReport
        {
            Suite = suite.Name,
            Mode = suite.Mode,
            GeneratedUtc = DateTime.UtcNow,
            ScenarioResults = scenarioResults,
            TowerProfiles = towerProfiles,
            TuningSuggestions = suggestions,
        };
    }

    public static string BuildScenarioCsv(CombatLabTowerBenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "scenario_id,scenario_name,path_type,case_id,tower_id,cost,total_damage,effective_dps,cost_efficiency,leak_prevention_value,anti_swarm_performance,anti_tank_performance,reliability_variance,overkill_waste,average_targets_hit,uptime,idle_time_seconds,kill_count,leak_count,wave_clear_seconds,clear_rate,surges_triggered,global_surges_triggered,edge_spike_rate,normalized_global,normalized_cost_band,normalized_role");
        foreach (CombatLabTowerBenchmarkScenarioResult scenario in report.ScenarioResults)
        {
            foreach (CombatLabTowerBenchmarkTowerResult row in scenario.Results.OrderBy(r => r.CaseId, StringComparer.Ordinal))
            {
                sb.AppendLine(string.Join(",",
                    Csv(scenario.ScenarioId),
                    Csv(scenario.ScenarioName),
                    Csv(scenario.PathType),
                    Csv(row.CaseId),
                    Csv(row.TowerId),
                    Csv(row.Cost),
                    Csv(row.TotalDamage),
                    Csv(row.EffectiveDps),
                    Csv(row.CostEfficiency),
                    Csv(row.LeakPreventionValue),
                    Csv(row.AntiSwarmPerformance),
                    Csv(row.AntiTankPerformance),
                    Csv(row.ReliabilityVariance),
                    Csv(row.OverkillWaste),
                    Csv(row.AverageTargetsHit),
                    Csv(row.Uptime),
                    Csv(row.IdleTimeSeconds),
                    Csv(row.KillCount),
                    Csv(row.LeakCount),
                    Csv(row.WaveClearSeconds),
                    Csv(row.ClearRate),
                    Csv(row.SurgesTriggered),
                    Csv(row.GlobalSurgesTriggered),
                    Csv(row.EdgeSpikeRate),
                    Csv(row.NormalizedGlobal),
                    Csv(row.NormalizedCostBand),
                    Csv(row.NormalizedRole)));
            }
        }

        return sb.ToString();
    }

    public static string BuildTowerProfileCsv(CombatLabTowerBenchmarkReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "case_id,tower_id,cost,cost_band,roles,scenario_count,avg_normalized_global,avg_normalized_cost_band,avg_normalized_role,map_path_sensitivity,aggregate_reliability_variance,flags");
        foreach (CombatLabTowerBenchmarkProfile profile in report.TowerProfiles.OrderBy(p => p.CaseId, StringComparer.Ordinal))
        {
            sb.AppendLine(string.Join(",",
                Csv(profile.CaseId),
                Csv(profile.TowerId),
                Csv(profile.Cost),
                Csv(profile.CostBand),
                Csv(string.Join("|", profile.Roles ?? Array.Empty<string>())),
                Csv(profile.ScenarioCount),
                Csv(profile.AvgNormalizedGlobal),
                Csv(profile.AvgNormalizedCostBand),
                Csv(profile.AvgNormalizedRole),
                Csv(profile.MapPathSensitivity),
                Csv(profile.AggregateReliabilityVariance),
                Csv(string.Join("|", profile.Flags))));
        }

        return sb.ToString();
    }

    private CombatLabTowerBenchmarkTowerResult RunTowerScenario(
        CombatLabTowerBenchmarkSuite suite,
        CombatLabTowerBenchmarkScenario scenario,
        TowerCase towerCase,
        TowerDef towerDef)
    {
        int trials = Math.Max(1, suite.TrialsPerScenario);
        float dt = Mathf.Clamp(suite.TimestepSeconds <= 0f ? 0.05f : suite.TimestepSeconds, 0.01f, 0.20f);
        var trialMetrics = new List<TrialMetrics>(trials);

        for (int trial = 0; trial < trials; trial++)
        {
            int seed = ComposeSeed(suite.Seed, scenario.Id, towerCase.CaseId, trial);
            TrialMetrics result = RunTrial(suite, scenario, towerCase, towerDef, dt, seed);
            trialMetrics.Add(result);
        }

        float avgTotalDamage = (float)trialMetrics.Average(t => t.TotalDamage);
        float avgDps = (float)trialMetrics.Average(t => t.SimulatedSeconds > 0.0001f ? t.TotalDamage / t.SimulatedSeconds : 0f);
        float avgLeakPrevention = (float)trialMetrics.Average(t => t.TotalHp > 0.0001f ? Mathf.Clamp(1f - (t.LeakedHp / t.TotalHp), 0f, 1f) : 1f);
        float avgSwarm = (float)trialMetrics.Average(t => t.SwarmHpTotal > 0.0001f ? t.SwarmDamage / t.SwarmHpTotal : 0f);
        float avgTank = (float)trialMetrics.Average(t => t.TankHpTotal > 0.0001f ? t.TankDamage / t.TankHpTotal : 0f);
        float avgWaste = (float)trialMetrics.Average(t => t.OverkillWaste);
        float avgTargetsHit = (float)trialMetrics.Average(t => t.Shots > 0 ? (float)t.TargetsHitTotal / t.Shots : 0f);
        float avgUptime = (float)trialMetrics.Average(t => t.EligibleSteps > 0 ? (float)t.FiredSteps / t.EligibleSteps : 0f);
        float avgIdleSeconds = (float)trialMetrics.Average(t => t.IdleSteps * dt);
        float avgKills = (float)trialMetrics.Average(t => t.Kills);
        float avgLeaks = (float)trialMetrics.Average(t => t.Leaks);
        float avgWaveClearSeconds = (float)trialMetrics.Average(t => t.WaveClearSeconds);
        float clearRate = (float)trialMetrics.Average(t => t.Cleared ? 1f : 0f);
        float avgSurges = (float)trialMetrics.Average(t => t.SurgesTriggered);
        float avgGlobalSurges = (float)trialMetrics.Average(t => t.GlobalSurgesTriggered);
        float edgeSpikeRate = (float)trialMetrics.Average(t => t.EdgeSpike ? 1f : 0f);
        float avgCostEfficiency = towerCase.Cost > 0.0001f ? avgTotalDamage / towerCase.Cost : avgTotalDamage;
        float reliabilityVariance = ComputeCoefficientOfVariation(trialMetrics.Select(t => t.TotalDamage).ToList());

        return new CombatLabTowerBenchmarkTowerResult
        {
            CaseId = towerCase.CaseId,
            TowerId = towerCase.TowerId,
            ScenarioId = scenario.Id,
            Cost = towerCase.Cost,
            TotalDamage = avgTotalDamage,
            EffectiveDps = avgDps,
            CostEfficiency = avgCostEfficiency,
            LeakPreventionValue = avgLeakPrevention,
            AntiSwarmPerformance = avgSwarm,
            AntiTankPerformance = avgTank,
            ReliabilityVariance = reliabilityVariance,
            OverkillWaste = avgWaste,
            AverageTargetsHit = avgTargetsHit,
            Uptime = avgUptime,
            IdleTimeSeconds = avgIdleSeconds,
            KillCount = avgKills,
            LeakCount = avgLeaks,
            WaveClearSeconds = avgWaveClearSeconds,
            ClearRate = clearRate,
            SurgesTriggered = avgSurges,
            GlobalSurgesTriggered = avgGlobalSurges,
            EdgeSpikeRate = edgeSpikeRate,
        };
    }

    private TrialMetrics RunTrial(
        CombatLabTowerBenchmarkSuite suite,
        CombatLabTowerBenchmarkScenario scenario,
        TowerCase towerCase,
        TowerDef towerDef,
        float dt,
        int seed)
    {
        var rng = new Random(seed);
        var metrics = new TrialMetrics();
        BenchmarkTower tower = BuildTower(scenario, towerCase, towerDef);
        List<BenchmarkEnemy> enemies = BuildEnemies(scenario, rng);
        foreach (BenchmarkEnemy enemy in enemies)
        {
            metrics.TotalHp += enemy.MaxHp;
            if (enemy.IsTank)
                metrics.TankHpTotal += enemy.MaxHp;
            if (enemy.IsSwarm)
                metrics.SwarmHpTotal += enemy.MaxHp;
        }

        bool liveRules = string.Equals((suite.Mode ?? string.Empty).Trim(), "live_rules", StringComparison.OrdinalIgnoreCase);
        SpectacleSystem? spectacle = liveRules ? new SpectacleSystem() : null;
        var residues = new List<ResidueZone>();
        var wildfireTrails = new List<WildfireTrail>();
        var activeUndertows = new List<BenchmarkUndertowEffect>();
        var undertowRecentRemaining = new Dictionary<int, float>();
        var undertowRetargetLockoutRemaining = new Dictionary<int, float>();
        bool pendingGlobal = false;
        float globalReadyAt = -1f;
        float simTime = 0f;
        int spawnedCount = 0;
        int maxLeaks = Math.Max(0, scenario.MaxLeaks);
        int totalEnemies = Math.Max(1, enemies.Count);

        if (spectacle != null)
        {
            spectacle.OnSurgeTriggered += info =>
            {
                metrics.SurgesTriggered++;
                ApplyLiveSurgeGameplay(tower, enemies, residues, info.Signature.SurgePower, globalSurge: false, metrics);
            };

            spectacle.OnGlobalSurgeReady += _ =>
            {
                pendingGlobal = true;
                globalReadyAt = simTime;
            };

            spectacle.OnGlobalTriggered += info =>
            {
                metrics.GlobalSurgesTriggered++;
                pendingGlobal = false;
                globalReadyAt = -1f;
                ApplyLiveGlobalGameplay(tower, enemies, residues, info.UniqueContributors, metrics);
            };
        }

        float duration = Math.Max(1f, scenario.DurationSeconds);
        while (simTime < duration)
        {
            simTime += dt;
            metrics.SimulatedSeconds += dt;

            foreach (BenchmarkEnemy enemy in enemies)
            {
                if (!enemy.Spawned && simTime + 0.0001f >= enemy.SpawnTime)
                {
                    enemy.Spawned = true;
                    spawnedCount++;
                }
            }

            UpdateEnemyStatuses(enemies, dt);
            UpdateActiveUndertows(activeUndertows, undertowRecentRemaining, undertowRetargetLockoutRemaining, enemies, dt);
            UpdateWildfireBurnAndTrails(wildfireTrails, enemies, tower, dt, metrics);
            UpdateResidues(residues, enemies, tower, dt, metrics);
            MoveEnemies(enemies, dt, metrics);
            MarkNewDeaths(enemies, metrics);

            List<BenchmarkEnemy> activeEnemies = enemies
                .Where(e => e.Spawned && !e.Resolved && e.Hp > 0f)
                .ToList();

            bool hasInRangeEnemy = activeEnemies.Any(e => e.GlobalPosition.DistanceTo(tower.GlobalPosition) <= tower.Range);
            if (hasInRangeEnemy)
                metrics.EligibleSteps++;

            if (tower.Cooldown > 0f)
                tower.Cooldown = Math.Max(0f, tower.Cooldown - dt);

            foreach (Modifier mod in tower.Modifiers)
                mod.Update(dt, tower);

            if (spectacle != null)
                spectacle.Update(dt);

            if (spectacle != null && pendingGlobal && spectacle.IsGlobalSurgeReady)
            {
                float readyAge = globalReadyAt >= 0f ? Mathf.Max(0f, simTime - globalReadyAt) : 0f;
                var snapshot = new BotGlobalSurgeSnapshot(
                    IsGlobalSurgeReady: spectacle.IsGlobalSurgeReady,
                    HasPendingGlobalSurge: pendingGlobal,
                    Lives: Math.Max(1, Balance.StartingLives - metrics.Leaks),
                    EnemiesAlive: activeEnemies.Count,
                    EnemiesSpawnedThisWave: spawnedCount,
                    TotalEnemiesThisWave: totalEnemies,
                    ReadyAgeSeconds: readyAge);
                if (BotGlobalSurgeAdvisor.ShouldActivate(snapshot))
                    spectacle.ActivateGlobalSurge();
            }

            bool firedThisStep = false;
            if (tower.Cooldown <= 0f)
            {
                if (tower.TowerId == "phase_splitter")
                {
                    var (front, back) = Targeting.SelectFirstAndLastTargets(tower, activeEnemies, ignoreRange: false);
                    if (front != null || back != null)
                    {
                        firedThisStep = true;
                        metrics.Shots++;
                        metrics.FiredSteps++;

                        float effectiveInterval = tower.AttackInterval;
                        foreach (Modifier mod in tower.Modifiers)
                            mod.ModifyAttackInterval(ref effectiveInterval, tower);
                        tower.Cooldown = Math.Max(0.01f, effectiveInterval);

                        if (spectacle != null)
                            spectacle.RegisterShotFired(tower);

                        int totalHitsThisShot = 0;
                        float primaryDamage = tower.BaseDamage * Balance.PhaseSplitterDamageRatio;
                        var phaseTargets = new List<BenchmarkEnemy>(2);
                        if (front != null) phaseTargets.Add(front);
                        if (back != null && !ReferenceEquals(front, back)) phaseTargets.Add(back);

                        foreach (BenchmarkEnemy phaseTarget in phaseTargets)
                        {
                            bool markedBefore = phaseTarget.IsMarked;
                            DamageContext primaryCtx = ApplyHitAndCollect(
                                tower,
                                phaseTarget,
                                activeEnemies,
                                metrics,
                                state: null,
                                isChain: false,
                                damageOverride: primaryDamage);
                            RegisterLiveProcForHit(spectacle, tower, markedBefore, phaseTarget, primaryCtx.DamageDealt);
                            totalHitsThisShot++;

                            int chainHits = CombatResolution.ApplyChainHits(
                                tower,
                                phaseTarget,
                                waveIndex: 0,
                                activeEnemies,
                                state: null,
                                onHit: ctx =>
                                {
                                    ApplyContextToMetrics(ctx, metrics);
                                    RegisterLiveProcForHit(spectacle, tower, ctx.Target.IsMarked, ctx.Target, ctx.DamageDealt);
                                });
                            if (chainHits > 0 && spectacle != null)
                            {
                                float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
                                spectacle.RegisterProc(tower, SpectacleDefinitions.ChainReaction,
                                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
                            }
                            totalHitsThisShot += chainHits;

                            int splitHits = ApplyPhaseSplitterSplitHits(
                                tower,
                                phaseTarget,
                                activeEnemies,
                                metrics,
                                spectacle);
                            totalHitsThisShot += splitHits;
                        }

                        metrics.TargetsHitTotal += totalHitsThisShot;
                        MarkNewDeaths(enemies, metrics);
                    }
                }
                else
                {
                    if (tower.TowerId == "undertow_engine")
                    {
                        BenchmarkEnemy? primaryTarget = SelectUndertowPrimaryTarget(
                            tower,
                            activeEnemies,
                            activeUndertows,
                            undertowRetargetLockoutRemaining);
                        if (primaryTarget != null)
                        {
                            firedThisStep = true;
                            metrics.Shots++;
                            metrics.FiredSteps++;

                            float effectiveInterval = tower.AttackInterval;
                            foreach (Modifier mod in tower.Modifiers)
                                mod.ModifyAttackInterval(ref effectiveInterval, tower);
                            tower.Cooldown = Math.Max(0.01f, effectiveInterval);

                            if (spectacle != null)
                                spectacle.RegisterShotFired(tower);

                            bool markedBefore = primaryTarget.IsMarked;
                            DamageContext primaryCtx = ApplyHitAndCollect(tower, primaryTarget, activeEnemies, metrics, state: null, isChain: false);
                            RegisterLiveProcForHit(spectacle, tower, markedBefore, primaryTarget, primaryCtx.DamageDealt);

                            int undertowTargets = 1;
                            bool startedPrimary = TryStartUndertowEffect(
                                tower,
                                primaryTarget,
                                strengthMultiplier: 1f,
                                isSecondary: false,
                                isFollowup: false,
                                enableEndpointPulse: true,
                                activeUndertows,
                                undertowRecentRemaining,
                                undertowRetargetLockoutRemaining);
                            if (startedPrimary)
                            {
                                undertowTargets += ApplyUndertowSecondaryTugs(
                                    tower,
                                    primaryTarget,
                                    activeEnemies,
                                    activeUndertows,
                                    undertowRecentRemaining,
                                    undertowRetargetLockoutRemaining);
                                ScheduleUndertowFeedbackFollowup(
                                    tower,
                                    primaryTarget,
                                    rng,
                                    activeUndertows,
                                    undertowRecentRemaining,
                                    undertowRetargetLockoutRemaining);
                            }

                            metrics.TargetsHitTotal += undertowTargets;
                            MarkNewDeaths(enemies, metrics);
                        }
                    }
                    else
                    {
                        BenchmarkEnemy? target = tower.TowerId == "rocket_launcher"
                            ? SelectRocketSplashTarget(tower, activeEnemies)
                            : Targeting.SelectTarget(tower, activeEnemies, ignoreRange: false);
                        if (target != null)
                        {
                            firedThisStep = true;
                            metrics.Shots++;
                            metrics.FiredSteps++;

                            float effectiveInterval = tower.AttackInterval;
                            foreach (Modifier mod in tower.Modifiers)
                                mod.ModifyAttackInterval(ref effectiveInterval, tower);
                            tower.Cooldown = Math.Max(0.01f, effectiveInterval);

                            if (spectacle != null)
                                spectacle.RegisterShotFired(tower);

                            bool targetMarkedBefore = target.IsMarked;
                            DamageContext primaryCtx = ApplyHitAndCollect(tower, target, activeEnemies, metrics, state: null, isChain: false);
                            RegisterLiveProcForHit(spectacle, tower, targetMarkedBefore, target, primaryCtx.DamageDealt);

                            int chainHits = CombatResolution.ApplyChainHits(
                                tower,
                                target,
                                waveIndex: 0,
                                activeEnemies,
                                state: null,
                                onHit: ctx =>
                                {
                                    ApplyContextToMetrics(ctx, metrics);
                                    RegisterLiveProcForHit(spectacle, tower, ctx.Target.IsMarked, ctx.Target, ctx.DamageDealt);
                                });
                            if (chainHits > 0 && spectacle != null)
                            {
                                float chainEventDamage = tower.BaseDamage * tower.ChainDamageDecay;
                                spectacle.RegisterProc(tower, SpectacleDefinitions.ChainReaction,
                                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChainReaction), chainEventDamage);
                            }

                            int splitHits = CombatResolution.ApplySplitHits(
                                tower,
                                target,
                                waveIndex: 0,
                                activeEnemies,
                                state: null,
                                onHit: ctx =>
                                {
                                    ApplyContextToMetrics(ctx, metrics);
                                    RegisterLiveProcForHit(spectacle, tower, ctx.Target.IsMarked, ctx.Target, ctx.DamageDealt);
                                });
                            if (splitHits > 0 && spectacle != null)
                            {
                                float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
                                spectacle.RegisterProc(tower, SpectacleDefinitions.SplitShot,
                                    SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
                            }

                            metrics.TargetsHitTotal += 1 + chainHits + splitHits;
                            MarkNewDeaths(enemies, metrics);
                        }
                    }
                }
            }

            if (!firedThisStep && tower.Cooldown <= 0f)
                metrics.IdleSteps++;

            if (metrics.Leaks > maxLeaks)
                break;

            if (scenario.StopWhenResolved)
            {
                bool allResolved = enemies.All(e => !e.Spawned || e.Resolved || e.Hp <= 0f);
                bool allSpawned = enemies.All(e => e.Spawned || simTime + 0.0001f < e.SpawnTime);
                if (allResolved && allSpawned)
                {
                    metrics.Cleared = true;
                    metrics.WaveClearSeconds = simTime;
                    break;
                }
            }
        }

        if (!metrics.Cleared)
        {
            bool allResolved = enemies.All(e => !e.Spawned || e.Resolved || e.Hp <= 0f);
            bool allSpawned = enemies.All(e => e.Spawned || simTime + 0.0001f < e.SpawnTime);
            metrics.Cleared = allResolved && allSpawned;
        }
        if (metrics.WaveClearSeconds <= 0.0001f)
            metrics.WaveClearSeconds = simTime;
        metrics.Kills = enemies.Count(e => e.Killed);

        return metrics;
    }

    private static void UpdateEnemyStatuses(List<BenchmarkEnemy> enemies, float dt)
    {
        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!enemy.Spawned || enemy.Resolved)
                continue;

            if (enemy.MarkedRemaining > 0f)
                enemy.MarkedRemaining = Math.Max(0f, enemy.MarkedRemaining - dt);

            if (enemy.SlowRemaining > 0f)
            {
                enemy.SlowRemaining = Math.Max(0f, enemy.SlowRemaining - dt);
                if (enemy.SlowRemaining <= 0f)
                    enemy.SlowSpeedFactor = 1f;
            }

            if (enemy.DamageAmpRemaining > 0f)
            {
                enemy.DamageAmpRemaining = Math.Max(0f, enemy.DamageAmpRemaining - dt);
                if (enemy.DamageAmpRemaining <= 0f)
                    enemy.DamageAmpMultiplier = 0f;
            }
        }
    }

    private static void UpdateWildfireBurnAndTrails(
        List<WildfireTrail> trails,
        List<BenchmarkEnemy> enemies,
        BenchmarkTower tower,
        float dt,
        TrialMetrics metrics)
    {
        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!enemy.Spawned || enemy.Resolved || enemy.Hp <= 0f)
                continue;
            if (enemy.BurnRemaining <= 0f || enemy.BurnDamagePerSecond <= 0f)
                continue;

            enemy.BurnRemaining = Math.Max(0f, enemy.BurnRemaining - dt);

            float burnRaw = enemy.BurnDamagePerSecond * dt;
            float burnBefore = enemy.Hp;
            float burnDealt = SpectacleDamageCore.ApplyRawDamage(enemy, burnRaw);
            ApplyRawDamageToMetrics(enemy, burnRaw, burnDealt, metrics);
            if (burnBefore > 0f && enemy.Hp <= 0f)
                enemy.Killed = true;

            if (enemy.BurnRemaining > 0f)
            {
                enemy.BurnTrailDropTimer -= dt;
                while (enemy.BurnTrailDropTimer <= 0f)
                {
                    enemy.BurnTrailDropTimer += Balance.WildfireTrailDropInterval;
                    float trailDps = enemy.BurnDamagePerSecond * Balance.WildfireTrailDamageRatio;
                    if (trailDps <= 0f)
                        continue;

                    if (trails.Count >= Balance.WildfireMaxTrailSegments)
                        trails.RemoveAt(0);

                    trails.Add(new WildfireTrail
                    {
                        Position = enemy.GlobalPosition,
                        RemainingSeconds = Balance.WildfireTrailLifetime,
                        DamagePerSecond = trailDps,
                    });
                }
            }
        }

        for (int i = trails.Count - 1; i >= 0; i--)
        {
            WildfireTrail trail = trails[i];
            trail.RemainingSeconds = Math.Max(0f, trail.RemainingSeconds - dt);

            if (trail.RemainingSeconds <= 0f)
            {
                trails.RemoveAt(i);
                continue;
            }

            foreach (BenchmarkEnemy enemy in enemies)
            {
                if (!enemy.Spawned || enemy.Resolved || enemy.Hp <= 0f)
                    continue;
                if (trail.Position.DistanceTo(enemy.GlobalPosition) > Balance.WildfireTrailRadius)
                    continue;

                float trailRaw = trail.DamagePerSecond * dt;
                float trailBefore = enemy.Hp;
                float trailDealt = SpectacleDamageCore.ApplyRawDamage(enemy, trailRaw);
                ApplyRawDamageToMetrics(enemy, trailRaw, trailDealt, metrics);
                if (trailBefore > 0f && enemy.Hp <= 0f)
                    enemy.Killed = true;
            }
        }
    }

    private static void UpdateResidues(
        List<ResidueZone> residues,
        List<BenchmarkEnemy> enemies,
        BenchmarkTower tower,
        float dt,
        TrialMetrics metrics)
    {
        for (int i = residues.Count - 1; i >= 0; i--)
        {
            ResidueZone zone = residues[i];
            zone.RemainingSeconds = Math.Max(0f, zone.RemainingSeconds - dt);
            zone.TickRemainingSeconds -= dt;

            while (zone.TickRemainingSeconds <= 0f)
            {
                zone.TickRemainingSeconds += Math.Max(0.05f, zone.TickIntervalSeconds);
                foreach (BenchmarkEnemy enemy in enemies)
                {
                    if (!enemy.Spawned || enemy.Resolved || enemy.Hp <= 0f)
                        continue;
                    if (enemy.GlobalPosition.DistanceTo(zone.Origin) > zone.Radius)
                        continue;

                    switch (zone.Kind)
                    {
                        case ExplosionResidueKind.FrostSlow:
                            Statuses.ApplySlow(enemy, Balance.SlowDuration * 0.35f, Mathf.Clamp(0.95f - 0.35f * zone.Potency, 0.55f, 0.95f));
                            break;
                        case ExplosionResidueKind.VulnerabilityZone:
                            Statuses.ApplyDamageAmp(enemy, 0.42f * zone.Potency, 0.06f * zone.Potency);
                            break;
                        case ExplosionResidueKind.BurnPatch:
                        {
                            float burnBase = tower.BaseDamage * 0.08f * zone.Potency;
                            float burnDamage = burnBase * MathF.Max(0f, SpectacleTuning.Current.ResidueDamageMultiplier);
                            float before = enemy.Hp;
                            float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, burnDamage);
                            metrics.Hits++;
                            metrics.TotalDamage += dealt;
                            metrics.RawDamage += burnDamage;
                            metrics.OverkillWaste += Math.Max(0f, burnDamage - dealt);
                            if (enemy.IsTank) metrics.TankDamage += dealt;
                            if (enemy.IsSwarm) metrics.SwarmDamage += dealt;
                            if (before > 0f && enemy.Hp <= 0f)
                                enemy.Killed = true;
                            break;
                        }
                    }
                }
            }

            if (zone.RemainingSeconds <= 0f)
                residues.RemoveAt(i);
        }
    }

    private static void MoveEnemies(List<BenchmarkEnemy> enemies, float dt, TrialMetrics metrics)
    {
        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!enemy.Spawned || enemy.Resolved || enemy.Hp <= 0f)
                continue;

            float speed = enemy.BaseSpeed * (enemy.SlowRemaining > 0f ? enemy.SlowSpeedFactor : 1f);
            enemy.PathDistance += speed * dt;
            if (enemy.PathDistance >= enemy.PathLength)
            {
                enemy.Resolved = true;
                metrics.Leaks++;
                metrics.LeakedHp += Math.Max(0f, enemy.Hp);
            }
        }
    }

    private static void TickUndertowTimers(Dictionary<int, float> timers, float dt)
    {
        if (timers.Count == 0 || dt <= 0f)
            return;

        List<int> keys = timers.Keys.ToList();
        foreach (int id in keys)
        {
            float remaining = timers[id] - dt;
            if (remaining <= 0f)
                timers.Remove(id);
            else
                timers[id] = remaining;
        }
    }

    private static bool IsUsableUndertowTarget(BenchmarkEnemy enemy)
        => enemy.Spawned && !enemy.Resolved && enemy.Hp > 0f;

    private static bool IsInUndertowRange(BenchmarkTower tower, BenchmarkEnemy enemy)
        => tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range;

    private static BenchmarkEnemy? SelectRocketSplashTarget(BenchmarkTower tower, List<BenchmarkEnemy> enemies)
    {
        List<BenchmarkEnemy> inRange = enemies
            .Where(e => e.Spawned && !e.Resolved && e.Hp > 0f)
            .Where(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition) <= tower.Range)
            .ToList();
        if (inRange.Count == 0)
            return null;

        int blastCoreCopies = CountMod(tower, SpectacleDefinitions.BlastCore);
        float splashRadius = Balance.RocketLauncherSplashRadius
            + blastCoreCopies * Balance.RocketLauncherBlastCoreRadiusPerCopy;

        BenchmarkEnemy best = inRange[0];
        int bestCluster = -1;
        foreach (BenchmarkEnemy candidate in inRange)
        {
            int cluster = 0;
            foreach (BenchmarkEnemy enemy in enemies)
            {
                if (!enemy.Spawned || enemy.Resolved || enemy.Hp <= 0f)
                    continue;
                if (candidate.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= splashRadius)
                    cluster++;
            }

            if (cluster > bestCluster)
            {
                best = candidate;
                bestCluster = cluster;
                continue;
            }

            if (cluster == bestCluster && PreferTargetByMode(candidate, best, tower.TargetingMode))
                best = candidate;
        }

        return best;
    }

    private static bool PreferTargetByMode(BenchmarkEnemy candidate, BenchmarkEnemy current, TargetingMode mode)
    {
        switch (mode)
        {
            case TargetingMode.Strongest:
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                if (MathF.Abs(candidate.PathDistance - current.PathDistance) > 0.001f)
                    return candidate.PathDistance > current.PathDistance;
                break;
            case TargetingMode.LowestHp:
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp < current.Hp;
                if (MathF.Abs(candidate.PathDistance - current.PathDistance) > 0.001f)
                    return candidate.PathDistance > current.PathDistance;
                break;
            case TargetingMode.Last:
                if (MathF.Abs(candidate.PathDistance - current.PathDistance) > 0.001f)
                    return candidate.PathDistance < current.PathDistance;
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                break;
            default:
                if (MathF.Abs(candidate.PathDistance - current.PathDistance) > 0.001f)
                    return candidate.PathDistance > current.PathDistance;
                if (MathF.Abs(candidate.Hp - current.Hp) > 0.001f)
                    return candidate.Hp > current.Hp;
                break;
        }

        return candidate.Id < current.Id;
    }

    private static float ResolveUndertowResistance(BenchmarkEnemy target)
    {
        string group = (target.GroupId ?? string.Empty).Trim().ToLowerInvariant();
        if (group.Contains("armored", StringComparison.Ordinal) || group.Contains("shield", StringComparison.Ordinal))
            return Balance.UndertowArmoredResistanceMultiplier;
        if (target.IsTank || group.Contains("elite", StringComparison.Ordinal) || group.Contains("heavy", StringComparison.Ordinal))
            return Balance.UndertowHeavyResistanceMultiplier;
        return 1f;
    }

    private static float ResolveUndertowRecentMultiplier(
        Dictionary<int, float> undertowRecentRemaining,
        BenchmarkEnemy target)
    {
        if (!undertowRecentRemaining.TryGetValue(target.Id, out float remaining))
            return 1f;

        float t = Mathf.Clamp(remaining / MathF.Max(0.0001f, Balance.UndertowRecentWindow), 0f, 1f);
        return Mathf.Lerp(1f, Balance.UndertowRecentMinMultiplier, t);
    }

    private static float ResolveUndertowSlowFactor(BenchmarkTower tower, bool isSecondary, bool isFollowup)
    {
        float factor = Balance.UndertowSlowFactor;
        int chillCopies = CountMod(tower, "slow");
        int focusCopies = CountMod(tower, "focus_lens");
        factor -= chillCopies * Balance.UndertowSlowPerChillCopy;
        factor -= focusCopies * Balance.UndertowFocusLensSlowPerCopy;
        if (isSecondary)
            factor = Mathf.Lerp(factor, 1f, 0.35f);
        if (isFollowup)
            factor = Mathf.Lerp(factor, 1f, 0.24f);
        return Mathf.Clamp(factor, 0.12f, 0.95f);
    }

    private static void RegisterUndertowAffect(
        Dictionary<int, float> undertowRecentRemaining,
        Dictionary<int, float> undertowRetargetLockoutRemaining,
        BenchmarkEnemy target)
    {
        undertowRecentRemaining.TryGetValue(target.Id, out float currentRecent);
        undertowRecentRemaining[target.Id] = MathF.Max(currentRecent, Balance.UndertowRecentWindow);

        undertowRetargetLockoutRemaining.TryGetValue(target.Id, out float currentLockout);
        undertowRetargetLockoutRemaining[target.Id] = MathF.Max(currentLockout, Balance.UndertowRetargetLockout);
    }

    private static void UpdateActiveUndertows(
        List<BenchmarkUndertowEffect> activeUndertows,
        Dictionary<int, float> undertowRecentRemaining,
        Dictionary<int, float> undertowRetargetLockoutRemaining,
        List<BenchmarkEnemy> enemies,
        float dt)
    {
        TickUndertowTimers(undertowRecentRemaining, dt);
        TickUndertowTimers(undertowRetargetLockoutRemaining, dt);
        if (activeUndertows.Count == 0 || dt <= 0f)
            return;

        var activeCountByEnemy = new Dictionary<int, int>();
        foreach (BenchmarkUndertowEffect effect in activeUndertows)
        {
            if (effect.DelayRemaining > 0f || !IsUsableUndertowTarget(effect.Target))
                continue;
            activeCountByEnemy.TryGetValue(effect.Target.Id, out int count);
            activeCountByEnemy[effect.Target.Id] = count + 1;
        }

        for (int i = activeUndertows.Count - 1; i >= 0; i--)
        {
            BenchmarkUndertowEffect effect = activeUndertows[i];
            if (!IsUsableUndertowTarget(effect.Target))
            {
                activeUndertows.RemoveAt(i);
                continue;
            }

            if (effect.DelayRemaining > 0f)
            {
                effect.DelayRemaining = MathF.Max(0f, effect.DelayRemaining - dt);
                if (effect.DelayRemaining > 0f)
                    continue;

                RegisterUndertowAffect(undertowRecentRemaining, undertowRetargetLockoutRemaining, effect.Target);
                Statuses.ApplySlow(effect.Target, MathF.Max(0.12f, effect.TotalDuration), effect.SlowFactor);
            }

            float remainingDistance = MathF.Max(0f, effect.TotalPullDistance - effect.PulledDistance);
            if (remainingDistance <= 0.01f || effect.Remaining <= 0f)
            {
                CompleteUndertowEffect(effect, enemies, undertowRecentRemaining);
                activeUndertows.RemoveAt(i);
                continue;
            }

            Statuses.ApplySlow(effect.Target, MathF.Max(0.10f, dt + 0.05f), effect.SlowFactor);

            float stepPull = effect.TotalDuration <= 0.001f
                ? remainingDistance
                : effect.TotalPullDistance * (dt / effect.TotalDuration);
            stepPull = MathF.Min(stepPull, remainingDistance);

            if (activeCountByEnemy.TryGetValue(effect.Target.Id, out int concurrent) && concurrent > 1)
            {
                float concurrentScale = 1f;
                for (int c = 1; c < concurrent; c++)
                    concurrentScale *= Balance.UndertowConcurrentExtraDecay;
                stepPull *= concurrentScale;
            }

            float startDistance = effect.Target.PathDistance;
            float endDistance = MathF.Max(0f, startDistance - stepPull);
            float applied = startDistance - endDistance;
            if (applied > 0f)
            {
                effect.Target.PathDistance = endDistance;
                effect.PulledDistance += applied;
            }

            effect.Remaining -= dt;
            if (effect.Remaining <= 0f || effect.PulledDistance >= effect.TotalPullDistance - 0.01f)
            {
                CompleteUndertowEffect(effect, enemies, undertowRecentRemaining);
                activeUndertows.RemoveAt(i);
            }
        }
    }

    private static BenchmarkEnemy? SelectUndertowPrimaryTarget(
        BenchmarkTower tower,
        List<BenchmarkEnemy> enemies,
        List<BenchmarkUndertowEffect> activeUndertows,
        Dictionary<int, float> undertowRetargetLockoutRemaining)
    {
        List<BenchmarkEnemy> candidates = enemies
            .Where(IsUsableUndertowTarget)
            .Where(e => IsInUndertowRange(tower, e))
            .ToList();
        if (candidates.Count == 0)
            return null;

        bool PreferCandidate(BenchmarkEnemy a, BenchmarkEnemy b)
        {
            bool aActive = activeUndertows.Any(u => u.DelayRemaining <= 0f && ReferenceEquals(u.Target, a));
            bool bActive = activeUndertows.Any(u => u.DelayRemaining <= 0f && ReferenceEquals(u.Target, b));
            if (aActive != bActive)
                return !aActive;

            bool aLocked = undertowRetargetLockoutRemaining.TryGetValue(a.Id, out float aLock) && aLock > 0f;
            bool bLocked = undertowRetargetLockoutRemaining.TryGetValue(b.Id, out float bLock) && bLock > 0f;
            if (aLocked != bLocked)
                return !aLocked;

            if (MathF.Abs(a.PathDistance - b.PathDistance) > 0.001f)
                return a.PathDistance > b.PathDistance;
            if (MathF.Abs(a.Hp - b.Hp) > 0.001f)
                return a.Hp > b.Hp;
            return a.Id < b.Id;
        }

        BenchmarkEnemy best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            BenchmarkEnemy candidate = candidates[i];
            if (PreferCandidate(candidate, best))
                best = candidate;
        }
        return best;
    }

    private static bool TryStartUndertowEffect(
        BenchmarkTower tower,
        BenchmarkEnemy target,
        float strengthMultiplier,
        bool isSecondary,
        bool isFollowup,
        bool enableEndpointPulse,
        List<BenchmarkUndertowEffect> activeUndertows,
        Dictionary<int, float> undertowRecentRemaining,
        Dictionary<int, float> undertowRetargetLockoutRemaining,
        float delaySeconds = 0f)
    {
        if (!IsUsableUndertowTarget(target))
            return false;

        float pull = Balance.UndertowPullDistance;
        pull *= (1f + CountMod(tower, "focus_lens") * Balance.UndertowFocusLensPullPerCopy);
        if (target.IsMarked || target.DamageAmpRemaining > 0f || target.SlowRemaining > 0f)
            pull *= (1f + Balance.UndertowMarkedSusceptibilityBonus);

        pull *= ResolveUndertowResistance(target);
        pull *= ResolveUndertowRecentMultiplier(undertowRecentRemaining, target);
        pull *= MathF.Max(0f, strengthMultiplier);
        if (isFollowup)
            pull *= Balance.UndertowFeedbackFollowupMultiplier;
        pull = MathF.Min(Balance.UndertowPullDistanceCap, pull);
        pull = MathF.Min(pull, target.PathDistance);
        if (pull < Balance.UndertowMinEffectivePull)
            return false;

        float duration = Balance.UndertowDuration;
        if (isSecondary)
            duration *= Balance.UndertowSecondaryDurationMultiplier;
        if (isFollowup)
            duration *= 0.82f;
        duration = MathF.Max(0.12f, duration);

        var effect = new BenchmarkUndertowEffect
        {
            Tower = tower,
            Target = target,
            TotalDuration = duration,
            Remaining = duration,
            TotalPullDistance = pull,
            PulledDistance = 0f,
            SlowFactor = ResolveUndertowSlowFactor(tower, isSecondary, isFollowup),
            IsSecondary = isSecondary,
            IsFollowup = isFollowup,
            EnableEndpointPulse = enableEndpointPulse,
            DelayRemaining = MathF.Max(0f, delaySeconds),
        };

        activeUndertows.Add(effect);
        if (effect.DelayRemaining <= 0f)
        {
            RegisterUndertowAffect(undertowRecentRemaining, undertowRetargetLockoutRemaining, target);
            Statuses.ApplySlow(target, MathF.Max(0.12f, duration), effect.SlowFactor);
        }
        return true;
    }

    private static BenchmarkEnemy? SelectUndertowSecondaryTarget(
        BenchmarkTower tower,
        BenchmarkEnemy primary,
        List<BenchmarkEnemy> enemies,
        HashSet<BenchmarkEnemy> excluded,
        float searchRadius)
    {
        BenchmarkEnemy? best = null;
        float bestDistance = searchRadius;
        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!IsUsableUndertowTarget(enemy) || ReferenceEquals(enemy, primary) || excluded.Contains(enemy))
                continue;
            if (!IsInUndertowRange(tower, enemy))
                continue;

            float distance = primary.GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (distance > searchRadius)
                continue;

            if (best == null
                || distance < bestDistance - 0.001f
                || (MathF.Abs(distance - bestDistance) <= 0.001f && enemy.Id < best.Id))
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static int ApplyUndertowSecondaryTugs(
        BenchmarkTower tower,
        BenchmarkEnemy primaryTarget,
        List<BenchmarkEnemy> enemies,
        List<BenchmarkUndertowEffect> activeUndertows,
        Dictionary<int, float> undertowRecentRemaining,
        Dictionary<int, float> undertowRetargetLockoutRemaining)
    {
        int applied = 0;
        var excluded = new HashSet<BenchmarkEnemy> { primaryTarget };

        if (tower.SplitCount > 0)
        {
            BenchmarkEnemy? splitTarget = SelectUndertowSecondaryTarget(
                tower,
                primaryTarget,
                enemies,
                excluded,
                Balance.UndertowSecondarySearchRadius);
            if (splitTarget != null && TryStartUndertowEffect(
                tower,
                splitTarget,
                Balance.UndertowSplitSecondaryMultiplier,
                isSecondary: true,
                isFollowup: false,
                enableEndpointPulse: false,
                activeUndertows,
                undertowRecentRemaining,
                undertowRetargetLockoutRemaining))
            {
                applied++;
                excluded.Add(splitTarget);
            }
        }

        if (tower.ChainCount > 0)
        {
            BenchmarkEnemy? chainTarget = SelectUndertowSecondaryTarget(
                tower,
                primaryTarget,
                enemies,
                excluded,
                MathF.Max(Balance.UndertowSecondarySearchRadius, tower.ChainRange));
            if (chainTarget != null && TryStartUndertowEffect(
                tower,
                chainTarget,
                Balance.UndertowChainSecondaryMultiplier,
                isSecondary: true,
                isFollowup: false,
                enableEndpointPulse: false,
                activeUndertows,
                undertowRecentRemaining,
                undertowRetargetLockoutRemaining))
            {
                applied++;
            }
        }

        return applied;
    }

    private static void ScheduleUndertowFeedbackFollowup(
        BenchmarkTower tower,
        BenchmarkEnemy primaryTarget,
        Random rng,
        List<BenchmarkUndertowEffect> activeUndertows,
        Dictionary<int, float> undertowRecentRemaining,
        Dictionary<int, float> undertowRetargetLockoutRemaining)
    {
        int feedbackCopies = CountMod(tower, "feedback_loop");
        if (feedbackCopies <= 0)
            return;

        float chance = Mathf.Clamp(feedbackCopies * Balance.UndertowFeedbackFollowupChance, 0f, 0.72f);
        if (rng.NextDouble() > chance)
            return;

        TryStartUndertowEffect(
            tower,
            primaryTarget,
            strengthMultiplier: 1f,
            isSecondary: true,
            isFollowup: true,
            enableEndpointPulse: false,
            activeUndertows,
            undertowRecentRemaining,
            undertowRetargetLockoutRemaining,
            delaySeconds: Balance.UndertowFeedbackFollowupDelay);
    }

    private static void CompleteUndertowEffect(
        BenchmarkUndertowEffect effect,
        List<BenchmarkEnemy> enemies,
        Dictionary<int, float> undertowRecentRemaining)
    {
        if (!IsUsableUndertowTarget(effect.Target))
            return;
        if (effect.PulledDistance <= 0.01f)
            return;
        if (!effect.EnableEndpointPulse)
            return;

        ApplyUndertowEndpointCompression(effect, enemies, undertowRecentRemaining);
    }

    private static void ApplyUndertowEndpointCompression(
        BenchmarkUndertowEffect effect,
        List<BenchmarkEnemy> enemies,
        Dictionary<int, float> undertowRecentRemaining)
    {
        int blastCopies = CountMod(effect.Tower, "blast_core");
        float radius = Balance.UndertowEndpointBaseRadius + blastCopies * Balance.UndertowEndpointRadiusPerBlastCore;
        float basePull = Balance.UndertowEndpointBasePull + blastCopies * Balance.UndertowEndpointPullPerBlastCore;
        if (radius <= 0f || basePull <= 0f)
            return;

        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!IsUsableUndertowTarget(enemy))
                continue;
            if (enemy.GlobalPosition.DistanceTo(effect.Target.GlobalPosition) > radius)
                continue;

            float pull = basePull;
            if (!ReferenceEquals(enemy, effect.Target))
                pull *= 0.72f;
            pull *= ResolveUndertowResistance(enemy);
            pull *= ResolveUndertowRecentMultiplier(undertowRecentRemaining, enemy);

            float start = enemy.PathDistance;
            float end = MathF.Max(0f, start - pull);
            float moved = start - end;
            if (moved <= 0.001f)
                continue;

            enemy.PathDistance = end;
            if (!ReferenceEquals(enemy, effect.Target))
                Statuses.ApplySlow(enemy, Balance.UndertowEndpointSlowDuration, Balance.UndertowEndpointSlowFactor);
        }
    }

    private static void MarkNewDeaths(List<BenchmarkEnemy> enemies, TrialMetrics metrics)
    {
        foreach (BenchmarkEnemy enemy in enemies)
        {
            if (!enemy.Spawned || enemy.Resolved)
                continue;
            if (enemy.Hp > 0f)
                continue;

            enemy.Resolved = true;
            enemy.Killed = true;
            if (enemy.IsTank && enemy.ProgressRatio > 0.65f)
                metrics.EdgeSpike = true;
        }
    }

    private static void ApplyLiveSurgeGameplay(
        BenchmarkTower tower,
        List<BenchmarkEnemy> enemies,
        List<ResidueZone> residues,
        float surgePower,
        bool globalSurge,
        TrialMetrics metrics)
    {
        List<BenchmarkEnemy> activeEnemies = enemies
            .Where(e => e.Spawned && !e.Resolved && e.Hp > 0f)
            .ToList();
        if (activeEnemies.Count == 0)
            return;

        bool reducedMotion = false;
        int maxTargets = SpectacleExplosionCore.ResolveStatusDetonationMaxTargets(globalSurge, reducedMotion);
        if (maxTargets <= 0)
            return;

        List<BenchmarkEnemy> statusTargets = activeEnemies
            .Where(e => e.IsMarked || e.SlowRemaining > 0f || e.DamageAmpRemaining > 0f)
            .OrderBy(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
            .ThenByDescending(e => e.ProgressRatio)
            .Take(maxTargets)
            .ToList();
        if (statusTargets.Count == 0)
            statusTargets = activeEnemies
                .OrderBy(e => tower.GlobalPosition.DistanceTo(e.GlobalPosition))
                .Take(Math.Min(maxTargets, 3))
                .ToList();

        ComboExplosionSkin skin = ResolveSkinFromMods(tower.Modifiers);
        for (int i = 0; i < statusTargets.Count; i++)
        {
            BenchmarkEnemy enemy = statusTargets[i];
            float damage = tower.BaseDamage
                * (globalSurge ? 0.22f : 0.16f)
                * Mathf.Clamp(surgePower, 0.6f, 2.2f)
                * MathF.Max(0.52f, 1f - i * 0.04f)
                * MathF.Max(0f, SpectacleTuning.Current.StatusDetonationDamageMultiplier);
            float before = enemy.Hp;
            float dealt = SpectacleDamageCore.ApplyRawDamage(enemy, damage);
            metrics.Hits++;
            metrics.TotalDamage += dealt;
            metrics.RawDamage += damage;
            metrics.OverkillWaste += Math.Max(0f, damage - dealt);
            if (enemy.IsTank) metrics.TankDamage += dealt;
            if (enemy.IsSwarm) metrics.SwarmDamage += dealt;
            if (before > 0f && enemy.Hp <= 0f)
                enemy.Killed = true;

            ExplosionResidueProfile residue = SpectacleExplosionCore.ResolveResidueProfile(
                skin,
                globalSurge,
                surgePower,
                i);
            if (residue.ShouldSpawn)
            {
                residues.Add(new ResidueZone
                {
                    Kind = residue.Kind,
                    Origin = enemy.GlobalPosition,
                    Radius = residue.Radius,
                    RemainingSeconds = residue.DurationSeconds,
                    TickIntervalSeconds = residue.TickIntervalSeconds,
                    TickRemainingSeconds = residue.TickIntervalSeconds,
                    Potency = residue.Potency,
                });
            }
        }
    }

    private static void ApplyLiveGlobalGameplay(
        BenchmarkTower tower,
        List<BenchmarkEnemy> enemies,
        List<ResidueZone> residues,
        int contributors,
        TrialMetrics metrics)
    {
        int c = Math.Max(2, contributors);
        float markDuration = 2.2f + 0.28f * c;
        float slowDuration = 1.8f + 0.24f * c;
        float slowFactor = Mathf.Clamp(0.88f - 0.06f * c, 0.58f, 0.90f);
        foreach (BenchmarkEnemy enemy in enemies.Where(e => e.Spawned && !e.Resolved && e.Hp > 0f))
        {
            Statuses.ApplyMarked(enemy, markDuration);
            Statuses.ApplySlow(enemy, slowDuration, slowFactor);
        }

        float cooldownRefund = Mathf.Clamp(0.24f + 0.04f * c, 0.24f, 0.46f);
        tower.Cooldown = Math.Max(0f, tower.Cooldown * (1f - cooldownRefund));

        ApplyLiveSurgeGameplay(tower, enemies, residues, surgePower: 1.45f, globalSurge: true, metrics);
    }

    private static void RegisterLiveProcForHit(
        SpectacleSystem? spectacle,
        BenchmarkTower tower,
        bool targetWasMarkedBeforeHit,
        IEnemyView target,
        float hitDamage)
    {
        if (spectacle == null)
            return;

        float dealt = Math.Max(0f, hitDamage);
        if (CountMod(tower, SpectacleDefinitions.ExploitWeakness) > 0 && targetWasMarkedBeforeHit)
            spectacle.RegisterProc(tower, SpectacleDefinitions.ExploitWeakness,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ExploitWeakness), dealt);

        if (CountMod(tower, SpectacleDefinitions.FocusLens) > 0)
            spectacle.RegisterProc(tower, SpectacleDefinitions.FocusLens,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.FocusLens), dealt);

        if (CountMod(tower, SpectacleDefinitions.Overreach) > 0)
            spectacle.RegisterProc(tower, SpectacleDefinitions.Overreach,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.Overreach), dealt);

        if (CountMod(tower, SpectacleDefinitions.ChillShot) > 0)
            spectacle.RegisterProc(tower, SpectacleDefinitions.ChillShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.ChillShot), dealt);
    }

    private static DamageContext ApplyHitAndCollect(
        BenchmarkTower tower,
        BenchmarkEnemy target,
        List<BenchmarkEnemy> activeEnemies,
        TrialMetrics metrics,
        RunState? state,
        bool isChain,
        float damageOverride = -1f)
    {
        var ctx = new DamageContext(
            tower,
            target,
            waveIndex: 0,
            activeEnemies,
            state,
            isChain: isChain,
            damageOverride: damageOverride);
        DamageModel.Apply(ctx);
        ApplyContextToMetrics(ctx, metrics);
        return ctx;
    }

    private static int ApplyPhaseSplitterSplitHits(
        BenchmarkTower tower,
        BenchmarkEnemy primary,
        List<BenchmarkEnemy> activeEnemies,
        TrialMetrics metrics,
        SpectacleSystem? spectacle)
    {
        if (tower.SplitCount <= 0)
            return 0;

        float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
        int splitBudget = tower.SplitCount; // phase splitter: one split per copy on each endpoint hit
        int spawned = 0;
        foreach (BenchmarkEnemy candidate in activeEnemies
            .Where(e => !ReferenceEquals(e, primary) && e.Hp > 0f)
            .OrderBy(e => e.GlobalPosition.DistanceTo(primary.GlobalPosition)))
        {
            if (spawned >= splitBudget)
                break;
            if (candidate.GlobalPosition.DistanceTo(primary.GlobalPosition) > Balance.SplitShotRange)
                break;

            var ctx = new DamageContext(
                tower,
                candidate,
                waveIndex: 0,
                activeEnemies,
                state: null,
                isChain: true,
                damageOverride: splitDamage);
            DamageModel.Apply(ctx);
            ApplyContextToMetrics(ctx, metrics);
            RegisterLiveProcForHit(spectacle, tower, ctx.Target.IsMarked, ctx.Target, ctx.DamageDealt);
            spawned++;
        }

        if (spawned > 0 && spectacle != null)
            spectacle.RegisterProc(tower, SpectacleDefinitions.SplitShot,
                SpectacleDefinitions.GetProcScalar(SpectacleDefinitions.SplitShot), splitDamage);
        return spawned;
    }

    private static void ApplyRawDamageToMetrics(BenchmarkEnemy enemy, float rawDamage, float dealt, TrialMetrics metrics)
    {
        float safeRaw = Math.Max(0f, rawDamage);
        float safeDealt = Math.Max(0f, dealt);
        metrics.Hits++;
        metrics.TotalDamage += safeDealt;
        metrics.RawDamage += safeRaw;
        metrics.OverkillWaste += Math.Max(0f, safeRaw - safeDealt);
        if (enemy.IsTank) metrics.TankDamage += safeDealt;
        if (enemy.IsSwarm) metrics.SwarmDamage += safeDealt;
    }

    private static void ApplyContextToMetrics(DamageContext ctx, TrialMetrics metrics)
    {
        metrics.Hits++;
        float primaryDealt = Math.Max(0f, ctx.DamageDealt);
        float splashDealt  = Math.Max(0f, ctx.SplashDamageDealt);
        float spillDealt   = Math.Max(0f, ctx.OverkillSpillDealt);
        metrics.TotalDamage += primaryDealt + splashDealt + spillDealt;
        metrics.RawDamage += Math.Max(0f, ctx.FinalDamage);
        metrics.OverkillWaste += Math.Max(0f, ctx.FinalDamage - ctx.DamageDealt);
        if (ctx.Target is BenchmarkEnemy enemy)
        {
            if (enemy.IsTank) metrics.TankDamage += primaryDealt + splashDealt + spillDealt;
            if (enemy.IsSwarm) metrics.SwarmDamage += primaryDealt + splashDealt + spillDealt;
        }
    }

    private static void ApplyScenarioNormalization(
        CombatLabTowerBenchmarkScenarioResult scenarioResult,
        CombatLabTowerBenchmarkSuite suite,
        List<TowerCase> towerCases)
    {
        List<(CombatLabTowerBenchmarkTowerResult Result, float Score)> scored = scenarioResult.Results
            .Select(result => (result, Score: ResolveScenarioPerformanceScore(result)))
            .ToList();
        float globalMedian = ResolveMedian(scored.Select(row => row.Score).ToList());
        if (globalMedian <= 0.0001f)
            globalMedian = 1f;

        Dictionary<string, float> towerCost = towerCases.ToDictionary(t => t.CaseId, t => t.Cost, StringComparer.Ordinal);
        List<CombatLabCostBand> costBands = ResolveCostBands(suite, towerCases);

        foreach (CombatLabTowerBenchmarkTowerResult row in scenarioResult.Results)
        {
            float score = scored.First(s => s.Result == row).Score;
            row.NormalizedGlobal = score / globalMedian;

            string bandId = ResolveCostBandId(row.Cost, costBands);
            List<float> sameBandScores = scored
                .Where(s =>
                {
                    towerCost.TryGetValue(s.Result.CaseId, out float c);
                    return ResolveCostBandId(c, costBands) == bandId;
                })
                .Select(s => s.Score)
                .ToList();
            float bandMedian = ResolveMedian(sameBandScores);
            if (bandMedian <= 0.0001f) bandMedian = globalMedian;
            row.NormalizedCostBand = score / bandMedian;

            string[] towerRoles = ResolveRolesForTower(suite, row.TowerId);
            List<float> rolePeerScores = scored
                .Where(s => SharesRole(towerRoles, ResolveRolesForTower(suite, s.Result.TowerId)))
                .Select(s => s.Score)
                .ToList();
            float roleMedian = ResolveMedian(rolePeerScores);
            if (roleMedian <= 0.0001f) roleMedian = globalMedian;
            row.NormalizedRole = score / roleMedian;
        }
    }

    private static List<CombatLabTowerBenchmarkProfile> BuildTowerProfiles(
        CombatLabTowerBenchmarkSuite suite,
        List<TowerCase> towerCases,
        List<CombatLabTowerBenchmarkScenarioResult> scenarioResults)
    {
        List<CombatLabCostBand> bands = ResolveCostBands(suite, towerCases);
        var profiles = new List<CombatLabTowerBenchmarkProfile>();

        foreach (TowerCase towerCase in towerCases.OrderBy(t => t.CaseId, StringComparer.Ordinal))
        {
            List<CombatLabTowerBenchmarkTowerResult> rows = scenarioResults
                .SelectMany(s => s.Results)
                .Where(r => r.CaseId == towerCase.CaseId)
                .ToList();
            if (rows.Count == 0)
                continue;

            float avgGlobal = (float)rows.Average(r => r.NormalizedGlobal);
            float avgCostBand = (float)rows.Average(r => r.NormalizedCostBand);
            float avgRole = (float)rows.Average(r => r.NormalizedRole);
            float avgVariance = (float)rows.Average(r => r.ReliabilityVariance);
            float pathSensitivity = ResolvePathSensitivity(towerCase.CaseId, scenarioResults);
            var profile = new CombatLabTowerBenchmarkProfile
            {
                CaseId = towerCase.CaseId,
                TowerId = towerCase.TowerId,
                Cost = towerCase.Cost,
                CostBand = ResolveCostBandId(towerCase.Cost, bands),
                Roles = ResolveRolesForTower(suite, towerCase.TowerId),
                ScenarioCount = rows.Count,
                AvgNormalizedGlobal = avgGlobal,
                AvgNormalizedCostBand = avgCostBand,
                AvgNormalizedRole = avgRole,
                MapPathSensitivity = pathSensitivity,
                AggregateReliabilityVariance = avgVariance,
            };

            int dominantScenarios = rows.Count(r => r.NormalizedGlobal >= 1.20f);
            if (avgGlobal < 0.88f && avgCostBand < 0.88f)
                profile.Flags.Add("likely underpowered");
            if (avgGlobal > 1.15f && avgCostBand > 1.15f)
                profile.Flags.Add("likely overpowered");
            if (dominantScenarios >= Math.Max(2, (int)MathF.Ceiling(rows.Count * 0.70f)))
                profile.Flags.Add("suspiciously dominant across too many scenarios");
            if (avgVariance > 0.22f || pathSensitivity > 0.48f)
                profile.Flags.Add("inconsistent / unstable");

            float maxRole = ResolveMaxRolePerformance(rows, profile.Roles, scenarioResults);
            if (maxRole >= 1.20f && avgGlobal >= 0.85f && avgGlobal <= 1.08f)
                profile.Flags.Add("highly niche but acceptable");

            float topScenario = rows.Max(r => r.NormalizedGlobal);
            float medianScenario = ResolveMedian(rows.Select(r => r.NormalizedGlobal).ToList());
            if (topScenario >= 1.65f && medianScenario <= 1.02f)
                profile.Flags.Add("warning: balanced by one abusive edge case");

            profiles.Add(profile);
        }

        return profiles.OrderBy(p => p.CaseId, StringComparer.Ordinal).ToList();
    }

    private static float ResolveMaxRolePerformance(
        List<CombatLabTowerBenchmarkTowerResult> rows,
        string[] roles,
        List<CombatLabTowerBenchmarkScenarioResult> scenarioResults)
    {
        if (roles == null || roles.Length == 0)
            return rows.Count > 0 ? rows.Max(r => r.NormalizedRole) : 0f;

        float best = 0f;
        foreach (string role in roles)
        {
            var roleRows = scenarioResults
                .Where(s => (s.Tags ?? Array.Empty<string>()).Any(tag => string.Equals(tag, role, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(s => s.Results)
                .Where(r => rows.Contains(r))
                .ToList();
            if (roleRows.Count == 0)
                continue;
            best = MathF.Max(best, (float)roleRows.Average(r => r.NormalizedRole));
        }
        return best;
    }

    private static List<CombatLabTowerBenchmarkSuggestion> BuildSuggestions(
        CombatLabTowerBenchmarkSuite suite,
        List<CombatLabTowerBenchmarkProfile> profiles)
    {
        var autotune = suite.Autotune;
        if (autotune == null || !autotune.Enabled)
            return new List<CombatLabTowerBenchmarkSuggestion>();

        float maxDelta = Mathf.Clamp(autotune.MaxPercentDelta, 0.01f, 0.30f);
        float targetMin = MathF.Max(0.5f, autotune.TargetRoleScoreMin);
        float targetMax = MathF.Max(targetMin, autotune.TargetRoleScoreMax);
        float targetMid = (targetMin + targetMax) * 0.5f;
        var suggestions = new List<CombatLabTowerBenchmarkSuggestion>();

        foreach (CombatLabTowerBenchmarkProfile profile in profiles)
        {
            if (profile.Flags.Contains("highly niche but acceptable"))
                continue;

            float roleScore = profile.AvgNormalizedRole;
            bool buff = roleScore < targetMin;
            bool nerf = roleScore > targetMax && profile.AvgNormalizedGlobal > targetMax;
            if (!buff && !nerf)
                continue;

            float error = MathF.Abs(roleScore - targetMid) / MathF.Max(0.0001f, targetMid);
            float magnitude = Mathf.Clamp(error * 0.6f, 0.01f, maxDelta);
            float direction = buff ? 1f : -1f;
            CombatLabTowerAutotuneBounds? bounds = ResolveAutotuneBoundsForTower(autotune, profile.TowerId);
            var deltas = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                ["damage"] = ClampDelta(direction * magnitude, maxDelta, bounds?.Damage),
                ["attack_interval"] = ClampDelta(-direction * magnitude * 0.55f, maxDelta, bounds?.AttackInterval),
                ["range"] = ClampDelta(direction * magnitude * 0.35f, maxDelta, bounds?.Range),
                ["cost"] = ClampDelta(-direction * magnitude * 0.50f, maxDelta, bounds?.Cost),
            };

            suggestions.Add(new CombatLabTowerBenchmarkSuggestion
            {
                TowerId = profile.CaseId,
                Reason = buff ? "role score below target band" : "role score above target band",
                SuggestedDeltas = deltas,
                Notes = buff
                    ? "Prioritize role recovery over global dominance. Keep mechanic identity unchanged."
                    : "Reduce broad dominance while preserving specialty role."
            });
        }

        return suggestions;
    }

    private Dictionary<string, TowerDef> ResolveTowerDefinitions()
    {
        if (_towerDefsOverride != null)
            return new Dictionary<string, TowerDef>(_towerDefsOverride, StringComparer.Ordinal);

        return DataLoader
            .GetAllTowerIds(includeLocked: true)
            .ToDictionary(id => id, DataLoader.GetTowerDef, StringComparer.Ordinal);
    }

    private List<TowerCase> ResolveTowerCases(CombatLabTowerBenchmarkSuite suite, Dictionary<string, TowerDef> towerDefs)
    {
        var cases = new Dictionary<string, TowerCase>(StringComparer.Ordinal);

        if (suite.IncludeAllTowers)
        {
            foreach ((string towerId, _) in towerDefs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                float cost = ResolveCostForTower(suite, towerId);
                cases[towerId] = new TowerCase
                {
                    CaseId = towerId,
                    TowerId = towerId,
                    Mods = Array.Empty<string>(),
                    Cost = cost,
                };
            }
        }

        foreach (CombatLabTowerBenchmarkTowerSetup setup in suite.Towers)
        {
            if (string.IsNullOrWhiteSpace(setup.Tower))
                continue;
            if (!towerDefs.ContainsKey(setup.Tower))
                continue;

            string caseId = string.IsNullOrWhiteSpace(setup.CaseId)
                ? setup.Tower
                : setup.CaseId.Trim();
            float cost = setup.Cost ?? ResolveCostForTower(suite, setup.Tower);
            cases[caseId] = new TowerCase
            {
                CaseId = caseId,
                TowerId = setup.Tower,
                Mods = (setup.Mods ?? Array.Empty<string>()).Take(Balance.MaxModifiersPerTower).ToArray(),
                TargetingMode = ResolveTargetingMode(setup.Targeting),
                BaseDamageOverride = setup.BaseDamageOverride,
                AttackIntervalOverride = setup.AttackIntervalOverride,
                RangeOverride = setup.RangeOverride,
                SplitCountOverride = setup.SplitCountOverride,
                ChainCountOverride = setup.ChainCountOverride,
                Cost = Math.Max(0.01f, cost),
            };
        }

        return cases.Values.OrderBy(c => c.CaseId, StringComparer.Ordinal).ToList();
    }

    private BenchmarkTower BuildTower(CombatLabTowerBenchmarkScenario scenario, TowerCase towerCase, TowerDef def)
    {
        Vector2 position = scenario.TowerPosition != null && scenario.TowerPosition.Length >= 2
            ? new Vector2(scenario.TowerPosition[0], scenario.TowerPosition[1])
            : new Vector2(MathF.Max(420f, scenario.PathLength * 0.38f), 0f);

        var tower = new BenchmarkTower
        {
            TowerId = towerCase.TowerId,
            BaseDamage = towerCase.BaseDamageOverride ?? def.BaseDamage,
            AttackInterval = towerCase.AttackIntervalOverride ?? def.AttackInterval,
            Range = towerCase.RangeOverride ?? def.Range,
            AppliesMark = def.AppliesMark,
            SplitCount = towerCase.SplitCountOverride ?? def.SplitCount,
            ChainCount = towerCase.ChainCountOverride ?? def.ChainCount,
            ChainRange = def.ChainRange,
            ChainDamageDecay = def.ChainDamageDecay,
            TargetingMode = towerCase.TargetingMode,
            GlobalPosition = position,
        };

        foreach (string modId in towerCase.Mods)
        {
            if (string.IsNullOrWhiteSpace(modId))
                continue;
            Modifier mod = _modifierFactoryOverride?.Invoke(modId) ?? ModifierRegistry.Create(modId);
            tower.Modifiers.Add(mod);
            mod.OnEquip(tower);
        }

        return tower;
    }

    private static List<BenchmarkEnemy> BuildEnemies(CombatLabTowerBenchmarkScenario scenario, Random rng)
    {
        var groups = scenario.EnemyGroups ?? new List<CombatLabTowerBenchmarkEnemyGroup>();
        float medianHp = ResolveMedian(groups.Select(g => g.Hp).ToList());
        if (medianHp <= 0f)
            medianHp = Balance.BaseEnemyHp;

        int nextId = 1;
        var enemies = new List<BenchmarkEnemy>();
        foreach (CombatLabTowerBenchmarkEnemyGroup group in groups)
        {
            int count = Math.Max(0, group.Count);
            float interval = Math.Max(0f, group.SpawnInterval);
            float laneSpread = Math.Max(0f, group.LaneSpread);
            bool tankGroup = (group.Id ?? string.Empty).Contains("tank", StringComparison.OrdinalIgnoreCase)
                || group.Hp >= medianHp * 1.35f;
            bool swarmGroup = (group.Id ?? string.Empty).Contains("swarm", StringComparison.OrdinalIgnoreCase)
                || group.Hp <= medianHp * 0.85f;
            for (int i = 0; i < count; i++)
            {
                float offsetY = ResolveLaneOffset(scenario, laneSpread, rng);
                enemies.Add(new BenchmarkEnemy
                {
                    Id = nextId++,
                    GroupId = group.Id ?? string.Empty,
                    IsTank = tankGroup,
                    IsSwarm = swarmGroup,
                    MaxHp = Math.Max(1f, group.Hp),
                    Hp = Math.Max(1f, group.Hp * Math.Clamp(group.StartHpRatio, 0.01f, 1f)),
                    BaseSpeed = Math.Max(1f, group.Speed),
                    PathLength = Math.Max(200f, scenario.PathLength),
                    LaneOffsetY = offsetY,
                    SpawnTime = Math.Max(0f, group.StartTime) + interval * i,
                    PathDistance = Mathf.Clamp(group.StartProgress, 0f, 1f) * Math.Max(200f, scenario.PathLength),
                    SlowSpeedFactor = 1f,
                });
            }
        }

        return enemies;
    }

    private static float ResolveLaneOffset(CombatLabTowerBenchmarkScenario scenario, float groupSpread, Random rng)
    {
        string pathType = (scenario.PathType ?? string.Empty).Trim().ToLowerInvariant();
        float laneWidth = Math.Max(20f, scenario.LaneWidth);
        float half = laneWidth * 0.5f;
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
        float spread = groupSpread > 0f ? groupSpread : pathType switch
        {
            "dense_choke" => laneWidth * 0.12f,
            "open_lane" => laneWidth * 0.95f,
            _ => laneWidth * 0.32f,
        };
        return Mathf.Clamp(noise * spread * 0.5f, -half, half);
    }

    private static float ResolveScenarioPerformanceScore(CombatLabTowerBenchmarkTowerResult result)
    {
        float dpsComponent = MathF.Max(0f, result.EffectiveDps);
        float leakComponent = Mathf.Clamp(result.LeakPreventionValue, 0f, 1f);
        float roleComponent = MathF.Max(0f, result.AntiTankPerformance + result.AntiSwarmPerformance) * 0.5f;
        return dpsComponent * (0.52f + leakComponent * 0.48f) * (0.72f + MathF.Min(1f, roleComponent) * 0.28f);
    }

    private static float ResolvePathSensitivity(string caseId, List<CombatLabTowerBenchmarkScenarioResult> scenarios)
    {
        var byPath = scenarios
            .GroupBy(s => (s.PathType ?? "unknown").Trim().ToLowerInvariant())
            .Select(g =>
            {
                var rows = g.SelectMany(s => s.Results).Where(r => r.CaseId == caseId).ToList();
                return rows.Count == 0 ? 0f : (float)rows.Average(r => r.NormalizedGlobal);
            })
            .Where(v => v > 0f)
            .ToList();

        if (byPath.Count <= 1)
            return 0f;
        float max = byPath.Max();
        float min = byPath.Min();
        float median = ResolveMedian(byPath);
        return median <= 0.0001f ? 0f : (max - min) / median;
    }

    private static List<CombatLabCostBand> ResolveCostBands(CombatLabTowerBenchmarkSuite suite, List<TowerCase> cases)
    {
        if (suite.CostBands != null && suite.CostBands.Count > 0)
            return suite.CostBands.OrderBy(b => b.MinCost).ToList();

        List<float> costs = cases.Select(c => c.Cost).OrderBy(v => v).ToList();
        float p33 = ResolveQuantile(costs, 0.33f);
        float p66 = ResolveQuantile(costs, 0.66f);
        return new List<CombatLabCostBand>
        {
            new() { Id = "low", MinCost = 0f, MaxCost = p33 },
            new() { Id = "mid", MinCost = p33, MaxCost = p66 },
            new() { Id = "high", MinCost = p66, MaxCost = float.MaxValue },
        };
    }

    private static string ResolveCostBandId(float cost, List<CombatLabCostBand> bands)
    {
        foreach (CombatLabCostBand band in bands)
        {
            if (cost >= band.MinCost && cost <= band.MaxCost)
                return band.Id;
        }
        return bands.Count > 0 ? bands[^1].Id : "default";
    }

    private static float ResolveCostForTower(CombatLabTowerBenchmarkSuite suite, string towerId)
    {
        if (suite.CostByTower != null && suite.CostByTower.TryGetValue(towerId, out float configured))
            return Math.Max(0.01f, configured);

        return towerId switch
        {
            "rapid_shooter" => 95f,
            "heavy_cannon" => 130f,
            "rocket_launcher" => 124f,
            "marker_tower" => 90f,
            "chain_tower" => 115f,
            "rift_prism" => 120f,
            "phase_splitter" => 122f,
            "undertow_engine" => 118f,
            _ => 100f
        };
    }

    private static string[] ResolveRolesForTower(CombatLabTowerBenchmarkSuite suite, string towerId)
    {
        if (suite.RoleAssignments != null && suite.RoleAssignments.TryGetValue(towerId, out string[]? roles) && roles != null)
            return roles;

        return towerId switch
        {
            "rapid_shooter" => new[] { "anti_swarm", "generalist" },
            "heavy_cannon" => new[] { "anti_tank", "burst" },
            "rocket_launcher" => new[] { "anti_swarm", "pack_control", "generalist" },
            "marker_tower" => new[] { "support", "control" },
            "chain_tower" => new[] { "anti_swarm", "control" },
            "rift_prism" => new[] { "area_denial", "anti_tank" },
            "phase_splitter" => new[] { "backline_pressure", "control", "generalist" },
            "undertow_engine" => new[] { "control", "support", "backline_pressure" },
            _ => Array.Empty<string>()
        };
    }

    private static bool SharesRole(string[] lhs, string[] rhs)
    {
        if (lhs.Length == 0 || rhs.Length == 0)
            return false;
        var right = new HashSet<string>(rhs, StringComparer.OrdinalIgnoreCase);
        return lhs.Any(role => right.Contains(role));
    }

    private static ComboExplosionSkin ResolveSkinFromMods(List<Modifier> mods)
    {
        if (mods.Count < 2)
            return ComboExplosionSkin.ChainArc;
        return SpectacleExplosionCore.ResolveComboExplosionSkin(mods[0].ModifierId, mods[1].ModifierId);
    }

    private static TargetingMode ResolveTargetingMode(string? value)
    {
        string normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "strongest" => TargetingMode.Strongest,
            "lowest_hp" => TargetingMode.LowestHp,
            _ => TargetingMode.First,
        };
    }

    private static int ComposeSeed(int suiteSeed, string scenarioId, string towerId, int trial)
    {
        unchecked
        {
            int hash = suiteSeed;
            hash = (hash * 397) ^ StableHash(scenarioId);
            hash = (hash * 397) ^ StableHash(towerId);
            hash = (hash * 397) ^ trial;
            return hash == 0 ? 1 : hash;
        }
    }

    private static int StableHash(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        unchecked
        {
            // FNV-1a 32-bit over UTF-16 chars for cross-process deterministic seeding.
            int hash = unchecked((int)2166136261);
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ value[i]) * 16777619;
            return hash;
        }
    }

    private static int CountMod(ITowerView tower, string modId)
    {
        string normalized = SpectacleDefinitions.NormalizeModId(modId);
        int count = 0;
        foreach (Modifier mod in tower.Modifiers)
        {
            if (SpectacleDefinitions.NormalizeModId(mod.ModifierId) == normalized)
                count++;
        }
        return count;
    }

    private static CombatLabTowerAutotuneBounds? ResolveAutotuneBoundsForTower(
        CombatLabTowerBenchmarkAutotuneConfig autotune,
        string towerId)
    {
        if (autotune.PerTowerBounds != null && autotune.PerTowerBounds.TryGetValue(towerId, out CombatLabTowerAutotuneBounds? perTower))
            return perTower ?? autotune.GlobalBounds;
        return autotune.GlobalBounds;
    }

    private static float ClampDelta(float value, float maxAbs, CombatLabNumericBounds? bounds)
    {
        float min = -Math.Abs(maxAbs);
        float max = Math.Abs(maxAbs);
        if (bounds != null)
        {
            min = Math.Max(min, Math.Min(bounds.Min, bounds.Max));
            max = Math.Min(max, Math.Max(bounds.Min, bounds.Max));
        }
        if (min > max)
        {
            float mid = (min + max) * 0.5f;
            min = mid;
            max = mid;
        }

        return Mathf.Clamp(value, min, max);
    }

    private static float ComputeCoefficientOfVariation(List<float> values)
    {
        if (values.Count <= 1)
            return 0f;
        float mean = values.Average();
        if (mean <= 0.0001f)
            return 0f;
        float variance = values.Sum(v =>
        {
            float d = v - mean;
            return d * d;
        }) / values.Count;
        float std = MathF.Sqrt(MathF.Max(0f, variance));
        return std / mean;
    }

    private static float ResolveMedian(List<float> values)
    {
        if (values.Count == 0)
            return 0f;
        values.Sort();
        int mid = values.Count / 2;
        if ((values.Count % 2) == 0)
            return (values[mid - 1] + values[mid]) * 0.5f;
        return values[mid];
    }

    private static float ResolveQuantile(List<float> values, float quantile)
    {
        if (values.Count == 0)
            return 0f;
        if (values.Count == 1)
            return values[0];
        float q = Mathf.Clamp(quantile, 0f, 1f);
        float index = q * (values.Count - 1);
        int lo = (int)MathF.Floor(index);
        int hi = (int)MathF.Ceiling(index);
        if (lo == hi)
            return values[lo];
        float t = index - lo;
        return Mathf.Lerp(values[lo], values[hi], t);
    }

    private static string Csv(string? value)
    {
        string s = value ?? string.Empty;
        bool quote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        if (!quote)
            return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string Csv(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    private static string Csv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
