"""官谱 bundle → MDM。

权威输入是游戏资源，不是 31 列 CSV：

  music_<id>_assets_all.bundle
      StageInfo (mopemope_mapN)  SerializedBytes = MusicData 列表
      AudioClip  整曲
  song_<id>_assets_all_*.bundle
      Texture2D 封面 / AudioClip 试听
  config_others_assets_notedata_*.bundle
      NoteConfigData 模板（uid → type / pathway / ibms_id）

map_decoder 的 list/export CSV 保持不变；本模块只多读 MusicConfigData
（tick / note_uid / length / pathway）并 join 模板后打包编辑器 MDM。

CSV→MDM（csv_mmg_convert.csv2mdm）仍作已导出表的兜底，映射表可引用，
但 ibms_id 优先于 type 猜测。点长 0F（type 3、length=0）不进 13/14。

类似 Cyaegha 的官机不要拿低难度 type7 23 当硬开关（同句常换轨，或只写在长条头上）。
固定三条：
  type7 23 → 23（0003_score 音符；叠在长条上也不画头星）
  1 槽 0F → 24（02_top 星，条内条外都转）
  同刻同轨已有其它可玩键（摆锤/怪/23 等）→ 丢掉这颗 1 槽 0F（chrome_vox 点长残渣）
导出时用专辑 type7 23 只做 star24 对表统计。Play 必须把 24 算进 IsPlayableP0。
"""
from __future__ import annotations

import glob
import json
import os
import re
import struct
import subprocess
import sys
import zipfile
from collections import Counter

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import _paths
import map_decoder as md
import csv_mmg_convert as conv

# 抓取 tick 可能含设备游戏延迟。默认自动检索；--capture-delay 可强制。
# D 必须是整格秒 n×(240/BPM/192)，禁止用游戏设置里的 0.35 这种两位小数去减。
MAX_CAPTURE_DELAY_SEC = 0.6
DELAY_CACHE_NAME = "capture_delay.txt"

UID_KEY = "note_uid".encode("utf-16le")
LEN_KEY = "length".encode("utf-16le")
PATH_KEY = "pathway".encode("utf-16le")
TICK_KEY = "tick".encode("utf-16le") + b"\x00\x00"
UID_RE = re.compile(rb"(?:[\x30-\x39]\x00){6,8}")


def _utf16_digits(blob: bytes) -> str:
    m = UID_RE.search(blob)
    if not m:
        return ""
    return m.group().decode("utf-16le")


def parse_music_data(serialized: bytes) -> list[dict]:
    """从 StageInfo.SerializedBytes 解 MusicData + 内嵌 MusicConfigData。"""
    anchors = [m.start() for m in re.finditer(re.escape(TICK_KEY), serialized)]
    notes = []
    for i, a in enumerate(anchors):
        hi = anchors[i + 1] if i + 1 < len(anchors) else len(serialized)
        w = serialized[a:hi]
        tick = md.rd_dec(w, 8)
        uid = ""
        p = w.find(UID_KEY)
        if p >= 0:
            uid = _utf16_digits(w[p + len(UID_KEY):p + len(UID_KEY) + 48])
        length = 0.0
        p = w.find(LEN_KEY)
        if p >= 0:
            for off in (len(LEN_KEY), len(LEN_KEY) + 2, len(LEN_KEY) + 4):
                if p + off + 16 <= len(w):
                    v = md.rd_dec(w, p + off)
                    if 0 <= v < 600:
                        length = v
                        break
        pathway = None
        p = w.find(PATH_KEY)
        if p >= 0:
            for off in range(len(PATH_KEY), len(PATH_KEY) + 8):
                if p + off + 4 > len(w):
                    break
                v = struct.unpack_from("<i", w, p + off)[0]
                if v in (0, 1):
                    pathway = v
                    break
        if tick <= 0 and not uid:
            continue
        notes.append({"tick": tick, "note_uid": uid, "length": length, "pathway": pathway})
    notes.sort(key=lambda n: n["tick"])
    return notes


def load_notedata(bundle_dir: str) -> dict:
    path = _paths.notedata_bundle() if not bundle_dir else None
    if bundle_dir:
        hits = glob.glob(os.path.join(bundle_dir, "config_others_assets_notedata_*.bundle"))
        if hits:
            path = hits[0]
    if not path:
        path = _paths.notedata_bundle()
    env = __import__("UnityPy").load(path)
    ta = next(o.read() for o in env.objects if o.type.name == "TextAsset")
    raw = ta.m_Script if isinstance(ta.m_Script, str) else bytes(ta.m_Script).decode("utf-8-sig")
    tpl = json.loads(raw)
    return {str(e.get("uid")): e for e in tpl}


def join_template(note: dict, templates: dict) -> dict:
    e = templates.get(note["note_uid"]) or {}
    typ = int(e.get("type") or 0)
    pathway = note["pathway"]
    if pathway is None:
        pathway = int(e.get("pathway") or 0)
    ibms = conv.norm_ibms(e.get("ibms_id"))
    if typ == 8:
        ibms = conv.mash_ibms(ibms, e.get("des") or "")
    elif len(ibms) != 2:
        if typ == 3:
            ibms = "0F"
        else:
            ibms = conv.TYPE_TO_BMS.get(typ, "")
    air = pathway == 1
    return {
        "tick": note["tick"],
        "length": note["length"],
        "air": air,
        "type": typ,
        "bms_id": ibms,
        "note_uid": note["note_uid"],
        "des": e.get("des") or "",
        "speed": int(e.get("speed") or 1),
    }


def parse_delay(serialized: bytes) -> float:
    """StageInfo.delay 在 SerializedBytes 尾部（Decimal 秒），不写进音符。"""
    if not serialized:
        return 0.0
    key = "delay".encode("utf-16le") + b"\x00\x00"
    p = serialized.rfind(key)
    if p < 0:
        return 0.0
    for off in (len(key), len(key) + 2):
        if p + off + 16 <= len(serialized):
            v = md.rd_dec(serialized, p + off)
            if abs(v) < 60:
                return float(v)
    return 0.0


