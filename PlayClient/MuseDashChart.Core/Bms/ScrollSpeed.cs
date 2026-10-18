namespace MuseDashChart;

public static class ScrollSpeed
{
    public const int DefaultTier = 1;
    public const string DefaultId = "0O";

    public static int ClampTier(int tier) => tier switch
    {
        <= 1 => 1,
        2 => 2,
        _ => 3
    };

    public static string WithTier(string bmsId, int tier)
    {
        tier = ClampTier(tier);
        return ScopeOf(bmsId) switch
        {
            "地面" => tier == 1 ? "0R" : tier == 2 ? "0S" : "0T",
            "空中" => tier == 1 ? "0U" : tier == 2 ? "0V" : "0W",
            _ => tier == 1 ? "0O" : tier == 2 ? "0P" : "0Q"
        };
    }

    public static bool IsScrollId(string? bmsId)
    {
        var id = (bmsId ?? "").ToUpperInvariant();
        return id is "0O" or "0P" or "0Q" or "0R" or "0S" or "0T" or "0U" or "0V" or "0W";
    }

    public readonly record struct State(int Tier, string BmsId, Placement? Event);

    public static int TierOf(string bmsId) => bmsId.ToUpperInvariant() switch
    {
        "0O" or "0R" or "0U" => 1,
        "0P" or "0S" or "0V" => 2,
        "0Q" or "0T" or "0W" => 3,
        _ => 0
    };

    public static string ScopeOf(string bmsId) => bmsId.ToUpperInvariant() switch
    {
        "0O" or "0P" or "0Q" => "双轨",
        "0R" or "0S" or "0T" => "地面",
        "0U" or "0V" or "0W" => "空中",
        _ => ""
    };

    public static string Caption(string bmsId)
    {
        var tier = TierOf(bmsId);
        if (tier <= 0) return "";
        var scope = ScopeOf(bmsId);
        return string.IsNullOrEmpty(scope) ? $"流速{tier}" : $"流速{tier} {scope}";
    }

    public static IEnumerable<Placement> Events(DifficultyChart chart)
        => chart.Notes
            .Where(n => IsScrollId(n.BmsId))
            .OrderBy(n => n.TimeSec)
            .ThenBy(n => n.Id);

    public static State At(DifficultyChart chart, decimal timeSec)
    {
        Placement? last = null;
        foreach (var ev in Events(chart))
        {
            if (ev.TimeSec > timeSec) break;
            last = ev;
        }

        return last is null
            ? new State(DefaultTier, DefaultId, null)
            : new State(TierOf(last.BmsId), last.BmsId, last);
    }
}

