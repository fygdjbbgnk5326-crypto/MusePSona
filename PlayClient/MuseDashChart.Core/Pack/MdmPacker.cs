using System.IO.Compression;
using System.Text;

namespace MuseDashChart;

public sealed class UnpackReport
{
    public static UnpackReport Empty { get; } = new();

    /// <summary>目标被占用且未能写出旁路文件，目录里仍是旧拷贝。</summary>
    public List<string> Stale { get; } = [];

    /// <summary>目标被占用，已写到 name.new（原文件未更新）。</summary>
    public List<string> Sidecar { get; } = [];

    public bool HasProblems => Stale.Count > 0 || Sidecar.Count > 0;

    public string ToChineseMessage()
    {
        if (!HasProblems) return "";
        var sb = new StringBuilder();
        sb.AppendLine("解包未能完整覆盖，谱面目录可能新旧混杂。");
        if (Stale.Count > 0)
        {
            sb.AppendLine("仍是旧文件（未能更新）：");
            foreach (var name in Stale)
                sb.AppendLine("  " + name);
        }

        if (Sidecar.Count > 0)
        {
            sb.AppendLine("已写到旁路文件（原文件未更新）：");
            foreach (var line in Sidecar)
                sb.AppendLine("  " + line);
        }

        sb.Append("请关掉占用它们的程序后重新打开。");
        return sb.ToString();
    }
}

public static class MdmPacker
{
    public const string StagingFolderName = "_unpacking";
    public const string PackStagingFolderName = "_pack";

    private static readonly string[] PackNames =
    [
        "info.json",
        "cover.png",
        "cover.gif",
        "cover.jpg",
        "cover.jpeg",
        "music.ogg",
        "music.mp3",
        "demo.ogg",
        "demo.mp3",
        "map1.bms",
        "map2.bms",
        "map3.bms",
        "map4.bms",
        "map5.bms",
        FolderChart.ProjectFileName
    ];

    public static string PackFolder(string folder, string mdmPath)
    {
        if (!File.Exists(Path.Combine(folder, FolderChart.InfoFileName)))
            throw new FileNotFoundException("info.json is required to pack an MDM.", folder);

        mdmPath = Path.GetFullPath(mdmPath);
        var tmp = CreateTempArchivePath(folder, mdmPath);
        try
        {
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in PackNames)
                {
                    var src = Path.Combine(folder, name);
                    if (!File.Exists(src))
                        continue;
                    zip.CreateEntryFromFile(src, name, CompressionLevel.Fastest);
                    added.Add(name);
                }

                if (!added.Contains("info.json"))
                    throw new InvalidDataException("Pack produced no info.json.");
                if (!added.Any(n => n.StartsWith("map", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".bms")))
                    throw new InvalidDataException("Pack produced no map*.bms.");
            }

