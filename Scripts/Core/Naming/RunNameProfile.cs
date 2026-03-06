namespace SlotTheory.Core.Naming;

public enum RunPaceBucket
{
    Blazing,
    Fast,
    Steady,
    Deliberate,
    Marathon
}

public sealed record RunNameProfile(
    string MapId,
    DifficultyMode Difficulty,
    bool Won,
    int WaveReached,
    int LivesRemaining,
    string PrimaryFamily,
    string SecondaryFamily,
    int PrimaryFamilyCount,
    int SecondaryFamilyCount,
    string MvpTowerId,
    string SupportTowerId,
    int UniqueTowerTypes,
    int TotalTowersPlaced,
    int TotalKills,
    int TotalDamage,
    RunPaceBucket Pace
);

