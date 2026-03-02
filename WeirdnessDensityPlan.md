
# Slot Theory — Weirdness Density Implementation Plan

## Overview

Implementing the top 3 modifiers from the design brief in priority order:
1. **Split Shot** — on hit, fires split projectiles at nearby enemies (stacks)
2. **Feedback Loop** — killing an enemy reduces current cooldown (stacks)
3. **Chain Reaction** — hit jumps to a nearby enemy (stacks, reuses chain infrastructure)

Stop and reassess balance after these three.

### Stacking Philosophy

All three modifiers are designed so that every copy always does something — no dead picks, no UI disclaimers. The natural limiters are built into the mechanics themselves:

| Modifier | What stacks | Natural limiter |
|---|---|---|
| Split Shot | +1 split projectile per copy | Needs enemies within 200px; empty space = wasted shots |
| Feedback Loop | Each copy fires independently on kill | Multiplicative with itself → diminishing returns (×0.7 per copy) |
| Chain Reaction | +1 bounce per copy | Damage decays 60% per hop; 3 copies = 21.6% on last bounce |

No hard caps or artificial blocks required. The opportunity cost of stacking the same modifier (sacrificing Momentum, Focus Lens, etc. for a 3rd copy) is itself a balance lever.

---

## Architecture Notes (from codebase review)

Key facts that affect implementation decisions:

- `TowerInstance.IsChainTower` is a **computed property**: `=> ChainCount > 0`. Adding chain behavior to any tower only requires `tower.ChainCount += 1` in `OnEquip()`. No separate bool needed.
- Default `TowerInstance` values: `ChainCount = 0`, `ChainDamageDecay = 0.6f`, `ChainRange = 260f`. Non-chain towers already have range/decay defaults — no need to set them in Chain Reaction `OnEquip()`.
- `DamageContext` with `damageOverride >= 0` overrides `BaseDamage`. Use `isChain: true` + `damageOverride` to apply fixed damage that skips `ModifyDamage` hooks.
- `OnHit` and `OnKill` fire regardless of `IsChain` — status effects (Slow, Mark) still propagate on chain/split hits.
- `ProjectileVisual` spawns `DamageContext` fresh on arrival — no damage state carried in flight. To apply custom damage, extend `Initialize()` with `damageOverride`.
- Bot mode bypasses `ProjectileVisual` entirely — split shot needs a separate bot path in `CombatSim`.

---

## Files Changed Summary

### New Files (3)
```
Scripts/Modifiers/SplitShot.cs
Scripts/Modifiers/FeedbackLoop.cs
Scripts/Modifiers/ChainReaction.cs
```

### Modified Files (5)
```
Data/modifiers.json              — 3 new entries
Scripts/Core/Balance.cs          — 4 new constants
Scripts/Modifiers/ModifierRegistry.cs   — 3 new factory entries
Scripts/Entities/TowerInstance.cs       — add SplitCount int
Scripts/Entities/ProjectileVisual.cs    — split shot visual logic
Scripts/Combat/CombatSim.cs            — split shot bot mode path
```

---

## Step 1 — Balance.cs

Add four new constants under the existing modifier block:

```csharp
// Split Shot modifier
public const float SplitShotDamageRatio = 0.60f;  // 60% of base damage per split
public const float SplitShotRange       = 200f;   // retarget radius from impact point

// Feedback Loop modifier
public const float FeedbackLoopCooldownReduction = 0.30f;  // 30% of remaining cooldown removed on kill

// Chain Reaction modifier — range/decay inherit tower defaults (260f / 0.6f)
// No new constants needed; modifier just adds 1 to ChainCount
```

---

## Step 2 — Data/modifiers.json

Add three entries following the existing schema:

```json
"split_shot": {
    "Id": "split_shot",
    "Name": "Split Shot",
    "Description": "On hit, fires a split projectile at 60% damage to a nearby enemy. Each additional copy fires one more split."
},
"feedback_loop": {
    "Id": "feedback_loop",
    "Name": "Feedback Loop",
    "Description": "Killing an enemy reduces this tower's current cooldown by 30%."
},
"chain_reaction": {
    "Id": "chain_reaction",
    "Name": "Chain Reaction",
    "Description": "After hitting a target, the attack jumps to 1 nearby enemy for 60% damage."
}
```

