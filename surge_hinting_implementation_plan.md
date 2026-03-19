# Surge Teaching / Hinting Implementation Plan

## Goal
Reduce player confusion around the Surge system **without relying on tutorials, popups, or settings discovery**.

The plan is to teach Surge through normal play using:
- stronger passive visual communication
- contextual micro-hints near the relevant UI
- failure-driven reminders after missed usage
- adaptive retirement logic so hints stop once the player clearly gets it

This is meant to preserve flow, avoid tutorial friction, and prevent repetitive annoyance.

---

## Problem Summary
Current issue is not simply that Surge is poorly documented.

The actual problem is:
- many players skip tutorials
- many players dismiss popups automatically
- many players do not open settings before quitting
- the existing explanation is too text-dependent relative to how players actually behave
- players often see Surge effects without forming a clear causal model for:
  - what builds tower surge
  - what makes a tower surge-ready
  - how full towers contribute to global surge
  - when and why global surge should be activated

So the solution must assume the player is impatient, half-distracted, and not volunteering to learn.

---

## Design Principles

### 1. No mandatory reading
Any critical understanding should be possible through ordinary play and moment-to-moment UI feedback.

### 2. No modal interruption by default
Avoid large popups, blocking overlays, or explanation-first flows unless used very selectively for scripted tutorial content.

### 3. Teach at point of relevance
Only explain Surge when the player is already looking at the tower, ring, or global bar.

### 4. Use very short language
Hints should be one line whenever possible. No paragraphs.

### 5. Auto-retire hints
Hints should disappear permanently for players who demonstrate understanding.

### 6. Failure should teach
If the player ignores Surge and loses, the game should surface that missed opportunity in one clean sentence.

---

## Target Player Understanding
After a few runs, the average player should understand these points:

1. **Combat fills Tower Surge**
2. **Tower Surge readiness is shown on the tower ring**
3. **Full towers contribute to the Global Surge bar**
4. **Global Surge is a manually activated resource**
5. **2+ mods unlock more meaningful combo surge behavior**

The player does not need every detail immediately. They need the core causal ladder.

---

## System Overview
Implement Surge teaching as a four-layer system:

### Layer 1 — Passive Visual Truth
Make important Surge states visually obvious without any text.

### Layer 2 — Contextual Micro-Hints
Show tiny hints attached to the relevant tower or surge bar only when relevant.

### Layer 3 — Failure / Missed-Opportunity Reminders
After ignoring Surge or losing with Surge available, show a one-line tip tied to what was missed.

### Layer 4 — Optional Reference Material
Keep the tutorial map and how-to-play screen, but treat them as supplemental rather than primary teaching.

---

## Implementation Phases

# Phase 1 — Instrumentation and State Tracking
Before adding hint logic, track the behaviors needed to decide when hints should appear and retire.

## Add Per-Profile or Per-Save Flags
Track whether the player has:
- seen first Surge gain hint
- seen first Tower Surge ready hint
- seen first Global Surge hint
- seen combo Surge hint
- activated Global Surge at least once
- activated Global Surge at least twice
- had Global Surge available but unused
- lost a run with Global Surge available
- hovered or focused the Surge bar (if hover/focus exists)

## Add Run-Level Counters
Track per run:
- number of towers that reached Surge-ready
- number of times global surge became available
- number of times global surge was activated
- total time global surge sat ready but unused
- whether player used any combo-mod towers
- whether any 2+ mod towers generated surge-ready states

## Add Simple Derived Understanding Heuristics
Use these as hint retirement signals:
- player has activated Global Surge 2+ times total
- player has activated Global Surge within 10 seconds of readiness multiple times
- player is regularly building 2+ mod towers
- player wins runs while using Surge

These do not need to be perfect. They just need to be good enough.

### Deliverables
- data fields added
- events fired for surge milestones
- save persistence for hint state
- debug logging or overlay for Surge teaching telemetry

---

# Phase 2 — Passive Visual Communication Improvements
Improve the non-text readability of Surge.

## Tower Surge Ring Readability
Make sure the tower ring clearly communicates:
- charging state
- near-full state
- full / ready state

### Possible Improvements
- stronger fill contrast
- clearer fill direction or segmentation
- subtle pulse when charge increases
- stronger glow when full
- brief ring flash on meaningful gain events

## Global Surge Bar Readability
Make the global bar clearly communicate:
- that it exists
- when it is filling
- when it is ready
- when the player should care

### Possible Improvements
- subtle pulse when a tower contributes to it
- stronger ready-state animation
- readiness shimmer / edge flash
- slight idle motion only when ready and unused

## Combo Identity Communication
When a tower has 2+ mods, make it clearer that this tower is now surge-relevant.

### Possible Improvements
- stronger tower accent treatment
- combo color identity on ring
- small icon or badge indicating combo-enabled surge behavior

