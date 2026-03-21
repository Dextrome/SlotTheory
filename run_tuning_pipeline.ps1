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
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SkipTowerBenchmark
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SkipModifierBenchmark
#
# TODO(perf-step-3): Add multi-fidelity candidate evaluation (coarse runs for all candidates, full runs for shortlisted candidates).
# DONE(perf-step-4): -EvalShardParallelism decouples per-eval shard count from candidate-level parallelism.
# TODO(perf-step-5): Add deterministic metrics cache keyed by tuning hash + runs + strategy_set + run_index_offset.
# TODO(perf-step-6): Add explicit "search mode" defaults/presets plus a final confirmation pass mode.
# DONE: Eval + reeval are now merged into a combined payload before scoring (eliminates score-averaging variance).
# DONE: -FreezeSpectacleParams / -DifficultyOnlyMode suppress spectacle mutations when those params carry no signal.

[CmdletBinding()]
param(
    [int]$Runs = 200,
    [Alias("TuningIterations")][int]$Iterations = 4,
    [int]$CandidatesPerIteration = 3,
    [int]$CandidateParallelism = 1,
    # Number of parallel shards per candidate eval (splits N runs across K Godot processes).
    # 0 = inherit from CandidateParallelism (legacy behaviour). Set independently to decouple
    # candidate-level concurrency from per-eval shard concurrency, e.g.:
    #   -CandidateParallelism 2 -EvalShardParallelism 4  => 2 candidates × 4 shards = 8 processes
    [int]$EvalShardParallelism = 0,
    [int]$SweepRunsPerVariant = 12,
    [int]$Seed = 1337,
    [double]$MutationStrength = 1.0,
    [double]$TargetExplosionShare = 0.02,
    [double]$TargetExplosionShareTolerance = 0.05,
    [double]$TargetWinRateEasy = 0.90,
    [double]$TargetWinRateNormal = 0.70,
    [double]$TargetWinRateHard = 0.40,
    [bool]$UseBaselineRelativeWinTargets = $false,
    [double]$RelativeWinTargetEasyUplift = 0.05,
    [double]$RelativeWinTargetNormalUplift = 0.08,
    [double]$RelativeWinTargetHardUplift = 0.06,
    [double]$TargetWinRateTolerance = 0.06,
    [double]$TargetSurgesPerRun = 36.0,
    [double]$TargetSurgesPerRunTolerance = 8.0,
    [double]$MaxKillsPerSurge = 0.70,
    [double]$MinGlobalSurgesPerRun = 1.20,
    [double]$DifficultyRegressionTolerance = 0.01,
    [double]$NormalRegressionPenaltyWeight = 260.0,
    [double]$HardRegressionPenaltyWeight = 320.0,
    [double]$TargetMaxTowerSurgeRatio = 2.0,
    [double]$TargetMaxModifierSurgeRatio = 2.2,
    [double]$TargetTowerWinRateGap = 0.18,
    [double]$TargetModifierWinRateGap = 0.20,
    [double]$HardGuardMaxTowerSurgeRatio = 2.8,
    [double]$HardGuardMaxTowerWinRateGap = 0.28,
    [int]$MinTowerRunsForFairness = 40,
    [int]$MinModifierRunsForFairness = 50,
    [int]$MinModifierRunsForSurgeParity = 40,
    [double]$MinSweepScoreRatioVsBaseline = 1.0,
    [int]$MinTowerPlacementsForParity = 6,
    [double]$MaxChainDepth = 4.0,
    [int]$MaxSimultaneousExplosions = 8,
    [int]$MaxSimultaneousHazards = 12,
    [int]$MaxSimultaneousHitStops = 4,
    [double]$MinRunDurationSeconds = 900.0,
    [int]$TopCandidateReevalCount = 3,
    [bool]$UseFastBotMetrics = $true,
    [bool]$UseBotQuiet = $true,
    [string]$StrategySet = "optimization",
    [string]$GodotPath = "",
    [string]$TuningFile = "",
    [string]$ScenarioFile = "Data/combat_lab/core_scenarios.json",
    [string]$TowerBenchmarkFile = "Data/combat_lab/tower_benchmark_core.json",
    [string]$ModifierBenchmarkFile = "Data/combat_lab/modifier_benchmark_core.json",
    [string]$OutputRoot = "release/tuning_pipeline",
    [switch]$SkipBuild,
    [switch]$SkipTrace,
    [switch]$SkipTowerBenchmark,
    [switch]$SkipModifierBenchmark,
    # Freeze spectacle params (overkill bloom, residue, detonation) during mutation so the search
    # only moves difficulty levers. Useful when the strategy set doesn't trigger spectacle systems
    # and those params produce no signal (just noise). DifficultyOnlyMode implies this.
    [switch]$FreezeSpectacleParams,
    # Restrict mutation to the 10 difficulty params only (enemy hp/count/spawn + tanky/swift count).
    # Use this when the sole goal is hitting Normal/Hard win-rate targets; quality params can be
    # tuned in a separate pass once the difficulty curve is stable.
    [switch]$DifficultyOnlyMode,
    # Restrict difficulty mutations to Normal-only params (normal_enemy_*, normal_tanky_count,
    # normal_swift_count). Freezes hard_* params so Hard difficulty cannot drift. Implies
    # DifficultyOnlyMode (spectacle params are also frozen). Use when targeting Normal win-rate
    # without disturbing the Hard curve.
    [switch]$NormalOnlyMode,
    # Restrict mutation to spectacle params only (overkill bloom, residue, detonation, explosion).
    # Use with -StrategySet spectacle so the scorer actually sees explosion/residue signal.
    # Difficulty params are frozen; win-rate targets are treated as guardrails only.
    [switch]$SpectacleOnlyMode,
    # Enables two-pass search:
    # pass 1 prioritizes surge parity/fairness, pass 2 re-optimizes difficulty and win-rate targets.
    [switch]$TwoPassMode,
    # Percentage of iterations allocated to pass 1 when -TwoPassMode is enabled.
    [int]$TwoPassPhaseSplitPercent = 50,
    # Multiplies run count during pass 2 only when -TwoPassMode is enabled.
    # Example: Runs=420 and TwoPassPass2RunsMultiplier=1.5 -> pass 2 uses 630 runs.
    [double]$TwoPassPass2RunsMultiplier = 1.5,
    # Skip final all-strategy validation pass (strategy_set=all) if you only need optimization-set results.
    [switch]$SkipAllStrategyValidation,
    # Run bot sims in demo mode: passes --demo to all Godot invocations so Shield Drone and
    # Reverse Walker are zeroed out. Final best_tuning.json is also written to
    # Data/best_tuning_demo.json for use by demo builds at startup.
    # Without this flag, the result is written to Data/best_tuning_full.json.
    [switch]$Demo
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
        easy_enemy_hp_multiplier = 1.0
        easy_enemy_count_multiplier = 1.0
        easy_spawn_interval_multiplier = 1.0
        easy_hp_growth_multiplier = 1.0
        normal_hp_growth_multiplier = 1.0
        hard_hp_growth_multiplier = 1.0
        normal_enemy_hp_multiplier = 1.24
        normal_enemy_count_multiplier = 1.1
        normal_spawn_interval_multiplier = 0.95
        hard_enemy_hp_multiplier = 1.40
        hard_enemy_count_multiplier = 1.15
        hard_spawn_interval_multiplier = 0.90
        easy_tanky_count_multiplier = 1.0
        easy_swift_count_multiplier = 1.0
        easy_splitter_count_multiplier = 1.0
        easy_reverse_count_multiplier = 1.0
        easy_shield_drone_count_multiplier = 1.0
        normal_tanky_count_multiplier = 1.0
        normal_swift_count_multiplier = 1.0
        normal_splitter_count_multiplier = 1.0
        normal_reverse_count_multiplier = 1.0
        normal_shield_drone_count_multiplier = 1.0
        hard_tanky_count_multiplier = 1.0
        hard_swift_count_multiplier = 1.0
        hard_splitter_count_multiplier = 1.0
        hard_reverse_count_multiplier = 1.0
        hard_shield_drone_count_multiplier = 1.0
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
        tower_meter_gain_multipliers = [PSCustomObject]@{
            rapid_shooter = 1.0
            heavy_cannon = 1.0
            marker_tower = 1.0
            chain_tower = 1.0
            rift_prism = 1.0
        }
        tower_surge_threshold_multipliers = [PSCustomObject]@{
            rapid_shooter = 1.0
            heavy_cannon = 1.0
            marker_tower = 1.0
            chain_tower = 1.0
            rift_prism = 1.0
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
    if ($props -contains "easy_enemy_hp_multiplier") { $p.easy_enemy_hp_multiplier = [double]$InputProfile.easy_enemy_hp_multiplier }
    if ($props -contains "easy_enemy_count_multiplier") { $p.easy_enemy_count_multiplier = [double]$InputProfile.easy_enemy_count_multiplier }
    if ($props -contains "easy_spawn_interval_multiplier") { $p.easy_spawn_interval_multiplier = [double]$InputProfile.easy_spawn_interval_multiplier }
    if ($props -contains "easy_hp_growth_multiplier") { $p.easy_hp_growth_multiplier = [double]$InputProfile.easy_hp_growth_multiplier }
    if ($props -contains "normal_hp_growth_multiplier") { $p.normal_hp_growth_multiplier = [double]$InputProfile.normal_hp_growth_multiplier }
    if ($props -contains "hard_hp_growth_multiplier") { $p.hard_hp_growth_multiplier = [double]$InputProfile.hard_hp_growth_multiplier }
    if ($props -contains "normal_enemy_hp_multiplier") { $p.normal_enemy_hp_multiplier = [double]$InputProfile.normal_enemy_hp_multiplier }
    if ($props -contains "normal_enemy_count_multiplier") { $p.normal_enemy_count_multiplier = [double]$InputProfile.normal_enemy_count_multiplier }
    if ($props -contains "normal_spawn_interval_multiplier") { $p.normal_spawn_interval_multiplier = [double]$InputProfile.normal_spawn_interval_multiplier }
    if ($props -contains "hard_enemy_hp_multiplier") { $p.hard_enemy_hp_multiplier = [double]$InputProfile.hard_enemy_hp_multiplier }
    if ($props -contains "hard_enemy_count_multiplier") { $p.hard_enemy_count_multiplier = [double]$InputProfile.hard_enemy_count_multiplier }
    if ($props -contains "hard_spawn_interval_multiplier") { $p.hard_spawn_interval_multiplier = [double]$InputProfile.hard_spawn_interval_multiplier }
    if ($props -contains "easy_tanky_count_multiplier") { $p.easy_tanky_count_multiplier = [double]$InputProfile.easy_tanky_count_multiplier }
    if ($props -contains "easy_swift_count_multiplier") { $p.easy_swift_count_multiplier = [double]$InputProfile.easy_swift_count_multiplier }
    if ($props -contains "easy_splitter_count_multiplier") { $p.easy_splitter_count_multiplier = [double]$InputProfile.easy_splitter_count_multiplier }
    if ($props -contains "easy_reverse_count_multiplier") { $p.easy_reverse_count_multiplier = [double]$InputProfile.easy_reverse_count_multiplier }
    if ($props -contains "easy_shield_drone_count_multiplier") { $p.easy_shield_drone_count_multiplier = [double]$InputProfile.easy_shield_drone_count_multiplier }
    if ($props -contains "normal_tanky_count_multiplier") { $p.normal_tanky_count_multiplier = [double]$InputProfile.normal_tanky_count_multiplier }
    if ($props -contains "normal_swift_count_multiplier") { $p.normal_swift_count_multiplier = [double]$InputProfile.normal_swift_count_multiplier }
    if ($props -contains "normal_splitter_count_multiplier") { $p.normal_splitter_count_multiplier = [double]$InputProfile.normal_splitter_count_multiplier }
    if ($props -contains "normal_reverse_count_multiplier") { $p.normal_reverse_count_multiplier = [double]$InputProfile.normal_reverse_count_multiplier }
    if ($props -contains "normal_shield_drone_count_multiplier") { $p.normal_shield_drone_count_multiplier = [double]$InputProfile.normal_shield_drone_count_multiplier }
    if ($props -contains "hard_tanky_count_multiplier") { $p.hard_tanky_count_multiplier = [double]$InputProfile.hard_tanky_count_multiplier }
    if ($props -contains "hard_swift_count_multiplier") { $p.hard_swift_count_multiplier = [double]$InputProfile.hard_swift_count_multiplier }
    if ($props -contains "hard_splitter_count_multiplier") { $p.hard_splitter_count_multiplier = [double]$InputProfile.hard_splitter_count_multiplier }
    if ($props -contains "hard_reverse_count_multiplier") { $p.hard_reverse_count_multiplier = [double]$InputProfile.hard_reverse_count_multiplier }
    if ($props -contains "hard_shield_drone_count_multiplier") { $p.hard_shield_drone_count_multiplier = [double]$InputProfile.hard_shield_drone_count_multiplier }

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
    if ($props -contains "tower_meter_gain_multipliers" -and $InputProfile.tower_meter_gain_multipliers -ne $null) {
        $tmgm = $InputProfile.tower_meter_gain_multipliers.PSObject.Properties.Name
        foreach ($name in $tmgm) {
            $p.tower_meter_gain_multipliers.$name = [double]$InputProfile.tower_meter_gain_multipliers.$name
        }
    }
    if ($props -contains "tower_surge_threshold_multipliers" -and $InputProfile.tower_surge_threshold_multipliers -ne $null) {
        $tstm = $InputProfile.tower_surge_threshold_multipliers.PSObject.Properties.Name
        foreach ($name in $tstm) {
            $p.tower_surge_threshold_multipliers.$name = [double]$InputProfile.tower_surge_threshold_multipliers.$name
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
    $p.easy_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_enemy_hp_multiplier -Min 0.3 -Max 2.0), 4)
    $p.easy_enemy_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_enemy_count_multiplier -Min 0.3 -Max 2.0), 4)
    $p.easy_spawn_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_spawn_interval_multiplier -Min 0.5 -Max 2.0), 4)
    $p.easy_hp_growth_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_hp_growth_multiplier -Min 0.5 -Max 1.5), 4)
    $p.normal_hp_growth_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_hp_growth_multiplier -Min 0.5 -Max 1.5), 4)
    $p.hard_hp_growth_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_hp_growth_multiplier -Min 0.5 -Max 1.5), 4)
    # Removed min=1.0 floor - optimizer needs to be able to make Normal/Hard easier than base waves.json
    # (prior floor prevented convergence toward 70%/40% win targets when base waves were already too hard).
    $p.normal_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_enemy_hp_multiplier -Min 0.5 -Max 5.0), 4)
    $p.normal_enemy_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_enemy_count_multiplier -Min 0.5 -Max 5.0), 4)
    $p.normal_spawn_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_spawn_interval_multiplier -Min 0.3 -Max 2.0), 4)
    $p.hard_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_enemy_hp_multiplier -Min 0.6 -Max 5.0), 4)
    $p.hard_enemy_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_enemy_count_multiplier -Min 0.6 -Max 5.0), 4)
    $p.hard_spawn_interval_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_spawn_interval_multiplier -Min 0.3 -Max 2.0), 4)
    $p.easy_tanky_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_tanky_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.easy_swift_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_swift_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.easy_splitter_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_splitter_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.easy_reverse_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_reverse_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.easy_shield_drone_count_multiplier = [Math]::Round((Clamp-Double -Value $p.easy_shield_drone_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.normal_tanky_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_tanky_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.normal_swift_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_swift_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.normal_splitter_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_splitter_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.normal_reverse_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_reverse_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.normal_shield_drone_count_multiplier = [Math]::Round((Clamp-Double -Value $p.normal_shield_drone_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.hard_tanky_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_tanky_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.hard_swift_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_swift_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.hard_splitter_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_splitter_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.hard_reverse_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_reverse_count_multiplier -Min 0.1 -Max 5.0), 4)
    $p.hard_shield_drone_count_multiplier = [Math]::Round((Clamp-Double -Value $p.hard_shield_drone_count_multiplier -Min 0.1 -Max 5.0), 4)
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
    foreach ($name in $p.tower_meter_gain_multipliers.PSObject.Properties.Name) {
        $p.tower_meter_gain_multipliers.$name = [Math]::Round((Clamp-Double -Value $p.tower_meter_gain_multipliers.$name -Min 0.3 -Max 2.5), 4)
    }
    foreach ($name in $p.tower_surge_threshold_multipliers.PSObject.Properties.Name) {
        $p.tower_surge_threshold_multipliers.$name = [Math]::Round((Clamp-Double -Value $p.tower_surge_threshold_multipliers.$name -Min 0.6 -Max 1.8), 4)
    }

    # Guardrail: keep core spectacle systems enabled.
    $p.enable_overkill_bloom = $true
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

function Get-DifficultyWinRateFromSummary {
    param(
        [object]$Summary,
        [string]$Difficulty
    )

    if ($null -eq $Summary) { return $null }
    if (-not $Summary.PSObject.Properties["difficulty_win_rates"]) { return $null }

    $rows = @($Summary.difficulty_win_rates)
    if ($rows.Count -eq 0) { return $null }
    $match = $rows | Where-Object { [string]$_.difficulty -ieq $Difficulty } | Select-Object -First 1
    if ($null -eq $match) { return $null }

    if ($match.PSObject.Properties["win_rate"] -and $null -ne $match.win_rate) {
        return [double]$match.win_rate
    }

    $runs = [double](Get-NumberValue -Obj $match -Property "runs")
    if ($runs -le 0.000001) { return $null }
    $wins = [double](Get-NumberValue -Obj $match -Property "wins")
    return $wins / $runs
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

function Get-RunItemWinRateGap {
    param(
        [object[]]$Runs,
        [string]$ItemProperty,
        [int]$MinRuns
    )

    if ($null -eq $Runs -or $Runs.Count -eq 0) {
        return [PSCustomObject]@{
            gap = 0.0
            eligible_count = 0
        }
    }

    $stats = @{}
    foreach ($run in $Runs) {
        if ($null -eq $run) { continue }
        $prop = $run.PSObject.Properties[$ItemProperty]
        if ($null -eq $prop -or $null -eq $prop.Value) { continue }

        $seen = New-Object System.Collections.Generic.HashSet[string]
        foreach ($raw in @($prop.Value)) {
            $item = [string]$raw
            if ([string]::IsNullOrWhiteSpace($item)) { continue }
            if (-not $seen.Add($item)) { continue }

            if (-not $stats.ContainsKey($item)) {
                $stats[$item] = [PSCustomObject]@{
                    runs = 0
                    wins = 0
                }
            }

            $stats[$item].runs += 1
            if ($run.won -eq $true) {
                $stats[$item].wins += 1
            }
        }
    }

    $rates = @()
    foreach ($item in $stats.Keys) {
        $entry = $stats[$item]
        if ($entry.runs -lt $MinRuns) { continue }
        $rates += [double]$entry.wins / [double]$entry.runs
    }

    if ($rates.Count -lt 2) {
        return [PSCustomObject]@{
            gap = 0.0
            eligible_count = $rates.Count
        }
    }

    $minRate = ($rates | Measure-Object -Minimum).Minimum
    $maxRate = ($rates | Measure-Object -Maximum).Maximum

    return [PSCustomObject]@{
        gap = [double]$maxRate - [double]$minRate
        eligible_count = $rates.Count
    }
}

function Get-ItemWinRateGapFromSummary {
    param(
        [object]$Summary,
        [string]$SummaryProperty,
        [int]$MinRuns
    )

    if ($null -eq $Summary -or -not $Summary.PSObject.Properties[$SummaryProperty]) {
        return $null
    }

    $rows = @($Summary.$SummaryProperty)
    if ($rows.Count -lt 1) { return $null }

    $rates = @()
    foreach ($row in $rows) {
        if ($null -eq $row) { continue }
        $runs = [int](Get-NumberValue -Obj $row -Property "runs")
        if ($runs -lt $MinRuns) { continue }
        $rate = Get-NumberValue -Obj $row -Property "win_rate"
        if ($rate -le 0 -and $runs -gt 0) {
            $wins = Get-NumberValue -Obj $row -Property "wins"
            $rate = $wins / [double]$runs
        }
        $rates += [double]$rate
    }

    if ($rates.Count -lt 2) {
        return [PSCustomObject]@{
            gap = 0.0
            eligible_count = $rates.Count
        }
    }

    $minRate = ($rates | Measure-Object -Minimum).Minimum
    $maxRate = ($rates | Measure-Object -Maximum).Maximum
    return [PSCustomObject]@{
        gap = [double]$maxRate - [double]$minRate
        eligible_count = $rates.Count
    }
}

function Get-ModifierSurgeParityFromSummary {
    param(
        [object]$Summary,
        [int]$MinRuns
    )

    if ($null -eq $Summary -or -not $Summary.PSObject.Properties["surges_by_modifier"]) {
        return [PSCustomObject]@{
            ratio = 1.0
            eligible_count = 0
        }
    }

    $rows = @($Summary.surges_by_modifier)
    if ($rows.Count -eq 0) {
        return [PSCustomObject]@{
            ratio = 1.0
            eligible_count = 0
        }
    }

    $rates = @()
    foreach ($row in $rows) {
        $runs = [int](Get-NumberValue -Obj $row -Property "runs")
        if ($runs -lt $MinRuns) { continue }
        $rate = Get-NumberValue -Obj $row -Property "surges_per_run"
        if ($rate -le 0.000001) {
            $surges = Get-NumberValue -Obj $row -Property "surges"
            if ($runs -gt 0) { $rate = $surges / [double]$runs }
        }
        if ($rate -le 0.000001) { continue }
        $rates += [double]$rate
    }

    if ($rates.Count -lt 2) {
        return [PSCustomObject]@{
            ratio = 1.0
            eligible_count = $rates.Count
        }
    }

    $minRate = ($rates | Measure-Object -Minimum).Minimum
    $maxRate = ($rates | Measure-Object -Maximum).Maximum
    $ratio = if ($minRate -gt 0.000001) { [double]$maxRate / [double]$minRate } else { 9999.0 }
    return [PSCustomObject]@{
        ratio = $ratio
        eligible_count = $rates.Count
    }
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
        [double]$TargetSurgesPerRun,
        [double]$TargetSurgesPerRunTolerance,
        [double]$MaxKillsPerSurge,
        [double]$MinGlobalSurgesPerRun,
        [double]$TargetMaxTowerSurgeRatio,
        [double]$TargetMaxModifierSurgeRatio = 2.2,
        [double]$TargetTowerWinRateGap,
        [double]$TargetModifierWinRateGap,
        [double]$HardGuardMaxTowerSurgeRatio = 9999.0,
        [double]$HardGuardMaxTowerWinRateGap = 1.0,
        [double]$MinNormalWinRate = -1.0,
        [double]$MinHardWinRate = -1.0,
        [double]$NormalRegressionPenaltyWeight = 0.0,
        [double]$HardRegressionPenaltyWeight = 0.0,
        [int]$MinTowerPlacementsForParity,
        [int]$MinTowerRunsForFairness,
        [int]$MinModifierRunsForFairness,
        [int]$MinModifierRunsForSurgeParity = 40,
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
    $globalSurgesPerRun = Get-NumberValue -Obj $Summary -Property "avg_global_surges_per_run"
    if ($globalSurgesPerRun -le 0.000001 -and $null -ne $Runs -and $Runs.Count -gt 0) {
        $globalSurgesPerRun = (@($Runs | Measure-Object -Property global_surges -Average).Average)
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
    $towerRechargeRates = @()
    foreach ($row in $surgesByTowerRows) {
        $placements = [int](Get-NumberValue -Obj $row -Property "placements")
        if ($placements -lt $MinTowerPlacementsForParity) { continue }
        $samples = [int](Get-NumberValue -Obj $row -Property "recharge_samples")
        if ($samples -lt 1) { continue }
        $avgRecharge = Get-NumberValue -Obj $row -Property "avg_recharge_seconds"
        if ($avgRecharge -le 0.000001) { continue }
        $towerRechargeRates += [double]$avgRecharge
    }
    $towerRechargeRatio = 1.0
    if ($towerRechargeRates.Count -ge 2) {
        $towerRechargeMin = ($towerRechargeRates | Measure-Object -Minimum).Minimum
        $towerRechargeMax = ($towerRechargeRates | Measure-Object -Maximum).Maximum
        if ($towerRechargeMin -gt 0.000001) {
            $towerRechargeRatio = [double]$towerRechargeMax / [double]$towerRechargeMin
        } else {
            $towerRechargeRatio = 9999.0
        }
    }

    $modifierSurgeParityInfo = Get-ModifierSurgeParityFromSummary -Summary $Summary -MinRuns $MinModifierRunsForSurgeParity
    $modifierSurgeRatio = [double]$modifierSurgeParityInfo.ratio
    $modifierSurgeParityEligible = [int]$modifierSurgeParityInfo.eligible_count

    $frameStress = $Summary.frame_stress_peaks
    $peakExplosions = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_explosions")
    $peakHazards = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_active_hazards")
    $peakHitStops = [int](Get-NumberValue -Obj $frameStress -Property "simultaneous_hitstops_requested")

    $easyWinRateFromSummary = Get-DifficultyWinRateFromSummary -Summary $Summary -Difficulty "Easy"
    $normalWinRateFromSummary = Get-DifficultyWinRateFromSummary -Summary $Summary -Difficulty "Normal"
    $hardWinRateFromSummary = Get-DifficultyWinRateFromSummary -Summary $Summary -Difficulty "Hard"
    $easyWinRate = if ($null -ne $easyWinRateFromSummary) { [double]$easyWinRateFromSummary } else { Get-DifficultyWinRate -Runs $Runs -Difficulty "Easy" }
    $normalWinRate = if ($null -ne $normalWinRateFromSummary) { [double]$normalWinRateFromSummary } else { Get-DifficultyWinRate -Runs $Runs -Difficulty "Normal" }
    $hardWinRate = if ($null -ne $hardWinRateFromSummary) { [double]$hardWinRateFromSummary } else { Get-DifficultyWinRate -Runs $Runs -Difficulty "Hard" }
    $winTol = [Math]::Max(0.0001, $WinRateTolerance)
    $surgeTol = [Math]::Max(0.0001, $TargetSurgesPerRunTolerance)

    $towerWinRateGapInfo = Get-ItemWinRateGapFromSummary -Summary $Summary -SummaryProperty "tower_win_rates" -MinRuns $MinTowerRunsForFairness
    if ($null -eq $towerWinRateGapInfo) {
        $towerWinRateGapInfo = Get-RunItemWinRateGap -Runs $Runs -ItemProperty "towers" -MinRuns $MinTowerRunsForFairness
    }
    $towerWinRateGap = [double]$towerWinRateGapInfo.gap
    $towerWinRateEligible = [int]$towerWinRateGapInfo.eligible_count
    $modifierWinRateGapInfo = Get-ItemWinRateGapFromSummary -Summary $Summary -SummaryProperty "modifier_win_rates" -MinRuns $MinModifierRunsForFairness
    if ($null -eq $modifierWinRateGapInfo) {
        $modifierWinRateGapInfo = Get-RunItemWinRateGap -Runs $Runs -ItemProperty "mods" -MinRuns $MinModifierRunsForFairness
    }
    $modifierWinRateGap = [double]$modifierWinRateGapInfo.gap
    $modifierWinRateEligible = [int]$modifierWinRateGapInfo.eligible_count

    $hardRejected = $false
    $hardRejectReason = ""
    if ($towerSurgeRates.Count -ge 2 -and $towerSurgeRatio -gt $HardGuardMaxTowerSurgeRatio) {
        $hardRejected = $true
        $hardRejectReason = "tower_surge_ratio_exceeded"
    }
    if (-not $hardRejected -and $towerWinRateEligible -ge 2 -and $towerWinRateGap -gt $HardGuardMaxTowerWinRateGap) {
        $hardRejected = $true
        $hardRejectReason = "tower_win_rate_gap_exceeded"
    }

    $easyError = [Math]::Abs($easyWinRate - $TargetEasyWinRate)
    $normalError = [Math]::Abs($normalWinRate - $TargetNormalWinRate)
    $hardError = [Math]::Abs($hardWinRate - $TargetHardWinRate)
    $surgeCadenceError = [Math]::Abs($surgesPerRun - $TargetSurgesPerRun)

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

    # Surge cadence objective: avoid over-frequent tower surges.
    $surgeCadenceFit = [Math]::Max(0.0, 1.0 - ($surgeCadenceError / $surgeTol))
    $score += $surgeCadenceFit * 24.0
    if ($surgesPerRun -gt ($TargetSurgesPerRun + $surgeTol)) {
        $score -= ($surgesPerRun - ($TargetSurgesPerRun + $surgeTol)) * 7.0
    }

    # Keep tower surges from becoming too individually powerful.
    if ($killsPerSurge -gt $MaxKillsPerSurge) {
        $score -= ($killsPerSurge - $MaxKillsPerSurge) * 80.0
    }

    # Reward meaningful global-surge participation.
    if ($globalSurgesPerRun -ge $MinGlobalSurgesPerRun) {
        $score += [Math]::Min(16.0, ($globalSurgesPerRun / [Math]::Max(0.01, $MinGlobalSurgesPerRun)) * 10.0)
    } else {
        $score -= ($MinGlobalSurgesPerRun - $globalSurgesPerRun) * 26.0
    }

    # Secondary quality signals.
    $score += $winRate * 15.0
    $score += $avgWave * 1.0
    $score += $killsPerSurge * 6.0


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
    if ($towerRechargeRates.Count -ge 2) {
        if ($towerRechargeRatio -le $TargetMaxTowerSurgeRatio) {
            $rechargeRatioDenom = [Math]::Max(0.0001, $TargetMaxTowerSurgeRatio - 1.0)
            $rechargeRatioFit = [Math]::Max(0.0, 1.0 - (($towerRechargeRatio - 1.0) / $rechargeRatioDenom))
            $score += $rechargeRatioFit * 10.0
        } else {
            $score -= ($towerRechargeRatio - $TargetMaxTowerSurgeRatio) * 16.0
        }
    }
    if ($modifierSurgeParityEligible -ge 2) {
        if ($modifierSurgeRatio -le $TargetMaxModifierSurgeRatio) {
            $modRatioDenom = [Math]::Max(0.0001, $TargetMaxModifierSurgeRatio - 1.0)
            $modRatioFit = [Math]::Max(0.0, 1.0 - (($modifierSurgeRatio - 1.0) / $modRatioDenom))
            $score += $modRatioFit * 20.0
        } else {
            $score -= ($modifierSurgeRatio - $TargetMaxModifierSurgeRatio) * 34.0
        }
    }

    # Fairness objective: avoid towers/mods with extreme win-rate spread.
    if ($towerWinRateEligible -ge 2) {
        if ($towerWinRateGap -le $TargetTowerWinRateGap) {
            $towerGapDenom = [Math]::Max(0.0001, $TargetTowerWinRateGap)
            $towerGapFit = [Math]::Max(0.0, 1.0 - ($towerWinRateGap / $towerGapDenom))
            $score += $towerGapFit * 18.0
        } else {
            $score -= ($towerWinRateGap - $TargetTowerWinRateGap) * 85.0
        }
    }

    if ($modifierWinRateEligible -ge 2) {
        if ($modifierWinRateGap -le $TargetModifierWinRateGap) {
            $modGapDenom = [Math]::Max(0.0001, $TargetModifierWinRateGap)
            $modGapFit = [Math]::Max(0.0, 1.0 - ($modifierWinRateGap / $modGapDenom))
            $score += $modGapFit * 16.0
        } else {
            $score -= ($modifierWinRateGap - $TargetModifierWinRateGap) * 70.0
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

    if ($hardRejected) {
        $score = -1000000.0 - ($towerSurgeRatio * 100.0) - ($towerWinRateGap * 1000.0)
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
        GlobalSurgesPerRun = [Math]::Round($globalSurgesPerRun, 6)
        KillsPerSurge = [Math]::Round($killsPerSurge, 6)
        ExplosionShare = [Math]::Round($explosionShare, 6)
        TowerSurgeParityRatio = [Math]::Round($towerSurgeRatio, 6)
        TowerSurgeRateMin = [Math]::Round($towerSurgeRateMin, 6)
        TowerSurgeRateMax = [Math]::Round($towerSurgeRateMax, 6)
        TowerSurgeParityEligibleTowers = $towerSurgeRates.Count
        TowerRechargeParityRatio = [Math]::Round($towerRechargeRatio, 6)
        TowerRechargeParityEligibleTowers = $towerRechargeRates.Count
        ModifierSurgeParityRatio = [Math]::Round($modifierSurgeRatio, 6)
        ModifierSurgeParityEligible = $modifierSurgeParityEligible
        TowerWinRateGap = [Math]::Round($towerWinRateGap, 6)
        TowerWinRateEligible = $towerWinRateEligible
        ModifierWinRateGap = [Math]::Round($modifierWinRateGap, 6)
        ModifierWinRateEligible = $modifierWinRateEligible
        AvgMaxChainDepth = [Math]::Round($chainDepth, 6)
        PeakExplosions = $peakExplosions
        PeakHazards = $peakHazards
        PeakHitStops = $peakHitStops
        HardRejected = $hardRejected
        HardRejectReason = $hardRejectReason
    }
}

# Merge two surges_by_tower arrays by summing placements/surges per tower_id, then recompute
# surges_per_placed_tower. Used when combining eval + reeval payloads for scoring.
function Merge-SurgesByTower {
    param(
        [object[]]$Rows1,
        [object[]]$Rows2
    )

    $map = @{}
    foreach ($row in @($Rows1) + @($Rows2)) {
        if ($null -eq $row) { continue }
        $id = [string]$row.tower_id
        if (-not $map.ContainsKey($id)) {
            $map[$id] = [PSCustomObject]@{
                tower_id = $id
                placements = 0.0
                surges = 0.0
                first_surge_total_seconds = 0.0
                first_surge_samples = 0.0
                recharge_total_seconds = 0.0
                recharge_samples = 0.0
            }
        }
        $map[$id].placements += [double](Get-NumberValue -Obj $row -Property "placements")
        $map[$id].surges     += [double](Get-NumberValue -Obj $row -Property "surges")
        $firstSamples = [double](Get-NumberValue -Obj $row -Property "first_surge_samples")
        $firstAvg = [double](Get-NumberValue -Obj $row -Property "avg_time_to_first_surge_seconds")
        if ($firstSamples -gt 0.000001) {
            $map[$id].first_surge_samples += $firstSamples
            $map[$id].first_surge_total_seconds += ($firstAvg * $firstSamples)
        }

        $rechargeSamples = [double](Get-NumberValue -Obj $row -Property "recharge_samples")
        $rechargeAvg = [double](Get-NumberValue -Obj $row -Property "avg_recharge_seconds")
        if ($rechargeSamples -gt 0.000001) {
            $map[$id].recharge_samples += $rechargeSamples
            $map[$id].recharge_total_seconds += ($rechargeAvg * $rechargeSamples)
        }
    }

    $result = @()
    foreach ($key in $map.Keys) {
        $e = $map[$key]
        $rate = if ($e.placements -gt 0.000001) { $e.surges / $e.placements } else { 0.0 }
        $avgFirst = if ($e.first_surge_samples -gt 0.000001) { $e.first_surge_total_seconds / $e.first_surge_samples } else { 0.0 }
        $avgRecharge = if ($e.recharge_samples -gt 0.000001) { $e.recharge_total_seconds / $e.recharge_samples } else { 0.0 }
        $result += [PSCustomObject]@{
            tower_id                 = $e.tower_id
            placements               = [int]$e.placements
            surges                   = [int]$e.surges
            surges_per_placed_tower  = [Math]::Round($rate, 6)
            avg_time_to_first_surge_seconds = [Math]::Round($avgFirst, 6)
            first_surge_samples = [int]$e.first_surge_samples
            avg_recharge_seconds = [Math]::Round($avgRecharge, 6)
            recharge_samples = [int]$e.recharge_samples
        }
    }
    return $result
}

function Merge-SurgesByModifier {
    param(
        [object[]]$Rows1,
        [object[]]$Rows2
    )

    $map = @{}
    foreach ($row in @($Rows1) + @($Rows2)) {
        if ($null -eq $row) { continue }
        $id = [string]$row.modifier_id
        if ([string]::IsNullOrWhiteSpace($id)) { continue }
        if (-not $map.ContainsKey($id)) {
            $map[$id] = [PSCustomObject]@{ modifier_id = $id; runs = 0.0; surges = 0.0 }
        }
        $map[$id].runs += [double](Get-NumberValue -Obj $row -Property "runs")
        $map[$id].surges += [double](Get-NumberValue -Obj $row -Property "surges")
    }

    $result = @()
    foreach ($key in $map.Keys) {
        $e = $map[$key]
        $rate = if ($e.runs -gt 0.000001) { $e.surges / $e.runs } else { 0.0 }
        $result += [PSCustomObject]@{
            modifier_id = $e.modifier_id
            runs = [int]$e.runs
            surges = [int]$e.surges
            surges_per_run = [Math]::Round($rate, 6)
        }
    }
    return $result
}

# Combine two metrics payloads (eval + reeval) into a single merged payload so the scorer
# sees all runs at once rather than averaging two independent scores. This eliminates the
# artificial score variance caused by treating two 200-run samples as independent rankings.
function Merge-MetricsPayloads {
    param(
        [Parameter(Mandatory = $true)][object]$Payload1,
        [Parameter(Mandatory = $true)][object]$Payload2
    )

    $n1 = [double](Get-NumberValue -Obj $Payload1 -Property "run_count")
    $n2 = [double](Get-NumberValue -Obj $Payload2 -Property "run_count")
    if ($n1 -lt 0.5) { $n1 = if ($null -ne $Payload1.runs) { @($Payload1.runs).Count } else { 0.0 } }
    if ($n2 -lt 0.5) { $n2 = if ($null -ne $Payload2.runs) { @($Payload2.runs).Count } else { 0.0 } }
    $nTotal = $n1 + $n2
    if ($nTotal -lt 0.5) { return $Payload1 }
    $w1 = $n1 / $nTotal
    $w2 = $n2 / $nTotal

    $mergedRuns = @()
    if ($null -ne $Payload1.runs) { $mergedRuns += @($Payload1.runs) }
    if ($null -ne $Payload2.runs) { $mergedRuns += @($Payload2.runs) }

    $s1 = $Payload1.summary
    $s2 = $Payload2.summary

    function WAvg { param($a, $b) return $a * $w1 + $b * $w2 }
    function WAvgField { param($obj1, $obj2, [string]$name)
        return (Get-NumberValue -Obj $obj1 -Property $name) * $w1 + (Get-NumberValue -Obj $obj2 -Property $name) * $w2
    }

    $wins = @($mergedRuns | Where-Object { $_.won -eq $true }).Count
    $mergedWinRate = if ($mergedRuns.Count -gt 0) { [double]$wins / [double]$mergedRuns.Count } else { 0.0 }

    $d1 = $s1.dps_split
    $d2 = $s2.dps_split
    $mergedDps = [PSCustomObject]@{
        base_attacks       = WAvgField $d1 $d2 "base_attacks"
        surge_core         = WAvgField $d1 $d2 "surge_core"
        explosion_follow_ups = WAvgField $d1 $d2 "explosion_follow_ups"
        residue            = WAvgField $d1 $d2 "residue"
    }

    $fs1 = $s1.frame_stress_peaks
    $fs2 = $s2.frame_stress_peaks
    $mergedFrameStress = [PSCustomObject]@{
        simultaneous_explosions        = [Math]::Max([int](Get-NumberValue -Obj $fs1 -Property "simultaneous_explosions"), [int](Get-NumberValue -Obj $fs2 -Property "simultaneous_explosions"))
        simultaneous_active_hazards    = [Math]::Max([int](Get-NumberValue -Obj $fs1 -Property "simultaneous_active_hazards"), [int](Get-NumberValue -Obj $fs2 -Property "simultaneous_active_hazards"))
        simultaneous_hitstops_requested = [Math]::Max([int](Get-NumberValue -Obj $fs1 -Property "simultaneous_hitstops_requested"), [int](Get-NumberValue -Obj $fs2 -Property "simultaneous_hitstops_requested"))
    }

    $rows1 = if ($null -ne $s1 -and $s1.PSObject.Properties["surges_by_tower"]) { @($s1.surges_by_tower) } else { @() }
    $rows2 = if ($null -ne $s2 -and $s2.PSObject.Properties["surges_by_tower"]) { @($s2.surges_by_tower) } else { @() }
    $mergedSurgesByTower = Merge-SurgesByTower -Rows1 $rows1 -Rows2 $rows2
    $modRows1 = if ($null -ne $s1 -and $s1.PSObject.Properties["surges_by_modifier"]) { @($s1.surges_by_modifier) } else { @() }
    $modRows2 = if ($null -ne $s2 -and $s2.PSObject.Properties["surges_by_modifier"]) { @($s2.surges_by_modifier) } else { @() }
    $mergedSurgesByModifier = Merge-SurgesByModifier -Rows1 $modRows1 -Rows2 $modRows2

    $mergedSummary = [PSCustomObject]@{
        win_rate                  = [Math]::Round($mergedWinRate, 6)
        avg_wave_reached          = WAvgField $s1 $s2 "avg_wave_reached"
        avg_run_duration_seconds  = WAvgField $s1 $s2 "avg_run_duration_seconds"
        avg_surges_per_run        = WAvgField $s1 $s2 "avg_surges_per_run"
        avg_global_surges_per_run = WAvgField $s1 $s2 "avg_global_surges_per_run"
        avg_kills_per_surge       = WAvgField $s1 $s2 "avg_kills_per_surge"
        avg_max_chain_depth       = WAvgField $s1 $s2 "avg_max_chain_depth"
        dps_split                 = $mergedDps
        frame_stress_peaks        = $mergedFrameStress
        surges_by_tower           = $mergedSurgesByTower
        surges_by_modifier        = $mergedSurgesByModifier
    }

    return [PSCustomObject]@{
        run_count = [int]$nTotal
        summary   = $mergedSummary
        runs      = $mergedRuns
    }
}

function New-MutatedTuningProfile {
    param(
        [Parameter(Mandatory = $true)][object]$BaseProfile,
        [Parameter(Mandatory = $true)][double]$Anneal,
        [Parameter(Mandatory = $true)][double]$Strength,
        # When set, skip all spectacle param mutations (overkill bloom, residue, detonation).
        # Use when the strategy set produces no explosion/residue signal so those params are noise.
        [switch]$FreezeSpectacleParams,
        # When set, skip difficulty param mutations and only mutate spectacle params.
        # Pair with -StrategySet spectacle so the scorer actually sees explosion/residue signal.
        [switch]$SpectacleOnlyMode,
        # When set, skip hard_* difficulty param mutations (hard_enemy_*, hard_tanky_count,
        # hard_swift_count). Normal difficulty params are still mutated freely. Use when the goal
        # is Normal win-rate improvement without disturbing the Hard curve.
        [switch]$FreezeHardParams
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

    # Difficulty multipliers: primary win-rate levers. Skipped in SpectacleOnlyMode since
    # difficulty curve is already set and we don't want those params drifting during spectacle tuning.
    if (-not $SpectacleOnlyMode) {
        $changed += Apply-Mutation -Obj $candidate -Name "easy_enemy_hp_multiplier" -Step (0.08 * $scale) -Min 0.3 -Max 2.0 -Chance 0.70
        $changed += Apply-Mutation -Obj $candidate -Name "easy_enemy_count_multiplier" -Step (0.08 * $scale) -Min 0.3 -Max 2.0 -Chance 0.70
        $changed += Apply-Mutation -Obj $candidate -Name "easy_spawn_interval_multiplier" -Step (0.06 * $scale) -Min 0.5 -Max 2.0 -Chance 0.70
        $changed += Apply-Mutation -Obj $candidate -Name "easy_hp_growth_multiplier" -Step (0.05 * $scale) -Min 0.5 -Max 1.5 -Chance 0.65
        $changed += Apply-Mutation -Obj $candidate -Name "normal_hp_growth_multiplier" -Step (0.05 * $scale) -Min 0.5 -Max 1.5 -Chance 0.80
        $changed += Apply-Mutation -Obj $candidate -Name "easy_tanky_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "easy_swift_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "easy_splitter_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "easy_reverse_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
        $changed += Apply-Mutation -Obj $candidate -Name "easy_shield_drone_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
        $changed += Apply-Mutation -Obj $candidate -Name "normal_enemy_hp_multiplier" -Step (0.10 * $scale) -Min 0.5 -Max 5.0 -Chance 0.80
        $changed += Apply-Mutation -Obj $candidate -Name "normal_enemy_count_multiplier" -Step (0.08 * $scale) -Min 0.5 -Max 5.0 -Chance 0.80
        $changed += Apply-Mutation -Obj $candidate -Name "normal_spawn_interval_multiplier" -Step (0.06 * $scale) -Min 0.3 -Max 2.0 -Chance 0.80
        if (-not $FreezeHardParams) {
            $changed += Apply-Mutation -Obj $candidate -Name "hard_hp_growth_multiplier" -Step (0.05 * $scale) -Min 0.5 -Max 1.5 -Chance 0.80
            $changed += Apply-Mutation -Obj $candidate -Name "hard_enemy_hp_multiplier" -Step (0.10 * $scale) -Min 0.6 -Max 5.0 -Chance 0.80
            $changed += Apply-Mutation -Obj $candidate -Name "hard_enemy_count_multiplier" -Step (0.08 * $scale) -Min 0.6 -Max 5.0 -Chance 0.80
            $changed += Apply-Mutation -Obj $candidate -Name "hard_spawn_interval_multiplier" -Step (0.06 * $scale) -Min 0.3 -Max 2.0 -Chance 0.80
        }
        $changed += Apply-Mutation -Obj $candidate -Name "normal_tanky_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "normal_swift_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "normal_splitter_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
        $changed += Apply-Mutation -Obj $candidate -Name "normal_reverse_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
        $changed += Apply-Mutation -Obj $candidate -Name "normal_shield_drone_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
        if (-not $FreezeHardParams) {
            $changed += Apply-Mutation -Obj $candidate -Name "hard_tanky_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
            $changed += Apply-Mutation -Obj $candidate -Name "hard_swift_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
            $changed += Apply-Mutation -Obj $candidate -Name "hard_splitter_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.50
            $changed += Apply-Mutation -Obj $candidate -Name "hard_reverse_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
            $changed += Apply-Mutation -Obj $candidate -Name "hard_shield_drone_count_multiplier" -Step (0.08 * $scale) -Min 0.1 -Max 5.0 -Chance 0.35
        }
    }

    $towerIds = @("rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower", "rift_prism")
    $mutateTowerSurgeLevers = $SpectacleOnlyMode -or (-not $FreezeSpectacleParams)
    if ($mutateTowerSurgeLevers) {
        foreach ($towerId in $towerIds) {
            $changed += Apply-Mutation -Obj $candidate.tower_meter_gain_multipliers -Name $towerId -Step (0.12 * $scale) -Min 0.3 -Max 2.5 -Chance 0.55
            $changed += Apply-Mutation -Obj $candidate.tower_surge_threshold_multipliers -Name $towerId -Step (0.08 * $scale) -Min 0.6 -Max 1.8 -Chance 0.55
        }
    }

    # Explosion share params: overkill bloom trigger, damage, spread, and residue DPS.
    # Skipped when FreezeSpectacleParams is set (unless SpectacleOnlyMode overrides it).
    # In SpectacleOnlyMode, these are the primary levers so mutation chance is higher.
    $mutateSpectacle = $SpectacleOnlyMode -or (-not $FreezeSpectacleParams)
    if ($mutateSpectacle) {
        $spectacleChanceBoost = if ($SpectacleOnlyMode) { 0.25 } else { 0.0 }
        $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_threshold_multiplier"     -Step (0.18 * $scale) -Min 0.1 -Max 4.0 -Chance (0.55 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_damage_scale_multiplier"  -Step (0.18 * $scale) -Min 0.0 -Max 4.0 -Chance (0.55 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "explosion_followup_damage_multiplier"    -Step (0.18 * $scale) -Min 0.0 -Max 6.0 -Chance (0.55 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_radius_multiplier"        -Step (0.12 * $scale) -Min 0.1 -Max 4.0 -Chance (0.40 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "overkill_bloom_max_targets_multiplier"   -Step (0.12 * $scale) -Min 0.1 -Max 3.0 -Chance (0.40 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "residue_damage_multiplier"               -Step (0.18 * $scale) -Min 0.0 -Max 6.0 -Chance (0.45 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "residue_potency_multiplier"              -Step (0.12 * $scale) -Min 0.1 -Max 4.0 -Chance (0.40 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "residue_duration_multiplier"             -Step (0.12 * $scale) -Min 0.1 -Max 4.0 -Chance (0.40 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "detonation_max_targets_multiplier"       -Step (0.12 * $scale) -Min 0.1 -Max 4.0 -Chance (0.40 + $spectacleChanceBoost)
        $changed += Apply-Mutation -Obj $candidate -Name "status_detonation_damage_multiplier"     -Step (0.15 * $scale) -Min 0.0 -Max 6.0 -Chance (0.45 + $spectacleChanceBoost)
    }

    if ($changed -eq 0) {
        $forceDelta = (($script:Rng.NextDouble() * 2.0) - 1.0) * (0.10 * $scale)
        if ($SpectacleOnlyMode) {
            $candidate.overkill_bloom_damage_scale_multiplier = [Math]::Round((Clamp-Double -Value ([double]$candidate.overkill_bloom_damage_scale_multiplier + $forceDelta) -Min 0.0 -Max 4.0), 4)
        } else {
            $candidate.normal_enemy_hp_multiplier = [Math]::Round((Clamp-Double -Value ([double]$candidate.normal_enemy_hp_multiplier + $forceDelta) -Min 1.0 -Max 5.0), 4)
        }
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
    if ($script:UseFastBotMetrics) {
        $args += "--bot_fast_metrics"
    }
    if ($script:UseBotQuiet) {
        $args += "--bot_quiet"
    }
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

    # Strip only the very heavy per-run arrays (kill-depth and chain-size samples).
    # Keep compact scalar signals needed by scoring (including fairness on towers/mods).
    $allRuns = [System.Collections.Generic.List[object]]::new()
    foreach ($payload in $payloads) {
        if ($null -ne $payload -and $null -ne $payload.runs) {
            foreach ($run in $payload.runs) {
                $slim = [ordered]@{
                    strategy           = $run.strategy
                    map                = $run.map
                    difficulty         = $run.difficulty
                    won                = $run.won
                    wave_reached       = $run.wave_reached
                    run_duration_seconds = $run.run_duration_seconds
                    surges             = $run.surges
                    global_surges      = $run.global_surges
                    surge_interval_seconds = $run.surge_interval_seconds
                    status_detonations = $run.status_detonations
                    residue_uptime_seconds = $run.residue_uptime_seconds
                    max_chain_depth    = $run.max_chain_depth
                    top_tower_damage_share = $run.top_tower_damage_share
                    towers             = $run.towers
                    mods               = $run.mods
                    damage_split       = $run.damage_split
                    frame_stress       = $run.frame_stress
                }
                $allRuns.Add([PSCustomObject]$slim)
            }
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
    $globalSurgesSum = 0.0
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
        $globalSurgesSum += [int](Get-NumberValue -Obj $run -Property "global_surges")
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

    $modifierAggregate = @{}
    foreach ($payload in $payloads) {
        $rows = @()
        if ($null -ne $payload.summary -and $null -ne $payload.summary.surges_by_modifier) {
            $rows = @($payload.summary.surges_by_modifier)
        }

        foreach ($row in $rows) {
            $modifierId = [string]$row.modifier_id
            if ([string]::IsNullOrWhiteSpace($modifierId)) { continue }
            $runs = [int](Get-NumberValue -Obj $row -Property "runs")
            $surges = [int](Get-NumberValue -Obj $row -Property "surges")
            if (-not $modifierAggregate.ContainsKey($modifierId)) {
                $modifierAggregate[$modifierId] = [PSCustomObject]@{
                    runs = 0
                    surges = 0
                }
            }
            $modifierAggregate[$modifierId].runs = [int]$modifierAggregate[$modifierId].runs + $runs
            $modifierAggregate[$modifierId].surges = [int]$modifierAggregate[$modifierId].surges + $surges
        }
    }

    $surgesByModifierRows = @()
    foreach ($modifierId in ($modifierAggregate.Keys | Sort-Object)) {
        $runsForModifier = [int]$modifierAggregate[$modifierId].runs
        $surgesForModifier = [int]$modifierAggregate[$modifierId].surges
        $surgesPerRun = if ($runsForModifier -gt 0) { [double]$surgesForModifier / [double]$runsForModifier } else { 0.0 }
        $surgesByModifierRows += [PSCustomObject]@{
            modifier_id = $modifierId
            runs = $runsForModifier
            surges = $surgesForModifier
            surges_per_run = [Math]::Round($surgesPerRun, 6)
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
            avg_global_surges_per_run = [Math]::Round(($globalSurgesSum / [double]$runCount), 6)
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
            surges_by_modifier = $surgesByModifierRows
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

# Like Invoke-BotMetricsRunBatch but also shards each individual candidate eval when
# EvalShardCount > 1. All shards across all candidates are submitted as a single flat
# batch so the scheduler can fill available cores optimally. Shards are merged per-candidate
# after the batch completes. Falls back to plain RunBatch when EvalShardCount <= 1.
function Invoke-BotMetricsRunBatchSharded {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [Parameter(Mandatory = $true)][object[]]$CandidateSpecs,
        [Parameter(Mandatory = $true)][int]$CandidateParallelism,
        [Parameter(Mandatory = $true)][int]$EvalShardCount
    )

    if ($EvalShardCount -le 1) {
        Invoke-BotMetricsRunBatch `
            -GodotExe $GodotExe `
            -CommonPrefix $CommonPrefix `
            -RunsPerEval $RunsPerEval `
            -StrategySet $StrategySet `
            -CandidateSpecs $CandidateSpecs `
            -Parallelism $CandidateParallelism
        return
    }

    # Pre-expand: each candidate spec becomes EvalShardCount shard specs.
    $allShardSpecs = @()
    $mergeMap = [ordered]@{}
    foreach ($spec in $CandidateSpecs) {
        $candidateId  = [string]$spec.candidate_id
        $metricsPath  = [string]$spec.metrics_path
        $outDir       = Split-Path -Parent $metricsPath
        $baseName     = [System.IO.Path]::GetFileNameWithoutExtension($metricsPath)
        $ext          = [System.IO.Path]::GetExtension($metricsPath)
        if ([string]::IsNullOrWhiteSpace($ext)) { $ext = ".json" }
        if (-not [string]::IsNullOrWhiteSpace($outDir)) { Ensure-Directory -PathValue $outDir }

        $shardPlan  = Get-RunShardPlan -TotalRuns $RunsPerEval -ShardCount $EvalShardCount
        $shardPaths = @()
        foreach ($shard in $shardPlan) {
            $shardId   = "shard{0:d2}" -f ([int]$shard.shard_index)
            $shardPath = Join-Path $outDir "$baseName.$shardId$ext"
            $shardPaths += $shardPath
            $allShardSpecs += [PSCustomObject]@{
                candidate_id    = "${candidateId}_${shardId}"
                tuning_path     = [string]$spec.tuning_path
                metrics_path    = $shardPath
                label           = "$([string]$spec.label) [$shardId runs=$($shard.runs) offset=$($shard.offset)]"
                runs_per_eval   = [int]$shard.runs
                run_index_offset = [int]$shard.offset
            }
        }
        $mergeMap[$candidateId] = [PSCustomObject]@{
            metrics_path = $metricsPath
            shard_paths  = $shardPaths
        }
    }

    # Run all shards from all candidates together in one flat batch.
    $totalParallelism = $CandidateParallelism * $EvalShardCount
    Invoke-BotMetricsRunBatch `
        -GodotExe $GodotExe `
        -CommonPrefix $CommonPrefix `
        -RunsPerEval $RunsPerEval `
        -StrategySet $StrategySet `
        -CandidateSpecs $allShardSpecs `
        -Parallelism $totalParallelism

    # Merge shards back into each candidate's final metrics file.
    foreach ($candidateId in $mergeMap.Keys) {
        $entry = $mergeMap[$candidateId]
        Merge-BotMetricsShards -ShardMetricsPaths $entry.shard_paths -OutputPath $entry.metrics_path
    }
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
$towerBenchmarkFileResolved = ""
$modifierBenchmarkFileResolved = ""
$runTowerBenchmark = $false
$runModifierBenchmark = $false
if (-not $SkipTowerBenchmark -and -not [string]::IsNullOrWhiteSpace($TowerBenchmarkFile)) {
    $towerBenchmarkFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $TowerBenchmarkFile
    $runTowerBenchmark = $true
}
if (-not $SkipModifierBenchmark -and -not [string]::IsNullOrWhiteSpace($ModifierBenchmarkFile)) {
    $modifierBenchmarkFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $ModifierBenchmarkFile
    $runModifierBenchmark = $true
}
$outputRootResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $OutputRoot

Assert-FileExists -PathValue $godotExe -Label "Godot executable"
Assert-FileExists -PathValue $scenarioFileResolved -Label "Scenario suite file"
if ($runTowerBenchmark) {
    Assert-FileExists -PathValue $towerBenchmarkFileResolved -Label "Tower benchmark suite file"
}
if ($runModifierBenchmark) {
    Assert-FileExists -PathValue $modifierBenchmarkFileResolved -Label "Modifier benchmark suite file"
}
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
if ($TargetSurgesPerRunTolerance -le 0) { throw "TargetSurgesPerRunTolerance must be > 0." }
if ($MaxKillsPerSurge -lt 0) { throw "MaxKillsPerSurge must be >= 0." }
if ($MinGlobalSurgesPerRun -lt 0) { throw "MinGlobalSurgesPerRun must be >= 0." }
if ($RelativeWinTargetEasyUplift -lt 0) { throw "RelativeWinTargetEasyUplift must be >= 0." }
if ($RelativeWinTargetNormalUplift -lt 0) { throw "RelativeWinTargetNormalUplift must be >= 0." }
if ($RelativeWinTargetHardUplift -lt 0) { throw "RelativeWinTargetHardUplift must be >= 0." }
if ($DifficultyRegressionTolerance -lt 0) { throw "DifficultyRegressionTolerance must be >= 0." }
if ($NormalRegressionPenaltyWeight -lt 0) { throw "NormalRegressionPenaltyWeight must be >= 0." }
if ($HardRegressionPenaltyWeight -lt 0) { throw "HardRegressionPenaltyWeight must be >= 0." }
if ($MinSweepScoreRatioVsBaseline -lt 0) { throw "MinSweepScoreRatioVsBaseline must be >= 0." }
if ($TargetMaxTowerSurgeRatio -lt 1.0) { throw "TargetMaxTowerSurgeRatio must be >= 1.0." }
if ($TargetMaxModifierSurgeRatio -lt 1.0) { throw "TargetMaxModifierSurgeRatio must be >= 1.0." }
if ($TargetTowerWinRateGap -lt 0) { throw "TargetTowerWinRateGap must be >= 0." }
if ($TargetModifierWinRateGap -lt 0) { throw "TargetModifierWinRateGap must be >= 0." }
if ($HardGuardMaxTowerSurgeRatio -lt 1.0) { throw "HardGuardMaxTowerSurgeRatio must be >= 1.0." }
if ($HardGuardMaxTowerWinRateGap -lt 0) { throw "HardGuardMaxTowerWinRateGap must be >= 0." }
if ($TopCandidateReevalCount -lt 0) { throw "TopCandidateReevalCount must be >= 0." }
if ($MinTowerPlacementsForParity -lt 1) { throw "MinTowerPlacementsForParity must be >= 1." }
if ($MinTowerRunsForFairness -lt 1) { throw "MinTowerRunsForFairness must be >= 1." }
if ($MinModifierRunsForFairness -lt 1) { throw "MinModifierRunsForFairness must be >= 1." }
if ($MinModifierRunsForSurgeParity -lt 1) { throw "MinModifierRunsForSurgeParity must be >= 1." }
if ($TargetWinRateEasy -lt 0 -or $TargetWinRateEasy -gt 1) { throw "TargetWinRateEasy must be between 0 and 1." }
if ($TargetWinRateNormal -lt 0 -or $TargetWinRateNormal -gt 1) { throw "TargetWinRateNormal must be between 0 and 1." }
if ($TargetWinRateHard -lt 0 -or $TargetWinRateHard -gt 1) { throw "TargetWinRateHard must be between 0 and 1." }
if ($TwoPassPhaseSplitPercent -lt 10 -or $TwoPassPhaseSplitPercent -gt 90) { throw "TwoPassPhaseSplitPercent must be between 10 and 90." }
if ($TwoPassPass2RunsMultiplier -lt 1.0) { throw "TwoPassPass2RunsMultiplier must be >= 1.0." }
if ($CandidateParallelism -gt 1 -and $null -eq (Get-Command Start-Job -ErrorAction SilentlyContinue)) {
    throw "CandidateParallelism > 1 requires Start-Job support in this PowerShell host."
}
$rawStrategySet = if ($null -eq $StrategySet) { "" } else { [string]$StrategySet }
$strategySetNormalized = $rawStrategySet.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($strategySetNormalized)) {
    $strategySetNormalized = "all"
}
if ($strategySetNormalized -notin @("all", "optimization", "edge", "spectacle")) {
    throw "StrategySet must be one of: all, optimization, edge, spectacle."
}

$script:UseFastBotMetrics = [bool]$UseFastBotMetrics

$effectiveCandidateParallelism = [Math]::Max(1, [Math]::Min($CandidateParallelism, $CandidatesPerIteration))
# EvalShardParallelism=0 inherits from CandidateParallelism (legacy behaviour).
$resolvedEvalShardParallelism = if ($EvalShardParallelism -gt 0) { $EvalShardParallelism } else { $CandidateParallelism }
$effectiveRunParallelism = [Math]::Max(1, [Math]::Min($resolvedEvalShardParallelism, $Runs))
$effectiveEvalShardParallelism = [Math]::Max(1, [Math]::Min($resolvedEvalShardParallelism, $Runs))

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
Write-Host "Parallelism:  candidates=$effectiveCandidateParallelism, eval_shards=$effectiveEvalShardParallelism (total per iter up to $($effectiveCandidateParallelism * $effectiveEvalShardParallelism))"
Write-Host "Top re-eval:  $TopCandidateReevalCount candidate(s) per iteration"
Write-Host "Strategy set: $strategySetNormalized"
Write-Host "Fast metrics: $script:UseFastBotMetrics"
Write-Host "Two-pass:     $TwoPassMode (split=$TwoPassPhaseSplitPercent%)"
Write-Host "Pass2 runs x: $TwoPassPass2RunsMultiplier"
Write-Host "All-set val:  $(-not $SkipAllStrategyValidation)"
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
if ($Demo) {
    $commonPrefix += "--demo"
    Write-Host "[pipeline] Demo mode enabled  -  bot runs will use demo enemy composition."
}
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
$towerBenchmarkOut = Join-Path $runDir "combat_lab_tower_benchmark.json"
$modifierBenchmarkOut = Join-Path $runDir "combat_lab_modifier_benchmark.json"

if ($runTowerBenchmark) {
    Invoke-GodotCommand -GodotExe $godotExe -Label "[prescreen] Tower benchmark suite" -Args (
        $commonPrefix + @("--tuning_file", $tuningFileResolved, "--lab_tower_benchmark", $towerBenchmarkFileResolved, "--lab_out", $towerBenchmarkOut)
    )
} else {
    Write-Host "Tower benchmark prescreen skipped."
}

if ($runModifierBenchmark) {
    Invoke-GodotCommand -GodotExe $godotExe -Label "[prescreen] Modifier benchmark suite" -Args (
        $commonPrefix + @("--tuning_file", $tuningFileResolved, "--lab_modifier_benchmark", $modifierBenchmarkFileResolved, "--lab_out", $modifierBenchmarkOut)
    )
} else {
    Write-Host "Modifier benchmark prescreen skipped."
}

$baselineMetricsOut = Join-Path $runDir "bot_metrics_baseline.json"
$scenarioReportOut = Join-Path $runDir "combat_lab_report.json"
$sweepReportOut = Join-Path $runDir "combat_lab_sweep.json"
$tunedMetricsOut = Join-Path $runDir "bot_metrics_tuned.json"
$traceOut = Join-Path $runDir "bot_trace.json"
$deltaOut = Join-Path $runDir "bot_metrics_delta.txt"
$bestTuningOut = Join-Path $runDir "best_tuning.json"
$autoTuneReportOut = Join-Path $runDir "autotune_report.json"
$allStrategyMetricsOut = Join-Path $runDir "bot_metrics_tuned_all_strategies.json"

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
    -TargetSurgesPerRun $TargetSurgesPerRun `
    -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
    -MaxKillsPerSurge $MaxKillsPerSurge `
    -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxModifierSurgeRatio $TargetMaxModifierSurgeRatio `
    -TargetTowerWinRateGap $TargetTowerWinRateGap `
    -TargetModifierWinRateGap $TargetModifierWinRateGap `
    -HardGuardMaxTowerSurgeRatio $HardGuardMaxTowerSurgeRatio `
    -HardGuardMaxTowerWinRateGap $HardGuardMaxTowerWinRateGap `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -MinTowerRunsForFairness $MinTowerRunsForFairness `
    -MinModifierRunsForFairness $MinModifierRunsForFairness `
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
    -TargetSurgesPerRun $TargetSurgesPerRun `
    -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
    -MaxKillsPerSurge $MaxKillsPerSurge `
    -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxModifierSurgeRatio $TargetMaxModifierSurgeRatio `
    -TargetTowerWinRateGap $TargetTowerWinRateGap `
    -TargetModifierWinRateGap $TargetModifierWinRateGap `
    -HardGuardMaxTowerSurgeRatio $HardGuardMaxTowerSurgeRatio `
    -HardGuardMaxTowerWinRateGap $HardGuardMaxTowerWinRateGap `
    -MinNormalWinRate $guardMinNormalWinRate `
    -MinHardWinRate $guardMinHardWinRate `
    -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
    -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -MinTowerRunsForFairness $MinTowerRunsForFairness `
    -MinModifierRunsForFairness $MinModifierRunsForFairness `
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
    global_surges_per_run = $seedScore.GlobalSurgesPerRun
    kills_per_surge = $seedScore.KillsPerSurge
    tower_surge_parity_ratio = $seedScore.TowerSurgeParityRatio
    tower_surge_parity_eligible_towers = $seedScore.TowerSurgeParityEligibleTowers
    tower_recharge_parity_ratio = $seedScore.TowerRechargeParityRatio
    tower_recharge_parity_eligible_towers = $seedScore.TowerRechargeParityEligibleTowers
    modifier_surge_parity_ratio = $seedScore.ModifierSurgeParityRatio
    modifier_surge_parity_eligible = $seedScore.ModifierSurgeParityEligible
    tower_win_rate_gap = $seedScore.TowerWinRateGap
    tower_win_rate_eligible = $seedScore.TowerWinRateEligible
    modifier_win_rate_gap = $seedScore.ModifierWinRateGap
    modifier_win_rate_eligible = $seedScore.ModifierWinRateEligible
    avg_max_chain_depth = $seedScore.AvgMaxChainDepth
    frame_stress_peaks = [PSCustomObject]@{
        explosions = $seedScore.PeakExplosions
        hazards = $seedScore.PeakHazards
        hitstops = $seedScore.PeakHitStops
    }
    tuning_file = $seedTuningPath
    metrics_file = $seedMetricsPath
}

$twoPassSplitIterations = 0
if ($TwoPassMode) {
    if ($Iterations -le 1) {
        $twoPassSplitIterations = 1
    } else {
        $split = [int][Math]::Round($Iterations * ([double]$TwoPassPhaseSplitPercent / 100.0))
        $twoPassSplitIterations = [Math]::Min([Math]::Max(1, $split), $Iterations - 1)
    }
    Write-Host ("Two-pass split: pass1 iterations={0}, pass2 iterations={1}" -f $twoPassSplitIterations, [Math]::Max(0, $Iterations - $twoPassSplitIterations))
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

    $passPhase = "single"
    $iterationMutationStrength = [double]$MutationStrength
    $iterationRuns = [int]$Runs
    $iterTargetMaxTowerSurgeRatio = [double]$TargetMaxTowerSurgeRatio
    $iterTargetMaxModifierSurgeRatio = [double]$TargetMaxModifierSurgeRatio
    $iterTargetTowerWinRateGap = [double]$TargetTowerWinRateGap
    $iterTargetModifierWinRateGap = [double]$TargetModifierWinRateGap
    $iterHardGuardMaxTowerSurgeRatio = [double]$HardGuardMaxTowerSurgeRatio
    $iterHardGuardMaxTowerWinRateGap = [double]$HardGuardMaxTowerWinRateGap

    if ($TwoPassMode) {
        $isPass1Parity = $iteration -le $twoPassSplitIterations
        if ($isPass1Parity) {
            $passPhase = "pass1_parity"
            $iterationMutationStrength = [double]$MutationStrength * 1.10
            $iterTargetMaxTowerSurgeRatio = [Math]::Min([double]$TargetMaxTowerSurgeRatio, 1.95)
            $iterTargetMaxModifierSurgeRatio = [Math]::Min([double]$TargetMaxModifierSurgeRatio, 2.15)
            $iterTargetTowerWinRateGap = [Math]::Min([double]$TargetTowerWinRateGap, 0.17)
            $iterTargetModifierWinRateGap = [Math]::Min([double]$TargetModifierWinRateGap, 0.20)
            $iterHardGuardMaxTowerSurgeRatio = [Math]::Min([double]$HardGuardMaxTowerSurgeRatio, $iterTargetMaxTowerSurgeRatio + 0.55)
            $iterHardGuardMaxTowerWinRateGap = [Math]::Min([double]$HardGuardMaxTowerWinRateGap, $iterTargetTowerWinRateGap + 0.10)
        } else {
            $passPhase = "pass2_difficulty"
            $iterationMutationStrength = [double]$MutationStrength * 0.90
            $iterationRuns = [Math]::Max(1, [int][Math]::Ceiling([double]$Runs * [double]$TwoPassPass2RunsMultiplier))
        }
    }

    Write-Host ""
    Write-Host "=== [3/8] Auto-tune iteration $iteration/$Iterations (anneal=$([Math]::Round($anneal,3)), phase=$passPhase, runs=$iterationRuns) ==="

    $iterCandidates = @()
    $candidateSpecsToEvaluate = @()
    $allCandidateSpecs = @()

    for ($candidateIndex = 1; $candidateIndex -le $CandidatesPerIteration; $candidateIndex++) {
        $candidateId = "iter{0:d2}_cand{1:d2}" -f $iteration, $candidateIndex
        $candidateType = if ($candidateIndex -eq 1) { "carry_forward" } else { "mutated" }
        $candidateProfile = if ($candidateIndex -eq 1) {
            Clone-TuningProfile -Profile $globalBestProfile
        } else {
            New-MutatedTuningProfile -BaseProfile $globalBestProfile -Anneal $anneal -Strength $iterationMutationStrength -FreezeSpectacleParams:($FreezeSpectacleParams -or $DifficultyOnlyMode -or $NormalOnlyMode) -SpectacleOnlyMode:$SpectacleOnlyMode -FreezeHardParams:($NormalOnlyMode)
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
        Invoke-BotMetricsRunBatchSharded `
            -GodotExe $godotExe `
            -CommonPrefix $commonPrefix `
            -RunsPerEval $iterationRuns `
            -StrategySet $strategySetNormalized `
            -CandidateSpecs $candidateSpecsToEvaluate `
            -CandidateParallelism $effectiveCandidateParallelism `
            -EvalShardCount $effectiveEvalShardParallelism
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
            -TargetSurgesPerRun $TargetSurgesPerRun `
            -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
            -MaxKillsPerSurge $MaxKillsPerSurge `
            -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
            -TargetMaxTowerSurgeRatio $iterTargetMaxTowerSurgeRatio `
            -TargetMaxModifierSurgeRatio $iterTargetMaxModifierSurgeRatio `
            -TargetTowerWinRateGap $iterTargetTowerWinRateGap `
            -TargetModifierWinRateGap $iterTargetModifierWinRateGap `
            -HardGuardMaxTowerSurgeRatio $iterHardGuardMaxTowerSurgeRatio `
            -HardGuardMaxTowerWinRateGap $iterHardGuardMaxTowerWinRateGap `
            -MinNormalWinRate $guardMinNormalWinRate `
            -MinHardWinRate $guardMinHardWinRate `
            -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
            -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
            -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
            -MinTowerRunsForFairness $MinTowerRunsForFairness `
            -MinModifierRunsForFairness $MinModifierRunsForFairness `
            -ChainDepthCap $MaxChainDepth `
            -ExplosionCap $MaxSimultaneousExplosions `
            -HazardCap $MaxSimultaneousHazards `
            -HitStopCap $MaxSimultaneousHitStops `
            -DurationFloor $MinRunDurationSeconds

        $candidateResult = [PSCustomObject]@{
            iteration = $iteration
            pass_phase = $passPhase
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
            global_surges_per_run = $candidateScore.GlobalSurgesPerRun
            kills_per_surge = $candidateScore.KillsPerSurge
            tower_surge_parity_ratio = $candidateScore.TowerSurgeParityRatio
            tower_surge_parity_eligible_towers = $candidateScore.TowerSurgeParityEligibleTowers
            tower_recharge_parity_ratio = $candidateScore.TowerRechargeParityRatio
            tower_recharge_parity_eligible_towers = $candidateScore.TowerRechargeParityEligibleTowers
            modifier_surge_parity_ratio = $candidateScore.ModifierSurgeParityRatio
            modifier_surge_parity_eligible = $candidateScore.ModifierSurgeParityEligible
            tower_win_rate_gap = $candidateScore.TowerWinRateGap
            tower_win_rate_eligible = $candidateScore.TowerWinRateEligible
            modifier_win_rate_gap = $candidateScore.ModifierWinRateGap
            modifier_win_rate_eligible = $candidateScore.ModifierWinRateEligible
            hard_rejected = $candidateScore.HardRejected
            hard_reject_reason = $candidateScore.HardRejectReason
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
        $hardRejectSuffix = if ($candidateScore.HardRejected) { " HARD_REJECT($($candidateScore.HardRejectReason))" } else { "" }
        Write-Host (("Candidate {0}: score={1:0.0000}, win(E/N/H)={2:P1}/{3:P1}/{4:P1}, wave={5:0.00}, surges/global={6:0.00}/{7:0.00}, kps={8:0.00}, explosion={9:P2}, towerSurgeParity={10:0.00}, towerRechargeParity={11:0.00}, modSurgeParity={12:0.00}, towerGap={13:P1}, modGap={14:P1}" -f $candidateId, $candidateScore.Score, $candidateScore.EasyWinRate, $candidateScore.NormalWinRate, $candidateScore.HardWinRate, $candidateScore.AvgWave, $candidateScore.SurgesPerRun, $candidateScore.GlobalSurgesPerRun, $candidateScore.KillsPerSurge, $candidateScore.ExplosionShare, $candidateScore.TowerSurgeParityRatio, $candidateScore.TowerRechargeParityRatio, $candidateScore.ModifierSurgeParityRatio, $candidateScore.TowerWinRateGap, $candidateScore.ModifierWinRateGap) + $hardRejectSuffix)
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

        Invoke-BotMetricsRunBatchSharded `
            -GodotExe $godotExe `
            -CommonPrefix $commonPrefix `
            -RunsPerEval $iterationRuns `
            -StrategySet $strategySetNormalized `
            -CandidateSpecs $reevalSpecs `
            -CandidateParallelism $effectiveCandidateParallelism `
            -EvalShardCount $effectiveEvalShardParallelism

        foreach ($candidate in $topCandidatesForReeval) {
            $candidateId = [string]$candidate.candidate
            $reevalMetricsPath = [string]$reevalSpecMap[$candidateId]
            $origPayload  = Read-MetricsPayload -MetricsPath ([string]$candidate.metrics_file)
            $reevalPayload = Read-MetricsPayload -MetricsPath $reevalMetricsPath

            # Score reeval in isolation for diagnostics only.
            $reevalScore = Get-MetricsScore `
                -Summary $reevalPayload.summary `
                -Runs $reevalPayload.runs `
                -TargetShare $TargetExplosionShare `
                -TargetShareTolerance $TargetExplosionShareTolerance `
                -TargetEasyWinRate $effectiveTargetWinRateEasy `
                -TargetNormalWinRate $effectiveTargetWinRateNormal `
                -TargetHardWinRate $effectiveTargetWinRateHard `
                -WinRateTolerance $TargetWinRateTolerance `
                -TargetSurgesPerRun $TargetSurgesPerRun `
                -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
                -MaxKillsPerSurge $MaxKillsPerSurge `
                -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
                -TargetMaxTowerSurgeRatio $iterTargetMaxTowerSurgeRatio `
                -TargetMaxModifierSurgeRatio $iterTargetMaxModifierSurgeRatio `
                -TargetTowerWinRateGap $iterTargetTowerWinRateGap `
                -TargetModifierWinRateGap $iterTargetModifierWinRateGap `
                -HardGuardMaxTowerSurgeRatio $iterHardGuardMaxTowerSurgeRatio `
                -HardGuardMaxTowerWinRateGap $iterHardGuardMaxTowerWinRateGap `
                -MinNormalWinRate $guardMinNormalWinRate `
                -MinHardWinRate $guardMinHardWinRate `
                -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
                -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
                -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
                -MinTowerRunsForFairness $MinTowerRunsForFairness `
                -MinModifierRunsForFairness $MinModifierRunsForFairness `
                -ChainDepthCap $MaxChainDepth `
                -ExplosionCap $MaxSimultaneousExplosions `
                -HazardCap $MaxSimultaneousHazards `
                -HitStopCap $MaxSimultaneousHitStops `
                -DurationFloor $MinRunDurationSeconds

            # Merge eval + reeval into a single combined payload and score that.
            # This eliminates the artificial variance from averaging two independent scores -
            # instead the scorer sees all runs at once (e.g. 400 runs vs two 200-run samples).
            $mergedPayload = Merge-MetricsPayloads -Payload1 $origPayload -Payload2 $reevalPayload
            $mergedScore = Get-MetricsScore `
                -Summary $mergedPayload.summary `
                -Runs $mergedPayload.runs `
                -TargetShare $TargetExplosionShare `
                -TargetShareTolerance $TargetExplosionShareTolerance `
                -TargetEasyWinRate $effectiveTargetWinRateEasy `
                -TargetNormalWinRate $effectiveTargetWinRateNormal `
                -TargetHardWinRate $effectiveTargetWinRateHard `
                -WinRateTolerance $TargetWinRateTolerance `
                -TargetSurgesPerRun $TargetSurgesPerRun `
                -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
                -MaxKillsPerSurge $MaxKillsPerSurge `
                -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
                -TargetMaxTowerSurgeRatio $iterTargetMaxTowerSurgeRatio `
                -TargetMaxModifierSurgeRatio $iterTargetMaxModifierSurgeRatio `
                -TargetTowerWinRateGap $iterTargetTowerWinRateGap `
                -TargetModifierWinRateGap $iterTargetModifierWinRateGap `
                -HardGuardMaxTowerSurgeRatio $iterHardGuardMaxTowerSurgeRatio `
                -HardGuardMaxTowerWinRateGap $iterHardGuardMaxTowerWinRateGap `
                -MinNormalWinRate $guardMinNormalWinRate `
                -MinHardWinRate $guardMinHardWinRate `
                -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
                -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
                -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
                -MinTowerRunsForFairness $MinTowerRunsForFairness `
                -MinModifierRunsForFairness $MinModifierRunsForFairness `
                -ChainDepthCap $MaxChainDepth `
                -ExplosionCap $MaxSimultaneousExplosions `
                -HazardCap $MaxSimultaneousHazards `
                -HitStopCap $MaxSimultaneousHitStops `
                -DurationFloor $MinRunDurationSeconds

            $candidate.reevaluated = $true
            $candidate.reeval_score = $reevalScore.Score
            $candidate.reeval_metrics_file = $reevalMetricsPath
            $candidate.promoted_metrics_file = $reevalMetricsPath
            $candidate.effective_score = [Math]::Round($mergedScore.Score, 4)
            Write-Host ("Candidate {0}: reeval_score={1:0.0000} (isolated), merged_score={2:0.0000} ({3} runs)" -f $candidateId, $reevalScore.Score, $mergedScore.Score, $mergedPayload.run_count)
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

# Write best tuning to Data/ so the game auto-loads the correct profile at startup.
$dataDir = Join-Path $projectRoot "Data"
$dataTargetFile = if ($Demo) { "best_tuning_demo.json" } else { "best_tuning_full.json" }
$dataTargetPath = Join-Path $dataDir $dataTargetFile
Copy-Item -Path $bestTuningOut -Destination $dataTargetPath -Force
Write-Host "[pipeline] Updated $dataTargetPath"

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

$allStrategyMetricsPath = $null
if (-not $SkipAllStrategyValidation) {
    if ($strategySetNormalized -eq "all") {
        $allStrategyMetricsPath = $tunedMetricsOut
        Write-Host "[8/8] All-strategy validation reuses final tuned metrics (strategy_set=all)."
    } else {
        $allStrategyMetricsPath = $allStrategyMetricsOut
        Invoke-BotMetricsRunSharded -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet "all" -TuningPath $bestTuningOut -MetricsOut $allStrategyMetricsOut -Label "[8/8] Validation bot metrics (all strategies)" -Parallelism $effectiveRunParallelism
    }
}

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
    -TargetSurgesPerRun $TargetSurgesPerRun `
    -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
    -MaxKillsPerSurge $MaxKillsPerSurge `
    -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -TargetMaxModifierSurgeRatio $TargetMaxModifierSurgeRatio `
    -TargetTowerWinRateGap $TargetTowerWinRateGap `
    -TargetModifierWinRateGap $TargetModifierWinRateGap `
    -HardGuardMaxTowerSurgeRatio $HardGuardMaxTowerSurgeRatio `
    -HardGuardMaxTowerWinRateGap $HardGuardMaxTowerWinRateGap `
    -MinNormalWinRate $guardMinNormalWinRate `
    -MinHardWinRate $guardMinHardWinRate `
    -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
    -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -MinTowerRunsForFairness $MinTowerRunsForFairness `
    -MinModifierRunsForFairness $MinModifierRunsForFairness `
    -ChainDepthCap $MaxChainDepth `
    -ExplosionCap $MaxSimultaneousExplosions `
    -HazardCap $MaxSimultaneousHazards `
    -HitStopCap $MaxSimultaneousHitStops `
    -DurationFloor $MinRunDurationSeconds

$allStrategyValidation = $null
if (-not $SkipAllStrategyValidation -and -not [string]::IsNullOrWhiteSpace($allStrategyMetricsPath)) {
    $allPayload = if ($allStrategyMetricsPath -eq $tunedMetricsOut) { $tunedPayload } else { Read-MetricsPayload -MetricsPath $allStrategyMetricsPath }
    $allScore = if ($allStrategyMetricsPath -eq $tunedMetricsOut) {
        $tunedScore
    } else {
        Get-MetricsScore `
            -Summary $allPayload.summary `
            -Runs $allPayload.runs `
            -TargetShare $TargetExplosionShare `
            -TargetShareTolerance $TargetExplosionShareTolerance `
            -TargetEasyWinRate $effectiveTargetWinRateEasy `
            -TargetNormalWinRate $effectiveTargetWinRateNormal `
            -TargetHardWinRate $effectiveTargetWinRateHard `
            -WinRateTolerance $TargetWinRateTolerance `
            -TargetSurgesPerRun $TargetSurgesPerRun `
            -TargetSurgesPerRunTolerance $TargetSurgesPerRunTolerance `
            -MaxKillsPerSurge $MaxKillsPerSurge `
            -MinGlobalSurgesPerRun $MinGlobalSurgesPerRun `
            -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
            -TargetMaxModifierSurgeRatio $TargetMaxModifierSurgeRatio `
            -TargetTowerWinRateGap $TargetTowerWinRateGap `
            -TargetModifierWinRateGap $TargetModifierWinRateGap `
            -HardGuardMaxTowerSurgeRatio $HardGuardMaxTowerSurgeRatio `
            -HardGuardMaxTowerWinRateGap $HardGuardMaxTowerWinRateGap `
            -MinNormalWinRate $guardMinNormalWinRate `
            -MinHardWinRate $guardMinHardWinRate `
            -NormalRegressionPenaltyWeight $NormalRegressionPenaltyWeight `
            -HardRegressionPenaltyWeight $HardRegressionPenaltyWeight `
            -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
            -MinTowerRunsForFairness $MinTowerRunsForFairness `
            -MinModifierRunsForFairness $MinModifierRunsForFairness `
            -ChainDepthCap $MaxChainDepth `
            -ExplosionCap $MaxSimultaneousExplosions `
            -HazardCap $MaxSimultaneousHazards `
            -HitStopCap $MaxSimultaneousHitStops `
            -DurationFloor $MinRunDurationSeconds
    }

    $allStrategyValidation = [ordered]@{
        metrics_file = $allStrategyMetricsPath
        score = $allScore.Score
        win_rate = $allScore.WinRate
        easy_win_rate = $allScore.EasyWinRate
        normal_win_rate = $allScore.NormalWinRate
        hard_win_rate = $allScore.HardWinRate
        surges_per_run = $allScore.SurgesPerRun
        global_surges_per_run = $allScore.GlobalSurgesPerRun
        tower_surge_parity_ratio = $allScore.TowerSurgeParityRatio
        tower_recharge_parity_ratio = $allScore.TowerRechargeParityRatio
        tower_win_rate_gap = $allScore.TowerWinRateGap
        modifier_win_rate_gap = $allScore.ModifierWinRateGap
        hard_rejected = $allScore.HardRejected
        hard_reject_reason = $allScore.HardRejectReason
    }
}

$reportPayload = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    runs = $Runs
    iterations = $Iterations
    candidates_per_iteration = $CandidatesPerIteration
    candidate_parallelism = $effectiveCandidateParallelism
    eval_shard_parallelism = $effectiveEvalShardParallelism
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
        target_surges_per_run = $TargetSurgesPerRun
        target_surges_per_run_tolerance = $TargetSurgesPerRunTolerance
        max_kills_per_surge = $MaxKillsPerSurge
        min_global_surges_per_run = $MinGlobalSurgesPerRun
        difficulty_regression_tolerance = $DifficultyRegressionTolerance
        normal_regression_penalty_weight = $NormalRegressionPenaltyWeight
        hard_regression_penalty_weight = $HardRegressionPenaltyWeight
        min_sweep_score_ratio_vs_baseline = $MinSweepScoreRatioVsBaseline
        target_max_tower_surge_ratio = $TargetMaxTowerSurgeRatio
        target_max_modifier_surge_ratio = $TargetMaxModifierSurgeRatio
        hard_guard_max_tower_surge_ratio = $HardGuardMaxTowerSurgeRatio
        hard_guard_max_tower_win_rate_gap = $HardGuardMaxTowerWinRateGap
        min_tower_placements_for_parity = $MinTowerPlacementsForParity
        target_tower_win_rate_gap = $TargetTowerWinRateGap
        target_modifier_win_rate_gap = $TargetModifierWinRateGap
        min_tower_runs_for_fairness = $MinTowerRunsForFairness
        min_modifier_runs_for_fairness = $MinModifierRunsForFairness
        min_modifier_runs_for_surge_parity = $MinModifierRunsForSurgeParity
        max_chain_depth = $MaxChainDepth
        max_simultaneous_explosions = $MaxSimultaneousExplosions
        max_simultaneous_hazards = $MaxSimultaneousHazards
        max_simultaneous_hitstops = $MaxSimultaneousHitStops
        min_run_duration_seconds = $MinRunDurationSeconds
        top_candidate_reeval_count = $TopCandidateReevalCount
        two_pass_mode = $TwoPassMode
        two_pass_phase_split_percent = $TwoPassPhaseSplitPercent
        two_pass_phase1_iterations = $twoPassSplitIterations
        skip_all_strategy_validation = $SkipAllStrategyValidation
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
        global_surges_per_run = $baselineScore.GlobalSurgesPerRun
        kills_per_surge = $baselineScore.KillsPerSurge
        explosion_share = $baselineScore.ExplosionShare
        tower_surge_parity_ratio = $baselineScore.TowerSurgeParityRatio
        tower_surge_parity_eligible_towers = $baselineScore.TowerSurgeParityEligibleTowers
        tower_recharge_parity_ratio = $baselineScore.TowerRechargeParityRatio
        tower_recharge_parity_eligible_towers = $baselineScore.TowerRechargeParityEligibleTowers
        modifier_surge_parity_ratio = $baselineScore.ModifierSurgeParityRatio
        modifier_surge_parity_eligible = $baselineScore.ModifierSurgeParityEligible
        tower_win_rate_gap = $baselineScore.TowerWinRateGap
        tower_win_rate_eligible = $baselineScore.TowerWinRateEligible
        modifier_win_rate_gap = $baselineScore.ModifierWinRateGap
        modifier_win_rate_eligible = $baselineScore.ModifierWinRateEligible
        hard_rejected = $baselineScore.HardRejected
        hard_reject_reason = $baselineScore.HardRejectReason
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
        final_global_surges_per_run = $tunedScore.GlobalSurgesPerRun
        final_kills_per_surge = $tunedScore.KillsPerSurge
        final_explosion_share = $tunedScore.ExplosionShare
        final_tower_surge_parity_ratio = $tunedScore.TowerSurgeParityRatio
        final_tower_surge_parity_eligible_towers = $tunedScore.TowerSurgeParityEligibleTowers
        final_tower_recharge_parity_ratio = $tunedScore.TowerRechargeParityRatio
        final_tower_recharge_parity_eligible_towers = $tunedScore.TowerRechargeParityEligibleTowers
        final_modifier_surge_parity_ratio = $tunedScore.ModifierSurgeParityRatio
        final_modifier_surge_parity_eligible = $tunedScore.ModifierSurgeParityEligible
        final_tower_win_rate_gap = $tunedScore.TowerWinRateGap
        final_tower_win_rate_eligible = $tunedScore.TowerWinRateEligible
        final_modifier_win_rate_gap = $tunedScore.ModifierWinRateGap
        final_modifier_win_rate_eligible = $tunedScore.ModifierWinRateEligible
        final_hard_rejected = $tunedScore.HardRejected
        final_hard_reject_reason = $tunedScore.HardRejectReason
    }
    validation_all_strategies = $allStrategyValidation
    history = $history
}

$reportJson = $reportPayload | ConvertTo-Json -Depth 14
Set-Content -Path $autoTuneReportOut -Value $reportJson -Encoding UTF8

Write-Host ""
Write-Host "=== Pipeline complete ==="
Write-Host "Baseline metrics : $baselineMetricsOut"
Write-Host "Best tuning file : $bestTuningOut"
Write-Host "Data tuning file : $dataTargetPath"
Write-Host "Scenario report  : $scenarioReportOut"
Write-Host "Sweep report     : $sweepReportOut"
if ($runTowerBenchmark) {
    Write-Host "Tower benchmark  : $towerBenchmarkOut"
}
if ($runModifierBenchmark) {
    Write-Host "Modifier bench   : $modifierBenchmarkOut"
}
Write-Host "Tuned metrics    : $tunedMetricsOut"
if (-not $SkipAllStrategyValidation -and -not [string]::IsNullOrWhiteSpace($allStrategyMetricsPath) -and $allStrategyMetricsPath -ne $tunedMetricsOut) {
    Write-Host "All-set metrics  : $allStrategyMetricsPath"
}
if (-not $SkipTrace) {
    Write-Host "Trace file       : $traceOut"
}
Write-Host "Delta report     : $deltaOut"
Write-Host "Auto-tune report : $autoTuneReportOut"


