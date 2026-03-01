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

## 5️⃣ Towers (Vertical Slice: 3)

### 1. Rapid Shooter

-   Fast attacks
-   Low damage

### 2. Heavy Cannon

-   Slow attacks
-   High damage

### 3. Marker Tower

-   Applies "Marked" (enemies take +20% damage for 2s)

All towers: - Targeting mode: First / Strongest / Lowest HP - Max **3
modifiers per tower** - Cannot exceed modifier cap

------------------------------------------------------------------------

## 6️⃣ Modifiers (Vertical Slice: 4)

Design Rules: - Predictable - Conditional - Reinforce commitment - No
new mechanics

### Momentum

Gain +10% damage per consecutive hit on same target (resets on switch).

### Overkill

Excess damage carries to next enemy.

### Exploit Weakness

Deal +100% damage to Marked enemies.

### Focus Lens

+100% damage but -50% attack speed.

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

## 8️⃣ Enemies

### Vertical Slice

1 Enemy Type: - Basic Walker - Medium HP - Medium speed

Full Version: - Max 3 enemy archetypes - Passive traits only - No active
abilities

------------------------------------------------------------------------

## 9️⃣ Wave Structure

-   Continuous spawn
-   Fixed spawn rate per wave
-   Fixed enemy count per wave (v1)
-   HP scaling per wave: HP × 1.12\^(wave-1)
-   Speed constant for v1

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