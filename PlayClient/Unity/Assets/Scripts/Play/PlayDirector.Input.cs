using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>快捷键、换谱、换歌。</summary>
public sealed partial class PlayDirector
{
    void CycleForceScene(int delta)
    {
        var cur = _forceScene.Length > 0 ? _forceScene : (_scene.Length > 0 ? _scene : "scene_01");
        var n = 1;
        var m = System.Text.RegularExpressions.Regex.Match(cur, @"(\d+)");
        if (m.Success) n = int.Parse(m.Groups[1].Value);
        n = (n - 1 + delta + 12) % 12 + 1;
        _forceScene = "scene_" + n.ToString("00");
        TintScene(_forceScene);
    }

    void HandleKey(KeyCode key)
    {
        if (key == KeyCode.F1)
        {
            autoplay = !autoplay;
            return;
        }
        if (key == KeyCode.F2)
        {
            autoplay = false;
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
            return;
        }
        if (key == KeyCode.F3)
        {
            autoplay = true;
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
            return;
        }
        if (key == KeyCode.N)
        {
            CycleForceScene(1);
            return;
        }
        if (key == KeyCode.M)
        {
            CycleForceScene(-1);
            return;
        }
        if (key == KeyCode.F4 || key == KeyCode.PageDown)
        {
            CycleSong(1);
            return;
        }
        if (key == KeyCode.F8 || key == KeyCode.PageUp)
        {
            CycleSong(-1);
            return;
        }
        if (key == KeyCode.F5)
        {
            _forceScene = "";
            if (_session != null) TintScene(_session.SceneAt(Clock()));
            return;
        }
        if (key == KeyCode.F6)
        {
            if (TempCharSwitch) CycleChar(-1);
            else if (TempSceneSwitch) CycleForceScene(-1);
            return;
        }
        if (key == KeyCode.F7)
        {
            if (TempCharSwitch) CycleChar(1);
            else if (TempSceneSwitch) CycleForceScene(1);
            return;
        }
        if (key == KeyCode.R)
        {
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
            return;
        }
        if (key is >= KeyCode.Alpha1 and <= KeyCode.Alpha5)
        {
            SwitchMap(key - KeyCode.Alpha0);
            return;
        }
        if (key is >= KeyCode.Keypad1 and <= KeyCode.Keypad5)
        {
            SwitchMap(key - KeyCode.Keypad0);
            return;
        }
        if (key is KeyCode.LeftBracket or KeyCode.Minus or KeyCode.Comma)
        {
            CycleMap(-1);
            return;
        }
        if (key is KeyCode.RightBracket or KeyCode.Equals or KeyCode.Period)
        {
            CycleMap(1);
            return;
        }
        if (key == KeyCode.Space && _audio != null && _audio.clip != null)
        {
            _paused = !_paused;
            if (_paused) _audio.Pause();
            else _audio.UnPause();
            return;
        }
        if (_session == null || _paused || autoplay) return;
        var now = InputClock();
        if (key is not (KeyCode.D or KeyCode.F or KeyCode.J or KeyCode.K)) return;
        var oneKey = SceneTimeline.RulesFor(_session.SceneAt(now)).OneKey;
        var air = !oneKey && key is KeyCode.D or KeyCode.F;
        var grade = _session.HitLane(air, now);
        if (_playInputFrame != Time.frameCount)
        {
            _playInputFrame = Time.frameCount;
            _playHitLaneMask = 0;
        }
        var laneBit = 1 << (air ? 1 : 0);
        var laneAlreadyHit = (_playHitLaneMask & laneBit) != 0;
        if (grade != Judge.Grade.None)
            _playHitLaneMask |= laneBit;
        if (air) _flashAirUntil = Time.unscaledTime + 0.08f;
        else _flashRoadUntil = Time.unscaledTime + 0.08f;
        FlushHits();
        // 谱面判定事件会按物件类型选择 Hold、双押、连打等官机动作。
        // 空打没有 JudgeHit，单独播放基础输入动作，方便手动模式测试角色。
        if (grade == Judge.Grade.None && !laneAlreadyHit)
            PlayGirlManualInput(air);
    }

