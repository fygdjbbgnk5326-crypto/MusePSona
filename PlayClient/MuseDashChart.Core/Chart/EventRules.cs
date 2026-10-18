namespace MuseDashChart;

/// <summary>
/// 事件轨同刻替换族：同族互斥，异族可并存。对照词典 §2.2–2.3。
/// </summary>
public static class EventRules
{
    public static string Group(string? bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        if (ScrollSpeed.IsScrollId(id)) return "scroll";
        if (ApproachSpeed.IsApproachId(id)) return "approach";
        return id switch
        {
            "2E" or "2F" => "vignette",
            "1J" or "1K" => "notes-vis",
            "1L" or "1M" => "boss-vis",
            "25" or "26" => "bg-vis",
            "27" or "28" or "29" => "cam-roll",
            "2A" or "2B" => "ripple",
            "2C" or "2D" => "chroma",
            "2G" or "2H" => "tv",
            "2I" or "2J" or "2K" => "flash",
            "2L" or "2M" => "note-freeze",
            "2N" or "2O" => "bg-freeze",
            "2P" or "2Q" => "mosaic",
            "2R" or "2S" => "sepia",
            "2T" or "2U" or "2V" => "speedline",
            "2W" or "2X" => "retro",
            "2Y" or "2Z" => "auto",
            "40" or "41" => "quiz",
            _ => SceneCut(id) ? "scene-cut"
                : FlashColor(id) ? "flash-color"
                : id
        };
    }

    public static bool SceneCut(string id)
    {
        id = (id ?? "").ToUpperInvariant();
        return id.Length == 2 && id[0] == '1' && id[1] is >= 'O' and <= 'Y';
    }

    public static bool FlashColor(string id)
    {
        id = (id ?? "").ToUpperInvariant();
        return id.Length == 2 && id[0] == 'C' && id[1] is >= 'M' and <= 'T';
    }

    /// <summary>1O=scene_01 … 1W=scene_09，1X=scene_10，1Y=scene_12。</summary>
    public static string? SceneOfCut(string? bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        if (!SceneCut(id)) return null;
        var n = id[1] - 'O' + 1;
        if (id == "1X") n = 10;
        if (id == "1Y") n = 12;
        return n is >= 1 and <= 15 ? $"scene_{n:00}" : null;
    }

    public static void Inspect(int diff, DifficultyChart chart, ExportCheck check)
    {
        var ev = chart.Notes.Where(n => n.Kind == "event").ToList();
        if (ev.Count == 0) return;

        HintIfOpen(diff, ev, "vignette", "2E", "暗角", check);
        HintIfOpen(diff, ev, "notes-vis", "1J", "隐藏音符", check);
        HintIfOpen(diff, ev, "bg-vis", "25", "隐藏背景", check);
        HintIfOpen(diff, ev, "tv", "2G", "TV噪点", check);
        HintIfOpen(diff, ev, "bg-freeze", "2N", "背景静止", check);
        HintIfOpen(diff, ev, "mosaic", "2P", "马赛克", check);
        HintIfOpen(diff, ev, "chroma", "2C", "色散", check);
        HintIfOpen(diff, ev, "ripple", "2A", "判定线波纹", check);
        HintIfOpen(diff, ev, "sepia", "2R", "棕褐", check);
        HintIfOpen(diff, ev, "retro", "2W", "怀旧滤镜", check);
        HintIfOpen(diff, ev, "cam-roll", "29", "画面滚动", check, onMeansClosed: true);

        var unused = ev.Count(n =>
        {
            var id = (n.BmsId ?? "").ToUpperInvariant();
            return id is "1U" or "1W";
        });
        if (unused > 0)
            check.Hints.Add($"map{diff} 有 {unused} 条 1U/1W 切景（GC/DJMAX）：官包没有实装案例。");
    }

    static void HintIfOpen(
        int diff, List<Placement> ev, string group, string onId, string label, ExportCheck check,
        bool onMeansClosed = false)
    {
        Placement? last = null;
        foreach (var n in ev.OrderBy(x => x.TimeSec).ThenBy(x => x.Id))
        {
            if (Group(n.BmsId) != group) continue;
            last = n;
        }
        if (last == null) return;
        var id = (last.BmsId ?? "").ToUpperInvariant();
        var open = onMeansClosed ? id != onId : id == onId;
        if (open)
            check.Hints.Add($"map{diff} 结尾仍是{label}开（{id} @ {last.TimeSec:0.###}s），官谱通常会关掉。");
    }
}

