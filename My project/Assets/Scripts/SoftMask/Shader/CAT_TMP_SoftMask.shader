// TextMeshPro Mobile SDF + SoftMask
// TMP_SDF-Mobile.shader 기반으로 SoftMask 알파 마스킹 추가
// - TMPro_Properties.cginc 직접 포함 (TMP 유니폼 호환성 보장)
// - SoftMask 프로퍼티는 _SoftMask* 접두사 사용 (TMP의 _MaskTex 충돌 방지)
// - SoftMask UV는 버텍스 셰이더에서 계산 (모바일 최적화)
// - 중첩 마스크 지원 (_SOFTMASK_NESTED 키워드)
// - Premultiplied alpha 블렌딩 호환

Shader "CAT/UI/TMP_SoftMask"
{
    Properties
    {
        // --- TMP Properties (TMP_SDF-Mobile.shader 동일) ---
        _FaceColor          ("Face Color", Color) = (1,1,1,1)
        _FaceDilate         ("Face Dilate", Range(-1,1)) = 0

        _OutlineColor       ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth       ("Outline Thickness", Range(0,1)) = 0
        _OutlineSoftness    ("Outline Softness", Range(0,1)) = 0

        _UnderlayColor      ("Border Color", Color) = (0,0,0,.5)
        _UnderlayOffsetX    ("Border OffsetX", Range(-1,1)) = 0
        _UnderlayOffsetY    ("Border OffsetY", Range(-1,1)) = 0
        _UnderlayDilate     ("Border Dilate", Range(-1,1)) = 0
        _UnderlaySoftness   ("Border Softness", Range(0,1)) = 0

        _WeightNormal       ("Weight Normal", float) = 0
        _WeightBold         ("Weight Bold", float) = .5

        _ShaderFlags        ("Flags", float) = 0
        _ScaleRatioA        ("Scale RatioA", float) = 1
        _ScaleRatioB        ("Scale RatioB", float) = 1
        _ScaleRatioC        ("Scale RatioC", float) = 1

        _MainTex            ("Font Atlas", 2D) = "white" {}
        _TextureWidth       ("Texture Width", float) = 512
        _TextureHeight      ("Texture Height", float) = 512
        _GradientScale      ("Gradient Scale", float) = 5
        _ScaleX             ("Scale X", float) = 1
        _ScaleY             ("Scale Y", float) = 1
        _PerspectiveFilter  ("Perspective Correction", Range(0, 1)) = 0.875
        _Sharpness          ("Sharpness", Range(-1,1)) = 0

        _VertexOffsetX      ("Vertex OffsetX", float) = 0
        _VertexOffsetY      ("Vertex OffsetY", float) = 0

        _ClipRect           ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)
        _MaskSoftnessX      ("Mask SoftnessX", float) = 0
        _MaskSoftnessY      ("Mask SoftnessY", float) = 0

        _StencilComp        ("Stencil Comparison", Float) = 8
        _Stencil            ("Stencil ID", Float) = 0
        _StencilOp          ("Stencil Operation", Float) = 0
        _StencilWriteMask   ("Stencil Write Mask", Float) = 255
        _StencilReadMask    ("Stencil Read Mask", Float) = 255

        _CullMode           ("Cull Mode", Float) = 0
        _ColorMask          ("Color Mask", Float) = 15

        // --- SoftMask Properties (_SoftMask 접두사: TMP _MaskTex 충돌 방지) ---
        _SoftMaskTex        ("SoftMask Texture", 2D) = "white" {}
        _SoftMaskSoftness   ("SoftMask Softness", Range(0, 1)) = 0.1
        _SoftMaskInvert     ("SoftMask Invert", Float) = 0
        _SoftMaskUVRect     ("SoftMask UV Rect", Vector) = (0, 0, 1, 1)

        [HideInInspector] _SoftMaskTex2      ("SoftMask Texture 2", 2D) = "white" {}
        [HideInInspector] _SoftMaskSoftness2  ("SoftMask Softness 2", Range(0, 1)) = 0.1
        [HideInInspector] _SoftMaskInvert2    ("SoftMask Invert 2", Float) = 0
        [HideInInspector] _SoftMaskUVRect2    ("SoftMask UV Rect 2", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        ZWrite Off
        Lighting Off
        Fog { Mode Off }
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex VertShader
            #pragma fragment PixShader
            #pragma multi_compile __ OUTLINE_ON
            #pragma multi_compile __ UNDERLAY_ON UNDERLAY_INNER
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local _ _SOFTMASK_NESTED

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Assets/Plugins/TextMesh Pro/Shaders/TMPro_Properties.cginc"

            // Unity 6 추가 유니폼 (TMPro_Properties.cginc에 포함되지 않음)
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            // ─────────────────────────────────────────
            // SoftMask 유니폼 (_SoftMask 접두사)
            // ─────────────────────────────────────────
            sampler2D _SoftMaskTex;
            half _SoftMaskSoftness;
            half _SoftMaskInvert;
            float4x4 _SoftMaskWorldToUV;
            float4 _SoftMaskUVRect;

            sampler2D _SoftMaskTex2;
            half _SoftMaskSoftness2;
            half _SoftMaskInvert2;
            float4x4 _SoftMaskWorldToUV2;
            float4 _SoftMaskUVRect2;

            // ─────────────────────────────────────────
            // SoftMask 샘플링 (half precision, 분기 없음)
            // Metal 백엔드 호환: 전역 유니폼 직접 접근
            // ─────────────────────────────────────────
            inline half SampleSoftMask1(float2 uv)
            {
                half inBounds = step(0.0h, uv.x) * step(uv.x, 1.0h)
                              * step(0.0h, uv.y) * step(uv.y, 1.0h);
                float2 atlasUV = _SoftMaskUVRect.xy + uv * _SoftMaskUVRect.zw;
                half maskAlpha = tex2D(_SoftMaskTex, atlasUV).a;
                half softEdge = smoothstep(0.0h, max(_SoftMaskSoftness, 0.001h), maskAlpha);
                half finalMask = lerp(softEdge, 1.0h - softEdge, _SoftMaskInvert);
                return finalMask * inBounds;
            }

            #if defined(_SOFTMASK_NESTED)
            inline half SampleSoftMask2(float2 uv)
            {
                half inBounds = step(0.0h, uv.x) * step(uv.x, 1.0h)
                              * step(0.0h, uv.y) * step(uv.y, 1.0h);
                float2 atlasUV = _SoftMaskUVRect2.xy + uv * _SoftMaskUVRect2.zw;
                half maskAlpha = tex2D(_SoftMaskTex2, atlasUV).a;
                half softEdge = smoothstep(0.0h, max(_SoftMaskSoftness2, 0.001h), maskAlpha);
                half finalMask = lerp(softEdge, 1.0h - softEdge, _SoftMaskInvert2);
                return finalMask * inBounds;
            }
            #endif

            // ─────────────────────────────────────────
            // 구조체
            // ─────────────────────────────────────────
            struct vertex_t
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex     : POSITION;
                float3 normal     : NORMAL;
                fixed4 color      : COLOR;
                float4 texcoord0  : TEXCOORD0;
                float2 texcoord1  : TEXCOORD1;
            };

            struct pixel_t
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex       : SV_POSITION;
                fixed4 faceColor    : COLOR;
                fixed4 outlineColor : COLOR1;
                float4 texcoord0    : TEXCOORD0;
                half4  param        : TEXCOORD1;
                half4  mask         : TEXCOORD2;
                #if (UNDERLAY_ON | UNDERLAY_INNER)
                float4 texcoord1    : TEXCOORD3;
                half2  underlayParam : TEXCOORD4;
                #endif
                float2 softMaskUV   : TEXCOORD5;
                #if defined(_SOFTMASK_NESTED)
                float2 softMaskUV2  : TEXCOORD6;
                #endif
            };

            // ─────────────────────────────────────────
            // 버텍스 셰이더
            // ─────────────────────────────────────────
            pixel_t VertShader(vertex_t input)
            {
                pixel_t output;
                UNITY_INITIALIZE_OUTPUT(pixel_t, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float bold = step(input.texcoord0.w, 0);

                float4 vert = input.vertex;
                vert.x += _VertexOffsetX;
                vert.y += _VertexOffsetY;
                float4 vPosition = UnityObjectToClipPos(vert);

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(_ScaleX, _ScaleY) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float scale = rsqrt(dot(pixelSize, pixelSize));
                scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
                if (UNITY_MATRIX_P[3][3] == 0) scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(UnityObjectToWorldNormal(input.normal.xyz), normalize(WorldSpaceViewDir(vert)))));

                float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
                weight = (weight + _FaceDilate) * _ScaleRatioA * 0.5;

                float layerScale = scale;

                scale /= 1 + (_OutlineSoftness * _ScaleRatioA * scale);
                float bias = (0.5 - weight) * scale - 0.5;
                float outline = _OutlineWidth * _ScaleRatioA * 0.5 * scale;

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                }
                float opacity = input.color.a;
                #if (UNDERLAY_ON | UNDERLAY_INNER)
                opacity = 1.0;
                #endif

                fixed4 faceColor = fixed4(input.color.rgb, opacity) * _FaceColor;
                faceColor.rgb *= faceColor.a;

                fixed4 outlineColor = _OutlineColor;
                outlineColor.a *= opacity;
                outlineColor.rgb *= outlineColor.a;
                outlineColor = lerp(faceColor, outlineColor, sqrt(min(1.0, (outline * 2))));

                #if (UNDERLAY_ON | UNDERLAY_INNER)
                layerScale /= 1 + ((_UnderlaySoftness * _ScaleRatioC) * layerScale);
                float layerBias = (.5 - weight) * layerScale - .5 - ((_UnderlayDilate * _ScaleRatioC) * .5 * layerScale);

                float x = -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth;
                float y = -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight;
                float2 layerOffset = float2(x, y);
                #endif

                // TMP 클리핑 UV (RectMask2D용)
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (vert.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);

                output.vertex = vPosition;
                output.faceColor = faceColor;
                output.outlineColor = outlineColor;
                output.texcoord0 = float4(input.texcoord0.x, input.texcoord0.y, maskUV.x, maskUV.y);
                output.param = half4(scale, bias - outline, bias + outline, bias);

                const half2 maskSoftness = half2(max(_UIMaskSoftnessX, _MaskSoftnessX), max(_UIMaskSoftnessY, _MaskSoftnessY));
                output.mask = half4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * maskSoftness + pixelSize.xy));

                #if (UNDERLAY_ON || UNDERLAY_INNER)
                output.texcoord1 = float4(input.texcoord0 + layerOffset, input.color.a, 0);
                output.underlayParam = half2(layerScale, layerBias);
                #endif

                // SoftMask UV (월드 좌표 -> 마스크 UV, 버텍스에서 계산)
                float3 worldPos = mul(unity_ObjectToWorld, vert).xyz;
                output.softMaskUV = mul(_SoftMaskWorldToUV, float4(worldPos, 1)).xy;
                #if defined(_SOFTMASK_NESTED)
                output.softMaskUV2 = mul(_SoftMaskWorldToUV2, float4(worldPos, 1)).xy;
                #endif

                return output;
            }

            // ─────────────────────────────────────────
            // 프래그먼트 셰이더
            // ─────────────────────────────────────────
            fixed4 PixShader(pixel_t input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half d = tex2D(_MainTex, input.texcoord0.xy).a * input.param.x;
                half4 c = input.faceColor * saturate(d - input.param.w);

                #ifdef OUTLINE_ON
                c = lerp(input.outlineColor, input.faceColor, saturate(d - input.param.z));
                c *= saturate(d - input.param.y);
                #endif

                #if UNDERLAY_ON
                d = tex2D(_MainTex, input.texcoord1.xy).a * input.underlayParam.x;
                c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * saturate(d - input.underlayParam.y) * (1 - c.a);
                #endif

                #if UNDERLAY_INNER
                half sd = saturate(d - input.param.z);
                d = tex2D(_MainTex, input.texcoord1.xy).a * input.underlayParam.x;
                c += float4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * (1 - saturate(d - input.underlayParam.y)) * sd * (1 - c.a);
                #endif

                // RectMask2D 클리핑
                #if UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                c *= m.x * m.y;
                #endif

                #if (UNDERLAY_ON | UNDERLAY_INNER)
                c *= input.texcoord1.z;
                #endif

                // SoftMask 적용 (premultiplied alpha: RGB + A 모두 마스크)
                c *= SampleSoftMask1(input.softMaskUV);

                #if defined(_SOFTMASK_NESTED)
                c *= SampleSoftMask2(input.softMaskUV2);
                #endif

                #if UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif

                return c;
            }
            ENDCG
        }
    }

    Fallback "TextMeshPro/Mobile/Distance Field"
}
