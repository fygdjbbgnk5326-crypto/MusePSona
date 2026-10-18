using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>Spine 物件的飞近、判定后动画与双押连线定位。</summary>
public sealed partial class PlayDirector
{
    bool TryPlacePose(PlayNote n, float chartNow, out Flight.Pose pose)
    {
        var g = _session.GradeOf(n.Id);
        if (n.Category == NoteCategory.Gear && g == Judge.Grade.Miss
            && chartNow < (float)n.TimeSec + Flight.GearDestroySec)
        {
            pose = new Flight.Pose
            {
                X = Flight.HitX,
                Y = Flight.LaneY(n),
                PivotX = float.NaN,
                PivotY = float.NaN
            };
            return true;
        }
        if (g is Judge.Grade.Perfect or Judge.Grade.Great)
        {
            if (!n.NeedsHit)
                return Flight.TryExitPose(n, chartNow, out pose);
            if ((n.Special == NoteSpecial.Pendulum || n.Special == NoteSpecial.Raider)
                && chartNow < (float)n.TimeSec)
                return Flight.TryPose(n, chartNow, out pose);
            pose = new Flight.Pose { X = Flight.HitX, Y = Flight.LaneY(n), PivotX = float.NaN, PivotY = float.NaN };
            return true;
        }
        if (g == Judge.Grade.Miss
            || (!n.NeedsHit && chartNow >= (float)n.TimeSec)
            || (n.NeedsHit && g == Judge.Grade.None && chartNow >= (float)n.TimeSec))
            return Flight.TryExitPose(n, chartNow, out pose);
        return Flight.TryPose(n, chartNow, out pose);
    }

