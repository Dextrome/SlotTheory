using Godot;
using SlotTheory.UI;

namespace SlotTheory.Entities;

/// <summary>
/// Lightweight mine marker for Rift Sapper traps.
/// </summary>
public partial class RiftMineVisual : Node2D
{
    private const float ChainFlashDuration = 0.20f;
    private const float MiniVisualScale = 0.80f;

    private Color _accent = new Color(0.60f, 1.00f, 0.56f);
    private float _damageScale = 1f;
    private bool _isMiniMine;
    private bool _armed;
    private int _chargesRemaining = 1;
    private int _chargesMax = 1;
    private float _pulseT;
    private float _chainFlash;
    private float _chainFlashStrength = 1f;
    private TowerVisualTier _tier = TowerVisualTier.Tier0;
    private string _focalModId = string.Empty;
    private string _supportModId = string.Empty;
    private string _tertiaryModId = string.Empty;
    private FocalAccentShape _focalShape = FocalAccentShape.Crest;
    private FocalAccentShape _supportShape = FocalAccentShape.Crest;
    private FocalAccentShape _tertiaryShape = FocalAccentShape.Crest;

    public void Initialize(
        Color accent,
        float damageScale,
        bool isMiniMine = false,
        int modCount = 0,
        string? focalModId = null,
        string? supportModId = null,
        string? tertiaryModId = null)
    {
        _accent = accent;
        _damageScale = Mathf.Clamp(damageScale, 0.25f, 1.5f);
        _isMiniMine = isMiniMine;
        _tier = ResolveTier(modCount);
        _focalModId = focalModId ?? string.Empty;
        _supportModId = supportModId ?? string.Empty;
        _tertiaryModId = tertiaryModId ?? string.Empty;
        _focalShape = ResolveShape(_focalModId);
        _supportShape = ResolveShape(_supportModId);
        _tertiaryShape = ResolveShape(_tertiaryModId);
        SetProcess(true);
        QueueRedraw();
    }

    public void SetArmed(bool armed)
    {
        if (_armed == armed) return;
        _armed = armed;
        QueueRedraw();
    }

    public void SetCharges(int remaining, int max)
    {
        int clampedMax = Mathf.Max(1, max);
        int clampedRemaining = remaining;
        if (clampedRemaining < 0) clampedRemaining = 0;
        if (clampedRemaining > clampedMax) clampedRemaining = clampedMax;
        if (_chargesRemaining == clampedRemaining && _chargesMax == clampedMax)
            return;
        _chargesRemaining = clampedRemaining;
        _chargesMax = clampedMax;
        QueueRedraw();
    }

    public void TriggerChainFlash(float strength = 1f)
    {
        _chainFlash = ChainFlashDuration;
        _chainFlashStrength = Mathf.Clamp(strength, 0.75f, 1.45f);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _pulseT += dt;
        if (_chainFlash > 0f)
            _chainFlash = Mathf.Max(0f, _chainFlash - dt);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pulse = 0.72f + 0.28f * Mathf.Sin(_pulseT * 6.0f);
        float armAlpha = _armed ? 1f : 0.44f;
        float mini = Mathf.Lerp(0.72f, 1f, _damageScale);
        if (_isMiniMine)
            mini *= MiniVisualScale;

        var glow = new Color(_accent.R, _accent.G, _accent.B, (0.12f + 0.08f * pulse) * armAlpha);
        DrawCircle(Vector2.Zero, 13f * mini, glow);
        DrawCircle(Vector2.Zero, 8.5f * mini, new Color(_accent.R, _accent.G, _accent.B, (0.20f + 0.10f * pulse) * armAlpha));

        var shell = new Color(_accent.R, _accent.G, _accent.B, (0.84f + 0.12f * pulse) * armAlpha);
        var core = new Color(0.08f, 0.20f, 0.10f, 0.95f);
        DrawPolygon(RegularPoly(6, 6.8f * mini, Mathf.Pi / 6f), new[] { shell });
        DrawPolygon(RegularPoly(6, 5.1f * mini, Mathf.Pi / 6f), new[] { core });

        // Arming indicator.
        float ringAlpha = _armed ? 0.92f : 0.40f;
        DrawArc(Vector2.Zero, 9.5f * mini, 0f, Mathf.Tau, 28, new Color(0.84f, 1.0f, 0.80f, ringAlpha), 1.3f);

        DrawEvolutionShell(mini, armAlpha, pulse);
        DrawEvolutionAccents(mini, armAlpha, pulse);
        DrawChargeSegments(mini, armAlpha, pulse);

        // Crosshair center.
        var cross = new Color(0.90f, 1.0f, 0.84f, 0.80f * armAlpha);
        DrawLine(new Vector2(-2.8f, 0f), new Vector2(2.8f, 0f), cross, 1.2f);
        DrawLine(new Vector2(0f, -2.8f), new Vector2(0f, 2.8f), cross, 1.2f);

        if (_chainFlash > 0f)
        {
            float t = _chainFlash / ChainFlashDuration;
            float boost = _chainFlashStrength;
            DrawCircle(Vector2.Zero, 14.5f * (1f + (1f - t) * 0.48f) * boost,
                new Color(0.92f, 1.00f, 0.84f, 0.24f * t));
            DrawArc(Vector2.Zero, 9.2f * (1f + (1f - t) * 0.40f) * boost, 0f, Mathf.Tau, 24,
                new Color(0.86f, 1.00f, 0.80f, 0.30f * t), 1.4f);
            DrawArc(Vector2.Zero, 12.7f * (1f + (1f - t) * 0.28f) * boost, 0f, Mathf.Tau, 28,
                new Color(0.96f, 1.00f, 0.88f, 0.20f * t), 1.0f);
        }
    }