def lookup_music_info(catalog: dict, song_id: str, music_field: str) -> dict:
    cands = []
    if song_id:
        cands.append(song_id)
    if music_field:
        cands.append(md._song_key({"music": music_field}))
        cands.append(music_field)
    for k in cands:
        if k and k in catalog:
            return catalog[k]
    return {}


def _s(entry: dict, *keys, default=""):
    for k in keys:
        v = entry.get(k)
        if v is None:
            continue
        t = str(v).strip()
        if t:
            return t
    return default


def _star_of(entry: dict, map_index: int) -> str:
    v = _s(entry, f"difficulty{map_index}")
    if v:
        return v
    return "1"


def _designer_of(entry: dict, map_index: int) -> str:
    return _s(entry, f"levelDesigner{map_index}", "levelDesigner")


def _fmt_bpm_num(v) -> str:
    try:
        f = float(v)
    except (TypeError, ValueError):
        return ""
    if f <= 0:
        return ""
    return str(int(f)) if float(f).is_integer() else f"{f:g}"


def _fmt_delay(v) -> str:
    try:
        f = float(v)
    except (TypeError, ValueError):
        return "0"
    if abs(f) < 1e-9:
        return "0"
    return str(int(f)) if float(f).is_integer() else f"{f:g}"


def build_info_json(song_id: str, stage_meta: dict, music_info: dict,
                    diffs: list[str], designers: list[str], delay: float) -> dict:
    name = _s(music_info, "name", default=song_id) or song_id
    author = _s(music_info, "author")
    scene = _s(music_info, "scene") or stage_meta.get("scene") or "scene_01"
    bpm = _fmt_bpm_num(stage_meta.get("bpm")) or "120"
    delay_s = _fmt_delay(delay)
    lead = next((d for d in designers if d), "")
    return {
        "name": name,
        "name_romanized": _s(music_info, "name_romanized"),
        "author": author,
        "levelDesigner": lead,
        "bpm": bpm,
        "scene": scene,
        "delay": delay_s,
        "difficulty1": diffs[0],
        "difficulty2": diffs[1],
        "difficulty3": diffs[2],
        "difficulty4": diffs[3],
        "difficulty5": diffs[4],
        "unlockLevel": _s(music_info, "unlockLevel", default="0") or "0",
        "searchTags": [song_id] if song_id else [],
        "levelDesigner1": designers[0],
        "levelDesigner2": designers[1],
        "levelDesigner3": designers[2],
        "levelDesigner4": designers[3],
        "levelDesigner5": designers[4],
    }


def _is_event_note(typ: int, bms: str) -> bool:
    """NoteType 0 与 ≥9 是脚本/演出；可玩键是 1–8。"""
    if not bms:
        return False
    return typ == 0 or typ >= 9


def _delay_cache_path() -> str:
    root = os.environ.get("LOCALAPPDATA") or os.path.expanduser("~")
    return os.path.join(root, "MuseDashChart", DELAY_CACHE_NAME)


def load_cached_capture_delay() -> float | None:
    try:
        with open(_delay_cache_path(), encoding="utf-8") as f:
            v = float((f.read() or "").strip())
        if 0 <= v <= MAX_CAPTURE_DELAY_SEC:
            return v
    except (OSError, ValueError):
        return None
    return None


def save_cached_capture_delay(delay: float) -> None:
    path = _delay_cache_path()
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as f:
            f.write(f"{delay:.12g}\n")
    except OSError:
        pass


def _snap_residual_mean(ticks: list[float], delay: float, bpm: float) -> float:
    total = 0.0
    n = 0
    for t in ticks:
        x = t - delay
        if x < -1e-7:
            return 1e9
        s = conv.snap_to_slots(max(0.0, x), bpm)
        total += abs(x - s)
        n += 1
    return total / max(n, 1)


def format_capture_delay(delay: float, bpm: float) -> str:
    """打印整格秒，例如 BPM160：45格=0.3515625s（槽长0.0078125s）。"""
    d = conv.snap_delay_to_slots(delay, bpm)
    if abs(d) < 1e-12:
        return "0格=0s"
    n = int(round(d / conv.slot_duration(bpm)))
    return f"{n}格={d:.12g}s（槽长{conv.slot_duration(bpm):.12g}s）"


def estimate_capture_delay(ticks: list[float], bpm: float,
                           max_sec: float = MAX_CAPTURE_DELAY_SEC,
                           extra: list[float] | None = None) -> float:
    """检索设备延迟 D：tick−D 贴 192 槽，且 D 本身是整格秒。

    D = n × (240/BPM/192)。游戏设置里的 0.35 在 BPM160 应变成 45格=0.3515625s，
    不能直接减 0.35。键已在格子上时残差相同，用「最早键扣完后落在拍上」
    （拍内槽 0–47 为 0）决胜，小 D 优先，避免把整拍前奏当延迟。
    """
    ticks = [float(t) for t in ticks if t is not None and float(t) > 1e-9]
    if len(ticks) < 4 or not bpm or float(bpm) <= 0:
        return 0.0
    bpm = float(bpm)
    t0 = min(ticks)
    cap = min(float(max_sec), t0)
    slot = conv.slot_duration(bpm)
    cands: set[float] = {0.0}
    n = 0
    while n * slot <= cap + 1e-12:
        cands.add(n * slot)
        n += 1
    phase0 = conv.beat_phase_slots(t0, bpm)
    if 0 < phase0 * slot <= cap + 1e-9:
        cands.add(phase0 * slot)
    if t0 <= cap + 1e-9:
        cands.add(conv.snap_delay_to_slots(t0, bpm))
    for x in extra or []:
        if x is None:
            continue
        try:
            v = float(x)
        except (TypeError, ValueError):
            continue
        d = conv.snap_delay_to_slots(v, bpm)
        if 0 <= d <= cap + 1e-9:
            cands.add(d)

    def score(delay: float) -> tuple[float, int, float]:
        d = conv.snap_delay_to_slots(delay, bpm)
        mean = _snap_residual_mean(ticks, d, bpm)
        first = max(0.0, t0 - d)
        phase = conv.beat_phase_slots(first, bpm)
        return (mean, phase, d)

    zero = score(0.0)
    best_d, best_s = 0.0, zero
    for delay in sorted(cands):
        if delay > cap + 1e-9:
            continue
        s = score(delay)
        if s < best_s:
            best_d, best_s = s[2], s
    if best_d <= 1e-9:
        return 0.0
    # 残差明显变差才放弃（已在格子上时残差相等，允许整格平移把首键推到拍上）
    if best_s[0] > zero[0] * 0.85 + 1e-12:
        return 0.0
    return conv.snap_delay_to_slots(best_d, bpm)


