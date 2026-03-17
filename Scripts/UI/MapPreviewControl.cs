using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Miniature map preview drawn inside the map selection screen.
///
/// Renders the path polyline, start/exit markers, and tower slot positions
/// scaled to fit the control's current size. Call SetMap() for a known map
/// or SetMystery() for the Unstable Anomaly (procedural) map.
/// The control redraws immediately on either call.
/// </summary>
public partial class MapPreviewControl : Control
{
    private Vector2[] _pathPoints = System.Array.Empty<Vector2>();
    private Vector2[] _slotPoints = System.Array.Empty<Vector2>();
    private Rect2     _worldBounds;
    private bool      _mystery;

    // ── Palette (matches actual in-game rendering) ────────────────────────
    private static readonly Color BgColor      = new(0.06f,  0.07f,  0.11f,  1.00f);
    private static readonly Color BorderColor  = new(0.25f,  0.27f,  0.40f,  0.70f);
    // Path layers (mirrors GameController Line2D stack, in draw order)
    private static readonly Color PathGlow1    = new(0.72f,  0.01f,  0.50f,  0.08f);  // wide magenta glow
    private static readonly Color PathGlow2    = new(0.64f,  0.03f,  0.46f,  0.15f);  // medium glow
    private static readonly Color PathDark     = new(0.06f,  0.00f,  0.12f,  0.97f);  // dark near-black fill
    private static readonly Color PathHighlight= new(1.00f,  0.12f,  0.58f,  0.22f);  // pink inner highlight
    private static readonly Color PathEdge     = new(1.00f,  0.27f,  0.70f,  0.78f);  // bright pink edge
    // Slot marker: small yellow-gold square (matches tower body)
    private static readonly Color SlotShadow   = new(0.08f,  0.08f,  0.12f,  1.00f);
    public  static readonly Color SlotFill     = new(0.90f,  0.85f,  0.30f,  0.90f);
    public  static readonly Color StartMarker  = new(0.30f,  0.95f,  0.45f,  1.00f);
    public  static readonly Color ExitMarker   = new(0.95f,  0.30f,  0.30f,  1.00f);

    private const float Padding = 14f;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Supply path and slot data for a known map. Redraws immediately.</summary>
    public void SetMap(Vector2[] pathPoints, Vector2[] slotPoints)
    {
        _mystery     = false;
        _pathPoints  = pathPoints;
        _slotPoints  = slotPoints;
        _worldBounds = ComputeBounds(pathPoints, slotPoints);
        QueueRedraw();
    }

    /// <summary>
    /// Switch to mystery mode (Unstable Anomaly). Draws a centred "?" instead
    /// of a path. Redraws immediately.
    /// </summary>
    public void SetMystery()
    {
        _mystery    = true;
        _pathPoints = System.Array.Empty<Vector2>();
        _slotPoints = System.Array.Empty<Vector2>();
        QueueRedraw();
    }

    // ── Drawing ───────────────────────────────────────────────────────────

    public override void _Draw()
    {
        var size = Size;

        DrawRect(new Rect2(Vector2.Zero, size), BgColor);
        DrawRect(new Rect2(Vector2.Zero, size), BorderColor, filled: false, width: 1.5f);

        if (_mystery)
        {
            var font     = ThemeDB.FallbackFont;
            int fontSize = Mathf.RoundToInt(size.Y * 0.48f);
            // pos.Y = baseline; offset down by ~35% of font size to visually centre
            DrawString(font,
                new Vector2(0f, size.Y / 2f + fontSize * 0.35f),
                "?",
                HorizontalAlignment.Center,
                size.X,
                fontSize,
                new Color(0.45f, 0.45f, 0.65f, 0.45f));
            return;
        }

        if (_pathPoints.Length < 2 || _worldBounds.Size.X <= 0 || _worldBounds.Size.Y <= 0)
            return;

        float drawW = size.X - Padding * 2;
        float drawH = size.Y - Padding * 2;
        if (drawW <= 0 || drawH <= 0) return;

        float scale = Mathf.Min(drawW / _worldBounds.Size.X, drawH / _worldBounds.Size.Y);
        float offX  = Padding + (drawW - _worldBounds.Size.X * scale) / 2f;
        float offY  = Padding + (drawH - _worldBounds.Size.Y * scale) / 2f;

        Vector2 ToLocal(Vector2 w) => new(
            offX + (w.X - _worldBounds.Position.X) * scale,
            offY + (w.Y - _worldBounds.Position.Y) * scale);

        var local = new Vector2[_pathPoints.Length];
        for (int i = 0; i < _pathPoints.Length; i++)
            local[i] = ToLocal(_pathPoints[i]);

        // In-world path width is ~CELL_H (128 px). Scale proportionally.
        // Clamp so it looks reasonable at small preview sizes.
        float pathW = Mathf.Clamp(scale * 128f, 6f, 32f);

        // Layered polylines matching GameController Line2D stack
        DrawPolyline(local, PathGlow1,     pathW * 1.75f, antialiased: true);
        DrawPolyline(local, PathGlow2,     pathW * 1.09f, antialiased: true);
        DrawPolyline(local, PathDark,      pathW,         antialiased: true);
        DrawPolyline(local, PathHighlight, pathW * 0.55f, antialiased: true);
        DrawPolyline(local, PathEdge,      pathW * 0.10f, antialiased: true);

        // Tower slots: small squares (not circles), matching in-game tower body style
        float slotHalf = Mathf.Clamp(scale * 20f, 3f, 7f);
        foreach (var s in _slotPoints)
        {
            var lp = ToLocal(s);
            DrawRect(new Rect2(lp - Vector2.One * (slotHalf + 1.5f), Vector2.One * (slotHalf * 2 + 3f)), SlotShadow);
            DrawRect(new Rect2(lp - Vector2.One * slotHalf,           Vector2.One * (slotHalf * 2)),       SlotFill);
        }

        float markerR = Mathf.Max(3f, pathW * 0.18f);
        DrawCircle(local[0],   markerR + 1.5f, SlotShadow);
        DrawCircle(local[0],   markerR,        StartMarker);
        DrawCircle(local[^1],  markerR + 1.5f, SlotShadow);
        DrawCircle(local[^1],  markerR,        ExitMarker);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Rect2 ComputeBounds(Vector2[] path, Vector2[] slots)
    {
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        void Expand(float x, float y)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        foreach (var p in path)  Expand(p.X, p.Y);
        foreach (var s in slots) Expand(s.X, s.Y);

        if (minX == float.MaxValue) return default;

        const float margin = 40f;
        return new Rect2(minX - margin, minY - margin,
                         maxX - minX + margin * 2,
                         maxY - minY + margin * 2);
    }
}
