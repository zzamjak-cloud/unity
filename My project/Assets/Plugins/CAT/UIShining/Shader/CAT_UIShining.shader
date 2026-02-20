Shader "CAT/Effects/UIShining"
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
        [PerRendererData] _Softness("Softness", Range(0, 1)) = 0
        [PerRendererData] _BurnBias("Burn Bias (bright = strong)", Range(0, 1)) = 0.85
        [PerRendererData] _BlendStrength("Blend Strength", Range(0.5, 2.5)) = 1.45
        [HideInInspector] _SpriteUVRect("Sprite UV Rect", Vector) = (0, 0, 1, 1)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15
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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex    : POSITION;
                half4 color      : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 texcoord2 : TEXCOORD2;
                float2 texcoord3 : TEXCOORD3;
                float4 tangent   : TANGENT;   // RectTransform 로컬 좌표 (0~1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                half4 color          : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 spriteUVRect  : TEXCOORD2;
                float2 localPos      : TEXCOORD3; // RectTransform 로컬 좌표 (0~1)
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
            half _Softness;
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
                OUT.localPos = v.tangent.xy; // RectTransform 로컬 좌표 (Sliced 이미지 대응)
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
                // IN.localPos는 버텍스 셰이더에서 전달된 0~1 정규화 좌표
                half2 localPos = IN.localPos;

                // Fallback: ModifyMesh가 호출되지 않았거나 tangent가 0인 경우 텍스처 UV 사용
                if (localPos.x <= 0.001h && localPos.y <= 0.001h)
                {
                    // 스프라이트 UV 기준 정규화 (기존 방식)
                    half4 spriteRect = IN.spriteUVRect;
                    if (spriteRect.z <= spriteRect.x || spriteRect.w <= spriteRect.y)
                        spriteRect = _SpriteUVRect;
                    half2 spriteMin = spriteRect.xy;
                    half2 spriteSize = spriteRect.zw - spriteRect.xy;
                    spriteSize = max(spriteSize, half2(1e-5h, 1e-5h));
                    localPos = (IN.texcoord - spriteMin) / spriteSize;
                }

                // 로컬 좌표를 _Angle만큼 회전: progress 방향이 ay (angle=0 일 때 u 방향)
                half rad = _Angle * 0.0174532925h; // deg to rad
                half cosA = cos(rad);
                half sinA = sin(rad);
                half ax = -localPos.x * sinA + localPos.y * cosA;
                half ay = localPos.x * cosA + localPos.y * sinA;

                // progress 0~1을 이미지 밖 시작/끝으로 확장 (0=완전히 밖, 1=완전히 밖)
                half effectiveProgress = -_ProgressOffset + (1.0h + 2.0h * _ProgressOffset) * _Progress;
                // Duration 진행에 따라 휘어짐 보간: 시작 -> 종료
                half curvature = lerp(_CurvatureStart, _CurvatureEnd, _Progress);
                // 휘어진 밴드 중심선: ay_center = effectiveProgress + curvature * (ax - 0.5)^2
                half ayCenter = effectiveProgress + curvature * (ax - 0.5h) * (ax - 0.5h);
                half d = abs(ay - ayCenter);

                // Duration 진행에 따라 밴드 두께 보간: 시작 -> 종료
                half width = lerp(_WidthStart, _WidthEnd, _Progress);
                // falloff: 0=선형 경계(smoothstep), 1=소프트 블러/롱 테일(가우시안)
                half falloffSharp = 1.0h - smoothstep(0.0h, width, d);
                half sigma = max(width * 0.6h, 1e-5h);
                half falloffSoft = exp(-(d * d) / (2.0h * sigma * sigma));
                half falloff = lerp(falloffSharp, falloffSoft, _Softness);
                // 밝은 픽셀에서만 강한 Additive(Burn), 어두운 픽셀은 미미하게
                half lum = dot(base.rgb, half3(0.299h, 0.587h, 0.114h));
                half burnFactor = lerp(lerp(0.12h, 1.0h, pow(saturate(lum), 0.6h)), 1.0h, 1.0h - _BurnBias);
                half3 shineAdd = _ShineColor.rgb * _Intensity * falloff * burnFactor * base.a * _BlendStrength;

                return half4(base.rgb + shineAdd, base.a);
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
