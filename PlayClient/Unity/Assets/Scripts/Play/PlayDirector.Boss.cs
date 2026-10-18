using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>x01 Boss 本体站位与 clip。</summary>
public sealed partial class PlayDirector
{
    void PlaceBoss(float chartNow)
    {
        if (_session == null) return;
        var view = _session.Boss.At(chartNow);
        if (!view.Visible)
        {
            if (_boss != null && _boss.Root != null)
                _boss.Root.gameObject.SetActive(false);
            _bossHud = "hide";
            return;
        }

        if (_boss == null || _bossScene != _scene || _boss.Spine == null)
        {
            DropBoss();
            var pack = MdSpine.LoadBoss(string.IsNullOrEmpty(_scene) ? "scene_01" : _scene);
            if (pack == null)
            {
                Debug.LogWarning("Boss 本体没有骨骼: " + _scene);
                return;
            }
            var go = new GameObject("Boss");
            var a = new Actor { Root = go.transform, Category = NoteCategory.Boss };
            a.Spine = MdSpineView.Attach(a.Root, pack);
            a.Spine.WearBossSkin();
            // 角色走 Character 层。Boss 走 Default，画在速度线/背景之上、闪光之下。
            a.Spine.SetSortLayer("Default");
            a.Spine.SetLayer(3300, 80);
            PaintLayer(a.Root, 3300, 80);
            _boss = a;
            _bossScene = _scene;
        }

        _boss.Root.gameObject.SetActive(true);
        _boss.Root.SetAsLastSibling();
        var data = _boss.Spine.Skeleton.Data;
        var want = view.Clip;
        var time = view.Time;
        var loop = view.Loop;
        if (view.MashId != 0)
            MashClip(view, chartNow, data, ref want, ref time, ref loop);
        var clip = MdSpine.PickBossClip(data, want);
        if (string.IsNullOrEmpty(clip)) return;
        var anim = data.FindAnimation(clip);
        if (anim != null && !loop && time > anim.Duration && BossLeaveClip(clip))
        {
            _boss.Root.gameObject.SetActive(false);
            return;
        }
        // 攻击片段播完：形态还在就循环该形态待机。end 播完才回入场待机。
        if (anim != null && !loop && time > anim.Duration)
        {
            var leftover = time - anim.Duration;
            var recover = MdSpine.PickBossRecover(data, clip);
            if (string.IsNullOrEmpty(recover))
            {
                time = anim.Duration;
            }
            else
            {
                clip = recover;
                time = leftover;
                loop = clip.EndsWith("_standby", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(clip, "standby", System.StringComparison.OrdinalIgnoreCase);
                anim = data.FindAnimation(clip);
                if (loop && anim != null && anim.Duration > 0.01f)
                    time %= anim.Duration;
                else if (anim != null && time > anim.Duration)
                {
                    leftover = time - anim.Duration;
                    clip = MdSpine.PickBossClip(data, "standby");
                    time = leftover;
                    loop = true;
                    anim = data.FindAnimation(clip);
                    if (anim != null && anim.Duration > 0.01f)
                        time %= anim.Duration;
                }
            }
        }
        if (!_bossHomeReady)
        {
            // 锚点用待机第 0 帧，不要用当前 clip 第 0 帧：in 从屏外走进 BodyX，冲刺仍走骨骼位移。
            var idle = MdSpine.PickBossClip(data, "standby") ?? clip;
            _boss.Spine.PoseTime(idle, 0f, true);
            var homeShadow = _boss.Spine.ShadowPivot();
            _bossHomeTrack = homeShadow != null;
            _bossHome = homeShadow ?? _boss.Spine.BodyOffset();
            if (_bossHome.sqrMagnitude < 1e-8f) _bossHome = _boss.Spine.VisualCenter;
            _bossHomeReady = true;
        }
        _boss.Spine.PoseTime(clip, time, loop);
        var aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        var bossDown = _bossHomeTrack ? Flight.ShadowTrackY : BossTimeline.BodyY;
        var dest = Stage(BossTimeline.BodyXForAspect(aspect), bossDown, -3.0f);
        var pos = dest - _bossHome;
        _boss.Root.position = pos;
        _bossHud = clip + " " + time.ToString("0.00")
            + " v" + _boss.Spine.VisualSize.ToString("0.0");
    }

    static bool BossLeaveClip(string clip)
    {
        if (string.IsNullOrEmpty(clip)) return false;
        return clip == "out" || clip == "outside"
            || clip == "multi_atk_end" || clip == "multi_atk_48_end"
            || clip.StartsWith("multi_atk_hurt_end");
    }

    /// <summary>
    /// 连打挨打段：打中立刻播 hurt / multi_atk_hurt。
    /// 长条 17 在时间轴上已经是连打位，不要再切回 multi_atk_48。
    /// 16 结束后整段 multi_atk_out 都是回场（0601 约 0.63s 才从右侧出现，不能从 0.25s 起跳过）。
    /// 漏接才把近战续成 close_atk、把连打换成 48_end。
    /// </summary>
    void MashClip(BossTimeline.View view, float chartNow, Spine.SkeletonData data, ref string want, ref float time, ref bool loop)
    {
        var id = view.MashId;
        var note = _session.Notes.FirstOrDefault(n => n.Id == id);
        if (note == null) return;
        var g = _session.GradeOf(id);
        var hit = (float)note.TimeSec;
        var caught = g is Judge.Grade.Perfect or Judge.Grade.Great;
        // 自动演奏在 NotesHidden 时不会记 Perfect。本体仍要播击退，不能一直卡在冲刺。
        if (autoplay && g != Judge.Grade.Miss && chartNow + 1e-4f >= hit)
            caught = true;
        if (BossTimeline.IsMelee(note.BmsId) && want == "hurt")
        {
            if (!caught)
            {
                var dash = note.BmsId == "12" ? "close_atk_2_24" : "close_atk_1_48";
                var wind = note.BmsId == "12" ? BossTimeline.Close2Hit : BossTimeline.Close1Hit;
                want = dash;
                time = chartNow - hit + wind;
                loop = false;
            }
            else
            {
                time = Mathf.Max(0f, chartNow - hit);
                loop = false;
            }
            return;
        }
        if (want == "multi_atk_48")
            time = BeforeMashStep(data, time);
        if (want == "multi_atk_hurt")
        {
            if (caught)
            {
                var hits = _session.MashHits(id);
                want = MdSpine.PickMashHurt(data, hits);
                time = hits > 0
                    ? Mathf.Max(0f, chartNow - _session.MashAt(id))
                    : Mathf.Max(0f, chartNow - hit);
                loop = false;
                return;
            }
            if (chartNow - hit > Judge.Great)
            {
                want = "multi_atk_48_end";
                var missed = chartNow - hit - Judge.Great;
                time = MashEndTime(data, missed);
                loop = false;
                return;
            }
            if (note.BmsId == "17")
            {
                loop = false;
                return;
            }
            want = "multi_atk_48";
            time = chartNow - hit + BossTimeline.Close1Hit;
            loop = false;
            return;
        }
        if (!caught && (want == "multi_atk_out" || want == "multi_atk_hurt_end"))
        {
            if (note.BmsId == "17")
            {
                want = "multi_atk_48_end";
                time = 99f;
            }
            else
                want = "multi_atk_out";
        }
        if (want == "multi_atk_48_end" && time < 50f)
            time = MashEndTime(data, time);
    }

    static float BeforeMashStep(Spine.SkeletonData data, float time)
    {
        var dash = data?.FindAnimation("multi_atk_48");
        if (dash == null) return time;
        if (!TryDashTrack(data, dash, out _, out _, out _, out var t1, out _, out _, out _)) return time;
        var hold = t1 - 0.05f;
        return time > hold ? hold : time;
    }

    static float MashEndTime(Spine.SkeletonData data, float timeFromEnd)
    {
        var end = data?.FindAnimation("multi_atk_48_end");
        if (end == null) return timeFromEnd;
        if (!TryDashTrack(data, end, out var track, out var t0, out var x0, out var t1, out var x1, out var curve, out var xHome))
            return timeFromEnd + (end.Duration > BossTimeline.MashEndLead + 0.4f ? BossTimeline.MashEndLead : 0f);
        return DashOnJudge(end.Duration, track, t0, x0, t1, x1, curve, xHome, timeFromEnd);
    }

    static float PoseAtScreen(Spine.SkeletonData data, float screenX)
    {
        var end = data?.FindAnimation("multi_atk_48_end");
        if (end == null) return 0f;
        if (!TryDashTrack(data, end, out var track, out var t0, out var x0, out var t1, out var x1, out var curve, out var xHome))
            return 0f;
        SampleDash(track, t0, x0, t1, x1, curve, xHome, out var clipT, out var world);
        return ClipAtScreen(clipT, world, screenX);
    }

    static float DashOnJudge(float duration, Spine.TranslateTimeline track, float t0, float x0, float t1, float x1, int curve, float xHome, float timeFromEnd)
    {
        var aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        if (aspect < 1.2f) aspect = 1.2f;
        var left = -Flight.CameraSize * aspect - 0.8f;
        SampleDash(track, t0, x0, t1, x1, curve, xHome, out var clipT, out var world);
        const float arrive = 0.70f;
        const float stay = 1.35f;
        const float leave = 1.85f;
        var from = world.Length > 0 ? world[0] : BossTimeline.BodyXForAspect(aspect);
        float want;
        if (timeFromEnd <= 0f) want = from;
        else if (timeFromEnd < arrive) want = Mathf.Lerp(from, Flight.HitX, timeFromEnd / arrive);
        else if (timeFromEnd < stay) want = Flight.HitX;
        else if (timeFromEnd < leave) want = Mathf.Lerp(Flight.HitX, left, (timeFromEnd - stay) / (leave - stay));
        else return duration + (timeFromEnd - leave);
        return ClipAtScreen(clipT, world, want);
    }

    static void SampleDash(Spine.TranslateTimeline track, float t0, float x0, float t1, float x1, int curve, float xHome, out float[] clipT, out float[] world)
    {
        var aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        if (aspect < 1.2f) aspect = 1.2f;
        var body = BossTimeline.BodyXForAspect(aspect);
        // 平移键有的已乘 0.005。root.scaleX = 0.5，世界位移再乘这一层。
        var unit = (Mathf.Abs(xHome) > 80f || Mathf.Abs(x0) > 80f ? 0.005f : 1f) * 0.5f;
        const int n = 64;
        clipT = new float[n + 1];
        world = new float[n + 1];
        var span = Mathf.Max(0.001f, t1 - t0);
        for (var i = 0; i <= n; i++)
        {
            var p = i / (float)n;
            var along = track.GetCurvePercent(curve, p);
            clipT[i] = t0 + span * p;
            world[i] = body + (x0 + (x1 - x0) * along - xHome) * unit;
        }
    }

    static float ClipAtScreen(float[] clipT, float[] world, float screenX)
    {
        if (clipT == null || clipT.Length == 0) return 0f;
        var best = 0;
        var bestDist = Mathf.Abs(world[0] - screenX);
        for (var i = 1; i < clipT.Length; i++)
        {
            if (world[i] < screenX - 0.35f) continue;
            var dist = Mathf.Abs(world[i] - screenX);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return clipT[best];
    }

    static bool TryDashTrack(Spine.SkeletonData data, Spine.Animation anim, out Spine.TranslateTimeline track, out float t0, out float x0, out float t1, out float x1, out int curve, out float xHome)
    {
        track = null;
        t0 = x0 = t1 = x1 = xHome = 0f;
        curve = 0;
        var bones = data.Bones;
        foreach (var timeline in anim.Timelines)
        {
            if (timeline is not Spine.TranslateTimeline tr) continue;
            if ((uint)tr.BoneIndex >= (uint)bones.Count) continue;
            if (bones.Items[tr.BoneIndex].Name != "X") continue;
            track = tr;
            break;
        }
        if (track == null) return false;
        var frames = track.Frames;
        xHome = frames[1];
        var minDrop = Mathf.Abs(xHome) > 80f ? 400f : 2f;
        var best = minDrop;
        var found = -1;
        for (var i = Spine.TranslateTimeline.ENTRIES; i < frames.Length; i += Spine.TranslateTimeline.ENTRIES)
        {
            var drop = frames[i - 2] - frames[i + 1];
            if (drop <= best) continue;
            best = drop;
            found = i;
        }
        if (found < 0) return false;
        t0 = frames[found - 3];
        x0 = frames[found - 2];
        t1 = frames[found];
        x1 = frames[found + 1];
        curve = found / Spine.TranslateTimeline.ENTRIES - 1;
        return t1 > t0;
    }

    void DropBoss()
    {
        if (_boss == null) return;
        DropActor(_boss);
        _boss = null;
        _bossScene = "";
        _bossHomeReady = false;
        _bossHomeTrack = false;
        _bossHome = Vector3.zero;
    }

}
