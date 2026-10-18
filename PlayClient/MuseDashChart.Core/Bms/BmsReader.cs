namespace MuseDashChart;

public readonly record struct RawBmsNote(float Time, string Value, string Channel);

public sealed class ParsedBms
{
    public BmsHeader Info { get; set; } = new();
    public List<RawBmsNote> Notes { get; set; } = new();
    public List<BpmChange> BpmChanges { get; set; } = new();
    public List<TimeSigChange> TimeSigs { get; set; } = new();
    public Dictionary<string, float> BpmTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Warnings { get; set; } = new();
}

public sealed class BmsHeader
{
    public string Genre { get; set; } = "scene_01";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string LevelDesign { get; set; } = "";
    public float Bpm { get; set; }
    public int Player { get; set; } = 1;
    public int Rank { get; set; }
    public int LnType { get; set; } = 1;
    public string PlayLevel { get; set; } = "1";
    public string Banner { get; set; } = "";
}

public static class BmsReader
{
    public static ParsedBms Load(Stream stream)
    {
        var bpmDict = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var timeSigEntries = new Dictionary<int, BmsTime.TimeSigEntry>();
        var timeSigs = new List<TimeSigChange>();
        var bpmEntries = new List<BmsTime.BpmEntry>();
        var bpmChanges = new List<BpmChange>();
        var rawNotes = new List<RawBmsNote>();
        var warnings = new List<string>();
        var info = new BmsHeader();

        using var reader = new StreamReader(stream);
        while (reader.ReadLine()?.Trim() is { } line)
        {
            if (string.IsNullOrEmpty(line) || !line.StartsWith('#'))
                continue;
            line = line[1..];

            if (line.Contains(' '))
            {
                var spaceIndex = line.IndexOf(' ');
                var key = line[..spaceIndex];
                var value = line[(spaceIndex + 1)..];
                if (key.StartsWith("WAV", StringComparison.OrdinalIgnoreCase))
                    continue;
                ParseHeader(info, key, value, bpmDict, bpmEntries, bpmChanges);
            }
            else if (line.Contains(':'))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 5)
                    continue;
                var key = line[..colonIndex];
                var value = line[(colonIndex + 1)..];
                if (!ParseUtil.TryInt(key[..3], out var beat))
                    continue;
                var typeCode = key.Substring(3, 2);
                var known = BmsTables.Channels.TryGetValue(typeCode, out var type);
                if (!known)
                    type = ChannelType.None;

                if (known && type is ChannelType.SpTimesig)
                {
                    if (ParseUtil.TryFloat(value, out var percent))
                    {
                        timeSigEntries[beat] = new BmsTime.TimeSigEntry(beat, percent);
                        timeSigs.Add(new TimeSigChange { Measure = beat, Percent = percent });
                    }
                    else
                        warnings.Add($"拍号无法解析 #{key}:{value}");
                }
                else if (known && type is ChannelType.SpBpmDirect or ChannelType.SpBpmLookup)
                {
                    ParseBpmChannel(
                        beat, value, type, typeCode, bpmDict, bpmEntries, bpmChanges, warnings);
                }
                else
                {
                    if (value.Length % 2 != 0)
                    {
                        warnings.Add($"通道 {typeCode} 小节 {beat} 长度为奇数，已跳过");
                        continue;
                    }

                    if (!known)
                        warnings.Add($"未识别通道 {typeCode}（小节 {beat}），已保留");

                    var objLength = value.Length / 2;
                    for (var i = 0; i < objLength; i++)
                    {
                        var note = value.Substring(i * 2, 2);
                        if (note is "00")
                            continue;
                        var tick = (float)i / objLength + beat;
                        rawNotes.Add(new RawBmsNote(
                            BmsTime.CalculateNoteTime(tick, bpmEntries, timeSigEntries),
                            note,
                            typeCode));
                    }
                }
            }
        }

        rawNotes.Sort((l, r) =>
        {
            var lScore = (long)(l.Time * 1_000_000) * 10 + (l.Channel == "15" ? 0 : 1);
            var rScore = (long)(r.Time * 1_000_000) * 10 + (r.Channel == "15" ? 0 : 1);
            return lScore.CompareTo(rScore);
        });

        return new ParsedBms
        {
            Info = info,
            Notes = rawNotes,
            BpmChanges = bpmChanges,
            TimeSigs = timeSigs,
            BpmTable = bpmDict,
            Warnings = warnings
        };
    }

    public static ParsedBms LoadText(string text)
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        return Load(ms);
    }

    public static DifficultyChart ToDifficulty(ParsedBms bms)
    {
        var notes = bms.Notes;
        var skip = new bool[notes.Count];
        var lengths = new decimal[notes.Count];
        var chart = new DifficultyChart
        {
            BpmChanges = bms.BpmChanges.ToList(),
            TimeSigs = bms.TimeSigs.ToList(),
            BpmTable = new Dictionary<string, float>(bms.BpmTable, StringComparer.OrdinalIgnoreCase),
            Warnings = bms.Warnings.ToList()
        };

        // 物件速度档（0S 通道）单独抽出，其余音符照常处理。
        var speedMarkers = bms.Notes
            .Where(n => string.Equals(n.Channel, ApproachSpeed.Channel, StringComparison.OrdinalIgnoreCase))
            .Select(n => (Time: n.Time, Rate: ApproachSpeed.RateOf(n.Value)))
            .OrderBy(m => m.Time)
            .ToList();

        var groups = new Dictionary<(string Channel, string Value), List<int>>(PairKeyComparer.Instance);
        for (var i = 0; i < notes.Count; i++)
        {
            var note = notes[i];
            if (!BmsTables.IsHoldKey(note.Value))
                continue;
            BmsTables.Channels.TryGetValue(note.Channel, out var channelType);
            if (channelType.HasFlag(ChannelType.SpTapHolds))
                continue;
            if (!BmsTables.IsLongNoteChannel(note.Channel))
                continue;
            var key = (note.Channel, note.Value);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(i);
        }

        foreach (var (key, list) in groups)
        {
            list.Sort((a, b) =>
            {
                var cmp = notes[a].Time.CompareTo(notes[b].Time);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });
            for (var g = 0; g + 1 < list.Count; g += 2)
            {
                var start = list[g];
                var end = list[g + 1];
                lengths[start] = (decimal)notes[end].Time - (decimal)notes[start].Time;
                skip[end] = true;
            }

            if (list.Count % 2 == 1)
            {
                var last = list[^1];
                chart.Warnings.Add(
                    $"未配对长按 {key.Value} 通道 {key.Channel} t={notes[last].Time:0.###}");
            }
        }

        var id = 1;
        for (var i = 0; i < notes.Count; i++)
        {
            if (skip[i])
                continue;
            var note = notes[i];
            if (string.IsNullOrEmpty(note.Value))
                continue;
            if (string.Equals(note.Channel, ApproachSpeed.Channel, StringComparison.OrdinalIgnoreCase))
                continue;
            BmsTables.Channels.TryGetValue(note.Channel, out var channelType);

            var pathway = BmsTables.PathwayFromChannel(note.Channel);
            var isEventOnly = channelType.HasFlag(ChannelType.Scene)
                || (channelType.HasFlag(ChannelType.Event)
                    && !channelType.HasFlag(ChannelType.Ground)
                    && !channelType.HasFlag(ChannelType.Air));

            var length = lengths[i];
            if (channelType.HasFlag(ChannelType.SpTapHolds) && BmsTables.IsHoldKey(note.Value))
                length = BmsTables.TapHoldLength;

            // 点长 0F 写在 13/14 是长按残渣，官机不画；收进来会和同刻摆锤叠成怪物。
            if (note.Value == BmsTables.IdHold && !BmsTables.IsLongNoteChannel(note.Channel)
                && length <= BmsTables.TapHoldLength)
                continue;

            var kind = isEventOnly || !BmsTables.IsLaneNote(note.Value) ? "event" : "note";

            chart.Notes.Add(new Placement
            {
                Id = id++,
                TimeSec = decimal.Round((decimal)note.Time, 6),
                LengthSec = decimal.Round(length, 6),
                Pathway = pathway,
                BmsId = note.Value,
                Blood = channelType.HasFlag(ChannelType.SpBlood) && !BmsTables.IsHoldKey(note.Value),
                Kind = kind,
                Channel = note.Channel
            });
        }

        var markers = speedMarkers
            .Select(m => new ApproachSpeed.Marker(decimal.Round((decimal)m.Time, 6), m.Rate))
            .ToList();
        ApproachSpeed.ApplyMarkersToNotes(chart, markers);

        return chart;
    }

    private static void ParseBpmChannel(
        int beat,
        string value,
        ChannelType type,
        string typeCode,
        Dictionary<string, float> bpmDict,
        List<BmsTime.BpmEntry> bpmEntries,
        List<BpmChange> bpmChanges,
        List<string> warnings)
    {
        if (value.Length % 2 != 0)
        {
            warnings.Add($"BPM 通道 {typeCode} 小节 {beat} 长度为奇数，已跳过");
            return;
        }

        var objLength = value.Length / 2;
        for (var i = 0; i < objLength; i++)
        {
            var note = value.Substring(i * 2, 2);
            if (note is "00")
                continue;
            var tick = (float)i / objLength + beat;
            float bpmValue;
            if (type == ChannelType.SpBpmLookup)
            {
                if (!bpmDict.TryGetValue(note, out bpmValue))
                {
                    warnings.Add($"BPM 查表键 {note} 未定义（tick {tick:0.###}）");
                    continue;
                }
            }
            else
            {
                try { bpmValue = Convert.ToInt32(note, 16); }
                catch (FormatException)
                {
                    warnings.Add($"BPM 十六进制无法解析 {note}");
                    continue;
                }
            }

            if (bpmValue <= 0)
                continue;
            var freq = 60f / bpmValue * 4f;
            bpmEntries.Add(new BmsTime.BpmEntry(tick, freq));
            bpmChanges.Add(new BpmChange
            {
                Tick = tick,
                Bpm = bpmValue,
                Channel = typeCode,
                Key = note
            });
        }
    }

    private static bool ParseHeader(
        BmsHeader info,
        string key,
        string value,
        Dictionary<string, float> bpmDict,
        List<BmsTime.BpmEntry> bpmEntries,
        List<BpmChange> bpmChanges)
    {
        if (key.StartsWith("BPM", StringComparison.OrdinalIgnoreCase))
        {
            if (!ParseUtil.TryFloat(value, out var bpmValue))
                return true;
            var bpmKey = key.Length > 3 ? key[3..] : "00";
            bpmDict[bpmKey] = bpmValue;
            if (bpmKey is "" or "00")
            {
                info.Bpm = bpmValue;
                var freq = 60f / bpmValue * 4f;
                bpmEntries.Add(new BmsTime.BpmEntry(0f, freq));
                bpmChanges.Add(new BpmChange
                {
                    Tick = 0,
                    Bpm = bpmValue,
                    Channel = "",
                    Key = "00"
                });
            }

            return true;
        }

        switch (key.ToUpperInvariant())
        {
            case "PLAYER":
                if (ParseUtil.TryInt(value, out var player)) info.Player = player;
                return true;
            case "GENRE":
                info.Genre = value;
                return true;
            case "TITLE":
                info.Title = value;
                return true;
            case "ARTIST":
                info.Artist = value;
                return true;
            case "LEVELDESIGN":
                info.LevelDesign = value;
                return true;
            case "BANNER":
                info.Banner = value;
                return true;
            case "RANK":
                if (ParseUtil.TryInt(value, out var rank)) info.Rank = rank;
                return true;
            case "LNTYPE":
                if (ParseUtil.TryInt(value, out var lnType)) info.LnType = lnType;
                return true;
            case "PLAYLEVEL":
                info.PlayLevel = value;
                return true;
            default:
                return false;
        }
    }

    private sealed class PairKeyComparer : IEqualityComparer<(string Channel, string Value)>
    {
        public static readonly PairKeyComparer Instance = new();

        public bool Equals((string Channel, string Value) x, (string Channel, string Value) y)
            => string.Equals(x.Channel, y.Channel, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Value, y.Value, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Channel, string Value) obj)
            => HashCode.Combine(
                obj.Channel.ToUpperInvariant(),
                obj.Value.ToUpperInvariant());
    }
}

