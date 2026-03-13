using System;
using System.Collections.Generic;

namespace SlotTheory.Core.Leaderboards;

public static class LeaderboardRetryQueuePolicy
{
    public static bool ShouldAttemptInitialization(
        bool serviceAvailable,
        bool hasInFlightInitTask,
        bool hasCompletedInitTask,
        double nowUnixSeconds,
        double nextRetryAtUnixSeconds)
    {
        if (serviceAvailable)
            return false;
        if (hasInFlightInitTask)
            return false;
        if (hasCompletedInitTask && nowUnixSeconds < nextRetryAtUnixSeconds)
            return false;
        return true;
    }

    public static int BeginFlushBudget(int queueCount)
        => Math.Max(0, queueCount);

    public static bool HasFlushWork(int queueCount, int remainingBudget)
        => queueCount > 0 && remainingBudget > 0;

    public static void ApplySubmissionResult<T>(
        List<T> queue,
        GlobalSubmitResult result,
        Action<T, string>? updateReason = null)
    {
        if (queue.Count == 0)
            return;

        if (result.State == GlobalSubmitState.Submitted || result.State == GlobalSubmitState.Skipped)
        {
            queue.RemoveAt(0);
            return;
        }

        var head = queue[0];
        queue.RemoveAt(0);
        updateReason?.Invoke(head, result.Message);
        queue.Add(head);
    }
}
