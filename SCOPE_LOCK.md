# SCOPE_LOCK.md

This is a contract. It does not get updated to accommodate new ideas.

---

## What is frozen

**No new systems.**

**No new UI panels** beyond what is needed to ship.

**No new tower types** beyond the implemented set (5 towers: Rapid Shooter, Heavy Cannon, Marker Tower, Arc Emitter, Rift Sapper).

**No new modifiers** beyond the implemented pool (10 modifiers: Momentum, Overkill, Exploit Weakness, Focus Lens, Chill Shot, Overreach, Hair Trigger, Split Shot, Feedback Loop, Chain Reaction).

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

### 1. Core pacing ✅ COMPLETED

- Target run length: 15–25 minutes → ~30–60s per wave
- Tuned: enemy base HP, HP growth per wave, spawn rate/quota
- Added Normal/Hard difficulty modes with balanced multipliers
- Goal achieved: wave 1–3 = learn, wave 6–12 = build identity, wave 16–20 = clench

### 2. Draft readability ✅ COMPLETED

- Cards show short name + one-line effect
- Modifier assignment: highlight eligible towers, block invalid clicks, show mod count
- After pick: one obvious transition into wave
- Clear placement hints and world-click system implemented

### 3. Functional polish ✅ COMPLETED

- Pause (Esc) ✅
- Restart run (from pause and from loss screen) ✅
- Game speed toggle (1×/2×/3×) ✅
- Audio: hit, kill, leak, draft pick ✅
- Settings: master/music/FX volume, fullscreen/window ✅
- Quit button ✅

### 4. Hardening ✅ COMPLETED

Murder pass completed:
- Edge case handling implemented ✅
- Memory leak fixes for long-running tests ✅
- Bot testing framework validates stability ✅
- UI state management robust ✅

No crashes. No softlocks. No state corruption.

### 5. Balance pass ✅ COMPLETED

Extensive balance testing completed:
- 200+ bot runs across multiple strategies ✅
- Wave difficulty tuning for progression ✅
- Normal/Hard difficulty modes balanced ✅
- Win rates stabilized at healthy levels ✅

### 6. Steam pipeline ✅ COMPLETE

- Export system working (Windows builds) ✅
- Steam pipeline documentation complete ✅
- Steamworks account, depots, SteamCMD configured ✅
- Store page submitted for review (2026-03-15) ✅
- Achievements, Cloud, Leaderboards wired ✅
- Valve review in progress

---

## The rule

If you are about to add something not on this list, you are procrastinating on shipping.

Close the file and go back to the list.