    private static TowerVisualTier ResolveTier(int modCount) => modCount switch
    {
        <= 0 => TowerVisualTier.Tier0,
        1 => TowerVisualTier.Tier1,
        2 => TowerVisualTier.Tier2,
        _ => TowerVisualTier.Tier3,
    };

    private static FocalAccentShape ResolveShape(string modId)
    {
        if (string.IsNullOrWhiteSpace(modId))
            return FocalAccentShape.Crest;
        return modId.Trim().ToLowerInvariant() switch
        {
            "focus_lens" => FocalAccentShape.Lens,
            "blast_core" => FocalAccentShape.Spike,
            "chain_reaction" => FocalAccentShape.Chain,
            "split_shot" => FocalAccentShape.Chain,
            "wildfire" => FocalAccentShape.Spike,
            "overkill" => FocalAccentShape.Spike,
            "feedback_loop" => FocalAccentShape.Crest,
            "reaper_protocol" => FocalAccentShape.Lens,
            "exploit_weakness" => FocalAccentShape.Bracket,
            "momentum" => FocalAccentShape.Crest,
            "overreach" => FocalAccentShape.Lens,
            "slow" => FocalAccentShape.Bracket,
            "hair_trigger" => FocalAccentShape.Spike,
            _ => FocalAccentShape.Crest,
        };
    }