    void PlaceSpine(Actor a, PlayNote n, float chartNow)
    {
        var g = _session.GradeOf(n.Id);
        var data = a.Spine.Skeleton.Data;
        var inClip = MdSpine.PickClip(data, n);
        Vector3 dest;
        var pinned = false;
        var followDest = false;
        var arcIn = n.Special is NoteSpecial.Pendulum or NoteSpecial.Raider;
        var catchPin = n.NeedsHit
            && g is Judge.Grade.Perfect or Judge.Grade.Great
            && (!arcIn || chartNow >= (float)n.TimeSec)
            // 0E 的判定窗口允许早按，但物件不能提前吸到判定圈；继续播放
            // in_* 飞近，抵达谱面时刻后再切换命中离场动画。
            && (n.BmsId != "0E" || chartNow >= (float)n.TimeSec);

        if (n.Category == NoteCategory.Masher && chartNow >= (float)n.TimeSec
            && g is Judge.Grade.Perfect or Judge.Grade.Great)
        {
            dest = Stage(Flight.HitX, Flight.LaneY(n));
            pinned = true;
            if (chartNow < (float)n.EndSec)
            {
                var hits = _session.MashHits(n.Id);
                if (hits > 0)
                {
                    var hurt = MdSpine.PickHurt(data, hits - 1) ?? inClip;
                    a.Spine.PoseTime(hurt, chartNow - _session.MashAt(n.Id), false);
                }
                else
                    a.Spine.Pose(n, (float)n.TimeSec, inClip);
            }
            else
            {
                var outClip = MdSpine.PickMasherOut(data, false);
                if (outClip != null)
                    a.Spine.PoseTime(outClip, chartNow - (float)n.EndSec, false);
                else
                    a.Spine.Pose(n, (float)n.EndSec, inClip);
            }
        }
        else if (n.Category == NoteCategory.Masher && g == Judge.Grade.Miss
            && chartNow >= (float)n.TimeSec)
        {
            var missOut = MdSpine.PickMasherOut(data, true);
            if (missOut != null)
                a.Spine.PoseTime(missOut, chartNow - (float)n.TimeSec, false);
            else
                a.Spine.Pose(n, chartNow, inClip);
            dest = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
            followDest = true;
        }
        else if (n.Category == NoteCategory.Gear && g == Judge.Grade.Miss
            && chartNow >= (float)n.TimeSec
            && MdSpine.PickDestroy(data) is string destroyClip)
        {
            // 官机 x03/x09 同轨碰撞：判定点切入 destroy，但碎片仍按车道
            // 速度向左离场。不能 pinned 在 HitX，否则整段动画会卡在判定圈内。
            // 异轨 PASS 不进入此分支，仍沿 in_* 穿过判定线。
            dest = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
            followDest = true;
            a.Spine.PoseTime(destroyClip, chartNow - (float)n.TimeSec, false);
        }
        else if (n.BmsId == "0E" && chartNow >= (float)n.TimeSec
            && g is Judge.Grade.None or Judge.Grade.Miss)
        {
            // 0E 的双押专用定位在判定后会把 LinearX 钳到 HitX；漏接时若
            // 继续走通用分支，物件便停在圈内且 in_* 卡最后一帧。明确使用
            // ThroughX 穿线，同时继续推进原生入场动画的判定后帧。
            dest = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
            followDest = true;
            a.Spine.Pose(n, chartNow, inClip);
        }
        else if (catchPin && n.Special == NoteSpecial.Pendulum)
        {
            // 击退 out_* 和入场同一套骨骼，根保持 x=0。再钉判定点会把爆炸件挪出画面。
            if (!a.HitBodyYReady)
            {
                a.Spine.Pose(n, (float)n.TimeSec, inClip);
                a.HitBodyY = a.Spine.BodyOffset().y;
                a.HitBodyYReady = true;
            }
            dest = Vector3.zero;
            var outClip = MdSpine.PickOut(data, g);
            if (outClip != null)
                a.Spine.PoseTime(outClip, chartNow - (float)n.TimeSec, false);
            else
                a.Spine.Pose(n, (float)n.TimeSec, inClip);
        }
        else if (catchPin && n.BmsId == "0E")
        {
            // 双押体积和连线都很醒目，固定根节点播放 out_* 会看起来卡在
            // 判定圈。保留官机 out_p/out_g 形变，同时按车道速度向左离场。
            dest = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
            followDest = true;
            var outClip = MdSpine.PickOut(data, g);
            if (outClip != null)
                a.Spine.PoseTime(outClip, chartNow - (float)n.TimeSec, false);
            else
                a.Spine.Pose(n, (float)n.TimeSec, inClip);
        }
        else if (catchPin)
        {
            dest = Stage(Flight.HitX, Flight.LaneY(n));
            pinned = true;
            var outClip = MdSpine.PickOut(data, g);
            if (outClip != null)
                a.Spine.PoseTime(outClip, chartNow - (float)n.TimeSec, false);
            else
                a.Spine.Pose(n, (float)n.TimeSec, inClip);
        }
        else if (!TryPlacePose(n, chartNow, out var fly))
        {
            a.Root.gameObject.SetActive(false);
            return;
        }
        else
        {
            var collect = AnimBinding.CollectPack(n) != null;
            if (!collect && !a.HitBodyYReady)
            {
                a.Spine.Pose(n, (float)n.TimeSec, inClip);
                if (n.BmsId == "0E")
                {
                    var face = a.Spine.LaneFaceCenter();
                    a.HitBodyY = face?.y ?? a.Spine.BodyOffset().y;
                    a.HitOnTrack = false;
                }
                else
                {
                    var trackShadow = arcIn ? null : a.Spine.ShadowCenter();
                    if (trackShadow != null)
                    {
                        a.HitBodyY = trackShadow.Value.y;
                        a.HitOnTrack = true;
                    }
                    else
                    {
                        var gearBottom = n.Category == NoteCategory.Gear && n.Pathway != 1
                            ? a.Spine.SawBottomY() : null;
                        if (gearBottom != null)
                        {
                            // 底边贴红线。mask 裁掉下半时，可见底边是裁切线，不是整圆的底。
                            a.HitBodyY = gearBottom.Value;
                            a.GearOnLine = true;
                        }
                        else
                            a.HitBodyY = a.Spine.BodyOffset().y;
                    }
                }
                a.HitBodyYReady = true;
            }
            var through = !n.NeedsHit && chartNow >= (float)n.TimeSec;
            var clipTime = chartNow - (float)n.TimeSec + Flight.DurationSec(n);
            var anim = inClip != null ? data.FindAnimation(inClip) : null;
            var clipEnd = anim != null ? anim.Duration : Flight.DurationSec(n);
            if (through && clipTime > clipEnd)
            {
                a.Spine.PoseTime(inClip, clipEnd, false);
                dest = Stage(Flight.ThroughX(n, chartNow), Flight.LaneY(n));
                followDest = true;
            }
            else
            {
                a.Spine.Pose(n, chartNow, inClip);
                dest = collect ? Stage(fly.X, fly.Y) : Vector3.zero;
                followDest = collect;
            }
        }

        a.Root.gameObject.SetActive(true);
        if (n.Category == NoteCategory.Masher)
        {
            dest.z = -0.12f;
            ApplyActorLayer(a);
        }
        var vis = a.Spine.VisualSize;
        var k = worldScale;
        if (!pinned)
            a.FlyScale = k;
        else if (a.FlyScale > 0.01f)
            k = a.FlyScale;
        else if (vis > 2.8f && n.BmsId != "0E" && n.Category != NoteCategory.Masher
            && n.Special != NoteSpecial.Pendulum)
            k *= 2.8f / vis;
        if (n.Category == NoteCategory.Masher && pinned
            && _session.GradeOf(n.Id) is Judge.Grade.Perfect or Judge.Grade.Great)
        {
            var age = chartNow - _session.MashAt(n.Id);
            if (age >= 0f)
                k *= 1f + 0.08f * Mathf.Exp(-8f * age);
        }
        // 0I/0J 已换对侧轨骨骼，官机不做 Y 翻转。
        var flipY = n.Inverted && !pinned
            && n.Special != NoteSpecial.Pendulum
            && n.Special != NoteSpecial.Raider;
        var scl = flipY ? new Vector3(k, -k, k) : Vector3.one * k;
        a.Root.localScale = scl;
        a.Root.rotation = Quaternion.identity;
        if (a.Extra != null) a.Extra.gameObject.SetActive(false);
        var off = a.Spine.BodyOffset();
        var shadow = arcIn ? null : a.Spine.ShadowCenter();
        var dualFace = n.BmsId == "0E" ? a.Spine.LaneFaceCenter() : null;
        if (pinned)
        {
            if (!a.OutAnchored)
            {
                a.OutAnchored = true;
                var anchor = dualFace ?? off;
                a.OutRootPos = dest - Vector3.Scale(anchor, scl);
            }
            a.Root.position = a.OutRootPos;
        }
        else if (dualFace != null)
        {
            // 两轨脸钉到同一条飞近 X + 本轨 Y。03/05/10 空中/地面动画脸心不同，
            // 跟各自 X 骨会对不齐下部。
            a.OutAnchored = false;
            var lane = Stage(0f, Flight.LaneY(n));
            var xTarget = dest.x;
            if (!followDest)
            {
                var lx = Flight.LinearX(n, chartNow);
                xTarget = float.IsNaN(lx) ? 0f : Stage(lx, 0f).x;
            }
            a.Root.position = new Vector3(
                xTarget - dualFace.Value.x * scl.x,
                lane.y - dualFace.Value.y * scl.y,
                dest.z);
        }
        else if (followDest)
        {
            a.OutAnchored = false;
            if (shadow != null)
            {
                var track = Stage(0f, Flight.ShadowTrackY);
                a.Root.position = new Vector3(dest.x, track.y - shadow.Value.y * scl.y, dest.z);
            }
            else if (a.GearOnLine)
            {
                var line = Stage(0f, Flight.GearRoadLine + Flight.GearBite);
                a.Root.position = new Vector3(dest.x, line.y - a.HitBodyY * scl.y, dest.z);
            }
            else
                a.Root.position = dest - Vector3.Scale(off, scl);
        }
        else
        {
            a.OutAnchored = false;
            var down = a.HitOnTrack ? Flight.ShadowTrackY
                : a.GearOnLine ? Flight.GearRoadLine + Flight.GearBite
                : Flight.LaneY(n);
            var lane = Stage(0f, down);
            a.Root.position = new Vector3(0f, lane.y - a.HitBodyY * scl.y, dest.z);
        }
        if (n.BmsId == "0E" && a.Spine != null)
        {
            var dualHit = chartNow >= (float)n.TimeSec
                && g is Judge.Grade.Perfect or Judge.Grade.Great;
            if (dualHit || DualPressHideLaser(n))
                a.Spine.HideLaserSlots();
            else
            {
                var keepRoad = SceneTimeline.Norm(string.IsNullOrEmpty(_scene) ? Flight.Scene : _scene) == "scene_12";
                a.Spine.StretchLaserTo(Mathf.Abs(Flight.AirY - Flight.RoadY) * worldScale, keepRoad ? 1f : -1f);
            }
        }
        if (n.Category == NoteCategory.Ghost && !pinned)
            a.Spine.SetMulA(Flight.GhostAlpha(n, chartNow));
    }

    /// <summary>
    /// 同刻两颗 0E 只留一份连线。多数场景连线在空中包；scene_12 粉柱+闪电纹理在 1205_road。
    /// </summary>
    bool DualPressHideLaser(PlayNote n)
    {
        if (n.BmsId != "0E") return false;
        var keepRoad = SceneTimeline.Norm(string.IsNullOrEmpty(_scene) ? Flight.Scene : _scene) == "scene_12";
        PlayNote keep = null;
        var twins = 0;
        foreach (var o in _session.Notes)
        {
            if (o.BmsId != "0E") continue;
            if (System.Math.Abs(o.TimeSec - n.TimeSec) > 0.002m) continue;
            twins++;
            if (keep == null) keep = o;
            else if (keepRoad)
            {
                if (o.Pathway != 1 && keep.Pathway == 1) keep = o;
                else if (o.Pathway == keep.Pathway && o.Id < keep.Id) keep = o;
            }
            else if (o.Pathway == 1 && keep.Pathway != 1) keep = o;
            else if (o.Pathway == keep.Pathway && o.Id < keep.Id) keep = o;
        }
        return twins >= 2 && keep != null && keep.Id != n.Id;
    }
}

