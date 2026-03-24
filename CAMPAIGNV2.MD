# Campaign Mode - The Fracture Circuit

---

## Identity

The world did not end in fire.
It calcified into systems.

What remains is a dead machine-world still trying to run old instructions through broken veins of circuitry, collapsed transit lines, and weapon grids that no longer know who they were built to protect. Every battlefield is part of the same rotting network. Every victory pushes deeper into the machine. Every stage wakes something meaner.

Campaign mode is a linear descent through four linked combat zones, each one forcing the player to adapt to a harsher battlefield protocol. What begins as standard deployment slowly becomes a hostile override: the system starts restricting the draft, constraining the player's board, and finally hardening the enemy itself.

This is not just a sequence of maps.
It is the world noticing you.

Progress persists across sessions. Clearing a stage unlocks the next sector and grants the same content unlocks already tied to those maps. Difficulty does not gate progress - any difficulty clears a stage - but each difficulty is marked separately so players can return to perfect the run. The campaign stays readable and mechanically clean while feeling like a deliberate escalation in pressure and tone.

---

## Stage Ladder

### Stage 1 - Orbit Breach

| | |
|---|---|
| **Map** | Orbit (`orbit`) |
| **Mandate** | None |
| **Unlock on clear** | Arc Emitter |
| **Stage subtitle** | Outer defense ring |
| **Intro line** | `"Outer grid compromised. Response latency acceptable."` |
| **Clear stamp** | `SECTOR BREACHED` |

The outer shell. The first fracture.

This is the only part of the campaign where the world still pretends to play fair. Orbit Breach serves as the entry point: standard rules, no restrictions, just enough breathing room to establish rhythm and confidence. Narratively, this is the incursion point - the first successful breach of the old defense lattice. Mechanically, it gives the player a clean baseline before the campaign starts tightening the screws.

**Tone:** controlled, exploratory, ominous
**Function:** onboarding that doesn't feel like a tutorial

---

### Stage 2 - Crossroads Interdiction

| | |
|---|---|
| **Map** | Crossroads (`crossroads`) |
| **Mandate** | **Draft Ban** - Momentum and Exploit Weakness removed from pool |
| **Unlock on clear** | Split Shot |
| **Stage subtitle** | Adaptive response sector |
| **Intro line** | `"Adaptive filtering active. Draft channels restricted."` |
| **Clear stamp** | `INTERDICTION BROKEN` |

The system has now observed your methods and begun to react. Patterns that worked before are no longer available.

This is where the campaign first shows its teeth. Instead of making enemies stronger, it attacks the player's assumptions. By stripping out a pair of familiar draft options, the campaign sends a clear message: adaptation is part of the mode's identity. The machine is not just spawning resistance - it is learning which tools you lean on and cutting them away.

**Tone:** tactical disruption, first "this mode is doing something" moment
**Function:** reframes the campaign from map sequence to active resistance

---

### Stage 3 - Pinch & Bleed

| | |
|---|---|
| **Map** | Pinch & Bleed (`pinch_bleed`) |
| **Mandate** | **Slot Lockout** - only 5 tower slots available |
| **Unlock on clear** | Rift Sapper |
| **Stage subtitle** | Constricted deployment lane |
| **Intro line** | `"Spatial permissions revoked. Deployment capacity reduced."` |
| **Clear stamp** | `CAPACITY OVERRIDE SURVIVED` |

Now the battlefield itself starts closing around you.

By this point the campaign shifts from "less choice in the draft" to "less room to breathe at all." The reduced slot count creates a sharper, more claustrophobic decision space. Every placement matters more. Every tower has to justify its existence. It should feel like the campaign is forcing the player to become cleaner, harsher, and more efficient.

This is where the mode stops being a tour and starts becoming a test.

**Tone:** pressure, scarcity, compression
**Function:** strongest gameplay identity spike in the campaign

---

### Stage 4 - Iron Mandate

