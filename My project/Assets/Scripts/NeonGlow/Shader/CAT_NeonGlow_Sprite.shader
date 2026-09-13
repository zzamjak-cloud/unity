Shader "CAT/Effects/NeonGlow Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Neon SDF (R)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Tube)]
        [HDR] _TubeColor ("Tube Color", Color) = (1, 0.25, 0.45, 1)
        [HDR] _CoreColor ("Core Color", Color) = (1, 0.92, 0.95, 1)
        _TubeWidth ("Tube Width", Range(0.01, 1)) = 0.2
        _TubeShading ("Tube Shading", Range(0, 1)) = 0.6
        _CoreWidth ("Core Width", Range(0, 1)) = 0.55
        _CoreSoftness ("Core Softness", Range(0.001, 1)) = 0.3
        _TubeIntensity ("Tube Intensity", Range(0, 4)) = 1.0
        _TubeOpacity ("Tube Opacity", Range(0, 1)) = 1.0

        [Header(Inner Glow)]
        [HDR] _InnerGlowColor ("Inner Glow Color", Color) = (1, 0.2, 0.4, 1)
        _InnerGlowRadius ("Inner Glow Radius", Range(0.01, 1)) = 0.2
        _InnerGlowFalloff ("Inner Glow Falloff", Range(0.5, 8)) = 2.0
        _InnerGlowIntensity ("Inner Glow Intensity", Range(0, 8)) = 1.6

        [Header(Outer Glow)]
        [HDR] _OuterGlowColor ("Outer Glow Color", Color) = (0.9, 0.1, 0.5, 1)
        _OuterGlowRadius ("Outer Glow Radius", Range(0.01, 1)) = 0.9
        _OuterGlowFalloff ("Outer Glow Falloff", Range(0.5, 8)) = 3.0
        _OuterGlowIntensity ("Outer Glow Intensity", Range(0, 8)) = 0.8

        [Header(Global)]
        _Exposure ("Exposure (HDR)", Range(0, 8)) = 1.5
        [HideInInspector] _GlowCutoff ("Glow Cutoff", Range(0, 1)) = 1.0
        [HideInInspector] _UsePreviewTime ("Use Preview Time", Float) = 0
        [HideInInspector] _PreviewTime ("Preview Time", Float) = 0

        [Header(Flicker)]
        _FlickerMode ("Flicker Mode", Range(0, 3)) = 0
        _FlickerSpeed ("Flicker Speed", Range(0.1, 10)) = 1.0
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.5
        [Toggle] _FlickerGlowOnly ("Flicker Glow Only", Float) = 0
        _WarmupDuration ("Warmup Duration", Range(0.1, 10)) = 2.0
        _StartTime ("Start Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        // 프리멀티플라이드 알파: 글로우는 가산, 튜브 몸통만 배경을 덮는다.
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "NeonGlowSprite"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "CAT_NeonGlowCore.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // xy = UV, z = 정점에서 계산한 플리커 값
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.z = CatNeonFlicker(CatNeonTime());
                o.color = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return CatNeonShade(i.uv.xy, (half)i.uv.z, i.color);
            }
            ENDCG
        }
    }

    FallBack Off
}
