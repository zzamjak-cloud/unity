Shader "CAT/Particles/FlowUV" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _FlowMultiplier ("Flow Multiplier", Float) = 1.0

        // SoftMask
        [HideInInspector] _MaskTex("Mask Texture", 2D) = "white" {}
        [HideInInspector] _Softness("Softness", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask("Invert Mask", Float) = 0
        [HideInInspector] _MaskUVRect("Mask UV Rect", Vector) = (0, 0, 1, 1)
        [HideInInspector] _MaskTex2("Mask Texture 2", 2D) = "white" {}
        [HideInInspector] _Softness2("Softness 2", Range(0, 1)) = 0.1
        [HideInInspector] _InvertMask2("Invert Mask 2", Float) = 0
        [HideInInspector] _MaskUVRect2("Mask UV Rect 2", Vector) = (0, 0, 1, 1)
    }

    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ _CAT_SOFTMASK
            #pragma multi_compile_local _ _SOFTMASK_NESTED

            #include "UnityCG.cginc"
            #include "CAT_SoftMask.cginc"

            struct appdata {
                float4 vertex  : POSITION;
                float2 uv      : TEXCOORD0;
                float4 custom1 : TEXCOORD1;
                float4 color   : COLOR;
            };

            struct v2f {
                float2 uv      : TEXCOORD0;
                float4 vertex  : SV_POSITION;
                float4 custom1 : TEXCOORD1;
                float4 color   : COLOR;
                CAT_SOFTMASK_COORDS(2, 3)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FlowMultiplier;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.y = o.uv.y - v.custom1.y * _FlowMultiplier;
                o.custom1 = v.custom1;
                o.color = v.color;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                CAT_SOFTMASK_VERT(worldPos, o)

                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // SoftMask
                col.a *= CAT_SOFTMASK_FRAG(i);

                return col;
            }
            ENDCG
        }
    }
}
