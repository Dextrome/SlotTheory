# Surge & Spectacle System -- Complete Reference

Slot Theory's spectacle system is a two-tier excitement engine layered on top of the core damage pipeline. Every proc from a supported modifier feeds a per-tower **Spectacle Meter**. When that meter fills, the tower fires a **Surge** -- a named visual + gameplay event whose flavor is determined by the tower's modifier build. Enough tower surges fill a shared **Global Meter**, which the player can then activate as a **Global Surge** -- a map-wide catharsis event.

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
- `ShotTimes` -- queue of shot timestamps for HairTrigger streak measurement
- `LoadoutSignature` -- hash of current modifier loadout; if this changes, the entire state resets

State resets automatically when the tower's modifier loadout changes (new mod drafted onto the tower).

---

## 3. Registering a Proc

Two entry points feed the system:

### `RegisterProc(tower, modifierId, eventScalar, eventDamage)`
Called by modifier code after the mod does something meaningful (kill, hit, bounce, etc.). Validates inputs, normalizes the mod ID, checks it is supported, then calls `RegisterProcInternal`.

### `RegisterShotFired(tower)`
Called every time a tower fires. Only acts if the tower has at least one `hair_trigger` modifier. Computes `streakNorm` from a 1-second rolling shot-count window and calls `RegisterProcInternal` with `HairTrigger` and the streak-based event scalar.

```
streakNorm = recentShots(last 1s) / expectedShotsPerSecond
           = recentShots / (1 / attackInterval)
           = recentShots * attackInterval
Capped at 2.0
```

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

Each modifier computes an `eventScalar` that reflects how impactful the specific proc was. All formulas are multiplied by `ResolveEventScalarMultiplier(mod)` which is `EventScalarMultiplier × perModEventScalarMultiplier`.

| Modifier | Formula | Min | Max |
|---|---|---|---|
| Momentum | `0.75 + 0.50 × stackNorm` | 0.75 | 1.25 |
| Overkill | `Clamp(0.65 + 0.55 × spillRatio, 0.65, 1.80)` | 0.65 | 1.80 |
| Exploit Weakness | `0.70 + 0.65 × markedHit + 0.45 × markedKill` | 0.70 | 1.80 |
| Focus Lens | `Clamp(0.85 + 0.45 × Clamp(damageNorm, 0, 2), 0.85, 1.75)` | 0.85 | 1.75 |
| Chill Shot | `Clamp(0.80 + 0.20 × affectedEnemies, 0.80, 1.80)` | 0.80 | 1.80 |
| Overreach | `0.85 + 0.40 × Clamp(rangeNorm, 0, 1.5)` | 0.85 | 1.45 |
| Hair Trigger | `0.70 + 0.30 × Clamp(streakNorm, 0, 2)` | 0.70 | 1.30 |
| Split Shot | `Clamp(0.70 + 0.15 × extraHits, 0.70, 1.75)` | 0.70 | 1.75 |
| Feedback Loop | `Clamp(0.90 + 1.20 × refundFrac, 0.90, 2.00)` | 0.90 | 2.00 |
| Chain Reaction | `Clamp(0.75 + 0.18 × bounces, 0.75, 1.85)` | 0.75 | 1.85 |
| Blast Core | `Clamp(0.70 + 0.22 × splashHits, 0.70, 1.60)` | 0.70 | 1.60 |
| Wildfire | `Clamp(0.75 + 0.35 × Clamp(burnDpsScalar, 0, 2), 0.75, 1.70)` | 0.75 | 1.70 |
| Reaper Protocol | `Clamp(0.80 + 0.25 × livesRestoredThisWave, 0.80, 1.80)` | 0.80 | 1.80 |

**Notes on inputs:**
- `stackNorm`: Momentum's current stack divided by max (normalized 0–1)
- `spillRatio`: Overkill excess damage divided by target's max HP
- `markedHit`/`markedKill`: booleans -- max scalar requires both true simultaneously (hit a Marked enemy AND kill it)
- `damageNorm`: final damage divided by some reference (context-specific)
- `affectedEnemies`: number of enemies slowed on this hit
- `rangeNorm`: how "far out" the hit was relative to base range
- `streakNorm`: computed in `RegisterShotFired`, NOT in `RegisterProc` -- Hair Trigger fires automatically
- `extraHits`: number of extra targets Split Shot hit
- `refundFrac`: fraction of cooldown refunded (1.0 = full instant refund)
- `bounces`: number of chain bounces achieved
- `splashHits`: number of enemies caught by the Blast Core AoE
- `burnDpsScalar`: `burnDps × 0.5`, passed from Wildfire.OnHit and CombatSim burn/trail procs; higher effective burn = richer payout
- `livesRestoredThisWave`: cumulative Reaper Protocol life gains on this tower this wave

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
  Combo  → 1.08
  Triad  → 1.16

