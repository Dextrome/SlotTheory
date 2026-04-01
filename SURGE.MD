# Surge & Spectacle System -- Complete Reference

Slot Theory's spectacle system is a two-tier excitement engine layered on top of the core damage pipeline. Every proc from a supported modifier feeds a per-tower **Spectacle Meter**. When that meter fills, the tower fires a **Surge** -- a named visual + gameplay event whose flavor is determined by the tower's modifier build. Enough tower surges fill a shared **Global Meter**, which the player can then activate as a **Global Surge** -- a map-wide catharsis event.

---

## PLAYER-FACING TRUTH (IN GAME)

- Every tower has a Surge Meter that fills as it deals damage. When full, it triggers a Surge.
- **1 mod = core surge** from that mod.
- **2 mods = core surge + second mod effect**.
- **3 mods = core surge + second mod effect + augment boost** (Pulse / Strike / Recharge).
- All equipped mods contribute equally to the surge's **category** (Spread / Burst / Control / Echo), which drives distinct visuals, audio, and gameplay behavior.
- Each tower surge fills the shared Global Surge bar.
- The Global Surge label shows the dominant surge type your build has leaned toward **since the last Global Surge**, based on all mods on all surging towers.

UI teaching is intentionally minimal: surge category callout and `Global Surge`.

---

## INTERNAL SYSTEM TRUTH (FOR IMPLEMENTATION)

The sections below keep the full implementation detail (signatures, IDs, mode taxonomy, tuning and FX internals). This richer structure is for code/design maintenance, not for player onboarding.

---

## 1. Supported Mods and Towers

Only the following modifier IDs are tracked by the spectacle system:

| Canonical ID | Display Name |
|---|---|
| `momentum` | Momentum |
| `overkill` | Overkill |
| `exploit_weakness` | Exploit Weakness |
| `focus_lens` | Focus Lens |
| `slow` | Chill Shot (`chill_shot` normalizes to `slow`) |
| `overreach` | Overreach |
| `hair_trigger` | Hair Trigger |
| `split_shot` | Split Shot |
| `feedback_loop` | Feedback Loop |
| `chain_reaction` | Chain Reaction |
| `blast_core` | Blast Core |
| `wildfire` | Wildfire |
| `afterimage` | Afterimage |
| `reaper_protocol` | Reaper Protocol |
| `deadzone` | Deadzone |

Supported tower IDs: `rapid_shooter`, `heavy_cannon`, `rocket_launcher`, `marker_tower`, `chain_tower`, `rift_prism`, `accordion_engine`, `phase_splitter`, `undertow_engine`, `latch_nest`.

Any proc from an unsupported mod or on an unsupported tower is silently ignored.

---

## 2. Per-Tower State

Each tower tracked by the system has a private `TowerState` containing:

- `Meter` -- current spectacle meter value (float)
- `SurgeCooldown` -- remaining lockout after a surge (seconds)
- `InactivityTime` -- time since last proc (for decay)
- `Pulse` / `PulseHold` -- visual pulse animation state
- `ModCooldowns[modId]` -- per-mod-per-tower cooldown timer; prevents same mod from contributing more than once per `ModProcCooldownSeconds` (0.25s)
- `LoadoutSignature` -- hash of current modifier loadout; if this changes, the entire state resets

State resets automatically when the tower's modifier loadout changes (new mod drafted onto the tower).

---

## 3. Registering a Proc

Two entry points feed the system:

### `RegisterProc(tower, modifierId, eventScalar, eventDamage)`
Called by modifier code after the mod does something meaningful (kill, hit, bounce, etc.). Validates inputs, normalizes the mod ID, checks it is supported, then calls `RegisterProcInternal`.

### `RegisterShotFired(tower)`
Called every time a tower fires. Only acts if the tower has at least one `hair_trigger` modifier. Uses the HairTrigger bucket scalar (Low = 0.75) and an estimated hit damage, then calls `RegisterProcInternal`.

