using System;
using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Sample-accurate metronome for the procedural music system.
///
/// Accumulates time via Time.GetTicksUsec() — an absolute wall-clock that never
/// drifts regardless of frame rate or GC pauses. BPM changes can ramp smoothly
/// over a configurable number of bars (accelerando / ritardando).
///
/// Event firing order on the downbeat (beat 0 of bar 0):
///   PhraseFired → BarFired(0) → BeatFired(0)
/// On other bar downbeats (beat 0, bar 1–3):
///   BarFired(barIndex) → BeatFired(0)
/// On beats 1–3:
///   BeatFired(beatIndex)
/// </summary>
public partial class MusicClock : Node
{
    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Fires on every beat. beatIndex is 0–3 within the current bar.</summary>
    public event Action<int>? BeatFired;

    /// <summary>Fires on beat 0 of each bar. barIndex is 0–3 within the current phrase.</summary>
    public event Action<int>? BarFired;

    /// <summary>Fires on beat 0 of bar 0 — the downbeat of every 4-bar phrase.</summary>
    public event Action? PhraseFired;

    // ── Properties ────────────────────────────────────────────────────────

    public float Bpm        { get; private set; } = 72f;
    public bool  IsRunning  { get; private set; }
    public int   CurrentBeat { get; private set; }  // 0–3 within bar
    public int   CurrentBar  { get; private set; }  // 0–3 within phrase
    public int   TotalBeats  { get; private set; }  // monotonically increasing

    public float SecondsPerBeat   => 60f / Bpm;
    public float SecondsPerBar    => SecondsPerBeat * BeatsPerBar;
    public float SecondsPerPhrase => SecondsPerBar  * BarsPerPhrase;

    // ── Constants ─────────────────────────────────────────────────────────

    public const int BeatsPerBar   = 4;
    public const int BarsPerPhrase = 4;

    // ── Internal ──────────────────────────────────────────────────────────

    private ulong  _clockOriginUsec;    // Time.GetTicksUsec() at Start()
    private double _nextBeatSec;        // seconds from origin when the next beat fires

    private float  _rampStartBpm;
    private float  _rampTargetBpm;
    private double _rampOriginSec;
    private double _rampDurationSec;    // 0 = no ramp active

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Start the clock from beat 0 at the given BPM.
    /// The first beat fires on the next _Process frame.
    /// </summary>
    public void Start(float bpm = 72f)
    {
        Bpm              = bpm;
        _rampTargetBpm   = bpm;
        _rampDurationSec = 0;
        _clockOriginUsec = Time.GetTicksUsec();
        _nextBeatSec     = 0;
        CurrentBeat      = 0;
        CurrentBar       = 0;
        TotalBeats       = 0;
        IsRunning        = true;
    }

    public void Stop() => IsRunning = false;

    /// <summary>
    /// Schedule a tempo change. rampBars == 0 snaps immediately;
    /// otherwise the tempo interpolates linearly over that many bars at the current BPM.
    /// </summary>
    public void SetBpm(float newBpm, float rampBars = 0f)
    {
        if (rampBars <= 0f)
        {
            Bpm              = newBpm;
            _rampTargetBpm   = newBpm;
            _rampDurationSec = 0;
            return;
        }
        _rampStartBpm    = Bpm;
        _rampTargetBpm   = newBpm;
        _rampOriginSec   = ElapsedSec();
        _rampDurationSec = rampBars * BeatsPerBar * (60.0 / Bpm);
    }

    // ── Node callbacks ─────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (!IsRunning) return;

        // Advance BPM ramp
        if (_rampDurationSec > 0)
        {
            double progress = Math.Min(1.0, (ElapsedSec() - _rampOriginSec) / _rampDurationSec);
            Bpm = _rampStartBpm + (_rampTargetBpm - _rampStartBpm) * (float)progress;
            if (progress >= 1.0)
                _rampDurationSec = 0;
        }

        // Fire beat events.
        // The while handles the (unlikely) case where multiple beats elapse in one frame
        // (e.g. after a GC pause or OS suspend).
        double nowSec = ElapsedSec();
        while (nowSec >= _nextBeatSec)
        {
            if (CurrentBeat == 0)
            {
                if (CurrentBar == 0)
                    PhraseFired?.Invoke();
                BarFired?.Invoke(CurrentBar);
            }
            BeatFired?.Invoke(CurrentBeat);

            _nextBeatSec += 60.0 / Bpm;
            CurrentBeat   = (CurrentBeat + 1) % BeatsPerBar;
            if (CurrentBeat == 0)
                CurrentBar = (CurrentBar + 1) % BarsPerPhrase;
            TotalBeats++;
        }
    }

    private double ElapsedSec() =>
        (Time.GetTicksUsec() - _clockOriginUsec) / 1_000_000.0;
}
