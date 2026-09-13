// UGUI는 URP에서도 빌트인 UI 셰이더 경로(CanvasRenderer)를 그대로 사용하므로
// UI/Default와 동일하게 CGPROGRAM + UnityUI.cginc 기반을 유지한다
Shader "CAT/UI/VerticalFlipUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SecondTex ("Second Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 초 단위 경과 시간이 들어온다. Range로 선언하면 인스펙터 조작 시 1로 잘린다
        _FlipProgress ("Flip Progress (seconds)", Float) = 0
        _SliceCount ("Slice Count", Range(1, 50)) = 6
        _FlipDuration ("Flip Duration", Range(0.1, 2.0)) = 0.2
        _FlipOffset ("Flip Offset", Range(0, 1)) = 0.1

        // 라인 관련 속성
        [Toggle] _ShowLines ("Show Column Lines", Float) = 1
        _LineColor ("Line Color", Color) = (0, 0, 0, 1)
        _LineWidth ("Line Width (UV)", Range(0.0005, 0.02)) = 0.004

        // 아틀라스 대응: 스프라이트가 텍스처에서 차지하는 영역 (xy=시작, zw=크기)
        _MainTexRect ("Main Sprite Rect", Vector) = (0, 0, 1, 1)
        _SecondTexRect ("Second Sprite Rect", Vector) = (0, 0, 1, 1)

        // UGUI 마스크에 필요한 속성들
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.5

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local_fragment _ _SHOWLINES_ON

            #define CAT_PI      3.14159265
            #define CAT_HALF_PI 1.57079633

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _SecondTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _MainTexRect;
            float4 _SecondTexRect;

            float _FlipProgress;
            float _SliceCount;
            float _FlipDuration;
            float _FlipOffset;

            // 라인 관련 변수
            float _ShowLines;
            float4 _LineColor;
            float _LineWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                // 아틀라스 UV를 그대로 보존해야 하므로 TRANSFORM_TEX를 적용하지 않는다
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 아틀라스 UV -> 스프라이트 로컬 0~1 UV
                float2 local = (IN.texcoord - _MainTexRect.xy) / max(_MainTexRect.zw, 1e-6);

                // 슬라이스마다 시간차를 두고 진행.
                // sliceWidth로 나누는 대신 _SliceCount를 곱해 픽셀당 나눗셈을 줄인다
                float sliceWidth = 1.0 / _SliceCount;
                float scaled = local.x * _SliceCount;
                float sliceIndex = min(floor(scaled), _SliceCount - 1.0);
                float inSlice = scaled - sliceIndex; // 슬라이스 내부 0~1 좌표
                float progress = saturate((_FlipProgress - sliceIndex * _FlipOffset) / _FlipDuration);

                // 0~pi. pi/2에서 폭이 0이 되며 뒷면으로 넘어간다
                float flipAngle = progress * CAT_PI;
                float halfAngle = min(flipAngle, CAT_PI - flipAngle);
                float stretch   = max(cos(halfAngle), 1e-4);
                float invStretch = 1.0 / stretch;

                // 각 슬라이스는 이미지 중심이 아니라 자기 세로축을 중심으로 회전한다
                float adjInSlice = (inSlice - 0.5) * invStretch + 0.5;
                float2 adjusted = float2((sliceIndex + adjInSlice) * sliceWidth, local.y);

                // 슬라이스 경계에서 adjusted는 불연속이라 자동 미분이 튄다.
                // 연속인 local의 미분을 압축률로 보정해 쓰면 분기 안에서도 밉이 정확하다
                float2 dLocalDx = ddx(local);
                float2 dLocalDy = ddy(local);
                float2 gradX = float2(dLocalDx.x * invStretch, dLocalDx.y);
                float2 gradY = float2(dLocalDy.x * invStretch, dLocalDy.y);

                // 앞/뒤 중 실제로 보이는 면만 샘플링한다
                fixed4 color;
                UNITY_BRANCH
                if (flipAngle < CAT_HALF_PI)
                {
                    float2 uv = _MainTexRect.xy + adjusted * _MainTexRect.zw;
                    color = tex2Dgrad(_MainTex, uv, gradX * _MainTexRect.zw, gradY * _MainTexRect.zw) + _TextureSampleAdd;
                }
                else
                {
                    float2 uv = _SecondTexRect.xy + adjusted * _SecondTexRect.zw;
                    color = tex2Dgrad(_SecondTex, uv, gradX * _SecondTexRect.zw, gradY * _SecondTexRect.zw) + _TextureSampleAdd;
                }

                color *= IN.color;

                // 회전으로 슬라이스 밖을 벗어난 영역은 비운다
                color.a *= step(0.0, adjInSlice) * step(adjInSlice, 1.0);

                #ifdef _SHOWLINES_ON
                {
                    // 슬라이스 경계까지의 거리를 UV 단위로 환산해 슬라이스 개수와 무관한 두께를 만든다
                    float boundary = round(scaled);
                    float distToBoundary = abs(scaled - boundary) * sliceWidth;

                    // 픽셀 폭보다 얇은 라인도 사라지지 않도록 화면 미분으로 정규화한다
                    float aa = max(abs(dLocalDx.x) + abs(dLocalDy.x), 1e-5);
                    float lineMask = saturate(0.5 - (distToBoundary - _LineWidth * 0.5) / aa);

                    // 바깥 테두리(0번, n번)는 슬라이스 경계가 아니므로 제외한다
                    lineMask *= step(0.5, boundary) * step(boundary, _SliceCount - 0.5);

                    // 알파는 콘텐츠를 따라가게 두어 카드가 없는 영역에는 라인이 남지 않는다
                    color.rgb = lerp(color.rgb, _LineColor.rgb, lineMask * _LineColor.a);
                }
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }

    // 폴백 셰이더 - 렌더링 문제 시 기본 UI 셰이더 사용
    Fallback "UI/Default"
}