    int HeldPlayLaneMask(float now)
    {
        var anyAir = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.F);
        var anyRoad = Input.GetKey(KeyCode.J) || Input.GetKey(KeyCode.K);
        if (_session != null && SceneTimeline.RulesFor(_session.SceneAt(now)).OneKey)
            return anyAir || anyRoad ? 3 : 0;
        return (anyRoad ? 1 : 0) | (anyAir ? 2 : 0);
    }

    static bool IsPlayKey(KeyCode key)
        => key is KeyCode.D or KeyCode.F or KeyCode.J or KeyCode.K;

    void PressPlayKey(KeyCode key)
    {
        // 每个物理键独立锁定：J 尚未松开时 K 仍能触发下一击；来自
        // Update、IMGUI 及系统长按重复的同一次 KeyDown 只结算一次。
        if (!IsPlayKey(key) || !_playKeysHeld.Add(key)) return;
        // 游玩键的去重由按下/松开状态负责，不使用“同帧同键”限制；这样
        // 极快的松开后重按以及 J-K-J 滚键不会被帧边界吞掉。
        HandleKey(key);
    }

    void ReleasePlayKey(KeyCode key)
    {
        if (IsPlayKey(key)) _playKeysHeld.Remove(key);
    }

    bool CaptureGuiPlayKey(UnityEngine.Event e)
    {
        if (e == null || !IsPlayKey(e.keyCode)) return false;
        if (e.type == EventType.KeyDown)
        {
            PressPlayKey(e.keyCode);
            return true;
        }
        if (e.type == EventType.KeyUp)
        {
            ReleasePlayKey(e.keyCode);
            return true;
        }
        return false;
    }

    void PollPlayKey(KeyCode key)
    {
        if (Input.GetKeyDown(key)) PressPlayKey(key);
        if (Input.GetKeyUp(key)) ReleasePlayKey(key);
    }

    void OnApplicationFocus(bool focused)
    {
        // 切出窗口时 Unity 可能收不到 KeyUp，回来后必须解除物理键锁。
        if (!focused) _playKeysHeld.Clear();
    }

    void TryKey(KeyCode key)
    {
        if (key == KeyCode.None) return;
        if (_keyFrame == Time.frameCount && _keySeen == key) return;
        _keyFrame = Time.frameCount;
        _keySeen = key;
        HandleKey(key);
    }

    void PollPlayKeys()
    {
        // 游玩键必须只读取物理按下沿。IMGUI 的 KeyDown 会随系统键盘
        // 重复率连续触发，长按 Hold 时会被误判成一串单击动作。
        PollPlayKey(KeyCode.D);
        PollPlayKey(KeyCode.F);
        PollPlayKey(KeyCode.J);
        PollPlayKey(KeyCode.K);
        for (var i = 1; i <= 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
                TryKey(KeyCode.Alpha0 + i);
        }
        if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Comma))
            TryKey(KeyCode.LeftBracket);
        if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Period))
            TryKey(KeyCode.RightBracket);
        if (Input.GetKeyDown(KeyCode.F1)) TryKey(KeyCode.F1);
        if (Input.GetKeyDown(KeyCode.F2)) TryKey(KeyCode.F2);
        if (Input.GetKeyDown(KeyCode.F3)) TryKey(KeyCode.F3);
        if (Input.GetKeyDown(KeyCode.N)) TryKey(KeyCode.N);
        if (Input.GetKeyDown(KeyCode.M)) TryKey(KeyCode.M);
        if (Input.GetKeyDown(KeyCode.F4)) TryKey(KeyCode.F4);
        if (Input.GetKeyDown(KeyCode.PageDown)) TryKey(KeyCode.PageDown);
        if (Input.GetKeyDown(KeyCode.F8)) TryKey(KeyCode.F8);
        if (Input.GetKeyDown(KeyCode.PageUp)) TryKey(KeyCode.PageUp);
        if (Input.GetKeyDown(KeyCode.F5)) TryKey(KeyCode.F5);
        if (Input.GetKeyDown(KeyCode.F6)) TryKey(KeyCode.F6);
        if (Input.GetKeyDown(KeyCode.F7)) TryKey(KeyCode.F7);
        if (Input.GetKeyDown(KeyCode.R)) TryKey(KeyCode.R);
        if (Input.GetKeyDown(KeyCode.Space)) TryKey(KeyCode.Space);
    }

    void CycleMap(int delta)
    {
        if (_song == null || _song.Maps.Count == 0) return;
        var maps = _song.Maps;
        var cur = _session != null ? _session.Difficulty : maps[0];
        var i = 0;
        for (var n = 0; n < maps.Count; n++)
        {
            if (maps[n] != cur) continue;
            i = n;
            break;
        }
        var next = maps[(i + delta % maps.Count + maps.Count) % maps.Count];
        if (next == cur && maps.Count > 1)
            next = maps[(i + (delta >= 0 ? 1 : maps.Count - 1)) % maps.Count];
        SwitchMap(next);
    }

    void CollectSongs()
    {
        _songFiles.Clear();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        void AddDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*.mdm", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith("pack-", System.StringComparison.OrdinalIgnoreCase)) continue;
                var full = Path.GetFullPath(f);
                if (seen.Add(full)) _songFiles.Add(full);
            }
        }

        AddDir(Path.Combine(Application.streamingAssetsPath, "Charts"));
        AddDir(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "导出")));
        AddDir(Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "MuseDashChart", "export"));
        _songFiles.Sort((a, b) => string.Compare(SongLabel(a), SongLabel(b), System.StringComparison.OrdinalIgnoreCase));
    }

    static string SongLabel(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var parent = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (string.Equals(file, "song", System.StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrEmpty(parent) ? file : parent;
        if (string.Equals(file, parent, System.StringComparison.OrdinalIgnoreCase))
            return file;
        return string.IsNullOrEmpty(parent) ? file : parent + "/" + file;
    }

    void CycleSong(int delta)
    {
        if (_songFiles.Count == 0) CollectSongs();
        if (_songFiles.Count == 0) return;
        var i = _songIndex;
        if (i < 0 || i >= _songFiles.Count) i = 0;
        SwitchSong(i + delta);
    }

    void SwitchSong(int index)
    {
        if (_songFiles.Count == 0) return;
        var n = _songFiles.Count;
        index = (index % n + n) % n;
        PlaySong loaded;
        try
        {
            loaded = PlaySong.Load(_songFiles[index]);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            return;
        }

        _songIndex = index;
        _song = loaded;
        var map = s_map is >= 1 and <= 5 ? s_map : difficulty;
        if (!_song.Document.Charts.ContainsKey(map))
            map = _song.ResolveDifficulty(map);
        if (_loadAudio != null)
        {
            StopCoroutine(_loadAudio);
            _loadAudio = null;
        }
        if (_audio != null)
        {
            _audio.Stop();
            _audio.clip = null;
        }
        _waitClip = true;
        SwitchMap(map);
        var music = _song.MusicPath;
        if (PlaySong.IsPlayableAudio(music))
            _loadAudio = StartCoroutine(LoadAudio(music));
        else
        {
            _waitClip = false;
            _clockOrigin = Time.time;
        }
        Debug.Log("换歌 " + SongLabel(_songFiles[index]) + "  map" + (_session != null ? _session.Difficulty : map));
    }
}