| | |
|---|---|
| **Map** | Ridgeback (`ridgeback`) |
| **Mandate** | **Heavy Burden** - all enemies have +25% HP |
| **Unlock on clear** | None - capstone |
| **Stage subtitle** | Core reinforcement spine |
| **Intro line** | `"Final sector awake. Resistance parameters lifted."` |
| **Clear stamp** | `CORE MANDATE SHATTERED` |
| **Full-game gate** | Yes - requires full game (Ridgeback is already `isFullGame: true`) |

The core defense wakes up. No more subtlety. No more observation. Just force.

The final stage should feel less like "the next map" and more like the campaign entering open hostility. After two stages of indirect restriction, the machine stops sabotaging the player's options and simply makes the enemy line harder to kill. A final act should not be fussy. It should hit hard.

**Tone:** raw pressure, open hostility, no more games
**Function:** clean climax; the campaign's arc completes

---

### Campaign Arc in One Sentence Per Stage

| Stage | What the machine does |
|-------|-----------------------|
| 1 | Does nothing. Watches. |
| 2 | Takes away your habits. |
| 3 | Takes away your space. |
| 4 | Stops being subtle. |

---

## Narrative Framing

A small amount of world text earns disproportionate atmosphere. No cutscenes, no dialogue trees, no lore sludge.

**Campaign description (menu/select panel):**
> Fight through a four-stage combat chain where each sector imposes a new battlefield mandate. What begins as a standard run becomes a hostile adaptation sequence: drafts are restricted, slots are locked, and enemy resistance escalates as you push deeper into the system. Clear stages to unlock new content, mark difficulty completions, and break the circuit at its core.

**Context panel header (shown above stage list in `CampaignSelectPanel`):**
> THE FRACTURE CIRCUIT

**After Stage 4 clear - final completion text:**
```
The circuit does not die.
It goes quiet.
For now.
```

**Stage intro delivery:** Cold machine-voice. One line. Present tense. No subject. Printed to screen before the first wave draft opens, then auto-dismissed.

---

## Implementation Plan

### What CAMPAIGN.MD Already Specifies

The full mechanical plan (data model, mandate types, `CampaignProgress`, `CampaignManager`, `DraftSystem` filtering, `WaveSystem` HP multiplier, `SlotInstance.IsLocked`, `RunState.ActiveMandate`, `GameController` boot wiring, `CampaignSelectPanel` structure, `MainMenu` button) is documented in `CAMPAIGN.MD`. All of that applies unchanged here.

This document extends that plan with the **narrative layer** - the data fields, UI moments, and display logic that give the campaign its voice.

---

### Narrative Fields Added to `Data/campaign_stages.json`

Each stage entry gains four new string fields. The campaign root gains one:

```json
{
  "stages": [
    {
      "stageIndex": 0,
      "mapId": "orbit",
      "stageName": "Orbit Breach",
      "stageSubtitle": "Outer defense ring",
      "introLine": "Outer grid compromised. Response latency acceptable.",
      "clearStamp": "SECTOR BREACHED",
      "mandate": {
        "type": "none",
        "displayText": "Standard rules. No restrictions."
      }
    },
    {
      "stageIndex": 1,
      "mapId": "crossroads",
      "stageName": "Crossroads Interdiction",
      "stageSubtitle": "Adaptive response sector",
      "introLine": "Adaptive filtering active. Draft channels restricted.",
      "clearStamp": "INTERDICTION BROKEN",
      "mandate": {
        "type": "banned_modifiers",
        "bannedIds": ["momentum", "exploit_weakness"],
        "displayText": "MANDATE: Momentum and Exploit Weakness are banned from all drafts."
      }
    },
    {
      "stageIndex": 2,
      "mapId": "pinch_bleed",
      "stageName": "Pinch & Bleed",
      "stageSubtitle": "Constricted deployment lane",
      "introLine": "Spatial permissions revoked. Deployment capacity reduced.",
      "clearStamp": "CAPACITY OVERRIDE SURVIVED",
      "mandate": {
        "type": "locked_slots",
        "lockedSlotCount": 1,
        "displayText": "MANDATE: Only 5 tower slots may be used."
      }
    },
    {
      "stageIndex": 3,
      "mapId": "ridgeback",
      "stageName": "Iron Mandate",
      "stageSubtitle": "Core reinforcement spine",
      "introLine": "Final sector awake. Resistance parameters lifted.",
      "clearStamp": "CORE MANDATE SHATTERED",
      "mandate": {
        "type": "enemy_hp_bonus",
        "enemyHpMultiplier": 1.25,
        "displayText": "MANDATE: All enemies have +25% HP."
      }
    }
  ],
  "finalCompletionText": "The circuit does not die.\nIt goes quiet.\nFor now."
}
```