copyBoost = 1.0
          + 0.28 × (copies(r1) - 1)   -- primary stacking bonus
          + 0.16 × (copies(r2) - 1)   -- secondary stacking bonus
          + 0.10 × (copies(r3) - 1)   -- tertiary stacking bonus

SurgePower = ModeBase × copyBoost
```

`AugmentStrength` (Triad only): `w3 × SurgePower` -- how strongly the third modifier's augment flavors the explosion.

Example: Heavy Cannon with 2× Focus Lens + 1× Chain Reaction + 1× Overkill:
- Mode = Triad
- r1 = focus_lens (2 copies), r2 = chain_reaction (1), r3 = overkill (1)
- copyBoost = 1 + 0.28×(2-1) + 0 + 0 = 1.28
- SurgePower = 1.16 × 1.28 = 1.485

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
1. Resolve the `SpectacleSignature` from locked roles
2. Reset meter to `10 × SurgeMeterAfterTriggerMultiplier` (≈10.8, i.e. partial carry-over)
3. Apply surge cooldown: `6.0s × SurgeCooldownMultiplier` (≈5.9s)
4. Set visual pulse to 1.0, hold for 0.42s (then decays at 0.72/s)
5. Unlock roles (cleared for fresh accumulation)
6. Add `10 × GlobalMeterPerSurgeMultiplier` (≈10.78) to the global meter
7. Increment `_globalCycleScores[PrimaryModId]` (cycle-accumulated score for global attribution)
8. Fire `OnSurgeTriggered` event with the full `SpectacleTriggerInfo`
9. Check if global meter is now full (see §13)

---

## 12. Effect Naming

### Single Mode (1 unique mod)
Each mod has one named Single effect:

| Modifier | Effect ID | Effect Name |
|---|---|---|
| momentum | S_MOMENTUM_REDLINE_BREAK | Redline Break |
| overkill | S_OVERKILL_SPILL_CASCADE | Spill Cascade |
| exploit_weakness | S_EXPLOIT_MARK_COLLAPSE | Mark Collapse |
| focus_lens | S_FOCUS_PRISM_LANCE | Prism Lance |
| slow (chill) | S_CHILL_ABSOLUTE_ZERO | Absolute Zero |
| overreach | S_OVERREACH_HORIZON_SWEEP | Horizon Sweep |
| hair_trigger | S_HAIR_HYPERBURST | Hyperburst |
| split_shot | S_SPLIT_FRACTAL_BLOOM | Fractal Bloom |
| feedback_loop | S_FEEDBACK_REBOOT_STORM | Reboot Storm |
| chain_reaction | S_CHAIN_GRID_OVERLOAD | Grid Overload |
| blast_core | S_BLAST_DETONATION_ZONE | Detonation Zone |
| wildfire | S_WILDFIRE_CONFLAGRATION | Conflagration |
| reaper_protocol | S_REAPER_DEATH_DECREE | Death Decree |

### Combo Mode (2 unique mods)
Named pairings (order-independent). Selected pairs (full list in `SpectacleDefinitions.BuildComboDefs()`):

| Pair | Name |
|---|---|
| Momentum + Overkill | Redline Spillstorm |
| Momentum + Exploit Weakness | Predatory Ramp |
| Momentum + Focus Lens | Siege Tempo |
| Momentum + Chill Shot | Glacial Grind |
| Momentum + Overreach | Longline Pressure |
| Momentum + Hair Trigger | Ramped Overclock |
| Momentum + Split Shot | Escalating Barrage |
| Momentum + Feedback Loop | Adrenal Reboot |
| Momentum + Chain Reaction | Acceleration Lattice |
| Overkill + Exploit Weakness | Execution Spill |
| Overkill + Focus Lens | Prism Overflow |
| Overkill + Chill Shot | Shatter Spill |
| Overkill + Overreach | Linebreaker Wake |
| Overkill + Hair Trigger | Shrapnel Cycler |
| Overkill + Split Shot | Splinter Flood |
| Overkill + Feedback Loop | Overflow Reboot |
| Overkill + Chain Reaction | Spill Conduit |
| Exploit Weakness + Focus Lens | Marked Lance |
| Exploit Weakness + Chill Shot | Cryo Execute |
| Exploit Weakness + Overreach | Huntline |
| Exploit Weakness + Hair Trigger | Predator Haste |
| Exploit Weakness + Split Shot | Marked Shrapnel |
| Exploit Weakness + Feedback Loop | Execution Loop |
| Exploit Weakness + Chain Reaction | Reticle Web |
| Focus Lens + Chill Shot | Polar Beam |
| Focus Lens + Overreach | Rail Horizon |
| Focus Lens + Hair Trigger | Pulse Gatling |
| Focus Lens + Split Shot | Prism Bloom |
| Focus Lens + Feedback Loop | Lens Reboot |
| Focus Lens + Chain Reaction | Arc Lance |
| Chill Shot + Overreach | Frostline Sweep |
| Chill Shot + Hair Trigger | Rime Turbine |
| Chill Shot + Split Shot | Shatter Bloom |
| Chill Shot + Feedback Loop | Cryo Recursion |
| Chill Shot + Chain Reaction | Cryo Lattice |
| Overreach + Hair Trigger | Suppressive Reach |
| Overreach + Split Shot | Wide Bloom |
| Overreach + Feedback Loop | Echo Range Loop |
| Overreach + Chain Reaction | Grid Sweep |
| Hair Trigger + Split Shot | Needle Storm |
| Hair Trigger + Feedback Loop | Overclock Loop |
| Hair Trigger + Chain Reaction | Livewire Barrage |
| Split Shot + Feedback Loop | Recursive Bloom |
| Split Shot + Chain Reaction | Fractal Overload |
| Feedback Loop + Chain Reaction | Reactor Grid |
| Blast Core + Momentum | Impact Escalation |
| Blast Core + Overkill | Explosive Overflow |
| Blast Core + Exploit Weakness | Mark Detonation |
| Blast Core + Focus Lens | Siege Detonation |
| Blast Core + Chill Shot | Cryo Blast |
| Blast Core + Overreach | Wide Detonation |
| Blast Core + Hair Trigger | Rapid Detonation |
| Blast Core + Split Shot | Shrapnel Burst |
| Blast Core + Feedback Loop | Detonation Loop |
| Blast Core + Chain Reaction | Shockwave Propagation |
| Reaper Protocol + Momentum | Death Tempo |
| Reaper Protocol + Overkill | Grim Overflow |
| Reaper Protocol + Exploit Weakness | Marked for Death |
| Reaper Protocol + Focus Lens | Lethal Prism |
| Reaper Protocol + Chill Shot | Cryo Harvest |
| Reaper Protocol + Overreach | Death Horizon |
| Reaper Protocol + Hair Trigger | Kill Surge |
| Reaper Protocol + Split Shot | Harvest Bloom |
| Reaper Protocol + Feedback Loop | Death Loop |
| Reaper Protocol + Chain Reaction | Grim Lattice |
| Reaper Protocol + Blast Core | Lethal Detonation |
| Wildfire + Momentum | Scorched Escalation |
| Wildfire + Overkill | Inferno Spill |
| Wildfire + Exploit Weakness | Marked Combustion |
| Wildfire + Focus Lens | Prism Ignition |
| Wildfire + Chill Shot | Boiling Point |
| Wildfire + Overreach | Wildfire Sweep |
| Wildfire + Hair Trigger | Firestorm Cycler |
| Wildfire + Split Shot | Ember Bloom |
| Wildfire + Feedback Loop | Phoenix Loop |
| Wildfire + Chain Reaction | Inferno Cascade |
| Wildfire + Blast Core | Blast Furnace |
| Wildfire + Reaper Protocol | Death Pyre |

Unmatched pairs fall back to `"ModA x ModB"`.

### Triad Mode (3+ unique mods)
The surge name is `"[combo name] + [augment name]"` where:
- Combo = r1 + r2 pairing (same as Combo mode)
- Augment = the r3 mod's triad augment definition

**Triad Augments** (r3 modifier contributes one of these):

| Modifier (r3) | Augment Name | Kind | Coefficient | Duration |
|---|---|---|---|---|
| Momentum | Momentum Surge | RampCap | 0.18 | 2.0s |
| Overkill | Spill Multiplier | SpillTransfer | 0.22 | 1.8s |
| Exploit Weakness | Marked Execute | MarkedVulnerability | 0.20 | 2.2s |
| Focus Lens | Beam Spike | BeamBurst | 0.25 | instant |
| Chill Shot | Cryo Saturation | SlowIntensity | 0.20 | 2.0s |
| Overreach | Range Pulse | RangePulse | 0.22 | 2.0s |
| Hair Trigger | Overclock Injection | AttackSpeed | 0.24 | 1.6s |
| Split Shot | Split Volley | SplitVolley | 0.30 | instant |
| Feedback Loop | Cooldown Reclaim | CooldownRefund | 0.35 | instant |
| Chain Reaction | Chain Charge | ChainBounces | 0.28 | 1.8s |
| Blast Core | Blast Radius | RangePulse | 0.22 | 1.8s |
| Wildfire | Flame Surge | BurnAmplify | 0.24 | 2.5s |
| Reaper Protocol | Death Mark | ExecutionStrike | 0.28 | instant |

The augment's `Coefficient × AugmentStrength` determines intensity. `AugmentStrength = w3 × SurgePower`.

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
- `DominantModIds[]` -- up to 3 dominant mod IDs (drives visual archetype)

---

## 14. Dominant Mod Attribution (Global Surge)

The system maintains `_globalCycleScores: Dictionary<string, int>` -- a count of how many tower surges each `PrimaryModId` has led since the last global surge armed. Every tower surge increments the score for its primary mod by 1.

When the global meter fills and arms, `ResolveDominantGlobalMods()`:
1. Read `_globalCycleScores` (already accumulated over the entire cycle)
2. Sort descending by score, then by canonical order for tiebreaks
3. Return top 3 mod IDs
4. Clear `_globalCycleScores` and `_globalCycleContributors` (reset for next cycle)

Only `dominantModIds[0]` (the highest-score primary mod across the whole cycle) determines the archetype label and feel.

This replaces the previous 6-second time-windowed `Queue<SurgeContribution>` approach. Cycle-accumulated scoring is O(1) per surge and produces deterministic outcomes based on the full build pattern since the last global surge.

---

## 15. Surge Differentiation -- Labels and Feel

`SurgeDifferentiation.ResolveLabel(dominantModIds)` maps the dominant mod to one of 13 named archetypes. The banner on screen displays this label. Falls back to `"GLOBAL SURGE"` if no match.

| Dominant Mod | Global Surge Label | Feel |
|---|---|---|
| momentum | REDLINE WAVE | Pressure |
| overkill | OVERKILL STORM | Detonation |
| exploit_weakness | EXECUTION STORM | Neutral |
| focus_lens | PRISM BARRAGE | Detonation |
| slow (chill) | CRYO STORM | Pressure |
| overreach | HORIZON BREAK | Pressure |
| hair_trigger | HYPERBURST WAVE | Detonation |
| split_shot | FRACTAL STORM | Neutral |
| feedback_loop | REBOOT CASCADE | Detonation |
| chain_reaction | CHAIN STORM | Neutral |
| blast_core | BLAST WAVE | Detonation |
| wildfire | INFERNO SURGE | Detonation |
| reaper_protocol | GRIM TIDE | Neutral |

### Visual Feel Classification
- **Detonation** -- spike/burst builds; bright flash
- **Pressure** -- sustained/range builds; dimmer flash
- **Neutral** -- everything else; mid flash

Flash alpha for the full-screen flash on global surge trigger:
```
Detonation → 0.34
Pressure   → 0.21
Neutral    → 0.28
```

Feel also drives secondary pulses, ripple pattern, and per-tower archetype FX in sequence.

---

## 16. Explosion System (SpectacleExplosionCore)

The explosion system provides pure-math helpers consumed by visual/gameplay nodes when a surge or global surge fires.

### Combo Explosion Skin
Assigned based on which mods are in the combo (r1 or r2):
- Chill Shot present → `ChillShatter` (generates FrostSlow residue)
- Chain Reaction present → `ChainArc` (generates VulnerabilityZone residue)
- Split Shot present → `SplitShrapnel` (generates BurnPatch residue)
- Focus Lens present → `FocusImplosion` (generates VulnerabilityZone residue)
- Otherwise → `Default` (no residue unless global surge)

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

maxTargets = Round(Clamp(2 + floor(overflowVisualT × 5), 2, 7) × OverkillBloomMaxTargetsMultiplier)
           = 2–7 scaled by ≈1.025

bloomPower = Clamp(0.92 + overflowVisualT × 0.90, 0.92, 1.95)

StageTwoKick = overflowVisualT >= 0.40
```

