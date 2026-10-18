using MuseDashChart;

namespace MusePSona.Play;

public sealed class PlaySong
{
    public ChartDocument Document { get; }
    public string WorkingFolder { get; }
    public string? MdmPath { get; }
    public float DelaySec { get; }

    public PlaySong(ChartDocument document, string workingFolder, string? mdmPath)
    {
        Document = document;
        WorkingFolder = workingFolder;
        MdmPath = mdmPath;
        DelaySec = BmsTime.ParseDelayText(document.Meta.DelaySec, (float)document.Meta.BpmValue);
    }

    public static PlaySong Load(string path)
    {
        var r = ChartIo.Load(path);
        return new PlaySong(r.Document, r.WorkingFolder, r.OriginalMdm);
    }

    public string? MusicPath
    {
        get
        {
            foreach (var name in new[]
            {
                "music.ogg", "music.mp3", Document.Meta.MusicFile,
                "demo.ogg", "music.wav", "music.flac",
            })
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var p = Path.Combine(WorkingFolder, name);
                if (IsPlayableAudio(p)) return p;
            }
            return null;
        }
    }

    /// <summary>
    /// 导出占位 music.ogg 只有几个字节（不是 OggS），FMOD 会报 clip "" unsupported format。
    /// </summary>
    public static bool IsPlayableAudio(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 16) return false;
            var b = new byte[12];
            if (fs.Read(b, 0, 12) < 4) return false;
            if (b[0] == (byte)'O' && b[1] == (byte)'g' && b[2] == (byte)'g' && b[3] == (byte)'S')
                return true;
            if (b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F')
                return true;
            if (b[0] == (byte)'f' && b[1] == (byte)'L' && b[2] == (byte)'a' && b[3] == (byte)'C')
                return true;
            if (b[0] == (byte)'I' && b[1] == (byte)'D' && b[2] == (byte)'3')
                return true;
            if (b[0] == 0xFF && (b[1] & 0xE0) == 0xE0)
                return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    public int ResolveDifficulty(int requested)
    {
        if (HasPlayableMap(requested))
            return requested;
        foreach (var i in new[] { 3, 2, 4, 1, 5 })
            if (HasPlayableMap(i))
                return i;
        if (Document.Charts.ContainsKey(requested))
            return requested;
        if (Document.Charts.Count == 0)
            throw new InvalidOperationException("谱面里没有任何 map");
        return Document.Charts.Keys.OrderBy(k => k).First();
    }

    bool HasPlayableMap(int d)
    {
        if (!Document.Charts.ContainsKey(d)) return false;
        var star = Star(d);
        return !string.IsNullOrWhiteSpace(star) && star != "0";
    }

    public DifficultyChart Chart(int diff)
    {
        var use = ResolveDifficulty(diff);
        if (!Document.Charts.TryGetValue(use, out var c))
            throw new InvalidOperationException("没有 map" + use);
        return c;
    }

    public string Star(int diff) => diff switch
    {
        1 => Document.Meta.Difficulty1,
        2 => Document.Meta.Difficulty2,
        3 => Document.Meta.Difficulty3,
        4 => Document.Meta.Difficulty4,
        5 => Document.Meta.Difficulty5,
        _ => "0"
    };

    public IReadOnlyList<int> Maps
        => Document.Charts.Keys.OrderBy(k => k).ToList();

    public List<PlayNote> Playable(int diff)
        => Chart(diff).Notes.Where(n => n.Kind != "event" && BmsTables.DrawsOnPathway(n))
            .Select(PlayNote.From).ToList();
}
