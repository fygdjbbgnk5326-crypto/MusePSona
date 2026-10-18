# -*- coding: utf-8 -*-
"""Official Mid_1 only: Mid_H / warning / heart / bg_white frames."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SCRIPTS = os.path.dirname(HERE)
sys.path.insert(0, SCRIPTS)
import _paths
import extract_md_scene_bg as bg


def sr_info(go):
    out = {"name": go.m_Name, "active": bool(getattr(go, "m_IsActive", True))}
    for _, o in bg.comps(go):
        if o.__class__.__name__ != "SpriteRenderer":
            continue
        try:
            out["order"] = int(o.m_SortingOrder)
        except Exception:
            pass
        try:
            out["draw"] = int(getattr(o, "m_DrawMode", 0) or 0)
        except Exception:
            pass
        try:
            sz = getattr(o, "m_Size", None)
            if sz is not None:
                out["size"] = (round(float(sz.x), 4), round(float(sz.y), 4))
        except Exception:
            pass
        try:
            c = o.m_Color
            out["color"] = (round(c.r, 3), round(c.g, 3), round(c.b, 3), round(c.a, 3))
        except Exception:
            pass
        try:
            sp = o.m_Sprite.read()
            out["spr"] = sp.m_Name
            out["ppu"] = float(sp.m_PixelsToUnits) or 100.0
            pv = sp.m_Pivot
            out["pivot"] = (round(float(pv.x), 4), round(float(pv.y), 4))
            r = sp.m_Rect
            out["rect"] = (round(float(r.width), 2), round(float(r.height), 2))
        except Exception:
            out["spr"] = "?"
    return out


def walk(go, px, py, psx, psy, path, acc, clip_map):
    if bg.skip_name(go.m_Name) or bg.is_variant(go.m_Name):
        return
    if not bool(getattr(go, "m_IsActive", True)):
        # still record inactive add frames
        pass
    tr = bg.transform_of(go)
    if tr is None:
        return
    lp, ls = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    wx, wy = px + lp[0] * psx, py + lp[1] * psy
    wsx, wsy = psx * ls[0], psy * ls[1]
    pth = path + "/" + (go.m_Name or "")
    info = sr_info(go)
    spr = (info.get("spr") or "")
    nm = (go.m_Name or "")
    keep = (
        spr in ("bg_white", "warning", "mid_H7", "mid_H5", "mid_H4", "mid_H3", "mid_H6", "s02_Fx_heart", "mid_H")
        or nm.startswith("Mid_H") or nm.startswith("mid_H") or "heart" in nm.lower()
        or nm == "warning" or "warning" in pth.lower()
    )
    if keep and "Cytus" not in pth and "Touhou" not in pth and "Arknights" not in pth and "Wacca" not in pth:
        raw = None
        center = None
        if info.get("rect") and info.get("ppu"):
            if info.get("draw") in (1, 2) and info.get("size"):
                rw, rh = info["size"][0] * wsx, info["size"][1] * wsy
            else:
                rw = info["rect"][0] / info["ppu"] * wsx
                rh = info["rect"][1] / info["ppu"] * wsy
            pv = info.get("pivot") or (0, 0)
            raw = (round(abs(rw), 4), round(abs(rh), 4))
            center = (round(wx + (0.5 - pv[0]) * abs(rw), 4), round(wy + (0.5 - pv[1]) * abs(rh), 4))
        loop, period = bg.parse_scroll(go, clip_map)
        loc = bg.local_motion(go, clip_map, 0.0)
        acc.append({
            **info,
            "path": pth,
            "world": (round(wx, 4), round(wy, 4)),
            "wscale": (round(wsx, 4), round(wsy, 4)),
            "local": (round(lp[0], 4), round(lp[1], 4)),
            "raw": raw,
            "center": center,
            "loop": loop,
            "period": period,
            "loc": loc,
        })
    for ch in bg.child_gos(go):
        walk(ch, wx, wy, wsx, wsy, pth, acc, clip_map)


def main():
    env = bg.UnityPy.load(bg.find_bundle(_paths.bundle_dir(), "gamescene_02"))
    root = bg.find_go(env, "scene_02")
    tr = bg.transform_of(root)
    rp, rs = bg.v3(tr.m_LocalPosition), bg.v3(tr.m_LocalScale)
    clips = bg.clip_index(env)
    acc = []
    walk(root, rp[0], rp[1], rs[0], rs[1], "", acc, clips)
    # Floor_A Mid_1 only
    acc = [r for r in acc if "/Mid_1/" in r["path"] and "Floor_A" in r["path"]]
    print("rows", len(acc))
    for r in acc:
        spr = r.get("spr") or ""
        if spr not in ("warning", "mid_H7", "mid_H5", "s02_Fx_heart", "mid_H3", "mid_H4", "mid_H6", "mid_H") and "heart" not in (r.get("name") or "").lower():
            if spr == "bg_white" and ("Mid_H" in r["path"] or "mid_H" in r["path"]):
                pass
            else:
                continue
        print(
            f"{r.get('name')!r:16} spr={spr:14} act={r.get('active')} "
            f"c={r.get('center')} raw={r.get('raw')} order={r.get('order')} "
            f"draw={r.get('draw')} loop={r.get('loop')} per={r.get('period')} loc={r.get('loc')} "
            f"path=...{r['path'][-70:]}"
        )


if __name__ == "__main__":
    main()
