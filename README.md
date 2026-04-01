![Slot Theory](icon2.png)

# Slot Theory

A constraint-driven drafting tower defense game by 7ants Studios. Build a 6-slot loadout across 20 waves, survive to win.

Platforms: Windows (Steam), Android (phone and tablet)

---

## Core Loop

1. Play → **Mode Select** → Campaign or Skirmish.
2. Draft a card between waves.
3. Place a tower or assign a modifier in the world.
4. Run the wave (auto-combat - no direct input during waves).
5. Repeat until wave 20 clear (win) or lives reach 0 (loss).

Starting lives: Easy 25 · Normal 20 · Hard 15

## Handcrafted Skirmish Maps

Current handcrafted map roster (in display order):

- `orbit` - Spiral-in slingshot that rewards interior multi-pass coverage.
- `crossroads` - Mid-lane crossover pressure with high-value center control.
- `pinch_bleed` - Asymmetric choke map that punishes weak pinch setups.
- `ridgeback` - Alternating ridge control with repeated handoffs in lane priority.
- `double_back` - Pocket detours and return passes that reward crossing coverage.
- `crossfire` - Diagonal slash geometry with high-value re-entry angles.
- `threshold` - Long perimeter sweep with interior recapture opportunities.
- `switchback` - Parallel channel descent with mirrored return pressure.
- `ziggurat` - Apex crossing map with repeated diagonal convergence.
- `hourglass` - Twin wing kill-zones feeding a narrow center pinch.
- `perimeter_lock` - Fortress perimeter route with a late inward breach.
- `trident` - Three long prongs that collapse into concentrated exit pressure.

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
| Arc Emitter | 17.25 | 1.26 s | 257 px | Chains to 2 extra enemies, 400 px chain range, 60% damage carry per bounce |
| Rift Sapper | 22 | 0.98 s | 230 px | Charged lane-mine trap tower with wave-start rapid seeding |
| Accordion Engine | 14 | 3.2 s | 290 px | Pulses all in-range enemies simultaneously; compresses their progress spacing |
| Phase Splitter | 20 | 0.95 s | 275 px | One shot hits first and last enemies in range at 65% per target; backline/frontline pressure specialist |
| Rocket Launcher | 34 | 1.45 s | 248 px | Rocket Launcher fires explosive rockets that damage the target and nearby enemies. Burst Core further expands the blast radius. |
| Undertow Engine | 8 | 2.35 s | 265 px | Pull/control specialist that drags enemies backward in path progress |
| Latch Nest | 11 | 1.05 s | 255 px | Attrition parasite tower. Primary pod is a full hit; attached parasites tick as secondary hits over time. |

Unlock flow (via Campaign mode):
- Arc Emitter: beat the first campaign map on any difficulty (`ARC_UNSEALED`)
- Split Shot modifier: beat the second campaign map on any difficulty (`SPLIT_UNSEALED`)
- Rift Sapper: beat the third campaign map on any difficulty (`RIFT_UNSEALED`)
- Blast Core modifier: beat the fourth campaign map - Ridgeback / Iron Mandate (`BLAST_UNSEALED`)
- Wildfire modifier: beat the sixth campaign map (`WILDFIRE_UNSEALED`)
- Afterimage modifier: beat Perimeter Lock on any difficulty (`AFTERIMAGE_UNSEALED`)
- Accordion Engine: beat the fifth map - Double Back (`ACCORDION_UNSEALED`)
- Rocket Launcher: beat Hourglass on any difficulty (`ROCKET_UNSEALED`)
- Undertow Engine: beat Trident on any difficulty (`UNDERTOW_UNSEALED`)
- Latch Nest: beat Ziggurat on any difficulty (`LATCH_UNSEALED`)

### Rocket Launcher Mechanics

- Internal tower ID: `rocket_launcher` (display name: Rocket Launcher)
- Rocket Launcher fires explosive rockets that damage the target and nearby enemies.
- Burst Core further expands the blast radius.
- Fires a visible rocket projectile toward a target in range
- On impact:
  - primary target takes full hit damage
  - nearby enemies take built-in splash damage (`55%` of the final primary hit)
- Base splash radius: `88 px`
- Blast Core synergy: each Blast Core copy increases Rocket Launcher splash radius by `+24 px`
- Splash is native to the base tower and does not require any modifier to function

