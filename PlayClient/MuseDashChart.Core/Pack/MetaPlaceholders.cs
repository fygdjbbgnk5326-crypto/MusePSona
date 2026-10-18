namespace MuseDashChart;

/// <summary>info 空缺字段用英文占位，场景/人物不自动填。</summary>
public static class MetaPlaceholders
{
    public const string Name = "Untitled";
    public const string Author = "Unknown";
    public const string Designer = "Unknown";

    public static List<string> FillEnglishBlanks(AlbumMeta meta)
    {
        var notes = new List<string>();
        if (string.IsNullOrWhiteSpace(meta.Name))
        {
            meta.Name = Name;
            notes.Add("曲名空缺，已填 Untitled");
        }

        if (string.IsNullOrWhiteSpace(meta.Author))
        {
            meta.Author = Author;
            notes.Add("作者空缺，已填 Unknown");
        }

        if (string.IsNullOrWhiteSpace(meta.LevelDesigner))
            meta.LevelDesigner = Designer;
        if (string.IsNullOrWhiteSpace(meta.DelaySec))
            meta.DelaySec = "0";
        if (string.IsNullOrWhiteSpace(meta.UnlockLevel))
            meta.UnlockLevel = "0";
        return notes;
    }

    public static bool HasScene(string? scene)
        => !string.IsNullOrWhiteSpace(scene)
           && scene.Trim().StartsWith("scene_", StringComparison.OrdinalIgnoreCase);

    public static bool HasPlayer(int playerSpeed)
        => playerSpeed is 1 or 2 or 3;
}

