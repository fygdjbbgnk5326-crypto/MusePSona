using MusePSona.Play;
using UnityEngine;

/// <summary>
/// 连打 Hits 计数与相机。官机 MultiHitCombo 在 (-3.85, 1.55)，HitsBase 黄爆 + 数字；
/// MulteHitShake 把相机 x 收到 -1.08、正交 size 收到 3.15。
/// 镜头只跟连打；近战 close_atk、突击者飞近、Boss 出场都不拉。事件结束弹回 3.75。
/// </summary>
public sealed partial class PlayDirector
{
    const float HitsInDur = 0.15f;
    const float HitsIdleDur = 1f;
    const float HitsOutDur = 0.25f;
    const float CamPullX = -1.08f;
    const float CamPullSize = 3.15f;
    const float CamShakeX = -1.25f;
    const float CamShakeSize = 3.05f;
    const float CamShakePeak = 0.01667f;
    const float CamShakeLoop = 0.08333f;
    /// <summary>HitsBase 官机 Sprite m_Rect 156×143 / PPU100。</summary>
    const float HitsBurstW = 1.56f;
    const float HitsDialogEnd = 0.16667f;
    /// <summary>官机 TxtMultiHitCombo sizeDelta.y 0.6，略加大。</summary>
    const float HitsDigitH = 0.88f;

    void TickHits(float chartNow)
    {
        LiveMash(chartNow, out var count, out var hitting);
        if (count <= 0 && !hitting)
        {
            if (_hitsRoot == null || !_hitsRoot.gameObject.activeSelf)
            {
                _hitsShown = 0;
                _hitsLeaving = false;
                return;
            }
            if (!_hitsLeaving)
            {
                _hitsLeaving = true;
                _hitsPopAt = Time.unscaledTime;
            }
            var leave = Time.unscaledTime - _hitsPopAt;
            PoseHitsFx(leave, true);
            if (leave >= HitsOutDur)
            {
                _hitsRoot.gameObject.SetActive(false);
                _hitsLeaving = false;
                _hitsShown = 0;
            }
            return;
        }

        if (!EnsureHitsHud()) return;
        _hitsRoot.gameObject.SetActive(true);
        _hitsLeaving = false;
        if (count != _hitsShown && count > 0)
        {
            var first = _hitsShown <= 0;
            _hitsShown = count;
            PlaceHitsDigits(count);
            // 官机 CountAnimator：进场只播一次 CounterIn，之后 Standby；换数字不重播 In。
            if (first)
            {
                _hitsPopAt = Time.unscaledTime;
                _hitsBurstAt = Time.unscaledTime;
            }
        }
        AlignHitsHud();
        PoseHitsFx(Time.unscaledTime - _hitsBurstAt, false);
    }

    void LiveMash(float chartNow, out int count, out bool hitting)
    {
        if (_liveMashFrame == Time.frameCount)
        {
            count = _liveMashCount;
            hitting = _liveMashHitting;
            return;
        }
        _liveMashFrame = Time.frameCount;
        count = 0;
        hitting = false;
        if (_session != null)
        {
            foreach (var n in _session.MasherNotes)
            {
                if (chartNow < (float)n.TimeSec) break;
                var g = _session.GradeOf(n.Id);
                if (g is not (Judge.Grade.Perfect or Judge.Grade.Great)) continue;
                var hits = _session.MashHits(n.Id);
                if (hits <= 0) continue;
                if (chartNow <= (float)n.EndSec + 0.02f)
                {
                    hitting = true;
                    count = hits;
                }
            }
        }
        _liveMashCount = count;
        _liveMashHitting = hitting;
    }

    bool EnsureHitsHud()
    {
        if (_hitsRoot != null && _hitsBurst != null && _hitsNum != null) return true;
        if (!MdLooks.TrySprite(_scene, "hits_base", out var burst) || burst == null)
            return false;
        if (_hitsRoot == null)
        {
            var go = new GameObject("HitsHud");
            go.transform.SetParent(transform, false);
            _hitsRoot = go.transform;
            var num = new GameObject("num");
            num.transform.SetParent(_hitsRoot, false);
            _hitsNum = num.transform;
        }
        if (_hitsBurst == null)
        {
            _hitsBurst = SpriteQuad(_hitsRoot, burst, HitsBurstW * worldScale, "base");
            PaintLayer(_hitsBurst, 3500, 100);
            _hitsBurstFit = FitHitsBurst();
            _hitsBurst.localScale = _hitsBurstFit;
            TintOne(_hitsBurst, 1f);
        }
        _hitsBurst.localPosition = new Vector3(0f, 0f, 0.02f);
        _hitsNum.localPosition = new Vector3(0f, 0.22f, -0.04f);
        AlignHitsHud();
        return true;
    }

