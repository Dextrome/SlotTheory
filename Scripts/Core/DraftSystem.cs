using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Core;

public enum DraftOptionType { Tower, Modifier }

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
    // candidates evaluating "run #42" see different card pools — injecting noise
    // that swamps the win-rate signal the optimizer is trying to track.
    public DraftSystem(int seed) : this(new DataLoaderDraftDataSource(), seed) { }
    public DraftSystem(IDraftDataSource data) { _data = data; _rng = new Random(); }
    public DraftSystem(IDraftDataSource data, int seed) { _data = data; _rng = new Random(seed); }

    public List<DraftOption> GenerateOptions(RunState state)
    {
        // Wave 1: always offer towers only — player has no towers yet
        if (state.WaveIndex == 0)
        {
            var wave1Options = new List<DraftOption>(Balance.DraftOptionsCount);
            AddTowerOptions(wave1Options, Balance.DraftOptionsCount);
            return wave1Options;
        }
        var placedTowers = state.Slots
            .Where(s => s.Tower != null)
            .Select(s => (Entities.ITowerView)s.Tower!);
        return GenerateOptions(state.HasFreeSlots(), placedTowers);
    }

    /// <summary>
    /// Overload for unit tests: supply free-slot status and tower views directly,
    /// with no dependency on RunState or Godot-backed TowerInstance.
    /// </summary>
    public List<DraftOption> GenerateOptions(bool hasFreeSlots, IEnumerable<Entities.ITowerView> placedTowers)
    {
        var options = new List<DraftOption>(Balance.DraftOptionsCount);

        if (hasFreeSlots)
        {
            AddTowerOptions(options, Balance.DraftTowerOptions);
            AddModifierOptions(options, Balance.DraftModifierOptions, placedTowers);
        }
        else
        {
            AddModifierOptions(options, Balance.DraftModifierOptionsFull, placedTowers);
        }

        // Pad to 5 with tower options if modifiers couldn't fill the list
        // (e.g. wave 1 with no towers yet, or all towers at modifier cap)
        if (options.Count < Balance.DraftOptionsCount && hasFreeSlots)
            AddTowerOptions(options, Balance.DraftOptionsCount - options.Count);

        return options;
    }

    public void ApplyTower(string towerId, RunState state)
    {
        var def = DataLoader.GetTowerDef(towerId);
        var freeSlot = state.Slots.FirstOrDefault(s => s.Tower == null);
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

    private void AddTowerOptions(List<DraftOption> list, int count)
    {
        var pool = _data.GetAllTowerIds().OrderBy(_ => _rng.Next()).Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Tower, id));
    }

    private void AddModifierOptions(List<DraftOption> list, int count,
                                     IEnumerable<Entities.ITowerView> placedTowers)
    {
        var towersWithSpace = placedTowers.Where(t => t.CanAddModifier).ToList();

        if (towersWithSpace.Count == 0) return; // full anti-brick: no eligible towers

        var pool = _data.GetAllModifierIds().OrderBy(_ => _rng.Next()).Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Modifier, id));
    }
}