---

### `Scripts/Data/Models.cs` - Extended `CampaignStageDef`

Update the record from CAMPAIGN.MD to include narrative fields:

```csharp
public record CampaignStageDef(
    int         StageIndex,
    string      MapId,
    MandateDef  Mandate,
    string      StageName        = "",
    string      StageSubtitle    = "",
    string      IntroLine        = "",
    string      ClearStamp       = ""
);
```

And add one field to the campaign root:

```csharp
public record CampaignDataRoot(
    CampaignStageDef[] Stages,
    string FinalCompletionText = ""
);
```

`DataLoader.GetFinalCompletionText()` returns the root-level string.

---

### `Scripts/Core/CampaignStageDefinition.cs` - Narrative Properties

Add to the existing class from CAMPAIGN.MD:

```csharp
public string StageName     { get; }
public string StageSubtitle { get; }
public string IntroLine     { get; }
public string ClearStamp    { get; }

// Constructor updated:
public CampaignStageDefinition(CampaignStageDef def)
{
    StageIndex   = def.StageIndex;
    MapId        = def.MapId;
    Mandate      = new MandateDefinition(def.Mandate);
    StageName    = def.StageName;
    StageSubtitle = def.StageSubtitle;
    IntroLine    = def.IntroLine;
    ClearStamp   = def.ClearStamp;
}
```

---

### New: Stage Intro Overlay

**Where it fires:** `GameController._Ready()`, after the map and run state are initialized, before the first draft opens.

**How it works:** A `CanvasLayer` (Layer=20, above everything) is built procedurally and shown for ~3.5 seconds, then fades out and calls the normal first-draft open callback.

#### Method on `GameController`:

