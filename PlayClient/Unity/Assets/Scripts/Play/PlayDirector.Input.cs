using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MusePSona.Play;
using UnityEngine;

/// <summary>快捷键、换谱、换歌。</summary>
public sealed partial class PlayDirector
{
    sealed class PreparedSong
    {
        public PlaySong Song;
        public PlaySession Session;
        public int Map;
    }

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
        // 曲终 Full Combo 演出由固定 2 秒时间线接管，期间禁止暂停、换歌、
        // 重开或打开校准页，避免演出状态被半途留在场景中。
        if (_fullComboPlaying) return;
        if (key == KeyCode.F12)
        {
            ToggleAudioOffsetPanel();
            return;
        }
        if (key == KeyCode.Escape && _audioOffsetPanelOpen)
        {
            CloseAudioOffsetPanel();
            return;
        }
        if (key == KeyCode.F9)
        {
            ChangeAudioOffset(ShiftHeld() ? -AudioOffset.FastStepMs : -AudioOffset.StepMs);
            return;
        }
        if (key == KeyCode.F10)
        {
            ChangeAudioOffset(ShiftHeld() ? AudioOffset.FastStepMs : AudioOffset.StepMs);
            return;
        }
        if (key == KeyCode.F11)
        {
            SetAudioOffset(0);
            return;
        }
        if (_audioOffsetPanelOpen) return;
        if (_frontendPage == FrontendPage.SongSelect)
        {
            HandleSongSelectKey(key);
            return;
        }
        if (_frontendPage == FrontendPage.Loading)
        {
            if (key == KeyCode.Escape) OpenSongSelect();
            return;
        }
        if (_resultOpen)
        {
            if (key is KeyCode.R or KeyCode.Return or KeyCode.KeypadEnter)
                RestartFromResult();
            else if (key == KeyCode.F2)
                RestartFromResult(false);
            else if (key == KeyCode.F3)
                RestartFromResult(true);
            else if (key is KeyCode.F4 or KeyCode.PageDown)
                LeaveResultForSong(1);
            else if (key is KeyCode.F8 or KeyCode.PageUp)
                LeaveResultForSong(-1);
            else if (key == KeyCode.Escape)
                OpenSongSelect();
            return;
        }
        if (key == KeyCode.Escape)
        {
            SetPaused(!_paused);
            return;
        }
        if (_paused)
        {
            if (key is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space)
                SetPaused(false);
            else if (key == KeyCode.R)
            {
                SetPaused(false);
                SwitchMap(_session != null ? _session.Difficulty : difficulty);
            }
            return;
        }
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
            SetPaused(!_paused);
            return;
        }
        if (_session == null || _paused || autoplay) return;
        var now = InputClock();
        if (!TryMappedLane(key, now, out var air)) return;
        var oneKey = SceneTimeline.RulesFor(_session.SceneAt(now)).OneKey;
        if (oneKey) air = false;
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
        var anyAir = AnyMappedKeyHeld(AirKeys());
        var anyRoad = AnyMappedKeyHeld(GroundKeys());
        if (_session != null && SceneTimeline.RulesFor(_session.SceneAt(now)).OneKey)
            return anyAir || anyRoad ? 3 : 0;
        return (anyRoad ? 1 : 0) | (anyAir ? 2 : 0);
    }

    KeyCode[] AirKeys()
        => _keyboardLayout switch
        {
            KeyboardLayout.Default => KeysDefaultAir,
            KeyboardLayout.DefaultReverse => KeysDefaultGround,
            KeyboardLayout.ModeB => KeysModeBAir,
            KeyboardLayout.ModeBReverse => KeysModeBGround,
            KeyboardLayout.ModeC => KeysModeCAir,
            KeyboardLayout.ModeCReverse => KeysModeCGround,
            KeyboardLayout.ModeD => KeysModeDAir,
            _ => KeysDefaultAir
        };

    KeyCode[] GroundKeys()
        => _keyboardLayout switch
        {
            KeyboardLayout.Default => KeysDefaultGround,
            KeyboardLayout.DefaultReverse => KeysDefaultAir,
            KeyboardLayout.ModeB => KeysModeBGround,
            KeyboardLayout.ModeBReverse => KeysModeBAir,
            KeyboardLayout.ModeC => KeysModeCGround,
            KeyboardLayout.ModeCReverse => KeysModeCAir,
            KeyboardLayout.ModeD => KeysModeDGround,
            _ => KeysDefaultGround
        };

    static bool ContainsKey(KeyCode[] keys, KeyCode key)
    {
        for (var i = 0; i < keys.Length; i++)
            if (keys[i] == key) return true;
        return false;
    }

    static bool AnyMappedKeyHeld(KeyCode[] keys)
    {
        for (var i = 0; i < keys.Length; i++)
            if (Input.GetKey(keys[i])) return true;
        return false;
    }

    bool IsMappedPlayKey(KeyCode key)
        => ContainsKey(AirKeys(), key) || ContainsKey(GroundKeys(), key);

    bool TryMappedLane(KeyCode key, float now, out bool air)
    {
        air = false;
        var inAir = ContainsKey(AirKeys(), key);
        var inGround = ContainsKey(GroundKeys(), key);
        if (!inAir && !inGround) return false;
        if (_session != null && SceneTimeline.RulesFor(_session.SceneAt(now)).OneKey)
            return true;
        air = inAir;
        return true;
    }

    static string JoinKeys(KeyCode[] keys)
    {
        var names = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
            names[i] = keys[i] switch
            {
                KeyCode.LeftArrow => "←",
                KeyCode.RightArrow => "→",
                KeyCode.UpArrow => "↑",
                KeyCode.DownArrow => "↓",
                _ => keys[i].ToString()
            };
        return string.Join("/", names);
    }

    string KeyboardLayoutName()
        => _keyboardLayout switch
        {
            KeyboardLayout.Default => "Default",
            KeyboardLayout.DefaultReverse => "Default-R",
            KeyboardLayout.ModeB => "Mode B",
            KeyboardLayout.ModeBReverse => "Mode B-R",
            KeyboardLayout.ModeC => "Mode C",
            KeyboardLayout.ModeCReverse => "Mode C-R",
            KeyboardLayout.ModeD => "Mode D",
            _ => "Default"
        };

    string KeyboardHint()
        => "空中 " + JoinKeys(AirKeys()) + "  地面 " + JoinKeys(GroundKeys());

    void CycleKeyboardLayout()
    {
        var count = (int)KeyboardLayout.ModeD + 1;
        _keyboardLayout = (KeyboardLayout)(((int)_keyboardLayout + 1) % count);
        _playKeysHeld.Clear();
        PlayerPrefs.SetInt(KeyboardLayoutPrefsKey, (int)_keyboardLayout);
        PlayerPrefs.Save();
    }

    static bool ShiftHeld()
        => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

    void ChangeAudioOffset(int deltaMs) => SetAudioOffset(_audioOffsetMs + deltaMs);

    void SetAudioOffset(int milliseconds)
    {
        var next = AudioOffset.Snap(milliseconds);
        if (next == _audioOffsetMs && milliseconds != 0) return;
        _audioOffsetMs = next;
        _inputClockFrame = -1;
        _audioOffsetNoticeUntil = Time.unscaledTime + 1.5f;
        PlayerPrefs.SetInt(AudioOffsetPrefsKey, _audioOffsetMs);
        _audioOffsetDirty = true;
        Debug.Log("音频偏移 " + AudioOffset.Format(_audioOffsetMs));
    }

    void SaveAudioOffset()
    {
        if (!_audioOffsetDirty) return;
        PlayerPrefs.Save();
        _audioOffsetDirty = false;
    }

    void PollAudioOffsetKeys()
    {
        if (_audioOffsetHoldKey == KeyCode.None)
        {
            if (Input.GetKeyDown(KeyCode.F9)) BeginAudioOffsetHold(KeyCode.F9);
            else if (Input.GetKeyDown(KeyCode.F10)) BeginAudioOffsetHold(KeyCode.F10);
            return;
        }

        if (!Input.GetKey(_audioOffsetHoldKey))
        {
            SaveAudioOffset();
            _audioOffsetHoldKey = KeyCode.None;
            return;
        }

        if (Time.unscaledTime < _audioOffsetNextRepeatAt) return;
        var direction = _audioOffsetHoldKey == KeyCode.F10 ? 1 : -1;
        ChangeAudioOffset(direction * (ShiftHeld() ? AudioOffset.FastStepMs : AudioOffset.StepMs));
        _audioOffsetNextRepeatAt = Time.unscaledTime + AudioOffset.LongPressRepeatSec;
    }

    void BeginAudioOffsetHold(KeyCode key)
    {
        _audioOffsetHoldKey = key;
        _audioOffsetNextRepeatAt = Time.unscaledTime + AudioOffset.LongPressDelaySec;
        TryKey(key);
    }

    void PressPlayKey(KeyCode key)
    {
        // 每个物理键独立锁定：J 尚未松开时 K 仍能触发下一击；来自
        // Update、IMGUI 及系统长按重复的同一次 KeyDown 只结算一次。
        if (!IsMappedPlayKey(key) || !_playKeysHeld.Add(key)) return;
        // 游玩键的去重由按下/松开状态负责，不使用“同帧同键”限制；这样
        // 极快的松开后重按以及 J-K-J 滚键不会被帧边界吞掉。
        HandleKey(key);
    }

    void ReleasePlayKey(KeyCode key)
    {
        _playKeysHeld.Remove(key);
    }

    bool CaptureGuiPlayKey(UnityEngine.Event e)
    {
        if (e == null || !IsMappedPlayKey(e.keyCode)) return false;
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
        if (!focused)
        {
            _playKeysHeld.Clear();
            _audioOffsetHoldKey = KeyCode.None;
            SaveAudioOffset();
        }
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
        var airKeys = AirKeys();
        var groundKeys = GroundKeys();
        for (var i = 0; i < airKeys.Length; i++) PollPlayKey(airKeys[i]);
        for (var i = 0; i < groundKeys.Length; i++) PollPlayKey(groundKeys[i]);
        PollAudioOffsetKeys();
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
        if (Input.GetKeyDown(KeyCode.F11)) TryKey(KeyCode.F11);
        if (Input.GetKeyDown(KeyCode.F12)) TryKey(KeyCode.F12);
        if (Input.GetKeyDown(KeyCode.F5)) TryKey(KeyCode.F5);
        if (Input.GetKeyDown(KeyCode.F6)) TryKey(KeyCode.F6);
        if (Input.GetKeyDown(KeyCode.F7)) TryKey(KeyCode.F7);
        if (Input.GetKeyDown(KeyCode.R)) TryKey(KeyCode.R);
        if (Input.GetKeyDown(KeyCode.Return)) TryKey(KeyCode.Return);
        if (Input.GetKeyDown(KeyCode.KeypadEnter)) TryKey(KeyCode.KeypadEnter);
        if (Input.GetKeyDown(KeyCode.Space)) TryKey(KeyCode.Space);
        if (Input.GetKeyDown(KeyCode.Escape)) TryKey(KeyCode.Escape);
        if (!IsMappedPlayKey(KeyCode.LeftArrow) && Input.GetKeyDown(KeyCode.LeftArrow)) TryKey(KeyCode.LeftArrow);
        if (!IsMappedPlayKey(KeyCode.RightArrow) && Input.GetKeyDown(KeyCode.RightArrow)) TryKey(KeyCode.RightArrow);
        if (!IsMappedPlayKey(KeyCode.UpArrow) && Input.GetKeyDown(KeyCode.UpArrow)) TryKey(KeyCode.UpArrow);
        if (!IsMappedPlayKey(KeyCode.DownArrow) && Input.GetKeyDown(KeyCode.DownArrow)) TryKey(KeyCode.DownArrow);
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
        // 连续按上/下一首时，以已请求的目标为起点，而不是仍以
        // 旧歌为起点，避免后台加载期间多次按键又跳回同一首。
        var i = _songTargetIndex >= 0 ? _songTargetIndex : _songIndex;
        if (i < 0 || i >= _songFiles.Count) i = 0;
        SwitchSong(i + delta);
    }

    void SwitchSong(int index)
    {
        if (_songFiles.Count == 0) return;
        var n = _songFiles.Count;
        index = (index % n + n) % n;
        _frontendPage = FrontendPage.Loading;
        _songTargetIndex = index;
        var serial = ++_songSwitchSerial;
        if (_switchSong != null)
            StopCoroutine(_switchSong);
        _switchSong = StartCoroutine(SwitchSongRoutine(index, serial));
    }

    IEnumerator SwitchSongRoutine(int index, int serial)
    {
        var path = _songFiles[index];
        var requestedMap = s_map is >= 1 and <= 5 ? s_map : difficulty;
        _waitClip = true;
        // 若上一次的音频请求尚未完成，立即终止。否则在新谱面
        // 后台准备期间，旧请求可能完成并突然播放错误的歌曲。
        if (_loadAudio != null)
        {
            StopCoroutine(_loadAudio);
            _loadAudio = null;
        }
        if (_audio != null && _audio.isPlaying)
            _audio.Pause();

        // MDM 读取、JSON 解析、物件排序与各条时间轴构建都不依赖
        // Unity API，放到工作线程后不再阻塞点击切歌的当帧。
        var prepare = Task.Run(() =>
        {
            var song = PlaySong.Load(path);
            var map = song.Document.Charts.ContainsKey(requestedMap)
                ? requestedMap : song.ResolveDifficulty(requestedMap);
            return new PreparedSong
            {
                Song = song,
                Map = map,
                Session = new PlaySession(song, map)
            };
        });
        while (!prepare.IsCompleted)
            yield return null;

        if (serial != _songSwitchSerial)
            yield break;
        if (prepare.IsFaulted || prepare.IsCanceled)
        {
            var ex = prepare.Exception?.GetBaseException();
            if (ex != null) Debug.LogException(ex);
            _switchSong = null;
            OpenSongSelect();
            yield break;
        }

        var prepared = prepare.Result;
        if (_audio != null)
        {
            _audio.Stop();
            var oldClip = _audio.clip;
            _audio.clip = null;
            if (oldClip != null) Destroy(oldClip);
        }
        _songIndex = index;
        _selectedSongIndex = index;
        _selectedDifficulty = prepared.Map;
        _songTargetIndex = -1;
        _song = prepared.Song;
        ApplySession(prepared.Session, prepared.Map);
        var music = _song.MusicPath;
        if (PlaySong.IsPlayableAudio(music))
            _loadAudio = StartCoroutine(LoadAudio(music));
        else
        {
            _waitClip = false;
            _clockOrigin = Time.time;
            FinishSongLoading();
        }
        _switchSong = null;
        Debug.Log("换歌 " + SongLabel(_songFiles[index]) + "  map"
            + (_session != null ? _session.Difficulty : prepared.Map));
    }
}
