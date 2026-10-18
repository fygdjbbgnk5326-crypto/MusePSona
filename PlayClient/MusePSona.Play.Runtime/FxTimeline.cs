namespace MusePSona.Play;

/// <summary>通道 15 画面事件。暗角/集中线/闪光/像素/怀旧/藏键与切景独立，互不覆盖。事件没有坐标，开关铺满画面。</summary>
public static class FxTimeline
{
    public enum Flag
    {
        None,
        VigOn, VigOff,
        LineBlack, LineWhite, LineOff,
        NotesHide, NotesShow,
        FlashIn, FlashPeak, FlashOut,
        SepiaOn, SepiaOff,
        TvOn, TvOff,
        RippleOn, RippleOff,
        ChromaOn, ChromaOff,
        PixelOn, PixelOff,
        FilmOn, FilmOff,
        FlashTint
    }

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
        public bool Vignette { get; init; }
        public int SpeedLine { get; init; }
        public bool NotesHidden { get; init; }
        public float Flash { get; init; }
        public int FlashRgb { get; init; }
        public bool Sepia { get; init; }
        public bool Tv { get; init; }
        public bool Ripple { get; init; }
        public bool Chroma { get; init; }
        public bool Pixel { get; init; }
        public bool Film { get; init; }
    }

    public static List<Step> Build(IEnumerable<MuseDashChart.Placement> notes)
    {
        var steps = new List<Step>();
        foreach (var n in notes.OrderBy(n => n.TimeSec).ThenBy(n => n.Id))
        {
            if (LaneShared2X(n)) continue;
            var f = Of(n.BmsId);
            if (f == Flag.None) continue;
            steps.Add(new((float)n.TimeSec, f, n.BmsId ?? ""));
        }
        return steps;
    }

    public static State At(IReadOnlyList<Step> steps, float t)
    {
        var vig = false;
        var line = 0;
        var hide = false;
        var rgb = 0xffffff;
        var sepia = false;
        var tv = false;
        var ripple = false;
        var chroma = false;
        var pixel = false;
        var film = false;
        foreach (var s in steps)
        {
            if (s.T > t) break;
            switch (s.Flag)
            {
                case Flag.VigOn: vig = true; break;
                case Flag.VigOff: vig = false; break;
                case Flag.LineBlack: line = 1; break;
                case Flag.LineWhite: line = 2; break;
                case Flag.LineOff: line = 0; break;
                case Flag.NotesHide: hide = true; break;
                case Flag.NotesShow: hide = false; break;
                case Flag.FlashIn:
                case Flag.FlashPeak:
                case Flag.FlashOut:
                    break; // 白闪强度由三个时间点之间的连续曲线计算。
                case Flag.SepiaOn: sepia = true; break;
                case Flag.SepiaOff: sepia = false; break;
                case Flag.TvOn: tv = true; break;
                case Flag.TvOff: tv = false; break;
                case Flag.RippleOn: ripple = true; break;
                case Flag.RippleOff: ripple = false; break;
                case Flag.ChromaOn: chroma = true; break;
                case Flag.ChromaOff: chroma = false; break;
                case Flag.PixelOn: pixel = true; break;
                case Flag.PixelOff: pixel = false; break;
                case Flag.FilmOn: film = true; break;
                case Flag.FilmOff: film = false; break;
                case Flag.FlashTint: rgb = TintOf(s.Id); break;
            }
        }
        return new State
        {
            Vignette = vig,
            SpeedLine = line,
            NotesHidden = hide,
            Flash = FlashAt(steps, t),
            FlashRgb = rgb,
            Sepia = sepia,
            Tv = tv,
            Ripple = ripple,
            Chroma = chroma,
            Pixel = pixel,
            Film = film
        };
    }

    /// <summary>
    /// 官机把 2I/2J/2K 当作同一条白闪曲线的起点、峰值和终点：
    /// 2I→2J 从 0 线性升到 1，2J→2K 再线性降到 0。
    /// </summary>
    static float FlashAt(IReadOnlyList<Step> steps, float t)
    {
        Step? previous = null;
        Step? next = null;
        foreach (var step in steps)
        {
            if (step.Flag is not (Flag.FlashIn or Flag.FlashPeak or Flag.FlashOut)) continue;
            if (step.T <= t) previous = step;
            else { next = step; break; }
        }
        if (previous is not { } a) return 0f;
        if (a.Flag == Flag.FlashOut) return 0f;
        if (next is not { } b)
            return a.Flag == Flag.FlashPeak ? 1f : 0f;
        var span = b.T - a.T;
        var u = span <= 0.000001f ? 1f : Math.Clamp((t - a.T) / span, 0f, 1f);
        return a.Flag switch
        {
            Flag.FlashIn when b.Flag == Flag.FlashPeak => u,
            Flag.FlashPeak when b.Flag == Flag.FlashOut => 1f - u,
            Flag.FlashPeak => 1f,
            _ => 0f
        };
    }

    /// <summary>type 2 带提示齿轮和怀旧结束共用 ibms <c>2X</c>。轨道通道上的是齿轮。</summary>
    static bool LaneShared2X(MuseDashChart.Placement n)
        => string.Equals(n.BmsId, "2X", StringComparison.OrdinalIgnoreCase)
           && MuseDashChart.BmsTables.IsAirOrGroundChannel(n.Channel);

    public static Flag Of(string? id) => id switch
    {
        "2E" => Flag.VigOn,
        "2F" => Flag.VigOff,
        "2T" => Flag.LineBlack,
        "2U" => Flag.LineWhite,
        "2V" => Flag.LineOff,
        "1J" => Flag.NotesHide,
        "1K" => Flag.NotesShow,
        "2I" => Flag.FlashIn,
        "2J" => Flag.FlashPeak,
        "2K" => Flag.FlashOut,
        "2R" => Flag.SepiaOn,
        "2S" => Flag.SepiaOff,
        "2G" => Flag.TvOn,
        "2H" => Flag.TvOff,
        "2A" => Flag.RippleOn,
        "2B" => Flag.RippleOff,
        "2C" => Flag.ChromaOn,
        "2D" => Flag.ChromaOff,
        "2P" => Flag.PixelOn,
        "2Q" => Flag.PixelOff,
        "2W" => Flag.FilmOn,
        "2X" => Flag.FilmOff,
        "CM" or "CN" or "CO" or "CP" or "CQ" or "CR" or "CS" or "CT" => Flag.FlashTint,
        _ => Flag.None
    };

    static int TintOf(string id) => id switch
    {
        "CN" => 0x000000,
        "CO" => 0xff2244,
        "CP" => 0x33dd55,
        "CQ" => 0x4488ff,
        "CR" => 0x33e0e0,
        "CS" => 0xff44cc,
        "CT" => 0xffee33,
        _ => 0xffffff
    };
}
