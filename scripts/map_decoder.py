"""离线谱面解码器：music_*.bundle -> StageInfo -> SerializedBytes -> note 列表

子命令:
  selftest                    解码 cthugha 并与 MapLoader 导出的 CSV 基准逐毫秒验证
  list   [目录]               批量查看目录下全部 music_*.bundle 的谱面元数据(曲名/作曲)
  export [目录] [输出目录]     批量导出解析后的谱面文件(每个 map 一个 CSV)

选项:
  --limit N                   只处理前 N 个 bundle
  --dict <路径>               指定兜底词典文件（默认 scripts\\data\\map_dictionary.txt）
  --no-dict                   禁用曲名翻译, 仅显示内部代号

曲名来源优先级: 游戏官方配置表(config_others_assets_album*/defaultanchor 的 name+author)
> 兜底词典文件；两者都查不到时显示内部代号。
"""
import UnityPy, re, struct, os, glob, json, sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import _paths

DICT_PATH = os.path.join(SCRIPT_DIR, "data", "map_dictionary.txt")

TICK = b"t\x00i\x00c\x00k\x00\x00\x00"

def rd_dec(b, p):
    """Sirenix Decimal: 4字节flags(符号bit31, scale<<16) + 4字节words[2]hi + lo + mid"""
    flags = struct.unpack_from("<I", b, p)[0]
    hi    = struct.unpack_from("<I", b, p + 4)[0]
    lo    = struct.unpack_from("<I", b, p + 8)[0]
    mid   = struct.unpack_from("<I", b, p + 12)[0]
    scale = (flags >> 16) & 0xFF
    v = ((hi << 64) | (mid << 32) | lo) / (10 ** scale)
    return -v if (flags >> 31) & 1 else v

def get_serialized_bytes(mb_obj):
    sd = mb_obj.serializationData
    # UnityPy: serializationData 是带属性的树容器（SerializedFormat / SerializedBytes）
    for attr in ("SerializedBytes", "serializedBytes"):
        v = getattr(sd, attr, None)
        if v is not None:
            return bytes(v) if not isinstance(v, bytes) else v
    # 兜底：dict 递归
    def walk(n):
        if isinstance(n, dict):
            for k, x in n.items():
                if k == "SerializedBytes" and isinstance(x, (list, bytes, bytearray)):
                    return bytes(x)
            for x in n.values():
                r = walk(x)
                if r is not None: return r
        elif isinstance(n, (list, tuple)):
            for x in n:
                r = walk(x)
                if r is not None: return r
        return None
    try:
        return walk(dict(sd))
    except Exception:
        return None

def decode_map(by):
    """从 SerializedBytes 解出 note 时间线（已验证的核心字段）"""
    anchors = [m.start() for m in re.finditer(re.escape(TICK), by)]
    notes = []
    n = len(anchors)
    for i, a in enumerate(anchors):
        hi_b = anchors[i + 1] if i + 1 < n else len(by)
        w = by[a:hi_b]
        tick_s = rd_dec(w, 8)
        p = w.find(b"i\x00d\x00\x00\x00")
        oid = struct.unpack_from("<I", w, p + 4)[0] if p >= 0 else None
        notes.append({"tick_sec": round(tick_s, 3), "obj_id": oid})
    notes.sort(key=lambda x: x["tick_sec"])
    return notes

def decode_bundle(path):
    """bundle -> {mapName: {meta, notes}}"""
    out = {}
    env = UnityPy.load(path)
    for o in env.objects:
        if o.type.name != "MonoBehaviour": continue
        d = o.read()
        name = getattr(d, "m_Name", "") or ""
        if "_map" not in name: continue
        sb = get_serialized_bytes(d)
        if not sb: continue
        notes = decode_map(sb)
        try: bpm = float(d.bpm)
        except Exception: bpm = 0
        try: diff = int(d.difficulty)
        except Exception: diff = -1
        out[name] = {"bpm": bpm, "difficulty": diff, "md5": getattr(d, "md5", ""),
                     "music": getattr(d, "music", ""), "scene": getattr(d, "scene", ""),
                     "note_count": len(notes), "notes": notes}
    return out

# ---------------- 批量处理 ----------------

