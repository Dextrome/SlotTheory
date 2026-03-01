# Slot Theory --- Getting Started Guide

Studio: 7ants Studios\
Engine: Godot 4.x (.NET build)\
Language: C#\
Runtime: .NET 8+

------------------------------------------------------------------------

## 1. Install & Setup

1.  Download **Godot 4.x .NET build** (C# version).
2.  Install **.NET SDK 8**.
3.  Create a new project:
    -   Name: `SlotTheory`
    -   Folder: `slot-theory`
4.  Run once to confirm C# works.

Create a Git repo immediately and commit the empty project.

------------------------------------------------------------------------

## 2. Initial Scene Structure

Create `Main.tscn`:

-   Root: `Node`
    -   `GameController` (script attached)
    -   `World` (Node2D)
    -   `UIRoot` (CanvasLayer)

Attach `GameController.cs`:

``` csharp
using Godot;

public partial class GameController : Node
{
    public override void _Ready()
    {
        GD.Print("Slot Theory booted.");
    }
}
```

Run. If it prints, your project is alive.

------------------------------------------------------------------------

## 3. Core Development Order (Do Not Skip Around)

### Phase 1: Wave Skeleton (No Towers Yet)

Goal: Controlled spawning + wave lifecycle.

1.  Create `WaveSystem.cs`
    -   Track wave index
    -   Spawn quota (e.g., 20 enemies)
    -   Spawn interval (e.g., 0.5s)
    -   Detect wave completion
2.  Spawn enemies continuously during the wave.
3.  End wave when:
    -   All enemies spawned
    -   All enemies dead

Do not add towers yet.

------------------------------------------------------------------------

## 4. Top-Down Enemy Architecture

### Recommended: PathFollow2D-based movement

Use a `Path2D` for the lane.

Structure:

World └── LanePath (Path2D)

Enemy scene root should be `PathFollow2D`.

Movement:

``` csharp
public override void _Process(double delta)
{
    Progress += Speed * (float)delta;
}
```

Expose:

-   `float ProgressRatio` (0..1) for targeting.
-   `float Hp`

Lose condition: If `ProgressRatio >= 1.0f` → enemy leaked.

------------------------------------------------------------------------

## 5. Slot System (Fixed)

Create `Slot.tscn`:

-   Root: `Node2D`
-   Positioned manually in world

Each slot holds: - Index - Optional TowerInstance reference

Slots never move. Towers are children of slots.

------------------------------------------------------------------------

## 6. Tower Core Logic (Hitscan Only)

No projectiles in v1.

Tower fields: - baseDamage - attackInterval - range - cooldown -
targetingMode (First / Strongest / LowestHP) - List`<Modifier>`{=html}
(max 3)

Per update:

1.  cooldown -= delta
2.  If cooldown \<= 0:
    -   Select target in range
    -   Apply damage instantly
    -   cooldown = attackInterval

Range check = simple circular distance in world space.

------------------------------------------------------------------------

## 7. Targeting System

Maintain a central list of alive enemies.

Do NOT scan the scene tree every frame.

Target rules:

-   First → highest ProgressRatio
-   Strongest → highest Hp
-   LowestHP → lowest Hp

All targeting should operate on a filtered list: Enemies in range only.

------------------------------------------------------------------------

## 8. Modifier System (Minimal Version)

Goals: - Data-driven availability (JSON) - Code-driven behavior - No
if/else spaghetti

Base Modifier methods:

-   ModifyDamage(ref damage, context)
-   ModifyAttackInterval(ref interval)
-   OnHit(context)
-   OnKill(context)

Attach modifiers to towers. Max 3 per tower. Additive stacking within
categories.

Vertical slice modifiers: - Momentum - Focus Lens - Exploit Weakness -
Overkill

Keep status effects minimal (only Marked in v1).

------------------------------------------------------------------------

## 9. Draft System (Before Wave)

Rules:

-   5 options per round
-   If slots not full:
    -   2 towers
    -   3 modifiers
-   If full:
    -   5 modifiers

Flow:

1.  Draft panel appears.
2.  Player selects option.
3.  Apply tower or modifier.
4.  Start wave.

No rerolls. No replacement. No selling.

------------------------------------------------------------------------

## 10. Implementation Roadmap

Week 1: - Enemy movement - Wave spawning - Wave end detection

Week 2: - Slot system - One tower - Targeting

Week 3: - Modifier framework - 2--3 modifiers working

Week 4: - Marker tower + synergy - Draft loop fully operational

Only after vertical slice works → expand content.

------------------------------------------------------------------------

## 11. Discipline Rules

-   No new systems without finishing current milestone.
-   No meta progression in v1.
-   No art polish before core loop feels good.
-   Commit to Git after every meaningful milestone.

------------------------------------------------------------------------

You now have a clean, bounded starting plan.

Build the wave loop first. Everything else plugs into it.
