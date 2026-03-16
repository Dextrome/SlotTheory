using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for MusicHarmony — the pure-C# scale/chord/tension logic
/// that drives the procedural music system.
///
/// MusicClock and MusicBassLayer require the Godot runtime and are covered
/// by integration (play-test) verification rather than unit tests.
/// </summary>
public class MusicHarmonyTests
{
    // ── Scale offsets ─────────────────────────────────────────────────────

    [Fact]
    public void Dorian_has_7_scale_degrees()
    {
        Assert.Equal(7, MusicHarmony.GetScaleOffsets(MusicMode.Dorian).Length);
    }

    [Fact]
    public void Dorian_intervals_are_correct()
    {
        // A Dorian: A B C D E F# G — semitone offsets from root
        int[] expected = { 0, 2, 3, 5, 7, 9, 10 };
        Assert.Equal(expected, MusicHarmony.GetScaleOffsets(MusicMode.Dorian));
    }

    [Fact]
    public void Mixolydian_has_raised_third_vs_Dorian()
    {
        // Mixolydian has C# (offset 4) where Dorian has C (offset 3)
        var dor = MusicHarmony.GetScaleOffsets(MusicMode.Dorian);
        var mix = MusicHarmony.GetScaleOffsets(MusicMode.Mixolydian);
        Assert.Equal(3, dor[2]);
        Assert.Equal(4, mix[2]);
    }

    [Fact]
    public void Phrygian_has_lowered_second()
    {
        // Phrygian has Bb (offset 1) where Dorian has B (offset 2)
        var phr = MusicHarmony.GetScaleOffsets(MusicMode.Phrygian);
        Assert.Equal(1, phr[1]);
    }

    [Fact]
    public void All_modes_start_at_root_offset_zero()
    {
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
            Assert.Equal(0, MusicHarmony.GetScaleOffsets(mode)[0]);
    }

    // ── BassNormalize ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   0)]   // root — unchanged
    [InlineData(5,   5)]   // D above A — close enough, stays
    [InlineData(6,   6)]   // boundary: 6 is NOT > 6, stays
    [InlineData(7,  -5)]   // E (fifth) — folded down an octave
    [InlineData(10, -2)]   // G subtonic — folded down
    [InlineData(12,  0)]   // octave above root — folds back to root offset
    [InlineData(-1, -1)]   // negative values pass through unchanged
    [InlineData(-5, -5)]   // already-low values unchanged
    public void BassNormalize_maps_correctly(int input, int expected)
    {
        Assert.Equal(expected, MusicHarmony.BassNormalize(input));
    }

    [Fact]
    public void BassNormalize_all_progression_offsets_stay_in_bass_range()
    {
        // Every raw chord offset, once normalized, should keep the bass
        // within ±6 semitones of the tonic (i.e. result in [-5, 6]).
        int[] rawOffsets = { 0, 1, 2, 3, 5, 7, 8, 9, 10 };
        foreach (int raw in rawOffsets)
        {
            int norm = MusicHarmony.BassNormalize(raw);
            Assert.True(norm >= -6 && norm <= 6,
                $"BassNormalize({raw}) = {norm} is outside [-6, 6]");
        }
    }

    // ── ComputeTension ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  10, MusicTension.Intro)]
    [InlineData(3,  10, MusicTension.Intro)]
    [InlineData(4,  10, MusicTension.Building)]
    [InlineData(7,  10, MusicTension.Building)]
    [InlineData(8,  10, MusicTension.MidGame)]
    [InlineData(14, 10, MusicTension.MidGame)]
    [InlineData(15, 10, MusicTension.LateGame)]
    [InlineData(20, 10, MusicTension.LateGame)]
    public void ComputeTension_uses_wave_index_tiers(int wave, int lives, MusicTension expected)
    {
        Assert.Equal(expected, MusicHarmony.ComputeTension(wave, lives));
    }

    [Theory]
    [InlineData(1,  3)]   // early wave, at near-death threshold
    [InlineData(8,  3)]   // mid-game wave index but near-death overrides
    [InlineData(15, 1)]   // late wave and only 1 life
    [InlineData(20, 3)]   // final wave, exactly at threshold
    public void ComputeTension_NearDeath_overrides_wave_tier(int wave, int lives)
    {
        Assert.Equal(MusicTension.NearDeath, MusicHarmony.ComputeTension(wave, lives));
    }

    [Fact]
    public void ComputeTension_lives_4_is_not_NearDeath()
    {
        Assert.NotEqual(MusicTension.NearDeath, MusicHarmony.ComputeTension(1, 4));
    }

    // ── TensionToMode ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(MusicTension.Intro,     MusicMode.Dorian)]
    [InlineData(MusicTension.Building,  MusicMode.Dorian)]
    [InlineData(MusicTension.MidGame,   MusicMode.Mixolydian)]
    [InlineData(MusicTension.LateGame,  MusicMode.Phrygian)]
    [InlineData(MusicTension.NearDeath, MusicMode.Phrygian)]
    public void TensionToMode_maps_correctly(MusicTension tension, MusicMode expected)
    {
        Assert.Equal(expected, MusicHarmony.TensionToMode(tension));
    }

    // ── TensionToBpm ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(MusicTension.Intro,     72f)]
    [InlineData(MusicTension.Building,  72f)]
    [InlineData(MusicTension.MidGame,   88f)]
    [InlineData(MusicTension.LateGame,  96f)]
    [InlineData(MusicTension.NearDeath, 96f)]  // same as LateGame; callers skip BPM change for NearDeath
    public void TensionToBpm_maps_correctly(MusicTension tension, float expected)
    {
        Assert.Equal(expected, MusicHarmony.TensionToBpm(tension));
    }

