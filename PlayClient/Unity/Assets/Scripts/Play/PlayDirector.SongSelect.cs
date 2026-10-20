using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using MuseDashChart;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>官机风格选曲、加载与暂停页面。</summary>
public sealed partial class PlayDirector
{
    enum FrontendPage
    {
        SongSelect,
        Loading,
        Playing
    }

    sealed class SongCard
    {
        public string Path = "";
        public string Name = "";
        public string Romanized = "";
        public string Author = "";
        public string Bpm = "120";
        public string Scene = "scene_01";
        public string CoverEntry = "";
        public string DemoEntry = "";
        public string Error = "";
        public readonly List<int> Maps = new();
        public readonly string[] Stars = new string[6];
        public Texture2D Cover;
        public bool CoverTried;
    }

    const string SelectedSongPrefsKey = "MusePSona.SelectedSong";
    const string SelectedDifficultyPrefsKey = "MusePSona.SelectedDifficulty";
    const float SongSelectDesignWidth = 1920f;
    const float SongSelectDesignHeight = 1080f;
    static readonly string[] DifficultyNames = { "", "萌新", "高手", "大触", "隐藏", "特殊" };
    static readonly string[] CoverNames = { "cover.png", "cover.jpg", "cover.jpeg" };
    // 官机 UISystem_PC/PnlStage 在 1920x1080 下的五个唱片可见区域。
    // 侧盘的 RectTransform 包含额外动画留白，直接使用抓取包围框会显得过大，
    // 因此这里按官机截图中的实际唱片外沿做了二次校准。
    static readonly Rect[] SongDiscSlots =
    {
        new(-278f, 812f, 414f, 414f),
        new(204.9f, 450.7f, 412f, 412f),
        new(749.4f, 329.4f, 421.2f, 421.2f),
        new(1303.1f, 450.7f, 412f, 412f),
        new(1784f, 812f, 414f, 414f)
    };

    FrontendPage _frontendPage = FrontendPage.SongSelect;
    readonly List<SongCard> _songCatalog = new();
    readonly Dictionary<string, Texture2D> _songUiCache = new(StringComparer.Ordinal);
    readonly HashSet<string> _songUiMissing = new(StringComparer.Ordinal);
    int _selectedSongIndex;
    int _selectedDifficulty = 1;
    GUIStyle _selectBrandStyle;
    GUIStyle _selectTitleStyle;
    GUIStyle _selectSongStyle;
    GUIStyle _selectAuthorStyle;
    GUIStyle _selectMetaStyle;
    GUIStyle _selectHintStyle;
    GUIStyle _selectDifficultyStyle;
    GUIStyle _selectCenteredSongStyle;
    GUIStyle _selectDiscLabelStyle;
    GUIStyle _selectArrowStyle;
    GUIStyle _selectTabStyle;
    Texture2D _songDiscTexture;
    Texture2D _songGearTexture;
    Texture2D _songBackdropCubeTexture;
    Texture2D _songBackdropGlowTexture;
    Texture2D _songBackdropFacetTexture;
    float _songBackdropStartedAt;
    AudioSource _previewAudio;
    Coroutine _previewLoad;
    int _previewSerial;

    void BuildSongCatalog()
    {
        DropSongCatalog();
        for (var i = 0; i < _songFiles.Count; i++)
            _songCatalog.Add(ReadSongCard(_songFiles[i]));
    }

