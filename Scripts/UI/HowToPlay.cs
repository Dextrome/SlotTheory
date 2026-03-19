using Godot;
using SlotTheory.Core;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen how-to-play reference. All UI built procedurally.
/// </summary>
public partial class HowToPlay : Node
{
    private enum HowToTab { Basics, Surges }

    private Button _basicsTabBtn = null!;
    private Button _surgesTabBtn = null!;
    private VBoxContainer _basicsSection = null!;
    private VBoxContainer _surgesSection = null!;

    private static readonly string[] CanonicalSurgeMods =
    {
        SpectacleDefinitions.Momentum,
        SpectacleDefinitions.Overkill,
        SpectacleDefinitions.ExploitWeakness,
        SpectacleDefinitions.FocusLens,
        SpectacleDefinitions.ChillShot,
        SpectacleDefinitions.Overreach,
        SpectacleDefinitions.HairTrigger,
        SpectacleDefinitions.SplitShot,
        SpectacleDefinitions.FeedbackLoop,
        SpectacleDefinitions.ChainReaction,
    };

    /// <summary>
    /// When set, the Back button calls this and frees the node instead of navigating to MainMenu.
    /// Used by PauseScreen to dismiss the overlay without leaving the game scene.
    /// </summary>
    public System.Action? OnBack { get; set; }

    /// <summary>
    /// When true, opens directly on the Surges tab instead of the default Basics tab.
    /// Set before adding to the scene tree.
    /// </summary>
    public bool StartOnSurgesTab { get; set; } = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // stay responsive when opened from paused PauseScreen
        var canvas = new CanvasLayer();
        // Set high layer when used as overlay from pause screen
        // (PauseScreen uses layer 8, so we need to be above it)
        canvas.Layer = 10;
        AddChild(canvas);

        // Background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color("#07071a");
        canvas.AddChild(bg);

        var grid = new NeonGridBg();
        grid.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        grid.MouseFilter = Control.MouseFilterEnum.Ignore;
        canvas.AddChild(grid);

        // Root layout - full rect VBox so the panel sits inside viewport margins
        var root = new VBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        canvas.AddChild(root);

        // 15% side margins so the neon grid peeks through on both sides
        float vpW = GetViewport().GetVisibleRect().Size.X;
        int sidePad = MobileOptimization.IsMobile() ? 8 : (int)(vpW * 0.15f);

        var panelMargin = new MarginContainer();
        panelMargin.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        panelMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panelMargin.AddThemeConstantOverride("margin_left",   sidePad);
        panelMargin.AddThemeConstantOverride("margin_right",  sidePad);
        panelMargin.AddThemeConstantOverride("margin_top",    12);
        panelMargin.AddThemeConstantOverride("margin_bottom", 12);
        root.AddChild(panelMargin);

        var scrollPanel = new PanelContainer();
        scrollPanel.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        scrollPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SlotTheory.Core.UITheme.ApplyGlassChassisPanel(
            scrollPanel,
            bg: new Color(0.05f, 0.04f, 0.13f, 0.96f),
            accent: new Color(0.42f, 0.78f, 0.94f, 0.92f),
            corners: 10, borderWidth: 2, padH: 0, padV: 0,
            sideEmitters: true, emitterIntensity: 0.78f);
        panelMargin.AddChild(scrollPanel);

        int innerH = MobileOptimization.IsMobile() ? 10 : 24;

        // Outer VBox inside the glass panel: frozen header + scrollable content + frozen footer
        var outerVBox = new VBoxContainer();
        outerVBox.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        outerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerVBox.AddThemeConstantOverride("separation", 0);
        scrollPanel.AddChild(outerVBox);

        // --- Frozen header: title + tab buttons ---
        var headerMargin = new MarginContainer();
        headerMargin.AddThemeConstantOverride("margin_left",   innerH);
        headerMargin.AddThemeConstantOverride("margin_right",  innerH);
        headerMargin.AddThemeConstantOverride("margin_top",    12);
        headerMargin.AddThemeConstantOverride("margin_bottom", 8);
        headerMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerVBox.AddChild(headerMargin);

