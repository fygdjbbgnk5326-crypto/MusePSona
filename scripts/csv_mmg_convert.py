"""官谱 CSV → MusePSona MDM（谱面编辑器）/ 可选 mmg

转录 MDM：写出 info.json + mapN.bms 并打包 .mdm，可直接用谱面编辑器打开。

用法:
  python csv_mmg_convert.py csv2mdm <in.csv> [out.mdm]
  python csv_mmg_convert.py batch <csv目录> [输出目录]
  python csv_mmg_convert.py csv2mmg <in.csv> [out.mmg]
  python csv_mmg_convert.py mmg2csv <in.mmg> [out.csv]
"""
from __future__ import annotations

import csv
import json
import math
import os
import re
import struct
import sys
import zipfile
from collections import defaultdict

MAGIC = 100000
REC = 24
SKIP_TYPES = {0, 7, 9, 10, 11, 12, 13, 14, 15, 16, 17}

def _f(v, default=0.0):
    try:
        if v in (None, "", "None"):
            return default
        return float(v)
    except (TypeError, ValueError):
        return default


def _i(v, default=0):
    try:
        if v in (None, "", "None", "false"):
            return default
        if str(v).lower() == "true":
            return 1
        return int(float(v))
    except (TypeError, ValueError):
        return default


def _b(v):
    return str(v).lower() in ("1", "true", "yes")


def read_csv_chart(path):
    meta = {
        "bpm": 120.0,
        "scene": "scene_01",
        "map_name": os.path.splitext(os.path.basename(path))[0],
        "music": "music.mp3",
        "difficulty": "1",
        "composer": "Unknown",
        "mapper": "Unknown",
    }
    rows = []
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        for line in f:
            if line.startswith("#"):
                text = line[1:].strip()
                for part in text.split(","):
                    if "=" not in part:
                        continue
                    k, v = part.split("=", 1)
                    k, v = k.strip(), v.strip()
                    if k in ("bpm", "runtime_bpm"):
                        try:
                            meta["bpm"] = float(v)
                        except ValueError:
                            pass
                    elif k in meta:
                        meta[k] = v
                continue
            rows.append(line)
    if not rows:
        raise ValueError("CSV 没有数据行: " + path)
    reader = csv.DictReader(rows)
    notes = list(reader)
    if not notes:
        raise ValueError("CSV 表头后没有音符: " + path)
    return meta, notes


def bpm_segments(notes):
    segs = []
    last = None
    for n in notes:
        bpm = _f(n.get("runtime_bpm") or n.get("bpm"), 0)
        if bpm <= 0:
            continue
        t = _f(n.get("time_sec"))
        key = round(bpm, 6)
        if last is None or abs(key - last) > 1e-4:
            segs.append({"time_sec": t, "bpm": bpm})
            last = key
    return segs


def pathway_track(n):
    air = _b(n.get("is_air")) or _i(n.get("pathway")) == 1
    return 1 if air else 2


def playable_holds_and_taps(notes):
    holds_span = []
    occupied = []
    for n in notes:
        typ = _i(n.get("type"))
        length = _f(n.get("length_sec"))
        if typ == 3 and (length > 0.02 or _b(n.get("is_long_pressing"))):
            t0 = _f(n.get("time_sec"))
            t1 = _f(n.get("end_time_sec"))
            if t1 <= t0:
                t1 = t0 + max(length, 0.05)
            holds_span.append((t0, t1, pathway_track(n)))
            occupied.append((t0 - 0.001, t1 + 0.001, pathway_track(n)))

    taps = []
    for n in notes:
        typ = _i(n.get("type"))
        if typ in SKIP_TYPES:
            continue
        t = _f(n.get("time_sec"))
        tr = pathway_track(n)
        if typ == 3:
            length = _f(n.get("length_sec"))
            if length > 0.02 or _b(n.get("is_long_pressing")):
                continue
            skip = False
            for a, b, tr2 in occupied:
                if tr == tr2 and a <= t <= b:
                    skip = True
                    break
            if skip:
                continue
            taps.append((t, tr))
            continue
        if typ == 8:
            t1 = _f(n.get("end_time_sec"))
            length = _f(n.get("length_sec"))
            if t1 <= t:
                t1 = t + max(length, 0.05)
            holds_span.append((t, t1, tr))
            continue
        if typ in (1, 2, 4, 5, 6):
            taps.append((t, tr))
    return taps, holds_span


