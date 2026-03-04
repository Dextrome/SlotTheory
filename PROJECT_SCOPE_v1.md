# 🎮 PROJECT SCOPE CONTRACT --- v1.0

### Working Title: Slot Theory

------------------------------------------------------------------------

## 1️⃣ Core Identity

> A constraint-driven drafting game where players commit to imperfect
> choices and amplify them into powerful builds.

**Design Pillars:** - Decisions are permanent. - Commitment is
rewarded. - Synergies are predictable and readable. - No meta systems. -
No economy layer. - No feature creep.

------------------------------------------------------------------------

## 2️⃣ Core Loop

Each round:

1.  Player drafts **1 of 5 options**.
2.  Wave runs automatically.
3.  If player survives → next round.
4.  After final wave → victory.
5.  If overwhelmed during wave → run ends.

No mid-wave interaction.

------------------------------------------------------------------------

## 3️⃣ Win / Loss Conditions

-   Fixed-length run.
-   **20 waves total.**
-   Lose if enemies reach end during a wave (simple loss for v1).
-   Win after wave 20 clears.

------------------------------------------------------------------------

## 4️⃣ Map & Structure

-   Single map.
-   Single lane.
-   Fixed enemy path.
-   6 fixed tower slots.
-   No movement.
-   No selling.
-   No rerolls.
-   No meta progression.

------------------------------------------------------------------------

## 5️⃣ Towers (4 types) ✅ IMPLEMENTED

### 1. Rapid Shooter

-   Fast attacks (0.4s interval)
-   Low damage (10)

### 2. Heavy Cannon

-   Slow attacks (2.0s interval)
-   High damage (60)

### 3. Marker Tower

-   Applies "Marked" (enemies take +40% damage for 2.5s)
-   Low damage (5)

### 4. Arc Emitter

-   Chains to 2 additional enemies
-   60% damage decay per bounce
-   260px chain range

All towers: - Targeting mode: First / Strongest / Lowest HP - Max **3
modifiers per tower** - Cannot exceed modifier cap

------------------------------------------------------------------------

## 6️⃣ Modifiers (10 types) ✅ IMPLEMENTED

Design Rules: - Predictable - Conditional - Reinforce commitment - No new mechanics

### Core Set (Original 4)
- **Momentum**: +16% damage per consecutive hit on same target (caps at ×1.8)
- **Overkill**: 60% excess damage spills to next enemy
- **Exploit Weakness**: +60% damage vs Marked enemies  
- **Focus Lens**: +125% damage, ×2 attack interval

### Extended Set (Added 6)
- **Chill Shot**: Hits slow enemies to 70% speed for 5s
- **Overreach**: +50% range, −30% damage
- **Hair Trigger**: +50% attack speed, −40% range
- **Split Shot**: Fires 2× 42% damage projectiles
- **Feedback Loop**: Kill reduces cooldown by 70%
- **Chain Reaction**: +1 chain bounce per copy

Stacking: - Additive within category - Simple predictable math

------------------------------------------------------------------------

## 7️⃣ Draft Rules

-   5 options per round
-   Until slots full:
    -   2 tower offers
    -   3 modifier offers
-   Once slots full:
    -   5 modifiers
-   No rerolls
-   No replacement mechanics

Modifier application: - Pick modifier → click tower to apply - If tower
at cap → cannot apply

------------------------------------------------------------------------

## 8️⃣ Enemies ✅ IMPLEMENTED

### 2 Enemy Types

**Basic Walker**:
- HP: 65 × 1.08^(wave-1)
- Speed: 120 px/s  
- Lives lost: 1

**Armored Walker** (appears wave 7+):
- HP: 4× Basic Walker HP
- Speed: 60 px/s (half speed)
- Lives lost: 2
- Visual: 1.5× scale, hexagonal crimson design

------------------------------------------------------------------------

## 9️⃣ Wave Structure ✅ IMPLEMENTED

-   Continuous spawn ✅
-   Variable spawn rate per wave ✅
-   Dynamic enemy count scaling ✅
-   HP scaling: HP × 1.08^(wave-1) ✅
-   Speed constant ✅
-   Difficulty modes: Normal/Hard multipliers ✅
-   Special clumped waves (12-14) ✅

------------------------------------------------------------------------

## 🔒 Absolute Scope Locks

This version will NOT include:

-   Meta progression
-   Shop systems
-   Gold / economy
-   Selling towers
-   Moving towers
-   Active enemy abilities
-   Procedural maps
-   Modifier replacement systems
-   Stackable rerolls
-   Mid-wave decisions
-   Rarity tiers
-   Unlock trees

If an idea requires a new system → move it to "Project 2."

------------------------------------------------------------------------

## 🏁 Definition of Done

The game is DONE when:

-   20 waves playable start to finish
-   3 towers implemented
-   4 modifiers implemented
-   Draft works
-   Targeting works
-   No critical bugs
-   Stable Windows build
-   Basic menu + settings exist

Even if balance isn't perfect. Even if improvements are obvious. Ship
anyway.

------------------------------------------------------------------------

## 🗓️ Development Phases

### Phase 1 (2 weeks)

-   Core combat
-   Enemy spawning
-   Basic targeting
-   2 towers

### Phase 2 (2 weeks)

-   Marker tower
-   Modifier system
-   Draft system (ugly UI allowed)

### Phase 3 (2--4 weeks)

-   Wave scaling
-   Basic polish
-   Sound feedback
-   Menu

Ship vertical slice internally before expanding.




------------------------------------------------------------------------


# Technical Notes

## Programming Language

### Game Engine
Pick: Godot (C#/.NET)

Godot is MIT licensed (free, no royalties, no tier thresholds).

Godot’s C# support is real, and as of Godot 4.4 your C# projects target .NET 8+ (which is fine and modern).

This fits the game (systems + UI + deterministic sim)

### Scripts/Tools

Use Python for:

- content generation scripts (JSON/YAML)

- balancing calculators

- wave curves / sim tooling

- modifier list generation

## Install

Godot “.NET” build (not the standard build), so you can write C#.

.NET SDK 8 on your machine. (Godot 4.4+ expects it.)