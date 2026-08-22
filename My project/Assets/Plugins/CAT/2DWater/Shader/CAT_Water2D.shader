// 2D 물 표현용 URP 언릿 셰이더.
// 모바일 우선: 텍스처 샘플 최대 1회, 나머지는 전부 절차적(ALU) 처리이며
// 기능별 shader_feature 로 사용하지 않는 연산은 컴파일 단계에서 제거된다.
//
// UV 규약 (Water2D 메시): u = 좌→우(0~1), v = 하단 0 → 수면 1.
Shader "CAT/Effects/2D Water"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color (수면)", Color) = (0.45, 0.85, 1.0, 0.65)
        _DeepColor ("Deep Color (심층)", Color) = (0.03, 0.25, 0.5, 0.9)
        _GradientPower ("Gradient Power", Range(0.1, 6)) = 1.4
        _Alpha ("Alpha 배율", Range(0, 1)) = 1

        [Header(Texture)]
        [Toggle(_CAT_TEXTURE)] _TextureEnabled ("질감 텍스처 사용", Float) = 0
        [NoScaleOffset] _MainTex ("질감 텍스처", 2D) = "white" {}
        _TexTint ("질감 색조", Color) = (1, 1, 1, 1)
        _TexStrength ("질감 세기", Range(0, 1)) = 0.35
        _TexTiling ("질감 타일링 (XY)", Vector) = (2, 1, 0, 0)
        _TexScroll ("질감 스크롤 (XY/초)", Vector) = (0.06, 0.01, 0, 0)

        [Header(Caustics)]
        [Toggle(_CAT_CAUSTICS)] _CausticsEnabled ("물결 무늬(코스틱) 사용", Float) = 1
        _CausticsColor ("코스틱 색", Color) = (0.7, 0.95, 1.0, 1)
        _CausticsStrength ("코스틱 세기", Range(0, 2)) = 0.55
        _CausticsScale ("코스틱 밀도", Range(0.5, 40)) = 12
        _CausticsSpeed ("코스틱 속도", Range(0, 5)) = 0.6
        _CausticsSharpness ("코스틱 선명도", Range(1, 16)) = 4
        _CausticsDepthBias ("깊이 감쇠 (0=균일, 1=수면집중)", Range(0, 1)) = 0.5

        [Header(Distortion)]
        [Toggle(_CAT_DISTORT)] _DistortEnabled ("굴절 왜곡 사용", Float) = 1
        _DistortStrength ("왜곡 세기", Range(0, 0.3)) = 0.03
        _DistortScale ("왜곡 밀도", Range(0.5, 30)) = 6
        _DistortSpeed ("왜곡 속도", Range(0, 5)) = 0.8

        [Header(Foam)]
        [Toggle(_CAT_FOAM)] _FoamEnabled ("수면 거품 사용", Float) = 1
        _FoamColor ("거품 색", Color) = (1, 1, 1, 0.8)
        _FoamThickness ("거품 두께 (0~1 UV)", Range(0, 0.5)) = 0.04
        _FoamSoftness ("거품 경계 부드러움", Range(0.001, 0.5)) = 0.05

        [Header(Depth Fade)]
        _BottomFade ("하단 페이드", Range(0, 1)) = 0
        _EdgeFade ("좌우 페이드", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // SRP Batcher 호환: 모든 머티리얼 프로퍼티를 UnityPerMaterial 로 묶는다.
        CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor;
            float4 _DeepColor;
            float _GradientPower;
            float _Alpha;

            float4 _TexTint;
            float _TexStrength;
            float4 _TexTiling;
            float4 _TexScroll;

            float4 _CausticsColor;
            float _CausticsStrength;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsSharpness;
            float _CausticsDepthBias;

            float _DistortStrength;
            float _DistortScale;
            float _DistortSpeed;

            float4 _FoamColor;
            float _FoamThickness;
            float _FoamSoftness;

            float _BottomFade;
            float _EdgeFade;

            // Toggle 프로퍼티도 CBUFFER 에 포함해야 SRP Batcher 가 깨지지 않는다.
            float _TextureEnabled;
            float _CausticsEnabled;
            float _DistortEnabled;
            float _FoamEnabled;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings WaterVert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        half4 WaterFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = input.uv;
            float t = _Time.y;

            // ── 굴절 왜곡: 절차적 sin/cos 2회. 씬 텍스처(GrabPass) 미사용으로 모바일 안전.
            float2 patternUV = uv;
        #if defined(_CAT_DISTORT)
            float2 wobble;
            wobble.x = sin(uv.y * _DistortScale + t * _DistortSpeed);
            wobble.y = cos(uv.x * _DistortScale * 0.9 - t * _DistortSpeed * 1.1);
            patternUV += wobble * _DistortStrength;
        #endif

            // ── 깊이 그라디언트: v=1(수면) → Shallow, v=0(하단) → Deep
            half depthT = pow(saturate(uv.y), _GradientPower);
            half4 col = lerp(_DeepColor, _ShallowColor, depthT);

            // ── 질감 텍스처 (샘플 1회, 스크롤 포함)
        #if defined(_CAT_TEXTURE)
            float2 texUV = patternUV * _TexTiling.xy + _TexScroll.xy * t;
            half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texUV);
            half3 modulated = col.rgb * tex.rgb * _TexTint.rgb;
            col.rgb = lerp(col.rgb, modulated, _TexStrength * tex.a);
        #endif

            // ── 코스틱(물결 무늬): sin 교차 패턴. 노이즈 텍스처 없이 표현.
        #if defined(_CAT_CAUSTICS)
            float2 cp = patternUV * _CausticsScale;
            float ct = t * _CausticsSpeed;
            half c = sin(cp.x + sin(cp.y * 1.7 + ct)) * sin(cp.y - sin(cp.x * 1.3 - ct * 0.8));
            c = saturate(c * 0.5 + 0.5);
            c = pow(c, _CausticsSharpness);
            // 깊이 감쇠: Bias 가 1 이면 수면 근처만 밝아진다.
            half causticMask = lerp(1.0, depthT, _CausticsDepthBias);
            col.rgb += _CausticsColor.rgb * (c * _CausticsStrength * causticMask * _CausticsColor.a);
        #endif

            // ── 수면 거품 라인
        #if defined(_CAT_FOAM)
            half distFromSurface = 1.0 - uv.y;
            half foam = 1.0 - smoothstep(_FoamThickness, _FoamThickness + _FoamSoftness, distFromSurface);
            foam *= _FoamColor.a;
            col.rgb = lerp(col.rgb, _FoamColor.rgb, foam);
            col.a = max(col.a, foam);
        #endif

            // ── 경계 페이드 (배경과 자연스럽게 섞이도록)
            // 분기 없이 처리: 페이드 값이 0 이면 max(1e-4) 로 smoothstep 이 즉시 1 이 되어 무효화된다.
            half bottomEdge = max(1e-4, _BottomFade);
            half sideEdge = max(1e-4, _EdgeFade);
            half fade = smoothstep(0.0, bottomEdge, uv.y)
                      * smoothstep(0.0, sideEdge, uv.x)
                      * smoothstep(0.0, sideEdge, 1.0 - uv.x);

            col.a = saturate(col.a * fade * _Alpha);
            return col;
        }
        ENDHLSL

        // URP 2D Renderer 경로
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #pragma shader_feature_local_fragment _CAT_TEXTURE
            #pragma shader_feature_local_fragment _CAT_CAUSTICS
            #pragma shader_feature_local_fragment _CAT_DISTORT
            #pragma shader_feature_local_fragment _CAT_FOAM
            ENDHLSL
        }

        // URP Forward Renderer(3D) 로 전환된 프로젝트 대응
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex WaterVert
            #pragma fragment WaterFrag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #pragma shader_feature_local_fragment _CAT_TEXTURE
            #pragma shader_feature_local_fragment _CAT_CAUSTICS
            #pragma shader_feature_local_fragment _CAT_DISTORT
            #pragma shader_feature_local_fragment _CAT_FOAM
            ENDHLSL
        }
    }

    Fallback Off
}
