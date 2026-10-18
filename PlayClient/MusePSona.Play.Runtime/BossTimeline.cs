using MuseDashChart;

namespace MusePSona.Play;

/// <summary>
/// 官谱通道 15 的 Boss 本体。x01 只演出，不进判定。
/// 可打的仍是弹幕 13/14/15 与近战 11/12（以及连打 16/17）；待机 / 入场 / 换阶段 / 远攻循环都不算击打。
/// </summary>
public sealed class BossTimeline
{
    public const float LeadIn = 2.5f;
    public const float TrailOut = 1.5f;
    public const float MergeGap = 4f;
    /// <summary>
    /// 高度钉两轨中线（与近战/连打同一条中轨）。待机 body 槽对到 MidY；不要再往地面偏，0101 少女会站到地轨上。
    /// 水平落在判定线→可见右缘的 3:1（0.75）。不要用最右 1/4 段中心（0.875）：x01 体型偏右，会裁出画面。
    /// </summary>
    public const float BodyAlong = 0.75f;
    public static float BodyX => BodyXForAspect(16f / 9f);
    public static float BodyXForAspect(float aspect)
    {
        if (aspect < 1.2f) aspect = 1.2f;
        var visRight = Flight.CameraSize * aspect;
        return Flight.HitX + (visRight - Flight.HitX) * BodyAlong;
    }
    public static float BodyY => Flight.MidY;
    /// <summary>0101 的 in 是 31 帧，人从轨道右侧走进来。</summary>
    public const float InSec = 31f / 30f;
    /// <summary>0601 的 out 要到约 1.6s 才滑出画面，1.0s 时人还在原位。</summary>
    public const float OutSec = 1.7f;
    public const float StartSec = 0.7f;
    /// <summary>1D/1F 与弹幕投完后的 far_atk_*_end 回场（0101 实测 0.7s）。</summary>
    public const float FarEndSec = 0.7f;
    /// <summary>同一拍里的 1C/1D、1E/1F 是连发。重开 start 会把人从悬停按回地面，这段间隔内保持原悬停。</summary>
    public const float FarHoldGap = 0.5f;
    public const float PhaseSec = 0.7f;
    public const float Close1Hit = 48f / 30f;
    public const float Close1Dur = 2.6f;
    public const float Close2Hit = 24f / 30f;
    public const float Close2Dur = 1.8f;
    public const float FireSec = 1f;
    /// <summary>0101 的 hurt 在 0.7s 已回到原位。0601 的 hurt 要 0.9s，回场还占 close_atk 命中后的一整段，不要用这个数提前切待机。</summary>
    public const float HurtSec = 0.7f;
    /// <summary>16 挨打后整段 multi_atk_out 回场（0101 约 1.03s，0601 约 0.94s）。</summary>
    public const float MashOutSec = 31f / 30f;
    /// <summary>
    /// 17 长条结束后留给 multi_atk_48_end 的时间。
    /// 0101 这段约 1.83s，从受击位退场。0601 同名片段长 2.60s，从右侧冲出再离场，1.83s 会在离场前被掐掉。
    /// 短片段播完由播放端按片长藏掉，这里按长的留。
    /// </summary>
    public const float Mash48EndSec = 2.65f;
    /// <summary>读不到 X 骨关键帧时，长片段从冲刺前 0.5s 起播。</summary>
    public const float MashEndLead = Close1Hit - 0.5f;
    /// <summary>
    /// 连打结束瞬间就要对准突击中段。0101 的 48_end 在 1.5s 以前骨 X 还在右侧，
    /// 再晚 0.25s 去对，38.4s 看起来仍是原地待机。
    /// </summary>
    public const float MashEndSight = 0f;

    /// <summary>0101 multi_atk_hurt_end。点按 17 的瞬间突击，自带离场。</summary>
    public const float HurtEndSec = 31f / 30f;

