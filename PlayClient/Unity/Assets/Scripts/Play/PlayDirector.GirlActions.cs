using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>角色手动输入、判定结果与攻击/受伤动画映射。</summary>
public sealed partial class PlayDirector
{
    string ExistingGirlClip(params string[] names)
    {
        var data = _girl?.Skeleton?.Data;
        if (data == null) return null;
        foreach (var name in names)
            if (!string.IsNullOrEmpty(name) && data.FindAnimation(name) != null)
                return name;
        return null;
    }

    void StartGirlAction(string clip, float now, float until = 0f, bool loop = false)
    {
        if (string.IsNullOrEmpty(clip)) return;
        _girlAction = clip;
        _girlActionAt = now;
        _girlActionUntil = until;
        _girlActionLoop = loop;
    }

    /// <summary>
    /// 手动模式空打时没有 JudgeHit，按官机基础操作补角色反馈。
    /// 真正命中物件仍只走 PlayGirlHit，避免覆盖 Hold、双押和连打动作。
    /// </summary>
    void PlayGirlManualInput(bool air)
    {
        if (_girl == null) return;
        var now = GirlNow();
        if (now < _girlHurtUntil) return;

        // Hold 期间主角色必须保持 press。副轨命中物件时由 PlayGirlHit
        // 生成虚影；没有物件的空打不应中断 Hold 或凭空生成虚影。
        if (_girlHoldMask != 0) return;

        _girlMasherPoseUntil = -99f;
        _girlDoublePoseUntil = -99f;
        _girlMeleePoseUntil = -99f;

        if (air)
        {
            if (!GirlIsAir())
            {
                BeginGirlAirborne(now);
                StartGirlAction(ExistingGirlClip("jump", "air_hit_perfect_1", "run"), now);
                return;
            }

            // 已在空中时继续按空轨键，按普通空中打击序列播放并刷新滞空，
            // 避免连续测试动作时角色在两次按键之间突然落地。
            var index = (++_girlAirHit - 1) % 4 + 1;
            BeginGirlAirborne(now, refresh: true);
            StartGirlAction(ExistingGirlClip(
                "air_hit_perfect_" + index,
                "air_hit_perfect_1", "jump", "run"), now);
            return;
        }

        if (GirlIsAir())
        {
            BeginGirlDownStrike(now);
            StartGirlAction(ExistingGirlClip("down_hit", "road_hit_perfect_1", "run"), now);
            return;
        }

        var roadIndex = (++_girlRoadHit - 1) % 4 + 1;
        EndGirlAirborne();
        StartGirlAction(ExistingGirlClip(
            "road_hit_miss", "atk_miss",
            "road_hit_perfect_" + roadIndex,
            "road_hit_perfect_1", "run"), now);
    }

