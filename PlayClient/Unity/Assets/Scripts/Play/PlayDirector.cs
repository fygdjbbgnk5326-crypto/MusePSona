using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>自制谱游玩端：StreamingAssets .mdm，DFJK 手动判定 + HUD。</summary>
public sealed partial class PlayDirector : MonoBehaviour
{
    [SerializeField] string chartFile = "Charts/song.mdm";
    [SerializeField] int difficulty = 1;
    [SerializeField] float worldScale = 1f;
    [SerializeField] bool autoplay = true;

    static int s_map;
    public static PlayDirector Live { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoMount()
    {
        if (FindObjectOfType<PlayDirector>() != null) return;
        var go = GameObject.Find("PlayDirector");
        if (go == null) go = new GameObject("PlayDirector");
        go.AddComponent<PlayDirector>();
    }

    PlaySong _song;
    PlaySession _session;
    AudioSource _audio;
    readonly Dictionary<int, Actor> _actors = new();
    readonly HashSet<int> _liveActorIds = new();
    readonly List<int> _deadActorIds = new();
    readonly List<PlaySession.JudgeHit> _hits = new();
    readonly List<Transform> _fx = new();
    Shader _shader;
    Material _matRoad;
    Material _matAir;
    Material _matGuide;
    Material _matLaneAir;
    Material _matLaneRoad;
    Material _matGear;
    Material _matGearCore;
    Material _matMasher;
    Material _matBoss;
    Material _matHeart;
    Material _matMusic;
    Material _matHold;
    Material _matPendulum;
    Material _matRaider;
    Material _matFxP;
    Material _matFxG;
    Material _matFxM;
    Transform _airJudge;
    Transform _roadJudge;
    Renderer _roadLane;
    Renderer _airLane;
    GUIStyle _comboStyle;
    GUIStyle _gradeStyle;
    GUIStyle _hudStyle;
    GUIStyle _clockStyle;
    Judge.Grade _flashGrade;
    float _flashGradeUntil;
    float _flashAirUntil;
    float _flashRoadUntil;
    bool _paused;
    bool _waitClip;
    float _clockOrigin;
    Coroutine _loadAudio;
    readonly List<string> _songFiles = new();
    int _songIndex;
    int _keyFrame = -1;
    KeyCode _keySeen;
    int _playInputFrame = -1;
    int _playHitLaneMask;
    readonly HashSet<KeyCode> _playKeysHeld = new();
    int _inputClockFrame = -1;
    float _inputClockNow;
    string _scene = "";
    MdLooks.Pack _looks;
    Actor _boss;
    string _bossScene = "";
    Vector3 _bossHome;
    bool _bossHomeReady;
    bool _bossHomeTrack;
    string _bossHud = "";
    MdSpineView _comboSpine;
    Transform _comboRoot;
    Transform _comboNumRoot;
    Transform _comboWord;
    int _comboShown = -1;
    int _comboDigits = -1;
    float _comboPopAt;
    string _comboScene = "";
    bool _comboBreak;
    bool _comboDjMax;
    static AnimationCurve _comboIn;
    static AnimationCurve _comboIdleX;
    static AnimationCurve _comboIdleY;
    static AnimationCurve _comboOut;
    Transform _hitsRoot;
    Transform _hitsBurst;
    Transform _hitsNum;
    int _hitsShown = -1;
    float _hitsPopAt;
    float _hitsBurstAt;
    bool _hitsLeaving;
    float _camX;
    float _camSize = Flight.CameraSize;
    MdSceneBg.View _bg;
    string _bgScene = "";
    Transform _laneRoadT;
    Transform _laneAirT;
    Transform _laneMidT;
    // 场景跟随谱面 1O–1W。底部强制切景已撤；角色切换 / N·M 切图仅测试用。
    static readonly bool TempSceneSwitch = false;
    static readonly bool TempCharSwitch = true;
    static readonly bool TempMapKeys = true;
    string _forceScene = "";
    readonly List<CharPick> _chars = new();
    int _charIndex;
    Transform _girlRoot;
    MdSpineView _girl;
    float _girlClock;


    void OnEnable() => Live = this;
    void OnDisable()
    {
        DropGirl();
        if (Live == this) Live = null;
    }

    void Start()
    {
        autoplay = true;
        if (s_map is >= 1 and <= 5) difficulty = s_map;
        SetupStage();
        CollectSongs();
        MdSpine.Ensure();
        CollectChars();
        if (TempCharSwitch) EnsureGirl();
        var path = Path.Combine(Application.streamingAssetsPath, chartFile);
        var listed = _songFiles.FindIndex(p => string.Equals(p, path, System.StringComparison.OrdinalIgnoreCase));
        if (listed >= 0) _songIndex = listed;
        if (!File.Exists(path))
        {
            Debug.LogError("找不到谱面: " + path + "  （放到 Assets/StreamingAssets/Charts/ 或菜单 MusePSona/从导出复制最新 MDM）");
            return;
        }

        try
        {
            _song = PlaySong.Load(path);
            if (s_map is < 1 or > 5 || !_song.Document.Charts.ContainsKey(s_map))
                s_map = _song.ResolveDifficulty(difficulty);
            difficulty = s_map;
            _session = new PlaySession(_song, difficulty);
            ResetScreenFxState();
            if (_bg != null) _bg.ScrollY = 0f;
            if (_session.Difficulty != difficulty)
                Debug.Log("没有 map" + difficulty + "，改用 map" + _session.Difficulty);
            MdLooks.Reload();
            MdSpine.Ensure();
            RebuildJudges();
            if (TempCharSwitch) EnsureGirl();
            var need = SceneTimeline.Needed(_session.Scenes);
            Debug.Log("MDM 场景 " + string.Join(" → ", need));
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            return;
        }
        _audio = gameObject.AddComponent<AudioSource>();
        _clockOrigin = Time.time;
        var music = _song != null ? _song.MusicPath : _session.Song.MusicPath;
        if (PlaySong.IsPlayableAudio(music))
        {
            _waitClip = true;
            _loadAudio = StartCoroutine(LoadAudio(music));
        }
    }

    System.Collections.IEnumerator LoadAudio(string path)
    {
        if (!PlaySong.IsPlayableAudio(path))
        {
            Debug.LogWarning("跳过无效音频（占位/空文件）: " + path);
            _waitClip = false;
            _clockOrigin = Time.time;
            yield break;
        }
        var type = ClipType(path);
        if (type == AudioType.UNKNOWN)
        {
            Debug.LogWarning("不支持的音频格式: " + path);
            _waitClip = false;
            _clockOrigin = Time.time;
            yield break;
        }
        var uri = new System.Uri(path).AbsoluteUri;
        using var req = UnityWebRequestMultimedia.GetAudioClip(uri, type);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("音频加载失败: " + req.error + "  " + path);
            _waitClip = false;
            _clockOrigin = Time.time;
            yield break;
        }
        try
        {
            _audio.clip = DownloadHandlerAudioClip.GetContent(req);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("音频解码失败: " + path + "  " + ex.Message);
            _waitClip = false;
            _clockOrigin = Time.time;
            yield break;
        }
        _waitClip = false;
        if (!_paused) _audio.Play();
    }

