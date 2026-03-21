using System.Collections.Generic;
using System.Text.Json.Serialization;
using SlotTheory.Core;

namespace SlotTheory.Tools;

public sealed class CombatLabScenarioSuite
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "combat_lab_suite";
    [JsonPropertyName("scenarios")]
    public List<CombatLabScenario> Scenarios { get; set; } = new();
}

public sealed class CombatLabSweepConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "combat_lab_sweep";
    [JsonPropertyName("runs_per_variant")]
    public int RunsPerVariant { get; set; } = 20;
    [JsonPropertyName("scenario_file")]
    public string ScenarioFile { get; set; } = string.Empty;
    [JsonPropertyName("variants")]
    public List<CombatLabSweepVariant> Variants { get; set; } = new();
}

public sealed class CombatLabSweepVariant
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "baseline";
    [JsonPropertyName("tuning")]
    public SpectacleTuningProfile Tuning { get; set; } = new();
}

public sealed class CombatLabScenario
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "scenario";
    [JsonPropertyName("scenario_type")]
    public string ScenarioType { get; set; } = "status_detonation";
    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 1;
    [JsonPropertyName("tower_setup")]
    public List<CombatLabTowerSetup> TowerSetup { get; set; } = new();
    [JsonPropertyName("enemies")]
    public List<CombatLabEnemySetup> Enemies { get; set; } = new();
    [JsonPropertyName("simulate_seconds")]
    public float SimulateSeconds { get; set; } = 4.0f;
    [JsonPropertyName("trigger_damage")]
    public float TriggerDamage { get; set; } = 120f;
    [JsonPropertyName("surge_power")]
    public float SurgePower { get; set; } = 1.20f;
    [JsonPropertyName("global_surge")]
    public bool GlobalSurge { get; set; } = false;
    [JsonPropertyName("reduced_motion")]
    public bool ReducedMotion { get; set; } = false;
    [JsonPropertyName("contributors")]
    public int Contributors { get; set; } = 3;
    [JsonPropertyName("expect")]
    public CombatLabExpectations Expect { get; set; } = new();
}

public sealed class CombatLabTowerSetup
{
    [JsonPropertyName("tower")]
    public string Tower { get; set; } = "arc_emitter";
    [JsonPropertyName("position")]
    public float[] Position { get; set; } = new float[] { 0f, 0f };
    [JsonPropertyName("mods")]
    public string[] Mods { get; set; } = new string[0];
    [JsonPropertyName("base_damage")]
    public float BaseDamage { get; set; } = 36f;
    [JsonPropertyName("attack_interval")]
    public float AttackInterval { get; set; } = 1.0f;
    [JsonPropertyName("range")]
    public float Range { get; set; } = 280f;
}

public sealed class CombatLabEnemySetup
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }
    [JsonPropertyName("type")]
    public string Type { get; set; } = "basic";
    [JsonPropertyName("path_t")]
    public float PathT { get; set; } = 0f;
    [JsonPropertyName("position")]
    public float[]? Position { get; set; }
    [JsonPropertyName("status")]
    public string[] Status { get; set; } = new string[0];
    [JsonPropertyName("hp")]
    public float Hp { get; set; } = 65f;
}

public sealed class CombatLabExpectations
{
    [JsonPropertyName("major_surges")]
    public int? MajorSurges { get; set; }
    [JsonPropertyName("explosion_hits_min")]
    public int? ExplosionHitsMin { get; set; }
    [JsonPropertyName("explosion_hits_max")]
    public int? ExplosionHitsMax { get; set; }
    [JsonPropertyName("residue_count")]
    public int? ResidueCount { get; set; }
    [JsonPropertyName("status_detonations_min")]
    public int? StatusDetonationsMin { get; set; }
    [JsonPropertyName("status_detonations_max")]
    public int? StatusDetonationsMax { get; set; }
    [JsonPropertyName("stage_two_delay_min_seconds")]
    public float? StageTwoDelayMinSeconds { get; set; }
    [JsonPropertyName("stage_two_delay_max_seconds")]
    public float? StageTwoDelayMaxSeconds { get; set; }
    [JsonPropertyName("ripple_monotonic")]
    public bool? RippleMonotonic { get; set; }
    [JsonPropertyName("reduced_motion_collapses_delays")]
    public bool? ReducedMotionCollapsesDelays { get; set; }
    [JsonPropertyName("max_simultaneous_detonations")]
    public int? MaxSimultaneousDetonations { get; set; }
}

