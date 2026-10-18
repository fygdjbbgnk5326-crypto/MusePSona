# -*- coding: utf-8 -*-
"""从本机 gamescene_XX 抽出对局背景分层（scene_XX 预制：Bg/Near/Mid/Far + Animator 循环平移）。

只读游戏文件；输出 sprites/scene_XX/bg/（gitignore，不进 MDM）。
默认局只抽原版激活物件；联动换装跳过。scene_03 包内常激活 *_hexie，官机美术用旁边关掉的 *_default。
"""
import argparse
import glob
import io
import json
import math
import os
import re
import struct
import sys
import zlib
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import _paths  # noqa: E402
import UnityPy

DEFAULT_OUT = os.path.join(os.path.dirname(HERE), "PlayClient", "Unity", "Assets", "StreamingAssets", "MdLooks")
SKIP_NAMES = {
    "HitPointAir", "HitPointRoad", "HitPointAirIn", "HitPointRoadIn",
    "HitPointAirOut", "HitPointRoadOut", "HitPointInCatch", "HitPointOutTriangle",
}
COLLAB_RE = re.compile(
    r"cytus|_cy\d|_cy[^a-z]|touhou|wacca|arknight|folivora|ghost_cy|"
    r"well\d_leaf|mountain\d+_cytus|_ba_|girl_wacca|leaf_girl|leaf_soldier|"
    r"hexie|sceneegg|mergesceneegg",
    re.I,
)


def find_bundle(bdir, prefix):
    hits = sorted(glob.glob(os.path.join(bdir, prefix + "_assets_all_*.bundle")))
    if not hits:
        raise FileNotFoundError(f"未找到 {prefix}_assets_all_*.bundle")
    return hits[0]


def v3(p):
    return (float(p.x), float(p.y), float(p.z))


def local_euler_z(tr):
    """2D 精灵几乎都是绕 Z。far_rocket 官机约 +75°，贴图仍是竖条，靠四边形旋转。"""
    try:
        q = tr.m_LocalRotation
        x, y, z, w = float(q.x), float(q.y), float(q.z), float(q.w)
    except Exception:
        return 0.0
    if abs(x) < 1e-3 and abs(y) < 1e-3:
        # w=0 是绕 Z 转 180°（WellArrowA/C 人字纹列）。不要改成 w=1，否则会变成 90°，箭头被摊成横排。
        return math.degrees(2.0 * math.atan2(z, w))
    siny = 2.0 * (w * z + x * y)
    cosy = 1.0 - 2.0 * (y * y + z * z)
    return math.degrees(math.atan2(siny, cosy))


def comps(go):
    out = []
    for c in go.m_Components:
        try:
            out.append((c, c.read()))
        except Exception:
            pass
    return out


def transform_of(go):
    # WellArrowB 的内层是 RectTransform，只认 Transform 会丢掉中间那列人字纹。
    hit = None
    for _, o in comps(go):
        n = o.__class__.__name__
        if n == "Transform":
            return o
        if n == "RectTransform" and hit is None:
            hit = o
    return hit


def skip_name(name):
    # 不要按名字丢掉 arrow：scene_01 墙边人字纹是 WellArrow 下 60 个 s01_arrow。
    # 判定线箭头在 HitPoint* 下，整棵已经跳过。
    if name in SKIP_NAMES:
        return True
    if name.startswith("HitPoint"):
        return True
    # 对局不要站立初音切图（s10_miku），舞台背景 s10_stage_miku_2 仍保留。
    if name == "s10_miku":
        return True
    return False


def is_collab(name):
    return bool(name) and COLLAB_RE.search(name) is not None


def is_variant(name):
    """联动换装 / 和谐替图名跳过。scene_03 要抽关掉的 *_default，见 drive_uncensored。"""
    if not name:
        return False
    if is_collab(name):
        return True
    if re.search(r"_(Arknights|Touhou|Wacca|Cytus)\d*$", name, re.I):
        return True
    return False


def is_hexie_name(name):
    return bool(name) and "hexie" in name.lower()


def default_sibling(go, siblings):
    n = go.m_Name or ""
    if not is_hexie_name(n):
        return None
    want = n.lower().replace("_hexie", "_default")
    for o in siblings:
        if o is go:
            continue
        if (o.m_Name or "").lower() == want:
            return o
    return None


def drive_uncensored(parent, go):
    """包内 Floor_hexie 常是激活的；官机井/骷髅走 Floor_default / Well4_default。"""
    n = go.m_Name or ""
    if is_hexie_name(n):
        return False
    if bool(getattr(go, "m_IsActive", True)):
        return True
    nl = n.lower()
    if not nl.endswith("_default"):
        return False
    hexn = n[:-8] + "_hexie"
    for ch in child_gos(parent):
        if (ch.m_Name or "") == hexn and bool(getattr(ch, "m_IsActive", True)):
            return True
    return False


def clip_root_x_span(clip):
    """StreamedClip 关键帧：coeff[3] 才是位移值。乱解 uint 会把斜率当成循环宽。"""
    try:
        raw = clip.m_MuscleClip.m_Clip.data.m_StreamedClip.data or []
    except Exception:
        return 0.0
    buf = b"".join(struct.pack("<I", u & 0xFFFFFFFF) for u in raw)
    r = io.BytesIO(buf)
    n = len(buf)
    xs = []
    while r.tell() + 8 <= n:
        t = struct.unpack("<f", r.read(4))[0]
        count = struct.unpack("<i", r.read(4))[0]
        if count < 0 or count > 64:
            break
        xval = None
        for _ in range(count):
            if r.tell() + 20 > n:
                return abs(xs[-1] - xs[0]) if len(xs) >= 2 else 0.0
            idx = struct.unpack("<i", r.read(4))[0]
            coeff = struct.unpack("<4f", r.read(16))
            if idx == 0:
                xval = coeff[3]
        if xval is not None and t == t and abs(t) < 1e6:
            xs.append(xval)
    if len(xs) < 2:
        return 0.0
    end = abs(xs[-1] - xs[0])
    span = max(xs) - min(xs)
    # 单向滚到终点再 LoopTime 折回：首尾差就是循环宽。
    # 往返再回到起点时首尾差是 0，行程仍是循环宽（scene_08 Mid_B_1 约 66）。
    # 小于 2 的往返是上下/左右晃，不当成横移。
    if end >= 0.05:
        return end
    if span > 2.0:
        return span
    return 0.0


def stream_xy_keys(clip):
    """Animator 本地位置关键帧 (t, x, y)。跳过无穷默认帧。"""
    try:
        raw = clip.m_MuscleClip.m_Clip.data.m_StreamedClip.data or []
    except Exception:
        return []
    buf = b"".join(struct.pack("<I", u & 0xFFFFFFFF) for u in raw)
    r = io.BytesIO(buf)
    n = len(buf)
    keys = []
    while r.tell() + 8 <= n:
        t = struct.unpack("<f", r.read(4))[0]
        count = struct.unpack("<i", r.read(4))[0]
        if count < 0 or count > 64:
            break
        xval = yval = None
        for _ in range(count):
            if r.tell() + 20 > n:
                return keys
            idx = struct.unpack("<i", r.read(4))[0]
            coeff = struct.unpack("<4f", r.read(16))
            if idx == 0:
                xval = coeff[3]
            elif idx == 1:
                yval = coeff[3]
        if t == t and abs(t) < 1e6 and (xval is not None or yval is not None):
            keys.append((t, xval, yval))
    keys.sort(key=lambda k: k[0])
    return keys


def clip_stream(clip):
    """流式曲线：下标 → [(t, 值)]。"""
    try:
        raw = clip.m_MuscleClip.m_Clip.data.m_StreamedClip.data or []
    except Exception:
        return {}
    buf = b"".join(struct.pack("<I", u & 0xFFFFFFFF) for u in raw)
    r = io.BytesIO(buf)
    n = len(buf)
    series = {}
    while r.tell() + 8 <= n:
        t = struct.unpack("<f", r.read(4))[0]
        count = struct.unpack("<i", r.read(4))[0]
        if count < 0 or count > 64:
            break
        for _ in range(count):
            if r.tell() + 20 > n:
                return series
            idx = struct.unpack("<i", r.read(4))[0]
            coeff = struct.unpack("<4f", r.read(16))
            if t == t and abs(t) < 1e6:
                series.setdefault(idx, []).append((t, coeff[3]))
    return series


def clip_bindings(clip):
    try:
        tree = clip.object_reader.read_typetree()
        return tree["m_ClipBindingConstant"]["genericBindings"] or []
    except Exception:
        return []


def name_hash(name):
    return zlib.crc32((name or "").encode("utf-8")) & 0xFFFFFFFF


# 远光 Z 旋转。path 哈希 → 1 起的 spinId。抽一场之前清空。
_SPIN = {}
_SPIN_CURVES = []
_CLIP_POS = {}


def reset_spins():
    global _SPIN, _SPIN_CURVES, _CLIP_POS
    _SPIN = {}
    _SPIN_CURVES = []
    _CLIP_POS = {}


def _anim_child_paths(go, prefix=""):
    out = []
    for ch in child_gos(go):
        p = (prefix + "/" + (ch.m_Name or "")) if prefix else (ch.m_Name or "")
        out.append((ch, p, name_hash(p)))
        out.extend(_anim_child_paths(ch, p))
    return out


