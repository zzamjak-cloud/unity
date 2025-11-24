Shader "CAT/Effects/SpriteDissolve"
{
    Properties {
        [PerRendererData] _MainTex ("Main texture", 2D) = "white" {}
        _DissolveTex ("Dissolution texture", 2D) = "gray" {}
        _DissolveScale ("Dissolve Scale", Vector) = (1, 1, 0, 0)
        _Threshold ("Threshold", Range(0., 1.01)) = 0.
    }
    SubShader {
        Tags { "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            sampler2D _MainTex;
            v2f vert(appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            sampler2D _DissolveTex;
            float4 _DissolveScale;
            float _Threshold;
            fixed4 frag(v2f i) : SV_Target {
                float4 c = tex2D(_MainTex, i.uv);
                // 스케일을 적용한 UV 좌표 계산
                float2 dissolveUV = i.uv * _DissolveScale.xy;
                float val = tex2D(_DissolveTex, dissolveUV).r;
                c.a *= step(_Threshold, val);
                return c;
            }
            ENDCG
        }
    }
}