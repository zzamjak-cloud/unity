Shader "CAT/2D/SpriteColorLerp"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _TargetColor ("Target Color", Color) = (1,0,0,1)
        _LerpValue ("Lerp Value", Range(0, 1)) = 0
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            fixed4 _TargetColor;
            float _LerpValue;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // 타겟 색상 준비 (알파값은 원본 유지)
                fixed4 targetColor = _TargetColor;
                targetColor.a = c.a;
                
                // Lerp를 사용한 색상 블렌딩
                fixed4 finalColor = lerp(c, targetColor, _LerpValue);
                
                // 프리멀티플라이드 알파 적용
                finalColor.rgb *= finalColor.a;
                
                return finalColor;
            }
            ENDCG
        }
    }
}