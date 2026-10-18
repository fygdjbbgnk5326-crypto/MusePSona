namespace MuseDashChart;

/// <summary>
/// DJ 类 BPM 估算：降采样到约 1 kHz、振幅包络、自相关取峰。
/// 算法对应 MixMeister BPM Analyzer / SoundTouch BPMDetect 这一路的公开描述，
/// 不反编译 MixMeister 二进制。
/// </summary>
public static class BpmEstimate
{
    public const double DefaultMin = 55;
    public const double DefaultMax = 185;
    private const int TargetRate = 1000;

    public static double? FromMono(float[] samples, int sampleRate, double minBpm = DefaultMin, double maxBpm = DefaultMax)
    {
        if (samples.Length < sampleRate / 2 || sampleRate < 200) return null;
        var env = Envelope(Decimate(samples, sampleRate, TargetRate), TargetRate);
        if (env.Length < TargetRate) return null;
        return FromOnset(env, TargetRate, minBpm, maxBpm);
    }

    public static double? FromOnset(float[] onset, double hopsPerSec, double minBpm, double maxBpm)
    {
        var minLag = Math.Max(2, (int)Math.Floor(60 * hopsPerSec / maxBpm));
        var maxLag = Math.Min(onset.Length / 3, (int)Math.Ceiling(60 * hopsPerSec / minBpm));
        if (maxLag <= minLag + 4) return null;

        var xcorr = new float[maxLag + 1];
        for (var lag = minLag; lag <= maxLag; lag++)
            xcorr[lag] = Correlate(onset, lag);

        RemoveBias(xcorr, minLag, maxLag);
        Smooth(xcorr, minLag, maxLag, 9);

        var peakLag = FindPeakLag(xcorr, minLag, maxLag);
        if (peakLag <= 0) return null;
        var lagF = RefinePeak(xcorr, peakLag, minLag, maxLag);
        var bpm = 60.0 * hopsPerSec / lagF;
        bpm = PreferNearNeighbors(bpm, xcorr, hopsPerSec, minLag, maxLag, minBpm, maxBpm);
        if (bpm < 100 && bpm * 2 <= maxBpm)
            bpm *= 2;
        return Snap(bpm);
    }

    private static float[] Decimate(float[] samples, int sampleRate, int target)
    {
        var factor = Math.Max(1, sampleRate / target);
        var n = samples.Length / factor;
        var dest = new float[n];
        for (var i = 0; i < n; i++)
        {
            double sum = 0;
            var start = i * factor;
            for (var j = 0; j < factor; j++)
                sum += samples[start + j];
            dest[i] = (float)(sum / factor);
        }

        return dest;
    }

    private static float[] Envelope(float[] decimated, int rate)
    {
        var n = decimated.Length;
        var abs = new float[n];
        for (var i = 0; i < n; i++)
            abs[i] = Math.Abs(decimated[i]);

        var win = Math.Max(7, rate / 60);
        var env = MovingAverage(abs, win);
        double rms = 0;
        for (var i = 0; i < n; i++)
            rms += env[i] * env[i];
        rms = Math.Sqrt(rms / Math.Max(1, n));
        var cut = (float)(2.0 * rms);
        for (var i = 0; i < n; i++)
            env[i] = env[i] <= cut ? 0 : env[i] - cut;
        return env;
    }

    private static float[] MovingAverage(float[] src, int n)
    {
        var dest = new float[src.Length];
        var half = Math.Max(1, n / 2);
        double acc = 0;
        var count = 0;
        for (var i = 0; i < src.Length; i++)
        {
            acc += src[i];
            count++;
            if (i > half)
            {
                acc -= src[i - half - 1];
                count--;
            }

            dest[i] = (float)(acc / Math.Max(1, count));
        }

        return dest;
    }

    private static float Correlate(float[] onset, int lag)
    {
        double sum = 0;
        var n = onset.Length - lag;
        if (n <= 0) return 0;
        for (var i = 0; i < n; i++)
            sum += onset[i] * onset[i + lag];
        return (float)(sum / n);
    }

