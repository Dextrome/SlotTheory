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
    public Color BodyColor { get; set; } = Colors.White;

    public List<Modifier> Modifiers { get; } = new();
    public string? LastTargetId { get; set; }
    public Vector2? LastTargetPosition { get; set; }

    public int   ChainCount       { get; set; } = 0;
    public float ChainRange       { get; set; } = 260f;
    public float ChainDamageDecay { get; set; } = 0.6f;
    public bool  IsChainTower     => ChainCount > 0;

    public bool CanAddModifier => Modifiers.Count < Balance.MaxModifiersPerTower;

    public Label?    ModeLabel   { get; set; }
    public Polygon2D? RangeCircle { get; set; }
    public Line2D?   RangeBorder  { get; set; }

    /// <summary>Rebuilds the range circle fill and border to match the tower's current Range value.</summary>
    public void RefreshRangeCircle()
    {
        var pts = new Vector2[65]; // 65th point closes the loop for Line2D
        for (int p = 0; p < 64; p++)
        {
            float a = p * Mathf.Tau / 64;
            pts[p] = new Vector2(Mathf.Cos(a) * Range, Mathf.Sin(a) * Range);
        }
        pts[64] = pts[0];

        if (RangeCircle != null)
            RangeCircle.Polygon = pts[..64];
        if (RangeBorder != null)
            RangeBorder.Points = pts;
    }

    public override void _Process(double delta)
    {
        // Smoothly rotate barrel toward last known target
        if (LastTargetPosition.HasValue)
        {
            var dir = LastTargetPosition.Value - GlobalPosition;
            float targetAngle = dir.Angle() + Mathf.Pi * 0.5f; // barrels point local -Y
            Rotation = Mathf.LerpAngle(Rotation, targetAngle, 15f * (float)delta);
        }
        if (AttackInterval > 0f) QueueRedraw();
    }

    public void FlashAttack()
    {
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate", new Color(1.4f, 1.4f, 1.4f), 0.03f);
        tween.TweenProperty(this, "modulate", Colors.White, 0.25f)
             .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
    }

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

    public override void _Draw()
    {
        switch (TowerId)
        {
            case "rapid_shooter": DrawRapidShooter(); break;
            case "heavy_cannon":  DrawHeavyCannon();  break;
            case "marker_tower":  DrawMarkerTower();  break;
            case "chain_tower":   DrawChainTower();   break;
            default: DrawCircle(Vector2.Zero, 10f, new Color(0.2f, 0.5f, 1.0f)); break;
        }
        DrawChargeArc();
    }

    private void DrawChargeArc()
    {
        if (AttackInterval <= 0f) return;
        float fill   = Mathf.Clamp(1f - Cooldown / AttackInterval, 0f, 1f);
        const float r = 21f;

        // Dim background ring
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 48,
            new Color(BodyColor.R, BodyColor.G, BodyColor.B, 0.16f), 2f);

        // Filled charge arc — clockwise from top
        if (fill > 0.01f)
        {
            float start = -Mathf.Pi / 2f;
            float end   = start + fill * Mathf.Tau;
            DrawArc(Vector2.Zero, r, start, end, 48,
                new Color(BodyColor.R, BodyColor.G, BodyColor.B, 0.88f), 2.5f);
        }
    }

    private void DrawRapidShooter()
    {
        var cyan  = new Color(0.10f, 0.75f, 1.00f);
        var dark  = new Color(0.02f, 0.02f, 0.12f);
        var flash = new Color(0.70f, 0.95f, 1.00f);
        // Soft glow behind everything
        DrawCircle(Vector2.Zero, 22f, new Color(cyan.R, cyan.G, cyan.B, 0.07f));
        DrawCircle(Vector2.Zero, 15f, new Color(cyan.R, cyan.G, cyan.B, 0.14f));
        // Barrel ΓÇö bright outer, dark inner
        DrawRect(new Rect2(-3.5f, -23f, 7f, 18f), cyan);
        DrawRect(new Rect2(-2.0f, -22f, 4f, 16f), dark);
        // Muzzle glow
        DrawCircle(new Vector2(0f, -23f), 7f, new Color(flash.R, flash.G, flash.B, 0.20f));
        DrawCircle(new Vector2(0f, -23f), 4f, flash);
        // Hexagonal base ΓÇö bright outer hex + dark inner hex
        DrawPolygon(RegularPoly(6, 12f, -Mathf.Pi / 6f), new[] { cyan });
        DrawPolygon(RegularPoly(6, 10f, -Mathf.Pi / 6f), new[] { dark });
        DrawCircle(Vector2.Zero, 3.5f, cyan);
    }

    private void DrawHeavyCannon()
    {
        var orange = new Color(1.00f, 0.55f, 0.00f);
        var dark   = new Color(0.10f, 0.04f, 0.00f);
        var rim    = new Color(1.00f, 0.82f, 0.30f);
        // Soft glow
        DrawCircle(Vector2.Zero, 24f, new Color(orange.R, orange.G, orange.B, 0.07f));
        DrawCircle(Vector2.Zero, 17f, new Color(orange.R, orange.G, orange.B, 0.14f));
        // Wide barrel ΓÇö bright outer, dark inner
        DrawRect(new Rect2(-6.5f, -19f, 13f, 15f), orange);
        DrawRect(new Rect2(-5.0f, -18f, 10f, 13f), dark);
        // Muzzle ring glow
        DrawRect(new Rect2(-5.0f, -22f, 10f, 4f), rim);
        DrawRect(new Rect2(-3.5f, -23f,  7f, 4f), new Color(rim.R, rim.G, rim.B, 0.30f));
        // Octagonal base ΓÇö bright outer, dark inner
        DrawPolygon(RegularPoly(8, 14f, 0f), new[] { orange });
        DrawPolygon(RegularPoly(8, 12f, 0f), new[] { dark });
        DrawCircle(Vector2.Zero, 4f, rim);
    }

    private void DrawMarkerTower()
    {
        var pink = new Color(1.00f, 0.15f, 0.60f);
        var dark = new Color(0.12f, 0.00f, 0.08f);
        var beam = new Color(0.90f, 0.70f, 1.00f);
        // Soft glow
        DrawCircle(Vector2.Zero, 20f, new Color(pink.R, pink.G, pink.B, 0.07f));
        DrawCircle(Vector2.Zero, 14f, new Color(pink.R, pink.G, pink.B, 0.14f));
        // Diamond body ΓÇö bright outer, dark inner
        DrawPolygon(new[] { new Vector2(0,-16), new Vector2(16,0), new Vector2(0,16), new Vector2(-16,0) }, new[] { pink });
        DrawPolygon(new[] { new Vector2(0,-13), new Vector2(13,0), new Vector2(0,13), new Vector2(-13,0) }, new[] { dark });
        // Antenna with glow
        DrawLine(new Vector2(0f, -13f), new Vector2(0f, -22f), beam, 2f);
        DrawCircle(new Vector2(0f, -22f), 8f, new Color(beam.R, beam.G, beam.B, 0.20f));
        DrawCircle(new Vector2(0f, -22f), 4f, beam);
        DrawCircle(new Vector2(0f, -22f), 1.5f, new Color(1f, 0.95f, 1f));
    }

    private void DrawChainTower()
    {
        var blue  = new Color(0.50f, 0.85f, 1.00f);
        var dark  = new Color(0.02f, 0.05f, 0.15f);
        var white = new Color(0.90f, 0.97f, 1.00f);
        // Soft glow
        DrawCircle(Vector2.Zero, 22f, new Color(blue.R, blue.G, blue.B, 0.07f));
        DrawCircle(Vector2.Zero, 15f, new Color(blue.R, blue.G, blue.B, 0.14f));
        // Circular base
        DrawCircle(Vector2.Zero, 11f, blue);
        DrawCircle(Vector2.Zero,  9f, dark);
        // Three discharge prongs (120° apart, pointing out from base)
        for (int i = 0; i < 3; i++)
        {
            float angle    = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
            var   dir      = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var   prongBase = dir * 9f;
            var   prongTip  = dir * 19f;
            DrawLine(prongBase, prongTip, blue, 3f);
            DrawCircle(prongTip, 4f, new Color(blue.R, blue.G, blue.B, 0.50f));
            DrawCircle(prongTip, 2.5f, white);
        }
        // Inner energy core
        DrawCircle(Vector2.Zero, 4f, new Color(blue.R, blue.G, blue.B, 0.55f));
        DrawCircle(Vector2.Zero, 2.5f, white);
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
