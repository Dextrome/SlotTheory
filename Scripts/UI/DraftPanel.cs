using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen overlay shown during draft phase. Builds its own UI in code.
/// Two-step for modifiers: pick option → pick which tower to assign it to.
/// Keys 1–5 select cards; 1–6 select slots in the assignment step.
/// </summary>
public partial class DraftPanel : CanvasLayer
{
    private Label _titleLabel = null!;
    private HBoxContainer _cardRow = null!;
    private Label _assignLabel = null!;
    private HBoxContainer _towerRow = null!;
    private DraftOption? _pendingModifier;
    private DraftOption? _pendingTower;
    private ColorRect _bg = null!;
    private List<DraftOption> _lastOptions  = new();
    private int  _lastWaveNumber = 1;
    private int  _lastPickNumber = 1;
    private int  _lastTotalPicks = 1;

    public bool IsAwaitingSlot  => _pendingTower    != null;
    public bool IsAwaitingTower => _pendingModifier != null;

    public string PlacementHint
    {
        get
        {
            if (_pendingTower != null)
            {
                var def = DataLoader.GetTowerDef(_pendingTower.Id);
                return $"Click a slot to place  {def.Name}";
            }
            if (_pendingModifier != null)
            {
                var def = DataLoader.GetModifierDef(_pendingModifier.Id);
                return $"Click a tower to assign  {def.Name}";
            }
            return "";
        }
    }

    public bool IsSlotValidTarget(int i)
    {
        var slots = GameController.Instance.GetRunState().Slots;
        if (IsAwaitingSlot)  return slots[i].Tower == null;
        if (IsAwaitingTower) return slots[i].Tower?.CanAddModifier == true;
        return false;
    }

    public void SelectSlot(int slotIndex)
    {
        if (_pendingTower    != null) OnSlotPicked(slotIndex);
        else if (_pendingModifier != null) OnTowerAssigned(slotIndex);
    }

    public override void _Ready()
    {
        Visible = false;

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.Theme = SlotTheory.Core.UITheme.Build();
        AddChild(root);

        _bg = new ColorRect();
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bg.Color = new Color(0f, 0f, 0f, 0.75f);
        root.AddChild(_bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        center.AddChild(vbox);

        _titleLabel = new Label();
        _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _titleLabel.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(_titleLabel);

        _cardRow = new HBoxContainer();
        _cardRow.Alignment = BoxContainer.AlignmentMode.Center;
        _cardRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(_cardRow);

        _assignLabel = new Label();
        _assignLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _assignLabel.AddThemeFontSizeOverride("font_size", 18);
        _assignLabel.Visible = false;
        vbox.AddChild(_assignLabel);

        _towerRow = new HBoxContainer();
        _towerRow.Alignment = BoxContainer.AlignmentMode.Center;
        _towerRow.AddThemeConstantOverride("separation", 12);
        _towerRow.Visible = false;
        vbox.AddChild(_towerRow);
    }

    public void Show(List<DraftOption> options, int waveNumber, int pickNumber = 1, int totalPicks = 1)
    {
        _lastOptions    = options;
        _lastWaveNumber = waveNumber;
        _lastPickNumber = pickNumber;
        _lastTotalPicks = totalPicks;

        _titleLabel.Text = totalPicks > 1
            ? $"Wave {waveNumber}  —  Pick {pickNumber} of {totalPicks}"
            : $"Wave {waveNumber}  —  Choose";
        _pendingModifier = null;
        _pendingTower = null;
        _assignLabel.Visible = false;
        _towerRow.Visible = false;
        _cardRow.Visible = true;
        _bg.MouseFilter = Control.MouseFilterEnum.Stop;
        BuildCardRow(options);
        Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey { Pressed: true } key) return;

        if (key.Keycode == Key.Escape && (_pendingTower != null || _pendingModifier != null))
        {
            _pendingTower    = null;
            _pendingModifier = null;
            Show(_lastOptions, _lastWaveNumber, _lastPickNumber, _lastTotalPicks);
            GetViewport().SetInputAsHandled();
            return;
        }

        int idx = key.Keycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            Key.Key4 => 3,
            Key.Key5 => 4,
            Key.Key6 => 5,
            _        => -1,
        };
        if (idx < 0) return;

        var row = _cardRow.Visible ? _cardRow : (_towerRow.Visible ? _towerRow : null);
        if (row == null) return;

