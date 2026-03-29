using Godot;
using SlotTheory.Core;
using SlotTheory.Data;
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
        SpectacleDefinitions.BlastCore,
        SpectacleDefinitions.Wildfire,
        SpectacleDefinitions.Afterimage,
        SpectacleDefinitions.ReaperProtocol,
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
        DataLoader.LoadAll();
        ProcessMode = ProcessModeEnum.Always; // stay responsive when opened from paused PauseScreen
        var canvas = new CanvasLayer();
        // Set high layer when used as overlay from pause screen
        // (PauseScreen uses layer 8, so we need to be above it)
        canvas.Layer = 10;
        AddChild(canvas);

        // Background
        var bg = new ColorRect();
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = UITheme.BgDeep;
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
        tabRow.AddThemeConstantOverride("separation", 22);
        headerVBox.AddChild(tabRow);

        _basicsTabBtn = BuildTabButton("How to Play", () => SetActiveTab(HowToTab.Basics));
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

    public static void BuildBasicsSection(VBoxContainer vbox, bool includeReferenceSections = true)
    {
        var loopCard = AddSurgeCard(vbox, "CORE LOOP", new Color(0.52f, 0.90f, 1.00f, 0.92f));
        AddSurgeRuleRow(loopCard, "DRAFT", "Pick 1 card before each wave (5 if free slots exist, 4 if all occupied).", new Color(0.64f, 0.92f, 1.00f, 0.92f));
        AddSurgeRuleRow(loopCard, "WAVE", "Combat runs automatically. You do not manually fire during waves.", new Color(0.64f, 0.92f, 1.00f, 0.92f));
        AddSurgeRuleRow(loopCard, "WIN", "Survive all 20 waves.", new Color(0.92f, 0.86f, 0.46f, 0.92f));
        AddSurgeRuleRow(loopCard, "LIVES", BuildStartingLivesLine(), new Color(0.96f, 0.74f, 0.48f, 0.92f));
        AddSpacer(vbox, 10);

        var controlsCard = AddSurgeCard(vbox, "CONTROLS", new Color(0.48f, 0.88f, 1.00f, 0.92f));
        AddRow(controlsCard, "Pick a draft card", "Left-click the card");
        AddRow(controlsCard, "Assign tower / modifier", "Click target to preview, click same target again to confirm");
        AddRow(controlsCard, "Set targeting mode", "Left-click a tower, then pick an icon");
        AddRow(controlsCard, "Pause / unpause", "Esc / Space / HUD Pause button");
        AddRow(controlsCard, "Speed", "Click speed button to cycle available game-speed steps");
        AddSpacer(vbox, 10);

        if (includeReferenceSections)
        {
            var towersCard = AddSurgeCard(vbox, "TOWERS", new Color(0.68f, 0.86f, 0.20f, 0.92f));
            TowerDef chainTowerDef = DataLoader.GetTowerDef("chain_tower");
            AddTowerRow(towersCard, "rapid_shooter", "Rapid Shooter", GetTowerStatsLine("rapid_shooter"),
                "High rate of fire, low damage per hit. Shines with Momentum and Hair Trigger.");
            AddTowerRow(towersCard, "heavy_cannon", "Heavy Cannon", GetTowerStatsLine("heavy_cannon"),
                "Slow but hits hard. Great with Overkill and Focus Lens.");
            AddTowerRow(towersCard, "marker_tower", "Marker Tower", GetTowerStatsLine("marker_tower"),
                "Applies Mark on every hit. Synergises with Exploit Weakness.");
            AddTowerRow(towersCard, "chain_tower", "Arc Emitter (unlock)", GetTowerStatsLine("chain_tower"),
                $"Unlock by beating the first campaign map. Chains to {chainTowerDef.ChainCount} nearby enemies per shot ({chainTowerDef.ChainDamageDecay * 100f:0}% damage per bounce).");
            AddTowerRow(towersCard, "rift_prism", "Rift Sapper (unlock)", GetTowerStatsLine("rift_prism"),
                $"Unlock by beating the third campaign map. Plants up to {Balance.RiftMineMaxActivePerTower} mines with {Balance.RiftMineChargesPerMine} charges each; final charge causes the big pop. Wave start gets rapid seeding for {Balance.RiftMineBurstWindow:0.#}s. Split Shot seeds mini-mines ({Balance.RiftMineMiniDamageFactor * 100f:0}% scale) on final pops only.");
            AddTowerRow(towersCard, "phase_splitter", "Phase Splitter", GetTowerStatsLine("phase_splitter"),
                "Each attack hits both the first and last enemy in range at 65% damage each. Strong versus blockers/backline pressure, weaker into dense mid-pack clusters.");
            AddTowerRow(towersCard, "rocket_launcher", "Rocket Launcher", GetTowerStatsLine("rocket_launcher"),
                ProductCopy.RocketLauncherBlastCoreDescription);
            AddTowerRow(towersCard, "undertow_engine", "Undertow Engine", GetTowerStatsLine("undertow_engine"),
                ProductCopy.UndertowEngineBaseDescription + " It heavily slows the pulled target and can lightly re-clump nearby enemies.");
            AddTowerRow(towersCard, "latch_nest", "Latch Nest", GetTowerStatsLine("latch_nest"),
                ProductCopy.LatchNestBaseDescription + " Attach shots are primary hits; parasite bites are secondary hits that still trigger normal on-hit synergies.");
            AddSpacer(vbox, 10);
        }

        var targetingCard = AddSurgeCard(vbox, "TARGETING MODES", new Color(0.62f, 0.88f, 1.00f, 0.92f));
        AddSurgeCardLine(targetingCard, "Click a tower mid-wave, then choose a targeting icon.");
        AddTargetModeRow(targetingCard, TargetingMode.First, "First", "Enemy furthest along the path");
        AddTargetModeRow(targetingCard, TargetingMode.Strongest, "Strongest", "Enemy with the most current HP");
        AddTargetModeRow(targetingCard, TargetingMode.LowestHp, "Lowest HP", "Enemy closest to death");
        AddTargetModeRow(targetingCard, TargetingMode.Last, "Last", "Enemy least far along the path (trailing enemy)");
        AddSpacer(targetingCard, 4);
        AddSurgeCardLine(targetingCard, "Rift Sapper uses its own targeting set:");
        AddTargetModeRow(targetingCard, TargetingMode.First, "Random", "Place mines at random valid lane points within range.", TargetModeIconSet.RiftSapper);
        AddTargetModeRow(targetingCard, TargetingMode.Strongest, "Closest", "Place mines at the closest valid lane point within range.", TargetModeIconSet.RiftSapper);
        AddTargetModeRow(targetingCard, TargetingMode.LowestHp, "Furthest", "Place mines at the furthest valid lane point within range.", TargetModeIconSet.RiftSapper);
        AddSpacer(vbox, 10);

        if (includeReferenceSections)
        {
            var modifiersCard = AddSurgeCard(vbox, "MODIFIERS (MAX 3 PER TOWER)", new Color(0.82f, 0.84f, 0.98f, 0.92f));
            AddModRowWithIcon(modifiersCard, "momentum",         "Momentum",            "+16% damage per consecutive hit on same target, up to x1.8. Resets on target switch.");
            AddModRowWithIcon(modifiersCard, "overkill",         "Overkill",            "Excess damage from a kill spills to the next enemy in the lane.");
            AddModRowWithIcon(modifiersCard, "exploit_weakness", "Exploit Weakness",    "+45% damage to Marked enemies. Pairs with Marker Tower.");
            AddModRowWithIcon(modifiersCard, "focus_lens",       "Focus Lens",          "+140% damage, +85% attack interval. Big hits, slow fire - ideal for Overkill combos.");
            AddModRowWithIcon(modifiersCard, "slow",             "Chill Shot",          "On hit: -30% move speed for 6 s. Keeps enemies in range longer.");
            AddModRowWithIcon(modifiersCard, "overreach",        "Overreach",           "+45% range, -10% damage. Wider coverage with a light damage tradeoff.");
            AddModRowWithIcon(modifiersCard, "hair_trigger",     "Hair Trigger",        "+30% attack speed, -18% range. Pairs with Momentum and Chill Shot.");
            AddModRowWithIcon(modifiersCard, "split_shot",       "Split Shot (unlock)", $"Unlock by beating the second campaign map. On hit, fires 2 projectiles at nearby enemies for {Balance.SplitShotDamageRatio * 100f:0}% damage each. Each additional copy fires one more projectile.");
            AddModRowWithIcon(modifiersCard, "feedback_loop",    "Feedback Loop",       "Killing an enemy instantly resets this tower's cooldown to zero. Fire again immediately after each kill.");
            AddModRowWithIcon(modifiersCard, "chain_reaction",   "Chain Reaction",      $"After each hit, the attack jumps to 1 nearby enemy for {Balance.ChainReactionDamageDecay * 100f:0}% damage. Each additional copy adds 1 more bounce. Rift mine chains trigger on final pops.");
            if (!Balance.IsDemo)
                AddModRowWithIcon(modifiersCard, "afterimage", "Afterimage (full game)",
                    "Unlock by beating Perimeter Lock. Hits leave a ghost imprint that triggers one delayed weaker replay from that spot (single replay, not a lingering zone).");
            if (!Balance.IsDemo)
                AddModRowWithIcon(modifiersCard, "reaper_protocol", "Reaper Protocol (full game, wave 10+)",
                    $"Kill (primary hits only): the first {Balance.ReaperProtocolKillCap} kills each wave restore 1 life, up to your starting life total. Available from wave 10. Invaluable in Endless runs where lives become scarce.");
            AddSpacer(vbox, 10);
        }

        var evolutionCard = AddSurgeCard(vbox, "TOWER EVOLUTION VISUALS", new Color(0.62f, 0.90f, 0.98f, 0.92f));
        AddSurgeCardLine(evolutionCard, "Visual hierarchy is readability-first: count tier first, exact mod identity second.", new Color(0.90f, 0.97f, 1.00f), 15);
        AddSpacer(evolutionCard, 2);
        AddSurgeRuleRow(evolutionCard, "0 MODS", "Base tower silhouette only.", new Color(0.70f, 0.84f, 0.98f, 0.90f));
        AddSurgeRuleRow(evolutionCard, "1 MOD", "First equipped mod becomes the stable focal accent/theme.", new Color(0.95f, 0.62f, 0.18f, 0.95f));
        AddSurgeRuleRow(evolutionCard, "2 MODS", "Focal stays the same; shell steps up and one support accent appears.", new Color(0.44f, 0.94f, 0.86f, 0.92f));
        AddSurgeRuleRow(evolutionCard, "3 MODS", "Final shell form + restrained tertiary hint: fully loaded at a glance.", new Color(0.90f, 0.88f, 0.98f, 0.92f));
        AddSurgeCardLine(evolutionCard, "Rift Sapper mines inherit this tier/focal read from their parent tower.");
        AddSurgeCardLine(evolutionCard, "Slot pips/icons remain the exact source of truth for full loadout details.");
        AddSpacer(vbox, 10);

        if (includeReferenceSections)
        {
            var enemiesCard = AddSurgeCard(vbox, "ENEMIES", new Color(0.96f, 0.78f, 0.46f, 0.94f));
            AddSurgeCardLine(enemiesCard, $"Basic Walker: {Balance.BaseEnemyHp:0} HP on wave 1, x{Balance.HpGrowthPerWave:0.00} per wave. Speed: {Balance.BaseEnemySpeed:0} px/s. Leaks cost 1 life.");
            AddSurgeCardLine(enemiesCard, $"Armored Walker: {Balance.TankyHpMultiplier:0.#}x HP, half speed ({Balance.TankyEnemySpeed:0} px/s). Leaks cost 2 lives. First appears wave 6.");
            AddSurgeCardLine(enemiesCard, $"Swift Walker: {Balance.SwiftHpMultiplier:0.#}x HP, double speed ({Balance.SwiftEnemySpeed:0} px/s). Leaks cost 1 life. Appears in mid-game surge waves.");
            AddSurgeCardLine(enemiesCard, $"Reverse Walker (full game): {Balance.ReverseWalkerHpMultiplier:0.#}x HP, {Balance.ReverseWalkerSpeed:0} px/s. Single hit >= {Balance.ReverseWalkerTriggerDamageRatio * 100f:0}% max HP can rewind it a short distance. Cooldown-gated and capped per enemy. First appears wave 11.");
            AddSurgeCardLine(enemiesCard, $"Shield Drone (full game): {Balance.ShieldDroneHpMultiplier:0.#}x HP, {Balance.ShieldDroneSpeed:0} px/s. Projects {Balance.ShieldDroneProtectionReduction * 100f:0}% damage reduction to nearby allies within {Balance.ShieldDroneAuraRadius:0}px. Kill it first.");
            AddSurgeCardLine(enemiesCard, $"Splitter: {Balance.SplitterHpMultiplier:0.#}x HP, {Balance.SplitterSpeed:0} px/s. Splits into {Balance.SplitterShardCount} fast shards on death. Leaking one risks 3 lives total. Waves 9-15.");
            AddSurgeCardLine(enemiesCard, "Enemy count scales with map and difficulty; late waves can exceed 40 total enemies on harder settings.");
            AddSpacer(vbox, 10);
        }

        var tipsCard = AddSurgeCard(vbox, "BUILD TIPS", new Color(0.60f, 0.96f, 0.74f, 0.94f));
        AddSurgeChipLine(tipsCard, "Rapid + Momentum", "devastating DPS on enemies that take many hits to kill.", new Color(0.70f, 0.98f, 0.74f, 0.92f));
        AddSurgeChipLine(tipsCard, "Marker + Exploit", "marks the target then bursts it for x2.03 total damage.", new Color(0.66f, 0.96f, 0.92f, 0.92f));
        AddSurgeChipLine(tipsCard, "Heavy + Overkill", "chain-kills tightly packed groups; spill damage carries forward.", new Color(0.94f, 0.78f, 0.50f, 0.92f));
        AddSurgeChipLine(tipsCard, "Arc + Chain", "with 3 copies, Arc Emitter can hit 6 targets per shot.", new Color(0.66f, 0.92f, 1.00f, 0.92f));
        AddSurgeChipLine(tipsCard, "Targeting", "set Marker Tower to First so it tags the lead enemy before damage towers fire.", new Color(0.84f, 0.90f, 0.98f, 0.92f));
    }

    public static void BuildSurgesSection(VBoxContainer vbox)
    {
        var modelCard = AddSurgeCard(vbox, "SURGE MODEL", new Color(0.48f, 0.90f, 1.00f, 0.92f));
        AddSurgeCardLine(modelCard, "Fast read in combat: Surge / Twist / Bonus / Global Surge.", new Color(0.90f, 0.96f, 1.00f), 16);
        AddSpacer(modelCard, 2);
        AddSurgeRuleRow(modelCard, "1 MOD", "Core Surge from the primary mod.", new Color(0.95f, 0.62f, 0.18f, 0.95f));
        AddSurgeRuleRow(modelCard, "2 MODS", "Core Surge + one Twist from the second mod.", new Color(0.44f, 0.94f, 0.86f, 0.92f));
        AddSurgeRuleRow(modelCard, "3 MODS", "Core Surge + one Bonus from the third mod.", new Color(0.90f, 0.88f, 0.98f, 0.92f));
        AddSurgeRuleRow(modelCard, "GLOBAL", "Bar fills from tower surges. Click READY to trigger Global Surge.", new Color(0.88f, 0.96f, 0.56f, 0.92f));
        AddSpacer(vbox, 10);

        Control behaviorLayout = MobileOptimization.IsMobile()
            ? new VBoxContainer()
            : new HBoxContainer();
        if (behaviorLayout is BoxContainer behaviorBox)
            behaviorBox.AddThemeConstantOverride("separation", 20);
        behaviorLayout.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddChild(behaviorLayout);

        var twistCard = AddSurgeCardContainer(behaviorLayout, "TWIST (2nd MOD)", new Color(0.44f, 0.94f, 0.86f, 0.92f));
        AddSurgeCardLine(twistCard, "Adds the second mod's trait to your Surge.", new Color(0.88f, 0.96f, 0.98f), 15);
        AddSpacer(twistCard, 2);
        AddSurgeChipLine(twistCard, "Chill Shot", "adds slow/freeze pressure", new Color(0.48f, 0.90f, 1.00f, 0.90f));
        AddSurgeChipLine(twistCard, "Chain Reaction", "adds bounce chaining", new Color(0.54f, 0.96f, 0.92f, 0.90f));
        AddSurgeChipLine(twistCard, "Feedback Loop", "adds cooldown reset follow-up", new Color(0.70f, 0.96f, 0.72f, 0.90f));

        var bonusCard = AddSurgeCardContainer(behaviorLayout, "BONUS (3rd MOD)", new Color(0.86f, 0.84f, 0.98f, 0.92f));
        AddSurgeCardLine(bonusCard, "Exactly one bonus type is added:", new Color(0.90f, 0.93f, 1.00f), 15);
        AddSpacer(bonusCard, 2);
        AddSurgeChipLine(bonusCard, "Pulse", "hit all nearby enemies", new Color(0.92f, 0.78f, 0.46f, 0.92f));
        AddSurgeChipLine(bonusCard, "Strike", "one heavy single-target hit", new Color(0.96f, 0.70f, 0.42f, 0.92f));
        AddSurgeChipLine(bonusCard, "Recharge", "reset cooldown and fire again", new Color(0.74f, 0.96f, 0.66f, 0.92f));
        AddSpacer(vbox, 10);

        var tableCard = AddSurgeCard(vbox, "PRIMARY MOD -> CORE SURGE BEHAVIOR", new Color(0.69f, 0.86f, 0.16f, 0.92f));
        foreach (string modId in CanonicalSurgeMods)
        {
            if (modId == SpectacleDefinitions.ReaperProtocol && Balance.IsDemo) continue;
            if (modId == SpectacleDefinitions.Afterimage && Balance.IsDemo) continue;
            string modName = SpectacleDefinitions.GetDisplayName(modId);
            AddModRowWithIcon(tableCard, modId, modName.ToUpperInvariant(), DescribeSingleEffect(modId));
        }
        AddSpacer(vbox, 10);

        var globalCard = AddSurgeCard(vbox, "GLOBAL SURGE", new Color(0.95f, 0.86f, 0.36f, 0.94f));
        AddSurgeCardLine(globalCard, "When the bar is full, click READY to fire Global Surge.", new Color(0.96f, 0.95f, 0.88f), 15);
        AddSurgeCardLine(globalCard, "Global Surge activates all towers and marks + slows all living enemies.");
        AddSurgeCardLine(globalCard, "Display format: Global Surge: Momentum / Chain / Blast / ...");
        AddSurgeCardLine(globalCard, "To bias the label, stack more of that mod across towers.");
    }
    private static string DescribeSingleEffect(string modId) => SpectacleDefinitions.NormalizeModId(modId) switch
    {
        "momentum"         => "Burst hit scaled to current Momentum ramp -- longer streak = bigger surge.",
        "overkill"         => "Burst hit that overflows excess damage into the next enemy in line.",
        "exploit_weakness" => "Detonates all Marked enemies in range for bonus damage simultaneously.",
        "focus_lens"       => "One massive focused beam shot at the highest-HP target in range.",
        "slow"             => "Cryo shockwave: slows all enemies in range and deals chip damage to each.",
        "overreach"        => "Extended-range sweep hitting the farthest enemy in this tower's expanded coverage.",
        "hair_trigger"     => "Rapid-fire burst: fires extra shots at all enemies in range in quick succession.",
        "split_shot"       => "Scatter burst: all nearby enemies take split-shot hits at the same time.",
        "feedback_loop"    => "Burst hit, then instant cooldown reset -- tower fires again immediately.",
        "chain_reaction"   => "Arc burst that bounces from target to nearby targets through the group.",
        "blast_core"       => "Detonation at target position: area damage to all enemies in the blast radius.",
        "wildfire"         => "Flame burst across all enemies in range, leaving fire trails that slow and tick damage.",
        "afterimage"       => "Ghost imprint appears at hit position, then replays one delayed weaker echo from that spot.",
        "reaper_protocol"  => "Executes the lowest-HP enemy in range. Grants +1 life if it kills.",
        _                  => "Modifier-specific primary surge payload.",
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
        var lbl = new Label { Text = text };
        SlotTheory.Core.UITheme.ApplyAnagramTitle(lbl, MobileOptimization.IsMobile() ? 38 : 50);
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
        lbl.Modulate = UITheme.Lime;
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

    private static string GetTowerStatsLine(string towerId)
    {
        TowerDef def = DataLoader.GetTowerDef(towerId);
        return $"{def.BaseDamage:0.##} dmg, {def.AttackInterval:0.##} s, {def.Range:0} px range";
    }

    private static string BuildStartingLivesLine()
    {
        return $"Leaks cost life. Starting lives: Easy {Balance.GetStartingLives(DifficultyMode.Easy)} / Normal {Balance.GetStartingLives(DifficultyMode.Normal)} / Hard {Balance.GetStartingLives(DifficultyMode.Hard)}.";
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

    private static VBoxContainer AddSurgeCard(VBoxContainer parent, string title, Color accent)
        => AddSurgeCardContainer(parent, title, accent);

    private static VBoxContainer AddSurgeCardContainer(Node parent, string title, Color accent)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.06f, 0.07f, 0.16f, 0.96f),
            border: new Color(accent.R, accent.G, accent.B, 0.78f),
            corners: 10,
            borderWidth: 2,
            padH: 12,
            padV: 10));
        UITheme.AddTopAccent(panel, new Color(accent.R, accent.G, accent.B, 0.72f));
        parent.AddChild(panel);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 6);
        panel.AddChild(body);

        var titleLabel = new Label
        {
            Text = title,
        };
        UITheme.ApplyFont(titleLabel, semiBold: true, size: MobileOptimization.IsMobile() ? 15 : 17);
        titleLabel.Modulate = accent;
        body.AddChild(titleLabel);

        return body;
    }

    private static void AddSurgeCardLine(VBoxContainer body, string text, Color? color = null, int size = 14)
    {
        var lbl = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        UITheme.ApplyFont(lbl, size: MobileOptimization.IsMobile() ? size - 1 : size);
        lbl.Modulate = color ?? new Color(0.80f, 0.84f, 0.92f);
        body.AddChild(lbl);
    }

    private static void AddSurgeRuleRow(VBoxContainer body, string tag, string text, Color accent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        body.AddChild(row);

        var chip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 68f : 78f, 24f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        chip.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.10f, 0.12f, 0.24f, 0.96f),
            border: new Color(accent.R, accent.G, accent.B, 0.90f),
            corners: 7,
            borderWidth: 1,
            padH: 8,
            padV: 3));
        row.AddChild(chip);

        var chipLbl = new Label
        {
            Text = tag,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        UITheme.ApplyFont(chipLbl, semiBold: true, size: MobileOptimization.IsMobile() ? 11 : 12);
        chipLbl.Modulate = new Color(0.94f, 0.98f, 1.00f);
        chip.AddChild(chipLbl);

        var textLbl = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UITheme.ApplyFont(textLbl, size: MobileOptimization.IsMobile() ? 13 : 14);
        textLbl.Modulate = new Color(0.82f, 0.86f, 0.93f);
        row.AddChild(textLbl);
    }

    private static void AddSurgeChipLine(VBoxContainer body, string chipText, string description, Color chipColor)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        body.AddChild(row);

        var chip = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MobileOptimization.IsMobile() ? 86f : 102f, 24f),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        chip.AddThemeStyleboxOverride("panel", UITheme.MakePanel(
            bg: new Color(0.10f, 0.11f, 0.20f, 0.96f),
            border: new Color(chipColor.R, chipColor.G, chipColor.B, 0.92f),
            corners: 7,
            borderWidth: 1,
            padH: 8,
            padV: 3));
        row.AddChild(chip);

        var chipLbl = new Label
        {
            Text = chipText,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        UITheme.ApplyFont(chipLbl, semiBold: true, size: MobileOptimization.IsMobile() ? 12 : 13);
        chipLbl.Modulate = chipColor;
        chip.AddChild(chipLbl);

        var desc = new Label
        {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        UITheme.ApplyFont(desc, size: MobileOptimization.IsMobile() ? 13 : 14);
        desc.Modulate = new Color(0.78f, 0.83f, 0.90f);
        row.AddChild(desc);
    }

    private static void AddSpacer(VBoxContainer vbox, int px)
    {
        var s = new Control { CustomMinimumSize = new Vector2(0, px) };
        vbox.AddChild(s);
    }
}



