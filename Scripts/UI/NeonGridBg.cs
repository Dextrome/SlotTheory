using System;
using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Fullscreen animated background drawn behind the main menu.
/// Visual language is grounded entirely in Slot Theory's own systems:
///   - 8×5 map grid (MapGenerator.COLS=8, ROWS=5)
///   - Representative Serpent Grid snake path (3 horizontal legs)
///   - Energy pulses flowing along path legs (like enemies on a lane)
///   - Slot zone nodes at the 3×2 slot placement grid
///   - Two tower range rings (circular range-indicator motif)
///   - Lime/cyan palette from the game's own UI colors
/// All elements are at very low opacity — texture, not overlay.
/// </summary>
public partial class NeonGridBg : Control
{
    private float _t = 0f;

    // Game palette — matches UITheme exactly
    private static readonly Color LaneH  = new(0.651f, 0.839f, 0.031f); // lime  – horizontal grid / path
    private static readonly Color LaneV  = new(0.08f,  0.85f,  0.90f);  // cyan  – vertical grid columns
    private static readonly Color PathC  = new(0.80f,  1.00f,  0.20f);  // bright lime – snake path + pulses
    private static readonly Color NodeC  = new(0.08f,  0.85f,  0.90f);  // cyan  – slot zone nodes
    private static readonly Color RingC  = new(0.651f, 0.839f, 0.031f); // lime  – tower range rings

    // Representative Serpent Grid snake path in normalized 8×5 grid coords.
    // Mirrors the MapGenerator's 3-horizontal-leg snake with c1=5, row turns at row 2 and row 4.
    //   Leg 1 (right):  col 0→5, row 0
    //   Down:           col 5, row 0→2
    //   Leg 2 (left):   col 5→1, row 2
    //   Down:           col 1, row 2→4
    //   Leg 3 (right):  col 1→8, row 4
    private static readonly (float C, float R)[] PathWaypoints =
    {
        (0f, 0f), (5f, 0f),   // Leg 1
        (5f, 2f),              // Turn down
        (1f, 2f),              // Leg 2
        (1f, 4f),              // Turn down
        (8f, 4f),              // Leg 3 exit
    };

    // Slot zone centers in 8×5 grid coords (3 cols × 2 rows of zones).
    // These match where slots are placed in each zone (slightly inset from zone edge,
    // preferring cells adjacent to the path).
    private static readonly (float C, float R)[] SlotZones =
    {
        (1.5f, 1f), (4f, 1f), (6.5f, 1f),   // top row of zones
        (1.5f, 3f), (4f, 3f), (6.5f, 3f),   // bottom row of zones
    };

    // Which horizontal path legs carry energy pulses (leg index 0, 2, 4 = h-legs)
    private static readonly int[] PulseLegIndices = { 0, 2, 4 };

