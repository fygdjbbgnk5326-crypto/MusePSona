namespace MusePSona.Play;

/// <summary>
/// 官机 <c>config_others_assets_notedata</c> 物件判定窗。
/// left = Early，right = Late；单位秒，相对物件判定时刻。
/// </summary>
public static class Judge
{
    public readonly struct Window
    {
        public Window(float earlyPerfect, float earlyGreat, float latePerfect, float lateGreat)
        {
            EarlyPerfect = earlyPerfect;
            EarlyGreat = earlyGreat;
            LatePerfect = latePerfect;
            LateGreat = lateGreat;
        }

        public float EarlyPerfect { get; }
        public float EarlyGreat { get; }
        public float LatePerfect { get; }
        public float LateGreat { get; }
    }

    /// <summary>01–17、Hold 与连打头：官机四项均为 50/80ms。</summary>
    public static readonly Window Attack = new(0.050f, 0.080f, 0.050f, 0.080f);
    /// <summary>20/22/23：到点后由角色接触，官机 right_perfect_range=160ms。</summary>
    public static readonly Window PassiveCollect = new(0f, 0f, 0.160f, 0.160f);

    // 保留旧常量名，供飞近/测试和外部工具读取。
    public const float Perfect = 0.050f;
    public const float Great = 0.080f;
    public const float MissAfter = 0.080f;
    public const float MaxEarlyHit = 0.080f;
    const float BoundaryEpsilon = 0.000001f;

    public enum Grade { None, Perfect, Great, Miss, Pass }

    public static Window For(PlayNote n)
        => n.IsPassiveCollectible ? PassiveCollect : Attack;

    public static bool CanTap(PlayNote n, float chartNow)
    {
        if (!n.NeedsHit || n.IsPassiveCollectible) return false;
        var delta = chartNow - (float)n.TimeSec;
        var window = For(n);
        return delta >= -window.EarlyGreat - BoundaryEpsilon
            && delta <= window.LateGreat + BoundaryEpsilon;
    }

    public static float LateWindow(PlayNote n) => For(n).LateGreat;

    public static Grade Tap(PlayNote n, float now, float audioDelay = 0)
    {
        if (!n.NeedsHit) return Grade.None;
        var delta = now - audioDelay - (float)n.TimeSec;
        var window = For(n);
        if (delta < -window.EarlyGreat - BoundaryEpsilon) return Grade.None;
        if (delta < 0f)
            return -delta <= window.EarlyPerfect + BoundaryEpsilon ? Grade.Perfect : Grade.Great;
        if (delta <= window.LatePerfect + BoundaryEpsilon) return Grade.Perfect;
        if (delta <= window.LateGreat + BoundaryEpsilon) return Grade.Great;
        return Grade.Miss;
    }

    public static bool IsLateMiss(PlayNote n, float now, float audioDelay = 0)
        => n.NeedsHit
            && now - audioDelay > (float)n.TimeSec + LateWindow(n) + BoundaryEpsilon;
}
