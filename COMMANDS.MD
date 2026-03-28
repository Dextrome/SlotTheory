# Slot Theory Command Reference

This file documents command surfaces currently present in the repository (scripts + runtime CLI flags), with examples.

Verified against source on 2026-03-28:
- `run_tuning_pipeline.ps1`
- root `*.ps1` and `*.sh` scripts
- `Scripts/Core/GameController.cs`
- `Scripts/Tools/CombatLab/*.cs`
- `Scripts/Tools/BotRunner.cs`
- `Scripts/Tools/BotPlayer.cs`
- `Scripts/Tools/IconExport.cs`
- `Scripts/Tools/ScreenshotPipeline.cs`

## 1. Quick Start Commands

### Build and test

```powershell
# Build solution
dotnet build .\SlotTheory.sln

# Run tests
dotnet test .\SlotTheory.Tests\
```

### Export binaries

```powershell
# Windows release export (bash script)
./build_release.sh

# Android debug AAB/APK helper scripts
powershell -ExecutionPolicy Bypass -File .\build_android_debug.ps1
./build_android_debug.sh

# Android release AAB
powershell -ExecutionPolicy Bypass -File .\build_android_release.ps1
```

### Run bots (headless)

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 120
```

### Run tuning pipeline

```powershell
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1
```

## 2. Godot Runtime Command Model

Godot uses two argument layers:

1. Engine args (before `--`): `--headless`, `--path`, `--scene`, etc.
2. Game/user args (after `--`): `--bot`, `--tuning_file`, `--lab_*`, etc.

Template:

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- <game-args>
```

For screenshot capture or visual auto-draft, do not use `--headless`.

## 3. Runtime Game Flags (Main Scene)

### 3.1 General flags

| Flag | Value | Notes |
| --- | --- | --- |
| `--demo` | none | Simulates demo mode (`Balance.IsDemo`). |
| `--tuning_file` | `<path>` | Loads tuning JSON from file. |
| `--dump_seed_tuning_json` | `<outputPath>` | Writes current tuning seed to JSON, then exits. |
| `--bot_unlocks` | none | Only matters with `--bot` in non-headless mode; bypasses progression locks. |

Examples:

```powershell
# Load explicit tuning profile
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --tuning_file "release\tuning_pipeline\latest\best_tuning.json"

# Dump current seed profile and exit
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --dump_seed_tuning_json "release\seed_current.json"
```

### 3.2 Bot mode flags

Enable bot mode with `--bot`.

| Flag | Value | Default | Notes |
| --- | --- | --- | --- |
| `--bot` | none | off | Enables bot automation. |
| `--runs` | int | `50` | Number of runs. |
| `--difficulty` | `easy|normal|hard` | all difficulties | Restricts difficulty. |
| `--map` | map id | map rotation | Restricts map. |
| `--strategy` | BotStrategy enum value | strategy set rotation | Forces single strategy. |
| `--strategy_set` | set name | `all` | Strategy pool label. |
| `--force_tower` | tower id | none | Forced lab drafting tower id. |
| `--force_mod` | modifier id | none | Forced lab drafting modifier id. |
| `--bot_metrics_out` | path | none | Writes bot metrics JSON. |
| `--bot_trace_out` | path | none | Writes trace JSON. |
| `--run_index_offset` | int | `0` | Run index offset for sharded runs. |
| `--run_offset` | int | `0` | Legacy alias for `--run_index_offset`. |
| `--bot_fast_metrics` | none | off | Compact/faster metrics payload. |
| `--bot_quiet` | none | off | Reduced console logging. |

Alias variants accepted in current code:
- `--bot-fast-metrics` (alias of `--bot_fast_metrics`)
- `--bot-quiet` (alias of `--bot_quiet`)

Bot strategy enum values (`--strategy`):
- `Random`
- `TowerFirst`
- `GreedyDps`
- `MarkerSynergy`
- `ChainFocus`
- `SplitFocus`
- `HeavyStack`
- `RiftPrismFocus`
- `SpectacleSingleStack`
- `AccordionEngine`
- `PlayerStyleKenny`
- `HeavyOverkill`

Bot strategy set values (`--strategy_set` in runtime bot mode):
- `all`
- `optimization`
- `edge`
- `spectacle`
- `top3`
- `opt` (alias for `optimization`)

Examples:

