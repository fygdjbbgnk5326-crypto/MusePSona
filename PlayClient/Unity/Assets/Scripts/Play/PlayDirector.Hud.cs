using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>IMGUI：状态栏、难度、切景调试。</summary>
public sealed partial class PlayDirector
{
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
        if (_session == null)
        {
            GUI.Label(new Rect(12, 12, 720, 40), "无谱面  Assets/StreamingAssets/" + chartFile, _hudStyle);
            DrawSongSwitch();
            DrawMapButtons();
            if (TempCharSwitch) DrawTempCharButtons();
            else if (TempSceneSwitch) DrawTempSceneButtons();
            return;
        }

        var map = _session.Difficulty;
        var mode = autoplay ? "自动" : "手动 DFJK";
        if (_paused) mode = "暂停";
        var sceneHud = _session.SceneAt(Clock());
        if (_forceScene.Length > 0) sceneHud = _forceScene + " 强制";
        GUI.Label(new Rect(12, 10, 980, 22),
            $"{_session.Song.Document.Meta.Name}  map{map}  {mode}  P{_session.Perfects} G{_session.Greats} M{_session.Misses}  {sceneHud}",
            _hudStyle);
        GUI.Label(new Rect(12, 32, 980, 22),
            TempCharSwitch
                ? "D/F空中 J/K地面  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌 F6/F7换角色"
                : TempSceneSwitch
                ? "D/F空中 J/K地面  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌 F6/F7切景"
                : "D/F空中 J/K地面  F1切模式 F2手动重开 F3自动重开  空格暂停 R重开  F4/F8换歌",
            _hudStyle);
        // TEMP 测试读秒，过后删除。
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
        GUI.Label(new Rect(12, 56, 720, 48), Clock().ToString("0.00") + "s" + belt + "  " + _bossHud, _clockStyle);
        DrawSongSwitch();
        DrawMapButtons();
        if (TempCharSwitch) DrawTempCharButtons();
        else if (TempSceneSwitch) DrawTempSceneButtons();

        const float comboY = 58f;
        const float comboH = 62f;
        if (_session.Combo > 0 && _comboSpine == null && _comboNumRoot == null)
            GUI.Label(new Rect((Screen.width - 360f) * 0.5f, comboY, 360f, comboH), _session.Combo.ToString(), _comboStyle);
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
        _gradeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            clipping = TextClipping.Overflow
        };
    }
}
