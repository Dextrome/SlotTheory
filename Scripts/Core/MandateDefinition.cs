using SlotTheory.Data;

namespace SlotTheory.Core;

/// <summary>
/// Runtime wrapper around a MandateDef that exposes typed helpers used by
/// DraftSystem, WaveSystem, and GameController. Keeps Models.cs as pure data.
/// </summary>
public sealed class MandateDefinition
{
    public static readonly MandateDefinition None = new(new MandateDef(MandateType.None, ""));

    private readonly MandateDef _def;

    public MandateDefinition(MandateDef def) => _def = def;

    public MandateType Type            => _def.Type;
    public string      DisplayText     => _def.DisplayText;
    public int         LockedSlotCount => _def.LockedSlotCount;
    public float       EnemyHpMultiplier => _def.EnemyHpMultiplier;

    public bool IsActive =>
        _def.Type != MandateType.None
        || (_def.BannedTowerIds?.Length    > 0)
        || (_def.BannedModifierIds?.Length > 0);

    public bool IsTowerBanned(string towerId)
        => _def.BannedTowerIds != null
           && System.Array.Exists(_def.BannedTowerIds, id =>
               string.Equals(id, towerId, System.StringComparison.OrdinalIgnoreCase));

    public bool IsModifierBanned(string modifierId)
        => (_def.BannedModifierIds != null
            && System.Array.Exists(_def.BannedModifierIds, id =>
                string.Equals(id, modifierId, System.StringComparison.OrdinalIgnoreCase)))
           || (_def.Type == MandateType.BannedModifiers   // backward compat with old BannedIds field
               && _def.BannedIds != null
               && System.Array.Exists(_def.BannedIds, id =>
                   string.Equals(id, modifierId, System.StringComparison.OrdinalIgnoreCase)));
}
