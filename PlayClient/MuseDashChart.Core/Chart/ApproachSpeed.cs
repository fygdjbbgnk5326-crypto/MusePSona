namespace MuseDashChart;

/// <summary>
/// 物件飞近速度档（官方 NoteConfigData.speed 1/2/3），与画面「流速」ScrollSpeed 区分。
/// 官谱落在 uid 上；MDM 压成通道 0S（值 01/02/03）；编辑器用 0X/0Y/0Z 事件。
/// 档 1/2/3 行程相同只差时间：普通键判定帧 44/38/33（/30 s），速度比 1 : 1.158 : 1.333，不是 1 : 1.5 : 2。
/// </summary>
public static class ApproachSpeed
{
    public const string Channel = "0S";
    public const int DefaultRate = 1;

    public readonly record struct Marker(decimal TimeSec, int Rate);

    public static int Clamp(int rate) => rate switch
    {
        <= 1 => 1,
        2 => 2,
        _ => 3
    };

    /// <summary>1/2/3 → 两位 BMS 值 "01"/"02"/"03"。</summary>
    public static string Code(int rate)
        => Clamp(rate).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>"01"/"02"/"03" → 1/2/3；非法回退 1。</summary>
    public static int RateOf(string? code)
    {
        if (int.TryParse(code, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var n))
            return Clamp(n);
        return DefaultRate;
    }

    /// <summary>BMS 值 "01"/"02"/"03" 里，「03」及以上才算非默认（写档时据此裁剪默认档）。</summary>
    public static bool IsNonDefault(string? code) => RateOf(code) != DefaultRate;

    public static string Caption(int rate) => $"速度{Clamp(rate)}";

    /// <summary>编辑器里的变速事件 ibms 码（0X/0Y/0Z = 速度1/2/3）。</summary>
    public static bool IsApproachId(string? bmsId) => (bmsId ?? "").ToUpperInvariant() is "0X" or "0Y" or "0Z";

    public static bool IsApproachChannel(string? channel)
        => string.Equals(channel, Channel, StringComparison.OrdinalIgnoreCase);

    public static int RateOfBmsId(string? bmsId) => (bmsId ?? "").ToUpperInvariant() switch
    {
        "0Y" => 2,
        "0Z" => 3,
        _ => 1
    };

    public static string BmsIdOfRate(int rate) => Clamp(rate) switch
    {
        2 => "0Y",
        3 => "0Z",
        _ => "0X"
    };

    /// <summary>变速事件（0X/0Y/0Z，批量改档标记），按时间排序。</summary>
    public static IEnumerable<Placement> Markers(DifficultyChart chart)
        => chart.Notes
            .Where(n => IsApproachId(n.BmsId))
            .OrderBy(n => n.TimeSec).ThenBy(n => n.Id);

    /// <summary>逐音符速度的变化点（可玩键 ApproachRate 变化的那颗键），按时间排序。</summary>
    public static IEnumerable<Placement> Events(DifficultyChart chart)
    {
        var last = DefaultRate;
        foreach (var n in chart.Notes
                     .Where(IsPlayable)
                     .OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            var rate = Clamp(n.ApproachRate);
            if (rate == last)
                continue;
            yield return n;
            last = rate;
        }
    }

    public static bool IsPlayable(Placement n)
        => !IsApproachId(n.BmsId) && BmsTables.DrawsOnPathway(n);

    /// <summary>逐音符模型：每颗键用自身档（StampNotes 已把批量标记写进去）。不能用 At()——同刻混档时会取到别的键。</summary>
    public static int EffectiveRate(Placement n) => Clamp(n.ApproachRate);

