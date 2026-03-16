using System.Collections.Generic;
using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen overlay shown during draft phase.
/// Two-step for modifiers: pick option then pick which tower to assign it to.
/// Keys 1-5 select cards; 1-6 select slots in the assignment step.
/// </summary>
public partial class DraftPanel : CanvasLayer
{
    private Label _titleLabel = null!;
    private Label _runMemoryLabel = null!;
    private Label _bonusStamp = null!;
    private HBoxContainer _cardRow = null!;
    private Label _waveFooter = null!;
    private Label _wavePerformance = null!;
    private Label _assignLabel = null!;
    private HBoxContainer _towerRow = null!;
    private DraftOption? _pendingModifier;
    private DraftOption? _pendingTower;
    private int _previewModifierSlot = -1;
    private int _previewTowerSlot = -1;
    private ulong _previewSetAtMs = 0;
    private ulong _previewTowerSetAtMs = 0;
    private ColorRect _bg = null!;
    private DraftBackdropFx _bgFx = null!;
    private CenterContainer _center = null!;
    private VBoxContainer _placementGroup = null!;
    private Button _cancelBtn = null!;
    private Label _placementHintLbl = null!;
    private System.Action? _cancelCallback;
    private List<DraftOption> _lastOptions = new();
    private int _lastWaveNumber = 1;
    private int _lastPickNumber = 1;
    private int _lastTotalPicks = 1;
    private int _foilCardIndex = -1;
    private string _hoveredModifierHintId = "";
    private string _touchHoldModifierId = "";
    private ulong _touchHoldStartMs = 0;
    private bool _touchHintShown = false;
    private Button? _touchPreviewCard;
    private ulong _touchPreviewStartMs = 0;
    private bool _touchPreviewActive = false;
    private bool _suppressNextCardPress = false;
    private bool _isCardCommitInFlight = false;
    private readonly RandomNumberGenerator _rng = new();
    private PanelContainer _firstRunBanner = null!;
    private Label _bannerHeader = null!;
    private Label _bannerBody   = null!;
    private Button _bannerNext  = null!;
    private Button _bannerHowTo = null!;
    private int _bannerPage = 0;
    private const float CardFaceDownHoldSeconds = 0.12f;
    private const float CardStaggerSeconds = 0.40f;
    private const float CardEntranceSeconds = 0.34f;
    private const float TouchHoldHintMs = 170f;
    private const ulong TouchCardPreviewMs = 250;

    public bool IsAwaitingSlot => _pendingTower != null;
    public bool IsAwaitingTower => _pendingModifier != null;
    public bool HasModifierPreview => _previewModifierSlot >= 0;
    public bool HasTowerPreview => _previewTowerSlot >= 0;
    public int ModifierPreviewSlot => _previewModifierSlot;
    public int TowerPreviewSlot => _previewTowerSlot;
    public string PendingModifierId => _pendingModifier?.Id ?? "";
    public string PendingTowerId => _pendingTower?.Id ?? "";
    public List<DraftOption> GetLastOptionsSnapshot() => new(_lastOptions);
    public (int waveNumber, int pickNumber, int totalPicks) GetLastDraftMeta()
        => (_lastWaveNumber, _lastPickNumber, _lastTotalPicks);

    public void CancelModifierPreview()
    {
        _previewModifierSlot = -1;
        _previewSetAtMs = 0;
    }

    public void CancelTowerPreview()
    {
        _previewTowerSlot = -1;
        _previewTowerSetAtMs = 0;
    }

    public string PlacementHint
    {
        get
        {
            if (_pendingTower != null)
            {
                var def = DataLoader.GetTowerDef(_pendingTower.Id);
                if (_previewTowerSlot >= 0)
                    return $"Preview: {def.Name} on slot {_previewTowerSlot + 1}  -  tap again to confirm";
                return $"Tap a slot to preview  {def.Name}\nTap again to confirm placement";
            }

            if (_pendingModifier != null)
            {
                var def = DataLoader.GetModifierDef(_pendingModifier.Id);
                if (_previewModifierSlot >= 0)
                    return $"Preview: {def.Name} on slot {_previewModifierSlot + 1}  -  tap same slot to confirm, tap elsewhere to cancel\n{def.Description}";
                return $"Tap a tower to apply {def.Name}\n{def.Description}";
            }

            return "";
        }
    }

    public bool IsSlotValidTarget(int i)
    {
        var slots = GameController.Instance.GetRunState().Slots;
        if (IsAwaitingSlot) return slots[i].Tower == null;
        if (IsAwaitingTower) return slots[i].Tower?.CanAddModifier == true;
        return false;
    }

    public void SelectSlot(int slotIndex)
    {
        if (_pendingTower != null) OnSlotPicked(slotIndex);
        else if (_pendingModifier != null) OnModifierSlotPicked(slotIndex);
    }

    public override void _Ready()
    {
        Visible = false;
        _rng.Randomize();

        var vpSize = GetViewport().GetVisibleRect().Size;

        _bg = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.70f),
            Position = Vector2.Zero,
            Size = vpSize,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        AddChild(_bg);

        _bgFx = new DraftBackdropFx
        {
            Position = Vector2.Zero,
            Size = vpSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_bgFx);

        _center = new CenterContainer
        {
            Theme = UITheme.Build(),
            Position = Vector2.Zero,
            Size = vpSize,
        };
        AddChild(_center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        _center.AddChild(vbox);

        _titleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        UITheme.ApplyFont(_titleLabel, semiBold: true, size: 28);
        vbox.AddChild(_titleLabel);

        _runMemoryLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            Modulate = new Color(0.70f, 0.82f, 1.00f, 0.78f),
        };
        UITheme.ApplyFont(_runMemoryLabel, semiBold: true, size: 14);
        vbox.AddChild(_runMemoryLabel);

