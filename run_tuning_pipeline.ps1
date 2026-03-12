# run_tuning_pipeline.ps1
#
# Automated tuning pipeline with true iterative optimization:
# 1) Baseline bot metrics (once)
# 2) Evaluate starting tuning profile (seed)
# 3) For each iteration:
#    - Generate candidate tuning profiles
#    - Run bot metrics per candidate
#    - Score candidates and keep the best
#    - Validate iteration best with scenario suite
#    - Compare baseline vs iteration-best via generated sweep
# 4) Final validation/reporting with global best:
#    - Scenario suite
#    - Sweep comparison
#    - Tuned bot metrics
#    - Optional live bot traces
#    - Baseline vs tuned delta report
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Runs 60 -Iterations 6 -CandidatesPerIteration 4
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -StrategySet optimization
#   powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SkipBuild -SkipTrace

[CmdletBinding()]
param(
    [int]$Runs = 120,
    [Alias("TuningIterations")][int]$Iterations = 4,
    [int]$CandidatesPerIteration = 3,
    [int]$SweepRunsPerVariant = 12,
    [int]$Seed = 1337,
    [double]$MutationStrength = 1.0,
    [double]$TargetExplosionShare = 0.10,
    [double]$TargetExplosionShareTolerance = 0.12,
    [double]$TargetWinRateEasy = 0.95,
    [double]$TargetWinRateNormal = 0.75,
    [double]$TargetWinRateHard = 0.45,
    [double]$TargetWinRateTolerance = 0.06,
    [double]$TargetMaxTowerSurgeRatio = 2.0,
    [int]$MinTowerPlacementsForParity = 6,
    [double]$MaxChainDepth = 4.0,
    [int]$MaxSimultaneousExplosions = 8,
    [int]$MaxSimultaneousHazards = 12,
    [int]$MaxSimultaneousHitStops = 4,
    [double]$MinRunDurationSeconds = 900.0,
    [string]$StrategySet = "optimization",
    [string]$GodotPath = "",
    [string]$TuningFile = "Data/combat_lab/sample_tuning.json",
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
    $p.meter_gain_multiplier = [Math]::Round((Clamp-Double -Value $p.meter_gain_multiplier -Min 0.0 -Max 4.0), 4)
    $p.surge_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_threshold_multiplier -Min 0.05 -Max 4.0), 4)
    $p.surge_cooldown_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_cooldown_multiplier -Min 0.0 -Max 4.0), 4)
    $p.surge_meter_after_trigger_multiplier = [Math]::Round((Clamp-Double -Value $p.surge_meter_after_trigger_multiplier -Min 0.0 -Max 4.0), 4)
    $p.global_meter_per_surge_multiplier = [Math]::Round((Clamp-Double -Value $p.global_meter_per_surge_multiplier -Min 0.0 -Max 4.0), 4)
    $p.global_threshold_multiplier = [Math]::Round((Clamp-Double -Value $p.global_threshold_multiplier -Min 0.05 -Max 4.0), 4)
    $p.global_meter_after_trigger_multiplier = [Math]::Round((Clamp-Double -Value $p.global_meter_after_trigger_multiplier -Min 0.0 -Max 4.0), 4)
    $p.global_contribution_window_multiplier = [Math]::Round((Clamp-Double -Value $p.global_contribution_window_multiplier -Min 0.05 -Max 4.0), 4)
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

    # Secondary quality signals.
    $score += $winRate * 15.0
    $score += $avgWave * 1.0
    $score += [Math]::Min($surgesPerRun, 120.0) * 0.03
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
    $changed += Apply-ToggleMutation -Obj $candidate -Name "enable_status_detonation" -Chance $toggleChance
    $changed += Apply-ToggleMutation -Obj $candidate -Name "enable_residue" -Chance $toggleChance

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
    $changed += Apply-Mutation -Obj $candidate -Name "meter_gain_multiplier" -Step (0.30 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "surge_threshold_multiplier" -Step (0.24 * $scale) -Min 0.05 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "surge_cooldown_multiplier" -Step (0.24 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "surge_meter_after_trigger_multiplier" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "global_meter_per_surge_multiplier" -Step (0.24 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "global_threshold_multiplier" -Step (0.24 * $scale) -Min 0.05 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "global_meter_after_trigger_multiplier" -Step (0.22 * $scale) -Min 0.0 -Max 4.0
    $changed += Apply-Mutation -Obj $candidate -Name "global_contribution_window_multiplier" -Step (0.24 * $scale) -Min 0.05 -Max 4.0
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
        $candidate.meter_gain_multiplier = [Math]::Round((Clamp-Double -Value ([double]$candidate.meter_gain_multiplier + $forceDelta) -Min 0.0 -Max 4.0), 4)
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

function Invoke-BotMetricsRun {
    param(
        [Parameter(Mandatory = $true)][string]$GodotExe,
        [Parameter(Mandatory = $true)][string[]]$CommonPrefix,
        [Parameter(Mandatory = $true)][int]$RunsPerEval,
        [string]$StrategySet,
        [string]$TuningPath,
        [Parameter(Mandatory = $true)][string]$MetricsOut,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $args = @()
    $args += $CommonPrefix
    $args += "--bot"
    $args += "--runs"
    $args += "$RunsPerEval"
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

    Invoke-GodotCommand -GodotExe $GodotExe -Args $args -Label $Label
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
$tuningFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $TuningFile
$scenarioFileResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $ScenarioFile
$outputRootResolved = Resolve-PathFromProject -ProjectRoot $projectRoot -PathValue $OutputRoot

Assert-FileExists -PathValue $godotExe -Label "Godot executable"
Assert-FileExists -PathValue $tuningFileResolved -Label "Starting tuning profile"
Assert-FileExists -PathValue $scenarioFileResolved -Label "Scenario suite file"

if ($Runs -lt 1) { throw "Runs must be >= 1." }
if ($Iterations -lt 1) { throw "Iterations must be >= 1." }
if ($CandidatesPerIteration -lt 1) { throw "CandidatesPerIteration must be >= 1." }
if ($SweepRunsPerVariant -lt 1) { throw "SweepRunsPerVariant must be >= 1." }
if ($MutationStrength -le 0) { throw "MutationStrength must be > 0." }
if ($TargetExplosionShareTolerance -le 0) { throw "TargetExplosionShareTolerance must be > 0." }
if ($TargetWinRateTolerance -le 0) { throw "TargetWinRateTolerance must be > 0." }
if ($TargetMaxTowerSurgeRatio -lt 1.0) { throw "TargetMaxTowerSurgeRatio must be >= 1.0." }
if ($MinTowerPlacementsForParity -lt 1) { throw "MinTowerPlacementsForParity must be >= 1." }
if ($TargetWinRateEasy -lt 0 -or $TargetWinRateEasy -gt 1) { throw "TargetWinRateEasy must be between 0 and 1." }
if ($TargetWinRateNormal -lt 0 -or $TargetWinRateNormal -gt 1) { throw "TargetWinRateNormal must be between 0 and 1." }
if ($TargetWinRateHard -lt 0 -or $TargetWinRateHard -gt 1) { throw "TargetWinRateHard must be between 0 and 1." }
$rawStrategySet = if ($null -eq $StrategySet) { "" } else { [string]$StrategySet }
$strategySetNormalized = $rawStrategySet.Trim().ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($strategySetNormalized)) {
    $strategySetNormalized = "all"
}
if ($strategySetNormalized -notin @("all", "optimization", "edge")) {
    throw "StrategySet must be one of: all, optimization, edge."
}

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

$commonPrefix = @(
    "--headless",
    "--path", $projectRoot,
    "--scene", "res://Scenes/Main.tscn",
    "--"
)

$baselineMetricsOut = Join-Path $runDir "bot_metrics_baseline.json"
$scenarioReportOut = Join-Path $runDir "combat_lab_report.json"
$sweepReportOut = Join-Path $runDir "combat_lab_sweep.json"
$tunedMetricsOut = Join-Path $runDir "bot_metrics_tuned.json"
$traceOut = Join-Path $runDir "bot_trace.json"
$deltaOut = Join-Path $runDir "bot_metrics_delta.txt"
$bestTuningOut = Join-Path $runDir "best_tuning.json"
$autoTuneReportOut = Join-Path $runDir "autotune_report.json"

Invoke-BotMetricsRun -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -MetricsOut $baselineMetricsOut -Label "[1/8] Baseline bot metrics"
$baselinePayload = Read-MetricsPayload -MetricsPath $baselineMetricsOut
$baselineScore = Get-MetricsScore `
    -Summary $baselinePayload.summary `
    -Runs $baselinePayload.runs `
    -TargetShare $TargetExplosionShare `
    -TargetShareTolerance $TargetExplosionShareTolerance `
    -TargetEasyWinRate $TargetWinRateEasy `
    -TargetNormalWinRate $TargetWinRateNormal `
    -TargetHardWinRate $TargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
    -MinTowerPlacementsForParity $MinTowerPlacementsForParity `
    -ChainDepthCap $MaxChainDepth `
    -ExplosionCap $MaxSimultaneousExplosions `
    -HazardCap $MaxSimultaneousHazards `
    -HitStopCap $MaxSimultaneousHitStops `
    -DurationFloor $MinRunDurationSeconds

$startProfileRaw = Get-Content -Raw $tuningFileResolved | ConvertFrom-Json
$startProfile = Normalize-TuningProfile -InputProfile $startProfileRaw

$seedTuningPath = Join-Path $autoTuneDir "seed_tuning.json"
$seedMetricsPath = Join-Path $autoTuneDir "seed_metrics.json"
Write-TuningProfile -Profile $startProfile -OutputPath $seedTuningPath
Invoke-BotMetricsRun -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -TuningPath $seedTuningPath -MetricsOut $seedMetricsPath -Label "[2/8] Evaluate starting tuning profile"
$seedPayload = Read-MetricsPayload -MetricsPath $seedMetricsPath
$seedScore = Get-MetricsScore `
    -Summary $seedPayload.summary `
    -Runs $seedPayload.runs `
    -TargetShare $TargetExplosionShare `
    -TargetShareTolerance $TargetExplosionShareTolerance `
    -TargetEasyWinRate $TargetWinRateEasy `
    -TargetNormalWinRate $TargetWinRateNormal `
    -TargetHardWinRate $TargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
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

    for ($candidateIndex = 1; $candidateIndex -le $CandidatesPerIteration; $candidateIndex++) {
        $candidateId = "iter{0:d2}_cand{1:d2}" -f $iteration, $candidateIndex
        $candidateType = if ($candidateIndex -eq 1) { "carry_forward" } else { "mutated" }
        $candidateProfile = if ($candidateIndex -eq 1) {
            Clone-TuningProfile -Profile $globalBestProfile
        } else {
            New-MutatedTuningProfile -BaseProfile $globalBestProfile -Anneal $anneal -Strength $MutationStrength
        }

        $candidateTuningPath = Join-Path $iterDir "$candidateId.tuning.json"
        $candidateMetricsPath = Join-Path $iterDir "$candidateId.metrics.json"
        Write-TuningProfile -Profile $candidateProfile -OutputPath $candidateTuningPath

        Invoke-BotMetricsRun `
            -GodotExe $godotExe `
            -CommonPrefix $commonPrefix `
            -RunsPerEval $Runs `
            -StrategySet $strategySetNormalized `
            -TuningPath $candidateTuningPath `
            -MetricsOut $candidateMetricsPath `
            -Label "[3/8][$iteration/$Iterations] Evaluate $candidateId ($candidateType)"

        $candidatePayload = Read-MetricsPayload -MetricsPath $candidateMetricsPath
        $candidateScore = Get-MetricsScore `
            -Summary $candidatePayload.summary `
            -Runs $candidatePayload.runs `
            -TargetShare $TargetExplosionShare `
            -TargetShareTolerance $TargetExplosionShareTolerance `
            -TargetEasyWinRate $TargetWinRateEasy `
            -TargetNormalWinRate $TargetWinRateNormal `
            -TargetHardWinRate $TargetWinRateHard `
            -WinRateTolerance $TargetWinRateTolerance `
            -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
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
        }

        $iterCandidates += $candidateResult
        $history += $candidateResult
        Write-Host ("Candidate {0}: score={1:0.0000}, win(E/N/H)={2:P1}/{3:P1}/{4:P1}, wave={5:0.00}, explosion_share={6:P2}, surge_parity_ratio={7:0.00}" -f $candidateId, $candidateScore.Score, $candidateScore.EasyWinRate, $candidateScore.NormalWinRate, $candidateScore.HardWinRate, $candidateScore.AvgWave, $candidateScore.ExplosionShare, $candidateScore.TowerSurgeParityRatio)
    }

    $iterBest = $iterCandidates | Sort-Object -Property score -Descending | Select-Object -First 1
    if ($null -eq $iterBest) {
        throw "Iteration $iteration produced no candidates."
    }

    Write-Host "Iteration $iteration best: $($iterBest.candidate) score=$($iterBest.score)"

    if ([double]$iterBest.score -gt $globalBestScore) {
        $globalBestScore = [double]$iterBest.score
        $globalBestProfile = Normalize-TuningProfile -InputProfile (Get-Content -Raw $iterBest.tuning_file | ConvertFrom-Json)
        $globalBestTuningPath = $iterBest.tuning_file
        $globalBestMetricsPath = $iterBest.metrics_file
        $globalBestSource = $iterBest.candidate
        Write-Host "New global best found at iteration ${iteration}: $globalBestSource (score=$globalBestScore)"
    } else {
        Write-Host "Global best unchanged: $globalBestSource (score=$globalBestScore)"
    }

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

    $iterDeltaOut = Join-Path $iterDir "bot_metrics_delta.iter.txt"
    Invoke-GodotCommand -GodotExe $godotExe -Label "[6/8][$iteration/$Iterations] Delta baseline vs iteration best candidate" -Args (
        $commonPrefix + @("--metrics_delta", $baselineMetricsOut, $iterBest.metrics_file, "--delta_out", $iterDeltaOut)
    )
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

Invoke-BotMetricsRun -GodotExe $godotExe -CommonPrefix $commonPrefix -RunsPerEval $Runs -StrategySet $strategySetNormalized -TuningPath $bestTuningOut -MetricsOut $tunedMetricsOut -Label "[8/8] Final tuned bot metrics (global best)"

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
    -TargetEasyWinRate $TargetWinRateEasy `
    -TargetNormalWinRate $TargetWinRateNormal `
    -TargetHardWinRate $TargetWinRateHard `
    -WinRateTolerance $TargetWinRateTolerance `
    -TargetMaxTowerSurgeRatio $TargetMaxTowerSurgeRatio `
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
    strategy_set = $strategySetNormalized
    seed = $Seed
    scoring = [ordered]@{
        target_explosion_share = $TargetExplosionShare
        target_explosion_share_tolerance = $TargetExplosionShareTolerance
        target_win_rate_easy = $TargetWinRateEasy
        target_win_rate_normal = $TargetWinRateNormal
        target_win_rate_hard = $TargetWinRateHard
        target_win_rate_tolerance = $TargetWinRateTolerance
        target_max_tower_surge_ratio = $TargetMaxTowerSurgeRatio
        min_tower_placements_for_parity = $MinTowerPlacementsForParity
        max_chain_depth = $MaxChainDepth
        max_simultaneous_explosions = $MaxSimultaneousExplosions
        max_simultaneous_hazards = $MaxSimultaneousHazards
        max_simultaneous_hitstops = $MaxSimultaneousHitStops
        min_run_duration_seconds = $MinRunDurationSeconds
    }
    baseline = [ordered]@{
        metrics_file = $baselineMetricsOut
        score = $baselineScore.Score
        avg_wave = $baselineScore.AvgWave
        win_rate = $baselineScore.WinRate
        easy_win_rate = $baselineScore.EasyWinRate
        normal_win_rate = $baselineScore.NormalWinRate
        hard_win_rate = $baselineScore.HardWinRate
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