```csharp
/// <summary>
/// Shows the stage intro overlay for a campaign run and invokes the callback
/// when dismissed. If not a campaign run, invokes the callback immediately.
/// </summary>
private void ShowStageIntroThenContinue(System.Action onDismiss)
{
    if (!CampaignManager.IsCampaignRun || CampaignManager.ActiveStage == null)
    {
        onDismiss();
        return;
    }

    var stage = CampaignManager.ActiveStage;
    var overlay = new CanvasLayer { Layer = 20 };
    AddChild(overlay);

    // Fullscreen dark fill
    var bg = new ColorRect();
    bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    bg.Color = new Color(0f, 0f, 0f, 0.92f);
    bg.MouseFilter = Control.MouseFilterEnum.Stop;
    overlay.AddChild(bg);

    var center = new CenterContainer();
    center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    overlay.AddChild(center);

    var vbox = new VBoxContainer();
    vbox.AddThemeConstantOverride("separation", 10);
    vbox.CustomMinimumSize = new Vector2(640f, 0f);
    center.AddChild(vbox);

    // Stage number (small, muted)
    var stageNum = new Label
    {
        Text = $"STAGE {stage.StageIndex + 1}",
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    UITheme.ApplyFont(stageNum, semiBold: true, size: 16);
    stageNum.Modulate = new Color(0.35f, 0.55f, 0.65f, 0.88f);
    vbox.AddChild(stageNum);

    // Stage name (large, white)
    var nameLabel = new Label
    {
        Text = stage.StageName.ToUpper(),
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    UITheme.ApplyFont(nameLabel, semiBold: true, size: 52);
    nameLabel.Modulate = new Color(0.93f, 0.97f, 1.00f);
    vbox.AddChild(nameLabel);

    // Stage subtitle (small, cyan-tinted)
    var subtitle = new Label
    {
        Text = stage.StageSubtitle.ToUpper(),
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    UITheme.ApplyFont(subtitle, semiBold: false, size: 16);
    subtitle.Modulate = new Color(0.40f, 0.78f, 0.92f, 0.80f);
    vbox.AddChild(subtitle);

    // Horizontal rule
    var rule = new ColorRect
    {
        Color = new Color(0.24f, 0.52f, 0.64f, 0.44f),
        CustomMinimumSize = new Vector2(0f, 1f),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    vbox.AddChild(rule);

    // Intro line (cold machine voice, monospaced feel)
    var introLabel = new Label
    {
        Text = stage.IntroLine,
        HorizontalAlignment = HorizontalAlignment.Center,
        AutowrapMode = TextServer.AutowrapMode.Off,
    };
    UITheme.ApplyFont(introLabel, semiBold: false, size: 18);
    introLabel.Modulate = new Color(0.70f, 0.85f, 0.70f, 0.90f);  // slightly green - machine readout
    vbox.AddChild(introLabel);

    // If mandate is active, show a one-line mandate warning
    if (stage.Mandate.IsActive)
    {
        var mandateLabel = new Label
        {
            Text = stage.Mandate.DisplayText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 0f),
        };
        UITheme.ApplyFont(mandateLabel, semiBold: true, size: 15);
        mandateLabel.Modulate = new Color(1.0f, 0.62f, 0.15f, 0.96f);  // orange mandate color
        vbox.AddChild(mandateLabel);
    }

    // Dismiss hint
    var hint = new Label
    {
        Text = "[click or wait]",
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    hint.AddThemeFontSizeOverride("font_size", 12);
    hint.Modulate = new Color(0.35f, 0.45f, 0.55f, 0.70f);
    vbox.AddChild(hint);

    // Fade-in the overlay
    overlay.Modulate = new Color(1f, 1f, 1f, 0f);
    var tw = overlay.CreateTween();
    tw.TweenProperty(overlay, "modulate:a", 1f, 0.40f).SetTrans(Tween.TransitionType.Sine);

    // Dismiss on click
    bg.GuiInput += (InputEvent ev) =>
    {
        if (ev is InputEventMouseButton mb && mb.Pressed)
            DismissIntroOverlay(overlay, onDismiss);
    };

    // Auto-dismiss after 3.8s
    GetTree().CreateTimer(3.8).Timeout += () =>
    {
        if (GodotObject.IsInstanceValid(overlay))
            DismissIntroOverlay(overlay, onDismiss);
    };
}

private void DismissIntroOverlay(CanvasLayer overlay, System.Action onDismiss)
{
    if (!GodotObject.IsInstanceValid(overlay)) return;
    // Guard against double-dismiss (timer fires after click)
    overlay.SetMeta("dismissed", true);
    var tw = overlay.CreateTween();
    tw.TweenProperty(overlay, "modulate:a", 0f, 0.35f).SetTrans(Tween.TransitionType.Sine);
    tw.TweenCallback(Callable.From(() =>
    {
        overlay.QueueFree();
        onDismiss();
    }));
}
```

#### Wiring into `GameController._Ready()`

Find the point where the first draft is triggered (currently governed by `DraftOpenDelaySeconds`). Wrap it:

```csharp
// Replace the existing deferred first-draft call with:
ShowStageIntroThenContinue(() =>
{
    // existing first draft open logic
    CallDeferred(nameof(OpenFirstDraft));
});
```

If the intro overlay is not needed (non-campaign run), `ShowStageIntroThenContinue` calls the callback immediately and nothing changes for free play.

---

### Extended: `CampaignSelectPanel`

The panel from CAMPAIGN.MD gains narrative context:

**Panel header:** "THE FRACTURE CIRCUIT" (replacing the generic "CAMPAIGN" label) with a subtitle: "Four sectors. One path forward."

