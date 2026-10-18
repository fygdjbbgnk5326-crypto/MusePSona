# -*- coding: utf-8 -*-
"""官谱入口：music bundle → MDM，列出曲目，打开 导出。"""
from __future__ import annotations

import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import _paths

_OFF = os.path.join(os.path.dirname(os.path.abspath(__file__)), "official_to_mdm.py")
_DEC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "map_decoder.py")
_CMP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "compare_mdm_official.py")


def _run(args: list[str]) -> int:
    return subprocess.call([sys.executable, *args])


def _menu() -> int:
    while True:
        print()
        print("  ================================================")
        print("     MusePSona 官谱解析")
        print("     导出: " + _paths.export_dir())
        print("  ================================================")
        print()
        print("   [1] 转录 music bundle 为 MDM")
        print("   [2] 列出游戏曲目")
        print("   [3] 打开导出目录")
        print("   [4] 自制谱对照官包")
        print("       1 槽 0F→24 星（撞键丢掉）；type7 23 仍是音符")
        print("   [0] 退出")
        print()
        opt = input("  请选择: ").strip()
        if opt == "0":
            return 0
        if opt == "1":
            src = input("  music_*.bundle 路径: ").strip().strip('"')
            if not src:
                continue
            if not os.path.isfile(src):
                print("  找不到文件")
                continue
            if _run([_OFF, "list", src]) != 0:
                continue
            maps = input("  map号 回车=全部，或 1 或 1,3 : ").strip()
            out = _paths.export_dir()
            cmd = [_OFF, "export", src, "--out", out]
            if maps:
                cmd.append("--maps=" + maps)
            _run(cmd)
            continue
        if opt == "2":
            _run([_DEC, "list"])
            continue
        if opt == "3":
            out = _paths.export_dir()
            os.startfile(out)  # type: ignore[attr-defined]
            continue
        if opt == "4":
            src = input("  自制 .mdm 或导出文件夹: ").strip().strip('"')
            if not src:
                continue
            if not os.path.exists(src):
                print("  找不到路径")
                continue
            bun = input("  music_*.bundle（回车=按曲名搜）: ").strip().strip('"')
            cmd = [_CMP, src]
            if bun:
                cmd.append(bun)
            _run(cmd)
            continue
        print("  无效选项")


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    if not os.path.isfile(_OFF):
        print("找不到 official_to_mdm.py", file=sys.stderr)
        sys.exit(1)
    sys.exit(_menu() or 0)