```powershell
# Baseline all-map, all-difficulty bot run
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 300

# Single difficulty + map + strategy set
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 120 --difficulty normal --map crossroads --strategy_set optimization

# Forced strategy enum
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 90 --strategy PlayerStyleKenny

# Forced tower/mod lab-style drafts
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 60 --force_tower chain_tower --force_mod blast_core

# Fast + quiet + sharded offset output
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 120 --strategy_set optimization --run_index_offset 240 --bot_fast_metrics --bot_quiet --bot_metrics_out "release\bot_metrics_shard3.json"

# Trace output
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 50 --bot_trace_out "release\bot_trace.json"
```

### 3.3 Tower surge benchmark bot mode

Enable with `--tower_surge_benchmark` (inside `--bot`).

| Flag | Value | Default | Notes |
| --- | --- | --- | --- |
| `--tower_surge_benchmark` | none | off | Turns on benchmark mode. |
| `--benchmark_slot` | int | `0` | Clamped to valid slot range. |
| `--benchmark_max_waves` | int | `10` | Minimum `1`. |
| `--benchmark_trials_per_tower` | int | `1` | Minimum `1`. |
| `--benchmark_mods` | `mod1,mod2,...` | `focus_lens,overkill,feedback_loop` | CSV list. |
| `--benchmark_towers` | `tower1,tower2,...` | all towers | CSV list; if omitted uses all tower ids. |

Alias variants accepted in current code:
- `--tower-surge-benchmark` (alias of `--tower_surge_benchmark`)
- `--benchmark-slot` (alias of `--benchmark_slot`)
- `--benchmark-max-waves` (alias of `--benchmark_max_waves`)
- `--benchmark-trials-per-tower` (alias of `--benchmark_trials_per_tower`)
- `--benchmark-mods` (alias of `--benchmark_mods`)
- `--benchmark-towers` (alias of `--benchmark_towers`)

Example:

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --tower_surge_benchmark --map crossroads --difficulty normal --benchmark_slot 2 --benchmark_max_waves 12 --benchmark_trials_per_tower 3 --benchmark_mods focus_lens,overkill,feedback_loop --benchmark_towers rapid_shooter,chain_tower,rift_prism --bot_metrics_out "release\tower_surge_benchmark.json"
```

### 3.4 Combat Lab flags

Any of these triggers lab mode and exits after run:
- `--lab_scenario <path>`
- `--lab_sweep <path>`
- `--lab_tower_benchmark <path>`
- `--lab_modifier_benchmark <path>`

Optional:
- `--lab_out <path>`

If multiple lab flags are passed, priority is:
1. `--lab_modifier_benchmark`
2. `--lab_tower_benchmark`
3. `--lab_scenario`
4. `--lab_sweep`

Examples:

```powershell
# Scenario suite
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --lab_scenario Data/combat_lab/core_scenarios.json --lab_out release/combat_lab_report.json

# Sweep
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --lab_sweep Data/combat_lab/sample_sweep.json --lab_out release/combat_lab_sweep.json

# Tower benchmark
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --lab_tower_benchmark Data/combat_lab/tower_benchmark_core.json --lab_out release/combat_lab_tower_benchmark.json

# Modifier benchmark
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --lab_modifier_benchmark Data/combat_lab/modifier_benchmark_core.json --lab_out release/combat_lab_modifier_benchmark.json
```

### 3.5 Metrics delta reporter flags

| Flag | Value | Notes |
| --- | --- | --- |
| `--metrics_delta` | `<baseline.json> <tuned.json>` | Required pair. |
| `--delta_out` | `<path>` | Optional text output path. |

Example:

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --metrics_delta release/bot_metrics_baseline.json release/bot_metrics_tuned.json --delta_out release/bot_metrics_delta.txt
```

### 3.6 Screenshot pipeline flags

| Flag | Value | Default | Notes |
| --- | --- | --- | --- |
| `--screenshot-capture` | none | off | Requires non-headless renderer. |
| `--screenshot_out` | path | `<project>/screenshots` | Output directory. |

