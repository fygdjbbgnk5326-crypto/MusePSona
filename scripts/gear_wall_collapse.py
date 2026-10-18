"""齿轮墙塌缩 + 对立安全齿轮 v2：
pass1: 齿轮处于"对侧长条"覆盖窗口内 -> 角色被锁在对侧姿态，齿轮打不到 -> 全部删除
pass2: 剩余齿轮按八分密度聚墙；墙头 -> 天键(起跳)，墙内其余 -> 删除
用法: python gear_wall_collapse.py <in.csv> <out.csv>
"""
import csv, sys

SRC, DST = sys.argv[1], sys.argv[2]
lines = open(SRC, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.startswith("index,"))
hdr = lines[hi].split(",")
iT, iAir, iTm, iL = (hdr.index("type"), hdr.index("is_air"),
                     hdr.index("time_ms"), hdr.index("length_ms"))
bpm = float(lines[0].split("bpm=")[1].split(",")[0])
EIGHTH = 60000 / bpm / 2

rows = [l.split(",") for l in lines[hi+1:] if l.strip()]
T = lambda c: int(c[iTm]); L = lambda c: int(c[iL])

# 每侧长条区间（type3, len>60）；is_air=true 记 A 侧
holds = {}
for s, val in (("A", "true"), ("G", "false")):
    holds[s] = sorted([(T(c), T(c) + max(L(c), 0))
                       for c in rows if c[iT] == "3" and L(c) > 60 and c[iAir] == val])

def opp_hold_covers(c, pad=40):
    o = "G" if c[iAir] == "true" else "A"
    return any(st - pad <= T(c) <= en + pad for st, en in holds[o])

gears = [i for i, c in enumerate(rows) if c[iT] == "2"]
safe = {i for i in gears if opp_hold_covers(rows[i])}          # pass1

by_side = {}
for i in gears:
    if i in safe: continue
    by_side.setdefault(rows[i][iAir], []).append(i)

drop, firsts = set(safe), []
for s, idxs in by_side.items():
    idxs.sort(key=lambda i: T(rows[i]))
    run = []
    for i in idxs + [None]:
        if run and (i is None or T(rows[i]) - T(rows[run[-1]]) >= EIGHTH):
            if len(run) >= 2:
                head = rows[run[0]]
                head[iT] = "1"; head[iAir] = "true"            # 墙头 -> 起跳天键
                firsts.append(T(head))
                drop.update(run[1:])
            run = []
        if i is not None:
            run.append(i)

out = [c for i, c in enumerate(rows) if i not in drop]
kept = sum(1 for c in out if c[iT] == "2")
print(f"八分={EIGHTH:.0f}ms 齿轮{len(gears)}: 对立安全删{len(safe)} 墙头天键{len(firsts)} "
      f"墙内删{len(drop)-len(safe)} 保留type2 {kept}")
print(f"{len(rows)} -> {len(out)}  -> {DST}")

with open(DST, "w", encoding="utf-8") as f:
    f.write("\n".join(lines[:hi+1]) + "\n")
    f.write("\n".join(",".join(c) for c in out) + "\n")
