# -*- coding: utf-8 -*-
"""从本机已安装的 Muse Dash 读取场景 bundle，导出 Unity 游玩端使用的物件贴图。

只读游戏文件；输出到 Unity StreamingAssets/MdLooks（已 .gitignore，仅限本机个人使用，不要提交/分发）。

怪物在游戏里是 Spine 骨骼动画（部件拼装），这里只裁切每种物件的「主体」部件
（body / sawtooth / wooden / hp / score ...），外加长按条与判定圈 Sprite，供 Unity 游玩端加载。

用法：
  python scripts/extract_md_sprites.py               # 默认 scene_01 → Unity StreamingAssets/MdLooks/sprites/scene_01/
  python scripts/extract_md_sprites.py --all         # gamescene_01–12 各一份
  python scripts/extract_md_sprites.py --scene 05 --out <目录>
  python scripts/extract_md_sprites.py --list        # 只列出 bundle 里的 atlas 区域与 Sprite 名
依赖：UnityPy（pip install UnityPy）、Pillow。
"""
import argparse
import glob
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import _paths  # noqa: E402

DEFAULT_OUT = os.path.join(os.path.dirname(HERE), "PlayClient", "Unity", "Assets", "StreamingAssets", "MdLooks")

# 输出键 -> (候选 atlas 区域名, 来源)。区域名里的 {s} 替换为两位场景号；按顺序取第一个存在的。
# 场景内编号（见 notedata prefab_name）：02 长按 03 齿轮 04 连打 08 Boss 炸弹 09 Boss 齿轮
# 10 小怪 11/12 中怪 13/14 大怪 15 摆锤 16 突击者 17 幽灵；common 里 0002 红心 0003 音符。
REGIONS = {
    "air_small": ["{s}10/images_air/body"],
    "ground_small": ["{s}10/images_road/body"],
    "air_medium1": ["{s}11/images_air/body"],
    "ground_medium1": ["{s}11/images_road/body"],
    "air_medium2": ["{s}12/images_air/body1"],
    "ground_medium2": ["{s}12/images_road/body1"],
    "air_large1": ["{s}13/images_air/body"],
    "ground_large1": ["{s}13/images_road/body"],
    "air_large2": ["{s}14/images_air/body"],
    "ground_large2": ["{s}14/images_road/body"],
    "air_pendulum": ["{s}15/images_air/wooden", "{s}15/images_air/wooden_hexie"],
    "ground_pendulum": ["{s}15/images_road/wooden", "{s}15/images_road/wooden_hexie"],
    "air_raider": ["{s}16/images_air/body"],
    "ground_raider": ["{s}16/images_road/body"],
    "air_ghost": ["{s}17/images_air/body"],
    "ground_ghost": ["{s}17/images_road/body"],
    "masher": ["{s}04/images/body"],
    "air_gear": ["{s}03/images_air/sawtooth", "{s}09/images_air/sawtooth"],
    "ground_gear": ["{s}03/images_road/sawtooth", "{s}09/images_road/sawtooth"],
    "air_boss": ["{s}08/images_air/bomb_body"],
    "ground_boss": ["{s}08/images_road/bomb_body", "{s}08/images_air/bomb_body"],
    "heart": ["hp"],
    "note": ["score"],
}
SPRITES = {
    "air_hold_head": ["{s}02_air_top"],
    "ground_hold_head": ["{s}02_road_top"],
    "air_hold_body": ["{s}02_air_body"],
    "ground_hold_body": ["{s}02_road_body"],
    "air_hold_note_up": ["{s}02_air_note_up"],
    "air_hold_note_down": ["{s}02_air_note_down"],
    "ground_hold_note_up": ["{s}02_road_note_up"],
    "ground_hold_note_down": ["{s}02_road_note_down"],
    "judge_out": ["HitPointOut"],
    "judge_in": ["HitPointIn"],
    "judge_catch": ["HitPointInCatch"],
}


def parse_atlas(text):
    """Spine 文本 atlas -> {region: {page, xy, size, rotate}}（兼容 3.x 布局）。"""
    regions, page, cur = {}, None, None
    expect_page = True
    atlas_h = None
    atlas_w = None
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
            if k == "size" and atlas_h is None:
                try:
                    atlas_w, atlas_h = (int(x) for x in v.split(","))
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
        out[name] = {
            "page": os.path.splitext(r["page"])[0],
            "x": x, "y": y, "w": w, "h": h, "deg": deg,
            "atlas_w": atlas_w, "atlas_h": atlas_h,
        }
    return out


