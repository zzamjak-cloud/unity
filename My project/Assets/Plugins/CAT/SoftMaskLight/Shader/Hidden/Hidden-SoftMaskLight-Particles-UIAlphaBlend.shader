// [OptionalShader] SoftMaskLight: SoftMaskLight/Particles/UIAlphaBlend
// 파티클 AlphaBlend 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
// 원본: SoftMaskLight_UIAlphaBlend.shader
Shader "Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskLight)"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

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

    Category
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // AlphaBlend 블렌딩
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        SubShader
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 2.0
                #pragma multi_compile_particles
                #pragma multi_compile_fog
                #pragma multi_compile __ UNITY_UI_ALPHACLIP
                // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
                #pragma multi_compile_local _ _SOFTMASK_NESTED
                #pragma multi_compile_local _ _SOFTMASK_SLICE
                #pragma multi_compile_local _ _SOFTMASK_NESTED_SLICE

                // SoftMaskLight 항상 활성화
                #define _CAT_SOFTMASK 1

                #include "UnityCG.cginc"
                #include "UnityUI.cginc"
                #include "../SoftMaskLight_Core.cginc"

                sampler2D _MainTex;
                fixed4 _TintColor;

                struct appdata_t
                {
                    float4 vertex   : POSITION;
                    fixed4 color    : COLOR;
                    float2 texcoord : TEXCOORD0;
                };

                struct v2f
                {
                    float4 vertex   : SV_POSITION;
                    fixed4 color    : COLOR;
                    float2 texcoord : TEXCOORD0;
                    UNITY_FOG_COORDS(1)
                    #ifdef SOFTPARTICLES_ON
                    float4 projPos  : TEXCOORD2;
                    #endif
                    CAT_SOFTMASK_COORDS(3, 4)
                };

                float4 _MainTex_ST;

                v2f vert(appdata_t IN)
                {
                    v2f v;
                    v.vertex = UnityObjectToClipPos(IN.vertex);
                    #ifdef SOFTPARTICLES_ON
                    v.projPos = ComputeScreenPos(v.vertex);
                    COMPUTE_EYEDEPTH(v.projPos.z);
                    #endif
                    v.color = IN.color;
                    v.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                    UNITY_TRANSFER_FOG(v, v.vertex);

                    // SoftMaskLight 버텍스 처리 (항상 활성)
                    // IN.vertex는 Canvas 로컬 좌표 (unity_ObjectToWorld 미사용 -> Overlay 호환)
                    CAT_SOFTMASK_VERT(IN.vertex.xyz, v)

                    return v;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    fixed4 col = i.color * _TintColor * tex2D(_MainTex, i.texcoord);

                    // SoftMaskLight 적용 (alpha blend: 알파만 곱)
                    col.a *= CAT_SOFTMASK_FRAG(i);

                    return col;
                }
                ENDCG
            }
        }
    }
}
