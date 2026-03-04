
# Slot Theory — Technical Design Document (TDD) v1.0
**Studio:** 7ants Studios  
**Game:** Slot Theory  
**Engine:** Godot 4.x **.NET** (C#)  
**Runtime:** .NET 8+ (install the .NET SDK 8)  
**Target platform (v1):** Windows (Steam)

> This doc assumes a recent Godot 4.x **.NET** build and .NET 8+. If you install a later Godot 4.x version, the architecture still applies.

---

## 1) Project Goals

### Goals
- Ship a small, professional, deterministic-ish, systems-first game on Steam.
- Core loop: **Draft (before wave) → Auto wave → Repeat**.
- Depth via **tower-specific, conditional modifiers** and **targeting modes**.
- Tight scope: fixed slots, no economy layer, no rerolls, no selling, no moving.

### Non-goals (v1)
- Meta progression, unlocks, rarity tiers, shop systems.
- Tower moving/selling/replacement mechanics.
- Active enemy abilities (timers/casts/teleports/etc.).
- Procedural maps or multiple lanes.
- Multiplayer, leaderboards, cloud saves.

---

## 2) Gameplay Constraints (Locked Design)

### Run structure
- **20 waves** (fixed-length run).
- Draft happens **before** each wave.
- Continuous enemy spawn during wave.
- Scaling: **linear stat scaling** (HP scales by formula; speed constant initially).

### Draft rules
- 5 options per round.
- While slots not full: **2 tower cards + 3 modifier cards**.
- Once slots full: **4 modifier cards** (anti-brick protection).
- Wave 1 and 15: **bonus picks** (2 picks each).
- No rerolls.

### Towers
- 6 fixed slots total.
- 4 tower types implemented: Rapid Shooter, Heavy Cannon, Marker Tower, Arc Emitter.
- Each tower has targeting mode: **First / Strongest / Lowest HP**.
- Each tower can hold **max 3 modifiers**. (Hard cap, no replacement in v1.)

### Modifiers
- Attach permanently to a specific tower.
- Mostly additive stacking within categories.
- Predictable + readable effects (clear condition, clear outcome).
- No modifiers may introduce new systems (economy, inventories, new UI subsystems).

---

## 3) Technical Requirements

### Performance
- 60 FPS on typical mid-range PCs.
- Enemy counts modest (avoid count scaling early; scale HP instead).

### Determinism / Debuggability
- Prefer deterministic calculations (fixed logic, minimal physics dependence).
- Optional seedable RNG for drafts (for reproducibility during tuning).

### Shipping requirements
- Stable Windows build.
- Steam store-ready binaries + minimal UX expectations:
  - Main menu, settings (volume, fullscreen/window), quit.
  - No crashes / no softlocks.

---

## 4) Engine & Tooling

### Godot setup
- Use **Godot .NET** build (C# capable).
- Install **.NET SDK 8**.

### IDE
- Rider / Visual Studio / VS Code (any is fine).
- Keep formatting consistent (EditorConfig optional).

### Version control
- Git repo: `slot-theory`
- Ignore `.godot/`, `.mono/`, `bin/`, `obj/`, exported builds.
- Use conventional commits (optional) to keep history readable.

---

## 5) Project Structure

### Folder layout
```
res://
  Scenes/
    Main.tscn
    World/
      World.tscn
      Slot.tscn
      Enemy.tscn
      Tower.tscn
    UI/
      DraftPanel.tscn
      DraftCard.tscn
      HUDPanel.tscn

  Scripts/
    Core/
      GameController.cs
      RunState.cs
      DraftSystem.cs
      WaveSystem.cs
      Balance.cs

    Combat/
      CombatSim.cs
      Targeting.cs
      DamageModel.cs
      Statuses.cs

    Entities/
      EnemyInstance.cs
      TowerInstance.cs
      SlotInstance.cs

    Modifiers/
      Modifier.cs
      ModifierRegistry.cs
      ModEvents.cs

    Data/
      DataLoader.cs
      Models.cs

  Data/
    towers.json
    modifiers.json
    waves.json

  Art/
  Audio/
```

### Scene tree (Main)
- `Main.tscn`
  - `GameController` (Node) — orchestration / state machine
  - `UIRoot` (CanvasLayer)
    - `DraftPanel`
    - `HUDPanel`
    - `WaveBanner`
  - `World` (Node2D)
    - `Lane` (Path2D or simple lane transform)
    - `Enemies` (Node2D container)
    - `Towers` (Node2D container)
    - `Slots` (Node2D container)

---

## 6) Core Architecture

### High-level approach
- Use a **simulation-driven loop** rather than physics-heavy behaviors.
- Enemies and towers can be plain Nodes with minimal visuals; the logic lives in C# systems.
- Keep the game readable and testable: simulation code should be predictable.

### Key objects
- **GameController**: top-level state machine for run lifecycle.
- **RunState**: single source of truth for run data.
- **CombatSim**: executes wave step-by-step (spawning, moving, targeting, attacks, deaths).
- **DraftSystem**: produces 5 draft options following hard rules.
- **WaveSystem**: config + spawn schedule + scaling formula.
- **ModifierRegistry**: maps modifier IDs to concrete modifier behavior classes.

---

## 7) RunState (Single Source of Truth)

`RunState` owns:
- `int WaveIndex`
- `SlotInstance[] Slots` (size 6)
- `List<EnemyInstance> EnemiesAlive`
- `int EnemiesSpawnedThisWave`
- `float WaveTime`
- Optional: RNG seed / state for draft reproducibility
- Optional: derived stats for debug HUD

### Why this matters
- Easy to debug.
- Easy to add save/load later if desired.
- Prevents logic spread across scene nodes.

---

## 8) Entities

### SlotInstance
- `int Index`
- `TowerInstance? Tower` (nullable)

### TowerInstance
- `string TowerId`
- Base stats (from data): damage, attack interval, range.
- Runtime state:
  - `TargetingMode` enum: First / Strongest / LowestHP
  - `float Cooldown`
  - `List<Modifier>` Modifiers (max 3)
  - Optional: last target ID for “momentum” style effects
  - Optional: per-modifier state (kept inside modifier instances)

### EnemyInstance
- `string EnemyTypeId`
- Runtime state:
  - `float Hp`
  - `float MaxHp`
  - `float Speed`
  - `float Progress` (0..1 along lane)
  - Status flags/timers (v1 minimal):
    - `Marked` (bool) + `MarkedRemaining` (seconds)

> Keep enemy movement math simple: `Progress += Speed * delta / LaneLength` or equivalent.

---

## 9) Combat Simulation

### Update order per frame (CombatSim)
1. **Spawn** enemies according to wave spawn interval until wave quota reached.
2. **Move** enemies along lane.
3. For each tower:
   - Reduce cooldown by delta.
   - If cooldown <= 0:
     - Acquire target via `Targeting.SelectTarget(...)`.
     - If target exists: attack (apply damage pipeline), set cooldown = attack interval.
4. Remove dead enemies.
5. Determine wave end:
   - Wave ends when quota spawned AND no enemies alive.
6. Determine loss:
   - If any enemy reaches end (progress >= 1.0): fail run (v1: immediate loss).

### Targeting
- **First**: enemy with highest `Progress` in range.
- **Strongest**: enemy with highest `Hp` in range.
- **Lowest HP**: enemy with lowest `Hp` in range.

### Damage pipeline (recommended)
- Build a `DamageContext`:
  - attacker tower, target enemy, base damage, targeting mode, wave index, etc.
- Apply modifier hooks in deterministic order:
  1) Stat modifiers (damage/attack interval)
  2) Conditional modifiers (e.g., vs Marked)
  3) On-hit effects (apply Mark, etc.)