Bloom damage is raw HP damage -- no modifier hooks, no chain.

### Status Detonation
On surge trigger, all Marked/Slowed/Burning enemies within range can be "detonated" to strip and explode their status:

```
maxTargets (regular surge)  = Round(8 × DetonationMaxTargetsMultiplier) capped at 8
maxTargets (global surge)   = Round(24 × DetonationMaxTargetsMultiplier) capped at 48
                              (10 in reduced-motion mode)

staggerSeconds = 0.20s × DetonationStaggerMultiplier × (0.55 if reducedMotion else 1.0)
               ≈ 0.181s between pops
```

### Marked Pop (ExploitWeakness / Marker Tower context)
When a Marked enemy is detonated in a status detonation event:
```
popRadius = 170px
damageAmpMultiplier = +22% damage received for 1.45s applied to nearby enemies
```

### Explosion Residue Zones
After a surge/explosion, a lingering zone can spawn at the impact point:

| Kind | Radius | Base Duration | Tick Interval |
|---|---|---|---|
| FrostSlow | 88px | 0.80s | 0.20s |
| VulnerabilityZone | 108px | 1.20s | 0.20s |
| BurnPatch | 96px | 0.90s | 0.20s |

Duration is multiplied by `ResidueDurationMultiplier` (1.2161). Potency:
```
potency = Clamp(
    (0.72 + 0.20 × Clamp(surgePower, 0.6, 2.2)) × (1.10 if globalSurge else 1.0),
    0.65, 1.35)
          × ResiduePotencyMultiplier (1.1563)
          clamped to [0.20, 2.40]
```

