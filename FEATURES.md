# Slot Theory — Feature & Polish Reference

> All v1 features implemented as of the current build. This document is the single source of truth for what exists — useful for QA, playtesting briefs, and onboarding.

**Platforms:** Windows Desktop, Android (Phone & Tablet)

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

## Platform Support

| Platform | Controls | Features |
|---|---|---|
| **Windows Desktop** | Mouse & keyboard | Full feature set, Esc pause, optimized for desktop screens |
| **Android (Phone & Tablet)** | Touch controls | Responsive UI scaling, hamburger menu pause, auto-pause on minimize, adaptive layout |

---

## Towers (4 types)

| Tower | Fire Rate | Damage | Special |
|---|---|---|---|
| Rapid Shooter | Fast (0.4 s) | Low (10) | — |
| Heavy Cannon | Slow (2.0 s) | High (60) | — |
| Marker Tower | Medium (1.0 s) | Very Low (5) | Applies **Marked** status on hit |
| Arc Emitter | Medium (1.2 s) | Low (14) | Chains to 2 additional enemies (260 px radius, 60% damage decay per bounce) |

Towers are placed in **6 slots** on the map. Max **3 modifiers** per tower.

---

## Modifiers (10 types)

| Modifier | Effect |
|---|---|
| Momentum | +16% damage per consecutive hit on the same target; resets on target switch. Caps at 5 stacks (×1.8 total). |
| Overkill | 60% of excess damage from a killing blow spills to the next enemy in lane (one spill only). |
| Exploit Weakness | +60% damage vs Marked enemies. |
| Focus Lens | +125% damage, ×2 attack interval. Big hits, slow fire — synergises with Overkill. |
| Chill Shot | Hits apply Slow: enemy moves at 70% speed for 5 seconds. |
| Overreach | +50% range, −30% damage. |
| Hair Trigger | +50% attack speed, −40% range. |
| Split Shot | Fires 2 projectiles at 42% damage each to nearby enemies on hit. |
| Feedback Loop | On kill, reduce current cooldown by 70%. |
| Chain Reaction | Adds +1 chain bounce (60% damage per hop). |

### Modifier Color Language

| Family | Color | Members |
|---|---|---|
| **DamageScaling** | Orange | Momentum, Overkill, Focus Lens, Hair Trigger, Feedback Loop |
| **Utility** | Cyan | Chill Shot |
| **Range** | Violet | Overreach |
| **StatusSynergy** | Magenta | Exploit Weakness |
| **MultiTarget** | Mint-green | Split Shot, Chain Reaction |

This color language is used on draft cards, slot halos, and live modifier icons for consistent visual communication.

---

## Enemies (3 types)

| Enemy | HP | Speed | Leak Cost |
|---|---|---|---|
| Basic Walker | `65 × 1.08^(wave-1)` | 120 px/s | 1 life |
| Armored Walker | 4× Basic HP | 60 px/s (half) | **2 lives** |
| **Swift Walker** | 1.5× Basic HP | 240 px/s (double) | **1 life** |

Armored Walkers first appear at wave 7; count ramps to 5 by wave 20. Rendered at **1.5× scale** so they are visually distinct from Basic Walkers at a glance.

**Swift Walkers** appear waves 10–14 as small, fast lime-green diamonds at 0.8× scale. Their speed makes them hard to catch but they're relatively fragile.

**Waves 12–14 use clumped Armored spawns** (`"ClumpArmored": true` in `waves.json`): all Armored Walkers arrive as a consecutive block after the first third of basics, creating a mid-wave panic spike instead of a uniform drip.

### Enemy Visuals

| Element | Detail |
|---|---|
| **Basic Walker** | Round teal body with white + dark-pupil eyes; drawn via `_Draw()` |
| **Armored Walker** | Hexagonal crimson body with 3-layer depth shading, larger eyes; 1.5× scale |
| **Swift Walker** | Small lime-green diamond at 0.8× scale; moves at double speed, easily identified by color and size |
| **HP bar** | Thin bar above enemy; shifts **green → yellow → red** at 50% and 25% HP; cyan tint for Basic, magenta tint for Armored |
| **Marked ring** | 3 spinning 90° purple arcs orbiting the enemy while Marked status is active; rotates at 2.5 rad/s |
| **Slow ring** | Cyan outer ring drawn around enemy while Slow status is active |
| **Slow tint** | Enemy `SelfModulate` shifts to blue-grey (`#B3D9FF`) while slowed, composites cleanly with hit flash |
| **Spawn scale-in** | Enemy scales from 0 → full size over 0.15 s with a Back/Out ease on spawn |

