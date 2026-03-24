using System;
using System.Collections.Generic;
using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Generates all SFX procedurally at startup via PCM synthesis -
/// no audio files required. Uses AudioStreamGenerator so samples are pushed into a
/// ring buffer and played once per pool slot.
/// Use  SoundManager.Instance?.Play("id")  from anywhere.
/// </summary>
public partial class SoundManager : Node
{
    public static SoundManager? Instance { get; private set; }

    private const int   Rate     = 22050;
    private const int   PoolSize = 20;
    private const float MaxDur   = 9.0f;  // buffer length - must exceed longest sound (surge_global = 8s)

    private readonly Dictionary<string, Vector2[]> _samples  = new();
    private readonly Dictionary<string, float>     _durations = new();

    private AudioStreamPlayer[] _pool       = Array.Empty<AudioStreamPlayer>();
    private float[]             _poolTimers = Array.Empty<float>();
    private int                 _poolIdx;

    // Music streaming - precomputed loop pushed frame-by-frame via AudioStreamGenerator
    private Vector2[]                       _musicFrames   = Array.Empty<Vector2>();
    private int                             _musicPos;
    private AudioStreamPlayer?              _musicPlayer;
    private AudioStreamGeneratorPlayback?   _musicPlayback;
    private float _speedFxPitch = 1f;
    private float _speedMusicPitch = 1f;
    private float _musicTensionDb = 0f;
    private float _musicTensionPitch = 1f;
    private float _musicDuckDb = 0f;
    private float _musicDuckHold = 0f;
    private const float MusicBaseDb = -3f;

    // Procedural solo for alt rock track -- re-generated every 32 s loop (3 final bars)
    private float[] _soloNoteFreqs   = Array.Empty<float>();
    private bool[]  _soloNoteVibrato = Array.Empty<bool>();
    private float[] _soloNoteVol     = Array.Empty<float>(); // 1=full, 0.2=ghost, 0=rest
    private float[] _soloNoteBend    = Array.Empty<float>(); // != 0 → slide FROM this freq to target
    // Bars 0-8 lead: cascade (deterministic) OR Zappa-esque improv -- chosen each loop
    private float[] _leadNoteFreqs   = Array.Empty<float>();
    private bool[]  _leadVib         = Array.Empty<bool>();
    private float[] _leadVol         = Array.Empty<float>();
    private float[] _leadBend        = Array.Empty<float>();
    private int      _soloLoopSeed       = 12345;
    private bool     _isAltMusic         = false;
    private Vector2[]? _musicFramesFull  = null;   // full-intensity loop (no buildup); used from loop 2+
    private bool     _altMusicIntroPlayed = false;  // true after first 32s loop completes
    private bool     _isZoolMusic        = false;
    private bool     _zoolIntroPlayed    = false;
    // Fast-lick overlay: short blazing 64th-note bursts rendered additively on top of main solo
    private float[] _fastFreqs   = Array.Empty<float>();
    private float[] _fastVols    = Array.Empty<float>();
    private float[] _fastBends   = Array.Empty<float>();
    private int[]   _fastOffsets = Array.Empty<int>();
    private const int SoloNoteCount      = 96;          // 3 bars × 32 32nd-notes (double speed)
    private const int SoloSamplesPerNote = 1837;        // ≈ 22050/12  (32nd note @ 90 BPM)
    private const int SoloStartSample    = 529200;      // 24 × 22050
    private const int LeadNoteCount      = 144;         // 9 bars × 16 sixteenth-notes (bars 0-8)

    private static readonly HashSet<string> UiSoundIds = new(StringComparer.Ordinal)
    {
        "draft_pick", "ui_card_pick", "ui_preview_ghost", "ui_lock_in", "ui_lock_in_hard",
        "ui_thunk", "ui_speed_shift", "card_shing", "ui_select", "tower_place", "ui_hover",
        "low_heartbeat", "wave_halfway_lift", "wave20_swell", "achievement_unlock",
    };

    private bool _headless;
    private bool _isMobileAudio;
    private float _gameSpeedScale = 1f;
    private readonly Dictionary<string, ulong> _mobileSfxLastPlayMs = new();
    private readonly Dictionary<string, ulong> _desktopSfxLastPlayMs = new();
    private ulong[] _poolStopAtMs = Array.Empty<ulong>();

    // Music note pool - separate from the SFX pool; routed to the Music bus
    private const int NotePoolSize = 8;
    private const float NoteBufferLength = 2.5f;  // seconds; notes are max 2s
    private AudioStreamPlayer[] _notePool       = Array.Empty<AudioStreamPlayer>();
    private ulong[]             _notePoolStopMs = Array.Empty<ulong>();
    private int                 _notePoolIdx;

    // Percussion pool - 4 slots on the Music bus; short buffer (max perc sample ≈ 0.5s)
    private const int   PercPoolSize     = 4;
    private const float PercBufferLength = 0.6f;
    private AudioStreamPlayer[] _percPool       = Array.Empty<AudioStreamPlayer>();
    private ulong[]             _percPoolStopMs = Array.Empty<ulong>();
    private int                 _percPoolIdx;

    // Pad fade - gradually attenuates the ambient loop when the procedural system takes over
    private float _padFadeDb   = 0f;   // current accumulated fade (dB removed from pad)
    private float _padFadeRate = 0f;   // dB/second; 0 = no fade in progress
    private const float MobileFxBaseHeadroomDb = -4.0f;
    private const float MobileFxSurgeExtraHeadroomDb = -2.5f;
    private const int MobileFxBurstStartVoices = 8;
    private const int MobileFxBusyVoiceDropThreshold = 12;
    private const float MobileFxBurstPenaltyPerVoiceDb = 0.9f;
    private const float MobileFxBurstPenaltyCapDb = 7.0f;
    private static readonly HashSet<string> MobileDisabledExplosionSfx = new(StringComparer.Ordinal)
    {
        // Burst-heavy combat SFX can stack and distort on low-end mobile CPUs.
        "wave20_swell",
    };
    private static readonly Dictionary<string, int> MobileSfxCooldownMs = new()
    {
        // Rapid-fire tower shots: tight throttle prevents distortion stacking on mobile CPUs.
        ["shoot_rapid"]       = 45,
        ["shoot_marker"]      = 60,
        ["shoot_accordion"]   = 80,
        ["shoot_phase_splitter"] = 70,
        // Heavy impacts: longer cooldown since they're loud and infrequent by design.
        ["shoot_heavy"]       = 90,
        ["mine_pop"]       = 55,
        ["mine_chain_pop"] = 85,
        // Per-frame hit ticks: 35ms floor keeps them audible without drowning other SFX.
        ["hit"]            = 35,
        ["kill_confirm"]   = 40,   // layered kill tick - throttled to avoid pile-up on mass kills
        // Death sounds: slightly longer gap so the mix breathes between kills.
        ["die_basic"]      = 55,
        ["die_armored"]    = 65,
        ["die_swift"]      = 45,
        ["die_reverse"]    = 45,
        ["enemy_rewind"]   = 85,
        // Critical events: long cooldowns since these punctuate the mix, not fill it.
        ["leak"]           = 120,
        ["wave20_swell"]   = 600,
    };

    // Desktop: applied at speed > 1× to prevent burst stacking when many enemies die in one frame.
    // Values are divided by game speed so throttling tightens at higher speeds (floor = 1ms).
    // Goal: at ×3 speed, rapid-fire sounds compress to ~6ms apart - audible but not wall-of-noise.
    private static readonly Dictionary<string, int> DesktopSfxCooldownMs = new()
    {
        // Per-frame hits: very short to feel responsive; throttle only kicks in at high speed.
        ["hit"]            = 12,
        ["kill_confirm"]   = 15,   // matches hit cadence; both fire on the same combat frame
        ["shoot_rapid"]       = 20,
        ["shoot_accordion"]   = 60,
        ["shoot_phase_splitter"] = 50,
        // Death sounds: 60–100ms keeps individual kills audibly distinct at ×2/×3.
        ["die_basic"]      = 60,
        ["die_armored"]    = 100,
        ["die_swift"]      = 50,
        ["die_reverse"]    = 55,
        ["enemy_rewind"]   = 80,
        // Mine events: longer gap since chain-pop can cascade many in a single frame.
        ["mine_pop"]       = 60,
        ["mine_chain_pop"] = 90,
    };

