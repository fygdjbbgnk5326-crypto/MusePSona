using System.Globalization;
using System.Text;

namespace MuseDashChart;

public static class BmsWriter
{
    public const int SlotsPerMeasure = 192;

    public static string Write(DifficultyChart chart, AlbumMeta meta, string playLevel = "1")
    {
        var fallbackBpm = (float)meta.BpmValue;
        var headerBpm = HeaderBpm(chart, fallbackBpm);
        var bpmEntries = BmsTime.EntriesFrom(chart.BpmChanges, headerBpm);
        var timeSigs = BmsTime.SigsFrom(chart.TimeSigs);

        var sb = new StringBuilder();
        sb.AppendLine("#GENRE " + meta.Scene);
        sb.AppendLine("#TITLE " + meta.Name);
        sb.AppendLine("#ARTIST " + meta.Author);
        sb.AppendLine("#LEVELDESIGN " + meta.LevelDesigner);
        sb.AppendLine("#BPM " + ParseUtil.Flt(headerBpm));
        foreach (var (key, value) in LookupTable(chart).OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (key is "" or "00") continue;
            sb.Append("#BPM").Append(key.ToUpperInvariant()).Append(' ')
                .AppendLine(ParseUtil.Flt(value));
        }

        sb.AppendLine("#PLAYER " + meta.PlayerSpeed.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("#LNTYPE 1");
        sb.AppendLine("#PLAYLEVEL " + playLevel);
        sb.AppendLine();

        var cells = new Dictionary<(int Measure, string Channel), List<string[]>>();

        void Place(int measure, int slot, string channel, string value)
        {
            var key = (measure, channel);
            if (!cells.TryGetValue(key, out var rows))
            {
                rows = [];
                cells[key] = rows;
            }

            foreach (var row in rows)
            {
                if (row[slot] is "00" || row[slot] == value)
                {
                    row[slot] = value;
                    return;
                }
            }

            var neu = new string[SlotsPerMeasure];
            Array.Fill(neu, "00");
            neu[slot] = value;
            rows.Add(neu);
        }

        foreach (var change in chart.BpmChanges)
        {
            var channel = (change.Channel ?? "").ToUpperInvariant();
            if (channel is not ("03" or "08"))
                continue;
            var key = string.IsNullOrWhiteSpace(change.Key)
                ? DirectBpmKey(change.Bpm)
                : change.Key.ToUpperInvariant();
            if (string.IsNullOrEmpty(key)) continue;
            PlaceTick(change.Tick, channel, key, Place);
        }

        foreach (var note in chart.Notes.OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            if (ApproachSpeed.IsApproachId(note.BmsId)) continue; // 变速事件单独走 0S 通道
            var channel = BmsTables.ChannelFor(note);
            var tick = BmsTime.TickFromSeconds((float)note.TimeSec, bpmEntries, timeSigs, headerBpm);
            PlaceTick(tick, channel, note.BmsId, Place);
            if (note.IsHoldLike && note.LengthSec > BmsTables.HoldEndMinLength)
            {
                var endTick = BmsTime.TickFromSeconds(
                    (float)(note.TimeSec + note.LengthSec), bpmEntries, timeSigs, headerBpm);
                PlaceTick(endTick, channel, note.BmsId, Place);
            }
        }

        var stamped = new DifficultyChart
        {
            Notes = chart.Notes.Select(n => n.Clone()).ToList(),
            BpmChanges = chart.BpmChanges,
            TimeSigs = chart.TimeSigs,
            BpmTable = chart.BpmTable
        };
        ApproachSpeed.StampNotes(stamped, headerBpm);
        foreach (var mark in ApproachSpeed.ToBmsMarkers(stamped, headerBpm))
        {
            var tick = BmsTime.TickFromSeconds((float)mark.TimeSec, bpmEntries, timeSigs, headerBpm);
            PlaceTick(tick, ApproachSpeed.Channel, ApproachSpeed.Code(mark.Rate), Place);
        }

        var lines = new List<(int Measure, string Channel, int Overlay, string Body)>();
        foreach (var sig in chart.TimeSigs.OrderBy(s => s.Measure))
        {
            lines.Add((
                sig.Measure,
                "02",
                0,
                sig.Percent.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var ((measure, channel), rows) in cells)
        {
            for (var i = 0; i < rows.Count; i++)
                lines.Add((measure, channel, i, PackRow(rows[i])));
        }

        foreach (var line in lines
                     .OrderBy(l => l.Measure)
                     .ThenBy(l => l.Channel, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(l => l.Overlay))
        {
            sb.Append('#')
                .Append(line.Measure.ToString("000", CultureInfo.InvariantCulture))
                .Append(line.Channel)
                .Append(':')
                .Append(line.Body)
                .AppendLine();
        }

        return sb.ToString();
    }

    private static float HeaderBpm(DifficultyChart chart, float fallback)
    {
        var header = chart.BpmChanges.FirstOrDefault(c =>
            c.Tick == 0 && string.IsNullOrEmpty(c.Channel));
        if (header != null && header.Bpm > 0)
            return header.Bpm;
        var first = chart.BpmChanges.FirstOrDefault(c => c.Bpm > 0);
        return first != null ? first.Bpm : fallback;
    }

    private static Dictionary<string, float> LookupTable(DifficultyChart chart)
    {
        var table = new Dictionary<string, float>(
            chart.BpmTable ?? new Dictionary<string, float>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var change in chart.BpmChanges)
        {
            if (!string.Equals(change.Channel, "08", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(change.Key) || change.Key is "00")
                continue;
            if (!table.ContainsKey(change.Key))
                table[change.Key] = change.Bpm;
        }

        return table;
    }

    private static string DirectBpmKey(float bpm)
    {
        var n = (int)MathF.Round(bpm);
        if (n is < 1 or > 255) return "";
        return n.ToString("X2", CultureInfo.InvariantCulture);
    }

    private static void PlaceTick(
        float tick,
        string channel,
        string value,
        Action<int, int, string, string> place)
    {
        if (tick < 0) tick = 0;
        var measure = (int)MathF.Floor(tick);
        var frac = tick - measure;
        var slot = (int)MathF.Round(frac * SlotsPerMeasure);
        if (slot >= SlotsPerMeasure)
        {
            measure++;
            slot = 0;
        }

        place(measure, slot, channel, value);
    }

    internal static string PackRow(string[] row)
    {
        var used = new List<int>();
        for (var i = 0; i < row.Length; i++)
        {
            if (row[i] != "00")
                used.Add(i);
        }

        if (used.Count == 0)
            return "00";

        var width = row.Length;
        var gcd = width;
        foreach (var i in used)
            gcd = Gcd(gcd, i);
        if (gcd == 0) gcd = 1;
        var len = width / gcd;
        var chars = new char[len * 2];
        for (var i = 0; i < len; i++)
        {
            var src = i * gcd;
            var v = row[src];
            chars[i * 2] = v[0];
            chars[i * 2 + 1] = v[1];
        }

        return new string(chars);
    }

    private static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }

        return a == 0 ? 1 : a;
    }
}

