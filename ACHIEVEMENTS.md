# Achievements

## Current Achievements (33)

Tracked locally via `AchievementManager`, persisted to `user://achievements.cfg`. Forwarded to Steam when available.

| ID | Name | Condition | When Checked |
|---|---|---|---|
| `TUTORIAL_COMPLETE` | First Steps | Complete the tutorial run | Tutorial win - `CheckTutorialComplete()` |
| `FIRST_WIN` | First Victory | Complete all 20 waves | Run end (win) |
| `HARD_WIN` | Hard Carry | Complete all 20 waves on Hard | Run end (win) |
| `ARC_UNSEALED` | Arc Unsealed | Beat the first campaign map on any difficulty | Run end (win) - gates Arc Emitter |
| `SPLIT_UNSEALED` | Split Unsealed | Beat the second campaign map on any difficulty | Run end (win) - gates Split Shot |
| `RIFT_UNSEALED` | Rift Unsealed | Beat the third campaign map on any difficulty | Run end (win) - gates Rift Sapper |
| `ACCORDION_UNSEALED` | Accordion Unsealed | Beat the fifth campaign map on any difficulty | Run end (win) - gates Accordion Engine tower |
| `BLAST_UNSEALED` | Blast Unsealed | Beat the fourth campaign map on any difficulty | Run end (win) - gates Blast Core modifier |
| `WILDFIRE_UNSEALED` | Wildfire Unsealed | Beat the sixth campaign map on any difficulty | Run end (win) - gates Wildfire modifier |
| `AFTERIMAGE_UNSEALED` | Afterimage Unsealed | Beat Perimeter Lock on any difficulty | Run end (win) - gates Afterimage modifier |
| `PHASE_UNSEALED` | Phase Unsealed | Beat the seventh campaign map on any difficulty | Run end (win) - gates Phase Splitter tower |
| `REAPER_UNSEALED` | Reaper Unsealed | Beat the eighth campaign map on any difficulty | Run end (win) - gates Reaper Protocol modifier |
| `ROCKET_UNSEALED` | Rocket Unsealed | Beat Hourglass on any difficulty | Run end (win) - gates Rocket Launcher tower |
| `UNDERTOW_UNSEALED` | Undertow Unsealed | Beat Trident on any difficulty | Run end (win) - gates Undertow Engine tower |
| `FLAWLESS` | Flawless | Win without losing a single life | Run end (win) |
| `LAST_STAND` | Last Stand | Win with exactly 1 life remaining | Run end (win) |
| `HALFWAY_THERE` | Halfway There | Survive to wave 10 | Wave 10 start (mid-run) |
| `FULL_HOUSE` | Full House | Fill all 6 tower slots in one run | After draft pick (mid-run) |
| `STACKED` | Stacked | Give any tower 3 modifiers in one run | After draft pick (mid-run) |
| `SPEED_RUN` | Speed Run | Win in under 15 minutes | Run end (win) |
| `ANNIHILATOR` | Annihilator | Deal 100,000 total damage in one run | After wave clear (mid-run) |
| `DEVASTATOR` | Devastator | Deal 200,000 total damage in one run | After wave clear (mid-run) |
| `CHAIN_MASTER` | Chain Master | Win with all 6 slots filled by Arc Emitters | Run end (win) |
| `CHAIN_GANG` | Chain Gang | Place 3 or more Arc Emitters in a single run | After draft pick (mid-run) |
| `FULL_ARSENAL` | Full Arsenal | Use 5 different towers in one run | After draft pick (mid-run) |
| `OVER_EQUIPPED` | Over Equipped | Fill all 18 modifier slots in one run | After draft pick (mid-run) |
| `GLASS_CANNON` | Glass Cannon | Equip Focus Lens and Hair Trigger on the same tower | After draft pick (mid-run) |
| `KEEP_GOING` | Keep Going | Start an endless run | On Continue pressed |
| `ENDLESS_25` | Into the Void | Clear wave 25 in endless mode | After wave clear (endless) |
| `ENDLESS_30` | No End in Sight | Clear wave 30 in endless mode | After wave clear (endless) |
| `ENDLESS_40` | The Abyss | Clear wave 40 in endless mode | After wave clear (endless) |
| `CAMPAIGN_CLEAR` | The Circuit | Clear all four stages of The Fracture Circuit (any difficulty) | Campaign stage win - `HandleCampaignStageWin()` |
| `CAMPAIGN_HARD_CLEAR` | Iron Mandate | Clear all four stages of The Fracture Circuit on Hard difficulty | Campaign stage win - `HandleCampaignStageWin()` |

Achievement icon assets:
- `Assets/Achievements/AFTERIMAGE_UNSEALED.png`
- `Assets/Achievements/AFTERIMAGE_UNSEALED_locked.png`

### Implementation notes

- `CheckRunEndAndCollectUnlocks(state, difficulty, won, isTutorialRun)` - main evaluation call, run end only. Tutorial runs (`isTutorialRun=true`) return immediately with no unlocks - all achievements require a real run except `TUTORIAL_COMPLETE`.
- `HandleCampaignStageWin(difficulty)` in `GameController` - calls `CampaignProgress.MarkCleared`, then checks all stages cleared for `CAMPAIGN_CLEAR` (any difficulty) and `CAMPAIGN_HARD_CLEAR` (all on Hard). Checked after every campaign stage win, not just the final one.
- `CheckTutorialComplete()` - called from GameController on tutorial win (before `CheckRunEndAndCollectUnlocks`)
- `CheckHalfwayThere()` - called at wave 10 start from `GameController`
- `CheckDraftMilestones(state)` - called after each draft pick; covers `FULL_HOUSE`, `STACKED`, `FULL_ARSENAL`, `OVER_EQUIPPED`, `CHAIN_GANG`, `GLASS_CANNON` (checks arc emitter count as towers are placed)
- `CheckAnnihilator(state)` - called after each wave clear; covers `ANNIHILATOR` and `DEVASTATOR`
- `CheckEndlessMilestones(state)` - called after each endless wave clear; covers `ENDLESS_25/30/40`
- `CheckKeepGoing()` - called in `OnContinueEndlessPressed`
- Bot mode (`--bot`) skips all unlock evaluation