### Deliverables
- updated ring visuals
- updated global bar visuals
- ready-state emphasis pass
- combo-enabled tower visual differentiation

---

# Phase 3 — Contextual Micro-Hint System
Build a lightweight hint system that attaches tiny messages to the relevant object.

## Rules for Micro-Hints
- one line max
- non-modal
- auto-fade after a short duration
- anchored to tower ring or global bar
- only shown when relevant
- rate-limited to avoid spam

## Candidate Hint Messages
Use short, operational language.

### Tower Charging Hint
**Combat fills Surge**

Trigger:
- first time a 2+ mod tower starts generating meaningful surge gain
- only if player has not yet shown basic understanding

### Tower Ready Hint
**Tower Surge Ready**

Trigger:
- first time any tower becomes full
- can repeat a limited number of times if player repeatedly ignores it

### Global Bar Contribution Hint
**Full towers power this**

Trigger:
- first time a full tower contributes to the global bar
- anchor to global bar

### Global Activation Hint
**Click to trigger Global Surge**

Trigger:
- global bar is full or usable
- player has not used it before
- delay a few seconds so it does not feel too eager

### Combo Identity Hint
**2+ mods unlock combo Surge**

Trigger:
- first time a tower reaches 2 mods
- only once unless player never engages with combo towers again after many runs

## Rate Limiting
Per hint type:
- cooldown during a run
- hard cap per run
- hard cap lifetime before retirement

Suggested initial caps:
- charging hint: max 2 lifetime
- ready hint: max 4 lifetime
- global contribution hint: max 2 lifetime
- activation hint: max 5 lifetime
- combo hint: max 2 lifetime

### Deliverables
- hint anchor system
- hint display component
- trigger conditions per hint type
- cooldown and repetition limits

---

# Phase 4 — Adaptive Retirement Logic
Hints should stop once the player likely understands the system.

## Retirement Conditions
Retire most basic Surge hints once any of the following are true:
- player has activated Global Surge 2 times total
- player has activated Global Surge in 2 separate runs
- player has used Surge successfully after it became available without delay
- player has won at least 1 run while using Surge

Retire combo-specific hints once:
- player has built multiple 2+ mod towers across runs
- player has triggered several combo surge states

## Partial Retirement
Not all hints need to retire together.

Example:
- retire “Combat fills Surge” early
- keep “Click to trigger Global Surge” until actual activation behavior is learned

## Re-Enable Conditions
Normally do not re-enable retired hints.

Possible exception:
- if a major update changes Surge behavior significantly
- if user resets tutorials / hints manually

### Deliverables
- retirement state machine
- per-hint completion flags
- manual reset option in settings

---

# Phase 5 — Failure-Driven Reminder Layer
Teach the mechanic when the player has a reason to care.

## Post-Loss Tip Conditions
Show a Surge-related reminder only when directly relevant.

### Example Triggers
- player lost while global surge was available but unused
- player had multiple towers reach ready state but never activated global surge
- player built combo-capable towers but did not use the mechanic effectively

## Example Post-Loss Tips
Keep these short and specific.

- **Tip: full modded towers charge your Global Surge.**
- **Tip: Global Surge was available this run — click the bar to trigger it.**
- **Tip: Combat fills Tower Surge over time.**
- **Tip: 2+ mod towers unlock stronger combo surges.**

## Selection Logic
Only show one tip at a time.
Prefer the most specific relevant missed opportunity.
Do not repeat the same tip too many times in a row.

### Deliverables
- post-run analysis pass
- ranked tip selection logic
- minimal result-screen tip UI

---

# Phase 6 — Wording and UI Label Pass
Audit whether current naming helps or hurts comprehension.

## Review Current Terms
Check whether these are sufficiently self-explanatory in context:
- Surge
n- Tower Surge
- Global Surge
- Surge-ready
- combo surge

## Potential UI Support Labels
Do not necessarily rename the whole system yet, but consider slightly more descriptive UI copy:
- **Tower Surge Charge**
- **Global Surge Power**
- **2+ mods unlock combo Surge**
- **Full towers charge Global Surge**

Goal is not verbosity. Goal is reducing abstractness.

### Deliverables
- wording audit
- revised tooltip / hover text pass
- optional updated UI labels where useful

---

# Phase 7 — Tutorial / How-to-Play Repositioning
Keep existing tutorial material, but demote it from “primary teaching method” to “reinforcement layer.”

## Tutorial Design Change
Do not assume the player will learn Surge there.
Instead make sure tutorial text:
- matches in-game terminology exactly
- uses same visual language as the actual UI
- reinforces the same short causal chain already taught in play

## How-to-Play Revision
Compress around the causal ladder:
1. Combat fills Tower Surge
2. Full towers charge Global Surge
3. Click Global Surge when ready
4. 2+ mods unlock stronger combo effects

