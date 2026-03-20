using System;

namespace SlotTheory.Core;

/// <summary>
/// Centralized UX timing floors for surge hints/callouts.
/// Kept pure so behavior can be unit-tested without scene setup.
/// </summary>
public static class SurgeUxTiming
{
    public const float WorldTeachingHintMinHoldSeconds = 5.25f;
    public const float WorldTeachingHintFadeOutSeconds = 0.45f;
    public const float SurgeMeterHintMinHoldSeconds = 2.1f;
    public const float SurgeMeterHintFadeOutSeconds = 0.3f;

    public const float SurgeCalloutMinDurationScale = 3.6f;
    public const float SurgeCalloutMinHoldPortion = 0.82f;
    public const float TriadAugmentMinRemainingSeconds = 4.1f;

    public static float ResolveWorldTeachingHintHold(float requestedSeconds)
        => MathF.Max(WorldTeachingHintMinHoldSeconds, requestedSeconds);

    public static float ResolveSurgeMeterHintHold(float requestedSeconds)
        => MathF.Max(SurgeMeterHintMinHoldSeconds, requestedSeconds);

    public static float ResolveSurgeCalloutDurationScale(float requestedScale)
        => MathF.Max(SurgeCalloutMinDurationScale, requestedScale);

    public static float ResolveSurgeCalloutHoldPortion(float requestedHoldPortion)
        => Clamp(MathF.Max(SurgeCalloutMinHoldPortion, requestedHoldPortion), 0f, 0.95f);

    public static float ResolveTriadAugmentRemaining(float requestedSeconds)
        => MathF.Max(TriadAugmentMinRemainingSeconds, requestedSeconds);

    private static float Clamp(float value, float min, float max)
        => MathF.Min(max, MathF.Max(min, value));
}
