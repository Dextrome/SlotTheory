using Godot;
using SlotTheory.Entities;

namespace SlotTheory.UI;

/// <summary>
/// Full-screen how-to-play reference. All UI built procedurally.
/// </summary>
public partial class HowToPlay : Node
{
	/// <summary>
	/// When set, the Back button calls this and frees the node instead of navigating to MainMenu.
	/// Used by PauseScreen to dismiss the overlay without leaving the game scene.
	/// </summary>
	public System.Action? OnBack { get; set; }

	public override void _Ready()
	{
		var canvas = new CanvasLayer();
		// Set high layer when used as overlay from pause screen 
		// (PauseScreen uses layer 8, so we need to be above it)
		canvas.Layer = 10;
		AddChild(canvas);

		// Background
		var bg = new ColorRect();
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.Color = new Color("#141420");
		canvas.AddChild(bg);

		// Scroll area
		var scroll = new ScrollContainer();
		scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		scroll.Theme = SlotTheory.Core.UITheme.Build();
		// Enable vertical scrollbar and disable horizontal scrolling
		scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		canvas.AddChild(scroll);

		// Wrap VBox in MarginContainer for proper margins
		var marginContainer = new MarginContainer();
		// Use smaller margins on mobile to prevent off-screen content
		var leftMargin = MobileOptimization.IsMobile() ? 10 : 30;
		var rightMargin = MobileOptimization.IsMobile() ? 10 : 20;
		marginContainer.AddThemeConstantOverride("margin_left", leftMargin);
		marginContainer.AddThemeConstantOverride("margin_right", rightMargin);
		marginContainer.AddThemeConstantOverride("margin_top", 20);
		marginContainer.AddThemeConstantOverride("margin_bottom", 20);
		marginContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		marginContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.AddChild(marginContainer);

		// Simple VBox that can expand vertically
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// Don't set vertical size flags - let it size naturally to content
		marginContainer.AddChild(vbox);

		// ── Title ────────────────────────────────────────────────────────
		AddTitle(vbox, "HOW TO PLAY");
		AddSpacer(vbox, 16);

		// ── Core Loop ────────────────────────────────────────────────────
		AddHeader(vbox, "CORE LOOP");
		AddLine(vbox, "Before each wave, draft 1 card (5 options if slots are free; 4 if all slots are occupied).");
		AddLine(vbox, "Waves run automatically — no mid-wave interaction.");
		AddLine(vbox, "Survive all 20 waves to win.");
		AddLine(vbox, "An enemy reaching the exit costs 1 life. You have 10 lives.");
		AddSpacer(vbox, 12);

		// ── Controls ─────────────────────────────────────────────────────
		AddHeader(vbox, "CONTROLS");
		AddRow(vbox, "Pick a draft card",         "Left-click the card");
		AddRow(vbox, "Assign to a slot / tower",  "Left-click the target");
		AddRow(vbox, "Cycle targeting mode",       "Left-click a tower during a wave");
		AddRow(vbox, "Pause / unpause",            "Esc");
		AddRow(vbox, "Speed",                      "Click speed button to cycle  ×1 → ×2 → ×3");
		AddSpacer(vbox, 12);

		// ── Towers ───────────────────────────────────────────────────────
		AddHeader(vbox, "TOWERS");
		AddTowerRow(vbox, "Rapid Shooter", "10 dmg · 0.45 s · 285 px range",
			"High rate of fire, low damage per hit. Shines with Momentum and Hair Trigger.");
		AddTowerRow(vbox, "Heavy Cannon",  "52 dmg · 2.0 s · 238 px range",
			"Slow but hits hard. Great with Overkill and Focus Lens.");
		AddTowerRow(vbox, "Marker Tower",  " 7 dmg · 1.0 s · 333 px range",
			"Applies Mark on every hit. Synergises with Exploit Weakness.");
		AddTowerRow(vbox, "Arc Emitter",   "14 dmg · 1.2 s · 270 px range",
			"Chains to 2 nearby enemies per shot (60% damage decay per bounce). Excellent in dense clusters.");
		AddSpacer(vbox, 12);

		// ── Targeting ────────────────────────────────────────────────────
		AddHeader(vbox, "TARGETING MODES  (click a tower mid-wave to cycle)");
		AddLine(vbox, "The same icon badge appears beside each tower during combat.");
		AddTargetModeRow(vbox, TargetingMode.First, "First", "Enemy furthest along the path");
		AddTargetModeRow(vbox, TargetingMode.Strongest, "Strongest", "Enemy with the most current HP");
		AddTargetModeRow(vbox, TargetingMode.LowestHp, "Lowest HP", "Enemy closest to death");
		AddSpacer(vbox, 12);

		// ── Mark ─────────────────────────────────────────────────────────
		AddHeader(vbox, "MARK");
		AddLine(vbox, "Marker Tower hits apply Mark for 2 seconds.");
		AddLine(vbox, "Marked enemies take +40% damage from all towers.");
		AddLine(vbox, "Pair with Exploit Weakness for a ×2.1 burst combo (+40% mark × +50% exploit).");
		AddSpacer(vbox, 12);

		// ── Modifiers ────────────────────────────────────────────────────
		AddHeader(vbox, "MODIFIERS  (max 3 per tower)");
		AddModRow(vbox, "Momentum",         "+16% damage per consecutive hit on same target, up to ×1.80. Resets on target switch.");
		AddModRow(vbox, "Overkill",         "Excess damage from a kill spills to the next enemy in the lane.");
		AddModRow(vbox, "Exploit Weakness", "+60% damage to Marked enemies. Pairs with Marker Tower.");
		AddModRow(vbox, "Focus Lens",       "+125% damage, ×2 attack interval. Big hits, slow fire — ideal for Overkill combos.");
		AddModRow(vbox, "Chill Shot",       "On hit: −25% move speed for 5 s. Keeps enemies in range longer.");
		AddModRow(vbox, "Overreach",        "+40% range, −20% damage. Wider coverage at a small cost — great on Marker Tower.");
		AddModRow(vbox, "Hair Trigger",     "+40% attack speed, −18% range. Pairs with Momentum and Chill Shot.");
		AddModRow(vbox, "Split Shot",       "On hit, fires 2 projectiles at nearby enemies for 42% damage each. Each additional copy fires one more projectile.");
		AddModRow(vbox, "Feedback Loop",    "Killing an enemy reduces this tower's current cooldown by 25%. Lets rapid killers fire again sooner.");
		AddModRow(vbox, "Chain Reaction",   "After each hit, the attack jumps to 1 nearby enemy for 55% damage. Each additional copy adds 1 more bounce.");
		AddSpacer(vbox, 12);

		// ── Enemies ──────────────────────────────────────────────────────
		AddHeader(vbox, "ENEMIES");
		AddLine(vbox, "Basic Walker: 65 HP on wave 1, ×1.08 per wave (~280 HP by wave 20). Speed: 120 px/s. Leaks cost 1 life. Round teal body.");
		AddLine(vbox, "Armored Walker: 4× HP, half speed (60 px/s). Leaks cost 2 lives — priority target. First appears wave 7; up to 5 per wave by wave 20. Large hexagonal crimson body.");
		AddLine(vbox, "Swift Walker: 1.5× HP, double speed (240 px/s). Leaks cost 1 life. Appears waves 10–14 (2–4 per wave). Small lime-green diamond — fast and hard to catch.");
		AddLine(vbox, "Enemy count scales from 10 (wave 1) to 30 (wave 20). Spawn interval tightens each wave.");
		AddSpacer(vbox, 12);

		// ── Tips ─────────────────────────────────────────────────────────
		AddHeader(vbox, "TIPS");
		AddLine(vbox, "Rapid Shooter + Momentum — devastating DPS on enemies that take many hits to kill.");
		AddLine(vbox, "Marker Tower + Exploit Weakness — marks the target then bursts it for ×1.95 total damage.");
		AddLine(vbox, "Heavy Cannon + Overkill — chain-kills tightly packed groups; spill damage carries forward.");
		AddLine(vbox, "Arc Emitter + Chain Reaction — each copy adds a bounce; 3 copies hits 5 enemies per shot.");
		AddLine(vbox, "Heavy Cannon + Split Shot — even at 40%, cannon hits still add meaningful side pressure to nearby enemies.");
		AddLine(vbox, "Feedback Loop + Hair Trigger — killing enemies reduces cooldown faster; rapid shooters cycle almost instantly.");
		AddLine(vbox, "Set your Marker Tower to First so it tags the lead enemy before damage towers fire.");
		AddLine(vbox, "Hair Trigger + Chill Shot — rapid-fire slows stack to keep enemies frozen in range.");
		AddLine(vbox, "Swift Walkers appear waves 10–14 — Chill Shot or Overreach helps catch them before they outrun your range.");
		AddSpacer(vbox, 32);

		// ── Back button ──────────────────────────────────────────────────
		var backBtn = new Button
		{
			Text = "← Back",
			CustomMinimumSize = new Vector2(160, 48),
		};
		var backBtnSize = MobileOptimization.IsMobile() ? 18 : 22;
		backBtn.AddThemeFontSizeOverride("font_size", backBtnSize);
		backBtn.Pressed += () =>
		{
			if (OnBack != null) { OnBack(); QueueFree(); }
			else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
		};
		vbox.AddChild(backBtn);

		AddSpacer(vbox, 40);
		
		// Don't use MobileOptimization.ApplyUIScale for this screen
		// Instead handle mobile layout through responsive margins and font sizes
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Consume Escape key to prevent it from reaching pause screen underneath
		if (@event.IsActionPressed("ui_cancel"))
		{
			// Trigger back button behavior
			if (OnBack != null) { OnBack(); QueueFree(); }
			else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
			
			// Mark event as handled
			GetViewport().SetInputAsHandled();
		}
	}

