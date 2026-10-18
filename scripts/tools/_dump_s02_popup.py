# -*- coding: utf-8 -*-
"""Dump official scene_02 Mid_H / bg_white / mid_F2 (heart) vs current layout."""
import json
import os
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
sys.path.insert(0, SCRIPTS)
import _paths
import extract_md_scene_bg as bg


def sr_info(go):
    sr = None
    order = draw = None
    size = None
    color = None
    active = bool(getattr(go, "m_IsActive", True))
    for _, o in bg.comps(go):
        if o.__class__.__name__ != "SpriteRenderer":
            continue
        sr = o
        try:
            order = int(o.m_SortingOrder)
        except Exception:
            pass
        try:
            draw = int(getattr(o, "m_DrawMode", 0) or 0)
        except Exception:
            pass
        try:
            sz = getattr(o, "m_Size", None)
            if sz is not None:
                size = (round(float(sz.x), 4), round(float(sz.y), 4))
        except Exception:
            pass
        try:
            c = o.m_Color
            color = (round(float(c.r), 3), round(float(c.g), 3), round(float(c.b), 3), round(float(c.a), 3))
        except Exception:
            pass
    if sr is None:
        return None
    try:
        sp = sr.m_Sprite.read()
        spr = sp.m_Name
        ppu = float(sp.m_PixelsToUnits) or 100.0
        pv = sp.m_Pivot
        pivot = (round(float(pv.x), 4), round(float(pv.y), 4))
        r = sp.m_Rect
        rect = (round(float(r.width), 2), round(float(r.height), 2))
        try:
            b = sp.m_Border
            border = (round(float(b.x), 2), round(float(b.y), 2), round(float(b.z), 2), round(float(b.w), 2))
        except Exception:
            border = None
    except Exception:
        return {"spr": "?", "order": order, "draw": draw, "size": size, "active": active, "color": color}
    return {
        "spr": spr, "ppu": ppu, "pivot": pivot, "rect": rect, "border": border,
        "order": order, "draw": draw, "size": size, "active": active, "color": color,
    }


def walk(go, px, py, psx, psy, path, rows, want):
    tr = bg.transform_of(go)
    if tr is None:
        return
    lp, ls = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    wx = px + lp[0] * psx
    wy = py + lp[1] * psy
    wsx, wsy = psx * ls[0], psy * ls[1]
    nm = go.m_Name or ""
    pth = path + "/" + nm
    info = sr_info(go)
    hit = False
    if info:
        spr = (info.get("spr") or "").lower()
        if want(nm, spr, pth):
            hit = True
    elif want(nm, "", pth):
        hit = True
    if hit:
        rec = {
            "path": pth,
            "name": nm,
            "active": bool(getattr(go, "m_IsActive", True)),
            "local": (round(lp[0], 4), round(lp[1], 4)),
            "scale": (round(ls[0], 4), round(ls[1], 4)),
            "world": (round(wx, 4), round(wy, 4)),
            "wscale": (round(wsx, 4), round(wsy, 4)),
        }
        if info:
            rec.update(info)
            if info.get("rect") and info.get("ppu"):
                rw, rh = info["rect"]
                ppu = info["ppu"]
                if info.get("draw") in (1, 2) and info.get("size"):
                    raw_w = info["size"][0] * wsx
                    raw_h = info["size"][1] * wsy
                else:
                    raw_w = (rw / ppu) * wsx
                    raw_h = (rh / ppu) * wsy
                pv = info.get("pivot") or (0, 0)
                cx = wx + (0.5 - pv[0]) * abs(raw_w)
                cy = wy + (0.5 - pv[1]) * abs(raw_h)
                rec["raw"] = (round(abs(raw_w), 4), round(abs(raw_h), 4))
                rec["center"] = (round(cx, 4), round(cy, 4))
        rows.append(rec)
    for ch in bg.child_gos(go):
        walk(ch, wx, wy, wsx, wsy, pth, rows, want)


def want(nm, spr, pth):
    n = nm.lower()
    s = spr.lower()
    p = pth.lower()
    if "mid_h" in n or "mid_h" in s or "/mid_h" in p:
        return True
    if s in ("bg_white", "white") or n in ("bg_white", "white"):
        return True
    if "mid_f2" in n or "mid_f2" in s or "heart" in n or "heart" in s:
        return True
    if s.startswith("mid_f") or n.startswith("mid_f"):
        return True
    return False


def main():
    path = bg.find_bundle(_paths.bundle_dir(), "gamescene_02")
    print("bundle", path)
    env = bg.UnityPy.load(path)
    root = bg.find_go(env, "scene_02")
    tr = bg.transform_of(root)
    rp, rs = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    rows = []
    walk(root, rp[0], rp[1], rs[0], rs[1], "", rows, want)
    print("official hits", len(rows))

    # summarize groups
    by = defaultdict(list)
    for r in rows:
        key = r.get("spr") or r["name"]
        by[key].append(r)
    for key in sorted(by):
        items = by[key]
        act = sum(1 for i in items if i.get("active") and i.get("spr"))
        print(f"\n=== {key} n={len(items)} spr_active={act} ===")
        shown = 0
        for i in items:
            if not i.get("spr"):
                continue
            if not i.get("active") and "add" not in (i.get("name") or "").lower() and "mid_h1" not in (i.get("name") or "").lower():
                continue
            print(
                f"  {i['name']!r} act={i.get('active')} world={i.get('world')} "
                f"center={i.get('center')} raw={i.get('raw')} draw={i.get('draw')} "
                f"size={i.get('size')} scale={i.get('scale')} order={i.get('order')} "
                f"color={i.get('color')} path={i['path'][-80:]}"
            )
            shown += 1
            if shown >= 40:
                print("  ...")
                break

    lay = os.path.join(
        os.path.dirname(SCRIPTS), "PlayClient", "Unity", "Assets", "StreamingAssets",
        "MdLooks", "sprites", "scene_02", "bg", "layout.json",
    )
    d = json.loads(open(lay, encoding="utf-8").read())
    print("\n\n===== CURRENT LAYOUT =====")
    for it in d["items"]:
        spr = it.get("spr") or ""
        if spr in ("bg_white",) or spr.startswith("mid_H") or spr.startswith("mid_F2") or "heart" in spr.lower() or spr.startswith("mid_F"):
            print(
                f"  {spr} gi={it.get('gi')} x={it['x']} y={it['y']} w={it['w']} h={it['h']} "
                f"order={it.get('order')} slice=({it.get('sliceL')},{it.get('sliceR')}) "
                f"cap=({it.get('capL')},{it.get('capR')}) pop={it.get('popAt')}/{it.get('popPeriod')} "
                f"move={it.get('moveId')} mx={it.get('mx')}"
            )


if __name__ == "__main__":
    main()
