using SlotTheory.Core;
using System.Collections.Generic;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Unit tests for the pure-C# static helpers in MusicMelodyLayer.
///
/// Phrase planning uses RNG and requires the Godot runtime so it is covered
/// by play-test verification rather than unit tests.
/// </summary>
public class MusicMelodyLayerTests
{
    // ── GetMelodyScaleNotes ───────────────────────────────────────────────

    [Theory]
    [InlineData(MusicMode.Dorian)]
    [InlineData(MusicMode.Mixolydian)]
    [InlineData(MusicMode.Phrygian)]
    public void All_returned_notes_are_within_lead_range(MusicMode mode)
    {
        var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, mode);
        foreach (int midi in notes)
        {
            Assert.True(midi >= MusicMelodyLayer.LeadMidiMin && midi <= MusicMelodyLayer.LeadMidiMax,
                $"mode={mode} midi={midi} outside [{MusicMelodyLayer.LeadMidiMin},{MusicMelodyLayer.LeadMidiMax}]");
        }
    }

    [Theory]
    [InlineData(MusicMode.Dorian)]
    [InlineData(MusicMode.Mixolydian)]
    [InlineData(MusicMode.Phrygian)]
    public void All_returned_notes_are_in_scale(MusicMode mode)
    {
        int   rootMidi = 45;  // A2
        int[] offsets  = MusicHarmony.GetScaleOffsets(mode);
        var   notes    = MusicMelodyLayer.GetMelodyScaleNotes(rootMidi, mode);

        foreach (int midi in notes)
        {
            int semitone = ((midi - rootMidi) % 12 + 12) % 12;
            Assert.Contains(semitone, offsets);
        }
    }

    [Theory]
    [InlineData(MusicMode.Dorian)]
    [InlineData(MusicMode.Mixolydian)]
    [InlineData(MusicMode.Phrygian)]
    public void Notes_are_sorted_ascending(MusicMode mode)
    {
        var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, mode);
        for (int i = 1; i < notes.Count; i++)
            Assert.True(notes[i] > notes[i - 1],
                $"mode={mode}: note at index {i} ({notes[i]}) ≤ previous ({notes[i - 1]})");
    }

    [Theory]
    [InlineData(MusicMode.Dorian,     14)]   // 2 complete octaves of 7-note scale = 14 notes
    [InlineData(MusicMode.Mixolydian, 14)]
    [InlineData(MusicMode.Phrygian,   14)]
    public void Note_count_covers_two_octaves(MusicMode mode, int expected)
    {
        var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, mode);
        Assert.Equal(expected, notes.Count);
    }

    [Fact]
    public void Dorian_root_A4_is_included()
    {
        // A4 = MIDI 69 should be in Dorian (it's the tonic in the lead octave)
        var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, MusicMode.Dorian);
        Assert.Contains(69, notes);
    }

    [Fact]
    public void Phrygian_has_bII_Bb_not_B_in_lead_range()
    {
        // Phrygian has Bb (offset 1) - midi 70 (Bb4) should appear, midi 71 (B4) should not
        int rootMidi = 45;  // A2; A4 = 69, Bb4 = 70, B4 = 71
        var notes    = MusicMelodyLayer.GetMelodyScaleNotes(rootMidi, MusicMode.Phrygian);
        Assert.Contains(70, notes);     // Bb4 - in Phrygian (offset 1)
        Assert.DoesNotContain(71, notes); // B4 - NOT in Phrygian
    }

    [Fact]
    public void Mixolydian_has_C_sharp_not_C_natural_in_lead_range()
    {
        // Mixolydian has C# (offset 4) - midi 61 (Db4/C#4) should appear, not midi 60 (C4)
        int rootMidi = 45;  // A2; C4 = 60, C#4 = 61
        var notes    = MusicMelodyLayer.GetMelodyScaleNotes(rootMidi, MusicMode.Mixolydian);
        Assert.Contains(61, notes);
        Assert.DoesNotContain(60, notes);
    }

    [Fact]
    public void Dorian_has_C_natural_not_C_sharp_in_lead_range()
    {
        // Dorian has C (offset 3) - midi 60 (C4) should appear, midi 61 (C#4) should not
        int rootMidi = 45;
        var notes    = MusicMelodyLayer.GetMelodyScaleNotes(rootMidi, MusicMode.Dorian);
        Assert.Contains(60, notes);
        Assert.DoesNotContain(61, notes);
    }

    // ── RestProbability ───────────────────────────────────────────────────

    [Theory]
    [InlineData(MusicTension.NearDeath, 0.65f)]
    [InlineData(MusicTension.Intro,     0.55f)]
    [InlineData(MusicTension.Building,  0.35f)]
    [InlineData(MusicTension.MidGame,   0.25f)]
    [InlineData(MusicTension.LateGame,  0.20f)]
    public void RestProbability_correct_per_tension(MusicTension tension, float expected)
    {
        Assert.Equal(expected, MusicMelodyLayer.RestProbability(tension));
    }

    [Fact]
    public void RestProbability_decreases_from_NearDeath_to_LateGame()
    {
        float nd  = MusicMelodyLayer.RestProbability(MusicTension.NearDeath);
        float i   = MusicMelodyLayer.RestProbability(MusicTension.Intro);
        float b   = MusicMelodyLayer.RestProbability(MusicTension.Building);
        float mg  = MusicMelodyLayer.RestProbability(MusicTension.MidGame);
        float lg  = MusicMelodyLayer.RestProbability(MusicTension.LateGame);

        Assert.True(nd > i,  "NearDeath should be sparser than Intro");
        Assert.True(i  > b,  "Intro should be sparser than Building");
        Assert.True(b  > mg, "Building should be sparser than MidGame");
        Assert.True(mg > lg, "MidGame should be sparser than LateGame");
    }

    [Theory]
    [InlineData(MusicTension.Intro)]
    [InlineData(MusicTension.Building)]
    [InlineData(MusicTension.MidGame)]
    [InlineData(MusicTension.LateGame)]
    [InlineData(MusicTension.NearDeath)]
    public void RestProbability_is_between_0_and_1(MusicTension tension)
    {
        float p = MusicMelodyLayer.RestProbability(tension);
        Assert.True(p >= 0f && p <= 1f,
            $"RestProbability({tension}) = {p} is not in [0, 1]");
    }

    // ── Integration: note pool covers melody range ────────────────────────

    [Fact]
    public void All_scale_notes_are_in_registered_MIDI_range()
    {
        // Verify all notes from all modes fit in the registered lead MIDI range.
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
        {
            var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, mode);
            foreach (int midi in notes)
            {
                Assert.True(midi >= 58 && midi <= 81,
                    $"mode={mode} midi={midi} outside registered lead range [58,81]");
            }
        }
    }

    [Fact]
    public void No_duplicate_notes_returned()
    {
        foreach (MusicMode mode in System.Enum.GetValues<MusicMode>())
        {
            var notes = MusicMelodyLayer.GetMelodyScaleNotes(45, mode);
            var set   = new HashSet<int>(notes);
            Assert.Equal(notes.Count, set.Count);
        }
    }
}
