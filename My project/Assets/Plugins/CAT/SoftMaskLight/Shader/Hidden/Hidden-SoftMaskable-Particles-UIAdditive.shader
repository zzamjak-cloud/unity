// [OptionalShader] com.coffee.softmask-for-ugui: SoftMaskLight/Particles/UIAdditive
// mob-sakai SoftMask 대응 파티클 Additive 셰이더
Shader "Hidden/SoftMaskLight/Particles/UIAdditive (SoftMaskable)"
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

        Blend SrcAlpha One, One One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // ==== SOFTMASKABLE START (mob-sakai SoftMaskable) ====
            #if SOFTMASKABLE
            #include "Packages/com.coffee.softmask-for-ugui/Shaders/SoftMask.cginc"
            #pragma shader_feature _ SOFTMASK_EDITOR // Add for soft mask
            #pragma shader_feature_local _ SOFTMASKABLE // Add for soft mask
            #endif
            // ==== SOFTMASKABLE END ====

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                half2 texcoord       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            sampler2D _MainTex;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.worldPosition = IN.vertex;
                OUT.texcoord = IN.texcoord;
                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
                #endif
                OUT.color = IN.color * _Color;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;
                color.rgb *= color.a;

                // ==== SOFTMASKABLE START ====
                #if SOFTMASKABLE
                color *= SoftMask(IN.vertex, IN.worldPosition, color.a);
                #endif
                // ==== SOFTMASKABLE END ====

                clip(color.a - 0.01);
                return color;
            }
            ENDCG
        }
    }
}