    /// <summary>把运行时判定映射到官机角色包内的原生动画名。</summary>
    void PlayGirlHit(PlaySession.JudgeHit hit)
    {
        if (_girl == null || hit.Note == null) return;
        var n = hit.Note;
        var now = GirlNow();
        // 手动闪避齿轮只写 PASS：不能先清理当前攻击/连打/双押姿态，
        // 也不能进入副轨 ghost 或受击分支。角色动作已经由玩家按键决定。
        if (!autoplay && n.Category == NoteCategory.Gear
            && hit.Grade == Judge.Grade.Pass)
            return;
        var doubleGroup = n.Id;
        var pairedDouble = hit.Grade is Judge.Grade.Perfect or Judge.Grade.Great
            && TryGirlDoubleGroup(n, out doubleGroup);
        if (n.Category != NoteCategory.Masher && _girlHoldMask == 0)
            _girlMasherPoseUntil = -99f;
        if (n.BmsId != "0E" && !pairedDouble && _girlHoldMask == 0)
            _girlDoublePoseUntil = -99f;
        if (n.BmsId is not ("11" or "12") && _girlHoldMask == 0)
            _girlMeleePoseUntil = -99f;

        if (hit.Grade == Judge.Grade.Pass && n.Category == NoteCategory.Gear)
        {
            // 这里只剩自动演奏：InjectGearLogic 为展示闪避动作补输入。
            // 手动模式已在上方返回，只能由玩家 D/F/J/K 改变角色姿态。
            // 是否按住长条取自谱面判定状态，不再依赖当前动画；副轨动画也不会让
            // Hold 内的密集上下齿轮误触发 jump/down_hit。
            if (_girlHoldMask != 0 || _girlDodgeUsed)
                return;
            var gearAir = n.Pathway == 1;
            if (_girlDodgePoseAir != gearAir)
                return;
            var dodge = gearAir
                ? ExistingGirlClip("down_hit", "press", "run")
                : ExistingGirlClip("jump", "run");
            if (gearAir) EndGirlAirborne();
            else BeginGirlAirborne(now);
            _girlDodgeUsed = true;
            StartGirlAction(dodge, now);
            return;
        }

        // 被动收集物（10/23/红心）没有独立的收集动画；24 是必须另按的
        // Hold 星，但漏掉时同样不播受伤。官机只检查判定瞬间的角色
        // 姿态：同轨时被动收集，不重播攻击也不刷新滞空；漏掉收集物同样不播
        // hurt。姿态不符时才等价于补一次对应轨道输入，动画由下方在 Hold
        // 分流之后处理（Hold 内的副轨 23 仍应由 ghost 表现）。
        if (n.Category is NoteCategory.Music or NoteCategory.Heart
            && hit.Grade == Judge.Grade.Miss)
            return;

        if (hit.Grade == Judge.Grade.Miss)
        {
            var noteAir = n.Pathway == 1;
            var center = Flight.CenterLane(n);
            // 手动模式不能按漏击物件的 pathway 把角色强制切到另一轨受伤。
            // 同一批结算使用 BeginGirlHitBatch 预先采样的姿态，避免第一颗
            // Miss 改变状态后让同帧另一轨也错误触发碰撞。
            if (!autoplay && !center && _girlDodgePoseAir != noteAir)
                return;
            var hurtAir = autoplay ? noteAir : _girlDodgePoseAir;
            if (now < _girlHurtUntil)
                return;
            var wasAirAttack = !string.IsNullOrEmpty(_girlAction)
                && _girlAction.StartsWith("air_hit_", System.StringComparison.Ordinal);
            var wasAirPress = hurtAir && (_girlHoldMask & 2) != 0;
            if (hurtAir) BeginGirlAirborne(now);
            else EndGirlAirborne();
            // 官机角色包区分四种受击上下文：地面碰撞 hurt、跳跃碰撞
            // jump_hurt、空中攻击碰撞 air_hit_hurt、上轨 Hold 碰撞
            // air_press_hurt。road_hit_miss/atk_miss 是空挥，不能用于受伤。
            var hurt = !hurtAir
                ? ExistingGirlClip("hurt", "road_hit_miss", "atk_miss")
                : wasAirPress
                    ? ExistingGirlClip("air_press_hurt", "jump_hurt", "air_hit_hurt", "hurt")
                    : wasAirAttack
                        ? ExistingGirlClip("air_hit_hurt", "jump_hurt", "hurt")
                        : ExistingGirlClip("jump_hurt", "air_hit_hurt", "hurt");
            var hurtDuration = !string.IsNullOrEmpty(hurt)
                ? _girl.Skeleton?.Data?.FindAnimation(hurt)?.Duration ?? 0.25f
                : 0.25f;
            _girlHurtUntil = now + Mathf.Max(0.05f, hurtDuration);
            _girlMasherPoseUntil = -99f;
            _girlDoublePoseUntil = -99f;
            _girlMeleePoseUntil = -99f;
            HideGirlGhost();
            StartGirlAction(hurt, now);
            return;
        }

        if (hit.Grade is not (Judge.Grade.Perfect or Judge.Grade.Great)) return;

        if (_girlHoldMask != 0)
        {
            // 官机按输入轨分流：主轨继续 press；副轨敌人和条内 23 音符由
            // *_ghost 播放正常 hit。第二根 Hold 只切双押位置，不生成虚影。
            var secondaryHit = n.Category != NoteCategory.Heart
                && n.Category != NoteCategory.Gear
                && n.Category != NoteCategory.Hold
                && n.BmsId != "0E"
                && n.Pathway != _girlPrimaryHoldLane;
            if (secondaryHit)
                PlayGirlHoldGhost(hit, now);
            return;
        }

        if (n.Category is NoteCategory.Music or NoteCategory.Heart)
        {
            var airborne = now < _girlAirborneUntil;
            if (n.Pathway == 1)
            {
                if (!airborne)
                {
                    BeginGirlAirborne(now);
                    StartGirlAction(ExistingGirlClip("jump", "run"), now);
                }
            }
            else if (airborne)
            {
                BeginGirlDownStrike(now);
                StartGirlAction(ExistingGirlClip("down_hit", "run"), now);
            }
            return;
        }

        if (n.BmsId == "0E" || pairedDouble)
        {
            // 必须在地轨 down_hit / 空轨 jump 分流之前处理。手动模式若先
            // 打上轨再打地轨，第二击否则会被 groundEnemy 提前 return，
            // 导致双押动作继承上轨高度。
            if (pairedDouble && !_girlDoubleGroupsPlayed.Add(doubleGroup)) return;
            if (!pairedDouble) _girlDoubleGroupsPlayed.Add(n.Id);
            var i = (++_girlDoubleHit - 1) % 2 + 1;
            var doubleAttack = ExistingGirlClip("double_hit_" + i, "double_hit_1", "run");
            EndGirlAirborne();
            _girlHoldMoveAt = _girlHoldMoveUntil = -99f;
            // double_hit 动画内部的 girl_Y 已从地面根节点抬到两轨中线；
            // 外层若也设为 GirlDoubleHoldY，会叠加成上轨。
            _girlDoublePoseY = GirlGroundY;
            var doubleDuration = !string.IsNullOrEmpty(doubleAttack)
                ? _girl?.Skeleton?.Data?.FindAnimation(doubleAttack)?.Duration ?? 0.15f
                : 0.15f;
            _girlDoublePoseUntil = now + Mathf.Max(0.05f, doubleDuration);
            StartGirlAction(doubleAttack, now);
            return;
        }

        // 官机从空中接地面敌人时先播 down_hit，并从当前高度快速下砍落地；
        // 不能直接清空高度后切 road_hit，否则会出现生硬瞬移。
        var groundEnemy = n.Pathway != 1 && n.BmsId != "0E"
            && n.BmsId is not ("11" or "12")
            && n.Category is NoteCategory.Monster or NoteCategory.Boss
                or NoteCategory.Ghost;
        if (groundEnemy && now < _girlAirborneUntil)
        {
            BeginGirlDownStrike(now);
            StartGirlAction(ExistingGirlClip("down_hit", "road_hit_perfect_1", "run"), now);
            return;
        }

        if (n.Category == NoteCategory.Hold)
        {
            var tail = now >= (float)n.EndSec - 0.02f;
            if (tail)
            {
                // 官机成功收尾无论上下轨都直接回 run；air_press_end 属于其它
                // 中断/受伤路径，自动 Perfect Hold 的实录中不会在尾点出现。
                StartGirlAction(ExistingGirlClip("run"), now);
            }
            else
            {
                // 手动双押时两个按键会各自 FlushHits。第二根 Hold 判定到达后
                // 立即把当前轨合入 mask，不能等下一帧扫描，否则第一根若是
                // 空轨，角色会整段沿用 mask=2 而看起来被固定在上轨。
                var laneBit = 1 << (n.Pathway == 1 ? 1 : 0);
                if ((_girlHoldMask & laneBit) == 0)
                {
                    var fromY = _girlHoldMask != 0
                        ? GirlHoldTrackY()
                        : GirlGroundY + GirlTrackLift();
                    _girlHoldMask |= laneBit;
                    _girlHoldUntil = Mathf.Max(_girlHoldUntil, (float)n.EndSec);
                    _girlHoldMoveFromY = fromY;
                    _girlHoldMoveToY = GirlHoldTrackY();
                    _girlHoldMoveAt = now;
                    _girlHoldMoveUntil = now + (_girlHoldMoveToY >= fromY
                        ? GirlJumpRise : GirlDownStrike);
                    if (_girlPrimaryHoldLane < 0)
                        _girlPrimaryHoldLane = n.Pathway == 1 ? 1 : 0;
                }
                var press = ExistingGirlHoldClip(_girlHoldMask != 0
                    ? _girlHoldMask
                    : 1 << (n.Pathway == 1 ? 1 : 0));
                if (_girlActionLoop && _girlAction == press && now < _girlActionUntil)
                    _girlActionUntil = Mathf.Max(_girlActionUntil, (float)n.EndSec);
                else
                    StartGirlAction(press, now, (float)n.EndSec, loop: true);
            }
            return;
        }

        // 连打物件不是 Hold：官机按实际连打次数依次播放 hit_1..4，
        // 到第 5 击重新从 hit_1 开始。判定事件携带 MashIndex，不能用
        // Update 帧数或角色全局攻击计数推算，否则自动播放补 tick 时会乱序。
        if (n.Category == NoteCategory.Masher)
        {
            var perfectMash = hit.Grade == Judge.Grade.Perfect;
            var mashCount = perfectMash ? 4 : 3;
            var mashIndex = ((Mathf.Max(1, hit.MashIndex) - 1) % mashCount) + 1;
            var mashLane = n.Pathway == 1 ? "air" : "road";
            var mashGrade = perfectMash ? "perfect" : "great";
            var mashAttack = ExistingGirlClip(
                mashLane + "_hit_" + mashGrade + "_" + mashIndex,
                mashLane + "_hit_" + mashGrade + "_1",
                mashLane == "air" ? "jump" : "run");
            // 连打物件本身固定在两轨中线；角色的连续打击也固定到同一中轨，
            // 不再根据谱面 pathway 叠加上跳/下砍位移。
            EndGirlAirborne();
            var mashDuration = !string.IsNullOrEmpty(mashAttack)
                ? _girl?.Skeleton?.Data?.FindAnimation(mashAttack)?.Duration ?? 0.15f
                : 0.15f;
            _girlMasherPoseUntil = now + Mathf.Max(0.05f, mashDuration);
            StartGirlAction(mashAttack, now);
            return;
        }

        if (n.BmsId is "11" or "12")
        {
            // 官机的 Boss 近战本体与判定都在两轨中线。角色仍按谱面轨选
            // air/road hit 片段，但根节点与片段根骨固定在中轨，不能随
            // pathway 突然跳到上轨或落回地轨。
            var perfectMelee = hit.Grade == Judge.Grade.Perfect;
            var meleeSeq = n.Pathway == 1 ? ++_girlAirHit : ++_girlRoadHit;
            var meleeCount = perfectMelee ? 4 : 3;
            var meleeIndex = (meleeSeq - 1) % meleeCount + 1;
            var meleeLane = n.Pathway == 1 ? "air" : "road";
            var meleeGrade = perfectMelee ? "perfect" : "great";
            var meleeAttack = ExistingGirlClip(
                meleeLane + "_hit_" + meleeGrade + "_" + meleeIndex,
                meleeLane + "_hit_" + meleeGrade + "_1",
                meleeLane == "air" ? "jump" : "run");
            EndGirlAirborne();
            var meleeDuration = !string.IsNullOrEmpty(meleeAttack)
                ? _girl?.Skeleton?.Data?.FindAnimation(meleeAttack)?.Duration ?? 0.15f
                : 0.15f;
            _girlMeleePoseUntil = now + Mathf.Max(0.05f, meleeDuration);
            StartGirlAction(meleeAttack, now);
            return;
        }

        var perfect = hit.Grade == Judge.Grade.Perfect;
        var seq = n.Pathway == 1 ? ++_girlAirHit : ++_girlRoadHit;
        var count = perfect ? 4 : 3;
        var index = (seq - 1) % count + 1;
        var lane = n.Pathway == 1 ? "air" : "road";
        var grade = perfect ? "perfect" : "great";
        var attack = ExistingGirlClip(
            lane + "_hit_" + grade + "_" + index,
            lane + "_hit_" + grade + "_1",
            lane == "air" ? "jump" : "run");
        if (n.Pathway == 1) BeginGirlAirborne(now, refresh: true);
        else EndGirlAirborne();
        StartGirlAction(attack, now);
    }
}

