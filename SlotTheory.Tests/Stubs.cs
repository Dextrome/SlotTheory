using System.Collections.Generic;
using Godot;
using SlotTheory.Entities;
using SlotTheory.Modifiers;

namespace SlotTheory.Tests;

/// <summary>
/// Minimal tower stub for unit tests. Implements ITowerView with plain mutable fields.
/// RefreshRangeCircle is a no-op (no Godot scene graph available in tests).
/// </summary>
public class FakeTower : ITowerView
{
    public string TowerId { get; set; } = "test_tower";
    public bool AppliesMark { get; set; } = false;
    public float BaseDamage { get; set; } = 10f;
    public float AttackInterval { get; set; } = 1f;
    public float Range { get; set; } = 200f;
    public int SplitCount { get; set; } = 0;
    public int ChainCount { get; set; } = 0;
    public float ChainRange { get; set; } = 400f;
    public float ChainDamageDecay { get; set; } = 0.6f;
    public bool IsChainTower => ChainCount > 0;
    public TargetingMode TargetingMode { get; set; } = TargetingMode.First;
    public List<Modifier> Modifiers { get; } = new();
    private bool? _canAddModifier;
    /// <summary>Set explicitly in tests to simulate a tower at modifier cap.</summary>
    public bool CanAddModifier
    {
        get => _canAddModifier ?? (Modifiers.Count < SlotTheory.Core.Balance.MaxModifiersPerTower);
        set => _canAddModifier = value;
    }
    public float Cooldown { get; set; } = 0f;
    public Vector2 GlobalPosition { get; set; } = Vector2.Zero;

    public void RefreshRangeCircle() { } // no-op in tests
    public float GetEffectiveDamageForPreview() => BaseDamage;
}

/// <summary>
/// Minimal enemy stub for unit tests. Implements IEnemyView with plain mutable fields.
/// </summary>
public class FakeEnemy : IEnemyView
{
    public float Hp { get; set; } = 65f;
    public float ProgressRatio { get; set; } = 0f;
    public Vector2 GlobalPosition { get; set; } = Vector2.Zero;
    public bool IsMarked => MarkedRemaining > 0f;
    public float MarkedRemaining { get; set; } = 0f;
    public float SlowSpeedFactor { get; set; } = 1f;
    public float SlowRemaining { get; set; } = 0f;
    public float DamageAmpRemaining { get; set; } = 0f;
    public float DamageAmpMultiplier { get; set; } = 0f;
    public bool IsShieldProtected { get; set; } = false;
    public float BurnRemaining { get; set; } = 0f;
    public float BurnDamagePerSecond { get; set; } = 0f;
    public int BurnOwnerSlotIndex { get; set; } = -1;
    public float BurnTrailDropTimer { get; set; } = 0f;
}
