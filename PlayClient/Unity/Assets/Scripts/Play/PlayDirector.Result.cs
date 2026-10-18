using MusePSona.Play;
using UnityEngine;

/// <summary>官机式判定偏差提示与单曲结算页。</summary>
public sealed partial class PlayDirector
{
    enum FullComboTheme
    {
        Standard,
        Touhou,
        DjMax,
        Arknights
    }

    // 官机高级判定：±25ms 内为亮 P，不显示方向；其外的小 P 和 Great 显示 Early/Late。
    const float ShinyPerfectSec = 0.025f;
    // 官机 gamemain 的 FullComboText AnimationClip：0–2.0s，非循环。
    const float FullComboDuration = 2.0f;
    // 官机 16:9 画面中，整行位于上下判定圈中点，宽约占画面 56%。
    const float FullComboWidthRatio = 0.56f;
    const float FullComboHeightRatio = 0.16f;
    const float FullComboCenterYRatio = 0.50f;
    // FullComboText 并非把十张字图连续等距排开：FULL / COMBO 之间有
    // 明显词距，末尾惊叹号前也留有较小停顿。单位沿用原图像素，随
    // 整体 scale 缩放，保持不同分辨率下的官方排版比例。
    const float FullComboLetterGap = 14f;
    const float TouhouFullComboLetterGap = 4f;
    const float FullComboWordGap = 70f;
    const float FullComboBangGap = 18f;
    static readonly string[] FullComboSpriteKeys =
    {
        "full_combo_f", "full_combo_u", "full_combo_l", "full_combo_l",
        "full_combo_c", "full_combo_o", "full_combo_m", "full_combo_b",
        "full_combo_o", "full_combo_bang"
    };
    static readonly string[] TouhouFullComboSpriteKeys =
    {
        "full_combo_touhou_f", "full_combo_touhou_u", "full_combo_touhou_l",
        "full_combo_touhou_l", "full_combo_touhou_c", "full_combo_touhou_o",
        "full_combo_touhou_m", "full_combo_touhou_b", "full_combo_touhou_o",
        "full_combo_touhou_bang"
    };
    static readonly float[] FullComboLetterAngles =
        { -8f, 5f, -4f, 3f, -5f, 4f, -3f, 5f, -4f, 7f };
    bool _resultOpen;
    bool _resultSequenceStarted;
    bool _fullComboPlaying;
    float _fullComboAt = -99f;
    readonly Texture2D[] _fullComboTextures = new Texture2D[FullComboSpriteKeys.Length];
    bool _fullComboTexturesReady;
    bool _fullComboTexturesFound;
    FullComboTheme _fullComboLetterTheme = (FullComboTheme)(-1);
    GUIStyle _resultTitleStyle;
    GUIStyle _resultScoreStyle;
    GUIStyle _resultBodyStyle;
    GUIStyle _resultSmallStyle;
    GUIStyle _fullComboFallbackStyle;

    void ResetResultState()
    {
        _resultOpen = false;
        _resultSequenceStarted = false;
        _fullComboPlaying = false;
        _fullComboAt = -99f;
        _fullComboTexturesReady = false;
        _fullComboTexturesFound = false;
        _fullComboLetterTheme = (FullComboTheme)(-1);
    }

    /// <summary>
    /// 官机 JudgeDisplay 将 Early/Late 放在 Perfect/Great 判定显示下。
    /// Miss 与零偏差（Just）目前不显示；Hold 尾和连打内部拍也没有独立输入偏差。
    /// </summary>
    string HitTimingText(PlaySession.JudgeHit hit)
    {
        if (!_showAdvancedJudge || autoplay || _resultOpen
            || hit.Note.Category == NoteCategory.Gear
            || hit.Note.IsPassiveCollectible
            || hit.Grade is Judge.Grade.Pass or Judge.Grade.Miss
            || !hit.HasTiming)
            return "";

        var ms = Mathf.RoundToInt(Mathf.Abs(hit.OffsetSec) * 1000f);
        if (Mathf.Abs(hit.OffsetSec) <= ShinyPerfectSec) return "";
        return (hit.OffsetSec < 0f ? "EARLY  " : "LATE  ") + ms + "ms";
    }

