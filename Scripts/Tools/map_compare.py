"""
map_compare.py — path length + avg tower coverage comparison across campaign maps.
Uses the same scoring logic as MapGenerator.ScoreCell (340px range, 30px sample spacing, bends ×5).
Run from project root: python Scripts/Tools/map_compare.py
"""

import math, json

RANGE        = 340.0
SAMPLE_STEP  = 30.0
BEND_WEIGHT  = 5

def path_length(pts):
    return sum(math.dist(pts[i], pts[i+1]) for i in range(len(pts)-1))

def sample_path(pts):
    """Return list of (point, is_bend) samples along the path."""
    samples = []
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i+1]
        seg_len = math.dist(a, b)
        steps = max(1, int(seg_len / SAMPLE_STEP))
        for s in range(steps + 1):
            t = s / steps
            p = (a[0] + (b[0]-a[0])*t, a[1] + (b[1]-a[1])*t)
            samples.append((p, False))
    # Mark bend waypoints (all except first and last)
    for wp in pts[1:-1]:
        samples.append((wp, True))
    return samples

def score_slot(slot, path_samples):
    score = 0
    for (p, is_bend) in path_samples:
        if math.dist(slot, p) <= RANGE:
            score += BEND_WEIGHT if is_bend else 1
    return score

def analyse_map(name, pts, slots):
    samples   = sample_path(pts)
    length    = path_length(pts)
    scores    = [score_slot(s, samples) for s in slots]
    avg_score = sum(scores) / len(scores)
    min_score = min(scores)
    max_score = max(scores)
    # Coverage fraction: what % of path samples are within range of AT LEAST ONE slot
    covered = sum(
        1 for (p, _) in samples
        if any(math.dist(p, s) <= RANGE for s in slots)
    )
    coverage_pct = 100.0 * covered / len(samples)
    return dict(name=name, length=length, avg_score=avg_score,
                min_score=min_score, max_score=max_score,
                coverage_pct=coverage_pct, scores=scores)

def load_maps():
    with open("Data/maps.json") as f:
        data = json.load(f)
    maps = {}
    for m in data["maps"]:
        pts   = [(p["x"], p["y"]) for p in m["path"]]
        slots = [(s["x"], s["y"]) for s in m["slots"]]
        maps[m["id"]] = (m["name"], pts, slots)
    return maps

def fmt(r):
    scores_str = "  ".join(f"{s:4d}" for s in r["scores"])
    print(f"\n  {r['name']}")
    print(f"    Path length   : {r['length']:6.0f} px")
    print(f"    Coverage      : {r['coverage_pct']:5.1f}% of path within ≥1 slot range")
    print(f"    Avg slot score: {r['avg_score']:6.1f}  (min {r['min_score']}  max {r['max_score']})")
    print(f"    Per-slot scores: {scores_str}")

if __name__ == "__main__":
    maps = load_maps()
    order = ["sprawl", "arena_classic", "gauntlet", "trench_line"]
    results = []
    for mid in order:
        if mid not in maps:
            print(f"  [skip] {mid} not found")
            continue
        name, pts, slots = maps[mid]
        results.append(analyse_map(name, pts, slots))

    print("\n=== Map Comparison ====================================━━")
    for r in results:
        fmt(r)

    print("\n=== Summary table =======================================")
    print(f"  {'Map':<22} {'Length':>8}  {'Coverage':>10}  {'AvgScore':>10}  {'MinScore':>9}")
    print(f"  {'-'*22} {'-'*8}  {'-'*10}  {'-'*10}  {'-'*9}")
    for r in results:
        print(f"  {r['name']:<22} {r['length']:8.0f}  {r['coverage_pct']:9.1f}%  {r['avg_score']:10.1f}  {r['min_score']:9d}")
