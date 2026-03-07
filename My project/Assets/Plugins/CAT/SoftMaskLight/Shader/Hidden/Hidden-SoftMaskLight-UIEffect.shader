// [OptionalShader] SoftMaskLight: Hidden/UI/Default (UIEffect)
// UIEffect 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
// 원본: UIDefault_UIEffect.shader
// UIEffect 패키지 업데이트 시 수동 병합 필요
Shader "Hidden/UI/Default (UIEffect) (SoftMaskLight)"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        // UIEffect 블렌드 모드 (UIEffect.cs가 런타임에 설정)
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10

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

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend [_SrcBlend] [_DstBlend]
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
            #pragma multi_compile _ UNITY_UI_CLIP_RECT
            #pragma multi_compile _ UNITY_UI_ALPHACLIP

            // ==== UIEFFECT START ====
            // multi_compile 사용: 런타임에 동적 생성되는 프록시 머티리얼에서
            // shader_feature 변형이 컴파일되지 않는 문제 방지
            #pragma multi_compile_local_fragment _ TONE_GRAYSCALE TONE_SEPIA TONE_NEGATIVE TONE_RETRO TONE_POSTERIZE
            #pragma multi_compile_local_fragment _ COLOR_FILTER
            #pragma multi_compile_local_fragment _ SAMPLING_BLUR_FAST SAMPLING_BLUR_MEDIUM SAMPLING_BLUR_DETAIL SAMPLING_PIXELATION SAMPLING_RGB_SHIFT SAMPLING_EDGE_LUMINANCE SAMPLING_EDGE_ALPHA
            #pragma multi_compile_local_fragment _ TRANSITION_FADE TRANSITION_CUTOFF TRANSITION_DISSOLVE TRANSITION_SHINY TRANSITION_MASK TRANSITION_MELT TRANSITION_BURN TRANSITION_PATTERN TRANSITION_BLAZE
            #pragma multi_compile_local_fragment _ EDGE_PLAIN EDGE_SHINY
            #pragma multi_compile_local_fragment _ DETAIL_MASKING DETAIL_MULTIPLY DETAIL_ADDITIVE DETAIL_SUBTRACTIVE DETAIL_REPLACE DETAIL_MULTIPLY_ADDITIVE
            #pragma multi_compile_local_fragment _ TARGET_HUE TARGET_LUMINANCE
            #pragma multi_compile_local_fragment _ GRADATION_GRADIENT GRADATION_COLOR2 GRADATION_COLOR4
            #pragma multi_compile_fragment _ UIEFFECT_EDITOR
            // ==== UIEFFECT END ====

            // ==== SOFTMASKABLE START (mob-sakai SoftMaskable 호환 -- 기존 기능 보존) ====
            #pragma shader_feature_fragment _ SOFTMASK_EDITOR
            #pragma shader_feature_local_fragment _ SOFTMASKABLE
            #if SOFTMASKABLE
            #include "Packages/com.coffee.softmask-for-ugui/Shaders/SoftMask.cginc"
            #endif
            // ==== SOFTMASKABLE END ====

            // ==== CAT SOFTMASK START ====
            // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE
            // SoftMaskLight 항상 활성화
            #define _CAT_SOFTMASK 1
            #include "../SoftMaskLight_Core.cginc"
            // ==== CAT SOFTMASK END ====

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                // ==== UIEFFECT START ====
                float4 uvMask : TEXCOORD1;
                // ==== UIEFFECT END ====
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask : TEXCOORD2;
                // ==== UIEFFECT START ====
                float4 uvMask : TEXCOORD3;
                // ==== UIEFFECT END ====
                UNITY_VERTEX_OUTPUT_STEREO
                // ==== CAT SOFTMASK START ====
                // TEXCOORD 4, 5 슬롯 사용 (UIEFFECT는 0~3 사용)
                CAT_SOFTMASK_COORDS(4, 5)
                // ==== CAT SOFTMASK END ====
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                  0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace)
                {
                    if (!IsGammaSpace())
                    {
                        v.color.rgb = GammaToLinearSpace(v.color.rgb);
                    }
                }

                OUT.color = v.color * _Color;

                // ==== UIEFFECT START ====
                OUT.uvMask = v.uvMask;
                // ==== UIEFFECT END ====

                // ==== CAT SOFTMASK START ====
                // v.vertex는 Canvas 로컬 좌표 (unity_ObjectToWorld 미사용 -> Overlay 호환)
                // 항상 활성 (가드 없음)
                CAT_SOFTMASK_VERT(v.vertex.xyz, OUT)
                // ==== CAT SOFTMASK END ====

                return OUT;
            }

            // ==== UIEFFECT START ====
            v2f _fragInput;
            fixed4 uieffect_frag(float2 uv)
            {
                v2f IN = _fragInput;
                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd);
                color.rgb *= color.a;
                return color;
            }

            #include "Packages/com.coffee.ui-effect/Shaders/UIEffect.cginc"
            // ==== UIEFFECT END ====

            half4 frag(v2f IN) : SV_Target
            {
                // 알파 정밀도 보정 (HDR 투명도 블렌딩 안정성)
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0 / alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision) * invAlphaPrecision;

                _fragInput = IN;
                half4 c = uieffect(IN.texcoord, IN.uvMask, IN.worldPosition);
                c.rgb *= IN.color.rgb;
                c *= IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                c *= m.x * m.y;
                #endif

                // ==== SOFTMASKABLE START (mob-sakai 호환) ====
                #if SOFTMASKABLE
                c *= SoftMask(IN.vertex, IN.worldPosition, c.a);
                #endif
                // ==== SOFTMASKABLE END ====

                // ==== CAT SOFTMASK START ====
                // premultiplied alpha 방식: RGB와 A 모두 마스크 곱
                // UIEffect는 Blend One OneMinusSrcAlpha (premultiplied alpha blending) 사용
                // 항상 활성 (가드 없음)
                c *= CAT_SOFTMASK_FRAG(IN);
                // ==== CAT SOFTMASK END ====

                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