def iter_bundles(bundle_dir):
    """目录下全部谱面 bundle（music_*_assets_all.bundle 及 music_*.bundle）"""
    pats = [os.path.join(bundle_dir, "music_*_assets_all.bundle"),
            os.path.join(bundle_dir, "music_assets_*.bundle")]
    seen, files = set(), []
    for p in pats:
        for f in sorted(glob.glob(p)):
            if f not in seen:
                seen.add(f); files.append(f)
    return files

# ---------------- 曲名词典：官方配置表为主，词典文件兜底 ----------------

def _ta_text(o):
    x = o.read()
    return x.m_Script if isinstance(x.m_Script, str) else bytes(x.m_Script).decode("utf-8-sig", "replace")

def _walk_songs(node):
    """递归找出含 name/music 的曲目节点"""
    if isinstance(node, dict):
        if node.get("music") and node.get("name"):
            yield node
        else:
            for v in node.values(): yield from _walk_songs(v)
    elif isinstance(node, list):
        for v in node: yield from _walk_songs(v)

def _song_key(e):
    """music 字段(cthugha_music) -> bundle/map 代号(cthugha)"""
    k = str(e["music"]).strip()
    return k[:-6] if k.endswith("_music") else k

def _loads_lenient(s):
    """游戏配置 JSON 常含 // 整行注释与尾逗号，容错解析"""
    for cand in (s, re.sub(r"^\s*//[^\n]*$", "", s, flags=re.M),
                 re.sub(r",\s*([}\]])", r"\1", re.sub(r"^\s*//[^\n]*$", "", s, flags=re.M))):
        for kw in ({}, {"strict": False}):
            try:
                return json.loads(cand, **kw)
            except Exception:
                continue
    return None

def _parse_song_json(s):
    j = _loads_lenient(s)
    if j is None: return {}
    m = {}
    for e in _walk_songs(j):
        k = _song_key(e)
        if k: m[k] = (str(e["name"]).strip(), str(e.get("author", "") or "").strip())
    return m

def _parse_song_entries(s):
    j = _loads_lenient(s)
    if j is None:
        return {}
    m = {}
    for e in _walk_songs(j):
        k = _song_key(e)
        if k:
            m[k] = e
    return m

def load_official_catalog(bundle_dir):
    """官方曲库 MusicInfo -> {代号: 完整 JSON 节点}。album* 覆盖 defaultanchor。"""
    d = {}
    pats = [os.path.join(bundle_dir, "config_others_assets_defaultanchor_*.bundle"),
            os.path.join(bundle_dir, "config_others_assets_album*.bundle")]
    for p in pats:
        for f in sorted(glob.glob(p)):
            try:
                for o in UnityPy.load(f).objects:
                    if o.type.name != "TextAsset":
                        continue
                    d.update(_parse_song_entries(_ta_text(o)))
            except Exception:
                pass
    return d

def load_official_dict(bundle_dir):
    """官方配置表 -> {代号: (曲名, 作曲)}，覆盖 DLC 用 config_others_assets_album*，全量用 defaultanchor"""
    d = {}
    pats = [os.path.join(bundle_dir, "config_others_assets_defaultanchor_*.bundle"),
            os.path.join(bundle_dir, "config_others_assets_album*.bundle")]
    for p in pats:
        for f in sorted(glob.glob(p)):
            try:
                for o in UnityPy.load(f).objects:
                    if o.type.name != "TextAsset": continue
                    d.update(_parse_song_json(_ta_text(o)))
            except Exception:
                pass
    return d

def load_file_dict(path):
    """词典文件 -> {代号: (曲名, 作曲)}；兼容两列(代号\t曲名)与三列(代号\t曲名\t作曲)，跳过 TRUNCATED 坏 key"""
    d = {}
    if not os.path.isfile(path): return d
    for enc in ("utf-8-sig", "gbk"):
        try: raw = open(path, encoding=enc).read(); break
        except Exception: raw = None
    if not raw: return d
    for ln in raw.splitlines():
        parts = ln.split("\t")
        if len(parts) < 2: continue
        k, v = parts[0].strip(), parts[1].strip()
        a = parts[2].strip() if len(parts) > 2 else ""
        if k and "TRUNCATED" not in k and v:
            d.setdefault(k, (v, a))
    return d

def load_dict(bundle_dir, dict_path=DICT_PATH):
    off, fd = load_official_dict(bundle_dir), load_file_dict(dict_path)
    return {**fd, **off}, len(off), len(fd)

