namespace MusePSona.Play;

/// <summary>通道 15：藏景 25/26、背景停滞 2N/2O、画面上下滚 27/28/29。</summary>
public static class BgTimeline
{
    public enum Flag { None, Hide, Show, Freeze, Unfreeze, ScrollUp, ScrollDown, ScrollStop }

    public readonly struct Step
    {
        public Step(float t, Flag flag, string id)
        {
            T = t; Flag = flag; Id = id;
        }
        public float T { get; }
        public Flag Flag { get; }
        public string Id { get; }
    }

    public readonly struct State
    {
        public bool Hidden { get; init; }
        public bool Frozen { get; init; }
        /// <summary>1 上滚，-1 下滚，0 停。</summary>
        public int Scroll { get; init; }
    }

    public static List<Step> Build(IEnumerable<MuseDashChart.Placement> notes)
    {
        var steps = new List<Step>();
        foreach (var n in notes.OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            var f = Of(n.BmsId);
            if (f == Flag.None) continue;
            steps.Add(new((float)n.TimeSec, f, n.BmsId ?? ""));
        }
        return steps;
    }

    public static State At(IReadOnlyList<Step> steps, float t)
    {
        var hidden = false;
        var frozen = false;
        var scroll = 0;
        foreach (var s in steps)
        {
            if (s.T > t) break;
            switch (s.Flag)
            {
                case Flag.Hide: hidden = true; break;
                case Flag.Show: hidden = false; break;
                case Flag.Freeze: frozen = true; break;
                case Flag.Unfreeze: frozen = false; break;
                case Flag.ScrollUp: scroll = 1; break;
                case Flag.ScrollDown: scroll = -1; break;
                case Flag.ScrollStop: scroll = 0; break;
            }
        }
        return new State { Hidden = hidden, Frozen = frozen, Scroll = scroll };
    }

    public static Flag Of(string? id) => id switch
    {
        "25" => Flag.Hide,
        "26" => Flag.Show,
        "2N" => Flag.Freeze,
        "2O" => Flag.Unfreeze,
        "27" => Flag.ScrollUp,
        "28" => Flag.ScrollDown,
        "29" => Flag.ScrollStop,
        _ => Flag.None
    };
}
