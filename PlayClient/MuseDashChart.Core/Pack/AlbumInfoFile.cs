using System.Text.Json.Serialization;

namespace MuseDashChart;

/// <summary>CustomAlbums Data/AlbumInfo 子集，大小写不敏感反序列化。</summary>
public sealed class AlbumInfoFile
{
    public string Name { get; set; } = "";

    [JsonPropertyName("name_romanized")]
    public string NameRomanized { get; set; } = "";

    public string Author { get; set; } = "";
    public string LevelDesigner { get; set; } = "";
    public string Bpm { get; set; } = "120";
    public string Scene { get; set; } = "scene_01";
    public string Delay { get; set; } = "0";
    public const int MaxStar = 13;

    public static string ClampStarLevel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "0";
        var t = raw.Trim();
        if (!int.TryParse(t, out var n)) return t;
        return Math.Clamp(n, 0, MaxStar).ToString();
    }

    public string Difficulty1 { get; set; } = "0";
    public string Difficulty2 { get; set; } = "0";
    public string Difficulty3 { get; set; } = "0";
    public string Difficulty4 { get; set; } = "0";
    public string Difficulty5 { get; set; } = "0";
    public string UnlockLevel { get; set; } = "0";
    public string[] SearchTags { get; set; } = [];
    public string LevelDesigner1 { get; set; } = "";
    public string LevelDesigner2 { get; set; } = "";
    public string LevelDesigner3 { get; set; } = "";
    public string LevelDesigner4 { get; set; } = "";
    public string LevelDesigner5 { get; set; } = "";

    public static AlbumInfoFile FromMeta(AlbumMeta meta) => new()
    {
        Name = meta.Name,
        NameRomanized = meta.NameRomanized,
        Author = meta.Author,
        LevelDesigner = meta.LevelDesigner,
        Bpm = meta.Bpm,
        Scene = meta.Scene,
        Delay = BmsTime.FormatDelay(
            BmsTime.ParseDelayText(meta.DelaySec, (float)meta.BpmValue), (float)meta.BpmValue),
        Difficulty1 = ClampStarLevel(meta.Difficulty1),
        Difficulty2 = ClampStarLevel(meta.Difficulty2),
        Difficulty3 = ClampStarLevel(meta.Difficulty3),
        Difficulty4 = ClampStarLevel(meta.Difficulty4),
        Difficulty5 = ClampStarLevel(meta.Difficulty5),
        UnlockLevel = meta.UnlockLevel,
        SearchTags = meta.SearchTags,
        LevelDesigner1 = meta.LevelDesigner1,
        LevelDesigner2 = meta.LevelDesigner2,
        LevelDesigner3 = meta.LevelDesigner3,
        LevelDesigner4 = meta.LevelDesigner4,
        LevelDesigner5 = meta.LevelDesigner5
    };

    public void ApplyTo(AlbumMeta meta)
    {
        meta.Name = Name;
        meta.NameRomanized = NameRomanized;
        meta.Author = Author;
        meta.LevelDesigner = LevelDesigner;
        meta.Bpm = Bpm;
        meta.Scene = Scene;
        meta.DelaySec = BmsTime.FormatDelay(
            BmsTime.ParseDelayText(Delay, (float)meta.BpmValue), (float)meta.BpmValue);
        meta.Difficulty1 = Difficulty1;
        meta.Difficulty2 = Difficulty2;
        meta.Difficulty3 = Difficulty3;
        meta.Difficulty4 = Difficulty4;
        meta.Difficulty5 = Difficulty5;
        meta.UnlockLevel = UnlockLevel ?? "0";
        meta.SearchTags = SearchTags ?? [];
        meta.LevelDesigner1 = LevelDesigner1;
        meta.LevelDesigner2 = LevelDesigner2;
        meta.LevelDesigner3 = LevelDesigner3;
        meta.LevelDesigner4 = LevelDesigner4;
        meta.LevelDesigner5 = LevelDesigner5;
    }

    public IEnumerable<int> ActiveDifficulties()
    {
        if (Difficulty1 != "0") yield return 1;
        if (Difficulty2 != "0") yield return 2;
        if (Difficulty3 != "0") yield return 3;
        if (Difficulty4 != "0") yield return 4;
        if (Difficulty5 != "0") yield return 5;
    }

    public void SetDifficultyLevel(int difficulty, string level)
    {
        switch (difficulty)
        {
            case 1: Difficulty1 = level; break;
            case 2: Difficulty2 = level; break;
            case 3: Difficulty3 = level; break;
            case 4: Difficulty4 = level; break;
            case 5: Difficulty5 = level; break;
            default: throw new ArgumentOutOfRangeException(nameof(difficulty));
        }
    }

    public string GetDifficultyLevel(int difficulty) => difficulty switch
    {
        1 => Difficulty1,
        2 => Difficulty2,
        3 => Difficulty3,
        4 => Difficulty4,
        5 => Difficulty5,
        _ => "0"
    };

    public void SyncFromCharts(IReadOnlyDictionary<int, DifficultyChart> charts)
    {
        for (var i = 1; i <= 5; i++)
        {
            if (charts.ContainsKey(i))
            {
                if (GetDifficultyLevel(i) == "0")
                    SetDifficultyLevel(i, "1");
            }
        }
    }
}

