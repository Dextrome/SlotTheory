using System;
using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Fullscreen animated neon-grid background drawn behind the main menu.
/// Horizontal + vertical grid lines with per-line alpha sine waves, plus a
/// slow horizontal scan sweep — all at very low opacity so UI text stays readable.
/// </summary>
public partial class NeonGridBg : Control
{
    private float _t = 0f;

    private static readonly Color LineH  = new(0.45f, 0.10f, 0.90f);
    private static readonly Color LineV  = new(0.30f, 0.05f, 0.75f);
    private static readonly Color Scan1  = new(0.80f, 0.15f, 1.00f);
    private static readonly Color Scan2  = new(0.95f, 0.35f, 1.00f);

    public override void _Process(double delta)
    {
        _t += (float)delta * 0.10f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var  sz = GetViewportRect().Size;
        float w = sz.X, h = sz.Y;

        // Horizontal lines
        const int Rows = 9;
        for (int row = 0; row <= Rows; row++)
        {
            float y = h * row / Rows;
            float a = 0.050f + 0.022f * MathF.Sin(_t * 0.75f + row * 0.62f);
            DrawLine(new Vector2(0, y), new Vector2(w, y), new Color(LineH, a), 1f);
        }

        // Vertical lines
        const int Cols = 13;
        for (int col = 0; col <= Cols; col++)
        {
            float x = w * col / Cols;
            float a = 0.050f + 0.022f * MathF.Sin(_t * 0.65f + col * 0.48f);
            DrawLine(new Vector2(x, 0), new Vector2(x, h), new Color(LineV, a), 1f);
        }

        // Slow downward scan sweep — two overlapping rects for a soft glow falloff
        float scanY = ((_t * 0.28f % 1f) * (h + 140f)) - 70f;
        DrawRect(new Rect2(0, scanY - 35f, w, 70f), new Color(Scan1, 0.030f));
        DrawRect(new Rect2(0, scanY - 10f, w, 20f), new Color(Scan2, 0.038f));
    }
}
