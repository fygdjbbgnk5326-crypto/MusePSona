"""轨道选择优化：在"空{1,2}/地{3,4}"侧内，为每个 tap 选离上一指位最近的轨，
减少 1<->4 大跳；密集段(<94ms 同侧)保持交替防鬼手。长按不动。
用法: python optim_lanes.py <map.osu> <out.osu>
"""
import re, sys
from collections import defaultdict

SRC, DST = sys.argv[1], sys.argv[2]
S16 = 62.5; FAST = S16 * 1.5
lines = open(SRC, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.strip().lower() == "[hitobjects]")
head = lines[:hi+1]
objs = []
for ln in lines[hi+1:]:
    if not ln.strip() or not re.match(r"^\d+,\d+,\d+,\d+,", ln): continue
    p = ln.split(",")
    x, t, typ = int(p[0]), int(p[2]), int(p[3])
    lane = min(4, int(x*4/512)+1)
    hold = (typ & 128) != 0
    objs.append({"p": p, "t": t, "lane": lane, "hold": hold})
objs.sort(key=lambda o: (o["t"], o["lane"]))

SIDE_LANES = {0: (1, 2), 1: (3, 4)}
side_of = lambda l: 0 if l <= 2 else 1
hold_heads = {(o["t"], o["lane"]) for o in objs if o["hold"]}
prev = None; prev_t = None
for o in objs:
    s = side_of(o["lane"])
    if o["hold"]:
        continue
    cands = [l for l in SIDE_LANES[s] if (o["t"], l) not in hold_heads] or list(SIDE_LANES[s])
    if prev is not None:
        if s == side_of(prev) and o["t"] - prev_t < FAST:
            cands = [l for l in cands if l != prev] or cands
        o["lane"] = min(cands, key=lambda l: abs(l - prev))
    if not o["hold"]:
        prev = o["lane"]; prev_t = o["t"]

out = []
for o in sorted(objs, key=lambda x: (x["t"], x["lane"])):
    p = o["p"]; p[0] = str({1: 64, 2: 192, 3: 320, 4: 448}[o["lane"]])
    out.append(",".join(p))
open(DST, "w", encoding="utf-8").write("\n".join(head + out + [""]))

# 前后对比
def jumps(os_):
    ts = sorted([o for o in os_ if not o["hold"]], key=lambda o: o["t"])
    return sum(1 for a, b in zip(ts, ts[1:]) if abs(b["lane"] - a["lane"]) >= 3)
print(f"跨3轨大跳: 优化前(按原lane记录) 见 scan 对比 ->  本文件: {jumps(objs)}")
