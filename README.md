# Slot Theory

A constraint-driven drafting tower defense game by 7ants Studios. Build a 6-slot loadout across 20 waves, survive to win.

Platforms: Windows (Steam), Android (phone and tablet)

---

## Core Loop

1. Draft a card between waves.
2. Place a tower or assign a modifier in the world.
3. Run the wave (auto-combat - no direct input during waves).
4. Repeat until wave 20 clear (win) or lives reach 0 (loss).

Starting lives: 10

---

## Controls

### Desktop

- Pick draft card: left click card
- Place tower / assign modifier: left click world slot or tower
- Cancel placement: `Esc`
- Cycle targeting mode: left click tower during wave
- Pause: `Esc`
- Speed: HUD button cycles `1x -> 2x -> 3x`
- Card shortcuts: `1-5` keys

### Mobile

- Pick draft card: tap
- Place tower / assign modifier: tap
- Cancel placement: Cancel button (appears below HUD bar) or Android back button
- Cycle targeting mode: tap tower during wave
- Pause: hamburger button in HUD
- Speed: HUD button cycles `1x -> 2x -> 3x`

Android auto-pauses when app is minimized.

---

## Draft Rules

- With free slots: 5 options, targeting a 2 towers + 3 modifiers mix
- If no modifier targets exist yet, missing modifier cards are backfilled with tower cards
- With all slots occupied: 4 modifiers
- Anti-brick: modifier options are only offered if at least one tower can still accept one

Modifier assignment uses `Preview -> Confirm`:

1. Pick modifier
2. Click/tap valid tower slot to preview
3. Click/tap same slot again to confirm
4. Click/tap elsewhere to cancel preview

---

## Draft Placement Previews

- **Tower range circle:** Hovering a tower card over an empty slot shows a pulsing range fill and border ring on the ghost tower so you can evaluate coverage before confirming.
- **Modifier range overlays:** Hovering Overreach over a placed tower shows a larger pulsing ring at the post-modifier range (×1.45). Hovering Hair Trigger shows a smaller ring at the post-modifier range (×0.82). Both rings use the modifier's accent color.

---

## Towers

| Tower | Damage | Attack Interval | Range | Notes |
|---|---:|---:|---:|---|
| Rapid Shooter | 10 | 0.45 s | 285 px | Fast single-target fire |
| Heavy Cannon | 56 | 2.0 s | 238 px | Heavy burst hits |
| Marker Tower | 7 | 1.0 s | 333 px | Applies Marked on hit |
| Arc Emitter | 18 | 1.2 s | 257 px | Chains to 2 extra enemies, 400 px chain range, 60% damage decay per bounce |
| Rift Sapper | 22 | 0.98 s | 230 px | Charged lane-mine trap tower with wave-start rapid seeding |

Unlock flow:
- Arc Emitter: beat the first campaign map on Normal or Hard (`ARC_UNSEALED`)
- Split Shot modifier: beat the second campaign map on Normal or Hard (`SPLIT_UNSEALED`)
- Rift Sapper: beat the third campaign map on Normal or Hard (`RIFT_UNSEALED`)

### Rift Sapper Mechanics

- Internal tower ID: `rift_prism` (display name: Rift Sapper)
- Passive behavior: plants mines on valid lane anchors in range
- Mine cap: 7 active mines per Rift Sapper
- Charge system:
  - each mine has 3 charges
  - trigger 1-2: 0.65x base mine damage
  - trigger 3 (final): 1.15x base mine damage
  - non-final triggers rearm for 0.18 s before they can trigger again
- Trigger/blast:
  - trigger radius: 32 px
  - blast target radius: 82 px
- Wave-start burst seeding:
  - first 2.4 s of each wave uses a faster plant interval (x0.55)
  - capped to 3 burst-speed plants per tower per wave
- Modifier interactions:
  - Split Shot: extra mini-mines spawn on final-charge pops only
  - Chain Reaction: mine-to-mine chaining occurs on final-charge pops only

---

## Modifiers

