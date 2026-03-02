# Slot Theory — Feature & Polish Reference

> All v1 features implemented as of the current build. This document is the single source of truth for what exists — useful for QA, playtesting briefs, and onboarding.

---

## Core Loop

| Step | What Happens |
|---|---|
| Start | Main menu → Play → Main.tscn loads |
| Draft | Player picks 1 of 5 options (tower or modifier) |
| Wave | Enemies walk the path automatically; no player input until wave ends |
| Repeat | 20 waves total |
| Win | All 20 waves cleared |
| Loss | Lives reach 0 at any point during a wave |

---

## Towers (3 types)

| Tower | Fire Rate | Damage | Special |
|---|---|---|---|
| Rapid Shooter | Fast | Low | — |
| Heavy Cannon | Slow | High | — |
| Marker Tower | Medium | Low | Applies **Marked** status on hit |

Towers are placed in **6 slots** on the map. Max **3 modifiers** per tower.

---

## Modifiers (7 types)

| Modifier | Effect |
|---|---|
| Momentum | +10% damage per consecutive hit on the same target; resets on target switch. Caps at 5 stacks (×1.5 total). |
| Overkill | Excess damage from a killing blow spills to the next enemy in lane (one spill only). |
| Exploit Weakness | +100% damage vs Marked enemies. |
| Focus Lens | +150% damage, ×2 attack interval. Big hits, slow fire — synergises with Overkill. |
| Hair Trigger | Reduces attack interval. |
| Overreach | Increases tower range. |
| Slow | Hits apply a Slow status: enemy moves at 70% speed for 5 seconds. |

---

## Enemies (2 types)

| Enemy | HP | Speed | Leak Cost |
|---|---|---|---|
| Basic Walker | `65 × 1.06^(wave-1)` | 120 px/s | 1 life |
| Armored Walker | 4× Basic HP | 60 px/s (half) | **2 lives** |

Armored Walkers first appear at wave 7; count ramps to 5 by wave 20. Rendered at **1.5× scale** so they are visually distinct from Basic Walkers at a glance.

### Enemy Visuals

| Element | Detail |
|---|---|
| **Basic Walker** | Round teal body with white + dark-pupil eyes; drawn via `_Draw()` |
| **Armored Walker** | Hexagonal crimson body with 3-layer depth shading, larger eyes; 1.5× scale |
| **HP bar** | Thin bar above enemy; shifts **green → yellow → red** at 50% and 25% HP; cyan tint for Basic, magenta tint for Armored |
| **Marked ring** | Purple arc ring drawn around enemy while Marked status is active |
| **Slow ring** | Cyan outer ring drawn around enemy while Slow status is active |

---

## Status Effects

| Status | Effect | Duration |
|---|---|---|
| Marked | +20% incoming damage from all towers | 2 seconds |
| Slow | 70% movement speed | 5 seconds |

---

## Draft System

- **5 options** shown each round
- **Free slots available** → 2 tower cards + 3 modifier cards
- **All slots occupied** → 5 modifier cards (anti-brick: never offers a modifier with no valid target tower)
- **Wave 1**: 2 picks (one extra pick before the first wave)
- **Wave 15**: 2 picks (second bonus pick mid-run as a lifeline)
- Modifier cards are only offered when at least one tower can still accept them

---

## Targeting Modes

Each tower cycles through 3 modes via left-click:

| Icon | Mode | Behaviour |
|---|---|---|
| ▶ | First | Highest progress along path (default) |
| ★ | Strongest | Highest current HP in range |
| ▼ | Lowest HP | Lowest current HP in range (finisher) |

---

## Procedural Map

A new snake-path map is generated each run:

- **Grid**: 8 cols × 5 rows, cell 160×128 px, grid origin y=80
- **Path shape**: 3-horizontal-leg snake; turn rows/cols randomised each run
- **Slot placement**: 6 zones (3×2), one slot per zone; prefers non-path cells adjacent to the path
- **Visuals**: flat `ColorRect` nodes — grass `#a6d608`, path dark neon purple line on `ColorRect` bg
- Path rendered as three overlaid `Line2D` passes: thick dark fill, mid glow, thin bright edge
- Path direction shown by animated **flow arrows** (`PathFlow`) travelling along the route
- On restart: `Free()` (not `QueueFree()`) for instant teardown without one-frame flicker

---

## HUD

Top bar (always visible during play):

| Element | Content |
|---|---|
| Wave label | `Wave X / 20` |
| Enemy counter | `alive / total` during a wave; hidden when wave is clear |
| Lives label | `Lives: N`; turns red when ≤ 3; **elastic punch-scale flash** on any life loss |
| Speed button | Cycles **1× → 2× → 3×** on each click; resets to 1× on wave clear or restart |
| ESC hint | Dim label reminding player about pause |

