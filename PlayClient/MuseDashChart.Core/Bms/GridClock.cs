namespace MuseDashChart;

/// <summary>
/// PatternDIY 式格点时钟：秒是派生量，落键吸附到格子。
/// elementTime = 60 / BPM / G；T(i) = elementTime * i + offsetSec。
/// </summary>
public sealed class GridClock
{
    public GridClock(decimal bpm, int gridDivision, decimal offsetSec = 0)
    {
        if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
        if (gridDivision <= 0) throw new ArgumentOutOfRangeException(nameof(gridDivision));
        Bpm = bpm;
        GridDivision = gridDivision;
        OffsetSec = offsetSec;
    }

    public decimal Bpm { get; }
    public int GridDivision { get; }
    public decimal OffsetSec { get; }

    /// <summary>单格时长（秒）。G 为每拍细分。</summary>
    public decimal ElementTime => 60m / Bpm / GridDivision;

    public decimal TimeFromIndex(int index)
        => ElementTime * index + OffsetSec;

    public int IndexFromTime(decimal timeSec)
    {
        var rel = timeSec - OffsetSec;
        if (rel < 0) return 0;
        return (int)decimal.Round(rel / ElementTime, MidpointRounding.AwayFromZero);
    }

    public decimal Snap(decimal timeSec)
        => TimeFromIndex(IndexFromTime(timeSec));

    /// <summary>显示格吸附后再落到 BMS 192 槽。有 Offset 时只走显示格（测偏移时钟）。</summary>
    public decimal SnapBms(decimal timeSec)
    {
        var g = Snap(timeSec);
        if (OffsetSec != 0) return g;
        return (decimal)BmsTime.SnapTo192((float)g, (float)Bpm);
    }

    /// <summary>每拍 48 格 = 每小节 192 槽。列表里的细分都能整除 48。</summary>
    public const int BmsSlotsPerBeat = 48;

    public static readonly int[] DefaultGridList = [4, 8, 12, 16, 24, 48];

    public static int ClampGrid(int grid)
        => Array.IndexOf(DefaultGridList, grid) >= 0 ? grid : BmsSlotsPerBeat;
}

