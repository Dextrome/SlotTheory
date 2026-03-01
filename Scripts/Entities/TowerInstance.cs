using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Modifiers;

namespace SlotTheory.Entities;

public enum TargetingMode { First, Strongest, LowestHp }

/// <summary>
/// Tower node. Positioned as a child of its Slot node so GlobalPosition is correct for range checks.
/// </summary>
public partial class TowerInstance : Node2D
{
    public string TowerId { get; set; } = string.Empty;
    public float BaseDamage { get; set; }
    public float AttackInterval { get; set; }
    public float Range { get; set; }
    public bool AppliesMark { get; set; }

    public TargetingMode TargetingMode { get; set; } = TargetingMode.First;
    public float Cooldown { get; set; } = 0f;
    public Color ProjectileColor { get; set; } = Colors.Yellow;

    public List<Modifier> Modifiers { get; } = new();
    public string? LastTargetId { get; set; }

    public bool CanAddModifier => Modifiers.Count < Balance.MaxModifiersPerTower;

    public Label? ModeLabel { get; set; }

    public void CycleTargetingMode()
    {
        TargetingMode = TargetingMode switch
        {
            TargetingMode.First     => TargetingMode.Strongest,
            TargetingMode.Strongest => TargetingMode.LowestHp,
            _                      => TargetingMode.First,
        };
        if (ModeLabel != null)
            ModeLabel.Text = ModeIcon(TargetingMode);
    }

    public static string ModeIcon(TargetingMode mode) => mode switch
    {
        TargetingMode.First     => "▶",
        TargetingMode.Strongest => "★",
        TargetingMode.LowestHp  => "▼",
        _                      => "▶",
    };
}