---

## Draft Panel

Full-screen overlay shown between waves:

| Feature | Detail |
|---|---|
| Pick counter | `"Wave 15 Draft — Pick 2 of 2"` when a bonus pick is active |
| Card layout | Tower cards (name + stats) or modifier cards (name + description) |
| Hover scale | Cards scale to 1.06× on mouse-over (0.08 s tween) |
| Keyboard 1–5 | Press a number to select the corresponding card |
| Key hints | `[ 1 ]` – `[ 5 ]` labels on each card |
| **World-click placement** | After picking a card, the panel closes; player clicks directly on a slot/tower in the world to complete placement |
| **Color-coded highlights** | Valid tower slots glow **gold**; valid modifier targets glow **white**; occupied/ineligible slots glow **red** |
| **Placement hint label** | Gold text shows `"Click a slot to place  X"` or `"Click a tower to assign  X"` |
| **Cancel (Esc)** | While awaiting world-click, Esc restores the draft panel with the original options |
| **Key hint labels** | `[ 1 ]`–`[ 5 ]` shown as separate child labels anchored to the bottom-right of each card in a muted blue-grey; styled independently from card body text |

---

## Wave Announcement

On wave start (not shown in bot mode):

- Large centred label fades in/out showing `"WAVE N"`
- Animates with scale + alpha tween; holds briefly then fades
- Gives the player a moment to read the wave number before enemies spawn

---

## Slot Visuals

Each of the 6 tower slots is a node with persistent child visuals:

| Element | Detail |
|---|---|
| Empty slot | Dark purple filled square + neon violet border (7 px thick outer + 1.5 px inner) |
| Mod-count pip | Green `"N/3"` label in lower-right of slot; only visible when the tower has ≥ 1 modifier |
| **Draft highlights** | Gold for valid tower slots, white for valid modifier targets, red for occupied/ineligible; fade in/out via tween |

---

## Tower Visuals

Each placed tower renders entirely via `_Draw()`:

| Visual | Detail |
|---|---|
| Tower body | Hand-drawn per type: Rapid Shooter (hexagonal cyan), Heavy Cannon (octagonal orange), Marker Tower (diamond pink) |
| Glow layers | Each tower has 2–3 soft radial glow circles behind the main shape |
| Charge arc | Thin bright arc sweeps clockwise from 12 o'clock showing cooldown progress; dim background ring behind it |
| Range circle | Faint filled polygon (10% opacity) + subtle Line2D border showing attack range |
| Targeting icon | `▶` / `★` / `▼` label in the centre of the tower |
| **Attack flash** | Tower briefly pulses to 1.4× brightness then fades back (0.03 s spike, 0.25 s Expo/Out decay) on each shot |

---

## Projectiles & Effects

| Effect | Detail |
|---|---|
| Projectile | Diamond-shaped head with a tapered glowing trail (10-point history); tracks target position |
| Target dies in-flight | Projectile dissolves harmlessly |
| Damage number | Floating number drifts upward and fades over 0.7 s on hit; coloured to match the projectile |
| **Enemy hit flash** | Enemy flashes to 2× brightness for 0.03 s then fades back (0.15 s Expo/Out) on every hit |
| Death burst | Particle-style burst on enemy death; larger/redder for armored enemies |

---

## Tooltip

Visible **during wave** and **while assigning a modifier to a tower** (hides during card selection and pause):

- Appears on hover over any placed tower
- Shows: tower name, targeting mode, and a bulleted list of attached modifiers with their descriptions
- Sized dynamically to content; positioned at cursor
- During modifier assignment: lets player inspect existing modifiers before committing

---

## Visual Feedback

| Effect | Trigger | Detail |
|---|---|---|
| **Screen shake** | Any life lost | `_worldNode` snaps through 4 offset positions (±8 px) in 0.18 s via tween, returns to origin |
| **Wave clear flash** | Wave completed | Semi-transparent green `ColorRect` over the world fades in then out over ~0.6 s |
| **Wave-clear hold** | Wave completed | 0.48 s pause after the flash before the draft panel opens, giving player a moment to breathe |
| **Enemy hit flash** | Any damage landed | Enemy node modulate spikes to 2× then decays over 0.15 s (handled by `EnemyInstance.FlashHit()`) |
| **Tower attack flash** | Tower fires | Tower modulate spikes to 1.4× then decays over 0.25 s (handled by `TowerInstance.FlashAttack()`) |
| **Lives label flash** | Life lost | HUD lives label punches to 1.25× scale then returns (elastic tween) |
| **UI hover sound** | Mouse enters any button | Short quiet high-pitched `"ui_hover"` SFX on all buttons (draft cards, pause menu, main menu) |

