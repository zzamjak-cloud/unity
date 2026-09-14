Shader "CAT/Effects/OldTVEffect_Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
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
        // 아틀라스 이웃 스프라이트 샘플링 방지용 UV 클램프 범위 (xmin, ymin, xmax, ymax)
        [HideInInspector] _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)

        [Toggle(_COLORBLEED_ON)] _ColorBleedOn ("Color Bleed Enabled", Float) = 1
        [Toggle(_SCREENSPACE_SCANLINES)] _ScreenSpaceScanLines ("Screen Space Scan Lines", Float) = 0

        // Sprite 셰이더 필수 속성
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex TVSpriteVert
            #pragma fragment TVSpriteFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            // 런타임 생성 머티리얼만 사용하므로 shader_feature 는 빌드에서 스트립됨 → multi_compile 사용
            #pragma multi_compile_local _ _COLORBLEED_ON
            #pragma multi_compile_local _ _SCREENSPACE_SCANLINES

            #include "UnitySprites.cginc"
            #include "UnityCG.cginc"
            #include "OldTVEffectCommon.cginc"

            struct v2f_tv
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 noiseUV  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_tv TVSpriteVert(appdata_t IN)
            {
                v2f_tv OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(OUT.vertex);
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                // 지터·노이즈 UV 는 버텍스 단계에서 계산 → 프래그먼트의 종속 텍스처 읽기 제거
                TV_ComputeUVs(IN.texcoord, OUT.texcoord, OUT.noiseUV);

                return OUT;
            }

            fixed4 TVSpriteFrag(v2f_tv IN) : SV_Target
            {
                float2 uv = TV_ClampUV(IN.texcoord);

                fixed4 col = SampleSpriteTexture(uv);

                #ifdef _COLORBLEED_ON
                fixed bleedR = SampleSpriteTexture(TV_ClampUV(uv + float2(_ColorBleedOffset, 0))).r;
                fixed bleedB = SampleSpriteTexture(TV_ClampUV(uv - float2(_ColorBleedOffset * 0.5, 0))).b;
                col.rgb = TV_ApplyColorBleed(col.rgb, bleedR, bleedB);
                #endif

                col.rgb = TV_ApplyPostEffects(col.rgb, IN.noiseUV, uv.y, IN.vertex);

                col *= IN.color;

                // 프리멀티플라이드 알파 (Sprites/Default 와 동일)
                col.rgb *= col.a;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