---

## Status Effects

| Status | Effect | Duration |
|---|---|---|
| Marked | +40% incoming damage from all towers | 2.5 seconds |
| Slow | 70% movement speed | 5 seconds |

---

## Difficulty Modes

| Mode | Enemy HP | Enemy Count | Spawn Speed |
|---|---|---|---|
| Normal | 1.0× | 1.0× | 1.0× |
| Hard | 1.2× | 1.15× | 0.85× (15% faster) |

### Wave Tension Curve Upgrade

When a clumped armored wave is about to start (`ClumpArmored` with enough armored units):

- Shows warning text: `"ARMORED WAVE INCOMING"`
- Plays a short screen pulse
- Applies `InitialSpawnDelay = 0.8s` for anticipation

---

## Draft System

- **Free slots available** → 5 options: 2 tower cards + 3 modifier cards
- **All slots occupied** → 4 modifier cards (anti-brick: never offers a modifier with no valid target tower)
- **Wave 1**: 2 picks (one extra pick before the first wave)
- **Wave 15**: 2 picks (second bonus pick mid-run as a lifeline)
- Modifier cards are only offered when at least one tower can still accept them

### Placement Flow (Preview → Confirm)

Modifier assignment now requires explicit confirmation:

1. Pick a modifier card
2. Valid towers/slots highlight
3. Tap once to preview ghost modifier on target
4. Tap same target again to confirm

**Input behavior details:**
- Tapping elsewhere while preview is active cancels the preview
- Mobile supports tapping tower body as fallback for preview/confirm
- Short guard window prevents instant confirm from touch→mouse duplicate events

---

## Map Selection

Before starting a game:
- Choose from multiple procedurally generated maps
- **Auto-select**: First map is automatically selected when entering map selection
- Browse and select any available map before starting
- Each map has unique snake-path layout and tower slot placement

---

## Targeting Modes

Each tower cycles through 3 modes via left-click (desktop) or tap (mobile):

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
| Pause access | **Desktop:** ESC hint label; **Mobile:** Hamburger menu button (☰) in top-right |

**Mobile optimizations:**
- Button sizes automatically scale for touch targets
- UI elements adapt to screen size and orientation
- Game auto-pauses when Android app is minimized

---

## Draft Panel

Full-screen overlay shown between waves:

| Feature | Detail |
|---|---|
| Pick counter | `"Wave 15 Draft — Pick 2 of 2"` when a bonus pick is active |
| **Wave preview footer** | Blue-tinted label below the cards: `"↓ 22 Basic · 3 Armored [clumped]"` so the player knows what's coming before committing |
| Card layout | Tower cards (name + stats) or modifier cards (name + description) |
| **Enhanced card sizing** | Cards automatically scale to fit content; taller cards (132-186px height) accommodate detailed modifier descriptions |
| **Smooth animation** | Card entrance staggered at 0.29s intervals with 0.24s entrance duration for polished presentation |
| **Draft REVEAL ritual** | Face-down hold + staggered flip reveal (~120ms hold, 400ms stagger), per-card shing SFX, and icon/title micro-punch animation |
| **BONUS PICK stamp** | Animated stamp overlay on multi-pick waves (Wave 1 and Wave 15) for visual emphasis |
| **Rare foil shimmer** | 1-in-12 drafts feature subtle foil shimmer pass effect (visual-only enhancement) |
| **Smart synergy hints** | Modifier cards show tiny synergy tags (e.g. "GOOD WITH: MARKED"); hover (desktop) or tap-hold (mobile) pulse-highlights synergy towers in world |
| Hover scale | Cards scale to 1.06× on mouse-over (0.08 s tween) |
| Keyboard 1–5 | Press a number to select the corresponding card |
| Key hints | `[ 1 ]` – `[ 5 ]` labels on each card |
| **World-click placement** | After picking a card, the panel closes; player clicks/taps directly on a slot/tower in the world to complete placement |
| **Color-coded highlights** | Valid tower slots glow **gold**; valid modifier targets glow **white**; occupied/ineligible slots glow **red** |
| **Placement hint label** | Gold text shows `"Click/Tap a slot to place  X"` or `"Click/Tap a tower to assign  X"` (platform-adaptive) |
| **Cancel (Esc)** | While awaiting world-click, Esc (desktop) or back gesture (mobile) restores the draft panel with the original options |
| **Key hint labels** | `[ 1 ]`–`[ 5 ]` shown as separate child labels anchored to the bottom-right of each card in a muted blue-grey; styled independently from card body text |

