namespace MuseDashChart;

/// <summary>
/// 连打次数由时长决定，谱面不另存次数。游戏按约 40ms 一击计入上限
/// （GetMulHitHighThreshold，与萌娘百科「每 40ms 一次」一致）。
/// </summary>
public static class MasherHits
{
    public const decimal TickSec = 0.04m;

    public static bool IsMasher(string? bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        return id is BmsTables.IdMasher or "16" or "17";
    }

    public static int High(decimal lengthSec)
    {
        if (lengthSec <= 0) return 0;
        var n = (int)decimal.Truncate(lengthSec / TickSec);
        return n < 1 ? 1 : n;
    }

    public static string Caption(decimal lengthSec)
    {
        var high = High(lengthSec);
        return high <= 0 ? "连打" : $"连打 ×{high}";
    }

    public static string LengthCell(decimal lengthSec)
    {
        var high = High(lengthSec);
        if (lengthSec <= 0) return high > 0 ? $"{high}次" : "";
        return high > 0 ? $"{lengthSec:0.###}s · {high}次" : lengthSec.ToString("0.###") + "s";
    }
}

