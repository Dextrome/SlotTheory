using System;
using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Fullscreen animated circuit-board background for the main menu.
/// PCB infrastructure in dormant standby - crisp traces, tight annular pads,
/// component footprints, via holes. Atmosphere from corner vignette and a soft
/// central light pocket, not from density or noise.
/// </summary>
public partial class NeonGridBg : Control
{
    private float _t = 0f;

    private static readonly Color TraceC = new(0.09f, 0.58f, 0.68f);
    private static readonly Color Orange = new(1.00f, 0.52f, 0.05f);
    private static readonly Color Cyan   = new(0.08f, 0.85f, 0.90f);
    private static readonly Color Lime   = new(0.651f, 0.839f, 0.031f);

    // ── Traces (weight: 0=primary 2px, 1=secondary 1.5px, 2=stub 1px) ────────
    private static readonly (float X1, float Y1, float X2, float Y2, int W)[] Traces =
    {
        // PRIMARY TRUNKS
        (0.13f, 0.00f, 0.13f, 1.00f, 0),
        (0.87f, 0.00f, 0.87f, 1.00f, 0),
        (0.00f, 0.14f, 0.20f, 0.14f, 0),
        (0.80f, 0.14f, 1.00f, 0.14f, 0),
        (0.00f, 0.38f, 0.13f, 0.38f, 0),
        (0.87f, 0.40f, 1.00f, 0.40f, 0),
        (0.00f, 0.62f, 0.18f, 0.62f, 0),
        (0.82f, 0.62f, 1.00f, 0.62f, 0),
        (0.00f, 0.85f, 0.13f, 0.85f, 0),
        (0.87f, 0.85f, 1.00f, 0.85f, 0),
        (0.13f, 0.48f, 0.27f, 0.48f, 0),
        (0.73f, 0.48f, 0.87f, 0.48f, 0),
        (0.00f, 0.06f, 0.34f, 0.06f, 0),
        (0.66f, 0.06f, 1.00f, 0.06f, 0),
        (0.33f, 0.00f, 0.33f, 0.16f, 0),
        (0.67f, 0.00f, 0.67f, 0.16f, 0),
        (0.00f, 0.92f, 0.42f, 0.92f, 0),
        (0.58f, 0.92f, 1.00f, 0.92f, 0),
        (0.42f, 0.92f, 0.42f, 1.00f, 0),
        (0.58f, 0.92f, 0.58f, 1.00f, 0),
        // SECONDARY COLUMNS
        (0.06f, 0.14f, 0.06f, 0.65f, 1),
        (0.94f, 0.14f, 0.94f, 0.65f, 1),
        (0.04f, 0.38f, 0.04f, 0.62f, 1),
        (0.96f, 0.38f, 0.96f, 0.62f, 1),
        // LEFT PANEL ROUTING
        (0.00f, 0.25f, 0.06f, 0.25f, 1),
        (0.00f, 0.30f, 0.13f, 0.30f, 1),
        (0.06f, 0.44f, 0.13f, 0.44f, 1),
        (0.00f, 0.50f, 0.06f, 0.50f, 1),
        (0.00f, 0.55f, 0.13f, 0.55f, 1),
        (0.06f, 0.65f, 0.13f, 0.65f, 1),
        (0.00f, 0.72f, 0.13f, 0.72f, 1),
        (0.00f, 0.78f, 0.06f, 0.78f, 1),
        (0.13f, 0.14f, 0.34f, 0.14f, 1),
        (0.13f, 0.76f, 0.06f, 0.76f, 1),
        (0.00f, 0.13f, 0.13f, 0.13f, 2),
        // RIGHT PANEL ROUTING
        (0.94f, 0.25f, 1.00f, 0.25f, 1),
        (0.87f, 0.30f, 1.00f, 0.30f, 1),
        (0.87f, 0.44f, 0.94f, 0.44f, 1),
        (0.94f, 0.50f, 1.00f, 0.50f, 1),
        (0.87f, 0.55f, 1.00f, 0.55f, 1),
        (0.87f, 0.65f, 0.94f, 0.65f, 1),
        (0.87f, 0.72f, 1.00f, 0.72f, 1),
        (0.94f, 0.78f, 1.00f, 0.78f, 1),
        (0.66f, 0.14f, 0.87f, 0.14f, 1),
        (0.87f, 0.76f, 0.94f, 0.76f, 1),
        (0.87f, 0.13f, 1.00f, 0.13f, 2),
        // COLUMN CONNECTOR BRIDGES
        (0.06f, 0.38f, 0.13f, 0.38f, 2),
        (0.06f, 0.50f, 0.13f, 0.50f, 2),
        (0.04f, 0.44f, 0.06f, 0.44f, 2),
        (0.04f, 0.55f, 0.06f, 0.55f, 2),
        (0.20f, 0.14f, 0.20f, 0.24f, 1),
        (0.20f, 0.24f, 0.27f, 0.24f, 2),
        (0.87f, 0.50f, 0.94f, 0.50f, 2),
        (0.96f, 0.44f, 0.94f, 0.44f, 2),
        (0.96f, 0.55f, 0.94f, 0.55f, 2),
        (0.80f, 0.14f, 0.80f, 0.24f, 1),
        (0.80f, 0.24f, 0.73f, 0.24f, 2),
        // INNER FEED ROUTING
        (0.27f, 0.48f, 0.27f, 0.62f, 1),
        (0.27f, 0.62f, 0.38f, 0.62f, 1),
        (0.38f, 0.62f, 0.38f, 0.48f, 2),
        (0.73f, 0.48f, 0.73f, 0.62f, 1),
        (0.73f, 0.62f, 0.62f, 0.62f, 1),
        (0.62f, 0.62f, 0.62f, 0.48f, 2),
        (0.27f, 0.35f, 0.38f, 0.35f, 2),
        (0.62f, 0.35f, 0.73f, 0.35f, 2),
        (0.13f, 0.54f, 0.27f, 0.54f, 2),
        (0.73f, 0.54f, 0.87f, 0.54f, 2),
        // CENTER ROUTING (above card)
        (0.27f, 0.28f, 0.38f, 0.28f, 2),
        (0.62f, 0.28f, 0.73f, 0.28f, 2),
        (0.27f, 0.32f, 0.50f, 0.32f, 2),
        (0.50f, 0.28f, 0.73f, 0.28f, 2),
        (0.38f, 0.28f, 0.38f, 0.35f, 2),
        (0.62f, 0.28f, 0.62f, 0.35f, 2),
        (0.50f, 0.24f, 0.50f, 0.32f, 2),
        // CENTER ROUTING (below card)
        (0.27f, 0.74f, 0.38f, 0.74f, 2),
        (0.62f, 0.74f, 0.73f, 0.74f, 2),
        (0.38f, 0.74f, 0.38f, 0.62f, 2),
        (0.62f, 0.74f, 0.62f, 0.62f, 2),
        (0.27f, 0.78f, 0.50f, 0.78f, 2),
        (0.50f, 0.78f, 0.73f, 0.78f, 2),
        // CROSS-ROUTES
        (0.13f, 0.70f, 0.27f, 0.70f, 1),
        (0.87f, 0.70f, 0.73f, 0.70f, 1),
        (0.27f, 0.70f, 0.27f, 0.62f, 2),
        (0.73f, 0.70f, 0.73f, 0.62f, 2),
        // FOOTER EXTRAS
        (0.42f, 0.85f, 0.42f, 0.92f, 2),
        (0.58f, 0.85f, 0.58f, 0.92f, 2),
        (0.00f, 0.91f, 0.13f, 0.91f, 2),
        (0.87f, 0.91f, 1.00f, 0.91f, 2),
        // DIAGONAL STUBS
        (0.13f, 0.14f, 0.16f, 0.11f, 2),
        (0.87f, 0.14f, 0.84f, 0.11f, 2),
        (0.13f, 0.62f, 0.10f, 0.59f, 2),
        (0.87f, 0.62f, 0.90f, 0.59f, 2),
        (0.33f, 0.06f, 0.30f, 0.03f, 2),
        (0.67f, 0.06f, 0.70f, 0.03f, 2),
        (0.13f, 0.38f, 0.10f, 0.35f, 2),
        (0.87f, 0.40f, 0.90f, 0.37f, 2),
        (0.06f, 0.25f, 0.03f, 0.22f, 2),
        (0.94f, 0.25f, 0.97f, 0.22f, 2),
        (0.20f, 0.24f, 0.23f, 0.27f, 2),
        (0.80f, 0.24f, 0.77f, 0.27f, 2),
        (0.38f, 0.28f, 0.35f, 0.25f, 2),
        (0.62f, 0.28f, 0.65f, 0.25f, 2),
    };