    private static void RemoveBias(float[] xcorr, int start, int end)
    {
        double meanX = 0;
        var count = end - start + 1;
        for (var i = start; i <= end; i++) meanX += xcorr[i];
        meanX /= count;
        var meanI = 0.5 * (start + end);
        double bNum = 0, bDen = 0;
        for (var i = start; i <= end; i++)
        {
            var xi = i - meanI;
            bNum += (xcorr[i] - meanX) * xi;
            bDen += xi * xi;
        }

        var b = bDen < 1e-9 ? 0 : bNum / bDen;
        var min = float.MaxValue;
        for (var i = start; i <= end; i++)
        {
            xcorr[i] -= (float)(b * i);
            if (xcorr[i] < min) min = xcorr[i];
        }

        for (var i = start; i <= end; i++)
            xcorr[i] -= min;
    }

    private static void Smooth(float[] xcorr, int start, int end, int n)
    {
        var copy = (float[])xcorr.Clone();
        var half = n / 2;
        for (var i = start; i <= end; i++)
        {
            double sum = 0;
            var c = 0;
            var a = Math.Max(start, i - half);
            var b = Math.Min(end, i + half);
            for (var j = a; j <= b; j++)
            {
                sum += copy[j];
                c++;
            }

            xcorr[i] = (float)(sum / c);
        }
    }

    private static int FindPeakLag(float[] xcorr, int minLag, int maxLag)
    {
        var best = minLag;
        var bestScore = float.MinValue;
        for (var lag = minLag + 1; lag < maxLag; lag++)
        {
            if (xcorr[lag] >= xcorr[lag - 1] && xcorr[lag] >= xcorr[lag + 1] && xcorr[lag] > bestScore)
            {
                bestScore = xcorr[lag];
                best = lag;
            }
        }

        if (bestScore <= 0)
        {
            for (var lag = minLag; lag <= maxLag; lag++)
            {
                if (xcorr[lag] > bestScore)
                {
                    bestScore = xcorr[lag];
                    best = lag;
                }
            }
        }

        return best;
    }

    private static double RefinePeak(float[] xcorr, int lag, int minLag, int maxLag)
    {
        var y0 = lag > minLag ? xcorr[lag - 1] : xcorr[lag];
        var y1 = xcorr[lag];
        var y2 = lag < maxLag ? xcorr[lag + 1] : y1;
        var denom = 2.0 * (2 * y1 - y0 - y2);
        if (Math.Abs(denom) < 1e-12) return lag;
        var delta = (y0 - y2) / denom;
        if (delta is < -0.6 or > 0.6) return lag;
        return lag + delta;
    }

    /// <summary>
    /// MixMeister 这类 DJ 分析器通常落在约 90–140。156 与 117 是 4/3 关系时取更接近该区间的峰。
    /// </summary>
    private static double PreferNearNeighbors(
        double bpm, float[] xcorr, double hopsPerSec, int minLag, int maxLag, double minBpm, double maxBpm)
    {
        double best = bpm;
        var bestScore = double.MinValue;
        foreach (var raw in new[] { bpm, bpm * 2, bpm / 2, bpm * 0.75, bpm / 0.75 })
        {
            var c = raw;
            while (c < minBpm && c * 2 <= maxBpm) c *= 2;
            while (c > maxBpm && c / 2 >= minBpm) c /= 2;
            if (c < minBpm || c > maxBpm) continue;
            var lag = (int)Math.Round(60 * hopsPerSec / c);
            if (lag < minLag || lag > maxLag) continue;
            var score = (double)xcorr[lag];
            var dj = c is >= 95 and <= 135 ? 1.22 : c is >= 80 and <= 160 ? 1.06 : 1.0;
            score *= dj;
            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    public static double AlignToKnown(double estimated, double known)
    {
        if (known is < 40 or > 300) return estimated;
        var best = estimated;
        var bestDist = Math.Abs(estimated - known);
        foreach (var f in new[] { 0.5, 0.75, 1.0, 4.0 / 3.0, 1.5, 2.0 })
        {
            var c = estimated * f;
            if (c is < 40 or > 300) continue;
            var d = Math.Abs(c - known);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        if (bestDist <= Math.Max(2.5, known * 0.04))
            return Snap(known);
        return Snap(best);
    }

    public static bool Near(double a, double b)
        => Math.Abs(a - b) <= Math.Max(2.5, Math.Max(a, b) * 0.04);

    public static double Snap(double bpm)
        => Math.Round(bpm, MidpointRounding.AwayFromZero);

    public static string Format(double bpm)
        => ((int)Math.Round(bpm, MidpointRounding.AwayFromZero)).ToString();
}

