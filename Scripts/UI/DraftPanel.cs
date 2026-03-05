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
    private Label _waveFooter = null!;
    private Label _wavePerformance = null!;
    private Label _assignLabel = null!;
    private HBoxContainer _towerRow = null!;
    private DraftOption? _pendingModifier;
    private DraftOption? _pendingTower;
    private ColorRect _bg = null!;
    private CenterContainer _center = null!;
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

        // Use explicit Position/Size instead of SetAnchorsPreset — anchor evaluation
        // is deferred and can drift after multiple show/hide cycles.  With canvas_items
        // stretch mode the viewport rect is always 1280×720.
        var vpSize = GetViewport().GetVisibleRect().Size;

        _bg = new ColorRect();
        _bg.Color = new Color(0f, 0f, 0f, 0.75f);
        _bg.Position = Vector2.Zero;
        _bg.Size = vpSize;
        AddChild(_bg);

        _center = new CenterContainer();
        _center.Theme = SlotTheory.Core.UITheme.Build();
        _center.Position = Vector2.Zero;
        _center.Size = vpSize;
        AddChild(_center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        _center.AddChild(vbox);

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

        _waveFooter = new Label();
        _waveFooter.HorizontalAlignment = HorizontalAlignment.Center;
        _waveFooter.AddThemeFontSizeOverride("font_size", 14);
        _waveFooter.AddThemeColorOverride("font_color", new Color(0.60f, 0.75f, 1.00f, 0.80f));
        _waveFooter.Visible = false;
        vbox.AddChild(_waveFooter);

        // Wave performance report from previous wave
        _wavePerformance = new Label();
        _wavePerformance.HorizontalAlignment = HorizontalAlignment.Center;
        _wavePerformance.AddThemeFontSizeOverride("font_size", 14);
        _wavePerformance.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.60f, 0.90f));
        _wavePerformance.Visible = false;
        vbox.AddChild(_wavePerformance);
        if (!MobileOptimization.IsTablet())
            MobileOptimization.ApplyUIScale(_center);
    }

    public void Show(List<DraftOption> options, int waveNumber, int pickNumber = 1, int totalPicks = 1, WaveReport? lastWaveReport = null)
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

        // Enhanced wave composition preview footer with Swift enemies and threat tags
        var cfg = waveNumber >= 1 && waveNumber <= Balance.TotalWaves
            ? DataLoader.GetWaveConfig(waveNumber - 1) : null;
        if (cfg != null)
        {
            int basic   = cfg.EnemyCount;
            int armored = cfg.TankyCount;
            int swift   = cfg.SwiftCount;
            
            List<string> parts = new();
            parts.Add($"{basic} Basic");
            if (armored > 0)
            {
                string armoredText = cfg.ClumpArmored ? $"{armored} Armored [clumped]" : $"{armored} Armored";
                parts.Add(armoredText);
            }
            if (swift > 0)
            {
                parts.Add($"{swift} Swift");
            }
            
            // Add threat tags based on enemy composition
            List<string> threats = new();
            if (armored >= 3) threats.Add("TANK");
            if (swift >= 2) threats.Add("FAST");
            if (cfg.ClumpArmored && armored >= 2) threats.Add("SURGE");
            
            string baseText = string.Join("  ·  ", parts);
            string threatHint = threats.Count > 0 ? $"  [{string.Join(", ", threats)}]" : "";
            
            // Optional archetype recommendation based on composition
            string archetypeHint = GenerateArchetypeHint(cfg);
            
            _waveFooter.Text = $"↓  {baseText}{threatHint}{archetypeHint}";
            _waveFooter.Visible = true;
        }
        else
        {
            _waveFooter.Visible = false;
        }
        
        // Show previous wave performance if available
        if (lastWaveReport != null && waveNumber > 1)
        {
            var topTower = lastWaveReport.TopDamageDealer;
            if (topTower != null)
            {
                var towerDef = DataLoader.GetTowerDef(topTower.TowerId);
                string performance = lastWaveReport.Leaks > 0 
                    ? $"Wave {lastWaveReport.WaveNumber}: {lastWaveReport.Leaks} leak(s)  •  Top damage: {towerDef.Name} ({topTower.Damage})"
                    : $"Wave {lastWaveReport.WaveNumber}: Perfect clear!  •  Top damage: {towerDef.Name} ({topTower.Damage})";
                _wavePerformance.Text = performance;
                _wavePerformance.Visible = true;
            }
            else
            {
                _wavePerformance.Visible = false;
            }
        }
        else
        {
            _wavePerformance.Visible = false;
        }
        
        // Explicitly reset position/size each open to prevent any deferred layout drift.
        var vpSize = GetViewport().GetVisibleRect().Size;
        _bg.Position = Vector2.Zero;
        _bg.Size = vpSize;
        _center.Position = Vector2.Zero;
        _center.Size = vpSize;
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
            Show(_lastOptions, _lastWaveNumber, _lastPickNumber, _lastTotalPicks, null);
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
        // Use Free() (immediate) not QueueFree() so old buttons are gone before new
        // ones are added — prevents the container briefly sizing to 2× the card count.
        foreach (Node child in _cardRow.GetChildren())
            child.Free();

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

            // Key hint as a separate child label — styled differently from the card body text,
            // anchored to the bottom-right corner so it never competes with card content
            var keyLbl = new Label { Text = $"[ {i + 1} ]" };
            keyLbl.AddThemeFontSizeOverride("font_size", 12);
            keyLbl.Modulate = new Color(0.55f, 0.55f, 0.75f, 0.80f);
            keyLbl.MouseFilter = Control.MouseFilterEnum.Ignore;
            keyLbl.HorizontalAlignment = HorizontalAlignment.Right;
            keyLbl.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            keyLbl.OffsetLeft = -46; keyLbl.OffsetRight = -4;
            keyLbl.OffsetTop  = -20; keyLbl.OffsetBottom = -4;
            btn.AddChild(keyLbl);

            _cardRow.AddChild(btn);
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
            SlotTheory.Core.SoundManager.Instance?.Play("ui_hover");
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
        if (opt.Type == DraftOptionType.Tower)
        {
            var def = DataLoader.GetTowerDef(opt.Id);
            return $"{def.Name}\n{def.BaseDamage} dmg  ·  {def.AttackInterval} s\nRange {def.Range}";
        }
        else
        {
            var def = DataLoader.GetModifierDef(opt.Id);
            return $"{def.Name}\n{def.Description}";
        }
    }

    /// <summary>
    /// Generates a subtle archetype hint based on wave composition.
    /// Helps players understand what build strategies might work well.
    /// </summary>
    private string GenerateArchetypeHint(WaveConfig cfg)
    {
        // Only show hints for challenging waves to avoid clutter
        if (cfg.TankyCount + cfg.SwiftCount < 2) return "";
        
        List<string> hints = new();
        
        // High armored count suggests marker strategy
        if (cfg.TankyCount >= 3)
        {
            hints.Add("Marker recommended");
        }
        // Mix of armored and swift suggests AoE/chain
        else if (cfg.TankyCount >= 1 && cfg.SwiftCount >= 2)
        {
            hints.Add("AoE favored");
        }
        // Lots of swift enemies suggests range/coverage
        else if (cfg.SwiftCount >= 3)
        {
            hints.Add("Range coverage");
        }
        // Clumped armored suggests burst damage
        else if (cfg.ClumpArmored && cfg.TankyCount >= 2)
        {
            hints.Add("Burst damage");
        }
        
        return hints.Count > 0 ? $"  • {string.Join(", ", hints)}" : "";
    }
}