def crop_region(tex, r, pad_top=True):
    pw, ph = (r["h"], r["w"]) if r["deg"] in (90, 270) else (r["w"], r["h"])
    # libGDX/Spine 文本 atlas 的 y 是页内上往下；PNG 若 pad 成 POT，多出来的行在上方。
    ah = r.get("atlas_h") or tex.height
    pad = max(0, tex.height - ah) if pad_top else 0
    y = pad + r["y"]
    img = tex.crop((r["x"], max(0, y), r["x"] + pw, min(tex.height, max(0, y) + ph)))
    return img.rotate(-r["deg"], expand=True) if r["deg"] else img


def _largest_component(img, alpha=16):
    a = img.split()[-1]
    w, h = a.size
    pix = a.load()
    seen = [[False] * w for _ in range(h)]
    best = []
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
            if len(pts) > len(best):
                best = pts
    if len(best) < 80:
        return None
    xs = [p[0] for p in best]
    ys = [p[1] for p in best]
    return img.crop((min(xs), min(ys), max(xs) + 1, max(ys) + 1))


def _is_groove(img):
    if img is None or img.width <= img.height * 1.45 or img.height < 36:
        return False
    from PIL import Image
    a = img.split()[-1].resize((24, 24), Image.NEAREST)
    w, h = a.size
    seen = [[False] * w for _ in range(h)]
    nblob = 0
    for y in range(h):
        for x in range(w):
            if seen[y][x] or a.getpixel((x, y)) < 16:
                continue
            nblob += 1
            if nblob > 1:
                return False
            stack = [(x, y)]
            seen[y][x] = True
            while stack:
                cx, cy = stack.pop()
                for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and a.getpixel((nx, ny)) >= 16:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
    aa = img.split()[-1]
    iw, ih = aa.size
    x0, x1 = iw * 2 // 5, iw * 3 // 5
    mid = sum(1 for y in range(ih) for x in range(x0, x1) if aa.getpixel((x, y)) > 16)
    return nblob == 1 and mid / max(1, ih * (x1 - x0)) > 0.28