    // ── Nodes (annular ring + bright core - no wide soft halos) ──────────────
    private static readonly (float X, float Y, int C, float R)[] Nodes =
    {
        // Orange hot nodes
        (0.13f, 0.14f, 0, 8f), (0.87f, 0.14f, 0, 9f),
        (0.13f, 0.62f, 0, 7f), (0.87f, 0.62f, 0, 8f),
        (0.00f, 0.38f, 0, 6f), (0.13f, 0.38f, 0, 5f),
        (1.00f, 0.40f, 0, 6f), (0.87f, 0.40f, 0, 5f),
        (0.13f, 0.30f, 0, 4f), (0.87f, 0.30f, 0, 4f),
        // Cyan routing nodes
        (0.27f, 0.48f, 1, 6f), (0.73f, 0.48f, 1, 6f),
        (0.33f, 0.06f, 1, 5f), (0.67f, 0.06f, 1, 5f),
        (0.06f, 0.38f, 1, 4f), (0.94f, 0.40f, 1, 4f),
        (0.20f, 0.14f, 1, 3f), (0.80f, 0.14f, 1, 3f),
        (0.38f, 0.62f, 1, 4f), (0.62f, 0.62f, 1, 4f),
        (0.27f, 0.35f, 1, 3f), (0.73f, 0.35f, 1, 3f),
        (0.50f, 0.28f, 1, 3f), (0.50f, 0.78f, 1, 3f),
        // Lime passive nodes (square pad shape)
        (0.13f, 0.85f, 2, 4f), (0.87f, 0.85f, 2, 4f),
        (0.42f, 0.92f, 2, 3f), (0.58f, 0.92f, 2, 3f),
        (0.06f, 0.25f, 2, 3f), (0.94f, 0.25f, 2, 3f),
        (0.27f, 0.62f, 2, 3f), (0.73f, 0.62f, 2, 3f),
        (0.06f, 0.65f, 2, 3f), (0.94f, 0.65f, 2, 3f),
        (0.38f, 0.74f, 2, 3f), (0.62f, 0.74f, 2, 3f),
    };

