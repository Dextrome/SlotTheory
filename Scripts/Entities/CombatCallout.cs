using Godot;
using SlotTheory.Core;

namespace SlotTheory.Entities;

/// <summary>
/// Short-lived world-space callout for major combat moments.
/// </summary>
public partial class CombatCallout : Node2D
{
    private const float DefaultDuration = 1.35f;
    private const float RiseSpeed = 5f;
    private static float _mobileReadabilityScale = 1f;
    private static readonly Vector2[] OutlineDirs =
    {
        new Vector2(-1f, 0f),
        new Vector2(1f, 0f),
        new Vector2(0f, -1f),
        new Vector2(0f, 1f),
        new Vector2(-0.7071f, -0.7071f),
        new Vector2(0.7071f, -0.7071f),
        new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, 0.7071f),
    };

    private float _life = 0f;
    private float _duration = DefaultDuration;
    private float _holdPortion = 0.42f;
    private string _text = "";
    private Color _color = Colors.White;
    private bool _driftEnabled = true;
    private ulong _lastRealTickUsec = 0;

    private int _sizeOverride = 0;

    public void Initialize(
        string text,
        Color color,
        float duration = DefaultDuration,
        int sizeOverride = 0,
        bool driftEnabled = true,
        float holdPortion = 0.42f)
    {
        _text = text;
        _color = color;
        _duration = Mathf.Max(0.1f, duration);
        _sizeOverride = sizeOverride;
        _driftEnabled = driftEnabled;
        _holdPortion = Mathf.Clamp(holdPortion, 0f, 0.95f);
    }

    public void SetText(string text)
    {
        _text = text ?? string.Empty;
        QueueRedraw();
    }

    public void EnsureRemaining(float remainingSeconds)
    {
        float targetRemaining = Mathf.Max(0.1f, remainingSeconds);
        float requiredDuration = _life + targetRemaining;
        if (requiredDuration > _duration)
            _duration = requiredDuration;
    }

    public static void SetMobileReadabilityScale(float scale)
    {
        _mobileReadabilityScale = Mathf.Clamp(scale, 1f, 2f);
    }

    public override void _Process(double delta)
    {
        float fallbackDt = (float)delta / Mathf.Max(0.001f, (float)Engine.TimeScale);
        ulong nowUsec = Time.GetTicksUsec();
        float realDt;
        if (_lastRealTickUsec == 0)
        {
            _lastRealTickUsec = nowUsec;
            realDt = Mathf.Max(0f, fallbackDt);
        }
        else
        {
            realDt = (nowUsec - _lastRealTickUsec) / 1_000_000f;
            _lastRealTickUsec = nowUsec;
            if (!float.IsFinite(realDt) || realDt <= 0f || realDt > 0.25f)
                realDt = Mathf.Max(0f, fallbackDt);
        }

        _life += realDt;
        if (_driftEnabled)
            Position += new Vector2(0f, -RiseSpeed * realDt);
        if (_life >= _duration)
        {
            QueueFree();
            return;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        Font font = UITheme.Bold;
        float t = _life / _duration;
        float holdPortion = _holdPortion;
        float fadeT = t <= holdPortion ? 0f : Mathf.Clamp((t - holdPortion) / (1f - holdPortion), 0f, 1f);
        float alpha = 1f - fadeT * fadeT * fadeT;
        int size = _sizeOverride > 0
            ? Mathf.Clamp(Mathf.RoundToInt(_sizeOverride * _mobileReadabilityScale), 12, 72)
            : Mathf.Clamp(Mathf.RoundToInt(20f * _mobileReadabilityScale), 12, 52);
        var col = new Color(_color.R, _color.G, _color.B, alpha);
        float ascent = font.GetAscent(size);
        float descent = font.GetDescent(size);
        float lineHeight = ascent + descent + 2f;
        string[] lines = _text.Split('\n');
        float startBaselineY = -((lines.Length - 1) * lineHeight) * 0.5f;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            float baselineY = startBaselineY + i * lineHeight;
            Vector2 textSize = font.GetStringSize(line, HorizontalAlignment.Left, -1, size);
            Vector2 baselinePos = new Vector2(-textSize.X * 0.5f, baselineY);
            var bg = new Rect2(
                baselinePos.X - 10f,
                baselineY - ascent - 2f,
                textSize.X + 20f,
                lineHeight + 3f);
            float feather = Mathf.Clamp(lineHeight * 0.42f, 5f, 12f);
            DrawFeatheredBackdrop(bg, alpha, feather);

            // Single dark outline reads cleaner than white+dark layering on busy lanes.
            DrawOutline(font, size, line, baselinePos, 2.2f, new Color(0f, 0f, 0f, alpha * 0.96f));
            DrawString(font, baselinePos, line, HorizontalAlignment.Left, -1, size, col);
        }
    }

    private void DrawOutline(Font font, int size, string line, Vector2 baselinePos, float radius, Color color)
    {
        for (int i = 0; i < OutlineDirs.Length; i++)
        {
            Vector2 offset = OutlineDirs[i] * radius;
            DrawString(font, baselinePos + offset, line, HorizontalAlignment.Left, -1, size, color);
        }
    }

    private void DrawFeatheredBackdrop(Rect2 outer, float alpha, float feather)
    {
        if (outer.Size.X <= 2f || outer.Size.Y <= 2f)
            return;

        Rect2 inner = outer.Grow(-feather);
        if (inner.Size.X <= 2f || inner.Size.Y <= 2f)
        {
            DrawRect(outer, new Color(0f, 0f, 0f, alpha * 0.44f), filled: true);
            return;
        }

        Color core = new Color(0f, 0f, 0f, alpha * 0.56f);
        Color edge = new Color(0f, 0f, 0f, 0f);

        DrawRect(inner, core, filled: true);

        // Top and bottom fades.
        DrawGradientQuad(
            new Vector2(outer.Position.X, outer.Position.Y),
            new Vector2(outer.End.X, outer.Position.Y),
            new Vector2(inner.End.X, inner.Position.Y),
            new Vector2(inner.Position.X, inner.Position.Y),
            edge, edge, core, core);
        DrawGradientQuad(
            new Vector2(inner.Position.X, inner.End.Y),
            new Vector2(inner.End.X, inner.End.Y),
            new Vector2(outer.End.X, outer.End.Y),
            new Vector2(outer.Position.X, outer.End.Y),
            core, core, edge, edge);

        // Left and right fades.
        DrawGradientQuad(
            new Vector2(outer.Position.X, outer.Position.Y),
            new Vector2(inner.Position.X, inner.Position.Y),
            new Vector2(inner.Position.X, inner.End.Y),
            new Vector2(outer.Position.X, outer.End.Y),
            edge, core, core, edge);
        DrawGradientQuad(
            new Vector2(inner.End.X, inner.Position.Y),
            new Vector2(outer.End.X, outer.Position.Y),
            new Vector2(outer.End.X, outer.End.Y),
            new Vector2(inner.End.X, inner.End.Y),
            core, edge, edge, core);
    }

    private void DrawGradientQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ca, Color cb, Color cc, Color cd)
    {
        var points = new[] { a, b, c, d };
        var colors = new[] { ca, cb, cc, cd };
        DrawPolygon(points, colors);
    }
}
