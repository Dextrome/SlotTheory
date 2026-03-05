using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Animated synthwave backdrop used under draft cards.
/// </summary>
public partial class DraftBackdropFx : Control
{
    private float _time;

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Size.X <= 0 || Size.Y <= 0) return;

        var full = new Rect2(Vector2.Zero, Size);
        DrawRect(full, new Color(0.05f, 0.03f, 0.10f, 0.70f));

        // Vertical gradient slices.
        DrawRect(new Rect2(0, 0, Size.X, Size.Y * 0.45f), new Color(0.10f, 0.03f, 0.16f, 0.35f));
        DrawRect(new Rect2(0, Size.Y * 0.45f, Size.X, Size.Y * 0.35f), new Color(0.03f, 0.06f, 0.14f, 0.26f));
        DrawRect(new Rect2(0, Size.Y * 0.80f, Size.X, Size.Y * 0.20f), new Color(0.03f, 0.03f, 0.09f, 0.25f));

        // Animated horizontal scanlines.
        float phase = _time * 60f;
        for (int i = 0; i < 9; i++)
        {
            float y = Mathf.PosMod(i * 92f + phase, Size.Y + 24f) - 12f;
            float alpha = 0.06f + 0.04f * Mathf.Sin(_time * 2f + i);
            DrawRect(new Rect2(0, y, Size.X, 2f), new Color(0.40f, 0.92f, 1.00f, alpha));
        }

        // Slow moving neon columns.
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.PosMod(i * (Size.X / 4f) + _time * (8f + i * 3f), Size.X);
            DrawRect(new Rect2(x, 0, 2f, Size.Y), new Color(1.00f, 0.28f, 0.76f, 0.08f));
        }

        // Top haze.
        DrawRect(new Rect2(0, 0, Size.X, 84f), new Color(1.00f, 0.20f, 0.66f, 0.08f));
    }
}
