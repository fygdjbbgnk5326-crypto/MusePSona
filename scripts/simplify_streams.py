"""4K map.osu 后处理：密集流简化为长条（手感优化规则 v2）
- 同侧(空1/2 地3/4)连续 tap 流：相邻间隔 <= 2x16分(125ms@240BPM) 且 长度 >= 9 -> 合并为一条 hold
- 对侧音符为"单点"，保持 tap 不动（单点+长条配置）
- 与已有真长按同侧重叠的流跳过；不删任何对侧点
用法: python simplify_streams.py <map.osu> <bpm> <输出.osu>
"""
import re, sys

SRC, BPM, DST = sys.argv[1], float(sys.argv[2]), sys.argv[3]
S16 = 60000 / BPM / 4
GAP = S16 * 2 + 2          # 125ms@240：允许八分间隙仍算同一流
MIN_RUN = 9                 # "超过8"

lines = open(SRC, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.strip().lower() == "[hitobjects]")
head = lines[:hi+1]

objs = []
for ln in lines[hi+1:]:
    if not ln.strip() or not re.match(r"^\d+,\d+,\d+,\d+,", ln): continue
    p = ln.split(",")
    x, t, typ = int(p[0]), int(p[2]), int(p[3])
    lane = min(4, int(x * 4 / 512) + 1)
    hold = (typ & 128) != 0
    end = max(int(p[5].split(":")[0]), t) if hold and p[5] else t
    objs.append({"t": t, "typ": typ, "snd": p[4], "y": p[1], "x": x,
                 "lane": lane, "hold": hold, "end": end, "drop": False})

SIDE = {1: 0, 2: 0, 3: 1, 4: 1}
X4 = {1: 64, 2: 192, 3: 320, 4: 448}

streams = []
for s in (0, 1):
    taps = sorted([o for o in objs if SIDE[o["lane"]] == s and not o["hold"]], key=lambda o: o["t"])
    run = []
    for o in taps + [None]:
        if run and (o is None or o["t"] - run[-1]["t"] > GAP):
            if len(run) >= MIN_RUN: streams.append((s, list(run)))
            run = []
        if o is not None: run.append(o)

made = skipped = trill = 0
for s, run in streams:
    t0, t1 = run[0]["t"], run[-1]["t"]
    if any(o["hold"] and SIDE[o["lane"]] == s and o["t"] < t1 + 40 and o["end"] > t0 - 40 for o in objs):
        skipped += 1; continue
    # 轮指保护：对侧同窗口内音符密度 >= 本侧一半 -> 两侧交替流，保持 tap
    o_side = 1 - s
    opp = [o for o in objs if not o["hold"] and SIDE[o["lane"]] == o_side and t0 <= o["t"] <= t1]
    if len(opp) >= 4:
        trill += 1; continue
    lane = run[0]["lane"]
    for o in run: o["drop"] = True
    objs.append({"t": t0, "typ": 128, "snd": "0", "y": "192", "x": X4[lane],
                 "lane": lane, "hold": True, "end": t1, "drop": False})
    made += 1

final = [o for o in objs if not o["drop"]]
final.sort(key=lambda o: (o["t"], o["lane"]))
out = [f"{o['x']},{o['y']},{o['t']},{o['typ']},{o['snd']},"
       + (f"{o['end']}:0:0:0:0:" if o["hold"] else "0:0:0:0:0:") for o in final]
open(DST, "w", encoding="utf-8").write("\n".join(head + out + [""]))

n_taps = sum(1 for o in final if not o["hold"])
n_hold = len(final) - n_taps
print(f"对象 {len([o for o in objs if not o['drop']])} -> {len(final)}  (tap {n_taps} / hold {n_hold})  简化{made}条 跳过{skipped}条 轮指保护{trill}条")
for s, run in streams:
    print(f"  {'空' if s==0 else '地'}侧 {run[0]['t']/1000:7.2f}s~{run[-1]['t']/1000:7.2f}s  len={len(run)}")