def strip_capture_delay(joined: list[dict], delay: float) -> None:
    """去掉整格设备延迟，得到 0.0 标准轴。length 是时长，不减。"""
    d = float(delay or 0)
    if abs(d) < 1e-12:
        return
    for n in joined:
        n["tick"] = max(0.0, float(n["tick"]) - d)


def snap_joined_to_192(joined: list[dict], bpm: float, capture_delay: float = 0.0) -> None:
    """先减去整格 D，再吸到 0.0 对齐的 192 标准槽。info.delay 不写进音符。"""
    strip_capture_delay(joined, conv.snap_delay_to_slots(capture_delay, bpm))
    for n in joined:
        raw = float(n["tick"])
        t0 = conv.snap_to_slots(raw, bpm)
        ln = float(n.get("length") or 0)
        if ln > 0:
            n["length"] = max(0.0, conv.snap_to_slots(raw + ln, bpm) - t0)
        n["tick"] = t0


def _hold_length_min(bpm: float) -> float:
    """长按头门槛：必须长过 1 槽点长，避免低 BPM 把条上星收成短条。"""
    slot = conv.slot_duration(bpm)
    return max(0.02, slot * 2 + 1e-9)


def _note_key(t: float, air: bool, bpm: float) -> tuple:
    return (round(conv.snap_to_slots(float(t), bpm), 4), bool(air))


def type7_star_keys(joined: list[dict], bpm: float) -> set[tuple]:
    """本张 type7 23 的 (吸槽秒, 空中)。用来确认其它 map 的 1 槽 0F 是不是星。"""
    keys = set()
    for n in joined:
        if int(n.get("type") or 0) != 7:
            continue
        if str(n.get("bms_id") or "").upper() != "23":
            continue
        keys.add(_note_key(n["tick"], n["air"], bpm))
    return keys


def album_type7_star_keys(album: dict) -> set[tuple]:
    keys = set()
    for data in (album.get("maps") or {}).values():
        bpm = float(data.get("bpm") or 120)
        keys |= type7_star_keys(data.get("notes") or [], bpm)
    return keys


def _is_slot_star_sample(n: dict, bpm: float) -> bool:
    """1 槽点长 0F。length=0 是采样刻；超过 2 槽是长按头。"""
    if int(n.get("type") or 0) != 3:
        return False
    if str(n.get("bms_id") or "").upper() != "0F":
        return False
    ln = float(n.get("length") or 0)
    slot = conv.slot_duration(bpm)
    return slot * 0.5 < ln <= slot * 1.5 + 1e-12


def _clash_tap_keys(joined: list[dict], bpm: float, hold_min: float) -> set[tuple]:
    """同刻同轨已有可玩键。1 槽 0F 撞上就丢掉，避免和摆锤叠星。"""
    keys = set()
    for n in joined:
        bms = str(n.get("bms_id") or "").upper()
        if not bms or _is_event_note(n.get("type"), bms):
            continue
        ln = float(n.get("length") or 0)
        if bms in ("0F", "0G") and ln <= hold_min:
            continue
        keys.add(_note_key(n["tick"], n["air"], bpm))
    return keys


def notes_to_taps_holds(joined: list[dict], bpm: float = 120.0,
                        confirm_stars: set | None = None):
    """可玩键 + 事件。长按/连打只留头上的 length；事件走通道 15。

    confirm_stars 只用于统计，不挡 24。返回
    (taps, holds, events, dropped_0f, hold_stars)。
    """
    hold_min = _hold_length_min(bpm)
    clash = _clash_tap_keys(joined, bpm, hold_min)
    holds = []
    events = []
    for n in joined:
        typ, bms, t, air = n["type"], n["bms_id"], n["tick"], n["air"]
        if not bms:
            continue
        if _is_event_note(typ, bms):
            events.append((t, bms))
            continue
        length = n["length"]
        if typ == 3 and length > hold_min:
            hid = "0F" if bms == "0F" or not bms else bms
            holds.append((t, t + length, air, hid))
        elif typ == 8 and length > hold_min:
            hid = conv.mash_ibms(bms, n.get("des") or "")
            holds.append((t, t + length, air, hid))
    holds = _merge_holds(holds)
    occupied = [(a, b, air) for a, b, air, _ in holds]
    taps = []
    dropped_0f = 0
    hold_stars = 0
    seen = set()
    for n in joined:
        typ, bms, t, air = n["type"], n["bms_id"], n["tick"], n["air"]
        if not bms or _is_event_note(typ, bms):
            continue
        if typ in (3, 8):
            # 长按/连打只留头；中间刻的 0F/0G/16/17 丢掉。同轨上的音符/摆锤/突击者等要保留
            # （reanimate map4 在长条里叠了 23/0D/0C/0E 等，旧逻辑只放行 0H 会少 180+ 颗）。
            if n["length"] > hold_min:
                continue
            if _is_slot_star_sample(n, bpm):
                star_key = _note_key(t, air, bpm)
                if star_key in clash:
                    dropped_0f += 1
                    continue
                key = (star_key[0], air, "24")
                if key not in seen:
                    seen.add(key)
                    taps.append((t, air, "24"))
                    hold_stars += 1
                continue
            if _inside_hold(t, air, occupied):
                if bms == "0F":
                    dropped_0f += 1
                continue
            if bms == "0F":
                dropped_0f += 1
                continue
        key = (round(t, 4), air, bms)
        if key in seen:
            continue
        seen.add(key)
        taps.append((t, air, bms))
    taps.sort(key=lambda x: (x[0], x[1]))
    return taps, holds, events, dropped_0f, hold_stars


def _clamp_speed(n) -> int:
    rate = int(n.get("speed") or 1)
    return 1 if rate < 1 else (3 if rate > 3 else rate)


