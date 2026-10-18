namespace MuseDashChart;

/// <summary>
/// BMS 拍号时间轴。与 CustomAlbums.BmsLoader.CalculateNoteTime 对齐：
/// 一个 4/4 小节时长 freq = 60/BPM*4 = 240/BPM 秒；tick 单位是小节。
/// </summary>
public static class BmsTime
{
    public readonly record struct BpmEntry(float Tick, float Freq) : IComparable<BpmEntry>
    {
        public int CompareTo(BpmEntry other) => Tick.CompareTo(other.Tick);
    }

    public readonly record struct TimeSigEntry(int Beat, float Percent);

    public static float SecondsPerMeasure(float bpm) => 60f / bpm * 4f;

    public static float TickFromSecondsConstantBpm(float timeSec, float bpm)
        => timeSec / SecondsPerMeasure(bpm);

    public static float SecondsFromTickConstantBpm(float tick, float bpm)
        => tick * SecondsPerMeasure(bpm);

    /// <summary>吸到每小节 192 整数槽（与 BMS 行长约定一致）。</summary>
    public static float SnapTo192(float timeSec, float bpm)
    {
        Split192(timeSec, bpm, out var measure, out var slot);
        return (measure + slot / 192f) * SecondsPerMeasure(bpm <= 0 ? 120f : bpm);
    }

    /// <summary>全局 192 槽序号：同拍变速、覆盖回贴都按这个比，不再用 20ms。</summary>
    public static int SlotIndex(float timeSec, float bpm)
    {
        Split192(timeSec, bpm, out var measure, out var slot);
        return measure * 192 + slot;
    }

    public static bool SameSlot(float a, float b, float bpm)
        => SlotIndex(a, bpm) == SlotIndex(b, bpm);

    public static float SlotDuration(float bpm)
        => SecondsPerMeasure(bpm <= 0 ? 120f : bpm) / 192f;

    public static int DelayToSlots(float delaySec, float bpm)
        => (int)MathF.Round(delaySec / SlotDuration(bpm));

    public static float SlotsToDelay(int slots, float bpm)
        => slots * SlotDuration(bpm);

    /// <summary>吃音：delay 只取整格。谱面时间戳始终是 0.0 对齐的 192 槽，不跟 delay 走。</summary>
    public static float SnapDelayToSlots(float delaySec, float bpm)
        => SlotsToDelay(DelayToSlots(delaySec, bpm), bpm);

