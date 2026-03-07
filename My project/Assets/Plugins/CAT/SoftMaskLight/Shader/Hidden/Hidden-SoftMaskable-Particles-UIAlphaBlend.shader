// [OptionalShader] com.coffee.softmask-for-ugui: SoftMaskLight/Particles/UIAlphaBlend
// mob-sakai SoftMask 대응 파티클 AlphaBlend 셰이더
Shader "Hidden/SoftMaskLight/Particles/UIAlphaBlend (SoftMaskable)"
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

                #include "UnityCG.cginc"
                #include "UnityUI.cginc"

                // ==== SOFTMASKABLE START (mob-sakai SoftMaskable) ====
                #pragma shader_feature _ SOFTMASK_EDITOR
                #pragma shader_feature_local_fragment _ SOFTMASKABLE
                #if SOFTMASKABLE
                #include "Packages/com.coffee.softmask-for-ugui/Shaders/SoftMask.cginc"
                #endif
                // ==== SOFTMASKABLE END ====

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
                    float4 vertex        : SV_POSITION;
                    fixed4 color         : COLOR;
                    float2 texcoord      : TEXCOORD0;
                    float4 worldPosition : TEXCOORD1;
                    UNITY_FOG_COORDS(2)
                    #ifdef SOFTPARTICLES_ON
                    float4 projPos : TEXCOORD3;
                    #endif
                };

                float4 _MainTex_ST;

                v2f vert(appdata_t IN)
                {
                    v2f v;
                    v.vertex = UnityObjectToClipPos(IN.vertex);
                    v.worldPosition = IN.vertex;
                    #ifdef SOFTPARTICLES_ON
                    v.projPos = ComputeScreenPos(v.vertex);
                    COMPUTE_EYEDEPTH(v.projPos.z);
                    #endif
                    v.color = IN.color;
                    v.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                    UNITY_TRANSFER_FOG(v, v.vertex);

                    return v;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    fixed4 col = i.color * _TintColor * tex2D(_MainTex, i.texcoord);

                    // ==== SOFTMASKABLE START ====
                    #if SOFTMASKABLE
                    col *= SoftMask(i.vertex, i.worldPosition, col.a);
                    #endif
                    // ==== SOFTMASKABLE END ====

                    return col;
                }
                ENDCG
            }
        }
    }
}
