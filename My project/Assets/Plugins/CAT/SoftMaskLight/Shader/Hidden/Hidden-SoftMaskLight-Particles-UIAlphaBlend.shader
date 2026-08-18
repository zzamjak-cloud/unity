// [OptionalShader] SoftMaskLight: CAT/VFX/UIAlphaBlend
// 파티클 AlphaBlend 셰이더에 SoftMaskLight 마스킹을 항상 활성화한 Hidden 변형
// 원본: com.zzamjak.vfxmaker 패키지의 CAT_UIAlphaBlend.shader (Shader "CAT/VFX/UIAlphaBlend")
// 블렌드 모드는 반드시 원본과 동일하게 유지할 것
Shader "Hidden/CAT/VFX/UIAlphaBlend (SoftMaskLight)"
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
                // 미사용 키워드 제거: multi_compile_particles(soft particle 미구현),
                // multi_compile_fog(UNITY_APPLY_FOG 미호출), UNITY_UI_ALPHACLIP(clip 미호출)
                // _CAT_SOFTMASK multi_compile 없음 (항상 활성)
                #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
                #pragma multi_compile_local _ _SOFTMASK_NESTED
                #pragma multi_compile_local _ _SOFTMASK_SLICE

                // SoftMaskLight 항상 활성화
                #define _CAT_SOFTMASK 1

                #include "UnityCG.cginc"
                #include "UnityUI.cginc"
                #include "../SoftMaskLight_Core.cginc"
                #include "../SoftMaskLight_UIClip.cginc"

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
                    CAT_SOFTMASK_COORDS(1, 2)
                    CAT_UI_CLIP_COORDS(3)
                };

                float4 _MainTex_ST;

                v2f vert(appdata_t IN)
                {
                    v2f v;
                    v.vertex = UnityObjectToClipPos(IN.vertex);
                    v.color = IN.color;
                    v.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);

                    // RectMask2D 클리핑 좌표
                    v.uiClipMask = CAT_UI_ComputeClipMask(v.vertex, IN.vertex.xy);

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

                    // RectMask2D 클리핑 (straight alpha이므로 알파에만 적용)
                    col.a *= CAT_UI_ClipFactor(i.uiClipMask);

                    return col;
                }
                ENDCG
            }
        }
    }
}
