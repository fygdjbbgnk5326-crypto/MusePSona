/* 谱面时钟 = WebAudio 时间线。没有音频时照常走时，方便只看谱。以后做判定直接复用 time / ctxTimeOf。 */
(function (MDP) {
  "use strict";

  class AudioClock {
    constructor() {
      this.ctx = new (window.AudioContext || window.webkitAudioContext)();
      this.musicGain = this.ctx.createGain();
      this.sfxGain = this.ctx.createGain();
      this.musicGain.connect(this.ctx.destination);
      this.sfxGain.connect(this.ctx.destination);
      this.buffer = null;
      this.source = null;
      this.playing = false;
      this.rate = 1;
      this.offset = 0;
      this.startCtx = 0;
      this.fallbackDuration = 0;
      this.onEnded = null;
    }

    async load(bytes) {
      this.stop();
      this.buffer = null;
      if (!bytes) return;
      const copy = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
      this.buffer = await this.ctx.decodeAudioData(copy);
    }

    get duration() { return this.buffer ? this.buffer.duration : this.fallbackDuration; }

    get time() {
      if (!this.playing) return this.offset;
      return this.offset + (this.ctx.currentTime - this.startCtx) * this.rate;
    }

    /** 谱面时间 t（秒）对应的 AudioContext 时间，用于精确排程打击音。 */
    ctxTimeOf(t) { return this.startCtx + (t - this.offset) / this.rate; }

    async play() {
      if (this.playing) return;
      if (this.ctx.state === "suspended") await this.ctx.resume();
      if (this.offset >= this.duration - 0.01) this.offset = 0;
      this.startCtx = this.ctx.currentTime;
      if (this.buffer) {
        const src = this.ctx.createBufferSource();
        src.buffer = this.buffer;
        src.playbackRate.value = this.rate;
        src.connect(this.musicGain);
        src.onended = () => { if (this.source === src) this.finish(); };
        src.start(0, Math.max(0, this.offset));
        this.source = src;
      }
      this.playing = true;
    }

    pause() {
      if (!this.playing) return;
      this.offset = this.time;
      this.stopSource();
      this.playing = false;
    }

    stop() { this.pause(); this.offset = 0; }

    seek(t) {
      const was = this.playing;
      this.pause();
      this.offset = Math.min(Math.max(0, t), this.duration);
      if (was) this.play();
    }

    setRate(r) {
      const was = this.playing;
      this.pause();
      this.rate = r;
      if (was) this.play();
    }

    /** 无音频模式下由渲染循环调用，走到结尾时停下。 */
    tick() { if (this.playing && !this.buffer && this.time >= this.duration) this.finish(); }

    finish() {
      this.offset = this.duration;
      this.stopSource();
      this.playing = false;
      if (this.onEnded) this.onEnded();
    }

    stopSource() {
      if (!this.source) return;
      const s = this.source;
      this.source = null;
      try { s.stop(); } catch (_) { /* 已停止 */ }
      s.disconnect();
    }

    /** 在 AudioContext 时间 when 播一声短促打击音。 */
    blip(when, freq, dur = 0.045, vol = 0.5) {
      const ctx = this.ctx;
      const osc = ctx.createOscillator();
      const g = ctx.createGain();
      osc.type = "triangle";
      osc.frequency.setValueAtTime(freq, when);
      osc.frequency.exponentialRampToValueAtTime(freq * 0.5, when + dur);
      g.gain.setValueAtTime(vol, when);
      g.gain.exponentialRampToValueAtTime(0.001, when + dur);
      osc.connect(g).connect(this.sfxGain);
      osc.start(when);
      osc.stop(when + dur + 0.01);
      return osc;
    }
  }

  MDP.AudioClock = AudioClock;
})(window.MDP = window.MDP || {});
