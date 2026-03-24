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
/// Phase 4 (done): MusicMelodyLayer - phrase-planned improvised lead.
/// Phase 5 (done): MusicPercLayer + pad fade-out.
/// Phase 6 (done): Per-map music profiles, drum fills, hat variation.
/// </summary>
public partial class MusicDirector : Node
{
    public static MusicDirector? Instance { get; private set; }

    public MusicClock       Clock       { get; private set; } = null!;
    public MusicBassLayer   BassLayer   { get; private set; } = null!;
    public MusicMelodyLayer MelodyLayer { get; private set; } = null!;
    public MusicPercLayer   PercLayer   { get; private set; } = null!;

    // Base tonic: A2 = MIDI 45 (110 Hz). Per-map profiles shift this.
    private const int BaseRootMidi = 45;

    private MusicTension  _tension = MusicTension.Intro;
    private System.Random _rng     = new();

    // Active profile (set in _Ready based on selected map)
    private MapMusicProfile _profile;

    // Wave-clear breath: silence bass + perc for N bars then restore.
    private int _breathBarsLeft;
    private int _breathPrevBassDensity;
    private int _breathPrevPercDensity;
    private bool _startupPercRampActive;
    private int _startupPercRampBarIndex;

    // ── Per-map music profiles ─────────────────────────────────────────────
    //
    // RootOffset:       semitones added to BaseRootMidi (A2=45). 0=A, 2=B, 3=C, 5=D.
    // IntroMode:        mode for Intro/Building tiers; higher tiers still shift normally.
    // BpmOffset:        added to tension-based BPM for overall pace feel.
    // HatQuarterChance: per-bar probability of dropping to quarter-note hats (0=never).
    // DrumStyle:        groove character for kick/snare patterns.

    private readonly record struct MapMusicProfile(
        int                         RootOffset,
        MusicMode                   IntroMode,
        float                       BpmOffset,
        float                       HatQuarterChance,
        MusicPercLayer.DrumStyle    DrumStyle);

    private static MapMusicProfile GetProfileForMap(string? mapId) => mapId switch
    {
        // crossroads - more drive up-front without losing readability
        "crossroads" => new MapMusicProfile(0, MusicMode.Dorian, +12f, 0.10f, MusicPercLayer.DrumStyle.Funk),
        // pinch_bleed - max aggression: darker color + faster pace + relentless kick
        "pinch_bleed"   => new MapMusicProfile(5, MusicMode.Phrygian,   +30f, 0.00f, MusicPercLayer.DrumStyle.FourOnFloor),
        // orbit - keep space but keep a dark/funky pocket
        "orbit"         => new MapMusicProfile(2, MusicMode.Dorian,      +8f, 0.14f, MusicPercLayer.DrumStyle.Funk),
        // random_map - tense and energetic by default
        "random_map"    => new MapMusicProfile(3, MusicMode.Phrygian,   +18f, 0.12f, MusicPercLayer.DrumStyle.Funk),
        _               => new MapMusicProfile(0, MusicMode.Dorian,     +10f, 0.12f, MusicPercLayer.DrumStyle.Funk),
    };


    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        // No audio in headless / bot mode.
        // Instance stays null so all GameController hook calls (?.operator) are no-ops.
        if (DisplayServer.GetName() == "headless") return;

        Instance = this;

        // Pick the per-map profile before anything else.
        string? mapId = GameController.Instance?.GetRunState()?.SelectedMapId;
        _profile = GetProfileForMap(mapId);

        int rootMidi = BaseRootMidi + _profile.RootOffset;
        var mode     = _profile.IntroMode;
        int prog     = _rng.Next(MusicHarmony.ProgressionCount(mode));

        Clock = new MusicClock();
        AddChild(Clock);
        Clock.BarFired += OnBar;

        BassLayer = new MusicBassLayer();
        AddChild(BassLayer);
        BassLayer.Configure(Clock, rootMidi, mode, prog);
        BassLayer.Density = 0;  // silent until first wave

        MelodyLayer = new MusicMelodyLayer();
        AddChild(MelodyLayer);
        MelodyLayer.Configure(Clock, rootMidi, mode, _tension, prog);
        MelodyLayer.Active = false;

        PercLayer = new MusicPercLayer();
        AddChild(PercLayer);
        PercLayer.Configure(Clock, _tension);
        PercLayer.Active           = false;
        PercLayer.Density          = 0;
        PercLayer.HatQuarterChance = _profile.HatQuarterChance;
        PercLayer.Style            = _profile.DrumStyle;