    public static string FormatDelay(float delaySec, float bpm)
    {
        var snapped = SnapDelayToSlots(delaySec, bpm);
        if (MathF.Abs(snapped) < 1e-9f) return "0";
        if (MathF.Abs(snapped - MathF.Round(snapped)) < 1e-6f)
            return ((int)MathF.Round(snapped)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return snapped.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 纯整数且绝对值 ≤192（或带 +/格）当格子数；否则当秒再吸到整格。
    /// </summary>
    public static float ParseDelayText(string? text, float bpm)
    {
        var raw = (text ?? "").Trim().Replace(" ", "");
        if (raw.Length == 0) return 0f;
        var asSlots = raw.StartsWith('+') || raw.EndsWith("格") || raw.EndsWith("槽");
        raw = raw.TrimEnd('格', '槽');
        if (ParseUtil.TryInt(raw, out var slots) && (asSlots || raw.IndexOf('.') < 0 && Math.Abs(slots) <= 192))
            return SlotsToDelay(slots, bpm);
        if (ParseUtil.TryFloat(raw, out var sec))
            return SnapDelayToSlots(sec, bpm);
        return 0f;
    }

    /// <summary>打开/保存时把键和事件吸到 192 槽。Delay 不写进这些秒数。</summary>
    public static void SnapDocumentTo192(ChartDocument doc)
    {
        var bpm = (float)doc.Meta.BpmValue;
        if (bpm <= 0) return;
        foreach (var chart in doc.Charts.Values)
            SnapChartTo192(chart, bpm);
    }

    public static void SnapChartTo192(DifficultyChart chart, float bpm)
    {
        if (chart?.Notes == null || bpm <= 0) return;
        foreach (var n in chart.Notes)
        {
            var t0 = SnapTo192((float)n.TimeSec, bpm);
            if (n.LengthSec > 0)
            {
                var t1 = SnapTo192((float)(n.TimeSec + n.LengthSec), bpm);
                n.LengthSec = (decimal)MathF.Max(0f, t1 - t0);
            }
            n.TimeSec = (decimal)t0;
        }
    }

    static void Split192(float timeSec, float bpm, out int measure, out int slot)
    {
        if (bpm <= 0) bpm = 120f;
        if (timeSec <= 0)
        {
            measure = 0;
            slot = 0;
            return;
        }

        var tick = timeSec / SecondsPerMeasure(bpm);
        measure = (int)MathF.Floor(tick);
        slot = (int)MathF.Round((tick - measure) * 192f);
        if (slot >= 192)
        {
            measure++;
            slot = 0;
        }
    }

    public static List<BpmEntry> EntriesFrom(IReadOnlyList<BpmChange> changes, float fallbackBpm)
    {
        var list = new List<BpmEntry>();
        if (changes != null)
        {
            foreach (var change in changes.OrderBy(c => c.Tick))
            {
                if (change.Bpm <= 0) continue;
                list.Add(new BpmEntry(change.Tick, SecondsPerMeasure(change.Bpm)));
            }
        }

        if (list.Count == 0 || list[0].Tick > 0)
            list.Insert(0, new BpmEntry(0f, SecondsPerMeasure(fallbackBpm > 0 ? fallbackBpm : 120f)));
        return list;
    }

    public static Dictionary<int, TimeSigEntry> SigsFrom(IReadOnlyList<TimeSigChange> changes)
    {
        var dict = new Dictionary<int, TimeSigEntry>();
        if (changes == null) return dict;
        foreach (var change in changes)
            dict[change.Measure] = new TimeSigEntry(change.Measure, change.Percent);
        return dict;
    }

    /// <summary>秒 → tick，与 <see cref="CalculateNoteTime"/> 互逆（二分）。</summary>
    public static float TickFromSeconds(
        float timeSec,
        List<BpmEntry> bpmEntries,
        Dictionary<int, TimeSigEntry> timeSigEntries,
        float fallbackBpm = 120f)
    {
        if (timeSec <= 0) return 0f;
        if (bpmEntries == null || bpmEntries.Count == 0)
            return TickFromSecondsConstantBpm(timeSec, fallbackBpm);

        var lo = 0f;
        var hi = 1f;
        var guard = 0;
        while (CalculateNoteTime(hi, bpmEntries, timeSigEntries) < timeSec && guard++ < 32)
            hi *= 2f;

        for (var i = 0; i < 48; i++)
        {
            var mid = (lo + hi) * 0.5f;
            if (CalculateNoteTime(mid, bpmEntries, timeSigEntries) < timeSec)
                lo = mid;
            else
                hi = mid;
        }

        return (lo + hi) * 0.5f;
    }

    /// <summary>CustomAlbums 原实现对齐（float + 微秒四舍五入）。</summary>
    public static float CalculateNoteTime(float tick, List<BpmEntry> bpmEntries, Dictionary<int, TimeSigEntry> timeSigEntries)
    {
        var relevant = new List<BpmEntry>();
        foreach (var entry in bpmEntries)
        {
            if (entry.Tick < tick)
                relevant.Add(entry);
        }

        if (relevant.Count == 0) return 0f;
        relevant.Sort();

        var accumulatedTime = 0f;
        var totalBeatOffset = 0f;

        for (var j = 0; j < relevant.Count; j++)
        {
            var entry = relevant[j];
            var beatFrequency = entry.Freq;
            float segmentBeats = j < relevant.Count - 1
                ? relevant[j + 1].Tick - entry.Tick
                : tick - entry.Tick;

            var previousOffset = totalBeatOffset;
            totalBeatOffset += segmentBeats;
            var floorBeat = (int)MathF.Floor(previousOffset);
            var ceilBeat = (int)MathF.Ceiling(totalBeatOffset);

            for (var k = floorBeat; k < ceilBeat; k++)
            {
                var beatFraction = 1f;
                if (k == floorBeat)
                    beatFraction = k + 1 - previousOffset;
                if (k == ceilBeat - 1)
                    beatFraction = totalBeatOffset - (ceilBeat - 1);
                if (ceilBeat == floorBeat + 1)
                    beatFraction = totalBeatOffset - previousOffset;

                var timeSigMultiplier = timeSigEntries.TryGetValue(k, out var timeSig)
                    ? timeSig.Percent
                    : 1f;
                accumulatedTime +=
                    MathF.Round(beatFraction * timeSigMultiplier * beatFrequency / 1e-6f) * 1e-6f;
            }
        }

        return accumulatedTime;
    }
}