def display_name(music_field, dic, map_name=""):
    """代号优先查表：music 字段 -> map 名去 _mapN 后缀 -> 原名；未命中回退空"""
    cands = []
    if music_field:
        cands.append(music_field[:-6] if music_field.endswith("_music") else music_field)
    if map_name:
        cands.append(re.sub(r"_map\d+$", "", map_name))
    for k in cands:
        hit = dic.get(k)
        if hit and (hit[0] or hit[1]): return hit
    return ("", "")

def collect_maps(bundle_dir, limit=0):
    """批量解码 -> [(bundle文件名, {mapName: data}), ...]，单个文件失败不影响整体"""
    files = iter_bundles(bundle_dir)
    if limit: files = files[:limit]
    res, errs = [], []
    for i, f in enumerate(files, 1):
        bn = os.path.basename(f)
        print(f"\r  解码进度 {i}/{len(files)}  {bn[:56]:<56}", end="", flush=True)
        try:
            m = decode_bundle(f)
            if m: res.append((bn, m))
        except Exception as e:
            errs.append((bn, f"{type(e).__name__}: {e}"))
    print(f"\r  解码完成：共 {len(files)} 个 bundle，成功 {len(res)}，失败 {len(errs)}"
          f"{' '*20}")
    return res, errs

def meta_rows(res):
    """展平为表格行"""
    rows = []
    for bn, maps in res:
        for name, d in sorted(maps.items()):
            rows.append({"bundle": bn, "map": name, "notes": d["note_count"],
                         "bpm": d["bpm"], "difficulty": d["difficulty"],
                         "music": d["music"], "scene": d["scene"], "md5": d["md5"]})
    return rows

def cmd_list(bundle_dir, limit=0, dic=None):
    if not os.path.isdir(bundle_dir):
        print(f"[错误] 目录不存在: {bundle_dir}"); return 1
    if dic is None: dic = {}
    print(f"扫描目录: {bundle_dir}")
    res, errs = collect_maps(bundle_dir, limit)
    rows = meta_rows(res)
    if not rows:
        print("未发现任何谱面。"); return 1
    # 按曲目聚合：以 map 名去掉 _mapN 后缀为键（同曲各难度一致），一行一首
    groups, gmap = [], {}
    for r in rows:
        key = re.sub(r"_map\d+$", "", r["map"]) or r["music"] or r["map"]
        if key not in gmap:
            title, author = display_name(r["music"], dic, r["map"])
            gmap[key] = {"title": title or r["map"], "author": author, "count": 0}
            groups.append(gmap[key])
        gmap[key]["count"] += 1
    def wlen(s):
        return sum(2 if ord(c) > 0x2E80 else 1 for c in s)
    def clip(s, n):
        out = ""
        for c in s:
            if wlen(out + c) > n: break
            out += c
        return out
    def pad(s, n):
        return s + " " * max(0, n - wlen(s))
    NAME, AUTH = 30, 28
    print(f"\n{pad('曲名', NAME)}{pad('作曲', AUTH)}{'谱面数量':>8}")
    print("-" * (NAME + AUTH + 10))
    for g in groups:
        print(f"{pad(clip(g['title'], NAME - 2), NAME)}{pad(clip(g['author'], AUTH - 2), AUTH)}"
              f"{g['count']:>10}")
    print("-" * (NAME + AUTH + 10))
    print(f"合计 {len(groups)} 首曲目，{len(rows)} 张谱面，音符 {sum(r['notes'] for r in rows)} 个")
    if errs:
        print(f"\n解码失败 {len(errs)} 个（前 10 条）:")
        for bn, e in errs[:10]: print(f"  {bn}: {e}")
    return 0

def export_map_csv(path, map_name, d):
    """单张谱面 -> CSV（含元数据头 + 逐音符时间线）"""
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(f"# map={map_name},bpm={d['bpm']},difficulty={d['difficulty']},"
                f"music={d['music']},scene={d['scene']},md5={d['md5']},notes={d['note_count']}\n")
        f.write("index,time_sec,time_ms,obj_id\n")
        for i, n in enumerate(d["notes"]):
            f.write(f"{i},{n['tick_sec']:.3f},{int(round(n['tick_sec']*1000))},{n['obj_id']}\n")