def _is_playable_speed(n) -> bool:
    return bool(n.get("bms_id")) and not _is_event_note(n["type"], n["bms_id"])


def _is_ghost_note(n) -> bool:
    return int(n.get("type") or 0) == 4 or str(n.get("bms_id") or "").upper() == "21"


def _pick_bucket_rate(notes: list[dict]) -> int:
    rates = [_clamp_speed(n) for n in notes]
    if len(set(rates)) == 1:
        return rates[0]
    music = [n for n in notes if not _is_ghost_note(n)]
    pool = music or notes
    counts = Counter(_clamp_speed(n) for n in pool)
    return min(counts, key=lambda r: (-counts[r], r))


def speed_transitions(joined: list[dict], min_gap: float = 0.0, bpm: float | None = None):
    """可玩键 speed 档变化点 → [(time_sec, rate)]，含回到 1。

    抓取秒已减去整格设备延迟 D=n×(240/BPM/192)，再按 192 槽：同槽一档
    （幽灵让路，多数票，平票取小），相邻同档丢掉。通道 15 与 0S 同一条标准轴。
    君往何处 map3 例：BPM160 槽长=0.0078125s，D=45格=0.3515625s（不是 0.35s），
    0S 在 0.75 / 78.375 / 78.75 / 141.375。不要用带延迟的 1.6640625 当变速点。

    min_gap > 0 时做「变速降噪」：间隔小于 min_gap 秒（按槽计）的连续变速点
    聚成一簇，每簇取多数档。
    """
    buckets: dict[int | float, list] = {}
    use_slots = bpm is not None and float(bpm) > 0
    for n in joined:
        if not _is_playable_speed(n):
            continue
        key: int | float = conv.slot_index(n["tick"], bpm) if use_slots else round(n["tick"], 6)
        buckets.setdefault(key, []).append(n)
    trans = []
    last = 1
    for key in sorted(buckets):
        notes = buckets[key]
        rate = _pick_bucket_rate(notes)
        t = conv.snap_to_slots(min(x["tick"] for x in notes), bpm) if use_slots else min(
            x["tick"] for x in notes)
        if rate != last:
            trans.append((t, rate))
            last = rate

    if min_gap <= 0:
        return trans

    gap_slots = 0
    if use_slots:
        meas = 240.0 / float(bpm)
        slot_dur = meas / conv.SLOTS
        gap_slots = max(1, int(round(min_gap / slot_dur)))

    clusters: list[tuple[float, list[int]]] = []
    prev_t: float | None = None
    prev_slot: int | None = None
    for t, rate in trans:
        if use_slots:
            sl = conv.slot_index(t, bpm)
            join = prev_slot is not None and sl - prev_slot < gap_slots
            prev_slot = sl
        else:
            join = prev_t is not None and t - prev_t < min_gap
        if clusters and join:
            clusters[-1][1].append(rate)
        else:
            clusters.append((t, [rate]))
        prev_t = t

    result: list[tuple[float, int]] = []
    for start, rates in clusters:
        counts = Counter(rates)
        rate = min(counts, key=lambda r: (-counts[r], r))
        result.append((start, rate))

    dedup: list[tuple[float, int]] = []
    for t, rate in result:
        if dedup and dedup[-1][1] == rate:
            continue
        dedup.append((t, rate))
    return dedup


def approach_at(trans, t: float, bpm: float | None = None) -> int:
    result = 1
    use_slots = bpm is not None and float(bpm) > 0
    t_slot = conv.slot_index(t, bpm) if use_slots else None
    for start, rate in trans:
        if use_slots:
            if conv.slot_index(start, bpm) <= t_slot:
                result = rate
            else:
                break
        elif start <= t:
            result = rate
        else:
            break
    return result


def speed_overrides(joined: list[dict], trans=None, bpm: float | None = None) -> list[dict]:
    """相对全局 0S 仍不同的键 → chart.json 覆盖（编辑器回读用）。"""
    trans = list(trans if trans is not None else speed_transitions(joined, bpm=bpm))
    rows = []
    for n in joined:
        if not _is_playable_speed(n):
            continue
        rate = _clamp_speed(n)
        if rate == approach_at(trans, n["tick"], bpm):
            continue
        rows.append({
            "timeSec": n["tick"],
            "lengthSec": n.get("length") or 0,
            "pathway": 1 if n.get("air") else 0,
            "bmsId": n["bms_id"],
            "kind": "note",
            "approachRate": rate,
            "approachOverride": True,
        })
    return rows


def _inside_hold(t: float, air: bool, occupied: list[tuple], eps: float = 0.001) -> bool:
    for a, b, ha in occupied:
        if ha != air:
            continue
        if a - eps <= t <= b + eps:
            return True
    return False


def _merge_holds(holds: list[tuple]) -> list[tuple]:
    """同一轨道上被更长条包住的短长按丢掉（中间刻误带 length 时）。"""
    if len(holds) <= 1:
        return holds
    ordered = sorted(holds, key=lambda h: (h[2], h[0], -(h[1] - h[0])))
    kept = []
    for t0, t1, air, bms in ordered:
        skip = False
        for a, b, ha, hb in kept:
            if ha != air or hb != bms:
                continue
            if t0 >= a - 0.001 and t1 <= b + 0.001:
                skip = True
                break
        if not skip:
            kept.append((t0, t1, air, bms))
    kept.sort(key=lambda h: (h[0], h[2]))
    return kept


def _export_clip(clip, dest_wav):
    samples = getattr(clip, "samples", None)
    if not samples:
        return None
    if isinstance(samples, dict):
        data = next(iter(samples.values()), None)
    else:
        data = samples
    if not data:
        return None
    os.makedirs(os.path.dirname(dest_wav) or ".", exist_ok=True)
    with open(dest_wav, "wb") as f:
        f.write(data if isinstance(data, (bytes, bytearray)) else bytes(data))
    return dest_wav


