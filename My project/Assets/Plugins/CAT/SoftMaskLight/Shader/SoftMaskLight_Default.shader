Shader "SoftMaskLight/UI/Default"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        // 마스크 파라미터
        _MaskTex("Mask Texture", 2D) = "white" {}
        // 소프트니스 역수 (= 1 / max(softness, 0.001), C# 사전 계산 — 픽셀당 나눗셈 제거)
        [HideInInspector] _SoftnessRcp("Softness Rcp", Float) = 10
        _InvertMask("Invert Mask", Float) = 0
        _MaskUVRect("Mask UV Rect", Vector) = (0, 0, 1, 1)

        // 형태 대응 파라미터 (Sliced / Tiled / Filled — _SOFTMASK_SLICE)
        [HideInInspector] _MaskSliceBorder("Mask Slice Border", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV("Mask Slice Inner UV", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX("Mask Slice Slope X", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY("Mask Slice Slope Y", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA("Mask Fill Line A", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB("Mask Fill Line B", Vector) = (0, 0, 1, 10000)

        // 중첩 마스크 파라미터
        [HideInInspector] _MaskTex2("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _SoftnessRcp2("Softness Rcp 2", Float) = 10
        [HideInInspector] _InvertMask2("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2("Mask UV Rect 2", Vector) = (0, 0, 1, 1)

        // 중첩 형태 대응 파라미터
        [HideInInspector] _MaskSliceBorder2("Mask Slice Border 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceInnerUV2("Mask Slice Inner UV 2", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskSliceSlopeX2("Mask Slice Slope X 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskSliceSlopeY2("Mask Slice Slope Y 2", Vector) = (0, 1, 0, 0.9999)
        [HideInInspector] _MaskFillLineA2("Mask Fill Line A 2", Vector) = (0, 0, 1, 0)
        [HideInInspector] _MaskFillLineB2("Mask Fill Line B 2", Vector) = (0, 0, 1, 10000)

        // UI 스텐실/마스크 설정
        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15

        // RectMask2D: 기본값을 넓게 두어 미주입 시 전체가 사라지지 않게 함
        [HideInInspector] _ClipRect("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    // 마스크 유니폼/샘플링은 Core에서 공용 사용 (구현 3중 복제 제거)
    #define _CAT_SOFTMASK 1
    #include "SoftMaskLight_Core.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    ENDCG

    // SubShader 0: UI
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
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma target 2.0

            #include "UnityUI.cginc"
            #include "SoftMaskLight_UIClip.cginc"

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
                CAT_UI_CLIP_COORDS(1)
                CAT_SOFTMASK_COORDS(2, 3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uiClipMask = CAT_UI_ComputeClipMask(OUT.vertex, v.vertex.xy);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;

                // Canvas 로컬 좌표 → 마스크 UV (버텍스에서 계산, 프래그먼트 비용 절감)
                // v.vertex는 Canvas 로컬 좌표 (unity_ObjectToWorld 미사용 → Overlay 호환)
                CAT_SOFTMASK_VERT(v.vertex.xyz, OUT)

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                CAT_UI_ApplyClipRect(color, IN.uiClipMask);

                color.a *= CAT_SOFTMASK_FRAG(IN);

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDCG
        }
    }

    // SubShader 1: Sprite
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            // 동적 배칭 시 v.vertex가 사전 월드 변환되어 마스크 UV가 어긋나는 것을 방지
            "DisableBatching" = "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        ColorMask[_ColorMask]
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE
            #pragma target 2.0

            struct appdata_t
            {
                float4 vertex   : POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                half4 color     : COLOR;
                float2 texcoord : TEXCOORD0;
                CAT_SOFTMASK_COORDS(1, 2)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;

                // v.vertex는 Canvas 로컬 좌표 (unity_ObjectToWorld 미사용 → Overlay 호환)
                CAT_SOFTMASK_VERT(IN.vertex.xyz, OUT)

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                color.a *= CAT_SOFTMASK_FRAG(IN);

                return color;
            }
            ENDCG
        }
    }

    Fallback "UI/Default"
}
