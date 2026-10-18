using System.Text.Json.Serialization;

namespace MuseDashChart;

public sealed class ChartDocument
{
    public const string SchemaId = "md-chart-p0/1";

    public string Schema { get; set; } = SchemaId;
    public AlbumMeta Meta { get; set; } = new();

    /// <summary>键为难度 1–5，与 CustomAlbums map{n}.bms 对应。</summary>
    public Dictionary<int, DifficultyChart> Charts { get; set; } = new();

    public DifficultyChart EnsureChart(int difficulty)
    {
        if (difficulty is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficulty));
        if (!Charts.TryGetValue(difficulty, out var chart))
        {
            chart = new DifficultyChart();
            Charts[difficulty] = chart;
        }

        return chart;
    }
}

public sealed class AlbumMeta
{
    public string Name { get; set; } = "Untitled";
    public string NameRomanized { get; set; } = "";
    public string Author { get; set; } = "";
    public string LevelDesigner { get; set; } = "";
    public string Bpm { get; set; } = "120";
    public string Scene { get; set; } = "scene_01";
    public int PlayerSpeed { get; set; } = 1;
    public string DelaySec { get; set; } = "0";
    public string Difficulty1 { get; set; } = "1";
    public string Difficulty2 { get; set; } = "0";
    public string Difficulty3 { get; set; } = "0";
    public string Difficulty4 { get; set; } = "0";
    public string Difficulty5 { get; set; } = "0";
    public string MusicFile { get; set; } = "music.ogg";
    public string DemoFile { get; set; } = "demo.ogg";
    public string CoverFile { get; set; } = "cover.png";
    public string UnlockLevel { get; set; } = "0";
    public string[] SearchTags { get; set; } = [];
    public string LevelDesigner1 { get; set; } = "";
    public string LevelDesigner2 { get; set; } = "";
    public string LevelDesigner3 { get; set; } = "";
    public string LevelDesigner4 { get; set; } = "";
    public string LevelDesigner5 { get; set; } = "";

    [JsonIgnore]
    public decimal BpmValue => ParseUtil.TryDecimal(Bpm, out var v) && v > 0 ? v : 120m;
}

public sealed class BpmChange
{
    /// <summary>小节单位（与 BMS tick 相同）。</summary>
    public float Tick { get; set; }

    public float Bpm { get; set; }

    /// <summary>空=头 #BPM；03=直接十六进制；08=查表。</summary>
    public string Channel { get; set; } = "";

    /// <summary>查表键或 03 通道的两位十六进制。</summary>
    public string Key { get; set; } = "";
}

public sealed class TimeSigChange
{
    public int Measure { get; set; }
    public float Percent { get; set; } = 1f;
}

public sealed class DifficultyChart
{
    public List<Placement> Notes { get; set; } = new();
    public List<BpmChange> BpmChanges { get; set; } = new();
    public List<TimeSigChange> TimeSigs { get; set; } = new();
    public Dictionary<string, float> BpmTable { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public List<string> Warnings { get; set; } = new();

    public void RenumberIds()
    {
        for (var i = 0; i < Notes.Count; i++)
            Notes[i].Id = i + 1;
    }
}

public sealed class Placement
{
    public int Id { get; set; }
    public decimal TimeSec { get; set; }
    public decimal LengthSec { get; set; }
    public int Pathway { get; set; }
    public string BmsId { get; set; } = BmsTables.IdSmall;
    public bool Blood { get; set; }

    /// <summary>物件飞近速度档（对应官方 NoteConfigData.speed 1/2/3）。1=默认。</summary>
    public int ApproachRate { get; set; } = 1;

    /// <summary>为真时本键钉死 ApproachRate，不跟全局 0S / 0Y/0Z。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ApproachOverride { get; set; }

    /// <summary>原始 BMS 通道（13/14/53/54 等）。空则按玩法默认写出。</summary>
    public string Channel { get; set; } = "";

    /// <summary>note = 可玩键；event = 事件轨（流速/场景等）。</summary>
    public string Kind { get; set; } = "note";

    [JsonIgnore]
    public bool IsHoldLike =>
        LengthSec > 0 || BmsTables.IsHoldKey(BmsId);

    public Placement Clone() => new()
    {
        Id = Id,
        TimeSec = TimeSec,
        LengthSec = LengthSec,
        Pathway = Pathway,
        BmsId = BmsId,
        Blood = Blood,
        ApproachRate = ApproachRate,
        ApproachOverride = ApproachOverride,
        Channel = Channel,
        Kind = Kind
    };
}

