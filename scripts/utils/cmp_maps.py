import re
from collections import Counter

def stats(f):
    objs = []
    for ln in open(f, encoding="utf-8").read().splitlines():
        if not re.match(r"^\d+,\d+,\d+,\d+,", ln): continue
        p = ln.split(","); x = int(p[0]); t = int(p[2]); typ = int(p[3])
        lane = min(4, int(x*4/512)+1); hold = (typ & 128) != 0
        end = max(int(p[5].split(":")[0]), t) if hold and p[5] else t
        objs.append({"t": t, "lane": lane, "hold": hold, "end": end})
    objs.sort(key=lambda o: (o["t"], o["lane"]))
    taps = sorted([o for o in objs if not o["hold"]], key=lambda o: o["t"])
    jumps = sum(1 for a, b in zip(taps, taps[1:]) if abs(b["lane"] - a["lane"]) >= 3)
    # 同轨 hold 重叠 / 同刻同轨双 tap
    dup = 0
    for l in range(1, 5):
        tc = Counter(o["t"] for o in objs if o["lane"] == l and not o["hold"])
        dup += sum(v - 1 for v in tc.values() if v > 1)
    hov = 0
    for l in range(1, 5):
        hs = sorted([(o["t"], o["end"]) for o in objs if o["lane"] == l and o["hold"]])
        hov += sum(1 for a, b in zip(hs, hs[1:]) if b[0] < a[1])
    lc = Counter(o["lane"] for o in objs)
    print(f"{f.split(chr(92))[-1]}: 对象{len(objs)} 大跳{jumps} 同刻同轨tap{dup} 同轨hold重叠{hov} lane{dict(sorted(lc.items()))}")

import sys
for a in sys.argv[1:]:
    stats(a)
