using System;
using Godot;

namespace SlotTheory.Entities;

/// <summary>
/// Procedural visual for a Wildfire fire trail segment.
/// Spawned by CombatSim when a burning enemy deposits a trail node.
/// Oriented along the enemy's movement direction so adjacent segments
/// blend into a continuous streak of fire along the lane.
///
/// Each segment is an elongated burn scar on the ground (~50 px along direction)
/// with flame tongues rising straight up from points distributed along it.
/// Multiple segments dropped every 0.65 s overlap visually to form the trail.
///
/// Purely cosmetic -- gameplay logic lives in CombatSim.FireTrailSegment.
/// </summary>
public partial class WildfireTrailVfx : Node2D
{
    private float _totalLifetime;
    private float _lifetimeFraction = 1f;
    private float _age;
    private Vector2 _dir = Vector2.Right;

    public void Initialize(float lifetime, Vector2 direction)
    {
        _totalLifetime = lifetime;
        _lifetimeFraction = 1f;
        _age = 0f;
        _dir = direction.IsZeroApprox() ? Vector2.Right : direction.Normalized();
        ZIndex = 8; // below enemies (10) and mines (9)
    }

    public void SetLifetimeFraction(float fraction)
    {
        _lifetimeFraction = Mathf.Clamp(fraction, 0f, 1f);
        _age = _totalLifetime * (1f - _lifetimeFraction);
        QueueRedraw();
    }

    public void ExpireAndFree() => QueueFree();

    public override void _Draw()
    {
        if (_lifetimeFraction <= 0f) return;

        // Alpha envelope: fade in first 8%, hold, fade out last 25%
        float alpha;
        if (_lifetimeFraction > 0.92f)
            alpha = (1f - _lifetimeFraction) / 0.08f;
        else if (_lifetimeFraction < 0.25f)
            alpha = _lifetimeFraction / 0.25f;
        else
            alpha = 1f;

        alpha *= 0.50f + 0.50f * _lifetimeFraction; // additional dim as trail ages

        if (alpha < 0.02f) return;

        Vector2 dir  = _dir;
        Vector2 perp = new Vector2(-dir.Y, dir.X); // perpendicular (width axis)

        // ── 1. Ground burn scar ───────────────────────────────────────────────
        // Elongated dark base: 7 overlapping circles distributed along dir (-24→+24 px)
        for (int i = -3; i <= 3; i++)
        {
            float t      = i / 3f;                         // -1 → +1
            Vector2 p    = dir * (i * 8f);
            float r      = 9f - MathF.Abs(t) * 3.5f;      // bigger in the center
            float darken = 1f - MathF.Abs(t) * 0.35f;

            DrawCircle(p, r + 1f, new Color(0.45f * darken, 0.05f * darken, 0.01f, alpha * 0.70f));
            DrawCircle(p, r * 0.65f, new Color(0.80f * darken, 0.16f * darken, 0.02f, alpha * 0.65f));
        }

        // Bright amber hotspot at center
        DrawCircle(Vector2.Zero, 5f, new Color(1.0f, 0.42f, 0.07f, alpha * 0.72f));
        DrawCircle(Vector2.Zero, 2.5f, new Color(1.0f, 0.72f, 0.28f, alpha * 0.82f));

        // ── 2. Flame tongues ─────────────────────────────────────────────────
        // 5 flames along the streak; center tallest, ends shortest.
        // They point straight up (−Y) regardless of movement direction --
        // fire rises against gravity, not along travel.

        // (offset along dir, height, width, phase)
        DrawFlameTongue(dir * -20f, height: 15f, width: 4.5f, phase: 0.0f,  alpha);
        DrawFlameTongue(dir * -10f, height: 22f, width: 5.5f, phase: 1.1f,  alpha);
        DrawFlameTongue(dir *   0f, height: 28f, width: 6.0f, phase: 2.4f,  alpha); // center / tallest
        DrawFlameTongue(dir *  10f, height: 22f, width: 5.5f, phase: 0.7f,  alpha);
        DrawFlameTongue(dir *  20f, height: 15f, width: 4.5f, phase: 1.85f, alpha);

        // ── 3. Center highlight ───────────────────────────────────────────────
        DrawCircle(new Vector2(0f, -7f), 3f, new Color(1.0f, 0.90f, 0.55f, alpha * 0.78f));
    }

    /// <summary>
    /// Draws a flame tongue as two layered polygons pointing straight up (−Y).
    /// Outer layer: deep orange-red, transparent at tip.
    /// Inner layer: hot amber-yellow, narrower and slightly shorter.
    /// Sways and breathes with <see cref="_age"/> + <paramref name="phase"/>.
    /// </summary>
    private void DrawFlameTongue(Vector2 basePos, float height, float width, float phase, float alpha)
    {
        float sway      = Mathf.Sin(_age * 4.5f + phase) * 3.5f;
        float heightMod = 0.82f + 0.18f * Mathf.Sin(_age * 6.1f + phase + 1.0f);
        float h         = height * heightMod * _lifetimeFraction;
        if (h < 1f) return;

        // Outer flame (5-point polygon: base-left, mid-left, tip, mid-right, base-right)
        var tip = new Vector2(basePos.X + sway,              basePos.Y - h);
        var bl  = new Vector2(basePos.X - width,             basePos.Y);
        var br  = new Vector2(basePos.X + width,             basePos.Y);
        var ml  = new Vector2(basePos.X - width * 0.45f + sway * 0.45f, basePos.Y - h * 0.50f);
        var mr  = new Vector2(basePos.X + width * 0.45f + sway * 0.45f, basePos.Y - h * 0.50f);

        DrawPolygon(
            new[] { bl, ml, tip, mr, br },
            new[]
            {
                new Color(0.82f, 0.18f, 0.02f, alpha * 0.82f),
                new Color(0.95f, 0.32f, 0.04f, alpha * 0.72f),
                new Color(0.90f, 0.38f, 0.05f, alpha * 0.08f), // tip -- near-transparent
                new Color(0.95f, 0.32f, 0.04f, alpha * 0.72f),
                new Color(0.82f, 0.18f, 0.02f, alpha * 0.82f),
            });

        // Inner flame -- narrower, shorter, hotter color
        float iw    = width * 0.52f;
        float ih    = h * 0.82f;
        float isway = sway * 0.75f;
        var itip = new Vector2(basePos.X + isway,                          basePos.Y - ih);
        var ibl  = new Vector2(basePos.X - iw,                             basePos.Y);
        var ibr  = new Vector2(basePos.X + iw,                             basePos.Y);
        var iml  = new Vector2(basePos.X - iw * 0.45f + isway * 0.40f,   basePos.Y - ih * 0.48f);
        var imr  = new Vector2(basePos.X + iw * 0.45f + isway * 0.40f,   basePos.Y - ih * 0.48f);

        DrawPolygon(
            new[] { ibl, iml, itip, imr, ibr },
            new[]
            {
                new Color(1.0f, 0.58f, 0.10f, alpha * 0.84f),
                new Color(1.0f, 0.74f, 0.20f, alpha * 0.76f),
                new Color(1.0f, 0.92f, 0.62f, alpha * 0.15f), // tip -- near-white, transparent
                new Color(1.0f, 0.74f, 0.20f, alpha * 0.76f),
                new Color(1.0f, 0.58f, 0.10f, alpha * 0.84f),
            });
    }
}
