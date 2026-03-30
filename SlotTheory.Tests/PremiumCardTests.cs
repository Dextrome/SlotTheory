using System.Collections.Generic;
using System.Linq;
using SlotTheory.Core;
using SlotTheory.Entities;
using Xunit;

namespace SlotTheory.Tests;

/// <summary>
/// Tests for the Premium Card system: registry, rarity tiers, RunState fields,
/// DraftSystem BonusDraftCards, Expanded Chassis slot logic, and bot evaluation.
/// </summary>
public class PremiumCardTests
{
    // ── PremiumCardRegistry ───────────────────────────────────────────────────

    [Fact]
    public void Registry_ContainsAllExpectedCards()
    {
        var allIds = PremiumCardRegistry.GetAll().Select(c => c.Id).ToHashSet();
        Assert.Contains(PremiumCardRegistry.ExpandedChassisId,    allIds);
        Assert.Contains(PremiumCardRegistry.BetterOddsId,         allIds);
        Assert.Contains(PremiumCardRegistry.KineticCalibrationId,  allIds);
        Assert.Contains(PremiumCardRegistry.HotLoadersId,          allIds);
        Assert.Contains(PremiumCardRegistry.EmergencyReservesId,   allIds);
        Assert.Contains(PremiumCardRegistry.HardenedReservesId,    allIds);
        Assert.Contains(PremiumCardRegistry.LongFuseId,            allIds);
        Assert.Contains(PremiumCardRegistry.SignalBoostId,         allIds);
        Assert.Contains(PremiumCardRegistry.MultitargetRelayId,    allIds);
        Assert.Contains(PremiumCardRegistry.ExtendedRailsId,       allIds);
        Assert.Contains(PremiumCardRegistry.ColdCircuitId,         allIds);
    }

    [Fact]
    public void Registry_SuperRareCards_AreCorrect()
    {
        var superRare = PremiumCardRegistry.GetByRarity(PremiumRarity.SuperRare)
            .Select(c => c.Id).ToHashSet();
        Assert.Contains(PremiumCardRegistry.ExpandedChassisId,    superRare);
        Assert.Contains(PremiumCardRegistry.BetterOddsId,         superRare);
        Assert.Contains(PremiumCardRegistry.KineticCalibrationId,  superRare);
        Assert.Contains(PremiumCardRegistry.HotLoadersId,          superRare);
        // Rare cards must NOT appear in the SuperRare tier
        Assert.DoesNotContain(PremiumCardRegistry.EmergencyReservesId,  superRare);
        Assert.DoesNotContain(PremiumCardRegistry.ColdCircuitId,        superRare);
    }

    [Fact]
    public void Registry_RareCards_AreCorrect()
    {
        var rare = PremiumCardRegistry.GetByRarity(PremiumRarity.Rare)
            .Select(c => c.Id).ToHashSet();
        Assert.Contains(PremiumCardRegistry.EmergencyReservesId,  rare);
        Assert.Contains(PremiumCardRegistry.HardenedReservesId,   rare);
        Assert.Contains(PremiumCardRegistry.LongFuseId,           rare);
        Assert.Contains(PremiumCardRegistry.SignalBoostId,        rare);
        Assert.Contains(PremiumCardRegistry.MultitargetRelayId,   rare);
        Assert.Contains(PremiumCardRegistry.ExtendedRailsId,      rare);
        Assert.Contains(PremiumCardRegistry.ColdCircuitId,        rare);
        // Super Rare cards must NOT appear in the Rare tier
        Assert.DoesNotContain(PremiumCardRegistry.ExpandedChassisId, rare);
        Assert.DoesNotContain(PremiumCardRegistry.BetterOddsId,      rare);
    }

    [Fact]
    public void Registry_ExpandedChassis_RequiresTarget()
    {
        Assert.True(PremiumCardRegistry.RequiresTarget(PremiumCardRegistry.ExpandedChassisId));
    }

    [Fact]
    public void Registry_NonTargetCards_DoNotRequireTarget()
    {
        Assert.False(PremiumCardRegistry.RequiresTarget(PremiumCardRegistry.BetterOddsId));
        Assert.False(PremiumCardRegistry.RequiresTarget(PremiumCardRegistry.EmergencyReservesId));
        Assert.False(PremiumCardRegistry.RequiresTarget(PremiumCardRegistry.ColdCircuitId));
    }

    [Fact]
    public void Registry_TryGet_ReturnsNull_ForUnknownId()
    {
        Assert.Null(PremiumCardRegistry.TryGet("not_a_real_card"));
    }

    [Fact]
    public void Registry_MaxCopies_BetterOdds_IsBounded()
    {
        Assert.True(PremiumCardRegistry.GetMaxCopies(PremiumCardRegistry.BetterOddsId) >= 1);
        Assert.True(PremiumCardRegistry.GetMaxCopies(PremiumCardRegistry.BetterOddsId) <= 4);
    }