def load_music_bundle(path: str, templates: dict, capture_delay: float | None = None):
    import UnityPy
    env = UnityPy.load(path)
    maps = {}
    audio = None
    audio_name = ""
    meta = {"bpm": 120.0, "scene": "scene_01", "music": "", "name": ""}
    for o in env.objects:
        if o.type.name == "AudioClip":
            d = o.read()
            audio = d
            audio_name = getattr(d, "m_Name", "") or "music"
            continue
        if o.type.name != "MonoBehaviour":
            continue
        d = o.read()
        name = getattr(d, "m_Name", "") or ""
        if "_map" not in name:
            continue
        sb = md.get_serialized_bytes(d)
        raw_notes = parse_music_data(sb) if sb else []
        joined = [join_template(n, templates) for n in raw_notes]
        try:
            bpm = float(d.bpm)
        except Exception:
            bpm = 120.0
        try:
            diff = int(d.difficulty)
        except Exception:
            diff = 1
        delay = parse_delay(sb) if sb else 0.0
        maps[name] = {
            "bpm": bpm,
            "difficulty": diff,
            "delay": delay,
            "md5": getattr(d, "md5", "") or "",
            "music": getattr(d, "music", "") or "",
            "scene": getattr(d, "scene", "") or "scene_01",
            "notes": joined,
        }
        meta["bpm"] = bpm
        meta["scene"] = maps[name]["scene"]
        meta["music"] = maps[name]["music"]
        meta["name"] = name.split("_map")[0]
    resolved = resolve_capture_delay(maps, capture_delay)
    for data in maps.values():
        snap_joined_to_192(data["notes"], data["bpm"], resolved)
    return {
        "maps": maps, "audio": audio, "audio_name": audio_name, "meta": meta,
        "capture_delay": resolved,
    }


def resolve_capture_delay(maps: dict, capture_delay=None) -> float:
    bpm = 120.0
    for data in maps.values():
        if data.get("bpm"):
            bpm = data["bpm"]
            break
    if capture_delay is not None:
        raw = str(capture_delay).strip().replace(" ", "")
        if raw.endswith("格") or raw.endswith("槽"):
            n = int(raw[:-1])
            return conv.snap_delay_to_slots(n * conv.slot_duration(bpm), bpm)
        return conv.snap_delay_to_slots(max(0.0, float(capture_delay)), bpm)
    ticks = []
    for data in maps.values():
        bpm = data.get("bpm") or bpm
        for n in data.get("notes") or []:
            t = n.get("tick")
            if t is None:
                continue
            # 先吸到 192 再检索 D，避免 0.35 这种两位小数比整格 0.3515625 更小而导致不能减
            ticks.append(conv.snap_to_slots(float(t), bpm))
    extra = []
    cached = load_cached_capture_delay()
    if cached is not None:
        extra.append(cached)
    found = estimate_capture_delay(ticks, bpm, extra=extra)
    if found > 0:
        save_cached_capture_delay(found)
    return found


def find_song_bundle(music_bundle_path: str) -> str | None:
    folder = os.path.dirname(music_bundle_path)
    base = os.path.basename(music_bundle_path)
    m = re.match(r"music_(.+)_assets_all\.bundle$", base, re.I)
    if not m:
        return None
    key = m.group(1)
    hits = glob.glob(os.path.join(folder, f"song_{key}_assets_all*.bundle"))
    return hits[0] if hits else None


def resolve_music_bundle(path: str) -> str:
    """song_* 只有封面/试听；谱面在同目录 music_<id>_assets_all.bundle。"""
    path = os.path.normpath(path)
    base = os.path.basename(path)
    m = re.match(r"song_(.+)_assets_all.*\.bundle$", base, re.I)
    if not m:
        return path
    key = m.group(1)
    music = os.path.join(os.path.dirname(path), f"music_{key}_assets_all.bundle")
    if os.path.isfile(music):
        return music
    raise ValueError("这是封面/试听包 song_*，未找到谱面 " + music)


def _take_existing_file(tokens: list[str]) -> tuple[str, list[str]]:
    """拼回带空格的路径（Muse Dash）。取最长且存在的文件。"""
    built = []
    found = ""
    used = 0
    for i, tok in enumerate(tokens):
        built.append(tok)
        cand = " ".join(built)
        if os.path.isfile(cand):
            found = cand
            used = i + 1
    if found:
        return found, tokens[used:]
    return " ".join(built) if built else "", []


def _clean_dir(path: str | None) -> str | None:
    if not path:
        return None
    path = path.strip().strip("\"'").replace('"', "").rstrip("\\/")
    return path or None


def _extract_option(args: list[str], flag: str) -> tuple[list[str], str]:
    out = []
    val = ""
    eq = flag + "="
    i = 0
    while i < len(args):
        a = args[i]
        if a == flag and i + 1 < len(args):
            val = args[i + 1]
            i += 2
            continue
        if a.startswith(eq):
            val = a[len(eq):]
            i += 1
            continue
        out.append(a)
        i += 1
    return out, val


def _peel_maps_from_text(text: str) -> tuple[str | None, str]:
    """cmd 把 导出\\ 的收尾反斜杠吃掉引号后，--maps 会粘在目录字符串里。"""
    maps = ""
    t = text or ""
    for sep in (" --maps=", " --maps ", " --maps"):
        if sep in t:
            left, right = t.split(sep, 1)
            maps = right.strip().strip("\"'")
            t = left
            break
    return _clean_dir(t), maps


def load_song_bundle(path: str | None):
    cover = None
    demo = None
    if not path or not os.path.isfile(path):
        return cover, demo
    import UnityPy
    env = UnityPy.load(path)
    for o in env.objects:
        if o.type.name == "Texture2D":
            d = o.read()
            img = getattr(d, "image", None)
            if img is not None:
                cover = img
        elif o.type.name == "AudioClip":
            demo = o.read()
    return cover, demo


def _map_index(map_name: str, difficulty: int) -> int:
    m = re.search(r"_map(\d)", map_name, re.I)
    if m:
        n = int(m.group(1))
        if 1 <= n <= 5:
            return n
    return difficulty if 1 <= difficulty <= 5 else 1


def _audio_ext(path: str) -> str:
    with open(path, "rb") as f:
        magic = f.read(12)
    if magic[:4] == b"OggS":
        return ".ogg"
    if magic[:3] == b"ID3" or magic[:2] in (b"\xff\xfb", b"\xff\xf3", b"\xff\xf2"):
        return ".mp3"
    return ".wav"


