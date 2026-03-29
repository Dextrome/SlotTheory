using System.Collections.Generic;
using SlotTheory.Core;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for DraftSystem's anti-brick rule when towers ARE placed - the case previously blocked
/// by TowerInstance extending Node2D. Unlocked via ITowerView + FakeTower stubs.
/// </summary>
public class DraftAntiBrickTests
{
    private sealed class StubData : IDraftDataSource
    {
        public IEnumerable<string> GetAllTowerIds()
            => ["rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower", "latch_nest"];

        public IEnumerable<string> GetAllModifierIds()
            => ["momentum", "overkill", "exploit_weakness", "focus_lens",
                "slow", "overreach", "hair_trigger", "split_shot",
                "feedback_loop", "chain_reaction"];
    }

    private static DraftSystem MakeDraft() => new(new StubData());

    // ── All towers at modifier cap ─────────────────────────────────────────

    [Fact]
    public void GenerateOptions_AllTowersAtModCap_ZeroModifierOptions()
    {
        // Core anti-brick rule: modifiers must never be offered when no tower can accept them.
        // Simulates the wave-N case where every placed tower is already at 3 modifiers.
        var towers = new[] { AtCap(), AtCap() };

        var options = MakeDraft().GenerateOptions(hasFreeSlots: true, placedTowers: towers);

        Assert.DoesNotContain(options, o => o.Type == DraftOptionType.Modifier);
    }

    [Fact]
    public void GenerateOptions_AllTowersAtModCap_FillsWithTowerCards()
    {
        var towers = new[] { AtCap() };

        var options = MakeDraft().GenerateOptions(hasFreeSlots: true, placedTowers: towers);

        Assert.Equal(Balance.DraftOptionsCount, options.Count);
        Assert.All(options, o => Assert.Equal(DraftOptionType.Tower, o.Type));
    }

    [Fact]
    public void GenerateOptions_AllSlotsFull_AllTowersAtModCap_ReturnsEmpty()
    {
        // No free slots + all towers at cap → anti-brick returns 0 modifier options.
        // The pad-with-towers path only fires when hasFreeSlots is true, so result is empty.
        var towers = new[] { AtCap(), AtCap() };

        var options = MakeDraft().GenerateOptions(hasFreeSlots: false, placedTowers: towers);

        Assert.Empty(options);
    }

    [Fact]
    public void GenerateOptions_OneTowerAtCapOneFree_OffersModifiers()
    {
        // Mix: one at cap, one with space. towersWithSpace.Count > 0 → modifiers offered.
        var towers = new[] { AtCap(), new FakeTower() };

        var options = MakeDraft().GenerateOptions(hasFreeSlots: true, placedTowers: towers);

        Assert.Contains(options, o => o.Type == DraftOptionType.Modifier);
    }

    [Fact]
    public void GenerateOptions_WithLatchNestPresent_AllTowersAtCap_StillOffersNoModifiers()
    {
        var towers = new[] { AtCap(), AtCap(), AtCap() };
        towers[2].TowerId = "latch_nest";

        var options = MakeDraft().GenerateOptions(hasFreeSlots: true, placedTowers: towers);

        Assert.DoesNotContain(options, o => o.Type == DraftOptionType.Modifier);
    }

    [Fact]
    public void GenerateOptions_AllSlotsFull_TowersHaveSpace_OffersModifiersOnly()
    {
        // All slots occupied but towers still have mod space →
        // DraftModifierOptionsFull modifier cards, no tower cards.
        var towers = new[] { new FakeTower(), new FakeTower() };

        var options = MakeDraft().GenerateOptions(hasFreeSlots: false, placedTowers: towers);

        Assert.Equal(Balance.DraftModifierOptionsFull, options.Count);
        Assert.All(options, o => Assert.Equal(DraftOptionType.Modifier, o.Type));
    }

    private static FakeTower AtCap()
    {
        var t = new FakeTower();
        t.CanAddModifier = false;
        return t;
    }
}