Example:

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 20 --screenshot-capture --screenshot_out "E:\SlotTheory\release\screenshots"
```

### 3.7 Auto-draft visual mode flags

| Flag | Value | Default | Notes |
| --- | --- | --- | --- |
| `--auto-draft` | none | off | Visual AI drafting mode (not headless bot runner). |
| `--auto-runs` | int | `5` | Number of visual runs. |
| `--strategy` | BotStrategy value | `PlayerStyleKenny` | Bot drafting strategy. |
| `--map` | map id | existing pending selection | Preselects map. |

Example:

```powershell
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --auto-draft --auto-runs 10 --strategy ChainFocus --map hourglass
```

## 4. Map, Tower, Modifier ID Reference

Map ids (handcrafted):
- `orbit`, `crossroads`, `pinch_bleed`, `ridgeback`, `double_back`, `crossfire`, `threshold`, `switchback`, `ziggurat`, `hourglass`, `perimeter_lock`, `trident`

Special map ids:
- `tutorial`
- `random_map`

Tower ids (`--force_tower`, benchmark tower CSV):
- `rapid_shooter`, `heavy_cannon`, `marker_tower`, `chain_tower`, `rift_prism`, `accordion_engine`, `phase_splitter`, `rocket_launcher`, `undertow_engine`

Modifier ids (`--force_mod`, benchmark mod CSV):
- `momentum`, `overkill`, `exploit_weakness`, `focus_lens`, `slow`, `overreach`, `hair_trigger`, `split_shot`, `feedback_loop`, `chain_reaction`, `blast_core`, `wildfire`, `afterimage`, `reaper_protocol`

### 4.1 Map comparison workflows

Map-to-map comparison is done by running separate map-scoped bot metrics and comparing outputs.

```powershell
# Map A metrics
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 240 --difficulty normal --map crossroads --strategy_set optimization --bot_metrics_out "release\maps\crossroads.normal.json"

# Map B metrics
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --bot --runs 240 --difficulty normal --map ridgeback --strategy_set optimization --bot_metrics_out "release\maps\ridgeback.normal.json"

# Delta text report (map A vs map B)
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --scene "res://Scenes/Main.tscn" -- --metrics_delta "release\maps\crossroads.normal.json" "release\maps\ridgeback.normal.json" --delta_out "release\maps\crossroads_vs_ridgeback.txt"
```

Multi-map batch pattern (repeat `--map <id>`):
- `orbit`, `crossroads`, `pinch_bleed`, `ridgeback`, `double_back`, `crossfire`, `threshold`, `switchback`, `ziggurat`, `hourglass`, `perimeter_lock`, `trident`

## 5. Tuning Pipeline Script

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 [options]
```

Common examples:

```powershell
# Default
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1

# Demo tuning output (also copies final to Data/best_tuning_demo.json)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Demo

# Fast pass
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Runs 60 -Iterations 3 -CandidatesPerIteration 4 -SkipBuild -SkipTrace

# High parallelism
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -CandidateParallelism 2 -EvalShardParallelism 4

# Difficulty focused
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -DifficultyOnlyMode -Runs 180

# Normal-only curve adjustments
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -NormalOnlyMode -Runs 180

# Spectacle focused
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SpectacleOnlyMode -StrategySet spectacle

# Two-pass search
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -TwoPassMode -TwoPassPhaseSplitPercent 60 -TwoPassPass2RunsMultiplier 1.75

# Disable compact metrics or quiet mode (boolean parameters)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -UseFastBotMetrics:$false -UseBotQuiet:$false
```

All parameters (source of truth from script param block):