### Undertow Engine Mechanics

- Internal tower ID: `undertow_engine` (display name: Undertow Engine)
- Role: battlefield control, not raw DPS
- Default target behavior strongly prefers the furthest-progressed enemy in range
- On activation:
  - applies a heavy slow during the drag
  - rewinds the enemy by reducing **path progress** (safe on curved/snake/zigzag maps)
- Optional endpoint compression pulse:
  - tiny base tug at pull completion
  - strengthened by Blast Core copies
- Secondary interactions:
  - Split Shot: weaker tug on one nearby secondary target
  - Chain Reaction: weaker linked tug on one nearby target
  - Feedback Loop: chance for a delayed follow-up tug
- Anti-abuse safeguards:
  - hard pull-distance cap
  - diminishing returns on recently-pulled targets
  - short retarget lockout window
  - concurrent-stack decay if multiple Undertow effects overlap the same target
  - resistance multipliers on heavy/elite archetypes

### Latch Nest Mechanics

- Internal tower ID: `latch_nest` (display name: Latch Nest)
- Role: sustained parasite attrition vs durable targets
- Fires a parasite pod as its normal attack
- Primary impact is a normal full hit (`isChain=false`) and runs the full pipeline
- On successful impact, parasites attach to living hosts:
  - max active per tower: `6`
  - max parasites per host from the same tower: `2`
  - parasite duration: `7.0 s`
  - parasite bite interval: `0.45 s`
- Parasite bites are secondary hits (`isChain=true`) and use normal modifiers naturally
- Chain-style bite hits suppress Blast Core/Wildfire/Afterimage/Overkill OnHit behavior and do not satisfy Reaper Protocol primary-only kill credit

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
| Overkill | 100% of excess kill damage spills to next enemy in lane |
| Exploit Weakness | +45% damage vs Marked enemies |
| Focus Lens | +140% damage, ×1.85 attack interval |
| Chill Shot | On hit: 0.70× enemy speed for 6 s; stacks multiplicatively per tower |
| Overreach | +45% range, -10% damage |
| Hair Trigger | +30% attack speed, -18% range |
| Split Shot | Fires 2 split projectiles at 28% damage each; each extra copy adds +1 projectile |
| Feedback Loop | On kill: instantly reset cooldown + 20% attack speed for 4 s |
| Chain Reaction | +1 chain bounce per copy, 50% damage carry per bounce |
| Blast Core | On hit: 45% splash in 140 px radius; each extra copy adds +25 px radius. On Rocket Launcher, also expands native rocket splash radius by +24 px per copy |
| Wildfire | On primary hit: ignite for 4 s burn at 25% BaseDamage/s; burning enemies drop fire trail segments (2.2 s, 30 px radius, 40% burn DPS to overlapping enemies); stacks add burn DPS |
| Afterimage | Hits leave a ghost imprint. After a short delay, the imprint triggers one weaker replay from that spot (not a lingering hazard). Echo hits use the tower's current modifiers, but do not seed new Afterimages |

Max modifiers per tower: 3

Unlockable modifiers:
- Split Shot: beat the second campaign map on Normal or Hard (`SPLIT_UNSEALED`)
- Blast Core: beat the fourth campaign map - Ridgeback / Iron Mandate (`BLAST_UNSEALED`)
- Wildfire: beat the sixth campaign map (`WILDFIRE_UNSEALED`)
- Afterimage: beat Perimeter Lock on any difficulty (`AFTERIMAGE_UNSEALED`)

Full-game only modifiers (not in demo):
- Blast Core
- Wildfire
- Afterimage

---

## Enemies

| Enemy | HP | Speed | Leak Cost | Notes |
|---|---|---|---:|---|
| Basic Walker | `65 × 1.10^(wave-1)` | 120 px/s | 1 | baseline |
| Armored Walker | 3.5× Basic HP | 60 px/s | 2 | first appears wave 6 |
| Swift Walker | 1.5× Basic HP | 240 px/s | 1 | waves 10-19 (skips wave 12 and 20) |
| Reverse Walker | 1.35× Basic HP | 108 px/s | 1 | full game: appears from wave 11 |
| Splitter Walker | 1.8× Basic HP | 90 px/s | 3 | waves 9-15, splits on death |
| Splitter Shard | 0.55× Basic HP | 165 px/s | 1 | spawned by Splitter death |
| Shield Drone | 1.8× Basic HP | 85 px/s | 1 | full game: waves 9-20; projects 35% damage reduction aura to allies within 140 px |

