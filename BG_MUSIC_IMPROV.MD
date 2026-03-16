# BG_MUSIC_IMPROV.MD
## Procedurally Improvised Background Music - Design & Implementation Plan

---

## Goal

Replace the static precomputed ambient loop with a system that **composes in real-time**, reacting to game state (wave progress, lives, surge events) to create music that is never identical between runs. The feel should be something between ambient electronic and lo-fi jazz - harmonically grounded but loose and alive.

---

## Approach: Synthesized Notes (Path A)

All notes are synthesized at startup via PCM - the same technique already used for SFX in `SoundManager`. No audio files required. The improvisation value comes from **structure** (scale selection, chord progressions, phrase contours, rhythmic density), not sample quality.

This is a deliberate choice over the alternative of loading recorded samples:
- Consistent with the existing all-synthesized audio identity
- Avoids asset production dependency
- Startup synthesis is ~50–100ms total, acceptable alongside existing SFX synthesis
- Timbre can be refined in Phase 5 alongside balance tuning

Note IDs follow the pattern `mnote_{midiNote}` (e.g. `mnote_45` = A2).

---

## Design Philosophy

- **Phrase-based, not sample-based.** The system thinks in 2–4 bar musical phrases. It plans a phrase, plays it, then decides what comes next based on current game state.
- **Three layers.** Bass line (harmonic anchor), melody (improvised lead), percussion (rhythm grid). Each layer can be enabled/disabled independently. Game state affects which layers are active and how "busy" they are.
- **Game-reactive, not game-driven.** Music responds to state transitions (wave start, surge trigger, near-death), but never lurches abruptly. Transitions queue at the next phrase boundary.
- **Coexists with the ambient pad.** The precomputed 32-second pad loop continues running during development. The procedural layers layer on top. The pad is faded out in Phase 5 when the full system is validated.

---

## Musical Framework

### Scales
Three modal palettes - one per emotional zone:

| Mode | Use | Character |
|---|---|---|
| Dorian | Neutral / early waves | Mellow, slightly melancholic |
| Mixolydian | Mid-game / building tension | Driving, confident |
| Phrygian | Late waves / low lives | Dark, tense |

Root note: A. Fixed per run (per-map root note variation is an open question, see below).

### Chord Progression
A looping 4-bar progression anchors the bass and constrains melody choices. The system picks from a small pool of progressions per mode (3–4 options), selects one at run start, and stays on it. Mid-run shifts happen only at structural phrase boundaries (every 8 bars).

Example Dorian progression: `i – IV – i – VII` (Am – Dm – Am – Gm)

### Rhythm Grid
16th-note grid, 4/4 time. BPM is fixed per tension level:

| State | BPM |
|---|---|
| Early waves (1–7) | 72 |
| Mid waves (8–14) | 88 |
| Late waves (15–20) | 96 |
| Near-death (≤3 lives) | Stays at current BPM but rhythmic density drops - sparser, more space |

BPM changes are gradual: `MusicClock.SetBpm(newBpm, rampBars: 4)` - tempo ramps over 4 bars.

---

## Architecture

### Files

```
Scripts/Core/
  MusicClock.cs         ✅ Phase 1 - metronome; beat/bar/phrase events; BPM ramp
  MusicDirector.cs      ✅ Phase 3 - owns clock + bass layer; game state hooks wired
  MusicHarmony.cs       ✅ Phase 2 - scale/chord tables; tension → mode/BPM mapping
  MusicBassLayer.cs     ✅ Phase 2 - root + fifth patterns; phrase-boundary mode changes
  MusicMelodyLayer.cs   ✅ Phase 4 - improvised lead; contour-based phrase planning, scale-walking
  MusicPercLayer.cs        Phase 5 - kick/hat/snare grid; density varies with tension
```

`SoundManager` handles note synthesis and playback - no separate MusicSampler file needed.

### Note Synthesis (SoundManager)

Registered at startup in `SoundManager._Ready()`. All notes on the "Music" bus via a dedicated 8-slot pool (`_notePool`), separate from the SFX pool.

| Range | MIDI | Instrument character |
|---|---|---|
| Bass | 28–57 (E1–A3) | Fundamental + octave harmonic + detuned pair; 2s decay |
| Lead | 58–81 (A3+1–A5) | Chorused pair (±3Hz detuning); 1.6s decay |

