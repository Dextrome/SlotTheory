using Godot;
using SlotTheory.Core;
using SlotTheory.Data;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen reveal shown when a tower/modifier is newly unlocked.
/// </summary>
public partial class UnlockRevealScreen : CanvasLayer
{
    [Signal]
    public delegate void RevealClosedEventHandler();

    private Control _root = null!;
    private Label _titleLabel = null!;
    private PanelContainer _cardPanel = null!;
    private Label _cardTypeLabel = null!;
    private Label _cardNameLabel = null!;
    private Label _cardStatsLabel = null!;
    private Label _cardDescLabel = null!;
    private VBoxContainer _layout = null!;
    private PanelContainer _graphicPanel = null!;
    private CenterContainer _graphicHolder = null!;
    private Button _okButton = null!;
    private ColorRect _flashOverlay = null!;
    private Tween? _openTween;
    private bool _canClose;

    public bool IsShowing => Visible;

    public override void _Ready()
    {
        Layer = 12;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.Theme = UITheme.Build();
        _root.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(_root);

        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0f, 0f, 0f, 0.965f);
        _root.AddChild(bg);

        var burstTint = new ColorRect();
        burstTint.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        burstTint.Color = new Color(0.10f, 0.24f, 0.46f, 0.34f);
        _root.AddChild(burstTint);

        _flashOverlay = new ColorRect();
        _flashOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _flashOverlay.Color = new Color(1f, 1f, 1f, 0f);
        _root.AddChild(_flashOverlay);

        var center = MobileOptimization.MakeScaledRoot(_root, pinchZoom: false);
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", MobileOptimization.IsMobile() ? 12 : 22);
        margin.AddThemeConstantOverride("margin_right", MobileOptimization.IsMobile() ? 12 : 22);
        margin.AddThemeConstantOverride("margin_top", MobileOptimization.IsMobile() ? 10 : 16);
        margin.AddThemeConstantOverride("margin_bottom", MobileOptimization.IsMobile() ? 10 : 16);
        center.AddChild(margin);

        var columnCenter = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        margin.AddChild(columnCenter);

