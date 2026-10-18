# -*- coding: utf-8 -*-
"""集中式路径探测模块（无配置文件，运行时自动发现依赖目录）。

所有脚本/工程不再写死绝对路径，统一从这里取路径。探测优先级：
  1. 环境变量（手动覆盖口子，非必须）
  2. Windows 注册表（Steam App 安装位置）
  3. Steam libraryfolders.vdf（覆盖游戏装在非默认盘的情况）
  4. 常见安装位置
  5. 相对脚本目录向上查找（针对本项目的"提取_归类后"数据目录）
  6. 明确报错并提示

环境变量一览：
  MUSE_DASH_DIR   Muse Dash 游戏根目录（含 MuseDash.exe / musedash_Data）
  MUSE_DATA_DIR   "提取_归类后" 数据根目录（含 csv/ 与各曲素材子目录）

仅依赖标准库，跨 Python 3.8+ 可用（winreg 在非 Windows 上自动降级）。
"""
import os
import sys
import glob

MUSE_DASH_APPID = 774171  # Muse Dash 的 Steam AppID（见 dump.cs: store.steampowered.com/app/774171/Muse_Dash）

# 常见 Steam 安装位置（通用布局，非某台机器专属）
_COMMON_STEAM_ROOTS = (
    r"C:\Program Files (x86)\Steam",
    r"C:\Program Files\Steam",
    r"D:\Steam",
    r"D:\steam",
)


def _env(name):
    return os.environ.get(name) or ""


# ------------------------------------------------------------------ Muse Dash 目录
def _is_muse_dash(path):
    if not path or not os.path.isdir(path):
        return False
    return (os.path.isfile(os.path.join(path, "MuseDash.exe"))
            or os.path.isdir(os.path.join(path, "musedash_Data")))


def _steam_install_path():
    """从注册表读 Steam 自身安装目录（用于定位 libraryfolders.vdf）。"""
    try:
        import winreg
    except ImportError:
        return None
    for root, sub in (
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam"),
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Valve\Steam"),
    ):
        try:
            with winreg.OpenKey(root, sub) as k:
                v, _ = winreg.QueryValueEx(k, "InstallPath")
                if v:
                    return v
        except OSError:
            pass
    return None


def _steam_app_install_location(appid):
    """从注册表读 Steam App <appid> 的 InstallLocation。"""
    try:
        import winreg
    except ImportError:
        return None
    subs = (
        r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App %d",
        r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App %d",
    )
    for root in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
        for sub in subs:
            try:
                with winreg.OpenKey(root, sub % appid) as k:
                    v, _ = winreg.QueryValueEx(k, "InstallLocation")
                    if v:
                        return v
            except OSError:
                pass
    return None


def _steam_library_paths():
    """解析 libraryfolders.vdf，返回全部 Steam 库根目录列表。"""
    import re
    vdf_candidates = []
    sp = _steam_install_path()
    if sp:
        vdf_candidates.append(os.path.join(sp, "steamapps", "libraryfolders.vdf"))
    vdf_candidates += [os.path.join(r, "steamapps", "libraryfolders.vdf")
                       for r in _COMMON_STEAM_ROOTS]

    paths = []
    for vdf in vdf_candidates:
        if not os.path.isfile(vdf):
            continue
        try:
            with open(vdf, encoding="utf-8", errors="replace") as f:
                txt = f.read()
        except OSError:
            continue
        # 新旧两种布局都靠 "path" 字段定位，兼容转义
        for m in re.findall(r'"path"\s+"((?:[^"\\]|\\.)*)"', txt):
            p = m.replace(r"\\", "\\").replace(r"\/", "/")
            if p and p not in paths:
                paths.append(p)
    return paths


def muse_dash_dir():
    """Muse Dash 游戏根目录（含 MuseDash.exe / musedash_Data）。"""
    # 1. 环境变量
    v = _env("MUSE_DASH_DIR")
    if _is_muse_dash(v):
        return v

    # 2. 注册表 Steam App 774171
    reg = _steam_app_install_location(MUSE_DASH_APPID)
    if _is_muse_dash(reg):
        return reg

    # 3. Steam 库目录
    for lib in _steam_library_paths():
        cand = os.path.join(lib, "steamapps", "common", "Muse Dash")
        if _is_muse_dash(cand):
            return cand

    # 4. 常见位置兜底
    for r in _COMMON_STEAM_ROOTS:
        cand = os.path.join(r, "steamapps", "common", "Muse Dash")
        if _is_muse_dash(cand):
            return cand

    raise FileNotFoundError(
        "未找到 Muse Dash 安装目录。请设置环境变量 MUSE_DASH_DIR 指向游戏根目录"
        "（含 MuseDash.exe / musedash_Data 的那个目录）。")


