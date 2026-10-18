# -*- coding: utf-8 -*-
"""比对自制 MDM / 导出文件夹 与官谱 music_*.bundle（可玩键 / 事件 / Boss / 飞近档）。

用法:
  python compare_mdm_official.py <song.mdm|导出文件夹> [music_*.bundle] [--json]
  python compare_mdm_official.py 导出/change_the_game
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import zipfile
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _paths
import official_to_mdm as off

APPROACH = {"0S", "0X", "0Y", "0Z"}
EPS = 0.002
BOSS_PLAY = ("11", "12", "13", "14", "15", "16", "17", "18", "0G")
BOSS_EV = ("1A", "1B", "1C", "1D", "1E", "1F", "1G", "1H", "1L", "1M")
HOLD_IDS = {"0F", "0G", "16", "17"}
_BMS_LINE = re.compile(r"^#(\d{3})(\w{2}):([0-9A-Za-z]+)", re.M)


def _unpack_mdm(mdm: str, dest: str) -> dict:
    with zipfile.ZipFile(mdm) as z:
        names = z.namelist()
        z.extractall(dest)
    return {"zip": names}


def _mdchart_json(folder: str) -> dict:
    local = os.path.join(os.environ.get("LOCALAPPDATA") or "", "MuseDashChart", "run", "mdchart.exe")
    exe = local if os.path.isfile(local) else _paths.mdchart_exe()
    if not exe:
        raise FileNotFoundError("找不到 mdchart.exe")
    r = subprocess.run([exe, "json", folder], capture_output=True, text=True)
    if r.returncode != 0:
        raise RuntimeError((r.stdout or "") + (r.stderr or "") + " exe=" + exe)
    with open(os.path.join(folder, "chart.json"), encoding="utf-8") as f:
        return json.load(f)


def _parse_bms(text: str) -> tuple[list[dict], list[dict], list]:
    bpm = 120.0
    m = re.search(r"(?m)^#BPM\s+(\S+)", text)
    if m:
        try:
            bpm = float(m.group(1))
        except ValueError:
            pass
    cells = []
    for hit in _BMS_LINE.finditer(text):
        meas, ch, data = int(hit.group(1)), hit.group(2).upper(), hit.group(3)
        if len(data) % 2:
            data += "0"
        n = len(data) // 2
        beat = 240.0 / max(bpm, 1.0)
        for i in range(n):
            tok = data[i * 2 : i * 2 + 2].upper()
            if tok in ("", "00"):
                continue
            cells.append((meas * beat + (i / n) * beat, ch, tok))
    cells.sort(key=lambda x: (x[0], x[1], x[2]))
    pending = {}
    play, ev, zeros = [], [], []
    for t, ch, tok in cells:
        if ch == "0S":
            try:
                zeros.append((t, tok, int(tok)))
            except ValueError:
                zeros.append((t, tok, 1))
            continue
        if ch == "15":
            ev.append({"t": t, "bms": tok})
            continue
        if ch in ("53", "54") and tok in HOLD_IDS:
            key = (ch, tok)
            if key in pending:
                t0 = pending.pop(key)
                play.append({
                    "t": t0, "path": 1 if ch == "53" else 0, "bms": tok,
                    "len": t - t0, "rate": 1, "kind": "note",
                })
            else:
                pending[key] = t
            continue
        path = 1 if ch in ("13", "53", "11") else 0
        play.append({"t": t, "path": path, "bms": tok, "len": 0.0, "rate": 1, "kind": "note"})
    for (ch, tok), t0 in pending.items():
        play.append({
            "t": t0, "path": 1 if ch == "53" else 0, "bms": tok,
            "len": 0.0, "rate": 1, "kind": "note",
        })
    return play, ev, zeros


def _charts_from_bms_folder(folder: str) -> dict:
    charts = {}
    for idx in range(1, 6):
        path = os.path.join(folder, f"map{idx}.bms")
        if not os.path.isfile(path):
            continue
        text = open(path, encoding="utf-8", errors="replace").read()
        play, ev, zeros = _parse_bms(text)
        notes = []
        for n in play:
            notes.append({
                "timeSec": n["t"], "lengthSec": n["len"], "pathway": n["path"],
                "bmsId": n["bms"], "kind": "note", "approachRate": n["rate"],
            })
        for e in ev:
            notes.append({
                "timeSec": e["t"], "lengthSec": 0, "pathway": 0,
                "bmsId": e["bms"], "kind": "event",
            })
        for t, tok, rate in zeros:
            notes.append({
                "timeSec": t, "bmsId": "0S", "kind": "event", "approachRate": rate,
            })
        charts[idx] = {"notes": notes}
    sidecar = os.path.join(folder, "chart.json")
    if os.path.isfile(sidecar):
        try:
            extra = json.load(open(sidecar, encoding="utf-8"))
            for k, ch in (extra.get("charts") or {}).items():
                idx = int(k)
                ov = [n for n in (ch.get("notes") or []) if n.get("approachOverride")]
                if not ov or idx not in charts:
                    continue
                by_key = {(round(float(n["timeSec"]), 3), str(n.get("bmsId") or "").upper(), int(n.get("pathway") or 0)): n for n in ov}
                for n in charts[idx]["notes"]:
                    hit = by_key.get((round(float(n.get("timeSec") or 0), 3), str(n.get("bmsId") or "").upper(), int(n.get("pathway") or 0)))
                    if hit and hit.get("approachRate"):
                        n["approachRate"] = hit["approachRate"]
                        n["approachOverride"] = True
        except (OSError, ValueError, TypeError):
            pass
    return charts


def _copy_chart_folder(src: str) -> str:
    dest = os.path.join(
        os.environ.get("LOCALAPPDATA") or tempfile.gettempdir(),
        "MuseDashChart",
        "cmp-mdm",
    )
    if os.path.isdir(dest):
        shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest, exist_ok=True)
    if os.path.isfile(src) and src.lower().endswith(".mdm"):
        zipinfo = _unpack_mdm(src, dest)
        return dest, zipinfo["zip"]
    for name in os.listdir(src):
        a, b = os.path.join(src, name), os.path.join(dest, name)
        if os.path.isfile(a):
            shutil.copy2(a, b)
    return dest, os.listdir(dest)


def _load_charts(src: str) -> tuple[dict, dict, list]:
    dest, names = _copy_chart_folder(src)
    info_path = os.path.join(dest, "info.json")
    info = {}
    if os.path.isfile(info_path):
        with open(info_path, encoding="utf-8") as f:
            info = json.load(f)
    # mapN.bms 为准。mdchart json 会把 14 通道点长 0G 两两收成长条，change_the_game 会少几十颗。
    charts = _charts_from_bms_folder(dest)
    try:
        doc = _mdchart_json(dest)
        mch = {int(k): v for k, v in (doc.get("charts") or {}).items()}
        for idx, ch in mch.items():
            if idx not in charts:
                charts[idx] = ch
                continue
            ov = {}
            for n in ch.get("notes") or []:
                if not n.get("approachRate"):
                    continue
                key = (
                    round(float(n.get("timeSec") or 0), 3),
                    str(n.get("bmsId") or "").upper(),
                    int(n.get("pathway") or 0),
                )
                ov[key] = int(n.get("approachRate") or 1)
            for n in charts[idx].get("notes") or []:
                key = (
                    round(float(n.get("timeSec") or 0), 3),
                    str(n.get("bmsId") or "").upper(),
                    int(n.get("pathway") or 0),
                )
                if key in ov:
                    n["approachRate"] = ov[key]
    except (FileNotFoundError, RuntimeError, OSError):
        pass
    return info, charts, names


def _song_keys(src: str, info: dict) -> list[str]:
    keys = []
    if os.path.isfile(src) and src.lower().endswith(".mdm"):
        keys.append(os.path.splitext(os.path.basename(src))[0])
    elif os.path.isdir(src):
        keys.append(os.path.basename(os.path.normpath(src)))
    for tag in info.get("searchTags") or []:
        if tag:
            keys.append(str(tag))
    name = str(info.get("name") or "")
    if name:
        keys.append(re.sub(r"[^0-9A-Za-z_]+", "_", name).strip("_").lower())
    out, seen = [], set()
    for k in keys:
        k = str(k).strip()
        if k and k.lower() not in seen:
            seen.add(k.lower())
            out.append(k)
    return out


def resolve_bundle(src: str, info: dict, explicit: str | None) -> str:
    if explicit:
        path = os.path.normpath(explicit.strip().strip('"'))
        if os.path.isfile(path):
            return off.resolve_music_bundle(path)
        raise FileNotFoundError("找不到官包: " + path)
    bdir = _paths.bundle_dir()
    for key in _song_keys(src, info):
        cand = os.path.join(bdir, f"music_{key}_assets_all.bundle")
        if os.path.isfile(cand):
            return cand
        hits = [
            p for p in os.listdir(bdir)
            if p.lower().startswith("music_" + key.lower()) and p.lower().endswith(".bundle")
        ]
        if hits:
            return os.path.join(bdir, hits[0])
    raise FileNotFoundError(
        "未指定 music_*.bundle，且无法从曲名推断。请把官包路径作为第二参数。"
        + " 试过: " + ", ".join(_song_keys(src, info))
    )


def _off_rows(joined: list[dict], bpm: float | None = None, confirm_stars=None):
    taps, holds, events = off.notes_to_taps_holds(
        joined, bpm=float(bpm or 120), confirm_stars=confirm_stars)[:3]
    trans = off.speed_transitions(joined, bpm=bpm)
    play = []
    for t, air, bms in taps:
        play.append({
            "t": t, "path": 1 if air else 0, "bms": str(bms).upper(),
            "len": 0.0, "rate": off.approach_at(trans, t, bpm), "kind": "note",
        })
    for t0, t1, air, bms in holds:
        play.append({
            "t": t0, "path": 1 if air else 0, "bms": str(bms).upper(),
            "len": t1 - t0, "rate": off.approach_at(trans, t0, bpm), "kind": "note",
        })
    ev = [{"t": t, "bms": str(bms).upper()} for t, bms in events]
    return play, ev, trans


def _mdm_rows(chart: dict):
    play, ev, zeros = [], [], []
    for n in chart.get("notes") or []:
        bms = str(n.get("bmsId") or "").upper()
        t = float(n.get("timeSec") or 0)
        if bms in APPROACH:
            zeros.append((t, bms, int(n.get("approachRate") or 1)))
            continue
        kind = n.get("kind") or "note"
        if kind == "event":
            ev.append({"t": t, "bms": bms})
            continue
        play.append({
            "t": t,
            "path": int(n.get("pathway") or 0),
            "bms": bms,
            "len": float(n.get("lengthSec") or 0),
            "rate": int(n.get("approachRate") or 1),
            "kind": "note",
        })
    return play, ev, zeros


def _match(a: list[dict], b: list[dict]):
    used = [False] * len(b)
    pairs = []
    only_a, only_b = [], []
    for x in a:
        best_i, best_d = -1, 1e9
        for i, y in enumerate(b):
            if used[i] or x["path"] != y["path"] or x["bms"] != y["bms"]:
                continue
            d = abs(x["t"] - y["t"])
            if d < best_d:
                best_d, best_i = d, i
        if best_i >= 0 and best_d <= EPS:
            used[best_i] = True
            y = b[best_i]
            pairs.append((x, y, best_d))
        else:
            only_a.append(x)
    for i, y in enumerate(b):
        if not used[i]:
            only_b.append(y)
    return pairs, only_a, only_b


def _span(row: dict) -> tuple[float, float]:
    t0 = float(row["t"])
    return t0, t0 + max(float(row.get("len") or 0), 0.0)


def _ghost_hold_taps(play: list[dict]) -> list[dict]:
    """13/14 上的点长 0F：官机不画，自制谱会当成怪物和摆锤叠键。"""
    return [
        {"t": round(n["t"], 3), "path": n["path"], "len": round(n["len"], 3)}
        for n in play
        if n["bms"] == "0F" and n["len"] <= 0.02
    ]
    """0G 与 16/17 判定区间是否相交（不含飞近窗口）。"""
    g = [r for r in play if r["bms"] == "0G"]
    b = [r for r in play if r["bms"] in ("16", "17")]
    hits = []
    for x in g:
        x0, x1 = _span(x)
        for y in b:
            y0, y1 = _span(y)
            lo, hi = max(x0, y0), min(x1, y1)
            if hi >= lo - EPS:
                hits.append({
                    "t0g": round(x0, 3), "len0g": round(x1 - x0, 3), "path0g": x["path"],
                    "tBoss": round(y0, 3), "lenBoss": round(y1 - y0, 3), "pathBoss": y["path"],
                    "bms": y["bms"],
                })
    return hits


def compare(src: str, bundle: str, info: dict | None = None, charts: dict | None = None,
            names: list | None = None) -> dict:
    album = off.load_album(bundle)
    if info is None or charts is None:
        info, charts, names = _load_charts(src)
    names = names or []
    meta = album.get("meta") or {}
    cat = album.get("catalog") or []
    maps = album.get("maps") or {}
    confirm_stars = off.album_type7_star_keys(album)
    by_index = {row["index"]: row["name"] for row in cat}
    charts = {int(k): v for k, v in (charts or {}).items()}

    out_maps = []
    for idx in range(1, 6):
        map_name = by_index.get(idx)
        mdm_chart = charts.get(idx)
        if not mdm_chart:
            continue
        off_play, off_ev, trans = ([], [], [])
        if map_name and map_name in maps:
            off_play, off_ev, trans = _off_rows(
                maps[map_name]["notes"], maps[map_name].get("bpm"), confirm_stars)
        mdm_play, mdm_ev, zeros = ([], [], [])
        if mdm_chart:
            mdm_play, mdm_ev, zeros = _mdm_rows(mdm_chart)
        pairs, miss_off, extra = _match(off_play, mdm_play)
        len_bad = sum(1 for x, y, _ in pairs if abs(x["len"] - y["len"]) > 0.02)
        rate_bad = sum(1 for x, y, _ in pairs if x["rate"] != y["rate"])
        ev_pairs, ev_miss, ev_extra = _match(
            [{"t": e["t"], "path": 0, "bms": e["bms"], "len": 0, "rate": 1} for e in off_ev],
            [{"t": e["t"], "path": 0, "bms": e["bms"], "len": 0, "rate": 1} for e in mdm_ev],
        )
        samples_miss = [
            {"t": round(n["t"], 3), "path": n["path"], "bms": n["bms"], "len": round(n["len"], 3)}
            for n in miss_off[:12]
        ]
        samples_extra = [
            {"t": round(n["t"], 3), "path": n["path"], "bms": n["bms"], "len": round(n["len"], 3)}
            for n in extra[:12]
        ]
        rate_ex = []
        for x, y, d in pairs:
            if x["rate"] != y["rate"] and len(rate_ex) < 8:
                rate_ex.append({
                    "t": round(x["t"], 3), "bms": x["bms"],
                    "official": x["rate"], "mdm": y["rate"],
                })
        off_hist = _bms_hist(off_play)
        mdm_hist = _bms_hist(mdm_play)
        off_ev_hist = dict(Counter(e["bms"] for e in off_ev))
        mdm_ev_hist = dict(Counter(e["bms"] for e in mdm_ev))
        out_maps.append({
            "map": idx,
            "officialMap": map_name or "",
            "officialNotes": len(off_play),
            "mdmNotes": len(mdm_play),
            "matched": len(pairs),
            "missingInMdm": len(miss_off),
            "extraInMdm": len(extra),
            "lengthMismatch": len_bad,
            "rateMismatch": rate_bad,
            "officialEvents": len(off_ev),
            "mdmEvents": len(mdm_ev),
            "eventMatched": len(ev_pairs),
            "eventMissing": len(ev_miss),
            "eventExtra": len(ev_extra),
            "speedSteps": len(trans),
            "mdmApproachMarks": len(zeros),
            "officialBms": off_hist,
            "mdmBms": mdm_hist,
            "officialBossPlay": {k: off_hist[k] for k in BOSS_PLAY if off_hist.get(k)},
            "mdmBossPlay": {k: mdm_hist[k] for k in BOSS_PLAY if mdm_hist.get(k)},
            "officialBossEvents": {k: off_ev_hist[k] for k in BOSS_EV if off_ev_hist.get(k)},
            "mdmBossEvents": {k: mdm_ev_hist[k] for k in BOSS_EV if mdm_ev_hist.get(k)},
            "missingBoss": [
                {"t": round(n["t"], 3), "path": n["path"], "bms": n["bms"]}
                for n in miss_off if n["bms"] in BOSS_PLAY
            ][:16],
            "meleeOfficial": off_hist.get("11", 0) + off_hist.get("12", 0),
            "meleeMdm": mdm_hist.get("11", 0) + mdm_hist.get("12", 0),
            "bossMashOfficial": off_hist.get("16", 0) + off_hist.get("17", 0),
            "bossMashMdm": mdm_hist.get("16", 0) + mdm_hist.get("17", 0),
            "mashOverlapOfficial": _mash_time_overlap(off_play),
            "mashOverlapMdm": _mash_time_overlap(mdm_play),
            "ghostHoldTapsOfficial": _ghost_hold_taps(off_play),
            "ghostHoldTapsMdm": _ghost_hold_taps(mdm_play),
            "missingSamples": samples_miss,
            "extraSamples": samples_extra,
            "rateSamples": rate_ex,
        })

    return {
        "mdm": src,
        "bundle": bundle,
        "zip": names,
        "official": {
            "song": meta.get("name") or "",
            "music": meta.get("music") or "",
            "scene": meta.get("scene") or "",
            "bpm": meta.get("bpm"),
            "title": album["meta"].get("display_name") or "",
            "author": album["meta"].get("author") or "",
            "maps": [{"index": r["index"], "map": r["name"], "notes": r["notes"], "bpm": r["bpm"], "delay": r["delay"]} for r in cat],
        },
        "export": {
            "name": info.get("name"),
            "author": info.get("author"),
            "bpm": info.get("bpm"),
            "scene": info.get("scene"),
            "delay": info.get("delay"),
            "difficulty1": info.get("difficulty1"),
            "difficulty2": info.get("difficulty2"),
            "difficulty3": info.get("difficulty3"),
            "difficulty4": info.get("difficulty4"),
            "difficulty5": info.get("difficulty5"),
        },
        "maps": out_maps,
    }


def print_report(data: dict) -> None:
    o = data.get("official") or {}
    e = data.get("export") or {}
    print(f"{o.get('title') or o.get('song')}  ←  {e.get('name') or data.get('mdm')}")
    print(f"官包 {data.get('bundle')}")
    print(f"BPM {o.get('bpm')} / {e.get('bpm')}  scene {o.get('scene')} / {e.get('scene')}")
    print()
    for row in data.get("maps") or []:
        print(
            f"  map{row['map']}  键 {row['officialNotes']}→{row['mdmNotes']}  "
            f"对上 {row['matched']}  缺 {row['missingInMdm']}  多 {row['extraInMdm']}  "
            f"事件 {row['eventMatched']}/{row['officialEvents']}"
        )
        print(f"    近战 11/12  官 {row['meleeOfficial']}  自制 {row['meleeMdm']}    "
              f"Boss连打 16/17  官 {row['bossMashOfficial']}  自制 {row['bossMashMdm']}")
        ov_o, ov_m = row.get("mashOverlapOfficial") or [], row.get("mashOverlapMdm") or []
        if ov_o or ov_m:
            print(f"    0G∩16/17  官 {len(ov_o)}  自制 {len(ov_m)}"
                  + (f"  例 {ov_m[:3] or ov_o[:3]}" if (ov_m or ov_o) else ""))
        ghost = row.get("ghostHoldTapsMdm") or []
        if ghost:
            print(f"    点长0F残渣 自制 {len(ghost)}（13/14 上的 0F，会和同刻键叠成怪物） 例 {ghost[:4]}")
        ob, mb = row.get("officialBossPlay") or {}, row.get("mdmBossPlay") or {}
        keys = sorted(set(ob) | set(mb))
        if keys:
            bits = [f"{k} {ob.get(k, 0)}/{mb.get(k, 0)}" for k in keys]
            print("    Boss键 官/自制  " + "  ".join(bits))
        oe, me = row.get("officialBossEvents") or {}, row.get("mdmBossEvents") or {}
        ek = sorted(set(oe) | set(me))
        if ek:
            bits = [f"{k} {oe.get(k, 0)}/{me.get(k, 0)}" for k in ek]
            print("    脚本  " + "  ".join(bits))
        if row.get("missingBoss"):
            print("    自制缺 Boss:", row["missingBoss"][:8])
        if row["missingInMdm"] and row.get("missingSamples"):
            miss_ids = Counter(n["bms"] for n in row["missingSamples"])
            print("    缺键样本 ibms", dict(miss_ids), "…", row["missingSamples"][:4])
        if row["meleeOfficial"] == 0 and row["bossMashOfficial"] and row["meleeMdm"] == 0:
            print("    提示: 这张官谱没有近战 11/12，连打 16/17 不是导出写错。")


def main(argv: list[str]) -> int:
    args = [a for a in argv[1:] if a != "--json"]
    want_json = "--json" in argv[1:]
    if not args or args[0] in ("-h", "--help"):
        print(__doc__)
        return 0
    src = os.path.normpath(args[0].strip().strip('"'))
    explicit = args[1] if len(args) > 1 else None
    if not os.path.exists(src):
        print("找不到自制谱: " + src, file=sys.stderr)
        return 2
    info, charts, names = _load_charts(src)
    try:
        bundle = resolve_bundle(src, info, explicit)
    except FileNotFoundError as ex:
        print(ex, file=sys.stderr)
        return 2
    data = compare(src, bundle, info, charts, names)
    if want_json:
        print(json.dumps(data, ensure_ascii=False, indent=2))
    else:
        print_report(data)
    bad = any(m["missingInMdm"] or m["extraInMdm"] or m["eventMissing"] for m in data["maps"])
    return 1 if bad else 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    sys.exit(main(sys.argv))