    public override void _Ready()
    {
        Instance  = this;
        _headless = DisplayServer.GetName() == "headless";
        _isMobileAudio = MobileOptimization.IsMobile();
        if (_headless) return;   // skip all audio setup in bot/headless mode

        _pool       = new AudioStreamPlayer[PoolSize];
        _poolTimers = new float[PoolSize];
        _poolStopAtMs = new ulong[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            var gen    = new AudioStreamGenerator { MixRate = Rate, BufferLength = MaxDur };
            var player = new AudioStreamPlayer { Stream = gen, Bus = "FX" };
            AddChild(player);
            _pool[i] = player;
        }

        EnsureFxLimiter();
        EnsureBusLimiter("UI");

        // ── Music note pool ──────────────────────────────────────────────
        _notePool       = new AudioStreamPlayer[NotePoolSize];
        _notePoolStopMs = new ulong[NotePoolSize];
        for (int i = 0; i < NotePoolSize; i++)
        {
            var gen    = new AudioStreamGenerator { MixRate = Rate, BufferLength = NoteBufferLength };
            var player = new AudioStreamPlayer { Stream = gen, VolumeDb = -8f, Bus = "Music" };
            AddChild(player);
            _notePool[i] = player;
        }

        // ── Percussion pool ───────────────────────────────────────────────
        _percPool       = new AudioStreamPlayer[PercPoolSize];
        _percPoolStopMs = new ulong[PercPoolSize];
        for (int i = 0; i < PercPoolSize; i++)
        {
            var gen    = new AudioStreamGenerator { MixRate = Rate, BufferLength = PercBufferLength };
            var player = new AudioStreamPlayer { Stream = gen, VolumeDb = -10f, Bus = "Music" };
            AddChild(player);
            _percPool[i] = player;
        }

        // ── Tower attacks ────────────────────────────────────────────────
        Reg("shoot_rapid",      Tone(680f, 0.05f, vol: 0.26f, shape: 't', env: 'f'));
        Reg("shoot_rapid_cold", Tone(420f, 0.07f, vol: 0.24f, shape: 's', env: 'f'));  // Chill Shot variant: softer, icier
        Reg("shoot_heavy",      Tone( 72f, 0.24f, vol: 0.72f, shape: 's', env: 'f'));
        Reg("shoot_marker",     Tone(360f, 0.07f, vol: 0.32f, shape: 's', env: 'f'));
        Reg("shoot_accordion",  Layer(
            Sweep(860f, 140f, 0.22f, vol: 0.58f),                        // compression fold: high-to-low crunch
            Tone(95f, 0.18f, vol: 0.34f, shape: 'q', env: 'f'),          // mechanical thump
            Tone(2800f, 0.04f, vol: 0.12f, shape: 'q', env: 'f')));      // lock click
        Reg("shoot_phase_splitter", Layer(
            Sweep(1500f, 520f, 0.14f, vol: 0.36f),                       // phase discharge
            Sweep(420f, 980f, 0.11f, vol: 0.16f),                        // spatial split shimmer
            Tone(110f, 0.11f, vol: 0.18f, shape: 'q', env: 'f')));       // core thump

        // ── Enemy events ─────────────────────────────────────────────────
        Reg("hit",          Tone(520f, 0.03f, vol: 0.16f, shape: 'n', env: 'f'));
        Reg("hit_phase_splitter", Layer(
            Tone(760f, 0.035f, vol: 0.11f, shape: 's', env: 'f'),
            Tone(1400f, 0.022f, vol: 0.06f, shape: 'n', env: 'f')));
        Reg("kill_confirm", Tone(1200f, 0.04f, vol: 0.14f, shape: 's', env: 'f'));  // subtle tick layered with die_* on kill
        Reg("die_basic",    Sweep(400f, 170f, 0.14f, vol: 0.55f));
        Reg("die_armored",  Sweep(155f,  50f, 0.24f, vol: 0.70f));
        Reg("die_swift",    Sweep(900f, 400f, 0.08f, vol: 0.45f));
        Reg("die_reverse",  Layer(
            Sweep(860f, 230f, 0.12f, vol: 0.46f),
            Tone(4100f, 0.03f, vol: 0.08f, shape: 'n', env: 'f')));
        Reg("enemy_rewind", Layer(
            Sweep(1650f, 620f, 0.16f, vol: 0.30f),
            Tone(2900f, 0.07f, vol: 0.11f, shape: 'n', env: 'f')));
        Reg("leak",         Sweep(230f,  90f, 0.34f, vol: 0.60f));
        // life_gain: soft rising two-note chime + high shimmer -- "soul collect" feel.
        // Fires up to 5x per wave; pitch is randomized at call site to avoid monotony.
        Reg("life_gain", Layer(
            Seq(new[] { 660f, 990f }, gapMs: 18, noteLen: 0.09f, vol: 0.28f),
            Tone(1980f, 0.06f, vol: 0.09f, shape: 's', env: 'f')));
        Reg("mine_pop", Layer(
            Sweep(220f, 72f, 0.16f, vol: 0.44f),
            Tone(96f, 0.11f, vol: 0.22f, shape: 'q', env: 'f'),
            Tone(1800f, 0.05f, vol: 0.06f, shape: 'n', env: 'f')));
        Reg("mine_chain_pop", Layer(
            Sweep(360f, 84f, 0.24f, vol: 0.68f),
            Tone(180f, 0.14f, vol: 0.24f, shape: 'q', env: 'f'),
            Tone(3200f, 0.06f, vol: 0.10f, shape: 'n', env: 'f'),
            Tone(4100f, 0.024f, vol: 0.10f, shape: 'q', env: 'f')));

        // ── Wave events ──────────────────────────────────────────────────
        Reg("wave_start", Seq(new[] { 300f, 500f },               gapMs: 20, noteLen: 0.09f, vol: 0.52f));
        Reg("wave20_start", Seq(new[] { 140f, 180f, 220f, 310f }, gapMs: 16, noteLen: 0.10f, vol: 0.70f));
        Reg("wave_clear", Seq(new[] { 420f, 560f, 780f },         gapMs: 20, noteLen: 0.10f, vol: 0.58f));
        Reg("game_over",  Seq(new[] { 320f, 250f, 170f },       gapMs: 40, noteLen: 0.22f, vol: 0.65f));
        Reg("victory",    Seq(new[] { 400f, 520f, 680f, 900f }, gapMs: 25, noteLen: 0.12f, vol: 0.62f));

        // ── Surge sounds ─────────────────────────────────────────────────
        Reg("surge", Layer(                                         // Rise and Fall: fast bright rise into heavy low drop
            Sweep(200f, 1800f, 0.14f, vol: 0.60f),
            Sweep(400f, 48f, 0.42f, vol: 0.80f),
            Tone(1000f, 0.06f, vol: 0.50f, shape: 'n', env: 'f'),
            Sweep(1500f, 300f, 0.16f, vol: 0.40f)));

        // ── Lightning surge sound (chain_reaction surges) ────────────────
        Reg("surge_lightning", Thunder(dur: 0.9f, vol: 0.88f));

        // ── Global surge: explosion + expanding shockwave ─────────────────
        Reg("surge_global", GlobalSurge(dur: 8.0f, vol: 0.92f, boomDecay: 0.30f, waveSweepDur: 1.44f, sparkVol: 0.20f));

        // ── UI ───────────────────────────────────────────────────────────
        Reg("draft_pick",      Tone(740f, 0.07f, vol: 0.40f, shape: 's', env: 'f'));
        Reg("ui_card_pick",    Tone(240f, 0.07f, vol: 0.42f, shape: 'q', env: 'f'));
        Reg("ui_preview_ghost", Tone(640f, 0.045f, vol: 0.22f, shape: 's', env: 'f'));
        Reg("ui_lock_in",      Seq(new[] { 320f, 460f }, gapMs: 10, noteLen: 0.08f, vol: 0.60f));
        Reg("ui_lock_in_hard", Seq(new[] { 180f, 320f, 520f }, gapMs: 8, noteLen: 0.09f, vol: 0.76f));
        Reg("ui_thunk",        Tone(120f, 0.06f, vol: 0.34f, shape: 's', env: 'f'));
        Reg("ui_speed_shift",  Sweep(260f, 720f, 0.09f, vol: 0.30f));
        Reg("card_shing",      Sweep(900f, 1650f, 0.08f, vol: 0.25f));
        Reg("ui_select",       Tone(740f, 0.05f, vol: 0.26f, shape: 's', env: 'f'));
        Reg("tower_place",     Seq(new[] { 380f, 560f }, gapMs: 10, noteLen: 0.07f, vol: 0.46f));
        Reg("ui_hover",        Tone(900f, 0.025f, vol: 0.18f, shape: 's', env: 'f'));
        Reg("low_heartbeat",   Seq(new[] { 74f, 58f }, gapMs: 40, noteLen: 0.10f, vol: 0.30f));
        Reg("wave_halfway_lift", Sweep(300f, 980f, 0.16f, vol: 0.26f));
        Reg("wave20_swell",    Sweep(90f, 480f, 0.56f, vol: 0.62f));
        // ── Achievement unlock: bright 4-note C major arpeggio + high shimmer ──
        // Distinct from wave_clear (3-note, lower, shorter) and victory (starts lower, slower).
        // Goes to the UI bus so it won't duck under FX limiter during combat.
        Reg("achievement_unlock", Layer(
            Seq(new[] { 523f, 659f, 784f, 1047f }, gapMs: 12, noteLen: 0.10f, vol: 0.52f),
            Sweep(1600f, 3400f, 0.44f, vol: 0.12f),
            Tone(1047f, 0.07f, vol: 0.14f, shape: 's', env: 'f')));

        // ── Synthesized music notes ──────────────────────────────────────
        // Bass range: MIDI 28–57 (E1–A3). Lead range: MIDI 57–81 (A3–A5).
        // Notes are synthesized once at startup and played via PlayNote(midiNote).
        for (int midi = 28; midi <= 57; midi++)
        {
            float freq = 440f * MathF.Pow(2f, (midi - 69) / 12f);
            Reg($"mnote_{midi}", MusicNote(freq, isBass: true));
        }
        for (int midi = 58; midi <= 81; midi++)
        {
            float freq = 440f * MathF.Pow(2f, (midi - 69) / 12f);
            Reg($"mnote_{midi}", MusicNote(freq, isBass: false));
        }

        // ── Percussion sounds (808-style; played via PlayPerc) ────────────
        // Kick: punchy mid-bass sweep (100→65 Hz), short decay - avoids sub-bass boom.
        Reg("perc_kick",  Layer(
            Sweep(100f, 65f, 0.22f, vol: 0.50f),
            Tone(2000f, 0.005f, vol: 0.22f, shape: 'n', env: 'f')));

        // Snare: mid-freq sweep + broad noise burst for snap.
        Reg("perc_snare", Layer(
            Sweep(260f, 100f, 0.18f, vol: 0.54f),
            Tone(4800f, 0.16f,  vol: 0.42f, shape: 'n', env: 'f')));

        // Closed hat: tight high-freq noise pair, fast decay.
        Reg("perc_hat_c", Layer(
            Tone( 9200f, 0.09f, vol: 0.20f, shape: 'n', env: 'f'),
            Tone(13600f, 0.07f, vol: 0.14f, shape: 'n', env: 'f')));

        // Open hat: same character, slower decay for an open sound.
        Reg("perc_hat_o", Layer(
            Tone( 9200f, 0.28f, vol: 0.20f, shape: 'n', env: 'f'),
            Tone(13600f, 0.24f, vol: 0.14f, shape: 'n', env: 'f')));

        StartMusic();
    }

    // ── Background music ─────────────────────────────────────────────────