        float startBpm = TargetBpmFor(_tension);
        Clock.Start(startBpm);

        // Fade ambient pad quickly so map combat layers dominate sooner.
        SoundManager.Instance?.FadePad(2.5f);
    }

    // ── Bar handler (for breath management) ──────────────────────────────

    private void OnBar(int _)
    {
        if (_breathBarsLeft > 0)
        {
            if (--_breathBarsLeft == 0)
            {
                BassLayer.Density = _breathPrevBassDensity;
                PercLayer.Density = _breathPrevPercDensity;
            }
            return;
        }

        if (!_startupPercRampActive || PercLayer is null)
            return;

        _startupPercRampBarIndex++;
        if (_startupPercRampBarIndex == 1)
        {
            // Bar 1: no drums at all.
            PercLayer.Density = 0;
            PercLayer.WaveIntroBarsLeft = Mathf.Max(PercLayer.WaveIntroBarsLeft, 2);
        }
        else if (_startupPercRampBarIndex == 2)
        {
            // Bar 2: bring rhythm back but keep hats restrained.
            PercLayer.Density = 1;
            PercLayer.HatQuarterChance = Mathf.Clamp(_profile.HatQuarterChance + 0.45f, 0f, 1f);
            PercLayer.WaveIntroBarsLeft = Mathf.Max(PercLayer.WaveIntroBarsLeft, 1);
        }
        else
        {
            // Bar 3+: fully unlocked profile groove.
            PercLayer.HatQuarterChance = _profile.HatQuarterChance;
            _startupPercRampActive = false;
        }
    }

    // ── Game state hooks (called from GameController) ─────────────────────

    /// <summary>
    /// Call at the start of each wave. Recomputes tension from wave + lives,
    /// ramps BPM if needed, and queues a mode change at the next phrase boundary.
    /// </summary>
    public void OnWaveStart(int waveIndex, int lives)
    {
        if (BassLayer is null) return;
        var newTension = MusicHarmony.ComputeTension(waveIndex, lives);
        // Keep the ramp, but shorten it so combat energy ramps in faster.
        if (newTension != MusicTension.NearDeath)
            PercLayer.WaveIntroBarsLeft = 1;
        ApplyTension(newTension);
    }

    /// <summary>
    /// Call when a global surge triggers. Queues a 1-bar surge fill on the perc layer.
    /// </summary>
    public void OnGlobalSurge()
    {
        if (PercLayer is null || BassLayer is null) return;
        if (PercLayer.Active && PercLayer.Density > 0)
            PercLayer.SurgeFillPending = true;
        if (BassLayer.Density > 0)
            BassLayer.SurgeAccentPending = true;
    }

    /// <summary>
    /// Call when a wave clears. Silences bass + perc for a brief breath
    /// before the draft panel appears.
    /// </summary>
    public void OnWaveClear()
    {
        if (BassLayer is null) return;
        // Keep map music continuous into draft; no wave-clear breath mute.
        _breathBarsLeft = 0;
    }

    /// <summary>
    /// Call whenever lives change. Applies near-death texture immediately
    /// without waiting for the next wave boundary.
    /// </summary>
    public void OnLivesChanged(int lives)
    {
        if (BassLayer is null) return;
        if (lives <= 3 && _tension != MusicTension.NearDeath)
        {
            BassLayer.Density = 0;
            PercLayer.Density = 0;
            _tension          = MusicTension.NearDeath;
        }
        else if (lives > 3 && _tension == MusicTension.NearDeath)
        {
            // Recovering from near-death - restore density (next OnWaveStart re-evaluates fully).
            BassLayer.Density = 1;
            PercLayer.Density = 1;
        }
    }

    /// <summary>
    /// Call when the draft phase begins. Silences all layers for a quieter backdrop.
    /// </summary>
    public void OnDraftPhaseStart()
    {
        if (BassLayer is null) return;
        _startupPercRampActive = false;
        _startupPercRampBarIndex = 0;
        _breathBarsLeft = 0;  // ensure no pending mute behavior from prior logic
        // Keep all layers running through draft panels.
        MelodyLayer.Active = true;
        PercLayer.Active   = true;
        if (_tension != MusicTension.NearDeath)
        {
            BassLayer.Density = 1;
            PercLayer.Density = 1;
        }
        PercLayer.HatQuarterChance = _profile.HatQuarterChance;
    }

    /// <summary>
    /// Starts map music for the very first draft screen so the run does not begin in silence.
    /// Keeps percussion muted until wave combat actually starts.
    /// </summary>
    public void OnInitialDraftStart(int lives)
    {
        if (BassLayer is null) return;

        var introTension = MusicHarmony.ComputeTension(waveIndex: 1, lives);
        ApplyTension(introTension);
        BeginStartupPercussionRamp();
    }

    /// <summary>
    /// Call when the run ends. Stops the procedural layers; the ambient pad continues.
    /// </summary>
    public void OnRunEnd(bool won)
    {
        if (BassLayer is null) return;
        _startupPercRampActive = false;
        _startupPercRampBarIndex = 0;
        // Deactivate layers before stopping the clock so no new notes fire on
        // any remaining beat callbacks, then reset pad tension to ambient.
        BassLayer.Density  = 0;  // MusicBassLayer has no Active flag; zero density silences it
        PercLayer.Active   = false;
        MelodyLayer.Active = false;
        Clock.Stop();
        SoundManager.Instance?.SetMusicTension(0f);
        SoundManager.Instance?.FadePadIn(3.0f);  // gently restore ambient pad on end screen
    }

    /// <summary>
    /// Call when the player continues into Endless mode. Restarts the clock and
    /// re-activates procedural layers that were stopped by OnRunEnd.
    /// </summary>
    public void OnEndlessContinue(int waveIndex, int lives)
    {
        if (BassLayer is null) return;
        _tension = MusicHarmony.ComputeTension(waveIndex, lives);
        float bpm = TargetBpmFor(_tension);
        Clock.Start(bpm);
        BeginStartupPercussionRamp();
        MelodyLayer.Active = true;
        OnWaveStart(waveIndex, lives);
    }

    /// <summary>
    /// No-op: map music tempo is fixed regardless of gameplay speed.
    /// </summary>
    public void SetGameSpeedScale(float speedScale) { }

    // ── Internal ──────────────────────────────────────────────────────────

    private void ApplyTension(MusicTension newTension)
    {
        bool changed = newTension != _tension;
        _tension     = newTension;

        // Near-death: tension shifts do not retarget BPM (speed scaling may still retarget).
        if (newTension != MusicTension.NearDeath)
        {
            float bpm = TargetBpmFor(newTension);
            if (System.MathF.Abs(Clock.Bpm - bpm) > 1f)
                Clock.SetBpm(bpm, rampBars: 4);
        }

        // Queue a mode change if the emotional tier shifted.
        if (changed)
        {
            // Use the map's IntroMode for Intro/Building; higher tension tiers
            // still shift to Mixolydian/Phrygian as normal.
            MusicMode mode = newTension == MusicTension.Intro || newTension == MusicTension.Building
                ? _profile.IntroMode
                : MusicHarmony.TensionToMode(newTension);

            int prog = _rng.Next(MusicHarmony.ProgressionCount(mode));
            BassLayer.QueueModeChange(mode, prog);
            MelodyLayer.QueueModeChange(mode, prog);
        }

        // Update melody + perc tension for phrase/pattern selection.
        BassLayer.Tension   = newTension;
        MelodyLayer.Tension = newTension;
        PercLayer.Tension   = newTension;

        // Activate all layers for wave phase.
        MelodyLayer.Active = true;
        PercLayer.Active   = true;

        // Near-death → bass + perc drop out (hollow "last stand" feel).
        if (newTension == MusicTension.NearDeath)
        {
            BassLayer.Density = 0;
            PercLayer.Density = 0;
        }
        else if (_breathBarsLeft == 0)
        {
            BassLayer.Density = 1;
            PercLayer.Density = 1;
        }
    }

    private float TargetBpmFor(MusicTension tension)
    {
        // Fixed at 2× gameplay-speed tempo (× 1.25 over base BPM).
        return (MusicHarmony.TensionToBpm(tension) + _profile.BpmOffset) * 1.25f;
    }

    private void BeginStartupPercussionRamp()
    {
        _startupPercRampActive = true;
        _startupPercRampBarIndex = 0;
        PercLayer.Active = true;
        PercLayer.Density = 0;
        PercLayer.HatQuarterChance = Mathf.Clamp(_profile.HatQuarterChance + 0.55f, 0f, 1f);
        PercLayer.WaveIntroBarsLeft = Mathf.Max(PercLayer.WaveIntroBarsLeft, 2);
    }
}
