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
