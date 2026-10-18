namespace MuseDashChart;

public sealed class ExportCheck
{
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    /// <summary>做谱建议（官谱统计），不影响导出。</summary>
    public List<string> Hints { get; } = [];
    public bool CanExport => Errors.Count == 0;
}

public static class ExportPreflight
{
    /// <summary>预览曲（demo）上限，超过则新建时拒绝选用。不参与 BPM。</summary>
    public const double DemoMaxSeconds = 40;
    public static ExportCheck Inspect(ChartDocument document, string? folder, string? destMdm)
    {
        var check = new ExportCheck();
        var meta = document.Meta;

        if (string.IsNullOrWhiteSpace(meta.Name))
            check.Warnings.Add("info 缺曲名，将用 Untitled。");
        if (string.IsNullOrWhiteSpace(meta.Author))
            check.Warnings.Add("info 缺作者，将用 Unknown。");
        if (!MetaPlaceholders.HasScene(meta.Scene))
            check.Errors.Add("缺少场景 Scene，无法导出。");
        if (!MetaPlaceholders.HasPlayer(meta.PlayerSpeed))
            check.Errors.Add("缺少人物 PLAYER（1/2/3），无法导出。");

        if (string.IsNullOrWhiteSpace(meta.Bpm)
            || !ParseUtil.TryDecimal(meta.Bpm, out var bpm)
            || bpm <= 0)
            check.Errors.Add("缺少有效 BPM。");

        var active = 0;
        for (var i = 1; i <= 5; i++)
        {
            var level = i switch
            {
                1 => meta.Difficulty1,
                2 => meta.Difficulty2,
                3 => meta.Difficulty3,
                4 => meta.Difficulty4,
                _ => meta.Difficulty5
            };
            if (level != "0" || document.Charts.ContainsKey(i))
                active++;
        }

        if (active < 1)
            check.Warnings.Add("空 map 小于 1：还没有难度谱。");

        var empty = new List<int>();
        var playTotal = 0;
        foreach (var (diff, chart) in document.Charts.OrderBy(kv => kv.Key))
        {
            var keys = chart.Notes.Count(BmsTables.DrawsOnPathway);
            playTotal += keys;
            if (keys == 0)
                empty.Add(diff);
        }

        if (playTotal == 0)
            check.Errors.Add("整张谱没有键位，拒绝打包空谱。");
        else if (empty.Count > 1)
            check.Errors.Add("空 map 大于 1 个并且没有键位：" + string.Join("、", empty.Select(d => "map" + d)) + "。");
        else if (empty.Count == 1)
            check.Warnings.Add("map" + empty[0] + " 没有键位。");

        var lintBpm = ParseUtil.TryDecimal(meta.Bpm, out var bpmVal) && bpmVal > 0 ? (float)bpmVal : 0f;
        foreach (var (diff, chart) in document.Charts.OrderBy(kv => kv.Key))
            ChartLint.Inspect(diff, chart, check, lintBpm);

        if (!HasPackedAudio(folder, meta))
        {
            var msg = HasPreviewAudio(folder, meta)
                ? "CustomAlbums 只认 ogg/mp3，请转码后再导出。"
                : "缺少音频（music.ogg / music.mp3）。CustomAlbums 只认 ogg/mp3，请转码后再导出。";
            if (check.Errors.Count > 0)
                check.Errors.Add(msg);
            else
                check.Warnings.Add(msg);
        }
        if (!HasDemo(folder, meta))
            check.Warnings.Add("缺少预览曲（demo.ogg / demo.mp3），仍可导出。");
        if (!HasCover(folder, meta))
            check.Warnings.Add("缺少曲绘（cover.png / cover.gif）。");

        return check;
    }

    private static bool HasPreviewAudio(string? folder, AlbumMeta meta)
        => MediaSidecar.PreviewMusicNames.Any(n => FileExists(folder, n))
           || (FileExists(folder, meta.MusicFile)
               && !MediaSidecar.IsPackedAudioExtension(meta.MusicFile));

    private static bool HasPackedAudio(string? folder, AlbumMeta meta)
    {
        foreach (var name in new[] { meta.MusicFile }.Concat(MediaSidecar.PackedMusicNames))
        {
            if (string.IsNullOrWhiteSpace(name) || !MediaSidecar.IsPackedAudioExtension(name))
                continue;
            if (FileExists(folder, name))
                return true;
        }

        return false;
    }

    private static bool HasDemo(string? folder, AlbumMeta meta)
    {
        foreach (var name in new[] { meta.DemoFile, "demo.ogg", "demo.mp3" })
        {
            if (FileExists(folder, name))
                return true;
        }

        return false;
    }

    private static bool HasCover(string? folder, AlbumMeta meta)
    {
        foreach (var name in new[] { meta.CoverFile, "cover.png", "cover.gif", "cover.jpg", "cover.jpeg" })
        {
            if (FileExists(folder, name))
                return true;
        }

        return false;
    }

    private static bool FileExists(string? folder, string? name)
    {
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name))
            return false;
        try { return File.Exists(Path.Combine(folder, name)); }
        catch { return false; }
    }
}

