namespace SlotTheory.Core.Leaderboards;

public static class ScoreCalculator
{
    public static int ComputeScore(RunScorePayload payload)
    {
        long winBonus  = payload.Won ? 1_000_000_000L : 0L;
        long wavePart  = System.Math.Clamp(payload.WaveReached, 0, 99) * 10_000_000L;
        long livesPart = System.Math.Clamp(payload.LivesRemaining, 0, 99) * 100_000L;
        long timePart  = System.Math.Clamp(9_999 - (int)System.Math.Floor(payload.PlayTimeSeconds), 0, 9_999);

        long total = winBonus + wavePart + livesPart + timePart;
        return (int)System.Math.Clamp(total, int.MinValue, int.MaxValue);
    }

    public static bool IsPayloadSane(RunScorePayload payload)
    {
        if (payload.WaveReached < 0 || payload.WaveReached > Balance.TotalWaves) return false;
        if (payload.LivesRemaining < 0 || payload.LivesRemaining > 999) return false;
        if (payload.TotalDamageDealt < 0 || payload.TotalDamageDealt > 50_000_000) return false;
        if (payload.TotalKills < 0 || payload.TotalKills > 200_000) return false;
        if (payload.PlayTimeSeconds < 0f || payload.PlayTimeSeconds > 60f * 60f * 6f) return false;
        return true;
    }

    public static bool IsBetterThanExisting(RunScorePayload candidate, PersonalBestEntry? existing)
    {
        if (existing == null) return true;

        int candidateScore = ComputeScore(candidate);
        if (candidateScore > existing.Score) return true;
        if (candidateScore < existing.Score) return false;

        if (candidate.Won != existing.Won) return candidate.Won;
        if (candidate.WaveReached != existing.WaveReached) return candidate.WaveReached > existing.WaveReached;
        if (candidate.LivesRemaining != existing.LivesRemaining) return candidate.LivesRemaining > existing.LivesRemaining;
        if (candidate.PlayTimeSeconds != existing.PlayTimeSeconds) return candidate.PlayTimeSeconds < existing.PlayTimeSeconds;

        return false;
    }
}
