namespace MuseDashChart;

public static class BeatPos
{
    public static string Short(decimal timeSec, decimal bpm)
    {
        Split(timeSec, bpm, out var measure, out var beat, out _);
        return $"{measure}.{beat}";
    }

    public static string Long(decimal timeSec, decimal bpm, int gridDivision)
    {
        var g = Math.Max(1, gridDivision);
        var beatSec = 60m / Math.Max(1m, bpm);
        var step = beatSec / g;
        if (step <= 0) return Short(timeSec, bpm);
        var slot = (int)decimal.Round(timeSec / step, MidpointRounding.AwayFromZero);
        if (slot < 0) slot = 0;
        var beatIndex = slot / g;
        var sub = slot % g;
        var measure = beatIndex / 4 + 1;
        var beat = beatIndex % 4 + 1;
        return sub == 0 ? $"{measure}.{beat}" : $"{measure}.{beat}.{sub}";
    }

    public static void Split(decimal timeSec, decimal bpm, out int measure, out int beat, out decimal beatFrac)
    {
        var beatSec = 60m / Math.Max(1m, bpm);
        var total = timeSec / beatSec;
        if (total < 0) total = 0;
        var m = (int)decimal.Truncate(total / 4m);
        var inBar = total - m * 4m;
        var b = (int)decimal.Truncate(inBar);
        measure = m + 1;
        beat = b + 1;
        beatFrac = inBar - b;
    }

    public static bool IsDoubleWith(Placement a, Placement b, float bpm = 0)
        => a.Id != b.Id
           && BmsTables.DrawsOnPathway(a)
           && BmsTables.DrawsOnPathway(b)
           && a.Pathway != b.Pathway
           && SameBeat(a.TimeSec, b.TimeSec, bpm);

    public static bool HasDouble(Placement note, IEnumerable<Placement> all, float bpm = 0)
        => all.Any(other => IsDoubleWith(note, other, bpm));

    public static int CountDoubles(IReadOnlyList<Placement> notes, float bpm = 0)
    {
        var list = notes.Where(BmsTables.DrawsOnPathway).OrderBy(n => n.TimeSec).ToList();
        var n = 0;
        var window = bpm > 0 ? (decimal)BmsTime.SlotDuration(bpm) * 2 : 0.02m;
        for (var i = 0; i < list.Count; i++)
        {
            for (var j = i + 1; j < list.Count; j++)
            {
                if (list[j].TimeSec - list[i].TimeSec > window) break;
                if (list[i].Pathway != list[j].Pathway && SameBeat(list[i].TimeSec, list[j].TimeSec, bpm))
                    n++;
            }
        }

        return n;
    }

    static bool SameBeat(decimal a, decimal b, float bpm)
        => bpm > 0
            ? BmsTime.SameSlot((float)a, (float)b, bpm)
            : Math.Abs(a - b) <= 0.02m;
}

