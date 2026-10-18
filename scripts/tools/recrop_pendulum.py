# -*- coding: utf-8 -*-
"""从各场景 0315 Spine atlas 重裁摆锤 wooden，写回 MdLooks/sprites 与 mdm_player。"""
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SPINE = ROOT / "PlayClient/Unity/Assets/StreamingAssets/MdLooks/spine"
SPRITE_A = ROOT / "PlayClient/Unity/Assets/StreamingAssets/MdLooks/sprites"
SPRITE_B = ROOT / "play/mdm_player/assets/md/sprites"
PREVIEW = ROOT / "play/_pendulum_preview"


def parse_atlas(text):
    regions, page, cur = {}, None, None
    expect_page = True
    atlas_wh = None
    for raw in text.splitlines():
        line = raw.rstrip()
        if not line.strip():
            expect_page, cur = True, None
            continue
        if expect_page:
            page, expect_page = line.strip(), False
            continue
        if ":" in line and (raw.startswith((" ", "\t")) or cur is None):
            k, v = (s.strip() for s in line.split(":", 1))
            if k == "size" and atlas_wh is None:
                try:
                    atlas_wh = tuple(int(x) for x in v.split(","))
                except ValueError:
                    pass
            if cur is not None:
                cur[k] = v
            continue
        cur = {"page": page}
        regions[line.strip()] = cur
    out = {}
    for name, r in regions.items():
        try:
            x, y = (int(v) for v in r["xy"].split(","))
            w, h = (int(v) for v in r["size"].split(","))
        except (KeyError, ValueError):
            continue
        rot = r.get("rotate", "false")
        deg = 90 if rot == "true" else 0 if rot == "false" else int(rot)
        out[name] = {"x": x, "y": y, "w": w, "h": h, "deg": deg}
    return out, atlas_wh


def packed(r):
    return (r["h"], r["w"]) if r["deg"] in (90, 270) else (r["w"], r["h"])


def crop(im, r, atlas_wh, mode):
    pw, ph = packed(r)
    aw, ah = atlas_wh
    iw, ih = im.size
    x, y = float(r["x"]), float(r["y"])
    pad = max(0, ih - ah)
    if mode == "png_bl":
        yy = ih - y - ph
    elif mode == "atlas_bl_toppad":
        yy = pad + (ah - y - ph)
    elif mode == "atlas_bl":
        yy = ah - y - ph
    elif mode == "tl":
        yy = y
    elif mode == "tl_pad":
        # libGDX y-down on the logical atlas, extra POT rows above.
        yy = pad + y
    elif mode == "scale":
        x = x * iw / aw
        yb = y * ih / ah
        pw = max(1, pw * iw / aw)
        ph = max(1, ph * ih / ah)
        yy = yb if ih <= ah else (ih - yb - ph)
    else:
        raise ValueError(mode)
    x0, y0 = int(round(x)), int(round(yy))
    x1, y1 = int(round(x + pw)), int(round(yy + ph))
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(iw, x1), min(ih, y1)
    if x1 - x0 < 4 or y1 - y0 < 4:
        return None
    img = im.crop((x0, y0, x1, y1))
    if r["deg"]:
        img = img.rotate(-r["deg"], expand=True)
    return img


def blobs(img):
    a = img.split()[-1].resize((24, 24), Image.NEAREST)
    w, h = a.size
    seen = [[False] * w for _ in range(h)]
    n = 0
    for y in range(h):
        for x in range(w):
            if seen[y][x] or a.getpixel((x, y)) < 16:
                continue
            n += 1
            stack = [(x, y)]
            seen[y][x] = True
            while stack:
                cx, cy = stack.pop()
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and a.getpixel((nx, ny)) >= 16:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
    return n


def score(img):
    """Prefer one yo-yo/doll, not a packed atlas strip."""
    a = img.split()[-1]
    px = a.tobytes()
    n = len(px)
    nz = sum(1 for v in px if v > 12)
    if nz < 80:
        return -1e9
    w, h = img.size
    corners = 0
    for x, y in ((1, 1), (w - 2, 1), (1, h - 2), (w - 2, h - 2)):
        if 0 <= x < w and 0 <= y < h and a.getpixel((x, y)) < 12:
            corners += 1
    b = blobs(img)
    fill = nz / n
    return 25000 - b * 8000 + corners * 3000 - abs(fill - 0.62) * 4000


