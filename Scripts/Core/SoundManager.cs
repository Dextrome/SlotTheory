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

    public override void _Ready()
    {
        Instance = this;

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
        Reg("shoot_rapid",  Tone(680f, 0.05f, vol: 0.38f, shape: 'q', env: 'f'));
        Reg("shoot_heavy",  Tone( 72f, 0.24f, vol: 0.72f, shape: 's', env: 'f'));
        Reg("shoot_marker", Tone(360f, 0.07f, vol: 0.32f, shape: 's', env: 'f'));

        // ── Enemy events ─────────────────────────────────────────────────
        Reg("hit",          Tone(520f, 0.03f, vol: 0.16f, shape: 'n', env: 'f'));
        Reg("die_basic",    Sweep(400f, 170f, 0.14f, vol: 0.55f));
        Reg("die_armored",  Sweep(155f,  50f, 0.24f, vol: 0.70f));
        Reg("leak",         Sweep(230f,  90f, 0.34f, vol: 0.60f));

        // ── Wave events ──────────────────────────────────────────────────
        Reg("wave_start", Seq(new[] { 300f, 500f },             gapMs: 20, noteLen: 0.09f, vol: 0.52f));
        Reg("wave_clear", Seq(new[] { 420f, 560f, 780f },       gapMs: 20, noteLen: 0.10f, vol: 0.58f));
        Reg("game_over",  Seq(new[] { 320f, 250f, 170f },       gapMs: 40, noteLen: 0.22f, vol: 0.65f));
        Reg("victory",    Seq(new[] { 400f, 520f, 680f, 900f }, gapMs: 25, noteLen: 0.12f, vol: 0.62f));

        // ── UI ───────────────────────────────────────────────────────────
        Reg("draft_pick",  Tone(740f, 0.07f, vol: 0.40f, shape: 's', env: 'f'));
        Reg("tower_place", Seq(new[] { 380f, 560f }, gapMs: 10, noteLen: 0.07f, vol: 0.46f));

        StartMusic();
    }

    // ── Background music ─────────────────────────────────────────────────

    /// <summary>
    /// Precomputes an 8-second ambient loop (Cm bass pulse + pad drone + shimmer)
    /// and streams it continuously via AudioStreamGenerator for seamless looping.
    /// </summary>
    /// <summary>
    /// Precomputes a 32-second ambient drone loop in the style of slow, evolving
    /// space-ambient music. No percussion or melody — pure layered drone texture.
    ///
    /// Technique: each note is three slightly detuned oscillators (±0.10–0.15 Hz).
    /// The detuning creates a slow beating / chorus effect (~7–10 s beat period)
    /// that makes static sine waves feel alive without any explicit LFO on pitch.
    /// Each harmonic group has an independent amplitude swell with a different period
    /// (32 s, 16 s, 8 s) so the mix continuously shifts over the loop.
    /// </summary>
    private void StartMusic()
    {
        const float LoopDur = 32f;
        int n = (int)(Rate * LoopDur);
        _musicFrames = new Vector2[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;

            // ── Deep root: C1 + C2, three detuned oscillators each ───────────
            // ±0.12 Hz offset → ~8 s beat period — very slow, living texture
            float root = (MathF.Sin(t * MathF.Tau * 32.70f)
                        + MathF.Sin(t * MathF.Tau * 32.82f)
                        + MathF.Sin(t * MathF.Tau * 32.58f)) * 0.10f
                       + (MathF.Sin(t * MathF.Tau * 65.41f)
                        + MathF.Sin(t * MathF.Tau * 65.53f)
                        + MathF.Sin(t * MathF.Tau * 65.29f)) * 0.07f;

            // ── Fifth: G1 + G2 with a slow swell (32-s period) ───────────────
            float fifthSwell = 0.40f + 0.38f * MathF.Sin(t * MathF.Tau / 32f - MathF.PI * 0.5f);
            float fifth = (MathF.Sin(t * MathF.Tau * 49.00f)
                         + MathF.Sin(t * MathF.Tau * 49.10f)) * 0.06f * fifthSwell
                        + (MathF.Sin(t * MathF.Tau * 98.00f)
                         + MathF.Sin(t * MathF.Tau * 98.09f)) * 0.04f * fifthSwell;

            // ── Minor third: Eb2 + Eb3, independent swell (16-s period) ─────
            float thirdSwell = 0.30f + 0.28f * MathF.Sin(t * MathF.Tau / 16f + MathF.PI * 0.3f);
            float third = (MathF.Sin(t * MathF.Tau * 77.78f)
                         + MathF.Sin(t * MathF.Tau * 77.87f)) * 0.05f * thirdSwell
                        + (MathF.Sin(t * MathF.Tau * 155.56f)
                         + MathF.Sin(t * MathF.Tau * 155.65f)) * 0.035f * thirdSwell;

            // ── Mid pad: full Cm cluster, detuned pairs, 8-s swell ───────────
            float padSwell = 0.45f + 0.35f * MathF.Sin(t * MathF.Tau / 8f + MathF.PI * 0.7f);
            float pad = (MathF.Sin(t * MathF.Tau * 130.81f) + MathF.Sin(t * MathF.Tau * 130.92f)) * 0.055f  // C3
                      + (MathF.Sin(t * MathF.Tau * 155.56f) + MathF.Sin(t * MathF.Tau * 155.67f)) * 0.040f  // Eb3
                      + (MathF.Sin(t * MathF.Tau * 196.00f) + MathF.Sin(t * MathF.Tau * 196.12f)) * 0.030f; // G3
            pad *= padSwell;

            // ── Shimmer: C4 + G4, very quiet, offset 32-s swell ─────────────
            float shimSwell = 0.20f + 0.20f * MathF.Sin(t * MathF.Tau / 32f + MathF.PI * 0.9f);
            float shimmer = (MathF.Sin(t * MathF.Tau * 261.63f) + MathF.Sin(t * MathF.Tau * 261.75f)) * 0.018f * shimSwell
                           + MathF.Sin(t * MathF.Tau * 392.00f) * 0.010f * shimSwell;

            float s = Mathf.Clamp(root + fifth + third + pad + shimmer, -1f, 1f);
            _musicFrames[i] = new Vector2(s, s);
        }

        var gen = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.5f };
        _musicPlayer = new AudioStreamPlayer { Stream = gen, VolumeDb = -5f, Bus = "Music" };
        AddChild(_musicPlayer);
        _musicPlayer.Play();
        _musicPlayback = (AudioStreamGeneratorPlayback)_musicPlayer.GetStreamPlayback();
    }

    public override void _Process(double delta)
    {
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

    public void Play(string id)
    {
        if (!_samples.TryGetValue(id, out var samples)) return;
        float dur = _durations[id];

        int idx    = _poolIdx;
        _poolIdx   = (_poolIdx + 1) % PoolSize;
        var player = _pool[idx];

        player.Stop();
        player.Play();
        var playback = (AudioStreamGeneratorPlayback)player.GetStreamPlayback();
        playback.PushBuffer(samples);
        _poolTimers[idx] = dur + 0.05f;
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
