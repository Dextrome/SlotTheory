# Steam Leaderboards Setup

Create these 6 leaderboards in Steamworks (App Admin -> Stats and Achievements -> Leaderboards):

1. `global_arena_classic_normal`
2. `global_arena_classic_hard`
3. `global_gauntlet_normal`
4. `global_gauntlet_hard`
5. `global_sprawl_normal`
6. `global_sprawl_hard`

Use these settings for each board:
- Sort method: `Descending`
- Display type: `Numeric`
- Writes: client writes (`KeepBest` behavior is used in code)

Notes:
- `random_map` is intentionally excluded from global boards.
- Local highscores still track `random_map`.
