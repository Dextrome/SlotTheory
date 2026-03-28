using Godot;

/// <summary>
/// Mobile-specific input handling and touch detection.
/// Handles tap-and-hold for tooltips, long press for targeting, etc.
/// </summary>
public partial class MobileInput : Node
{
    [Signal] public delegate void TapAndHoldEventHandler(Vector2 position);
    [Signal] public delegate void LongPressEventHandler(Vector2 position);
    [Signal] public delegate void DoubleTapEventHandler(Vector2 position);
    
    private Vector2 _touchStartPosition;
    private double _touchStartTime;
    private bool _isTouching;
    private bool _tapAndHoldFired;
    private bool _longPressFired;
    
    // Touch timing thresholds
    private const float TAP_AND_HOLD_TIME = 0.5f;  // For tooltips
    private const float LONG_PRESS_TIME = 0.8f;    // For targeting mode cycle
    private const float DOUBLE_TAP_TIME = 0.3f;    // For future features
    private const float MAX_TOUCH_DRIFT = 30.0f;   // px movement allowance
    
    private double _lastTapTime;
    private Vector2 _lastTapPosition;
    
    public override void _Ready()
    {
        // Only activate on mobile platforms
        if (!MobileOptimization.IsMobile())
        {
            SetProcess(false);
            return;
        }
        
        GD.Print("[MobileInput] Activated for platform: " + OS.GetName());
    }
    
    public override void _Input(InputEvent @event)
    {
        if (!MobileOptimization.IsMobile()) return;
        
        switch (@event)
        {
            case InputEventScreenTouch touch:
                HandleScreenTouch(touch);
                break;
            case InputEventScreenDrag drag:
                HandleScreenDrag(drag);
                break;
        }
    }
    
    private void HandleScreenTouch(InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            // Touch started
            _touchStartPosition = touch.Position;
            _touchStartTime = Time.GetUnixTimeFromSystem();
            _isTouching = true;
            _tapAndHoldFired = false;
            _longPressFired = false;
        }
        else
        {
            // Touch released
            if (_isTouching)
            {
                double touchDuration = Time.GetUnixTimeFromSystem() - _touchStartTime;
                Vector2 touchDrift = touch.Position - _touchStartPosition;
                
                // Check for double tap
                if (touchDuration < DOUBLE_TAP_TIME && touchDrift.Length() <= MAX_TOUCH_DRIFT)
                {
                    double timeSinceLastTap = Time.GetUnixTimeFromSystem() - _lastTapTime;
                    Vector2 distanceFromLastTap = touch.Position - _lastTapPosition;
                    
                    if (timeSinceLastTap <= DOUBLE_TAP_TIME && distanceFromLastTap.Length() <= MAX_TOUCH_DRIFT)
                    {
                        EmitSignal(SignalName.DoubleTap, touch.Position);
                        GD.Print("[MobileInput] Double tap at: " + touch.Position);
                    }
                }
                
                _lastTapTime = Time.GetUnixTimeFromSystem();
                _lastTapPosition = touch.Position;
            }
            
            _isTouching = false;
        }
    }
    
    private void HandleScreenDrag(InputEventScreenDrag drag)
    {
        if (!_isTouching) return;
        
        Vector2 touchDrift = drag.Position - _touchStartPosition;
        if (touchDrift.Length() > MAX_TOUCH_DRIFT)
        {
            // Too much movement - cancel hold/press detection
            _tapAndHoldFired = true;
            _longPressFired = true;
        }
    }
    
    public override void _Process(double delta)
    {
        if (!_isTouching) return;
        
        double touchDuration = Time.GetUnixTimeFromSystem() - _touchStartTime;
        
        // Check for tap-and-hold (tooltip trigger)
        if (!_tapAndHoldFired && touchDuration >= TAP_AND_HOLD_TIME)
        {
            _tapAndHoldFired = true;
            EmitSignal(SignalName.TapAndHold, _touchStartPosition);
            GD.Print("[MobileInput] Tap-and-hold at: " + _touchStartPosition);
        }
        
        // Check for long press (targeting mode cycle)
        if (!_longPressFired && touchDuration >= LONG_PRESS_TIME)
        {
            _longPressFired = true;
            EmitSignal(SignalName.LongPress, _touchStartPosition);
            GD.Print("[MobileInput] Long press at: " + _touchStartPosition);
        }
    }
    
    /// <summary>
    /// Test if a position is within touch target guidelines (44dp minimum)
    /// </summary>
    public static bool IsValidTouchTarget(Rect2 area)
    {
        float minSize = MobileOptimization.TouchTargetMinSize * MobileOptimization.GetUIScale();
        return area.Size.X >= minSize && area.Size.Y >= minSize;
    }
    
    /// <summary>
    /// Scale a UI element for mobile touch accessibility
    /// </summary>
    public static Vector2 GetMobileTouchSize(Vector2 originalSize)
    {
        if (!MobileOptimization.IsMobile()) return originalSize;
        
        float scale = MobileOptimization.GetUIScale();
        float minSize = MobileOptimization.TouchTargetMinSize;
        
        Vector2 scaledSize = originalSize * scale;
        scaledSize.X = Mathf.Max(scaledSize.X, minSize);
        scaledSize.Y = Mathf.Max(scaledSize.Y, minSize);
        
        return scaledSize;
    }
}