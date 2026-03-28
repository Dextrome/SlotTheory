# UI Style Canon

Last updated: 2026-03-28

This document defines the shared style source of truth for Slot Theory UI code.
Use this as the contract for future style cleanup and feature work.

## Source Of Truth

- Primary token file: `Scripts/Core/UIStyle.cs`
- Theme primitives and shared builders: `Scripts/Core/UITheme.cs`

## Approved Rules

### Typography

- Base families:
  - Rajdhani for UI body and controls.
  - Anagram for hero/title moments only.
- Canonical Rajdhani files:
  - Regular: `Assets/Fonts/Rajdhani-Regular.ttf`
  - SemiBold: `Assets/Fonts/Rajdhani-SemiBold.ttf`
  - Bold: `Assets/Fonts/Rajdhani-Bold.ttf`
- Approved type scale (from `UIStyle.TypeScale`):
  - `11, 12, 13, 14, 15, 16, 18, 20, 22, 28, 72`

### Spacing

- Approved spacing tokens (from `UIStyle.Spacing`):
  - `2, 4, 6, 8, 10, 12, 14, 16, 18, 24`
- New UI should use token values, not ad hoc numbers.

### Radius

- Approved corner radius tokens (from `UIStyle.Radius`):
  - `5, 6, 8, 10`

### Color Semantics

- Shared semantic menu palette remains in `UITheme` (`Lime`, `Cyan`, `Magenta`, `Bg*`, `Border*`).
- Shared tower accents now live in `UIStyle.TowerAccent(...)`:
  - `Ui` variant: card/codex/reveal/announce style surfaces.
  - `Projectile` variant: projectile VFX accents.
  - `Body` variant: world body/range accents.

## Deprecated Patterns

- Duplicating per-tower accent switch maps in feature files.
- Loading fonts directly in feature scripts for standard UI text.
- New standalone stylebox factories when `UITheme.MakeBtn` or `UITheme.MakePanel` already fits.
- Introducing new spacing/font-size literals that are not in the canonical scale.

## Migration Guidance

1. Route repeated color and size literals to `UIStyle` or `UITheme`.
2. Keep intentional one-off art/VFX draw code local, but reference canonical accents when possible.
3. Prefer additive wrappers over copy-pasted style forks.

