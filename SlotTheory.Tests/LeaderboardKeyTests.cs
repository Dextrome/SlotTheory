using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using Xunit;

namespace SlotTheory.Tests;

public class LeaderboardKeyTests
{
    // ── DifficultyToken ────────────────────────────────────────────────────────

    [Fact]
    public void DifficultyToken_Easy_ReturnsEasy()
        => Assert.Equal("easy", LeaderboardKey.DifficultyToken(DifficultyMode.Easy));

    [Fact]
    public void DifficultyToken_Normal_ReturnsNormal()
        => Assert.Equal("normal", LeaderboardKey.DifficultyToken(DifficultyMode.Normal));

    [Fact]
    public void DifficultyToken_Hard_ReturnsHard()
        => Assert.Equal("hard", LeaderboardKey.DifficultyToken(DifficultyMode.Hard));

    // ── ToBucketId ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("crossroads", DifficultyMode.Easy,   "crossroads_easy")]
    [InlineData("crossroads", DifficultyMode.Normal, "crossroads_normal")]
    [InlineData("crossroads", DifficultyMode.Hard,   "crossroads_hard")]
    [InlineData("forest_run",    DifficultyMode.Easy,   "forest_run_easy")]
    [InlineData("forest_run",    DifficultyMode.Normal, "forest_run_normal")]
    public void ToBucketId_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToBucketId(mapId, diff));

    // ── ToSectionKey ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("crossroads", DifficultyMode.Easy,   "bucket.crossroads.easy")]
    [InlineData("crossroads", DifficultyMode.Normal, "bucket.crossroads.normal")]
    [InlineData("crossroads", DifficultyMode.Hard,   "bucket.crossroads.hard")]
    public void ToSectionKey_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToSectionKey(mapId, diff));

    // ── ToSteamLeaderboardName ─────────────────────────────────────────────────

    [Theory]
    [InlineData("crossroads", DifficultyMode.Easy,   "global_crossroads_easy")]
    [InlineData("crossroads", DifficultyMode.Normal, "global_crossroads_normal")]
    [InlineData("crossroads", DifficultyMode.Hard,   "global_crossroads_hard")]
    public void ToSteamLeaderboardName_FormatsCorrectly(string mapId, DifficultyMode diff, string expected)
        => Assert.Equal(expected, LeaderboardKey.ToSteamLeaderboardName(mapId, diff));

    // ── IsGlobalEligibleMap ────────────────────────────────────────────────────

    [Fact]
    public void IsGlobalEligibleMap_RegularMap_ReturnsTrue()
        => Assert.True(LeaderboardKey.IsGlobalEligibleMap("crossroads"));

    [Fact]
    public void IsGlobalEligibleMap_Tutorial_ReturnsFalse()
        => Assert.False(LeaderboardKey.IsGlobalEligibleMap(LeaderboardKey.TutorialMapId));

    // ── Key uniqueness ─────────────────────────────────────────────────────────

    [Fact]
    public void BucketId_EasyNormalHard_AreDistinct()
    {
        string easy = LeaderboardKey.ToBucketId("crossroads", DifficultyMode.Easy);
        string normal = LeaderboardKey.ToBucketId("crossroads", DifficultyMode.Normal);
        string hard = LeaderboardKey.ToBucketId("crossroads", DifficultyMode.Hard);
        Assert.NotEqual(easy, normal);
        Assert.NotEqual(normal, hard);
        Assert.NotEqual(easy, hard);
    }

    [Fact]
    public void SectionKey_EasyNormalHard_AreDistinct()
    {
        string easy = LeaderboardKey.ToSectionKey("crossroads", DifficultyMode.Easy);
        string normal = LeaderboardKey.ToSectionKey("crossroads", DifficultyMode.Normal);
        string hard = LeaderboardKey.ToSectionKey("crossroads", DifficultyMode.Hard);
        Assert.NotEqual(easy, normal);
        Assert.NotEqual(normal, hard);
        Assert.NotEqual(easy, hard);
    }

    [Fact]
    public void SteamName_DifferentMaps_AreDistinct()
    {
        string a = LeaderboardKey.ToSteamLeaderboardName("crossroads", DifficultyMode.Normal);
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
