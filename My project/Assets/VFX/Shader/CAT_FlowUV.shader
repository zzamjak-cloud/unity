Shader "CAT/Particles/FlowUV" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _FlowMultiplier ("Flow Multiplier", Float) = 1.0
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
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 custom1 : TEXCOORD1; // CustomData1
                float4 color : COLOR;
            };
            
            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 custom1 : TEXCOORD1;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FlowMultiplier;
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // 여기가 중요합니다: Custom1.y를 사용하여 UV의 y 오프셋 적용
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.y = o.uv.y - v.custom1.y * _FlowMultiplier;
                o.custom1 = v.custom1;
                o.color = v.color;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                return col;
            }
            ENDCG
        }
    }
}