using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>IMGUI：状态栏、难度、切景调试。</summary>
public sealed partial class PlayDirector
{
    readonly Texture2D[] _scoreDigitTextures = new Texture2D[10];
    bool _scoreDigitTexturesReady;
    bool _scoreDigitTexturesFound;

    void OnGUI()
    {
        var e = UnityEngine.Event.current;
        if (CaptureGuiPlayKey(e))
        {
            // Update 仍是主输入源；IMGUI 补获发生在两帧之间的快速滚键。
            // PressPlayKey 的物理键锁会过滤系统长按重复与双来源重复。
            e.Use();
        }
        else if (e != null && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
        {
            // 非游玩快捷键仍由 Update 统一处理。
            e.Use();
        }
        GUI.FocusControl(null);

        EnsureStyles();
        if (_audioOffsetPanelOpen)
        {
            DrawAudioOffsetPanel();
            return;
        }
        if (_session == null)
        {
            GUI.Label(new Rect(12, 12, 720, 40), "无谱面  Assets/StreamingAssets/" + chartFile, _hudStyle);
            DrawSongSwitch();
            DrawMapButtons();
            DrawAudioOffsetTest();
            if (TempCharSwitch) DrawTempCharButtons();
            else if (TempSceneSwitch) DrawTempSceneButtons();
            return;
        }
        if (_resultOpen)
        {
            DrawResultPanel();
            return;
        }
        if (_fullComboPlaying)
        {
            // 曲终演出保持官机式干净画面：只留左上分数及场景内原有 Combo，
            // 隐藏调试按钮/时钟，FULL COMBO 始终画在最上层。
            DrawScoreHud();
            DrawFullComboOverlay();
            return;
        }

        RefreshHudText();
        DrawScoreHud();
        var debugX = Mathf.Max(12f, 310f * HudReferenceScale());
        var debugW = Mathf.Max(120f, Screen.width - debugX - 12f);
        GUI.Label(new Rect(debugX, 10, debugW, 22), _hudStatusText, _hudStyle);
        GUI.Label(new Rect(debugX, 32, debugW, 22),
            TempCharSwitch
                ? KeyboardHint() + "  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌 F6/F7换角色  F12校准"
                : TempSceneSwitch
                ? KeyboardHint() + "  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌 F6/F7切景  F12校准"
                : KeyboardHint() + "  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌  F12校准",
            _hudStyle);
        GUI.Label(new Rect(debugX, 56, debugW, 48), _hudClockText, _clockStyle);
        DrawSongSwitch();
        DrawMapButtons();
        DrawAudioOffsetTest();
        DrawAdvancedJudgeToggle();
        DrawKeyboardLayoutToggle();
        if (TempCharSwitch) DrawTempCharButtons();
        else if (TempSceneSwitch) DrawTempSceneButtons();

        const float comboY = 58f;
        const float comboH = 62f;
        if (_session.Combo > 0 && _comboSpine == null && _comboNumRoot == null)
            GUI.Label(new Rect((Screen.width - 360f) * 0.5f, comboY, 360f, comboH), _hudComboText, _comboStyle);
    }

    static float HudReferenceScale()
        => Mathf.Clamp(Screen.width / 1920f, 0.5f, 1.5f);

    /// <summary>
    /// 官机 Score 根锚在左上 (80,130)，数字字号 72，AP 图标是 68×66 的
    /// icon_ap。这里按 1920 宽参考缩放，避免 IMGUI 在小 GameView 中挤占轨道。
    /// </summary>
    void DrawScoreHud()
    {
        if (_session == null) return;
        var scale = HudReferenceScale();
        var x = 60f * scale;
        var y = 2f * scale;
        var score = _session.Score.ToString();
        _scoreHudStyle.fontSize = Mathf.Max(32, Mathf.RoundToInt(72f * scale));
        _scoreTitleFallbackStyle.fontSize = Mathf.Max(18, Mathf.RoundToInt(40f * scale));

        var oldColor = GUI.color;
        var numberWidth = DrawOfficialScoreDigits(score, x, y, scale);
        if (numberWidth <= 0f)
        {
            var content = new GUIContent(score);
            var numberSize = _scoreHudStyle.CalcSize(content);
            numberWidth = numberSize.x;
            // 官机数字使用浅青色；资源未导出时保留旧字体回退。
            var scoreRect = new Rect(x, y, Mathf.Max(180f * scale, numberSize.x + 8f), 72f * scale);
            GUI.color = new Color(0.20f, 0.06f, 0.34f, 0.85f);
            GUI.Label(new Rect(scoreRect.x + 2f * scale, scoreRect.y + 3f * scale,
                scoreRect.width, scoreRect.height), content, _scoreHudStyle);
            GUI.color = new Color(0.747f, 1f, 1f, 1f);
            GUI.Label(scoreRect, content, _scoreHudStyle);
        }
        GUI.color = oldColor;

        if (_session.Greats == 0 && _session.Misses == 0)
        {
            var apX = x + numberWidth + 8f * scale;
            if (MdLooks.TrySprite(_scene, "hud_ap", out var ap) && ap != null)
                GUI.DrawTexture(new Rect(apX, y + 7f * scale, 68f * scale, 66f * scale),
                    ap, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(new Rect(apX, y + 8f * scale, 58f * scale, 40f * scale), "AP", _scoreTitleFallbackStyle);
        }

        if (MdLooks.TrySprite(_scene, "hud_score_title", out var title) && title != null)
            GUI.DrawTexture(new Rect(x, y + 70f * scale, 95f * scale, 46f * scale),
                title, ScaleMode.ScaleToFit, true);
        else
            GUI.Label(new Rect(x, y + 70f * scale, 96f * scale, 46f * scale),
                "得分", _scoreTitleFallbackStyle);
    }

    float DrawOfficialScoreDigits(string score, float x, float y, float scale)
    {
        if (!_scoreDigitTexturesReady)
        {
            _scoreDigitTexturesFound = true;
            for (var i = 0; i < _scoreDigitTextures.Length; i++)
            {
                if (!MdLooks.TrySprite(_scene, "score_digit_" + i, out _scoreDigitTextures[i])
                    || _scoreDigitTextures[i] == null)
                {
                    _scoreDigitTexturesFound = false;
                    break;
                }
            }
            _scoreDigitTexturesReady = true;
        }
        if (!_scoreDigitTexturesFound || string.IsNullOrEmpty(score)) return 0f;

        var width = 0f;
        // 导出的单字四周各含 2px 透明边；Unity UI Text 按字体 advance
        // 排列，不应把相邻字的两侧 padding 再算作字距。
        const float transparentPadding = 4f;
        for (var i = 0; i < score.Length; i++)
        {
            var digit = score[i] - '0';
            if (digit < 0 || digit > 9) continue;
            var tex = _scoreDigitTextures[digit];
            width += Mathf.Max(1f, tex.width - transparentPadding) * scale;
        }
        width += transparentPadding * scale;

        var cursor = x;
        for (var i = 0; i < score.Length; i++)
        {
            var digit = score[i] - '0';
            if (digit < 0 || digit > 9) continue;
            var tex = _scoreDigitTextures[digit];
            var rect = new Rect(cursor, y + 7f * scale,
                tex.width * scale, tex.height * scale);
            // UI Text 本身没有描边；轻微紫影只用于复杂背景上的可读性。
            GUI.color = new Color(0.26f, 0.08f, 0.38f, 0.70f);
            GUI.DrawTexture(new Rect(rect.x + 2f * scale, rect.y + 3f * scale,
                rect.width, rect.height), tex, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
            cursor += Mathf.Max(1f, tex.width - transparentPadding) * scale;
        }
        return width;
    }

    void DrawAdvancedJudgeToggle()
    {
        const float width = 202f;
        const float height = 26f;
        var x = Mathf.Max(12f, Screen.width - width - 12f);
        var y = 88f;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = _showAdvancedJudge
            ? new Color(0.45f, 0.90f, 0.72f)
            : new Color(0.62f, 0.64f, 0.70f);
        var label = "EARLY/LATE 小P  " + (_showAdvancedJudge ? "开" : "关");
        if (GuiClick(new Rect(x, y, width, height), label))
        {
            _showAdvancedJudge = !_showAdvancedJudge;
            PlayerPrefs.SetInt(AdvancedJudgePrefsKey, _showAdvancedJudge ? 1 : 0);
            PlayerPrefs.Save();
        }
        GUI.backgroundColor = old;
    }

    void DrawKeyboardLayoutToggle()
    {
        const float width = 202f;
        const float height = 26f;
        var x = Mathf.Max(12f, Screen.width - width - 12f);
        var y = 118f;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.64f, 0.76f, 0.96f);
        if (GuiClick(new Rect(x, y, width, height), "键位  " + KeyboardLayoutName()))
            CycleKeyboardLayout();
        GUI.backgroundColor = old;
    }

    void RefreshHudText()
    {
        if (_session == null) return;
        if (Time.unscaledTime < _hudTextNextAt && _hudStatusText.Length > 0) return;
        _hudTextNextAt = Time.unscaledTime + 0.05f;
        var mode = _paused ? "暂停" : autoplay ? "自动" : "手动 " + KeyboardLayoutName();
        var clock = Clock();
        var sceneHud = _forceScene.Length > 0
            ? _forceScene + " 强制"
            : _session.SceneAt(clock);
        _hudStatusText = _session.Song.Document.Meta.Name + "  map" + _session.Difficulty
            + "  " + mode + "  P" + _session.Perfects + " G" + _session.Greats
            + " M" + _session.Misses + "  S" + _session.Score.ToString("000000")
            + "  " + _session.Accuracy.ToString("0.00") + "%  " + sceneHud;
        var belt = "";
        if (_bg != null)
        {
            foreach (var layer in _bg.Layers)
            {
                if (layer.Speed <= 1f) continue;
                belt = "  横移" + layer.Shift.ToString("0.0");
                break;
            }
        }
        _hudClockText = clock.ToString("0.00") + "s" + belt + "  " + _bossHud;
        _hudComboText = _session.Combo.ToString();
    }

    void DrawAudioOffsetTest()
    {
        const float buttonW = 72f;
        const float valueW = 126f;
        const float h = 26f;
        const float gap = 4f;
        var total = buttonW + valueW + gap;
        var x = Mathf.Max(12f, Screen.width - total - 12f);
        var y = 58f;
        var old = GUI.backgroundColor;
        GUI.backgroundColor = Time.unscaledTime < _audioOffsetNoticeUntil
            ? new Color(1f, 0.85f, 0.2f)
            : new Color(0.95f, 0.95f, 0.98f);
        GUI.Box(new Rect(x, y, valueW, h), "偏移 " + AudioOffset.Format(_audioOffsetMs));
        x += valueW + gap;
        GUI.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        if (GuiClick(new Rect(x, y, buttonW, h), "校准 F12")) ToggleAudioOffsetPanel();
        GUI.backgroundColor = old;
    }

    float ChromeBottom()
    {
        var above = 16f;
        if (!TempMapKeys) above += 44f;
        if (TempCharSwitch || TempSceneSwitch) above += 8f + 32f;
        return above;
    }

    // IMGUI 的 Button 在按键时若鼠标停在控件上会当点击；F2 会误触「重开」。
    static bool GuiClick(Rect r, string label)
    {
        var hit = GUI.Button(r, label);
        var e = UnityEngine.Event.current;
        if (e != null && e.isKey) return false;
        return hit;
    }

    void DrawSongSwitch()
    {
        if (_songFiles.Count < 2) return;
        var h = 28f;
        var y = Screen.height - ChromeBottom() - 8f - h;
        var label = _songIndex >= 0 && _songIndex < _songFiles.Count
            ? SongLabel(_songFiles[_songIndex])
            : "换歌";
        var nameW = Mathf.Clamp(label.Length * 14f, 160f, 360f);
        var total = 72f + 8f + nameW + 8f + 72f;
        var x = Mathf.Max(12f, (Screen.width - total) * 0.5f);
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        if (GuiClick(new Rect(x, y, 72f, h), "上一首"))
            CycleSong(-1);
        x += 80f;
        GUI.backgroundColor = new Color(0.95f, 0.95f, 0.98f);
        GUI.Box(new Rect(x, y, nameW, h), label);
        x += nameW + 8f;
        GUI.backgroundColor = new Color(0.62f, 0.78f, 0.95f);
        if (GuiClick(new Rect(x, y, 72f, h), "下一首"))
            CycleSong(1);
        GUI.backgroundColor = prev;
    }

    void DrawMapButtons()
    {
        if (TempMapKeys) return;
        if (_song == null) return;
        var maps = _song.Maps;
        if (maps.Count == 0) return;
        var w = 140f;
        var h = 44f;
        var gap = 10f;
        var total = maps.Count * w + (maps.Count - 1) * gap;
        var x = Mathf.Max(12f, (Screen.width - total) * 0.5f);
        var y = Screen.height - h - 16f;
        for (var i = 0; i < maps.Count; i++)
        {
            var d = maps[i];
            var on = _session != null && _session.Difficulty == d;
            var star = _song.Star(d);
            var label = string.IsNullOrWhiteSpace(star) || star == "0"
                ? $"map{d}"
                : $"map{d}  ★{star}";
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = on ? new Color(1f, 0.85f, 0.2f) : new Color(0.75f, 0.78f, 0.85f);
            if (GuiClick(new Rect(x, y, w, h), label))
                SwitchMap(d);
            GUI.backgroundColor = prev;
            x += w + gap;
        }
    }

    void DrawTempSceneButtons()
    {
        const int n = 12;
        var w = 52f;
        var h = 32f;
        var gap = 6f;
        var followW = 72f;
        var restartW = 72f;
        var total = restartW + gap + followW + gap + n * w + (n - 1) * gap;
        var x = Mathf.Max(12f, (Screen.width - total) * 0.5f);
        var y = Screen.height - (TempMapKeys ? 16f : 44f + 16f + 8f) - h;
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.95f, 0.55f, 0.45f);
        if (GuiClick(new Rect(x, y, restartW, h), "重开"))
            SwitchMap(_session != null ? _session.Difficulty : difficulty);
        x += restartW + gap;
        GUI.backgroundColor = _forceScene.Length == 0 ? new Color(1f, 0.85f, 0.2f) : new Color(0.75f, 0.78f, 0.85f);
        if (GuiClick(new Rect(x, y, followW, h), "跟谱"))
        {
            _forceScene = "";
            if (_session != null) TintScene(_session.SceneAt(Clock()));
        }
        x += followW + gap;
        var shown = _forceScene.Length > 0 ? _forceScene : _scene;
        for (var i = 1; i <= n; i++)
        {
            var id = "scene_" + i.ToString("00");
            var on = shown == id && _forceScene.Length > 0;
            GUI.backgroundColor = on ? new Color(1f, 0.85f, 0.2f) : new Color(0.75f, 0.78f, 0.85f);
            if (GuiClick(new Rect(x, y, w, h), i.ToString("00")))
            {
                _forceScene = id;
                MdSceneBg.InvalidateLayout();
                _bgScene = "";
                TintScene(id);
            }
            x += w + gap;
        }
        GUI.backgroundColor = prev;
    }

    void EnsureStyles()
    {
        if (_hudStyle != null) return;
        _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        _comboStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        _comboStyle.normal.textColor = Color.white;
        _clockStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Overflow
        };
        _clockStyle.normal.textColor = new Color(1f, 0.92f, 0.35f);
        _scoreHudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Overflow,
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 0, 0)
        };
        _scoreTitleFallbackStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 40,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Overflow
        };
        _scoreTitleFallbackStyle.normal.textColor = new Color(0.76f, 0.66f, 1f);
        _gradeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            clipping = TextClipping.Overflow
        };
    }
}