    void TickResult(float now)
    {
        if (_resultOpen || _session == null || _waitClip) return;
        if (_fullComboPlaying)
        {
            if (Time.unscaledTime - _fullComboAt >= FullComboDuration)
                OpenResultPanel();
            return;
        }
        if (_resultSequenceStarted || _paused) return;
        if (!_session.IsFinished(now)) return;
        // 官机在乐曲收尾后进入结算。无音频的测试谱则在最后判定完成后进入。
        if (_audio != null && _audio.clip != null && _audio.isPlaying) return;

        _resultSequenceStarted = true;
        _paused = true;
        if (_audio != null && _audio.isPlaying) _audio.Pause();

        // 官机只有有效谱面且全程无 Miss 才播放 FULL COMBO；AP 同样属于 FC。
        // 演出期间冻结谱面与输入，背景和角色保持曲终画面，2 秒后再开结算。
        if (_session.TotalScoreUnits > 0 && _session.Misses == 0)
        {
            _fullComboPlaying = true;
            _fullComboAt = Time.unscaledTime;
            return;
        }

        OpenResultPanel();
    }

    void OpenResultPanel()
    {
        _fullComboPlaying = false;
        _resultOpen = true;
        _paused = true;
        if (_audio != null && _audio.isPlaying) _audio.Pause();
        ClearGirlAction();
    }