SLOTS = 192
# 4/4 每拍 48 槽（与编辑器 GridClock.BmsSlotsPerBeat 相同）
SLOTS_PER_BEAT = 48
TYPE_TO_BMS = {
    1: "01",
    2: "0H",
    4: "21",
    5: "11",  # NoteType.Boss；0I 是 type 1 突击者倒，不要用 5 兜底
    6: "22",
    7: "10",
    8: "0G",
}


def norm_ibms(raw) -> str:
    ibms = str(raw or "").strip().upper()
    if len(ibms) == 1:
        ibms = "0" + ibms
    return ibms if len(ibms) == 2 else ""


def mash_ibms(ibms: str = "", des: str = "") -> str:
    """type 8：保留 0G / 16 / 17。缺 ibms 时用 notedata des（change_the_game）。"""
    ibms = norm_ibms(ibms)
    if ibms in ("0G", "16", "17"):
        return ibms
    d = des or ""
    if "后退场" in d:
        return "17"
    if "连击_平移" in d or "连击_下落" in d or "连击_上升" in d:
        return "0G"
    if "连击" in d:
        return "16"
    return "0G"


def _map_index(path, meta):
    m = re.search(r"_map(\d)", os.path.basename(path), re.I)
    if m:
        n = int(m.group(1))
        if 1 <= n <= 5:
            return n
    d = _i(meta.get("difficulty"), 1)
    return d if 1 <= d <= 5 else 1


def _is_air_note(n):
    return _b(n.get("is_air")) or _i(n.get("pathway")) == 1


def mdm_notes(notes):
    holds = []
    occupied = []
    for n in notes:
        typ = _i(n.get("type"))
        length = _f(n.get("length_sec"))
        if typ == 3 and (length > 0.02 or _b(n.get("is_long_pressing"))):
            t0 = _f(n.get("time_sec"))
            t1 = _f(n.get("end_time_sec"))
            if t1 <= t0:
                t1 = t0 + max(length, 0.05)
            air = _is_air_note(n)
            holds.append((t0, t1, air, "0F"))
            occupied.append((t0 - 0.001, t1 + 0.001, air))

    taps = []
    for n in notes:
        typ = _i(n.get("type"))
        t = _f(n.get("time_sec"))
        air = _is_air_note(n)
        des = n.get("des") or n.get("description") or ""
        ibms = mash_ibms(n.get("ibms_id") or n.get("bms_id"), des) if typ == 8 else norm_ibms(
            n.get("ibms_id") or n.get("bms_id")
        )
        if typ == 3:
            length = _f(n.get("length_sec"))
            if length > 0.02 or _b(n.get("is_long_pressing")):
                continue
            skip = any(tr == air and a <= t <= b for a, b, tr in occupied)
            if skip:
                continue
            if (ibms or "0F") == "0F":
                continue
            taps.append((t, air, ibms or "0F"))
            continue
        if typ == 8:
            t1 = _f(n.get("end_time_sec"))
            length = _f(n.get("length_sec"))
            span = max(t1 - t, length)
            if span > 0.02:
                if t1 <= t:
                    t1 = t + max(length, 0.05)
                holds.append((t, t1, air, ibms or "0G"))
            else:
                taps.append((t, air, ibms or "0G"))
            continue
        bms_id = ibms or TYPE_TO_BMS.get(typ)
        if bms_id:
            taps.append((t, air, bms_id))
    return taps, holds