| Modifier | Effect |
|---|---|
| Momentum | +16% damage per consecutive hit on same target, up to ×1.80. Resets on target switch. |
| Overkill | 60% of excess kill damage spills to next enemy in lane |
| Exploit Weakness | +45% damage vs Marked enemies |
| Focus Lens | +125% damage, ×2 attack interval |
| Chill Shot | On hit: 0.75× enemy speed for 5 s; stacks multiplicatively per tower |
| Overreach | +45% range, -10% damage |
| Hair Trigger | +35% attack speed, -18% range |
| Split Shot | Fires 2 split projectiles at 35% damage each; each extra copy adds +1 projectile |
| Feedback Loop | On kill, removes 65% of current cooldown |
| Chain Reaction | +1 chain bounce per copy, 60% damage carry per bounce |

Max modifiers per tower: 3

Split Shot is unlockable - beat the second campaign map on Normal or Hard.

---

## Enemies

| Enemy | HP | Speed | Leak Cost |
|---|---|---|---:|
| Basic Walker | `65 × 1.10^(wave-1)` | 120 px/s | 1 |
| Armored Walker | 3.5× Basic HP | 60 px/s | 2 |
| Swift Walker | 1.5× Basic HP | 240 px/s | 1 |

- Armored first appears on wave 6.
- Swift appears in waves 10-14.

---

## Targeting Modes

- Right arrow icon - **First**: highest path progress
- Star icon - **Strongest**: highest current HP
- Down arrow icon - **Lowest HP**: lowest current HP

Modes cycle on tower click/tap during waves.

Rift Sapper uses tower-specific labels/icons for the same 3 internal modes:
- Die icon: Random (internal First)
- Down arrow icon: Closest (internal Strongest)
- Up arrow icon: Furthest (internal Lowest HP)

---

## Difficulty Modes

| Mode | Enemy HP | Enemy Count | Spawn Interval |
|---|---:|---:|---:|
| Easy | 1.0× | 1.0× | 1.0× |
| Normal | 1.2× | 1.05× | 0.95× |
| Hard | 1.3× | 1.1× | 0.90× |

Normal targets ~75% bot win rate; Hard targets ~50%. Multipliers are tunable at runtime via the automated tuning pipeline. Easy has no scaling.

---

## Surge System

The combat centerpiece is the **Spectacle system**, built around two event tiers:

1. **Surge** (per-tower burst trigger)
2. **Global Surge** (teamwide cataclysm trigger)

### Surge Meter (Per Tower)

Each tower has its own spectacle meter.

- Surge threshold: **145**
- After surge trigger, meter resets to: **10**
- Surge cooldown: **6.0 s**
- Meter gain comes from supported modifier procs, scaled by copy count, loadout diversity, and per-mod anti-spam token gates.

### How Surge Effects Are Chosen

When a surge fires, the tower resolves a signature from its equipped supported modifiers:

- **Single**: 1 unique supported mod - single effect
- **Combo**: 2 unique supported mods - combo core effect
- **Triad**: 3 unique supported mods - combo core + augment effect

Loadout identity directly controls what the surge does and how it looks.

### Global Meter and Global Surge

Every surge contributes to a shared global meter:

- per-surge gain: **+10**
- global threshold: **100**
- after global trigger, meter resets to: **0**

### What Happens On Global Surge

Global surge is a mapwide event with synchronized spectacle + gameplay:

- center-out cataclysm burst/ripple pass
- global status/detonation propagation
- broad tower payload application + cooldown reclaim
- synchronized tower accent bursts
- stronger screen treatment and impact audio

### Surge Readability and Visual Feel

The banner label and visual treatment are driven by which mods contributed most to filling the global meter:

- **10 named archetypes** - e.g. REDLINE WAVE (Momentum), CHAIN STORM (Chain Reaction), PRISM BARRAGE (Focus Lens). Falls back to GLOBAL SURGE if no dominant mod is detected.
- **Archetype preview**: the HUD global meter label transitions from "GLOBAL SURGE" to the predicted archetype name at ≥70% fill.
- **Feel types** - Detonation builds (Overkill, Focus Lens, Feedback Loop, Hair Trigger) produce a sharp spike flash + second snap pulse. Pressure builds (Momentum, Chill Shot, Overreach) produce a softer sustained flash.
- **Multi-color ripples** - up to 3 ripple colors reflecting the top contributing mods.
- **Per-tower identity FX** - each tower type fires its own archetype effect in staggered sequence.
- **Screen-edge vignette** - square-masked overlay ramps in during the final 30% of global meter fill, tinted to the dominant mod's color.
- **Sustained archetype tint** - a low-alpha full-screen color wash lingers ~2.4 s after global surge, keyed to feel (red/orange/purple).
- **SURGE ×N chain counter** - a gold callout accumulates at screen center when multiple surges chain within the contribution window.
- **Per-tower afterglow** - each tower involved holds a 2.4 s accent-colored modulate fade after its FX burst.
- **Triad callouts** - combo name and augment name spawn as separate sequential callouts; augment appears below in the augment modifier's own color.

