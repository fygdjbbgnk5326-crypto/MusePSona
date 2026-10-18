# -*- coding: utf-8 -*-
"""把 MDSceneGrabber 整合好的逐帧动画打成 MDM 预览器「游戏动画」模式用的精灵表。

调用 MDSceneGrabber/tools/pack_frames.py 的 collect / write_pack，只挑物件相关条目（场景内编号 01 Boss、
03 齿轮、04 连打、05 双押、06-08 Boss 抛射物、09 Boss 齿轮、10-14 普通怪、15 摆锤、16 突击者、17 幽灵），
去掉 _old / _hint / _explode，默认 32 px/单位、15fps。另给每条入场动画加 body = 判定帧主体中心
（pack_frames 的 hitPos 是整帧包围盒中心，会被影子 / 吊绳拉偏），播放器用它把主体对准判定圈。

输出：play/mdm_player/assets/md/anims/<scene_XX>/scene_XX.js + scene_XX_N.png，并重写 anims/index.js
（window.MD_ANIM_INDEX，列出已有的包）。目录已被 assets/md/.gitignore 忽略：游戏素材，仅限本机个人使用。

用法：
  python scripts/pack_md_anims.py                      # 默认 scene_05（城堡地图）
  python scripts/pack_md_anims.py --all                # 有 F9 帧的场景都打，空序列跳过
  python scripts/pack_md_anims.py --scene scene_01 --ppu 48
  python scripts/pack_md_anims.py --grabber "D:/xxx/MDSceneGrabber"   # 或环境变量 MD_SCENE_GRABBER
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ANIM_DIR = os.path.join(ROOT, "play", "mdm_player", "assets", "md", "anims")
CODES = ["01", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17"]
ANIM_RE = r"^(?!.*_(old|hint|explode)$)(in_|out_[pg]$|standby$|normal_standby$|hurt_|boss_[12]_nor_)"
SCENE_RE = re.compile(r"^scene_(\d{1,2})$", re.I)


def find_grabber(arg):
    cands = [arg, os.environ.get("MD_SCENE_GRABBER"),
             os.path.join(os.path.dirname(ROOT), "Muse dash mod支持", "MDSceneGrabber")]
    for c in cands:
        if c and os.path.isfile(os.path.join(c, "tools", "pack_frames.py")):
            return c
    raise SystemExit("找不到 MDSceneGrabber（含 tools/pack_frames.py），用 --grabber 或环境变量 MD_SCENE_GRABBER 指定")


def solid_center(im):
    """判定帧里「主体」的中心（像素，相对裁切图左上）：只看不透明像素（排除半透明影子），
    取宽度 ≥ 最宽行一半的最长连续行段（排除摆锤吊绳这类细长部件）。"""
    w, h = im.size
    a = im.getchannel("A").point(lambda v: 255 if v >= 160 else 0).tobytes()
    rows = []
    for y in range(h):
        line = a[y * w:(y + 1) * w]
        xs = [x for x in range(w) if line[x]]
        rows.append((len(xs), xs[0] if xs else 0, xs[-1] if xs else 0))
    widest = max(r[0] for r in rows)
    if not widest:
        return None
    best, cur = (0, 0), None
    for y, r in enumerate(rows + [(0, 0, 0)]):
        if r[0] >= widest * 0.5:
            cur = (cur[0], y) if cur else (y, y)
        elif cur:
            if cur[1] - cur[0] > best[1] - best[0]:
                best = cur
            cur = None
    y0, y1 = best
    x0 = min(rows[y][1] for y in range(y0, y1 + 1) if rows[y][0])
    x1 = max(rows[y][2] for y in range(y0, y1 + 1) if rows[y][0])
    return (x0 + x1 + 1) / 2, (y0 + y1 + 1) / 2


def add_body(anims, images, step):
    """给每个带判定帧的动画加 body = 判定帧主体中心（单位，相对骨骼原点，y 向上）；
    判定帧不可见（幽灵）时退回首个可见帧，此时 x 无意义、只用 y。"""
    for el in anims.values():
        for a in el.values():
            if a["hit"] is None or not a["frames"]:
                continue
            r = a["frames"][min(a["hit"] // step, len(a["frames"]) - 1)]
            if r is None:
                r = next((f for f in a["frames"] if f), None)
                a["bodyFromFirst"] = True
            c = r and solid_center(images[r[0]])
            if c:
                a["body"] = [round((r[1] + c[0]) / a["ppu"], 3), round(-(r[2] + c[1]) / a["ppu"], 3)]


def write_index():
    packs = {}
    for d in sorted(os.listdir(ANIM_DIR)):
        js = os.path.join(ANIM_DIR, d, d + ".js")
        if re.fullmatch(r"scene_\d\d", d) and os.path.isfile(js):
            packs[d] = d + "/" + d + ".js"
    with open(os.path.join(ANIM_DIR, "index.js"), "w", encoding="utf-8") as f:
        f.write("window.MD_ANIM_INDEX = " + json.dumps(packs, ensure_ascii=False) + ";\n")
    with open(os.path.join(ANIM_DIR, "index.json"), "w", encoding="utf-8") as f:
        json.dump({"anims": list(packs)}, f, ensure_ascii=False)
    return packs


def scene_id(s):
    m = re.fullmatch(r"(?:scene_)?(\d{1,2})", str(s).strip(), re.I)
    return f"scene_{int(m.group(1)):02d}" if m else ""


def candidate_scenes(src):
    found = set()
    alias_path = os.path.join(src, "scene_alias.json")
    if os.path.isfile(alias_path):
        aliases = json.load(open(alias_path, encoding="utf-8-sig"))
        for k in aliases:
            sid = scene_id(k)
            if sid:
                found.add(sid)
    index_path = os.path.join(src, "index.json")
    if os.path.isfile(index_path):
        data = json.load(open(index_path, encoding="utf-8-sig"))
        for k in data.get("scenes") or {}:
            sid = scene_id(k)
            if sid:
                found.add(sid)
    if os.path.isdir(src):
        for name in os.listdir(src):
            sid = scene_id(name) if SCENE_RE.match(name) else ""
            if sid and os.path.isfile(os.path.join(src, name, "mod", "manifest.json")):
                found.add(sid)
    return sorted(found)


def pack_one(pf, grabber, scene, ppu, step, max_page):
    src = os.path.join(grabber, "素材整合")
    try:
        mod_dir = pf.resolve_scene_dir(src, scene)
    except SystemExit as e:
        print(f"{scene}: 跳过（{e}）")
        return False
    num = scene[-2:]
    anims, images = pf.collect(mod_dir, [num + c for c in CODES], re.compile(ANIM_RE), 1.0, ppu, step)
    if not images:
        print(f"{scene}: 跳过（没有匹配的逐帧序列；先在游戏里按 F9 抓取并整合）")
        return False
    add_body(anims, images, step)
    os.makedirs(ANIM_DIR, exist_ok=True)
    out = os.path.join(ANIM_DIR, scene)
    if os.path.isdir(out):
        for fn in os.listdir(out):
            if fn.startswith(scene):
                os.remove(os.path.join(out, fn))
    pages, png_bytes, js_bytes = pf.write_pack(anims, images, out, scene, max_page, scene)
    n_anims = sum(len(el) for el in anims.values())
    print(f"{scene}: {len(anims)} 个条目 / {n_anims} 个动画 → {len(pages)} 页，PNG {png_bytes / 1048576:.1f} MB，"
          f"JS {js_bytes / 1024:.0f} KB → {out}")
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--scene", default=None, help="场景 ID（scene_05）；素材整合里用别名目录也能找到")
    ap.add_argument("--all", action="store_true", help="扫素材整合，有帧的场景都打，空序列跳过")
    ap.add_argument("--grabber", help="MDSceneGrabber 根目录")
    ap.add_argument("--ppu", type=float, default=32, help="每单位像素（只缩小不放大）；48 更清晰但约 24 MB / 11 页")
    ap.add_argument("--step", type=int, default=2, help="每 N 帧取 1 帧（2 = 15fps）")
    ap.add_argument("--max-page", type=int, default=2048)
    args = ap.parse_args()

    grabber = find_grabber(args.grabber)
    sys.path.insert(0, os.path.join(grabber, "tools"))
    import pack_frames as pf  # noqa: E402

    if args.all:
        scenes = candidate_scenes(os.path.join(grabber, "素材整合"))
        if not scenes:
            print("素材整合里没有可识别的 scene_XX")
            return 1
    else:
        raw = args.scene or "scene_05"
        scene = scene_id(raw)
        if not scene:
            raise SystemExit("--scene 要写成 scene_05 / 05")
        scenes = [scene]

    n_ok = 0
    for scene in scenes:
        if pack_one(pf, grabber, scene, args.ppu, args.step, args.max_page):
            n_ok += 1
        elif not args.all:
            return 1
    os.makedirs(ANIM_DIR, exist_ok=True)
    print("anims/index.js：", write_index())
    try:
        import sync_md_assets_to_unity as sync
        sync.copy_looks()
    except Exception as e:
        print("同步 Unity StreamingAssets 跳过：", e)
    if args.all:
        print(f"完成：{n_ok}/{len(scenes)} 个场景打出动画包")
    return 0 if n_ok or not args.all else 1


if __name__ == "__main__":
    sys.exit(main())