- Reverse Walker rewinds on heavy single-hit bursts (`>=10%` max HP in one hit); rewinds are cooldown-gated and capped per enemy.

---

## Targeting Modes

Regular towers cycle through 4 modes on click/tap during waves:

- Right arrow icon - **First**: highest path progress (closest to exit)
- Star icon - **Strongest**: highest current HP
- Down arrow icon - **Lowest HP**: lowest current HP
- Left arrow icon - **Last**: lowest path progress (trailing enemy in range)

Rift Sapper uses tower-specific labels/icons for its own 3 internal modes (no Last mode):
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

## Endless Mode

After clearing all 20 waves, a **Continue - Endless** button appears on the win screen. Pressing it continues the run from wave 21 with no upper limit.

Scaling per wave past wave 20 (depth = waves past 20):

| What scales | Rate |
|---|---|
| Enemy count | ×1.05 per wave (compounding) |
| Enemy HP | ×1.02 per wave (compounding) |
| Swift Walkers | +1 extra every 5 endless waves |
| Reverse Walkers (full game) | +1 extra every 6 endless waves |

- Spawn interval shrinks slowly, floored at 0.70 s.
- HUD shows "Wave 21 ∞", "Wave 22 ∞", etc.
- Losing ends the run normally. The endless wave reached is shown on the loss screen.
- The wave-20 win is not submitted to the global leaderboard if you continue - only the endless result is submitted.

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

The banner label and visual treatment are driven by which mods contributed most to filling the global meter. The dominant mod maps to one of 3 genuinely distinct feel types:

- **PRESSURE SURGE** (Momentum, Chill Shot, Overreach, Afterimage) - extended control: marks and slows run 1.5× longer with deeper slow factor. Arcs flood from every tower to every live enemy. Cold blue tint, wide slow ripples.
- **CHAIN SURGE** (Exploit Weakness, Split Shot, Chain Reaction, Reaper Protocol, Deadzone) - spreading chain: arcs jump enemy-to-enemy in progress order. Normal damage, full cooldown refund. Electric purple tint, balanced ripples.
- **DETONATION SURGE** (Overkill, Focus Lens, Hair Trigger, Feedback Loop, Blast Core, Wildfire) - heavy burst: 1.35× spike damage, bonus cooldown refund. Radial arcs explode from board center. Hot orange tint, fast tight ripples.
- **Feel preview** - the HUD global meter label transitions to the predicted surge type at ≥70% fill.
- **Multi-color ripples** - up to 3-4 ripple rings reflecting the top contributing mods; width and expansion speed vary by feel.
- **Per-tower identity FX** - each tower type fires its own effect in staggered sequence.
- **Screen-edge vignette** - square-masked overlay ramps in during the final 30% of global meter fill, tinted to the dominant mod's color.
- **Sustained feel tint** - a low-alpha full-screen color wash lingers ~2.4 s after global surge (blue/purple/orange per feel).
- **SURGE ×N chain counter** - a gold callout accumulates at screen center when multiple surges chain within the contribution window.
- **Per-tower afterglow** - each tower involved holds a 2.4 s accent-colored modulate fade after its FX burst.
- **Triad callouts** - combo name and augment name spawn as separate sequential callouts; augment appears below in the augment modifier's own color.

---

## Campaign Mode - The Fracture Circuit

4-stage linear campaign accessible from Play → Mode Select → Campaign.

| # | Stage | Map | Mandate |
|---|---|---|---|
| 1 | Orbit Breach | orbit | Rapid, Heavy, Marker only · Split Shot banned |
| 2 | Crossroads Interdiction | crossroads | + Arc Emitter · Momentum, Exploit Weakness, Split Shot banned |
| 3 | Pinch & Bleed | pinch_bleed | + Arc Emitter · 5 slots only · Rift Sapper banned |
| 4 | Iron Mandate | ridgeback | All towers · Enemies +25% HP |

Stages unlock sequentially. Per-stage clear state persisted per difficulty. Campaign end screen shows sector stamps, "Next Stage →" on win, and "Campaign Select" instead of "Main Menu".

---

## Achievements

