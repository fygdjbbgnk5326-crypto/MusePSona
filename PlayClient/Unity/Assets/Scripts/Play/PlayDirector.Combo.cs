using System.Collections.Generic;
using System.IO;
using System.Linq;
using MusePSona.Play;
using UnityEngine;

/// <summary>连击 HUD（含 DJMAX 字模）。</summary>
public sealed partial class PlayDirector
{
    void TickCombo()
    {
        if (_session == null) return;
        var combo = _session.Combo;
        // 官机 CountAnimator rangeMin=5：5 以下不显示连击 HUD。
        if (combo < 5)
        {
            TickComboBreak();
            return;
        }
        _comboBreak = false;
        var band = combo >= 50 ? 2 : combo >= 20 ? 1 : 0;
        var was = _comboShown;
        if (!EnsureComboHud(band)) return;
        _comboRoot.gameObject.SetActive(true);
        if (combo != _comboShown)
        {
            _comboShown = combo;
            PlaceComboDigits(combo);
            // 只在首次出现、以及 50 连击变色进 High 时重播 start / ComboCounterIn。
            if (was < 5 || (was < 50 && combo >= 50))
                _comboPopAt = Time.unscaledTime;
        }
        PoseComboNow();
        AlignComboHud();
        PoseComboNum(Time.unscaledTime - _comboPopAt, false);
    }

    void TickComboBreak()
    {
        if (_comboRoot == null || !_comboRoot.gameObject.activeSelf)
        {
            _comboShown = 0;
            return;
        }
        if (!_comboBreak)
        {
            _comboBreak = true;
            _comboPopAt = Time.unscaledTime;
            _comboShown = 0;
        }
        var data = _comboSpine != null ? _comboSpine.Skeleton.Data : null;
        var end = MdSpine.PickComboEnd(data);
        var elapsed = Time.unscaledTime - _comboPopAt;
        // 默认 ComboCounterOut=0.4s；DJMAX 的 Out 片段时长 0，靠 Animator 过渡约 0.25s 缩到 0。
        var dur = 0.40f;
        if (!string.IsNullOrEmpty(end) && data != null)
        {
            var anim = data.FindAnimation(end);
            if (anim != null) dur = Mathf.Max(dur, anim.Duration);
            _comboSpine.PoseTime(end, elapsed, false);
        }
        AlignComboHud();
        PoseComboNum(elapsed, true);
        if (elapsed < dur) return;
        _comboRoot.gameObject.SetActive(false);
        _comboBreak = false;
    }

    static bool IsDjMaxCombo(string scene) =>
        SceneTimeline.Norm(scene) == "scene_09";

    bool EnsureComboHud(int band)
    {
        var scene = string.IsNullOrEmpty(_scene) ? "scene_01" : _scene;
        var dj = IsDjMaxCombo(scene);
        if (_comboRoot != null && _comboDigits == band && _comboScene == scene && _comboDjMax == dj)
        {
            if (dj ? _comboWord != null : _comboSpine != null)
                return true;
        }
        if (dj)
        {
            var wkey = band <= 0 ? "combo_djmax_word_low" : band == 1 ? "combo_djmax_word_mid" : "combo_djmax_word_high";
            if (!MdLooks.TrySprite(scene, wkey, out var word) || word == null)
                dj = false;
            else
            {
                if (_comboRoot == null)
                {
                    var go = new GameObject("ComboHud");
                    go.transform.SetParent(transform, false);
                    _comboRoot = go.transform;
                    var num = new GameObject("num");
                    num.transform.SetParent(_comboRoot, false);
                    _comboNumRoot = num.transform;
                }
                if (_comboSpine != null)
                {
                    Destroy(_comboSpine.gameObject);
                    _comboSpine = null;
                }
                var px = (2f * Flight.CameraSize) / 1080f * worldScale;
                _comboRoot.position = new Vector3(0f, 25f * px, -0.42f);
                _comboRoot.localScale = new Vector3(100f * px, 100f * px, 1f);
                var aspect = word.height > 0 ? (float)word.width / word.height : 1f;
                var box = word.width / 100f;
                if (_comboWord == null)
                {
                    _comboWord = SpriteQuad(_comboRoot, word, box, "word");
                    PaintLayer(_comboWord, 3400, 80);
                }
                else
                {
                    if (_comboWord.parent != _comboRoot)
                        _comboWord.SetParent(_comboRoot, false);
                    _comboWord.localScale = FitBox(box, aspect);
                    var r = _comboWord.GetComponent<Renderer>();
                    if (r != null && r.material != null)
                    {
                        MdMat.SetTex(r.material, word);
                    }
                }
                _comboWord.localPosition = new Vector3(0f, 4.25f, 0.04f);
                _comboWord.localScale = FitBox(box, aspect);
                _comboDigits = band;
                _comboScene = scene;
                _comboDjMax = true;
                return true;
            }
        }
        var key = band <= 0 ? "fx_combo_0" : band == 1 ? "fx_combo_1" : "fx_combo_2";
        var pack = MdSpine.LoadNamed(scene, key);
        if (pack == null) return false;
        if (_comboRoot == null)
        {
            var go = new GameObject("ComboHud");
            go.transform.SetParent(transform, false);
            _comboRoot = go.transform;
            var num = new GameObject("num");
            num.transform.SetParent(_comboRoot, false);
            _comboNumRoot = num.transform;
        }
        // 官机：Combo 在 1080 画布中心偏上 25px，ComboLow 缩放 100（SkeletonData scale 0.005）。
        var px2 = (2f * Flight.CameraSize) / 1080f * worldScale;
        _comboRoot.position = new Vector3(0f, 25f * px2, -0.42f);
        _comboRoot.localScale = new Vector3(100f * px2, 100f * px2, 1f);
        if (_comboWord != null)
        {
            Destroy(_comboWord.gameObject);
            _comboWord = null;
        }
        if (_comboSpine != null && (_comboDigits != band || _comboDjMax))
        {
            Destroy(_comboSpine.gameObject);
            _comboSpine = null;
        }
        if (_comboSpine == null)
        {
            _comboSpine = MdSpineView.Attach(_comboRoot, pack);
            _comboSpine.SetLayer(3400, 80);
        }
        _comboDigits = band;
        _comboScene = scene;
        _comboDjMax = false;
        return true;
    }

