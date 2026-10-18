/* 移植自 MuseDashChart.Core：BmsTables / BmsTime / BmsReader / ApproachSpeed。改动 C# 侧时要同步这里。 */
(function (MDP) {
  "use strict";

  const F = { Ground: 1, Air: 2, Event: 4, Scene: 8, Blood: 16, TapHolds: 32, BpmDirect: 64, BpmLookup: 128, Timesig: 256, Scroll: 512, Speed: 1024 };

  const CHANNELS = {
    "01": F.Scene, "02": F.Timesig, "03": F.BpmDirect, "08": F.BpmLookup,
    "15": F.Event, "16": F.Event, "18": F.Event,
    "11": F.Air | F.Event, "13": F.Air | F.Event,
    "31": F.Air | F.Event | F.Blood, "33": F.Air | F.Event | F.Blood,
    "51": F.Air | F.Event, "53": F.Air | F.Event,
    "D1": F.Air | F.Event | F.TapHolds, "D3": F.Air | F.Event | F.TapHolds,
    "12": F.Ground | F.Event, "14": F.Ground | F.Event,
    "32": F.Ground | F.Event | F.Blood, "34": F.Ground | F.Event | F.Blood,
    "52": F.Ground | F.Event, "54": F.Ground | F.Event,
    "D2": F.Ground | F.Event | F.TapHolds, "D4": F.Ground | F.Event | F.TapHolds,
    "SC": F.Scroll, "0S": F.Speed
  };

  const APPROACH_CHANNEL = "0S";
  const TAP_HOLD_LENGTH = 0.001;

  const channelType = (ch) => CHANNELS[(ch || "").toUpperCase()];
  const isHoldKey = (id) => id === "0F" || id === "0G" || id === "16" || id === "17";
  const isGear = (id) => id === "0H" || id === "18";

  const P0 = new Set(["01", "02", "03", "04", "05", "06", "07", "08", "09", "0A", "0B", "0C", "0D", "0E",
    "0F", "0G", "0H", "10", "20", "21", "22", "23", "24"]);

  function isLaneNote(id) {
    if (P0.has(id)) return true;
    if (id === "0I" || id === "0J") return true;
    if (id.length !== 2) return false;
    if (id[0] === "1" && id[1] >= "1" && id[1] <= "8") return true;
    return id[0] === "3";
  }

  const isApproachId = (id) => id === "0X" || id === "0Y" || id === "0Z";
  // 词典 §2.2 / BmsLabels：切场景 ibms → StageInfo.scene，不是 1O=scene_01、1Q=scene_03 连号。
  const SCENE_CHANGE = {
    "1O": "scene_01", "1P": "scene_02", "1Q": "scene_05", "1R": "scene_03", "1S": "scene_04",
    "1T": "scene_08", "1U": "scene_07", "1V": "scene_09", "1W": "scene_10",
    "1X": "scene_10", "1Y": "scene_12"
  };
  const isSceneChangeId = (id) => id in SCENE_CHANGE;

  function normScene(s) {
    const m = /(?:scene[_-]?)?(\d{1,2})/i.exec(String(s || "").trim());
    return m ? "scene_" + String(Number(m[1])).padStart(2, "0") : "";
  }

  /** 当前场景是否 1K 单轨判定（跟 1O–1W 走，不看 40–42）。 */
  const isOneKeyScene = (s) => normScene(s) === "scene_11";

  /** 默认场景 + 通道 15 切场景台阶。1X=scene_10，1Y=scene_12。 */
  function sceneTimeline(notes, defaultScene) {
    const def = normScene(defaultScene) || "scene_01";
    const steps = [{ t: -Infinity, scene: def, id: "", unknown: false }];
    const evs = (notes || []).filter((n) => isSceneChangeId(n.bmsId)).sort((a, b) => a.timeSec - b.timeSec || a.id - b.id);
    for (const n of evs) {
      const scene = SCENE_CHANGE[n.bmsId];
      steps.push(scene
        ? { t: n.timeSec, scene, id: n.bmsId, unknown: false }
        : { t: n.timeSec, scene: null, id: n.bmsId, unknown: true });
    }
    return steps;
  }

  function sceneAt(steps, t) {
    let scene = steps && steps[0] ? steps[0].scene : "", unknown = "";
    if (!steps) return { scene, unknown };
    for (const s of steps) {
      if (s.t > t) break;
      if (s.unknown) unknown = s.id;
      else { scene = s.scene; unknown = ""; }
    }
    return { scene, unknown };
  }

  function scenesNeeded(steps) {
    const out = [];
    for (const s of steps || []) {
      if (s.scene && !out.includes(s.scene)) out.push(s.scene);
    }
    return out;
  }
  const SCROLL_IDS = { "0O": [1, "both"], "0P": [2, "both"], "0Q": [3, "both"], "0R": [1, "ground"], "0S": [2, "ground"],
    "0T": [3, "ground"], "0U": [1, "air"], "0V": [2, "air"], "0W": [3, "air"] };
  const isScrollId = (id) => id in SCROLL_IDS;

  function clampRate(n) { return n <= 1 ? 1 : n === 2 ? 2 : 3; }
  function rateOf(code) { const n = parseInt(code, 10); return Number.isFinite(n) ? clampRate(n) : 1; }

  const tryInt = (s) => /^[+-]?\d+$/.test(s.trim()) ? parseInt(s, 10) : null;
  const tryFloat = (s) => { const v = Number(s.trim()); return s.trim() !== "" && Number.isFinite(v) ? v : null; };

  /* ---------- BmsTime（与 CustomAlbums.CalculateNoteTime 对齐，tick 单位为小节） ---------- */
  // C# 全程用 float 运算、MathF.Round 为银行家舍入；这里逐步 fround 以做到逐位一致。
  const f32 = Math.fround;
  const MICRO = f32(1e-6);

  function roundEven(x) {
    const r = Math.round(x);
    return Math.abs(x % 1) === 0.5 && r % 2 !== 0 ? r - 1 : r;
  }

  /** (decimal)float：.NET 只保留 7 位有效数字。 */
  const floatToDecimal = (x) => (x === 0 ? 0 : Number(f32(x).toPrecision(7)));

  const freqOf = (bpm) => f32(f32(60 / f32(bpm)) * 4);
  const tickOf = (i, n, beat) => f32(f32(i / n) + beat);

  function calculateNoteTime(tick, bpmEntries, sigs) {
    tick = f32(tick);
    const rel = bpmEntries.filter((e) => e.tick < tick).sort((a, b) => a.tick - b.tick);
    if (!rel.length) return 0;
    let acc = 0, total = 0;
    for (let j = 0; j < rel.length; j++) {
      const e = rel[j];
      const seg = f32(j < rel.length - 1 ? rel[j + 1].tick - e.tick : tick - e.tick);
      const prev = total;
      total = f32(total + seg);
      const floorBeat = Math.floor(prev), ceilBeat = Math.ceil(total);
      for (let k = floorBeat; k < ceilBeat; k++) {
        let frac = 1;
        if (k === floorBeat) frac = f32(k + 1 - prev);
        if (k === ceilBeat - 1) frac = f32(total - (ceilBeat - 1));
        if (ceilBeat === floorBeat + 1) frac = f32(total - prev);
        const mult = sigs.has(k) ? sigs.get(k) : 1;
        const units = roundEven(f32(f32(f32(frac * mult) * e.freq) / MICRO));
        acc = f32(acc + f32(units * MICRO));
      }
    }
    return acc;
  }

  /* ---------- BmsReader.Load ---------- */
  function parseBms(text) {
    const bpmDict = {};
    const sigs = new Map();
    const bpmEntries = [];
    const bpmChanges = [];
    const raw = [];
    const warnings = [];
    const info = { genre: "scene_01", title: "", artist: "", levelDesign: "", bpm: 0, playLevel: "1" };

    const addBpm = (tick, bpm, channel, key) => {
      bpmEntries.push({ tick, freq: freqOf(bpm) });
      bpmChanges.push({ tick, bpm, channel, key });
    };

    for (let line of text.replace(/^\uFEFF/, "").split(/\r?\n/)) {
      line = line.trim();
      if (!line.startsWith("#")) continue;
      line = line.slice(1);

      const sp = line.indexOf(" ");
      if (sp >= 0) {
        const key = line.slice(0, sp), value = line.slice(sp + 1);
        const up = key.toUpperCase();
        if (up.startsWith("WAV")) continue;
        if (up.startsWith("BPM")) {
          let v = tryFloat(value);
          if (v === null) continue;
          v = f32(v);
          const bk = key.length > 3 ? key.slice(3).toUpperCase() : "00";
          bpmDict[bk] = v;
          if (bk === "" || bk === "00") { info.bpm = v; addBpm(0, v, "", "00"); }
          continue;
        }
        if (up === "GENRE") info.genre = value;
        else if (up === "TITLE") info.title = value;
        else if (up === "ARTIST") info.artist = value;
        else if (up === "LEVELDESIGN") info.levelDesign = value;
        else if (up === "PLAYLEVEL") info.playLevel = value;
        continue;
      }

      const colon = line.indexOf(":");
      if (colon < 5) continue;
      const key = line.slice(0, colon), value = line.slice(colon + 1);
      const beat = tryInt(key.slice(0, 3));
      if (beat === null) continue;
      const code = key.slice(3, 5).toUpperCase();
      const type = channelType(code);

      if (type === F.Timesig) {
        const pct = tryFloat(value);
        if (pct !== null) sigs.set(beat, f32(pct));
        else warnings.push(`拍号无法解析 #${key}:${value}`);
        continue;
      }

      if (value.length % 2 !== 0) { warnings.push(`通道 ${code} 小节 ${beat} 长度为奇数，已跳过`); continue; }
      const n = value.length / 2;

      if (type === F.BpmDirect || type === F.BpmLookup) {
        for (let i = 0; i < n; i++) {
          const v = value.substr(i * 2, 2).toUpperCase();
          if (v === "00") continue;
          const tick = tickOf(i, n, beat);
          let bpm;
          if (type === F.BpmLookup) {
            if (!(v in bpmDict)) { warnings.push(`BPM 查表键 ${v} 未定义（tick ${tick.toFixed(3)}）`); continue; }
            bpm = bpmDict[v];
          } else {
            bpm = parseInt(v, 16);
            if (!Number.isFinite(bpm)) { warnings.push(`BPM 十六进制无法解析 ${v}`); continue; }
          }
          if (bpm > 0) addBpm(tick, bpm, code, v);
        }
        continue;
      }

      if (type === undefined) warnings.push(`未识别通道 ${code}（小节 ${beat}），已保留`);
      for (let i = 0; i < n; i++) {
        const v = value.substr(i * 2, 2).toUpperCase();
        if (v === "00") continue;
        const tick = tickOf(i, n, beat);
        raw.push({ time: calculateNoteTime(tick, bpmEntries, sigs), value: v, channel: code });
      }
    }

    const score = (r) => Math.trunc(f32(r.time * 1e6)) * 10 + (r.channel === "15" ? 0 : 1);
    raw.sort((a, b) => score(a) - score(b));

    return { info, raw, bpmEntries, bpmChanges, sigs, warnings };
  }

  /* ---------- BmsReader.ToDifficulty ---------- */
  function toChart(parsed) {
    const notes = parsed.raw;
    const skip = new Array(notes.length).fill(false);
    const lengths = new Array(notes.length).fill(0);
    const warnings = parsed.warnings.slice();

    const markers = notes
      .filter((r) => r.channel === APPROACH_CHANNEL)
      .map((r) => ({ time: round6(floatToDecimal(r.time)), rate: rateOf(r.value) }))
      .sort((a, b) => a.time - b.time);

    const groups = new Map();
    notes.forEach((r, i) => {
      if (!isHoldKey(r.value)) return;
      const t = channelType(r.channel) || 0;
      if (t & F.TapHolds) return;
      const ch = String(r.channel || "").toUpperCase();
      if (ch !== "53" && ch !== "54") return;
      const k = r.channel + "|" + r.value;
      if (!groups.has(k)) groups.set(k, []);
      groups.get(k).push(i);
    });
    for (const [k, list] of groups) {
      list.sort((a, b) => notes[a].time - notes[b].time || a - b);
      for (let g = 0; g + 1 < list.length; g += 2) {
        lengths[list[g]] = floatToDecimal(notes[list[g + 1]].time) - floatToDecimal(notes[list[g]].time);
        skip[list[g + 1]] = true;
      }
      if (list.length % 2 === 1) {
        const [value, channel] = [k.split("|")[1], k.split("|")[0]];
        warnings.push(`未配对长按 ${value} 通道 ${channel} t=${notes[list[list.length - 1]].time.toFixed(3)}`);
      }
    }

    const out = [];
    let id = 1;
    notes.forEach((r, i) => {
      if (skip[i] || !r.value || r.channel === APPROACH_CHANNEL) return;
      const t = channelType(r.channel) || 0;
      const pathway = t & F.Air ? 1 : 0;
      const eventOnly = (t & F.Scene) || ((t & F.Event) && !(t & F.Ground) && !(t & F.Air));
      let length = lengths[i];
      if ((t & F.TapHolds) && isHoldKey(r.value)) length = TAP_HOLD_LENGTH;
      const ch = String(r.channel || "").toUpperCase();
      if (r.value === "0F" && ch !== "53" && ch !== "54" && !(length > TAP_HOLD_LENGTH)) return;
      out.push({
        id: id++,
        timeSec: round6(floatToDecimal(r.time)),
        lengthSec: round6(length),
        pathway,
        bmsId: r.value,
        blood: !!(t & F.Blood) && !isHoldKey(r.value),
        kind: eventOnly || !isLaneNote(r.value) ? "event" : "note",
        channel: r.channel,
        approachRate: 1,
        approachOverride: false
      });
    });

    for (const n of out) {
      if (isPlayable(n)) n.approachRate = rateAt(markers, n.timeSec);
    }

    return { notes: out, markers, bpmEntries: parsed.bpmEntries, bpmChanges: parsed.bpmChanges, sigs: parsed.sigs, info: parsed.info, warnings };
  }

  function round6(v) { return Math.round(v * 1e6) / 1e6; }

  function rateAt(markers, t) {
    let r = 1;
    for (const m of markers) { if (m.time <= t) r = clampRate(m.rate); else break; }
    return r;
  }

  function drawsOnPathway(n) {
    if (isApproachId(n.bmsId) || isScrollId(n.bmsId) || n.bmsId === "2E" || n.bmsId === "2F") return false;
    if (n.kind !== "event") return true;
    const t = channelType(n.channel) || 0;
    return isLaneNote(n.bmsId) && !!(t & (F.Air | F.Ground));
  }
  const isPlayable = (n) => !isApproachId(n.bmsId) && drawsOnPathway(n);

  /** ApproachSpeed.ApplyOverrides：有 BPM 时按同一 192 槽 + 轨道 + ibms 贴回。 */
  function slotIndex(t, bpm) {
    const meas = 240 / Math.max(Number(bpm) || 120, 1);
    if (!(t > 0)) return 0;
    const tick = t / meas;
    let measure = Math.floor(tick);
    let slot = Math.round((tick - measure) * 192);
    if (slot >= 192) { measure++; slot = 0; }
    return measure * 192 + slot;
  }

  function applyOverrides(chart, storedNotes) {
    const unused = (storedNotes || [])
      .filter((s) => s && s.approachOverride && isPlayable(normStored(s)))
      .map(normStored)
      .sort((a, b) => a.timeSec - b.timeSec);
    if (!unused.length) return 0;
    const bpm = Number((chart.info && chart.info.bpm) || 0);
    let applied = 0;
    const targets = chart.notes.filter(isPlayable).sort((a, b) => a.timeSec - b.timeSec || a.id - b.id);
    for (const n of targets) {
      let best = -1, bestDt = 0.02;
      for (let i = 0; i < unused.length; i++) {
        const s = unused[i];
        if (s.pathway !== n.pathway || s.bmsId !== n.bmsId) continue;
        if (bpm > 0) {
          if (slotIndex(s.timeSec, bpm) !== slotIndex(n.timeSec, bpm)) continue;
          best = i;
          break;
        }
        const dt = Math.abs(s.timeSec - n.timeSec);
        if (dt > bestDt) continue;
        bestDt = dt;
        best = i;
      }
      if (best < 0) continue;
      n.approachRate = clampRate(unused[best].approachRate || 1);
      n.approachOverride = true;
      unused.splice(best, 1);
      applied++;
    }
    return applied;
  }

  function normStored(s) {
    return {
      timeSec: Number(s.timeSec) || 0,
      pathway: Number(s.pathway) || 0,
      bmsId: String(s.bmsId || "").toUpperCase(),
      kind: s.kind || "note",
      channel: String(s.channel || "").toUpperCase(),
      approachRate: Number(s.approachRate) || 1,
      approachOverride: !!s.approachOverride
    };
  }

  /** 物件外观分类，渲染与以后判定共用。 */
  function categoryOf(n) {
    const id = n.bmsId;
    if (isGear(id)) return "gear";
    if (id === "0G") return "masher";
    if (isHoldKey(id) && n.lengthSec > TAP_HOLD_LENGTH) return "hold";
    if (id === "10" || id === "23") return "note";
    if (id === "24") return "holdstar";
    if (id === "22") return "heart";
    if (id === "21") return "ghost";
    if (id === "0I" || id === "0J" || id[0] === "3" || (id[0] === "1" && id[1] >= "1" && id[1] <= "8")) return "boss";
    return "monster";
  }

  /** 仅影响外观的子类：摆锤 0D / 倒置 0J、突击者 0C / 倒置 0I；不改变 categoryOf（连击规则依赖它）。 */
  const SPECIAL = { "0D": ["pendulum", false], "0J": ["pendulum", true], "0C": ["raider", false], "0I": ["raider", true] };
  function specialOf(n) {
    const s = SPECIAL[n.bmsId];
    return s ? { special: s[0], inverted: s[1] } : { special: null, inverted: false };
  }

  MDP.bms = {
    parseBms, toChart, applyOverrides, calculateNoteTime, categoryOf, specialOf,
    isPlayable, isScrollId, scrollInfo: (id) => SCROLL_IDS[id], rateAt,
    SCENE_CHANGE, isSceneChangeId, normScene, isOneKeyScene, sceneTimeline, sceneAt, scenesNeeded
  };
})(window.MDP = window.MDP || {});
