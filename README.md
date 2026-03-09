# Slot Theory

A drafting tower defense game. Build a 6-slot loadout, survive 20 waves.

Platforms: Windows Desktop, Android (phone and tablet)

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
- Feedback Loop: kill reduces current cooldown by 25%
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
- Difficulty: Normal / Hard

Normal multipliers:
- Enemy HP: 1.05x
- Enemy count: 1.05x
- Spawn interval: 0.95x (slightly faster spawns)

Hard multipliers:
- Enemy HP: 1.1x
- Enemy count: 1.2x
- Spawn interval: 0.9x (faster spawns)

---

## Bot Mode

Run automated balance tests:

```text
--scene res://Scenes/Main.tscn -- --bot --runs N
```

Bot rotates strategies across maps and difficulties and prints summary stats.

---

Engine: Godot 4.6 + .NET 8
