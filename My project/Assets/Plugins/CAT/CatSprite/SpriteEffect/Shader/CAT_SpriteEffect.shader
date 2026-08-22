Shader "CAT/Effects/SpriteEffect"
{
    // SpriteEffect(단일)와 SpriteGroupEffect(그룹)가 함께 쓰는 통합 셰이더.
    // 기능을 추가할 때는 여기 한 곳만 고치면 양쪽 컴포넌트가 같이 지원하게 된다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _TargetColor ("Target Color", Color) = (1,1,1,1)
        _LerpValue ("Color Lerp", Range(0, 1)) = 0

        _DissolveTex ("Dissolve Texture", 2D) = "gray" {}
        _DissolveScale ("Dissolve Scale", Vector) = (1, 1, 0, 0)
        _Threshold ("Dissolve Threshold", Range(0, 1)) = 0

        // 아틀라스 내 스프라이트 UV 영역. xy = offset, zw = size. 단일 컴포넌트가 렌더러 단위로 채운다.
        [HideInInspector] _SpriteRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)

        // Android ETC1 분리 알파용. SpriteRenderer가 렌더러 단위로 채워준다.
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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

            // 디졸브만 키워드로 분기한다. 텍스처 페치 1회 + 보간기 1개 + 정점 변환이 실비용이라
            // 쓰지 않을 때는 완전히 빼는 편이 낫다.
            // 반대로 컬러 Lerp는 lerp 3회(MAD)뿐이라 상시 켜 두는 편이 변형 수를 줄여 이득이다.
            #pragma multi_compile_local _ _CAT_DISSOLVE

            // 디졸브 UV의 기준 좌표계. 켜면 그룹 로컬(SpriteGroupEffect), 끄면 스프라이트 로컬(SpriteEffect).
            #pragma multi_compile_local _ _CAT_GROUPUV

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
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
                #if _CAT_DISSOLVE
                float2 dissolveUV : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            sampler2D _DissolveTex;

            // SRP Batcher 호환: 머티리얼 단위 상수는 전부 UnityPerMaterial 안에 모아야 한다.
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                fixed4 _TargetColor;

                // 월드 좌표 -> 디졸브 UV 변환. 그룹 바운즈 정규화와 타일 스케일까지 CPU에서 합쳐 넣는다.
                // 파츠(자식 스프라이트)마다 로컬 UV를 쓰면 팔·다리·몸통이 제각각 녹아버리므로,
                // 그룹 전체를 하나의 좌표계로 묶어 패턴이 캐릭터를 가로질러 이어지게 한다.
                float4x4 _GroupMatrix;

                float4 _DissolveScale;
                float4 _SpriteRect;
                float  _LerpValue;
                float  _Threshold;
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

                #if _CAT_DISSOLVE
                    #if _CAT_GROUPUV
                    // 다이나믹 배칭 시 정점은 이미 월드 공간이고 unity_ObjectToWorld가 단위행렬이므로
                    // 배칭 여부와 무관하게 같은 결과가 나온다.
                    float3 worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                    OUT.dissolveUV = mul(_GroupMatrix, float4(worldPos, 1.0)).xy;
                    #else
                    // 아틀라스 UV를 스프라이트 로컬 UV(0~1)로 정규화한다.
                    // 이렇게 하지 않으면 아틀라스 내 위치/크기에 따라 패턴이 어긋나고 늘어난다.
                    float2 localUV = (OUT.texcoord - _SpriteRect.xy) / max(_SpriteRect.zw, 1e-5);
                    OUT.dissolveUV = localUV * _DissolveScale.xy;
                    #endif
                #endif

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
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                #if _CAT_DISSOLVE
                half val = tex2D(_DissolveTex, IN.dissolveUV).r;
                // Threshold가 1일 때 노이즈 최대값(1)까지 확실히 잘리도록 컷오프를 살짝 위로 민다.
                c.a *= step(_Threshold * 1.0001, val);
                #endif

                // 알파는 유지하고 RGB만 타겟 컬러로 보간한다. (하얗게 태우기 등)
                c.rgb = lerp(c.rgb, _TargetColor.rgb, _LerpValue);

                // 프리멀티플라이드 알파 적용
                c.rgb *= c.a;

                return c;
            }
            ENDCG
        }
    }

    // 셰이더 컴파일 실패 시 마젠타 대신 기본 스프라이트로 렌더링되도록 한다.
    Fallback "Sprites/Default"
}