| Parameter | Alias | Type | Default |
| --- | --- | --- | --- |
| `-Runs` | - | `Int32` | `200` |
| `-Iterations` | TuningIterations | `Int32` | `4` |
| `-CandidatesPerIteration` | - | `Int32` | `3` |
| `-CandidateParallelism` | - | `Int32` | `1` |
| `-EvalShardParallelism` | - | `Int32` | `0` |
| `-SweepRunsPerVariant` | - | `Int32` | `12` |
| `-Seed` | - | `Int32` | `1337` |
| `-MutationStrength` | - | `Double` | `1.0` |
| `-TargetExplosionShare` | - | `Double` | `0.06` |
| `-TargetExplosionShareTolerance` | - | `Double` | `0.03` |
| `-TargetWinRateEasy` | - | `Double` | `0.95` |
| `-TargetWinRateNormal` | - | `Double` | `0.60` |
| `-TargetWinRateHard` | - | `Double` | `0.30` |
| `-TargetWinRateTolerance` | - | `Double` | `0.06` |
| `-TargetSurgesPerRun` | - | `Double` | `36.0` |
| `-TargetSurgesPerRunTolerance` | - | `Double` | `8.0` |
| `-MaxKillsPerSurge` | - | `Double` | `0.70` |
| `-MinGlobalSurgesPerRun` | - | `Double` | `1.20` |
| `-DifficultyRegressionTolerance` | - | `Double` | `0.01` |
| `-NormalRegressionPenaltyWeight` | - | `Double` | `260.0` |
| `-HardRegressionPenaltyWeight` | - | `Double` | `320.0` |
| `-TargetMaxModifierSurgeRatio` | - | `Double` | `2.2` |
| `-TargetTowerWinRateGap` | - | `Double` | `0.18` |
| `-TargetModifierWinRateGap` | - | `Double` | `0.20` |
| `-HardGuardMaxTowerWinRateGap` | - | `Double` | `0.35` |
| `-MinTowerRunsForFairness` | - | `Int32` | `40` |
| `-MinModifierRunsForFairness` | - | `Int32` | `50` |
| `-MinModifierRunsForSurgeParity` | - | `Int32` | `40` |
| `-MinSweepScoreRatioVsBaseline` | - | `Double` | `1.0` |
| `-MinTowerPlacementsForParity` | - | `Int32` | `6` |
| `-MaxChainDepth` | - | `Double` | `999.0` |
| `-MaxSimultaneousExplosions` | - | `Int32` | `999` |
| `-MaxSimultaneousHazards` | - | `Int32` | `999` |
| `-MaxSimultaneousHitStops` | - | `Int32` | `999` |
| `-MinRunDurationSeconds` | - | `Double` | `0.0` |
| `-TopCandidateReevalCount` | - | `Int32` | `3` |
| `-UseFastBotMetrics` | - | `Boolean` | `$true` |
| `-UseBotQuiet` | - | `Boolean` | `$true` |
| `-StrategySet` | - | `String` | `"optimization"` |
| `-GodotPath` | - | `String` | `""` |
| `-TuningFile` | - | `String` | `""` |
| `-ScenarioFile` | - | `String` | `"Data/combat_lab/core_scenarios.json"` |
| `-TowerBenchmarkFile` | - | `String` | `"Data/combat_lab/tower_benchmark_core.json"` |
| `-ModifierBenchmarkFile` | - | `String` | `"Data/combat_lab/modifier_benchmark_core.json"` |
| `-OutputRoot` | - | `String` | `"release/tuning_pipeline"` |
| `-SkipBuild` | - | `SwitchParameter` | off |
| `-SkipTrace` | - | `SwitchParameter` | off |
| `-SkipTowerBenchmark` | - | `SwitchParameter` | off |
| `-SkipModifierBenchmark` | - | `SwitchParameter` | off |
| `-FreezeSpectacleParams` | - | `SwitchParameter` | off |
| `-DifficultyOnlyMode` | - | `SwitchParameter` | off |
| `-NormalOnlyMode` | - | `SwitchParameter` | off |
| `-SpectacleOnlyMode` | - | `SwitchParameter` | off |
| `-TwoPassMode` | - | `SwitchParameter` | off |
| `-TwoPassPhaseSplitPercent` | - | `Int32` | `50` |
| `-TwoPassPass2RunsMultiplier` | - | `Double` | `1.5` |
| `-SkipAllStrategyValidation` | - | `SwitchParameter` | off |
| `-Demo` | - | `SwitchParameter` | off |

Validation constraints enforced by script:
- `-Runs >= 1`
- `-Iterations >= 1`
- `-CandidatesPerIteration >= 1`
- `-CandidateParallelism >= 1`
- `-SweepRunsPerVariant >= 1`
- `-MutationStrength > 0`
- `-TargetExplosionShareTolerance > 0`
- `-TargetWinRateTolerance > 0`
- `-TargetSurgesPerRunTolerance > 0`
- `-MaxKillsPerSurge >= 0`
- `-MinGlobalSurgesPerRun >= 0`
- `-DifficultyRegressionTolerance >= 0`
- `-NormalRegressionPenaltyWeight >= 0`
- `-HardRegressionPenaltyWeight >= 0`
- `-MinSweepScoreRatioVsBaseline >= 0`
- `-TargetMaxModifierSurgeRatio >= 1.0`
- `-TargetTowerWinRateGap >= 0`
- `-TargetModifierWinRateGap >= 0`
- `-HardGuardMaxTowerWinRateGap >= 0`
- `-TopCandidateReevalCount >= 0`
- `-MinTowerPlacementsForParity >= 1`
- `-MinTowerRunsForFairness >= 1`
- `-MinModifierRunsForFairness >= 1`
- `-MinModifierRunsForSurgeParity >= 1`
- `-TargetWinRateEasy/Normal/Hard` each in `[0,1]`
- `-TwoPassPhaseSplitPercent` in `[10,90]`
- `-TwoPassPass2RunsMultiplier >= 1.0`
- `-StrategySet` must be one of: `all`, `optimization`, `edge`, `spectacle`