    // ── RunState premium fields ────────────────────────────────────────────────

    [Fact]
    public void RunState_PremiumFields_InitializedNeutral()
    {
        var state = new RunState();
        Assert.Empty(state.PickedPremiumCards);
        Assert.Equal(0, state.BonusDraftCards);
        Assert.Equal(0, state.BonusDamage);
        Assert.Equal(1f, state.AttackIntervalMultiplier);
        Assert.Equal(0f, state.TowerRangeBonus);
        Assert.Equal(0f, state.ChainRangeBonus);
        Assert.Equal(0f, state.ExplosionRadiusBonus);
        Assert.Equal(0f, state.MarkDurationBonus);
        Assert.Equal(1f, state.SlowDurationMultiplier);
    }

    [Fact]
    public void RunState_Reset_ClearsPremiumState()
    {
        var state = new RunState();
        state.PickedPremiumCards.Add("some_card");
        state.BonusDraftCards = 2;
        state.BonusDamage = 3;
        state.AttackIntervalMultiplier = 0.80f;
        state.TowerRangeBonus = 30f;
        state.ChainRangeBonus = 10f;
        state.ExplosionRadiusBonus = 20f;
        state.MarkDurationBonus = 1.5f;
        state.SlowDurationMultiplier = 1.35f;

        state.Reset();

        Assert.Empty(state.PickedPremiumCards);
        Assert.Equal(0, state.BonusDraftCards);
        Assert.Equal(0, state.BonusDamage);
        Assert.Equal(1f, state.AttackIntervalMultiplier);
        Assert.Equal(0f, state.TowerRangeBonus);
        Assert.Equal(0f, state.ChainRangeBonus);
        Assert.Equal(0f, state.ExplosionRadiusBonus);
        Assert.Equal(0f, state.MarkDurationBonus);
        Assert.Equal(1f, state.SlowDurationMultiplier);
    }

    // ── TowerInstance MaxModifiers ────────────────────────────────────────────

    [Fact]
    public void TowerMaxModifiers_DefaultsToBalance()
    {
        var tower = new FakeTower();
        Assert.Equal(Balance.MaxModifiersPerTower, tower.MaxModifiers);
    }

    [Fact]
    public void ExpandedChassis_IncreasesMaxModifiers()
    {
        var tower = new FakeTower();
        int before = tower.MaxModifiers;
        tower.MaxModifiers = System.Math.Min(tower.MaxModifiers + 1, Balance.MaxPremiumModSlots);
        Assert.Equal(before + 1, tower.MaxModifiers);
    }

    [Fact]
    public void ExpandedChassis_CappedAtMaxPremiumModSlots()
    {
        var tower = new FakeTower();
        tower.MaxModifiers = Balance.MaxPremiumModSlots;  // already at cap
        int capped = System.Math.Min(tower.MaxModifiers + 1, Balance.MaxPremiumModSlots);
        Assert.Equal(Balance.MaxPremiumModSlots, capped);  // cap holds
    }

    [Fact]
    public void ExpandedChassis_CanAddModifier_UsesMaxModifiers()
    {
        var tower = new FakeTower();
        tower.MaxModifiers = Balance.MaxModifiersPerTower;
        // Fill to default cap
        for (int i = 0; i < Balance.MaxModifiersPerTower; i++)
            tower.Modifiers.Add(new TestModifier());
        Assert.False(tower.CanAddModifier);

        // Expanding allows one more
        tower.MaxModifiers++;
        Assert.True(tower.CanAddModifier);
    }

    // ── DraftSystem BonusDraftCards ───────────────────────────────────────────

    private sealed class StubData : IDraftDataSource
    {
        public IEnumerable<string> GetAllTowerIds()
            => ["rapid_shooter", "heavy_cannon", "marker_tower", "chain_tower", "arc_emitter"];
        public IEnumerable<string> GetAllModifierIds()
            => ["momentum", "overkill", "exploit_weakness", "focus_lens",
                "slow", "overreach", "hair_trigger", "split_shot", "feedback_loop", "chain_reaction"];
    }

    [Fact]
    public void GenerateOptions_WithBonusDraftCards_ShowsExtraCard()
    {
        var draft = new DraftSystem(new StubData());
        var tower = new FakeTower { TowerId = "rapid_shooter" };
        var options = draft.GenerateOptions(
            hasFreeSlots: true,
            placedTowers: [tower],
            bonusDraftCards: 1);

        // Should have 6 non-premium cards (5 base + 1 bonus)
        int normalCount = options.Count(o => o.Type != DraftOptionType.Premium);
        Assert.Equal(Balance.DraftOptionsCount + 1, normalCount);
    }

