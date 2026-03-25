# Surge & Spectacle System -- Complete Reference

Slot Theory's spectacle system is a two-tier excitement engine layered on top of the core damage pipeline. Every proc from a supported modifier feeds a per-tower **Spectacle Meter**. When that meter fills, the tower fires a **Surge** -- a named visual + gameplay event whose flavor is determined by the tower's modifier build. Enough tower surges fill a shared **Global Meter**, which the player can then activate as a **Global Surge** -- a map-wide catharsis event.

---

## PLAYER-FACING TRUTH (IN GAME)

- Every tower has a Surge Meter that fills as it deals damage. When full, it triggers a Surge.
- **1 mod = core surge** from that primary mod.
- **2 mods = core surge + one mutation** from the secondary mod (its normal trait, now riding the surge).
- **3 mods = core surge + one bonus** from the third mod: **Pulse** (area), **Strike** (heavy hit), or **Recharge** (instant refire).
- Each tower surge fills the shared Global Surge bar.
- The Global Surge label shows the dominant surge type your build has leaned toward **since the last Global Surge**.

UI teaching is intentionally minimal: `Surge`, `Twist`, `Bonus`, and `Global Surge`.

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
| `reaper_protocol` | Reaper Protocol |

Supported tower IDs: `rapid_shooter`, `heavy_cannon`, `marker_tower`, `chain_tower`, `rift_prism`, `phase_splitter`.

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
| reaper_protocol | 3.2 |

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

### Per-Tower Meter Gain Multipliers (tuned defaults)
| Tower | Multiplier |
|---|---|
| rapid_shooter | 0.7929× |
| heavy_cannon | 1.0601× |
| marker_tower | 1.0× |
| chain_tower | 0.96× |
| rift_prism | 1.091× |
| phase_splitter | 1.0× |

---

## 6. Event Scalars Per Modifier

Each modifier passes an `eventScalar` (a float) to `RegisterSpectacleProc`. Scalars use three fixed buckets defined in `SpectacleDefinitions.GetProcScalar(modId)`:

| Bucket | Value | Modifiers |
|---|---|---|
| Low | 0.75 | Hair Trigger, Split Shot, Chain Reaction, Blast Core, Wildfire |
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

## 8. Role Resolution

Roles are computed fresh on every surge trigger from the tower's current modifier loadout. There is no contribution window, no role lock, and no separate tracking per mod.

### Role Resolution Order
Supported mods on the tower are sorted by:
1. **Copy count** (descending) -- more copies = higher priority
2. **Canonical order** (tiebreak) -- deterministic fallback
3. **Alphabetical** (final tiebreak)

Result: `r1` (Primary), `r2` (Secondary), `r3` (Tertiary).

### Weight Shares
Used for SurgePower calculation:
```
w1 = copies(r1) / totalModCount
w2 = copies(r2) / totalModCount
w3 = copies(r3) / totalModCount
```

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

`AugmentStrength` (Triad only): fixed `1.0` -- copies affect meter fill frequency via `CopyMultiplier`, not surge power.

Example: Heavy Cannon with 2× Focus Lens + 1× Chain Reaction + 1× Overkill:
- Mode = Triad
- SurgePower = 1.20
- AugmentStrength = 1.0

---

## 11. Surge Threshold and Trigger

Effective surge threshold per tower:
```
threshold = 150 × SurgeThresholdMultiplier × TowerSurgeThresholdMultiplier(towerId)
```

Tuned per-tower surge threshold multipliers:
| Tower | Threshold Multiplier |
|---|---|
| rapid_shooter | 1.0759× |
| heavy_cannon | 1.0× |
| marker_tower | 0.9755× |
| chain_tower | 1.18× |
| rift_prism | 1.0673× |
| phase_splitter | 1.0× |

When `meter >= threshold` AND `surgeCooldown <= 0`:
1. Resolve the `SpectacleSignature` from the current loadout
2. Reset meter to `10 × SurgeMeterAfterTriggerMultiplier` (≈10.8, i.e. partial carry-over)
3. Apply surge cooldown: `6.0s × SurgeCooldownMultiplier` (≈5.9s)
4. Set visual pulse to 1.0, hold for 0.42s (then decays at 0.72/s)
5. Add `10 × GlobalMeterPerSurgeMultiplier` (≈10.78) to the global meter
6. Increment `_globalCycleScores[PrimaryModId]` (cycle-accumulated score for global attribution)
7. Fire `OnSurgeTriggered` event with the full `SpectacleTriggerInfo`
8. Check if global meter is now full (see §13)

---

## 12. Effect Naming

### Player-facing naming (UI)
Combat UI surfaces (tooltips + callouts) use this structure:

- `Surge: [Primary Display Name]`
- `Twist: [Secondary Display Name]` + short trait hint (when 2+ unique mods)
- `Bonus: [Pulse|Strike|Recharge]` + short behavior hint (when 3+ unique mods)

