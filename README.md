# Slot Theory

A drafting tower defense game. Build a 6-slot loadout, survive 20 waves.

Platforms: Windows Desktop, Android (phone and tablet)

---

## Surge System

The combat centerpiece is now the **Spectacle system**, built around two event tiers:

1. **Surge** (per-tower burst trigger)
2. **Global Surge** (teamwide catastrophe trigger)

Only these two tiers are active. Minor spectacle triggers were intentionally removed to keep pacing clean.

### Surge Meter (Per Tower)

Each tower has its own spectacle meter.

- Surge threshold: **145**
- After surge trigger, meter resets to: **18**
- Surge cooldown: **6.0s**
- Meter gain comes from supported modifier procs and is affected by:
  - the proc's event scalar
  - copy count scaling (`1x -> 1.00`, `2x -> 1.92`, `3x+ -> 2.70`)
  - loadout diversity scaling (`1 mod -> 1.00`, `2 mods -> 1.08`, `3+ mods -> 1.16`)
  - per-mod anti-spam token gates (regen over time)

To stop stale loops, each tower also has inactivity decay:

- grace before decay: **2.0s**
- decay rate after grace: **6.0 meter/sec**

### How Surge Effects Are Chosen

When a surge fires, the tower resolves a signature from equipped supported modifiers:

- **Single**: 1 unique supported mod
- **Combo**: 2 unique supported mods
- **Triad**: 3+ unique supported mods

The system ranks primary/secondary/tertiary roles by copy weight plus recent contribution window, then dispatches:

- single effect for Single
- combo core effect for Combo
- combo core + augment effect for Triad

So loadout identity directly controls what the surge does and how it looks.

### What Happens On Surge Trigger

A triggered surge runs both gameplay and presentation payloads:

- tower-centered burst + link/volley visuals
- combo skin routing (Chill/Chain/Split/Focus families)
- status detonation chains from primed enemies (marked/slow/amped)
- short cinematic timing (slow-mo + tiny major-only hitstop)
- major-impact audio layers
- optional large-surge screen afterimage pass

Phase 3 adds short-lived aftermath zones from explosion families:

- frost slow residue (~0.8s)
- vulnerability residue (~1.2s)
- burn residue (~0.9s)

These are intentionally short and spawn with spacing rules to prevent visual clutter.

### Global Meter and Global Surge

Every surge contributes to a shared global meter:

- per-surge gain: **+10**
- global threshold: **100**
- after global trigger, meter resets to: **20**

Global surge contributor context tracks unique towers that surged recently (6s window), and the trigger uses that contributor count to scale parts of the payload.

### What Happens On Global Surge Trigger

Global surge is a mapwide event with synchronized spectacle + gameplay:

- center-out cataclysm burst/ripple pass
- global status/detonation propagation
- broad tower payload application + cooldown reclaim
- synchronized tower accent bursts
- stronger screen treatment and impact audio

This is the system's "board-state reset / momentum spike" moment and is designed to create a clearly distinct battlefield pattern from normal surges.

### Tooling and Readability Support

- Tower tooltips enumerate possible surge outcomes from current supported mods.
- Bot simulation executes full spectacle gameplay payloads, so automation reflects real surge/global behavior.
- Bot analytics track surge/global trigger distributions for balancing and regression checks.

---

## Downloads

Compiled Windows & Android downloads can be found at https://dextrome.itch.io/slot-theory

---

## Core Loop

1. Draft a card between waves.
2. Place a tower or assign a modifier in the world.
3. Run the wave (auto-combat).
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
- Bonus picks:
  - Wave 1: +1 pick
  - Wave 15: +1 pick
- Anti-brick: modifier options are only offered if at least one tower can still accept one

Modifier assignment uses `Preview -> Confirm`:

1. Pick modifier
2. Tap valid tower slot to preview
3. Tap same slot again to confirm
4. Tap elsewhere to cancel preview
5. Cancel button (or back/Esc) to return to draft cards

---

## Towers

| Tower | Damage | Attack Interval | Range | Notes |
|---|---:|---:|---:|---|
| Rapid Shooter | 10 | 0.45 s | 285 px | Fast pressure |
| Heavy Cannon | 52 | 2.0 s | 238 px | Big burst hits |
| Marker Tower | 7 | 1.0 s | 333 px | Applies Marked |
| Arc Emitter | 18 | 1.2 s | 257 px | Base chain: 2 extra bounces, 400 px chain range |
| Rift Sapper | 20 | 0.98 s | 230 px | Charged mine trap tower with setup/burst playstyle |

Unlock flow:
- Arc Emitter: beat the first campaign map on Normal or Hard
- Rift Sapper: beat the second campaign map on Normal or Hard

### Rift Sapper Mechanics

- Internal tower ID: `rift_prism` (display name: Rift Sapper)
- Passive behavior: plants mines on valid lane anchors in range
- Mine cap: 7 active mines per Rift Sapper
- Charge system:
  - each mine has 3 charges
  - trigger 1-2: 0.65x base mine damage
  - trigger 3 (final): 1.15x base mine damage
  - non-final triggers rearm for 0.18 s before they can trigger again
- Damage basis:
  - base mine multiplier: 1.00x tower base damage (before charge-stage multipliers)
  - split mini-mine damage scale: 0.55x of source mine scale
- Trigger/blast:
  - trigger radius: 32 px
  - blast target radius: 82 px
- Placement tuning:
  - anchor sampling step: 26 px
  - minimum spacing between mines: 46 px
  - split-mini plant radius: 104 px
- Wave-start burst seeding:
  - first 2.4 s of each wave uses a faster plant interval (x0.55)
  - capped to 3 burst-speed plants per tower per wave
