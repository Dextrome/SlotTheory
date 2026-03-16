using Godot;
using System;
using System.Collections.Generic;

namespace SlotTheory.Core;

/// <summary>
/// Improvised lead melody layer for the procedural music system.
///
/// Plans one 4-bar phrase ahead (16 beats). On each PhraseFired event the
/// pre-planned phrase becomes current and a new phrase is scheduled for next
/// time. Notes are chosen from the active modal scale and shaped by a contour
/// (ascending / descending / arch / static) weighted by the current tension.
///
/// Activated by MusicDirector on wave start; silenced on draft phase.
/// </summary>
public partial class MusicMelodyLayer : Node
{
    private MusicClock   _clock   = null!;
    private int          _rootMidi;
    private MusicMode    _mode;
    private MusicTension _tension = MusicTension.Intro;
    private int          _currentBar;
    private Random       _rng     = new();

    // 16-slot phrase schedules (4 bars × 4 beats; null = rest)
    private int?[] _current = new int?[16];
    private int?[] _next    = new int?[16];

    // Pitch continuity across phrases
    private int? _lastNote;

    // Pending mode change — applied at next PhraseFired
    private bool      _pendingChange;
    private MusicMode _pendingMode;

    /// <summary>When false, beats are silenced (e.g. during draft phase).</summary>
    public bool Active { get; set; } = false;

    /// <summary>
    /// Current tension tier. Updated by MusicDirector.ApplyTension();
    /// affects rest probability and contour weights when planning the next phrase.
    /// </summary>
    public MusicTension Tension
    {
        get => _tension;
        set => _tension = value;
    }

    /// <summary>MIDI lead range lower bound (A3+1 = Bb3).</summary>
    public const int LeadMidiMin = 58;

    /// <summary>MIDI lead range upper bound (A5).</summary>
    public const int LeadMidiMax = 81;

    // Volume offset relative to the note pool base (-8 dB).
    // Melody sits 3 dB below bass so it doesn't crowd the low-mid.
    private const float MelodyVolDb = -9f;

    // ── Configuration ──────────────────────────────────────────────────────

    /// <summary>
    /// Attach this layer to the clock and begin responding to phrase/beat events.
    /// Call once from MusicDirector._Ready() after MusicBassLayer.Configure().
    /// </summary>
    public void Configure(MusicClock clock, int rootMidi, MusicMode mode, MusicTension tension)
    {
        _clock    = clock;
        _rootMidi = rootMidi;
        _mode     = mode;
        _tension  = tension;

        // Pre-plan the first phrase; it becomes _current at the first PhraseFired.
        _next = PlanPhrase(mode, tension);

        _clock.PhraseFired += OnPhrase;
        _clock.BarFired    += OnBar;
        _clock.BeatFired   += OnBeat;
    }

    /// <summary>
    /// Queue a mode change at the next phrase boundary.
    /// Safe to call mid-phrase.
    /// </summary>
    public void QueueModeChange(MusicMode mode)
    {
        _pendingMode   = mode;
        _pendingChange = true;
    }

    // ── Clock event handlers ───────────────────────────────────────────────

    private void OnPhrase()
    {
        // Swap pre-planned phrase into current slot
        (_current, _next) = (_next, _current);

        // Apply any deferred mode change
        if (_pendingChange)
        {
            _mode          = _pendingMode;
            _pendingChange = false;
        }

        // Plan ahead for the phrase after this one
        _next = PlanPhrase(_mode, _tension);
    }

    private void OnBar(int barIndex)
    {
        _currentBar = barIndex;
    }

    private void OnBeat(int beatIndex)
    {
        if (!Active) return;
        int slot = _currentBar * 4 + beatIndex;
        if ((uint)slot >= (uint)_current.Length) return;
        int? midi = _current[slot];
        if (midi.HasValue)
            SoundManager.Instance?.PlayNote(midi.Value, MelodyVolDb);
    }

    // ── Phrase planning ────────────────────────────────────────────────────

    private enum Contour { Ascending, Descending, Arch, Static }

