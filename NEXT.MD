# NEXT.MD - Campaign Framing & Onboarding Fantasy

Two issues to fix before the game feels like it has gravity.

---

## Issue 1: Stronger Campaign / Progression Framing

**The problem:** The player has unlocks, achievements, and a map select screen - but the structure doesn't communicate "you're on a journey." There's no sense of a world, no chapter arc, no "what's next after I beat this."

### What's already there (underused)
- Map select with 3 maps (Easy/Normal/Hard difficulty ladder)
- Unlocks: Arc Emitter, Split Shot, Rift Prism gated behind map clears
- 9+ achievements
- Personal best scores per map
- Endless mode after wave 20

### The gap
None of these pieces say *why* they exist in the same universe. The player sees three identical-looking buttons with different names. The unlock reveal screen fires once and is forgotten. There's no narrative hook that makes "clear the next map" feel like pushing into new territory.

---

### Plan

#### 1-A: Map Select - Chapter Arc UI
Give each map a subtitle and a one-line flavor brief that implies progression:

| Map | Current | Add |
|-----|---------|-----|
| Ironvale Basin | Easy | *"Sector 1 - establish your defenses"* |
| Ashfield Ridge | Normal | *"Sector 2 - the formations are heavier here"* |
| Terminus Core | Hard | *"Sector 3 - the final push"* |

**Implementation:** Add a subtitle `Label` below the map name in `MapSelectPanel`. Populate from `maps.json` - add an optional `"subtitle"` and `"flavor"` field. Single-line flavor text, 8–12 words max. No lore dump.

#### 1-B: Lock State Communicates Stakes
Currently a locked map is just... locked. Change the locked state to show:
- Lock icon with label: *"Clear [previous map] to unlock"*
- Optionally: ghost/dim the map card instead of hiding it

This turns the lock into forward pointer instead of a dead end.

**Implementation:** `MapSelectPanel` already reads map unlock state. Add a `Label` on locked cards that names the prerequisite.

#### 1-C: Unlock Reveal - Name What You Got and Why It Matters
The `UnlockRevealScreen` currently shows the tower/modifier name. Add a single punchy line that frames the unlock as a power escalation:

- *"Arc Emitter - chain lightning, now in your arsenal"*
- *"Split Shot - double the angles, double the pressure"*
- *"Rift Prism - stop enemies before they start"*

**Implementation:** Add an `"unlock_line"` field to the tower/modifier data or hardcode in `UnlockRevealScreen.cs`. One sentence, active voice.

#### 1-D: Post-Full-Unlock Forward Pointer
Once all 3 maps are cleared and all unlocks are active, the player has no obvious "next thing." Options (pick one):

- **Endless as the destination:** After clearing Terminus Core, show a beat on the map select: *"All sectors cleared. The run doesn't have to end - Endless mode active."* Make endless feel earned, not incidental.
- **Score ladder as the new goal:** Show the global leaderboard ranking on the map select after full clear, with a nudge: *"Your best: #XX - can you push higher?"*

**Implementation:** `MapSelectPanel` - detect all-maps-cleared state and conditionally show a banner node.

#### 1-E: Achievements Panel - Cluster by Arc
Currently achievements are a flat list. Group them visually into three clusters:

- **Campaign** (clear maps, first win, etc.)
- **Mastery** (no-damage waves, high scores, etc.)
- **Discovery** (try all towers, find combos, etc.)

This makes the achievements panel feel like a progression tree, not a trophy shelf.

**Implementation:** Add a `"group"` field to `AchievementDefinition`. Section headers in `AchievementsPanel`.

---

## Issue 2: Better Immediate Onboarding Fantasy

**The problem:** The game currently teaches before it sells. The first minute is explanation. What's missing is the 10-second "oh, THIS is what this game is" moment before the player reads anything.

A good onboarding fantasy makes the player feel the game's identity before they understand its mechanics. Right now the tutorial tells the player what to do. It should instead let the game speak for itself first.

### The goal
The first minute should feel like: *watch something cool happen → feel the first choice → understand it only after you've already made it.*