def _find_ffmpeg() -> str | None:
    env = os.environ.get("FFMPEG") or ""
    if env and os.path.isfile(env):
        return os.path.abspath(env)
    here = os.path.join(SCRIPT_DIR, "ffmpeg.exe")
    if os.path.isfile(here):
        return os.path.abspath(here)
    found = _paths.ffmpeg_exe()
    return os.path.abspath(found) if found else None


EDITOR_DIR = _paths.editor_root()


def _encode_temp_dir(fallback: str) -> str:
    """%TEMP% 下 ffmpeg 常被拒写；默认放编辑器目录，可用 MUSE_TMP 覆盖。"""
    for cand in (os.environ.get("MUSE_TMP") or "", EDITOR_DIR, fallback):
        if not cand:
            continue
        try:
            os.makedirs(cand, exist_ok=True)
            probe = os.path.join(cand, f".muse_probe_{os.getpid()}")
            with open(probe, "wb"):
                pass
            os.remove(probe)
            return cand
        except OSError:
            continue
    return fallback


def _ensure_mdm_audio(src_path: str | None, folder: str, stem: str) -> str | None:
    """转成 CustomAlbums 认的 music.ogg / demo.ogg（各一份）。"""
    if not src_path or not os.path.isfile(src_path):
        return None
    ext = os.path.splitext(src_path)[1].lower()
    dest = os.path.join(folder, stem + ".ogg")
    if ext == ".ogg":
        if os.path.abspath(src_path) != os.path.abspath(dest):
            import shutil
            shutil.copy2(src_path, dest)
        return dest
    if ext == ".mp3":
        dest_mp3 = os.path.join(folder, stem + ".mp3")
        if os.path.abspath(src_path) != os.path.abspath(dest_mp3):
            import shutil
            shutil.copy2(src_path, dest_mp3)
        return dest_mp3
    ff = _find_ffmpeg()
    if not ff:
        print("  [提示] 无 ffmpeg，正曲/试听仍是 WAV，未打进 MDM（编辑器要 ogg/mp3）")
        return None
    tmp_dir = _encode_temp_dir(folder)
    tmp_out = os.path.join(tmp_dir, f"muse_{stem}_{os.getpid()}.ogg")
    cmd = [ff, "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
           "-i", src_path, "-vn", "-c:a", "libvorbis", "-q:a", "6", tmp_out]
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0 or not os.path.isfile(tmp_out) or os.path.getsize(tmp_out) < 64:
        print("  [提示] ffmpeg 转 ogg 失败:", (r.stderr or r.stdout or "")[:240])
        try:
            os.remove(tmp_out)
        except OSError:
            pass
        return None
    import shutil
    try:
        if os.path.isfile(dest):
            os.remove(dest)
        shutil.move(tmp_out, dest)
    except OSError:
        shutil.copy2(tmp_out, dest)
        try:
            os.remove(tmp_out)
        except OSError:
            pass
    try:
        if src_path.lower().endswith(".wav"):
            os.remove(src_path)
    except OSError:
        pass
    return dest if os.path.isfile(dest) else None


def parse_map_selection(text: str, available: set[int]) -> list[int]:
    raw = (text or "").strip().lower()
    if raw in ("", "all", "*", "a"):
        return sorted(available)
    nums = []
    for part in re.split(r"[,\s;]+", raw):
        if not part:
            continue
        n = int(part)
        if n not in available:
            raise ValueError(f"没有 map{n}，可选: {sorted(available)}")
        if n not in nums:
            nums.append(n)
    if not nums:
        raise ValueError("未选择任何 map")
    return nums


def catalog_maps(album: dict) -> list[dict]:
    rows = []
    for map_name, data in sorted(album["maps"].items()):
        idx = _map_index(map_name, data["difficulty"])
        rows.append({
            "index": idx,
            "name": map_name,
            "difficulty": data["difficulty"],
            "notes": len(data["notes"]),
            "bpm": data["bpm"],
            "scene": data["scene"],
            "delay": data.get("delay") or 0.0,
        })
    rows.sort(key=lambda r: r["index"])
    return rows


def load_album(music_bundle: str, bundle_dir: str | None = None,
               capture_delay: float | None = None):
    music_bundle = resolve_music_bundle(music_bundle)
    bundle_dir = bundle_dir or os.path.dirname(os.path.abspath(music_bundle))
    templates = load_notedata(bundle_dir)
    album = load_music_bundle(music_bundle, templates, capture_delay)
    if not album["maps"]:
        raise ValueError("bundle 里没有 *_mapN StageInfo: " + music_bundle)
    song_path = find_song_bundle(music_bundle)
    cover, demo = load_song_bundle(song_path)
    album["song_bundle"] = song_path
    album["cover"] = cover
    album["demo"] = demo
    album["catalog"] = catalog_maps(album)
    cat = md.load_official_catalog(bundle_dir)
    song_id = album["meta"]["name"] or md._song_key({"music": album["meta"].get("music") or ""})
    album["music_info"] = lookup_music_info(cat, song_id, album["meta"].get("music") or "")
    mi = album["music_info"]
    if mi:
        album["meta"]["display_name"] = _s(mi, "name")
        album["meta"]["author"] = _s(mi, "author")
    else:
        album["meta"]["display_name"] = ""
        album["meta"]["author"] = ""
    return album


def print_catalog(album: dict):
    meta = album["meta"]
    mi = album.get("music_info") or {}
    shown = meta.get("display_name") or meta["name"] or meta["music"]
    author = meta.get("author") or ""
    print(f"曲目 {shown}  ({meta['name'] or meta['music']})  {author}")
    print(f"bpm={meta['bpm']:g}  scene={meta['scene']}  unlock={_s(mi, 'unlockLevel', default='0')}")
    cd = album.get("capture_delay")
    if cd is not None:
        print(
            "检索设备延迟="
            + format_capture_delay(cd, float(meta.get("bpm") or 120))
            + "（事件/0S/键已扣；--capture-delay 可强制秒或 N格）"
        )
    print(f"正曲音频: {album.get('audio_name') or '(无)'}")
    print(f"封面/试听: {album.get('song_bundle') or '(无 song_* bundle)'}")
    print("谱面:")
    for row in album["catalog"]:
        star = _star_of(mi, row["index"]) if mi else str(row["difficulty"])
        print(f"  [{row['index']}] {row['name']}  ★{star}  notes={row['notes']}")
    print("回车=全部合并为一个 MDM；例如 1  或  1,3")