---

## 4. Mod Proc Rate Limiter

Before computing meter gain, each proc checks a per-mod-per-tower cooldown:

```
if ModCooldowns[modId] > 0 → skip proc (no meter gain)
ModCooldowns[modId] = ModProcCooldownSeconds   (0.25s)
```

The same mod on the same tower can contribute at most once every 0.25 seconds. There is no per-mod cap or regen curve -- the gate is a simple flat cooldown applied uniformly to all mods.

---

## 5. Meter Gain Formula

When a proc passes the mod cooldown gate, meter gain is:

```
gain = BaseGain(mod)
     × eventScalar
     × CopyMultiplier(copies)
     × MeterGainScale                          -- (0.75 / 1.30 ≈ 0.577)
     × MeterGainMultiplier                     -- tuning, default 1.0
     × TowerMeterGainMultiplier(towerId)       -- per-tower tuning
```

### Base Gain per Modifier
| Modifier | BaseGain |
|---|---|
| momentum | 2.4 |
| overkill | 2.9 |
| exploit_weakness | 3.0 |
| focus_lens | 2.7 |
| slow (chill) | 2.4 |
| overreach | 2.5 |
| hair_trigger | 2.2 |
| split_shot | 2.1 |
| feedback_loop | 3.1 |
| chain_reaction | 2.2 |
| blast_core | 2.3 |
| wildfire | 2.2 |
| afterimage | 2.4 |
| reaper_protocol | 3.2 |
| deadzone | 2.3 |

Per-mod `GainMultipliers` from the tuning profile multiply these.

### Copy Multiplier
Rewards stacking the same modifier:
```
copies = 1 → 1.00×
copies = 2 → 1.92×
copies ≥ 3 → 2.70×
(all × CopyMultiplierScale, default 0.8532)
```
Effective at tuned defaults: 1×=0.853, 2×=1.638, 3+=2.304.

### Per-Tower Meter Gain Multipliers (hardcoded defaults; pipeline overrides via JSON)
| Tower | Multiplier | Rationale |
|---|---|---|
| rapid_shooter | 0.40× | Fires 4+/s -- heavy dampening prevents over-surging |
| heavy_cannon | 3.50× | Very slow fire rate -- needs large per-hit boost |
| rocket_launcher | 1.45× | Moderate AoE cadence |
| marker_tower | 0.70× | Slightly above-average rate, trimmed |
| chain_tower | 1.20× | Moderate rate, small boost |
| rift_prism | 1.50× | Mine pops infrequent, boost per-proc |
| accordion_engine | 3.00× | 3.2 s pulse interval -- needs large per-hit boost |
| phase_splitter | 0.75× | Hits 2 targets per shot (double proc rate), trimmed |
| undertow_engine | 2.40× | Low cadence control shot |
| latch_nest | 0.46× | Frequent parasite ticks -- dampened to prevent overflow |

---

## 6. Event Scalars Per Modifier

Each modifier passes an `eventScalar` (a float) to `RegisterSpectacleProc`. Scalars use three fixed buckets defined in `SpectacleDefinitions.GetProcScalar(modId)`:

| Bucket | Value | Modifiers |
|---|---|---|
| Low | 0.75 | Hair Trigger, Split Shot, Chain Reaction, Blast Core, Wildfire, Afterimage, Deadzone |
| Medium | 1.00 | Momentum, Exploit Weakness, Chill Shot, Overreach, Feedback Loop |
| High | 1.35 | Overkill, Focus Lens, Reaper Protocol |

All mods call `GetProcScalar(ModifierId)` -- there are no bespoke per-modifier formulas.

---

## 7. Inactivity Decay

If a tower hasn't registered any proc for `InactivityGraceSeconds` (2.0s × 0.9151 ≈ 1.83s effective), its meter begins decaying:
```
meter -= InactivityDecayPerSecond × delta
       = 3.0 × 1.2156 × delta
       ≈ 3.65 pts/s
```
Meter clamps to 0; decay stops when a new proc fires or meter hits zero.

