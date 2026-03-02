# Slot Theory

A constraint-driven drafting tower defense. Place towers, pick upgrades, survive 20 waves.

---

## How to Play

### Core Loop

1. **Draft** — Before each wave, pick **1 of 5 cards**.
2. **Wave** — Enemies march automatically. No interaction mid-wave.
3. **Repeat** — 20 waves total. Survive all of them to win.

You lose a life for every enemy that reaches the exit. Lose all 10 lives and it's over.

---

## Controls

| Action | Input |
|---|---|
| Pick a draft card | Left-click the card |
| Assign to a slot / tower | Left-click the target in the world |
| Cycle tower targeting mode | Left-click a tower during a wave |
| Pause / unpause | **Esc** |
| Speed up / slow down | Speed button in HUD — cycles ×1 → ×2 → ×3 |
| Quit to main menu | Pause → Main Menu |

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

**Wave preview**: The draft panel shows the enemy composition of the upcoming wave (e.g., `↓ 22 Basic · 3 Armored [clumped]`) so you can plan your pick before committing.

---

## Towers

| Tower | Shape / Color | Damage | Attack Speed | Range | Notes |
|---|---|---|---|---|---|
| **Rapid Shooter** | Hexagonal cyan | 10 | 0.4 s | 300 px | High rate of fire, low damage per hit |
| **Heavy Cannon** | Octagonal orange | 60 | 2.0 s | 250 px | Slow but hits hard |
| **Marker Tower** | Diamond pink | 5 | 1.0 s | 350 px | Applies **Mark** on every hit |

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
| **Momentum** | +10% damage per consecutive hit on the same target, capped at 5 stacks (+50% max). Resets when the tower switches targets. |
| **Overkill** | Excess damage from a killing blow spills over to the next enemy in the lane (once per attack). |
| **Exploit Weakness** | Deal +50% damage to **Marked** enemies. Pairs with Marker Tower. |
| **Focus Lens** | +150% damage, ×2 attack interval. Net DPS +25% — but the big hits are the point: ideal for Overkill combos and one-shotting tanky enemies. |
| **Chill Shot** | On hit, −30% movement speed for 5 seconds. Enemies linger in range longer, giving all towers more time to fire. |
| **Overreach** | +50% range, −25% damage. Wider coverage — great on Marker Tower or any tower that needs to reach more of the path. |
| **Hair Trigger** | +50% attack speed, −30% range. Close-quarters rapid-fire — pairs naturally with Momentum and Chill Shot. |

---

## Marked

When a **Marker Tower** hits an enemy it applies the **Mark** status for 2 seconds.
While Marked, that enemy takes **+20% damage from all towers** (before any modifier bonuses).

Pair Marker Tower + **Exploit Weakness** on another tower for a large burst damage combo.

---

## Enemies

### Basic Walker
- 120 px/s movement, 65 HP on wave 1, scales ×1.06 per wave (~197 HP by wave 20).
- Leaks cost **1 life**.
- Round teal body — easy to spot.

### Armored Walker
- Half speed (60 px/s), 4× the HP of a Basic Walker on the same wave.
- Leaks cost **2 lives** — priority target.
- Large hexagonal crimson body at 1.5× scale — unmistakable.
- First appears on wave 7; up to 5 per wave by wave 20.

Enemy count and spawn speed both increase over 20 waves (from 10 enemies at wave 1 up to 30 at wave 20).

Waves 12–14 use **clumped** Armored spawns: all Armored Walkers arrive as a consecutive group after the initial basic wave, creating a mid-wave panic spike instead of a uniform drip.

---

## Maps

Each run generates a new snake-shaped path across an 8×5 grid. Tower slots are placed in grass cells adjacent to the path so every tower is always within shooting range of passing enemies.

---

## Tips

- **Rapid Shooter + Momentum**: Stack Momentum on your fastest-firing tower for massive DPS on tanky enemies that take multiple hits to kill.
- **Marker Tower + Exploit Weakness**: Mark the enemy, then burst it for ×1.8 total (+20% mark × +50% exploit). The Marker Tower frees up your damage towers' modifier slots for pure damage.
- **Heavy Cannon + Overkill**: One-shots weaker enemies and spills damage forward — good for tightly packed groups.
- **Focus Lens** raises your floor if you're already doing enough DPS but need heavier individual hits to finish off enemies before they exit.
- **Targeting mode matters late game** — switch your Marker Tower to *First* so it tags the lead enemy before your damage towers hit it.