def cmd_export(bundle_dir, out_dir, limit=0, dic=None):
    if not os.path.isdir(bundle_dir):
        print(f"[错误] 目录不存在: {bundle_dir}"); return 1
    if dic is None: dic = {}
    res, errs = collect_maps(bundle_dir, limit)
    rows = meta_rows(res)
    if not rows:
        print("未发现任何谱面，未导出。"); return 1
    os.makedirs(out_dir, exist_ok=True)
    for bn, maps in res:
        sub = os.path.join(out_dir, os.path.splitext(bn)[0])
        os.makedirs(sub, exist_ok=True)
        for name, d in sorted(maps.items()):
            export_map_csv(os.path.join(sub, f"{name}.csv"), name, d)
    idx = os.path.join(out_dir, "_index.csv")
    with open(idx, "w", encoding="utf-8-sig", newline="") as f:
        w = __import__("csv").writer(f)
        w.writerow(["曲名", "作曲", "bundle", "map", "difficulty", "bpm", "note_count"])
        for r in rows:
            title, author = display_name(r["music"], dic, r["map"])
            w.writerow([title or r["map"], author, r["bundle"], r["map"],
                        r["difficulty"], r["bpm"], r["notes"]])
    print(f"\n已导出 {len(rows)} 张谱面到: {out_dir}")
    print(f"  按 bundle 分子目录，每张谱面一个 CSV；汇总索引: {idx}")
    if errs:
        print(f"  解码失败 {len(errs)} 个 bundle（前 10 条）:")
        for bn, e in errs[:10]: print(f"    {bn}: {e}")
    return 0

def usage():
    print(__doc__)
    print("示例:")
    print("  python map_decoder.py selftest")
    print("  python map_decoder.py list")
    print("  python map_decoder.py list --limit 20")
    print("  python map_decoder.py export")
    print("  python map_decoder.py export <bundle目录> <输出目录> --limit 50")

if __name__ == "__main__":
    import sys
    argv = sys.argv[1:]
    flag_limit, flag_dict, no_dict = 0, DICT_PATH, False
    for fl in ("--limit", "--dict"):
        if fl in argv:
            k = argv.index(fl)
            try:
                if fl == "--limit": flag_limit = int(argv[k + 1])
                else: flag_dict = argv[k + 1]
            except Exception:
                print(f"[错误] {fl} 缺少参数"); sys.exit(1)
            del argv[k:k + 2]
    if "--no-dict" in argv:
        no_dict = True; argv.remove("--no-dict")
    cmd = argv[0] if argv else ""

    if cmd == "selftest":
        b = os.path.join(_paths.bundle_dir(), "music_cthugha_assets_all.bundle")
        r = decode_bundle(b)
        for k, v in r.items():
            print(f"{k}: notes={v['note_count']} bpm={v['bpm']} diff={v['difficulty']} md5={v['md5'][:8]}")
        # 对 map2 与 892 基准做多重集合验证：取 maps 目录下最新的 Cthugha CSV
        cands = sorted(glob.glob(os.path.join(_paths.maps_dir(), "*Cthugha*_notes.csv")),
                       key=os.path.getmtime, reverse=True)
        if not cands:
            print("[错误] 未在 maps 目录找到 Cthugha 基准 CSV，跳过对比")
            sys.exit(1)
        CSV = cands[0]
        print("基准 CSV:", CSV)
        ms = sorted(int(round(x["tick_sec"] * 1000)) for x in r.get("cthugha_map2", {}).get("notes", []))
        csv = sorted(int(float(l.split(",")[2])) for l in open(CSV, encoding="utf-8").readlines()[5:] if l.strip())
        print("map2 vs CSV(892) identical:", ms == csv)
        sys.exit(0)
    elif cmd in ("list", "export"):
        d = argv[1] if len(argv) > 1 and argv[1].strip() else _paths.bundle_dir()
        dic = {}
        if not no_dict:
            dic, n_off, n_fd = load_dict(d, flag_dict)
            print(f"[词典] 官方配置表 {n_off} 条 + 兜底词典 {n_fd} 条 -> 生效 {len(dic)} 条")
        if cmd == "list":
            sys.exit(cmd_list(d, flag_limit, dic))
        o = argv[2] if len(argv) > 2 else os.path.join(SCRIPT_DIR, "decoded_maps")
        sys.exit(cmd_export(d, o, flag_limit, dic))
    else:
        usage(); sys.exit(0)