    // ── Via holes ─────────────────────────────────────────────────────────────
    private static readonly (float X, float Y, float R)[] Vias =
    {
        (0.13f, 0.48f, 4.5f), (0.87f, 0.48f, 4.5f),
        (0.33f, 0.16f, 3.5f), (0.67f, 0.16f, 3.5f),
        (0.06f, 0.62f, 3.0f), (0.94f, 0.62f, 3.0f),
        (0.06f, 0.50f, 2.8f), (0.94f, 0.50f, 2.8f),
        (0.20f, 0.24f, 2.8f), (0.80f, 0.24f, 2.8f),
        (0.42f, 0.85f, 2.5f), (0.58f, 0.85f, 2.5f),
        (0.38f, 0.35f, 2.5f), (0.62f, 0.35f, 2.5f),
        (0.50f, 0.24f, 2.5f),
    };

    // ── Junction pads (2×2 squares at bends / termini) ────────────────────────
    private static readonly (float X, float Y)[] JunctionPads =
    {
        (0.13f, 0.25f), (0.13f, 0.30f), (0.13f, 0.44f), (0.13f, 0.50f),
        (0.13f, 0.54f), (0.13f, 0.55f), (0.13f, 0.65f), (0.13f, 0.70f),
        (0.13f, 0.72f), (0.13f, 0.76f),
        (0.06f, 0.44f), (0.06f, 0.50f), (0.06f, 0.55f), (0.06f, 0.65f), (0.06f, 0.78f),
        (0.04f, 0.44f), (0.04f, 0.55f),
        (0.00f, 0.25f), (0.00f, 0.30f), (0.00f, 0.50f), (0.00f, 0.55f),
        (0.00f, 0.72f), (0.00f, 0.78f), (0.00f, 0.62f), (0.00f, 0.85f),
        (0.20f, 0.14f), (0.27f, 0.24f), (0.27f, 0.70f),
        (0.16f, 0.11f), (0.10f, 0.59f), (0.10f, 0.35f),
        (0.23f, 0.27f), (0.03f, 0.22f), (0.03f, 0.50f),
        (0.38f, 0.48f), (0.38f, 0.28f), (0.50f, 0.32f),
        (0.62f, 0.48f), (0.62f, 0.28f),
        (0.87f, 0.25f), (0.87f, 0.30f), (0.87f, 0.44f), (0.87f, 0.50f),
        (0.87f, 0.54f), (0.87f, 0.55f), (0.87f, 0.65f), (0.87f, 0.70f),
        (0.87f, 0.72f), (0.87f, 0.76f),
        (0.94f, 0.44f), (0.94f, 0.50f), (0.94f, 0.55f), (0.94f, 0.65f), (0.94f, 0.78f),
        (0.96f, 0.44f), (0.96f, 0.55f),
        (1.00f, 0.25f), (1.00f, 0.30f), (1.00f, 0.50f), (1.00f, 0.55f),
        (1.00f, 0.72f), (1.00f, 0.78f), (1.00f, 0.62f), (1.00f, 0.85f),
        (0.80f, 0.14f), (0.73f, 0.24f), (0.73f, 0.70f),
        (0.84f, 0.11f), (0.90f, 0.59f), (0.90f, 0.37f),
        (0.77f, 0.27f), (0.97f, 0.22f), (0.97f, 0.50f),
        (0.33f, 0.00f), (0.67f, 0.00f), (0.34f, 0.06f), (0.66f, 0.06f),
        (0.30f, 0.03f), (0.70f, 0.03f), (0.00f, 0.13f), (1.00f, 0.13f),
        (0.42f, 1.00f), (0.58f, 1.00f),
        (0.00f, 0.91f), (0.13f, 0.91f), (0.87f, 0.91f), (1.00f, 0.91f),
        (0.38f, 0.35f), (0.62f, 0.35f),
        (0.27f, 0.28f), (0.73f, 0.28f), (0.50f, 0.24f), (0.27f, 0.78f), (0.73f, 0.78f),
    };