---

## 8. Mod Slot Assignment

Slots are computed fresh on every surge trigger from the tower's current modifier loadout. All equipped mods contribute equally -- there is no "primary mod" concept and no copy-count weighting.

### Slot Assignment Order
Unique supported mods on the tower are sorted by:
1. **Canonical order** -- deterministic, consistent across all contexts
2. **Alphabetical** (tiebreak)

Result: `r1` (slot 1), `r2` (slot 2), `r3` (slot 3). These slots drive effect selection and combo naming, not dominance weighting.

---

## 9. Spectacle Mode

Determined by how many *unique* supported modifier types are on the tower:

| Unique Supported Mods | Mode |
|---|---|
| 1 | `Single` |
| 2 | `Combo` |
| ≥ 3 | `Triad` |

---

## 10. SurgePower Calculation

```
ModeBase:
  Single → 1.00
  Combo  → 1.10
  Triad  → 1.20

SurgePower = ModeBase   (fixed; no copyBoost)
```

Example: Heavy Cannon with Focus Lens + Chain Reaction + Overkill:
- Mode = Triad (3 unique mods)
- SurgePower = 1.20

---

## 11. Surge Threshold and Trigger

Effective surge threshold per tower:
```
threshold = 150 × SurgeThresholdMultiplier × TowerSurgeThresholdMultiplier(towerId)
```

Per-tower threshold multipliers are listed in §20. When `meter >= threshold` AND `surgeCooldown <= 0`:
1. Resolve the `SpectacleSignature` from the current loadout
2. Reset meter to `10 × SurgeMeterAfterTriggerMultiplier` (≈10.8, i.e. partial carry-over)
3. Apply surge cooldown: `6.0s × SurgeCooldownMultiplier` (≈5.9s)
4. Set visual pulse to 1.0, hold for 0.42s (then decays at 0.72/s)
5. Add `10 × GlobalMeterPerSurgeMultiplier` (≈10.78) to the global meter
6. Increment `_globalCycleScores[modId]` for **each equipped mod** (all contribute equally to global attribution)
7. Fire `OnSurgeTriggered` event with the full `SpectacleTriggerInfo`
8. Check if global meter is now full (see §13)

---

## 12. Effect Naming

The surge banner shows the **category callout** (SPREAD SURGE / BURST SURGE / CONTROL SURGE / ECHO SURGE). Internal effect names are used for logging and spectacle payloads only -- they are not surfaced to players.

### Single Mode (1 unique mod)
Each mod has one internal Single effect definition:

| Modifier | Effect ID | Effect Name |
|---|---|---|
| momentum | S_MOMENTUM_REDLINE_BREAK | Momentum Break |
| overkill | S_OVERKILL_SPILL_CASCADE | Overkill Cascade |
| exploit_weakness | S_EXPLOIT_MARK_COLLAPSE | Exploit Collapse |
| focus_lens | S_FOCUS_PRISM_LANCE | Focus Lance |
| slow (chill) | S_CHILL_ABSOLUTE_ZERO | Absolute Zero |
| overreach | S_OVERREACH_HORIZON_SWEEP | Overreach Sweep |
| hair_trigger | S_HAIR_HYPERBURST | Hyperburst |
| split_shot | S_SPLIT_FRACTAL_BLOOM | Split Bloom |
| feedback_loop | S_FEEDBACK_REBOOT_STORM | Feedback Storm |
| chain_reaction | S_CHAIN_ARC_OVERLOAD | Arc Overload |
| blast_core | S_BLAST_DETONATION_ZONE | Detonation Zone |
| wildfire | S_WILDFIRE_FIRESTORM | Firestorm |
| reaper_protocol | S_REAPER_DEATH_DECREE | Death Decree |

### Combo Mode (2 unique mods)
Internal combo names are dynamically generated at runtime (no static lookup table):