Mode interactions:
- `-NormalOnlyMode` implies normal-only mutation and freezes hard parameters.
- `-DifficultyOnlyMode` and `-NormalOnlyMode` freeze spectacle mutation paths.
- `-SpectacleOnlyMode` freezes difficulty mutation paths.
- `-Demo` adds `--demo` to internal Godot invocations and copies final tuning to `Data/best_tuning_demo.json`.
- Without `-Demo`, final tuning copy target is `Data/best_tuning_full.json`.

## 6. Combat Lab Helper Script

Script:

```powershell
powershell -ExecutionPolicy Bypass -File .\Scripts\Tools\CombatLab\Export-SurgeFillKnobSuggestions.ps1 [options]
```

Parameters:

| Parameter | Type | Required | Default |
| --- | --- | --- | --- |
| `-BenchmarkReport` | `String` | yes | none |
| `-TuningFile` | `String` | yes | none |
| `-OutFile` | `String` | no | `""` |
| `-TargetSeconds` | `Double` | no | `0` |
| `-MaxStepFraction` | `Double` | no | `0.25` |

Examples:

```powershell
# Print JSON suggestions to stdout
powershell -ExecutionPolicy Bypass -File .\Scripts\Tools\CombatLab\Export-SurgeFillKnobSuggestions.ps1 -BenchmarkReport "release\combat_lab_tower_benchmark.json" -TuningFile "Data\best_tuning_full.json"

# Write suggestions to file
powershell -ExecutionPolicy Bypass -File .\Scripts\Tools\CombatLab\Export-SurgeFillKnobSuggestions.ps1 -BenchmarkReport "release\combat_lab_tower_benchmark.json" -TuningFile "Data\best_tuning_full.json" -OutFile "release\surge_knob_suggestions.json"

# Override target seconds and max step
powershell -ExecutionPolicy Bypass -File .\Scripts\Tools\CombatLab\Export-SurgeFillKnobSuggestions.ps1 -BenchmarkReport "release\combat_lab_tower_benchmark.json" -TuningFile "Data\best_tuning_full.json" -TargetSeconds 14.0 -MaxStepFraction 0.15
```

## 7. Build and Export Scripts (Repository Root)

These scripts currently take no CLI parameters; variations are done by editing script constants or composing with runtime flags.

| Script | Purpose | Run command |
| --- | --- | --- |
| `build_release.sh` | Build C# + Godot Windows Desktop release export | `./build_release.sh` |
| `build_android_debug.sh` | Build C# + Godot Android debug export | `./build_android_debug.sh` |
| `build_android_debug.ps1` | Windows PowerShell Android debug export wrapper | `powershell -ExecutionPolicy Bypass -File .\build_android_debug.ps1` |
| `build_android_release.ps1` | Windows PowerShell Android release AAB export wrapper | `powershell -ExecutionPolicy Bypass -File .\build_android_release.ps1` |
| `setup_godot_android.ps1` | Launches Godot and guides Android SDK path setup | `powershell -ExecutionPolicy Bypass -File .\setup_godot_android.ps1` |
| `install_android.ps1` | Converts AAB to APKS and installs universal APK via adb | `powershell -ExecutionPolicy Bypass -File .\install_android.ps1` |
| `sign_and_install_android.ps1` | Signs debug AAB/APKS and installs APK | `powershell -ExecutionPolicy Bypass -File .\sign_and_install_android.ps1` |
| `sign_and_install_android_release.ps1` | Builds release AAB, signs, installs release APK | `powershell -ExecutionPolicy Bypass -File .\sign_and_install_android_release.ps1` |
| `steam/push_build.sh` | Builds Steam export and uploads via SteamCMD | `./steam/push_build.sh` |

Notes:
- `install_android.ps1` currently checks for `export/SlotTheory.aab`.
- `build_android_debug.ps1` currently exports `export/SlotTheory_ui_fixed.aab`.

Direct export commands (without wrappers):