public sealed class CombatLabTraceEvent
{
    [JsonPropertyName("timestamp")]
    public float Timestamp { get; set; }
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;
    [JsonPropertyName("enemy_id")]
    public int EnemyId { get; set; } = -1;
    [JsonPropertyName("x")]
    public float X { get; set; }
    [JsonPropertyName("y")]
    public float Y { get; set; }
    [JsonPropertyName("hp_before")]
    public float HpBefore { get; set; }
    [JsonPropertyName("hp_after")]
    public float HpAfter { get; set; }
    [JsonPropertyName("status_tags")]
    public string StatusTags { get; set; } = string.Empty;
    [JsonPropertyName("surge_trigger_id")]
    public string SurgeTriggerId { get; set; } = string.Empty;
    [JsonPropertyName("explosion_stage_id")]
    public string ExplosionStageId { get; set; } = string.Empty;
    [JsonPropertyName("hitstop_requested")]
    public bool HitStopRequested { get; set; }
    [JsonPropertyName("residue_spawned")]
    public bool ResidueSpawned { get; set; }
    [JsonPropertyName("combo_skin")]
    public string ComboSkin { get; set; } = string.Empty;
}

public sealed class CombatLabScenarioMetrics
{
    public int MajorSurges { get; set; }
    public int ExplosionHits { get; set; }
    public int ResidueCount { get; set; }
    public int StatusDetonations { get; set; }
    public float ExplosionDamage { get; set; }
    public float ResidueUptimeSeconds { get; set; }
    public int SimultaneousDetonationPeak { get; set; }
    public int HitStopRequests { get; set; }
}

public sealed class CombatLabScenarioResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public List<string> Failures { get; set; } = new();
    public CombatLabScenarioMetrics Metrics { get; set; } = new();
    public List<CombatLabTraceEvent> Trace { get; set; } = new();
}

public sealed class CombatLabTowerBenchmarkSuite
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "tower_benchmark_suite";
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "base_combat_only";
    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 1337;
    [JsonPropertyName("trials_per_scenario")]
    public int TrialsPerScenario { get; set; } = 5;
    [JsonPropertyName("timestep_seconds")]
    public float TimestepSeconds { get; set; } = 0.05f;
    [JsonPropertyName("include_all_towers")]
    public bool IncludeAllTowers { get; set; } = true;
    [JsonPropertyName("towers")]
    public List<CombatLabTowerBenchmarkTowerSetup> Towers { get; set; } = new();
    [JsonPropertyName("scenarios")]
    public List<CombatLabTowerBenchmarkScenario> Scenarios { get; set; } = new();
    [JsonPropertyName("role_assignments")]
    public Dictionary<string, string[]> RoleAssignments { get; set; } = new();
    [JsonPropertyName("cost_by_tower")]
    public Dictionary<string, float> CostByTower { get; set; } = new();
    [JsonPropertyName("cost_bands")]
    public List<CombatLabCostBand> CostBands { get; set; } = new();
    [JsonPropertyName("autotune")]
    public CombatLabTowerBenchmarkAutotuneConfig? Autotune { get; set; }
}

