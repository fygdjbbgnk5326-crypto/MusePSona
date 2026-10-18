namespace MuseDashChart;

public static class FolderChart
{
    public const string InfoFileName = "info.json";
    public const string ProjectFileName = "chart.json";

    public static ChartDocument Load(string folder)
    {
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(folder);

        var hasBms = false;
        for (var diff = 1; diff <= 5; diff++)
        {
            if (File.Exists(Path.Combine(folder, $"map{diff}.bms")))
            {
                hasBms = true;
                break;
            }
        }

        var projectPath = Path.Combine(folder, ProjectFileName);
        if (!hasBms && File.Exists(projectPath))
        {
            var fromProject = ProjectJson.Load(projectPath);
            MediaSidecar.Bind(fromProject.Meta, folder);
            BmsTime.SnapDocumentTo192(fromProject);
            return fromProject;
        }

        var infoPath = Path.Combine(folder, InfoFileName);
        if (!File.Exists(infoPath))
            throw new FileNotFoundException("Need info.json (with map*.bms) or chart.json", infoPath);

        var info = AlbumInfoJson.Load(infoPath);
        var doc = new ChartDocument();
        info.ApplyTo(doc.Meta);

        var found = new List<int>();
        for (var diff = 1; diff <= 5; diff++)
        {
            var bmsPath = Path.Combine(folder, $"map{diff}.bms");
            if (!File.Exists(bmsPath))
                continue;
            using var fs = File.OpenRead(bmsPath);
            var parsed = BmsReader.Load(fs);
            var chart = BmsReader.ToDifficulty(parsed);
            chart.RenumberIds();
            doc.Charts[diff] = chart;
            found.Add(diff);
            if (parsed.Info.Bpm > 0)
                doc.Meta.Bpm = ParseUtil.Flt(parsed.Info.Bpm);
            if (!string.IsNullOrWhiteSpace(parsed.Info.Genre))
                doc.Meta.Scene = parsed.Info.Genre;
            if (parsed.Info.Player > 0)
                doc.Meta.PlayerSpeed = parsed.Info.Player;
        }

        if (found.Count == 0)
            throw new InvalidDataException("No map1.bms … map5.bms in folder.");

        info.SyncFromCharts(doc.Charts);
        info.ApplyTo(doc.Meta);
        MediaSidecar.Bind(doc.Meta, folder);
        MergeApproachOverrides(doc, projectPath);
        BmsTime.SnapDocumentTo192(doc);
        return doc;
    }

    private static void MergeApproachOverrides(ChartDocument doc, string projectPath)
    {
        if (!File.Exists(projectPath)) return;
        ChartDocument stored;
        try
        {
            stored = ProjectJson.Load(projectPath);
        }
        catch
        {
            return;
        }

        foreach (var (diff, chart) in doc.Charts)
        {
            if (!stored.Charts.TryGetValue(diff, out var src)) continue;
            ApproachSpeed.ApplyOverrides(chart, src, bpm: (float)doc.Meta.BpmValue);
        }
    }

    public static void Save(ChartDocument document, string folder, bool writeProjectJson = true)
    {
        Directory.CreateDirectory(folder);
        BmsTime.SnapDocumentTo192(document);
        var info = AlbumInfoFile.FromMeta(document.Meta);
        info.SyncFromCharts(document.Charts);
        AlbumInfoJson.Save(info, Path.Combine(folder, InfoFileName));

        foreach (var (diff, chart) in document.Charts.OrderBy(kv => kv.Key))
        {
            var level = info.GetDifficultyLevel(diff);
            if (level == "0")
                level = "1";
            var bms = BmsWriter.Write(chart, document.Meta, level);
            File.WriteAllText(Path.Combine(folder, $"map{diff}.bms"), bms);
        }

        for (var diff = 1; diff <= 5; diff++)
        {
            if (document.Charts.ContainsKey(diff))
                continue;
            var leftover = Path.Combine(folder, $"map{diff}.bms");
            if (!File.Exists(leftover))
                continue;
            try { File.Delete(leftover); }
            catch (IOException) { /* 占用则下次打开仍可能复活，由调用方处理 */ }
            catch (UnauthorizedAccessException) { }
        }

        if (writeProjectJson)
            ProjectJson.Save(ApproachSpeed.ProjectSidecar(document), Path.Combine(folder, ProjectFileName));
    }
}