def _sec_to_tick(t, bpm):
    return max(0.0, t / (240.0 / max(float(bpm), 1.0)))


def snap_to_slots(t, bpm, slots=SLOTS):
    """把秒吸到「小节 × slots」整数格，使 BMS 192 槽能整除。"""
    measure, slot = _split_slots(t, bpm, slots)
    meas = 240.0 / max(float(bpm), 1.0)
    return (measure + slot / slots) * meas


def slot_duration(bpm, slots=SLOTS):
    return (240.0 / max(float(bpm), 1.0)) / slots


def snap_delay_to_slots(delay, bpm, slots=SLOTS):
    """吃音只用整格：delay 秒吸到 n × 槽长。谱面 tick 不平移。"""
    try:
        d = float(delay)
    except (TypeError, ValueError):
        return 0.0
    if abs(d) < 1e-12:
        return 0.0
    step = slot_duration(bpm, slots)
    n = int(round(d / step))
    return n * step


def slot_index(t, bpm, slots=SLOTS):
    measure, slot = _split_slots(t, bpm, slots)
    return measure * slots + slot


def beat_phase_slots(t, bpm, slots=SLOTS, per_beat=SLOTS_PER_BEAT):
    """拍内槽 0–47。0=落在拍上。"""
    return slot_index(t, bpm, slots) % max(1, per_beat)


def _split_slots(t, bpm, slots=SLOTS):
    meas = 240.0 / max(float(bpm), 1.0)
    tick = max(0.0, float(t) / meas)
    measure = int(math.floor(tick))
    slot = int(round((tick - measure) * slots))
    if slot >= slots:
        measure += 1
        slot = 0
    return measure, slot


def _place_tick(cells, tick, channel, value):
    if tick < 0:
        tick = 0.0
    measure = int(math.floor(tick))
    slot = int(round((tick - measure) * SLOTS))
    if slot >= SLOTS:
        measure += 1
        slot = 0
    key = (measure, channel)
    rows = cells.setdefault(key, [])
    for row in rows:
        if row[slot] in ("00", value):
            row[slot] = value
            return
    neu = ["00"] * SLOTS
    neu[slot] = value
    rows.append(neu)


def _pack_row(row):
    used = [i for i, v in enumerate(row) if v != "00"]
    if not used:
        return "00"
    g = len(row)
    for i in used:
        g = math.gcd(g, i)
    if g == 0:
        g = 1
    length = len(row) // g
    return "".join(row[i * g] for i in range(length))


def write_bms(meta, taps, holds, segs, play_level="1", events=None, speeds=None):
    bpm = float(meta["bpm"])
    scene = str(meta.get("scene") or "scene_01")
    title = str(meta.get("map_name") or "Untitled")
    artist = str(meta.get("composer") or "Unknown") or "Unknown"
    mapper = str(meta.get("mapper") or "Unknown") or "Unknown"
    lines = [
        f"#GENRE {scene}",
        f"#TITLE {title}",
        f"#ARTIST {artist}",
        f"#LEVELDESIGN {mapper}",
        f"#BPM {bpm:g}",
        "#PLAYER 1",
        "#LNTYPE 1",
        f"#PLAYLEVEL {play_level}",
        "",
    ]
    table = []
    cells = {}

    def place_sec(t, channel, value):
        _place_tick(cells, _sec_to_tick(snap_to_slots(t, bpm), bpm), channel, value)

    if len(segs) > 1:
        for i, seg in enumerate(segs):
            if i == 0 and abs(seg["bpm"] - bpm) < 1e-4:
                continue
            key = f"{len(table) + 1:02X}"
            table.append((key, seg["bpm"]))
            place_sec(seg["time_sec"], "08", key)
        for key, value in table:
            lines.append(f"#BPM{key} {value:g}")
        if table:
            lines.append("")

    for t, air, bms_id in taps:
        bid = str(bms_id or "").upper()
        if bid == "0F":
            continue
        ch = "13" if air else "14"
        place_sec(t, ch, bid)
    for t0, t1, air, bms_id in holds:
        ch = "53" if air else "54"
        place_sec(t0, ch, bms_id)
        place_sec(t1, ch, bms_id)
    for t, bms_id in events or []:
        if not bms_id:
            continue
        place_sec(t, "15", bms_id)
    for t, rate in speeds or []:
        # 物件速度档走 0S 通道（值 01/02/03），与编辑器 BmsWriter 约定一致。
        place_sec(t, "0S", f"{int(rate):02d}")

    packed = []
    for (measure, channel), rows in cells.items():
        for i, row in enumerate(rows):
            packed.append((measure, channel, i, _pack_row(row)))
    packed.sort(key=lambda x: (x[0], x[1], x[2]))
    for measure, channel, _i, body in packed:
        lines.append(f"#{measure:03d}{channel}:{body}")
    return "\r\n".join(lines) + "\r\n"


