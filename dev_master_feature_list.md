# Slot Theory - Dev-Facing Master Feature List

This document is the current internal source-of-truth feature overview for the shipped game state. It is written for development, planning, QA, store-page prep, and patch-note extraction.

---

## Current Product Shape

Slot Theory is an auto-battler / tower-defense hybrid built around a pick-and-place draft loop. The player chooses one card at a time, places towers or modifiers into fixed slots, then watches the wave resolve automatically. The game’s identity comes from build composition, modifier synergies, procedural spectacle, adaptive audio, and strong presentation feedback.

Main run structure:
1. Main menu → Tutorial or Play
2. Mode Select → Campaign or Skirmish
3. Draft one card
4. Place tower or modifier
5. Wave auto-runs
6. Fill and manually activate Global Surge
7. Continue until wave 20 clear or lives reach 0
8. On skirmish win: optionally continue to Endless
9. On campaign win: continue to next stage

Platforms currently supported:
- Windows desktop
- Android phone and tablet

---

## Top-Level Feature Pillars

### 1. Core Combat Loop
- Auto-resolving wave combat with no direct combat micro during waves
- Fixed-slot board building
- Towers can be enhanced with stackable modifiers
- Runs culminate in a 20-wave win state or fail on life loss
- Endless continuation available after a standard win

### 2. Buildcraft / Drafting
- Draft-based progression: choose one option at a time
- Mix of tower and modifier options based on board state
- Anti-brick drafting rules prevent dead modifier offers
- Build identity is a central design pillar rather than a side effect

### 3. Spectacle / Surge Identity
- Per-tower spectacle interactions
- Build-driven global surge system with named archetypes
- Deliberately flashy presentation pass for anticipation, activation, and aftermath
- Sim and live gameplay both use the same spectacle payload logic

### 4. Strong System Feedback
- Combat callouts for meaningful proc moments
- Kill hitstop, damage numbers, chain FX, leak feedback, wave pacing banners
- Low-life tension presentation and clutch feedback
- Distinct build naming and leaderboard identity presentation

### 5. Replay / Meta Layer
- Campaign mode with stage progression and mandates
- Local highscores + global leaderboards
- Achievements
- Unlockable towers/modifiers/content
- Tutorialized onboarding with a dedicated scripted map

### 6. Platform Completeness
- Full desktop flow
- Strong mobile-specific UX pass: zoom, pan, touch scrolling, persistence, haptics, back-button handling, viewport adaptation

---

## Game Modes

### Tutorial
- Dedicated scripted tutorial run
- Accessible from a separate main-menu button
- 8 waves on fixed Easy difficulty
- Curated early draft sequence with targeted teaching moments
- Contextual blocking panels teach drafting, build naming, targeting modes, per-tower surge, and global surge activation
- Global surge meter is prefilled late in the tutorial so the player experiences the mechanic naturally
- Tutorial completion persists and mutes the tutorial button afterward
- Tutorial map is excluded from normal map select and leaderboards
- Completion grants the tutorial achievement

### Skirmish
- Standard free-play run structure
- Player selects map and difficulty
- No campaign mandate restrictions
- Clears at wave 20
- Allows Endless continuation after victory

### Campaign - The Fracture Circuit
- 4-stage linear campaign structure
- Separate Campaign Select flow from Mode Select
- Stage ladder with locked / available / cleared state
- Difficulty can be selected per stage
- Clear markers are tracked by difficulty
- Each stage applies a battlefield mandate
- Campaign win flow replaces the normal skirmish end options with next-stage progression
- Mandate is displayed prominently during gameplay

Campaign mandate escalation currently includes:
- Tower restrictions
- Modifier bans
- Slot lockouts
- Enemy HP bonuses

---

## Maps / Progression Structure

### Standard Maps
- Skirmish map selection flow
- Map-specific identity is also reflected in music BPM spread and run naming

### Campaign Stage Ladder
- Stage availability is persisted
- Stage N unlocks when stage N-1 has been cleared on any difficulty
- Stage cards show lock state, mandate, and difficulty clear checkmarks
- Intro overlays provide campaign framing before runs begin

---

## Towers

Current tower roster: 5 towers.

### Rapid Shooter
- Fast single-target tower
- High hit frequency, strong synergy with proc-based modifier interactions

### Heavy Cannon
- Slow heavy-hitting tower
- Strong kill impact and distinct hitstop feel

### Marker Tower
- Applies Marked on hit
- Enables status-synergy builds

### Arc Emitter
- Built-in chaining identity
- Chain-oriented offensive archetype
- Campaign unlock content

### Rift Sapper
- Mine / trap-style tower
- Seeds mines on lane anchors within range
- Has wave-start burst planting behavior
- Supports mine charge logic and cascade interactions
- Campaign unlock content

Shared tower rules:
- Towers are placed into fixed world slots
- Each tower can hold up to 3 modifiers

---

## Modifiers

Current modifier roster: 10 modifiers.

### Damage / Scaling
- Momentum
- Overkill
- Focus Lens
- Hair Trigger
- Feedback Loop

### Utility
- Chill Shot

### Range
- Overreach

