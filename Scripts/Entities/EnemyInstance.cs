using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Enemy node. Extends PathFollow2D so it self-moves along LanePath.
/// RunState.EnemiesAlive holds references to these nodes.
/// </summary>
public partial class EnemyInstance : PathFollow2D
{
    public string EnemyTypeId { get; private set; } = "basic_walker";
    public float Hp { get; set; }
    public float MaxHp { get; private set; }
    public float Speed { get; set; }          // pixels per second

    public float MarkedRemaining { get; set; } = 0f;
    public bool IsMarked => MarkedRemaining > 0f;

    public override void _Ready()
    {
        Loop = false; // enemies stop at end of path so ProgressRatio >= 1 triggers loss
    }

    public void Initialize(string typeId, float hp, float speed)
    {
        EnemyTypeId = typeId;
        Hp = MaxHp = hp;
        Speed = speed;
    }

    public override void _Process(double delta)
    {
        Progress += Speed * (float)delta;

        if (MarkedRemaining > 0f)
            MarkedRemaining -= (float)delta;
    }
}
