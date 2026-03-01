# SCOPE_LOCK.md

This is a contract. It does not get updated to accommodate new ideas.

---

## What is frozen

**No new systems.**

**No new UI panels** beyond what is needed to ship.

**No new tower types** beyond the planned vertical slice set.

**No new modifiers** beyond the initial pool (4 modifiers: Momentum, Overkill, ExploitWeakness, FocusLens).

---

## What is allowed

Every change from this point must be one of:

- **Bugfix** — something is broken
- **Clarity / readability** — code or UI is confusing
- **Balance / tuning** — numbers, pacing, feel
- **Shipping / pipeline** — build, Steam, packaging

If a change does not fit one of those four categories, it does not happen.

---

## Remaining work (in priority order)

### 1. Core pacing

- Target run length: 15–25 minutes → ~30–60s per wave
- Tune exactly 3 knobs: enemy base HP, HP growth per wave, spawn rate/quota
- Nothing else changes while tuning
- Goal: wave 1–3 = learn, wave 6–12 = build identity, wave 16–20 = clench

### 2. Draft readability

- Cards show short name + one-line effect
- Modifier assignment: highlight eligible towers, block invalid clicks, show mod count
- After pick: one obvious transition into wave
- Nobody asks "what do I do now?"

### 3. Functional polish

- Pause (Esc)
- Restart run (from pause and from loss screen)
- Game speed toggle (optional but high value for tuning)
- Audio: hit, kill, leak, draft pick (placeholder sounds are fine)
- Settings: master volume, fullscreen/window
- Quit button

### 4. Hardening

Murder pass — test every edge case:
- Spam-click draft options
- Apply modifier with no towers / full towers
- Multiple enemies leak at once
- Low FPS behavior (projectile/desync)
- Alt-tab during wave
- Window mode change mid-run

No crashes. No softlocks. No state corruption.

### 5. Balance pass

Play 10 runs. After each run, write one sentence:
- "Lost because X felt unfair/unclear"
- "Won because Y snowballed too hard"

Change one variable at a time. Stop when it feels fair and replayable.

### 6. Steam pipeline

- Create Steam app entry
- Set up depots/branches
- Push a private build
- Start store page (placeholder capsules are fine)

Steam review runs in parallel with tuning. Do not postpone this.

---

## The rule

If you are about to add something not on this list, you are procrastinating on shipping.

Close the file and go back to the list.