```
name     = "[Slot1ShortName] [Slot2Tag]"
effectId = "C_{R1_UPPERCASE}_{R2_UPPERCASE}"
```

`r1` and `r2` are assigned by canonical order only (no copy-count weighting).

### Triad Mode (3+ unique mods)
Internal triad name format:

```
"[combo name] + [augment name]"
```

Triad augment names are intentionally flattened to three player-facing concepts:

| Kind | Display Name | Behavior |
|---|---|---|
| `Area` | Pulse | Hits all enemies near the tower |
| `Strike` | Strike | One heavy single-target hit |
| `Reload` | Recharge | Resets cooldown and fires again |

Per-mod augment IDs, coefficients, and durations still exist internally and are unchanged.

---

## 13. Global Meter and Global Surge

### Accumulation
Every tower surge adds `GlobalMeterPerSurge × GlobalMeterPerSurgeMultiplier` (10 × 1.0778 ≈ 10.78 pts) to the shared global meter.

Global threshold = `200 × GlobalThresholdMultiplier` (200 × 1.1049 ≈ 220.98).

So roughly **20–21 tower surges** fill the global meter under default conditions (fewer if individual surges add more due to tuning drift).

### Arming
When `globalMeter >= globalThreshold` the system does NOT fire immediately. Instead it arms a `pendingGlobalSurge` and sets `IsGlobalSurgeReady = true`, firing `OnGlobalSurgeReady(label)` -- the HUD animates the surge bar as ready for player activation.

### Activation
The player clicks the global surge bar → `ActivateGlobalSurge()`:
1. Fire `OnGlobalTriggered` with the pending `GlobalSurgeTriggerInfo`
2. Clear the pending surge and ready state
3. Set `globalMeter = 0 × GlobalMeterAfterTriggerMultiplier = 0` (full reset)

In bot mode, activation is immediate (no player input needed).

### Global Surge Payload Info
`GlobalSurgeTriggerInfo` carries:
- `MeterAfter` -- post-trigger meter value (= 0)
- `UniqueContributors` -- distinct towers that surged in the recent contribution window
- `EffectId` -- always `"G_SPECTACLE_CATHARSIS"`
- `EffectName` -- always `"Catastrophe"`
- `DominantModIds[]` -- up to 3 dominant mod IDs (drives global label + feel)

---

## 14. Dominant Mod Attribution (Global Surge Label)

The system maintains `_globalCycleScores: Dictionary<string, int>` -- a count of how many surges each mod ID contributed to since the last global surge armed. Every tower surge increments the score for **all equipped mods** by 1 each, so a tower with Overkill + Chain Reaction + Chill Shot contributes one count to each of those three mods per surge.

When the global meter fills and arms, `ResolveDominantGlobalMods()`:
1. Read `_globalCycleScores` (accumulated over the entire cycle)
2. Sort descending by score, then by canonical order for tiebreaks
3. Return top 3 mod IDs
4. Clear `_globalCycleScores` and `_globalCycleContributors` (reset for next cycle)

Only `dominantModIds[0]` (the highest-score mod across the whole cycle) determines the displayed global label and feel. All mods on all surging towers influence this -- a build that mixes many Detonation mods across multiple towers will bias toward DETONATION SURGE regardless of which mod holds slot 1.

---

## 15. Global Label Mapping and Feel

`SurgeDifferentiation.ResolveLabel(dominantModIds)` maps the dominant mod to one of three player-facing labels. Falls back to `"CHAIN SURGE"` (Neutral) if no match.

