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
        if (AttackInterval > 0f) QueueRedraw();
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
        var body   = new Color(0.15f, 0.65f, 1.00f);
        var dark   = new Color(0.05f, 0.30f, 0.70f);
        var muzzle = new Color(0.80f, 0.95f, 1.00f);
        // Barrel pointing up
        DrawRect(new Rect2(-2.5f, -22f, 5f, 16f), dark);
        DrawCircle(new Vector2(0f, -22f), 3f, muzzle);
        // Hexagonal base
        DrawPolygon(RegularPoly(6, 10f, -Mathf.Pi / 6f), new[] { body });
        DrawCircle(Vector2.Zero, 5f, dark);
    }

    private void DrawHeavyCannon()
    {
        var body = new Color(0.10f, 0.20f, 0.80f);
        var dark = new Color(0.05f, 0.10f, 0.50f);
        var rim  = new Color(0.30f, 0.45f, 1.00f);
        // Wide short barrel
        DrawRect(new Rect2(-5.5f, -18f, 11f, 14f), dark);
        DrawRect(new Rect2(-4f, -21f, 8f, 4f), rim);   // muzzle ring
        // Octagonal body
        DrawPolygon(RegularPoly(8, 12f, 0f), new[] { body });
        DrawCircle(Vector2.Zero, 6f, rim);
    }

    private void DrawMarkerTower()
    {
        var body = new Color(0.55f, 0.20f, 0.92f);
        var dark = new Color(0.30f, 0.08f, 0.60f);
        var beam = new Color(0.80f, 0.60f, 1.00f);
        // Diamond outline then fill
        DrawPolygon(new[] { new Vector2(0,-14), new Vector2(14,0), new Vector2(0,14), new Vector2(-14,0) }, new[] { dark });
        DrawPolygon(new[] { new Vector2(0,-12), new Vector2(12,0), new Vector2(0,12), new Vector2(-12,0) }, new[] { body });
        // Antenna
        DrawLine(new Vector2(0f, -12f), new Vector2(0f, -21f), beam, 2f);
        DrawCircle(new Vector2(0f, -21f), 3.5f, beam);
        DrawCircle(new Vector2(0f, -21f), 1.5f, new Color(1f, 0.9f, 1f));
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
