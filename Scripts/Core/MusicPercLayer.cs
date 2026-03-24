using Godot;

namespace SlotTheory.Core;

/// <summary>
/// 808-style percussion layer for the procedural music system.
///
/// Subscribes to MusicClock.SubBeatFired (8th-note grid, index 0–7 per bar)
/// and plays kick/snare/hat patterns that vary with the current tension tier.
///
/// 8th-note index map:
///   0 = beat 1    1 = beat 1-and
///   2 = beat 2    3 = beat 2-and
///   4 = beat 3    5 = beat 3-and
///   6 = beat 4    7 = beat 4-and
///
/// Density:
///   0 = silent (near-death, draft, wave-clear breath)
///   1 = active (follows tension-based pattern)
/// </summary>
public partial class MusicPercLayer : Node
{
    private MusicClock   _clock   = null!;
    private MusicTension _tension = MusicTension.Intro;
    private System.Random _rng   = new();

    // Sub-beat index within the current bar (mirrors MusicClock._subBeatIdx)
    private int _subBeatInBar;

    // Fill bar: bar 3 of each phrase (0-indexed) gets a drum fill
    private bool _isFillBar;

    // Hat variation: some bars drop to quarter-note hats
    private bool _useQuarterHats;

    // Surge fill: play an intense 1-bar accent pattern on the next bar after a global surge
    public bool SurgeFillPending { get; set; } = false;
    private bool _surgeFillThisBar;

    // Density arc: suppress hats for the first N bars of a wave so they "fill in"
    public int WaveIntroBarsLeft { get; set; } = 0;
    private bool _introHatsSuppressed;

    // Per-map feel controls (set by MusicDirector from the map profile)
    // HatQuarterChance: probability per bar that 8th-note hats drop to quarters (0=never, 1=always)
    // Style: selects the kick/snare groove table for this map
    public float      HatQuarterChance { get; set; } = 0.20f;
    public DrumStyle  Style            { get; set; } = DrumStyle.Standard;

    public enum DrumStyle { Standard, HalfTime, FourOnFloor, Syncopated, Funk }

    public int  Density { get; set; } = 0;
    public bool Active  { get; set; } = false;

    public MusicTension Tension
    {
        get => _tension;
        set => _tension = value;
    }

    // ── Patterns: bool[8] per sub-beat position ────────────────────────────
    // Four groove styles; each has 4 tension tiers (Intro/Building/MidGame/LateGame).
    // Index map: 0=beat1, 1=beat1-and, 2=beat2, 3=beat2-and, 4=beat3, 5=beat3-and, 6=beat4, 7=beat4-and

    // ── Standard (crossroads) - classic hip-hop/electronic backbeat ──────
    private static readonly bool[][] KickStandard =
    {
        new[] { true,  false, false, false, true,  false, false, false },  // Intro:    1 + 3
        new[] { true,  false, false, false, true,  true,  false, false },  // Building: 1 + 3 + 3-and
        new[] { true,  false, false, true,  true,  false, false, false },  // MidGame:  1 + 2-and + 3
        new[] { true,  false, false, true,  true,  false, false, true  },  // LateGame: 1 + 2-and + 3 + 4-and
    };
    private static readonly bool[][] SnareStandard =
    {
        new[] { false, false, true,  false, false, false, true,  false },  // Intro–MidGame: 2 + 4
        new[] { false, false, true,  false, false, false, true,  false },
        new[] { false, false, true,  false, false, false, true,  false },
        new[] { false, false, true,  false, false, true,  true,  false },  // LateGame: 2 + 3-and + 4
    };

    // ── HalfTime (orbit) - slow, weighty; snare lands on beat 3 only ───────
    private static readonly bool[][] KickHalfTime =
    {
        new[] { true,  false, false, false, false, false, false, false },  // Intro:    1 only
        new[] { true,  false, false, false, false, true,  false, false },  // Building: 1 + 3-and
        new[] { true,  true,  false, false, false, false, false, false },  // MidGame:  1 + 1-and (heavy emphasis)
        new[] { true,  true,  false, false, false, true,  false, true  },  // LateGame: 1 + 1-and + 3-and + 4-and
    };
    private static readonly bool[][] SnareHalfTime =
    {
        new[] { false, false, false, false, true,  false, false, false },  // Intro–MidGame: beat 3 only (half-time snare)
        new[] { false, false, false, false, true,  false, false, false },
        new[] { false, false, false, false, true,  false, false, false },
        new[] { false, false, false, true,  true,  false, true,  false },  // LateGame: 2-and + 3 + 4 (snare fills out)
    };

