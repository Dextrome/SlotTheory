# Slot Theory

A constraint-driven drafting tower defense. Place towers, pick upgrades, survive 20 waves.

**Available on:** Windows Desktop, Android (Phone & Tablet)

---

## How to Play

### Core Loop

1. **Draft** — Before each wave, pick **1 of 5 cards**.
2. **Wave** — Enemies march automatically. No interaction mid-wave.
3. **Repeat** — 20 waves total. Survive all of them to win.

You lose a life for every enemy that reaches the exit. Lose all 10 lives and it's over.

---

## Controls

### Desktop Controls

| Action | Input |
|---|---|
| Pick a draft card | Left-click the card |
| Assign to a slot / tower | Left-click the target in the world |
| Cycle tower targeting mode | Left-click a tower during a wave |
| Pause / unpause | **Esc** |
| Speed up / slow down | Speed button in HUD — cycles ×1 → ×2 → ×3 |
| Quit to main menu | Pause → Main Menu |
| Access How to Play | Main Menu → How to Play |

### Mobile Controls

| Action | Input |
|---|---|
| Pick a draft card | Tap the card |
| Assign to a slot / tower | Tap the target in the world |
| Cycle tower targeting mode | Tap a tower during a wave |
| Pause / unpause | Hamburger menu button (☰) in top-right corner |
| Speed up / slow down | Speed button in HUD — cycles ×1 → ×2 → ×3 |
| Quit to main menu | Pause → Main Menu |
| Access How to Play | Main Menu → How to Play |

**Note:** The game automatically pauses when minimized on Android devices.

---

## Map Selection

Before starting a game, you can choose from multiple procedurally generated maps. Each map features:
- A unique snake-path layout with randomized turns
- 6 strategically placed tower slots  
- Varied terrain providing different tactical challenges

The first map is automatically selected when you enter the map selection screen, but you can browse and choose any available map before starting your run.

---

## Draft Cards

Which cards appear depends on your situation:

- **Free slots available** → 5 cards: 2 tower cards + 3 modifier cards
- **All slots full** → 4 modifier cards (no new towers to place)

### Tower Cards

Towers go into one of the 6 slots on the map. After picking a tower card, click an empty slot in the world to place it. You cannot move or sell a placed tower.

### Modifier Cards

Modifiers upgrade an existing tower. After picking a modifier card, click any tower in the world to assign it. Each tower holds up to **3 modifiers**. Fully upgraded towers are ineligible.

If no draft options are possible (all slots full and all towers at modifier cap), the draft is skipped and the wave starts immediately.

**Bonus picks**: Wave 1 and Wave 15 each give you 2 picks instead of 1 — use them to establish a strong build early and recover mid-run.

**Wave preview**: The draft panel shows the enemy composition of the upcoming wave (e.g., `↓ 20 Basic · 3 Armored · 3 Swift [clumped]`) so you can plan your pick before committing.

---

## Towers

| Tower | Shape / Color | Damage | Attack Speed | Range | Notes |
|---|---|---|---|---|---|
| **Rapid Shooter** | Hexagonal cyan | 10 | 0.4 s | 300 px | High rate of fire, low damage per hit |
| **Heavy Cannon** | Octagonal orange | 60 | 2.0 s | 250 px | Slow but hits hard |
| **Marker Tower** | Diamond pink | 5 | 1.0 s | 350 px | Applies **Mark** on every hit |
| **Arc Emitter** | Circular blue-white | 14 | 1.2 s | 270 px | Chains to 2 nearby enemies (60% decay per bounce) |

---

## Targeting Modes

Click a tower during a wave to cycle through targeting priorities:

| Icon | Mode | Target |
|---|---|---|
| ▶ | **First** | Enemy furthest along the path |
| ★ | **Strongest** | Enemy with the most current HP |
| ▼ | **Lowest HP** | Enemy closest to death |

Hover over a tower to see its current targeting mode and equipped modifiers.

---

## Modifiers

