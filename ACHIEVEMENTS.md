# Achievements

## Current Achievements (13)

Tracked locally via `AchievementManager`, persisted to `user://achievements.cfg`. Forwarded to Steam when available.

| ID | Name | Condition | When Checked |
|---|---|---|---|
| `FIRST_WIN` | First Victory | Complete all 20 waves | Run end (win) |
| `HARD_WIN` | Hard Carry | Complete all 20 waves on Hard | Run end (win) |
| `FLAWLESS` | Flawless | Win without losing a single life | Run end (win) |
| `LAST_STAND` | Last Stand | Win with exactly 1 life remaining | Run end (win) |
| `HALFWAY_THERE` | Halfway There | Survive to wave 10 | Wave 10 start (mid-run) |
| `FULL_HOUSE` | Full House | Fill all 6 tower slots in one run | After draft pick (mid-run) |
| `STACKED` | Stacked | Give any tower 3 modifiers in one run | After draft pick (mid-run) |
| `SPEED_RUN` | Speed Run | Win in under 15 minutes | Run end (win) |
| `ANNIHILATOR` | Annihilator | Deal 100,000 total damage in one run | After wave clear (mid-run) |
| `CHAIN_MASTER` | Chain Master | Win with all 6 slots filled by Arc Emitters | Run end (win) |
| `ARC_UNSEALED` | Arc Unsealed | Beat the first campaign map on Normal or Hard | Run end (win) - gates Arc Emitter |
| `SPLIT_UNSEALED` | Split Unsealed | Beat the second campaign map on Normal or Hard | Run end (win) - gates Split Shot |
| `RIFT_UNSEALED` | Rift Unsealed | Beat the third campaign map on Normal or Hard | Run end (win) - gates Rift Sapper |

### Implementation notes

- `CheckRunEndAndCollectUnlocks(state, difficulty, won)` - main evaluation call, run end only
- `CheckHalfwayThere()` - called at wave 10 start from `GameController`
- `CheckDraftMilestones(state)` - called after each draft pick; covers `FULL_HOUSE` and `STACKED`
- `CheckAnnihilator(state)` - called after each wave clear
- Bot mode (`--bot`) skips all unlock evaluation

---

## Planned Achievements

### Damage / Combat

| ID | Name | Condition | Notes |
|---|---|---|---|
| `DEVASTATOR` | Devastator | Deal 200,000 total damage in one run | Upgrade tier above `ANNIHILATOR`. Check mid-run in `CheckAnnihilator`-style hook and at run end. |

### Endless

| ID | Name | Condition | Notes |
|---|---|---|---|
| `KEEP_GOING` | Keep Going | Start an endless run | Trigger immediately in `OnContinueEndlessPressed` in `GameController`, before `StartDraftPhase`. |
| `ENDLESS_25` | Into the Void | Clear wave 25 (endless depth 5) | Check at wave clear when `state.WaveIndex + 1 >= 25` and `state.IsEndlessMode`. |
| `ENDLESS_30` | No End in Sight | Clear wave 30 (endless depth 10) | Check at wave clear when `state.WaveIndex + 1 >= 30` and `state.IsEndlessMode`. |
| `ENDLESS_40` | The Abyss | Clear wave 40 (endless depth 20) | Prestige-tier stretch goal. Same pattern as above. |

### Build / Loadout

| ID | Name | Condition | Notes |
|---|---|---|---|
| `FULL_ARSENAL` | Full Arsenal | Use all 5 tower types in one run | Check at run end: all 5 tower IDs (`rapid_shooter`, `heavy_cannon`, `marker_tower`, `chain_tower`, `rift_prism`) must appear in at least one slot. |
| `OVER_EQUIPPED` | Over Equipped | Fill all 18 modifier slots in one run | Check at run end: sum of `Modifiers.Count` across all occupied slots >= 18. |
| `CHAIN_GANG` | Chain Gang | Win with 3 or more Arc Emitters placed | Stepping stone toward `CHAIN_MASTER`. Check at run end (win only): `chain_tower` count in filled slots >= 3. |
| `GLASS_CANNON` | Glass Cannon | Win with Focus Lens and Hair Trigger on the same tower | Check at run end (win only): any slot has both `focus_lens` and `hair_trigger` in its modifier list. |

---

## Implementation Plan

### New constants (add to `AchievementManager`)

```csharp
private const int  DevastatorDamage     = 200_000;
private const int  EndlessWave25        = 24;   // 0-based WaveIndex
private const int  EndlessWave30        = 29;
private const int  EndlessWave40        = 39;
private const int  OverEquippedMinMods  = 18;
private const int  ChainGangMinArcs     = 3;
```

### New definitions (add to `All` array)

```csharp
new("DEVASTATOR",    "Devastator",       "Deal 200,000 total damage in a single run."),
new("KEEP_GOING",    "Keep Going",       "Start an endless run."),
new("ENDLESS_25",    "Into the Void",    "Clear wave 25 in endless mode."),
new("ENDLESS_30",    "No End in Sight",  "Clear wave 30 in endless mode."),
new("ENDLESS_40",    "The Abyss",        "Clear wave 40 in endless mode."),
new("FULL_ARSENAL",  "Full Arsenal",     "Use all 5 tower types in a single run."),
new("OVER_EQUIPPED", "Over Equipped",    "Fill all 18 modifier slots in a single run."),
new("CHAIN_GANG",    "Chain Gang",       "Win with 3 or more Arc Emitters placed."),
new("GLASS_CANNON",  "Glass Cannon",     "Win with Focus Lens and Hair Trigger on the same tower."),
```

### New check hooks needed

| Hook | Where to call |
|---|---|
| `CheckDevastator(state)` | After each wave clear (same pattern as `CheckAnnihilator`) |
| `CheckEndlessMilestones(state)` | After each endless wave clear in `GameController` |
| `CheckKeepGoing()` | In `GameController.OnContinueEndlessPressed`, before `StartDraftPhase` |

`FULL_ARSENAL`, `OVER_EQUIPPED`, `CHAIN_GANG`, `GLASS_CANNON` are run-end checks - add to `CheckRunEndAndCollectUnlocks`.

### Goal hints to add to `GetGoalHint`

- `DEVASTATOR` near-miss: damage >= 140,000 and < 200,000
- `ENDLESS_25` / `ENDLESS_30` / `ENDLESS_40` forward goals when endless active
- `CHAIN_GANG` near-miss: won with 2 Arc Emitters (1 away)