public sealed class CombatLabTowerBenchmarkTowerSetup
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; } = string.Empty;
    [JsonPropertyName("tower")]
    public string Tower { get; set; } = string.Empty;
    [JsonPropertyName("mods")]
    public string[] Mods { get; set; } = new string[0];
    [JsonPropertyName("targeting")]
    public string Targeting { get; set; } = "first";
    [JsonPropertyName("base_damage_override")]
    public float? BaseDamageOverride { get; set; }
    [JsonPropertyName("attack_interval_override")]
    public float? AttackIntervalOverride { get; set; }
    [JsonPropertyName("range_override")]
    public float? RangeOverride { get; set; }
    [JsonPropertyName("split_count_override")]
    public int? SplitCountOverride { get; set; }
    [JsonPropertyName("chain_count_override")]
    public int? ChainCountOverride { get; set; }
    [JsonPropertyName("cost")]
    public float? Cost { get; set; }
}

public sealed class CombatLabTowerBenchmarkScenario
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "scenario";
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Scenario";
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = new string[0];
    [JsonPropertyName("duration_seconds")]
    public float DurationSeconds { get; set; } = 45f;
    [JsonPropertyName("path_type")]
    public string PathType { get; set; } = "open_lane";
    [JsonPropertyName("path_length")]
    public float PathLength { get; set; } = 1800f;
    [JsonPropertyName("lane_width")]
    public float LaneWidth { get; set; } = 140f;
    [JsonPropertyName("tower_position")]
    public float[] TowerPosition { get; set; } = new float[] { 640f, 0f };
    [JsonPropertyName("stop_when_resolved")]
    public bool StopWhenResolved { get; set; } = true;
    [JsonPropertyName("max_leaks")]
    public int MaxLeaks { get; set; } = 9999;
    [JsonPropertyName("enemy_groups")]
    public List<CombatLabTowerBenchmarkEnemyGroup> EnemyGroups { get; set; } = new();
}

public sealed class CombatLabTowerBenchmarkEnemyGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "group";
    [JsonPropertyName("count")]
    public int Count { get; set; } = 10;
    [JsonPropertyName("hp")]
    public float Hp { get; set; } = 65f;
    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 120f;
    [JsonPropertyName("spawn_interval")]
    public float SpawnInterval { get; set; } = 0.50f;
    [JsonPropertyName("start_time")]
    public float StartTime { get; set; } = 0f;
    [JsonPropertyName("start_progress")]
    public float StartProgress { get; set; } = 0f;
    [JsonPropertyName("lane_spread")]
    public float LaneSpread { get; set; } = 0f;
}

public sealed class CombatLabCostBand
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "band";
    [JsonPropertyName("min_cost")]
    public float MinCost { get; set; } = 0f;
    [JsonPropertyName("max_cost")]
    public float MaxCost { get; set; } = float.MaxValue;
}

public sealed class CombatLabTowerBenchmarkAutotuneConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    [JsonPropertyName("max_percent_delta")]
    public float MaxPercentDelta { get; set; } = 0.08f;
    [JsonPropertyName("target_role_score_min")]
    public float TargetRoleScoreMin { get; set; } = 0.95f;
    [JsonPropertyName("target_role_score_max")]
    public float TargetRoleScoreMax { get; set; } = 1.05f;
    [JsonPropertyName("global_bounds")]
    public CombatLabTowerAutotuneBounds? GlobalBounds { get; set; }
    [JsonPropertyName("per_tower_bounds")]
    public Dictionary<string, CombatLabTowerAutotuneBounds> PerTowerBounds { get; set; } = new();
}

public sealed class CombatLabTowerAutotuneBounds
{
    [JsonPropertyName("damage")]
    public CombatLabNumericBounds? Damage { get; set; }
    [JsonPropertyName("attack_interval")]
    public CombatLabNumericBounds? AttackInterval { get; set; }
    [JsonPropertyName("range")]
    public CombatLabNumericBounds? Range { get; set; }
    [JsonPropertyName("split_count")]
    public CombatLabNumericBounds? SplitCount { get; set; }
    [JsonPropertyName("chain_count")]
    public CombatLabNumericBounds? ChainCount { get; set; }
    [JsonPropertyName("cost")]
    public CombatLabNumericBounds? Cost { get; set; }
}

public sealed class CombatLabNumericBounds
{
    [JsonPropertyName("min")]
    public float Min { get; set; }
    [JsonPropertyName("max")]
    public float Max { get; set; }
}