### Status Synergy
- Exploit Weakness

### Multi-Target
- Split Shot
- Chain Reaction

Modifier system traits:
- Draft-offered based on current board state
- Modifiers are only offered when at least one valid target tower exists
- Color language is consistent across cards, icons, halos, and feedback systems
- Includes alternate high-contrast palette for colorblind mode

---

## Enemy Roster

Current enemy roster: 6 active enemy types.

### Basic Walker
- Baseline unit

### Armored Walker
- High-HP slow unit
- Used for tension spikes and armored warning moments

### Swift Walker
- Fast pressure unit

### Reverse Walker
- Trickster pressure unit (full game) that can rewind along the path after heavy non-lethal hits (single-hit threshold: >=10% max HP)
- Rewind trigger is cooldown-gated and per-life capped for fairness/readability

### Splitter Walker
- Mid-tier enemy that breaks into shards on death
- Adds multi-phase cleanup pressure and spectacle value

### Splitter Shard
- Spawned by Splitter Walker death
- Faster cleanup fragment unit

Enemy visual support includes:
- Procedural body shapes
- Health-bar state shifts
- Status overlays for Marked and Slow
- Spawn animation
- Codex card support

---

## Status Effects / Combat States

### Marked
- Applied via Marker Tower
- Enables stronger damage synergy interactions

### Slow
- Applied via Chill Shot
- Affects enemy movement speed

---

## Difficulty Modes

Current difficulty modes:
- Easy
- Normal
- Hard

Difficulty support includes:
- HUD difficulty label during active gameplay
- End-screen difficulty label
- Campaign difficulty clear tracking
- Difficulty-scaled enemy HP / count / spawn pressure

---

## Draft System

Core draft behavior:
- When free slots exist, the draft targets a mixed tower/modifier offering
- When all slots are occupied, the draft becomes modifier-only
- Invalid modifier offers are backfilled appropriately
- Bonus pick support exists in the ruleset, though currently disabled

Draft polish / UX support:
- Reduced-motion option to skip card flip animations
- Hover previews for tower range
- Hover previews for range-changing modifiers
- Mobile-friendly card sizing behavior

---

## Targeting / Tower Control

- Towers support targeting modes
- Tutorial explicitly teaches First / Strongest / Lowest HP behavior
- Tooltip surfaces tower state and current targeting mode

---

## Tooltip / Info Surfaces

Tooltip currently exposes:
- Slot and tower name
- Targeting mode
- Effective stats
- Modifier list with descriptions
- Spectacle trigger preview information
- Possible spectacle outcomes based on current modifier support
- Single / Combo / Triad labeling

Mobile behavior:
- Tooltip anchors to the selected tower rather than cursor position

---

## Spectacle and Surge Systems

### Per-Tower Spectacle Support
- Tower spectacle payloads are active in live gameplay and bot simulation
- Tooltip and analytics reflect spectacle behavior

### Global Surge
- Player-activated once the meter is full
- Manual activation replaced the old auto-fire behavior
- Bot mode still auto-activates for sim integrity
- Tutorial includes a dedicated first-time explanation and forced interaction

### Global Surge Meter / Readability Pass
- 20-pip meter replaces the old continuous fill bar
- Archetype prediction appears before full charge
- Ready state locks to archetype identity
- Clickable gold “activate” state at readiness
- Mechanical payload subtitle appears after activation
- Screen-edge vignette ramps in near readiness
- Post-activation tint lingers by archetype feel
- Chain counter supports surge chain readability
- Per-tower afterglow reinforces contributor identity
- Combo and augment callouts are split into sequential lines

### Global Surge Identity System
- 10 named build archetypes
- Archetype determined by dominant modifier contribution
- Feel buckets drive visual behavior (e.g. detonation / pressure / neutral)
- Multi-color ripples reflect top contributing modifiers
- Each tower fires its own sequence during global activation

### Surge Gain Balance
- Meter gain is scaled by attack interval so fast towers do not overfill too easily and slow towers remain relevant

---

## Combat Feedback and Juice

### Hit / Kill / Proc Feedback
- Damage numbers with kill-shot differentiation
- Source hint notch by tower family color
- Enemy hit flash
- Kill hitstop with heavier cannon variant
- Projectile dissolve when target dies mid-flight
- Retarget ping
- Death burst FX
- Chain arc FX

### High-Level Combat Messaging
- World-space combat callouts for major proc moments
- Cooldown-gated proc callouts to avoid spam
- Wave-clear flash and pacing hold
- Mid-run HALF-WAY pulse moment
- Leak feedback via shake and HUD flash
- Low-life heartbeat and CLUTCH / TOO CLOSE messaging

### Speed Feel
- Speed toggle gets a dedicated center toast, streak effect, and subtle audio lift

### Signature Flourish
- Scanline-style streak used on key moment callouts

---

## Audio / Music

### Procedural Music System
- Fully procedural adaptive score driven by `MusicDirector`
- Eight development phases currently integrated
- Includes harmony, bass, melody, percussion, tension response, surge percussion fill, chord-aware melody behavior, walking bass, and per-map BPM spread

