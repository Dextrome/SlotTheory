using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Adds touch drag-scrolling to a ScrollContainer on mobile.
/// Uses _Input (fires before GUI processing) so drags that begin on interactive
/// children (sliders, buttons) still scroll the container once the threshold is crossed.
/// </summary>
public static class TouchScrollHelper
{
    public static void EnableDragScroll(ScrollContainer scroll, bool allowHorizontal = false)
    {
        if (!MobileOptimization.IsMobile())
            return;

        scroll.AddChild(new DragScrollInterceptor(scroll, allowHorizontal));
    }
}

internal sealed partial class DragScrollInterceptor : Node
{
    private readonly ScrollContainer _scroll;
    private readonly bool _allowHorizontal;

    private bool   _dragging;
    private bool   _scrollMode;
    private int    _pointerId = -1;
    private Vector2 _startPos;
    private Vector2 _lastPos;

    private const float DragThreshold = 8f;

    // Parameterless ctor required by Godot's C# binding
    public DragScrollInterceptor() : this(null!, false) { }

    public DragScrollInterceptor(ScrollContainer scroll, bool allowHorizontal)
    {
        _scroll          = scroll;
        _allowHorizontal = allowHorizontal;
    }

    public override void _Ready() => SetProcessInput(true);

    public override void _Input(InputEvent @event)
    {
        if (_scroll == null) return;

        switch (@event)
        {
            case InputEventScreenTouch touch:
                if (touch.Pressed && !_dragging)
                {
                    // Only track touches that start inside the scroll container
                    if (!_scroll.GetGlobalRect().HasPoint(touch.Position))
                        break;

                    _dragging   = true;
                    _scrollMode = false;
                    _pointerId  = touch.Index;
                    _startPos   = touch.Position;
                    _lastPos    = touch.Position;
                }
                else if (!touch.Pressed && _dragging && _pointerId == touch.Index)
                {
                    bool wasScrolling = _scrollMode;
                    _dragging   = false;
                    _scrollMode = false;
                    _pointerId  = -1;
                    // Consume the release so a button under the finger doesn't fire after a scroll
                    if (wasScrolling)
                        GetViewport().SetInputAsHandled();
                }
                break;

            case InputEventScreenDrag drag when _dragging && drag.Index == _pointerId:
            {
                if (!_scrollMode)
                {
                    var total = drag.Position - _startPos;
                    bool crossed = Mathf.Abs(total.Y) > DragThreshold ||
                                   (_allowHorizontal && Mathf.Abs(total.X) > DragThreshold);
                    if (crossed) _scrollMode = true;
                }

                if (_scrollMode)
                {
                    ApplyDelta(drag.Position - _lastPos);
                    GetViewport().SetInputAsHandled();
                }

                _lastPos = drag.Position;
                break;
            }
        }
    }

    private void ApplyDelta(Vector2 delta)
    {
        if (_allowHorizontal)
            _scroll.ScrollHorizontal = Mathf.Max(0, _scroll.ScrollHorizontal - Mathf.RoundToInt(delta.X));
        _scroll.ScrollVertical = Mathf.Max(0, _scroll.ScrollVertical - Mathf.RoundToInt(delta.Y));
    }
}
