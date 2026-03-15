using System;
using System.Collections.Generic;
using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Autoload singleton. Generates all SFX procedurally at startup via PCM synthesis —
/// no audio files required. Uses AudioStreamGenerator so samples are pushed into a
/// ring buffer and played once per pool slot.
/// Use  SoundManager.Instance?.Play("id")  from anywhere.
/// </summary>
public partial class SoundManager : Node
{
    public static SoundManager? Instance { get; private set; }

    private const int   Rate     = 22050;
    private const int   PoolSize = 20;
    private const float MaxDur   = 11.0f;  // buffer length — must exceed longest sound (surge_global = 10s)

    private readonly Dictionary<string, Vector2[]> _samples  = new();
    private readonly Dictionary<string, float>     _durations = new();

    private AudioStreamPlayer[] _pool       = Array.Empty<AudioStreamPlayer>();
    private float[]             _poolTimers = Array.Empty<float>();
    private int                 _poolIdx;

    // Music streaming — precomputed loop pushed frame-by-frame via AudioStreamGenerator
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

    private static readonly HashSet<string> UiSoundIds = new(StringComparer.Ordinal)
    {
        "draft_pick", "ui_card_pick", "ui_preview_ghost", "ui_lock_in", "ui_lock_in_hard",
        "ui_thunk", "ui_speed_shift", "card_shing", "ui_select", "tower_place", "ui_hover",
        "low_heartbeat", "wave_halfway_lift", "wave20_swell",
    };

    private bool _headless;
    private bool _isMobileAudio;
    private readonly Dictionary<string, ulong> _mobileSfxLastPlayMs = new();
    private ulong[] _poolStopAtMs = Array.Empty<ulong>();
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
        // Keep tower/combat sounds audible on mobile while preventing runaway spam.
        ["shoot_rapid"] = 45,
        ["shoot_heavy"] = 90,
        ["shoot_marker"] = 60,
        ["hit"] = 35,
        ["mine_pop"] = 55,
        ["mine_chain_pop"] = 85,
        ["die_basic"] = 55,
        ["die_armored"] = 65,
        ["die_swift"] = 45,
        ["leak"] = 120,
        ["wave20_swell"] = 600,
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

        // ── Tower attacks ────────────────────────────────────────────────
        Reg("shoot_rapid",  Tone(680f, 0.05f, vol: 0.26f, shape: 'q', env: 'f'));
        Reg("shoot_heavy",  Tone( 72f, 0.24f, vol: 0.72f, shape: 's', env: 'f'));
        Reg("shoot_marker", Tone(360f, 0.07f, vol: 0.32f, shape: 's', env: 'f'));

        // ── Enemy events ─────────────────────────────────────────────────
        Reg("hit",          Tone(520f, 0.03f, vol: 0.16f, shape: 'n', env: 'f'));
        Reg("die_basic",    Sweep(400f, 170f, 0.14f, vol: 0.55f));
        Reg("die_armored",  Sweep(155f,  50f, 0.24f, vol: 0.70f));
        Reg("die_swift",    Sweep(900f, 400f, 0.08f, vol: 0.45f));
        Reg("leak",         Sweep(230f,  90f, 0.34f, vol: 0.60f));
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
        Reg("surge_global", GlobalSurge(dur: 10.0f, vol: 0.92f, boomDecay: 0.30f, waveSweepDur: 1.80f, sparkVol: 0.20f));

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

