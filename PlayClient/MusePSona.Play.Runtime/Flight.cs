namespace MusePSona.Play;

/// <summary>飞近时长与轨迹。与词典 §1.2–1.3 对齐（含 Boss 齿轮 18 = 弹 13）。Y 为舞台向下，Unity 显示时取负。</summary>
public static class Flight
{
    public const float HitX = -3.85f;
    public const float SpawnX = 10f;
    public const float Travel = 13.85f;
    /// <summary>官机正交相机 size（半高）。PlayDirector 必须用这个，不能用 5。</summary>
    public const float CameraSize = 3.75f;
    /// <summary>当前切景。14/15 只在 scene_05 用城堡 31/28/24 帧。</summary>
    public static string Scene = "";
    public const float AirY = -1.125f;
    public const float RoadY = 1.0f;
    /// <summary>官机遥测：上轨齿轮 body 骨骼位于屏幕自下 0.747H；正交半高 3.75 换算为向下坐标 -1.8525。</summary>
    public const float GearAirY = -1.8525f;
    public const float StageU = 7.5f;
    /// <summary>scene_01 红线（地板顶，向下）。</summary>
    public const float GearRoadLine = 1.67f;
    /// <summary>地面齿轮可见底边再低于地板顶这一截，对齐官机咬进地板的位置。</summary>
    public const float GearBite = 0.20f;
    /// <summary>投影中心在红线下方、地板上沿那一截的中间。飞近物件和 Boss 本体共用。</summary>
    public const float ShadowTrackY = GearRoadLine + 0.20f;
    public const float GearRadius = 150f * 0.005f * 0.5f;
    public const float GearRoadY = GearRoadLine - GearRadius;
    public const float RaiderJump = 0.38f;
    public const float RaiderPeak = 0.8f;
    public const float RaiderEdge = (0.98f - 0.5f) * StageU;
    public const float RaiderOver = 0.1f * StageU;
    public const float PendGroundR = 0.83f * StageU;
    public const float PendAirR = 1.08f * StageU;
    public const float PendEndDeg = -9f;
    public const float UpdownX = (0.86f - 0.5f) * StageU * (16f / 9f);
    /// <summary>判定前约 0.3 s 贴进轨道：纵移用全程 ease-out，不在 0.3 s 前提前钉死在轨上（钉死会让 23 帧档看起来砸下来）。</summary>
    public const float UpdownMerge = 0.3f;
    public const float UpdownEdge = 4.3f;
    /// <summary>幽灵：判定前这一段从全可见线性渐隐到 0。</summary>
    public const float GhostHideSec = 0.15f;
    /// <summary>官机 LongPressHurt → SpineActionController.SetAlpha(0.391)，DOTween 时长 0.2s。</summary>
    public const float HoldMissAlpha = 0.391f;
    public const float HoldMissFadeSec = 0.2f;
    /// <summary>x03/x09 齿轮 destroy 最长约 16 帧；独立于飞行档速保留完整碎裂。</summary>
    public const float GearDestroySec = 16f / 30f;
    /// <summary>
    /// 官机齿轮只在经过判定圈的当前逻辑帧采样一次；超过一帧仍未处理时
    /// 只能按已通过结算，禁止藏键/切模式/卡帧后补判受击。
    /// </summary>
    public const float GearJudgeLateSec = 1f / 30f;

    public static float GhostAlpha(PlayNote n, float now)
    {
        var remain = (float)n.TimeSec - now;
        if (remain >= GhostHideSec) return 1f;
        if (remain <= 0f) return 0f;
        return remain / GhostHideSec;
    }

    /// <summary>未接住时头星不钉判定线，按 TimeSec 连续飞过；Miss 后 0.2s 落到 0.391。</summary>
    public static float HoldMissVisual(PlayNote n, float now)
    {
        var t = now - (float)n.TimeSec;
        if (t <= 0f) return 1f;
        var k = t / HoldMissFadeSec;
        if (k >= 1f) return HoldMissAlpha;
        return 1f + (HoldMissAlpha - 1f) * k;
    }

    public enum PathKind { Linear, Raider, Pendulum, Rise, Drop }

