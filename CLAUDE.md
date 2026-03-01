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
- Export: Godot → Export Presets → Windows x64
- .NET runtime packaging is handled by Godot's export preset

## Testing

No test infrastructure exists yet. The TDD calls for:
- Pure C# unit tests for: targeting selection logic, damage pipeline math, modifier stacking math
- A developer debug overlay (wave count, spawned/alive counts, DPS estimate, targeting mode, Marked status counts)
- Optional "fast-forward" key for speeding through wave simulation during balancing

Python scripts (`Scripts/Tools/`) will handle content generation (JSON/YAML), balancing calculators, wave curve simulation, and modifier list generation.

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
1. Stat modifiers (`ModifyAttackInterval`, `ModifyDamage`)
2. Conditional modifiers (e.g., vs Marked)
3. On-hit effects (apply Mark, resolve Overkill spill)

### Modifier system:
- Data-driven via `modifiers.json` (id, name, desc, params); behavior is code-driven via `ModifierRegistry`
- No `if (modifierId == ...)` in tower code — modifiers implement the base interface
- Modifier interface: `ModifyAttackInterval`, `ModifyDamage`, `OnHit`, `OnKill`
- Modifiers keep private internal state where needed (e.g., Momentum tracks last target ID)
- Stacking: **additive within category** (no multiplicative explosion)

### Draft system rules (locked):
- 5 options per round
- Free slots exist → 2 tower cards + 3 modifier cards
- All slots full → 5 modifier cards
- Anti-brick: never offer a modifier if no tower can accept it (swap for applicable one)

### Targeting modes:
- **First**: highest `Progress` in range
- **Strongest**: highest current HP in range
- **Lowest HP**: lowest current HP in range

## Planned Folder Layout

```
res://
  Scenes/
    Main.tscn               # Root: GameController + UIRoot + World
    World/
      World.tscn, Slot.tscn, Enemy.tscn, Tower.tscn
    UI/
      DraftPanel.tscn, DraftCard.tscn, HUDPanel.tscn

  Scripts/
    Core/
      GameController.cs     # Run lifecycle state machine
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
      EnemyInstance.cs, TowerInstance.cs, SlotInstance.cs
    Modifiers/
      Modifier.cs           # Base interface
      ModifierRegistry.cs   # JSON ID → concrete modifier object
      ModEvents.cs          # Event system for modifier interactions
    Data/
      DataLoader.cs, Models.cs

  Data/
    towers.json, modifiers.json, waves.json
```

## V1 Scope Locks

These will NOT be in v1 — do not design toward them:
- Meta progression, unlocks, rarity tiers, shop/economy
- Tower moving, selling, or replacement
- Active enemy abilities
- Procedural maps or multiple lanes
- Rerolls or mid-wave decisions

If an idea requires a new system → defer to "Project 2."

## V1 Content

- **3 towers**: Rapid Shooter (fast/low dmg), Heavy Cannon (slow/high dmg), Marker Tower (applies Marked: +20% dmg for 2s)
- **4 modifiers**: Momentum (+10% dmg per consecutive hit same target, resets on switch), Overkill (excess dmg spills to next enemy, 1 spill only), Exploit Weakness (+100% dmg vs Marked), Focus Lens (+100% dmg, ×2 attack interval)
- **1 enemy type**: Basic Walker (medium HP/speed); HP scales as `baseHP × 1.12^(wave-1)`
- **20 waves**, 6 tower slots, max 3 modifiers per tower
