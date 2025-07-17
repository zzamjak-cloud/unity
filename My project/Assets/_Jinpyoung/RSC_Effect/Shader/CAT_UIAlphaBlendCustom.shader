Shader "CAT/Particles/UIAlphaBlendCustom"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 customData1 : TEXCOORD1;
                float4 customData2 : TEXCOORD2; // CustomData2는 여전히 정의되어 있지만, 사용 여부를 결정
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 customData1 : TEXCOORD1;
                float4 customData2 : TEXCOORD2;
                float4 projPos : TEXCOORD3;
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
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenPos = i.projPos.xy / i.projPos.w;

                // Sample main texture and apply dissolve effect
                fixed4 col = tex2D(_MainTex, i.uv);
                float dissolveValue = i.customData1.x;
                float dissolveSharpness = i.customData1.y;
                float emissivePower = i.customData1.z;
                fixed4 finalCol;
                float lerpFactor = col.b;   // Secondary Color Factor

                col.a *= smoothstep(dissolveValue - dissolveSharpness, dissolveValue + dissolveSharpness, col.g);
                col.a *= i.color.a;

                finalCol.rgb = col.r * i.color.rgb;     // Particle Color
                finalCol.a = col.a;                     // Particle Alpha

                // Apply emissive power based on custom data
                finalCol.rgb += finalCol.rgb * emissivePower;

                // Check if CustomData2 is valid (e.g., if the alpha channel is not zero)
                if (i.customData2.a > 0.0) // CustomData2의 알파 값이 0보다 큰 경우에만 사용
                {
                    // Blend with secondary color from custom data
                    finalCol.rgb = lerp(finalCol.rgb, i.customData2.rgb, 1 - lerpFactor);
                }

                return finalCol;
            }
            ENDCG
        }
    }
}