- Apply final damage to enemy HP.
- Resolve overkill spill if modifier present (see below).

---

## 10) Statuses (v1 minimal)

### Marked
- Applied by Marker tower on hit.
- Increases incoming damage by a fixed percentage (e.g., +20%).
- Duration 2 seconds (tunable).

**Implementation detail:**
- Use a timer float on the enemy (`MarkedRemaining`).
- Each frame: decrement and clear flag when <= 0.

> Keep status vocabulary tiny for v1. Don’t add burn/poison/slow until after vertical slice proves fun.

---

## 11) Modifiers System

### Goals
- Data-driven availability (JSON), code-driven behavior.
- No “if modifier id == ...” spaghetti in the tower code.
- Modifiers can keep private internal state where needed.

### Modifier base class
Suggested interface (minimal):
- `void ModifyAttackInterval(ref float interval, TowerInstance tower)`
- `void ModifyDamage(ref float damage, DamageContext ctx)`
- `void OnHit(DamageContext ctx)`
- `void OnKill(DamageContext ctx)`

> You can start even simpler: `ModifyDamage` + `ModifyAttackInterval` + `OnHit` and expand only if needed.

### ModifierRegistry
- Loads modifier definitions from JSON (`id`, `name`, `desc`, `params`).
- Creates concrete modifier objects by ID.