    public readonly struct Cue
    {
        public Cue(float t, string id) { T = t; Id = id; }
        public float T { get; }
        public string Id { get; }
    }

    public readonly struct Span
    {
        public Span(float a, float b) { Start = a; End = b; }
        public float Start { get; }
        public float End { get; }
    }

    public readonly struct View
    {
        public bool Visible { get; init; }
        public string Clip { get; init; }
        public float Time { get; init; }
        public bool Loop { get; init; }
        public bool CountsHit { get; init; }
        /// <summary>连打段对应的键 Id（0 = 不是连打）。挨打/没打中由调用方按判定换 clip。</summary>
        public int MashId { get; init; }
    }

    public IReadOnlyList<Cue> Cues { get; }
    public IReadOnlyList<Span> Spans { get; }
    public IReadOnlyList<PlayNote> Attacks { get; }

    BossTimeline(List<Cue> cues, List<Span> spans, List<PlayNote> attacks)
    {
        Cues = cues;
        Spans = spans;
        Attacks = attacks;
    }

    public static bool IsScript(string? id)
    {
        var s = (id ?? "").ToUpperInvariant();
        return s is "1A" or "1B" or "1C" or "1D" or "1E" or "1F" or "1G" or "1H" or "1L" or "1M";
    }

    /// <summary>会拉出 Boss 本体的键：近战/弹幕/连打/齿轮/3x。突击者倒 0I/0J 不算。</summary>
    public static bool DrawsBody(PlayNote n)
    {
        if (n.Special != NoteSpecial.None) return false;
        var id = n.BmsId ?? "";
        if (id.Length == 2 && id[0] == '3') return true;
        return id is "11" or "12" or "13" or "14" or "15" or "16" or "17" or "18";
    }

    public static bool IsDanmaku(string? id)
        => id is "13" or "14" or "15";

    public static bool IsMelee(string? id)
        => id is "11" or "12";

    /// <summary>Boss 连打。判定仍是 Masher，外观走本体 multi_atk，不要再画 x04。</summary>
    public static bool IsBossMash(string? id)
        => id is "16" or "17";

    /// <summary>近战 11/12 与连打 16/17 打的是本体（挥砍/冲刺到判定线），不另画飞近物件。</summary>
    public static bool BodyIsTarget(string? id)
        => IsMelee(id) || IsBossMash(id);

    public static BossTimeline Build(IEnumerable<Placement> chart, IEnumerable<PlayNote> playable)
    {
        var cues = chart
            .Where(n => IsScript(n.BmsId))
            .OrderBy(n => n.TimeSec).ThenBy(n => n.Id)
            .Select(n => new Cue((float)n.TimeSec, (n.BmsId ?? "").ToUpperInvariant()))
            .ToList();
        var attacks = playable.Where(DrawsBody).OrderBy(n => n.TimeSec).ToList();
        var spans = new List<Span>();
        foreach (var n in attacks)
        {
            var s = (float)n.TimeSec - LeadIn;
            var e = (float)n.EndSec + TrailOut;
            if (spans.Count > 0 && s - spans[^1].End < MergeGap)
                spans[^1] = new Span(spans[^1].Start, Math.Max(spans[^1].End, e));
            else
                spans.Add(new Span(s, e));
        }
        return new BossTimeline(cues, spans, attacks);
    }

