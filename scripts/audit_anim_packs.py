# -*- coding: utf-8 -*-
"""对照播放器 ANIM_SPEC，清点已打包 / 素材整合 里各场景逐帧是否够用。

  python scripts/audit_anim_packs.py
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANIM = os.path.join(ROOT, "play", "mdm_player", "assets", "md", "anims")
GRAB = os.path.join(os.path.dirname(ROOT), "Muse dash mod支持", "MDSceneGrabber", "素材整合")

CODES = ["01", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17"]
WANT = {
    "01": ["standby"],
    "03": ["in_"],
    "04": ["in_", "hurt_1", "out_p"],
    "05": ["in_"],
    "06": ["in_boss"],
    "07": ["in_boss"],
    "08": ["in_boss"],
    "09": ["boss_1_nor"],
    "10": ["in_nor", "in_up", "in_down", "out_p"],
    "11": ["in_nor", "in_up", "in_down", "out_p"],
    "12": ["in_nor", "in_up", "in_down"],
    "13": ["in_nor"],
    "14": ["in_nor"],
    "15": ["in_num"],
    "16": ["in_up"],
    "17": ["in_nor"],
}
IBMS = {
    "01/02/03": "10", "04/05/06": "11", "07/08/09": "12", "0A": "13", "0B": "14",
    "0C/0I": "16", "0D/0J": "15", "0E": "05", "0G": "04", "0H": "03", "18": "09",
    "13": "06", "14": "07", "15": "08", "21": "17", "Boss待机": "01",
}


def kinds(anims):
    out = set()
    hits = {"in_nor": [], "in_up": [], "in_down": [], "in_num": [], "in_boss": [], "boss_1_nor": []}
    for n, a in anims.items():
        h = a.get("hit")
        if n.startswith("in_nor_"):
            out.add("in_nor"); hits["in_nor"].append(h)
        elif n.startswith("in_up_"):
            out.add("in_up"); hits["in_up"].append(h)
        elif n.startswith("in_down_"):
            out.add("in_down"); hits["in_down"].append(h)
        elif n.startswith("in_boss_"):
            out.add("in_boss"); hits["in_boss"].append(h)
        elif re.fullmatch(r"in_\d+", n):
            out.add("in_num"); hits["in_num"].append(h)
        elif n.startswith("boss_1_nor_"):
            out.add("boss_1_nor"); hits["boss_1_nor"].append(h)
        elif n.startswith("hurt_"):
            out.add(n if n in ("hurt_1", "hurt_2", "hurt_3", "hurt_4") else "hurt")
        elif n in ("out_p", "out_g", "standby", "normal_standby"):
            out.add(n)
        elif n.startswith("in_"):
            out.add("in_")
        else:
            out.add(n)
    if any(x.startswith("in_") or x in ("in_nor", "in_up", "in_down", "in_num", "in_boss") for x in out):
        out.add("in_")
    return out, {k: sorted({h for h in v if h}, reverse=True) for k, v in hits.items()}


def grade(code, got):
    miss = []
    for n in WANT[code]:
        if n == "in_":
            if not any(x.startswith("in") or x in ("in_nor", "in_up", "in_down", "in_num", "in_boss") for x in got):
                miss.append("in_*")
        elif n == "in_num":
            if "in_num" not in got:
                miss.append("in_NN")
        elif n not in got:
            miss.append(n)
    if not miss:
        return "OK", []
    if any(x.startswith("in") or x == "in_" for x in got) or "standby" in got:
        return "PARTIAL", miss
    return "MISSING", miss


def load_pack(scene):
    p = os.path.join(ANIM, scene, scene + ".json")
    return json.load(open(p, encoding="utf-8")) if os.path.isfile(p) else None


def scan_mod_frames(scene):
    alias_path = os.path.join(GRAB, "scene_alias.json")
    aliases = json.load(open(alias_path, encoding="utf-8-sig")) if os.path.isfile(alias_path) else {}
    cand = [scene, aliases.get(scene, "")]
    index = os.path.join(GRAB, "index.json")
    if os.path.isfile(index):
        d = (json.load(open(index, encoding="utf-8-sig")).get("scenes") or {}).get(scene, {}).get("dir")
        if d:
            cand.append(d)
    mod = None
    for c in cand:
        if c and os.path.isfile(os.path.join(GRAB, c, "mod", "manifest.json")):
            mod = os.path.join(GRAB, c, "mod")
            break
    if not mod:
        return None, {}
    man = json.load(open(os.path.join(mod, "manifest.json"), encoding="utf-8-sig"))
    els = {}
    for e in man.get("entries") or []:
        if e.get("type") != "spine":
            continue
        key = (e.get("dir") or "").split("/")[-1]
        for a in e.get("animations") or []:
            fr = a.get("frames") or {}
            n = int(fr.get("count") or 0)
            if n <= 0:
                continue
            d = fr.get("dir") or ""
            png = os.path.join(mod, *d.replace("\\", "/").split("/"), "0000.png") if d else ""
            els.setdefault(key, []).append((a["name"], n, os.path.isfile(png) if png else False))
    return man.get("scene") or scene, els


def main():
    packed = []
    if os.path.isdir(ANIM):
        packed = [d for d in sorted(os.listdir(ANIM)) if os.path.isfile(os.path.join(ANIM, d, d + ".json"))]
    src_scenes = set()
    idx = os.path.join(GRAB, "index.json")
    if os.path.isfile(idx):
        src_scenes.update((json.load(open(idx, encoding="utf-8-sig")).get("scenes") or {}).keys())
    alias_path = os.path.join(GRAB, "scene_alias.json")
    if os.path.isfile(alias_path):
        src_scenes.update(json.load(open(alias_path, encoding="utf-8-sig")))
    if os.path.isdir(GRAB):
        for d in os.listdir(GRAB):
            if re.fullmatch(r"scene_\d\d", d):
                src_scenes.add(d)

    print("ibms -> xNN:", IBMS)
    print("packed:", packed or "(none)")
    scenes = sorted(s for s in (src_scenes | set(packed)) if re.fullmatch(r"scene_\d\d", s))
    for scene in scenes:
        pack = load_pack(scene)
        src_name, src = scan_mod_frames(scene)
        print("\n====", scene, "====")
        print("  pack:", ("%d pages, %d els" % (len(pack.get("pages") or []), len(pack.get("elements") or {}))) if pack else "none")
        print("  src:", "%s, %d framed" % (src_name, len(src)) if src is not None else "no mod/manifest")
        for code in CODES:
            matches = []
            if pack:
                for k, v in pack["elements"].items():
                    if len(k) >= 4 and k[2:4] == code:
                        matches.append((k, v))
            src_m = [(k, a) for k, a in (src or {}).items() if len(k) >= 4 and k[2:4] == code]
            if not matches and not src_m:
                print("  x%s  MISSING" % code)
                continue
            if matches:
                names, all_kinds = [], set()
                for k, v in matches:
                    kd, hits = kinds(v)
                    all_kinds |= kd
                    names.append("%s[%s]" % (k, ",".join("%s%s" % (hk, hits[hk] or "") for hk in hits if hits[hk])))
                st, miss = grade(code, all_kinds)
                extra = ("  miss " + ",".join(miss)) if miss else ""
                print("  x%s  %s  %s%s" % (code, st, "; ".join(names), extra))
            else:
                on = any(a[2] for _, anims in src_m for a in anims)
                print("  x%s  SRC_ONLY disk=%s" % (code, on))


if __name__ == "__main__":
    sys.exit(main() or 0)