    // ── FourOnFloor (pinch_bleed) - driving techno, kick on every beat ──────────
    private static readonly bool[][] KickFourOnFloor =
    {
        new[] { true,  false, true,  false, true,  false, true,  false },  // Intro:    1+2+3+4 (pure 4-on-floor)
        new[] { true,  false, true,  false, true,  false, true,  true  },  // Building: + 4-and
        new[] { true,  false, true,  true,  true,  false, true,  true  },  // MidGame:  + 2-and
        new[] { true,  true,  true,  true,  true,  false, true,  true  },  // LateGame: nearly every 8th
    };
    private static readonly bool[][] SnareFourOnFloor =
    {
        new[] { false, false, true,  false, false, false, true,  false },  // standard backbeat 2+4
        new[] { false, false, true,  false, false, false, true,  false },
        new[] { false, false, true,  false, false, false, true,  false },
        new[] { false, false, true,  false, false, true,  true,  false },  // LateGame: + 3-and snare
    };

    // ── Syncopated (random_map) - offbeat, avoids the downbeat ──────────────
    private static readonly bool[][] KickSyncopated =
    {
        new[] { false, true,  false, false, true,  false, false, false },  // Intro:    1-and + 3 (push ahead of beat)
        new[] { false, true,  false, false, true,  false, false, true  },  // Building: + 4-and
        new[] { false, true,  false, true,  true,  false, false, true  },  // MidGame:  + 2-and
        new[] { true,  true,  false, true,  false, false, true,  true  },  // LateGame: scattered, unpredictable
    };
    private static readonly bool[][] SnareSyncopated =
    {
        new[] { false, false, false, true,  false, false, true,  false },  // Intro:    2-and + 4
        new[] { false, false, false, true,  false, false, true,  false },
        new[] { false, false, false, true,  false, true,  true,  false },  // MidGame:  + 3-and
        new[] { false, true,  false, true,  false, true,  true,  false },  // LateGame: 1-and + 2-and + 3-and + 4
    };

    // ── Funk (global default) - syncopated pocket with off-beat pushes ──
    private static readonly bool[][] KickFunk =
    {
        new[] { true,  false, false, true,  true,  false, false, true  },  // Intro:    1 + 2-and + 3 + 4-and
        new[] { true,  true,  false, true,  true,  false, false, true  },  // Building: + 1-and pickup
        new[] { true,  true,  false, true,  true,  true,  false, true  },  // MidGame:  + 3-and
        new[] { true,  true,  true,  true,  true,  true,  false, true  },  // LateGame: dense but keeps beat-4 space
    };
    private static readonly bool[][] SnareFunk =
    {
        new[] { false, false, true,  false, false, false, true,  false },  // Intro:    2 + 4
        new[] { false, false, true,  false, false, false, true,  false },  // Building
        new[] { false, false, true,  true,  false, false, true,  false },  // MidGame:  + 2-and ghost
        new[] { false, false, true,  true,  false, true,  true,  false },  // LateGame: + 2-and + 3-and ghosts
    };

    // ── Hat patterns (shared across styles) ─────────────────────────────────
    private static readonly bool[][] HatCPatterns =
    {
        new[] { true,  false, true,  false, true,  false, true,  false },  // Intro:    quarter notes
        new[] { true,  true,  true,  true,  true,  true,  true,  true  },  // Building: 8th notes
        new[] { true,  true,  true,  true,  true,  true,  true,  true  },  // MidGame:  8th notes
        new[] { true,  true,  true,  true,  true,  true,  true,  true  },  // LateGame: 8th notes
    };
    // Quarter-note hat pattern used during variation bars
    private static readonly bool[] HatCQuarterPattern =
        { true, false, true, false, true, false, true, false };

    // Open hat only at MidGame+ on off-beats 3 and 7
    private static readonly bool[][] HatOPatterns =
    {
        new[] { false, false, false, false, false, false, false, false },  // Intro
        new[] { false, false, false, false, false, false, false, false },  // Building
        new[] { false, false, false, true,  false, false, false, true  },  // MidGame
        new[] { false, false, false, true,  false, false, false, true  },  // LateGame
    };

    // Fill pattern overlays: extra hits on steps 5, 6, 7 of bar 3
    private static readonly bool[] FillKick  = { false, false, false, false, false, false, true,  false };
    private static readonly bool[] FillSnare = { false, false, false, false, false, true,  true,  true  };

    // Surge fill: 1-bar accent fired after a global surge - double kick + snare roll
    // Index map: 0=beat1, 1=beat1-and, 2=beat2, 3=beat2-and, 4=beat3, 5=beat3-and, 6=beat4, 7=beat4-and
    private static readonly bool[] SurgeFillKick  = { true, true, false, false, true, false, true, true  };
    private static readonly bool[] SurgeFillSnare = { false, false, false, false, true, true, true, true  };

    // Volume constants (absolute dB passed to PlayPerc)
    private const float KickVol  = -13.5f;
    private const float SnareVol = -14.5f;
    private const float HatCVol  = -15.5f;  // hats stay behind kick/snare but with more energy
    private const float HatOVol  = -13.5f;

    // ── Configuration ──────────────────────────────────────────────────────

