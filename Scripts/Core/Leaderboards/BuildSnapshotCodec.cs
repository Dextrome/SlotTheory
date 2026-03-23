using System.Collections.Generic;
using System.Linq;
using SlotTheory.Entities;

namespace SlotTheory.Core.Leaderboards;

public static class BuildSnapshotCodec
{
    private const int TowerMask = 0xF;
    private const int ModMask = 0xF;

    private static readonly Dictionary<string, int> TowerToCode = new()
    {
        ["rapid_shooter"] = 1,
        ["heavy_cannon"] = 2,
        ["marker_tower"] = 3,
        ["chain_tower"] = 4,
        ["rift_prism"] = 5,
        ["accordion_engine"] = 6,
        ["phase_splitter"] = 7,
    };

    private static readonly Dictionary<int, string> CodeToTower = TowerToCode.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static readonly Dictionary<string, int> ModToCode = new()
    {
        ["momentum"] = 1,
        ["overkill"] = 2,
        ["exploit_weakness"] = 3,
        ["focus_lens"] = 4,
        ["slow"] = 5,
        ["overreach"] = 6,
        ["hair_trigger"] = 7,
        ["split_shot"] = 8,
        ["feedback_loop"] = 9,
        ["chain_reaction"] = 10,
        ["blast_core"] = 11,
    };

    private static readonly Dictionary<int, string> CodeToMod = ModToCode.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static RunBuildSnapshot Empty()
    {
        var slots = new RunSlotBuild[Balance.SlotCount];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new RunSlotBuild("", []);
        return new RunBuildSnapshot(slots);
    }

    public static RunBuildSnapshot CaptureFromRunState(RunState runState)
    {
        var slots = new RunSlotBuild[runState.Slots.Length];
        for (int i = 0; i < runState.Slots.Length; i++)
        {
            var tower = runState.Slots[i].Tower;
            if (tower == null)
            {
                slots[i] = new RunSlotBuild("", []);
                continue;
            }

            string[] mods = tower.Modifiers.Select(m => m.ModifierId).Take(Balance.MaxModifiersPerTower).ToArray();
            slots[i] = new RunSlotBuild(tower.TowerId, mods);
        }
        return new RunBuildSnapshot(slots);
    }

    public static int[] Pack(RunBuildSnapshot snapshot)
    {
        var packed = new int[Balance.SlotCount];
        for (int i = 0; i < packed.Length; i++)
        {
            if (i >= snapshot.Slots.Length)
            {
                packed[i] = 0;
                continue;
            }
            packed[i] = PackSlot(snapshot.Slots[i]);
        }
        return packed;
    }

    public static RunBuildSnapshot Unpack(IReadOnlyList<int> packed)
    {
        var slots = new RunSlotBuild[Balance.SlotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            int code = i < packed.Count ? packed[i] : 0;
            slots[i] = UnpackSlot(code);
        }
        return new RunBuildSnapshot(slots);
    }

    public static int PackSlot(RunSlotBuild slot)
    {
        if (slot == null || string.IsNullOrEmpty(slot.TowerId))
            return 0;

        TowerToCode.TryGetValue(slot.TowerId, out int towerCode);
        if (towerCode <= 0) return 0;

        int mod1 = GetModCode(slot.ModifierIds, 0);
        int mod2 = GetModCode(slot.ModifierIds, 1);
        int mod3 = GetModCode(slot.ModifierIds, 2);
        return towerCode
            | (mod1 << 4)
            | (mod2 << 8)
            | (mod3 << 12);
    }

    private static RunSlotBuild UnpackSlot(int packed)
    {
        int towerCode = packed & TowerMask;
        if (towerCode == 0 || !CodeToTower.TryGetValue(towerCode, out string? towerId))
            return new RunSlotBuild("", []);

        var mods = new List<string>(3);
        int mod1 = (packed >> 4) & ModMask;
        int mod2 = (packed >> 8) & ModMask;
        int mod3 = (packed >> 12) & ModMask;
        AddModIfValid(mods, mod1);
        AddModIfValid(mods, mod2);
        AddModIfValid(mods, mod3);
        return new RunSlotBuild(towerId, mods.ToArray());
    }

    private static int GetModCode(string[] modifierIds, int index)
    {
        if (modifierIds == null || index >= modifierIds.Length) return 0;
        string id = modifierIds[index];
        if (string.IsNullOrEmpty(id)) return 0;
        return ModToCode.TryGetValue(id, out int code) ? code : 0;
    }

    private static void AddModIfValid(List<string> mods, int code)
    {
        if (code == 0) return;
        if (!CodeToMod.TryGetValue(code, out string? id)) return;
        mods.Add(id);
    }
}