| Dominant Mod | Feel | Player Label |
|---|---|---|
| momentum | Pressure | PRESSURE SURGE |
| slow (chill) | Pressure | PRESSURE SURGE |
| overreach | Pressure | PRESSURE SURGE |
| afterimage | Pressure | PRESSURE SURGE |
| overkill | Detonation | DETONATION SURGE |
| focus_lens | Detonation | DETONATION SURGE |
| hair_trigger | Detonation | DETONATION SURGE |
| feedback_loop | Detonation | DETONATION SURGE |
| blast_core | Detonation | DETONATION SURGE |
| wildfire | Detonation | DETONATION SURGE |
| exploit_weakness | Neutral | CHAIN SURGE |
| split_shot | Neutral | CHAIN SURGE |
| chain_reaction | Neutral | CHAIN SURGE |
| reaper_protocol | Neutral | CHAIN SURGE |
| deadzone | Neutral | CHAIN SURGE |

### Visual Feel Classification
- **Detonation** -- spike/burst builds; bright flash
- **Pressure** -- sustained/range builds; dimmer flash
- **Neutral** -- everything else; mid flash

Flash alpha for the full-screen flash on global surge trigger:
```
Detonation -> 0.34
Pressure   -> 0.21
Neutral    -> 0.28
```

Feel also drives secondary pulses, ripple pattern, and per-tower identity FX sequencing.

---

## 16. Explosion System (SpectacleExplosionCore)

The explosion system provides pure-math helpers consumed by visual/gameplay nodes when a surge or global surge fires.

### Combo Explosion Skin
Assigned based on which mods are in the combo (r1 or r2):
- Chill Shot present → `ChillShatter`
- Chain Reaction present → `ChainArc`
- Split Shot present → `SplitShrapnel`
- Focus Lens present → `FocusImplosion`
- Otherwise → `Default`

All skins generate `VulnerabilityZone` residue when they spawn residue (see below). Skin only affects visuals.

### Overkill Bloom
Fires when Overkill's overflow damage (excess beyond killing blow) exceeds a threshold.

```
overflowThreshold = 6.0 × OverkillBloomThresholdMultiplier   (default: 6.0 × 1.0709 ≈ 6.43)

If overflow < threshold → no bloom.

overflowVisualT = Clamp(overflow / 320, 0, 1)     -- visual scale factor
radius = Lerp(120, 250, overflowVisualT)           -- px
       × OverkillBloomRadiusMultiplier              -- (0.9387)
       → range: ~113 to ~235 px

bloomDamage = Clamp(overflow × 0.42 × OverkillBloomDamageScaleMultiplier,
                    4,
                    86 × OverkillBloomDamageScaleMultiplier)
            = Clamp(overflow × 0.42 × 0.7532, 4, ~64.8)

maxTargets = Round(Clamp(2 + floor(overflowVisualT × 5), 2, 7))
```

Bloom damage is raw HP damage -- no modifier hooks, no chain.

### Status Detonation
On surge trigger, all Marked/Slowed/Burning enemies within range can be "detonated" to strip and explode their status:

```
maxTargets (regular surge)  = Round(8 × DetonationMaxTargetsMultiplier) capped at 8
maxTargets (global surge)   = Round(24 × DetonationMaxTargetsMultiplier) capped at 48
                              (10 in reduced-motion mode)

staggerSeconds = 0.20s × (0.55 if reducedMotion else 1.0)
               ≈ 0.11–0.20s between pops
```

### Marked Pop (ExploitWeakness / Marker Tower context)
When a Marked enemy is detonated in a status detonation event:
```
popRadius = 170px
damageAmpMultiplier = +22% damage received for 1.45s applied to nearby enemies
```

### Explosion Residue Zones
After a surge/explosion, a lingering `VulnerabilityZone` can spawn at the impact point. All skins use the same single residue kind.

| Kind | Radius | Base Duration | Tick Interval |
|---|---|---|---|
| VulnerabilityZone | 108px | 1.20s | 0.20s |

Potency:
```
potency = 1.10 if globalSurge else 1.00
```

Spawn rule: residue spawns when `EnableResidue` is true AND (`globalSurge` OR `chainIndex == 0`). Non-global chain explosions (chainIndex > 0) skip residue to avoid visual clutter.

