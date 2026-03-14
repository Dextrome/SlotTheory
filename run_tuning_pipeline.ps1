# run_tuning_pipeline.ps1
#
# Automated tuning pipeline with true iterative optimization:
# 1) Baseline bot metrics (once)
# 2) Evaluate starting tuning profile (seed)
# 3) For each iteration:
#    - Generate candidate tuning profiles
#    - Run bot metrics per candidate
#    - Score candidates and keep the best
#    - Validate iteration best with scenario suite (only when global best improves)
#    - Compare baseline vs iteration-best via generated sweep (only when global best improves)
# 4) Final validation/reporting with global best:
#    - Scenario suite
#    - Sweep comparison
#    - Tuned bot metrics
#    - Optional live bot traces
#    - Baseline vs tuned delta report
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1
#     (auto-generates seed_current.json from current runtime settings)
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Runs 60 -Iterations 6 -CandidatesPerIteration 4
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -StrategySet optimization
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SkipBuild -SkipTrace
#
# TODO(perf-step-3): Add multi-fidelity candidate evaluation (coarse runs for all candidates, full runs for shortlisted candidates).
# TODO(perf-step-4): Add a dedicated eval shard parallelism parameter (separate from CandidateParallelism).
# TODO(perf-step-5): Add deterministic metrics cache keyed by tuning hash + runs + strategy_set + run_index_offset.
# TODO(perf-step-6): Add explicit "search mode" defaults/presets plus a final confirmation pass mode.

[CmdletBinding()]
param(
    [int]$Runs = 120,
    [Alias("TuningIterations")][int]$Iterations = 4,
    [int]$CandidatesPerIteration = 3,
    [int]$CandidateParallelism = 1,
    [int]$SweepRunsPerVariant = 12,
    [int]$Seed = 1337,
    [double]$MutationStrength = 1.0,
    [double]$TargetExplosionShare = 0.05,
    [double]$TargetExplosionShareTolerance = 0.12,
    [double]$TargetWinRateEasy = 0.95,
    [double]$TargetWinRateNormal = 0.75,
    [double]$TargetWinRateHard = 0.45,
    [bool]$UseBaselineRelativeWinTargets = $true,
    [double]$RelativeWinTargetEasyUplift = 0.05,
    [double]$RelativeWinTargetNormalUplift = 0.08,
    [double]$RelativeWinTargetHardUplift = 0.06,
    [double]$TargetWinRateTolerance = 0.06,
    [double]$DifficultyRegressionTolerance = 0.01,
    [double]$NormalRegressionPenaltyWeight = 260.0,
    [double]$HardRegressionPenaltyWeight = 320.0,
    [double]$TargetMaxTowerSurgeRatio = 2.0,
    [double]$TargetMaxSurgesPerRun = 50.0,
    [double]$TargetMaxSurgesPerRunTolerance = 2.0,
    [double]$SurgesPerRunPenaltyWeight = 6.0,
    [double]$MinSweepScoreRatioVsBaseline = 1.0,
    [int]$MinTowerPlacementsForParity = 6,
    [double]$MaxChainDepth = 4.0,
    [int]$MaxSimultaneousExplosions = 8,
    [int]$MaxSimultaneousHazards = 12,
    [int]$MaxSimultaneousHitStops = 4,
    [double]$MinRunDurationSeconds = 900.0,
    [int]$TopCandidateReevalCount = 3,
    [string]$StrategySet = "optimization",
    [string]$GodotPath = "",
    [string]$TuningFile = "",
    [string]$ScenarioFile = "Data/combat_lab/core_scenarios.json",
    [string]$OutputRoot = "release/tuning_pipeline",
    [switch]$SkipBuild,
    [switch]$SkipTrace
)

$ErrorActionPreference = "Stop"

function Resolve-PathFromProject {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectRoot,
        [Parameter(Mandatory = $true)][string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $PathValue))
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (!(Test-Path $PathValue)) {
        throw "$Label not found: $PathValue"
    }
}

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    New-Item -ItemType Directory -Path $PathValue -Force | Out-Null
}

function Invoke-GodotCommand {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$Args,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Write-Host ""
    Write-Host "=== $Label ==="
    & $GodotExe @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed ($Label). Godot exit code: $LASTEXITCODE"
    }
}

function Clamp-Double {
    param(
        [double]$Value,
        [double]$Min,
        [double]$Max
    )

    if ($Value -lt $Min) { return $Min }
    if ($Value -gt $Max) { return $Max }
    return $Value
}

function New-NeutralTuningProfile {
    return [PSCustomObject]@{
        enable_overkill_bloom = $true
        enable_status_detonation = $true
        enable_residue = $true
        overkill_bloom_damage_scale_multiplier = 1.0
        overkill_bloom_radius_multiplier = 1.0
        overkill_bloom_threshold_multiplier = 1.0
        overkill_bloom_max_targets_multiplier = 1.0
        detonation_max_targets_multiplier = 1.0
        detonation_stagger_multiplier = 1.0
        status_detonation_damage_multiplier = 1.0
        residue_duration_multiplier = 1.0
        residue_potency_multiplier = 1.0
        residue_damage_multiplier = 1.0
        residue_tick_interval_multiplier = 1.0
        residue_max_active_multiplier = 1.0
        explosion_followup_damage_multiplier = 1.0
        meter_gain_multiplier = 1.0
        surge_threshold_multiplier = 1.0
        surge_cooldown_multiplier = 1.0
        surge_meter_after_trigger_multiplier = 1.0
        global_meter_per_surge_multiplier = 1.0
        global_threshold_multiplier = 1.0
        global_meter_after_trigger_multiplier = 1.0
        global_contribution_window_multiplier = 1.0
        inactivity_grace_multiplier = 1.0
        inactivity_decay_multiplier = 1.0
        contribution_window_multiplier = 1.0
        role_lock_meter_threshold_multiplier = 1.0
        meter_damage_reference_multiplier = 1.0
        meter_damage_weight_multiplier = 1.0
        meter_damage_min_clamp_multiplier = 1.0
        meter_damage_max_clamp_multiplier = 1.0
        token_cap_multiplier = 1.0
        token_regen_multiplier = 1.0
        copy_multiplier_scale = 1.0
        diversity_multiplier_scale = 1.0
        event_scalar_multiplier = 1.0
        second_stage_power_threshold = 0.95
        normal_enemy_hp_multiplier = 1.24
        normal_enemy_count_multiplier = 1.1
        normal_spawn_interval_multiplier = 0.95
        hard_enemy_hp_multiplier = 1.40
        hard_enemy_count_multiplier = 1.15
        hard_spawn_interval_multiplier = 0.90
        gain_multipliers = [PSCustomObject]@{
            overkill = 1.0
            chain_reaction = 1.0
            split_shot = 1.0
        }
        event_scalar_multipliers = [PSCustomObject]@{
            overkill = 1.0
            chain_reaction = 1.0
            split_shot = 1.0
            feedback_loop = 1.0
            hair_trigger = 1.0
            slow = 1.0
            exploit_weakness = 1.0
            focus_lens = 1.0
            momentum = 1.0
            overreach = 1.0
        }
        token_cap_multipliers = [PSCustomObject]@{
            overkill = 1.0
            chain_reaction = 1.0
            split_shot = 1.0
            feedback_loop = 1.0
            hair_trigger = 1.0
            slow = 1.0
            exploit_weakness = 1.0
            focus_lens = 1.0
            momentum = 1.0
            overreach = 1.0
        }
        token_regen_multipliers = [PSCustomObject]@{
            overkill = 1.0
            chain_reaction = 1.0
            split_shot = 1.0
            feedback_loop = 1.0
            hair_trigger = 1.0
            slow = 1.0
            exploit_weakness = 1.0
            focus_lens = 1.0
            momentum = 1.0
            overreach = 1.0
        }
    }
}

function Clone-TuningProfile {
    param([Parameter(Mandatory = $true)][object]$Profile)
    return ($Profile | ConvertTo-Json -Depth 12 | ConvertFrom-Json)
}

