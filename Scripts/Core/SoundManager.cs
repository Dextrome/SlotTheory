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
    private const float MaxDur   = 2.0f;   // buffer length — longer than any sound

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

    private bool _headless;

    public override void _Ready()
    {
        Instance  = this;
        _headless = DisplayServer.GetName() == "headless";
        if (_headless) return;   // skip all audio setup in bot/headless mode

        _pool       = new AudioStreamPlayer[PoolSize];
        _poolTimers = new float[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            var gen    = new AudioStreamGenerator { MixRate = Rate, BufferLength = MaxDur };
            var player = new AudioStreamPlayer { Stream = gen, Bus = "FX" };
            AddChild(player);
            _pool[i] = player;
        }

        // ── Tower attacks ────────────────────────────────────────────────
        Reg("shoot_rapid",  Tone(680f, 0.05f, vol: 0.33f, shape: 'q', env: 'f'));
        Reg("shoot_heavy",  Tone( 72f, 0.24f, vol: 0.72f, shape: 's', env: 'f'));
        Reg("shoot_marker", Tone(360f, 0.07f, vol: 0.32f, shape: 's', env: 'f'));

        // ── Enemy events ─────────────────────────────────────────────────
        Reg("hit",          Tone(520f, 0.03f, vol: 0.16f, shape: 'n', env: 'f'));
        Reg("die_basic",    Sweep(400f, 170f, 0.14f, vol: 0.55f));
        Reg("die_armored",  Sweep(155f,  50f, 0.24f, vol: 0.70f));
        Reg("die_swift",    Sweep(900f, 400f, 0.08f, vol: 0.45f));
        Reg("leak",         Sweep(230f,  90f, 0.34f, vol: 0.60f));

        // ── Wave events ──────────────────────────────────────────────────
        Reg("wave_start", Seq(new[] { 300f, 500f },               gapMs: 20, noteLen: 0.09f, vol: 0.52f));
        Reg("wave20_start", Seq(new[] { 140f, 180f, 220f, 310f }, gapMs: 16, noteLen: 0.10f, vol: 0.70f));
        Reg("wave_clear", Seq(new[] { 420f, 560f, 780f },         gapMs: 20, noteLen: 0.10f, vol: 0.58f));
        Reg("game_over",  Seq(new[] { 320f, 250f, 170f },       gapMs: 40, noteLen: 0.22f, vol: 0.65f));
        Reg("victory",    Seq(new[] { 400f, 520f, 680f, 900f }, gapMs: 25, noteLen: 0.12f, vol: 0.62f));

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
        Reg("low_heartbeat",   Seq(new[] { 74f, 58f }, gapMs: 40, noteLen: 0.10f, vol: 0.42f));
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
        _musicPlayer = new AudioStreamPlayer { Stream = gen, VolumeDb = -3f, Bus = "Music" };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
        _musicPlayback = (AudioStreamGeneratorPlayback)_musicPlayer.GetStreamPlayback();
    }

    public override void _Process(double delta)
    {
        if (_headless) return;
        // Stop SFX players whose sound has finished
        for (int i = 0; i < PoolSize; i++)
        {
            if (_poolTimers[i] > 0f)
            {
                _poolTimers[i] -= (float)delta;
                if (_poolTimers[i] <= 0f)
                    _pool[i].Stop();
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
    }

    public void Play(string id, float pitchScale = 1f)
    {
        if (_headless) return;
        if (!_samples.TryGetValue(id, out var samples)) return;
        float dur = _durations[id];

        int idx    = _poolIdx;
        _poolIdx   = (_poolIdx + 1) % PoolSize;
        var player = _pool[idx];

        player.Stop();
        player.PitchScale = Mathf.Clamp(pitchScale * _speedFxPitch, 0.75f, 1.40f);
        player.Play();
        var playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        playback.PushBuffer(samples);
        _poolTimers[idx] = dur + 0.05f;
    }

    public void SetSpeedFeel(float speedScale)
    {
        if (_headless) return;

        // Subtle "machine pushed harder" effect at 2x/3x.
        float speedOver = Mathf.Max(0f, speedScale - 1f);
        _speedFxPitch = 1f + speedOver * 0.026f; // 2x: 1.026, 3x: 1.052

        if (_musicPlayer != null)
            _musicPlayer.PitchScale = 1f + speedOver * 0.012f;
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
}