    static AudioType ClipType(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var b = new byte[4];
            if (fs.Read(b, 0, 4) == 4)
            {
                if (b[0] == (byte)'O' && b[1] == (byte)'g' && b[2] == (byte)'g' && b[3] == (byte)'S')
                    return AudioType.OGGVORBIS;
                if (b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F')
                    return AudioType.WAV;
                if (b[0] == (byte)'I' && b[1] == (byte)'D' && b[2] == (byte)'3')
                    return AudioType.MPEG;
                if (b[0] == 0xFF && (b[1] & 0xE0) == 0xE0)
                    return AudioType.MPEG;
            }
        }
        catch { /* 按后缀兜底 */ }
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => AudioType.MPEG,
            ".wav" => AudioType.WAV,
            ".ogg" => AudioType.OGGVORBIS,
            _ => AudioType.UNKNOWN
        };
    }

    public static void RequestMap(int map)
    {
        if (Live != null) Live.SwitchMap(map);
        else s_map = map;
    }

    public static void RequestCycle(int delta)
    {
        if (Live != null) Live.CycleMap(delta);
    }

    float Clock()
    {
        if (_audio != null && _audio.clip != null && (_audio.isPlaying || _paused))
        {
            // AudioSource.time 在部分压缩音频上按解码块更新，键盘判定会产生
            // 一帧左右的台阶感。timeSamples 直接换算采样位置，时间轴更稳定。
            var frequency = _audio.clip.frequency;
            if (frequency > 0)
                return (float)_audio.timeSamples / frequency;
            return _audio.time;
        }
        if (_waitClip) return 0f;
        return Time.time - _clockOrigin;
    }

    float InputClock()
    {
        // 同一帧的 DF / JK 双押必须使用完全相同的谱面时间，避免依次读取
        // 音频采样位置时落入不同判定等级。
        if (_inputClockFrame == Time.frameCount) return _inputClockNow;
        _inputClockFrame = Time.frameCount;
        _inputClockNow = Clock();
        return _inputClockNow;
    }

    void SwitchMap(int map)
    {
        if (_song == null) return;
        if (!_song.Document.Charts.ContainsKey(map))
        {
            Debug.Log("没有 map" + map + "，可选 " + string.Join("/", _song.Maps.Select(m => "map" + m)));
            return;
        }
        s_map = map;
        difficulty = map;
        ClearActors();
        _session = new PlaySession(_song, map);
        ResetScreenFxState();
        _paused = false;
        MdSceneBg.Drop(_bg);
        _bg = null;
        _bgScene = "";
        if (_audio != null && _audio.clip != null)
        {
            _audio.Stop();
            _audio.time = 0f;
            if (!_waitClip) _audio.Play();
        }
        _clockOrigin = Time.time;
        _inputClockFrame = -1;
        _flashGradeUntil = 0f;
        ClearGirlAction();
        _scene = "";
        if (_session != null)
        {
            var scene = _forceScene.Length > 0 ? _forceScene : _session.SceneAt(0f);
            TintScene(scene);
        }
    }

    void ClearActors()
    {
        DropBoss();
        foreach (var kv in _actors)
            DropActor(kv.Value);
        _actors.Clear();
        foreach (var t in _fx)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _fx.Clear();
    }

    void Update()
    {
        PollPlayKeys();
        if (_session == null) return;
        var now = Clock();
        var scene = _session.SceneAt(now);
        if (_forceScene.Length > 0) scene = _forceScene;
        TintScene(scene);
        TickBg(now);
        TickFx(now);
        if (!_paused && !_waitClip)
        {
            if (autoplay) _session.Autoplay(now);
            else _session.TickManual(now, GirlIsAir(), _girlHoldMask, HeldPlayLaneMask(now));
        }
        FlushHits();
        PulseLane(_airLane, _matLaneAir, Time.unscaledTime < _flashAirUntil, new Color(0.45f, 0.95f, 1f));
        PulseLane(_roadLane, _matLaneRoad, Time.unscaledTime < _flashRoadUntil, new Color(1f, 0.55f, 0.82f));
        AnimateJudge(_airJudge, Time.unscaledTime < _flashAirUntil, true);
        AnimateJudge(_roadJudge, Time.unscaledTime < _flashRoadUntil, false);

        var chartNow = now - _session.DelaySec;
        _liveActorIds.Clear();
        foreach (var n in _session.Visible(now))
        {
            // 11/12 近战、16/17 连打的判定点就是本体（close_atk / multi_atk 冲到判定线），再画物件会叠在中线。
            if (BossTimeline.BodyIsTarget(n.BmsId)) continue;
            _liveActorIds.Add(n.Id);
            if (!_actors.TryGetValue(n.Id, out var a))
            {
                a = SpawnActor(n);
                _actors[n.Id] = a;
            }
            Place(a, n, chartNow);
        }
        _deadActorIds.Clear();
        foreach (var kv in _actors)
        {
            if (_liveActorIds.Contains(kv.Key)) continue;
            DropActor(kv.Value);
            _deadActorIds.Add(kv.Key);
        }
        foreach (var id in _deadActorIds) _actors.Remove(id);
        PlaceBoss(chartNow);
        TickCombo();
        TickHits(chartNow);
        TickCamera(chartNow);
        TickGirl();

        for (var i = _fx.Count - 1; i >= 0; i--)
        {
            var t = _fx[i];
            if (t == null)
            {
                _fx.RemoveAt(i);
                continue;
            }
            if (t.name == "hitFx")
            {
                t.localScale += new Vector3(0.55f, 0.55f, 0f) * Time.unscaledDeltaTime;
                var fade = 2.6f * Time.unscaledDeltaTime;
                var gone = false;
                var tm = t.GetComponent<TextMesh>();
                if (tm != null)
                {
                    var c = tm.color;
                    c.a -= fade;
                    tm.color = c;
                    if (c.a <= 0.03f) gone = true;
                }
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r.material == null) continue;
                    var c = r.material.color;
                    c.a -= fade;
                    r.material.color = c;
                    if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
                    if (c.a <= 0.03f) gone = true;
                }
                if (gone)
                {
                    Destroy(t.gameObject);
                    _fx.RemoveAt(i);
                }
                continue;
            }
            t.localScale += Vector3.one * (2.8f * Time.unscaledDeltaTime);
            if (t.localScale.x > 1.35f)
            {
                Destroy(t.gameObject);
                _fx.RemoveAt(i);
            }
        }
    }

    void TintScene(string scene)
    {
        if (scene != _scene)
        {
            _scene = scene;
            Flight.Scene = scene;
            _looks = MdLooks.For(scene);
            RebuildJudges();
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        }
        EnsureBg(scene);
    }

    void EnsureBg(string scene)
    {
        if (scene == _bgScene && _bg != null && !MdSceneBg.LayoutStale(scene)) return;
        MdSceneBg.Drop(_bg);
        _bg = null;
        _bgScene = scene;
        var sh = Shader.Find("Sprites/Default") ?? _shader;
        _bg = MdSceneBg.Spawn(transform, scene, sh, worldScale);
        var has = _bg != null;
        var oneKey = SceneTimeline.RulesFor(scene).OneKey;
        if (_laneRoadT != null) _laneRoadT.gameObject.SetActive(!has);
        if (_laneAirT != null) _laneAirT.gameObject.SetActive(!has && !oneKey);
        if (_laneMidT != null) _laneMidT.gameObject.SetActive(!has && !oneKey);
    }

    void TickBg(float now)
    {
        if (_session == null) return;
        var chartNow = now - _session.DelaySec;
        var st = BgTimeline.At(_session.Bg, chartNow);
        MdSceneBg.Tick(_bg, chartNow, st, _paused ? 0f : Time.deltaTime);
    }

    void FlushHits()
    {
        if (_session == null) return;
        _hits.Clear();
        _session.TakeHits(_hits);
        BeginGirlHitBatch();
        foreach (var hit in _hits)
        {
            var manualGearPass = !autoplay
                && hit.Note.Category == NoteCategory.Gear
                && hit.Grade == Judge.Grade.Pass;
            PlayGirlHit(hit);
            if (hit.Note.Category == NoteCategory.Ghost) continue;
            if (hit.Grade != Judge.Grade.Miss)
            {
                _flashGrade = hit.Grade;
                _flashGradeUntil = Time.unscaledTime + 0.55f;
            }
            if (hit.Grade is Judge.Grade.Perfect or Judge.Grade.Great)
            {
                if (hit.Note.Pathway == 1) _flashAirUntil = Time.unscaledTime + 0.18f;
                else _flashRoadUntil = Time.unscaledTime + 0.18f;
            }
            // 手动闪避由玩家的跳跃/下砍动作本身提供反馈。不要再在对侧轨
            // 生成 hit_pass，否则看起来像角色成功闪避后又播放副轨受击。
            if (manualGearPass) continue;
            // 齿轮 PASS 写在对侧轨（地齿轮上天轨，天齿轮上地轨）。
            var laneY = hit.Grade == Judge.Grade.Pass
                ? (hit.Note.Pathway == 1 ? Flight.RoadY : Flight.AirY)
                : Flight.LaneY(hit.Note);
            var y = -laneY * worldScale;
            SpawnFx(y, hit.Grade);
        }
    }

    void SpawnFx(float y, Judge.Grade grade)
    {
        // 官机 ImgScorePerfect / Great / Pass；双押仍是同一张字。无 MISS 气泡。
        var key = grade == Judge.Grade.Perfect ? "hit_perfect"
            : grade == Judge.Grade.Great ? "hit_great"
            : grade == Judge.Grade.Pass ? "hit_pass" : null;
        if (key == null || !MdLooks.TrySprite(_scene, key, out var tex) || tex == null)
            return;
        var box = Mathf.Max(0.5f, tex.width / MdLooks.HitGradePpu * worldScale);
        var t = SpriteQuad(transform, tex, box, "hitFx");
        t.position = new Vector3(Flight.HitX * worldScale, y + 0.70f * worldScale, -0.18f);
        t.localScale *= 0.90f;
        PaintLayer(t, 3220, -8);
        _fx.Add(t);
    }

    static void PulseLane(Renderer lane, Material mat, bool on, Color flash)
    {
        if (lane == null) return;
        var s = lane.transform.localScale;
        s.y = on ? 0.12f : 0.04f;
        lane.transform.localScale = s;
        if (mat != null) mat.color = on ? flash : new Color(0.55f, 0.55f, 0.42f);
    }
}
