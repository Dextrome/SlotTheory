using System;
using System.Collections.Generic;
using Godot;
using SlotTheory.Core;

namespace SlotTheory.UI;

/// <summary>
/// Which entity type the user is currently placing.
/// Selection and drag work for both types regardless of mode.
/// </summary>
public enum EditorMode { Waypoint, Slot }

/// <summary>
/// Immutable snapshot of canvas state -- used by the panel for undo/redo.
/// </summary>
public sealed class MapStateSnapshot
{
    public List<Vector2> Waypoints { get; }
    public List<Vector2> Slots     { get; }

    public MapStateSnapshot(List<Vector2> wp, List<Vector2> sl)
    {
        Waypoints = new List<Vector2>(wp);
        Slots     = new List<Vector2>(sl);
    }
}

/// <summary>
/// Interactive canvas that renders the map in editor mode and handles mouse input.
/// The panel owns the undo stack; the canvas fires <see cref="CommitChange"/> with a
/// pre-change snapshot so the panel can push it to undo without needing extra hooks.
/// </summary>
public partial class MapEditorCanvas : Control
{
    // ── Palette (mirrors MapPreviewControl) ─────────────────────────────────
    private static readonly Color BgColor       = new(0.012f, 0.018f, 0.052f);
    private static readonly Color GridColor      = new(0.14f,  0.16f,  0.30f,  0.40f);
    private static readonly Color GridAccent     = new(0.22f,  0.26f,  0.46f,  0.28f);  // every 4 cells
    private static readonly Color BoundsBorder   = new(0.28f,  0.32f,  0.55f,  0.60f);
    private static readonly Color PathGlow       = new(0.72f,  0.01f,  0.50f,  0.08f);
    private static readonly Color PathCore       = new(0.06f,  0.00f,  0.12f,  0.97f);
    private static readonly Color PathEdge       = new(1.00f,  0.27f,  0.70f,  0.78f);
    private static readonly Color WaypointColor  = new(0.88f,  0.88f,  0.92f,  1.00f);
    private static readonly Color WaypointSpawn  = new(0.30f,  0.96f,  0.44f,  1.00f);
    private static readonly Color WaypointExit   = new(0.96f,  0.28f,  0.28f,  1.00f);
    private static readonly Color SelectedColor  = new(0.10f,  0.90f,  1.00f,  1.00f);
    private static readonly Color HoverAddColor  = new(0.65f,  0.85f,  0.30f,  0.55f);  // lime ghost
    private static readonly Color SlotColor      = new(0.90f,  0.85f,  0.28f,  0.95f);
    private static readonly Color SlotSelected   = new(0.10f,  0.90f,  1.00f,  1.00f);
    private static readonly Color LabelColor     = new(0.78f,  0.90f,  1.00f,  0.90f);
    private static readonly Color ErrorHighlight = new(1.00f,  0.22f,  0.22f,  0.70f);

    // ── World dimensions (matches GameController playfield) ────────────────
    private const float WorldW = MapBounds.WorldMaxX;
    private const float WorldH = MapBounds.WorldMaxY;

    // ── Public state ───────────────────────────────────────────────────────
    public EditorMode Mode { get; private set; } = EditorMode.Waypoint;
    public bool SnapEnabled { get; private set; } = true;
    public float SnapSize   { get; private set; } = 80f;
    public bool GridVisible { get; private set; } = true;

    /// <summary>
    /// Fired when a committed change is made. Argument is the state BEFORE the change
    /// so the panel can push it to the undo stack.
    /// </summary>
    public event Action<MapStateSnapshot>? CommitChange;

    // ── Internal mutable state ─────────────────────────────────────────────
    private readonly List<Vector2> _waypoints = new();
    private readonly List<Vector2> _slots     = new();

    // Interaction
    private Vector2 _hoverWorld   = Vector2.Zero;
    private bool    _isDragging   = false;
    private bool    _dragIsSlot   = false;
    private int     _dragIdx      = -1;
    private bool    _hasDragMoved = false;
    private MapStateSnapshot? _preDragSnapshot;

    // Visual selection
    private int  _selectedIdx    = -1;
    private bool _selectedIsSlot = false;

    // Validation error highlights (set by panel via SetErrorHighlights)
    private readonly List<int> _errorWaypointIdxs = new();
    private readonly List<int> _errorSlotIdxs     = new();

    // ── Public setup API ───────────────────────────────────────────────────

    public void SetMode(EditorMode mode)
    {
        Mode = mode;
        _selectedIdx = -1;
        QueueRedraw();
    }

    public void SetSnapEnabled(bool enabled) { SnapEnabled = enabled; QueueRedraw(); }
    public void SetGridVisible(bool visible) { GridVisible = visible; QueueRedraw(); }

