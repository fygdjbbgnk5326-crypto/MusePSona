"""骨头海 128~138s 窗口按类型拆解 + 全图 music 流普查"""
import csv, json, glob, sys, os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SCRIPTS_DIR = os.path.dirname(SCRIPT_DIR)   # MusePSona\scripts
sys.path.insert(0, SCRIPTS_DIR)
import _paths
try:
    import UnityPy
except ImportError:
    sys.exit("缺少 UnityPy，请先执行：pip install UnityPy")

P = os.path.join(_paths.data_dir(), "csv", "ut_megalovania_map4.csv")
lines = open(P, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.startswith("index,"))
rows = [r for r in csv.DictReader(lines[hi:])]

NB = _paths.notedata_bundle()
env = UnityPy.load(NB)
ta = [o for o in env.objects if o.type.name == "TextAsset"][0].read()
tpl = json.loads(ta.m_Script if isinstance(ta.m_Script, str) else bytes(ta.m_Script).decode("utf-8-sig"))
TMAP = {e["uid"]: e for e in tpl}

def side(r):
    d = (TMAP.get(r["note_uid"]) or {}).get("des", "") or ""
    if any(k in d for k in ("空中", "下落", "上升")): return "A"
    if "地面" in d: return "G"
    return "?"

T = lambda r: int(r["time_ms"])

print("== 128~138s 窗口类型构成 ==")
w = [r for r in rows if 128000 <= T(r) <= 138000]
from collections import Counter
print(" ", dict(Counter((r["type_name"], side(r)) for r in w).most_common()))
gh = sorted([T(r) for r in w if r["type_name"] == "ghost"], key=int)
print("  鬼魂时间戳:", gh)
m7a = sorted(T(r) for r in w if r["type_name"] == "music" and side(r) == "A")
m7g = sorted(T(r) for r in w if r["type_name"] == "music" and side(r) == "G")
print(f"  music 空侧 {len(m7a)} 个: {m7a[:8]}...{m7a[-3:] if m7a else ''}")
print(f"  music 地侧 {len(m7g)} 个: {m7g}")

# 全图 music 流普查（间隔<=450ms、>=9 连）
print("\n== 全图 music(type7) 同侧流普查 ==")
GAP, MIN = 450, 9
for s in ("A", "G"):
    taps = sorted([r for r in rows if r["type_name"] == "music" and side(r) == s], key=T)
    run, runs = [], []
    for r in taps + [None]:
        if run and (r is None or T(r) - T(run[-1]) > GAP):
            if len(run) >= MIN: runs.append(list(run))
            run = []
        if r is not None: run.append(r)
    for run in runs:
        print(f"  {s}侧 {T(run[0])/1000:7.2f}s~{T(run[-1])/1000:7.2f}s len={len(run)}")