    void AlignHitsHud()
    {
        if (_hitsRoot == null) return;
        _hitsRoot.position = new Vector3(Flight.HitX * worldScale, 1.55f * worldScale, -0.38f);
        _hitsRoot.localScale = Vector3.one;
    }

    /// <summary>数字跟 HITS 爆点同一套 MultiHitDialog 缩放。</summary>
    void PoseHitsFx(float elapsed, bool leaving)
    {
        float sx, sy;
        if (leaving)
        {
            var t = Mathf.Clamp(elapsed, 0f, HitsDialogEnd);
            if (t < 0.0833f)
            {
                var u = t / 0.0833f;
                sx = sy = Mathf.Lerp(1f, 1.8f, u);
            }
            else
            {
                var u = (t - 0.0833f) / 0.0833f;
                sx = sy = Mathf.Lerp(1.8f, 0f, u);
            }
        }
        else if (elapsed < HitsInDur)
        {
            if (elapsed < 0.0333f)
            {
                var u = elapsed / 0.0333f;
                sx = Mathf.Lerp(1f, 0.5f, u);
                sy = Mathf.Lerp(0f, 1.5f, u);
            }
            else if (elapsed < 0.0833f)
            {
                var u = (elapsed - 0.0333f) / 0.05f;
                sx = Mathf.Lerp(0.5f, 1.1f, u);
                sy = Mathf.Lerp(1.5f, 0.9f, u);
            }
            else
            {
                var u = (elapsed - 0.0833f) / 0.0667f;
                sx = Mathf.Lerp(1.1f, 1f, u);
                sy = Mathf.Lerp(0.9f, 1f, u);
            }
        }
        else
        {
            var u = (elapsed - HitsInDur) % HitsIdleDur;
            if (u < 0.0333f)
            {
                var k = u / 0.0333f;
                sx = Mathf.Lerp(1f, 0.8f, k);
                sy = Mathf.Lerp(1f, 1.2f, k);
            }
            else if (u < 0.0833f)
            {
                var k = (u - 0.0333f) / 0.05f;
                sx = Mathf.Lerp(0.8f, 1.1f, k);
                sy = Mathf.Lerp(1.2f, 0.9f, k);
            }
            else if (u < 0.15f)
            {
                var k = (u - 0.0833f) / 0.0667f;
                sx = Mathf.Lerp(1.1f, 1f, k);
                sy = Mathf.Lerp(0.9f, 1f, k);
            }
            else
                sx = sy = 1f;
        }
        if (_hitsBurst != null)
            _hitsBurst.localScale = Vector3.Scale(_hitsBurstFit, new Vector3(sx, sy, 1f));
        if (_hitsNum != null)
        {
            float nx = sx, ny = sy;
            if (!leaving && elapsed < HitsInDur)
            {
                // 爆点 DialogStart 从 sy=0 拉起，数字跟会糊成一团；进场用 CounterIn。
                if (elapsed < 0.05f)
                {
                    nx = 0.9f;
                    ny = Mathf.Lerp(0.9f, 1.1f, elapsed / 0.05f);
                }
                else if (elapsed < 0.10f)
                {
                    var u = (elapsed - 0.05f) / 0.05f;
                    nx = Mathf.Lerp(0.9f, 1.1f, u);
                    ny = Mathf.Lerp(1.1f, 0.9f, u);
                }
                else
                {
                    var u = (elapsed - 0.10f) / 0.05f;
                    nx = Mathf.Lerp(1.1f, 1f, u);
                    ny = Mathf.Lerp(0.9f, 1f, u);
                }
            }
            _hitsNum.localScale = new Vector3(nx, ny, 1f);
        }
    }

    Vector3 FitHitsBurst()
    {
        if (_hitsBurst == null) return Vector3.one;
        var r = _hitsBurst.GetComponent<Renderer>();
        var tex = r != null ? MdMat.Tex(r.material) : null;
        var aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 156f / 143f;
        return FitBox(HitsBurstW * worldScale, aspect);
    }

    void TintHits(float a)
    {
        if (_hitsNum == null) return;
        var c = new Color(1f, 1f, 1f, a);
        _rendererScratch.Clear();
        _hitsNum.GetComponentsInChildren(true, _rendererScratch);
        for (var i = 0; i < _rendererScratch.Count; i++)
            TintRenderer(_rendererScratch[i], c);
    }

