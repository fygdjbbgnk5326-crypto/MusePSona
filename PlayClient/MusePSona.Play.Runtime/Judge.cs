namespace MusePSona.Play;

/// <summary>
/// 练习端判定窗，<b>不是</b>官机实测值。单位秒，相对判定时刻。
/// 接上遥测后再改数字。
/// </summary>
public static class Judge
{
    public const float Perfect = 0.050f;
    public const float Great = 0.080f;
    public const float MissAfter = 0.120f;

    public enum Grade { None, Perfect, Great, Miss, Pass }

    public static Grade Tap(PlayNote n, float now, float audioDelay = 0)
    {
        if (!n.NeedsHit) return Grade.None;
        var err = Math.Abs(now - audioDelay - (float)n.TimeSec);
        if (err <= Perfect) return Grade.Perfect;
        if (err <= Great) return Grade.Great;
        if (now - audioDelay > (float)n.TimeSec + MissAfter) return Grade.Miss;
        return Grade.None;
    }

    public static bool IsLateMiss(PlayNote n, float now, float audioDelay = 0)
        => n.NeedsHit && now - audioDelay > (float)n.TimeSec + MissAfter;
}
