Shader "MusePSona/MdSprite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _UseClip ("UseClip", Float) = 0
        _ClipRect ("ClipRect", Vector) = (0,0,0,0)
        _ClipValue ("ClipValue", Float) = 1
        _ClipMirror ("ClipMirror", Float) = 0
        _ClipAxis ("ClipAxis", Float) = 0.5
        _ClipUp ("ClipUp", Float) = 1
        _ClipDown ("ClipDown", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _UseClip;
            float4 _ClipRect;
            float _ClipValue;
            float _ClipMirror;
            float _ClipAxis;
            float _ClipUp;
            float _ClipDown;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 wxy : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wxy = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                if (_UseClip > 0.5)
                {
                    if (i.wxy.x < _ClipRect.x || i.wxy.x > _ClipRect.z || i.wxy.y < _ClipRect.y || i.wxy.y > _ClipRect.w)
                        discard;
                    if (i.uv.y > _ClipValue) discard;
                }
                // 水平镜面：_ClipUp 是彩虹轨半宽，_ClipDown 是两侧共用的外沿。轨内裁掉。
                if (_ClipMirror > 0.5)
                {
                    float d = abs(i.uv.y - _ClipAxis);
                    if (d < _ClipUp || d > _ClipDown) discard;
                }
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
