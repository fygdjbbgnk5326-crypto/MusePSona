namespace MuseDashChart;

/// <summary>
/// 工具合集布局：工程在「MusePSona 1.0.0」下，编辑器与自制谱并列。
/// 发布后依赖 DLL 与设置 JSON 在 exe 旁的 lib\。
/// </summary>
public static class MusePSonaPaths
{
    public const string EditorFolderName = "MusePSona 编辑器";
    public const string ChartsFolderName = "MusePSona";
    public const string BundleFolderName = "MusePSona 1.0.0";
    public const string RuntimeFolderName = "lib";
    public const string EditorRootEnv = "MUSEPSONA_EDITOR_ROOT";

    static bool IsPublishLayoutFolder(string name)
        => name.Equals("dist", StringComparison.OrdinalIgnoreCase)
           || name.Equals("dist-next", StringComparison.OrdinalIgnoreCase)
           || name.Equals(RuntimeFolderName, StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeEditorRoot(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return false;
        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (name.Equals(EditorFolderName, StringComparison.OrdinalIgnoreCase))
            return true;
        return File.Exists(Path.Combine(dir, "MuseDashChart.sln"))
               || File.Exists(Path.Combine(dir, "启动编辑器.bat"))
               || Directory.Exists(Path.Combine(dir, "_ref"));
    }

    /// <summary>exe 旁的 lib：依赖 DLL、设置 JSON。若当前就在 lib 内则用当前目录。</summary>
    public static string RuntimeDirectory(string? baseDir = null)
    {
        var dir = (baseDir ?? AppContext.BaseDirectory ?? "").TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(dir);
        if (name.Equals(RuntimeFolderName, StringComparison.OrdinalIgnoreCase))
            return dir;
        return Path.Combine(dir, RuntimeFolderName);
    }

    /// <summary>从 exe / dist / lib 向上找到编辑器根。启动脚本可通过 MUSEPSONA_EDITOR_ROOT 指定。</summary>
    public static string LocateEditorRoot(string? baseDir = null)
    {
        try
        {
            var env = Environment.GetEnvironmentVariable(EditorRootEnv);
            if (!string.IsNullOrWhiteSpace(env))
            {
                env = Path.GetFullPath(env.Trim().Trim('"'));
                if (LooksLikeEditorRoot(env))
                    return env;
            }
        }
        catch
        {
            // fall through
        }

        var dir = (baseDir ?? AppContext.BaseDirectory ?? "").TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrEmpty(dir) && IsPublishLayoutFolder(Path.GetFileName(dir)))
        {
            var parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(parent)) break;
            dir = parent;
        }

        var walk = dir;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(walk); i++)
        {
            if (LooksLikeEditorRoot(walk))
                return walk;
            walk = Path.GetDirectoryName(walk) ?? "";
        }

        return dir;
    }

    /// <summary>自制谱落点：MusePSona\导出；没有该目录则用编辑器根下 导出。</summary>
    public static string DefaultChartsExport(string editorRoot)
    {
        try
        {
            var muse = ChartsRepo(editorRoot);
            if (Directory.Exists(muse))
            {
                var folder = Path.Combine(muse, "导出");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }
        catch
        {
            // fall through
        }

        var local = Path.Combine(editorRoot, "导出");
        Directory.CreateDirectory(local);
        return local;
    }

    static string TrimDir(string dir)
        => dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    static bool IsChartsRepo(string dir)
        => Directory.Exists(dir)
           && (Directory.Exists(Path.Combine(dir, "PlayClient"))
               || Directory.Exists(Path.Combine(dir, "自制谱"))
               || Directory.Exists(Path.Combine(dir, "导出")));

    static IEnumerable<string> Ancestors(string start, int max)
    {
        var walk = TrimDir(start);
        for (var i = 0; i < max && !string.IsNullOrEmpty(walk); i++)
        {
            yield return walk;
            walk = Path.GetDirectoryName(walk) ?? "";
        }
    }

    /// <summary>自制谱 / PlayClient 所在的 MusePSona 目录（在 1.0.0 包内与编辑器并列）。</summary>
    public static string ChartsRepo(string editorRoot)
    {
        foreach (var d in Ancestors(editorRoot, 8))
        {
            var direct = Path.Combine(d, ChartsFolderName);
            if (IsChartsRepo(direct)) return direct;
            var nested = Path.Combine(d, BundleFolderName, ChartsFolderName);
            if (IsChartsRepo(nested)) return nested;
            if (Path.GetFileName(d).Equals(ChartsFolderName, StringComparison.OrdinalIgnoreCase)
                && IsChartsRepo(d))
                return d;
        }

        var parent = Path.GetDirectoryName(TrimDir(editorRoot));
        return string.IsNullOrEmpty(parent)
            ? Path.Combine(editorRoot, ChartsFolderName)
            : Path.Combine(parent, ChartsFolderName);
    }

    /// <summary>工具合集根（含 Malody 谱面转换器）。</summary>
    public static string CollectionRoot(string editorRoot)
    {
        foreach (var d in Ancestors(editorRoot, 8))
        {
            if (Directory.Exists(Path.Combine(d, "Malody 谱面转换器")))
                return d;
        }

        var charts = ChartsRepo(editorRoot);
        var bundle = Path.GetDirectoryName(TrimDir(charts));
        if (!string.IsNullOrEmpty(bundle))
        {
            var tools = Path.GetDirectoryName(bundle);
            if (!string.IsNullOrEmpty(tools)) return tools;
            return bundle;
        }

        return Path.GetDirectoryName(TrimDir(editorRoot)) ?? editorRoot;
    }

    /// <summary>自制谱工作目录：MusePSona\自制谱。</summary>
    public static string ChartsProjectDir(string editorRoot)
    {
        var folder = Path.Combine(ChartsRepo(editorRoot), "自制谱");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>本机可写导出：%LOCALAPPDATA%\MuseDashChart\export。桌面目录常被完整性/受控文件夹拦截。</summary>
    public static string LocalExportDir()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuseDashChart", "export");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>优先本机 export，写不了再落到 exe 旁 _pack。</summary>
    public static string StagingMdmPath(string fileName)
    {
        try
        {
            return Path.Combine(LocalExportDir(), fileName);
        }
        catch
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "_pack");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }
    }
    /// <summary>播放端 StreamingAssets 里的场景 Spine（时间轴物件贴图）。</summary>
    public static string? MdLooksSpine(string? editorRoot = null)
    {
        try
        {
            var root = LocateEditorRoot(editorRoot);
            var p = Path.Combine(ChartsRepo(root), "PlayClient", "Unity", "Assets",
                "StreamingAssets", "MdLooks", "spine");
            return Directory.Exists(p) ? p : null;
        }
        catch
        {
            return null;
        }
    }

    public static string PlayClientSongMdm(string editorRoot)
    {
        var charts = Path.Combine(ChartsRepo(editorRoot), "PlayClient", "Unity",
            "Assets", "StreamingAssets", "Charts");
        Directory.CreateDirectory(charts);
        return Path.Combine(charts, "song.mdm");
    }
}