function Normalize-TuningProfile {
    param([Parameter(Mandatory = $true)][object]$InputProfile)

    $p = New-NeutralTuningProfile
    $props = $InputProfile.PSObject.Properties.Name

    if ($props -contains "enable_overkill_bloom") { $p.enable_overkill_bloom = [bool]$InputProfile.enable_overkill_bloom }
    if ($props -contains "enable_status_detonation") { $p.enable_status_detonation = [bool]$InputProfile.enable_status_detonation }
    if ($props -contains "enable_residue") { $p.enable_residue = [bool]$InputProfile.enable_residue }

    if ($props -contains "overkill_bloom_damage_scale_multiplier") { $p.overkill_bloom_damage_scale_multiplier = [double]$InputProfile.overkill_bloom_damage_scale_multiplier }
    if ($props -contains "overkill_bloom_radius_multiplier") { $p.overkill_bloom_radius_multiplier = [double]$InputProfile.overkill_bloom_radius_multiplier }
    if ($props -contains "overkill_bloom_threshold_multiplier") { $p.overkill_bloom_threshold_multiplier = [double]$InputProfile.overkill_bloom_threshold_multiplier }
    if ($props -contains "overkill_bloom_max_targets_multiplier") { $p.overkill_bloom_max_targets_multiplier = [double]$InputProfile.overkill_bloom_max_targets_multiplier }
    if ($props -contains "detonation_max_targets_multiplier") { $p.detonation_max_targets_multiplier = [double]$InputProfile.detonation_max_targets_multiplier }
    if ($props -contains "detonation_stagger_multiplier") { $p.detonation_stagger_multiplier = [double]$InputProfile.detonation_stagger_multiplier }
    if ($props -contains "status_detonation_damage_multiplier") { $p.status_detonation_damage_multiplier = [double]$InputProfile.status_detonation_damage_multiplier }
    if ($props -contains "residue_duration_multiplier") { $p.residue_duration_multiplier = [double]$InputProfile.residue_duration_multiplier }
    if ($props -contains "residue_potency_multiplier") { $p.residue_potency_multiplier = [double]$InputProfile.residue_potency_multiplier }
    if ($props -contains "residue_damage_multiplier") { $p.residue_damage_multiplier = [double]$InputProfile.residue_damage_multiplier }
    if ($props -contains "residue_tick_interval_multiplier") { $p.residue_tick_interval_multiplier = [double]$InputProfile.residue_tick_interval_multiplier }
    if ($props -contains "residue_max_active_multiplier") { $p.residue_max_active_multiplier = [double]$InputProfile.residue_max_active_multiplier }
    if ($props -contains "explosion_followup_damage_multiplier") { $p.explosion_followup_damage_multiplier = [double]$InputProfile.explosion_followup_damage_multiplier }
    if ($props -contains "meter_gain_multiplier") { $p.meter_gain_multiplier = [double]$InputProfile.meter_gain_multiplier }
    if ($props -contains "surge_threshold_multiplier") { $p.surge_threshold_multiplier = [double]$InputProfile.surge_threshold_multiplier }
    if ($props -contains "surge_cooldown_multiplier") { $p.surge_cooldown_multiplier = [double]$InputProfile.surge_cooldown_multiplier }
    if ($props -contains "surge_meter_after_trigger_multiplier") { $p.surge_meter_after_trigger_multiplier = [double]$InputProfile.surge_meter_after_trigger_multiplier }
    if ($props -contains "global_meter_per_surge_multiplier") { $p.global_meter_per_surge_multiplier = [double]$InputProfile.global_meter_per_surge_multiplier }
    if ($props -contains "global_threshold_multiplier") { $p.global_threshold_multiplier = [double]$InputProfile.global_threshold_multiplier }
    if ($props -contains "global_meter_after_trigger_multiplier") { $p.global_meter_after_trigger_multiplier = [double]$InputProfile.global_meter_after_trigger_multiplier }
    if ($props -contains "global_contribution_window_multiplier") { $p.global_contribution_window_multiplier = [double]$InputProfile.global_contribution_window_multiplier }
    if ($props -contains "inactivity_grace_multiplier") { $p.inactivity_grace_multiplier = [double]$InputProfile.inactivity_grace_multiplier }
    if ($props -contains "inactivity_decay_multiplier") { $p.inactivity_decay_multiplier = [double]$InputProfile.inactivity_decay_multiplier }
    if ($props -contains "contribution_window_multiplier") { $p.contribution_window_multiplier = [double]$InputProfile.contribution_window_multiplier }
    if ($props -contains "role_lock_meter_threshold_multiplier") { $p.role_lock_meter_threshold_multiplier = [double]$InputProfile.role_lock_meter_threshold_multiplier }
    if ($props -contains "meter_damage_reference_multiplier") { $p.meter_damage_reference_multiplier = [double]$InputProfile.meter_damage_reference_multiplier }
    if ($props -contains "meter_damage_weight_multiplier") { $p.meter_damage_weight_multiplier = [double]$InputProfile.meter_damage_weight_multiplier }
    if ($props -contains "meter_damage_min_clamp_multiplier") { $p.meter_damage_min_clamp_multiplier = [double]$InputProfile.meter_damage_min_clamp_multiplier }
    if ($props -contains "meter_damage_max_clamp_multiplier") { $p.meter_damage_max_clamp_multiplier = [double]$InputProfile.meter_damage_max_clamp_multiplier }
    if ($props -contains "token_cap_multiplier") { $p.token_cap_multiplier = [double]$InputProfile.token_cap_multiplier }
    if ($props -contains "token_regen_multiplier") { $p.token_regen_multiplier = [double]$InputProfile.token_regen_multiplier }
    if ($props -contains "copy_multiplier_scale") { $p.copy_multiplier_scale = [double]$InputProfile.copy_multiplier_scale }
    if ($props -contains "diversity_multiplier_scale") { $p.diversity_multiplier_scale = [double]$InputProfile.diversity_multiplier_scale }
    if ($props -contains "event_scalar_multiplier") { $p.event_scalar_multiplier = [double]$InputProfile.event_scalar_multiplier }
    if ($props -contains "second_stage_power_threshold") { $p.second_stage_power_threshold = [double]$InputProfile.second_stage_power_threshold }
    if ($props -contains "normal_enemy_hp_multiplier") { $p.normal_enemy_hp_multiplier = [double]$InputProfile.normal_enemy_hp_multiplier }
    if ($props -contains "normal_enemy_count_multiplier") { $p.normal_enemy_count_multiplier = [double]$InputProfile.normal_enemy_count_multiplier }
    if ($props -contains "normal_spawn_interval_multiplier") { $p.normal_spawn_interval_multiplier = [double]$InputProfile.normal_spawn_interval_multiplier }
    if ($props -contains "hard_enemy_hp_multiplier") { $p.hard_enemy_hp_multiplier = [double]$InputProfile.hard_enemy_hp_multiplier }
    if ($props -contains "hard_enemy_count_multiplier") { $p.hard_enemy_count_multiplier = [double]$InputProfile.hard_enemy_count_multiplier }
    if ($props -contains "hard_spawn_interval_multiplier") { $p.hard_spawn_interval_multiplier = [double]$InputProfile.hard_spawn_interval_multiplier }

    if ($props -contains "gain_multipliers" -and $InputProfile.gain_multipliers -ne $null) {
        $gm = $InputProfile.gain_multipliers.PSObject.Properties.Name
        if ($gm -contains "overkill") { $p.gain_multipliers.overkill = [double]$InputProfile.gain_multipliers.overkill }
        if ($gm -contains "chain_reaction") { $p.gain_multipliers.chain_reaction = [double]$InputProfile.gain_multipliers.chain_reaction }
        if ($gm -contains "split_shot") { $p.gain_multipliers.split_shot = [double]$InputProfile.gain_multipliers.split_shot }
    }
    if ($props -contains "event_scalar_multipliers" -and $InputProfile.event_scalar_multipliers -ne $null) {
        $esm = $InputProfile.event_scalar_multipliers.PSObject.Properties.Name
        foreach ($name in $esm) {
            $p.event_scalar_multipliers.$name = [double]$InputProfile.event_scalar_multipliers.$name
        }
    }
    if ($props -contains "token_cap_multipliers" -and $InputProfile.token_cap_multipliers -ne $null) {
        $tcm = $InputProfile.token_cap_multipliers.PSObject.Properties.Name
        foreach ($name in $tcm) {
            $p.token_cap_multipliers.$name = [double]$InputProfile.token_cap_multipliers.$name
        }
    }
    if ($props -contains "token_regen_multipliers" -and $InputProfile.token_regen_multipliers -ne $null) {
        $trm = $InputProfile.token_regen_multipliers.PSObject.Properties.Name
        foreach ($name in $trm) {
            $p.token_regen_multipliers.$name = [double]$InputProfile.token_regen_multipliers.$name
        }
    }

    $p.overkill_bloom_damage_scale_multiplier = [Math]::Round((Clamp-Double -Value $p.overkill_bloom_damage_scale_multiplier -Min 0.0 -Max 4.0), 4)
    $p.overkill_bloom_radius_multiplier = [Math]::Round((Clamp-Double -Value $p.overkill_bloom_radius_multiplier -Min 0.1 -Max 4.0), 4)
    $p.overkill_bloom_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.overkill_bloom_threshold_multiplier -Min 0.1 -Max 4.0), 4)
    $p.overkill_bloom_max_targets_multiplier = [Math]::Round((Clamp-Double -Value $p.overkill_bloom_max_targets_multiplier -Min 0.1 -Max 3.0), 4)
    $p.detonation_max_targets_multiplier = [Math]::Round((Clamp-Double -Value $p.detonation_max_targets_multiplier -Min 0.1 -Max 4.0), 4)
    $p.detonation_stagger_multiplier = [Math]::Round((Clamp-Double -Value $p.detonation_stagger_multiplier -Min 0.1 -Max 4.0), 4)
    $p.status_detonation_damage_multiplier = [Math]::Round((Clamp-Double -Value $p.status_detonation_damage_multiplier -Min 0.0 -Max 6.0), 4)
    $p.residue_duration_multiplier = [Math]::Round((Clamp-Double -Value $p.residue_duration_multiplier -Min 0.1 -Max 4.0), 4)
    $p.residue_potency_multiplier = [Math]::Round((Clamp-Double -Value $p.residue_potency_multiplier -Min 0.1 -Max 4.0), 4)
    $p.residue_damage_multiplier = [Math]::Round((Clamp-Double -Value $p.residue_damage_multiplier -Min 0.0 -Max 6.0), 4)
    $p.residue_tick_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.residue_tick_interval_multiplier -Min 0.2 -Max 4.0), 4)
    $p.residue_max_active_multiplier = [Math]::Round((Clamp-Double -Value $p.residue_max_active_multiplier -Min 0.1 -Max 4.0), 4)
    $p.explosion_followup_damage_multiplier = [Math]::Round((Clamp-Double -Value $p.explosion_followup_damage_multiplier -Min 0.0 -Max 6.0), 4)
    # Guardrail: keep key meter/global knobs in tighter bands.
    $p.meter_gain_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_gain_multiplier -Min 0.75 -Max 1.60), 4)
    $p.surge_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_threshold_multiplier -Min 0.75 -Max 1.35), 4)
    $p.surge_cooldown_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_cooldown_multiplier -Min 0.75 -Max 1.80), 4)
    $p.surge_meter_after_trigger_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_meter_after_trigger_multiplier -Min 0.75 -Max 1.35), 4)
    $p.global_meter_per_surge_multiplier = [Math]::Round((Clamp-Double -Value $p.global_meter_per_surge_multiplier -Min 0.75 -Max 1.80), 4)
    $p.global_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.global_threshold_multiplier -Min 0.75 -Max 1.35), 4)
    $p.global_meter_after_trigger_multiplier = [Math]::Round((Clamp-Double -Value $p.global_meter_after_trigger_multiplier -Min 0.75 -Max 1.35), 4)
    $p.global_contribution_window_multiplier = [Math]::Round((Clamp-Double -Value $p.global_contribution_window_multiplier -Min 0.75 -Max 1.35), 4)
    $p.inactivity_grace_multiplier = [Math]::Round((Clamp-Double -Value $p.inactivity_grace_multiplier -Min 0.0 -Max 4.0), 4)
    $p.inactivity_decay_multiplier = [Math]::Round((Clamp-Double -Value $p.inactivity_decay_multiplier -Min 0.0 -Max 4.0), 4)
    $p.contribution_window_multiplier = [Math]::Round((Clamp-Double -Value $p.contribution_window_multiplier -Min 0.05 -Max 4.0), 4)
    $p.role_lock_meter_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.role_lock_meter_threshold_multiplier -Min 0.0 -Max 4.0), 4)
    $p.meter_damage_reference_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_damage_reference_multiplier -Min 0.05 -Max 6.0), 4)
    $p.meter_damage_weight_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_damage_weight_multiplier -Min 0.0 -Max 4.0), 4)
    $p.meter_damage_min_clamp_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_damage_min_clamp_multiplier -Min 0.0 -Max 6.0), 4)
    $p.meter_damage_max_clamp_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_damage_max_clamp_multiplier -Min 0.0 -Max 6.0), 4)
    $p.token_cap_multiplier = [Math]::Round((Clamp-Double -Value $p.token_cap_multiplier -Min 0.0 -Max 4.0), 4)
    $p.token_regen_multiplier = [Math]::Round((Clamp-Double -Value $p.token_regen_multiplier -Min 0.0 -Max 4.0), 4)
    $p.copy_multiplier_scale = [Math]::Round((Clamp-Double -Value $p.copy_multiplier_scale -Min 0.0 -Max 4.0), 4)
    $p.diversity_multiplier_scale = [Math]::Round((Clamp-Double -Value $p.diversity_multiplier_scale -Min 0.0 -Max 4.0), 4)
    $p.event_scalar_multiplier = [Math]::Round((Clamp-Double -Value $p.event_scalar_multiplier -Min 0.0 -Max 4.0), 4)
    $p.second_stage_power_threshold = [Math]::Round((Clamp-Double -Value $p.second_stage_power_threshold -Min 0.05 -Max 3.0), 4)
    # Requested lower bounds for enemy toughness/count scaling.
    $p.normal_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_enemy_hp_multiplier -Min 1.0 -Max 5.0), 4)
    $p.normal_enemy_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_enemy_count_multiplier -Min 1.0 -Max 5.0), 4)
    $p.normal_spawn_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_spawn_interval_multiplier -Min 0.2 -Max 0.99), 4)
    $p.hard_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_enemy_hp_multiplier -Min 1.05 -Max 5.0), 4)
    $p.hard_enemy_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_enemy_count_multiplier -Min 1.05 -Max 5.0), 4)
    $p.hard_spawn_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_spawn_interval_multiplier -Min 0.2 -Max 0.98), 4)
    $p.gain_multipliers.overkill = [Math]::Round((Clamp-Double -Value $p.gain_multipliers.overkill -Min 0.0 -Max 4.0), 4)
    $p.gain_multipliers.chain_reaction = [Math]::Round((Clamp-Double -Value $p.gain_multipliers.chain_reaction -Min 0.0 -Max 4.0), 4)
    $p.gain_multipliers.split_shot = [Math]::Round((Clamp-Double -Value $p.gain_multipliers.split_shot -Min 0.0 -Max 4.0), 4)
    foreach ($name in $p.event_scalar_multipliers.PSObject.Properties.Name) {
        $p.event_scalar_multipliers.$name = [Math]::Round((Clamp-Double -Value $p.event_scalar_multipliers.$name -Min 0.0 -Max 4.0), 4)
    }
    foreach ($name in $p.token_cap_multipliers.PSObject.Properties.Name) {
        $p.token_cap_multipliers.$name = [Math]::Round((Clamp-Double -Value $p.token_cap_multipliers.$name -Min 0.0 -Max 4.0), 4)
    }
    foreach ($name in $p.token_regen_multipliers.PSObject.Properties.Name) {
        $p.token_regen_multipliers.$name = [Math]::Round((Clamp-Double -Value $p.token_regen_multipliers.$name -Min 0.0 -Max 4.0), 4)
    }

    # Guardrail: keep core spectacle systems enabled.
    $p.enable_status_detonation = $true
    $p.enable_residue = $true

    return $p
}

function Write-TuningProfile {
    param(
        [Parameter(Mandatory = $true)][object]$Profile,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    $dir = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        Ensure-Directory -PathValue $dir
    }

    $normalized = Normalize-TuningProfile -InputProfile $Profile
    $json = $normalized | ConvertTo-Json -Depth 12
    Set-Content -Path $OutputPath -Value $json -Encoding UTF8
}

function Read-MetricsPayload {
    param([Parameter(Mandatory = $true)][string]$MetricsPath)
    Assert-FileExists -PathValue $MetricsPath -Label "Metrics JSON"
    return (Get-Content -Raw $MetricsPath | ConvertFrom-Json)
}

function Get-NumberValue {
    param(
        [object]$Obj,
        [string]$Property,
        [double]$Default = 0.0
    )

    if ($null -eq $Obj) { return $Default }
    $prop = $Obj.PSObject.Properties[$Property]
    if ($null -eq $prop -or $null -eq $prop.Value) { return $Default }
    return [double]$prop.Value
}

function Get-DifficultyWinRate {
    param(
        [object[]]$Runs,
        [string]$Difficulty
    )

    if ($null -eq $Runs) { return 0.0 }
    $runsForDifficulty = @($Runs | Where-Object { [string]$_.difficulty -ieq $Difficulty })
    if ($runsForDifficulty.Count -eq 0) { return 0.0 }
    $wins = @($runsForDifficulty | Where-Object { $_.won -eq $true }).Count
    return [double]$wins / [double]$runsForDifficulty.Count
}

function Get-SweepVariantScore {
    param(
        [Parameter(Mandatory = $true)][string]$SweepReportPath,
        [Parameter(Mandatory = $true)][string]$VariantId
    )

    Assert-FileExists -PathValue $SweepReportPath -Label "Sweep report JSON"
    $payload = Get-Content -Raw $SweepReportPath | ConvertFrom-Json
    $variants = @($payload.variants)
    if ($variants.Count -lt 1) {
        throw "Sweep report has no variants: $SweepReportPath"
    }
    $match = $variants | Where-Object { [string]$_.id -eq $VariantId } | Select-Object -First 1
    if ($null -eq $match) {
        throw "Sweep report missing variant '$VariantId': $SweepReportPath"
    }
    return [double](Get-NumberValue -Obj $match -Property "score")
}