Percussion samples (kick, hat, snare) will be synthesized and registered in Phase 5.

Public API: `SoundManager.Instance?.PlayNote(int midiNote)`

### Integration with Existing Code

`MusicDirector` is a `Node` in `Main.tscn` as a sibling of `GameController`. It listens to existing game events:

| Signal / Hook | Music Response |
|---|---|
| `GameController.StartWavePhase()` (Phase 3) | Recalculate tension tier; queue tempo/mode shift at next phrase boundary |
| Wave clear (Phase 3) | Brief "breath" - drop percussion density for 1 bar |
| `RunState.Lives` threshold ≤ 3 (Phase 3) | Shift to sparse near-death texture |
| `SpectacleSystem.OnGlobalTriggered` (Phase 3) | Trigger a 1-bar "surge fill" phrase; melody plays ascending run |
| Run end win/loss (Phase 3) | Win: resolve to tonic chord. Lose: leave phrase unresolved, fade |
| `StartDraftPhase()` (Phase 3) | Drop to bass-only or silence; resume full texture on wave start |

`SoundManager` remains the audio backend - `MusicDirector` orchestrates *when* notes play, `SoundManager` handles *how*.

---

## Improvisation Logic (Phase 4 - MusicMelodyLayer)

The melody layer is the heart of the system. Each phrase (4 bars) is planned one phrase ahead:

1. **Pick a target note** - the phrase "goal". Weighted toward scale tones that resolve the previous phrase's ending note. Tension state biases toward dissonant scale degrees (b7, b2 in Phrygian).
2. **Plan the phrase contour** - ascending, descending, arch, or static. Randomly selected but weighted by tension (higher tension → more angular, less smooth).
3. **Fill the phrase** - walk from start pitch toward target using stepwise motion + occasional leaps. Rhythm slots are weighted; busier = more 8th notes, calmer = more quarter rests.
4. **Apply "human" imperfections** - random ±10ms timing offset per note, random ±5% velocity variation via VolumeDb on the note pool player.

Weights are tuned constants in `Balance.cs` (or a dedicated `MusicBalance.cs` added in Phase 5).

### Key constraint
The melody **never plays notes outside the active scale**. All improvisation is chromatic-free within the chosen mode.

---

## Game State → Music Tension Mapping

```
Tension 0 (intro/early):  Bass only or bass+perc. Melody sparse (50% rest probability).
Tension 1 (building):     All three layers. Melody moderate density. Dorian or Mixolydian.
Tension 2 (mid-game):     Full texture. Melody busier. Mixolydian. BPM ramp begins.
Tension 3 (late/hard):    Full texture + increased perc syncopation. Phrygian option unlocked.
Tension 4 (near-death):   Perc drops out. Melody goes to long tones. Bass holds chord roots only.
                           Creates a "last stand" hollow feel.
```

Tension level = f(WaveIndex, CurrentLives, Difficulty). Exact formula tuned in Phase 5.

---

## MusicClock - Timing

`MusicClock` uses `Time.GetTicksUsec()` as an absolute monotonic clock rather than accumulating `_Process(delta)`. This eliminates drift in long sessions - the beat never slips relative to wall time regardless of GC pauses or frame rate variation.

Beat firing order per frame:
```
if beat == 0:
    if bar == 0: PhraseFired
    BarFired(barIndex)
BeatFired(beatIndex)
```

BPM ramp: `SetBpm(float newBpm, float rampBars)` interpolates linearly over `rampBars` bars at the current BPM. Used for the wave-tier tempo transitions (72→88→96).

---

## Implementation Phases

### Phase 1 - Clock + Note Infrastructure ✅ COMPLETE
- `MusicClock.cs`: beat/bar/phrase events, BPM ramp, drift-free timing
- `SoundManager`: 8-slot music note pool on "Music" bus; `MusicNote()` synthesis (bass + lead timbres); `PlayNote(int midiNote)` public API; chromatic coverage MIDI 28–81
- `MusicDirector.cs` stub: owns clock, plays 4-bar A-Dorian test pattern (i–IV–i–VII) to validate timing end-to-end
- `Main.tscn`: MusicDirector added as sibling of GameController

