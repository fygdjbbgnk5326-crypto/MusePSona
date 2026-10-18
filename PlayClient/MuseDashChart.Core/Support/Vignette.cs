namespace MuseDashChart;

public static class Vignette
{
    public const string OnId = "2E";
    public const string OffId = "2F";

    public readonly record struct State(bool On, Placement? Event);

    public static bool IsVignetteId(string bmsId)
    {
        var id = bmsId.ToUpperInvariant();
        return id is OnId or OffId;
    }

    public static bool? OnOff(string bmsId) => bmsId.ToUpperInvariant() switch
    {
        OnId => true,
        OffId => false,
        _ => null
    };

    public static IEnumerable<Placement> Events(DifficultyChart chart)
        => chart.Notes
            .Where(n => IsVignetteId(n.BmsId))
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
            ? new State(false, null)
            : new State(OnOff(last.BmsId) == true, last);
    }
}