Max simultaneous residues: 12 (7 in reduced-motion mode). Oldest is evicted when at cap.

### Explosion Hit Stop
On major explosions (surge or global surge), enemy movement briefly slows:
```
globalSurge:      duration = 0.08s,  slowScale = 0.24
non-global major: duration = 0.06s,  slowScale = 0.34
```

### Second Stage Explosion
A second blast wave fires with a 0.12s delay when `major == true`. Non-major surges do not emit a second stage.

### Afterimage Strength
```
if not majorExplosion → 0.0
if globalSurge       → 1.0
else                 → 0.5
```

### Global Surge Wave Timing
The ripple wave that sweeps out from the surge point:
```
waveSpeed    = (GlobalSurgeWaveSpeedMin + GlobalSurgeWaveSpeedMax) / 2   (fixed average, ~1080 px/s)
impactDelay  = distance / waveSpeed
preFlashDelay = Max(0, impactDelay - GlobalSurgeWavePreFlashLeadSeconds)
```
Wave speed is fixed -- it no longer scales with contributor count.

---

## 17. Visual State (Pulse)

Per-tower pulse used for glowing/pulsing UI:
- On surge: `Pulse = Max(current, 1.0)`, `PulseHold = Max(current, 0.42s)`
- Each frame while `PulseHold > 0`: hold decays (no pulse fade)
- Once hold expires: `Pulse -= 0.72 × delta` until it reaches 0

`GetVisualState(tower)` returns:
- `MeterNormalized` = `Clamp(meter / threshold, 0, 1)`
- `Pulse` = current pulse value
- `PrimaryModId` = slot 1 mod (canonical order, resolved fresh from loadout) -- used for accent color

---

## 18. SpectacleSignature Record

Returned by `PreviewSignature()` and embedded in `SpectacleTriggerInfo`. Contains everything needed to render and execute the surge:

| Field | Description |
|---|---|
| `Mode` | Single / Combo / Triad |
| `PrimaryModId` | Slot 1 mod (canonical order) |
| `SecondaryModId` | Slot 2 mod |
| `TertiaryModId` | Slot 3 mod |
| `SurgePower` | ModeBase (1.00 / 1.10 / 1.20) |
| `EffectId` | Full effect string (e.g. "C_MOMENTUM_SPLIT_SHOT+T_AUG_CHAIN_REACTION") |
| `EffectName` | Human-readable (e.g. "Momentum Split + Pulse") |
| `ComboEffectId` | Combo portion only |
| `ComboEffectName` | Combo name only |
| `AugmentEffectId` | Augment portion only (Triad) |
| `AugmentName` | Augment name only (Triad) |

Slot assignment is by canonical order only. There are no share fields -- all mods contribute equally to surge category and global feel.

---

## 19. Tower Surge Categories

Every tower surge is classified into one of four categories. The category drives distinct visual presentation, audio, and gameplay behavior. It is resolved by `SurgeDifferentiation.ResolveTowerSurgeCategory(towerId, signature)`.

### Category Resolution
All equipped mods contribute equally (flat weight, no position penalty). Mod weights from `ModCategoryTable` are summed by category; the tower's base bias from `TowerBiasTable` is added on top. Highest total wins; ties broken by enum order (Spread > Burst > Control > Echo, lower value wins).

### Category Mod Weights
| Mod | Category | Weight |
|---|---|---|
| Chain Reaction | Spread | 2.5 |
| Split Shot | Spread | 2.0 |
| Overreach | Spread | 1.8 |
| Blast Core | Spread | 1.5 |
| Overkill | Burst | 2.5 |
| Focus Lens | Burst | 2.2 |
| Exploit Weakness | Burst | 1.8 |
| Reaper Protocol | Burst | 1.5 |
| Chill Shot | Control | 2.5 |
| Deadzone | Control | 2.2 |
| Momentum | Control | 2.0 |
| Afterimage | Echo | 2.5 |
| Wildfire | Echo | 2.0 |
| Hair Trigger | Echo | 2.0 |
| Feedback Loop | Echo | 1.8 |

