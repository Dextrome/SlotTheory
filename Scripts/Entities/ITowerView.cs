using System.Collections.Generic;
using Godot;
using SlotTheory.Modifiers;

namespace SlotTheory.Entities;

/// <summary>
/// Read/write view of a tower's stats. Implemented by TowerInstance (production) and FakeTower (tests).
/// Modifier OnEquip/ModifyAttackInterval methods accept ITowerView so they can be unit-tested
/// without the Godot engine.
/// </summary>
public interface ITowerView
{
    string TowerId { get; }
    bool AppliesMark { get; }
    float BaseDamage { get; set; }
    float AttackInterval { get; set; }
    float Range { get; set; }
    int SplitCount { get; set; }
    int ChainCount { get; set; }
    float ChainRange { get; set; }
    float ChainDamageDecay { get; set; }
    bool IsChainTower { get; }
    TargetingMode TargetingMode { get; }
    List<Modifier> Modifiers { get; }
    bool CanAddModifier { get; }
    float Cooldown { get; set; }
    Vector2 GlobalPosition { get; }

    void RefreshRangeCircle();

    /// <summary>
    /// Computes effective damage for tooltip display by applying unconditional damage modifiers.
    /// FakeTower returns BaseDamage; TowerInstance applies modifier hooks without a real target.
    /// </summary>
    float GetEffectiveDamageForPreview();
}
