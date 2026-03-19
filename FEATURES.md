# Slot Theory - Feature and Polish Reference

This document reflects the current implementation in code/data.


**Current Status:** All documented features are fully implemented and production-ready. Recent implementation includes:
- Enhanced undo system
- Combat callout notifications
- Low-life tension feedback
- Comprehensive audio polish
- **Campaign mode:** 4-stage linear campaign "The Fracture Circuit" with escalating per-stage mandates (tower restrictions, modifier bans, slot lockouts, HP bonuses). Play → Mode Select → Campaign or Skirmish. CampaignSelect stage ladder with lock/clear state, difficulty checkmarks, and stage intro overlays. Campaign end screen replaces leaderboard/play-again/endless with "Next Stage →" and "Campaign Select" buttons. Mandate displayed as a full-width banner below the HUD bar.
- **Leaderboard build-name label:** Each leaderboard row now displays a large build-name label on the right, generated per entry from map/difficulty/stats/build snapshot (see below for details).
- **Run naming engine:** Dedicated naming engine with richer profile analysis and anti-repeat logic (see below).
- **Mobile camera system:** Pinch-to-zoom, pan controls, camera bounds, and readability scaling for mobile gameplay.
- **Mobile session persistence:** Automatic save/restore of game state when app is paused/resumed on mobile.
- **Touch scroll support:** Drag scrolling for all scrollable UI areas on mobile devices.
- **Haptic feedback:** Light/medium/strong haptics on card pick, tower placement, wave clear, life lost, win, and game over.
- **Back button SFX:** `ui_select` sound plays on Android back press across all screens.
- **Tension ramp:** Music volume and pitch gradually increase across waves 15–20 (up to +3.5 dB / +2.5% pitch at wave 20); resets each run.
- **Colorblind mode:** Settings toggle that switches modifier accent colors to a high-contrast palette with no red/green reliance.
- **Reduced motion toggle:** Settings toggle that skips card flip animations in draft - cards appear face-up instantly.
- **In-game achievements:** 16 achievements tracked locally (persistent across sessions) with unlock toast notifications and a dedicated achievements screen. Steam forwarding active via Steamworks.NET.
- **Procedural music system (Phases 1–8):** `MusicDirector` drives a fully procedural adaptive score. Phase 1: drift-free `MusicClock` + MIDI note pool. Phase 2: `MusicHarmony` scale/chord tables + `MusicBassLayer`. Phase 3: game-state hooks (tension tier, BPM, mode, layer density). Phase 4: `MusicMelodyLayer` - phrase-planned lead with contour weighting and cross-phrase continuity. Phase 5: `MusicPercLayer` - tension-driven kick/snare/hat grid, density arc, pad fade-out. Phase 6: surge percussion fill on global surge trigger. Phase 7: chord-aware melody (root pitch-class snapping) + walking bass. Phase 8: BPM tiers raised (112/128/140) + per-map BPM spread (Gauntlet +24, Sprawl −24).
- **Slot Codex in-game graphics:** Tower and enemy cards in the Slot Codex now render actual in-game body shapes using `TowerIconFull` and `EnemyIcon` - the same procedural draw geometry used in live gameplay, scaled to icon size. Codex header and each card have a 2 px colored top accent stripe via `UITheme.AddTopAccent()`.
- **All-runs leaderboard:** Global leaderboard now stores every run as a separate row (wins and losses). Previously only kept the personal best per player.
- **Spectacle system integration:** Surge/global surge spectacle gameplay payloads are active in both live and bot simulations; tooltip and bot analytics now expose spectacle behavior.
- **Surge differentiation:** Global surge banner shows a dynamic build archetype label (10 named archetypes driven by dominant contributing mod - REDLINE WAVE, OVERKILL STORM, CHAIN STORM, etc.). Visual feel (Detonation/Pressure/Neutral) controls flash alpha, second snap pulse, and ripple intensity. Multi-color ripples (up to 3 colors) reflect top contributing mods. Each tower fires its own identity FX in staggered sequence on global surge. `SurgeDifferentiation.cs` is the single source of truth (no Godot deps, fully unit-tested with 35 xUnit tests). HowToPlay Surges tab lists all 10 archetypes with feel indicators and modifier icons throughout.
- **Surge readability and wow-factor pass:**
  - Global meter HUD redesigned: 20 discrete pips (1 per surge needed) replace the continuous progress bar; label transitions from "GLOBAL SURGE" to the predicted archetype name at ≥70% fill, then locks to the archetype name once surge is ready; resets to "GLOBAL SURGE" on activation.
  - Banner subtitle: after global surge, a second line below the archetype label shows the mechanical payload summary (e.g. "TOWERS −36% RELOAD · ENEMIES MARKED & SLOWED"), scaled by unique contributor count (32%–46% refund range).
  - Screen-edge vignette: square-masked shader overlay ramps in during the final 30% of global meter fill, tinted to the dominant mod's color; clears instantly on trigger.
  - Sustained archetype tint: after global surge fires, a low-alpha full-screen color wash lingers for ~2.4 s keyed to feel - deep red (Pressure), orange (Detonation), purple (Neutral).
  - SURGE ×N chain counter: a gold callout at screen center accumulates when surges chain within the contribution window (≥2 surges); resets on global surge or window expiry.
  - Per-tower afterglow: each tower involved in the global surge sequence holds a 2.4 s accent-colored modulate fade after its FX burst.
  - Triad callout polish: combo name and augment name now spawn as separate sequential callouts; augment appears below the combo name in the augment modifier's own color.
  - Unit-tested: `SurgePreviewFeatureTests` (26 tests) covers `PeekDominantMods` state-safety, preview alpha ramp formula, and banner subtitle refund-percent formula.