    /// <summary>
    /// Starts the background music player and precomputes the initial loop.
    /// Style (ambient vs rock) is read from SettingsManager.
    /// </summary>
    private void StartMusic()
    {
        int style = SettingsManager.Instance?.MenuMusicStyle ?? 0;
        _isAltMusic  = (style == 0);
        _isZoolMusic = (style == 2);
        _altMusicIntroPlayed = false;
        _zoolIntroPlayed     = false;
        if (style == 0)  // Mars
        {
            GenerateWildSolo(_soloLoopSeed);
            _musicFramesFull = BuildMusicFramesAlt(intro: false);
        }
        else if (style == 2)  // Zool
        {
            _musicFramesFull = BuildMusicFramesZool(intro: false);
        }
        _musicFrames = style switch
        {
            1 => BuildMusicFramesClassic(),
            2 => BuildMusicFramesZool(intro: true),
            _ => BuildMusicFramesAlt(intro: true),
        };

        var gen = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.5f };
        _musicPlayer = new AudioStreamPlayer { Stream = gen, VolumeDb = MusicBaseDb, Bus = "Music" };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
        _musicPlayback = (AudioStreamGeneratorPlayback)_musicPlayer.GetStreamPlayback();
    }

    /// <summary>
    /// Switches the menu music style while already playing. Safe to call from Settings UI.
    /// Replaces the precomputed loop buffer immediately; the generator's internal 0.5 s
    /// buffer drains the old track, then the new style starts from position 0.
    /// </summary>
    public void SetMenuMusicStyle(int style)
    {
        if (_headless) return;
        if (_musicPlayer == null) return;  // not started yet; StartMusic() will read the setting
        _isAltMusic  = (style == 0);
        _isZoolMusic = (style == 2);
        _altMusicIntroPlayed = false;
        _zoolIntroPlayed     = false;
        if (style == 0)  // Mars
        {
            GenerateWildSolo(_soloLoopSeed);
            _musicFramesFull = BuildMusicFramesAlt(intro: false);
        }
        else if (style == 2)  // Zool
        {
            _musicFramesFull = BuildMusicFramesZool(intro: false);
        }
        _musicFrames = style switch
        {
            1 => BuildMusicFramesClassic(),
            2 => BuildMusicFramesZool(intro: true),
            _ => BuildMusicFramesAlt(intro: true),
        };
        _musicPos = 0;
    }

    /// <summary>
    /// Precomputes a 32-second funky ambient loop.
    /// Key: A major 9th (A, C#, E, F#, B) -- warm, soulful, not minor/sad.
    /// A half-wave-rectified groove pulse on the bright layer bounces every 1 s
    /// for a rhythmic feel without percussion. Swell periods are faster (4/8 s)
    /// so the mix feels lively rather than slow and droney.
    /// All swell periods divide 32 s evenly for a seamless loop.
    /// </summary>
    private static Vector2[] BuildMusicFramesClassic()
    {
        const float LoopDur = 32f;
        int n = (int)(Rate * LoopDur);
        var frames = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // ── Root: A2, light sub-bass anchor ──────────────────────────────
            float root = (MathF.Sin(t * MathF.Tau * 110.00f)
                        + MathF.Sin(t * MathF.Tau * 110.08f)
                        + MathF.Sin(t * MathF.Tau * 109.92f)) * 0.008f;

            // ── Fifth: E3, 8-s swell (was 32 s -- faster = more energy) ──────
            float fifthSwell = 0.45f + 0.40f * MathF.Sin(t * MathF.Tau / 8f - MathF.PI * 0.4f);
            float fifth = (MathF.Sin(t * MathF.Tau * 164.81f)
                         + MathF.Sin(t * MathF.Tau * 164.97f)) * 0.048f * fifthSwell;

            // ── Mid pad: A3 + C#4 + E4 -- MAJOR triad (C# not C, no more whine)
            // C#4 = 277.18 Hz replaces C4 = 261.63 Hz; 4-s swell keeps it lively
            float padSwell = 0.50f + 0.42f * MathF.Sin(t * MathF.Tau / 4f + MathF.PI * 0.3f);
            float pad = (MathF.Sin(t * MathF.Tau * 220.00f) + MathF.Sin(t * MathF.Tau * 220.30f) + MathF.Sin(t * MathF.Tau * 219.70f)) * 0.060f  // A3
                      + (MathF.Sin(t * MathF.Tau * 277.18f) + MathF.Sin(t * MathF.Tau * 277.48f)) * 0.048f  // C#4 (major 3rd)
                      + (MathF.Sin(t * MathF.Tau * 329.63f) + MathF.Sin(t * MathF.Tau * 329.95f)) * 0.036f; // E4
            pad *= padSwell;

            // ── Jazz 6th: F#4 -- major 6th adds soulful funk colour, 8-s swell ─
            float jazzSwell = 0.28f + 0.25f * MathF.Sin(t * MathF.Tau / 8f + MathF.PI * 0.7f);
            float jazz = (MathF.Sin(t * MathF.Tau * 369.99f)
                        + MathF.Sin(t * MathF.Tau * 370.38f)) * 0.038f * jazzSwell;

            // ── 9th: B4 -- Amaj9 richness, gives that smooth soul/funk feel ────
            float ninthSwell = 0.22f + 0.20f * MathF.Sin(t * MathF.Tau / 4f + MathF.PI);
            float ninth = (MathF.Sin(t * MathF.Tau * 493.88f)
                         + MathF.Sin(t * MathF.Tau * 494.28f)) * 0.030f * ninthSwell;

            // ── Bright A4: 1-s groove bounce (half-wave rectified = punchy) ───
            // Positive half of 1 Hz sine fires 60 accents/min -- rhythmic without drums.
            // 1 s divides 32 s evenly (32 cycles). Base level 0.20 keeps it present
            // between accents; peak 0.75 gives a clear forward push.
            float groovePulse = 0.20f + 0.55f * MathF.Max(0f, MathF.Sin(t * MathF.Tau / 1f));
            float bright = (MathF.Sin(t * MathF.Tau * 440.00f)
                          + MathF.Sin(t * MathF.Tau * 440.60f)
                          + MathF.Sin(t * MathF.Tau * 439.40f)) * 0.062f * groovePulse;

            // ── Shimmer: E5 + B5, bright sparkle, 8-s swell ─────────────────
            // B5 (987.77 Hz) replaces A5 (880 Hz) -- airier, less droney high end
            float shimSwell = 0.32f + 0.30f * MathF.Sin(t * MathF.Tau / 8f + MathF.PI * 0.5f);
            float shimmer = (MathF.Sin(t * MathF.Tau * 659.25f)
                           + MathF.Sin(t * MathF.Tau * 659.85f)) * 0.032f * shimSwell
                          + (MathF.Sin(t * MathF.Tau * 987.77f)
                           + MathF.Sin(t * MathF.Tau * 988.42f)) * 0.020f * shimSwell;

            float s = Mathf.Clamp(root + fifth + pad + jazz + ninth + bright + shimmer, -1f, 1f);
            frames[i] = new Vector2(s, s);
        }

        return frames;
    }

    /// <summary>
    /// Precomputes a 32-second hard-driving synth-rock loop inspired by
    /// "Rock 'n' Zool" by Patrick Phelan (Amiga MOD, Zool 1992).
    ///
    /// Key: Ab/G# blues (G#, A#, C, D#, F).  Tempo: 90 BPM (12 bars / 32 s).
    ///
    /// The defining feature of Rock N Zool is a FAST 2-octave descending shred
    /// run that races through the Ab scale on 16th notes (~160 ms/note, matching
    /// the original MOD).  Everything else -- chug, kick, snare, pad -- provides
    /// the driving backdrop for that lead cascade.
    /// <summary>
    /// PCM recreation of "Rock N Zool" (Patrick Phelan, Zool 1992, Amiga).
    /// Reconstructed note-for-note from the original ProTracker MOD file.
    /// Tempo: CIA 125 BPM, speed=4 → 80 ms/row = 1764 samples/row @ 22050 Hz.
    /// Key: F minor (F Gb Ab Bb C Db Eb).
    ///
    /// intro=true  → Pattern 0 only (64 rows, ~5.1 s)
    /// intro=false → Patterns 3+7+3+8 (256 rows, ~20.5 s main loop)
    /// </summary>
    private static Vector2[] BuildMusicFramesZool(bool intro)
    {
        const int RowSamples = 1764;   // 80 ms row @ 22050 Hz
        int rows = intro ? 64 : 256;
        int n    = rows * RowSamples;
        var mix  = new float[n];

        static float MidiFreq(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);

        // ── Kick (freq-swept sine, 100→40 Hz) ───────────────────────────────
        int[] kickRows;
        if (intro)
        {
            kickRows = new[] { 0 };
        }
        else
        {
            int[] baseKick = { 0,8,10,16,22,26,32,40,42,48,56,62 };
            var klist = new List<int>();
            for (int p = 0; p < 4; p++)
                foreach (int r in baseKick) klist.Add(p * 64 + r);
            kickRows = klist.ToArray();
        }
        const int KickSamp = (int)(0.28f * Rate);
        foreach (int row in kickRows)
        {
            int start = row * RowSamples;
            for (int i = 0; i < KickSamp && start + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Exp(-18f * tt) * 0.9f;
                float ph  = (100f / 30f) * (1f - MathF.Exp(-30f * tt)) + 40f * tt;
                mix[start + i] += MathF.Sin(ph * MathF.Tau) * env * 0.55f;
            }
        }

        // ── Snare (noise + 185 Hz tone) ──────────────────────────────────────
        int[] snareRows;
        if (intro)
        {
            snareRows = Array.Empty<int>();
        }
        else
        {
            int[] baseSnare = { 4,12,20,28,36,44,52,54,60 };
            var slist = new List<int>();
            for (int p = 0; p < 4; p++)
                foreach (int r in baseSnare) slist.Add(p * 64 + r);
            snareRows = slist.ToArray();
        }
        const int SnareSamp = (int)(0.22f * Rate);
        var noiseRng = new Random(42);
        var noise    = new float[SnareSamp];
        for (int i = 0; i < SnareSamp; i++) noise[i] = (float)(noiseRng.NextDouble() * 2.0 - 1.0);
        foreach (int row in snareRows)
        {
            int start = row * RowSamples;
            for (int i = 0; i < SnareSamp && start + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Exp(-28f * tt) * 0.85f;
                mix[start + i] += (noise[i] * 0.6f + MathF.Sin(tt * MathF.Tau * 185f) * 0.4f) * env * 0.40f;
            }
        }

        // ── Hi-hat (16th-note closed, inharmonic metallic sines) ────────────
        // Fires every 2 rows = 160 ms = 16th note @ ~94 BPM. Accented on 8th notes (row%4==0).
        const int HatLen = (int)(0.038f * Rate);
        for (int hrow = 0; hrow < rows; hrow += 2)
        {
            bool isAccent = (hrow % 4 == 0);
            float hatVol  = isAccent ? 0.12f : 0.06f;
            int   hstart  = hrow * RowSamples;
            for (int i = 0; i < HatLen && hstart + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Exp(-95f * tt);
                // Inharmonic high-freq sines → metallic closed hi-hat timbre
                float h = MathF.Sin(tt * MathF.Tau * 7800f) * 0.38f
                        + MathF.Sin(tt * MathF.Tau * 9300f) * 0.30f
                        + MathF.Sin(tt * MathF.Tau * 11600f) * 0.22f
                        + MathF.Sin(tt * MathF.Tau * 13800f) * 0.10f;
                mix[hstart + i] += h * env * hatVol;
            }
        }

        // ── Chug (distorted sawtooth, staccato 70 ms) ───────────────────────
        var chugEvents = new List<(int row, int midi)>();
        if (intro)
        {
            // F minor pentatonic chug (Ab4,Ab4,Bb4,F4 pattern) - in key with bass and melody.
            int[] cp = { 68,68,70,65,68,70,68,65 };  // Ab4,Ab4,Bb4,F4,Ab4,Bb4,Ab4,F4
            for (int r = 0; r < 64; r += 2) chugEvents.Add((r, cp[(r / 2) % cp.Length]));
        }
        else
        {
            // F minor pentatonic / Dorian chug - all notes in key with bass and melody.
            // Main phrase: Ab4-Ab4-Bb4-F4 (minor3rd / 4th / root) - funky root-bounce pattern.
            int[] chugPhrase = { 68,68,70,65,68,70,68,65,68,68,70,65,68,70,74,68 };
            // Settle groove second half: F4-F4-Ab4-F4 - tonic pedal with root colour.
            int[] chugGroove = { 65,65,68,65,65,68,65,65,65,65,68,65,65,65,68,65 };
            // P8 high section: keep chug in low register (Ab4/Bb4/F4) so it doesn't
            // compete with the high melody (Ab5/F5) in the same octave.
            int[] cHigh = { 68,68,70,65,68,70,68,68 };
            for (int p = 0; p < 4; p++)
            {
                for (int r = 0; r < 64; r += 2)
                {
                    int midi;
                    if (p == 3)
                    {
                        bool isHi = (r >= 16 && r <= 30) || (r >= 48 && r <= 62);
                        if (isHi)
                        {
                            int hi = (r < 32 ? r - 16 : r - 48) / 2;
                            midi = cHigh[hi % cHigh.Length];
                        }
                        else
                        {
                            int idx = (r < 32 ? r : r - 32) / 2;
                            midi = chugPhrase[idx % 8];
                        }
                    }
                    else
                    {
                        midi = r < 32 ? chugPhrase[(r / 2) % chugPhrase.Length]
                                      : chugGroove[((r - 32) / 2) % chugGroove.Length];
                    }
                    chugEvents.Add((p * 64 + r, midi));
                }
            }
        }
        // Chug: very short staccato "chk" - rhythm guitar mute, not a sustained pitched tone.
        // 25 ms gate + fast decay so pitch content is minimal; it reads as rhythmic texture.
        const int ChugGate = (int)(0.025f * Rate);
        foreach (var (row, midi) in chugEvents)
        {
            int   start = row * RowSamples;
            // Chug gate is 25 ms - percussive attack only, pitch content is negligible.
            // Use equal-temperament MIDI freq so chug, bass, and melody share the same tuning reference.
            float freq  = MidiFreq(midi);
            float phase = 0f;
            for (int i = 0; i < ChugGate && start + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Exp(-40f * tt) * (1f - MathF.Exp(-800f * tt));
                phase += freq / Rate;
                float saw = (phase % 1f) * 2f - 1f;
                mix[start + i] += MathF.Tanh(saw * 2.0f) * env * 0.12f;
            }
        }

        // ── Melody (sine + harmonics, note-for-note from MOD) ───────────────
        var mel = new List<(int row, int midi)>();
        if (intro)
        {
            mel.Add((0, 65));  // F4 (period 160), fades out over pattern
        }
        else
        {
            // Correct MIDI: MIDI=60+12*log2(428/period). Period 428=C4=MIDI60.
            // Pattern 3 sparse melody (rows 0-63)
            int[] p3r = { 0,24,32,56,60 };
            int[] p3m = { 65,65,63,63,68 };  // F4,F4,Eb4,Eb4,G#4
            for (int i = 0; i < p3r.Length; i++) mel.Add((p3r[i], p3m[i]));
            // Pattern 3 repeat (rows 128-191)
            for (int i = 0; i < p3r.Length; i++) mel.Add((128 + p3r[i], p3m[i]));
            // Pattern 7 main riff (rows 64-127)
            int[] p7r = { 0,2,4,6,8,10,12,14,16,18,20,22,24,25,26,27,28,29,30,31 };
            int[] p7m = { 80,75,77,80,75,77,80,75,77,72,70,72,75,72,70,68,65,63,65,63 };
            for (int i = 0; i < p7r.Length; i++) mel.Add((64 + p7r[i], p7m[i]));
            // Pattern 7 second half (rows 32-62)
            int[] p7ar = { 32,42,44,46,48,50,52,54,56,58,60,62 };
            int[] p7am = { 63,65,68,70,68,70,72,70,72,75,77,75 };
            for (int i = 0; i < p7ar.Length; i++) mel.Add((64 + p7ar[i], p7am[i]));
            // Pattern 8 melody (rows 192-255)
            int[] p8r = { 0,4,8,10,12,14,18,26,28,30,36,40,42,44,46,58,60,62 };
            int[] p8m = { 65,80,80,79,77,75,77,70,72,63,77,77,75,74,70,68,70,63 };
            for (int i = 0; i < p8r.Length; i++) mel.Add((192 + p8r[i], p8m[i]));
        }
        for (int evIdx = 0; evIdx < mel.Count; evIdx++)
        {
            var (row, midi) = mel[evIdx];
            int nextRow  = evIdx + 1 < mel.Count ? mel[evIdx + 1].row : rows;
            int gate     = Math.Min(nextRow - row, 8) * RowSamples;
            int start    = row * RowSamples;
            float freq   = MidiFreq(midi);
            float totalT = gate / (float)Rate;
            for (int i = 0; i < gate && start + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Min(tt / 0.012f, 1f)
                          * MathF.Min((totalT - tt) / 0.04f, 1f);
                if (intro) env *= MathF.Max(0f, 1f - (start + i) / (float)(64 * RowSamples));
                // Electric guitar lead: richer harmonics + light tanh bite
                float raw = (MathF.Sin(tt * MathF.Tau * freq)        * 0.55f
                           + MathF.Sin(tt * MathF.Tau * freq * 2f)   * 0.25f
                           + MathF.Sin(tt * MathF.Tau * freq * 3f)   * 0.12f
                           + MathF.Sin(tt * MathF.Tau * freq * 4f)   * 0.06f) * env;
                float s = MathF.Tanh(raw * 1.4f);  // peak ≈ 0.88 → ×0.55 ≈ 0.48 (near original 0.50)
                mix[start + i] += s * 0.55f;
            }
        }

        // ── Bass (sine + octave, actual MOD Ch4 note schedule) ───────────────
        var bass = new List<(int row, int midi)>();
        if (intro)
        {
            bass.Add((0, 68));  // Ab4 (period 269, MIDI=60+12*log2(428/269)=68)
        }
        else
        {
            // P3 and P7 bass: Ab4 root pedal with Bb4 (4th) bounce - classic F minor groove.
            // Original Gb4(66) and B4(71) caused half-step and tritone clashes with the melody.
            int[] p37BassRows = { 0, 24, 28, 32, 60 };
            int[] p37BassMidi = { 68, 68, 70, 68, 70 };  // Ab4, Ab4, Bb4, Ab4, Bb4
            for (int p = 0; p < 3; p++)  // patterns 3+7+3 at offsets 0,64,128
                for (int i = 0; i < p37BassRows.Length; i++)
                    bass.Add((p * 64 + p37BassRows[i], p37BassMidi[i]));
            // Pattern 8 bass (offset 192): original high runs (G#5=80, F#5=78, C#5=73) clash with
            // melody in the same register. Keep G#4(68) anchor notes as-is; drop only the high
            // runs by one octave: 80→68, 78→66, 73→61. Bass stays in G#4/F#4/C#4 range (~285-428 Hz),
            // below the melody (~830 Hz). Sample-rate correction then applies as normal.
            int[] p8BassRows = { 0,4,6,8,12,14,16,20,22,24,28,32,36,38,40,44,46,48,52,54,56,60 };
            // Gb4(66) replaced with F4(65) - tonic root; Db4(61) kept (bVI = natural minor 6th).
            int[] p8BassMidi = { 68,68,68,65,68,68,61,68,61,65,68,68,68,68,65,68,68,61,68,61,65,68 };
            for (int i = 0; i < p8BassRows.Length; i++)
                bass.Add((192 + p8BassRows[i], p8BassMidi[i]));
        }
        for (int evIdx = 0; evIdx < bass.Count; evIdx++)
        {
            var (row, midi) = bass[evIdx];
            int nextRow  = evIdx + 1 < bass.Count ? bass[evIdx + 1].row : rows;
            int gate     = (nextRow - row) * RowSamples;
            int start    = row * RowSamples;
            // Use equal-temperament MIDI freq so bass sits exactly an octave below the melody
            // (e.g. G#4=415 Hz is a perfect octave below G#5=831 Hz). Sample-rate corrections
            // shifted bass +50¢ sharp vs melody, causing audible dissonance.
            float freq   = MidiFreq(midi);
            float totalT = gate / (float)Rate;
            for (int i = 0; i < gate && start + i < n; i++)
            {
                float tt  = i / (float)Rate;
                float env = MathF.Min(tt / 0.010f, 1f)
                          * Math.Clamp((totalT - tt) / 0.05f, 0f, 1f);
                // Electric bass: fundamental + harmonics for body and presence
                float s = (MathF.Sin(tt * MathF.Tau * freq)      * 0.60f
                         + MathF.Sin(tt * MathF.Tau * freq * 2f) * 0.25f
                         + MathF.Sin(tt * MathF.Tau * freq * 3f) * 0.10f) * env;
                mix[start + i] += s * 0.30f;
            }
        }

        // ── Soft limit → stereo ──────────────────────────────────────────────
        var frames = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float s = MathF.Tanh(mix[i] * 0.50f);
            frames[i] = new Vector2(s, s);
        }
        return frames;
    }

    ///
    /// Loop guarantee: all frequencies are N/32 Hz (integer N) so every sine
    /// completes exactly N cycles in 32 s → signal is 0 at t=0 and t=32, no click.
    /// </summary>
    private static Vector2[] BuildMusicFramesAlt(bool intro = true)
    {
        const float LoopDur   = 32f;
        const float BPM       = 90f;
        const float Beat      = 60f / BPM;    // 0.6667 s quarter note
        const float Eighth    = Beat / 2f;    // 0.3333 s 8th note
        // Note: Sixteenth = Beat/4 used by real-time lead layer in _Process, not the precomputed buffer.
        const float Bar       = Beat * 4f;    // 2.6667 s bar  (12 bars = 32 s)

        // Loop-safe frequencies: N/32 Hz, N = round(pitch × 32).
        const float GS2  = 3323f  / 32f;  // G#2  ≈ 103.84 Hz  root bass
        const float FS2  = 2960f  / 32f;  // F#2  =  92.50 Hz  b7 bass (bar 4 each group)
        const float AS2  = 3729f  / 32f;  // A#2  ≈ 116.53 Hz  chug alt
        const float GS3  = 6645f  / 32f;  // G#3  ≈ 207.66 Hz  pad root
        const float DS4  = 9956f  / 32f;  // D#4  ≈ 311.13 Hz  pad 5th
        const float GS4  = 13290f / 32f;  // G#4  ≈ 415.31 Hz  pad oct

        // Lead melody is fully real-time (see _Process / GenerateWildSolo).
        // This buffer carries only the backing track: bass + chug + drums + pad.
        // Buildup envelope schedule (all thresholds are bar numbers):
        //   Bar 0      -- kick only (anchor, sparse)
        //   Bar 1      -- bass fades in
        //   Bar 2      -- snare enters
        //   Bar 3      -- chug enters at quarter-note pace, low drive
        //   Bar 5      -- chug doubles to 8th notes, drive increases
        //   Bar 5      -- pad fades in
        //   Bar 7      -- full intensity, hi-hat enters

        int n = (int)(Rate * LoopDur);
        var frames = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // ── Per-element buildup envelopes (intro only) ──────────────────
            float bassVol, snareVol, chugVol, padVol, hatVol, kickVol;
            float chugInterval, drive;
            if (intro)
            {
                bassVol      = Mathf.Min(t / (Bar * 0.6f), 1f);
                snareVol     = t < Bar       ? 0f : Mathf.Min((t - Bar)       / (Bar * 0.5f), 1f);
                chugVol      = t < Bar * 1.5f? 0f : Mathf.Min((t - Bar * 1.5f)/ (Bar * 0.4f), 1f);
                padVol       = t < Bar * 2.5f? 0f : Mathf.Min((t - Bar * 2.5f)/ (Bar * 0.6f), 1f);
                hatVol       = t < Bar * 4f  ? 0f : Mathf.Min((t - Bar * 4f)  / (Bar * 0.3f), 1f);
                kickVol      = 0.35f + 0.65f * Mathf.Min(t / (Bar * 2f), 1f);
                chugInterval = t < Bar * 3f ? Beat : Eighth;
                drive        = 1.2f + 0.8f * Mathf.Min((t - Bar * 1.5f) / (Bar * 3f), 1f);
            }
            else // full intensity: all layers on, chug at 8th pace, full drive
            {
                bassVol = snareVol = chugVol = padVol = hatVol = kickVol = 1f;
                chugInterval = Eighth;
                drive = 2.0f;
            }

            // ── Bass: G#2 root; F#2 on bar 4 of every 4-bar group ──────────
            int   barNum   = (int)(t / Bar);
            float barPos   = (t - barNum * Bar) / Bar;
            float bassFreq = (barNum % 4 == 3) ? FS2 : GS2;
            float bassEnv  = Mathf.Min(barPos / 0.03f, 1f);
            float bass     = (MathF.Sin(t * MathF.Tau * bassFreq)      * 0.10f
                           +  MathF.Sin(t * MathF.Tau * bassFreq * 2f) * 0.028f) * bassEnv * bassVol;
            float chugStep     = (t / chugInterval) % 2f;
            float chugFreq     = chugStep < 1f ? GS2 : AS2;
            float chugBeat     = chugStep % 1f;
            float chugEnv      = MathF.Exp(-chugBeat * 12f);
            float chugWave     = MathF.Sin(t * MathF.Tau * chugFreq)
                               + MathF.Sin(t * MathF.Tau * chugFreq * 3f) * 0.45f
                               + MathF.Sin(t * MathF.Tau * chugFreq * 5f) * 0.18f;
            float chugDriven   = chugWave * drive;
            float chug         = (chugDriven / (1f + MathF.Abs(chugDriven))) * 0.060f * chugEnv * chugVol;

            // ── Kick: every beat, integrated 160→50 Hz pitch sweep ──────────
            float kickPos = (t / Beat) - MathF.Floor(t / Beat);
            float tKick   = kickPos * Beat;
            float kPhase  = 160f * (1f - MathF.Exp(-tKick * 25f)) / 25f + 50f * tKick;
            float kick    = MathF.Sin(MathF.Tau * kPhase) * MathF.Exp(-tKick * 18f) * 0.082f * kickVol;

            // ── Snare: beats 2 and 4 ────────────────────────────────────────
            float beatNum = MathF.Floor(t / Beat);
            float tSnare  = t - beatNum * Beat;
            float snare   = 0f;
            if ((int)beatNum % 2 == 1)
            {
                float sdecay = MathF.Exp(-tSnare * 26f);
                snare = ((MathF.Sin(t * MathF.Tau * 285f)
                        + MathF.Sin(t * MathF.Tau * 437f)
                        + MathF.Sin(t * MathF.Tau * 612f)) * 0.030f * sdecay
                        + MathF.Sin(t * MathF.Tau * 180f)  * 0.018f * MathF.Exp(-tSnare * 35f))
                       * snareVol;
            }

            // ── Hi-hat: 8th-note metallic clicks enter at bar 7 ─────────────
            float hatStep  = (t / Eighth) % 1f;
            float tHat     = hatStep * Eighth;
            float hat      = (MathF.Sin(t * MathF.Tau * 4600f)
                            + MathF.Sin(t * MathF.Tau * 5900f) * 0.75f
                            + MathF.Sin(t * MathF.Tau * 7300f) * 0.50f
                            + MathF.Sin(t * MathF.Tau * 9100f) * 0.30f)
                            * MathF.Exp(-tHat * 90f) * 0.009f * hatVol;

            // ── Lead is fully real-time; precomputed buffer carries 0 here ──
            const float lead = 0f;

            // ── Pad: G#3 + D#4 + G#4, enters at bar 5 ─────────────────────
            float padSwell = 0.42f + 0.30f * MathF.Sin(t * MathF.Tau / (Bar * 1.5f));
            float pad = (MathF.Sin(t * MathF.Tau * GS3)
                       + MathF.Sin(t * MathF.Tau * DS4) * 0.65f
                       + MathF.Sin(t * MathF.Tau * GS4) * 0.25f) * 0.020f * padSwell * padVol;

            float s = Mathf.Clamp(bass + chug + kick + snare + hat + lead + pad, -1f, 1f);
            frames[i] = new Vector2(s, s);
        }

        return frames;
    }

    /// <summary>
    /// Generates a wild, chaotic 3-bar guitar solo that plays differently every loop.
    /// Full chromatic range G#3–A#5, with huge jumps, screaming high notes, wide
    /// vibrato (±1 semitone+), and a forced dive-bomb ending. Seeded so consecutive
    /// loops are always distinct.
    /// </summary>
    private void GenerateWildSolo(int seed)
    {
        // Full chromatic pool G#3 to D6 (31 notes, G#3=0...D6=30)
        float[] pool =
        {
            207.652f, 220.000f, 233.082f, 246.942f,
            261.626f, 277.183f, 293.665f, 311.127f, 329.628f, 349.228f, 369.994f, 391.995f,
            415.305f, 440.000f, 466.164f, 493.883f,
            523.251f, 554.365f, 587.330f, 622.254f, 659.255f, 698.456f, 739.989f, 783.991f,
            830.609f, 880.000f, 932.328f, 987.767f, 1046.502f, 1108.731f, 1174.659f,
        };
        const int PoolLen = 31;

        var rng  = new System.Random(seed);
        var freq = new float[SoloNoteCount];
        var vib  = new bool[SoloNoteCount];
        var vol  = new float[SoloNoteCount];
        var bnd  = new float[SoloNoteCount]; // slide-in start freq; 0 = no bend

        // G# minor pentatonic indices into pool: G#3,A#3,C4,D#4,F4,G#4,A#4,C5,D#5,F5,G#5,A#5,C6
        int[] penta    = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24, 26, 28 };
        const int PentaLen = 13;

        int NearestPenta(int poolIdx)
        {
            int best = 0;
            for (int k = 1; k < PentaLen; k++)
                if (Math.Abs(penta[k] - poolIdx) < Math.Abs(penta[best] - poolIdx))
                    best = k;
            return best;
        }

        int cur       = penta[5 + rng.Next(4)]; // G#4-D#5 start zone
        int pp        = 5 + rng.Next(4);
        int n         = 0;
        int diveStart = SoloNoteCount - 14;

        while (n < diveStart)
        {
            int rem = diveStart - n;
            double ph = rng.NextDouble();

            if (ph < 0.22) // pentatonic blues run ascending
            {
                int len = Math.Min(7 + rng.Next(8), rem);
                for (int k = 0; k < len; k++, n++)
                {
                    pp = Math.Clamp(pp + 1, 0, PentaLen - 1);  cur = penta[pp];
                    freq[n] = pool[cur];  vib[n] = false;  vol[n] = 1f;
                    bnd[n]  = k == 0 && rng.NextDouble() < 0.45 ? pool[Math.Max(0, cur - 1)] : 0f;
                }
            }
            else if (ph < 0.44) // pentatonic blues run descending
            {
                int len = Math.Min(7 + rng.Next(8), rem);
                for (int k = 0; k < len; k++, n++)
                {
                    pp = Math.Clamp(pp - 1, 0, PentaLen - 1);  cur = penta[pp];
                    freq[n] = pool[cur];  vib[n] = false;  vol[n] = 1f;
                    bnd[n]  = k == 0 && rng.NextDouble() < 0.45 ? pool[Math.Min(PoolLen - 1, cur + 1)] : 0f;
                }
            }
            else if (ph < 0.56) // octave leap + bends (pentatonic-snapped target)
            {
                int len = Math.Min(3 + rng.Next(6), rem);
                int dir = rng.NextDouble() < 0.5 ? 5 : -5; // ~5 penta steps ≈ 1 octave
                for (int k = 0; k < len; k++, n++)
                {
                    pp  = Math.Clamp(k % 2 == 0 ? pp + dir : pp - dir + rng.Next(3) - 1, 0, PentaLen - 1);
                    cur = penta[pp];
                    freq[n] = pool[cur];
                    vib[n]  = rng.NextDouble() < 0.25;
                    vol[n]  = 1f;
                    bnd[n]  = rng.NextDouble() < 0.55 ? pool[Math.Clamp(cur + (dir > 0 ? -2 : 2), 0, PoolLen - 1)] : 0f;
                }
            }
            else if (ph < 0.68) // ghost-note funk
            {
                int len = Math.Min(5 + rng.Next(7), rem);
                for (int k = 0; k < len; k++, n++)
                {
                    bool ghost = k % 3 == 1;
                    if (!ghost)
                    {
                        pp  = Math.Clamp(pp + (rng.NextDouble() < 0.5 ? 1 : -1) * (1 + rng.Next(2)), 0, PentaLen - 1);
                        cur = penta[pp];
                    }
                    freq[n] = pool[cur];
                    vib[n]  = !ghost && rng.NextDouble() < 0.18;
                    vol[n]  = ghost ? 0.22f : 1f;
                    bnd[n]  = 0f;
                }
            }
            else if (ph < 0.80) // machine-gun stutter → chromatic slide to next penta note
            {
                int len = Math.Min(3 + rng.Next(4), rem);
                for (int k = 0; k < len; k++, n++)
                {
                    freq[n] = pool[cur];
                    vib[n]  = k == len - 1 && rng.NextDouble() < 0.35;
                    vol[n]  = k % 2 == 0 ? 1f : 0.35f;
                    bnd[n]  = 0f;
                }
                if (n < diveStart && rng.NextDouble() < 0.6)
                {
                    pp = Math.Clamp(pp + (rng.NextDouble() < 0.5 ? 1 : -1), 0, PentaLen - 1);
                    cur = penta[pp];
                    freq[n] = pool[cur];  vib[n] = false;  vol[n] = 1f;
                    bnd[n] = pool[Math.Clamp(cur + (rng.NextDouble() < 0.5 ? -1 : 1), 0, PoolLen - 1)];
                    n++;
                }
            }
            else // blues lick: pentatonic steps with chromatic passing tone
            {
                int len = Math.Min(3 + rng.Next(4), rem);
                for (int k = 0; k < len; k++, n++)
                {
                    if (k % 3 == 2) // brief chromatic passing tone
                    {
                        int pass = Math.Clamp(cur + (rng.NextDouble() < 0.5 ? 1 : -1), 0, PoolLen - 1);
                        freq[n] = pool[pass];  vib[n] = false;  vol[n] = 1f;  bnd[n] = 0f;
                    }
                    else
                    {
                        pp  = Math.Clamp(pp + (rng.NextDouble() < 0.6 ? 1 : -1) * (1 + rng.Next(2)), 0, PentaLen - 1);
                        cur = penta[pp];
                        freq[n] = pool[cur];
                        vib[n]  = rng.NextDouble() < 0.22;
                        vol[n]  = 1f;
                        bnd[n]  = rng.NextDouble() < 0.30 ? pool[Math.Clamp(cur - 1, 0, PoolLen - 1)] : 0f;
                    }
                }
            }
        }

        while (n < diveStart) { freq[n] = pool[cur]; vol[n] = 1f; n++; }

        // Dive bomb: pentatonic sweep down with glide bends
        pp = NearestPenta(cur);
        for (; n < SoloNoteCount; n++)
        {
            pp  = Math.Max(0, pp - 1 - rng.Next(2));
            cur = penta[pp];
            freq[n] = pool[cur];  vib[n] = false;  vol[n] = 1f;
            bnd[n]  = n < diveStart + 5 ? freq[n - 1] : 0f;
        }

        _soloNoteFreqs   = freq;
        _soloNoteVibrato = vib;
        _soloNoteVol     = vol;
        _soloNoteBend    = bnd;

        // ── Lead layer: bars 0-8 (144 sixteenth-note slots) ─────────────────
        // Lead pool: F3-A#5 chromatic (30 notes, no giant gap)
        float[] lPool =
        {
            174.614f, 185.000f, 196.000f, 207.652f, 220.000f, 233.082f, // F3-A#3
            246.942f, 261.626f, 277.183f, 293.665f, 311.127f, 329.628f, // B3-E4
            349.228f, 369.994f, 391.995f, 415.305f, 440.000f, 466.164f, // F4-A#4
            493.883f, 523.251f, 554.365f, 587.330f, 622.254f, 659.255f, // B4-E5
            698.456f, 739.989f, 783.991f, 830.609f, 880.000f, 932.328f, // F5-A#5
        };
        const int LPoolLen = 30;

        // G# minor pentatonic in lPool: G#3=3, A#3=5, C4=7, D#4=10, F4=12, G#4=15, A#4=17, C5=19, D#5=22, F5=24, G#5=27, A#5=29
        int[] lpenta   = { 3, 5, 7, 10, 12, 15, 17, 19, 22, 24, 27, 29 };
        const int LPentaLen = 12;

        var lFreq = new float[LeadNoteCount];
        var lVib  = new bool[LeadNoteCount];
        var lVol  = new float[LeadNoteCount];
        var lBnd  = new float[LeadNoteCount];

        // Improv: blues-pentatonic phrases (changes every loop via seed walk)
        {
            var lrng = new System.Random(seed ^ 0xABCD);
            int lp   = 4 + lrng.Next(3); // start G#4-A#4 zone
            int lcur = lpenta[lp];
            int ln   = 0;

            while (ln < LeadNoteCount)
            {
                int rem  = LeadNoteCount - ln;
                double lph = lrng.NextDouble();

                if (lph < 0.25) // scale walk (ascending or descending)
                {
                    int len = Math.Min(5 + lrng.Next(9), rem);
                    int dir = lrng.NextDouble() < 0.5 ? 1 : -1;
                    for (int k = 0; k < len; k++, ln++)
                    {
                        lp = Math.Clamp(lp + dir, 0, LPentaLen - 1);  lcur = lpenta[lp];
                        lFreq[ln] = lPool[lcur];
                        lVib[ln]  = k == len - 1 && lrng.NextDouble() < 0.30;
                        lVol[ln]  = 1f;
                        lBnd[ln]  = k == 0 && lrng.NextDouble() < 0.40
                            ? lPool[Math.Clamp(lcur + (dir > 0 ? -1 : 1), 0, LPoolLen - 1)] : 0f;
                    }
                }
                else if (lph < 0.45) // repeating blues riff (2-3 note motif)
                {
                    int motifLen = 2 + lrng.Next(2);
                    int[] motif  = new int[motifLen];
                    int motifLp  = lp;
                    for (int k = 0; k < motifLen; k++)
                    {
                        motifLp  = Math.Clamp(motifLp + (lrng.NextDouble() < 0.6 ? 1 : -1) * (1 + lrng.Next(2)), 0, LPentaLen - 1);
                        motif[k] = motifLp;
                    }
                    int maxReps = Math.Min(2 + lrng.Next(3), rem / Math.Max(motifLen, 1) + 1);
                    for (int r = 0; r < maxReps && ln < LeadNoteCount; r++)
                        for (int k = 0; k < motifLen && ln < LeadNoteCount; k++, ln++)
                        {
                            lp = motif[k];  lcur = lpenta[lp];
                            lFreq[ln] = lPool[lcur];  lVib[ln] = false;  lVol[ln] = 1f;
                            lBnd[ln]  = r == 0 && k == 0 && lrng.NextDouble() < 0.35
                                ? lPool[Math.Clamp(lcur - 1, 0, LPoolLen - 1)] : 0f;
                        }
                }
                else if (lph < 0.58) // ghost-note funk
                {
                    int len = Math.Min(4 + lrng.Next(6), rem);
                    for (int k = 0; k < len; k++, ln++)
                    {
                        bool ghost = k % 3 == 1;
                        if (!ghost) { lp = Math.Clamp(lp + (lrng.NextDouble() < 0.6 ? 1 : -1) * (1 + lrng.Next(2)), 0, LPentaLen - 1);  lcur = lpenta[lp]; }
                        lFreq[ln] = lPool[lcur];
                        lVib[ln]  = !ghost && lrng.NextDouble() < 0.18;
                        lVol[ln]  = ghost ? 0.22f : 1f;
                        lBnd[ln]  = 0f;
                    }
                }
                else if (lph < 0.70) // octave leap with bend
                {
                    int len = Math.Min(2 + lrng.Next(4), rem);
                    int dir = lrng.NextDouble() < 0.5 ? 5 : -5;
                    for (int k = 0; k < len; k++, ln++)
                    {
                        lp = Math.Clamp(k % 2 == 0 ? lp + dir : lp - dir + lrng.Next(3) - 1, 0, LPentaLen - 1);
                        lcur = lpenta[lp];
                        lFreq[ln] = lPool[lcur];
                        lVib[ln]  = lrng.NextDouble() < 0.28;
                        lVol[ln]  = 1f;
                        lBnd[ln]  = lrng.NextDouble() < 0.50
                            ? lPool[Math.Clamp(lcur + (dir > 0 ? -2 : 2), 0, LPoolLen - 1)] : 0f;
                    }
                }
                else if (lph < 0.82) // machine-gun stutter
                {
                    int len = Math.Min(2 + lrng.Next(4), rem);
                    for (int k = 0; k < len; k++, ln++)
                    {
                        lFreq[ln] = lPool[lcur];
                        lVib[ln]  = k == len - 1 && lrng.NextDouble() < 0.28;
                        lVol[ln]  = k % 2 == 0 ? 1f : 0.30f;
                        lBnd[ln]  = 0f;
                    }
                }
                else // sustained note with vibrato + bend
                {
                    int len = Math.Min(2 + lrng.Next(4), rem);
                    lp = Math.Clamp(lp + (lrng.NextDouble() < 0.5 ? 1 : -1), 0, LPentaLen - 1);
                    lcur = lpenta[lp];
                    for (int k = 0; k < len; k++, ln++)
                    {
                        lFreq[ln] = lPool[lcur];
                        lVib[ln]  = true;
                        lVol[ln]  = 1f;
                        lBnd[ln]  = k == 0 && lrng.NextDouble() < 0.50
                            ? lPool[Math.Clamp(lcur + (lrng.NextDouble() < 0.5 ? -2 : 2), 0, LPoolLen - 1)] : 0f;
                    }
                }
            }
        }

        _leadNoteFreqs = lFreq;
        _leadVib       = lVib;
        _leadVol       = lVol;
        _leadBend      = lBnd;

        // ── Fast lick overlay: 0-3 blazing 64th-note bursts in the solo window ─────
        const int FastDur    = SoloSamplesPerNote / 2; // 64th note ≈ 918 samples
        int soloBodyEnd      = diveStart * SoloSamplesPerNote; // sample budget before dive bomb
        int numBursts        = rng.Next(4); // 0, 1, 2, or 3 bursts per loop
        var fFreqList  = new System.Collections.Generic.List<float>();
        var fVolList   = new System.Collections.Generic.List<float>();
        var fBendList  = new System.Collections.Generic.List<float>();
        var fOffList   = new System.Collections.Generic.List<int>();

        for (int b = 0; b < numBursts; b++)
        {
            int burstLen = 4 + rng.Next(9); // 4-12 notes per burst
            int span     = burstLen * FastDur;
            int maxStart = soloBodyEnd - span;
            if (maxStart <= 0) break;

            // Snap to beat boundary (4 × SoloSamplesPerNote ≈ 1 beat)
            int snap       = SoloSamplesPerNote * 4;
            int burstStart = rng.Next(Math.Max(1, maxStart / snap + 1)) * snap;
            burstStart     = Math.Min(burstStart, maxStart);

            // Reject if overlapping an existing burst
            bool overlaps = false;
            foreach (int off in fOffList)
                if (Math.Abs(off - burstStart) < span + SoloSamplesPerNote * 2) { overlaps = true; break; }
            if (overlaps) continue;

            // Generate pentatonic blues run (mostly ascending for energy)
            int bpp = 4 + rng.Next(5); // G#4-C5 start
            int bdir = rng.NextDouble() < 0.65 ? 1 : -1;
            for (int k = 0; k < burstLen; k++)
            {
                bpp = Math.Clamp(bpp + bdir, 0, PentaLen - 1);
                if (k > 0 && rng.NextDouble() < 0.18) bdir = -bdir; // occasional reversal
                int bcur = penta[bpp];
                fFreqList.Add(pool[bcur]);
                fVolList.Add(1f);
                fBendList.Add(k == 0 && rng.NextDouble() < 0.50
                    ? pool[Math.Clamp(bcur + (bdir > 0 ? -1 : 1), 0, PoolLen - 1)] : 0f);
                fOffList.Add(burstStart + k * FastDur);
            }
        }

        _fastFreqs   = fFreqList.ToArray();
        _fastVols    = fVolList.ToArray();
        _fastBends   = fBendList.ToArray();
        _fastOffsets = fOffList.ToArray();
    }

    /// <summary>
    /// Starts the ambient background loop once. Safe to call repeatedly.
    /// </summary>
    public void EnsureBackgroundMusicStarted()
    {
        if (_headless || _musicPlayer != null)
            return;
        StartMusic();
        UpdateMusicPitch();
    }

    /// <summary>
    /// Menu ambient loop should be audible only outside maps.
    /// Maps use MusicDirector layers instead.
    /// </summary>
    public void SetMenuAmbientEnabled(bool enabled)
    {
        if (_headless)
            return;

        if (enabled)
            EnsureBackgroundMusicStarted();
        if (_musicPlayer == null)
            return;

        if (enabled)
            FadePadIn(2.0f);   // smooth return to menu over 2 s
        else
            FadePad(2.5f);     // smooth fade into game over 2.5 s (matches map animation)
    }

    public override void _Process(double delta)
    {
        if (_headless) return;
        // Stop SFX players whose sound has finished
        ulong nowMs = Time.GetTicksMsec();
        for (int i = 0; i < PoolSize; i++)
        {
            if (_poolStopAtMs[i] > 0)
            {
                if (nowMs >= _poolStopAtMs[i])
                {
                    _pool[i].Stop();
                    _poolStopAtMs[i] = 0;
                }
            }
            else if (_poolTimers[i] > 0f)
            {
                // Keep legacy timer bookkeeping as a soft fallback for active-voice estimation.
                _poolTimers[i] = Mathf.Max(0f, _poolTimers[i] - (float)delta);
            }
        }

        // Stream music: push however many frames the generator buffer can accept
        if (_musicPlayback != null && _musicFrames.Length > 0)
        {
            int avail = _musicPlayback.GetFramesAvailable();
            for (int i = 0; i < avail; i++)
            {
                // Detect loop wrap → evolve seed and regenerate the solo for the next loop
                if (_isAltMusic && _musicPos + 1 >= _musicFrames.Length)
                {
                    _soloLoopSeed += 7919;
                    GenerateWildSolo(_soloLoopSeed);
                    // After intro finishes, swap to full-intensity buffer -- no more buildup replay
                    if (!_altMusicIntroPlayed && _musicFramesFull != null)
                    {
                        _musicFrames = _musicFramesFull;
                        _altMusicIntroPlayed = true;
                    }
                }

                // Zool: swap intro → main loop after first pass
                if (_isZoolMusic && _musicPos + 1 >= _musicFrames.Length
                    && !_zoolIntroPlayed && _musicFramesFull != null)
                {
                    _musicFrames    = _musicFramesFull;
                    _zoolIntroPlayed = true;
                }

                var frame = _musicFrames[_musicPos];

                // Shared 6-harmonic saw -- used by both solo and fast-lick layers
                static float Saw(float tt, float f) =>
                    MathF.Sin(tt * MathF.Tau * f)
                  + MathF.Sin(tt * MathF.Tau * f * 2f) * 0.500f
                  + MathF.Sin(tt * MathF.Tau * f * 3f) * 0.333f
                  + MathF.Sin(tt * MathF.Tau * f * 4f) * 0.250f
                  + MathF.Sin(tt * MathF.Tau * f * 5f) * 0.200f
                  + MathF.Sin(tt * MathF.Tau * f * 6f) * 0.167f;

                // Real-time lead layer: bars 0-8 (samples 0 to SoloStartSample-1)
                if (_isAltMusic && _leadNoteFreqs.Length == LeadNoteCount
                    && _musicPos < SoloStartSample)
                {
                    // BPM constants (must match BuildMusicFramesAlt)
                    const float BPM_     = 90f;
                    const float Beat_    = 60f / BPM_;
                    const float Eighth_  = Beat_ / 2f;
                    const float Sixteenth_ = Beat_ / 4f;
                    const float Bar_     = Beat_ * 4f;

                    float t = _musicPos / (float)Rate;

                    // Lead volume: intro ramps in from bar 4; after intro always full
                    float leadVol = _altMusicIntroPlayed ? 1f :
                        (t < Bar_ * 4f ? 0f : Mathf.Min((t - Bar_ * 4f) / (Bar_ * 0.5f), 1f));

                    if (leadVol > 0f)
                    {
                        // leadInterval: intro ramps quarter→8th→16th; after intro always 16th
                        float leadInterval = _altMusicIntroPlayed ? Sixteenth_ :
                            (t < Bar_ * 4f ? Beat_ : t < Bar_ * 6f ? Eighth_ : Sixteenth_);

                        int noteIdx = (int)(t / leadInterval) % LeadNoteCount;
                        float vol   = _leadVol.Length == LeadNoteCount ? _leadVol[noteIdx] : 1f;

                        if (vol > 0f)
                        {
                            float notePos = (t % leadInterval) / leadInterval;
                            float sf      = _leadNoteFreqs[noteIdx];

                            // Pitch bend: slide FROM bend-start freq → target during first 40%
                            if (_leadBend.Length == LeadNoteCount && _leadBend[noteIdx] != 0f)
                            {
                                float bendT = Mathf.Min(notePos / 0.40f, 1f);
                                sf = _leadBend[noteIdx] + (_leadNoteFreqs[noteIdx] - _leadBend[noteIdx]) * bendT;
                            }

                            // Vibrato
                            if (_leadVib.Length == LeadNoteCount && _leadVib[noteIdx])
                            {
                                float depth = sf > 700f ? 0.060f : 0.040f;
                                float vrate = sf > 700f ? 8.2f   : 6.8f;
                                sf *= 1f + depth * MathF.Sin(t * MathF.Tau * vrate);
                            }

                            static float SawL(float tt, float f) =>
                                MathF.Sin(tt * MathF.Tau * f)
                              + MathF.Sin(tt * MathF.Tau * f * 2f) * 0.500f
                              + MathF.Sin(tt * MathF.Tau * f * 3f) * 0.333f
                              + MathF.Sin(tt * MathF.Tau * f * 4f) * 0.250f
                              + MathF.Sin(tt * MathF.Tau * f * 5f) * 0.200f
                              + MathF.Sin(tt * MathF.Tau * f * 6f) * 0.167f;

                            float sf1 = sf * 1.00462f;
                            float sf2 = sf * 0.99541f;

                            float attack = vol < 0.5f ? 0.012f : 0.020f;
                            float env    = Mathf.Min(notePos / attack, 1f)
                                         * Mathf.Min((1f - notePos) / 0.06f, 1f);
                            float lead   = (SawL(t, sf1) + SawL(t, sf2)) * 0.5f * 0.040f * env * vol * leadVol;

                            float mixed = Mathf.Clamp(frame.X + lead, -1f, 1f);
                            frame = new Vector2(mixed, mixed);
                        }
                    }
                }

                // Real-time solo layer: replaces cascade lead in final 3 bars (bars 9-11)
                if (_isAltMusic && _soloNoteFreqs.Length == SoloNoteCount
                    && _musicPos >= SoloStartSample)
                {
                    int soloSample = _musicPos - SoloStartSample;
                    int noteIdx    = soloSample / SoloSamplesPerNote;
                    if (noteIdx < SoloNoteCount)
                    {
                        float vol     = _soloNoteVol.Length == SoloNoteCount ? _soloNoteVol[noteIdx] : 1f;
                        float notePos = (soloSample % SoloSamplesPerNote) / (float)SoloSamplesPerNote;
                        float t       = _musicPos / (float)Rate;
                        float sf      = _soloNoteFreqs[noteIdx];

                        // Pitch bend: slide FROM bend-start freq → target during first 40% of note
                        if (_soloNoteBend.Length == SoloNoteCount && _soloNoteBend[noteIdx] != 0f)
                        {
                            float bendT = Mathf.Min(notePos / 0.40f, 1f);
                            sf = _soloNoteBend[noteIdx] + (_soloNoteFreqs[noteIdx] - _soloNoteBend[noteIdx]) * bendT;
                        }

                        // Wide synth vibrato -- faster and deeper on high screamers
                        if (_soloNoteVibrato[noteIdx])
                        {
                            float depth = sf > 700f ? 0.075f : 0.050f;
                            float rate  = sf > 700f ? 8.6f    : 7.2f;
                            sf *= 1f + depth * MathF.Sin(t * MathF.Tau * rate);
                        }

                        // Fat unison: two oscillators detuned ±8 cents (×1.00462 / ×0.99541)
                        float sf1 = sf * 1.00462f;
                        float sf2 = sf * 0.99541f;
                        float attack = vol < 0.5f ? 0.015f : 0.025f; // ghosts attack faster (shorter)
                        float env    = Mathf.Min(notePos / attack, 1f)
                                     * Mathf.Min((1f - notePos) / 0.07f, 1f);
                        float solo   = (Saw(t, sf1) + Saw(t, sf2)) * 0.5f * 0.058f * env * vol;

                        float mixed = Mathf.Clamp(frame.X + solo, -1f, 1f);
                        frame = new Vector2(mixed, mixed);
                    }
                }

                // Fast lick overlay: blazing 64th-note bursts on top of main solo
                if (_isAltMusic && _fastOffsets.Length > 0 && _musicPos >= SoloStartSample)
                {
                    int ss = _musicPos - SoloStartSample;
                    const int FastDur_ = SoloSamplesPerNote / 2;
                    for (int k = 0; k < _fastOffsets.Length; k++)
                    {
                        int ns = _fastOffsets[k];
                        if (ss >= ns && ss < ns + FastDur_)
                        {
                            float notePos = (ss - ns) / (float)FastDur_;
                            float t  = _musicPos / (float)Rate;
                            float sf = _fastFreqs[k];
                            if (_fastBends[k] != 0f)
                            {
                                float bendT = Mathf.Min(notePos / 0.35f, 1f);
                                sf = _fastBends[k] + (_fastFreqs[k] - _fastBends[k]) * bendT;
                            }
                            float sf1  = sf * 1.00462f;
                            float sf2  = sf * 0.99541f;
                            float env  = Mathf.Min(notePos / 0.010f, 1f) * Mathf.Min((1f - notePos) / 0.05f, 1f);
                            float fast = (Saw(t, sf1) + Saw(t, sf2)) * 0.5f * 0.052f * env;
                            float mixed = Mathf.Clamp(frame.X + fast, -1f, 1f);
                            frame = new Vector2(mixed, mixed);
                            break;
                        }
                    }
                }

                _musicPlayback.PushFrame(frame);
                _musicPos = (_musicPos + 1) % _musicFrames.Length;
            }
        }

        if (_musicPlayer != null)
        {
            if (_musicDuckHold > 0f)
                _musicDuckHold = Mathf.Max(0f, _musicDuckHold - (float)delta);
            else
                _musicDuckDb = Mathf.Max(0f, _musicDuckDb - 14f * (float)delta);

            // Advance pad fade (positive rate = fade out, negative = fade in)
            if (_padFadeRate != 0f)
            {
                _padFadeDb += _padFadeRate * (float)delta;
                if (_padFadeRate > 0f && _padFadeDb >= 60f) { _padFadeDb = 60f; _padFadeRate = 0f; }
                else if (_padFadeRate < 0f && _padFadeDb <= 0f) { _padFadeDb = 0f; _padFadeRate = 0f; }
            }

            float targetDb = MusicBaseDb + _musicTensionDb - _musicDuckDb - _padFadeDb;
            _musicPlayer.VolumeDb = Mathf.Lerp(_musicPlayer.VolumeDb, targetDb, 12f * (float)delta);
        }
    }

    public void Play(string id, float pitchScale = 1f)
    {
        if (_headless) return;
        if (!_samples.TryGetValue(id, out var samples)) return;
        ulong nowMs = Time.GetTicksMsec();
        if (ShouldSkipMobileSfx(id, nowMs)) return;
        if (!_isMobileAudio && _gameSpeedScale > 1f && ShouldSkipDesktopSfx(id, nowMs)) return;
        float dur = _durations[id];
        float finalPitch = Mathf.Clamp(pitchScale * _speedFxPitch, 0.75f, 1.40f);

        // Prefer a slot that has already finished playing so we never hard-stop active audio.
        // A hard Stop() on a playing AudioStreamGenerator causes an audible click/pop.
        int idx = -1;
        for (int i = 0; i < PoolSize; i++)
        {
            int candidate = (_poolIdx + i) % PoolSize;
            if (_poolStopAtMs[candidate] == 0 || nowMs >= _poolStopAtMs[candidate])
            {
                idx = candidate;
                break;
            }
        }
        if (idx < 0)
            idx = _poolIdx; // all 20 slots active - steal the oldest (unavoidable click)
        _poolIdx = (idx + 1) % PoolSize;

        var player = _pool[idx];
        player.Stop();
        player.Bus = UiSoundIds.Contains(id) ? "UI" : "FX";
        player.VolumeDb = ResolveFxPlaybackDb(id);
        player.PitchScale = finalPitch;
        player.Play();
        var playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        playback.PushBuffer(samples);
        // The buffer is consumed at finalPitch rate, so the slot becomes free in dur/finalPitch seconds.
        float effectiveDurSec = dur / Mathf.Max(0.1f, finalPitch);
        _poolTimers[idx] = effectiveDurSec + 0.05f;
        _poolStopAtMs[idx] = nowMs + (ulong)Mathf.CeilToInt((effectiveDurSec + 0.05f) * 1000f);
    }

    public void SetSpeedFeel(float speedScale)
    {
        if (_headless) return;

        _gameSpeedScale = Mathf.Max(1f, speedScale);
        // Subtle "machine pushed harder" effect at 2x/3x.
        float speedOver = Mathf.Max(0f, speedScale - 1f);
        _speedFxPitch = 1f + speedOver * 0.026f; // 2x: 1.026, 3x: 1.052
        _speedMusicPitch = 1f + speedOver * 0.012f;
        UpdateMusicPitch();
    }

    /// <summary>
    /// Ramps up music intensity for late-game tension. Call at each wave start.
    /// level 0 = baseline; level 1 = full tension (waves 15-20).
    /// </summary>
    public void SetMusicTension(float level)
    {
        if (_headless) return;
        _musicTensionDb    = Mathf.Clamp(level, 0f, 1f) * 3.5f;  // up to +3.5 dB
        _musicTensionPitch = 1f + Mathf.Clamp(level, 0f, 1f) * 0.025f; // up to +2.5%
        UpdateMusicPitch();
    }

    private void UpdateMusicPitch()
    {
        if (_musicPlayer != null)
            _musicPlayer.PitchScale = _speedMusicPitch * _musicTensionPitch;
    }

    public void DuckMusic(float amountDb = 1.8f, float holdSeconds = 0.10f)
    {
        if (_headless || _musicPlayer == null) return;
        _musicDuckDb = Mathf.Max(_musicDuckDb, Mathf.Clamp(amountDb, 0f, 5f));
        _musicDuckHold = Mathf.Max(_musicDuckHold, Mathf.Clamp(holdSeconds, 0.02f, 0.40f));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Reg(string id, (Vector2[] s, float d) data)
    {
        _samples[id]   = data.s;
        _durations[id] = data.d;
    }

    // ── Synthesis ────────────────────────────────────────────────────────

    /// <param name="shape">'s'=sine  'q'=square  't'=triangle  'n'=noise</param>
    /// <param name="env">  'f'=exp decay  'l'=linear decay</param>
    private static (Vector2[] s, float d) Tone(float freq, float dur,
        float vol = 0.5f, char shape = 's', char env = 'l')
    {
        int     n   = (int)(Rate * dur);
        var     arr = new Vector2[n];
        var     rng = new Random(42);

        for (int i = 0; i < n; i++)
        {
            float t     = i / (float)Rate;
            float phase = t * freq;
            float wave  = shape switch
            {
                'q' => (phase % 1f) < 0.5f ? 1f : -1f,
                't' => 1f - 4f * MathF.Abs((phase % 1f) - 0.5f),
                'n' => (float)(rng.NextDouble() * 2.0 - 1.0),
                _   => MathF.Sin(t * MathF.Tau * freq),
            };
            float amp = env == 'f'
                ? vol * MathF.Exp(-5f * t / dur)
                : vol * MathF.Max(0f, 1f - t / dur);
            float s = wave * amp;
            arr[i] = new Vector2(s, s);
        }
        return (arr, dur);
    }

    /// Frequency sweep f0 → f1 with linear decay.
    private static (Vector2[] s, float d) Sweep(float f0, float f1, float dur, float vol = 0.5f)
    {
        int    n     = (int)(Rate * dur);
        var    arr   = new Vector2[n];
        double phase = 0.0;

        for (int i = 0; i < n; i++)
        {
            float t    = i / (float)Rate;
            float freq = f0 + (f1 - f0) * (t / dur);
            phase += freq / Rate;
            float wave = MathF.Sin((float)(phase * Math.PI * 2.0));
            float amp  = vol * MathF.Max(0f, 1f - t / dur);
            float s    = wave * amp;
            arr[i] = new Vector2(s, s);
        }
        return (arr, dur);
    }

    /// Multiple tones played back to back with a silent gap between notes.
    private static (Vector2[] s, float d) Seq(float[] freqs, int gapMs, float noteLen, float vol = 0.5f)
    {
        int noteN  = (int)(Rate * noteLen);
        int gapN   = Rate * gapMs / 1000;
        int stride = noteN + gapN;
        int total  = stride * freqs.Length;
        var arr    = new Vector2[total];

        for (int k = 0; k < freqs.Length; k++)
        {
            int   offset = k * stride;
            float freq   = freqs[k];
            for (int i = 0; i < noteN; i++)
            {
                float t    = i / (float)Rate;
                float wave = MathF.Sin(t * MathF.Tau * freq);
                float amp  = vol * MathF.Max(0f, 1f - t / noteLen);
                float s    = wave * amp;
                arr[offset + i] = new Vector2(s, s);
            }
            // gap entries stay at Vector2.Zero (silence)
        }
        return (arr, (float)total / Rate);
    }

    /// <summary>Layers multiple synthesized clips for richer one-shot effects.</summary>
    private static (Vector2[] s, float d) Layer(params (Vector2[] s, float d)[] clips)
    {
        int max = 0;
        for (int i = 0; i < clips.Length; i++)
            max = Math.Max(max, clips[i].s.Length);

        if (max <= 0)
            return (Array.Empty<Vector2>(), 0f);

        var mixed = new Vector2[max];
        for (int c = 0; c < clips.Length; c++)
        {
            var src = clips[c].s;
            for (int i = 0; i < src.Length; i++)
                mixed[i] += src[i];
        }

        // Avoid hard clipping distortion on layered one-shots by normalizing peaks.
        float peak = 0f;
        for (int i = 0; i < mixed.Length; i++)
        {
            float samplePeak = Mathf.Max(Mathf.Abs(mixed[i].X), Mathf.Abs(mixed[i].Y));
            if (samplePeak > peak)
                peak = samplePeak;
        }
        float targetPeak = 0.86f;
        float normalizeScale = peak > targetPeak ? (targetPeak / peak) : 1f;

        for (int i = 0; i < mixed.Length; i++)
        {
            float l = Mathf.Clamp(mixed[i].X * normalizeScale, -1f, 1f);
            float r = Mathf.Clamp(mixed[i].Y * normalizeScale, -1f, 1f);
            mixed[i] = new Vector2(l, r);
        }
        return (mixed, max / (float)Rate);
    }

    /// <summary>
    /// Synthesizes an electrical crackling sound - rapid irregular noise bursts in the
    /// 600–4000 Hz range, like a lightning arc or Jacob's ladder.
    /// </summary>
    private static (Vector2[] s, float d) Thunder(
        float dur = 0.9f,
        float vol = 0.88f,
        float crackVol = 0.70f,    // unused - kept for call-site compat
        float[]? rollFreqs = null,  // unused - kept for call-site compat
        float[]? bodyFreqs = null)  // unused - kept for call-site compat
    {
        int n   = (int)(Rate * dur);
        var rng = new Random(17);
        var arr = new Vector2[n];

        // ── "KA-PTSHOWRRRR" - one continuous event, three parallel decays ──
        // No repeating bursts (that's what causes the rattlesnake character).

        // Broadband noise for impact + sizzle tail
        var white = new float[n];
        for (int i = 0; i < n; i++)
            white[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        // Bandpass 400–2000 Hz for crackle body
        const float aHi = 0.566f;   // LP ~2000 Hz
        const float aLo = 0.892f;   // LP ~400 Hz
        float lpHi = 0f, lpLo = 0f;
        var midNoise = new float[n];
        var rng2 = new Random(99);
        for (int i = 0; i < n; i++)
        {
            float w2 = (float)(rng2.NextDouble() * 2.0 - 1.0);
            lpHi = aHi * lpHi + (1f - aHi) * w2;
            lpLo = aLo * lpLo + (1f - aLo) * w2;
            midNoise[i] = lpHi - lpLo;
        }

        // HP above ~300 Hz for short tail
        const float aLp = 0.918f;   // LP ~300 Hz
        float lpW = 0f;
        var hpNoise = new float[n];
        for (int i = 0; i < n; i++)
        {
            lpW = aLp * lpW + (1f - aLp) * white[i];
            hpNoise[i] = white[i] - lpW;
        }

        // Spark sweep: 2500→400 Hz over 12ms - electrical "zap" onset, no physical slap
        double sparkPhase = 0.0;
        var spark = new float[n];
        int sparkN = (int)(Rate * 0.012f);
        for (int i = 0; i < sparkN; i++)
        {
            float tc = i / (float)Rate;
            float freq = 2500f + (400f - 2500f) * (tc / 0.012f);
            sparkPhase += freq / Rate;
            spark[i] = MathF.Sin((float)(sparkPhase * MathF.Tau)) * MathF.Exp(-tc / 0.005f);
        }

        // Periodic burst envelope - hard-clipped mid noise per burst
        // 28ms interval = ~36 Hz, slow enough to feel like distinct cracks not rattlesnake
        int burstN = (int)(Rate * 0.028f);
        var rng3 = new Random(77);
        float burstAmp = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i % burstN == 0)
                burstAmp = (float)(rng3.NextDouble() * 0.6 + 0.4);  // 0.4–1.0 random amplitude
        }
        // Pre-compute burst envelope per sample
        var burstEnvArr = new float[n];
        burstAmp = 0f;
        var rng4 = new Random(77);
        for (int i = 0; i < n; i++)
        {
            if (i % burstN == 0)
                burstAmp = (float)(rng4.NextDouble() * 0.6 + 0.4);
            int phase = i % burstN;
            burstEnvArr[i] = burstAmp * MathF.Exp(-phase / (Rate * 0.014f));  // 14ms per-burst decay
        }

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float overallDecay = MathF.Exp(-3.5f * t / dur);

            // Spark onset
            float zapLayer = vol * 1.1f * spark[i];

            // Continuous hard-clipped crackle body (40ms decay)
            float crackleRaw = Mathf.Clamp(midNoise[i] * 5f, -1f, 1f);
            float crackle = vol * 0.70f * MathF.Exp(-t / 0.040f) * crackleRaw;

            // Periodic burst layer: hard-clipped mid noise pops over the tail
            float burstCrackle = vol * 0.55f * overallDecay * burstEnvArr[i]
                                * Mathf.Clamp(midNoise[i] * 5f, -1f, 1f);

            // Short HP tail
            float ssh = vol * 0.18f * MathF.Exp(-t / 0.055f) * hpNoise[i];

            float s = Mathf.Clamp(zapLayer + crackle + burstCrackle + ssh, -1f, 1f);
            arr[i] = new Vector2(s, s);
        }
        return (arr, dur);
    }

    /// <summary>
    /// Explosion with expanding shockwave ring.
    /// Layer 1: Low-mid boom (80–300 Hz), sharp attack, 200ms decay.
    /// Layer 2: Time-varying bandpass that sweeps 150→1500 Hz over 600ms - the "expanding wave."
    /// Layer 3: High sparkle tail above 800 Hz that fades after the wave passes.
    /// </summary>
    private static (Vector2[] s, float d) GlobalSurge(
        float dur = 1.8f, float vol = 0.90f,
        float boomDecay = 0.200f, float waveSweepDur = 0.60f, float sparkVol = 0.35f)
    {
        int n   = (int)(Rate * dur);
        var rng  = new Random(55);
        var rng2 = new Random(83);
        var arr  = new Vector2[n];

        // ── Boom layer: bandpass 80–300 Hz ───────────────────────────────
        const float aBoomHi = 0.919f;   // LP ~280 Hz
        const float aBoomLo = 0.978f;   // LP ~75 Hz
        float bHi = 0f, bLo = 0f;
        var boom = new float[n];
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rng.NextDouble() * 2.0 - 1.0);
            bHi = aBoomHi * bHi + (1f - aBoomHi) * w;
            bLo = aBoomLo * bLo + (1f - aBoomLo) * w;
            boom[i] = bHi - bLo;
        }

        // ── Expanding wave: time-varying LP, cutoff 150 Hz → 1500 Hz ─────
        // Cutoff rises as the shockwave ring expands outward.
        float sweepDur = waveSweepDur;
        float lpWave = 0f;
        var wave = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t  = i / (float)Rate;
            float fc = 150f + 1350f * MathF.Min(1f, t / sweepDur);
            float a  = MathF.Exp(-MathF.Tau * fc / Rate);
            float w  = (float)(rng2.NextDouble() * 2.0 - 1.0);
            lpWave = a * lpWave + (1f - a) * w;
            wave[i] = lpWave;
        }

        // ── Sparkle tail: HP above ~800 Hz ───────────────────────────────
        const float aSpark = 0.772f;   // LP ~800 Hz  →  HP = white − LP
        float lpSp = 0f;
        var spark = new float[n];
        var rng3  = new Random(29);
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rng3.NextDouble() * 2.0 - 1.0);
            lpSp = aSpark * lpSp + (1f - aSpark) * w;
            spark[i] = w - lpSp;
        }

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // Boom: instant onset, tunable decay
            float boomLayer = vol * 1.0f * MathF.Exp(-t / boomDecay) * boom[i];

            // Wave: delayed onset swell (peaks ~80ms), then fades over full dur
            float waveSwell = (1f - MathF.Exp(-t / 0.035f)) * MathF.Exp(-2.5f * t / dur);
            float waveLayer = vol * 0.80f * waveSwell * wave[i];

            // Sparkle tail: kicks in as wave passes, fades by 600ms
            float sparkEnv = MathF.Exp(-t / 0.180f) * (1f - MathF.Exp(-t / 0.060f));
            float sparkLayer = vol * sparkVol * sparkEnv * spark[i];

            float s = Mathf.Clamp(boomLayer + waveLayer + sparkLayer, -1f, 1f);
            arr[i] = new Vector2(s, s);
        }
        return (arr, dur);
    }

    private bool ShouldSkipMobileSfx(string id, ulong nowMs)
    {
        if (!_isMobileAudio)
            return false;
        if (MobileDisabledExplosionSfx.Contains(id))
            return true;
        if (!MobileSfxCooldownMs.TryGetValue(id, out int cooldownMs))
        {
            return false;
        }

        int resolvedCooldownMs = ResolveMobileCooldownMs(cooldownMs);
        if (_mobileSfxLastPlayMs.TryGetValue(id, out ulong lastMs))
        {
            if (nowMs - lastMs < (ulong)resolvedCooldownMs)
                return true;
        }

        if (CountActiveVoices(nowMs) >= MobileFxBusyVoiceDropThreshold && id != "wave20_swell")
            return true;

        _mobileSfxLastPlayMs[id] = nowMs;
        return false;
    }

    private bool ShouldSkipDesktopSfx(string id, ulong nowMs)
    {
        if (!DesktopSfxCooldownMs.TryGetValue(id, out int baseCooldownMs))
            return false;

        // Tighten cooldown proportionally at higher speeds so stacking is bounded
        // without silencing sounds entirely (floor at 1ms so something always plays).
        int cooldownMs = Mathf.Max(1, (int)(baseCooldownMs / Mathf.Max(1f, _gameSpeedScale)));
        if (_desktopSfxLastPlayMs.TryGetValue(id, out ulong lastMs) && nowMs - lastMs < (ulong)cooldownMs)
            return true;

        _desktopSfxLastPlayMs[id] = nowMs;
        return false;
    }

    private float ResolveFxPlaybackDb(string id)
    {
        if (!_isMobileAudio)
            return 0f;

        float db = MobileFxBaseHeadroomDb;
        if (MobileSfxCooldownMs.ContainsKey(id))
            db += MobileFxSurgeExtraHeadroomDb;

        int activeVoices = 0;
        ulong nowMs = Time.GetTicksMsec();
        activeVoices = CountActiveVoices(nowMs);

        if (activeVoices > MobileFxBurstStartVoices)
        {
            float extraVoices = activeVoices - MobileFxBurstStartVoices;
            float burstPenalty = Mathf.Min(MobileFxBurstPenaltyCapDb, extraVoices * MobileFxBurstPenaltyPerVoiceDb);
            db -= burstPenalty;
        }

        return Mathf.Clamp(db, -18f, 0f);
    }

    private int ResolveMobileCooldownMs(int baseCooldownMs)
    {
        float fps = (float)Engine.GetFramesPerSecond();
        if (fps <= 0f)
            return baseCooldownMs;
        if (fps < 20f)
            return (int)(baseCooldownMs * 2.0f);
        if (fps < 30f)
            return (int)(baseCooldownMs * 1.55f);
        if (fps < 45f)
            return (int)(baseCooldownMs * 1.25f);
        return baseCooldownMs;
    }

    private int CountActiveVoices(ulong nowMs)
    {
        int activeVoices = 0;
        for (int i = 0; i < _poolStopAtMs.Length; i++)
        {
            if (_poolStopAtMs[i] > nowMs)
                activeVoices++;
        }
        return activeVoices;
    }

    private void EnsureFxLimiter() => EnsureBusLimiter("FX");

    private void EnsureBusLimiter(string busName)
    {
        int busIdx = AudioServer.GetBusIndex(busName);
        if (busIdx < 0) return;

        int effectCount = AudioServer.GetBusEffectCount(busIdx);
        for (int i = 0; i < effectCount; i++)
        {
            if (AudioServer.GetBusEffect(busIdx, i) is AudioEffectHardLimiter)
                return;
        }

        var limiter = new AudioEffectHardLimiter();
        if (_isMobileAudio)
            limiter.PreGainDb = MobileFxBaseHeadroomDb;
        AudioServer.AddBusEffect(busIdx, limiter, 0);
    }

    // ── Music note playback ───────────────────────────────────────────────

    /// <summary>
    /// Play a synthesized music note on the Music bus.
    /// midiNote: standard MIDI number (e.g. 45 = A2, 69 = A4).
    /// Bass notes (MIDI 28–57) and lead notes (MIDI 58–81) are registered
    /// at startup via the MusicNote synthesis loop in _Ready().
    /// </summary>
    /// <param name="volDb">
    /// Absolute VolumeDb for this note. Default -8 dB (bass level).
    /// Melody layer passes -11 dB to sit below the bass.
    /// </param>
    public void PlayNote(int midiNote, float volDb = -8f)
    {
        if (_headless) return;
        string id = $"mnote_{midiNote}";
        if (!_samples.TryGetValue(id, out var samples)) return;

        // Prefer a slot that has finished; fall back to oldest if all active.
        ulong nowMs = Time.GetTicksMsec();
        int idx = -1;
        for (int i = 0; i < NotePoolSize; i++)
        {
            int c = (_notePoolIdx + i) % NotePoolSize;
            if (_notePoolStopMs[c] == 0 || nowMs >= _notePoolStopMs[c])
            {
                idx = c;
                break;
            }
        }
        if (idx < 0) idx = _notePoolIdx;
        _notePoolIdx = (idx + 1) % NotePoolSize;

        var player = _notePool[idx];
        player.Stop();
        player.VolumeDb = volDb;
        player.Play();
        var pb = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        pb.PushBuffer(samples);
        _notePoolStopMs[idx] = nowMs + (ulong)(_durations[id] * 1000f + 50);
    }

    /// <summary>
    /// Play a percussion sound from the dedicated perc pool on the Music bus.
    /// id: "perc_kick", "perc_snare", "perc_hat_c", or "perc_hat_o".
    /// volDb: absolute VolumeDb; default -10 dB.
    /// </summary>
    public void PlayPerc(string id, float volDb = -10f)
    {
        if (_headless) return;
        if (!_samples.TryGetValue(id, out var samples)) return;

        ulong nowMs = Time.GetTicksMsec();
        int idx = -1;
        for (int i = 0; i < PercPoolSize; i++)
        {
            int c = (_percPoolIdx + i) % PercPoolSize;
            if (_percPoolStopMs[c] == 0 || nowMs >= _percPoolStopMs[c])
            {
                idx = c;
                break;
            }
        }
        if (idx < 0) idx = _percPoolIdx;
        _percPoolIdx = (idx + 1) % PercPoolSize;

        var player = _percPool[idx];
        player.Stop();
        player.VolumeDb = volDb;
        player.Play();
        var pb = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        pb.PushBuffer(samples);
        _percPoolStopMs[idx] = nowMs + (ulong)(_durations[id] * 1000f + 50);
    }

    /// <summary>
    /// Gradually fade out the ambient pad loop over the given duration.
    /// </summary>
    public void FadePad(float durationSec)
    {
        if (_headless || _musicPlayer == null) return;
        _padFadeRate = 60f / MathF.Max(0.1f, durationSec);  // positive → fade out
    }

    /// <summary>
    /// Gradually fade the ambient pad back in over the given duration.
    /// </summary>
    public void FadePadIn(float durationSec)
    {
        if (_headless || _musicPlayer == null) return;
        _padFadeRate = -(60f / MathF.Max(0.1f, durationSec));  // negative → fade in
    }

    // ── Music note synthesis ──────────────────────────────────────────────

    /// <summary>
    /// Synthesizes a single musical note with a short attack and natural exponential decay.
    /// Bass: fundamental + octave harmonic + subtle detuned pair for warmth.
    /// Lead: chorused fundamental (two slightly detuned oscillators).
    /// </summary>
    private static (Vector2[] s, float d) MusicNote(float freq, bool isBass)
    {
        float dur       = isBass ? 2.0f  : 1.6f;
        float vol       = isBass ? 0.38f : 0.30f;
        float tau       = isBass ? 1.2f  : 0.7f;   // exponential decay time constant
        float attackSec = isBass ? 0.008f : 0.012f; // linear attack ramp duration
        float detune    = isBass ? 1.5f  : 3.0f;   // Hz offset for chorus / warmth

        int n   = (int)(Rate * dur);
        var arr = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // Envelope: linear attack into exponential decay
            float envAttack = MathF.Min(1f, t / attackSec);
            float envDecay  = MathF.Exp(-t / tau);
            float env       = envAttack * envDecay * vol;

            float s;
            if (isBass)
            {
                // Doom-leaning bass tone: strong sub/fundamental, odd harmonics,
                // then soft saturation for a gritty, amp-like edge.
                float raw =
                    MathF.Sin(t * MathF.Tau * freq * 0.5f) * 0.22f +
                    MathF.Sin(t * MathF.Tau * freq) * 0.46f +
                    MathF.Sin(t * MathF.Tau * freq * 2f) * 0.15f +
                    MathF.Sin(t * MathF.Tau * freq * 3f) * 0.09f +
                    MathF.Sin(t * MathF.Tau * (freq + detune)) * 0.07f +
                    MathF.Sin(t * MathF.Tau * (freq - detune)) * 0.07f;
                s = MathF.Tanh(raw * 1.90f) * 0.90f;
            }
            else
            {
                // Chorused fundamental - two oscillators slightly detuned
                s = MathF.Sin(t * MathF.Tau * freq) * 0.60f
                  + MathF.Sin(t * MathF.Tau * (freq + detune)) * 0.22f
                  + MathF.Sin(t * MathF.Tau * (freq - detune)) * 0.22f;
            }

            float sample = Mathf.Clamp(s * env, -1f, 1f);
            arr[i] = new Vector2(sample, sample);
        }
        return (arr, dur);
    }
}
