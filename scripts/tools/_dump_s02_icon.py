# -*- coding: utf-8 -*-
"""Dump official scene_02 mid_I_machines_icon."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
sys.path.insert(0, SCRIPTS)
import _paths
import extract_md_scene_bg as bg


def sr_info(go):
    info = {"name": go.m_Name, "active": bool(getattr(go, "m_IsActive", True))}
    for _, o in bg.comps(go):
        if o.__class__.__name__ != "SpriteRenderer":
            continue
        try:
            info["order"] = int(o.m_SortingOrder)
        except Exception:
            pass
        try:
            info["draw"] = int(getattr(o, "m_DrawMode", 0) or 0)
        except Exception:
            pass
        try:
            sz = getattr(o, "m_Size", None)
            if sz is not None:
                info["size"] = (round(float(sz.x), 4), round(float(sz.y), 4))
        except Exception:
            pass
        try:
            c = o.m_Color
            info["color"] = (round(c.r, 3), round(c.g, 3), round(c.b, 3), round(c.a, 3))
        except Exception:
            pass
        try:
            info["flip"] = (bool(getattr(o, "m_FlipX", False)), bool(getattr(o, "m_FlipY", False)))
        except Exception:
            pass
        try:
            sp = o.m_Sprite.read()
            info["spr"] = sp.m_Name
            info["ppu"] = float(sp.m_PixelsToUnits) or 100.0
            pv = sp.m_Pivot
            info["pivot"] = (round(float(pv.x), 4), round(float(pv.y), 4))
            r = sp.m_Rect
            info["rect"] = (round(float(r.width), 2), round(float(r.height), 2))
        except Exception:
            info["spr"] = "?"
    return info


def walk(go, px, py, psx, psy, path, acc, clip_map):
    if bg.is_variant(go.m_Name):
        return
    tr = bg.transform_of(go)
    if tr is None:
        return
    lp, ls = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    wx, wy = px + lp[0] * psx, py + lp[1] * psy
    wsx, wsy = psx * ls[0], psy * ls[1]
    pth = path + "/" + (go.m_Name or "")
    nm = (go.m_Name or "").lower()
    info = sr_info(go)
    spr = (info.get("spr") or "").lower()
    if "icon" in nm or "icon" in spr or nm.endswith("machines") or "machines" in nm:
        if "cytus" in pth.lower() or "touhou" in pth.lower() or "arknight" in pth.lower() or "wacca" in pth.lower():
            pass
        elif "floor_a" in pth.lower() and "/mid_1/" in pth.lower():
            raw = center = None
            if info.get("rect") and info.get("ppu"):
                rw = info["rect"][0] / info["ppu"] * wsx
                rh = info["rect"][1] / info["ppu"] * wsy
                pv = info.get("pivot") or (0, 0)
                raw = (round(abs(rw), 4), round(abs(rh), 4))
                center = (
                    round(wx + (0.5 - pv[0]) * abs(rw), 4),
                    round(wy + (0.5 - pv[1]) * abs(rh), 4),
                )
            loc = bg.local_motion(go, clip_map, 0.0)
            acc.append({
                **info,
                "path": pth,
                "world": (round(wx, 4), round(wy, 4)),
                "local": (round(lp[0], 4), round(lp[1], 4)),
                "scale": (round(ls[0], 4), round(ls[1], 4)),
                "wscale": (round(wsx, 4), round(wsy, 4)),
                "raw": raw,
                "center": center,
                "loc": loc,
                "loop": bg.parse_scroll(go, clip_map),
            })
    for ch in bg.child_gos(go):
        walk(ch, wx, wy, wsx, wsy, pth, acc, clip_map)


def main():
    env = bg.UnityPy.load(bg.find_bundle(_paths.bundle_dir(), "gamescene_02"))
    root = bg.find_go(env, "scene_02")
    tr = bg.transform_of(root)
    rp, rs = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    acc = []
    walk(root, rp[0], rp[1], rs[0], rs[1], "", acc, bg.clip_index(env))
    print("hits", len(acc))
    for r in acc:
        if "icon" not in (r.get("name") or "").lower() and "icon" not in (r.get("spr") or "").lower():
            continue
        print(
            f"{r.get('name')!r:28} act={r.get('active')} spr={r.get('spr')} "
            f"c={r.get('center')} raw={r.get('raw')} order={r.get('order')} "
            f"flip={r.get('flip')} color={r.get('color')} scale={r.get('scale')} "
            f"loc={r.get('loc')} loop={r.get('loop')} path=...{r['path'][-90:]}"
        )


if __name__ == "__main__":
    main()