    private void DrawEvolutionShell(float mini, float armAlpha, float pulse)
    {
        if (_tier == TowerVisualTier.Tier0)
            return;

        float miniMul = _isMiniMine ? 0.80f : 1f;
        float alphaBoost = _tier switch
        {
            TowerVisualTier.Tier1 => 0.20f,
            TowerVisualTier.Tier2 => 0.30f,
            TowerVisualTier.Tier3 => 0.40f,
            _ => 0f,
        };
        float alpha = (0.16f + 0.10f * pulse + alphaBoost) * armAlpha * miniMul;
        var shell = new Color(
            Mathf.Lerp(_accent.R, 1f, 0.32f),
            Mathf.Lerp(_accent.G, 1f, 0.32f),
            Mathf.Lerp(_accent.B, 1f, 0.32f),
            alpha);
        float outerR = 12.8f * mini;

        switch (_tier)
        {
            case TowerVisualTier.Tier1:
                DrawArc(Vector2.Zero, outerR, -2.18f, -0.96f, 16, shell, 1.25f * miniMul);
                DrawLine(new Vector2(-2.2f, -11.8f) * mini, new Vector2(2.2f, -11.8f) * mini, new Color(shell.R, shell.G, shell.B, alpha * 0.90f), 1.15f * miniMul);
                DrawCircle(new Vector2(0f, -12.3f) * mini, 1.20f * miniMul, new Color(shell.R, shell.G, shell.B, alpha * 0.92f));
                break;
            case TowerVisualTier.Tier2:
                DrawArc(Vector2.Zero, outerR + 0.8f * mini, -2.90f, -0.24f, 18, shell, 1.35f * miniMul);
                DrawArc(Vector2.Zero, outerR + 0.8f * mini, 0.24f, 2.90f, 18, shell, 1.35f * miniMul);
                DrawCircle(new Vector2(-11.8f, 0f) * mini, 1.25f * miniMul, new Color(shell.R, shell.G, shell.B, alpha * 0.94f));
                DrawCircle(new Vector2(11.8f, 0f) * mini, 1.25f * miniMul, new Color(shell.R, shell.G, shell.B, alpha * 0.94f));
                DrawLine(new Vector2(-10.6f, 0f) * mini, new Vector2(-7.6f, 0f) * mini, new Color(shell.R, shell.G, shell.B, alpha * 0.84f), 1.1f * miniMul);
                DrawLine(new Vector2(10.6f, 0f) * mini, new Vector2(7.6f, 0f) * mini, new Color(shell.R, shell.G, shell.B, alpha * 0.84f), 1.1f * miniMul);
                break;
            case TowerVisualTier.Tier3:
                DrawArc(Vector2.Zero, outerR + 1.4f * mini, 0f, Mathf.Tau, 28, shell, 1.55f * miniMul);
                for (int i = 0; i < 3; i++)
                {
                    float a = -Mathf.Pi / 2f + i * Mathf.Tau / 3f;
                    Vector2 node = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((outerR + 2.0f * mini));
                    Vector2 inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (9.2f * mini);
                    DrawCircle(node, 1.35f * miniMul, new Color(shell.R, shell.G, shell.B, alpha * 0.94f));
                    DrawLine(inner, node, new Color(shell.R, shell.G, shell.B, alpha * 0.82f), 1.15f * miniMul);
                }
                DrawCircle(Vector2.Zero, 1.15f * miniMul, new Color(shell.R, shell.G, shell.B, alpha * 0.80f));
                break;
        }
    }

    private void DrawEvolutionAccents(float mini, float armAlpha, float pulse)
    {
        if (_focalModId.Length > 0)
        {
            Color accent = BlendAccent(ModifierVisuals.GetAccent(_focalModId), bodyMix: 0.08f, alpha: (0.46f + pulse * 0.16f) * armAlpha);
            DrawFocalCoreRing(_focalShape, mini, armAlpha, pulse, accent);
            DrawGlyph(_focalShape, new Vector2(0f, -10.7f) * mini, 1.22f * mini, accent, micro: false);
        }

        if (_tier >= TowerVisualTier.Tier2 && _supportModId.Length > 0)
        {
            bool reinforced = string.Equals(_supportModId, _focalModId, System.StringComparison.OrdinalIgnoreCase);
            float scale = (reinforced ? 1.10f : 1f) * mini;
            Color accent = BlendAccent(ModifierVisuals.GetAccent(_supportModId), bodyMix: 0.26f, alpha: (0.30f + pulse * 0.10f) * armAlpha);
            DrawGlyph(_supportShape, new Vector2(-8.6f, 7.0f) * mini, scale, accent, micro: true);
        }

        if (_tier >= TowerVisualTier.Tier3 && _tertiaryModId.Length > 0)
        {
            bool reinforced = string.Equals(_tertiaryModId, _focalModId, System.StringComparison.OrdinalIgnoreCase)
                           || string.Equals(_tertiaryModId, _supportModId, System.StringComparison.OrdinalIgnoreCase);
            float scale = (reinforced ? 1.12f : 1f) * mini;
            Color accent = BlendAccent(ModifierVisuals.GetAccent(_tertiaryModId), bodyMix: 0.42f, alpha: (0.22f + pulse * 0.08f) * armAlpha);
            DrawGlyph(_tertiaryShape, new Vector2(8.6f, 7.0f) * mini, scale, accent, micro: true);
        }
    }