---

## Achievements

13 achievements tracked locally via `AchievementManager`, persisted to `user://achievements.cfg`. Forwarded to Steam when available.

| ID | Name | Condition |
|---|---|---|
| FIRST_WIN | First Victory | Complete all 20 waves |
| HARD_WIN | Hard Carry | Complete all 20 waves on Hard |
| FLAWLESS | Flawless | Win without losing a life |
| LAST_STAND | Last Stand | Win with exactly 1 life remaining |
| HALFWAY_THERE | Halfway There | Survive to wave 10 |
| FULL_HOUSE | Full House | Fill all 6 tower slots in one run |
| STACKED | Stacked | Give any tower 3 modifiers in one run |
| SPEED_RUN | Speed Run | Win in under 8 minutes |
| ANNIHILATOR | Annihilator | Deal 100,000 total damage in one run |
| CHAIN_MASTER | Chain Master | Win with all 6 slots filled by Arc Emitters |
| ARC_UNSEALED | Arc Unsealed | Beat the first campaign map (unlocks Arc Emitter) |
| SPLIT_UNSEALED | Split Unsealed | Beat the second campaign map (unlocks Split Shot) |
| RIFT_UNSEALED | Rift Unsealed | Beat the third campaign map (unlocks Rift Sapper) |

---

## Settings

- Master / Music / FX sliders (saved to `user://settings.cfg`)
- Display: Windowed / Fullscreen toggle
- Colorblind mode: switches modifier accent colors to a high-contrast palette
- Reduced motion: skips draft card flip animations

---

## Steam

The game is available on Steam. Store page: [Slot Theory on Steam](https://store.steampowered.com/app/3670150/Slot_Theory/)

Steam features:
- **Global leaderboards**: per map/difficulty, all runs stored (not just personal best)
- **Achievements**: all 13 achievements forwarded to Steam
- **Steam Cloud**: settings and high scores sync across devices

---

## Bot Mode

Run automated balance tests:

```text
--scene res://Scenes/Main.tscn -- --bot --runs N
```

Optional difficulty override:

```text
--difficulty easy|normal|hard
```

Bot rotates strategies across maps and difficulties and prints summary stats.

Current strategy set (12 strategies):
- `Random`, `TowerFirst`, `GreedyDps`, `MarkerSynergy`, `ChainFocus`, `SplitFocus`, `HeavyStack`, `RiftPrismFocus`
- `SpectacleSingleStack`, `SpectacleComboPairing`, `SpectacleTriadDiversity`, `PlayerStyleKenny`

Optional strategy pool selection:

```text
--strategy_set all|optimization|edge
```

Bot summary includes win rates, wave curves, modifier/tower usage, and spectacle trigger analytics (surge/global surge totals and top effect breakdowns).

### Bot Metrics JSON Export

```text
--scene res://Scenes/Main.tscn -- --bot --runs 120 --bot_metrics_out release/bot_metrics.json
```

---

## Combat Lab Automation

Run scripted scenario validations:

```text
--scene res://Scenes/Main.tscn -- --lab_scenario Data/combat_lab/core_scenarios.json --lab_out release/combat_lab_report.json
```

Run a tuning sweep:

```text
--scene res://Scenes/Main.tscn -- --lab_sweep Data/combat_lab/sample_sweep.json --lab_out release/combat_lab_sweep.json
```

The automated tuning pipeline (`run_tuning_pipeline.ps1`) combines bot eval + scenario suite to iteratively optimize difficulty multipliers against win-rate targets.

---

Engine: Godot 4.6 + .NET 8