        var children = row.GetChildren();
        if (idx < children.Count)
        {
            if (children[idx] is Button btn && !btn.Disabled)
            {
                btn.EmitSignal(Button.SignalName.Pressed);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildCardRow(List<DraftOption> options)
    {
        foreach (Node child in _cardRow.GetChildren())
            child.QueueFree();

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var btn = new Button();
            btn.Text = GetOptionLabel(opt, i);
            btn.CustomMinimumSize = new Vector2(190, 110);
            btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            btn.PivotOffset = new Vector2(95f, 55f);
            var captured = opt;
            btn.Pressed += () => OnCardPressed(captured);
            AddHover(btn);
            _cardRow.AddChild(btn);
        }
    }

    private void BuildTowerRow()
    {
        foreach (Node child in _towerRow.GetChildren())
            child.QueueFree();

        var slots = GameController.Instance.GetRunState().Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            var btn = new Button();
            if (slot.Tower != null)
            {
                var def = DataLoader.GetTowerDef(slot.Tower.TowerId);
                int mods = slot.Tower.Modifiers.Count;
                btn.Text = $"[{i + 1}]  Slot {i + 1}  ·  {def.Name}\n{mods}/{Balance.MaxModifiersPerTower} mods";
                btn.Disabled = !slot.Tower.CanAddModifier;
            }
            else
            {
                btn.Text = $"[{i + 1}]  empty";
                btn.Disabled = true;
            }
            btn.CustomMinimumSize = new Vector2(150, 70);
            btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            btn.PivotOffset = new Vector2(75f, 35f);
            var idx = i;
            btn.Pressed += () => OnTowerAssigned(idx);
            if (!btn.Disabled) AddHover(btn);
            _towerRow.AddChild(btn);
        }
    }

    private void BuildEmptySlotRow()
    {
        foreach (Node child in _towerRow.GetChildren())
            child.QueueFree();

        var slots = GameController.Instance.GetRunState().Slots;
        for (int i = 0; i < slots.Length; i++)
        {
            bool isEmpty = slots[i].Tower == null;
            var btn = new Button();
            btn.Text = isEmpty ? $"[{i + 1}]  Slot {i + 1}" : "occupied";
            btn.Disabled = !isEmpty;
            btn.CustomMinimumSize = new Vector2(150, 70);
            btn.PivotOffset = new Vector2(75f, 35f);
            var idx = i;
            btn.Pressed += () => OnSlotPicked(idx);
            if (!btn.Disabled) AddHover(btn);
            _towerRow.AddChild(btn);
        }
    }

    private void OnCardPressed(DraftOption opt)
    {
        SlotTheory.Core.SoundManager.Instance?.Play("draft_pick");
        if (opt.Type == DraftOptionType.Tower)
            _pendingTower = opt;
        else
            _pendingModifier = opt;
        // Hide panel — world slot highlights take over for placement/assignment
        Visible = false;
    }

    private void OnTowerAssigned(int slotIndex)
    {
        if (_pendingModifier == null) return;
        Visible = false;
        SlotTheory.Core.SoundManager.Instance?.Play("tower_place");
        GameController.Instance.OnDraftPick(_pendingModifier, slotIndex);
    }

    private void OnSlotPicked(int slotIndex)
    {
        if (_pendingTower == null) return;
        Visible = false;
        SlotTheory.Core.SoundManager.Instance?.Play("tower_place");
        GameController.Instance.OnDraftPick(_pendingTower, slotIndex);
    }

    private static void AddHover(Button btn)
    {
        btn.MouseEntered += () =>
        {
            var tw = btn.CreateTween();
            tw.TweenProperty(btn, "scale", new Vector2(1.06f, 1.06f), 0.08f);
        };
        btn.MouseExited += () =>
        {
            var tw = btn.CreateTween();
            tw.TweenProperty(btn, "scale", Vector2.One, 0.08f);
        };
    }

    private static string GetOptionLabel(DraftOption opt, int index)
    {
        string key = $"[ {index + 1} ]";
        if (opt.Type == DraftOptionType.Tower)
        {
            var def = DataLoader.GetTowerDef(opt.Id);
            return $"{def.Name}\n{def.BaseDamage} dmg  ·  {def.AttackInterval} s\nRange {def.Range}\n{key}";
        }
        else
        {
            var def = DataLoader.GetModifierDef(opt.Id);
            return $"{def.Name}\n{def.Description}\n{key}";
        }
    }
}
