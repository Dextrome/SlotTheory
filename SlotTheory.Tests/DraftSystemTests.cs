using System.Collections.Generic;
using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for DraftSystem using a stub IDraftDataSource — no Godot runtime needed.
///
/// TESTABLE without Godot (Tower == null path):
///   - Anti-brick: no modifier options when no towers are placed (towersWithSpace is empty).
///   - Total option count fills to DraftOptionsCount via the pad-with-towers path.
///
/// REQUIRES Godot (cannot test here):
///   - Anti-brick when towers ARE placed but all are at modifier cap.
///     HasFreeSlots() checks slot.Tower != null; TowerInstance is a Godot Node2D and
///     cannot be instantiated without the engine. Fix: extract an ITowerView with
///     CanAddModifier from TowerInstance and inject into SlotInstance.
///   - All-slots-full modifier-only path (same blocker).
///
/// NOTE on wave-1 duplicates: With no towers placed, AddModifierOptions returns 0 and
/// the pad logic calls AddTowerOptions twice. Each call shuffles the pool independently,
/// so with pool size 4 and total needed 5, duplicate tower IDs can appear. This is a
/// known edge-case in the current draft system; tests below document that behaviour.
/// </summary>
public class DraftSystemTests
{
    // ── Stub ──────────────────────────────────────────────────────────────────

    private sealed class StubData : IDraftDataSource
    {
        public IEnumerable<string> GetAllTowerIds()
            => ["rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower"];

        public IEnumerable<string> GetAllModifierIds()
            => ["momentum", "overkill", "exploit_weakness", "focus_lens",
                "slow", "overreach", "hair_trigger", "split_shot",
                "feedback_loop", "chain_reaction"];
    }

    private static DraftSystem MakeDraft() => new(new StubData());

    // ── Anti-brick: no towers placed ──────────────────────────────────────────

    [Fact]
    public void GenerateOptions_NoTowersPlaced_ZeroModifierOptions()
    {
        // Core anti-brick rule: modifiers must never be offered when no tower can accept them.
        // With an empty RunState, slot.Tower == null for all slots → towersWithSpace is empty
        // → AddModifierOptions returns immediately with 0 entries.
        var options = MakeDraft().GenerateOptions(new RunState());

        Assert.DoesNotContain(options, o => o.Type == DraftOptionType.Modifier);
    }

    [Fact]
    public void GenerateOptions_NoTowersPlaced_AllOptionsAreTowerCards()
    {
        var options = MakeDraft().GenerateOptions(new RunState());

        Assert.NotEmpty(options);
        Assert.All(options, o => Assert.Equal(DraftOptionType.Tower, o.Type));
    }

    // ── Option counts ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateOptions_NoTowersPlaced_TotalCountEqualsDraftOptionsCount()
    {
        // AddModifierOptions returns 0 (no towers with space), so the pad-with-towers
        // path runs and fills the list up to DraftOptionsCount.
        var options = MakeDraft().GenerateOptions(new RunState());

        Assert.Equal(Balance.DraftOptionsCount, options.Count);
    }

    [Fact]
    public void GenerateOptions_NoTowersPlaced_AllOfferedIdsAreInPool()
    {
        var knownTowers = new HashSet<string> { "rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower" };
        var options = MakeDraft().GenerateOptions(new RunState());

        Assert.All(options, o => Assert.Contains(o.Id, knownTowers));
    }

    // ── Repeated calls are independent ────────────────────────────────────────

    [Fact]
    public void GenerateOptions_CalledTwice_BothResultsAreValid()
    {
        // Each call is independent; both must satisfy the anti-brick rule.
        var draft = MakeDraft();
        var state = new RunState();

        var first  = draft.GenerateOptions(state);
        var second = draft.GenerateOptions(state);

        Assert.DoesNotContain(first,  o => o.Type == DraftOptionType.Modifier);
        Assert.DoesNotContain(second, o => o.Type == DraftOptionType.Modifier);
    }
}
