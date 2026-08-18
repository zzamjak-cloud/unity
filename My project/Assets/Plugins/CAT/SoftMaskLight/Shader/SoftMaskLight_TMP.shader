// TextMeshPro Mobile SDF + SoftMask
// TMP_SDF-Mobile.shader 기반으로 SoftMask 알파 마스킹 추가
// - TMPro_Properties.cginc 직접 포함 (TMP 유니폼 호환성 보장)
// - SoftMask 프로퍼티는 _SoftMask* 접두사 사용 (TMP의 _MaskTex 충돌 방지)
// - SoftMask UV는 버텍스 셰이더에서 계산 (모바일 최적화)
// - 중첩 마스크 지원 (_SOFTMASK_NESTED 키워드)
// - Premultiplied alpha 블렌딩 호환

Shader "SoftMaskLight/UI/TMP_SoftMask"
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
        // 소프트니스 역수 (= 1 / max(softness, 0.001), C# 사전 계산 — 픽셀당 나눗셈 제거)
        [HideInInspector] _SoftMaskSoftnessRcp ("SoftMask Softness Rcp", Float) = 10
        _SoftMaskInvert     ("SoftMask Invert", Float) = 0
        _SoftMaskUVRect     ("SoftMask UV Rect", Vector) = (0, 0, 1, 1)

        // 슬라이스 마스크 파라미터 (Image.Type.Sliced 대응)
        [HideInInspector] _SoftMaskSliceBorder   ("SoftMask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SoftMaskSliceInnerUV  ("SoftMask Slice Inner UV", Vector) = (0, 0, 1, 1)
        // 구간별 기울기 (k1, k2n, k3, 타일수-ε) — C# 사전 계산 (픽셀당 나눗셈 제거)
        [HideInInspector] _SoftMaskSliceSlopeX   ("SoftMask Slice Slope X", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _SoftMaskSliceSlopeY   ("SoftMask Slice Slope Y", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _SoftMaskFillLineA     ("SoftMask Fill Line A", Vector) = (0, 0, 1, 0)
        [HideInInspector] _SoftMaskFillLineB     ("SoftMask Fill Line B", Vector) = (0, 0, 1, 10000)

        [HideInInspector] _SoftMaskTex2      ("SoftMask Texture 2", 2D) = "white" {}
        [HideInInspector] _SoftMaskSoftnessRcp2 ("SoftMask Softness Rcp 2", Float) = 10
        [HideInInspector] _SoftMaskInvert2    ("SoftMask Invert 2", Float) = 0
        [HideInInspector] _SoftMaskUVRect2    ("SoftMask UV Rect 2", Vector) = (0, 0, 1, 1)

        // 중첩 형태 대응 파라미터
        [HideInInspector] _SoftMaskSliceBorder2  ("SoftMask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SoftMaskSliceInnerUV2 ("SoftMask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _SoftMaskSliceSlopeX2  ("SoftMask Slice Slope X 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _SoftMaskSliceSlopeY2  ("SoftMask Slice Slope Y 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _SoftMaskFillLineA2    ("SoftMask Fill Line A 2", Vector) = (0, 0, 1, 0)
        [HideInInspector] _SoftMaskFillLineB2    ("SoftMask Fill Line B 2", Vector) = (0, 0, 1, 10000)
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
            // _SOFTMASK_SLICE 하나가 마스크 1·2의 슬라이스를 모두 담당 (변형 수 절감)
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE

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
            half _SoftMaskSoftnessRcp;  // = 1 / max(softness, 0.001), C# 사전 계산
            half _SoftMaskInvert;
            float4x4 _SoftMaskWorldToUV;
            float4 _SoftMaskUVRect;

            sampler2D _SoftMaskTex2;
            half _SoftMaskSoftnessRcp2;
            half _SoftMaskInvert2;
            float4x4 _SoftMaskWorldToUV2;
            float4 _SoftMaskUVRect2;

            // 형태 대응 유니폼 (마스크 1: 9-slice / Tiled / Filled)
            #if defined(_SOFTMASK_SLICE)
            float4 _SoftMaskSliceBorder;   // (leftBreak, bottomBreak, rightBreak, topBreak) rect 정규화 [0,1]
            float4 _SoftMaskSliceInnerUV;  // (innerLeft, innerBottom, innerRight, innerTop) 스프라이트 UV [0,1]
            float4 _SoftMaskSliceSlopeX;   // (k1, k2n, k3, 타일수-ε) — C# 사전 계산
            float4 _SoftMaskSliceSlopeY;
            float4 _SoftMaskFillLineA;     // Filled 반평면 A (a, b, c, 결합모드). 항등=(0,0,1,0)
            float4 _SoftMaskFillLineB;     // Filled 반평면 B (a, b, c, 0). 항등=(0,0,1,0)
            #endif

            // 형태 대응 유니폼 (마스크 2, 중첩)
            #if defined(_SOFTMASK_NESTED) && defined(_SOFTMASK_SLICE)
            float4 _SoftMaskSliceBorder2;
            float4 _SoftMaskSliceInnerUV2;
            float4 _SoftMaskSliceSlopeX2;
            float4 _SoftMaskSliceSlopeY2;
            float4 _SoftMaskFillLineA2;
            float4 _SoftMaskFillLineB2;
            #endif

            // ─────────────────────────────────────────
            // 9-슬라이스/타일 1D 리매핑 (브랜치·나눗셈 없음, 모바일 최적화)
            // k = (k1, k2n, k3, nMax) — C#이 역수/타일 수 사전 계산 (Core.cginc와 동일 알고리즘)
            // ─────────────────────────────────────────
            inline float SliceRemap1D_TMP(float u, float uA, float uB, float pA, float pB, float4 k)
            {
                float s1 = step(u, uA);
                float s3 = step(uB, u);
                float s2 = saturate(1.0 - s1 - s3); // uA==uB 경계점 음수 가중치 방지
                float r1 = u * k.x;
                float r2 = pA + frac(min((u - uA) * k.y, k.w)) * (pB - pA);
                float r3 = pB + (u - uB) * k.z;
                return s1 * r1 + s2 * r2 + s3 * r3;
            }

            // Filled 커버리지 (반평면 2개, atan2 없음 — Core.cginc와 동일 알고리즘)
            // lineB.w = AA 스케일 (saturate(d*aa+0.5)로 ~1px 소프트 경계)
            inline half FillCoverage_TMP(float2 uv, float4 lineA, float4 lineB)
            {
                half aa = (half)lineB.w;
                half a = saturate((half)dot(float3(uv, 1.0), lineA.xyz) * aa + 0.5h);
                half b = saturate((half)dot(float3(uv, 1.0), lineB.xyz) * aa + 0.5h);
                return lerp(a * b, max(a, b), (half)lineA.w);
            }

            // ─────────────────────────────────────────
            // SoftMask 샘플링 (half precision, 분기 없음)
            // Metal 백엔드 호환: 전역 유니폼 직접 접근
            // ─────────────────────────────────────────
            inline half SampleSoftMask1(float2 uv)
            {
                half inBounds = step(0.0h, uv.x) * step(uv.x, 1.0h)
                              * step(0.0h, uv.y) * step(uv.y, 1.0h);

                // 형태 대응: Filled 커버리지 + 9-slice/Tiled 리매핑
                #if defined(_SOFTMASK_SLICE)
                inBounds *= FillCoverage_TMP(uv, _SoftMaskFillLineA, _SoftMaskFillLineB);
                uv = float2(
                    SliceRemap1D_TMP(uv.x, _SoftMaskSliceBorder.x, _SoftMaskSliceBorder.z, _SoftMaskSliceInnerUV.x, _SoftMaskSliceInnerUV.z, _SoftMaskSliceSlopeX),
                    SliceRemap1D_TMP(uv.y, _SoftMaskSliceBorder.y, _SoftMaskSliceBorder.w, _SoftMaskSliceInnerUV.y, _SoftMaskSliceInnerUV.w, _SoftMaskSliceSlopeY)
                );
                #endif

                float2 atlasUV = _SoftMaskUVRect.xy + uv * _SoftMaskUVRect.zw;
                // rect 밖은 마스크 알파 0 취급 — 인버트 시 rect 밖이 표시되도록 반전 이전에 적용
                half maskAlpha = tex2D(_SoftMaskTex, atlasUV).a * inBounds;
                // smoothstep(0, softness, a)와 동일하되 역수 사전 계산으로 픽셀당 나눗셈 제거
                half t = saturate(maskAlpha * _SoftMaskSoftnessRcp);
                half softEdge = t * t * (3.0h - 2.0h * t);
                return lerp(softEdge, 1.0h - softEdge, _SoftMaskInvert);
            }

            #if defined(_SOFTMASK_NESTED)
            inline half SampleSoftMask2(float2 uv)
            {
                half inBounds = step(0.0h, uv.x) * step(uv.x, 1.0h)
                              * step(0.0h, uv.y) * step(uv.y, 1.0h);

                // 중첩 마스크 형태 대응: 마스크 1과 동일 키워드로 제어
                #if defined(_SOFTMASK_SLICE)
                inBounds *= FillCoverage_TMP(uv, _SoftMaskFillLineA2, _SoftMaskFillLineB2);
                uv = float2(
                    SliceRemap1D_TMP(uv.x, _SoftMaskSliceBorder2.x, _SoftMaskSliceBorder2.z, _SoftMaskSliceInnerUV2.x, _SoftMaskSliceInnerUV2.z, _SoftMaskSliceSlopeX2),
                    SliceRemap1D_TMP(uv.y, _SoftMaskSliceBorder2.y, _SoftMaskSliceBorder2.w, _SoftMaskSliceInnerUV2.y, _SoftMaskSliceInnerUV2.w, _SoftMaskSliceSlopeY2)
                );
                #endif

                float2 atlasUV = _SoftMaskUVRect2.xy + uv * _SoftMaskUVRect2.zw;
                half maskAlpha = tex2D(_SoftMaskTex2, atlasUV).a * inBounds;
                half t = saturate(maskAlpha * _SoftMaskSoftnessRcp2);
                half softEdge = t * t * (3.0h - 2.0h * t);
                return lerp(softEdge, 1.0h - softEdge, _SoftMaskInvert2);
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

                // SoftMask UV (Canvas 로컬 좌표 → 마스크 UV, 버텍스에서 계산)
                // vert는 Canvas 로컬 좌표 (unity_ObjectToWorld 미사용 → Overlay 호환)
                output.softMaskUV = mul(_SoftMaskWorldToUV, float4(vert.xyz, 1)).xy;
                #if defined(_SOFTMASK_NESTED)
                output.softMaskUV2 = mul(_SoftMaskWorldToUV2, float4(vert.xyz, 1)).xy;
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