def write_info_json(meta, map_index, play_level):
    bpm = meta["bpm"]
    bpm_s = str(int(bpm)) if float(bpm).is_integer() else f"{bpm:g}"
    diffs = ["0"] * 5
    diffs[map_index - 1] = str(play_level)
    name = str(meta.get("music") or meta.get("map_name") or "Untitled")
    if name.lower().endswith((".mp3", ".ogg", ".wav")):
        name = os.path.splitext(name)[0]
    return {
        "name": name,
        "name_romanized": "",
        "author": str(meta.get("composer") or "Unknown") or "Unknown",
        "levelDesigner": str(meta.get("mapper") or "Unknown") or "Unknown",
        "bpm": bpm_s,
        "scene": str(meta.get("scene") or "scene_01"),
        "delay": "0",
        "difficulty1": diffs[0],
        "difficulty2": diffs[1],
        "difficulty3": diffs[2],
        "difficulty4": diffs[3],
        "difficulty5": diffs[4],
        "unlockLevel": "0",
        "searchTags": [],
        "levelDesigner1": "",
        "levelDesigner2": "",
        "levelDesigner3": "",
        "levelDesigner4": "",
        "levelDesigner5": "",
    }


def csv2mdm(src, dst=None):
    meta, notes = read_csv_chart(src)
    taps, holds = mdm_notes(notes)
    segs = bpm_segments(notes)
    map_index = _map_index(src, meta)
    play_level = str(meta.get("difficulty") or "1")
    if play_level in ("", "0"):
        play_level = "1"
    bms = write_bms(meta, taps, holds, segs, play_level)
    info = write_info_json(meta, map_index, play_level)
    if dst is None:
        dst = os.path.splitext(src)[0] + ".mdm"
    os.makedirs(os.path.dirname(os.path.abspath(dst)) or ".", exist_ok=True)
    tmp = dst + ".tmp"
    with zipfile.ZipFile(tmp, "w", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr("info.json", json.dumps(info, ensure_ascii=False, indent=2))
        z.writestr(f"map{map_index}.bms", bms)
    os.replace(tmp, dst)
    return dst, {
        "mdm": os.path.abspath(dst),
        "map": map_index,
        "taps": len(taps),
        "holds": len(holds),
        "bpm": meta["bpm"],
        "scene": info["scene"],
        "variable": len(segs) > 1,
    }


def batch_csv_dir(src_dir, dst_dir=None, kind="mdm"):
    dst_dir = dst_dir or src_dir
    os.makedirs(dst_dir, exist_ok=True)
    files = sorted(
        n for n in os.listdir(src_dir)
        if n.lower().endswith(".csv") and not n.startswith("_")
    )
    results = []
    ext = ".mdm" if kind == "mdm" else ".mmg"
    for name in files:
        src = os.path.join(src_dir, name)
        dst = os.path.join(dst_dir, os.path.splitext(name)[0] + ext)
        try:
            if kind == "mdm":
                path, info = csv2mdm(src, dst)
                results.append((name, "ok", info["taps"], info["holds"], info["variable"], path))
                print(f"  OK  {name}  map{info['map']} tap={info['taps']} hold={info['holds']}")
            else:
                path, sidecar, info = csv2mmg(src, dst)
                results.append((name, "ok", info["taps"], info["holds"], info["variable"], path))
                print(f"  OK  {name}  tap={info['taps']} hold={info['holds']} var_bpm={info['variable']}")
        except Exception as ex:
            results.append((name, str(ex), 0, 0, False, ""))
            print(f"  FAIL {name}  {ex}")
    return results


def write_mmg(out_path, bpm, audio_name, taps, holds, last_t, grid=4, offset_ms=0):
    beat = 60.0 / max(bpm, 1.0)
    cells = max(1, int(round(last_t / beat)) + 1)
    stem = audio_name if audio_name.lower().endswith((".mp3", ".ogg", ".wav")) else audio_name + ".mp3"
    cfg = f"{stem},{grid},{cells},{int(offset_ms)}"
    cfg_b = cfg.encode("utf-8")
    buf = bytearray(struct.pack("<IfI", MAGIC, float(bpm), len(cfg_b)))
    buf += cfg_b
    order = 0
    gid = 1
    for t, tr in sorted(taps, key=lambda x: (x[0], x[1])):
        buf += struct.pack("<ifiiii", tr, float(t), 1, -1, -1, 0)
        order += 1
    for t0, t1, tr in sorted(holds, key=lambda x: (x[0], x[2])):
        buf += struct.pack("<ifiiii", tr, float(t0), 2, gid, 0, 0)
        buf += struct.pack("<ifiiii", tr, float(t1), 2, gid, 1, 0)
        gid += 1
        order += 2
    os.makedirs(os.path.dirname(os.path.abspath(out_path)) or ".", exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(buf)
    return len(taps), len(holds)


def csv2mmg(src, dst=None):
    meta, notes = read_csv_chart(src)
    taps, holds = playable_holds_and_taps(notes)
    last_t = 0.0
    if notes:
        last_t = max(_f(n.get("time_sec")) + _f(n.get("length_sec")) for n in notes)
    if dst is None:
        dst = os.path.splitext(src)[0] + ".mmg"
    n_tap, n_hold = write_mmg(dst, meta["bpm"], str(meta.get("music") or meta["map_name"]), taps, holds, last_t)
    segs = bpm_segments(notes)
    sidecar = os.path.splitext(dst)[0] + ".bpm.json"
    payload = {
        "source_csv": os.path.abspath(src),
        "bpm_header": meta["bpm"],
        "scene": meta.get("scene"),
        "map_name": meta.get("map_name"),
        "segments": segs,
        "variable": len(segs) > 1,
        "taps": n_tap,
        "holds": n_hold,
        "mmg": os.path.abspath(dst),
        "note": "mmg 头只有一个 BPM；键时间为 CSV 绝对秒，变速不改时间。",
    }
    with open(sidecar, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=2)
    return dst, sidecar, payload


def parse_mmg(path):
    b = open(path, "rb").read()
    magic, bpm, nlen = struct.unpack_from("<IfI", b, 0)
    if magic != MAGIC:
        raise ValueError(f"不是 MusePSona 二进制 mmg（魔数 {magic}）")
    cfg = b[12:12 + nlen].decode("utf-8", "replace")
    parts = cfg.split(",")
    pos = 12 + nlen
    recs = []
    while pos + REC <= len(b):
        track, t, typ, grp, idx, evt = struct.unpack_from("<ifiiii", b, pos)
        recs.append({"track": track, "time": t, "type": typ, "group": grp, "index": idx, "event": evt})
        pos += REC
    if len(b) - pos:
        raise ValueError(f"尾部残留 {len(b) - pos} 字节")
    return {"bpm": bpm, "cfg": cfg, "audio": parts[0] if parts else "music.mp3", "recs": recs}


def mmg2csv(src, dst=None):
    mmg = parse_mmg(src)
    if dst is None:
        dst = os.path.splitext(src)[0] + ".csv"
    groups = defaultdict(list)
    rows_out = []
    idx = 0
    for r in mmg["recs"]:
        if r["type"] == 2 and r["group"] >= 0:
            groups[r["group"]].append(r)
            continue
        air = 1 if r["track"] in (0, 1) else 0
        rows_out.append(_csv_row(idx, r["time"], 0, 1, "monster", air, mmg["bpm"]))
        idx += 1
    for gid, segs in groups.items():
        segs.sort(key=lambda x: x["index"])
        t0 = segs[0]["time"]
        t1 = segs[-1]["time"]
        air = 1 if segs[0]["track"] in (0, 1) else 0
        length = max(0.0, t1 - t0)
        rows_out.append(_csv_row(idx, t0, length, 3, "hold", air, mmg["bpm"], t1))
        idx += 1
    rows_out.sort(key=lambda r: r["time_sec"])
    for i, r in enumerate(rows_out):
        r["index"] = i
    fields = list(rows_out[0].keys()) if rows_out else ["index", "time_sec"]
    os.makedirs(os.path.dirname(os.path.abspath(dst)) or ".", exist_ok=True)
    with open(dst, "w", encoding="utf-8", newline="") as f:
        f.write(f"# bpm={mmg['bpm']},runtime_bpm={mmg['bpm']}\n")
        f.write(f"# music={mmg['audio']},provenance=mmg2csv\n")
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows_out)
    return dst, len(rows_out)


def _csv_row(index, t, length, typ, name, air, bpm, end=0.0):
    if end <= 0:
        end = t + length
    return {
        "index": index,
        "time_sec": f"{t:.6f}",
        "time_ms": int(round(t * 1000)),
        "end_time_sec": f"{end:.6f}" if length > 0 else "0.000000",
        "end_time_ms": int(round(end * 1000)) if length > 0 else 0,
        "length_sec": f"{length:.6f}",
        "length_ms": int(round(length * 1000)),
        "type": typ,
        "type_name": name,
        "is_air": "true" if air else "false",
        "pathway": air,
        "bpm": f"{bpm:.6f}",
        "runtime_bpm": f"{bpm:.6f}",
    }


def main(argv):
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print(__doc__)
        return 0
    cmd = argv[1].lower()
    if cmd == "csv2mdm":
        if len(argv) < 3:
            print("需要 CSV 路径")
            return 2
        dst = argv[3] if len(argv) > 3 else None
        path, info = csv2mdm(argv[2], dst)
        print("写出", path)
        print("map", info["map"], "tap", info["taps"], "hold", info["holds"], "scene", info["scene"])
        return 0
    if cmd == "csv2mmg":
        if len(argv) < 3:
            print("需要 CSV 路径")
            return 2
        dst = argv[3] if len(argv) > 3 else None
        path, sidecar, info = csv2mmg(argv[2], dst)
        print("写出", path)
        print("BPM段", sidecar, "variable=", info["variable"], "segs=", len(info["segments"]))
        print("tap", info["taps"], "hold", info["holds"])
        return 0
    if cmd == "mmg2csv":
        if len(argv) < 3:
            print("需要 MMG 路径")
            return 2
        dst = argv[3] if len(argv) > 3 else None
        path, n = mmg2csv(argv[2], dst)
        print("写出", path, "行", n)
        return 0
    if cmd in ("batch", "batch_mdm"):
        if len(argv) < 3:
            print("需要 CSV 目录")
            return 2
        dst = argv[3] if len(argv) > 3 else None
        batch_csv_dir(argv[2], dst, kind="mdm")
        return 0
    if cmd == "batch_mmg":
        if len(argv) < 3:
            print("需要 CSV 目录")
            return 2
        dst = argv[3] if len(argv) > 3 else None
        batch_csv_dir(argv[2], dst, kind="mmg")
        return 0
    print("未知命令", cmd)
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
