# Enemy Render Perf Pass

Use this checklist for consistent Desktop vs Android profiling after visual/render changes.

## In-Game Capture (Dev Mode)

1. Enable `Dev Mode` in settings.
2. Start a run and let a wave stabilize with a representative enemy count.
3. Press `F9` to write a perf report.
4. Find the report in `user://perf_reports/` (printed in the console as `[PERF] Enemy render report saved: ...`).

## Suggested Test Matrix

- Desktop + layered rendering ON + bloom ON.
- Desktop + layered rendering ON + bloom OFF.
- Android + layered rendering ON + bloom ON.
- Android + layered rendering ON + bloom OFF.

Use the same map and similar wave/enemy density for all captures.

## What To Compare

- `Average frame ms`
- `P95 frame ms`
- `Worst frame ms`
- `Max bloom primitives/frame`
- `Max bloom budget rejects/frame`
- `Max bloom fallback calls/frame`

## Quick Acceptance Gate

- No sustained spikes above your target frame budget.
- Bloom rejects are low on Desktop and acceptable on Android.
- Visual readability remains acceptable when fallbacks activate.