        var headerVBox = new VBoxContainer();
        headerVBox.AddThemeConstantOverride("separation", 6);
        headerVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headerMargin.AddChild(headerVBox);

        AddTitle(headerVBox, "HOW TO PLAY");
        AddSpacer(headerVBox, 4);

        var tabRow = new HBoxContainer();
        tabRow.Alignment = BoxContainer.AlignmentMode.Center;
        tabRow.AddThemeConstantOverride("separation", 12);
        headerVBox.AddChild(tabRow);

        _basicsTabBtn = BuildTabButton("How To Play", () => SetActiveTab(HowToTab.Basics));
        _surgesTabBtn = BuildTabButton("Surges", () => SetActiveTab(HowToTab.Surges));
        tabRow.AddChild(_basicsTabBtn);
        tabRow.AddChild(_surgesTabBtn);

        var headerSep = new HSeparator();
        headerSep.Modulate = new Color(0.28f, 0.28f, 0.38f, 0.8f);
        outerVBox.AddChild(headerSep);

        // --- Scrollable content ---
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical    = Control.SizeFlags.ExpandFill;
        scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;
        scroll.Theme = SlotTheory.Core.UITheme.Build();
        scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        TouchScrollHelper.EnableDragScroll(scroll);
        outerVBox.AddChild(scroll);

        var marginContainer = new MarginContainer();
        marginContainer.AddThemeConstantOverride("margin_left",   innerH);
        marginContainer.AddThemeConstantOverride("margin_right",  innerH);
        marginContainer.AddThemeConstantOverride("margin_top",    10);
        marginContainer.AddThemeConstantOverride("margin_bottom", 10);
        marginContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        marginContainer.SizeFlagsVertical   = Control.SizeFlags.ExpandFill;
        marginContainer.MouseFilter = Control.MouseFilterEnum.Pass;
        scroll.AddChild(marginContainer);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        marginContainer.AddChild(vbox);

        _basicsSection = new VBoxContainer();
        _basicsSection.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(_basicsSection);
        BuildBasicsSection(_basicsSection);

        _surgesSection = new VBoxContainer();
        _surgesSection.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(_surgesSection);
        BuildSurgesSection(_surgesSection);

        SetActiveTab(StartOnSurgesTab ? HowToTab.Surges : HowToTab.Basics);

        var footerSep = new HSeparator();
        footerSep.Modulate = new Color(0.28f, 0.28f, 0.38f, 0.8f);
        outerVBox.AddChild(footerSep);

        // --- Frozen footer: back button ---
        var footerMargin = new MarginContainer();
        footerMargin.AddThemeConstantOverride("margin_left",   innerH);
        footerMargin.AddThemeConstantOverride("margin_right",  innerH);
        footerMargin.AddThemeConstantOverride("margin_top",    10);
        footerMargin.AddThemeConstantOverride("margin_bottom", 10);
        footerMargin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        outerVBox.AddChild(footerMargin);

        var footerHBox = new HBoxContainer();
        footerHBox.Alignment = BoxContainer.AlignmentMode.Center;
        footerMargin.AddChild(footerHBox);

