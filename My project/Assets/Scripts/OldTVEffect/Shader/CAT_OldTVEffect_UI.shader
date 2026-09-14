Shader "CAT/Effects/OldTVEffect_UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.5
        _NoiseScale ("Noise Scale", Range(0.1, 10.0)) = 3.0
        _ScanLineIntensity ("Scan Line Intensity", Range(0, 1)) = 0.5
        _ScanLineCount ("Scan Line Count", Float) = 100
        [HideInInspector] _ScanLineThicknessInv ("Scan Line Thickness (Inverse)", Float) = 1.0
        _VerticalJitter ("Vertical Jitter", Range(0, 0.1)) = 0.01
        _HorizontalJitter ("Horizontal Jitter", Range(0, 0.1)) = 0.01
        _ColorBleed ("Color Bleed", Range(0, 0.5)) = 0.1
        _ColorBleedOffset ("Color Bleed Offset", Range(0, 0.1)) = 0.02
        _RollSpeed ("Roll Speed", Range(0, 5)) = 1.0
        _Saturation ("Saturation", Range(0, 1)) = 1.0
        [HideInInspector] _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)

        [Toggle(_COLORBLEED_ON)] _ColorBleedOn ("Color Bleed Enabled", Float) = 1
        [Toggle(_SCREENSPACE_SCANLINES)] _ScreenSpaceScanLines ("Screen Space Scan Lines", Float) = 0

        // UI 셰이더 필수 속성
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex TVUIVert
            #pragma fragment TVUIFrag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            // 런타임 생성 머티리얼만 사용하므로 shader_feature 는 빌드에서 스트립됨 → multi_compile 사용
            #pragma multi_compile_local _ _COLORBLEED_ON
            #pragma multi_compile_local _ _SCREENSPACE_SCANLINES

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "OldTVEffectCommon.cginc"

            struct appdata_ui
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_ui
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float2 noiseUV       : TEXCOORD1;
                float4 mask          : TEXCOORD2; // RectMask2D 소프트 클리핑 (UI-Default 와 동일)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f_ui TVUIVert(appdata_ui v)
            {
                v2f_ui o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 vPosition = UnityObjectToClipPos(v.vertex);
                o.vertex = vPosition;
                o.color = v.color;

                // UI-Default 와 동일한 소프트 클리핑 좌표 계산
                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw,
                                0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                TV_ComputeUVs(TRANSFORM_TEX(v.uv, _MainTex), o.uv, o.noiseUV);
                return o;
            }

            fixed4 TVUIFrag(v2f_ui i) : SV_Target
            {
                float2 uv = TV_ClampUV(i.uv);

                fixed4 col = tex2D(_MainTex, uv);

                #ifdef _COLORBLEED_ON
                fixed bleedR = tex2D(_MainTex, TV_ClampUV(uv + float2(_ColorBleedOffset, 0))).r;
                fixed bleedB = tex2D(_MainTex, TV_ClampUV(uv - float2(_ColorBleedOffset * 0.5, 0))).b;
                col.rgb = TV_ApplyColorBleed(col.rgb, bleedR, bleedB);
                #endif

                col.rgb = TV_ApplyPostEffects(col.rgb, i.noiseUV, uv.y, i.vertex);

                col *= i.color;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                col.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
