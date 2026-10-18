"""ut_megalovania_map4 管线 v2（精确动画过滤）：
剔除：套条内齿轮(动画) + type14~28特效 + type0 boss脚本；独立齿轮保留交生成器。
airfix(模板des补is_air) -> OsuGenerator(6K) -> compress_4k -> play_megalovania
"""
import csv, json, glob, os, re, shutil, subprocess, sys
from collections import Counter

# 相对路径：脚本所在目录
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import UnityPy
import _paths

WD = SCRIPT_DIR
SRC = os.path.join(_paths.data_dir(), "csv", "ut_megalovania_map4.csv")
EXE = os.path.join(SCRIPT_DIR, r"..\OsuGenerator\bin\Release\net9.0\ManiaInMuse.OsuGenerator.exe")

NB = _paths.notedata_bundle()
env = UnityPy.load(NB)
ta = [o for o in env.objects if o.type.name == "TextAsset"][0].read()
tpl = json.loads(ta.m_Script if isinstance(ta.m_Script, str) else bytes(ta.m_Script).decode("utf-8-sig"))
TMAP = {e["uid"]: e for e in tpl}
uid2air = {e["uid"]: ("true" if any(k in e.get("des","") for k in ("空中","下落","上升"))
                     else ("false" if "地面" in e.get("des","") else "")) for e in tpl}

lines = open(SRC, encoding="utf-8").read().splitlines()
hi = next(i for i, l in enumerate(lines) if l.startswith("index,"))
hdr = lines[hi].split(",")
i_type, i_uid, i_t, i_len, i_air = (hdr.index("type"), hdr.index("note_uid"),
                                    hdr.index("time_ms"), hdr.index("length_ms"), hdr.index("is_air"))
i_sec = hdr.index("multi_duration_sec")
rows = [l.split(",") for l in lines[hi+1:] if l.strip()]

def side_of(c):
    d = (TMAP.get(c[i_uid]) or {}).get("des", "") or ""
    if any(k in d for k in ("空中","下落","上升")): return "A"
    if "地面" in d: return "G"
    return "A" if c[i_air] == "true" else "G"

holds = [(int(c[i_t]), int(c[i_t]) + int(c[i_len]), side_of(c))
         for c in rows if c[i_type] == "3" and int(c[i_len]) > 60]
def nested(c):
    t = int(c[i_t]); s = side_of(c)
    return any(t0 <= t <= t1 and s == sh for t0, t1, sh in holds)

drop_nested_gear = drop_fx = drop_script = keep_gear = 0
kept = []
for c in rows:
    t = c[i_type]
    if t == "0":
        drop_script += 1; continue
    if t.isdigit() and int(t) >= 14:
        drop_fx += 1; continue
    if t == "2":
        if nested(c): drop_nested_gear += 1; continue
        keep_gear += 1
    if t != "5" and c[i_uid] in uid2air and uid2air[c[i_uid]]:
        c[i_air] = uid2air[c[i_uid]]
    c = c[:i_sec+1] + [""] + c[i_sec+1:]
    kept.append(c)

print(f"输入 {len(rows)} -> 保留 {len(kept)}")
print(f"剔除: 套条齿轮{drop_nested_gear} 特效type14~28 {drop_fx} boss脚本type0 {drop_script}")
print(f"保留独立齿轮(交生成器姿态模拟): {keep_gear}")

hdr2 = hdr[:i_sec+1] + ["multi_duration_ms"] + hdr[i_sec+1:]
airfix = os.path.join(WD, "ut_megalovania_map4_airfix.csv")
with open(airfix, "w", encoding="utf-8") as f:
    f.write("\n".join(lines[:hi]) + "\n" + ",".join(hdr2) + "\n")
    f.write("\n".join(",".join(c) for c in kept) + "\n")

airfix2 = os.path.join(WD, "ut_megalovania_map4_airfix2.csv")
r = subprocess.run([sys.executable, os.path.join(WD, "music_to_hold.py"), airfix, airfix2],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("music->hold:", (r.stdout or r.stderr).strip()[:160])

airfix3 = os.path.join(WD, "ut_megalovania_map4_airfix3.csv")
r = subprocess.run([sys.executable, os.path.join(WD, "gear_wall_collapse.py"), airfix2, airfix3],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("齿轮墙塌缩:", (r.stdout or r.stderr).strip()[:200])

o6 = os.path.join(WD, "ut_megalovania_map4_6k.osu")
e2 = dict(os.environ); e2["DOTNET_ROLL_FORWARD"] = "LatestMajor"
r = subprocess.run([EXE, airfix3, o6, "--title", "Megalovania"], env=e2,
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("6K:", (r.stdout or r.stderr).strip()[:160])

PDIR = os.path.join(WD, "play_megalovania"); os.makedirs(PDIR, exist_ok=True)
r = subprocess.run([sys.executable, os.path.join(WD, "compress_4k.py"), o6, os.path.join(PDIR, "map.osu")],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("4K:", (r.stdout or r.stderr).strip()[:160])
shutil.copy(os.path.join(WD, "play_cthugha", "index.html"), os.path.join(PDIR, "index.html"))
shutil.copy(os.path.join(_paths.data_dir(), "ut_megalovania", "ut_megalovania_music.wav"), os.path.join(PDIR, "audio.wav"))

BPM = float(re.search(r"bpm=([\d.]+)", lines[0]).group(1))
mp = os.path.join(PDIR, "map.osu")
r = subprocess.run([sys.executable, os.path.join(WD, "simplify_streams.py"), mp, str(BPM), mp + ".tmp"],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("流简化:", (r.stdout or r.stderr).strip()[:200])
if os.path.exists(mp + ".tmp"):
    shutil.move(mp + ".tmp", mp)

# 轨道优化：侧内就近选轨消大跳（密集段保持交替）
r = subprocess.run([sys.executable, os.path.join(WD, "optim_lanes.py"), mp, mp + ".tmp"],
                   capture_output=True, text=True, encoding="utf-8", errors="replace")
print("轨道优化:", (r.stdout or r.stderr).strip()[:120])
if os.path.exists(mp + ".tmp"):
    shutil.move(mp + ".tmp", mp)

objs = [ln.split(",") for ln in open(os.path.join(PDIR, "map.osu"), encoding="utf-8").read().splitlines()
        if re.match(r"^\d+,\d+,\d+,\d+,", ln)]
lane4 = lambda x: min(4, int(int(x)*4/512)+1)
lc = Counter(lane4(o[0]) for o in objs)
from collections import defaultdict
per = defaultdict(list)
for o in objs: per[lane4(o[0])].append(int(o[2]))
dup = sum(v-1 for iv in per.values() for k, v in Counter(iv).items() if v > 1)
print(f"成品: 对象{len(objs)} 长按{sum(1 for o in objs if int(o[3])&128)} lane{dict(sorted(lc.items()))} 同刻同轨{dup}")
