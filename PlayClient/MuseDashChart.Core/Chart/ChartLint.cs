using System.Globalization;

namespace MuseDashChart;

/// <summary>
/// 依官谱统计的做谱规则（只出警告 / 提示，不拦导出）。
/// 规则出处：规则解析/自制包飞近规则.md、mdm物件转化词典.md §1.1–1.3。
/// 同刻混速、摆锤/突击者飞近、齿轮异档不检查：官谱本身大量使用。
/// </summary>
public static class ChartLint
{
    public const decimal TickEpsilon = 0.001m;
    private const int ListedTimes = 6;

    /// <summary>状态栏一句话：时间轴契约 + 仍会提示/警告的做谱规则。</summary>
    public const string StatusHint =
        "时间轴：导入导出 192 槽（0.0 对齐）。Delay 只加减整格吃音，不写进音符。设备延迟由官谱转录检索，不要填进 Delay。导出检查：空中 0I 飞近档 1 会提示；齿轮落在同轨长按中间（对侧无长按）或与同轨键同一拍会警告。事件同刻按族替换（切景/暗角/滤镜各不相冲）。";

    public static bool IsLaneGear(Placement n) => Id(n) == BmsTables.IdGear;

    private static string Id(Placement n) => (n.BmsId ?? "").ToUpperInvariant();

    private static bool IsHoldSpan(Placement n)
        => n.LengthSec > BmsTables.HoldEndMinLength && BmsTables.IsHoldKey(Id(n));

    public static void Inspect(int diff, DifficultyChart chart, ExportCheck check, float bpm = 0)
    {
        EventRules.Inspect(diff, chart, check);
        var playable = chart.Notes
            .Where(ApproachSpeed.IsPlayable)
            .OrderBy(n => n.TimeSec).ThenBy(n => n.Id)
            .ToList();
        if (playable.Count == 0) return;

        var airRaiderSlow = playable
            .Where(n => Id(n) == "0I" && n.Pathway == 1 && ApproachSpeed.Clamp(n.ApproachRate) == 1)
            .Select(n => n.TimeSec)
            .ToList();
        if (airRaiderSlow.Count > 0)
            check.Hints.Add(
                $"map{diff} 有 {airRaiderSlow.Count} 颗空中轨倒突击者 0I 为飞近档 1（{Times(airRaiderSlow)}）：官谱没有这种组合。");

        InspectGears(diff, chart, playable, check, bpm);
    }

    private static void InspectGears(int diff, DifficultyChart chart, List<Placement> playable, ExportCheck check, float bpm)
    {
        var gears = playable.Where(IsLaneGear).ToList();
        if (gears.Count == 0) return;

        var holds = chart.Notes.Where(n => ApproachSpeed.IsPlayable(n) && IsHoldSpan(n)).ToList();
        var others = playable.Where(n => !IsLaneGear(n)).ToList();
        var eps = TickEpsilon;

        bool Covers(Placement h, decimal t) => h.TimeSec - eps <= t && t <= h.TimeSec + h.LengthSec + eps;
        bool SameBeat(Placement n, decimal t)
            => bpm > 0
                ? BmsTime.SameSlot((float)n.TimeSec, (float)t, bpm)
                : Math.Abs(n.TimeSec - t) <= eps;

        var lonelyInHold = new List<decimal>();
        var sameTick = new List<decimal>();
        foreach (var g in gears)
        {
            var t = g.TimeSec;
            var inSameHold = holds.Any(h => h.Pathway == g.Pathway && h.TimeSec + eps < t && Covers(h, t));
            var oppositeHeld = holds.Any(h => h.Pathway != g.Pathway && Covers(h, t));
            if (inSameHold && !oppositeHeld)
                lonelyInHold.Add(t);

            if (others.Any(n => n.Pathway == g.Pathway && SameBeat(n, t)))
                sameTick.Add(t);
        }

        if (lonelyInHold.Count > 0)
            check.Warnings.Add(
                $"map{diff} 有 {lonelyInHold.Count} 个齿轮落在同轨长按/连打中间、对侧轨没有长按（{Times(lonelyInHold)}）：官谱从不出现（对侧按住时才正常）。");
        if (sameTick.Count > 0)
            check.Warnings.Add(
                $"map{diff} 有 {sameTick.Count} 个齿轮与同轨键同一拍（{Times(sameTick)}）：官谱全库仅 12 例，请确认。");
    }

    private static string Times(IReadOnlyList<decimal> times)
    {
        var shown = times.Take(ListedTimes)
            .Select(t => t.ToString("0.###", CultureInfo.InvariantCulture) + "s");
        var text = string.Join("、", shown);
        return times.Count > ListedTimes ? text + "…" : text;
    }
}