Residue zones do NOT spawn on every explosion -- they use a stride (spawn every N-th):
- FrostSlow: every 2nd explosion in chain
- BurnPatch: every 2nd
- VulnerabilityZone (regular surge): every 3rd
- VulnerabilityZone (global surge): every 2nd

### Explosion Hit Stop
On major explosions (surge or global surge), enemy movement briefly slows:
```
t = Clamp((surgePower - 0.85) / 1.35, 0, 1)
duration = Lerp(
    globalSurge ? 0.056s : 0.04s,
    0.08s,
    t)
slowScale = Lerp(
    globalSurge ? 0.30 : 0.38,
    globalSurge ? 0.22 : 0.30,
    t)
           clamped to [0.20, 0.55]
```

### Second Stage Explosion
Major surges can emit a second blast wave with a 0.12s delay:
```
if major surge  → always fires
if not major    → fires when surgePower >= SecondStagePowerThreshold (0.95)
```

### Afterimage Strength
```
t = Clamp((surgePower - 1.55) / 0.75, 0, 1)
if globalSurge → t = Max(0.62, t)
afterimage = Clamp(0.40 + 0.60 × t, 0, 1)   (0 if not major)
```

### Global Surge Wave Timing
The ripple wave that sweeps out from the surge point:
```
contributorT = Clamp((uniqueContributors - 1) / 5, 0, 1)
waveSpeed    = Lerp(880, 1280, contributorT) px/s
impactDelay  = distance / Max(220, waveSpeed)
preFlashDelay = impactDelay - 0.05s
```
More unique contributors → faster wave (more towers recently surged = faster global catharsis).

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
| `SurgePower` | ModeBase × copyBoost |
| `AugmentStrength` | w3 × SurgePower (Triad only) |
| `EffectId` | Full effect string (e.g. "C_MOMENTUM_OVERKILL+T_AUG_CHAIN") |
| `EffectName` | Human-readable (e.g. "Redline Spillstorm + Chain Charge") |
| `ComboEffectId` | Combo portion only |
| `ComboEffectName` | Combo name only |
| `AugmentEffectId` | Augment portion only (Triad) |
| `AugmentName` | Augment name only (Triad) |