    void PoseComboNow()
    {
        if (_comboSpine == null) return;
        var data = _comboSpine.Skeleton.Data;
        var elapsed = Time.unscaledTime - _comboPopAt;
        var start = MdSpine.PickComboClip(data);
        var idle = MdSpine.PickComboIdle(data);
        var startDur = 0f;
        if (!string.IsNullOrEmpty(start) && data.FindAnimation(start) != null)
            startDur = data.FindAnimation(start).Duration;
        if (!string.IsNullOrEmpty(start) && elapsed < startDur)
            _comboSpine.PoseTime(start, elapsed, false);
        else if (!string.IsNullOrEmpty(idle))
            _comboSpine.PoseLoop(idle, elapsed - startDur);
        else if (!string.IsNullOrEmpty(start))
            _comboSpine.PoseTime(start, startDur, false);
    }

    void AlignComboHud()
    {
        var px = (2f * Flight.CameraSize) / 1080f * worldScale;
        if (_comboRoot != null)
        {
            _comboRoot.position = new Vector3(0f, 25f * px, -0.42f);
            _comboRoot.localScale = new Vector3(100f * px, 100f * px, 1f);
        }
        if (_comboSpine != null)
            _comboSpine.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        if (_comboNumRoot == null) return;
        if (_comboDjMax) return;
        // 官机 TxtCounter：默认 Combo anchored (0, 376)。DJMAX 由 PoseComboNum 按片段位移。
        _comboNumRoot.localPosition = new Vector3(0f, 3.85f, -0.05f);
    }

    static AnimationCurve ComboKeys(params float[] tv)
    {
        var c = new AnimationCurve();
        for (var i = 0; i + 1 < tv.Length; i += 2)
            c.AddKey(new Keyframe(tv[i], tv[i + 1], 0f, 0f));
        return c;
    }

    static void EnsureComboCurves()
    {
        if (_comboIn != null) return;
        // ComboCounterIn / Standby / Out，attribute=scale。
        _comboIn = ComboKeys(0f, 0.7f, 0.05f, 1.3f, 0.11667f, 0.9f, 0.18333f, 1f);
        _comboIdleX = ComboKeys(0f, 1f, 0.5f, 1f, 0.53333f, 0.85f, 0.63333f, 1.1f, 0.7f, 1f, 1f, 1f);
        _comboIdleY = ComboKeys(0f, 1f, 0.5f, 1f, 0.53333f, 1.15f, 0.63333f, 0.9f, 0.7f, 1f, 1f, 1f);
        _comboOut = ComboKeys(0f, 1f, 0.23333f, 1f, 0.4f, 0f);
    }

