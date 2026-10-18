using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>角色 Hold 状态、双押分组与副轨虚影表现。</summary>
public sealed partial class PlayDirector
{
    void BeginGirlHitBatch()
    {
        _girlDoubleGroupsPlayed.Clear();
        var now = GirlNow();
        if (now < _girlAirborneAt || now < _girlDownStrikeAt
            || (!string.IsNullOrEmpty(_girlGhostAction) && now < _girlGhostAt))
        {
            EndGirlAirborne();
            HideGirlGhost();
        }
        var oldMask = _girlHoldMask;
        var oldTrackY = oldMask != 0
            ? GirlHoldTrackY(oldMask)
            : GirlDoublePoseActive(now) ? _girlDoublePoseY
            : GirlCenterPoseActive(now) ? GirlDoubleHoldY : GirlGroundY + GirlTrackLift();
        _girlHoldMask = ActiveGirlHoldMask(now, out _girlHoldUntil);
        if (_girlHoldMask == 0)
            _girlPrimaryHoldLane = -1;
        else if (_girlPrimaryHoldLane < 0 || (_girlHoldMask & (1 << _girlPrimaryHoldLane)) == 0)
            // 官机同帧双押会先保留地面主指、吞掉第二个空中 GetButtonDown。
            _girlPrimaryHoldLane = (_girlHoldMask & 1) != 0 ? 0 : 1;

        // 双押仍只播放一份 press，但 PlaceGirl 会把根节点抬到 Boss 中轨。
        // 第二根 Hold 只改变 mask/位置，不生成额外角色虚影。
        if (_girlHoldMask != 0)
        {
            EndGirlAirborne();
            var press = ExistingGirlHoldClip(_girlHoldMask);
            if (oldMask != _girlHoldMask)
            {
                _girlHoldMoveFromY = oldTrackY;
                _girlHoldMoveToY = GirlHoldTrackY();
                _girlHoldMoveAt = now;
                _girlHoldMoveUntil = now + (_girlHoldMoveToY >= _girlHoldMoveFromY
                    ? GirlJumpRise : GirlDownStrike);
            }

            // Hold 中受击时先完整播放 air_press_hurt/hurt，不能在下一帧
            // 扫描活动 Hold 时立刻被 press 覆盖。
            if (now < _girlHurtUntil)
            {
                _girlActionUntil = Mathf.Max(_girlActionUntil, _girlHurtUntil);
            }
            else if (!_girlActionLoop || _girlAction != press || oldMask == 0)
                StartGirlAction(press, now, _girlHoldUntil, loop: true);
            else
                _girlActionUntil = _girlHoldUntil;
        }

        _girlDodgePoseAir = GirlIsAir();
        _girlDodgeUsed = false;
    }

    static bool GirlDoubleCandidate(PlayNote n)
        => n != null && n.NeedsHit && !Flight.CenterLane(n)
            && n.Category is NoteCategory.Monster or NoteCategory.Boss or NoteCategory.Ghost;

    /// <summary>
    /// 官机把同刻上下轨敌人当作一次双押表现。必须等两颗都已成功判定，
    /// 手动模式只按中一边时不能错误切入 double_hit。
    /// </summary>
    bool TryGirlDoubleGroup(PlayNote n, out int group)
    {
        group = n?.Id ?? 0;
        if (_session == null || !GirlDoubleCandidate(n)) return false;
        foreach (var other in _session.Notes)
        {
            if (other.Id == n.Id || !GirlDoubleCandidate(other)) continue;
            if (other.Pathway == n.Pathway) continue;
            if (System.Math.Abs(other.TimeSec - n.TimeSec) > 0.002m) continue;
            var grade = _session.GradeOf(other.Id);
            if (grade is not (Judge.Grade.Perfect or Judge.Grade.Great)) continue;
            group = Mathf.Min(n.Id, other.Id);
            return true;
        }
        return false;
    }

    int ActiveGirlHoldMask(float now, out float until)
    {
        until = 0f;
        if (_session == null) return 0;
        var mask = 0;
        foreach (var n in _session.HoldNotes)
        {
            if (n.Category != NoteCategory.Hold || now < (float)n.TimeSec || now >= (float)n.EndSec)
                continue;
            var grade = _session.GradeOf(n.Id);
            if (grade is not (Judge.Grade.Perfect or Judge.Grade.Great))
                continue;
            mask |= 1 << (n.Pathway == 1 ? 1 : 0);
            until = Mathf.Max(until, (float)n.EndSec);
        }
        return mask;
    }

