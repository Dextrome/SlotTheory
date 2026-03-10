using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using Xunit;

namespace SlotTheory.Tests;

public class BuildSnapshotCodecTests
{
    // ── PackSlot / UnpackSlot roundtrip ────────────────────────────────────────

    [Fact]
    public void PackSlot_EmptySlot_ReturnsZero()
    {
        var slot = new RunSlotBuild("", []);
        Assert.Equal(0, BuildSnapshotCodec.PackSlot(slot));
    }

    [Fact]
    public void PackSlot_UnknownTower_ReturnsZero()
    {
        var slot = new RunSlotBuild("nonexistent_tower", []);
        Assert.Equal(0, BuildSnapshotCodec.PackSlot(slot));
    }

    [Theory]
    [InlineData("rapid_shooter")]
    [InlineData("heavy_cannon")]
    [InlineData("marker_tower")]
    [InlineData("chain_tower")]
    [InlineData("rift_prism")]
    public void PackSlot_KnownTower_NoMods_IsNonZero(string towerId)
    {
        var slot = new RunSlotBuild(towerId, []);
        Assert.True(BuildSnapshotCodec.PackSlot(slot) > 0);
    }

    [Fact]
    public void Pack_Unpack_EmptyBuild_RoundTrips()
    {
        var original = BuildSnapshotCodec.Empty();
        var packed   = BuildSnapshotCodec.Pack(original);
        var restored = BuildSnapshotCodec.Unpack(packed);

        for (int i = 0; i < Balance.SlotCount; i++)
        {
            Assert.Equal("", restored.Slots[i].TowerId);
            Assert.Empty(restored.Slots[i].ModifierIds);
        }
    }

    [Fact]
    public void Pack_Unpack_SingleTower_NoMods_RoundTrips()
    {
        var slots = MakeEmptySlots();
        slots[0] = new RunSlotBuild("rapid_shooter", []);
        var original = new RunBuildSnapshot(slots);

        var restored = BuildSnapshotCodec.Unpack(BuildSnapshotCodec.Pack(original));

        Assert.Equal("rapid_shooter", restored.Slots[0].TowerId);
        Assert.Empty(restored.Slots[0].ModifierIds);
        Assert.Equal("", restored.Slots[1].TowerId);
    }

    [Fact]
    public void Pack_Unpack_TowerWithMods_RoundTrips()
    {
        var slots = MakeEmptySlots();
        slots[2] = new RunSlotBuild("heavy_cannon", ["momentum", "overkill", "focus_lens"]);
        var original = new RunBuildSnapshot(slots);

        var restored = BuildSnapshotCodec.Unpack(BuildSnapshotCodec.Pack(original));

        Assert.Equal("heavy_cannon", restored.Slots[2].TowerId);
        Assert.Equal(["momentum", "overkill", "focus_lens"], restored.Slots[2].ModifierIds);
    }

    [Fact]
    public void Pack_Unpack_FullBuild_AllTowers_AllMods_RoundTrips()
    {
        var towers = new[] { "rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower", "rapid_shooter", "marker_tower" };
        var mods   = new[] { "momentum", "overkill", "exploit_weakness" };

        var slots = new RunSlotBuild[Balance.SlotCount];
        for (int i = 0; i < Balance.SlotCount; i++)
            slots[i] = new RunSlotBuild(towers[i], mods);

        var original = new RunBuildSnapshot(slots);
        var restored = BuildSnapshotCodec.Unpack(BuildSnapshotCodec.Pack(original));

        for (int i = 0; i < Balance.SlotCount; i++)
        {
            Assert.Equal(towers[i], restored.Slots[i].TowerId);
            Assert.Equal(mods, restored.Slots[i].ModifierIds);
        }
    }

    [Fact]
    public void Unpack_AllZeros_ReturnsEmptyBuild()
    {
        var zeros    = new int[Balance.SlotCount];
        var restored = BuildSnapshotCodec.Unpack(zeros);

        foreach (var slot in restored.Slots)
        {
            Assert.Equal("", slot.TowerId);
            Assert.Empty(slot.ModifierIds);
        }
    }

    [Fact]
    public void Pack_Unpack_UnknownModIgnored_OnlyKnownModsPreserved()
    {
        // Build a slot with 1 valid + 1 unknown mod.
        // The unknown mod will pack as 0 and be silently dropped on unpack.
        var slots = MakeEmptySlots();
        slots[0] = new RunSlotBuild("rapid_shooter", ["momentum", "NOT_A_REAL_MOD"]);
        var original = new RunBuildSnapshot(slots);

        var restored = BuildSnapshotCodec.Unpack(BuildSnapshotCodec.Pack(original));

        Assert.Equal("rapid_shooter", restored.Slots[0].TowerId);
        Assert.Equal(["momentum"], restored.Slots[0].ModifierIds);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RunSlotBuild[] MakeEmptySlots()
    {
        var slots = new RunSlotBuild[Balance.SlotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new RunSlotBuild("", []);
        return slots;
    }
}