**Each stage card** shows:
- Stage number badge (e.g. `01`, `02`)
- Stage name in large text (e.g. "Orbit Breach")
- Stage subtitle in small muted text (e.g. "Outer defense ring")
- Mandate badge (orange label with mandate display text; hidden if `type == none`)
- Lock/available/cleared state as before

The stage name and subtitle come from `CampaignStageDefinition.StageName` and `.StageSubtitle`.

**Selected stage panel (right column)** shows the intro line as flavor text:
```
"Outer grid compromised. Response latency acceptable."
```
Displayed below the difficulty selector in a small, green-tinted, italic-style label - suggesting a live system readout.

---

### Extended: `EndScreen` - Sector Stamp

Add a new public method to `EndScreen.cs`:

```csharp
/// <summary>
/// Shows a campaign sector stamp above the stats on a win screen.
/// isFinalStage: if true, also shows the final completion text below the stamp.
/// </summary>
public void SetCampaignStageStamp(string stampText, bool isFinalStage, string finalCompletionText)
{
    // Build a label between _subtitleLabel and _statsLabel in the vbox.
    // Since vbox is already constructed, use MoveChild to insert at the right position.
    var stamp = new Label
    {
        Text = stampText,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    UITheme.ApplyFont(stamp, semiBold: true, size: 28);
    // Gold for stages 1-3, bright lime for stage 4
    stamp.Modulate = isFinalStage
        ? new Color(UITheme.Lime.R, UITheme.Lime.G, UITheme.Lime.B, 1.0f)
        : new Color(1.0f, 0.85f, 0.25f, 0.96f);

    // Insert after _subtitleLabel (index 1 in the vbox)
    _root.GetChild(0)  // scroll
         // ... navigate to vbox and insert after subtitle
    vbox.AddChild(stamp);
    vbox.MoveChild(stamp, _subtitleLabel.GetIndex() + 1);

    if (isFinalStage && !string.IsNullOrEmpty(finalCompletionText))
    {
        var finalLabel = new Label
        {
            Text = finalCompletionText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        UITheme.ApplyFont(finalLabel, semiBold: false, size: 18);
        finalLabel.Modulate = new Color(0.55f, 0.72f, 0.88f, 0.88f);
        vbox.AddChild(finalLabel);
        vbox.MoveChild(finalLabel, stamp.GetIndex() + 1);
    }
}
```

**Note on implementation:** `EndScreen` builds its layout entirely in `_Ready()` using a private `vbox` reference. To keep the vbox accessible in `SetCampaignStageStamp`, promote the `vbox` local variable to a `private VBoxContainer _vbox` field (one-line change in `_Ready()`).

#### Calling it from `GameController`

Immediately after `_endScreen.ShowWin(...)` on a campaign run win:

```csharp
if (CampaignManager.IsCampaignRun && CampaignManager.ActiveStage != null)
{
    var stage = CampaignManager.ActiveStage;
    bool isFinal = stage.StageIndex == DataLoader.GetCampaignStages().Count - 1;
    string finalText = isFinal ? DataLoader.GetFinalCompletionText() : "";
    _endScreen.SetCampaignStageStamp(stage.ClearStamp, isFinal, finalText);

    // Record progress
    var difficulty = SettingsManager.Instance?.Difficulty ?? DifficultyMode.Normal;
    CampaignProgress.MarkCleared(stage.StageIndex, difficulty);
}
```

---

### New: Mode Select Screen

**Flow change:** The existing "Play" button on `MainMenu` currently navigates directly to `MapSelect.tscn`. With campaign added, pressing Play goes to a new intermediate screen where the player chooses their mode.

```
MainMenu → [Play] → ModeSelect.tscn
                        ├── [CAMPAIGN] → CampaignSelect.tscn
                        └── [SKIRMISH] → MapSelect.tscn  (unchanged)
```

The "Campaign" button that CAMPAIGN.MD planned to add directly to `MainMenu` is replaced entirely by this flow - the main menu stays uncluttered and the mode split lives in its own screen.