    public View At(float t)
    {
        Fold(t, out var shown, out var enterAt, out var leaveAt, out var hidden, out var hideOutAt, out var hideStealth,
            out var phase, out var phaseAt, out var far1, out var far1At, out var far2, out var far2At,
            out var farCross, out var farCrossAt, out var far1EndAt, out var far2EndAt);

        // 17 连打后退场盖过远程收尾和 1L。官谱 37.976 的 17 正压在 1F 和 1L 中间。
        var mash = MashAt(t);
        if (mash.Visible)
        {
            var meleeOver = MeleeAt(t);
            if (meleeOver.Visible) return meleeOver;
            var shotOver = ShotAt(t);
            if (shotOver.Visible) return shotOver;
            return mash;
        }

        // 1F 的 far_atk_*_end 播完再藏。官谱 1L 只是消失，不要在这拍前插入冲刺。
        if (!far2 && far2EndAt >= 0f && t - far2EndAt < FarEndSec)
            return Play("far_atk_2_end", t - far2EndAt, false);
        if (!far1 && far1EndAt >= 0f && t - far1EndAt < FarEndSec)
            return Play("far_atk_1_end", t - far1EndAt, false);
        if (!hideStealth && hideOutAt >= 0f && t >= hideOutAt && t - hideOutAt < OutSec)
            return Play("out", t - hideOutAt, false);

        // 有 1A/1B 的谱：1A 之前、1L/1B 之后本体不在场。弹幕/近战/连打不能把 x01 召回来。
        if (hidden) return Hide();
        if (leaveAt >= 0f)
        {
            var meleeLeave = MeleeAt(t);
            if (meleeLeave.Visible) return meleeLeave;
            var origin = OutFrom(leaveAt);
            var u = t - origin;
            if (u < OutSec) return Play("out", u, false);
            return Hide();
        }
        if (HasEnterScript && !shown) return Hide();
        if (LeftAfterMash(t)) return Hide();

        var melee = MeleeAt(t);
        if (melee.Visible) return melee;

        var shot = ShotAt(t);
        if (shot.Visible) return shot;

        var inSpan = InSpan(t);
        if (!shown && !inSpan) return Hide();

        var inFrom = EnterInFrom(enterAt);
        if (shown && t >= inFrom && t - inFrom < InSec)
            return Play("in", t - inFrom, false);

        if (phaseAt >= 0f && t - phaseAt < PhaseSec)
            return Play(phase == 2 ? "atk_1_to_2" : "atk_2_to_1", t - phaseAt, false);

        if (far2 && farCross)
        {
            // 1F 后面马上还有 1E：播 far_atk_2_end，下一拍 1E 再进 far_atk_2_start。不是切到 far_atk_1。
            return Play("far_atk_2_end", t - farCrossAt, false);
        }
        if (far2)
        {
            // 每一拍 1E 重播进场。far_atk_2 是弹幕攻击，这段没有 14/15，不要混进来。
            var u = t - far2At;
            if (u < StartSec) return Play("far_atk_2_start", u, false);
            return Play("far_atk_2_standby", u - StartSec, true);
        }
        if (far1)
        {
            var u = t - far1At;
            if (u < StartSec) return Play("far_atk_1_start", u, false);
            return Play("far_atk_1_standby", u - StartSec, true);
        }

        if (!shown && inSpan)
        {
            if (HasEnterScript) return Hide();
            return Play("standby", t, true);
        }
        if (!shown) return Hide();
        return Play("standby", Math.Max(0f, t - enterAt - InSec), true);
    }

    bool HasEnterScript
    {
        get
        {
            foreach (var c in Cues)
            {
                if (c.Id is "1A" or "1B") return true;
            }
            return false;
        }
    }

