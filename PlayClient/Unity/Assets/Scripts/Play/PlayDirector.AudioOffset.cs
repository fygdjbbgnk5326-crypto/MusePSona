using MusePSona.Play;
using UnityEngine;

/// <summary>覆盖式音频偏移校准页：独立测试音，不改谱面 delay。</summary>
public sealed partial class PlayDirector
{
    const float OffsetBeatSeconds = 1f;

    void ToggleAudioOffsetPanel()
    {
        if (_audioOffsetPanelOpen) CloseAudioOffsetPanel();
        else OpenAudioOffsetPanel();
    }

    void OpenAudioOffsetPanel()
    {
        if (_audioOffsetPanelOpen) return;
        _audioOffsetPanelOpen = true;
        _audioOffsetWasPaused = _paused;
        _audioOffsetResumeMusic = _audio != null && _audio.isPlaying;
        if (_audioOffsetResumeMusic) _audio.Pause();
        _paused = true;
        StartAudioOffsetTest();
    }

    void CloseAudioOffsetPanel(bool resumeMusic = true)
    {
        if (!_audioOffsetPanelOpen && _audioOffsetTestAudio == null) return;
        _audioOffsetPanelOpen = false;
        StopAudioOffsetTest();
        _paused = _audioOffsetWasPaused;
        if (resumeMusic && _audioOffsetResumeMusic && !_paused && _audio != null && _audio.clip != null)
            _audio.UnPause();
        _audioOffsetResumeMusic = false;
        SaveAudioOffset();
    }

    void StartAudioOffsetTest()
    {
        if (_audioOffsetTestAudio == null)
        {
            _audioOffsetTestAudio = gameObject.AddComponent<AudioSource>();
            _audioOffsetTestAudio.playOnAwake = false;
            _audioOffsetTestAudio.loop = true;
            _audioOffsetTestAudio.volume = 0.7f;
        }
        if (_audioOffsetTestClip == null)
            _audioOffsetTestClip = CreateAudioOffsetClick();
        _audioOffsetTestAudio.Stop();
        _audioOffsetTestAudio.clip = _audioOffsetTestClip;
        _audioOffsetTestStartDsp = AudioSettings.dspTime + 0.2;
        _audioOffsetTestAudio.PlayScheduled(_audioOffsetTestStartDsp);
    }

    void StopAudioOffsetTest()
    {
        if (_audioOffsetTestAudio != null) _audioOffsetTestAudio.Stop();
    }

    static AudioClip CreateAudioOffsetClick()
    {
        var rate = Mathf.Max(22050, AudioSettings.outputSampleRate);
        var data = new float[rate];
        var clickSamples = Mathf.Min(rate, Mathf.RoundToInt(rate * 0.035f));
        for (var i = 0; i < clickSamples; i++)
        {
            var t = i / (float)rate;
            var envelope = 1f - i / (float)clickSamples;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1320f * t) * envelope * envelope * 0.65f;
        }
        var clip = AudioClip.Create("MusePSonaOffsetClick", rate, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void DrawAudioOffsetPanel()
    {
        var oldColor = GUI.color;
        GUI.color = new Color(0.08f, 0.025f, 0.12f, 0.96f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        var compact = Screen.height < 600f;
        var top = compact ? 18f : 38f;
        var width = Mathf.Min(920f, Screen.width - 48f);
        var left = (Screen.width - width) * 0.5f;
        GUI.Label(new Rect(left, top, width, 52f), "音频偏移校准", _comboStyle);
        GUI.Label(new Rect(left, top + 54f, width, 28f),
            "听测试音，使黄色标记经过中央基准线时与重音重合。正值让画面更晚。", CenterHudStyle());

        var track = new Rect(left + 30f, top + 100f, width - 60f,
            Mathf.Clamp(Screen.height * 0.25f, 110f, 210f));
        GUI.color = new Color(1f, 0.08f, 0.48f, 0.45f);
        GUI.DrawTexture(track, Texture2D.whiteTexture);
        GUI.color = Color.white;
        var centerX = track.center.x;
        GUI.DrawTexture(new Rect(centerX - 2f, track.y, 4f, track.height), Texture2D.whiteTexture);

        var elapsed = AudioSettings.dspTime - _audioOffsetTestStartDsp - AudioOffset.ToSeconds(_audioOffsetMs);
        var phase = Mathf.Repeat((float)(elapsed / OffsetBeatSeconds) + 0.5f, 1f) - 0.5f;
        var markerX = centerX + phase * (track.width - 24f);
        GUI.color = new Color(1f, 0.92f, 0.1f);
        GUI.DrawTexture(new Rect(markerX - 5f, track.y + 14f, 10f, track.height - 28f), Texture2D.whiteTexture);
        GUI.color = oldColor;

        var valueY = track.yMax + 10f;
        GUI.Label(new Rect(left, valueY, width, 52f), AudioOffset.Format(_audioOffsetMs), _comboStyle);
        const float buttonW = 104f;
        const float buttonH = 42f;
        const float gap = 12f;
        var total = buttonW * 5f + gap * 4f;
        var x = (Screen.width - total) * 0.5f;
        var y = valueY + 56f;
        if (GuiClick(new Rect(x, y, buttonW, buttonH), "-0.010")) ChangeAudioOffset(-AudioOffset.FastStepMs);
        x += buttonW + gap;
        if (GuiClick(new Rect(x, y, buttonW, buttonH), "-0.001")) ChangeAudioOffset(-AudioOffset.StepMs);
        x += buttonW + gap;
        if (GuiClick(new Rect(x, y, buttonW, buttonH), "归零")) SetAudioOffset(0);
        x += buttonW + gap;
        if (GuiClick(new Rect(x, y, buttonW, buttonH), "+0.001")) ChangeAudioOffset(AudioOffset.StepMs);
        x += buttonW + gap;
        if (GuiClick(new Rect(x, y, buttonW, buttonH), "+0.010")) ChangeAudioOffset(AudioOffset.FastStepMs);

        GUI.Label(new Rect(left, y + 58f, width, 25f),
            "F9/F10 精调 0.001 s　Shift+F9/F10 快调 0.010 s　F11 归零", CenterHudStyle());
        if (GuiClick(new Rect((Screen.width - 180f) * 0.5f, y + 94f, 180f, 44f), "保存并返回  F12"))
            CloseAudioOffsetPanel();
    }

    GUIStyle CenterHudStyle()
    {
        if (_offsetCenterStyle != null) return _offsetCenterStyle;
        _offsetCenterStyle = new GUIStyle(_hudStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18
        };
        _offsetCenterStyle.normal.textColor = new Color(1f, 0.72f, 0.9f);
        return _offsetCenterStyle;
    }
}
