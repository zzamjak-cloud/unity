Shader "CAT/Effect/SpriteGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color1 ("Color 1", Color) = (1,1,1,1)
        _Color2 ("Color 2", Color) = (0,0,0,1)
        _GradientDirection ("Gradient Direction", Float) = 0.0  // 0: Vertical, 1: Horizontal
        _LerpValue ("Lerp Value", Range(0, 1)) = 0.0
        
        // Sprite 셰이더 필수 속성
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        
        // 일반 블렌딩 옵션들
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
            fixed4 _Color1;
            fixed4 _Color2;
            float _GradientDirection;
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
                // 원본 텍스처 샘플링
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // Gradient 계산
                // Vertical: UV.y 사용, Horizontal: UV.x 사용
                float gradientFactor = lerp(IN.texcoord.y, IN.texcoord.x, _GradientDirection);
                
                // 두 컬러 사이의 Gradient
                fixed4 gradientColor = lerp(_Color1, _Color2, gradientFactor);
                
                // Lerp를 사용하여 원본과 Gradient 사이를 보간
                fixed4 finalGradient = lerp(_Color1, gradientColor, _LerpValue);
                
                // 원본 텍스처와 Gradient를 Multiply
                fixed4 finalColor = c * finalGradient;
                
                // 프리멀티플라이드 알파 적용
                finalColor.rgb *= finalColor.a;
                
                return finalColor;
            }
            ENDCG
        }
    }
}

