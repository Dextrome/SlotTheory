using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Top-level coordinator for the procedural music system.
/// Lives in Main.tscn as a sibling of GameController.
///
/// Phase 1 (current): Clock foundation + note synthesis validation.
///   Owns a MusicClock and plays a 4-bar A-Dorian bass pattern (i–IV–i–VII)
///   to verify beat-accurate note scheduling end-to-end.
///
/// Phase 2 (next): MusicHarmony + MusicBassLayer.
///   Replace the test pattern with a chord-following bass line driven by
///   MusicHarmony's scale/progression tables.
///
/// Phase 3: Game state hooks.
///   Wire OnWaveStart / OnWaveClear / OnLivesChanged / OnGlobalSurge / OnRunEnd /
///   OnDraftPhaseStart to tension-level changes, BPM ramps, and layer muting.
///
/// Phase 4: MusicMelodyLayer.
///   Phrase-planned improvised lead: contour selection, weighted scale-degree
///   walking, human-feel timing offsets.
///
/// Phase 5: MusicPercLayer + polish.
///   Kick/hat/snare density grid; game-reactive density drops; per-layer
///   volume envelopes; crossfade on mode transitions.
/// </summary>
public partial class MusicDirector : Node
{
    public MusicClock Clock { get; private set; } = null!;

    // ── Phase 1: A-Dorian test bass pattern ──────────────────────────────
    // One MIDI root note per bar, cycling a 4-bar i–IV–i–VII progression.
    //   Bar 0: A2 = MIDI 45 (tonic)
    //   Bar 1: D2 = MIDI 38 (IV - Dorian subdominant)
    //   Bar 2: A2 = MIDI 45 (tonic return)
    //   Bar 3: G2 = MIDI 43 (VII - Dorian subtonic)
    private static readonly int[] _testBassRoots = { 45, 38, 45, 43 };

    public override void _Ready()
    {
        Clock = new MusicClock();
        AddChild(Clock);
        Clock.BarFired  += OnBar;
        Clock.BeatFired += OnBeat;
        Clock.Start(72f);
    }

    private void OnBar(int barIndex)
    {
        // Root note on the downbeat of every bar
        SoundManager.Instance?.PlayNote(_testBassRoots[barIndex]);
    }

    private void OnBeat(int beatIndex)
    {
        // Fifth above (7 semitones) on beat 2 - adds harmonic bounce
        if (beatIndex == 2)
            SoundManager.Instance?.PlayNote(_testBassRoots[Clock.CurrentBar] + 7);
    }

    // ── Phase 3: game state hooks (stubbed - wired in Phase 3) ───────────

    // public void OnWaveStart(int waveIndex) { }
    // public void OnWaveClear(int waveIndex) { }
    // public void OnLivesChanged(int lives) { }
    // public void OnGlobalSurge() { }
    // public void OnRunEnd(bool won) { }
    // public void OnDraftPhaseStart() { }
}