    void DrawFullComboOverlay()
    {
        if (!_fullComboPlaying) return;
        var age = Mathf.Max(0f, Time.unscaledTime - _fullComboAt);
        var fade = Mathf.Clamp01((FullComboDuration - age) / 0.20f);
        var theme = ResolveFullComboTheme();
        if (theme == FullComboTheme.DjMax && DrawDjMaxFullCombo(age, fade)) return;
        if (theme == FullComboTheme.Arknights && DrawArknightsFullCombo(age, fade)) return;

        var letterTheme = theme == FullComboTheme.Touhou
            ? FullComboTheme.Touhou
            : FullComboTheme.Standard;
        var spriteKeys = letterTheme == FullComboTheme.Touhou
            ? TouhouFullComboSpriteKeys
            : FullComboSpriteKeys;
        var rawWidth = 0f;
        if (!_fullComboTexturesReady || _fullComboLetterTheme != letterTheme)
        {
            _fullComboTexturesFound = true;
            for (var i = 0; i < _fullComboTextures.Length; i++)
            {
                if (!MdLooks.TrySprite(_scene, spriteKeys[i], out _fullComboTextures[i])
                    || _fullComboTextures[i] == null)
                {
                    _fullComboTexturesFound = false;
                    break;
                }
            }
            _fullComboTexturesReady = true;
            _fullComboLetterTheme = letterTheme;
        }
        if (!_fullComboTexturesFound && letterTheme == FullComboTheme.Touhou)
        {
            // 旧的本地素材包可能尚未重新提取联动字图；退回普通官机字图，
            // 不让资源缺失把演出降级成 Unity 系统字体。
            _fullComboTexturesFound = true;
            for (var i = 0; i < _fullComboTextures.Length; i++)
            {
                if (!MdLooks.TrySprite(_scene, FullComboSpriteKeys[i], out _fullComboTextures[i])
                    || _fullComboTextures[i] == null)
                {
                    _fullComboTexturesFound = false;
                    break;
                }
            }
            _fullComboLetterTheme = letterTheme;
        }
        var rawGap = letterTheme == FullComboTheme.Standard
            ? FullComboLetterGap
            : TouhouFullComboLetterGap;
        if (_fullComboTexturesFound)
        {
            for (var i = 0; i < _fullComboTextures.Length; i++)
            {
                rawWidth += _fullComboTextures[i].width;
                if (i < _fullComboTextures.Length - 1)
                    rawWidth += rawGap + FullComboExtraGapAfter(i);
            }
        }

        if (!_fullComboTexturesFound || rawWidth <= 0f)
        {
            EnsureResultStyles();
            var old = GUI.color;
            var size = Mathf.RoundToInt(Mathf.Clamp(Screen.width * 0.063f, 42f, 104f));
            if (_fullComboFallbackStyle == null)
                _fullComboFallbackStyle = new GUIStyle(_resultTitleStyle);
            _fullComboFallbackStyle.fontSize = size;
            _fullComboFallbackStyle.normal.textColor = new Color(1f, 0.33f, 0.82f, fade);
            var p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.35f));
            GUI.Label(new Rect(0f,
                Screen.height * FullComboCenterYRatio - size * 0.75f - (1f - p) * 45f,
                Screen.width, size * 1.5f), "FULL  COMBO !", _fullComboFallbackStyle);
            GUI.color = old;
            return;
        }

        // 使用画面比例而非固定像素：不同 GameView 尺寸下仍保持官机的
        // 字号与锚点，不会因窗口变化重新压到角色和下判定圈上。
        var scale = Mathf.Min(Screen.width * FullComboWidthRatio / rawWidth,
            Screen.height * FullComboHeightRatio / 113f);
        var totalWidth = rawWidth * scale;
        var x = (Screen.width - totalWidth) * 0.5f;
        var centerY = Screen.height * FullComboCenterYRatio;
        var oldMatrix = GUI.matrix;
        var oldColor = GUI.color;
        for (var i = 0; i < _fullComboTextures.Length; i++)
        {
            var tex = _fullComboTextures[i];
            var delay = i * 0.055f;
            var p = Mathf.Clamp01((age - delay) / 0.24f);
            if (p <= 0f)
            {
                x += (tex.width + rawGap + FullComboExtraGapAfter(i)) * scale;
                continue;
            }
            // 每个字母依次从下方弹入，先越过 1.0 再回弹，对齐 FullComboText
            // 的逐字 Transform 曲线；最后 200ms 整体淡出接结算页。
            var pop = p < 0.68f
                ? Mathf.Lerp(0.15f, 1.16f, Mathf.SmoothStep(0f, 1f, p / 0.68f))
                : Mathf.Lerp(1.16f, 1f, Mathf.SmoothStep(0f, 1f, (p - 0.68f) / 0.32f));
            var w = tex.width * scale * pop;
            var h = tex.height * scale * pop;
            var cellW = tex.width * scale;
            var rect = new Rect(x + (cellW - w) * 0.5f,
                centerY - h * 0.5f + (1f - p) * 42f * scale, w, h);
            var pivot = rect.center;
            GUIUtility.RotateAroundPivot(FullComboLetterAngles[i] * p, pivot);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
            GUI.matrix = oldMatrix;
            x += (tex.width + rawGap + FullComboExtraGapAfter(i)) * scale;
        }
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    FullComboTheme ResolveFullComboTheme()
    {
        var meta = _session?.Song?.Document?.Meta;
        var hints = "";
        if (meta != null)
        {
            hints = (meta.Name ?? "") + " " + (meta.NameRomanized ?? "") + " "
                + (meta.Author ?? "");
            if (meta.SearchTags != null)
                for (var i = 0; i < meta.SearchTags.Length; i++)
                    hints += " " + (meta.SearchTags[i] ?? "");
        }

        // 联动标签优先于场景默认值，允许自制谱通过 SearchTags 精确指定主题。
        if (HasThemeHint(hints, "arknights", "明日方舟", "rhodes", "罗德岛"))
            return FullComboTheme.Arknights;
        if (HasThemeHint(hints, "touhou", "东方"))
            return FullComboTheme.Touhou;
        if (HasThemeHint(hints, "djmax"))
            return FullComboTheme.DjMax;
        // scene_09 的 Combo HUD 已按官机使用 DJMAX 皮肤，曲终提示保持同一主题。
        if (IsDjMaxCombo(_scene)) return FullComboTheme.DjMax;
        return FullComboTheme.Standard;
    }

    static bool HasThemeHint(string value, params string[] tokens)
    {
        if (string.IsNullOrEmpty(value)) return false;
        for (var i = 0; i < tokens.Length; i++)
            if (value.IndexOf(tokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    bool DrawDjMaxFullCombo(float age, float fade)
    {
        if (!MdLooks.TrySprite(_scene, "full_combo_djmax_text", out var text) || text == null
            || !MdLooks.TrySprite(_scene, "full_combo_djmax_bg", out var bg) || bg == null
            || !MdLooks.TrySprite(_scene, "full_combo_djmax_crown", out var crown) || crown == null
            || !MdLooks.TrySprite(_scene, "full_combo_djmax_star_l", out var starL) || starL == null
            || !MdLooks.TrySprite(_scene, "full_combo_djmax_star_r", out var starR) || starR == null)
            return false;

        var pop = FullComboPop(age);
        var scale = Mathf.Min(Screen.width / 960f, Screen.height / 540f) * pop;
        scale = Mathf.Min(scale, Screen.height * 0.72f / 424f);
        var alpha = fade * Mathf.Clamp01(age / 0.16f);
        var old = GUI.color;
        DrawFullComboSprite(starL, -110f, -20.8f, 220f, 424f, scale, alpha);
        DrawFullComboSprite(starR, 110f, -20.8f, 220f, 424f, scale, alpha);
        DrawFullComboSprite(bg, -17.45f, -13.9f, 348f, 338f, scale, alpha);
        DrawFullComboSprite(crown, 15.1f, 197.1f, 112f, 84f, scale, alpha);
        DrawFullComboSprite(text, -35.5f, 13.9f, 311f, 284f, scale, alpha);
        GUI.color = old;
        return true;
    }

    bool DrawArknightsFullCombo(float age, float fade)
    {
        if (!MdLooks.TrySprite(_scene, "full_combo_arknights_zh", out var text) || text == null
            || !MdLooks.TrySprite(_scene, "full_combo_arknights_mark", out var mark) || mark == null)
            return false;

        var pop = FullComboPop(age);
        var scale = Mathf.Min(Screen.width * 0.60f / 681f, Screen.height * 0.58f / 450f) * pop;
        var alpha = fade * Mathf.Clamp01(age / 0.16f);
        var old = GUI.color;
        DrawFullComboSprite(mark, 0f, 34f, 542.4f, 450f, scale, alpha);
        DrawFullComboSprite(text, 0f, 0f, 681f, 437f, scale, alpha);
        GUI.color = old;
        return true;
    }

    static float FullComboPop(float age)
    {
        var p = Mathf.Clamp01(age / 0.32f);
        return p < 0.68f
            ? Mathf.Lerp(0.15f, 1.08f, Mathf.SmoothStep(0f, 1f, p / 0.68f))
            : Mathf.Lerp(1.08f, 1f, Mathf.SmoothStep(0f, 1f, (p - 0.68f) / 0.32f));
    }

    static void DrawFullComboSprite(Texture2D tex, float anchoredX, float anchoredY,
        float width, float height, float scale, float alpha)
    {
        var w = width * scale;
        var h = height * scale;
        var cx = Screen.width * 0.5f + anchoredX * scale;
        var cy = Screen.height * FullComboCenterYRatio - anchoredY * scale;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), tex,
            ScaleMode.StretchToFill, true);
    }

    static float FullComboExtraGapAfter(int letterIndex)
    {
        if (letterIndex == 3) return FullComboWordGap; // FULL | COMBO
        if (letterIndex == 8) return FullComboBangGap; // O | !
        return 0f;
    }

    void SetHitTiming(Transform hitFx, string text, Judge.Grade grade)
    {
        if (hitFx == null) return;
        var label = FindNamed(hitFx, "timing");
        if (label == null)
        {
            var go = new GameObject("timing");
            label = go.transform;
            label.SetParent(hitFx, false);
            var tm = go.AddComponent<TextMesh>();
            tm.anchor = TextAnchor.MiddleLeft;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = 32;
            // EARLY/LATE 改用官机 Sprite；TextMesh 只保留较小的毫秒数。
            tm.characterSize = 0.043f;
            tm.fontStyle = FontStyle.Bold;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = -7;
                if (renderer.material != null) renderer.material.renderQueue = 3230;
            }
        }

        var sx = Mathf.Max(0.001f, Mathf.Abs(hitFx.localScale.x));
        var sy = Mathf.Max(0.001f, Mathf.Abs(hitFx.localScale.y));
        // 抵消判定贴图宽高比，让文字保持等比。官机节点：
        // Perfect 的 ImgEarly/ImgLate y=-0.47，Great 为 y=-0.40。
        label.localScale = new Vector3(1f / sx, 1f / sy, 1f);
        var officialY = grade == Judge.Grade.Great ? -0.40f : -0.47f;
        label.localPosition = new Vector3(0f, officialY / sy, -0.01f);
        label.localRotation = Quaternion.identity;
        var timing = label.GetComponent<TextMesh>();
        var early = !string.IsNullOrEmpty(text) && text.StartsWith("EARLY");
        var late = !string.IsNullOrEmpty(text) && text.StartsWith("LATE");
        var badge = FindNamed(label, "timingBadge");
        var badgeKey = early ? "judge_early" : late ? "judge_late" : "";
        if (badge == null && badgeKey.Length > 0
            && MdLooks.TrySprite(_scene, badgeKey, out var badgeTex) && badgeTex != null)
        {
            var badgeBox = badgeTex.width / MdLooks.HitGradePpu * worldScale;
            badge = SpriteQuad(label, badgeTex, badgeBox, "timingBadge");
            PaintLayer(badge, 3230, -7);
            _hitFxVisuals.Remove(hitFx); // pooled hitFx 必须重新缓存新材质，才能一起淡出。
        }
        else if (badge != null && badgeKey.Length > 0
            && MdLooks.TrySprite(_scene, badgeKey, out var nextBadge) && nextBadge != null)
        {
            var renderer = badge.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                MdMat.SetTex(renderer.sharedMaterial, nextBadge);
                renderer.sharedMaterial.color = Color.white;
                if (renderer.sharedMaterial.HasProperty("_Color"))
                    renderer.sharedMaterial.SetColor("_Color", Color.white);
            }
            badge.localScale = FitBox(nextBadge.width / MdLooks.HitGradePpu * worldScale,
                nextBadge.height > 0 ? (float)nextBadge.width / nextBadge.height : 1f);
        }
        if (badge != null)
        {
            badge.gameObject.SetActive(badgeKey.Length > 0);
            // 方向 Sprite 在左、毫秒在右；两者组合后仍以判定字中心对齐。
            badge.localPosition = new Vector3(early ? -0.56f : -0.47f, 0f, -0.01f);
            badge.localRotation = Quaternion.identity;
        }

        var split = string.IsNullOrEmpty(text) ? -1 : text.LastIndexOf("  ");
        var hasBadge = badge != null && badge.gameObject.activeSelf && badgeKey.Length > 0;
        timing.anchor = hasBadge ? TextAnchor.MiddleLeft : TextAnchor.UpperCenter;
        timing.alignment = hasBadge ? TextAlignment.Left : TextAlignment.Center;
        timing.text = hasBadge && split >= 0 ? text.Substring(split + 2) : text ?? "";
        if (hasBadge)
            label.localPosition = new Vector3(0.30f / sx, officialY / sy, -0.01f);
        timing.color = early
            ? new Color(0.45f, 0.92f, 1f)
            : late ? new Color(1f, 0.48f, 0.76f) : Color.white;
        var timingRenderer = label.GetComponent<Renderer>();
        if (timingRenderer != null && timingRenderer.material != null)
        {
            var color = timing.color;
            timingRenderer.material.color = color;
            if (timingRenderer.material.HasProperty("_Color"))
                timingRenderer.material.SetColor("_Color", color);
        }
    }

    void DrawResultPanel()
    {
        if (_session == null) return;
        EnsureResultStyles();
        var oldColor = GUI.color;
        var oldBg = GUI.backgroundColor;
        GUI.color = new Color(0.035f, 0.015f, 0.075f, 0.90f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var w = Mathf.Min(720f, Screen.width - 36f);
        var h = Mathf.Min(520f, Screen.height - 36f);
        var x = (Screen.width - w) * 0.5f;
        var y = (Screen.height - h) * 0.5f;
        GUI.backgroundColor = new Color(0.24f, 0.08f, 0.35f, 0.98f);
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);

        GUI.Label(new Rect(x + 24f, y + 20f, w - 48f, 48f), "RESULT", _resultTitleStyle);
        GUI.Label(new Rect(x + 24f, y + 70f, w - 48f, 28f),
            _session.Song.Document.Meta.Name + "  ·  map" + _session.Difficulty,
            _resultSmallStyle);
        GUI.Label(new Rect(x + 24f, y + 112f, w - 48f, 82f),
            _session.Score.ToString("000000"), _resultScoreStyle);
        GUI.Label(new Rect(x + 24f, y + 188f, w - 48f, 28f),
            "/ " + PlaySession.FullScore.ToString("000000") + "    ACCURACY "
            + _session.Accuracy.ToString("0.00") + "%", _resultSmallStyle);

        var badge = _session.Score == PlaySession.FullScore
            ? "ALL PERFECT"
            : _session.Misses == 0 ? "FULL COMBO" : "CLEAR";
        GUI.Label(new Rect(x + 24f, y + 228f, w - 48f, 38f), badge, _resultTitleStyle);
        GUI.Label(new Rect(x + 70f, y + 282f, w - 140f, 36f),
            "PERFECT  " + _session.Perfects + "      GREAT  " + _session.Greats
            + "      MISS  " + _session.Misses, _resultBodyStyle);
        GUI.Label(new Rect(x + 70f, y + 324f, w - 140f, 36f),
            "MAX COMBO  " + _session.MaxCombo + "      SCORE UNITS  "
            + _session.JudgedScoreUnits + " / " + _session.TotalScoreUnits,
            _resultBodyStyle);

        const float buttonW = 132f;
        const float buttonH = 42f;
        const float gap = 18f;
        var buttonsX = x + (w - buttonW * 3f - gap * 2f) * 0.5f;
        var buttonsY = y + h - 70f;
        GUI.backgroundColor = new Color(0.55f, 0.62f, 0.95f);
        if (GuiClick(new Rect(buttonsX, buttonsY, buttonW, buttonH), "上一首  F8"))
            LeaveResultForSong(-1);
        GUI.backgroundColor = new Color(1f, 0.38f, 0.72f);
        if (GuiClick(new Rect(buttonsX + buttonW + gap, buttonsY, buttonW, buttonH), "重试  R"))
            RestartFromResult();
        GUI.backgroundColor = new Color(0.55f, 0.62f, 0.95f);
        if (GuiClick(new Rect(buttonsX + (buttonW + gap) * 2f, buttonsY, buttonW, buttonH), "下一首  F4"))
            LeaveResultForSong(1);

        GUI.color = oldColor;
        GUI.backgroundColor = oldBg;
    }

    void RestartFromResult(bool? useAutoplay = null)
    {
        if (useAutoplay.HasValue) autoplay = useAutoplay.Value;
        _resultOpen = false;
        _resultSequenceStarted = false;
        _fullComboPlaying = false;
        _paused = false;
        SwitchMap(_session != null ? _session.Difficulty : difficulty);
    }

    void LeaveResultForSong(int delta)
    {
        _resultOpen = false;
        _resultSequenceStarted = false;
        _fullComboPlaying = false;
        CycleSong(delta);
    }

    void EnsureResultStyles()
    {
        if (_resultTitleStyle != null) return;
        var centered = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        _resultTitleStyle = new GUIStyle(centered) { fontSize = 32 };
        _resultTitleStyle.normal.textColor = new Color(1f, 0.78f, 0.22f);
        _resultScoreStyle = new GUIStyle(centered) { fontSize = 64 };
        _resultScoreStyle.normal.textColor = Color.white;
        _resultBodyStyle = new GUIStyle(centered) { fontSize = 22 };
        _resultBodyStyle.normal.textColor = new Color(0.94f, 0.90f, 1f);
        _resultSmallStyle = new GUIStyle(centered) { fontSize = 17 };
        _resultSmallStyle.normal.textColor = new Color(0.82f, 0.75f, 0.90f);
    }
}
