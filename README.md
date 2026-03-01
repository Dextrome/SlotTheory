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
| Assign to a slot / tower | Left-click the target button |
| Cycle tower targeting mode | Left-click a tower during a wave |
| Pause / unpause | **Esc** |
| Speed up / slow down | Speed buttons in the HUD (×1, ×2, ×4) |
| Quit to main menu | Pause → Main Menu |

---

## Draft Cards

Each draft round you see **5 cards**. Which cards appear depends on your situation:

- **Free slots available** → 2 tower cards + 3 modifier cards
- **All slots full** → 5 modifier cards (no new towers to place)

### Tower Cards

Towers go into one of the 6 numbered slots on the map. Empty slots are shown in the slot-picker after you choose a tower card. You cannot move or sell a placed tower.

### Modifier Cards

Modifiers upgrade an existing tower. After picking a modifier card, choose which tower to assign it to. Each tower can hold up to **3 modifiers**. Fully upgraded towers are greyed out in the slot picker.

If no draft options are possible (all slots full and all towers at modifier cap), the draft is skipped and the wave starts immediately.

---

## Towers

| Tower | Color | Damage | Attack Speed | Range | Notes |
|---|---|---|---|---|---|
| **Rapid Shooter** | Sky blue | 10 | 0.4 s | 300 px | High rate of fire, low damage per hit |
| **Heavy Cannon** | Navy blue | 60 | 2.0 s | 250 px | Slow but hits hard |
| **Marker Tower** | Violet | 5 | 1.0 s | 350 px | Applies **Mark** on every hit |

### Targeting Modes

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

---

## Marked

When a **Marker Tower** hits an enemy it applies the **Mark** status for 2 seconds.
While Marked, that enemy takes **+20% damage from all towers** (before any modifier bonuses).

Pair Marker Tower + **Exploit Weakness** on another tower for a large burst damage combo.

---

## Enemies

One enemy type in v1: the **Basic Walker**.

- Travels along the procedurally generated path at 120 px/s.
- Starts with 72 HP on wave 1.
- HP scales by ×1.12 each wave (~×9 by wave 20).
- Wave 20 walkers have approximately 640 HP.

Enemy count and spawn speed also increase over 20 waves (from 10 enemies at 1 per 2.5 s up to 30 enemies at 1 per 1.1 s).

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
