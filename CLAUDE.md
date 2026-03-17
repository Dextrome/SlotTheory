# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Slot Theory** is a constraint-driven drafting tower defense game by 7ants Studios. Core loop: Draft 1 of 5 options â†’ wave runs automatically â†’ repeat for 20 waves or lose. No mid-wave interaction, no economy, no meta progression.

Engine: **Godot 4.x .NET** (C#) Â· Runtime: **.NET 8+** Â· Target: **Windows (Steam)**

## Since v1.0.8 (Documentation Baseline)

- Spectacle combat system is integrated into runtime gameplay and bot simulations.
- Spectacle analytics are now reported in bot summaries (trigger tier totals and top effects).
- Tower tooltip now shows all possible spectacle outcomes for current supported modifier loadout.
- New spectacle-focused bot draft strategies were added:
  - `SpectacleSingleStack`
  - `SpectacleComboPairing`
  - `SpectacleTriadDiversity`
- Minor spectacle triggers were removed; balancing should target surge/global surge spectacle cadence and effects.
- **Surge differentiation system** added: `SurgeDifferentiation.cs` (pure-logic, no Godot deps) is the single source of truth for global surge archetypes (10 named labels mapped from dominant contributing mod, e.g. REDLINE WAVE, CHAIN STORM). Global surge banner shows dynamic label; visual feel (Detonation/Pressure/Neutral) drives flash alpha, secondary pulses, and ripple pattern. Multi-color ripples reflect top-3 contributing mods. Per-tower archetype FX fire in sequence. 35 unit tests in `SurgeDifferentiationTests`.
- **Automated tuning pipeline** added: `run_tuning_pipeline.ps1` + `Scripts/Tools/CombatLab/` drive iterative difficulty optimization against bot win-rate targets. `SpectacleTuning.Current` overrides difficulty multipliers at runtime.
- **Achievement system** added: `AchievementManager.cs`, `AchievementDefinition.cs`, `SteamAchievements.cs`. Achievements persist across sessions and gate unlocks.
- **Unlockable content**: Arc Emitter, Split Shot modifier, and Rift Prism are now gated behind campaign map clears (`Unlocks.cs`). Bots always have full unlock access for deterministic balance testing.
- **Three difficulty modes**: Easy (no scaling), Normal (~75% bot win target), Hard (~50% bot win target). Multipliers are tunable via `SpectacleTuning.Current`.
- **Enemy render pipeline overhaul**: layered render pipeline with perf controls, class-specific death FX, and mobile-adaptive quality settings.
- **New UI screens**: `UnlockRevealScreen.cs`, `AchievementsPanel.cs`, `AchievementToast.cs`, `SlotCodexPanel.cs`.
- **Supabase leaderboard service** added alongside Steam leaderboards (`SupabaseLeaderboardService.cs`, `SupabaseConfig.cs`).

## Setup Requirements

- Install **Godot .NET build** (4.4+, not the standard build â€” required for C# support)
  - Executable: `E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe`
- Install **.NET SDK 8** on your machine (`.NET 10` also works â€” Godot targets `net8.0` in the `.csproj`)
- Use any IDE: Rider, Visual Studio, or VS Code
- Git ignore: `.godot/`, `.mono/`, `bin/`, `obj/`, exported builds

## Running From CLI

```bash
# Always build first â€” Godot does NOT auto-rebuild C# on CLI runs
dotnet build SlotTheory.sln

# Run in background, capture output, kill after N seconds
"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" --path "E:/SlotTheory" 2>&1 &
sleep 5; kill %1
```

**`--quit-after N` is FRAMES, not seconds.** At 60fps, `--quit-after 60` = 1 second. Use the background+kill approach above for timed test runs instead.

**Stale DLL gotcha:** When running Godot from CLI, it uses whatever `.dll` is in `.godot/mono/temp/bin/Debug/`. If you edit source files without running `dotnet build`, Godot will silently run the old code. Always `dotnet build` before a CLI run.

## Build & Export

- `<Nullable>enable</Nullable>` is set in `SlotTheory.csproj` â€” required to suppress nullable warnings in C# 8+ code
- Build via Godot's built-in build system (no separate CLI build tool); first time: Project â†’ Tools â†’ C# â†’ Create C# Solution
- .NET runtime packaging is handled by Godot's export preset

### Exporting from CLI

```bash
# Always dotnet build first, then export
dotnet build SlotTheory.sln

"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
  --path "E:/SlotTheory" \
  --export-release "Windows Desktop" \
  "E:/SlotTheory/export/SlotTheory.exe" \
  --headless
```

**If export fails with "Failed to rename temporary file":** the previous `SlotTheory.exe` (or a leftover `SlotTheory.tmp`) is locked â€” close the game if it is running, then delete both files and re-run the export command:

```bash
rm -f "E:/SlotTheory/export/SlotTheory.tmp" "E:/SlotTheory/export/SlotTheory.exe"
```

## Bot Playtest Mode

Runs N fully-automated games headless for balance data. **Must pass `--scene` â€” the default startup scene is MainMenu, not Main.**

```bash
# Always dotnet build first
dotnet build SlotTheory.sln

"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
  --headless \
  --path "E:/SlotTheory" \
  --scene "res://Scenes/Main.tscn" \
  -- --bot --runs 100
```

- Output (strategy table, wave difficulty, tower/modifier usage) goes to **stdout** â€” captured directly by the shell.
- Bot log file also written to `C:/Users/kenny/AppData/Roaming/Godot/app_userdata/Slot Theory/logs/godot.log` (note the space in "Slot Theory" â€” different from the old "SlotTheory" directory).
- `SoundManager` auto-detects headless mode (`DisplayServer.GetName() == "headless"`) and skips all audio init, preventing the per-frame `GetFramesAvailable()` hang.
- Range checks are **fully enforced** in bot mode (`ignoreRange: false` hardcoded in `CombatSim.Step`). Enemy `GlobalPosition` is correctly updated by `PathFollow2D` whenever `Progress` is set in `BotTick`.
- Spectacle gameplay payloads are applied in bot mode for surge/global surge triggers (matching live gameplay logic).
- Minor spectacle trigger tier no longer exists, so surge/global surge metrics are the primary balancing signal.
- 12 strategies cycle round-robin: `Random`, `TowerFirst`, `GreedyDps`, `MarkerSynergy`, `ChainFocus`, `SplitFocus`, `HeavyStack`, `RiftPrismFocus`, `SpectacleSingleStack`, `SpectacleComboPairing`, `SpectacleTriadDiversity`, `PlayerStyleKenny`.
- Bot strategy sets: pass `--strategy-set optimization` (8 strategies focused on win-rate) or `--strategy-set edge` (3 edge-case strategies) to scope runs. Default (`all`) cycles all 12.
- Run `run_tuning_pipeline.ps1` for automated iterative tuning (generates seed from current `SpectacleTuning`, runs bot eval + scenario suite, outputs best profile).

## Testing

`SlotTheory.Tests` (xUnit) is active and references the prebuilt game DLL.

- Always build game code first:
  - `dotnet build SlotTheory.sln`
- Then run tests:
  - `dotnet test SlotTheory.Tests/SlotTheory.Tests.csproj`
- Useful targeted run:
  - `dotnet test SlotTheory.Tests/SlotTheory.Tests.csproj --filter "RunStateTests|SpectacleSystemTests|SurgeDifferentiationTests"`

`DraftSystem` anti-brick coverage remains critical because a mismapped modifier can silently brick drafts.

Python scripts (`Scripts/Tools/`) can still be used for content generation (JSON/YAML), balancing calculators, wave curve simulation, and modifier list generation.

## Data Consistency & Balance Updates

### Modifier Description Validation

**Problem:** Card tooltips can drift from implementation after balance changes.

**Solution:** `ModifierDataValidator.cs` runs on startup and validates modifier descriptions match their code implementations.

**How it works:**
1. Validator checks that key stat tokens appear in descriptions (e.g., "42%", "âˆ’25%", "Ã-1.80")
2. Runs automatically during `DataLoader.LoadAll()`
3. Prints `[VALIDATOR] OK All modifier descriptions match implementation` or lists mismatches
4. Zero overhead once validated (no runtime checks after initial load)

**When making balance changes:**
1. Update the constant in `Balance.cs` (e.g., `SplitShotDamageRatio = 0.42f`)
2. Update the description in `Data/modifiers.json` to match (e.g., "42% damage each")
3. Run the game or bot test â€” validator will catch mismatches on startup
4. If validator fails, fix the description to match the constant

**To add a new modifier's validation check:**
1. Add entry to `ModifierDataValidator.cs` `expectations` list with expected tokens
2. Use exact text from description (e.g., "+40%", "âˆ’25%", "Ã-1.80", "5 s")
3. Rerun to verify

## Hand-Written .tscn Rules

Godot `.tscn` files have strict patterns when writing by hand:

1. **`[Export] NodePath` properties** require `node_paths=PackedStringArray("PropName")` on the `[node ...]` line:
   ```
   [node name="GameController" type="Node" parent="." node_paths=PackedStringArray("LanePath")]
   LanePath = NodePath("../World/LanePath")
   ```
2. **`[Export] PackedScene?`** works directly â€” just `PropName = ExtResource("id")` under the node.
3. **Property names use PascalCase** in `.tscn` (matching C# property name exactly, not snake_case).
4. Unique node IDs (`unique_id=...`) are required on the root node; child nodes may omit them.
5. `[gd_scene format=3 uid="uid://..."]` header must match what Godot expects (Godot auto-generates UIDs on first open).
6. **Sibling `_Ready()` order = scene order (top to bottom).** If Node A's `_Ready()` needs Node B to already be initialized, B must appear before A in the `.tscn` file. Example: `DraftPanel` must be listed before `GameController` so its UI fields are initialized when GameController calls into it.

## Architecture

The game uses a **simulation-driven loop** â€” logic lives in C# systems, not scene node behaviors. All magic numbers go in `Balance.cs`.

Key constraints from the Getting Started guide:
- **Enemy movement**: `EnemyInstance` extends `PathFollow2D` â€” enemies self-move via `_Process()`. Do not move enemies manually in `CombatSim`.
- **Targeting list**: Maintained as `RunState.EnemiesAlive` â€” never scan the scene tree every frame.
- **Range checks**: Circular distance using `tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range`.
- **No projectiles**: All tower attacks are hitscan (instant damage on attack, no projectile nodes).

### State Machine (GameController)
`GameController` drives the run lifecycle: DraftPhase â†’ WavePhase â†’ Win/Lose. `RunState` is the single source of truth for all runtime data.

### RunState owns:
- `int WaveIndex`
- `SlotInstance[] Slots` (size 6)
- `List<EnemyInstance> EnemiesAlive`
- `int EnemiesSpawnedThisWave`
- `float WaveTime`
- Optional: RNG seed for draft reproducibility

### Combat simulation order (per frame, in `CombatSim`):
1. Spawn enemies per wave interval until quota reached
2. Move enemies: enemies self-move via `EnemyInstance._Process()` â€” `Progress += Speed * delta` (PathFollow2D absolute pixel offset; no LaneLength division)
3. For each tower: reduce cooldown â†’ acquire target via `Targeting.SelectTarget()` â†’ run damage pipeline â†’ reset cooldown
4. Remove dead enemies
5. Wave end: quota spawned AND no enemies alive
6. Loss: player `Lives` reaches 0 â€” each enemy that exits (`ProgressRatio >= 1.0`) costs 1 life (`Balance.StartingLives = 10`)

### Damage pipeline (`DamageModel`):
Build a `DamageContext` (attacker, target, base damage, wave index) â†’ apply modifier hooks in deterministic order:
1. Stat modifiers (`ModifyAttackInterval`, `ModifyDamage`) â€” apply to all hits (primary, chain, split)
2. Conditional modifiers (e.g., vs Marked)
3. On-hit effects (`OnHit`) â€” apply to all hits UNLESS modifier opts out via `ApplyToChainTargets => false` (used to prevent Overkill cascading; most modifiers like Chill Shot apply to chain/split targets)
4. On-kill effects (`OnKill`) â€” always run regardless of bounce type

### Modifier system:
- Data-driven via `modifiers.json` (id, name, desc, params); behavior is code-driven via `ModifierRegistry`
- No `if (modifierId == ...)` in tower code â€” modifiers implement the base interface
- Modifier interface: `ModifyAttackInterval`, `ModifyDamage`, `OnHit`, `OnKill`
- Modifiers keep private internal state where needed (e.g., Momentum tracks last target ID)
- Stacking: **additive within category** (no multiplicative explosion)

### Draft system rules (locked):
- Free slots exist â†’ 5 options: 2 tower cards + 3 modifier cards
- All slots full â†’ 4 modifier cards (scarcity intentional; `Balance.DraftModifierOptionsFull = 4`)
- Anti-brick: never offer a modifier if no tower can accept it (swap for applicable one)

### Targeting modes:
- **First**: highest `Progress` in range
- **Strongest**: highest current HP in range
- **Lowest HP**: lowest current HP in range
- **Rift Sapper UI mapping** (tower-specific labels/icons):
  - `First` => `Random` (die icon)
  - `Strongest` => `Closest` (down-arrow icon)
  - `Lowest HP` => `Furthest` (up-arrow icon)

### Rift Sapper combat model (`rift_prism`):
- Rift Sapper is a trap/setup tower; it does not need a current enemy target to act.
- Plants lane mines at anchor points in range using tower-specific targeting mode semantics.
- Active mine cap per tower: `Balance.RiftMineMaxActivePerTower` (currently 7).
- Mines are charge-based:
  - `Balance.RiftMineChargesPerMine` (currently 3) charges per mine.
  - Non-final triggers use `Balance.RiftMineTickDamageMultiplier` (currently 0.65).
  - Final trigger uses `Balance.RiftMineFinalDamageMultiplier` (currently 1.15).
  - Per-trigger lockout: `Balance.RiftMineRetriggerDelay` (currently 0.18s).
- Wave-start seeding burst:
  - During first `Balance.RiftMineBurstWindow` seconds (currently 2.4s), plant interval is multiplied by `Balance.RiftMineBurstIntervalMultiplier` (currently 0.55).
  - Burst acceleration is capped by `Balance.RiftMineBurstFastPlantsPerTower` (currently +3 accelerated plants per tower per wave).
- Modifier behavior on mines:
  - Split Shot and chain-propagation effects trigger on final-charge pops only.
  - Chain-triggered mine pops force the target mine into a final pop to preserve cascade behavior.

### Procedural Map System (`MapGenerator.cs`):
- **Grid**: 8 cols Ã- 5 rows, cell 160Ã-128 px, grid origin (0, 80)
- **Path**: Fixed 3-horizontal-leg snake, randomized turn rows/cols each run
  - `c1 âˆˆ [2,3]`, `c2 âˆˆ [c1+2,5]` â€” ensures cols 6â€“7 always have grass cells
- **Slot placement**: 6 zones (3Ã-2 grid of zones), one slot per zone; prefers non-path cells adjacent to path (guaranteed in range), falls back to any grass cell
- **Rendering**: flat `ColorRect` nodes under `_mapVisuals` Node2D (first child of World); grass `#a6d608`, path `#8B5E3C`
- **Restart**: `Free()` (not `QueueFree()`) all `_mapVisuals` / slot node children to avoid one-frame flicker
- `System.Environment.TickCount` must be fully qualified â€” `Godot.Environment` exists in the same namespace

### Tower visual sub-nodes (created in `GameController.PlaceTower()`):
- `Polygon2D` â€” semi-transparent range circle (10% opacity)
- `ColorRect` â€” tower body square (color varies by tower type)
- `ColorRect` track + `ColorRect` fill â€” cooldown bar below square; fill width updated every frame in `TowerInstance._Process`
- `TargetModeIcon` - procedural targeting badge icon (right-arrow / star / down-arrow); updated via `TowerInstance.CycleTargetingMode()`
- All visual child nodes must have `MouseFilter = Control.MouseFilterEnum.Ignore` or `_Input` click events on the tower won't fire

### Click / tooltip system:
- Tower targeting cycle: `_Input` (not `_UnhandledInput`) + `GetViewport().GetMousePosition()` + 50Ã-50 `Rect2` hit test
- Tooltip: `CanvasLayer(Layer=5)` â†’ `Panel` â†’ `Label`; sized to `label.GetMinimumSize() + (16,12)`; only visible during `GamePhase.Wave`

## Current Folder Layout

```
res://
  Scenes/
    Main.tscn               # Root: GameController + UIRoot + World
    MainMenu.tscn           # Start screen
    UI/
      DraftPanel.tscn, HUDPanel.tscn, EndScreen.tscn, PauseScreen.tscn

  Scripts/
    Core/
      GameController.cs     # Run lifecycle state machine + visual setup
      MapGenerator.cs       # Procedural snake-path map (pure C#)
      Map.cs                # Static map support
      RunState.cs           # Single source of truth
      DraftSystem.cs        # Generates 5 draft options
      WaveSystem.cs         # Wave config + spawn schedule + scaling
      Balance.cs            # All tunables in one place
      SettingsManager.cs    # Difficulty mode + persistent settings
      SoundManager.cs       # Audio; auto-disables in headless mode
      SpectacleSystem.cs    # Surge/global-surge trigger evaluation
      SpectacleTuning.cs    # Runtime tuning profile (overrides difficulty multipliers)
      SpectacleDefinitions.cs
      SurgeDifferentiation.cs   # Pure-logic global surge label/feel table (no Godot deps); single source of truth for archetypes
      SpectacleExplosionCore.cs
      SpectacleDamageCore.cs
      SpectacleDamageSource.cs
      AchievementManager.cs # Persistent achievement tracking; gates unlocks
      AchievementDefinition.cs
      SteamAchievements.cs
      SteamCloudSync.cs
      Unlocks.cs            # Content unlock gates (Arc Emitter, Split Shot, Rift Prism)
      PathFlow.cs
      Transition.cs
      UITheme.cs
      GridBackground.cs
      MobileInput.cs
      MobileOptimization.cs
      MobileRunSession.cs
      MapDecorationLayer.cs
      WorldAtmosphere.cs
      EnemyRenderPerfProfiler.cs
      EnemyRenderSettingsSnapshot.cs
      PinchZoomHandler.cs
      Leaderboards/
        ILeaderboardService.cs
        LeaderboardKey.cs
        LeaderboardModels.cs
        LeaderboardManager.cs
        ScoreCalculator.cs
        BuildSnapshotCodec.cs
        SteamLeaderboardService.cs
        NullLeaderboardService.cs
        SupabaseLeaderboardService.cs
        SupabaseConfig.cs
      Naming/
        RunNameGenerator.cs
        RunNameProfile.cs
      HighScoreManager.cs
    Combat/
      CombatSim.cs          # Step-by-step wave execution
      Targeting.cs          # Target selection logic
      DamageModel.cs        # Damage pipeline with modifier hooks
      Statuses.cs           # Status effect tracking (Marked, Slow)
    Entities/
      EnemyInstance.cs      # PathFollow2D, self-moves via _Process
      TowerInstance.cs      # Node2D, cooldown bar + targeting mode
      SlotInstance.cs
      DeathBurst.cs
      ChainArc.cs
      ProjectileVisual.cs
      TargetAcquirePing.cs
      DamageNumber.cs
      CombatCallout.cs
      RiftMineVisual.cs
      RiftMineBurst.cs
      GlobalSurgeRipple.cs
      SpectacleSkinGlyph.cs
      ExplosionResidueZoneFx.cs
      ImpactSparkBurst.cs
      EnemyRenderState.cs
      EnemyRenderStyle.cs
      EnemyVisualArchetype.cs
      EnemyVisualProfile.cs
      EnemyRenderLayerSettings.cs
      EnemyRenderDebugCounters.cs
      IEnemyView.cs
      ITowerView.cs
    Modifiers/
      Modifier.cs           # Base interface
      ModifierRegistry.cs   # JSON ID â†’ concrete modifier object
      ModEvents.cs          # Event system for modifier interactions
      (one .cs per modifier: Momentum, Overkill, ExploitWeakness, FocusLens,
       Slow, Overreach, HairTrigger, SplitShot, FeedbackLoop, ChainReaction)
    Tools/
      BotRunner.cs          # Orchestrates automated bot playtests (N runs, per-strategy tracking)
      BotPlayer.cs
      ModifierDataValidator.cs # Validates tooltip text matches implementation constants
      IconBgNode.cs, IconExport.cs
      CombatLab/
        CombatLabCli.cs       # CLI entry for scenario/sweep runs
        CombatLabScenarioRunner.cs
        CombatLabModels.cs
        SpectacleTuningLoader.cs
        BotMetricsDeltaReporter.cs # Compares baseline vs tuned bot metrics
    Data/
      DataLoader.cs, Models.cs
    UI/
      DraftPanel.cs         # Full-screen overlay, 2-step pick flow
      HudPanel.cs           # Wave + lives display, speed controls
      EndScreen.cs          # Win/loss; left-click â†’ MainMenu
      PauseScreen.cs        # Esc overlay; unpause / main menu
      MainMenu.cs           # Procedural dark main menu
      MapSelectPanel.cs
      LeaderboardsMenu.cs
      Settings.cs
      HowToPlay.cs
      ModifierIcon.cs
      ModifierVisuals.cs
      TargetModeIcon.cs
      TowerIcon.cs
      TouchScrollHelper.cs
      DraftBackdropFx.cs
      NeonGridBg.cs
      UnlockRevealScreen.cs # Shown on first map-clear unlock
      AchievementsPanel.cs
      AchievementToast.cs
      SlotCodexPanel.cs

  Data/
    towers.json, modifiers.json, waves.json, maps.json
```

## V1 Scope Locks

These will NOT be in v1 â€” do not design toward them:
- Meta progression, unlocks, rarity tiers, shop/economy
- Tower moving, selling, or replacement
- Active enemy abilities
- Multiple lanes
- Rerolls or mid-wave decisions

If an idea requires a new system â†’ defer to "Project 2."

## V1 Content

- **5 towers**: Rapid Shooter (fast/low dmg), Heavy Cannon (slow/high dmg), Marker Tower (applies Marked: +40% dmg taken, 4s), Arc Emitter (**unlockable** via campaign map clear; chains to 2 enemies, 400 px chain range, 60% decay/bounce), Rift Sapper (**unlockable** via campaign map clear; charged lane-mine trap tower with wave-start seeding burst)
- **10 modifiers** (always check `Balance.cs` + `modifiers.json` for current values â€” these drift after balance passes):
  - Momentum: +16% dmg/hit same target, max Ã-1.80
  - Overkill: 60% excess dmg spills to next enemy
  - Exploit Weakness: +45% dmg vs Marked enemies
  - Focus Lens: +140% dmg, Ã-1.85 attack interval
  - Chill Shot: âˆ’30% enemy speed on hit, 6s (stacks multiplicatively per tower)
  - Overreach: +45% range, âˆ’10% dmg
  - Hair Trigger: +30% attack speed, âˆ’18% range
  - Split Shot: fires 2 projectiles at nearby enemies for 35% damage each (search radius 280px); **unlockable** via campaign map clear
  - Feedback Loop: 65% of remaining cooldown removed on kill
  - Chain Reaction: adds 1 bounce (60% decay/bounce); stacks add more bounces
- **3 enemy types**:
  - Basic Walker: 65 HP wave 1, Ã-1.10/wave, 120px/s
  - Armored Walker: 3.5Ã- HP, 60px/s, first appears wave 6 (index 5)
  - Swift Walker: 1.5Ã- HP, 240px/s, appears waves 10â€“19 (skips wave 12 and 20)
- **10 player lives** â€” each leaked enemy costs 1 life (`Balance.StartingLives = 10`)
- **20 waves**, 6 tower slots, max 3 modifiers per tower
- **Extra draft picks**: `Balance.Wave1ExtraPicks` and `Balance.Wave15ExtraPicks` are both currently 0 (temporarily disabled)
- **Difficulty modes**: Easy (no scaling), Normal (tuned ~75% bot win), Hard (tuned ~50% bot win). Exact multipliers are in `Balance.DifficultyMultipliers` and overridable at runtime via `SpectacleTuning.Current`
- **Endless mode**: After wave 20 win, “Continue — Endless” button on win screen continues the run from wave 21. Each endless wave: +5% enemy count (compounding), +2% enemy HP (compounding), every 5 waves +1 Swift Walker. Global win score is only submitted if the player does NOT continue to endless (endless loss replaces it).