---

## Wave Announcement

On wave start (not shown in bot mode):

- Large centred label fades in/out showing `"WAVE N"`
- Animates with scale + alpha tween; holds briefly then fades
- Gives the player a moment to read the wave number before enemies spawn
- **Wave 20 special treatment**: Enhanced final-wave banner with special pulse behavior and `wave20_start` sound cue

---

## Slot Visuals

Each of the 6 tower slots is a node with persistent child visuals:

| Element | Detail |
|---|---|
| Empty slot | Dark purple filled square + neon violet border (7 px thick outer + 1.5 px inner) |
| Modifier pips | 3 small squares (6×6 px) below each slot; hidden until a tower is placed, then dim grey = empty slot, green = filled, orange = tower at max mods |
| **Modifier icons** | Visual icons (18×18 px) displayed below tower showing each equipped modifier; unique symbol and color per modifier type; pulse and brighten (1.35× scale) when modifier activates |
| **Draft highlights** | Gold for valid tower slots, white for valid modifier targets, red for occupied/ineligible; fade in/out via tween |
| **Modifier proc halo** | Color-coded halo effect around slot when modifiers activate (0.2s duration, pulsing animation) |

---

## Tower Visuals

Each placed tower renders entirely via `_Draw()`:

| Visual | Detail |
|---|---|
| Tower body | Hand-drawn per type: Rapid Shooter (hexagonal cyan), Heavy Cannon (octagonal orange), Marker Tower (diamond pink), Arc Emitter (circular blue-white, 3 discharge prongs) |
| Glow layers | Each tower has 2–3 soft radial glow circles behind the main shape |
| Charge arc | Thin bright arc sweeps clockwise from 12 o'clock showing cooldown progress; dim background ring behind it |
| Range circle | Faint filled polygon (10% opacity) + subtle Line2D border showing attack range |
| Targeting icon | `▶` / `★` / `▼` label centred on the **slot** node (not the tower), so it stays upright as the tower rotates |
| **Tower rotation** | Tower body rotates smoothly to face its last target (`LerpAngle` at 15 rad/s); barrel aims at -Y axis |
| **Placement bounce** | Tower scales from 0 → 1.15 → 1.0 over 0.25 s with Back/Out + Sine/InOut eases on placement |
| **Attack flash** | Tower briefly pulses to 1.4× brightness then fades back (0.03 s spike, 0.25 s Expo/Out decay) on each shot |
| **Recoil animation** | Tower kicks backward 3.5px from target direction on firing, returns with elastic ease (~0.1s total) |

### Tower Personality

Towers now use their idle draw parts as actual firing motion when a projectile is fired.

| Tower | Idle Behavior | Firing Motion |
|---|---|---|
| **Rapid Shooter** | Subtle idle barrel sway | Barrel and muzzle pull back briefly on shot; muzzle glow expands/brightens during impulse |
| **Heavy Cannon** | Slight piston-like idle motion | Barrel slam-back layered on top of piston idle; stronger muzzle bloom burst on fire |
| **Marker Tower** | Steady targeting stance | Antenna recoils and core glow pulses per shot |
| **Arc Emitter** | Idle electric flicker | Prongs flare outward with thicker discharge lines; core/endpoint glow surges on fire |

---

## Projectiles & Effects

| Effect | Detail |
|---|---|
| **Enhanced projectile** | Diamond-shaped head (5px) with enhanced glowing trail (14-point history, 0.82α max) + larger bloom (10px outer + 5px inner white); tracks target position |
| Target dies in-flight | Projectile dissolves harmlessly |
| Damage number | Floating number drifts upward and fades over 0.7 s on hit; coloured to match the projectile |
| **Kill-shot number** | Killing blow shows a **larger (24 px), gold** damage number instead of the standard 18 px coloured number |
| **Enemy hit flash** | Enemy flashes to 2× brightness for 0.03 s then fades back (0.15 s Expo/Out) on every hit (skipped on kill shot) |
| **Kill hit stop** | Brief time freeze (0.04-0.055s real-time) on enemy death; Heavy Cannon kills get longer/stronger effect (0.055s, 0.16× time scale) |
| Death burst | Expanding ring + brief white flash at centre + **16 semi-random radial sparks** + inner ring (fades in first 60%); larger/redder for armored enemies |
| **Chain arc** | Arc Emitter bounces draw a jagged electric arc between hit positions (4 random perpendicular jitter midpoints); fades over 0.18 s with endpoint glow blooms |

### Kill Satisfaction and Audio Scaling

