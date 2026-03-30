using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Core;

public enum DraftOptionType { Tower, Modifier, Premium }

public record DraftOption(DraftOptionType Type, string Id);

/// <summary>Abstraction over DataLoader so DraftSystem can be unit-tested without Godot.</summary>
public interface IDraftDataSource
{
    IEnumerable<string> GetAllTowerIds();
    IEnumerable<string> GetAllModifierIds();
}

/// <summary>Production implementation - delegates to the static DataLoader.</summary>
public sealed class DataLoaderDraftDataSource : IDraftDataSource
{
    public IEnumerable<string> GetAllTowerIds()    => DataLoader.GetAllTowerIds();
    public IEnumerable<string> GetAllModifierIds() => DataLoader.GetAllModifierIds();
}

public class DraftSystem
{
    private readonly Random _rng;
    private readonly IDraftDataSource _data;

    public DraftSystem() : this(new DataLoaderDraftDataSource()) { }
    // Seeded constructor: pass a deterministic seed (e.g. run index) so that
    // draft option pools are identical across candidates in the tuning pipeline.
    // Without this, DraftSystem uses a fresh Random() each time, meaning two
    // candidates evaluating "run #42" see different card pools - injecting noise
    // that swamps the win-rate signal the optimizer is trying to track.
    public DraftSystem(int seed) : this(new DataLoaderDraftDataSource(), seed) { }
    public DraftSystem(IDraftDataSource data) { _data = data; _rng = new Random(); }
    public DraftSystem(IDraftDataSource data, int seed) { _data = data; _rng = new Random(seed); }

    public List<DraftOption> GenerateOptions(RunState state)
    {
        // Wave 1: always offer towers only - player has no towers yet
        if (state.WaveIndex == 0)
        {
            int wave1Total = Balance.DraftOptionsCount + state.BonusDraftCards;
            var wave1Options = new List<DraftOption>(wave1Total);
            AddTowerOptions(wave1Options, wave1Total, state.ActiveMandate);
            return wave1Options;
        }
        var placedTowers = state.Slots
            .Where(s => s.Tower != null)
            .Select(s => (Entities.ITowerView)s.Tower!);
        var options = GenerateOptions(state.HasFreeSlots(), placedTowers, state.ActiveMandate, state.WaveIndex, state.BonusDraftCards);

        if (options.Count == 0)
        {
            // All tower slots and modifier slots are full - fall back to premium-only draft.
            int count = Balance.DraftModifierOptionsFull + state.BonusDraftCards;
            AddPremiumOptions(options, count, state);
        }
        else
        {
            TryInjectPremiumCard(options, state);
        }

        return options;
    }

    /// <summary>
    /// Overload for unit tests: supply free-slot status and tower views directly,
    /// with no dependency on RunState or Godot-backed TowerInstance.
    /// <paramref name="waveIndex"/> gates wave-restricted modifiers (e.g. Reaper Protocol at wave 9+);
    /// defaults to 0 so existing test calls that omit it behave identically to pre-gating code.
    /// <paramref name="bonusDraftCards"/> adds extra options (from Better Odds); defaults to 0.
    /// </summary>
    public List<DraftOption> GenerateOptions(bool hasFreeSlots, IEnumerable<Entities.ITowerView> placedTowers,
        MandateDefinition? mandate = null, int waveIndex = 0, int bonusDraftCards = 0)
    {
        int targetCount = (hasFreeSlots ? Balance.DraftOptionsCount : Balance.DraftModifierOptionsFull) + bonusDraftCards;
        var options = new List<DraftOption>(targetCount);

        if (hasFreeSlots)
        {
            int modCount = targetCount - Balance.DraftTowerOptions;
            AddTowerOptions(options, Balance.DraftTowerOptions, mandate, waveIndex);
            AddModifierOptions(options, modCount, placedTowers, mandate, waveIndex);
        }
        else
        {
            AddModifierOptions(options, targetCount, placedTowers, mandate, waveIndex);
        }

        // Pad to targetCount with tower options if modifiers couldn't fill the list
        // (e.g. all towers at modifier cap, or not enough modifier pool entries)
        if (options.Count < targetCount && hasFreeSlots)
            AddTowerOptions(options, targetCount - options.Count, mandate, waveIndex);

        return options;
    }

