using Godot;

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
		canvas.AddChild(scroll);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.OffsetLeft   = 120;
		vbox.OffsetRight  = -120;
		vbox.OffsetTop    = 40;
		vbox.OffsetBottom = -40;
		scroll.AddChild(vbox);

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
		AddRow(vbox, "▶  First",      "Enemy furthest along the path");
		AddRow(vbox, "★  Strongest",  "Enemy with the most current HP");
		AddRow(vbox, "▼  Lowest HP",  "Enemy closest to death");
		AddSpacer(vbox, 12);

		// ── Mark ─────────────────────────────────────────────────────────
		AddHeader(vbox, "MARK");
		AddLine(vbox, "Marker Tower hits apply Mark for 2 seconds.");
		AddLine(vbox, "Marked enemies take +30% damage from all towers.");
		AddLine(vbox, "Pair with Exploit Weakness for a ×1.95 burst combo (+30% mark × +50% exploit).");
		AddSpacer(vbox, 12);

		// ── Modifiers ────────────────────────────────────────────────────
		AddHeader(vbox, "MODIFIERS  (max 3 per tower)");
		AddModRow(vbox, "Momentum",         "+8% damage per consecutive hit on same target, up to ×5 stacks (×1.4 max). Resets on target switch.");
		AddModRow(vbox, "Overkill",         "Excess damage from a kill spills to the next enemy in the lane.");
		AddModRow(vbox, "Exploit Weakness", "+50% damage to Marked enemies. Pairs with Marker Tower.");
		AddModRow(vbox, "Focus Lens",       "+150% damage, ×2 attack interval. Big hits, slow fire — ideal for Overkill combos.");
		AddModRow(vbox, "Chill Shot",       "On hit: −30% move speed for 5 s. Keeps enemies in range longer.");
		AddModRow(vbox, "Overreach",        "+50% range, −15% damage. Wider coverage at a small cost — great on Marker Tower.");
		AddModRow(vbox, "Hair Trigger",     "+50% attack speed, −30% range. Pairs with Momentum and Chill Shot.");
		AddModRow(vbox, "Split Shot",       "On hit, fires 2 projectiles at nearby enemies for 65% damage each. Each additional copy fires one more projectile.");
		AddModRow(vbox, "Feedback Loop",    "Killing an enemy reduces this tower's current cooldown by 30%. Lets rapid killers fire again sooner.");
		AddModRow(vbox, "Chain Reaction",   "After each hit, the attack jumps to 1 nearby enemy for 60% damage. Each additional copy adds 1 more bounce.");
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
		AddLine(vbox, "Heavy Cannon + Split Shot — 65% of the cannon's big damage fires at 2–3 nearby enemies simultaneously.");
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
		backBtn.AddThemeFontSizeOverride("font_size", 22);
		backBtn.Pressed += () =>
		{
			if (OnBack != null) { OnBack(); QueueFree(); }
			else SlotTheory.Core.Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
		};
		vbox.AddChild(backBtn);

		AddSpacer(vbox, 40);
	}

	// ── Helpers ──────────────────────────────────────────────────────────

	private static void AddTitle(VBoxContainer vbox, string text)
	{
		var lbl = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
		SlotTheory.Core.UITheme.ApplyFont(lbl, semiBold: true, size: 52);
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
		lbl.AddThemeFontSizeOverride("font_size", 18);
		lbl.Modulate = new Color("#a6d608");
		vbox.AddChild(lbl);

		AddSpacer(vbox, 4);
	}

	private static void AddLine(VBoxContainer vbox, string text)
	{
		var lbl = new Label { Text = "  • " + text };
		lbl.AddThemeFontSizeOverride("font_size", 16);
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
		lblLeft.AddThemeFontSizeOverride("font_size", 16);
		lblLeft.Modulate = new Color(0.90f, 0.90f, 0.90f);
		lblLeft.CustomMinimumSize = new Vector2(300, 0);
		hbox.AddChild(lblLeft);

		var lblRight = new Label { Text = value };
		lblRight.AddThemeFontSizeOverride("font_size", 16);
		lblRight.Modulate = new Color(0.60f, 0.60f, 0.60f);
		lblRight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(lblRight);
	}

	private static void AddTowerRow(VBoxContainer vbox, string name, string stats, string desc)
	{
		var nameLbl = new Label { Text = "  " + name };
		nameLbl.AddThemeFontSizeOverride("font_size", 17);
		nameLbl.Modulate = new Color(0.95f, 0.95f, 0.95f);
		vbox.AddChild(nameLbl);

		var statsLbl = new Label { Text = "    " + stats };
		statsLbl.AddThemeFontSizeOverride("font_size", 15);
		statsLbl.Modulate = new Color(0.55f, 0.75f, 1.00f);
		vbox.AddChild(statsLbl);

		var descLbl = new Label { Text = "    " + desc };
		descLbl.AddThemeFontSizeOverride("font_size", 15);
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
		nameLbl.AddThemeFontSizeOverride("font_size", 16);
		nameLbl.Modulate = new Color(0.90f, 0.75f, 1.00f);
		nameLbl.CustomMinimumSize = new Vector2(220, 0);
		hbox.AddChild(nameLbl);

		var descLbl = new Label { Text = desc };
		descLbl.AddThemeFontSizeOverride("font_size", 16);
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
