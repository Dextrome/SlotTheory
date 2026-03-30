using System.Collections.Generic;
using SlotTheory.Core;
using SlotTheory.Tools;
using Xunit;

namespace SlotTheory.Tests;

public class BotPlayerStrategyTests
{
    [Fact]
    public void SpectacleSingleStack_EarlySurvivalGate_PrioritizesSecondTowerBeforeStackingMods()
    {
        var bot = new BotPlayer(BotStrategy.SpectacleSingleStack, seed: 7);
        var state = NewState(mapId: "orbit", waveIndex: 1, lives: 10);

        state.Slots[0].Tower = new FakeTower
        {
            TowerId = "rapid_shooter",
            CanAddModifier = true
        };

        var options = new List<DraftOption>
        {
            Tower("chain_tower"),
            Mod("overkill"),
            Mod("chain_reaction")
        };

        DraftAndAssert(
            expectedType: DraftOptionType.Tower,
            expectedId: "chain_tower",
            pick: bot.Pick(options, state));
    }

    [Fact]
    public void RiftPrismFocus_OnPressureMapWithLowLives_DelaysFirstRiftUntilStableCoreExists()
    {
        var bot = new BotPlayer(BotStrategy.RiftPrismFocus, seed: 11);
        var state = NewState(mapId: "pinch_bleed", waveIndex: 2, lives: 4);

        state.Slots[0].Tower = new FakeTower
        {
            TowerId = "rapid_shooter",
            CanAddModifier = false
        };

        var options = new List<DraftOption>
        {
            Tower("rift_prism"),
            Tower("chain_tower")
        };

        DraftAndAssert(
            expectedType: DraftOptionType.Tower,
            expectedId: "chain_tower",
            pick: bot.Pick(options, state));
    }

    [Fact]
    public void GreedyDps_DeadzoneOffered_PrefersAnchorTowerOverNonAnchor()
    {
        // Deadzone is most effective on chokepoint/control towers.
        // GreedyDps should prefer placing it on undertow_engine over rapid_shooter.
        var bot = new BotPlayer(BotStrategy.GreedyDps, seed: 1);
        var state = NewState(mapId: "orbit", waveIndex: 5, lives: 10);

        // Two towers: undertow_engine (anchor) and rapid_shooter (non-anchor).
        state.Slots[0].Tower = new FakeTower { TowerId = "undertow_engine", CanAddModifier = true };
        state.Slots[1].Tower = new FakeTower { TowerId = "rapid_shooter",   CanAddModifier = true };

        var options = new List<DraftOption> { Mod("deadzone") };

        var pick = bot.Pick(options, state);

        Assert.NotNull(pick);
        Assert.Equal(DraftOptionType.Modifier, pick!.Option.Type);
        Assert.Equal("deadzone", pick.Option.Id);
        // Deadzone should land on the anchor tower (slot 0 = undertow_engine).
        Assert.Equal(0, pick.SlotIndex);
    }

    private static RunState NewState(string mapId, int waveIndex, int lives)
    {
        var state = new RunState
        {
            SelectedMapId = mapId,
            WaveIndex = waveIndex,
            Lives = lives,
        };

        return state;
    }

    private static DraftOption Tower(string id) => new(DraftOptionType.Tower, id);
    private static DraftOption Mod(string id) => new(DraftOptionType.Modifier, id);

    private static void DraftAndAssert(DraftOptionType expectedType, string expectedId, BotPlayer.DraftPick? pick)
    {
        Assert.NotNull(pick);
        Assert.Equal(expectedType, pick!.Option.Type);
        Assert.Equal(expectedId, pick.Option.Id);
    }
}
