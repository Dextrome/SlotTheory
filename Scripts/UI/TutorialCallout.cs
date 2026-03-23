using Godot;
using System.Collections.Generic;

namespace SlotTheory.UI;

/// <summary>
/// Lightweight callout overlay used exclusively during tutorial runs.
/// Draft callouts require a tap to dismiss (full-screen blocker).
/// Auto-dismiss callouts (armored walker, surge) don't capture input so the
/// player can still interact with the game while they read.
/// </summary>
public partial class TutorialCallout : CanvasLayer
{
    private readonly record struct CalloutSpec(string Text, bool AnchorBottom, float AutoDismissSeconds);

    private readonly Queue<CalloutSpec> _queue = new();
    private Control? _panel;
    private bool _dismissed = false; // guards against double-dismiss (click + timer race)

    public override void _Ready()
    {
        Layer = 20;
        Visible = false;
    }

    /// <summary>Temporarily hide the callout while the pause menu is open without losing queued state.</summary>
    public void SetPaused(bool paused)
    {
        if (paused)
            Visible = false;
        else if (_panel != null && GodotObject.IsInstanceValid(_panel))
            Visible = true;
    }

    /// <summary>Queue a callout. anchorBottom=true places it near the bottom (for HUD references).</summary>
    public void Show(string text, bool anchorBottom = false, float autoDismissSeconds = 0f)
    {
        _queue.Enqueue(new CalloutSpec(text, anchorBottom, autoDismissSeconds));
        if (_panel == null)
            ShowNext();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            Visible = false;
            return;
        }

        Visible = true;
        _dismissed = false;
        var spec = _queue.Dequeue();
        BuildPanel(spec);
    }

    private void BuildPanel(CalloutSpec spec)
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.QueueFree();

        var vpSize = GetViewport().GetVisibleRect().Size;
        float panelW = Mathf.Min(560f, vpSize.X - 32f);
        float x = (vpSize.X - panelW) / 2f;
        float y = spec.AnchorBottom ? vpSize.Y - 150f : 24f;

        var container = new Control();
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(container);
        _panel = container;

        // Click-to-dismiss callouts (no auto-timer) use a full-screen blocker.
        // Auto-dismiss callouts use MouseIgnore so the game remains interactive.
        bool needsBlocker = spec.AutoDismissSeconds <= 0f;
        if (needsBlocker)
        {
            var blocker = new Control();
            blocker.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            blocker.MouseFilter = Control.MouseFilterEnum.Stop;
            blocker.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed)
                    DismissWithSound();
            };
            container.AddChild(blocker);
        }

        var panel = new PanelContainer
        {
            Position = new Vector2(x, y),
            CustomMinimumSize = new Vector2(panelW, 0),
            MouseFilter = needsBlocker
                ? Control.MouseFilterEnum.Stop
                : Control.MouseFilterEnum.Ignore,
        };
        if (needsBlocker)
        {
            panel.GuiInput += (@event) =>
            {
                if (@event is InputEventMouseButton mb && mb.Pressed)
                    DismissWithSound();
            };
        }
        panel.AddThemeStyleboxOverride("panel", Core.UITheme.MakePanel(
            bg: new Color(0.04f, 0.04f, 0.14f, 0.96f),
            border: new Color(0.20f, 0.95f, 1.00f, 0.80f),
            corners: 10, borderWidth: 2, padH: 18, padV: 12));

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 8);
        panel.AddChild(inner);

        var label = new Label
        {
            Text = spec.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.Modulate = new Color(0.90f, 0.97f, 1.00f);
        inner.AddChild(label);

        if (needsBlocker)
        {
            var hint = new Label
            {
                Text = "tap to continue",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            hint.AddThemeFontSizeOverride("font_size", 12);
            hint.Modulate = new Color(0.55f, 0.65f, 0.65f);
            inner.AddChild(hint);
        }

        container.AddChild(panel);

        panel.Modulate = new Color(1f, 1f, 1f, 0f);
        var tw = container.CreateTween();
        tw.TweenProperty(panel, "modulate:a", 1f, 0.20f)
          .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

        if (spec.AutoDismissSeconds > 0f)
            GetTree().CreateTimer(spec.AutoDismissSeconds).Timeout += DismissSilently;
    }

    /// <summary>Immediately clears all queued and active callouts (e.g. when a wave ends).</summary>
    public void DismissAll()
    {
        _queue.Clear();
        DismissSilently();
    }

    private void DismissWithSound()
    {
        if (_dismissed) return;
        _dismissed = true;
        Core.SoundManager.Instance?.Play("ui_select");
        DismissCore();
    }

    private void DismissSilently()
    {
        if (_dismissed) return;
        _dismissed = true;
        DismissCore();
    }

    private void DismissCore()
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.QueueFree();
        _panel = null;
        ShowNext();
    }
}