    public void Configure(MusicClock clock, MusicTension tension)
    {
        _clock   = clock;
        _tension = tension;

        _clock.SubBeatFired += OnSubBeat;
        _clock.BarFired     += OnBar;
    }

    // ── Clock event handlers ───────────────────────────────────────────────

    private void OnBar(int barIndex)
    {
        // Keep our sub-beat-in-bar counter in sync with bar resets
        _subBeatInBar = 0;

        // Bar 3 of each 4-bar phrase (0-indexed) is the fill bar
        _isFillBar = (barIndex % 4 == 3);

        // Hat variation: chance to drop to quarter-note hats for this bar.
        // Only applies at Building+ tension where 8th hats are the default.
        int patternIdx = TensionToPatternIndex(_tension);
        bool eighthsDefault = patternIdx >= 1;
        _useQuarterHats = eighthsDefault && HatQuarterChance > 0f && (_rng.NextDouble() < HatQuarterChance);

        // Surge fill: latch for this bar, clear pending flag
        _surgeFillThisBar  = SurgeFillPending;
        SurgeFillPending   = false;

        // Density arc: snapshot suppression state then decrement counter
        _introHatsSuppressed = WaveIntroBarsLeft > 0;
        if (WaveIntroBarsLeft > 0) WaveIntroBarsLeft--;
    }

    private void OnSubBeat(int globalIdx)
    {
        _subBeatInBar = globalIdx;

        if (!Active || Density == 0) return;

        int patternIdx = TensionToPatternIndex(_tension);
        var (kickTable, snareTable) = Style switch
        {
            DrumStyle.HalfTime    => (KickHalfTime,    SnareHalfTime),
            DrumStyle.FourOnFloor => (KickFourOnFloor, SnareFourOnFloor),
            DrumStyle.Syncopated  => (KickSyncopated,  SnareSyncopated),
            DrumStyle.Funk        => (KickFunk,        SnareFunk),
            _                     => (KickStandard,    SnareStandard),
        };
        var kick  = kickTable[patternIdx];
        var snare = snareTable[patternIdx];
        var hatO  = HatOPatterns[patternIdx];

        // Hat pattern: quarter variation or normal (fill bar suppresses hats on steps 5–7)
        bool[] hatC = _useQuarterHats
            ? HatCQuarterPattern
            : HatCPatterns[patternIdx];

        int i = _subBeatInBar;

        // Surge fill overrides the normal pattern for the whole bar
        bool playKick, playSnare;
        if (_surgeFillThisBar)
        {
            playKick  = SurgeFillKick[i];
            playSnare = SurgeFillSnare[i];
        }
        else
        {
            // Merge fill overlay on the fill bar
            playKick  = kick[i]  || (_isFillBar && FillKick[i]);
            playSnare = snare[i] || (_isFillBar && FillSnare[i]);
        }

        // Fill bar: suppress open hat and hats on steps 5–7 to let the fill breathe.
        // Density arc: suppress all hats for the first 2 bars of each wave.
        bool suppressHat = (_isFillBar && i >= 5) || _introHatsSuppressed;

        if (playKick)  SoundManager.Instance?.PlayPerc("perc_kick",  KickVol);

        // Fill snare hits (non-backbeat) play quieter like ghost notes
        bool isFunkGhost = Style == DrumStyle.Funk && (i == 3 || i == 5);
        if (playSnare)
        {
            bool isBackbeat = snare[i] && !isFunkGhost;
            float vol = isBackbeat ? SnareVol : SnareVol - 4f;
            SoundManager.Instance?.PlayPerc("perc_snare", vol);
        }

        if (!suppressHat)
        {
            bool funkOpenAccent = Style == DrumStyle.Funk
                && (i == 1 || i == 5)
                && !_useQuarterHats
                && _rng.NextDouble() < 0.28;

            if (hatO[i] || funkOpenAccent)
            {
                float openVol = funkOpenAccent ? HatOVol - 1.2f : HatOVol;
                SoundManager.Instance?.PlayPerc("perc_hat_o", openVol);
            }
            else if (hatC[i])
            {
                // Random hat velocity variation (±2 dB) for a more human feel
                float hatVol = HatCVol + (_rng.NextSingle() * 4f - 2f);
                SoundManager.Instance?.PlayPerc("perc_hat_c", hatVol);
            }
        }

        // Ghost snare: 12% chance on un-patterned beats at Building+ tension (suppressed during intro arc)
        if (!playSnare && patternIdx >= 1 && !_introHatsSuppressed && _rng.NextDouble() < 0.12)
            SoundManager.Instance?.PlayPerc("perc_snare", SnareVol - 6f);  // ghost = quieter
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static int TensionToPatternIndex(MusicTension tension) => tension switch
    {
        MusicTension.Intro     => 0,
        MusicTension.Building  => 1,
        MusicTension.MidGame   => 2,
        MusicTension.LateGame  => 3,
        MusicTension.NearDeath => 0,  // near-death: Density set to 0 by director anyway
        _                      => 0,
    };
}