    View MeleeAt(float t)
    {
        PlayNote? best = null;
        var bestStart = float.MinValue;
        foreach (var n in Attacks)
        {
            if (!IsMelee(n.BmsId)) continue;
            var hit0 = (float)n.TimeSec;
            var wind = n.BmsId == "12" ? Close2Hit : Close1Hit;
            var dur = n.BmsId == "12" ? Close2Dur : Close1Dur;
            var start = hit0 - wind;
            var end = start + dur;
            var farAt = FarAfterMelee(hit0);
            if (MeleeLeaves(hit0)) end = start + dur + OutSec;
            else if (farAt > 0f) end = farAt - 0.001f;
            if (t < start || t > end) continue;
            if (start >= bestStart) { bestStart = start; best = n; }
        }
        if (best == null) return Hide();
        var hit = (float)best.TimeSec;
        var dash = best.BmsId == "12" ? "close_atk_2_24" : "close_atk_1_48";
        if (t < hit)
            return new View { Visible = true, Clip = dash, Time = t - bestStart, Loop = false, CountsHit = false, MashId = best.Id };
        // 打中由 PlayDirector 播 hurt 击退。漏接才把 hurt 换回 close_atk 续播。
        // 后面有 1B：击退仍要播，片段结束再 out，不要为了离场把 hurt 整段换成斩击。
        // 后面有 1E 时窗口拉到远攻开始，人留在轨道上，不要在冲出左缘后直接藏掉。
        if (MeleeLeaves(hit))
        {
            var clipEnd = bestStart + (best.BmsId == "12" ? Close2Dur : Close1Dur);
            if (t < clipEnd)
                return new View { Visible = true, Clip = "hurt", Time = t - hit, Loop = false, CountsHit = false, MashId = best.Id };
            return new View { Visible = true, Clip = "out", Time = t - clipEnd, Loop = false, CountsHit = false, MashId = best.Id };
        }
        return new View { Visible = true, Clip = "hurt", Time = t - hit, Loop = false, CountsHit = false, MashId = best.Id };
    }

    /// <summary>命中后 1.5s 内的 1B 才算这次近战的退场。1E 是远程，不在这里退场。</summary>
    bool MeleeLeaves(float hit)
    {
        foreach (var c in Cues)
        {
            if (c.T < hit - 1e-3f) continue;
            if (c.T - hit > 1.5f) return false;
            if (c.Id == "1B") return !FalseLeave(c);
            if (c.Id is "1A" or "1C" or "1E" or "1L") return false;
        }
        return false;
    }

    /// <summary>近战后 3.2s 内第一件有效脚本是 1E：接着播远程2，不回待机。</summary>
    float FarAfterMelee(float hit)
    {
        foreach (var c in Cues)
        {
            if (c.T < hit - 1e-3f) continue;
            if (c.T - hit > 3.2f) return -1f;
            if (c.Id == "1B")
            {
                if (FalseLeave(c)) continue;
                return -1f;
            }
            if (c.Id is "1A" or "1C" or "1L") return -1f;
            if (c.Id == "1E") return c.T;
        }
        return -1f;
    }

    /// <summary>近战引出的 1B：退场计时从命中开始，避免 1B 再把 out 从待机姿势重播。</summary>
    float OutFrom(float leaveAt)
    {
        var start = leaveAt;
        foreach (var n in Attacks)
        {
            if (!IsMelee(n.BmsId)) continue;
            var hit = (float)n.TimeSec;
            if (hit <= leaveAt && leaveAt - hit <= 1.5f && MeleeLeaves(hit))
                start = Math.Min(start, hit);
        }
        return start;
    }

    /// <summary>
    /// 16：multi_atk_48 → hurt → multi_atk_out 回到右侧，人留在场上。
    /// 17 有连打长度：没有 48 的前置。判定瞬间切到 multi_atk_hurt（骨已在连打位，看起来是瞬移），
    /// 打完接 multi_atk_hurt_end 从连打位往右离场。不要接 multi_atk_48_end 第 0 帧，那是右侧蓄力，会像退场后又出场。
    /// 17 没有连打长度：multi_atk_hurt_end，瞬间突击并且自己离场。
    /// </summary>
    View MashAt(float t)
    {
        PlayNote? best = null;
        var bestStart = float.MinValue;
        foreach (var n in Attacks)
        {
            if (!IsBossMash(n.BmsId)) continue;
            var hit0 = (float)n.TimeSec;
            var longLeave = n.BmsId == "17" && n.LengthSec > 0.02m;
            var instant = n.BmsId == "17" && n.LengthSec <= 0.02m;
            var start = longLeave || instant ? hit0 : hit0 - Close1Hit;
            var end = (float)n.EndSec + MashTail(n);
            if (t < start || t > end) continue;
            if (start >= bestStart) { bestStart = start; best = n; }
        }
        if (best == null) return Hide();
        var hit = (float)best.TimeSec;
        var done = (float)best.EndSec;
        if (best.BmsId == "17" && best.LengthSec <= 0.02m)
            return Mash(best, "multi_atk_hurt_end", t - hit, false);
        if (best.BmsId == "17")
        {
            if (t <= done)
                return Mash(best, "multi_atk_hurt", t - hit, true);
            return Mash(best, "multi_atk_hurt_end", t - done, false);
        }
        if (t < hit)
            return Mash(best, "multi_atk_48", t - (hit - Close1Hit), false);
        if (t <= done)
            return Mash(best, "multi_atk_hurt", t - hit, true);
        return Mash(best, "multi_atk_out", t - done, false);
    }