34 achievements tracked locally via `AchievementManager`, persisted to `user://achievements.cfg`. Forwarded to Steam when available.

| ID | Name | Condition |
|---|---|---|
| TUTORIAL_COMPLETE | First Steps | Complete the tutorial run |
| FIRST_WIN | First Victory | Complete all 20 waves |
| HARD_WIN | Hard Carry | Complete all 20 waves on Hard |
| ARC_UNSEALED | Arc Unsealed | Beat the first campaign map on any difficulty (unlocks Arc Emitter) |
| SPLIT_UNSEALED | Split Unsealed | Beat the second campaign map on any difficulty (unlocks Split Shot) |
| RIFT_UNSEALED | Rift Unsealed | Beat the third campaign map on any difficulty (unlocks Rift Sapper) |
| ACCORDION_UNSEALED | Accordion Unsealed | Beat the fifth campaign map on any difficulty (unlocks Accordion Engine) |
| BLAST_UNSEALED | Blast Unsealed | Beat the fourth campaign map on any difficulty (unlocks Blast Core) |
| WILDFIRE_UNSEALED | Wildfire Unsealed | Beat the sixth campaign map on any difficulty (unlocks Wildfire) |
| AFTERIMAGE_UNSEALED | Afterimage Unsealed | Beat Perimeter Lock on any difficulty (unlocks Afterimage) |
| PHASE_UNSEALED | Phase Unsealed | Beat the seventh campaign map on any difficulty (unlocks Phase Splitter) |
| REAPER_UNSEALED | Reaper Unsealed | Beat the eighth campaign map on any difficulty (unlocks Reaper Protocol) |
| ROCKET_UNSEALED | Rocket Unsealed | Beat Hourglass on any difficulty (unlocks Rocket Launcher) |
| UNDERTOW_UNSEALED | Undertow Unsealed | Beat Trident on any difficulty (unlocks Undertow Engine) |
| LATCH_UNSEALED | Latch Unsealed | Beat Ziggurat on any difficulty (unlocks Latch Nest) |
| FLAWLESS | Flawless | Win without losing a life |
| LAST_STAND | Last Stand | Win with exactly 1 life remaining |
| HALFWAY_THERE | Halfway There | Survive to wave 10 |
| FULL_HOUSE | Full House | Fill all 6 tower slots in one run |
| STACKED | Stacked | Give any tower 3 modifiers in one run |
| SPEED_RUN | Speed Run | Win in under 15 minutes |
| ANNIHILATOR | Annihilator | Deal 100,000 total damage in one run |
| CHAIN_MASTER | Chain Master | Win with all 6 slots filled by Arc Emitters |
| DEVASTATOR | Devastator | Deal 200,000 total damage in one run |
| KEEP_GOING | Keep Going | Start an endless run |
| ENDLESS_25 | Into the Void | Clear wave 25 in endless mode |
| ENDLESS_30 | No End in Sight | Clear wave 30 in endless mode |
| ENDLESS_40 | The Abyss | Clear wave 40 in endless mode |
| FULL_ARSENAL | Full Arsenal | Use 5 different towers in one run |
| OVER_EQUIPPED | Over Equipped | Fill all 18 modifier slots in one run |
| CHAIN_GANG | Chain Gang | Place 3 or more Arc Emitters in a single run |
| GLASS_CANNON | Glass Cannon | Equip Focus Lens and Hair Trigger on the same tower |
| CAMPAIGN_CLEAR | The Circuit | Clear all four campaign stages |
| CAMPAIGN_HARD_CLEAR | Iron Mandate | Clear all four campaign stages on Hard |

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
- **Achievements**: all 34 achievements forwarded to Steam
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
- `SpectacleSingleStack`, `AccordionEngine`, `PlayerStyleKenny`, `HeavyOverkill`

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

Run tower benchmark prescreen (scenario-based per-tower profiling, JSON + CSV):

```text
--scene res://Scenes/Main.tscn -- --lab_tower_benchmark Data/combat_lab/tower_benchmark_core.json --lab_out release/combat_lab_tower_benchmark.json
```

Run modifier benchmark prescreen (baseline vs modified delta analysis, JSON + CSV):

```text
--scene res://Scenes/Main.tscn -- --lab_modifier_benchmark Data/combat_lab/modifier_benchmark_core.json --lab_out release/combat_lab_modifier_benchmark.json
```

