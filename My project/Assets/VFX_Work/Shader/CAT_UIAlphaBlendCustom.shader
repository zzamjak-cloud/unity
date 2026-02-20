Shader "CAT/Particles/UIAlphaBlendCustom"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}

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

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ _CAT_SOFTMASK
            #pragma multi_compile_local _ _SOFTMASK_NESTED

            #include "UnityCG.cginc"
            #include "CAT_SoftMask.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex     : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float4 customData1 : TEXCOORD1;
                float4 customData2 : TEXCOORD2;
            };

            struct v2f
            {
                float4 vertex     : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float4 customData1 : TEXCOORD1;
                float4 customData2 : TEXCOORD2;
                float4 projPos    : TEXCOORD3;
                CAT_SOFTMASK_COORDS(4, 5)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.customData1 = v.customData1;
                o.customData2 = v.customData2;
                o.projPos = ComputeScreenPos(o.vertex);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                CAT_SOFTMASK_VERT(worldPos, o)

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenPos = i.projPos.xy / i.projPos.w;

                fixed4 col = tex2D(_MainTex, i.uv);
                float dissolveValue = i.customData1.x;
                float dissolveSharpness = i.customData1.y;
                float emissivePower = i.customData1.z;
                fixed4 finalCol;
                float lerpFactor = col.b;

                col.a *= smoothstep(dissolveValue - dissolveSharpness, dissolveValue + dissolveSharpness, col.g);
                col.a *= i.color.a;

                finalCol.rgb = col.r * i.color.rgb;
                finalCol.a = col.a;

                finalCol.rgb += finalCol.rgb * emissivePower;

                if (i.customData2.a > 0.0)
                {
                    finalCol.rgb = lerp(finalCol.rgb, i.customData2.rgb, 1 - lerpFactor);
                }

                // SoftMask
                finalCol.a *= CAT_SOFTMASK_FRAG(i);

                return finalCol;
            }
            ENDCG
        }
    }
}