    /// <summary>
    /// dashMid 是 X 骨突击位移走完一半的片内时间。连打结束瞬间对准这一帧。
    /// </summary>
    public static float MashEndClock(float dashMid, float timeFromEnd)
    {
        var lead = dashMid - MashEndSight;
        if (lead < 0f) lead = 0f;
        return timeFromEnd + lead;
    }

    static float MashTail(PlayNote n)
    {
        if (n.BmsId == "17") return HurtEndSec;
        return MashOutSec;
    }

    /// <summary>
    /// 17 连击后退场播完，直到退场动画结束之后的 1A 才回场。
    /// hurt_end 期间或紧挨着的 1A/1M 都不算（否则会看起来刚飞走又走进来）。
    /// </summary>
    bool LeftAfterMash(float t)
    {
        PlayNote? last = null;
        foreach (var n in Attacks)
        {
            if (n.BmsId != "17") continue;
            var at = (float)n.EndSec + MashTail(n);
            if (at <= t) last = n;
        }
        if (last == null) return false;
        var gone = (float)last.EndSec + MashTail(last);
        foreach (var c in Cues)
        {
            if (c.T > t) break;
            if (c.Id == "1A" && c.T >= gone) return false;
        }
        return true;
    }

    /// <summary>1F 后面很快又有 1E：先播 far_atk_2_end，下一拍再进 far_atk_2_start。不是换到 far_atk_1，也不是把 end 吞掉。</summary>
    bool FarContinues(Cue end, string startId)
    {
        foreach (var n in Cues)
        {
            if (n.T <= end.T + 1e-4f) continue;
            if (n.T - end.T > FarHoldGap) return false;
            if (n.Id == startId) return true;
            if (n.Id is "1A" or "1B" or "1L" or "1G" or "1H" or "11" or "12" or "16" or "17") return false;
        }
        return false;
    }

    /// <summary>1A 后立刻 1B 再 1M：显隐脚本，不是退场。</summary>
    bool FalseLeave(Cue leave)
    {
        Cue? prev = null;
        Cue? next = null;
        foreach (var c in Cues)
        {
            if (c.T < leave.T - 1e-4f) prev = c;
            else if (c.T > leave.T + 1e-4f)
            {
                next = c;
                break;
            }
        }
        return prev is { Id: "1A" } && leave.T - prev.Value.T < 1f
            && next is { Id: "1M" } && next.Value.T - leave.T < 3f;
    }

    static View Mash(PlayNote n, string clip, float time, bool loop)
        => new() { Visible = true, Clip = clip, Time = Math.Max(0f, time), Loop = loop, CountsHit = false, MashId = n.Id };