---

## Step 3 — Modifier Classes

### 3a. `Scripts/Modifiers/FeedbackLoop.cs` (simplest — implement first)

```csharp
using SlotTheory.Combat;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

public class FeedbackLoop : Modifier
{
    public FeedbackLoop(ModifierDef def) { ModifierId = def.Id; }

    public override void OnKill(DamageContext ctx)
    {
        ctx.Attacker.Cooldown = System.MathF.Max(
            0f,
            ctx.Attacker.Cooldown * (1f - Balance.FeedbackLoopCooldownReduction)
        );
    }
}
```

**Design note:** Only `Cooldown` is reduced (the current countdown timer), not `AttackInterval` (the base period). This prevents permanent acceleration — it's a one-shot burst when you get a kill, not compounding speed. Compliant with the design brief's warning against infinite cooldown loops.

---

### 3b. `Scripts/Modifiers/ChainReaction.cs`

```csharp
using SlotTheory.Combat;
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

public class ChainReaction : Modifier
{
    public ChainReaction(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        // IsChainTower is computed as ChainCount > 0.
        // Adding 1 activates the full existing chain infrastructure:
        //   - ProjectileVisual.ApplyChainHits()
        //   - SpawnChainArc() visual
        //   - CombatSim.ApplyChainBotMode()
        // Non-chain tower defaults (ChainRange=260, ChainDamageDecay=0.6) apply automatically.
        tower.ChainCount += 1;
    }
}
```

**This reuses 100% of existing chain infrastructure.** The chain tower type has `ChainCount = 2` set at placement time — adding this modifier to it gives 3 bounces. On any other tower, it activates chain behavior with 1 bounce at 60% damage (from the default `ChainDamageDecay = 0.6f`). The design brief specified 50% but 60% matches the chain tower's existing tuning — adjust in Balance.cs if playtesting reveals it's too strong.

**Marker Tower + Chain Reaction synergy:** Chain hits still trigger `OnHit`, and `AppliesMark` is checked in `DamageModel.Apply()` regardless of `IsChain`. So the chain target gets Marked. This is intentional and desirable.

---

### 3c. `Scripts/Modifiers/SplitShot.cs`

```csharp
using SlotTheory.Entities;

namespace SlotTheory.Modifiers;

public class SplitShot : Modifier
{
    public SplitShot(ModifierDef def) { ModifierId = def.Id; }

    public override void OnEquip(TowerInstance tower)
    {
        tower.SplitCount += 1;
    }
}
```

`SplitCount` starts at 0 and increments by 1 per copy of the modifier. The visual spawning logic reads `tower.SplitCount` to know how many split projectiles to spawn. At max 3 modifiers per tower, the worst case is 3× Split Shot = 3 splits per hit — each at 60% damage and each still requiring a valid nearby target. The visual spawning logic lives in `ProjectileVisual` (same pattern as `IsChainTower` / `ApplyChainHits`). Bot mode logic lives in `CombatSim`.

---

## Step 4 — TowerInstance.cs

Add one property after the chain properties (around line 33):

```csharp
public int SplitCount { get; set; } = 0;
```

An int rather than a bool so each copy of the modifier has an effect. `SplitCount > 0` acts as the "has split shot" check everywhere it's needed.

---

## Step 5 — ProjectileVisual.cs

Two changes:

### 5a. Extend `Initialize()` with optional parameters

Add `bool isSplitProjectile = false` and `float damageOverride = -1f` to the signature:

```csharp
public void Initialize(Vector2 fromGlobal, EnemyInstance target, Color color, float speed,
                       TowerInstance tower, int waveIndex, List<EnemyInstance> enemies,
                       RunState? runState = null,
                       bool isSplitProjectile = false, float damageOverride = -1f)
{
    GlobalPosition = fromGlobal;
    _target             = target;
    _tower              = tower;
    _waveIndex          = waveIndex;
    _enemies            = enemies;
    _runState           = runState;
    _speed              = speed;
    _color              = color;
    _isSplitProjectile  = isSplitProjectile;
    _damageOverride     = damageOverride;
}
```

