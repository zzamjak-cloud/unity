// [OptionalShader] SoftMaskLight: CAT/Effects/ColorReplace
// CAT/Effects/ColorReplace 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
Shader "Hidden/CAT/Effects/ColorReplace (SoftMaskLight)"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        // PerRendererData로 PropertyBlock에서 개별 값 설정 가능
        [PerRendererData] _HSVRangeMin("HSV Affect Min", Range(0, 1)) = 0
        [PerRendererData] _HSVRangeMax("HSV Affect Max", Range(0, 1)) = 1
        [PerRendererData] _HSVAAdjust("HSVA Adjust", Vector) = (0, 0, 0, 0)

        // UI 스텐실/마스크 설정
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

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

    // 공통 HSV 함수를 CGINCLUDE로 분리
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    half _HSVRangeMin;
    half _HSVRangeMax;
    half4 _HSVAAdjust;

    // RGB -> HSV 변환 (half precision)
    inline half3 RGB2HSV(half3 c)
    {
        half4 K = half4(0.0h, -1.0h / 3.0h, 2.0h / 3.0h, -1.0h);
        half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
        half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
        half d = q.x - min(q.w, q.y);
        half e = 1.0e-4h;
        return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
    }

    // HSV -> RGB 변환 (half precision)
    inline half3 HSV2RGB(half3 c)
    {
        c = half3(c.x, saturate(c.yz));
        half4 K = half4(1.0h, 2.0h / 3.0h, 1.0h / 3.0h, 3.0h);
        half3 p = abs(frac(c.xxx + K.xyz) * 6.0h - K.www);
        return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
    }

    // HSV 범위 체크 (분기 없이 수학 연산)
    inline half ComputeAffectMult(half hue, half rangeMin, half rangeMax)
    {
        half isWrapped = step(rangeMax + 0.001h, rangeMin);
        half normalCase = step(rangeMin, hue) * step(hue, rangeMax);
        half wrappedCase = saturate(step(rangeMin, hue) + step(hue, rangeMax));
        return lerp(normalCase, wrappedCase, isWrapped);
    }
    ENDCG

    // UI SubShader (SoftMaskLight는 UI 전용)
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

            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                half4 color          : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                CAT_SOFTMASK_COORDS(2, 3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

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

                // SoftMaskLight 버텍스 처리 (항상 활성)
                CAT_SOFTMASK_VERT(v.vertex.xyz, OUT)

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                half3 hsv = RGB2HSV(color.rgb);
                half affectMult = ComputeAffectMult(hsv.x, _HSVRangeMin, _HSVRangeMax);
                half3 rgb = HSV2RGB(hsv + _HSVAAdjust.xyz * affectMult);

                half4 finalColor = half4(rgb, saturate(color.a + _HSVAAdjust.w));

                // SoftMaskLight 적용 (항상 활성)
                finalColor.a *= CAT_SOFTMASK_FRAG(IN);

                return finalColor;
            }
            ENDCG
        }
    }
}
