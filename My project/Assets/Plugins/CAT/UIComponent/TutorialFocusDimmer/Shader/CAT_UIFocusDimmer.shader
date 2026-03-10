Shader "CAT/UI/FocusDimmer"
{
    Properties
    {
        // UGUI 배칭 호환성을 위해 선언 유지 (샘플링은 수행하지 않음 — mainTexture = s_WhiteTexture 고정)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FocusRect ("Focus Rect (xMin, yMin, xMax, yMax) local", Vector) = (0,0,100,100)
        _CornerRadius ("Hole Corner Radius", Range(0, 200)) = 16
        _HoleSoftness ("Hole Edge Softness", Range(0, 100)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                // UGUI 메시 형식 호환 (프래그먼트에서 미사용)
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                // ClipRect + SDF 모두 XY만 필요 — float4 → float2로 인터폴레이터 절약
                float2 localPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float4 _FocusRect;
            half   _CornerRadius;  // half precision으로 충분 (0~200 범위)
            half   _HoleSoftness;  // half precision으로 충분 (0~100 범위)

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.localPos = v.vertex.xy;  // XY만 전달
                OUT.vertex   = UnityObjectToClipPos(v.vertex);
                OUT.color    = v.color * _Color;
                return OUT;
            }

            // 둥근 사각형 경계까지의 부호 있는 거리 (내부: 음수, 외부: 양수)
            float RoundedRectSDF(float2 p, float2 center, float2 halfSize, float r)
            {
                float2 b = max(halfSize - r, 0.001);
                float2 d = abs(p - center) - b;
                return length(max(d, 0)) + min(max(d.x, d.y), 0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // mainTexture = s_WhiteTexture 고정이므로 텍스처 샘플링 불필요
                half4 color = IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.localPos, _ClipRect);
                #endif

                // 포커스 구멍 SDF 계산
                float2 center   = (_FocusRect.xy + _FocusRect.zw) * 0.5;
                float2 halfSize = (_FocusRect.zw - _FocusRect.xy) * 0.5;
                float  r        = max(0, _CornerRadius);
                float  soft     = max(_HoleSoftness, 0.001);

                float sdf    = RoundedRectSDF(IN.localPos, center, halfSize, r);
                float inside = 1.0 - smoothstep(-soft, 0, sdf);
                color.a     *= (1.0 - inside);

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
