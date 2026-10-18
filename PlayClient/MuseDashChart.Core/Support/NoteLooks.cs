namespace MuseDashChart;

/// <summary>编辑器时间轴贴图：ibms → 场景 Spine 条目号（与播放端 AnimBinding 一致）。</summary>
public static class NoteLooks
{
    public static string? SpineCode(string? bmsId)
        => (bmsId ?? "").ToUpperInvariant() switch
        {
            "01" or "02" or "03" => "10",
            "04" or "05" or "06" => "11",
            "07" or "08" or "09" => "12",
            "0A" => "13",
            "0B" => "14",
            "0C" or "0I" => "16",
            "0D" or "0J" => "15",
            "0E" => "05",
            "0G" or "16" or "17" => "04",
            "0H" => "03",
            "10" or "23" => "score",
            "13" => "06",
            "14" => "07",
            "15" => "08",
            "18" => "09",
            "21" => "17",
            "22" => "hp",
            _ => null
        };

    public static string[] RegionLeaves(string code)
        => code switch
        {
            "03" or "09" => ["sawtooth"],
            "04" => ["body"],
            "05" => ["FlowerHeart", "taiji", "bagua", "head", "body"],
            "08" => ["bomb_body", "body"],
            "15" => ["wooden", "wooden_hexie", "down"],
            "hp" => ["hp"],
            "score" => ["score"],
            _ => ["body", "body1", "head"]
        };

    public static string PackFolder(string scene, string code, int pathway)
    {
        var num = SceneNum(scene);
        if (code is "hp") return "0002_hp";
        if (code is "score") return "0003_score";
        var lane = pathway == 1 ? "air" : "road";
        if (code is "04") return num + "04";
        return num + code + "_" + lane;
    }

    public static string SceneNum(string? scene)
    {
        var s = (scene ?? "scene_01").Trim();
        if (s.StartsWith("scene_", StringComparison.OrdinalIgnoreCase) && s.Length >= 8)
            return s[^2..];
        return "01";
    }
}