public sealed class CombatLabTowerBenchmarkScenarioResult
{
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;
    [JsonPropertyName("scenario_name")]
    public string ScenarioName { get; set; } = string.Empty;
    [JsonPropertyName("path_type")]
    public string PathType { get; set; } = string.Empty;
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = new string[0];
    [JsonPropertyName("results")]
    public List<CombatLabTowerBenchmarkTowerResult> Results { get; set; } = new();
}

public sealed class CombatLabTowerBenchmarkTowerResult
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; } = string.Empty;
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;
    [JsonPropertyName("cost")]
    public float Cost { get; set; } = 1f;
    [JsonPropertyName("total_damage")]
    public float TotalDamage { get; set; }
    [JsonPropertyName("effective_dps")]
    public float EffectiveDps { get; set; }
    [JsonPropertyName("cost_efficiency")]
    public float CostEfficiency { get; set; }
    [JsonPropertyName("leak_prevention_value")]
    public float LeakPreventionValue { get; set; }
    [JsonPropertyName("anti_swarm_performance")]
    public float AntiSwarmPerformance { get; set; }
    [JsonPropertyName("anti_tank_performance")]
    public float AntiTankPerformance { get; set; }
    [JsonPropertyName("reliability_variance")]
    public float ReliabilityVariance { get; set; }
    [JsonPropertyName("overkill_waste")]
    public float OverkillWaste { get; set; }
    [JsonPropertyName("average_targets_hit")]
    public float AverageTargetsHit { get; set; }
    [JsonPropertyName("uptime")]
    public float Uptime { get; set; }
    [JsonPropertyName("idle_time_seconds")]
    public float IdleTimeSeconds { get; set; }
    [JsonPropertyName("kill_count")]
    public float KillCount { get; set; }
    [JsonPropertyName("leak_count")]
    public float LeakCount { get; set; }
    [JsonPropertyName("wave_clear_seconds")]
    public float WaveClearSeconds { get; set; }
    [JsonPropertyName("clear_rate")]
    public float ClearRate { get; set; }
    [JsonPropertyName("surges_triggered")]
    public float SurgesTriggered { get; set; }
    [JsonPropertyName("global_surges_triggered")]
    public float GlobalSurgesTriggered { get; set; }
    [JsonPropertyName("edge_spike_rate")]
    public float EdgeSpikeRate { get; set; }
    [JsonPropertyName("normalized_global")]
    public float NormalizedGlobal { get; set; }
    [JsonPropertyName("normalized_cost_band")]
    public float NormalizedCostBand { get; set; }
    [JsonPropertyName("normalized_role")]
    public float NormalizedRole { get; set; }
}

public sealed class CombatLabTowerBenchmarkProfile
{
    [JsonPropertyName("case_id")]
    public string CaseId { get; set; } = string.Empty;
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;
    [JsonPropertyName("cost")]
    public float Cost { get; set; } = 1f;
    [JsonPropertyName("cost_band")]
    public string CostBand { get; set; } = "default";
    [JsonPropertyName("roles")]
    public string[] Roles { get; set; } = new string[0];
    [JsonPropertyName("scenario_count")]
    public int ScenarioCount { get; set; }
    [JsonPropertyName("avg_normalized_global")]
    public float AvgNormalizedGlobal { get; set; }
    [JsonPropertyName("avg_normalized_cost_band")]
    public float AvgNormalizedCostBand { get; set; }
    [JsonPropertyName("avg_normalized_role")]
    public float AvgNormalizedRole { get; set; }
    [JsonPropertyName("map_path_sensitivity")]
    public float MapPathSensitivity { get; set; }
    [JsonPropertyName("aggregate_reliability_variance")]
    public float AggregateReliabilityVariance { get; set; }
    [JsonPropertyName("flags")]
    public List<string> Flags { get; set; } = new();
}

