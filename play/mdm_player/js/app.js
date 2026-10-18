/* 界面胶水：加载 MDM、切难度、播放控制、打击音排程、键位可视化。 */
(function (MDP) {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const bms = MDP.bms;
  const clock = new MDP.AudioClock();
  const renderer = new MDP.Renderer($("cv"));

  const SFX_AHEAD = 0.15;
  const KEYS = { KeyD: "air", KeyF: "air", KeyJ: "ground", KeyK: "ground" };

  const saved = JSON.parse(localStorage.getItem("mdmPlayer") || "{}");
  const settings = {
    speed: saved.speed || 1,
    offsetMs: saved.offsetMs || 0,
    sfx: saved.sfx !== false,
    showIds: !!saved.showIds,
    rate: saved.rate || 1,
    mdSprites: saved.mdSprites !== false,
    mdAnims: saved.mdAnims !== false
  };
  const persist = () => localStorage.setItem("mdmPlayer", JSON.stringify(settings));
  const query = new URLSearchParams(location.search);

  let song = null;
  let diff = 0;
  let view = null;
  let sfxFrom = 0;
  let scheduled = [];
  const pressed = { air: false, ground: false };

  /* ---------- 谱面准备 ---------- */
  /** 画面流速 0O–0W 的档位 → 时长除数，按普通键判定帧折算（1 : 44/38 : 44/33）。 */
  function scrollMulFn(chart) {
    const evs = chart.notes.filter((n) => bms.isScrollId(n.bmsId)).sort((a, b) => a.timeSec - b.timeSec || a.id - b.id);
    return (n) => {
      let tier = 1;
      for (const e of evs) {
        if (e.timeSec > n.timeSec) break;
        const [t, scope] = bms.scrollInfo(e.bmsId);
        if (scope === "both" || (scope === "air") === (n.pathway === 1)) tier = t;
      }
      return MDP.normalFlight(1) / MDP.normalFlight(tier);
    };
  }

  function prepare(chart) {
    const scrollMul = scrollMulFn(chart);
    const notes = chart.notes
      .filter(bms.isPlayable)
      .map((n) => {
        const o = { ...n, ...bms.specialOf(n), cat: bms.categoryOf(n) };
        Object.assign(o, MDP.lookOf(o), MDP.flightOf(o));
        o.dur = o.flight / scrollMul(o);
        return o;
      })
      .sort((a, b) => a.timeSec - b.timeSec || a.id - b.id);

    const lastEnd = notes.reduce((m, n) => Math.max(m, n.timeSec + n.lengthSec), 0);
    const maxLen = notes.reduce((m, n) => Math.max(m, n.lengthSec), 0);
    const lead = notes.reduce((m, n) => Math.max(m, n.dur), MDP.normalFlight(1));

    const measures = [];
    const timeOf = (tick) => bms.calculateNoteTime(tick, chart.bpmEntries, chart.sigs);
    const until = Math.max(lastEnd, clock.buffer ? clock.buffer.duration : 0) + 2;
    for (let bar = 0; bar < 4000; bar++) {
      const t = timeOf(bar);
      if (t > until) break;
      for (let q = 0; q < 4; q++) measures.push({ t: q === 0 ? t : timeOf(bar + q / 4), major: q === 0 });
    }
    for (const m of measures) m.dur = MDP.normalFlight(bms.rateAt(chart.markers, m.t));

    const bpmTimes = chart.bpmChanges
      .map((c) => ({ t: timeOf(c.tick), bpm: c.bpm }))
      .sort((a, b) => a.t - b.t);

    const count = {};
    for (const n of notes) count[n.cat] = (count[n.cat] || 0) + 1;

    const comboTimes = MDP.combo.buildTimes(notes);
    const defaultScene = songScene();
    const forced = query.get("animScene");
    const sceneSteps = forced
      ? [{ t: -Infinity, scene: bms.normScene(forced) || forced, id: "", unknown: false }]
      : bms.sceneTimeline(chart.notes, defaultScene);

    return { chart, notes, lastEnd, maxLen, lead, measures, bpmTimes, count, comboTimes, sceneSteps,
      sceneAt: (t) => bms.sceneAt(sceneSteps, t) };
  }

  function songScene() {
    const first = song && song.charts[Object.keys(song.charts)[0]];
    return bms.normScene((song && song.info.scene) || (first && first.info.genre) || "");
  }

  /* ---------- 加载 ---------- */
  async function openFile(file) {
    if (!file) return;
    try {
      toast("");
      clock.stop();
      const loaded = await MDP.loadMdm(file);
      if (song && song.coverUrl) URL.revokeObjectURL(song.coverUrl);
      song = loaded;
      try { await clock.load(song.music && song.music.bytes); }
      catch (e) { toast(`音频解码失败（${song.music.name}），将静音预览：${e.message}`); await clock.load(null); }

      $("bg").style.backgroundImage = song.coverUrl ? `url("${song.coverUrl}")` : "";
      const name = song.info.name || song.fileName;
      const by = [song.info.author, song.info.leveldesigner && "谱师 " + song.info.leveldesigner].filter(Boolean).join(" · ");
      $("title").innerHTML = "";
      $("title").append(document.createTextNode(name));
      const small = document.createElement("small");
      small.textContent = by;
      $("title").append(small);

      buildDiffButtons();
      ensureAnimPacks();
      ensureSpritePacks();
      const first = Number(Object.keys(song.charts).sort((a, b) => b - a)[0]);
      selectDiff(first);
      $("drop").classList.add("hidden");
    } catch (e) {
      console.error(e);
      toast("打开失败：" + e.message);
    }
  }

  function buildDiffButtons() {
    const box = $("diffs");
    box.innerHTML = "";
    for (const d of Object.keys(song.charts).map(Number).sort()) {
      const b = document.createElement("button");
      b.textContent = `map${d} ★${song.charts[d].level}`;
      b.dataset.d = d;
      b.onclick = () => selectDiff(d);
      box.append(b);
    }
  }

  function selectDiff(d) {
    if (!song || !song.charts[d]) return;
    diff = d;
    view = prepare(song.charts[d]);
    clock.fallbackDuration = view.lastEnd + 2;
    for (const b of $("diffs").children) b.classList.toggle("on", Number(b.dataset.d) === d);
    renderWarnings();
    renderMarks();
    resetSfx();
  }

  function renderWarnings() {
    const list = view.chart.warnings;
    $("warn").querySelector("summary").textContent = `警告 ${list.length}`;
    const ul = $("warnlist");
    ul.innerHTML = "";
    for (const w of list.length ? list : ["无"]) {
      const li = document.createElement("li");
      li.textContent = w;
      ul.append(li);
    }
  }

  function renderMarks() {
    const box = $("marks");
    box.innerHTML = "";
    const dur = clock.duration || 1;
    const colors = { 2: "#ffca28", 3: "#ff5252" };
    for (const m of view.chart.markers) {
      if (m.rate === 1) continue;
      const i = document.createElement("i");
      i.style.left = (m.time / dur * 100) + "%";
      i.style.background = colors[m.rate];
      i.title = `飞近档 ${m.rate} @ ${m.time.toFixed(2)}s`;
      box.append(i);
    }
  }

  /* ---------- 打击音 ---------- */
  const offsetSec = () => settings.offsetMs / 1000;
  const chartNow = () => clock.time - offsetSec();

  function resetSfx() {
    for (const o of scheduled) { try { o.stop(); } catch (_) { /* 已结束 */ } }
    scheduled = [];
    sfxFrom = chartNow();
  }

  function scheduleSfx() {
    if (!settings.sfx || !clock.playing || !view) return;
    const until = chartNow() + SFX_AHEAD * clock.rate;
    const notes = view.notes;
    const ctxNow = clock.ctx.currentTime;
    for (const n of notes) {
      if (n.timeSec < sfxFrom) continue;
      if (n.timeSec >= until) break;
      if (n.cat === "gear") continue;
      const when = Math.max(ctxNow, clock.ctxTimeOf(n.timeSec + offsetSec()));
      const soft = n.cat === "note" || n.cat === "heart";
      scheduled.push(clock.blip(when, soft ? 1320 : n.pathway === 1 ? 880 : 520, 0.045, soft ? 0.18 : 0.45));
    }
    sfxFrom = until;
    if (scheduled.length > 64) scheduled = scheduled.slice(-32);
  }

  /* ---------- 主循环 ---------- */
  function frame() {
    clock.tick();
    if (view) {
      scheduleSfx();
      const now = chartNow();
      const combo = MDP.combo.at(view.comboTimes, now);
      renderer.draw({
        combo,
        comboAge: combo ? now - view.comboTimes[combo - 1] : Infinity,
        notes: view.notes, maxLen: view.maxLen, measures: view.measures, now,
        lead: view.lead, speed: settings.speed, showIds: settings.showIds, pressed
      });
      drawHud(now);
    }
    const dur = clock.duration || 0;
    $("fill").style.width = (dur ? clock.time / dur * 100 : 0) + "%";
    $("time").textContent = `${clock.time.toFixed(1)} / ${dur.toFixed(1)}`;
    requestAnimationFrame(frame);
  }

  function drawHud(now) {
    let bpm = view.chart.info.bpm;
    for (const b of view.bpmTimes) { if (b.t <= now) bpm = b.bpm; else break; }
    const rate = bms.rateAt(view.chart.markers, now);
    const c = view.count;
    const playableCount = view.notes.filter((n) => n.cat !== "gear" && n.cat !== "note" && n.cat !== "heart").length;
    const lines = [
      `map${diff}  ★${view.chart.level}   BPM ${+bpm.toFixed(2)}   飞近档 ${rate}   最大连击 ${view.comboTimes.length}`,
      `可击打 ${playableCount}  长按 ${c.hold || 0}  连打 ${c.masher || 0}  齿轮 ${c.gear || 0}  音符 ${c.note || 0}  红心 ${c.heart || 0}  Boss ${c.boss || 0}`,
      song.music ? `音频 ${song.music.name}` : "无音频（静音预览）"
    ];
    if (view.chart.overrides) lines.push(`chart.json 覆盖档 ${view.chart.overrides} 颗`);
    if (song.info.delay && song.info.delay !== "0") lines.push(`info.json delay = ${song.info.delay}`);
    if (settings.mdSprites && !$("mdwrap").hidden) {
      const cur = view.sceneAt(now);
      const has = cur.scene && renderer.spritePacks[cur.scene];
      lines.push("游戏贴图 " + (cur.scene || "?") + (has ? "" : "（无包，回退）"));
    }
    if (settings.mdAnims && !$("animwrap").hidden) {
      const cur = view.sceneAt(now);
      const has = cur.scene && renderer.packs[cur.scene];
      const extra = [];
      if (query.get("animScene")) extra.push("?animScene= 强制");
      if (cur.unknown) extra.push("未知切场景 " + cur.unknown + "，未换包");
      if (!has) extra.push(cur.scene ? "无动画包，矢量/贴图" : "无场景");
      if (anim.state === "loading") extra.push("加载中…");
      if (anim.state === "fail") extra.push("加载失败");
      lines.push("游戏动画 " + (cur.scene || "?") + (extra.length ? "（" + extra.join("；") + "）" : ""));
    }
    $("hud").textContent = lines.join("\n");
  }

  function toast(msg) {
    const t = $("toast");
    t.textContent = msg;
    t.style.display = msg ? "block" : "none";
  }

  /* ---------- 控制 ---------- */
  async function toggle() {
    if (!view) return;
    if (clock.playing) clock.pause(); else await clock.play();
    resetSfx();
    syncPlayButton();
  }
  function syncPlayButton() { $("play").textContent = clock.playing ? "⏸" : "▶"; }
  clock.onEnded = syncPlayButton;

  function seek(t) { clock.seek(t); resetSfx(); }

  function setSpeed(v) {
    settings.speed = Math.round(Math.min(4, Math.max(0.5, v)) * 10) / 10;
    $("speedv").textContent = settings.speed.toFixed(1);
    persist();
  }

  $("open").onclick = () => $("file").click();
  $("file").onchange = (e) => { openFile(e.target.files[0]); e.target.value = ""; };
  $("drop").onclick = () => $("file").click();
  $("play").onclick = toggle;
  $("back").onclick = () => seek(0);
  $("slow").onclick = () => setSpeed(settings.speed - 0.1);
  $("fast").onclick = () => setSpeed(settings.speed + 0.1);

  $("rate").value = String(settings.rate);
  clock.rate = settings.rate;
  $("rate").onchange = (e) => { settings.rate = Number(e.target.value); clock.setRate(settings.rate); resetSfx(); persist(); };

  $("offset").value = settings.offsetMs;
  $("offset").oninput = (e) => { settings.offsetMs = Number(e.target.value) || 0; resetSfx(); persist(); };

  $("sfx").checked = settings.sfx;
  $("sfx").onchange = (e) => { settings.sfx = e.target.checked; resetSfx(); persist(); };
  $("ids").checked = settings.showIds;
  $("ids").onchange = (e) => { settings.showIds = e.target.checked; persist(); };
  // 可选的本地游戏素材：sprites/index.js 按场景列出包；没有 index 时退回根目录 manifest.js（旧的单场景导出）。
  const MD_BASE = "assets/md/";
  const SPRITE_BASE = MD_BASE + "sprites/";
  $("md").checked = settings.mdSprites;
  renderer.useSprites = settings.mdSprites;
  $("md").onchange = (e) => { settings.mdSprites = renderer.useSprites = e.target.checked; persist(); };
  setSpeed(settings.speed);

  function ensureSpritePacks() {
    const index = window.MD_SPRITE_INDEX;
    if (!song || !index) return;
    const want = neededScenes();
    const toLoad = want.filter((k) => index[k] && !renderer.spritePacks[k]);
    if (!toLoad.length) {
      if (Object.keys(renderer.spritePacks).length) $("mdwrap").hidden = false;
      return;
    }
    let left = toLoad.length;
    const doneOne = () => {
      if (--left) return;
      $("mdwrap").hidden = !Object.keys(renderer.spritePacks).length && !renderer.sprites;
    };
    for (const key of toLoad) {
      const apply = () => {
        const pack = (window.MD_SPRITE_PACKS || {})[key];
        renderer.addSpritePack(key, pack && pack.sprites, SPRITE_BASE + key + "/", doneOne);
      };
      if (window.MD_SPRITE_PACKS && window.MD_SPRITE_PACKS[key]) apply();
      else loadScript(SPRITE_BASE + index[key], apply, doneOne);
    }
  }

  loadScript(SPRITE_BASE + "index.js", () => {
    if (!window.MD_SPRITE_INDEX || !Object.keys(window.MD_SPRITE_INDEX).length) return;
    $("mdwrap").hidden = false;
    ensureSpritePacks();
  }, () => {
    const mdScript = document.createElement("script");
    mdScript.src = MD_BASE + "manifest.js";
    mdScript.onload = () => {
      const m = MDP.mdAssets;
      if (m) renderer.loadSprites(m.sprites, MD_BASE, (ok) => { $("mdwrap").hidden = !ok; });
    };
    mdScript.onerror = () => mdScript.remove();
    document.head.append(mdScript);
  });

  // 可选的逐帧动画包（scripts/pack_md_anims.py 生成）：anims/index.js 列出已有场景，按谱面场景按需插入 <script>。
  const ANIM_BASE = MD_BASE + "anims/";
  const anim = { state: "" };
  if (query.has("animAir")) renderer.animAirLift = Number(query.get("animAir"));
  $("anim").checked = settings.mdAnims;
  renderer.useAnims = false;
  $("anim").onchange = (e) => {
    settings.mdAnims = e.target.checked;
    renderer.useAnims = settings.mdAnims && Object.keys(renderer.packs).length > 0;
    persist();
  };

  function loadScript(src, onload, onerror) {
    const s = document.createElement("script");
    s.src = src;
    s.onload = onload;
    s.onerror = () => { s.remove(); onerror(); };
    document.head.append(s);
  }

  function neededScenes() {
    if (!song) return [];
    const forced = query.get("animScene");
    if (forced) return [bms.normScene(forced) || forced];
    const out = [];
    for (const c of Object.values(song.charts)) {
      for (const s of bms.scenesNeeded(bms.sceneTimeline(c.notes, songScene()))) {
        if (!out.includes(s)) out.push(s);
      }
    }
    return out;
  }

  function syncAnimFlag() {
    renderer.useAnims = settings.mdAnims && Object.keys(renderer.packs).length > 0;
  }

  function ensureAnimPacks() {
    const index = window.MD_ANIM_INDEX;
    if (!song || !index) return;
    const keys = Object.keys(index);
    if (!keys.length) return;
    const want = neededScenes();
    const toLoad = want.filter((k) => index[k] && !renderer.packs[k]);
    if (!toLoad.length) {
      anim.state = Object.keys(renderer.packs).length ? "ok" : "fail";
      syncAnimFlag();
      return;
    }
    anim.state = "loading";
    renderer.useAnims = false;
    let left = toLoad.length;
    const doneOne = () => {
      if (--left) return;
      anim.state = Object.keys(renderer.packs).length ? "ok" : "fail";
      syncAnimFlag();
    };
    for (const key of toLoad) {
      const apply = () => renderer.addAnimPack(key, (window.MD_ANIM_PACKS || {})[key], ANIM_BASE + key + "/", doneOne);
      if (window.MD_ANIM_PACKS && window.MD_ANIM_PACKS[key]) apply();
      else loadScript(ANIM_BASE + index[key], apply, doneOne);
    }
  }

  loadScript(ANIM_BASE + "index.js", () => {
    if (!window.MD_ANIM_INDEX || !Object.keys(window.MD_ANIM_INDEX).length) return;
    $("animwrap").hidden = false;
    ensureAnimPacks();
  }, () => {});

  let dragging = false;
  const seekTo = (e) => {
    const r = $("seek").getBoundingClientRect();
    seek(Math.min(1, Math.max(0, (e.clientX - r.left) / r.width)) * (clock.duration || 0));
  };
  $("seek").onpointerdown = (e) => { dragging = true; $("seek").setPointerCapture(e.pointerId); seekTo(e); };
  $("seek").onpointermove = (e) => { if (dragging) seekTo(e); };
  $("seek").onpointerup = () => { dragging = false; };

  addEventListener("keydown", (e) => {
    if (e.target.tagName === "INPUT" && e.target.type === "number") return;
    if (KEYS[e.code]) { pressed[KEYS[e.code]] = true; return; }
    switch (e.code) {
      case "Space": e.preventDefault(); toggle(); break;
      case "ArrowLeft": seek(clock.time - (e.shiftKey ? 10 : 2)); break;
      case "ArrowRight": seek(clock.time + (e.shiftKey ? 10 : 2)); break;
      case "Home": seek(0); break;
      case "BracketLeft": setSpeed(settings.speed - 0.1); break;
      case "BracketRight": setSpeed(settings.speed + 0.1); break;
      case "KeyH": $("sfx").click(); break;
      case "KeyI": $("ids").click(); break;
      case "KeyG": if (!$("mdwrap").hidden) $("md").click(); break;
      case "KeyA": if (!$("animwrap").hidden) $("anim").click(); break;
      default:
        if (/^Digit[1-5]$/.test(e.code)) selectDiff(Number(e.code.slice(5)));
    }
  });
  addEventListener("keyup", (e) => { if (KEYS[e.code]) pressed[KEYS[e.code]] = false; });

  addEventListener("dragover", (e) => { e.preventDefault(); document.body.classList.add("dragging"); });
  addEventListener("dragleave", (e) => { if (!e.relatedTarget) document.body.classList.remove("dragging"); });
  addEventListener("drop", (e) => {
    e.preventDefault();
    document.body.classList.remove("dragging");
    openFile(e.dataTransfer.files[0]);
  });

  const src = new URLSearchParams(location.search).get("src");
  if (src) {
    fetch(src)
      .then((r) => { if (!r.ok) throw new Error(`HTTP ${r.status}`); return r.blob(); })
      .then((b) => openFile(new File([b], decodeURIComponent(src.split("/").pop()))))
      .catch((e) => toast(`无法加载 ${src}：${e.message}（file:// 下请用拖入方式）`));
  }

  MDP.app = { openFile, selectDiff, get view() { return view; }, get song() { return song; }, clock, anim, renderer };
  requestAnimationFrame(frame);
})(window.MDP = window.MDP || {});
