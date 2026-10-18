# -*- coding: utf-8 -*-
"""Parse official umpopoff and check ApproachSpeed vs per-note NoteConfigData.speed."""
from __future__ import annotations

import json
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPT_DIR)
import official_to_mdm as ot  # noqa: E402

BUNDLE = (
    r"d:\steam\steamapps\common\Muse Dash\MuseDash_Data\StreamingAssets"
    r"\aa\StandaloneWindows64\music_umpopoff_assets_all.bundle"
)


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


def main():
    album = ot.load_album(BUNDLE)
    print("meta", json.dumps(album["meta"], ensure_ascii=False, default=str)[:500])
    print("catalog", album["catalog"])
    print("maps", list(album["maps"]))

    for map_name, data in album["maps"].items():
        notes = data["notes"]
        play = [
            n
            for n in notes
            if n.get("bms_id") and not ot._is_event_note(n["type"], n["bms_id"])
        ]
        ev = [
            n
            for n in notes
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
        print("editor events (skip rate-1 starts)", events)

        by_t = {}
        for n in play:
            by_t.setdefault(round(n["tick"], 6), set()).add(n["speed"])
        mixed = [(t, s) for t, s in sorted(by_t.items()) if len(s) > 1]
        print("same-tick mixed speeds", len(mixed))
        for row in mixed[:20]:
            print(" ", row)

        mismatches = []
        for n in play:
            got = approach_at(events, n["tick"])
            if got != n["speed"]:
                mismatches.append(n | {"at": got})
        print("per-note vs ApproachSpeed.At mismatches", len(mismatches), "/", len(play))
        for n in mismatches[:30]:
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

        print("notes 110-116s:")
        for n in play:
            if 110 <= n["tick"] <= 116:
                print(
                    f"  t={n['tick']:.6f} len={n['length']:.3f} spd={n['speed']} "
                    f"type={n['type']} bms={n['bms_id']} air={int(n['air'])} "
                    f"uid={n['note_uid']} {n.get('des','')[:48]}"
                )

        uid_speed = {}
        for n in play:
            uid_speed.setdefault(n["note_uid"], n["speed"])
        print("unique play uids", len(uid_speed))
        for uid, sp in sorted(uid_speed.items(), key=lambda x: (x[1], x[0])):
            sample = next(n for n in play if n["note_uid"] == uid)
            print(f"  {uid} speed={sp} bms={sample['bms_id']} {sample.get('des','')[:56]}")


if __name__ == "__main__":
    main()