### Tower Base Biases
| Tower | Category | Weight |
|---|---|---|
| rapid_shooter | Echo | 1.5 |
| heavy_cannon | Burst | 2.0 |
| marker_tower | Control | 1.8 |
| chain_tower | Spread | 2.0 |
| rift_prism | Control | 1.8 |
| accordion_engine | Control | 2.0 |
| phase_splitter | Spread | 1.5 |
| latch_nest | Echo | 1.5 |
| rocket_launcher | Burst | 1.8 |
| undertow_engine | Control | 2.0 |

### Category Effects
| Category | Callout | Visual | Audio | Gameplay |
|---|---|---|---|---|
| Spread | SPREAD SURGE | Arcs radiate from tower to 5 enemies (520 px) | Staggered arc sweeps + electric hiss | +40% link range, up to 5 links |
| Burst | BURST SURGE | Second explosion fires 0.30 s after primary | Sub-bass boom + impact crack | 1.55x burst power, deeper slowmo |
| Control | CONTROL SURGE | Tight signature rings at high drama (0.46) | Cold resonant hum, soft onset | 2 links max, 0.22 s slowmo |
| Echo | ECHO SURGE | Delayed repeat FX fires at 0.48 s | Three hits decaying 100%/50%/25% | Late echo burst at 72% power |

---

## 20. Tuning System (SpectacleTuningProfile)

`SpectacleTuning.Current` is a singleton `SpectacleTuningProfile` that wraps all the base constants with runtime multipliers. Every constant in the system is expressed as `baseValue × tuningMultiplier`, meaning you can adjust the entire spectacle system's pacing without changing code.

The profile is loaded at startup from `res://Data/best_tuning_full.json` (or `best_tuning_demo.json` in demo mode). If the file doesn't exist, the hardcoded defaults in `SpectacleTuningProfile` are used. The `--tuning_file` CLI arg overrides this for pipeline runs.

**Spectacle / surge knobs:**

| Multiplier | Default | Controls |
|---|---|---|
| MeterGainMultiplier | 1.0 | Global gain scale |
| SurgeThresholdMultiplier | 1.0561 | How full the meter must be |
| SurgeCooldownMultiplier | 0.9831 | Time between surges per tower |
| SurgeMeterAfterTriggerMultiplier | 1.0825 | Carry-over meter after surge |
| GlobalMeterPerSurgeMultiplier | 1.0778 | Global meter added per surge |
| GlobalThresholdMultiplier | 1.1049 | Global meter capacity |
| GlobalMeterAfterTriggerMultiplier | 0.9987 | Global meter carry-over (≈0) |
| InactivityGraceMultiplier | 0.9151 | How long before decay starts |
| InactivityDecayMultiplier | 1.2156 | How fast meter decays |
| CopyMultiplierScale | 0.8532 | How much stacking same mod pays off |

**Explosion knobs:**

| Multiplier | Default | Controls |
|---|---|---|
| EnableOverkillBloom | true | Toggle Overkill bloom entirely |
| EnableStatusDetonation | true | Toggle status detonation entirely |
| EnableResidue | true | Toggle residue zone spawning entirely |
| OverkillBloomDamageScaleMultiplier | 0.7532 | Bloom damage output |
| OverkillBloomRadiusMultiplier | 0.9387 | Bloom visual radius |
| DetonationMaxTargetsMultiplier | 1.0799 | Detonation target cap scale |
| StatusDetonationDamageMultiplier | 1.2506 | Status pop damage |
| ResidueDamageMultiplier | 0.9283 | Residue zone tick damage |