---

## Visual Identity

| System | Detail |
|---|---|
| **Font** | Rajdhani Bold throughout all UI (labels, buttons, HUD, draft panel, end screen) |
| **UI theme** | Neon synthwave palette via `UITheme.Build()` — `StyleBoxFlat` buttons with rounded corners, purple/magenta border glow; 5 button states (normal, hover, pressed, focus, disabled) |
| **Scene transitions** | `Transition.cs` autoload (CanvasLayer Layer=100, always-process) fades to black then back on every scene change; `FadeToScene(path)` is the single entry point for all scene navigation |
| **Map rendering** | Flat `ColorRect` nodes (no textures); grass `#a6d608`, path `#8B5E3C`; `Line2D` edges + animated flow arrows on path |

---

## End Screen

| State | Title | Subtitle |
|---|---|---|
| Win | `VICTORY` (green) | `All 20 waves survived!` |
| Loss | `GAME OVER` (red) | `Reached wave N / 20  ·  Lives lost: N` |

Both states show:
- **Run stats** (blue tint): `Enemies killed: N  ·  Total damage: N` — damage is actual HP removed, not overkill
- **Build summary**: each occupied slot lists the tower name and its modifiers

Dismiss: **left-click anywhere** or **press Enter / Space** → returns to main menu.

---

## Pause Screen

**Esc** toggles the pause overlay (blocked during Win/Loss phase).

| Button | Behaviour |
|---|---|
| Resume | Unpauses and closes overlay |
| Restart Run | Unpauses + full run reset (new map, all slots cleared, wave 1) |
| Settings | Slides to inline settings panel (Master/Music/FX sliders + fullscreen toggle) |
| How to Play | Opens scrollable tutorial overlay within the pause context; Back returns to pause |
| Main Menu | Unpauses engine then fades to `MainMenu.tscn` |
| Quit to Desktop | `GetTree().Quit()` |

- While settings open, Esc navigates back to the main pause panel (not unpause)
- Speed resets to 1× on any run restart

---

## Main Menu

- Procedural dark panel layout (no scene file, fully code-driven)
- Buttons: **Play**, **How to Play**, **Settings**, **Quit to Desktop**
- All buttons play `"ui_hover"` SFX on mouse-enter
- **Animated neon grid background** (`NeonGridBg` Control): 9 horizontal + 13 vertical neon-purple grid lines with per-line alpha sine waves, plus a slow downward scan sweep — very low opacity so UI stays readable

## Settings Screen

Accessible from main menu and from in-game pause overlay:

| Control | Detail |
|---|---|
| Master / Music / FX sliders | 0–100 range; live value label updates while dragging; logarithmic dB conversion (`Mathf.LinearToDb`) |
| Display toggle | Button cycles `Windowed` ↔ `Fullscreen` |
| Persistence | Saved to `user://settings.cfg` via Godot `ConfigFile`; restored on next launch |
| Audio buses | `SettingsManager` creates "Music" and "FX" buses (routed to Master) at startup if missing |

---

## Bot / Playtest Mode

For headless balance testing (not exposed to players):

```
--scene res://Scenes/Main.tscn -- --bot --runs N
```

- Runs N games at ~300× speed (100 substeps/frame at dt=0.05)
- 4 strategies cycle: Random, TowerFirst, GreedyDps, MarkerSynergy
- Prints per-wave lives heatmap, strategy win rates, tower/modifier usage table
- Armored walker 2-life penalty and bonus wave-15 pick are both respected

---

## Balance Constants (`Balance.cs`)

| Constant | Value | Notes |
|---|---|---|
| TotalWaves | 20 | |
| SlotCount | 6 | |
| StartingLives | 10 | |
| MaxModifiersPerTower | 3 | |
| DraftOptionsCount | 5 | |
| Wave1ExtraPicks | 1 | +1 pick before wave 1 |
| Wave15ExtraPicks | 1 | +1 pick before wave 15 |
| BaseEnemyHp | 65 | |
| HpGrowthPerWave | 1.06 | HP × 1.06^(wave-1) |
| BaseEnemySpeed | 120 px/s | |
| TankyHpMultiplier | 4× | vs basic walker |
| TankyEnemySpeed | 60 px/s | |
| MarkedDamageBonus | +20% | |
| MarkedDuration | 2 s | |
| SlowSpeedFactor | 0.70 | −30% speed |
| SlowDuration | 5 s | |
| MomentumMaxStacks | 5 | caps at ×1.5 damage |