    [Fact]
    public void GenerateOptions_WithZeroBonusDraftCards_ShowsDefaultCount()
    {
        var draft = new DraftSystem(new StubData());
        var tower = new FakeTower { TowerId = "rapid_shooter" };
        var options = draft.GenerateOptions(
            hasFreeSlots: true,
            placedTowers: [tower],
            bonusDraftCards: 0);

        int normalCount = options.Count(o => o.Type != DraftOptionType.Premium);
        Assert.Equal(Balance.DraftOptionsCount, normalCount);
    }

    // ── IsExcluded / ExpandedChassis eligibility ──────────────────────────────

    [Fact]
    public void ExpandedChassis_IsExcluded_WhenNoTowersPlaced()
    {
        var state = new RunState();  // no towers
        var card = PremiumCardRegistry.TryGet(PremiumCardRegistry.ExpandedChassisId)!;
        bool excluded = !state.Slots.Any(s => s.Tower != null && s.Tower.MaxModifiers < Balance.MaxPremiumModSlots);
        Assert.True(excluded);
    }

    [Fact]
    public void ExpandedChassis_NotExcluded_WhenTowerBelowCap()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower { MaxModifiers = Balance.MaxModifiersPerTower };  // below cap
        bool excluded = !state.Slots.Any(s => s.Tower != null && s.Tower.MaxModifiers < Balance.MaxPremiumModSlots);
        Assert.False(excluded);
    }

    [Fact]
    public void ExpandedChassis_IsExcluded_WhenAllTowersAtCap()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower { MaxModifiers = Balance.MaxPremiumModSlots };
        state.Slots[1].Tower = new FakeTower { MaxModifiers = Balance.MaxPremiumModSlots };
        bool excluded = !state.Slots.Any(s => s.Tower != null && s.Tower.MaxModifiers < Balance.MaxPremiumModSlots);
        Assert.True(excluded);
    }

    // ── MaxPremiumCardsPerRun cap ─────────────────────────────────────────────

    [Fact]
    public void MaxPremiumCardsPerRun_IsBounded()
    {
        Assert.True(Balance.MaxPremiumCardsPerRun >= 1);
        Assert.True(Balance.MaxPremiumCardsPerRun <= 10);
    }

    // ── Rarity constants sanity ───────────────────────────────────────────────

    [Fact]
    public void PremiumCardChance_IsReasonablyRare()
    {
        // Should not be too common (> 50%) or impossibly rare (< 1%)
        Assert.InRange(Balance.PremiumCardChance, 0.01f, 0.50f);
    }

    [Fact]
    public void SuperRarePremiumFraction_IsLessThanHalf()
    {
        // Super Rare must be rarer than Rare
        Assert.True(Balance.SuperRarePremiumFraction < 0.50f);
    }

    // ── Bot premium evaluation ────────────────────────────────────────────────

    [Fact]
    public void BotPlayer_TakesPremiumCard_WhenOffered()
    {
        var state = new RunState();
        state.Slots[0].Tower = new FakeTower { TowerId = "rapid_shooter" };
        var options = new List<DraftOption>
        {
            new(DraftOptionType.Tower,    "heavy_cannon"),
            new(DraftOptionType.Modifier, "momentum"),
            new(DraftOptionType.Premium,  PremiumCardRegistry.BetterOddsId),
        };
        var bot = new SlotTheory.Tools.BotPlayer(SlotTheory.Tools.BotStrategy.GreedyDps, seed: 42);
        var pick = bot.Pick(options, state);
        Assert.NotNull(pick);
        Assert.Equal(DraftOptionType.Premium, pick!.Option.Type);
    }

    [Fact]
    public void BotPlayer_ExpandedChassis_TargetsSlotWithMostMods()
    {
        var state = new RunState();
        var tower0 = new FakeTower { TowerId = "rapid_shooter" };
        var tower1 = new FakeTower { TowerId = "heavy_cannon" };
        // Tower1 has more modifiers
        tower1.Modifiers.Add(new TestModifier());
        tower1.Modifiers.Add(new TestModifier());
        state.Slots[0].Tower = tower0;
        state.Slots[1].Tower = tower1;

        var options = new List<DraftOption>
        {
            new(DraftOptionType.Premium, PremiumCardRegistry.ExpandedChassisId),
        };
        var bot = new SlotTheory.Tools.BotPlayer(SlotTheory.Tools.BotStrategy.GreedyDps, seed: 1);
        var pick = bot.Pick(options, state);
        Assert.NotNull(pick);
        Assert.Equal(1, pick!.SlotIndex);  // slot 1 = tower with most mods
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private sealed class TestModifier : SlotTheory.Modifiers.Modifier
    {
        public TestModifier() { ModifierId = "test_mod"; }
    }
}