        StartMusic();
    }

    // ── Background music ─────────────────────────────────────────────────

    /// <summary>
    /// Precomputes an 8-second ambient loop (Cm bass pulse + pad drone + shimmer)
    /// and streams it continuously via AudioStreamGenerator for seamless looping.
    /// </summary>
    /// <summary>
    /// Precomputes a 32-second synthwave ambient loop.
    /// Key: A minor Dorian (A, C, E + F# colour tone) — brighter and more energetic
    /// than C minor. Wide chorus detuning on mid/upper layers creates the lush
    /// analogue-pad sound. A 4-second gentle pulse on the high register adds groove
    /// without percussion. Each layer has an independent swell period (4/8/16/32 s)
    /// so the mix evolves continuously across the loop.
    /// </summary>
    private void StartMusic()
    {
        const float LoopDur = 32f;
        int n = (int)(Rate * LoopDur);
        _musicFrames = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // ── Root: A1 + A2, narrow detuning — minimal bass foundation ────
            float root = (MathF.Sin(t * MathF.Tau * 55.00f)
                        + MathF.Sin(t * MathF.Tau * 55.07f)
                        + MathF.Sin(t * MathF.Tau * 54.93f)) * 0.026f
                       + (MathF.Sin(t * MathF.Tau * 110.00f)
                        + MathF.Sin(t * MathF.Tau * 110.10f)
                        + MathF.Sin(t * MathF.Tau * 109.90f)) * 0.020f;

            // ── Fifth: E3, 32-s swell ─────────────────────────────────────────
            float fifthSwell = 0.42f + 0.38f * MathF.Sin(t * MathF.Tau / 32f - MathF.PI * 0.4f);
            float fifth = (MathF.Sin(t * MathF.Tau * 164.81f)
                         + MathF.Sin(t * MathF.Tau * 164.95f)) * 0.055f * fifthSwell;

            // ── Mid pad: A3 + C4 + E4, wide chorus detuning, 8-s swell ───────
            // ±0.28 Hz on A3 → ~3.6 s beat = lush analogue chorus pad sound
            float padSwell = 0.44f + 0.40f * MathF.Sin(t * MathF.Tau / 8f + MathF.PI * 0.5f);
            float pad = (MathF.Sin(t * MathF.Tau * 220.00f) + MathF.Sin(t * MathF.Tau * 220.28f) + MathF.Sin(t * MathF.Tau * 219.72f)) * 0.065f  // A3
                      + (MathF.Sin(t * MathF.Tau * 261.63f) + MathF.Sin(t * MathF.Tau * 261.90f)) * 0.050f  // C4
                      + (MathF.Sin(t * MathF.Tau * 329.63f) + MathF.Sin(t * MathF.Tau * 329.93f)) * 0.036f; // E4
            pad *= padSwell;

            // ── Dorian colour: F#4 — raised 6th lifts mood, gives 80s feel ───
            float dorianSwell = 0.25f + 0.23f * MathF.Sin(t * MathF.Tau / 16f + MathF.PI * 0.2f);
            float dorian = (MathF.Sin(t * MathF.Tau * 369.99f)
                          + MathF.Sin(t * MathF.Tau * 370.35f)) * 0.038f * dorianSwell;

            // ── Bright A4: wide chorus, gentle 4-s pulse for groove ──────────
            // 4 s divides 32 s evenly (8 cycles) — loop stays seamless
            float brightPulse = 0.35f + 0.33f * MathF.Sin(t * MathF.Tau / 4f - MathF.PI * 0.5f);
            float bright = (MathF.Sin(t * MathF.Tau * 440.00f)
                          + MathF.Sin(t * MathF.Tau * 440.55f)
                          + MathF.Sin(t * MathF.Tau * 439.45f)) * 0.055f * brightPulse;

            // ── Shimmer: E5 + A5, prominent sparkle, 16-s swell ─────────────
            float shimSwell = 0.30f + 0.28f * MathF.Sin(t * MathF.Tau / 16f + MathF.PI * 0.75f);
            float shimmer = (MathF.Sin(t * MathF.Tau * 659.25f)
                           + MathF.Sin(t * MathF.Tau * 659.80f)) * 0.032f * shimSwell
                          + (MathF.Sin(t * MathF.Tau * 880.00f)
                           + MathF.Sin(t * MathF.Tau * 880.70f)) * 0.018f * shimSwell;

            float s = Mathf.Clamp(root + fifth + pad + dorian + bright + shimmer, -1f, 1f);
            _musicFrames[i] = new Vector2(s, s);
        }

        var gen = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.5f };
        _musicPlayer = new AudioStreamPlayer { Stream = gen, VolumeDb = MusicBaseDb, Bus = "Music" };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
        _musicPlayback = (AudioStreamGeneratorPlayback)_musicPlayer.GetStreamPlayback();
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
            int frames = _musicPlayback.GetFramesAvailable();
            for (int i = 0; i < frames; i++)
            {
                _musicPlayback.PushFrame(_musicFrames[_musicPos]);
                _musicPos = (_musicPos + 1) % _musicFrames.Length;
            }
        }

        if (_musicPlayer != null)
        {
            if (_musicDuckHold > 0f)
                _musicDuckHold = Mathf.Max(0f, _musicDuckHold - (float)delta);
            else
                _musicDuckDb = Mathf.Max(0f, _musicDuckDb - 14f * (float)delta);

            float targetDb = MusicBaseDb + _musicTensionDb - _musicDuckDb;
            _musicPlayer.VolumeDb = Mathf.Lerp(_musicPlayer.VolumeDb, targetDb, 12f * (float)delta);
        }
    }

    public void Play(string id, float pitchScale = 1f)
    {
        if (_headless) return;
        if (!_samples.TryGetValue(id, out var samples)) return;
        ulong nowMs = Time.GetTicksMsec();
        if (ShouldSkipMobileSfx(id, nowMs)) return;
        float dur = _durations[id];

        int idx    = _poolIdx;
        _poolIdx   = (_poolIdx + 1) % PoolSize;
        var player = _pool[idx];

        player.Stop();
        player.Bus = UiSoundIds.Contains(id) ? "UI" : "FX";
        player.VolumeDb = ResolveFxPlaybackDb(id);
        player.PitchScale = Mathf.Clamp(pitchScale * _speedFxPitch, 0.75f, 1.40f);
        player.Play();
        var playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        playback.PushBuffer(samples);
        _poolTimers[idx] = dur + 0.05f;
        _poolStopAtMs[idx] = nowMs + (ulong)Mathf.CeilToInt((dur + 0.05f) * 1000f);
    }

    public void SetSpeedFeel(float speedScale)
    {
        if (_headless) return;

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
    /// Synthesizes an electrical crackling sound — rapid irregular noise bursts in the
    /// 600–4000 Hz range, like a lightning arc or Jacob's ladder.
    /// </summary>
    private static (Vector2[] s, float d) Thunder(
        float dur = 0.9f,
        float vol = 0.88f,
        float crackVol = 0.70f,    // unused — kept for call-site compat
        float[]? rollFreqs = null,  // unused — kept for call-site compat
        float[]? bodyFreqs = null)  // unused — kept for call-site compat
    {
        int n   = (int)(Rate * dur);
        var rng = new Random(17);
        var arr = new Vector2[n];

        // ── "KA-PTSHOWRRRR" — one continuous event, three parallel decays ──
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

        // Spark sweep: 2500→400 Hz over 12ms — electrical "zap" onset, no physical slap
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

        // Periodic burst envelope — hard-clipped mid noise per burst
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
    /// Layer 2: Time-varying bandpass that sweeps 150→1500 Hz over 600ms — the "expanding wave."
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
}