---

### Plan

#### 2-A: Opening Beat - Stakes Before Explanation
On first run (IsFirstRun), before the first draft panel opens, show a 2-second atmospheric beat:

- Enemies begin moving down the lane (no towers yet)
- Camera or vignette effect implies "incoming"
- Then DraftPanel slides in: *"Place your first defense."*

No text, no instructions. Just: danger incoming → you have choices → pick one.

**Implementation:** In `GameController`, on first run before first draft, spawn a small enemy group at `Progress = 0`, let them walk 1–2 seconds, then pause and open draft. Enemies don't reach the end - this is purely visual. Or simpler: just a 1.5s delay with ambient enemy audio before the first draft opens.

#### 2-B: First Pick Ceremony - Make the First Draft Feel Like Power
The first draft panel currently looks identical to all subsequent ones. On wave 1 only:

- Animate the cards in with a slight stagger (150ms between cards)
- The panel title on wave 1: *"Choose your first defender"* instead of *"Draft - Wave 1"*
- After the pick, brief flash or sound cue: *tower slam-places with more emphasis*

This isn't a tutorial moment. It's a "you just made your first move" moment. The game acknowledges the pick.

**Implementation:** `DraftPanel` - detect wave index 0, add stagger animation via `Tween` on card entry. `GameController.PlaceTower()` - add an extra visual/sound beat on first placement. Cost: ~30 lines.

#### 2-C: Wave 1 - Let It Breathe (Don't Narrate It)
Currently the surge tutorial panel fires on wave 3. Consider: wave 1 has no interruption, no explanation. Just enemies, your tower, and what happens. Let the player watch their choice fight.

The tutorial for surges fires *after* the player has seen surges happen naturally (or nearly so). The current surge-on-wave-3 timing is good. Keep it.

What to avoid: adding more explanation to wave 1. Let wave 1 be the demo reel.

#### 2-D: HowToPlay - Defer, Don't Eliminate
The HowToPlay panel is currently accessible from the main menu. Keep it. But don't surface it proactively in the first-run flow.

Instead of "here's how to play before you start," the first-run experience should be:
1. Start → see danger → make first pick (2-A, 2-B)
2. Wave runs - player observes
3. Second draft → player is already contextually oriented
4. Surge tutorial fires (wave 3, current behavior - keep)
5. After run ends → surface "View full guide" link on end screen for first-run only

This way the manual is pull, not push.

**Implementation:** `EndScreen` - on first run (RunsStarted == 1), add a small `Button` or label: *"First time? View the full guide →"* that opens HowToPlay overlay.

#### 2-E: Main Menu - Sell the Loop in One Line
The main menu currently shows the game title. Add a single tagline that captures the feel:

> *"Draft. Defend. Survive 20 waves."*

Or shorter:

> *"20 waves. One pick at a time."*

No paragraph. One line. This is the pitch.

**Implementation:** `MainMenu.cs` - add a `Label` node below the title with tagline text. Style it smaller, lower opacity than the title.

---

## Execution Order

These are sequenced by impact-to-effort ratio. Do them in this order:

1. **2-E** - tagline on main menu (15 min, high first-impression ROI)
2. **1-A + 1-B** - map subtitle + locked-card forward pointer (1–2h, immediately makes map select feel like a world)
3. **1-C** - unlock reveal flavor line (30 min)
4. **2-B** - first pick ceremony / card stagger (1–2h)
5. **2-D** - end screen "view guide" for first run (30 min)
6. **1-D** - post-full-unlock forward pointer (1h)
7. **2-A** - opening beat before first draft (2–3h, most risk, do last)
8. **1-E** - achievements grouping (2h, polish, not urgent)

---

## What NOT to do

- No narrator, no NPC dialogue, no story cutscenes - the game is mechanical, keep it that way
- No tutorial prompts pointing at UI elements - players click through these; trust implicit learning
- No lore document or worldbuilding text - flavor is one line max, not backstory
- Don't gate the first run behind a "read this first" screen