---

## 19. Tuning System (SpectacleTuningProfile)

`SpectacleTuning.Current` is a singleton `SpectacleTuningProfile` that wraps all the base constants with runtime multipliers. Every constant in the system is expressed as `baseValue × tuningMultiplier`, meaning you can adjust the entire spectacle system's pacing without changing code.

The profile is loaded at startup from `res://Data/best_tuning_full.json` (or `best_tuning_demo.json` in demo mode). If the file doesn't exist, the hardcoded defaults in `SpectacleTuningProfile` are used. The `--tuning_file` CLI arg overrides this for pipeline runs.

**Key multipliers:**

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
| ContributionWindowMultiplier | 0.8148 | How far back role resolution looks |
| RoleLockMeterThresholdMultiplier | 1.1286 | When roles freeze |
| CopyMultiplierScale | 0.8532 | How much stacking same mod pays off |
| DiversityMultiplierScale | 0.867 | Bonus for mixing mods |
| TokenCapMultiplier | 1.1632 | Token bucket sizes |
| OverkillBloomDamageScaleMultiplier | 0.7532 | Bloom damage output |
| StatusDetonationDamageMultiplier | 1.2506 | Status pop damage |
| ResidueDurationMultiplier | 1.2161 | How long residue zones last |

### Per-Mod and Per-Tower Overrides
The profile also carries dictionaries for per-mod `GainMultipliers`, `EventScalarMultipliers`, `TokenCapMultipliers`, `TokenRegenMultipliers`, and per-tower `TowerMeterGainMultipliers` / `TowerSurgeThresholdMultipliers`. These stack multiplicatively with the global versions.