        _bonusStamp = new Label
        {
            Text = "BONUS PICK",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
            Modulate = new Color(1.0f, 0.72f, 0.30f, 0f),
        };
        UITheme.ApplyFont(_bonusStamp, semiBold: true, size: 14);
        _bonusStamp.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.30f));
        vbox.AddChild(_bonusStamp);

        _cardRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _cardRow.AddThemeConstantOverride("separation", 14);
        vbox.AddChild(_cardRow);

        _assignLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        UITheme.ApplyFont(_assignLabel, semiBold: true, size: 18);
        vbox.AddChild(_assignLabel);

        _towerRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            Visible = false,
        };
        _towerRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(_towerRow);

        _waveFooter = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        UITheme.ApplyFont(_waveFooter, size: 14);
        _waveFooter.AddThemeColorOverride("font_color", new Color(0.60f, 0.75f, 1.00f, 0.82f));
        vbox.AddChild(_waveFooter);

        _wavePerformance = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        UITheme.ApplyFont(_wavePerformance, size: 14);
        _wavePerformance.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.60f, 0.90f));
        vbox.AddChild(_wavePerformance);

        if (!MobileOptimization.IsTablet())
            MobileOptimization.ApplyUIScale(_center);
        AddChild(new PinchZoomHandler(_center));

        // ── Placement UI: cancel button + hint label, scales with zoom in _Process ──
        _placementGroup = new VBoxContainer();
        _placementGroup.AnchorLeft    = 0.5f;
        _placementGroup.AnchorRight   = 0.5f;
        _placementGroup.AnchorTop     = 0f;
        _placementGroup.AnchorBottom  = 0f;
        _placementGroup.OffsetLeft    = -120f;
        _placementGroup.OffsetRight   = 120f;
        _placementGroup.OffsetTop     = 48f;
        _placementGroup.OffsetBottom  = 130f;
        _placementGroup.GrowHorizontal = Control.GrowDirection.Both;
        _placementGroup.AddThemeConstantOverride("separation", 14);
        _placementGroup.Visible = false;
        AddChild(_placementGroup);

        _cancelBtn = new Button
        {
            Text = "Cancel",
            CustomMinimumSize = new Vector2(0, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _cancelBtn.AddThemeFontSizeOverride("font_size", 16);
        var cancelStyle = new StyleBoxFlat
        {
            BgColor     = new Color(0.08f, 0.06f, 0.14f, 0.88f),
            BorderColor = new Color(0.75f, 0.15f, 0.75f, 0.90f),
            CornerRadiusTopLeft     = 6,
            CornerRadiusTopRight    = 6,
            CornerRadiusBottomLeft  = 6,
            CornerRadiusBottomRight = 6,
        };
        cancelStyle.SetBorderWidthAll(2);
        cancelStyle.ContentMarginLeft   = 12f;
        cancelStyle.ContentMarginRight  = 12f;
        cancelStyle.ContentMarginTop    = 4f;
        cancelStyle.ContentMarginBottom = 4f;
        _cancelBtn.AddThemeStyleboxOverride("normal", cancelStyle);
        var cancelHover = (StyleBoxFlat)cancelStyle.Duplicate();
        cancelHover.BgColor     = new Color(0.18f, 0.06f, 0.22f, 0.95f);
        cancelHover.BorderColor = new Color(0.95f, 0.30f, 0.95f, 1.00f);
        _cancelBtn.AddThemeStyleboxOverride("hover", cancelHover);
        var cancelPress = (StyleBoxFlat)cancelStyle.Duplicate();
        cancelPress.BgColor = new Color(0.12f, 0.04f, 0.18f, 1.00f);
        _cancelBtn.AddThemeStyleboxOverride("pressed", cancelPress);
        _cancelBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        _cancelBtn.Pressed      += () => SoundManager.Instance?.Play("ui_select");
        _placementGroup.AddChild(_cancelBtn);

        _placementHintLbl = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var hintLs = new LabelSettings
        {
            Font         = UITheme.SemiBold,
            FontSize     = 17,
            FontColor    = new Color(0.74f, 0.88f, 1.00f),
            ShadowColor  = new Color(0f, 0f, 0f, 0.70f),
            ShadowSize   = 3,
            ShadowOffset = new Vector2(0f, 1f),
        };
        _placementHintLbl.LabelSettings = hintLs;
        _placementGroup.AddChild(_placementHintLbl);

        // ── First-run guidance banner ────────────────────────────────────────
        // Two-page overlay shown only on wave 1 pick 1 of the player's first run.
        _firstRunBanner = new PanelContainer();
        _firstRunBanner.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.04f, 0.04f, 0.14f, 0.95f),
            border: new Color(0.30f, 0.35f, 0.55f),
            corners: 10, borderWidth: 1, padH: 16, padV: 12));
        _firstRunBanner.AnchorLeft    = 0.5f;
        _firstRunBanner.AnchorRight   = 0.5f;
        _firstRunBanner.AnchorTop     = 0f;
        _firstRunBanner.AnchorBottom  = 0f;
        _firstRunBanner.GrowHorizontal = Control.GrowDirection.Both;
        _firstRunBanner.OffsetLeft    = -280f;
        _firstRunBanner.OffsetRight   =  280f;
        _firstRunBanner.OffsetTop     =  10f;
        _firstRunBanner.Visible = false;

        var bannerVbox = new VBoxContainer();
        bannerVbox.AddThemeConstantOverride("separation", 8);
        _firstRunBanner.AddChild(bannerVbox);

        var bannerTopRow = new HBoxContainer();
        _bannerHeader = new Label { Text = "HOW THIS WORKS  (1/2)" };
        UITheme.ApplyFont(_bannerHeader, semiBold: true, size: 13);
        _bannerHeader.AddThemeColorOverride("font_color", UITheme.Lime);
        bannerTopRow.AddChild(_bannerHeader);
        bannerVbox.AddChild(bannerTopRow);

        _bannerBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(520f, 0f),
        };
        UITheme.ApplyFont(_bannerBody, size: 14);
        _bannerBody.AddThemeColorOverride("font_color", new Color(0.88f, 0.90f, 1.00f));
        bannerVbox.AddChild(_bannerBody);

        var bannerBtnRow = new HBoxContainer();
        bannerBtnRow.AddThemeConstantOverride("separation", 10);

        _bannerHowTo = new Button { Text = "How to Play \u2192" };
        _bannerHowTo.AddThemeFontSizeOverride("font_size", 13);
        UITheme.ApplyMutedStyle(_bannerHowTo);
        _bannerHowTo.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _bannerHowTo.Pressed += OnBannerHowToPressed;
        _bannerHowTo.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        bannerBtnRow.AddChild(_bannerHowTo);

        _bannerNext = new Button { Text = "Next \u2192" };
        _bannerNext.AddThemeFontSizeOverride("font_size", 13);
        UITheme.ApplyPrimaryStyle(_bannerNext);
        _bannerNext.CustomMinimumSize = new Vector2(90f, 0f);
        _bannerNext.Pressed += OnBannerNextPressed;
        _bannerNext.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        bannerBtnRow.AddChild(_bannerNext);

        bannerVbox.AddChild(bannerBtnRow);
        AddChild(_firstRunBanner);
    }

    private void SetBannerPage(int page)
    {
        _bannerPage = page;
        if (page == 0)
        {
            _bannerHeader.Text = "HOW THIS WORKS  (1/2)";
            _bannerBody.Text =
                "Pick one card — towers fill empty slots, modifiers upgrade towers you already have.\n" +
                "Waves run automatically. You draft once between every wave. Survive 20 waves to win.";
            _bannerNext.Text = "Next \u2192";
            _bannerHowTo.Visible = false;
        }
        else
        {
            _bannerHeader.Text = "SURGES  (2/2)";
            _bannerBody.Text =
                "Modifiers generate charge as they activate (hits, kills, procs). When a tower's meter fills, it triggers a Surge: a powerful mid-wave effect.\n" +
                "Each Surge adds to a global meter. Fill it enough and a Global Surge fires, refunding all cooldowns and hitting every enemy on the lane.";
            _bannerNext.Text = "Got it";
            _bannerHowTo.Visible = true;
        }
    }

    private void OnBannerNextPressed()
    {
        SoundManager.Instance?.Play("ui_select");
        if (_bannerPage == 0)
            SetBannerPage(1);
        else
            _firstRunBanner.Visible = false;
    }

    private void OnBannerHowToPressed()
    {
        SoundManager.Instance?.Play("ui_select");
        _firstRunBanner.Visible = false;
        var howTo = new HowToPlay();
        howTo.StartOnSurgesTab = true;
        howTo.OnBack = () => { /* draft is still open underneath */ };
        GetTree().Root.AddChild(howTo);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        // Keep placement group scaled with zoom (grows downward from top of screen)
        float s = _center.Scale.X;
        _placementGroup.Scale = new Vector2(s, s);
        _placementGroup.PivotOffset = new Vector2(_placementGroup.Size.X * 0.5f, 0f);

        ulong nowMs = Time.GetTicksMsec();

        if (_touchPreviewCard != null && !_touchPreviewActive && nowMs - _touchPreviewStartMs >= TouchCardPreviewMs)
        {
            _touchPreviewActive = true;
            _touchPreviewCard.Scale = Vector2.One;
            _touchPreviewCard.ZIndex = 20;
            var tw = _touchPreviewCard.CreateTween();
            tw.TweenProperty(_touchPreviewCard, "scale", new Vector2(1.12f, 1.12f), 0.12f)
              .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }

        if (_touchHoldModifierId.Length == 0 || _touchHintShown) return;
        if (nowMs - _touchHoldStartMs < (ulong)TouchHoldHintMs) return;

        _touchHintShown = true;
        SetModifierSynergyHint(_touchHoldModifierId);
    }

    public void ShowPlacementUI(System.Action onCancel)
    {
        if (_cancelCallback != null)
            _cancelBtn.Pressed -= _cancelCallback;
        _cancelCallback = onCancel;
        _cancelBtn.Pressed += _cancelCallback;
        // Hide card overlay content but keep CanvasLayer visible so _placementGroup shows.
        _bg.Visible = false;
        _bgFx.Visible = false;
        _center.Visible = false;
        _placementGroup.Visible = true;
        Visible = true;
    }

    public void HidePlacementUI()
    {
        if (_cancelCallback != null)
        {
            _cancelBtn.Pressed -= _cancelCallback;
            _cancelCallback = null;
        }
        _placementGroup.Visible = false;
        // Restore card overlay nodes for next Show() call. Do NOT set Visible = false here —
        // OnSlotPicked / OnModifierSlotPicked already do that, and Show() (for a next pick) may
        // have already set Visible = true; hiding it here would break the second-pick display.
        _bg.Visible = true;
        _bgFx.Visible = true;
        _center.Visible = true;
    }

    public void SetPlacementHintText(string text)
    {
        _placementHintLbl.Text = text;
    }

    public void Show(List<DraftOption> options, int waveNumber, int pickNumber = 1, int totalPicks = 1, WaveReport? lastWaveReport = null)
    {
        _lastOptions = options;
        _lastWaveNumber = waveNumber;
        _lastPickNumber = pickNumber;
        _lastTotalPicks = totalPicks;

        _titleLabel.Text = totalPicks > 1
            ? $"Wave {waveNumber}  -  Pick {pickNumber} of {totalPicks}"
            : $"Wave {waveNumber}  -  Choose";

        var run = GameController.Instance?.GetRunState();
        string buildName = GameController.Instance?.GetCurrentRunName() ?? "Neon Arsenal";
        int lives = run?.Lives ?? Balance.StartingLives;
        _runMemoryLabel.Text = $"Build: {buildName}  |  Lives: {lives}  |  Speed: {Engine.TimeScale:0.0}x";
        _runMemoryLabel.Visible = true;

        _pendingModifier = null;
        _pendingTower = null;
        _previewModifierSlot = -1;
        _previewTowerSlot = -1;
        _previewSetAtMs = 0;
        _previewTowerSetAtMs = 0;
        _foilCardIndex = (_rng.RandiRange(1, 12) == 1 && options.Count > 0) ? _rng.RandiRange(0, options.Count - 1) : -1;
        ClearModifierSynergyHint();
        _touchHoldModifierId = "";
        _touchHoldStartMs = 0;
        _touchHintShown = false;
        _touchPreviewCard = null;
        _touchPreviewStartMs = 0;
        _touchPreviewActive = false;
        _suppressNextCardPress = false;
        _isCardCommitInFlight = false;
        _assignLabel.Visible = false;
        _towerRow.Visible = false;
        _cardRow.Visible = true;
        _bonusStamp.Visible = false;
        if (totalPicks > 1 && pickNumber == totalPicks)
            AnimateBonusPickStamp();

        string? mapId = GameController.Instance?.GetRunState().SelectedMapId;
        var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Easy;
        var cfg = waveNumber >= 1 && waveNumber <= Balance.TotalWaves
            ? DataLoader.GetWaveConfig(waveNumber - 1, difficulty, mapId)
            : null;

        if (cfg != null)
        {
            int basic = cfg.EnemyCount;
            int armored = cfg.TankyCount;
            int swift = cfg.SwiftCount;

            List<string> parts = new() { $"{basic} Basic" };
            if (armored > 0)
            {
                string armoredText = cfg.ClumpArmored ? $"{armored} Armored [clumped]" : $"{armored} Armored";
                parts.Add(armoredText);
            }

            if (swift > 0)
                parts.Add($"{swift} Swift");

            List<string> threats = new();
            if (armored >= 3) threats.Add("TANK");
            if (swift >= 2) threats.Add("FAST");
            if (cfg.ClumpArmored && armored >= 2) threats.Add("SURGE");

            string baseText = string.Join("  |  ", parts);
            string threatHint = threats.Count > 0 ? $"  [{string.Join(", ", threats)}]" : "";
            string archetypeHint = GenerateArchetypeHint(cfg);

            _waveFooter.Text = $"v  {baseText}{threatHint}{archetypeHint}";
            _waveFooter.Visible = true;
        }
        else
        {
            _waveFooter.Visible = false;
        }

        if (lastWaveReport != null && waveNumber > 1)
        {
            var topTower = lastWaveReport.TopDamageDealer;
            if (topTower != null)
            {
                var towerDef = DataLoader.GetTowerDef(topTower.TowerId);
                _wavePerformance.Text = lastWaveReport.Leaks > 0
                    ? $"Wave {lastWaveReport.WaveNumber}: {lastWaveReport.Leaks} leak(s)  |  Top damage: {towerDef.Name} ({topTower.Damage})"
                    : $"Wave {lastWaveReport.WaveNumber}: Perfect clear  |  Top damage: {towerDef.Name} ({topTower.Damage})";
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

        var vpSize = GetViewport().GetVisibleRect().Size;
        _bg.Position = Vector2.Zero;
        _bg.Size = vpSize;
        _bg.Visible = true;
        _bgFx.Position = Vector2.Zero;
        _bgFx.Size = vpSize;
        _bgFx.Visible = true;
        _center.Position = Vector2.Zero;
        _center.Size = vpSize;
        _center.Visible = true;

        BuildCardRow(options);

        bool showBanner = waveNumber == 1 && pickNumber == 1
                       && (SettingsManager.Instance?.IsFirstRun ?? false);
        if (showBanner)
        {
            SetBannerPage(0);
            _firstRunBanner.Visible = true;
        }
        else
        {
            _firstRunBanner.Visible = false;
        }

        Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey { Pressed: true } key) return;

        if (key.Keycode == Key.Escape && (_pendingTower != null || _pendingModifier != null))
        {
            CancelAssignment();
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
            _ => -1,
        };
        if (idx < 0) return;

        var row = _cardRow.Visible ? _cardRow : (_towerRow.Visible ? _towerRow : null);
        if (row == null) return;

        var children = row.GetChildren();
        if (idx < children.Count && children[idx] is Button btn && !btn.Disabled)
        {
            btn.EmitSignal(Button.SignalName.Pressed);
            GetViewport().SetInputAsHandled();
        }
    }

    public void CancelAssignment()
    {
        if (_pendingTower == null && _pendingModifier == null) return;
        _pendingTower = null;
        _pendingModifier = null;
        _previewTowerSlot = -1;
        _previewTowerSetAtMs = 0;
        Show(_lastOptions, _lastWaveNumber, _lastPickNumber, _lastTotalPicks, null);
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */ && Visible)
        {
            if (_pendingTower != null || _pendingModifier != null)
                CancelAssignment();
        }
    }

    private void BuildCardRow(List<DraftOption> options)
    {
        foreach (Node child in _cardRow.GetChildren())
            child.Free();

        float vpW = GetViewport().GetVisibleRect().Size.X;
        // Card width must be computed in unscaled UI space; otherwise mobile UI scaling can overflow the row.
        float uiScale = Mathf.Max(0.01f, _center.Scale.X);
        float layoutW = vpW / uiScale;
        int cardCount = Mathf.Max(1, options.Count);
        int spacing = 14;
        float sidePadding = 44f;
        float cardWidth = 230f;

        // Adaptive sizing keeps all five cards visible on tablet / narrow viewports.
        if (MobileOptimization.IsTablet() || layoutW < 1240f)
        {
            spacing = layoutW < 980f ? 8 : 10;
            cardWidth = (layoutW - sidePadding - spacing * (cardCount - 1)) / cardCount;
            cardWidth = Mathf.Clamp(cardWidth, 140f, 220f);
        }

        float widthScale = Mathf.Clamp(cardWidth / 230f, 0.72f, 1f);
        // Taller cards to fit roughly two extra description lines.
        float cardHeight = Mathf.Lerp(132f, 186f, widthScale);
        int titleSize = Mathf.RoundToInt(21f * widthScale);
        int bodySize = Mathf.RoundToInt(14f * widthScale);
        int statSize = Mathf.RoundToInt(13f * widthScale);
        int tagSize = Mathf.RoundToInt(12f * widthScale);
        float iconSize = Mathf.Lerp(22f, 28f, widthScale);
        int marginX = Mathf.RoundToInt(Mathf.Lerp(8f, 12f, widthScale));
        int marginTop = Mathf.RoundToInt(Mathf.Lerp(8f, 10f, widthScale));
        int marginBottom = Mathf.RoundToInt(Mathf.Lerp(8f, 10f, widthScale));

        _cardRow.AddThemeConstantOverride("separation", spacing);

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var accent = GetOptionAccent(opt);
            var btn = new Button
            {
                Text = "",
                CustomMinimumSize = new Vector2(cardWidth, cardHeight),
                PivotOffset = new Vector2(cardWidth * 0.5f, cardHeight * 0.5f),
                Alignment = HorizontalAlignment.Left,
                Visible = true,
            };
            ApplyCardStyle(btn, opt);

            var front = new Control
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                PivotOffset = new Vector2(cardWidth * 0.5f, cardHeight * 0.5f),
                Scale = new Vector2(0.02f, 1f),
                Visible = false,
            };
            front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            btn.AddChild(front);

            var accentStrip = new ColorRect
            {
                Color = new Color(accent.R, accent.G, accent.B, 0.86f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            accentStrip.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            accentStrip.OffsetLeft = 6;
            accentStrip.OffsetRight = -6;
            accentStrip.OffsetTop = 5;
            accentStrip.OffsetBottom = 9;
            front.AddChild(accentStrip);

            var captured = opt;
            btn.Pressed += () => OnCardPressed(captured, btn);
            AddHover(btn, captured);
            BindTouchHoldHint(btn, captured);
            BindTouchCardPreview(btn, captured);

            var cardBody = new MarginContainer();
            cardBody.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            cardBody.AddThemeConstantOverride("margin_left", marginX);
            cardBody.AddThemeConstantOverride("margin_top", marginTop);
            cardBody.AddThemeConstantOverride("margin_right", marginX);
            cardBody.AddThemeConstantOverride("margin_bottom", marginBottom);
            cardBody.MouseFilter = Control.MouseFilterEnum.Ignore;
            front.AddChild(cardBody);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            cardBody.AddChild(vbox);

            (Control? iconNode, Control? titleNode) punchTargets;
            if (opt.Type == DraftOptionType.Tower)
                punchTargets = BuildTowerCard(vbox, opt.Id, titleSize, statSize, bodySize, iconSize);
            else
                punchTargets = BuildModifierCard(vbox, opt.Id, titleSize, tagSize, bodySize, iconSize);

            var keyLbl = new Label
            {
                Text = $"[ {i + 1} ]",
                Modulate = new Color(0.55f, 0.55f, 0.75f, 0.80f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            UITheme.ApplyFont(keyLbl, semiBold: true, size: 12);
            keyLbl.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            keyLbl.OffsetLeft = -46;
            keyLbl.OffsetRight = -4;
            keyLbl.OffsetTop = -20;
            keyLbl.OffsetBottom = -4;
            front.AddChild(keyLbl);

            var cardBack = BuildCardBack(accent, cardWidth, cardHeight);
            btn.AddChild(cardBack);

            _cardRow.AddChild(btn);
            AnimateCardReveal(btn, front, cardBack, accent, i, punchTargets.iconNode, punchTargets.titleNode, isFoil: i == _foilCardIndex);
        }
    }

    private void OnCardPressed(DraftOption opt, Button sourceButton)
    {
        // Touch hold-preview should never immediately select the card on release.
        if (_touchPreviewActive)
            return;
        if (_suppressNextCardPress)
        {
            _suppressNextCardPress = false;
            return;
        }
        if (_isCardCommitInFlight)
            return;

        _isCardCommitInFlight = true;
        SoundManager.Instance?.Play("ui_card_pick");
        SoundManager.Instance?.Play("ui_thunk");
        _previewModifierSlot = -1;
        _previewSetAtMs = 0;
        ClearModifierSynergyHint();
        _touchHoldModifierId = "";
        _touchHintShown = false;
        if (opt.Type == DraftOptionType.Tower)
            _pendingTower = opt;
        else
            _pendingModifier = opt;
        if (GodotObject.IsInstanceValid(sourceButton))
        {
            var rect = sourceButton.GetGlobalRect();
            var start = rect.Position + rect.Size * 0.5f;
            GameController.Instance?.PlayDraftCardSpirit(start, opt);
        }
        PulseDraftVignette();
        GetTree().CreateTimer(0.06f).Timeout += () =>
        {
            // Don't hide if player is already in slot-selection — ShowPlacementUI already owns Visible.
            if (!GodotObject.IsInstanceValid(this) || IsAwaitingSlot || IsAwaitingTower) return;
            var tw = CreateTween();
            tw.SetParallel(true);
            tw.TweenProperty(_bg,    "modulate:a", 0f, 0.10f);
            tw.TweenProperty(_bgFx,  "modulate:a", 0f, 0.10f);
            tw.TweenProperty(_center,"modulate:a", 0f, 0.10f);
            tw.Chain().TweenCallback(Callable.From(() =>
            {
                Visible = false;
                _bg.Modulate    = Colors.White;
                _bgFx.Modulate  = Colors.White;
                _center.Modulate = Colors.White;
            }));
        };
    }

    private void OnModifierSlotPicked(int slotIndex)
    {
        if (_pendingModifier == null) return;
        ulong nowMs = Time.GetTicksMsec();
        if (_previewModifierSlot == slotIndex)
        {
            // Require a distinct follow-up tap (avoids touch->mouse event duplication confirming instantly).
            if (_previewSetAtMs > 0 && (nowMs - _previewSetAtMs) < 120)
                return;

            Visible = false;
            var pick = _pendingModifier;
            if (pick == null) return;

            GameController.Instance?.PlayModifierLockInFx(slotIndex, pick.Id, () =>
            {
                _previewModifierSlot = -1;
                _previewSetAtMs = 0;
                GameController.Instance.OnDraftPick(pick, slotIndex);
            });
            return;
        }

        _previewModifierSlot = slotIndex;
        _previewSetAtMs = nowMs;
        SoundManager.Instance?.Play("ui_preview_ghost");
    }

    private void OnSlotPicked(int slotIndex)
    {
        if (_pendingTower == null) return;
        ulong nowMs = Time.GetTicksMsec();
        if (_previewTowerSlot == slotIndex)
        {
            // Require a distinct follow-up tap (avoids touch->mouse event duplication confirming instantly).
            if (_previewTowerSetAtMs > 0 && (nowMs - _previewTowerSetAtMs) < 120)
                return;

            Visible = false;
            var pick = _pendingTower;
            if (pick == null) return;
            _previewTowerSlot = -1;
            _previewTowerSetAtMs = 0;
            GameController.Instance.OnDraftPick(pick, slotIndex);
            return;
        }

        _previewTowerSlot = slotIndex;
        _previewTowerSetAtMs = nowMs;
        SoundManager.Instance?.Play("ui_preview_ghost");
    }

    private void AddHover(Button btn, DraftOption opt)
    {
        btn.MouseEntered += () =>
        {
            SoundManager.Instance?.Play("ui_hover");
            var tw = btn.CreateTween();
            tw.TweenProperty(btn, "scale", new Vector2(1.06f, 1.06f), 0.08f);
            if (opt.Type == DraftOptionType.Modifier)
                SetModifierSynergyHint(opt.Id);
        };

        btn.MouseExited += () =>
        {
            var tw = btn.CreateTween();
            tw.TweenProperty(btn, "scale", Vector2.One, 0.08f);
            if (opt.Type == DraftOptionType.Modifier)
                ClearModifierSynergyHint(opt.Id);
        };
    }

    private void BindTouchHoldHint(Button btn, DraftOption opt)
    {
        if (opt.Type != DraftOptionType.Modifier) return;
        btn.GuiInput += (@event) =>
        {
            if (@event is not InputEventScreenTouch touch) return;
            if (touch.Pressed)
            {
                _touchHoldModifierId = opt.Id;
                _touchHoldStartMs = Time.GetTicksMsec();
                _touchHintShown = false;
            }
            else if (_touchHoldModifierId == opt.Id)
            {
                _touchHoldModifierId = "";
                _touchHintShown = false;
                ClearModifierSynergyHint(opt.Id);
            }
        };
    }

    private void BindTouchCardPreview(Button btn, DraftOption opt)
    {
        btn.GuiInput += (@event) =>
        {
            if (@event is not InputEventScreenTouch touch) return;

            if (touch.Pressed)
            {
                _touchPreviewCard = btn;
                _touchPreviewStartMs = Time.GetTicksMsec();
                _touchPreviewActive = false;
                _suppressNextCardPress = false;
            }
            else if (_touchPreviewCard == btn)
            {
                if (_touchPreviewActive)
                {
                    btn.AcceptEvent();
                    _suppressNextCardPress = true;
                    btn.ZIndex = 0;
                    var tw = btn.CreateTween();
                    tw.TweenProperty(btn, "scale", Vector2.One, 0.08f)
                      .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
                }

                _touchPreviewCard = null;
                _touchPreviewActive = false;
                _touchPreviewStartMs = 0;
                if (opt.Type == DraftOptionType.Modifier)
                {
                    _touchHoldModifierId = "";
                    _touchHintShown = false;
                    ClearModifierSynergyHint(opt.Id);
                }
            }
        };
    }

    private void PulseDraftVignette()
    {
        if (!GodotObject.IsInstanceValid(_bg)) return;
        _bg.Color = new Color(0f, 0f, 0f, 0.70f);
        var tw = _bg.CreateTween();
        tw.TweenProperty(_bg, "color", new Color(0f, 0f, 0f, 0.82f), 0.03f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_bg, "color", new Color(0f, 0f, 0f, 0.70f), 0.06f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private void AnimateCardReveal(Button btn, Control front, Control cardBack, Color accent, int index, Control? iconNode, Control? titleNode, bool isFoil)
    {
        // Reduced Motion: skip flip delay and animations entirely
        if (SlotTheory.Core.SettingsManager.Instance?.ReducedMotion == true)
        {
            if (!GodotObject.IsInstanceValid(front) || !GodotObject.IsInstanceValid(cardBack)) return;
            cardBack.Visible = false;
            front.Visible = true;
            front.Scale = Vector2.One;
            btn.Scale = Vector2.One;
            btn.Modulate = Colors.White;
            return;
        }

        float delay = CardFaceDownHoldSeconds + index * CardStaggerSeconds;
        btn.GetTree().CreateTimer(delay).Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(btn)) return;
            if (!GodotObject.IsInstanceValid(front) || !GodotObject.IsInstanceValid(cardBack)) return;

            SoundManager.Instance?.Play("card_shing");
            btn.Scale = new Vector2(0.92f, 0.92f);
            btn.Modulate = new Color(1f, 1f, 1f, 0.82f);

            var cardPunch = btn.CreateTween();
            cardPunch.SetParallel(true);
            cardPunch.TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), CardEntranceSeconds * 0.30f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            cardPunch.TweenProperty(btn, "modulate:a", 1f, CardEntranceSeconds * 0.28f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            cardPunch.Chain().SetParallel(true);
            cardPunch.TweenProperty(btn, "scale", Vector2.One, CardEntranceSeconds * 0.24f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);

            var flipOut = btn.CreateTween();
            flipOut.SetParallel(true);
            flipOut.TweenProperty(cardBack, "scale:x", 0f, CardEntranceSeconds * 0.40f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.In);
            flipOut.TweenProperty(cardBack, "scale:y", 1.14f, CardEntranceSeconds * 0.40f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            flipOut.Chain();
            flipOut.TweenCallback(Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(cardBack) || !GodotObject.IsInstanceValid(front)) return;
                cardBack.Visible = false;
                front.Visible = true;
                front.Scale = new Vector2(0.02f, 1.14f);
                PlayRevealBurst(front, btn.CustomMinimumSize, accent);
            }));

            var flipIn = btn.CreateTween();
            flipIn.TweenInterval(CardEntranceSeconds * 0.40f);
            flipIn.SetParallel(true);
            flipIn.TweenProperty(front, "scale:x", 1.12f, CardEntranceSeconds * 0.34f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            flipIn.TweenProperty(front, "scale:y", 1.06f, CardEntranceSeconds * 0.34f)
                .SetTrans(Tween.TransitionType.Back)
                .SetEase(Tween.EaseType.Out);
            flipIn.Chain();
            flipIn.TweenProperty(front, "scale", Vector2.One, CardEntranceSeconds * 0.22f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);

            if (iconNode != null && titleNode != null)
            {
                var punch = btn.CreateTween();
                punch.TweenInterval(CardEntranceSeconds * 0.70f);
                punch.SetParallel(true);
                punch.TweenProperty(iconNode, "scale", new Vector2(1.08f, 1.08f), 0.085f);
                punch.TweenProperty(titleNode, "scale", new Vector2(1.06f, 1.06f), 0.085f);
                punch.Chain().SetParallel(true);
                punch.TweenProperty(iconNode, "scale", Vector2.One, 0.08f);
                punch.TweenProperty(titleNode, "scale", Vector2.One, 0.08f);
            }

            if (isFoil)
            {
                var foilDelay = CardEntranceSeconds * 0.85f;
                btn.GetTree().CreateTimer(foilDelay).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(btn) && GodotObject.IsInstanceValid(front))
                        PlayFoilShimmer(front, btn.CustomMinimumSize);
                };
            }
        };
    }

    private static Control BuildCardBack(Color accent, float cardWidth, float cardHeight)
    {
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            PivotOffset = new Vector2(cardWidth * 0.5f, cardHeight * 0.5f),
            Scale = Vector2.One,
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        static StyleBoxFlat BackBox(Color border)
        {
            var b = new StyleBoxFlat();
            b.BgColor = new Color(0.035f, 0.05f, 0.12f, 0.98f);
            b.BorderColor = border;
            b.BorderWidthTop = 2;
            b.BorderWidthBottom = 2;
            b.BorderWidthLeft = 2;
            b.BorderWidthRight = 2;
            b.CornerRadiusTopLeft = 8;
            b.CornerRadiusTopRight = 8;
            b.CornerRadiusBottomLeft = 8;
            b.CornerRadiusBottomRight = 8;
            return b;
        }

        var panel = new Panel
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = UITheme.Build(),
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride("panel", BackBox(new Color(accent.R * 0.80f, accent.G * 0.80f, accent.B * 0.80f, 1f)));
        root.AddChild(panel);

        var strip = new ColorRect
        {
            Color = new Color(accent.R, accent.G, accent.B, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        strip.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        strip.OffsetLeft = 7f;
        strip.OffsetRight = -7f;
        strip.OffsetTop = 6f;
        strip.OffsetBottom = 10f;
        root.AddChild(strip);

        var glyph = new Label
        {
            Text = "?",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(0.72f, 0.80f, 1.00f, 0.76f),
        };
        glyph.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        int glyphSize = Mathf.Clamp(Mathf.RoundToInt(cardHeight * 0.38f), 36, 58);
        UITheme.ApplyFont(glyph, semiBold: true, size: glyphSize);
        root.AddChild(glyph);
        return root;
    }

    private static void PlayRevealBurst(Control front, Vector2 cardSize, Color accent)
    {
        var pulse = new ColorRect
        {
            Color = new Color(1.00f, 1.00f, 1.00f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        pulse.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        front.AddChild(pulse);

        var pulseTween = pulse.CreateTween();
        pulseTween.TweenProperty(pulse, "color:a", 0.26f, 0.045f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        pulseTween.TweenProperty(pulse, "color:a", 0f, 0.20f)
            .SetTrans(Tween.TransitionType.Expo)
            .SetEase(Tween.EaseType.Out);
        pulseTween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(pulse))
                pulse.QueueFree();
        }));

        var streak = new ColorRect
        {
            Color = new Color(accent.R, accent.G, accent.B, 0f),
            Position = new Vector2(-78f, -24f),
            Size = new Vector2(48f, cardSize.Y + 48f),
            RotationDegrees = 20f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        front.AddChild(streak);

        var streakTween = streak.CreateTween();
        streakTween.SetParallel(true);
        streakTween.TweenProperty(streak, "color:a", 0.26f, 0.07f);
        streakTween.TweenProperty(streak, "position:x", cardSize.X + 78f, 0.30f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        streakTween.Chain().TweenProperty(streak, "color:a", 0f, 0.10f);
        streakTween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(streak))
                streak.QueueFree();
        }));
    }

    private static void PlayFoilShimmer(Control front, Vector2 cardSize)
    {
        var shimmer = new ColorRect
        {
            Color = new Color(0.85f, 1.00f, 1.00f, 0f),
            Position = new Vector2(-52f, -20f),
            Size = new Vector2(34f, cardSize.Y + 40f),
            RotationDegrees = 18f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        front.AddChild(shimmer);

        var tw = shimmer.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(shimmer, "color:a", 0.22f, 0.08f);
        tw.TweenProperty(shimmer, "position:x", cardSize.X + 52f, 0.45f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tw.Chain();
        tw.TweenProperty(shimmer, "color:a", 0f, 0.08f);
        tw.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(shimmer))
                shimmer.QueueFree();
        }));
    }

    private static void ApplyCardStyle(Button btn, DraftOption opt)
    {
        Color accent = GetOptionAccent(opt);

        static StyleBoxFlat Box(Color bg, Color border)
        {
            var b = new StyleBoxFlat();
            b.BgColor = bg;
            b.BorderColor = border;
            b.BorderWidthTop = 2;
            b.BorderWidthBottom = 2;
            b.BorderWidthLeft = 2;
            b.BorderWidthRight = 2;
            b.CornerRadiusTopLeft = 8;
            b.CornerRadiusTopRight = 8;
            b.CornerRadiusBottomLeft = 8;
            b.CornerRadiusBottomRight = 8;
            return b;
        }

        btn.AddThemeStyleboxOverride("normal", Box(new Color(0.05f, 0.06f, 0.13f, 0.96f), new Color(accent.R * 0.75f, accent.G * 0.75f, accent.B * 0.75f, 1f)));
        btn.AddThemeStyleboxOverride("hover", Box(new Color(0.08f, 0.10f, 0.20f, 0.98f), accent));
        btn.AddThemeStyleboxOverride("pressed", Box(new Color(0.11f, 0.08f, 0.18f, 0.98f), accent));
        btn.AddThemeStyleboxOverride("focus", Box(new Color(0.09f, 0.10f, 0.22f, 0.98f), accent));
        btn.AddThemeColorOverride("font_color", Colors.White);
    }

    private static Color GetOptionAccent(DraftOption opt) => opt.Type == DraftOptionType.Modifier
        ? ModifierVisuals.GetAccent(opt.Id)
        : GetTowerAccent(opt.Id);

    private static (Control? iconNode, Control? titleNode) BuildTowerCard(VBoxContainer root, string towerId, int titleSize, int statSize, int bodySize, float iconSize)
    {
        var def = DataLoader.GetTowerDef(towerId);
        var accent = GetTowerAccent(towerId);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 8);
        root.AddChild(top);

        var glyphWrap = new ColorRect
        {
            Color = new Color(accent.R, accent.G, accent.B, 0.25f),
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            Size = new Vector2(iconSize, iconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        top.AddChild(glyphWrap);

        var glyph = new Label
        {
            Text = TowerGlyph(towerId),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        glyph.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UITheme.ApplyFont(glyph, semiBold: true, size: Mathf.Max(12, Mathf.RoundToInt(iconSize * 0.56f)));
        glyphWrap.AddChild(glyph);

        var title = new Label
        {
            Text = def.Name,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        UITheme.ApplyFont(title, semiBold: true, size: titleSize);
        title.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.96f);
        top.AddChild(title);

        var stats = new Label
        {
            Text = $"{def.BaseDamage:0.#} DMG   {def.AttackInterval:0.##}s   R {def.Range:0}",
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        UITheme.ApplyFont(stats, semiBold: true, size: statSize);
        stats.Modulate = new Color(0.75f, 0.88f, 1f, 0.9f);
        root.AddChild(stats);

        var role = new Label
        {
            Text = TowerRole(towerId),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        UITheme.ApplyFont(role, size: bodySize);
        role.Modulate = new Color(0.88f, 0.88f, 0.95f, 0.9f);
        root.AddChild(role);
        return (glyphWrap, title);
    }

    private static (Control? iconNode, Control? titleNode) BuildModifierCard(VBoxContainer root, string modifierId, int titleSize, int tagSize, int bodySize, float iconSize)
    {
        var def = DataLoader.GetModifierDef(modifierId);
        var accent = ModifierVisuals.GetAccent(modifierId);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 8);
        root.AddChild(top);

        var iconHolder = new ColorRect
        {
            Color = new Color(accent.R, accent.G, accent.B, 0.20f),
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            Size = new Vector2(iconSize, iconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        top.AddChild(iconHolder);

        var icon = new ModifierIcon
        {
            ModifierId = modifierId,
            IconColor = accent,
        };
        icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        iconHolder.AddChild(icon);

        var title = new Label
        {
            Text = def.Name,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        UITheme.ApplyFont(title, semiBold: true, size: titleSize);
        title.Modulate = new Color(1f, 1f, 1f, 0.97f);
        top.AddChild(title);

        var tag = new Label
        {
            Text = ModifierVisuals.GetTag(modifierId),
            AutowrapMode = TextServer.AutowrapMode.Off,
        };
        UITheme.ApplyFont(tag, semiBold: true, size: tagSize);
        tag.Modulate = new Color(accent.R, accent.G, accent.B, 0.95f);
        root.AddChild(tag);

        string synergyTag = GetModifierSynergyTag(modifierId);
        if (synergyTag.Length > 0)
        {
            var synergy = new Label
            {
                Text = synergyTag,
                AutowrapMode = TextServer.AutowrapMode.Off,
            };
            UITheme.ApplyFont(synergy, semiBold: true, size: Mathf.Max(10, tagSize - 1));
            synergy.Modulate = new Color(0.86f, 0.90f, 1.00f, 0.68f);
            root.AddChild(synergy);
        }

        var desc = new Label
        {
            Text = def.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        UITheme.ApplyFont(desc, size: bodySize);
        desc.Modulate = new Color(0.86f, 0.86f, 0.94f, 0.92f);
        root.AddChild(desc);
        return (iconHolder, title);
    }

    private static string GetModifierSynergyTag(string modifierId) => modifierId switch
    {
        "exploit_weakness" => "GOOD WITH: MARKED",
        "chain_reaction"   => "GOOD WITH: ARC EMITTER",
        "overkill"         => "SYNERGY: BIG HITS",
        "focus_lens"       => "SYNERGY: BIG HITS",
        _ => "",
    };

    private static Color GetTowerAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.25f, 0.92f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.60f, 0.18f),
        "marker_tower" => new Color(1.00f, 0.30f, 0.72f),
        "chain_tower" => new Color(0.62f, 0.90f, 1.00f),
        "rift_prism" => new Color(0.60f, 1.00f, 0.58f),
        _ => new Color(0.75f, 0.85f, 1.00f),
    };

    private static string TowerGlyph(string towerId) => towerId switch
    {
        "rapid_shooter" => "RS",
        "heavy_cannon" => "HC",
        "marker_tower" => "MK",
        "chain_tower" => "AR",
        "rift_prism" => "SA",
        _ => "TW",
    };

    private static string TowerRole(string towerId) => towerId switch
    {
        "rapid_shooter" => "Fast single-target pressure.",
        "heavy_cannon" => "Slow burst shots with heavy hits.",
        "marker_tower" => "Applies mark to amplify team damage.",
        "chain_tower" => "Bounces into grouped enemy packs.",
        "rift_prism" => "Plants charged mines along the lane. Final charge pops harder; rapid seeding at wave start.",
        _ => "Generalist tower.",
    };

    private void SetModifierSynergyHint(string modifierId)
    {
        if (_hoveredModifierHintId == modifierId) return;
        _hoveredModifierHintId = modifierId;
        GameController.Instance?.SetDraftSynergyHint(modifierId);
    }

    private void ClearModifierSynergyHint(string expectedModifierId = "")
    {
        if (expectedModifierId.Length > 0 && _hoveredModifierHintId != expectedModifierId) return;
        if (_hoveredModifierHintId.Length == 0) return;
        _hoveredModifierHintId = "";
        GameController.Instance?.SetDraftSynergyHint("");
    }

    private void AnimateBonusPickStamp()
    {
        _bonusStamp.Visible = true;
        _bonusStamp.Scale = new Vector2(1.18f, 1.18f);
        _bonusStamp.RotationDegrees = -4.5f;
        _bonusStamp.Modulate = new Color(1f, 1f, 1f, 0f);
        var tw = _bonusStamp.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(_bonusStamp, "modulate:a", 1f, 0.10f);
        tw.TweenProperty(_bonusStamp, "scale", Vector2.One * 1.04f, 0.12f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tw.TweenProperty(_bonusStamp, "rotation_degrees", 0f, 0.12f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        tw.Chain().TweenProperty(_bonusStamp, "scale", Vector2.One, 0.08f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        PlaySignatureScanline(_bonusStamp, new Color(1.0f, 0.82f, 0.36f, 0.85f));
    }

    private static void PlaySignatureScanline(Control target, Color color)
    {
        var stripe = new ColorRect
        {
            Color = new Color(color.R, color.G, color.B, 0f),
            Position = new Vector2(-58f, -6f),
            Size = new Vector2(42f, 18f),
            RotationDegrees = 13f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        target.AddChild(stripe);

        var tw = stripe.CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(stripe, "color:a", 0.34f, 0.05f);
        tw.TweenProperty(stripe, "position:x", target.Size.X + 58f, 0.30f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tw.Chain().TweenProperty(stripe, "color:a", 0f, 0.08f);
        tw.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(stripe))
                stripe.QueueFree();
        }));
    }

    /// <summary>
    /// Generates a subtle archetype hint based on wave composition.
    /// Helps players understand what build strategies might work well.
    /// </summary>
    private string GenerateArchetypeHint(WaveConfig cfg)
    {
        if (cfg.TankyCount + cfg.SwiftCount < 2) return "";

        List<string> hints = new();

        if (cfg.TankyCount >= 3)
            hints.Add("Marker recommended");
        else if (cfg.TankyCount >= 1 && cfg.SwiftCount >= 2)
            hints.Add("AoE favored");
        else if (cfg.SwiftCount >= 3)
            hints.Add("Range coverage");
        else if (cfg.ClumpArmored && cfg.TankyCount >= 2)
            hints.Add("Burst damage");

        return hints.Count > 0 ? $"  |  {string.Join(", ", hints)}" : "";
    }
}