    static SongCard ReadSongCard(string path)
    {
        var card = new SongCard
        {
            Path = path,
            Name = SongLabel(path),
            Author = "Unknown"
        };
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var infoEntry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.Name, "info.json", StringComparison.OrdinalIgnoreCase));
            if (infoEntry == null)
                throw new InvalidDataException("MDM 中缺少 info.json");
            using (var reader = new StreamReader(infoEntry.Open()))
            {
                var info = JsonSerializer.Deserialize<AlbumInfoFile>(reader.ReadToEnd(), AlbumInfoJson.Options)
                    ?? throw new InvalidDataException("info.json 无法读取");
                if (!string.IsNullOrWhiteSpace(info.Name)) card.Name = info.Name.Trim();
                card.Romanized = info.NameRomanized?.Trim() ?? "";
                card.Author = string.IsNullOrWhiteSpace(info.Author) ? "Unknown" : info.Author.Trim();
                card.Bpm = string.IsNullOrWhiteSpace(info.Bpm) ? "120" : info.Bpm.Trim();
                card.Scene = string.IsNullOrWhiteSpace(info.Scene) ? "scene_01" : info.Scene.Trim();
                for (var map = 1; map <= 5; map++)
                    card.Stars[map] = info.GetDifficultyLevel(map);
            }

            foreach (var entry in archive.Entries)
            {
                var name = entry.Name;
                for (var map = 1; map <= 5; map++)
                {
                    if (!string.Equals(name, "map" + map + ".bms", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!card.Maps.Contains(map)) card.Maps.Add(map);
                }
                if (card.CoverEntry.Length == 0 && CoverNames.Any(n =>
                        string.Equals(name, n, StringComparison.OrdinalIgnoreCase)))
                    card.CoverEntry = entry.FullName;
                if (card.DemoEntry.Length == 0
                    && (string.Equals(name, "demo.ogg", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "demo.mp3", StringComparison.OrdinalIgnoreCase)))
                    card.DemoEntry = entry.FullName;
            }
            card.Maps.Sort();
            if (card.Maps.Count == 0)
                throw new InvalidDataException("MDM 中没有 map1–map5");
        }
        catch (Exception ex)
        {
            card.Error = ex.Message;
        }
        return card;
    }

    void InitializeSongSelection(string preferredPath)
    {
        _songBackdropStartedAt = Time.unscaledTime;
        _selectedDifficulty = Mathf.Clamp(PlayerPrefs.GetInt(SelectedDifficultyPrefsKey, difficulty), 1, 5);
        var saved = PlayerPrefs.GetString(SelectedSongPrefsKey, preferredPath ?? "");
        var index = _songFiles.FindIndex(p =>
            string.Equals(p, saved, StringComparison.OrdinalIgnoreCase));
        if (index < 0 && !string.IsNullOrEmpty(preferredPath))
            index = _songFiles.FindIndex(p =>
                string.Equals(p, preferredPath, StringComparison.OrdinalIgnoreCase));
        _selectedSongIndex = index >= 0 ? index : 0;
        _songIndex = _selectedSongIndex;
        FitSelectedDifficulty();
        WarmVisibleCovers();
        QueueSongPreview();
    }

    void SelectSongDelta(int delta)
    {
        if (_songCatalog.Count == 0) return;
        _selectedSongIndex = (_selectedSongIndex + delta % _songCatalog.Count + _songCatalog.Count)
            % _songCatalog.Count;
        FitSelectedDifficulty();
        WarmVisibleCovers();
        QueueSongPreview();
    }

    void SelectDifficultyDelta(int delta)
    {
        var card = SelectedSongCard();
        if (card == null || card.Maps.Count == 0) return;
        var index = card.Maps.IndexOf(_selectedDifficulty);
        if (index < 0) index = 0;
        _selectedDifficulty = card.Maps[(index + delta % card.Maps.Count + card.Maps.Count) % card.Maps.Count];
    }

    void SelectDifficulty(int map)
    {
        var card = SelectedSongCard();
        if (card != null && card.Maps.Contains(map)) _selectedDifficulty = map;
    }

    void FitSelectedDifficulty()
    {
        var card = SelectedSongCard();
        if (card == null || card.Maps.Count == 0) return;
        if (card.Maps.Contains(_selectedDifficulty)) return;
        foreach (var preferred in new[] { 3, 2, 1, 4, 5 })
        {
            if (!card.Maps.Contains(preferred)) continue;
            _selectedDifficulty = preferred;
            return;
        }
        _selectedDifficulty = card.Maps[0];
    }

    SongCard SelectedSongCard()
        => _selectedSongIndex >= 0 && _selectedSongIndex < _songCatalog.Count
            ? _songCatalog[_selectedSongIndex]
            : null;

    void WarmVisibleCovers()
    {
        if (_songCatalog.Count == 0) return;
        EnsureSongCover(_selectedSongIndex);
        if (_songCatalog.Count > 1)
        {
            EnsureSongCover((_selectedSongIndex - 1 + _songCatalog.Count) % _songCatalog.Count);
            EnsureSongCover((_selectedSongIndex + 1) % _songCatalog.Count);
        }
    }

    void EnsureSongCover(int index)
    {
        if (index < 0 || index >= _songCatalog.Count) return;
        var card = _songCatalog[index];
        if (card.CoverTried) return;
        card.CoverTried = true;
        if (card.CoverEntry.Length == 0 || !File.Exists(card.Path)) return;
        try
        {
            using var archive = ZipFile.OpenRead(card.Path);
            var entry = archive.GetEntry(card.CoverEntry) ?? archive.Entries.FirstOrDefault(e =>
                string.Equals(e.Name, Path.GetFileName(card.CoverEntry), StringComparison.OrdinalIgnoreCase));
            if (entry == null) return;
            using var source = entry.Open();
            using var memory = new MemoryStream();
            source.CopyTo(memory);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "song-cover-" + index,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (texture.LoadImage(memory.ToArray(), false))
            {
                ApplyRoundCoverMask(texture);
                card.Cover = texture;
            }
            else Destroy(texture);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("曲绘读取失败: " + card.Path + "  " + ex.Message);
        }
    }

    void DropSongCatalog()
    {
        for (var i = 0; i < _songCatalog.Count; i++)
            if (_songCatalog[i].Cover != null) Destroy(_songCatalog[i].Cover);
        _songCatalog.Clear();
        if (_songDiscTexture != null)
        {
            Destroy(_songDiscTexture);
            _songDiscTexture = null;
        }
        if (_songGearTexture != null)
        {
            Destroy(_songGearTexture);
            _songGearTexture = null;
        }
        if (_songBackdropCubeTexture != null)
        {
            Destroy(_songBackdropCubeTexture);
            _songBackdropCubeTexture = null;
        }
        if (_songBackdropGlowTexture != null)
        {
            Destroy(_songBackdropGlowTexture);
            _songBackdropGlowTexture = null;
        }
        if (_songBackdropFacetTexture != null)
        {
            Destroy(_songBackdropFacetTexture);
            _songBackdropFacetTexture = null;
        }
    }

    static void ApplyRoundCoverMask(Texture2D texture)
    {
        if (texture == null || !texture.isReadable) return;
        try
        {
            var pixels = texture.GetPixels32();
            var width = texture.width;
            var height = texture.height;
            var centerX = (width - 1) * 0.5f;
            var centerY = (height - 1) * 0.5f;
            var radius = Mathf.Min(width, height) * 0.5f;
            var radiusSquared = radius * radius;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= radiusSquared) continue;
                var pixel = pixels[y * width + x];
                pixel.a = 0;
                pixels[y * width + x] = pixel;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("曲绘圆形遮罩失败: " + ex.Message);
        }
    }

    void SetupSongPreview()
    {
        if (_previewAudio != null) return;
        _previewAudio = gameObject.AddComponent<AudioSource>();
        _previewAudio.playOnAwake = false;
        _previewAudio.loop = true;
        _previewAudio.volume = 0.68f;
    }

    void QueueSongPreview()
    {
        StopSongPreview();
        if (_frontendPage != FrontendPage.SongSelect) return;
        var card = SelectedSongCard();
        if (card == null || card.DemoEntry.Length == 0 || !File.Exists(card.Path)) return;
        var serial = _previewSerial;
        _previewLoad = StartCoroutine(LoadSongPreview(card, _selectedSongIndex, serial));
    }

    System.Collections.IEnumerator LoadSongPreview(SongCard card, int index, int serial)
    {
        yield return new WaitForSecondsRealtime(0.32f);
        if (serial != _previewSerial || _frontendPage != FrontendPage.SongSelect
            || index != _selectedSongIndex)
            yield break;

        string cachePath;
        try
        {
            var cacheDir = Path.Combine(Application.temporaryCachePath, "MusePSonaPreviews");
            Directory.CreateDirectory(cacheDir);
            var extension = Path.GetExtension(card.DemoEntry).ToLowerInvariant();
            var stamp = File.GetLastWriteTimeUtc(card.Path).Ticks;
            cachePath = Path.Combine(cacheDir, "preview-" + index + "-" + stamp + extension);
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length < 16)
            {
                using var archive = ZipFile.OpenRead(card.Path);
                var entry = archive.GetEntry(card.DemoEntry) ?? archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.Name, Path.GetFileName(card.DemoEntry), StringComparison.OrdinalIgnoreCase));
                if (entry == null) yield break;
                using var source = entry.Open();
                using var output = File.Create(cachePath);
                source.CopyTo(output);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("试听解包失败: " + card.Path + "  " + ex.Message);
            yield break;
        }

        var type = ClipType(cachePath);
        if (type == AudioType.UNKNOWN) yield break;
        using var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(cachePath).AbsoluteUri, type);
        if (request.downloadHandler is DownloadHandlerAudioClip handler)
            handler.streamAudio = true;
        yield return request.SendWebRequest();
        if (serial != _previewSerial || _frontendPage != FrontendPage.SongSelect
            || index != _selectedSongIndex)
            yield break;
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning("试听加载失败: " + request.error + "  " + cachePath);
            yield break;
        }
        AudioClip clip;
        try { clip = DownloadHandlerAudioClip.GetContent(request); }
        catch (Exception ex)
        {
            Debug.LogWarning("试听解码失败: " + ex.Message);
            yield break;
        }
        SetupSongPreview();
        _previewAudio.clip = clip;
        _previewAudio.Play();
        _previewLoad = null;
    }

    void StopSongPreview()
    {
        _previewSerial++;
        if (_previewLoad != null)
        {
            StopCoroutine(_previewLoad);
            _previewLoad = null;
        }
        if (_previewAudio == null) return;
        _previewAudio.Stop();
        var clip = _previewAudio.clip;
        _previewAudio.clip = null;
        if (clip != null) Destroy(clip);
    }

    void StartSelectedSong()
    {
        var card = SelectedSongCard();
        if (card == null || card.Maps.Count == 0 || card.Error.Length > 0) return;
        StopSongPreview();
        _songIndex = _selectedSongIndex;
        s_map = _selectedDifficulty;
        difficulty = _selectedDifficulty;
        PlayerPrefs.SetString(SelectedSongPrefsKey, card.Path);
        PlayerPrefs.SetInt(SelectedDifficultyPrefsKey, _selectedDifficulty);
        PlayerPrefs.Save();
        _frontendPage = FrontendPage.Loading;
        _paused = false;
        SwitchSong(_selectedSongIndex);
    }

    void FinishSongLoading()
    {
        if (_frontendPage != FrontendPage.Loading) return;
        _frontendPage = FrontendPage.Playing;
        _paused = false;
    }

    void OpenSongSelect()
    {
        _songSwitchSerial++;
        if (_switchSong != null)
        {
            StopCoroutine(_switchSong);
            _switchSong = null;
        }
        if (_loadAudio != null)
        {
            StopCoroutine(_loadAudio);
            _loadAudio = null;
        }
        if (_audio != null)
        {
            _audio.Stop();
            var clip = _audio.clip;
            _audio.clip = null;
            if (clip != null) Destroy(clip);
        }
        ClearActors();
        ResetResultState();
        ClearGirlAction();
        MdSceneBg.Drop(_bg);
        _bg = null;
        _bgScene = "";
        _scene = "";
        _forceScene = "";
        _songTargetIndex = -1;
        _waitClip = false;
        _paused = false;
        _session = null;
        _song = null;
        _playKeysHeld.Clear();
        _frontendPage = FrontendPage.SongSelect;
        _songBackdropStartedAt = Time.unscaledTime;
        if (_songCatalog.Count != _songFiles.Count) BuildSongCatalog();
        if (_songCatalog.Count > 0)
            _selectedSongIndex = Mathf.Clamp(_songIndex, 0, _songCatalog.Count - 1);
        FitSelectedDifficulty();
        WarmVisibleCovers();
        QueueSongPreview();
    }

    void RescanSongCatalog()
    {
        _songUiCache.Clear();
        _songUiMissing.Clear();
        MdLooks.Reload();
        CollectSongs();
        BuildSongCatalog();
        InitializeSongSelection(PlayerPrefs.GetString(SelectedSongPrefsKey, ""));
    }

    void SetPaused(bool paused)
    {
        if (_frontendPage != FrontendPage.Playing || _resultOpen || _fullComboPlaying) return;
        _paused = paused;
        if (_audio == null || _audio.clip == null) return;
        if (_paused) _audio.Pause();
        else if (!_waitClip) _audio.UnPause();
    }

    void HandleSongSelectKey(KeyCode key)
    {
        if (key is KeyCode.LeftArrow or KeyCode.PageUp or KeyCode.F8)
            SelectSongDelta(-1);
        else if (key is KeyCode.RightArrow or KeyCode.PageDown or KeyCode.F4)
            SelectSongDelta(1);
        else if (key is KeyCode.UpArrow or KeyCode.LeftBracket or KeyCode.Minus or KeyCode.Comma)
            SelectDifficultyDelta(-1);
        else if (key is KeyCode.DownArrow or KeyCode.RightBracket or KeyCode.Equals or KeyCode.Period)
            SelectDifficultyDelta(1);
        else if (key is >= KeyCode.Alpha1 and <= KeyCode.Alpha5)
            SelectDifficulty(key - KeyCode.Alpha0);
        else if (key is >= KeyCode.Keypad1 and <= KeyCode.Keypad5)
            SelectDifficulty(key - KeyCode.Keypad0);
        else if (key == KeyCode.Q)
            RescanSongCatalog();
        else if (key == KeyCode.E)
            CycleChar(1);
        else if (key is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space)
            StartSelectedSong();
    }

    void DrawSongSelectBackdrop(float scale)
    {
        // 官机 BgAlbum1 不是 RayFx 放射贴图，而是两层立方体粒子和一层短线粒子。
        // BgAlbum1 静态配置 staticRotateSpeed=2；两次逐帧采集实测约 -2.01°/s。
        var t = Mathf.Max(0f, Time.unscaledTime - _songBackdropStartedAt);
        var rootDegrees = -2f * t;
        var ambient = BackdropAmbientColor(t);
        DrawSolid(new Rect(0f, 0f, SongSelectDesignWidth, SongSelectDesignHeight),
            new Color(0.075f, 0.018f, 0.15f));
        DrawSolid(new Rect(0f, 70f, SongSelectDesignWidth, SongSelectDesignHeight - 100f),
            Color.Lerp(new Color(0.26f, 0f, 0.44f), ambient, 0.22f));

        // F8 官机画面中，中央底色从紫红经过橙色转为绿色，边缘仍保持暗紫。
        // 用宽径向亮区表现该变化；没有逐帧材质颜色记录，时序按截图近似。
        var before = GUI.color;
        GUI.color = new Color(ambient.r, ambient.g, ambient.b, 1f);
        GUI.DrawTexture(new Rect(-100f, 70f, SongSelectDesignWidth + 200f,
            SongSelectDesignHeight - 170f), SongBackdropGlowTexture(), ScaleMode.StretchToFill, true);
        GUI.color = before;

        DrawBackdropAmbientFacets(t, rootDegrees, ambient);
        DrawBackdropParticleField(t, rootDegrees);
        DrawBackdropLineParticles(t, rootDegrees);

        DrawSolid(new Rect(0f, SongSelectDesignHeight - 100f, SongSelectDesignWidth, 100f),
            new Color(0.10f, 0.055f, 0.25f, 0.96f));
    }

    static Color BackdropAmbientColor(float time)
    {
        // 从 10 秒官机采集中的紫红、橙、绿区间取色，并平滑循环。
        var phase = Mathf.Repeat(time, 13f);
        var pink = new Color(0.96f, 0.02f, 0.48f);
        var orange = new Color(1f, 0.52f, 0.08f);
        var green = new Color(0.22f, 0.70f, 0.06f);
        if (phase < 2.6f) return pink;
        if (phase < 4.4f)
            return Color.Lerp(pink, orange, Mathf.SmoothStep(0f, 1f, (phase - 2.6f) / 1.8f));
        if (phase < 8f) return orange;
        if (phase < 9.8f)
            return Color.Lerp(orange, green, Mathf.SmoothStep(0f, 1f, (phase - 8f) / 1.8f));
        if (phase < 10.7f) return green;
        return Color.Lerp(green, pink, Mathf.SmoothStep(0f, 1f, (phase - 10.7f) / 2.3f));
    }

    void DrawBackdropAmbientFacets(float time, float rootDegrees, Color ambient)
    {
        // F8 橙色阶段可见大块连续色面。用缓慢外移的多边形层补足平滑底色，
        // 它位于立方体粒子后面，不参与两组 Cube 的数量与遮挡。
        var center = new Vector2(SongSelectDesignWidth * 0.5f, SongSelectDesignHeight * 0.5f);
        for (var i = 0; i < 18; i++)
        {
            var age = Mathf.Repeat(time / 13f + i / 18f, 1f);
            var theta = BackdropHash(i * 53 + 701) * Mathf.PI * 2f
                + rootDegrees * Mathf.Deg2Rad;
            var distance = Mathf.Lerp(100f, 960f, age * age);
            var x = center.x + Mathf.Cos(theta) * distance;
            var y = center.y + Mathf.Sin(theta) * distance * 0.62f;
            var size = Mathf.Lerp(400f, 1050f, age)
                * Mathf.Lerp(0.75f, 1.25f, BackdropHash(i * 41 + 29));
            var fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.12f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - age) / 0.16f));
            var color = Color.Lerp(ambient, new Color(0.44f, 0.01f, 0.38f),
                BackdropHash(i * 37 + 19) * 0.72f);
            color.a = fade * 0.38f;
            DrawRotatedTexture(new Rect(x - size * 0.5f, y - size * 0.32f,
                size, size * 0.64f), SongBackdropFacetTexture(), color,
                theta * Mathf.Rad2Deg + BackdropHash(i * 71 + 47) * 55f);
        }
    }

    void DrawBackdropParticleField(float time, float rootDegrees)
    {
        // 按粒子年龄从远到近绘制，近处的方块自然盖住远处的方块。
        const int depthBands = 8;
        for (var band = 0; band < depthBands; band++)
        {
            // 两个官机发射器同为每秒 8 粒、寿命 12 秒，稳定态各约 96 粒。
            DrawBackdropParticleDepthBand(time, rootDegrees, 96, true, band, depthBands);
            DrawBackdropParticleDepthBand(time, rootDegrees, 96, false, band, depthBands);
        }
    }

    void DrawBackdropParticleDepthBand(float time, float rootDegrees, int count, bool small,
        int depthBand, int depthBands)
    {
        const float lifetime = 12f;
        var center = new Vector2(SongSelectDesignWidth * 0.5f, SongSelectDesignHeight * 0.5f);
        var rootAngle = rootDegrees * Mathf.Deg2Rad;
        // 官机的两组发射器各自启用 autoRandomSeed；不能把大小 Cube 配成同一队列。
        var seed = small ? 3109 : 7919;
        for (var i = 0; i < count; i++)
        {
            // 均匀错开的生命周期模拟官机每秒持续发射，避免同一帧成片消失。
            var phase = Mathf.Repeat(i / (float)count + BackdropHash(i * 17 + seed) * 0.09f, 1f);
            var age = Mathf.Repeat(time / lifetime + phase, 1f);
            if (Mathf.Min(depthBands - 1, Mathf.FloorToInt(age * depthBands)) != depthBand)
                continue;
            // 发射器位于 Camera_3D 前方约 8.9 个世界单位，Z 速度为 1/s。
            // 镜头前的粒子会迅速放大并离开画面；它们不会在屏幕边缘缩小重生。
            var cameraDistance = 8.9f - age * lifetime;
            if (cameraDistance < 0.9f) continue;
            var perspective = 8.9f / cameraDistance;
            // 官机使用随机圆周角。黄金角会让方块过于均匀，缺少自然的疏密变化。
            var theta = BackdropHash(i * 29 + seed + 11) * Mathf.PI * 2f + rootAngle;
            // ShapeModule 的半径分别为 3.3 和 1.2，Radius Thickness = 0。
            var emitterRatio = small ? 1.2f / 3.3f : 1f;
            // 实机截图中，投影后的圆周在纵向被压进背景可视区域；
            // 上一版使用 500×500，让过多方块在上下边缘提前离屏。
            var x = center.x + Mathf.Cos(theta) * 450f * emitterRatio * perspective;
            var y = center.y + Mathf.Sin(theta) * 320f * emitterRatio * perspective;

            // 官机 startSize：ParCubeS=0.10–0.42，大 Cube=1–3。
            // 两者用同一 Mesh，尺寸差来自粒子大小，不应把小 Cube 拉成竖线。
            var sourceSize = small
                ? Mathf.Lerp(0.10f, 0.42f, BackdropHash(i * 71 + seed + 31))
                : Mathf.Lerp(1f, 3f, BackdropHash(i * 71 + seed + 31));
            var height = sourceSize * (small ? 125f : 90f);
            var width = height * Mathf.Lerp(0.88f, 1.42f, BackdropHash(i * 61 + seed + 23));
            width *= perspective;
            height *= perspective;
            var rect = new Rect(x - width * 0.5f, y - height * 0.5f, width, height);
            if (rect.xMax < -220f || rect.xMin > SongSelectDesignWidth + 220f
                || rect.yMax < 35f || rect.yMin > SongSelectDesignHeight + 160f)
                continue;

            // Rotation over Lifetime: 小 Cube 30°/s，大 Cube 5°/s；每粒从出生角开始旋转。
            var angle = Mathf.Lerp(0f, 193f, BackdropHash(i * 97 + seed + 47))
                + rootDegrees + age * lifetime * (small ? 30f : 5f);
            var color = BackdropParticleColor(age);
            DrawRotatedTexture(rect, SongBackdropCubeTexture(), color, angle);
        }
    }

    static Color BackdropParticleColor(float depth)
    {
        // F9 两组方块共用紫色材质；近景大块仍保留遮挡，不做透明淡出。
        var nearBlend = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0.25f, 0.65f, depth));
        return Color.Lerp(new Color(0.39f, 0f, 0.55f),
            new Color(0.25f, 0f, 0.40f), nearBlend);
    }

    static void DrawBackdropLineParticles(float time, float rootDegrees)
    {
        var center = new Vector2(SongSelectDesignWidth * 0.5f, SongSelectDesignHeight * 0.5f);
        // F9 抓取时 ParLine 未播放且粒子数为 0；常驻短线只模拟 38 个 ParRay。
        // ParRay 为 15/s × 2.5s，Start Size 最多 0.025，Stretch lengthScale=20。
        for (var i = 0; i < 38; i++)
        {
            var age = Mathf.Repeat(time / 2.5f + BackdropHash(i * 37 + 5), 1f);
            var fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.08f)) * (1f - age);
            var theta = i * 2.3999632f + BackdropHash(i * 73 + 17) * 0.8f
                + rootDegrees * Mathf.Deg2Rad;
            var birthRadius = 420f * Mathf.Lerp(0.85f, 1.15f, BackdropHash(i * 83 + 61));
            var radius = birthRadius * Mathf.Lerp(0.72f, 1.85f, age * age);
            var position = center + new Vector2(Mathf.Cos(theta) * radius,
                Mathf.Sin(theta) * radius * 0.52f);
            var length = Mathf.Lerp(18f, 105f, BackdropHash(i * 103 + 43))
                * Mathf.Lerp(0.65f, 1.55f, age);
            var thickness = Mathf.Lerp(1f, 3f, BackdropHash(i * 67 + 19));
            var color = new Color(0.852f, 0.588f, 1f, fade * 0.25f);
            DrawRotatedSolid(new Rect(position.x - length * 0.5f,
                position.y - thickness * 0.5f, length, thickness), color,
                theta * Mathf.Rad2Deg + age * 75f);
        }
    }

    static float BackdropHash(int value)
        => Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);

    bool DrawSongSelectHeader(float scale)
    {
        const float topH = 70f;
        var topRect = new Rect(0f, 0f, SongSelectDesignWidth, topH);
        if (TrySongUi("ui_top_base", out var topBase))
            DrawHorizontalSlicedTexture(topRect, topBase, 197f, 197f);
        else
            DrawSolid(topRect, new Color(0.12f, 0.075f, 0.28f, 0.98f));

        // 官机等级区由左右翻转的星星底座与中央进度槽组成。
        if (TrySongUi("ui_level_star_bg", out var starBase))
        {
            GUI.DrawTextureWithTexCoords(new Rect(620f, 0f, 340f, 100f), starBase,
                new Rect(1f, 0f, -1f, 1f), true);
            GUI.DrawTexture(new Rect(960f, 0f, 340f, 100f), starBase, ScaleMode.StretchToFill, true);
        }
        else
            DrawSolid(new Rect(730f, 0f, 460f, 96f), new Color(0.26f, 0.17f, 0.48f, 0.98f));

        if (TrySongUi("ui_level_slider", out var levelSlider))
        {
            var before = GUI.color;
            GUI.color = new Color(0.33f, 0.19f, 0.55f, 1f);
            GUI.DrawTexture(new Rect(770f, 0f, 380f, 66f), levelSlider, ScaleMode.StretchToFill, true);
            // 官机用同一遮罩绘制橙黄色经验填充；青色下沿属于后方星级底座。
            GUI.color = new Color(1f, 0.71f, 0f, 1f);
            GUI.DrawTexture(new Rect(770f, 0f, 380f, 66f), levelSlider, ScaleMode.StretchToFill, true);
            GUI.color = before;
        }
        else
            DrawSolid(new Rect(800f, 82f, 320f, 8f), new Color(0.05f, 0.94f, 0.82f));

        if (TrySongUi("ui_star", out var levelStar))
        {
            var before = GUI.color;
            GUI.color = new Color(1f, 0.93f, 0.05f, 0.92f);
            foreach (var x in new[] { 792f, 832f, 872f, 1018f, 1058f, 1098f })
                GUI.DrawTexture(new Rect(x, 3f, 32f, 30f), levelStar, ScaleMode.ScaleToFit, true);
            GUI.color = before;
        }

        var backRect = new Rect(0f, 0f, 184f, 70f);
        var optionRect = new Rect(1736f, 0f, 184f, 70f);
        if (TrySongUi("ui_option_button", out var optionButton))
        {
            GUI.DrawTexture(optionRect, optionButton, ScaleMode.StretchToFill, true);
        }
        if (TrySongUi("ui_back_button", out var backButton))
            GUI.DrawTexture(backRect, backButton, ScaleMode.StretchToFill, true);
        else if (TrySongUi("ui_option_button", out optionButton))
            GUI.DrawTextureWithTexCoords(backRect, optionButton, new Rect(1f, 0f, -1f, 1f), true);
        GUI.Label(new Rect(850f, -6f, 220f, 80f), "LEVEL  1", _selectBrandStyle);
        if (TrySongUi("ui_option_icon", out var optionIcon))
        {
            var iconBefore = GUI.color;
            GUI.color = new Color(0.376f, 0.082f, 0.502f, 1f);
            GUI.DrawTexture(new Rect(1828f, 15f, 40f, 40f), optionIcon, ScaleMode.ScaleToFit, true);
            GUI.color = iconBefore;
        }
        else
        {
            var gearBefore = GUI.color;
            GUI.color = new Color(0.36f, 0.07f, 0.55f, 1f);
            GUI.DrawTexture(new Rect(1828f, 15f, 40f, 40f), SongGearTexture(), ScaleMode.ScaleToFit, true);
            GUI.color = gearBefore;
        }
        if (GUI.Button(optionRect, GUIContent.none, GUIStyle.none))
            ToggleAudioOffsetPanel();

        const float tabY = 128f;
        const float tabH = 80f;
        if (TrySongUi("ui_chapter_base", out var chapterBase))
        {
            var before = GUI.color;
            GUI.color = new Color(0.15f, 0.09f, 0.30f, 1f);
            DrawHorizontalSlicedTexture(new Rect(260f, 116f, 1400f, 80f), chapterBase, 56f);
            GUI.color = before;
        }
        var refresh = SongUiButton(new Rect(280f, tabY, 580f, tabH),
            "ui_chapter_button", "", new Color(0.52f, 0.28f, 0.94f, 0.98f),
            _selectTabStyle, flipX: true);
        if (TrySongUi("ui_search_icon", out var searchIcon))
            GUI.DrawTexture(new Rect(441.5f, 145.5f, 45f, 45f), searchIcon, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(496.5f, 133f, 132f, 70f), "change", _selectTabStyle);
        var qRect = new Rect(646f, 145.5f, 45f, 45f);
        if (TrySongUi("ui_key_q", out var keyQ))
        {
            var before = GUI.color;
            GUI.color = new Color(0.400f, 0.149f, 0.757f, 1f);
            GUI.DrawTexture(qRect, keyQ, ScaleMode.ScaleToFit, true);
            GUI.color = before;
        }
        else
        {
            DrawSolid(qRect, new Color(0.40f, 0.15f, 0.76f, 0.96f));
            GUI.Label(qRect, "Q", _selectDiscLabelStyle);
        }

        var character = SongUiButton(new Rect(1060f, tabY, 580f, tabH),
            "ui_store_button", "", new Color(1f, 0.69f, 0.02f, 0.98f), _selectTabStyle);
        var tabTextColor = _selectTabStyle.normal.textColor;
        var tabFontSize = _selectTabStyle.fontSize;
        _selectTabStyle.fontSize = 40;
        _selectTabStyle.normal.textColor = new Color(0.88f, 0.58f, 0.10f, 1f);
        GUI.Label(new Rect(1268f, 133f, 100f, 70f), "角色", _selectTabStyle);
        _selectTabStyle.fontSize = tabFontSize;
        _selectTabStyle.normal.textColor = tabTextColor;
        var eRect = new Rect(1388f, 145.5f, 45f, 45f);
        if (TrySongUi("ui_key_e", out var keyE))
        {
            var before = GUI.color;
            GUI.color = new Color(0.82f, 0.48f, 0.08f, 1f);
            GUI.DrawTexture(eRect, keyE, ScaleMode.ScaleToFit, true);
            GUI.color = before;
        }
        else
        {
            DrawSolid(eRect, new Color(0.82f, 0.48f, 0.08f, 0.96f));
            GUI.Label(eRect, "E", _selectDiscLabelStyle);
        }
        if (character)
            CycleChar(1);
        if (!refresh) return false;
        RescanSongCatalog();
        return true;
    }

    void DrawSongInfoBase()
    {
        // 官机底栏分为后景窄条、曲名/作者中层和覆盖屏幕底部的前景三层。
        // 三层按此顺序绘制，既保留楔形轮廓，也会正确遮住最远端唱片。
        if (TrySongUi("ui_stage_info_back", out var back))
        {
            DrawHorizontalSlicedTexture(new Rect(635f, 851f, 325f, 50f), back,
                0f, 31f, true);
            DrawHorizontalSlicedTexture(new Rect(960f, 851f, 325f, 50f), back,
                0f, 31f);
        }

        if (TrySongUi("ui_stage_info_middle", out var middle))
        {
            DrawHorizontalSlicedTexture(new Rect(522f, 900f, 438f, 82f), middle,
                3f, 46f, true);
            DrawHorizontalSlicedTexture(new Rect(960f, 900f, 438f, 82f), middle,
                3f, 46f);
        }
        else
            DrawSolid(new Rect(522f, 900f, 876f, 82f), new Color(0.20f, 0.13f, 0.40f, 0.98f));

        var drewBottom = false;
        if (TrySongUi("ui_stage_info_bottom_reverse", out var bottomLeft))
        {
            DrawHorizontalSlicedTexture(new Rect(0f, 980f, 960f, 100f), bottomLeft,
                60f, 420f);
            drewBottom = true;
        }
        if (TrySongUi("ui_stage_info_bottom", out var bottomRight))
        {
            DrawHorizontalSlicedTexture(new Rect(960f, 980f, 960f, 100f), bottomRight,
                450f, 30f);
            drewBottom = true;
        }
        if (!drewBottom)
            DrawSolid(new Rect(0f, 980f, SongSelectDesignWidth, 100f),
                new Color(0.10f, 0.055f, 0.25f, 0.98f));
    }

    static void DrawRotatedSolid(Rect rect, Color color, float angle)
    {
        var matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, rect.center);
        DrawSolid(rect, color);
        GUI.matrix = matrix;
    }

    static void DrawRotatedTexture(Rect rect, Texture2D texture, Color color, float angle)
    {
        if (texture == null) return;
        var matrix = GUI.matrix;
        var before = GUI.color;
        GUIUtility.RotateAroundPivot(angle, rect.center);
        GUI.color = color;
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
        GUI.color = before;
        GUI.matrix = matrix;
    }

    static void DrawHorizontalSlicedTexture(Rect rect, Texture2D texture, float sourceBorder)
        => DrawHorizontalSlicedTexture(rect, texture, sourceBorder, sourceBorder);

    static void DrawHorizontalSlicedTexture(Rect rect, Texture2D texture,
        float sourceLeftBorder, float sourceRightBorder, bool flipX = false)
    {
        if (texture == null) return;
        var sourceCenter = texture.width - sourceLeftBorder - sourceRightBorder;
        var destinationLeft = flipX ? sourceRightBorder : sourceLeftBorder;
        var destinationRight = flipX ? sourceLeftBorder : sourceRightBorder;
        if (sourceCenter <= 0f || rect.width <= destinationLeft + destinationRight)
        {
            GUI.DrawTextureWithTexCoords(rect, texture,
                flipX ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f), true);
            return;
        }

        var invWidth = 1f / texture.width;
        if (!flipX)
        {
            DrawTextureSegment(new Rect(rect.x, rect.y, destinationLeft, rect.height), texture,
                0f, sourceLeftBorder, invWidth, false);
            DrawTextureSegment(new Rect(rect.x + destinationLeft, rect.y,
                    rect.width - destinationLeft - destinationRight, rect.height), texture,
                sourceLeftBorder, sourceCenter, invWidth, false);
            DrawTextureSegment(new Rect(rect.xMax - destinationRight, rect.y,
                    destinationRight, rect.height), texture,
                sourceLeftBorder + sourceCenter, sourceRightBorder, invWidth, false);
            return;
        }

        DrawTextureSegment(new Rect(rect.x, rect.y, destinationLeft, rect.height), texture,
            sourceLeftBorder + sourceCenter, sourceRightBorder, invWidth, true);
        DrawTextureSegment(new Rect(rect.x + destinationLeft, rect.y,
                rect.width - destinationLeft - destinationRight, rect.height), texture,
            sourceLeftBorder, sourceCenter, invWidth, true);
        DrawTextureSegment(new Rect(rect.xMax - destinationRight, rect.y,
                destinationRight, rect.height), texture,
            0f, sourceLeftBorder, invWidth, true);
    }

    static void DrawTextureSegment(Rect rect, Texture2D texture,
        float sourceX, float sourceWidth, float invTextureWidth, bool flipX)
    {
        if (rect.width <= 0f || sourceWidth <= 0f) return;
        var uvX = flipX ? (sourceX + sourceWidth) * invTextureWidth : sourceX * invTextureWidth;
        var uvWidth = sourceWidth * invTextureWidth * (flipX ? -1f : 1f);
        GUI.DrawTextureWithTexCoords(rect, texture, new Rect(uvX, 0f, uvWidth, 1f), true);
    }

    bool TrySongUi(string key, out Texture2D texture)
    {
        if (_songUiCache.TryGetValue(key, out texture) && texture != null) return true;
        if (_songUiMissing.Contains(key))
        {
            texture = null;
            return false;
        }
        if (MdLooks.TrySprite("common", key, out texture) && texture != null)
        {
            _songUiCache[key] = texture;
            return true;
        }
        _songUiMissing.Add(key);
        texture = null;
        return false;
    }

    bool SongUiButton(Rect rect, string textureKey, string label, Color fallback,
        GUIStyle labelStyle = null, bool flipX = false)
    {
        if (TrySongUi(textureKey, out var texture))
        {
            var before = GUI.color;
            GUI.color = Color.white;
            if (textureKey is "ui_chapter_button" or "ui_store_button")
                DrawHorizontalSlicedTexture(rect, texture, 56f, 56f, flipX);
            else
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = before;
        }
        else
            DrawSolid(rect, fallback);
        GUI.Label(rect, label, labelStyle ?? _selectDiscLabelStyle);
        var clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        var e = Event.current;
        return clicked && (e == null || !e.isKey);
    }

    void EnsureSongDiscTexture()
    {
        if (_songDiscTexture != null) return;
        const int size = 192;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "song-select-disc",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = (x + 0.5f) / size - 0.5f;
            var dy = (y + 0.5f) / size - 0.5f;
            var radius = Mathf.Sqrt(dx * dx + dy * dy);
            Color color;
            if (radius > 0.5f) color = Color.clear;
            else if (radius > 0.465f) color = new Color(0.015f, 0.012f, 0.025f, 1f);
            else if (radius > 0.425f) color = new Color(0.08f, 0.065f, 0.11f, 1f);
            else
            {
                var groove = Mathf.Repeat(radius * 42f, 1f) < 0.22f ? 0.035f : 0f;
                color = new Color(0.12f + groove, 0.035f + groove, 0.20f + groove, 1f);
            }
            pixels[y * size + x] = color;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        _songDiscTexture = texture;
    }

    Texture2D SongDiscTexture()
    {
        if (TrySongUi("ui_song_disc", out var official)) return official;
        EnsureSongDiscTexture();
        return _songDiscTexture;
    }

    Texture2D SongGearTexture()
    {
        if (_songGearTexture != null) return _songGearTexture;
        const int size = 64;
        _songGearTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "song-select-gear",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = (x + 0.5f) / size - 0.5f;
            var dy = (y + 0.5f) / size - 0.5f;
            var radius = Mathf.Sqrt(dx * dx + dy * dy);
            var angle = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 0.5f;
            var tooth = Mathf.Abs(Mathf.Repeat(angle * 8f, 1f) - 0.5f) < 0.22f;
            var outer = tooth ? 0.49f : 0.41f;
            pixels[y * size + x] = radius <= outer && radius >= 0.17f
                ? new Color32(255, 255, 255, 255)
                : new Color32(0, 0, 0, 0);
        }
        _songGearTexture.SetPixels32(pixels);
        _songGearTexture.Apply(false, true);
        return _songGearTexture;
    }

    Texture2D SongBackdropGlowTexture()
    {
        if (_songBackdropGlowTexture != null) return _songBackdropGlowTexture;
        const int size = 128;
        _songBackdropGlowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "song-select-backdrop-glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            var dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 0.65f);
            pixels[y * size + x] = new Color32(255, 255, 255,
                (byte)Mathf.RoundToInt(alpha * 255f));
        }
        _songBackdropGlowTexture.SetPixels32(pixels);
        _songBackdropGlowTexture.Apply(false, true);
        return _songBackdropGlowTexture;
    }

    Texture2D SongBackdropFacetTexture()
    {
        if (_songBackdropFacetTexture != null) return _songBackdropFacetTexture;
        const int size = 128;
        _songBackdropFacetTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "song-select-backdrop-facet",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        FillBackdropPolygon(pixels, size, new[]
        {
            new Vector2(0f, 20f), new Vector2(97f, 0f),
            new Vector2(127f, 35f), new Vector2(110f, 127f),
            new Vector2(23f, 119f)
        }, new Color32(255, 255, 255, 255));
        _songBackdropFacetTexture.SetPixels32(pixels);
        _songBackdropFacetTexture.Apply(false, true);
        return _songBackdropFacetTexture;
    }

    Texture2D SongBackdropCubeTexture()
    {
        if (_songBackdropCubeTexture != null) return _songBackdropCubeTexture;
        const int size = 256;
        const float p = size / 128f;
        _songBackdropCubeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "song-select-backdrop-cube",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var pixels = new Color32[size * size];
        // 三面的灰阶由 GUI.color 统一染色，保留官机立方体粒子的面间明暗。
        FillBackdropPolygon(pixels, size, new[]
        {
            new Vector2(5f, 28f) * p, new Vector2(97f, 4f) * p,
            new Vector2(123f, 27f) * p, new Vector2(31f, 51f) * p
        }, new Color32(221, 221, 221, 255));
        FillBackdropPolygon(pixels, size, new[]
        {
            new Vector2(31f, 51f) * p, new Vector2(123f, 27f) * p,
            new Vector2(123f, 101f) * p, new Vector2(31f, 124f) * p
        }, new Color32(255, 255, 255, 255));
        FillBackdropPolygon(pixels, size, new[]
        {
            new Vector2(5f, 28f) * p, new Vector2(31f, 51f) * p,
            new Vector2(31f, 124f) * p, new Vector2(5f, 101f) * p
        }, new Color32(142, 142, 142, 255));
        _songBackdropCubeTexture.SetPixels32(pixels);
        _songBackdropCubeTexture.Apply(false, true);
        return _songBackdropCubeTexture;
    }

    static void FillBackdropPolygon(Color32[] pixels, int size, Vector2[] points,
        Color32 color)
    {
        var minX = size - 1;
        var maxX = 0;
        var minY = size - 1;
        var maxY = 0;
        for (var i = 0; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, Mathf.FloorToInt(points[i].x));
            maxX = Mathf.Max(maxX, Mathf.CeilToInt(points[i].x));
            minY = Mathf.Min(minY, Mathf.FloorToInt(points[i].y));
            maxY = Mathf.Max(maxY, Mathf.CeilToInt(points[i].y));
        }
        minX = Mathf.Clamp(minX, 0, size - 1);
        maxX = Mathf.Clamp(maxX, 0, size - 1);
        minY = Mathf.Clamp(minY, 0, size - 1);
        maxY = Mathf.Clamp(maxY, 0, size - 1);
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                var a = points[i];
                var b = points[j];
                if ((a.y > y + 0.5f) != (b.y > y + 0.5f)
                    && x + 0.5f < (b.x - a.x) * (y + 0.5f - a.y)
                    / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            if (inside) pixels[y * size + x] = color;
        }
    }

    void ResizeSongSelectStyles(float scale)
    {
        _selectBrandStyle.fontSize = 36;
        _selectTitleStyle.fontSize = 34;
        _selectSongStyle.fontSize = 34;
        _selectCenteredSongStyle.fontSize = 48;
        _selectAuthorStyle.fontSize = 40;
        _selectMetaStyle.fontSize = 24;
        _selectHintStyle.fontSize = 24;
        _selectDifficultyStyle.fontSize = 24;
        _selectDiscLabelStyle.fontSize = 27;
        _selectArrowStyle.fontSize = 70;
        _selectTabStyle.fontSize = 36;
    }

    void DrawSongSelect()
    {
        EnsureSongSelectStyles();
        var oldColor = GUI.color;
        var oldBackground = GUI.backgroundColor;
        var oldMatrix = GUI.matrix;
        var scale = Mathf.Min(Screen.width / SongSelectDesignWidth, Screen.height / SongSelectDesignHeight);
        var offsetX = (Screen.width - SongSelectDesignWidth * scale) * 0.5f;
        var offsetY = (Screen.height - SongSelectDesignHeight * scale) * 0.5f;
        GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
            new Vector3(scale, scale, 1f));
        ResizeSongSelectStyles(1f);
        DrawSongSelectBackdrop(1f);
        if (DrawSongSelectHeader(1f))
        {
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            GUI.backgroundColor = oldBackground;
            return;
        }

        if (_songCatalog.Count == 0)
        {
            GUI.Label(new Rect(0f, 410f, SongSelectDesignWidth, 90f),
                "没有找到 MDM 谱面\n请放入 StreamingAssets/Charts 或导出目录", _selectSongStyle);
            GUI.backgroundColor = new Color(1f, 0.16f, 0.55f);
            if (GuiClick(new Rect(850f, 648f, 220f, 52f), "重新扫描"))
                RescanSongCatalog();
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            GUI.backgroundColor = oldBackground;
            return;
        }

        if (Event.current != null && Event.current.type == EventType.ScrollWheel
            && Mathf.Abs(Event.current.delta.y) > 0.05f)
        {
            SelectSongDelta(Event.current.delta.y > 0f ? 1 : -1);
            Event.current.Use();
        }

        WarmVisibleCovers();
        // 官机由五个唱片槽构成；先画远端，再画近端与中心，以保持正确遮挡。
        DrawSongSlot(-2, SongDiscSlots[0]);
        DrawSongSlot(2, SongDiscSlots[4]);
        DrawSongSlot(-1, SongDiscSlots[1]);
        DrawSongSlot(1, SongDiscSlots[3]);
        DrawSongCover(_selectedSongIndex, SongDiscSlots[2], true);

        var leftArrow = new Rect(146.8f, 482.4f, 94.4f, 115.2f);
        var rightArrow = new Rect(1678.8f, 482.4f, 94.4f, 115.2f);
        DrawSongArrow(leftArrow, false);
        DrawSongArrow(rightArrow, true);
        if (GUI.Button(leftArrow, GUIContent.none, GUIStyle.none)) SelectSongDelta(-1);
        if (GUI.Button(rightArrow, GUIContent.none, GUIStyle.none)) SelectSongDelta(1);

        DrawSongInfoBase();

        var selected = SelectedSongCard();
        // 官机细节对照期间，暂时隐藏中央唱片下方的圆角专辑标签及其文字。

        DrawSongPreviewProgress();
        GUI.Label(new Rect(660f, 862.5f, 600f, 55f), selected.Name, _selectCenteredSongStyle);
        GUI.Label(new Rect(570f, 913f, 780f, 60f), selected.Author, _selectAuthorStyle);
        DrawSongCopyButton(selected);
        if (selected.Error.Length > 0)
        {
            var before = _selectMetaStyle.normal.textColor;
            _selectMetaStyle.normal.textColor = new Color(1f, 0.48f, 0.48f);
            GUI.Label(new Rect(420f, 935f, 1080f, 42f), selected.Error, _selectMetaStyle);
            _selectMetaStyle.normal.textColor = before;
        }

        DrawSongDifficulties(selected, 975f, 1f);
        DrawSongFooterIcon(new Rect(750f, 975f, 80f, 80f), "ui_like_button", "♥",
            new Color(0.31f, 0.10f, 0.46f));
        DrawSongFooterIcon(new Rect(1090f, 975f, 80f, 80f), "ui_achievement_button", "★",
            new Color(1f, 0.74f, 0.02f));

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
        GUI.backgroundColor = oldBackground;
    }

    void DrawSongCopyButton(SongCard selected)
    {
        // 官机 BtnCopy：设计坐标 (1293, 917.7)，原物件 60×60、localScale 0.9。
        var rect = new Rect(1293f, 917.7f, 54f, 54f);
        if (TrySongUi("ui_copy_button", out var copyButton))
        {
            var before = GUI.color;
            GUI.color = new Color(0.439f, 0.153f, 0.678f, 1f);
            GUI.DrawTexture(rect, copyButton, ScaleMode.ScaleToFit, true);
            GUI.color = before;
        }
        else
        {
            var before = _selectMetaStyle.normal.textColor;
            _selectMetaStyle.normal.textColor = new Color(0.439f, 0.153f, 0.678f, 1f);
            GUI.Label(rect, "▣", _selectMetaStyle);
            _selectMetaStyle.normal.textColor = before;
        }

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
            GUIUtility.systemCopyBuffer = selected.Name;
    }

    void DrawSongSlot(int offset, Rect rect)
    {
        if (_songCatalog.Count <= 1 || Mathf.Abs(offset) >= _songCatalog.Count)
        {
            DrawEmptySongDisc(rect);
            return;
        }
        var index = (_selectedSongIndex + offset % _songCatalog.Count + _songCatalog.Count)
            % _songCatalog.Count;
        DrawSongCover(index, rect, false);
    }

    void DrawSongPreviewProgress()
    {
        var rect = new Rect(585f, 812.5f, 750f, 15f);
        DrawSolid(rect, new Color(0.17f, 0.04f, 0.32f, 0.88f));
        var progress = 0f;
        if (_previewAudio != null && _previewAudio.clip != null && _previewAudio.clip.length > 0.001f)
            progress = Mathf.Clamp01(_previewAudio.time / _previewAudio.clip.length);
        DrawSolid(new Rect(rect.x, rect.y, rect.width * progress, rect.height),
            new Color(0.86f, 0.69f, 0.91f, 0.96f));
    }

    void DrawSongFooterIcon(Rect rect, string textureKey, string symbol, Color color)
    {
        if (TrySongUi(textureKey, out var texture))
        {
            var old = GUI.color;
            GUI.color = textureKey == "ui_achievement_button"
                ? new Color(0.50f, 0f, 0.66f, 1f)
                : Color.white;
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            if (textureKey == "ui_achievement_button"
                && TrySongUi("ui_achievement_icon", out var achievementIcon))
            {
                GUI.color = Color.white;
                GUI.DrawTexture(rect, achievementIcon, ScaleMode.ScaleToFit, true);
            }
            GUI.color = old;
            return;
        }
        DrawSolid(rect, new Color(color.r * 0.42f, color.g * 0.42f, color.b * 0.42f, 0.92f));
        var before = _selectDiscLabelStyle.normal.textColor;
        _selectDiscLabelStyle.normal.textColor = color;
        GUI.Label(rect, symbol, _selectDiscLabelStyle);
        _selectDiscLabelStyle.normal.textColor = before;
    }

    void DrawSongCover(int index, Rect rect, bool selected)
    {
        EnsureSongCover(index);
        var card = _songCatalog[index];
        var before = GUI.color;
        // 官机所有 ImgBase 均保持白色；非选中状态只压暗 ImgCover。
        GUI.color = Color.white;
        GUI.DrawTexture(rect, SongDiscTexture(), ScaleMode.StretchToFill, true);
        if (card.Cover != null)
        {
            GUI.color = selected ? Color.white : new Color(0.333f, 0.333f, 0.333f, 1f);
            // 官机 ImgCover 相对唱片边缘约缩进 7.7%。
            var inset = rect.width * 0.077f;
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f),
                card.Cover, ScaleMode.ScaleAndCrop, true);
        }
        else if (TrySongUi("ui_random_song_cover", out var randomCover))
        {
            GUI.color = selected ? Color.white : new Color(0.333f, 0.333f, 0.333f, 1f);
            var inset = rect.width * 0.077f;
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset,
                    rect.width - inset * 2f, rect.height - inset * 2f),
                randomCover, ScaleMode.ScaleAndCrop, true);
        }
        else
        {
            GUI.color = selected ? Color.white : new Color(0.62f, 0.50f, 0.75f, 0.82f);
            GUI.Label(rect, "?", _selectDiscLabelStyle);
        }
        GUI.color = before;
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none) && Event.current != null && !Event.current.isKey)
        {
            if (selected && card.Error.Length == 0)
            {
                StartSelectedSong();
                return;
            }
            _selectedSongIndex = index;
            FitSelectedDifficulty();
            WarmVisibleCovers();
            QueueSongPreview();
        }
    }

    void DrawEmptySongDisc(Rect rect)
    {
        var before = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(rect, SongDiscTexture(), ScaleMode.StretchToFill, true);
        if (TrySongUi("ui_random_song_cover", out var randomCover))
        {
            GUI.color = new Color(0.333f, 0.333f, 0.333f, 1f);
            var inset = rect.width * 0.077f;
            GUI.DrawTexture(new Rect(rect.x + inset, rect.y + inset,
                    rect.width - inset * 2f, rect.height - inset * 2f),
                randomCover, ScaleMode.ScaleAndCrop, true);
        }
        else
        {
            GUI.color = new Color(0.62f, 0.50f, 0.75f, 0.82f);
            GUI.Label(rect, "?", _selectDiscLabelStyle);
        }
        GUI.color = before;
    }

    void DrawSongArrow(Rect rect, bool right)
    {
        if (!TrySongUi("ui_album_switch", out var arrow))
        {
            GUI.Label(rect, right ? "›" : "‹", _selectArrowStyle);
            return;
        }
        var before = GUI.color;
        GUI.color = new Color(0.88f, 0.67f, 1f, 0.95f);
        var uv = right ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
        GUI.DrawTextureWithTexCoords(rect, arrow, uv, true);
        GUI.color = before;
    }

    void DrawSongDifficulties(SongCard card, float y, float scale)
    {
        if (card == null || card.Maps.Count == 0) return;
        var baseRect = new Rect(850f, y, 220f, 80f);
        if (TrySongUi("ui_difficulty_base", out var difficultyBase))
        {
            var before = GUI.color;
            GUI.color = new Color(0.153f, 0.094f, 0.302f, 1f);
            DrawHorizontalSlicedTexture(baseRect, difficultyBase, 39f, 40f);
            GUI.color = before;
        }

        var upperMap = _selectedDifficulty >= 3
            ? _selectedDifficulty
            : card.Maps.FirstOrDefault(map => map >= 3);
        if (upperMap < 3) upperMap = 3;
        var displayedMaps = new[] { 1, 2, upperMap };
        for (var slot = 0; slot < displayedMaps.Length; slot++)
        {
            var map = displayedMaps[slot];
            var rect = new Rect(850f + slot * 70f, y, 80f, 80f);
            if (!card.Maps.Contains(map))
            {
                if (TrySongUi("ui_difficulty_empty", out var empty))
                    GUI.DrawTexture(CenteredRect(rect, 50f, 50f), empty, ScaleMode.ScaleToFit, true);
                continue;
            }

            var star = string.IsNullOrWhiteSpace(card.Stars[map]) || card.Stars[map] == "0"
                ? "?" : card.Stars[map];
            if (DrawSongDifficultyButton(rect, map, star))
                SelectDifficulty(map);
        }
    }

    bool DrawSongDifficultyButton(Rect rect, int map, string star)
    {
        var before = GUI.color;
        var iconRect = CenteredRect(rect, 50f, 50f);
        var hiddenDifficulty = map == 5;
        if (TrySongUi(SongDifficultyIconKey(map), out var icon))
        {
            GUI.color = Color.white;
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        }
        else
        {
            DrawSolid(iconRect, DifficultyColor(map));
            if (hiddenDifficulty)
                GUI.Label(rect, "•••", _selectDifficultyStyle);
        }
        GUI.color = before;

        // IconHide 是未揭示占位，图标自身已经包含三点暗纹，不叠加数字。
        // map4 是揭示后的里谱，官机仍使用 Master 的玫红星并显示难度数字。
        if (hiddenDifficulty)
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);

        var suffix = star == "?" ? "unknown" : star;
        if (TrySongUi("ui_difficulty_value_" + suffix, out var glyph))
        {
            var glyphBefore = GUI.color;
            GUI.color = DifficultyTextColor(map);
            // 新版导出物使用官机 80×80 TxtValue 固定文本槽；兼容旧的紧边界数字纹理。
            var glyphRect = glyph.width == 80 && glyph.height == 80
                ? rect : CenteredRect(iconRect, glyph.width, glyph.height);
            glyphRect.position += DifficultyGlyphOffset(map);
            GUI.DrawTexture(glyphRect, glyph, ScaleMode.StretchToFill, true);
            GUI.color = glyphBefore;
        }
        else
        {
            var textBefore = _selectDifficultyStyle.normal.textColor;
            _selectDifficultyStyle.normal.textColor = DifficultyTextColor(map);
            var textRect = rect;
            textRect.position += DifficultyGlyphOffset(map);
            GUI.Label(textRect, star, _selectDifficultyStyle);
            _selectDifficultyStyle.normal.textColor = textBefore;
        }
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    static Rect CenteredRect(Rect outer, float width, float height) =>
        new(outer.center.x - width * 0.5f, outer.center.y - height * 0.5f, width, height);

    // 三张官机星形的内部数字槽并不共享同一个视觉中心。
    // 水平按图标分别校准；纵向取 0（偏上）与 2（偏下）的中点。
    static Vector2 DifficultyGlyphOffset(int map) => map switch
    {
        1 => new Vector2(1f, 1f),
        2 => new Vector2(0f, 1f),
        3 or 4 => new Vector2(1f, 1f),
        _ => new Vector2(0f, 1f)
    };

    static Color DifficultyTextColor(int map) => map switch
    {
        1 => new Color(0.075f, 0.384f, 0.369f, 1f),
        2 => new Color(0.188f, 0.396f, 0.518f, 1f),
        _ => new Color(0.447f, 0.129f, 0.561f, 1f)
    };

    static string SongDifficultyIconKey(int map) => map switch
    {
        1 => "ui_difficulty_easy",
        2 => "ui_difficulty_hard",
        3 or 4 => "ui_difficulty_master",
        5 => "ui_difficulty_hidden",
        _ => "ui_star"
    };

    static Color DifficultyColor(int map) => map switch
    {
        1 => new Color(1f, 0.77f, 0.03f),
        2 => new Color(0.38f, 0.72f, 0.98f),
        3 or 4 => new Color(1f, 0.14f, 0.52f),
        5 => new Color(0.12f, 0.92f, 0.82f),
        _ => new Color(0.36f, 0.42f, 0.72f)
    };

    void DrawLoadingPage()
    {
        EnsureSongSelectStyles();
        var old = GUI.color;
        GUI.color = new Color(0.055f, 0.012f, 0.12f, 0.98f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.40f, Screen.width, 72f), "LOADING...", _selectBrandStyle);
        var card = SelectedSongCard();
        if (card != null)
            GUI.Label(new Rect(0f, Screen.height * 0.51f, Screen.width, 52f), card.Name, _selectAuthorStyle);
        GUI.Label(new Rect(0f, Screen.height - 70f, Screen.width, 36f), "Esc 返回选曲", _selectHintStyle);
        GUI.color = old;
    }

    void DrawPausePanel()
    {
        EnsureSongSelectStyles();
        var oldColor = GUI.color;
        var oldBackground = GUI.backgroundColor;
        GUI.color = new Color(0.025f, 0.008f, 0.06f, 0.86f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        var w = Mathf.Min(520f, Screen.width - 40f);
        var h = 360f;
        var x = (Screen.width - w) * 0.5f;
        var y = (Screen.height - h) * 0.5f;
        GUI.backgroundColor = new Color(0.23f, 0.07f, 0.36f, 0.98f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);
        GUI.Label(new Rect(x, y + 28f, w, 64f), "暂停", _selectTitleStyle);
        GUI.backgroundColor = new Color(1f, 0.14f, 0.52f);
        if (GuiClick(new Rect(x + 110f, y + 112f, w - 220f, 52f), "继续游戏")) SetPaused(false);
        GUI.backgroundColor = new Color(0.28f, 0.64f, 0.92f);
        if (GuiClick(new Rect(x + 110f, y + 182f, w - 220f, 52f), "重新开始"))
        {
            SetPaused(false);
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
        }
        GUI.backgroundColor = new Color(0.50f, 0.40f, 0.68f);
        if (GuiClick(new Rect(x + 110f, y + 252f, w - 220f, 52f), "返回选曲")) OpenSongSelect();
        GUI.color = oldColor;
        GUI.backgroundColor = oldBackground;
    }

    static void DrawSolid(Rect rect, Color color)
    {
        var before = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = before;
    }

    void EnsureSongSelectStyles()
    {
        // 新增样式后也允许 Play Mode 热重载补建，避免旧实例只持有部分样式。
        if (_selectBrandStyle != null && _selectTabStyle != null && _selectDifficultyStyle != null) return;
        _selectBrandStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        _selectBrandStyle.normal.textColor = new Color(0.76f, 1f, 1f);
        _selectTitleStyle = new GUIStyle(_selectBrandStyle) { fontSize = 34 };
        _selectTitleStyle.normal.textColor = new Color(1f, 0.40f, 0.76f);
        _selectSongStyle = new GUIStyle(_selectBrandStyle)
        {
            fontSize = 34,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
        _selectSongStyle.normal.textColor = Color.white;
        _selectCenteredSongStyle = new GUIStyle(_selectSongStyle)
        {
            fontSize = 40,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        _selectCenteredSongStyle.normal.textColor = new Color(0.992f, 0.918f, 1f, 1f);
        _selectAuthorStyle = new GUIStyle(_selectSongStyle)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        _selectAuthorStyle.normal.textColor = new Color(1f, 1f, 1f, 0.298f);
        _selectMetaStyle = new GUIStyle(_selectAuthorStyle) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        _selectMetaStyle.normal.textColor = new Color(0.83f, 0.79f, 0.92f);
        _selectHintStyle = new GUIStyle(_selectMetaStyle) { alignment = TextAnchor.MiddleCenter };
        _selectDifficultyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        _selectDiscLabelStyle = new GUIStyle(_selectBrandStyle)
        {
            fontSize = 25,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        _selectDiscLabelStyle.normal.textColor = Color.white;
        _selectArrowStyle = new GUIStyle(_selectBrandStyle)
        {
            fontSize = 70,
            alignment = TextAnchor.MiddleCenter
        };
        _selectArrowStyle.normal.textColor = new Color(0.92f, 0.74f, 1f, 0.92f);
        _selectTabStyle = new GUIStyle(_selectBrandStyle)
        {
            fontSize = 36,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        _selectTabStyle.normal.textColor = new Color(0.401f, 0.150f, 0.755f, 1f);
    }
}