def all_components(img, alpha=16, min_px=80):
    a = img.split()[-1]
    w, h = a.size
    pix = a.load()
    seen = [[False] * w for _ in range(h)]
    comps = []
    for y in range(h):
        for x in range(w):
            if seen[y][x] or pix[x, y] < alpha:
                continue
            stack = [(x, y)]
            seen[y][x] = True
            pts = []
            while stack:
                cx, cy = stack.pop()
                pts.append((cx, cy))
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and pix[nx, ny] >= alpha:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if len(pts) < min_px:
                continue
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
            comps.append((box, img.crop(box)))
    comps.sort(key=lambda t: t[1].width * t[1].height, reverse=True)
    return comps


def groove_from_hang(hang):
    """同一裁切里偏下的宽扁块才是吊绳开槽，上面往往是邻格的脸。"""
    cands = []
    for box, im in all_components(hang):
        if im.width > im.height * 1.2 and im.height >= 36:
            cands.append((box[3], im))
    if not cands:
        return None
    cands.sort(reverse=True)
    return cands[0][1]


def stack_groove_on_side(groove, side, overlap=12):
    """对照 comp_groove_plus_sidebody：开槽盖上沿，侧视球去顶后拼在下面。"""
    trim = min(28, max(0, side.height // 4))
    body = side.crop((0, trim, side.width, side.height)) if trim else side
    overlap = min(overlap, max(0, groove.height - 8), max(0, body.height // 3))
    w = max(groove.width, body.width)
    h = groove.height + body.height - overlap
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.paste(body, ((w - body.width) // 2, groove.height - overlap), body)
    canvas.paste(groove, ((w - groove.width) // 2, 0), groove)
    return canvas


def pendulum_crop(im, r, atlas_wh):
    """无和谐 wooden 侧视 + 顶部开槽拼合（与 preview/comp_groove_plus_sidebody 同策略）。"""
    hang = crop(im, r, atlas_wh, "tl")
    side = crop(im, r, atlas_wh, "tl_pad") or hang
    if side is None:
        return None, "fail"
    groove = groove_from_hang(hang) if hang is not None else None
    if groove is not None and groove.size != side.size:
        return stack_groove_on_side(groove, side), "joined"
    return side, "side"


def pick_wooden(regions, lane):
    """优先无和谐 wooden（骷髅等），缺省再回退 wooden_hexie。"""
    hexie, plain = [], []
    for n in regions:
        leaf = n.split("/")[-1].lower()
        if "wooden" not in leaf:
            continue
        if lane == "air" and not ("air" in n or "images_air" in n or "image_air" in n):
            continue
        if lane == "road" and not ("road" in n or "images_road" in n or "image_road" in n):
            continue
        if "hexie" in leaf:
            hexie.append(n)
        elif leaf == "wooden":
            plain.append(n)
    return (plain or hexie or [None])[0]


def recrop_scene(scene_dir):
    packs = list(scene_dir.glob("*15_air")) + list(scene_dir.glob("*15_road"))
    if not packs:
        return None
    d = packs[0]
    atlas = next(d.glob("*.atlas"), None)
    png = next((p for p in d.glob("*.png") if p.suffix.lower() == ".png"), None)
    if atlas is None or png is None:
        return None
    regions, wh = parse_atlas(atlas.read_text(encoding="utf-8", errors="replace"))
    im = Image.open(png).convert("RGBA")
    out = {}
    for lane in ("air", "road"):
        name = pick_wooden(regions, lane)
        if not name:
            continue
        r = regions[name]
        img, mode = pendulum_crop(im, r, wh)
        out[lane] = (img, mode, name, r, 0 if img is None else score(img))
    return out, wh, im.size


def main():
    PREVIEW.mkdir(parents=True, exist_ok=True)
    for scene in sorted(p for p in SPINE.iterdir() if p.is_dir() and p.name.startswith("scene_")):
        got = recrop_scene(scene)
        if not got:
            print(scene.name, "no pack")
            continue
        crops, wh, png_size = got
        print(scene.name, "atlas", wh, "png", png_size)
        for lane, (img, mode, name, r, sc) in crops.items():
            if img is None:
                print(" ", lane, "FAIL", name)
                continue
            key = ("air" if lane == "air" else "ground") + "_pendulum.png"
            print(" ", key, "mode", mode, "score", int(sc), img.size, name, r)
            img.save(PREVIEW / f"{scene.name}_{key}")
            for dest_root in (SPRITE_A, SPRITE_B):
                dest = dest_root / scene.name
                dest.mkdir(parents=True, exist_ok=True)
                img.save(dest / key)
    print("preview", PREVIEW)


if __name__ == "__main__":
    main()
