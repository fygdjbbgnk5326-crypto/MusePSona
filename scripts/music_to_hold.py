"""airfix 前置步骤：music(type7) 密集流 -> 长按(type3) 改写 v3。
- 单侧连续 music 流（间隔<=100ms、>=3连、跨度>=0.4s）-> 转一条 hold
- 双侧交错流（80s 型：任一时刻只有一侧有 music，但两侧合计密集）-> 两侧各自转 hold 首尾接条
- 双侧真正同时有 music（122s 型重叠轮指）-> 保持 tap
- 与已有长条重叠的流跳过
用法: python music_to_hold.py <airfix.csv> <输出.csv>
"""
import csv, json, glob, sys, bisect, os

# 相对路径：脚本所在目录
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import UnityPy
import _paths

SRC, DST = sys.argv[1], sys.argv[2]
NB = _paths.notedata_bundle()
env = UnityPy.load(NB)
ta = [o for o in env.objects if o.type.name == "TextAsset"][0].read()
tpl = json.loads(ta.m_Script if isinstance(ta.m_Script, str) else bytes(ta.m_Script).decode("utf-8-sig"))
TMAP = {e["uid"]: e for e in tpl}

lines = open(SRC, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.startswith("index,"))
hdr = lines[hi].split(",")
iT, iU, iTm, iL, iAir = (hdr.index("type"), hdr.index("note_uid"), hdr.index("time_ms"),
                         hdr.index("length_ms"), hdr.index("is_air"))
rows = [l.split(",") for l in lines[hi+1:] if l.strip()]

def side(c):
    d = (TMAP.get(c[iU]) or {}).get("des", "") or ""
    if any(k in d for k in ("空中", "下落", "上升")): return "A"
    if "地面" in d: return "G"
    return "A" if c[iAir] == "true" else "G"

T = lambda c: int(c[iTm])
holds = [(T(c), T(c) + int(c[iL])) for c in rows if c[iT] == "3" and int(c[iL]) > 60]

GAP, MIN, MIN_SPAN = 100, 3, 150
TRILL_NEAR = 300            # 对侧流首尾相距 <300ms 视为快速交替 = 轮指

# 双侧 music 时间点（用于重叠判定）
mus_t = {s: sorted(T(c) for c in rows if c[iT] == "7" and side(c) == s) for s in ("A", "G")}

# 1) 各侧找流（先不过滤 span，用于轮指判定）
def find_runs(s, min_len=MIN):
    idxs = sorted([i for i, c in enumerate(rows) if c[iT] == "7" and side(c) == s], key=lambda i: T(rows[i]))
    runs, run = [], []
    for i in idxs + [None]:
        if run and (i is None or T(rows[i]) - T(rows[run[-1]]) > GAP):
            if len(run) >= min_len:
                runs.append(run)
            run = []
        if i is not None: run.append(i)
    return runs

runsA, runsG = find_runs("A"), find_runs("G")
sA = [(T(rows[r[0]]), T(rows[r[-1]])) for r in runsA]
sG = [(T(rows[r[0]]), T(rows[r[-1]])) for r in runsG]

NEAR = 250
def is_trill(t0, t1, side_cur):
    """本流窗口附近若被对侧 music 密集填充(>=2 个) -> 连续交替轮指，保持 tap；
    否则为孤立爆发流 -> 转长条。"""
    o = "G" if side_cur == "A" else "A"
    cnt = sum(1 for x in mus_t[o] if t0 - NEAR <= x <= t1 + NEAR)
    return cnt >= 2

drop, made = set(), []
def convert(run):
    t0, t1 = T(rows[run[0]]), T(rows[run[-1]])
    if any(t0 <= e and t1 >= st for st, e in holds): return
    head = rows[run[0]]
    head[iT] = "3"; head[iL] = str(t1 - t0 + 40)
    drop.update(run[1:])
    made.append((side(rows[run[0]]), t0, t1))

n_trill = 0
trill_windows = []
for s, runs in (("A", runsA), ("G", runsG)):
    for run in runs:
        t0, t1 = T(rows[run[0]]), T(rows[run[-1]])
        if t1 - t0 < MIN_SPAN:
            if is_trill(t0, t1, s):
                trill_windows.append((t0, t1, s))   # 轮指段内的碎片流也保护
            continue                                 # 碎片流不转条
        if is_trill(t0, t1, s):
            trill_windows.append((t0, t1, s))
            n_trill += 1; continue
        convert(run)

# 轮指窗口（含邻域）内的所有 type7 music -> type1 强制击打，防被姿态模拟吸收
NEAR2 = 120
fixed = 0
for c in rows:
    if c[iT] != "7": continue
    t = T(c); s = side(c)
    for w0, w1, ws in trill_windows:
        if ws == s and w0 - NEAR2 <= t <= w1 + NEAR2:
            c[iT] = "1"; fixed += 1
            break
n_trill = len(set(trill_windows))

kept = [c for i, c in enumerate(rows) if i not in drop]
out = lines[:hi+1] + [",".join(c) for c in kept]
open(DST, "w", encoding="utf-8").write("\n".join(out) + "\n")
print(f"music流->长按: {len(made)} 条  轮指保持tap {n_trill} 段  删除散点 {len(drop)}  {len(rows)} -> {len(kept)}")
for s, t0, t1 in made[:12]:
    print(f"  {s}侧 {t0/1000:.2f}~{t1/1000:.2f}s")
