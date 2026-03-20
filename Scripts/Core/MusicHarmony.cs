namespace SlotTheory.Core;

/// <summary>
/// Musical tension level driving mode and BPM selection.
/// Computed from wave index, lives remaining, and difficulty.
/// </summary>
public enum MusicTension
{
    Intro      = 0,  // waves 1–7:  bass only or bass+perc, melody sparse
    Building   = 1,  // waves 1–7 full: all layers, moderate density, Dorian
    MidGame    = 2,  // waves 8-14:  full texture, Dorian, BPM ramp
    LateGame   = 3,  // waves 15–20: full texture + syncopation, Phrygian
    NearDeath  = 4,  // ≤ 3 lives:   perc out, long tones, bass root only
}

/// <summary>
/// Musical mode (scale flavor) used by the improvisation system.
/// </summary>
public enum MusicMode { Dorian, Mixolydian, Phrygian }

/// <summary>
/// Scale and chord knowledge for the procedural music system.
/// Pure C# - no Godot dependencies.
///
/// All intervals are expressed as semitone offsets from the run's root MIDI note.
/// "Bass-normalized" offsets fold roots > 6 semitones above the tonic down an
/// octave so the bass line stays grounded.
/// </summary>
public static class MusicHarmony
{
    // ── Scale degree offsets (7-note modes, 1 octave) ─────────────────────

    // A Dorian: A B C D E F# G  - mellow, slightly melancholic
    private static readonly int[] DorianOffsets    = { 0, 2, 3, 5, 7, 9, 10 };

    // A Mixolydian: A B C# D E F# G  - driving, confident
    private static readonly int[] MixolydianOffsets = { 0, 2, 4, 5, 7, 9, 10 };

    // A Phrygian: A Bb C D E F G  - dark, tense
    private static readonly int[] PhrygianOffsets   = { 0, 1, 3, 5, 7, 8, 10 };

    /// <summary>Returns the 7 scale-degree semitone offsets for the given mode.</summary>
    public static int[] GetScaleOffsets(MusicMode mode) => mode switch
    {
        MusicMode.Mixolydian => MixolydianOffsets,
        MusicMode.Phrygian   => PhrygianOffsets,
        _                    => DorianOffsets,
    };

    // ── Chord progressions ────────────────────────────────────────────────
    // Each progression is a 4-element int[] of semitone offsets from the tonic,
    // one per bar. Values > 6 are folded down an octave by BassNormalize() when
    // used by MusicBassLayer, so the bass line stays below the tonic.

    private static readonly int[][] DorianProgressions =
    {
        new[] {  0,  5,  0, 10 },  // i – IV – i – VII    A – D – A – G
        new[] {  0, 10,  5,  0 },  // i – VII – IV – i    A – G – D – A
        new[] {  0,  5, 10,  0 },  // i – IV – VII – i    A – D – G – A
        new[] {  0,  7,  5,  0 },  // i – v – IV – i      A – E – D – A
    };

    private static readonly int[][] MixolydianProgressions =
    {
        new[] {  0, 10,  5,  0 },  // I – VII – IV – I    A – G – D – A
        new[] {  0,  5, 10,  0 },  // I – IV – VII – I    A – D – G – A
        new[] {  0,  2, 10,  0 },  // I – II – VII – I    A – B – G – A
        new[] {  0,  7,  5,  0 },  // I – V – IV – I      A – E – D – A
    };

    private static readonly int[][] PhrygianProgressions =
    {
        new[] {  0,  1,  0, 10 },  // i – bII – i – bVII   A – Bb – A – G
        new[] {  0, 10,  5,  0 },  // i – bVII – iv – i    A – G – D – A
        new[] {  0,  1, 10,  0 },  // i – bII – bVII – i   A – Bb – G – A
    };

    private static int[][] ProgressionsFor(MusicMode mode) => mode switch
    {
        MusicMode.Mixolydian => MixolydianProgressions,
        MusicMode.Phrygian   => PhrygianProgressions,
        _                    => DorianProgressions,
    };

    /// <summary>Returns the number of available progressions for the given mode.</summary>
    public static int ProgressionCount(MusicMode mode) => ProgressionsFor(mode).Length;

    /// <summary>
    /// Returns the raw (non-normalized) semitone offset from the tonic for the chord
    /// root at a given bar position in the selected progression.
    /// </summary>
    public static int GetChordRootOffset(MusicMode mode, int progressionIndex, int barInPhrase)
    {
        var table = ProgressionsFor(mode);
        var prog  = table[progressionIndex % table.Length];
        return prog[barInPhrase % prog.Length];
    }

    /// <summary>
    /// Maps a MusicTension level to a musical mode.
    /// Intro/Building/MidGame -> Dorian, LateGame/NearDeath -> Phrygian.
    /// </summary>
    public static MusicMode TensionToMode(MusicTension tension) => tension switch
    {
        MusicTension.LateGame
            or MusicTension.NearDeath      => MusicMode.Phrygian,
        _                                  => MusicMode.Dorian,
    };

    /// <summary>
    /// Computes a tension level from wave index (1-based) and current lives.
    /// NearDeath overrides all other tiers when lives ≤ 3.
    /// </summary>
    public static MusicTension ComputeTension(int waveIndex, int lives)
    {
        if (lives <= 3)                 return MusicTension.NearDeath;
        if (waveIndex >= 15)            return MusicTension.LateGame;
        if (waveIndex >= 8)             return MusicTension.MidGame;
        if (waveIndex >= 4)             return MusicTension.Building;
        return MusicTension.Intro;
    }

    /// <summary>
    /// Returns the target BPM for a given tension level.
    /// Transitions should be gradual - call Clock.SetBpm(bpm, rampBars: 4).
    /// </summary>
    public static float TensionToBpm(MusicTension tension) => tension switch
    {
        MusicTension.MidGame               => 128f,
        MusicTension.LateGame
            or MusicTension.NearDeath      => 140f,
        _                                  => 112f,
    };

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Fold a semitone offset so bass roots stay close to (and below) the tonic.
    /// Offsets > 6 are pulled down an octave; result is in range [-5, 6].
    /// Example: G above A (offset 10) → G below A (offset -2).
    /// </summary>
    public static int BassNormalize(int offset) => offset > 6 ? offset - 12 : offset;

    /// <summary>
    /// Converts a scale degree index to an absolute MIDI note number.
    /// degree wraps within the 7-note mode; octave shifts the result up/down.
    /// </summary>
    public static int ScaleDegreeToMidi(int rootMidi, MusicMode mode, int degree, int octave = 0)
    {
        var offsets = GetScaleOffsets(mode);
        int idx     = ((degree % offsets.Length) + offsets.Length) % offsets.Length;
        return rootMidi + offsets[idx] + octave * 12;
    }
}

