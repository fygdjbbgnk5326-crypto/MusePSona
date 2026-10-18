using System.Globalization;

namespace MusePSona.Play;

/// <summary>
/// 官机音频偏移。内部使用整数毫秒，界面按三位小数显示秒。
/// </summary>
public static class AudioOffset
{
    public const int MinMs = -1000;
    public const int MaxMs = 1000;
    public const int StepMs = 1;
    public const int FastStepMs = 10;
    public const float LongPressDelaySec = 0.75f;
    public const float LongPressRepeatSec = 0.2f;

    public static int Clamp(int milliseconds)
        => Math.Clamp(milliseconds, MinMs, MaxMs);

    public static int Snap(int milliseconds)
        => Clamp((int)Math.Round(milliseconds / (double)StepMs, MidpointRounding.AwayFromZero) * StepMs);

    public static float ToSeconds(int milliseconds)
        => Snap(milliseconds) * 0.001f;

    /// <summary>把设备偏移应用到原始音频时钟；正值延后谱面事件。</summary>
    public static float Apply(float rawAudioClock, int milliseconds)
        => rawAudioClock - ToSeconds(milliseconds);

    public static string Format(int milliseconds)
        => (Snap(milliseconds) * 0.001f).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) + " s";
}
