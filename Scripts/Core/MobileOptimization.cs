using Godot;

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
    public const float GlowRadius = 0.5f;        // vs 1.0 on PC
    public const int TargetFrameRate = 60;       // Can be lowered to 30 for battery
    
    // Touch input settings  
    public const float TouchTargetMinSize = 44.0f; // dp (Android guideline)
    public const float TooltipHoldTime = 0.5f;     // seconds for tap-and-hold
    public const float LongPressTime = 0.8f;       // seconds for targeting cycle
    
    // UI scaling for different screen densities
    public const float PhoneUIScale = 1.2f;        // Larger touch targets
    public const float TabletUIScale = 1.0f;       // Normal size for tablets
    
    /// <summary>
    /// Check if we're running on a mobile platform
    /// </summary>
    public static bool IsMobile()
    {
        return OS.GetName() == "Android" || OS.GetName() == "iOS";
    }
    
    /// <summary>
    /// Get appropriate UI scale based on screen size
    /// </summary>
    public static float GetUIScale()
    {
        if (!IsMobile()) return 1.0f;
        
        var screenSize = DisplayServer.ScreenGetSize();
        var minDimension = Mathf.Min(screenSize.X, screenSize.Y);
        
        // Assume tablet if shortest dimension > 800px
        return minDimension > 800 ? TabletUIScale : PhoneUIScale;
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
}

public enum PerformanceLevel
{
    Low,    // Reduce effects significantly
    Medium, // Some effect reduction  
    High    // Full effects
}