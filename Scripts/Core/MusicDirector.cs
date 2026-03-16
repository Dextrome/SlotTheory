using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Top-level coordinator for the procedural music system.
/// Lives in Main.tscn as a sibling of GameController.
///
/// Phase 1 (done): Clock foundation + note synthesis validation.
/// Phase 2 (done): MusicHarmony chord tables + MusicBassLayer.
/// Phase 3 (done): Game state hooks.
///   Responds to wave start/clear, lives changes, run end, and draft phase
///   by adjusting tension tier, BPM, mode, and layer density.
///
/// Phase 4 (done): MusicMelodyLayer — phrase-planned improvised lead.
/// Phase 5 (next): MusicPercLayer + polish; fade out ambient pad.
/// </summary>
public partial class MusicDirector : Node
{
    public static MusicDirector? Instance { get; private set; }

    public MusicClock       Clock       { get; private set; } = null!;
    public MusicBassLayer   BassLayer   { get; private set; } = null!;
    public MusicMelodyLayer MelodyLayer { get; private set; } = null!;

    // Run tonic: A2 = MIDI 45 (110 Hz).
    private const int RunRootMidi = 45;

    private MusicTension  _tension = MusicTension.Intro;
    private System.Random _rng     = new();

    // Wave-clear breath: silence bass for N bars then restore.
    private int _breathBarsLeft;
    private int _breathPrevDensity;

    public override void _Ready()
    {
        // No audio in headless / bot mode.
        // Instance stays null so all GameController hook calls (?.operator) are no-ops.
        if (DisplayServer.GetName() == "headless") return;

        Instance = this;

        var mode = MusicMode.Dorian;
        int prog = _rng.Next(MusicHarmony.ProgressionCount(mode));

        Clock = new MusicClock();
        AddChild(Clock);
        Clock.BarFired += OnBar;

        BassLayer = new MusicBassLayer();
        AddChild(BassLayer);
        BassLayer.Configure(Clock, RunRootMidi, mode, prog);

        // Start silent — OnWaveStart will open up density when the first wave begins.
        BassLayer.Density = 0;

        MelodyLayer = new MusicMelodyLayer();
        AddChild(MelodyLayer);
        MelodyLayer.Configure(Clock, RunRootMidi, mode, _tension);
        MelodyLayer.Active = false;  // silent until first wave

        Clock.Start(MusicHarmony.TensionToBpm(_tension));
    }

    // ── Bar handler (for breath management) ──────────────────────────────

    private void OnBar(int _)
    {
        if (_breathBarsLeft <= 0) return;
        if (--_breathBarsLeft == 0)
            BassLayer.Density = _breathPrevDensity;
    }

    // ── Game state hooks (called from GameController) ─────────────────────

    /// <summary>
    /// Call at the start of each wave. Recomputes tension from wave + lives,
    /// ramps BPM if needed, and queues a mode change at the next phrase boundary.
    /// </summary>
    public void OnWaveStart(int waveIndex, int lives)
    {
        var newTension = MusicHarmony.ComputeTension(waveIndex, lives);
        ApplyTension(newTension);
    }

    /// <summary>
    /// Call when a wave clears. Silences the bass for one bar (a brief breath)
    /// before the draft panel appears.
    /// </summary>
    public void OnWaveClear()
    {
        _breathPrevDensity = BassLayer.Density;
        BassLayer.Density  = 0;
        // 2 bars needed: MusicDirector.OnBar fires first and restores Density,
        // then MusicBassLayer.OnBar fires on the same event — so bar N would play
        // immediately. The extra bar ensures bar N is truly silent.
        _breathBarsLeft    = 2;
    }

    /// <summary>
    /// Call whenever lives change. Applies near-death texture immediately
    /// without waiting for the next wave boundary.
    /// </summary>
    public void OnLivesChanged(int lives)
    {
        if (lives <= 3 && _tension != MusicTension.NearDeath)
        {
            BassLayer.Density = 0;
            _tension          = MusicTension.NearDeath;
        }
        else if (lives > 3 && _tension == MusicTension.NearDeath)
        {
            // Recovering from near-death — restore density (next OnWaveStart re-evaluates fully).
            BassLayer.Density = 1;
        }
    }

    /// <summary>
    /// Call when the draft phase begins. Drops to root-only for a quieter backdrop.
    /// </summary>
    public void OnDraftPhaseStart()
    {
        _breathBarsLeft    = 0;  // cancel any pending breath restore
        BassLayer.Density  = 0;
        MelodyLayer.Active = false;
    }

    /// <summary>
    /// Call when the run ends. Stops the procedural layers; the ambient pad continues.
    /// </summary>
    public void OnRunEnd(bool won)
    {
        Clock.Stop();
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private void ApplyTension(MusicTension newTension)
    {
        bool changed = newTension != _tension;
        _tension     = newTension;

        // Near-death: BPM stays at whatever it currently is (doc spec).
        if (newTension != MusicTension.NearDeath)
        {
            float bpm = MusicHarmony.TensionToBpm(newTension);
            if (System.MathF.Abs(Clock.Bpm - bpm) > 1f)
                Clock.SetBpm(bpm, rampBars: 4);
        }

        // Queue a mode change if the emotional tier shifted.
        if (changed)
        {
            var mode = MusicHarmony.TensionToMode(newTension);
            int prog = _rng.Next(MusicHarmony.ProgressionCount(mode));
            BassLayer.QueueModeChange(mode, prog);
            MelodyLayer.QueueModeChange(mode);
        }

        // Update melody tension for phrase planning.
        MelodyLayer.Tension = newTension;
        MelodyLayer.Active  = true;  // entering wave phase

        // Near-death → sparse; otherwise open density (unless a breath is in progress).
        if (newTension == MusicTension.NearDeath)
            BassLayer.Density = 0;
        else if (_breathBarsLeft == 0)
            BassLayer.Density = 1;
    }
}
