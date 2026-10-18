/* 连击计数。预览为自动演奏：判定点经过即算命中。以后接判定时，用判定结果替换 at() 的时间计数即可。 */
(function (MDP) {
  "use strict";

  // 每类物件贡献连击的时刻：head = 头，tail = 尾。齿轮、音符、红心不计连击。
  const RULES = {
    monster: ["head"],
    boss: ["head"],
    ghost: ["head"],
    hold: ["head", "tail"],
    holdstar: ["head"],
    masher: ["head"]
  };

  /** 可玩物件 → 升序的连击时刻表。 */
  function buildTimes(notes) {
    const times = [];
    for (const n of notes) {
      const parts = RULES[n.cat];
      if (!parts) continue;
      for (const p of parts) times.push(p === "tail" ? n.timeSec + n.lengthSec : n.timeSec);
    }
    return times.sort((a, b) => a - b);
  }

  /** now 时刻的连击数 = 时刻表中 <= now 的个数。 */
  function at(times, now) {
    let lo = 0, hi = times.length;
    while (lo < hi) { const m = (lo + hi) >> 1; if (times[m] <= now) lo = m + 1; else hi = m; }
    return lo;
  }

  MDP.combo = { RULES, buildTimes, at };
})(window.MDP = window.MDP || {});
