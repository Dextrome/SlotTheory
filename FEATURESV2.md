# Slot Theory -- Feature Reference v2

Cross-referenced against the live codebase. All stats from `Data/` JSON files and `Balance.cs`.

---

## Table of Contents

1. [Core Loop & Game Rules](#1-core-loop--game-rules)
2. [Tower Roster](#2-tower-roster)
3. [Modifier Roster](#3-modifier-roster)
4. [Enemy Roster](#4-enemy-roster)
5. [Draft System](#5-draft-system)
6. [Combat Model](#6-combat-model)
7. [Targeting & Statuses](#7-targeting--statuses)
8. [Wave System & Difficulty](#8-wave-system--difficulty)
9. [Campaign Mode](#9-campaign-mode)
10. [Unlockable Content](#10-unlockable-content)
11. [Endless Mode](#11-endless-mode)
12. [Surge & Spectacle System](#12-surge--spectacle-system)
13. [Achievement System](#13-achievement-system)
14. [Leaderboards & Scores](#14-leaderboards--scores)
15. [UI & Screens](#15-ui--screens)
16. [Bot Mode & Tuning Pipeline](#16-bot-mode--tuning-pipeline)
17. [Procedural Music System](#17-procedural-music-system)
18. [Technical Architecture](#18-technical-architecture)
19. [Platform & Build](#19-platform--build)

---

## 1. Core Loop & Game Rules

**Genre:** Constraint-driven drafting tower defense.

**Loop per wave:**
1. Draft phase: player picks 1 card from 4-5 options (tower or modifier)
2. Placement phase: place tower in a world slot, or assign modifier to a placed tower
3. Wave runs automatically -- no direct input during combat
4. After wave: next draft begins

**Win condition:** Survive all 20 waves (enemies reach 0 alive, quota exhausted).
**Loss condition:** Lives reach 0 -- each enemy that exits the lane costs 1 life. Armored Walker costs 2.

**Starting lives by difficulty:**

| Difficulty | Starting Lives |
|---|---:|
| Easy | 25 |
| Normal | 20 |
| Hard | 15 |

**Run limits:**
- 20 waves per run
- 6 tower slots
- Max 3 modifiers per tower (`Balance.MaxModifiersPerTower`)
- No mid-wave player input, no tower selling, no rerolls

**Key implementation files:**
- `GameController.cs` -- run lifecycle state machine (DraftPhase -> WavePhase -> Win/Lose)
- `RunState.cs` -- single source of truth for all runtime data
- `Balance.cs` -- all tunable constants

---

## 2. Tower Roster

9 towers total. 5 available from the start; 4 unlockable via campaign progression.

### Base Towers

| Tower | Internal ID | Damage | Interval | Range | Notes |
|---|---|---:|---:|---:|---|
| Rapid Shooter | `rapid_shooter` | 10 | 0.45 s | 285 px | Fast single-target fire |
| Heavy Cannon | `heavy_cannon` | 56 | 2.0 s | 238 px | Slow, high-burst impact shots |
| Marker Tower | `marker_tower` | 7 | 1.0 s | 333 px | Applies Marked status on hit (+40% damage taken, 4 s) |
| Rocket Launcher | `rocket_launcher` | 34 | 1.45 s | 248 px | Visible rocket projectile; full primary hit + built-in radial splash |
| Undertow Engine | `undertow_engine` | 8 | 2.35 s | 265 px | Pull/control tower that drags enemies backward in path progress |

### Unlockable Towers

| Tower | Internal ID | Damage | Interval | Range | Unlock |
|---|---|---:|---:|---:|---|
| Arc Emitter | `chain_tower` | 17.25 | 1.26 s | 257 px | ARC_UNSEALED (1st campaign map) |
| Rift Sapper | `rift_prism` | 22 | 0.98 s | 230 px | RIFT_UNSEALED (3rd campaign map) |
| Accordion Engine | `accordion_engine` | 14 | 3.2 s | 290 px | ACCORDION_UNSEALED (Double Back map) |
| Phase Splitter | `phase_splitter` | 20 | 0.95 s | 275 px | PHASE_UNSEALED (7th campaign map / `threshold` fallback) |

### Arc Emitter (chain_tower) -- Chain Mechanics
- Primary target receives full hit
- Bounces to 2 additional enemies within **400 px** chain range
- Each bounce: `0.6x` damage carry (40% decay per bounce)
- Chain targets receive `isChain: true` -- Blast Core and Overkill OnHit are skipped on them

### Rift Sapper (rift_prism) -- Mine Mechanics
- Passive placement: plants mines at lane anchor points in range; no target needed to act
- Mine cap: **7 active mines per tower** (`Balance.RiftMineMaxActivePerTower`)
- Each mine has **3 charges** (`Balance.RiftMineChargesPerMine`):
  - Triggers 1-2: `0.65x` base damage (`Balance.RiftMineTickDamageMultiplier`), rearm delay 0.18 s
  - Trigger 3 (final pop): `1.15x` base damage (`Balance.RiftMineFinalDamageMultiplier`)
- Wave-start burst seeding: first **2.4 s** of each wave plants at `0.55x` normal interval, capped at +3 accelerated plants per tower per wave
- Modifier interactions on mines:
  - Split Shot and Chain Reaction only trigger on final-charge pops
  - Chain-triggered mine pops are forced to final pop to preserve cascades
- Targeting modes re-labeled on this tower (die = Random, down-arrow = Closest, up-arrow = Furthest)

### Accordion Engine (accordion_engine) -- Pulse Mechanics
- On each pulse: collects **all in-range alive enemies**, sorts by Progress (trailing -> leading)
- **Formation compression:** enemies with >= 2 in range have their Progress values compressed toward the group median by `0.25x` factor (75% spread reduction), enforcing 8 px minimum spacing (`AccordionFormation.Compress`)
- **Primary target** (leading enemy, highest Progress): `isChain: false` -- full modifier pipeline including Blast Core and Overkill OnHit
- **Secondary targets** (all others in range): `isChain: true` -- stat modifiers apply, OnHit effects that opt out (Blast Core, Overkill) are skipped
- Chain bounces from ChainReaction fire from the primary target only
- VFX: contracting ring via `AccordionPulseVfx.cs` (violet identity)
- Compression is pure Progress manipulation (not a slow, stun, or crowd-control -- enemies continue moving)

### Phase Splitter (phase_splitter) -- Dual-End Targeting
- On each attack, acquires two endpoints in range:
  - **First** target: highest ProgressRatio (front-most)
  - **Last** target: lowest ProgressRatio (rear-most)
- Each endpoint hit is a full primary hit context with `isChain: false` and `0.65x` damage (`Balance.PhaseSplitterDamageRatio`)
- If only one valid target exists, fires one hit only (no duplicate/double-hit)
- Modifier pipeline is run per endpoint hit independently:
  - independent crit/modify damage path
  - independent OnHit / OnKill hooks
  - Chain Reaction can bounce from both endpoints
  - Blast Core uses both endpoint impacts as separate splash anchors
  - Wildfire can ignite both endpoints independently
- Split Shot on Phase Splitter applies one extra split per copy on each endpoint hit (4-hit pattern with one Split Shot copy)
- Visual/audio identity:
  - one phase-split emission event with linked dual-end beams (`PhaseSplitVfx`)
  - shared linked impact cue (not two unrelated projectile shots)

### Rocket Launcher (rocket_launcher) -- Built-In Splash Identity
- Fires a visible rocket projectile toward a target in range
- On impact:
  - primary target takes full hit damage
  - nearby enemies take built-in splash damage
- Native splash profile:
  - base splash radius: `88 px`
  - splash damage: `55%` of the final primary hit damage
- Blast Core synergy:
  - each Blast Core copy adds `+24 px` to Rocket Launcher native splash radius
- Splash is part of the base kit and applies on primary hits only (`isChain: false`)

### Undertow Engine (undertow_engine) -- Reverse-Current Control Mechanics
- Core identity: positional control via path-progress rewind, not DPS racing
- Primary target selection:
  - candidate pool: alive enemies in range
  - default behavior strongly prefers highest Progress (lead enemy)
  - deterministic ties: HP, then instance-id
- Primary activation:
  - applies normal primary-hit damage context (for modifier compatibility)
  - starts undertow drag with heavy slow while active
  - drag rewinds **Progress** directly (safe on curves/zigzags; no world-space push hack)
- Base control profile (`Balance.cs`):
  - duration: `0.78 s`
  - pull distance: `84 px`
  - hard cap: `112 px`
  - min effective pull: `12 px` (tiny pulls are discarded)
  - slow factor during drag: `x0.28`
- Anti-abuse safeguards:
  - recent-target diminishing returns window: `3.0 s` (down to `0.42x` pull)
  - short retarget lockout: `0.85 s`
  - concurrent overlap decay: extra undertow stacks on same target multiply by `0.36x` each
  - resistance multipliers on heavy archetypes (armored/reverse/shield/splitter classes)
  - pull always clamped to valid path range (`Progress >= 0`)
- Secondary/control interactions:
  - Split Shot: optional nearby secondary tug at `0.46x` strength
  - Chain Reaction: optional nearby linked tug at `0.56x` strength
  - Feedback Loop: chance for delayed follow-up tug (`0.38 s` delay, `0.40x` strength)
  - Blast Core: strengthens endpoint compression pulse (radius + pull)
  - Focus Lens / Chill Shot: strengthen undertow pull and/or slow profile
- Endpoint compression:
  - at drag completion, optional local pulse tugs nearby enemies backward and applies a short lingering slow to secondaries
  - this creates re-clumping pressure without hard stuns

---

## 3. Modifier Roster

13 modifiers total. 2 unlockable, 1 full-game-only wave-gated. All equipped via draft; max 3 per tower.

| Modifier | ID | Effect | Unlock |
|---|---|---|---|
| Momentum | `momentum` | +16% dmg per consecutive hit on same target, max x1.8; resets on target switch | -- |
| Overkill | `overkill` | 100% excess kill damage spills to next enemy in lane | -- |
| Exploit Weakness | `exploit_weakness` | +45% dmg vs Marked enemies | -- |
| Focus Lens | `focus_lens` | +140% damage, x1.85 attack interval (slower) | -- |
| Chill Shot | `slow` | On hit: x0.70 enemy speed for 6 s; stacks multiplicatively per tower | -- |
| Overreach | `overreach` | +45% range, -10% damage | -- |
| Hair Trigger | `hair_trigger` | +30% attack speed, -18% range | -- |
| Feedback Loop | `feedback_loop` | On kill: instantly reset cooldown + +20% attack speed for 4 s | -- |
| Chain Reaction | `chain_reaction` | On hit: +1 bounce at 50% damage carry; each extra copy adds +1 bounce | -- |
| Split Shot | `split_shot` | On hit: fires 2 extra projectiles at nearby enemies at 28% damage; each extra copy adds +1 projectile | SPLIT_UNSEALED |
| Blast Core | `blast_core` | On primary hit: 45% splash in 140 px radius; each extra copy adds +25 px radius | BLAST_UNSEALED |
| Wildfire | `wildfire` | On primary hit: ignite for 4 s burn at 25% BaseDamage/s; burning enemies drop fire trail segments (2.2 s, 30 px radius, 40% burn DPS to overlapping enemies); stacks add burn DPS | -- |
| Reaper Protocol | `reaper_protocol` | Kill (primary only): first 5 kills per wave restore 1 life each (capped at MaxLives). **Full game only. Available from wave 10 onward.** | -- |

### Modifier isChain Rules

Modifiers that opt out of chain/secondary targets set `ApplyToChainTargets = false`. `DamageModel.Apply` skips their `OnHit` when `ctx.IsChain == true`:

| Modifier | ApplyToChainTargets | OnHit fires on secondaries? |
|---|---|---|
| Blast Core | **false** | No -- splash only on primary hits |
| Wildfire | **false** | No -- ignition only on primary hits |
| Overkill | **false** | No -- kill spill only on primary kills |
| Reaper Protocol | true (OnKill only) | OnKill checks `ctx.IsChain` internally -- chain kills are rejected at the modifier level, not via `ApplyToChainTargets` |
| All others | true | Yes |

### Stacking

All modifier effects stack additively within their category (no multiplicative explosion). For range/interval stat modifiers, each copy applies its multiplier via `OnEquip` to the tower's base stat at equip time.

---

## 4. Enemy Roster

7 enemy types total. 5 in demo build; Shield Drone and Reverse Walker are full-game only.

| Enemy | HP Multiplier | Speed | Leak Cost | Appearance | Notes |
|---|---|---:|---:|---|---|
| Basic Walker | `65 x 1.10^(wave-1)` | 120 px/s | 1 | All waves | Baseline enemy |
| Armored Walker | 3.5x Basic HP | 60 px/s | 2 | Wave 6+ | Counts as 2 lives on exit |
| Swift Walker | 1.5x Basic HP | 240 px/s | 1 | Waves 10-19 (skip 12, 20) | Fast fragile |
| Splitter Walker | 1.8x Basic HP | 90 px/s | 3 | Waves 9-15 | Spawns 2 Splitter Shards on death |
| Splitter Shard | 0.55x Basic HP | 165 px/s | 1 | On Splitter death | Inherit split path |
| Shield Drone | 1.8x Basic HP | 85 px/s | 1 | Waves 9-20 (full game) | Projects 35% damage reduction aura to allies within 140 px |
| Reverse Walker | 1.35x Basic HP | 108 px/s | 1 | Wave 11+ (full game) | Jumps backward (-Progress) when hit for >=10% max HP in one shot; cooldown-gated |

**HP scaling:** every wave the Basic Walker baseline grows by x1.10. All HP-multiplier types use the current-wave baseline.

**Shield Drone protection:** `35%` damage reduction (`Balance.ShieldDroneProtectionReduction`) applied per hit to any allied enemy within 140 px -- including splash (Blast Core respects it) and mine pops.

**Demo gating:** `DataLoader.GetWaveConfig()` zeros Shield Drone and Reverse Walker counts in demo builds. `WaveSystem.BuildEndlessWaveConfig()` also suppresses them in demo.

---

## 5. Draft System

### Draft Options

| Condition | Options Count | Composition Target |
|---|---|---|
| At least one free slot | 5 cards | 2 towers + 3 modifiers (best-effort) |
| All 6 slots filled | 4 cards | All modifiers |

- If fewer than 2 applicable modifiers exist (no tower can accept one), tower cards backfill the gap
- Constant: `Balance.DraftModifierOptionsFull = 4`
- Bonus extra picks: `Balance.Wave1ExtraPicks` and `Balance.Wave15ExtraPicks` (both currently 0)

### Anti-Brick Rules

`DraftSystem` enforces: a modifier card is never offered if no placed tower can still accept it (tower at modifier cap, or modifier already equipped on the only eligible tower). Swap for an applicable card instead.

Coverage: `DraftAntiBrickTests.cs` (critical -- a mismapped modifier can silently brick future drafts).

### Unlockable Gating in Draft

If a tower or modifier is locked (`Unlocks.IsTowerUnlocked` / `Unlocks.IsModifierUnlocked` returns false), it is excluded from `DraftSystem.BuildOptions()`. Bot mode always returns `true` regardless of lock state.

---

## 6. Combat Model

### Simulation Loop (per frame, `CombatSim.Step`)

1. Spawn enemies per wave interval until wave quota reached
2. Enemies self-move via `EnemyInstance._Process()` -- `Progress += Speed * delta` (PathFollow2D absolute offset)
3. For each tower: decrement cooldown -> acquire target -> run damage pipeline -> reset cooldown
4. Remove dead enemies (Hp <= 0)
5. Wave end: quota spawned **and** no enemies alive
6. Loss check: player Lives reach 0

### Damage Pipeline (`DamageModel.Apply`)

A `DamageContext` flows through five stages:

1. **`ModifyAttackInterval`** -- stat modifiers adjust the interval (applied at cooldown reset, not here)
2. **`ModifyDamage`** -- stat/conditional damage multipliers (Momentum stacks, Exploit Weakness vs Marked, Focus Lens)
3. **Marked + DamageAmp bonus** -- if target is Marked, +40% damage applied
4. **`OnHit`** -- on-hit effects (Chill Shot slow, Blast Core splash, Overkill spill, Chain Reaction bounce) -- skipped per-modifier if `IsChain && !mod.ApplyToChainTargets`
5. **`OnKill`** -- on-kill effects (Feedback Loop cooldown reset) -- always run regardless of bounce type

Combat timing is mixed:
- Most standard tower shots spawn `ProjectileVisual` and apply damage on impact.
- Specialized towers (Rift Sapper mines, Accordion Engine pulses, Phase Splitter endpoint hits, Undertow tugs) resolve directly in sim logic.
- Bot mode resolves instantly without spawning visual projectiles for deterministic throughput.

### isChain Flag

`DamageContext.IsChain` controls modifier opt-out:
- `false` = primary hit -> full pipeline
- `true` = chain bounce / Accordion secondary / Overkill spill -> `ApplyToChainTargets=false` mods skip `OnHit`

### Accordion Engine Hit Model

- **Primary target** (leading enemy): `isChain: false`
- **Secondary targets** (all other in-range): `isChain: true`
- Chain bounces (ChainReaction modifier) fire from the primary only, also with `isChain: true`

---

## 7. Targeting & Statuses

### Targeting Modes

Four modes cycle on tower click/tap during waves (regular towers only -- Rift Sapper has 3 custom modes):

| Mode | Icon | Logic |
|---|---|---|
| First | Right-arrow | Highest Progress (closest to exit) |
| Strongest | Star | Highest current HP |
| Lowest HP | Down-arrow | Lowest current HP |
| Last | Left-arrow | Lowest Progress (trailing enemy in range) |

Rift Sapper uses custom labels/icons mapped to its own 3 internal modes (Random/Closest/Furthest via die/down-arrow/up-arrow icons) and does not get the Last mode. Implemented in `TowerInstance.CycleTargetingMode()`.

### Statuses

**Marked** (applied by Marker Tower, 4 s duration):
- `+40% damage taken` from all sources while active
- Interact with: Exploit Weakness (`+45%` multiplicative bonus on top), `DamageModel` step 3

**Slow** (applied by Chill Shot, 6 s duration):
- `x0.70` speed multiplier per tower that applies it
- Stacks **multiplicatively** across towers (two Chill Shot towers -> x0.70 x 0.70 = x0.49 effective speed)

---

## 8. Wave System & Difficulty

### Wave Scaling

- 20 standard waves
- Basic Walker HP: `65 x 1.10^(waveIndex)` (waveIndex 0-based)
- Enemy types introduced at specific waves (see Enemy Roster section)
- Wave config: `WaveSystem.GetWaveConfig(waveIndex)` -> `WaveConfig` struct

### Difficulty Modes

Three modes selectable in Settings. Multipliers apply to all spawned enemies.

| Difficulty | Enemy HP | Enemy Count | Spawn Interval |
|---|---:|---:|---:|
| Easy | 1.0x | 1.0x | 1.0x |
| Normal | 1.2x | 1.05x | 0.95x |
| Hard | 1.3x | 1.1x | 0.90x |

- Easy: no scaling beyond base wave difficulty -- intended for accessibility
- Normal: targets ~75% bot win rate
- Hard: targets ~50% bot win rate
- Multipliers are runtime-overridable via `SpectacleTuning.Current` (used by tuning pipeline)
- `SettingsManager` persists difficulty to `user://settings.cfg`

### Tuning Pipeline

`run_tuning_pipeline.ps1` runs iterative bot eval + scenario suite to converge on difficulty multipliers that hit win-rate targets. Outputs to `Data/best_tuning_full.json` or `Data/best_tuning_demo.json`. Loaded at startup by `GameController._Ready()`.

---

## 9. Campaign Mode

### The Fracture Circuit

4-stage linear campaign. Stages unlock sequentially (each requires the previous cleared on any difficulty). Per-stage, per-difficulty clear state persisted to `user://campaign_progress.cfg`.

| # | Stage Name | Map | Mandate |
|---|---|---|---|
| 1 | Orbit Breach | `orbit` | Arc Emitter, Rift Sapper, Split Shot unavailable |
| 2 | Crossroads Interdiction | `crossroads` | Rift Sapper unavailable - Momentum, Exploit Weakness, Split Shot banned |
| 3 | Pinch & Bleed | `pinch_bleed` | 5 tower slots only (1 locked) - Rift Sapper unavailable |
| 4 | Iron Mandate | `ridgeback` | Full tower + modifier access - Enemies +25% HP |

Mandate data source: `Data/campaign_stages.json`.

### Campaign UI

- Entry: Main Menu -> Play -> Mode Select -> Campaign
- `CampaignSelectPanel.cs` -- stage ladder with per-difficulty clear stamps (EOK NOK HOK)
- `CampaignManager.cs` -- active stage tracking; `CampaignProgress.cs` -- ConfigFile-backed save
- End screen shows campaign stamps + "Next Stage ->" on win; "Campaign Select" replaces "Main Menu"
- `CAMPAIGN_CLEAR` achievement: all 4 stages cleared (any difficulty)
- `CAMPAIGN_HARD_CLEAR` achievement: all 4 stages cleared on Hard

---

## 10. Unlockable Content

Unlock gates live in `Unlocks.cs`. All unlocks require winning a run on the specified map (any difficulty). Bots always have full access for deterministic balance testing.

| Content | Type | Unlock Map | Achievement | Order |
|---|---|---|---|---|
| Arc Emitter | Tower | orbit (Stage 1) | `ARC_UNSEALED` | 1st non-random map |
| Split Shot | Modifier | crossroads (Stage 2) | `SPLIT_UNSEALED` | 2nd non-random map |
| Rift Sapper | Tower | pinch_bleed (Stage 3) | `RIFT_UNSEALED` | 3rd non-random map |
| Blast Core | Modifier | ridgeback (Stage 4 / Iron Mandate) | `BLAST_UNSEALED` | 4th non-random map |
| Accordion Engine | Tower | double_back (skirmish only) | `ACCORDION_UNSEALED` | 5th non-random map |
| Phase Splitter | Tower | threshold (fallback) | `PHASE_UNSEALED` | 7th non-random map |

**Map order** is determined by `displayOrder` field in `maps.json`, filtered to non-random maps. Fallback IDs are hardcoded in `Unlocks.cs` for cases where DataLoader is unavailable.

**Demo gating:** Blast Core and Wildfire are always locked in demo builds regardless of achievement state. Accordion Engine and Phase Splitter are similarly locked in demo (they unlock on full-game map progression).

**Unlock reveal flow:** On winning a run that triggers an unlock, `GameController.EnqueueUnlockReveals()` queues `UnlockRevealScreen` panels that show sequentially. Each panel uses `ShowTowerUnlock` or `ShowModifierUnlock` based on content type.

**Slot Codex:** Locked towers and modifiers show a "UNREVEALED" card with the unlock map name as a hint. Full-game-only content (in demo) shows "FULL GAME -- Available in full release." instead.

---

## 11. Endless Mode

After clearing all 20 waves, the win screen offers **Continue - Endless**. Pressing it continues from wave 21 with no upper limit.

### Endless Scaling (per wave past wave 20)

| What scales | Rate |
|---|---|
| Enemy count | x1.05 per wave (compounding) |
| Enemy HP | x1.02 per wave (compounding) |
| Swift Walkers | +1 extra every 5 endless waves |
| Reverse Walkers (full game) | +1 extra every 6 endless waves |

- Spawn interval shrinks slowly, floored at 0.70 s
- HUD shows "Wave 21 inf", "Wave 22 inf", etc.
- `KEEP_GOING` achievement: press Continue - Endless
- `ENDLESS_25 / 30 / 40` achievements: clear those wave numbers
- If player continues to endless, the wave-20 global win leaderboard score is **not submitted** -- only the endless loss result is submitted

---

## 12. Surge & Spectacle System

### Per-Tower Surge Meter

Each tower has its own spectacle meter that fills from modifier procs.

| Constant | Value |
|---|---|
| Surge threshold | 145 |
| Post-surge reset | 10 |
| Surge cooldown | 6.0 s |

Meter gain comes from supported modifier procs, scaled by copy count, loadout diversity, and per-mod anti-spam token gates.

### Surge Effect Resolution

When a tower's meter hits threshold, it resolves a signature from equipped supported modifiers:

| Loadout type | Effect |
|---|---|
| Single (1 unique supported mod) | Single effect |
| Combo (2 unique supported mods) | Combo core effect |
| Triad (3 unique supported mods) | Combo core + augment effect |

The surge effect type directly controls gameplay payloads (status clears, damage bursts, cooldown effects) and VFX identity.

### Global Surge Meter

Every tower surge contributes to a shared global meter:

| Constant | Value |
|---|---|
| Per-surge gain | +10 |
| Global threshold | 100 |
| Post-global reset | 0 |

### Global Surge Effects

- Center-out cataclysm burst and ripple pass
- Global status/detonation propagation
- Broad tower payload application + cooldown reclaim
- Synchronized per-tower accent bursts
- Screen treatment, vignette, and impact audio

### Surge Differentiation System

`SurgeDifferentiation.cs` (pure-logic, no Godot deps) is the single source of truth for global surge archetypes. 12 named labels mapped from dominant contributing modifier:

| Label | Dominant Modifier | Feel |
|---|---|---|
| REDLINE WAVE | Momentum | Pressure |
| OVERKILL STORM | Overkill | Detonation |
| EXECUTION STORM | Exploit Weakness | Neutral |
| PRISM BARRAGE | Focus Lens | Detonation |
| CRYO STORM | Chill Shot | Pressure |
| HORIZON BREAK | Overreach | Pressure |
| HYPERBURST WAVE | Hair Trigger | Detonation |
| FRACTAL STORM | Split Shot | Neutral |
| REBOOT CASCADE | Feedback Loop | Detonation |
| CHAIN STORM | Chain Reaction | Neutral |
| BLAST WAVE | Blast Core | Detonation |
| INFERNO SURGE | Wildfire | Detonation |

Falls back to **GLOBAL SURGE** if no dominant modifier detected.

### Surge UX Details

- **Archetype preview:** global meter label transitions to predicted archetype name at >=70% fill
- **Feel types:** Detonation builds (Overkill, Focus Lens, Feedback Loop, Hair Trigger) = sharp flash + snap pulse; Pressure builds (Momentum, Chill Shot, Overreach) = softer sustained flash
- **Multi-color ripples:** up to 3 ripple colors reflecting top contributing modifiers
- **Per-tower archetype FX:** each tower type fires its own sequence in staggered order
- **Screen-edge vignette:** square-masked overlay ramps in during final 30% of global meter fill, tinted to dominant mod's color
- **Sustained archetype tint:** low-alpha full-screen color wash lingers ~2.4 s post-global-surge
- **SURGE xN chain counter:** gold callout accumulates at screen center when surges chain within the contribution window
- **Per-tower afterglow:** each contributing tower holds a 2.4 s accent-colored modulate fade

---

## 13. Achievement System

27 achievements tracked locally via `AchievementManager` (autoload, persistent to `user://achievements.cfg`). Steam forwarding via `SteamAchievements`. No Steam dependency in `AchievementManager` itself.

**Unlock toasts:** fade-in/out notification at bottom-right on new unlock; multiple queue and show sequentially.

**Achievements screen:** full-screen list from main menu and pause menu (inline overlay, no scene change). Locked entries show `???` name.

### Achievement Table

| ID | Name | Condition | Checked |
|---|---|---|---|
| `TUTORIAL_COMPLETE` | First Steps | Complete the tutorial run | Tutorial win |
| `FIRST_WIN` | First Victory | Complete all 20 waves | Run end (win) |
| `HARD_WIN` | Hard Carry | Complete all 20 waves on Hard | Run end (win) |
| `FLAWLESS` | Flawless | Win without losing a single life | Run end (win) |
| `LAST_STAND` | Last Stand | Win with exactly 1 life remaining | Run end (win) |
| `SPEED_RUN` | Speed Run | Win a run in under 15 minutes | Run end (win) |
| `CHAIN_MASTER` | Chain Master | Win with all 6 slots filled by Arc Emitters | Run end (win) |
| `ARC_UNSEALED` | Arc Unsealed | Beat orbit -- unlocks Arc Emitter | Run end (win) |
| `SPLIT_UNSEALED` | Split Unsealed | Beat crossroads -- unlocks Split Shot | Run end (win) |
| `RIFT_UNSEALED` | Rift Unsealed | Beat pinch_bleed -- unlocks Rift Sapper | Run end (win) |
| `BLAST_UNSEALED` | Blast Unsealed | Beat ridgeback (Iron Mandate) -- unlocks Blast Core | Run end (win) |
| `ACCORDION_UNSEALED` | Accordion Unsealed | Beat double_back -- unlocks Accordion Engine | Run end (win) |
| `HALFWAY_THERE` | Halfway There | Survive to wave 10 | Wave 10 start |
| `FULL_HOUSE` | Full House | Fill all 6 tower slots in one run | After draft pick |
| `STACKED` | Stacked | Give any tower 3 modifiers in one run | After draft pick |
| `FULL_ARSENAL` | Full Arsenal | Use 5 different tower types in one run | After draft pick |
| `OVER_EQUIPPED` | Over Equipped | Fill all 18 modifier slots in one run | After draft pick |
| `CHAIN_GANG` | Chain Gang | Place 3 or more Arc Emitters in one run | After draft pick |
| `GLASS_CANNON` | Glass Cannon | Equip Focus Lens and Hair Trigger on the same tower | After draft pick |
| `ANNIHILATOR` | Annihilator | Deal 100,000 total damage in one run | After wave clear |
| `DEVASTATOR` | Devastator | Deal 200,000 total damage in one run | After wave clear |
| `KEEP_GOING` | Keep Going | Start an endless run | On Continue pressed |
| `ENDLESS_25` | Into the Void | Clear wave 25 in endless mode | After endless wave clear |
| `ENDLESS_30` | No End in Sight | Clear wave 30 in endless mode | After endless wave clear |
| `ENDLESS_40` | The Abyss | Clear wave 40 in endless mode | After endless wave clear |
| `CAMPAIGN_CLEAR` | The Circuit | Clear all 4 campaign stages (any difficulty) | Campaign stage win |
| `CAMPAIGN_HARD_CLEAR` | Iron Mandate | Clear all 4 campaign stages on Hard | Campaign stage win |

### Check Methods (AchievementManager.cs)

| Method | When Called | Covers |
|---|---|---|
| `CheckRunEndAndCollectUnlocks(state, difficulty, won)` | Run end | All run-end achievements; returns newly unlocked IDs for reveal flow |
| `CheckTutorialComplete()` | Tutorial win | `TUTORIAL_COMPLETE` |
| `CheckHalfwayThere()` | Wave 10 start | `HALFWAY_THERE` |
| `CheckDraftMilestones(state)` | After each draft pick | FULL_HOUSE, STACKED, FULL_ARSENAL, OVER_EQUIPPED, CHAIN_GANG, GLASS_CANNON |
| `CheckAnnihilator(state)` | After each wave clear | ANNIHILATOR, DEVASTATOR |
| `CheckEndlessMilestones(state)` | After each endless wave clear | ENDLESS_25/30/40 |
| `CheckKeepGoing()` | On Continue pressed | KEEP_GOING |

Bot mode (`--bot`) skips all evaluation -- unlocks are never modified in automated runs.

---

## 14. Leaderboards & Scores

### Steam Leaderboard

- Global leaderboards per map+difficulty combination
- Every run stored as a separate row (not just personal best)
- Forwarded via `SteamLeaderboardService.cs` when Steamworks is available
- `NullLeaderboardService.cs` used when Steam is unavailable (standalone builds)

### Supabase Leaderboard

- `SupabaseLeaderboardService.cs` + `SupabaseConfig.cs` -- web-backend leaderboard path
- Used for standalone/itch builds without Steamworks

### Local High Score

- `HighScoreManager.cs` -- persists personal bests per map/difficulty
- `ScoreCalculator.cs` -- score formula based on waves, lives, damage dealt, time
- Steam Cloud sync via `SteamCloudSync.cs`

### Build Snapshot

- `BuildSnapshotCodec.cs` -- encodes the full tower/modifier loadout into a compact string appended to leaderboard entries for community replay reference

---

## 15. UI & Screens

### Screen Flow

```
MainMenu.tscn
  -> Play -> ModeSelectPanel -> Campaign or Skirmish
  -> Map Editor -> MapEditor.tscn
  -> Leaderboards -> LeaderboardsMenu.tscn
  -> Achievements -> AchievementsPanel (inline)
  -> How to Play -> HowToPlay
  -> Settings -> Settings

Main.tscn (active run)
  -> DraftPanel (full-screen overlay, 2-step pick flow)
  -> HudPanel (wave, lives, speed, global surge meter)
  -> PauseScreen (Esc overlay)
  -> UnlockRevealScreen (on achievement unlock)
  -> AchievementToast (bottom-right notification)
  -> EndScreen -> MainMenu / Campaign Select

MapEditor.tscn
  -> Canvas -> Main.tscn (playtest, Easy difficulty)
  -> Playtest EndScreen -> Back to Editor
```

### DraftPanel

- Full-screen overlay shown between waves
- 2-step: pick card -> placement mode (click slot or tower)
- Cancel placement: Esc or Cancel button
- Card keyboard shortcuts: `1-5`
- Modifier preview: hovering Overreach or Hair Trigger over a placed tower shows range ring at modified radius
- Tower preview: hovering a tower card over an empty slot shows pulsing range fill on the ghost

### HudPanel

- Wave number, lives counter, speed toggle (1x/2x/3x)
- Global surge meter bar with archetype label (transitions to predicted name at >=70% fill)
- Screen-edge vignette at high global meter fill

### Tower Tooltips

- Visible only during Wave phase
- Hover 50x50 hit area on tower -> `CanvasLayer(Layer=5)` panel + label
- Shows: tower name, targeting mode, modifier list

### Slot Codex (SlotCodexPanel)

In-game reference for all towers, modifiers, and enemies:
- Unlocked entries: full stats + mechanic description; in-game procedural art (TowerIconFull, EnemyIcon)
- Locked tower entries: "UNREVEALED TOWER" + map name hint
- Locked modifier entries: "UNREVEALED MOD" + map name hint, or "FULL GAME" for demo-excluded content
- Each card has a 2 px colored accent stripe via `UITheme.AddTopAccent()`

### Map Editor

- Entry from Main Menu
- Interactive waypoint + slot placement on an 8x5 grid (snap to 80 px)
- Custom maps saved to `user://custom_maps/{id}.json`
- Playtest always uses Easy difficulty; end screen shows [PLAYTEST] badge + Back to Editor
- Ctrl+Z/Y undo/redo; Ctrl+S save

### Settings

- Master / Music / FX volume sliders (saved to `user://settings.cfg`)
- Display: Windowed / Fullscreen toggle
- Colorblind mode: high-contrast modifier accent colors (no red/green reliance)
- Reduced motion: skips draft card flip animations -- cards appear face-up

---

## 16. Bot Mode & Tuning Pipeline

### Bot Mode

Runs N fully-automated games headless for balance data.

```bash
dotnet build SlotTheory.sln
"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
  --headless --path "E:/SlotTheory" \
  --scene "res://Scenes/Main.tscn" \
  -- --bot --runs 100
```

Output goes to stdout. Log also written to `C:/Users/kenny/AppData/Roaming/Godot/app_userdata/Slot Theory/logs/godot.log`.

**Key behaviors in bot mode:**
- `SoundManager` auto-disables (`DisplayServer.GetName() == "headless"`)
- `Unlocks.IsTowerUnlocked` / `IsModifierUnlocked` always returns `true`
- `AchievementManager.CheckRunEndAndCollectUnlocks` returns early (no saves)
- Spectacle gameplay payloads are applied (matching live logic for balance accuracy)
- Range checks fully enforced (`ignoreRange: false`)

### Bot Strategies (12)

Cycle round-robin across runs:

| Strategy | Focus |
|---|---|
| `Random` | Random draft picks |
| `TowerFirst` | Prioritize filling tower slots |
| `GreedyDps` | Highest damage-per-second tower/modifier |
| `MarkerSynergy` | Marker Tower + Exploit Weakness combos |
| `ChainFocus` | Arc Emitter stacking |
| `SplitFocus` | Split Shot stacking |
| `HeavyStack` | Heavy Cannon with Focus Lens |
| `RiftPrismFocus` | Rift Sapper mine coverage |
| `SpectacleSingleStack` | Single modifier stacked 3x for surge identity |
| `AccordionEngine` | Accordion Engine + compression synergies |
| `PlayerStyleKenny` | Mimics observed human play patterns |
| `HeavyOverkill` | Heavy Cannon priority + max 1 Overkill + 1 Feedback Loop per tower, then Hair Trigger/Focus Lens/Chain Reaction/Split Shot finisher |

**Strategy sets:** `--strategy_set all` (default, all 12), `optimization` (4 win-rate focused), `edge` (3 edge-case), `spectacle` (2 chain/overkill focused), `top3` (GreedyDps + PlayerStyleKenny + RiftPrismFocus -- ceiling testing).

**Demo simulation:** `--demo` flag zeroes Shield Drone and Reverse Walker counts via `Balance.SetDemoOverride(true)`.

### Tuning Pipeline

`run_tuning_pipeline.ps1` -- iterative difficulty optimization:

```powershell
# Full game:
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1
# Demo:
powershell -ExecutionPolicy Bypass -File .\run_tuning_pipeline.ps1 -Demo
```

- Generates a seed profile from current `SpectacleTuning`, runs bot eval + scenario suite, outputs best profile
- Full game output: `Data/best_tuning_full.json`; demo output: `Data/best_tuning_demo.json`
- Both files tracked in git and loaded at startup by `GameController._Ready()`

### Combat Lab

Scripted scenario validation and sweeps:

```bash
# Scenario validation
-- --lab_scenario Data/combat_lab/core_scenarios.json --lab_out release/report.json
# Tuning sweep
-- --lab_sweep Data/combat_lab/sample_sweep.json --lab_out release/sweep.json
# Tower benchmark
-- --lab_tower_benchmark Data/combat_lab/tower_benchmark_core.json --lab_out release/tower_bench.json
# Modifier benchmark
-- --lab_modifier_benchmark Data/combat_lab/modifier_benchmark_core.json --lab_out release/mod_bench.json
```

- `CombatLabCli.cs` -- CLI entry; `CombatLabScenarioRunner.cs` -- scenario runner
- `CombatResolution.cs` -- reusable chain-resolution helper (used by both benchmark runners)
- `BotMetricsDeltaReporter.cs` -- compares baseline vs tuned bot metrics

### Modifier Description Validation

`ModifierDataValidator.cs` runs on startup during `DataLoader.LoadAll()`. Checks that key stat tokens appear in `modifiers.json` descriptions (e.g., "42%", "x1.80"). Prints `[VALIDATOR] OK` or lists mismatches. Zero runtime overhead after initial load.

---

## 17. Procedural Music System

`MusicDirector` drives a fully adaptive score. No pre-authored tracks -- all generated from patterns and harmony tables at runtime.

| Phase | Component | Feature |
|---|---|---|
| 1 | `MusicClock` | Drift-free beat/bar/phrase events, BPM ramp; note pool (MIDI 28-81) |
| 2 | `MusicHarmony` | Scale/chord tables: Dorian, Mixolydian, Phrygian |
| 3 | `MusicBassLayer` | Root + fifth patterns; BPM ramp |
| 3 | `MusicDirector` hooks | OnWaveStart, OnWaveClear, OnLivesChanged, OnDraftPhaseStart, OnRunEnd |
| 4 | `MusicMelodyLayer` | Phrase-planned lead: contour-weighted (Ascending/Descending/Arch/Static), rest probability by tension, cross-phrase continuity |
| 5 | `MusicPercLayer` | Kick/snare/hat 8th-note grid, tension-driven density; kick sweep 100->65 Hz |
| 6 | Surge percussion | Fill triggered on global surge event |
| 7 | Chord-aware melody | Root pitch-class snapping; walking bass |
| 8 | BPM tiers | 112/128/140 BPM + per-map BPM spread (Gauntlet +24, Sprawl -24) |

`SoundManager` auto-disables all audio init in headless mode (`DisplayServer.GetName() == "headless"`).

---

## 18. Technical Architecture

### Simulation-Driven Design

All combat logic lives in C# systems, not scene node behaviors. The scene tree provides rendering and input only.

- `CombatSim.cs` -- step-by-step wave execution; no game logic in `_Process()` outside of `EnemyInstance`
- `DamageModel.cs` -- damage pipeline with modifier hooks
- `Targeting.cs` -- target selection
- `Statuses.cs` -- status effect tracking (Marked, Slow)
- `AccordionFormation.cs` -- pure-logic formation compression helper (no Godot deps; unit-testable)
- `SurgeDifferentiation.cs` -- pure-logic global surge archetype table (no Godot deps; 35 unit tests)

### Enemy Movement

`EnemyInstance` extends `PathFollow2D`. Enemies self-move via `_Process()`: `Progress += Speed * delta`. Never move enemies manually in `CombatSim`. Assigning `Progress` automatically updates `GlobalPosition`.

### Projectile & Resolution Model

- Most standard tower attacks spawn `ProjectileVisual` nodes and apply damage on impact.
- Rocket Launcher uses this path explicitly with Rocket-specific projectile speed and impact/splash readability cues.
- Sim-native mechanics (Rift Sapper mines, Accordion Engine pulses, Phase Splitter endpoint hits, Undertow pull effects) resolve directly in combat systems without projectile travel.

### Data-Driven Modifiers

- Modifier definitions in `Data/modifiers.json` (id, name, description, params)
- Behavior is code-driven via `ModifierRegistry` -- no `if (modifierId == ...)` in tower code
- Each modifier implements the `Modifier` base class: `OnEquip`, `ModifyAttackInterval`, `ModifyDamage`, `OnHit`, `OnKill`

### Draft Anti-Brick

`DraftSystem` enforces that the draft never enters a state where a chosen modifier cannot be applied to any tower -- swaps are deterministic and covered by `DraftAntiBrickTests.cs`.

### Map System

- `MapGenerator.cs` -- procedural snake-path 8x5 grid map (for skirmish/random)
- Custom maps: `user://custom_maps/{id}.json` via `CustomMapManager.cs`
- Campaign maps: fixed paths defined in `Data/maps.json` with `displayOrder` field

### Unit Tests

`SlotTheory.Tests` (xUnit, 523 tests as of current build). All pure-logic tests; no Godot engine initialization required.

Key test suites:
- `ModifierTests.cs` -- all 11 modifiers
- `AccordionEngineTests.cs` -- formation compression math + isChain differentiation
- `ShieldDroneTests.cs` -- damage reduction and HP scaling
- `DamageModelStatusTests.cs` -- Marked and DamageAmp interactions
- `RocketLauncherTests.cs` -- built-in Rocket splash behavior and Blast Core radius synergy
- `SurgeDifferentiationTests.cs` -- 35 archetype label tests
- `DraftAntiBrickTests.cs` -- anti-brick draft coverage
- `WaveSystemTests.cs` -- wave configuration and enemy scaling

---

## 19. Platform & Build

**Engine:** Godot 4.6.1 .NET (mono build required for C# support)
**Runtime:** .NET 8 (target `net8.0`; .NET 10 installed and compatible)
**Platform target:** Windows (Steam), Android (phone and tablet)

### Build

```bash
dotnet build SlotTheory.sln   # always build before any Godot CLI run
```

Stale DLL warning: Godot CLI uses the DLL at `.godot/mono/temp/bin/Debug/`. If C# source changed but `dotnet build` was not run, Godot silently runs old code.

### Export (Windows)

```bash
dotnet build SlotTheory.sln
"E:/Godot/Godot_v4.6.1-stable_mono_win64_console.exe" \
  --path "E:/SlotTheory" \
  --export-release "Windows Desktop" \
  "E:/SlotTheory/export/SlotTheory.exe" --headless
```

### Export (Android APK -- physical device)

```bash
"E:/Godot/..." --path "E:/SlotTheory" --export-debug "Android" "E:/SlotTheory/export/SlotTheory.apk" --headless
adb install -r "E:/SlotTheory/export/SlotTheory.apk"
```

### Godot .tscn Rules

- `[Export] NodePath` requires `node_paths=PackedStringArray("PropName")` on the `[node ...]` line
- `[Export] PackedScene?` uses `PropName = ExtResource("id")` under the node, no `node_paths`
- Property names in `.tscn` use PascalCase (matching C# property name exactly)
- Sibling `_Ready()` order = scene order (top to bottom); children ready before parents

### Steam Integration

- Steamworks.NET integrated; Steam App ID active
- Global leaderboards operational
- Steam Cloud: settings and high scores sync across devices
- Achievements forwarded to Steam on local unlock

### `--quit-after N` Warning

`--quit-after N` counts **frames, not seconds**. At 60 fps, `--quit-after 60` = 1 second. Use background process + `kill` for timed runs instead.