def crop_pendulum(tex, r):
    """无和谐侧视 + 顶部开槽拼合（与 recrop_pendulum / comp_groove_plus_sidebody 同策略）。"""
    from PIL import Image

    hang = crop_region(tex, r, pad_top=False)
    side = crop_region(tex, r, pad_top=True) or hang
    if side is None:
        return None
    groove = None
    if hang is not None:
        boxed = []
        a = hang.split()[-1]
        w, h = a.size
        pix = a.load()
        seen = [[False] * w for _ in range(h)]
        for y in range(h):
            for x in range(w):
                if seen[y][x] or pix[x, y] < 16:
                    continue
                stack = [(x, y)]
                seen[y][x] = True
                pts = []
                while stack:
                    cx, cy = stack.pop()
                    pts.append((cx, cy))
                    for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and pix[nx, ny] >= 16:
                            seen[ny][nx] = True
                            stack.append((nx, ny))
                if len(pts) < 80:
                    continue
                xs = [p[0] for p in pts]
                ys = [p[1] for p in pts]
                box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
                im = hang.crop(box)
                if im.width > im.height * 1.2 and im.height >= 36:
                    boxed.append((box[3], im))
        if boxed:
            boxed.sort(reverse=True)
            groove = boxed[0][1]
    if groove is not None and groove.size != side.size:
        trim = min(28, max(0, side.height // 4))
        body = side.crop((0, trim, side.width, side.height)) if trim else side
        overlap = min(12, max(0, groove.height - 8), max(0, body.height // 3))
        cw = max(groove.width, body.width)
        ch = groove.height + body.height - overlap
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        canvas.paste(body, ((cw - body.width) // 2, groove.height - overlap), body)
        canvas.paste(groove, ((cw - groove.width) // 2, 0), groove)
        return canvas
    return side


def load_bundle(path):
    import UnityPy
    env = UnityPy.load(path)
    textures, atlases, sprites = {}, {}, {}
    for o in env.objects:
        t = o.type.name
        if t == "Texture2D":
            d = o.read()
            textures[d.m_Name] = d
        elif t == "TextAsset":
            d = o.read()
            if d.m_Name.endswith(".atlas"):
                s = d.m_Script
                atlases[d.m_Name] = s if isinstance(s, str) else bytes(s).decode("utf-8", "replace")
        elif t == "Sprite":
            d = o.read()
            sprites[d.m_Name] = d
    return textures, atlases, sprites


def find_bundle(bdir, prefix):
    hits = sorted(glob.glob(os.path.join(bdir, prefix + "_assets_all_*.bundle")))
    if not hits:
        raise FileNotFoundError(f"未找到 {prefix}_assets_all_*.bundle：{bdir}")
    return hits[0]


def merge_bundle(path, textures, regions, sprites, page_tex):
    tx, at, sp = load_bundle(path)
    textures.update(tx)
    sprites.update(sp)
    for _name, text in at.items():
        for rn, r in parse_atlas(text).items():
            tex = tx.get(r["page"]) or textures.get(r["page"])
            if rn not in regions and tex is not None:
                regions[rn] = r
                page_tex[rn] = tex


def sprite_texture_image(sp, tex_image):
    """独立贴图（note_up/down、星星、body）用整张 Texture2D；图集 Sprite（HitPoint）必须用 Sprite.image，否则会整页 atlas。"""
    try:
        rd = getattr(sp, "m_RD", None)
        tex = rd.texture.read() if rd is not None else None
        name = (getattr(tex, "m_Name", None) or "") if tex is not None else ""
        if tex is not None and not name.startswith("SpriteAtlasTexture"):
            return tex_image(tex)
    except Exception:
        pass
    return sp.image.convert("RGBA")


def write_gitignore(out):
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, ".gitignore"), "w", encoding="utf-8") as f:
        f.write("# Muse Dash 游戏素材：仅限本机个人使用，不要提交/分发\n*\n!.gitignore\n")


_SKIP_LEAF = (
    "shadow", "fx", "highlight", "line", "dust", "smoke", "scrap", "multi_hit",
    "hexie",
)


def find_region(s, code, lane, names, regions):
    """先精确候选（images_air / image_air 等），再取该条目下最大的主体区域。"""
    folders = []
    if lane == "air":
        folders += ["images_air", "image_air"]
    elif lane == "road":
        folders += ["images_road", "image_road", "image_road_b", "image_road_c"]
    folders += ["images", "image"]
    prefixes = [f"{s}{code}/{f}/" for f in folders]
    want = {n.lower() for n in names}
    for n in names:
        for p in prefixes:
            if p + n in regions:
                return p + n
    stem = f"{s}{code}/"
    for rn in regions:
        if rn.startswith(stem) and rn.split("/")[-1].lower() in want:
            return rn
    def keep(rn):
        leaf = rn.split("/")[-1].lower()
        return not any(k in leaf for k in _SKIP_LEAF)

    cands = [rn for p in prefixes for rn in regions
             if rn.startswith(p) and keep(rn) and "body" in rn.split("/")[-1].lower()]
    if not cands:
        cands = [rn for p in prefixes for rn in regions if rn.startswith(p) and keep(rn)]
    if not cands:
        cands = [rn for rn in regions if rn.startswith(stem) and keep(rn)]
    if not cands:
        return None
    return max(cands, key=lambda rn: regions[rn]["w"] * regions[rn]["h"])


def extract_scene(s, out, common_path):
    """裁切 scene_XX 物件主体。返回 (manifest, missing)。"""
    bdir = os.path.dirname(common_path)
    regions, sprites, page_tex, textures = {}, {}, {}, {}
    merge_bundle(find_bundle(bdir, f"gamescene_{s}"), textures, regions, sprites, page_tex)
    merge_bundle(common_path, textures, regions, sprites, page_tex)

    os.makedirs(out, exist_ok=True)
    images, manifest, missing = {}, {}, []

    def tex_image(t):
        if t.m_Name not in images:
            images[t.m_Name] = t.image.convert("RGBA")
        return images[t.m_Name]

    # (输出键, 场景内编号, air|road|'', 区域名候选)
    REGION_SPECS = [
        ("air_small", "10", "air", ["body"]), ("ground_small", "10", "road", ["body"]),
        ("air_medium1", "11", "air", ["body"]), ("ground_medium1", "11", "road", ["body"]),
        ("air_medium2", "12", "air", ["body1", "body"]), ("ground_medium2", "12", "road", ["body1", "body"]),
        ("air_large1", "13", "air", ["body"]), ("ground_large1", "13", "road", ["body"]),
        ("air_large2", "14", "air", ["body"]), ("ground_large2", "14", "road", ["body"]),
        ("air_pendulum", "15", "air", ["wooden", "wooden_hexie", "body", "up"]),
        ("ground_pendulum", "15", "road", ["wooden", "wooden_hexie", "body", "up"]),
        ("air_raider", "16", "air", ["body"]), ("ground_raider", "16", "road", ["body"]),
        ("air_ghost", "17", "air", ["body"]), ("ground_ghost", "17", "road", ["body"]),
        ("masher", "04", "", ["body", "1_body", "3_body"]),
        ("air_gear", "03", "air", ["sawtooth", "sawtooth_air"]),
        ("ground_gear", "03", "road", ["sawtooth", "sawtooth_road"]),
        ("air_boss", "08", "air", ["bomb_body", "body"]),
        ("ground_boss", "08", "road", ["bomb_body", "body"]),
    ]

    for key, code, lane, names in REGION_SPECS:
        name = find_region(s, code, lane, names, regions)
        if not name and key.endswith("_gear"):
            name = find_region(s, "09", lane, ["sawtooth", "sawtooth_air", "sawtooth_road"], regions)
        if not name:
            missing.append(key)
            continue
        raw = tex_image(page_tex[name])
        img = crop_pendulum(raw, regions[name]) if key.endswith("_pendulum") else crop_region(raw, regions[name])
        img.save(os.path.join(out, key + ".png"))
        manifest[key] = {"file": key + ".png", "w": img.width, "h": img.height, "src": name}

    for key, cands in SPRITES.items():
        name = next((c.format(s=s) for c in cands if c.format(s=s) in sprites), None)
        img = None
        if name:
            # Sprite.image 会按 Tight 网格栅格化，长条音符变成多边形。改读 Texture2D（note_up/down 为 880×118）。
            img = sprite_texture_image(sprites[name], tex_image)
        else:
            # scene_10 地面条子只有 Texture2D 1002_road_body，没有同名 Sprite。
            name = next((c.format(s=s) for c in cands if c.format(s=s) in textures), None)
            if name:
                img = tex_image(textures[name])
        if img is None:
            missing.append(key)
            continue
        img.save(os.path.join(out, key + ".png"))
        manifest[key] = {"file": key + ".png", "w": img.width, "h": img.height, "src": name}

    # 官机长条音符粒子：common Texture2D press_fx = 4×8 表（512×1024），不要裁 Sprite 512×905。
    fx = textures.get("press_fx")
    if fx is not None:
        img = tex_image(fx)
        img.save(os.path.join(out, "hold_note_fx.png"))
        manifest["hold_note_fx"] = {"file": "hold_note_fx.png", "w": img.width, "h": img.height, "src": "press_fx"}
    else:
        missing.append("hold_note_fx")

    for key, names in (("heart", ["hp"]), ("note", ["score"]), ("heart_glow", ["hp_glow"]), ("note_glow", ["score_glow"])):
        name = next((n for n in names if n in regions), None)
        if not name:
            missing.append(key)
            continue
        img = crop_region(tex_image(page_tex[name]), regions[name])
        img.save(os.path.join(out, key + ".png"))
        manifest[key] = {"file": key + ".png", "w": img.width, "h": img.height, "src": name}

    keep = {v["file"] for v in manifest.values()}
    for fn in os.listdir(out):
        if fn.endswith(".png") and fn not in keep:
            os.remove(os.path.join(out, fn))

    scene = "scene_" + s
    with open(os.path.join(out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"scene": scene, "sprites": manifest}, f, ensure_ascii=False)
    return manifest, missing


def extract_hit_fx(out_root, bdir):
    """gamemain 判定字：ScorePerfect / ScoreGreat。MISS 官机无独立气泡。"""
    hits = sorted(glob.glob(os.path.join(bdir, "gamemain_assets_all_*.bundle")))
    hits = [p for p in hits if "gc_" not in os.path.basename(p).lower() and "tutorial" not in os.path.basename(p).lower()]
    if not hits:
        print("未找到 gamemain_assets_all_*.bundle，跳过打击特效字")
        return
    path = hits[0]
    tx, _at, sp = load_bundle(path)
    dest = os.path.join(out_root, "sprites", "common")
    os.makedirs(dest, exist_ok=True)
    images, manifest, missing = {}, {}, []

    def tex_image(t):
        if t.m_Name not in images:
            images[t.m_Name] = t.image.convert("RGBA")
        return images[t.m_Name]

    keys = (
        ("hit_perfect", "ScorePerfect"),
        ("hit_great", "ScoreGreat"),
        ("hit_pass", "ScorePass"),
        ("hit_gold_perfect", "GoldPerfect"),
        ("hit_gold_great", "GoldGreat"),
        ("hits_base", "HitsBase"),
        # JudgeDisplay 的小 P 方向字。原图尺寸分别为 108x33 / 90x33，
        # 不再由 Unity 的系统字体近似绘制。
        ("judge_early", "Early"),
        ("judge_late", "Late"),
        # 曲终 FullComboText（官机 AnimationClip 长 2.0s）由十个独立 Image
        # 逐字驱动；L/O 会重复使用同一张 Sprite。
        ("full_combo_f", "fullcombo_f"),
        ("full_combo_u", "fullcombo_u"),
        ("full_combo_l", "fullcombo_l"),
        ("full_combo_c", "fullcombo_c"),
        ("full_combo_o", "fullcombo_o"),
        ("full_combo_m", "fullcombo_m"),
        ("full_combo_b", "fullcombo_b"),
        ("full_combo_bang", "fullcombo_!"),
        # 东方联动沿用逐字 FullComboText 动画，但字图是黑色主题专版。
        ("full_combo_touhou_f", "fullcombo_f_touhou_black"),
        ("full_combo_touhou_u", "fullcombo_u_touhou_black"),
        ("full_combo_touhou_l", "fullcombo_l_touhou_black"),
        ("full_combo_touhou_c", "fullcombo_c_touhou_black"),
        ("full_combo_touhou_o", "fullcombo_o_touhou_black"),
        ("full_combo_touhou_m", "fullcombo_m_touhou_black"),
        ("full_combo_touhou_b", "fullcombo_b_touhou_black"),
        ("full_combo_touhou_bang", "fullcombo_!_touhou_black"),
        # DJMAX 的曲终提示不是普通逐字横排，而是 MaxCombo 徽章。
        ("full_combo_djmax_text", "MaxComboText"),
        ("full_combo_djmax_bg", "MaxComboTextBg"),
        ("full_combo_djmax_crown", "MaxComboCrown"),
        ("full_combo_djmax_star_l", "MaxComboStarL"),
        ("full_combo_djmax_star_r", "MaxComboStarR"),
        # 明日方舟联动的 FC 结算牌；语言默认取简中版。
        ("full_combo_arknights_zh", "FC_zh"),
        ("full_combo_arknights_en", "FC_en"),
        ("full_combo_arknights_mark", "RHODES_ISLAND"),
        # 官机左上 Score：AP 图标与 95x46 的中文“得分”标题。
        ("hud_ap", "icon_ap"),
        ("hud_score_title", "score_Chinese"),
    )
    for key, name in keys:
        src = None
        if name in tx:
            src = tex_image(tx[name])
        elif name in sp:
            src = sprite_texture_image(sp[name], tex_image)
        if src is None:
            missing.append(key)
            continue
        src.save(os.path.join(dest, key + ".png"))
        manifest[key] = {"file": key + ".png", "w": src.width, "h": src.height, "src": name}
    extract_combo_digits(dest, manifest)
    extract_score_digits(dest, manifest)
    extract_hits_digits(dest, manifest)
    extract_djmax_combo(dest, manifest)
    keep = {v["file"] for v in manifest.values()}
    for fn in os.listdir(dest):
        if fn.endswith(".png") and fn not in keep:
            os.remove(os.path.join(dest, fn))
    with open(os.path.join(dest, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump({"scene": "common", "sprites": manifest}, f, ensure_ascii=False)
    print(f"common hit: {len(manifest)} 张 → {dest}" + (f"  未找到 {', '.join(missing)}" if missing else ""))


def extract_score_digits(dest, manifest):
    """左上分数：官机 TxtScore 使用 LuckiestGuy-Regular、72px、浅青色。"""
    import UnityPy
    from io import BytesIO
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        print("没有 Pillow，跳过分数数字")
        return
    bdir = _paths.bundle_dir()
    fonts = sorted(glob.glob(os.path.join(bdir, "fonts_assets_all_*.bundle")))
    if not fonts:
        print("未找到 fonts_assets_all_*.bundle，跳过分数数字")
        return
    env = UnityPy.load(fonts[0])
    raw = None
    # gamemain/TxtScore 的 m_Font 引用。
    for o in env.objects:
        if o.path_id != -2103961664902859467:
            continue
        tree = o.read_typetree()
        fd = tree.get("m_FontData")
        if isinstance(fd, list) and fd:
            raw = bytes(fd)
        break
    if not raw:
        print("未找到官机 Score 的 LuckiestGuy-Regular")
        return

    font = ImageFont.truetype(BytesIO(raw), 72)
    fill = (190, 255, 255, 255)  # TxtScore m_Color = (0.7469, 1, 1, 1)
    os.makedirs(dest, exist_ok=True)
    for i in range(10):
        ch = str(i)
        box = font.getbbox(ch)
        w = max(1, box[2] - box[0])
        h = max(1, box[3] - box[1])
        pad = 2
        img = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
        ImageDraw.Draw(img).text((pad - box[0], pad - box[1]), ch, font=font, fill=fill)
        key = "score_digit_" + ch
        img.save(os.path.join(dest, key + ".png"))
        manifest[key] = {
            "file": key + ".png",
            "w": img.width,
            "h": img.height,
            "src": "LuckiestGuy-Regular_150_115@72",
        }


def extract_combo_digits(dest, manifest):
    """连击数字：官机 COMBO 字是 Spine（Luckiest Guy 风格），数字另用同一套 LuckiestGuy-Regular，描边仍走 TxtCounter 三档色。不改 COMBO 字。"""
    import UnityPy
    from io import BytesIO
    try:
        from PIL import Image, ImageDraw, ImageFont, ImageChops
    except ImportError:
        print("没有 Pillow，跳过连击数字")
        return
    bdir = _paths.bundle_dir()
    fonts = sorted(glob.glob(os.path.join(bdir, "fonts_assets_all_*.bundle")))
    if not fonts:
        print("未找到 fonts_assets_all_*.bundle，跳过连击数字")
        return
    env = UnityPy.load(fonts[0])
    raw = None
    for o in env.objects:
        if o.path_id != 8257107684568535854:
            continue
        tree = o.read_typetree()
        fd = tree.get("m_FontData")
        if isinstance(fd, list) and fd:
            raw = bytes(fd)
        break
    if not raw:
        print("未找到 LuckiestGuy-Regular")
        return
    size = 125
    font = ImageFont.truetype(BytesIO(raw), size)
    off = 4
    pad = 2
    palettes = (
        ("low", (159, 185, 255, 255), (101, 14, 178, 255)),
        ("mid", (164, 240, 254, 255), (0, 168, 223, 255)),
        ("high", (255, 233, 50, 255), (181, 0, 81, 255)),
    )
    boxes = []
    for i in range(10):
        boxes.append(font.getbbox(str(i)))

    def blit_alpha(dst, mask, x, y):
        layer = Image.new("L", dst.size, 0)
        layer.paste(mask, (int(x), int(y)))
        return ImageChops.lighter(dst, layer)

    os.makedirs(dest, exist_ok=True)
    n = 0
    for i in range(10):
        ch = str(i)
        box = boxes[i]
        gw = max(1, box[2] - box[0])
        gh = max(1, box[3] - box[1])
        glyph = Image.new("L", (gw, gh), 0)
        ImageDraw.Draw(glyph).text((-box[0], -box[1]), ch, font=font, fill=255)
        cell_w = gw + pad * 2 + off * 2
        cell_h = gh + pad * 2 + off * 2
        gx = pad + off
        gy = pad + off
        for band, fill, outline in palettes:
            oa = Image.new("L", (cell_w, cell_h), 0)
            for dx, dy in ((-off, -off), (off, -off), (-off, off), (off, off)):
                oa = blit_alpha(oa, glyph, gx + dx, gy + dy)
            fa = blit_alpha(Image.new("L", (cell_w, cell_h), 0), glyph, gx, gy)
            img = Image.new("RGBA", (cell_w, cell_h), (0, 0, 0, 0))
            img.paste(outline, mask=oa)
            img.paste(fill, mask=fa)
            key = "combo_digit_%s_%s" % (band, i)
            img.save(os.path.join(dest, key + ".png"))
            manifest[key] = {
                "file": key + ".png",
                "w": img.width,
                "h": img.height,
                "src": "LuckiestGuy-Regular",
            }
            n += 1
            if band == "low":
                alias = "combo_digit_" + str(i)
                img.save(os.path.join(dest, alias + ".png"))
                manifest[alias] = {
                    "file": alias + ".png",
                    "w": img.width,
                    "h": img.height,
                    "src": "LuckiestGuy-Regular",
                }
                n += 1
    print("combo digits: %s 张 LuckiestGuy-Regular → %s" % (n, dest))


def extract_hits_digits(dest, manifest):
    """连打数字：从官机 MultiCombo SDF 图集烘焙。材质 FaceDilate 0.8 / Outline 0.54，顶点色玫红。"""
    import UnityPy
    try:
        from PIL import Image
    except ImportError:
        print("没有 Pillow，跳过 Hits 数字")
        return
    bdir = _paths.bundle_dir()
    mains = sorted(glob.glob(os.path.join(bdir, "gamemain_assets_all_*.bundle")))
    mains = [p for p in mains if "gc_" not in os.path.basename(p).lower() and "tutorial" not in os.path.basename(p).lower()]
    if not mains:
        print("未找到 gamemain_assets_all_*.bundle，跳过 Hits 数字")
        return
    env = UnityPy.load(mains[0])
    objs = {o.path_id: o for o in env.objects}
    font_pid, atlas_pid = -4962547079172184517, 592227237324267260
    if font_pid not in objs or atlas_pid not in objs:
        print("未找到 MultiCombo SDF")
        return
    font = objs[font_pid].read_typetree()
    atlas = objs[atlas_pid].read().image.convert("RGBA")
    aw, ah = atlas.size
    pad = int(font.get("m_AtlasPadding") or 9)
    glyphs = {g["m_Index"]: g for g in (font.get("m_GlyphTable") or [])}
    chars = {c["m_Unicode"]: c["m_GlyphIndex"] for c in (font.get("m_CharacterTable") or [])}
    # 官机 0.8/0.54 烘焙后填心会糊；填心少膨、描边保留。
    dilate, outline_w, ratio_a = 0.42, 0.68, 0.5891980528831482
    face_t = 0.5 - (dilate * ratio_a) * 0.5
    out_t = face_t - (outline_w * ratio_a) * 0.5
    fill = (255, 0, 162, 255)
    ring = (118, 0, 96, 255)
    up = 4
    os.makedirs(dest, exist_ok=True)
    n = 0
    for i in range(10):
        gi = chars.get(ord(str(i)))
        g = glyphs.get(gi)
        if not g:
            continue
        r = g["m_GlyphRect"]
        gx, gy, gw, gh = int(r["m_X"]), int(r["m_Y"]), int(r["m_Width"]), int(r["m_Height"])
        extra = pad + 2
        x0 = max(0, gx - extra)
        y0 = max(0, gy - extra)
        x1 = min(aw, gx + gw + extra)
        y1 = min(ah, gy + gh + extra)
        # Unity 图集原点在左下，PIL 在左上
        crop = atlas.crop((x0, ah - y1, x1, ah - y0))
        sdf = crop.resize((crop.width * up, crop.height * up), Image.BILINEAR)
        pix = sdf.load()
        baked = Image.new("RGBA", sdf.size, (0, 0, 0, 0))
        op = baked.load()
        aa = 0.03
        for y in range(sdf.height):
            for x in range(sdf.width):
                d = pix[x, y][3] / 255.0
                fo = max(0.0, min(1.0, (d - face_t) / aa + 0.5))
                oo = max(0.0, min(1.0, (d - out_t) / aa + 0.5))
                if oo <= 0.0:
                    continue
                a = 1.0 - (1.0 - oo * ring[3] / 255.0) * (1.0 - fo * fill[3] / 255.0)
                r8 = int(ring[0] * (1.0 - fo) + fill[0] * fo + 0.5)
                g8 = int(ring[1] * (1.0 - fo) + fill[1] * fo + 0.5)
                b8 = int(ring[2] * (1.0 - fo) + fill[2] * fo + 0.5)
                op[x, y] = (r8, g8, b8, int(a * 255 + 0.5))
        box = baked.getbbox()
        if box:
            m = 8
            box = (max(0, box[0] - m), max(0, box[1] - m), min(baked.width, box[2] + m), min(baked.height, box[3] + m))
            baked = baked.crop(box)
        key = "hits_digit_" + str(i)
        baked.save(os.path.join(dest, key + ".png"))
        out = baked
        met = g["m_Metrics"]
        manifest[key] = {
            "file": key + ".png",
            "w": out.width,
            "h": out.height,
            "src": "MultiCombo SDF",
            "up": up,
            "mw": met["m_Width"],
            "mh": met["m_Height"],
            "adv": met["m_HorizontalAdvance"],
        }
        n += 1
    print("hits digits: %s 张 MultiCombo SDF 图集 → %s" % (n, dest))


def extract_djmax_combo(dest, manifest):
    """官机 Combo_djmax：ImgCombo=Low/Normal/HighCombo_djmax，数字=Teko Bold + FaceTex，无描边。"""
    import UnityPy
    from io import BytesIO
    try:
        from PIL import Image, ImageDraw, ImageFont
    except ImportError:
        print("没有 Pillow，跳过 DJMAX combo")
        return
    bdir = _paths.bundle_dir()
    mains = sorted(glob.glob(os.path.join(bdir, "gamemain_assets_all_*.bundle")))
    mains = [p for p in mains if "gc_" not in os.path.basename(p).lower() and "tutorial" not in os.path.basename(p).lower()]
    fonts = sorted(glob.glob(os.path.join(bdir, "fonts_assets_all_*.bundle")))
    if not mains or not fonts:
        print("未找到 gamemain/fonts，跳过 DJMAX combo")
        return
    env = UnityPy.load(mains[0])
    words = {}
    faces = {}
    for o in env.objects:
        if o.type.name != "Texture2D":
            continue
        try:
            tex = o.read()
            n = tex.m_Name
        except Exception:
            continue
        if n in ("LowCombo_djmax", "NormalCombo_djmax", "HighCombo_djmax"):
            words[n] = tex.image.convert("RGBA")
        elif n in ("LowComboTexDjmax", "NormalComboTexDjmax", "HighComboTexDjmax"):
            faces[n] = tex.image.convert("RGBA")
    os.makedirs(dest, exist_ok=True)
    word_map = (
        ("combo_djmax_word_low", "LowCombo_djmax"),
        ("combo_djmax_word_mid", "NormalCombo_djmax"),
        ("combo_djmax_word_high", "HighCombo_djmax"),
    )
    for key, src in word_map:
        img = words.get(src)
        if img is None:
            print("缺少", src)
            continue
        img.save(os.path.join(dest, key + ".png"))
        manifest[key] = {"file": key + ".png", "w": img.width, "h": img.height, "src": src}

    raw = None
    fenv = UnityPy.load(fonts[0])
    for o in fenv.objects:
        if o.path_id != -7799849944524606012:
            continue
        tree = o.read_typetree()
        fd = tree.get("m_FontData")
        if isinstance(fd, list) and fd:
            raw = bytes(fd)
        break
    if not raw:
        print("未找到 Teko-Bold，跳过 DJMAX 数字")
        return
    size = 160
    font = ImageFont.truetype(BytesIO(raw), size)
    palettes = (
        ("low", "LowComboTexDjmax"),
        ("mid", "NormalComboTexDjmax"),
        ("high", "HighComboTexDjmax"),
    )

    n = 0
    for i in range(10):
        ch = str(i)
        box = font.getbbox(ch)
        gw = max(1, box[2] - box[0])
        gh = max(1, box[3] - box[1])
        glyph = Image.new("L", (gw, gh), 0)
        ImageDraw.Draw(glyph).text((-box[0], -box[1]), ch, font=font, fill=255)
        for band, face_name in palettes:
            img = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
            face = faces.get(face_name)
            if face is not None:
                col = face.resize((1, gh), Image.BILINEAR)
                pix = img.load()
                mask = glyph.load()
                for y in range(gh):
                    c = col.getpixel((0, y))
                    if len(c) == 3:
                        c = c + (255,)
                    for x in range(gw):
                        a = mask[x, y]
                        if a:
                            pix[x, y] = (c[0], c[1], c[2], a)
            else:
                img.paste((255, 255, 255, 255), mask=glyph)
            bb = img.getbbox()
            if bb:
                img = img.crop(bb)
            key = "combo_digit_djmax_%s_%s" % (band, i)
            img.save(os.path.join(dest, key + ".png"))
            manifest[key] = {
                "file": key + ".png",
                "w": img.width,
                "h": img.height,
                "src": "Teko-Bold/" + face_name,
            }
            n += 1
    print("DJMAX combo: 3 张 ImgCombo + %s 张 Teko 数字 → %s" % (n, dest))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--scene", default="01", help="场景号（两位，默认 01）")
    ap.add_argument("--all", action="store_true", help="抽出 gamescene_01–12 到 sprites/scene_XX/")
    ap.add_argument("--out", default=DEFAULT_OUT, help="输出目录（默认 Unity StreamingAssets/MdLooks）")
    ap.add_argument("--list", action="store_true", help="只列出区域/Sprite 名，不导出")
    args = ap.parse_args()

    bdir = _paths.bundle_dir()
    common = find_bundle(bdir, "gamescene_common")
    write_gitignore(args.out)

    if args.list and not args.all:
        s = args.scene.zfill(2)
        regions, sprites, page_tex, textures = {}, {}, {}, {}
        merge_bundle(find_bundle(bdir, f"gamescene_{s}"), textures, regions, sprites, page_tex)
        merge_bundle(common, textures, regions, sprites, page_tex)
        for rn in sorted(regions):
            print("region", rn, regions[rn]["page"])
        for sn in sorted(sprites):
            print("sprite", sn)
        return

    scenes = [f"{n:02d}" for n in range(1, 13)] if args.all else [args.scene.zfill(2)]
    sprites_dir = os.path.join(args.out, "sprites")
    done = []
    for s in scenes:
        try:
            find_bundle(bdir, f"gamescene_{s}")
        except FileNotFoundError as e:
            print(e)
            continue
        dest = os.path.join(sprites_dir, "scene_" + s)
        man, missing = extract_scene(s, dest, common)
        done.append("scene_" + s)
        print(f"scene_{s}: {len(man)} 张 → {dest}" + (f"  未找到 {', '.join(missing)}" if missing else ""))

        extract_hit_fx(args.out, bdir)

        try:
            import extract_md_scene_bg as bg
            _, msg = bg.extract_scene(s, args.out)
            print(f"scene_{s} bg: {msg}")
        except Exception as e:
            print(f"scene_{s} bg 跳过: {e}")

    if done:
        print("Unity MdLooks 已更新：", args.out)


if __name__ == "__main__":
    main()
