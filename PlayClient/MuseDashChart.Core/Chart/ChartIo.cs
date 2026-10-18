namespace MuseDashChart;

public readonly record struct ChartLoadResult(
    ChartDocument Document,
    string WorkingFolder,
    string? OriginalMdm)
{
    public UnpackReport Unpack { get; init; } = UnpackReport.Empty;
}

public static class ChartIo
{
    public static ChartLoadResult Load(string path)
    {
        path = Path.GetFullPath(path);
        if (Directory.Exists(path))
        {
            var unpack = new UnpackReport();
            if (!File.Exists(Path.Combine(path, FolderChart.InfoFileName)))
            {
                var mdm = MediaSidecar.FindMdm(path);
                if (mdm != null)
                    unpack = MdmPacker.Unpack(mdm, path);
            }

            return new ChartLoadResult(FolderChart.Load(path), path, MediaSidecar.FindMdm(path))
            {
                Unpack = unpack
            };
        }

        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        if (path.EndsWith(".mdm", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var folder = PrepareWorkingFolder(path);
            var unpack = MdmPacker.Unpack(path, folder);
            return new ChartLoadResult(FolderChart.Load(folder), folder, path)
            {
                Unpack = unpack
            };
        }

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var folder = Path.GetDirectoryName(path)!;
            return new ChartLoadResult(FolderChart.Load(folder), folder, null);
        }

        throw new NotSupportedException("Open a folder, info.json/chart.json, or .mdm");
    }

    public static string Inspect(ChartDocument document)
    {
        var lines = new List<string>
        {
            $"{document.Meta.Name}  BPM {document.Meta.Bpm}  scene {document.Meta.Scene}"
        };
        foreach (var (diff, chart) in document.Charts.OrderBy(kv => kv.Key))
        {
            var notes = chart.Notes.Count(n => n.Kind != "event");
            var ev = chart.Notes.Count(n => n.Kind == "event");
            var holds = chart.Notes.Count(n => n.Kind != "event" && n.IsHoldLike);
            var air = chart.Notes.Count(n => n.Kind != "event" && n.Pathway == 1);
            var gnd = chart.Notes.Count(n => n.Kind != "event" && n.Pathway == 0);
            var last = chart.Notes.Count == 0 ? 0 : chart.Notes.Max(n => n.TimeSec + n.LengthSec);
            lines.Add($"  map{diff}  notes={notes} events={ev} holds={holds} air={air} ground={gnd} last={last:0.###}s");
            if (chart.BpmChanges.Count > 0 || chart.TimeSigs.Count > 0)
                lines.Add($"    bpmChanges={chart.BpmChanges.Count} timeSigs={chart.TimeSigs.Count} bpmTable={chart.BpmTable.Count}");
            foreach (var warn in chart.Warnings)
                lines.Add("    warn " + warn);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string PrepareWorkingFolder(string mdmPath)
    {
        foreach (var folder in CandidateWorkingFolders(mdmPath))
        {
            if (TryCreateDirectory(folder))
                return folder;
        }

        throw new IOException(
            "Cannot create a working folder to unpack the MDM. Copy the .mdm next to MuseDashChart.Editor.exe and try again.");
    }

    private static IEnumerable<string> CandidateWorkingFolders(string mdmPath)
    {
        var name = Path.GetFileNameWithoutExtension(mdmPath);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = string.IsNullOrWhiteSpace(name) ? "chart" : name.Trim();
        var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        var mdmDir = Path.GetDirectoryName(mdmPath);
        if (!string.IsNullOrEmpty(mdmDir))
            yield return Path.Combine(mdmDir, name + "_open");

        yield return Path.Combine(MusePSonaPaths.RuntimeDirectory(), "work", name);
        yield return Path.Combine(AppContext.BaseDirectory, "work", name);
        yield return Path.Combine(Path.GetTempPath(), "MuseDashChart", name + "-" + stamp);
    }

    private static bool TryCreateDirectory(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            using var probe = File.Create(
                Path.Combine(folder, ".write-test"),
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