#### `Scripts/UI/ModeSelectPanel.cs`

New scene/script, same procedural pattern as everything else. Root node: `Node`, script attached, no `.tscn` children.

**Layout:**
```
[dark bg + NeonGridBg]
  CenterContainer
    VBoxContainer
      Label:  "SELECT MODE"        ← small header, muted
      HBoxContainer (the two cards)
        [Campaign card]   [Skirmish card]
      Button: "Back"               ← below cards, slim
```

**The two mode cards** are large, clickable `PanelContainer`s side by side. Each card contains:
- Mode name (large, semibold)
- One-line description (small, muted)
- Visual accent color: Campaign = lime, Skirmish = cyan

```
┌─────────────────────┐   ┌─────────────────────┐
│                     │   │                     │
│    CAMPAIGN         │   │    SKIRMISH         │
│                     │   │                     │
│  Four linked stages │   │  Pick any map,      │
│  with escalating    │   │  any difficulty,    │
│  battlefield rules. │   │  single run.        │
│                     │   │                     │
└─────────────────────┘   └─────────────────────┘
```

Card dimensions: `CustomMinimumSize = new Vector2(380, 240)` (desktop). Cards use `UITheme.ApplyGlassChassisPanel` with their respective accent colors. Hover state lifts each card with a subtle scale tween (scale 1.0 → 1.02, matching the existing button hover pattern).

**Key wiring:**

```csharp
public partial class ModeSelectPanel : Node
{
    public override void _Ready()
    {
        CampaignManager.ClearActiveStage();   // same guard as old MainMenu._Ready()
        CampaignProgress.Load();
        // ... build UI
    }

    private void OnCampaign()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/CampaignSelect.tscn");
    }

    private void OnSkirmish()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/MapSelect.tscn");
    }

    private void OnBack()
    {
        SoundManager.Instance?.Play("ui_select");
        Transition.Instance?.FadeToScene("res://Scenes/MainMenu.tscn");
    }
}
```

`_UnhandledInput` handles Esc → back, same as all other panels.

#### `Scenes/ModeSelect.tscn`

Minimal scene file, same pattern as `CampaignSelect.tscn`:

```
[gd_scene format=3 uid="uid://..."]
[node name="ModeSelect" type="Node"]
script = ExtResource("ModeSelectPanel.cs")
```

#### `Scripts/UI/MainMenu.cs` change

`OnPlay()` becomes a one-liner:

```csharp
private void OnPlay()
{
    SoundManager.Instance?.Play("ui_select");
    Transition.Instance?.FadeToScene("res://Scenes/ModeSelect.tscn");
}
```

The `CampaignManager.ClearActiveStage()` call that was planned for `MainMenu._Ready()` moves to `ModeSelectPanel._Ready()` - that's the right place for it since it's the entry point for both modes.

#### Back navigation from `CampaignSelectPanel`

The Back button in `CampaignSelectPanel` currently points to `MainMenu.tscn`. Update it to point to `ModeSelect.tscn` instead:

```csharp
private void OnBack()
{
    CampaignManager.ClearActiveStage();
    Transition.Instance?.FadeToScene("res://Scenes/ModeSelect.tscn");
}
```

Same update in `MapSelectPanel.OnBack()` - it should also return to `ModeSelect.tscn`, not `MainMenu.tscn`. This keeps Back always going one level up in the hierarchy rather than jumping all the way to the main menu.

---

### New Achievements

Add to `AchievementManager.All`:

```csharp
new("CAMPAIGN_CLEAR",      "The Circuit",      "Clear all four stages of The Fracture Circuit."),
new("CAMPAIGN_HARD_CLEAR", "Iron Mandate",     "Clear all four stages of The Fracture Circuit on Hard difficulty."),
```

Check in `GameController` win logic, after `CampaignProgress.MarkCleared`:

