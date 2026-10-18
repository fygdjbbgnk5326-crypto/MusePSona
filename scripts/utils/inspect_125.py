import re, os
SCRIPTS_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # MusePSona\scripts
SRC = os.path.join(SCRIPTS_DIR, "play_megalovania", "map.osu")
for ln in open(SRC, encoding="utf-8").read().splitlines():
    if not re.match(r"^\d+,\d+,\d+,\d+,", ln): continue
    p = ln.split(",")
    t, typ = int(p[2]), int(p[3])
    if not (120000 <= t <= 130000): continue
    lane = min(4, int(int(p[0])*4/512)+1)
    if typ & 128:
        end = int(p[5].split(":")[0])
        print(f"HOLD lane{lane}  {t/1000:7.2f}~{end/1000:7.2f}s  ({(end-t)/1000:.2f}s)")
    else:
        print(f"tap  lane{lane}  {t/1000:7.2f}s")
