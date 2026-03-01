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

    private ColorRect? _hpFill;
    private bool _wasMarked;

    public override void _Ready()
    {
        Loop = false; // enemies stop at end of path so ProgressRatio >= 1 triggers loss
    }

    public void Initialize(string typeId, float hp, float speed)
    {
        EnemyTypeId = typeId;
        Hp = MaxHp = hp;
        Speed = speed;

        // HP bar — track then fill
        AddChild(new ColorRect
        {
            Position    = new Vector2(-12f, -20f),
            Size        = new Vector2(24f, 3f),
            Color       = new Color(0.15f, 0.15f, 0.15f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _hpFill = new ColorRect
        {
            Position    = new Vector2(-12f, -20f),
            Size        = new Vector2(24f, 3f),
            Color       = new Color(0.15f, 0.90f, 0.25f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_hpFill);
    }

    public override void _Process(double delta)
    {
        Progress += Speed * (float)delta;

        if (MarkedRemaining > 0f)
            MarkedRemaining -= (float)delta;

        // Update HP bar width and colour
        if (_hpFill != null && MaxHp > 0f)
        {
            float ratio = Mathf.Clamp(Hp / MaxHp, 0f, 1f);
            _hpFill.Size = new Vector2(24f * ratio, _hpFill.Size.Y);
            _hpFill.Color = ratio > 0.5f
                ? new Color(0.15f, 0.90f, 0.25f)   // green
                : ratio > 0.25f
                    ? new Color(0.90f, 0.70f, 0.10f) // yellow
                    : new Color(0.90f, 0.15f, 0.10f); // red
        }

        // Redraw only when mark state toggles (keeps draw calls cheap)
        bool nowMarked = IsMarked;
        if (nowMarked != _wasMarked)
        {
            _wasMarked = nowMarked;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        // Outer dark ring
        DrawCircle(Vector2.Zero, 11f, new Color(0.40f, 0.08f, 0.05f));
        // Main body
        DrawCircle(Vector2.Zero, 9f, new Color(0.95f, 0.22f, 0.12f));
        // Highlight (upper-left)
        DrawCircle(new Vector2(-2.5f, -2.5f), 4.5f, new Color(1.00f, 0.48f, 0.30f));
        // Eyes
        DrawCircle(new Vector2(-3f, -2.5f), 2.2f, Colors.White);
        DrawCircle(new Vector2( 3f, -2.5f), 2.2f, Colors.White);
        DrawCircle(new Vector2(-3f, -2.0f), 1.1f, new Color(0.08f, 0.08f, 0.08f));
        DrawCircle(new Vector2( 3f, -2.0f), 1.1f, new Color(0.08f, 0.08f, 0.08f));
        // Mark ring
        if (IsMarked)
            DrawArc(Vector2.Zero, 13f, 0f, Mathf.Tau, 32, new Color(0.85f, 0.30f, 1.00f, 0.90f), 2.5f);
    }
}