Add the two new fields at the top of the class:

```csharp
private bool  _isSplitProjectile = false;
private float _damageOverride     = -1f;
```

### 5b. On-impact: use damageOverride + trigger splits

In `_Process()`, replace the existing impact block:

```csharp
// Old:
var ctx = new DamageContext(_tower, _target, _waveIndex, _enemies, _runState);
DamageModel.Apply(ctx);

// New:
var ctx = _damageOverride >= 0f
    ? new DamageContext(_tower, _target, _waveIndex, _enemies, _runState,
                        isChain: true, damageOverride: _damageOverride)
    : new DamageContext(_tower, _target, _waveIndex, _enemies, _runState);
DamageModel.Apply(ctx);
```

Then after `DamageModel.Apply(ctx)` and before chain hit logic, add split trigger:

```csharp
// Split Shot — spawn child projectiles (primary hit only, no recursive splits)
if (_tower.SplitCount > 0 && !_isSplitProjectile)
    SpawnSplitProjectiles(_target.GlobalPosition);
```

Also guard chain hits to prevent split projectiles from chaining:

```csharp
// Old:
if (_tower.IsChainTower)
    ApplyChainHits(_target.GlobalPosition);

// New:
if (_tower.IsChainTower && !_isSplitProjectile)
    ApplyChainHits(_target.GlobalPosition);
```

### 5c. Add `SpawnSplitProjectiles()` method

```csharp
private void SpawnSplitProjectiles(Vector2 impactPos)
{
    if (_tower == null || _enemies == null) return;

    float splitDamage = _tower.BaseDamage * Balance.SplitShotDamageRatio;
    int spawned = 0;

    // Sort by distance to impact — pick the nearest SplitCount valid targets
    var candidates = _enemies
        .Where(e => e != _target && e.Hp > 0 && GodotObject.IsInstanceValid(e))
        .OrderBy(e => e.GlobalPosition.DistanceTo(impactPos));

    foreach (var candidate in candidates)
    {
        if (spawned >= _tower.SplitCount) break;
        if (candidate.GlobalPosition.DistanceTo(impactPos) > Balance.SplitShotRange) break;

        var split = new ProjectileVisual();
        GetParent().AddChild(split);
        // Slightly transparent to visually distinguish from primary
        var splitColor = new Color(_color.R, _color.G, _color.B, 0.65f);
        split.Initialize(impactPos, candidate, splitColor, speed: 500f,
                         _tower, _waveIndex, _enemies, _runState,
                         isSplitProjectile: true, damageOverride: splitDamage);
        spawned++;
    }
}
```

The cap is `_tower.SplitCount` — 1 split per copy of the modifier. If there are fewer valid targets nearby than `SplitCount`, the loop exits early with no error.

---

## Step 6 — CombatSim.cs (bot mode split shot)

In `SpawnProjectile()`, after the primary `DamageModel.Apply()` and chain bot call, add:

```csharp
if (BotMode)
{
    DamageModel.Apply(new DamageContext(tower, target, waveIndex, enemies, _state));
    if (tower.IsChainTower)
        ApplyChainBotMode(tower, target, waveIndex, enemies);
    if (tower.SplitCount > 0)                                         // NEW
        ApplySplitBotMode(tower, target, waveIndex, enemies);        // NEW
    return;
}
```

Add the new helper method:

```csharp
private void ApplySplitBotMode(TowerInstance tower, EnemyInstance primary,
                                int waveIndex, List<EnemyInstance> enemies)
{
    // Bot mode: GlobalPositions unreliable; approximate by hitting next SplitCount alive enemies in list order
    float splitDamage = tower.BaseDamage * Balance.SplitShotDamageRatio;
    int spawned = 0;

    foreach (var e in enemies)
    {
        if (spawned >= tower.SplitCount) break;
        if (e == primary || e.Hp <= 0) continue;
        DamageModel.Apply(new DamageContext(tower, e, waveIndex, enemies, _state,
                                            isChain: true, damageOverride: splitDamage));
        spawned++;
    }
}
```

---