```csharp
// After MarkCleared:
bool allCleared    = Enumerable.Range(0, stageCount).All(i => CampaignProgress.IsClearedOnAny(i));
bool allHardCleared = Enumerable.Range(0, stageCount).All(i => CampaignProgress.IsCleared(i, DifficultyMode.Hard));
if (allCleared)     AchievementManager.Instance?.Unlock("CAMPAIGN_CLEAR");
if (allHardCleared) AchievementManager.Instance?.Unlock("CAMPAIGN_HARD_CLEAR");
```

---

## Implementation Sequence

Same ordered steps as CAMPAIGN.MD, with narrative work interleaved where it's cheapest:

**Step 1 - Data foundation**
- Add narrative fields to `CampaignStageDef` record (`StageName`, `StageSubtitle`, `IntroLine`, `ClearStamp`)
- Add `CampaignDataRoot` record with `FinalCompletionText`
- Write `Data/campaign_stages.json` with all four stages fully populated
- Update `DataLoader`: load narrative fields, expose `GetFinalCompletionText()`
- Unit test: all four stages deserialize with non-empty `StageName` and `ClearStamp`

**Step 2 - Core mandate plumbing** *(from CAMPAIGN.MD, unchanged)*
- `SlotInstance.IsLocked`
- `RunState.ActiveMandate`
- `MandateDefinition.cs`
- `CampaignStageDefinition.cs` (with narrative properties added)
- `DraftSystem` ID filtering
- `WaveSystem.GetScaledHp` multiplier param
- Unit tests for filtered draft pools

**Step 3 - Campaign state management** *(from CAMPAIGN.MD, unchanged)*
- `CampaignManager.cs`
- `CampaignProgress.cs`

**Step 4 - GameController integration**
- Apply mandate + lock slots on boot
- Render locked slot visual
- Wrap first-draft open in `ShowStageIntroThenContinue`
- On win: call `CampaignProgress.MarkCleared` + `EndScreen.SetCampaignStageStamp`
- Unlock `CAMPAIGN_CLEAR` / `CAMPAIGN_HARD_CLEAR` achievements on full completion
- Test: run campaign stage, verify intro shows, stamp appears on win screen

**Step 5 - HUD mandate strip** *(from CAMPAIGN.MD, unchanged)*
- Orange one-line reminder in `HudPanel` during campaign runs

**Step 6 - `EndScreen` sector stamp**
- Promote `vbox` to field in `EndScreen._Ready()`
- Implement `SetCampaignStageStamp`
- Test: win a campaign stage, verify stamp color and text, test final stage for completion text

**Step 7 - `CampaignSelectPanel` with narrative skin**
- Panel header: "THE FRACTURE CIRCUIT" + subtitle
- Stage cards: number badge + stage name + subtitle + mandate badge + intro line in right column
- Lock/clear state as CAMPAIGN.MD

**Step 8 - Mode select screen + navigation wiring**
- Create `Scripts/UI/ModeSelectPanel.cs` and `Scenes/ModeSelect.tscn`
- Change `MainMenu.OnPlay()` to navigate to `ModeSelect.tscn` (one-line change)
- `CampaignSelectPanel.OnBack()` → `ModeSelect.tscn`
- `MapSelectPanel.OnBack()` → `ModeSelect.tscn`
- `CampaignManager.ClearActiveStage()` + `CampaignProgress.Load()` in `ModeSelectPanel._Ready()`
- Test full nav loop: MainMenu → ModeSelect → Campaign → back to ModeSelect → Skirmish → back to ModeSelect → back to MainMenu

**Step 9 - Achievements**
- Add `CAMPAIGN_CLEAR` and `CAMPAIGN_HARD_CLEAR` to `AchievementManager.All`
- Wire check logic in `GameController`

---

## What Does NOT Change

Everything from the corresponding section in CAMPAIGN.MD applies here. To restate the critical one:

**Free play is completely unaffected.** `CampaignManager.ActiveStage` is null during any non-campaign run. The intro overlay, the stage stamp, and the mandate wiring all check `CampaignManager.IsCampaignRun` before doing anything. The existing run loop is untouched.