### Per-Mod and Per-Tower Overrides
The profile carries three dictionaries:
- `GainMultipliers[modId]` -- multiplies BaseGain for specific mods (default entries: overkill ×0.879, split_shot ×0.872, chain_reaction ×0.94, blast_core ×0.88)
- `TowerMeterGainMultipliers[towerId]` -- per-tower gain scale (see §5 table)
- `TowerSurgeThresholdMultipliers[towerId]` -- per-tower threshold scale

Hardcoded surge threshold multiplier defaults:
| Tower | Threshold Multiplier |
|---|---|
| rapid_shooter | 1.50× |
| heavy_cannon | 0.50× |
| rocket_launcher | 0.82× |
| marker_tower | 1.00× |
| chain_tower | 0.90× |
| rift_prism | 0.90× |
| accordion_engine | 0.55× |
| phase_splitter | 1.00× |
| undertow_engine | 0.70× |
| latch_nest | 1.26× |

### Automated Tuning Pipeline
`run_tuning_pipeline.ps1` runs iterative bot playtest sweeps and gradient-searches tuning parameters to hit target win rates (~75% Normal, ~50% Hard). The pipeline outputs `Data/best_tuning_full.json` / `Data/best_tuning_demo.json`. Those files ship with the game.

---

## 21. Code Map

| File | Role |
|---|---|
| `SpectacleSystem.cs` | Per-tower meter, mod proc cooldown gate, gain formula, surge trigger, global meter, global surge arming |
| `SpectacleDefinitions.cs` | Constants, base gains, 3-bucket proc scalars (`GetProcScalar`), copy multiplier, mode base, effect name tables, resolve helpers |
| `SpectacleTuning.cs` | `SpectacleTuningProfile` (all multipliers + dicts), `SpectacleTuning.Current` singleton |
| `SurgeDifferentiation.cs` | Tower surge category resolution (Spread/Burst/Control/Echo), mod/tower weight tables, category helpers; global surge label + feel table, `ResolveLabel()`, `ResolveFeel()`, flash alpha |
| `SpectacleExplosionCore.cs` | Pure-math explosion profiles: OverkillBloom, residue, hit stop, afterimage, wave timing |
| `SpectacleDamageCore.cs` | `ApplyRawDamage(enemy, damage)` -- raw HP mutation used by spectacle payloads |
| `SpectacleDamageSource.cs` | Enum identifying which system dealt damage (for VFX/logging) |

---

## 22. Quick Reference -- Default Base Values

| Constant | Value |
|---|---|
| SurgeThreshold (base) | 150 |
| SurgeCooldownSeconds | 6.0s |
| SurgeMeterAfterTrigger | 10 |
| GlobalMeterPerSurge | 10 |
| GlobalThreshold | 200 |
| GlobalMeterAfterTrigger | 0 |
| InactivityGraceSeconds | 2s |
| InactivityDecayPerSecond | 3 pts/s |
| MeterGainScale | 0.75 / 1.30 ≈ 0.577 |
| ModProcCooldownSeconds | 0.25s |
| ProcScalarLow / Medium / High | 0.75 / 1.00 / 1.35 |
| OverkillBloomOverflowThreshold | 6 |
| OverkillBloomDamageScale | 0.42 |
| OverkillBloomDamageCap | 86 |
| OverkillBloomRadiusMin / Max | 120 / 250 px |
| MarkedPopRadius | 170 px |
| MarkedPopDamageAmpMultiplier | +22% for 1.45s |
| SurgeStatusDetonationStaggerSeconds | 0.20s |
| ResidueVulnerabilityRadius | 108 px |
| ResidueVulnerabilityDurationSeconds | 1.20s |
| ResidueTickIntervalSeconds | 0.20s |
| HitStop (non-global major) | 0.06s duration, 0.34 slowScale |
| HitStop (global surge) | 0.08s duration, 0.24 slowScale |
| TwoStageBlastDelaySeconds | 0.12s |
| GlobalSurgeWaveSpeedMin / Max | 880 / 1280 px/s |
| Pulse decay rate | 0.72/s |
| Pulse hold duration | 0.42s |

