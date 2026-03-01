# Neon Synthwave Visual Theme Plan

## Goal
Replace the green-grass tower-defense aesthetic with a dark neon-synthwave look
using only Godot's `_Draw()` API and node primitives. No external assets.

---

## Color Palette

| Role                | Color (hex approx) | RGBA float                        |
|---------------------|--------------------|-----------------------------------|
| Background          | `#0a0019`          | `(0.04, 0.00, 0.10)`              |
| Grid lines          | `#6600a6`          | `(0.40, 0.00, 0.65, 0.28)`        |
| Grid glow           | `#8000cc`          | `(0.50, 0.00, 0.80, 0.07)`        |
| Horizon line        | `#b200ff`          | `(0.70, 0.00, 1.00, 0.65)`        |
| Path edge glow      | `#ff0d8c`          | `(1.00, 0.05, 0.55, 0.10)`        |
| Path dark fill      | `#1a002e`          | `(0.10, 0.00, 0.18)`              |
| Path core edge      | `#ff40a6`          | `(1.00, 0.25, 0.65, 0.85)`        |
| Slot border         | `#cc00ff`          | `(0.80, 0.00, 1.00, 0.80)`        |
| Rapid Shooter       | `#1abfff` (cyan)   | `(0.10, 0.75, 1.00)`              |
| Heavy Cannon        | `#ff8c00` (orange) | `(1.00, 0.55, 0.00)`              |
| Marker Tower        | `#ff2699` (pink)   | `(1.00, 0.15, 0.60)`              |
| Basic Walker        | `#00f2cc` (cyan)   | `(0.00, 0.95, 0.80)`              |
| Armored Walker      | `#ff0d8c` (pink)   | `(1.00, 0.05, 0.55)`              |
| Path flow chevrons  | `#00f0ff` (cyan)   | `(0.00, 0.94, 1.00, 0.40)`        |

---

## Glow Technique

All glowing shapes use the **double-draw pattern**:
1. Draw 1–2 larger versions with low alpha (outer/inner glow halo)
2. Draw the real shape at full opacity on top

For polygons: draw a **bright outer polygon** then a **dark inner polygon** of slightly
smaller radius to fake a neon outlined look.

For circles: just stack 2 extra transparent circles at larger radii before the core.

For lines: 3–5 overlapping Line2Ds with widths 120/80/50/16/3 px and alphas
0.05/0.10/0.95/0.18/0.85 for path layering.

---

## Changes Per File

### New: `Scripts/Core/GridBackground.cs`
- `Node2D` with `_Draw()` only — no `_Process()`
- Draws the dark background `DrawRect` covering full 1280×720
- Draws 9 vertical + 6 horizontal grid lines (cell boundaries) in two passes:
  glow (6px, alpha 0.07) then core (1.5px, alpha 0.28)
- Draws a bright violet horizon line at `y = GRID_Y`

### `Scripts/Core/GameController.cs`
- **`RenderMap()`**: Replace grass/splotches/brown-road/decorations with:
  - `GridBackground` node
  - 5 layered `Line2D` nodes for neon path (outer glow → dark fill → edge glow → core)
  - `PathFlow` at end (unchanged)
  - Remove: MakeTree, MakeRock, MakeCirclePoly (dead code)
- **`SetupSlots()`**: Dark purple `ColorRect` bg + 2 `Line2D` nodes for neon violet border
- **`PlaceTower()`**: Update `BodyColor` for all 3 towers to match neon palette

### `Scripts/Entities/TowerInstance.cs`
All 3 draw methods get:
- 2 glow `DrawCircle` calls pre-drawn (radius ~22 & ~15, alpha 0.07 & 0.14)
- Shapes redrawn using **bright outer + dark inner** polygon technique
- Muzzle/beam tips get extra glow circles

### `Scripts/Entities/EnemyInstance.cs`
- **Basic Walker**: cyan glow + neon ring outline + dark interior
- **Armored Walker**: hot-pink glow + neon hexagon outline + dark interior
- **HP bar**: dark-purple track; fill color cycles cyan→yellow→pink by health ratio
  (replaces green→yellow→red)

### `Scripts/Core/PathFlow.cs`
- Chevron color: `Color(1f, 1f, 1f, 0.18f)` → `Color(0f, 0.94f, 1f, 0.40f)` (neon cyan)

### `Scripts/Combat/CombatSim.cs`
- Death burst colors updated to match neon enemy palette:
  - Basic walker death → cyan `(0.00, 0.95, 0.80)`
  - Armored walker death → hot pink `(1.00, 0.05, 0.55)`

---

## What Stays the Same
- Projectile trails (already neon-colored per tower)
- Damage numbers (inherit projectile color, already bright)
- Death burst animation shape (just color changes)
- Charge arc (already glow-like; BodyColor update makes it match)
- Status rings on enemies (Marked = purple, Slow = cyan — already fit the palette)
- All gameplay logic, HUD, UI screens

---

## Verification
1. `dotnet build` → 0 errors
2. Run → dark purple background with purple grid visible
3. Path appears as a dark strip with glowing pink edges
4. Tower charge arcs glow in tower colors (cyan/orange/pink)
5. Enemies glow cyan (basic) and pink (armored)
6. Death bursts match enemy color
7. Slot borders glow violet
8. Path chevrons visible as cyan arrows