Players are not required to learn internal effect IDs or mode taxonomy to read surges.

### Internal naming (runtime/signature data)

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
name     = "[PrimaryShortName] [SecondaryTag]"
effectId = "C_{R1_UPPERCASE}_{R2_UPPERCASE}"
```

`r1` is the primary mod (highest copy count, then canonical rank). `r2` is the secondary mod.

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

The system maintains `_globalCycleScores: Dictionary<string, int>` -- a count of how many tower surges each `PrimaryModId` has led since the last global surge armed. Every tower surge increments the score for its primary mod by 1.

When the global meter fills and arms, `ResolveDominantGlobalMods()`:
1. Read `_globalCycleScores` (already accumulated over the entire cycle)
2. Sort descending by score, then by canonical order for tiebreaks
3. Return top 3 mod IDs
4. Clear `_globalCycleScores` and `_globalCycleContributors` (reset for next cycle)

Only `dominantModIds[0]` (the highest-score primary mod across the whole cycle) determines the displayed global label and feel.

This replaces the previous 6-second time-windowed `Queue<SurgeContribution>` approach. Cycle-accumulated scoring is O(1) per surge and deterministic over the full cycle.

---

## 15. Global Label Mapping and Feel

`SurgeDifferentiation.ResolveLabel(dominantModIds)` maps the dominant mod to one normalized player-facing label. Falls back to `"GLOBAL SURGE"` if no match.

| Dominant Mod | Global Label | Feel |
|---|---|---|
| momentum | Momentum | Pressure |
| overkill | Overkill | Detonation |
| exploit_weakness | Exploit | Neutral |
| focus_lens | Focus | Detonation |
| slow (chill) | Chill | Pressure |
| overreach | Overreach | Pressure |
| hair_trigger | Hair Trigger | Detonation |
| split_shot | Split Shot | Neutral |
| feedback_loop | Feedback | Detonation |
| chain_reaction | Chain | Neutral |
| blast_core | Blast | Detonation |
| wildfire | Wildfire | Detonation |
| reaper_protocol | Reaper | Neutral |

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
- `PrimaryModId` = current primary mod (resolved fresh from loadout)

---

## 18. SpectacleSignature Record

Returned by `PreviewSignature()` and embedded in `SpectacleTriggerInfo`. Contains everything needed to render and execute the surge:

| Field | Description |
|---|---|
| `Mode` | Single / Combo / Triad |
| `PrimaryModId` | r1 locked mod |
| `SecondaryModId` | r2 |
| `TertiaryModId` | r3 |
| `PrimaryShare` | copies(r1) / totalMods |
| `SecondaryShare` | copies(r2) / totalMods |
| `TertiaryShare` | copies(r3) / totalMods |
| `SurgePower` | ModeBase (1.00 / 1.10 / 1.20) |
| `AugmentStrength` | Fixed 1.0 (Triad only) |
| `EffectId` | Full effect string (e.g. "C_MOMENTUM_SPLIT_SHOT+T_AUG_CHAIN_REACTION") |
| `EffectName` | Human-readable (e.g. "Momentum Split + Pulse") |
| `ComboEffectId` | Combo portion only |
| `ComboEffectName` | Combo name only |
| `AugmentEffectId` | Augment portion only (Triad) |
| `AugmentName` | Augment name only (Triad) |

---

## 19. Tuning System (SpectacleTuningProfile)

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
- `TowerMeterGainMultipliers[towerId]` -- per-tower gain scale
- `TowerSurgeThresholdMultipliers[towerId]` -- per-tower threshold scale

### Automated Tuning Pipeline
`run_tuning_pipeline.ps1` runs iterative bot playtest sweeps and gradient-searches tuning parameters to hit target win rates (~75% Normal, ~50% Hard). The pipeline outputs `Data/best_tuning_full.json` / `Data/best_tuning_demo.json`. Those files ship with the game.

---

## 20. Code Map

| File | Role |
|---|---|
| `SpectacleSystem.cs` | Per-tower meter, mod proc cooldown gate, gain formula, surge trigger, global meter, global surge arming |
| `SpectacleDefinitions.cs` | Constants, base gains, 3-bucket proc scalars (`GetProcScalar`), copy multiplier, mode base, effect name tables, resolve helpers |
| `SpectacleTuning.cs` | `SpectacleTuningProfile` (all multipliers + dicts), `SpectacleTuning.Current` singleton |
| `SurgeDifferentiation.cs` | Global surge label table (label + feel), `ResolveLabel()`, `ResolveFeel()`, flash alpha |
| `SpectacleExplosionCore.cs` | Pure-math explosion profiles: OverkillBloom, residue, hit stop, afterimage, wave timing |
| `SpectacleDamageCore.cs` | `ApplyRawDamage(enemy, damage)` -- raw HP mutation used by spectacle payloads |
| `SpectacleDamageSource.cs` | Enum identifying which system dealt damage (for VFX/logging) |

---

## 21. Quick Reference -- Default Base Values

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

