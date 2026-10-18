Shader "MusePSona/MdScreenPost"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _Pixel ("Pixel", Float) = 0
        _Gray ("Gray", Float) = 0
        _Chroma ("Chroma", Float) = 0
        _Twist ("Twist", Float) = 0
        _SinCycle ("SinCycle", Float) = 70
        _SinScale ("SinScale", Float) = 0.18
        _TwistStrength ("TwistStrength", Float) = 10
        _TwistTime ("TwistTime", Float) = 0
        _Vignette ("Vignette", Float) = 0
        _Tv ("Tv", Float) = 0
        _Film ("Film", Float) = 0
        _SpeedAmt ("SpeedAmt", Float) = 0
        _SpeedColor ("SpeedColor", Color) = (1,1,1,1)
        _LineCenter ("LineCenter", Vector) = (0.5, 0.5, 0, 0)
        _LineCount ("LineCount", Float) = 12
        _LineDensity ("LineDensity", Float) = 0.7
        _LineFrames ("LineFrames", Float) = 15
        _LineSpeed ("LineSpeed", Float) = 15
        _LineMask ("LineMask", Float) = -0.12
        _LineFalloff ("LineFalloff", Float) = 0.16
        _Flash ("Flash", Color) = (1,1,1,0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Pixel;
            float _Gray;
            float _Chroma;
            float _Twist;
            float _SinCycle;
            float _SinScale;
            float _TwistStrength;
            float _TwistTime;
            float _Vignette;
            float _Tv;
            float _Film;
            float _SpeedAmt;
            fixed4 _SpeedColor;
            float4 _LineCenter;
            float _LineCount;
            float _LineDensity;
            float _LineFrames;
            float _LineSpeed;
            float _LineMask;
            float _LineFalloff;
            fixed4 _Flash;

            float Hash12(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.546875);
            }

            float Hash127(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.546875);
            }

            float VNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash12(cell);
                float b = Hash12(cell + float2(1, 0));
                float c = Hash12(cell + float2(0, 1));
                float d = Hash12(cell + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float SignedCell(float2 cell)
            {
                return Hash127(cell) * 2.0 - 1.0;
            }

            // 官机 ValueNoise：格子用 127.1/311.7，双线性后再 Hash12，输出 0.4~0.8 花屏。
            float TvSnow(float2 uv)
            {
                float2 p = uv + _Time.xy;
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float n00 = SignedCell(cell);
                float n10 = SignedCell(cell + float2(1, 0));
                float n01 = SignedCell(cell + float2(0, 1));
                float n11 = SignedCell(cell + float2(1, 1));
                float nx0 = lerp(n00, n10, u.x);
                float nx1 = lerp(n01, n11, u.x);
                float n = lerp(nx0, nx1, u.y);
                return 0.4 + 0.4 * Hash12(n.xx);
            }

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // 官机运行值：SinCycle=70、SinScale=0.18、strength=10，TimeLB 以 10/秒推进。
                if (_Twist > 0.001)
                {
                    float wave = sin(uv.y * _SinCycle) * _SinScale;
                    float strength = max(_TwistStrength, 1.0);
                    float band = floor(uv.y * strength);
                    float h = Hash12(float2(band, floor(_TwistTime))) * strength;
                    float q = floor(h + h) / max(h, 1e-5);
                    uv.x += wave * (q * 0.1 - 0.2);
                }

                // Pixelation：pixelSize 8，按屏幕像素向下取整，不要半像素回中。
                float2 uvPix = floor(uv * _ScreenParams.xy / 8.0) * 8.0 / _ScreenParams.xy;
                uv = lerp(uv, uvPix, saturate(_Pixel));

                // RGB：R/B 各偏 colorOffset 0.005。
                float shift = 0.005 * _Chroma;
                float r = tex2D(_MainTex, uv + float2(shift, 0)).r;
                float g = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(shift, 0)).b;
                float3 col = float3(r, g, b);

                // GrayScale：0.30/0.59/0.11，再按 grayScale 0.8 混。
                float lum = dot(col, float3(0.30, 0.59, 0.11));
                col = lerp(col, lum.xxx, saturate(_Gray) * 0.8);

                // BlackSide：uv*(1-uv) 枕形，vignetteSize 17。
                float pillow = uv.x * (1.0 - uv.x) * uv.y * (1.0 - uv.y);
                float keep = saturate(pillow * 17.0);
                col *= lerp(1.0, keep, saturate(_Vignette));

                // OldFilm：1.5 * (c^3 - c) 提反差，刮痕/尘点用程序近似两张滚动贴图。
                if (_Film > 0.001)
                {
                    float3 lifted = col + 0.02;
                    float3 contrast = col + 1.5 * (lifted * lifted * lifted - col);
                    float scratch = pow(saturate(sin((uv.y + _Time.x * 100.0) * 90.0)), 28.0);
                    scratch *= step(0.62, Hash12(float2(floor(uv.x * 64.0), floor(_Time.x * 50.0))));
                    float2 dustUv = uv + _Time.x * float2(15.0, 25.0);
                    float dust = step(0.988, Hash12(floor(dustUv * float2(180.0, 110.0))));
                    float3 grain = saturate(contrast) * float3(1.0, 0.96, 0.84);
                    grain *= 1.0 - scratch * 0.7 - dust * 0.45;
                    col = lerp(col, saturate(grain), saturate(_Film));
                }

                // 官机只开 ComicLine 的像素着色器：v2 减 lineCenter，atan2/length，
                // mask = 1 - (dist - maskSize) * k；噪声 UV 只跟角度走，再按
                // floor(frac(_Time.x * speed) * 15) 整圈分成 15 格去转。
                if (_SpeedAmt > 0.001)
                {
                    float2 d = i.uv - _LineCenter.xy;
                    float dist = length(d);
                    float ang = atan2(d.y, d.x);
                    float k = 0.85;
                    float mask = 1.0 - (dist - _LineMask) * k;
                    float ny = ang * _LineCount * 0.159236 - 0.5;
                    float2 polar = float2(-0.5, ny);
                    float frames = max(_LineFrames, 1.0);
                    float frameN = floor(frac(_Time.x * _LineSpeed) * frames);
                    float rot = frameN / frames * 6.283185;
                    float cs = cos(rot);
                    float sn = sin(rot);
                    float2 rq = float2(polar.x * cs + polar.y * sn, polar.y * cs - polar.x * sn);
                    float2 uvn = rq + 0.5;
                    float nse = VNoise(uvn) * 0.18 + VNoise(uvn * 44.0) * 0.82;
                    float m = saturate((nse - mask - 0.24) * 8.0);
                    float densityLo = _LineDensity - _LineFalloff * 1.125;
                    float densityHi = _LineDensity - _LineFalloff * 0.25;
                    m = smoothstep(densityLo, densityHi, m);
                    m *= smoothstep(0.22, 0.34, dist);
                    m = saturate((m - 0.22) * 1.45);
                    m = saturate(m * _SpeedAmt);
                    col = lerp(col, _SpeedColor.rgb, m);
                }

                // ValueNoise 变体直接输出花屏，开到满时盖住成片。
                col = lerp(col, TvSnow(i.uv).xxx, saturate(_Tv));
                col = lerp(col, _Flash.rgb, saturate(_Flash.a));
                return fixed4(saturate(col), 1);
            }
            ENDCG
        }
    }
}
