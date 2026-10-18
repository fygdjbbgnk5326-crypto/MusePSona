# -*- coding: utf-8 -*-
"""抽出完整 Spine 3.8 三件套（.skel + .atlas + 图集 PNG），供 UP 直读。不进 MDM。

优先 F8（MDSceneGrabber 素材整合/<scene>/mod/spine），缺的场景用 UnityPy 读 gamescene_XX bundle。
图层散碎的 F9/静图裁切不是完整物件；这里拷的是运行时骨骼。

用法：
  python scripts/extract_md_spine.py --all
  python scripts/extract_md_spine.py --scene 03
"""
import glob
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import _paths  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "play", "mdm_player", "assets", "md", "spine")
CODES = ("01", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17")
COMMON = ("0002_hp", "0003_score")


def grabber_src():
    cands = [
        os.environ.get("MD_SCENE_GRABBER"),
        os.path.join(os.path.dirname(ROOT), "Muse dash mod支持", "MDSceneGrabber"),
    ]
    for c in cands:
        if c and os.path.isdir(os.path.join(c, "素材整合")):
            return os.path.join(c, "素材整合")
    return None


def scene_id(name):
    m = re.search(r"(?:scene[_-]?)?(\d{1,2})$", str(name).strip(), re.I)
    return f"scene_{int(m.group(1)):02d}" if m else ""


def resolve_alias(src):
    alias = {}
    p = os.path.join(src, "scene_alias.json")
    if os.path.isfile(p):
        raw = json.load(open(p, encoding="utf-8-sig"))
        for k, v in raw.items():
            sid = scene_id(k)
            if sid:
                alias[sid] = v
    return alias


def default_scale(key):
    """官机 SkeletonDataAsset.scale：场景物件 xNN / 红心 0.005，角色秀 0.5，杂项 0.01。"""
    return 0.005 if re.match(r"^(?:\d{4}|000\d|s\d{2}_)", key or "") else 0.01


def write_scale(folder, scale):
    os.makedirs(folder, exist_ok=True)
    with open(os.path.join(folder, "scale.json"), "w", encoding="utf-8") as f:
        json.dump({"scale": float(scale)}, f)


def copy_trio(src_dir, dst_dir):
    os.makedirs(dst_dir, exist_ok=True)
    n = 0
    for fn in os.listdir(src_dir):
        if fn.endswith((".skel", ".json", ".atlas", ".png", ".txt")):
            if fn.endswith(".txt") and ".atlas" not in fn:
                continue
            shutil.copy2(os.path.join(src_dir, fn), os.path.join(dst_dir, fn.replace(".atlas.txt", ".atlas")))
            n += 1
    return n


def copy_f8(src, scene, dest):
    alias = resolve_alias(src)
    folder = alias.get(scene, scene)
    spine = os.path.join(src, folder, "mod", "spine")
    if not os.path.isdir(spine):
        return 0
    n = 0
    for name in os.listdir(spine):
        d = os.path.join(spine, name)
        if not os.path.isdir(d):
            continue
        if not any(f.endswith(".skel") or f.endswith(".json") for f in os.listdir(d)):
            continue
        n += copy_trio(d, os.path.join(dest, name)) > 0
    man = os.path.join(os.path.dirname(spine), "manifest.json")
    if os.path.isfile(man):
        try:
            data = json.load(open(man, encoding="utf-8-sig"))
        except Exception:
            data = {}
        for e in data.get("entries") or []:
            if not isinstance(e, dict) or e.get("scale") is None:
                continue
            dname = os.path.basename((e.get("dir") or "").replace("\\", "/"))
            if not dname:
                dname = (e.get("name") or "").replace("_SkeletonData", "")
            folder = os.path.join(dest, dname)
            if os.path.isdir(folder):
                write_scale(folder, e["scale"])
    return n


def parse_atlas_pages(text):
    pages, expect = [], True
    for raw in text.splitlines():
        line = raw.rstrip()
        if not line.strip():
            expect = True
            continue
        if expect:
            pages.append(line.strip())
            expect = False
            continue
        if ":" not in line and not raw.startswith((" ", "\t")):
            pages.append(line.strip())
    return pages


def expand_short_region_aliases(text):
    """s03_fire 等短图集：贴图叫 Aa/Bb/fx，skel 槽是 Ac1/Bc1/fx1。补同 uv 别名。"""
    if not text or "\nAa\n" not in text and not re.search(r"^Aa\s*$", text, re.M):
        if not re.search(r"^(Aa|Bb|fx)\s*$", text or "", re.M):
            return text
    blocks = {}
    cur = None
    buf = []
    pages = []
    for raw in text.splitlines(True):
        line = raw.rstrip("\n")
        if not line.strip():
            if cur:
                blocks[cur] = buf
                cur = None
                buf = []
            pages.append(raw)
            continue
        if ":" not in line and not raw.startswith((" ", "\t")):
            if cur:
                blocks[cur] = buf
            cur = line.strip()
            buf = [raw]
            continue
        if cur:
            buf.append(raw)
        else:
            pages.append(raw)
    if cur:
        blocks[cur] = buf
    extra = []
    if "Aa" in blocks:
        body = "".join(blocks["Aa"][1:])
        for i in range(1, 21):
            name = f"Ac{i}"
            if name not in blocks:
                extra.append(name + "\n" + body)
        for name in ("body1", "body2"):
            if name not in blocks:
                extra.append(name + "\n" + body)
    if "Bb" in blocks:
        body = "".join(blocks["Bb"][1:])
        for i in range(1, 8):
            name = f"Bc{i}"
            if name not in blocks:
                extra.append(name + "\n" + body)
    if "fx" in blocks:
        body = "".join(blocks["fx"][1:])
        for i in range(1, 9):
            name = f"fx{i}"
            if name not in blocks:
                extra.append(name + "\n" + body)
    if not extra:
        return text
    return text.rstrip() + "\n" + "".join(extra)


def _complete(folder):
    if not os.path.isdir(folder):
        return False
    files = os.listdir(folder)
    return (any(f.endswith((".skel", ".json")) for f in files)
            and any(f.endswith(".atlas") for f in files)
            and any(f.endswith(".png") for f in files))


def _write_atlas(folder, texts, textures, key, scene_num):
    shared = []
    if scene_num != "common":
        shared = [f"s{scene_num}_atlas.atlas", f"s{scene_num}_atlas.atlas.txt"]
    atlas_name = next((k for k in [key + ".atlas", key + ".atlas.txt"] + shared if k in texts), None)
    if not atlas_name:
        atlas_name = next((k for k in texts if k.endswith(".atlas") or k.endswith(".atlas.txt")), None)
    if not atlas_name:
        return 0
    raw = texts[atlas_name]
    text = raw.decode("utf-8", "replace") if isinstance(raw, (bytes, bytearray)) else raw
    atlas_fn = os.path.basename(atlas_name).replace(".atlas.txt", ".atlas")
    if not atlas_fn.endswith(".atlas"):
        atlas_fn += ".atlas"
    with open(os.path.join(folder, atlas_fn), "w", encoding="utf-8") as f:
        f.write(expand_short_region_aliases(text))
    n = 0
    for page in parse_atlas_pages(text):
        stem = os.path.splitext(page)[0]
        img = textures.get(page) or textures.get(stem) or textures.get(stem + ".png")
        if img is None:
            continue
        fn = page if str(page).lower().endswith(".png") else stem + ".png"
        img.save(os.path.join(folder, fn))
        n += 1
    return n


def skel_scale(env, key):
    """官机 SkeletonDataAsset.scale。名字不像 0701 的装饰骨（摩天轮）也是 0.005，不能按杂项 0.01。"""
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            d = o.read()
        except Exception:
            continue
        n = d.m_Name or ""
        if n != key + "_SkeletonData":
            continue
        try:
            t = d.object_reader.read_typetree()
            return float(t.get("scale") or 0.005)
        except Exception:
            return 0.005
    return default_scale(key)


def extract_bundle(scene_num, dest, common_tex):
    import UnityPy
    bdir = _paths.bundle_dir()
    prefix = "gamescene_common" if scene_num == "common" else f"gamescene_{scene_num}"
    hits = sorted(glob.glob(os.path.join(bdir, prefix + "_assets_all_*.bundle")))
    if not hits:
        return 0
    env = UnityPy.load(hits[0])
    texts, textures = {}, {}
    for o in env.objects:
        t = o.type.name
        if t == "TextAsset":
            d = o.read()
            name = d.m_Name or ""
            raw = d.m_Script
            if isinstance(raw, str):
                raw = raw.encode("utf-8", "surrogateescape")
            texts[name] = raw
        elif t == "Texture2D":
            d = o.read()
            try:
                textures[d.m_Name] = d.image
            except Exception:
                pass
    textures.update(common_tex)
    n = 0
    for name, raw in texts.items():
        # 多数骨骼叫 xxx.skel。scene_07 摩天轮是不带后缀的二进制，旁边有同名 .atlas。
        paired = (name + ".atlas") in texts or (name + ".atlas.txt") in texts
        if name.endswith(".atlas") or name.endswith(".atlas.txt"):
            paired = False
        if not (name.endswith(".skel") or (name.endswith(".json") and b"skeleton" in raw[:80]) or paired):
            continue
        key = re.sub(r"\.(skel|json)$", "", name)
        folder = os.path.join(dest, key)
        if _complete(folder):
            if not os.path.isfile(os.path.join(folder, "scale.json")):
                write_scale(folder, default_scale(key))
            continue
        os.makedirs(folder, exist_ok=True)
        write_scale(folder, skel_scale(env, key))
        # 不带后缀时看内容：摩天轮是 JSON，写成 .skel 会被当成二进制读失败。
        body = raw.lstrip() if isinstance(raw, (bytes, bytearray)) else b""
        if name.endswith(".json") or body.startswith(b"{") or body.startswith(b"["):
            ext = ".json"
        else:
            ext = ".skel"
        with open(os.path.join(folder, key + ext), "wb") as f:
            f.write(raw)
        _write_atlas(folder, texts, textures, key, scene_num)
        n += 1
    return n


def load_common_tex():
    try:
        import UnityPy
    except ImportError:
        return {}
    bdir = _paths.bundle_dir()
    hits = sorted(glob.glob(os.path.join(bdir, "gamescene_common_assets_all_*.bundle")))
    if not hits:
        return {}
    env = UnityPy.load(hits[0])
    tex = {}
    for o in env.objects:
        if o.type.name != "Texture2D":
            continue
        d = o.read()
        try:
            tex[d.m_Name] = d.image
        except Exception:
            pass
    return tex


def write_index(out):
    scenes = {}
    if os.path.isdir(out):
        for sc in sorted(os.listdir(out)):
            if not sc.startswith("scene_"):
                continue
            keys = []
            root = os.path.join(out, sc)
            for name in sorted(os.listdir(root)):
                d = os.path.join(root, name)
                if os.path.isdir(d) and any(f.endswith((".skel", ".json")) for f in os.listdir(d)):
                    keys.append(name)
                    if not os.path.isfile(os.path.join(d, "scale.json")):
                        write_scale(d, default_scale(name))
            if keys:
                scenes[sc] = keys
    os.makedirs(out, exist_ok=True)
    with open(os.path.join(out, "index.json"), "w", encoding="utf-8") as f:
        json.dump({"scale": 0.005, "scenes": scenes}, f, ensure_ascii=False, indent=1)
    git = os.path.join(os.path.dirname(out), ".gitignore")
    if not os.path.isfile(git):
        with open(git, "w", encoding="utf-8") as f:
            f.write("# Muse Dash 游戏素材：仅限本机个人使用，不要提交/分发\n*\n!.gitignore\n")
    return scenes


def main():
    import argparse
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--scene", help="场景号，如 03 / scene_03")
    ap.add_argument("--all", action="store_true")
    args = ap.parse_args()
    scenes = [f"scene_{n:02d}" for n in range(1, 13)] if args.all else [scene_id(args.scene or "01")]
    src = grabber_src()
    common_tex = load_common_tex()
    os.makedirs(OUT, exist_ok=True)
    for sc in scenes:
        dest = os.path.join(OUT, sc)
        n_f8 = copy_f8(src, sc, dest) if src else 0
        num = sc[-2:]
        n_b = extract_bundle(num, dest, common_tex)
        print(f"{sc}: F8 {n_f8} 组，bundle 补 {n_b} 组 → {dest}")
    # common hearts
    dest_c = os.path.join(OUT, "common")
    n_c = extract_bundle("common", dest_c, common_tex)
    if src:
        cspine = os.path.join(src, "common", "offline_spine")
        if os.path.isdir(cspine):
            for name in os.listdir(cspine):
                d = os.path.join(cspine, name)
                if os.path.isdir(d):
                    copy_trio(d, os.path.join(dest_c, name))
    print(f"common: bundle {n_c} 组")
    scenes_idx = write_index(OUT)
    print("spine/index.json", {k: len(v) for k, v in scenes_idx.items()})
    try:
        import sync_md_assets_to_unity as sync
        sync.copy_looks()
    except Exception as e:
        print("同步 Unity 跳过：", e)


if __name__ == "__main__":
    main()
