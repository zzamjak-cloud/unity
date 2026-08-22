Shader "CAT/Effect/SpriteGradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color1 ("Color 1", Color) = (1,1,1,1)
        _Color2 ("Color 2", Color) = (0,0,0,1)
        _GradientDirection ("Gradient Direction", Float) = 0.0  // 0: Vertical, 1: Horizontal
        _LerpValue ("Lerp Value (Color2 비중)", Range(0, 1)) = 0.5

        // 아틀라스 내 스프라이트 UV 영역. xy = offset, zw = size. SpriteGradient가 렌더러 단위로 채운다.
        [HideInInspector] _SpriteRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)

        // Android ETC1 분리 알파용. SpriteRenderer가 렌더러 단위로 채워준다.
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma target 2.0
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

            // SRP Batcher 호환: 머티리얼 단위 상수는 전부 UnityPerMaterial 안에 모아야 한다.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                fixed4 _Color1;
                fixed4 _Color2;
                float4 _SpriteRect;
                float  _GradientDirection;
                float  _LerpValue;
                float  _EnableExternalAlpha;
            CBUFFER_END

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA
                // ETC1은 알파를 담지 못하므로 별도 텍스처의 R 채널을 알파로 사용한다.
                fixed4 alpha = tex2D(_AlphaTex, uv);
                color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
                #endif

                return color;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 원본 텍스처 샘플링
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                // 아틀라스 UV를 스프라이트 로컬 UV(0~1)로 정규화한다.
                // 이렇게 하지 않으면 아틀라스 내 위치에 해당하는 좁은 구간만 사용되어 그라디언트가 거의 단색이 된다.
                float2 localUV = saturate((IN.texcoord - _SpriteRect.xy) / max(_SpriteRect.zw, 1e-5));

                // Vertical: UV.y 사용, Horizontal: UV.x 사용
                float gradientFactor = lerp(localUV.y, localUV.x, _GradientDirection);

                // _LerpValue = Color2의 비중. 0.5면 5:5, 0.2면 Color1 80% / Color2 20%.
                // 두 컬러가 50:50이 되는 지점을 (1 - _LerpValue) 위치로 옮기는 midpoint 리맵(Schlick bias).
                // f/((1/w-2)(1-f)+1) 를 f*w/((1-2w)(1-f)+w) 로 정리해 역수 연산을 1회로 줄였다. (모바일 최적화)
                half w = clamp(_LerpValue, 0.001, 0.999);
                half denom = (1.0 - 2.0 * w) * (1.0 - gradientFactor) + w;
                half blend = saturate(gradientFactor * w / denom);

                fixed4 finalGradient = lerp(_Color1, _Color2, blend);

                // 원본 텍스처와 Gradient를 Multiply
                fixed4 finalColor = c * finalGradient;

                // 프리멀티플라이드 알파 적용
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }

    // 셰이더 컴파일 실패 시 마젠타 대신 기본 스프라이트로 렌더링되도록 한다.
    Fallback "Sprites/Default"
}
