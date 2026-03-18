using System;
using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Horizontal circuit-bus drawn above the "SLOT THEORY" title.
/// Arms extend from each edge toward a central hub node, with animated
/// cyan energy pulses converging inward — referencing the surge meter's
/// "charging toward activation" state. The center node suggests the title
/// is the game's primary hub, connected to the surrounding system.
/// </summary>
internal partial class TitleArmDecor : Control
{
    private float _t = 0f;
    private static readonly Color Lime = new(0.651f, 0.839f, 0.031f);
    private static readonly Color Cyan = new(0.08f,  0.85f,  0.90f);

    public override void _Ready()
    {
        CustomMinimumSize    = new Vector2(0f, 16f);
        MouseFilter          = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal  = SizeFlags.ExpandFill;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w   = Size.X;
        float y   = Size.Y * 0.5f;
        float mid = w * 0.5f;

        // Arms reach from edges toward center, stopping short to leave a gap
        float armEnd = mid - 10f;

        // Horizontal arms (lime) — left and right of center
        DrawLine(new Vector2(4f, y),     new Vector2(armEnd, y),     new Color(Lime, 0.26f), 1f);
        DrawLine(new Vector2(w - 4f, y), new Vector2(w - armEnd, y), new Color(Lime, 0.26f), 1f);

        // Outer port terminals — the connection points at each edge
        DrawRect(new Rect2(4f - 3f,     y - 3f, 6f, 6f), new Color(Lime, 0.52f));
        DrawRect(new Rect2(w - 4f - 3f, y - 3f, 6f, 6f), new Color(Lime, 0.52f));

        // Central hub node — the title acts as the system's root node
        DrawRect(new Rect2(mid - 4.5f, y - 4.5f, 9f, 9f), new Color(Lime, 0.60f));
        DrawRect(new Rect2(mid - 2f,   y - 2f,   4f, 4f), new Color(Cyan, 0.78f));

        // Tick marks at quarter points along each arm
        float half = armEnd - 4f;
        float[] ticks = { 0.22f, 0.42f, 0.58f, 0.78f };
        foreach (float tp in ticks)
        {
            float tx = w * tp;
            // Only draw ticks that fall on an arm (skip center gap)
            if (MathF.Abs(tx - mid) > 12f)
                DrawLine(new Vector2(tx, y - 3f), new Vector2(tx, y + 3f),
                         new Color(Lime, 0.15f), 1f);
        }

        // Animated surge-charge pulses converging from edges toward the hub.
        // This references the surge meter charging toward activation.
        float phase = (_t * 0.44f) % 1.0f;
        float pa    = 0.38f + 0.28f * MathF.Sin(_t * 2.5f);
        float lpx   = 4f + phase * half;          // left pulse → center
        float rpx   = (w - 4f) - phase * half;    // right pulse → center
        DrawRect(new Rect2(lpx - 3f, y - 3f, 6f, 6f), new Color(Cyan, pa * 0.68f));
        DrawRect(new Rect2(rpx - 3f, y - 3f, 6f, 6f), new Color(Cyan, pa * 0.68f));
    }
}

/// <summary>
/// Row of 6 animated slot-indicator rectangles drawn below the subtitle.
/// This is the most Slot Theory-specific element: it directly visualizes
/// the 6 tower slots — the core structural mechanic of the game.
/// Each slot pulses in a staggered wave, and has a connector pip below
/// (referencing the modifier socket below each tower slot).
/// Visual language matches the slot geometry used on the map and in the draft.
/// </summary>
internal partial class SlotIndicatorRow : Control
{
    private float _t = 0f;
    private static readonly Color Lime = new(0.651f, 0.839f, 0.031f);
    private static readonly Color Cyan = new(0.08f,  0.85f,  0.90f);

    public override void _Ready()
    {
        CustomMinimumSize   = new Vector2(0f, 24f);
        MouseFilter         = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w  = Size.X;
        float cy = Size.Y * 0.5f;

        // 6 slots — matches the 6 tower slot count exactly
        const int   Slots  = 6;
        const float SlotW  = 26f;
        const float SlotH  = 12f;
        const float Gap    = 7f;
        float totalW  = Slots * SlotW + (Slots - 1) * Gap;
        float startX  = (w - totalW) * 0.5f;

        for (int i = 0; i < Slots; i++)
        {
            float sx = startX + i * (SlotW + Gap);
            float sy = cy - SlotH * 0.5f;

            // Staggered wave pulse — each slot has a phase offset so the
            // activation ripples across the row like a cascading surge fill
            float wave  = _t * 1.25f + i * 0.58f;
            float pulse = 0.5f + 0.5f * MathF.Sin(wave);

            float fillAlpha   = 0.04f + 0.05f * pulse;
            float borderAlpha = 0.20f + 0.22f * pulse;

            // Slot body
            DrawRect(new Rect2(sx, sy, SlotW, SlotH), new Color(Lime, fillAlpha));
            // Slot outline (drawn as four lines to control width precisely)
            DrawRect(new Rect2(sx, sy, SlotW, SlotH), new Color(Lime, borderAlpha), false, 1f);

            // Connector pip below — the modifier socket port beneath each slot
            float pipX = sx + SlotW * 0.5f;
            DrawLine(new Vector2(pipX, sy + SlotH),
                     new Vector2(pipX, sy + SlotH + 5f),
                     new Color(Cyan, 0.16f + 0.14f * pulse), 1f);
        }
    }
}