function Get-MetricsScore {
    param(
        [Parameter(Mandatory = $true)][object]$Summary,
        [object[]]$Runs,
        [double]$TargetShare,
        [double]$TargetShareTolerance,
        [double]$TargetEasyWinRate,
        [double]$TargetNormalWinRate,
        [double]$TargetHardWinRate,
        [double]$WinRateTolerance,
        [double]$TargetMaxTowerSurgeRatio,
        [double]$TargetMaxSurgesPerRun,
        [double]$TargetMaxSurgesPerRunTolerance,
        [double]$SurgesPerRunPenaltyWeight,
        [double]$MinNormalWinRate = -1.0,
        [double]$MinHardWinRate = -1.0,
        [double]$NormalRegressionPenaltyWeight = 0.0,
        [double]$HardRegressionPenaltyWeight = 0.0,
        [int]$MinTowerPlacementsForParity,
        [double]$ChainDepthCap,
        [int]$ExplosionCap,
        [int]$HazardCap,
        [int]$HitStopCap,
        [double]$DurationFloor
    )

    $winRate = Get-NumberValue -Obj $Summary -Property "win_rate"
    $avgWave = Get-NumberValue -Obj $Summary -Property "avg_wave_reached"
    $runDuration = Get-NumberValue -Obj $Summary -Property "avg_run_duration_seconds"
    $surgesPerRun = Get-NumberValue -Obj $Summary -Property "avg_surges_per_run"
    if ($surgesPerRun -le 0.000001) {
        # Backward compatibility with older metrics payloads.
        $surgesPerRun = Get-NumberValue -Obj $Summary -Property "avg_major_surges_per_run"
    }
    $killsPerSurge = Get-NumberValue -Obj $Summary -Property "avg_kills_per_surge"
    $chainDepth = Get-NumberValue -Obj $Summary -Property "avg_max_chain_depth"

    $dpsSplit = $Summary.dps_split
    $baseDps = Get-NumberValue -Obj $dpsSplit -Property "base_attacks"
    $surgeDps = Get-NumberValue -Obj $dpsSplit -Property "surge_core"
    $explosionDps = Get-NumberValue -Obj $dpsSplit -Property "explosion_follow_ups"
    $residueDps = Get-NumberValue -Obj $dpsSplit -Property "residue"
    $totalDps = $baseDps + $surgeDps + $explosionDps + $residueDps
    $explosionShare = if ($totalDps -gt 0.0001) { ($explosionDps + $residueDps) / $totalDps } else { 0.0 }
    $surgesByTowerRows = @()
    if ($null -ne $Summary -and $Summary.PSObject.Properties["surges_by_tower"]) {
        $rawRows = $Summary.surges_by_tower
        if ($null -ne $rawRows) {
            $surgesByTowerRows = @($rawRows)
        }
    }
    $towerSurgeRates = @()
    foreach ($row in $surgesByTowerRows) {
        $placements = [int](Get-NumberValue -Obj $row -Property "placements")
        if ($placements -lt $MinTowerPlacementsForParity) { continue }
        $rate = Get-NumberValue -Obj $row -Property "surges_per_placed_tower"
        if ($rate -le 0.000001) { continue }
        $towerSurgeRates += [double]$rate
    }
    $towerSurgeRatio = 1.0
    $towerSurgeRateMin = 0.0
    $towerSurgeRateMax = 0.0
    if ($towerSurgeRates.Count -ge 2) {
        $towerSurgeRateMin = ($towerSurgeRates | Measure-Object -Minimum).Minimum
        $towerSurgeRateMax = ($towerSurgeRates | Measure-Object -Maximum).Maximum
        if ($towerSurgeRateMin -gt 0.000001) {
            $towerSurgeRatio = [double]$towerSurgeRateMax / [double]$towerSurgeRateMin
        } else {
            $towerSurgeRatio = 9999.0
        }
    }

    $frameStress = $Summary.frame_stress_peaks
    $peakExplosions = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_explosions")
    $peakHazards = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_active_hazards")
    $peakHitStops = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_hitstops_requested")

    $easyWinRate = Get-DifficultyWinRate -Runs $Runs -Difficulty "Easy"
    $normalWinRate = Get-DifficultyWinRate -Runs $Runs -Difficulty "Normal"
    $hardWinRate = Get-DifficultyWinRate -Runs $Runs -Difficulty "Hard"
    $winTol = [Math]::Max(0.0001, $WinRateTolerance)

    $easyError = [Math]::Abs($easyWinRate - $TargetEasyWinRate)
    $normalError = [Math]::Abs($normalWinRate - $TargetNormalWinRate)
    $hardError = [Math]::Abs($hardWinRate - $TargetHardWinRate)

    $easyFit = [Math]::Max(0.0, 1.0 - ($easyError / $winTol))
    $normalFit = [Math]::Max(0.0, 1.0 - ($normalError / $winTol))
    $hardFit = [Math]::Max(0.0, 1.0 - ($hardError / $winTol))

    $score = 0.0
    # Primary objective: hit desired win-rate bands by difficulty.
    $score += $easyFit * 70.0
    $score += $normalFit * 60.0
    $score += $hardFit * 60.0
    if ($easyWinRate -lt $TargetEasyWinRate) {
        $score -= ($TargetEasyWinRate - $easyWinRate) * 140.0
    }
    if ($normalWinRate -lt $TargetNormalWinRate) {
        $score -= ($TargetNormalWinRate - $normalWinRate) * 120.0
    }
    if ($hardWinRate -lt $TargetHardWinRate) {
        $score -= ($TargetHardWinRate - $hardWinRate) * 100.0
    }
    if ($MinNormalWinRate -ge 0.0 -and $normalWinRate -lt $MinNormalWinRate) {
        $score -= ($MinNormalWinRate - $normalWinRate) * [Math]::Max(0.0, $NormalRegressionPenaltyWeight)
    }
    if ($MinHardWinRate -ge 0.0 -and $hardWinRate -lt $MinHardWinRate) {
        $score -= ($MinHardWinRate - $hardWinRate) * [Math]::Max(0.0, $HardRegressionPenaltyWeight)
    }

    # Secondary quality signals.
    $score += $winRate * 15.0
    $score += $avgWave * 1.0
    $score += $killsPerSurge * 6.0

    $surgeTarget = [Math]::Max(0.0, $TargetMaxSurgesPerRun)
    $surgeTolerance = [Math]::Max(0.0, $TargetMaxSurgesPerRunTolerance)
    $surgePenaltyWeight = [Math]::Max(0.0, $SurgesPerRunPenaltyWeight)
    $surgePenaltyStart = $surgeTarget + $surgeTolerance
    if ($surgesPerRun -gt $surgePenaltyStart) {
        $score -= ($surgesPerRun - $surgePenaltyStart) * $surgePenaltyWeight
    } elseif ($surgeTarget -gt 0.0001 -and $surgesPerRun -le $surgeTarget) {
        # Reward staying close to the surge budget without incentivizing surge spam.
        $surgeFit = [Math]::Max(0.0, 1.0 - (($surgeTarget - $surgesPerRun) / $surgeTarget))
        $score += $surgeFit * 6.0
    }

    $shareError = [Math]::Abs($explosionShare - $TargetShare)
    if ($TargetShareTolerance -gt 0.0001) {
        $shareReward = [Math]::Max(0.0, 1.0 - ($shareError / $TargetShareTolerance)) * 12.0
        $score += $shareReward
    }
    if ($towerSurgeRates.Count -ge 2) {
        if ($towerSurgeRatio -le $TargetMaxTowerSurgeRatio) {
            $ratioDenom = [Math]::Max(0.0001, $TargetMaxTowerSurgeRatio - 1.0)
            $ratioFit = [Math]::Max(0.0, 1.0 - (($towerSurgeRatio - 1.0) / $ratioDenom))
            $score += $ratioFit * 30.0
        } else {
            $score -= ($towerSurgeRatio - $TargetMaxTowerSurgeRatio) * 45.0
        }
    }

    if ($chainDepth -gt $ChainDepthCap) {
        $score -= ($chainDepth - $ChainDepthCap) * 10.0
    }
    if ($peakExplosions -gt $ExplosionCap) {
        $score -= ($peakExplosions - $ExplosionCap) * 6.0
    }
    if ($peakHazards -gt $HazardCap) {
        $score -= ($peakHazards - $HazardCap) * 4.0
    }
    if ($peakHitStops -gt $HitStopCap) {
        $score -= ($peakHitStops - $HitStopCap) * 8.0
    }
    if ($runDuration -lt $DurationFloor) {
        $score -= ($DurationFloor - $runDuration) / 10.0
    }

    return [PSCustomObject]@{
        Score = [Math]::Round($score, 4)
        WinRate = [Math]::Round($winRate, 6)
        EasyWinRate = [Math]::Round($easyWinRate, 6)
        NormalWinRate = [Math]::Round($normalWinRate, 6)
        HardWinRate = [Math]::Round($hardWinRate, 6)
        AvgWave = [Math]::Round($avgWave, 6)
        RunDurationSeconds = [Math]::Round($runDuration, 4)
        SurgesPerRun = [Math]::Round($surgesPerRun, 6)
        KillsPerSurge = [Math]::Round($killsPerSurge, 6)
        ExplosionShare = [Math]::Round($explosionShare, 6)
        TowerSurgeParityRatio = [Math]::Round($towerSurgeRatio, 6)
        TowerSurgeRateMin = [Math]::Round($towerSurgeRateMin, 6)
        TowerSurgeRateMax = [Math]::Round($towerSurgeRateMax, 6)
        TowerSurgeParityEligibleTowers = $towerSurgeRates.Count
        AvgMaxChainDepth = [Math]::Round($chainDepth, 6)
        PeakExplosions = $peakExplosions
        PeakHazards = $peakHazards
        PeakHitStops = $peakHitStops
    }
}

