using System;
using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Minimal circuit-bus above the title - two power rails converging to a central
/// hub node. Animation is slow and standby-level (not surging/charging).
/// The title reads as a component mounted on the hub, powered by the rails.
/// </summary>
internal partial class TitleArmDecor : Control
{
    private float _t = 0f;
    private static readonly Color Lime = new(0.651f, 0.839f, 0.031f);
    private static readonly Color Cyan = new(0.08f,  0.85f,  0.90f);

    public override void _Ready()
    {
        CustomMinimumSize   = new Vector2(0f, 16f);
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
        float w   = Size.X;
        float y   = Size.Y * 0.5f;
        float mid = w * 0.5f;
        float armEnd = mid - 10f;

        // Power rails (lime, low alpha)
        DrawLine(new Vector2(4f,     y), new Vector2(armEnd,     y), new Color(Lime, 0.20f), 1f);
        DrawLine(new Vector2(w - 4f, y), new Vector2(w - armEnd, y), new Color(Lime, 0.20f), 1f);

        // Outer port terminals
        DrawRect(new Rect2(1f,     y - 3f, 6f, 6f), new Color(Lime, 0.44f));
        DrawRect(new Rect2(w - 7f, y - 3f, 6f, 6f), new Color(Lime, 0.44f));

        // Central hub node
        DrawRect(new Rect2(mid - 4.5f, y - 4.5f, 9f, 9f), new Color(Lime, 0.50f));
        DrawRect(new Rect2(mid - 2f,   y - 2f,   4f, 4f), new Color(Cyan, 0.72f));

        // Very slow standby pulse - not "charging toward activation," just alive
        // Traversal period ≈ 8 seconds
        float phase = (_t * 0.40f) % 1.0f;
        float pa    = 0.28f + 0.22f * MathF.Sin(_t * 1.5f);
        float span  = armEnd - 4f;
        float lpx   = 4f + phase * span;
        float rpx   = (w - 4f) - phase * span;
        DrawRect(new Rect2(lpx - 2.5f, y - 2.5f, 5f, 5f), new Color(Cyan, pa * 0.60f));
        DrawRect(new Rect2(rpx - 2.5f, y - 2.5f, 5f, 5f), new Color(Cyan, pa * 0.60f));
    }
}

/// <summary>
/// Structural interface between the title zone and the reactor core (menu card).
/// Visualizes the 6 tower slots as a power-feed bus bar with downward feeder lines
/// - implying the card below receives charge through these connections.
///
/// Animation: slow standby breath on each slot (not a cascading surge fill),
/// plus a single charge sweep along the bus rail.
/// This is dormant-reactor state, not surge-activation state.
/// </summary>
internal partial class ReactorFeedBar : Control
{
    private float _t = 0f;
    private static readonly Color Lime = new(0.651f, 0.839f, 0.031f);
    private static readonly Color Cyan = new(0.08f,  0.85f,  0.90f);

    public override void _Ready()
    {
        CustomMinimumSize   = new Vector2(0f, 28f);
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
        float w = Size.X;
        float h = Size.Y;

        const int   Slots = 6;
        const float SlotW = 26f;
        const float SlotH = 11f;
        const float Gap   = 7f;
        const float FeedH = 8f;   // feeder line length below bus

        float totalW = Slots * SlotW + (Slots - 1) * Gap;
        float startX = (w - totalW) * 0.5f;
        float slotY  = h * 0.22f;
        float busY   = slotY + SlotH;
        float railL  = startX - 16f;
        float railR  = startX + totalW + 16f;

        // Horizontal bus rail - the spine feeding into the card below
        DrawLine(new Vector2(railL, busY), new Vector2(railR, busY), new Color(Lime, 0.26f), 1.5f);

        // Rail end caps (port terminals)
        DrawRect(new Rect2(railL - 3.5f, busY - 3.5f, 7f, 7f), new Color(Lime, 0.42f));
        DrawRect(new Rect2(railR - 3.5f, busY - 3.5f, 7f, 7f), new Color(Lime, 0.42f));

        // Single slow charge sweep along the bus - left → right, period ≈ 2.5s
        float busPhase = (_t * 0.40f) % 1.0f;
        float bpa = 0.24f + 0.18f * MathF.Sin(_t * 1.8f);
        float bpx = railL + busPhase * (railR - railL);
        DrawRect(new Rect2(bpx - 3.5f, busY - 3.5f, 7f, 7f), new Color(Cyan, bpa * 0.62f));

        for (int i = 0; i < Slots; i++)
        {
            float sx = startX + i * (SlotW + Gap);

            // Slow individual standby breath - period ≈ 9.7 seconds per slot,
            // offset so they're not in sync, but NOT a cascade surge ripple.
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 0.65f + i * 0.40f);

            float fillA   = 0.032f + 0.038f * pulse;
            float borderA = 0.14f  + 0.20f  * pulse;

            // Slot body
            DrawRect(new Rect2(sx, slotY, SlotW, SlotH), new Color(Lime, fillA));
            DrawRect(new Rect2(sx, slotY, SlotW, SlotH), new Color(Lime, borderA), false, 1f);

            // Downward feeder line - visible routing into the card below
            float feedX = sx + SlotW * 0.5f;
            float feedA = 0.10f + 0.13f * pulse;
            DrawLine(new Vector2(feedX, busY),
                     new Vector2(feedX, busY + FeedH),
                     new Color(Cyan, feedA), 1f);
            DrawRect(new Rect2(feedX - 1.5f, busY + FeedH - 1.5f, 3f, 3f),
                     new Color(Cyan, feedA * 0.75f));
        }
    }
}