    /// <summary>弹幕飞近时本体播对轨远攻：13 → far_atk_1_air/road，14/15 → far_atk_2。不算击打。</summary>
    View ShotAt(float t)
    {
        PlayNote? best = null;
        var bestStart = float.MinValue;
        foreach (var n in Attacks)
        {
            if (!IsDanmaku(n.BmsId)) continue;
            var start = (float)n.TimeSec - Flight.DurationSec(n);
            var end = start + FireSec + FarEndSec;
            if (t < start || t > end) continue;
            if (start >= bestStart) { bestStart = start; best = n; }
        }
        if (best == null) return Hide();
        var u = t - bestStart;
        var throwClip = best.BmsId == "13"
            ? (best.Pathway == 1 ? "far_atk_1_air" : "far_atk_1_road")
            : "far_atk_2";
        if (u < FireSec) return Play(throwClip, u, false);
        // 1C/1E 还按着：投完回到远攻待机。far_atk_*_end 是 1D/1F 的离场，接在每发弹幕后面会把人按回原地待机。
        if (FarHeld(t)) return Hide();
        var endClip = best.BmsId == "13" ? "far_atk_1_end" : "far_atk_2_end";
        return Play(endClip, u - FireSec, false);
    }

    bool FarHeld(float t)
    {
        Fold(t, out _, out _, out _, out _, out _, out _,
            out _, out _, out var far1, out _, out var far2, out _,
            out _, out _, out _, out _);
        return far1 || far2;
    }

    /// <summary>1L 刚藏完，紧跟着的 1A+1B+1M 是同一段显隐，不要把 out 打断再走进来。</summary>
    bool HideClusterEnter(Cue enter)
    {
        Cue? prev = null;
        float b = -1f;
        foreach (var c in Cues)
        {
            if (c.T < enter.T - 1e-4f) prev = c;
            else if (c.T > enter.T + 1e-4f)
            {
                if (b < 0f)
                {
                    if (c.Id != "1B" || c.T - enter.T >= 1f) return false;
                    b = c.T;
                    continue;
                }
                return c.Id == "1M" && c.T - b < 3f;
            }
        }
        return false;
    }

    /// <summary>1L 后面那组 1A+1B+1M 的 1M 不要把刚离场的人再拉回来。</summary>
    bool HideClusterShow(Cue show)
    {
        Cue? link = null;
        Cue? enter = null;
        Cue? hide = null;
        foreach (var c in Cues)
        {
            if (c.T >= show.T - 1e-4f) break;
            if (c.Id == "1L") hide = c;
            if (c.Id == "1A") enter = c;
            if (c.Id == "1B") link = c;
        }
        return hide != null && enter != null && link != null
            && show.T - link.Value.T < 3f
            && link.Value.T - enter.Value.T < 1f
            && enter.Value.T - hide.Value.T < 2f;
    }