    /// <summary>
    /// Fill <paramref name="options"/> with premium cards when no normal options exist.
    /// Respects the same exclusion rules as TryInjectPremiumCard.
    /// </summary>
    private void AddPremiumOptions(List<DraftOption> options, int count, RunState state)
    {
        var pool = PremiumCardRegistry.GetAll()
            .Where(c => !IsExcluded(c, state))
            .OrderBy(_ => _rng.Next())
            .Take(count);
        foreach (var card in pool)
            options.Add(new DraftOption(DraftOptionType.Premium, card.Id));
    }

    /// <summary>
    /// Attempt to replace a random existing option with a premium card.
    /// Fires at most once per draft, never on wave 1, and respects per-run caps.
    /// The premium card takes one of the normal draft slots so the total count stays the same.
    /// </summary>
    private void TryInjectPremiumCard(List<DraftOption> options, RunState state)
    {
        if (options.Count == 0) return;
        if (state.PickedPremiumCards.Count >= Balance.MaxPremiumCardsPerRun) return;
        if (_rng.NextDouble() >= Balance.PremiumCardChance) return;

        bool superRare = _rng.NextDouble() < Balance.SuperRarePremiumFraction;
        var rarity = superRare ? PremiumRarity.SuperRare : PremiumRarity.Rare;

        var available = PremiumCardRegistry.GetAll()
            .Where(c => c.Rarity == rarity && !IsExcluded(c, state))
            .ToList();

        // Fall back to the other rarity if the target tier has no eligible cards
        if (available.Count == 0)
        {
            rarity = superRare ? PremiumRarity.Rare : PremiumRarity.SuperRare;
            available = PremiumCardRegistry.GetAll()
                .Where(c => c.Rarity == rarity && !IsExcluded(c, state))
                .ToList();
        }

        if (available.Count == 0) return;

        var card = available[_rng.Next(available.Count)];
        int replaceIndex = _rng.Next(options.Count);
        options[replaceIndex] = new DraftOption(DraftOptionType.Premium, card.Id);
    }

    private static bool IsExcluded(PremiumCardDef card, RunState state)
    {
        int copies = state.PickedPremiumCards.Count(id => id == card.Id);
        if (copies >= PremiumCardRegistry.GetMaxCopies(card.Id)) return true;

        // Long Fuse and Better Odds are full-game only -- excluded from demo builds.
        if (Balance.IsDemo && card.Id == PremiumCardRegistry.LongFuseId)   return true;
        if (Balance.IsDemo && card.Id == PremiumCardRegistry.BetterOddsId) return true;

        // Expanded Chassis: no eligible tower (all at cap or no towers placed)
        if (card.Id == PremiumCardRegistry.ExpandedChassisId)
            return !state.Slots.Any(s => s.Tower != null && s.Tower.MaxModifiers < Balance.MaxPremiumModSlots);

        return false;
    }

    public void ApplyTower(string towerId, RunState state)
    {
        var def = DataLoader.GetTowerDef(towerId);
        var freeSlot = state.Slots.FirstOrDefault(s => s.Tower == null && !s.IsLocked);
        if (freeSlot == null) return;

        // Tower node is instantiated and wired up by GameController via scene
        // This marks the slot as pending tower placement
        freeSlot.PendingTowerId = towerId;
    }

    public void ApplyModifier(string modifierId, Entities.ITowerView tower)
    {
        if (!tower.CanAddModifier) return;
        var mod = Modifiers.ModifierRegistry.Create(modifierId);
        tower.Modifiers.Add(mod);
        mod.OnEquip(tower);
    }

    private void AddTowerOptions(List<DraftOption> list, int count, MandateDefinition? mandate = null, int waveIndex = 0)
    {
        var pool = _data.GetAllTowerIds()
            .Where(id => mandate?.IsTowerBanned(id) != true)
            .Where(id => Balance.GetTowerMinWaveIndex(id) <= waveIndex)  // wave gate
            .OrderBy(_ => _rng.Next())
            .Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Tower, id));
    }

    private void AddModifierOptions(List<DraftOption> list, int count,
                                     IEnumerable<Entities.ITowerView> placedTowers,
                                     MandateDefinition? mandate = null,
                                     int waveIndex = 0)
    {
        var towersWithSpace = placedTowers.Where(t => t.CanAddModifier).ToList();

        if (towersWithSpace.Count == 0) return; // full anti-brick: no eligible towers

        var pool = _data.GetAllModifierIds()
            .Where(id => mandate?.IsModifierBanned(id) != true)
            .Where(id => Balance.GetModifierMinWaveIndex(id) <= waveIndex)  // wave gate
            .OrderBy(_ => _rng.Next())
            .Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Modifier, id));
    }
}
