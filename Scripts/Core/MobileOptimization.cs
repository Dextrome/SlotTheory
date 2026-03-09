using Godot;
using SlotTheory.Core;

/// <summary>
/// Mobile-specific balance and optimization settings.
/// Activated when running on Android platform.
/// </summary>
public static class MobileOptimization
{
    // Performance settings for mobile devices
    public const int MaxParticles = 50;           // vs 100 on PC
    public const int ProjectileHistoryLength = 5; // vs 10 on PC
    public const bool EnableScreenShake = false;  // Battery saving
    public const float GlowRadius = 0.5f;         // vs 1.0 on PC
    public const int TargetFrameRate = 60;        // Can be lowered to 30 for battery

    // Touch input settings
    public const float TouchTargetMinSize = 44.0f; // dp (Android guideline)
    public const float TooltipHoldTime = 0.5f;     // seconds for tap-and-hold
    public const float LongPressTime = 0.8f;       // seconds for targeting cycle

    // UI scaling for different screen densities
    public const float PhoneUIScale = 1.45f;      // Larger touch targets
    public const float TabletUIScale = 1.20f;     // Slightly larger than desktop

    /// <summary>
    /// Check if we're running on a mobile platform
    /// </summary>
    public static bool IsMobile()
    {
        var os = OS.GetName();
        if (os == "Android" || os == "iOS")
            return true;

        // Web exports on phones report "Web", so rely on touch capability there.
        if (os == "Web" && DisplayServer.IsTouchscreenAvailable())
            return true;

        return OS.HasFeature("mobile");
    }

    /// <summary>
    /// Best-effort tablet detection used for layout decisions.
    /// </summary>
    public static bool IsTablet()
    {
        if (!IsMobile()) return false;

        var screenSize = DisplayServer.ScreenGetSize();
        int dpi = DisplayServer.ScreenGetDpi();

        if (dpi > 0)
        {
            float diagonalPx = Mathf.Sqrt(screenSize.X * screenSize.X + screenSize.Y * screenSize.Y);
            float diagonalInches = diagonalPx / dpi;
            return diagonalInches >= 7.0f;
        }

        var minDimension = Mathf.Min(screenSize.X, screenSize.Y);
        return minDimension >= 1200;
    }

    /// <summary>
    /// Get appropriate UI scale based on screen size
    /// </summary>
    public static float GetUIScale()
    {
        if (!IsMobile()) return 1.0f;
        return IsTablet() ? TabletUIScale : PhoneUIScale;
    }

    /// <summary>
    /// Get performance level based on device capabilities
    /// </summary>
    public static PerformanceLevel GetPerformanceLevel()
    {
        if (!IsMobile()) return PerformanceLevel.High;

        // Simple heuristic - can be made more sophisticated
        var displaySize = DisplayServer.ScreenGetSize();
        var pixelCount = displaySize.X * displaySize.Y;

        if (pixelCount > 2000000) // > 1920x1080
            return PerformanceLevel.Medium;
        else
            return PerformanceLevel.Low;
    }

    // ── Haptics ───────────────────────────────────────────────────────────

    /// <summary>Light tap — card pick, modifier equip, UI confirm.</summary>
    public static void HapticLight()
    {
        if (OS.GetName() != "Android") return;
        Input.VibrateHandheld(20, 0.35f);
    }

    /// <summary>Medium pulse — tower placement, wave clear.</summary>
    public static void HapticMedium()
    {
        if (OS.GetName() != "Android") return;
        Input.VibrateHandheld(60, 0.65f);
    }

    /// <summary>Strong thud — win, game over, life lost.</summary>
    public static void HapticStrong()
    {
        if (OS.GetName() != "Android") return;
        Input.VibrateHandheld(120, 1.0f);
    }

    // ── UI Scale ──────────────────────────────────────────────────────────

    /// <summary>
    /// Uniformly scales a top-level UI control on mobile only.
    /// </summary>
    public static void ApplyUIScale(Control root)
    {
        if (!IsMobile()) return;

        float scale = GetUIScale();
        if (Mathf.IsEqualApprox(scale, 1.0f)) return;

        root.PivotOffset = root.Size * 0.5f;
        root.Scale = new Vector2(scale, scale);
    }

    /// <summary>
    /// Creates a full-rect CenterContainer scaled for the current device,
    /// adds it to <paramref name="parent"/>, and attaches a PinchZoomHandler.
    /// Use this as the root content node for every new UI screen.
    /// </summary>
    public static CenterContainer MakeScaledRoot(Node parent, bool pinchZoom = true)
    {
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = UITheme.Build();
        parent.AddChild(center);
        ApplyUIScale(center);
        if (pinchZoom)
            parent.AddChild(new PinchZoomHandler(center));
        return center;
    }
}

public enum PerformanceLevel
{
    Low,    // Reduce effects significantly
    Medium, // Some effect reduction
    High    // Full effects
}
