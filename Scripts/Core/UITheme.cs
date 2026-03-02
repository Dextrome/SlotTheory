using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Static helper that loads the Rajdhani font family and builds a shared Theme
/// for consistent neon-synthwave UI across all screens.
/// </summary>
public static class UITheme
{
    public static FontFile Regular   { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");
    public static FontFile SemiBold  { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");

    /// <summary>
    /// Builds a fully configured Theme with Rajdhani fonts and neon-synthwave colours.
    /// Call once per root control — not every frame.
    /// </summary>
    public static Theme Build()
    {
        var theme = new Theme();

        // ── Default font ──────────────────────────────────────────────────────
        theme.DefaultFont     = Regular;
        theme.DefaultFontSize = 18;

        // ── Button styles ─────────────────────────────────────────────────────
        theme.SetStylebox("normal",   "Button", MakeButtonBox(
            bgColor:     new Color(0.05f,  0.05f,  0.18f),
            borderColor: new Color(0.23f,  0.10f,  0.37f)));

        theme.SetStylebox("hover",    "Button", MakeButtonBox(
            bgColor:     new Color(0.10f,  0.06f,  0.25f),
            borderColor: new Color(0.75f,  0.08f,  0.48f)));

        theme.SetStylebox("pressed",  "Button", MakeButtonBox(
            bgColor:     new Color(0.165f, 0.05f,  0.31f),
            borderColor: new Color(0.75f,  0.08f,  0.48f)));

        theme.SetStylebox("focus",    "Button", MakeButtonBox(
            bgColor:     new Color(0.10f,  0.06f,  0.25f),
            borderColor: new Color(0.75f,  0.08f,  0.48f)));

        theme.SetStylebox("disabled", "Button", MakeButtonBox(
            bgColor:     new Color(0.04f,  0.04f,  0.10f),
            borderColor: new Color(0.15f,  0.08f,  0.22f)));

        theme.SetColor("font_color",          "Button", Colors.White);
        theme.SetColor("font_hover_color",    "Button", Colors.White);
        theme.SetColor("font_pressed_color",  "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(0.45f, 0.45f, 0.45f));
        theme.SetFont("font", "Button", Regular);

        // ── Panel style ───────────────────────────────────────────────────────
        var panelBox = new StyleBoxFlat();
        panelBox.BgColor               = new Color(0.07f, 0.07f, 0.165f);
        panelBox.BorderColor           = new Color(0.12f, 0.12f, 0.29f);
        panelBox.BorderWidthTop        = 1;
        panelBox.BorderWidthBottom     = 1;
        panelBox.BorderWidthLeft       = 1;
        panelBox.BorderWidthRight      = 1;
        panelBox.CornerRadiusTopLeft     = 4;
        panelBox.CornerRadiusTopRight    = 4;
        panelBox.CornerRadiusBottomLeft  = 4;
        panelBox.CornerRadiusBottomRight = 4;
        panelBox.ContentMarginLeft   = 8;
        panelBox.ContentMarginRight  = 8;
        panelBox.ContentMarginTop    = 6;
        panelBox.ContentMarginBottom = 6;
        theme.SetStylebox("panel", "Panel", panelBox);

        // ── Label font ────────────────────────────────────────────────────────
        theme.SetFont("font", "Label", Regular);

        return theme;
    }

    /// <summary>
    /// Applies the Rajdhani font to a Label, optionally using SemiBold and/or
    /// overriding the font size.
    /// </summary>
    public static void ApplyFont(Label label, bool semiBold = false, int size = 0)
    {
        label.AddThemeFontOverride("font", semiBold ? SemiBold : Regular);
        if (size > 0)
            label.AddThemeFontSizeOverride("font_size", size);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StyleBoxFlat MakeButtonBox(Color bgColor, Color borderColor)
    {
        var box = new StyleBoxFlat();
        box.BgColor               = bgColor;
        box.BorderColor           = borderColor;
        box.BorderWidthTop        = 1;
        box.BorderWidthBottom     = 1;
        box.BorderWidthLeft       = 1;
        box.BorderWidthRight      = 1;
        box.CornerRadiusTopLeft     = 4;
        box.CornerRadiusTopRight    = 4;
        box.CornerRadiusBottomLeft  = 4;
        box.CornerRadiusBottomRight = 4;
        box.ContentMarginLeft   = 8;
        box.ContentMarginRight  = 8;
        box.ContentMarginTop    = 6;
        box.ContentMarginBottom = 6;
        return box;
    }
}
