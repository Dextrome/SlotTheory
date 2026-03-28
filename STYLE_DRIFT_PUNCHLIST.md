# Style Drift Punch List (Step 3)

Last updated: 2026-03-28

Scoring:
- Impact: High / Medium / Low
- Effort: S (small), M (medium), L (large)
- Priority order favors high-impact, lower-effort work first.

## 1. Consolidate duplicated Settings/Pause value-button style logic

- Impact: High
- Effort: S
- Why: Settings and Pause each maintain nearly identical ON/OFF style state logic and local button style factories. This is already causing repeated baseline/padding edits.
- Files:
  - `Scripts/UI/Settings.cs` (`ApplyValueButtonStyle`, `MakeValBtn`)
  - `Scripts/UI/PauseScreen.cs` (`ApplyPauseValueButtonStyle`, `MakePauseValBtn`)
- Action:
  - Add `UITheme.ApplyToggleValueStyle(...)` and `UITheme.MakeToggleValueButtonStyle(...)`.
  - Replace both local implementations with one shared path.

## 2. Centralize stat-icon color map (currently duplicated 1:1)

- Impact: High
- Effort: S
- Why: Tooltip stat icon colors are duplicated in two places with identical values; future tweaks will drift.
- Files:
  - `Scripts/Core/GameController.cs` (`GetTooltipStatIconColor`)
  - `Scripts/UI/SlotCodexPanel.cs` (`GetStatIconColor`)
- Action:
  - Move map to `UIStyle` (or `UITheme`) as `GetStatIconColor(StatIconNode.IconType)`.
  - Remove duplicate switch blocks.

## 3. Unify hero/title typography path for menu screens

- Impact: Medium
- Effort: S
- Why: Main menu still uses a custom `LabelSettings` title path rather than shared title helper semantics.
- Files:
  - `Scripts/UI/MainMenu.cs` (`MakeTitleSettings`)
  - `Scripts/Core/UITheme.cs` (`ApplyAnagramTitle`)
- Action:
  - Migrate MainMenu title config to shared helper or shared tokenized title settings.

## 4. Replace remaining hex literals with semantic tokens

- Impact: Medium
- Effort: S
- Why: 28 hex literals remain across UI files, making palette updates harder and less consistent than token-based colors.
- Files (examples):
  - `Scripts/UI/AchievementsPanel.cs`
  - `Scripts/UI/AchievementToast.cs`
  - `Scripts/UI/CampaignSelectPanel.cs`
  - `Scripts/UI/MainMenu.cs`
  - `Scripts/UI/Settings.cs`
- Action:
  - Replace `new Color("#...")` with `UITheme`/`UIStyle` semantic colors where appropriate.

## 5. Resolve type-scale outliers (`17`, `24`) or canonize them explicitly

- Impact: Medium
- Effort: S
- Why: Canonical type scale in `UIStyle` does not currently include these sizes, but they are used in active UI.
- Files:
  - `Scripts/UI/MapEditorPanel.cs` (17)
  - `Scripts/UI/PauseScreen.cs` (17)
  - `Scripts/UI/TutorialCallout.cs` (17)
  - `Scripts/UI/MapSelectPanel.cs` (24)
  - `Scripts/UI/AchievementToast.cs` (24)
- Action:
  - Either migrate to nearest canonical sizes or add explicit tokens (`HeadingMd`, `DisplaySm`) to `UIStyle.TypeScale`.

## 6. Resolve spacing-token outliers (`1`, `3`, `5`, `7`) or canonize intentionally

- Impact: Medium
- Effort: S
- Why: Current spacing canon excludes several values actively used in layout separation.
- Files (examples):
  - `Scripts/UI/HudPanel.cs` (1)
  - `Scripts/UI/MapEditorPanel.cs` (3)
  - `Scripts/UI/PauseScreen.cs` (5, 3)
  - `Scripts/UI/LeaderboardsMenu.cs` (7)
  - `Scripts/UI/ModeSelectPanel.cs` (7)
- Action:
  - Decide whether to add micro-spacing tokens or normalize to existing scale.

## 7. Extract reusable button variants from MainMenu/Hud custom overrides

- Impact: High
- Effort: M
- Why: MainMenu and Hud are still heavy bespoke style islands (`~214 new Color` combined), making global style tuning expensive.
- Files:
  - `Scripts/UI/MainMenu.cs`
  - `Scripts/UI/HudPanel.cs`
- Action:
  - Add shared variants to `UITheme` for:
    - `PrimaryHeroButton`
    - `TopHudButton`
    - Optional shared animated finish layers
  - Keep only truly scene-specific draw polish local.

## 8. Normalize local stylebox factories to `UITheme` family

- Impact: Medium
- Effort: M
- Why: Several screens still define local stylebox factories with small parameter differences.
- Files (examples):
  - `Scripts/UI/LeaderboardsMenu.cs` (`MakeExpandButtonStyle`)
  - `Scripts/UI/DraftPanel.cs` (`MakeTutorialBannerStyle`, `BackBox`, `Box`)
  - `Scripts/UI/PauseScreen.cs` (`MakePauseRowStyle`)
  - `Scripts/UI/Settings.cs` (`MakeRowStyle`)
- Action:
  - Move reusable shape/padding variants into `UITheme`.
  - Keep only behavior-specific visuals local.

## 9. Align icon palette with canonical tower accents where intended

- Impact: Low
- Effort: M
- Why: `TowerIcon` uses a local accent map that is close but not always identical to canonical tower accents; this can create subtle brand hue drift.
- Files:
  - `Scripts/UI/TowerIcon.cs`
  - `Scripts/Core/UIStyle.cs`
- Action:
  - Drive icon base accents from `UIStyle.TowerAccent`.
  - Preserve dark secondary tone as icon-specific.

## 10. Build a lightweight style regression check script

- Impact: Medium
- Effort: M
- Why: This pass found drift quickly via ad hoc grep/regex. A committed script would keep future drift visible.
- Files:
  - New script under `Scripts/Tools/` or root `tools/`
- Action:
  - Report:
    - non-token hex colors
    - non-canonical font sizes/spacing values
    - duplicate switch maps for known style tokens

## Suggested Execution Order

1. Items 1, 2, 4
2. Items 5, 6
3. Items 7, 8
4. Items 9, 10

