Shader "CAT/UI/TMP_Outline"
{
    Properties
    {
        // TMP 기본 Properties
        _MainTex ("Font Atlas", 2D) = "white" {}
        _FaceTex ("Face Texture", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1,1,1,1)

        // Outline Properties
        [Header(Outline Settings)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 1)) = 0.1
        _OutlineSoftness ("Outline Softness", Range(0, 1)) = 0.05

        // UI Properties
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // TMP Textures
            sampler2D _MainTex;
            sampler2D _FaceTex;
            float4 _MainTex_ST;

            // TMP Colors
            half4 _FaceColor;

            // Outline Properties
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineSoftness;

            // UI
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // TMP SDF 샘플링
                half dist = tex2D(_MainTex, i.uv).a;

                // Outline 계산 (SDF distance 기반)
                // dist: 0 = 텍스트 외부, 0.5 = 경계, 1 = 텍스트 내부
                half outlineStart = 0.5h - _OutlineWidth - _OutlineSoftness;
                half outlineEnd = 0.5h - _OutlineWidth;
                half fillStart = 0.5h - _OutlineSoftness;
                half fillEnd = 0.5h;

                // Outline 영역 (0.5 - width - softness ~ 0.5 - width)
                half outline = smoothstep(outlineStart, outlineEnd, dist);

                // 텍스트 본체 영역 (0.5 - softness ~ 0.5)
                half fill = smoothstep(fillStart, fillEnd, dist);

                // Face Texture (선택적)
                half4 faceColor = tex2D(_FaceTex, i.uv) * _FaceColor;

                // 색상 블렌딩 (Outline → Text)
                half4 color = lerp(_OutlineColor, i.color * faceColor, fill);

                // 최종 알파 (Outline 영역 전체)
                color.a *= outline;

                // UI Clipping
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                // Alpha Clip
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                return color;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