public sealed class CombatLabTowerBenchmarkSuggestion
{
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("suggested_deltas")]
    public Dictionary<string, float> SuggestedDeltas { get; set; } = new();
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatLabTowerBenchmarkReport
{
    [JsonPropertyName("suite")]
    public string Suite { get; set; } = string.Empty;
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
    [JsonPropertyName("generated_utc")]
    public System.DateTime GeneratedUtc { get; set; } = System.DateTime.UtcNow;
    [JsonPropertyName("scenario_results")]
    public List<CombatLabTowerBenchmarkScenarioResult> ScenarioResults { get; set; } = new();
    [JsonPropertyName("tower_profiles")]
    public List<CombatLabTowerBenchmarkProfile> TowerProfiles { get; set; } = new();
    [JsonPropertyName("tuning_suggestions")]
    public List<CombatLabTowerBenchmarkSuggestion> TuningSuggestions { get; set; } = new();
}

public sealed class CombatLabModifierBenchmarkSuite
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "modifier_benchmark_suite";
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "base_combat_only";
    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 1337;
    [JsonPropertyName("trials_per_scenario")]
    public int TrialsPerScenario { get; set; } = 5;
    [JsonPropertyName("timestep_seconds")]
    public float TimestepSeconds { get; set; } = 0.05f;
    [JsonPropertyName("include_all_modifiers")]
    public bool IncludeAllModifiers { get; set; } = true;
    [JsonPropertyName("include_all_towers")]
    public bool IncludeAllTowers { get; set; } = true;
    [JsonPropertyName("modifiers")]
    public string[] Modifiers { get; set; } = new string[0];
    [JsonPropertyName("contexts")]
    public List<CombatLabModifierBenchmarkContext> Contexts { get; set; } = new();
    [JsonPropertyName("scenarios")]
    public List<CombatLabTowerBenchmarkScenario> Scenarios { get; set; } = new();
    [JsonPropertyName("pair_probes")]
    public List<CombatLabModifierPairProbe> PairProbes { get; set; } = new();
    [JsonPropertyName("cost_by_tower")]
    public Dictionary<string, float> CostByTower { get; set; } = new();
    [JsonPropertyName("cost_bands")]
    public List<CombatLabCostBand> CostBands { get; set; } = new();
    [JsonPropertyName("compatibility_by_modifier")]
    public Dictionary<string, string[]> CompatibilityByModifier { get; set; } = new();
    [JsonPropertyName("intent_tags_by_modifier")]
    public Dictionary<string, string[]> IntentTagsByModifier { get; set; } = new();
    [JsonPropertyName("thresholds")]
    public CombatLabModifierThresholds Thresholds { get; set; } = new();
    [JsonPropertyName("autotune")]
    public CombatLabModifierBenchmarkAutotuneConfig? Autotune { get; set; }
}

public sealed class CombatLabModifierThresholds
{
    [JsonPropertyName("weak_avg_damage_delta_pct")]
    public float WeakAvgDamageDeltaPct { get; set; } = 0.04f;
    [JsonPropertyName("strong_avg_damage_delta_pct")]
    public float StrongAvgDamageDeltaPct { get; set; } = 0.24f;
    [JsonPropertyName("trap_uselessness_rate")]
    public float TrapUselessnessRate { get; set; } = 0.60f;
    [JsonPropertyName("useless_delta_pct")]
    public float UselessDeltaPct { get; set; } = 0.015f;
    [JsonPropertyName("abuse_damage_delta_pct")]
    public float AbuseDamageDeltaPct { get; set; } = 0.40f;
    [JsonPropertyName("abuse_leak_reduction_delta")]
    public float AbuseLeakReductionDelta { get; set; } = 0.08f;
    [JsonPropertyName("edge_case_peak_delta_pct")]
    public float EdgeCasePeakDeltaPct { get; set; } = 0.35f;
    [JsonPropertyName("edge_case_worst_delta_pct")]
    public float EdgeCaseWorstDeltaPct { get; set; } = 0.02f;
}