    public override void _Process(double delta)
    {
        _t += (float)delta * 0.10f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var sz = GetViewportRect().Size;
        float w = sz.X, h = sz.Y;

        // ── Helper: convert 8×5 grid coord → screen position ────────────────
        Vector2 ToScreen(float col, float row) => new(col / 8f * w, row / 5f * h);

        // ── Tower range rings at two slot zone positions ──────────────────────
        // Placed at top-left zone and bottom-right zone for visual balance.
        // This is exactly the range-circle motif drawn around towers in gameplay.
        float ringPulse1 = 0.008f + 0.004f * MathF.Sin(_t * 0.25f);
        float ringPulse2 = 0.007f + 0.004f * MathF.Sin(_t * 0.20f + 1.4f);
        var ringCenter1 = ToScreen(SlotZones[0].C, SlotZones[0].R); // top-left zone
        var ringCenter2 = ToScreen(SlotZones[5].C, SlotZones[5].R); // bottom-right zone
        // Outer ring (large — tower max range suggestion)
        DrawArc(ringCenter1, h * 0.27f, 0f, MathF.Tau, 72, new Color(RingC, ringPulse1), 1f);
        DrawArc(ringCenter2, h * 0.22f, 0f, MathF.Tau, 72, new Color(RingC, ringPulse2), 1f);
        // Inner concentric ring (like the 10% opacity range circle on towers)
        DrawArc(ringCenter1, h * 0.15f, 0f, MathF.Tau, 48, new Color(LaneV, ringPulse1 * 0.55f), 1f);
        DrawArc(ringCenter2, h * 0.12f, 0f, MathF.Tau, 48, new Color(LaneV, ringPulse2 * 0.50f), 1f);

        // ── 8×5 map grid lines ───────────────────────────────────────────────
        // Same proportions as MapGenerator.COLS=8, ROWS=5.
        // Horizontal = lime (map grass color), vertical = cyan.
        const int GridCols = 8;
        const int GridRows = 5;
        for (int row = 0; row <= GridRows; row++)
        {
            float y = h * row / GridRows;
            float a = 0.032f + 0.016f * MathF.Sin(_t * 0.70f + row * 0.80f);
            DrawLine(new Vector2(0, y), new Vector2(w, y), new Color(LaneH, a), 1f);
        }
        for (int col = 0; col <= GridCols; col++)
        {
            float x = w * col / GridCols;
            float a = 0.025f + 0.012f * MathF.Sin(_t * 0.55f + col * 0.60f);
            DrawLine(new Vector2(x, 0), new Vector2(x, h), new Color(LaneV, a), 1f);
        }

        // ── Ghost snake path ─────────────────────────────────────────────────
        // A representative Serpent Grid path — the same 3-horizontal-leg snake
        // the map generator produces. Drawn as a faint bright line.
        float pathAlpha = 0.060f + 0.020f * MathF.Sin(_t * 0.30f);
        for (int i = 0; i < PathWaypoints.Length - 1; i++)
        {
            var a = ToScreen(PathWaypoints[i].C, PathWaypoints[i].R);
            var b = ToScreen(PathWaypoints[i + 1].C, PathWaypoints[i + 1].R);
            DrawLine(a, b, new Color(PathC, pathAlpha), 1.5f);
        }

        // ── Energy pulses along horizontal path legs ─────────────────────────
        // Bright packets travel along the horizontal legs of the snake path,
        // mimicking enemy movement on the lane. One pulse per horizontal leg,
        // each with its own offset so they don't move in sync.
        foreach (int legIdx in PulseLegIndices)
        {
            if (legIdx >= PathWaypoints.Length - 1) continue;
            var legStart = ToScreen(PathWaypoints[legIdx].C, PathWaypoints[legIdx].R);
            var legEnd   = ToScreen(PathWaypoints[legIdx + 1].C, PathWaypoints[legIdx + 1].R);
            // Only pulse along horizontal segments (same row)
            if (MathF.Abs(legStart.Y - legEnd.Y) > 2f) continue;

            float phase  = legIdx * 1.618f; // golden ratio offset per leg
            float xNorm  = ((_t * 0.18f + phase) % 1.0f + 1.0f) % 1.0f;
            float lx     = legStart.X + xNorm * (legEnd.X - legStart.X);
            float ly     = legStart.Y;

            // Direction: left or right
            bool goingLeft = legEnd.X < legStart.X;
            float dx = goingLeft ? -1f : 1f;

            // Soft tail → bright core (enemy-packet feel)
            DrawLine(new Vector2(lx - dx * 24f, ly), new Vector2(lx, ly), new Color(PathC, 0.030f), 2f);
            DrawLine(new Vector2(lx - dx * 8f,  ly), new Vector2(lx, ly), new Color(PathC, 0.075f), 2f);
            DrawRect(new Rect2(lx - 3f, ly - 2f, 6f, 4f), new Color(PathC, 0.150f));
        }

        // ── Slot zone node markers ───────────────────────────────────────────
        // Small squares at the 6 slot zone centers, showing where tower slots live.
        // Pulse individually like powered nodes in the system.
        for (int i = 0; i < SlotZones.Length; i++)
        {
            var sp = ToScreen(SlotZones[i].C, SlotZones[i].R);
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 1.10f + i * 0.85f);
            float a = 0.045f + 0.035f * pulse;
            DrawRect(new Rect2(sp.X - 3f, sp.Y - 3f, 6f, 6f), new Color(NodeC, a));
            // Small cross-hair tick (like a slot position indicator)
            float tickA = 0.025f + 0.015f * pulse;
            DrawLine(new Vector2(sp.X - 8f, sp.Y), new Vector2(sp.X + 8f, sp.Y), new Color(NodeC, tickA), 1f);
            DrawLine(new Vector2(sp.X, sp.Y - 8f), new Vector2(sp.X, sp.Y + 8f), new Color(NodeC, tickA), 1f);
        }

        // ── Scan sweep (lime tint — Pressure-archetype feel) ─────────────────
        // The slow downward scan gives a "sustained pressure" ambient quality,
        // referencing the Pressure feel-class from SurgeDifferentiation.
        float scanY = ((_t * 0.28f % 1f) * (h + 140f)) - 70f;
        DrawRect(new Rect2(0, scanY - 35f, w, 70f), new Color(LaneH, 0.014f));
        DrawRect(new Rect2(0, scanY - 10f, w, 20f), new Color(LaneH, 0.020f));
    }
}