| System | Detail |
|---|---|
| **Kill hitstop** | Brief time slowdown (0.04-0.055s) with different intensities per tower type; Heavy Cannon gets strongest effect |
| **Death sound scaling** | Death sound pitch scales for quick kill chains |
| **Combo pitch ramp** | Short-window escalation: 1.00 → 1.05 → 1.10 → 1.15 (clamped) |
| **Audio feedback** | Enhanced impact feel for satisfying kill sequences |

---

## Tooltip

Visible **during wave** and **while assigning a modifier to a tower** (hides during card selection and pause):

- Appears on hover over any placed tower
- Shows: tower name, targeting mode, **damage / effective attack interval / range** stat line, then a bulleted list of attached modifiers with their descriptions
- Effective attack interval accounts for Focus Lens and Hair Trigger modifiers baked in
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
| **Tower recoil** | Tower fires | Tower kicks backward 3.5px from target direction, returns with back/out ease over ~0.1s |
| **Modifier proc halo** | Modifier activates | Colored halo pulse around tower slot for 0.2s, color matches modifier type |
| **Modifier icon pulse** | Modifier activates | Individual modifier icon scales and brightens for 0.24s with sine wave animation |
| **Hit stop** | Enemy killed | Brief time slowdown (0.04-0.055s) with different intensities per tower type; Heavy Cannon gets strongest effect |
| **Lives label flash** | Life lost | HUD lives label punches to 1.25× scale then returns (elastic tween) |
| **UI hover sound** | Mouse enters any button | Short quiet high-pitched `"ui_hover"` SFX on all buttons (draft cards, pause menu, main menu) |
| **Enhanced draft audio** | Card interactions | New UI/audio cues for card pick, preview ghost, lock-in confirm, and reveal shing effects |

---

## Visual Identity

| System | Detail |
|---|---|
| **Font** | Rajdhani Bold throughout all UI (labels, buttons, HUD, draft panel, end screen) |
| **UI theme** | Neon synthwave palette via `UITheme.Build()` — `StyleBoxFlat` buttons with rounded corners, purple/magenta border glow; 5 button states (normal, hover, pressed, focus, disabled) |
| **Font system** | `UITheme.Build()` default font: `Assets/Fonts/Rajdhani-SemiBold.ttf`; `UITheme.ApplyFont(...)` for runtime-generated controls; SemiBold/Bold variants for headings |
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
- **Run story hook**: Build Name, MVP Tower (% of total damage), Most Valuable Mod (triggered Nx)

Dismiss: **left-click anywhere** or **press Enter / Space** → returns to main menu.

---

## Pause Screen

**Desktop:** Esc toggles the pause overlay  
**Mobile:** Hamburger menu (☰) button or system back gesture  
(Blocked during Win/Loss phase)

| Button | Behaviour |
|---|---|
| Resume | Unpauses and closes overlay |
| Restart Run | Unpauses + full run reset (new map, all slots cleared, wave 1) |
| Settings | Slides to inline settings panel (Master/Music/FX sliders + display toggle) |
| How to Play | Opens responsive scrollable tutorial overlay; **improved mobile formatting** with adaptive font sizes and margins |
| Main Menu | Unpauses engine then fades to `MainMenu.tscn` |
| Quit to Desktop | `GetTree().Quit()` (desktop only) |

**Mobile-specific features:**
- Game automatically pauses when Android app is minimized
- Touch-optimized button sizes and spacing
- Back gesture support for navigation
- While settings open, Esc/back navigates back to the main pause panel (not unpause)
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
| Display toggle | **Desktop:** Button cycles `Windowed` ↔ `Fullscreen`; **Mobile:** Adapts to system settings |
| Persistence | Saved to `user://settings.cfg` via Godot `ConfigFile`; restored on next launch |
| Audio buses | `SettingsManager` creates "Music" and "FX" buses (routed to Master) at startup if missing |

**Responsive design:** Settings UI automatically adapts to screen size and input method (touch vs mouse).

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
| HpGrowthPerWave | 1.08 | HP × 1.08^(wave-1) |
| BaseEnemySpeed | 120 px/s | |
| TankyHpMultiplier | 4× | vs basic walker |
| TankyEnemySpeed | 60 px/s | |
| MarkedDamageBonus | +40% | |
| MarkedDuration | 2 s | |
| SlowSpeedFactor | 0.70 | −30% speed |
| SlowDuration | 5 s | |
| MomentumMaxStacks | 5 | |
| MomentumBonusPerStack | 8% | caps at ×1.4 damage |
