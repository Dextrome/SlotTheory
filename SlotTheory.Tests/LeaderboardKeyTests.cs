using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using Xunit;

namespace SlotTheory.Tests;

public class LeaderboardKeyTests
{
    // ── DifficultyToken ────────────────────────────────────────────────────────

    [Fact]
    public void DifficultyToken_Normal_ReturnsNormal()
        => Assert.Equal("normal", LeaderboardKey.DifficultyToken(DifficultyMode.Normal));

    [Fact]
    public void DifficultyToken_Hard_ReturnsHard()
        => Assert.Equal("hard", LeaderboardKey.DifficultyToken(DifficultyMode.Hard));

    // ── ToBucketId ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("arena_classic", DifficultyMode.Normal, "arena_classic_normal")]
    [InlineData("arena_classic", DifficultyMode.Hard,   "arena_classic_hard")]
    [InlineData("forest_run",    DifficultyMode.Normal, "forest_run_normal")]
    public void ToBucketId_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToBucketId(mapId, diff));

    // ── ToSectionKey ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("arena_classic", DifficultyMode.Normal, "bucket.arena_classic.normal")]
    [InlineData("arena_classic", DifficultyMode.Hard,   "bucket.arena_classic.hard")]
    public void ToSectionKey_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToSectionKey(mapId, diff));

    // ── ToSteamLeaderboardName ─────────────────────────────────────────────────

    [Theory]
    [InlineData("arena_classic", DifficultyMode.Normal, "global_arena_classic_normal")]
    [InlineData("arena_classic", DifficultyMode.Hard,   "global_arena_classic_hard")]
    public void ToSteamLeaderboardName_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToSteamLeaderboardName(mapId, diff));

    // ── IsGlobalEligibleMap ────────────────────────────────────────────────────

    [Fact]
    public void IsGlobalEligibleMap_AnyMap_ReturnsTrue()
        => Assert.True(LeaderboardKey.IsGlobalEligibleMap("arena_classic"));

    // ── Key uniqueness ─────────────────────────────────────────────────────────

    [Fact]
    public void BucketId_NormalAndHard_AreDistinct()
    {
        string normal = LeaderboardKey.ToBucketId("arena_classic", DifficultyMode.Normal);
        string hard   = LeaderboardKey.ToBucketId("arena_classic", DifficultyMode.Hard);
        Assert.NotEqual(normal, hard);
    }

    [Fact]
    public void SectionKey_NormalAndHard_AreDistinct()
    {
        string normal = LeaderboardKey.ToSectionKey("arena_classic", DifficultyMode.Normal);
        string hard   = LeaderboardKey.ToSectionKey("arena_classic", DifficultyMode.Hard);
        Assert.NotEqual(normal, hard);
    }

    [Fact]
    public void SteamName_DifferentMaps_AreDistinct()
    {
        string a = LeaderboardKey.ToSteamLeaderboardName("arena_classic", DifficultyMode.Normal);
        string b = LeaderboardKey.ToSteamLeaderboardName("forest_run",    DifficultyMode.Normal);
        Assert.NotEqual(a, b);
    }

    // ── Anti-brick: DraftSystem skips modifiers when no towers can accept them ──
    // TODO: DraftSystem.GenerateOptions() depends on DataLoader (Godot resource loading)
    // and TowerInstance (Godot Node2D), making it untestable in the current pure-C# harness.
    // When DataLoader is extracted behind an IDataSource interface, add these tests:
    //   - Given RunState with 0 towers placed → GenerateOptions offers 0 modifier cards
    //   - Given RunState with all towers at modifier cap → GenerateOptions offers 0 modifier cards
}