public sealed class CombatLabModifierBenchmarkContext
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("tower")]
    public string Tower { get; set; } = string.Empty;
    [JsonPropertyName("baseline_mods")]
    public string[] BaselineMods { get; set; } = new string[0];
    [JsonPropertyName("targeting")]
    public string Targeting { get; set; } = "first";
    [JsonPropertyName("base_damage_override")]
    public float? BaseDamageOverride { get; set; }
    [JsonPropertyName("attack_interval_override")]
    public float? AttackIntervalOverride { get; set; }
    [JsonPropertyName("range_override")]
    public float? RangeOverride { get; set; }
    [JsonPropertyName("split_count_override")]
    public int? SplitCountOverride { get; set; }
    [JsonPropertyName("chain_count_override")]
    public int? ChainCountOverride { get; set; }
    [JsonPropertyName("cost")]
    public float? Cost { get; set; }
}

public sealed class CombatLabModifierPairProbe
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("context_id")]
    public string ContextId { get; set; } = string.Empty;
    [JsonPropertyName("modifier_a")]
    public string ModifierA { get; set; } = string.Empty;
    [JsonPropertyName("modifier_b")]
    public string ModifierB { get; set; } = string.Empty;
    [JsonPropertyName("scenario_ids")]
    public string[] ScenarioIds { get; set; } = new string[0];
}

public sealed class CombatLabModifierBenchmarkAutotuneConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    [JsonPropertyName("max_percent_delta")]
    public float MaxPercentDelta { get; set; } = 0.08f;
    [JsonPropertyName("target_avg_damage_delta_pct_min")]
    public float TargetAvgDamageDeltaPctMin { get; set; } = 0.08f;
    [JsonPropertyName("target_avg_damage_delta_pct_max")]
    public float TargetAvgDamageDeltaPctMax { get; set; } = 0.22f;
    [JsonPropertyName("global_bounds")]
    public CombatLabModifierAutotuneBounds? GlobalBounds { get; set; }
    [JsonPropertyName("per_modifier_bounds")]
    public Dictionary<string, CombatLabModifierAutotuneBounds> PerModifierBounds { get; set; } = new();
}

public sealed class CombatLabModifierAutotuneBounds
{
    [JsonPropertyName("bonus")]
    public CombatLabNumericBounds? Bonus { get; set; }
    [JsonPropertyName("flat_bonus")]
    public CombatLabNumericBounds? FlatBonus { get; set; }
    [JsonPropertyName("proc_chance")]
    public CombatLabNumericBounds? ProcChance { get; set; }
    [JsonPropertyName("proc_strength")]
    public CombatLabNumericBounds? ProcStrength { get; set; }
    [JsonPropertyName("duration")]
    public CombatLabNumericBounds? Duration { get; set; }
    [JsonPropertyName("radius")]
    public CombatLabNumericBounds? Radius { get; set; }
    [JsonPropertyName("stack_cap")]
    public CombatLabNumericBounds? StackCap { get; set; }
    [JsonPropertyName("cooldown")]
    public CombatLabNumericBounds? Cooldown { get; set; }
    [JsonPropertyName("penalty")]
    public CombatLabNumericBounds? Penalty { get; set; }
}