    /// <summary>Replace the canvas state from an external source (load or undo/redo).</summary>
    public void RestoreSnapshot(MapStateSnapshot snap)
    {
        _waypoints.Clear(); _waypoints.AddRange(snap.Waypoints);
        _slots.Clear();     _slots.AddRange(snap.Slots);
        _selectedIdx = -1;
        _isDragging = false;
        QueueRedraw();
    }

    /// <summary>Capture the current state as an immutable snapshot.</summary>
    public MapStateSnapshot CaptureSnapshot() => new(_waypoints, _slots);

    public IReadOnlyList<Vector2> Waypoints => _waypoints;
    public IReadOnlyList<Vector2> Slots     => _slots;

    /// <summary>Delete the currently selected item (called by keyboard shortcut).</summary>
    public void DeleteSelected()
    {
        if (_selectedIdx < 0) return;
        var before = CaptureSnapshot();
        if (_selectedIsSlot)
        {
            if (_selectedIdx < _slots.Count)
                _slots.RemoveAt(_selectedIdx);
        }
        else
        {
            if (_selectedIdx < _waypoints.Count)
                _waypoints.RemoveAt(_selectedIdx);
        }
        _selectedIdx = -1;
        CommitChange?.Invoke(before);
        QueueRedraw();
    }

    /// <summary>Clear all waypoints.</summary>
    public void ClearWaypoints()
    {
        if (_waypoints.Count == 0) return;
        var before = CaptureSnapshot();
        _waypoints.Clear();
        _selectedIdx = -1;
        CommitChange?.Invoke(before);
        QueueRedraw();
    }

    /// <summary>Clear all slots.</summary>
    public void ClearSlots()
    {
        if (_slots.Count == 0) return;
        var before = CaptureSnapshot();
        _slots.Clear();
        _selectedIdx = -1;
        CommitChange?.Invoke(before);
        QueueRedraw();
    }

    /// <summary>Highlight specific waypoint/slot indices as validation errors.</summary>
    public void SetErrorHighlights(List<int> wpErrors, List<int> slotErrors)
    {
        _errorWaypointIdxs.Clear(); _errorWaypointIdxs.AddRange(wpErrors);
        _errorSlotIdxs.Clear();     _errorSlotIdxs.AddRange(slotErrors);
        QueueRedraw();
    }