    private void DrawFocalCoreRing(FocalAccentShape shape, float mini, float armAlpha, float pulse, Color focalAccent)
    {
        float ringAlpha = (0.44f + pulse * 0.14f) * armAlpha;
        float ringWidth = 1.25f;
        float ringRadius = 3.9f * mini;

        if (shape == FocalAccentShape.Lens)
        {
            ringAlpha += 0.08f;
            ringWidth = 1.45f;
            ringRadius = 4.2f * mini;
        }
        else if (shape == FocalAccentShape.Spike)
        {
            ringRadius = 3.7f * mini;
        }
        else if (shape == FocalAccentShape.Chain)
        {
            ringRadius = 3.8f * mini;
        }

        var ring = new Color(focalAccent.R, focalAccent.G, focalAccent.B, Mathf.Clamp(ringAlpha, 0f, 1f));
        DrawArc(Vector2.Zero, ringRadius, 0f, Mathf.Tau, 18, ring, ringWidth);
        DrawCircle(Vector2.Zero, 1.0f * mini, new Color(ring.R, ring.G, ring.B, ring.A * 0.34f));
    }

    private void DrawGlyph(FocalAccentShape shape, Vector2 anchor, float scale, Color accent, bool micro)
    {
        float line = micro ? 1.05f : 1.28f;
        switch (shape)
        {
            case FocalAccentShape.Lens:
                DrawArc(anchor, (micro ? 1.8f : 2.3f) * scale, 0f, Mathf.Tau, 10, accent, line);
                DrawLine(anchor + new Vector2(-1.3f, 0f) * scale, anchor + new Vector2(1.3f, 0f) * scale, new Color(accent.R, accent.G, accent.B, accent.A * 0.84f), line * 0.9f);
                break;
            case FocalAccentShape.Bracket:
                DrawLine(anchor + new Vector2(-1.5f, -1.1f) * scale, anchor + new Vector2(-1.5f, 1.1f) * scale, accent, line);
                DrawLine(anchor + new Vector2(-1.5f, 1.1f) * scale, anchor + new Vector2(1.3f, 1.1f) * scale, accent, line);
                break;
            case FocalAccentShape.Spike:
                DrawPolygon(new[]
                {
                    anchor + new Vector2(0f, -2.3f) * scale,
                    anchor + new Vector2(1.3f, 0.5f) * scale,
                    anchor + new Vector2(-1.3f, 0.5f) * scale,
                }, new[] { new Color(accent.R, accent.G, accent.B, accent.A * 0.90f) });
                break;
            case FocalAccentShape.Chain:
                DrawLine(anchor + new Vector2(-1.8f, 0f) * scale, anchor + new Vector2(1.8f, 0f) * scale, accent, line);
                DrawCircle(anchor + new Vector2(-1.8f, 0f) * scale, 0.58f * scale, accent);
                DrawCircle(anchor + new Vector2(1.8f, 0f) * scale, 0.58f * scale, accent);
                break;
            default:
                DrawArc(anchor, (micro ? 2.0f : 2.5f) * scale, -2.45f, -0.72f, 10, accent, line);
                break;
        }
    }

    private Color BlendAccent(Color accent, float bodyMix, float alpha)
    {
        return new Color(
            Mathf.Lerp(accent.R, _accent.R, bodyMix),
            Mathf.Lerp(accent.G, _accent.G, bodyMix),
            Mathf.Lerp(accent.B, _accent.B, bodyMix),
            Mathf.Clamp(alpha, 0f, 1f));
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

    private void DrawChargeSegments(float mini, float armAlpha, float pulse)
    {
        if (_chargesMax <= 0) return;

        float radius = 12.2f * mini;
        float width = 1.6f;
        float gap = 0.14f;
        float seg = (Mathf.Tau / _chargesMax) - gap;
        float startOffset = -Mathf.Pi * 0.5f;

        for (int i = 0; i < _chargesMax; i++)
        {
            float a0 = startOffset + i * (seg + gap);
            float a1 = a0 + seg;
            bool active = i < _chargesRemaining;
            float alpha = active ? (0.78f + 0.14f * pulse) * armAlpha : 0.20f * armAlpha;
            var col = new Color(0.86f, 1.00f, 0.86f, alpha);
            DrawArc(Vector2.Zero, radius, a0, a1, 12, col, width);
        }

        // Make the final charge state pop visually.
        if (_chargesRemaining == 1)
        {
            float glowAlpha = (0.30f + 0.16f * pulse) * armAlpha;
            DrawArc(Vector2.Zero, radius + 1.2f, 0f, Mathf.Tau, 28,
                new Color(1.00f, 0.96f, 0.72f, glowAlpha), 1.2f);
        }
    }
}