            return CommitPackedFile(tmp, mdmPath);
        }
        finally
        {
            TryDeleteFile(tmp);
        }
    }

    /// <summary>
    /// 先解到 folder\_unpacking\，再逐个替换。占用则重试；仍失败则写 name.new，绝不假装已更新。
    /// </summary>
    public static UnpackReport Unpack(string mdmPath, string folder)
    {
        Directory.CreateDirectory(folder);
        var report = new UnpackReport();
        var staging = Path.Combine(folder, StagingFolderName);
        TryDeleteDirectory(staging);
        Directory.CreateDirectory(staging);

        try
        {
            using (var zip = ZipFile.OpenRead(mdmPath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || entry.FullName.Contains(".."))
                        continue;
                    var name = Path.GetFileName(entry.FullName);
                    if (string.IsNullOrEmpty(name)
                        || name.Equals(StagingFolderName, StringComparison.OrdinalIgnoreCase)
                        || name.Equals(PackStagingFolderName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var dest = Path.Combine(staging, name);
                    entry.ExtractToFile(dest, overwrite: true);
                }
            }

            var stagedProject = Path.Combine(staging, FolderChart.ProjectFileName);
            if (!File.Exists(stagedProject))
            {
                var staleProject = Path.Combine(folder, FolderChart.ProjectFileName);
                TryDeleteFile(staleProject);
            }

            var unpacked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(staging))
            {
                var name = Path.GetFileName(file);
                unpacked.Add(name);
                var dest = Path.Combine(folder, name);
                if (TryCommitFile(file, dest, attempts: 8))
                    continue;

                var sidecar = dest + ".new";
                if (TryCommitFile(file, sidecar, attempts: 4))
                    report.Sidecar.Add(name + " → " + Path.GetFileName(sidecar));
                else
                    report.Stale.Add(name);
            }

            // zip 里没有的 mapN.bms 要删掉，否则旧散文件会让 FolderChart 多出 map1，默认难度 1 就进不去 map3。
            for (var i = 1; i <= 5; i++)
            {
                var name = $"map{i}.bms";
                if (unpacked.Contains(name)) continue;
                TryDeleteFile(Path.Combine(folder, name));
            }
        }
        finally
        {
            TryDeleteDirectory(staging);
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(mdmPath));
        MediaSidecar.CopyMissing(parent, folder);
        return report;
    }

    public static void SaveDocumentAsMdm(ChartDocument document, string mdmPath)
    {
        mdmPath = Path.GetFullPath(mdmPath);
        var destDir = Path.GetDirectoryName(mdmPath) ?? "";
        var temp = Path.Combine(
            string.IsNullOrEmpty(destDir) ? AppContext.BaseDirectory : destDir,
            PackStagingFolderName,
            "chart-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            FolderChart.Save(document, temp, writeProjectJson: true);
            PackFolder(temp, mdmPath);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    /// <summary>在目标/谱面子目录 _pack 里写临时 zip，不占用系统 Temp。</summary>
    private static string CreateTempArchivePath(string sourceFolder, string mdmPath)
    {
        var dirs = new List<string>();
        void Add(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            try { dir = Path.GetFullPath(dir); }
            catch { return; }
            if (!dirs.Contains(dir, StringComparer.OrdinalIgnoreCase))
                dirs.Add(dir);
        }

        static string PackSub(string? parent)
        {
            if (string.IsNullOrWhiteSpace(parent)) return "";
            return Path.Combine(parent, PackStagingFolderName);
        }

        var destDir = Path.GetDirectoryName(Path.GetFullPath(mdmPath));
        try { Add(MusePSonaPaths.LocalExportDir()); } catch { /* ignore */ }
        Add(PackSub(AppContext.BaseDirectory));
        Add(PackSub(MusePSonaPaths.LocateEditorRoot()));
        Add(PackSub(sourceFolder));
        Add(PackSub(destDir));
        try { Add(Path.Combine(Path.GetTempPath(), "MuseDashChart", "pack")); }
        catch { /* 系统 Temp 不可用则跳过 */ }

        Exception? last = null;
        foreach (var dir in dirs)
        {
            try
            {
                Directory.CreateDirectory(dir);
                ClearDirectoryReadOnly(dir);
                return Path.Combine(dir, "pack-" + Guid.NewGuid().ToString("N") + ".tmp");
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new IOException(
            "找不到可写的临时目录（已尝试目标/_pack、谱面/_pack、编辑器/_pack）。"
            + (last != null ? "\n" + last.Message : ""),
            last);
    }

    /// <summary>
    /// 路径合法且能创建目录则允许导出。不在目标目录写探测文件（桌面受控文件夹会拦以点开头、无扩展名的临时文件）。
    /// 真正打包写到系统临时目录，再拷到目标；目标写失败会落到 LocalAppData。
    /// </summary>
    public static bool CanWriteDestination(string destPath, out string? reason)
    {
        reason = null;
        try
        {
            destPath = Path.GetFullPath(destPath);
            var dir = Path.GetDirectoryName(destPath);
            if (string.IsNullOrEmpty(dir))
            {
                reason = "导出路径无效。";
                return false;
            }

            Directory.CreateDirectory(dir);
            ClearReadOnly(destPath);
            ClearDirectoryReadOnly(dir);
            return true;
        }
        catch (Exception ex)
        {
            reason = destPath + " 路径无效。\n" + ex.Message;
            return false;
        }
    }

    private static string CommitPackedFile(string tempPath, string destPath)
    {
        var errors = new List<string>();
        foreach (var candidate in CommitCandidates(destPath))
        {
            if (TryCopyMdm(tempPath, candidate, out var ex))
                return candidate;
            if (ex != null)
                errors.Add(candidate + "\n  " + ex.Message);
        }

        throw new IOException(
            "无法写出 MDM。\n"
            + string.Join("\n", errors)
            + "\n请先退出 Unity 的 Play，关掉旧编辑器；若仍失败，把 MuseDashChart.Editor.exe 加入杀毒/勒索防护允许名单。谱面文件夹仍可保存。");
    }

    private static IEnumerable<string> CommitCandidates(string destPath)
    {
        destPath = Path.GetFullPath(destPath);
        var dir = Path.GetDirectoryName(destPath) ?? "";
        var fileName = Path.GetFileName(destPath);
        var stem = Path.GetFileNameWithoutExtension(destPath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        // 调用者指定的位置必须优先。只有目标被占用或不可写时，才回退到
        // LocalAppData / _pack；否则 PackFolder 返回的路径和实际落点会偏离，
        // 命令行、测试以及后续立即解包都找不到刚生成的 MDM。
        yield return destPath;

        string? local = null;
        try { local = Path.Combine(MusePSonaPaths.LocalExportDir(), fileName); }
        catch { /* LocalAppData 不可用则跳过 */ }
        if (!string.IsNullOrEmpty(local)
            && !string.Equals(local, destPath, StringComparison.OrdinalIgnoreCase))
            yield return local;
        yield return Path.Combine(AppContext.BaseDirectory, PackStagingFolderName, fileName);
        yield return Path.Combine(MusePSonaPaths.LocateEditorRoot(), PackStagingFolderName, fileName);
        if (!string.IsNullOrEmpty(dir))
        {
            yield return Path.Combine(dir, PackStagingFolderName, fileName);
            yield return Path.Combine(dir, PackStagingFolderName, stem + "-" + stamp + ".mdm");
        }
    }

    private static bool TryCopyMdm(string tempPath, string destPath, out Exception? error)
    {
        error = null;
        var destDir = Path.GetDirectoryName(destPath);
        try
        {
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
                ClearDirectoryReadOnly(destDir);
            }
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                ClearReadOnly(destPath);
                File.Copy(tempPath, destPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                Thread.Sleep(40 * (attempt + 1));
            }
        }

        return false;
    }

    private static bool TryCommitFile(string source, string dest, int attempts)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                ClearReadOnly(dest);
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(dest))
                    File.Replace(source, dest, destinationBackupFileName: null);
                else
                    File.Move(source, dest);
                return true;
            }
            catch (IOException)
            {
                ClearReadOnly(dest);
            }
            catch (UnauthorizedAccessException)
            {
                ClearReadOnly(dest);
            }

            Thread.Sleep(40 * (attempt + 1));
        }

        return false;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            ClearReadOnly(path);
            File.Delete(path);
        }
        catch
        {
            // leftover chart.json from a previous open
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                ClearReadOnly(file);
            Directory.Delete(path, true);
        }
        catch
        {
            // leftover _unpacking is cleaned on the next unpack
        }
    }

    private static void ClearReadOnly(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch
        {
            // retry loop will surface the real error
        }
    }

    private static void ClearDirectoryReadOnly(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            var info = new DirectoryInfo(dir);
            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;
        }
        catch
        {
            // commit fallbacks handle a locked folder
        }
    }
}

