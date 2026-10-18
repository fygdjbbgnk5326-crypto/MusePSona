import re, sys, csv, json, glob, os

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SCRIPTS_DIR = os.path.dirname(SCRIPT_DIR)   # MusePSona\scripts
sys.path.insert(0, SCRIPTS_DIR)
import _paths
try:
    import UnityPy
except ImportError:
    sys.exit("缺少 UnityPy，请先执行：pip install UnityPy")

LO, HI = int(sys.argv[1]), int(sys.argv[2])
NB = _paths.notedata_bundle()
env = UnityPy.load(NB)
ta = [o for o in env.objects if o.type.name=="TextAsset"][0].read()
tpl = json.loads(ta.m_Script if isinstance(ta.m_Script,str) else bytes(ta.m_Script).decode("utf-8-sig"))
TMAP = {e["uid"]:e for e in tpl}

print(f"===== 中间产物 airfix3.csv  {LO/1000}~{HI/1000}s 原始构成 =====")
P = os.path.join(SCRIPTS_DIR, "ut_megalovania_map4_airfix3.csv")
lines = open(P, encoding="utf-8").read().splitlines()
hi = next(i for i,l in enumerate(lines) if l.startswith("index,"))
hdr = lines[hi].split(",")
iT,iAir,iTm,iL,iU = (hdr.index("type"),hdr.index("is_air"),hdr.index("time_ms"),hdr.index("length_ms"),hdr.index("note_uid"))
from collections import Counter
win = [l.split(",") for l in lines[hi+1:] if l.strip() and LO<=int(l.split(",")[iTm])<=HI]
def side(c):
    d=(TMAP.get(c[iU]) or {}).get("des","") or ""
    return "A" if any(k in d for k in("空中","下落","上升")) else ("G" if "地面" in d else c[iAir])
print("type分布:", dict(Counter((c[iT], side(c)) for c in win).most_common()))
for c in sorted(win, key=lambda z:int(z[iTm]))[:40]:
    print(f"  t={int(c[iTm])/1000:6.2f}s type{c[iT]} {side(c)} len={c[iL]} uid={c[iU]}")

print(f"\n===== 成品 map.osu  {LO/1000}~{HI/1000}s =====")
SRC = os.path.join(SCRIPTS_DIR, "play_megalovania", "map.osu")
for ln in open(SRC, encoding="utf-8").read().splitlines():
    if not re.match(r"^\d+,\d+,\d+,\d+,", ln): continue
    p=ln.split(","); t=int(p[2])
    if not (LO<=t<=HI): continue
    lane=min(4,int(int(p[0])*4/512)+1); typ=int(p[3]); hold=(typ&128)!=0
    if hold:
        end=int(p[5].split(":")[0]); print(f"  HOLD lane{lane} {t/1000:.2f}~{end/1000:.2f}s")
    else:
        print(f"  tap  lane{lane} {t/1000:.2f}s")