function New-MutatedTuningProfile {
    param(
        [Parameter(Mandatory = $true)][object]$BaseProfile,
        [Parameter(Mandatory = $true)][double]$Anneal,
        [Parameter(Mandatory = $true)][double]$Strength
    )

    $candidate = Clone-TuningProfile -Profile $BaseProfile
    $changed = 0

    function Apply-Mutation {
        param(
            [object]$Obj,
            [string]$Name,
            [double]$Step,
            [double]$Min,
            [double]$Max,
            [double]$Chance = 0.85
        )

        if ($script:Rng.NextDouble() -gt $Chance) { return 0 }

        $before = [double]$Obj.$Name
        $delta = (($script:Rng.NextDouble() * 2.0) - 1.0) * $Step
        $after = Clamp-Double -Value ($before + $delta) -Min $Min -Max $Max
        $Obj.$Name = [Math]::Round($after, 4)

        if ([Math]::Abs($after - $before) -gt 0.000001) { return 1 }
        return 0
    }

    function Apply-ToggleMutation {
        param(
            [object]$Obj,
            [string]$Name,
            [double]$Chance = 0.10
        )

        if ($script:Rng.NextDouble() -gt $Chance) { return 0 }
        $Obj.$Name = -not [bool]$Obj.$Name
        return 1
    }

    $scale = [Math]::Max(0.05, $Anneal * $Strength)
    $toggleChance = [Math]::Min(0.30, [Math]::Max(0.03, 0.16 * $scale))

    $changed += Apply-ToggleMutation -Obj $candidate -Name "enable_overkill_bloom" -Chance $toggleChance
    # Guardrail: keep these core systems enabled during auto-tune.
    $candidate.enable_status_detonation = $true
    $candidate.enable_residue = $true

    $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_damage_scale_multiplier" -Step (0.30 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_radius_multiplier" -Step (0.24 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_threshold_multiplier" -Step (0.20 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_max_targets_multiplier" -Step (0.18 * $scale) -Min 0.1 -Max 3.0
    $changed += Apply-Mutation -Obj $candidate -Name "detonation_max_targets_multiplier" -Step (0.24 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "detonation_stagger_multiplier" -Step (0.24 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "status_detonation_damage_multiplier" -Step (0.34 * $scale) -Min 0.0 -Max 6.0
    $changed += Apply-Mutation -Obj $candidate -Name "residue_duration_multiplier" -Step (0.28 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "residue_potency_multiplier" -Step (0.28 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "residue_damage_multiplier" -Step (0.34 * $scale) -Min 0.0 -Max 6.0
    $changed += Apply-Mutation -Obj $candidate -Name "residue_tick_interval_multiplier" -Step (0.26 * $scale) -Min 0.2 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "residue_max_active_multiplier" -Step (0.24 * $scale) -Min 0.1 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "explosion_followup_damage_multiplier" -Step (0.36 * $scale) -Min 0.0 -Max 6.0
    # Guardrail: keep key meter/global knobs in tighter bands.
    $changed += Apply-Mutation -Obj $candidate -Name "meter_gain_multiplier" -Step (0.14 * $scale) -Min 0.75 -Max 1.60
    $changed += Apply-Mutation -Obj $candidate -Name "surge_threshold_multiplier" -Step (0.12 * $scale) -Min 0.75 -Max 1.35
    $changed += Apply-Mutation -Obj $candidate -Name "surge_cooldown_multiplier" -Step (0.14 * $scale) -Min 0.75 -Max 1.80
    $changed += Apply-Mutation -Obj $candidate -Name "surge_meter_after_trigger_multiplier" -Step (0.12 * $scale) -Min 0.75 -Max 1.35
    $changed += Apply-Mutation -Obj $candidate -Name "global_meter_per_surge_multiplier" -Step (0.14 * $scale) -Min 0.75 -Max 1.80
    $changed += Apply-Mutation -Obj $candidate -Name "global_threshold_multiplier" -Step (0.12 * $scale) -Min 0.75 -Max 1.35
    $changed += Apply-Mutation -Obj $candidate -Name "global_meter_after_trigger_multiplier" -Step (0.12 * $scale) -Min 0.75 -Max 1.35
    $changed += Apply-Mutation -Obj $candidate -Name "global_contribution_window_multiplier" -Step (0.12 * $scale) -Min 0.75 -Max 1.35
    $changed += Apply-Mutation -Obj $candidate -Name "inactivity_grace_multiplier" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "inactivity_decay_multiplier" -Step (0.26 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "contribution_window_multiplier" -Step (0.22 * $scale) -Min 0.05 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "role_lock_meter_threshold_multiplier" -Step (0.20 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "meter_damage_reference_multiplier" -Step (0.30 * $scale) -Min 0.05 -Max 6.0
    $changed += Apply-Mutation -Obj $candidate -Name "meter_damage_weight_multiplier" -Step (0.28 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "meter_damage_min_clamp_multiplier" -Step (0.24 * $scale) -Min 0.0 -Max 6.0
    $changed += Apply-Mutation -Obj $candidate -Name "meter_damage_max_clamp_multiplier" -Step (0.24 * $scale) -Min 0.0 -Max 6.0
    $changed += Apply-Mutation -Obj $candidate -Name "token_cap_multiplier" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "token_regen_multiplier" -Step (0.26 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "copy_multiplier_scale" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "diversity_multiplier_scale" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "event_scalar_multiplier" -Step (0.28 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "second_stage_power_threshold" -Step (0.15 * $scale) -Min 0.05 -Max 3.0
    $changed += Apply-Mutation -Obj $candidate -Name "normal_enemy_hp_multiplier" -Step (0.10 * $scale) -Min 1.0 -Max 5.0 -Chance 0.80
    $changed += Apply-Mutation -Obj $candidate -Name "normal_enemy_count_multiplier" -Step (0.08 * $scale) -Min 1.0 -Max 5.0 -Chance 0.80
    $changed += Apply-Mutation -Obj $candidate -Name "normal_spawn_interval_multiplier" -Step (0.06 * $scale) -Min 0.2 -Max 0.99 -Chance 0.80
    $changed += Apply-Mutation -Obj $candidate -Name "hard_enemy_hp_multiplier" -Step (0.10 * $scale) -Min 1.05 -Max 5.0 -Chance 0.80
    $changed += Apply-Mutation -Obj $candidate -Name "hard_enemy_count_multiplier" -Step (0.08 * $scale) -Min 1.05 -Max 5.0 -Chance 0.80
    $changed += Apply-Mutation -Obj $candidate -Name "hard_spawn_interval_multiplier" -Step (0.06 * $scale) -Min 0.2 -Max 0.98 -Chance 0.80

    $changed += Apply-Mutation -Obj $candidate.gain_multipliers -Name "overkill" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.65
    $changed += Apply-Mutation -Obj $candidate.gain_multipliers -Name "chain_reaction" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.65
    $changed += Apply-Mutation -Obj $candidate.gain_multipliers -Name "split_shot" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.65
    $changed += Apply-Mutation -Obj $candidate.event_scalar_multipliers -Name "overkill" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.50
    $changed += Apply-Mutation -Obj $candidate.event_scalar_multipliers -Name "chain_reaction" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.50
    $changed += Apply-Mutation -Obj $candidate.event_scalar_multipliers -Name "split_shot" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.50
    $changed += Apply-Mutation -Obj $candidate.event_scalar_multipliers -Name "feedback_loop" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.50
    $changed += Apply-Mutation -Obj $candidate.event_scalar_multipliers -Name "hair_trigger" -Step (0.30 * $scale) -Min 0.0 -Max 4.0 -Chance 0.50
    $changed += Apply-Mutation -Obj $candidate.token_cap_multipliers -Name "overkill" -Step (0.22 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_cap_multipliers -Name "chain_reaction" -Step (0.22 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_cap_multipliers -Name "split_shot" -Step (0.22 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_cap_multipliers -Name "feedback_loop" -Step (0.22 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_cap_multipliers -Name "hair_trigger" -Step (0.22 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_regen_multipliers -Name "overkill" -Step (0.24 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_regen_multipliers -Name "chain_reaction" -Step (0.24 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_regen_multipliers -Name "split_shot" -Step (0.24 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_regen_multipliers -Name "feedback_loop" -Step (0.24 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45
    $changed += Apply-Mutation -Obj $candidate.token_regen_multipliers -Name "hair_trigger" -Step (0.24 * $scale) -Min 0.0 -Max 4.0 -Chance 0.45

    if ($changed -eq 0) {
        $forceDelta = (($script:Rng.NextDouble() * 2.0) - 1.0) * (0.20 * $scale)
        $candidate.meter_gain_multiplier = [Math]::Round((Clamp-Double -Value ([double]$candidate.meter_gain_multiplier + $forceDelta) -Min 0.75 -Max 1.60), 4)
    }

    return (Normalize-TuningProfile -InputProfile $candidate)
}

function Write-SweepComparisonConfig {
    param(
        [Parameter(Mandatory = $true)][string]$OutputPath,
        [Parameter(Mandatory = $true)][string]$ScenarioFilePath,
        [Parameter(Mandatory = $true)][int]$RunsPerVariant,
        [Parameter(Mandatory = $true)][object]$BestProfile,
        [Parameter(Mandatory = $true)][string]$BestId,
        [Parameter(Mandatory = $true)][string]$SweepName
    )

    $payload = [ordered]@{
        name = $SweepName
        runs_per_variant = [Math]::Max(1, $RunsPerVariant)
        scenario_file = $ScenarioFilePath
        variants = @(
            [ordered]@{
                id = "baseline"
                tuning = New-NeutralTuningProfile
            },
            [ordered]@{
                id = $BestId
                tuning = (Normalize-TuningProfile -InputProfile $BestProfile)
            }
        )
    }

    $dir = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        Ensure-Directory -PathValue $dir
    }
    $json = $payload | ConvertTo-Json -Depth 12
    Set-Content -Path $OutputPath -Value $json -Encoding UTF8
}

function New-BotMetricsArgs {
    param(
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [string]$TuningPath,
        [Parameter(Mandatory = $true)][string]$MetricsOut,
        [int]$RunIndexOffset = 0
    )

    $args = @()
    $args += $CommonPrefix
    $args += "--bot"
    $args += "--runs"
    $args += "$RunsPerEval"
    if ($RunIndexOffset -gt 0) {
        $args += "--run_index_offset"
        $args += "$RunIndexOffset"
    }
    if (-not [string]::IsNullOrWhiteSpace($StrategySet)) {
        $args += "--strategy_set"
        $args += $StrategySet
    }
    if (-not [string]::IsNullOrWhiteSpace($TuningPath)) {
        $args += "--tuning_file"
        $args += $TuningPath
    }
    $args += "--bot_metrics_out"
    $args += $MetricsOut
    return $args
}

function Invoke-BotMetricsRun {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [string]$TuningPath,
        [Parameter(Mandatory = $true)][string]$MetricsOut,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$RunIndexOffset = 0
    )

    $args = New-BotMetricsArgs -CommonPrefix $CommonPrefix -RunsPerEval $RunsPerEval -StrategySet $StrategySet -TuningPath $TuningPath -MetricsOut $MetricsOut -RunIndexOffset $RunIndexOffset

    Invoke-GodotCommand -GodotExe $GodotExe -Args $args -Label $Label
}

function Invoke-BotMetricsRunBatch {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [Parameter(Mandatory = $true)][object[]]$CandidateSpecs,
        [Parameter(Mandatory = $true)][int]$Parallelism
    )

    if ($Parallelism -le 1 -or $CandidateSpecs.Count -le 1) {
        foreach ($spec in $CandidateSpecs) {
            $specRuns = if ($null -ne $spec.PSObject.Properties["runs_per_eval"]) { [int]$spec.runs_per_eval } else { $RunsPerEval }
            if ($specRuns -lt 1) { $specRuns = $RunsPerEval }
            $specRunOffset = if ($null -ne $spec.PSObject.Properties["run_index_offset"]) { [int]$spec.run_index_offset } else { 0 }
            if ($specRunOffset -lt 0) { $specRunOffset = 0 }
            Invoke-BotMetricsRun `
                -GodotExe $GodotExe `
                -CommonPrefix $CommonPrefix `
                -RunsPerEval $specRuns `
                -StrategySet $StrategySet `
                -TuningPath ([string]$spec.tuning_path) `
                -MetricsOut ([string]$spec.metrics_path) `
                -Label ([string]$spec.label) `
                -RunIndexOffset $specRunOffset
        }
        return
    }

    $batchSize = [Math]::Max(1, $Parallelism)
    for ($offset = 0; $offset -lt $CandidateSpecs.Count; $offset += $batchSize) {
        $batch = @($CandidateSpecs | Select-Object -Skip $offset -First $batchSize)
        $launches = @()
        $maxRunsInBatch = 1

        foreach ($spec in $batch) {
            $label = [string]$spec.label
            $specRuns = if ($null -ne $spec.PSObject.Properties["runs_per_eval"]) { [int]$spec.runs_per_eval } else { $RunsPerEval }
            if ($specRuns -lt 1) { $specRuns = $RunsPerEval }
            if ($specRuns -gt $maxRunsInBatch) { $maxRunsInBatch = $specRuns }
            $specRunOffset = if ($null -ne $spec.PSObject.Properties["run_index_offset"]) { [int]$spec.run_index_offset } else { 0 }
            if ($specRunOffset -lt 0) { $specRunOffset = 0 }
            Write-Host ""
            Write-Host "=== $label (parallel launch) ==="

            $args = New-BotMetricsArgs `
                -CommonPrefix $CommonPrefix `
                -RunsPerEval $specRuns `
                -StrategySet $StrategySet `
                -TuningPath ([string]$spec.tuning_path) `
                -MetricsOut ([string]$spec.metrics_path) `
                -RunIndexOffset $specRunOffset

            $metricsPath = [string]$spec.metrics_path
            $metricsDir = Split-Path -Parent $metricsPath
            if (-not [string]::IsNullOrWhiteSpace($metricsDir)) {
                Ensure-Directory -PathValue $metricsDir
            }
            $stderrPath = Join-Path $metricsDir ("{0}.stderr.log" -f ([string]$spec.candidate_id))
            $stdoutPath = Join-Path $metricsDir ("{0}.stdout.log" -f ([string]$spec.candidate_id))
            if (Test-Path $stderrPath) { Remove-Item -Path $stderrPath -Force -ErrorAction SilentlyContinue }
            if (Test-Path $stdoutPath) { Remove-Item -Path $stdoutPath -Force -ErrorAction SilentlyContinue }

            $proc = Start-Process `
                -FilePath $GodotExe `
                -ArgumentList $args `
                -NoNewWindow `
                -PassThru `
                -RedirectStandardOutput $stdoutPath `
                -RedirectStandardError $stderrPath

            $launches += [PSCustomObject]@{
                spec = $spec
                process = $proc
                stdout_path = $stdoutPath
                stderr_path = $stderrPath
            }
        }

        # Guard against silent hangs in shard execution.
        $timeoutSeconds = [Math]::Max(900, [int]($maxRunsInBatch * 20))
        $deadline = (Get-Date).AddSeconds($timeoutSeconds)
        while ($true) {
            $running = @($launches | Where-Object { -not $_.process.HasExited })
            if ($running.Count -eq 0) { break }
            if ((Get-Date) -ge $deadline) {
                foreach ($entry in $running) {
                    try { $entry.process.Kill() } catch {}
                }
                $timedOutIds = @($running | ForEach-Object { [string]$_.spec.candidate_id }) -join ", "
                throw "Parallel bot run timed out after $timeoutSeconds seconds. Candidate(s): $timedOutIds"
            }
            Start-Sleep -Seconds 2
        }

        foreach ($entry in $launches) {
            $candidateId = [string]$entry.spec.candidate_id
            $exitCode = [int]$entry.process.ExitCode
            if ($exitCode -ne 0) {
                throw "Candidate $candidateId failed in parallel bot run. exit_code=$exitCode stderr_log=$($entry.stderr_path)"
            }
        }
    }
}

function Get-RunShardPlan {
    param(
        [Parameter(Mandatory = $true)][int]$TotalRuns,
        [Parameter(Mandatory = $true)][int]$ShardCount
    )

    $resolvedRuns = [Math]::Max(1, $TotalRuns)
    $resolvedShards = [Math]::Max(1, [Math]::Min($ShardCount, $resolvedRuns))
    $baseRuns = [Math]::Floor($resolvedRuns / $resolvedShards)
    $remainder = $resolvedRuns % $resolvedShards
    $offset = 0
    $plan = @()

    for ($i = 0; $i -lt $resolvedShards; $i++) {
        $runsForShard = [int]$baseRuns
        if ($i -lt $remainder) { $runsForShard++ }
        $plan += [PSCustomObject]@{
            shard_index = $i + 1
            runs = $runsForShard
            offset = $offset
        }
        $offset += $runsForShard
    }

    return $plan
}

function Get-SafeRunDps {
    param(
        [double]$Damage,
        [double]$DurationSeconds
    )

    if ($DurationSeconds -le 0.0001) { return 0.0 }
    return $Damage / $DurationSeconds
}

function Build-DiscreteDistribution {
    param([object[]]$Values)

    if ($null -eq $Values) { return @() }
    return @(
        $Values |
            Group-Object |
            Sort-Object { [int]$_.Name } |
            ForEach-Object {
                [PSCustomObject]@{
                    value = [int]$_.Name
                    count = [int]$_.Count
                }
            }
    )
}

function Build-SparseBinnedDistribution {
    param(
        [object[]]$Values,
        [double]$BinSize
    )

    if ($null -eq $Values -or $BinSize -le 0.0) { return @() }

    $buckets = @{}
    foreach ($raw in $Values) {
        $value = [double]$raw
        if ($value -lt 0.0) { $value = 0.0 }
        $binIndex = [int][Math]::Floor($value / $BinSize)
        if ($buckets.ContainsKey($binIndex)) {
            $buckets[$binIndex] = [int]$buckets[$binIndex] + 1
        } else {
            $buckets[$binIndex] = 1
        }
    }

    return @(
        $buckets.Keys |
            Sort-Object |
            ForEach-Object {
                $binIndex = [int]$_
                [PSCustomObject]@{
                    bin_start = [Math]::Round($binIndex * $BinSize, 6)
                    bin_end = [Math]::Round(($binIndex + 1) * $BinSize, 6)
                    bin_start_percent = [Math]::Round($binIndex * $BinSize * 100.0, 6)
                    bin_end_percent = [Math]::Round(($binIndex + 1) * $BinSize * 100.0, 6)
                    count = [int]$buckets[$binIndex]
                }
            }
    )
}

function Get-InterpolatedPercentile {
    param(
        [double[]]$SortedValues,
        [double]$Quantile
    )

    if ($null -eq $SortedValues -or $SortedValues.Count -eq 0) { return 0.0 }
    if ($SortedValues.Count -eq 1) { return [double]$SortedValues[0] }

    $q = [Math]::Max(0.0, [Math]::Min(1.0, $Quantile))
    $index = $q * ([double]$SortedValues.Count - 1.0)
    $lo = [int][Math]::Floor($index)
    $hi = [int][Math]::Ceiling($index)
    if ($lo -eq $hi) { return [double]$SortedValues[$lo] }

    $t = $index - $lo
    return ([double]$SortedValues[$lo] * (1.0 - $t)) + ([double]$SortedValues[$hi] * $t)
}

function Get-PercentileStats {
    param([object[]]$Values)

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return [PSCustomObject]@{
            sample_count = 0
            avg = 0.0
            p25 = 0.0
            p50 = 0.0
            p75 = 0.0
            p90 = 0.0
            p99 = 0.0
        }
    }

    $sorted = @($Values | ForEach-Object { [double]$_ } | Sort-Object)
    $sum = 0.0
    foreach ($v in $sorted) { $sum += $v }
    $avg = $sum / [double]$sorted.Count

    return [PSCustomObject]@{
        sample_count = [int]$sorted.Count
        avg = [Math]::Round($avg, 6)
        p25 = [Math]::Round((Get-InterpolatedPercentile -SortedValues $sorted -Quantile 0.25), 6)
        p50 = [Math]::Round((Get-InterpolatedPercentile -SortedValues $sorted -Quantile 0.50), 6)
        p75 = [Math]::Round((Get-InterpolatedPercentile -SortedValues $sorted -Quantile 0.75), 6)
        p90 = [Math]::Round((Get-InterpolatedPercentile -SortedValues $sorted -Quantile 0.90), 6)
        p99 = [Math]::Round((Get-InterpolatedPercentile -SortedValues $sorted -Quantile 0.99), 6)
    }
}

function Merge-BotMetricsShards {
    param(
        [Parameter(Mandatory = $true)][string[]]$ShardMetricsPaths,
        [Parameter(Mandatory = $true)][string]$OutputPath
    )

    if ($null -eq $ShardMetricsPaths -or $ShardMetricsPaths.Count -lt 1) {
        throw "Merge-BotMetricsShards requires at least one shard metrics file."
    }

    if ($ShardMetricsPaths.Count -eq 1) {
        Copy-Item -Path $ShardMetricsPaths[0] -Destination $OutputPath -Force
        return
    }

    $payloads = @()
    foreach ($path in $ShardMetricsPaths) {
        $payloads += Read-MetricsPayload -MetricsPath $path
    }

    $allRuns = @()
    foreach ($payload in $payloads) {
        if ($null -ne $payload -and $null -ne $payload.runs) {
            $allRuns += @($payload.runs)
        }
    }

    $runCount = [int]$allRuns.Count
    if ($runCount -le 0) {
        throw "Merged shard metrics contained zero runs."
    }

    $tuningProfile = [string]$payloads[0].tuning_profile
    if ([string]::IsNullOrWhiteSpace($tuningProfile)) {
        $tuningProfile = "baseline"
    }

    function Get-WeightedSummaryAverage {
        param([string]$PropertyName)
        $numerator = 0.0
        $denominator = 0.0
        foreach ($payload in $payloads) {
            $payloadRunCount = [int](Get-NumberValue -Obj $payload -Property "run_count")
            if ($payloadRunCount -le 0 -and $null -ne $payload.runs) {
                $payloadRunCount = @($payload.runs).Count
            }
            if ($payloadRunCount -le 0) { continue }

            $value = Get-NumberValue -Obj $payload.summary -Property $PropertyName
            $numerator += $value * $payloadRunCount
            $denominator += $payloadRunCount
        }
        if ($denominator -le 0.0) { return 0.0 }
        return $numerator / $denominator
    }

    $wonCount = 0
    $waveSum = 0.0
    $durationSum = 0.0
    $surgesSum = 0.0
    $statusDetSum = 0.0
    $residueUptimeSum = 0.0
    $chainDepthSum = 0.0
    $baseDpsSum = 0.0
    $surgeDpsSum = 0.0
    $explosionDpsSum = 0.0
    $residueDpsSum = 0.0
    $totalExplosionDamage = 0.0
    $peakExplosions = 0
    $peakHazards = 0
    $peakHitStops = 0
    $surgeIntervals = @()
    $surgesPerRunValues = @()
    $chainDepthValues = @()
    $waveReachedValues = @()
    $explosionShareValues = @()
    $topTowerDamageShareValues = @()
    $killDepthValues = @()
    $chainReactionSizeValues = @()

    foreach ($run in $allRuns) {
        if ($run.won -eq $true) { $wonCount++ }

        $waveReached = [int](Get-NumberValue -Obj $run -Property "wave_reached")
        $runDuration = Get-NumberValue -Obj $run -Property "run_duration_seconds"
        $surges = [int](Get-NumberValue -Obj $run -Property "surges")
        $surgeInterval = Get-NumberValue -Obj $run -Property "surge_interval_seconds"
        $statusDet = Get-NumberValue -Obj $run -Property "status_detonations"
        $residueUptime = Get-NumberValue -Obj $run -Property "residue_uptime_seconds"
        $chainDepth = [int](Get-NumberValue -Obj $run -Property "max_chain_depth")

        $damageSplit = $run.damage_split
        $baseDamage = Get-NumberValue -Obj $damageSplit -Property "base_attacks"
        $surgeDamage = Get-NumberValue -Obj $damageSplit -Property "surge_core"
        $explosionDamage = Get-NumberValue -Obj $damageSplit -Property "explosion_follow_ups"
        $residueDamage = Get-NumberValue -Obj $damageSplit -Property "residue"
        $runExplosionDamage = $explosionDamage + $residueDamage
        $runTotalDamage = $baseDamage + $surgeDamage + $explosionDamage + $residueDamage

        $frameStress = $run.frame_stress
        $runPeakExplosions = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_explosions")
        $runPeakHazards = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_active_hazards")
        $runPeakHitStops = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_hitstops_requested")

        $waveSum += $waveReached
        $durationSum += $runDuration
        $surgesSum += $surges
        $statusDetSum += $statusDet
        $residueUptimeSum += $residueUptime
        $chainDepthSum += $chainDepth
        $baseDpsSum += (Get-SafeRunDps -Damage $baseDamage -DurationSeconds $runDuration)
        $surgeDpsSum += (Get-SafeRunDps -Damage $surgeDamage -DurationSeconds $runDuration)
        $explosionDpsSum += (Get-SafeRunDps -Damage $explosionDamage -DurationSeconds $runDuration)
        $residueDpsSum += (Get-SafeRunDps -Damage $residueDamage -DurationSeconds $runDuration)
        $totalExplosionDamage += $runExplosionDamage

        if ($surges -gt 0 -and $surgeInterval -gt 0.0) {
            $surgeIntervals += $surgeInterval
        }

        $surgesPerRunValues += $surges
        $chainDepthValues += $chainDepth
        $waveReachedValues += $waveReached
        if ($runTotalDamage -gt 0.0001) {
            $explosionShareValues += ($runExplosionDamage / $runTotalDamage)
        } else {
            $explosionShareValues += 0.0
        }
        $topTowerDamageShareValues += (Get-NumberValue -Obj $run -Property "top_tower_damage_share")

        if ($null -ne $run.kill_depth_samples) {
            foreach ($sample in @($run.kill_depth_samples)) {
                $killDepthValues += [double]$sample
            }
        }

        if ($null -ne $run.chain_reaction_sizes) {
            foreach ($chainSize in @($run.chain_reaction_sizes)) {
                $chainReactionSizeValues += [int]$chainSize
            }
        }

        if ($runPeakExplosions -gt $peakExplosions) { $peakExplosions = $runPeakExplosions }
        if ($runPeakHazards -gt $peakHazards) { $peakHazards = $runPeakHazards }
        if ($runPeakHitStops -gt $peakHitStops) { $peakHitStops = $runPeakHitStops }
    }

    $towerAggregate = @{}
    foreach ($payload in $payloads) {
        $rows = @()
        if ($null -ne $payload.summary -and $null -ne $payload.summary.surges_by_tower) {
            $rows = @($payload.summary.surges_by_tower)
        }

        foreach ($row in $rows) {
            $towerId = [string]$row.tower_id
            if ([string]::IsNullOrWhiteSpace($towerId)) { continue }
            $placements = [int](Get-NumberValue -Obj $row -Property "placements")
            $surges = [int](Get-NumberValue -Obj $row -Property "surges")
            if (-not $towerAggregate.ContainsKey($towerId)) {
                $towerAggregate[$towerId] = [PSCustomObject]@{
                    placements = 0
                    surges = 0
                }
            }
            $towerAggregate[$towerId].placements = [int]$towerAggregate[$towerId].placements + $placements
            $towerAggregate[$towerId].surges = [int]$towerAggregate[$towerId].surges + $surges
        }
    }

    $surgesByTowerRows = @()
    foreach ($towerId in ($towerAggregate.Keys | Sort-Object)) {
        $placements = [int]$towerAggregate[$towerId].placements
        $surges = [int]$towerAggregate[$towerId].surges
        $surgesPerPlacedTower = if ($placements -gt 0) { [double]$surges / [double]$placements } else { 0.0 }
        $surgesByTowerRows += [PSCustomObject]@{
            tower_id = $towerId
            placements = $placements
            surges = $surges
            surges_per_placed_tower = [Math]::Round($surgesPerPlacedTower, 6)
        }
    }

    $estimatedExplosionTriggers = 0.0
    foreach ($payload in $payloads) {
        $payloadRunCount = [int](Get-NumberValue -Obj $payload -Property "run_count")
        if ($payloadRunCount -le 0 -and $null -ne $payload.runs) {
            $payloadRunCount = @($payload.runs).Count
        }
        if ($payloadRunCount -le 0) { continue }

        $avgExplosionDamagePerRunShard = Get-NumberValue -Obj $payload.summary -Property "avg_explosion_damage_per_run"
        $avgExplosionDamagePerTriggerShard = Get-NumberValue -Obj $payload.summary -Property "avg_explosion_damage_per_trigger"
        if ($avgExplosionDamagePerRunShard -le 0.0 -or $avgExplosionDamagePerTriggerShard -le 0.000001) { continue }

        $estimatedExplosionTriggers += ($avgExplosionDamagePerRunShard * $payloadRunCount) / $avgExplosionDamagePerTriggerShard
    }

    $avgSurgeIntervalSeconds = if ($surgeIntervals.Count -gt 0) {
        ($surgeIntervals | Measure-Object -Average).Average
    } else {
        0.0
    }

    $avgExplosionDamagePerTrigger = if ($estimatedExplosionTriggers -gt 0.000001) {
        $totalExplosionDamage / $estimatedExplosionTriggers
    } else {
        0.0
    }

    $damageConcentrationStats = Get-PercentileStats -Values $topTowerDamageShareValues
    $killDepthStats = Get-PercentileStats -Values $killDepthValues
    $chainReactionSizeStats = Get-PercentileStats -Values $chainReactionSizeValues
    $damageConcentrationWarningThreshold = 0.60
    $damageConcentrationProblemThreshold = 0.75
    $healthyDamageConcentrationRuns = @($topTowerDamageShareValues | Where-Object { [double]$_ -lt $damageConcentrationWarningThreshold }).Count
    $warningDamageConcentrationRuns = @($topTowerDamageShareValues | Where-Object {
        [double]$_ -ge $damageConcentrationWarningThreshold -and [double]$_ -lt $damageConcentrationProblemThreshold
    }).Count
    $problemDamageConcentrationRuns = @($topTowerDamageShareValues | Where-Object { [double]$_ -ge $damageConcentrationProblemThreshold }).Count

    $explosionShareBinSize = 0.001
    $payload = [ordered]@{
        generated_utc = (Get-Date).ToUniversalTime().ToString("o")
        tuning_profile = $tuningProfile
        run_count = $runCount
        summary = [ordered]@{
            win_rate = [Math]::Round(([double]$wonCount / [double]$runCount), 6)
            avg_wave_reached = [Math]::Round(($waveSum / [double]$runCount), 6)
            avg_run_duration_seconds = [Math]::Round(($durationSum / [double]$runCount), 6)
            avg_surges_per_run = [Math]::Round(($surgesSum / [double]$runCount), 6)
            avg_surge_interval_seconds = [Math]::Round($avgSurgeIntervalSeconds, 6)
            avg_kills_per_surge = [Math]::Round((Get-WeightedSummaryAverage -PropertyName "avg_kills_per_surge"), 6)
            avg_explosion_damage_per_run = [Math]::Round(($totalExplosionDamage / [double]$runCount), 6)
            avg_explosion_damage_per_trigger = [Math]::Round($avgExplosionDamagePerTrigger, 6)
            avg_status_detonation_count = [Math]::Round(($statusDetSum / [double]$runCount), 6)
            avg_residue_uptime_seconds = [Math]::Round(($residueUptimeSum / [double]$runCount), 6)
            avg_max_chain_depth = [Math]::Round(($chainDepthSum / [double]$runCount), 6)
            damage_concentration = [ordered]@{
                thresholds = [ordered]@{
                    warning_min = $damageConcentrationWarningThreshold
                    problem_min = $damageConcentrationProblemThreshold
                }
                avg_top_tower_share = [double]$damageConcentrationStats.avg
                p25_top_tower_share = [double]$damageConcentrationStats.p25
                p50_top_tower_share = [double]$damageConcentrationStats.p50
                p75_top_tower_share = [double]$damageConcentrationStats.p75
                p90_top_tower_share = [double]$damageConcentrationStats.p90
                p99_top_tower_share = [double]$damageConcentrationStats.p99
                healthy_runs = [int]$healthyDamageConcentrationRuns
                warning_runs = [int]$warningDamageConcentrationRuns
                problem_runs = [int]$problemDamageConcentrationRuns
            }
            kill_depth_distribution = [ordered]@{
                sample_count = [int]$killDepthStats.sample_count
                avg = [double]$killDepthStats.avg
                p25 = [double]$killDepthStats.p25
                p50 = [double]$killDepthStats.p50
                p75 = [double]$killDepthStats.p75
                p90 = [double]$killDepthStats.p90
                p99 = [double]$killDepthStats.p99
            }
            chain_reaction_size_distribution = [ordered]@{
                sample_count = [int]$chainReactionSizeStats.sample_count
                median = [double]$chainReactionSizeStats.p50
                p90 = [double]$chainReactionSizeStats.p90
                p99 = [double]$chainReactionSizeStats.p99
            }
            dps_split = [ordered]@{
                base_attacks = [Math]::Round(($baseDpsSum / [double]$runCount), 6)
                surge_core = [Math]::Round(($surgeDpsSum / [double]$runCount), 6)
                explosion_follow_ups = [Math]::Round(($explosionDpsSum / [double]$runCount), 6)
                residue = [Math]::Round(($residueDpsSum / [double]$runCount), 6)
            }
            frame_stress_peaks = [ordered]@{
                simultaneous_explosions = $peakExplosions
                simultaneous_active_hazards = $peakHazards
                simultaneous_hitstops_requested = $peakHitStops
            }
            surges_by_tower = $surgesByTowerRows
            distributions = [ordered]@{
                surges_per_run = (Build-DiscreteDistribution -Values $surgesPerRunValues)
                chain_depth_per_run = (Build-DiscreteDistribution -Values $chainDepthValues)
                wave_reached_per_run = (Build-DiscreteDistribution -Values $waveReachedValues)
                explosion_damage_share_per_run = [ordered]@{
                    bin_size_fraction = $explosionShareBinSize
                    bin_size_percent = $explosionShareBinSize * 100.0
                    bins = (Build-SparseBinnedDistribution -Values $explosionShareValues -BinSize $explosionShareBinSize)
                }
                top_tower_damage_share_per_run = [ordered]@{
                    bin_size_fraction = $explosionShareBinSize
                    bin_size_percent = $explosionShareBinSize * 100.0
                    bins = (Build-SparseBinnedDistribution -Values $topTowerDamageShareValues -BinSize $explosionShareBinSize)
                }
                kill_depth = [ordered]@{
                    bin_size_fraction = 0.01
                    bins = (Build-SparseBinnedDistribution -Values $killDepthValues -BinSize 0.01)
                }
                chain_reaction_size = (Build-DiscreteDistribution -Values $chainReactionSizeValues)
            }
        }
        runs = $allRuns
    }

    $dir = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($dir)) {
        Ensure-Directory -PathValue $dir
    }

    $json = $payload | ConvertTo-Json -Depth 14
    Set-Content -Path $OutputPath -Value $json -Encoding UTF8
}

function Invoke-BotMetricsRunSharded {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [string]$TuningPath,
        [Parameter(Mandatory = $true)][string]$MetricsOut,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][int]$Parallelism
    )

    $effectiveParallelism = [Math]::Max(1, [Math]::Min($Parallelism, $RunsPerEval))
    if ($effectiveParallelism -le 1) {
        Invoke-BotMetricsRun `
            -GodotExe $GodotExe `
            -CommonPrefix $CommonPrefix `
            -RunsPerEval $RunsPerEval `
            -StrategySet $StrategySet `
            -TuningPath $TuningPath `
            -MetricsOut $MetricsOut `
            -Label $Label
        return
    }

    $outDir = Split-Path -Parent $MetricsOut
    if (-not [string]::IsNullOrWhiteSpace($outDir)) {
        Ensure-Directory -PathValue $outDir
    }
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($MetricsOut)
    $ext = [System.IO.Path]::GetExtension($MetricsOut)
    if ([string]::IsNullOrWhiteSpace($ext)) { $ext = ".json" }

    $shardPlan = Get-RunShardPlan -TotalRuns $RunsPerEval -ShardCount $effectiveParallelism
    $shardSpecs = @()
    foreach ($shard in $shardPlan) {
        $shardId = "shard{0:d2}" -f ([int]$shard.shard_index)
        $shardMetricsOut = Join-Path $outDir "$baseName.$shardId$ext"
        $shardSpecs += [PSCustomObject]@{
            candidate_id = $shardId
            tuning_path = $TuningPath
            metrics_path = $shardMetricsOut
            label = "$Label [$shardId runs=$($shard.runs) offset=$($shard.offset)]"
            runs_per_eval = [int]$shard.runs
            run_index_offset = [int]$shard.offset
        }
    }

    Invoke-BotMetricsRunBatch `
        -GodotExe $GodotExe `
        -CommonPrefix $CommonPrefix `
        -RunsPerEval $RunsPerEval `
        -StrategySet $StrategySet `
        -CandidateSpecs $shardSpecs `
        -Parallelism $effectiveParallelism

    $shardPaths = @($shardSpecs | ForEach-Object { [string]$_.metrics_path })
    Merge-BotMetricsShards -ShardMetricsPaths $shardPaths -OutputPath $MetricsOut
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    if (![string]::IsNullOrWhiteSpace($env:GODOT_EXE)) {
        $GodotPath = $env:GODOT_EXE
    } else {
        $GodotPath = "E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe"
    }
}

$godotExe = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $GodotPath
$tuningFileResolved = ""
$scenarioFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $ScenarioFile
$outputRootResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $OutputRoot

Assert-FileExists -PathValue $godotExe -Label "Godot executable"
Assert-FileExists -PathValue $scenarioFileResolved -Label "Scenario suite file"
if (-not [string]::IsNullOrWhiteSpace($TuningFile)) {
    $tuningFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $TuningFile
    Assert-FileExists -PathValue $tuningFileResolved -Label "Starting tuning profile"
}

if ($Runs -lt 1) { throw "Runs must be >= 1." }
if ($Iterations -lt 1) { throw "Iterations must be >= 1." }
if ($CandidatesPerIteration -lt 1) { throw "CandidatesPerIteration must be >= 1." }
if ($CandidateParallelism -lt 1) { throw "CandidateParallelism must be >= 1." }
if ($SweepRunsPerVariant -lt 1) { throw "SweepRunsPerVariant must be >= 1." }
if ($MutationStrength -le 0) { throw "MutationStrength must be > 0." }
if ($TargetExplosionShareTolerance -le 0) { throw "TargetExplosionShareTolerance must be > 0." }
if ($TargetWinRateTolerance -le 0) { throw "TargetWinRateTolerance must be > 0." }
if ($RelativeWinTargetEasyUplift -lt 0) { throw "RelativeWinTargetEasyUplift must be >= 0." }
if ($RelativeWinTargetNormalUplift -lt 0) { throw "RelativeWinTargetNormalUplift must be >= 0." }
if ($RelativeWinTargetHardUplift -lt 0) { throw "RelativeWinTargetHardUplift must be >= 0." }
if ($DifficultyRegressionTolerance -lt 0) { throw "DifficultyRegressionTolerance must be >= 0." }
if ($NormalRegressionPenaltyWeight -lt 0) { throw "NormalRegressionPenaltyWeight must be >= 0." }
if ($HardRegressionPenaltyWeight -lt 0) { throw "HardRegressionPenaltyWeight must be >= 0." }
if ($MinSweepScoreRatioVsBaseline -lt 0) { throw "MinSweepScoreRatioVsBaseline must be >= 0." }
if ($TargetMaxTowerSurgeRatio -lt 1.0) { throw "TargetMaxTowerSurgeRatio must be >= 1.0." }
if ($TopCandidateReevalCount -lt 0) { throw "TopCandidateReevalCount must be >= 0." }
if ($MinTowerPlacementsForParity -lt 1) { throw "MinTowerPlacementsForParity must be >= 1." }
if ($TargetWinRateEasy -lt 0 -or $TargetWinRateEasy -gt 1) { throw "TargetWinRateEasy must be between 0 and 1." }
if ($TargetWinRateNormal -lt 0 -or $TargetWinRateNormal -gt 1) { throw "TargetWinRateNormal must be between 0 and 1." }
if ($TargetWinRateHard -lt 0 -or $TargetWinRateHard -gt 1) { throw "TargetWinRateHard must be between 0 and 1." }
if ($CandidateParallelism -gt 1 -and $null -eq (Get-Command Start-Job -ErrorAction SilentlyContinue)) {
    throw "CandidateParallelism > 1 requires Start-Job support in this PowerShell host."
}
$rawStrategySet = if ($null -eq $StrategySet) { "" } else { [string]$StrategySet }
$strategySetNormalized = $rawStrategySet.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($strategySetNormalized)) {
    $strategySetNormalized = "all"
}
if ($strategySetNormalized -notin @("all", "optimization", "edge")) {
    throw "StrategySet must be one of: all, optimization, edge."
}

# TODO(perf-step-4): Introduce a dedicated -EvalShardParallelism parameter and decouple
# run-shard concurrency from candidate-level concurrency.
$effectiveCandidateParallelism = [Math]::Max(1, [Math]::Min($CandidateParallelism, $CandidatesPerIteration))
$effectiveRunParallelism = [Math]::Max(1, [Math]::Min($CandidateParallelism, $Runs))

$script:Rng = [System.Random]::new($Seed)

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$runDir = Join-Path $outputRootResolved $timestamp
Ensure-Directory -PathValue $runDir
$autoTuneDir = Join-Path $runDir "autotune"
Ensure-Directory -PathValue $autoTuneDir

Write-Host "Project root: $projectRoot"
Write-Host "Godot exe:    $godotExe"
Write-Host "Runs:         $Runs"
Write-Host "Iterations:   $Iterations"
Write-Host "Candidates:   $CandidatesPerIteration per iteration"
Write-Host "Parallelism:  candidates=$effectiveCandidateParallelism, eval_shards=$effectiveRunParallelism"
Write-Host "Top re-eval:  $TopCandidateReevalCount candidate(s) per iteration"
Write-Host "Strategy set: $strategySetNormalized"
Write-Host "Seed:         $Seed"
Write-Host "Output dir:   $runDir"

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "=== Build ==="
    dotnet build "SlotTheory.sln"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
} else {
    Write-Host ""
    Write-Host "=== Build skipped (-SkipBuild) ==="
}

# TODO(perf-step-6): Add an explicit mode preset switch (for example, search vs confirm)
# that adjusts Runs/SweepRunsPerVariant/SkipTrace defaults for fast iteration.

$commonPrefix = @(
    "--headless",
    "--path", $projectRoot,
    "--scene", "res://Scenes/Main.tscn",
    "--"
)
if ([string]::IsNullOrWhiteSpace($tuningFileResolved)) {
    $generatedSeedPath = Join-Path $runDir "seed_current.json"
    Invoke-GodotCommand -GodotExe $godotExe -Label "[seed] Generate current tuning seed" -Args @(
        "--headless",
        "--path", $projectRoot,
        "--scene", "res://Scenes/Main.tscn",
        "--",
        "--dump_seed_tuning_json", $generatedSeedPath
    )
    Assert-FileExists -PathValue $generatedSeedPath -Label "Generated seed tuning profile"
    $tuningFileResolved = $generatedSeedPath
}

Write-Host "Seed profile: $tuningFileResolved"

$baselineMetricsOut = Join-Path $runDir "bot_metrics_baseline.json"
$scenarioReportOut = Join-Path $runDir "combat_lab_report.json"
$sweepReportOut = Join-Path $runDir "combat_lab_sweep.json"
$tunedMetricsOut = Join-Path $runDir "bot_metrics_tuned.json"
$traceOut = Join-Path $runDir "bot_trace.json"
$deltaOut = Join-Path $runDir "bot_metrics_delta.txt"
$bestTuningOut = Join-Path $runDir "best_tuning.json"
$autoTuneReportOut = Join-Path $runDir "autotune_report.json"

Invoke-BotMetricsRunSharded -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -MetricsOut $baselineMetricsOut -Label "[1/8] Baseline bot metrics" -Parallelism $effectiveRunParallelism
# TODO(perf-step-5): Add metrics cache lookup before each expensive bot eval call and reuse cached payloads when keys match.
# Key should include tuning hash + runs + strategy_set + run_index_offset + scoring-relevant version info.
$baselinePayload = Read-MetricsPayload -MetricsPath $baselineMetricsOut
$baselineEasyWinRate = Get-DifficultyWinRate -Runs $baselinePayload.runs -Difficulty "Easy"
$baselineNormalWinRate = Get-DifficultyWinRate -Runs $baselinePayload.runs -Difficulty "Normal"
$baselineHardWinRate = Get-DifficultyWinRate -Runs $baselinePayload.runs -Difficulty "Hard"

$effectiveTargetWinRateEasy = [double]$TargetWinRateEasy
$effectiveTargetWinRateNormal = [double]$TargetWinRateNormal
$effectiveTargetWinRateHard = [double]$TargetWinRateHard
if ($UseBaselineRelativeWinTargets) {
    $effectiveTargetWinRateEasy = Clamp-Double -Value ([Math]::Min([double]$TargetWinRateEasy, [double]$baselineEasyWinRate + [double]$RelativeWinTargetEasyUplift)) -Min 0.0 -Max 1.0
    $effectiveTargetWinRateNormal = Clamp-Double -Value ([Math]::Min([double]$TargetWinRateNormal, [double]$baselineNormalWinRate + [double]$RelativeWinTargetNormalUplift)) -Min 0.0 -Max 1.0
    $effectiveTargetWinRateHard = Clamp-Double -Value ([Math]::Min([double]$TargetWinRateHard, [double]$baselineHardWinRate + [double]$RelativeWinTargetHardUplift)) -Min 0.0 -Max 1.0
}
Write-Host ("Win-rate targets (effective): easy={0:P2}, normal={1:P2}, hard={2:P2}" -f $effectiveTargetWinRateEasy, $effectiveTargetWinRateNormal, $effectiveTargetWinRateHard)
if ($UseBaselineRelativeWinTargets) {
    Write-Host ("Baseline-relative uplift mode: on (easy+{0:P2}, normal+{1:P2}, hard+{2:P2}, capped by absolute targets)" -f $RelativeWinTargetEasyUplift, $RelativeWinTargetNormalUplift, $RelativeWinTargetHardUplift)
} else {
    Write-Host "Baseline-relative uplift mode: off (using absolute target win rates)."
}

$baselineScore = Get-MetricsScore `
    -Summary $baselinePayload.summary `
    -Runs $baselinePayload.runs `
    -TargetShare $TargetExplosionShare `
    -TargetShareTolerance $TargetExplosionShareTolerance `
    -TargetEasyWinRate $effectiveTargetWinRateEasy `
    -TargetNormalWinRate $effectiveTargetWinRateNormal `
    -TargetHardWinRate $effectiveTargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxSurgesPerRun $TargetMaxSurgesPerRun `
    -TargetMaxSurgesPerRunTolerance $TargetMaxSurgesPerRunTolerance `
    -SurgesPerRunPenaltyWeight $SurgesPerRunPenaltyWeight `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -ChainDepthCap $MaxChainDepth `
    -ExplosionCap $MaxSimultaneousExplosions `
    -HazardCap $MaxSimultaneousHazards `
    -HitStopCap $MaxSimultaneousHitStops `
    -DurationFloor $MinRunDurationSeconds

$guardMinNormalWinRate = [Math]::Max(0.0, [double]$baselineScore.NormalWinRate - [double]$DifficultyRegressionTolerance)
$guardMinHardWinRate = [Math]::Max(0.0, [double]$baselineScore.HardWinRate - [double]$DifficultyRegressionTolerance)
Write-Host ("Regression guards: normal>={0:P2}, hard>={1:P2} (tolerance={2:P2})" -f $guardMinNormalWinRate, $guardMinHardWinRate, $DifficultyRegressionTolerance)

$startProfileRaw = Get-Content -Raw $tuningFileResolved | ConvertFrom-Json
$startProfile = Normalize-TuningProfile -InputProfile $startProfileRaw

$seedTuningPath = Join-Path $autoTuneDir "seed_tuning.json"
$seedMetricsPath = Join-Path $autoTuneDir "seed_metrics.json"
Write-TuningProfile -Profile $startProfile -OutputPath $seedTuningPath
Invoke-BotMetricsRunSharded -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -TuningPath $seedTuningPath -MetricsOut $seedMetricsPath -Label "[2/8] Evaluate starting tuning profile" -Parallelism $effectiveRunParallelism
$seedPayload = Read-MetricsPayload -MetricsPath $seedMetricsPath
$seedScore = Get-MetricsScore `
    -Summary $seedPayload.summary `
    -Runs $seedPayload.runs `
    -TargetShare $TargetExplosionShare `
    -TargetShareTolerance $TargetExplosionShareTolerance `
    -TargetEasyWinRate $effectiveTargetWinRateEasy `
    -TargetNormalWinRate $effectiveTargetWinRateNormal `
    -TargetHardWinRate $effectiveTargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxSurgesPerRun $TargetMaxSurgesPerRun `
    -TargetMaxSurgesPerRunTolerance $TargetMaxSurgesPerRunTolerance `
    -SurgesPerRunPenaltyWeight $SurgesPerRunPenaltyWeight `
    -MinNormalWinRate $guardMinNormalWinRate `
    -MinHardWinRate $guardMinHardWinRate `
    -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
    -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -ChainDepthCap $MaxChainDepth `
    -ExplosionCap $MaxSimultaneousExplosions `
    -HazardCap $MaxSimultaneousHazards `
    -HitStopCap $MaxSimultaneousHitStops `
    -DurationFloor $MinRunDurationSeconds

$globalBestProfile = Clone-TuningProfile -Profile $startProfile
$globalBestTuningPath = $seedTuningPath
$globalBestMetricsPath = $seedMetricsPath
$globalBestScore = [double]$seedScore.Score
$globalBestSource = "seed"

$history = @()
$history += [PSCustomObject]@{
    iteration = 0
    candidate = "seed"
    candidate_type = "starting_profile"
    anneal = 1.0
    score = $seedScore.Score
    win_rate = $seedScore.WinRate
    easy_win_rate = $seedScore.EasyWinRate
    normal_win_rate = $seedScore.NormalWinRate
    hard_win_rate = $seedScore.HardWinRate
    avg_wave = $seedScore.AvgWave
    explosion_share = $seedScore.ExplosionShare
    run_duration_seconds = $seedScore.RunDurationSeconds
    surges_per_run = $seedScore.SurgesPerRun
    tower_surge_parity_ratio = $seedScore.TowerSurgeParityRatio
    tower_surge_parity_eligible_towers = $seedScore.TowerSurgeParityEligibleTowers
    avg_max_chain_depth = $seedScore.AvgMaxChainDepth
    frame_stress_peaks = [PSCustomObject]@{
        explosions = $seedScore.PeakExplosions
        hazards = $seedScore.PeakHazards
        hitstops = $seedScore.PeakHitStops
    }
    tuning_file = $seedTuningPath
    metrics_file = $seedMetricsPath
}

for ($iteration = 1; $iteration -le $Iterations; $iteration++) {
    $iterDirName = "iter_{0:d2}" -f $iteration
    $iterDir = Join-Path $autoTuneDir $iterDirName
    Ensure-Directory -PathValue $iterDir

    $anneal = if ($Iterations -gt 1) {
        1.0 - (([double]($iteration - 1) / [double]($Iterations - 1)) * 0.65)
    } else {
        1.0
    }

    Write-Host ""
    Write-Host "=== [3/8] Auto-tune iteration $iteration/$Iterations (anneal=$([Math]::Round($anneal,3))) ==="

    $iterCandidates = @()
    $candidateSpecsToEvaluate = @()
    $allCandidateSpecs = @()

    for ($candidateIndex = 1; $candidateIndex -le $CandidatesPerIteration; $candidateIndex++) {
        $candidateId = "iter{0:d2}_cand{1:d2}" -f $iteration, $candidateIndex
        $candidateType = if ($candidateIndex -eq 1) { "carry_forward" } else { "mutated" }
        $candidateProfile = if ($candidateIndex -eq 1) {
            Clone-TuningProfile -Profile $globalBestProfile
        } else {
            New-MutatedTuningProfile -BaseProfile $globalBestProfile -Anneal $anneal -Strength $MutationStrength
        }

        $candidateTuningPath = Join-Path $iterDir "$candidateId.tuning.json"
        $reuseGlobalBestMetrics = $candidateIndex -eq 1
        $candidateMetricsPath = if ($reuseGlobalBestMetrics) {
            $globalBestMetricsPath
        } else {
            Join-Path $iterDir "$candidateId.metrics.json"
        }
        Write-TuningProfile -Profile $candidateProfile -OutputPath $candidateTuningPath
        $candidateSpec = [PSCustomObject]@{
            candidate_id = $candidateId
            candidate_type = $candidateType
            tuning_path = $candidateTuningPath
            metrics_path = $candidateMetricsPath
            label = "[3/8][$iteration/$Iterations] Evaluate $candidateId ($candidateType)"
            reuse_cached_metrics = $reuseGlobalBestMetrics
        }
        $allCandidateSpecs += $candidateSpec
        if (-not $reuseGlobalBestMetrics) {
            $candidateSpecsToEvaluate += $candidateSpec
        } else {
            Write-Host "Candidate ${candidateId}: reusing global-best metrics from $candidateMetricsPath"
        }
    }

    # TODO(perf-step-3): Implement multi-fidelity search:
    # Stage A evaluate all mutated candidates with coarse runs, then Stage B reevaluate top-K with full runs.
    if ($candidateSpecsToEvaluate.Count -gt 0) {
        Invoke-BotMetricsRunBatch `
            -GodotExe $godotExe `
            -CommonPrefix $commonPrefix `
            -RunsPerEval $Runs `
            -StrategySet $strategySetNormalized `
            -CandidateSpecs $candidateSpecsToEvaluate `
            -Parallelism $effectiveCandidateParallelism
    } else {
        Write-Host "No candidate evaluations required for iteration $iteration."
    }

    foreach ($spec in $allCandidateSpecs) {
        $candidateId = [string]$spec.candidate_id
        $candidateType = [string]$spec.candidate_type
        $candidateTuningPath = [string]$spec.tuning_path
        $candidateMetricsPath = [string]$spec.metrics_path
        $candidatePayload = Read-MetricsPayload -MetricsPath $candidateMetricsPath
        $candidateScore = Get-MetricsScore `
            -Summary $candidatePayload.summary `
            -Runs $candidatePayload.runs `
            -TargetShare $TargetExplosionShare `
            -TargetShareTolerance $TargetExplosionShareTolerance `
            -TargetEasyWinRate $effectiveTargetWinRateEasy `
            -TargetNormalWinRate $effectiveTargetWinRateNormal `
            -TargetHardWinRate $effectiveTargetWinRateHard `
            -WinRateTolerance $TargetWinRateTolerance `
            -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
            -TargetMaxSurgesPerRun $TargetMaxSurgesPerRun `
            -TargetMaxSurgesPerRunTolerance $TargetMaxSurgesPerRunTolerance `
            -SurgesPerRunPenaltyWeight $SurgesPerRunPenaltyWeight `
            -MinNormalWinRate $guardMinNormalWinRate `
            -MinHardWinRate $guardMinHardWinRate `
            -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
            -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
            -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
            -ChainDepthCap $MaxChainDepth `
            -ExplosionCap $MaxSimultaneousExplosions `
            -HazardCap $MaxSimultaneousHazards `
            -HitStopCap $MaxSimultaneousHitStops `
            -DurationFloor $MinRunDurationSeconds

        $candidateResult = [PSCustomObject]@{
            iteration = $iteration
            candidate = $candidateId
            candidate_type = $candidateType
            anneal = [Math]::Round($anneal, 4)
            score = $candidateScore.Score
            effective_score = $candidateScore.Score
            win_rate = $candidateScore.WinRate
            easy_win_rate = $candidateScore.EasyWinRate
            normal_win_rate = $candidateScore.NormalWinRate
            hard_win_rate = $candidateScore.HardWinRate
            avg_wave = $candidateScore.AvgWave
            explosion_share = $candidateScore.ExplosionShare
            run_duration_seconds = $candidateScore.RunDurationSeconds
            surges_per_run = $candidateScore.SurgesPerRun
            tower_surge_parity_ratio = $candidateScore.TowerSurgeParityRatio
            tower_surge_parity_eligible_towers = $candidateScore.TowerSurgeParityEligibleTowers
            avg_max_chain_depth = $candidateScore.AvgMaxChainDepth
            frame_stress_peaks = [PSCustomObject]@{
                explosions = $candidateScore.PeakExplosions
                hazards = $candidateScore.PeakHazards
                hitstops = $candidateScore.PeakHitStops
            }
            tuning_file = $candidateTuningPath
            metrics_file = $candidateMetricsPath
            promoted_metrics_file = $candidateMetricsPath
            reevaluated = $false
            reeval_score = $null
            reeval_metrics_file = $null
        }

        $iterCandidates += $candidateResult
        $history += $candidateResult
        Write-Host ("Candidate {0}: score={1:0.0000}, win(E/N/H)={2:P1}/{3:P1}/{4:P1}, wave={5:0.00}, surges/run={6:0.00}, explosion_share={7:P2}, surge_parity_ratio={8:0.00}" -f $candidateId, $candidateScore.Score, $candidateScore.EasyWinRate, $candidateScore.NormalWinRate, $candidateScore.HardWinRate, $candidateScore.AvgWave, $candidateScore.SurgesPerRun, $candidateScore.ExplosionShare, $candidateScore.TowerSurgeParityRatio)
    }

    $reevalCount = [Math]::Min([Math]::Max(0, $TopCandidateReevalCount), $iterCandidates.Count)
    if ($reevalCount -gt 0) {
        $topCandidatesForReeval = @($iterCandidates | Sort-Object -Property score -Descending | Select-Object -First $reevalCount)
        $reevalSpecMap = @{}
        $reevalSpecs = @()
        foreach ($candidate in $topCandidatesForReeval) {
            $candidateId = [string]$candidate.candidate
            $reevalMetricsPath = Join-Path $iterDir "$candidateId.reeval.metrics.json"
            $reevalSpecMap[$candidateId] = $reevalMetricsPath
            $reevalSpecs += [PSCustomObject]@{
                candidate_id = "${candidateId}_reeval"
                tuning_path = [string]$candidate.tuning_file
                metrics_path = $reevalMetricsPath
                label = "[3/8][$iteration/$Iterations] Re-evaluate $candidateId (confirm)"
            }
        }

        Invoke-BotMetricsRunBatch `
            -GodotExe $godotExe `
            -CommonPrefix $commonPrefix `
            -RunsPerEval $Runs `
            -StrategySet $strategySetNormalized `
            -CandidateSpecs $reevalSpecs `
            -Parallelism $effectiveCandidateParallelism

        foreach ($candidate in $topCandidatesForReeval) {
            $candidateId = [string]$candidate.candidate
            $reevalMetricsPath = [string]$reevalSpecMap[$candidateId]
            $reevalPayload = Read-MetricsPayload -MetricsPath $reevalMetricsPath
            $reevalScore = Get-MetricsScore `
                -Summary $reevalPayload.summary `
                -Runs $reevalPayload.runs `
                -TargetShare $TargetExplosionShare `
                -TargetShareTolerance $TargetExplosionShareTolerance `
                -TargetEasyWinRate $effectiveTargetWinRateEasy `
                -TargetNormalWinRate $effectiveTargetWinRateNormal `
                -TargetHardWinRate $effectiveTargetWinRateHard `
                -WinRateTolerance $TargetWinRateTolerance `
                -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
                -TargetMaxSurgesPerRun $TargetMaxSurgesPerRun `
                -TargetMaxSurgesPerRunTolerance $TargetMaxSurgesPerRunTolerance `
                -SurgesPerRunPenaltyWeight $SurgesPerRunPenaltyWeight `
                -MinNormalWinRate $guardMinNormalWinRate `
                -MinHardWinRate $guardMinHardWinRate `
                -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
                -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
                -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
                -ChainDepthCap $MaxChainDepth `
                -ExplosionCap $MaxSimultaneousExplosions `
                -HazardCap $MaxSimultaneousHazards `
                -HitStopCap $MaxSimultaneousHitStops `
                -DurationFloor $MinRunDurationSeconds

            $candidate.reevaluated = $true
            $candidate.reeval_score = $reevalScore.Score
            $candidate.reeval_metrics_file = $reevalMetricsPath
            $candidate.promoted_metrics_file = $reevalMetricsPath
            $candidate.effective_score = [Math]::Round((([double]$candidate.score) + ([double]$reevalScore.Score)) / 2.0, 4)
            Write-Host ("Candidate {0}: reeval_score={1:0.0000}, effective_score={2:0.0000}" -f $candidateId, $reevalScore.Score, $candidate.effective_score)
        }
    }

    $iterBest = $iterCandidates | Sort-Object -Property @{ Expression = "effective_score"; Descending = $true }, @{ Expression = "score"; Descending = $true } | Select-Object -First 1
    if ($null -eq $iterBest) {
        throw "Iteration $iteration produced no candidates."
    }

    Write-Host "Iteration $iteration best: $($iterBest.candidate) effective_score=$($iterBest.effective_score) raw_score=$($iterBest.score)"

    $iterationImprovedGlobalBest = $false
    $previousGlobalBestScore = $globalBestScore
    $previousGlobalBestProfile = Clone-TuningProfile -Profile $globalBestProfile
    $previousGlobalBestTuningPath = $globalBestTuningPath
    $previousGlobalBestMetricsPath = $globalBestMetricsPath
    $previousGlobalBestSource = $globalBestSource
    if ([double]$iterBest.effective_score -gt $globalBestScore) {
        $globalBestScore = [double]$iterBest.effective_score
        $globalBestProfile = Normalize-TuningProfile -InputProfile (Get-Content -Raw $iterBest.tuning_file | ConvertFrom-Json)
        $globalBestTuningPath = $iterBest.tuning_file
        $globalBestMetricsPath = [string]$iterBest.promoted_metrics_file
        $globalBestSource = $iterBest.candidate
        $iterationImprovedGlobalBest = $true
        Write-Host "New global best found at iteration ${iteration}: $globalBestSource (score=$globalBestScore)"
    } else {
        Write-Host "Global best unchanged: $globalBestSource (score=$globalBestScore)"
    }

    if ($iterationImprovedGlobalBest) {
        $iterScenarioOut = Join-Path $iterDir "combat_lab_report.iter.json"
        Invoke-GodotCommand -GodotExe $godotExe -Label "[4/8][$iteration/$Iterations] Scenario validation for global best" -Args (
            $commonPrefix + @("--tuning_file", $globalBestTuningPath, "--lab_scenario", $scenarioFileResolved, "--lab_out", $iterScenarioOut)
        )

        $iterSweepConfig = Join-Path $iterDir "generated_sweep.iter.json"
        Write-SweepComparisonConfig `
            -OutputPath $iterSweepConfig `
            -ScenarioFilePath $scenarioFileResolved `
            -RunsPerVariant $SweepRunsPerVariant `
            -BestProfile $globalBestProfile `
            -BestId ("iter_{0:d2}_global_best" -f $iteration) `
            -SweepName ("autotune_iter_{0:d2}" -f $iteration)

        $iterSweepOut = Join-Path $iterDir "combat_lab_sweep.iter.json"
        Invoke-GodotCommand -GodotExe $godotExe -Label "[5/8][$iteration/$Iterations] Sweep comparison baseline vs global best" -Args (
            $commonPrefix + @("--lab_sweep", $iterSweepConfig, "--lab_out", $iterSweepOut)
        )

        $iterSweepBaselineScore = Get-SweepVariantScore -SweepReportPath $iterSweepOut -VariantId "baseline"
        $iterSweepBestScore = Get-SweepVariantScore -SweepReportPath $iterSweepOut -VariantId ("iter_{0:d2}_global_best" -f $iteration)
        $requiredSweepScore = $iterSweepBaselineScore * [Math]::Max(0.0, $MinSweepScoreRatioVsBaseline)
        if ($iterSweepBestScore + 0.000001 -lt $requiredSweepScore) {
            Write-Host ("Rejecting candidate {0}: sweep score {1:0.###} below required threshold {2:0.###} (baseline={3:0.###}, ratio={4:0.###})." -f $globalBestSource, $iterSweepBestScore, $requiredSweepScore, $iterSweepBaselineScore, $MinSweepScoreRatioVsBaseline)
            $globalBestScore = $previousGlobalBestScore
            $globalBestProfile = Clone-TuningProfile -Profile $previousGlobalBestProfile
            $globalBestTuningPath = $previousGlobalBestTuningPath
            $globalBestMetricsPath = $previousGlobalBestMetricsPath
            $globalBestSource = $previousGlobalBestSource
            $iterationImprovedGlobalBest = $false
        }

        if ($iterationImprovedGlobalBest) {
            $iterDeltaOut = Join-Path $iterDir "bot_metrics_delta.iter.txt"
            Invoke-GodotCommand -GodotExe $godotExe -Label "[6/8][$iteration/$Iterations] Delta baseline vs iteration best candidate" -Args (
                $commonPrefix + @("--metrics_delta", $baselineMetricsOut, $iterBest.promoted_metrics_file, "--delta_out", $iterDeltaOut)
            )
        } else {
            Write-Host "Skipping [6/8] delta for iteration $iteration (candidate rejected by sweep guardrail)."
        }
    } else {
        Write-Host "Skipping [4/8]-[6/8] for iteration $iteration (global best unchanged)."
    }
}

Copy-Item -Path $globalBestTuningPath -Destination $bestTuningOut -Force

Invoke-GodotCommand -GodotExe $godotExe -Label "[7/8] Final scenario suite (global best)" -Args (
    $commonPrefix + @("--tuning_file", $bestTuningOut, "--lab_scenario", $scenarioFileResolved, "--lab_out", $scenarioReportOut)
)

$finalSweepConfig = Join-Path $runDir "generated_sweep.final.json"
Write-SweepComparisonConfig `
    -OutputPath $finalSweepConfig `
    -ScenarioFilePath $scenarioFileResolved `
    -RunsPerVariant $SweepRunsPerVariant `
    -BestProfile $globalBestProfile `
    -BestId "global_best" `
    -SweepName "autotune_final_baseline_vs_best"
Invoke-GodotCommand -GodotExe $godotExe -Label "[7/8] Final sweep comparison (baseline vs global best)" -Args (
    $commonPrefix + @("--lab_sweep", $finalSweepConfig, "--lab_out", $sweepReportOut)
)

Invoke-BotMetricsRunSharded -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -TuningPath $bestTuningOut -MetricsOut $tunedMetricsOut -Label "[8/8] Final tuned bot metrics (global best)" -Parallelism $effectiveRunParallelism

if (-not $SkipTrace) {
    Invoke-GodotCommand -GodotExe $godotExe -Label "[8/8] Live bot trace capture (global best)" -Args (
        $commonPrefix + @("--bot", "--runs", "$Runs", "--strategy_set", $strategySetNormalized, "--tuning_file", $bestTuningOut, "--bot_trace_out", $traceOut)
    )
} else {
    Write-Host ""
    Write-Host "=== [8/8] Live bot trace capture skipped (-SkipTrace) ==="
}

Invoke-GodotCommand -GodotExe $godotExe -Label "[8/8] Baseline vs tuned metrics delta (global best)" -Args (
    $commonPrefix + @("--metrics_delta", $baselineMetricsOut, $tunedMetricsOut, "--delta_out", $deltaOut)
)

$tunedPayload = Read-MetricsPayload -MetricsPath $tunedMetricsOut
$tunedScore = Get-MetricsScore `
    -Summary $tunedPayload.summary `
    -Runs $tunedPayload.runs `
    -TargetShare $TargetExplosionShare `
    -TargetShareTolerance $TargetExplosionShareTolerance `
    -TargetEasyWinRate $effectiveTargetWinRateEasy `
    -TargetNormalWinRate $effectiveTargetWinRateNormal `
    -TargetHardWinRate $effectiveTargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxSurgesPerRun $TargetMaxSurgesPerRun `
    -TargetMaxSurgesPerRunTolerance $TargetMaxSurgesPerRunTolerance `
    -SurgesPerRunPenaltyWeight $SurgesPerRunPenaltyWeight `
    -MinNormalWinRate $guardMinNormalWinRate `
    -MinHardWinRate $guardMinHardWinRate `
    -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
    -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -ChainDepthCap $MaxChainDepth `
    -ExplosionCap $MaxSimultaneousExplosions `
    -HazardCap $MaxSimultaneousHazards `
    -HitStopCap $MaxSimultaneousHitStops `
    -DurationFloor $MinRunDurationSeconds

$reportPayload = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    runs = $Runs
    iterations = $Iterations
    candidates_per_iteration = $CandidatesPerIteration
    candidate_parallelism = $effectiveCandidateParallelism
    eval_shard_parallelism = $effectiveRunParallelism
    strategy_set = $strategySetNormalized
    seed = $Seed
    scoring = [ordered]@{
        target_explosion_share = $TargetExplosionShare
        target_explosion_share_tolerance = $TargetExplosionShareTolerance
        use_baseline_relative_win_targets = $UseBaselineRelativeWinTargets
        target_win_rate_easy = $TargetWinRateEasy
        target_win_rate_normal = $TargetWinRateNormal
        target_win_rate_hard = $TargetWinRateHard
        effective_target_win_rate_easy = $effectiveTargetWinRateEasy
        effective_target_win_rate_normal = $effectiveTargetWinRateNormal
        effective_target_win_rate_hard = $effectiveTargetWinRateHard
        relative_win_target_easy_uplift = $RelativeWinTargetEasyUplift
        relative_win_target_normal_uplift = $RelativeWinTargetNormalUplift
        relative_win_target_hard_uplift = $RelativeWinTargetHardUplift
        target_win_rate_tolerance = $TargetWinRateTolerance
        difficulty_regression_tolerance = $DifficultyRegressionTolerance
        normal_regression_penalty_weight = $NormalRegressionPenaltyWeight
        hard_regression_penalty_weight = $HardRegressionPenaltyWeight
        min_sweep_score_ratio_vs_baseline = $MinSweepScoreRatioVsBaseline
        target_max_tower_surge_ratio = $TargetMaxTowerSurgeRatio
        target_max_surges_per_run = $TargetMaxSurgesPerRun
        target_max_surges_per_run_tolerance = $TargetMaxSurgesPerRunTolerance
        surges_per_run_penalty_weight = $SurgesPerRunPenaltyWeight
        min_tower_placements_for_parity = $MinTowerPlacementsForParity
        max_chain_depth = $MaxChainDepth
        max_simultaneous_explosions = $MaxSimultaneousExplosions
        max_simultaneous_hazards = $MaxSimultaneousHazards
        max_simultaneous_hitstops = $MaxSimultaneousHitStops
        min_run_duration_seconds = $MinRunDurationSeconds
        top_candidate_reeval_count = $TopCandidateReevalCount
    }
    baseline = [ordered]@{
        metrics_file = $baselineMetricsOut
        score = $baselineScore.Score
        avg_wave = $baselineScore.AvgWave
        win_rate = $baselineScore.WinRate
        easy_win_rate = $baselineScore.EasyWinRate
        normal_win_rate = $baselineScore.NormalWinRate
        hard_win_rate = $baselineScore.HardWinRate
        surges_per_run = $baselineScore.SurgesPerRun
        explosion_share = $baselineScore.ExplosionShare
        tower_surge_parity_ratio = $baselineScore.TowerSurgeParityRatio
        tower_surge_parity_eligible_towers = $baselineScore.TowerSurgeParityEligibleTowers
    }
    best = [ordered]@{
        source_candidate = $globalBestSource
        best_score_during_search = [Math]::Round($globalBestScore, 4)
        tuning_file = $bestTuningOut
        metrics_file = $tunedMetricsOut
        final_score = $tunedScore.Score
        final_avg_wave = $tunedScore.AvgWave
        final_win_rate = $tunedScore.WinRate
        final_easy_win_rate = $tunedScore.EasyWinRate
        final_normal_win_rate = $tunedScore.NormalWinRate
        final_hard_win_rate = $tunedScore.HardWinRate
        final_surges_per_run = $tunedScore.SurgesPerRun
        final_explosion_share = $tunedScore.ExplosionShare
        final_tower_surge_parity_ratio = $tunedScore.TowerSurgeParityRatio
        final_tower_surge_parity_eligible_towers = $tunedScore.TowerSurgeParityEligibleTowers
    }
    history = $history
}

$reportJson = $reportPayload | ConvertTo-Json -Depth 14
Set-Content -Path $autoTuneReportOut -Value $reportJson -Encoding UTF8

Write-Host ""
Write-Host "=== Pipeline complete ==="
Write-Host "Baseline metrics : $baselineMetricsOut"
Write-Host "Best tuning file : $bestTuningOut"
Write-Host "Scenario report  : $scenarioReportOut"
Write-Host "Sweep report     : $sweepReportOut"
Write-Host "Tuned metrics    : $tunedMetricsOut"
if (-not $SkipTrace) {
    Write-Host "Trace file       : $traceOut"
}
Write-Host "Delta report     : $deltaOut"
Write-Host "Auto-tune report : $autoTuneReportOut"
