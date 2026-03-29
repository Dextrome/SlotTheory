using Godot;
using SlotTheory.Combat;

namespace SlotTheory.Entities;

/// <summary>
/// Lightweight host-linked parasite visual for Latch Nest.
/// Combat state is owned by CombatSim; this node only mirrors state.
/// </summary>
public partial class LatchParasiteVfx : Node2D
{
    private EnemyInstance? _host;
    private int _hostSlot;
    private Color _accent = new(0.70f, 0.96f, 0.56f);
    private float _pulse;
    private float _detachAnim;
    private bool _detached;
    private bool _hostDeathPop;
    private Vector2 _detachDrift;

    public ulong ParasiteId { get; private set; }

    public void Initialize(ulong parasiteId, EnemyInstance host, int hostSlot, Color accent)
    {
        ParasiteId = parasiteId;
        _host = host;
        _hostSlot = hostSlot;
        _accent = accent;
        _pulse = 0f;
        _detachAnim = 0f;
        _detached = false;
        _hostDeathPop = false;
        _detachDrift = Vector2.Zero;
        GlobalPosition = ResolveAnchor();
        QueueRedraw();
    }

    public void NotifyTick()
    {
        _pulse = 1f;
        QueueRedraw();
    }

    public void NotifyDetached(bool hostDeathPop)
    {
        _detached = true;
        _hostDeathPop = hostDeathPop;
        _detachAnim = 0.18f;
        _detachDrift = new Vector2(_hostSlot == 0 ? -8f : 8f, -12f);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!_detached)
        {
            if (_host == null || !GodotObject.IsInstanceValid(_host) || _host.Hp <= 0f)
            {
                QueueFree();
                return;
            }

            GlobalPosition = ResolveAnchor();
        }
        else
        {
            if (_detachAnim <= 0f)
            {
                QueueFree();
                return;
            }

            _detachAnim = Mathf.Max(0f, _detachAnim - dt);
            GlobalPosition += _detachDrift * dt;
            _detachDrift *= 0.90f;
        }

        if (_pulse > 0f)
            _pulse = Mathf.Max(0f, _pulse - dt * 4.4f);

        QueueRedraw();
    }

    public override void _Draw()
    {
        float detachAlpha = _detached ? Mathf.Clamp(_detachAnim / 0.18f, 0f, 1f) : 1f;
        float pulse = 1f + _pulse * 0.55f;
        Color shell = new(_accent.R * 0.48f + 0.14f, _accent.G * 0.38f + 0.22f, _accent.B * 0.26f + 0.14f, 0.92f * detachAlpha);
        Color spike = new(0.92f, 0.98f, 0.84f, 0.85f * detachAlpha);
        Color core = new(_accent.R, _accent.G, _accent.B, (0.82f + _pulse * 0.18f) * detachAlpha);

        if (_hostDeathPop)
            DrawCircle(Vector2.Zero, 8.5f * pulse, new Color(shell.R, shell.G, shell.B, 0.10f * detachAlpha));

        DrawCircle(Vector2.Zero, 5.2f * pulse, new Color(shell.R, shell.G, shell.B, 0.24f * detachAlpha));
        DrawCircle(Vector2.Zero, 3.1f * pulse, shell);
        DrawPolygon(
            new[]
            {
                new Vector2(0f, -5.0f * pulse),
                new Vector2(4.0f * pulse, -1.0f * pulse),
                new Vector2(2.3f * pulse, 4.2f * pulse),
                new Vector2(-2.3f * pulse, 4.2f * pulse),
                new Vector2(-4.0f * pulse, -1.0f * pulse),
            },
            new[] { shell });
        DrawCircle(new Vector2(0f, -0.4f * pulse), 1.4f * pulse, core);
        DrawLine(new Vector2(-2.2f * pulse, 3.4f * pulse), new Vector2(-4.9f * pulse, 6.1f * pulse), spike, 1.1f);
        DrawLine(new Vector2(2.2f * pulse, 3.4f * pulse), new Vector2(4.9f * pulse, 6.1f * pulse), spike, 1.1f);
    }

    private Vector2 ResolveAnchor()
    {
        if (_host == null || !GodotObject.IsInstanceValid(_host))
            return GlobalPosition;

        Vector2 lateral = _hostSlot == 0 ? new Vector2(-7f, -3f) : new Vector2(7f, 3f);
        return _host.GlobalPosition + lateral;
    }
}
