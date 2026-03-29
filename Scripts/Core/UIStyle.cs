using Godot;

namespace SlotTheory.Core;

/// <summary>
/// Canonical UI tokens for cohesion across menus, overlays, and HUD surfaces.
/// Keep style primitives centralized here and reference this class from feature UI code.
/// </summary>
public static class UIStyle
{
    public enum TowerAccentVariant
    {
        Ui,
        Projectile,
        Body,
    }

    public static class TypeScale
    {
        public const int Micro = 11;
        public const int Caption = 12;
        public const int BodySm = 13;
        public const int Body = 14;
        public const int BodyLg = 15;
        public const int Label = 16;
        public const int HeadingSm = 18;
        public const int Heading = 20;
        public const int HeadingLg = 22;
        public const int Hero = 28;
        public const int Display = 72;
    }

    public static class Spacing
    {
        public const int Xs = 2;
        public const int Sm = 4;
        public const int Md = 6;
        public const int Lg = 8;
        public const int Xl = 10;
        public const int Xxl = 12;
        public const int Xxxl = 14;
        public const int Jumbo = 16;
        public const int Mega = 18;
        public const int Edge = 24;
    }

    public static class Radius
    {
        public const int Sm = 5;
        public const int Md = 6;
        public const int Lg = 8;
        public const int Xl = 10;
    }

    public static Color TowerAccent(string towerId, TowerAccentVariant variant = TowerAccentVariant.Ui)
    {
        return variant switch
        {
            TowerAccentVariant.Projectile => TowerProjectileAccent(towerId),
            TowerAccentVariant.Body => TowerBodyAccent(towerId),
            _ => TowerUiAccent(towerId),
        };
    }

    private static Color TowerUiAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.25f, 0.92f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.60f, 0.18f),
        "rocket_launcher" => new Color(1.00f, 0.54f, 0.14f),
        "marker_tower" => new Color(1.00f, 0.30f, 0.72f),
        "chain_tower" => new Color(0.62f, 0.90f, 1.00f),
        "rift_prism" => new Color(0.60f, 1.00f, 0.58f),
        "accordion_engine" => new Color(0.78f, 0.40f, 1.00f),
        "phase_splitter" => new Color(0.45f, 1.00f, 0.95f),
        "undertow_engine" => new Color(0.08f, 0.64f, 0.86f),
        "latch_nest" => new Color(0.64f, 0.92f, 0.50f),
        _ => new Color(0.75f, 0.85f, 1.00f),
    };

    private static Color TowerProjectileAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.30f, 0.90f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.55f, 0.00f),
        "rocket_launcher" => new Color(1.00f, 0.58f, 0.14f),
        "marker_tower" => new Color(0.75f, 0.30f, 1.00f),
        "chain_tower" => new Color(0.55f, 0.90f, 1.00f),
        "rift_prism" => new Color(0.70f, 1.00f, 0.56f),
        "accordion_engine" => new Color(0.78f, 0.40f, 1.00f),
        "phase_splitter" => new Color(0.45f, 1.00f, 0.95f),
        "undertow_engine" => new Color(0.08f, 0.64f, 0.86f),
        "latch_nest" => new Color(0.70f, 0.98f, 0.56f),
        _ => Colors.Yellow,
    };

    private static Color TowerBodyAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.15f, 0.65f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.55f, 0.00f),
        "rocket_launcher" => new Color(0.96f, 0.36f, 0.10f),
        "marker_tower" => new Color(1.00f, 0.15f, 0.60f),
        "chain_tower" => new Color(0.50f, 0.85f, 1.00f),
        "rift_prism" => new Color(0.58f, 0.98f, 0.50f),
        "accordion_engine" => new Color(0.72f, 0.20f, 1.00f),
        "phase_splitter" => new Color(0.36f, 0.92f, 0.88f),
        "undertow_engine" => new Color(0.02f, 0.56f, 0.78f),
        "latch_nest" => new Color(0.56f, 0.90f, 0.46f),
        _ => new Color(0.20f, 0.50f, 1.00f),
    };
}