### Phase 2 - Harmony + Bass Layer ✅ COMPLETE
- `MusicHarmony.cs`: scale tables (Dorian, Mixolydian, Phrygian), 4 progressions per mode, `ComputeTension()`, `TensionToMode()`, `TensionToBpm()`, `BassNormalize()`
- `MusicBassLayer.cs`: root + fifth patterns at bar/beat boundaries; `QueueModeChange()` applies at next phrase boundary; `Density` property (0=root only, 1=root+fifth)
- `MusicDirector.cs`: replaced hardcoded test pattern with `MusicBassLayer`; picks random progression from Dorian pool at run start

### Phase 3 - Game State Hooks ✅ COMPLETE
- `MusicDirector`: added `Instance` singleton, headless guard, `OnWaveStart`, `OnWaveClear`, `OnLivesChanged`, `OnDraftPhaseStart`, `OnRunEnd`
- `ApplyTension()`: ramps BPM over 4 bars, queues mode change at next phrase boundary, sets Density
- Wave-clear breath: 1-bar silence on bass, restored on next BarFired
- Near-death override: triggers immediately on lives drop, bypasses wave boundary
- `GameController`: 6 call sites added (wave start, wave clear, loss, win, lives changed, draft phase)

### Phase 4 - Melody Improvisation ✅ COMPLETE
- `MusicMelodyLayer.cs`: phrase planning, contour selection (Ascending/Descending/Arch/Static),
  weighted scale-degree walking via `GetMelodyScaleNotes()` + `AdvanceIndex()`; `Active` property
  for draft-phase silencing; `QueueModeChange()` for phrase-boundary transitions
- `SoundManager.PlayNote(int midiNote, float volDb = -8f)` — optional per-call volume;
  melody passes -11 dB to sit 3 dB below the bass
- `MusicDirector`: wires melody layer in `_Ready()`, `ApplyTension()` (Tension + Active + QueueModeChange),
  `OnDraftPhaseStart()` (Active = false)
- 29 unit tests in `MusicMelodyLayerTests` covering `GetMelodyScaleNotes` (range, scale membership,
  sorted order, mode-specific chromatic exclusions) and `RestProbability` (values + monotonicity)

### Phase 5 - Percussion + Polish ✅ COMPLETE
- `MusicClock`: added `SubBeatFired(int index)` (8th-note, 0–7 per bar) used by perc layer
- `SoundManager`: kick/snare/hat_c/hat_o synthesis registered; 4-slot perc pool on Music bus;
  `PlayPerc(id, volDb)`; `FadePad(durationSec)` fades ambient pad to inaudible
- `MusicPercLayer.cs`: 8th-note kick/snare/hat grid; 4 tension patterns; ghost snare probability;
  hat velocity jitter; `Active`/`Density`/`Tension` properties
- `MusicDirector`: wires perc layer; pad fade starts immediately on load (5s); breath silences
  bass + perc; near-death drops both to Density=0
- Mix tuning: kick sweep 100→65 Hz (avoids sub-bass boom); pad A1 root removed; bass at -19 dB

---

## Open Questions

1. **Percussion feel** - 808-style electronic drum machine fits the neon aesthetic. Confirm before synthesizing perc samples in Phase 5.
2. **Draft phase** - silence or ambient pad? A static low drone during draft may feel better than abrupt silence. Low-volume bass-only is probably the answer.
3. **Per-map root note** - tying the key to the map could add subconscious identity to each map. Worth a small design pass once the system works.
4. **Melody "badness" floor** - pure algorithmic melody can produce ugly phrases. May need a small set of "safe" fallback patterns it can splice in if weighted selection produces too many leaps.
5. **Pad coexistence mix** - once the procedural bass is running alongside the pad, the pad's bass frequencies may clash. May need to low-cut or attenuate the pad's bass layer temporarily before full replacement in Phase 5.

---

## Out of Scope (for this implementation)

- Live synthesis / DSP (AudioStreamGenerator melodic synthesis) - complexity not justified by gain
- Adaptive mixing based on SFX frequency content
- Branching narrative music cues
- Player-influenceable music (e.g. tower types affecting instrumentation - cool, but post-v1)