    string ExistingGirlGhostClip(params string[] names)
    {
        var data = _girlGhost?.Skeleton?.Data;
        if (data == null) return null;
        foreach (var name in names)
            if (!string.IsNullOrEmpty(name) && data.FindAnimation(name) != null)
                return name;
        return null;
    }

    void PlayGirlHoldGhost(PlaySession.JudgeHit hit, float now)
    {
        if (_girlGhost == null || _girlGhostRoot == null) return;
        var n = hit.Note;
        // 官机的副键虚影是从当前 Hold 姿态分出的单次残影。每次打击都重新
        // 以主角色 Hold 轨道为起点，不能继承上一段虚影自己的空中位置。
        _girlGhostFromY = GirlHoldTrackY();
        _girlGhostToY = n.Category == NoteCategory.Masher
            ? GirlDoubleHoldY
            : n.Pathway == 1 ? GirlGroundY + GirlAirLift : GirlGroundY;
        _girlGhostCenterLocked = n.Category == NoteCategory.Masher;
        _girlGhostMoveDuration = _girlGhostToY > _girlGhostFromY + 0.001f
            ? GirlJumpRise
            : _girlGhostToY < _girlGhostFromY - 0.001f ? GirlDownStrike : 0f;
        var perfect = hit.Grade == Judge.Grade.Perfect;
        var seq = hit.MashIndex > 0
            ? hit.MashIndex
            : n.Pathway == 1 ? ++_girlGhostAirHit : ++_girlGhostRoadHit;
        var count = perfect ? 4 : 3;
        var index = (seq - 1) % count + 1;
        var lane = n.Pathway == 1 ? "air" : "road";
        var grade = perfect ? "perfect" : "great";
        var clip = n.Pathway != 1 && _girlGhostFromY > GirlGroundY + 0.001f
            ? ExistingGirlGhostClip("down_hit",
                lane + "_hit_" + grade + "_" + index,
                lane + "_hit_" + grade + "_1", "run")
            : ExistingGirlGhostClip(
                lane + "_hit_" + grade + "_" + index,
                lane + "_hit_" + grade + "_1",
                n.Pathway == 1 ? "jump" : "run");
        if (string.IsNullOrEmpty(clip)) return;
        _girlGhostAction = clip;
        _girlGhostAt = now;
        _girlGhostRoot.position = new Vector3(
            GirlTrackX * worldScale, _girlGhostFromY * worldScale, -3.19f);
        _girlGhostRoot.gameObject.SetActive(true);
        _girlGhost.PoseTime(clip, 0f, false);
        _girlGhost.SetMulA(GirlHoldGhostAlpha);
    }

    void TickGirlGhost(float now)
    {
        if (_girlGhost == null || _girlGhostRoot == null || string.IsNullOrEmpty(_girlGhostAction)) return;
        var anim = _girlGhost.Skeleton?.Data?.FindAnimation(_girlGhostAction);
        var age = now - _girlGhostAt;
        if (anim == null || age < 0f || age > anim.Duration)
        {
            HideGirlGhost();
            return;
        }
        var u = Mathf.Clamp01(age / Mathf.Max(0.01f, anim.Duration));
        var move = _girlGhostMoveDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / _girlGhostMoveDuration))
            : 1f;
        var ghostY = Mathf.Lerp(_girlGhostFromY, _girlGhostToY, move);
        _girlGhostRoot.position = new Vector3(
            GirlTrackX * worldScale,
            ghostY * worldScale,
            -3.19f);
        _girlGhost.transform.localPosition = Vector3.zero;
        _girlGhost.PoseTime(_girlGhostAction, age, false);
        if (_girlGhostCenterLocked)
            LockGirlCenterHeight(_girlGhost);
        // PoseTime 会恢复 Skeleton.A，因此每帧姿势计算后重新施加虚影透明度。
        // 官机幽灵贴图自带冷色，再加轻微尾部淡出，让副键残影更明确。
        _girlGhost.SetMulA(GirlHoldGhostAlpha * Mathf.Lerp(1f, 0.45f, u));
    }

    void HideGirlGhost()
    {
        _girlGhostAction = "";
        _girlGhostCenterLocked = false;
        if (_girlGhost != null) _girlGhost.transform.localPosition = Vector3.zero;
        if (_girlGhostRoot != null) _girlGhostRoot.gameObject.SetActive(false);
    }
}

