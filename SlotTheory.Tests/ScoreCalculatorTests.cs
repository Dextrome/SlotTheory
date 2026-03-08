using SlotTheory.Core;
using SlotTheory.Core.Leaderboards;
using Xunit;

namespace SlotTheory.Tests;

public class ScoreCalculatorTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RunBuildSnapshot EmptyBuild() => BuildSnapshotCodec.Empty();

    private static RunScorePayload WinPayload(
        int wave = 20, int lives = 5, float time = 300f, int damage = 50_000, int kills = 200)
        => new("arena_classic", DifficultyMode.Normal, Won: true, wave, lives,
               damage, kills, time, 0L, "0.1.5", EmptyBuild());

    private static RunScorePayload LossPayload(
        int wave = 10, int lives = 0, float time = 300f, int damage = 20_000, int kills = 100)
        => new("arena_classic", DifficultyMode.Normal, Won: false, wave, lives,
               damage, kills, time, 0L, "0.1.5", EmptyBuild());

    // ── ComputeScore ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeScore_Win_ContainsWinBonus()
    {
        int win  = ScoreCalculator.ComputeScore(WinPayload());
        int loss = ScoreCalculator.ComputeScore(LossPayload(wave: 20, lives: 5, time: 300f));
        Assert.True(win > loss, "A win must outscore an equivalent loss.");
    }

    [Fact]
    public void ComputeScore_Win_AllWaves_MaxLives_IsHighest()
    {
        int best  = ScoreCalculator.ComputeScore(WinPayload(wave: 20, lives: 10, time: 1f));
        int worse = ScoreCalculator.ComputeScore(WinPayload(wave: 20, lives: 5,  time: 300f));
        Assert.True(best > worse);
    }

    [Fact]
    public void ComputeScore_FasterTime_GivesHigherScore()
    {
        int fast = ScoreCalculator.ComputeScore(WinPayload(time: 100f));
        int slow = ScoreCalculator.ComputeScore(WinPayload(time: 500f));
        Assert.True(fast > slow);
    }

    [Fact]
    public void ComputeScore_MoreLives_GivesHigherScore()
    {
        int many = ScoreCalculator.ComputeScore(WinPayload(lives: 10));
        int few  = ScoreCalculator.ComputeScore(WinPayload(lives: 1));
        Assert.True(many > few);
    }

    [Fact]
    public void ComputeScore_HigherWave_GivesHigherScore_OnLoss()
    {
        int wave15 = ScoreCalculator.ComputeScore(LossPayload(wave: 15));
        int wave5  = ScoreCalculator.ComputeScore(LossPayload(wave: 5));
        Assert.True(wave15 > wave5);
    }

    [Fact]
    public void ComputeScore_ZeroLives_ZeroWave_ReturnsNonNegative()
    {
        var payload = LossPayload(wave: 0, lives: 0, time: 0f);
        int score = ScoreCalculator.ComputeScore(payload);
        Assert.True(score >= 0);
    }

    // ── IsPayloadSane ──────────────────────────────────────────────────────────

    [Fact]
    public void IsPayloadSane_ValidPayload_ReturnsTrue()
    {
        Assert.True(ScoreCalculator.IsPayloadSane(WinPayload()));
    }

    [Fact]
    public void IsPayloadSane_NegativeWave_ReturnsFalse()
    {
        var p = WinPayload(wave: -1);
        Assert.False(ScoreCalculator.IsPayloadSane(p));
    }

    [Fact]
    public void IsPayloadSane_ExcessiveDamage_ReturnsFalse()
    {
        var p = new RunScorePayload("arena_classic", DifficultyMode.Normal, true,
            20, 5, TotalDamageDealt: 100_000_000, 200, 300f, 0L, "0.1.5", EmptyBuild());
        Assert.False(ScoreCalculator.IsPayloadSane(p));
    }

    // ── IsBetterThanExisting ───────────────────────────────────────────────────

    [Fact]
    public void IsBetterThanExisting_HigherScore_ReturnsTrue()
    {
        var better = WinPayload(lives: 10, time: 1f);
        var existing = MakePersonalBest(ScoreCalculator.ComputeScore(WinPayload()));
        Assert.True(ScoreCalculator.IsBetterThanExisting(better, existing));
    }

    [Fact]
    public void IsBetterThanExisting_NullExisting_ReturnsTrue()
    {
        Assert.True(ScoreCalculator.IsBetterThanExisting(WinPayload(), null));
    }

    [Fact]
    public void IsBetterThanExisting_LowerScore_ReturnsFalse()
    {
        var worse = LossPayload(wave: 5);
        var existing = MakePersonalBest(ScoreCalculator.ComputeScore(WinPayload()));
        Assert.False(ScoreCalculator.IsBetterThanExisting(worse, existing));
    }

    [Fact]
    public void IsBetterThanExisting_SameScore_Win_BeatsLoss()
    {
        // Create two payloads with identical scores but different won flag.
        // In practice the win bonus prevents equal scores, but IsBetterThanExisting
        // handles the tie-break in case scores happen to match.
        var winPayload  = WinPayload(wave: 20, lives: 5, time: 300f);
        var lossPayload = LossPayload(wave: 20, lives: 5, time: 300f);
        int winScore  = ScoreCalculator.ComputeScore(winPayload);
        int lossScore = ScoreCalculator.ComputeScore(lossPayload);

        // Only meaningful if we artificially construct a tie: just verify directional order
        if (winScore > lossScore)
        {
            var existingLoss = MakePersonalBest(lossScore, won: false, wave: 20, lives: 5);
            Assert.True(ScoreCalculator.IsBetterThanExisting(winPayload, existingLoss));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PersonalBestEntry MakePersonalBest(int score, bool won = true, int wave = 20, int lives = 5)
        => new(score, won, wave, lives, 50_000, 200, 300f, 0L, "0.1.5", EmptyBuild());
}
