#!/usr/bin/env python3
"""
screenshot_gallery.py
Regenerate gallery.html and manifest.json from an existing screenshots folder.

Usage:
    python screenshot_gallery.py [--dir E:/SlotTheory/screenshots] [--top N]

Options:
    --dir   Path to the screenshots folder (default: E:/SlotTheory/screenshots)
    --top   Keep only the top N candidates by score (default: all)
    --min-score  Minimum score to include (default: 0)
"""

import argparse
import json
import os
import sys
from datetime import datetime
from pathlib import Path


def load_metadata(folder: Path) -> list[dict]:
    entries = []
    for json_file in folder.glob("*.json"):
        if json_file.stem in ("manifest", "gallery"):
            continue
        try:
            with open(json_file, encoding="utf-8") as f:
                data = json.load(f)
            # Skip if the matching PNG is missing
            png = folder / data.get("file", "")
            if not png.exists():
                print(f"[SKIP] Missing PNG for {json_file.name}")
                continue
            entries.append(data)
        except Exception as e:
            print(f"[WARN] Could not read {json_file.name}: {e}")
    return entries


def build_gallery(entries: list[dict]) -> str:
    ts = datetime.now().strftime("%Y-%m-%d %H:%M")
    rows = []
    for m in entries:
        towers  = " ".join(f"<span class='tag'>{t}</span>" for t in m.get("towers", []))
        mods    = " ".join(f"<span class='tag mod'>{mod}</span>" for mod in m.get("modifiers", []))
        surge   = f"<span class='tag surge'>⚡ {m['surge_effect']}</span>" if m.get("surge_effect") else ""
        rows.append(f"""
<div class="card">
  <a href="{m['file']}" target="_blank"><img class="thumb" src="{m['file']}" loading="lazy" alt="{m.get('label','')}"></a>
  <div><span class="score">★ {m.get('score',0):.0f}</span>&nbsp;&nbsp;<span class="evnt">{m.get('event','')}</span></div>
  <div class="meta">Wave {m.get('wave','?')} · {m.get('enemy_count','?')} enemies · {m.get('lives','?')} lives · Run #{m.get('run_index','?')}</div>
  <div class="meta">{towers} {mods} {surge}</div>
  <div class="meta">{m.get('label','')}</div>
</div>""")

    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Slot Theory Screenshot Candidates</title>
<style>
* {{ box-sizing: border-box; }}
body {{ background:#0c0c12; color:#ccc; font-family:'Courier New',monospace; margin:0; padding:16px; }}
h1   {{ color:#a8e063; font-size:1.15em; margin:0 0 4px 0; }}
.sub {{ color:#555; font-size:.8em; margin:0 0 14px 0; }}
.grid{{ display:flex; flex-wrap:wrap; gap:10px; }}
.card{{ background:#17171f; border:1px solid #2c2c3a; padding:8px; width:300px;
        border-radius:4px; transition:border-color .15s; }}
.card:hover{{ border-color:#a8e063; }}
.thumb{{ width:100%; display:block; border-radius:2px; }}
.score{{ color:#a8e063; font-size:1.05em; font-weight:bold; }}
.evnt {{ color:#7ec8e3; }}
.meta {{ font-size:.75em; color:#666; margin-top:3px; }}
.tag  {{ background:#222230; border-radius:3px; padding:1px 5px;
         margin:1px; display:inline-block; font-size:.72em; }}
.surge{{ color:#ffcc44; }}
.mod  {{ color:#c5a3ff; }}
</style>
</head>
<body>
<h1>Slot Theory -- Screenshot Candidates</h1>
<p class='sub'>{len(entries)} candidates &nbsp;·&nbsp; generated {ts}</p>
<div class='grid'>
{''.join(rows)}
</div>
</body>
</html>"""


def main():
    parser = argparse.ArgumentParser(description="Regenerate screenshot gallery")
    parser.add_argument("--dir",       default="E:/SlotTheory/screenshots",
                        help="Screenshots folder")
    parser.add_argument("--top",       type=int, default=0,
                        help="Keep only top N by score (0 = all)")
    parser.add_argument("--min-score", type=float, default=0,
                        help="Minimum score to include")
    args = parser.parse_args()

    folder = Path(args.dir)
    if not folder.exists():
        print(f"Error: folder not found: {folder}")
        sys.exit(1)

    print(f"Scanning {folder} ...")
    entries = load_metadata(folder)
    print(f"Found {len(entries)} valid candidates")

    # Filter
    if args.min_score > 0:
        entries = [e for e in entries if e.get("score", 0) >= args.min_score]
        print(f"After min-score filter: {len(entries)}")

    # Sort by score descending
    entries.sort(key=lambda e: e.get("score", 0), reverse=True)

    # Cap
    if args.top > 0:
        entries = entries[:args.top]
        print(f"Keeping top {args.top}: {len(entries)}")

    # Write manifest
    manifest_path = folder / "manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(entries, f, indent=2)
    print(f"Manifest -> {manifest_path}")

    # Write gallery
    gallery_path = folder / "gallery.html"
    html = build_gallery(entries)
    with open(gallery_path, "w", encoding="utf-8") as f:
        f.write(html)
    print(f"Gallery  -> {gallery_path}")
    print(f"\nOpen in browser: file:///{gallery_path.as_posix()}")


if __name__ == "__main__":
    main()