def _thin_spin_keys(ts, zs, max_keys=24):
    n = min(len(ts), len(zs))
    if n <= max_keys:
        return [round(ts[i], 4) for i in range(n)], [round(zs[i], 4) for i in range(n)]
    step = max(1, (n - 1) // (max_keys - 1))
    idx = list(range(0, n, step))
    if idx[-1] != n - 1:
        idx.append(n - 1)
    return [round(ts[i], 4) for i in idx], [round(zs[i], 4) for i in idx]


def note_light_spins(go):
    """s07_farlight / scene_10 SpotLightAnim：曲线在灯的父节点上，不在精灵上。"""
    nm = go.m_Name or ""
    nml = nm.lower().replace(" ", "")
    if not (nm.startswith("Farlight") or nml.startswith("spotlight") or nml.startswith("animspotlight")):
        return
    clip = animator_clip(go)
    if clip is None:
        return
    by_hash = {h: (ch, p) for ch, p, h in _anim_child_paths(go)}
    series = clip_stream(clip)
    ei = 0
    for b in clip_bindings(clip):
        if int(b.get("attribute") or 0) != 4:
            continue
        zs = series.get(ei * 3 + 2) or []
        ei += 1
        path = int(b.get("path") or 0)
        if path in _SPIN or len(zs) < 2:
            continue
        vals = [z for _, z in zs]
        if max(vals) - min(vals) < 1.0:
            continue
        period = round(zs[-1][0], 4)
        z0 = vals[0]
        reused = None
        # scene_10 各 SpotLight 必须独占曲线：Light1/Light6 开局角都是 −54，
        # 合用一条 spin 的话翻其中一盏另一盏必反。
        share = not nml.startswith("spotlight")
        if share:
            for i, c in enumerate(_SPIN_CURVES):
                if abs(c["period"] - period) < 0.05 and abs(c["zs"][0] - z0) < 0.6:
                    reused = i + 1
                    break
        if reused is None:
            ts, zz = _thin_spin_keys([t for t, _ in zs], vals)
            _SPIN_CURVES.append({"ts": ts, "zs": zz, "period": period})
            reused = len(_SPIN_CURVES)
        _SPIN[path] = reused
        hit = by_hash.get(path)
        if hit is not None:
            rel = hit[1] or ""
            _SPIN[name_hash(rel)] = reused
            # 直系子节点名（Farlight1）可以当键；SpotLight1 在多簇里复用，
            # 不能全局登记，否则没 Animator 的 SpotLight_E 也会跟着扫。
            if "/" not in rel:
                _SPIN[name_hash(hit[0].m_Name or "")] = reused


def lookup_spin(anim_path, go_name="", parent_name=""):
    """返回 (spinId, 'self'|'parent')。嵌套路径 Haft1/SpotLight1，避免 SpotLight_E 同名灯头串台。"""
    p = anim_path or ""
    segs = p.split("/") if p else []

    def hit(key):
        return _SPIN.get(name_hash(key)) if key else None

    for i in range(len(segs)):
        sub = "/".join(segs[i:])
        sid = hit(sub)
        if sid and go_name and (sub == go_name or sub.endswith("/" + go_name)):
            return sid, "self"
    sid = hit(go_name)
    if sid:
        return sid, "self"
    if len(segs) >= 2:
        up = segs[:-1]
        for i in range(len(up)):
            sid = hit("/".join(up[i:]))
            if sid:
                return sid, "parent"
    sid = hit(parent_name)
    if sid:
        return sid, "parent"
    return 0, ""


def flip_s10_spotlights_h(groups):
    """scene_10 射灯：±35/±55 左正右负；Mid1 镜音 ±15° 左负右正。
    """
    if not _SPIN_CURVES:
        return

    def clone_spin(sid, negate=False):
        if sid <= 0:
            return 0
        c = _SPIN_CURVES[sid - 1]
        zs = [-z for z in c["zs"]] if negate else list(c["zs"])
        _SPIN_CURVES.append({
            "ts": list(c["ts"]),
            "zs": zs,
            "period": c["period"],
        })
        return len(_SPIN_CURVES)

    items = []
    for g in groups:
        items.extend(g.get("items") or [])
    spots = [it for it in items if "spotlight" in (it.get("spr") or "").lower()]
    by_gi = {}
    for it in spots:
        by_gi.setdefault(int(it.get("gi") or 0), []).append(it)
    clusters = []
    for gi_spots in by_gi.values():
        gi_spots.sort(key=lambda it: float(it.get("pivotX") or it.get("x") or 0))
        for it in gi_spots:
            px = float(it.get("pivotX") or it.get("x") or 0)
            if not clusters or clusters[-1]["_gi"] != int(it.get("gi") or 0) or px - clusters[-1]["_max"] > 20:
                clusters.append({"_max": px, "_gi": int(it.get("gi") or 0), "its": [it]})
            else:
                clusters[-1]["its"].append(it)
                clusters[-1]["_max"] = max(clusters[-1]["_max"], px)

    for cl in clusters:
        its = []
        for it in cl["its"]:
            rot = float(it.get("rotZ") or 0)
            sid = int(it.get("spinId") or 0)
            if abs(rot) < 0.5 and sid <= 0:
                continue
            its.append(it)
        if not its:
            continue
        pxs = [float(it.get("pivotX") or it.get("x") or 0) for it in its if int(it.get("orbit") or 0)]
        if not pxs:
            pxs = [float(it.get("pivotX") or it.get("x") or 0) for it in its]
        cx = (min(pxs) + max(pxs)) / 2.0
        for it in its:
            spr = (it.get("spr") or "").lower()
            housing = spr.endswith("_3")
            rot = float(it.get("rotZ") or 0)
            mag = abs(rot)
            sid = int(it.get("spinId") or 0)
            px = float(it.get("pivotX") or it.get("x") or 0)
            left = px < cx
            kagamine = abs(mag - 15.0) < 3.5 and int(it.get("gi") or 0) == 4
            if kagamine:
                new_rot = -mag if left else mag
            else:
                new_rot = mag if left else -mag
            if not housing:
                it["flipX"] = False
            if sid and abs(new_rot - rot) > 0.5:
                it["spinId"] = clone_spin(sid, negate=True)
            if abs(new_rot - rot) < 0.5:
                continue
            py = float(it.get("pivotY") or 0)
            if int(it.get("orbit") or 0) and (abs(float(it.get("pivotX") or 0)) > 0.01 or abs(py) > 0.01):
                length = math.hypot(float(it["x"]) - float(it["pivotX"]), float(it["y"]) - py)
                rad = math.radians(new_rot)
                it["x"] = round(float(it["pivotX"]) - math.sin(rad) * length, 4)
                it["y"] = round(py + math.cos(rad) * length, 4)
            it["rotZ"] = round(new_rot, 3)


def parent_go(go):
    tr = transform_of(go)
    if tr is None:
        return None
    try:
        father = tr.m_Father
        if father is None or int(getattr(father, "m_PathID", 0) or 0) == 0:
            return None
        return father.read().m_GameObject.read()
    except Exception:
        return None


def bound_local_xy(parent, child):
    """父节点 Animator 绑到这个子物体时，用曲线第 0 帧替换 Transform。

    scene_08 的 08_ice 挂在 scene_08 根上，绑 Ice_all：Transform 停在行程末端
    （约 −27），开局帧是 x=16。只用 Transform 的话冰块冻在镜头左边。
    """
    clip = animator_clip(parent)
    if clip is None:
        return None
    h = name_hash(child.m_Name or "")
    series = clip_stream(clip)
    pi = 0
    for b in clip_bindings(clip):
        if int(b.get("attribute") or 0) != 1:
            continue
        path = int(b.get("path") or 0)
        xs = series.get(pi * 3) or []
        ys = series.get(pi * 3 + 1) or []
        pi += 1
        if path != h or not xs:
            continue
        x0 = float(xs[0][1])
        y0 = float(ys[0][1]) if ys else 0.0
        return x0, y0
    return None


def layer_clip_positions(go):
    """层 Animator 绑到子路径的开局本地坐标。路径哈希是相对本层的 'A' 或 'A/B'。

    Transform 经常停在行程中段（scene_08 钟楼父节点 Mid_B_2 写 13.8，开局 61.3）。
    """
    clip = animator_clip(go)
    if clip is None:
        return {}
    series = clip_stream(clip)
    out = {}
    pi = 0
    for b in clip_bindings(clip):
        if int(b.get("attribute") or 0) != 1:
            continue
        path = int(b.get("path") or 0)
        xs = series.get(pi * 3) or []
        ys = series.get(pi * 3 + 1) or []
        pi += 1
        if path == 0 or not xs:
            continue
        x0 = float(xs[0][1])
        y0 = float(ys[0][1]) if ys else 0.0
        if abs(x0 - 1.0) < 0.02 and abs(y0 - 1.0) < 0.02:
            continue
        xs_v = [p[1] for p in xs]
        span = (max(xs_v) - min(xs_v)) if xs_v else 0.0
        # 常值 0 不是姿势（会把 Transform 在 62 的灌木拽回原点）。
        if span < 0.05 and abs(x0) < 0.05 and abs(y0) < 0.05:
            continue
        # 整段皮带已经写在 group.loop。同名双份再套开局帧会叠掉一座神社，
        # 只移动 Mid_B_2 会让钟楼离开自己的门和栅栏，中间露出空档。
        if span > 2.0:
            continue
        out[path] = (x0, y0)
    return out


def sole_sibling(go):
    """同名激活兄弟多于一个时，一条曲线套上去会叠成同一 x（两座 Mid_A_1）。"""
    parent = parent_go(go)
    if parent is None:
        return True
    name = go.m_Name or ""
    n = 0
    for ch in child_gos(parent):
        if (ch.m_Name or "") != name or is_variant(ch.m_Name):
            continue
        if not bool(getattr(ch, "m_IsActive", True)):
            continue
        n += 1
    return n <= 1


def anim_bind(go):
    """Animator 开局帧。返回 (x, y, x 振幅, y 振幅)，缺的轴是 None。"""
    clip = animator_clip(go)
    if clip is None:
        return None
    # SpriteRenderer 颜色/缩放曲线也会进 StreamedClip。idx0 不是 Transform.x。
    # 灯笼光 s12_lantern_light1 只有 typeID 212，当成位移会把光斑挪到灯笼旁边。
    binds = clip_bindings(clip)
    if not any(int(b.get("typeID") or 0) == 4 for b in binds):
        return None
    # 只信绑在自己身上的根位移（path=0）。kagamine / s10_kaito 第一条是子物体，
    # 套到根上会把镜音坐标叠两次、KAITO 被音符曲线抬偏。
    first_pos = None
    for b in binds:
        if int(b.get("attribute") or 0) != 1:
            continue
        first_pos = int(b.get("path") or 0)
        break
    if first_pos not in (None, 0):
        return None
    keys = stream_xy_keys(clip)
    if not keys:
        return None
    _t, x, y = keys[0]
    xs = [k[1] for k in keys if k[1] is not None]
    ys = [k[2] for k in keys if k[2] is not None]
    xspan = (max(xs) - min(xs)) if len(xs) >= 2 else 0.0
    yspan = (max(ys) - min(ys)) if len(ys) >= 2 else 0.0
    return x, y, xspan, yspan


def parse_scroll(go, clip_map=None):
    """Animator 根位移：循环宽度与时长。优先 MuscleClip.m_StopTime。"""
    clip = None
    for _, o in comps(go):
        if o.__class__.__name__ != "Animator":
            continue
        try:
            ctrl = o.m_Controller.read()
            for c in getattr(ctrl, "m_AnimationClips", None) or []:
                try:
                    clip = c.read()
                    break
                except Exception:
                    pass
        except Exception:
            clip = None
        break
    if clip is None and clip_map:
        clip = clip_map.get(go.m_Name or "")
    if clip is None:
        return 0.0, 0.0
    rate = float(getattr(clip, "m_SampleRate", 60) or 60)
    muscle = getattr(clip, "m_MuscleClip", None)
    if muscle is None:
        return 0.0, 0.0
    stop = float(getattr(muscle, "m_StopTime", 0) or 0)
    try:
        dense = muscle.m_Clip.data.m_DenseClip
        frames = int(getattr(dense, "m_FrameCount", 0) or 0)
    except Exception:
        frames = 0
    period = stop if stop > 0.02 else (frames / rate if rate > 0 and frames > 0 else 0.0)
    loop = clip_root_x_span(clip)
    if loop > 160:
        loop = 0.0
    return round(loop, 4), round(period, 4)


def is_layer_anim(go):
    nm = (go.m_Name or "").lower().strip()
    return (
        nm in ("mid", "near", "far", "bg", "default", "floor")
        or "scrollable" in nm
        or nm.startswith("floor")
        or (nm.endswith("_all") and "h7" not in nm and "h5" not in nm)
    )


def animator_clip(go):
    for _, o in comps(go):
        if o.__class__.__name__ != "Animator":
            continue
        try:
            ctrl = o.m_Controller.read()
            for c in getattr(ctrl, "m_AnimationClips", None) or []:
                try:
                    return c.read()
                except Exception:
                    pass
        except Exception:
            return None
        break
    return None


def pop_on_times(clip):
    """Animator 把 inactive 子物体 0→1 打开：弹窗叠层时刻。"""
    if clip is None:
        return [], 0.0
    muscle = getattr(clip, "m_MuscleClip", None)
    if muscle is None:
        return [], 0.0
    period = float(getattr(muscle, "m_StopTime", 0) or 0)
    try:
        raw = muscle.m_Clip.data.m_StreamedClip.data or []
    except Exception:
        return [], period
    buf = b"".join(struct.pack("<I", u & 0xFFFFFFFF) for u in raw)
    r = io.BytesIO(buf)
    n = len(buf)
    start = {}
    first_on = {}
    while r.tell() + 8 <= n:
        t = struct.unpack("<f", r.read(4))[0]
        count = struct.unpack("<i", r.read(4))[0]
        if count < 0 or count > 64:
            break
        for _ in range(count):
            if r.tell() + 20 > n:
                return sorted(first_on.values()), period
            idx = struct.unpack("<i", r.read(4))[0]
            coeff = struct.unpack("<4f", r.read(16))
            val = coeff[3]
            if t != t or abs(t) > 1e6:
                continue
            if idx <= 2:
                continue
            if idx not in start:
                start[idx] = val
            if start[idx] < 0.5 and val >= 0.5 and idx not in first_on:
                first_on[idx] = t
    times = sorted(first_on.values())
    return times, period


def pop_root_xy(clip):
    """弹窗根位移：每弹出一层整组阶跃 (−0.14, +0.14)，第一屏往左上抬，新窗本地 +0.14/−0.14 留在前层。"""
    if clip is None:
        return [], [], [], 0.0
    muscle = getattr(clip, "m_MuscleClip", None)
    if muscle is None:
        return [], [], [], 0.0
    period = float(getattr(muscle, "m_StopTime", 0) or 0)
    try:
        raw = muscle.m_Clip.data.m_StreamedClip.data or []
    except Exception:
        return [], [], [], period
    buf = b"".join(struct.pack("<I", u & 0xFFFFFFFF) for u in raw)
    r = io.BytesIO(buf)
    n = len(buf)
    keys = []
    while r.tell() + 8 <= n:
        t = struct.unpack("<f", r.read(4))[0]
        count = struct.unpack("<i", r.read(4))[0]
        if count < 0 or count > 64:
            break
        x = y = None
        for _ in range(count):
            if r.tell() + 20 > n:
                break
            idx = struct.unpack("<i", r.read(4))[0]
            coeff = struct.unpack("<4f", r.read(16))
            if t != t or abs(t) > 1e6:
                continue
            if idx == 0:
                x = coeff[3]
            elif idx == 1:
                y = coeff[3]
        if x is not None or y is not None:
            keys.append((t, x, y))
    if not keys:
        return [], [], [], period
    # 补全缺轴
    lx, ly = 0.0, 0.0
    ts, xs, ys = [], [], []
    for t, x, y in keys:
        if x is not None:
            lx = x
        if y is not None:
            ly = y
        if ts and abs(t - ts[-1]) < 1e-4:
            xs[-1], ys[-1] = lx, ly
            continue
        ts.append(round(t, 4))
        xs.append(round(lx, 4))
        ys.append(round(ly, 4))
    if period > 0.05 and ts and abs(ts[-1] - period) > 0.05:
        ts.append(round(period, 4))
        xs.append(xs[0])
        ys.append(ys[0])
    return ts, xs, ys, period


def clip_index(env):
    out = {}
    for obj in env.objects:
        if obj.type.name != "AnimationClip":
            continue
        try:
            d = obj.read()
        except Exception:
            continue
        n = d.m_Name or ""
        if n:
            out[n] = d
    return out


def local_motion(go, clip_map, layer_loop=0.0):
    """元件自己的 Animator：浮动 / 闪灯 / 横移 / 竖向循环。层滚动夹不要当成浮动。

    返回 (bob, bobPeriod, blink, scrollX, scrollPeriod, scrollY, scrollYPeriod)。
    scrollY 为负表示往下滚（scene_01 的井列）。
    """
    z = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
    has = False
    for _, o in comps(go):
        if o.__class__.__name__ == "Animator":
            has = True
            break
    if not has:
        return z
    # 不用 clip 名兜底：层名 Bg/Mid 会对上无关 Clip，整层被当成元件动画。
    loop, period = parse_scroll(go, None)
    if period < 0.05:
        period = 1.2
    nm = (go.m_Name or "").lower().strip()
    if nm in ("mid", "near", "far", "bg", "default", "floor") or "scrollable" in nm:
        return z
    clip = animator_clip(go)
    keys = stream_xy_keys(clip) if clip is not None else []
    dx = dy = xspan = 0.0
    if len(keys) >= 2:
        x0, y0 = keys[0][1], keys[0][2]
        x1, y1 = keys[-1][1], keys[-1][2]
        if x0 is not None and x1 is not None:
            dx = x1 - x0
        if y0 is not None and y1 is not None:
            dy = y1 - y0
        xs = [k[1] for k in keys if k[1] is not None]
        if len(xs) >= 2:
            xspan = max(xs) - min(xs)
    # 井列：Y 从起点直线走到终点再跳回，不是 0.12 的正弦漂。
    if abs(dy) >= 1.0 and abs(dx) < 0.5 and period > 0.2:
        return (0.0, 0.0, 0.0, 0.0, 0.0, dy, period)
    # scene_01 人字纹 WellArrow：s01_arrow 0.333s 走 0.18（约一格）。阈值 1.0 会当成 bob 0.12，箭头就不滑了。
    if abs(dy) >= 0.12 and abs(dx) < 0.05 and period > 0.05:
        return (0.0, 0.0, 0.0, 0.0, 0.0, dy, period)
    if any(k in nm for k in ("light", "lamp", "neon", "led", "sign", "screen", "blink", "marquee")):
        # 射灯走 orbit（note_light_spins），不是闪透明度。
        if "spotlight" in nm:
            return z
        # SpriteRenderer.m_Color.a 小幅呼吸（RedLight 0.75→0.55）不是闪灭。播放端正弦会把 alpha 打到 0.35。
        color_a = False
        if clip is not None:
            for b in clip_bindings(clip):
                if int(b.get("typeID") or 0) == 212 and int(b.get("attribute") or 0) == 304273561:
                    color_a = True
                    break
        if color_a:
            return z
        # s03_bpm_light / s06_bpm_light 片内是空曲线，官机亮度钉在 SR alpha。
        # 不要按层名 Light 开 0.35～1 正弦，窗户/洗光会闪成对局灯。
        binds = clip_bindings(clip) if clip is not None else []
        if (not binds) and len(keys) < 2:
            return z
        blink = min(period, 0.8)
        # 灯笼 clip 约 2.17s，不要压成 0.8，否则闪太快。
        if "lantern" in nm and clip is not None:
            muscle = getattr(clip, "m_MuscleClip", None)
            st = float(getattr(muscle, "m_StopTime", 0) or 0) if muscle is not None else 0.0
            if st > 0.2:
                blink = st
        return (0.0, period, blink, 0.0, 0.0, 0.0, 0.0)
    if any(k in nm for k in ("floor", "building", "wall", "roof", "ground", "street", "mountain", "moutain")):
        return z
    # 整组井：X 出去再回来（-3.84→-3.44→-3.84），幅度取半做正弦。
    if abs(dx) < 0.05 and 0.15 <= xspan <= 1.5 and period > 0.2:
        return (xspan * 0.5, period, 0.0, 0.0, 0.0, 0.0, 0.0)
    # 带符号：正值往 +X，负值往 -X。官机 warning 是 0→-6，脸图和 mid_B5 是往 +X。
    signed = dx if abs(dx) > 0.45 else (loop if loop > 0.45 else 0.0)
    if nm.startswith("mid_h"):
        # H1 弹窗走 pop/move；H2 不要默认 0.12 漂。H5/warning 筐内横移仍要 loop。
        if abs(signed) > 0.45:
            return (0.0, 0.0, 0.0, signed, period, 0.0, 0.0)
        return z
    if abs(signed) > 0.45:
        return (0.0, 0.0, 0.0, signed, period, 0.0, 0.0)
    return (0.12, period, 0.0, 0.0, 0.0, 0.0, 0.0)


def is_soft_mask(go):
    for _, o in comps(go):
        if o.__class__.__name__ != "MonoBehaviour":
            continue
        try:
            t = o.object_reader.read_typetree()
        except Exception:
            t = None
        if t and ("_showMaskGraphics" in t or "maskedObjects" in t):
            return True
    return False


def dotween_scroll(go):
    """DOTweenAnimation：scene_02 far_rocket 相对位移 (-20,0) / 38s 无限 Restart，没有 Animator。"""
    for _, o in comps(go):
        if o.__class__.__name__ != "MonoBehaviour":
            continue
        try:
            t = o.object_reader.read_typetree()
        except Exception:
            continue
        if not t or "endValueV3" not in t or "duration" not in t:
            continue
        if not t.get("isActive", True) or not t.get("autoPlay", True):
            continue
        if int(t.get("animationType") or 0) not in (0, 1):
            continue
        dur = float(t.get("duration") or 0)
        ev = t.get("endValueV3") or {}
        dx = abs(float(ev.get("x") or 0)) if isinstance(ev, dict) else 0.0
        if dur < 0.5 or dx < 0.5:
            continue
        return round(dx, 4), round(dur, 4)
    return 0.0, 0.0


def scrollable_of(go):
    """Scrollable：scene_10 舞台没有 Animator，圈长和速度写在这条脚本上。"""
    for _, o in comps(go):
        if o.__class__.__name__ != "MonoBehaviour":
            continue
        try:
            t = o.object_reader.read_typetree()
        except Exception:
            continue
        if not isinstance(t, dict) or "m_ScrollLength" not in t:
            continue
        length = float(t.get("m_ScrollLength") or 0)
        spd = float(t.get("m_SpeedFactor") or 0)
        if length < 1.0 or spd <= 0:
            continue
        anchor = float(t.get("m_Anchor") or 0)
        return length, spd, anchor
    return None


def sprite_image(sr):
    try:
        sp = sr.m_Sprite.read()
    except Exception:
        return None, None, (0.0, 0.0)
    name = sp.m_Name
    pivot = (0.0, 0.0)
    try:
        p = sp.m_Pivot
        pivot = (float(p.x), float(p.y))
    except Exception:
        pass
    try:
        img = sp.image.convert("RGBA")
    except Exception:
        return name, None, pivot
    try:
        rd = getattr(sp, "m_RD", None)
        vd = getattr(rd, "m_VertexData", None) if rd is not None else None
        vc = int(getattr(vd, "m_VertexCount", 0) or 0)
        if vc > 6:
            from PIL.Image import Transpose
            from UnityPy.export.SpriteHelper import get_image_from_sprite, mask_sprite
            from UnityPy.helpers.MeshHelper import MeshHandler
            raw = get_image_from_sprite(sp).convert("RGBA").transpose(Transpose.FLIP_TOP_BOTTOM)
            reader = getattr(sp, "object_reader", None)
            ver = reader.version if reader is not None else (2019, 4, 0, 0)
            mesh = MeshHandler(rd, ver)
            mesh.process()
            img = mask_sprite(sp, mesh, raw).transpose(Transpose.FLIP_TOP_BOTTOM)
    except Exception:
        pass
    return name, img, pivot


def fit_loop_to_tiles(items, loop, period, layer_name=""):
    """循环宽度必须等于地板块间距，否则取模会整段回跳。速度仍跟 Animator。"""
    if period < 0.05 or len(items) < 4:
        return loop, period
    # 官机循环宽已是地砖步长的整数倍就保持。scene_01 的 floor_1 间距忽 1.28 忽 2.56，
    # 按单张贴图会把 3.84（AAB 三块）扩成 12.8，接缝对不齐。
    if loop > 0.05:
        xs = sorted(
            it["x"] for it in items
            if "floor" in (it.get("spr") or "").lower() or (it.get("spr") or "").lower().startswith("road")
            or "water" in (it.get("spr") or "").lower()
        )
        gaps = []
        for i in range(len(xs) - 1):
            d = round(xs[i + 1] - xs[i], 3)
            if 0.35 <= d <= 4.0:
                gaps.append(d)
        if len(gaps) >= 3:
            step, n = Counter(gaps).most_common(1)[0]
            if n >= 3 and step > 0.3:
                k = loop / step
                if abs(k - round(k)) < 0.08 and round(k) >= 1:
                    return round(loop, 4), round(period, 4)
    by_spr = {}
    for it in items:
        by_spr.setdefault(it["spr"], []).append(it["x"])
    best = None
    nm = (layer_name or "").lower()
    prefer = "floor" in nm or "road" in nm
    for spr, xs in by_spr.items():
        xs = sorted(xs)
        if len(xs) < 4:
            continue
        diffs = []
        for i in range(len(xs) - 1):
            d = xs[i + 1] - xs[i]
            if 0.35 <= d <= 12.0:
                diffs.append(round(d, 4))
        if len(diffs) < 3:
            continue
        pitch, n = Counter(diffs).most_common(1)[0]
        if n < 3:
            continue
        tiled_spr = "floor" in spr.lower() or spr.lower().startswith("road")
        score = n + (1000 if tiled_spr else 0)
        if best is None or score > best[0]:
            best = (score, pitch, n, len(diffs), tiled_spr)
    if best is None:
        return loop, period
    _, pitch, n, nd, tiled_spr = best
    # 楼景、远景的循环宽就是 Animator 行程。偶发的小间距不是地砖，
    # 收进 6–28 会把 scene_04 中景 69.6 裁成 23，后半条街消失。
    if not tiled_spr and not prefer:
        return loop, period
    # 行程已经长过一屏（scene_08 近景 62、scene_07 mid 57、scene_09 中景 48）。
    # 下面的 6–28 包裹只修短地砖，不能把整条街裁成一块。
    if loop > 28:
        return loop, period
    speed = loop / period if loop > 0.05 else 5.0
    if speed < 0.5:
        speed = 5.0
    # 官 clip 循环宽已是块间距整数倍就保持（含 scene_05 水面 5.0）。不要强行扩到 12。
    if loop > 0.05 and pitch > 0.3:
        k = loop / pitch
        if abs(k - round(k)) < 0.08 and round(k) >= 1:
            return round(loop, 4), round(loop / speed, 4)
    wrap = None
    if loop > 0.05 and pitch > 0.3:
        tiles = max(1, int(round(loop / pitch)))
        for n in range(min(tiles, 20), 0, -1):
            if tiles % n == 0:
                w = n * pitch
                if 6.0 <= w <= 28.0:
                    wrap = w
                    break
    if wrap is None:
        wrap = pitch * max(1, int(round(12.0 / pitch))) if pitch > 0.3 else loop
        if wrap < 6:
            wrap = pitch * max(4, int(round(8.0 / max(pitch, 0.3))))
    return round(wrap, 4), round(wrap / speed, 4)


def clip_popup_titles_to_screen(items):
    """mid_H4 电台标题比屏幕宽，不裁会从脸屏左边伸到判定线。"""
    h7s = [it for it in items if it.get("spr") == "mid_H7"]
    if not h7s:
        return items
    for it in items:
        if it.get("spr") not in ("mid_H3", "mid_H4", "mid_H6"):
            continue
        near = min(h7s, key=lambda h: abs(h["x"] - it["x"]))
        if abs(near["x"] - it["x"]) > 4.0:
            continue
        it["mx"] = near["x"]
        it["my"] = near["y"]
        it["mw"] = near["w"]
        it["mh"] = near["h"]
    return items


def loop_twin_slack(items, spr, loop):
    """密铺地砖一圈头尾会比 loop 短 1～2。2.0 容差会把最后一块当下一圈删掉。"""
    xs = sorted(float(it["x"]) for it in items if it.get("spr") == spr)
    if len(xs) < 8 or loop < 8:
        return 2.0
    gaps = [
        xs[i + 1] - xs[i]
        for i in range(len(xs) - 1)
        if 0.35 <= xs[i + 1] - xs[i] <= loop * 0.2
    ]
    if len(gaps) < 6:
        return 2.0
    gaps.sort()
    med = gaps[len(gaps) // 2]
    if med < 0.35 or med > loop * 0.15:
        return 2.0
    return min(2.0, med * 0.35)


def clip_items_to_loop(items, origin, loop):
    """只留第一个循环窗（Floor_A）。弹筐子物件按遮罩中心判断，好留下卷进框内的条。"""
    if loop < 0.05 or not items:
        return items
    lo, hi = origin - 0.2, origin + loop + 0.2
    # 层原点可能在第一块贴图左边。本圈后半截会超过 origin+loop，
    # 下一圈同一块又在窗外，两份一起被丢掉，接缝上就空一截。
    xs = [
        float(it["x"]) for it in items
        if float(it.get("mw") or 0) <= 0.05 and float(it.get("popPeriod") or 0) <= 0.05
    ]
    if xs:
        hi = max(hi, min(xs) + loop + 0.05)
        # 原点也可能在第一块右边：scene_12 近景两块地砖在 Near.x 左侧 4.08，
        # 只扩右窗会把它们丢掉，接缝空一截瓦。
        lo = min(lo, max(xs) - loop - 0.05)

    def in_win(x):
        return lo <= x < hi

    kept = []
    for it in items:
        if float(it.get("mw") or 0) > 0.05:
            if in_win(float(it.get("mx") or 0)):
                kept.append(it)
        elif float(it.get("popPeriod") or 0) > 0.05:
            if origin - 2.0 <= it["x"] < origin + loop + 12.0:
                kept.append(it)
        elif in_win(it["x"]):
            kept.append(it)
    # 下一圈开头和这一圈差一个 loop。留着会在接缝上叠一份。
    if kept and loop > 0.5:
        slack = {it["spr"]: loop_twin_slack(kept, it["spr"], loop) for it in kept}
        cyc = []
        for it in kept:
            twin = False
            # 短循环水面（scene_06 Water 3.6、块距 1.8）：默认 slack=2 会把邻居当成下一圈，
            # 每行只剩一块，k 拷后叠成一串同心圆。容差必须小于半个 loop。
            eps = min(slack.get(it["spr"], 2.0), max(0.08, loop * 0.22))
            for other in kept:
                if other is it or other["spr"] != it["spr"]:
                    continue
                # 层 loop 和预制间距会差 1～2（scene_08 中景 loop 63.5，鸟居两份却隔 61.87）。
                # 容差 0.08 认不出下一圈，鸟居、水和冰块会留在轨道外面。
                if abs(other["x"] - (it["x"] - loop)) < eps and abs(other["y"] - it["y"]) < 0.4:
                    twin = True
                    break
            if not twin:
                cyc.append(it)
        if cyc:
            kept = cyc
    # 本圈唯一的物件落在 origin+loop 之外：scene_08 的 `tree` 在 x≈21，
    # 预制两份间距 62.1，层循环却是 63.5，按 origin+loop 会整棵丢掉。
    # 神社围栏/草、钟楼 cattle 同样有一份落在窗外，只补 tree 会缺件。
    keep_out = {
        "tree", "tree_small", "house", "house_fence", "brand",
        "grass_from_house_left", "grass_from_house_right",
        "cattle", "cattle_fence", "cattle_gate",
    }
    have = {(it["spr"], round(float(it["y"]), 2)) for it in kept}
    for it in items:
        if (it.get("spr") or "") not in keep_out:
            continue
        key = (it["spr"], round(float(it["y"]), 2))
        if key in have:
            continue
        if origin - 0.2 <= float(it["x"]) < origin + loop * 2 + 0.2:
            kept.append(it)
            have.add(key)
    # 圈长正好切在物件上：scene_10 镜音连在 65.92，窗只到约 65.3。
    # 右缘 1.8 以内、左边没有同一张的，留下。再远就是下一圈，不要带进来。
    edge = hi + 1.8
    for it in items:
        x = float(it["x"])
        if not (hi <= x < edge):
            continue
        if any(k is it for k in kept):
            continue
        twin = False
        for other in kept:
            if other.get("spr") != it.get("spr"):
                continue
            if abs(float(other["x"]) - (x - loop)) < min(loop_twin_slack(kept, it.get("spr"), loop), max(0.08, loop * 0.22)) and abs(float(other["y"]) - float(it["y"])) < 0.5:
                twin = True
                break
        if not twin:
            kept.append(it)
    # 左缘：scene_10 SpeakerBoxMiddle1 在 Mid1 origin 左侧约 5（x≈−13.07），
    # lo 只扩到 origin−0.2 会丢掉；折回 x+loop 在圈内没有孪生件。
    left_edge = lo - 6.0
    for it in items:
        x = float(it["x"])
        if not (left_edge <= x < lo):
            continue
        if any(k is it for k in kept):
            continue
        twin = False
        for other in kept:
            if other.get("spr") != it.get("spr"):
                continue
            if abs(float(other["x"]) - (x + loop)) < min(loop_twin_slack(kept, it.get("spr"), loop), max(0.08, loop * 0.22)) and abs(float(other["y"]) - float(it["y"])) < 0.5:
                twin = True
                break
        if not twin:
            kept.append(it)
    return kept if kept else items


def reattach_clipped_children(kept, items):
    """只把 s01_floor_light 挂回本圈地砖。

    clip 按 x−loop 去重时，上一圈 FloorB 出窗、灯还在窗里，会和留下的 AAB 错位。
    不要把其它景 w<0.8 的瓦/鱼/叶当成灯丢掉（scene_12 的 d_tile、鱼、leaf_5）。
    """
    if not kept or not items:
        return kept

    def is_light(it):
        return "floor_light" in (it.get("spr") or "").lower()

    def is_floor(it):
        spr = (it.get("spr") or "").lower()
        return "floor" in spr and "light" not in spr and "flower" not in spr

    lights_all = [it for it in items if is_light(it)]
    if not lights_all:
        return kept
    floors = [it for it in kept if is_floor(it)]
    floors_all = [it for it in items if is_floor(it)]
    if not floors:
        return kept

    def dist(a, b):
        return abs(float(a["x"]) - float(b["x"])) + abs(float(a["y"]) - float(b["y"]))

    def closest(pool, it):
        return min(pool, key=lambda p: dist(p, it))

    out = [it for it in kept if not is_light(it)]
    have = {(round(float(it["x"]), 3), round(float(it["y"]), 3), it.get("spr")) for it in out}
    for p in floors:
        for it in lights_all:
            if dist(it, p) > 1.8:
                continue
            owner = closest(floors_all, it) if floors_all else p
            if abs(float(owner["x"]) - float(p["x"])) > 0.05 or abs(float(owner["y"]) - float(p["y"])) > 0.05:
                continue
            key = (round(float(it["x"]), 3), round(float(it["y"]), 3), it.get("spr"))
            if key in have:
                continue
            out.append(it)
            have.add(key)
    return out


def fill_house_cluster_props(items):
    """scene_08 两座神社间距约 38.3，围栏/草只挂在左边那座。右边这座（开局进镜）要按间距补一份。"""
    houses = sorted(
        float(it["x"]) for it in items
        if (it.get("spr") or "") == "house" and float(it.get("h") or 0) > 3.0
    )
    if len(houses) < 4:
        return items
    left = houses[0]
    rights = [x for x in houses if x > left + 20]
    if not rights:
        return items
    delta = round(rights[0] - left, 4)
    if delta < 20 or delta > 50:
        return items
    props = {
        "house_fence", "grass_from_house_left", "grass_from_house_right",
        "brand", "tree_small",
    }
    extras = []
    for it in items:
        if (it.get("spr") or "") not in props:
            continue
        nx = round(float(it["x"]) + delta, 4)
        hit = False
        for other in items:
            if other.get("spr") != it.get("spr"):
                continue
            if abs(float(other["x"]) - nx) < 0.35 and abs(float(other["y"]) - float(it["y"])) < 0.35:
                hit = True
                break
        if hit:
            continue
        rec = dict(it)
        rec["x"] = nx
        extras.append(rec)
    if extras:
        items = list(items) + extras
    return items


def clip_spines_to_loop(spines, origin, loop):
    if loop < 0.05 or not spines:
        return spines
    lo, hi = origin - 0.2, origin + loop
    kept = [sp for sp in spines if lo <= float(sp.get("x") or 0) < hi]
    return kept if kept else spines


def shop_piece(it, hearts):
    """爱心公告栏的框、立柱、棚沿细条面积很小，按面积裁切会只剩大底板。"""
    spr = it.get("spr") or ""
    if spr.startswith("mid_F2"):
        return True
    if spr != "bg_white":
        return False
    for h in hearts:
        if abs(it["x"] - h["x"]) < 3.2 and -3.3 < it["y"] - h["y"] < 0.4:
            return True
    return False


def trim_items(items, cap=480):
    pops = [it for it in items if float(it.get("popPeriod") or 0) > 0.05]
    rest = [it for it in items if float(it.get("popPeriod") or 0) <= 0.05]
    hearts = [it for it in rest if it.get("spr") == "mid_F2_up2"]
    reserved_ids = {id(it) for it in rest if shop_piece(it, hearts)}
    reserved = [it for it in rest if id(it) in reserved_ids]
    pool = [it for it in rest if id(it) not in reserved_ids]
    if len(pool) <= cap:
        return pool + reserved + pops
    items = pool
    by_area = sorted(items, key=lambda it: -(it["w"] * it["h"]))
    keep, seen, ids = [], set(), set()
    for it in by_area:
        spr = it["spr"]
        if spr in seen:
            continue
        keep.append(it)
        seen.add(spr)
        ids.add(id(it))
    for it in by_area:
        if len(keep) >= cap:
            break
        if id(it) in ids:
            continue
        keep.append(it)
        ids.add(id(it))
    return keep[:cap] + reserved + pops


def skeleton_index(env):
    names, scales = {}, {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            d = obj.read()
        except Exception:
            continue
        n = d.m_Name or ""
        if not n.endswith("_SkeletonData"):
            continue
        pid = obj.path_id
        names[pid] = n[: -len("_SkeletonData")]
        try:
            t = d.object_reader.read_typetree()
            scales[pid] = float(t.get("scale") or 0.005)
        except Exception:
            scales[pid] = 0.005
    return names, scales


def walk_group(go, gx, gy, gsx, gsy, items, sprites, spines, skel_names, vis, clip_map, skip_root=False, layer_loop=0.0, motion=None, mask=None, drive=False, popAt=0.0, popPeriod=0.0, popQueue=None, moves=None, moveId=0, nest_pop=False, nest_i=0, popOff=0.0, rotz=0.0, anim_path=""):
    if skip_name(go.m_Name) or is_variant(go.m_Name):
        return
    was_active = bool(getattr(go, "m_IsActive", True))
    if not was_active and not drive:
        return
    # scene_02 街机 Fx：官机整棵 Fx（s02_Fx_heart / mid_H / mid_I_machines_icon）默认关。
    # 父级有 Animator 时 drive 会把它们抽出来，叠在柜面孔上。
    if not was_active:
        nm = go.m_Name or ""
        if nm.startswith("Fx") or nm in ("s02_Fx_heart", "mid_H", "mid_I_machines_icon"):
            return
    vis = vis and (was_active or drive)
    tr = transform_of(go)
    if tr is None:
        return
    lp, ls = v3(tr.m_LocalPosition), v3(tr.m_LocalScale)
    if anim_path and sole_sibling(go):
        hit = _CLIP_POS.get(name_hash(anim_path))
        if hit is not None:
            lp = (hit[0], hit[1], lp[2])
    bind = anim_bind(go)
    if bind is not None:
        bx, by, xspan, yspan = bind
        # 整层横移时，曲线记的是游动的子物体（scene_06 的鱼从 18 走到 -2）。
        # 首帧再写到根上，会和子物体自己的本地坐标叠成两倍。
        scroll_like = layer_loop > 1.0 and xspan > 1.0 and abs(xspan - layer_loop) < 0.6
        # 曲线离 Transform 很远、自身只晃一点点：是加在原位上的修正，不是替换。
        # RedLight 的 x=-3.31 再加开局 0.753，亮边才落在 Well3 左缘；直接改成 0.753 会切进方块。
        if (not scroll_like) and bx is not None and abs(bx - lp[0]) > 0.02:
            if abs(lp[0]) > 0.5 and xspan < 1.0 and abs(bx) < 2.0 and abs(bx - lp[0]) > 0.5:
                bx = lp[0] + bx
            lp = (bx, lp[1], lp[2])
        if (not scroll_like) and by is not None and abs(by - lp[1]) > 0.02:
            if abs(lp[1]) > 0.5 and yspan < 1.0 and abs(by) < 2.0 and abs(by - lp[1]) > 0.5:
                by = lp[1] + by
            lp = (lp[0], by, lp[2])
    parent_rot = rotz
    hier_rot = parent_rot + local_euler_z(tr)
    # 父节点有旋转时，子物体本地坐标要先转再相加。远光父节点约 40°，不转会把光柱挪到轨道上。
    rad = math.radians(parent_rot)
    rc, rs = math.cos(rad), math.sin(rad)
    lx, ly = lp[0] * gsx, lp[1] * gsy
    wx = gx + rc * lx - rs * ly
    wy = gy + rs * lx + rc * ly
    wsx, wsy = gsx * ls[0], gsy * ls[1]
    ac = animator_clip(go) if not is_layer_anim(go) else None
    pops, pop_len = pop_on_times(ac) if ac is not None else ([], 0.0)
    loc = local_motion(go, clip_map, layer_loop)
    inh = motion or (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
    if pops:
        loc = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
        inh = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0)
    note_light_spins(go)
    pgo = parent_go(go)
    sid, spin_src = lookup_spin(anim_path, go.m_Name or "", pgo.m_Name if pgo is not None else "")
    if sid and spin_src == "self":
        # s07_farlight / scene_10 射灯：用曲线第 0 帧替换父节点欧拉角。
        hier_rot = parent_rot + _SPIN_CURVES[sid - 1]["zs"][0]
    rotz = hier_rot
    ride = animator_clip(go) if "roller_coaster_icon_all" in (go.m_Name or "") else None
    if moves is not None and ride is not None and not any(mv.get("path") for mv in moves):
        # 两只耳机各一条本地位移，不是父节点带着一起走。
        series = clip_stream(ride)
        pi = 0
        for b in clip_bindings(ride):
            if int(b.get("attribute") or 0) != 1:
                continue
            xs = series.get(pi * 3) or []
            ys = series.get(pi * 3 + 1) or []
            pi += 1
            pts = []
            n = min(len(xs), len(ys))
            for i in range(n):
                if abs(xs[i][0] - ys[i][0]) > 0.02:
                    continue
                pts.append((round(xs[i][0], 4), round(xs[i][1], 4), round(ys[i][1], 4)))
            if len(pts) < 2:
                continue
            moves.append({
                "period": round(pts[-1][0], 4),
                "ts": [p[0] for p in pts],
                "xs": [p[1] for p in pts],
                "ys": [p[2] for p in pts],
                "step": False,
                "path": int(b.get("path") or 0),
            })
        if moves:
            moveId = 1
    if (go.m_Name or "") == "Mid_H1" and ac is not None:
        ts, xs, ys, per = pop_root_xy(ac)
        if ts and moves is not None:
            if not moves:
                # LoopBlend=0：弹出时阶跃，不要在 1.85→周期 之间把整组滑回去。
                moves.append({"period": per, "ts": ts, "xs": xs, "ys": ys, "step": True})
            moveId = 1
    bob_amp = loc[0] if loc[0] > 0.001 else inh[0]
    bob_per = loc[1] if loc[0] > 0.001 else inh[1]
    blink = loc[2] if loc[2] > 0.05 else inh[2]
    dx = loc[3] if abs(loc[3]) > 0.05 else inh[3]
    scroll_per = loc[4] if abs(loc[3]) > 0.05 else inh[4]
    scroll_y = loc[5] if abs(loc[5]) > 0.05 else inh[5]
    scroll_y_per = loc[6] if abs(loc[5]) > 0.05 else inh[6]
    # WellArrowA/C 绕 Z 180°：本地 −Y 变成世界 +Y，两边上滑、中间列不转仍下滑。
    if abs(loc[5]) > 0.05:
        rz = abs(hier_rot) % 360.0
        if min(abs(rz - 180.0), abs(rz - 180.0 - 360.0)) < 8.0:
            scroll_y = -scroll_y
    tw, twp = dotween_scroll(go)
    if tw > 0.05:
        bob_amp = 0.0
    if abs(scroll_y) > 0.05:
        bob_amp = 0.0
    # 灯笼本体挂在 biuld_4 下会继承 0.12 浮动；官机只有鱼在晃，长灯笼钉死。
    gn = (go.m_Name or "").lower()
    if "lantern" in gn and "light" not in gn:
        bob_amp = 0.0
    # scene_10：KAITO 本体钉死；头上两颗音符跟镜音同一套 0.12 / 1s。
    if "kaito" in gn and "note" not in gn:
        bob_amp = 0.0
    if gn in ("rin", "len") or "kaito_note" in gn or (gn.endswith("note") and "kaito" in gn):
        bob_amp = 0.12
        bob_per = bob_per if bob_per > 0.05 else 1.0
    motion = (bob_amp, bob_per, blink, dx, scroll_per, scroll_y, scroll_y_per)
    if not skip_root:
        sr = None
        order = 0
        color = (1, 1, 1, 1)
        pack = None
        clip = ""
        for _, o in comps(go):
            cn = o.__class__.__name__
            if cn == "SpriteRenderer":
                sr = o
                try:
                    c = o.m_Color
                    color = (float(c.r), float(c.g), float(c.b), float(c.a))
                except Exception:
                    pass
                try:
                    order = int(o.m_SortingOrder)
                except Exception:
                    pass
            elif cn == "MeshRenderer":
                try:
                    order = int(o.m_SortingOrder)
                except Exception:
                    pass
            elif cn == "MonoBehaviour":
                try:
                    t = o.object_reader.read_typetree()
                except Exception:
                    t = None
                if t and t.get("skeletonDataAsset"):
                    pid = (t["skeletonDataAsset"] or {}).get("m_PathID")
                    pack = skel_names.get(pid)
                    clip = t.get("_animationName") or t.get("animationName") or ""
        spr_name = None
        if sr is not None:
            try:
                spr_name = sr.m_Sprite.read().m_Name
            except Exception:
                spr_name = None
        show_spr = vis and not is_variant(spr_name)
        show_spn = vis and pack and not is_variant(pack) and not re.match(r"^\d{4}", pack)
        this_mask = None
        if show_spr and sr is not None and color[3] > 0.01:
            name, img, pivot = sprite_image(sr)
            if name and img is not None and not is_variant(name):
                sprites[name] = img
                ppu = 100.0
                try:
                    ppu = float(sr.m_Sprite.read().m_PixelsToUnits) or 100.0
                except Exception:
                    pass
                raw_w = (img.width / ppu) * wsx
                raw_h = (img.height / ppu) * wsy
                slice_l = slice_r = cap_l = cap_r = 0.0
                try:
                    sp = sr.m_Sprite.read()
                    border = sp.m_Border
                    rect = sp.m_Rect
                    bw = float(rect.width) or 1.0
                    bh = float(rect.height) or 1.0
                    slice_l = float(border.x) / bw
                    slice_r = float(border.z) / bw
                    cap_l = float(border.x) / ppu * abs(wsx)
                    cap_r = float(border.z) / ppu * abs(wsx)
                    draw_mode = int(getattr(sr, "m_DrawMode", 0) or 0)
                    sz = getattr(sr, "m_Size", None)
                    if draw_mode in (1, 2) and sz is not None:
                        # Sliced/Tiled：m_Size 是九宫格目标尺寸，再乘 Transform scale。
                        raw_w = float(sz.x) * wsx
                        raw_h = float(sz.y) * wsy
                    else:
                        slice_l = slice_r = cap_l = cap_r = 0.0
                except Exception:
                    pass
                flip_x = raw_w < 0
                flip_y = raw_h < 0
                try:
                    if bool(getattr(sr, "m_FlipX", False)):
                        flip_x = not flip_x
                    if bool(getattr(sr, "m_FlipY", False)):
                        flip_y = not flip_y
                except Exception:
                    pass
                # 官机 flipX 绕 pivot 镜像，不是绕贴图中心。pivot 在左下时，中心要翻到 pivot 另一侧。
                # 有旋转时再把这个中心绕 pivot 转过去（光带 -90°，绕中心转会整条偏出画面）。
                ox = (0.5 - pivot[0]) * abs(raw_w) * (-1 if flip_x else 1)
                oy = (0.5 - pivot[1]) * abs(raw_h) * (-1 if flip_y else 1)
                if abs(rotz) > 0.5:
                    rad = math.radians(rotz)
                    c, s = math.cos(rad), math.sin(rad)
                    cx = wx + c * ox - s * oy
                    cy = wy + s * ox + c * oy
                else:
                    cx = wx + ox
                    cy = wy + oy
                if abs(raw_w) * abs(raw_h) > 6.0:
                    bob_amp = 0.0
                if is_soft_mask(go):
                    this_mask = (cx, cy, abs(raw_w), abs(raw_h))
                clip_src = mask
                if clip_src or this_mask or nest_pop or popPeriod > 0.05:
                    bob_amp = 0.0
                # scene_10 人物面积>6、又挂在射灯遮罩下，会被上面两条清掉。
                ln = (name or "").lower()
                kaito_note = "kaito_note" in ln or ("kaito" in gn and "note" in ln)
                if ("kaito" in gn or "kaito" in ln) and not kaito_note:
                    bob_amp = 0.0
                elif kaito_note or gn in ("rin", "len") or "s10_rin" in gn or gn == "s10_len" or ln in ("s10_rin", "s10_len") or "rin_star" in ln:
                    bob_amp = 0.12
                    if bob_per < 0.05:
                        bob_per = 1.0
                # 官机 mid_H7 是 -43，框/标题 -42～-40。叠层小窗必须比底盘更靠前，否则会被脸屏盖住。
                if nest_pop:
                    order = -39 + nest_i
                if name == "far_rocket":
                    # 贴图已是紫色；再乘 SR 色会融进天空。order 提到远楼前面、近景后面，否则 -78 被 Far 楼挡住。
                    color = (1.0, 1.0, 1.0, color[3] if color[3] > 0.01 else 1.0)
                    order = -58
                # 遮罩外的 warning/H5 滚动拷贝：没裁切时会铺满铁网、盖到判定线。
                # 筐外的滚动拷贝要留一份，否则字和脸图滑出遮罩后没有下一截接上。仍靠遮罩裁切，不铺到判定线。
                reach = clip_src[2] * 0.58 if clip_src else 0.0
                if clip_src and abs(dx) > 0.45:
                    reach = abs(dx) + clip_src[2] + 0.6
                outside = bool(clip_src) and (not this_mask) and abs(cx - clip_src[0]) > reach
                same = any(
                    it["spr"] == name and abs(it["x"] - cx) < 0.02 and abs(it["y"] - cy) < 0.02
                    and abs(float(it.get("w") or 0) - abs(raw_w)) < 0.02
                    and abs(float(it.get("h") or 0) - abs(raw_h)) < 0.02
                    and int(it.get("order") or 0) == int(order)
                    and abs(float(it.get("rotZ") or 0) - (rotz if abs(rotz) > 0.5 else 0.0)) < 0.5
                    for it in items
                )
                if name == "s10_miku":
                    same = True
                if not outside and not same:
                    h = name_hash(go.m_Name)
                    for mi, mv in enumerate(moves or []):
                        if mv.get("path") and mv["path"] == h:
                            moveId = mi + 1
                            break
                    parent = parent_go(go)
                    spin_id, spin_src = lookup_spin(
                        anim_path, go.m_Name or "", parent.m_Name if parent is not None else ""
                    )
                    spin_rot = rotz
                    orbit = 1 if spin_id else 0
                    if spin_src == "self":
                        pivx, pivy = wx, wy
                    elif spin_id:
                        pivx, pivy = gx, gy
                    else:
                        pivx, pivy = 0.0, 0.0
                    items.append({
                        "spr": name,
                        "x": round(cx, 4),
                        "y": round(cy, 4),
                        "w": round(abs(raw_w), 4),
                        "h": round(abs(raw_h), 4),
                        "order": order,
                        "r": round(color[0], 4),
                        "g": round(color[1], 4),
                        "b": round(color[2], 4),
                        "a": round(color[3], 4),
                        "flipX": flip_x,
                        "flipY": flip_y,
                        "topW": 0.0,
                        "sliceL": round(slice_l, 4) if cap_l + cap_r > 0.02 else 0.0,
                        "sliceR": round(slice_r, 4) if cap_l + cap_r > 0.02 else 0.0,
                        "capL": round(cap_l, 4) if cap_l + cap_r > 0.02 else 0.0,
                        "capR": round(cap_r, 4) if cap_l + cap_r > 0.02 else 0.0,
                        "rotZ": round(spin_rot, 3) if abs(spin_rot) > 0.5 else 0.0,
                        "spinId": spin_id,
                        "orbit": orbit,
                        "pivotX": round(pivx, 4) if orbit else 0.0,
                        "pivotY": round(pivy, 4) if orbit else 0.0,
                        "bobAmp": round(bob_amp, 4),
                        "bobPeriod": round(bob_per, 4),
                        "bobPhase": round(((clip_src[0] if clip_src else cx) * 1.73 + (clip_src[1] if clip_src else cy) * 2.11) % 6.2832, 4),
                        "blinkPeriod": round(blink, 4),
                        "scrollLoop": round((tw if tw > 0.05 else (dx if clip_src else 0.0)), 4),
                        "scrollAngle": round(spin_rot, 3) if abs(dx) > 0.45 and abs(spin_rot) > 0.5 else 0.0,
                        # warning / mid_H5 官机是往 −X（dx<0）。用 dx>0 会丢掉 5.5s / 9s 周期。
                        "scrollPeriod": round((twp if tw > 0.05 else (scroll_per if clip_src and abs(dx) > 0.05 else (scroll_y_per if abs(scroll_y) > 0.05 else 0.0))), 4),
                        "scrollY": round(scroll_y, 4) if abs(scroll_y) > 0.05 else 0.0,
                        "mx": round(clip_src[0], 4) if clip_src else 0.0,
                        "my": round(clip_src[1], 4) if clip_src else 0.0,
                        "mw": round(clip_src[2], 4) if clip_src else 0.0,
                        "mh": round(clip_src[3], 4) if clip_src else 0.0,
                        "popAt": round(popAt, 4) if popPeriod > 0.05 else 0.0,
                        "popPeriod": round(popPeriod, 4) if popAt > 0.02 else 0.0,
                        "popOff": round(popOff, 4) if popOff > 0.05 else 0.0,
                        "moveId": int(moveId) if moveId else 0,
                    })
        if show_spn:
            spines.append({
                "pack": pack,
                "x": round(wx, 4),
                "y": round(wy, 4),
                "sx": round(wsx, 4),
                "sy": round(wsy, 4),
                "clip": clip,
                "order": order,
            })
        if this_mask:
            mask = this_mask
    child_vis = True if (drive and not was_active) else vis
    child_drive = drive or (ac is not None and not is_layer_anim(go))
    queue = list(popQueue or [])
    if pops:
        queue = pops
        if pop_len > 0.05:
            popPeriod = pop_len
    kids = []
    if tr.m_Children:
        for ch in tr.m_Children:
            try:
                kids.append(ch.read().m_GameObject.read())
            except Exception:
                continue
    inact = [k for k in kids if not bool(getattr(k, "m_IsActive", True))]
    if queue and inact:
        if popPeriod < 0.05:
            popPeriod = pop_len if pop_len > 0.05 else 2.8
        for i, k in enumerate(inact):
            # 官 clip s02_Mid_H1：0.85 / 1.10 / 1.35 / 1.60 / 1.85 逐层开，周期末复位；不开假两波、不提前关顶层。
            t = queue[i] if i < len(queue) else popPeriod * (i + 1) / (len(inact) + 1)
            ap = (anim_path + "/" + (k.m_Name or "")) if anim_path else (k.m_Name or "")
            walk_group(k, wx, wy, wsx, wsy, items, sprites, spines, skel_names, child_vis, clip_map, skip_root=False, layer_loop=layer_loop, motion=(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0), mask=mask, drive=True, popAt=t, popPeriod=popPeriod, popQueue=None, moves=moves, moveId=moveId, nest_pop=True, nest_i=i, popOff=0.0, rotz=hier_rot, anim_path=ap)
        inact_set = set(id(k) for k in inact)
        kids = [k for k in kids if id(k) not in inact_set]
        queue = []
    for k in kids:
        ap = (anim_path + "/" + (k.m_Name or "")) if anim_path else (k.m_Name or "")
        alt = default_sibling(k, kids)
        if alt is not None:
            walk_group(alt, wx, wy, wsx, wsy, items, sprites, spines, skel_names, child_vis, clip_map, skip_root=False, layer_loop=layer_loop, motion=motion, mask=mask, drive=True, popAt=popAt, popPeriod=popPeriod, popQueue=queue, moves=moves, moveId=moveId, nest_pop=nest_pop, nest_i=nest_i, popOff=popOff, rotz=hier_rot, anim_path=ap)
            continue
        walk_group(k, wx, wy, wsx, wsy, items, sprites, spines, skel_names, child_vis, clip_map, skip_root=False, layer_loop=layer_loop, motion=motion, mask=mask, drive=child_drive, popAt=popAt, popPeriod=popPeriod, popQueue=queue, moves=moves, moveId=moveId, nest_pop=nest_pop, nest_i=nest_i, popOff=popOff, rotz=hier_rot, anim_path=ap)


def find_go(env, name):
    for obj in env.objects:
        if obj.type.name != "GameObject":
            continue
        d = obj.read()
        if d.m_Name == name:
            return d
    return None


def child_gos(go):
    tr = transform_of(go)
    if tr is None or not tr.m_Children:
        return []
    out = []
    for ch in tr.m_Children:
        try:
            out.append(ch.read().m_GameObject.read())
        except Exception:
            continue
    return out


def add_layers(parent, px, py, psx, psy, groups, sprites, spines, skel_names, clip_map, inherited_loop=0.0, inherited_period=0.0, moves=None):
    if moves is None:
        moves = []
    for go in child_gos(parent):
        if skip_name(go.m_Name) or is_variant(go.m_Name):
            continue
        if go.m_Name in ("par_star", "par_star2"):
            continue
        if not drive_uncensored(parent, go):
            continue
        ctr = transform_of(go)
        if ctr is None:
            continue
        lp, ls = v3(ctr.m_LocalPosition), v3(ctr.m_LocalScale)
        gx = px + lp[0] * psx
        gy = py + lp[1] * psy
        gsx, gsy = psx * ls[0], psy * ls[1]
        gx_tf, gy_tf = gx, gy
        loop, period = parse_scroll(go, clip_map)
        driven = bound_local_xy(parent, go)
        group_mode = None
        if loop < 0.05 and driven is not None:
            pl, pp = parse_scroll(parent, clip_map)
            if pl > 0.05:
                # 只把父曲线交给被绑定的那一层（Ice_all），不要让 water / Near 跟着 08_ice 走。
                # 08_ice 是单次行程动画（从本地 x=16 移到 x≈−27，约 11 秒），不是循环滚动。
                loop, period = pl, pp
                gx = px + driven[0] * psx
                gy = py + driven[1] * psy
                # 标记为单次行程模式
                if (go.m_Name or "").startswith("Ice"):
                    group_mode = "once"
                else:
                    group_mode = "scroll"
        if loop < 0.05 and inherited_loop > 0.05:
            loop, period = inherited_loop, inherited_period
        if go.m_Name == "ScrollableGear" or go.m_Name.startswith("Scrollable"):
            add_layers(go, gx, gy, gsx, gsy, groups, sprites, spines, skel_names, clip_map, loop, period, moves)
            continue
        # scene_03 Default/Near 自己没有 Animator，滚动在 Floor_hexie / Water / Mid 上。
        # scene_09 Far_all 自己也不滚，山/云/月亮在 Far_A、cloud、Far_B、Far_C 上。
        # scene_12 的 Near 自己有 Scrollable（圈长 50），不要拆成不滚的子层。
        if loop < 0.05 and scrollable_of(go) is None and (go.m_Name or "") in ("Default", "Near", "Far_all"):
            add_layers(go, gx, gy, gsx, gsy, groups, sprites, spines, skel_names, clip_map, loop, period, moves)
            continue
        # 官机地板/道路 Animator 在 Near 的子节点（scene_02 Floor clip s02_floor），
        # 不能只读 Near 自己的控制器，否则层 loop=0 地板完全不滚。
        # scene_10 荧光棒的 Scrollable 在 Near 的子节点上，父级自己没有圈长。
        if loop < 0.05 and scrollable_of(go) is None:
            split = False
            for ch in child_gos(go):
                cl, _cp = parse_scroll(ch, clip_map)
                if scrollable_of(ch) is not None:
                    split = True
                    break
                if cl < 0.05:
                    continue
                nm = (ch.m_Name or "").lower()
                if (
                    nm in ("floor", "road", "water", "mountain", "tree", "fartree", "bg", "audioclips")
                    or nm.startswith("floor")
                    or nm.startswith("road")
                    or nm.startswith("tree")
                    or nm.startswith("audioclip")
                    or nm.endswith("_road")
                    or ("floor" in nm and "light" not in nm)
                    or "mountain" in nm
                    or "tree" in nm
                ):
                    split = True
                    break
            if split:
                add_layers(go, gx, gy, gsx, gsy, groups, sprites, spines, skel_names, clip_map, loop, period, moves)
                continue
        items = []
        local_sp = []
        vis = True
        forced = not bool(getattr(go, "m_IsActive", True))
        global _CLIP_POS
        prev_clip = _CLIP_POS
        _CLIP_POS = layer_clip_positions(go)
        walk_group(go, px, py, psx, psy, items, sprites, local_sp, skel_names, vis, clip_map, skip_root=False, layer_loop=loop, moves=moves, drive=forced, anim_path="")
        _CLIP_POS = prev_clip
        if abs(gx - gx_tf) > 0.05 or abs(gy - gy_tf) > 0.05:
            dx, dy = gx - gx_tf, gy - gy_tf
            for it in items:
                it["x"] = round(it["x"] + dx, 4)
                it["y"] = round(it["y"] + dy, 4)
            for sp in local_sp:
                sp["x"] = round(float(sp.get("x") or 0) + dx, 4)
                sp["y"] = round(float(sp.get("y") or 0) + dy, 4)
        items = trim_items(items)
        if loop < 0.05 and len(items) >= 4:
            # 只用地砖/水面的跨度补循环。井墙没有横移，用全部 x 会编出一条假皮带。
            xs = [
                it["x"] for it in items
                if "floor" in (it.get("spr") or "").lower()
                or (it.get("spr") or "").lower().startswith("road")
                or "water" in (it.get("spr") or "").lower()
            ]
            if len(xs) >= 4:
                span = max(xs) - min(xs)
                if 2.0 < span < 28.0:
                    loop = round(span, 4)
                    if period < 0.05:
                        period = 2.0
        loop, period = fit_loop_to_tiles(items, loop, period, go.m_Name)
        # 09_far_volcano 只有时长，根上没有位移。两套山相距 48.14，和远景其余层同一圈。
        if (go.m_Name or "") == "volcano" and loop < 0.05 and period >= 10.0 and len(items) >= 4:
            xs = sorted(
                float(it["x"]) for it in items
                if (it.get("spr") or "") == "volcano_01"
            )
            diffs = []
            for i in range(len(xs) - 1):
                d = round(xs[i + 1] - xs[i], 2)
                if 30.0 <= d <= 70.0:
                    diffs.append(d)
            if diffs:
                pitch, n = Counter(diffs).most_common(1)[0]
                if n >= 2:
                    loop = pitch
            if loop < 0.05:
                loop = 48.14
        if loop < 0.05:
            for it in items:
                sl = float(it.get("scrollLoop") or 0)
                sp = float(it.get("scrollPeriod") or 0)
                if sl > 0.5 and sp > 0.5:
                    # far_rocket：DOTween 提到层上，靠 Shift 飞过镜头；只写在 item 上容易一直停在 x=8.5 画面外。
                    loop, period = sl, sp
                    it["scrollLoop"] = 0.0
                    it["scrollPeriod"] = 0.0
                    break
        # 普通键 44 帧、13.85 宽。Scrollable.m_SpeedFactor 乘的是这条车道速度。
        scroll_phase = 0.0
        sc = scrollable_of(go)
        if sc is not None:
            length, spd, anchor = sc
            base = 13.85 / (44.0 / 30.0)
            loop = round(length, 4)
            period = round(length / (base * spd), 4)
            scroll_phase = round(anchor * length, 4)
        # 不要压成 12 块地砖：scene_02 官机 Floor 是 48 块（92.16）。
        if loop > 0.05:
            raw_items = items
            items = clip_items_to_loop(items, gx, loop)
            items = reattach_clipped_children(items, raw_items)
            local_sp = clip_spines_to_loop(local_sp, gx, loop)
        # scene_06 两条鱼共用父层 Animator：第二条晚 1.233s 起游。
        # 圈长仍用层 20/4.233s，但两条要错开，叠在同一 x 会在水里看起来像收音机。
        if (go.m_Name or "").lower() == "fish" and loop > 0.05 and period > 0.05 and len(items) >= 2:
            ac = animator_clip(go)
            starts = []
            if ac is not None:
                series = clip_stream(ac)
                pi = 0
                for b in clip_bindings(ac):
                    if int(b.get("attribute") or 0) != 1:
                        continue
                    xs = series.get(pi * 3) or []
                    pi += 1
                    if xs:
                        starts.append(float(xs[0][0]))
            if len(starts) >= 2:
                speed = loop / period
                base_x = float(items[0]["x"])
                t0 = starts[0]
                for i, it in enumerate(items):
                    dt = (starts[i] - t0) if i < len(starts) else 0.0
                    it["x"] = round(base_x + dt * speed, 4)
        if (go.m_Name or "") == "Floor":
            items = clip_popup_titles_to_screen(items)
        if not items and not local_sp:
            continue
        gi = len(groups)
        belt = 0
        if (go.m_Name or "") == "Mid" and loop < 0.05 and any(
            str(it.get("spr") or "").startswith("s01_well") for it in items
        ):
            # 播放端看到 Mid 且速度为 0 会按地砖发明一条横移。scene_01 的井只竖滚。
            belt = 1
        if (go.m_Name or "") == "mid" and loop > 1.0 and period >= 10.0 and loop / period < 2.5:
            # 播放端把名字带 mid、速度低于 2.5 的层改成 5u/s。scene_07 过山车是 56.8/80s，不能加快。
            belt = 1
        groups.append({
            "name": go.m_Name,
            "x": round(gx, 4),
            "y": round(gy, 4),
            "loop": loop,
            "period": period,
            "loopW": loop,
            "periodS": period,
            "belt": belt,
            "phase": scroll_phase,
            "mode": group_mode or "scroll",
            "items": items,
        })
        for sp in local_sp:
            sp["gi"] = gi
            spines.append(sp)


def _mm_pair(mod):
    if not isinstance(mod, dict):
        return 0.0, 0.0
    mx = float(mod.get("scalar") or 0.0)
    mn = float(mod.get("minScalar") or 0.0)
    if int(mod.get("minMaxState") or 0) == 3:
        return (round(min(mn, mx), 4), round(max(mn, mx), 4))
    return round(mx, 4), round(mx, 4)


def _ps_tree(cref, obj):
    for cand in (getattr(obj, "object_reader", None), cref):
        if cand is None:
            continue
        try:
            return cand.read_typetree()
        except Exception:
            pass
    return None


def _color4(d, fallback=(1.0, 1.0, 1.0, 1.0)):
    if not isinstance(d, dict):
        return fallback
    return (
        round(float(d.get("r") or 0.0), 4),
        round(float(d.get("g") or 0.0), 4),
        round(float(d.get("b") or 0.0), 4),
        round(float(d.get("a") if d.get("a") is not None else 1.0), 4),
    )


def _ps_texture(env, mat_pid, sprites):
    """粒子材质贴图写进 sprites。缺依赖时用包内 fx_ring_5 / Default-ParticleSystem。"""
    if not mat_pid:
        return "Default-ParticleSystem"
    mat_obj = tex_obj = None
    for obj in env.objects:
        if obj.path_id == mat_pid and obj.type.name == "Material":
            mat_obj = obj
            break
    tex_pid = 0
    tex_file_id = 0
    if mat_obj is not None:
        try:
            mt = mat_obj.read_typetree()
            envs = ((mt.get("m_SavedProperties") or {}).get("m_TexEnvs") or [])
            for pair in envs:
                key, val = (pair[0], pair[1]) if isinstance(pair, (list, tuple)) and len(pair) >= 2 else (None, None)
                if key != "_MainTex" or not isinstance(val, dict):
                    continue
                tex = val.get("m_Texture") or {}
                tex_pid = int(tex.get("m_PathID") or 0)
                tex_file_id = int(tex.get("m_FileID") or 0)
                break
        except Exception:
            pass
    if tex_pid and tex_file_id == 0:
        for obj in env.objects:
            if obj.path_id != tex_pid:
                continue
            try:
                if obj.type.name == "Texture2D":
                    tex_obj = obj.read()
                elif obj.type.name == "Sprite":
                    spr = obj.read()
                    tex_obj = spr.m_RD.texture.read()
            except Exception:
                tex_obj = None
            break
    img = None
    name = None
    if tex_obj is not None:
        try:
            name = tex_obj.m_Name or "particle"
            img = tex_obj.image
        except Exception:
            img = None
    if img is None:
        for fallback in ("fx_ring_5", "Default-ParticleSystem", "Default-Particle"):
            for obj in env.objects:
                if obj.type.name != "Texture2D":
                    continue
                try:
                    d = obj.read()
                except Exception:
                    continue
                if (d.m_Name or "") == fallback:
                    name, img = fallback, d.image
                    break
            if img is not None:
                break
    if img is None or not name:
        return "Default-ParticleSystem"
    if name not in sprites:
        sprites[name] = img
    return name


def particle_of(go, wx, wy, rotz, sprites, env):
    ps_t = psr_t = None
    for cref, o in comps(go):
        cn = o.__class__.__name__
        if cn == "ParticleSystem":
            ps_t = _ps_tree(cref, o)
        elif cn == "ParticleSystemRenderer":
            psr_t = _ps_tree(cref, o)
    if not ps_t or not psr_t:
        return None
    em = ps_t.get("EmissionModule") or {}
    if not em.get("enabled"):
        return None
    init = ps_t.get("InitialModule") or {}
    shape = ps_t.get("ShapeModule") or {}
    col = ps_t.get("ColorModule") or {}
    size = ps_t.get("SizeModule") or {}
    noise = ps_t.get("NoiseModule") or {}
    sc = init.get("startColor") or {}
    cr, cg, cb, ca = _color4(sc.get("maxColor"))
    grad = ((col.get("gradient") or {}).get("maxGradient") or {}) if col.get("enabled") else {}
    lr, lg, lb, la = _color4(grad.get("key0"), (cr, cg, cb, ca)) if grad else (cr, cg, cb, ca)
    life_a, life_b = _mm_pair(init.get("startLifetime") or {})
    spd_a, spd_b = _mm_pair(init.get("startSpeed") or {})
    sz_a, sz_b = _mm_pair(init.get("startSize") or {})
    grav, _g2 = _mm_pair(init.get("gravityModifier") or {})
    rate_a, _r2 = _mm_pair(em.get("rateOverTime") or {})
    burst_a = burst_b = 0.0
    bursts = em.get("m_Bursts") or []
    if bursts and isinstance(bursts[0], dict):
        burst_a, burst_b = _mm_pair(bursts[0].get("countCurve") or {})
    rad = 1.0
    rd = shape.get("radius")
    if isinstance(rd, dict):
        rad = float(rd.get("value") or 1.0)
    elif rd is not None:
        rad = float(rd)
    sx = 1.0
    sca = shape.get("m_Scale") or {}
    st = int(shape.get("type") or 0)
    if isinstance(sca, dict):
        ax = abs(float(sca.get("x") or 0.0))
        ay = abs(float(sca.get("y") or 0.0))
        az = abs(float(sca.get("z") or 0.0))
        if st == 5:
            sx = max(ax, ay, 0.01)
            if az > 0.01:
                rad = az
        else:
            sx = ax if ax > 0.01 else 1.0
    mats = psr_t.get("m_Materials") or []
    mat_pid = 0
    if mats and isinstance(mats[0], dict):
        mat_pid = int(mats[0].get("m_PathID") or 0)
    tex = _ps_texture(env, mat_pid, sprites)
    return {
        "tex": tex,
        "x": round(wx, 4),
        "y": round(wy, 4),
        "rotZ": round(rotz, 3) if abs(rotz) > 0.05 else 0.0,
        "order": int(psr_t.get("m_SortingOrder") or 0),
        "dur": round(float(ps_t.get("lengthInSec") or 5.0), 4),
        "delay": round(float((ps_t.get("startDelay") or {}).get("scalar") or 0.0), 4),
        "rate": rate_a,
        "burstMin": burst_a,
        "burstMax": burst_b,
        "life": life_a,
        "life2": life_b,
        "speed": spd_a,
        "speed2": spd_b,
        "size": sz_a,
        "size2": sz_b,
        "grav": grav,
        "shape": int(shape.get("type") or 0),
        "radius": round(rad, 4),
        "shapeSX": round(sx, 4),
        "cr": cr, "cg": cg, "cb": cb, "ca": ca,
        "lr": lr, "lg": lg, "lb": lb, "la": la,
        "colorLife": 1 if col.get("enabled") else 0,
        "sizeLife": 1 if size.get("enabled") else 0,
        "noise": 1 if noise.get("enabled") else 0,
        "noiseStr": round(float((noise.get("strength") or {}).get("scalar") or 0.0), 4) if noise.get("enabled") else 0.0,
        "noiseFreq": round(float(noise.get("frequency") or 0.0), 4) if noise.get("enabled") else 0.0,
        "maxP": int(init.get("maxNumParticles") or 200),
    }


def _gi_for_name(nm, name_to_gi, gi):
    if nm in name_to_gi:
        return name_to_gi[nm]
    nl = (nm or "").lower()
    for gname, i in name_to_gi.items():
        gl = (gname or "").lower()
        if not gl:
            continue
        if nl == gl or nl == gl + "_all" or gl == nl + "_all":
            return i
    return gi


def harvest_particles(go, px, py, psx, psy, gi, name_to_gi, particles, sprites, env, rotz=0.0):
    """Sprite 层走完后再收 ParticleSystem。par_star / 联动仍跳过。"""
    nm = go.m_Name or ""
    if skip_name(nm) or is_variant(nm) or nm in ("par_star", "par_star2"):
        return
    if not bool(getattr(go, "m_IsActive", True)):
        return
    tr = transform_of(go)
    if tr is None:
        return
    lp, ls = v3(tr.m_LocalPosition), v3(tr.m_LocalScale)
    parent_rot = rotz
    rotz = parent_rot + local_euler_z(tr)
    rad = math.radians(parent_rot)
    rc, rs = math.cos(rad), math.sin(rad)
    lx, ly = lp[0] * psx, lp[1] * psy
    wx = px + rc * lx - rs * ly
    wy = py + rs * lx + rc * ly
    wsx, wsy = psx * ls[0], psy * ls[1]
    gi = _gi_for_name(nm, name_to_gi, gi)
    rec = particle_of(go, wx, wy, rotz, sprites, env)
    if rec is not None:
        rec["gi"] = int(gi)
        if rec["rate"] > 40:
            rec["maxP"] = min(int(rec["maxP"]), 80)
        particles.append(rec)
    for ch in child_gos(go):
        harvest_particles(ch, wx, wy, wsx, wsy, gi, name_to_gi, particles, sprites, env, rotz)


def clip_particles_to_groups(particles, groups):
    out = []
    by = {}
    for p in particles:
        by.setdefault(int(p.get("gi") or -1), []).append(p)
    for gi, ps in by.items():
        if gi < 0 or gi >= len(groups):
            out.extend(ps)
            continue
        g = groups[gi]
        loop = float(g.get("loop") or 0)
        origin = float(g.get("x") or 0)
        if loop < 0.5 or len(ps) < 2:
            out.extend(ps)
            continue
        xs = [float(p["x"]) for p in ps]
        lo = min(origin - 0.2, max(xs) - loop - 0.05)
        hi = max(origin + loop + 0.2, min(xs) + loop + 0.05)
        kept = [p for p in ps if lo <= float(p["x"]) < hi]
        if not kept:
            kept = ps
        cyc = []
        for it in kept:
            twin = False
            for other in kept:
                if other is it:
                    continue
                if abs(float(other["y"]) - float(it["y"])) > 0.45:
                    continue
                if (other.get("tex") or "") != (it.get("tex") or ""):
                    continue
                if abs(float(other["x"]) - (float(it["x"]) - loop)) < 0.8:
                    twin = True
                    break
            if not twin:
                cyc.append(it)
        out.extend(cyc if cyc else kept)
    return out


def harvest_scene_particles(s, out_root):
    """只补 particles + 新粒子贴图，不删背景 PNG、不重写 groups/items。"""
    bdir = _paths.bundle_dir()
    path = find_bundle(bdir, f"gamescene_{s}")
    if not path:
        raise FileNotFoundError(f"gamescene_{s} 不在 {bdir}")
    env = UnityPy.load(str(path))
    want = f"scene_{s}"
    root = find_go(env, want)
    if root is None:
        raise RuntimeError(f"scene_{s}: 无 Scene")
    if not child_gos(root):
        for alt in (want + "_miku", want + "_rin_len"):
            cand = find_go(env, alt)
            if cand is not None and child_gos(cand):
                root = cand
                break
    dest = os.path.join(out_root, "sprites", "scene_" + s, "bg")
    lay_path = os.path.join(dest, "layout.json")
    if not os.path.isfile(lay_path):
        raise FileNotFoundError(lay_path)
    with open(lay_path, encoding="utf-8") as f:
        layout = json.load(f)
    groups = layout.get("groups") or []
    name_to_gi = {str(g.get("name") or ""): i for i, g in enumerate(groups)}
    sprites = {}
    particles = []
    tr = transform_of(root)
    rp = v3(tr.m_LocalPosition) if tr is not None else (0.0, 0.0, 0.0)
    rs = v3(tr.m_LocalScale) if tr is not None else (1.0, 1.0, 1.0)
    for ch in child_gos(root):
        harvest_particles(ch, rp[0], rp[1], rs[0], rs[1], -1, name_to_gi, particles, sprites, env)
    particles = clip_particles_to_groups(particles, groups)
    os.makedirs(dest, exist_ok=True)
    for name, img in sprites.items():
        fn = name.replace("/", "_") + ".png"
        outp = os.path.join(dest, fn)
        if not os.path.isfile(outp):
            img.save(outp)
        files = layout.get("sprites") or []
        if not any((sp.get("name") or "") == name for sp in files):
            files.append({"name": name, "file": fn, "w": img.width, "h": img.height})
            layout["sprites"] = files
    layout["particles"] = particles
    with open(lay_path, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, separators=(",", ":"))
    return len(particles), sorted(sprites.keys())


def extract_scene(s, out_root):
    bdir = _paths.bundle_dir()
    path = find_bundle(bdir, f"gamescene_{s}")
    env = UnityPy.load(path)
    want = f"scene_{s}"
    root = find_go(env, want)
    if root is None:
        return None, "no root " + want
    if not child_gos(root):
        for alt in (want + "_miku", want + "_rin_len"):
            cand = find_go(env, alt)
            if cand is not None and child_gos(cand):
                root = cand
                break
    tr = transform_of(root)
    rp, rs = v3(tr.m_LocalPosition), v3(tr.m_LocalScale)
    groups = []
    sprites = {}
    spines = []
    reset_spins()
    skel_names, skel_scales = skeleton_index(env)
    clips = clip_index(env)
    moves = []
    add_layers(root, rp[0], rp[1], rs[0], rs[1], groups, sprites, spines, skel_names, clips, moves=moves)
    particles = []
    name_to_gi = {g.get("name"): i for i, g in enumerate(groups) if g.get("name")}
    for ch in child_gos(root):
        harvest_particles(ch, rp[0], rp[1], rs[0], rs[1], -1, name_to_gi, particles, sprites, env)
    particles = clip_particles_to_groups(particles, groups)
    dest = os.path.join(out_root, "sprites", "scene_" + s, "bg")
    os.makedirs(dest, exist_ok=True)
    for old in glob.glob(os.path.join(dest, "*.png")):
        os.remove(old)
    files = []
    for name, img in sprites.items():
        fn = name.replace("/", "_") + ".png"
        img.save(os.path.join(dest, fn))
        files.append({"name": name, "file": fn, "w": img.width, "h": img.height})
    # groups 不内嵌 items：Unity JsonUtility 嵌套数组经常解成空。
    flat = []
    slim = []
    for i, g in enumerate(groups):
        rec = {k: g[k] for k in ("name", "x", "y", "loop", "period", "loopW", "periodS", "belt", "mode") if k in g}
        if abs(float(g.get("phase") or 0)) > 0.001:
            rec["phase"] = g["phase"]
        nm = g.get("name") or ""
        # 52.6 只拟合 scene_02 街景地标；城堡水面/地板套上会错位相。
        if s == "02" and nm in ("Floor", "Water", "Mid_2"):
            rec["phase"] = 52.6
        slim.append(rec)
        for it in g["items"]:
            rec = dict(it)
            rec["gi"] = i
            flat.append(rec)
    if s == "10":
        flip_s10_spotlights_h([{"items": flat}])
    # moveT/X/Y 放 layout 顶层：JsonUtility 解 PopMove 里的 float[] 经常是空的，位移就不会播。
    layout = {"scene": "scene_" + s, "groups": slim, "items": flat, "spines": spines, "sprites": files, "moves": moves, "particles": particles}
    if moves:
        m0 = moves[0]
        layout["movePeriod"] = m0.get("period") or 2.8167
        layout["moveT"] = m0.get("ts") or []
        layout["moveX"] = m0.get("xs") or []
        layout["moveY"] = m0.get("ys") or []
        layout["moveStep"] = 1 if m0.get("step") else 0
        if len(moves) >= 2 and moves[1].get("path"):
            m1 = moves[1]
            layout["movePeriod2"] = m1.get("period") or 0
            layout["moveT2"] = m1.get("ts") or []
            layout["moveX2"] = m1.get("xs") or []
            layout["moveY2"] = m1.get("ys") or []
    if _SPIN_CURVES:
        layout["spinPeriod"] = _SPIN_CURVES[0]["period"]
        layout["spinT"] = _SPIN_CURVES[0]["ts"]
        flat_z = []
        for c in _SPIN_CURVES:
            flat_z.extend(c["zs"])
        layout["spinZ"] = flat_z
    with open(os.path.join(dest, "layout.json"), "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False)
    n = len(flat)
    packs = sorted({sp["pack"] for sp in spines})
    extra = f" 骨{len(spines)}/{len(packs)}" if spines else ""
    if particles:
        extra += f" 粒子{len(particles)}"
    # 场景装饰骨骼官机 scale=0.005（extract_md_spine 曾写成 0.01）。
    spine_root = os.path.join(out_root, "spine", "scene_" + s)
    for pack in packs:
        sc = next((skel_scales[pid] for pid, nm in skel_names.items() if nm == pack), 0.005)
        folder = os.path.join(spine_root, pack)
        if os.path.isdir(folder):
            with open(os.path.join(folder, "scale.json"), "w", encoding="utf-8") as f:
                json.dump({"scale": float(sc)}, f)
    return layout, f"{len(groups)} 层 {n} 片 {len(files)} 图{extra}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scene", default="")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--particles", action="store_true", help="只补粒子，不重抽背景")
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()
    scenes = [f"{n:02d}" for n in range(1, 13)] if args.all or not args.scene else [args.scene.zfill(2)]
    os.makedirs(args.out, exist_ok=True)
    git = os.path.join(args.out, ".gitignore")
    if not os.path.isfile(git):
        with open(git, "w", encoding="utf-8") as f:
            f.write("# 仅本机\n*\n!.gitignore\n")
    for s in scenes:
        try:
            if args.particles:
                n, tex = harvest_scene_particles(s, args.out)
                print(f"scene_{s} particles: {n} {tex}")
            else:
                _, msg = extract_scene(s, args.out)
                print(f"scene_{s} bg: {msg}")
        except FileNotFoundError as e:
            print(e)
        except Exception as e:
            print(f"scene_{s} bg 失败: {e}")
    if args.particles:
        return
    print("Unity MdLooks 已更新：", args.out)


if __name__ == "__main__":
    main()
