Shader "MusePSona/MdScreenFx"
{
    Properties
    {
        _Vignette ("Vignette", Float) = 0
        _Inner ("Inner", Float) = 0.35
        _Outer ("Outer", Float) = 1.15
        _Flash ("Flash", Color) = (1,1,1,0)
        _SpeedAmt ("SpeedAmt", Float) = 0
        _SpeedColor ("SpeedColor", Color) = (0,0,0,0.85)
        _Sepia ("Sepia", Float) = 0
        _Tv ("Tv", Float) = 0
        _Chroma ("Chroma", Float) = 0
        _Pixel ("Pixel", Float) = 0
        _Film ("Film", Float) = 0
        _Aspect ("Aspect", Float) = 1.777
        _AddFlash ("AddFlash", Float) = 0
        [HideInInspector] _SrcBlend ("SrcBlend", Float) = 1
        [HideInInspector] _DstBlend ("DstBlend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest LEqual
        Blend [_SrcBlend] [_DstBlend]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Vignette;
            float _Inner;
            float _Outer;
            fixed4 _Flash;
            float _SpeedAmt;
            fixed4 _SpeedColor;
            float _Sepia;
            float _Tv;
            float _Chroma;
            float _Pixel;
            float _Film;
            float _Aspect;
            float _AddFlash;
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
                float2 uv = i.uv * 2.0 - 1.0;
                uv.x *= _Aspect;
                float r = length(uv);
                float4 col = float4(0, 0, 0, 0);

                if (_AddFlash > 0.5)
                {
                    col.rgb = _Flash.rgb * _Flash.a * 0.32;
                    col.a = 0;
                    return col;
                }

                float vig = smoothstep(_Inner, _Outer, r) * _Vignette;
                col.rgb += vig * 0.92;
                col.a += vig * 0.92;

                // 官机 2U：细白射线，中间空、边缘密，不要铺成实心风车。
                float ang = atan2(uv.y, uv.x);
                float spin = _Time.y * 2.4;
                float spokes = pow(abs(sin(ang * 16.0 + spin)), 36.0);
                float spokes2 = pow(abs(sin(ang * 16.0 + spin * 0.31 + 1.1)), 42.0);
                float radial = smoothstep(0.22, 0.72, r) * (1.0 - smoothstep(1.28, 1.72, r));
                float sl = (spokes * 0.85 + spokes2 * 0.35) * radial * _SpeedAmt;
                col.rgb += _SpeedColor.rgb * sl * _SpeedColor.a;
                col.a = max(col.a, sl * _SpeedColor.a * 0.22);

                col.rgb += float3(0.42, 0.32, 0.18) * _Sepia * 0.28;
                col.a = max(col.a, _Sepia * 0.18);

                float n = frac(sin(dot(i.uv * 240.0, float2(12.9898, 78.233)) + _Time.y * 40.0) * 43758.5453);
                col.rgb += n * _Tv * 0.18;
                col.a = max(col.a, _Tv * n * 0.18);

                float fringe = smoothstep(0.25, 1.2, r) * _Chroma;
                col.rgb += float3(0.55, 0.08, 0.12) * fringe * 0.45;
                col.rgb += float3(0.08, 0.12, 0.55) * fringe * 0.35;
                col.a = max(col.a, fringe * 0.22);

                // 2P：官机像素块约 8。这里只能叠块面，采不到画面。
                float2 cell = floor(i.uv * float2(48.0 * max(_Aspect, 0.01), 27.0));
                float block = fmod(cell.x + cell.y, 2.0);
                col.rgb += (0.04 + block * 0.05) * _Pixel;
                col.a = max(col.a, _Pixel * 0.16);

                // 2W：划痕和灰尘一直在滚，没有单独淡变时长。
                float scratch = pow(abs(sin(i.uv.y * 70.0 + _Time.y * 6.0)), 90.0);
                float dust = step(0.985, frac(sin(dot(floor(i.uv * float2(64.0, 36.0) + _Time.yy * float2(4.0, 9.0)), float2(12.9898, 78.233))) * 43758.5453));
                col.rgb += float3(0.72, 0.64, 0.48) * _Film * (scratch * 0.55 + dust * 0.35);
                col.a = max(col.a, _Film * (scratch * 0.28 + dust * 0.16));
                return col;
            }
            ENDCG
        }
    }
}
