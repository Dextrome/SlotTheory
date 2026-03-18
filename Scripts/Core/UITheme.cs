using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Shared neon-synthwave theme and font helpers.
/// </summary>
public static class UITheme
{
    // ── Fonts ─────────────────────────────────────────────────────────────
    public static FontFile Regular  { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-SemiBold.ttf");
    public static FontFile SemiBold { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");
    public static FontFile Bold     { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");

    // ── Color palette ─────────────────────────────────────────────────────
    public static readonly Color Lime      = new(0.651f, 0.839f, 0.031f);      // #a6d608 - brand green
    public static readonly Color LimeDim   = new(0.45f,  0.58f,  0.02f);       // muted lime for pressed
    public static readonly Color LimeDark  = new(0.22f,  0.30f,  0.01f);       // deep lime for normal border
    public static readonly Color Cyan      = new(0.08f,  0.85f,  0.90f);       // #14d9e6 - secondary accent
    public static readonly Color Magenta   = new(0.75f,  0.08f,  0.48f);       // existing pink accent for muted
    public static readonly Color BgDeep    = new(0.027f, 0.027f, 0.102f);      // #07071a - near-black bg
    public static readonly Color BgMid     = new(0.06f,  0.06f,  0.18f);       // mid-deep bg
    public static readonly Color BgPanel   = new(0.07f,  0.07f,  0.165f);      // card / panel bg
    public static readonly Color BgHover   = new(0.04f,  0.09f,  0.04f);       // tinted green on hover
    public static readonly Color BorderDim = new(0.15f,  0.20f,  0.15f);       // subtle default border
    public static readonly Color BorderMid = new(0.20f,  0.28f,  0.18f);       // mid brightness border

    // ── Theme builder ─────────────────────────────────────────────────────

    /// <summary>Builds a configured theme with Rajdhani and neon colors.</summary>
    public static Theme Build()
    {
        var theme = new Theme();

        theme.DefaultFont     = Regular;
        theme.DefaultFontSize = 18;

        // Standard buttons - lime accent on hover
        theme.SetStylebox("normal",   "Button", MakeBtn(BgDeep,  BorderDim,  border: 1, corners: 8, glowAlpha: 0f,   glowSize: 0));
        theme.SetStylebox("hover",    "Button", MakeBtn(BgHover, Lime,       border: 2, corners: 8, glowAlpha: 0.16f, glowSize: 5));
        theme.SetStylebox("pressed",  "Button", MakeBtn(BgDeep,  LimeDim,    border: 2, corners: 8, glowAlpha: 0.10f, glowSize: 3));
        theme.SetStylebox("focus",    "Button", MakeBtn(BgHover, Lime,       border: 2, corners: 8, glowAlpha: 0.13f, glowSize: 4));
        theme.SetStylebox("disabled", "Button", MakeBtn(new Color(0.04f, 0.04f, 0.10f), new Color(0.10f, 0.10f, 0.14f), border: 1, corners: 8, glowAlpha: 0f, glowSize: 0));

        theme.SetColor("font_color",          "Button", Colors.White);
        theme.SetColor("font_hover_color",    "Button", Colors.White);
        theme.SetColor("font_pressed_color",  "Button", new Color(0.85f, 1f, 0.85f));
        theme.SetColor("font_disabled_color", "Button", new Color(0.38f, 0.38f, 0.38f));
        theme.SetColor("font_outline_color",  "Button", new Color(0f, 0f, 0f, 0.50f));
        theme.SetConstant("outline_size",     "Button", 1);
        theme.SetFont("font",                 "Button", Regular);

        // Panel
        theme.SetStylebox("panel", "Panel", MakePanel());

        // Labels
        theme.SetFont("font",              "Label", Regular);
        theme.SetColor("font_outline_color","Label", new Color(0f, 0f, 0f, 0.45f));
        theme.SetConstant("outline_size",  "Label", 1);

        return theme;
    }

    // ── Per-button style variants ─────────────────────────────────────────

    /// <summary>
    /// Primary action button - Play, Submit, etc.
    /// Stronger lime glow; lime-tinted background.
    /// </summary>
    public static void ApplyPrimaryStyle(Button btn)
    {
        var bgNormal  = new Color(0.06f, 0.14f, 0.04f);
        var bgHover   = new Color(0.09f, 0.20f, 0.05f);
        btn.AddThemeStyleboxOverride("normal",  MakeBtn(bgNormal,  LimeDark, border: 1, corners: 10, glowAlpha: 0.10f, glowSize: 4,  glowColor: Lime));
        btn.AddThemeStyleboxOverride("hover",   MakeBtn(bgHover,   Lime,     border: 2, corners: 10, glowAlpha: 0.34f, glowSize: 9,  glowColor: Lime));
        btn.AddThemeStyleboxOverride("pressed", MakeBtn(BgDeep,    LimeDim,  border: 2, corners: 10, glowAlpha: 0.18f, glowSize: 5,  glowColor: LimeDim));
        btn.AddThemeStyleboxOverride("focus",   MakeBtn(bgHover,   Lime,     border: 2, corners: 10, glowAlpha: 0.26f, glowSize: 7,  glowColor: Lime));
        btn.AddThemeColorOverride("font_color",       Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Lime);
        btn.AddThemeFontSizeOverride("font_size", 20);
    }

    /// <summary>
    /// Muted / destructive button - Quit, etc.
    /// Magenta/pink accent on hover instead of lime.
    /// </summary>
    public static void ApplyMutedStyle(Button btn)
    {
        var bgMuted = new Color(0.08f, 0.04f, 0.10f);
        btn.AddThemeStyleboxOverride("normal",  MakeBtn(BgDeep,  new Color(0.18f, 0.08f, 0.22f), border: 1, corners: 8, glowAlpha: 0f,   glowSize: 0));
        btn.AddThemeStyleboxOverride("hover",   MakeBtn(bgMuted, Magenta, border: 2, corners: 8, glowAlpha: 0.18f, glowSize: 8, glowColor: Magenta));
        btn.AddThemeStyleboxOverride("pressed", MakeBtn(BgDeep,  new Color(0.55f, 0.06f, 0.35f), border: 2, corners: 8, glowAlpha: 0.10f, glowSize: 4, glowColor: Magenta));
        btn.AddThemeStyleboxOverride("focus",   MakeBtn(bgMuted, Magenta, border: 2, corners: 8, glowAlpha: 0.14f, glowSize: 6, glowColor: Magenta));
        btn.AddThemeColorOverride("font_color",       new Color(0.70f, 0.70f, 0.70f));
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    /// <summary>
    /// Cyan / secondary accent - used for navigational buttons (Back, etc.)
    /// </summary>
    public static void ApplyCyanStyle(Button btn)
    {
        var bgCyan = new Color(0.02f, 0.09f, 0.11f);
        btn.AddThemeStyleboxOverride("normal",  MakeBtn(BgDeep,  new Color(0.06f, 0.30f, 0.33f), border: 1, corners: 8, glowAlpha: 0f,   glowSize: 0));
        btn.AddThemeStyleboxOverride("hover",   MakeBtn(bgCyan,  Cyan, border: 2, corners: 8, glowAlpha: 0.20f, glowSize: 8, glowColor: Cyan));
        btn.AddThemeStyleboxOverride("pressed", MakeBtn(BgDeep,  new Color(0.04f, 0.55f, 0.60f), border: 2, corners: 8, glowAlpha: 0.10f, glowSize: 4, glowColor: Cyan));
        btn.AddThemeStyleboxOverride("focus",   MakeBtn(bgCyan,  Cyan, border: 2, corners: 8, glowAlpha: 0.16f, glowSize: 6, glowColor: Cyan));
        btn.AddThemeColorOverride("font_color",       new Color(0.75f, 0.95f, 0.96f));
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Applies Rajdhani font overrides to any Control.</summary>
    public static void ApplyFont(Control control, bool semiBold = false, bool bold = false, int size = 0)
    {
        var font = bold ? Bold : (semiBold ? SemiBold : Regular);
        control.AddThemeFontOverride("font", font);
        control.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.45f));
        control.AddThemeConstantOverride("outline_size", bold ? 2 : 1);
        if (size > 0)
            control.AddThemeFontSizeOverride("font_size", size);
    }

    /// <summary>
    /// Adds a 2 px lime accent stripe at the top of any Control (panel, card, bar).
    /// Call after AddChild so it renders above the panel content.
    /// </summary>
    public static void AddTopAccent(Control panel, Color? color = null)
    {
        var c = color ?? new Color(Lime.R, Lime.G, Lime.B, 0.82f);
        // Draw a 2px line at the top of the control's own draw pass so it works
        // for PanelContainer (which overrides child sizing) as well as plain Panel.
        panel.Draw += () => panel.DrawLine(new Vector2(0f, 1f), new Vector2(panel.Size.X, 1f), c, 2f);
    }

    public static StyleBoxFlat MakePanel(
        Color? bg = null,
        Color? border = null,
        int corners = 8,
        int borderWidth = 1,
        int padH = 10,
        int padV = 8)
    {
        var box = new StyleBoxFlat();
        box.BgColor     = bg     ?? BgPanel;
        box.BorderColor = border ?? BorderDim;
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(corners);
        box.ContentMarginLeft   = padH;
        box.ContentMarginRight  = padH;
        box.ContentMarginTop    = padV;
        box.ContentMarginBottom = padV;
        return box;
    }

    public static StyleBoxFlat MakeBtn(
        Color bgColor,
        Color borderColor,
        int border    = 1,
        int corners   = 8,
        float glowAlpha = 0f,
        int glowSize  = 0,
        Color? glowColor = null)
    {
        var box = new StyleBoxFlat();
        box.BgColor     = bgColor;
        box.BorderColor = borderColor;
        box.SetBorderWidthAll(border);
        box.SetCornerRadiusAll(corners);
        box.ContentMarginLeft   = 14;
        box.ContentMarginRight  = 14;
        box.ContentMarginTop    = 9;
        box.ContentMarginBottom = 9;

        if (glowAlpha > 0f && glowSize > 0)
        {
            var gc = glowColor ?? borderColor;
            box.ShadowColor  = new Color(gc.R, gc.G, gc.B, glowAlpha);
            box.ShadowSize   = glowSize;
            box.ShadowOffset = Vector2.Zero;
        }

        return box;
    }
}