- Modifier interaction rules:
  - Split Shot: extra mini-mines are spawned only on final-charge pops
  - Chain Reaction: mine-to-mine chaining and enemy chain pops occur only on final-charge pops

---

## Modifiers

- Momentum: +16% per consecutive hit stack, max 5 stacks (x1.80)
- Overkill: 60% excess kill damage spills forward
- Exploit Weakness: +60% vs Marked
- Focus Lens: +125% damage, x2 attack interval
- Chill Shot: -25% move speed for 5 s (stacking on same tower)
- Overreach: +40% range, -20% damage
- Hair Trigger: +35% attack speed, -18% range
- Split Shot: 2 split projectiles at 35% each (280px search radius); extra copies add extra split
- Feedback Loop: kill reduces current cooldown by 50%
- Chain Reaction: +1 bounce per copy, sets chain carry to 60%

Max modifiers per tower: 3

---

## Enemies

- Basic Walker:
  - HP: `65 * 1.10^(wave-1)`
  - Speed: 120 px/s
  - Leak cost: 1 life
- Armored Walker:
  - HP: 3.5x Basic
  - Speed: 60 px/s
  - Leak cost: 2 lives
  - First appears wave 6
- Swift Walker:
  - HP: 1.5x Basic
  - Speed: 240 px/s
  - Leak cost: 1 life
  - Appears waves 10-14

Waves 12-14 can use clumped Armored blocks.

---

## Targeting Modes

- First: highest path progress
- Strongest: highest current HP
- Lowest HP: lowest current HP

Modes cycle on tower click/tap during waves.

Rift Sapper uses tower-specific labels/icons for the same 3 internal modes:
- Random (die icon) = internal First
- Closest (down-arrow icon) = internal Strongest
- Furthest (up-arrow icon) = internal Lowest HP

---

## UI and Polish Highlights

- Neon synthwave visual theme
- Staggered flip reveal draft cards
- Modifier proc halo + live icon pulse
- Kill hitstop and tower recoil feedback
- Target-acquire pings for readability
- Combat callouts for major procs
- Build name + run story on end screen

---

## Settings

- Master / Music / FX sliders (saved to `user://settings.cfg`)
- Display mode toggle
- Difficulty: Easy / Normal / Hard

Easy multipliers:
- Enemy HP: 1.0x
- Enemy count: 1.0x
- Spawn interval: 1.0x

Normal multipliers:
- Enemy HP: 1.05x
- Enemy count: 1.05x
- Spawn interval: 0.97x

Hard multipliers:
- Enemy HP: 1.1x
- Enemy count: 1.1x
- Spawn interval: 0.94x

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

Current strategy set:
- `Random`
- `TowerFirst`
- `GreedyDps`
- `MarkerSynergy`
- `ChainFocus`
- `SplitFocus`
- `HeavyStack`
- `RiftPrismFocus`
- `SpectacleSingleStack`
- `SpectacleComboPairing`
- `SpectacleTriadDiversity`

Optional strategy pool selection:

```text
--strategy_set all|optimization|edge
```

Strategy pools:
- `all`: all 11 strategies above (default)
- `optimization`: `Random`, `GreedyDps`, `MarkerSynergy`, `SplitFocus`, `RiftPrismFocus`, `SpectacleSingleStack`, `SpectacleComboPairing`, `SpectacleTriadDiversity`
- `edge`: `TowerFirst`, `ChainFocus`, `HeavyStack`

Bot summary now includes spectacle trigger analytics by tier and effect mix. Surge/global surge metrics are the primary balance signals.

### Bot Metrics JSON Export

Write machine-readable balancing metrics:

```text
--scene res://Scenes/Main.tscn -- --bot --runs 120 --bot_metrics_out release/bot_metrics.json
```

Optional live replay/event trace capture (same event schema as Combat Lab scenario traces):

```text
--scene res://Scenes/Main.tscn -- --bot --runs 120 --bot_trace_out release/bot_trace.json
```

Optional tuning profile for this run:

```text
--scene res://Scenes/Main.tscn -- --bot --runs 120 --tuning_file Data/combat_lab/sample_tuning.json --bot_metrics_out release/bot_metrics_tuned.json
```

Use optimization-only strategy pool (recommended for tuning runs):

```text
--scene res://Scenes/Main.tscn -- --bot --runs 120 --strategy_set optimization --bot_metrics_out release/bot_metrics_opt_pool.json
```

The JSON summary includes:
- win rate / average wave reached / run duration
- surges + major surges per run
- kills per surge
- explosion damage per run + per trigger
- status detonation counts
- residue uptime
- DPS split (`base_attacks`, `surge_core`, `explosion_follow_ups`, `residue`)
- frame-stress peaks (`simultaneous_explosions`, `simultaneous_active_hazards`, `simultaneous_hitstops_requested`)

## Combat Lab Automation

Run scripted scenario validations (gameplay effect + trace invariants):

```text
--scene res://Scenes/Main.tscn -- --lab_scenario Data/combat_lab/core_scenarios.json --lab_out release/combat_lab_report.json
```

Run a tuning sweep against those scenarios:

```text
--scene res://Scenes/Main.tscn -- --lab_sweep Data/combat_lab/sample_sweep.json --lab_out release/combat_lab_sweep.json
```

Compare baseline vs tuned bot metrics:

```text
--scene res://Scenes/Main.tscn -- --metrics_delta release/bot_metrics.json release/bot_metrics_tuned.json --delta_out release/bot_metrics_delta.txt
```

Sample files:
- `Data/combat_lab/core_scenarios.json`
- `Data/combat_lab/sample_sweep.json`
- `Data/combat_lab/sample_tuning.json`

---

Engine: Godot 4.6 + .NET 8
