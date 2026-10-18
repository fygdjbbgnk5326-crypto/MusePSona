# -*- coding: utf-8 -*-
"""Dump official gamescene_06 Water / water_gradient transforms."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
sys.path.insert(0, SCRIPTS)
import _paths
import extract_md_scene_bg as bg


def dump_go(go, px, py, psx, psy, indent=0):
    tr = bg.transform_of(go)
    if tr is None:
        return
    lp, ls = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    wx = px + lp[0] * psx
    wy = py + lp[1] * psy
    wsx, wsy = psx * ls[0], psy * ls[1]
    sr = None
    order = None
    draw = None
    size = None
    flip = (False, False)
    color = None
    for _, o in bg.comps(go):
        if o.__class__.__name__ == "SpriteRenderer":
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
                    size = (float(sz.x), float(sz.y))
            except Exception:
                pass
            try:
                flip = (bool(getattr(o, "m_FlipX", False)), bool(getattr(o, "m_FlipY", False)))
            except Exception:
                pass
            try:
                c = o.m_Color
                color = (float(c.r), float(c.g), float(c.b), float(c.a))
            except Exception:
                pass
    spr = ppu = pivot = rect = None
    if sr is not None:
        try:
            sp = sr.m_Sprite.read()
            spr = sp.m_Name
            ppu = float(sp.m_PixelsToUnits)
            pv = sp.m_Pivot
            pivot = (float(pv.x), float(pv.y))
            r = sp.m_Rect
            rect = (float(r.width), float(r.height))
        except Exception as e:
            spr = f"err:{e}"
    pad = "  " * indent
    print(
        f"{pad}{go.m_Name!r} active={getattr(go, 'm_IsActive', True)} "
        f"local=({lp[0]:.4f},{lp[1]:.4f}) scale=({ls[0]:.4f},{ls[1]:.4f}) "
        f"world=({wx:.4f},{wy:.4f}) wscale=({wsx:.4f},{wsy:.4f})"
    )
    if spr:
        print(
            f"{pad}  spr={spr} ppu={ppu} pivot={pivot} rect={rect} "
            f"order={order} draw={draw} size={size} flip={flip} color={color}"
        )
        if rect and ppu:
            raw_w = (rect[0] / ppu) * wsx
            raw_h = (rect[1] / ppu) * wsy
            ox = (0.5 - pivot[0]) * abs(raw_w)
            oy = (0.5 - pivot[1]) * abs(raw_h)
            print(f"{pad}  raw=({raw_w:.4f},{raw_h:.4f}) center=({wx+ox:.4f},{wy+oy:.4f})")
    for ch in bg.child_gos(go):
        dump_go(ch, wx, wy, wsx, wsy, indent + 1)


def main():
    path = bg.find_bundle(_paths.bundle_dir(), "gamescene_06")
    print("bundle", path)
    env = bg.UnityPy.load(path)
    root = bg.find_go(env, "scene_06")
    tr = bg.transform_of(root)
    rp, rs = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    print("root", rp, rs)
    ac = bg.animator_clip(root)
    print("root clip", ac.m_Name if ac else None)
    for go in bg.child_gos(root):
        nm = go.m_Name or ""
        if "water" in nm.lower() or nm == "Near" or nm == "Default":
            loop, period = bg.parse_scroll(go, bg.clip_index(env))
            print("===", nm, "loop", loop, "period", period, "=== ")
            dump_go(go, rp[0], rp[1], rs[0], rs[1])


if __name__ == "__main__":
    main()
