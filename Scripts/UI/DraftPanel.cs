using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen overlay shown during draft phase. Builds its own UI in code.
/// Two-step for modifiers: pick option → pick which tower to assign it to.
/// </summary>
public partial class DraftPanel : CanvasLayer
{
    private Label _titleLabel = null!;
    private HBoxContainer _cardRow = null!;
    private Label _assignLabel = null!;
    private HBoxContainer _towerRow = null!;
    private DraftOption? _pendingModifier;

    public override void _Ready()
    {
        Visible = false;

        // Semi-transparent background
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.75f);
        root.AddChild(bg);

        // Centered content
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
        _assignLabel.Text = "Assign modifier to:";
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

    public void Show(List<DraftOption> options, int waveNumber)
    {
        _titleLabel.Text = $"Wave {waveNumber} — Pick One";
        _pendingModifier = null;
        _assignLabel.Visible = false;
        _towerRow.Visible = false;
        BuildCardRow(options);
        Visible = true;
    }

    private void BuildCardRow(List<DraftOption> options)
    {
        foreach (Node child in _cardRow.GetChildren())
            child.QueueFree();

        foreach (var opt in options)
        {
            var btn = new Button();
            btn.Text = GetOptionLabel(opt);
            btn.CustomMinimumSize = new Vector2(190, 110);
            btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            var captured = opt;
            btn.Pressed += () => OnCardPressed(captured);
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
                btn.Text = $"Slot {i + 1}\n{def.Name}\n{slot.Tower.Modifiers.Count}/{Balance.MaxModifiersPerTower} mods";
                btn.Disabled = !slot.Tower.CanAddModifier;
            }
            else
            {
                btn.Text = $"Slot {i + 1}\n(empty)";
                btn.Disabled = true;
            }
            btn.CustomMinimumSize = new Vector2(150, 80);
            btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            var idx = i;
            btn.Pressed += () => OnTowerAssigned(idx);
            _towerRow.AddChild(btn);
        }
    }

    private void OnCardPressed(DraftOption opt)
    {
        if (opt.Type == DraftOptionType.Tower)
        {
            Visible = false;
            GameController.Instance.OnDraftPick(opt, -1);
        }
        else
        {
            _pendingModifier = opt;
            BuildTowerRow();
            _assignLabel.Visible = true;
            _towerRow.Visible = true;
        }
    }

    private void OnTowerAssigned(int slotIndex)
    {
        if (_pendingModifier == null) return;
        Visible = false;
        GameController.Instance.OnDraftPick(_pendingModifier, slotIndex);
    }

    private static string GetOptionLabel(DraftOption opt)
    {
        if (opt.Type == DraftOptionType.Tower)
        {
            var def = DataLoader.GetTowerDef(opt.Id);
            return $"[Tower]\n{def.Name}\nDMG {def.BaseDamage}  INT {def.AttackInterval}s\nRange {def.Range}";
        }
        else
        {
            var def = DataLoader.GetModifierDef(opt.Id);
            return $"[Mod]\n{def.Name}\n{def.Description}";
        }
    }
}
