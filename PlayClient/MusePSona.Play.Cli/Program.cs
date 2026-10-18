using MuseDashChart;
using MusePSona.Play;

if (args.Length == 0)
{
    Console.WriteLine("用法: mdplay <文件夹或.mdm> [map编号=1] [--auto]");
    Console.WriteLine("自制谱游玩端运行时自检：解 MDM、列可玩键、自动演奏统计。");
    return 1;
}

var path = args[0];
var diff = 1;
var auto = args.Contains("--auto", StringComparer.OrdinalIgnoreCase);
foreach (var a in args.Skip(1))
{
    if (a.StartsWith("-")) continue;
    if (int.TryParse(a, out var d) && d is >= 1 and <= 5) diff = d;
}

PlaySong song;
try
{
    song = PlaySong.Load(path);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

Console.WriteLine(ChartIo.Inspect(song.Document));
Console.WriteLine("delay=" + song.DelaySec + "  music=" + (song.MusicPath ?? "(无)"));
var session = new PlaySession(song, diff);
var notes = session.Notes;
if (session.Difficulty != diff)
    Console.WriteLine("(没有 map" + diff + "，改用 map" + session.Difficulty + ")");
Console.WriteLine($"map{session.Difficulty}  playable={notes.Count}  hit={notes.Count(n => n.NeedsHit)}  gear={notes.Count(n => n.Category == NoteCategory.Gear)}");

if (auto)
{
    var last = notes.Count == 0 ? 0 : notes.Max(n => (float)n.EndSec) + 1f;
    for (var t = 0f; t <= last; t += 1f / 60f)
        session.Autoplay(t);
    Console.WriteLine($"autoplay P={session.Perfects} G={session.Greats} M={session.Misses} combo={session.Combo}");
}

return 0;
