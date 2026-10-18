import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import _paths
p = Path(_paths.export_dir()) / "chrome_vox" / "chrome_vox_open" / "chart.json"
data = json.loads(p.read_text(encoding="utf-8"))
scripts = {"1A","1B","1C","1D","1E","1F","1G","1H","1L","1M"}
for mk, ch in data["charts"].items():
    notes = [n for n in ch["notes"] if n.get("kind") != "event" and n.get("bmsId") not in scripts]
    notes.sort(key=lambda n: (n["timeSec"], n["id"]))
    print("map", mk, "playable", len(notes))
    if len(notes) < 305:
        print("  no 305")
        continue
    n = notes[304]
    print(f"  note305 t={n['timeSec']:.3f} id={n['bmsId']}")
    # nearby boss events
    ev = [e for e in ch["notes"] if e.get("bmsId") in scripts or e.get("bmsId") in {"11","12","16","17"}]
    ev.sort(key=lambda e: e["timeSec"])
    t = n["timeSec"]
    prev = [e for e in ev if e["timeSec"] <= t][-6:]
    nxt = [e for e in ev if e["timeSec"] > t][:4]
    print("  prev", [(round(e["timeSec"],3), e["bmsId"]) for e in prev])
    print("  next", [(round(e["timeSec"],3), e["bmsId"]) for e in nxt])
