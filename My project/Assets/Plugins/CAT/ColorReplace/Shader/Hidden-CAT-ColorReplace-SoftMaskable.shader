// [OptionalShader] com.coffee.softmask-for-ugui: CAT/Effects/ColorReplace
Shader "Hidden/CAT/Effects/ColorReplace (SoftMaskable)"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        _HSVRangeMin("HSV Affect Min", Range(0, 1)) = 0
        _HSVRangeMax("HSV Affect Max", Range(0, 1)) = 1
        _HSVAAdjust("HSVA Adjust", Vector) = (0, 0, 0, 0)

        [HideInInspector] _StencilComp("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma target 2.0

            // mob-sakai SoftMaskable
            #if SOFTMASKABLE
            #include "Packages/com.coffee.softmask-for-ugui/Shaders/SoftMask.cginc"
            #pragma shader_feature _ SOFTMASK_EDITOR // Add for soft mask
            #pragma shader_feature_local _ SOFTMASKABLE // Add for soft mask
            #endif

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _HSVRangeMin;
            half _HSVRangeMax;
            half4 _HSVAAdjust;
            float4 _ClipRect;

            // RGB -> HSV 변환 (half precision)
            inline half3 RGB2HSV(half3 c)
            {
                half4 K = half4(0.0h, -1.0h / 3.0h, 2.0h / 3.0h, -1.0h);
                half4 p = lerp(half4(c.bg, K.wz), half4(c.gb, K.xy), step(c.b, c.g));
                half4 q = lerp(half4(p.xyw, c.r), half4(c.r, p.yzx), step(p.x, c.r));
                half d = q.x - min(q.w, q.y);
                half e = 1.0e-4h;
                return half3(abs(q.z + (q.w - q.y) / (6.0h * d + e)), d / (q.x + e), q.x);
            }

            // HSV -> RGB 변환 (half precision)
            inline half3 HSV2RGB(half3 c)
            {
                c = half3(c.x, saturate(c.yz));
                half4 K = half4(1.0h, 2.0h / 3.0h, 1.0h / 3.0h, 3.0h);
                half3 p = abs(frac(c.xxx + K.xyz) * 6.0h - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            // HSV 범위 체크 (분기 없이 수학 연산)
            inline half ComputeAffectMult(half hue, half rangeMin, half rangeMax)
            {
                half isWrapped = step(rangeMax + 0.001h, rangeMin);
                half normalCase = step(rangeMin, hue) * step(hue, rangeMax);
                half wrappedCase = saturate(step(rangeMin, hue) + step(hue, rangeMax));
                return lerp(normalCase, wrappedCase, isWrapped);
            }

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
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.texcoord) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001h);
                #endif

                half3 hsv = RGB2HSV(color.rgb);
                half affectMult = ComputeAffectMult(hsv.x, _HSVRangeMin, _HSVRangeMax);
                half3 rgb = HSV2RGB(hsv + _HSVAAdjust.xyz * affectMult);

                half4 finalColor = half4(rgb, color.a + _HSVAAdjust.w);

                // SoftMask 적용
                #if SOFTMASKABLE
                finalColor.a *= SoftMask(IN.vertex, IN.worldPosition, finalColor.a);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
