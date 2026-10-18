"""6K osu -> 4K 压缩（同侧两轨轮转，避免音符堆单轨）。
上区(空中){1,2} ← 原 lane 1/2/5；下区(地面){3,4} ← 原 lane 3/4/6。
"""
import re, sys
from collections import defaultdict, Counter

src, dst = sys.argv[1], sys.argv[2]
lines = open(src, encoding="utf-8").read().splitlines()

AIR6 = {1, 2, 5}
lane6_of_x = lambda x: min(6, int(x * 6 / 512) + 1)
X4 = {1: 64, 2: 192, 3: 320, 4: 448}
MIN_GAP = 24

objs = []
for ln in lines:
    if re.match(r"^\d+,\d+,\d+,\d+,", ln):
        p = ln.split(",")
        l6 = lane6_of_x(int(p[0]))
        end = int(p[5].split(":")[0]) if (int(p[3]) & 128) and p[5] else int(p[2])
        objs.append({"p": p, "side": "air" if l6 in AIR6 else "gnd",
                     "start": int(p[2]), "end": max(end, int(p[2]))})
objs.sort(key=lambda o: (o["start"], 0 if o["side"] == "air" else 1))

free = {1: -1e9, 2: -1e9, 3: -1e9, 4: -1e9}
last_side = {"air": 2, "gnd": 4}          # 上次用过的同侧轨，令下一枚轮转
cur_time_used = set(); cur_time = None

for o in objs:
    if o["start"] != cur_time:
        cur_time = o["start"]; cur_time_used = set()
    pref = [1, 2] if o["side"] == "air" else [3, 4]
    other = [3, 4] if o["side"] == "air" else [1, 2]

    def ok(l):
        return free[l] <= o["start"] - MIN_GAP and l not in cur_time_used
    pool = pref + other
    avail = [l for l in pool if ok(l)]
    # 本侧两轨都可用时，选与 last_side 不同的那个 → 交替
    same_avail = [l for l in avail if l in pref]
    if len(same_avail) >= 2:
        lane = same_avail[0] if same_avail[0] != last_side[o["side"]] else same_avail[1]
    elif same_avail:
        lane = same_avail[0]
    elif avail:
        lane = avail[0]
    else:
        lane = min(pool, key=lambda l: (free[l], l not in pref))

    o["lane4"] = lane
    free[lane] = o["end"] + MIN_GAP
    cur_time_used.add(lane)
    last_side[o["side"]] = lane

res = []; k = 0
for ln in lines:
    if re.match(r"^\d+,\d+,\d+,\d+,", ln):
        o = objs[k]; k += 1
        p = o["p"]; p[0] = str(X4[o["lane4"]])
        res.append(",".join(p))
    elif ln.strip().lower().startswith("circlesize"):
        res.append("CircleSize:4")
    else:
        res.append(ln)
open(dst, "w", encoding="utf-8").write("\n".join(res) + "\n")

lc = Counter(o["lane4"] for o in objs)
per = defaultdict(list)
for o in objs: per[o["lane4"]].append((o["start"], o["end"]))
bad = sum(1 for iv in per.values()
          for a, b in zip(sorted(iv), sorted(iv)[1:]) if b[0] < a[1] + MIN_GAP)
print("4K lane分布:", dict(sorted(lc.items())), "冲突:", bad, "总:", len(objs))
