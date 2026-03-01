using System;
using System.Collections.Generic;
using System.Linq;
using SlotTheory.Data;
using SlotTheory.Entities;

namespace SlotTheory.Core;

public enum DraftOptionType { Tower, Modifier }

public record DraftOption(DraftOptionType Type, string Id);

public class DraftSystem
{
    private readonly Random _rng = new();

    public List<DraftOption> GenerateOptions(RunState state)
    {
        var options = new List<DraftOption>(Balance.DraftOptionsCount);

        if (state.HasFreeSlots())
        {
            AddTowerOptions(options, Balance.DraftTowerOptions, state);
            AddModifierOptions(options, Balance.DraftModifierOptions, state);
        }
        else
        {
            // Anti-brick: only offer modifiers that can actually be applied
            AddModifierOptions(options, Balance.DraftOptionsCount, state);
        }

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

    public void ApplyModifier(string modifierId, TowerInstance tower)
    {
        if (!tower.CanAddModifier) return;
        var mod = Modifiers.ModifierRegistry.Create(modifierId);
        tower.Modifiers.Add(mod);
    }

    private void AddTowerOptions(List<DraftOption> list, int count, RunState state)
    {
        var pool = DataLoader.GetAllTowerIds().OrderBy(_ => _rng.Next()).Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Tower, id));
    }

    private void AddModifierOptions(List<DraftOption> list, int count, RunState state)
    {
        var towersWithSpace = state.Slots
            .Where(s => s.Tower != null && s.Tower.CanAddModifier)
            .Select(s => s.Tower!)
            .ToList();

        if (towersWithSpace.Count == 0) return; // full anti-brick: no eligible towers

        var pool = DataLoader.GetAllModifierIds().OrderBy(_ => _rng.Next()).Take(count);
        foreach (var id in pool)
            list.Add(new DraftOption(DraftOptionType.Modifier, id));
    }
}
