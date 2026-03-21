using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen in-game map creator/editor.
/// Layout: header bar | left panel (tools + metadata) | center canvas | right panel (map list).
/// All UI is built procedurally -- no .tscn children required.
/// </summary>
public partial class MapEditorPanel : Node
{
    // ── Children ───────────────────────────────────────────────────────────
    private MapEditorCanvas _canvas = null!;

    // Toolbar
    private Label  _mapNameHeaderLabel = null!;
    private Label  _unsavedDot         = null!;
    private Button _validateBtn        = null!;
    private Button _playtestBtn        = null!;
    private Button _saveBtn            = null!;

    // Left panel
    private Button _waypointModeBtn = null!;
    private Button _slotModeBtn     = null!;
    private Button _undoBtn         = null!;
    private Button _redoBtn         = null!;
    private Label  _statusLabel     = null!;
    private LineEdit _nameField     = null!;
    private TextEdit _descField     = null!;

    // Map dropdown (replaces right panel)
    private PopupPanel    _mapDropdown   = null!;
    private VBoxContainer _dropdownList  = null!;
    private LineEdit      _dropdownSearch = null!;
    private Button        _openMenuBtn   = null!;   // anchor for dropdown positioning

    // File dialogs
    private FileDialog _exportDialog = null!;
    private FileDialog _importDialog = null!;

    // ── State ───────────────────────────────────────────────────────────────
    private string  _editingMapId    = "";
    private bool    _hasUnsavedChanges = false;

    private readonly Stack<MapStateSnapshot> _undoStack = new();
    private readonly Stack<MapStateSnapshot> _redoStack = new();
    private const int MaxUndoDepth = 50;

    // ── Build UI ────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        DataLoader.LoadAll();
        DataLoader.RegisterCustomMaps(CustomMapManager.LoadAll());

        // Build UI first so all node references are valid before loading map data
        BuildUI();

        // Restore last-edited map if returning from playtest, else start fresh
        if (MapEditorState.LastEditedMapId is { } lastId && lastId.Length > 0)
            LoadMap(lastId);
        else
            NewMap();

