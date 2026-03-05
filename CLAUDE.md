# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Slot Theory** is a constraint-driven drafting tower defense game by 7ants Studios. Core loop: Draft 1 of 5 options → wave runs automatically → repeat for 20 waves or lose. No mid-wave interaction, no economy, no meta progression.

Engine: **Godot 4.x .NET** (C#) · Runtime: **.NET 8+** · Target: **Windows (Steam)**

## Setup Requirements

- Install **Godot .NET build** (4.4+, not the standard build — required for C# support)
  - Executable: `E:\Godot\Godot_v4.6.1-stable_mono_win64_console.exe`
- Install **.NET SDK 8** on your machine (`.NET 10` also works — Godot targets `net8.0` in the `.csproj`)
- Use any IDE: Rider, Visual Studio, or VS Code
- Git ignore: `.godot/`, `.mono/`, `bin/`, `obj/`, exported builds

## Running From CLI

```bash
# Always build first — Godot does NOT auto-rebuild C# on CLI runs
dotnet build SlotTheory.sln

# Run in background, capture output, kill after N seconds
"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" --path "E:/SlotTheory" 2>&1 &
sleep 5; kill %1
```

**`--quit-after N` is FRAMES, not seconds.** At 60fps, `--quit-after 60` = 1 second. Use the background+kill approach above for timed test runs instead.

**Stale DLL gotcha:** When running Godot from CLI, it uses whatever `.dll` is in `.godot/mono/temp/bin/Debug/`. If you edit source files without running `dotnet build`, Godot will silently run the old code. Always `dotnet build` before a CLI run.

## Build & Export

- `<Nullable>enable</Nullable>` is set in `SlotTheory.csproj` — required to suppress nullable warnings in C# 8+ code
- Build via Godot's built-in build system (no separate CLI build tool); first time: Project → Tools → C# → Create C# Solution
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

**If export fails with "Failed to rename temporary file":** the previous `SlotTheory.exe` (or a leftover `SlotTheory.tmp`) is locked — close the game if it is running, then delete both files and re-run the export command:

```bash
rm -f "E:/SlotTheory/export/SlotTheory.tmp" "E:/SlotTheory/export/SlotTheory.exe"
```

## Bot Playtest Mode

Runs N fully-automated games headless for balance data. **Must pass `--scene` — the default startup scene is MainMenu, not Main.**

```bash
# Always dotnet build first
dotnet build SlotTheory.sln

"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
  --headless \
  --path "E:/SlotTheory" \
  --scene "res://Scenes/Main.tscn" \
  -- --bot --runs 100
```

- Output (strategy table, wave difficulty, tower/modifier usage) goes to **stdout** — captured directly by the shell.
- Bot log file also written to `C:/Users/kenny/AppData/Roaming/Godot/app_userdata/Slot Theory/logs/godot.log` (note the space in "Slot Theory" — different from the old "SlotTheory" directory).
- `SoundManager` auto-detects headless mode (`DisplayServer.GetName() == "headless"`) and skips all audio init, preventing the per-frame `GetFramesAvailable()` hang.
- `Targeting.SelectTarget(..., ignoreRange: BotMode)` skips range checks in bot mode (enemies' `GlobalPosition` is still updated via manual `Progress` increments in `BotTick`, but skipping range avoids misses on early-spawn enemies before they move).
- 4 strategies cycle round-robin: `Random`, `TowerFirst`, `GreedyDps`, `MarkerSynergy`.

## Testing

No test infrastructure exists yet. The TDD calls for:
- Pure C# unit tests for: targeting selection logic, damage pipeline math, modifier stacking math
- A developer debug overlay (wave count, spawned/alive counts, DPS estimate, targeting mode, Marked status counts)
- Optional "fast-forward" key for speeding through wave simulation during balancing

Python scripts (`Scripts/Tools/`) will handle content generation (JSON/YAML), balancing calculators, wave curve simulation, and modifier list generation.

## Data Consistency & Balance Updates

### Modifier Description Validation

**Problem:** Card tooltips can drift from implementation after balance changes. Example: Split Shot said "80% damage" but code implemented "65%" (from commit 622ef70 nerf).

**Solution:** `ModifierDataValidator.cs` runs on startup and validates modifier descriptions match their code implementations.

**How it works:**
1. Validator checks that key stat tokens appear in descriptions (e.g., "65%", "−30%", "×1.4")
2. Runs automatically during `DataLoader.LoadAll()`
3. Prints `[VALIDATOR] ✓ All descriptions match` or lists mismatches
4. Zero overhead once validated (no runtime checks after initial load)

**When making balance changes:**
1. Update the constant in `Balance.cs` (e.g., `SplitShotDamageRatio = 0.65f`)
2. Update the description in `Data/modifiers.json` to match (e.g., "65% damage each")
3. Run the game or bot test — validator will catch mismatches on startup
4. If validator fails, fix the description to match the constant

**To add a new modifier's validation check:**
1. Add entry to `ModifierDataValidator.cs` `expectations` list with expected tokens
2. Use exact text from description (e.g., "+50%", "−30%", "×1.4", "5 s")
3. Rerun to verify

## Hand-Written .tscn Rules

Godot `.tscn` files have strict patterns when writing by hand:

1. **`[Export] NodePath` properties** require `node_paths=PackedStringArray("PropName")` on the `[node ...]` line:
   ```
   [node name="GameController" type="Node" parent="." node_paths=PackedStringArray("LanePath")]
   LanePath = NodePath("../World/LanePath")
   ```
2. **`[Export] PackedScene?`** works directly — just `PropName = ExtResource("id")` under the node.
3. **Property names use PascalCase** in `.tscn` (matching C# property name exactly, not snake_case).
4. Unique node IDs (`unique_id=...`) are required on the root node; child nodes may omit them.
5. `[gd_scene format=3 uid="uid://..."]` header must match what Godot expects (Godot auto-generates UIDs on first open).
6. **Sibling `_Ready()` order = scene order (top to bottom).** If Node A's `_Ready()` needs Node B to already be initialized, B must appear before A in the `.tscn` file. Example: `DraftPanel` must be listed before `GameController` so its UI fields are initialized when GameController calls into it.

## Architecture

The game uses a **simulation-driven loop** — logic lives in C# systems, not scene node behaviors. All magic numbers go in `Balance.cs`.

Key constraints from the Getting Started guide:
- **Enemy movement**: `EnemyInstance` extends `PathFollow2D` — enemies self-move via `_Process()`. Do not move enemies manually in `CombatSim`.
- **Targeting list**: Maintained as `RunState.EnemiesAlive` — never scan the scene tree every frame.
- **Range checks**: Circular distance using `tower.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= tower.Range`.
- **No projectiles**: All tower attacks are hitscan (instant damage on attack, no projectile nodes).

### State Machine (GameController)
`GameController` drives the run lifecycle: DraftPhase → WavePhase → Win/Lose. `RunState` is the single source of truth for all runtime data.

### RunState owns:
- `int WaveIndex`
- `SlotInstance[] Slots` (size 6)
- `List<EnemyInstance> EnemiesAlive`
- `int EnemiesSpawnedThisWave`
- `float WaveTime`
- Optional: RNG seed for draft reproducibility

### Combat simulation order (per frame, in `CombatSim`):
1. Spawn enemies per wave interval until quota reached
2. Move enemies: `Progress += Speed * delta / LaneLength`
3. For each tower: reduce cooldown → acquire target via `Targeting.SelectTarget()` → run damage pipeline → reset cooldown
4. Remove dead enemies
5. Wave end: quota spawned AND no enemies alive
6. Loss: any enemy reaches `Progress >= 1.0` (immediate fail in v1)

### Damage pipeline (`DamageModel`):
Build a `DamageContext` (attacker, target, base damage, wave index) → apply modifier hooks in deterministic order:
1. Stat modifiers (`ModifyAttackInterval`, `ModifyDamage`) — apply to all hits (primary, chain, split)
2. Conditional modifiers (e.g., vs Marked)
3. On-hit effects (`OnHit`) — apply to all hits UNLESS modifier opts out via `ApplyToChainTargets => false` (used to prevent Overkill cascading; most modifiers like Chill Shot apply to chain/split targets)
4. On-kill effects (`OnKill`) — always run regardless of bounce type

### Modifier system:
- Data-driven via `modifiers.json` (id, name, desc, params); behavior is code-driven via `ModifierRegistry`
- No `if (modifierId == ...)` in tower code — modifiers implement the base interface
- Modifier interface: `ModifyAttackInterval`, `ModifyDamage`, `OnHit`, `OnKill`
- Modifiers keep private internal state where needed (e.g., Momentum tracks last target ID)
- Stacking: **additive within category** (no multiplicative explosion)

### Draft system rules (locked):
- Free slots exist → 5 options: 2 tower cards + 3 modifier cards
- All slots full → 4 modifier cards (scarcity intentional; `Balance.DraftModifierOptionsFull = 4`)
- Anti-brick: never offer a modifier if no tower can accept it (swap for applicable one)

### Targeting modes:
- **First**: highest `Progress` in range
- **Strongest**: highest current HP in range
- **Lowest HP**: lowest current HP in range

### Procedural Map System (`MapGenerator.cs`):
- **Grid**: 8 cols × 5 rows, cell 160×128 px, grid origin (0, 80)
- **Path**: Fixed 3-horizontal-leg snake, randomized turn rows/cols each run
  - `c1 ∈ [2,3]`, `c2 ∈ [c1+2,5]` — ensures cols 6–7 always have grass cells
- **Slot placement**: 6 zones (3×2 grid of zones), one slot per zone; prefers non-path cells adjacent to path (guaranteed in range), falls back to any grass cell
- **Rendering**: flat `ColorRect` nodes under `_mapVisuals` Node2D (first child of World); grass `#a6d608`, path `#8B5E3C`
- **Restart**: `Free()` (not `QueueFree()`) all `_mapVisuals` / slot node children to avoid one-frame flicker
- `System.Environment.TickCount` must be fully qualified — `Godot.Environment` exists in the same namespace

### Tower visual sub-nodes (created in `GameController.PlaceTower()`):
- `Polygon2D` — semi-transparent range circle (10% opacity)
- `ColorRect` — tower body square (color varies by tower type)
- `ColorRect` track + `ColorRect` fill — cooldown bar below square; fill width updated every frame in `TowerInstance._Process`
- `TargetModeIcon` - procedural targeting badge icon (right-arrow / star / down-arrow); updated via `TowerInstance.CycleTargetingMode()`
- All visual child nodes must have `MouseFilter = Control.MouseFilterEnum.Ignore` or `_Input` click events on the tower won't fire

### Click / tooltip system:
- Tower targeting cycle: `_Input` (not `_UnhandledInput`) + `GetViewport().GetMousePosition()` + 50×50 `Rect2` hit test
- Tooltip: `CanvasLayer(Layer=5)` → `Panel` → `Label`; sized to `label.GetMinimumSize() + (16,12)`; only visible during `GamePhase.Wave`

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
      RunState.cs           # Single source of truth
      DraftSystem.cs        # Generates 5 draft options
      WaveSystem.cs         # Wave config + spawn schedule + scaling
      Balance.cs            # All tunables in one place
    Combat/
      CombatSim.cs          # Step-by-step wave execution
      Targeting.cs          # Target selection logic
      DamageModel.cs        # Damage pipeline with modifier hooks
      Statuses.cs           # Status effect tracking (Marked)
    Entities/
      EnemyInstance.cs      # PathFollow2D, self-moves via _Process
      TowerInstance.cs      # Node2D, cooldown bar + targeting mode
      SlotInstance.cs
    Modifiers/
      Modifier.cs           # Base interface
      ModifierRegistry.cs   # JSON ID → concrete modifier object
      ModEvents.cs          # Event system for modifier interactions
    Tools/
      BotRunner.cs          # Orchestrates automated bot playtests (N runs, per-strategy tracking)
      ModifierDataValidator.cs # Validates tooltip text matches implementation constants
    Data/
      DataLoader.cs, Models.cs
    UI/
      DraftPanel.cs         # Full-screen overlay, 2-step pick flow
      HudPanel.cs           # Wave + lives display, speed controls
      EndScreen.cs          # Win/loss; left-click → MainMenu
      PauseScreen.cs        # Esc overlay; unpause / main menu
      MainMenu.cs           # Procedural dark main menu

  Data/
    towers.json, modifiers.json, waves.json
```

## V1 Scope Locks

These will NOT be in v1 — do not design toward them:
- Meta progression, unlocks, rarity tiers, shop/economy
- Tower moving, selling, or replacement
- Active enemy abilities
- Multiple lanes
- Rerolls or mid-wave decisions

If an idea requires a new system → defer to "Project 2."

## V1 Content

- **4 towers**: Rapid Shooter (fast/low dmg), Heavy Cannon (slow/high dmg), Marker Tower (applies Marked: +30% dmg for 2s), Arc Emitter (chains to 2 enemies, 260 px range, 60% decay/bounce)
- **10 modifiers**: Momentum (+8% dmg/hit same target, max ×1.4), Overkill (80% excess dmg spills to next enemy), Exploit Weakness (+50% dmg vs Marked), Focus Lens (+150% dmg, ×2 attack interval), Chill Shot (−30% enemy speed on hit, 5s), Overreach (+50% range, −15% dmg), Hair Trigger (+50% attack speed, −30% range), Split Shot (65% damage per copy, fires 2 projectiles), Feedback Loop (30% cooldown reduction on kill), Chain Reaction (60% damage per bounce, adds 1 more)
- **2 enemy types**: Basic Walker (65 HP wave 1, ×1.10/wave, 120px/s, 1 life); Armored Walker (4× HP, 60px/s, 2 lives, first appears wave 7)
- **20 waves**, 6 tower slots, max 3 modifiers per tower