    static float ComboHermite(float p0, float m0, float p1, float m1, float dt, float u)
    {
        u = Mathf.Clamp01(u);
        var u2 = u * u;
        var u3 = u2 * u;
        var h00 = 2f * u3 - 3f * u2 + 1f;
        var h10 = u3 - 2f * u2 + u;
        var h01 = -2f * u3 + 3f * u2;
        var h11 = u3 - u2;
        return h00 * p0 + h10 * m0 * dt + h01 * p1 + h11 * m1 * dt;
    }

    void PoseComboNum(float elapsed, bool breaking)
    {
        if (_comboNumRoot == null) return;
        if (_comboDjMax)
        {
            PoseDjmaxCombo(elapsed, breaking);
            return;
        }
        EnsureComboCurves();
        float sx, sy;
        if (breaking)
        {
            var t = Mathf.Clamp(elapsed, 0f, 0.4f);
            sx = sy = _comboOut.Evaluate(t);
        }
        else if (elapsed < 0.18333f)
            sx = sy = _comboIn.Evaluate(elapsed);
        else
        {
            var u = (elapsed - 0.18333f) % 1f;
            if (u < 0f) u += 1f;
            sx = _comboIdleX.Evaluate(u);
            sy = _comboIdleY.Evaluate(u);
        }
        _comboNumRoot.localScale = new Vector3(sx, sy, 1f);
    }

    void PoseDjmaxCombo(float elapsed, bool breaking)
    {
        // 位移走 ComboCounterInDjmax；缩放跟默认 ComboCounterIn/Standby/Out，否则数字看起来是死的。
        const float restNum = 3.35f;
        var numY = restNum;
        if (!breaking && elapsed < 0.25f)
        {
            var u = elapsed / 0.25f;
            numY = ComboHermite(2.95f, 19.20f, restNum, 0f, 0.25f, u);
        }
        _comboNumRoot.localPosition = new Vector3(0f, numY, -0.05f);
        if (_comboWord != null)
            _comboWord.localPosition = new Vector3(0f, 4.25f, 0.04f);
        EnsureComboCurves();
        float sx, sy;
        if (breaking)
        {
            var t = Mathf.Clamp(elapsed, 0f, 0.4f);
            sx = sy = _comboOut.Evaluate(t);
        }
        else if (elapsed < 0.18333f)
            sx = sy = _comboIn.Evaluate(elapsed);
        else
        {
            var u = (elapsed - 0.18333f) % 1f;
            if (u < 0f) u += 1f;
            sx = _comboIdleX.Evaluate(u);
            sy = _comboIdleY.Evaluate(u);
        }
        _comboNumRoot.localScale = new Vector3(sx, sy, 1f);
    }

    void PlaceComboDigits(int combo)
    {
        if (_comboNumRoot == null) return;
        for (var i = _comboNumRoot.childCount - 1; i >= 0; i--)
        {
            var child = _comboNumRoot.GetChild(i);
            if (_comboWord != null && child == _comboWord) continue;
            Destroy(child.gameObject);
        }
        var s = combo.ToString();
        var band = combo >= 50 ? "high" : combo >= 20 ? "mid" : "low";
        var em = _comboDjMax ? 0.88f : (combo >= 50 ? 1.25f / 1.10f : 1f) * 0.90f;
        var steps = new float[s.Length];
        var texs = new Texture2D[s.Length];
        var hs = new float[s.Length];
        var total = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            Texture2D tex = null;
            if (_comboDjMax)
                MdLooks.TrySprite(_scene, "combo_digit_djmax_" + band + "_" + s[i], out tex);
            if (tex == null)
            {
                var key = "combo_digit_" + band + "_" + s[i];
                if (!MdLooks.TrySprite(_scene, key, out tex) || tex == null)
                {
                    if (!MdLooks.TrySprite(_scene, "combo_digit_" + s[i], out tex) || tex == null)
                        return;
                }
            }
            texs[i] = tex;
            hs[i] = tex.height / 100f * em;
            var aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            steps[i] = FitBox(hs[i], aspect).x;
            total += steps[i];
        }
        var gap = _comboDjMax && s.Length > 1 ? 0.1f : 0f;
        total += gap * (s.Length - 1);
        var x = -total * 0.5f;
        for (var i = 0; i < s.Length; i++)
        {
            var t = SpriteQuad(_comboNumRoot, texs[i], hs[i], "d" + s[i]);
            t.localPosition = new Vector3(x + steps[i] * 0.5f, 0f, 0f);
            PaintLayer(t, 3410, 90);
            x += steps[i] + gap;
        }
    }
}
