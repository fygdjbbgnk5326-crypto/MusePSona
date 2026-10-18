using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>官机角色 Spine 的资源生命周期、轨道高度与空中状态。</summary>
public sealed partial class PlayDirector
{
    // 官机 layout_runtime（scene_01 / scene_03）：角色与独立影子常态根节点均为 (-4.86, -1.90)。
    const float GirlTrackX = -4.86f;
    const float GirlGroundY = -1.90f;
    // 官机单上轨 Hold 稳定段：isAir=1 / isAirPressing=1 时 girl_pos_y 恒为 0.3205。
    const float GirlAirHoldY = 0.3205f;
    // 官机双押稳定段：isAir=0 / isJumping=0 / isAirPressing=1 时 girl_pos_y 恒为 -0.7900。
    const float GirlDoubleHoldY = -0.7900f;
    // 官机空中攻击采样最高根节点 y=0.3607，相对地面根节点抬升 2.2607。
    const float GirlAirLift = 2.2607f;
    // 官机/转换器共同使用的空中窗口：180ms 上升，400ms 前有效滞空，500ms 落地。
    const float GirlJumpRise = 0.18f;
    const float GirlAirHold = 0.40f;
    const float GirlAirborne = 0.50f;
    // 官机 down_hit：触发后约两帧半从空中高度快速落到地面。
    const float GirlDownStrike = 0.065f;
    // 官机 *_ghost 贴图已自带暗色虚影，额外只需轻微压透明度。
    const float GirlHoldGhostAlpha = 0.82f;

    string _girlAction = "";
    float _girlActionAt;
    float _girlActionUntil;
    bool _girlActionLoop;
    float _girlHurtUntil = -99f;
    int _girlAirHit;
    int _girlRoadHit;
    int _girlDoubleHit;
    readonly HashSet<int> _girlDoubleGroupsPlayed = new();
    bool _girlDodgePoseAir;
    bool _girlDodgeUsed;
    float _girlAirborneAt = -99f;
    float _girlAirborneUntil = -99f;
    float _girlDownStrikeAt = -99f;
    float _girlDownStrikeUntil = -99f;
    float _girlDownStrikeFrom;
    float _girlMasherPoseUntil = -99f;
    float _girlDoublePoseUntil = -99f;
    float _girlDoublePoseY = GirlGroundY;
    float _girlMeleePoseUntil = -99f;
    int _girlHoldMask;
    int _girlPrimaryHoldLane = -1;
    float _girlHoldUntil;
    float _girlHoldMoveAt = -99f;
    float _girlHoldMoveUntil = -99f;
    float _girlHoldMoveFromY;
    float _girlHoldMoveToY;
    Transform _girlGhostRoot;
    MdSpineView _girlGhost;
    string _girlGhostAction = "";
    float _girlGhostAt;
    int _girlGhostAirHit;
    int _girlGhostRoadHit;
    float _girlGhostFromY = GirlGroundY;
    float _girlGhostToY = GirlGroundY;
    float _girlGhostMoveDuration;
    bool _girlGhostCenterLocked;

    readonly struct CharPick
    {
        public readonly string Scene;
        public readonly string Key;
        public readonly string Label;
        public CharPick(string scene, string key, string label)
        {
            Scene = scene;
            Key = key;
            Label = label;
        }
    }