	// ── Helpers ──────────────────────────────────────────────────────────

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
		AddSpacer(vbox, 6);

		var lbl = new Label { Text = text };
		var headerSize = MobileOptimization.IsMobile() ? 14 : 18;
		lbl.AddThemeFontSizeOverride("font_size", headerSize);
		lbl.Modulate = new Color("#a6d608");
		vbox.AddChild(lbl);

		AddSpacer(vbox, 4);
	}

	private static void AddLine(VBoxContainer vbox, string text)
	{
		var lbl = new Label { Text = "  • " + text };
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
		hbox.AddChild(lblRight);
	}

	private static void AddTargetModeRow(VBoxContainer vbox, TargetingMode mode, string label, string value)
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

	private static void AddTowerRow(VBoxContainer vbox, string name, string stats, string desc)
	{
		var nameLbl = new Label { Text = "  " + name };
		var nameSize = MobileOptimization.IsMobile() ? 15 : 17;
		nameLbl.AddThemeFontSizeOverride("font_size", nameSize);
		nameLbl.Modulate = new Color(0.95f, 0.95f, 0.95f);
		vbox.AddChild(nameLbl);

		var statsLbl = new Label { Text = "    " + stats };
		var detailSize = MobileOptimization.IsMobile() ? 13 : 15;
		statsLbl.AddThemeFontSizeOverride("font_size", detailSize);
		statsLbl.Modulate = new Color(0.55f, 0.75f, 1.00f);
		vbox.AddChild(statsLbl);

		var descLbl = new Label { Text = "    " + desc };
		descLbl.AddThemeFontSizeOverride("font_size", detailSize);
		descLbl.Modulate = new Color(0.60f, 0.60f, 0.60f);
		descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vbox.AddChild(descLbl);

		AddSpacer(vbox, 6);
	}

	private static void AddModRow(VBoxContainer vbox, string name, string desc)
	{
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 0);
		vbox.AddChild(hbox);

		var nameLbl = new Label { Text = "  " + name };
		var modSize = MobileOptimization.IsMobile() ? 14 : 16;
		var modNameWidth = MobileOptimization.IsMobile() ? 150 : 220;
		nameLbl.AddThemeFontSizeOverride("font_size", modSize);
		nameLbl.Modulate = new Color(0.90f, 0.75f, 1.00f);
		nameLbl.CustomMinimumSize = new Vector2(modNameWidth, 0);
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