```powershell
# Windows export
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --export-release "Windows Desktop" "E:\SlotTheory\export\SlotTheory.exe"

# Steam export preset
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --export-release "Windows Desktop (Steam)" "E:\SlotTheory\export\steam_build\SlotTheory.exe"

# Android debug export
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --export-debug "Android" "E:\SlotTheory\export\SlotTheory_ui_fixed.aab"

# Android release export
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "E:\SlotTheory" --export-release "Android" "E:\SlotTheory\export\SlotTheory_release.aab"
```

## 8. Steam and Assets Commands

### Steam upload

```bash
./steam/push_build.sh
```

Direct SteamCMD variants:

```powershell
# Main app build VDF
steamcmd +login <steam_user> +run_app_build "E:\SlotTheory\steam\app_build.vdf" +quit

# Demo app build VDF
steamcmd +login <steam_user> +run_app_build "E:\SlotTheory\steam\app_build_demo.vdf" +quit
```

### Icon export scene

```powershell
dotnet build SlotTheory.sln
& "E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe" --path "E:\SlotTheory" --scene "res://Scenes/IconExport.tscn"
```

Do not use `--headless` for icon export.

### Android Gradle wrapper (generated by Godot Android template)

```powershell
# Run Gradle tasks from the generated Android build project
cd E:\SlotTheory\android\build
.\gradlew.bat tasks
.\gradlew.bat assembleDebug
.\gradlew.bat assembleRelease
```

Note:
- `android/build/gradlew.bat` is generated tooling from Godot's Android export template.
- Available task variations are standard Gradle task variants (`tasks`, `assemble*`, `bundle*`, etc.).

## 9. Utility and Maintenance Scripts (Internal)

These are repo surgery/debug helpers and are not part of normal build pipeline.

| Script | Purpose | Run command |
| --- | --- | --- |
| `tmp_typecheck.ps1` | Attempts to load built assembly and type-check a specific type | `powershell -ExecutionPolicy Bypass -File .\tmp_typecheck.ps1` |
| `inspect_gc.ps1` | Byte-level inspection around bot code in `GameController.cs` | `powershell -ExecutionPolicy Bypass -File .\inspect_gc.ps1` |
| `inspect2.ps1` | Byte-level inspection around combat sim creation snippet | `powershell -ExecutionPolicy Bypass -File .\inspect2.ps1` |
| `inspect3.ps1` | Byte-level inspection around boot log snippet | `powershell -ExecutionPolicy Bypass -File .\inspect3.ps1` |
| `patch_gc.ps1` | Legacy scripted text patch for `GameController.cs` bot logic | `powershell -ExecutionPolicy Bypass -File .\patch_gc.ps1` |
| `patch_gc2.ps1` | Legacy scripted text patch for extra wave-1 picks | `powershell -ExecutionPolicy Bypass -File .\patch_gc2.ps1` |
| `patch_polish.ps1` | Legacy scripted text patch for UI/polish hooks | `powershell -ExecutionPolicy Bypass -File .\patch_polish.ps1` |

## 10. Android Setup Related External Commands

Used in setup docs (`ANDROID_SETUP.md` / `ANDROID_TEMPLATE_INSTALL.md`):

```powershell
mkdir C:\Android\sdk
cd C:\Android\cmdline-tools\latest
bin\sdkmanager --install "platforms;android-34" --sdk_root=C:\Android\sdk
bin\sdkmanager --install "build-tools;34.0.0" --sdk_root=C:\Android\sdk
bin\sdkmanager --install "platform-tools" --sdk_root=C:\Android\sdk
bin\sdkmanager --install "cmake;3.22.1" --sdk_root=C:\Android\sdk
bin\sdkmanager --install "ndk;25.1.8937393" --sdk_root=C:\Android\sdk
java -version
javac -version
```

## 11. Notes On "All Variations"

- All runtime flags can be combined in one invocation as long as they are not mutually exclusive in code path.
- Short-circuit priority in startup:
  1. `--dump_seed_tuning_json`
  2. `--metrics_delta`
  3. any `--lab_*`
  4. normal gameplay/bot/screenshot/auto-draft flow
- `run_tuning_pipeline.ps1` is itself an orchestrator that emits many internal Godot commands (`--bot`, `--lab_*`, `--metrics_delta`, `--bot_trace_out`) in different combinations.
- If you need this file generated as a machine-readable command schema (JSON/YAML), it can be derived from this same source set.