    // ── SMD component footprints ───────────────────────────────────────────────
    private static readonly (float CX, float CY, float HW, float HH, int Pads)[] Components =
    {
        (0.055f, 0.305f, 0.030f, 0.038f, 4),
        (0.055f, 0.710f, 0.038f, 0.024f, 3),
        (0.945f, 0.305f, 0.030f, 0.038f, 4),
        (0.945f, 0.710f, 0.038f, 0.024f, 3),
        (0.185f, 0.145f, 0.018f, 0.016f, 2),
        (0.815f, 0.145f, 0.018f, 0.016f, 2),
        (0.020f, 0.680f, 0.016f, 0.026f, 3),
        (0.980f, 0.680f, 0.016f, 0.026f, 3),
        (0.020f, 0.500f, 0.012f, 0.016f, 2),
        (0.980f, 0.500f, 0.012f, 0.016f, 2),
    };

    // Reduced packet routes so the board reads quieter at a glance.
    private static readonly int[] PulseTraces = { 0, 6, 1, 7 };

    public override void _Process(double delta)
    {
        _t += (float)delta * 0.10f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var sz = GetViewportRect().Size;
        float w = sz.X, h = sz.Y;
        Vector2 S(float nx, float ny) => new(nx * w, ny * h);

        // ── Corner vignette ───────────────────────────────────────────────────
        // Four dark circles at screen corners pull the eye inward toward the
        // central panel. This is the primary source of atmospheric depth.
        DrawRect(new Rect2(0f, 0f, w * 0.08f, h), new Color(0f, 0f, 0f, 0.038f));
        DrawRect(new Rect2(w * 0.92f, 0f, w * 0.08f, h), new Color(0f, 0f, 0f, 0.038f));
        DrawRect(new Rect2(0f, 0f, w, h * 0.05f), new Color(0f, 0f, 0f, 0.05f));
        DrawRect(new Rect2(0f, h * 0.95f, w, h * 0.05f), new Color(0f, 0f, 0f, 0.06f));

        // ── Central atmospheric light ─────────────────────────────────────────
        // A faint blue-indigo light pocket behind the menu card area.
        // Creates the impression the board is powered and the card is lit
        // from within - not a visible glow, just a warmth/depth separation.
        var focus = S(0.50f, 0.50f);
        for (int i = 0; i < 38; i++)
        {
            float t = i + 1f;
            float hx = Hash01(t * 3.11f + 0.7f);
            float hy = Hash01(t * 4.93f + 1.9f);
            float hr = Hash01(t * 7.73f + 2.3f);
            var cp = new Vector2(
                focus.X + (hx - 0.5f) * w * 0.40f,
                focus.Y + (hy - 0.5f) * h * 0.24f);
            float radius = w * (0.010f + hr * 0.026f);
            float alpha = 0.003f + hr * 0.007f;
            Color c = hr > 0.72f
                ? new Color(0.12f, 0.34f, 0.46f, alpha)
                : new Color(0.04f, 0.14f, 0.22f, alpha);
            DrawCircle(cp, radius, c);
        }
        for (int i = 0; i < 98; i++)
        {
            float a = Hash01(i * 9.31f + 0.6f);
            float b = Hash01(i * 7.87f + 2.2f);
            float c = Hash01(i * 5.61f + 1.4f);
            var p = new Vector2(
                w * 0.34f + (a - 0.5f) * w * 0.24f,
                h * 0.62f + (b - 0.5f) * h * 0.24f);
            float r = w * (0.004f + c * 0.014f);
            float aa = 0.003f + c * 0.009f;
            DrawCircle(p, r, new Color(0.08f, 0.42f, 0.36f, aa));
        }
        for (int i = 0; i < 64; i++)
        {
            float a = Hash01(i * 6.11f + 0.4f);
            float b = Hash01(i * 4.87f + 1.6f);
            float c = Hash01(i * 8.61f + 2.4f);
            var p = new Vector2(
                w * 0.63f + (a - 0.5f) * w * 0.18f,
                h * 0.50f + (b - 0.5f) * h * 0.18f);
            float r = w * (0.0035f + c * 0.012f);
            float aa = 0.0025f + c * 0.007f;
            DrawCircle(p, r, new Color(0.06f, 0.30f, 0.45f, aa));
        }
        DrawRect(new Rect2(0f, 0f, w, h), new Color(0f, 0.01f, 0.02f, 0.12f));
        DrawCircle(S(0.40f, 0.58f), w * 0.034f, new Color(0.24f, 0.92f, 1.0f, 0.12f));
        DrawCircle(S(0.60f, 0.58f), w * 0.034f, new Color(0.56f, 0.54f, 1.0f, 0.12f));
        DrawCircle(S(0.40f, 0.58f), w * 0.020f, new Color(0.72f, 1.0f, 1.0f, 0.20f));
        DrawCircle(S(0.60f, 0.58f), w * 0.020f, new Color(0.90f, 0.84f, 1.0f, 0.18f));

        // ── Trunk glow halos (tight columns only) ────────────────────────────
        var halo = new Color(TraceC.R, TraceC.G, TraceC.B, 0.006f);
        DrawLine(S(0.13f, 0.00f), S(0.13f, 1.00f), halo, 5f);
        DrawLine(S(0.87f, 0.00f), S(0.87f, 1.00f), halo, 5f);
        DrawLine(S(0.00f, 0.06f), S(0.34f, 0.06f), halo, 4f);
        DrawLine(S(0.66f, 0.06f), S(1.00f, 0.06f), halo, 4f);

        // ── Circuit traces ────────────────────────────────────────────────────
        // Drawn quiet - the structure should read as detail, not as the scene.
        for (int ti = 0; ti < Traces.Length; ti++)
        {
            var (x1, y1, x2, y2, wClass) = Traces[ti];
            float ba  = wClass switch { 0 => 0.034f, 1 => 0.016f, _ => 0.007f };
            float lw  = wClass switch { 0 => 2.0f,   1 => 1.5f,   _ => 1.0f   };
            float ta  = ba + ba * 0.12f * MathF.Sin(_t * 0.35f + x1 * 4.1f + y1 * 3.3f);
            DrawLine(S(x1, y1), S(x2, y2), new Color(TraceC, ta), lw);

            // Sparse orange accents for premium dual-tone circuitry.
            if (ti % 17 == 0 || ti % 23 == 0)
            {
                float oa = 0.026f + 0.020f * (0.5f + 0.5f * MathF.Sin(_t * 1.6f + ti * 0.33f));
                DrawLine(S(x1, y1), S(x2, y2), new Color(Orange, oa), MathF.Max(1f, lw - 0.3f));
            }
        }

        // ── SMD component footprints ──────────────────────────────────────────
        float compA = 0.06f + 0.02f * MathF.Sin(_t * 0.7f);
        foreach (var (cx, cy, hw, hh, pads) in Components)
        {
            var  cPos = S(cx, cy);
            float cw2 = hw * w * 2f, ch2 = hh * h * 2f;
            var  br   = new Rect2(cPos.X - hw * w, cPos.Y - hh * h, cw2, ch2);
            DrawRect(br, new Color(0.02f, 0.05f, 0.03f, 0.30f));
            DrawRect(br, new Color(TraceC, compA * 0.55f), false, 1f);
            float mk = 2.5f;
            DrawRect(new Rect2(br.Position.X,           br.Position.Y,           mk, mk), new Color(TraceC, compA));
            DrawRect(new Rect2(br.Position.X + cw2 - mk, br.Position.Y,           mk, mk), new Color(TraceC, compA));
            DrawRect(new Rect2(br.Position.X,           br.Position.Y + ch2 - mk, mk, mk), new Color(TraceC, compA));
            DrawRect(new Rect2(br.Position.X + cw2 - mk, br.Position.Y + ch2 - mk, mk, mk), new Color(TraceC, compA));
            for (int pi = 0; pi < pads; pi++)
            {
                float frac = (pi + 1f) / (pads + 1f);
                float padY = br.Position.Y + frac * ch2;
                float ps = 3.5f, pd = 2.0f;
                DrawRect(new Rect2(br.Position.X - pd - ps,  padY - ps * 0.5f, ps, ps), new Color(TraceC, compA * 0.80f));
                DrawRect(new Rect2(br.Position.X + cw2 + pd, padY - ps * 0.5f, ps, ps), new Color(TraceC, compA * 0.80f));
            }
        }

        // ── Junction pads ─────────────────────────────────────────────────────
        foreach (var (jx, jy) in JunctionPads)
        {
            var  pos = S(jx, jy);
            float pa = 0.030f + 0.015f * MathF.Sin(_t * 1.8f + jx * 5.7f + jy * 3.9f);
            DrawRect(new Rect2(pos.X - 1.5f, pos.Y - 1.5f, 3f, 3f), new Color(TraceC, pa));
        }

        // ── Via holes ─────────────────────────────────────────────────────────
        foreach (var (vx, vy, vr) in Vias)
        {
            var  pos = S(vx, vy);
            float va = 0.14f + 0.06f * MathF.Sin(_t * 1.4f + vx * 6.3f);
            DrawCircle(pos, vr,       new Color(0.02f, 0.02f, 0.06f, 0.90f));
            DrawArc(pos, vr,       0f, MathF.Tau, 16, new Color(TraceC, va * 0.34f), 1.1f);
            DrawArc(pos, vr * 0.6f, 0f, MathF.Tau, 16, new Color(Cyan,   va * 0.14f), 0.9f);
        }

        // ── Glow nodes - tight annular rings, controlled cores ────────────────
        for (int ni = 0; ni < Nodes.Length; ni++)
        {
            var (nx, ny, colorIdx, r) = Nodes[ni];
            var   pos   = S(nx, ny);
            float pulse = 0.5f + 0.5f * MathF.Sin(_t * 11f + ni * 0.73f);

            switch (colorIdx)
            {
                case 0: // orange - tight halo + annular ring + bright core
                    DrawCircle(pos, r * 1.70f, new Color(Orange, 0.025f + 0.016f * pulse));
                    DrawArc(pos, r,       0f, MathF.Tau, 18, new Color(Orange, 0.28f + 0.14f * pulse), 1.2f);
                    DrawCircle(pos, r * 0.44f, new Color(1.0f, 0.84f, 0.44f, 0.38f + 0.12f * pulse));
                    DrawCircle(pos, r * 0.16f, new Color(1.0f, 0.96f, 0.80f, 0.52f + 0.10f * pulse));
                    break;
                case 1: // cyan - tight halo + ring + core
                    DrawCircle(pos, r * 1.52f, new Color(Cyan, 0.018f + 0.012f * pulse));
                    DrawArc(pos, r,       0f, MathF.Tau, 16, new Color(Cyan, 0.19f + 0.12f * pulse), 1.0f);
                    DrawCircle(pos, r * 0.40f, new Color(Cyan, 0.36f + 0.12f * pulse));
                    break;
                case 2: // lime - square SMD pad
                    DrawRect(new Rect2(pos.X - r * 1.1f, pos.Y - r * 1.1f, r * 2.2f, r * 2.2f),
                        new Color(Lime, 0.018f + 0.012f * pulse));
                    DrawRect(new Rect2(pos.X - r,        pos.Y - r,        r * 2.0f, r * 2.0f),
                        new Color(Lime, 0.06f + 0.045f * pulse), false, 1f);
                    DrawRect(new Rect2(pos.X - r * 0.5f, pos.Y - r * 0.5f, r * 1.0f, r * 1.0f),
                        new Color(Lime, 0.20f + 0.09f * pulse));
                    break;
            }
        }

        // ── Charge packets - slow, deliberate, 8 traces ───────────────────────
        for (int i = 0; i < 260; i++)
        {
            float hx = Hash01(i * 13.17f + 2.1f);
            float hy = Hash01(i * 29.71f + 7.4f);
            float hr = Hash01(i * 5.23f + 1.3f);
            float twinkle = 0.5f + 0.5f * MathF.Sin(_t * 2.2f + i * 0.37f);
            float alpha = (0.020f + hr * 0.064f) * (0.64f + 0.36f * twinkle);
            float radius = 0.40f + hr * 1.25f;
            Color c = hr > 0.72f
                ? new Color(1.0f, 0.68f, 0.20f, alpha)
                : new Color(0.34f, 0.82f, 0.96f, alpha);
            DrawCircle(new Vector2(hx * w, hy * h), radius, c);
        }

        for (int i = 0; i < 110; i++)
        {
            float hx = Hash01(i * 17.13f + 0.9f);
            float hy = Hash01(i * 11.41f + 4.2f);
            float hr = Hash01(i * 8.19f + 2.7f);
            float twinkle = 0.5f + 0.5f * MathF.Sin(_t * 2.8f + i * 0.61f);
            float alpha = (0.024f + hr * 0.078f) * (0.60f + 0.40f * twinkle);
            float radius = 0.60f + hr * 1.65f;
            DrawCircle(new Vector2(hx * w, hy * h), radius, new Color(1.0f, 0.64f, 0.20f, alpha));
        }

        for (int pi = 0; pi < PulseTraces.Length; pi++)
        {
            int ti = PulseTraces[pi];
            if (ti >= Traces.Length) continue;
            var (x1, y1, x2, y2, _) = Traces[ti];
            var pA = S(x1, y1); var pB = S(x2, y2);
            float tNorm = ((_t + pi * 1.618f) % 1.0f + 1.0f) % 1.0f;
            var pPt = pA.Lerp(pB, tNorm);
            var dir = (pB - pA).Normalized();
            DrawLine(pPt - dir * 14f, pPt, new Color(Cyan, 0.008f), 2.0f);
            DrawLine(pPt - dir *  4f, pPt, new Color(Cyan, 0.024f), 2.0f);
            DrawCircle(pPt, 2.3f, new Color(Cyan, 0.08f));
            DrawCircle(pPt, 1.0f, new Color(Cyan, 0.24f));
        }
    }

    private static float Hash01(float x)
    {
        float s = MathF.Sin(x) * 43758.5453f;
        return s - MathF.Floor(s);
    }
}