Tower benchmark modeling now includes Undertow control behavior and Latch Nest parasite attrition behavior, so utility/control towers are evaluated beyond raw DPS.

`Data/combat_lab/tower_benchmark_core.json` is the starter suite for:
- single tank target
- small clustered wave
- large swarm wave
- fast fragile enemies
- slow tanky enemies
- short path
- long path
- dense choke
- open lane

`Data/combat_lab/modifier_benchmark_core.json` extends those scenarios with modifier delta contexts and selected pair probes.

The automated tuning pipeline (`run_tuning_pipeline.ps1`) combines bot eval + scenario suite to iteratively optimize difficulty multipliers against win-rate targets.
It can also run tower/modifier benchmark prescreens before full bot metrics (`-SkipTowerBenchmark` / `-SkipModifierBenchmark` to disable).

---

## Tuning Pipeline (`run_tuning_pipeline.ps1`)

Iterative optimizer: generates candidate tuning profiles, scores them against bot win-rate + spectacle targets, keeps the best, and writes the result to `Data/best_tuning_full.json` (or `best_tuning_demo.json` with `-Demo`).

### Quick examples

```powershell
# Default run (200 runs, 4 iterations, optimization strategy set)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1

# Demo build tuning (no Shield Drone / Reverse Walker)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Demo

# Fast exploratory pass
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Runs 60 -Iterations 3 -CandidatesPerIteration 4 -SkipBuild -SkipTrace

# Difficulty-only tuning (freeze spectacle params, Normal only)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -NormalOnlyMode -Runs 120

# High-parallelism run (2 candidates × 4 shards = 8 Godot processes)
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -CandidateParallelism 2 -EvalShardParallelism 4

# Two-pass search: surge parity first, then difficulty
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -TwoPassMode -Runs 300 -Iterations 6

# Spectacle-only tuning pass
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -SpectacleOnlyMode -StrategySet spectacle
```

### Core run controls

| Parameter | Default | Description |
|---|---|---|
| `-Runs` | `200` | Bot runs per candidate evaluation |
| `-Iterations` | `4` | Number of optimization iterations |
| `-CandidatesPerIteration` | `3` | Candidate profiles generated per iteration |
| `-CandidateParallelism` | `1` | Candidates evaluated in parallel |
| `-EvalShardParallelism` | `0` | Parallel Godot shards per candidate eval (0 = inherit from CandidateParallelism) |
| `-TopCandidateReevalCount` | `3` | Top candidates re-evaluated before finalizing iteration best |
| `-SweepRunsPerVariant` | `12` | Runs per variant in the sweep comparison |
| `-Seed` | `1337` | RNG seed for mutation |
| `-MutationStrength` | `1.0` | Scales mutation step sizes across all params |

### Search mode switches

| Flag | Description |
|---|---|
| `-DifficultyOnlyMode` | Mutate only difficulty params (HP/count/spawn + tanky/swift counts); freeze spectacle |
| `-NormalOnlyMode` | Mutate only Normal-difficulty params; freeze Hard curve. Implies `-DifficultyOnlyMode` |
| `-SpectacleOnlyMode` | Mutate only spectacle params; win-rate targets are guardrails only |
| `-FreezeSpectacleParams` | Freeze spectacle params during mutation (subset of `-DifficultyOnlyMode`) |
| `-TwoPassMode` | Pass 1 targets surge parity/fairness; pass 2 re-optimizes difficulty and win-rate |
| `-TwoPassPhaseSplitPercent` | `50` - % of iterations allocated to pass 1 |
| `-TwoPassPass2RunsMultiplier` | `1.5` - multiplies run count during pass 2 only |
| `-Demo` | Sim demo build conditions; writes result to `Data/best_tuning_demo.json` |

### Win-rate targets

| Parameter | Default | Description |
|---|---|---|
| `-TargetWinRateEasy` | `0.90` | Target bot win rate on Easy |
| `-TargetWinRateNormal` | `0.60` | Target bot win rate on Normal |
| `-TargetWinRateHard` | `0.30` | Target bot win rate on Hard |
| `-TargetWinRateTolerance` | `0.06` | Acceptable deviation from win-rate targets |
| `-UseBaselineRelativeWinTargets` | `false` | Derive targets relative to baseline rather than using absolute values |
| `-RelativeWinTargetEasyUplift` | `0.05` | Uplift above baseline for Easy when using relative targets |
| `-RelativeWinTargetNormalUplift` | `0.08` | Uplift above baseline for Normal |
| `-RelativeWinTargetHardUplift` | `0.06` | Uplift above baseline for Hard |

