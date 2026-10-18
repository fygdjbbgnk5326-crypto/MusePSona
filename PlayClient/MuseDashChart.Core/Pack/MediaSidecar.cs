namespace MuseDashChart;

/// <summary>
/// CustomAlbums 媒体约定：文件名固定，不写在 info.json 里。
/// 打开 MDM / 曲目文件夹时，把包内和旁边的正曲、试听、曲绘绑到同一工程。
/// </summary>
public static class MediaSidecar
{
    /// <summary>CustomAlbums / 游戏包只认这些正曲名。</summary>
    public static readonly string[] PackedMusicNames = ["music.ogg", "music.mp3"];
    /// <summary>工作目录预览可用，不会打进 MDM。</summary>
    public static readonly string[] PreviewMusicNames = ["music.wav", "music.flac"];
    public static readonly string[] MusicNames =
        ["music.ogg", "music.mp3", "music.wav", "music.flac"];
    public static readonly string[] PackedDemoNames = ["demo.ogg", "demo.mp3"];
    public static readonly string[] DemoNames = ["demo.ogg", "demo.mp3"];
    public static readonly string[] CoverNames = ["cover.png", "cover.gif", "cover.jpg", "cover.jpeg"];

    public static bool IsPackedAudioExtension(string? pathOrName)
    {
        var ext = Path.GetExtension(pathOrName ?? "");
        return ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".oga", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> AllNames()
    {
        foreach (var n in CoverNames) yield return n;
        foreach (var n in MusicNames) yield return n;
        foreach (var n in DemoNames) yield return n;
    }

    public static void Bind(AlbumMeta meta, string folder)
    {
        if (meta == null || string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;
        meta.MusicFile = FirstExisting(folder, meta.MusicFile, MusicNames) ?? meta.MusicFile;
        meta.DemoFile = FirstExisting(folder, meta.DemoFile, DemoNames) ?? meta.DemoFile;
        meta.CoverFile = FirstExisting(folder, meta.CoverFile, CoverNames) ?? meta.CoverFile;
    }

    public static void CopyMissing(string? sourceFolder, string destFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || string.IsNullOrWhiteSpace(destFolder))
            return;
        if (!Directory.Exists(sourceFolder))
            return;
        Directory.CreateDirectory(destFolder);
        if (PathsEqual(sourceFolder, destFolder))
            return;
        foreach (var name in AllNames())
        {
            var dest = Path.Combine(destFolder, name);
            if (File.Exists(dest))
                continue;
            var src = Path.Combine(sourceFolder, name);
            if (!File.Exists(src))
                continue;
            try
            {
                File.Copy(src, dest, overwrite: false);
            }
            catch (IOException)
            {
            }
        }
    }

    public static string? FindMdm(string folder)
    {
        if (!Directory.Exists(folder))
            return null;
        var hits = Directory.GetFiles(folder, "*.mdm");
        return hits.Length == 1 ? hits[0] : hits.FirstOrDefault(p =>
            string.Equals(Path.GetFileNameWithoutExtension(p), Path.GetFileName(folder),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstExisting(string folder, string preferred, string[] names)
    {
        var order = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferred))
            order.Add(preferred);
        order.AddRange(names);
        foreach (var name in order.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(folder, name);
            if (File.Exists(path))
                return Path.GetFileName(path);
        }

        return null;
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}

