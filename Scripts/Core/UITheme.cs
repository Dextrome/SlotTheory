using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Shared neon-synthwave theme and font helpers.
/// </summary>
public static class UITheme
{
    // Slightly heavier defaults keep neon text more legible.
    public static FontFile Regular  { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-SemiBold.ttf");
    public static FontFile SemiBold { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");
    public static FontFile Bold     { get; } = GD.Load<FontFile>("res://Assets/Fonts/Rajdhani-Bold.ttf");

    /// <summary>
    /// Builds a configured theme with Rajdhani and neon colors.
    /// </summary>
    public static Theme Build()
    {
        var theme = new Theme();

        theme.DefaultFont = Regular;
        theme.DefaultFontSize = 18;

        theme.SetStylebox("normal", "Button", MakeButtonBox(
            bgColor: new Color(0.05f, 0.05f, 0.18f),
            borderColor: new Color(0.23f, 0.10f, 0.37f)));

        theme.SetStylebox("hover", "Button", MakeButtonBox(
            bgColor: new Color(0.10f, 0.06f, 0.25f),
            borderColor: new Color(0.75f, 0.08f, 0.48f)));

        theme.SetStylebox("pressed", "Button", MakeButtonBox(
            bgColor: new Color(0.165f, 0.05f, 0.31f),
            borderColor: new Color(0.75f, 0.08f, 0.48f)));

        theme.SetStylebox("focus", "Button", MakeButtonBox(
            bgColor: new Color(0.10f, 0.06f, 0.25f),
            borderColor: new Color(0.75f, 0.08f, 0.48f)));

        theme.SetStylebox("disabled", "Button", MakeButtonBox(
            bgColor: new Color(0.04f, 0.04f, 0.10f),
            borderColor: new Color(0.15f, 0.08f, 0.22f)));

        theme.SetColor("font_color", "Button", Colors.White);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(0.45f, 0.45f, 0.45f));
        theme.SetColor("font_outline_color", "Button", new Color(0f, 0f, 0f, 0.34f));
        theme.SetConstant("outline_size", "Button", 1);
        theme.SetFont("font", "Button", Regular);

        var panelBox = new StyleBoxFlat();
        panelBox.BgColor = new Color(0.07f, 0.07f, 0.165f);
        panelBox.BorderColor = new Color(0.12f, 0.12f, 0.29f);
        panelBox.BorderWidthTop = 1;
        panelBox.BorderWidthBottom = 1;
        panelBox.BorderWidthLeft = 1;
        panelBox.BorderWidthRight = 1;
        panelBox.CornerRadiusTopLeft = 4;
        panelBox.CornerRadiusTopRight = 4;
        panelBox.CornerRadiusBottomLeft = 4;
        panelBox.CornerRadiusBottomRight = 4;
        panelBox.ContentMarginLeft = 8;
        panelBox.ContentMarginRight = 8;
        panelBox.ContentMarginTop = 6;
        panelBox.ContentMarginBottom = 6;
        theme.SetStylebox("panel", "Panel", panelBox);

        theme.SetFont("font", "Label", Regular);
        theme.SetColor("font_outline_color", "Label", new Color(0f, 0f, 0f, 0.34f));
        theme.SetConstant("outline_size", "Label", 1);

        return theme;
    }

    /// <summary>
    /// Applies Rajdhani font overrides to any Control.
    /// </summary>
    public static void ApplyFont(Control control, bool semiBold = false, bool bold = false, int size = 0)
    {
        var font = bold ? Bold : (semiBold ? SemiBold : Regular);
        control.AddThemeFontOverride("font", font);
        control.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.34f));
        control.AddThemeConstantOverride("outline_size", bold ? 2 : 1);
        if (size > 0)
            control.AddThemeFontSizeOverride("font_size", size);
    }

    private static StyleBoxFlat MakeButtonBox(Color bgColor, Color borderColor)
    {
        var box = new StyleBoxFlat();
        box.BgColor = bgColor;
        box.BorderColor = borderColor;
        box.BorderWidthTop = 1;
        box.BorderWidthBottom = 1;
        box.BorderWidthLeft = 1;
        box.BorderWidthRight = 1;
        box.CornerRadiusTopLeft = 4;
        box.CornerRadiusTopRight = 4;
        box.CornerRadiusBottomLeft = 4;
        box.CornerRadiusBottomRight = 4;
        box.ContentMarginLeft = 8;
        box.ContentMarginRight = 8;
        box.ContentMarginTop = 6;
        box.ContentMarginBottom = 6;
        return box;
    }
}
