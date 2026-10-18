# -*- coding: utf-8 -*-
"""把本机抽出的场景贴图 / F9 动画包拷到 Unity StreamingAssets/MdLooks（gitignore，不进 MDM）。"""
import json
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
MD = os.path.join(ROOT, "play", "mdm_player", "assets", "md")
UNITY = os.path.join(ROOT, "PlayClient", "Unity", "Assets", "StreamingAssets", "MdLooks")


def _copy_dir(src, dst, prune=False):
    if not os.path.isdir(src):
        return False
    os.makedirs(dst, exist_ok=True)
    keep = set()
    for fn in os.listdir(src):
        if fn.endswith(".js"):
            continue
        s, d = os.path.join(src, fn), os.path.join(dst, fn)
        if os.path.isfile(s):
            shutil.copy2(s, d)
            keep.add(fn)
    if prune and os.path.isdir(dst):
        for fn in os.listdir(dst):
            if fn.endswith(".meta"):
                if fn[:-5] not in keep:
                    os.remove(os.path.join(dst, fn))
            elif fn not in keep:
                os.remove(os.path.join(dst, fn))
    return True


def _copy_tree(src, dst):
    if not os.path.isdir(src):
        return False
    copied = False
    for root, dirs, files in os.walk(src):
        rel = os.path.relpath(root, src)
        out = dst if rel == "." else os.path.join(dst, rel)
        os.makedirs(out, exist_ok=True)
        for fn in files:
            if fn.endswith(".js"):
                continue
            shutil.copy2(os.path.join(root, fn), os.path.join(out, fn))
            copied = True
    return copied


def copy_looks():
    anims, sprites, spines = [], [], []
    src_an = os.path.join(MD, "anims")
    if os.path.isdir(src_an):
        for name in sorted(os.listdir(src_an)):
            if not name.startswith("scene_"):
                continue
            folder = os.path.join(src_an, name)
            if os.path.isfile(os.path.join(folder, name + ".json")) and _copy_dir(folder, os.path.join(UNITY, "anims", name)):
                anims.append(name)
    src_sp = os.path.join(MD, "sprites")
    if os.path.isdir(src_sp):
        for name in sorted(os.listdir(src_sp)):
            if not (name.startswith("scene_") or name == "common"):
                continue
            folder = os.path.join(src_sp, name)
            if os.path.isfile(os.path.join(folder, "manifest.json")) and _copy_dir(folder, os.path.join(UNITY, "sprites", name)):
                sprites.append(name)
            bg = os.path.join(folder, "bg")
            if os.path.isfile(os.path.join(bg, "layout.json")):
                _copy_dir(bg, os.path.join(UNITY, "sprites", name, "bg"), prune=True)
    src_spn = os.path.join(MD, "spine")
    if os.path.isdir(src_spn):
        for name in sorted(os.listdir(src_spn)):
            folder = os.path.join(src_spn, name)
            if not os.path.isdir(folder):
                continue
            if name == "common" or name.startswith("scene_"):
                if _copy_tree(folder, os.path.join(UNITY, "spine", name)):
                    spines.append(name)
        idx = os.path.join(src_spn, "index.json")
        if os.path.isfile(idx):
            os.makedirs(os.path.join(UNITY, "spine"), exist_ok=True)
            shutil.copy2(idx, os.path.join(UNITY, "spine", "index.json"))
    os.makedirs(UNITY, exist_ok=True)
    with open(os.path.join(UNITY, "index.json"), "w", encoding="utf-8") as f:
        json.dump({"anims": anims, "sprites": sprites, "spine": spines}, f, ensure_ascii=False, indent=1)
    git = os.path.join(UNITY, ".gitignore")
    if not os.path.isfile(git):
        with open(git, "w", encoding="utf-8") as f:
            f.write("# 游戏素材仅限本机，不要提交/分发\n*\n!.gitignore\n")
    print("Unity MdLooks：anims", anims, "sprites", sprites, "spine", spines, "→", UNITY)
    return anims, sprites, spines


if __name__ == "__main__":
    copy_looks()
