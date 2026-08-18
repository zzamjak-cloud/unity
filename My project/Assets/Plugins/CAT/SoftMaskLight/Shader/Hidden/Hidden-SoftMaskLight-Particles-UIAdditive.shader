// [OptionalShader] SoftMaskLight: CAT/VFX/UIAdditive
// 파티클 Additive 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
// 원본: com.zzamjak.vfxmaker 패키지의 CAT_UIAdditive.shader (Shader "CAT/VFX/UIAdditive")
// 블렌드 모드는 반드시 원본과 동일하게 유지할 것 (다르면 마스크 적용 시 밝기가 달라짐)
Shader "Hidden/CAT/VFX/UIAdditive (SoftMaskLight)"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        // RectMask2D: 기본값을 넓게 두어 미주입 시 전체가 사라지지 않게 함
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)

        // SoftMaskLight 프로퍼티
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
        Fog { Mode Off }

        // 원본(CAT/VFX/UIAdditive)과 동일한 블렌드 유지 — 마스크 적용 전후 밝기 일치 보장
        Blend SrcAlpha One, One One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ _SOFTMASK_NESTED
            #pragma multi_compile_local _ _SOFTMASK_SLICE

            // SoftMaskLight 항상 활성화
            #define _CAT_SOFTMASK 1
            #include "../SoftMaskLight_Core.cginc"

            #include "UnityCG.cginc"
            #include "../SoftMaskLight_UIClip.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                half2 texcoord  : TEXCOORD0;
                CAT_SOFTMASK_COORDS(1, 2)
                CAT_UI_CLIP_COORDS(3)
            };

            fixed4 _Color;
            sampler2D _MainTex;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
                #endif
                OUT.color = IN.color * _Color;

                // SoftMaskLight 버텍스 처리 (항상 활성)
                CAT_SOFTMASK_VERT(IN.vertex.xyz, OUT)

                // RectMask2D 클리핑 좌표 (UnityObjectToClipPos 이후 값 사용)
                OUT.uiClipMask = CAT_UI_ComputeClipMask(OUT.vertex, IN.vertex.xy);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                color.rgb *= color.a;

                // Blend SrcAlpha One 경로에서 가시 기여는 rgb*a² 이므로
                // 알파에만 마스크를 곱해야 마스크가 정확히 1회 적용된다 (RGB에도 곱하면 m²)
                color.a *= CAT_SOFTMASK_FRAG(IN);

                // RectMask2D 클리핑 (동일 이유로 알파에만 적용)
                color.a *= CAT_UI_ClipFactor(IN.uiClipMask);

                clip(color.a - 0.01);
                return color;
            }
            ENDCG
        }
    }
}
