using MusePSona.Play;
using UnityEngine;

/// <summary>
/// 滤镜走相机后处理。白集中线按官机成片：收在画面正中，中间留空，线从四边进来。
/// 闪光加在滤镜之后。2A 是画面波纹，不闪判定圈。
/// </summary>
public sealed partial class PlayDirector
{
    Material _fxPostMat;
    MdScreenPost _fxPost;
    bool _fxPostMissing;
    float _vigShown;
    float _flashShown;
    float _speedShown;
    float _sepiaShown;
    float _tvShown;
    float _chromaShown;
    float _pixelShown;
    float _filmShown;
    float _twistShown;

    void ResetScreenFxState()
    {
        _vigShown = _flashShown = _speedShown = _sepiaShown = _tvShown = 0f;
        _chromaShown = _pixelShown = _filmShown = _twistShown = 0f;
        if (_fxPostMat == null) return;
        _fxPostMat.SetFloat("_Vignette", 0f);
        _fxPostMat.SetFloat("_Gray", 0f);
        _fxPostMat.SetFloat("_Tv", 0f);
        _fxPostMat.SetFloat("_Chroma", 0f);
        _fxPostMat.SetFloat("_Pixel", 0f);
        _fxPostMat.SetFloat("_Film", 0f);
        _fxPostMat.SetFloat("_Twist", 0f);
        _fxPostMat.SetFloat("_SpeedAmt", 0f);
        _fxPostMat.SetFloat("_TwistTime", 0f);
        _fxPostMat.SetColor("_Flash", new Color(1f, 1f, 1f, 0f));
    }

    void EnsureScreenFx()
    {
        if (_fxPostMat != null && _fxPost != null) return;
        var sh = Shader.Find("MusePSona/MdScreenPost");
        if (sh == null)
        {
            if (!_fxPostMissing)
                Debug.LogWarning("找不到 MusePSona/MdScreenPost，画面滤镜不会作用到成片上。");
            _fxPostMissing = true;
            return;
        }
        if (_fxPostMat == null)
        {
            _fxPostMat = new Material(sh);
            _fxPostMat.SetVector("_LineCenter", new Vector4(0.5f, 0.5f, 0f, 0f));
            _fxPostMat.SetFloat("_LineCount", 12f);
            _fxPostMat.SetFloat("_LineDensity", 0.7f);
            _fxPostMat.SetFloat("_LineFrames", 15f);
            _fxPostMat.SetFloat("_LineSpeed", 15f);
            _fxPostMat.SetFloat("_LineMask", -0.12f);
            _fxPostMat.SetFloat("_LineFalloff", 0.16f);
            _fxPostMat.SetFloat("_SinCycle", 70f);
            _fxPostMat.SetFloat("_SinScale", 0.18f);
            _fxPostMat.SetFloat("_TwistStrength", 10f);
        }
        var cam = Camera.main;
        if (cam == null) return;
        _fxPost = cam.GetComponent<MdScreenPost>();
        if (_fxPost == null) _fxPost = cam.gameObject.AddComponent<MdScreenPost>();
        _fxPost.Mat = _fxPostMat;
    }

    void TickFx(float now)
    {
        if (_session == null) return;
        EnsureScreenFx();
        if (_fxPostMat == null) return;
        var st = _session.FxAt(now);
        var dt = Time.unscaledDeltaTime;
        // 官机的 0.2 秒 transition 是逐帧 Lerp（指数尾巴），不是定速 MoveTowards。
        var slowK = 1f - Mathf.Exp(-dt / 0.2f);
        _vigShown = Mathf.Lerp(_vigShown, st.Vignette ? 1f : 0f, slowK);
        _sepiaShown = Mathf.Lerp(_sepiaShown, st.Sepia ? 1f : 0f, slowK);
        _chromaShown = Mathf.Lerp(_chromaShown, st.Chroma ? 1f : 0f, slowK);
        if (!st.Chroma && _chromaShown < 0.02f) _chromaShown = 0f;

        // 这些效果在官机里由布尔开关直接切换；参数会保留，但关闭后不再参与着色。
        _speedShown = st.SpeedLine != 0 ? 1f : 0f;
        _flashShown = st.Flash;
        _tvShown = st.Tv ? 1f : 0f;
        _pixelShown = st.Pixel ? 1f : 0f;
        _filmShown = st.Film ? 1f : 0f;
        _twistShown = st.Ripple ? 1f : 0f;

        var line = st.SpeedLine == 2
            ? new Color(1f, 1f, 1f, 1f)
            : new Color(0.02f, 0.02f, 0.04f, 1f);
        _fxPostMat.SetColor("_SpeedColor", line);
        _fxPostMat.SetFloat("_SpeedAmt", _speedShown);
        _fxPostMat.SetFloat("_Vignette", _vigShown);
        _fxPostMat.SetFloat("_Gray", _sepiaShown);
        _fxPostMat.SetFloat("_Tv", _tvShown);
        _fxPostMat.SetFloat("_Chroma", _chromaShown);
        _fxPostMat.SetFloat("_Pixel", _pixelShown);
        _fxPostMat.SetFloat("_Film", _filmShown);
        _fxPostMat.SetFloat("_Twist", _twistShown);
        _fxPostMat.SetFloat("_TwistTime", Mathf.Max(0f, now - _session.DelaySec) * 10f);

        var rgb = st.FlashRgb;
        var flash = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, _flashShown);
        _fxPostMat.SetColor("_Flash", flash);
    }
}