        _layout = new VBoxContainer();
        _layout.AddThemeConstantOverride("separation", MobileOptimization.IsMobile() ? 6 : 8);
        _layout.CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 280f : 420f, 0f);
        _layout.Resized += FitToViewport;
        columnCenter.AddChild(_layout);

        _titleLabel = new Label
        {
            Text = "UNLOCKED",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        UITheme.ApplyFont(_titleLabel, bold: true, size: MobileOptimization.IsMobile() ? 40 : 50);
        _titleLabel.Modulate = new Color(0.78f, 0.98f, 1.00f, 1f);
        _layout.AddChild(_titleLabel);

        _cardPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 118f : 142f)
        };
        _cardPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.07f, 0.08f, 0.17f, 0.96f),
            border: new Color(0.36f, 0.76f, 1.00f, 0.92f),
            corners: 12,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 9 : 12,
            padV: MobileOptimization.IsMobile() ? 8 : 10));
        _layout.AddChild(_cardPanel);

        var cardBody = new VBoxContainer();
        cardBody.AddThemeConstantOverride("separation", 4);
        _cardPanel.AddChild(cardBody);

        _cardTypeLabel = new Label();
        UITheme.ApplyFont(_cardTypeLabel, semiBold: true, size: MobileOptimization.IsMobile() ? 12 : 14);
        _cardTypeLabel.Modulate = new Color(0.62f, 1.00f, 0.95f);
        cardBody.AddChild(_cardTypeLabel);

        _cardNameLabel = new Label();
        UITheme.ApplyFont(_cardNameLabel, bold: true, size: MobileOptimization.IsMobile() ? 21 : 26);
        _cardNameLabel.Modulate = Colors.White;
        cardBody.AddChild(_cardNameLabel);

        _cardStatsLabel = new Label();
        UITheme.ApplyFont(_cardStatsLabel, semiBold: true, size: MobileOptimization.IsMobile() ? 12 : 14);
        _cardStatsLabel.Modulate = new Color(0.66f, 0.84f, 1.00f);
        cardBody.AddChild(_cardStatsLabel);

        _cardDescLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        UITheme.ApplyFont(_cardDescLabel, size: MobileOptimization.IsMobile() ? 11 : 13);
        _cardDescLabel.Modulate = new Color(0.80f, 0.87f, 0.96f, 0.95f);
        cardBody.AddChild(_cardDescLabel);

        _graphicPanel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 140f : 172f, MobileOptimization.IsMobile() ? 140f : 172f)
        };
        _graphicPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.06f, 0.08f, 0.16f, 0.96f),
            border: new Color(0.36f, 0.76f, 1.00f, 0.88f),
            corners: 999,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 12 : 16,
            padV: MobileOptimization.IsMobile() ? 12 : 16));
        _layout.AddChild(_graphicPanel);

        _graphicHolder = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        _graphicPanel.AddChild(_graphicHolder);

        // Keep the CTA near the reveal content so the whole stack sits a bit higher.
        _layout.AddChild(new Control { CustomMinimumSize = new Vector2(0f, MobileOptimization.IsMobile() ? 2f : 4f) });

        var buttonWrap = new CenterContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _layout.AddChild(buttonWrap);

        _okButton = new Button
        {
            Text = "Okay",
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 164f : 208f, MobileOptimization.IsMobile() ? 38f : 44f),
            Disabled = true
        };
        UITheme.ApplyPrimaryStyle(_okButton);
        _okButton.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 18 : 22);
        _okButton.Pressed += OnOkayPressed;
        buttonWrap.AddChild(_okButton);
        CallDeferred(nameof(FitToViewport));
    }

    public void ShowTowerUnlock(string towerId)
    {
        var def = DataLoader.GetTowerDef(towerId);
        Color accent = GetTowerAccent(towerId);

        _cardTypeLabel.Text = "NEW TOWER";
        _cardTypeLabel.Modulate = accent;
        _cardNameLabel.Text = def.Name;
        _cardStatsLabel.Visible = true;
        _cardStatsLabel.Text = $"{def.BaseDamage:0.#} dmg  |  {def.AttackInterval:0.##} s  |  {(int)def.Range} px";
        _cardDescLabel.Text = GetTowerRevealDescription(towerId);

        ApplyAccent(accent);
        SetGraphic(BuildTowerGraphic(towerId));
        CallDeferred(nameof(FitToViewport));
        OpenReveal();
    }

    public void ShowModifierUnlock(string modifierId)
    {
        var def = DataLoader.GetModifierDef(modifierId);
        Color accent = ModifierVisuals.GetAccent(modifierId);

        _cardTypeLabel.Text = "NEW MODIFIER";
        _cardTypeLabel.Modulate = accent;
        _cardNameLabel.Text = def.Name;
        _cardStatsLabel.Visible = false;
        _cardDescLabel.Text = def.Description;

        ApplyAccent(accent);
        SetGraphic(BuildModifierGraphic(modifierId, accent));
        CallDeferred(nameof(FitToViewport));
        OpenReveal();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
            return;

        if (@event.IsActionPressed("ui_accept") || @event.IsActionPressed("ui_cancel"))
        {
            if (_canClose)
                CloseReveal();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OpenReveal()
    {
        Visible = true;
        _canClose = false;
        _okButton.Disabled = true;
        _okButton.ReleaseFocus();
        FitToViewport();
        CallDeferred(nameof(FitToViewport));
        SoundManager.Instance?.Play("ui_select");

        _openTween?.Kill();
        _root.Modulate = new Color(1f, 1f, 1f, 0f);
        _titleLabel.Modulate = new Color(_titleLabel.Modulate.R, _titleLabel.Modulate.G, _titleLabel.Modulate.B, 0f);
        _titleLabel.Scale = new Vector2(1.24f, 1.24f);
        _cardPanel.Modulate = new Color(1f, 1f, 1f, 0f);
        _cardPanel.Scale = new Vector2(0.72f, 0.72f);
        _cardPanel.Rotation = -0.06f;
        _graphicPanel.Modulate = new Color(1f, 1f, 1f, 0f);
        _graphicPanel.Scale = new Vector2(0.18f, 0.18f);
        _flashOverlay.Color = new Color(1f, 1f, 1f, 0f);

        _openTween = CreateTween();
        _openTween.TweenProperty(_root, "modulate:a", 1f, 0.13f);
        _openTween.Parallel().TweenProperty(_flashOverlay, "color:a", 0.84f, 0.06f);
        _openTween.Parallel().TweenProperty(_titleLabel, "modulate:a", 1f, 0.16f);
        _openTween.Parallel().TweenProperty(_titleLabel, "scale", Vector2.One, 0.24f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        _openTween.TweenProperty(_flashOverlay, "color:a", 0f, 0.24f);

        _openTween.TweenProperty(_cardPanel, "modulate:a", 1f, 0.14f);
        _openTween.Parallel().TweenProperty(_cardPanel, "scale", Vector2.One, 0.36f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _openTween.Parallel().TweenProperty(_cardPanel, "rotation", 0f, 0.36f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);

        _openTween.TweenInterval(0.05f);
        _openTween.TweenCallback(Callable.From(() => SoundManager.Instance?.Play("ui_hover")));
        _openTween.TweenProperty(_graphicPanel, "modulate:a", 1f, 0.08f);
        _openTween.Parallel().TweenProperty(_graphicPanel, "scale", Vector2.One, 0.30f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _openTween.TweenCallback(Callable.From(() =>
        {
            _canClose = true;
            _okButton.Disabled = false;
            _okButton.GrabFocus();
        }));
    }

    private void FitToViewport()
    {
        if (!GodotObject.IsInstanceValid(_layout))
            return;

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 minSize = _layout.GetCombinedMinimumSize();
        Vector2 content = new Vector2(
            Mathf.Max(_layout.Size.X, minSize.X),
            Mathf.Max(_layout.Size.Y, minSize.Y));
        if (content.X <= 1f || content.Y <= 1f)
            return;

        float horizontalPadding = MobileOptimization.IsMobile() ? 16f : 40f;
        float verticalPadding = MobileOptimization.IsMobile() ? 16f : 56f;
        float maxWidth = Mathf.Max(1f, viewport.X - horizontalPadding);
        float maxHeight = Mathf.Max(1f, viewport.Y - verticalPadding);
        float scaleX = maxWidth / content.X;
        float scaleY = maxHeight / content.Y;
        float fit = Mathf.Clamp(Mathf.Min(1f, Mathf.Min(scaleX, scaleY)), 0.50f, 1f);

        _layout.PivotOffset = content * 0.5f;
        _layout.Scale = new Vector2(fit, fit);
    }

    private void OnOkayPressed()
    {
        if (!_canClose)
            return;
        CloseReveal();
    }

    private void CloseReveal()
    {
        SoundManager.Instance?.Play("ui_select");
        _canClose = false;
        Visible = false;
        EmitSignal(SignalName.RevealClosed);
    }

    private void ApplyAccent(Color accent)
    {
        _cardPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.07f, 0.08f, 0.17f, 0.96f),
            border: new Color(accent.R, accent.G, accent.B, 0.95f),
            corners: 12,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 9 : 12,
            padV: MobileOptimization.IsMobile() ? 8 : 10));

        _graphicPanel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.06f, 0.08f, 0.16f, 0.96f),
            border: new Color(accent.R, accent.G, accent.B, 0.92f),
            corners: 999,
            borderWidth: 2,
            padH: MobileOptimization.IsMobile() ? 12 : 16,
            padV: MobileOptimization.IsMobile() ? 12 : 16));
    }

    private void SetGraphic(Control graphic)
    {
        while (_graphicHolder.GetChildCount() > 0)
        {
            var child = _graphicHolder.GetChild(0);
            _graphicHolder.RemoveChild(child);
            child.QueueFree();
        }
        _graphicHolder.AddChild(graphic);
    }

    private static Control BuildTowerGraphic(string towerId)
    {
        float size = MobileOptimization.IsMobile() ? 76f : 92f;
        return new TowerIcon
        {
            TowerId = towerId,
            CustomMinimumSize = new Vector2(size, size),
            Size = new Vector2(size, size),
            Scale = Vector2.One * 1.05f
        };
    }

    private static Control BuildModifierGraphic(string modifierId, Color accent)
    {
        float size = MobileOptimization.IsMobile() ? 82f : 98f;
        return new ModifierIcon
        {
            ModifierId = modifierId,
            IconColor = accent,
            CustomMinimumSize = new Vector2(size, size),
            Size = new Vector2(size, size),
            Scale = Vector2.One
        };
    }

    private static Color GetTowerAccent(string towerId) => towerId switch
    {
        "rapid_shooter" => new Color(0.30f, 0.90f, 1.00f),
        "heavy_cannon" => new Color(1.00f, 0.55f, 0.00f),
        "marker_tower" => new Color(1.00f, 0.15f, 0.60f),
        "chain_tower" => new Color(0.50f, 0.85f, 1.00f),
        "rift_prism" => new Color(0.60f, 1.00f, 0.58f),
        _ => new Color(0.82f, 0.88f, 1.00f),
    };

    private static string GetTowerRevealDescription(string towerId) => towerId switch
    {
        "chain_tower" => "Built-in chain bounces for dense packs and lane control.",
        "rift_prism" => "Plants charged lane mines that detonate on final charge.",
        _ => "Tower unlocked."
    };
}
