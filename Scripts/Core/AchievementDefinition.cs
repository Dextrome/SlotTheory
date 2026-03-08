namespace SlotTheory.Core;

/// <summary>
/// Immutable data record describing a single achievement.
/// </summary>
public record AchievementDefinition(
    string Id,
    string Name,
    string Desc
);