Use visuals if possible rather than dense explanation.

### Deliverables
- wording pass on tutorial map
- wording pass on how-to-play entry
- terminology sync with in-game micro-hints

---

## Suggested Exact Trigger Spec

### Hint: Combat fills Surge
**Trigger**
- player owns a tower with 2+ mods
- tower gains surge for the first meaningful time
- player has not activated Global Surge before
- hint not shown this run

**Display**
- anchor near the tower ring
- 1.5–2.0 seconds

**Retire when**
- shown twice lifetime
- or player activates Global Surge once

---

### Hint: Tower Surge Ready
**Trigger**
- any tower reaches full surge
- player has not yet shown readiness understanding
- global ready hint not currently showing

**Display**
- anchor near the tower
- stronger ring glow during display

**Retire when**
- player has seen it a few times and activated surge successfully

---

### Hint: Full towers power this
**Trigger**
- first tower contribution to global bar occurs
- player has not yet seen the bar contribution relationship

**Display**
- anchor near global surge bar
- brief pulse on the bar

**Retire when**
- shown twice lifetime
- or player activates Global Surge once

---

### Hint: Click to trigger Global Surge
**Trigger**
- global surge becomes available
- player has not activated it before or has repeatedly left it unused
- 2–4 second delay after readiness

**Display**
- anchor near global bar
- can repeat with long cooldown while bar remains unused

**Retire when**
- player activates Global Surge twice across separate runs

---

### Hint: 2+ mods unlock combo Surge
**Trigger**
- first time a tower reaches 2 mods
- player has not yet used combo-capable builds meaningfully

**Display**
- anchor near tower or mod UI

**Retire when**
- shown twice lifetime
- or player creates multiple 2+ mod towers across runs

---

## Engineering Tasks

### Data / State
- add hint state persistence
- add surge telemetry events
- add run summary stats for unused surge opportunities
- add per-hint cooldowns and lifetime counters

### UI
- add anchored micro-hint component
- add ring pulse / ready-state animations
- add global bar pulse / ready-state emphasis
- add result-screen tip slot

### Logic
- add hint trigger evaluator
- add retirement evaluator
- add post-run missed-opportunity analyzer
- add anti-repeat / prioritization logic

### Design / Content
- write final micro-hint copy
- write post-loss tips
- revise tutorial map text
- revise how-to-play text

---

## Recommended Rollout Order

### Milestone 1 — Telemetry Foundation
Implement tracking first so later testing is grounded.

### Milestone 2 — Visual Readability Pass
Improve ring and global bar clarity before adding more text.

### Milestone 3 — Core Micro-Hints
Implement the four essential hints:
- Combat fills Surge
- Tower Surge Ready
- Full towers power this
- Click to trigger Global Surge

### Milestone 4 — Post-Loss Tips
Add missed-opportunity reminders.

### Milestone 5 — Adaptive Retirement
Prevent long-term annoyance.

### Milestone 6 — Tutorial / How-to-Play Cleanup
Align reference material with the new in-game teaching language.

---

## Testing Plan

## Qualitative Testing
Watch a few fresh players and check:
- do they notice tower rings filling
- do they understand that combat causes filling
- do they connect full towers to the global bar
- do they activate global surge without external explanation
- do hints feel helpful rather than nagging

## Quantitative Signals
Track before/after:
- % of runs where players activate Global Surge at least once
- average delay between global readiness and activation
- % of runs where surge was available but never used
- % of players who reach second run without activating surge
- % of players who use 2+ mod towers and still never trigger surge

## Success Criteria
This system is working if:
- more players activate Global Surge naturally
- fewer players describe Surge as mysterious
- players can describe basic cause/effect after playing only a little
- hint annoyance remains low

---

## Risks and Failure Modes

### Risk 1 — Hint spam
If triggers are too eager, players tune them out immediately.

**Mitigation:** strict cooldowns, caps, retirement logic.

### Risk 2 — Visual overload
If surge emphasis is too flashy, it muddies combat readability.

**Mitigation:** emphasize only relevant states, not constant animation.

### Risk 3 — Still too abstract
If wording stays too vague, players still fail to build the mental model.

**Mitigation:** prefer operational phrases tied to visible objects and actions.

### Risk 4 — Players understand activation but not combo logic
Basic surge may become clear while combo interactions remain fuzzy.

**Mitigation:** treat combo surge as a second-stage lesson, not day-one required knowledge.

---

## Final Implementation Intent
The point is not to make players read better.
The point is to make Surge understandable even when players behave like impatient goblins.

That means:
- less dependence on tutorial compliance
- more causality visible in live play
- hints only when context makes them relevant
- immediate shutdown of hints once the player demonstrates understanding

The system should feel like the game quietly teaching itself, not the game nagging for attention.