    private int?[] PlanPhrase(MusicMode mode, MusicTension tension)
    {
        var slots = new int?[16];
        var notes = GetMelodyScaleNotes(_rootMidi, mode);
        if (notes.Count == 0) return slots;

        float   restProb = RestProbability(tension);
        Contour contour  = PickContour(tension);

        // Start index: close to the note we ended on last phrase for continuity
        int noteIdx = _lastNote.HasValue
            ? FindClosestIndex(notes, _lastNote.Value)
            : notes.Count / 2;

        int? lastFilled = null;

        for (int beat = 0; beat < 16; beat++)
        {
            if (_rng.NextDouble() < restProb)
            {
                slots[beat] = null;
                continue;
            }

            slots[beat] = notes[noteIdx];
            lastFilled  = notes[noteIdx];
            noteIdx     = AdvanceIndex(notes, noteIdx, beat, contour);
        }

        _lastNote = lastFilled;
        return slots;
    }

    private int AdvanceIndex(List<int> notes, int idx, int beat, Contour contour)
    {
        int max = notes.Count - 1;

        int step = contour switch
        {
            Contour.Ascending  => _rng.Next(4) < 3 ? 1 : 2,    // mostly step up, occasional skip
            Contour.Descending => _rng.Next(4) < 3 ? -1 : -2,  // mostly step down
            Contour.Arch       => beat < 8 ? 1 : -1,            // up for first 8 beats, down after
            _                  => _rng.Next(3) - 1,              // static: drift ±1 or stay
        };

        // 10% chance of an extra step for variety
        if (_rng.NextDouble() < 0.10)
            step += _rng.Next(2) == 0 ? 1 : -1;

        return Math.Clamp(idx + step, 0, max);
    }

    private Contour PickContour(MusicTension tension)
    {
        // Weights: [Ascending, Descending, Arch, Static]
        int[] weights = tension switch
        {
            MusicTension.Intro     => new[] { 30, 20, 10, 40 },
            MusicTension.Building  => new[] { 35, 20, 30, 15 },
            MusicTension.MidGame   => new[] { 25, 25, 35, 15 },
            MusicTension.LateGame  => new[] { 20, 30, 30, 20 },
            MusicTension.NearDeath => new[] { 10, 30, 10, 50 },
            _                      => new[] { 25, 25, 25, 25 },
        };

        int total = 0;
        foreach (int w in weights) total += w;
        int roll = _rng.Next(total), acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (roll < acc) return (Contour)i;
        }
        return Contour.Static;
    }

    // ── Static helpers (pure C#; unit-testable) ────────────────────────────

    /// <summary>
    /// Returns all MIDI notes in [LeadMidiMin, LeadMidiMax] that belong to the
    /// given mode's scale (relative to rootMidi), sorted ascending.
    /// </summary>
    public static List<int> GetMelodyScaleNotes(int rootMidi, MusicMode mode)
    {
        var offsets = MusicHarmony.GetScaleOffsets(mode);
        var notes   = new List<int>();
        for (int midi = LeadMidiMin; midi <= LeadMidiMax; midi++)
        {
            int semitone = ((midi - rootMidi) % 12 + 12) % 12;
            foreach (int off in offsets)
            {
                if (semitone == off) { notes.Add(midi); break; }
            }
        }
        return notes;
    }

    /// <summary>Fraction of beats that are rests, by tension tier.</summary>
    public static float RestProbability(MusicTension tension) => tension switch
    {
        MusicTension.NearDeath => 0.65f,
        MusicTension.Intro     => 0.55f,
        MusicTension.Building  => 0.35f,
        MusicTension.MidGame   => 0.25f,
        MusicTension.LateGame  => 0.20f,
        _                      => 0.40f,
    };

    private static int FindClosestIndex(List<int> notes, int targetMidi)
    {
        int best = 0, bestDist = Math.Abs(notes[0] - targetMidi);
        for (int i = 1; i < notes.Count; i++)
        {
            int d = Math.Abs(notes[i] - targetMidi);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }
}
