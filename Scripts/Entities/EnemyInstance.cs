using Godot;
using SlotTheory.Core;

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

    public float SlowRemaining { get; set; } = 0f;
    public bool IsSlowed => SlowRemaining > 0f;

    private ColorRect? _hpFill;
    private float _hpBarWidth;
    private bool _wasMarked;
    private bool _wasSlow;
    private float _markAngle;

    public override void _Ready()
    {
        Loop = false; // enemies stop at end of path so ProgressRatio >= 1 triggers loss
    }

    public void Initialize(string typeId, float hp, float speed)
    {
        EnemyTypeId = typeId;
        Hp = MaxHp = hp;
        Speed = speed;

        bool isArmored = typeId == "armored_walker";
        if (isArmored)
            Scale = new Vector2(1.5f, 1.5f);

        _hpBarWidth    = isArmored ? 34f : 24f;
        float barY     = isArmored ? -26f : -20f;
        float barX     = -_hpBarWidth / 2f;

        // HP bar track
        AddChild(new ColorRect
        {
            Position    = new Vector2(barX, barY),
            Size        = new Vector2(_hpBarWidth, 3f),
            Color    = new Color(0.05f, 0.00f, 0.10f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        _hpFill = new ColorRect
        {
            Position    = new Vector2(barX, barY),
            Size        = new Vector2(_hpBarWidth, 3f),
            Color    = new Color(0.00f, 0.95f, 0.80f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_hpFill);
    }

    public override void _Process(double delta)
    {
        float effectiveSpeed = IsSlowed ? Speed * Balance.SlowSpeedFactor : Speed;
        Progress += effectiveSpeed * (float)delta;

        if (MarkedRemaining > 0f) MarkedRemaining -= (float)delta;
        if (SlowRemaining  > 0f) SlowRemaining  -= (float)delta;

        // Spin mark ring
        if (IsMarked) _markAngle += (float)delta * 2.5f;

        // Update HP bar width and colour
        if (_hpFill != null && MaxHp > 0f)
        {
            float ratio = Mathf.Clamp(Hp / MaxHp, 0f, 1f);
            _hpFill.Size = new Vector2(_hpBarWidth * ratio, _hpFill.Size.Y);
            _hpFill.Color = ratio > 0.5f
                ? (EnemyTypeId == "armored_walker" ? new Color(1.00f, 0.05f, 0.55f) : new Color(0.00f, 0.95f, 0.80f))
                : ratio > 0.25f
                    ? new Color(1.00f, 0.85f, 0.00f)
                    : new Color(1.00f, 0.15f, 0.60f);
        }

        bool nowMarked = IsMarked;
        bool nowSlowed = IsSlowed;

        // Blue-grey tint while slowed — uses SelfModulate so it stacks with FlashHit on Modulate
        if (nowSlowed != _wasSlow)
            SelfModulate = nowSlowed ? new Color(0.70f, 0.85f, 1.00f) : Colors.White;

        // Redraw when status changes OR every frame while marked (for ring rotation)
        if (nowMarked != _wasMarked || nowSlowed != _wasSlow || IsMarked)
        {
            _wasMarked = nowMarked;
            _wasSlow   = nowSlowed;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (EnemyTypeId == "armored_walker")
            DrawArmoredWalker();
        else
            DrawBasicWalker();
    }

    // ── Basic Walker ─────────────────────────────────────────────────────

    private void DrawBasicWalker()
    {
        var cyan = new Color(0.00f, 0.95f, 0.80f);
        var dark = new Color(0.00f, 0.10f, 0.12f);
        // Glow
        DrawCircle(Vector2.Zero, 20f, new Color(cyan.R, cyan.G, cyan.B, 0.07f));
        DrawCircle(Vector2.Zero, 14f, new Color(cyan.R, cyan.G, cyan.B, 0.15f));
        // Body ΓÇö bright ring + dark interior + inner accent
        DrawCircle(Vector2.Zero, 11f, cyan);
        DrawCircle(Vector2.Zero, 9f,  dark);
        DrawCircle(new Vector2(-1.5f, -2f), 5f, new Color(0.00f, 0.50f, 0.42f));
        // Eyes
        DrawCircle(new Vector2(-3f, -2.5f), 2.2f, Colors.White);
        DrawCircle(new Vector2( 3f, -2.5f), 2.2f, Colors.White);
        DrawCircle(new Vector2(-3f, -2.0f), 1.1f, new Color(0.05f, 0.05f, 0.05f));
        DrawCircle(new Vector2( 3f, -2.0f), 1.1f, new Color(0.05f, 0.05f, 0.05f));
        // Mark ring — 3 spinning 90° dashes
        if (IsMarked)
            for (int s = 0; s < 3; s++)
            {
                float a = _markAngle + s * (Mathf.Tau / 3f);
                DrawArc(Vector2.Zero, 13f, a, a + Mathf.Pi * 0.5f, 12, new Color(0.85f, 0.30f, 1.00f, 0.90f), 2.5f);
            }
        // Slow ring
        if (IsSlowed)
            DrawArc(Vector2.Zero, 15.5f, 0f, Mathf.Tau, 32, new Color(0.20f, 0.85f, 1.00f, 0.90f), 2.5f);
    }

    // ── Armored Walker ───────────────────────────────────────────────────

    private void DrawArmoredWalker()
    {
        // Hexagonal body — deep crimson, larger than basic walker
        DrawPolygon(RegularPoly(6, 17f, 0f), new[] { new Color(0.22f, 0.03f, 0.03f) }); // dark outer rim
        DrawPolygon(RegularPoly(6, 14f, 0f), new[] { new Color(0.62f, 0.07f, 0.07f) }); // deep crimson body
        DrawPolygon(RegularPoly(6, 10f, 0f), new[] { new Color(0.78f, 0.14f, 0.12f) }); // slightly lighter centre
        // Highlight (upper-left, muted)
        DrawCircle(new Vector2(-3f, -3f), 5.5f, new Color(0.88f, 0.24f, 0.18f));
        // Eyes — bigger and meaner
        DrawCircle(new Vector2(-3.5f, -2.5f), 2.8f, Colors.White);
        DrawCircle(new Vector2( 3.5f, -2.5f), 2.8f, Colors.White);
        DrawCircle(new Vector2(-3.5f, -2.0f), 1.4f, new Color(0.05f, 0.05f, 0.05f));
        DrawCircle(new Vector2( 3.5f, -2.0f), 1.4f, new Color(0.05f, 0.05f, 0.05f));
        // Mark ring — 3 spinning 90° dashes (wider radius for larger body)
        if (IsMarked)
            for (int s = 0; s < 3; s++)
            {
                float a = _markAngle + s * (Mathf.Tau / 3f);
                DrawArc(Vector2.Zero, 19f, a, a + Mathf.Pi * 0.5f, 12, new Color(0.85f, 0.30f, 1.00f, 0.90f), 2.5f);
            }
        // Slow ring (cyan)
        if (IsSlowed)
            DrawArc(Vector2.Zero, 21.5f, 0f, Mathf.Tau, 32, new Color(0.20f, 0.85f, 1.00f, 0.90f), 2.5f);
    }

    public void FlashHit()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", new Color(2f, 2f, 2f), 0.03f);
        tween.TweenProperty(this, "modulate", Colors.White, 0.15f)
             .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

    private static Vector2[] RegularPoly(int sides, float radius, float angleOffset)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = angleOffset + i * Mathf.Tau / sides;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
        return pts;
    }
}