    // ── Godot lifecycle ────────────────────────────────────────────────────

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode   = FocusModeEnum.Click;
        ClipContents = true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
            HandleMouseButton(mb);
        else if (@event is InputEventMouseMotion mm)
            HandleMouseMotion(mm);
    }

    // ── Input handling ─────────────────────────────────────────────────────

    private void HandleMouseButton(InputEventMouseButton mb)
    {
        Vector2 worldPos   = ClampToEditable(SnapIfNeeded(ToWorld(mb.Position)));
        Vector2 canvasPos  = mb.Position;

        if (mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                // Check if near an existing item to start drag
                int wpIdx = FindNearestWaypoint(canvasPos);
                int slIdx = FindNearestSlot(canvasPos);

                if (wpIdx >= 0)
                {
                    _selectedIdx = wpIdx; _selectedIsSlot = false;
                    StartDrag(wpIdx, isSlot: false);
                }
                else if (slIdx >= 0)
                {
                    _selectedIdx = slIdx; _selectedIsSlot = true;
                    StartDrag(slIdx, isSlot: true);
                }
                else
                {
                    // Add new item
                    var before = CaptureSnapshot();
                    bool changed = false;
                    if (Mode == EditorMode.Waypoint)
                    {
                        _waypoints.Add(worldPos);
                        _selectedIdx = _waypoints.Count - 1; _selectedIsSlot = false;
                        changed = true;
                    }
                    else if (Mode == EditorMode.Slot && _slots.Count < 6)
                    {
                        _slots.Add(worldPos);
                        _selectedIdx = _slots.Count - 1; _selectedIsSlot = true;
                        changed = true;
                    }
                    if (changed)
                    {
                        CommitChange?.Invoke(before);
                        QueueRedraw();
                    }
                }
            }
            else // released
            {
                if (_isDragging)
                    EndDrag();
            }
        }
        else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            // Right-click: delete nearest item based on active mode (or either if one is clearly closer)
            int wpIdx = FindNearestWaypoint(canvasPos, 20f);
            int slIdx = FindNearestSlot(canvasPos, 20f);
            bool deleteWp = wpIdx >= 0 && (slIdx < 0 || Mode == EditorMode.Waypoint);
            bool deleteSl = slIdx >= 0 && (wpIdx < 0 || Mode == EditorMode.Slot);

            // If both are equally close, prefer the active mode
            if (wpIdx >= 0 && slIdx >= 0)
            {
                deleteWp = Mode == EditorMode.Waypoint;
                deleteSl = Mode == EditorMode.Slot;
            }

            if (deleteWp)
            {
                var before = CaptureSnapshot();
                _waypoints.RemoveAt(wpIdx);
                if (_selectedIdx == wpIdx && !_selectedIsSlot) _selectedIdx = -1;
                CommitChange?.Invoke(before);
                QueueRedraw();
            }
            else if (deleteSl)
            {
                var before = CaptureSnapshot();
                _slots.RemoveAt(slIdx);
                if (_selectedIdx == slIdx && _selectedIsSlot) _selectedIdx = -1;
                CommitChange?.Invoke(before);
                QueueRedraw();
            }
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mm)
    {
        _hoverWorld = ClampToEditable(SnapIfNeeded(ToWorld(mm.Position)));

        if (_isDragging)
        {
            Vector2 snappedPos = ClampToEditable(SnapIfNeeded(ToWorld(mm.Position)));
            if (_dragIsSlot)
                _slots[_dragIdx] = snappedPos;
            else
                _waypoints[_dragIdx] = snappedPos;
            _hasDragMoved = true;
        }

        QueueRedraw();
    }

    private void StartDrag(int idx, bool isSlot)
    {
        _isDragging = true;
        _dragIsSlot = isSlot;
        _dragIdx    = idx;
        _hasDragMoved = false;
        _preDragSnapshot = CaptureSnapshot();
    }

    private void EndDrag()
    {
        _isDragging = false;
        if (_hasDragMoved && _preDragSnapshot != null)
        {
            CommitChange?.Invoke(_preDragSnapshot);
        }
        _preDragSnapshot = null;
        _hasDragMoved = false;
        QueueRedraw();
    }

    // ── Hit testing (canvas space) ─────────────────────────────────────────

    private int FindNearestWaypoint(Vector2 canvasPos, float maxDist = 18f)
    {
        int best = -1; float bestDist = maxDist;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            float d = ToCanvas(_waypoints[i]).DistanceTo(canvasPos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private int FindNearestSlot(Vector2 canvasPos, float maxDist = 18f)
    {
        int best = -1; float bestDist = maxDist;
        for (int i = 0; i < _slots.Count; i++)
        {
            float d = ToCanvas(_slots[i]).DistanceTo(canvasPos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    // ── Coordinate transforms ──────────────────────────────────────────────

    private Vector2 ToCanvas(Vector2 world) =>
        new(world.X * Size.X / WorldW, world.Y * Size.Y / WorldH);

    private Vector2 ToWorld(Vector2 canvas) =>
        new(canvas.X * WorldW / Size.X, canvas.Y * WorldH / Size.Y);

    private Vector2 SnapIfNeeded(Vector2 world) => SnapEnabled
        ? new Vector2(MathF.Round(world.X / SnapSize) * SnapSize,
                      MathF.Round(world.Y / SnapSize) * SnapSize)
        : world;

    private static Vector2 ClampToEditable(Vector2 world) => MapBounds.ClampToEditable(world);

    // ── Drawing ────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        var size = Size;
        if (size.X < 4f || size.Y < 4f) return;

        DrawRect(new Rect2(Vector2.Zero, size), BgColor);

        if (GridVisible) DrawGrid(size);

        // World bounds indicator
        DrawRect(new Rect2(Vector2.Zero, size),
                 new Color(BoundsBorder.R, BoundsBorder.G, BoundsBorder.B, 0.40f),
                 filled: false, width: 1.2f);

        // Editable placement bounds (matches authored map limits).
        Vector2 editMin = ToCanvas(new Vector2(MapBounds.EditableMinX, MapBounds.EditableMinY));
        Vector2 editMax = ToCanvas(new Vector2(MapBounds.EditableMaxX, MapBounds.EditableMaxY));
        DrawRect(new Rect2(editMin, editMax - editMin), BoundsBorder, filled: false, width: 1.8f);

        DrawPath();
        DrawSlots();
        DrawWaypoints();
        DrawHoverPreview();
    }

    private void DrawGrid(Vector2 size)
    {
        float scaleX = size.X / WorldW;
        float scaleY = size.Y / WorldH;

        // Vertical lines
        for (float wx = 0; wx <= WorldW; wx += SnapSize)
        {
            float cx = wx * scaleX;
            bool isAccent = ((int)(wx / SnapSize) % 4) == 0;
            DrawLine(new Vector2(cx, 0), new Vector2(cx, size.Y),
                     isAccent ? GridAccent : GridColor, 1f);
        }

        // Horizontal lines
        for (float wy = 0; wy <= WorldH; wy += SnapSize)
        {
            float cy = wy * scaleY;
            bool isAccent = ((int)(wy / SnapSize) % 4) == 0;
            DrawLine(new Vector2(0, cy), new Vector2(size.X, cy),
                     isAccent ? GridAccent : GridColor, 1f);
        }
    }

    private void DrawPath()
    {
        if (_waypoints.Count < 2) return;

        var pts = new Vector2[_waypoints.Count];
        for (int i = 0; i < _waypoints.Count; i++)
            pts[i] = ToCanvas(_waypoints[i]);

        float pathW = Mathf.Clamp(Size.Y / WorldH * 70f, 4f, 20f);
        DrawPolyline(pts, PathGlow,  pathW * 1.6f, antialiased: true);
        DrawPolyline(pts, PathCore,  pathW,         antialiased: true);
        DrawPolyline(pts, PathEdge,  pathW * 0.12f, antialiased: true);
    }

    private void DrawWaypoints()
    {
        var font     = ThemeDB.FallbackFont;
        float radius = Mathf.Clamp(Size.Y / WorldH * 14f, 5f, 12f);

        for (int i = 0; i < _waypoints.Count; i++)
        {
            Vector2 cp = ToCanvas(_waypoints[i]);
            bool isSelected = (_selectedIdx == i && !_selectedIsSlot);
            bool isError    = _errorWaypointIdxs.Contains(i);

            Color col = i == 0                   ? WaypointSpawn
                      : i == _waypoints.Count - 1 ? WaypointExit
                      : WaypointColor;

            if (isSelected) col = SelectedColor;
            if (isError)    col = ErrorHighlight;

            // Shadow
            DrawCircle(cp, radius + 2f, new Color(0f, 0f, 0f, 0.5f));
            DrawCircle(cp, radius,      col);

            // Selected ring
            if (isSelected)
                DrawArc(cp, radius + 3f, 0f, Mathf.Tau, 32,
                        new Color(SelectedColor.R, SelectedColor.G, SelectedColor.B, 0.7f), 1.5f);

            // Number label
            int fontSize = Mathf.RoundToInt(radius * 1.3f);
            DrawString(font, cp + new Vector2(radius + 3f, fontSize * 0.4f),
                       (i + 1).ToString(), HorizontalAlignment.Left,
                       -1, fontSize, LabelColor);
        }
    }

    private void DrawSlots()
    {
        var font   = ThemeDB.FallbackFont;
        float half = Mathf.Clamp(Size.Y / WorldH * 16f, 5f, 11f);

        for (int i = 0; i < _slots.Count; i++)
        {
            Vector2 cp = ToCanvas(_slots[i]);
            bool isSelected = (_selectedIdx == i && _selectedIsSlot);
            bool isError    = _errorSlotIdxs.Contains(i);

            Color col = isSelected ? SlotSelected
                      : isError    ? ErrorHighlight
                      : SlotColor;

            DrawRect(new Rect2(cp - Vector2.One * (half + 2f), Vector2.One * (half * 2 + 4f)),
                     new Color(0f, 0f, 0f, 0.5f));
            DrawRect(new Rect2(cp - Vector2.One * half, Vector2.One * (half * 2)), col);

            if (isSelected)
                DrawRect(new Rect2(cp - Vector2.One * (half + 2.5f), Vector2.One * (half * 2 + 5f)),
                         new Color(SelectedColor.R, SelectedColor.G, SelectedColor.B, 0.7f),
                         filled: false, width: 1.5f);

            int fontSize = Mathf.RoundToInt(half * 1.2f);
            DrawString(font, cp + new Vector2(half + 3f, fontSize * 0.4f),
                       $"S{i + 1}", HorizontalAlignment.Left,
                       -1, fontSize, LabelColor);
        }
    }

    private void DrawHoverPreview()
    {
        // Only show hover ghost when cursor is in bounds and not hovering existing items
        if (FindNearestWaypoint(ToCanvas(SnapIfNeeded(ToWorld(GetLocalMousePosition())))) >= 0) return;
        if (FindNearestSlot(ToCanvas(SnapIfNeeded(ToWorld(GetLocalMousePosition())))) >= 0) return;

        Vector2 cp = ToCanvas(_hoverWorld);

        if (Mode == EditorMode.Waypoint)
        {
            float radius = Mathf.Clamp(Size.Y / WorldH * 14f, 5f, 12f);
            DrawCircle(cp, radius, HoverAddColor);
        }
        else if (Mode == EditorMode.Slot && _slots.Count < 6)
        {
            float half = Mathf.Clamp(Size.Y / WorldH * 16f, 5f, 11f);
            DrawRect(new Rect2(cp - Vector2.One * half, Vector2.One * (half * 2)),
                     HoverAddColor);
        }
    }
}