    public static bool IsGhost(Placement n)
        => string.Equals(n.BmsId, BmsTables.IdGhost, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 可玩键 speed 序列 → 变化点（含回到 1）。与 official_to_mdm.speed_transitions 同一规则。
    /// 同刻多档先多数票再折叠，避免 last-wins 抖一次。
    /// </summary>
    public static List<Marker> FromPlayable(IEnumerable<(decimal TimeSec, int Rate)> playable)
    {
        var trans = new List<Marker>();
        var last = DefaultRate;
        foreach (var bucket in playable
                     .Select(p => (Time: p.TimeSec, Rate: Clamp(p.Rate)))
                     .GroupBy(p => GroupKey(p.Time, bpm: 0))
                     .OrderBy(g => g.Key))
        {
            var rate = MajorityRate(bucket.Select(x => x.Rate));
            if (rate == last) continue;
            trans.Add(new Marker(bucket.First().Time, rate));
            last = rate;
        }

        return trans;
    }

    /// <summary>自制包 0S：时间戳已是去掉抓取延迟的 192 槽；同槽幽灵让路，再多数票。</summary>
    public static List<Marker> FromPlayableNotes(
        IEnumerable<Placement> notes, Func<Placement, int>? rateOf = null, float bpm = 0)
    {
        rateOf ??= static n => n.ApproachRate;
        var trans = new List<Marker>();
        var last = DefaultRate;
        foreach (var bucket in notes
                     .Where(IsPlayable)
                     .GroupBy(n => GroupKey(n.TimeSec, bpm))
                     .OrderBy(g => g.Key))
        {
            var rate = PickBucketRate(bucket, rateOf);
            if (rate == last) continue;
            trans.Add(new Marker(CanonicalTime(bucket.First().TimeSec, bpm), rate));
            last = rate;
        }

        return trans;
    }

    static long GroupKey(decimal timeSec, float bpm)
        => bpm > 0
            ? BmsTime.SlotIndex((float)timeSec, bpm)
            : (long)(decimal.Round(timeSec, 6) * 1_000_000m);

    static decimal CanonicalTime(decimal timeSec, float bpm)
        => bpm > 0 ? (decimal)BmsTime.SnapTo192((float)timeSec, bpm) : timeSec;

    private static int PickBucketRate(IEnumerable<Placement> bucket, Func<Placement, int> rateOf)
    {
        var list = bucket as IList<Placement> ?? bucket.ToList();
        var music = list.Where(n => !IsGhost(n)).ToList();
        var pool = music.Count > 0 ? music : list;
        return MajorityRate(pool.Select(n => Clamp(rateOf(n))));
    }

    private static int MajorityRate(IEnumerable<int> rates)
        => rates.GroupBy(r => r)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .First()
            .Key;

    /// <summary>0S 台阶在某时刻的档；无变化点则 1。</summary>
    public static int RateAt(IReadOnlyList<Marker> marks, decimal timeSec)
    {
        var result = DefaultRate;
        foreach (var m in marks)
        {
            if (m.TimeSec <= timeSec)
                result = Clamp(m.Rate);
            else
                break;
        }

        return result;
    }

    /// <summary>某时刻的物件速度档（默认 1）。逐音符模型：取该时刻及之前最后一颗可玩键的档。</summary>
    public static int At(DifficultyChart chart, decimal timeSec)
    {
        var result = DefaultRate;
        foreach (var n in chart.Notes
                     .Where(IsPlayable)
                     .OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            if (n.TimeSec > timeSec)
                break;
            result = Clamp(n.ApproachRate);
        }

        return result;
    }

    /// <summary>
    /// 写出通道 0S 台阶：每颗可玩键的 ApproachRate（钉死用覆盖档，无则已 stamp 成全局档）。
    /// 相邻同档合并；同 192 槽幽灵让路。没有可玩键就不写 0S。
    /// </summary>
    public static List<Marker> ToBmsMarkers(DifficultyChart chart, float bpm = 0)
        => FromPlayableNotes(chart.Notes, bpm: bpm);

    /// <summary>同一 192 槽出现至少两档飞近（自制包 0S 只能留多数票）。</summary>
    public static int SameTickMixCount(DifficultyChart chart, float bpm = 0)
        => chart.Notes
            .Where(IsPlayable)
            .GroupBy(n => GroupKey(n.TimeSec, bpm))
            .Count(g => g.Select(n => Clamp(n.ApproachRate)).Distinct().Count() > 1);

    /// <summary>相对 0S 台阶仍不同的键（同槽输家），供 chart.json 回贴。</summary>
    public static IEnumerable<Placement> SidecarOverrides(DifficultyChart chart, float bpm = 0)
    {
        var marks = ToBmsMarkers(chart, bpm);
        foreach (var n in chart.Notes.Where(IsPlayable))
        {
            if (Clamp(n.ApproachRate) == RateAt(marks, CanonicalTime(n.TimeSec, bpm)))
                continue;
            yield return n;
        }
    }

    /// <summary>存盘用副本：只给 0S 吃不下的键留 ApproachOverride，不改编辑器内存。</summary>
    public static ChartDocument ProjectSidecar(ChartDocument src)
    {
        var dst = new ChartDocument { Schema = src.Schema, Meta = src.Meta };
        foreach (var (diff, chart) in src.Charts)
            dst.Charts[diff] = SidecarChart(chart, (float)src.Meta.BpmValue);
        return dst;
    }

    private static DifficultyChart SidecarChart(DifficultyChart chart, float bpm)
    {
        var keep = new HashSet<Placement>(SidecarOverrides(chart, bpm), RefEq<Placement>.Instance);
        return new DifficultyChart
        {
            Notes = chart.Notes.Select(n => CopyNote(n, keep.Contains(n))).ToList(),
            BpmChanges = chart.BpmChanges,
            TimeSigs = chart.TimeSigs,
            BpmTable = chart.BpmTable
        };
    }

    private static Placement CopyNote(Placement n, bool keepOverride)
        => new()
        {
            Id = n.Id,
            TimeSec = n.TimeSec,
            LengthSec = n.LengthSec,
            Pathway = n.Pathway,
            BmsId = n.BmsId,
            Blood = n.Blood,
            ApproachRate = n.ApproachRate,
            ApproachOverride = keepOverride && IsPlayable(n),
            Channel = n.Channel,
            Kind = n.Kind
        };

    /// <summary>把 0S 变化点还原成带时长的 0Y/0Z（01 只做上一段的结束，不生成 0X）。</summary>
    public static List<Placement> EventsFromMarkers(IReadOnlyList<Marker> markers, int startId = 1)
    {
        var notes = new List<Placement>();
        var id = startId;
        for (var i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            if (m.Rate == DefaultRate)
                continue;
            var end = i + 1 < markers.Count ? markers[i + 1].TimeSec : m.TimeSec;
            notes.Add(new Placement
            {
                Id = id++,
                TimeSec = decimal.Round(m.TimeSec, 6),
                LengthSec = decimal.Round(end > m.TimeSec ? end - m.TimeSec : 0m, 6),
                BmsId = BmsIdOfRate(m.Rate),
                Kind = "event",
                Channel = ""
            });
        }

        return notes;
    }

    /// <summary>从该时刻改档：裁掉覆盖中的旧事件；回到 1 只收口，不写 0X（否则存盘会变成 0S:01，再读就丢）。</summary>
    public static Placement? ApplyAt(DifficultyChart chart, decimal timeSec, int rate)
    {
        rate = Clamp(rate);
        chart.Notes.RemoveAll(n => IsApproachId(n.BmsId) && n.TimeSec == timeSec);
        foreach (var ev in Markers(chart).ToList())
        {
            var end = ev.LengthSec > 0 ? ev.TimeSec + ev.LengthSec : decimal.MaxValue;
            if (ev.TimeSec < timeSec && timeSec < end)
                ev.LengthSec = timeSec - ev.TimeSec;
        }

        if (rate == DefaultRate)
            return null;

        var note = new Placement
        {
            TimeSec = timeSec,
            LengthSec = 0,
            BmsId = BmsIdOfRate(rate),
            Kind = "event",
            Channel = ""
        };
        chart.Notes.Add(note);
        return note;
    }

    /// <summary>变速事件（批量改档标记）→ 逐音符速度。范围按 192 槽：起点含、终点不含。</summary>
    public static void StampNotes(DifficultyChart chart, float bpm = 0)
    {
        if (!Markers(chart).Any())
            return; // 没有批量改档标记时，逐音符速度就是源，别清零

        foreach (var n in chart.Notes.Where(IsPlayable))
            if (!n.ApproachOverride)
                n.ApproachRate = DefaultRate;

        foreach (var ev in Markers(chart).OrderBy(e => e.TimeSec))
        {
            var rate = RateOfBmsId(ev.BmsId);
            var end = ev.LengthSec > 0 ? ev.TimeSec + ev.LengthSec : decimal.MaxValue;
            foreach (var n in chart.Notes.Where(IsPlayable))
            {
                if (n.ApproachOverride) continue;
                if (InStampRange(ev.TimeSec, end, n.TimeSec, bpm))
                    n.ApproachRate = rate;
            }
        }
    }

    static bool InStampRange(decimal start, decimal end, decimal t, float bpm)
    {
        if (bpm <= 0)
            return start <= t && t < end;
        if (end >= decimal.MaxValue / 2)
            return BmsTime.SlotIndex((float)t, bpm) >= BmsTime.SlotIndex((float)start, bpm);
        var s = BmsTime.SlotIndex((float)t, bpm);
        return s >= BmsTime.SlotIndex((float)start, bpm) && s < BmsTime.SlotIndex((float)end, bpm);
    }

    /// <summary>0S 台阶 → 逐音符速度（导入用，不生成变速事件标记）。</summary>
    public static void ApplyMarkersToNotes(DifficultyChart chart, IReadOnlyList<Marker> markers)
    {
        foreach (var n in chart.Notes.Where(IsPlayable))
        {
            n.ApproachRate = RateAt(markers, n.TimeSec);
            n.ApproachOverride = false;
        }
    }

    /// <summary>检查器：0=无（不写单键事件），1/2/3=钉死该档。</summary>
    public static int InspectIndex(Placement n)
        => n.ApproachOverride ? Clamp(n.ApproachRate) : 0;

    /// <summary>无 → null；1/2/3 → 档位。</summary>
    public static int? RateFromInspectIndex(int index)
        => index <= 0 ? null : Clamp(index);

    /// <summary>钉死 1/2/3；与全局相同也算有飞近事件。</summary>
    public static void SetPlayableRate(DifficultyChart chart, Placement n, int rate)
    {
        if (!IsPlayable(n)) return;
        n.ApproachRate = Clamp(rate);
        n.ApproachOverride = true;
    }

    public static void InheritAt(DifficultyChart chart, Placement n)
    {
        if (!IsPlayable(n)) return;
        n.ApproachOverride = false;
        n.ApproachRate = At(chart, n.TimeSec);
    }

    public static int OverrideCount(DifficultyChart chart)
        => chart.Notes.Count(n => n.ApproachOverride && IsPlayable(n));

    /// <summary>
    /// 键位以 BMS 为准，把 chart.json 里的飞近覆盖贴回去。
    /// 有 BPM 时按同一 192 槽匹配；否则退回 20ms（旧包）。
    /// </summary>
    public static int ApplyOverrides(DifficultyChart target, DifficultyChart stored,
        decimal epsilonSec = 0.02m, float bpm = 0)
    {
        var unused = stored.Notes
            .Where(n => n.ApproachOverride && IsPlayable(n))
            .OrderBy(n => n.TimeSec)
            .ToList();
        if (unused.Count == 0) return 0;

        var applied = 0;
        foreach (var n in target.Notes.Where(IsPlayable).OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            var bestI = -1;
            var bestDt = bpm > 0 ? 0m : epsilonSec;
            for (var i = 0; i < unused.Count; i++)
            {
                var s = unused[i];
                if (s.Pathway != n.Pathway) continue;
                if (!string.Equals(s.BmsId, n.BmsId, StringComparison.OrdinalIgnoreCase)) continue;
                if (bpm > 0)
                {
                    if (!BmsTime.SameSlot((float)s.TimeSec, (float)n.TimeSec, bpm)) continue;
                    bestI = i;
                    break;
                }

                var dt = Math.Abs(s.TimeSec - n.TimeSec);
                if (dt > bestDt) continue;
                bestDt = dt;
                bestI = i;
            }

            if (bestI < 0) continue;
            var hit = unused[bestI];
            unused.RemoveAt(bestI);
            n.ApproachRate = Clamp(hit.ApproachRate);
            n.ApproachOverride = true;
            applied++;
        }

        return applied;
    }
}

