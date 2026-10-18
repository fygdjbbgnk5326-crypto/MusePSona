# -*- coding: utf-8 -*-
"""自制谱工程：新建 / 打开编辑器 / 预检 / 打包到 导出。"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _paths

_INVALID = re.compile(r'[\\/:*?"<>|]')
_PIXEL_PNG = bytes.fromhex(
    "89504e470d0a1a0a0000000d49484452000000010000000108060000001f15c489"
    "0000000a49444154789c63000100000500010d0a2db40000000049454e44ae426082"
)


def _die(msg, code=2):
    print(msg, file=sys.stderr)
    sys.exit(code)


def slug(raw: str) -> str:
    s = _INVALID.sub("_", (raw or "").strip())
    s = s.rstrip(" .")
    if not s or s in {".", "..", "_template"}:
        _die("工程 id 不合法")
    if s.startswith("_"):
        _die("工程 id 不要以下划线开头（保留给 _template）")
    return s


def _is_chart_dir(path: str) -> bool:
    return os.path.isfile(os.path.join(path, "info.json")) and any(
        os.path.isfile(os.path.join(path, f"map{i}.bms")) for i in range(1, 6)
    )


def list_charts() -> list[str]:
    root = _paths.charts_dir()
    names = []
    for name in sorted(os.listdir(root), key=str.lower):
        if name.startswith("_"):
            continue
        path = os.path.join(root, name)
        if os.path.isdir(path) and _is_chart_dir(path):
            names.append(name)
    return names


def _patch_info(folder: str, *, name: str, author: str, bpm: str, scene: str, designer: str):
    path = os.path.join(folder, "info.json")
    with open(path, encoding="utf-8") as f:
        info = json.load(f)
    info["name"] = name
    info["author"] = author
    info["bpm"] = str(bpm)
    info["scene"] = scene
    if designer:
        info["levelDesigner"] = designer
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(info, f, ensure_ascii=False, indent=2)
        f.write("\n")
    bms = os.path.join(folder, "map1.bms")
    if os.path.isfile(bms):
        text = open(bms, encoding="utf-8", errors="replace").read()
        text = re.sub(r"(?m)^#TITLE .*$", "#TITLE " + name, text, count=1)
        text = re.sub(r"(?m)^#ARTIST .*$", "#ARTIST " + author, text, count=1)
        text = re.sub(r"(?m)^#BPM .*$", "#BPM " + str(bpm), text, count=1)
        text = re.sub(r"(?m)^#GENRE .*$", "#GENRE " + scene, text, count=1)
        if designer:
            text = re.sub(r"(?m)^#LEVELDESIGN .*$", "#LEVELDESIGN " + designer, text, count=1)
        open(bms, "w", encoding="utf-8", newline="\n").write(text)


def _ensure_cover(folder: str):
    for n in ("cover.png", "cover.gif", "cover.jpg", "cover.jpeg"):
        if os.path.isfile(os.path.join(folder, n)):
            return
    with open(os.path.join(folder, "cover.png"), "wb") as f:
        f.write(_PIXEL_PNG)


def cmd_new(args):
    chart_id = slug(args.id)
    dest = _paths.chart_folder(chart_id)
    if os.path.exists(dest) and not args.force:
        _die("已存在: " + dest + "  （加 --force 覆盖模板文件，不删音频）")
    src = _paths.template_dir()
    if not _is_chart_dir(src):
        _die("缺少模板: " + src)
    os.makedirs(dest, exist_ok=True)
    for name in ("info.json", "map1.bms", "NOTES.txt"):
        shutil.copy2(os.path.join(src, name), os.path.join(dest, name))
    title = args.name or chart_id
    _patch_info(
        dest,
        name=title,
        author=args.author,
        bpm=args.bpm,
        scene=args.scene,
        designer=args.designer,
    )
    _ensure_cover(dest)
    print(dest)
    print("1. 放入 music.ogg 或 mp3（wav: python scripts/chart_project.py audio " + chart_id + " 音频）")
    print("2. python scripts/chart_project.py open " + chart_id)
    print("3. 做完: python scripts/chart_project.py install " + chart_id + " --pack")


def cmd_list(_args):
    ids = list_charts()
    if not ids:
        print("(空)  先: python scripts/chart_project.py new <id>")
        return
    for i in ids:
        info_path = os.path.join(_paths.chart_folder(i), "info.json")
        title = i
        try:
            with open(info_path, encoding="utf-8") as f:
                title = json.load(f).get("name") or i
        except (OSError, json.JSONDecodeError):
            pass
        print(f"{i}\t{title}")


def _need_mdchart() -> str:
    exe = _paths.mdchart_exe()
    if not exe:
        _die("找不到 mdchart.exe: " + os.path.join(_paths.editor_root(), "dist"))
    return exe


def cmd_lint(args):
    folder = _paths.chart_folder(slug(args.id))
    if not _is_chart_dir(folder):
        _die("不是谱面工程: " + folder)
    r = subprocess.run([_need_mdchart(), "lint", folder])
    sys.exit(r.returncode)


def _pack(chart_id: str) -> str:
    folder = _paths.chart_folder(chart_id)
    if not _is_chart_dir(folder):
        _die("不是谱面工程: " + folder)
    out_dir = os.path.join(_paths.export_dir(), chart_id)
    os.makedirs(out_dir, exist_ok=True)
    dest = os.path.join(out_dir, chart_id + ".mdm")
    r = subprocess.run([_need_mdchart(), "pack", folder, dest])
    if r.returncode != 0:
        sys.exit(r.returncode or 2)
    print(dest)
    return dest


def cmd_pack(args):
    dest = _pack(slug(args.id))
    if args.install:
        _install_mdm(dest)


def _install_mdm(src: str):
    if not os.path.isfile(src):
        _die("找不到 MDM: " + src)
    dest = _paths.play_client_song_mdm()
    shutil.copy2(src, dest)
    print("已导入 Unity 游玩端:")
    print(dest)
    print("打开 Play 场景后按 Play。覆盖的是 Charts\\song.mdm。")


def cmd_install(args):
    chart_id = slug(args.id)
    packed = _paths.packed_mdm(chart_id)
    if args.pack or not os.path.isfile(packed):
        packed = _pack(chart_id)
    _install_mdm(packed)


def cmd_uninstall(_args):
    dest = _paths.play_client_song_mdm()
    if os.path.isfile(dest):
        os.remove(dest)
        print("已删除 " + dest)
    else:
        print("游玩端没有 song.mdm")


def cmd_audio(args):
    chart_id = slug(args.id)
    folder = _paths.chart_folder(chart_id)
    if not _is_chart_dir(folder):
        _die("不是谱面工程: " + folder)
    src = os.path.abspath(args.file)
    if not os.path.isfile(src):
        _die("找不到音频: " + src)
    ext = os.path.splitext(src)[1].lower()
    if ext in (".ogg", ".mp3"):
        dest = os.path.join(folder, "music" + ext)
        shutil.copy2(src, dest)
        other = os.path.join(folder, "music.mp3" if ext == ".ogg" else "music.ogg")
        if os.path.isfile(other):
            os.remove(other)
        print(dest)
        return
    ff = _paths.ffmpeg_exe()
    if not ff:
        _die("wav/flac 需要 ffmpeg。把文件转成 music.ogg 或 music.mp3 再放进工程。")
    dest = os.path.join(folder, "music.ogg")
    r = subprocess.run([ff, "-y", "-i", src, "-c:a", "libvorbis", "-q:a", "6", dest])
    if r.returncode != 0:
        sys.exit(r.returncode or 2)
    for leftover in ("music.mp3",):
        p = os.path.join(folder, leftover)
        if os.path.isfile(p):
            os.remove(p)
    print(dest)


def cmd_unity(_args):
    proj = _paths.unity_project_dir()
    if not os.path.isdir(proj):
        _die("找不到 Unity 工程: " + proj)
    hub = r"E:\UH\Unity Hub\Unity Hub.exe"
    if os.path.isfile(hub):
        subprocess.Popen([hub, "--", "--projectPath", proj])
        print("已用 Hub 打开 " + proj)
        return
    os.startfile(proj)  # type: ignore[attr-defined]
    print(proj)


def cmd_open(args):
    folder = _paths.chart_folder(slug(args.id))
    if not _is_chart_dir(folder):
        _die("不是谱面工程: " + folder)
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import launch_editor
    code = launch_editor.launch(folder)
    if code != 0:
        sys.exit(code)
    print("已打开编辑器: " + folder)
    print("做完后在编辑器点「导出 MDM」，或回 bat 选 导出 / 打包并导入 Unity。")


def cmd_preview(args):
    player = os.path.join(_paths.repo_root(), "play", "mdm_player", "index.html")
    if not os.path.isfile(player):
        _die("找不到 play\\mdm_player\\index.html")
    os.startfile(player)  # type: ignore[attr-defined]
    out = _paths.export_dir()
    if args.id:
        packed = os.path.join(out, slug(args.id), slug(args.id) + ".mdm")
        if os.path.isfile(packed):
            os.startfile(os.path.dirname(packed))  # type: ignore[attr-defined]
            print(packed)
            return
    os.startfile(out)  # type: ignore[attr-defined]
    print("把 导出\\<id>\\<id>.mdm 拖进网页")


def main():
    p = argparse.ArgumentParser(description="MusePSona 自制谱工程")
    sub = p.add_subparsers(dest="cmd", required=True)

    n = sub.add_parser("new", help="从模板新建工程")
    n.add_argument("id", help="文件夹名，例如 my_song")
    n.add_argument("--name", default="", help="info.json 曲名，默认用 id")
    n.add_argument("--author", default="Unknown")
    n.add_argument("--designer", default="")
    n.add_argument("--bpm", default="120")
    n.add_argument("--scene", default="scene_01")
    n.add_argument("--force", action="store_true")
    n.set_defaults(func=cmd_new)

    sub.add_parser("list", help="列出 自制谱\\ 下的工程").set_defaults(func=cmd_list)

    o = sub.add_parser("open", help="用谱面编辑器打开")
    o.add_argument("id")
    o.set_defaults(func=cmd_open)

    t = sub.add_parser("lint", help="预检（mdchart lint）")
    t.add_argument("id")
    t.set_defaults(func=cmd_lint)

    k = sub.add_parser("pack", help="打包到 导出\\<id>\\<id>.mdm")
    k.add_argument("id")
    k.add_argument("--install", action="store_true", help="打包后拷到 Unity Charts/song.mdm")
    k.set_defaults(func=cmd_pack)

    x = sub.add_parser("export", help="等同编辑器「导出 MDM」：pack 到 导出\\<id>\\<id>.mdm")
    x.add_argument("id")
    x.add_argument("--install", action="store_true")
    x.set_defaults(func=cmd_pack)

    i = sub.add_parser("install", help="把导出 MDM 拷到 Unity 游玩端 song.mdm")
    i.add_argument("id")
    i.add_argument("--pack", action="store_true", help="没有包或想重打时先 pack")
    i.set_defaults(func=cmd_install)

    sub.add_parser("uninstall", help="删除 Unity 里的 song.mdm").set_defaults(func=cmd_uninstall)

    a = sub.add_parser("audio", help="把音频拷进工程为 music.ogg/mp3")
    a.add_argument("id")
    a.add_argument("file")
    a.set_defaults(func=cmd_audio)

    sub.add_parser("unity", help="用 Hub 打开 PlayClient Unity 工程").set_defaults(func=cmd_unity)

    v = sub.add_parser("preview", help="打开 MDM 预览页和导出目录")
    v.add_argument("id", nargs="?", default="")
    v.set_defaults(func=cmd_preview)

    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
