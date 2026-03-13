using System.Collections.Generic;
using SlotTheory.Core.Leaderboards;
using Xunit;

namespace SlotTheory.Tests;

public class LeaderboardRetryQueuePolicyTests
{
    private sealed class PendingItem
    {
        public string Id { get; }
        public string Reason { get; set; } = "";

        public PendingItem(string id)
        {
            Id = id;
        }
    }

    [Fact]
    public void ApplySubmissionResult_FailedHead_DoesNotBlockTailProcessing()
    {
        var queue = new List<PendingItem>
        {
            new("A"),
            new("B")
        };
        var submitOrder = new List<string>();

        int budget = LeaderboardRetryQueuePolicy.BeginFlushBudget(queue.Count);
        while (LeaderboardRetryQueuePolicy.HasFlushWork(queue.Count, budget))
        {
            budget--;
            var head = queue[0];
            submitOrder.Add(head.Id);

            GlobalSubmitResult result = head.Id == "A"
                ? GlobalSubmitResult.Failed("Fake", "head failed")
                : GlobalSubmitResult.Submitted("Fake", "tail submitted");

            LeaderboardRetryQueuePolicy.ApplySubmissionResult(
                queue,
                result,
                (item, message) => item.Reason = message);
        }

        Assert.Equal(new[] { "A", "B" }, submitOrder);
        Assert.Single(queue);
        Assert.Equal("A", queue[0].Id);
        Assert.Equal("head failed", queue[0].Reason);
    }

    [Fact]
    public void ApplySubmissionResult_Submitted_RemovesHead()
    {
        var queue = new List<PendingItem>
        {
            new("A"),
            new("B")
        };

        LeaderboardRetryQueuePolicy.ApplySubmissionResult(
            queue,
            GlobalSubmitResult.Submitted("Fake", "ok"));

        Assert.Single(queue);
        Assert.Equal("B", queue[0].Id);
    }

    [Fact]
    public void ShouldAttemptInitialization_RespectsAvailabilityInFlightAndBackoff()
    {
        Assert.False(LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
            serviceAvailable: true,
            hasInFlightInitTask: false,
            hasCompletedInitTask: false,
            nowUnixSeconds: 100,
            nextRetryAtUnixSeconds: 0));

        Assert.False(LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
            serviceAvailable: false,
            hasInFlightInitTask: true,
            hasCompletedInitTask: false,
            nowUnixSeconds: 100,
            nextRetryAtUnixSeconds: 0));

        Assert.False(LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
            serviceAvailable: false,
            hasInFlightInitTask: false,
            hasCompletedInitTask: true,
            nowUnixSeconds: 100,
            nextRetryAtUnixSeconds: 101));

        Assert.True(LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
            serviceAvailable: false,
            hasInFlightInitTask: false,
            hasCompletedInitTask: true,
            nowUnixSeconds: 101,
            nextRetryAtUnixSeconds: 101));

        Assert.True(LeaderboardRetryQueuePolicy.ShouldAttemptInitialization(
            serviceAvailable: false,
            hasInFlightInitTask: false,
            hasCompletedInitTask: false,
            nowUnixSeconds: 100,
            nextRetryAtUnixSeconds: 0));
    }
}