public sealed class CombatLabModifierBenchmarkDeltaRow
{
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;
    [JsonPropertyName("scenario_name")]
    public string ScenarioName { get; set; } = string.Empty;
    [JsonPropertyName("path_type")]
    public string PathType { get; set; } = string.Empty;
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = new string[0];
    [JsonPropertyName("context_id")]
    public string ContextId { get; set; } = string.Empty;
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;
    [JsonPropertyName("modifier_id")]
    public string ModifierId { get; set; } = string.Empty;
    [JsonPropertyName("baseline_mods")]
    public string[] BaselineMods { get; set; } = new string[0];
    [JsonPropertyName("tested_mods")]
    public string[] TestedMods { get; set; } = new string[0];
    [JsonPropertyName("baseline_damage")]
    public float BaselineDamage { get; set; }
    [JsonPropertyName("modified_damage")]
    public float ModifiedDamage { get; set; }
    [JsonPropertyName("delta_damage")]
    public float DeltaDamage { get; set; }
    [JsonPropertyName("delta_damage_pct")]
    public float DeltaDamagePct { get; set; }
    [JsonPropertyName("baseline_dps")]
    public float BaselineDps { get; set; }
    [JsonPropertyName("modified_dps")]
    public float ModifiedDps { get; set; }
    [JsonPropertyName("delta_dps")]
    public float DeltaDps { get; set; }
    [JsonPropertyName("delta_dps_pct")]
    public float DeltaDpsPct { get; set; }
    [JsonPropertyName("baseline_kills")]
    public float BaselineKills { get; set; }
    [JsonPropertyName("modified_kills")]
    public float ModifiedKills { get; set; }
    [JsonPropertyName("delta_kills")]
    public float DeltaKills { get; set; }
    [JsonPropertyName("baseline_leaks")]
    public float BaselineLeaks { get; set; }
    [JsonPropertyName("modified_leaks")]
    public float ModifiedLeaks { get; set; }
    [JsonPropertyName("delta_leaks")]
    public float DeltaLeaks { get; set; }
    [JsonPropertyName("baseline_leak_prevention")]
    public float BaselineLeakPrevention { get; set; }
    [JsonPropertyName("modified_leak_prevention")]
    public float ModifiedLeakPrevention { get; set; }
    [JsonPropertyName("delta_leak_prevention")]
    public float DeltaLeakPrevention { get; set; }
    [JsonPropertyName("baseline_wave_clear_seconds")]
    public float BaselineWaveClearSeconds { get; set; }
    [JsonPropertyName("modified_wave_clear_seconds")]
    public float ModifiedWaveClearSeconds { get; set; }
    [JsonPropertyName("delta_wave_clear_seconds")]
    public float DeltaWaveClearSeconds { get; set; }
    [JsonPropertyName("baseline_overkill_waste")]
    public float BaselineOverkillWaste { get; set; }
    [JsonPropertyName("modified_overkill_waste")]
    public float ModifiedOverkillWaste { get; set; }
    [JsonPropertyName("delta_overkill_waste")]
    public float DeltaOverkillWaste { get; set; }
    [JsonPropertyName("baseline_uptime")]
    public float BaselineUptime { get; set; }
    [JsonPropertyName("modified_uptime")]
    public float ModifiedUptime { get; set; }
    [JsonPropertyName("delta_uptime")]
    public float DeltaUptime { get; set; }
    [JsonPropertyName("baseline_targets_hit")]
    public float BaselineTargetsHit { get; set; }
    [JsonPropertyName("modified_targets_hit")]
    public float ModifiedTargetsHit { get; set; }
    [JsonPropertyName("delta_targets_hit")]
    public float DeltaTargetsHit { get; set; }
    [JsonPropertyName("baseline_reliability")]
    public float BaselineReliability { get; set; }
    [JsonPropertyName("modified_reliability")]
    public float ModifiedReliability { get; set; }
    [JsonPropertyName("delta_reliability")]
    public float DeltaReliability { get; set; }
    [JsonPropertyName("baseline_surges")]
    public float BaselineSurges { get; set; }
    [JsonPropertyName("modified_surges")]
    public float ModifiedSurges { get; set; }
    [JsonPropertyName("delta_surges")]
    public float DeltaSurges { get; set; }
    [JsonPropertyName("baseline_global_surges")]
    public float BaselineGlobalSurges { get; set; }
    [JsonPropertyName("modified_global_surges")]
    public float ModifiedGlobalSurges { get; set; }
    [JsonPropertyName("delta_global_surges")]
    public float DeltaGlobalSurges { get; set; }
    [JsonPropertyName("range_value_delta")]
    public float RangeValueDelta { get; set; }
}