| Modifier | Effect |
|---|---|
| **Momentum** | +16% damage per consecutive hit on the same target, capped at 5 stacks (×1.80 max). Resets when the tower switches targets. |
| **Overkill** | Excess damage from a killing blow spills over to the next enemy in the lane. |
| **Exploit Weakness** | Deal +60% damage to **Marked** enemies. Pairs with Marker Tower. |
| **Focus Lens** | +125% damage, ×2 attack interval. Big hits, slow fire — ideal for Overkill combos and one-shotting tanky enemies. |
| **Chill Shot** | On hit, −25% movement speed for 5 seconds. Enemies linger in range longer, giving all towers more time to fire. |
| **Overreach** | +40% range, −20% damage. Wider coverage at a small cost — great on Marker Tower or any tower that needs to reach more of the path. |
| **Hair Trigger** | +40% attack speed, −18% range. Close-quarters rapid-fire — pairs naturally with Momentum and Chill Shot. |
| **Split Shot** | On hit, fires 2 projectiles at nearby enemies for 42% damage each. Each additional copy fires one more projectile. Stacks well on heavy hitters. |
| **Feedback Loop** | Killing an enemy reduces the tower's current cooldown by 25%. Lets towers that kill quickly cycle back faster. |
| **Chain Reaction** | After each hit, the attack jumps to 1 nearby enemy for 55% damage. Each additional copy adds 1 more bounce — 3 copies = 4 targets hit per shot. |

---

## Marked

When a **Marker Tower** hits an enemy it applies the **Mark** status for 2 seconds.
While Marked, that enemy takes **+40% damage from all towers** (before any modifier bonuses).

Pair Marker Tower + **Exploit Weakness** on another tower for a large burst damage combo.

---

## Enemies

### Basic Walker
- 120 px/s movement, 65 HP on wave 1, scales ×1.08 per wave (~280 HP by wave 20).
- Leaks cost **1 life**.
- Round teal body — easy to spot.

### Armored Walker
- Half speed (60 px/s), 4× the HP of a Basic Walker on the same wave.
- Leaks cost **2 lives** — always prioritise.
- Large hexagonal crimson body at 1.5× scale — unmistakable.
- First appears on wave 7; up to 5 per wave by wave 20.

### Swift Walker
- Double speed (240 px/s), 1.5× the HP of a Basic Walker on the same wave.
- Leaks cost **1 life**, but their speed makes them hard to catch.
- Small lime-green diamond at 0.8× scale — easily identified by colour.
- Appears waves 10–14, 2–4 per wave, spread evenly through the spawn order.

Enemy count and spawn speed both increase over 20 waves (from 10 enemies at wave 1 up to 30 at wave 20).

Waves 12–14 use **clumped** Armored spawns: all Armored Walkers arrive as a consecutive group after the initial basic wave, creating a mid-wave panic spike. Swift Walkers are spread into the remaining gaps.

---

## Maps

Each run generates a new snake-shaped path across an 8×5 grid. Tower slots are placed in grass cells adjacent to the path so every tower is always within shooting range of passing enemies.

---

## User Interface & Accessibility

- **Responsive Design**: UI automatically adapts to different screen sizes and platforms
- **Mobile Optimized**: Special considerations for touch controls, button sizing, and screen real estate on phones and tablets  
- **How to Play**: Comprehensive in-game documentation accessible from the main menu with smooth scrolling and platform-optimized formatting
- **Pause Menu**: Accessible via Esc (desktop) or hamburger menu (mobile) during gameplay
- **Auto-Pause**: Game automatically pauses when minimized on Android devices

---

## Tips

- **Rapid Shooter + Momentum**: Stack Momentum on your fastest-firing tower for massive DPS on tanky enemies that take multiple hits to kill.
- **Marker Tower + Exploit Weakness**: Mark the enemy, then burst it for ×2.1 total (+40% mark × +50% exploit). The Marker Tower frees up your damage towers' modifier slots for pure damage.
- **Heavy Cannon + Overkill**: One-shots weaker enemies and spills excess damage forward — good for tightly packed groups.
- **Arc Emitter + Chain Reaction**: Each copy adds a bounce — 3 copies hits 5 targets per shot. Best at corners where enemies cluster.
- **Heavy Cannon + Split Shot**: Even at 42% damage per split, the cannon's high base hit still provides meaningful side pressure on clustered enemies.
- **Feedback Loop + Hair Trigger**: Killing enemies reduces cooldown; rapid-fire towers cycle back almost instantly in waves with many weak enemies.
- **Focus Lens** trades fire rate for huge individual hits — pairs naturally with Overkill to punch through groups.
- **Swift Walkers (waves 10–14)** are fast but fragile — Chill Shot or Overreach helps towers catch them before they run out of range. Chain and Split Shot can hit multiple Swifts at once.
- **Targeting mode matters late game** — switch your Marker Tower to *First* so it tags the lead enemy before your damage towers hit it.

---

## Platform Support

### Windows Desktop
- Full mouse and keyboard controls
- Pause via Esc key
- Optimized for desktop screen sizes

### Android (Phone & Tablet)  
- Touch-optimized controls and UI scaling
- Hamburger menu for pause access
- Automatic pause when app is minimized
- Responsive layout adapts to screen size and orientation

---

**Engine:** Godot 4.6.1 with .NET 8.0  
**Development:** 7ants Studios