- **Surge meter gain - attack-interval scaling:** per-proc meter gain is multiplied by `clamp(attackInterval / 1.0s, 0.65, 1.5)`. Fast towers (< 1 s interval) earn proportionally less per proc (floored at 0.65× so Rapid Shooter + Hair Trigger isn't double-penalized into near-impossible surge fill); slow towers (> 1 s) earn up to a 1.5× bonus so heavy hitters remain surge-relevant.
- **Tutorial surge page - live meter visible:** on the surge tutorial page the actual global surge meter (HudPanel) is forced visible and rendered above the draft overlay (HudPanel temporarily raised to Layer 7) so players see the real UI element being explained.
- **Tutorial map:** Dedicated scripted tutorial run accessible from a "▶  Tutorial" button in the main menu (above PLAY). 8 waves on fixed Easy difficulty. Curated draft options per wave (waves 0–5 scripted, 6–7 random). Contextual blocking panels at key moments: draft explanation each pick, build name highlight after first modifier placed, targeting mode panel at wave 3 (pauses game, explains First/Strongest/Lowest HP, unpauses when player clicks a tower to cycle mode), per-tower surge explanation (pauses game, highlights surge bar), global surge activation explanation (pauses game, gold highlight, "Activate →" button). Global surge meter pre-filled to 95% at wave 6 start so it fires naturally during combat. All wave callouts are click-to-dismiss; cleared on draft start. Completion flagged to `SettingsManager.TutorialCompleted` (persisted). Tutorial button becomes muted after completion. Tutorial map excluded from map select and leaderboards. End screen shows "How to Play →" button and re-labels play-again as "Play Again · Real Run →". Earns TUTORIAL_COMPLETE achievement.
- **Global surge - player-activated:** The global surge meter no longer auto-fires when full. When filled, `SpectacleSystem.IsGlobalSurgeReady` becomes true and `OnGlobalSurgeReady` fires (passing the resolved archetype label). The HUD surge bar switches to a gold pulsing clickable state with label "▶  ACTIVATE"; the label is locked to the archetype name until activation. The player clicks it to fire. `SpectacleSystem.ActivateGlobalSurge()` then fires `OnGlobalTriggered` and plays all existing effects. Bot mode auto-activates immediately to preserve simulation behavior. First-time in tutorial: game pauses, gold-bordered panel appears explaining the mechanic with an "Activate →" button that fires the surge and unpauses.
- **Difficulty label in HUD and end screen:** HudPanel shows the current difficulty mode (EASY / NORMAL / HARD) color-coded in the top-right bar during gameplay. EndScreen shows the difficulty label between the title and subtitle on both win and loss screens.
- **Music plays uninterrupted through draft:** Music no longer restarts at draft phase start — it flows continuously from wave to draft to wave for a seamless adaptive score.
- **Map intro animations:** On run start, the neon path draws from start to finish before the draft panel opens (grid background hidden during reveal). Tower slots then drop in from off-screen with a bounce and landing sound; slots start 900 px above their final position so bottom-row slots are fully hidden before the drop.
- **Endless mode music fix:** `MusicDirector.OnEndlessContinue()` restarts the clock and reactivates all layers after `OnRunEnd` stopped them, fixing music going silent when continuing to Endless.
- **Splitter Walker enemy:** New enemy type appearing in waves 9–15. HP is 1.8× basic, speed 90 px/s, leak cost 3 lives. On death, spawns 2 Splitter Shards (0.55× basic HP, 165 px/s, 1 life each) offset behind the death position. Both types have unique layered procedural visuals (amber hex + bisecting crack for Splitter; irregular crystal fragment polygon for Shard) and full Slot Codex cards with icon-per-stat rows.
- **Reverse Walker enemy (full game):** Trickster unit introduced in late-mid waves. Heavy non-lethal hits (>=10% max HP from a single hit) can trigger a short path-safe rewind with strong VFX/SFX feedback. Triggered rewinds are cooldown-gated and capped per enemy to prevent spam.
- **HUD wave label polish:** Wave label now displays as `[ Wave N / 20 ]` with cyan bracket tinting. A 2 px cyan accent stripe runs along the bottom edge of the HUD bar. Difficulty label and enemy counter offsets tightened to prevent overlap.

Platforms: Windows Desktop, Android (phone and tablet)

---

## Since v1.0.8

- Added spectacle combat system with per-tower meter signatures and global surge triggers.
- Minor spectacle triggers were removed; only surge/global surge tiers are active for balance and gameplay.
- Bot simulation now includes spectacle gameplay payload resolution (surge + global surge), not only non-gameplay spectacle hooks.
- Bot report now includes spectacle trigger totals and top effect breakdowns.
- Tooltip now displays all possible spectacle trigger outcomes based on current supported modifiers.
- Added spectacle-specific bot strategies:
  - `SpectacleSingleStack`
  - `SpectacleComboPairing`
  - `SpectacleTriadDiversity`
- Added surge differentiation: 10 named global surge archetypes, feel-keyed visual treatment (Detonation/Pressure/Neutral), multi-color ripples, and per-tower identity FX.
- HowToPlay screens polished with procedural icons throughout (TowerIcon, ModifierIcon with accent tinting, dual icons for combo surges, feel-bar + icon for global surge archetypes).
- **Automated tuning pipeline:** `run_tuning_pipeline.ps1` + `Scripts/Tools/CombatLab/` drive iterative difficulty optimization against bot win-rate targets. `SpectacleTuning.Current` overrides difficulty multipliers at runtime without recompiling.
- **Achievement system:** 13 achievements tracked via `AchievementManager` (autoload, persistent to `user://achievements.cfg`). Unlock toasts, dedicated achievements screen, and Steam forwarding via `SteamAchievements`. Gates content unlocks.
- **Unlockable content:** Arc Emitter, Split Shot, and Rift Prism are gated behind campaign map clears (`Unlocks.cs`). Bots always have full unlock access for deterministic balance testing.
- **Three difficulty modes:** Easy (no scaling), Normal (~75% bot win target), Hard (~50% bot win target). Multipliers live in `Balance.DifficultyMultipliers` and are overridable at runtime via `SpectacleTuning.Current`.
- **Enemy render pipeline overhaul:** Layered render pipeline with per-class death FX, perf controls, and mobile-adaptive quality settings (`EnemyRenderPerfProfiler`, `EnemyRenderSettingsSnapshot`, `EnemyRenderLayerSettings`).
- **New UI screens:** `UnlockRevealScreen` (shown on first map-clear unlock), `AchievementsPanel`, `AchievementToast`, `SlotCodexPanel`.
- **Supabase leaderboard service:** `SupabaseLeaderboardService` + `SupabaseConfig` provide a web-backend leaderboard path alongside Steam, used for standalone/itch builds.
- **Procedural music system:** `MusicDirector` + `MusicClock` + `MusicBassLayer` + `MusicMelodyLayer` deliver an adaptive procedural score. Tension tier, BPM, mode, and layer density all respond to live game events. See Procedural Music System section below.
- **Slot Codex in-game graphics:** `TowerIconFull` and `EnemyIcon` render actual in-game tower/enemy body geometry in codex cards.
- **Bug fixes:** DraftPanel cards reflow correctly on window resize (FullRect anchors); tooltip no longer renders above DraftPanel or PauseScreen (layer ordering fix).

---

## Recent Implementation

**Undo Safety Net:** Single-step undo for tower placement with 2-second window. UNDO toast button appears after placement, allowing reversion to draft choice state.

**Combat Callouts:** World-space notifications for major modifier procs (OVERKILL SPILL, FEEDBACK LOOP, CHAIN REACTION) with cooldown-gating.

**Low-Life Tension:** Heartbeat audio effect during waves when lives ≤ 2. "CLUTCH" / "TOO CLOSE" toast on enemy leaks at low lives.

**Run Arc Pacing:** Wave 10 HALFWAY banner pulse with short music lift cue for mid-run momentum.


**Enhanced Build Identity:**
- Deterministic build naming with a dedicated engine (see RunNameGenerator section).
- Build names now consider: primary/secondary modifier family, MVP/support tower, map, difficulty, pace, win/loss, clutch state, tower diversity.
- Multiple templates and richer vocabulary (not just adjective+noun).
- Anti-repeat history is persisted to `user://run_name_history.cfg` and applied to final run names (win/loss) to avoid recent duplicates.
- MVP tower tracking (% of total damage) and Most Valuable Modifier (proc count).

**Speed Enhancement:** "Speed feels like power" - center SPEED X× toast with neon streak on toggle, subtle SFX/music pitch lift at 2×/3×.

**Signature Flourish:** Scanline-style signature streak on key moments (Bonus Pick waves, wave beat labels).

**In-Game Achievements:** 13 achievements with persistent local state, unlock toast notifications, and a dedicated screen accessible from main menu and pause menu. Steam forwarding is wired - each local unlock is forwarded to Steamworks when available.

---

## Core Loop

1. Main menu → Tutorial (optional) or Play → **Mode Select** → Campaign or Skirmish.
   - **Campaign**: CampaignSelect stage ladder → pick stage + difficulty → Main scene loads with active mandate.
   - **Skirmish**: Map select → Main scene loads (free play, no mandate).
2. Draft: pick 1 card.
3. Place picked tower/modifier in the world.
4. Wave runs automatically (no direct combat input).
5. When global surge meter fills: click the glowing bar to activate.
6. Repeat until wave 20 clear (win) or lives reach 0 (loss).
7. On win (skirmish): optionally continue in **Endless Mode** (wave 21+, no victory condition).
8. On win (campaign): proceed to the next stage with the "Next Stage →" button.

---

## Platform Support

- Windows desktop:
  - Mouse + keyboard controls.
  - ESC pause support.
- Android:
  - Touch controls.
  - Hamburger pause button in HUD.
  - Auto-pause when app is minimized.
  - Responsive UI scaling and touch sizing.
  - Back button navigation: returns to previous screen from all menu screens; shows quit confirmation during active waves; opens pause menu during draft.

---

## Mobile Platform Features

**Camera System:**
- **Pinch-to-zoom:** Two-finger pinch gestures for zooming between 1.0x-2.6x
- **Pan controls:** Single-finger drag to pan camera when zoomed in
- **Camera bounds:** Automatic bounds calculation based on map content with margin
- **Readability scaling:** HUD and tooltip font sizes scale with zoom level for optimal readability
- **Direct/inverse zoom mapping:** Auto-calibrated zoom behavior based on device characteristics

**Session Persistence:**
- **Auto-save on pause:** Game state automatically saved when app goes to background
- **Auto-restore on launch:** Seamlessly resume interrupted runs from main menu (only when a run is genuinely in progress - navigating to main menu clears the session)
- **12-hour expiration:** Saved sessions expire after 12 hours to avoid stale state
- **Complete state capture:** Saves towers placement, modifiers, wave progress, stats, map seed, current draft options, and pick position
- **Anti-reroll:** Draft options are serialized into the snapshot so reloading mid-draft always restores the exact same cards

**Touch Interface:**
- **Drag scrolling:** All scrollable areas (leaderboards, how-to-play, end screen) support touch drag
- **Gesture suppression:** Tap detection properly handles multi-touch and pan gestures
- **Touch sizing:** Larger hit areas for slot placement and tower selection
- **Mobile-specific buttons:** "Main Menu" button on end screen, mobile-optimized layout

**UI Enhancements:**
- **Auto-resume flow:** Direct launch to game scene when session exists
- **Adaptive card sizing:** Draft cards scale properly on narrow viewports using unscaled UI space
- **Session cleanup:** Automatic cleanup on manual menu navigation or run completion
- **Mobile detection:** Enhanced platform detection including web exports on mobile devices
- **Back button:** System back navigates contextually - menu screens return to previous screen, draft opens pause menu, active wave shows a quit confirmation dialog

---

## Towers (5)

| Tower | Base Damage | Attack Interval | Range | Special |
|---|---:|---:|---:|---|
| Rapid Shooter | 10 | 0.45 s | 285 px | Fast single-target fire |
| Heavy Cannon | 56 | 2.0 s | 238 px | Heavy burst hits |
| Marker Tower | 7 | 1.0 s | 333 px | Applies Marked on hit |
| Arc Emitter (`chain_tower`) | 18 | 1.2 s | 257 px | Base chain: +2 bounces, 400 px chain range, 60% damage carry per hop |
| Rift Sapper (`rift_prism`) | 22 | 0.98 s | 230 px | Charged mine trap tower with wave-start rapid seeding |

- Towers are placed into 6 fixed world slots.
- Each tower can hold up to 3 modifiers.

### Rift Sapper Deep-Dive

- Unlock: beat the second campaign map on Normal or Hard (`RIFT_UNSEALED`).
- Placement model:
  - plants mines on lane anchors within tower range
  - mine cap per tower: `7`
  - mine spacing: `46 px`
- Mine trigger model:
  - trigger radius: `32 px`
  - blast target radius: `82 px`
  - arm time after plant: `0.16 s`
- Charge model:
  - charges per mine: `3`
  - charge 1/2 damage multiplier: `0.65`
  - final charge damage multiplier: `1.15`
  - per-trigger rearm lockout: `0.18 s`
  - base mine multiplier: `1.00x` tower base damage (before charge multipliers)
- Wave-start burst model:
  - burst window: first `2.4 s` of each wave
  - burst interval multiplier: `x0.55` (faster planting)
  - burst cap: `+3` accelerated plants per Rift Sapper per wave
- Modifier interactions on mines:
  - Split Shot mini-mine planting occurs on final-charge pops only
  - Chain Reaction mine-chain propagation occurs on final-charge pops only
  - chain-triggered mine detonations force final-pop behavior to preserve cascade feel

---

## Modifiers (10)

| Modifier | Current Effect |
|---|---|
| Momentum | +16% damage per consecutive hit on same target, up to 5 stacks (max x1.80), resets on target switch |
| Overkill | 60% of excess kill damage spills to next enemy in lane |
| Exploit Weakness | +45% damage vs Marked enemies |
| Focus Lens | +140% damage, x1.85 attack interval |
| Chill Shot (`slow`) | On hit: -30% move speed (factor 0.70) for 6 s; copies on the same tower stack multiplicatively |
| Overreach | +45% range, -10% damage |
| Hair Trigger | +30% attack speed, -18% range |
| Split Shot | Fires 2 split projectiles at 35% damage each, each extra copy adds +1 split projectile |
| Feedback Loop | On kill, instantly resets cooldown to zero |
| Chain Reaction | Adds +1 chain bounce per copy and sets chain carry to 60% |

### Modifier Color Language

| Category | Normal Palette | Colorblind Palette |
|---|---|---|
| DamageScaling | orange | bright yellow |
| Utility | cyan | bright blue |
| Range | violet | near-white |
| StatusSynergy | magenta | deep orange |
| MultiTarget | mint-green | bright teal |

- DamageScaling: Momentum, Overkill, Focus Lens, Hair Trigger, Feedback Loop
- Utility: Chill Shot
- Range: Overreach
- StatusSynergy: Exploit Weakness
- MultiTarget: Split Shot, Chain Reaction

Used consistently in draft cards, proc halos, and live modifier icons. Colorblind palette toggled via Settings.

---

## Enemies

| Enemy | HP Base | Speed | Leak Cost | Waves |
|---|---|---|---:|---|
| Basic Walker | `65 * 1.10^(wave-1)` | 120 px/s | 1 | 1–20 |
| Armored Walker | 3.5× Basic HP | 60 px/s | 2 | 6–20 |
| Swift Walker | 1.5× Basic HP | 240 px/s | 1 | 10–19 (skips 12, 20) |
| Reverse Walker | 1.35× Basic HP | 108 px/s | 1 | 11+ (full game) |
| Splitter Walker | 1.8× Basic HP | 90 px/s | 3 | 9–15 |
| Splitter Shard | 0.55× Basic HP | 165 px/s | 1 | spawned on Splitter death |

- Armored first appears on wave 6. Max count in default wave data is 3 (wave 20).
- Swift appears in waves 10–19 (skips wave 12 and 20).
- Reverse Walker appears from wave 11 onward in full-game wave compositions and endless scaling.
- Waves 12–14 can use clumped Armored spawn blocks (`ClumpArmored`).
- Splitter Walker spawns 2 Splitter Shards on death, offset -4 px and -22 px behind the death position. Shard spawn is handled by `CombatSim.SpawnShards()`.

### Enemy Visuals

- Basic: round teal body.
- Armored: larger crimson hex body, rendered at 1.5x scale.
- Swift: small lime diamond, rendered at 0.8x scale.
- Reverse: cyan/magenta phase-arrow silhouette with explicit rewind beam/chevron trail feedback.
- Splitter: amber hex with straining lobes and a bisecting crack; simple-path fallback for low-perf mode.
- Shard: irregular crystal fragment polygon; simple-path fallback available.
- HP bar color shifts with health.
- Marked ring: rotating purple arcs.
- Slow ring: cyan ring plus blue-grey tint.
- Spawn animation: 0 -> full scale over 0.15 s.

---

## Status Effects

- Marked:
  - +40% incoming damage from all towers.
  - Duration: 4.0 s.
- Slow:
  - Movement speed factor: 0.75.
  - Duration: 5 s.

---

## Difficulty Modes

| Mode | Enemy HP | Enemy Count | Spawn Interval |
|---|---:|---:|---:|
| Normal | 1.0x | 1.0x | 1.0x |
| Hard | 1.1x | 1.1x | 0.95x (faster spawns) |

### Tension Warning

If a clumped armored wave is incoming (with enough armored units), gameplay uses:
- Warning text: `ARMORED WAVE INCOMING`
- Brief pulse overlay
- Initial spawn delay: 0.8 s

---

## Draft Placement Previews

- **Tower range circle:** When hovering a tower card over an empty slot, the ghost tower displays a pulsing range fill and border ring so players can evaluate coverage before confirming.
- **Modifier range overlays:** Hovering Overreach over a tower shows a larger pulsing ring at the post-modifier range (`×1.45`); hovering Hair Trigger shows a smaller ring at the post-modifier range (`×0.82`). Both rings use the modifier's accent color.

---

## Draft System

- If free slots exist:
  - Draft targets 5 options with a 2 tower + 3 modifier split.
  - If no valid modifier targets exist, missing modifier cards are backfilled by tower cards.
- If all slots are occupied:
  - 4 modifier options.
- Anti-brick rule:
  - Modifier cards are only offered when at least one tower can still accept one.
- Bonus picks: currently disabled (Wave1ExtraPicks = 0, Wave15ExtraPicks = 0).

---

## Endless Mode

Accessible from the win end screen after completing all 20 waves.

- "Continue - Endless" button appears on the win screen.
- Clicking it continues the run from wave 21 with no victory condition.
- **Scaling per endless wave** (depth = waves past 20):
  - Enemy count: ×(1.05^depth) - compounding +5% per wave
  - Enemy HP: ×(1.02^depth) - compounding +2% per wave
  - Every 5 endless waves (+25, +30, +35…): +1 Swift Walker added to the wave
  - Spawn interval decreases slowly (floored at 0.70 s)
- HUD shows "Wave 21 ∞", "Wave 22 ∞", etc.
- Losing in endless shows the normal loss screen with the actual wave reached.
- **Leaderboard**: the wave-20 win entry is NOT submitted to the global leaderboard if the player continues to endless. When the endless run ends, only the endless loss result is submitted. Local high scores update on win as normal.
- Balance constants: `EndlessEnemyCountScalePerWave`, `EndlessEnemyHpScalePerWave`, `EndlessSwiftBonusInterval`, `EndlessSpawnIntervalFloor` in `Balance.cs`.

### Placement Flow (Preview -> Confirm for modifiers)

1. Pick modifier card.
2. Valid tower slots highlight.
3. First tap on a valid tower slot creates a preview ghost.
4. Second tap on the same slot confirms assignment.

Input details:
- Tapping elsewhere while preview exists cancels preview.
- Mobile fallback accepts tapping the tower body.
- Guard window prevents immediate accidental confirm from duplicate touch/mouse events.

---

## Tutorial Map

Accessible via the "▶  Tutorial" button on the main menu (above PLAY).

- **Duration:** 8 waves, always Easy difficulty.
- **Scripted drafts:** waves 0–5 have curated options; waves 6–7 are random.
  - Wave 0: Rapid Shooter only
  - Wave 1: Focus Lens vs Hair Trigger
  - Wave 2: Overreach only
  - Wave 3: Marker Tower only
  - Wave 4: Exploit Weakness only
  - Wave 5: Chain Reaction / Momentum / Chill Shot (3-way choice)
- **Contextual teaching panels** (all click-to-dismiss; cleared on draft start):
  - Each draft wave 0–5: callout explaining the card being offered
  - Wave 0 wave start: auto-dismiss callout explaining the path/lives
  - Wave 4 wave start: auto-dismiss callout on Armored Walker appearance
  - First modifier placed: "BUILD NAME" panel — pauses, highlights build label with cyan rect, explains the naming system
  - Wave 3 draft: "TARGETING" panel — pauses game, explains First/Strongest/Lowest HP modes, unpauses when player clicks any tower to cycle mode
  - First tower surge: "SURGE" panel — pauses wave, highlights global surge bar with cyan rect, explains surge/global surge; persisted via `SurgeTutorialSeen` flag (shows once)
  - First global surge ready: "GLOBAL SURGE READY" panel — pauses wave, gold highlight on surge bar, "Activate →" button fires the surge; global surge meter pre-filled to 95% at wave 6 start to ensure it fires naturally during combat
- **Persistence:** completion stored in `SettingsManager.TutorialCompleted`. Button on main menu becomes muted after first completion.
- **Isolation:** tutorial map excluded from map select, leaderboards, and score submission. `DataLoader.GetAllMapDefs()` filters it by default (`includeTutorial = false`).
- **End screen:** shows "How to Play →" button; hides leaderboard/endless buttons; labels play-again "Play Again · Real Run →" (goes to map select).
- **Wave count:** `_mapTotalWaves = 8` is propagated to HUD, win/loss screens, and wave announcement so no reference to 20-wave totals leaks in.
- **Implemented in:** `TutorialManager.cs` (scripted picks + callout text, pure C#), `TutorialCallout.cs` (CanvasLayer overlay queue), `GameController.cs` (lifecycle integration, blocking panels), `SettingsManager.cs` (`TutorialCompleted` + `PendingTutorialRun`), `maps.json` (`"tutorial"` key with inline `tutorialWaves` array).

---

## Map Selection

Before a run:
- Player can choose hand-crafted maps or the random procedural map.
- First map auto-selects on map select entry.
- Random map row shows a deterministic generated map name (`MAP: <seed name>`).
- Procedural seed is carried into runtime generation for consistent layout.

---

## Targeting Modes

Each tower cycles modes via left click/tap during waves:

- Right arrow icon: First (highest path progress)
- Star icon: Strongest (highest current HP)
- Down arrow icon: Lowest HP (finisher)

Target mode icon is drawn in a fixed upright badge on the slot node (to the right of tower), not rotated with the tower.

Rift Sapper uses a tower-specific label/icon set for the same internal modes:
- Die icon: Random (`First` internally)
- Down arrow icon: Closest (`Strongest` internally)
- Up arrow icon: Furthest (`Lowest HP` internally)

---

## Procedural Map (random map pathing)

- Grid: 8 columns x 5 rows, cell size 160 x 128, origin y=80.
- Path generation uses 3 long zigzag archetypes with seeded row/column variation.
- Optional horizontal mirroring (plus occasional vertical mirroring) varies start/end sides.
- Slot placement uses 6 non-path cells in a row adjacent to the dominant horizontal leg (with fallback row fill).
- Rendering:
  - Neon grid background (`GridBackground`).
  - Multi-layer path rendering (5 Line2D passes for haze/fill/edges).
  - Animated path flow arrows (`PathFlow`).
- Restart teardown uses `Free()` for immediate cleanup.

---

## HUD

Top bar includes:
- Wave label: `Wave X / 20`
- Enemy counter: `alive / total` during active wave
- Lives label with red state at <=3 and punch flash on life loss
- Speed toggle: 1x -> 2x -> 3x
- Pause access:
  - Desktop: ESC button/hint
  - Mobile: hamburger button
- Build name shown in the top bar (left side) during waves
- **Global surge bar (bottom center):** 20 pip meter. When full, switches to gold pulsing "▶  ACTIVATE" state - player must click to fire. Click triggers all surge effects and resets meter.
- **Mandate banner (campaign only):** Full-width dark-orange strip directly below the HUD bar showing the active stage mandate (e.g. "MANDATE: Rift Sapper locked · Momentum · Exploit Weakness · Split Shot banned"). Hidden in skirmish/free play.

Speed polish:
- Center toast: `SPEED Xx` with streak FX.
- Subtle audio speed feel at 2x/3x.

Current behavior decision:
- Speed resets to 1x on run restart (not on every wave clear).

---

## Draft Panel

- Pick counter text supports multi-pick waves (infrastructure exists; extra picks currently disabled).
- Wave preview footer shows next wave composition.
- Cards auto-size based on viewport; card heights scale to fit longer text.
- Reveal ritual:
  - Face-down hold: 0.12 s
  - Stagger delay: 0.40 s per card
  - Entrance duration: 0.34 s
  - Flip reveal, burst FX, icon/title punch
  - Card back shows one large `?`
  - Reduced motion: skip flip entirely - cards appear face-up immediately (toggle in Settings)
- Lock-in effects:
  - Card pick thunk + subtle vignette pulse
  - Modifier confirm lock-in SFX, border flash, icon snap
- Run memory line: current build name, lives, speed
- Mobile tap-hold preview on cards (250 ms)
- Card spirit transition from draft card to world hint area
- Bonus pick animated stamp (shown when extra picks are active)
- Rare foil shimmer (1 in 12 drafts)
- **First-run tutorial banner** (regular game only, not shown in tutorial map): Two-page overlay shown on wave 1, pick 1 of the player's first run (controlled by `SettingsManager.IsFirstRun` / `RunsStarted <= 1`):
  - Page 1 ("HOW THIS WORKS"): anchored at top of screen; explains the draft mechanic and wave loop.
  - Page 2 ("SURGES"): repositions to just above the global surge meter at the bottom of the screen. A cyan glow border highlights the surge meter panel and a cyan connector line runs from the tutorial panel down to the meter, pointing at it directly. The tutorial panel itself adopts the same cyan glow border for visual cohesion.
  - "How to Play →" button on page 2 deep-links into the Surges tab of HowToPlay.
  - Reset via Settings → PROFILE → Reset Tutorial.
- Synergy hints:
  - Small tags (for example `GOOD WITH: MARKED`)
  - Synergy tower pulse highlights on hover/hold
- Keyboard support:
  - `1-5` card select
  - key hint labels on cards
- Cancel while awaiting world placement:
  - ESC returns to draft choices

---

## Wave Announcement and Beats

On wave start (not in bot mode):
- Large center wave label with scale/alpha tween.
- Wave 10: `HALFWAY` beat with short lift cue.
- Wave 20: enhanced theater (special sound cues, wave label pulse, path flow surge).
- Waves 15–20: gradual music tension ramp (up to +3.5 dB / +2.5% pitch at wave 20).
- Signature scanline flourish on key beats.

---

## Procedural Music System

`MusicDirector` (autoload sibling of `GameController`) drives an adaptive procedural score with no audio files - all sound is synthesized from the MIDI note pool in `SoundManager`.

**Phase 1 - Clock + synthesis foundation:**
- `MusicClock`: drift-free beat/bar/phrase events at a tunable BPM; supports smooth BPM ramps.
- `SoundManager` note pool: polyphonic MIDI-pitched note playback (range MIDI 28–81).

**Phase 2 - Harmony + bass:**
- `MusicHarmony`: scale/chord tables for Dorian, Mixolydian, and Phrygian modes; chord-degree progressions per tension tier.
- `MusicBassLayer`: root + fifth chord-following bass line, density-gated per bar, driven by `MusicHarmony`.

**Phase 3 - Game-state hooks:**
- `MusicDirector` subscribes to: `OnWaveStart`, `OnWaveClear`, `OnLivesChanged`, `OnDraftPhaseStart`, `OnRunEnd`.
- Each event adjusts tension tier (Intro → Calm → Rising → Tense → Critical), BPM, mode (Dorian/Mixolydian/Phrygian), and bass/melody density.
- Wave clear triggers a breath pause (bass silent for N bars) then restores layer.

**Phase 4 - Melody layer:**
- `MusicMelodyLayer`: phrase-planned improvised lead.
- Contour selection per phrase: Ascending, Descending, Arch, Static - weighted by tension tier.
- Rest probability scales with tension (more rests at low tension for breathing room).
- Cross-phrase continuity: melody starting pitch biased toward previous phrase ending.
- 417 xUnit tests covering harmony, bass, and melody logic.

**Phase 5 - Percussion layer:**
- `MusicPercLayer`: 8th-note kick/snare/hat grid with 4 tension-driven drum styles.
- Density arc: hats suppressed for the first 2 bars of each new wave for a natural build-in feel.
- Ambient pad fade-out on tension tier shift.

**Phase 6 - Surge percussion fill:**
- Global surge trigger wired from `GameController` → `MusicDirector` → `MusicPercLayer`.
- On global surge: queues a 1-bar double-kick + snare-roll fill before resuming normal pattern.

**Phase 7 - Chord-aware melody + walking bass:**
- `MusicMelodyLayer`: phrases planned in 4-bar segments; note selection biased toward the chord root pitch class at each bar boundary (40% hard snap, 60% drift) so the melody locks to harmony without sounding mechanical.
- `MusicBassLayer`: 30% chance on beat 3 to substitute a walking scale tone between root and fifth, giving the bass line more motion.

**Phase 8 - BPM tuning + per-map spread:**
- `MusicHarmony`: base BPM tiers raised to 112 / 128 / 140 (previously 72 / 88 / 96) for a more energetic feel.
- `MusicDirector`: per-map BPM spread widened - Gauntlet +24, Sprawl −24, Random +10 - so map identity is reflected in pacing.
- `MusicDirector.OnEndlessContinue()`: restarts clock and reactivates all layers after `OnRunEnd` stops them, fixing music going silent when the player continues to Endless mode.

---

## Slot Visuals

Per slot visuals:
- Empty slot square with neon border.
- Modifier pips (3) below slot, shown when tower exists.
- Modifier icons (3) below pips, one per equipped modifier.
- Draft highlights:
  - Gold for valid tower placement
  - White for valid modifier targets
  - Red for invalid/ineligible
- Proc halo on modifier activation (0.2 s).
- Live modifier icon pulse on activation; pulses all matching modifier IDs on that tower.

---

## Tower Visuals

- Procedural per-tower bodies in `_Draw()`.
- Cooldown ring drawn behind tower geometry.
- Range fill + range border.
- Smooth target-facing rotation (`LerpAngle`, 15 rad/s).
- Placement bounce: 0 -> 1.15 -> 1.0.
- Attack flash and recoil kick (Heavy Cannon uses stronger recoil).

### Personality and Firing Motion

Tower idle parts are reused as firing animation parts:
- Rapid Shooter: barrel sway + muzzle kick
- Heavy Cannon: piston idle + barrel slam-back recoil
- Marker Tower: antenna recoil + core pulse
- Arc Emitter: electric flicker + prong flare surge

Target lock readability:
- Faint target lock line appears briefly when repeatedly firing same target.

---

## Projectiles and Combat FX

- Projectile:
  - Diamond head, larger glow bloom
  - Trail history: 14 samples
  - More visible trail alpha/width ramp
- If target dies mid-flight, projectile dissolves.
- Damage number:
  - Standard: colored floating number
  - Kill shot: larger gold number
  - Source hint notch by tower family color
- Enemy hit flash on non-kill hits.
- Kill hitstop:
  - Typical kill: ~0.04 s slowdown
  - Heavy Cannon kill: stronger/longer variant
- Death burst:
  - ring + center flash + 16 sparks + inner ring
- Chain arcs:
  - jagged arc with endpoint glow, fade ~0.18 s
- Retarget readability:
  - short target-acquire ping (8 px -> 14 px)

Audio polish:
- Kill combo pitch ramps up in short windows (clamped at 1.15x).
- Heavy Cannon shots duck music slightly for clarity.
- Rapid Shooter projectile volume reduced by 20%.
- **Chill Shot sound variant:** Rapid Shooter and Arc Emitter towers with the `slow` modifier play `shoot_rapid_cold` (softer, icier tone at 420 Hz) instead of `shoot_rapid`. Heavy Cannon and Marker Tower are unaffected (they use their own distinct sounds).
- **Modifier-based pitch scaling:** each equipped modifier shifts the shoot sound pitch multiplicatively - Focus Lens 0.78× (deeper), Hair Trigger 1.22× (brighter), Chain Reaction 0.88×, Overreach 0.92×, Overkill 0.94×. Stacks are multiplicative, clamped by `SoundManager.Play()` to 0.75–1.40×.

---

## Tooltip

Visible during:
- Active wave
- Modifier assignment phase in draft

Shows:
- Slot and tower name
- Targeting mode
- Effective damage, effective interval, range
- Modifier list with descriptions
- Spectacle trigger section:
  - Current previewed spectacle effect
  - All possible spectacle outcomes for the tower's currently equipped supported modifiers
  - Single/Combo/Triad labeling based on available supported modifier diversity

Mobile:
- Tooltip anchors to selected tower rather than cursor.

---

## Global Feedback Systems

- Screen shake on leak.
- Lives label flash on leak.
- Low-life heartbeat (during waves at <=2 lives).
- Short `CLUTCH` / `TOO CLOSE` toast on low-life leaks.
- Wave clear flash + short hold before next draft.
- Combat callouts for major proc moments:
  - `OVERKILL SPILL` (when excess damage transfers)
  - `FEEDBACK LOOP` (when cooldown reduction triggers)
  - `CHAIN REACTION` (when chain modifiers activate)
  - **Implementation:** World-space notifications with cooldown-gating to prevent spam
  - **Thresholds:** Only triggers on meaningful proc counts to avoid noise

---

## Visual Identity and Font

- Font family: Rajdhani variants via `UITheme`.
  - Default runtime UI font: `Rajdhani-SemiBold.ttf`
  - Strong emphasis/headings: `Rajdhani-Bold.ttf`
- Neon synthwave UI theme is built in code (`UITheme.Build()`).
- Scene transitions use `Transition` autoload fade to black/back.

---

## End Screen

Win:
- Title: `VICTORY` (amber on Hard difficulty)
- Subtitle: `All 20 waves survived · N lives remaining` (appends `  -  HARD` on Hard)
- Play Again button escalates difficulty: Easy → `"Play Again  ·  Try Normal →"`, Normal → `"Play Again  ·  Try Hard →"`
- **Campaign win:** Play Again / Continue Endless / Open Leaderboards hidden. "Next Stage: {name}  →" shown if not the final stage. "Campaign Select" replaces "Main Menu" and returns to `CampaignSelect.tscn`. Sector stamp (e.g. `SECTOR BREACHED`) injected below subtitle; final stage shows completion text.

Loss:
- Title: `GAME OVER`
- Subtitle includes reached wave and lives lost.
- **Campaign loss:** Leaderboard / Play Again buttons hidden. "Campaign Select" replaces "Main Menu".

Both states show:
- Enemies killed
- Total damage dealt
- Build name
- MVP tower line
- Most valuable modifier line (by proc count)
- Slot-by-slot build summary
- Leaderboard rank with gap to next rank (e.g. `rank #47  - 293 from #46`)

Dismiss with click, Enter, or Space.

---

## Pause, Main Menu, Settings

Pause screen:
- Resume, Restart Run, Settings, How to Play, Achievements, Main Menu, Quit
- Achievements opens as an inline overlay (same pattern as How to Play - no scene change)
- ESC/back handling supports subpanel navigation
- Blocked during win/loss state

Main menu:
- Code-driven layout
- Buttons: Tutorial (above Play, cyan when not yet completed / muted when done), Play, Leaderboards, Achievements, How to Play, Settings, Quit
- Animated neon grid background

Settings:
- Master/Music/FX sliders (0-100)
- Display: Windowed/Fullscreen toggle
- Colorblind mode toggle (high-contrast modifier accent palette)
- Reduced motion toggle (skip draft card flip animations)
- Screen Filter / VHS Glitch toggles
- Enemy FX toggles: Layered Rendering, Emissive Lines, Damage Material, Bloom Highlights
- **PROFILE section** (visible to all users):
  - **Reset Tutorial** button: shows inline Yes/No confirmation; on Yes, resets `RunsStarted = 0` so the first-run guidance banner appears again on the next run
- DEVELOPER section (DevMode only): Reset All Achievements
- Saved to `user://settings.cfg` (account/preferences, cloud-synced) and `user://display.cfg` (device-specific, not synced)
- Music and FX buses are created if missing

---

## Bot / Playtest Mode

Launch example:

```text
--scene res://Scenes/Main.tscn -- --bot --runs N
```

Behavior:
- High-speed simulation loop.
- Strategies rotate across map/difficulty combinations.
- Spectacle gameplay payloads are applied in bot mode for surge/global surge triggers (matching live gameplay logic).
- Minor spectacle trigger tier no longer exists in runtime or bot reporting.
- Current strategy set has 12 entries:
  - Random
  - TowerFirst
  - GreedyDps
  - MarkerSynergy
  - ChainFocus
  - SplitFocus
  - HeavyStack
  - RiftPrismFocus
  - SpectacleSingleStack
  - SpectacleComboPairing
  - SpectacleTriadDiversity
  - PlayerStyleKenny
- Summary output includes win rates, wave curves, usage analysis, and spectacle trigger analysis (tier totals + top effects).

---

## Balance Constants (`Balance.cs`)

| Constant | Value |
|---|---:|
| TotalWaves | 20 |
| SlotCount | 6 |
| StartingLives | 10 |
| MaxModifiersPerTower | 3 |
| DraftOptionsCount | 5 |
| Wave1ExtraPicks | 0 |
| Wave15ExtraPicks | 0 |
| BaseEnemyHp | 65 |
| HpGrowthPerWave | 1.10 |
| BaseEnemySpeed | 120 |
| TankyHpMultiplier | 3.5 |
| TankyEnemySpeed | 60 |
| SwiftHpMultiplier | 1.5 |
| SwiftEnemySpeed | 240 |
| MarkedDamageBonus | +40% |
| MarkedDuration | 4.0 s |
| SlowSpeedFactor | 0.70 |
| SlowDuration | 6 s |
| MomentumMaxStacks | 5 |
| MomentumBonusPerStack | +16% |
| SplitShotDamageRatio | 35% |
| SplitShotRange | 280 |
| FeedbackLoopCooldownReduction | 100% (full reset to zero) |
| HairTriggerAttackSpeed | +30% |
| HairTriggerRangeFactor | -18% |
| OverkillSpillEfficiency | 60% |
| FocusLensDamageBonus | +140% |
| FocusLensAttackInterval | x1.85 |

---


## Campaign Mode — The Fracture Circuit

Linear 4-stage campaign with escalating mandates. Accessible from Play → Mode Select → Campaign.

### Scene flow

`MainMenu` → `ModeSelect` → `CampaignSelect` → `Main` (with active mandate) → `EndScreen` → `CampaignSelect`

### Stage ladder

| # | Stage | Map | Mandate |
|---|---|---|---|
| 1 | Orbit Breach | sprawl | Tower pool: Rapid, Heavy, Marker only. Split Shot banned. |
| 2 | Crossroads Interdiction | arena_classic | Tower pool: + Arc Emitter. Momentum, Exploit Weakness, Split Shot banned. |
| 3 | Pinch & Bleed | gauntlet | Tower pool: + Arc Emitter. Only 5 tower slots. Rift Sapper banned. |
| 4 | Iron Mandate | ridgeback | All towers. All modifiers. Enemies have +25% HP. |

- Stages unlock sequentially (each requires the previous cleared on any difficulty).
- Per-stage, per-difficulty clear state saved to `user://campaign_progress.cfg` via `CampaignProgress.cs`.

### Mandate system

`MandateDefinition` wraps `MandateDef` from `campaign_stages.json` and exposes:
- `IsTowerBanned(id)` — checked in `DraftSystem` to filter tower options
- `IsModifierBanned(id)` — checked in `DraftSystem` to filter modifier options
- `LockedSlotCount` — last N slots marked `IsLocked` by `GameController.ApplyCampaignMandate()`; locked slots are excluded from `HasFreeSlots()` / `FreeSlotCount()` and never receive towers
- `EnemyHpMultiplier` — applied as a final multiplier in `WaveSystem.GetScaledHp()`
- `IsActive` — true if any field is set (drives HUD banner visibility)

Mandate is stored on `RunState.ActiveMandate`. All mandate logic is a no-op when `CampaignManager.ActiveStage == null` so skirmish is completely unaffected.

### CampaignSelect screen

- Stage cards showing: number badge, name, subtitle, mandate badge (color-coded by type), difficulty checkmarks (E✓ N✓ H✓)
- Locked stages show "—" badge and "Clear previous stage to unlock"
- Right panel: difficulty selector, intro flavor text, Begin / Back buttons
- `OnBegin()`: sets `CampaignManager.ActiveStage`, calls `MapSelectPanel.SetPendingMapSelection`, fades to `Main.tscn`

### Key files

- `Data/campaign_stages.json` — stage definitions and mandate data
- `Scripts/Core/CampaignManager.cs` — static context holder (ActiveStage, IsCampaignRun)
- `Scripts/Core/CampaignProgress.cs` — ConfigFile-backed save
- `Scripts/Core/MandateDefinition.cs` — runtime mandate logic
- `Scripts/Core/CampaignStageDefinition.cs` — typed wrapper around CampaignStageDef
- `Scripts/UI/ModeSelectPanel.cs` — Campaign / Skirmish card screen
- `Scripts/UI/CampaignSelectPanel.cs` — Stage ladder UI

---

## Achievements

16 achievements tracked locally via `AchievementManager` (autoload). State persisted to `user://achievements.cfg`.

| ID | Name | Condition |
|---|---|---|
| FIRST_WIN | First Victory | Complete all 20 waves |
| HARD_WIN | Hard Carry | Complete all 20 waves on Hard |
| FLAWLESS | Flawless | Win without losing a life |
| LAST_STAND | Last Stand | Win with exactly 1 life remaining |
| HALFWAY_THERE | Halfway There | Survive to wave 10 (win or loss) |
| FULL_HOUSE | Full House | Fill all 6 tower slots in one run |
| STACKED | Stacked | Give any tower 3 modifiers in one run |
| SPEED_RUN | Speed Run | Win in under 15 minutes |
| ANNIHILATOR | Annihilator | Deal 100,000 total damage in one run |
| CHAIN_MASTER | Chain Master | Win with all 6 slots filled by Arc Emitters |
| ARC_UNSEALED | Arc Unsealed | Beat the first campaign map (unlocks Arc Emitter) |
| SPLIT_UNSEALED | Split Unsealed | Beat the second campaign map (unlocks Split Shot) |
| RIFT_UNSEALED | Rift Unsealed | Beat the third campaign map (unlocks Rift Sapper) |
| TUTORIAL_COMPLETE | First Steps | Complete the tutorial run |
| CAMPAIGN_CLEAR | The Circuit | Clear all four stages of The Fracture Circuit |
| CAMPAIGN_HARD_CLEAR | Iron Mandate | Clear all four stages of The Fracture Circuit on Hard difficulty |

**Unlock toast:** Small fade-in/out notification in the bottom-right corner when an achievement is newly unlocked. Multiple unlocks queue and show sequentially.

**Achievements screen:** Full-screen list showing all 16 achievements. Locked entries show `???` name and a generic hint. Unlocked entries show name, description, and a green border + star. Accessible from both main menu and pause menu (pause menu opens it as an inline overlay without leaving the game scene).

**Steam forwarding:** `AchievementManager.AchievementUnlocked` signal is subscribed by `SteamAchievements`, which forwards each newly unlocked ID to Steamworks when available. No Steam dependency in `AchievementManager` itself.

---

## Leaderboards and High Scores

**Steam Integration Status:** Fully live. Steamworks.NET integrated, Steam App ID active, global leaderboards operational.

### Local High Score System

- **HighScoreManager**: Persistent local high scores stored in `user://highscores.json`
- **Personal Best Tracking**: Wave reached + lives remaining for each map/difficulty combination
- **Map Select Integration**: Personal best displayed on the map selection screen

### Global Leaderboard Infrastructure

**Core Architecture:**
- **LeaderboardManager**: Global coordinator autoload, routes between local and platform services
- **ILeaderboardService**: Platform abstraction (Steam, future mobile platforms)
- **SteamLeaderboardService**: Steamworks.NET implementation for Steam global leaderboards
- **NullLeaderboardService**: Fallback when platform unavailable

**Score Calculation:**
- **ScoreCalculator**: Converts run stats to a comparable integer score
- **Algorithm**: `win_bonus(1,000,000,000) + wave_reached × 10,000,000 + lives_remaining × 100,000 + time_bonus`
- Wins always rank above losses; within wins/losses, wave count is the primary tiebreaker

### Submission Flow

**Run Completion:**
- Automatic local high score update on win/loss
- Async global leaderboard submission (non-blocking)
- **Retry Queue**: Failed submissions persisted to `user://leaderboard_retry_queue.json`
- **Background Sync**: Periodic retry queue flush every 30 seconds

**End Screen Integration:**
- Leaderboard submission status line (success/fail/offline)
- "View Global Leaderboard" button (context-aware for current map/difficulty)
- Prevents accidental menu navigation when interacting with leaderboard UI
- Button opens dedicated leaderboard screen with preselected map/difficulty


### Dedicated Leaderboard Screen

**Build Name Label:**
- Each leaderboard row (local and global) now displays a large build-name label on the right side.
- Build names are generated per entry from that row's map/difficulty/stats/build snapshot via `RunNameGenerator.cs`.
- **Gradient rendering:** Build names use color gradients based on modifier family and MVP tower colors.
- **Text truncation:** Long build names are truncated to 30 characters with proper clipping.
- **UI specifics:** right-side label is font size 24, clipped, right-aligned, with a minimum width of 360px. Left stats text remains on the same row and is clipped to avoid overlap.
## Build Name Generation and Profile Analysis

**RunNameGenerator.cs**
- Dedicated naming engine for build/run names, used in:
  - In-run HUD names (`BuildRunName()`)
  - End-screen names (win/loss)
  - Leaderboard entry names (local/global)
  - Color gradient logic (now based on generated profile)
- Names consider: primary/secondary modifier family, MVP/support tower, map, difficulty, pace, win/loss, clutch state, tower diversity
- Uses multiple templates and a large vocabulary for variety
- Anti-repeat: recent names are tracked in `user://run_name_history.cfg` and avoided for final run names
- **Color resolution:** `ResolveNameColors()` generates gradient colors based on modifier family and MVP tower

**RunNameProfile.cs**
- Structured profile model for build/run analysis
- Used by `RunNameGenerator` to select templates, vocabulary, and color gradients

**GameController.cs**
- Rewired to use `RunNameGenerator` for all build/run name generation and color logic

**UI Details:**
- LeaderboardsMenu.cs: Each leaderboard row displays the build name label on the right (font size 28, right-aligned, min width 280, clipped)
- Left stats text is clipped to avoid overlap with the build name

**New Implementation:** Full-featured leaderboard browser accessible from main menu and end screen.

**Screen Features:**
- **Leaderboards.tscn + LeaderboardsMenu.cs**: Dedicated leaderboard viewing interface
- **Main Menu Integration**: "Leaderboards" button opens the screen directly
- **End Screen Integration**: "View Global Leaderboard" button opens screen with context preselection
- **Mode Switching**: Local personal bests vs Global leaderboards toggle
- **Map Selection**: Browse leaderboards for all available maps
- **Difficulty Switching**: Normal/Hard difficulty selection
- **Policy Enforcement**: Random map restricted to local-only (global blocked)

**Backend Integration:**
- **Global Fetch API**: `LeaderboardManager.FetchGlobalLeaderboard()` for downloading entries
- **Steam Implementation**: `SteamLeaderboardService.DownloadLeaderboard()` top-entries retrieval
- **Local Feed**: `HighScoreManager.GetPersonalBests()` for local leaderboard display

### Steam Leaderboard Setup

**Required Steam Leaderboard IDs** (to be created in Steamworks backend):
- `leaderboard_crossroads_normal`
- `leaderboard_crossroads_hard` 
- `leaderboard_gauntlet_normal`
- `leaderboard_gauntlet_hard`
- `leaderboard_sprawl_normal`
- `leaderboard_sprawl_hard`
- `leaderboard_random_normal`
- `leaderboard_random_hard`

**Configuration Requirements:**
- Sort Method: Descending (higher scores better)
- Display Type: Numeric
- Upload Score Method: Always (every run stored - no keep-best dedup)

### Platform Support

- **Windows Desktop**: Full Steam leaderboard integration via Steamworks.NET
- **Android**: Local high scores only (global leaderboards planned for future mobile backend)
- **Offline Mode**: Graceful degradation to local-only scoring with retry queue

---

## Notes

- This file is intentionally aligned to code/data as of 2026-03-19 (post v0.2.5).
- If gameplay values change, update:
  - `Data/towers.json`
  - `Data/modifiers.json`
  - `Data/waves.json`
  - `Scripts/Core/Balance.cs`
  - and this document together.
