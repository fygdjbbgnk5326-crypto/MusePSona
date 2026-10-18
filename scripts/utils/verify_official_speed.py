# -*- coding: utf-8 -*-
"""Parse an official music_*.bundle and check global 0S speed vs per-note uid.speed."""
from __future__ import annotations

import json
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPT_DIR)
import official_to_mdm as ot  # noqa: E402
import csv_mmg_convert as conv  # noqa: E402


def approach_at(events, t):
    result = 1
    for start, rate, end in events:
        last = 1e18 if end is None else end
        if start <= t < last:
            result = rate
    return result


def markers_to_events(markers):
    events = []
    for i, (t, rate) in enumerate(markers):
        if rate == 1:
            continue
        end = markers[i + 1][0] if i + 1 < len(markers) else None
        events.append((t, rate, end))
    return events


def analyze_map(map_name, data):
    notes = data["notes"]
    play = [
        n for n in notes
        if n.get("bms_id") and not ot._is_event_note(n["type"], n["bms_id"])
    ]
    ev = [
        n for n in notes
        if n.get("bms_id") and ot._is_event_note(n["type"], n["bms_id"])
    ]
    hist = {}
    for n in play:
        hist[n["speed"]] = hist.get(n["speed"], 0) + 1
    trans = ot.speed_transitions(notes, bpm=float(data["bpm"] or 120))
    events = markers_to_events(trans)
    print("---", map_name, "bpm", data["bpm"], "raw", len(notes), "play", len(play), "ev", len(ev))
    print("play speed hist", dict(sorted(hist.items())))
    print("speed_transitions", len(trans))
    for t, r in trans:
        print(f"  t={t:.6f}s rate={r}")

    by_t = {}
    mixed_notes = []
    for n in play:
        key = round(n["tick"], 6)
        by_t.setdefault(key, set()).add(n["speed"])
    mixed = [(t, s) for t, s in sorted(by_t.items()) if len(s) > 1]
    print("same-tick mixed speeds", len(mixed))
    for row in mixed[:15]:
        print(" ", row)
        mixed_notes.extend(
            n for n in play if round(n["tick"], 6) == row[0]
        )

    mismatches = []
    for n in play:
        got = approach_at(events, n["tick"])
        if got != n["speed"]:
            mismatches.append(n | {"at": got})
    print("per-note vs ApproachSpeed.At mismatches", len(mismatches), "/", len(play))
    for n in mismatches[:25]:
        print(
            f"  t={n['tick']:.6f} official={n['speed']} at={n['at']} "
            f"bms={n['bms_id']} uid={n['note_uid']} air={int(n['air'])} {n.get('des','')[:48]}"
        )

    for sp in sorted(hist):
        xs = [n for n in play if n["speed"] == sp]
        print(
            f"speed {sp}: n={len(xs)} first={xs[0]['tick']:.6f} last={xs[-1]['tick']:.6f} "
            f"uid={xs[0]['note_uid']} {xs[0].get('des','')[:50]}"
        )

    # BMS tick snap: after write_bms, both notes and 0S share the same grid.
    bpm = float(data["bpm"] or 120)
    taps, holds, events_bms = ot.notes_to_taps_holds(notes, bpm=bpm)[:3]
    bms = conv.write_bms(
        {"bpm": bpm, "scene": data.get("scene") or "scene_01",
         "map_name": map_name, "composer": "x", "mapper": "x"},
        taps, holds, [{"time_sec": 0, "bpm": bpm}], "1", events_bms, speeds=trans,
    )
    os_lines = [ln for ln in bms.splitlines() if "0S:" in ln]
    print("bms 0S lines", len(os_lines))
    for ln in os_lines[:12]:
        print(" ", ln[:120])

    return {
        "map": map_name,
        "play": len(play),
        "mismatches": len(mismatches),
        "mixed": len(mixed),
        "trans": trans,
        "hist": hist,
    }


def main():
    bundle = sys.argv[1] if len(sys.argv) > 1 else ""
    if not bundle or not os.path.isfile(bundle):
        print("usage: python verify_official_speed.py <music_*.bundle>")
        return 2
    album = ot.load_album(bundle)
    print("meta", json.dumps(album["meta"], ensure_ascii=False, default=str)[:500])
    print("catalog", album["catalog"])
    print("maps", list(album["maps"]))
    summary = []
    for map_name, data in album["maps"].items():
        summary.append(analyze_map(map_name, data))
    print("=== SUMMARY ===")
    for s in summary:
        print(
            f"{s['map']}: play={s['play']} mismatches={s['mismatches']} "
            f"mixed_ticks={s['mixed']} trans={len(s['trans'])} hist={s['hist']}"
        )
    return 0 if all(s["mismatches"] == 0 for s in summary) else 1


if __name__ == "__main__":
    sys.exit(main())