## Step 7 — ModifierRegistry.cs

Add three entries to `_factories`:

```csharp
["split_shot"]      = def => new SplitShot(def),
["feedback_loop"]   = def => new FeedbackLoop(def),
["chain_reaction"]  = def => new ChainReaction(def),
```

---

## Implementation Order

Follow this sequence to minimize integration friction:

1. **Balance.cs** — constants must exist before anything else compiles
2. **modifiers.json** — data entries (no compile dependency)
3. **FeedbackLoop.cs + registry** — touches nothing else; validate full pipeline end-to-end
4. **ChainReaction.cs + registry** — touches only `TowerInstance.ChainCount`; test chain arc appears
5. **TowerInstance.cs** — add `SplitCount`
6. **ProjectileVisual.cs** — extended Initialize + split spawning
7. **CombatSim.cs** — bot mode split path
8. **SplitShot.cs + registry** — wire it all together

---

## Verification Checklist

### Feedback Loop
- [ ] Kill triggers 30% cooldown reduction on the attacking tower
- [ ] Cooldown clamps to 0, never goes negative
- [ ] `AttackInterval` (base period) is NOT modified — no permanent speed-up
- [ ] Multiple rapid kills each give one reduction (not compounding)
- [ ] Works in bot mode (OnKill fires in hitscan path too)

### Chain Reaction
- [ ] Tower with modifier fires a chain arc to 1 nearby enemy on hit
- [ ] Secondary target takes ~60% of base damage (ChainDamageDecay default)
- [ ] Chain arc visual appears (thin lightning Line2D — already exists)
- [ ] No modifier damage hooks fire on the secondary hit (isChain: true)
- [ ] Marker Tower + Chain Reaction marks the chain target (OnHit still fires)
- [ ] Slow + Chain Reaction applies slow to chain target
- [ ] Chain Tower + Chain Reaction = 3 total bounces (synergy works)
- [ ] Works in bot mode (ApplyChainBotMode path)

### Split Shot
- [ ] 1× modifier: spawns 1 split projectile; 2× spawns 2; 3× spawns 3
- [ ] Split projectiles deal 60% of base damage each
- [ ] Split projectiles are visually distinct (alpha ~0.65)
- [ ] No recursion — split projectiles do NOT spawn further splits
- [ ] Split projectiles do NOT trigger chain bounces even if tower has Chain Reaction
- [ ] Projectile toward dead/invalid target dissolves without damage (target gone = QueueFree)
- [ ] Fewer valid targets than SplitCount = fewer splits fired, no error
- [ ] Marker Tower + Split Shot marks N additional enemies per shot (N = SplitCount)
- [ ] Slow + Split Shot slows N additional enemies (OnHit fires on split hits)
- [ ] Bot mode: hits next SplitCount alive enemies in list order
- [ ] Split Shot + Chain Reaction on same tower: primary chains, splits do NOT (see guard)

---

## Risk Register

| Risk | Severity | Mitigation |
|------|----------|------------|
| Split + Chain double-dipping | Medium | `!_isSplitProjectile` guard on `ApplyChainHits` |
| Feedback Loop + Focus Lens (slow fire + big reward) | Low | Only reduces Cooldown, not AttackInterval; no compounding |
| Chain Reaction on Marker Tower marking everything | Low | Intentional synergy; Marked duration is short (2s) |
| Split shot finding 0 nearby enemies | None | Loop just exits with `spawned = 0`; no error |
| Chain Reaction + Chain Tower (3 bounces) | Low | 3 bounces at 60% decay = primary + 60% + 36% total; not game-breaking |
| Two Split Shot modifiers on same tower | Low | 2× = 2 splits at 60% each; 3× = 3 splits. Requires nearby targets to matter. Opportunity cost is high (no Momentum, no Focus Lens). |

---

## After These Three: Reassess

Before implementing Resonance or Echo Mount, run the bot 100 times and check:
- Are Feedback Loop + Momentum towers winning too often?
- Is Split Shot + Marker breaking Marked propagation balance?
- Is Chain Reaction making Chain Tower obsolete?

Target: all strategies within 15% win rate of each other across 100 bot runs.