    void TintOne(Transform t, float a)
    {
        if (t == null) return;
        var c = new Color(1f, 1f, 1f, a);
        _rendererScratch.Clear();
        t.GetComponentsInChildren(true, _rendererScratch);
        for (var i = 0; i < _rendererScratch.Count; i++)
            TintRenderer(_rendererScratch[i], c);
    }

    static void TintRenderer(Renderer r, Color c)
    {
        if (r == null || r.material == null) return;
        r.material.color = c;
        if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
    }

    void PlaceHitsDigits(int hits)
    {
        if (_hitsNum == null) return;
        var s = hits.ToString();
        if (_hitsDigitSteps.Length < s.Length)
        {
            var capacity = Mathf.Max(8, s.Length);
            _hitsDigitSteps = new float[capacity];
            _hitsDigitTextures = new Texture2D[capacity];
            _hitsDigitHeights = new float[capacity];
        }
        var total = 0f;
        for (var i = 0; i < s.Length; i++)
        {
            Texture2D tex = null;
            if (!MdLooks.TrySprite(_scene, "hits_digit_" + s[i], out tex) || tex == null)
                MdLooks.TrySprite(_scene, "combo_digit_high_" + s[i], out tex);
            if (tex == null) return;
            _hitsDigitTextures[i] = tex;
            _hitsDigitHeights[i] = HitsDigitH * worldScale;
            var aspect = tex.height > 0 ? (float)tex.width / tex.height : 1f;
            _hitsDigitSteps[i] = FitBox(_hitsDigitHeights[i], aspect).x * 0.78f;
            total += _hitsDigitSteps[i];
        }
        var x = -total * 0.5f;
        for (var i = 0; i < s.Length; i++)
        {
            Transform t;
            if (i < _hitsDigitViews.Count)
            {
                t = _hitsDigitViews[i];
                t.gameObject.SetActive(true);
                var r = t.GetComponent<Renderer>();
                if (r != null && r.sharedMaterial != null)
                    MdMat.SetTex(r.sharedMaterial, _hitsDigitTextures[i]);
                var aspect = _hitsDigitTextures[i].height > 0
                    ? (float)_hitsDigitTextures[i].width / _hitsDigitTextures[i].height : 1f;
                t.localScale = FitBox(_hitsDigitHeights[i], aspect);
                t.name = "d" + s[i];
            }
            else
            {
                t = SpriteQuad(_hitsNum, _hitsDigitTextures[i], _hitsDigitHeights[i], "d" + s[i]);
                PaintLayer(t, 3510, 110);
                _hitsDigitViews.Add(t);
            }
            t.localPosition = new Vector3(x + _hitsDigitSteps[i] * 0.5f, 0f, 0f);
            x += _hitsDigitSteps[i];
        }
        for (var i = s.Length; i < _hitsDigitViews.Count; i++)
            _hitsDigitViews[i].gameObject.SetActive(false);
        TintHits(1f);
    }

    void TickCamera(float chartNow)
    {
        LiveMash(chartNow, out _, out var mash);
        var pull = mash;
        var targetX = pull ? CamPullX : 0f;
        var targetSize = pull ? CamPullSize : Flight.CameraSize;
        if (mash)
        {
            var u = Time.unscaledTime % CamShakeLoop;
            if (u < CamShakePeak)
            {
                var k = u / CamShakePeak;
                targetX = Mathf.Lerp(CamPullX, CamShakeX, k);
                targetSize = Mathf.Lerp(CamPullSize, CamShakeSize, k);
            }
            else
            {
                var k = (u - CamShakePeak) / (CamShakeLoop - CamShakePeak);
                targetX = Mathf.Lerp(CamShakeX, CamPullX, k);
                targetSize = Mathf.Lerp(CamShakeSize, CamPullSize, k);
            }
        }
        var dt = Time.unscaledDeltaTime;
        var fade = dt / CamShakeLoop;
        _camX = Mathf.MoveTowards(_camX, targetX, Mathf.Abs(CamPullX) * fade);
        _camSize = Mathf.MoveTowards(_camSize, targetSize, (Flight.CameraSize - CamPullSize) * fade);
        var cam = _mainCamera;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = _camSize;
        var p = cam.transform.position;
        cam.transform.position = new Vector3(_camX, p.y, p.z);
    }
}