### Music Behavior
- Music continues uninterrupted through draft phase
- Endless continuation properly restores music after end-of-run shutdown
- Tension ramp increases volume/pitch late in runs and resets each run

### Combat Audio Polish
- Combo pitch ramping
- Heavy Cannon music ducking for clarity
- Rapid Shooter projectile loudness adjustment
- Modifier-based shoot pitch scaling
- Chill Shot variant firing sound on supported towers

---

## Visual Identity / Presentation

- Neon synthwave UI theme built in code
- Rajdhani font family used across runtime UI
- Transition autoload handles scene fades
- HUD wave label uses bracketed presentation and accent stripe polish
- Map intro animations reveal path before slots drop into place
- Slot Codex cards render live-style tower and enemy body shapes rather than abstract icons

---

## UI / UX Surface Areas

### Main Menu / Navigation
- Tutorial button
- Play flow into Mode Select
- Campaign and Skirmish branching

### HUD
- Wave info
- Difficulty label
- Enemy counter
- Global surge meter with readiness state
- Campaign mandate banner when applicable

### End Screen
- Standard win/loss support
- Skirmish win allows Endless continuation
- Campaign win offers next-stage flow
- Difficulty label included
- Mobile-specific layout support

### Achievements Screen
- Dedicated full-screen achievements view
- Inline pause-menu overlay behavior during runs
- Hidden-name handling for locked achievements
- Unlock queue for sequential toast display

### How-To-Play / Codex Support
- Surges tab lists all 10 archetypes with feel indicators and modifier icons
- Slot Codex contains actual in-game geometry representations for towers and enemies

---

## Achievements

- 16 achievements tracked locally
- Persistent progression state
- Unlock toast queue
- Dedicated achievements screen
- Steam forwarding through Steamworks.NET

---

## Leaderboards / High Scores

### Local High Scores
- Persistent local highscores
- Personal best shown in map select
- Tracks map/difficulty combinations

### Global Leaderboards
- Steam leaderboard integration is live
- Every run is stored as a distinct row, including losses
- Async submission with retry queue and background sync
- Dedicated leaderboard screen
- Current build-name label appears on each row
- Build names are generated from the run snapshot and profile analysis

### Score Model
- Wins always rank above losses
- Wave count is the primary tiebreaker inside win/loss classes
- Additional tie logic includes lives/time

---

## Build Name / Profile Identity System

- Dedicated deterministic naming engine
- Uses primary/secondary modifier family, MVP/support tower, map, difficulty, pace, result state, clutch state, and tower diversity
- Multiple templates and richer vocabulary
- Anti-repeat history is persisted
- Build names are rendered prominently in leaderboards with gradient styling and truncation support

---

## Unlocks / Progression

Unlocks are currently tied into campaign/map progression and include content such as:
- Arc Emitter
- Split Shot
- Rift Sapper

Persistent systems include:
- Campaign clear state by difficulty
- Tutorial completion state
- Achievements
- Local highscores
- Run-name anti-repeat history
- Retry queues and mobile session snapshots where relevant

---

## Mobile-Specific Feature Set

### Camera / View Controls
- Pinch-to-zoom
- Single-finger panning when zoomed
- Automatic camera bounds
- Readability-aware scaling
- Device-calibrated zoom mapping

### Session Persistence
- Auto-save on backgrounding
- Auto-restore on relaunch
- Snapshot expiry window
- Full run state capture
- Anti-reroll preservation for mid-draft restores

### Touch UX
- Drag scrolling in scrollable surfaces
- Better gesture suppression for multi-touch
- Larger touch targets
- Mobile-specific buttons where needed
- Contextual back-button behavior
- Android back press now also gets SFX feedback

### Feel / Haptics
- Light / medium / strong haptics for key actions and outcomes

---

## Accessibility / Comfort Options

- Colorblind mode with non-red/green-reliant palette
- Reduced motion toggle for draft card presentation

---

## Simulation / Technical Notes Worth Preserving

- Spectacle / global surge payload logic is shared across live and bot simulation
- Surge differentiation logic is isolated into a pure, unit-tested source-of-truth class
- Preview / feature behavior around surge state is covered by unit tests
- Retry queues are used for resilient leaderboard submission
- Mobile snapshot system serializes full draft state to prevent reload rerolls

---

## Recent High-Impact Additions

Most notable recent implementation additions include:
- Campaign mode
- Player-activated global surge
- Scripted tutorial map and onboarding flow
- Surge readability / wow-factor pass
- Splitter Walker enemy family
- Difficulty labels in HUD and end screen
- Music continuity through draft and Endless fixes
- Mobile camera / persistence / touch / haptics pass
- Expanded achievements
- Build-name label and richer naming engine
- Codex in-game geometry rendering
- All-runs global leaderboard storage

---

## Short Internal Summary

Slot Theory is no longer just “functional.” It now has:
- a strong main loop,
- a real onboarding path,
- a proper campaign wrapper,
- high-identity build feedback,
- mature presentation systems,
- meaningful replay scaffolding,
- and a mobile pass that moves it out of prototype territory.

This file should remain the broad internal reference. Patch notes, public store copy, and external pitch material should be derived from this rather than replacing it.