def bundle_dir():
    """谱面 bundle 所在目录：musedash_Data\\StreamingAssets\\aa\\StandaloneWindows64。"""
    return os.path.join(muse_dash_dir(), "musedash_Data", "StreamingAssets", "aa",
                        "StandaloneWindows64")


def notedata_bundle():
    """config_others_assets_notedata_*.bundle（note 描述配置表）。"""
    hits = glob.glob(os.path.join(bundle_dir(), "config_others_assets_notedata_*.bundle"))
    if not hits:
        raise FileNotFoundError(
            "未找到 config_others_assets_notedata_*.bundle：\n" + bundle_dir())
    return hits[0]


def maps_dir():
    """Mod 导出的谱面 CSV 目录：UserData\\ManiaInMuse\\maps。"""
    return os.path.join(muse_dash_dir(), "UserData", "ManiaInMuse", "maps")


# ------------------------------------------------------------------ 数据目录（提取_归类后）
def _find_up(start, name):
    """从 start 逐级向上，查找名为 name 的子目录（覆盖 start 的各级祖先）。"""
    d = os.path.abspath(start)
    while True:
        cand = os.path.join(d, name)
        if os.path.isdir(cand):
            return cand
        parent = os.path.dirname(d)
        if parent == d:
            return None
        d = parent


def data_dir():
    """"提取_归类后" 数据根目录（含 csv/ 与各曲素材子目录）。"""
    v = _env("MUSE_DATA_DIR")
    if v and os.path.isdir(v):
        return v
    here = os.path.dirname(os.path.abspath(__file__))
    d = _find_up(here, "提取_归类后")
    if d:
        return d
    raise FileNotFoundError(
        "未找到数据目录「提取_归类后」。请设置环境变量 MUSE_DATA_DIR 指向它。")


def repo_root():
    """MusePSona 仓库根（scripts 的上一级）。"""
    return os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def charts_dir():
    """自制谱工程目录：每首歌一个子文件夹。"""
    d = os.path.join(repo_root(), "自制谱")
    os.makedirs(d, exist_ok=True)
    return d


def export_dir():
    """打包 MDM 落点（与编辑器默认导出相同）。"""
    d = os.path.join(repo_root(), "导出")
    os.makedirs(d, exist_ok=True)
    return d


def _walk_parents(start, n=8):
    d = os.path.abspath(start)
    for _ in range(n):
        yield d
        parent = os.path.dirname(d)
        if parent == d:
            return
        d = parent


def editor_root():
    for d in _walk_parents(repo_root()):
        cand = os.path.join(d, "MusePSona 编辑器")
        if os.path.isdir(os.path.join(cand, "dist")) or os.path.isfile(
                os.path.join(cand, "启动编辑器.bat")):
            return cand
        if os.path.basename(d) == "MusePSona 编辑器":
            return d
    return os.path.join(os.path.dirname(repo_root()), "MusePSona 编辑器")


def editor_exe():
    p = os.path.join(editor_root(), "dist", "MuseDashChart.Editor.exe")
    return p if os.path.isfile(p) else None


def mdchart_exe():
    p = os.path.join(editor_root(), "dist", "mdchart.exe")
    return p if os.path.isfile(p) else None


def ffmpeg_exe():
    cands = [
        os.path.join(editor_root(), "dist", "ffmpeg.exe"),
        os.path.join(editor_root(), "dist", "lib", "ffmpeg.exe"),
    ]
    for d in _walk_parents(repo_root()):
        cands.append(os.path.join(d, "Malody 谱面转换器", "ffmpeg.exe"))
    for p in cands:
        if os.path.isfile(p):
            return p
    return None


def chart_folder(chart_id):
    return os.path.join(charts_dir(), chart_id)


def template_dir():
    return os.path.join(charts_dir(), "_template")


def play_client_charts_dir():
    """Unity 游玩端 StreamingAssets/Charts（固定读 song.mdm）。"""
    d = os.path.join(repo_root(), "PlayClient", "Unity", "Assets", "StreamingAssets", "Charts")
    os.makedirs(d, exist_ok=True)
    return d


def play_client_song_mdm():
    return os.path.join(play_client_charts_dir(), "song.mdm")


def unity_project_dir():
    return os.path.join(repo_root(), "PlayClient", "Unity")


def packed_mdm(chart_id):
    return os.path.join(export_dir(), chart_id, chart_id + ".mdm")
