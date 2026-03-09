// [OptionalShader] SoftMaskLight: CAT/Effects/UIShining
// CAT/Effects/UIShining 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
Shader "Hidden/CAT/Effects/UIShining (SoftMaskLight)"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        [PerRendererData] _Progress("Progress", Range(0, 1)) = 0
        [PerRendererData] _WidthStart("Band Width Start", Range(0.01, 1)) = 0.15
        [PerRendererData] _WidthEnd("Band Width End", Range(0.01, 1)) = 0.15
        [PerRendererData] _Intensity("Intensity", Range(0, 3)) = 1.35
        [PerRendererData] _CurvatureStart("Curvature Start", Range(-1, 1)) = 0.3
        [PerRendererData] _CurvatureEnd("Curvature End", Range(-1, 1)) = 0.3
        [PerRendererData] _Angle("Angle (deg)", Range(-180, 180)) = 0
        [PerRendererData] _ProgressOffset("Progress Offset", Range(0, 2)) = 0.55
        [PerRendererData] _ShineColor("Shine Color", Color) = (1, 1, 1, 1)
        // SoftMaskLight_Core.cginc의 _Softness(마스크)와 충돌 방지를 위해 _ShineSoftness로 변경
        [PerRendererData] _ShineSoftness("Shine Softness", Range(0, 1)) = 0
        [PerRendererData] _BurnBias("Burn Bias (bright = strong)", Range(0, 1)) = 0.85
        [PerRendererData] _BlendStrength("Blend Strength", Range(0.5, 2.5)) = 1.45
        [HideInInspector] _SpriteUVRect("Sprite UV Rect", Vector) = (0, 0, 1, 1)

        // UI 스텐실/마스크 설정
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15

        // SoftMaskLight 프로퍼티
        [HideInInspector] _MaskTex ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _Softness ("Softness", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask ("Invert Mask", Float) = 0
        [HideInInspector] _MaskUVRect ("Mask UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder ("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV ("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _Softness2 ("Softness 2", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask2 ("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder2 ("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2 ("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
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
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma target 2.0
            // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE

            // SoftMaskLight 항상 활성화
            #define _CAT_SOFTMASK 1
            #include "../../SoftMaskLight/Shader/SoftMaskLight_Core.cginc"

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex    : POSITION;
                half4 color      : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 texcoord2 : TEXCOORD2;
                float2 texcoord3 : TEXCOORD3;
                float4 tangent   : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                half4 color          : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 spriteUVRect  : TEXCOORD2;
                float2 localPos      : TEXCOORD3;
                CAT_SOFTMASK_COORDS(4, 5)
                half2 angleSinCos    : TEXCOORD6; // sin/cos를 버텍스에서 계산하여 전달
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Progress;
            half _WidthStart;
            half _WidthEnd;
            half _Intensity;
            half _CurvatureStart;
            half _CurvatureEnd;
            half _Angle;
            half _ProgressOffset;
            half4 _ShineColor;
            // _Softness는 SoftMaskLight_Core.cginc가 마스크 소프트니스로 사용하므로
            // 광택 소프트니스는 _ShineSoftness로 분리
            half _ShineSoftness;
            half _BurnBias;
            half _BlendStrength;
            float4 _SpriteUVRect;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                OUT.spriteUVRect = float4(v.texcoord2.xy, v.texcoord3.xy);
                OUT.localPos = v.tangent.xy;
                // _Angle은 프레임 내 상수이므로 버텍스에서 sin/cos 계산 (픽셀당 반복 방지)
                half rad = _Angle * 0.0174532925h;
                OUT.angleSinCos = half2(sin(rad), cos(rad));

                // SoftMaskLight 버텍스 처리 (항상 활성)
                CAT_SOFTMASK_VERT(v.vertex.xyz, OUT)

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 base = tex2D(_MainTex, IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                base.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(base.a - 0.001h);
                #endif

                // RectTransform 로컬 좌표 사용 (Sliced 이미지 대응)
                half2 localPos = IN.localPos;

                // Fallback: ModifyMesh가 호출되지 않았거나 tangent가 0인 경우 텍스처 UV 사용
                // if 분기 대신 step/lerp로 GPU 분기 회피
                half validLocal = step(0.001h, max(localPos.x, localPos.y));

                // 무효 시 텍스처 UV 기반 fallback 계산
                half4 spriteRect = IN.spriteUVRect;
                half2 uvSpriteMin = spriteRect.xy;
                half2 uvSpriteSize = spriteRect.zw - spriteRect.xy;
                // spriteRect가 무효할 경우 _SpriteUVRect 사용
                half validRect = step(0.001h, uvSpriteSize.x) * step(0.001h, uvSpriteSize.y);
                uvSpriteMin = lerp(_SpriteUVRect.xy, uvSpriteMin, validRect);
                uvSpriteSize = lerp(_SpriteUVRect.zw - _SpriteUVRect.xy, uvSpriteSize, validRect);
                uvSpriteSize = max(uvSpriteSize, half2(1e-5h, 1e-5h));
                half2 fallbackPos = (IN.texcoord - uvSpriteMin) / uvSpriteSize;

                // validLocal이면 localPos, 아니면 fallbackPos
                localPos = lerp(fallbackPos, localPos, validLocal);

                // 로컬 좌표를 _Angle만큼 회전
                // sin/cos는 버텍스 셰이더에서 계산하여 전달 (픽셀당 반복 방지)
                half sinA = IN.angleSinCos.x;
                half cosA = IN.angleSinCos.y;
                half ax = -localPos.x * sinA + localPos.y * cosA;
                half ay = localPos.x * cosA + localPos.y * sinA;

                half effectiveProgress = -_ProgressOffset + (1.0h + 2.0h * _ProgressOffset) * _Progress;
                half curvature = lerp(_CurvatureStart, _CurvatureEnd, _Progress);
                half ayCenter = effectiveProgress + curvature * (ax - 0.5h) * (ax - 0.5h);
                half d = abs(ay - ayCenter);

                half width = lerp(_WidthStart, _WidthEnd, _Progress);
                half falloffSharp = 1.0h - smoothstep(0.0h, width, d);
                half sigma = max(width * 0.6h, 1e-5h);
                half falloffSoft = exp(-(d * d) / (2.0h * sigma * sigma));
                half falloff = lerp(falloffSharp, falloffSoft, _ShineSoftness);
                half lum = dot(base.rgb, half3(0.299h, 0.587h, 0.114h));
                half burnFactor = lerp(lerp(0.12h, 1.0h, pow(saturate(lum), 0.6h)), 1.0h, 1.0h - _BurnBias);
                half3 shineAdd = _ShineColor.rgb * _Intensity * falloff * burnFactor * base.a * _BlendStrength;

                half4 finalColor = half4(base.rgb + shineAdd, base.a);

                // SoftMaskLight 적용 (항상 활성)
                finalColor.a *= CAT_SOFTMASK_FRAG(IN);

                return finalColor;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