public sealed class CombatLabModifierPairResult
{
    [JsonPropertyName("probe_id")]
    public string ProbeId { get; set; } = string.Empty;
    [JsonPropertyName("scenario_id")]
    public string ScenarioId { get; set; } = string.Empty;
    [JsonPropertyName("context_id")]
    public string ContextId { get; set; } = string.Empty;
    [JsonPropertyName("tower_id")]
    public string TowerId { get; set; } = string.Empty;
    [JsonPropertyName("modifier_a")]
    public string ModifierA { get; set; } = string.Empty;
    [JsonPropertyName("modifier_b")]
    public string ModifierB { get; set; } = string.Empty;
    [JsonPropertyName("pair_delta_damage_pct")]
    public float PairDeltaDamagePct { get; set; }
    [JsonPropertyName("expected_additive_damage_pct")]
    public float ExpectedAdditiveDamagePct { get; set; }
    [JsonPropertyName("synergy_excess_pct")]
    public float SynergyExcessPct { get; set; }
    [JsonPropertyName("pair_delta_leak_prevention")]
    public float PairDeltaLeakPrevention { get; set; }
    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty;
}

public sealed class CombatLabModifierBenchmarkProfile
{
    [JsonPropertyName("modifier_id")]
    public string ModifierId { get; set; } = string.Empty;
    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty;
    [JsonPropertyName("scenario_count")]
    public int ScenarioCount { get; set; }
    [JsonPropertyName("context_count")]
    public int ContextCount { get; set; }
    [JsonPropertyName("best_case_gain_pct")]
    public float BestCaseGainPct { get; set; }
    [JsonPropertyName("avg_gain_pct")]
    public float AvgGainPct { get; set; }
    [JsonPropertyName("worst_case_gain_pct")]
    public float WorstCaseGainPct { get; set; }
    [JsonPropertyName("compatibility_spread")]
    public float CompatibilitySpread { get; set; }
    [JsonPropertyName("uselessness_rate")]
    public float UselessnessRate { get; set; }
    [JsonPropertyName("abuse_potential")]
    public float AbusePotential { get; set; }
    [JsonPropertyName("avg_delta_kills")]
    public float AvgDeltaKills { get; set; }
    [JsonPropertyName("avg_delta_leaks")]
    public float AvgDeltaLeaks { get; set; }
    [JsonPropertyName("avg_delta_wave_clear_seconds")]
    public float AvgDeltaWaveClearSeconds { get; set; }
    [JsonPropertyName("avg_delta_overkill")]
    public float AvgDeltaOverkill { get; set; }
    [JsonPropertyName("avg_delta_uptime")]
    public float AvgDeltaUptime { get; set; }
    [JsonPropertyName("avg_delta_targets_hit")]
    public float AvgDeltaTargetsHit { get; set; }
    [JsonPropertyName("avg_delta_reliability")]
    public float AvgDeltaReliability { get; set; }
    [JsonPropertyName("flags")]
    public List<string> Flags { get; set; } = new();
}

public sealed class CombatLabModifierBenchmarkSuggestion
{
    [JsonPropertyName("modifier_id")]
    public string ModifierId { get; set; } = string.Empty;
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
    [JsonPropertyName("suggested_deltas")]
    public Dictionary<string, float> SuggestedDeltas { get; set; } = new();
    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
    [JsonPropertyName("requires_structural_change")]
    public bool RequiresStructuralChange { get; set; }
}

public sealed class CombatLabModifierBenchmarkReport
{
    [JsonPropertyName("suite")]
    public string Suite { get; set; } = string.Empty;
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;
    [JsonPropertyName("generated_utc")]
    public System.DateTime GeneratedUtc { get; set; } = System.DateTime.UtcNow;
    [JsonPropertyName("delta_rows")]
    public List<CombatLabModifierBenchmarkDeltaRow> DeltaRows { get; set; } = new();
    [JsonPropertyName("pair_rows")]
    public List<CombatLabModifierPairResult> PairRows { get; set; } = new();
    [JsonPropertyName("modifier_profiles")]
    public List<CombatLabModifierBenchmarkProfile> ModifierProfiles { get; set; } = new();
    [JsonPropertyName("tuning_suggestions")]
    public List<CombatLabModifierBenchmarkSuggestion> TuningSuggestions { get; set; } = new();
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