    [Fact]
    public void BPM_increases_monotonically_through_non_NearDeath_tiers()
    {
        float intro    = MusicHarmony.TensionToBpm(MusicTension.Intro);
        float building = MusicHarmony.TensionToBpm(MusicTension.Building);
        float midgame  = MusicHarmony.TensionToBpm(MusicTension.MidGame);
        float late     = MusicHarmony.TensionToBpm(MusicTension.LateGame);
        Assert.True(intro <= building);
        Assert.True(building < midgame);
        Assert.True(midgame <= late);
    }

    // ── ProgressionCount ─────────────────────────────────────────────────

    [Theory]
    [InlineData(MusicMode.Dorian,     4)]
    [InlineData(MusicMode.Mixolydian, 4)]
    [InlineData(MusicMode.Phrygian,   3)]
    public void ProgressionCount_is_correct(MusicMode mode, int expected)
    {
        Assert.Equal(expected, MusicHarmony.ProgressionCount(mode));
    }

    // ── GetChordRootOffset ────────────────────────────────────────────────

    [Fact]
    public void Dorian_prog0_returns_known_progression()
    {
        // i – IV – i – VII: offsets 0, 5, 0, 10
        Assert.Equal( 0, MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 0));
        Assert.Equal( 5, MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 1));
        Assert.Equal( 0, MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 2));
        Assert.Equal(10, MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 3));
    }

    [Fact]
    public void Phrygian_prog0_has_bII_chord()
    {
        // i – bII – i – bVII: bar 1 is bII = offset 1
        Assert.Equal(1, MusicHarmony.GetChordRootOffset(MusicMode.Phrygian, 0, 1));
    }

    [Fact]
    public void BarInPhrase_wraps_at_4()
    {
        // bar 4 should equal bar 0 for a 4-bar progression
        Assert.Equal(
            MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 0),
            MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 4));
    }

    [Fact]
    public void ProgressionIndex_wraps_past_count()
    {
        // index 4 on a 4-entry table should equal index 0
        Assert.Equal(
            MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 0, 0),
            MusicHarmony.GetChordRootOffset(MusicMode.Dorian, 4, 0));
    }

    [Fact]
    public void All_progression_offsets_are_non_negative()
    {
        // Raw offsets are 0–10; BassNormalize is applied later by the bass layer.
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
        for (int prog = 0; prog < MusicHarmony.ProgressionCount(mode); prog++)
        for (int bar  = 0; bar  < 4; bar++)
        {
            int offset = MusicHarmony.GetChordRootOffset(mode, prog, bar);
            Assert.True(offset >= 0 && offset <= 11,
                $"mode={mode} prog={prog} bar={bar} → offset {offset} out of [0,11]");
        }
    }

    // ── ScaleDegreeToMidi ─────────────────────────────────────────────────

    [Fact]
    public void ScaleDegreeToMidi_degree0_returns_root()
    {
        Assert.Equal(45, MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, 0));
    }

    [Fact]
    public void ScaleDegreeToMidi_degree6_returns_subtonic()
    {
        // Dorian degree 6 = offset 10
        Assert.Equal(55, MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, 6));
    }

    [Fact]
    public void ScaleDegreeToMidi_degree7_wraps_to_root()
    {
        // degree 7 % 7 = 0 = root
        Assert.Equal(45, MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, 7));
    }

    [Fact]
    public void ScaleDegreeToMidi_negative_degree_wraps_backwards()
    {
        // degree -1 = last degree = offset 10
        Assert.Equal(55, MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, -1));
    }

    [Fact]
    public void ScaleDegreeToMidi_octave_shifts_by_12()
    {
        int base_  = MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, 0, octave: 0);
        int raised = MusicHarmony.ScaleDegreeToMidi(45, MusicMode.Dorian, 0, octave: 1);
        Assert.Equal(base_ + 12, raised);
    }

    // ── Integration: bass range stays reasonable ──────────────────────────

    [Fact]
    public void BassNormalized_offsets_keep_A2_root_within_registered_range()
    {
        // With root MIDI 45 (A2), all BassNormalized chord offsets should keep
        // the bass note within the registered note pool range [28, 57].
        const int root = 45;
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
        for (int prog = 0; prog < MusicHarmony.ProgressionCount(mode); prog++)
        for (int bar  = 0; bar  < 4; bar++)
        {
            int raw    = MusicHarmony.GetChordRootOffset(mode, prog, bar);
            int norm   = MusicHarmony.BassNormalize(raw);
            int midi   = root + norm;
            Assert.True(midi >= 28 && midi <= 57,
                $"mode={mode} prog={prog} bar={bar} → bass MIDI {midi} outside [28,57]");
        }
    }

    [Fact]
    public void Fifth_above_BassNormalized_stays_within_registered_range()
    {
        // The fifth (root + norm + 7) must also stay in [28, 57] without needing
        // MusicBassLayer.Clamp() to do multiple iterations.
        const int root = 45;
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
        for (int prog = 0; prog < MusicHarmony.ProgressionCount(mode); prog++)
        for (int bar  = 0; bar  < 4; bar++)
        {
            int raw   = MusicHarmony.GetChordRootOffset(mode, prog, bar);
            int norm  = MusicHarmony.BassNormalize(raw);
            int fifth = root + norm + 7;
            // Clamp logic: subtract 12 if > 57
            if (fifth > 57) fifth -= 12;
            Assert.True(fifth >= 28 && fifth <= 57,
                $"mode={mode} prog={prog} bar={bar} → fifth MIDI {fifth} outside [28,57]");
        }
    }
}