        RefreshMapList();
        UpdateToolButtons();
        UpdateUndoButtons();
        UpdateHeaderName();
        UpdateStatusBar();
    }

    // ── Keyboard shortcuts ──────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_canvas == null) return;
        if (@event is not InputEventKey key || !key.Pressed) return;

        bool ctrl = key.CtrlPressed || key.MetaPressed;

        if (ctrl && key.ShiftPressed && key.Keycode == Key.Z) { Redo(); GetViewport().SetInputAsHandled(); }
        else if (ctrl && key.Keycode == Key.Z) { Undo(); GetViewport().SetInputAsHandled(); }
        else if (ctrl && key.Keycode == Key.Y) { Redo(); GetViewport().SetInputAsHandled(); }
        else if (ctrl && key.Keycode == Key.S)  { OnSave(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.Delete || key.Keycode == Key.Backspace)
        {
            _canvas.DeleteSelected();
            MarkDirty();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.W && !ctrl) { SetMode(EditorMode.Waypoint); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.S && !ctrl) { SetMode(EditorMode.Slot);     GetViewport().SetInputAsHandled(); }
        else if (key.Keycode == Key.G && !ctrl)
        {
            bool snap = !_canvas.SnapEnabled;
            _canvas.SetSnapEnabled(snap);
            GetViewport().SetInputAsHandled();
        }
    }

    // ── Canvas commit hook ──────────────────────────────────────────────────

    private void OnCanvasCommit(MapStateSnapshot before)
    {
        PushUndo(before);
        MarkDirty();
        UpdateStatusBar();
    }

    // ── Undo / Redo ─────────────────────────────────────────────────────────

    private void PushUndo(MapStateSnapshot before)
    {
        _undoStack.Push(before);
        if (_undoStack.Count > MaxUndoDepth)
        {
            // Trim oldest by rebuilding (Stack doesn't support RemoveAt).
            // ToArray() returns LIFO order: arr[0]=newest, arr[N]=oldest.
            // Push in reverse so newest ends up on top.
            var arr = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = MaxUndoDepth - 1; i >= 0; i--)
                _undoStack.Push(arr[i]);
        }
        _redoStack.Clear();
        UpdateUndoButtons();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var current = _canvas.CaptureSnapshot();
        _redoStack.Push(current);
        _canvas.RestoreSnapshot(_undoStack.Pop());
        MarkDirty();
        UpdateStatusBar();
        UpdateUndoButtons();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        var current = _canvas.CaptureSnapshot();
        _undoStack.Push(current);
        _canvas.RestoreSnapshot(_redoStack.Pop());
        MarkDirty();
        UpdateStatusBar();
        UpdateUndoButtons();
    }

    // ── Map CRUD ────────────────────────────────────────────────────────────

    private void NewMap()
    {
        _editingMapId = CustomMapManager.GenerateNewId();
        if (_nameField != null) _nameField.Text = "New Map";
        if (_descField != null) _descField.Text = "";
        _canvas?.RestoreSnapshot(new MapStateSnapshot(new List<Vector2>(), new List<Vector2>()));
        _undoStack.Clear();
        _redoStack.Clear();
        _hasUnsavedChanges = false;
        MapEditorState.LastEditedMapId = _editingMapId;
        UpdateUndoButtons();
        UpdateHeaderName();
        UpdateStatusBar();
    }

    private void LoadMap(string id)
    {
        try
        {
            var def = DataLoader.GetMapDef(id);
            _editingMapId = id;
            if (_nameField != null) _nameField.Text = def.Name ?? "";
            if (_descField != null) _descField.Text = def.Description ?? "";
            var wp = def.Path.Select(p => new Vector2(p.X, p.Y)).ToList();
            var sl = def.Slots.Select(s => new Vector2(s.X, s.Y)).ToList();
            _canvas.RestoreSnapshot(new MapStateSnapshot(wp, sl));
            _undoStack.Clear();
            _redoStack.Clear();
            _hasUnsavedChanges = false;
            MapEditorState.LastEditedMapId = id;
            UpdateUndoButtons();
            UpdateHeaderName();
            UpdateStatusBar();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[MapEditor] LoadMap({id}) failed: {ex.Message}");
        }
    }

    private void OnSave()
    {
        if (!TryBuildMapDef(out var def, out var errors))
        {
            ShowValidationPanel(errors);
            return;
        }
        SaveDef(def);
    }

    private void OnSaveAndClose()
    {
        if (!TryBuildMapDef(out var def, out var errors))
        {
            ShowValidationPanel(errors);
            return;
        }
        if (SaveDef(def)) NavigateBack();
    }

    private bool SaveDef(MapDef def)
    {
        if (!CustomMapManager.Save(def))
        {
            ShowMessage("Save failed -- check app user data permissions.");
            return false;
        }
        _editingMapId = def.Id;   // commit ID only on successful save
        DataLoader.RegisterCustomMap(def);
        _hasUnsavedChanges = false;
        MapEditorState.LastEditedMapId = def.Id;
        UpdateHeaderName();
        RefreshMapList();
        SoundManager.Instance?.Play("ui_select");
        return true;
    }

    private void OnDuplicateMap(string sourceId)
    {
        ConfirmDiscardIfNeeded(() =>
        {
            try
            {
                var src = DataLoader.GetMapDef(sourceId);
                string newId = CustomMapManager.GenerateNewId();
                var dup = src with { Id = newId, Name = src.Name + " (copy)", IsCustom = true };
                if (!CustomMapManager.Save(dup)) return;
                DataLoader.RegisterCustomMap(dup);
                LoadMap(newId);
                RefreshMapList();
                MarkDirty();
            }
            catch (System.Exception ex)
            {
                GD.PrintErr($"[MapEditor] Duplicate failed: {ex.Message}");
            }
        });
    }

    private void OnDeleteMap(string id)
    {
        ShowConfirm("Delete this map? This cannot be undone.", () =>
        {
            CustomMapManager.Delete(id);
            DataLoader.UnregisterCustomMap(id);
            if (_editingMapId == id) NewMap();
            RefreshMapList();
        });
    }

    // ── Playtest ─────────────────────────────────────────────────────────────

    private void OnPlaytest()
    {
        if (!TryBuildMapDef(out var def, out var errors))
        {
            ShowValidationPanel(errors);
            return;
        }
        // Save first so GameController can load it
        if (!SaveDef(def)) return;

        MapEditorState.IsPlaytesting = true;
        MapEditorState.PlaytestMapId = def.Id;
        MapEditorState.LastEditedMapId = def.Id;

        MapSelectPanel.SetPendingMapSelection(def.Id);
        // Always playtest on Easy so balance doesn't interfere with design testing
        SettingsManager.Instance?.SetDifficulty(DifficultyMode.Easy);

        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/Main.tscn");
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private bool TryBuildMapDef(out MapDef def, out List<string> errors)
    {
        def = default!;
        errors = new List<string>();

        string name = (_nameField?.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            errors.Add("Map name cannot be empty.");

        var waypoints = _canvas.Waypoints.ToList();
        var slots     = _canvas.Slots.ToList();

        if (waypoints.Count < 2)
            errors.Add($"Path needs at least 2 waypoints (currently {waypoints.Count}).");

        if (slots.Count != 6)
            errors.Add($"Exactly 6 tower slots required (currently {slots.Count}).");

        // Out-of-bounds check
        const float minX = 0f, maxX = 1280f, minY = 0f, maxY = 720f;
        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            if (wp.X < minX || wp.X > maxX || wp.Y < minY || wp.Y > maxY)
                errors.Add($"Waypoint {i + 1} is outside the game world (0–1280, 0–720).");
        }
        for (int i = 0; i < slots.Count; i++)
        {
            var sl = slots[i];
            if (sl.X < minX || sl.X > maxX || sl.Y < minY || sl.Y > maxY)
                errors.Add($"Slot {i + 1} is outside the game world (0–1280, 0–720).");
        }

        // Adjacent duplicate waypoints
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i].DistanceTo(waypoints[i + 1]) < 10f)
                errors.Add($"Waypoints {i + 1} and {i + 2} are at the same position.");
        }

        // Slots too close to each other (would make tower placement degenerate)
        for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
                if (slots[i].DistanceTo(slots[j]) < 50f)
                    errors.Add($"Slots {i + 1} and {j + 1} are too close together (< 50 px).");

        // Path routing: minimum total length
        if (waypoints.Count >= 2)
        {
            float totalLen = 0f;
            for (int i = 0; i < waypoints.Count - 1; i++)
                totalLen += waypoints[i].DistanceTo(waypoints[i + 1]);
            if (totalLen < 600f)
                errors.Add($"Path is too short ({totalLen:F0} px) -- enemies need at least 600 px to traverse.");
        }


        if (errors.Count > 0) return false;

        string desc  = (_descField?.Text ?? "").Trim();
        // Resolve ID without side effects: callers that need a persistent ID
        // (OnSave, OnPlaytest) will call EnsureMapId() before using the result.
        string mapId = _editingMapId.Length > 0 ? _editingMapId : CustomMapManager.GenerateNewId();

        def = new MapDef(
            Id:          mapId,
            Name:        name,
            Description: desc,
            Path:        waypoints.Select(v => new Vector2Def(v.X, v.Y)).ToArray(),
            Slots:       slots.Select((v, i) => new SlotDef(i, v.X, v.Y)).ToArray(),
            DisplayOrder: 500,
            IsCustom:    true
        );
        return true;
    }


    // ── UI helpers ────────────────────────────────────────────────────────────

    private void MarkDirty()
    {
        _hasUnsavedChanges = true;
        UpdateHeaderName();
        UpdateUndoButtons();
    }

    private void SetMode(EditorMode mode)
    {
        _canvas.SetMode(mode);
        UpdateToolButtons();
    }

    private void UpdateToolButtons()
    {
        if (_waypointModeBtn == null || _slotModeBtn == null) return;
        bool isWp = _canvas.Mode == EditorMode.Waypoint;
        HighlightModeButton(_waypointModeBtn, isWp);
        HighlightModeButton(_slotModeBtn,     !isWp);
    }

    private static void HighlightModeButton(Button btn, bool active)
    {
        btn.Modulate = active
            ? Colors.White
            : new Color(0.55f, 0.55f, 0.65f, 1f);
    }

    private void UpdateUndoButtons()
    {
        if (_undoBtn != null) _undoBtn.Disabled = _undoStack.Count == 0;
        if (_redoBtn != null) _redoBtn.Disabled = _redoStack.Count == 0;
    }

    private void UpdateHeaderName()
    {
        if (_mapNameHeaderLabel == null) return;
        string name = (_nameField?.Text ?? "untitled").Trim();
        if (string.IsNullOrEmpty(name)) name = "untitled";
        _mapNameHeaderLabel.Text = name;
        if (_unsavedDot != null)
            _unsavedDot.Modulate = _hasUnsavedChanges
                ? new Color(1.0f, 0.72f, 0.10f, 1f)
                : new Color(0.3f, 0.3f, 0.4f, 0f);
    }

    private void UpdateStatusBar()
    {
        if (_statusLabel == null) return;
        int wp = _canvas.Waypoints.Count;
        int sl = _canvas.Slots.Count;
        string wpStr = $"Waypoints: {wp}";
        string slStr = $"Slots: {sl}/6";

        var errors = new List<string>();
        TryBuildMapDef(out _, out errors);
        string status = errors.Count == 0
            ? $"{wpStr}  |  {slStr}  |  ✓ Valid"
            : $"{wpStr}  |  {slStr}  |  ✗ {errors[0]}";
        _statusLabel.Text = status;
        _statusLabel.Modulate = errors.Count == 0
            ? new Color(0.40f, 0.90f, 0.50f, 1f)
            : new Color(1.00f, 0.40f, 0.40f, 1f);
    }

    private void RefreshMapList()
    {
        // Rebuild the dropdown list if it's visible; otherwise it will rebuild on next open.
        if (_dropdownList != null && _mapDropdown.Visible)
            RebuildDropdownList(_dropdownSearch?.Text.Trim().ToLowerInvariant() ?? "");
    }

    private static Button MakeSmallButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    // ── Dialog helpers ────────────────────────────────────────────────────────

    private void ShowValidationPanel(List<string> errors)
    {
        SoundManager.Instance?.Play("ui_hover");
        ShowModal("Cannot save -- fix these issues:\n\n• " + string.Join("\n• ", errors),
                  "OK", null, null, null);
    }

    private void ShowMessage(string msg)
        => ShowModal(msg, "OK", null, null, null);

    private void ShowConfirm(string msg, System.Action onConfirm)
        => ShowModal(msg, "Confirm", "Cancel", onConfirm, null);

    private void ConfirmDiscardIfNeeded(System.Action onProceed)
    {
        if (!_hasUnsavedChanges) { onProceed(); return; }
        ShowConfirm("Discard unsaved changes?", onProceed);
    }

    private void ShowModal(string message, string okText, string? cancelText,
                           System.Action? onOk, System.Action? onCancel)
    {
        // Build a simple overlay modal
        var overlay = new ColorRect();
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.Color = new Color(0f, 0f, 0f, 0.70f);
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;

        var canvasLayer = new CanvasLayer { Layer = 20 };
        canvasLayer.AddChild(overlay);
        AddChild(canvasLayer);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        center.Theme = UITheme.Build();
        overlay.AddChild(center);

        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(400, 0);
        UITheme.ApplyGlassChassisPanel(panel,
            bg: new Color(0.04f, 0.06f, 0.16f, 0.98f),
            accent: new Color(0.36f, 0.74f, 0.90f, 0.88f),
            corners: 10, borderWidth: 2, padH: 20, padV: 16, sideEmitters: false);
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        var lbl = new Label
        {
            Text = message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(lbl, size: 17);
        vbox.AddChild(lbl);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        vbox.AddChild(btnRow);

        void Close() { canvasLayer.QueueFree(); }

        var okBtn = new Button { Text = okText, CustomMinimumSize = new Vector2(110, 38) };
        okBtn.AddThemeFontSizeOverride("font_size", 17);
        UITheme.ApplyPrimaryStyle(okBtn);
        okBtn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            Close();
            onOk?.Invoke();
        };
        btnRow.AddChild(okBtn);

        if (cancelText != null)
        {
            var cancelBtn = new Button { Text = cancelText, CustomMinimumSize = new Vector2(90, 38) };
            cancelBtn.AddThemeFontSizeOverride("font_size", 17);
            UITheme.ApplyCyanStyle(cancelBtn);
            cancelBtn.Pressed += () =>
            {
                SoundManager.Instance?.Play("ui_hover");
                Close();
                onCancel?.Invoke();
            };
            btnRow.AddChild(cancelBtn);
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavigateBack()
    {
        MapEditorState.IsPlaytesting = false;
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }

    // ── Map dropdown ─────────────────────────────────────────────────────────

    private void BuildMapDropdown()
    {
        _mapDropdown = new PopupPanel();
        _mapDropdown.MinSize = new Vector2I(320, 0);
        AddChild(_mapDropdown);

        var vbox = new VBoxContainer();
        vbox.CustomMinimumSize = new Vector2(320, 0);
        vbox.AddThemeConstantOverride("separation", 0);
        _mapDropdown.AddChild(vbox);

        // Search box
        var searchRow = new PanelContainer();
        UITheme.ApplyGlassChassisPanel(searchRow,
            bg: new Color(0.02f, 0.04f, 0.12f, 1f),
            accent: new Color(0.22f, 0.50f, 0.80f, 0.60f),
            corners: 0, borderWidth: 0, padH: 8, padV: 6, sideEmitters: false);
        vbox.AddChild(searchRow);

        _dropdownSearch = new LineEdit
        {
            PlaceholderText = "Search maps…",
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _dropdownSearch.AddThemeFontSizeOverride("font_size", 14);
        _dropdownSearch.TextChanged += (q) => RebuildDropdownList(q.Trim().ToLowerInvariant());
        searchRow.AddChild(_dropdownSearch);

        // Scrollable list
        var scroll = new ScrollContainer();
        scroll.CustomMinimumSize = new Vector2(0, 280);
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        vbox.AddChild(scroll);

        _dropdownList = new VBoxContainer();
        _dropdownList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _dropdownList.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_dropdownList);
    }

    private void OpenMapDropdown()
    {
        _dropdownSearch.Text = "";
        RebuildDropdownList("");
        // Position below the Open button
        var screenPos = _openMenuBtn.GetScreenPosition();
        _mapDropdown.Position = new Vector2I((int)screenPos.X, (int)(screenPos.Y + _openMenuBtn.Size.Y + 2));
        _mapDropdown.Popup();
        _dropdownSearch.GrabFocus();
    }

    private void RebuildDropdownList(string filter)
    {
        foreach (var child in _dropdownList.GetChildren())
            child.QueueFree();

        var maps = DataLoader.GetCustomMapDefs()
            .Where(m => string.IsNullOrEmpty(filter) ||
                        m.Name.ToLowerInvariant().Contains(filter))
            .ToList();

        if (maps.Count == 0)
        {
            var empty = new Label
            {
                Text = string.IsNullOrEmpty(filter) ? "No custom maps yet." : "No matches.",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            UITheme.ApplyFont(empty, size: 13);
            empty.Modulate = new Color(0.5f, 0.55f, 0.65f);
            empty.CustomMinimumSize = new Vector2(0, 40);
            _dropdownList.AddChild(empty);
            return;
        }

        foreach (var map in maps)
            _dropdownList.AddChild(BuildDropdownRow(map));
    }

    private Control BuildDropdownRow(MapDef map)
    {
        bool isActive = map.Id == _editingMapId;

        var row = new PanelContainer();
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(row,
            bg:     isActive ? new Color(0.04f, 0.10f, 0.22f, 0.98f) : new Color(0.015f, 0.03f, 0.09f, 0.98f),
            accent: isActive ? new Color(0.10f, 0.90f, 1.00f, 0.80f) : new Color(0.20f, 0.45f, 0.70f, 0.40f),
            corners: 0, borderWidth: isActive ? 2 : 1,
            padH: 8, padV: 5, sideEmitters: false);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        row.AddChild(hbox);

        var nameLbl = new Label { Text = map.Name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        UITheme.ApplyFont(nameLbl, semiBold: true, size: 14);
        nameLbl.Modulate = isActive ? new Color(0.10f, 0.90f, 1.00f) : Colors.White;
        nameLbl.ClipContents = true;
        hbox.AddChild(nameLbl);

        var openBtn = MakeSmallButton("Open");
        UITheme.ApplyCyanStyle(openBtn);
        openBtn.Disabled = isActive;
        openBtn.Pressed += () =>
        {
            _mapDropdown.Hide();
            ConfirmDiscardIfNeeded(() => LoadMap(map.Id));
        };
        openBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(openBtn);

        var dupBtn = MakeSmallButton("Dup");
        UITheme.ApplyMutedStyle(dupBtn);
        dupBtn.Pressed += () =>
        {
            OnDuplicateMap(map.Id);
            RebuildDropdownList(_dropdownSearch.Text.Trim().ToLowerInvariant());
        };
        dupBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(dupBtn);

        var delBtn = MakeSmallButton("Del");
        UITheme.ApplyMutedStyle(delBtn);
        delBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
        delBtn.Pressed += () =>
        {
            OnDeleteMap(map.Id);
            RebuildDropdownList(_dropdownSearch.Text.Trim().ToLowerInvariant());
        };
        delBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(delBtn);

        return row;
    }

    // ── Import / Export ───────────────────────────────────────────────────────

    private void OnExportPressed()
    {
        if (!TryBuildMapDef(out var def, out var errors))
        {
            ShowValidationPanel(errors);
            return;
        }
        string docsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        _exportDialog.CurrentDir  = docsPath;
        _exportDialog.CurrentFile = SanitizeFilename(def.Name) + ".json";
        _exportDialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnExportFileSelected(string path)
    {
        if (!TryBuildMapDef(out var def, out _)) return;
        if (CustomMapManager.ExportToFile(def, path))
            ShowModal($"Map exported to:\n{path}", "OK", null, null, null);
        else
            ShowModal("Export failed -- check the output log.", "OK", null, null, null);
    }

    private void OnImportPressed()
    {
        string docsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        _importDialog.CurrentDir = docsPath;
        _importDialog.PopupCentered(new Vector2I(700, 500));
    }

    private void OnImportFileSelected(string path)
    {
        var def = CustomMapManager.ImportFromFile(path);
        if (def == null)
        {
            ShowModal("Could not import -- file may be corrupted or not a valid Slot Theory map.", "OK", null, null, null);
            return;
        }
        // Assign a fresh local ID to avoid collisions
        var imported = def with { Id = CustomMapManager.GenerateNewId() };
        if (!CustomMapManager.Save(imported))
        {
            ShowModal("Import failed -- could not save to custom maps folder.", "OK", null, null, null);
            return;
        }
        DataLoader.RegisterCustomMap(imported);
        RefreshMapList();
        SoundManager.Instance?.Play("ui_select");
        ConfirmDiscardIfNeeded(() => LoadMap(imported.Id));
    }

    private static FileDialog MakeFileDialog(FileDialog.FileModeEnum mode, string title) =>
        new FileDialog
        {
            FileMode = mode,
            Access   = FileDialog.AccessEnum.Filesystem,
            Title    = title,
            Filters  = new[] { "*.json ; Slot Theory Map (*.json)" },
        };

    private static string SanitizeFilename(string name)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "map" : name;
    }

    // ── UI construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // File dialogs -- must be in the scene tree before being popped up
        _exportDialog = MakeFileDialog(FileDialog.FileModeEnum.SaveFile, "Export Map As…");
        _exportDialog.FileSelected += OnExportFileSelected;
        AddChild(_exportDialog);

        _importDialog = MakeFileDialog(FileDialog.FileModeEnum.OpenFile, "Import Map…");
        _importDialog.FileSelected += OnImportFileSelected;
        AddChild(_importDialog);

        var canvas = new CanvasLayer();
        AddChild(canvas);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#030a14");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        root.Theme = UITheme.Build();
        canvas.AddChild(root);

        // ── Header bar ──────────────────────────────────────────────────────
        var header = BuildHeader();
        root.AddChild(header);

        // Thin separator
        var sep = new ColorRect
        {
            Color = new Color(0.22f, 0.40f, 0.60f, 0.35f),
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(sep);

        // ── Body: 3-column layout ────────────────────────────────────────────
        var body = new HBoxContainer();
        body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        body.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 0);
        root.AddChild(body);

        // BuildCenterColumn must run first -- it creates _canvas, which BuildLeftPanel reads
        var centerCol = BuildCenterColumn();
        body.AddChild(BuildLeftPanel());
        body.AddChild(centerCol);

        // Map dropdown (free-floating popup -- added to scene root, not the body)
        BuildMapDropdown();
    }

    // ─────────────────────── HEADER ────────────────────────────────────────

    private Control BuildHeader()
    {
        var header = new PanelContainer();
        header.CustomMinimumSize = new Vector2(0, 50);
        UITheme.ApplyGlassChassisPanel(header,
            bg: new Color(0.02f, 0.04f, 0.12f, 0.98f),
            accent: new Color(0.36f, 0.74f, 0.90f, 0.85f),
            corners: 0, borderWidth: 0,
            padH: 14, padV: 0, sideEmitters: false);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        hbox.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        header.AddChild(hbox);

        // Title
        var titleLbl = new Label { Text = "MAP EDITOR" };
        UITheme.ApplyFont(titleLbl, semiBold: true, size: 24);
        titleLbl.Modulate = new Color(0.10f, 0.90f, 1.00f);
        hbox.AddChild(titleLbl);

        // Separator pip
        var pipSep = new Label { Text = "·" };
        pipSep.Modulate = new Color(0.35f, 0.45f, 0.60f);
        hbox.AddChild(pipSep);

        // Map name display + unsaved dot
        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 4);
        nameRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(nameRow);

        _mapNameHeaderLabel = new Label { Text = "New Map" };
        UITheme.ApplyFont(_mapNameHeaderLabel, semiBold: true, size: 18);
        _mapNameHeaderLabel.Modulate = Colors.White;
        nameRow.AddChild(_mapNameHeaderLabel);

        _unsavedDot = new Label { Text = "●" };
        UITheme.ApplyFont(_unsavedDot, size: 16);
        _unsavedDot.Modulate = new Color(1.0f, 0.72f, 0.10f, 0f);
        _unsavedDot.TooltipText = "Unsaved changes";
        nameRow.AddChild(_unsavedDot);

        // Right-aligned actions
        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hbox.AddChild(spacer);

        var newMapBtn = MakeHeaderButton("New", UITheme.Lime, 70);
        UITheme.ApplyPrimaryStyle(newMapBtn);
        newMapBtn.Pressed += () => ConfirmDiscardIfNeeded(NewMap);
        newMapBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(newMapBtn);

        _openMenuBtn = MakeHeaderButton("Open ▾", UITheme.Cyan, 90);
        UITheme.ApplyCyanStyle(_openMenuBtn);
        _openMenuBtn.Pressed += OpenMapDropdown;
        _openMenuBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(_openMenuBtn);

        var importHdrBtn = MakeHeaderButton("Import…", UITheme.Cyan, 90);
        UITheme.ApplyCyanStyle(importHdrBtn);
        importHdrBtn.Pressed += OnImportPressed;
        importHdrBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(importHdrBtn);

        _validateBtn = MakeHeaderButton("Validate", UITheme.Cyan, 100);
        _validateBtn.Pressed += OnValidatePressed;
        _validateBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(_validateBtn);

        _playtestBtn = MakeHeaderButton("Playtest", UITheme.Lime, 120);
        UITheme.ApplyPrimaryStyle(_playtestBtn);
        _playtestBtn.Pressed += OnPlaytest;
        _playtestBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(_playtestBtn);

        _saveBtn = MakeHeaderButton("Save", UITheme.Lime, 90);
        UITheme.ApplyPrimaryStyle(_saveBtn);
        _saveBtn.Pressed += OnSave;
        _saveBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(_saveBtn);

        var exportBtn = MakeHeaderButton("Export…", UITheme.Cyan, 100);
        UITheme.ApplyCyanStyle(exportBtn);
        exportBtn.Pressed += OnExportPressed;
        exportBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(exportBtn);

        var backBtn = MakeHeaderButton("Back", UITheme.Magenta, 80);
        UITheme.ApplyMutedStyle(backBtn);
        backBtn.Pressed += () => ConfirmDiscardIfNeeded(NavigateBack);
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        hbox.AddChild(backBtn);

        return header;
    }

    private static Button MakeHeaderButton(string text, Color accent, int minWidth)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, 36),
        };
        btn.AddThemeFontSizeOverride("font_size", 16);
        UITheme.ApplyMenuButtonFinish(btn, accent, 0.10f, 0.12f);
        return btn;
    }

    // ─────────────────────── LEFT PANEL ────────────────────────────────────

    private Control BuildLeftPanel()
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(175, 0);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(panel,
            bg: new Color(0.022f, 0.030f, 0.084f, 0.97f),
            accent: new Color(0.30f, 0.60f, 0.84f, 0.70f),
            corners: 0, borderWidth: 0,
            padH: 8, padV: 8, sideEmitters: false);

        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        // ShowNever (not Disabled) so the scroll container doesn't propagate
        // content minimum width up the layout tree -- lets CustomMinimumSize
        // on the panel actually control the width.
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever;
        panel.AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(vbox);

        // Local helper -- compact button for the narrow left panel
        Button MakeBtn(string text)
        {
            var b = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(0, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            b.AddThemeFontSizeOverride("font_size", 13);
            return b;
        }

        // ── Mode ────────────────────────────────────────────────────────────
        vbox.AddChild(MakeSectionLabel("TOOL"));

        var modeBox = new VBoxContainer();
        modeBox.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(modeBox);

        _waypointModeBtn = MakeBtn("Edit Path  [W]");
        UITheme.ApplyCyanStyle(_waypointModeBtn);
        _waypointModeBtn.Pressed += () => { SetMode(EditorMode.Waypoint); SoundManager.Instance?.Play("ui_hover"); };
        modeBox.AddChild(_waypointModeBtn);

        _slotModeBtn = MakeBtn("Place Slots  [S]");
        UITheme.ApplyCyanStyle(_slotModeBtn);
        _slotModeBtn.Pressed += () => { SetMode(EditorMode.Slot); SoundManager.Instance?.Play("ui_hover"); };
        modeBox.AddChild(_slotModeBtn);

        var snapToggleBtn = new CheckButton { Text = "Snap to 80px  [G]" };
        snapToggleBtn.AddThemeFontSizeOverride("font_size", 12);
        snapToggleBtn.ButtonPressed = _canvas.SnapEnabled;
        snapToggleBtn.Toggled += (on) => _canvas.SetSnapEnabled(on);
        vbox.AddChild(snapToggleBtn);

        var gridToggleBtn = new CheckButton { Text = "Show Grid" };
        gridToggleBtn.AddThemeFontSizeOverride("font_size", 12);
        gridToggleBtn.ButtonPressed = _canvas.GridVisible;
        gridToggleBtn.Toggled += (on) => _canvas.SetGridVisible(on);
        vbox.AddChild(gridToggleBtn);

        // ── Edit actions ────────────────────────────────────────────────────
        vbox.AddChild(MakeSectionLabel("EDIT"));

        var undoRow = new HBoxContainer();
        undoRow.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(undoRow);

        _undoBtn = MakeBtn("Undo [Z]");
        UITheme.ApplyMutedStyle(_undoBtn);
        _undoBtn.Disabled = true;
        _undoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _undoBtn.Pressed += () => { Undo(); SoundManager.Instance?.Play("ui_hover"); };
        undoRow.AddChild(_undoBtn);

        _redoBtn = MakeBtn("Redo [Y]");
        UITheme.ApplyMutedStyle(_redoBtn);
        _redoBtn.Disabled = true;
        _redoBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _redoBtn.Pressed += () => { Redo(); SoundManager.Instance?.Play("ui_hover"); };
        undoRow.AddChild(_redoBtn);

        var clearPathBtn = MakeBtn("Clear Path");
        UITheme.ApplyMutedStyle(clearPathBtn);
        clearPathBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.5f));
        clearPathBtn.Pressed += () => ShowConfirm("Clear all waypoints?", () =>
        {
            _canvas.ClearWaypoints();
            MarkDirty(); UpdateStatusBar();
        });
        vbox.AddChild(clearPathBtn);

        var clearSlotsBtn = MakeBtn("Clear Slots");
        UITheme.ApplyMutedStyle(clearSlotsBtn);
        clearSlotsBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.5f, 0.5f));
        clearSlotsBtn.Pressed += () => ShowConfirm("Clear all tower slots?", () =>
        {
            _canvas.ClearSlots();
            MarkDirty(); UpdateStatusBar();
        });
        vbox.AddChild(clearSlotsBtn);

        // ── Metadata ────────────────────────────────────────────────────────
        vbox.AddChild(MakeSectionLabel("MAP INFO"));

        var nameLbl = new Label { Text = "Name" };
        UITheme.ApplyFont(nameLbl, size: 12);
        nameLbl.Modulate = new Color(0.70f, 0.82f, 0.95f);
        vbox.AddChild(nameLbl);

        _nameField = new LineEdit
        {
            PlaceholderText = "Map name",
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _nameField.AddThemeFontSizeOverride("font_size", 13);
        _nameField.TextChanged += (_) => { MarkDirty(); UpdateHeaderName(); };
        vbox.AddChild(_nameField);

        var descLbl = new Label { Text = "Description" };
        UITheme.ApplyFont(descLbl, size: 12);
        descLbl.Modulate = new Color(0.70f, 0.82f, 0.95f);
        vbox.AddChild(descLbl);

        _descField = new TextEdit
        {
            PlaceholderText = "Optional description",
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        _descField.AddThemeFontSizeOverride("font_size", 12);
        _descField.TextChanged += () => MarkDirty();
        vbox.AddChild(_descField);

        // Controls reference
        vbox.AddChild(MakeSectionLabel("CONTROLS"));
        vbox.AddChild(MakeHelpLabel("Left-click: place / select\nRight-click: remove nearest\nDrag: move selected\nDel: delete selected\nCtrl+S: save"));

        return panel;
    }

    // ─────────────────────── CENTER COLUMN ─────────────────────────────────

    private Control BuildCenterColumn()
    {
        var col = new VBoxContainer();
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        col.AddThemeConstantOverride("separation", 0);

        // Canvas wrapper with thin inner border
        var canvasPanel = new PanelContainer();
        canvasPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        canvasPanel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        UITheme.ApplyGlassChassisPanel(canvasPanel,
            bg: new Color(0.01f, 0.015f, 0.05f, 1.0f),
            accent: new Color(0.22f, 0.40f, 0.62f, 0.55f),
            corners: 0, borderWidth: 1,
            padH: 0, padV: 0, sideEmitters: false);
        col.AddChild(canvasPanel);

        _canvas = new MapEditorCanvas();
        _canvas.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _canvas.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        _canvas.CommitChange += OnCanvasCommit;
        canvasPanel.AddChild(_canvas);

        // Status bar
        var statusBar = new PanelContainer();
        statusBar.CustomMinimumSize = new Vector2(0, 34);
        UITheme.ApplyGlassChassisPanel(statusBar,
            bg: new Color(0.02f, 0.04f, 0.10f, 0.97f),
            accent: new Color(0.22f, 0.40f, 0.60f, 0.40f),
            corners: 0, borderWidth: 0,
            padH: 12, padV: 0, sideEmitters: false);
        col.AddChild(statusBar);

        _statusLabel = new Label { Text = "Waypoints: 0  |  Slots: 0/6  |  Add waypoints to begin" };
        UITheme.ApplyFont(_statusLabel, size: 14);
        _statusLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _statusLabel.Modulate = new Color(0.65f, 0.75f, 0.90f);
        statusBar.AddChild(_statusLabel);

        return col;
    }

    // ─────────────────────── Minor helpers ─────────────────────────────────

    private static Button MakePanelButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        btn.AddThemeFontSizeOverride("font_size", 14);
        return btn;
    }

    private static Label MakeSectionLabel(string text)
    {
        var lbl = new Label { Text = text };
        UITheme.ApplyFont(lbl, semiBold: true, size: 13);
        lbl.Modulate = new Color(0.42f, 0.76f, 0.95f, 0.88f);
        return lbl;
    }

    private static Label MakeHelpLabel(string text)
    {
        var lbl = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        UITheme.ApplyFont(lbl, size: 12);
        lbl.Modulate = new Color(0.50f, 0.58f, 0.72f);
        return lbl;
    }

    private void OnValidatePressed()
    {
        SoundManager.Instance?.Play("ui_hover");
        TryBuildMapDef(out _, out var errors);
        if (errors.Count == 0)
            ShowMessage("Map is valid and ready to save.");
        else
            ShowValidationPanel(errors);
        UpdateStatusBar();
    }
}