    public readonly struct Pose
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float PivotX { get; init; }
        public float PivotY { get; init; }
        public bool HasPivot => !float.IsNaN(PivotX);
    }

    public static int HitFrames(PlayNote n)
    {
        var r = n.ApproachRate;
        if (n.Special == NoteSpecial.Raider) return Pick(r, 39, 30, 24);
        if (n.Special == NoteSpecial.Pendulum) return Pick(r, 26, 21, 18);
        if (n.BmsId == "0G") return Pick(r, 27, 23, 20);
        if (n.BmsId is "02" or "05" or "08" or "03" or "06" or "09")
            return Pick(r, 30, 26, 23);
        if (n.BmsId is "13" or "18") return Pick(r, 21, 18, 15);
        // 14/15：多数场景 in_boss_2_23/20/17；城堡 scene_05 才是 31/28/24。
        if (n.BmsId is "14" or "15")
            return SceneTimeline.Norm(Scene) == "scene_05" ? Pick(r, 31, 28, 24) : Pick(r, 23, 20, 17);
        return Pick(r, 44, 38, 33);
    }

    public static float DurationSec(PlayNote n, float scrollMul = 1f)
    {
        var mul = scrollMul <= 0 ? 1f : scrollMul;
        return HitFrames(n) / 30f / mul;
    }

    /// <summary>普通键 44/38/33 的横向速度。短判定帧（13/18/14/15）同速、更近生成，不能把 13.85u 压进 0.5s。</summary>
    public static int NormalFrames(int rate) => Pick(rate, 44, 38, 33);

    public static float LaneSpeed(int rate, float scrollMul = 1f)
    {
        var mul = scrollMul <= 0 ? 1f : scrollMul;
        return Travel / (NormalFrames(rate) / 30f / mul);
    }

    public const float MidY = (AirY + RoadY) * 0.5f;

    /// <summary>连打 0G/16/17 与近战 11/12 画在两轨中线（官机 masher body ≈0.478H）；上下轨都能打。弹幕 13/14/15、双押 0E 走谱面 pathway。</summary>
    public static bool CenterLane(PlayNote n)
        => n.Category == NoteCategory.Masher || n.BmsId is "11" or "12";

    public static float LaneY(PlayNote n)
    {
        if (CenterLane(n)) return MidY;
        if (PlayNote.IsOneKeyId(n.BmsId))
            return RoadY;
        if (n.Category == NoteCategory.Gear && n.Pathway == 1)
            return GearAirY;
        if (n.Category == NoteCategory.Gear && n.Pathway != 1)
            return GearRoadY;
        return n.Pathway == 1 ? AirY : RoadY;
    }

    public static PathKind PathOf(PlayNote n)
    {
        if (n.Special == NoteSpecial.Raider) return PathKind.Raider;
        if (n.Special == NoteSpecial.Pendulum) return PathKind.Pendulum;
        if (n.BmsId is "02" or "05" or "08") return PathKind.Rise;
        if (n.BmsId is "03" or "06" or "09") return PathKind.Drop;
        return PathKind.Linear;
    }

    /// <summary>漏接 / 齿轮：判定后不钉在判定线，按车道速度继续向左飞出。</summary>
    public const float OffLeft = HitX - 5f;

    public static float ThroughX(PlayNote n, float now, float scrollMul = 1f)
    {
        var remain = (float)n.TimeSec - now;
        return HitX + remain * LaneSpeed(n.ApproachRate, scrollMul);
    }

    public static bool TryExitPose(PlayNote n, float now, out Pose pose, float scrollMul = 1f)
    {
        pose = default;
        var remain = (float)n.TimeSec - now;
        if (remain > DurationSec(n, scrollMul)) return false;
        var x = ThroughX(n, now, scrollMul);
        if (x < OffLeft) return false;
        pose = new Pose { X = x, Y = LaneY(n), PivotX = float.NaN, PivotY = float.NaN };
        return true;
    }

    /// <summary>击中后在判定点播 out_* 的最长保留（秒）。</summary>
    public const float OutKeepSec = 0.8f;

    /// <summary>直线：判定时刻在判定线。剩余时间超过飞近时长则尚未生成。</summary>
    public static float LinearX(PlayNote n, float now, float scrollMul = 1f)
        => LinearXAt(n, (float)n.TimeSec, now, scrollMul);

    public static float LinearXAt(PlayNote n, float judgeTime, float now, float scrollMul = 1f)
    {
        var dur = DurationSec(n, scrollMul);
        var remain = judgeTime - now;
        if (remain > dur) return float.NaN;
        if (remain <= 0f) return HitX;
        return HitX + remain * LaneSpeed(n.ApproachRate, scrollMul);
    }

    /// <summary>
    /// 长按条上除头尾外的拍点星时间。官机 02_top 不只钉在头尾：
    /// 1 拍一颗（BPM 200 则 0.3s），Cyaegha map3 50.1–51.3 地面条上会再看到 50.4/50.7/51.0。
    /// </summary>
    public static List<float> HoldStarTimes(float startSec, float endSec, float bpm)
    {
        var times = new List<float>();
        if (bpm < 1f) bpm = 120f;
        var beat = 60f / bpm;
        if (beat < 1e-4f || endSec - startSec < beat * 1.5f)
            return times;
        const float eps = 0.02f;
        for (var t = startSec + beat; t < endSec - eps; t += beat)
        {
            if (t <= startSec + eps) continue;
            if (t >= endSec - beat * 0.5f) break;
            times.Add(t);
        }
        return times;
    }

    public static bool TryHoldSpan(PlayNote n, float now, out float headX, out float tailX, float scrollMul = 1f, bool pinHead = true)
    {
        headX = 0;
        tailX = 0;
        var speed = LaneSpeed(n.ApproachRate, scrollMul);
        var dur = DurationSec(n, scrollMul);
        if (now < (float)n.TimeSec - dur) return false;
        headX = HitX + ((float)n.TimeSec - now) * speed;
        tailX = HitX + ((float)n.EndSec - now) * speed;
        if (pinHead)
        {
            if (now >= (float)n.EndSec) return false;
            if (headX < HitX) headX = HitX;
            if (tailX < HitX) return false;
        }
        else if (tailX < OffLeft)
            return false;
        if (tailX < headX) tailX = headX;
        return true;
    }

    public static bool TryPose(PlayNote n, float now, out Pose pose, float scrollMul = 1f)
        => TryPoseAt(n, (float)n.TimeSec, now, out pose, scrollMul);

    public static bool TryPoseAt(PlayNote n, float judgeTime, float now, out Pose pose, float scrollMul = 1f)
    {
        pose = default;
        var dur = DurationSec(n, scrollMul);
        var remain = judgeTime - now;
        if (remain > dur) return false;
        var lane = LaneY(n);
        var p = 1f - remain / dur;
        if (PathOf(n) == PathKind.Pendulum)
        {
            Pendulum(n, p, lane, out var hx, out var hy, out var hxPv, out var hyPv);
            if (p > 1f && hx < OffLeft) return false;
            pose = new Pose { X = hx, Y = hy, PivotX = hxPv, PivotY = hyPv };
            return true;
        }
        if (remain <= 0f)
        {
            pose = new Pose { X = HitX, Y = lane, PivotX = float.NaN, PivotY = float.NaN };
            return true;
        }
        var x = HitX + remain * LaneSpeed(n.ApproachRate, scrollMul);
        var y = lane;
        var pvX = float.NaN;
        var pvY = float.NaN;
        switch (PathOf(n))
        {
            case PathKind.Raider:
                x = HitX + remain / dur * Travel;
                y = RaiderY(n, p, lane);
                break;
            case PathKind.Rise:
                Updown(n, p, lane, rise: true, out x, out y);
                break;
            case PathKind.Drop:
                Updown(n, p, lane, rise: false, out x, out y);
                break;
        }
        if (x < HitX && PathOf(n) != PathKind.Pendulum)
        {
            x = HitX;
            y = lane;
            pvX = float.NaN;
            pvY = float.NaN;
        }
        pose = new Pose { X = x, Y = y, PivotX = pvX, PivotY = pvY };
        return true;
    }

    static float RaiderY(PlayNote n, float p, float lane)
    {
        var air = n.Pathway == 1;
        var srcAir = n.Inverted ? !air : air;
        var mid = (AirY + RoadY) * 0.5f;
        var edge = RaiderEdge;
        if (n.Inverted) edge = 2f * mid - edge;
        if (p < RaiderJump) return edge;
        if (!srcAir && !n.Inverted)
        {
            var peak = lane - RaiderOver;
            if (p < RaiderPeak)
            {
                var s = 1f - (p - RaiderJump) / (RaiderPeak - RaiderJump);
                return peak + (edge - peak) * s * s;
            }
            var t = (p - RaiderPeak) / (1f - RaiderPeak);
            return peak + (lane - peak) * t * t;
        }
        var u = 1f - (p - RaiderJump) / (1f - RaiderJump);
        return lane + (edge - lane) * u * u * u;
    }

    static void Pendulum(PlayNote n, float p, float lane, out float x, out float y, out float pvX, out float pvY)
    {
        var air = n.Pathway == 1;
        var srcAir = n.Inverted ? !air : air;
        var r = srcAir ? PendAirR : PendGroundR;
        var a0 = srcAir ? 56f : 86f;
        var a1 = PendEndDeg;
        var a = (a0 + (a1 - a0) * p) * (MathF.PI / 180f);
        var aEnd = a1 * (MathF.PI / 180f);
        var d = n.Inverted ? -1f : 1f;
        pvX = HitX - r * MathF.Sin(aEnd);
        pvY = lane - d * r * MathF.Cos(aEnd);
        x = pvX + r * MathF.Sin(a);
        y = pvY + d * r * MathF.Cos(a);
    }

    /// <summary>摆锤进度 p=0 场外、p=1 判定点。命中后倒放把 p 从 1 拉回 0。</summary>
    public static bool TryPendulum(PlayNote n, float p, out Pose pose)
    {
        pose = default;
        if (p < 0f) p = 0f;
        if (p > 1f) p = 1f;
        var lane = LaneY(n);
        Pendulum(n, p, lane, out var x, out var y, out var pvX, out var pvY);
        pose = new Pose { X = x, Y = y, PivotX = pvX, PivotY = pvY };
        return true;
    }

    static void Updown(PlayNote n, float p, float lane, bool rise, out float x, out float y)
    {
        var x0 = UpdownX;
        x = HitX + (1f - p) * (x0 - HitX);
        var s = 1f - p;
        var edge = rise ? UpdownEdge : -UpdownEdge;
        y = lane + (edge - lane) * s * s;
    }

    static int Pick(int rate, int a, int b, int c) => rate <= 1 ? a : rate == 2 ? b : c;
}
