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
        // arena_classic — balanced reference: A Dorian, standard groove
        "arena_classic" => new MapMusicProfile(0, MusicMode.Dorian,      0f,  0.20f, MusicPercLayer.DrumStyle.Standard),
        // gauntlet — aggressive drive: D Mixolydian, +12 BPM, 4-on-the-floor kick, no hat variation
        "gauntlet"      => new MapMusicProfile(5, MusicMode.Mixolydian, +12f, 0.00f, MusicPercLayer.DrumStyle.FourOnFloor),
        // sprawl — spacious and chill: B Dorian, -10 BPM, half-time snare, frequent quarter-hat bars
        "sprawl"        => new MapMusicProfile(2, MusicMode.Dorian,    -10f,  0.50f, MusicPercLayer.DrumStyle.HalfTime),
        // random_map — unpredictable edge: C Phrygian, off-beat kick, high hat variation
        "random_map"    => new MapMusicProfile(3, MusicMode.Phrygian,    0f,  0.40f, MusicPercLayer.DrumStyle.Syncopated),
        _               => new MapMusicProfile(0, MusicMode.Dorian,      0f,  0.20f, MusicPercLayer.DrumStyle.Standard),
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
        MelodyLayer.Configure(Clock, rootMidi, mode, _tension);
        MelodyLayer.Active = false;

        PercLayer = new MusicPercLayer();
        AddChild(PercLayer);
        PercLayer.Configure(Clock, _tension);
        PercLayer.Active           = false;
        PercLayer.Density          = 0;
        PercLayer.HatQuarterChance = _profile.HatQuarterChance;
        PercLayer.Style            = _profile.DrumStyle;

        float startBpm = MusicHarmony.TensionToBpm(_tension) + _profile.BpmOffset;
        Clock.Start(startBpm);

        // Start fading the ambient pad immediately — procedural layers take over from wave 1.
        SoundManager.Instance?.FadePad(5f);
    }

    // ── Bar handler (for breath management) ──────────────────────────────

    private void OnBar(int _)
    {
        if (_breathBarsLeft <= 0) return;
        if (--_breathBarsLeft == 0)
        {
            BassLayer.Density = _breathPrevBassDensity;
            PercLayer.Density = _breathPrevPercDensity;
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
        ApplyTension(newTension);
    }

    /// <summary>
    /// Call when a wave clears. Silences bass + perc for a brief breath
    /// before the draft panel appears.
    /// </summary>
    public void OnWaveClear()
    {
        if (BassLayer is null) return;
        _breathPrevBassDensity = BassLayer.Density;
        _breathPrevPercDensity = PercLayer.Density;
        BassLayer.Density      = 0;
        PercLayer.Density      = 0;
        // 2 bars: MusicDirector.OnBar fires first (restores density), then
        // MusicBassLayer.OnBar fires — extra bar ensures bar N is truly silent.
        _breathBarsLeft        = 2;
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
            // Recovering from near-death — restore density (next OnWaveStart re-evaluates fully).
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
        _breathBarsLeft    = 0;  // cancel any pending breath restore
        BassLayer.Density  = 0;
        MelodyLayer.Active = false;
        PercLayer.Active   = false;
        PercLayer.Density  = 0;
    }

    /// <summary>
    /// Call when the run ends. Stops the procedural layers; the ambient pad continues.
    /// </summary>
    public void OnRunEnd(bool won)
    {
        if (BassLayer is null) return;
        Clock.Stop();
        PercLayer.Active  = false;
        MelodyLayer.Active = false;
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private void ApplyTension(MusicTension newTension)
    {
        bool changed = newTension != _tension;
        _tension     = newTension;

        // Near-death: BPM stays at whatever it currently is (doc spec).
        if (newTension != MusicTension.NearDeath)
        {
            float bpm = MusicHarmony.TensionToBpm(newTension) + _profile.BpmOffset;
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
            MelodyLayer.QueueModeChange(mode);
        }

        // Update melody + perc tension for phrase/pattern selection.
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
}