def _write_clip(clip, folder: str, stem: str) -> str | None:
    if clip is None:
        return None
    tmp = os.path.join(folder, stem + ".bin")
    if not _export_clip(clip, tmp) or os.path.getsize(tmp) < 16:
        try:
            os.remove(tmp)
        except OSError:
            pass
        return None
    ext = _audio_ext(tmp)
    dest = os.path.join(folder, stem + ext)
    if os.path.exists(dest):
        os.remove(dest)
    os.replace(tmp, dest)
    return dest


def export_song_folder(music_bundle: str, parent_dir: str | None = None,
                       maps: list[int] | None = None, bundle_dir: str | None = None,
                       denoise_beats: float = 0.0,
                       capture_delay: float | None = None):
    """解析 music_* 后写出曲目文件夹，并把封面/正曲/试听归进同一个 MDM。"""
    album = load_album(music_bundle, bundle_dir, capture_delay)
    catalog = album["catalog"]
    available = {r["index"] for r in catalog}
    chosen = sorted(maps) if maps else sorted(available)
    missing = [n for n in chosen if n not in available]
    if missing:
        raise ValueError(f"没有 map{missing}，可选 {sorted(available)}")

    meta = album["meta"]
    mi = album.get("music_info") or {}
    song = meta["name"] or meta["music"] or "song"
    parent = _clean_dir(parent_dir) or os.path.dirname(os.path.abspath(music_bundle))
    folder = os.path.join(parent, song)
    os.makedirs(folder, exist_ok=True)

    by_index = {}
    for map_name, data in album["maps"].items():
        by_index[_map_index(map_name, data["difficulty"])] = (map_name, data)

    diffs = ["0"] * 5
    designers = [""] * 5
    bms_files = {}
    charts = {}
    stats = []
    delay = 0.0
    author = _s(mi, "author")
    bpm_meta = meta.get("bpm") or 120
    confirm_stars = album_type7_star_keys(album)
    for idx in chosen:
        map_name, data = by_index[idx]
        taps, holds, events, dropped_0f, hold_stars = notes_to_taps_holds(
            data["notes"], bpm=float(data["bpm"] or 120), confirm_stars=confirm_stars)
        min_gap = denoise_beats * (60.0 / float(data["bpm"] or 120)) if denoise_beats > 0 else 0.0
        speeds = speed_transitions(data["notes"], min_gap, bpm=float(data["bpm"] or 120))
        overrides = speed_overrides(data["notes"], speeds, bpm=float(data["bpm"] or 120))
        play_level = _star_of(mi, idx) if mi else str(data["difficulty"] if data["difficulty"] > 0 else 1)
        diffs[idx - 1] = play_level
        designers[idx - 1] = _designer_of(mi, idx)
        delay = conv.snap_delay_to_slots(data.get("delay") or 0.0, data.get("bpm") or 120)
        mmeta = {
            "bpm": data["bpm"],
            "scene": data["scene"] or _s(mi, "scene") or "scene_01",
            "map_name": map_name,
            "composer": author or "Unknown",
            "mapper": designers[idx - 1] or "Unknown",
        }
        bms_files[f"map{idx}.bms"] = conv.write_bms(
            mmeta, taps, holds, [{"time_sec": 0, "bpm": data["bpm"]}], play_level, events,
            speeds=speeds)
        if overrides:
            charts[str(idx)] = {"notes": overrides}
        bpm_meta = data["bpm"] or bpm_meta
        confirm_times = {k[0] for k in confirm_stars}
        aligned = sum(
            1 for t, _air, bid in taps
            if bid == "24" and round(conv.snap_to_slots(t, float(data["bpm"] or 120)), 4) in confirm_times)
        stats.append({"map": map_name, "index": idx, "taps": len(taps), "holds": len(holds),
                      "events": len(events), "raw": len(data["notes"]), "star": play_level,
                      "approachOverrides": len(overrides), "droppedHoldTaps": dropped_0f,
                      "holdStars": hold_stars, "star24Align": aligned})

    # 键时间已减抓取 D。StageInfo.delay 仍是吃音。D>0 且吃音为 0 时写回 info.delay，
    # 否则 ogg 比键晚，飞近看起来像判定帧被砍短。
    cd = conv.snap_delay_to_slots(album.get("capture_delay") or 0.0, bpm_meta)
    if abs(delay) < 1e-12 and cd > 0:
        delay = cd

    info = build_info_json(song, meta, mi, diffs, designers, delay)
    info_path = os.path.join(folder, "info.json")
    with open(info_path, "w", encoding="utf-8") as f:
        json.dump(info, f, ensure_ascii=False, indent=2)
        f.write("\n")
    for name, text in bms_files.items():
        with open(os.path.join(folder, name), "w", encoding="utf-8", newline="") as f:
            f.write(text)

    project = {
        "schema": "md-chart-p0/1",
        "meta": {
            "name": song,
            "author": author or "",
            "bpm": str(bpm_meta),
            "scene": _s(mi, "scene") or meta.get("scene") or "scene_01",
        },
        "charts": charts,
    }
    project_path = os.path.join(folder, "chart.json")
    if charts:
        with open(project_path, "w", encoding="utf-8") as f:
            json.dump(project, f, ensure_ascii=False, indent=2)
            f.write("\n")
    elif os.path.isfile(project_path):
        os.remove(project_path)

    assets = {}
    if album.get("cover") is not None:
        cover_path = os.path.join(folder, "cover.png")
        album["cover"].save(cover_path)
        assets["cover"] = cover_path
    raw_music = _write_clip(album.get("audio"), folder, "music")
    raw_demo = _write_clip(album.get("demo"), folder, "demo")
    assets["music"] = _ensure_mdm_audio(raw_music, folder, "music")
    assets["demo"] = _ensure_mdm_audio(raw_demo, folder, "demo")

    mdm_path = os.path.join(folder, song + ".mdm")
    tmp = mdm_path + ".tmp"
    pack_names = {"cover.png", "cover.gif", "music.ogg", "music.mp3", "demo.ogg", "demo.mp3"}
    with zipfile.ZipFile(tmp, "w", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr("info.json", json.dumps(info, ensure_ascii=False, indent=2))
        if charts:
            z.writestr("chart.json", json.dumps(project, ensure_ascii=False, indent=2))
        for name, text in bms_files.items():
            z.writestr(name, text)
        for key in ("cover", "music", "demo"):
            path = assets.get(key)
            if not path or not os.path.isfile(path):
                continue
            arc = os.path.basename(path)
            if arc.lower() in pack_names:
                z.write(path, arc)
    os.replace(tmp, mdm_path)
    return folder, {
        "mdm": mdm_path,
        "folder": folder,
        "song_bundle": album.get("song_bundle"),
        "info": info,
        "maps": stats,
        "type7Stars": len(confirm_stars),
        "assets": {k: v for k, v in assets.items() if v},
    }


def pack_official_mdm(music_bundle: str, dest_mdm: str | None = None, bundle_dir: str | None = None,
                      maps: list[int] | None = None):
    parent = None
    if dest_mdm:
        dest_mdm = os.path.abspath(dest_mdm)
        if dest_mdm.lower().endswith(".mdm"):
            parent = os.path.dirname(dest_mdm)
        else:
            parent = dest_mdm
    folder, info = export_song_folder(music_bundle, parent, maps, bundle_dir)
    if dest_mdm and dest_mdm.lower().endswith(".mdm"):
        target_dir = os.path.dirname(dest_mdm)
        os.makedirs(target_dir or ".", exist_ok=True)
        if os.path.abspath(info["mdm"]) != os.path.abspath(dest_mdm):
            # 仍放在曲目文件夹内，不把 mdm 挪出文件夹
            pass
    return info["mdm"], info


def main(argv):
    if len(argv) < 2 or argv[1] in ("-h", "--help"):
        print(__doc__)
        print("用法:")
        print("  python official_to_mdm.py list <music_*.bundle|song_*.bundle>")
        print("  python official_to_mdm.py export <bundle> [输出父目录] [--maps 1,3] [--denoise 2] [--capture-delay auto|0|45格|0.3515625]")
        print("  python official_to_mdm.py <bundle> [输出父目录]   # 等同 export 全部 map")
        print("--denoise 后跟拍数：把间隔小于该拍数的变速点合并，压掉 Hidden 谱的快速闪烁。")
        print("--capture-delay 默认 auto：D=n×(240/BPM/192)。秒会吸到整格（0.35@160→45格=0.3515625s）。0=不扣。")
        print("路径含空格请加引号；漏引号时会尝试拼回 Muse Dash。song_* 会改走同曲 music_*。")
        return 0
    args = argv[1:]
    args, maps_text = _extract_option(args, "--maps")
    args, out_text = _extract_option(args, "--out")
    args, denoise_text = _extract_option(args, "--denoise")
    args, capture_text = _extract_option(args, "--capture-delay")
    capture_delay = None
    raw = (capture_text or "").strip()
    raw_l = raw.lower()
    if raw_l in ("", "auto", "search"):
        capture_delay = None
    elif raw.endswith("格") or raw.endswith("槽"):
        capture_delay = raw.replace(" ", "")
    else:
        try:
            capture_delay = float(raw)
        except ValueError:
            print(f"无法解析 --capture-delay 值 {capture_text!r}，改自动检索")
            capture_delay = None
    cmd = args[0] if args else ""
    if cmd in ("list", "export"):
        tokens = args[1:]
    else:
        cmd = "export"
        tokens = args
    src, rest = _take_existing_file(tokens)
    if cmd == "list":
        if not src or not os.path.isfile(src):
            print("需要存在的 music_*.bundle 或 song_*.bundle（Muse Dash 路径请加引号）")
            print("当前拼到:", src or "(空)")
            return 2
        album = load_album(src, capture_delay=capture_delay)
        print_catalog(album)
        return 0
    parent = out_text or (" ".join(rest) if rest else None)
    parent, glued = _peel_maps_from_text(parent or "")
    if glued and not maps_text:
        maps_text = glued
    if not src or not os.path.isfile(src):
        print("需要存在的 music_*.bundle 或 song_*.bundle（Muse Dash 路径请加引号）")
        print("当前拼到:", src or "(空)")
        return 2
    src = resolve_music_bundle(src)
    album = load_album(src, capture_delay=capture_delay)
    print_catalog(album)
    available = {r["index"] for r in album["catalog"]}
    chosen = parse_map_selection(maps_text, available)
    try:
        denoise_beats = float(denoise_text) if denoise_text else 0.0
    except ValueError:
        print(f"无法解析 --denoise 值 {denoise_text!r}，按 0（不降噪）处理")
        denoise_beats = 0.0
    print(
        "导出 map", chosen, "变速降噪拍数", denoise_beats,
        "检索延迟", format_capture_delay(
            album.get("capture_delay") or 0, float(album["meta"].get("bpm") or 120)))
    folder, info = export_song_folder(
        src, parent, chosen, denoise_beats=denoise_beats, capture_delay=capture_delay)
    print("曲目文件夹", folder)
    print("MDM", info["mdm"])
    print("跨难度 type7 23", info.get("type7Stars") or 0, "个（只统计，1 槽 0F 撞键才丢）")
    for k, v in info["assets"].items():
        print(" ", k, v)
    for m in info["maps"]:
        extra = ""
        if m.get("holdStars"):
            extra += f"  star24={m['holdStars']}"
            if m.get("star24Align"):
                extra += f"/{m['star24Align']}对表"
        if m.get("droppedHoldTaps"):
            extra += f"  drop0F={m['droppedHoldTaps']}"
        print(f"  {m['map']} -> map{m['index']}.bms ★{m.get('star')} tap={m['taps']} hold={m['holds']} event={m.get('events', 0)} raw={m['raw']}" + extra)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
