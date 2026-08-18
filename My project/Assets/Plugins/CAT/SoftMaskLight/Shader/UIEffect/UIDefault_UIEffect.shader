// SoftMaskLight 마스킹을 지원하는 UIEffect 셰이더 변형
// 원본: Library/PackageCache/com.coffee.ui-effect/Shaders/UIEffect.shader
// (UIEffect.cginc를 직접 include하므로 이펙트 로직 드리프트 없음 — 구조 변경 시에만 수동 병합)
//
// 주의: 패키지 셰이더(Hidden/UI/Default (UIEffect))와 이름이 겹치면 Shader.Find가
// 임포트 순서에 따라 아무 쪽이나 선택해 마스킹이 조용히 꺼진다.
// 반드시 고유 이름을 유지하고, UIEffectSoftMaskLightProxy가 프록시 머티리얼의
// 셰이더를 이 변형으로 명시 교체한다 (키워드는 머티리얼에 보존되므로 이펙트 유지).
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

        // SoftMaskLight 프로퍼티 (_CAT_SOFTMASK 키워드 활성화 시 사용)
        [HideInInspector] _MaskTex ("Mask Texture", 2D) = "white" {}
        [HideInInspector] _SoftnessRcp ("Softness Rcp", Float) = 10
        [HideInInspector] _InvertMask ("Invert Mask", Float) = 0
        [HideInInspector] _MaskUVRect ("Mask UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder ("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV ("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX ("Mask Slice Slope X", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY ("Mask Slice Slope Y", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA ("Mask Fill Line A", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB ("Mask Fill Line B", Vector) = (0, 0, 1, 10000)
        [HideInInspector] _MaskTex2 ("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _SoftnessRcp2 ("Softness Rcp 2", Float) = 10
        [HideInInspector] _InvertMask2 ("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2 ("Mask UV Rect 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceBorder2 ("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2 ("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX2 ("Mask Slice Slope X 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY2 ("Mask Slice Slope Y 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA2 ("Mask Fill Line A 2", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB2 ("Mask Fill Line B 2", Vector) = (0, 0, 1, 10000)
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
            #pragma shader_feature_local_fragment _ TONE_GRAYSCALE TONE_SEPIA TONE_NEGATIVE TONE_RETRO TONE_POSTERIZE
            #pragma shader_feature_local_fragment _ COLOR_FILTER
            #pragma shader_feature_local_fragment _ SAMPLING_BLUR_FAST SAMPLING_BLUR_MEDIUM SAMPLING_BLUR_DETAIL SAMPLING_PIXELATION SAMPLING_RGB_SHIFT SAMPLING_EDGE_LUMINANCE SAMPLING_EDGE_ALPHA
            #pragma shader_feature_local_fragment _ TRANSITION_FADE TRANSITION_CUTOFF TRANSITION_DISSOLVE TRANSITION_SHINY TRANSITION_MASK TRANSITION_MELT TRANSITION_BURN TRANSITION_PATTERN TRANSITION_BLAZE
            #pragma shader_feature_local_fragment _ EDGE_PLAIN EDGE_SHINY
            #pragma shader_feature_local_fragment _ DETAIL_MASKING DETAIL_MULTIPLY DETAIL_ADDITIVE DETAIL_SUBTRACTIVE DETAIL_REPLACE DETAIL_MULTIPLY_ADDITIVE
            #pragma shader_feature_local_fragment _ TARGET_HUE TARGET_LUMINANCE
            #pragma shader_feature_local_fragment _ GRADATION_GRADIENT GRADATION_COLOR2 GRADATION_COLOR4
            #pragma shader_feature_fragment _ UIEFFECT_EDITOR
            // ==== UIEFFECT END ====

            // ==== CAT SOFTMASK START ====
            // 이 셰이더의 변형 수는 UIEffect의 이펙트 조합(shader_feature)과 곱해지므로
            // 마스크 키워드를 최소화한다. 슬라이스는 키워드 대신 상시 컴파일:
            // 슬라이스가 아닌 마스크는 C#이 항등 파라미터를 넣어 리매핑이 no-op이 된다.
            #pragma multi_compile_local _ _CAT_SOFTMASK
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #define _CAT_SOFTMASK_FORCE_SLICE 1
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
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
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

                // ==== CAT SOFTMASK START ====
                // premultiplied alpha 방식: RGB와 A 모두 마스크 곱
                c *= CAT_SOFTMASK_FRAG(IN);
                // ==== CAT SOFTMASK END ====

                #ifdef UNITY_UI_ALPHACLIP
                clip (c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }
}
