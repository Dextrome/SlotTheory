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
    private MusicClock _clock     = null!;
    private int        _rootMidi;           // run tonic MIDI note (e.g. 45 = A2)
    private MusicMode  _mode;
    private int        _progressionIdx;
    private int        _currentChordOffset; // semitones from _rootMidi for the current bar

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
            PlayFifth();
    }

    // ── Note helpers ──────────────────────────────────────────────────────

    private void PlayRoot()
    {
        int midi = Clamp(_rootMidi + _currentChordOffset);
        SoundManager.Instance?.PlayNote(midi);
    }

    private void PlayFifth()
    {
        int midi = Clamp(_rootMidi + _currentChordOffset + 7);
        SoundManager.Instance?.PlayNote(midi);
    }

    // Keep within the registered bass range (MIDI 28–57).
    private static int Clamp(int midi)
    {
        while (midi > 57) midi -= 12;
        while (midi < 28) midi += 12;
        return midi;
    }
}