    void CollectChars()
    {
        _chars.Clear();
        var root = Path.Combine(Application.streamingAssetsPath, "MdLooks", "spine");
        if (!Directory.Exists(root)) return;
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var sceneDir in Directory.GetDirectories(root))
        {
            var scene = Path.GetFileName(sceneDir);
            foreach (var dir in Directory.GetDirectories(sceneDir))
            {
                var key = Path.GetFileName(dir);
                if (key == null || !key.StartsWith("char_", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (key.IndexOf("ghost", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (key.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (!seen.Add(key)) continue;
                var label = key;
                var cut = key.IndexOf('_', 5);
                if (cut > 0 && cut + 1 < key.Length) label = key[(cut + 1)..];
                _chars.Add(new CharPick(scene, key, label));
            }
        }
        _chars.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        if (_charIndex >= _chars.Count) _charIndex = 0;
    }

    void CycleChar(int delta)
    {
        if (_chars.Count == 0) return;
        _charIndex = (_charIndex + delta) % _chars.Count;
        if (_charIndex < 0) _charIndex += _chars.Count;
        EnsureGirl(force: true);
    }

    void EnsureGirl(bool force = false)
    {
        if (!TempCharSwitch) return;
        if (_chars.Count == 0)
        {
            DropGirl();
            return;
        }
        if (_charIndex < 0 || _charIndex >= _chars.Count) _charIndex = 0;
        var pick = _chars[_charIndex];
        if (!force && _girl != null && _girlRoot != null && _girlRoot.name == "GirlTest")
            return;
        DropGirl();
        var pack = MdSpine.LoadNamed(pick.Scene, pick.Key);
        if (pack == null) return;
        var stage = transform.Find("CharStage");
        if (stage == null)
        {
            var host = new GameObject("CharStage");
            host.transform.SetParent(transform, false);
            stage = host.transform;
        }
        var go = new GameObject("GirlTest");
        go.transform.SetParent(stage, false);
        _girlRoot = go.transform;
        _girl = MdSpineView.Attach(_girlRoot, pack);
        // Character 排序层，不跟 Boss（Default / 3300）同一通道。
        _girl.SetSortLayer("Character");
        _girl.SetLayer(3600, 10);

        // 官机给梦游少女/兔女郎另带一套 *_ghost SkeletonData，正是按住一轨时
        // 另一轨打击所用的暗色虚影；不要用普通角色简单降透明度代替。
        var ghostPack = MdSpine.LoadNamed(pick.Scene, pick.Key + "_ghost");
        if (ghostPack != null)
        {
            var ghostGo = new GameObject("GirlHoldGhost");
            ghostGo.transform.SetParent(stage, false);
            _girlGhostRoot = ghostGo.transform;
            _girlGhost = MdSpineView.Attach(_girlGhostRoot, ghostPack);
            _girlGhost.SetSortLayer("Character");
            _girlGhost.SetLayer(3599, 9);
            _girlGhostRoot.gameObject.SetActive(false);
        }
        _girlClock = 0f;
        ClearGirlAction();
        PlaceGirl();
    }

    void DropGirl()
    {
        if (_girlRoot != null) Destroy(_girlRoot.gameObject);
        if (_girlGhostRoot != null) Destroy(_girlGhostRoot.gameObject);
        _girlRoot = null;
        _girl = null;
        _girlGhostRoot = null;
        _girlGhost = null;
        _girlGhostAction = "";
        _girlGhostAt = 0f;
        _girlAirborneAt = _girlAirborneUntil = -99f;
        _girlDownStrikeAt = _girlDownStrikeUntil = -99f;
        _girlDownStrikeFrom = 0f;
        _girlMasherPoseUntil = -99f;
        _girlDoublePoseUntil = -99f;
        _girlDoublePoseY = GirlGroundY;
        _girlMeleePoseUntil = -99f;
        _girlHoldMoveAt = _girlHoldMoveUntil = -99f;
        _girlHoldMoveFromY = _girlHoldMoveToY = GirlGroundY;
        _girlGhostFromY = _girlGhostToY = GirlGroundY;
        _girlGhostMoveDuration = 0f;
        _girlGhostCenterLocked = false;
        _girlHurtUntil = -99f;
        ClearGirlAction();
    }

    static float GirlHoldTrackY(int mask)
        => mask switch
        {
            2 => GirlAirHoldY,
            3 => GirlDoubleHoldY,
            _ => GirlGroundY
        };

    float GirlHoldTrackY() => GirlHoldTrackY(_girlHoldMask);

    void PlaceGirl()
    {
        if (_girlRoot == null) return;
        // 单轨仍使用官机常态根坐标。双押 Hold 时把角色相对地面轨抬到 Boss
        // 的中轨；不能只换 press 动画，否则兔女郎等皮肤仍会贴在下轨。
        var now = GirlNow();
        var trackY = _girlHoldMask != 0
            ? GirlHoldTrackY()
            : GirlDoublePoseActive(now) ? _girlDoublePoseY
            : GirlCenterPoseActive(now) ? GirlDoubleHoldY : GirlGroundY;
        // 双押/中轨攻击期间位置锁优先级最高。此前残留的 Hold 上升插值会
        // 覆盖 GirlDoubleHoldY，使 double_hit 看起来被固定在上轨。
        var centerLocked = GirlDoublePoseActive(now) || GirlCenterPoseActive(now);
        if (!centerLocked && now >= _girlHoldMoveAt && now < _girlHoldMoveUntil)
        {
            var u = Mathf.Clamp01((now - _girlHoldMoveAt)
                / Mathf.Max(0.001f, _girlHoldMoveUntil - _girlHoldMoveAt));
            trackY = Mathf.Lerp(_girlHoldMoveFromY, _girlHoldMoveToY,
                Mathf.SmoothStep(0f, 1f, u));
        }
        _girlRoot.position = new Vector3(
            GirlTrackX * worldScale,
            (trackY + GirlTrackLift()) * worldScale,
            -3.2f);
    }

    float GirlTrackLift()
    {
        if (_girlHoldMask != 0) return 0f;
        var now = GirlNow();
        if (GirlCenterPoseActive(now) || GirlDoublePoseActive(now)) return 0f;
        if (now >= _girlDownStrikeAt && now < _girlDownStrikeUntil)
        {
            var u = Mathf.Clamp01((now - _girlDownStrikeAt) / GirlDownStrike);
            return Mathf.Lerp(_girlDownStrikeFrom, 0f, Mathf.SmoothStep(0f, 1f, u));
        }
        return GirlAirLiftAt(now, _girlAirborneAt, _girlAirborneUntil);
    }

    static float GirlAirLiftAt(float now, float airborneAt, float airborneUntil)
    {
        if (now < airborneAt || now >= airborneUntil) return 0f;
        var age = now - airborneAt;
        if (age < GirlJumpRise)
            return GirlAirLift * Mathf.SmoothStep(0f, 1f, age / GirlJumpRise);
        // 官机按“最近一次空中打击 + 400ms”保持空中姿态。airborneUntil 会被
        // 连续空中打击向后刷新，但 airborneAt 不变，所以不会重复播放上升段。
        var fallAt = airborneUntil - (GirlAirborne - GirlAirHold);
        if (now < fallAt)
            return GirlAirLift;
        return GirlAirLift * (1f - Mathf.SmoothStep(0f, 1f,
            (now - fallAt) / (GirlAirborne - GirlAirHold)));
    }

    void BeginGirlAirborne(float now, bool refresh = false)
    {
        if (now < _girlAirborneUntil)
        {
            // 只有真正的空中打击刷新滞空尾点；闪避齿轮等空键仍是 no-op。
            // 不修改 airborneAt，故连续攻击只续空，不会二段跳或重复上升。
            if (refresh)
                _girlAirborneUntil = Mathf.Max(_girlAirborneUntil, now + GirlAirborne);
            return;
        }
        _girlDownStrikeAt = _girlDownStrikeUntil = -99f;
        _girlDownStrikeFrom = 0f;
        _girlAirborneAt = now;
        _girlAirborneUntil = now + GirlAirborne;
    }

    void BeginGirlDownStrike(float now)
    {
        _girlDownStrikeFrom = GirlAirLiftAt(now, _girlAirborneAt, _girlAirborneUntil);
        _girlAirborneAt = _girlAirborneUntil = -99f;
        _girlDownStrikeAt = now;
        _girlDownStrikeUntil = now + GirlDownStrike;
    }

    void EndGirlAirborne()
    {
        _girlAirborneAt = _girlAirborneUntil = -99f;
        _girlDownStrikeAt = _girlDownStrikeUntil = -99f;
        _girlDownStrikeFrom = 0f;
    }

    void TickGirl()
    {
        if (!TempCharSwitch) return;
        if (_girl == null)
        {
            EnsureGirl();
            if (_girl == null) return;
        }
        if (!_paused) _girlClock += Time.deltaTime;
        PlaceGirl();
        // PoseTime 会把动画内的根骨位移重新写回；每帧先清除上一帧补偿。
        _girl.transform.localPosition = Vector3.zero;
        var now = GirlNow();
        TickGirlGhost(now);
        if (!string.IsNullOrEmpty(_girlAction))
        {
            var action = _girl.Skeleton?.Data?.FindAnimation(_girlAction);
            if (action == null)
            {
                ClearGirlAction();
            }
            else
            {
                var age = Mathf.Max(0f, now - _girlActionAt);
                var keep = _girlActionLoop ? now < _girlActionUntil : age <= action.Duration;
                if (keep)
                {
                    if (_girlActionLoop)
                        _girl.PoseLoop(_girlAction, age);
                    else
                        _girl.PoseTime(_girlAction, age, false);
                    if (GirlCenterPoseActive(now) || GirlDoublePoseActive(now))
                        LockGirlCenterHeight(_girl);
                    return;
                }
                ClearGirlAction();
            }
        }

        // 官机常态为 run；只有缺少 run 的角色包才退到 standby/idle。
        var idle = _girl.Skeleton?.Data?.FindAnimation("run")
            ?? _girl.Skeleton?.Data?.FindAnimation("standby")
            ?? _girl.Skeleton?.Data?.FindAnimation("idle")
            ?? _girl.Skeleton?.Data?.FindAnimation("wait");
        var clip = idle != null ? idle.Name : null;
        if (string.IsNullOrEmpty(clip) && _girl.Skeleton?.Data?.Animations.Count > 0)
            clip = _girl.Skeleton.Data.Animations.Items[0].Name;
        if (string.IsNullOrEmpty(clip)) return;
        var dur = _girl.Skeleton.Data.FindAnimation(clip)?.Duration ?? 1f;
        var t = dur > 0.01f ? _girlClock % dur : 0f;
        _girl.PoseLoop(clip, t);
    }

    bool GirlCenterPoseActive(float now)
        => now < _girlMasherPoseUntil || now < _girlMeleePoseUntil;

    bool GirlDoublePoseActive(float now) => now < _girlDoublePoseUntil;

    string ExistingGirlHoldClip(int mask)
    {
        // 官机 RoleTrace 实录：地面、空中和双押 Hold 的主轨 Track 0 全部是
        // press(loop=1)，轨道差异只体现在 girl_pos_y / isAirPressing。
        // up_press_hit/down_press_hit 并不是成功 Hold 的主循环，不能按轨替换。
        return ExistingGirlClip("press",
            mask == 2 ? "up_press_hit" : "down_press_hit", "run");
    }

    static void LockGirlCenterHeight(MdSpineView view)
    {
        var root = view?.Skeleton?.RootBone;
        if (root == null) return;
        // 只抵消根骨漂移。double_hit 的 girl_Y 位移正是官机从地面根节点
        // 抬到中轨的动作组成，不能把它一起抵消。
        var p = view.transform.localPosition;
        p.y = -(root.Y - root.Data.Y);
        view.transform.localPosition = p;
    }

    float GirlNow()
        => _session != null ? Clock() - _session.DelaySec : _girlClock;

    void ClearGirlAction()
    {
        _girlAction = "";
        _girlActionAt = 0f;
        _girlActionUntil = 0f;
        _girlActionLoop = false;
        _girlHurtUntil = -99f;
        _girlMasherPoseUntil = -99f;
        _girlDoublePoseUntil = -99f;
        _girlDoublePoseY = GirlGroundY;
        _girlMeleePoseUntil = -99f;
        _girlHoldMask = 0;
        _girlPrimaryHoldLane = -1;
        _girlHoldUntil = 0f;
        _girlHoldMoveAt = _girlHoldMoveUntil = -99f;
        HideGirlGhost();
    }

    bool GirlIsAir()
    {
        if (_girlHoldMask == 2) return true;
        if (_girlHoldMask is 1 or 3) return false;
        if (GirlNow() < _girlDownStrikeUntil) return true;
        if (GirlNow() < _girlAirborneUntil) return true;
        if (string.IsNullOrEmpty(_girlAction)) return false;
        if (_girlAction == "down_hit" || _girlAction == "down_press_hit") return false;
        return _girlAction == "jump" || _girlAction == "jump_hurt"
            || _girlAction.StartsWith("air_hit_", System.StringComparison.Ordinal);
    }

    /// <summary>
    /// 官机 AutoPlayReplay.InjectGearLogic 在遍历整批齿轮前只采样一次 GirlAir，
    /// 因此同帧上下轨齿轮不能因第一个动作改变姿态后再触发第二次。
    /// </summary>

    void DrawTempCharButtons()
    {
        var h = 32f;
        var gap = 6f;
        var restartW = 72f;
        var n = Mathf.Max(1, _chars.Count);
        var w = 88f;
        var total = restartW + gap + n * w + (n - 1) * gap;
        var x = Mathf.Max(12f, (Screen.width - total) * 0.5f);
        var y = Screen.height - (TempMapKeys ? 16f : 44f + 16f + 8f) - h;
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.95f, 0.55f, 0.45f);
        if (GuiClick(new Rect(x, y, restartW, h), "重开"))
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
        x += restartW + gap;
        if (_chars.Count == 0)
        {
            GUI.backgroundColor = new Color(0.75f, 0.78f, 0.85f);
            GUI.Box(new Rect(x, y, 160f, h), "无角色包");
            GUI.backgroundColor = prev;
            return;
        }
        for (var i = 0; i < _chars.Count; i++)
        {
            var on = i == _charIndex;
            GUI.backgroundColor = on ? new Color(1f, 0.85f, 0.2f) : new Color(0.75f, 0.78f, 0.85f);
            if (GuiClick(new Rect(x, y, w, h), _chars[i].Label))
            {
                _charIndex = i;
                EnsureGirl(force: true);
            }
            x += w + gap;
        }
        GUI.backgroundColor = prev;
    }
}

