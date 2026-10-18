/* MDM → 播放器用的数据：info、各难度谱面、媒体字节。对应 C# 的 MdmPacker.Unpack + FolderChart.Load。 */
(function (MDP) {
  "use strict";

  const MUSIC_NAMES = ["music.ogg", "music.mp3", "music.wav", "music.flac"];
  const DEMO_NAMES = ["demo.ogg", "demo.mp3"];
  const COVER_NAMES = ["cover.png", "cover.jpg", "cover.jpeg", "cover.gif"];
  const MIME = { png: "image/png", jpg: "image/jpeg", jpeg: "image/jpeg", gif: "image/gif" };

  const textOf = (bytes) => new TextDecoder("utf-8").decode(bytes).replace(/^\uFEFF/, "");

  function lowerKeys(obj) {
    const out = {};
    for (const [k, v] of Object.entries(obj || {})) out[k.toLowerCase()] = v;
    return out;
  }

  async function loadMdm(file) {
    const entries = MDP.readZip(await file.arrayBuffer());
    const pick = (names) => names.map((n) => entries.get(n)).find(Boolean) || null;

    let info = {};
    const infoEntry = entries.get("info.json");
    if (infoEntry) {
      try { info = lowerKeys(JSON.parse(textOf(await infoEntry.read()))); }
      catch (e) { console.warn("info.json 解析失败", e); }
    }

    let stored = null;
    const projEntry = entries.get("chart.json");
    if (projEntry) {
      try { stored = JSON.parse(textOf(await projEntry.read())); }
      catch (e) { console.warn("chart.json 解析失败", e); }
    }
    const storedCharts = lowerKeys(stored && (stored.charts || stored.Charts));

    const charts = {};
    for (let d = 1; d <= 5; d++) {
      const e = entries.get(`map${d}.bms`);
      if (!e) continue;
      const chart = MDP.bms.toChart(MDP.bms.parseBms(textOf(await e.read())));
      const sc = storedCharts[String(d)];
      if (sc) chart.overrides = MDP.bms.applyOverrides(chart, sc.notes || sc.Notes);
      chart.level = info["difficulty" + d] || chart.info.playLevel || "?";
      charts[d] = chart;
    }
    if (!Object.keys(charts).length) throw new Error("MDM 里没有 map1~map5.bms");

    const musicEntry = pick(MUSIC_NAMES);
    const coverEntry = pick(COVER_NAMES);
    const demoEntry = pick(DEMO_NAMES);

    let coverUrl = null;
    if (coverEntry) {
      const ext = coverEntry.name.split(".").pop().toLowerCase();
      coverUrl = URL.createObjectURL(new Blob([await coverEntry.read()], { type: MIME[ext] || "image/png" }));
    }

    return {
      fileName: file.name,
      info,
      charts,
      music: musicEntry ? { name: musicEntry.name, bytes: await musicEntry.read() } : null,
      demoName: demoEntry ? demoEntry.name : null,
      coverUrl,
      files: [...entries.values()].map((e) => e.name)
    };
  }

  MDP.loadMdm = loadMdm;
})(window.MDP = window.MDP || {});