    void Fold(float t, out bool shown, out float enterAt, out float leaveAt, out bool hidden, out float hideOutAt, out bool hideStealth,
        out int phase, out float phaseAt, out bool far1, out float far1At, out bool far2, out float far2At,
        out bool farCross, out float farCrossAt, out float far1EndAt, out float far2EndAt)
    {
        shown = false;
        enterAt = 0f;
        leaveAt = -1f;
        hidden = false;
        hideOutAt = -1f;
        hideStealth = false;
        phase = 1;
        phaseAt = -1f;
        far1 = false;
        far1At = 0f;
        far2 = false;
        far2At = 0f;
        farCross = false;
        farCrossAt = -1f;
        far1EndAt = -1f;
        far2EndAt = -1f;
        var hasScript = Cues.Count > 0;
        if (!hasScript) return;

        foreach (var c in Cues)
        {
            if (c.T > t) break;
            switch (c.Id)
            {
                case "1A":
                    if (hidden && HideClusterEnter(c)) break;
                    shown = true;
                    hidden = false;
                    leaveAt = -1f;
                    enterAt = c.T;
                    break;
                case "1B":
                    if (FalseLeave(c)) break;
                    leaveAt = c.T;
                    shown = false;
                    far1 = far2 = false;
                    break;
                case "1C":
                    if (!far1) far1At = c.T;
                    far1 = true;
                    far2 = false;
                    far1EndAt = far2EndAt = -1f;
                    // 不解除 1L/1B：官谱藏 Boss 时仍可丢 1E 弹幕，本体不在场。
                    if (!hidden && leaveAt < 0f) shown = true;
                    break;
                case "1D":
                    if (FarContinues(c, "1C")) break;
                    far1EndAt = c.T;
                    far1 = false;
                    break;
                case "1E":
                    // 每一拍 1E 都从头切 far_atk_2_start，不要停在上一拍的姿势上。
                    far2At = c.T;
                    far2 = true;
                    far1 = false;
                    farCross = false;
                    far1EndAt = far2EndAt = -1f;
                    if (!hidden && leaveAt < 0f) shown = true;
                    break;
                case "1F":
                    if (FarContinues(c, "1E"))
                    {
                        farCross = true;
                        farCrossAt = c.T;
                        break;
                    }
                    farCross = false;
                    far2EndAt = c.T;
                    far2 = false;
                    break;
                case "1G":
                    // 切远程2。不要先播 far_atk_1_end：那是回原地待机。过渡后停在远攻2，直到 1F/1H。
                    phase = 2;
                    phaseAt = c.T;
                    far1 = false;
                    far1EndAt = -1f;
                    far2 = true;
                    farCross = false;
                    far2EndAt = -1f;
                    far2At = c.T;
                    if (!hidden && leaveAt < 0f) shown = true;
                    break;
                case "1H":
                    phase = 1;
                    phaseAt = c.T;
                    far2 = false;
                    far2EndAt = -1f;
                    far1 = true;
                    farCross = false;
                    far1EndAt = -1f;
                    far1At = c.T;
                    if (!hidden && leaveAt < 0f) shown = true;
                    break;
                case "1L":
                    var fromFar = far1 || far2
                        || (far1EndAt >= 0f && c.T - far1EndAt < FarEndSec + 0.5f)
                        || (far2EndAt >= 0f && c.T - far2EndAt < FarEndSec + 0.5f);
                    if (far1 && far1EndAt < 0f) far1EndAt = c.T;
                    if (far2 && far2EndAt < 0f) far2EndAt = c.T;
                    var endFirst = (far1EndAt >= 0f && c.T - far1EndAt < 1e-3f)
                        || (far2EndAt >= 0f && c.T - far2EndAt < 1e-3f);
                    far1 = far2 = false;
                    hidden = true;
                    hideStealth = fromFar;
                    // 远攻后的 1L：收尾已经在 1F 播了。这一拍只消失，不接冲刺，也不接 out。
                    hideOutAt = fromFar ? -1f : (endFirst ? c.T + FarEndSec : c.T);
                    break;
                case "1M":
                    // 1L 后复现。1B/17 退场后不要因 1M 自动回场，只认 1A。
                    if (leaveAt >= 0f) break;
                    if (hidden && HideClusterShow(c)) break;
                    hidden = false;
                    shown = true;
                    if (enterAt <= 0f) enterAt = c.T;
                    break;
            }
        }
    }

    bool InSpan(float t)
    {
        foreach (var s in Spans)
        {
            if (t < s.Start) return false;
            if (t <= s.End) return true;
        }
        return false;
    }

    /// <summary>1A 若落在 17 退场动画里，入场从 hurt_end 结束再走，避免从待机点瞬移。</summary>
    float EnterInFrom(float enterAt)
    {
        var start = enterAt;
        foreach (var n in Attacks)
        {
            if (n.BmsId != "17") continue;
            if ((float)n.TimeSec >= enterAt) continue;
            var gone = (float)n.EndSec + MashTail(n);
            if (gone > start) start = gone;
        }
        return start;
    }

    static View Hide() => new() { Visible = false, Clip = "", Time = 0, Loop = false, CountsHit = false };

    static View Play(string clip, float time, bool loop)
        => new() { Visible = true, Clip = clip, Time = Math.Max(0f, time), Loop = loop, CountsHit = false };
}