### Implemented modifiers (10 total)
1. **Momentum** - +16% damage per consecutive hit (max ×1.8)
2. **Overkill** - 60% excess damage spills to next enemy
3. **Exploit Weakness** - +60% damage vs Marked enemies
4. **Focus Lens** - +125% damage, ×2 attack interval
5. **Chill Shot** - Hits slow enemies to 70% speed for 5s
6. **Overreach** - +50% range, −30% damage
7. **Hair Trigger** - +50% attack speed, −40% range
8. **Split Shot** - Fires 2× 42% damage projectiles
9. **Feedback Loop** - Kill reduces cooldown by 70%
10. **Chain Reaction** - +1 chain bounce per copy (60% decay)

### Stacking rules
- Additive within category (e.g., conditional damage bonuses sum).
- Avoid multiplicative “explosion” early.

---

## 12) Draft System

### Inputs
- Current RunState: free slots, existing towers, modifier caps.
- Content pools: tower IDs, modifier IDs.

### Output
- `List<DraftOption>` size 5.
- DraftOption types:
  - `TowerOption(towerId)`
  - `ModifierOption(modifierId)`

### Rule set (locked)
- If free slots exist:
  - 2 towers + 3 modifiers.
- If all slots full:
  - 5 modifiers.

### Anti-brick rule
- Do not offer a modifier if it can’t be applied to any tower (e.g., all towers at modifier cap).  
  - If that occurs, swap it with another modifier that is applicable.

---

## 13) UI

### DraftPanel
- Displays 5 DraftCards.
- On selection:
  - If TowerOption: place into next empty slot (v1) or prompt user to choose an empty slot.
  - If ModifierOption: prompt “Select a tower to apply” (highlight eligible towers).

### HUDPanel
- Wave number, enemies remaining, basic tower overview.
- Minimal: keep it functional, not fancy.

### Settings (v1)
- Master volume.
- Fullscreen/windowed toggle.
- Quit.

---

## 14) Balancing & Data

### waves.json
- `waves[]`: count + spawnInterval.
- `hpGrowth`: e.g., 1.12.
- Keep speed constant in v1.

### Balance.cs
Centralize tunables:
- base HP for enemy
- lane length / speed constants
- Mark bonus %
- Mark duration
- damage categories scaling rules

> Avoid “magic numbers” scattered across systems.

---

## 15) Export / Steam (v1)

### Build target
- Windows x64 export preset in Godot.
- Ensure required redistributables are handled (Godot .NET export uses .NET runtime packaging; follow Godot export preset guidance).

### Steam
- App fee handled externally.
- Integrate Steamworks API **only if necessary** in v1 (you can ship without achievements).
- Focus on stable packaging and store assets later; pipeline proof matters.

---

## 16) Testing Strategy

### Developer debug tools (worth it)
- On-screen debug overlay:
  - current wave, spawned count, alive count, DPS estimate
  - selected tower targeting mode
  - Marked status counts
- “Fast-forward” key for wave sim (optional) to speed balancing.

### Unit-ish tests
- Keep a small set of pure C# tests for:
  - targeting selection logic
  - damage pipeline math
  - modifier stacking math

(If you don’t want full test infra, at least write “debug asserts” and log checks.)

---

## 17) Implementation Roadmap (Practical)

### Week 1 — Core loop skeleton
- Lane + enemy movement.
- Spawner (continuous spawn).
- 1 tower (Rapid) + basic targeting.
- Damage + death + wave end.

### Week 2 — Draft loop + slots
- Slot system (6 fixed slots).
- DraftPanel (ugly UI ok).
- Add Heavy Cannon.
- Targeting mode selection per tower.

### Implementation Status ✅ COMPLETED

**Core Systems Implemented:**
- Full 20-wave progression ✅
- 4 towers with targeting modes ✅  
- 10 modifiers with stacking ✅
- Draft system with anti-brick protection ✅
- Status effects (Marked, Slow) ✅
- Difficulty modes (Normal/Hard) ✅

**Polish & Quality:**
- Complete UI/UX with animations ✅
- Audio feedback system ✅
- Settings & pause functionality ✅
- Bot testing framework ✅
- Memory management & stability ✅
- Export pipeline ready ✅

---

## 18) Open Questions (Keep Minimal)
- Exact loss condition: immediate fail on first leak vs small “lives” value (keep simple; default immediate fail in v1).
- Visual style: minimalist readable shapes vs simple sprites (avoid art pipeline bloat).
- Whether to allow manual slot choice when placing a tower (auto-fill is fine for early builds).

---

## 19) Definition of Done (v1) ✅ ACHIEVED
- Complete 20-wave run structure ✅
- Draft-before-wave functioning ✅
- 4 towers + 10 modifiers fully playable ✅
- Targeting modes working ✅
- No critical bugs / no crashes ✅
- Main menu + settings + how-to-play ✅
- Difficulty modes implemented ✅
- Exported Windows build ready for Steam upload ✅
- Comprehensive balance testing completed ✅

---