### Automated Tuning Pipeline
`run_tuning_pipeline.ps1` runs iterative bot playtest sweeps and gradient-searches tuning parameters to hit target win rates (~75% Normal, ~50% Hard). The pipeline outputs `Data/best_tuning_full.json` / `Data/best_tuning_demo.json`. Those files ship with the game.

---

## 20. Code Map

| File | Role |
|---|---|
| `SpectacleSystem.cs` | Per-tower meter, token gate, gain formula, surge trigger, global meter, global surge arming |
| `SpectacleDefinitions.cs` | Constants, base gains, token configs, event scalar formulas, effect name tables, resolve helpers |
| `SpectacleTuning.cs` | `SpectacleTuningProfile` (all multipliers), `SpectacleTuning.Current` singleton |
| `SurgeDifferentiation.cs` | Global surge archetype table (label + feel), `ResolveLabel()`, `ResolveFeel()`, flash alpha |
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
| GlobalContributionWindowSeconds | 6s |
| InactivityGraceSeconds | 2s |
| InactivityDecayPerSecond | 3 pts/s |
| ContributionWindowSeconds | 20s |
| RoleLockMeterThreshold | 20 |
| MeterGainScale | 0.75 / 1.30 ≈ 0.577 |
| MeterIntervalReference | 1.0s |
| MeterIntervalMinScale | 0.65 (floor) |
| MeterIntervalMaxScale | 1.5 (cap) |
| MeterDamageReference | 20 |
| MeterDamageWeight | 0.90 |
| MeterDamageMinMultiplier | 0.45 |
| MeterDamageMaxMultiplier | 2.40 |
| OverkillBloomOverflowThreshold | 6 |
| OverkillBloomDamageScale | 0.42 |
| OverkillBloomDamageCap | 86 |
| OverkillBloomRadiusMin / Max | 120 / 250 px |
| MarkedPopRadius | 170 px |
| MarkedPopDamageAmpMultiplier | +22% for 1.45s |
| SurgeStatusDetonationStaggerSeconds | 0.20s |
| ResidueFrostRadius | 88 px |
| ResidueVulnerabilityRadius | 108 px |
| ResidueBurnRadius | 96 px |
| ResidueTickIntervalSeconds | 0.20s |
| HitStopMinDuration | 0.04s |
| HitStopMaxDuration | 0.08s |
| LargeSurgeAfterimagePowerThreshold | 1.55 |
| TwoStageBlastDelaySeconds | 0.12s |
| GlobalSurgeWaveSpeedMin / Max | 880 / 1280 px/s |
| Pulse decay rate | 0.72/s |
| Pulse hold duration | 0.42s |
