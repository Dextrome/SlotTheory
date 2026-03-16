using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Bass line layer for the procedural music system.
///
/// Subscribes to MusicClock beat/bar events and plays chord-root bass patterns
/// according to the current harmony and density setting.
///
/// Density levels:
///   0 = silent (near-death / draft / wave-clear breath)
///   1 = root on beat 0, fifth on beat 2 (standard)
///
/// Phase 5 will add density 2 (walking bass with approach notes).
/// </summary>
public partial class MusicBassLayer : Node
{
    private MusicClock    _clock     = null!;
    private int           _rootMidi;           // run tonic MIDI note (e.g. 45 = A2)
    private MusicMode     _mode;
    private int           _progressionIdx;
    private int           _currentChordOffset; // semitones from _rootMidi for the current bar
    private System.Random _rng = new();

    private const float PassNoteChance = 0.30f;

    public int Density { get; set; } = 1;

    // Pending mode/progression change - applied at the next phrase boundary.
    private bool      _pendingChange;
    private MusicMode _pendingMode;
    private int       _pendingProgressionIdx;

    // ── Configuration ─────────────────────────────────────────────────────

    /// <summary>
    /// Attach this layer to the clock and start responding to beat events.
    /// Call once from MusicDirector._Ready().
    /// </summary>
    public void Configure(MusicClock clock, int rootMidi, MusicMode mode, int progressionIndex)
    {
        _clock          = clock;
        _rootMidi       = rootMidi;
        _mode           = mode;
        _progressionIdx = progressionIndex;

        _clock.PhraseFired += OnPhrase;
        _clock.BarFired    += OnBar;
        _clock.BeatFired   += OnBeat;

        // Prime the first chord before the clock fires its first bar event.
        _currentChordOffset = MusicHarmony.BassNormalize(
            MusicHarmony.GetChordRootOffset(_mode, _progressionIdx, 0));
    }

    /// <summary>
    /// Schedule a mode + progression change at the next phrase boundary (bar 0).
    /// Safe to call mid-phrase - won't interrupt the current phrase.
    /// </summary>
    public void QueueModeChange(MusicMode mode, int progressionIndex)
    {
        _pendingMode           = mode;
        _pendingProgressionIdx = progressionIndex;
        _pendingChange         = true;
    }

    // ── Clock event handlers ──────────────────────────────────────────────

    private void OnPhrase()
    {
        // Apply any queued mode change at the phrase boundary.
        if (_pendingChange)
        {
            _mode           = _pendingMode;
            _progressionIdx = _pendingProgressionIdx;
            _pendingChange  = false;
        }
    }

    private void OnBar(int barIndex)
    {
        _currentChordOffset = MusicHarmony.BassNormalize(
            MusicHarmony.GetChordRootOffset(_mode, _progressionIdx, barIndex));
        if (Density > 0)
            PlayRoot();
    }

    private void OnBeat(int beatIndex)
    {
        if (Density >= 1 && beatIndex == 2)
        {
            if (_rng.NextDouble() < PassNoteChance)
                PlayPassingNote();
            else
                PlayFifth();
        }
    }

    // ── Note helpers ──────────────────────────────────────────────────────

    private const float BassVolDb = -17f;

    private void PlayRoot()
    {
        int midi = Clamp(_rootMidi + _currentChordOffset);
        SoundManager.Instance?.PlayNote(midi, BassVolDb);
    }

    private void PlayFifth()
    {
        int midi = Clamp(_rootMidi + _currentChordOffset + 7);
        SoundManager.Instance?.PlayNote(midi, BassVolDb);
    }

    /// <summary>
    /// On ~30% of beat-3 hits, play a scale tone between the chord root and its
    /// fifth for a walking bass feel. Picks a random scale degree in (root, root+7).
    /// Falls back to the fifth if no scale tone exists in that gap.
    /// </summary>
    private void PlayPassingNote()
    {
        int chordMidi  = Clamp(_rootMidi + _currentChordOffset);
        int[] scaleOff = MusicHarmony.GetScaleOffsets(_mode);
        var   scaleSet = new System.Collections.Generic.HashSet<int>(scaleOff);

        // Collect scale tones strictly between the chord root and fifth (semitone gap 1–6)
        var candidates = new System.Collections.Generic.List<int>();
        for (int delta = 1; delta <= 6; delta++)
        {
            int semitone = ((_rootMidi + _currentChordOffset + delta) % 12 + 12) % 12;
            if (scaleSet.Contains(semitone))
                candidates.Add(Clamp(chordMidi + delta));
        }

        int midi = candidates.Count > 0
            ? candidates[_rng.Next(candidates.Count)]
            : Clamp(chordMidi + 7);  // fallback: plain fifth

        SoundManager.Instance?.PlayNote(midi, BassVolDb - 2f);  // passing notes sit slightly softer
    }

    // Keep within the registered bass range (MIDI 28–57).
    private static int Clamp(int midi)
    {
        while (midi > 57) midi -= 12;
        while (midi < 28) midi += 12;
        return midi;
    }
}
