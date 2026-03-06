using Godot;

namespace SlotTheory.UI;

/// <summary>
/// Adds touch/mouse drag scrolling behavior for ScrollContainer on mobile.
/// </summary>
public static class TouchScrollHelper
{
    public static void EnableDragScroll(ScrollContainer scroll, bool allowHorizontal = false)
    {
        if (!MobileOptimization.IsMobile())
            return;

        bool dragging = false;
        bool consumedDrag = false;
        int pointerId = -1; // >= 0 touch index, -2 mouse
        Vector2 lastPos = Vector2.Zero;

        scroll.GuiInput += (@event) =>
        {
            switch (@event)
            {
                case InputEventScreenTouch touch:
                    if (touch.Pressed)
                    {
                        if (!dragging)
                        {
                            dragging = true;
                            consumedDrag = false;
                            pointerId = touch.Index;
                            lastPos = touch.Position;
                        }
                        scroll.AcceptEvent();
                        return;
                    }

                    if (dragging && pointerId == touch.Index)
                    {
                        dragging = false;
                        pointerId = -1;
                        if (consumedDrag)
                            scroll.AcceptEvent();
                    }
                    return;

                case InputEventScreenDrag drag when dragging && pointerId == drag.Index:
                    ApplyDelta(scroll, drag.Position - lastPos, allowHorizontal);
                    lastPos = drag.Position;
                    consumedDrag = true;
                    scroll.AcceptEvent();
                    return;

                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                    if (mb.Pressed)
                    {
                        dragging = true;
                        consumedDrag = false;
                        pointerId = -2;
                        lastPos = mb.Position;
                    }
                    else if (dragging && pointerId == -2)
                    {
                        dragging = false;
                        pointerId = -1;
                        if (consumedDrag)
                            scroll.AcceptEvent();
                    }
                    return;

                case InputEventMouseMotion mm when dragging && pointerId == -2 && (mm.ButtonMask & MouseButtonMask.Left) != 0:
                    ApplyDelta(scroll, mm.Position - lastPos, allowHorizontal);
                    lastPos = mm.Position;
                    consumedDrag = true;
                    scroll.AcceptEvent();
                    return;
            }
        };
    }

    private static void ApplyDelta(ScrollContainer scroll, Vector2 delta, bool allowHorizontal)
    {
        if (allowHorizontal)
            scroll.ScrollHorizontal = Mathf.Max(0, scroll.ScrollHorizontal - Mathf.RoundToInt(delta.X));
        scroll.ScrollVertical = Mathf.Max(0, scroll.ScrollVertical - Mathf.RoundToInt(delta.Y));
    }
}

