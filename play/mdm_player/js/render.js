/* 两轨横向卷轴：空中轨在上（pathway=1），地面轨在下（pathway=0），物件从右往左飞向判定点。 */
(function (MDP) {
  "use strict";

  /* ---------- 游戏几何（MDSceneGrabber 遥测，见 规则解析/mdm物件转化词典.md §1.2–1.3） ---------- */
  // 正交相机 size 3.75 → 1 单位 = 舞台高 / 7.5；舞台按 16:9 适配进画布，Spine 原点在画面中心。
  // 物件 x = +10 生成、-3.85 判定，行程 13.85 单位；轨道中心：空中 -1.125、地面 +1.0（屏幕 y 向下）。
  const STAGE_U = 7.5, STAGE_ASPECT = 16 / 9;
  const JUDGE_U = -3.85, SPAWN_U = 10, TRAVEL_U = SPAWN_U - JUDGE_U;
  const AIR_U = -1.125, GROUND_U = 1.0;
  // 0I / 0J 实测是对侧轨 0C / 0D 的上下镜像，镜像轴随目标轨：地面 0.478H、空中 0.487H。
  const AXIS_GROUND_U = (0.478 - 0.5) * STAGE_U, AXIS_AIR_U = (0.487 - 0.5) * STAGE_U;

  // 出场时长 = 判定帧 / 30 s（档 1 / 2 / 3）。
  const FLIGHT = {
    normal: [44, 38, 33], raider: [39, 30, 24], pendulum: [26, 21, 18], updown: [30, 26, 23], boss13: [21, 18, 15]
  };
  const UPDOWN = { "02": "rise", "05": "rise", "08": "rise", "03": "drop", "06": "drop", "09": "drop" };
  const flightSec = (f, rate) => f[Math.min(3, Math.max(1, rate || 1)) - 1] / 30;
  /** 档位 rate 下普通键的出场时长（小节线、流速档折算用）。 */
  const normalFlight = (rate) => flightSec(FLIGHT.normal, rate);

  /** 飞行时长与轨迹类型，prepare 时每颗算一次。需要 cat / special / bmsId / approachRate。 */
  function flightOf(n) {
    const r = n.approachRate;
    if (n.special === "raider") return { flight: flightSec(FLIGHT.raider, r), path: "raider" };
    if (n.special === "pendulum") return { flight: flightSec(FLIGHT.pendulum, r), path: "pendulum" };
    if (n.cat === "monster" && UPDOWN[n.bmsId]) return { flight: flightSec(FLIGHT.updown, r), path: UPDOWN[n.bmsId] };
    if (n.bmsId === "13") return { flight: flightSec(FLIGHT.boss13, r), path: null };
    return { flight: flightSec(FLIGHT.normal, r), path: null };
  }

  // 突击者：进度 0.38 前贴底边（0.98H）跑，之后跳进轨道；地面轨约 0.8 处过冲到轨道上方 0.1H。
  const RAIDER_JUMP = 0.38, RAIDER_PEAK = 0.8, RAIDER_EDGE_U = (0.98 - 0.5) * STAGE_U, RAIDER_OVER_U = 0.1 * STAGE_U;
  // 摆锤：半径 / 起止角（度，0 = 正下方，正 = 偏右）；按实测半径与角速度荡下，终点对齐判定点。
  const PEND = { ground: [0.83 * STAGE_U, 86, -9], air: [1.08 * STAGE_U, 56, -9] };
  const DEG = Math.PI / 180;
  // 上升 / 下落怪：x≈0.86W 从底 / 顶进场；纵移全程 ease-out，判定前约 0.3 s 贴近轨道（不要提前钉死）。
  const UPDOWN_X_U = (0.86 - 0.5) * STAGE_U * STAGE_ASPECT, UPDOWN_MERGE = 0.3, UPDOWN_EDGE_U = 4.3;

  // 配色向游戏靠拢：空中青蓝、地面粉紫（scene_01 怪物主色）。
  const COLOR = {
    air: "#38d6ff", ground: "#ff6ec7", boss: "#b26bff", gear: "#f4f1ff", gearCore: "#7a4dff",
    masher: "#e8204f", heart: "#ff2d6f", note: "#3f9bff", blood: "#ff1744"
  };
  const GLOW = { air: "rgba(56,214,255,.42)", ground: "rgba(255,110,199,.42)" };
  const RATE_COLOR = { 2: "#ffca28", 3: "#ff5252" };
  const NO_DASH = [];
  const TAU = Math.PI * 2;

  const HIT_FX_SEC = 0.22;
  const FX_MAX = 64;

  // 普通怪按 ibms 分体型：01-03 小怪、04-09 中怪、0A/0B 大怪（对应场景 prefab x10 / x11-x12 / x13-x14）。
  const MONSTER = {
    "01": ["small", 0.9], "02": ["small", 0.9], "03": ["small", 0.9],
    "04": ["medium1", 1], "05": ["medium1", 1], "06": ["medium1", 1],
    "07": ["medium2", 1], "08": ["medium2", 1], "09": ["medium2", 1],
    "0A": ["large1", 1.3], "0B": ["large2", 1.3]
  };

  /** 外观（体型 + 贴图键），prepare 时每颗算一次。需要 cat / special / pathway / bmsId。 */
  function lookOf(n) {
    const lane = n.pathway === 1 ? "air" : "ground";
    switch (n.cat) {
      case "gear": return { size: 1, spr: lane + "_gear" };
      case "masher": return { size: 1, spr: "masher" };
      case "hold": return { size: 1, spr: lane + "_hold_head", sprBody: lane + "_hold_body" };
      case "note": return { size: 0.85, spr: "note" };
      case "heart": return { size: 0.85, spr: "heart" };
      case "ghost": return { size: 1, spr: lane + "_ghost" };
    }
    if (n.special === "pendulum") return { size: 1.15, spr: lane + "_pendulum" };
    if (n.special === "raider") return { size: 1.1, spr: lane + "_raider" };
    if (n.cat === "boss") return { size: 1.3, spr: lane + "_boss" };
    const m = MONSTER[n.bmsId] || ["small", 0.95];
    return { size: m[1], spr: lane + "_" + m[0] };
  }

  /* ---------- 游戏动画模式（MDSceneGrabber 逐帧精灵表，scripts/pack_md_anims.py 生成） ---------- */
  // Spine 坐标（Unity 单位，y 向上）：物件原点在画面中心，in_* 从 x≈+10 飞到 -10，判定帧主体中心 x≈-3.85。
  // 横向直接用游戏几何（原点 = 舞台中心，单位 = 舞台高 / 7.5）；纵向按「判定帧主体中心」对齐两条轨道。
  const IN_RE = {
    nor: /^in_nor_\d+$/, up: /^in_up_\d+$/, down: /^in_down_\d+$/, num: /^in_\d+$/,
    boss: /^in_boss_\d_\d+$/, boss_1: /^boss_1_nor_\d+$/, any: /^(in_|boss_\d_nor_)/
  };
  const MASHER_HURT = ["hurt_1", "hurt_2", "hurt_3", "hurt_4"];
  // 场景内编号（两位，前面拼场景号）：01-09 → 10 小怪、04-06 → 11、07-09 → 12（nor / up / down），0A → 13、0B → 14。
  const ANIM_SPEC = {
    "01": ["10", "nor"], "02": ["10", "up"], "03": ["10", "down"],
    "04": ["11", "nor"], "05": ["11", "up"], "06": ["11", "down"],
    "07": ["12", "nor"], "08": ["12", "up"], "09": ["12", "down"],
    "0A": ["13", "nor"], "0B": ["14", "nor"],
    "0E": ["05", "nor"], "0G": ["04", "nor"], "0H": ["03", "nor"], "18": ["09", "boss_1"],
    "13": ["06", "boss"], "14": ["07", "boss"], "15": ["08", "boss"], "21": ["17", "nor"],
    "40": ["10", "nor"], "41": ["11", "nor"], "42": ["12", "nor"]
  };

  function animSpecOf(n) {
    if (n.cat === "hold" || n.cat === "note" || n.cat === "heart") return null;
    if (n.special === "pendulum") return ["15", "num"];
    if (n.special === "raider") return ["16", "up"];
    return ANIM_SPEC[n.bmsId] || null;
  }

  function median(a, dflt) {
    if (!a.length) return dflt;
    a.sort((x, y) => x - y);
    return a[a.length >> 1];
  }

  /** 精灵表数据 → 播放器用的派生字段（就地写入，只算一次）。 */
  function setupPack(pack) {
    // 纵向对齐用 body（pack_md_anims.py 算的判定帧主体中心）；旧包没有 body 时退回 hitPos（含影子，偏低）。
    const lane = { air: [], road: [] }, own = {};
    let prefix = null;
    for (const [el, anims] of Object.entries(pack.elements)) {
      if (!prefix && /^\d{4}/.test(el)) prefix = el.slice(0, 2);
      const m = /^\d\d(\d\d)(?:_(air|road))?$/.exec(el);
      own[el] = [];
      for (const [name, a] of Object.entries(anims)) {
        a.name = name;
        a.len = a.n / a.fps;
        const f = a.frames.find(Boolean);
        a.atBody = !!f && Math.abs((f[5] + f[3] / 2) / a.ppu) < 1.5 && Math.abs((f[6] + f[4] / 2) / a.ppu) < 1.5;
        const b = a.body || a.hitPos;
        if (!b) continue;
        own[el].push(b[1]);
        if (m && m[2] && m[1] >= "10" && m[1] <= "14" && IN_RE.nor.test(name)) lane[m[2]].push(b[1]);
      }
    }
    const ownBody = {};
    for (const el in own) ownBody[el] = median(own[el], 1.2);
    pack.prefix = prefix || "05";
    pack.laneBody = { road: median(lane.road, 1.2), air: median(lane.air, 3.3) };
    pack.ownBody = ownBody;
    pack.specCache = new Map();
  }

  /** 同组里判定帧最大 = 1 档（最慢），依次 2 / 3 档；组内不足三档时取最快的一个。 */
  function pickIn(anims, re, rate) {
    const list = Object.values(anims).filter((a) => a.hit && re.test(a.name)).sort((a, b) => b.hit - a.hit);
    return list.length ? list[Math.min(list.length - 1, Math.max(0, rate - 1))] : null;
  }

  function animOf(P, n) {
    const spec = animSpecOf(n);
    if (!spec) return null;
    // 倒置（0I / 0J）用对侧轨的入场动画，画时再关于固定轴镜像。
    const rate = n.approachRate || 1;
    const oneKey = P.prefix === "11" || n.bmsId === "40" || n.bmsId === "41" || n.bmsId === "42";
    const lane = oneKey ? "road" : ((n.pathway === 1) !== !!n.inverted ? "air" : "road");
    const key = spec[0] + spec[1] + lane + rate + (n.inverted ? "i" : "");
    if (P.specCache.has(key)) return P.specCache.get(key);
    const base = P.prefix + spec[0], other = lane === "air" ? "road" : "air";
    let el = null, suffix = null;
    for (const [k, s] of [[base + "_" + lane, lane], [base + "_" + other, other], [base, null]]) {
      if (P.elements[k]) { el = k; suffix = s; break; }
    }
    let out = null;
    if (el) {
      const anims = P.elements[el];
      const inA = pickIn(anims, IN_RE[spec[1]], rate) || pickIn(anims, IN_RE.any, rate);
      if (inA) {
        const gear = spec[0] === "03" || spec[0] === "09";
        const fb = P.elements[P.prefix + "11" + (suffix ? "_" + suffix : "_road")] || {};
        const hurts = spec[0] === "04" ? MASHER_HURT.map((h) => anims[h]).filter(Boolean) : null;
        out = {
          inA,
          outA: gear ? null : anims.out_p || fb.out_p || null,
          hurts,
          hurtLen: hurts ? hurts.reduce((m, h) => m + h.len, 0) || 1 : 0,
          offY: P.ownBody[el],
          flip: !!n.inverted,
          el
        };
      }
    }
    P.specCache.set(key, out);
    return out;
  }

  function frameAt(a, t) {
    let i = Math.floor(t * a.fps + 1e-6);
    if (a.loop) i = ((i % a.n) + a.n) % a.n;
    return i < 0 || i >= a.n ? null : a.frames[i];
  }

  function lowerBound(arr, t) {
    let lo = 0, hi = arr.length;
    while (lo < hi) { const m = (lo + hi) >> 1; if (arr[m].timeSec < t) lo = m + 1; else hi = m; }
    return lo;
  }

  class Renderer {
    constructor(canvas) {
      this.cv = canvas;
      this.ctx = canvas.getContext("2d");
      this.w = 0; this.h = 0;
      this.sprites = null;
      this.spritePacks = {};
      this.useSprites = true;
      this.anim = null;
      this.packs = {};
      this.useAnims = false;
      this.animAirLift = null;
      this.G = { w: -1, h: -1 };
      this.px = 0; this.py = 0; this.pvX = NaN; this.pvY = NaN;
      this.fxY = new Float32Array(FX_MAX);
      this.fxK = new Float32Array(FX_MAX);
      this.fxC = new Array(FX_MAX).fill("");
      this.fxN = 0;
      this.fontR = 0;
      this.comboFonts = [];
      this.resize();
      addEventListener("resize", () => this.resize());
    }

    resize() {
      const dpr = window.devicePixelRatio || 1;
      const r = this.cv.getBoundingClientRect();
      this.w = r.width; this.h = r.height;
      this.cv.width = Math.round(r.width * dpr);
      this.cv.height = Math.round(r.height * dpr);
      this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    /** manifest: MDP.mdAssets.sprites 或 MD_SPRITE_PACKS[key].sprites。 */
    loadSprites(manifest, base, onReady) {
      this.addSpritePack("default", manifest, base, (ok) => {
        if (ok) this.sprites = this.spritePacks.default;
        onReady(ok);
      });
    }

    addSpritePack(key, manifest, base, onReady) {
      const keys = Object.keys(manifest || {});
      if (!key || !keys.length) { onReady(false); return; }
      const out = {};
      let left = keys.length, ok = 0;
      const done = () => {
        if (--left) return;
        if (ok) {
          this.spritePacks[key] = out;
          if (!this.sprites) this.sprites = out;
        }
        onReady(ok > 0);
      };
      for (const k of keys) {
        const img = new Image();
        img.onload = () => { out[k] = { img, w: img.naturalWidth, h: img.naturalHeight }; ok++; done(); };
        img.onerror = done;
        img.src = base + manifest[k].file;
      }
    }

    /** pack: window.MD_ANIM_PACKS[key]；精灵表页全部加载后写入 this.packs[key]。 */
    addAnimPack(key, pack, base, onReady) {
      if (!key || !pack) { onReady(false); return; }
      if (!pack.specCache) setupPack(pack);
      const finish = (ok) => {
        if (ok) {
          this.packs[key] = pack;
          if (!this.anim) this.anim = pack;
        }
        onReady(ok);
      };
      if (pack.images) { finish(true); return; }
      const imgs = [];
      let left = pack.pages.length, bad = 0;
      const done = () => {
        if (--left) return;
        if (bad) { finish(false); return; }
        pack.images = imgs;
        finish(true);
      };
      pack.pages.forEach((p, i) => {
        const img = new Image();
        img.onload = done;
        img.onerror = () => { bad++; done(); };
        img.src = base + p.file;
        imgs[i] = img;
      });
    }

    setAnimPack(pack, base, onReady) {
      this.addAnimPack("default", pack, base, onReady);
    }

    /** 每份谱面对当前动画包算一次 n.an（选哪条入场动画、出场动画、纵向偏移），并找 Boss 出场区间。 */
    bindAnims(notes) {
      const P = this.anim;
      if (!P) {
        for (const n of notes) n.an = null;
        notes.animPack = null;
        notes.bossSpans = [];
        return;
      }
      for (const n of notes) n.an = animOf(P, n);
      const spans = [];
      for (const n of notes) {
        if (n.cat !== "boss" || n.special) continue;
        const s = n.timeSec - 2.5, e = n.timeSec + n.lengthSec + 1.5, last = spans[spans.length - 1];
        if (last && s - last[1] < 4) last[1] = Math.max(last[1], e); else spans.push([s, e]);
      }
      notes.bossSpans = spans;
      notes.animPack = P;
    }

    animFrame(a, f, ox, oy, unit, flipY) {
      const k = unit / a.ppu, ctx = this.ctx;
      if (flipY != null) { ctx.save(); ctx.translate(0, flipY * 2); ctx.scale(1, -1); }
      ctx.drawImage(this.anim.images[f[0]], f[1], f[2], f[3], f[4], ox + f[5] * k, oy + f[6] * k, f[3] * k, f[4] * k);
      if (flipY != null) ctx.restore();
    }

    /**
     * 时间线用游戏原生的 in_*：动画时间 = (now − 判定时刻) × 卷轴倍率 + 判定帧时间；预览默认全部 Perfect，到判定时刻切 out_p。
     * 倒置物件画对侧轨的 in_*（原点按对侧轨对齐），再关于目标轨的镜像轴上下翻转。
     * 摆锤 in_*：用判定帧 body 把主体平移到判定圈（与矢量模式同一取舍）；out_p 且 atBody 仍锚判定点、不镜像。
     */
    animNote(n, now, L) {
      const s = n.an, air = n.pathway === 1, y = air ? L.air : L.ground;
      const srcAir = s.flip ? !air : air;
      const oy = srcAir && this.animAirLift != null
        ? L.ground + (L.body.road - this.animAirLift) * L.unit
        : (srcAir ? L.air : L.ground) + s.offY * L.unit;
      const flip = s.flip ? (air ? L.axisAir : L.axisGround) : null;
      const t0 = n.timeSec, t1 = t0 + n.lengthSec;
      let a = s.inA, t = (now - t0) * L.spd + s.inA.hitT, ax = L.ox, ay = oy, fl = flip;
      if (s.hurts && now >= t0) {
        if (now <= t1 && s.hurts.length) {
          let u = (now - t0) % s.hurtLen, i = 0;
          while (i < s.hurts.length - 1 && u >= s.hurts[i].len) u -= s.hurts[i++].len;
          a = s.hurts[i]; t = u;
          this.pushFx(y, 0.3, COLOR.masher);
        } else if (s.outA) {
          a = s.outA; t = now - t1;
          if (t < HIT_FX_SEC) this.pushFx(y, t / HIT_FX_SEC, COLOR.masher);
        }
        if (a.atBody) { ax = L.hitX; ay = y; fl = null; }
      } else if (s.outA && now >= t0) {
        a = s.outA; t = now - t0;
        if (t < HIT_FX_SEC) this.pushFx(y, t / HIT_FX_SEC, this.colorOf(n));
        if (a.atBody) { ax = L.hitX; ay = y; fl = null; }
      } else if (n.special === "pendulum") {
        const b = s.inA.body || s.inA.hitPos;
        if (b) {
          ax += L.hitX - (L.ox + b[0] * L.unit);
          const solidY = oy - b[1] * L.unit;
          const wantY = fl == null ? y : 2 * fl - y;
          ay += wantY - solidY;
        }
      }
      const f = frameAt(a, t);
      if (!f) return;
      this.animFrame(a, f, ax, ay, L.unit, fl);
      if (L.showIds && a === s.inA) {
        const k = L.unit / a.ppu, top = oy + f[6] * k;
        this.label(n, ax + (f[5] + f[3] / 2) * k, fl == null ? top + 6 : 2 * fl - top - f[4] * k + 6, 0);
      }
    }

    animBoss(notes, now, L) {
      const el = this.anim.elements[this.anim.prefix + "01"], a = el && el.standby;
      if (!a) return;
      for (const sp of notes.bossSpans) {
        if (now < sp[0]) return;
        if (now > sp[1]) continue;
        const f = frameAt(a, now - sp[0]);
        if (f) this.animFrame(a, f, L.ox, L.ground + L.body.road * L.unit, L.unit, null);
        return;
      }
    }

    pushFx(y, k, c) {
      if (this.fxN >= FX_MAX) return;
      this.fxY[this.fxN] = y; this.fxK[this.fxN] = k; this.fxC[this.fxN] = c; this.fxN++;
    }

    /** 舞台几何：16:9 适配进画布（宽了左右留边、高了上下留边），原点 = 画布中心。结果写进 this.G，不分配。 */
    geometry() {
      const G = this.G, w = this.w, h = this.h;
      if (G.w === w && G.h === h) return G;
      const u = Math.min(h / STAGE_U, w / (STAGE_ASPECT * STAGE_U));
      G.w = w; G.h = h; G.u = u;
      G.ox = w / 2; G.cy = h / 2;
      G.hitX = G.ox + JUDGE_U * u;
      G.spawnX = G.ox + SPAWN_U * u;
      G.travel = TRAVEL_U * u;
      G.air = G.cy + AIR_U * u;
      G.ground = G.cy + GROUND_U * u;
      G.mid = (G.air + G.ground) / 2;
      G.axisGround = G.cy + AXIS_GROUND_U * u;
      G.axisAir = G.cy + AXIS_AIR_U * u;
      G.r = Math.max(12, Math.min(0.055 * STAGE_U * u, 32));
      return G;
    }

    /**
     * 物件 n 在 now 时刻的位置 → this.px / this.py（摆锤另给吊点 this.pvX / this.pvY，否则 NaN）。
     * 进度 p = 1 − (判定时刻 − now) / 飞行时长；未生成（p < 0）返回 false。
     * 直线横向速度 = 普通键 13.85u / (44|38|33 / 30 s)，短判定帧（齿轮 18、弹 13/14/15）同速从更近处出现。
     * 倒置（0I / 0J）= 对侧轨 0C / 0D 的轨迹上下镜像；矢量 / 贴图模式以两轨中线为轴，保证落在本轨判定圈上。
     */
    place(n, now, spd) {
      const G = this.G, dur = n.dur / spd, left = n.timeSec - now;
      this.pvX = this.pvY = NaN;
      if (left > dur) return false;
      const p = 1 - left / dur, air = n.pathway === 1, lane = air ? G.air : G.ground;
      const norDur = flightSec(FLIGHT.normal, n.approachRate || 1) / spd;
      let x = G.hitX + left / norDur * G.travel, y = lane;
      if (p < 1) {
        switch (n.path) {
          case "raider": {
            x = G.hitX + left / dur * G.travel;
            const srcAir = n.inverted ? !air : air;
            let edge = G.cy + RAIDER_EDGE_U * G.u;
            if (n.inverted) edge = 2 * G.mid - edge;
            if (p < RAIDER_JUMP) y = edge;
            else if (!srcAir && !n.inverted) {
              const peak = lane - RAIDER_OVER_U * G.u;
              if (p < RAIDER_PEAK) {
                const s = 1 - (p - RAIDER_JUMP) / (RAIDER_PEAK - RAIDER_JUMP);
                y = peak + (edge - peak) * s * s;
              } else {
                const s = (p - RAIDER_PEAK) / (1 - RAIDER_PEAK);
                y = peak + (lane - peak) * s * s;
              }
            } else {
              const s = 1 - (p - RAIDER_JUMP) / (1 - RAIDER_JUMP);
              y = lane + (edge - lane) * s * s * s;
            }
            break;
          }
          case "pendulum": {
            const P = (n.inverted ? !air : air) ? PEND.air : PEND.ground;
            const R = P[0] * G.u, a = (P[1] + (P[2] - P[1]) * p) * DEG, d = n.inverted ? -1 : 1;
            this.pvX = G.hitX - R * Math.sin(P[2] * DEG);
            this.pvY = lane - d * R * Math.cos(P[2] * DEG);
            x = this.pvX + R * Math.sin(a);
            y = this.pvY + d * R * Math.cos(a);
            break;
          }
          case "rise":
          case "drop": {
            const x0 = G.ox + UPDOWN_X_U * G.u;
            x = G.hitX + (1 - p) * (x0 - G.hitX);
            const s = 1 - p, edge = G.cy + (n.path === "rise" ? UPDOWN_EDGE_U : -UPDOWN_EDGE_U) * G.u;
            y = lane + (edge - lane) * s * s;
            break;
          }
        }
      }
      this.px = x; this.py = y;
      return true;
    }

    /** 调试 / 测量用：返回 { x, y, pvX, pvY, G }（会分配，不在主循环里用）。 */
    debugPlace(n, now, spd) {
      this.geometry();
      const ok = this.place(n, now, spd || 1);
      return ok ? { x: this.px, y: this.py, pvX: this.pvX, pvY: this.pvY, G: Object.assign({}, this.G) } : null;
    }

    /**
     * view: { notes(按 timeSec 排序的可玩物件，带 cat / dur / path / size / spr), maxLen, measures[{t, major, dur}], now,
     *         speed(卷轴倍率，时长 ÷ speed), lead(最长飞行秒数), showIds, pressed{air,ground} }
     */
    draw(view) {
      const { ctx, w, h } = this;
      ctx.clearRect(0, 0, w, h);
      const G = this.geometry();
      const hitX = G.hitX, laneY0 = G.ground, laneY1 = G.air, r = G.r;
      const spd = view.speed || 1, vps = G.travel * spd;
      const now = view.now;
      const sceneNow = view.sceneAt && view.sceneAt(now);
      const sceneKey = sceneNow && sceneNow.scene;
      const S = this.useSprites
        ? (sceneKey && this.spritePacks[sceneKey]) || this.sprites
        : null;
      if (this.dashR !== r) { this.dashR = r; this.ghostDash = [r * 0.38, r * 0.26]; }

      ctx.fillStyle = "rgba(0,0,0,.55)";
      ctx.fillRect(0, 0, w, h);

      ctx.lineWidth = 1;
      for (const m of view.measures) {
        const x = hitX + (m.t - now) * vps / m.dur;
        if (x < -2 || x > w + 2) continue;
        ctx.strokeStyle = m.major ? "rgba(255,255,255,.22)" : "rgba(255,255,255,.07)";
        ctx.beginPath(); ctx.moveTo(x, laneY1 - r * 2.2); ctx.lineTo(x, laneY0 + r * 2.2); ctx.stroke();
      }

      this.judge(hitX, laneY0, r, "ground", view.pressed.ground, S);
      this.judge(hitX, laneY1, r, "air", view.pressed.air, S);

      const notes = view.notes;
      const from = lowerBound(notes, now - view.maxLen - 2);
      const A = this.useAnims && sceneKey ? this.packs[sceneKey] || null : null;
      this.anim = A;
      const horizon = now + view.lead / spd + 0.5;
      this.fxN = 0;

      let L = null;
      if (A) {
        if (notes.animPack !== A) this.bindAnims(notes);
        L = this.L || (this.L = {});
        L.hitX = hitX; L.ground = laneY0; L.air = laneY1; L.body = A.laneBody; L.showIds = view.showIds;
        L.unit = G.u; L.ox = G.ox; L.spd = spd;
        L.axisGround = G.axisGround; L.axisAir = G.axisAir;
        this.animBoss(notes, now, L);
      }

      for (let i = from; i < notes.length; i++) {
        const n = notes[i];
        if (n.timeSec > horizon) break;
        if (A && n.an) { this.animNote(n, now, L); continue; }
        const y = n.pathway === 1 ? laneY1 : laneY0;
        const t0 = n.timeSec, t1 = n.timeSec + n.lengthSec;
        const k = vps / n.dur;
        if (t0 - now > n.dur / spd) continue;

        if (n.cat === "gear") {
          const x = hitX + (t0 - now) * k;
          if (x < -r * 2 || x > w + r * 2) continue;
          if (!(S && this.sprite(S, n.spr, x, y, r * 2.3))) this.gear(x, y, r);
          if (n.approachRate > 1) this.speed(n.approachRate, x, y, r);
          if (view.showIds) this.label(n, x, y, r);
          continue;
        }

        if (n.cat === "hold") {
          if (now > t1 + HIT_FX_SEC) continue;
          const xs = Math.max(hitX, hitX + (t0 - now) * k), xe = hitX + (t1 - now) * k;
          const lane = n.pathway === 1 ? "air" : "ground";
          if (now <= t1 && xe > hitX - r) {
            if (S && S[n.sprBody] && S[n.spr]) this.holdSprite(S, n.spr, n.sprBody, xs, xe, y, r);
            else this.hold(xs, xe, y, r, COLOR[lane]);
            if (n.approachRate > 1 && now < t0) this.speed(n.approachRate, xs, y, r);
            if (view.showIds) this.label(n, xs, y, r);
          }
          if (now >= t0 && now <= t1) this.pushFx(y, 0.5, COLOR[lane]);
          if (now > t1) this.pushFx(y, (now - t1) / HIT_FX_SEC, COLOR[lane]);
          continue;
        }

        if (n.cat === "masher") {
          if (now > t1 + HIT_FX_SEC) continue;
          if (now < t0) {
            const x = hitX + (t0 - now) * k;
            if (x > w + r * 2) continue;
            this.masherAt(S, x, y, r * 1.5);
            if (n.approachRate > 1) this.speed(n.approachRate, x, y, r * 1.5);
            if (view.showIds) this.label(n, x, y, r * 1.5);
          } else if (now <= t1) {
            this.masherAt(S, hitX, y, r * (1.5 + 0.18 * Math.sin(now * 40)));
            this.pushFx(y, 0.3, COLOR.masher);
          } else {
            this.pushFx(y, (now - t1) / HIT_FX_SEC, COLOR.masher);
          }
          continue;
        }

        if (now >= t0) {
          if (now - t0 < HIT_FX_SEC) this.pushFx(y, (now - t0) / HIT_FX_SEC, this.colorOf(n));
          continue;
        }
        if (!this.place(n, now, spd)) continue;
        const x = this.px, ny = this.py;
        if (x > w + r * 2 || ny < -r * 3 || ny > h + r * 3) continue;
        this.note(n, x, ny, r, S);
        if (n.approachRate > 1) this.speed(n.approachRate, x, ny, n.special ? r : r * n.size);
        if (view.showIds) this.label(n, x, ny, r);
      }

      ctx.lineWidth = 4;
      for (let j = 0; j < this.fxN; j++) {
        const f = Math.min(1, Math.max(0, this.fxK[j]));
        ctx.globalAlpha = 1 - f;
        ctx.strokeStyle = this.fxC[j];
        ctx.beginPath(); ctx.arc(hitX, this.fxY[j], r * (1.2 + f * 1.2), 0, TAU); ctx.stroke();
      }
      ctx.globalAlpha = 1;

      if (view.combo > 0) this.combo(view.combo, view.comboAge, w * 0.5, h * 0.17, r);
    }

    /** 贴图按最长边适配到 box；缺图返回 false 走矢量。 */
    sprite(S, key, x, y, box) {
      const s = S[key];
      if (!s) return false;
      const k = box / Math.max(s.w, s.h), dw = s.w * k, dh = s.h * k;
      this.ctx.drawImage(s.img, x - dw / 2, y - dh / 2, dw, dh);
      return true;
    }

    /** 判定圈：游戏里是四段断开的白环 + 中心小圈。 */
    judge(x, y, r, lane, on, S) {
      const ctx = this.ctx;
      if (on) {
        ctx.fillStyle = GLOW[lane];
        ctx.beginPath(); ctx.arc(x, y, r * 1.45, 0, TAU); ctx.fill();
      }
      ctx.lineWidth = 2;
      ctx.strokeStyle = COLOR[lane];
      ctx.globalAlpha = 0.5;
      ctx.beginPath(); ctx.arc(x, y, r * 1.55, 0, TAU); ctx.stroke();
      ctx.globalAlpha = 1;
      if (S && S.judge_out) {
        this.sprite(S, "judge_out", x, y, r * 2.7);
        this.sprite(S, "judge_in", x, y, r * 0.75);
        return;
      }
      ctx.lineWidth = Math.max(2.5, r * 0.14);
      ctx.lineCap = "round";
      ctx.strokeStyle = "#fff";
      ctx.beginPath();
      for (let q = 0; q < 4; q++) {
        const a = q * Math.PI / 2 + Math.PI / 4;
        ctx.moveTo(x + Math.cos(a - 0.5) * r * 1.25, y + Math.sin(a - 0.5) * r * 1.25);
        ctx.arc(x, y, r * 1.25, a - 0.5, a + 0.5);
      }
      ctx.stroke();
      ctx.lineCap = "butt";
      ctx.lineWidth = 2;
      ctx.beginPath(); ctx.arc(x, y, r * 0.22, 0, TAU); ctx.stroke();
    }

    combo(n, age, x, y, r) {
      const ctx = this.ctx;
      if (this.fontR !== r) {
        this.fontR = r;
        this.comboFonts.length = 0;
        this.comboSmall = `bold ${Math.round(r * 0.5)}px "Segoe UI", sans-serif`;
      }
      const pop = age < 0.12 ? 1 + 0.25 * (1 - age / 0.12) : 1;
      const size = Math.round(r * 1.6 * pop);
      const font = this.comboFonts[size] || (this.comboFonts[size] = `bold ${size}px "Segoe UI", "Microsoft YaHei", sans-serif`);
      ctx.textAlign = "center";
      ctx.textBaseline = "alphabetic";
      ctx.font = font;
      ctx.lineWidth = 4;
      ctx.strokeStyle = "rgba(0,0,0,.6)";
      const s = String(n);
      ctx.strokeText(s, x, y);
      ctx.fillStyle = "#fff";
      ctx.fillText(s, x, y);
      ctx.font = this.comboSmall;
      ctx.fillStyle = "#ffd54f";
      ctx.fillText("COMBO", x, y + r * 0.75);
    }

    colorOf(n) {
      if (n.cat === "boss" && !n.special) return COLOR.boss;
      if (n.cat === "heart") return COLOR.heart;
      if (n.cat === "note") return COLOR.note;
      return n.pathway === 1 ? COLOR.air : COLOR.ground;
    }

    note(n, x, y, r, S) {
      const c = this.colorOf(n);
      if (S && S[n.spr]) return this.noteSprite(S, n, x, y, r, c);
      if (n.cat === "note") return this.musicNote(x, y, r * 0.8, c);
      if (n.cat === "heart") return this.heart(x, y, r * 0.62, c);
      if (n.special === "pendulum") return this.pendulum(x, y, r, c, n.inverted, n.blood);
      if (n.special === "raider") return this.raider(x, y, r, c, n.inverted, n.blood);
      if (n.cat === "ghost") return this.ghost(x, y, r, c);
      if (n.cat === "boss") return this.boss(x, y, r * n.size, n.blood);
      this.monster(x, y, r * n.size, c, n.blood);
    }

    noteSprite(S, n, x, y, r, c) {
      const ctx = this.ctx, box = r * 2.3 * n.size;
      if (n.special === "pendulum") {
        this.rope(x, y, r, n.inverted, Math.max(2, r * 0.12));
      } else if (n.special === "raider") {
        this.streaks(x, y, r, c, n.inverted ? 1 : -1);
      }
      if (n.blood) {
        ctx.lineWidth = 4; ctx.strokeStyle = COLOR.blood;
        ctx.beginPath(); ctx.arc(x, y, box * 0.5, 0, TAU); ctx.stroke();
      }
      if (n.cat === "ghost") ctx.globalAlpha = 0.6;
      this.sprite(S, n.spr, x, y, box);
      ctx.globalAlpha = 1;
    }

    /** 普通怪：亮色圆身 + 高光 + 朝左的眼睛。 */
    monster(x, y, r, c, blood) {
      const ctx = this.ctx;
      this.circle(x, y, r, c, blood, "#fff");
      ctx.fillStyle = "rgba(255,255,255,.35)";
      ctx.beginPath(); ctx.ellipse(x + r * 0.25, y - r * 0.45, r * 0.42, r * 0.2, -0.35, 0, TAU); ctx.fill();
      ctx.fillStyle = "#fff";
      ctx.beginPath(); ctx.arc(x - r * 0.3, y - r * 0.05, r * 0.3, 0, TAU); ctx.fill();
      ctx.fillStyle = "#1a1030";
      ctx.beginPath(); ctx.arc(x - r * 0.4, y - r * 0.05, r * 0.15, 0, TAU); ctx.fill();
    }

    /** Boss 抛射物：大号紫色炸弹 + 引信。 */
    boss(x, y, r, blood) {
      const ctx = this.ctx;
      ctx.fillStyle = "#3b2a5c";
      ctx.fillRect(x - r * 0.28, y - r * 1.12, r * 0.56, r * 0.3);
      ctx.strokeStyle = "#ffd54f"; ctx.lineWidth = Math.max(2, r * 0.08);
      ctx.beginPath(); ctx.moveTo(x, y - r * 1.12); ctx.quadraticCurveTo(x + r * 0.25, y - r * 1.4, x + r * 0.45, y - r * 1.3); ctx.stroke();
      this.circle(x, y, r, COLOR.boss, blood, "#fff");
      ctx.fillStyle = "rgba(255,255,255,.4)";
      ctx.beginPath(); ctx.arc(x - r * 0.38, y - r * 0.38, r * 0.2, 0, TAU); ctx.fill();
    }

    circle(x, y, r, color, blood, edge) {
      const ctx = this.ctx;
      ctx.fillStyle = color;
      ctx.beginPath(); ctx.arc(x, y, r, 0, TAU); ctx.fill();
      ctx.lineWidth = blood ? 4 : edge ? 2.5 : 2;
      ctx.strokeStyle = blood ? COLOR.blood : edge || "rgba(0,0,0,.45)";
      ctx.stroke();
    }

    ghost(x, y, r, c) {
      const ctx = this.ctx;
      ctx.beginPath(); ctx.arc(x, y, r, 0, TAU);
      ctx.globalAlpha = 0.2;
      ctx.fillStyle = c; ctx.fill();
      ctx.globalAlpha = 1;
      ctx.setLineDash(this.ghostDash);
      ctx.lineWidth = 2.5; ctx.strokeStyle = c; ctx.stroke();
      ctx.setLineDash(NO_DASH);
    }

    /** 吊绳：有吊点（place 算出的 pvX / pvY）就连到吊点，否则按旧画法竖直画一小段并加吊环。 */
    rope(x, y, r, inverted, lw) {
      const ctx = this.ctx;
      ctx.lineWidth = lw;
      ctx.strokeStyle = "#fff";
      ctx.beginPath();
      if (this.pvX === this.pvX) {
        ctx.moveTo(x, y); ctx.lineTo(this.pvX, this.pvY); ctx.stroke();
        return;
      }
      const py = y + (inverted ? 1 : -1) * r * 1.85;
      ctx.moveTo(x, y); ctx.lineTo(x, py); ctx.stroke();
      ctx.fillStyle = "#fff";
      ctx.beginPath(); ctx.arc(x, py, r * 0.2, 0, TAU); ctx.fill();
    }

    pendulum(x, y, r, c, inverted, blood) {
      this.rope(x, y, r, inverted, Math.max(2, r * 0.14));
      this.circle(x, y, r, c, blood, "#fff");
    }

    streaks(x, y, r, c, d) {
      const ctx = this.ctx;
      ctx.globalAlpha = 0.6;
      ctx.lineCap = "round";
      ctx.lineWidth = Math.max(2, r * 0.12);
      ctx.strokeStyle = c;
      ctx.beginPath();
      for (let k = -1; k <= 1; k++) {
        const sy = y + k * r * 0.42;
        ctx.moveTo(x + r * 0.75, sy); ctx.lineTo(x + r * (2 - Math.abs(k) * 0.45), sy + d * r * 0.55);
      }
      ctx.stroke();
      ctx.globalAlpha = 1;
      ctx.lineCap = "butt";
    }

    raider(x, y, r, c, inverted, blood) {
      const ctx = this.ctx;
      this.streaks(x, y, r, c, inverted ? 1 : -1);
      ctx.fillStyle = c;
      ctx.beginPath();
      ctx.moveTo(x - r * 1.1, y); ctx.lineTo(x + r * 0.75, y - r * 0.95);
      ctx.lineTo(x + r * 0.35, y); ctx.lineTo(x + r * 0.75, y + r * 0.95);
      ctx.closePath(); ctx.fill();
      ctx.lineJoin = "round";
      ctx.lineWidth = blood ? 4 : 2.5;
      ctx.strokeStyle = blood ? COLOR.blood : "#fff";
      ctx.stroke();
      ctx.lineJoin = "miter";
    }

    masherAt(S, x, y, r) {
      if (S && this.sprite(S, "masher", x, y, r * 2.1)) return;
      const ctx = this.ctx;
      this.circle(x, y, r, COLOR.masher, false, "#fff");
      ctx.lineWidth = Math.max(2, r * 0.08);
      ctx.beginPath(); ctx.arc(x, y, r * 0.62, 0, TAU); ctx.stroke();
      ctx.beginPath(); ctx.arc(x, y, r * 0.28, 0, TAU); ctx.stroke();
    }

    /** 长按：粗胶囊条 + 两端星形头（游戏里头尾是星星）。 */
    hold(xs, xe, y, r, c) {
      const ctx = this.ctx;
      if (xe > xs) {
        ctx.lineCap = "round";
        ctx.globalAlpha = 0.5;
        ctx.lineWidth = r * 1.15; ctx.strokeStyle = c;
        ctx.beginPath(); ctx.moveTo(xs, y); ctx.lineTo(xe, y); ctx.stroke();
        ctx.globalAlpha = 0.85;
        ctx.lineWidth = Math.max(2, r * 0.12); ctx.strokeStyle = "#fff";
        ctx.beginPath(); ctx.moveTo(xs, y - r * 0.3); ctx.lineTo(xe, y - r * 0.3);
        ctx.moveTo(xs, y + r * 0.3); ctx.lineTo(xe, y + r * 0.3); ctx.stroke();
        ctx.globalAlpha = 1;
        ctx.lineCap = "butt";
      }
      this.star(xe, y, r * 0.75, c);
      this.star(xs, y, r * 1.1, c);
    }

    holdSprite(S, head, bodyKey, xs, xe, y, r) {
      const ctx = this.ctx, body = S[bodyKey], bh = r * 1.1, k = bh / body.h, tile = body.w * k;
      for (let x = xs; x < xe; x += tile) {
        const dw = Math.min(tile, xe - x);
        ctx.drawImage(body.img, 0, 0, dw / k, body.h, x, y - bh / 2, dw, bh);
      }
      this.sprite(S, head, xe, y, r * 1.6);
      this.sprite(S, head, xs, y, r * 2.4);
    }

    star(x, y, R, c) {
      const ctx = this.ctx, ri = R * 0.48;
      ctx.beginPath();
      for (let i = 0; i < 10; i++) {
        const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? ri : R;
        ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr);
      }
      ctx.closePath();
      ctx.fillStyle = c; ctx.fill();
      ctx.lineJoin = "round"; ctx.lineWidth = Math.max(2, R * 0.14); ctx.strokeStyle = "#fff"; ctx.stroke();
      ctx.lineJoin = "miter";
    }

    /** 飞近档 2 / 3：物件左上角 2 / 3 道指向行进方向的箭头。 */
    speed(rate, x, y, r) {
      const ctx = this.ctx;
      const s = Math.max(4, r * 0.22), cx = x - r * 0.8, cy = y - r * 0.95;
      ctx.beginPath();
      for (let i = 0; i < rate; i++) {
        const px = cx - i * s * 0.9;
        ctx.moveTo(px + s * 0.65, cy - s); ctx.lineTo(px, cy); ctx.lineTo(px + s * 0.65, cy + s);
      }
      ctx.lineCap = "round"; ctx.lineJoin = "round";
      ctx.lineWidth = Math.max(3.5, s * 0.8); ctx.strokeStyle = "rgba(0,0,0,.65)"; ctx.stroke();
      ctx.lineWidth = Math.max(1.6, s * 0.38); ctx.strokeStyle = RATE_COLOR[rate] || "#fff"; ctx.stroke();
      ctx.lineCap = "butt"; ctx.lineJoin = "miter";
    }

    /** 齿轮：白色尖齿锯片 + 紫色轮毂。 */
    gear(x, y, r) {
      const ctx = this.ctx;
      const teeth = 12, ro = r * 1.12, ri = r * 0.8;
      ctx.beginPath();
      for (let i = 0; i < teeth * 2; i++) {
        const a = (i / (teeth * 2)) * TAU;
        const rr = i % 2 ? ri : ro;
        ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr);
      }
      ctx.closePath();
      ctx.fillStyle = COLOR.gear; ctx.fill();
      ctx.lineWidth = 1.5; ctx.strokeStyle = "rgba(60,30,120,.6)"; ctx.stroke();
      ctx.fillStyle = COLOR.gearCore;
      ctx.beginPath(); ctx.arc(x, y, r * 0.66, 0, TAU); ctx.fill();
      ctx.fillStyle = "#e9a6ff";
      ctx.beginPath(); ctx.arc(x, y, r * 0.34, 0, TAU); ctx.fill();
      ctx.lineWidth = 2; ctx.strokeStyle = "#4a2a9a";
      ctx.beginPath(); ctx.moveTo(x - r * 0.66, y); ctx.lineTo(x + r * 0.66, y);
      ctx.moveTo(x, y - r * 0.66); ctx.lineTo(x, y + r * 0.66); ctx.stroke();
    }

    heart(x, y, s, c) {
      const ctx = this.ctx;
      ctx.beginPath();
      ctx.moveTo(x, y + s);
      ctx.bezierCurveTo(x - s * 1.6, y - s * 0.2, x - s * 0.6, y - s * 1.4, x, y - s * 0.5);
      ctx.bezierCurveTo(x + s * 0.6, y - s * 1.4, x + s * 1.6, y - s * 0.2, x, y + s);
      ctx.fillStyle = c; ctx.fill();
      ctx.lineWidth = 2; ctx.strokeStyle = "#fff"; ctx.stroke();
      ctx.fillStyle = "rgba(255,255,255,.55)";
      ctx.beginPath(); ctx.ellipse(x - s * 0.5, y - s * 0.45, s * 0.22, s * 0.13, -0.6, 0, TAU); ctx.fill();
    }

    /** 音符：双八分音符（♫）。 */
    musicNote(x, y, s, c) {
      const ctx = this.ctx;
      const lx = x - s * 0.45, rx = x + s * 0.5, ly = y + s * 0.55, ry = y + s * 0.4;
      ctx.lineJoin = "round";
      ctx.beginPath();
      ctx.ellipse(lx, ly, s * 0.32, s * 0.24, -0.4, 0, TAU);
      ctx.moveTo(rx + s * 0.32, ry);
      ctx.ellipse(rx, ry, s * 0.32, s * 0.24, -0.4, 0, TAU);
      ctx.moveTo(lx + s * 0.26, ly - s * 0.05);
      ctx.lineTo(lx + s * 0.26, y - s * 0.6);
      ctx.lineTo(rx + s * 0.26, y - s * 0.78);
      ctx.lineTo(rx + s * 0.26, ry - s * 0.05);
      ctx.lineTo(rx + s * 0.1, ry - s * 0.05);
      ctx.lineTo(rx + s * 0.1, y - s * 0.45);
      ctx.lineTo(lx + s * 0.42, y - s * 0.3);
      ctx.lineTo(lx + s * 0.42, ly - s * 0.05);
      ctx.closePath();
      ctx.lineWidth = 3; ctx.strokeStyle = "#fff"; ctx.stroke();
      ctx.fillStyle = c; ctx.fill();
      ctx.lineJoin = "miter";
    }

    label(n, x, y, r) {
      const ctx = this.ctx;
      ctx.font = "11px Consolas, monospace";
      ctx.textAlign = "center";
      ctx.fillStyle = "#fff";
      const rate = n.approachRate > 1 ? ` ×${n.approachRate}` : "";
      const top = n.special === "pendulum" && !n.inverted ? r * 2.05 : r * (n.size || 1);
      ctx.fillText(n.bmsId + rate, x, y - top - 6);
    }
  }

  MDP.Renderer = Renderer;
  MDP.lookOf = lookOf;
  MDP.flightOf = flightOf;
  MDP.normalFlight = normalFlight;
})(window.MDP = window.MDP || {});
