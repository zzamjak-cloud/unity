// 카메라 Depth / DepthNormals 텍스처를 이용한 스크린 스페이스 아웃라인.
// ScriptableRendererFeature(CATToonOutlineFeature) 가 풀스크린 삼각형으로 실행한다.
Shader "CAT/Toon/ScreenSpaceOutline"
{
    Properties
    {
        [HideInInspector] _OutlineColor ("Outline Color", Color) = (0.08, 0.07, 0.12, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

        float4 _OutlineColor;
        float4 _OutlineTexelSize;   // xy = 1/해상도, zw = 해상도
        float  _OutlineThickness;
        float  _DepthThreshold;
        float  _DepthSmooth;
        float  _NormalThreshold;
        float  _NormalSmooth;
        float  _GrazingSuppress;
        float  _FadeStart;
        float  _FadeEnd;
        float  _SketchJitter;
        float  _SketchFrequency;
        float  _UseNormalEdge;

        float CAT_Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float CAT_LinearDepth(float2 uv)
        {
            return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
        }

        // 로버츠 크로스 4샘플 오프셋을 만든다. 손그림 느낌을 위해 노이즈로 살짝 흔든다.
        void CAT_BuildOffsets(float2 uv, out float2 uv0, out float2 uv1, out float2 uv2, out float2 uv3)
        {
            float2 texel = _OutlineTexelSize.xy * max(_OutlineThickness, 0.0);

            if (_SketchJitter > 0.0)
            {
                // 시간을 계단식으로 양자화해 초당 몇 프레임만 라인이 흔들리게 한다.
                float t = floor(_Time.y * max(_SketchFrequency, 1.0));
                float2 seed = uv * _OutlineTexelSize.zw + t;
                float2 n = float2(CAT_Hash21(seed), CAT_Hash21(seed + 17.7)) - 0.5;
                uv += n * _OutlineTexelSize.xy * _SketchJitter * 2.0;
            }

            uv0 = uv + float2(-texel.x, -texel.y);
            uv1 = uv + float2( texel.x,  texel.y);
            uv2 = uv + float2(-texel.x,  texel.y);
            uv3 = uv + float2( texel.x, -texel.y);
        }

        // 0 = 엣지 없음, 1 = 완전한 엣지
        float CAT_EdgeFactor(float2 uv, out float centerDepth)
        {
            float2 uv0, uv1, uv2, uv3;
            CAT_BuildOffsets(uv, uv0, uv1, uv2, uv3);

            centerDepth = CAT_LinearDepth(uv);

            // --- 깊이 엣지 -------------------------------------------------
            float d0 = CAT_LinearDepth(uv0);
            float d1 = CAT_LinearDepth(uv1);
            float d2 = CAT_LinearDepth(uv2);
            float d3 = CAT_LinearDepth(uv3);

            float2 depthDelta = float2(d1 - d0, d3 - d2);
            // 카메라에서 멀수록 같은 각도 차이도 깊이 차가 커지므로 중심 깊이로 나눠 정규화한다.
            float  depthDiff  = length(depthDelta) / max(centerDepth, 1e-4);

            // 시선에 거의 평행한 면(바닥 등)에서 생기는 가짜 엣지를 억제한다.
            float3 nWS     = SampleSceneNormals(uv);
            float3 nVS     = TransformWorldToViewDir(nWS, true);
            float  facing  = saturate(abs(nVS.z));
            float  slope   = lerp(1.0, rcp(max(facing, 0.05)), saturate(_GrazingSuppress));

            float depthThreshold = _DepthThreshold * slope;
            float depthEdge = smoothstep(depthThreshold, depthThreshold + max(_DepthSmooth, 1e-4), depthDiff);

            // --- 노멀 엣지 (내부 크리스) -----------------------------------
            float normalEdge = 0.0;
            if (_UseNormalEdge > 0.5)
            {
                float3 n0 = SampleSceneNormals(uv0);
                float3 n1 = SampleSceneNormals(uv1);
                float3 n2 = SampleSceneNormals(uv2);
                float3 n3 = SampleSceneNormals(uv3);

                float3 dn1 = n1 - n0;
                float3 dn2 = n3 - n2;
                float  normalDiff = sqrt(dot(dn1, dn1) + dot(dn2, dn2));

                normalEdge = smoothstep(_NormalThreshold, _NormalThreshold + max(_NormalSmooth, 1e-4), normalDiff);

                // 깊이가 크게 튀는 실루엣 경계에서는 노멀 차이가 무의미하므로 깊이 엣지를 우선한다.
                normalEdge *= 1.0 - saturate(depthEdge);
            }

            return saturate(max(depthEdge, normalEdge));
        }

        // 원거리에서 아웃라인이 지저분해지지 않도록 페이드 아웃한다.
        float CAT_DistanceFade(float centerDepth)
        {
            return 1.0 - smoothstep(_FadeStart, max(_FadeEnd, _FadeStart + 1e-3), centerDepth);
        }
        ENDHLSL

        // ===================================================================
        // Pass 0 — 단색 아웃라인 (알파 블렌드)
        // ===================================================================
        Pass
        {
            Name "ToonOutlineSolid"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragSolid
            #pragma target 3.0

            half4 FragSolid(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float centerDepth;
                float edge = CAT_EdgeFactor(input.texcoord, centerDepth);
                edge *= CAT_DistanceFade(centerDepth);

                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }

        // ===================================================================
        // Pass 1 — 곱연산 아웃라인. 씬 컬러를 어둡게 물들여 잉크 느낌을 낸다.
        // ===================================================================
        Pass
        {
            Name "ToonOutlineMultiply"
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMultiply
            #pragma target 3.0

            half4 FragMultiply(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float centerDepth;
                float edge = CAT_EdgeFactor(input.texcoord, centerDepth);
                edge *= CAT_DistanceFade(centerDepth) * _OutlineColor.a;

                half3 tint = lerp(half3(1.0h, 1.0h, 1.0h), _OutlineColor.rgb, edge);
                return half4(tint, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