### Spectacle targets

| Parameter | Default | Description |
|---|---|---|
| `-TargetSurgesPerRun` | `36.0` | Target surge trigger count per run |
| `-TargetSurgesPerRunTolerance` | `8.0` | Acceptable deviation |
| `-MaxKillsPerSurge` | `0.70` | Guards against surge-carried wins |
| `-MinGlobalSurgesPerRun` | `1.20` | Minimum global surge triggers per run |
| `-TargetExplosionShare` | `0.02` | Target share of damage from explosions |
| `-TargetExplosionShareTolerance` | `0.05` | Acceptable deviation |

### Fairness / parity guards

| Parameter | Default | Description |
|---|---|---|
| `-TargetMaxTowerSurgeRatio` | `2.0` | Max allowed surge ratio between best/worst tower |
| `-TargetMaxModifierSurgeRatio` | `2.2` | Max allowed surge ratio between best/worst modifier |
| `-TargetTowerWinRateGap` | `0.18` | Max allowed win-rate gap between towers |
| `-TargetModifierWinRateGap` | `0.20` | Max allowed win-rate gap between modifiers |
| `-HardGuardMaxTowerSurgeRatio` | `5.0` | Hard ceiling on tower surge ratio (blocks candidates) |
| `-HardGuardMaxTowerWinRateGap` | `0.28` | Hard ceiling on tower win-rate gap |
| `-MinTowerRunsForFairness` | `40` | Min runs before applying tower fairness scoring |
| `-MinModifierRunsForFairness` | `50` | Min runs before applying modifier fairness scoring |
| `-MinModifierRunsForSurgeParity` | `40` | Min runs before applying surge parity scoring |
| `-MinTowerPlacementsForParity` | `6` | Min placements per tower to include in parity check |
| `-MinSweepScoreRatioVsBaseline` | `1.0` | Candidate must score at least this ratio vs baseline sweep |
| `-DifficultyRegressionTolerance` | `0.01` | Max allowed win-rate regression from iteration to iteration |
| `-NormalRegressionPenaltyWeight` | `260.0` | Scoring penalty weight for Normal regression |
| `-HardRegressionPenaltyWeight` | `320.0` | Scoring penalty weight for Hard regression |
| `-MaxChainDepth` | `4.0` | Guard: max chain bounce depth |
| `-MaxSimultaneousExplosions` | `8` | Guard: max simultaneous explosion events |
| `-MaxSimultaneousHazards` | `12` | Guard: max simultaneous hazard events |
| `-MaxSimultaneousHitStops` | `4` | Guard: max simultaneous hit-stop events |
| `-MinRunDurationSeconds` | `900.0` | Guard: discard runs shorter than this (likely crashes) |

### Skip flags

| Flag | Description |
|---|---|
| `-SkipBuild` | Skip `dotnet build` at start (use when code hasn't changed) |
| `-SkipTrace` | Skip final live bot trace capture |
| `-SkipTowerBenchmark` | Skip tower benchmark prescreen |
| `-SkipModifierBenchmark` | Skip modifier benchmark prescreen |
| `-SkipAllStrategyValidation` | Skip final all-strategy validation pass |

### Strategy and paths

| Parameter | Default | Description |
|---|---|---|
| `-StrategySet` | `"optimization"` | Bot strategy pool: `optimization`, `all`, `edge`, `spectacle` |
| `-GodotPath` | *(auto-detected)* | Override Godot executable path |
| `-TuningFile` | *(auto-generated seed)* | Seed tuning JSON to start from |
| `-ScenarioFile` | `Data/combat_lab/core_scenarios.json` | Scenario suite file |
| `-TowerBenchmarkFile` | `Data/combat_lab/tower_benchmark_core.json` | Tower benchmark file |
| `-ModifierBenchmarkFile` | `Data/combat_lab/modifier_benchmark_core.json` | Modifier benchmark file |
| `-OutputRoot` | `release/tuning_pipeline` | Directory for all pipeline output |

---

Engine: Godot 4.6 + .NET 8