        var backBtn = new Button
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(160, 48),
        };
        var backBtnSize = MobileOptimization.IsMobile() ? 18 : 22;
        backBtn.AddThemeFontSizeOverride("font_size", backBtnSize);
        UITheme.ApplyCyanStyle(backBtn);
        UITheme.ApplyMenuButtonFinish(backBtn, UITheme.Cyan, 0.10f, 0.12f);
        backBtn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        backBtn.Pressed += () =>
        {
            if (OnBack != null) { OnBack(); QueueFree(); }
            else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        };
        footerHBox.AddChild(backBtn);
    }

    public override void _Notification(int what)
    {
        if (what == 1007 /* NOTIFICATION_WM_GO_BACK_REQUEST */)
        {
            SlotTheory.Core.SoundManager.Instance?.Play("ui_select");
            if (OnBack != null) { OnBack(); QueueFree(); }
            else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (OnBack != null) { OnBack(); QueueFree(); }
            else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
            GetViewport().SetInputAsHandled();
        }
    }

    private static void BuildBasicsSection(VBoxContainer vbox)
    {
        AddHeader(vbox, "CORE LOOP");
        AddLine(vbox, "Before each wave, draft 1 card (5 options if slots are free; 4 if all slots are occupied).");
        AddLine(vbox, "Waves run automatically - you do not manually fire or place towers during waves.");
        AddLine(vbox, "Survive all 20 waves to win.");
        AddLine(vbox, "An enemy reaching the exit costs 1 life. You have 10 lives.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "CONTROLS");
        AddRow(vbox, "Pick a draft card", "Left-click the card");
        AddRow(vbox, "Assign tower / modifier", "Click target to preview, click same target again to confirm");
        AddRow(vbox, "Cycle targeting mode", "Left-click a tower during a wave");
        AddRow(vbox, "Pause / unpause", "Esc / Space / HUD Pause button");
        AddRow(vbox, "Speed", "Click speed button to cycle available game-speed steps");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "TOWERS");
        AddTowerRow(vbox, "rapid_shooter", "Rapid Shooter", "10 dmg, 0.45 s, 285 px range",
            "High rate of fire, low damage per hit. Shines with Momentum and Hair Trigger.");
        AddTowerRow(vbox, "heavy_cannon", "Heavy Cannon", "56 dmg, 2.0 s, 238 px range",
            "Slow but hits hard. Great with Overkill and Focus Lens.");
        AddTowerRow(vbox, "marker_tower", "Marker Tower", "7 dmg, 1.0 s, 333 px range",
            "Applies Mark on every hit. Synergises with Exploit Weakness.");
        AddTowerRow(vbox, "chain_tower", "Arc Emitter (unlock)", "18 dmg, 1.2 s, 257 px range",
            "Unlock by beating the first campaign map. Chains to 2 nearby enemies per shot (60% damage decay per bounce).");
        AddTowerRow(vbox, "rift_prism", "Rift Sapper (unlock)", "22 dmg, 0.98 s, 230 px range",
            "Unlock by beating the third campaign map. Plants up to 7 mines with 3 charges each; final charge causes the big pop. Wave start gets rapid seeding for 2.4s. Split Shot seeds mini-mines (35% scale) on final pops only.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "TARGETING MODES  (click a tower mid-wave to cycle)");
        AddLine(vbox, "The same icon badge appears beside each tower during combat.");
        AddTargetModeRow(vbox, TargetingMode.First, "First", "Enemy furthest along the path");
        AddTargetModeRow(vbox, TargetingMode.Strongest, "Strongest", "Enemy with the most current HP");
        AddTargetModeRow(vbox, TargetingMode.LowestHp, "Lowest HP", "Enemy closest to death");
        AddLine(vbox, "Rift Sapper uses its own targeting set (same cycle keybind):");
        AddTargetModeRow(vbox, TargetingMode.First, "Random", "Place mines at random valid lane points within range.", TargetModeIconSet.RiftSapper);
        AddTargetModeRow(vbox, TargetingMode.Strongest, "Closest", "Place mines at the closest valid lane point within range.", TargetModeIconSet.RiftSapper);
        AddTargetModeRow(vbox, TargetingMode.LowestHp, "Furthest", "Place mines at the furthest valid lane point within range.", TargetModeIconSet.RiftSapper);
        AddSpacer(vbox, 8);

        AddHeader(vbox, "MODIFIERS  (max 3 per tower)");
        AddModRowWithIcon(vbox, "momentum",         "Momentum",            "+16% damage per consecutive hit on same target, up to x1.8. Resets on target switch.");
        AddModRowWithIcon(vbox, "overkill",         "Overkill",            "Excess damage from a kill spills to the next enemy in the lane.");
        AddModRowWithIcon(vbox, "exploit_weakness", "Exploit Weakness",    "+45% damage to Marked enemies. Pairs with Marker Tower.");
        AddModRowWithIcon(vbox, "focus_lens",       "Focus Lens",          "+140% damage, +85% attack interval. Big hits, slow fire - ideal for Overkill combos.");
        AddModRowWithIcon(vbox, "slow",             "Chill Shot",          "On hit: -30% move speed for 6 s. Keeps enemies in range longer.");
        AddModRowWithIcon(vbox, "overreach",        "Overreach",           "+45% range, -10% damage. Wider coverage with a light damage tradeoff.");
        AddModRowWithIcon(vbox, "hair_trigger",     "Hair Trigger",        "+30% attack speed, -18% range. Pairs with Momentum and Chill Shot.");
        AddModRowWithIcon(vbox, "split_shot",       "Split Shot (unlock)", "Unlock by beating the second campaign map. On hit, fires 2 projectiles at nearby enemies for 35% damage each. Each additional copy fires one more projectile.");
        AddModRowWithIcon(vbox, "feedback_loop",    "Feedback Loop",       "Killing an enemy instantly resets this tower's cooldown to zero. Fire again immediately after each kill.");
        AddModRowWithIcon(vbox, "chain_reaction",   "Chain Reaction",      "After each hit, the attack jumps to 1 nearby enemy for 60% damage. Each additional copy adds 1 more bounce. Rift mine chains trigger on final pops.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "ENEMIES");
        AddLine(vbox, $"Basic Walker: {Balance.BaseEnemyHp:0} HP on wave 1, x{Balance.HpGrowthPerWave:0.00} per wave. Speed: {Balance.BaseEnemySpeed:0} px/s. Leaks cost 1 life.");
        AddLine(vbox, $"Armored Walker: {Balance.TankyHpMultiplier:0.#}x HP, half speed ({Balance.TankyEnemySpeed:0} px/s). Leaks cost 2 lives. First appears wave 6.");
        AddLine(vbox, $"Swift Walker: {Balance.SwiftHpMultiplier:0.#}x HP, double speed ({Balance.SwiftEnemySpeed:0} px/s). Leaks cost 1 life. Appears in mid-game surge waves.");
        AddLine(vbox, $"Splitter: {Balance.SplitterHpMultiplier:0.#}x HP, {Balance.SplitterSpeed:0} px/s. Splits into {Balance.SplitterShardCount} fast shards on death. Leaking one risks 3 lives total. Waves 9–15.");
        AddLine(vbox, "Enemy count scales with map and difficulty; late waves can exceed 40 total enemies on harder settings.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "TIPS");
        AddLine(vbox, "Rapid Shooter + Momentum - devastating DPS on enemies that take many hits to kill.");
        AddLine(vbox, "Marker Tower + Exploit Weakness - marks the target then bursts it for x2.03 total damage.");
        AddLine(vbox, "Heavy Cannon + Overkill - chain-kills tightly packed groups; spill damage carries forward.");
        AddLine(vbox, "Arc Emitter + Chain Reaction - each copy adds a bounce; with 3 copies, Arc Emitter can hit 6 targets per shot.");
        AddLine(vbox, "Set your Marker Tower to First so it tags the lead enemy before damage towers fire.");
    }

    private static void BuildSurgesSection(VBoxContainer vbox)
    {
        AddHeader(vbox, "SURGES OVERVIEW");
        AddLine(vbox, "Each tower builds spectacle charge from supported modifier events.");
        AddLine(vbox, $"Tower surge triggers at {SpectacleDefinitions.SurgeThreshold:0} meter, then resets to {SpectacleDefinitions.SurgeMeterAfterTrigger:0}.");
        AddLine(vbox, $"Each tower surge adds +{SpectacleDefinitions.GlobalMeterPerSurge:0} to the global meter. Global surge triggers at {SpectacleDefinitions.GlobalThreshold:0}, then resets to {SpectacleDefinitions.GlobalMeterAfterTrigger:0}.");
        AddLine(vbox, "A tower with 1 surge-capable mod fires a Single Surge. Two mods: Combo Surge. Three mods: Triad (Combo payload + Augment bonus).");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "SINGLE SURGE TYPES (10)");
        foreach (string modId in CanonicalSurgeMods)
        {
            var single = SpectacleDefinitions.GetSingle(modId);
            string modName = SpectacleDefinitions.GetDisplayName(modId);
            AddModRowWithIcon(vbox, modId, single.Name.ToUpperInvariant(), $"{modName}: {DescribeSingleEffect(modId)}");
        }
        AddSpacer(vbox, 8);

        AddHeader(vbox, "COMBO SURGES");
        AddLine(vbox, "Towers with 2 surge-capable mods fire Combo Surges - a hybrid payload blending both mod roles. 45 unique combinations exist, one for every modifier pairing.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "TRIAD AUGMENT TYPES (10)");
        foreach (string modId in CanonicalSurgeMods)
        {
            var aug = SpectacleDefinitions.GetTriadAugment(modId);
            string modName = SpectacleDefinitions.GetDisplayName(modId);
            string duration = aug.DurationSec > 0f ? $"{aug.DurationSec:0.0}s" : "instant";
            AddModRowWithIcon(vbox, modId, aug.Name.ToUpperInvariant(), $"{modName}: {DescribeAugmentKind(aug.Kind)}. Coef {aug.Coefficient * 100f:0}% ({duration}).");
        }
        AddLine(vbox, "Every Triad Surge fires one Combo core payload plus one Augment effect.");
        AddSpacer(vbox, 8);

        AddHeader(vbox, "GLOBAL SURGE");
        AddLine(vbox, "When it triggers, every placed tower gets a cooldown refund and fires its own major surge payload.");
        AddLine(vbox, "All alive enemies are marked and slowed for a short window.");
        AddLine(vbox, "The banner names your build archetype based on which mods drove the most surges. Ripple colors reflect the top contributing mods (up to 3 colors for diverse builds).");
        AddSpacer(vbox, 10);

        AddHeader(vbox, "GLOBAL SURGE ARCHETYPES (10)");
        foreach (var entry in SurgeDifferentiation.GlobalSurgeTable)
        {
            string modName = SpectacleDefinitions.GetDisplayName(entry.ModId);
            string feelTag = entry.Feel switch
            {
                SurgeDifferentiation.GlobalSurgeFeel.Detonation => "Detonation",
                SurgeDifferentiation.GlobalSurgeFeel.Pressure   => "Pressure",
                _                                               => "Neutral",
            };
            Color feelColor = entry.Feel switch
            {
                SurgeDifferentiation.GlobalSurgeFeel.Detonation => new Color(1.00f, 0.58f, 0.15f),
                SurgeDifferentiation.GlobalSurgeFeel.Pressure   => new Color(0.35f, 0.68f, 1.00f),
                _                                               => new Color(0.55f, 0.55f, 0.65f),
            };
            AddArchetypeRow(vbox, entry.ModId, entry.Label, $"Driven by {modName}  ·  {feelTag}", feelColor);
        }
    }

    private static string DescribeSingleEffect(string modId) => SpectacleDefinitions.NormalizeModId(modId) switch
    {
        "momentum" => "Ramped burst damage from sustained same-target pressure.",
        "overkill" => "Spill-focused burst that overflows into nearby enemies.",
        "exploit_weakness" => "Marked-target execution burst.",
        "focus_lens" => "Focused beam/lance burst for heavy single-target pressure.",
        "slow" => "Cryo wave that slows packs and adds chip damage.",
        "overreach" => "Long-range sweep that pressures distant enemies.",
        "hair_trigger" => "Overclock burst with high hit tempo.",
        "split_shot" => "Fractal spread burst across nearby enemies.",
        "feedback_loop" => "Reboot burst with strong cooldown tempo value.",
        "chain_reaction" => "Arc overload bouncing through linked targets.",
        _ => "Modifier-specific primary surge payload.",
    };

    private static string DescribeAugmentKind(SpectacleAugmentKind kind) => kind switch
    {
        SpectacleAugmentKind.RampCap => "Raises momentum ramp cap",
        SpectacleAugmentKind.SpillTransfer => "Boosts spill transfer",
        SpectacleAugmentKind.MarkedVulnerability => "Increases marked vulnerability",
        SpectacleAugmentKind.BeamBurst => "Strengthens beam burst",
        SpectacleAugmentKind.SlowIntensity => "Deepens slow intensity",
        SpectacleAugmentKind.RangePulse => "Adds range pulse coverage",
        SpectacleAugmentKind.AttackSpeed => "Adds temporary attack-speed overclock",
        SpectacleAugmentKind.SplitVolley => "Amplifies split-volley burst",
        SpectacleAugmentKind.CooldownRefund => "Increases cooldown reclaim",
        SpectacleAugmentKind.ChainBounces => "Adds chain-bounce pressure",
        _ => "Triad augment package",
    };

    // Helpers
    private static Button BuildTabButton(string text, System.Action onPressed)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(180f, 42f),
        };
        btn.AddThemeFontSizeOverride("font_size", MobileOptimization.IsMobile() ? 15 : 18);
        UITheme.ApplyCyanStyle(btn);
        UITheme.ApplyMenuButtonFinish(btn, UITheme.Cyan, 0.09f, 0.11f);
        btn.MouseEntered += () => SoundManager.Instance?.Play("ui_hover");
        btn.Pressed += () =>
        {
            SoundManager.Instance?.Play("ui_select");
            onPressed();
        };
        return btn;
    }

    private void SetActiveTab(HowToTab tab)
    {
        bool basics = tab == HowToTab.Basics;
        _basicsSection.Visible = basics;
        _surgesSection.Visible = !basics;
        ApplyTabButtonState(_basicsTabBtn, basics);
        ApplyTabButtonState(_surgesTabBtn, !basics);
    }

    private static void ApplyTabButtonState(Button btn, bool active)
    {
        btn.Modulate = active
            ? new Color(0.96f, 0.98f, 1.00f, 1f)
            : new Color(0.72f, 0.76f, 0.84f, 0.85f);
    }

    private static void AddTitle(VBoxContainer vbox, string text)
    {
        var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        var titleSize = MobileOptimization.IsMobile() ? 36 : 52;
        SlotTheory.Core.UITheme.ApplyFont(lbl, semiBold: true, size: titleSize);
        lbl.Modulate = new Color("#a6d608");
        vbox.AddChild(lbl);
    }

    private static void AddHeader(VBoxContainer vbox, string text)
    {
        var sep = new HSeparator();
        sep.Modulate = new Color(0.35f, 0.35f, 0.35f);
        vbox.AddChild(sep);
        AddSpacer(vbox, 3);

        var lbl = new Label { Text = text };
        var headerSize = MobileOptimization.IsMobile() ? 14 : 18;
        lbl.AddThemeFontSizeOverride("font_size", headerSize);
        lbl.Modulate = new Color("#a6d608");
        vbox.AddChild(lbl);

        AddSpacer(vbox, 2);
    }

    private static void AddLine(VBoxContainer vbox, string text)
    {
        var lbl = new Label { Text = "  - " + text };
        var lineSize = MobileOptimization.IsMobile() ? 14 : 16;
        lbl.AddThemeFontSizeOverride("font_size", lineSize);
        lbl.Modulate = new Color(0.82f, 0.82f, 0.82f);
        lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(lbl);
    }

    private static void AddRow(VBoxContainer vbox, string label, string value)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);
        vbox.AddChild(hbox);

        var lblLeft = new Label { Text = "  " + label };
        var rowSize = MobileOptimization.IsMobile() ? 14 : 16;
        var minWidth = MobileOptimization.IsMobile() ? 200 : 300;
        lblLeft.AddThemeFontSizeOverride("font_size", rowSize);
        lblLeft.Modulate = new Color(0.90f, 0.90f, 0.90f);
        lblLeft.CustomMinimumSize = new Vector2(minWidth, 0);
        hbox.AddChild(lblLeft);

        var lblRight = new Label { Text = value };
        lblRight.AddThemeFontSizeOverride("font_size", rowSize);
        lblRight.Modulate = new Color(0.60f, 0.60f, 0.60f);
        lblRight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lblRight.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(lblRight);
    }

    private static void AddTargetModeRow(
        VBoxContainer vbox,
        TargetingMode mode,
        string label,
        string value,
        TargetModeIconSet iconSet = TargetModeIconSet.Default)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(hbox);

        var indent = new Control { CustomMinimumSize = new Vector2(8f, 0f) };
        hbox.AddChild(indent);

        var badge = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.09f, 0.86f),
            CustomMinimumSize = new Vector2(20f, 20f),
            Size = new Vector2(20f, 20f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(badge);

        var border = new Line2D
        {
            Points = new[]
            {
                new Vector2(0f, 0f), new Vector2(20f, 0f), new Vector2(20f, 20f),
                new Vector2(0f, 20f), new Vector2(0f, 0f)
            },
            Width = 1.5f,
            DefaultColor = new Color(0.68f, 0.94f, 1.00f, 0.90f),
            Antialiased = true,
        };
        badge.AddChild(border);

        var icon = new TargetModeIcon
        {
            Mode = mode,
            IconSet = iconSet,
            Position = new Vector2(3f, 3f),
            Size = new Vector2(14f, 14f),
            CustomMinimumSize = new Vector2(14f, 14f),
            IconColor = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        badge.AddChild(icon);

        var rowSize = MobileOptimization.IsMobile() ? 14 : 16;

        var lblLeft = new Label { Text = label };
        lblLeft.AddThemeFontSizeOverride("font_size", rowSize);
        lblLeft.Modulate = new Color(0.90f, 0.90f, 0.90f);
        lblLeft.CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 86f : 110f, 0f);
        hbox.AddChild(lblLeft);

        var lblRight = new Label { Text = value };
        lblRight.AddThemeFontSizeOverride("font_size", rowSize);
        lblRight.Modulate = new Color(0.60f, 0.60f, 0.60f);
        lblRight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lblRight.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(lblRight);
    }

    private static void AddTowerRow(VBoxContainer vbox, string towerId, string name, string stats, string desc)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(hbox);

        float iconSize = MobileOptimization.IsMobile() ? 30f : 36f;
        var icon = new TowerIcon
        {
            TowerId = towerId,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(icon);

        var textBox = new VBoxContainer();
        textBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(textBox);

        var nameLbl = new Label { Text = name };
        var nameSize = MobileOptimization.IsMobile() ? 15 : 17;
        nameLbl.AddThemeFontSizeOverride("font_size", nameSize);
        nameLbl.Modulate = new Color(0.95f, 0.95f, 0.95f);
        textBox.AddChild(nameLbl);

        var statsLbl = new Label { Text = stats };
        var detailSize = MobileOptimization.IsMobile() ? 13 : 15;
        statsLbl.AddThemeFontSizeOverride("font_size", detailSize);
        statsLbl.Modulate = new Color(0.55f, 0.75f, 1.00f);
        textBox.AddChild(statsLbl);

        var descLbl = new Label { Text = desc };
        descLbl.AddThemeFontSizeOverride("font_size", detailSize);
        descLbl.Modulate = new Color(0.60f, 0.60f, 0.60f);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        textBox.AddChild(descLbl);

        AddSpacer(vbox, 6);
    }

    private static void AddModRow(VBoxContainer vbox, string name, string desc)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);
        vbox.AddChild(hbox);

        var nameLbl = new Label { Text = "  " + name };
        var modSize = MobileOptimization.IsMobile() ? 14 : 16;
        var modNameWidth = MobileOptimization.IsMobile() ? 260 : 390;
        nameLbl.AddThemeFontSizeOverride("font_size", modSize);
        nameLbl.Modulate = new Color(0.90f, 0.75f, 1.00f);
        nameLbl.CustomMinimumSize = new Vector2(modNameWidth, 0);
        nameLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(nameLbl);

        var descLbl = new Label { Text = desc };
        descLbl.AddThemeFontSizeOverride("font_size", modSize);
        descLbl.Modulate = new Color(0.65f, 0.65f, 0.65f);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(descLbl);
    }

    private static void AddModRowWithIcon(VBoxContainer vbox, string modId, string name, string desc)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(hbox);

        float iconSize = MobileOptimization.IsMobile() ? 18f : 20f;
        var accent = ModifierVisuals.GetAccent(modId);
        var icon = new ModifierIcon
        {
            ModifierId = modId,
            IconColor = accent,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(icon);

        var modSize = MobileOptimization.IsMobile() ? 14 : 16;
        var modNameWidth = MobileOptimization.IsMobile() ? 240 : 360;
        var nameLbl = new Label { Text = name };
        nameLbl.AddThemeFontSizeOverride("font_size", modSize);
        nameLbl.Modulate = accent;
        nameLbl.CustomMinimumSize = new Vector2(modNameWidth, 0);
        nameLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(nameLbl);

        var descLbl = new Label { Text = desc };
        descLbl.AddThemeFontSizeOverride("font_size", modSize);
        descLbl.Modulate = new Color(0.65f, 0.65f, 0.65f);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(descLbl);
    }

    private static void AddComboModRow(VBoxContainer vbox, string modIdA, string modIdB, string name, string desc)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 3);
        vbox.AddChild(hbox);

        float iconSize = MobileOptimization.IsMobile() ? 14f : 16f;
        foreach (string mid in new[] { modIdA, modIdB })
        {
            var accent = ModifierVisuals.GetAccent(mid);
            var icon = new ModifierIcon
            {
                ModifierId = mid,
                IconColor = accent,
                CustomMinimumSize = new Vector2(iconSize, iconSize),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            hbox.AddChild(icon);
        }

        var modSize = MobileOptimization.IsMobile() ? 14 : 16;
        var modNameWidth = MobileOptimization.IsMobile() ? 220 : 330;
        var nameLbl = new Label { Text = name };
        nameLbl.AddThemeFontSizeOverride("font_size", modSize);
        nameLbl.Modulate = new Color(0.90f, 0.75f, 1.00f);
        nameLbl.CustomMinimumSize = new Vector2(modNameWidth, 0);
        nameLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(nameLbl);

        var descLbl = new Label { Text = desc };
        descLbl.AddThemeFontSizeOverride("font_size", modSize);
        descLbl.Modulate = new Color(0.65f, 0.65f, 0.65f);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(descLbl);
    }

    private static void AddArchetypeRow(VBoxContainer vbox, string modId, string name, string desc, Color feelColor)
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 5);
        vbox.AddChild(hbox);

        // Feel color bar
        var bar = new ColorRect
        {
            Color = feelColor,
            CustomMinimumSize = new Vector2(3f, MobileOptimization.IsMobile() ? 18f : 20f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(bar);

        float iconSize = MobileOptimization.IsMobile() ? 18f : 20f;
        var accent = ModifierVisuals.GetAccent(modId);
        var icon = new ModifierIcon
        {
            ModifierId = modId,
            IconColor = accent,
            CustomMinimumSize = new Vector2(iconSize, iconSize),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hbox.AddChild(icon);

        var modSize = MobileOptimization.IsMobile() ? 14 : 16;
        var modNameWidth = MobileOptimization.IsMobile() ? 220 : 340;
        var nameLbl = new Label { Text = name };
        nameLbl.AddThemeFontSizeOverride("font_size", modSize);
        nameLbl.Modulate = new Color(0.96f, 0.92f, 1.00f);
        nameLbl.CustomMinimumSize = new Vector2(modNameWidth, 0);
        nameLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hbox.AddChild(nameLbl);

        var descLbl = new Label { Text = desc };
        descLbl.AddThemeFontSizeOverride("font_size", modSize);
        descLbl.Modulate = new Color(0.65f, 0.65f, 0.65f);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        descLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(descLbl);
    }

    private static void AddSpacer(VBoxContainer vbox, int px)
    {
        var s = new Control { CustomMinimumSize = new Vector2(0, px) };
        vbox.AddChild(s);
    }
}
